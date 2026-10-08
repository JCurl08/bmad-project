using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Fills the Town face at every run start: one NPC of each face race plus a per-screen share of Townsfolk (TownPlan: the whole Town population scales per screen, 11-15 Townsfolk at 3x3), on
    /// seeded spots that are clear of the module's walls and alcoves, the exit lanes and the screen centre,
    /// and confirmed free with NpcFactory.IsClear. Each NPC talks with sparse first-run hints that are true
    /// for the run (RunFacts.Of works before the reveal); the greeter adds the TownSign welcome. Each NPC
    /// moves only inside its own quadrant, so it never wanders into an exit lane.
    /// The first population happens in Start; after a rebuild the old NPCs are removed at once and the new
    /// ones spawn on the next frame (when everything the rebuild destroyed is really gone). The reveal
    /// rebuilds the population at once (same seed: same NPCs on the same spots) with post-reveal lines.
    /// No draft or shop logic: Epic 2 reads Npcs.
    /// On a face with a town (4.5) the start screen is Town's town: its first NPCs stand on the town's stands and Mayor
    /// Muddleton (Leader) by its statue, so it is the Town face's busiest screen.
    /// </summary>
    public class TownPopulation : MonoBehaviour, IHintDensityTarget
    {
        [SerializeField] private CubeWorld world;
        [SerializeField] private Transform player;
        [SerializeField] private HintDensity hintDensity = HintGenerator.FirstRunDensity;

        private readonly List<NpcTalker> npcs = new List<NpcTalker>();
        private readonly List<TownSpot> spots = new List<TownSpot>();
        private readonly List<NpcSpec> specs = new List<NpcSpec>();
        private Transform root;
        private bool pending;
        private int pendingAfterFrame;

        public CubeWorld World
        {
            get => world;
            set => world = value;
        }

        public Transform Player
        {
            get => player;
            set => player = value;
        }

        /// <summary>Density of the next population's hints (the RunLoop sets it from the saved run count).</summary>
        public HintDensity HintDensity
        {
            get => hintDensity;
            set => hintDensity = value;
        }

        /// <summary>The Town NPCs of this run, in TownPlan.Specs order (greeter first); destroyed ones are null.</summary>
        public IReadOnlyList<NpcTalker> Npcs => npcs;

        /// <summary>Where each NPC in Npcs was spawned (same order).</summary>
        public IReadOnlyList<TownSpot> Spots => spots;

        /// <summary>The spec each NPC in Npcs was spawned from (same order); TownPlan.IsGreeter finds the greeter.</summary>
        public IReadOnlyList<NpcSpec> Specs => specs;

        /// <summary>
        /// The Town's leader, Mayor Muddleton (4.5), by the statue of Town's town on the start screen, greeting the player with
        /// the quest hook; null on a Town face without a town, or before the first population. Not in Npcs.
        /// </summary>
        public NpcTalker Leader { get; private set; }

        /// <summary>The seed the current population was built for, or null before the first population.</summary>
        public int? PopulatedSeed { get; private set; }

        /// <summary>True while a rebuild has cleared the town and the new population waits for the next frame.</summary>
        public bool Pending => pending;

        private void OnEnable()
        {
            if (world == null) return;
            world.Rebuilt += OnRebuilt;
            world.Revealed += OnRevealed;
        }

        private void OnDisable()
        {
            if (world == null) return;
            world.Rebuilt -= OnRebuilt;
            world.Revealed -= OnRevealed;
        }

        private void Start()
        {
            if (world != null && world.Model != null) Populate();
        }

        private void Update()
        {
            if (pending && Time.frameCount > pendingAfterFrame) Populate();
        }

        private void OnRebuilt()
        {
            Clear();
            pending = true;
            pendingAfterFrame = Time.frameCount;
        }

        /// <summary>The reveal rebuilds the town: same seed, so the same NPCs on the same spots, with post-reveal lines.</summary>
        private void OnRevealed()
        {
            if (world == null || world.Model == null) return;
            Populate();
        }

        /// <summary>Removes the current Town NPCs (deactivated at once, so their colliders stop counting, then destroyed).</summary>
        public void Clear()
        {
            foreach (NpcTalker npc in npcs)
            {
                if (npc == null) continue;
                npc.gameObject.SetActive(false);
                Destroy(npc.gameObject);
            }
            npcs.Clear();
            spots.Clear();
            specs.Clear();
            if (Leader != null)
            {
                Leader.gameObject.SetActive(false);
                Destroy(Leader.gameObject);
            }
            Leader = null;
            PopulatedSeed = null;
        }

        /// <summary>Clears the town and spawns the current run's population now. Returns the spawned NPCs.</summary>
        public IReadOnlyList<NpcTalker> Populate()
        {
            Clear();
            pending = false;
            if (world == null || world.Model == null) return npcs;
            if (root == null) root = new GameObject("Town NPCs").transform;

            int seed = world.Seed;
            int faceSize = world.FaceSize;
            List<NpcSpec> planSpecs = TownPlan.Specs(seed, faceSize);
            List<TownSpot> planned = TownPlan.Spots(seed, faceSize, planSpecs.Count, BlockedFor,
                (screen, local) => NpcFactory.IsClear(world.ScreenCenter(screen) + local, TownPlan.Margin), IsTownSquare);
            RunFacts facts = RunFacts.Of(world);
            Transform target = ResolvePlayer();

            // Mayor Muddleton stands by the statue of Town's town (4.5), on the start screen.
            var square = new ScreenAddress(CubeModel.StartFace, CubeModel.StartCell(faceSize));
            if (IsTownSquare(square.Cell))
            {
                NpcSpec mayor = TownSquare.LeaderSpec(seed, Theme.Town);
                Leader = TownSquares.SpawnLeader(world, mayor, CubeModel.StartFace, world.ScreenCenter(square), TownLayout.For(Theme.Town).LeaderStand,
                    target, root);
                Leader.name = $"Town Leader {mayor.DisplayName}";
            }

            for (int i = 0; i < planSpecs.Count; i++)
            {
                NpcSpec spec = planSpecs[i];
                TownSpot spot = planned[i];
                if (!spot.Found)
                {
                    Debug.LogError($"TownPopulation: no clear spot for {spec} (seed {seed}).");
                    continue;
                }
                Vector2 centre = world.ScreenCenter(spot.Screen);
                Rect quadrant = TownPlan.QuadrantBounds(spot.Local);
                var bounds = new Rect(centre + quadrant.position, quadrant.size);
                NpcTalker npc = NpcFactory.Spawn(spec, LinesFor(spec, facts), centre + spot.Local, bounds,
                    world.Relations, target, root, world.Material);
                npc.name = $"Town NPC {i} {spec.DisplayName}";
                npcs.Add(npc);
                spots.Add(spot);
                specs.Add(spec);
            }
            PopulatedSeed = seed;
            return npcs;
        }

        private List<string> LinesFor(NpcSpec spec, RunFacts facts)
        {
            List<string> lines = Dialogue.For(spec, facts, hintDensity);
            return TownPlan.IsGreeter(spec) ? TownSign.GreeterLines(lines) : lines;
        }

        /// <summary>True when the Town module placed on a cell is a town square (the start screen's, 4.5), so its stands and Mayor apply.</summary>
        private bool IsTownSquare(Vector2Int cell)
        {
            ScreenModule module = world.ModuleAt(new ScreenAddress(CubeModel.StartFace, cell));
            return module != null && module.IsTownScreen;
        }

        private IReadOnlyList<Rect> BlockedFor(Vector2Int cell)
        {
            ScreenModule module = world.ModuleAt(new ScreenAddress(CubeModel.StartFace, cell));
            return TownPlan.BlockedRects(module);
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
