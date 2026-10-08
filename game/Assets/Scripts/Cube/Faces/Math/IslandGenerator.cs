using System;
using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>
    /// Searches an island screen's design for a tier (4.12), pure. The doorways are shuffled into roles: every open doorway
    /// but the third sits on the lit network (its island is one of the network's); the third (the gap doorway) reaches the
    /// network only over one broken span, and its own island carries the tier's red herrings (lookout planks off the
    /// network). A local search toggles one plank of the network at a time (neighbours, or two spots apart over an empty
    /// middle) and keeps a change while the distance from the tier's targets does not grow (light annealing). Once the
    /// network meets its targets (planks, islands, cycles, parity, a clean start, spread, P_rand) the rest is dealt: the
    /// reward island (an inner network island), the gap doorway's span and lookouts, and the trial island behind a broken
    /// span. The whole design must then pass every doorway's targets (IslandTargets.Misses) and accept.
    /// </summary>
    public static class IslandGenerator
    {
        public const int Attempts = 300;
        public const int StepsPerAttempt = 1200;

        /// <summary>Harness hook: why a dealt design was turned down.</summary>
        public static Action<string> Rejected;

        /// <summary>
        /// A design, or null past the budget. doors: which sides have a doorway (North, East, South, West). canBreak: whether
        /// a plank between two spots is long enough to hold a broken span. accept: the caller's last check (geometry).
        /// </summary>
        public static IslandDesign Generate(SeededRng rng, PuzzleTier tier, bool[] doors, bool trial, Func<int, int, bool> canBreak,
            Func<int, (double X, double Y)> spotCentre, Func<IslandDesign, bool> accept, out int steps)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            steps = 0;
            var sides = new List<int>();
            for (int f = 0; f < 4; f++)
                if (doors[f]) sides.Add(f);
            if (sides.Count == 0) return null;
            rng.Shuffle(sides);
            var search = new Search(rng, IslandTargets.For(tier), doors, sides, trial, canBreak, spotCentre, accept);
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                IslandDesign found = search.Attempt(ref steps);
                if (found != null) return found;
            }
            return null;
        }

        private sealed class Search
        {
            private readonly SeededRng rng;
            private readonly IslandTargets t;
            private readonly bool[] doors;
            private readonly List<int> netDoors = new List<int>();
            private readonly int gapSide = -1, gapSpot = -1;
            private readonly bool[] banned = new bool[IslandLattice.SpotCount];
            private readonly bool trial;
            private readonly Func<int, int, bool> canBreak;
            private readonly Func<int, (double X, double Y)> spotCentre;
            private readonly Func<IslandDesign, bool> accept;
            private readonly List<int> candidates = new List<int>();
            private readonly int herringsMin, herringsMax;

            public Search(SeededRng rng, IslandTargets t, bool[] doors, List<int> sides, bool trial, Func<int, int, bool> canBreak,
                Func<int, (double X, double Y)> spotCentre, Func<IslandDesign, bool> accept)
            {
                this.rng = rng;
                this.t = t;
                this.doors = doors;
                this.trial = trial;
                this.canBreak = canBreak;
                this.spotCentre = spotCentre;
                this.accept = accept;
                for (int i = 0; i < sides.Count; i++)
                {
                    if (i == 2)
                    {
                        gapSide = sides[i];
                        gapSpot = IslandLattice.DoorSpots[gapSide];
                    }
                    else netDoors.Add(IslandLattice.DoorSpots[sides[i]]);
                }
                for (int f = 0; f < 4; f++)
                    if (!doors[f]) banned[IslandLattice.DoorSpots[f]] = true;
                if (gapSpot >= 0) banned[gapSpot] = true;
                for (int i = 0; i < IslandLattice.Links.Length; i++)
                    if (!banned[IslandLattice.Links[i].A] && !banned[IslandLattice.Links[i].B]) candidates.Add(i);
                (herringsMin, herringsMax) = t.Herrings(gapSpot >= 0);
            }

            /// <summary>The gap doorway's spot: its island stands there, so no long plank may pass over it (4.12 review).</summary>
            private int Reserved => gapSpot >= 0 ? 1 << gapSpot : 0;

            private static int Occupied(bool[] on)
            {
                int mask = 0;
                for (int i = 0; i < on.Length; i++)
                    if (on[i]) mask |= (1 << IslandLattice.Links[i].A) | (1 << IslandLattice.Links[i].B);
                return mask;
            }

            /// <summary>No long plank over an island, and no two long planks over one middle (they would cross).</summary>
            private static bool Valid(bool[] on, int occupied, out int middles)
            {
                middles = 0;
                for (int i = 0; i < on.Length; i++)
                {
                    if (!on[i]) continue;
                    int m = IslandLattice.Links[i].Middle;
                    if (m < 0) continue;
                    if ((occupied & (1 << m)) != 0 || (middles & (1 << m)) != 0) return false;
                    middles |= 1 << m;
                }
                return true;
            }

            private static bool Has(int mask, int spot) => (mask & (1 << spot)) != 0;

            /// <summary>Distance from the network's targets (0: they are all met, P_rand included).</summary>
            private double Score(bool[] on)
            {
                int occupied = Occupied(on);
                if (!Valid(on, occupied | Reserved, out int middles)) return double.MaxValue;
                var degree = new int[IslandLattice.SpotCount];
                var parent = new int[IslandLattice.SpotCount];
                for (int i = 0; i < parent.Length; i++) parent[i] = i;
                int Find(int i)
                {
                    while (parent[i] != i) i = parent[i] = parent[parent[i]];
                    return i;
                }
                int e = 0;
                for (int i = 0; i < on.Length; i++)
                {
                    if (!on[i]) continue;
                    (int a, int b, _) = IslandLattice.Links[i];
                    degree[a]++;
                    degree[b]++;
                    parent[Find(a)] = Find(b);
                    e++;
                }
                int v = 0, roots = 0, inner = 0, odd = 0, oddDoor = 0, oddAlone = 0;
                int minX = 9, maxX = -1, minY = 9, maxY = -1;
                for (int s = 0; s < IslandLattice.SpotCount; s++)
                {
                    if (!Has(occupied, s)) continue;
                    v++;
                    if (Find(s) == s) roots++;
                    if (IslandLattice.SideOf(s) < 0) inner++;
                    if (degree[s] % 2 == 1)
                    {
                        odd++;
                        if (IslandLattice.SideOf(s) >= 0) oddDoor++;
                        if (degree[s] == 1) oddAlone++;
                    }
                    minX = Math.Min(minX, IslandLattice.X(s));
                    maxX = Math.Max(maxX, IslandLattice.X(s));
                    minY = Math.Min(minY, IslandLattice.Y(s));
                    maxY = Math.Max(maxY, IslandLattice.Y(s));
                }
                double score = 0;
                int Band(int x, int lo, int hi) => Math.Max(0, Math.Max(lo - x, x - hi));
                score += 2 * Band(e, t.MinPlanks, t.MaxPlanks) + Band(v, t.MinIslands, t.MaxIslands) + Band(e - v + 1, t.MinCycles, t.MaxCycles);
                if (roots > 1) score += 3 * (roots - 1);
                foreach (int d in netDoors)
                    if (!Has(occupied, d)) score += 4;
                if (t.OddIslands == 0) score += odd;
                else
                {
                    score += Math.Abs(odd - 2);
                    if (odd == 2 && t.OddDoorIslands >= 0) score += Math.Abs(oddDoor - t.OddDoorIslands);
                    if (odd == 2 && oddAlone == 2) score += 2; // neither start can be made clean
                }
                if (inner == 0) score += 3;
                if (v > 0 && (double)(maxX - minX + 1) * (maxY - minY + 1) / IslandLattice.SpotCount < t.MinSpread) score += 1;
                // A doorway-to-doorway crossing over the network stays at most half the planks.
                if (roots == 1 && netDoors.Count >= 2)
                {
                    int worst = 0;
                    foreach (int a in netDoors)
                        foreach (int b in netDoors)
                            if (a < b && Has(occupied, a) && Has(occupied, b)) worst = Math.Max(worst, Hops(on, a, b));
                    double limit = IslandTargets.CrossingShare * e;
                    if (worst > limit) score += worst - limit;
                }
                // The gap doorway can be spanned to the network, cheaply enough to cross by, and the lookouts and the trial have room.
                if (gapSpot >= 0)
                {
                    double best = double.MaxValue;
                    foreach (int s in SpanTargets(occupied, middles))
                    {
                        int worst = 0;
                        foreach (int d in netDoors) worst = Math.Max(worst, Hops(on, s, d));
                        best = Math.Min(best, worst);
                    }
                    if (best == double.MaxValue) score += 2;
                    else if (best > IslandTargets.CrossingShare * (1 + e)) score += best - IslandTargets.CrossingShare * (1 + e);
                }
                int free = 0;
                for (int s = 0; s < IslandLattice.SpotCount; s++)
                    if (!banned[s] && !Has(occupied, s) && !Has(middles, s)) free++;
                int need = herringsMin + (trial ? 1 : 0);
                if (free < need) score += 2 * (need - free);
                if (score > 0) return score;
                double p = new IslandSolver(NetworkOnly(on, occupied)).RandomSolveRate();
                if (p < t.MinRandom) score += 0.5 + 20 * (t.MinRandom - p);
                if (p > t.MaxRandom) score += 0.5 + 20 * (p - t.MaxRandom);
                return score;
            }

            /// <summary>The network islands the gap doorway's broken span could reach.</summary>
            private List<int> SpanTargets(int occupied, int middles)
            {
                var targets = new List<int>();
                for (int s = 0; s < IslandLattice.SpotCount; s++)
                    if (Has(occupied, s) && IslandLattice.InLine(gapSpot, s, out int m) && canBreak(gapSpot, s) &&
                        (m < 0 || (!Has(occupied, m) && !Has(middles, m))))
                        targets.Add(s);
                return targets;
            }

            private static int Hops(bool[] on, int from, int to)
            {
                var dist = new int[IslandLattice.SpotCount];
                for (int i = 0; i < dist.Length; i++) dist[i] = -1;
                dist[from] = 0;
                var queue = new Queue<int>();
                queue.Enqueue(from);
                while (queue.Count > 0)
                {
                    int at = queue.Dequeue();
                    if (at == to) return dist[at];
                    for (int i = 0; i < on.Length; i++)
                    {
                        if (!on[i]) continue;
                        (int a, int b, _) = IslandLattice.Links[i];
                        int other = a == at ? b : b == at ? a : -1;
                        if (other < 0 || dist[other] >= 0) continue;
                        dist[other] = dist[at] + 1;
                        queue.Enqueue(other);
                    }
                }
                return int.MaxValue / 2;
            }

            /// <summary>The network alone as a design (its first island the reward), for P_rand.</summary>
            private static IslandDesign NetworkOnly(bool[] on, int occupied)
            {
                var d = new IslandDesign();
                for (int s = 0; s < IslandLattice.SpotCount; s++)
                    if (Has(occupied, s)) d.Add(s);
                for (int i = 0; i < on.Length; i++)
                    if (on[i]) d.Links.Add((d.IslandOf(IslandLattice.Links[i].A), d.IslandOf(IslandLattice.Links[i].B), false));
                d.Reward = 0;
                return d;
            }

            /// <summary>A start: the network doorways joined by shortest lattice paths of neighbouring spots.</summary>
            private bool[] Start()
            {
                var on = new bool[IslandLattice.Links.Length];
                if (netDoors.Count == 0) return on;
                var joined = new HashSet<int> { netDoors[0] };
                for (int i = 1; i < netDoors.Count; i++)
                {
                    List<int> path = Path(netDoors[i], joined);
                    if (path == null) continue;
                    for (int k = 0; k + 1 < path.Count; k++) on[IslandLattice.LinkIndex(path[k], path[k + 1])] = true;
                    joined.UnionWith(path);
                }
                return on;
            }

            private List<int> Path(int from, HashSet<int> goal)
            {
                var previous = new Dictionary<int, int> { [from] = from };
                var queue = new Queue<int>();
                queue.Enqueue(from);
                while (queue.Count > 0)
                {
                    int at = queue.Dequeue();
                    if (at != from && goal.Contains(at))
                    {
                        var path = new List<int> { at };
                        while (at != from) path.Add(at = previous[at]);
                        path.Reverse();
                        return path;
                    }
                    var next = new List<int>();
                    int x = IslandLattice.X(at), y = IslandLattice.Y(at);
                    if (x > 0) next.Add(at - 1);
                    if (x < IslandLattice.Columns - 1) next.Add(at + 1);
                    if (y > 0) next.Add(at - IslandLattice.Columns);
                    if (y < IslandLattice.Rows - 1) next.Add(at + IslandLattice.Columns);
                    rng.Shuffle(next);
                    foreach (int s in next)
                    {
                        if (banned[s] || previous.ContainsKey(s)) continue;
                        previous[s] = at;
                        queue.Enqueue(s);
                    }
                }
                return null;
            }

            public IslandDesign Attempt(ref int steps)
            {
                bool[] on = Start();
                double current = Score(on);
                for (int step = 0; step < StepsPerAttempt; step++)
                {
                    steps++;
                    int link = candidates[rng.NextInt(candidates.Count)];
                    on[link] = !on[link];
                    double s = Score(on);
                    if (s == double.MaxValue || (s > current && rng.NextInt(100) >= 3))
                    {
                        on[link] = !on[link];
                        continue;
                    }
                    current = s;
                    if (current > 0) continue;
                    IslandDesign design = Complete(on);
                    if (design != null) return design;
                }
                return null;
            }

            /// <summary>Deals the rest round a network that meets its targets: reward, gap span, lookouts, trial. Null when it fails.</summary>
            private IslandDesign Complete(bool[] on)
            {
                int occupied = Occupied(on);
                Valid(on, occupied | Reserved, out int middles);
                var d = new IslandDesign();
                for (int s = 0; s < IslandLattice.SpotCount; s++)
                    if (Has(occupied, s)) d.Add(s);
                for (int i = 0; i < on.Length; i++)
                    if (on[i]) d.Links.Add((d.IslandOf(IslandLattice.Links[i].A), d.IslandOf(IslandLattice.Links[i].B), false));
                var inner = new List<int>();
                for (int i = 0; i < d.Spots.Count; i++)
                    if (IslandLattice.SideOf(d.Spots[i]) < 0) inner.Add(i);
                if (inner.Count == 0) return null;
                d.Reward = inner[rng.NextInt(inner.Count)];
                int islands = occupied;

                bool Open(int from, int to, bool broken)
                {
                    if (Has(islands, to) || banned[to] && to != gapSpot || Has(middles, to)) return false;
                    if (!IslandLattice.InLine(from, to, out int m)) return false;
                    if (broken && !canBreak(from, to)) return false;
                    return m < 0 || (!Has(islands, m) && !Has(middles, m));
                }
                void Join(int from, int to, bool broken)
                {
                    IslandLattice.InLine(from, to, out int m);
                    if (m >= 0) middles |= 1 << m;
                    d.Links.Add((d.Add(from), d.Add(to), broken));
                    islands |= (1 << from) | (1 << to);
                }

                if (gapSpot >= 0)
                {
                    // The gap doorway: one broken span onto the network (each option in a seeded order, the first whose
                    // crossing passes), then its lookouts (the red herrings): a chain of whole planks out from its island.
                    List<int> spans = SpanTargets(occupied, middles);
                    rng.Shuffle(spans);
                    var solver = new IslandSolver(d);
                    int chosen = -1;
                    foreach (int s in spans)
                    {
                        int worst = 0;
                        foreach (int door in netDoors) worst = Math.Max(worst, solver.Hops(d.IslandOf(s), d.IslandOf(door), false));
                        int startWalk = int.MaxValue;
                        foreach (int st in solver.Starts)
                            if (st == d.IslandOf(s) || solver.CleanStart(st)) startWalk = Math.Min(startWalk, solver.Hops(d.IslandOf(s), st, false, true));
                        if (startWalk == int.MaxValue) continue;
                        if (worst <= IslandTargets.CrossingShare * (1 + startWalk + solver.Network.Count) + 1e-9)
                        {
                            chosen = s;
                            break;
                        }
                    }
                    if (chosen < 0) return null;
                    Join(gapSpot, chosen, true);
                    int herrings = herringsMin + (herringsMax > herringsMin ? rng.NextInt(herringsMax - herringsMin + 1) : 0);
                    int from = gapSpot;
                    for (int h = 0; h < herrings; h++)
                    {
                        var options = new List<int>();
                        for (int s = 0; s < IslandLattice.SpotCount; s++)
                            if (Open(from, s, false)) options.Add(s);
                        if (options.Count == 0) return null;
                        int to = options[rng.NextInt(options.Count)];
                        Join(from, to, false);
                        from = to;
                    }
                }
                if (trial)
                {
                    var options = new List<(int, int)>();
                    for (int s = 0; s < IslandLattice.SpotCount; s++)
                    {
                        if (!Has(occupied, s)) continue;
                        for (int f = 0; f < IslandLattice.SpotCount; f++)
                            if (Open(s, f, true)) options.Add((s, f));
                    }
                    if (options.Count == 0) return null;
                    (int at, int to) = options[rng.NextInt(options.Count)];
                    Join(at, to, true);
                    d.Trial = d.IslandOf(to);
                }
                for (int f = 0; f < 4; f++)
                    if (doors[f]) d.DoorIslands[f] = d.IslandOf(IslandLattice.DoorSpots[f]);
                return Check(d) && (accept == null || accept(d)) ? d : null;
            }

            private bool Check(IslandDesign d)
            {
                var solver = new IslandSolver(d, spotCentre != null ? (Func<int, (double, double)>)(i => spotCentre(d.Spots[i])) : null);
                bool hasGap = gapSpot >= 0;
                for (int f = 0; f < 4; f++)
                {
                    if (!doors[f]) continue;
                    var misses = t.Misses(solver.Measure(f, s => doors[s]), hasGap);
                    if (misses.Count > 0) { Rejected?.Invoke(string.Join("; ", misses)); return false; }
                }
                return true;
            }
        }
    }
}
