using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The Wildlands*' face (Earth and Atmosphere), plugged into CubeWorld through IFaceContent (it sits on the CubeWorld's
    /// GameObject), in the same parts as the other faces. On the reveal it builds the run's EarthPlan around the rolled
    /// Stormcaller* (its phenomenon read from the item's id, never from the catalog's order), and then:
    /// gate behaviours: every Earth gate (home or off-home) becomes the rolled phenomenon's gate (rock pillars for the quake,
    /// a fire wall for rain, water for snow, a debris pile for the tornado), and every optional gate of another wand that
    /// wand's gate; each opens to one swing of its wand;
    /// the hazards: a wind band, snow patch, freezing rain or quake crevice on every Earth screen but the calm eye (the
    /// centre) and any screen holding a timed door; the player's HazardReceiver (added when missing) reads them through
    /// PlayerMover's modifier hook;
    /// the trial: an EarthTrial of the rolled phenomenon's targets on the plan's trial screen;
    /// the population: terrestrial and avian dinosaurs (EarthPopulation) and Earth enemies (Squalls, a Rumbler) on seeded
    /// clear spots (FaceSpots, stream PopulationRngStream), kept off crevices, gate fronts, trial pieces and slots.
    /// Everything is removed on a rebuild.
    /// </summary>
    public class EarthFace : MonoBehaviour, IFaceContent, IHintDensityTarget
    {
        /// <summary>PCG32 stream for Earth population spots and screens (see RngStreams).</summary>
        public const ulong PopulationRngStream = RngStreams.EarthPopulation;

        public const int TunedSquallCount = 2;
        public static int SquallCount(int faceSize) => CubeSettings.PerScreen(TunedSquallCount, faceSize);

        public const int TunedRumblerCount = 1;
        public static int RumblerCount(int faceSize) => CubeSettings.PerScreen(TunedRumblerCount, faceSize);

        /// <summary>NPCs and enemies spawn at least this far beyond a crevice, gate front, trial piece or slot.</summary>
        public const float KeepOut = 0.9f;

        /// <summary>The calm eye's soft ring (decor only).</summary>
        public static readonly Color EyeColor = new Color(1f, 1f, 0.85f, 0.18f);

        [SerializeField] private Transform player;
        [SerializeField] private HintDensity hintDensity = HintGenerator.FirstRunDensity;
        [SerializeField] private bool spawnEnemies = true;

        private readonly List<PhenomenonGate> phenomenonGates = new List<PhenomenonGate>();
        private readonly List<HazardZone> zones = new List<HazardZone>();
        private readonly List<QuakeCrevice> crevices = new List<QuakeCrevice>();
        private readonly List<NpcTalker> dinosaurs = new List<NpcTalker>();
        private readonly List<Enemy> enemies = new List<Enemy>();
        private readonly List<Transform> decor = new List<Transform>();
        private Transform root;

        public Theme Theme => Theme.EarthAtmosphere;

        /// <summary>This run's Earth plan; null before the reveal (or when Earth is not an item of the run).</summary>
        public EarthPlan Plan { get; private set; }

        /// <summary>The run's Stormcaller* (the rolled Earth item ItemPlacement places).</summary>
        public ItemDefinition Wand { get; private set; }

        public EarthTrial Trial { get; private set; }

        /// <summary>Every Earth gate built this run (required and optional, on any face).</summary>
        public IReadOnlyList<PhenomenonGate> PhenomenonGates => phenomenonGates;

        /// <summary>The wind bands, snow patches and freezing rain of this run.</summary>
        public IReadOnlyList<HazardZone> Zones => zones;

        public IReadOnlyList<QuakeCrevice> Crevices => crevices;
        public IReadOnlyList<NpcTalker> Dinosaurs => dinosaurs;
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
            // The phenomenon comes from the rolled item itself (by id), never from the catalog's order.
            Wand = world.ItemFor(Theme.EarthAtmosphere);
            Plan = EarthPlan.ForRun(world.Model, world.ItemPlacement, Wand, world.ModuleAt, StormcallerItem.VariantKinds(world.ItemCatalog),
                BiologyBeaks.VariantKinds(world.ItemCatalog), ChemistryIsotope.VariantKinds(world.ItemCatalog));
            if (Plan != null && Plan.Rolled == Phenomenon.None)
                Debug.LogWarning($"EarthFace: the catalog's Earth variants are not Stormcallers* ({Wand}); plain gates instead.");
        }

        public Gate CreateGate(CubeWorld world, FaceGateRequest request, GateSlot slot, ItemDefinition item)
        {
            if (Plan == null || !Plan.TryGetGate(request.Screen, request.Slot, request.Optional, out EarthGateSpec spec)) return null;
            if (!EarthPlan.Builds(spec, item))
            {
                Debug.LogWarning($"EarthFace: {item} does not open a {spec.Kind} gate ({spec}); a plain gate instead.");
                return null;
            }
            GameObject block = world.CreateBlock("Earth Gate", slot.transform, slot.transform.position, CubeWorld.GateSize(slot),
                Color.white, -4, true);
            PhenomenonGate gate = PhenomenonGate.Build(spec.Kind, block, item, spec.Optional, world.Material);
            phenomenonGates.Add(gate);
            return gate;
        }

        public void EndReveal(CubeWorld world)
        {
            if (Plan == null) return;
            if (root == null) root = new GameObject("Earth Face Content").transform;
            BuildHazards(world);
            BuildTrial(world);
            EarthPlan plan = Plan;
            Dictionary<ScreenAddress, List<ScreenMark>> own = plan.ContentMarks();
            var marks = new Dictionary<ScreenAddress, List<ScreenMark>>();
            bool Allowed(ScreenAddress screen, Vector2 feet)
            {
                if (plan.NearCrevice(screen, feet, KeepOut) || plan.NearCrevice(screen, feet + new Vector2(0f, NpcFactory.MaxBodyHeight), KeepOut))
                    return false;
                if (!marks.TryGetValue(screen, out List<ScreenMark> list))
                {
                    list = plan.MarksOn(screen);
                    if (own.TryGetValue(screen, out List<ScreenMark> mine)) list.AddRange(mine);
                    marks[screen] = list;
                }
                Vector2 body = feet + new Vector2(0f, NpcFactory.MaxBodyHeight / 2f);
                foreach (ScreenMark m in list)
                    if (Vector2.Distance(feet, m.Local) < m.Radius + KeepOut || Vector2.Distance(body, m.Local) < m.Radius + KeepOut)
                        return false;
                return true;
            }
            var rng = new SeededRng(unchecked((ulong)(uint)world.Seed), PopulationRngStream);
            var spots = new FaceSpots(world, rng, Allowed);
            SpawnDinosaurs(world, spots, rng);
            if (spawnEnemies) SpawnEarthEnemies(world, spots, rng);
        }

        public void Clear()
        {
            FaceContent.DestroyAll(dinosaurs);
            FaceContent.DestroyAll(enemies);
            FaceContent.DestroyAll(zones);
            FaceContent.DestroyAll(crevices);
            FaceContent.DestroyAll(decor);
            // Gates and the trial live under their modules, which the rebuild destroys.
            phenomenonGates.Clear();
            Trial = null;
            Plan = null;
            Wand = null;
        }

        /// <summary>The hazard objects of the plan, the calm eye's ring, and the player's receiver.</summary>
        private void BuildHazards(CubeWorld world)
        {
            Transform target = ResolvePlayer();
            if (target != null && target.GetComponent<HazardReceiver>() == null)
            {
                target.gameObject.AddComponent<HazardReceiver>();
                Game.Tracer.PlayerMover mover = target.GetComponent<Game.Tracer.PlayerMover>();
                if (mover != null) mover.RefreshModifier();
            }
            foreach (HazardSpec h in Plan.Hazards)
            {
                if (!h.IsPlaced) continue;
                Vector2 centre = world.ScreenCenter(h.Screen);
                var area = new Rect(centre + h.Area.position, h.Area.size);
                switch (h.Kind)
                {
                    case HazardKind.Wind:
                        zones.Add(WindBand.Create(root, h.Screen, area, h.Direction, h.Strength, world.Material));
                        break;
                    case HazardKind.Snow:
                        zones.Add(SnowPatch.Create(root, h.Screen, area, h.Strength, world.Material));
                        break;
                    case HazardKind.FreezingRain:
                        zones.Add(FreezingRain.Create(root, h.Screen, area, h.Strength, world.Material));
                        break;
                    default:
                        crevices.Add(QuakeCrevice.Create(root, h.Screen, area, world.Material));
                        break;
                }
            }
            var eye = new GameObject("Calm Eye").transform;
            eye.SetParent(root, false);
            eye.position = world.ScreenCenter(Plan.Eye);
            ArtCatalog.AddShape(eye, "Ring", PartShape.Circle, Vector2.zero, new Vector2(7f, 7f), EyeColor, -9, world.Material);
            decor.Add(eye);
        }

        private void BuildTrial(CubeWorld world)
        {
            ScreenModule module = world.ModuleAt(Plan.TrialScreen);
            if (module == null || Wand == null || Plan.Rolled == Phenomenon.None || Plan.TrialSpots == null ||
                Plan.TrialSpots.Count < EarthTrial.PieceCount)
            {
                Debug.LogWarning($"EarthFace: no room for the trial on {Plan.TrialScreen} (seed {world.Seed}).");
                return;
            }
            Vector2 centre = world.ScreenCenter(Plan.TrialScreen);
            var positions = new List<Vector2>();
            foreach (Vector2 local in Plan.TrialSpots) positions.Add(centre + local);
            var go = new GameObject("Earth Trial");
            go.transform.SetParent(module.transform, false);
            go.transform.position = centre;
            Trial = go.AddComponent<EarthTrial>();
            Trial.Configure(Plan.Rolled, Wand, positions, EarthPlan.TrialCurrency, world.Material);
            Trial.Completed += currency => Debug.Log($"Earth trial complete: +{currency} (seed {world.Seed}).");
        }

        private List<ScreenAddress> ShuffledScreens(SeededRng rng) => FaceContent.ShuffledScreens(Plan.Face, Plan.FaceSize, rng);

        private void SpawnDinosaurs(CubeWorld world, FaceSpots spots, SeededRng rng)
        {
            List<ScreenAddress> screens = ShuffledScreens(rng);
            RunFacts facts = RunFacts.Of(world);
            Transform target = ResolvePlayer();
            FaceContent.SpawnPopulation(world, spots, screens, EarthPopulation.DinoCount(Plan.FaceSize), i => EarthPopulation.DinoSpec(world.Seed, i),
                (spec, i) => EarthPopulation.LinesFor(spec, facts, hintDensity, Plan.Rolled, i), "EarthFace", "Earth Dinosaur", target, root, dinosaurs);
        }

        private void SpawnEarthEnemies(CubeWorld world, FaceSpots spots, SeededRng rng)
        {
            List<ScreenAddress> screens = ShuffledScreens(rng);
            Transform target = ResolvePlayer();
            FaceContent.SpawnEnemies(world, spots, screens, EarthEnemyKind.Squall, SquallCount(Plan.FaceSize), EarthEnemyKind.Rumbler, RumblerCount(Plan.FaceSize),
                (kind, at, bounds) => EarthEnemies.Spawn(kind, EarthEnemies.WeaknessFor(kind, world), at, bounds, target, root, world.Material),
                enemies);
        }

        private Transform ResolvePlayer() => FaceContent.ResolvePlayer(ref player);

        private void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
        }
    }
}
