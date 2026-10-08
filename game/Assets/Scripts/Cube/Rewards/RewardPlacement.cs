using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>What a roadblock hides (4.7).</summary>
    public enum RewardKind
    {
        /// <summary>Meta currency (Jumbles*), banked at the run's end like trial pay.</summary>
        Jumbles = 0,

        /// <summary>A face item (ItemPlacement's pickup of that theme).</summary>
        Equipment = 1,

        /// <summary>A small boost for this run only (RunUpgrade).</summary>
        Upgrade = 2,

        /// <summary>A key: held for the run (4.9's locked edges use it up); an unused one is cashed in at the run's end.</summary>
        Key = 3,

        /// <summary>
        /// A numbered fragment (4.8): on the face numbered k (DieNumbers), fragments 1..k fill its first k fragment slots; the
        /// slots above k hold plain Jumbles* chests instead.
        /// </summary>
        FragmentSlot = 4,

        /// <summary>
        /// A chest key (4.9): opens one optional gate's bonus alcove (a key-locked bonus alcove, AlcoveLock), never a face-edge
        /// arch. A pool of its own, so spending them never starves the arches' keys; an unused one is cashed in at the run's end.
        /// </summary>
        ChestKey = 5,
    }

    /// <summary>The in-run boosts a roadblock can hide. Each is one PlayerStats point for the rest of the run.</summary>
    public enum RunUpgrade { Heart = 0, Speed = 1, Power = 2 }

    /// <summary>What guards a reward.</summary>
    public enum RoadblockKind
    {
        /// <summary>A required item gate: the reward stands in the alcove behind it.</summary>
        Gate = 0,

        /// <summary>A non-rolled variant's optional gate: the reward stands in its alcove (a bonus, never required).</summary>
        OptionalGate = 1,

        /// <summary>A hidden spot: a heap of dirt any swing digs away (RewardMound), over a hidden-item slot or the lane spot.</summary>
        Mound = 2,

        /// <summary>A puzzle screen's goal (4.10-4.12 pass them in): the reward stands behind the puzzle's roadblock.</summary>
        Puzzle = 3,
    }

    /// <summary>One roadblock: a gate slot (Gate, OptionalGate), a hidden-item slot or the lane spot (Mound), or a puzzle goal.</summary>
    public readonly struct Roadblock : IEquatable<Roadblock>
    {
        /// <summary>A mound's Slot for the lane spot (CubeWorld.OpenPickupOffset) instead of a hidden-item slot.</summary>
        public const int LaneSpot = -1;

        public readonly RoadblockKind Kind;
        public readonly ScreenAddress Screen;

        /// <summary>Gate slot (Gate, OptionalGate), hidden-item slot or LaneSpot (Mound), puzzle goal index (Puzzle).</summary>
        public readonly int Slot;

        public Roadblock(RoadblockKind kind, ScreenAddress screen, int slot)
        {
            Kind = kind;
            Screen = screen;
            Slot = slot;
        }

        public bool IsLaneSpot => Kind == RoadblockKind.Mound && Slot == LaneSpot;

        public bool Equals(Roadblock o) => Kind == o.Kind && Screen == o.Screen && Slot == o.Slot;
        public override bool Equals(object obj) => obj is Roadblock o && Equals(o);
        public override int GetHashCode() => ((int)Kind * 31 + Screen.GetHashCode()) * 31 + Slot;
        public override string ToString() => $"{Kind} at {Screen} #{(IsLaneSpot ? "lane" : Slot.ToString())}";
    }

    /// <summary>A reward: its kind and its payload (an amount, an item theme, a boost or a fragment ordinal).</summary>
    public readonly struct Reward : IEquatable<Reward>
    {
        public readonly RewardKind Kind;

        /// <summary>Jumbles*: the amount (0 for every other kind).</summary>
        public readonly int Amount;

        /// <summary>Equipment: the item's home theme.</summary>
        public readonly Theme Item;

        /// <summary>Upgrade: the boost.</summary>
        public readonly RunUpgrade Upgrade;

        /// <summary>FragmentSlot: the fragment's number on its face, 1..k on the face numbered k (shown as die pips).</summary>
        public readonly int Ordinal;

        private Reward(RewardKind kind, int amount = 0, Theme item = Theme.Town, RunUpgrade upgrade = RunUpgrade.Heart, int ordinal = 0)
        {
            Kind = kind;
            Amount = amount;
            Item = item;
            Upgrade = upgrade;
            Ordinal = ordinal;
        }

        public static Reward Jumbles(int amount) => new Reward(RewardKind.Jumbles, amount);
        public static Reward Equipment(Theme item) => new Reward(RewardKind.Equipment, item: item);
        public static Reward Boost(RunUpgrade upgrade) => new Reward(RewardKind.Upgrade, upgrade: upgrade);
        public static Reward Key() => new Reward(RewardKind.Key);
        public static Reward Fragment(int ordinal) => new Reward(RewardKind.FragmentSlot, ordinal: ordinal);
        public static Reward ChestKey() => new Reward(RewardKind.ChestKey);

        public bool Equals(Reward o) => Kind == o.Kind && Amount == o.Amount && Item == o.Item && Upgrade == o.Upgrade && Ordinal == o.Ordinal;
        public override bool Equals(object obj) => obj is Reward o && Equals(o);
        public override int GetHashCode() => (((int)Kind * 31 + Amount) * 31 + (int)Item) * 31 + (int)Upgrade * 7 + Ordinal;

        public override string ToString()
        {
            switch (Kind)
            {
                case RewardKind.Jumbles: return $"{Amount} Jumbles";
                case RewardKind.Equipment: return $"{Item} item";
                case RewardKind.Upgrade: return $"{Upgrade} boost";
                case RewardKind.Key: return "key";
                case RewardKind.ChestKey: return "chest key";
                default: return $"fragment {Ordinal}";
            }
        }
    }

    /// <summary>One reward behind one roadblock.</summary>
    public readonly struct RewardSpot
    {
        /// <summary>Its index in RewardPlacement.Spots (a stable id within the run).</summary>
        public readonly int Index;
        public readonly Roadblock Where;
        public readonly Reward What;

        /// <summary>True when the run can always reach it (the sweep proves it); false for the optional gates' bonuses.</summary>
        public readonly bool Required;

        /// <summary>The hidden meta-currency spot it stands for (index into HiddenCurrencyPlacement.Spots), or -1.</summary>
        public readonly int HiddenIndex;

        public RewardSpot(int index, Roadblock where, Reward what, bool required, int hiddenIndex = -1)
        {
            Index = index;
            Where = where;
            What = what;
            Required = required;
            HiddenIndex = hiddenIndex;
        }

        public FaceId Face => Where.Screen.Face;

        public override string ToString() => $"{What} behind {Where}{(Required ? "" : " (bonus)")}";
    }

    /// <summary>A puzzle screen's goal (4.10-4.12): a point (local to its screen's centre) where its reward roadblock stands.</summary>
    public readonly struct PuzzleGoal
    {
        public readonly ScreenAddress Screen;
        public readonly Vector2 Local;

        public PuzzleGoal(ScreenAddress screen, Vector2 local)
        {
            Screen = screen;
            Local = local;
        }
    }

    /// <summary>
    /// Pure, deterministic roadblock rewards (4.7): every roadblock of the run hides one reward, and nothing lies loose on
    /// the ground. On its own RNG stream (RngStreams.Rewards, one sub-stream per face: FaceStream), after ItemPlacement and HiddenCurrencyPlacement, whose draws
    /// it never changes:
    /// <list type="bullet">
    /// <item>Equipment: each face item where ItemPlacement put it: in a gated alcove, or (an "open" pickup) under a mound on
    /// the lane spot, so an item needs a dig, never a stroll.</item>
    /// <item>The hidden meta currency: a mound over each of its hidden-item slots, with its Jumbles*.</item>
    /// <item>Optional gates: a bonus chest of OptionalJumbles each, never required (their variant is not this run's).</item>
    /// <item>Every face's pool: its other required gates' alcoves, its puzzle goals, a filler mound on each of its
    /// screens with no roadblock yet (so every screen holds something), and extra mounds on free hidden-item slots
    /// while the pool is under ReservedPerFace. The pool is shuffled; its first FragmentSlotsPerFace spots are the face's
    /// fragment slots 1..6: on the face numbered k (DieNumbers, 4.8) slots 1..k hold fragments 1..k and the slots above k
    /// chest keys (4.9: 6 - k a face, ChestKeysPerRun a run). Then KeysPerFace arch keys (on the first unguarded spots),
    /// UpgradesPerFace boosts, and Jumbles* chests (ChestJumbles) for the rest. (The die numbers decide only what the reserved
    /// slots hold, so every draw is as in 4.7.)</item>
    /// </list>
    /// The sweep (FindProblems) proves every required reward reachable, no reward outside a roadblock and no empty screen.
    /// </summary>
    public sealed class RewardPlacement
    {
        /// <summary>PCG32 stream for the rewards (see RngStreams).</summary>
        public const ulong RngStream = RngStreams.Rewards;

        /// <summary>
        /// Each face draws on its own sub-stream of RngStream (review patch): a change to one face's pool (a puzzle goal added
        /// by 4.10-4.12) never reshuffles another face's rewards. The sub-streams sit far above the registry's values.
        /// </summary>
        public static ulong FaceStream(FaceId face) => (RngStream << 8) + 1 + (ulong)face;

        /// <summary>Fragment slots reserved on every science face: the most any face needs (4.8's die numbers run 2-6).</summary>
        public const int FragmentSlotsPerFace = 6;

        /// <summary>
        /// Arch keys hidden on every science face (4.9 tuned it from 1 with its locks): each opens one key-locked face-edge arch
        /// (EdgeLocks). They go to the face's first unguarded spots (a mound or a puzzle, never behind an item gate), so a face
        /// entered with a key always pays its keys back.
        /// </summary>
        public const int KeysPerFace = 2;

        /// <summary>In-run boosts hidden on every science face.</summary>
        public const int UpgradesPerFace = 1;

        /// <summary>
        /// Chest keys a run hides (4.9): each opens one key-locked bonus alcove (an optional gate). They fill the fragment slots
        /// above each face's die number (6 - k on the face numbered k), so the run always has 30 - 20 = 10, two a face on
        /// average, more on the faces with fewer fragments.
        /// </summary>
        public const int ChestKeysPerRun = FragmentSlotsPerFace * 5 - (DieNumbers.FragmentsPerRun - DieNumbers.TownNumber);

        /// <summary>The pool a face needs: its fragment slots (fragments and chest keys), keys and boosts.</summary>
        public const int ReservedPerFace = FragmentSlotsPerFace + KeysPerFace + UpgradesPerFace;

        /// <summary>Jumbles* in a chest from a face's pool (and in a fragment slot above the face's die number).</summary>
        public const int ChestJumbles = 5;

        /// <summary>Jumbles* in an optional gate's bonus chest.</summary>
        public const int OptionalJumbles = 10;

        /// <summary>Jumbles* each found key (arch or chest key) still held at the run's end is cashed in for.</summary>
        public const int KeyCashIn = 5;

        private readonly List<RewardSpot> spots;

        public int Seed { get; }
        public IReadOnlyList<RewardSpot> Spots => spots;

        public RewardPlacement(int seed, IEnumerable<RewardSpot> spots)
        {
            Seed = seed;
            this.spots = new List<RewardSpot>(spots ?? Array.Empty<RewardSpot>());
        }

        /// <summary>A face's fragments in number order (1..k on the face numbered k).</summary>
        public List<RewardSpot> FragmentSlots(FaceId face)
        {
            var list = new List<RewardSpot>();
            foreach (RewardSpot s in spots)
                if (s.Face == face && s.What.Kind == RewardKind.FragmentSlot) list.Add(s);
            list.Sort((a, b) => a.What.Ordinal.CompareTo(b.What.Ordinal));
            return list;
        }

        /// <summary>The reward behind a roadblock, if any.</summary>
        public bool TryGet(Roadblock where, out RewardSpot spot)
        {
            foreach (RewardSpot s in spots)
            {
                if (!s.Where.Equals(where)) continue;
                spot = s;
                return true;
            }
            spot = default;
            return false;
        }

        /// <summary>How many rewards of a kind the run hides (all, or only the required ones).</summary>
        public int Count(RewardKind kind, bool requiredOnly = false)
        {
            int n = 0;
            foreach (RewardSpot s in spots)
                if (s.What.Kind == kind && (!requiredOnly || s.Required)) n++;
            return n;
        }

        /// <summary>The roadblock that guards an item's pickup: its gated alcove, or the mound on its screen's lane spot.</summary>
        public static Roadblock RoadblockOf(PickupPlacement pickup) => pickup.IsGuarded
            ? new Roadblock(RoadblockKind.Gate, pickup.Screen, pickup.GuardSlot)
            : new Roadblock(RoadblockKind.Mound, pickup.Screen, Roadblock.LaneSpot);

        /// <summary>The run's rewards, as the game (CubeWorld) and the sweep both make them.</summary>
        /// <remarks>dice defaults to the run's own (DieNumbers.Roll(model)).</remarks>
        public static RewardPlacement Generate(CubeModel model, CubeLayout layout, ItemPlacement items, HiddenCurrencyPlacement hidden,
            IHiddenSlotCatalog catalog, IReadOnlyList<PuzzleGoal> puzzles = null, DieNumbers dice = null)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            dice = dice ?? DieNumbers.Roll(model);

            var rngs = new Dictionary<FaceId, SeededRng>();
            SeededRng Rng(FaceId face)
            {
                if (!rngs.TryGetValue(face, out SeededRng r)) rngs[face] = r = new SeededRng(unchecked((ulong)(uint)model.Seed), FaceStream(face));
                return r;
            }
            var result = new List<RewardSpot>();
            var taken = new HashSet<Roadblock>();
            var occupied = new HashSet<ScreenAddress>();
            var usedHidden = new HashSet<(ScreenAddress, int)>();

            void Add(Roadblock where, Reward what, bool required, int hiddenIndex = -1)
            {
                result.Add(new RewardSpot(result.Count, where, what, required, hiddenIndex));
                taken.Add(where);
                occupied.Add(where.Screen);
                if (where.Kind == RoadblockKind.Mound && where.Slot >= 0) usedHidden.Add((where.Screen, where.Slot));
            }

            // 1. Equipment, where ItemPlacement put each item (theme order).
            foreach (PickupPlacement p in items.Pickups) Add(RoadblockOf(p), Reward.Equipment(p.Item), true);

            // 2. The hidden meta currency: a mound over each of its slots.
            if (hidden != null)
                for (int i = 0; i < hidden.Spots.Count; i++)
                {
                    HiddenCurrencySpot h = hidden.Spots[i];
                    var where = new Roadblock(RoadblockKind.Mound, h.Screen, h.Slot);
                    if (taken.Contains(where)) continue;
                    Add(where, Reward.Jumbles(HiddenCurrencyPlacement.Amount), true, i);
                }

            // 3. Optional gates: a bonus chest each.
            foreach (OptionalGatePlacement o in items.OptionalGates)
                Add(new Roadblock(RoadblockKind.OptionalGate, o.Screen, o.Slot), Reward.Jumbles(OptionalJumbles), false);

            // 4. Every face's pool, face by face.
            var pools = new Dictionary<FaceId, List<Roadblock>>();
            for (int f = 0; f < CubeSettings.FaceCount; f++)
            {
                var face = (FaceId)f;
                if (!layout.TryGetFace(face, out FaceLayout fl)) continue;
                int n = fl.FaceSize;
                var pool = new List<Roadblock>();
                foreach (GatePlacement g in items.Gates)
                {
                    var where = new Roadblock(RoadblockKind.Gate, g.Screen, g.Slot);
                    if (g.Screen.Face == face && !taken.Contains(where)) pool.Add(where);
                }
                if (puzzles != null)
                    for (int i = 0; i < puzzles.Count; i++)
                        if (puzzles[i].Screen.Face == face) pool.Add(new Roadblock(RoadblockKind.Puzzle, puzzles[i].Screen, i));
                foreach (Roadblock r in pool) occupied.Add(r.Screen);

                // Filler: a mound on every screen that holds no roadblock yet (no draw).
                var free = new List<(ScreenAddress Screen, int Slot)>();
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        var screen = new ScreenAddress(face, x, y);
                        if (TownSquare.IsTown(screen, n) || IsIsland(fl, screen.Cell)) continue;
                        int slots = catalog.HiddenSlotCount(fl.Theme, fl.ModuleAt(screen.Cell));
                        for (int s = 0; s < slots; s++)
                        {
                            if (usedHidden.Contains((screen, s))) continue;
                            if (!occupied.Contains(screen))
                            {
                                var mound = new Roadblock(RoadblockKind.Mound, screen, s);
                                pool.Add(mound);
                                occupied.Add(screen);
                                usedHidden.Add((screen, s));
                            }
                            else free.Add((screen, s));
                        }
                    }
                }

                // Capacity: extra mounds on free hidden-item slots while the pool is short.
                for (int i = 0; pool.Count < ReservedPerFace && i < free.Count; i++)
                {
                    int j = i + Rng(face).NextInt(free.Count - i);
                    (free[i], free[j]) = (free[j], free[i]);
                    pool.Add(new Roadblock(RoadblockKind.Mound, free[i].Screen, free[i].Slot));
                    usedHidden.Add((free[i].Screen, free[i].Slot));
                }
                pools[face] = pool;
            }


            for (int f = 0; f < CubeSettings.FaceCount; f++)
            {
                if (!pools.TryGetValue((FaceId)f, out List<Roadblock> pool)) continue;
                SeededRng rng = Rng((FaceId)f);
                rng.Shuffle(pool);
                int k = 0, number = dice.Of((FaceId)f);
                for (int ordinal = 1; ordinal <= FragmentSlotsPerFace && k < pool.Count; ordinal++)
                    Add(pool[k++], ordinal <= number ? Reward.Fragment(ordinal) : Reward.ChestKey(), true);
                void AddKey()
                {
                    // 4.9: a key never waits behind an item gate (no draw: the first unguarded spot left moves up).
                    int j = pool.FindIndex(k, r => r.Kind != RoadblockKind.Gate);
                    if (j > k) (pool[k], pool[j]) = (pool[j], pool[k]);
                    Add(pool[k++], Reward.Key(), true);
                }
                // The first key, the boost, then the other keys (a face short of room, Math with its island screens, drops a key).
                if (k < pool.Count) AddKey();
                for (int i = 0; i < UpgradesPerFace && k < pool.Count; i++)
                    Add(pool[k++], Reward.Boost((RunUpgrade)rng.NextInt(3)), true);
                for (int i = 1; i < KeysPerFace && k < pool.Count; i++) AddKey();
                while (k < pool.Count) Add(pool[k++], Reward.Jumbles(ChestJumbles), true);
            }
            return new RewardPlacement(model.Seed, result);
        }

        /// <summary>True for a whole-screen puzzle's cell (islands, ice, 4.11 wind): its only roadblock is its goal.</summary>
        private static bool IsIsland(FaceLayout fl, Vector2Int cell) => fl.IsPuzzleCell(cell);

        /// <summary>Where a roadblock's reward stands on its screen (local), for the doorway-by-doorway reach check.</summary>
        public static Vector2 LocalOf(Roadblock where, IReadOnlyList<PuzzleGoal> puzzles = null)
        {
            if (where.IsLaneSpot) return CubeWorld.OpenPickupOffset;
            if (where.Kind == RoadblockKind.Puzzle && puzzles != null && where.Slot >= 0 && where.Slot < puzzles.Count)
                return puzzles[where.Slot].Local;
            return Vector2.zero; // a slot on an ordinary module: all of its screen is one place
        }

        /// <summary>
        /// Problems with a run's rewards (empty when all is well):
        /// <list type="bullet">
        /// <item>every reward stands behind a real roadblock (a gate or optional gate in that slot, a hidden-item slot, the
        /// lane spot of a non-town screen, a puzzle goal), one reward per roadblock;</item>
        /// <item>every gate and optional gate hides one, every item pickup and hidden currency spot is a reward (no loose
        /// pickups), and optional gates hide only bonus Jumbles*;</item>
        /// <item>every science face numbered k (dice, by default the run's) holds exactly fragments 1..k (4.8), required,
        /// with its keys and boosts;</item>
        /// <item>every non-town screen of a laid-out face holds a roadblock or is an island screen (a puzzle);</item>
        /// <item>every required reward is reachable: collecting items doorway by doorway (DoorwayReach over Math's island
        /// screens), its place is reachable and its gate's item is collectable.</item>
        /// </list>
        /// </summary>
        public static List<string> FindProblems(CubeModel model, CubeLayout layout, ItemPlacement items, HiddenCurrencyPlacement hidden,
            RewardPlacement rewards, IHiddenSlotCatalog catalog, Func<ScreenAddress, IScreenRegions> islandAt = null,
            IReadOnlyList<PuzzleGoal> puzzles = null, DieNumbers dice = null)
        {
            var problems = new List<string>();
            int n = model.FaceSize;
            dice = dice ?? DieNumbers.Roll(model);
            // The die numbers (4.8): Town 1, the other faces 2-6 once each.
            if (dice.Of(CubeModel.StartFace) != DieNumbers.TownNumber) problems.Add($"Town is numbered {dice.Of(CubeModel.StartFace)}, not 1");
            var numbers = new HashSet<int>();
            for (int f = 0; f < CubeSettings.FaceCount; f++) numbers.Add(dice.Of((FaceId)f));
            if (numbers.Count != CubeSettings.FaceCount || numbers.Min() != 1 || numbers.Max() != DieNumbers.Highest)
                problems.Add($"the die numbers are not 1-6 once each: {dice}");
            var seen = new HashSet<Roadblock>();
            foreach (RewardSpot s in rewards.Spots)
            {
                Roadblock w = s.Where;
                if (!seen.Add(w)) problems.Add($"{w} hides two rewards");
                if (!layout.TryGetFace(w.Screen.Face, out FaceLayout fl))
                {
                    problems.Add($"{s} is not on a built science face");
                    continue;
                }
                if (TownSquare.IsTown(w.Screen, n)) problems.Add($"{s} is in a town");
                switch (w.Kind)
                {
                    case RoadblockKind.Gate:
                        if (!items.TryGetGate(w.Screen, w.Slot, out _)) problems.Add($"{s}: no gate stands there (a reward outside a roadblock)");
                        break;
                    case RoadblockKind.OptionalGate:
                        if (!items.TryGetOptionalGate(w.Screen, w.Slot, out _)) problems.Add($"{s}: no optional gate stands there");
                        if (s.Required || s.What.Kind != RewardKind.Jumbles) problems.Add($"{s}: an optional gate may only hide bonus Jumbles");
                        break;
                    case RoadblockKind.Mound:
                        if (w.Slot != Roadblock.LaneSpot)
                        {
                            int slots = catalog.HiddenSlotCount(fl.Theme, fl.ModuleAt(w.Screen.Cell));
                            if (w.Slot < 0 || w.Slot >= slots) problems.Add($"{s}: the module has {slots} hidden-item slots");
                        }
                        break;
                    case RoadblockKind.Puzzle:
                        if (puzzles == null || w.Slot < 0 || w.Slot >= puzzles.Count || puzzles[w.Slot].Screen != w.Screen)
                            problems.Add($"{s}: no such puzzle goal");
                        break;
                }
            }

            foreach (GatePlacement g in items.Gates)
                if (!rewards.TryGet(new Roadblock(RoadblockKind.Gate, g.Screen, g.Slot), out _)) problems.Add($"{g} hides nothing");
            foreach (OptionalGatePlacement g in items.OptionalGates)
                if (!rewards.TryGet(new Roadblock(RoadblockKind.OptionalGate, g.Screen, g.Slot), out _)) problems.Add($"{g} hides nothing");
            foreach (PickupPlacement p in items.Pickups)
                if (!rewards.TryGet(RoadblockOf(p), out RewardSpot spot) || spot.What.Kind != RewardKind.Equipment || spot.What.Item != p.Item)
                    problems.Add($"{p} lies loose (no roadblock hides it)");
            if (hidden != null)
                for (int i = 0; i < hidden.Spots.Count; i++)
                {
                    HiddenCurrencySpot h = hidden.Spots[i];
                    if (!rewards.TryGet(new Roadblock(RoadblockKind.Mound, h.Screen, h.Slot), out RewardSpot spot) || spot.HiddenIndex != i)
                        problems.Add($"{h} has no mound over it");
                }

            // Every face's mix.
            for (int f = 0; f < CubeSettings.FaceCount; f++)
            {
                var face = (FaceId)f;
                if (!layout.TryGetFace(face, out FaceLayout fl)) continue;
                List<RewardSpot> fragments = rewards.FragmentSlots(face);
                if (n >= TownSquare.MinFaceSize)
                {
                    int number = dice.Of(face);
                    if (fragments.Count != number)
                        problems.Add($"{face} ({fl.Theme}) numbered {number} holds {fragments.Count} fragments");
                    for (int i = 0; i < fragments.Count; i++)
                        if (fragments[i].What.Ordinal != i + 1) problems.Add($"{face}: fragment {i + 1} is numbered {fragments[i].What.Ordinal}");
                    int keys = 0, boosts = 0;
                    foreach (RewardSpot s in rewards.Spots)
                    {
                        if (s.Face != face) continue;
                        if (s.What.Kind == RewardKind.Key) keys++;
                        if (s.What.Kind == RewardKind.Upgrade) boosts++;
                    }
                    if (keys < 1 || keys > KeysPerFace) problems.Add($"{face} ({fl.Theme}) hides {keys} keys (expected 1-{KeysPerFace})");
                    if (boosts != UpgradesPerFace) problems.Add($"{face} ({fl.Theme}) hides {boosts} boosts (expected {UpgradesPerFace})");
                }
                foreach (RewardSpot s in fragments)
                    if (!s.Required) problems.Add($"{s}: a fragment must be required");

                // Every screen holds something.
                var held = new HashSet<ScreenAddress>();
                foreach (RewardSpot s in rewards.Spots) held.Add(s.Where.Screen);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        var screen = new ScreenAddress(face, x, y);
                        if (TownSquare.IsTown(screen, n) || IsIsland(fl, screen.Cell)) continue;
                        if (!held.Contains(screen)) problems.Add($"{screen} ({fl.Theme}) is empty: no puzzle, roadblock or reward");
                    }
            }

            // 4.9: the run's chest keys fill the fragment slots above each face's number.
            if (n >= TownSquare.MinFaceSize)
            {
                int expected = 0, chests = rewards.Count(RewardKind.ChestKey);
                foreach (FaceId face in layout.Faces.Keys) expected += FragmentSlotsPerFace - dice.Of(face);
                if (chests != expected) problems.Add($"the run hides {chests} chest keys (expected {expected})");
            }

            // Reachability: collect the items doorway by doorway, then every required reward.
            var reach = new DoorwayReach(model, islandAt);
            HashSet<Theme> heldItems = SeedSweep.CollectableItems(model, items, islandAt);
            bool gapCrosser = heldItems.Contains(Theme.Math);
            foreach (RewardSpot s in rewards.Spots)
            {
                if (!s.Required) continue;
                if (!reach.CanReach(s.Where.Screen, LocalOf(s.Where, puzzles), gapCrosser))
                {
                    problems.Add($"{s} can never be reached");
                    continue;
                }
                if (s.Where.Kind == RoadblockKind.Gate && items.TryGetGate(s.Where.Screen, s.Where.Slot, out GatePlacement g) &&
                    !heldItems.Contains(g.Item))
                    problems.Add($"{s} sits behind a {g.Item} gate whose item can never be collected");
            }
            return problems;
        }

        /// <summary>Compact text of the whole placement, for comparisons and the determinism fixture.</summary>
        public string Signature()
        {
            var sb = new StringBuilder();
            foreach (RewardSpot s in spots)
            {
                Roadblock w = s.Where;
                sb.Append(w.Kind.ToString()[0]).Append('@').Append(w.Screen.Face).Append(w.Screen.Cell.x).Append(w.Screen.Cell.y)
                    .Append('#').Append(w.Slot).Append('=');
                switch (s.What.Kind)
                {
                    case RewardKind.Jumbles: sb.Append('J').Append(s.What.Amount); break;
                    case RewardKind.Equipment: sb.Append('E').Append(s.What.Item); break;
                    case RewardKind.Upgrade: sb.Append('U').Append(s.What.Upgrade); break;
                    case RewardKind.Key: sb.Append('K'); break;
                    case RewardKind.ChestKey: sb.Append('C'); break;
                    default: sb.Append('F').Append(s.What.Ordinal); break;
                }
                if (!s.Required) sb.Append('?');
                sb.Append(' ');
            }
            return sb.ToString();
        }
    }
}
