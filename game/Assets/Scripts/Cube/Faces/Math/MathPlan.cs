using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>One Math gate of the run: a gap the gap-crosser* spans (every Math gate is one since 1.25).</summary>
    public sealed class MathGateSpec
    {
        public ScreenAddress Screen { get; }
        public int Slot { get; }

        /// <summary>True when the gate is on the Math face.</summary>
        public bool OnHome { get; }

        public MathGateSpec(ScreenAddress screen, int slot, bool onHome)
        {
            Screen = screen;
            Slot = slot;
            OnHome = onHome;
        }

        public override string ToString() => $"gap gate at {Screen} #{Slot}{(OnHome ? "" : " (off-home)")}";
    }

    /// <summary>One island screen of the Math face: where it is and its islands and planks (local to its centre).</summary>
    public sealed class IslandScreenSpec
    {
        public ScreenAddress Screen { get; }

        /// <summary>The islands and planks; null when no layout was found (FindProblems reports it).</summary>
        public IslandLayout Layout { get; }

        public IslandScreenSpec(ScreenAddress screen, IslandLayout layout)
        {
            Screen = screen;
            Layout = layout;
        }

        public bool HasTrial => Layout != null && Layout.HasTrial;

        public override string ToString() => $"island screen at {Screen}";
    }

    /// <summary>
    /// The Math face of a run as pure data, from the run seed, the item placement and (for the island screens) the screen
    /// modules. Every Math gate, on Math and off it, is a gap gate the gap-crosser* opens (so the item always opens a gate
    /// on another face). Since 1.25 the face's puzzle is its island screens (the screens CubeLayout gives the island
    /// module): each is solid water dotted with islands, every doorway landing on its own island, planks spread between
    /// some of them and some planks broken spans only the gap-crosser* crosses (IslandLayout). Their islands do not all
    /// join, so a doorway reaches only some of the screen and the player works out a route round through the other screens
    /// (DoorwayReach). The trial stands on an island of one island screen that only a broken span reaches; an open pickup
    /// on an island screen stands on its south doorway's island (CubeWorld.OpenPickupOffset).
    /// 4.12: each island screen is a "light every plank" puzzle (IslandLayout, IslandPlan): every doorway but one lands on
    /// its lit network, the other reaches it over a broken span, and lighting every network plank opens the reward roadblock.
    /// FindProblems checks all of it for the seed sweep, collecting every item doorway by doorway.
    /// Uses its own SeededRng stream for the trial's screen; the layouts are IslandPlan's (RngStreams.IslandPuzzles).
    /// </summary>
    public sealed class MathPlan
    {
        /// <summary>PCG32 stream for the Math face plan (see RngStreams).</summary>
        public const ulong RngStream = RngStreams.MathPlan;

        /// <summary>Currency the Math trial pays out (fired with TrialRoom.Completed).</summary>
        public const int TrialCurrency = 25;

        private readonly List<MathGateSpec> gates;
        private readonly List<IslandScreenSpec> islands;
        private readonly Func<ScreenAddress, ScreenModule> moduleAt;
        private readonly Dictionary<ScreenAddress, List<ScreenMark>> marks;

        public int Seed { get; }
        public FaceId Face { get; }
        public int FaceSize { get; }
        public IReadOnlyList<MathGateSpec> Gates => gates;

        /// <summary>The island screens, row by row (empty without modules).</summary>
        public IReadOnlyList<IslandScreenSpec> IslandScreens => islands;

        /// <summary>The island screen that holds the trial (default when there is none).</summary>
        public ScreenAddress TrialScreen
        {
            get
            {
                foreach (IslandScreenSpec s in islands)
                    if (s.HasTrial) return s.Screen;
                return default;
            }
        }

        public bool HasTrial
        {
            get
            {
                foreach (IslandScreenSpec s in islands)
                    if (s.HasTrial) return true;
                return false;
            }
        }

        /// <summary>True when the plan was made with modules (so the island screens are known).</summary>
        public bool HasLayout => moduleAt != null;

        private MathPlan(int seed, FaceId face, int faceSize, List<MathGateSpec> gates, List<IslandScreenSpec> islands,
            Func<ScreenAddress, ScreenModule> moduleAt, Dictionary<ScreenAddress, List<ScreenMark>> marks)
        {
            Seed = seed;
            Face = face;
            FaceSize = faceSize;
            this.gates = gates;
            this.islands = islands;
            this.moduleAt = moduleAt;
            this.marks = marks;
        }

        /// <summary>A copy with one island screen's layout replaced (tools and tests of the sweep rules).</summary>
        public MathPlan WithIslands(int index, IslandLayout layout)
        {
            var list = new List<IslandScreenSpec>(islands) { [index] = new IslandScreenSpec(islands[index].Screen, layout) };
            return new MathPlan(Seed, Face, FaceSize, gates, list, moduleAt, marks);
        }

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

        /// <summary>The island layout of a screen, or null for an ordinary screen (DoorwayReach's islandAt).</summary>
        public IslandLayout IslandAt(ScreenAddress screen)
        {
            foreach (IslandScreenSpec s in islands)
                if (s.Screen == screen) return s.Layout;
            return null;
        }

        /// <summary>True for an island screen (whether or not a layout was found for it).</summary>
        public bool IsIslandScreen(ScreenAddress screen)
        {
            foreach (IslandScreenSpec s in islands)
                if (s.Screen == screen) return true;
            return false;
        }

        /// <summary>The run's Math plan as MathFace makes it at the reveal: only with a rolled Math item, else null.</summary>
        public static MathPlan ForRun(CubeModel model, ItemPlacement placement, ItemDefinition gapCrosser,
            Func<ScreenAddress, ScreenModule> moduleAt, IReadOnlyList<BeakKind> biologyVariantKinds,
            IReadOnlyList<IsotopeStage> chemistryVariantKinds) =>
            gapCrosser != null ? Create(model, placement, moduleAt, biologyVariantKinds, chemistryVariantKinds) : null;

        /// <summary>The look (GateLooks) of the gate MathFace builds at a slot, or null where it builds none (optional gates never).</summary>
        public string GateLook(ScreenAddress screen, int slot, bool optional) =>
            !optional && TryGetGate(screen, slot, out _) ? GateLooks.Gap : null;

        /// <summary>Number of Math gates on the Math face.</summary>
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
            foreach (IslandScreenSpec s in islands)
            {
                sb.Append(s).Append(' ');
                sb.Append(s.Layout != null ? "[" + s.Layout.Signature() + "]" : "[none]").Append("; ");
            }
            foreach (MathGateSpec s in gates) sb.Append(s).Append("; ");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ Create

        /// <summary>
        /// The plan of a run; null when Math is not an item of the placement (no Math pickup). With moduleAt (prefabs in the
        /// sweep, instances at runtime) the island screens are found (the screens whose module is the island module) and laid
        /// out, and one of them, seeded, gets the trial. Pass the Biology beak kinds and the Chemistry stone kinds so their
        /// buttons, flowers and plates are known to Math's population (MarksOn; null: none).
        /// </summary>
        public static MathPlan Create(CubeModel model, ItemPlacement placement, Func<ScreenAddress, ScreenModule> moduleAt = null,
            IReadOnlyList<BeakKind> biologyVariantKinds = null, IReadOnlyList<IsotopeStage> chemistryVariantKinds = null, DieNumbers dice = null)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (placement == null || !placement.TryGetPickup(Theme.Math, out _)) return null;
            FaceId face = model.FaceOf(Theme.Math);
            int n = model.FaceSize;

            var specs = new List<MathGateSpec>();
            foreach (GatePlacement g in placement.Gates)
                if (g.Item == Theme.Math) specs.Add(new MathGateSpec(g.Screen, g.Slot, g.Screen.Face == face));

            var islandScreens = new List<ScreenAddress>();
            if (moduleAt != null)
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        var s = new ScreenAddress(face, x, y);
                        ScreenModule module = moduleAt(s);
                        if (module != null && module.IsIslandScreen) islandScreens.Add(s);
                    }
            // The trial's screen: the plan stream's first draw (as since 1.25). 4.12: each layout is IslandPlan's (its own stream,
            // Math's tier, cached), so the game, the sweep and the lock check see the same screens.
            int trial = IslandPlan.TrialIndex(model.Seed, islandScreens.Count);
            PuzzleTier tier = islandScreens.Count > 0 ? IslandPlan.TierOf(model, dice) : PuzzleTier.Early;
            var islands = new List<IslandScreenSpec>();
            for (int i = 0; i < islandScreens.Count; i++)
                islands.Add(new IslandScreenSpec(islandScreens[i], IslandPlan.LayoutFor(model, islandScreens[i], tier, i == trial)));
            Dictionary<ScreenAddress, List<ScreenMark>> others = moduleAt != null
                ? OtherFaceMarks(model, placement, moduleAt, biologyVariantKinds, chemistryVariantKinds)
                : null;
            return new MathPlan(model.Seed, face, n, specs, islands, moduleAt, others);
        }

        /// <summary>A screen's marks: other faces' interactables plus its module's hidden-item and core-entrance slots.</summary>
        private static List<ScreenMark> Marks(Dictionary<ScreenAddress, List<ScreenMark>> others, ScreenAddress screen, ScreenModule module)
        {
            var list = others.TryGetValue(screen, out List<ScreenMark> o) ? new List<ScreenMark>(o) : new List<ScreenMark>();
            if (module == null) return list;
            Vector2 origin = module.transform.position;
            foreach (HiddenItemSlot h in module.HiddenItems)
                list.Add(new ScreenMark("hidden-item slot", (Vector2)h.transform.position - origin, FaceContent.HiddenSlotRadius));
            foreach (CoreEntranceSlot c in module.CoreEntrances)
                list.Add(new ScreenMark("core-entrance slot", (Vector2)c.transform.position - origin, FaceContent.CoreSlotRadius));
            return list;
        }

        /// <summary>
        /// Spots other faces' content uses on each screen (local): Biology buttons and flowers, Chemistry lead plates, Earth
        /// gates' fronts and open pickups (Math's shape people and enemies keep off them).
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
            foreach (KeyValuePair<ScreenAddress, List<ScreenMark>> e in EarthPlan.GateFrontMarks(placement, moduleAt))
                foreach (ScreenMark m in e.Value) Add(e.Key, m.What, m.Local, m.Radius);
            foreach (PickupPlacement p in placement.Pickups)
                if (!p.IsGuarded) Add(p.Screen, $"{p.Item} pickup", CubeWorld.OpenPickupOffset, ItemPickup.Radius);
            return result;
        }

        /// <summary>The marks Math's population keeps clear of on a screen (other faces' interactables and the module's slots).</summary>
        public IReadOnlyList<ScreenMark> MarksOn(ScreenAddress screen) =>
            marks != null ? Marks(marks, screen, moduleAt(screen)) : (IReadOnlyList<ScreenMark>)Array.Empty<ScreenMark>();

        /// <summary>
        /// The marks other content must keep clear of for Math's content (PhysicsPlan's boulders and switches, Math's own
        /// NPCs and enemies): the front of every Math gate, keyed by screen. (Island screens hold no other face's content: the
        /// island module has no slots.)
        /// </summary>
        public Dictionary<ScreenAddress, List<ScreenMark>> ContentMarks()
        {
            var result = new Dictionary<ScreenAddress, List<ScreenMark>>();
            if (moduleAt == null) return result;
            foreach (MathGateSpec s in gates)
            {
                ScreenModule m = moduleAt(s.Screen);
                if (m == null || s.Slot >= m.Gates.Length) continue;
                GateSlot slot = m.Gates[s.Slot];
                Vector2 local = slot.transform.position - m.transform.position;
                if (!result.TryGetValue(s.Screen, out List<ScreenMark> list)) result[s.Screen] = list = new List<ScreenMark>();
                list.Add(new ScreenMark($"front of Math {s}", GapGate.FrontLocal(local, slot.PocketOffset), GapGate.FrontRadius));
            }
            return result;
        }

        // ------------------------------------------------------------------ Problems

        /// <summary>
        /// Problems with a run's Math plan: every Math gate has a spec and at least one is off the Math face (when another face
        /// is built). With moduleAt, the island screens too: the face has IslandScreensPerFace of them, each laid out
        /// (IslandLayout.Problems: every doorway on its own island, separate islands, planks shore to shore, a pair of
        /// doorways joined only over a broken span and a doorway joined to none), exactly one holds the trial, and the walk
        /// doorway by doorway (DoorwayReach.Problems: every ordinary screen reachable without the gap-crosser*, every island
        /// with it, every join two-way). Last, the collection of every item doorway by doorway (an open pickup counts where
        /// it stands: on an island screen, on its island), with Math's gap gates needing the gap-crosser*: nothing required
        /// sits where it can never be reached, and the trial island is reached once the gap-crosser* is. Empty when all is
        /// well (or when Math is not an item).
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

            int count = 0, offHome = 0;
            foreach (GatePlacement g in placement.Gates)
            {
                if (g.Item != Theme.Math) continue;
                count++;
                if (!TryGetGate(g.Screen, g.Slot, out _)) problems.Add($"{g} has no Math gate");
            }
            if (count != gates.Count) problems.Add($"{gates.Count} Math gates for {count} placed");
            foreach (MathGateSpec s in gates)
            {
                if (s.OnHome != (s.Screen.Face == Face)) problems.Add($"{s}: wrong home flag");
                if (!s.OnHome) offHome++;
                if (IsIslandScreen(s.Screen)) problems.Add($"{s}: a gate on an island screen");
            }
            if (offHome < 1 && builtFaces > 1) problems.Add("the gap-crosser* opens no gate on another face");

            var reach = new DoorwayReach(model, IslandAt);
            if (HasLayout)
            {
                int wanted = CubeLayout.IslandScreensPerFace(FaceSize);
                if (islands.Count != wanted) problems.Add($"Math has {islands.Count} island screens (want {wanted})");
                int trials = 0;
                foreach (IslandScreenSpec s in islands)
                {
                    if (s.Screen.Face != Face) problems.Add($"{s} is not on Math");
                    if (s.Layout == null)
                    {
                        problems.Add($"{s}: no island layout fits");
                        continue;
                    }
                    if (s.HasTrial) trials++;
                    if (!s.Layout.HasPuzzle) problems.Add($"{s}: no lit network");
                    ScreenAddress screen = s.Screen;
                    problems.AddRange(s.Layout.Problems(s.ToString(), side => model.TryStep(screen, side, out _, out _)));
                }
                if (islands.Count > 0 && trials != 1) problems.Add($"{trials} island screens hold the trial (want one)");
                problems.AddRange(reach.Problems());
            }

            // Collection, doorway by doorway, with Math's gap gates.
            var held = new HashSet<Theme>();
            bool progress = true;
            while (progress)
            {
                progress = false;
                foreach (PickupPlacement p in placement.Pickups)
                {
                    if (held.Contains(p.Item)) continue;
                    if (!reach.CanReach(p.Screen, PickupLocal(p), held.Contains(Theme.Math))) continue;
                    if (p.IsGuarded && (!placement.TryGetGate(p.Screen, p.GuardSlot, out GatePlacement guard) || !held.Contains(guard.Item)))
                        continue;
                    held.Add(p.Item);
                    progress = true;
                }
            }
            foreach (PickupPlacement p in placement.Pickups)
                if (!held.Contains(p.Item))
                    problems.Add($"softlock: the {p.Item} pickup at {p.Screen} can never be reached or sits behind a gate that never opens");
            if (HasTrial)
            {
                IslandLayout trial = IslandAt(TrialScreen);
                if (!held.Contains(Theme.Math) || !reach.CanReach(TrialScreen, trial.TrialSpot, true))
                    problems.Add($"the trial on {TrialScreen} can never be reached");
            }
            return problems;
        }

        /// <summary>Where a pickup stands on its screen (local): the open spot, or (guarded) the screen's centre region.</summary>
        public static Vector2 PickupLocal(PickupPlacement p) => p.IsGuarded ? Vector2.zero : CubeWorld.OpenPickupOffset;
    }
}
