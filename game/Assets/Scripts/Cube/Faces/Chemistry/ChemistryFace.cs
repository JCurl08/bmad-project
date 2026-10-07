using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Curie's face, plugged into CubeWorld through IFaceContent (it sits on the CubeWorld's GameObject), in Biology's
    /// three parts. On the reveal it builds the run's ChemistryPlan around the rolled stone (the Chemistry item the
    /// catalog rolled; its stage read from its id, never from the catalog's order), and then:
    /// gate behaviours: every Chemistry gate (home or off-home) becomes the rolled stone's stage gate (dark room for the
    /// Glowstone*, cracked wall for the Wildstone*, plate door for the Leadstone*), and every optional gate of another
    /// stone becomes that stone's gate; all of them open to swings with their stone equipped. The stone itself is a
    /// normal one-shot pickup (picked up once, kept for the run);
    /// the trial: a ChemistryTrial of the rolled stone's targets on the plan's trial screen;
    /// the population: mushroom NPCs (ChemistryPopulation) and Chemistry enemies (Free Radicals, a Rust Mite) on
    /// seeded clear spots (FaceSpots, stream PopulationRngStream).
    /// Everything is removed on a rebuild.
    /// </summary>
    public class ChemistryFace : MonoBehaviour, IFaceContent, IHintDensityTarget
    {
        /// <summary>PCG32 stream for Chemistry population spots, trial spots and screens (see RngStreams).</summary>
        public const ulong PopulationRngStream = RngStreams.ChemistryPopulation;

        /// <summary>Free Radicals on a face of CubeSettings.TunedScreensPerFace screens; RadicalCount scales it per screen.</summary>
        public const int TunedRadicalCount = 2;

        /// <summary>Free Radicals on a face of faceSize x faceSize screens (about the same number per screen).</summary>
        public static int RadicalCount(int faceSize) => CubeSettings.PerScreen(TunedRadicalCount, faceSize);

        /// <summary>Rust Mites on a face of CubeSettings.TunedScreensPerFace screens; MiteCount scales it per screen.</summary>
        public const int TunedMiteCount = 1;

        /// <summary>Rust Mites on a face of faceSize x faceSize screens (about the same number per screen).</summary>
        public static int MiteCount(int faceSize) => CubeSettings.PerScreen(TunedMiteCount, faceSize);

        [SerializeField] private Transform player;
        [SerializeField] private HintDensity hintDensity = HintGenerator.FirstRunDensity;
        [SerializeField] private bool spawnEnemies = true;

        private readonly List<IsotopeGate> stageGates = new List<IsotopeGate>();
        private readonly List<NpcTalker> mushrooms = new List<NpcTalker>();
        private readonly List<Enemy> enemies = new List<Enemy>();
        private Transform root;

        public Theme Theme => Theme.Chemistry;

        /// <summary>This run's Chemistry plan; null before the reveal (or without Chemistry stones).</summary>
        public ChemistryPlan Plan { get; private set; }

        /// <summary>The run's stone item (the rolled Chemistry item ItemPlacement places).</summary>
        public ItemDefinition IsotopeItem { get; private set; }

        public ChemistryTrial Trial { get; private set; }

        /// <summary>Every stage gate built this run (required and optional, on any face).</summary>
        public IReadOnlyList<IsotopeGate> StageGates => stageGates;

        /// <summary>The mushroom NPCs of this run (destroyed ones become null).</summary>
        public IReadOnlyList<NpcTalker> Mushrooms => mushrooms;

        /// <summary>The Chemistry enemies spawned this run (dead ones become null).</summary>
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

        /// <summary>Spawn Chemistry enemies on the reveal (tests may switch it off).</summary>
        public bool SpawnEnemies
        {
            get => spawnEnemies;
            set => spawnEnemies = value;
        }

        public void BeginReveal(CubeWorld world)
        {
            // The stone's stage comes from the rolled item itself (by id), never from the catalog's order.
            IsotopeItem = world.ItemFor(Theme.Chemistry);
            List<IsotopeStage> kinds = ChemistryIsotope.VariantKinds(world.ItemCatalog);
            IsotopeStage? rolled = ChemistryIsotope.KindOf(IsotopeItem);
            if (kinds == null || rolled == null || world.ItemPlacement == null || world.ItemPlacement.RolledVariant(Theme.Chemistry) < 0)
            {
                Plan = null;
                if (world.ItemPlacement != null && world.ItemPlacement.RolledVariant(Theme.Chemistry) >= 0)
                    Debug.LogWarning($"ChemistryFace: the catalog's Chemistry variants are not stones ({IsotopeItem}); no stage gates.");
                return;
            }
            Plan = ChemistryPlan.Create(world.Model, world.ItemPlacement, rolled.Value, kinds);
        }

        public Gate CreateGate(CubeWorld world, FaceGateRequest request, GateSlot slot, ItemDefinition item)
        {
            if (Plan == null || !Plan.TryGetGate(request.Screen, request.Slot, request.Optional, out IsotopeGateSpec spec))
                return null;
            if (ChemistryIsotope.KindOf(item) != spec.Stage)
            {
                Debug.LogWarning($"ChemistryFace: {item} does not open a {spec.Stage} gate ({spec}); a plain gate instead.");
                return null;
            }

            GameObject block = world.CreateBlock("Stage Gate", slot.transform, slot.transform.position,
                CubeWorld.GateSize(slot), Color.white, -4, true);
            IsotopeGate gate;
            switch (spec.Stage)
            {
                case IsotopeStage.Glow:
                    gate = DarkRoomGate.Build(block, item, spec.Difficulty, spec.Optional, world.Material, Plan.FaceSize);
                    break;
                case IsotopeStage.Unstable:
                    gate = CrackedWallGate.Build(block, item, spec.Difficulty, spec.Optional, world.Material, Plan.FaceSize);
                    break;
                default:
                {
                    ScreenModule module = world.ModuleAt(request.Screen);
                    Vector2 centre = world.ScreenCenter(request.Screen);
                    Vector2 slotLocal = (Vector2)slot.transform.position - centre;
                    Vector2 at = centre + LeadPlateGate.PlateLocal(slotLocal);
                    gate = LeadPlateGate.Build(block, item, at, module != null ? module.transform : slot.transform,
                        spec.Difficulty, spec.Optional, world.Material, Plan.FaceSize);
                    break;
                }
            }
            stageGates.Add(gate);
            return gate;
        }

        public void EndReveal(CubeWorld world)
        {
            if (Plan == null) return;
            if (root == null) root = new GameObject("Chemistry Face Content").transform;
            var rng = new SeededRng(unchecked((ulong)(uint)world.Seed), PopulationRngStream);
            BuildTrial(world, rng);
            var spots = new FaceSpots(world, rng, FaceContent.ClearOfSlots(world));
            SpawnMushrooms(world, spots, rng);
            if (spawnEnemies) SpawnChemistryEnemies(world, spots, rng);
        }

        public void Clear()
        {
            FaceContent.DestroyAll(mushrooms);
            FaceContent.DestroyAll(enemies);
            stageGates.Clear();
            Trial = null; // lives under its module, which the rebuild destroys
            Plan = null;
            IsotopeItem = null;
        }

        /// <summary>The trial's piece spots on a module (local to its centre), in striking order; fewer when they do not fit.</summary>
        public static List<Vector2> TrialSpots(ScreenModule module, SeededRng rng) =>
            BiologyTrial.PickSpots(BiologyTrial.LaneSideSpots(TownPlan.BlockedRects(module)), ChemistryTrial.PieceCount, rng);

        private void BuildTrial(CubeWorld world, SeededRng rng)
        {
            ScreenModule module = world.ModuleAt(Plan.TrialScreen);
            if (module == null || IsotopeItem == null) return;
            Vector2 centre = world.ScreenCenter(Plan.TrialScreen);
            List<Vector2> picked = TrialSpots(module, rng);
            if (picked.Count < ChemistryTrial.PieceCount)
            {
                Debug.LogWarning($"ChemistryFace: no room for the trial on {Plan.TrialScreen} (seed {world.Seed}).");
                return;
            }
            var positions = new List<Vector2>();
            foreach (Vector2 local in picked) positions.Add(centre + local);
            var go = new GameObject("Chemistry Trial");
            go.transform.SetParent(module.transform, false);
            go.transform.position = centre;
            Trial = go.AddComponent<ChemistryTrial>();
            Trial.Configure(Plan.Stage, IsotopeItem, positions, ChemistryPlan.TrialCurrency, world.Material);
            Trial.Completed += currency => Debug.Log($"Chemistry trial complete: +{currency} (seed {world.Seed}).");
        }

        private List<ScreenAddress> ShuffledScreens(SeededRng rng) => FaceContent.ShuffledScreens(Plan.Face, Plan.FaceSize, rng);

        private void SpawnMushrooms(CubeWorld world, FaceSpots spots, SeededRng rng)
        {
            List<ScreenAddress> screens = ShuffledScreens(rng);
            RunFacts facts = RunFacts.Of(world);
            Transform target = ResolvePlayer();
            for (int i = 0; i < ChemistryPopulation.MushroomCount(Plan.FaceSize); i++)
            {
                NpcSpec spec = ChemistryPopulation.MushroomSpec(world.Seed, i);
                if (!FaceContent.TryPickAnywhere(spots, screens, i, out ScreenAddress screen, out Vector2 feet))
                {
                    Debug.LogWarning($"ChemistryFace: no clear spot for {spec} (seed {world.Seed}).");
                    continue;
                }
                Vector2 centre = world.ScreenCenter(screen);
                Rect bounds = FaceContent.WanderBounds(centre, feet);
                List<string> lines = ChemistryPopulation.LinesFor(spec, facts, hintDensity, Plan.Stage, i);
                NpcTalker npc = NpcFactory.Spawn(spec, lines, centre + feet, bounds, world.Relations, target, root, world.Material);
                npc.name = $"Chemistry Mushroom {i} {spec.DisplayName}";
                mushrooms.Add(npc);
            }
        }

        private void SpawnChemistryEnemies(CubeWorld world, FaceSpots spots, SeededRng rng)
        {
            List<ScreenAddress> screens = ShuffledScreens(rng);
            Transform target = ResolvePlayer();
            int radicals = RadicalCount(Plan.FaceSize);
            for (int i = 0; i < radicals + MiteCount(Plan.FaceSize); i++)
            {
                ChemistryEnemyKind kind = i < radicals ? ChemistryEnemyKind.FreeRadical : ChemistryEnemyKind.RustMite;
                if (!FaceContent.TryPickAnywhere(spots, screens, i, out ScreenAddress screen, out Vector2 feet)) continue;
                Vector2 centre = world.ScreenCenter(screen);
                Rect bounds = FaceContent.WanderBounds(centre, feet);
                Vector2 at = FaceContent.EnemyPosition(centre, feet);
                enemies.Add(ChemistryEnemies.Spawn(kind, ChemistryEnemies.WeaknessFor(kind, world), at, bounds, target, root,
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
