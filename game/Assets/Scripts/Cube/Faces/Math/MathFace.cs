using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The Bridgekeeper*'s face, plugged into CubeWorld through IFaceContent (it sits on the CubeWorld's GameObject), in the
    /// same three parts as the other faces. On the reveal it builds the run's MathPlan (with the screens' modules, so every
    /// layout is placed), and then:
    /// gate behaviours: every gap gate (all off-home Math gates and some on Math) becomes a GapGate the gap-crosser*
    /// opens, and every bridge door a BridgeDoorGate with its island-and-bridge layout (BridgePuzzle) on its screen;
    /// the trial: a MathTrial (a bigger layout) on the plan's trial screen;
    /// the population: shape people (MathPopulation) and Math enemies (Dead Ends, a Loose End) on seeded clear spots
    /// (FaceSpots, stream PopulationRngStream), kept off the layouts, the fronts of Math's own gates, the core entrance and the other slots and interactables.
    /// Everything is removed on a rebuild.
    /// </summary>
    public class MathFace : MonoBehaviour, IFaceContent, IHintDensityTarget
    {
        /// <summary>PCG32 stream for Math population spots and screens (see RngStreams).</summary>
        public const ulong PopulationRngStream = RngStreams.MathPopulation;

        public const int TunedDeadEndCount = 2;
        public static int DeadEndCount(int faceSize) => CubeSettings.PerScreen(TunedDeadEndCount, faceSize);

        public const int TunedLooseEndCount = 1;
        public static int LooseEndCount(int faceSize) => CubeSettings.PerScreen(TunedLooseEndCount, faceSize);

        /// <summary>NPCs and enemies spawn at least this far from a layout (so they do not start on a bridge).</summary>
        public const float LayoutKeepOut = 0.9f;

        /// <summary>NPCs and enemies spawn at least this far beyond a slot or interactable (a portal, a hidden item, a plate).</summary>
        public const float SlotKeepOut = 1.2f;

        /// <summary>Flag colours of the bridge doors' layouts, in door order (a door and its layout share one).</summary>
        public static readonly Color[] FlagColors =
        {
            new Color(0.95f, 0.3f, 0.3f), new Color(0.3f, 0.6f, 1f), new Color(0.4f, 0.9f, 0.4f),
            new Color(0.95f, 0.5f, 0.95f), new Color(1f, 0.6f, 0.2f), new Color(0.3f, 0.9f, 0.9f),
        };

        [SerializeField] private Transform player;
        [SerializeField] private HintDensity hintDensity = HintGenerator.FirstRunDensity;
        [SerializeField] private bool spawnEnemies = true;

        private readonly List<BridgeDoorGate> doors = new List<BridgeDoorGate>();
        private readonly List<GapGate> gaps = new List<GapGate>();
        private readonly List<NpcTalker> shapes = new List<NpcTalker>();
        private readonly List<Enemy> enemies = new List<Enemy>();
        private Transform root;

        public Theme Theme => Theme.Math;

        /// <summary>This run's Math plan; null before the reveal (or when Math is not an item of the run).</summary>
        public MathPlan Plan { get; private set; }

        /// <summary>The run's gap-crosser* (the Math item ItemPlacement places).</summary>
        public ItemDefinition GapCrosser { get; private set; }

        public MathTrial Trial { get; private set; }

        public IReadOnlyList<BridgeDoorGate> Doors => doors;
        public IReadOnlyList<GapGate> Gaps => gaps;
        public IReadOnlyList<NpcTalker> Shapes => shapes;
        public IReadOnlyList<Enemy> Enemies => enemies;

        public Transform Player
        {
            get => player;
            set => player = value;
        }

        public HintDensity HintDensity
        {
            get => hintDensity;
            set => hintDensity = value;
        }

        public bool SpawnEnemies
        {
            get => spawnEnemies;
            set => spawnEnemies = value;
        }

        public void BeginReveal(CubeWorld world)
        {
            GapCrosser = world.ItemFor(Theme.Math);
            Plan = GapCrosser != null
                ? MathPlan.Create(world.Model, world.ItemPlacement, world.ModuleAt, BiologyBeaks.VariantKinds(world.ItemCatalog),
                    ChemistryIsotope.VariantKinds(world.ItemCatalog))
                : null;
        }

        public Gate CreateGate(CubeWorld world, FaceGateRequest request, GateSlot slot, ItemDefinition item)
        {
            if (Plan == null || request.Optional || !Plan.TryGetGate(request.Screen, request.Slot, out MathGateSpec spec)) return null;
            if (spec.Kind == MathGateKind.Gap)
            {
                GameObject gapBlock = world.CreateBlock("Gap Gate", slot.transform, slot.transform.position, CubeWorld.GateSize(slot),
                    Color.white, -4, true);
                GapGate gap = GapGate.Build(gapBlock, item, world.Material);
                gaps.Add(gap);
                return gap;
            }
            if (spec.Layout == null)
            {
                Debug.LogWarning($"MathFace: no layout for {spec} (seed {world.Seed}); a plain gate instead.");
                return null;
            }
            ScreenModule module = world.ModuleAt(request.Screen);
            Transform parent = module != null ? module.transform : slot.transform;
            Color flag = FlagColors[doors.Count % FlagColors.Length];
            BridgePuzzle puzzle = BridgePuzzle.Create($"Bridges ({spec.Screen} #{spec.Slot})", parent, spec.Layout,
                world.ScreenCenter(request.Screen), ResolvePlayer(), flag, world.Material);
            GameObject block = world.CreateBlock("Bridge Door", slot.transform, slot.transform.position, CubeWorld.GateSize(slot),
                Color.white, -4, true);
            BridgeDoorGate door = BridgeDoorGate.Build(block, item, puzzle, spec.Difficulty, world.Material);
            doors.Add(door);
            return door;
        }

        public void EndReveal(CubeWorld world)
        {
            if (Plan == null) return;
            if (root == null) root = new GameObject("Math Face Content").transform;
            var rng = new SeededRng(unchecked((ulong)(uint)world.Seed), PopulationRngStream);
            BuildTrial(world);
            MathPlan plan = Plan;
            var marks = new Dictionary<ScreenAddress, IReadOnlyList<ScreenMark>>();
            // Math's own content: the fronts of its gap gates and bridge doors (and its layouts, kept off above too).
            Dictionary<ScreenAddress, List<ScreenMark>> own = plan.ContentMarks();
            bool Allowed(ScreenAddress screen, Vector2 feet)
            {
                if (plan.NearLayout(screen, feet, LayoutKeepOut)) return false;
                if (!marks.TryGetValue(screen, out IReadOnlyList<ScreenMark> list))
                {
                    var all = new List<ScreenMark>(plan.MarksOn(screen));
                    if (own.TryGetValue(screen, out List<ScreenMark> mine)) all.AddRange(mine);
                    marks[screen] = list = all;
                }
                Vector2 body = feet + new Vector2(0f, NpcFactory.MaxBodyHeight / 2f);
                foreach (ScreenMark m in list)
                    if (Vector2.Distance(feet, m.Local) < m.Radius + SlotKeepOut || Vector2.Distance(body, m.Local) < m.Radius + SlotKeepOut)
                        return false;
                return true;
            }
            // Every gate front and slot too (other faces' gates on Math included).
            System.Func<ScreenAddress, Vector2, bool> clearOfSlots = FaceContent.ClearOfSlots(world);
            var spots = new FaceSpots(world, rng, (screen, feet) => Allowed(screen, feet) && clearOfSlots(screen, feet));
            SpawnShapes(world, spots, rng);
            if (spawnEnemies) SpawnMathEnemies(world, spots, rng);
        }

        public void Clear()
        {
            FaceContent.DestroyAll(shapes);
            FaceContent.DestroyAll(enemies);
            // Doors, gaps, layouts and the trial live under their modules, which the rebuild destroys.
            doors.Clear();
            gaps.Clear();
            Trial = null;
            Plan = null;
            GapCrosser = null;
        }

        private void BuildTrial(CubeWorld world)
        {
            ScreenModule module = world.ModuleAt(Plan.TrialScreen);
            if (module == null || Plan.TrialLayout == null)
            {
                Debug.LogWarning($"MathFace: no room for the trial on {Plan.TrialScreen} (seed {world.Seed}).");
                return;
            }
            var go = new GameObject("Math Trial");
            go.transform.SetParent(module.transform, false);
            Vector2 centre = world.ScreenCenter(Plan.TrialScreen);
            go.transform.position = centre;
            Trial = go.AddComponent<MathTrial>();
            Trial.Configure(Plan.TrialLayout, centre, ResolvePlayer(), MathPlan.TrialCurrency, world.Material);
            Trial.Completed += currency => Debug.Log($"Math trial complete: +{currency} (seed {world.Seed}).");
        }

        private List<ScreenAddress> ShuffledScreens(SeededRng rng) => FaceContent.ShuffledScreens(Plan.Face, Plan.FaceSize, rng);

        private void SpawnShapes(CubeWorld world, FaceSpots spots, SeededRng rng)
        {
            List<ScreenAddress> screens = ShuffledScreens(rng);
            RunFacts facts = RunFacts.Of(world);
            Transform target = ResolvePlayer();
            for (int i = 0; i < MathPopulation.ShapeCount(Plan.FaceSize); i++)
            {
                NpcSpec spec = MathPopulation.ShapeSpec(world.Seed, i);
                if (!FaceContent.TryPickAnywhere(spots, screens, i, out ScreenAddress screen, out Vector2 feet))
                {
                    Debug.LogWarning($"MathFace: no clear spot for {spec} (seed {world.Seed}).");
                    continue;
                }
                Vector2 centre = world.ScreenCenter(screen);
                Rect bounds = FaceContent.WanderBounds(centre, feet);
                NpcTalker npc = NpcFactory.Spawn(spec, MathPopulation.LinesFor(spec, facts, hintDensity, i), centre + feet, bounds,
                    world.Relations, target, root, world.Material);
                npc.name = $"Math Shape {i} {spec.DisplayName}";
                shapes.Add(npc);
            }
        }

        private void SpawnMathEnemies(CubeWorld world, FaceSpots spots, SeededRng rng)
        {
            List<ScreenAddress> screens = ShuffledScreens(rng);
            Transform target = ResolvePlayer();
            int deadEnds = DeadEndCount(Plan.FaceSize);
            for (int i = 0; i < deadEnds + LooseEndCount(Plan.FaceSize); i++)
            {
                MathEnemyKind kind = i < deadEnds ? MathEnemyKind.DeadEnd : MathEnemyKind.LooseEnd;
                if (!FaceContent.TryPickAnywhere(spots, screens, i, out ScreenAddress screen, out Vector2 feet)) continue;
                Vector2 centre = world.ScreenCenter(screen);
                Rect bounds = FaceContent.WanderBounds(centre, feet);
                enemies.Add(MathEnemies.Spawn(kind, MathEnemies.WeaknessFor(kind, world), FaceContent.EnemyPosition(centre, feet),
                    bounds, target, root, world.Material));
            }
        }

        private Transform ResolvePlayer() => FaceContent.ResolvePlayer(ref player);

        private void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
        }
    }
}
