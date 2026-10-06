using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Darwin's face, plugged into CubeWorld through IFaceContent (it sits on the CubeWorld's GameObject).
    /// On the reveal it builds the run's BiologyPlan and then:
    /// gate behaviours: every gate of the Biology item (the rolled beak, home or off-home) and every optional
    /// gate of the other beak becomes a beak gate (rock/pot, distant-button door or flower-vine bramble);
    /// the trial: a BiologyTrial on the plan's trial screen, built from the rolled beak's mechanics;
    /// the population: finch NPCs (BiologyPopulation) and Biology enemies (Seed Weevils, a Pollen Puff) on
    /// seeded clear spots (FaceSpots, stream PopulationRngStream).
    /// Everything is removed on a rebuild. Chemistry and Physics follow the same three-part pattern.
    /// </summary>
    public class BiologyFace : MonoBehaviour, IFaceContent, IHintDensityTarget
    {
        /// <summary>PCG32 stream for Biology population spots and screens (see RngStreams).</summary>
        public const ulong PopulationRngStream = RngStreams.BiologyPopulation;

        public const int WeevilCount = 2;
        public const int PuffCount = 1;

        [SerializeField] private Transform player;
        [SerializeField] private HintDensity hintDensity = HintGenerator.FirstRunDensity;
        [SerializeField] private bool spawnEnemies = true;

        private readonly List<BeakGate> beakGates = new List<BeakGate>();
        private readonly List<NpcTalker> finches = new List<NpcTalker>();
        private readonly List<Enemy> enemies = new List<Enemy>();
        private Transform root;

        public Theme Theme => Theme.Biology;

        /// <summary>This run's Biology plan; null before the reveal (or without beak variants).</summary>
        public BiologyPlan Plan { get; private set; }

        /// <summary>The run's beak item (the Biology item ItemPlacement places).</summary>
        public ItemDefinition BeakItem { get; private set; }

        public BiologyTrial Trial { get; private set; }

        /// <summary>Every beak gate built this run (required and optional, on any face).</summary>
        public IReadOnlyList<BeakGate> BeakGates => beakGates;

        /// <summary>The finch NPCs of this run (destroyed ones become null).</summary>
        public IReadOnlyList<NpcTalker> Finches => finches;

        /// <summary>The Biology enemies spawned this run (dead ones become null).</summary>
        public IReadOnlyList<Enemy> Enemies => enemies;

        public Transform Player
        {
            get => player;
            set => player = value;
        }

        /// <summary>Density of the population's hints (the RunLoop sets it from the saved run count).</summary>
        public HintDensity HintDensity
        {
            get => hintDensity;
            set => hintDensity = value;
        }

        /// <summary>Spawn Biology enemies on the reveal (tests may switch it off).</summary>
        public bool SpawnEnemies
        {
            get => spawnEnemies;
            set => spawnEnemies = value;
        }

        public void BeginReveal(CubeWorld world)
        {
            // The beak kinds come from the rolled items themselves (by id), never from the catalog's order.
            BeakItem = world.ItemFor(Theme.Biology);
            List<BeakKind> kinds = BiologyBeaks.VariantKinds(world.ItemCatalog);
            BeakKind? rolled = BiologyBeaks.KindOf(BeakItem);
            if (kinds == null || rolled == null || world.ItemPlacement == null || world.ItemPlacement.RolledVariant(Theme.Biology) < 0)
            {
                Plan = null;
                if (world.ItemPlacement != null && world.ItemPlacement.RolledVariant(Theme.Biology) >= 0)
                    Debug.LogWarning($"BiologyFace: the catalog's Biology variants are not beaks ({BeakItem}); no beak gates.");
                return;
            }
            Plan = BiologyPlan.Create(world.Model, world.ItemPlacement, rolled.Value, kinds);
        }

        public Gate CreateGate(CubeWorld world, FaceGateRequest request, GateSlot slot, ItemDefinition item)
        {
            if (Plan == null || !Plan.TryGetGate(request.Screen, request.Slot, request.Optional, out BeakGateSpec spec))
                return null;

            GameObject block = world.CreateBlock("Beak Gate", slot.transform, slot.transform.position,
                CubeWorld.GateSize(slot), Color.white, -4, true);
            Vector2 centre = world.ScreenCenter(request.Screen);
            BeakGate gate;
            switch (spec.Kind)
            {
                case BeakGateKind.Breakable:
                    gate = BreakableGate.Build(block, item, spec.Look, spec.Difficulty, spec.Optional, world.Material);
                    break;
                case BeakGateKind.FlowerVine:
                {
                    ScreenModule flowerModule = world.ModuleAt(spec.FlowerScreen);
                    Transform parent = flowerModule != null ? flowerModule.transform : slot.transform;
                    Vector2 at = world.ScreenCenter(spec.FlowerScreen) + FlowerVineGate.FlowerLocal;
                    gate = FlowerVineGate.Build(block, item, at, parent, spec.Difficulty, spec.Optional, world.Material);
                    break;
                }
                default:
                {
                    ScreenModule module = world.ModuleAt(request.Screen);
                    Vector2 slotLocal = (Vector2)slot.transform.position - centre;
                    Vector2 at = centre + ButtonGate.ButtonLocal(slotLocal);
                    gate = ButtonGate.Build(block, item, at, module != null ? module.transform : slot.transform,
                        spec.Difficulty, spec.Optional, world.Material);
                    break;
                }
            }
            beakGates.Add(gate);
            return gate;
        }

        public void EndReveal(CubeWorld world)
        {
            if (Plan == null) return;
            if (root == null) root = new GameObject("Biology Face Content").transform;
            var rng = new SeededRng(unchecked((ulong)(uint)world.Seed), PopulationRngStream);
            BuildTrial(world, rng);
            var spots = new FaceSpots(world, rng);
            SpawnFinches(world, spots, rng);
            if (spawnEnemies) SpawnBiologyEnemies(world, spots, rng);
        }

        public void Clear()
        {
            FaceContent.DestroyAll(finches);
            FaceContent.DestroyAll(enemies);
            beakGates.Clear();
            Trial = null; // lives under its module, which the rebuild destroys
            Plan = null;
            BeakItem = null;
        }

        private void BuildTrial(CubeWorld world, SeededRng rng)
        {
            ScreenModule module = world.ModuleAt(Plan.TrialScreen);
            if (module == null || BeakItem == null) return;
            int needed = BiologyTrial.PiecesFor(Plan.Beak);
            Vector2 centre = world.ScreenCenter(Plan.TrialScreen);
            List<Vector2> picked = BiologyTrial.PickSpots(BiologyTrial.LaneSideSpots(TownPlan.BlockedRects(module)), needed, rng);
            if (picked.Count < needed)
            {
                Debug.LogWarning($"BiologyFace: no room for the trial on {Plan.TrialScreen} (seed {world.Seed}).");
                return;
            }
            var positions = new List<Vector2>();
            foreach (Vector2 local in picked) positions.Add(centre + local);
            var go = new GameObject("Biology Trial");
            go.transform.SetParent(module.transform, false);
            go.transform.position = centre;
            Trial = go.AddComponent<BiologyTrial>();
            Trial.Configure(Plan.Beak, BeakItem, positions, BiologyPlan.TrialCurrency, world.Material);
            Trial.Completed += currency => Debug.Log($"Biology trial complete: +{currency} (seed {world.Seed}).");
        }

        private List<ScreenAddress> ShuffledScreens(SeededRng rng) => FaceContent.ShuffledScreens(Plan.Face, Plan.FaceSize, rng);

        private void SpawnFinches(CubeWorld world, FaceSpots spots, SeededRng rng)
        {
            List<ScreenAddress> screens = ShuffledScreens(rng);
            RunFacts facts = RunFacts.Of(world);
            Transform target = ResolvePlayer();
            for (int i = 0; i < BiologyPopulation.FinchCount; i++)
            {
                NpcSpec spec = BiologyPopulation.FinchSpec(world.Seed, i);
                if (!FaceContent.TryPickAnywhere(spots, screens, i, out ScreenAddress screen, out Vector2 feet))
                {
                    Debug.LogWarning($"BiologyFace: no clear spot for {spec} (seed {world.Seed}).");
                    continue;
                }
                Vector2 centre = world.ScreenCenter(screen);
                Rect bounds = FaceContent.WanderBounds(centre, feet);
                List<string> lines = BiologyPopulation.LinesFor(spec, facts, hintDensity, Plan.Beak, i == 0);
                NpcTalker npc = NpcFactory.Spawn(spec, lines, centre + feet, bounds, world.Relations, target, root, world.Material);
                npc.name = $"Biology Finch {i} {spec.DisplayName}";
                finches.Add(npc);
            }
        }

        private void SpawnBiologyEnemies(CubeWorld world, FaceSpots spots, SeededRng rng)
        {
            List<ScreenAddress> screens = ShuffledScreens(rng);
            Transform target = ResolvePlayer();
            for (int i = 0; i < WeevilCount + PuffCount; i++)
            {
                BiologyEnemyKind kind = i < WeevilCount ? BiologyEnemyKind.SeedWeevil : BiologyEnemyKind.PollenPuff;
                if (!FaceContent.TryPickAnywhere(spots, screens, i, out ScreenAddress screen, out Vector2 feet)) continue;
                Vector2 centre = world.ScreenCenter(screen);
                Rect bounds = FaceContent.WanderBounds(centre, feet);
                Vector2 at = FaceContent.EnemyPosition(centre, feet);
                enemies.Add(BiologyEnemies.Spawn(kind, BiologyEnemies.WeaknessFor(kind, world), at, bounds, target, root,
                    world.Material));
            }
        }

        private Transform ResolvePlayer() => FaceContent.ResolvePlayer(ref player);

        private void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
        }
    }
}
