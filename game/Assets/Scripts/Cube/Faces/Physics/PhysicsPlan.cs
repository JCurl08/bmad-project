using System;
using System.Collections.Generic;
using System.Text;
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

    /// <summary>
    /// The Physics face of a run as pure data, from the run seed, the item placement and (for positions) the
    /// screen modules: every Physics gate becomes a timed door with a required boulder count (the difficulty
    /// parameter, 1 to MaxRequired) and 0-1 spare boulders, all on the door's own screen; each door gets a switch in
    /// the horizontal lane on the far side of the screen and the base open time that makes its required count
    /// (TimeField.BaseSecondsFor); and the trial (two booths on one Physics screen, sharing TrialRequired boulders, so
    /// the boulders must be moved from one door to the other) goes on the first Physics screen, in a seeded order,
    /// where it fits.
    /// Spots follow the shared rules of the Biology and Chemistry faces: switches sit in the lane, boulders off both
    /// lanes and inside the edge band (ScreenModule.KeepsExitsOpen), clear of walls, obstacles and alcoves
    /// (TownPlan.BlockedRects), apart from each other, and clear of every other face's interactables (Biology
    /// buttons, flowers and trial pieces, Chemistry lead plates and trial pieces, open pickups, hidden-item and
    /// core-entrance slots). Boulders start outside every door's time field (a door is never dilated before the
    /// player moves a boulder), and each is reachable for dragging to its door (a grid search over the screen).
    /// FindProblems checks all of it for the seed sweep. Uses its own SeededRng stream; each screen's spots use
    /// that stream with the screen in the seed, so one screen's layout never shifts another's.
    /// </summary>
    public sealed class PhysicsPlan
    {
        /// <summary>PCG32 stream for the Physics face plan (Chemistry plan 11 and population 12; Physics population 14).</summary>
        public const ulong RngStream = 13;

        /// <summary>Currency the Physics trial pays out (fired with TrialRoom.Completed; spent in 1.12).</summary>
        public const int TrialCurrency = 25;

        /// <summary>The difficulty range: a door needs 1 to this many boulders.</summary>
        public const int MaxRequiredCap = 3;
        public const int DefaultMaxRequired = 3;

        public const int TrialBooths = 2;
        public const int TrialRequired = 2;

        /// <summary>Minimum centre distance between a placed thing and any other interactable (the 1.9 spot rule).</summary>
        public const float MinGap = 1.05f;

        /// <summary>Extra room kept between the edges of two placed things.</summary>
        public const float GapMargin = 0.25f;

        /// <summary>Minimum distance between two boulders' starting spots.</summary>
        public const float BoulderSpacing = 1.0f;

        /// <summary>Boulders start at least this far beyond a door's time field.</summary>
        public const float FieldClearance = 0.25f;

        /// <summary>A switch must be at least this many seconds' walk from its door.</summary>
        public const float MinWalkSeconds = 0.9f;

        /// <summary>Boulders counted as "near a door" sit this far inside its field (for room and reachability).</summary>
        public const float NearMargin = 0.45f;

        // Booth geometry (local, for a booth above the horizontal lane; mirrored below it).
        public const float BoothInner = 1.2f;
        public const float BoothWall = 0.15f;
        public const float BoothNearY = 1.3f;
        public static float BoothOuter => BoothInner + 2f * BoothWall;
        public static float BoothDoorY => BoothNearY + BoothWall / 2f;
        public static float BoothFarY => BoothNearY + BoothOuter;

        private const float GridStep = 0.25f;
        private const float BoulderHalf = Boulder.Radius + 0.05f;
        private const int TrialAttempts = 40;

        /// <summary>Lane x distances (far side of the door) tried for a switch, farthest first.</summary>
        private static readonly float[] SwitchLaneX = { 6.3f, 5.6f, 4.9f, 4.2f, 3.5f };

        private readonly List<TimedDoorSpec> doors;
        private readonly Dictionary<ScreenAddress, ScreenContext> contexts;

        public int Seed { get; }
        public FaceId Face { get; }
        public int FaceSize { get; }
        public int MaxRequired { get; }
        public IReadOnlyList<TimedDoorSpec> Doors => doors;

        /// <summary>The Physics screen that holds the trial.</summary>
        public ScreenAddress TrialScreen { get; }

        /// <summary>The trial's booths and boulders; null without modules, or when it fits nowhere.</summary>
        public PhysicsTrialSpec Trial { get; }

        /// <summary>True when the plan was made with modules (so every position is known).</summary>
        public bool HasLayout => contexts != null;

        private PhysicsPlan(int seed, FaceId face, int faceSize, int maxRequired, List<TimedDoorSpec> doors, ScreenAddress trialScreen,
            PhysicsTrialSpec trial, Dictionary<ScreenAddress, ScreenContext> contexts)
        {
            Seed = seed;
            Face = face;
            FaceSize = faceSize;
            MaxRequired = maxRequired;
            this.doors = doors;
            TrialScreen = trialScreen;
            Trial = trial;
            this.contexts = contexts;
        }

        /// <summary>A copy of the plan with one door replaced (tools and tests of the sweep rules).</summary>
        public PhysicsPlan WithDoor(int index, TimedDoorSpec door)
        {
            var list = new List<TimedDoorSpec>(doors) { [index] = door };
            return new PhysicsPlan(Seed, Face, FaceSize, MaxRequired, list, TrialScreen, Trial, contexts);
        }

        /// <summary>A copy of the plan with another trial (tools and tests of the sweep rules).</summary>
        public PhysicsPlan WithTrial(PhysicsTrialSpec trial) =>
            new PhysicsPlan(Seed, Face, FaceSize, MaxRequired, doors, TrialScreen, trial, contexts);

        /// <summary>The marks of other faces' interactables on a screen (empty without modules).</summary>
        public IReadOnlyList<ScreenMark> OtherMarks(ScreenAddress screen) =>
            contexts != null && contexts.TryGetValue(screen, out ScreenContext c) ? c.Others : (IReadOnlyList<ScreenMark>)Array.Empty<ScreenMark>();

        public bool TryGetDoor(ScreenAddress screen, int slot, out TimedDoorSpec spec)
        {
            foreach (TimedDoorSpec s in doors)
            {
                if (s.Screen != screen || s.Slot != slot) continue;
                spec = s;
                return true;
            }
            spec = null;
            return false;
        }

        /// <summary>Compact text of the plan, for comparisons and logs.</summary>
        public string Signature()
        {
            var sb = new StringBuilder();
            sb.Append("trial@").Append(TrialScreen).Append(' ');
            foreach (TimedDoorSpec s in doors)
            {
                sb.Append(s).Append(' ');
                if (s.Layout != null) sb.Append(s.Layout).Append(' ');
                foreach (Vector2 b in s.Boulders) sb.Append(b.ToString("0.00")).Append(' ');
                sb.Append("; ");
            }
            if (Trial != null)
            {
                foreach (TrialBoothSpec b in Trial.Booths) sb.Append("booth ").Append(b.Layout).Append(' ');
                foreach (Vector2 b in Trial.Boulders) sb.Append(b.ToString("0.00")).Append(' ');
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ Create

        /// <summary>
        /// The plan of a run; null when Physics is not an item of the placement (no Physics pickup). With moduleAt
        /// (the module of a screen: prefabs in the sweep, instances at runtime) every position is laid out; pass the
        /// Biology beak kinds (BiologyBeaks.VariantKinds) so Biology's trial spots are known. maxRequired is the
        /// difficulty knob (1 to MaxRequiredCap boulders per door).
        /// </summary>
        public static PhysicsPlan Create(CubeModel model, ItemPlacement placement, Func<ScreenAddress, ScreenModule> moduleAt = null,
            IReadOnlyList<BeakKind> biologyVariantKinds = null, int maxRequired = DefaultMaxRequired)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (placement == null || !placement.TryGetPickup(Theme.Physics, out _)) return null;

            maxRequired = Mathf.Clamp(maxRequired, 1, MaxRequiredCap);
            FaceId face = model.FaceOf(Theme.Physics);
            int n = model.FaceSize;
            var rng = new SeededRng(unchecked((ulong)(uint)model.Seed), RngStream);

            var specs = new List<TimedDoorSpec>();
            foreach (GatePlacement g in placement.Gates)
            {
                if (g.Item != Theme.Physics) continue;
                int required = 1 + rng.NextInt(maxRequired);
                int spare = rng.NextInt(2);
                specs.Add(new TimedDoorSpec(g.Screen, g.Slot, g.Screen.Face == face, required, spare, null, null));
            }

            var trialOrder = new List<ScreenAddress>();
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    trialOrder.Add(new ScreenAddress(face, x, y));
            rng.Shuffle(trialOrder);

            if (moduleAt == null)
                return new PhysicsPlan(model.Seed, face, n, maxRequired, specs, trialOrder[0], null, null);

            var builder = new LayoutBuilder(model, placement, moduleAt, biologyVariantKinds);
            builder.Lay(specs, trialOrder, out ScreenAddress trialScreen, out PhysicsTrialSpec trial);
            return new PhysicsPlan(model.Seed, face, n, maxRequired, specs, trialScreen, trial, builder.Contexts);
        }

        // ------------------------------------------------------------------ Problems

        /// <summary>
        /// Problems with a run's Physics plan: every Physics gate is a timed door with a required count of 1 to
        /// MaxRequired and 0-1 spare; the mitt opens a door on another face (when another face is built); the trial
        /// is on Physics. With moduleAt, the layout too (LayoutProblems): every door has a switch and timing that
        /// make its required count exact, its own boulders on its screen, enough of them reachable, room for them
        /// near the door, no boulder starting in a field, no spot on another face's interactable, and a trial that
        /// fits. Empty when all is well (or when Physics is not an item).
        /// </summary>
        public static List<string> FindProblems(CubeModel model, ItemPlacement placement,
            Func<ScreenAddress, ScreenModule> moduleAt = null, IReadOnlyList<BeakKind> biologyVariantKinds = null,
            int maxRequired = DefaultMaxRequired)
        {
            var problems = new List<string>();
            PhysicsPlan plan = Create(model, placement, moduleAt, biologyVariantKinds, maxRequired);
            if (plan == null) return problems;

            int builtFaces = 0;
            for (int f = 0; f < CubeSettings.FaceCount; f++)
                if (CubeLayout.IsLaidOut(model, (FaceId)f)) builtFaces++;

            int gates = 0;
            foreach (GatePlacement g in placement.Gates)
            {
                if (g.Item != Theme.Physics) continue;
                gates++;
                if (!plan.TryGetDoor(g.Screen, g.Slot, out _)) problems.Add($"{g} is not a timed door");
            }
            if (gates != plan.Doors.Count) problems.Add($"{plan.Doors.Count} timed doors for {gates} Physics gates");

            int offHome = 0;
            foreach (TimedDoorSpec s in plan.Doors)
            {
                if (s.Required < 1 || s.Required > plan.MaxRequired) problems.Add($"{s}: required count out of 1-{plan.MaxRequired}");
                if (s.Spare < 0 || s.Spare > 1) problems.Add($"{s}: {s.Spare} spare boulders");
                if (s.OnHome != (s.Screen.Face == plan.Face)) problems.Add($"{s}: wrong home flag");
                if (!s.OnHome) offHome++;
            }
            if (offHome < 1 && builtFaces > 1) problems.Add("the Mass Mitt opens no gate on another face");
            if (plan.TrialScreen.Face != plan.Face) problems.Add($"the trial is on {plan.TrialScreen}, not on Physics");
            if (plan.HasLayout) problems.AddRange(plan.LayoutProblems());
            return problems;
        }

        /// <summary>The layout rules of a plan made with modules (see FindProblems). Empty without modules.</summary>
        public List<string> LayoutProblems()
        {
            var problems = new List<string>();
            if (contexts == null) return problems;

            var byScreen = new Dictionary<ScreenAddress, List<(string what, Vector2 at, float r, bool boulder)>>();
            void Mark(ScreenAddress screen, string what, Vector2 at, float r, bool boulder)
            {
                if (!byScreen.TryGetValue(screen, out var list)) byScreen[screen] = list = new List<(string, Vector2, float, bool)>();
                list.Add((what, at, r, boulder));
            }

            foreach (TimedDoorSpec s in doors)
            {
                ScreenContext ctx = Context(s.Screen);
                DoorLayout d = s.Layout;
                if (d == null)
                {
                    problems.Add($"{s}: no door layout");
                    continue;
                }
                problems.AddRange(DoorProblems(s.ToString(), d, ctx, BoothsOn(s.Screen)));
                if (s.Boulders.Count != s.Required + s.Spare)
                    problems.Add($"{s}: {s.Boulders.Count} boulders on its screen, want {s.Required + s.Spare}");
                int reachable = 0;
                var grid = new ReachGrid(ctx.Blocked, BoothsOn(s.Screen));
                bool[] reach = grid.ReachableTo(d);
                foreach (Vector2 b in s.Boulders)
                    if (grid.Reaches(reach, b)) reachable++;
                if (reachable < s.Required)
                    problems.Add($"{s}: only {reachable} reachable boulders on its screen, needs {s.Required}");
                int room = grid.Room(reach, d);
                if (room < s.Required) problems.Add($"{s}: room for only {room} boulders near the door, needs {s.Required}");
                if (d.HasSwitch) Mark(s.Screen, $"switch of {s}", d.SwitchLocal, TimeField.SwitchRadius, false);
                foreach (Vector2 b in s.Boulders) Mark(s.Screen, $"boulder of {s}", b, Boulder.Radius, true);
            }

            if (Trial == null) problems.Add("the trial fits on no Physics screen");
            else
            {
                ScreenContext ctx = Context(Trial.Screen);
                if (Trial.Screen != TrialScreen) problems.Add($"the trial is built on {Trial.Screen}, planned on {TrialScreen}");
                if (Trial.Booths.Count != TrialBooths) problems.Add($"the trial has {Trial.Booths.Count} booths, want {TrialBooths}");
                if (Trial.Boulders.Count != Trial.Required)
                    problems.Add($"the trial has {Trial.Boulders.Count} boulders, want exactly {Trial.Required} (shared)");
                List<Rect> booths = BoothsOn(Trial.Screen);
                var grid = new ReachGrid(ctx.Blocked, booths);
                for (int i = 0; i < Trial.Booths.Count; i++)
                {
                    TrialBoothSpec booth = Trial.Booths[i];
                    string what = $"trial booth {i} on {Trial.Screen}";
                    if (!ScreenModule.KeepsExitsOpen(booth.Footprint)) problems.Add($"{what} blocks a lane or an edge");
                    foreach (Rect b in ctx.Blocked)
                        if (b.Overlaps(booth.Footprint)) problems.Add($"{what} overlaps a wall or alcove");
                    for (int j = i + 1; j < Trial.Booths.Count; j++)
                    {
                        if (Vector2.Distance(booth.Layout.FieldLocal, Trial.Booths[j].Layout.FieldLocal) < 2f * TimeField.FieldRadius)
                            problems.Add($"{what} and booth {j} share a time field (no need to move the boulders)");
                        if (Grow(booth.Footprint, 0.5f).Overlaps(Trial.Booths[j].Footprint)) problems.Add($"{what} touches booth {j}");
                    }
                    problems.AddRange(DoorProblems(what, booth.Layout, ctx, booths));
                    bool[] reach = grid.ReachableTo(booth.Layout);
                    int reachable = 0;
                    foreach (Vector2 b in Trial.Boulders)
                        if (grid.Reaches(reach, b)) reachable++;
                    if (reachable < Trial.Required) problems.Add($"{what}: only {reachable} trial boulders can reach it");
                    int room = grid.Room(reach, booth.Layout);
                    if (room < Trial.Required) problems.Add($"{what}: room for only {room} boulders near the door");
                    if (booth.Layout.HasSwitch) Mark(Trial.Screen, $"switch of {what}", booth.Layout.SwitchLocal, TimeField.SwitchRadius, false);
                    foreach (ScreenMark o in ctx.Others)
                        if (DistanceToRect(o.Local, booth.Footprint) < o.Radius + GapMargin - 1e-4f)
                            problems.Add($"{Trial.Screen}: {what} sits on {o.What}");
                }
                foreach (Vector2 b in Trial.Boulders) Mark(Trial.Screen, "trial boulder", b, Boulder.Radius, true);

                // Non-trial boulders (the screen's timed doors') must never satisfy a booth: the booths weigh only the
                // trial's own boulders, or else no other boulder can be dragged into a booth's field.
                if (!Trial.CountsOnlyOwnBoulders)
                {
                    var others = new List<Vector2>();
                    foreach (TimedDoorSpec s in doors)
                        if (s.Screen == Trial.Screen) others.AddRange(s.Boulders);
                    for (int i = 0; i < Trial.Booths.Count; i++)
                    {
                        bool[] reach = grid.ReachableTo(Trial.Booths[i].Layout);
                        int reachable = 0;
                        foreach (Vector2 b in others)
                            if (grid.Reaches(reach, b)) reachable++;
                        if (reachable > 0)
                            problems.Add($"trial booth {i} on {Trial.Screen}: {reachable} non-trial boulders can dilate it " +
                                         "(the shared boulders need not be moved)");
                    }
                }
            }

            // Every spot: clear of other faces' interactables and of each other; boulders off the lanes, out of walls,
            // and outside every time field on their screen.
            foreach (KeyValuePair<ScreenAddress, List<(string what, Vector2 at, float r, bool boulder)>> e in byScreen)
            {
                ScreenContext ctx = Context(e.Key);
                List<(string what, Vector2 at, float r, bool boulder)> list = e.Value;
                for (int i = 0; i < list.Count; i++)
                {
                    var a = list[i];
                    foreach (ScreenMark o in ctx.Others)
                        if (Vector2.Distance(a.at, o.Local) < Gap(a.r, o.Radius) - 1e-4f)
                            problems.Add($"{e.Key}: {a.what} sits on {o.What}");
                    for (int j = i + 1; j < list.Count; j++)
                    {
                        var b = list[j];
                        float need = a.boulder && b.boulder ? BoulderSpacing : Gap(a.r, b.r);
                        if (Vector2.Distance(a.at, b.at) < need - 1e-4f) problems.Add($"{e.Key}: {a.what} and {b.what} share a spot");
                    }
                    if (!a.boulder) continue;
                    if (!ScreenModule.KeepsExitsOpen(Square(a.at, BoulderHalf))) problems.Add($"{e.Key}: {a.what} blocks a lane or an edge");
                    if (DiscHits(a.at, Boulder.Radius, ctx.Blocked) || DiscHits(a.at, Boulder.Radius, BoothsOn(e.Key)))
                        problems.Add($"{e.Key}: {a.what} is inside a wall");
                    foreach (Vector2 door in DoorsOn(e.Key))
                        if (Vector2.Distance(a.at, door) <= TimeField.FieldRadius)
                            problems.Add($"{e.Key}: {a.what} starts in the time field of the door at {door}");
                }
            }
            return problems;
        }

        /// <summary>The rules for one door's switch and timing.</summary>
        private static List<string> DoorProblems(string what, DoorLayout d, ScreenContext ctx, List<Rect> booths)
        {
            var problems = new List<string>();
            if (!d.HasSwitch)
            {
                problems.Add($"{what}: no room for its switch");
                return problems;
            }
            if (d.WalkSeconds < MinWalkSeconds) problems.Add($"{what}: the switch is only {d.WalkSeconds:0.00}s from the door");
            float m = TimeField.BoulderMass;
            if (TimeField.Passable(d.BaseSeconds, 0f, d.WalkSeconds)) problems.Add($"{what}: passable with no boulders");
            if (TimeField.Passable(d.BaseSeconds, (d.Required - 1) * m, d.WalkSeconds))
                problems.Add($"{what}: passable with {d.Required - 1} boulders");
            if (!TimeField.Passable(d.BaseSeconds, d.Required * m, d.WalkSeconds))
                problems.Add($"{what}: not passable with its {d.Required} boulders");
            if (!PathClear(d.SwitchLocal, d.ThresholdLocal, ctx.Blocked, booths)) problems.Add($"{what}: the way from the switch is blocked");
            return problems;
        }

        private List<Rect> BoothsOn(ScreenAddress screen)
        {
            var rects = new List<Rect>();
            if (Trial != null && Trial.Screen == screen)
                foreach (TrialBoothSpec b in Trial.Booths) rects.Add(b.Footprint);
            return rects;
        }

        private List<Vector2> DoorsOn(ScreenAddress screen)
        {
            var list = new List<Vector2>();
            foreach (TimedDoorSpec s in doors)
                if (s.Screen == screen && s.Layout != null) list.Add(s.Layout.FieldLocal);
            if (Trial != null && Trial.Screen == screen)
                foreach (TrialBoothSpec b in Trial.Booths) list.Add(b.Layout.FieldLocal);
            return list;
        }

        private ScreenContext Context(ScreenAddress screen) =>
            contexts.TryGetValue(screen, out ScreenContext c) ? c : new ScreenContext(new List<Rect>(), new List<ScreenMark>());

        // ------------------------------------------------------------------ Geometry helpers

        /// <summary>The minimum centre distance between two placed things of these radii.</summary>
        public static float Gap(float a, float b) => Mathf.Max(MinGap, a + b + GapMargin);

        private static Rect Square(Vector2 centre, float half) => new Rect(centre - Vector2.one * half, Vector2.one * (2f * half));

        private static Rect Grow(Rect r, float by) => new Rect(r.xMin - by, r.yMin - by, r.width + 2f * by, r.height + 2f * by);

        private static float DistanceToRect(Vector2 p, Rect r)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
            float dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static bool DiscHits(Vector2 c, float r, List<Rect> rects)
        {
            foreach (Rect b in rects)
                if (DistanceToRect(c, b) < r) return true;
            return false;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>True when a player can walk straight from a to b without touching a wall, alcove or booth.</summary>
        public static bool PathClear(Vector2 a, Vector2 b, List<Rect> blocked, List<Rect> booths)
        {
            float length = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / 0.1f));
            float r = TimeField.PlayerRadius - 0.02f;
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
                if (DiscHits(p, r, blocked) || (booths != null && DiscHits(p, r, booths))) return false;
            }
            return true;
        }

        /// <summary>The geometry of a timed door in a module gate slot (local to the screen centre).</summary>
        public static DoorLayout SlotDoor(ScreenModule module, int slot, int required)
        {
            GateSlot gate = module.Gates[slot];
            Vector2 local = gate.transform.position - module.transform.position;
            Vector2 opening = gate.Opening.ToVector();
            return new DoorLayout(local, opening, GateSlot.GateWidth, GateSlot.GateThickness, gate.PocketOffset.magnitude * 2f, required);
        }

        /// <summary>The footprint of a trial booth centred at bx, above (side 1) or below (side -1) the horizontal lane.</summary>
        public static Rect BoothFootprint(float bx, int side)
        {
            float half = BoothOuter / 2f;
            return side > 0
                ? Rect.MinMaxRect(bx - half, BoothNearY, bx + half, BoothFarY)
                : Rect.MinMaxRect(bx - half, -BoothFarY, bx + half, -BoothNearY);
        }

        /// <summary>The door of a trial booth (in its lane-facing wall).</summary>
        public static DoorLayout BoothDoor(float bx, int side, int required) =>
            new DoorLayout(new Vector2(bx, side * BoothDoorY), new Vector2(0f, -side), BoothInner, BoothWall, BoothInner, required);

        // ------------------------------------------------------------------ Other faces

        private sealed class ScreenContext
        {
            public readonly List<Rect> Blocked;
            public readonly List<ScreenMark> Others;

            public ScreenContext(List<Rect> blocked, List<ScreenMark> others)
            {
                Blocked = blocked;
                Others = others;
            }
        }

        /// <summary>Lays out doors, the trial and boulders, keeping per-screen contexts for the checks.</summary>
        private sealed class LayoutBuilder
        {
            private readonly CubeModel model;
            private readonly ItemPlacement placement;
            private readonly Func<ScreenAddress, ScreenModule> moduleAt;
            private readonly Dictionary<ScreenAddress, List<ScreenMark>> others;
            public readonly Dictionary<ScreenAddress, ScreenContext> Contexts = new Dictionary<ScreenAddress, ScreenContext>();

            public LayoutBuilder(CubeModel model, ItemPlacement placement, Func<ScreenAddress, ScreenModule> moduleAt,
                IReadOnlyList<BeakKind> kinds)
            {
                this.model = model;
                this.placement = placement;
                this.moduleAt = moduleAt;
                others = OtherFaceMarks(model, placement, moduleAt, kinds);
            }

            private ScreenContext Ctx(ScreenAddress screen)
            {
                if (Contexts.TryGetValue(screen, out ScreenContext c)) return c;
                ScreenModule module = moduleAt(screen);
                var marks = others.TryGetValue(screen, out List<ScreenMark> list) ? new List<ScreenMark>(list) : new List<ScreenMark>();
                if (module != null)
                {
                    Vector2 origin = module.transform.position;
                    foreach (HiddenItemSlot h in module.HiddenItems)
                        marks.Add(new ScreenMark("hidden-item slot", (Vector2)h.transform.position - origin, Boulder.Radius));
                    foreach (CoreEntranceSlot core in module.CoreEntrances)
                        marks.Add(new ScreenMark("core-entrance slot", (Vector2)core.transform.position - origin, Boulder.Radius));
                }
                c = new ScreenContext(TownPlan.BlockedRects(module), marks);
                Contexts[screen] = c;
                return c;
            }

            public void Lay(List<TimedDoorSpec> specs, List<ScreenAddress> trialOrder, out ScreenAddress trialScreen,
                out PhysicsTrialSpec trial)
            {
                // 1. Doors and their switches (fixed candidate order: no randomness).
                var own = new Dictionary<ScreenAddress, List<ScreenMark>>();
                List<ScreenMark> Own(ScreenAddress s) => own.TryGetValue(s, out var l) ? l : own[s] = new List<ScreenMark>();
                for (int i = 0; i < specs.Count; i++)
                {
                    TimedDoorSpec s = specs[i];
                    ScreenModule module = moduleAt(s.Screen);
                    if (module == null || s.Slot >= module.Gates.Length) continue;
                    ScreenContext ctx = Ctx(s.Screen);
                    DoorLayout door = SlotDoor(module, s.Slot, s.Required);
                    door = WithSwitch(door, ctx, Own(s.Screen), null);
                    specs[i] = s.WithLayout(door);
                    AddDoorMarks(Own(s.Screen), door, s.ToString());
                }

                // 2. The trial, on the first screen in the seeded order where it and every door's boulders fit.
                trialScreen = trialOrder[0];
                trial = null;
                Dictionary<int, List<Vector2>> trialDoorBoulders = null;
                foreach (ScreenAddress t in trialOrder)
                {
                    if (moduleAt(t) == null) continue;
                    var rng = new SeededRng(ScreenSeed(t, 1), RngStream);
                    PhysicsTrialSpec attempt = TryTrial(t, specs, Own(t), rng, out Dictionary<int, List<Vector2>> boulders,
                        out List<TimedDoorSpec> adjusted);
                    if (attempt == null) continue;
                    trial = attempt;
                    trialScreen = t;
                    trialDoorBoulders = boulders;
                    for (int i = 0; i < specs.Count; i++) specs[i] = adjusted[i];
                    break;
                }

                // 3. Every other screen's boulders.
                var screens = new List<ScreenAddress>();
                foreach (TimedDoorSpec s in specs)
                    if (!screens.Contains(s.Screen)) screens.Add(s.Screen);
                foreach (ScreenAddress screen in screens)
                {
                    Dictionary<int, List<Vector2>> placed;
                    if (trial != null && screen == trial.Screen) placed = trialDoorBoulders;
                    else
                    {
                        FitBoulders(screen, specs, null, Own(screen), out List<TimedDoorSpec> adjusted, out placed, out _);
                        for (int i = 0; i < specs.Count; i++) specs[i] = adjusted[i];
                    }
                    foreach (KeyValuePair<int, List<Vector2>> e in placed) specs[e.Key] = specs[e.Key].WithBoulders(e.Value);
                }
            }

            private ulong ScreenSeed(ScreenAddress s, int use) =>
                ((ulong)(uint)model.Seed << 32) | (uint)(((int)s.Face * 64 + s.Cell.x * 8 + s.Cell.y) | (use << 16));

            /// <summary>
            /// Boulders for a screen (PlaceBoulders) with the doors' rolled counts where they fit; on a screen too
            /// cramped for them, the counts come down until they do (spares first, then the largest requirement, never
            /// below one boulder). The adjusted specs come back in a copy. False only if even one boulder per door fails.
            /// </summary>
            private bool FitBoulders(ScreenAddress screen, List<TimedDoorSpec> specs, PhysicsTrialSpec trial, List<ScreenMark> own,
                out List<TimedDoorSpec> adjusted, out Dictionary<int, List<Vector2>> placed, out List<Vector2> trialBoulders)
            {
                adjusted = new List<TimedDoorSpec>(specs);
                while (true)
                {
                    var rng = new SeededRng(ScreenSeed(screen, trial != null ? 3 : 2), RngStream);
                    if (PlaceBoulders(screen, adjusted, trial, own, rng, out placed, out trialBoulders)) return true;
                    int spare = -1, largest = -1;
                    for (int i = 0; i < adjusted.Count; i++)
                    {
                        TimedDoorSpec s = adjusted[i];
                        if (s.Screen != screen || s.Layout == null) continue;
                        if (s.Spare > 0) spare = i;
                        if (s.Required > 1 && (largest < 0 || s.Required >= adjusted[largest].Required)) largest = i;
                    }
                    if (spare >= 0) adjusted[spare] = adjusted[spare].WithCounts(adjusted[spare].Required, 0);
                    else if (largest >= 0) adjusted[largest] = adjusted[largest].WithCounts(adjusted[largest].Required - 1, 0);
                    else return false;
                }
            }

            private static void AddDoorMarks(List<ScreenMark> marks, DoorLayout door, string what)
            {
                marks.Add(new ScreenMark($"threshold of {what}", door.ThresholdLocal, TimeField.ThresholdRadius + TimeField.PlayerRadius));
                if (door.HasSwitch) marks.Add(new ScreenMark($"switch of {what}", door.SwitchLocal, TimeField.SwitchRadius));
            }

            /// <summary>The door with the first switch spot in the lane, on the far side, that is free and far enough.</summary>
            private static DoorLayout WithSwitch(DoorLayout door, ScreenContext ctx, List<ScreenMark> own, List<Rect> booths)
            {
                float sx = door.DoorLocal.x >= 0f ? 1f : -1f;
                float sy = door.DoorLocal.y >= 0f ? 1f : -1f;
                Vector2 threshold = door.ThresholdLocal;
                foreach (float x in SwitchLaneX)
                {
                    foreach (float y in new[] { sy * 0.6f, 0f, -sy * 0.6f })
                    {
                        var at = new Vector2(-sx * x, y);
                        if (!Free(at, TimeField.SwitchRadius, ctx.Others) || !Free(at, TimeField.SwitchRadius, own)) continue;
                        if (TimeField.WalkSeconds(at, threshold) < MinWalkSeconds) continue;
                        if (!PathClear(at, threshold, ctx.Blocked, booths)) continue;
                        return door.WithSwitch(at);
                    }
                }
                return door;
            }

            private static bool Free(Vector2 at, float r, List<ScreenMark> marks)
            {
                foreach (ScreenMark m in marks)
                    if (Vector2.Distance(at, m.Local) < Gap(r, m.Radius)) return false;
                return true;
            }

            private PhysicsTrialSpec TryTrial(ScreenAddress screen, List<TimedDoorSpec> specs, List<ScreenMark> own, SeededRng rng,
                out Dictionary<int, List<Vector2>> doorBoulders, out List<TimedDoorSpec> adjusted)
            {
                doorBoulders = null;
                adjusted = specs;
                // What the doors here get without a trial: the trial may not cost them boulders.
                FitBoulders(screen, specs, null, own, out List<TimedDoorSpec> baseline, out _, out _);
                ScreenContext ctx = Ctx(screen);
                var candidates = new List<(float bx, int side)>();
                for (float x = BoothOuter / 2f + ScreenModule.ExitLaneHalfWidth; x <= 6.5f - BoothOuter / 2f + 1e-4f; x += GridStep)
                    foreach (int side in new[] { 1, -1 })
                        foreach (float sign in new[] { 1f, -1f })
                        {
                            float bx = sign * x;
                            Rect rect = BoothFootprint(bx, side);
                            if (!BoothFits(rect, BoothDoor(bx, side, TrialRequired), ctx, own)) continue;
                            candidates.Add((bx, side));
                        }
                if (candidates.Count < TrialBooths) return null;

                for (int attempt = 0; attempt < TrialAttempts; attempt++)
                {
                    (float ax, int aside) = candidates[rng.NextInt(candidates.Count)];
                    Rect aRect = BoothFootprint(ax, aside);
                    DoorLayout aDoor = BoothDoor(ax, aside, TrialRequired);
                    var partners = new List<(float bx, int side)>();
                    foreach ((float bx, int side) c in candidates)
                    {
                        Rect r = BoothFootprint(c.bx, c.side);
                        if (Grow(aRect, 0.5f).Overlaps(r)) continue;
                        if (Vector2.Distance(aDoor.FieldLocal, BoothDoor(c.bx, c.side, TrialRequired).FieldLocal) < 2f * TimeField.FieldRadius + 0.5f) continue;
                        partners.Add(c);
                    }
                    if (partners.Count == 0) continue;
                    (float bx2, int bside) = partners[rng.NextInt(partners.Count)];
                    Rect bRect = BoothFootprint(bx2, bside);
                    var booths = new List<Rect> { aRect, bRect };
                    var marks = new List<ScreenMark>(own);
                    // Booths must not block any door's way from its switch.
                    bool blocksDoor = false;
                    foreach (TimedDoorSpec s in specs)
                        if (s.Screen == screen && s.Layout != null && s.Layout.HasSwitch &&
                            !PathClear(s.Layout.SwitchLocal, s.Layout.ThresholdLocal, ctx.Blocked, booths)) blocksDoor = true;
                    if (blocksDoor) continue;
                    marks.Add(new ScreenMark("trial booth", aRect.center, BoothOuter * 0.71f));
                    marks.Add(new ScreenMark("trial booth", bRect.center, BoothOuter * 0.71f));
                    DoorLayout a = WithSwitch(aDoor, ctx, marks, booths);
                    if (!a.HasSwitch) continue;
                    AddDoorMarks(marks, a, "trial booth");
                    DoorLayout b = WithSwitch(BoothDoor(bx2, bside, TrialRequired), ctx, marks, booths);
                    if (!b.HasSwitch) continue;
                    AddDoorMarks(marks, b, "trial booth");
                    var spec = new PhysicsTrialSpec(screen, new[] { new TrialBoothSpec(aRect, a), new TrialBoothSpec(bRect, b) },
                        null, TrialRequired);
                    if (!FitBoulders(screen, specs, spec, marks, out adjusted, out doorBoulders, out List<Vector2> trialBoulders)) continue;
                    bool costly = false;
                    for (int i = 0; i < adjusted.Count; i++)
                        if (adjusted[i].Required != baseline[i].Required || adjusted[i].Spare != baseline[i].Spare) costly = true;
                    if (costly) continue;
                    // Every door on this screen still has room near it with the booths there.
                    var grid = new ReachGrid(ctx.Blocked, booths);
                    bool roomy = true;
                    foreach (TimedDoorSpec s in adjusted)
                        if (s.Screen == screen && s.Layout != null && grid.Room(grid.ReachableTo(s.Layout), s.Layout) < s.Required) roomy = false;
                    foreach (TrialBoothSpec booth in spec.Booths)
                        if (grid.Room(grid.ReachableTo(booth.Layout), booth.Layout) < TrialRequired) roomy = false;
                    if (!roomy) continue;
                    return new PhysicsTrialSpec(screen, spec.Booths, trialBoulders, TrialRequired);
                }
                return null;
            }

            private static bool BoothFits(Rect rect, DoorLayout door, ScreenContext ctx, List<ScreenMark> own)
            {
                if (!ScreenModule.KeepsExitsOpen(rect)) return false;
                Rect grown = Grow(rect, 0.05f);
                foreach (Rect b in ctx.Blocked)
                    if (b.Overlaps(grown)) return false;
                foreach (ScreenMark m in ctx.Others)
                    if (DistanceToRect(m.Local, rect) < m.Radius + GapMargin) return false;
                foreach (ScreenMark m in own)
                    if (DistanceToRect(m.Local, rect) < m.Radius + GapMargin) return false;
                // The booth's threshold is in the lane: keep it off other interactables too.
                return Free(door.ThresholdLocal, TimeField.ThresholdRadius + TimeField.PlayerRadius, own);
            }

            /// <summary>
            /// Seeded boulder spots for every door on a screen (required + spare each) and, with a trial, its shared
            /// boulders: off the lanes, out of walls and booths, outside every time field, clear of interactables and
            /// apart, each reachable to its door (both booths for the trial's). False if any falls short.
            /// </summary>
            private bool PlaceBoulders(ScreenAddress screen, List<TimedDoorSpec> specs, PhysicsTrialSpec trial,
                List<ScreenMark> own, SeededRng rng, out Dictionary<int, List<Vector2>> doorBoulders, out List<Vector2> trialBoulders)
            {
                ScreenContext ctx = Ctx(screen);
                doorBoulders = new Dictionary<int, List<Vector2>>();
                trialBoulders = new List<Vector2>();
                var booths = new List<Rect>();
                var doorCentres = new List<Vector2>();
                if (trial != null)
                    foreach (TrialBoothSpec b in trial.Booths)
                    {
                        booths.Add(b.Footprint);
                        doorCentres.Add(b.Layout.FieldLocal);
                    }
                for (int i = 0; i < specs.Count; i++)
                    if (specs[i].Screen == screen && specs[i].Layout != null) doorCentres.Add(specs[i].Layout.FieldLocal);

                var grid = new ReachGrid(ctx.Blocked, booths);
                var candidates = new List<Vector2>();
                Vector2 half = Game.Tracer.ScreenMath.DefaultScreenSize / 2f;
                for (float y = -half.y; y <= half.y + 1e-4f; y += GridStep)
                {
                    for (float x = -half.x; x <= half.x + 1e-4f; x += GridStep)
                    {
                        var c = new Vector2(x, y);
                        if (!ScreenModule.KeepsExitsOpen(Square(c, BoulderHalf))) continue;
                        if (DiscHits(c, BoulderHalf, ctx.Blocked) || DiscHits(c, BoulderHalf, booths)) continue;
                        bool near = false;
                        foreach (Vector2 d in doorCentres)
                            if (Vector2.Distance(c, d) < TimeField.FieldRadius + FieldClearance) near = true;
                        if (near || !Free(c, Boulder.Radius, ctx.Others) || !Free(c, Boulder.Radius, own)) continue;
                        candidates.Add(c);
                    }
                }

                var chosen = new List<Vector2>();
                bool ok = true;
                List<Vector2> Pick(int count, List<bool[]> reaches)
                {
                    var free = new List<Vector2>();
                    foreach (Vector2 c in candidates)
                    {
                        bool spaced = true;
                        foreach (Vector2 b in chosen)
                            if (Vector2.Distance(b, c) < BoulderSpacing) spaced = false;
                        if (!spaced) continue;
                        bool reaches1 = true;
                        foreach (bool[] r in reaches)
                            if (!grid.Reaches(r, c)) reaches1 = false;
                        if (reaches1) free.Add(c);
                    }
                    var picked = new List<Vector2>();
                    while (picked.Count < count && free.Count > 0)
                    {
                        Vector2 p = free[rng.NextInt(free.Count)];
                        picked.Add(p);
                        chosen.Add(p);
                        free.RemoveAll(c => Vector2.Distance(c, p) < BoulderSpacing);
                    }
                    if (picked.Count < count) ok = false;
                    return picked;
                }

                for (int i = 0; i < specs.Count; i++)
                {
                    TimedDoorSpec s = specs[i];
                    if (s.Screen != screen || s.Layout == null) continue;
                    doorBoulders[i] = Pick(s.Required + s.Spare, new List<bool[]> { grid.ReachableTo(s.Layout) });
                }
                if (trial != null)
                {
                    var reaches = new List<bool[]>();
                    foreach (TrialBoothSpec b in trial.Booths) reaches.Add(grid.ReachableTo(b.Layout));
                    trialBoulders = Pick(trial.Required, reaches);
                }
                return ok;
            }

            /// <summary>
            /// Spots other faces' content uses on each screen (local): Biology buttons (required and optional),
            /// flowers and trial pieces, Chemistry lead plates and trial pieces, and open pickups (the isotope
            /// dispenser included).
            /// </summary>
            private static Dictionary<ScreenAddress, List<ScreenMark>> OtherFaceMarks(CubeModel model, ItemPlacement placement,
                Func<ScreenAddress, ScreenModule> moduleAt, IReadOnlyList<BeakKind> kinds)
            {
                var marks = new Dictionary<ScreenAddress, List<ScreenMark>>();
                void Add(ScreenAddress s, string what, Vector2 at, float r)
                {
                    if (!marks.TryGetValue(s, out var list)) marks[s] = list = new List<ScreenMark>();
                    list.Add(new ScreenMark(what, at, r));
                }
                Vector2? SlotLocal(ScreenAddress s, int slot)
                {
                    ScreenModule m = moduleAt(s);
                    if (m == null || slot >= m.Gates.Length) return null;
                    return m.Gates[slot].transform.position - m.transform.position;
                }

                BiologyPlan biology = BiologyPlan.Create(model, placement, kinds);
                if (biology != null)
                {
                    foreach (BeakGateSpec s in biology.Gates)
                    {
                        if (s.Kind == BeakGateKind.Button && SlotLocal(s.Screen, s.Slot) is Vector2 local)
                            Add(s.Screen, $"Biology button ({s})", ButtonGate.ButtonLocal(local), BeakButton.Radius);
                        if (s.Kind == BeakGateKind.FlowerVine)
                            Add(s.FlowerScreen, $"Biology flower ({s})", FlowerVineGate.FlowerLocal, PollinationFlower.Radius);
                    }
                    ScreenModule module = moduleAt(biology.TrialScreen);
                    if (module != null)
                    {
                        var rng = new SeededRng(unchecked((ulong)(uint)model.Seed), BiologyFace.PopulationRngStream);
                        List<Vector2> spots = BiologyTrial.PickSpots(BiologyTrial.LaneSideSpots(TownPlan.BlockedRects(module)),
                            BiologyTrial.PiecesFor(biology.Beak), rng);
                        foreach (Vector2 p in spots) Add(biology.TrialScreen, "Biology trial piece", p, BiologyTrial.PieceBoundRadius);
                    }
                }

                ChemistryPlan chemistry = ChemistryPlan.Create(model, placement);
                if (chemistry != null)
                {
                    foreach (IsotopeGateSpec s in chemistry.Gates)
                        if (s.Stage == IsotopeStage.Lead && SlotLocal(s.Screen, s.Slot) is Vector2 local)
                            Add(s.Screen, $"lead plate ({s})", LeadPlateGate.PlateLocal(local), LeadPlate.Radius);
                    ScreenModule module = moduleAt(chemistry.TrialScreen);
                    if (module != null)
                        foreach (Vector2 p in ChemistryPlan.TrialSpots(model.Seed, module))
                            Add(chemistry.TrialScreen, "Chemistry trial piece", p, BiologyTrial.PieceBoundRadius);
                }

                foreach (PickupPlacement p in placement.Pickups)
                    if (!p.IsGuarded) Add(p.Screen, $"{p.Item} pickup", CubeWorld.OpenPickupOffset, ItemPickup.Radius);
                return marks;
            }
        }

        // ------------------------------------------------------------------ Reachability

        /// <summary>
        /// A grid over the area a boulder may move in (its bounds: the screen clear of the edge band), marking where a
        /// boulder fits (clear of walls, alcoves and booths), with a search for where one can be dragged to a door.
        /// </summary>
        private sealed class ReachGrid
        {
            private readonly int nx, ny;
            private readonly bool[] free;
            private readonly Vector2 min;

            public ReachGrid(List<Rect> blocked, List<Rect> booths)
            {
                Vector2 half = Game.Tracer.ScreenMath.DefaultScreenSize / 2f -
                               new Vector2(ScreenModule.EdgeClearance + Boulder.Radius, ScreenModule.EdgeClearance + Boulder.Radius);
                min = -half;
                nx = Mathf.FloorToInt(half.x * 2f / GridStep) + 1;
                ny = Mathf.FloorToInt(half.y * 2f / GridStep) + 1;
                free = new bool[nx * ny];
                float r = Boulder.Radius + 0.02f;
                for (int j = 0; j < ny; j++)
                    for (int i = 0; i < nx; i++)
                    {
                        Vector2 p = Pos(i, j);
                        free[j * nx + i] = !DiscHits(p, r, blocked) && (booths == null || !DiscHits(p, r, booths));
                    }
            }

            private Vector2 Pos(int i, int j) => min + new Vector2(i * GridStep, j * GridStep);

            private int Index(Vector2 p)
            {
                int i = Mathf.Clamp(Mathf.RoundToInt((p.x - min.x) / GridStep), 0, nx - 1);
                int j = Mathf.Clamp(Mathf.RoundToInt((p.y - min.y) / GridStep), 0, ny - 1);
                return j * nx + i;
            }

            /// <summary>Cells from which a boulder can be dragged into the door's field (near its centre).</summary>
            public bool[] ReachableTo(DoorLayout door)
            {
                var seen = new bool[free.Length];
                var queue = new Queue<int>();
                float near = TimeField.FieldRadius - NearMargin;
                for (int k = 0; k < free.Length; k++)
                {
                    if (!free[k]) continue;
                    if (Vector2.Distance(Pos(k % nx, k / nx), door.FieldLocal) > near) continue;
                    seen[k] = true;
                    queue.Enqueue(k);
                }
                while (queue.Count > 0)
                {
                    int k = queue.Dequeue();
                    int i = k % nx, j = k / nx;
                    void Visit(int a, int b)
                    {
                        if (a < 0 || b < 0 || a >= nx || b >= ny) return;
                        int q = b * nx + a;
                        if (seen[q] || !free[q]) return;
                        seen[q] = true;
                        queue.Enqueue(q);
                    }
                    Visit(i + 1, j);
                    Visit(i - 1, j);
                    Visit(i, j + 1);
                    Visit(i, j - 1);
                }
                return seen;
            }

            /// <summary>True when a boulder starting at p (local) can be dragged into the door's field.</summary>
            public bool Reaches(bool[] reach, Vector2 p)
            {
                int k = Index(p);
                return reach[k] && Vector2.Distance(Pos(k % nx, k / nx), p) <= GridStep;
            }

            /// <summary>
            /// How many boulders fit near a door (inside its field, reachable, apart, and off the way from its switch to
            /// its threshold, so they never block the run).
            /// </summary>
            public int Room(bool[] reach, DoorLayout door)
            {
                float near = TimeField.FieldRadius - NearMargin;
                var placed = new List<Vector2>();
                const float spacing = 2f * Boulder.Radius + 0.05f;
                for (int k = 0; k < free.Length; k++)
                {
                    if (!reach[k]) continue;
                    Vector2 p = Pos(k % nx, k / nx);
                    if (Vector2.Distance(p, door.FieldLocal) > near) continue;
                    if (door.HasSwitch && DistanceToSegment(p, door.SwitchLocal, door.ThresholdLocal) <
                        Boulder.Radius + TimeField.PlayerRadius + 0.1f) continue;
                    if (Vector2.Distance(p, door.ThresholdLocal) < Boulder.Radius + TimeField.PlayerRadius + 0.1f) continue;
                    bool clear = true;
                    foreach (Vector2 q in placed)
                        if (Vector2.Distance(p, q) < spacing) clear = false;
                    if (clear) placed.Add(p);
                }
                return placed.Count;
            }
        }
    }
}
