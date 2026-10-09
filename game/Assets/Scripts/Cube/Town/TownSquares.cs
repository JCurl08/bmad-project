using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The people of the science faces' towns (4.5), spawned at the reveal once every face's own content is in place: on
    /// each built science face with a town, its race's leader (TownSquare.LeaderSpec) stands by the statue and greets the
    /// player with the region's quest hook (Dialogue.LeaderConversation), and TownSquare.ResidentCount residents of its race
    /// stand on the town's stands (TownLayout.Stands) and talk with their usual run-true hints. So a town is always its
    /// face's busiest screen, and no enemy is ever spawned there (the faces skip it). The Town face's own town is
    /// TownPopulation's (its first NPCs and Mayor Muddleton). Everything is removed on a rebuild.
    /// Each leader carries their face's quest (LeaderQuest, 4.8): bring all the face's fragments to mend it.
    /// </summary>
    public class TownSquares : MonoBehaviour, IHintDensityTarget
    {
        /// <summary>How far a leader may shuffle from their stand by the statue (each way, world units).</summary>
        public static readonly Vector2 LeaderRoom = new Vector2(0.3f, 0.2f);

        [SerializeField] private CubeWorld world;
        [SerializeField] private Transform player;
        [SerializeField] private HintDensity hintDensity = HintGenerator.FirstRunDensity;

        private readonly Dictionary<FaceId, NpcTalker> leaders = new Dictionary<FaceId, NpcTalker>();
        private readonly List<NpcTalker> residents = new List<NpcTalker>();
        private readonly Dictionary<FaceId, ErrandPlan> errands = new Dictionary<FaceId, ErrandPlan>();
        private Transform root;
        private CubeWorld subscribed;

        public CubeWorld World
        {
            get => world;
            set
            {
                world = value;
                Subscribe();
            }
        }

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

        /// <summary>Every science-face town resident this run (destroyed ones become null).</summary>
        public IReadOnlyList<NpcTalker> Residents => residents;

        /// <summary>Every science-face leader this run, by face.</summary>
        public IReadOnlyDictionary<FaceId, NpcTalker> Leaders => leaders;

        /// <summary>Each science face's errand this run (4.18), by face.</summary>
        public IReadOnlyDictionary<FaceId, ErrandPlan> FaceErrands => errands;

        /// <summary>A science face's leader, or null (no town, not revealed yet).</summary>
        public NpcTalker LeaderOf(FaceId face) => leaders.TryGetValue(face, out NpcTalker leader) ? leader : null;

        private void Awake()
        {
            if (world == null) world = GetComponent<CubeWorld>();
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            if (subscribed == null) return;
            subscribed.Rebuilt -= Clear;
            subscribed.Revealed -= Populate;
            subscribed = null;
        }

        private void Subscribe()
        {
            if (!isActiveAndEnabled || world == null || subscribed == world) return;
            OnDisable();
            subscribed = world;
            world.Rebuilt += Clear;
            world.Revealed += Populate;
            if (world.ScienceRevealed && leaders.Count == 0) Populate();
        }

        /// <summary>Removes every town resident and leader of the science faces.</summary>
        public void Clear()
        {
            foreach (NpcTalker leader in leaders.Values)
            {
                if (leader == null) continue;
                leader.gameObject.SetActive(false);
                Destroy(leader.gameObject);
            }
            leaders.Clear();
            errands.Clear();
            FaceContent.DestroyAll(residents);
        }

        /// <summary>Spawns the leader and residents of every revealed science face's town (after a Clear).</summary>
        public void Populate()
        {
            Clear();
            if (world == null || world.Model == null || !TownSquare.HasTown(world.FaceSize)) return;
            if (root == null) root = new GameObject("Town Squares").transform;
            RunFacts facts = RunFacts.Of(world);
            Transform target = FaceContent.ResolvePlayer(ref player);
            CubeModel model = world.Model;
            for (int f = 0; f < CubeSettings.FaceCount; f++)
            {
                var face = (FaceId)f;
                if (!CubeLayout.IsLaidOut(model, face)) continue;
                var screen = new ScreenAddress(face, TownSquare.Cell(world.FaceSize));
                ScreenModule module = world.ModuleAt(screen);
                if (module == null || !module.IsTownScreen) continue;
                Theme theme = model.ThemeOf(face);
                TownLayout layout = TownLayout.For(theme);
                Vector2 centre = world.ScreenCenter(screen);

                NpcSpec leaderSpec = TownSquare.LeaderSpec(world.Seed, theme);
                NpcTalker leader = SpawnLeader(world, leaderSpec, face, centre, layout.LeaderStand, target, root);
                leader.name = $"Town Leader {face} {leaderSpec.DisplayName}";
                leaders[face] = leader;

                // The face's one errand (4.18, Errands): one resident gives it, another receives it.
                ErrandPlan errand = Errands.For(world.Seed, world.FaceSize, face, theme);
                NpcTalker giver = null, receiver = null;
                for (int i = 0; i < TownSquare.ResidentCount && i < layout.Stands.Count; i++)
                {
                    NpcSpec spec = errand != null && i == errand.GiverIndex ? errand.Giver : TownSquare.ResidentSpec(world.Seed, theme, i);
                    Vector2 feet = layout.Stands[i];
                    Rect quadrant = TownPlan.QuadrantBounds(feet);
                    NpcTalker npc = NpcFactory.Spawn(spec, Dialogue.For(spec, facts, hintDensity, errand?.Giver.Name), centre + feet,
                        new Rect(centre + quadrant.position, quadrant.size), world.Relations, target, root, world.Material);
                    npc.name = $"Town Resident {face} {i} {spec.DisplayName}";
                    residents.Add(npc);
                    if (errand != null && i == errand.GiverIndex) giver = npc;
                    if (errand != null && i == errand.TargetIndex) receiver = npc;
                }
                if (giver != null && receiver != null)
                {
                    ErrandQuest.Attach(giver, world, errand, true, giver.Lines);
                    ErrandQuest.Attach(receiver, world, errand, false, receiver.Lines);
                    errands[face] = errand;
                }
            }
        }

        /// <summary>
        /// A town's leader at their stand by the statue, keeping close to it (LeaderRoom), with their face's quest (LeaderQuest,
        /// 4.8): the greeting, then the quest naming the face's fragments; talking hands them in.
        /// </summary>
        public static NpcTalker SpawnLeader(CubeWorld world, NpcSpec spec, FaceId face, Vector2 screenCentre, Vector2 stand, Transform player,
            Transform parent, bool peaceful = false)
        {
            Vector2 at = screenCentre + stand;
            var bounds = new Rect(at - LeaderRoom, LeaderRoom * 2f);
            // peaceful (the Mayor, 4.18 review): no race relations, so never hostile.
            NpcTalker leader = NpcFactory.Spawn(spec, TownSquare.LeaderLines(spec.Race), at, bounds, peaceful ? null : world.Relations, player,
                parent, world.Material);
            LeaderQuest.Attach(leader, world, face);
            return leader;
        }

        private void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
        }
    }
}
