using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.Cube
{
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
    public sealed partial class PhysicsPlan
    {
        /// <summary>PCG32 stream for the Physics face plan (see RngStreams).</summary>
        public const ulong RngStream = RngStreams.PhysicsPlan;

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
    }
}
