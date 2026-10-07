using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The Bridgekeeper*'s face, plugged into CubeWorld through IFaceContent (it sits on the CubeWorld's GameObject), in the
    /// same three parts as the other faces. On the reveal it builds the run's MathPlan (with the screens' modules, so the
    /// island screens are known), and then:
    /// gate behaviours: every Math gate (on Math and off it) becomes a GapGate the gap-crosser* opens;
    /// the island screens (1.25): each of the plan's island screens becomes an IslandScreen (solid water, islands, planks and
    /// broken spans the gap-crosser* mends), and the trial a MathTrial on its screen's trial island;
    /// the population: shape people (MathPopulation) and Math enemies (Dead Ends, a Loose End) on seeded clear spots
    /// (FaceSpots, stream PopulationRngStream) off the island screens, kept off the fronts of Math's own gates, the core
    /// entrance and the other slots and interactables.
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

        /// <summary>NPCs and enemies spawn at least this far beyond a slot or interactable (a portal, a hidden item, a plate).</summary>
        public const float SlotKeepOut = 1.2f;

        [SerializeField] private Transform player;
        [SerializeField] private HintDensity hintDensity = HintGenerator.FirstRunDensity;
        [SerializeField] private bool spawnEnemies = true;

        private readonly List<IslandScreen> islandScreens = new List<IslandScreen>();
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

        /// <summary>The island screens built on the reveal (row by row).</summary>
        public IReadOnlyList<IslandScreen> IslandScreens => islandScreens;

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
            Plan = MathPlan.ForRun(world.Model, world.ItemPlacement, GapCrosser, world.ModuleAt, BiologyBeaks.VariantKinds(world.ItemCatalog),
                ChemistryIsotope.VariantKinds(world.ItemCatalog));
        }

        public Gate CreateGate(CubeWorld world, FaceGateRequest request, GateSlot slot, ItemDefinition item)
        {
            if (Plan == null || request.Optional || !Plan.TryGetGate(request.Screen, request.Slot, out _)) return null;
            GameObject gapBlock = world.CreateBlock("Gap Gate", slot.transform, slot.transform.position, CubeWorld.GateSize(slot),
                Color.white, -4, true);
            GapGate gap = GapGate.Build(gapBlock, item, world.Material);
            gaps.Add(gap);
            return gap;
        }

        public void EndReveal(CubeWorld world)
        {
            if (Plan == null) return;
            if (root == null) root = new GameObject("Math Face Content").transform;
            var rng = new SeededRng(unchecked((ulong)(uint)world.Seed), PopulationRngStream);
            BuildIslands(world);
            MathPlan plan = Plan;
            var marks = new Dictionary<ScreenAddress, IReadOnlyList<ScreenMark>>();
            // Math's own content: the fronts of its gap gates.
            Dictionary<ScreenAddress, List<ScreenMark>> own = plan.ContentMarks();
            bool Allowed(ScreenAddress screen, Vector2 feet)
            {
                if (plan.IsIslandScreen(screen)) return false; // nobody stands in the water
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
            // Gaps, island screens and the trial live under their modules, which the rebuild destroys.
            islandScreens.Clear();
            gaps.Clear();
            Trial = null;
            Plan = null;
            GapCrosser = null;
        }

        /// <summary>Builds every island screen of the plan (water, islands, planks, broken spans) and the trial on its island.</summary>
        private void BuildIslands(CubeWorld world)
        {
            foreach (IslandScreenSpec spec in Plan.IslandScreens)
            {
                ScreenModule module = world.ModuleAt(spec.Screen);
                if (module == null || spec.Layout == null)
                {
                    Debug.LogWarning($"MathFace: no island layout for {spec} (seed {world.Seed}).");
                    continue;
                }
                IslandScreen screen = IslandScreen.Create($"Islands ({spec.Screen})", module.transform, spec.Layout,
                    world.ScreenCenter(spec.Screen), world.Material);
                islandScreens.Add(screen);
                if (!spec.HasTrial) continue;
                var go = new GameObject("Math Trial");
                go.transform.SetParent(screen.transform, false);
                Trial = go.AddComponent<MathTrial>();
                Trial.Configure(screen, spec.Layout.TrialIsland, MathPlan.TrialCurrency, world.Material);
                Trial.Completed += currency => Debug.Log($"Math trial complete: +{currency} (seed {world.Seed}).");
            }
        }

        private List<ScreenAddress> ShuffledScreens(SeededRng rng) => FaceContent.ShuffledScreens(Plan.Face, Plan.FaceSize, rng);

        private void SpawnShapes(CubeWorld world, FaceSpots spots, SeededRng rng)
        {
            List<ScreenAddress> screens = ShuffledScreens(rng);
            RunFacts facts = RunFacts.Of(world);
            Transform target = ResolvePlayer();
            FaceContent.SpawnPopulation(world, spots, screens, MathPopulation.ShapeCount(Plan.FaceSize), i => MathPopulation.ShapeSpec(world.Seed, i),
                (spec, i) => MathPopulation.LinesFor(spec, facts, hintDensity, i), "MathFace", "Math Shape", target, root, shapes);
        }

        private void SpawnMathEnemies(CubeWorld world, FaceSpots spots, SeededRng rng)
        {
            List<ScreenAddress> screens = ShuffledScreens(rng);
            Transform target = ResolvePlayer();
            FaceContent.SpawnEnemies(world, spots, screens, MathEnemyKind.DeadEnd, DeadEndCount(Plan.FaceSize), MathEnemyKind.LooseEnd, LooseEndCount(Plan.FaceSize),
                (kind, at, bounds) => MathEnemies.Spawn(kind, MathEnemies.WeaknessFor(kind, world), at, bounds, target, root, world.Material),
                enemies);
        }

        private Transform ResolvePlayer() => FaceContent.ResolvePlayer(ref player);

        private void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
        }
    }
}
