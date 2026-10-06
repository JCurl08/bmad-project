using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Curie's face, plugged into CubeWorld through IFaceContent (it sits on the CubeWorld's GameObject), in
    /// Biology's three parts. On the reveal it builds the run's ChemistryPlan, sets the player's Isotope up (the
    /// run's Chemistry item and the half-life difficulty) and then:
    /// gate behaviours: every Chemistry gate (home or off-home) becomes a stage gate by the plan (dark room for
    /// glow, cracked wall for unstable, plate door for lead), and the Chemistry pickup becomes the isotope
    /// dispenser (IFacePickupContent);
    /// the trial: a ChemistryTrial (lamp, cracked wall, plate) on the plan's trial screen;
    /// the population: mushroom NPCs (ChemistryPopulation) and Chemistry enemies (Free Radicals, a Rust Mite) on
    /// seeded clear spots (FaceSpots, stream PopulationRngStream).
    /// Everything is removed on a rebuild.
    /// </summary>
    public class ChemistryFace : MonoBehaviour, IFaceContent, IFacePickupContent
    {
        /// <summary>PCG32 stream for Chemistry population spots, trial spots and screens (plan 11).</summary>
        public const ulong PopulationRngStream = 12;

        public const int RadicalCount = 2;
        public const int MiteCount = 1;

        [SerializeField] private Transform player;
        [SerializeField] private HintDensity hintDensity = HintGenerator.FirstRunDensity;
        [SerializeField] private bool spawnEnemies = true;

        [Tooltip("Difficulty: the isotope's half-life (seconds of glow; unstable lasts 2/3 of it more). Shorter is harder.")]
        [SerializeField, Min(ChemistryIsotope.MinHalfLife)] private float halfLife = ChemistryIsotope.DefaultHalfLife;

        private readonly List<IsotopeGate> stageGates = new List<IsotopeGate>();
        private readonly List<NpcTalker> mushrooms = new List<NpcTalker>();
        private readonly List<Enemy> enemies = new List<Enemy>();
        private Transform root;

        public Theme Theme => Theme.Chemistry;

        /// <summary>This run's Chemistry plan; null before the reveal (or when Chemistry is not an item of the run).</summary>
        public ChemistryPlan Plan { get; private set; }

        /// <summary>The run's isotope item (the Chemistry item ItemPlacement places).</summary>
        public ItemDefinition IsotopeItem { get; private set; }

        /// <summary>The isotope dispenser at the Chemistry pickup's spot (null before the reveal).</summary>
        public IsotopeDispenser Dispenser { get; private set; }

        public ChemistryTrial Trial { get; private set; }

        /// <summary>Every stage gate built this run (on any face).</summary>
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

        /// <summary>Spawn Chemistry enemies on the reveal (tests may switch it off).</summary>
        public bool SpawnEnemies
        {
            get => spawnEnemies;
            set => spawnEnemies = value;
        }

        /// <summary>The half-life difficulty knob, applied to the player's Isotope on every reveal.</summary>
        public float HalfLife
        {
            get => halfLife;
            set
            {
                halfLife = Mathf.Max(ChemistryIsotope.MinHalfLife, value);
                Isotope isotope = PlayerIsotope();
                if (isotope != null) isotope.HalfLife = halfLife;
            }
        }

        /// <summary>The player's Isotope (added when missing); null without a player.</summary>
        public Isotope PlayerIsotope()
        {
            Transform target = ResolvePlayer();
            if (target == null || target.GetComponent<Inventory>() == null) return null;
            Isotope isotope = target.GetComponent<Isotope>();
            if (isotope == null) isotope = target.gameObject.AddComponent<Isotope>();
            return isotope;
        }

        public void BeginReveal(CubeWorld world)
        {
            IsotopeItem = world.ItemFor(Theme.Chemistry);
            Plan = IsotopeItem != null ? ChemistryPlan.Create(world.Model, world.ItemPlacement) : null;
            Isotope isotope = PlayerIsotope();
            if (isotope != null)
            {
                isotope.Item = IsotopeItem;
                isotope.HalfLife = halfLife;
            }
        }

        public Gate CreateGate(CubeWorld world, FaceGateRequest request, GateSlot slot, ItemDefinition item)
        {
            if (Plan == null || request.Optional || !Plan.TryGetGate(request.Screen, request.Slot, out IsotopeGateSpec spec))
                return null;

            GameObject block = world.CreateBlock("Stage Gate", slot.transform, slot.transform.position,
                CubeWorld.GateSize(slot), Color.white, -4, true);
            IsotopeGate gate;
            switch (spec.Stage)
            {
                case IsotopeStage.Glow:
                    gate = DarkRoomGate.Build(block, item, world.Material);
                    break;
                case IsotopeStage.Unstable:
                    gate = CrackedWallGate.Build(block, item, world.Material);
                    break;
                default:
                {
                    ScreenModule module = world.ModuleAt(request.Screen);
                    Vector2 centre = world.ScreenCenter(request.Screen);
                    Vector2 slotLocal = (Vector2)slot.transform.position - centre;
                    Vector2 at = centre + LeadPlateGate.PlateLocal(slotLocal);
                    gate = LeadPlateGate.Build(block, item, at, module != null ? module.transform : slot.transform, world.Material);
                    break;
                }
            }
            stageGates.Add(gate);
            return gate;
        }

        public ItemPickup CreatePickup(CubeWorld world, PickupPlacement placement, Transform parent, Vector2 position,
            ItemDefinition item)
        {
            if (Plan == null || item == null) return null;
            Dispenser = IsotopeDispenser.Create(parent, position, item, world.Material);
            return Dispenser;
        }

        public void EndReveal(CubeWorld world)
        {
            if (Plan == null) return;
            if (root == null) root = new GameObject("Chemistry Face Content").transform;
            var rng = new SeededRng(unchecked((ulong)(uint)world.Seed), PopulationRngStream);
            BuildTrial(world, rng);
            var spots = new FaceSpots(world, rng);
            SpawnMushrooms(world, spots, rng);
            if (spawnEnemies) SpawnChemistryEnemies(world, spots, rng);
        }

        public void Clear()
        {
            foreach (NpcTalker npc in mushrooms)
            {
                if (npc == null) continue;
                npc.gameObject.SetActive(false);
                Destroy(npc.gameObject);
            }
            foreach (Enemy enemy in enemies)
            {
                if (enemy == null) continue;
                enemy.gameObject.SetActive(false);
                Destroy(enemy.gameObject);
            }
            mushrooms.Clear();
            enemies.Clear();
            stageGates.Clear();
            Trial = null; // lives under its module, which the rebuild destroys
            Dispenser = null; // likewise
            Plan = null;
            IsotopeItem = null;
        }

        /// <summary>The trial's piece spots on a module (local to its centre): lamp, wall, plate; fewer when they do not fit.</summary>
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
            Trial.Configure(IsotopeItem, positions, ChemistryPlan.TrialCurrency, world.Material);
            Trial.Completed += currency => Debug.Log($"Chemistry trial complete: +{currency} (seed {world.Seed}).");
        }

        private List<ScreenAddress> ShuffledScreens(SeededRng rng)
        {
            var screens = new List<ScreenAddress>();
            for (int y = 0; y < Plan.FaceSize; y++)
                for (int x = 0; x < Plan.FaceSize; x++)
                    screens.Add(new ScreenAddress(Plan.Face, x, y));
            rng.Shuffle(screens);
            return screens;
        }

        private void SpawnMushrooms(CubeWorld world, FaceSpots spots, SeededRng rng)
        {
            List<ScreenAddress> screens = ShuffledScreens(rng);
            RunFacts facts = RunFacts.Of(world);
            Transform target = ResolvePlayer();
            for (int i = 0; i < ChemistryPopulation.MushroomCount; i++)
            {
                NpcSpec spec = ChemistryPopulation.MushroomSpec(world.Seed, i);
                if (!TryPickAnywhere(spots, screens, i, out ScreenAddress screen, out Vector2 feet))
                {
                    Debug.LogWarning($"ChemistryFace: no clear spot for {spec} (seed {world.Seed}).");
                    continue;
                }
                Vector2 centre = world.ScreenCenter(screen);
                Rect quadrant = TownPlan.QuadrantBounds(feet);
                var bounds = new Rect(centre + quadrant.position, quadrant.size);
                List<string> lines = ChemistryPopulation.LinesFor(spec, facts, hintDensity, i == 0);
                NpcTalker npc = NpcFactory.Spawn(spec, lines, centre + feet, bounds, world.Relations, target, root, world.Material);
                npc.name = $"Chemistry Mushroom {i} {spec.DisplayName}";
                mushrooms.Add(npc);
            }
        }

        private void SpawnChemistryEnemies(CubeWorld world, FaceSpots spots, SeededRng rng)
        {
            List<ScreenAddress> screens = ShuffledScreens(rng);
            Transform target = ResolvePlayer();
            for (int i = 0; i < RadicalCount + MiteCount; i++)
            {
                ChemistryEnemyKind kind = i < RadicalCount ? ChemistryEnemyKind.FreeRadical : ChemistryEnemyKind.RustMite;
                if (!TryPickAnywhere(spots, screens, i, out ScreenAddress screen, out Vector2 feet)) continue;
                Vector2 centre = world.ScreenCenter(screen);
                Rect quadrant = TownPlan.QuadrantBounds(feet);
                var bounds = new Rect(centre + quadrant.position, quadrant.size);
                Vector2 at = centre + feet + new Vector2(0f, Enemy.BodyRadius);
                enemies.Add(ChemistryEnemies.Spawn(kind, ChemistryEnemies.WeaknessFor(kind, world), at, bounds, target, root,
                    world.Material));
            }
        }

        /// <summary>A spot on screens[start], else on the next screens round the face.</summary>
        private static bool TryPickAnywhere(FaceSpots spots, List<ScreenAddress> screens, int start,
            out ScreenAddress screen, out Vector2 feet)
        {
            for (int k = 0; k < screens.Count; k++)
            {
                screen = screens[(start + k) % screens.Count];
                if (spots.TryPick(screen, out feet)) return true;
            }
            screen = default;
            feet = default;
            return false;
        }

        private Transform ResolvePlayer()
        {
            if (player != null) return player;
            var navigator = FindAnyObjectByType<CubeNavigator>();
            if (navigator != null) player = navigator.transform;
            return player;
        }

        private void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
        }
    }
}
