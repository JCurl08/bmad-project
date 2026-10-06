using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>Something occupying a spot on a screen (local to its centre), with the radius it needs.</summary>
    public readonly struct ScreenMark
    {
        public readonly string What;
        public readonly Vector2 Local;
        public readonly float Radius;

        public ScreenMark(string what, Vector2 local, float radius)
        {
            What = what;
            Local = local;
            Radius = radius;
        }

        public override string ToString() => $"{What} at {Local}";
    }

    /// <summary>
    /// The geometry of one timed door on its screen (local to the screen centre): the door, the side the player
    /// comes from, its pocket, its switch and the timing that follows from them (TimeField).
    /// </summary>
    public sealed class DoorLayout
    {
        public Vector2 DoorLocal { get; }

        /// <summary>Unit vector from the door toward the player's side (the lane).</summary>
        public Vector2 Opening { get; }

        public float Width { get; }
        public float Thickness { get; }
        public float PocketDepth { get; }

        /// <summary>Boulders needed (the door's difficulty).</summary>
        public int Required { get; }

        /// <summary>False when no switch spot was found (a problem the sweep reports).</summary>
        public bool HasSwitch { get; }

        public Vector2 SwitchLocal { get; }

        /// <summary>Seconds at base player speed from leaving the switch to reaching the threshold.</summary>
        public float WalkSeconds { get; }

        /// <summary>Seconds the door stays open with no mass beside it.</summary>
        public float BaseSeconds { get; }

        /// <summary>The threshold's centre: in the lane just in front of the door.</summary>
        public Vector2 ThresholdLocal => DoorLocal + Opening * (Thickness / 2f + TimeField.ThresholdGap);

        /// <summary>The centre of the door's time field: its threshold, just in front of it.</summary>
        public Vector2 FieldLocal => ThresholdLocal;

        public DoorLayout(Vector2 door, Vector2 opening, float width, float thickness, float pocketDepth, int required)
            : this(door, opening, width, thickness, pocketDepth, required, false, default)
        {
        }

        private DoorLayout(Vector2 door, Vector2 opening, float width, float thickness, float pocketDepth, int required,
            bool hasSwitch, Vector2 switchLocal)
        {
            DoorLocal = door;
            Opening = opening.sqrMagnitude > 1e-6f ? opening.normalized : Vector2.down;
            Width = width;
            Thickness = thickness;
            PocketDepth = pocketDepth;
            Required = Mathf.Max(1, required);
            HasSwitch = hasSwitch;
            SwitchLocal = switchLocal;
            WalkSeconds = hasSwitch ? TimeField.WalkSeconds(switchLocal, ThresholdLocal) : 0f;
            BaseSeconds = hasSwitch ? TimeField.BaseSecondsFor(WalkSeconds, Required) : 0f;
        }

        /// <summary>The same door with its switch at a spot (timing follows from the distance).</summary>
        public DoorLayout WithSwitch(Vector2 switchLocal) =>
            new DoorLayout(DoorLocal, Opening, Width, Thickness, PocketDepth, Required, true, switchLocal);

        /// <summary>The same door needing another boulder count (its base time follows).</summary>
        public DoorLayout WithRequired(int required) =>
            new DoorLayout(DoorLocal, Opening, Width, Thickness, PocketDepth, required, HasSwitch, SwitchLocal);

        public override string ToString() => HasSwitch
            ? $"door {DoorLocal} switch {SwitchLocal} walk {WalkSeconds:0.00}s base {BaseSeconds:0.00}s"
            : $"door {DoorLocal} (no switch)";
    }

    /// <summary>One required timed door (a Physics gate) of the run: where, its difficulty and its boulders.</summary>
    public sealed class TimedDoorSpec
    {
        public ScreenAddress Screen { get; }
        public int Slot { get; }

        /// <summary>True when the door is on the Physics face (its home).</summary>
        public bool OnHome { get; }

        /// <summary>Boulders needed to pass (the difficulty parameter, 1 to 3).</summary>
        public int Required { get; }

        /// <summary>Extra boulders beyond the required ones (0 or 1).</summary>
        public int Spare { get; }

        /// <summary>The door's geometry and timing; null when the plan was made without modules.</summary>
        public DoorLayout Layout { get; }

        /// <summary>Where this door's boulders start (local to its screen centre); empty without modules.</summary>
        public IReadOnlyList<Vector2> Boulders { get; }

        public TimedDoorSpec(ScreenAddress screen, int slot, bool onHome, int required, int spare, DoorLayout layout,
            IReadOnlyList<Vector2> boulders)
        {
            Screen = screen;
            Slot = slot;
            OnHome = onHome;
            Required = required;
            Spare = spare;
            Layout = layout;
            Boulders = boulders ?? Array.Empty<Vector2>();
        }

        public TimedDoorSpec WithLayout(DoorLayout layout) => new TimedDoorSpec(Screen, Slot, OnHome, Required, Spare, layout, Boulders);

        public TimedDoorSpec WithBoulders(IReadOnlyList<Vector2> boulders) =>
            new TimedDoorSpec(Screen, Slot, OnHome, Required, Spare, Layout, new List<Vector2>(boulders ?? Array.Empty<Vector2>()));

        /// <summary>The same door with other boulder counts (its layout's timing follows the required count).</summary>
        public TimedDoorSpec WithCounts(int required, int spare) =>
            new TimedDoorSpec(Screen, Slot, OnHome, required, spare, Layout?.WithRequired(required), Boulders);

        public override string ToString() =>
            $"timed door at {Screen} #{Slot}{(OnHome ? "" : " (off-home)")}, needs {Required} (+{Spare} spare)";
    }

    /// <summary>One booth of the Physics trial: a small walled room beside the lane with a timed door.</summary>
    public sealed class TrialBoothSpec
    {
        /// <summary>The booth's walls and door (local rect).</summary>
        public Rect Footprint { get; }

        public DoorLayout Layout { get; }

        public TrialBoothSpec(Rect footprint, DoorLayout layout)
        {
            Footprint = footprint;
            Layout = layout;
        }
    }

    /// <summary>The Physics trial: booths on one screen sharing a limited set of boulders.</summary>
    public sealed class PhysicsTrialSpec
    {
        public ScreenAddress Screen { get; }
        public IReadOnlyList<TrialBoothSpec> Booths { get; }
        public IReadOnlyList<Vector2> Boulders { get; }

        /// <summary>Boulders each booth door needs (the shared set holds exactly this many).</summary>
        public int Required { get; }

        /// <summary>
        /// True when the booth doors weigh only the trial's own boulders (TimedDoorGate.MassOwner): boulders brought
        /// from the screen's timed doors cannot dilate a booth, so the shared boulders really must be moved.
        /// </summary>
        public bool CountsOnlyOwnBoulders { get; }

        public PhysicsTrialSpec(ScreenAddress screen, IReadOnlyList<TrialBoothSpec> booths, IReadOnlyList<Vector2> boulders, int required,
            bool countsOnlyOwnBoulders = true)
        {
            Screen = screen;
            Booths = booths;
            Boulders = boulders ?? Array.Empty<Vector2>();
            Required = required;
            CountsOnlyOwnBoulders = countsOnlyOwnBoulders;
        }
    }
}
