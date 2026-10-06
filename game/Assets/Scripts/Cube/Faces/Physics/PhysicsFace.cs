using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Einstein's face, plugged into CubeWorld through IFaceContent (it sits on the CubeWorld's GameObject), in the
    /// same three parts as Biology and Chemistry. On the reveal it builds the run's PhysicsPlan (with the screens'
    /// modules, so every spot is known), sets the player's MassMitt up (the run's Physics item) and then:
    /// gate behaviours: every Physics gate (home or off-home) becomes a TimedDoorGate with its DoorSwitch and its own
    /// boulders on its screen, built as the gate is (before any face's population, so NPC spots keep clear of them);
    /// the trial: a PhysicsTrial (two booths, shared boulders) on the plan's trial screen;
    /// the population: alien NPCs (PhysicsPopulation), Sir Isaac Newton and Physics enemies (Quantum Fleas, a Static
    /// Cling) on seeded clear spots (FaceSpots, stream PopulationRngStream).
    /// Everything is removed on a rebuild.
    /// </summary>
    public class PhysicsFace : MonoBehaviour, IFaceContent
    {
        /// <summary>PCG32 stream for Physics population spots and screens (plan 13).</summary>
        public const ulong PopulationRngStream = 14;

        public const int FleaCount = 2;
        public const int ClingCount = 1;

        [SerializeField] private Transform player;
        [SerializeField] private HintDensity hintDensity = HintGenerator.FirstRunDensity;
        [SerializeField] private bool spawnEnemies = true;

        [Tooltip("Difficulty: the most boulders a timed door may need (1 to 3). Each door's count is seeded in 1..this.")]
        [SerializeField, Range(1, PhysicsPlan.MaxRequiredCap)] private int maxRequired = PhysicsPlan.DefaultMaxRequired;

        private readonly List<TimedDoorGate> doors = new List<TimedDoorGate>();
        private readonly List<Boulder> boulders = new List<Boulder>();
        private readonly List<NpcTalker> aliens = new List<NpcTalker>();
        private readonly List<Enemy> enemies = new List<Enemy>();
        private Transform root;

        public Theme Theme => Theme.Physics;

        /// <summary>This run's Physics plan; null before the reveal (or when Physics is not an item of the run).</summary>
        public PhysicsPlan Plan { get; private set; }

        /// <summary>The run's Mass Mitt (the Physics item ItemPlacement places).</summary>
        public ItemDefinition MittItem { get; private set; }

        public PhysicsTrial Trial { get; private set; }

        /// <summary>Every timed door built this run (on any face), in the plan's door order.</summary>
        public IReadOnlyList<TimedDoorGate> Doors => doors;

        /// <summary>Every gate boulder built this run (the trial's are on the trial).</summary>
        public IReadOnlyList<Boulder> Boulders => boulders;

        /// <summary>The alien NPCs of this run (destroyed ones become null).</summary>
        public IReadOnlyList<NpcTalker> Aliens => aliens;

        /// <summary>Sir Isaac Newton (null before the reveal or when there was no room).</summary>
        public NpcTalker Newton { get; private set; }

        /// <summary>The Physics enemies spawned this run (dead ones become null).</summary>
        public IReadOnlyList<Enemy> Enemies => enemies;

        public Transform Player
        {
            get => player;
            set => player = value;
        }

        public bool SpawnEnemies
        {
            get => spawnEnemies;
            set => spawnEnemies = value;
        }

        /// <summary>The difficulty knob, used by the next reveal.</summary>
        public int MaxRequired
        {
            get => maxRequired;
            set => maxRequired = Mathf.Clamp(value, 1, PhysicsPlan.MaxRequiredCap);
        }

        /// <summary>The player's MassMitt (added when missing); null without a player.</summary>
        public MassMitt PlayerMitt()
        {
            Transform target = ResolvePlayer();
            if (target == null || target.GetComponent<Inventory>() == null || target.GetComponent<Rigidbody2D>() == null) return null;
            MassMitt mitt = target.GetComponent<MassMitt>();
            if (mitt == null) mitt = target.gameObject.AddComponent<MassMitt>();
            return mitt;
        }

        /// <summary>The timed door built for a plan door (null if none).</summary>
        public TimedDoorGate DoorFor(TimedDoorSpec spec)
        {
            if (Plan == null || spec == null) return null;
            for (int i = 0; i < Plan.Doors.Count && i < doors.Count; i++)
                if (Plan.Doors[i].Screen == spec.Screen && Plan.Doors[i].Slot == spec.Slot) return doors[i];
            return null;
        }

        public void BeginReveal(CubeWorld world)
        {
            MittItem = world.ItemFor(Theme.Physics);
            Plan = MittItem != null
                ? PhysicsPlan.Create(world.Model, world.ItemPlacement, world.ModuleAt, BiologyBeaks.VariantKinds(world.ItemCatalog), maxRequired)
                : null;
            if (Plan != null)
                for (int i = 0; i < Plan.Doors.Count; i++) doors.Add(null);
            MassMitt mitt = PlayerMitt();
            if (mitt != null) mitt.Item = MittItem;
        }

        public Gate CreateGate(CubeWorld world, FaceGateRequest request, GateSlot slot, ItemDefinition item)
        {
            if (Plan == null || request.Optional) return null;
            int index = -1;
            for (int i = 0; i < Plan.Doors.Count; i++)
                if (Plan.Doors[i].Screen == request.Screen && Plan.Doors[i].Slot == request.Slot) index = i;
            if (index < 0) return null;
            TimedDoorSpec spec = Plan.Doors[index];
            DoorLayout layout = spec.Layout;
            if (layout == null || !layout.HasSwitch)
            {
                Debug.LogWarning($"PhysicsFace: no layout for {spec} (seed {world.Seed}); a plain gate instead.");
                return null;
            }

            ScreenModule module = world.ModuleAt(request.Screen);
            Transform parent = module != null ? module.transform : slot.transform;
            Vector2 centre = world.ScreenCenter(request.Screen);
            GameObject block = world.CreateBlock("Timed Door", slot.transform, slot.transform.position,
                CubeWorld.GateSize(slot), Color.white, -4, true);
            TimedDoorGate gate = TimedDoorGate.Build(block, item, layout.Opening, layout.PocketDepth, centre + layout.SwitchLocal,
                parent, layout.BaseSeconds, layout.Required, world.Material);
            doors[index] = gate;

            Rect bounds = Boulder.ScreenBounds(centre);
            foreach (Vector2 local in spec.Boulders)
            {
                Boulder b = Boulder.Create(parent, centre + local, bounds, world.Material);
                b.name = $"Boulder ({spec.Screen} #{spec.Slot})";
                boulders.Add(b);
            }
            return gate;
        }

        public void EndReveal(CubeWorld world)
        {
            if (Plan == null) return;
            if (root == null) root = new GameObject("Physics Face Content").transform;
            Physics2D.SyncTransforms();
            var rng = new SeededRng(unchecked((ulong)(uint)world.Seed), PopulationRngStream);
            BuildTrial(world);
            Physics2D.SyncTransforms();
            var spots = new FaceSpots(world, rng);
            SpawnAliens(world, spots, rng);
            if (spawnEnemies) SpawnPhysicsEnemies(world, spots, rng);
        }

        public void Clear()
        {
            MassMitt mitt = player != null ? player.GetComponent<MassMitt>() : null;
            if (mitt != null) mitt.Release();
            foreach (NpcTalker npc in aliens)
            {
                if (npc == null) continue;
                npc.gameObject.SetActive(false);
                Destroy(npc.gameObject);
            }
            if (Newton != null)
            {
                Newton.gameObject.SetActive(false);
                Destroy(Newton.gameObject);
            }
            foreach (Enemy enemy in enemies)
            {
                if (enemy == null) continue;
                enemy.gameObject.SetActive(false);
                Destroy(enemy.gameObject);
            }
            // Doors, boulders and the trial live under their modules, which the rebuild destroys; deactivate the
            // boulders now so the time field never weighs a boulder of the old run.
            foreach (Boulder b in boulders)
                if (b != null) b.gameObject.SetActive(false);
            if (Trial != null)
                foreach (Boulder b in Trial.Boulders)
                    if (b != null) b.gameObject.SetActive(false);
            aliens.Clear();
            enemies.Clear();
            doors.Clear();
            boulders.Clear();
            Newton = null;
            Trial = null;
            Plan = null;
            MittItem = null;
        }

        private void BuildTrial(CubeWorld world)
        {
            PhysicsTrialSpec spec = Plan.Trial;
            ScreenModule module = spec != null ? world.ModuleAt(spec.Screen) : null;
            if (module == null || MittItem == null)
            {
                Debug.LogWarning($"PhysicsFace: no room for the trial on {Plan.TrialScreen} (seed {world.Seed}).");
                return;
            }
            Vector2 centre = world.ScreenCenter(spec.Screen);
            var go = new GameObject("Physics Trial");
            go.transform.SetParent(module.transform, false);
            go.transform.position = centre;
            Trial = go.AddComponent<PhysicsTrial>();
            Trial.Configure(MittItem, spec, centre, PhysicsPlan.TrialCurrency, world.Material);
            Trial.Completed += currency => Debug.Log($"Physics trial complete: +{currency} (seed {world.Seed}).");
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

        private void SpawnAliens(CubeWorld world, FaceSpots spots, SeededRng rng)
        {
            List<ScreenAddress> screens = ShuffledScreens(rng);
            RunFacts facts = RunFacts.Of(world);
            Transform target = ResolvePlayer();
            for (int i = 0; i < PhysicsPopulation.AlienCount; i++)
            {
                NpcSpec spec = PhysicsPopulation.AlienSpec(world.Seed, i);
                if (!TryPickAnywhere(spots, screens, i, out ScreenAddress screen, out Vector2 feet))
                {
                    Debug.LogWarning($"PhysicsFace: no clear spot for {spec} (seed {world.Seed}).");
                    continue;
                }
                NpcTalker npc = SpawnNpc(world, spec, PhysicsPopulation.LinesFor(spec, facts, hintDensity, i == 0), screen, feet, target);
                npc.name = $"Physics Alien {i} {spec.DisplayName}";
                aliens.Add(npc);
            }

            NpcSpec newton = PhysicsPopulation.NewtonSpec(world.Seed);
            if (TryPickAnywhere(spots, screens, PhysicsPopulation.AlienCount, out ScreenAddress at, out Vector2 newtonFeet))
            {
                Newton = SpawnNpc(world, newton, PhysicsPopulation.NewtonLines(), at, newtonFeet, target);
                Newton.name = $"Physics Cameo {PhysicsPopulation.NewtonName}";
                AppleBonk.Add(Newton.gameObject, NpcFactory.MaxBodyHeight, world.Material);
            }
        }

        private NpcTalker SpawnNpc(CubeWorld world, NpcSpec spec, List<string> lines, ScreenAddress screen, Vector2 feet, Transform target)
        {
            Vector2 centre = world.ScreenCenter(screen);
            Rect quadrant = TownPlan.QuadrantBounds(feet);
            var bounds = new Rect(centre + quadrant.position, quadrant.size);
            return NpcFactory.Spawn(spec, lines, centre + feet, bounds, world.Relations, target, root, world.Material);
        }

        private void SpawnPhysicsEnemies(CubeWorld world, FaceSpots spots, SeededRng rng)
        {
            List<ScreenAddress> screens = ShuffledScreens(rng);
            Transform target = ResolvePlayer();
            for (int i = 0; i < FleaCount + ClingCount; i++)
            {
                PhysicsEnemyKind kind = i < FleaCount ? PhysicsEnemyKind.QuantumFlea : PhysicsEnemyKind.StaticCling;
                if (!TryPickAnywhere(spots, screens, i, out ScreenAddress screen, out Vector2 feet)) continue;
                Vector2 centre = world.ScreenCenter(screen);
                Rect quadrant = TownPlan.QuadrantBounds(feet);
                var bounds = new Rect(centre + quadrant.position, quadrant.size);
                Vector2 position = centre + feet + new Vector2(0f, Enemy.BodyRadius);
                enemies.Add(PhysicsEnemies.Spawn(kind, PhysicsEnemies.WeaknessFor(kind, world), position, bounds, target, root,
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
