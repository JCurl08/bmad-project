using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Game.Cube.Tests.TestInput;

namespace Game.Cube.Tests
{
    /// <summary>
    /// 4.21 release readiness in the real Cube scene, with real keys and a real (test) gamepad:
    /// <list type="bullet">
    /// <item>abandoning a run from the quest log asks first ("Keep going" picked), and a yes ends the run the way a death does
    /// (a defeat, the wallet banked, the run counted);</item>
    /// <item>a pad opens and closes the quest log and the map, skips a conversation, and shops and carries on at the
    /// between-runs screen; Backspace closes like Esc;</item>
    /// <item>the generation pre-warm runs over several frames in Town, and a warmed reveal generates no layout or temple.</item>
    /// </list>
    /// </summary>
    public class ReleaseReadinessSceneTests : InputTestFixture
    {
        private const string SceneName = "Cube";

        private Keyboard keyboard;
        private Gamepad pad;
        private CubeWorld world;
        private CubeNavigator navigator;
        private QuestLogView questLog;
        private CubeMap map;
        private RunLoop loop;
        private RunState runState;
        private RunWallet wallet;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            pad = InputSystem.AddDevice<Gamepad>();
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
            questLog = Object.FindAnyObjectByType<QuestLogView>();
            map = Object.FindAnyObjectByType<CubeMap>();
            loop = Object.FindAnyObjectByType<RunLoop>();
            runState = Object.FindAnyObjectByType<RunState>();
            wallet = Object.FindAnyObjectByType<RunWallet>();
            Assert.IsNotNull(questLog);
            Assert.IsNotNull(loop);
            world.Rebuild(seed);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator AbandonRun_FromTheQuestLog_AsksFirst_ThenEndsTheRunLikeADeath()
        {
            yield return Load(7);
            Assert.IsTrue(wallet.Add(EarningSource.Hidden, "test:abandon", 5));
            DialogueBox box = DialogueBox.Shared;

            // X in the log asks; the pick starts on "Keep going", so Interact keeps the run.
            yield return this.Tap(keyboard.qKey, 2);
            Assert.IsTrue(questLog.IsOpen);
            yield return this.Tap(keyboard.xKey, 2);
            Assert.IsFalse(questLog.IsOpen, "the log makes way for the question");
            Assert.IsTrue(box.ChoiceOpen, "the question shows");
            Assert.AreEqual(QuestText.AbandonQuestion, box.CurrentLine);
            Assert.IsFalse(box.SelectedYes, "a risky yes is never the default");
            Assert.IsTrue(GamePause.Talking, "time stands still while it asks");
            yield return this.Tap(keyboard.eKey, 2);
            Assert.IsFalse(box.IsOpen);
            Assert.AreEqual(false, questLog.LastAbandonAnswer);
            Assert.IsFalse(runState.Ended, "Keep going keeps the run");
            Assert.IsFalse(loop.ScreenOpen);

            // Esc leaves the question unanswered.
            yield return this.Tap(keyboard.qKey, 2);
            yield return this.Tap(keyboard.xKey, 2);
            Assert.IsTrue(box.ChoiceOpen);
            yield return this.Tap(keyboard.escapeKey, 2);
            Assert.IsFalse(box.IsOpen);
            Assert.IsFalse(runState.Ended);

            // A yes (picked with a fresh push left) ends the run as a death does.
            yield return this.Tap(keyboard.qKey, 2);
            yield return this.Tap(keyboard.xKey, 2);
            yield return this.Tap(keyboard.leftArrowKey, 2);
            Assert.IsTrue(box.SelectedYes);
            yield return this.Tap(keyboard.eKey, 2);
            yield return null;
            Assert.AreEqual(true, questLog.LastAbandonAnswer);
            Assert.IsTrue(runState.Ended, "the run is over");
            Assert.IsFalse(runState.Victory, "a defeat");
            Assert.IsTrue(navigator.GetComponent<PlayerStats>().IsDead, "through the death path");
            Assert.IsTrue(loop.ScreenOpen, "the between-runs screen opens, as after a death");
            Assert.IsFalse(loop.LastRun.Victory);
            Assert.AreEqual(5, loop.LastRun.Banked, "the run's earnings are banked as a death banks them");
            Assert.AreEqual(5, loop.Meta.Balance);
            Assert.AreEqual(1, loop.Meta.RunCount, "the run counts");
            Assert.IsFalse(GamePause.Talking);
            Assert.AreEqual(1f, Time.timeScale, "time runs again");

            // Continue starts the next run as ever.
            yield return this.Tap(keyboard.enterKey, 2);
            Assert.IsFalse(loop.ScreenOpen);
            Assert.IsFalse(navigator.GetComponent<PlayerStats>().IsDead);
        }

        [UnityTest]
        public IEnumerator APad_OpensAndClosesTheLogAndTheMap_SkipsTalk_AndAbandons_AndBackspaceClosesLikeEsc()
        {
            yield return Load(21);

            yield return this.Tap(pad.dpad.up, 2);
            Assert.IsTrue(questLog.IsOpen, "D-Pad Up opens the quest log");
            yield return this.Tap(pad.buttonEast, 2);
            Assert.IsFalse(questLog.IsOpen, "B closes it");
            yield return this.Tap(pad.dpad.up, 2);
            Assert.IsTrue(questLog.IsOpen);
            yield return this.Tap(pad.dpad.up, 2);
            Assert.IsFalse(questLog.IsOpen, "D-Pad Up again closes it");

            yield return this.Tap(pad.selectButton, 2);
            Assert.IsTrue(map.IsOpen, "Select opens the map");
            Assert.AreEqual(0f, Time.timeScale);
            yield return this.Tap(pad.buttonEast, 2);
            Assert.IsFalse(map.IsOpen, "B closes it");
            yield return this.Tap(pad.selectButton, 2);
            Assert.IsTrue(map.IsOpen);
            yield return this.Tap(pad.selectButton, 2);
            Assert.IsFalse(map.IsOpen, "Select again closes it");

            // Backspace: the second close key (Esc also leaves browser fullscreen).
            yield return this.Tap(keyboard.mKey, 2);
            Assert.IsTrue(map.IsOpen);
            yield return this.Tap(keyboard.backspaceKey, 2);
            Assert.IsFalse(map.IsOpen, "Backspace closes the map");
            yield return this.Tap(keyboard.qKey, 2);
            Assert.IsTrue(questLog.IsOpen);
            yield return this.Tap(keyboard.backspaceKey, 2);
            Assert.IsFalse(questLog.IsOpen, "and the quest log");

            DialogueBox box = DialogueBox.Shared;
            Assert.IsTrue(box.Open("Tester", new[] { "one", "two", "three" }));
            yield return null;
            yield return this.Tap(pad.buttonNorth, 2);
            Assert.AreEqual(1, box.Index, "Y pages (Interact)");
            yield return this.Tap(pad.buttonEast, 2);
            Assert.IsFalse(box.IsOpen, "B skips the conversation");
            Assert.IsTrue(box.Open("Tester", new[] { "one", "two" }));
            yield return null;
            yield return this.Tap(keyboard.backspaceKey, 2);
            Assert.IsFalse(box.IsOpen, "so does Backspace");

            // The abandon question on a pad: X asks, the stick picks, Y confirms.
            yield return this.Tap(pad.dpad.up, 2);
            yield return this.Tap(pad.buttonWest, 2);
            Assert.IsTrue(box.ChoiceOpen, "X in the log asks");
            Assert.IsFalse(box.SelectedYes);
            Set(pad.leftStick, new Vector2(-1f, 0f), queueEventOnly: true);
            yield return null;
            yield return null;
            Set(pad.leftStick, Vector2.zero, queueEventOnly: true);
            yield return null;
            Assert.IsTrue(box.SelectedYes, "the stick picks Abandon");
            yield return this.Tap(pad.buttonSouth, 2); // A answers, as it takes a pick between runs
            yield return null;
            Assert.IsTrue(runState.Ended);
            Assert.IsTrue(loop.ScreenOpen);
        }

        [UnityTest]
        public IEnumerator APad_ShopsAndCarriesOn_AtTheBetweenRunsScreen()
        {
            yield return Load(7);
            BetweenRunsScreen screen = Object.FindAnyObjectByType<BetweenRunsScreen>();
            Assert.IsNotNull(screen);
            runState.EndRun(false);
            yield return null;
            yield return null;
            Assert.IsTrue(screen.Visible);
            Assert.AreEqual(BetweenRunsScreen.NoPick, screen.Picked, "nothing is picked on opening");
            loop.Meta.Balance = 1000;

            // With nothing picked, A and Space do nothing: no stray purchase, no skipped shop.
            yield return this.Tap(pad.buttonSouth, 2);
            yield return this.Tap(keyboard.spaceKey, 2);
            Assert.AreEqual(1000, loop.Meta.Balance, "nothing bought");
            Assert.IsTrue(loop.ScreenOpen, "and the shop stays");

            yield return this.Tap(pad.dpad.down, 2);
            Assert.AreEqual(0, screen.Picked, "the d-pad picks the first upgrade");
            yield return this.Tap(pad.dpad.down, 2);
            Assert.AreEqual(1, screen.Picked);
            yield return this.Tap(pad.buttonSouth, 2);
            Assert.AreEqual(1, UpgradeShop.Level(loop.Meta, UpgradeShop.All[1]), "A buys the picked upgrade");
            yield return this.Tap(keyboard.digit3Key, 2);
            Assert.AreEqual(1, UpgradeShop.Level(loop.Meta, UpgradeShop.All[2]), "the 3 key still buys the third");
            yield return this.Tap(pad.dpad.up, 2);
            yield return this.Tap(pad.dpad.up, 2);
            Assert.AreEqual(BetweenRunsScreen.ContinueRow, screen.Picked, "the pick wraps round to Continue");
            yield return this.Tap(pad.buttonSouth, 2);
            Assert.IsFalse(loop.ScreenOpen, "A on Continue carries on");

            runState.EndRun(false);
            yield return null;
            yield return null;
            Assert.IsTrue(loop.ScreenOpen);
            yield return this.Tap(pad.startButton, 2);
            Assert.IsFalse(loop.ScreenOpen, "Start carries on from anywhere on the screen");
        }

        [Test]
        public void OnlyAButtonOrKey_SwitchesThePromptsDevice_NeverAStickOrThePointer()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Assert.AreEqual(true, InputPrompts.DeviceFor("Attack", pad.buttonWest));
            Assert.AreEqual(true, InputPrompts.DeviceFor("QuestLog", pad.dpad.up));
            Assert.IsNull(InputPrompts.DeviceFor("Move", pad.leftStick), "a stick never switches");
            Assert.IsNull(InputPrompts.DeviceFor("Look", pad.rightStick));
            Assert.IsNull(InputPrompts.DeviceFor("Look", mouse.delta), "nor the mouse's movement");
            Assert.IsNull(InputPrompts.DeviceFor("Look", keyboard.eKey), "nor anything on Look");
            Assert.AreEqual(false, InputPrompts.DeviceFor("Interact", keyboard.eKey));
            Assert.AreEqual(false, InputPrompts.DeviceFor("Attack", mouse.leftButton));

            // Filled prompts are cached per template and device.
            InputPrompts.ClearCache();
            InputPrompts.UsingGamepad = true;
            string pad1 = InputPrompts.Fill(DialogueBox.NextPrompt);
            Assert.AreSame(pad1, InputPrompts.Fill(DialogueBox.NextPrompt), "the second fill is the cached string");
            InputPrompts.UsingGamepad = false;
            string keys = InputPrompts.Fill(DialogueBox.NextPrompt);
            Assert.AreNotEqual(pad1, keys, "a device change gives the other device's names");
            Assert.AreEqual(2, InputPrompts.CachedCount);
        }

        private static int Generated() => IcePlan.Generated + WindPlan.Generated + IslandPlan.Generated + TemplePlan.Generated;

        [UnityTest]
        public IEnumerator ThePrewarm_RunsOverFramesInTown_AndTheRevealThenGeneratesNothing()
        {
            yield return Load(42);
            Assert.IsNotNull(world.Prewarm, "a new run starts warming");
            Assert.IsFalse(world.ScienceRevealed);
            yield return WaitUntilOrTimeout(() => world.Prewarm.Done, 20f);
            Assert.IsTrue(world.Prewarm.Done, "the pre-warm finishes in Town");
            Assert.Greater(world.PrewarmFrames, 1, "over several frames, not one");
            Debug.Log($"Pre-warm seed 42: {world.Prewarm.StepsRun} steps over {world.PrewarmFrames} frames");

            int before = Generated();
            CubeLayout warmed = world.Prewarm.Layout;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            Assert.IsTrue(world.RevealScience());
            clock.Stop();
            Debug.Log($"Warmed reveal, seed 42: {clock.Elapsed.TotalMilliseconds:0} ms (building only, plus the editor's plan check)");
            Assert.AreSame(warmed, world.Layout, "the reveal takes the warmed layout");
            Assert.AreEqual(before, Generated(), "and makes no puzzle layout or temple of its own");
            foreach (FaceId face in TemplePlan.TempleFaces(world.Model)) world.TempleOf(face);
            Assert.AreEqual(before, Generated(), "nor does a temple's unsealing");

            // A reveal before the pre-warm is done is just a cold reveal: the same plans.
            world.Rebuild(42);
            Assert.IsTrue(world.RevealScience());
            Assert.AreEqual(warmed.Signature(), world.Layout.Signature());
        }

        [UnityTest]
        public IEnumerator TheCamera_DrawsThePlayAreaBetweenTheSideColumns_AndTheHudDrawsBesideIt()
        {
            // 4.25 (playtest 7): the HUD lives in the side columns (SideHud); the camera draws only the play area between them.
            yield return Load(7);
            yield return null;
            yield return null;
            Camera cam = Camera.main;
            Assert.IsNotNull(cam);
            Assert.IsNotNull(world.GetComponent<SideHudView>(), "the side columns' view is on the world");
            Assert.IsNotNull(cam.GetComponent<Game.Tracer.ScreenCamera>().Viewport, "the camera takes its viewport from the HUD's layout");
            Rect expected = SideHud.CameraRect(Screen.width, Screen.height);
            Assert.AreEqual(expected.x, cam.rect.x, 1e-3f);
            Assert.AreEqual(expected.y, cam.rect.y, 1e-3f);
            Assert.AreEqual(expected.width, cam.rect.width, 1e-3f);
            Assert.AreEqual(expected.height, cam.rect.height, 1e-3f);
            Assert.AreEqual(1.6f, cam.pixelRect.width / cam.pixelRect.height, 0.02f, "the play area keeps the screen's aspect");
            SideHud.Frame frame = SideHud.Current;
            float k = HudScale.Current;
            Assert.LessOrEqual(frame.Left.xMax * k, cam.pixelRect.xMin + 1f, "the left column ends where the play area begins");
            Assert.GreaterOrEqual(frame.Right.xMin * k, cam.pixelRect.xMax - 1f, "and the right one starts where it ends");
            // The player stands in the play area: its screen point is inside the camera's pixel rect.
            Vector3 p = cam.WorldToScreenPoint(navigator.transform.position);
            Assert.IsTrue(cam.pixelRect.Contains(new Vector2(p.x, p.y)), $"the player at {p} is drawn in the play area {cam.pixelRect}");
        }
    }
}
