using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>One of the cube's 12 edges: the two faces it joins (A before B in FaceId order).</summary>
    public readonly struct CubeEdge : IEquatable<CubeEdge>
    {
        public readonly FaceId A, B;

        public CubeEdge(FaceId a, FaceId b)
        {
            if (a == b) throw new ArgumentException("An edge joins two different faces");
            if (a < b)
            {
                A = a;
                B = b;
            }
            else
            {
                A = b;
                B = a;
            }
        }

        /// <summary>The edge a side of a face lies on.</summary>
        public static CubeEdge Of(FaceId face, Facing side) => new CubeEdge(face, CubeModel.NeighborFace(face, side));

        public bool Touches(FaceId face) => A == face || B == face;

        /// <summary>The side of a face (A or B) this edge lies on.</summary>
        public Facing SideOn(FaceId face)
        {
            FaceId other = face == A ? B : A;
            for (int f = 0; f < 4; f++)
                if (CubeModel.NeighborFace(face, (Facing)f) == other) return (Facing)f;
            throw new ArgumentException($"{this} does not touch {face}");
        }

        /// <summary>The cube's 12 edges, in a fixed order (by A, then B).</summary>
        public static IReadOnlyList<CubeEdge> All
        {
            get
            {
                var set = new SortedSet<int>();
                for (int f = 0; f < CubeSettings.FaceCount; f++)
                    for (int s = 0; s < 4; s++)
                    {
                        CubeEdge e = Of((FaceId)f, (Facing)s);
                        set.Add((int)e.A * 8 + (int)e.B);
                    }
                return set.Select(k => new CubeEdge((FaceId)(k / 8), (FaceId)(k % 8))).ToList();
            }
        }

        public bool Equals(CubeEdge o) => A == o.A && B == o.B;
        public override bool Equals(object obj) => obj is CubeEdge o && Equals(o);
        public override int GetHashCode() => (int)A * 8 + (int)B;
        public static bool operator ==(CubeEdge a, CubeEdge b) => a.Equals(b);
        public static bool operator !=(CubeEdge a, CubeEdge b) => !a.Equals(b);
        public override string ToString() => $"{A}-{B}";
    }

    /// <summary>What opens a face-edge arch (4.9).</summary>
    public enum LockKind
    {
        /// <summary>No lock (only the generator's fallback, which the sweep reports).</summary>
        Open = 0,

        /// <summary>A key (RunRewards: the run's starting key or one found in a chest), used up.</summary>
        Key = 1,

        /// <summary>A face item (Item: its home theme), held in the inventory; never used up.</summary>
        Item = 2,
    }

    /// <summary>The lock on one cube edge: every face-edge arch along it (both faces' sides) shares it and opens with it.</summary>
    public readonly struct EdgeLock
    {
        public readonly CubeEdge Edge;
        public readonly LockKind Kind;

        /// <summary>Item: the item's home theme (any variant of it opens the lock).</summary>
        public readonly Theme Item;

        public EdgeLock(CubeEdge edge, LockKind kind, Theme item = Theme.Town)
        {
            Edge = edge;
            Kind = kind;
            Item = kind == LockKind.Item ? item : Theme.Town;
        }

        public override string ToString() => Kind == LockKind.Item ? $"{Edge}:{Item} item" : $"{Edge}:{Kind}";
    }

    /// <summary>What the lock check found (EdgeLocks.Check): its problems, the possible first edges and how much it explored.</summary>
    public sealed class LockReport
    {
        public readonly List<string> Problems = new List<string>();

        /// <summary>The edges that can be the first one opened (from Town, with the starting key).</summary>
        public readonly List<CubeEdge> FirstEdges = new List<CubeEdge>();

        /// <summary>Spending states explored (each a set of key locks opened, whatever the order).</summary>
        public int States;

        public bool Passed => Problems.Count == 0;
    }

    /// <summary>
    /// Pure, seeded locks on the cube's face edges (4.9). Every face-edge doorway is a gate arch (1.25); now each cube edge has a
    /// lock that all its arches share (both faces' sides): a key (used up) or a specific face item (held, never used up). The
    /// player starts every run with StartKeys key(s) and finds KeysPerFace more on each science face (RewardPlacement); chest
    /// keys (the bonus alcoves' own pool) never open an arch, so the bonus alcoves can never starve the arches.
    /// <para>
    /// Generate draws on its own stream (RngStreams.EdgeLocks): Town's four edges get 2 or 3 key locks (so there are several
    /// first routes out of Town, chosen with the starting key) and item locks on the rest; every other edge is a key lock one
    /// time in three, else an item lock. Each draw is checked (Check) against the run's content (LockWorld) over every order
    /// of spending the keys; a draw that could soft-lock is drawn again (up to MaxAttempts; then the fallback keeps Town's
    /// edges key-locked and opens the rest, which the sweep reports).
    /// </para>
    /// </summary>
    public sealed class EdgeLocks
    {
        public const ulong RngStream = RngStreams.EdgeLocks;

        /// <summary>Keys the player starts every run with (the way out of Town).</summary>
        public const int StartKeys = 1;

        /// <summary>The fewest of Town's edges with a key lock: each is a possible first route out of Town.</summary>
        public const int MinTownKeyLocks = 2;

        /// <summary>The fewest edges that can be the first one opened (the sweep's variety rule).</summary>
        public const int MinFirstEdges = 2;

        /// <summary>Draws tried before the fallback.</summary>
        public const int MaxAttempts = 64;

        private readonly Dictionary<CubeEdge, EdgeLock> locks = new Dictionary<CubeEdge, EdgeLock>();

        public int Seed { get; }

        /// <summary>Every locked (or, in the fallback, open) edge between two unsealed faces, in CubeEdge.All order.</summary>
        public IReadOnlyList<EdgeLock> Locks { get; }

        /// <summary>Which draw was kept (1-based); MaxAttempts + 1 for the fallback.</summary>
        public int Attempts { get; }

        public bool FellBack => Attempts > MaxAttempts;

        public EdgeLocks(int seed, IEnumerable<EdgeLock> locks, int attempts = 1)
        {
            Seed = seed;
            var list = new List<EdgeLock>(locks ?? Array.Empty<EdgeLock>());
            foreach (EdgeLock l in list) this.locks[l.Edge] = l;
            Locks = list;
            Attempts = attempts;
        }

        /// <summary>The lock on an edge (Open when it has none, e.g. into a sealed face).</summary>
        public EdgeLock LockOf(CubeEdge edge) => locks.TryGetValue(edge, out EdgeLock l) ? l : new EdgeLock(edge, LockKind.Open);

        /// <summary>The lock on the edge a side of a face lies on.</summary>
        public EdgeLock LockOf(FaceId face, Facing side) => LockOf(CubeEdge.Of(face, side));

        public int Count(LockKind kind) => Locks.Count(l => l.Kind == kind);

        /// <summary>The run's locks, as the game (CubeWorld) and the sweep both make them.</summary>
        public static EdgeLocks Generate(LockWorld world) => Generate(world, out _);

        /// <summary>The run's locks and the check of the kept draw.</summary>
        public static EdgeLocks Generate(LockWorld world, out LockReport report)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            CubeModel model = world.Model;
            var rng = new SeededRng(unchecked((ulong)(uint)model.Seed), RngStream);
            var edges = new List<CubeEdge>();
            foreach (CubeEdge e in CubeEdge.All)
                if (!model.IsSealed(e.A) && !model.IsSealed(e.B)) edges.Add(e);
            List<CubeEdge> town = edges.Where(e => e.Touches(CubeModel.StartFace)).ToList();
            List<CubeEdge> rest = edges.Where(e => !e.Touches(CubeModel.StartFace)).ToList();
            List<Theme> items = world.Items;

            EdgeLock Item(CubeEdge e) => items.Count == 0
                ? new EdgeLock(e, LockKind.Key)
                : new EdgeLock(e, LockKind.Item, items[rng.NextInt(items.Count)]);

            string lastProblem = null;
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var drawn = new List<EdgeLock>();
                var townOrder = new List<CubeEdge>(town);
                rng.Shuffle(townOrder);
                int townKeys = Math.Min(townOrder.Count, MinTownKeyLocks + rng.NextInt(2));
                for (int i = 0; i < townOrder.Count; i++)
                    drawn.Add(i < townKeys ? new EdgeLock(townOrder[i], LockKind.Key) : Item(townOrder[i]));
                foreach (CubeEdge e in rest)
                    drawn.Add(rng.NextInt(3) == 0 ? new EdgeLock(e, LockKind.Key) : Item(e));
                var candidate = new EdgeLocks(model.Seed, Ordered(drawn, edges), attempt);
                report = Check(world, candidate, true);
                if (report.Passed) return candidate;
                lastProblem = report.Problems.Count > 0 ? report.Problems[0] : null;
            }

            // Fallback: Town's edges key-locked, the rest open (never seen on seeds 1-50; the sweep reports it).
            var fallback = new List<EdgeLock>();
            foreach (CubeEdge e in edges)
                fallback.Add(new EdgeLock(e, e.Touches(CubeModel.StartFace) ? LockKind.Key : LockKind.Open));
            var result = new EdgeLocks(model.Seed, fallback, MaxAttempts + 1);
            report = Check(world, result);
            if (lastProblem != null) report.Problems.Add("the last draw: " + lastProblem);
            return result;
        }

        private static IEnumerable<EdgeLock> Ordered(List<EdgeLock> drawn, List<CubeEdge> order) =>
            order.Select(e => drawn.First(l => l.Edge == e));

        /// <summary>
        /// Checks locks against a run's content, over every order the player could spend its keys in (LockWorld.Explore):
        /// from every state the player can get into, everything (every screen and island, every item, every required reward:
        /// fragments, keys, the items' alcoves; every core portal stands on a screen) can still be reached; and at least
        /// MinFirstEdges edges can be the first one opened. Problems name the soft-lock found.
        /// </summary>
        public static LockReport Check(LockWorld world, EdgeLocks locks, bool stopAtFirstProblem = false)
        {
            var report = new LockReport();
            world.Explore(locks, report, stopAtFirstProblem);
            if (locks.FellBack) report.Problems.Add("the locks fell back to Town's edges only (no draw passed)");
            return report;
        }

        /// <summary>Compact text of every lock, for the determinism fixture: "Front-Right=K Front-Top=I:Math ...".</summary>
        public string Signature()
        {
            var sb = new StringBuilder();
            foreach (EdgeLock l in Locks)
            {
                sb.Append(l.Edge).Append('=');
                switch (l.Kind)
                {
                    case LockKind.Key: sb.Append('K'); break;
                    case LockKind.Item: sb.Append("I:").Append(l.Item); break;
                    default: sb.Append('O'); break;
                }
                sb.Append(' ');
            }
            sb.Append("draw ").Append(Attempts);
            return sb.ToString();
        }

        /// <summary>The run's locks from its seed alone (layout, items, hidden currency and rewards worked out as the game does).</summary>
        public static EdgeLocks ForRun(CubeModel model, ModuleLibrary library, IEnumerable<Theme> itemThemes,
            IReadOnlyDictionary<Theme, int> variantCounts)
        {
            if (model == null || library == null) return null;
            CubeLayout layout = CubeLayout.Generate(model, library);
            ItemPlacement placement = ItemPlacement.ForRun(model, layout, library, itemThemes, variantCounts);
            HiddenCurrencyPlacement hidden = HiddenCurrencyPlacement.Generate(model, layout, library);
            DieNumbers dice = DieNumbers.Roll(model);
            IcePlan ice = IcePlan.Create(model, layout, dice);
            List<PuzzleGoal> puzzles = PuzzleScreens.Goals(ice);
            RewardPlacement rewards = RewardPlacement.Generate(model, layout, placement, hidden, library, puzzles, dice);
            MathPlan math = MathPlan.Create(model, placement, SeedSweep.ModuleLookup(layout, library));
            return Generate(new LockWorld(model, layout, placement, rewards, PuzzleScreens.RegionsAt(math, ice), puzzles));
        }
    }

    /// <summary>
    /// A run's content as the lock check sees it (4.9, pure): the places (DoorwayReach: screens, and islands on Math's island
    /// screens, the regions of Earth's ice screens, 4.10), the item pickups, the keys and every required reward (a puzzle
    /// goal's in its goal region: puzzles), each with the item gate (if any) in front of it.
    /// Explore walks every state the player can get into by spending keys in any order, collecting everything it can reach
    /// at each (more never hurts, and item locks open for free).
    /// </summary>
    public sealed class LockWorld
    {
        private readonly struct Thing
        {
            public readonly (ScreenAddress, int) Place;
            public readonly bool Guarded;
            public readonly Theme Guard;
            public readonly Theme Item;
            public readonly string Name;

            public Thing((ScreenAddress, int) place, bool guarded, Theme guard, Theme item, string name)
            {
                Place = place;
                Guarded = guarded;
                Guard = guard;
                Item = item;
                Name = name;
            }
        }

        private readonly Func<ScreenAddress, IScreenRegions> islandAt;
        private readonly List<Thing> pickups = new List<Thing>();
        private readonly List<Thing> keys = new List<Thing>();
        private readonly List<Thing> required = new List<Thing>();
        private readonly HashSet<(ScreenAddress, int)> targets;
        private readonly Dictionary<CubeEdge, List<(ScreenAddress, int)>> doorPlaces = new Dictionary<CubeEdge, List<(ScreenAddress, int)>>();

        public CubeModel Model { get; }

        /// <summary>The run's items (their home themes, in theme order): what item locks may ask for.</summary>
        public List<Theme> Items { get; }

        public LockWorld(CubeModel model, CubeLayout layout, ItemPlacement items, RewardPlacement rewards,
            Func<ScreenAddress, IScreenRegions> islandAt = null, IReadOnlyList<PuzzleGoal> puzzles = null)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            this.islandAt = islandAt;
            var reach = new DoorwayReach(model, islandAt);
            (ScreenAddress, int) PlaceOf(ScreenAddress s, Vector2 local) => (s, reach.RegionAt(s, local));

            Items = items != null ? items.Pickups.Select(p => p.Item).Distinct().OrderBy(t => t).ToList() : new List<Theme>();
            if (items != null)
                foreach (PickupPlacement p in items.Pickups)
                {
                    GatePlacement guard = default;
                    bool guarded = p.IsGuarded && items.TryGetGate(p.Screen, p.GuardSlot, out guard);
                    pickups.Add(new Thing(PlaceOf(p.Screen, MathPlan.PickupLocal(p)), guarded, guard.Item, p.Item, $"the {p.Item} item"));
                }
            if (rewards != null)
                foreach (RewardSpot s in rewards.Spots)
                {
                    if (!s.Required) continue;
                    bool guarded = s.Where.Kind == RoadblockKind.Gate && items != null &&
                                   items.TryGetGate(s.Where.Screen, s.Where.Slot, out _);
                    GatePlacement g = default;
                    if (guarded) items.TryGetGate(s.Where.Screen, s.Where.Slot, out g);
                    var thing = new Thing(PlaceOf(s.Where.Screen, RewardPlacement.LocalOf(s.Where, puzzles)), guarded, g.Item, Theme.Town, s.ToString());
                    required.Add(thing);
                    if (s.What.Kind == RewardKind.Key) keys.Add(thing);
                }
            // Everything the unlocked cube reaches, with the gap-crosser* (the sweep proves that is the whole cube).
            targets = new HashSet<(ScreenAddress, int)>(reach.Reachable(true));
            // Core portals stand on screens: the targets hold every screen. The layout is only for them.
            if (layout != null)
                foreach (FaceLayout fl in layout.Faces.Values)
                    targets.Add((new ScreenAddress(fl.Face, fl.CoreCell), reach.RegionAt(new ScreenAddress(fl.Face, fl.CoreCell), Vector2.zero)));

            int n = model.FaceSize;
            foreach (CubeEdge e in CubeEdge.All)
            {
                var list = new List<(ScreenAddress, int)>();
                foreach (FaceId face in new[] { e.A, e.B })
                {
                    if (model.IsSealed(face)) continue;
                    Facing side = e.SideOn(face);
                    for (int i = 0; i < n; i++)
                    {
                        Vector2Int cell = side == Facing.East ? new Vector2Int(n - 1, i)
                            : side == Facing.West ? new Vector2Int(0, i)
                            : side == Facing.North ? new Vector2Int(i, n - 1) : new Vector2Int(i, 0);
                        var screen = new ScreenAddress(face, cell);
                        int region = reach.RegionOfDoor(screen, side);
                        if (region >= 0) list.Add((screen, region));
                    }
                }
                doorPlaces[e] = list;
            }
        }

        /// <summary>What the player has in one state: where it can walk, the items held, the keys found and the edges open.</summary>
        public sealed class State
        {
            public HashSet<(ScreenAddress Screen, int Region)> Places;
            public HashSet<Theme> Held = new HashSet<Theme>();
            public HashSet<CubeEdge> Open = new HashSet<CubeEdge>();
            public int KeysFound;
        }

        private bool Got(Thing t, State s) => s.Places.Contains(t.Place) && (!t.Guarded || s.Held.Contains(t.Guard));

        /// <summary>True when the player can walk up to one of an edge's arches.</summary>
        public bool AtEdge(CubeEdge edge, State s)
        {
            foreach ((ScreenAddress, int) p in doorPlaces[edge])
                if (s.Places.Contains(p)) return true;
            return false;
        }

        /// <summary>
        /// The state after opening the given key locks (whatever the order): every item lock whose item is held and whose arch
        /// can be walked to opens, and everything reachable is collected, until nothing changes.
        /// </summary>
        public State Close(EdgeLocks locks, IEnumerable<CubeEdge> keyOpened)
        {
            var s = new State();
            foreach (CubeEdge e in keyOpened) s.Open.Add(e);
            foreach (EdgeLock l in locks.Locks)
                if (l.Kind == LockKind.Open) s.Open.Add(l.Edge);
            int n = Model.FaceSize;
            bool DoorOpen(ScreenAddress screen, Facing side) =>
                !ScreenEdges.IsFaceEdge(n, screen.Cell, side) || locks.LockOf(screen.Face, side).Kind == LockKind.Open ||
                s.Open.Contains(CubeEdge.Of(screen.Face, side));
            while (true)
            {
                s.Places = new DoorwayReach(Model, islandAt, DoorOpen).Reachable(s.Held.Contains(Theme.Math));
                bool changed = false;
                foreach (Thing p in pickups)
                    if (!s.Held.Contains(p.Item) && Got(p, s))
                    {
                        s.Held.Add(p.Item);
                        changed = true;
                    }
                foreach (EdgeLock l in locks.Locks)
                    if (l.Kind == LockKind.Item && !s.Open.Contains(l.Edge) && s.Held.Contains(l.Item) && AtEdge(l.Edge, s))
                    {
                        s.Open.Add(l.Edge);
                        changed = true;
                    }
                if (!changed) break;
            }
            s.KeysFound = keys.Count(k => Got(k, s));
            return s;
        }

        /// <summary>True when everything is reached: every place, every item and every required reward.</summary>
        public bool Complete(State s) => Missing(s) == null;

        /// <summary>The first thing a state cannot reach, or null when it reaches everything.</summary>
        public string Missing(State s)
        {
            foreach ((ScreenAddress screen, int region) in targets)
                if (!s.Places.Contains((screen, region))) return region == 0 ? $"{screen}" : $"island {region} of {screen}";
            foreach (Thing p in pickups)
                if (!s.Held.Contains(p.Item)) return p.Name;
            foreach (Thing r in required)
                if (!Got(r, s)) return r.Name;
            return null;
        }

        /// <summary>
        /// Every state the player can get into, spending its keys in any order on any key lock whose arch it can walk to (an
        /// arch between two places it already reaches too: a wasted key); fills the report with every state that can no longer
        /// finish (a soft-lock) and the possible first edges.
        /// </summary>
        public void Explore(EdgeLocks locks, LockReport report, bool stopAtFirstProblem = false)
        {
            List<CubeEdge> keyEdges = locks.Locks.Where(l => l.Kind == LockKind.Key).Select(l => l.Edge).ToList();
            var states = new Dictionary<int, State>();
            var finish = new Dictionary<int, bool>();
            State Get(int mask)
            {
                if (states.TryGetValue(mask, out State st)) return st;
                var opened = new List<CubeEdge>();
                for (int i = 0; i < keyEdges.Count; i++)
                    if ((mask & (1 << i)) != 0) opened.Add(keyEdges[i]);
                return states[mask] = Close(locks, opened);
            }
            IEnumerable<int> Moves(int mask)
            {
                State st = Get(mask);
                if (Complete(st)) yield break;
                int spent = 0;
                for (int i = 0; i < keyEdges.Count; i++) if ((mask & (1 << i)) != 0) spent++;
                if (EdgeLocks.StartKeys + st.KeysFound - spent <= 0) yield break;
                for (int i = 0; i < keyEdges.Count; i++)
                    if ((mask & (1 << i)) == 0 && AtEdge(keyEdges[i], st)) yield return mask | (1 << i);
            }
            bool CanFinish(int mask)
            {
                if (finish.TryGetValue(mask, out bool done)) return done;
                bool ok = Complete(Get(mask));
                if (!ok)
                    foreach (int next in Moves(mask))
                        if (CanFinish(next))
                        {
                            ok = true;
                            break;
                        }
                return finish[mask] = ok;
            }

            // Walk every reachable state.
            var seen = new HashSet<int> { 0 };
            var queue = new Queue<int>();
            queue.Enqueue(0);
            int softLocks = 0;
            while (queue.Count > 0)
            {
                int mask = queue.Dequeue();
                if (!CanFinish(mask))
                {
                    if (softLocks++ < 3)
                    {
                        State st = Get(mask);
                        string opened = string.Join(", ", keyEdges.Where((e, i) => (mask & (1 << i)) != 0));
                        report.Problems.Add($"soft-lock: with keys spent on [{opened}] ({st.KeysFound} found) " +
                                            $"{Missing(st)} can never be reached");
                    }
                    if (stopAtFirstProblem) break; // a draw being tried: one soft-lock rejects it
                    continue;
                }
                foreach (int next in Moves(mask))
                    if (seen.Add(next)) queue.Enqueue(next);
            }
            if (softLocks > 3) report.Problems.Add($"... {softLocks} soft-locked states in all");
            report.States = seen.Count;

            // The first edge opened: an item lock open from the start (none: Town holds no item), or a key lock from Town.
            State start = Get(0);
            foreach (CubeEdge e in start.Open)
                if (locks.LockOf(e).Kind == LockKind.Item) report.FirstEdges.Add(e);
            foreach (int next in Moves(0))
                for (int i = 0; i < keyEdges.Count; i++)
                    if ((next & (1 << i)) != 0) report.FirstEdges.Add(keyEdges[i]);
            if (report.FirstEdges.Count < EdgeLocks.MinFirstEdges && !Complete(start))
                report.Problems.Add($"only {report.FirstEdges.Count} edge(s) can be opened first: {string.Join(", ", report.FirstEdges)}");
        }
    }
}
