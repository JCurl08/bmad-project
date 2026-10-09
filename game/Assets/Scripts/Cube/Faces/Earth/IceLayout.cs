using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>A tile of an ice screen's grid (4.10).</summary>
    public enum IceTile : byte
    {
        /// <summary>Slippery: a slide carries on over it.</summary>
        Ice = 0,

        /// <summary>A rock: a slide stops on the tile before it.</summary>
        Rock = 1,

        /// <summary>A pit: a slide over it falls in (no damage; back to the landing the player came in by).</summary>
        Pit = 2,

        /// <summary>Grip (rough snow): a slide stops on it.</summary>
        Grip = 3,

        /// <summary>The goal pad in front of the reward chest: a slide stops on it.</summary>
        Goal = 4,

        /// <summary>A snowbank: solid, outside the ice area (and beside the late tier's landings).</summary>
        Bank = 5,

        /// <summary>A doorway's landing lane: grip from the doorway to the ice.</summary>
        Landing = 6,

        /// <summary>The camp: grip in a plus on the centre, where every doorway's lane slides to.</summary>
        Camp = 7,
    }

    /// <summary>
    /// An Earth ice screen as pure data (4.10): a 21 x 11 grid of one-unit tiles centred on the screen (cell (10, 5) on the
    /// screen's centre), inside the half-tile edge walls. The ice area (by tier, IceTargets) is centred; outside it are
    /// snowbanks. Every doorway's landing lane (grip) runs from its doorway to the ice, and every lane's slide runs along a
    /// clear line to the camp, a plus of grip on the centre, so a crossing between any two doorways is two slides and every
    /// doorway starts the same puzzle. The rest of the ice holds rocks, pits, a few grip tiles and the goal pad (with the
    /// reward chest on it).
    /// Movement is the solver's own (Slide): the player rests on a tile; a move onto ice slides straight on, in whole tiles,
    /// until a rock, bank or the grid's edge stops it (on the tile before), it falls into a pit, or it reaches grip, a lane,
    /// the camp or the goal (it stops on that tile). Walking within a lane or the camp is free (one tile a step).
    /// The solver (PuzzleGraph) treats each lane and the camp as one resting place; Measure gives the research note's
    /// metrics from a doorway; Generate searches for a layout inside every target of its tier (IceTargets). Regions for
    /// DoorwayReach: 0 is everywhere the doorways reach (they all join through the camp), 1 is the goal pad.
    /// </summary>
    public sealed class IceLayout : IScreenRegions
    {
        public const int Cols = 21, Rows = 11, Cells = Cols * Rows;
        public const int CentreX = Cols / 2, CentreY = Rows / 2;
        public static readonly int CampCell = CentreY * Cols + CentreX;

        /// <summary>A Slide that goes nowhere (blocked at once, or a step within the same lane or camp).</summary>
        public const int NoMove = -2;

        private static readonly int[] Dx = { 0, 1, 0, -1 }, Dy = { 1, 0, -1, 0 }; // Facing order: North, East, South, West

        private readonly byte[] tiles = new byte[Cells];
        private readonly int[] rep = new int[Cells];
        private readonly bool[] cross = new bool[Cells];
        private readonly int[] mouths = { -1, -1, -1, -1 };
        private readonly List<int>[] lanes = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
        private readonly List<int> camp = new List<int>();
        private readonly PuzzleGraph.MoveSource source;
        private bool[] reachable;
        private bool solvable, goalSafe;

        public PuzzleTier Tier { get; }
        public IceTargets Targets => IceTargets.For(Tier);

        /// <summary>The ice area (grid cells; xMin, yMin inclusive).</summary>
        public RectInt Area { get; }

        /// <summary>The goal pad's cell index (y * Cols + x).</summary>
        public int Goal { get; private set; }

        /// <summary>How many search steps the generator took, and whether it fell back to its best miss (0 when made by hand).</summary>
        public int SearchSteps { get; private set; }

        private IceLayout(PuzzleTier tier, Func<Facing, bool> hasDoorway)
        {
            Tier = tier;
            IceTargets t = IceTargets.For(tier);
            Area = new RectInt((Cols - t.Width) / 2, (Rows - t.Height) / 2, t.Width, t.Height);
            source = Moves;
            for (int i = 0; i < Cells; i++)
            {
                rep[i] = i;
                tiles[i] = (byte)(Area.Contains(Pos(i)) ? IceTile.Ice : IceTile.Bank);
            }
            foreach (int c in CampCells())
            {
                tiles[c] = (byte)IceTile.Camp;
                rep[c] = CampCell;
                camp.Add(c);
            }
            for (int f = 0; f < 4; f++)
            {
                if (hasDoorway != null && !hasDoorway((Facing)f)) continue;
                // From the border cell at the doorway inward, while outside the ice area: the lane. If the area reaches the
                // border, the lane is that border cell, with banks on both sides of it.
                Vector2Int b = BorderCell((Facing)f), inward = new Vector2Int(-Dx[f], -Dy[f]);
                var lane = lanes[f];
                for (Vector2Int p = b; InGrid(p) && !Area.Contains(p); p += inward) lane.Add(Index(p));
                if (lane.Count == 0)
                {
                    lane.Add(Index(b));
                    var side = new Vector2Int(Dy[f], Dx[f]);
                    tiles[Index(b + side)] = (byte)IceTile.Bank;
                    tiles[Index(b - side)] = (byte)IceTile.Bank;
                }
                mouths[f] = lane[lane.Count - 1];
                foreach (int c in lane)
                {
                    tiles[c] = (byte)IceTile.Landing;
                    rep[c] = mouths[f];
                }
                // The clear line from the mouth to the camp: nothing may stand on it.
                for (Vector2Int p = Pos(mouths[f]) + inward; InGrid(p) && tiles[Index(p)] != (byte)IceTile.Camp; p += inward)
                    cross[Index(p)] = true;
            }
            Goal = -1;
        }

        // ------------------------------------------------------------------ Geometry

        public static Vector2Int Pos(int cell) => new Vector2Int(cell % Cols, cell / Cols);
        public static int Index(Vector2Int p) => p.y * Cols + p.x;
        public static bool InGrid(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < Cols && p.y < Rows;
        public static Vector2Int Step(Facing f) => new Vector2Int(Dx[(int)f], Dy[(int)f]);

        /// <summary>The middle of a cell, local to the screen's centre (cells are one unit; (10, 5) is the centre).</summary>
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

        /// <summary>The border cell at a doorway (the grid cell nearest the middle of that edge).</summary>
        public static Vector2Int BorderCell(Facing side)
        {
            switch (side)
            {
                case Facing.North: return new Vector2Int(CentreX, Rows - 1);
                case Facing.East: return new Vector2Int(Cols - 1, CentreY);
                case Facing.South: return new Vector2Int(CentreX, 0);
                default: return new Vector2Int(0, CentreY);
            }
        }

        /// <summary>The camp's cells: the centre and its four neighbours.</summary>
        public static IEnumerable<int> CampCells()
        {
            yield return CampCell;
            for (int f = 0; f < 4; f++) yield return Index(new Vector2Int(CentreX + Dx[f], CentreY + Dy[f]));
        }

        public IceTile TileAt(int cell) => (IceTile)tiles[cell];

        /// <summary>True for a cell nothing can stand on (a rock or a bank) or one off the grid.</summary>
        public bool IsSolid(Vector2Int p) => !InGrid(p) || tiles[Index(p)] == (byte)IceTile.Rock || tiles[Index(p)] == (byte)IceTile.Bank;

        /// <summary>The landing lane of a doorway, from the doorway inward (empty without a doorway on that side).</summary>
        public IReadOnlyList<int> Lane(Facing side) => lanes[(int)side];

        /// <summary>The innermost lane cell of a doorway (its resting place), or -1 without one.</summary>
        public int Mouth(Facing side) => mouths[(int)side];

        public bool HasDoor(Facing side) => mouths[(int)side] >= 0;

        /// <summary>The landing a lane cell belongs to, or null for any other cell.</summary>
        public Facing? LandingOf(int cell)
        {
            for (int f = 0; f < 4; f++)
                if (lanes[f].Contains(cell)) return (Facing)f;
            return null;
        }

        /// <summary>The resting place a cell belongs to (its lane's mouth, the camp's centre, or itself).</summary>
        public int NodeOf(int cell) => rep[cell];

        public int CountOf(IceTile tile)
        {
            int n = 0;
            for (int i = 0; i < Cells; i++)
                if (tiles[i] == (byte)tile) n++;
            return n;
        }

        /// <summary>The ice area's cells: the area less the camp, the lanes and the banks inside it.</summary>
        public int AreaCells
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Cells; i++)
                {
                    var t = (IceTile)tiles[i];
                    if (Area.Contains(Pos(i)) && t != IceTile.Camp && t != IceTile.Landing && t != IceTile.Bank) n++;
                }
                return n;
            }
        }

        // ------------------------------------------------------------------ Movement

        /// <summary>
        /// A move from a cell toward a facing: the resting place it ends on (NodeOf of the tile it stops on), PuzzleGraph.Fail
        /// when it falls into a pit, or NoMove when it cannot set off (a rock, bank or the edge right ahead, or a step within
        /// the same lane or camp, which is a walk). end is the tile it stops on (or the pit), tiles how many it covered.
        /// </summary>
        public int Slide(int cell, Facing facing, out int end, out int length)
        {
            int f = (int)facing, dx = Dx[f], dy = Dy[f];
            int x = cell % Cols, y = cell / Cols, step = dy * Cols + dx;
            length = 0;
            end = cell;
            while (true)
            {
                x += dx;
                y += dy;
                if (x < 0 || y < 0 || x >= Cols || y >= Rows) break;
                int n = end + step;
                byte t = tiles[n];
                if (t == (byte)IceTile.Rock || t == (byte)IceTile.Bank) break;
                if (length == 0 && rep[n] == rep[cell] && (t == (byte)IceTile.Camp || t == (byte)IceTile.Landing)) return NoMove;
                length++;
                end = n;
                if (t == (byte)IceTile.Pit) return PuzzleGraph.Fail;
                if (t != (byte)IceTile.Ice) return rep[n];
            }
            return length == 0 ? NoMove : rep[end];
        }

        /// <summary>The cells a resting place spans: a lane's or the camp's cells, else the cell itself.</summary>
        public IEnumerable<int> CellsOf(int node)
        {
            if (node == CampCell)
            {
                foreach (int c in camp) yield return c;
                yield break;
            }
            for (int f = 0; f < 4; f++)
                if (mouths[f] == node)
                {
                    foreach (int c in lanes[f]) yield return c;
                    yield break;
                }
            yield return node;
        }

        private void Moves(int node, List<PuzzleMove> into)
        {
            List<int> cells = null;
            if (node == CampCell) cells = camp;
            else
                for (int f = 0; f < 4; f++)
                    if (mouths[f] == node) cells = lanes[f];
            if (cells == null) MovesFrom(node, into);
            else
                foreach (int c in cells) MovesFrom(c, into);
        }

        private void MovesFrom(int cell, List<PuzzleMove> into)
        {
            for (int f = 0; f < 4; f++)
            {
                int to = Slide(cell, (Facing)f, out _, out int length);
                if (to != NoMove) into.Add(new PuzzleMove(to, length));
            }
        }

        public bool IsLandingNode(int node)
        {
            for (int f = 0; f < 4; f++)
                if (mouths[f] >= 0 && mouths[f] == node) return true;
            return false;
        }

        // ------------------------------------------------------------------ Measures

        /// <summary>The research note's metrics from a doorway's landing (IceMetrics), on a graph of IceLayout.Cells capacity.</summary>
        public IceMetrics Measure(Facing side, PuzzleGraph graph = null)
        {
            graph = graph ?? new PuzzleGraph(Cells);
            var m = new IceMetrics();
            int start = mouths[(int)side];
            m.AreaCells = AreaCells;
            m.Rocks = CountOf(IceTile.Rock);
            m.RockDensity = m.AreaCells > 0 ? m.Rocks / (float)m.AreaCells : 0f;
            m.Pits = CountOf(IceTile.Pit);
            m.Grips = CountOf(IceTile.Grip);
            if (start < 0 || Goal < 0) return m;
            graph.Explore(new[] { start }, source);
            IReadOnlyList<int> reached = graph.Reached;
            int stops = 0;
            float degree = 0f;
            foreach (int n in reached)
            {
                for (int i = 0; i < graph.MoveCount(n); i++) m.LongestSlide = Math.Max(m.LongestSlide, graph.Move(n, i).Length);
                if (graph.OnlyFails(n)) m.PitResets++;
                if (IsLandingNode(n)) continue;
                stops++;
                degree += graph.OutDegree(n);
            }
            m.Stops = stops;
            m.OutDegree = stops > 0 ? degree / stops : 0f;
            m.Traps = graph.Traps(IsLandingNode).Count;
            m.Solvable = graph.IsReached(Goal);
            if (!m.Solvable) return m;
            int[] dist = graph.Distances(start);
            m.MinSlides = dist[Goal];
            for (int f = 0; f < 4; f++)
                if (mouths[f] >= 0 && mouths[f] != start) m.Crossing = Math.Max(m.Crossing, dist[mouths[f]] < 0 ? 99 : dist[mouths[f]]);
            m.ShortestSolutions = graph.ShortestPathCount(start, Goal, dist);
            m.RandomSolveRate = graph.RandomSolveRate(start, Goal, 3 * m.MinSlides);
            List<int> route = graph.BestRoute(start, Goal, dist, out List<int> lengths);
            int tiles = 0;
            for (int i = 1; i < lengths.Count; i++) tiles += lengths[i]; // from the camp on
            Vector2Int g = Pos(Goal), c = Pos(CampCell);
            int manhattan = Math.Abs(g.x - c.x) + Math.Abs(g.y - c.y);
            m.Detour = manhattan > 0 ? tiles / (float)manhattan : 99f;
            for (int i = 0; i + 1 < route.Count; i++)
                if (graph.HasFailingMove(route[i])) m.RetryCost = Math.Max(m.RetryCost, i + 1);
            int[] toGoal = graph.DistancesTo(n => n == Goal);
            foreach (int n in reached)
            {
                if (n == Goal || n == CampCell || IsLandingNode(n)) continue;
                Vector2Int p = Pos(n);
                bool inLine = (p.x == g.x && Math.Abs(p.y - g.y) <= 3) || (p.y == g.y && Math.Abs(p.x - g.x) <= 3);
                if (inLine && (toGoal[n] < 0 || toGoal[n] >= 3)) m.RedHerrings++;
            }
            return m;
        }

        /// <summary>The doorways with a landing (the ones the screen connects; at 3x3 every side has one).</summary>
        public List<Facing> Doors()
        {
            var doors = new List<Facing>();
            for (int f = 0; f < 4; f++)
                if (mouths[f] >= 0) doors.Add((Facing)f);
            return doors;
        }

        /// <summary>
        /// Problems with the layout (empty when it is inside every target from every doorway): the frame (every lane slides
        /// clear to the camp and back), the goal off the camp, lanes and their lines, and the research note's metrics,
        /// measured from each doorway in turn.
        /// </summary>
        public List<string> Problems(string what)
        {
            var problems = new List<string>();
            if (Goal < 0 || tiles[Goal] != (byte)IceTile.Goal) problems.Add($"{what}: no goal pad");
            foreach (Facing side in Doors())
            {
                int m = mouths[(int)side];
                var inward = (Facing)(((int)side + 2) & 3);
                if (Slide(m, inward, out _, out _) != CampCell) problems.Add($"{what}: the {side} landing does not slide to the camp");
                bool back = false;
                foreach (int c in camp)
                    if (Slide(c, side, out _, out _) == m) back = true;
                if (!back) problems.Add($"{what}: the camp does not slide back to the {side} landing");
                foreach (int c in lanes[(int)side])
                    for (int f = 0; f < 4; f++)
                    {
                        if (f == (int)side || f == (int)inward) continue;
                        int to = Slide(c, (Facing)f, out _, out _);
                        if (to != NoMove) problems.Add($"{what}: the {side} lane leaks sideways");
                    }
                foreach (string miss in Targets.Misses(Measure(side)))
                    problems.Add($"{what} from the {side} doorway: {miss}");
            }
            for (int i = 0; i < Cells; i++)
                if (cross[i] && tiles[i] != (byte)IceTile.Ice) problems.Add($"{what}: {(IceTile)tiles[i]} on the line to the camp at {Pos(i)}");
            return problems;
        }

        // ------------------------------------------------------------------ Regions (DoorwayReach)

        public int RegionCount => 2;

        /// <summary>Every doorway with a landing lands in region 0 (they all join through the camp).</summary>
        public int DoorRegion(Facing side) => mouths[(int)side] >= 0 ? 0 : -1;

        /// <summary>1 on the goal pad, 0 on any other resting place the doorways reach (and the lanes and camp), else -1.</summary>
        public int RegionAt(Vector2 local)
        {
            int cell = CellAt(local);
            if (cell < 0) return -1;
            if (cell == Goal) return 1;
            return reachable != null && reachable[rep[cell]] ? 0 : -1;
        }

        /// <summary>Region 0 joins the goal when the goal can be reached; the goal joins back when a landing or a pit can be reached from it.</summary>
        public IEnumerable<int> Joins(int region, bool gapCrosser)
        {
            if (region == 0 && solvable) yield return 1;
            if (region == 1 && goalSafe) yield return 0;
        }

        /// <summary>Works out the regions once the layout is final (reach from every landing).</summary>
        private void Settle()
        {
            var graph = new PuzzleGraph(Cells);
            graph.Explore(Array.FindAll(mouths, m => m >= 0), source);
            reachable = new bool[Cells];
            foreach (int n in graph.Reached) reachable[n] = true;
            solvable = Goal >= 0 && reachable[Goal];
            goalSafe = solvable && !graph.Traps(IsLandingNode).Contains(Goal);
        }

        public string Signature()
        {
            var sb = new StringBuilder();
            sb.Append(Tier).Append(' ').Append(Area.width).Append('x').Append(Area.height).Append(" goal ").Append(Pos(Goal).x).Append(',')
                .Append(Pos(Goal).y).Append(' ');
            for (int y = Rows - 1; y >= 0; y--)
            {
                for (int x = 0; x < Cols; x++)
                {
                    switch ((IceTile)tiles[y * Cols + x])
                    {
                        case IceTile.Ice: sb.Append('.'); break;
                        case IceTile.Rock: sb.Append('#'); break;
                        case IceTile.Pit: sb.Append('o'); break;
                        case IceTile.Grip: sb.Append(':'); break;
                        case IceTile.Goal: sb.Append('G'); break;
                        case IceTile.Bank: sb.Append('~'); break;
                        case IceTile.Landing: sb.Append('L'); break;
                        default: sb.Append('+'); break;
                    }
                }
                if (y > 0) sb.Append('/');
            }
            return sb.ToString();
        }

        public override string ToString() => $"{Tier} ice {Area.width}x{Area.height}";

        // ------------------------------------------------------------------ Hand-made layouts (tests and tools)

        /// <summary>
        /// A layout from rows of text (top row first, Cols characters each, as Signature writes them: '.' ice, '#' rock,
        /// 'o' pit, ':' grip, 'G' goal; the frame's own cells, banks, lanes and camp, are kept whatever the text says).
        /// </summary>
        public static IceLayout FromRows(PuzzleTier tier, Func<Facing, bool> hasDoorway, IReadOnlyList<string> rows)
        {
            var layout = new IceLayout(tier, hasDoorway);
            for (int y = 0; y < Rows; y++)
            {
                string row = rows[Rows - 1 - y];
                for (int x = 0; x < Cols; x++)
                {
                    int i = y * Cols + x;
                    var t = (IceTile)layout.tiles[i];
                    if (t != IceTile.Ice) continue;
                    switch (x < row.Length ? row[x] : '.')
                    {
                        case '#': layout.tiles[i] = (byte)IceTile.Rock; break;
                        case 'o': layout.tiles[i] = (byte)IceTile.Pit; break;
                        case ':': layout.tiles[i] = (byte)IceTile.Grip; break;
                        case 'G':
                            layout.tiles[i] = (byte)IceTile.Goal;
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
        public const int StepsPerAttempt = 800, Attempts = 30;

        /// <summary>
        /// A seeded ice screen of a tier for a screen whose doorways are hasDoorway (research note 3a, as a search): the frame
        /// (area, banks, lanes, camp, the clear lines) is fixed; the goal goes at least the tier's GoalDistance from the camp;
        /// rocks, pits and grip tiles are dealt at the tier's counts; then single changes (a rock, pit, grip tile or the goal
        /// moved, a rock or pit added or taken away within its band) are kept while the layout's distance from every target
        /// (IceTargets.Score, from the first doorway) does not grow, with a little annealing, until it is inside them all.
        /// Every doorway starts the same puzzle (its lane slides to the camp), so the first doorway's measure is every
        /// doorway's; Problems re-checks each before it is kept. Out of steps, it starts afresh (up to Attempts); past that
        /// budget the tier's Template stands in (FromTemplate; the sweep reports it), so it never returns an unsolved layout.
        /// </summary>
        public static IceLayout Generate(SeededRng rng, PuzzleTier tier, Func<Facing, bool> hasDoorway)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            var search = new Search(rng, tier, hasDoorway);
            int steps = 0;
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                IceLayout found = search.Run(out float score, ref steps);
                if (found == null || score > 0f || found.Problems("generated").Count > 0) continue;
                found.SearchSteps = steps;
                found.Settle();
                return found;
            }
            // Out of the retry budget (never on seeds 1-500): the tier's hand-checked template, which is inside every target.
            IceLayout template = Template(tier, hasDoorway);
            template.SearchSteps = steps;
            template.FromTemplate = true;
            return template;
        }

        /// <summary>True when the search ran out and the tier's template stands in (the sweep reports it).</summary>
        public bool FromTemplate { get; private set; }

        /// <summary>
        /// The fallback layouts (research note 3a step 7), one per tier, top row first: layouts the search once found, kept
        /// by hand, inside every target from every doorway (IceTests checks them). The frame comes from the tier.
        /// </summary>
        public static readonly string[][] Templates =
        {
            new[]
            {
                "~~~~~~~~~~L~~~~~~~~~~", "~~~~~~~~~~L~~~~~~~~~~", "~~~~...G.........~~~~", "~~~~.....:.......~~~~",
                "~~~~.....#+#...#:~~~~", "LLLL.....+++.....LLLL", "~~~~....#.+......~~~~", "~~~~.o.......:..#~~~~",
                "~~~~.:#:.........~~~~", "~~~~~~~~~~L~~~~~~~~~~", "~~~~~~~~~~L~~~~~~~~~~",
            },
            new[]
            {
                "~~~~~~~~~~L~~~~~~~~~~", "~~o..##.:..o......#~~", "~~...#......#oG....~~", "~~..##............#~~",
                "~~......o#+#....:..~~", "LL.......+++.......LL", "~~.......#+........~~", "~~.........#.......~~",
                "~~.......#.#...#...~~", "~~...........#.:...~~", "~~~~~~~~~~L~~~~~~~~~~",
            },
            new[]
            {
                "....#....~L~#......#.", "#...#................", ".....#...o..#.#......", "#............#.......",
                "~..o.#.#..+.#......#~", "L........+++........L", "~#.......#+#...:##..~", "..#.........#........",
                ".#.o#............##..", ".#..#..#.............", "o........~L~....##G..",
            },
        };

        /// <summary>The tier's template on a screen with these doorways.</summary>
        public static IceLayout Template(PuzzleTier tier, Func<Facing, bool> hasDoorway) =>
            FromRows(tier, hasDoorway, Templates[(int)tier]);

        /// <summary>The generator's search over one working layout.</summary>
        private sealed class Search
        {
            private readonly SeededRng rng;
            private readonly PuzzleTier tier;
            private readonly Func<Facing, bool> hasDoorway;
            private readonly IceTargets targets;
            private readonly PuzzleGraph graph = new PuzzleGraph(Cells);
            private IceLayout work;
            private Facing first;
            private readonly List<int> free = new List<int>();
            private readonly List<int> rocks = new List<int>(), pits = new List<int>(), grips = new List<int>();
            private int rockLo, rockHi;

            public Search(SeededRng rng, PuzzleTier tier, Func<Facing, bool> hasDoorway)
            {
                this.rng = rng;
                this.tier = tier;
                this.hasDoorway = hasDoorway;
                targets = IceTargets.For(tier);
            }

            private bool IsFree(int c) => work.tiles[c] == (byte)IceTile.Ice && !work.cross[c];

            private bool Beside(int c, Func<IceTile, bool> what)
            {
                Vector2Int p = Pos(c);
                for (int f = 0; f < 4; f++)
                {
                    var q = new Vector2Int(p.x + Dx[f], p.y + Dy[f]);
                    if (InGrid(q) && what((IceTile)work.tiles[Index(q)])) return true;
                }
                return false;
            }

            private bool GoalFits(int c)
            {
                if (!IsFree(c)) return false;
                Vector2Int p = Pos(c);
                if (Math.Abs(p.x - CentreX) + Math.Abs(p.y - CentreY) < targets.GoalDistance) return false;
                return !Beside(c, t => t == IceTile.Camp || t == IceTile.Landing || t == IceTile.Grip);
            }

            private bool GripFits(int c) =>
                IsFree(c) && !Beside(c, t => t == IceTile.Camp || t == IceTile.Landing || t == IceTile.Grip || t == IceTile.Goal);

            private int Pick(Func<int, bool> fits)
            {
                for (int tries = 0; tries < 64; tries++)
                {
                    int c = free[rng.NextInt(free.Count)];
                    if (fits(c)) return c;
                }
                return -1;
            }

            private void Put(int c, IceTile t, List<int> list)
            {
                work.tiles[c] = (byte)t;
                list?.Add(c);
            }

            private void Lift(int index, List<int> list)
            {
                work.tiles[list[index]] = (byte)IceTile.Ice;
                list.RemoveAt(index);
            }

            /// <summary>A fresh working layout: the frame, the goal and every count dealt at random.</summary>
            private bool Deal()
            {
                work = new IceLayout(tier, hasDoorway);
                first = work.Doors().Count > 0 ? work.Doors()[0] : Facing.South;
                free.Clear();
                rocks.Clear();
                pits.Clear();
                grips.Clear();
                for (int i = 0; i < Cells; i++)
                    if (work.tiles[i] == (byte)IceTile.Ice && !work.cross[i]) free.Add(i);
                if (free.Count == 0 || work.Doors().Count == 0) return false;
                int area = work.AreaCells;
                rockLo = (int)Math.Ceiling(targets.RockLo * area - 1e-6);
                rockHi = (int)Math.Floor(targets.RockHi * area + 1e-6);
                int goal = Pick(GoalFits);
                if (goal < 0) return false;
                work.Goal = goal;
                Put(goal, IceTile.Goal, null);
                int nRocks = rockLo + rng.NextInt(rockHi - rockLo + 1);
                int nPits = targets.PitsLo + rng.NextInt(targets.PitsHi - targets.PitsLo + 1);
                int nGrips = targets.GripLo + rng.NextInt(targets.GripHi - targets.GripLo + 1);
                for (int i = 0; i < nGrips; i++)
                {
                    int c = Pick(GripFits);
                    if (c >= 0) Put(c, IceTile.Grip, grips);
                }
                // A head start: rocks on one to three of the camp's corners, each closing two of its eight ways onto the ice
                // (with all eight open the camp is a hub the random player wanders from, and its out-degree swamps the mean).
                var corners = new List<int>();
                foreach (Vector2Int d in new[] { new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 1) })
                    corners.Add(Index(new Vector2Int(CentreX + d.x, CentreY + d.y)));
                rng.Shuffle(corners);
                int closed = 1 + rng.NextInt(3);
                for (int i = 0; i < closed && rocks.Count < nRocks; i++)
                    if (IsFree(corners[i])) Put(corners[i], IceTile.Rock, rocks);
                while (rocks.Count < nRocks)
                {
                    int c = Pick(IsFree);
                    if (c < 0) break;
                    Put(c, IceTile.Rock, rocks);
                }
                for (int i = 0; i < nPits; i++)
                {
                    int c = Pick(IsFree);
                    if (c >= 0) Put(c, IceTile.Pit, pits);
                }
                return true;
            }

            private float Score()
            {
                IceMetrics m = work.Measure(first, graph);
                if (!m.Solvable)
                {
                    // No way to the goal yet: lead the search toward it (how near the nearest reached stop comes).
                    Vector2Int g = Pos(work.Goal);
                    int near = 99;
                    foreach (int n in graph.Reached)
                    {
                        Vector2Int p = Pos(n);
                        near = Math.Min(near, Math.Abs(p.x - g.x) + Math.Abs(p.y - g.y));
                    }
                    return 1000f + near + 20f * m.Traps;
                }
                return targets.Score(m);
            }

            /// <summary>One search from a fresh deal; the layout reached (a copy) and its score.</summary>
            public IceLayout Run(out float score, ref int steps)
            {
                score = float.MaxValue;
                if (!Deal()) return null;
                float current = Score();
                for (int step = 0; step < StepsPerAttempt && current > 0f; step++)
                {
                    steps++;
                    // Undo record: which cells changed and what they held.
                    int a = -1, b = -1;
                    byte ta = 0, tb = 0;
                    List<int> listA = null;
                    int oldGoal = work.Goal;
                    bool ok = false;
                    int kind = rng.NextInt(20);
                    if (kind < 11 && rocks.Count > 0)
                    {
                        // Move a rock.
                        int i = rng.NextInt(rocks.Count), to = Pick(IsFree);
                        if (to >= 0)
                        {
                            a = rocks[i]; ta = work.tiles[a]; b = to; tb = work.tiles[to];
                            Lift(i, rocks);
                            Put(to, IceTile.Rock, rocks);
                            listA = rocks;
                            ok = true;
                        }
                    }
                    else if (kind < 13)
                    {
                        // Add or take away a rock, within the density band.
                        bool add = rocks.Count <= rockLo || (rocks.Count < rockHi && rng.NextInt(2) == 0);
                        if (add && rocks.Count < rockHi)
                        {
                            int to = Pick(IsFree);
                            if (to >= 0) { b = to; tb = work.tiles[to]; Put(to, IceTile.Rock, rocks); listA = rocks; ok = true; }
                        }
                        else if (!add && rocks.Count > rockLo)
                        {
                            int i = rng.NextInt(rocks.Count);
                            a = rocks[i]; ta = work.tiles[a];
                            Lift(i, rocks);
                            listA = rocks;
                            ok = true;
                        }
                    }
                    else if (kind < 16 && (pits.Count > 0 || targets.PitsHi > 0))
                    {
                        // Move, add or take away a pit.
                        int choice = pits.Count == 0 ? 1 : rng.NextInt(3);
                        if (choice == 0)
                        {
                            int i = rng.NextInt(pits.Count), to = Pick(IsFree);
                            if (to >= 0) { a = pits[i]; ta = work.tiles[a]; b = to; tb = work.tiles[to]; Lift(i, pits); Put(to, IceTile.Pit, pits); listA = pits; ok = true; }
                        }
                        else if (choice == 1 && pits.Count < targets.PitsHi)
                        {
                            int to = Pick(IsFree);
                            if (to >= 0) { b = to; tb = work.tiles[to]; Put(to, IceTile.Pit, pits); listA = pits; ok = true; }
                        }
                        else if (choice == 2 && pits.Count > targets.PitsLo)
                        {
                            int i = rng.NextInt(pits.Count);
                            a = pits[i]; ta = work.tiles[a];
                            Lift(i, pits);
                            listA = pits;
                            ok = true;
                        }
                    }
                    else if (kind < 18 && grips.Count > 0)
                    {
                        // Move a grip tile.
                        int i = rng.NextInt(grips.Count);
                        int from = grips[i];
                        Lift(i, grips);
                        int to = Pick(GripFits);
                        if (to >= 0) { a = from; ta = (byte)IceTile.Grip; b = to; tb = work.tiles[to]; Put(to, IceTile.Grip, grips); listA = grips; ok = true; }
                        else Put(from, IceTile.Grip, grips);
                    }
                    else
                    {
                        // Move the goal.
                        work.tiles[oldGoal] = (byte)IceTile.Ice;
                        int to = Pick(GoalFits);
                        if (to >= 0) { a = oldGoal; ta = (byte)IceTile.Goal; b = to; tb = work.tiles[to]; Put(to, IceTile.Goal, null); work.Goal = to; ok = true; }
                        else work.tiles[oldGoal] = (byte)IceTile.Goal;
                    }
                    if (!ok) continue;
                    float next = Score();
                    // Keep it when no worse; now and then (annealing) when a little worse.
                    float temperature = 0.3f * (1f - step / (float)StepsPerAttempt);
                    bool keep = next <= current ||
                                (temperature > 0f && next < 900f && rng.NextInt(10000) < (int)(10000 * Math.Exp(-(next - current) / temperature)));
                    if (keep)
                    {
                        current = next;
                        continue;
                    }
                    // Undo: b back to tb, a back to ta, and the lists to match.
                    if (b >= 0)
                    {
                        listA?.Remove(b);
                        work.tiles[b] = tb;
                    }
                    if (a >= 0)
                    {
                        work.tiles[a] = ta;
                        if (listA != null && ta != (byte)IceTile.Goal) listA.Add(a);
                    }
                    work.Goal = oldGoal;
                }
                score = current;
                return work; // the next attempt deals a fresh working layout
            }
        }
    }
}
