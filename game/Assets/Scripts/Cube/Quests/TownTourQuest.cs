using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Mayor Muddleton's tour of Town in play (4.20; the plan is CubeWorld.TownTour, the progress the run's QuestLog.Tour).
    /// TownPopulation binds it after every population, so a re-populated Town (the reveal) carries on.
    /// <list type="number">
    /// <item>The Mayor's first talk (LeaderQuest asks MayorTalk while Town's fragment is out) ends in a question: shall he show
    /// the player round? Yes starts the tour (it joins the quest log); no hands Town's fragment over at once. Esc leaves it
    /// unanswered, and he asks again next time.</item>
    /// <item>The tour lights one glowing marker at a time (TownTour.Stops, a ring of Town's screens). Walking onto it says the
    /// stop's line (its sign, TownTourText) and sets its tiny task; once the task is done (and no conversation is open) the
    /// next marker lights and the quest log moves on. The item stop is passed over unless the player holds two items.</item>
    /// <item>The last stop (by one of Town's arches) hands Town's fragment over; the Mayor's usual hand-in then opens Town's
    /// temple. Mid-tour, the Mayor asks whether to skip the rest: yes hands the fragment over at once.</item>
    /// </list>
    /// Nothing here blocks the way: the markers, the heap of dirt and the chest are triggers, the guide stands off the lanes,
    /// and Town's arches are untouched, so the player can always leave Town; and the Mayor can always be asked to skip.
    /// </summary>
    public class TownTourQuest : MonoBehaviour
    {
        /// <summary>The guide's index among Town's townsfolk parts (TownPlan.TownsfolkParts), clear of the greeter's 0.</summary>
        public const int GuidePartsIndex = 777;

        private const float K = ScreenMath.ContentScale;

        /// <summary>The marker's drawn size (world units): the glow is the arrival circle.</summary>
        public const float GlowSize = 2f * TownTour.ArriveRadius;

        /// <summary>The stop sign's scale (its 14x20 pixel art at 8 px a unit: about 1 by 1.5 units).</summary>
        public const float SignScale = 0.6f;

        private CubeWorld world;
        private TownTour tour;
        private TownSpot guideSpot;
        private Transform player;
        private QuestLog builtFor;
        private Transform root;
        private readonly List<GameObject> markers = new List<GameObject>();
        private NpcTalker guide;
        private RewardMound mound;
        private RewardChest chest;
        private CubeMap map;
        private QuestLogView questLog;
        private bool sawTask;
        private ItemDefinition heldAtArrival;

        public TownTour Tour => tour;

        /// <summary>The guide at the Talk stop (null until the tour starts).</summary>
        public NpcTalker Guide => guide;

        /// <summary>The Dig stop's heap of dirt (null until the tour starts).</summary>
        public RewardMound Mound => mound;

        /// <summary>The Chest stop's chest (null until the tour starts).</summary>
        public RewardChest Chest => chest;

        /// <summary>Each stop's marker, in the tour's order (empty until the tour starts); only the current one is shown.</summary>
        public IReadOnlyList<GameObject> Markers => markers;

        /// <summary>Where the guide stands this run (TownTour.GuideSpot over the run's planned Town spots).</summary>
        public TownSpot GuideSpot => guideSpot;

        public TownTourProgress Progress => world != null && world.Quests != null ? world.Quests.Tour : null;

        /// <summary>True once bound to a run with a tour.</summary>
        public bool Ready => world != null && tour != null && Progress != null;

        /// <summary>True while the tour runs (started, not skipped, its fragment not yet given).</summary>
        public bool Running => Ready && Progress.Running;

        /// <summary>The stop the tour is at (its marker shown), or null when it is not running.</summary>
        public TourStop? Current => Running && Progress.Stage < tour.Stops.Count ? tour.Stops[Progress.Stage] : (TourStop?)null;

        /// <summary>Raised when a stop's task is done and the tour moves on (with the stop just finished).</summary>
        public event System.Action<TourStop> StopDone;

        /// <summary>Binds the run's tour to the current Town population (the guide's spot from the run's planned spots).</summary>
        public void Bind(CubeWorld runWorld, TownTour runTour, TownSpot guideAt, Transform playerTransform)
        {
            world = runWorld;
            tour = runTour;
            guideSpot = guideAt;
            if (playerTransform != null) player = playerTransform;
            QuestLog log = world != null ? world.Quests : null;
            if (!ReferenceEquals(builtFor, log)) DestroyProps(); // a new run: the old run's tour goes
            if (Running && root == null) BuildProps();
            else if (!Running) RetireProps();
            ShowCurrentMarker();
        }

        private void OnDestroy() => DestroyProps();

        private QuestLog Log => world != null ? world.Quests : null;

        private string WhereOf(TourStop stop) => QuestLines.Where(stop.Screen, tour.FaceSize);

        // ------------------------------------------------------------------ the Mayor

        /// <summary>
        /// What the Mayor says while Town's fragment is out and the tour can run: before the tour, his greeting and the question
        /// (with act, its answers: yes starts the tour, no hands the fragment over); mid-tour, where the next stop is and whether
        /// to skip the rest (yes hands the fragment over). The question comes back in choice (null without act).
        /// </summary>
        public List<string> MayorTalk(Race race, bool act, Vector2 at, out QuestStep step, out DialogueChoice choice)
        {
            TownTourProgress progress = Progress;
            choice = null;
            if (!progress.Started)
            {
                step = QuestStep.Offered;
                if (act)
                {
                    progress.Offered = true;
                    choice = new DialogueChoice(yes => yes ? StartTour(at) : Decline(at));
                }
                return new List<string>(Dialogue.MaxLines) { Dialogue.LeaderLine(race), TownTourText.Offer };
            }
            step = QuestStep.Touring;
            if (act) choice = new DialogueChoice(yes => yes ? SkipRest(at) : KeepGoing());
            return new List<string>(Dialogue.MaxLines) { TownTourText.Reminder(WhereNext()), TownTourText.SkipAsk };
        }

        /// <summary>Where the next stop is (the current one's screen), worded for the lines.</summary>
        public string WhereNext()
        {
            TourStop? stop = Current;
            return stop.HasValue ? WhereOf(stop.Value) : "in the town square";
        }

        /// <summary>Yes to the tour: it starts at its first stop, its things are built, and it joins the quest log.</summary>
        public List<string> StartTour(Vector2 at)
        {
            TownTourProgress progress = Progress;
            if (progress == null || progress.Started || progress.FragmentGiven) return new List<string>();
            progress.Started = true;
            progress.Stage = 0;
            progress.Arrived = false;
            progress.TaskDone = false;
            if (root == null) BuildProps();
            PassOverItemsIfNotHeld();
            QuestLog log = Log;
            if (log != null)
            {
                QuestEntry entry = log.Add(QuestLog.TownId, QuestKind.TownSide, CubeModel.StartFace, NpcNames.Leader(Race.Townsfolk),
                    QuestText.TourTitle, StepNow());
                QuestNotice.Added(entry, at);
            }
            ShowCurrentMarker();
            return new List<string> { TownTourText.Start(WhereNext()) };
        }

        /// <summary>No to the tour: Town's fragment at once (it joins the quest log as Town's fragment, to hand back).</summary>
        public List<string> Decline(Vector2 at)
        {
            TownTourProgress progress = Progress;
            if (progress == null || progress.Started || progress.FragmentGiven) return new List<string>();
            if (!GiveFragment(at)) return new List<string>(); // 4.20 review: nothing is recorded unless the fragment is really given
            progress.Declined = true;
            QuestLog log = Log;
            if (log != null)
            {
                QuestEntry entry = log.Add(QuestLog.TownId, QuestKind.TownSide, CubeModel.StartFace, NpcNames.Leader(Race.Townsfolk),
                    QuestText.TownTitle, QuestText.TownReturn);
                QuestNotice.Added(entry, at + new Vector2(0f, LeaderQuest.PopupStep));
            }
            return new List<string> { TownTourText.Declined };
        }

        /// <summary>Mid-tour, yes to skipping: the rest of the tour goes, and Town's fragment is the player's at once.</summary>
        public List<string> SkipRest(Vector2 at)
        {
            TownTourProgress progress = Progress;
            if (progress == null || !progress.Running) return new List<string>();
            if (!GiveFragment(at)) return new List<string>(); // 4.20 review: skipped only once the fragment is the player's
            progress.Skipped = true;
            RetireProps();
            ShowCurrentMarker();
            return new List<string> { TownTourText.Skipped };
        }

        private List<string> KeepGoing() => new List<string> { TownTourText.KeepGoing(WhereNext()) };

        /// <summary>
        /// Hands Town's fragment to the player (once): the popup, and the quest log's step to hand it back. True when the
        /// player holds it afterwards (false: no player rewards to give it to, so nothing changed).
        /// </summary>
        private bool GiveFragment(Vector2 at)
        {
            TownTourProgress progress = Progress;
            RunRewards rewards = PlayerRewards();
            if (progress == null || rewards == null) return false;
            if (progress.FragmentGiven) return true;
            if (!rewards.HasFragment(CubeModel.StartFace, 1) && !rewards.Grant(Reward.Fragment(1), CubeModel.StartFace)) return false;
            progress.FragmentGiven = true;
            PickupPopup.Show(at, RewardText.FragmentPopup(Theme.Town, 1, 1), RewardChest.PopupColor, 1.6f);
            Log?.SetStep(QuestLog.TownId, QuestText.TownReturn);
            return true;
        }

        /// <summary>
        /// Once the tour is over (finished or skipped; 4.20 review), its things stop teaching: the guide says ordinary town
        /// lines, and a heap or chest left untouched goes (the chest pays only while the tour runs). Ones already dug or opened
        /// stay as they are. Safe to call again.
        /// </summary>
        private void RetireProps()
        {
            if (root == null) return;
            if (guide != null && world != null)
                guide.SetLines(Dialogue.For(guide.Spec, RunFacts.Of(world), HintGenerator.FirstRunDensity));
            if (mound != null && !mound.IsOpen)
            {
                Destroy(mound.gameObject);
                mound = null;
            }
            if (chest != null && !chest.IsOpen)
            {
                Destroy(chest.gameObject);
                chest = null;
            }
        }

        // ------------------------------------------------------------------ the stops

        /// <summary>The quest log's step for where the tour is now.</summary>
        private string StepNow()
        {
            TourStop? stop = Current;
            return stop.HasValue ? QuestText.TourStep(stop.Value.Lesson, WhereOf(stop.Value), Progress.Arrived) : QuestText.TownReturn;
        }

        /// <summary>The item stop comes up only for a player holding two items or more (switching needs two); else it is passed over.</summary>
        private void PassOverItemsIfNotHeld()
        {
            TourStop? stop = Current;
            if (stop.HasValue && stop.Value.Lesson == TourLesson.Items && !ItemsApply()) Progress.Stage++;
        }

        /// <summary>True when the player holds two items or more (the item stop's lesson applies).</summary>
        public bool ItemsApply()
        {
            Inventory inventory = PlayerTransform() != null ? PlayerTransform().GetComponent<Inventory>() : null;
            return inventory != null && inventory.Items.Count >= 2;
        }

        private void Update()
        {
            if (!Running)
            {
                if (markers.Count > 0 && AnyMarkerShown()) ShowCurrentMarker();
                return;
            }
            if (Progress.Stage >= tour.Stops.Count) return; // 4.20 review: past the last stop, waiting only for the fragment
            TourStop stop = tour.Stops[Progress.Stage];
            Pulse(stop);
            TownTourProgress progress = Progress;
            if (!progress.Arrived)
            {
                if (GamePause.Frozen || !PlayerAt(stop)) return;
                Arrive(stop);
                return;
            }
            if (!progress.TaskDone && TaskDone(stop)) progress.TaskDone = true;
            if (progress.TaskDone && !GamePause.Frozen) CompleteStop(stop);
        }

        /// <summary>True when the player stands on the stop's marker (on its screen, not in a building).</summary>
        private bool PlayerAt(TourStop stop)
        {
            Transform p = PlayerTransform();
            if (p == null || world == null) return false;
            CubeNavigator navigator = p.GetComponent<CubeNavigator>();
            if (navigator != null && navigator.InInterior) return false;
            Vector2 marker = world.ScreenCenter(stop.Screen) + stop.Marker;
            return Vector2.Distance(p.position, marker) <= TownTour.ArriveRadius;
        }

        /// <summary>The player reached a stop: its line, its task in the quest log.</summary>
        private void Arrive(TourStop stop)
        {
            TownTourProgress progress = Progress;
            progress.Arrived = true;
            sawTask = false;
            Transform p = PlayerTransform();
            Equipment equipment = p != null ? p.GetComponent<Equipment>() : null;
            heldAtArrival = equipment != null ? equipment.Equipped : null;
            Log?.SetStep(QuestLog.TownId, StepNow());
            DialogueBox.Shared.Open(TownTourText.Speaker, new[] { TownTourText.StopLine(stop.Lesson) }, this);
        }

        /// <summary>Whether a stop's tiny task is done (a thing the tour could not build counts as done, never a soft-lock).</summary>
        private bool TaskDone(TourStop stop)
        {
            switch (stop.Lesson)
            {
                case TourLesson.Talk: return Progress.GuideTalked || guide == null;
                case TourLesson.Dig: return mound == null || mound.IsOpen;
                case TourLesson.Chest: return chest == null || chest.IsOpen;
                case TourLesson.Map:
                    if (map == null) map = FindAnyObjectByType<CubeMap>();
                    if (map != null && map.IsOpen) sawTask = true;
                    return sawTask || map == null;
                case TourLesson.QuestLog:
                    if (questLog == null) questLog = FindAnyObjectByType<QuestLogView>();
                    if (questLog != null && questLog.IsOpen) sawTask = true;
                    return sawTask || questLog == null;
                case TourLesson.Items:
                {
                    Transform p = PlayerTransform();
                    Equipment equipment = p != null ? p.GetComponent<Equipment>() : null;
                    return equipment == null || equipment.Equipped != heldAtArrival;
                }
                default: return true; // Move and Locks: getting there is the task
            }
        }

        /// <summary>A stop's task is done: the next stop lights (the last one hands Town's fragment over).</summary>
        private void CompleteStop(TourStop stop)
        {
            TownTourProgress progress = Progress;
            Vector2 at = PlayerTransform() != null ? (Vector2)PlayerTransform().position : world.ScreenCenter(stop.Screen);
            if (progress.Stage + 1 >= tour.Stops.Count)
            {
                // The last stop: the tour ends only once the fragment is really the player's (else it tries again next frame).
                if (!GiveFragment(at)) return;
                progress.Arrived = false;
                progress.TaskDone = false;
                progress.Stage = tour.Stops.Count;
                RetireProps();
                ShowCurrentMarker();
                StopDone?.Invoke(stop);
                return;
            }
            progress.Arrived = false;
            progress.TaskDone = false;
            progress.Stage++;
            PassOverItemsIfNotHeld();
            Log?.SetStep(QuestLog.TownId, StepNow());
            PickupPopup.Show(at, TownTourText.NextPopup(WhereNext()), QuestNotice.PopupColor, 2.5f);
            ShowCurrentMarker();
            StopDone?.Invoke(stop);
        }

        // ------------------------------------------------------------------ the things

        /// <summary>The world position of a stop's marker.</summary>
        public Vector2 MarkerPosition(TourStop stop) => world.ScreenCenter(stop.Screen) + stop.Marker;

        /// <summary>Builds the tour's markers, guide, heap of dirt and chest (once per run, when the tour starts).</summary>
        private void BuildProps()
        {
            if (world == null || tour == null || world.Model == null) return;
            DestroyProps();
            builtFor = world.Quests;
            root = new GameObject("Town Tour").transform;
            foreach (TourStop stop in tour.Stops) markers.Add(BuildMarker(stop));

            // The guide, standing still at the Talk stop (no race relations: Town's folk never turn hostile).
            if (guideSpot.Found)
            {
                NpcSpec spec = GuideSpec(world.Seed);
                Vector2 feet = world.ScreenCenter(guideSpot.Screen) + guideSpot.Local;
                var bounds = new Rect(feet - Vector2.one * 0.05f, Vector2.one * 0.1f);
                guide = NpcFactory.Spawn(spec, TownTourText.GuideLines, feet, bounds, null, PlayerTransform(), root, world.Material);
                guide.name = $"Tour Guide {spec.DisplayName}";
                guide.Talking += _ =>
                {
                    TownTourProgress progress = Progress;
                    if (progress != null) progress.GuideTalked = true;
                };
            }

            TourStop dig = tour.StopOf(TourLesson.Dig);
            Vector2 moundAt = world.ScreenCenter(dig.Screen) + dig.Prop;
            GameObject heap = world.CreateBlock("Tour Mound", root, moundAt, Vector2.one * RewardMound.Size, Color.white, 7, false, ArtKey.Mound);
            var heapTrigger = heap.AddComponent<CircleCollider2D>();
            heapTrigger.isTrigger = true;
            heapTrigger.radius = RewardMound.Radius;
            mound = heap.AddComponent<RewardMound>();

            TourStop box = tour.StopOf(TourLesson.Chest);
            Vector2 chestAt = world.ScreenCenter(box.Screen) + box.Prop;
            GameObject chestObject = world.CreateBlock("Tour Chest", root, chestAt, Vector2.one * RewardChest.Size, Color.white, 5, false, ArtKey.Chest);
            var chestTrigger = chestObject.AddComponent<CircleCollider2D>();
            chestTrigger.isTrigger = true;
            chestTrigger.radius = RewardChest.Radius;
            chest = chestObject.AddComponent<RewardChest>();
            chest.Spot = new RewardSpot(-1, new Roadblock(RoadblockKind.Mound, box.Screen, Roadblock.LaneSpot), Reward.Jumbles(TownTour.ChestJumbles), false);
            chest.Theme = Theme.Town;
            chest.SetArt(chestObject.transform.Find("Visual").GetComponent<SpriteRenderer>(), null);
            chest.Opened += (_, collector) =>
            {
                if (!Running) return; // 4.20 review: the tour's chest pays only while the tour runs
                RunWallet wallet = FindAnyObjectByType<RunWallet>();
                if (wallet != null) wallet.Add(EarningSource.Chest, "chest:tour", TownTour.ChestJumbles);
            };
            ShowCurrentMarker();
        }

        /// <summary>The guide's spec: a townsperson (a hooded traveller), standing still, named Gus Guidewell.</summary>
        public static NpcSpec GuideSpec(int seed)
        {
            NpcSpec spec = TownPlan.TownsfolkParts(seed, GuidePartsIndex);
            foreach (NpcPart legs in RacePartSets.For(Race.Townsfolk).Legs)
                if (legs.Movement == Movement.Stand)
                {
                    spec = spec.With(legs);
                    break;
                }
            return spec.Named(NpcNames.Guide, NpcNames.GuideKind);
        }

        /// <summary>A stop's marker: a glow on the floor (the arrival circle) and a little sign with a flag beside it.</summary>
        private GameObject BuildMarker(TourStop stop)
        {
            var go = new GameObject($"Tour Marker {(int)stop.Lesson + 1} ({stop.Lesson})");
            go.transform.SetParent(root, false);
            go.transform.position = MarkerPosition(stop);
            var glow = new GameObject("Glow");
            glow.transform.SetParent(go.transform, false);
            var glowRenderer = glow.AddComponent<SpriteRenderer>();
            glowRenderer.sprite = TownArt.TourGlow();
            Vector2 glowSize = glowRenderer.sprite.bounds.size;
            glow.transform.localScale = new Vector3(GlowSize / glowSize.x, GlowSize / glowSize.y, 1f);
            if (world.Material != null) glowRenderer.sharedMaterial = world.Material;
            glowRenderer.sortingOrder = -4; // on the floor, under people and things
            var sign = new GameObject("Sign");
            sign.transform.SetParent(go.transform, false);
            sign.transform.localPosition = new Vector3(0.7f * K, 0.45f * K, 0f);
            var signRenderer = sign.AddComponent<SpriteRenderer>();
            signRenderer.sprite = TownArt.TourSign();
            sign.transform.localScale = Vector3.one * SignScale; // about one tile wide, a tile and a half tall
            if (world.Material != null) signRenderer.sharedMaterial = world.Material;
            signRenderer.sortingOrder = 5;
            go.SetActive(false);
            return go;
        }

        /// <summary>Shows only the current stop's marker (none once the tour is over).</summary>
        private void ShowCurrentMarker()
        {
            int current = Running ? Progress.Stage : -1;
            for (int i = 0; i < markers.Count; i++)
                if (markers[i] != null) markers[i].SetActive(i == current);
        }

        private bool AnyMarkerShown()
        {
            foreach (GameObject m in markers)
                if (m != null && m.activeSelf) return true;
            return false;
        }

        /// <summary>The current marker's glow breathes (unscaled time, so it still glows while the map waits).</summary>
        private void Pulse(TourStop stop)
        {
            int i = Progress.Stage;
            if (i >= markers.Count || markers[i] == null) return;
            if (!markers[i].activeSelf) ShowCurrentMarker();
            Transform glow = markers[i].transform.Find("Glow");
            if (glow == null) return;
            float breathe = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 4f);
            SpriteRenderer r = glow.GetComponent<SpriteRenderer>();
            Vector2 size = r.sprite.bounds.size;
            glow.localScale = new Vector3(GlowSize * breathe / size.x, GlowSize * breathe / size.y, 1f);
        }

        private void DestroyProps()
        {
            markers.Clear();
            guide = null;
            mound = null;
            chest = null;
            builtFor = null;
            if (root == null) return;
            root.gameObject.SetActive(false);
            Destroy(root.gameObject);
            root = null;
        }

        private Transform PlayerTransform()
        {
            if (player != null) return player;
            var navigator = FindAnyObjectByType<CubeNavigator>();
            if (navigator != null) player = navigator.transform;
            return player;
        }

        private RunRewards PlayerRewards()
        {
            Transform p = PlayerTransform();
            return p != null ? p.GetComponentInParent<RunRewards>() : null;
        }
    }
}
