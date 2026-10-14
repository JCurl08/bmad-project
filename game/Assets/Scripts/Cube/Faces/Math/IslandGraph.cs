using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Game.Cube
{
    /// <summary>
    /// The island lattice as pure integers (4.12): 5 columns x 3 rows of spots, a spot's id is y * Columns + x. A plank joins
    /// two spots in one row or column, either neighbours or two apart over an empty middle spot (a long plank). Two long
    /// planks over the same middle spot would cross, so at most one may use it. Each doorway's island sits on a fixed spot.
    /// </summary>
    public static class IslandLattice
    {
        public const int Columns = 5, Rows = 3, SpotCount = Columns * Rows;

        public static int Spot(int x, int y) => y * Columns + x;
        public static int X(int spot) => spot % Columns;
        public static int Y(int spot) => spot / Columns;

        /// <summary>The spot of each side's doorway island (North, East, South, West: Facing's order).</summary>
        public static readonly int[] DoorSpots = { Spot(2, 2), Spot(4, 1), Spot(2, 0), Spot(0, 1) };

        /// <summary>The side whose doorway island sits on a spot, or -1.</summary>
        public static int SideOf(int spot) => Array.IndexOf(DoorSpots, spot);

        /// <summary>
        /// True when a plank may join two spots: in one row or column, one or two apart. middle is the spot a long plank
        /// passes over (it must hold no island), else -1.
        /// </summary>
        public static bool InLine(int a, int b, out int middle)
        {
            middle = -1;
            if (a == b || a < 0 || b < 0 || a >= SpotCount || b >= SpotCount) return false;
            int dx = Math.Abs(X(a) - X(b)), dy = Math.Abs(Y(a) - Y(b));
            if (dx != 0 && dy != 0) return false;
            int d = dx + dy;
            if (d == 1) return true;
            if (d != 2) return false;
            middle = Spot((X(a) + X(b)) / 2, (Y(a) + Y(b)) / 2);
            return true;
        }

        /// <summary>Every pair of spots a plank may join (a &lt; b), with the middle a long one passes over (-1 for neighbours).</summary>
        public static readonly (int A, int B, int Middle)[] Links = BuildLinks();

        private static (int, int, int)[] BuildLinks()
        {
            var list = new List<(int, int, int)>();
            for (int a = 0; a < SpotCount; a++)
                for (int b = a + 1; b < SpotCount; b++)
                    if (InLine(a, b, out int m)) list.Add((a, b, m));
            return list.ToArray();
        }

        /// <summary>The index of the link between two spots in Links, or -1.</summary>
        public static int LinkIndex(int a, int b)
        {
            if (a > b) (a, b) = (b, a);
            for (int i = 0; i < Links.Length; i++)
                if (Links[i].A == a && Links[i].B == b) return i;
            return -1;
        }
    }

    /// <summary>
    /// An island screen's design as pure data (4.12): the islands (each a lattice spot), the planks between them (whole or
    /// broken), each side's doorway island (-1 for none), the trial island and the reward island (-1 for none).
    /// </summary>
    public sealed class IslandDesign
    {
        public readonly List<int> Spots = new List<int>();
        public readonly List<(int A, int B, bool Broken)> Links = new List<(int, int, bool)>();
        public readonly int[] DoorIslands = { -1, -1, -1, -1 };
        public int Trial = -1;
        public int Reward = -1;

        public int IslandOf(int spot) => Spots.IndexOf(spot);

        public int Add(int spot)
        {
            int i = Spots.IndexOf(spot);
            if (i >= 0) return i;
            Spots.Add(spot);
            return Spots.Count - 1;
        }

        public string Signature()
        {
            var sb = new StringBuilder();
            foreach (int s in Spots) sb.Append(IslandLattice.X(s)).Append(',').Append(IslandLattice.Y(s)).Append(' ');
            sb.Append("| ");
            foreach ((int a, int b, bool broken) in Links) sb.Append(a).Append('-').Append(b).Append(broken ? "x " : " ");
            sb.Append("| doors ").Append(string.Join(",", DoorIslands)).Append(" trial ").Append(Trial).Append(" reward ").Append(Reward);
            return sb.ToString();
        }
    }

    /// <summary>
    /// What the solver measures on an island screen's lit network (4.12), from one doorway: the research note's section 1a
    /// metrics. The lit network is every whole plank of the reward island's whole-plank component; broken spans (hopped with
    /// the gap-crosser*, never mended: 4.25) and planks off that component never light.
    /// <list type="bullet">
    /// <item>Planks, Islands: E and V of the lit network. Cycles: E - V + 1. OddIslands: islands with an odd number of lit
    /// planks; OddDoorIslands: those that are a doorway's island.</item>
    /// <item>RandomSolveRate: P_rand, exactly: a random player starts on a uniformly random island of the network and crosses
    /// a uniformly random unlit plank of the island it stands on until none is left; the share of play-outs that light every
    /// plank. *Assumption:* the random player knows nothing of parity, so it starts anywhere.</item>
    /// <item>Trails: every way to light the whole network (an exhaustive count of the trails, each start and direction).</item>
    /// <item>Entry: the network island this doorway's player first stands on (over broken spans when it has to). StartWalk:
    /// planks from there to the nearest island it can start a clean trail from (a start of the trail that is the entry, or
    /// has two planks or more, so stepping out and back snuffs everything and leaves the player on it).</item>
    /// <item>Crossing: the most planks from this doorway's island to another doorway's (a broken span counts as none: it is the
    /// gap-crosser*'s one hop, not a walk); RewardRoute: the planks and spans to the entry, StartWalk and Planks.</item>
    /// <item>RedHerrings: whole planks off the lit network. BrokenInNetwork: spans inside it. Spread: the share of the
    /// lattice inside the network's bounding box. RetrySeconds: the longest walk, after a snuff anywhere on the network, to
    /// the nearest clean start and out and back over its shortest plank, at walking speed.</item>
    /// </list>
    /// </summary>
    public sealed class IslandMetrics
    {
        public int Side = -1;
        public bool Connected;
        public bool Solvable;
        public int Planks;
        public int Islands;
        public int Cycles;
        public int OddIslands;
        public int OddDoorIslands;
        public int RedHerrings;
        public int BrokenInNetwork;
        public double Spread;
        public double RandomSolveRate;
        public long Trails;
        public int Entry = -1;
        public int StartWalk;
        public int Crossing;
        public int RewardRoute;
        public double RetrySeconds;
        public int Traps;

        public override string ToString()
        {
            string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
            return !Connected ? "not joined to the network" :
                $"{(Solvable ? "solvable" : "UNSOLVABLE")}: planks {Planks}, islands {Islands}, cycles {Cycles}, odd {OddIslands} ({OddDoorIslands} at a doorway), " +
                $"P_rand {F(RandomSolveRate * 100)}%, trails {Trails}, red herrings {RedHerrings}, spans inside {BrokenInNetwork}, spread {F(Spread * 100)}%, " +
                $"entry {Entry}, start walk {StartWalk}, crossing {Crossing} vs reward route {RewardRoute}, retry {F(RetrySeconds)} s";
        }
    }

    /// <summary>
    /// The research note's targets for the island screens per tier (puzzle-difficulty-research.md 1a), as pass/fail bands.
    /// Planks, odd islands and P_rand are the note's own. Departures (assumptions, see the 4.12 plan): islands and cycles
    /// are bound by the 5x3 lattice (spread-out islands, planks only between spots in line, so no crossings), where every
    /// open doorway but the gap one sits on the network: early is a single loop of 5-7 islands (an even-only network on
    /// spots in line has no 2-cycle network under 7 islands), mid holds 7-10 islands with 2-4 cycles, late 9-12 islands
    /// with 2-6 cycles (the note's 4-6 needs more islands than the lattice has room for beside the gap doorway, its lookout
    /// and the trial). Red herrings are the gap doorway's lookout planks, so a screen without a gap doorway needs none.
    /// </summary>
    public sealed class IslandTargets
    {
        public readonly PuzzleTier Tier;
        public readonly int MinPlanks, MaxPlanks, MinIslands, MaxIslands, MinCycles, MaxCycles;

        /// <summary>The odd islands wanted (0: a circuit, start anywhere; 2: start on one of them).</summary>
        public readonly int OddIslands;

        /// <summary>How many of the 2 odd islands are a doorway's island (-1: any).</summary>
        public readonly int OddDoorIslands;

        public readonly double MinRandom, MaxRandom;
        public readonly int MinHerrings, MaxHerrings;
        public readonly double MinSpread;
        public readonly double MaxRetrySeconds;

        /// <summary>The crossing between doorways costs at most this share of the reward route (research section 0).</summary>
        public const double CrossingShare = 0.5;

        /// <summary>The most lit planks the solver counts trails for exhaustively.</summary>
        public const int MaxExhaustivePlanks = 16;

        private IslandTargets(PuzzleTier tier, int minPlanks, int maxPlanks, int minIslands, int maxIslands, int minCycles, int maxCycles,
            int odd, int oddDoor, double minRandom, double maxRandom, int minHerrings, int maxHerrings, double retry)
        {
            Tier = tier;
            MinPlanks = minPlanks;
            MaxPlanks = maxPlanks;
            MinIslands = minIslands;
            MaxIslands = maxIslands;
            MinCycles = minCycles;
            MaxCycles = maxCycles;
            OddIslands = odd;
            OddDoorIslands = oddDoor;
            MinRandom = minRandom;
            MaxRandom = maxRandom;
            MinHerrings = minHerrings;
            MaxHerrings = maxHerrings;
            MinSpread = 0.5;
            MaxRetrySeconds = retry;
        }

        public static readonly IslandTargets Early = new IslandTargets(PuzzleTier.Early, 5, 7, 5, 7, 1, 2, 0, -1, 0.40, 1.0, 0, 0, 5);
        public static readonly IslandTargets Mid = new IslandTargets(PuzzleTier.Mid, 8, 11, 7, 10, 2, 4, 2, 1, 0.10, 0.40, 1, 1, 8);
        public static readonly IslandTargets Late = new IslandTargets(PuzzleTier.Late, 12, 16, 9, 12, 2, 6, 2, 0, 0.02, 0.10, 1, 2, 12);

        public static IslandTargets For(PuzzleTier tier) => tier == PuzzleTier.Late ? Late : tier == PuzzleTier.Mid ? Mid : Early;

        /// <summary>The red herrings wanted: the tier's, or none on a screen without a gap doorway to hang its lookout on.</summary>
        public (int Min, int Max) Herrings(bool hasGapDoor) => hasGapDoor ? (MinHerrings, MaxHerrings) : (0, 0);

        /// <summary>Every target a doorway's measure misses (empty when it meets them all).</summary>
        public List<string> Misses(IslandMetrics m, bool hasGapDoor)
        {
            string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
            var misses = new List<string>();
            if (!m.Connected)
            {
                misses.Add("the doorway never reaches the lit network");
                return misses;
            }
            if (!m.Solvable) misses.Add("no clean start can be reached (or the network has no trail)");
            if (m.Planks < MinPlanks || m.Planks > MaxPlanks) misses.Add($"planks {m.Planks} (want {MinPlanks}-{MaxPlanks})");
            if (m.Planks > MaxExhaustivePlanks) misses.Add($"planks {m.Planks} over the solver's {MaxExhaustivePlanks}");
            if (m.Islands < MinIslands || m.Islands > MaxIslands) misses.Add($"islands {m.Islands} (want {MinIslands}-{MaxIslands})");
            if (m.Cycles < MinCycles || m.Cycles > MaxCycles) misses.Add($"cycles {m.Cycles} (want {MinCycles}-{MaxCycles})");
            if (m.OddIslands != OddIslands) misses.Add($"odd islands {m.OddIslands} (want {OddIslands})");
            else if (OddDoorIslands >= 0 && m.OddDoorIslands != OddDoorIslands)
                misses.Add($"odd doorway islands {m.OddDoorIslands} (want {OddDoorIslands})");
            if (m.RandomSolveRate < MinRandom - 1e-9 || m.RandomSolveRate > MaxRandom + 1e-9)
                misses.Add($"P_rand {F(m.RandomSolveRate)} (want {F(MinRandom)}-{F(MaxRandom)})");
            (int lo, int hi) = Herrings(hasGapDoor);
            if (m.RedHerrings < lo || m.RedHerrings > hi) misses.Add($"red herrings {m.RedHerrings} (want {lo}-{hi})");
            if (m.BrokenInNetwork != 0) misses.Add($"{m.BrokenInNetwork} broken spans inside the network (want 0)");
            if (m.Spread < MinSpread - 1e-9) misses.Add($"spread {F(m.Spread)} (want {F(MinSpread)}+)");
            if (m.RetrySeconds > MaxRetrySeconds + 1e-9) misses.Add($"retry {F(m.RetrySeconds)} s (want {MaxRetrySeconds} s at most)");
            if (m.Crossing > CrossingShare * m.RewardRoute + 1e-9) misses.Add($"crossing {m.Crossing} planks against a reward route of {m.RewardRoute}");
            if (m.Traps != 0) misses.Add($"{m.Traps} hard traps");
            return misses;
        }

        public override string ToString() =>
            $"{Tier}: planks {MinPlanks}-{MaxPlanks}, islands {MinIslands}-{MaxIslands}, cycles {MinCycles}-{MaxCycles}, odd {OddIslands}" +
            $"{(OddDoorIslands >= 0 ? $" ({OddDoorIslands} at a doorway)" : "")}, P_rand {MinRandom:0.##}-{MaxRandom:0.##}, red herrings {MinHerrings}-{MaxHerrings}";
    }

    /// <summary>
    /// The island puzzle's solver (4.12), pure: the lit network of a design and what it measures from each doorway
    /// (IslandMetrics): connectivity, parity, a clean start reachable from the doorway, and an exhaustive count of the
    /// trails (and P_rand) over up to IslandTargets.MaxExhaustivePlanks planks.
    /// </summary>
    public sealed class IslandSolver
    {
        private readonly IslandDesign design;
        private readonly int n;
        private readonly int[] component;
        private readonly List<int> network = new List<int>();
        private readonly int[] degree;
        private readonly bool[] onNetwork;
        private readonly List<(int Plank, int To)>[] networkAdjacent;
        private readonly double[] linkLength;
        private readonly int[] bitOf;

        /// <summary>The lit network's planks (indices into the design's links).</summary>
        public IReadOnlyList<int> Network => network;

        /// <summary>The islands of the lit network.</summary>
        public bool OnNetwork(int island) => island >= 0 && island < n && onNetwork[island];

        /// <summary>An island's lit planks.</summary>
        public int Degree(int island) => degree[island];

        public int NetworkIslands { get; }

        /// <summary>The islands a trail may start on (empty when none: the network is not one trail).</summary>
        public List<int> Starts { get; } = new List<int>();

        /// <summary>The islands with an odd number of lit planks.</summary>
        public List<int> Odd { get; } = new List<int>();

        /// <summary>
        /// The solver over a design. centres (optional) gives each island's middle (x, y) for walking distances; without
        /// them the lattice spacing stands in. walkSpeed is the player's base speed (units a second).
        /// </summary>
        public IslandSolver(IslandDesign design, Func<int, (double X, double Y)> centres = null, double walkSpeed = 9.0)
        {
            this.design = design ?? throw new ArgumentNullException(nameof(design));
            WalkSpeed = walkSpeed;
            n = design.Spots.Count;
            Centres = centres ?? (i => (IslandLattice.X(design.Spots[i]) * 4.6, IslandLattice.Y(design.Spots[i]) * 4.05));
            component = WholeComponents(design);
            degree = new int[n];
            onNetwork = new bool[n];
            networkAdjacent = new List<(int, int)>[n];
            for (int i = 0; i < n; i++) networkAdjacent[i] = new List<(int, int)>();
            linkLength = new double[design.Links.Count];
            bitOf = new int[design.Links.Count];
            for (int k = 0; k < bitOf.Length; k++) bitOf[k] = -1;
            for (int k = 0; k < design.Links.Count; k++)
            {
                (int a, int b, _) = design.Links[k];
                (double ax, double ay) = Centres(a);
                (double bx, double by) = Centres(b);
                linkLength[k] = Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
            }
            if (design.Reward >= 0 && design.Reward < n)
            {
                int c = component[design.Reward];
                for (int k = 0; k < design.Links.Count; k++)
                {
                    (int a, int b, bool broken) = design.Links[k];
                    if (broken || component[a] != c) continue;
                    bitOf[k] = network.Count;
                    network.Add(k);
                    degree[a]++;
                    degree[b]++;
                    networkAdjacent[a].Add((k, b));
                    networkAdjacent[b].Add((k, a));
                }
                for (int i = 0; i < n; i++)
                    if (component[i] == c && (degree[i] > 0 || i == design.Reward))
                    {
                        onNetwork[i] = true;
                        NetworkIslands++;
                        if (degree[i] % 2 == 1) Odd.Add(i);
                    }
                if (network.Count > 0 && (Odd.Count == 0 || Odd.Count == 2))
                    for (int i = 0; i < n; i++)
                        if (onNetwork[i] && (Odd.Count == 0 || Odd.Contains(i))) Starts.Add(i);
            }
        }

        public Func<int, (double X, double Y)> Centres { get; }
        public double WalkSpeed { get; }

        /// <summary>The island of each island's whole-plank component (islands with the same number are joined).</summary>
        public static int[] WholeComponents(IslandDesign design)
        {
            int n = design.Spots.Count;
            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;
            int Find(int i)
            {
                while (parent[i] != i) i = parent[i] = parent[parent[i]];
                return i;
            }
            foreach ((int a, int b, bool broken) in design.Links)
                if (!broken && a >= 0 && b >= 0 && a < n && b < n) parent[Find(a)] = Find(b);
            var result = new int[n];
            for (int i = 0; i < n; i++) result[i] = Find(i);
            return result;
        }

        /// <summary>The position of a lit plank in Network (a bit of the lit mask), or -1 for a plank off the network.</summary>
        public int Bit(int plank) => plank >= 0 && plank < bitOf.Length ? bitOf[plank] : -1;

        /// <summary>True when an island can be made a clean start: stepping out and back over one of two lit planks or more.</summary>
        public bool CleanStart(int island) => OnNetwork(island) && degree[island] >= 2;

        // ------------------------------------------------------------------ Trails

        private Dictionary<long, double> random;
        private Dictionary<long, long> counts;

        private double Random(int at, int mask)
        {
            int full = (1 << network.Count) - 1;
            if (mask == full) return 1.0;
            long key = ((long)mask << 5) | (uint)at;
            if (random.TryGetValue(key, out double p)) return p;
            int options = 0;
            double sum = 0;
            foreach ((int k, int to) in networkAdjacent[at])
            {
                int bit = 1 << bitOf[k];
                if ((mask & bit) != 0) continue;
                options++;
                sum += Random(to, mask | bit);
            }
            p = options == 0 ? 0.0 : sum / options;
            random[key] = p;
            return p;
        }

        private long Count(int at, int mask)
        {
            int full = (1 << network.Count) - 1;
            if (mask == full) return 1;
            long key = ((long)mask << 5) | (uint)at;
            if (counts.TryGetValue(key, out long c)) return c;
            c = 0;
            foreach ((int k, int to) in networkAdjacent[at])
            {
                int bit = 1 << bitOf[k];
                if ((mask & bit) == 0) c += Count(to, mask | bit);
            }
            counts[key] = c;
            return c;
        }

        /// <summary>P_rand over the whole network (uniform start island), exactly; 0 for an empty or too big network.</summary>
        public double RandomSolveRate()
        {
            if (network.Count == 0 || network.Count > 24 || NetworkIslands == 0) return 0;
            random = random ?? new Dictionary<long, double>();
            double sum = 0;
            for (int i = 0; i < n; i++)
                if (onNetwork[i]) sum += Random(i, 0);
            return sum / NetworkIslands;
        }

        /// <summary>Every trail that lights the whole network (each start and direction counted), exhaustively; -1 past the bound.</summary>
        public long TrailCount()
        {
            if (network.Count > IslandTargets.MaxExhaustivePlanks) return -1;
            if (Starts.Count == 0) return 0;
            counts = counts ?? new Dictionary<long, long>();
            long total = 0;
            foreach (int s in Starts) total += Count(s, 0);
            return total;
        }

        /// <summary>One trail lighting the whole network from a start (planks in order), or null.</summary>
        public List<int> Trail(int start)
        {
            if (!Starts.Contains(start)) return null;
            // A depth-first search over the trails (the network is small): the first complete one, planks tried in order.
            var path = new List<int>();
            var used = new bool[design.Links.Count];
            bool Go(int at)
            {
                if (path.Count == network.Count) return true;
                foreach ((int k, int to) in networkAdjacent[at])
                {
                    if (used[k]) continue;
                    used[k] = true;
                    path.Add(k);
                    if (Go(to)) return true;
                    path.RemoveAt(path.Count - 1);
                    used[k] = false;
                }
                return false;
            }
            return Go(start) ? path : null;
        }

        // ------------------------------------------------------------------ Distances

        /// <summary>
        /// Fewest planks between two islands (broken spans included when spans, each counting spanCost: a broken span is the
        /// gap-crosser*'s one hop, so the crossing rule counts it as 0), or -1 when they do not join.
        /// </summary>
        public int Hops(int from, int to, bool spans, bool networkOnly = false, int spanCost = 1)
        {
            if (from < 0 || to < 0) return -1;
            var dist = new int[n];
            for (int i = 0; i < n; i++) dist[i] = int.MaxValue;
            dist[from] = 0;
            var queue = new LinkedList<int>();
            queue.AddLast(from);
            while (queue.Count > 0)
            {
                int at = queue.First.Value;
                queue.RemoveFirst();
                for (int k = 0; k < design.Links.Count; k++)
                {
                    (int a, int b, bool broken) = design.Links[k];
                    if (broken && !spans) continue;
                    if (networkOnly && bitOf[k] < 0) continue;
                    int other = a == at ? b : b == at ? a : -1;
                    if (other < 0) continue;
                    int cost = broken ? spanCost : 1;
                    if (dist[at] + cost >= dist[other]) continue;
                    dist[other] = dist[at] + cost;
                    if (cost == 0) queue.AddFirst(other);
                    else queue.AddLast(other);
                }
            }
            return dist[to] == int.MaxValue ? -1 : dist[to];
        }

        /// <summary>Shortest walk (units, centre to centre) between two network islands over lit planks.</summary>
        private double[] Walks(int from)
        {
            var dist = new double[n];
            for (int i = 0; i < n; i++) dist[i] = double.MaxValue;
            dist[from] = 0;
            var done = new bool[n];
            for (int round = 0; round < n; round++)
            {
                int at = -1;
                for (int i = 0; i < n; i++)
                    if (!done[i] && dist[i] < double.MaxValue && (at < 0 || dist[i] < dist[at])) at = i;
                if (at < 0) break;
                done[at] = true;
                foreach ((int k, int to) in networkAdjacent[at])
                    if (dist[at] + linkLength[k] < dist[to]) dist[to] = dist[at] + linkLength[k];
            }
            return dist;
        }

        // ------------------------------------------------------------------ Measure

        /// <summary>
        /// What a doorway's player meets (doorway side 0-3, North, East, South, West). doorOpen tells which sides have a
        /// doorway (for Crossing).
        /// </summary>
        public IslandMetrics Measure(int side, Func<int, bool> doorOpen)
        {
            var m = new IslandMetrics { Side = side, Planks = network.Count, Islands = NetworkIslands, OddIslands = Odd.Count };
            m.Cycles = m.Planks - m.Islands + 1;
            foreach (int i in Odd)
                if (Array.IndexOf(design.DoorIslands, i) >= 0) m.OddDoorIslands++;
            for (int k = 0; k < design.Links.Count; k++)
            {
                (int a, int b, bool broken) = design.Links[k];
                if (!broken && bitOf[k] < 0) m.RedHerrings++;
                if (broken && onNetwork[a] && onNetwork[b]) m.BrokenInNetwork++;
            }
            int minX = int.MaxValue, maxX = -1, minY = int.MaxValue, maxY = -1;
            for (int i = 0; i < n; i++)
            {
                if (!onNetwork[i]) continue;
                int x = IslandLattice.X(design.Spots[i]), y = IslandLattice.Y(design.Spots[i]);
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
            m.Spread = maxX < 0 ? 0 : (double)(maxX - minX + 1) * (maxY - minY + 1) / IslandLattice.SpotCount;
            int door = side >= 0 && side < 4 ? design.DoorIslands[side] : -1;
            if (door < 0 || network.Count == 0) return m;
            // The entry: the nearest network island over every plank and span (the gap-crosser* in hand).
            int entry = -1, toEntry = int.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (!onNetwork[i]) continue;
                int h = Hops(door, i, true);
                if (h >= 0 && h < toEntry)
                {
                    toEntry = h;
                    entry = i;
                }
            }
            if (entry < 0) return m;
            m.Connected = true;
            m.Entry = entry;
            m.RandomSolveRate = RandomSolveRate();
            m.Trails = TrailCount();
            int walk = int.MaxValue;
            foreach (int s in Starts)
                if (s == entry || CleanStart(s))
                {
                    int h = Hops(entry, s, false, true);
                    if (h >= 0 && h < walk) walk = h;
                }
            m.Solvable = walk < int.MaxValue && network.Count <= 24;
            m.StartWalk = m.Solvable ? walk : 0;
            m.RewardRoute = toEntry + m.StartWalk + m.Planks;
            for (int f = 0; f < 4; f++)
            {
                int other = design.DoorIslands[f];
                if (f == side || other < 0 || (doorOpen != null && !doorOpen(f))) continue;
                int h = Hops(door, other, true, false, 0);
                if (h > m.Crossing) m.Crossing = h;
            }
            // Retry: from anywhere on the network after a snuff, to the nearest clean start, out and back over its shortest plank.
            double worst = 0;
            var cleanStarts = new List<int>();
            foreach (int s in Starts)
                if (CleanStart(s)) cleanStarts.Add(s);
            if (cleanStarts.Count == 0) worst = double.MaxValue;
            else
            {
                var walks = new Dictionary<int, double[]>();
                foreach (int s in cleanStarts) walks[s] = Walks(s);
                for (int u = 0; u < n; u++)
                {
                    if (!onNetwork[u]) continue;
                    double best = double.MaxValue;
                    foreach (int s in cleanStarts)
                    {
                        double shortest = double.MaxValue;
                        foreach ((int k, _) in networkAdjacent[s]) shortest = Math.Min(shortest, linkLength[k]);
                        best = Math.Min(best, walks[s][u] + 2 * shortest);
                    }
                    worst = Math.Max(worst, best);
                }
            }
            m.RetrySeconds = worst == double.MaxValue ? double.MaxValue : worst / WalkSpeed;
            return m;
        }
    }
}
