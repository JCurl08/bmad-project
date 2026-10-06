using System;
using System.Collections.Generic;
using System.Text;

namespace Game.Cube
{
    /// <summary>What ItemPlacement needs to know about the module library: gate slots per pool module.</summary>
    public interface IGateSlotCatalog
    {
        /// <summary>Number of gate slots (alcoves) in a science pool module.</summary>
        int GateSlotCount(Theme theme, int moduleIndex);
    }

    /// <summary>A gate in one module gate slot, bound to the item (by home theme) that opens it.</summary>
    public readonly struct GatePlacement : IEquatable<GatePlacement>
    {
        public readonly ScreenAddress Screen;

        /// <summary>Index into the module's gate slots (ScreenModule.Gates).</summary>
        public readonly int Slot;

        /// <summary>The item that opens this gate, identified by its home theme.</summary>
        public readonly Theme Item;

        public GatePlacement(ScreenAddress screen, int slot, Theme item)
        {
            Screen = screen;
            Slot = slot;
            Item = item;
        }

        public bool Equals(GatePlacement other) => Screen == other.Screen && Slot == other.Slot && Item == other.Item;
        public override bool Equals(object obj) => obj is GatePlacement other && Equals(other);
        public override int GetHashCode() => (Screen.GetHashCode() * 31 + Slot) * 31 + (int)Item;
        public override string ToString() => $"{Item} gate at {Screen} #{Slot}";
    }

    /// <summary>
    /// An item's pickup: on a screen of its home face, either in the open or inside the alcove behind the
    /// gate in gate slot GuardSlot of that screen's module.
    /// </summary>
    public readonly struct PickupPlacement : IEquatable<PickupPlacement>
    {
        public const int Open = -1;

        public readonly Theme Item;
        public readonly ScreenAddress Screen;

        /// <summary>Gate slot whose alcove holds the pickup, or Open (-1).</summary>
        public readonly int GuardSlot;

        public PickupPlacement(Theme item, ScreenAddress screen, int guardSlot = Open)
        {
            Item = item;
            Screen = screen;
            GuardSlot = guardSlot;
        }

        public bool IsGuarded => GuardSlot >= 0;

        public bool Equals(PickupPlacement other) => Item == other.Item && Screen == other.Screen && GuardSlot == other.GuardSlot;
        public override bool Equals(object obj) => obj is PickupPlacement other && Equals(other);
        public override int GetHashCode() => ((int)Item * 31 + Screen.GetHashCode()) * 31 + GuardSlot;

        public override string ToString() =>
            IsGuarded ? $"{Item} pickup at {Screen} behind gate slot #{GuardSlot}" : $"{Item} pickup at {Screen}";
    }

    /// <summary>
    /// Pure, deterministic item and gate placement for a revealed science layout. Items are keyed by
    /// their home theme (one per built science theme), so swapping a placeholder ItemDefinition for a
    /// face's real item needs no change here. For every item it places gates into module gate slots:
    /// at least one on the item's home face (up to HomeGatesPerItem when slots allow) and at least one
    /// on another built face. Each pickup sits on its home face, in the open or inside another item's
    /// gated alcove when that keeps the item dependencies acyclic (never behind its own gate). Unused gate
    /// slots get no gate. Uses its own SeededRng stream, so the same seed gives the same placement.
    /// </summary>
    public sealed class ItemPlacement
    {
        /// <summary>PCG32 stream for item placement (themes use the default stream, CubeLayout stream 2).</summary>
        public const ulong RngStream = 3;

        /// <summary>Target number of gates per item on its home face ("mostly on the home face").</summary>
        public const int HomeGatesPerItem = 2;

        /// <summary>Gates per item on another built face.</summary>
        public const int OffHomeGatesPerItem = 1;

        private readonly List<GatePlacement> gates;
        private readonly List<PickupPlacement> pickups;

        public int Seed { get; }
        public IReadOnlyList<GatePlacement> Gates => gates;
        public IReadOnlyList<PickupPlacement> Pickups => pickups;

        /// <summary>A placement from explicit data (hand-made placements in tests and tools).</summary>
        public ItemPlacement(int seed, IEnumerable<GatePlacement> gates, IEnumerable<PickupPlacement> pickups)
        {
            Seed = seed;
            this.gates = new List<GatePlacement>(gates ?? Array.Empty<GatePlacement>());
            this.pickups = new List<PickupPlacement>(pickups ?? Array.Empty<PickupPlacement>());
        }

        /// <summary>The items of a run: one per built science theme that is laid out, in theme order.</summary>
        public static List<Theme> DefaultItems(CubeLayout layout)
        {
            var items = new List<Theme>();
            foreach (Theme theme in CubeModel.ScienceThemes)
            {
                foreach (FaceLayout fl in layout.Faces.Values)
                {
                    if (fl.Theme != theme) continue;
                    items.Add(theme);
                    break;
                }
            }
            return items;
        }

        /// <summary>
        /// The run's item list, the one way both the game (CubeWorld) and the seed sweep derive it: the
        /// laid-out science themes that the item catalog has an item for (all laid-out themes when no
        /// catalog themes are given), in theme order. The list drives the RNG draws, so sharing it keeps
        /// the swept placement identical to the played one.
        /// </summary>
        public static List<Theme> RunItems(CubeLayout layout, IEnumerable<Theme> catalogThemes)
        {
            List<Theme> items = DefaultItems(layout);
            if (catalogThemes == null) return items;
            var available = new HashSet<Theme>(catalogThemes);
            items.RemoveAll(t => !available.Contains(t));
            return items;
        }

        /// <summary>The placement of a run: Generate over RunItems. Used by CubeWorld and SeedSweep.</summary>
        public static ItemPlacement ForRun(CubeModel model, CubeLayout layout, IGateSlotCatalog catalog,
            IEnumerable<Theme> catalogThemes) =>
            Generate(model, layout, catalog, RunItems(layout, catalogThemes));

        /// <summary>
        /// Places gates and pickups for the given items (home themes; default: every laid-out science
        /// theme). Items whose home face is not laid out are ignored.
        /// </summary>
        public static ItemPlacement Generate(CubeModel model, CubeLayout layout, IGateSlotCatalog catalog,
            IEnumerable<Theme> items = null)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            // Items in a fixed (theme) order, whatever order the caller lists them in.
            var wanted = new HashSet<Theme>(items ?? DefaultItems(layout));
            var itemList = new List<Theme>();
            var homeFace = new Dictionary<Theme, FaceId>();
            foreach (Theme theme in CubeModel.ScienceThemes)
            {
                if (!wanted.Contains(theme)) continue;
                FaceId face = model.FaceOf(theme);
                if (!layout.TryGetFace(face, out _)) continue;
                itemList.Add(theme);
                homeFace[theme] = face;
            }

            // Free gate slots per laid-out face, in a fixed order (face, row-major cell, slot).
            var freeSlots = new Dictionary<FaceId, List<(ScreenAddress screen, int slot)>>();
            var laidOutFaces = new List<FaceId>();
            for (int f = 0; f < CubeSettings.FaceCount; f++)
            {
                var face = (FaceId)f;
                if (!layout.TryGetFace(face, out FaceLayout fl)) continue;
                laidOutFaces.Add(face);
                var slots = new List<(ScreenAddress, int)>();
                for (int y = 0; y < fl.FaceSize; y++)
                {
                    for (int x = 0; x < fl.FaceSize; x++)
                    {
                        var screen = new ScreenAddress(face, x, y);
                        int count = catalog.GateSlotCount(fl.Theme, fl.ModuleAt(screen.Cell));
                        for (int s = 0; s < count; s++) slots.Add((screen, s));
                    }
                }
                freeSlots[face] = slots;
            }

            var rng = new SeededRng(unchecked((ulong)(uint)model.Seed), RngStream);
            var gates = new List<GatePlacement>();

            GatePlacement TakeSlot(FaceId face, Theme item)
            {
                List<(ScreenAddress screen, int slot)> free = freeSlots[face];
                if (free.Count == 0)
                    throw new InvalidOperationException($"No free gate slot on {face} for the {item} item");
                int pick = rng.NextInt(free.Count);
                (ScreenAddress screen, int slot) = free[pick];
                free.RemoveAt(pick);
                var gate = new GatePlacement(screen, slot, item);
                gates.Add(gate);
                return gate;
            }

            // 1. The required gates: one on the home face and one on another built face, per item.
            foreach (Theme item in itemList)
            {
                TakeSlot(homeFace[item], item);
                for (int g = 0; g < OffHomeGatesPerItem; g++)
                {
                    var others = new List<FaceId>();
                    foreach (FaceId face in laidOutFaces)
                        if (face != homeFace[item] && freeSlots[face].Count > 0) others.Add(face);
                    if (others.Count == 0)
                    {
                        bool anotherFace = laidOutFaces.Count > 1;
                        if (anotherFace)
                            throw new InvalidOperationException($"No free gate slot off the home face for the {item} item");
                        break; // A single built face has no "elsewhere".
                    }
                    TakeSlot(others[rng.NextInt(others.Count)], item);
                }
            }

            // 2. Extra home gates while slots remain, so most of an item's gates sit on its home face.
            for (int extra = 1; extra < HomeGatesPerItem; extra++)
                foreach (Theme item in itemList)
                    if (freeSlots[homeFace[item]].Count > 0) TakeSlot(homeFace[item], item);

            // 3. Pickups on their home face: in the open, or behind another item's gate on that face if
            //    the dependency graph stays acyclic (each item depends on at most one other).
            var dependsOn = new Dictionary<Theme, Theme>();
            var usedAlcoves = new HashSet<GatePlacement>();
            var pickups = new List<PickupPlacement>();
            var order = new List<Theme>(itemList);
            rng.Shuffle(order);
            foreach (Theme item in order)
            {
                FaceId face = homeFace[item];
                var candidates = new List<GatePlacement>();
                foreach (GatePlacement gate in gates)
                {
                    if (gate.Screen.Face != face || gate.Item == item || usedAlcoves.Contains(gate)) continue;
                    if (DependsOn(dependsOn, gate.Item, item)) continue; // would close a cycle
                    candidates.Add(gate);
                }

                bool behind = candidates.Count > 0 && rng.NextInt(2) == 0;
                if (behind)
                {
                    GatePlacement guard = candidates[rng.NextInt(candidates.Count)];
                    usedAlcoves.Add(guard);
                    dependsOn[item] = guard.Item;
                    pickups.Add(new PickupPlacement(item, guard.Screen, guard.Slot));
                }
                else
                {
                    int n = model.FaceSize;
                    int cell = rng.NextInt(n * n);
                    pickups.Add(new PickupPlacement(item, new ScreenAddress(face, cell % n, cell / n)));
                }
            }
            // Report pickups in theme order.
            pickups.Sort((a, b) => a.Item.CompareTo(b.Item));

            return new ItemPlacement(model.Seed, gates, pickups);
        }

        /// <summary>True if 'from' (transitively) needs 'target' to be collected first.</summary>
        private static bool DependsOn(Dictionary<Theme, Theme> dependsOn, Theme from, Theme target)
        {
            Theme current = from;
            for (int guard = 0; guard <= dependsOn.Count; guard++)
            {
                if (current == target) return true;
                if (!dependsOn.TryGetValue(current, out current)) return false;
            }
            return true; // malformed (cyclic) chain: treat as dependent
        }

        /// <summary>The gate in a module gate slot, if one was placed there.</summary>
        public bool TryGetGate(ScreenAddress screen, int slot, out GatePlacement gate)
        {
            foreach (GatePlacement g in gates)
            {
                if (g.Screen == screen && g.Slot == slot)
                {
                    gate = g;
                    return true;
                }
            }
            gate = default;
            return false;
        }

        /// <summary>The pickup of an item, if placed.</summary>
        public bool TryGetPickup(Theme item, out PickupPlacement pickup)
        {
            foreach (PickupPlacement p in pickups)
            {
                if (p.Item == item)
                {
                    pickup = p;
                    return true;
                }
            }
            pickup = default;
            return false;
        }

        /// <summary>Compact text of the whole placement, for comparisons and logs.</summary>
        public string Signature()
        {
            var sb = new StringBuilder();
            foreach (GatePlacement g in gates)
                sb.Append("G:").Append(g.Item).Append('@').Append(g.Screen.Face).Append(g.Screen.Cell.x)
                    .Append(g.Screen.Cell.y).Append('#').Append(g.Slot).Append(' ');
            foreach (PickupPlacement p in pickups)
                sb.Append("P:").Append(p.Item).Append('@').Append(p.Screen.Face).Append(p.Screen.Cell.x)
                    .Append(p.Screen.Cell.y).Append('#').Append(p.GuardSlot).Append(' ');
            return sb.ToString();
        }
    }
}
