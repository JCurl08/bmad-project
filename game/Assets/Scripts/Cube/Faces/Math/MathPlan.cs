using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>What a Math gate is: a door opened by walking its bridge layout, or a gap the gap-crosser* spans.</summary>
    public enum MathGateKind { BridgeDoor = 0, Gap = 1 }

    /// <summary>One Math gate of the run: where it is, what kind, its difficulty and (with modules) its bridge layout.</summary>
    public sealed class MathGateSpec
    {
        public ScreenAddress Screen { get; }
        public int Slot { get; }
        public MathGateKind Kind { get; }

        /// <summary>True when the gate is on the Math face.</summary>
        public bool OnHome { get; }

        /// <summary>The number of Math gates on the Math face (the difficulty parameter).</summary>
        public int Difficulty { get; }

        /// <summary>Bridges the door's layout has (MathPlan.BridgesFor the difficulty); 0 for a gap gate.</summary>
        public int Bridges { get; }

        /// <summary>A bridge door's layout, local to its screen centre (null for a gap gate, without modules, or when it fits nowhere).</summary>
        public BridgeLayout Layout { get; }

        public MathGateSpec(ScreenAddress screen, int slot, MathGateKind kind, bool onHome, int difficulty, int bridges,
            BridgeLayout layout)
        {
            Screen = screen;
            Slot = slot;
            Kind = kind;
            OnHome = onHome;
            Difficulty = difficulty;
            Bridges = bridges;
            Layout = layout;
        }

        public MathGateSpec WithLayout(BridgeLayout layout) =>
            new MathGateSpec(Screen, Slot, Kind, OnHome, Difficulty, Bridges, layout);

        public override string ToString() =>
            $"{(Kind == MathGateKind.Gap ? "gap gate" : "bridge door")} at {Screen} #{Slot}{(OnHome ? "" : " (off-home)")}, difficulty {Difficulty}" +
            (Kind == MathGateKind.BridgeDoor ? $", {Bridges} bridges" : "");
    }

    /// <summary>
    /// The Math face of a run as pure data, from the run seed, the item placement and (for positions) the screen modules.
    /// Every off-home Math gate is a gap gate (opened by swinging the gap-crosser* at it), so the item always opens a gate
    /// on another face; the Math face's own gates are split between gap gates and bridge doors (a seeded mix with at least
    /// one of each when there are two or more). A bridge door opens when its island-and-bridge layout on the same screen is
    /// walked crossing every bridge exactly once; its bridge count is the difficulty parameter, scaled by the Math face's
    /// gate count (BridgesFor). The trial is a bigger layout (TrialBridges, more than any door's) on the first Math screen,
    /// in a seeded order, where it fits.
    /// Layouts keep off both centre lanes (so lane content of every face stays clear), clear of walls and alcoves, of
    /// other faces' interactables (Biology buttons and flowers, Chemistry plates, open pickups, hidden-item and
    /// core-entrance slots) and of each other. FindProblems checks all of it for the seed sweep, solvability first.
    /// Uses its own SeededRng stream; each layout draws from that stream with its screen and slot in the seed.
    /// </summary>
    public sealed class MathPlan
    {
        /// <summary>PCG32 stream for the Math face plan (see RngStreams).</summary>
        public const ulong RngStream = RngStreams.MathPlan;

        /// <summary>Currency the Math trial pays out (fired with TrialRoom.Completed).</summary>
        public const int TrialCurrency = 25;

        /// <summary>
        /// The smallest stand-alone lattice that holds a door's bridges (3 x 2 islands, 7 links; BridgeLayout.Generate, for
        /// tools and tests). On the screen a layout grows over the free lattice spots of a quadrant instead (Place).
        /// </summary>
        public const int DoorCols = 3, DoorRows = 2;

        /// <summary>The smallest stand-alone lattice that holds the trial's bridges (4 x 2 islands, 10 links).</summary>
        public const int TrialCols = 4, TrialRows = 2;

        public const int MinDoorBridges = 4;
        public const int MaxDoorBridges = 7;

        /// <summary>The trial's bridges: more than any door's.</summary>
        public const int TrialBridges = 9;

        /// <summary>Math gates per extra bridge on a face of CubeSettings.TunedScreensPerFace screens (scaled per screen).</summary>
        public const int TunedGatesPerExtraBridge = 1;

        public static int GatesPerExtraBridge(int faceSize) => CubeSettings.PerScreen(TunedGatesPerExtraBridge, faceSize);

        /// <summary>
        /// Bridges a door's layout has on a face of faceSize x faceSize screens with difficulty Math gates on it: MinDoorBridges,
        /// plus one for every GatesPerExtraBridge gates beyond the first, at most MaxDoorBridges.
        /// </summary>
        public static int BridgesFor(int difficulty, int faceSize) =>
            Mathf.Clamp(MinDoorBridges + (Mathf.Max(1, difficulty) - 1) / GatesPerExtraBridge(faceSize), MinDoorBridges, MaxDoorBridges);

        // Layout geometry (local to the screen centre).
        /// <summary>Extra room between a layout (an island's standing radius, a plank's half width) and an exit lane.</summary>
        public const float LaneMargin = 0.1f;

        /// <summary>
        /// Island centres keep at least this far from the horizontal centre line: the island the player can stand on stays
        /// clear of the east-west exit lane (and its content) by LaneMargin.
        /// </summary>
        public const float LaneClearY = ScreenModule.ExitLaneHalfWidth + BridgeLayout.OnIslandRadius + LaneMargin;

        /// <summary>Island centres keep within these bounds (the islands stay on the screen, clear of its edges).</summary>
        public const float MaxX = 7.3f, MaxY = 4.35f;

        /// <summary>
        /// Island centres keep this far from the vertical centre line, the same way: a walk through the screen along either
        /// exit lane (and the entry band at the north and south exits) never touches a layout.
        /// </summary>
        public const float LaneClearX = ScreenModule.ExitLaneHalfWidth + BridgeLayout.OnIslandRadius + LaneMargin;

        /// <summary>Plank centre lines keep this far from both centre lines (the plank's standing width off the lanes).</summary>
        public const float BridgeLaneClear = ScreenModule.ExitLaneHalfWidth + BridgeLayout.OnBridgeHalfWidth + LaneMargin;

        /// <summary>Extra room between a layout and a wall or alcove.</summary>
        public const float WallMargin = 0.1f;

        /// <summary>Extra room between a layout and another face's interactable.</summary>
        public const float MarkMargin = 0.35f;

        /// <summary>Room between two layouts on a screen.</summary>
        public const float LayoutGap = 1.1f;

        private const float SampleStep = 0.25f;
        private const float GridStep = 0.25f;
        private const int LayoutTries = 6;

        private readonly List<MathGateSpec> gates;
        private readonly Dictionary<ScreenAddress, List<ScreenMark>> marks;
        private readonly Func<ScreenAddress, ScreenModule> moduleAt;

        public int Seed { get; }
        public FaceId Face { get; }
        public int FaceSize { get; }
        public IReadOnlyList<MathGateSpec> Gates => gates;

        /// <summary>The Math screen that holds the trial.</summary>
        public ScreenAddress TrialScreen { get; }

        /// <summary>The trial's layout, local to its screen centre; null without modules, or when it fits nowhere.</summary>
        public BridgeLayout TrialLayout { get; }

        /// <summary>True when the plan was made with modules (so every layout is placed).</summary>
        public bool HasLayout => moduleAt != null;

        private MathPlan(int seed, FaceId face, int faceSize, List<MathGateSpec> gates, ScreenAddress trialScreen,
            BridgeLayout trialLayout, Func<ScreenAddress, ScreenModule> moduleAt, Dictionary<ScreenAddress, List<ScreenMark>> marks)
        {
            Seed = seed;
            Face = face;
            FaceSize = faceSize;
            this.gates = gates;
            TrialScreen = trialScreen;
            TrialLayout = trialLayout;
            this.moduleAt = moduleAt;
            this.marks = marks;
        }

        /// <summary>A copy with one gate replaced (tools and tests of the sweep rules).</summary>
        public MathPlan WithGate(int index, MathGateSpec gate)
        {
            var list = new List<MathGateSpec>(gates) { [index] = gate };
            return new MathPlan(Seed, Face, FaceSize, list, TrialScreen, TrialLayout, moduleAt, marks);
        }

        /// <summary>A copy with another trial layout (tools and tests of the sweep rules).</summary>
        public MathPlan WithTrial(BridgeLayout trial) =>
            new MathPlan(Seed, Face, FaceSize, gates, TrialScreen, trial, moduleAt, marks);

        public bool TryGetGate(ScreenAddress screen, int slot, out MathGateSpec spec)
        {
            foreach (MathGateSpec s in gates)
            {
                if (s.Screen != screen || s.Slot != slot) continue;
                spec = s;
                return true;
            }
            spec = null;
            return false;
        }

        /// <summary>Every placed layout on a screen (bridge doors' and the trial's).</summary>
        public List<BridgeLayout> LayoutsOn(ScreenAddress screen)
        {
            var list = new List<BridgeLayout>();
            foreach (MathGateSpec s in gates)
                if (s.Screen == screen && s.Layout != null) list.Add(s.Layout);
            if (TrialLayout != null && TrialScreen == screen) list.Add(TrialLayout);
            return list;
        }

        /// <summary>True when a point (local to the screen centre) is within margin of a layout's island or bridge there.</summary>
        public bool NearLayout(ScreenAddress screen, Vector2 local, float margin)
        {
            foreach (BridgeLayout l in LayoutsOn(screen))
                if (Clearance(l, local) < margin) return true;
            return false;
        }

        /// <summary>Distance from a point to a layout's nearest island edge or bridge centre line.</summary>
        public static float Clearance(BridgeLayout layout, Vector2 p)
        {
            float best = float.MaxValue;
            foreach (Vector2 i in layout.Islands) best = Mathf.Min(best, Vector2.Distance(p, i) - BridgeLayout.IslandRadius);
            for (int b = 0; b < layout.BridgeCount; b++)
            {
                layout.Ends(b, out Vector2 a, out Vector2 c);
                best = Mathf.Min(best, BridgeLayout.DistanceToSegment(p, a, c) - BridgeLayout.BridgeWidth / 2f);
            }
            return best;
        }

        /// <summary>Number of Math gates on the Math face (the difficulty).</summary>
        public int HomeGateCount
        {
            get
            {
                int n = 0;
                foreach (MathGateSpec s in gates)
                    if (s.OnHome) n++;
                return n;
            }
        }

        public string Signature()
        {
            var sb = new StringBuilder();
            sb.Append("trial@").Append(TrialScreen).Append(' ');
            if (TrialLayout != null) sb.Append('[').Append(TrialLayout.Signature()).Append("] ");
            foreach (MathGateSpec s in gates)
            {
                sb.Append(s);
                if (s.Layout != null) sb.Append(" [").Append(s.Layout.Signature()).Append(']');
                sb.Append("; ");
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ Create

        /// <summary>
        /// The plan of a run; null when Math is not an item of the placement (no Math pickup). With moduleAt (prefabs in the
        /// sweep, instances at runtime) every layout is placed; pass the Biology beak kinds and the Chemistry stone kinds so
        /// their buttons, flowers and plates are known (null: none).
        /// </summary>
        public static MathPlan Create(CubeModel model, ItemPlacement placement, Func<ScreenAddress, ScreenModule> moduleAt = null,
            IReadOnlyList<BeakKind> biologyVariantKinds = null, IReadOnlyList<IsotopeStage> chemistryVariantKinds = null)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (placement == null || !placement.TryGetPickup(Theme.Math, out _)) return null;
            FaceId face = model.FaceOf(Theme.Math);
            int n = model.FaceSize;
            var rng = new SeededRng(unchecked((ulong)(uint)model.Seed), RngStream);

            var mathGates = new List<GatePlacement>();
            foreach (GatePlacement g in placement.Gates)
                if (g.Item == Theme.Math) mathGates.Add(g);
            var home = new List<int>();
            for (int i = 0; i < mathGates.Count; i++)
                if (mathGates[i].Screen.Face == face) home.Add(i);

            // Kinds: off-home gates are gaps; on Math, a seeded mix with one of each first.
            var kinds = new MathGateKind[mathGates.Count];
            for (int i = 0; i < kinds.Length; i++) kinds[i] = MathGateKind.Gap;
            var order = new List<int>(home);
            rng.Shuffle(order);
            for (int k = 0; k < order.Count; k++)
            {
                MathGateKind kind = k == 0 ? MathGateKind.Gap : k == 1 ? MathGateKind.BridgeDoor
                    : rng.NextInt(2) == 0 ? MathGateKind.BridgeDoor : MathGateKind.Gap;
                kinds[order[k]] = kind;
            }

            int difficulty = home.Count;
            int bridges = BridgesFor(difficulty, n);
            var specs = new List<MathGateSpec>();
            for (int i = 0; i < mathGates.Count; i++)
            {
                GatePlacement g = mathGates[i];
                bool onHome = g.Screen.Face == face;
                specs.Add(new MathGateSpec(g.Screen, g.Slot, kinds[i], onHome, difficulty,
                    kinds[i] == MathGateKind.BridgeDoor ? bridges : 0, null));
            }

            var trialOrder = new List<ScreenAddress>();
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    trialOrder.Add(new ScreenAddress(face, x, y));
            rng.Shuffle(trialOrder);

            if (moduleAt == null)
                return new MathPlan(model.Seed, face, n, specs, trialOrder[0], null, null, null);

            Dictionary<ScreenAddress, List<ScreenMark>> others = OtherFaceMarks(model, placement, moduleAt, biologyVariantKinds,
                chemistryVariantKinds);
            var placed = new Dictionary<ScreenAddress, List<BridgeLayout>>();
            List<BridgeLayout> Placed(ScreenAddress s) => placed.TryGetValue(s, out var l) ? l : placed[s] = new List<BridgeLayout>();

            for (int i = 0; i < specs.Count; i++)
            {
                MathGateSpec s = specs[i];
                if (s.Kind != MathGateKind.BridgeDoor) continue;
                ScreenModule module = moduleAt(s.Screen);
                if (module == null || s.Slot >= module.Gates.Length) continue;
                Vector2 door = module.Gates[s.Slot].transform.position - module.transform.position;
                var layoutRng = new SeededRng(ScreenSeed(model.Seed, s.Screen, s.Slot + 1), RngStream);
                BridgeLayout layout = Place(s.Bridges, layoutRng, module, Marks(others, s.Screen, module), Placed(s.Screen), door);
                if (layout == null) continue;
                specs[i] = s.WithLayout(layout);
                Placed(s.Screen).Add(layout);
            }

            ScreenAddress trialScreen = trialOrder[0];
            BridgeLayout trial = null;
            foreach (ScreenAddress t in trialOrder)
            {
                ScreenModule module = moduleAt(t);
                if (module == null) continue;
                var layoutRng = new SeededRng(ScreenSeed(model.Seed, t, 99), RngStream);
                BridgeLayout layout = Place(TrialBridges, layoutRng, module, Marks(others, t, module), Placed(t), null);
                if (layout == null) continue;
                trial = layout;
                trialScreen = t;
                break;
            }
            return new MathPlan(model.Seed, face, n, specs, trialScreen, trial, moduleAt, others);
        }

        private static ulong ScreenSeed(int seed, ScreenAddress s, int use) =>
            ((ulong)(uint)seed << 32) | (uint)(((int)s.Face * 64 + s.Cell.x * 8 + s.Cell.y) | (use << 16));

        /// <summary>A screen's marks: other faces' interactables plus its module's hidden-item and core-entrance slots.</summary>
        private static List<ScreenMark> Marks(Dictionary<ScreenAddress, List<ScreenMark>> others, ScreenAddress screen, ScreenModule module)
        {
            var list = others.TryGetValue(screen, out List<ScreenMark> o) ? new List<ScreenMark>(o) : new List<ScreenMark>();
            if (module == null) return list;
            Vector2 origin = module.transform.position;
            foreach (HiddenItemSlot h in module.HiddenItems)
                list.Add(new ScreenMark("hidden-item slot", (Vector2)h.transform.position - origin, 0.45f));
            foreach (CoreEntranceSlot c in module.CoreEntrances)
                list.Add(new ScreenMark("core-entrance slot", (Vector2)c.transform.position - origin, 0.6f));
            return list;
        }

        /// <summary>
        /// A layout grown on a screen where it fits: a lattice (the roomy spacing first, then the compact one) laid over one
        /// quadrant of the screen (off both exit lanes) at a seeded offset, its free spots (islands clear of walls, alcoves,
        /// interactables and other layouts) and free links (planks clear the same way) kept, and a seeded trail of exactly
        /// bridgeCount links walked over them (BridgeLayout.GenerateOn), so the layout bends round whatever stands in the
        /// quadrant. The door's quadrant is tried first. FitProblems has the last word. Null when it fits nowhere.
        /// </summary>
        private static BridgeLayout Place(int bridgeCount, SeededRng rng, ScreenModule module, List<ScreenMark> screenMarks,
            List<BridgeLayout> placed, Vector2? door)
        {
            List<Rect> blocked = TownPlan.BlockedRects(module);
            var placedSamples = new List<List<Vector2>>();
            foreach (BridgeLayout o in placed) placedSamples.Add(o.BridgeSamples(SampleStep));

            var anchors = new List<(int order, int sx, int sy, float spacing, float ox, float oy)>();
            float[] spacings = { BridgeLayout.Spacing, BridgeLayout.CompactSpacing };
            for (int k = 0; k < spacings.Length; k++)
                foreach (int sx in new[] { 1, -1 })
                    foreach (int sy in new[] { 1, -1 })
                    {
                        bool doorSide = door.HasValue && Mathf.Sign(door.Value.x) == sx && Mathf.Sign(door.Value.y) == sy;
                        for (float ox = 0f; ox < spacings[k] - 1e-4f; ox += GridStep)
                            for (float oy = 0f; oy < spacings[k] - 1e-4f; oy += GridStep)
                                anchors.Add((k * 2 + (doorSide ? 0 : 1), sx, sy, spacings[k], ox, oy));
                    }
            rng.Shuffle(anchors);
            anchors = StableByOrder(anchors);

            foreach (var a in anchors)
            {
                var points = new List<Vector2>();
                var index = new Dictionary<Vector2Int, int>();
                for (int i = 0; LaneClearX + a.ox + i * a.spacing <= MaxX + 1e-4f; i++)
                    for (int j = 0; LaneClearY + a.oy + j * a.spacing <= MaxY + 1e-4f; j++)
                    {
                        var p = new Vector2(a.sx * (LaneClearX + a.ox + i * a.spacing), a.sy * (LaneClearY + a.oy + j * a.spacing));
                        if (!SpotClear(p, blocked, screenMarks, placed)) continue;
                        index[new Vector2Int(i, j)] = points.Count;
                        points.Add(p);
                    }
                var links = new List<Vector2Int>();
                foreach (KeyValuePair<Vector2Int, int> e in index)
                    foreach (Vector2Int step in new[] { Vector2Int.right, Vector2Int.up })
                        if (index.TryGetValue(e.Key + step, out int other) && PlankClear(points[e.Value], points[other], blocked, screenMarks, placed, placedSamples))
                            links.Add(new Vector2Int(e.Value, other));
                if (links.Count < bridgeCount) continue;
                BridgeLayout layout = BridgeLayout.GenerateOn(points, links, bridgeCount, rng);
                if (layout != null && FitProblems(layout, blocked, screenMarks, placed, true).Count == 0) return layout;
            }
            return null;
        }

        private static List<(int order, int sx, int sy, float spacing, float ox, float oy)> StableByOrder(
            List<(int order, int sx, int sy, float spacing, float ox, float oy)> list)
        {
            var result = new List<(int, int, int, float, float, float)>();
            int max = 0;
            foreach (var e in list) max = Mathf.Max(max, e.order);
            for (int o = 0; o <= max; o++)
                foreach (var e in list)
                    if (e.order == o) result.Add(e);
            return result;
        }

        /// <summary>An island spot is free: clear of walls and alcoves, of interactables, and of every other layout.</summary>
        private static bool SpotClear(Vector2 p, List<Rect> blocked, IReadOnlyList<ScreenMark> screenMarks, List<BridgeLayout> placed)
        {
            float walk = TimeField.PlayerRadius + WallMargin;
            float islandWall = Mathf.Max(BridgeLayout.IslandRadius + WallMargin, walk);
            foreach (Rect r in blocked)
                if (DistanceToRect(p, r) < islandWall) return false;
            if (screenMarks != null)
                foreach (ScreenMark m in screenMarks)
                    if (Vector2.Distance(p, m.Local) - BridgeLayout.IslandRadius < m.Radius + MarkMargin) return false;
            foreach (BridgeLayout o in placed)
                if (Clearance(o, p) - BridgeLayout.IslandRadius < LayoutGap) return false;
            return true;
        }

        /// <summary>A plank between two free spots is free: every point along it clear of walls, interactables and other layouts.</summary>
        private static bool PlankClear(Vector2 a, Vector2 b, List<Rect> blocked, IReadOnlyList<ScreenMark> screenMarks,
            List<BridgeLayout> placed, List<List<Vector2>> placedSamples)
        {
            float walk = TimeField.PlayerRadius + WallMargin;
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / SampleStep));
            for (int k = 0; k <= n; k++)
            {
                Vector2 q = Vector2.Lerp(a, b, k / (float)n);
                foreach (Rect r in blocked)
                    if (DistanceToRect(q, r) < walk) return false;
                if (screenMarks != null)
                    foreach (ScreenMark m in screenMarks)
                        if (Vector2.Distance(q, m.Local) - BridgeLayout.BridgeWidth / 2f < m.Radius + MarkMargin) return false;
                foreach (BridgeLayout o in placed)
                    if (Clearance(o, q) - BridgeLayout.BridgeWidth / 2f < LayoutGap) return false;
            }
            return true;
        }

        /// <summary>
        /// Why a placed layout does not fit its screen: an island off the screen or in a centre lane, an island or bridge
        /// in a wall or alcove (the player walks them), on another face's interactable, or too near another layout.
        /// </summary>
        public static List<string> FitProblems(BridgeLayout layout, List<Rect> blocked, IReadOnlyList<ScreenMark> screenMarks,
            IReadOnlyList<BridgeLayout> others, bool firstOnly = false)
        {
            var problems = new List<string>();
            foreach (Vector2 p in layout.Islands)
            {
                if (Mathf.Abs(p.x) > MaxX + 1e-4f || Mathf.Abs(p.y) > MaxY + 1e-4f) problems.Add($"island at {p} is off the screen");
                if (Mathf.Abs(p.y) < LaneClearY - 1e-4f) problems.Add($"island at {p} is in the east-west lane");
                if (Mathf.Abs(p.x) < LaneClearX - 1e-4f) problems.Add($"island at {p} is in the north-south lane");
                if (firstOnly && problems.Count > 0) return problems;
            }
            foreach (Vector2 p in layout.BridgeSamples(SampleStep))
                if (Mathf.Abs(p.x) < BridgeLaneClear - 1e-4f || Mathf.Abs(p.y) < BridgeLaneClear - 1e-4f)
                {
                    problems.Add($"a bridge at {p} runs into an exit lane");
                    if (firstOnly) return problems;
                    break;
                }
            if (blocked == null) blocked = new List<Rect>();
            // Walls: the player (TimeField.PlayerRadius) walks the islands and bridges.
            float walk = TimeField.PlayerRadius + WallMargin;
            List<Vector2> samples = layout.BridgeSamples(SampleStep);
            foreach (Rect r in blocked)
            {
                foreach (Vector2 p in layout.Islands)
                    if (DistanceToRect(p, r) < Mathf.Max(BridgeLayout.IslandRadius + WallMargin, walk) - 1e-4f) problems.Add($"island at {p} is in a wall");
                foreach (Vector2 p in samples)
                    if (DistanceToRect(p, r) < walk - 1e-4f)
                    {
                        problems.Add($"a bridge at {p} runs into a wall");
                        break;
                    }
                if (firstOnly && problems.Count > 0) return problems;
            }
            if (screenMarks != null)
                foreach (ScreenMark m in screenMarks)
                {
                    if (Clearance(layout, m.Local) < m.Radius + MarkMargin - 1e-4f) problems.Add($"the layout sits on {m.What}");
                    if (firstOnly && problems.Count > 0) return problems;
                }
            if (others != null)
                foreach (BridgeLayout o in others)
                {
                    if (o == layout) continue;
                    float gap = float.MaxValue;
                    foreach (Vector2 p in o.Islands) gap = Mathf.Min(gap, Clearance(layout, p) - BridgeLayout.IslandRadius);
                    foreach (Vector2 p in o.BridgeSamples(SampleStep)) gap = Mathf.Min(gap, Clearance(layout, p) - BridgeLayout.BridgeWidth / 2f);
                    if (gap < LayoutGap - 1e-4f) problems.Add("two layouts on the screen run together");
                    if (firstOnly && problems.Count > 0) return problems;
                }
            return problems;
        }

        private static float DistanceToRect(Vector2 p, Rect r)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
            float dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Spots other faces' content uses on each screen (local): Biology buttons and flowers, Chemistry lead plates, Earth
        /// gates' fronts and open pickups (Physics avoids Math's layouts instead, see PhysicsPlan).
        /// </summary>
        private static Dictionary<ScreenAddress, List<ScreenMark>> OtherFaceMarks(CubeModel model, ItemPlacement placement,
            Func<ScreenAddress, ScreenModule> moduleAt, IReadOnlyList<BeakKind> kinds, IReadOnlyList<IsotopeStage> stoneKinds)
        {
            var result = new Dictionary<ScreenAddress, List<ScreenMark>>();
            void Add(ScreenAddress s, string what, Vector2 at, float r)
            {
                if (!result.TryGetValue(s, out var list)) result[s] = list = new List<ScreenMark>();
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
                foreach (BeakGateSpec s in biology.Gates)
                {
                    if (s.Kind == BeakGateKind.Button && SlotLocal(s.Screen, s.Slot) is Vector2 local)
                        Add(s.Screen, $"Biology button ({s})", ButtonGate.ButtonLocal(local), BeakButton.Radius);
                    if (s.Kind == BeakGateKind.FlowerVine)
                        Add(s.FlowerScreen, $"Biology flower ({s})", FlowerVineGate.FlowerLocal, PollinationFlower.Radius);
                }
            ChemistryPlan chemistry = ChemistryPlan.Create(model, placement, stoneKinds);
            if (chemistry != null)
                foreach (IsotopeGateSpec s in chemistry.Gates)
                    if (s.Stage == IsotopeStage.Lead && SlotLocal(s.Screen, s.Slot) is Vector2 local)
                        Add(s.Screen, $"lead plate ({s})", LeadPlateGate.PlateLocal(local), LeadPlate.Radius);
            // Earth: the front of every Earth gate (so layouts and Math's own NPCs and enemies keep clear of them).
            foreach (KeyValuePair<ScreenAddress, List<ScreenMark>> e in EarthPlan.GateFrontMarks(placement, moduleAt))
                foreach (ScreenMark m in e.Value) Add(e.Key, m.What, m.Local, m.Radius);
            foreach (PickupPlacement p in placement.Pickups)
                if (!p.IsGuarded) Add(p.Screen, $"{p.Item} pickup", CubeWorld.OpenPickupOffset, ItemPickup.Radius);
            return result;
        }

        /// <summary>The marks a placed layout must keep clear of on a screen (for FindProblems and tests).</summary>
        public IReadOnlyList<ScreenMark> MarksOn(ScreenAddress screen) =>
            marks != null ? Marks(marks, screen, moduleAt(screen)) : (IReadOnlyList<ScreenMark>)Array.Empty<ScreenMark>();

        /// <summary>
        /// The marks other content must keep clear of for Math's content (PhysicsPlan's boulders and switches, Math's own
        /// NPCs and enemies): the front of every Math gate (gap gates and bridge doors) and every placed layout's islands and
        /// bridges, keyed by screen.
        /// </summary>
        public Dictionary<ScreenAddress, List<ScreenMark>> ContentMarks()
        {
            var result = new Dictionary<ScreenAddress, List<ScreenMark>>();
            void Add(ScreenAddress s, string what, Vector2 at, float r)
            {
                if (!result.TryGetValue(s, out var list)) result[s] = list = new List<ScreenMark>();
                list.Add(new ScreenMark(what, at, r));
            }
            foreach (MathGateSpec s in gates)
            {
                if (moduleAt != null)
                {
                    ScreenModule m = moduleAt(s.Screen);
                    if (m != null && s.Slot < m.Gates.Length)
                    {
                        GateSlot slot = m.Gates[s.Slot];
                        Vector2 local = slot.transform.position - m.transform.position;
                        Add(s.Screen, $"front of Math {s}", GapGate.FrontLocal(local, slot.PocketOffset), GapGate.FrontRadius);
                    }
                }
                if (s.Layout != null) AddLayout(s.Screen, $"Math bridge layout ({s})", s.Layout, Add);
            }
            if (TrialLayout != null) AddLayout(TrialScreen, "Math trial layout", TrialLayout, Add);
            return result;
        }

        private static void AddLayout(ScreenAddress screen, string what, BridgeLayout layout, Action<ScreenAddress, string, Vector2, float> add)
        {
            foreach (Vector2 p in layout.Islands) add(screen, what, p, BridgeLayout.IslandRadius);
            foreach (Vector2 p in layout.BridgeSamples(0.5f)) add(screen, what, p, BridgeLayout.BridgeWidth / 2f);
        }

        // ------------------------------------------------------------------ Problems

        /// <summary>
        /// Problems with a run's Math plan: every Math gate has a spec; off-home ones are gap gates and at least one exists
        /// (when another face is built); with two or more Math gates on Math, at least one gap gate and one bridge door
        /// there; difficulty is the Math face's gate count and each door has BridgesFor it; the trial is on Math. With
        /// moduleAt, the layouts too: each bridge door's and the trial's exist, are solvable from their flag (an Eulerian
        /// path from the entry), have the right size (the trial bigger than any door), and fit their screen. Last, the
        /// collection of every item with Math's real gates (bridge doors open by walking a solvable layout, gap gates need
        /// the gap-crosser*): nothing required sits behind an unsolvable door or a gap gate before the gap-crosser* is
        /// reachable. Empty when all is well (or when Math is not an item).
        /// </summary>
        public static List<string> FindProblems(CubeModel model, ItemPlacement placement, Func<ScreenAddress, ScreenModule> moduleAt = null,
            IReadOnlyList<BeakKind> biologyVariantKinds = null, IReadOnlyList<IsotopeStage> chemistryVariantKinds = null)
        {
            MathPlan plan = Create(model, placement, moduleAt, biologyVariantKinds, chemistryVariantKinds);
            return plan == null ? new List<string>() : plan.Problems(model, placement);
        }

        /// <summary>The rules of FindProblems for this plan.</summary>
        public List<string> Problems(CubeModel model, ItemPlacement placement)
        {
            var problems = new List<string>();
            int builtFaces = 0;
            for (int f = 0; f < CubeSettings.FaceCount; f++)
                if (CubeLayout.IsLaidOut(model, (FaceId)f)) builtFaces++;

            int count = 0;
            foreach (GatePlacement g in placement.Gates)
            {
                if (g.Item != Theme.Math) continue;
                count++;
                if (!TryGetGate(g.Screen, g.Slot, out _)) problems.Add($"{g} has no Math gate");
            }
            if (count != gates.Count) problems.Add($"{gates.Count} Math gates for {count} placed");

            int offHomeGaps = 0, homeGaps = 0, homeDoors = 0, homeCount = HomeGateCount;
            int bridges = BridgesFor(homeCount, FaceSize);
            foreach (MathGateSpec s in gates)
            {
                if (s.OnHome != (s.Screen.Face == Face)) problems.Add($"{s}: wrong home flag");
                if (!s.OnHome && s.Kind != MathGateKind.Gap) problems.Add($"{s}: off-home Math gates are gap gates");
                if (!s.OnHome && s.Kind == MathGateKind.Gap) offHomeGaps++;
                if (s.OnHome && s.Kind == MathGateKind.Gap) homeGaps++;
                if (s.OnHome && s.Kind == MathGateKind.BridgeDoor) homeDoors++;
                if (s.Difficulty != homeCount) problems.Add($"{s}: wrong difficulty (want {homeCount})");
                if (s.Kind == MathGateKind.BridgeDoor && s.Bridges != bridges) problems.Add($"{s}: wants {bridges} bridges");
            }
            if (offHomeGaps < 1 && builtFaces > 1) problems.Add("the gap-crosser* opens no gate on another face");
            if (homeCount >= 2 && (homeGaps < 1 || homeDoors < 1))
                problems.Add($"Math has {homeGaps} gap gates and {homeDoors} bridge doors (want one of each)");
            if (TrialScreen.Face != Face) problems.Add($"the trial is on {TrialScreen}, not on Math");

            if (HasLayout)
            {
                foreach (MathGateSpec s in gates)
                {
                    if (s.Kind != MathGateKind.BridgeDoor) continue;
                    if (s.Layout == null)
                    {
                        problems.Add($"{s}: no room for its bridge layout");
                        continue;
                    }
                    problems.AddRange(s.Layout.Problems(s.ToString()));
                    if (s.Layout.BridgeCount != s.Bridges) problems.Add($"{s}: its layout has {s.Layout.BridgeCount} bridges");
                    foreach (string p in FitProblems(s.Layout, TownPlan.BlockedRects(moduleAt(s.Screen)), MarksOn(s.Screen), LayoutsOn(s.Screen)))
                        problems.Add($"{s}: {p}");
                }
                if (TrialLayout == null) problems.Add("the trial fits on no Math screen");
                else
                {
                    problems.AddRange(TrialLayout.Problems($"the trial on {TrialScreen}"));
                    if (TrialLayout.BridgeCount <= MaxDoorBridges)
                        problems.Add($"the trial has {TrialLayout.BridgeCount} bridges, no more than a door's {MaxDoorBridges}");
                    foreach (string p in FitProblems(TrialLayout, TownPlan.BlockedRects(moduleAt(TrialScreen)), MarksOn(TrialScreen), LayoutsOn(TrialScreen)))
                        problems.Add($"the trial on {TrialScreen}: {p}");
                }
            }

            // Collection with Math's real gates.
            var held = new HashSet<Theme>();
            bool progress = true;
            while (progress)
            {
                progress = false;
                foreach (PickupPlacement p in placement.Pickups)
                {
                    if (held.Contains(p.Item)) continue;
                    if (p.IsGuarded && (!placement.TryGetGate(p.Screen, p.GuardSlot, out GatePlacement guard) || !Opens(guard, held)))
                        continue;
                    held.Add(p.Item);
                    progress = true;
                }
            }
            foreach (PickupPlacement p in placement.Pickups)
                if (!held.Contains(p.Item))
                    problems.Add($"softlock: the {p.Item} pickup at {p.Screen} sits behind a gate that never opens");
            return problems;
        }

        /// <summary>True when a gate opens for a player holding these items (Math gates by their real mechanic).</summary>
        private bool Opens(GatePlacement gate, HashSet<Theme> held)
        {
            if (gate.Item != Theme.Math) return held.Contains(gate.Item);
            if (!TryGetGate(gate.Screen, gate.Slot, out MathGateSpec spec)) return false;
            if (spec.Kind == MathGateKind.Gap) return held.Contains(Theme.Math);
            if (!HasLayout) return true; // no positions known: the door is assumed laid out
            return spec.Layout != null && spec.Layout.HasWalkFrom(spec.Layout.Entry);
        }
    }
}
