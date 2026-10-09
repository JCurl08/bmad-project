using System;
using System.Collections.Generic;
using System.Text;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Town's temple (4.19) as pure data: a 3x3 sliding-tile picture of the town crest (the classic "8-puzzle"). Cell c is row
    /// c / 3 from the top, column c % 3; tile v (1-8) belongs in cell v - 1 and the gap (0) in the bottom-right cell. A tile
    /// beside the gap slides into it when struck. The scramble is made by legal moves from the solved picture (a seeded walk
    /// that never undoes its last move), so it is always solvable, and kept only when its optimal solution, measured by a
    /// breadth-first search over all 181,440 reachable boards (Distance), is MinMoves-MaxMoves long (assumption: 14-18).
    /// </summary>
    public sealed class TempleSliding
    {
        public const int Size = 3, Cells = Size * Size;

        /// <summary>A tile's cell across (units): a 2-unit picture piece and a thin seam.</summary>
        public const float CellSize = 2.1f;

        /// <summary>The board's centre (local to the room): below the altar, above the lane in.</summary>
        public static readonly Vector2 BoardCentre = new Vector2(0f, 0.15f);

        /// <summary>The optimal solution's length (assumption: the late tier, 14-18 moves; a random board averages 22).</summary>
        public const int MinMoves = 14, MaxMoves = 18;

        /// <summary>The walk's limit before it starts again from the solved picture, and the tries before the template.</summary>
        public const int MaxWalk = 200, Attempts = 20;

        /// <summary>A hand-checked scramble (16 moves from solved) for when the walk runs out (never on seeds 1-500).</summary>
        public static readonly int[] TemplateTiles = { 0, 1, 8, 4, 6, 5, 7, 3, 2 };

        private readonly int[] tiles;

        /// <summary>The tile in each cell (0 is the gap), as dealt.</summary>
        public IReadOnlyList<int> Tiles => tiles;

        /// <summary>The legal moves the scramble walked from the solved picture.</summary>
        public int WalkMoves { get; }

        public bool FromTemplate { get; }

        public TempleSliding(int[] tiles, int walkMoves, bool fromTemplate = false)
        {
            this.tiles = (int[])tiles.Clone();
            WalkMoves = walkMoves;
            FromTemplate = fromTemplate;
        }

        /// <summary>The fewest slides that solve the dealt board (breadth-first search; -1 when unsolvable).</summary>
        public int OptimalMoves => Distance(tiles);

        public static int[] Solved()
        {
            var t = new int[Cells];
            for (int i = 0; i < Cells - 1; i++) t[i] = i + 1;
            return t;
        }

        public static bool IsSolved(IReadOnlyList<int> t)
        {
            for (int i = 0; i < Cells - 1; i++)
                if (t[i] != i + 1) return false;
            return t[Cells - 1] == 0;
        }

        /// <summary>A cell's centre (local to the room).</summary>
        public static Vector2 CellLocal(int cell) =>
            BoardCentre + new Vector2((cell % Size - 1) * CellSize, (1 - cell / Size) * CellSize);

        /// <summary>The board's footprint (local).</summary>
        public static Rect BoardRect =>
            new Rect(BoardCentre - Vector2.one * (Size * CellSize / 2f), Vector2.one * (Size * CellSize));

        public static int GapOf(IReadOnlyList<int> t)
        {
            for (int i = 0; i < t.Count; i++)
                if (t[i] == 0) return i;
            return -1;
        }

        public static bool Adjacent(int a, int b) =>
            (a / Size == b / Size && Math.Abs(a - b) == 1) || (a % Size == b % Size && Math.Abs(a - b) == Size);

        /// <summary>The cells beside a cell (up, down, left, right).</summary>
        public static IEnumerable<int> Neighbours(int cell)
        {
            if (cell >= Size) yield return cell - Size;
            if (cell < Cells - Size) yield return cell + Size;
            if (cell % Size > 0) yield return cell - 1;
            if (cell % Size < Size - 1) yield return cell + 1;
        }

        /// <summary>Slides the tile in a cell into the gap (it must be beside it): a new board; null when it cannot slide.</summary>
        public static int[] Slide(IReadOnlyList<int> t, int cell)
        {
            int gap = GapOf(t);
            if (gap < 0 || cell < 0 || cell >= Cells || !Adjacent(gap, cell)) return null;
            var next = new int[Cells];
            for (int i = 0; i < Cells; i++) next[i] = t[i];
            next[gap] = next[cell];
            next[cell] = 0;
            return next;
        }

        // ---------------------------------------------------------------- the breadth-first search

        private static sbyte[] distances;
        private static readonly int[] Factorials = { 1, 1, 2, 6, 24, 120, 720, 5040, 40320, 362880 };

        /// <summary>A board's rank among the 9! orderings (its Lehmer code).</summary>
        private static int Rank(IReadOnlyList<int> t)
        {
            int rank = 0;
            for (int i = 0; i < Cells; i++)
            {
                int smaller = 0;
                for (int j = i + 1; j < Cells; j++)
                    if (t[j] < t[i]) smaller++;
                rank += smaller * Factorials[Cells - 1 - i];
            }
            return rank;
        }

        /// <summary>Every board's distance from the solved picture (made once: a breadth-first search over 181,440 boards).</summary>
        private static sbyte[] Distances
        {
            get
            {
                if (distances != null) return distances;
                var d = new sbyte[Factorials[Cells]];
                for (int i = 0; i < d.Length; i++) d[i] = -1;
                // Boards queue packed in base 9 (one int each, no garbage per board), unpacked into one reused array.
                var queue = new Queue<int>();
                var t = new int[Cells];
                int[] solved = Solved();
                d[Rank(solved)] = 0;
                queue.Enqueue(Pack(solved));
                while (queue.Count > 0)
                {
                    Unpack(queue.Dequeue(), t);
                    sbyte here = d[Rank(t)];
                    int gap = GapOf(t);
                    foreach (int cell in Neighbours(gap))
                    {
                        t[gap] = t[cell];
                        t[cell] = 0;
                        int r = Rank(t);
                        if (d[r] < 0)
                        {
                            d[r] = (sbyte)(here + 1);
                            queue.Enqueue(Pack(t));
                        }
                        t[cell] = t[gap];
                        t[gap] = 0;
                    }
                }
                distances = d;
                return d;
            }
        }

        private static int Pack(int[] t)
        {
            int p = 0;
            for (int i = 0; i < Cells; i++) p = p * Cells + t[i];
            return p;
        }

        private static void Unpack(int p, int[] t)
        {
            for (int i = Cells - 1; i >= 0; i--)
            {
                t[i] = p % Cells;
                p /= Cells;
            }
        }

        /// <summary>The fewest slides from a board to the solved picture (-1: not a board, or unsolvable).</summary>
        public static int Distance(IReadOnlyList<int> t)
        {
            if (t == null || t.Count != Cells) return -1;
            var seen = new bool[Cells];
            foreach (int v in t)
            {
                if (v < 0 || v >= Cells || seen[v]) return -1;
                seen[v] = true;
            }
            return Distances[Rank(t)];
        }

        /// <summary>An optimal solution: the cells to strike in turn (each beside the gap), straight down the search's distances.</summary>
        public static List<int> Solution(IReadOnlyList<int> start)
        {
            var cells = new List<int>();
            int d = Distance(start);
            if (d < 0) return cells;
            var t = new int[Cells];
            for (int i = 0; i < Cells; i++) t[i] = start[i];
            while (d > 0)
            {
                foreach (int cell in Neighbours(GapOf(t)))
                {
                    int[] next = Slide(t, cell);
                    if (Distance(next) != d - 1) continue;
                    cells.Add(cell);
                    t = next;
                    d--;
                    break;
                }
            }
            return cells;
        }

        // ---------------------------------------------------------------- dealing

        /// <summary>
        /// Deals a scramble: a seeded walk of legal slides from the solved picture (never straight back), at least MaxMoves
        /// long, stopping on the first board whose optimal solution is MinMoves-MaxMoves; a walk past MaxWalk starts over,
        /// and past Attempts walks the template stands in.
        /// </summary>
        public static TempleSliding Deal(SeededRng rng)
        {
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                int[] t = Solved();
                int previousGap = -1;
                for (int step = 1; step <= MaxWalk; step++)
                {
                    int gap = GapOf(t);
                    var options = new List<int>();
                    foreach (int cell in Neighbours(gap))
                        if (cell != previousGap) options.Add(cell);
                    int pick = options[rng.NextInt(options.Count)];
                    t = Slide(t, pick);
                    previousGap = gap;
                    if (step < MaxMoves) continue;
                    int d = Distance(t);
                    if (d >= MinMoves && d <= MaxMoves) return new TempleSliding(t, step);
                }
            }
            return new TempleSliding(TemplateTiles, 0, true);
        }

        /// <summary>
        /// Problems (empty when all is well): a board of the eight tiles and one gap; solvable, its optimal solution
        /// MinMoves-MaxMoves (the search, again); the solution's slides really solve it; the board on the open floor, clear of
        /// the altar and the lane in.
        /// </summary>
        public List<string> Problems(string what)
        {
            var problems = new List<string>();
            int d = Distance(tiles);
            if (d < 0)
            {
                problems.Add($"{what}: not a solvable board ({string.Join(",", tiles)})");
                return problems;
            }
            if (d < MinMoves || d > MaxMoves) problems.Add($"{what}: an optimal solution of {d} moves (want {MinMoves}-{MaxMoves})");
            IReadOnlyList<int> t = tiles;
            foreach (int cell in Solution(tiles))
            {
                t = Slide(t, cell);
                if (t == null)
                {
                    problems.Add($"{what}: the solution slides a tile not beside the gap");
                    return problems;
                }
            }
            if (!IsSolved(t)) problems.Add($"{what}: the solution does not solve it");
            Rect board = BoardRect;
            if (!TempleRoom.Grow(TempleRoom.Floor, -1f).Contains(board.min) || !TempleRoom.Grow(TempleRoom.Floor, -1f).Contains(board.max))
                problems.Add($"{what}: the board is off the open floor");
            if (board.Overlaps(TempleRoom.AltarRect)) problems.Add($"{what}: the board covers the altar");
            if (board.Overlaps(TempleRoom.EntryLane)) problems.Add($"{what}: the board stands in the lane in");
            return problems;
        }

        /// <summary>Tiles not in their own cell (the gap not counted).</summary>
        public int Misplaced
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Cells; i++)
                    if (tiles[i] != 0 && tiles[i] != i + 1) n++;
                return n;
            }
        }

        /// <summary>
        /// The share of random players who solve it (research note: uniformly random legal slides, at most 3x the optimal
        /// length, 2,000 runs on a fixed seed).
        /// </summary>
        public double RandomSolveRate()
        {
            int optimal = Distance(tiles);
            if (optimal <= 0) return 1.0;
            var rng = new SeededRng(0x5117D);
            int wins = 0;
            const int runs = 2000;
            for (int run = 0; run < runs; run++)
            {
                IReadOnlyList<int> t = tiles;
                for (int step = 0; step < 3 * optimal; step++)
                {
                    var options = new List<int>(Neighbours(GapOf(t)));
                    t = Slide(t, options[rng.NextInt(options.Count)]);
                    if (!IsSolved(t)) continue;
                    wins++;
                    break;
                }
            }
            return (double)wins / runs;
        }

        public string Stats() =>
            $"3x3 picture, optimal {OptimalMoves} moves (search over 181,440 boards), scrambled by {WalkMoves} legal moves, " +
            $"{Misplaced} of 8 tiles out of place, P_rand {RandomSolveRate() * 100.0:0.##}%";

        public string Signature() => string.Join("", tiles) + $" opt {OptimalMoves} walk {WalkMoves}";
    }

    /// <summary>
    /// Chemistry's temple (4.19) as pure data: Lights Out on a bank of Columns x Rows crystals. Striking a crystal flips it and
    /// the crystals beside it (up, down, left, right); the goal is every crystal lit. Crystal i stands in row i / Columns
    /// (0 the bottom), column i % Columns; a board is a bit mask of the lit ones. The start is the all-lit bank with a dealt
    /// set of strikes applied, so it is solvable (strikes undo themselves), and it is kept only when the least number of
    /// strikes that lights it all, measured by a breadth-first search over every board (LeastStrikes), is MinStrikes-MaxStrikes
    /// (assumption: 6-7 on the 5x3 bank, whose hardest boards need 8).
    /// </summary>
    public sealed class TempleLights
    {
        public const int Columns = 5, Rows = 3, Count = Columns * Rows;

        /// <summary>The crystals' spacing (units): across, and up (closer, so the bank keeps below the altar).</summary>
        public const float SpacingX = 2.4f, SpacingY = 2.1f;

        /// <summary>The bank's centre (local to the room): below the altar, above the lane in.</summary>
        public static readonly Vector2 BankCentre = new Vector2(0f, 0.2f);

        /// <summary>The least strikes that light the bank (assumption: 6-7, the late tier).</summary>
        public const int MinStrikes = 6, MaxStrikes = 7;

        public const int Attempts = 60;

        /// <summary>Every crystal lit.</summary>
        public const int AllLit = (1 << Count) - 1;

        /// <summary>A hand-checked bank (6 strikes from lit) for when the deal runs out (never on seeds 1-500).</summary>
        public static readonly int[] TemplateStrikes = { 0, 3, 6, 8, 11, 14 };

        public int Start { get; }

        /// <summary>The strikes the deal applied to the lit bank (one solution).</summary>
        public IReadOnlyList<int> Dealt { get; }

        public bool FromTemplate { get; }

        public TempleLights(int start, IReadOnlyList<int> dealt, bool fromTemplate = false)
        {
            Start = start;
            Dealt = dealt ?? new List<int>();
            FromTemplate = fromTemplate;
        }

        /// <summary>The least strikes that light every crystal (the search; -1 when it cannot be done).</summary>
        public int LeastStrikesToSolve => LeastStrikes(Start);

        public static int Column(int i) => i % Columns;
        public static int Row(int i) => i / Columns;

        /// <summary>A crystal's place (local to the room).</summary>
        public static Vector2 CrystalLocal(int i) =>
            BankCentre + new Vector2((Column(i) - (Columns - 1) / 2f) * SpacingX, (Row(i) - (Rows - 1) / 2f) * SpacingY);

        /// <summary>The crystals a strike on i flips: i and those beside it.</summary>
        public static int Flips(int i)
        {
            int m = 1 << i;
            int c = Column(i), r = Row(i);
            if (c > 0) m |= 1 << (i - 1);
            if (c < Columns - 1) m |= 1 << (i + 1);
            if (r > 0) m |= 1 << (i - Columns);
            if (r < Rows - 1) m |= 1 << (i + Columns);
            return m;
        }

        public static int Strike(int board, int i) => board ^ Flips(i);

        public static bool IsLit(int board, int i) => (board & (1 << i)) != 0;

        public static int LitCount(int board)
        {
            int n = 0;
            for (int i = 0; i < Count; i++)
                if (IsLit(board, i)) n++;
            return n;
        }

        private static sbyte[] least;

        /// <summary>Every board's least strikes to all lit (made once: a breadth-first search from the lit bank over 2^15 boards).</summary>
        private static sbyte[] Least
        {
            get
            {
                if (least != null) return least;
                var d = new sbyte[1 << Count];
                for (int i = 0; i < d.Length; i++) d[i] = -1;
                var queue = new Queue<int>();
                d[AllLit] = 0;
                queue.Enqueue(AllLit);
                while (queue.Count > 0)
                {
                    int b = queue.Dequeue();
                    for (int i = 0; i < Count; i++)
                    {
                        int next = Strike(b, i);
                        if (d[next] >= 0) continue;
                        d[next] = (sbyte)(d[b] + 1);
                        queue.Enqueue(next);
                    }
                }
                least = d;
                return d;
            }
        }

        /// <summary>The least strikes that light every crystal from a board (-1: never).</summary>
        public static int LeastStrikes(int board) => board < 0 || board > AllLit ? -1 : Least[board];

        /// <summary>The boards that can be lit, and the hardest's least strikes (the search's reach).</summary>
        public static (int boards, int hardest) Reach()
        {
            int boards = 0, hardest = 0;
            foreach (sbyte d in Least)
            {
                if (d < 0) continue;
                boards++;
                hardest = Math.Max(hardest, d);
            }
            return (boards, hardest);
        }

        /// <summary>A least solution: the crystals to strike, straight down the search's distances.</summary>
        public static List<int> Solution(int board)
        {
            var strikes = new List<int>();
            int d = LeastStrikes(board);
            if (d < 0) return strikes;
            while (d > 0)
                for (int i = 0; i < Count; i++)
                {
                    if (LeastStrikes(Strike(board, i)) != d - 1) continue;
                    strikes.Add(i);
                    board = Strike(board, i);
                    d--;
                    break;
                }
            return strikes;
        }

        /// <summary>
        /// Deals a bank: MinStrikes-MaxStrikes distinct crystals (seeded) struck on the lit bank, kept when the search's least
        /// strikes back fall in the band; past Attempts deals the template stands in.
        /// </summary>
        public static TempleLights Deal(SeededRng rng)
        {
            var all = new List<int>();
            for (int i = 0; i < Count; i++) all.Add(i);
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                int k = MinStrikes + rng.NextInt(MaxStrikes - MinStrikes + 1);
                var order = new List<int>(all);
                rng.Shuffle(order);
                List<int> dealt = order.GetRange(0, k);
                dealt.Sort();
                int board = AllLit;
                foreach (int i in dealt) board = Strike(board, i);
                int d = LeastStrikes(board);
                if (d >= MinStrikes && d <= MaxStrikes) return new TempleLights(board, dealt);
            }
            int template = AllLit;
            foreach (int i in TemplateStrikes) template = Strike(template, i);
            return new TempleLights(template, new List<int>(TemplateStrikes), true);
        }

        /// <summary>
        /// Problems (empty when all is well): a board of the bank, not lit already; solvable in MinStrikes-MaxStrikes (the
        /// search, again); the dealt strikes and the least solution both light it; every crystal on the open floor, clear of
        /// the altar and the lane in.
        /// </summary>
        public List<string> Problems(string what)
        {
            var problems = new List<string>();
            if (Start < 0 || Start > AllLit) problems.Add($"{what}: not a board of {Count} crystals");
            else if (Start == AllLit) problems.Add($"{what}: every crystal lit already");
            int d = LeastStrikes(Start);
            if (d < 0) problems.Add($"{what}: can never be lit");
            else if (d < MinStrikes || d > MaxStrikes) problems.Add($"{what}: lit in {d} strikes at least (want {MinStrikes}-{MaxStrikes})");
            int board = Start;
            foreach (int i in Dealt) board = Strike(board, i);
            if (board != AllLit) problems.Add($"{what}: its dealt strikes do not light it");
            board = Start;
            foreach (int i in Solution(Start)) board = Strike(board, i);
            if (d >= 0 && board != AllLit) problems.Add($"{what}: the least solution does not light it");
            Rect floor = TempleRoom.Grow(TempleRoom.Floor, -1.5f), altar = TempleRoom.Grow(TempleRoom.AltarRect, 0.5f);
            for (int i = 0; i < Count; i++)
            {
                Vector2 p = CrystalLocal(i);
                if (!floor.Contains(p)) problems.Add($"{what}: crystal {i} is off the open floor");
                if (altar.Contains(p)) problems.Add($"{what}: crystal {i} stands on the altar");
                if (TempleRoom.Grow(TempleRoom.EntryLane, TempleStrikes.TargetRadius).Contains(p)) problems.Add($"{what}: crystal {i} stands in the lane in");
            }
            return problems;
        }

        /// <summary>
        /// The share of random players who light it all (research note: uniformly random strikes, at most 3x the least, 2,000
        /// runs on a fixed seed).
        /// </summary>
        public double RandomSolveRate()
        {
            int d = LeastStrikes(Start);
            if (d <= 0) return 1.0;
            var rng = new SeededRng(0x11647);
            int wins = 0;
            const int runs = 2000;
            for (int run = 0; run < runs; run++)
            {
                int board = Start;
                for (int step = 0; step < 3 * d; step++)
                {
                    board = Strike(board, rng.NextInt(Count));
                    if (board != AllLit) continue;
                    wins++;
                    break;
                }
            }
            return (double)wins / runs;
        }

        public string Stats()
        {
            (int boards, int hardest) = Reach();
            return $"{Columns}x{Rows} crystals, {Count - LitCount(Start)} dark, least {LeastStrikesToSolve} strikes " +
                   $"(search over {boards} boards, the hardest {hardest}), dealt {Dealt.Count}, P_rand {RandomSolveRate() * 100.0:0.##}%";
        }

        public string Signature()
        {
            var sb = new StringBuilder();
            for (int r = Rows - 1; r >= 0; r--)
            {
                for (int c = 0; c < Columns; c++) sb.Append(IsLit(Start, r * Columns + c) ? 'o' : '.');
                if (r > 0) sb.Append('/');
            }
            return sb.Append(" least ").Append(LeastStrikesToSolve).ToString();
        }
    }

    /// <summary>
    /// Biology's temple (4.19) as pure data: birdsong Simon. Four finch statues stand round a perch (north, east, south, west,
    /// each a distinct colour with 1-4 crest feathers); they sing a song of Length notes, first only its first StartLength,
    /// one note longer each round. The player strikes them back in order; a wrong strike replays the round's song (the round
    /// is kept: never back to the start). Dealt from the seed: no finch sings twice in a row (each note reads on its own) and
    /// every finch sings at least once. Solvable by construction (any order can be struck); Problems re-checks it.
    /// Assumptions: Length 6 (the late ordering column's 5-6), the rounds start at 3 (the early column's length), and the
    /// song is shown (each finch glows and a note rises), as the game has no sound yet.
    /// </summary>
    public sealed class TempleSong
    {
        public const int Finches = 4, Length = 6, StartLength = 3;

        /// <summary>Where the player stands to reach every finch with a turn (local).</summary>
        public static readonly Vector2 Perch = new Vector2(0f, -0.6f);

        /// <summary>The listening stone (4.19 review), west of the perch: striking it sings the round's song again.</summary>
        public static readonly Vector2 StoneSpot = new Vector2(-4.2f, -0.6f);

        /// <summary>The finches' distance from the perch: within a swing's reach, and no swing reaches two.</summary>
        public const float Ring = 2f;

        /// <summary>Seconds a note shows, the gap after it, and the pause before a round's song.</summary>
        public const float NoteSeconds = 0.55f, GapSeconds = 0.25f, PauseSeconds = 1f;

        public const int Attempts = 40;

        /// <summary>A hand-checked song for when the deal runs out (never on seeds 1-500).</summary>
        public static readonly int[] TemplateNotes = { 0, 2, 1, 3, 0, 1 };

        private readonly int[] notes;

        /// <summary>The finch singing each note, in order.</summary>
        public IReadOnlyList<int> Notes => notes;

        public bool FromTemplate { get; }

        public TempleSong(int[] notes, bool fromTemplate = false)
        {
            this.notes = (int[])notes.Clone();
            FromTemplate = fromTemplate;
        }

        /// <summary>The rounds: StartLength notes, then one more each, up to Length.</summary>
        public static int Rounds => Length - StartLength + 1;

        /// <summary>The notes sung (and struck back) in round r (0 first).</summary>
        public static int RoundLength(int round) => StartLength + round;

        /// <summary>Every strike of a run with no wrong note.</summary>
        public static int TotalStrikes
        {
            get
            {
                int n = 0;
                for (int r = 0; r < Rounds; r++) n += RoundLength(r);
                return n;
            }
        }

        /// <summary>Finch i's place (local): 0 north, 1 east, 2 south, 3 west of the perch.</summary>
        public static Vector2 FinchLocal(int i) => Perch + Ways[((i % Finches) + Finches) % Finches] * Ring;

        private static readonly Vector2[] Ways = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };

        /// <summary>Seconds the longest round's song takes (from its pause to its last note).</summary>
        public static float SongSeconds(int length) => PauseSeconds + length * (NoteSeconds + GapSeconds);

        /// <summary>Deals a song: each note a seeded finch other than the last one's, kept once every finch sings.</summary>
        public static TempleSong Deal(SeededRng rng)
        {
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                var n = new int[Length];
                for (int i = 0; i < Length; i++)
                {
                    if (i == 0)
                    {
                        n[i] = rng.NextInt(Finches);
                        continue;
                    }
                    int pick = rng.NextInt(Finches - 1);
                    n[i] = pick >= n[i - 1] ? pick + 1 : pick;
                }
                var song = new TempleSong(n);
                if (song.SingersMissing().Count == 0) return song;
            }
            return new TempleSong(TemplateNotes, true);
        }

        private List<int> SingersMissing()
        {
            var missing = new List<int>();
            for (int f = 0; f < Finches; f++)
                if (Array.IndexOf(notes, f) < 0) missing.Add(f);
            return missing;
        }

        /// <summary>
        /// Problems (empty when all is well): Length notes of the four finches, none twice in a row, every finch singing; the
        /// finches on the open floor, clear of the altar and the lane in; from the perch a swing at one finch reaches it and
        /// never a second one; a wrong note's replay is a cheap retry (the research note's late bound, 12 s).
        /// </summary>
        public List<string> Problems(string what)
        {
            var problems = new List<string>();
            if (notes.Length != Length) problems.Add($"{what}: a song of {notes.Length} notes (want {Length})");
            for (int i = 0; i < notes.Length; i++)
            {
                if (notes[i] < 0 || notes[i] >= Finches) problems.Add($"{what}: note {i} is no finch's");
                if (i > 0 && notes[i] == notes[i - 1]) problems.Add($"{what}: notes {i - 1} and {i} are the same finch");
            }
            foreach (int f in SingersMissing()) problems.Add($"{what}: finch {f} never sings");
            Rect floor = TempleRoom.Grow(TempleRoom.Floor, -1.5f), altar = TempleRoom.Grow(TempleRoom.AltarRect, 0.5f);
            // The lane itself (the south finch's trigger may reach into it: a trigger never blocks the walk in).
            Rect lane = TempleRoom.EntryLane;
            for (int f = 0; f < Finches; f++)
            {
                Vector2 p = FinchLocal(f);
                if (!floor.Contains(p)) problems.Add($"{what}: finch {f} is off the open floor");
                if (altar.Contains(p)) problems.Add($"{what}: finch {f} stands on the altar");
                if (lane.Contains(p)) problems.Add($"{what}: finch {f} stands in the lane in");
                // A swing from the perch toward this finch (the player's default hitbox): reaches it, and no other.
                Vector2 dir = (FinchLocal(f) - Perch).normalized;
                Vector2 centre = Perch + dir * TempleStrikes.SwingReach;
                for (int g = 0; g < Finches; g++)
                {
                    bool reached = TempleStrikes.SwingReaches(centre, dir, FinchLocal(g));
                    if (g == f && !reached) problems.Add($"{what}: a swing from the perch misses finch {f}");
                    if (g != f && reached) problems.Add($"{what}: a swing at finch {f} also reaches finch {g}");
                }
            }
            if (!floor.Contains(StoneSpot) || lane.Contains(StoneSpot) || altar.Contains(StoneSpot))
                problems.Add($"{what}: the listening stone is off the open floor");
            for (int f = 0; f < Finches; f++)
                if (Vector2.Distance(StoneSpot, FinchLocal(f)) < 2f * TempleStrikes.TargetRadius + 0.6f)
                    problems.Add($"{what}: the listening stone stands by finch {f}");
            {
                // From the perch, a swing at any finch never reaches the stone.
                for (int f = 0; f < Finches; f++)
                {
                    Vector2 dir = (FinchLocal(f) - Perch).normalized;
                    if (TempleStrikes.SwingReaches(Perch + dir * TempleStrikes.SwingReach, dir, StoneSpot))
                        problems.Add($"{what}: a swing at finch {f} reaches the listening stone");
                }
            }
            if (SongSeconds(Length) > 12f) problems.Add($"{what}: a replay takes {SongSeconds(Length):0.0} s (over 12 s)");
            return problems;
        }

        /// <summary>The chance a random striker gets every round right first time: 1 in 4^(every strike).</summary>
        public double RandomSolveRate => Math.Pow(1.0 / Finches, TotalStrikes);

        public string Stats() =>
            $"{Finches} finches, a song of {Length} (rounds {StartLength}-{Length}, {TotalStrikes} strikes), no finch twice in a row, " +
            $"a wrong note replays the round ({SongSeconds(Length):0.0} s at most), P_rand per round 1/{Math.Pow(Finches, StartLength):0}-1/{Math.Pow(Finches, Length):0}";

        public string Signature() => string.Join("", notes);
    }
}
