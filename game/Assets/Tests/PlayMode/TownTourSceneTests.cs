using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Tracer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Game.Cube.Tests.TestInput;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The town tutorial and the dialogue choice (4.20) in the real Cube scene, with real key presses:
    /// <list type="bullet">
    /// <item>the yes/no question: the Mayor's ends his first talk; left/right and up/down (arrows and WASD) pick an answer and
    /// Interact confirms it; the talk pause holds throughout, the player never moves while choosing, and a direction key still
    /// held when the talk closes never walks them off; Esc leaves it unanswered and he asks again; no hands Town's fragment
    /// over at once;</item>
    /// <item>the tour walked stop by stop on two seeds (one holding two items, so the item stop comes up; one not): each
    /// marker reached by walking, its line said, its task done with real keys (talking to the guide, digging the heap,
    /// opening the chest, M, Q, 1/2), the quest log moving on step by step; the last stop by an arch gives Town's fragment,
    /// and the Mayor's hand-in opens Town's temple;</item>
    /// <item>mid-tour: Town's folk, the Mayor and the guide never turn hostile (F4, every race hostile); the player walks over
    /// the tour's heap and chest and out through the doorways beyond, and out of Town through the arch by the last stop; back in
    /// Town the tour carries on; then the Mayor's skip: no keeps the tour on, yes hands the fragment over at once, the markers
    /// go, the guide gets ordinary lines and the untouched heap and chest go.</item>
    /// </list>
    /// </summary>
    public class TownTourSceneTests : InputTestFixture
    {
        private const float K = ScreenMath.ContentScale;
        private const string SceneName = "Cube";

        private Keyboard keyboard;
        private CubeWorld world;
        private CubeNavigator navigator;
        private RunRewards rewards;
        private Rigidbody2D body;
        private TownPopulation town;
        private TownTourQuest tour;
        private CubeMap map;
        private QuestLogView questLog;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        public override void TearDown()
        {
            DialogueBox box = Object.FindAnyObjectByType<DialogueBox>();
            if (box != null) box.Close();
            GamePause.Release(QuestLogView.PauseKey);
            GamePause.Resume();
            Time.timeScale = 1f;
            base.TearDown();
        }

        private IEnumerator Load(int seed)
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            world = Object.FindAnyObjectByType<CubeWorld>();
            navigator = Object.FindAnyObjectByType<CubeNavigator>();
            rewards = navigator.GetComponent<RunRewards>();
            body = navigator.GetComponent<Rigidbody2D>();
            town = Object.FindAnyObjectByType<TownPopulation>();
            map = Object.FindAnyObjectByType<CubeMap>();
            questLog = Object.FindAnyObjectByType<QuestLogView>();
            world.Rebuild(seed);
            yield return null;
            yield return null;
            Assert.AreEqual(seed, town.PopulatedSeed);
            tour = town.Tour;
            Assert.IsNotNull(tour, "Town carries the Mayor's tour");
            Assert.IsTrue(tour.Ready, "bound to the run's tour");
            Assert.AreSame(world.TownTour, tour.Tour);
            navigator.GetComponent<Health>().InvulnerableSeconds = 999f;
        }

        private DialogueBox Box => DialogueBox.Shared;

        private void PlacePlayer(Vector2 at)
        {
            body.position = at;
            body.linearVelocity = Vector2.zero;
            navigator.transform.position = new Vector3(at.x, at.y, navigator.transform.position.z);
            Physics2D.SyncTransforms();
        }

        private Vector2 Pos => body.position;

        /// <summary>Stands the player beside the Mayor (a teleport to the square) and opens a talk with a real Interact press.</summary>
        private static void Log(string what) => Debug.Log("[tour test] " + what);

        private IEnumerator OpenMayor()
        {
            Log("open mayor");
            NpcTalker mayor = town.Leader;
            if (Box.IsOpen) Box.Close();
            if (navigator.Current != world.Model.StartScreen) navigator.TeleportTo(world.Model.StartScreen, Facing.East);
            yield return null;
            PlacePlayer((Vector2)mayor.transform.position + new Vector2(0.3f, 0f) * K);
            yield return null;
            yield return this.Tap(keyboard.eKey, 2);
            Assert.IsTrue(Box.IsOpen, "the Mayor talks");
            Assert.AreSame(mayor, Box.Owner);
        }

        /// <summary>Presses Interact until the question shows (or the talk ends).</summary>
        private IEnumerator PageToQuestion()
        {
            for (int i = 0; i < 4 && Box.IsOpen && !Box.ChoiceOpen; i++) yield return this.Tap(keyboard.eKey, 2);
            Assert.IsTrue(Box.ChoiceOpen, "the talk ends in a question");
        }

        /// <summary>Presses Interact until the box closes.</summary>
        private IEnumerator PressThrough()
        {
            for (int i = 0; i < 6 && Box.IsOpen; i++) yield return this.Tap(keyboard.eKey, 2);
            Assert.IsFalse(Box.IsOpen, "pressing through closes it");
            yield return null;
        }

        // ---------------------------------------------------------------- walking with real keys

        private KeyControl KeyToward(Vector2 delta, bool xAxis) =>
            xAxis ? (delta.x > 0f ? keyboard.dKey : keyboard.aKey) : (delta.y > 0f ? keyboard.wKey : keyboard.sKey);

        /// <summary>Walks along one axis (holding one key) until that coordinate is near target, or stop() holds.</summary>
        private IEnumerator WalkAxis(bool xAxis, float target, System.Func<bool> stop = null)
        {
            Log($"walk {(xAxis ? "x" : "y")} to {target:0.00} from {Pos}");
            float Off() => xAxis ? target - Pos.x : target - Pos.y;
            if (Mathf.Abs(Off()) < 0.2f) yield break;
            KeyControl key = KeyToward(xAxis ? new Vector2(Off(), 0f) : new Vector2(0f, Off()), xAxis);
            float sign = Mathf.Sign(Off());
            yield return this.HoldUntil(key, () => sign * Off() < 0.2f || (stop != null && stop()), 12f);
            Log($"walked to {Pos}");
            // A marker reached opens its line, which stops time: physics steps would never come then.
            if (GamePause.Frozen) yield return null;
            else yield return PhysicsSteps(2);
        }

        /// <summary>Walks to a point in a lane cross, the shorter axis first (both are lanes from a lane), stopping early on stop().</summary>
        private IEnumerator WalkTo(Vector2 target, System.Func<bool> stop = null)
        {
            Vector2 d = target - Pos;
            bool xFirst = Mathf.Abs(d.x) <= Mathf.Abs(d.y);
            yield return WalkAxis(xFirst, xFirst ? target.x : target.y, stop);
            if (stop != null && stop()) yield break;
            yield return WalkAxis(!xFirst, xFirst ? target.y : target.x, stop);
        }

        /// <summary>Walks from wherever the player is in a Town screen's lanes to a stop's marker, screen by screen round the ring.</summary>
        private IEnumerator WalkToStop(TourStop stop)
        {
            Assert.IsTrue(world.TryScreenAt(Pos, out ScreenAddress here), "on a screen");
            // Back to this screen's centre, then centre to centre (each next screen one doorway on) to the stop's screen.
            yield return WalkTo(world.ScreenCenter(here), () => Box.IsOpen);
            List<ScreenAddress> path = RingPath(here, stop.Screen);
            foreach (ScreenAddress screen in path)
            {
                if (Box.IsOpen) break;
                yield return WalkTo(world.ScreenCenter(screen), () => Box.IsOpen);
            }
            if (!Box.IsOpen) yield return WalkTo(tour.MarkerPosition(stop), () => Box.IsOpen);
            yield return WaitUntilOrTimeout(() => Box.IsOpen, 2f);
        }

        /// <summary>The screens from one ring screen (or the square) to another, one doorway at a time, round the tour's way.</summary>
        private List<ScreenAddress> RingPath(ScreenAddress from, ScreenAddress to)
        {
            var path = new List<ScreenAddress>();
            Vector2Int square = CubeModel.StartCell(world.FaceSize);
            int Index(ScreenAddress s) => TownTour.Ring.ToList().IndexOf(s.Cell - square);
            int i = Index(from), j = Index(to);
            Assert.GreaterOrEqual(j, 0, $"{to} is on the ring");
            if (i < 0)
            {
                path.Add(to);
                return path;
            }
            int dir = world.TownTour.Direction;
            while (i != j)
            {
                i = ((i + dir) % 8 + 8) % 8;
                path.Add(new ScreenAddress(CubeModel.StartFace, square + TownTour.Ring[i]));
            }
            return path;
        }

        // ---------------------------------------------------------------- the choice

        [UnityTest]
        public IEnumerator TheMayorsQuestion_IsPickedWithArrowsAndWasd_ConfirmedWithInteract_HoldsThePause_AndNoKeyLeaksIntoMovement()
        {
            yield return Load(7);
            // Esc leaves it unanswered: nothing given, and he asks again.
            yield return OpenMayor();
            yield return PageToQuestion();
            yield return this.Tap(keyboard.escapeKey, 2);
            Assert.IsFalse(Box.IsOpen, "Esc closes the question unanswered");
            Assert.IsFalse(rewards.HasFragment(CubeModel.StartFace, 1));
            Assert.IsTrue(world.Quests.Tour.Offered);
            Assert.IsFalse(world.Quests.Tour.Started);
            yield return PhysicsSteps(2);

            yield return OpenMayor();
            CollectionAssert.AreEqual(new[] { Dialogue.LeaderLine(Race.Townsfolk), TownTourText.Offer }, Box.Lines, "he asks again");
            yield return PageToQuestion();
            Assert.AreEqual(TownTourText.Offer, Box.CurrentLine);
            Assert.IsTrue(Box.SelectedYes, "a question starts on its first answer");
            Vector2 standing = Pos;
            Vector2 facing = navigator.GetComponent<PlayerMover>().Facing;

            // Every direction key picks: right and down the second, left and up the first, arrows and WASD alike.
            var picks = new (KeyControl key, bool yes)[]
            {
                (keyboard.rightArrowKey, false), (keyboard.upArrowKey, true), (keyboard.sKey, false), (keyboard.aKey, true),
                (keyboard.downArrowKey, false), (keyboard.wKey, true), (keyboard.dKey, false), (keyboard.leftArrowKey, true),
            };
            foreach ((KeyControl key, bool yes) in picks)
            {
                yield return this.Tap(key, 2);
                Assert.AreEqual(yes, Box.SelectedYes, $"{key.name} picks {(yes ? "yes" : "no")}");
                Assert.IsTrue(Box.ChoiceOpen, "picking never confirms");
                Assert.IsTrue(GamePause.Talking, "the talk pause holds while the question is open");
                Assert.AreEqual(0f, Time.timeScale);
            }
            Assert.AreEqual(standing, Pos, "choosing never moves the player");
            Assert.AreEqual(facing, navigator.GetComponent<PlayerMover>().Facing, "nor turns them");

            // Hold D (picks no), confirm with Interact: no hands the fragment over at once, in the same talk.
            Press(keyboard.dKey, queueEventOnly: true);
            yield return null;
            yield return null;
            Assert.IsFalse(Box.SelectedYes);
            yield return this.Tap(keyboard.eKey, 2);
            Assert.AreEqual(false, Box.LastAnswer);
            Assert.IsTrue(Box.IsOpen, "the answer's lines follow in the same box");
            CollectionAssert.AreEqual(new[] { TownTourText.Declined }, Box.Lines);
            Assert.IsTrue(GamePause.Talking, "still paused");
            Assert.IsTrue(rewards.HasFragment(CubeModel.StartFace, 1), "no tour: Town's fragment at once");
            Assert.IsTrue(world.Quests.Tour.Declined);
            Assert.AreEqual(QuestText.TownReturn, world.Quests.Get(QuestLog.TownId).Step);
            // Close it with D still held: the player waits for a fresh press, so D never walks them off.
            yield return this.Tap(keyboard.eKey, 2);
            Assert.IsFalse(Box.IsOpen);
            Assert.AreEqual(1f, Time.timeScale, "time comes back");
            var mover = navigator.GetComponent<PlayerMover>();
            Assert.IsTrue(mover.WaitingForFreshPress, "a held direction waits for a fresh press");
            yield return PhysicsSteps(5); // the player stood touching the Mayor: let the bodies settle apart first
            Vector2 settled = Pos;
            yield return PhysicsSteps(20);
            Assert.Less(Vector2.Distance(settled, Pos), 0.05f, "the held D never leaks into a walk");
            Assert.AreEqual(Vector2.zero, mover.MoveInput, "nothing is walked with");
            Assert.IsTrue(mover.WaitingForFreshPress);
            Release(keyboard.dKey, queueEventOnly: true);
            yield return PhysicsSteps(2);
            yield return this.HoldUntil(keyboard.dKey, () => Pos.x > standing.x + 0.5f * K, 2f);
            Assert.Greater(Pos.x, standing.x + 0.4f * K, "a fresh press walks again");
            yield return PhysicsSteps(2);

            // The hand-in opens Town's temple.
            yield return OpenMayor();
            CollectionAssert.AreEqual(Dialogue.UnsealConversation(Race.Townsfolk, 1), Box.Lines);
            Assert.IsNull(Box.Choice, "no question at the hand-in");
            Assert.IsTrue(world.Repairs.IsUnsealed(CubeModel.StartFace));
            yield return PressThrough();
        }

        // ---------------------------------------------------------------- the tour

        [UnityTest]
        public IEnumerator TheTour_WalkedWithRealKeys_StopByStop_GivesTownsFragment_AndTheMayorOpensTheTemple([Values(7, 21)] int seed)
        {
            yield return Load(seed);
            bool items = seed == 7;
            var inventory = navigator.GetComponent<Inventory>();
            if (items)
            {
                inventory.Add(world.ItemFor(Theme.Biology));
                inventory.Add(world.ItemFor(Theme.Chemistry));
                yield return null;
            }
            TownTour plan = world.TownTour;

            // Yes to the tour (Interact on the first answer): it starts, joins the quest log, and lights its first marker.
            yield return OpenMayor();
            yield return PageToQuestion();
            Assert.IsTrue(Box.SelectedYes);
            yield return this.Tap(keyboard.eKey, 2);
            Assert.AreEqual(true, Box.LastAnswer);
            TourStop first = plan.Stops[0];
            CollectionAssert.AreEqual(new[] { TownTourText.Start(QuestLines.Where(first.Screen, world.FaceSize)) }, Box.Lines);
            yield return PressThrough();
            Assert.IsTrue(tour.Running);
            Assert.IsFalse(rewards.HasFragment(CubeModel.StartFace, 1), "not before the last stop");
            QuestEntry entry = world.Quests.Get(QuestLog.TownId);
            Assert.IsNotNull(entry, "the tour joins the quest log");
            Assert.AreEqual(QuestText.TourTitle, entry.Title);
            Assert.AreEqual(8, tour.Markers.Count);
            Assert.AreEqual(1, tour.Markers.Count(m => m.activeSelf), "one marker glows at a time");
            Assert.IsTrue(tour.Markers[0].activeSelf);
            Assert.IsEmpty(tour.Markers.SelectMany(m => m.GetComponentsInChildren<Collider2D>(true)), "markers block nothing");
            Assert.IsTrue(tour.Mound.GetComponent<Collider2D>().isTrigger && tour.Chest.GetComponent<Collider2D>().isTrigger, "nor the heap or the chest");

            // From the square's centre, stop by stop.
            PlacePlayer(world.ScreenCenter(world.Model.StartScreen));
            yield return PhysicsSteps(2);
            var visited = new List<TourLesson>();
            for (int guard = 0; guard < 10 && tour.Running; guard++)
            {
                TourStop stop = tour.Current.Value;
                Log($"stop {stop}");
                string where = QuestLines.Where(stop.Screen, world.FaceSize);
                Assert.AreEqual(QuestText.TourStep(stop.Lesson, where, false), entry.Step, $"{stop}: the log says where to go");
                Assert.IsTrue(tour.Markers[world.TownTour.Stops.ToList().IndexOf(stop)].activeSelf, $"{stop}: its marker glows");
                yield return WalkToStop(stop);
                Assert.IsTrue(Box.IsOpen, $"{stop}: walking onto the marker says its line");
                Assert.AreEqual(TownTourText.Speaker, Box.Speaker);
                Assert.AreEqual(TownTourText.StopLine(stop.Lesson), Box.CurrentLine);
                Assert.IsTrue(world.TryScreenAt(Pos, out ScreenAddress at) && at == stop.Screen, $"{stop}: reached by walking");
                if (stop.Lesson != TourLesson.Locks)
                    Assert.AreEqual(QuestText.TourStep(stop.Lesson, where, true), entry.Step, $"{stop}: the log gives the task");
                yield return PressThrough();
                visited.Add(stop.Lesson);
                yield return DoTask(stop);
                if (stop.Lesson != TourLesson.Locks)
                    yield return WaitUntilOrTimeout(() => !tour.Running || tour.Current.Value.Lesson != stop.Lesson, 3f);
            }
            List<TourLesson> expected = TownTour.Lessons.Where(l => items || l != TourLesson.Items).ToList();
            CollectionAssert.AreEqual(expected, visited, items ? "every stop, the item stop too" : "every stop but the item stop");
            yield return WaitUntilOrTimeout(() => rewards.HasFragment(CubeModel.StartFace, 1), 3f);
            Assert.IsTrue(rewards.HasFragment(CubeModel.StartFace, 1), "the last stop gives Town's fragment");
            Assert.IsFalse(tour.Running);
            Assert.IsFalse(tour.Markers.Any(m => m.activeSelf), "the markers go");
            Assert.AreEqual(QuestText.TownReturn, entry.Step);
            Assert.IsTrue(tour.Mound.IsOpen && tour.Chest.IsOpen, "the dug heap and opened chest stay");
            CollectionAssert.AreNotEqual(TownTourText.GuideLines, tour.Guide.Lines, "after the tour the guide has ordinary lines");

            // Back to the Mayor: the usual hand-in opens Town's temple, and the quest is done.
            yield return OpenMayor();
            CollectionAssert.AreEqual(Dialogue.UnsealConversation(Race.Townsfolk, 1), Box.Lines);
            yield return PressThrough();
            Assert.IsTrue(world.Repairs.IsUnsealed(CubeModel.StartFace), "Town's temple is open");
            Assert.IsTrue(entry.Done);
            Assert.AreEqual(QuestText.TownDone, entry.Step);
        }

        /// <summary>A stop's tiny task, with real keys.</summary>
        private IEnumerator DoTask(TourStop stop)
        {
            Log($"task {stop.Lesson}");
            Vector2 centre = world.ScreenCenter(stop.Screen);
            // From the marker's glow to its very centre first: every walk below runs along a lane from there.
            if (stop.Lesson == TourLesson.Talk || stop.Lesson == TourLesson.Dig || stop.Lesson == TourLesson.Chest)
                yield return WalkTo(centre);
            switch (stop.Lesson)
            {
                case TourLesson.Talk:
                {
                    NpcTalker guide = tour.Guide;
                    Assert.IsNotNull(guide, "the guide waits by the marker");
                    Assert.IsTrue(world.TryScreenAt(guide.transform.position, out ScreenAddress gs) && gs == stop.Screen);
                    // Along the lane level with the guide, then up to them; Interact talks.
                    yield return WalkAxis(true, guide.transform.position.x);
                    yield return WalkAxis(false, guide.transform.position.y - Mathf.Sign(guide.transform.position.y - centre.y) * 1.1f * K,
                        () => Vector2.Distance(Pos, guide.transform.position) < 1.2f * K);
                    Assert.IsTrue(guide.PlayerInRange(), "the guide is in talking range");
                    yield return this.Tap(keyboard.eKey, 2);
                    Assert.IsTrue(Box.IsOpen);
                    Assert.AreSame(guide, Box.Owner, "the guide talks");
                    CollectionAssert.AreEqual(TownTourText.GuideLines, Box.Lines);
                    yield return PressThrough();
                    Assert.IsTrue(world.Quests.Tour.GuideTalked);
                    // Back to the lane.
                    yield return WalkAxis(false, centre.y);
                    break;
                }
                case TourLesson.Dig:
                {
                    var attack = navigator.GetComponent<PlayerAttack>();
                    Vector2 heap = tour.Mound.transform.position;
                    // South down the lane until the heap is within a swing, facing it; Enter twice.
                    yield return WalkAxis(false, heap.y + (attack.Reach + 0.3f * K) * Mathf.Sign(Pos.y - heap.y));
                    for (int swing = 0; swing < RewardMound.HitsToOpen && !tour.Mound.IsOpen; swing++)
                    {
                        yield return WaitUntilOrGameTimeout(() => attack.Ready, 2f);
                        yield return this.Tap(keyboard.enterKey, 2);
                    }
                    Assert.IsTrue(tour.Mound.IsOpen, "two swings dig the heap away");
                    break;
                }
                case TourLesson.Chest:
                {
                    // Walk into the chest.
                    yield return WalkAxis(false, tour.Chest.transform.position.y, () => tour.Chest.IsOpen);
                    yield return WaitUntilOrTimeout(() => tour.Chest.IsOpen, 2f);
                    Assert.IsTrue(tour.Chest.IsOpen, "walking into the chest opens it");
                    yield return WalkAxis(false, centre.y);
                    break;
                }
                case TourLesson.Map:
                    yield return this.Tap(keyboard.mKey, 2);
                    Assert.IsTrue(map.IsOpen, "M opens the map");
                    yield return this.Tap(keyboard.mKey, 2);
                    Assert.IsFalse(map.IsOpen);
                    break;
                case TourLesson.QuestLog:
                    yield return this.Tap(keyboard.qKey, 2);
                    Assert.IsTrue(questLog.IsOpen, "Q opens the quest log");
                    yield return this.Tap(keyboard.qKey, 2);
                    Assert.IsFalse(questLog.IsOpen);
                    break;
                case TourLesson.Items:
                {
                    var equipment = navigator.GetComponent<Equipment>();
                    ItemDefinition before = equipment.Equipped;
                    yield return this.Tap(keyboard.digit2Key, 2);
                    Assert.AreNotEqual(before, equipment.Equipped, "2 switches the item");
                    break;
                }
            }
            yield return null;
        }

        // ---------------------------------------------------------------- the skip

        [UnityTest]
        public IEnumerator MidTour_NothingBlocksWalkingOutOfTown_TownStaysPeaceful_AndTheMayorCanSkipTheRest()
        {
            yield return Load(21);
            TownTour plan = world.TownTour;
            yield return OpenMayor();
            yield return PageToQuestion();
            yield return this.Tap(keyboard.eKey, 2); // yes: the tour
            yield return PressThrough();
            Assert.IsTrue(tour.Running);

            // The first stop, by walking.
            PlacePlayer(world.ScreenCenter(world.Model.StartScreen));
            yield return PhysicsSteps(2);
            yield return WalkToStop(plan.Stops[0]);
            Assert.AreEqual(TownTourText.StopLine(TourLesson.Move), Box.CurrentLine);
            yield return PressThrough();
            yield return WaitUntilOrTimeout(() => tour.Current.Value.Lesson == TourLesson.Talk, 3f);
            Assert.AreEqual(TourLesson.Talk, tour.Current.Value.Lesson, "on to the second stop");

            // Nothing the tour put down blocks the way: the guide stands off the lanes, the rest are triggers.
            NpcTalker guide = tour.Guide;
            Assert.AreEqual(plan.StopOf(TourLesson.Talk).Screen, tour.GuideSpot.Screen);
            Assert.IsTrue(TownPlan.IsValid(tour.GuideSpot.Local, null), "the guide stands clear of the lanes");
            Assert.IsNull(guide.Relations, "and never turns hostile");

            // 4.20 review: Town's folk never turn hostile, the guide included: F4 beside one of them, then every race hostile.
            NpcTalker someone = town.Npcs.First(n => n != null && !TownPlan.IsGreeter(n.Spec));
            Assert.IsTrue(world.TryScreenAt(someone.transform.position, out ScreenAddress theirs));
            navigator.TeleportTo(theirs, Facing.East);
            yield return null;
            PlacePlayer((Vector2)someone.transform.position + new Vector2(0.3f, 0f) * K);
            yield return null;
            yield return this.Tap(keyboard.f4Key, 2);
            foreach (Race race in RaceExtensions.All) world.Relations.SetHostile(race, true);
            yield return null;
            foreach (NpcTalker npc in town.Npcs.Where(n => n != null).Append(town.Leader).Append(guide))
            {
                Assert.IsFalse(npc.IsHostile, $"{npc.Spec.DisplayName} in Town never turns hostile");
                Assert.IsNull(npc.Relations, $"{npc.Spec.DisplayName} has no race relations");
            }
            foreach (Race race in RaceExtensions.All) world.Relations.SetHostile(race, false);

            // 4.20 review: walking out with the tour's things in place. Down the heap's and the chest's lanes, over them and
            // out through the doorway beyond; then from the arch stop's screen out through its (opened) arch onto another face.
            world.UnlockAllEdges();
            foreach (TourLesson lesson in new[] { TourLesson.Dig, TourLesson.Chest })
            {
                TourStop stop = plan.StopOf(lesson);
                navigator.TeleportTo(stop.Screen, Facing.South);
                yield return PhysicsSteps(2);
                Vector2 thing = world.ScreenCenter(stop.Screen) + stop.Prop;
                float nearest = float.MaxValue;
                yield return this.HoldUntil(keyboard.sKey, () =>
                {
                    nearest = Mathf.Min(nearest, Vector2.Distance(Pos, thing));
                    return world.TryScreenAt(Pos, out ScreenAddress now) && now != stop.Screen;
                }, 8f);
                yield return PhysicsSteps(2);
                Assert.IsTrue(world.TryScreenAt(Pos, out ScreenAddress past) && past != stop.Screen, $"{lesson}: walked out through the doorway");
                Assert.Less(nearest, 0.6f * K, $"{lesson}: straight over the tour's {lesson.ToString().ToLowerInvariant()} thing");
                Assert.IsFalse(Box.IsOpen, "no stop but the current one speaks");
            }
            TourStop locks = plan.StopOf(TourLesson.Locks);
            navigator.TeleportTo(locks.Screen, locks.ArchSide);
            yield return PhysicsSteps(2);
            yield return this.HoldUntil(KeyFor(keyboard, locks.ArchSide),
                () => world.TryScreenAt(Pos, out ScreenAddress now) && now.Face != CubeModel.StartFace, 8f);
            yield return PhysicsSteps(3);
            Assert.IsTrue(world.TryScreenAt(Pos, out ScreenAddress outside) && outside.Face != CubeModel.StartFace,
                "out of Town through the arch by the last stop's marker");
            Assert.IsTrue(world.ScienceRevealed, "leaving Town reveals the faces (and re-populates Town)");

            // Back in Town, the tour carries on.
            navigator.TeleportTo(world.Model.StartScreen, Facing.North);
            yield return PhysicsSteps(3);
            Assert.AreSame(tour, town.Tour);
            Assert.IsTrue(tour.Running, "the tour carries on");
            Assert.AreEqual(TourLesson.Talk, tour.Current.Value.Lesson);
            Assert.AreSame(guide, tour.Guide, "the same guide");

            // The Mayor mid-tour: where next, and shall he skip? No (S picks it) keeps the tour on.
            yield return OpenMayor();
            string where = QuestLines.Where(plan.StopOf(TourLesson.Talk).Screen, world.FaceSize);
            CollectionAssert.AreEqual(new[] { TownTourText.Reminder(where), TownTourText.SkipAsk }, Box.Lines);
            yield return PageToQuestion();
            yield return this.Tap(keyboard.sKey, 2);
            Assert.IsFalse(Box.SelectedYes);
            yield return this.Tap(keyboard.eKey, 2);
            CollectionAssert.AreEqual(new[] { TownTourText.KeepGoing(where) }, Box.Lines);
            yield return PressThrough();
            Assert.IsTrue(tour.Running, "no: the tour goes on");
            Assert.IsFalse(rewards.HasFragment(CubeModel.StartFace, 1));

            // Again, and yes: the rest is skipped and the fragment is the player's at once.
            yield return OpenMayor();
            yield return PageToQuestion();
            yield return this.Tap(keyboard.wKey, 2);
            Assert.IsTrue(Box.SelectedYes);
            yield return this.Tap(keyboard.eKey, 2);
            CollectionAssert.AreEqual(new[] { TownTourText.Skipped }, Box.Lines);
            yield return PressThrough();
            Assert.IsTrue(rewards.HasFragment(CubeModel.StartFace, 1), "skipped: Town's fragment at once");
            Assert.IsTrue(world.Quests.Tour.Skipped);
            Assert.IsFalse(tour.Running);
            Assert.IsFalse(tour.Markers.Any(m => m.activeSelf), "the markers go");
            Assert.AreEqual(QuestText.TownReturn, world.Quests.Get(QuestLog.TownId).Step);
            // 4.20 review: the tour's things stop teaching: the guide has ordinary lines, the untouched heap and chest are gone.
            yield return null;
            CollectionAssert.AreNotEqual(TownTourText.GuideLines, tour.Guide.Lines, "the guide no longer gives the tour");
            Assert.IsTrue(tour.Guide.Lines.Count > 0);
            Assert.IsTrue(tour.Mound == null || tour.Mound.IsOpen, "an undug heap goes");
            Assert.IsTrue(tour.Chest == null || tour.Chest.IsOpen, "an unopened chest goes");

            // Walking onto a marker's old spot says nothing now.
            PlacePlayer(tour.MarkerPosition(plan.StopOf(TourLesson.Talk)));
            yield return PhysicsSteps(5);
            Assert.IsFalse(Box.IsOpen, "a skipped tour's stops are silent");

            // The usual hand-in opens Town's temple.
            yield return OpenMayor();
            CollectionAssert.AreEqual(Dialogue.UnsealConversation(Race.Townsfolk, 1), Box.Lines);
            yield return PressThrough();
            Assert.IsTrue(world.Repairs.IsUnsealed(CubeModel.StartFace));
            Assert.IsTrue(world.Quests.Get(QuestLog.TownId).Done);

            // A new run: a new tour, asked afresh, its old things gone.
            world.Rebuild(22);
            yield return null;
            yield return null;
            Assert.IsFalse(world.Quests.Tour.Offered);
            Assert.IsNull(tour.Guide, "the old run's tour is gone");
            Assert.AreEqual(0, tour.Markers.Count);
        }
    }
}
