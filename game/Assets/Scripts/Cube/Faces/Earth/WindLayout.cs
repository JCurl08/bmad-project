using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>A tile of a wind screen's grid (4.11).</summary>
    public enum WindTile : byte
    {
        /// <summary>Open sky (the chasm): nobody walks onto it.</summary>
        Sky = 0,

        /// <summary>Solid ground: the ring path round the screen and the floating islands.</summary>
        Floor = 1,

        /// <summary>A rock wall standing on an island: solid (it splits a twin island into two regions).</summary>
        Wall = 2,

        /// <summary>A wind stream: it carries the player along its arrow (WindLayout's direction for the cell).</summary>
        Stream = 3,

        /// <summary>A grip anchor inside a stream: a ride stops on it and control comes back.</summary>
        Grip = 4,

        /// <summary>The goal pad on an island, in front of the reward chest.</summary>
        Goal = 5,
    }

    /// <summary>
    /// An Earth wind-stream screen as pure data (4.11): a 21 x 13 grid of one-unit tiles centred on the screen (cell (10, 6)
    /// on the centre), inside the half-tile edge walls. Its border is the ring path (floor), which every doorway opens onto, so
    /// all doorways share one region and a crossing never needs a ride. Inside is the sky with floating islands (some split
    /// by a rock wall into twin halves), the goal pad on one of them, and the wind streams between them.
    /// Movement is the solver's own (Ride): a region is the floor (and grip, and the goal) the player walks over without
    /// riding; stepping from a region into a stream tile, from any side, sets off a ride that follows the arrows, with no
    /// steering, until the first tile that is not a stream: floor, the goal pad or a grip anchor, where control comes back.
    /// A ride that would only bring the player back where it set off is no move (the tail of a stream that ends here).
    /// Streams never end over the sky or against a wall, never merge and bend at most MaxBends times; only their first tile
    /// touches the land they leave and only their last the land they reach (Problems checks it all).
    /// The solver (PuzzleGraph) treats each region as one resting place; Measure gives the research note's metrics from a
    /// doorway (WindMetrics); Generate searches for a layout inside every target of its tier (WindTargets). Regions for
    /// DoorwayReach: 0 is everywhere the doorways reach, 1 is the goal pad's island, as on the ice screens.
    /// </summary>
    public sealed class WindLayout : IScreenRegions
    {
        public const int Cols = 21, Rows = 13, Cells = Cols * Rows;
        public const int CentreX = Cols / 2, CentreY = Rows / 2;

        /// <summary>The ring's resting place (its first cell, the bottom-left corner): every doorway's.</summary>
        public const int HomeNode = 0;

        /// <summary>The most turns one stream takes (research note 3b).</summary>
        public const int MaxBends = 2;

        /// <summary>Where islands may lie: one sky cell off the ring all round.</summary>
        public static readonly RectInt Zone = new RectInt(2, 2, Cols - 4, Rows - 4);

        private static readonly int[] Dx = { 0, 1, 0, -1 }, Dy = { 1, 0, -1, 0 }; // Facing order: North, East, South, West

        private readonly byte[] tiles = new byte[Cells];
        private readonly byte[] dirs = new byte[Cells];
        private readonly bool[] doors = new bool[4];
        private readonly PuzzleGraph.MoveSource source;

        // The analysis (regions, stream starts), redone after any change.
        private readonly int[] region = new int[Cells];
        private readonly int[] inflow = new int[Cells];
        private readonly List<List<int>> regionCells = new List<List<int>>();
        private readonly List<int> regionNodes = new List<int>();
        private readonly List<int> heads = new List<int>();
        private int regionCount;
        private bool analysed;
        private readonly int[] stamp = new int[Cells];
        private int stampNow;
        private readonly bool[] used = new bool[Cells];
        private readonly Queue<int> labelQueue = new Queue<int>();

        // Settled once the layout is final (DoorwayReach).
        private bool[] reachable;
        private bool solvable, goalSafe;

        public PuzzleTier Tier { get; }
        public WindTargets Targets => WindTargets.For(Tier);

        /// <summary>The goal pad's cell index (y * Cols + x), or -1.</summary>
        public int Goal { get; private set; } = -1;

        /// <summary>How many search steps the generator took (0 when made by hand).</summary>
        public int SearchSteps { get; private set; }

        /// <summary>True when the search ran out and the tier's template stands in (the sweep reports it).</summary>
        public bool FromTemplate { get; private set; }

        private WindLayout(PuzzleTier tier, Func<Facing, bool> hasDoorway)
        {
            Tier = tier;
            source = Moves;
            for (int f = 0; f < 4; f++) doors[f] = hasDoorway == null || hasDoorway((Facing)f);
            for (int i = 0; i < Cells; i++) tiles[i] = (byte)(IsRing(i) ? WindTile.Floor : WindTile.Sky);
        }

        // ------------------------------------------------------------------ Geometry

        public static Vector2Int Pos(int cell) => new Vector2Int(cell % Cols, cell / Cols);
        public static int Index(Vector2Int p) => p.y * Cols + p.x;
        public static bool InGrid(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < Cols && p.y < Rows;
        public static Vector2Int Step(Facing f) => new Vector2Int(Dx[(int)f], Dy[(int)f]);

        /// <summary>True for the border cells: the ring path.</summary>
        public static bool IsRing(int cell)
        {
            int x = cell % Cols, y = cell / Cols;
            return x == 0 || y == 0 || x == Cols - 1 || y == Rows - 1;
        }

        /// <summary>The middle of a cell, local to the screen's centre (cells are one unit; (10, 6) is the centre).</summary>
        public static Vector2 CellLocal(int cell)
        {
            Vector2Int p = Pos(cell);
            return new Vector2(p.x - CentreX, p.y - CentreY);
        }

        /// <summary>The grid's extent, local to the screen's centre.</summary>
        public static Rect GridRect => Rect.MinMaxRect(-CentreX - 0.5f, -CentreY - 0.5f, CentreX + 0.5f, CentreY + 0.5f);

        /// <summary>The cell a point (local) lies in, or -1 off the grid.</summary>
        public static int CellAt(Vector2 local)
        {
            var p = new Vector2Int(Mathf.RoundToInt(local.x) + CentreX, Mathf.RoundToInt(local.y) + CentreY);
            if (!GridRect.Contains(local) || !InGrid(p)) return -1;
            return Index(p);
        }

        /// <summary>The ring cell in the middle of a doorway (where a teleport lands).</summary>
        public static int DoorCell(Facing side)
        {
            switch (side)
            {
                case Facing.North: return Index(new Vector2Int(CentreX, Rows - 1));
                case Facing.East: return Index(new Vector2Int(Cols - 1, CentreY));
                case Facing.South: return Index(new Vector2Int(CentreX, 0));
                default: return Index(new Vector2Int(0, CentreY));
            }
        }

        /// <summary>
        /// The doorway a ring cell opens onto, or null: the cell is on that side's border, within the doorway's width (the middle
        /// cell and one either side), so stepping off the grid there leads out through it.
        /// </summary>
        public static Facing? DoorwayOf(int cell)
        {
            Vector2Int p = Pos(cell);
            if (p.y == Rows - 1 && Mathf.Abs(p.x - CentreX) <= 1) return Facing.North;
            if (p.y == 0 && Mathf.Abs(p.x - CentreX) <= 1) return Facing.South;
            if (p.x == Cols - 1 && Mathf.Abs(p.y - CentreY) <= 1) return Facing.East;
            if (p.x == 0 && Mathf.Abs(p.y - CentreY) <= 1) return Facing.West;
            return null;
        }

        public bool HasDoor(Facing side) => doors[(int)side];

        /// <summary>
        /// True for the three sky tiles just inside a doorway's width of the ring (4.11 review): no stream ever lies there, so a
        /// player walking in by a doorway, key held, can never be carried off by surprise.
        /// </summary>
        public static bool IsDoorFront(int cell)
        {
            Vector2Int p = Pos(cell);
            if (Mathf.Abs(p.x - CentreX) <= 1 && (p.y == 1 || p.y == Rows - 2)) return true;
            return Mathf.Abs(p.y - CentreY) <= 1 && (p.x == 1 || p.x == Cols - 2);
        }

        /// <summary>The doorways the screen connects (at 3x3 every side has one).</summary>
        public List<Facing> Doors()
        {
            var list = new List<Facing>();
            for (int f = 0; f < 4; f++)
                if (doors[f]) list.Add((Facing)f);
            return list;
        }

        public WindTile TileAt(int cell) => (WindTile)tiles[cell];

        /// <summary>A stream cell's direction (meaningless for other tiles).</summary>
        public Facing DirectionAt(int cell) => (Facing)dirs[cell];

        public static bool IsWalkableTile(WindTile t) => t == WindTile.Floor || t == WindTile.Grip || t == WindTile.Goal;

        public bool IsWalkable(int cell) => IsWalkableTile((WindTile)tiles[cell]);

        public int CountOf(WindTile tile)
        {
            int n = 0;
            for (int i = 0; i < Cells; i++)
                if (tiles[i] == (byte)tile) n++;
            return n;
        }

        /// <summary>The cell a stream cell's arrow points at, or -1 off the grid.</summary>
        public int Next(int cell)
        {
            var p = new Vector2Int(cell % Cols + Dx[dirs[cell]], cell / Cols + Dy[dirs[cell]]);
            return InGrid(p) ? Index(p) : -1;
        }

        private void Touch() => analysed = false;

        // ------------------------------------------------------------------ Analysis

        private void Analyse()
        {
            if (analysed) return;
            analysed = true;
            for (int i = 0; i < Cells; i++) region[i] = -1;
            regionCount = 0;
            regionNodes.Clear();
            Queue<int> queue = labelQueue;
            queue.Clear();
            for (int i = 0; i < Cells; i++)
            {
                if (region[i] >= 0 || !IsWalkable(i)) continue;
                if (regionCells.Count <= regionCount) regionCells.Add(new List<int>());
                List<int> cells = regionCells[regionCount];
                cells.Clear();
                region[i] = regionCount;
                queue.Enqueue(i);
                while (queue.Count > 0)
                {
                    int c = queue.Dequeue();
                    cells.Add(c);
                    for (int f = 0; f < 4; f++)
                    {
                        var p = new Vector2Int(c % Cols + Dx[f], c / Cols + Dy[f]);
                        if (!InGrid(p)) continue;
                        int n = Index(p);
                        if (region[n] >= 0 || !IsWalkable(n)) continue;
                        region[n] = regionCount;
                        queue.Enqueue(n);
                    }
                }
                regionNodes.Add(i); // scanning in order, the first cell found is the region's smallest
                regionCount++;
            }
            Array.Clear(inflow, 0, Cells);
            heads.Clear();
            for (int s = 0; s < Cells; s++)
            {
                if (tiles[s] != (byte)WindTile.Stream) continue;
                int t = Next(s);
                if (t >= 0 && tiles[t] == (byte)WindTile.Stream) inflow[t]++;
            }
            for (int s = 0; s < Cells; s++)
                if (tiles[s] == (byte)WindTile.Stream && inflow[s] == 0) heads.Add(s);
        }

        /// <summary>The number of regions (walkable areas joined without riding), the ring's first.</summary>
        public int Regions
        {
            get
            {
                Analyse();
                return regionCount;
            }
        }

        /// <summary>The resting place (a region's smallest cell) of a walkable cell, or -1.</summary>
        public int NodeOf(int cell)
        {
            Analyse();
            return region[cell] >= 0 ? regionNodes[region[cell]] : -1;
        }

        /// <summary>The cells of a resting place's region.</summary>
        public IReadOnlyList<int> CellsOf(int node)
        {
            Analyse();
            return region[node] >= 0 ? regionCells[region[node]] : (IReadOnlyList<int>)Array.Empty<int>();
        }

        /// <summary>The first cell of every stream (one no other stream cell flows into): one per stream.</summary>
        public IReadOnlyList<int> StreamStarts
        {
            get
            {
                Analyse();
                return heads;
            }
        }

        /// <summary>The resting place a doorway opens onto (the ring's), or -1 without a doorway.</summary>
        public int DoorNode(Facing side) => doors[(int)side] ? NodeOf(DoorCell(side)) : -1;

        public bool IsDoorNode(int node)
        {
            for (int f = 0; f < 4; f++)
                if (doors[f] && DoorNode((Facing)f) == node) return true;
            return false;
        }

        // ------------------------------------------------------------------ Movement

        /// <summary>
        /// A ride that sets off on a stream tile: the walkable cell it ends on (following the arrows), or -1 when the stream is
        /// broken (it leaves the grid, ends over the sky or against a wall, or loops). length is how many stream tiles it crossed.
        /// </summary>
        public int Ride(int entry, out int length)
        {
            length = 0;
            int pos = entry;
            for (int guard = 0; guard < Cells; guard++)
            {
                if (tiles[pos] != (byte)WindTile.Stream) return IsWalkable(pos) ? pos : -1;
                length++;
                int n = Next(pos);
                if (n < 0) return -1;
                pos = n;
            }
            return -1;
        }

        /// <summary>The cells a ride from entry passes over, the stream tiles then the landing (empty when broken).</summary>
        public List<int> RidePath(int entry)
        {
            var path = new List<int>();
            int pos = entry;
            for (int guard = 0; guard < Cells; guard++)
            {
                path.Add(pos);
                if (tiles[pos] != (byte)WindTile.Stream) return IsWalkable(pos) ? path : new List<int>();
                int n = Next(pos);
                if (n < 0) break;
                pos = n;
            }
            return new List<int>();
        }

        /// <summary>
        /// Where a step from a walkable cell onto a stream tile takes the player: the landing cell, or -1 when it is no move
        /// (the tile is not a stream, the stream is broken, or the ride ends back in the region it set off from).
        /// </summary>
        public int RideFrom(int cell, int entry, out int length)
        {
            length = 0;
            if (entry < 0 || tiles[entry] != (byte)WindTile.Stream || !IsWalkable(cell)) return -1;
            int end = Ride(entry, out length);
            if (end < 0 || NodeOf(end) == NodeOf(cell)) return -1;
            return end;
        }

        private void Moves(int node, List<PuzzleMove> into)
        {
            Analyse();
            int r = region[node];
            if (r < 0) return;
            stampNow++;
            foreach (int c in regionCells[r])
                for (int f = 0; f < 4; f++)
                {
                    var p = new Vector2Int(c % Cols + Dx[f], c / Cols + Dy[f]);
                    if (!InGrid(p)) continue;
                    int n = Index(p);
                    if (tiles[n] != (byte)WindTile.Stream || stamp[n] == stampNow) continue;
                    stamp[n] = stampNow; // one move per stream tile set off from, however many tiles of the region touch it
                    int end = Ride(n, out int length);
                    if (end < 0) continue;
                    int to = regionNodes[region[end]];
                    if (to == node) continue;
                    used[n] = true;
                    into.Add(new PuzzleMove(to, length));
                }
        }

        // ------------------------------------------------------------------ Measures

        /// <summary>The research note's metrics from a doorway (WindMetrics), on a graph of WindLayout.Cells capacity.</summary>
        public WindMetrics Measure(Facing side, PuzzleGraph graph = null)
        {
            Analyse();
            graph = graph ?? new PuzzleGraph(Cells);
            var m = new WindMetrics
            {
                Regions = regionCount,
                Streams = StreamCount(),
                Anchors = CountOf(WindTile.Grip),
                Walls = CountOf(WindTile.Wall),
            };
            int start = DoorNode(side);
            if (start < 0 || Goal < 0 || region[Goal] < 0) return m;
            Array.Clear(used, 0, Cells);
            graph.Explore(new[] { start }, source);
            IReadOnlyList<int> reached = graph.Reached;
            m.Unreached = regionCount - reached.Count;
            foreach (int h in heads)
                if (!used[h]) m.UnusedStreams++;
            foreach (int n in reached)
                for (int i = 0; i < graph.MoveCount(n); i++) m.LongestRide = Math.Max(m.LongestRide, graph.Move(n, i).Length);
            m.Traps = graph.Traps(IsDoorNode).Count;
            int goal = NodeOf(Goal);
            m.Solvable = graph.IsReached(goal);
            if (!m.Solvable) return m;
            int[] dist = graph.Distances(start);
            m.MinRides = dist[goal];
            for (int f = 0; f < 4; f++)
            {
                int other = DoorNode((Facing)f);
                if (other >= 0 && f != (int)side) m.Crossing = Math.Max(m.Crossing, dist[other] < 0 ? 99 : dist[other]);
            }
            m.ShortestSolutions = graph.ShortestPathCount(start, goal, dist);
            m.RandomSolveRate = graph.RandomSolveRate(start, goal, 3 * m.MinRides);
            int[] home = graph.DistancesTo(n => n == start);
            foreach (int n in reached) m.ReturnCost = Math.Max(m.ReturnCost, home[n] < 0 ? 99 : home[n]);
            int[] toGoal = graph.DistancesTo(n => n == goal);
            foreach (int u in reached)
                for (int i = 0; i < graph.MoveCount(u); i++)
                {
                    int v = graph.Move(u, i).To;
                    bool onRoute = v >= 0 && dist[v] >= 0 && toGoal[v] >= 0 && dist[v] + toGoal[v] == m.MinRides;
                    if (!onRoute) m.Decoys++;
                }
            return m;
        }

        /// <summary>
        /// The screen's streams, land to land (research note 3b): a grip anchor is a stop inside its stream, so the stream that
        /// goes on from an anchor is the one that came in; only a second one leaving it (a fork) counts on its own.
        /// </summary>
        public int StreamCount()
        {
            Analyse();
            int count = heads.Count;
            for (int a = 0; a < Cells; a++)
            {
                if (tiles[a] != (byte)WindTile.Grip) continue;
                for (int f = 0; f < 4; f++)
                {
                    var p = new Vector2Int(a % Cols + Dx[f], a / Cols + Dy[f]);
                    if (!InGrid(p)) continue;
                    int n = Index(p);
                    if (tiles[n] != (byte)WindTile.Stream || inflow[n] != 0 || Next(n) == a) continue;
                    count--; // the anchor's first way on continues its stream
                    break;
                }
            }
            return count;
        }

        /// <summary>How many times the stream starting at head turns, and how many tiles it has.</summary>
        public int Bends(int head, out int length)
        {
            length = 0;
            int bends = 0, pos = head;
            for (int guard = 0; guard < Cells && pos >= 0 && tiles[pos] == (byte)WindTile.Stream; guard++)
            {
                length++;
                int n = Next(pos);
                if (n >= 0 && tiles[n] == (byte)WindTile.Stream && dirs[n] != dirs[pos]) bends++;
                pos = n;
            }
            return bends;
        }

        /// <summary>
        /// Problems with the layout (empty when it is inside every target from every doorway): the frame (the ring whole, every
        /// doorway on it, the goal pad on an island), every stream sound (it ends on land, never over the sky or against a wall,
        /// never merges, loops or bends more than MaxBends times, and touches land only where it sets off and where it lands),
        /// and the research note's metrics, measured from each doorway in turn.
        /// </summary>
        public List<string> Problems(string what)
        {
            Analyse();
            var problems = new List<string>();
            if (Goal < 0 || tiles[Goal] != (byte)WindTile.Goal) problems.Add($"{what}: no goal pad");
            else if (region[Goal] == region[HomeNode]) problems.Add($"{what}: the goal pad is on the ring");
            for (int i = 0; i < Cells; i++)
                if (IsRing(i) && tiles[i] != (byte)WindTile.Floor)
                {
                    problems.Add($"{what}: the ring is broken at {Pos(i)}");
                    break;
                }
            foreach (Facing side in Doors())
                if (DoorNode(side) != HomeNode) problems.Add($"{what}: the {side} doorway is not on the ring");
            var visited = new bool[Cells];
            foreach (int h in heads)
            {
                if (Ride(h, out _) < 0) problems.Add($"{what}: the stream from {Pos(h)} ends over the sky, against a wall or off the screen");
                if (Bends(h, out int length) > MaxBends) problems.Add($"{what}: the stream from {Pos(h)} bends more than {MaxBends} times");
                for (int pos = h, guard = 0; guard <= length && pos >= 0 && tiles[pos] == (byte)WindTile.Stream; guard++, pos = Next(pos))
                    visited[pos] = true;
            }
            for (int s = 0; s < Cells; s++)
            {
                if (tiles[s] != (byte)WindTile.Stream) continue;
                if (inflow[s] > 1) problems.Add($"{what}: streams merge at {Pos(s)}");
                if (IsDoorFront(s)) problems.Add($"{what}: a stream lies in front of a doorway at {Pos(s)}");
                if (!visited[s]) problems.Add($"{what}: the stream through {Pos(s)} has no start (a loop)");
                // Land beside it: only where it sets off (its first tile, one region) and where it lands (its last tile).
                int next = Next(s), lands = next >= 0 && IsWalkable(next) ? region[next] : -1, other = -1, others = 0;
                for (int f = 0; f < 4; f++)
                {
                    var p = new Vector2Int(s % Cols + Dx[f], s / Cols + Dy[f]);
                    if (!InGrid(p)) continue;
                    int r = region[Index(p)];
                    if (r < 0 || r == lands || r == other) continue;
                    other = r;
                    others++;
                }
                if (others > (inflow[s] == 0 ? 1 : 0)) problems.Add($"{what}: the stream touches land along its side at {Pos(s)}");
            }
            foreach (Facing side in Doors())
                foreach (string miss in Targets.Misses(Measure(side)))
                    problems.Add($"{what} from the {side} doorway: {miss}");
            return problems;
        }

        // ------------------------------------------------------------------ Regions (DoorwayReach)

        public int RegionCount => 2;

        /// <summary>Every doorway lands in region 0 (they all open onto the ring).</summary>
        public int DoorRegion(Facing side) => doors[(int)side] ? 0 : -1;

        /// <summary>1 on the goal pad's island, 0 on any other land the doorways reach, else -1 (sky, a stream, a wall).</summary>
        public int RegionAt(Vector2 local)
        {
            int cell = CellAt(local);
            if (cell < 0) return -1;
            Analyse();
            int r = region[cell];
            if (r < 0) return -1;
            if (Goal >= 0 && r == region[Goal]) return 1;
            return reachable != null && reachable[regionNodes[r]] ? 0 : -1;
        }

        /// <summary>Region 0 joins the goal when the goal can be ridden to; the goal joins back when it can ride back to a doorway.</summary>
        public IEnumerable<int> Joins(int region, bool gapCrosser)
        {
            if (region == 0 && solvable) yield return 1;
            if (region == 1 && goalSafe) yield return 0;
        }

        /// <summary>Works out the regions once the layout is final (reach from every doorway).</summary>
        private void Settle()
        {
            Analyse();
            var graph = new PuzzleGraph(Cells);
            var starts = new List<int>();
            foreach (Facing side in Doors()) starts.Add(DoorNode(side));
            graph.Explore(starts, source);
            reachable = new bool[Cells];
            foreach (int n in graph.Reached) reachable[n] = true;
            int goal = Goal >= 0 ? NodeOf(Goal) : -1;
            solvable = goal >= 0 && reachable[goal];
            goalSafe = solvable && !graph.Traps(IsDoorNode).Contains(goal);
        }

        // ------------------------------------------------------------------ Text

        private static readonly char[] Arrows = { '^', '>', 'v', '<' };

        public string Signature()
        {
            var sb = new StringBuilder();
            sb.Append(Tier).Append(" goal ").Append(Goal >= 0 ? $"{Pos(Goal).x},{Pos(Goal).y}" : "none").Append(' ');
            foreach (string row in TextRows()) sb.Append(row).Append('/');
            sb.Length--;
            return sb.ToString();
        }

        /// <summary>The grid as text, top row first: '.' sky, '_' floor, '#' wall, arrows for streams, '+' grip, 'G' the goal.</summary>
        public List<string> TextRows()
        {
            var rows = new List<string>();
            for (int y = Rows - 1; y >= 0; y--)
            {
                var sb = new StringBuilder();
                for (int x = 0; x < Cols; x++)
                {
                    int i = y * Cols + x;
                    switch ((WindTile)tiles[i])
                    {
                        case WindTile.Sky: sb.Append('.'); break;
                        case WindTile.Floor: sb.Append('_'); break;
                        case WindTile.Wall: sb.Append('#'); break;
                        case WindTile.Stream: sb.Append(Arrows[dirs[i]]); break;
                        case WindTile.Grip: sb.Append('+'); break;
                        default: sb.Append('G'); break;
                    }
                }
                rows.Add(sb.ToString());
            }
            return rows;
        }

        public override string ToString() => $"{Tier} wind";

        /// <summary>
        /// A layout from rows of text (top row first, Cols characters each, as Rows_ writes them); the ring is floor whatever
        /// the text says.
        /// </summary>
        public static WindLayout FromRows(PuzzleTier tier, Func<Facing, bool> hasDoorway, IReadOnlyList<string> rows)
        {
            var layout = new WindLayout(tier, hasDoorway);
            for (int y = 0; y < Rows; y++)
            {
                string row = rows[Rows - 1 - y];
                for (int x = 0; x < Cols; x++)
                {
                    int i = y * Cols + x;
                    if (IsRing(i)) continue;
                    char ch = x < row.Length ? row[x] : '.';
                    int arrow = Array.IndexOf(Arrows, ch);
                    if (arrow >= 0)
                    {
                        layout.tiles[i] = (byte)WindTile.Stream;
                        layout.dirs[i] = (byte)arrow;
                        continue;
                    }
                    switch (ch)
                    {
                        case '_': layout.tiles[i] = (byte)WindTile.Floor; break;
                        case '#': layout.tiles[i] = (byte)WindTile.Wall; break;
                        case '+': layout.tiles[i] = (byte)WindTile.Grip; break;
                        case 'G':
                            layout.tiles[i] = (byte)WindTile.Goal;
                            layout.Goal = i;
                            break;
                    }
                }
            }
            layout.Settle();
            return layout;
        }

        // ------------------------------------------------------------------ Generation

        /// <summary>Search steps per attempt, and attempts (the retry budget, deterministic), before the template stands in.</summary>
        public const int StepsPerAttempt = 600, Attempts = 160; // early 400 steps an attempt, mid 600, late 800

        /// <summary>
        /// A seeded wind screen of a tier for a screen whose doorways are hasDoorway (research note 3b, as a search): the ring
        /// is fixed; islands (some twins split by a wall) are dealt at the tier's count over the sky, the goal pad on one of
        /// them; a route of streams is laid first (ring, islands, the goal's island) with a way home from the goal; then single
        /// changes (a stream laid between two regions, one taken away, a grip anchor set into a stream or taken out, the goal
        /// moved) are kept while the distance from every target (WindTargets.Score, from the first doorway) does not grow, with a
        /// little annealing, until it is inside them all. Every doorway opens onto the ring, so the first doorway's measure is
        /// every doorway's; Problems re-checks each before it is kept. Out of steps, it deals afresh (up to Attempts); past that
        /// budget the tier's Template stands in (FromTemplate; the sweep reports it), so it never returns an unsolved layout.
        /// </summary>
        public static WindLayout Generate(SeededRng rng, PuzzleTier tier, Func<Facing, bool> hasDoorway)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            var search = new Search(rng, tier, hasDoorway);
            int steps = 0;
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                WindLayout found = search.Run(out float score, ref steps);
                if (found == null || score > 0f || found.Problems("generated").Count > 0) continue;
                found.SearchSteps = steps;
                found.Settle();
                return found;
            }
            WindLayout template = Template(tier, hasDoorway);
            template.SearchSteps = steps;
            template.FromTemplate = true;
            return template;
        }

        /// <summary>
        /// The fallback layouts (research note 3b step 7), one per tier, top row first: layouts the search once found, kept by
        /// hand, inside every target from every doorway (WindTests checks them).
        /// </summary>
        public static readonly string[][] Templates =
        {
            new[]
            {
                "_____________________", "_....^.............._", "_....^.............._", "_..G__.............._",
                "_..___<<<<<<........_", "_..........^........_", "_..........^........_", "_..........^__......_",
                "_...........__......_", "_...........__v....._", "_...........^.v....._", "_...........^.v....._",
                "_____________________",
            },
            new[]
            {
                "_____________________", "_.......v..........._", "_.......v..........._", "_.......v.>>v......._",
                "_.......>__#__......_", "_........__#__......_", "_.v<<<<<<__#__>v...._", "_.v.......^<<..v...._",
                "_.v............v...._", "_.___..........___.._", "_.___..........__G.._", "_.v.^..........v...._",
                "_____________________",
            },
            new[]
            {
                "_____________________", "_.....^............._", "_<___.___>>>>>>>>v.._", "_.___<___........v.._",
                "_...v.___<<<<<...v.._", "_...+>^......^...v.._", "_...v........^..v+.._", "_...v........^..v..._",
                "_...>___.....^___>+>_", "_....__G......___..._", "_....___.....v___..._", "_......v.....v.^...._",
                "_____________________",
            },
        };

        /// <summary>The tier's template on a screen with these doorways.</summary>
        public static WindLayout Template(PuzzleTier tier, Func<Facing, bool> hasDoorway) =>
            FromRows(tier, hasDoorway, Templates[(int)tier]);

        /// <summary>The generator's search over one working layout.</summary>
        private sealed class Search
        {
            private const int States = Cells * 4 * (MaxBends + 1);

            private readonly SeededRng rng;
            private readonly PuzzleTier tier;
            private readonly Func<Facing, bool> hasDoorway;
            private readonly WindTargets targets;
            private readonly PuzzleGraph graph = new PuzzleGraph(Cells);
            private WindLayout work;
            private Facing first;
            private readonly byte[] saveTiles = new byte[Cells], saveDirs = new byte[Cells];
            private int saveGoal;

            // The router's scratch.
            private readonly int[] land = new int[Cells];
            private readonly bool[] nearStream = new bool[Cells];
            private readonly int[] parent = new int[States], length = new int[States], seen = new int[States], queue = new int[States];
            private int seenNow;
            private readonly List<int> starts = new List<int>(), completions = new List<int>(), order = new List<int>();

            public Search(SeededRng rng, PuzzleTier tier, Func<Facing, bool> hasDoorway)
            {
                this.rng = rng;
                this.tier = tier;
                this.hasDoorway = hasDoorway;
                targets = WindTargets.For(tier);
            }

            private void Save()
            {
                Array.Copy(work.tiles, saveTiles, Cells);
                Array.Copy(work.dirs, saveDirs, Cells);
                saveGoal = work.Goal;
            }

            private void Restore()
            {
                Array.Copy(saveTiles, work.tiles, Cells);
                Array.Copy(saveDirs, work.dirs, Cells);
                work.Goal = saveGoal;
                work.Touch();
            }

            /// <summary>A fresh working layout: the ring, the islands, the goal, a first route and a way home.</summary>
            private bool Deal()
            {
                work = new WindLayout(tier, hasDoorway);
                List<Facing> doorList = work.Doors();
                if (doorList.Count == 0) return false;
                first = doorList[0];
                var islands = new List<RectInt>();
                int want = targets.IslandsLo + rng.NextInt(targets.IslandsHi - targets.IslandsLo + 1);
                for (int tries = 0; islands.Count < want && tries < 300; tries++)
                {
                    bool twin = rng.NextInt(100) < targets.TwinPercent;
                    int w = twin ? 5 : 2 + rng.NextInt(3), h = 2 + rng.NextInt(2);
                    var r = new RectInt(Zone.xMin + rng.NextInt(Zone.width - w + 1), Zone.yMin + rng.NextInt(Zone.height - h + 1), w, h);
                    bool clash = false;
                    foreach (RectInt o in islands)
                        if (new RectInt(o.xMin - 1, o.yMin - 1, o.width + 2, o.height + 2).Overlaps(r)) clash = true;
                    if (clash) continue;
                    islands.Add(r);
                    for (int y = r.yMin; y < r.yMax; y++)
                        for (int x = r.xMin; x < r.xMax; x++)
                            work.tiles[Index(new Vector2Int(x, y))] = (byte)(twin && x == r.xMin + 2 ? WindTile.Wall : WindTile.Floor);
                }
                if (islands.Count < 2) return false;
                work.Touch();
                if (!MoveGoal()) return false;
                work.Analyse();
                int home = work.region[HomeNode], goal = work.region[work.Goal];
                var others = new List<int>();
                for (int r = 0; r < work.regionCount; r++)
                    if (r != home && r != goal) others.Add(r);
                rng.Shuffle(others);
                // The route first (research note 3b step 2): ring, islands, the goal's island; then the way home from the goal.
                int hops = targets.MinRidesLo + rng.NextInt(targets.MinRidesHi - targets.MinRidesLo + 1);
                var route = new List<int> { home };
                int used = 0;
                for (; used < hops - 1 && used < others.Count; used++)
                    if (AddStream(route[route.Count - 1], others[used])) route.Add(others[used]);
                AddStream(route[route.Count - 1], goal);
                route.Add(goal);
                AddStream(goal, home);
                // Returns (step 3): from every other route region, half the time, back to an earlier one (a way home that is
                // also a wrong turn for the random player).
                for (int i = 1; i + 1 < route.Count; i++)
                    if (rng.NextInt(2) == 0) AddStream(route[i], route[rng.NextInt(i)]);
                // Decoys (step 4): into a region off the route, from one or two route regions, with its own way home.
                if (targets.DecoysHi > 0 && used < others.Count && route.Count > 2)
                {
                    int decoy = others[used];
                    int ways = targets.DecoysLo + rng.NextInt(targets.DecoysHi - targets.DecoysLo + 1);
                    for (int i = 0; i < ways; i++) AddStream(route[1 + rng.NextInt(route.Count - 2)], decoy);
                    AddStream(decoy, home);
                }
                return true;
            }

            private WindMetrics last;

            private float Score()
            {
                last = work.Measure(first, graph);
                return targets.Score(last);
            }

            /// <summary>
            /// The two ends of a new stream: mostly at random (from a region the doorway reaches); while a region is unreached, often
            /// into it; while a region rides home too slowly, often from the slowest one toward the ring.
            /// </summary>
            private bool PickEnds(out int from, out int to)
            {
                IReadOnlyList<int> reached = graph.Reached;
                from = to = -1;
                if (reached.Count == 0) return false;
                int roll = rng.NextInt(10);
                if (last != null && last.Unreached > 0 && roll < 4)
                {
                    var unreached = new List<int>();
                    for (int r = 0; r < work.regionCount; r++)
                        if (!graph.IsReached(work.regionNodes[r])) unreached.Add(r);
                    if (unreached.Count > 0)
                    {
                        from = work.region[reached[rng.NextInt(reached.Count)]];
                        to = unreached[rng.NextInt(unreached.Count)];
                        return from >= 0;
                    }
                }
                if (last != null && last.Solvable && last.ReturnCost > targets.ReturnMax && roll < 7)
                {
                    int start = work.DoorNode(first);
                    int[] home = graph.DistancesTo(n => n == start);
                    int worst = -1;
                    foreach (int n in reached)
                        if (worst < 0 || home[n] < 0 || (home[worst] >= 0 && home[n] > home[worst])) worst = n;
                    from = work.region[worst];
                    to = rng.NextInt(3) > 0 ? work.region[start] : work.region[reached[rng.NextInt(reached.Count)]];
                    return from >= 0;
                }
                from = rng.NextInt(10) < 7 ? work.region[reached[rng.NextInt(reached.Count)]] : rng.NextInt(work.regionCount);
                to = rng.NextInt(work.regionCount);
                return from >= 0;
            }

            /// <summary>One search from a fresh deal; the layout reached and its score.</summary>
            public WindLayout Run(out float score, ref int steps)
            {
                score = float.MaxValue;
                if (!Deal()) return null;
                float current = Score();
                int budget = StepsPerAttempt * (2 + (int)tier) / 3;
                for (int step = 0; step < budget && current > 0f; step++)
                {
                    if (step == budget / 2 && current > 3f) break; // far off halfway: a fresh deal does better
                    steps++;
                    Save();
                    if (!Mutate())
                    {
                        Restore();
                        continue;
                    }
                    float next = Score();
                    float temperature = 0.4f * (1f - step / (float)budget);
                    bool keep = next <= current ||
                                (temperature > 0f && next < 900f && rng.NextInt(10000) < (int)(10000 * Math.Exp(-(next - current) / temperature)));
                    if (keep)
                    {
                        current = next;
                        continue;
                    }
                    Restore();
                }
                score = current;
                return work;
            }

            private bool Mutate()
            {
                work.Analyse();
                int kind = rng.NextInt(20);
                if (kind < 8)
                {
                    // A stream from a region (mostly one the doorway reaches) to another.
                    return PickEnds(out int from, out int to) && AddStream(from, to);
                }
                if (kind < 14)
                {
                    // Take a stream away, or (10-13) send it somewhere else from the same land.
                    if (work.heads.Count == 0) return false;
                    int head = work.heads[rng.NextInt(work.heads.Count)];
                    int from = SourceOf(head);
                    RemoveStream(head);
                    if (kind < 10) return true;
                    return from >= 0 && AddStream(from, rng.NextInt(work.regionCount));
                }
                if (kind < 15) return AddAnchor();
                if (kind < 16)
                {
                    // A fork: a second stream off a grip anchor.
                    var anchors = new List<int>();
                    for (int i = 0; i < Cells; i++)
                        if (work.tiles[i] == (byte)WindTile.Grip) anchors.Add(i);
                    if (anchors.Count == 0) return AddAnchor();
                    return AddStream(work.region[anchors[rng.NextInt(anchors.Count)]], rng.NextInt(work.regionCount));
                }
                if (kind < 18) return RemoveAnchor();
                return MoveGoal();
            }

            /// <summary>The region a stream sets off from (the land beside its first tile, other than where it lands), or -1.</summary>
            private int SourceOf(int head)
            {
                int lands = work.Ride(head, out _);
                int landRegion = lands >= 0 ? work.region[lands] : -1;
                for (int f = 0; f < 4; f++)
                {
                    var p = Pos(head) + Step((Facing)f);
                    if (!InGrid(p)) continue;
                    int r = work.region[Index(p)];
                    if (r >= 0 && r != landRegion) return r;
                }
                return -1;
            }

            private void RemoveStream(int head)
            {
                int pos = head;
                for (int guard = 0; guard < Cells && pos >= 0 && work.tiles[pos] == (byte)WindTile.Stream; guard++)
                {
                    int n = work.Next(pos);
                    work.tiles[pos] = (byte)WindTile.Sky;
                    work.dirs[pos] = 0;
                    pos = n;
                }
                work.Touch();
            }

            /// <summary>A grip anchor on a middle tile of a stream (it splits one ride into two).</summary>
            private bool AddAnchor()
            {
                if (work.heads.Count == 0) return false;
                int head = work.heads[rng.NextInt(work.heads.Count)];
                var cells = new List<int>();
                for (int pos = head, guard = 0; guard < Cells && pos >= 0 && work.tiles[pos] == (byte)WindTile.Stream; guard++, pos = work.Next(pos))
                    cells.Add(pos);
                if (cells.Count < 3) return false;
                int at = cells[1 + rng.NextInt(cells.Count - 2)];
                work.tiles[at] = (byte)WindTile.Grip;
                work.dirs[at] = 0;
                work.Touch();
                return true;
            }

            /// <summary>A grip anchor with one stream in and one out turned back into stream (the two rides joined), within the bends and length.</summary>
            private bool RemoveAnchor()
            {
                var anchors = new List<int>();
                for (int i = 0; i < Cells; i++)
                    if (work.tiles[i] == (byte)WindTile.Grip) anchors.Add(i);
                if (anchors.Count == 0) return false;
                int a = anchors[rng.NextInt(anchors.Count)];
                int into = 0, outOf = -1, outs = 0;
                for (int f = 0; f < 4; f++)
                {
                    var p = Pos(a) + Step((Facing)f);
                    if (!InGrid(p)) continue;
                    int n = Index(p);
                    if (work.tiles[n] != (byte)WindTile.Stream) continue;
                    if (work.Next(n) == a) into++;
                    else
                    {
                        outs++;
                        outOf = f;
                    }
                }
                if (into != 1 || outs != 1) return false;
                work.tiles[a] = (byte)WindTile.Stream;
                work.dirs[a] = (byte)outOf;
                work.Touch();
                work.Analyse();
                foreach (int h in work.heads)
                    if (work.Bends(h, out int len) > MaxBends || len > targets.LongestMax) return false;
                return true;
            }

            /// <summary>The goal pad onto another island cell.</summary>
            private bool MoveGoal()
            {
                var floor = new List<int>();
                for (int i = 0; i < Cells; i++)
                    if (!IsRing(i) && work.tiles[i] == (byte)WindTile.Floor) floor.Add(i);
                if (floor.Count == 0) return false;
                if (work.Goal >= 0) work.tiles[work.Goal] = (byte)WindTile.Floor;
                work.Goal = floor[rng.NextInt(floor.Count)];
                work.tiles[work.Goal] = (byte)WindTile.Goal;
                work.Touch();
                return true;
            }

            private int State(int cell, int dir, int bends) => (cell * 4 + dir) * (MaxBends + 1) + bends;

            private void Push(int state, int from, int len, ref int tail)
            {
                if (seen[state] == seenNow) return;
                seen[state] = seenNow;
                parent[state] = from;
                length[state] = len;
                queue[tail++] = state;
            }

            /// <summary>
            /// Lays a new stream from region from to region to, if one fits (breadth first over tile, direction and turns): it sets
            /// off from a sky tile beside from (touching no other land) and lands on to; every other tile of it is sky touching no
            /// land and no other stream; at most MaxBends turns and the tier's longest ride; then a random one of the shortest found.
            /// </summary>
            private bool AddStream(int from, int to)
            {
                work.Analyse();
                if (from == to || from < 0 || to < 0 || from >= work.regionCount || to >= work.regionCount) return false;
                for (int c = 0; c < Cells; c++)
                {
                    land[c] = -1;
                    nearStream[c] = false;
                    for (int f = 0; f < 4; f++)
                    {
                        var p = new Vector2Int(c % Cols + Dx[f], c / Cols + Dy[f]);
                        if (!InGrid(p)) continue;
                        int n = Index(p);
                        if (work.tiles[n] == (byte)WindTile.Stream) nearStream[c] = true;
                        int r = work.region[n];
                        if (r < 0) continue;
                        land[c] = land[c] == -1 || land[c] == r ? r : -2;
                    }
                }
                seenNow++;
                int tail = 0;
                starts.Clear();
                foreach (int c in work.regionCells[from])
                    for (int f = 0; f < 4; f++)
                    {
                        var p = new Vector2Int(c % Cols + Dx[f], c / Cols + Dy[f]);
                        if (!InGrid(p)) continue;
                        int h = Index(p);
                        if (work.tiles[h] != (byte)WindTile.Sky || nearStream[h] || IsDoorFront(h) || starts.Contains(h)) continue;
                        starts.Add(h);
                    }
                rng.Shuffle(starts);
                order.Clear();
                for (int f = 0; f < 4; f++) order.Add(f);
                foreach (int h in starts)
                {
                    // The head's land: from, and (for a one-tile stream) to; nothing else.
                    bool touchesTo = false, touchesOther = false;
                    for (int f = 0; f < 4; f++)
                    {
                        var p = Pos(h) + Step((Facing)f);
                        if (!InGrid(p)) continue;
                        int r = work.region[Index(p)];
                        if (r < 0 || r == from) continue;
                        if (r == to) touchesTo = true;
                        else touchesOther = true;
                    }
                    if (touchesOther) continue;
                    rng.Shuffle(order);
                    foreach (int f in order)
                    {
                        var p = Pos(h) + Step((Facing)f);
                        if (!InGrid(p)) continue;
                        int r = work.region[Index(p)];
                        if (r == from) continue;
                        if (touchesTo && r != to) continue;
                        Push(State(h, f, 0), -1, 1, ref tail);
                    }
                }
                completions.Clear();
                for (int headAt = 0; headAt < tail && completions.Count < 6; headAt++)
                {
                    int s = queue[headAt];
                    int b = s % (MaxBends + 1), d = s / (MaxBends + 1) % 4, c = s / (MaxBends + 1) / 4, len = length[s];
                    var np = Pos(c) + Step((Facing)d);
                    if (!InGrid(np)) continue;
                    int n = Index(np);
                    if (work.region[n] >= 0)
                    {
                        if (work.region[n] == to) completions.Add(s);
                        continue;
                    }
                    if (work.tiles[n] != (byte)WindTile.Sky || nearStream[n] || IsDoorFront(n) || len + 1 > targets.LongestMax) continue;
                    if (land[n] == -1)
                    {
                        int l = (d + 3) & 3, r = (d + 1) & 3;
                        bool leftFirst = rng.NextInt(2) == 0;
                        Push(State(n, d, b), s, len + 1, ref tail);
                        if (b < MaxBends)
                        {
                            Push(State(n, leftFirst ? l : r, b + 1), s, len + 1, ref tail);
                            Push(State(n, leftFirst ? r : l, b + 1), s, len + 1, ref tail);
                        }
                    }
                    else if (land[n] == to)
                    {
                        // Beside the land it goes to: the stream's last tile, its arrow onto that land.
                        for (int f = 0; f < 4; f++)
                        {
                            var q = np + Step((Facing)f);
                            if (!InGrid(q) || work.region[Index(q)] != to) continue;
                            int nb = b + (f == d ? 0 : 1);
                            if (nb > MaxBends) continue;
                            Push(State(n, f, nb), s, len + 1, ref tail);
                        }
                    }
                }
                if (completions.Count == 0) return false;
                // The straightest of the shortest found (fewest turns, then at random): streams that read at a glance.
                rng.Shuffle(completions);
                completions.Sort((x, y) => (length[x] * 4 + x % (MaxBends + 1)).CompareTo(length[y] * 4 + y % (MaxBends + 1)));
                foreach (int done in completions)
                {
                    var cells = new List<int>();
                    var ways = new List<int>();
                    for (int s = done; s >= 0; s = parent[s])
                    {
                        cells.Add(s / (MaxBends + 1) / 4);
                        ways.Add(s / (MaxBends + 1) % 4);
                    }
                    cells.Reverse();
                    ways.Reverse();
                    if (!Clean(cells)) continue;
                    for (int i = 0; i < cells.Count; i++)
                    {
                        work.tiles[cells[i]] = (byte)WindTile.Stream;
                        work.dirs[cells[i]] = (byte)ways[i];
                    }
                    work.Touch();
                    return true;
                }
                return false;
            }

            /// <summary>True when a path's tiles are all different and only consecutive ones touch.</summary>
            private static bool Clean(List<int> cells)
            {
                for (int i = 0; i < cells.Count; i++)
                    for (int j = i + 1; j < cells.Count; j++)
                    {
                        if (cells[i] == cells[j]) return false;
                        if (j == i + 1) continue;
                        Vector2Int a = Pos(cells[i]), b = Pos(cells[j]);
                        if (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1) return false;
                    }
                return true;
            }
        }
    }
}
