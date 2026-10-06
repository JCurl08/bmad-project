using UnityEngine;

namespace Game.Cube
{
    /// <summary>One gate CubeWorld is about to build: required (ItemPlacement.Gates) or optional (a non-rolled variant's).</summary>
    public readonly struct FaceGateRequest
    {
        public readonly ScreenAddress Screen;
        public readonly int Slot;

        /// <summary>The theme whose item opens the gate.</summary>
        public readonly Theme Item;

        /// <summary>True for a non-rolled variant's optional gate (guards nothing required).</summary>
        public readonly bool Optional;

        public FaceGateRequest(ScreenAddress screen, int slot, Theme item, bool optional)
        {
            Screen = screen;
            Slot = slot;
            Item = item;
            Optional = optional;
        }

        public override string ToString() => $"{(Optional ? "optional " : "")}{Item} gate at {Screen} #{Slot}";
    }

    /// <summary>
    /// The hook a face's content plugs into CubeWorld through (Biology and Chemistry now; Physics later, the
    /// same way). Implementations live on the CubeWorld's GameObject, under Faces/&lt;Theme&gt;/, in three parts:
    /// gate behaviours, the trial and the population. On every reveal CubeWorld calls BeginReveal once the
    /// item placement is known, CreateGate for each gate whose item is this content's theme (null falls back
    /// to a plain Gate), and EndReveal after every module, gate and pickup exists. Clear runs on every rebuild
    /// (a new run), before the new run's faces are built.
    /// </summary>
    public interface IFaceContent
    {
        /// <summary>The theme this content belongs to (its item's home theme).</summary>
        Theme Theme { get; }

        void BeginReveal(CubeWorld world);

        /// <summary>Builds the gate for a request in a slot, opened by item; null lets CubeWorld build a plain Gate.</summary>
        Gate CreateGate(CubeWorld world, FaceGateRequest request, GateSlot slot, ItemDefinition item);

        void EndReveal(CubeWorld world);

        void Clear();
    }

    /// <summary>
    /// Optional extra hook for face content that replaces its item's one-shot pickup (Chemistry: the isotope
    /// dispenser). CubeWorld calls it for the pickup of the content's theme, after the gates; null falls back to
    /// a plain ItemPickup. The returned pickup goes into CubeWorld.Pickups like any other.
    /// </summary>
    public interface IFacePickupContent
    {
        ItemPickup CreatePickup(CubeWorld world, PickupPlacement placement, Transform parent, Vector2 position, ItemDefinition item);
    }
}
