using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>What HiddenCurrencyPlacement needs to know about the module library: hidden-item slots per pool module.</summary>
    public interface IHiddenSlotCatalog
    {
        int HiddenSlotCount(Theme theme, int moduleIndex);
    }

    /// <summary>One hidden meta-currency item: in hidden-item slot Slot of the module on Screen.</summary>
    public readonly struct HiddenCurrencySpot : IEquatable<HiddenCurrencySpot>
    {
        public readonly ScreenAddress Screen;

        /// <summary>Index into the module's hidden-item slots (ScreenModule.HiddenItems).</summary>
        public readonly int Slot;

        public HiddenCurrencySpot(ScreenAddress screen, int slot)
        {
            Screen = screen;
            Slot = slot;
        }

        public bool Equals(HiddenCurrencySpot other) => Screen == other.Screen && Slot == other.Slot;
        public override bool Equals(object obj) => obj is HiddenCurrencySpot other && Equals(other);
        public override int GetHashCode() => Screen.GetHashCode() * 31 + Slot;
        public override string ToString() => $"hidden currency at {Screen} #{Slot}";
    }

    /// <summary>
    /// Pure, deterministic placement of the hidden meta-currency items: MinCount–MaxCount of them (fewer only when the
    /// built faces have fewer slots) in distinct hidden-item slots on the built science faces, on its own RNG stream (RngStreams.HiddenCurrency),
    /// so it changes no other draw. Hidden-item slots sit outside every alcove and off the exit lanes (the module builder
    /// enforces it, FindProblems checks it), and modules keep all exits open, so every spot is reachable from Town with no
    /// item: never behind a required or optional gate, and a pickup is a trigger, so it blocks nothing.
    /// </summary>
    public sealed class HiddenCurrencyPlacement
    {
        /// <summary>PCG32 stream for the hidden currency (see RngStreams).</summary>
        public const ulong RngStream = RngStreams.HiddenCurrency;

        public const int MinCount = 4;
        public const int MaxCount = 6;

        /// <summary>Meta currency each hidden item pays.</summary>
        public const int Amount = 5;

        private readonly List<HiddenCurrencySpot> spots;

        public int Seed { get; }
        public IReadOnlyList<HiddenCurrencySpot> Spots => spots;

        public HiddenCurrencyPlacement(int seed, IEnumerable<HiddenCurrencySpot> spots)
        {
            Seed = seed;
            this.spots = new List<HiddenCurrencySpot>(spots ?? Array.Empty<HiddenCurrencySpot>());
        }

        /// <summary>Every hidden-item slot on the laid-out faces, in a fixed order (face, row-major cell, slot).</summary>
        public static List<HiddenCurrencySpot> Candidates(CubeModel model, CubeLayout layout, IHiddenSlotCatalog catalog)
        {
            var all = new List<HiddenCurrencySpot>();
            for (int f = 0; f < CubeSettings.FaceCount; f++)
            {
                var face = (FaceId)f;
                if (!layout.TryGetFace(face, out FaceLayout fl)) continue;
                for (int y = 0; y < fl.FaceSize; y++)
                for (int x = 0; x < fl.FaceSize; x++)
                {
                    var screen = new ScreenAddress(face, x, y);
                    int count = catalog.HiddenSlotCount(fl.Theme, fl.ModuleAt(screen.Cell));
                    for (int s = 0; s < count; s++) all.Add(new HiddenCurrencySpot(screen, s));
                }
            }
            return all;
        }

        public static HiddenCurrencyPlacement Generate(CubeModel model, CubeLayout layout, IHiddenSlotCatalog catalog)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            List<HiddenCurrencySpot> candidates = Candidates(model, layout, catalog);
            var rng = new SeededRng(unchecked((ulong)(uint)model.Seed), RngStream);
            int wanted = MinCount + rng.NextInt(MaxCount - MinCount + 1);
            int count = Math.Min(wanted, candidates.Count);

            // Partial Fisher-Yates: the first `count` entries become a uniform pick; then back to the fixed order.
            var order = new List<int>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++) order.Add(i);
            for (int i = 0; i < count; i++)
            {
                int j = i + rng.NextInt(order.Count - i);
                (order[i], order[j]) = (order[j], order[i]);
            }
            List<int> picked = order.GetRange(0, count);
            picked.Sort();
            var spots = new List<HiddenCurrencySpot>(count);
            foreach (int i in picked) spots.Add(candidates[i]);
            return new HiddenCurrencyPlacement(model.Seed, spots);
        }

        /// <summary>
        /// Problems with a placement (empty when all is well): the count (MinCount–MaxCount, or every slot when there are
        /// fewer), each spot on a laid-out face, on a screen reachable from Town, in a real and unshared hidden-item slot,
        /// and (given module prefabs) outside every gated alcove and off the exit lanes.
        /// </summary>
        public static List<string> FindProblems(CubeModel model, CubeLayout layout, HiddenCurrencyPlacement placement,
            IHiddenSlotCatalog catalog, Func<ScreenAddress, ScreenModule> moduleAt = null)
        {
            var problems = new List<string>();
            int available = Candidates(model, layout, catalog).Count;
            int n = placement.Spots.Count;
            int min = Math.Min(MinCount, available);
            if (n < min || n > MaxCount) problems.Add($"{n} hidden currency items (expected {min}–{MaxCount})");

            HashSet<ScreenAddress> reachable = SeedSweep.FindReachable(model);
            var used = new HashSet<HiddenCurrencySpot>();
            foreach (HiddenCurrencySpot spot in placement.Spots)
            {
                if (!used.Add(spot)) problems.Add($"{spot} is used twice");
                if (!layout.TryGetFace(spot.Screen.Face, out FaceLayout fl))
                {
                    problems.Add($"{spot} is not on a built science face");
                    continue;
                }
                if (!reachable.Contains(spot.Screen)) problems.Add($"{spot} is on an unreachable screen");
                int slots = catalog.HiddenSlotCount(fl.Theme, fl.ModuleAt(spot.Screen.Cell));
                if (spot.Slot < 0 || spot.Slot >= slots)
                {
                    problems.Add($"{spot}: the module has {slots} hidden-item slots");
                    continue;
                }
                ScreenModule module = moduleAt?.Invoke(spot.Screen);
                if (module == null) continue;
                string where = GeometryProblem(module, spot.Slot);
                if (where != null) problems.Add($"{spot} {where}");
            }
            return problems;
        }

        /// <summary>Why a module's hidden-item slot is not a fair spot (inside a gated alcove, in an exit lane), or null.</summary>
        public static string GeometryProblem(ScreenModule module, int slot)
        {
            HiddenItemSlot[] hidden = module.HiddenItems;
            if (slot < 0 || slot >= hidden.Length) return "has no such slot";
            Vector2 origin = module.transform.position;
            Vector2 local = (Vector2)hidden[slot].transform.position - origin;
            if (Mathf.Abs(local.x) < ScreenModule.ExitLaneHalfWidth || Mathf.Abs(local.y) < ScreenModule.ExitLaneHalfWidth)
                return "is in an exit lane";
            GateSlot[] gates = module.Gates;
            for (int g = 0; g < gates.Length; g++)
            {
                if (AlcoveRect(gates[g], origin).Contains(local))
                    return $"is inside the alcove behind gate slot #{g}";
            }
            return null;
        }

        /// <summary>The area a gate slot closes off (module-local): its opening through the pocket, wall to wall.</summary>
        public static Rect AlcoveRect(GateSlot gate, Vector2 moduleOrigin)
        {
            Vector2 opening = (Vector2)gate.transform.position - moduleOrigin;
            Vector2 far = opening + gate.PocketOffset * 2f;
            bool across = gate.Opening == Facing.North || gate.Opening == Facing.South;
            float half = GateSlot.GateWidth / 2f + GateSlot.GateThickness;
            Vector2 min = Vector2.Min(opening, far);
            Vector2 max = Vector2.Max(opening, far);
            if (across)
            {
                min.x = opening.x - half;
                max.x = opening.x + half;
            }
            else
            {
                min.y = opening.y - half;
                max.y = opening.y + half;
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public string Signature()
        {
            var sb = new StringBuilder();
            foreach (HiddenCurrencySpot s in spots)
                sb.Append("H:").Append(s.Screen.Face).Append(s.Screen.Cell.x).Append(s.Screen.Cell.y).Append('#').Append(s.Slot).Append(' ');
            return sb.ToString();
        }
    }
}
