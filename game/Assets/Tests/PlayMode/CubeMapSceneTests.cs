using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Game.Tracer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Game.Cube.Tests.TestInput;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The cube-net pause map in the real Cube scene (story 21), one test per I/O row: M (or Tab) opens it and pauses the
    /// game (no movement, no attack) and closes it again; it never opens on the between-runs screen; a new run lights only
    /// the start screen; a visited screen lights in its region colour; the player marker sits on the right face and cell
    /// with its facing, or a core indicator in the arena; leaving town shows the revealed layout and a new run resets it;
    /// and on seeds 1, 7 and 42 with every screen visited (debug) every cell's face, colour and markers match the world.
    /// </summary>
    public class CubeMapSceneTests : InputTestFixture
    {
        /// <summary>Lengths and speeds below are in the first 16x10 screens' units, scaled like the game's (4.1).</summary>
        private const float K = ScreenMath.ContentScale;

        private const string SceneName = "Cube";

        private Keyboard keyboard;
        private CubeWorld world;
        private CubeNavigator navigator;
        private CubeMap map;
        private VisitTracker tracker;
        private RunLoop loop;
        private RunState runState;
        private CoreArena arena;
        private CubeDebug debug;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        public override void TearDown()
        {
            GamePause.Resume();
            Time.timeScale = 1f;
            base.TearDown();
        }

        private IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            world = Object.FindAnyObjectByType<CubeWorld>();
            navigator = Object.FindAnyObjectByType<CubeNavigator>();
            map = Object.FindAnyObjectByType<CubeMap>();
            tracker = Object.FindAnyObjectByType<VisitTracker>();
            loop = Object.FindAnyObjectByType<RunLoop>();
            runState = Object.FindAnyObjectByType<RunState>();
            arena = Object.FindAnyObjectByType<CoreArena>();
            debug = Object.FindAnyObjectByType<CubeDebug>();
            Assert.IsNotNull(map, "The Cube scene has a CubeMap");
            Assert.IsNotNull(tracker, "The Cube scene has a VisitTracker");
            Assert.AreSame(tracker, map.Tracker);
            Assert.AreSame(world, map.World);
            Assert.AreSame(navigator, map.Navigator);
            Assert.AreSame(loop, map.Loop);
            Assert.IsFalse(map.IsOpen);
            NoFaceEnemies();
        }

        private void NoFaceEnemies()
        {
            world.GetComponent<BiologyFace>().SpawnEnemies = false;
            world.GetComponent<ChemistryFace>().SpawnEnemies = false;
            world.GetComponent<PhysicsFace>().SpawnEnemies = false;
            world.GetComponent<MathFace>().SpawnEnemies = false;
            world.GetComponent<EarthFace>().SpawnEnemies = false;
        }

        /// <summary>A screen on the first built science face (teleporting there reveals the science layout).</summary>
        private ScreenAddress ScienceScreen(int x = 1, int y = 1)
        {
            for (int f = 0; f < CubeSettings.FaceCount; f++)
                if (CubeLayout.IsLaidOut(world.Model, (FaceId)f)) return new ScreenAddress((FaceId)f, x, y);
            Assert.Fail("No built science face");
            return default;
        }

        // ---------- Open / close ----------

        [UnityTest]
        public IEnumerator MOrTab_OpensTheMap_AndPausesTheGame_PressingAgainResumes()
        {
            yield return Load();
            var attack = navigator.GetComponent<PlayerAttack>();
            Assert.AreEqual(1f, Time.timeScale);

            yield return this.Tap(keyboard.mKey, 2);
            Assert.IsTrue(map.IsOpen, "M opens the map");
            Assert.IsTrue(GamePause.Active);
            Assert.AreEqual(0f, Time.timeScale, "the game is paused");

            // Walking and attacking while the map is open do nothing.
            Vector2 before = navigator.transform.position;
            int swings = attack.SwingCount;
            Press(keyboard.dKey, queueEventOnly: true);
            yield return WaitRealSeconds(0.4f);
            Release(keyboard.dKey, queueEventOnly: true);
            yield return this.Tap(keyboard.enterKey, 2);
            Assert.AreEqual(before, (Vector2)navigator.transform.position, "the player cannot move while the map is open");
            Assert.AreEqual(swings, attack.SwingCount, "the player cannot attack while the map is open");

            yield return this.Tap(keyboard.mKey, 2);
            Assert.IsFalse(map.IsOpen, "M again closes it");
            Assert.IsFalse(GamePause.Active);
            Assert.AreEqual(1f, Time.timeScale, "the game resumes");

            yield return this.Tap(keyboard.tabKey, 2);
            Assert.IsTrue(map.IsOpen, "Tab opens it too");
            Assert.AreEqual(0f, Time.timeScale);
            yield return this.Tap(keyboard.tabKey, 2);
            Assert.IsFalse(map.IsOpen, "Tab closes it");
            Assert.AreEqual(1f, Time.timeScale);

            yield return this.Tap(keyboard.mKey, 2);
            Assert.IsTrue(map.IsOpen);
            yield return this.Tap(keyboard.escapeKey, 2);
            Assert.IsFalse(map.IsOpen, "Escape closes it");
            Assert.AreEqual(1f, Time.timeScale);

            // Control is back: walking moves the player again.
            before = navigator.transform.position;
            Press(keyboard.dKey, queueEventOnly: true);
            yield return WaitRealSeconds(0.3f);
            Release(keyboard.dKey, queueEventOnly: true);
            yield return null;
            Assert.Greater(((Vector2)navigator.transform.position - before).magnitude, 0.2f * K, "movement resumes after closing");
        }

        [UnityTest]
        public IEnumerator BetweenRunsScreen_TheMapNeverOpens_AndClosesWhenTheScreenOpens()
        {
            yield return Load();
            Assert.IsTrue(map.Open());
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(runState.EndRun(false));
            Assert.IsTrue(loop.ScreenOpen);
            yield return null;
            yield return null;
            Assert.IsFalse(map.IsOpen, "the between-runs screen closes the map");
            Assert.AreEqual(1f, Time.timeScale);

            yield return this.Tap(keyboard.mKey, 2);
            Assert.IsFalse(map.IsOpen, "M is ignored on the between-runs screen");
            Assert.IsFalse(map.CanOpen);
            Assert.IsFalse(map.Open());
            Assert.AreEqual(1f, Time.timeScale);
        }

        // ---------- Fog and visits ----------

        [UnityTest]
        public IEnumerator NewRun_LightsOnlyTheStartScreen_AndAVisitedScreenLightsInItsRegionColour()
        {
            yield return Load();
            ScreenAddress start = world.Model.StartScreen;
            Assert.AreEqual(1, tracker.Visits.Count, "a new run: only the start screen");
            MapCellView startView = map.CellView(start);
            Assert.IsTrue(startView.Visited);
            Assert.AreEqual(CubeWorld.ThemeColor(Theme.Town), startView.Color);
            foreach (ScreenAddress s in world.Model.AllScreens().Where(s => s != start))
            {
                Assert.IsFalse(map.CellView(s).Visited, s.ToString());
                Assert.AreEqual(CubeMap.FogColor, map.CellView(s).Color, $"{s} stays dark");
            }

            // Walk onto the next Town screen east.
            var next = new ScreenAddress(start.Face, start.Cell + Vector2Int.right);
            Press(keyboard.dKey, queueEventOnly: true);
            yield return WaitUntilOrTimeout(() => navigator.Current == next, 10f);
            Release(keyboard.dKey, queueEventOnly: true);
            yield return null;
            yield return null;
            Assert.AreEqual(next, navigator.Current, "walked onto the next screen");
            Assert.IsTrue(map.CellView(next).Visited, "the screen walked onto is lit");
            Assert.AreEqual(CubeWorld.ThemeColor(Theme.Town), map.CellView(next).Color);

            // A science screen: lit in its own region colour once visited.
            ScreenAddress science = ScienceScreen();
            Assert.IsFalse(map.CellView(science).Visited);
            navigator.TeleportTo(science, Facing.East);
            yield return null;
            yield return null;
            Assert.IsTrue(map.CellView(science).Visited);
            Assert.AreEqual(CubeWorld.ThemeColor(world.Model.ThemeOf(science.Face)), map.CellView(science).Color);
            Assert.AreEqual(3, tracker.Visits.Count);

            // Markers only on visited screens.
            Assert.IsTrue(map.Open());
            Assert.IsTrue(map.Markers.All(m => tracker.IsVisited(m.Screen)), "no marker on an unvisited screen");
            Assert.AreEqual(1, map.Markers.Count(m => m.Kind == MapMarkerKind.Start && m.Screen == start));
            map.Close();
        }

        // ---------- Player marker ----------

        [UnityTest]
        public IEnumerator PlayerMarker_OnTheRightFaceAndCell_WithItsFacing_OrTheCoreIndicator()
        {
            yield return Load();
            ScreenAddress science = ScienceScreen(2, 0);
            navigator.TeleportTo(science, Facing.North);
            yield return null;
            yield return null;
            Assert.IsTrue(map.Open());
            Assert.IsFalse(map.PlayerInCore);
            Assert.AreEqual(science, map.PlayerScreen);
            Assert.AreEqual(map.Net.NetCell(science), map.CellView(science).NetCell);
            Vector2 facing = map.PlayerDirection;
            Assert.That(facing.magnitude, Is.EqualTo(1f).Within(1e-3f));
            Vector2 expected = map.Net.ToNetDirection(science.Face, facing);
            Assert.That(Vector2.Distance(expected, map.PlayerNetDirection), Is.LessThan(1e-4f));
            map.Close();
            yield return null;

            // Walk north a moment: the marker's facing follows the move direction, turned into the net.
            Press(keyboard.wKey, queueEventOnly: true);
            yield return WaitRealSeconds(0.25f);
            Release(keyboard.wKey, queueEventOnly: true);
            yield return null;
            Assert.That(Vector2.Distance(Vector2.up, map.PlayerDirection), Is.LessThan(1e-3f));
            Vector2 netUp = map.Net.ToNet(navigator.Current.Face, Facing.North).ToVector();
            Assert.That(Vector2.Distance(netUp, map.PlayerNetDirection), Is.LessThan(1e-3f));

            // The core arena: a core indicator instead of a cell.
            Assert.IsTrue(arena.Enter(navigator));
            yield return null;
            Assert.IsTrue(map.Open(), "the map opens in the core arena");
            Assert.IsTrue(map.PlayerInCore, "the core indicator shows");
            map.Close();
            Assert.IsTrue(debug.LeaveCoreArena());
            yield return null;
            Assert.IsFalse(map.PlayerInCore);
        }

        // ---------- Shuffle and new run ----------

        [UnityTest]
        public IEnumerator LeavingTown_ShowsTheRevealedLayout_AndANewRunResetsTheFog()
        {
            yield return Load();
            Assert.IsFalse(world.ScienceRevealed);
            ScreenAddress science = ScienceScreen();
            navigator.TeleportTo(science, Facing.East);
            yield return null;
            yield return null;
            Assert.IsTrue(world.ScienceRevealed, "leaving town reveals the layout");
            Assert.IsTrue(map.Open());
            Assert.AreEqual(CubeWorld.ThemeColor(world.Model.ThemeOf(science.Face)), map.CellView(science).Color);
            ScreenModule module = world.ModuleAt(science);
            Assert.IsNotNull(module);
            int gatesHere = module.GetComponentsInChildren<Gate>().Length;
            Assert.AreEqual(gatesHere, map.Markers.Count(m => m.Kind == MapMarkerKind.Gate && m.Screen == science),
                "the visited screen's gates are on the map");
            map.Close();

            int oldSeed = world.Seed;
            int runs = tracker.RunsTracked;
            loop.StartRun(oldSeed + 1);
            yield return null;
            yield return null;
            Assert.AreEqual(runs + 1, tracker.RunsTracked, "a new run resets the fog");
            Assert.AreEqual(1, tracker.Visits.Count);
            Assert.IsTrue(tracker.IsVisited(world.Model.StartScreen));
            Assert.IsFalse(map.CellView(science).Visited);
            Assert.IsTrue(map.Open());
            Assert.IsTrue(map.Markers.All(m => m.Screen == world.Model.StartScreen), "only the start screen's markers in a new run");
            Assert.AreEqual(1, map.Markers.Count(m => m.Kind == MapMarkerKind.Start));
            map.Close();

            // F10 (debug new run) resets it too, and the map follows the new layout.
            navigator.TeleportTo(ScienceScreen(0, 2), Facing.East);
            yield return null;
            Assert.Greater(tracker.Visits.Count, 1);
            Assert.IsTrue(debug.StartNewRun());
            yield return null;
            yield return null;
            Assert.AreEqual(1, tracker.Visits.Count);
            Assert.IsTrue(debug.RevealMap());
            Assert.IsTrue(map.Open());
            foreach (ScreenAddress s in world.Model.AllScreens())
                Assert.AreEqual(CubeWorld.ThemeColor(world.Model.ThemeOf(s.Face)), map.CellView(s).Color, s.ToString());
            map.Close();
        }

        [UnityTest]
        public IEnumerator WhileOpen_InteractNeitherTalksNorGrabs_AndEscapeClosesTheMapNotTheConversation()
        {
            yield return Load();
            var mitt = navigator.GetComponent<MassMitt>();

            // Reveal (so the run's Mass Mitt exists), then back to the start screen for the rest.
            navigator.TeleportTo(ScienceScreen(), Facing.East);
            yield return null;
            navigator.TeleportTo(world.Model.StartScreen, Facing.East);
            yield return null;

            // One NPC right next to the player, standing still; nobody else around.
            List<NpcTalker> npcs = debug.SpawnNpcs();
            NpcTalker target = npcs[0];
            foreach (NpcTalker other in npcs)
                if (other != target) other.gameObject.SetActive(false);
            foreach (NpcTalker townsfolk in Object.FindObjectsByType<NpcTalker>(FindObjectsSortMode.None))
                if (townsfolk != target) townsfolk.gameObject.SetActive(false);
            if (target.TryGetComponent(out NpcMover npcMover)) npcMover.enabled = false;
            Vector2 at = (Vector2)navigator.transform.position + new Vector2(1f * K, 0f);
            target.GetComponent<Rigidbody2D>().position = at;
            target.transform.position = at;
            yield return new WaitForFixedUpdate();
            yield return null; // input state is only valid in dynamic update
            DialogueBox box = DialogueBox.Shared;
            Assert.IsNotNull(box);

            yield return this.Tap(keyboard.mKey, 2);
            Assert.IsTrue(map.IsOpen);
            yield return this.Tap(keyboard.eKey, 2);
            Assert.IsFalse(box.IsOpen, "Interact does not start a conversation while the map is open");
            yield return this.Tap(keyboard.mKey, 2);
            Assert.IsFalse(map.IsOpen);

            yield return this.Tap(keyboard.eKey, 2);
            Assert.IsTrue(box.IsOpen, "with the map closed, Interact talks");
            string line = box.CurrentLine;

            yield return this.Tap(keyboard.mKey, 2);
            Assert.IsTrue(map.IsOpen);
            yield return this.Tap(keyboard.eKey, 2);
            Assert.IsTrue(box.IsOpen);
            Assert.AreEqual(line, box.CurrentLine, "Interact does not advance the conversation while the map is open");
            yield return this.Tap(keyboard.escapeKey, 2);
            Assert.IsFalse(map.IsOpen, "Escape closes the map");
            Assert.IsTrue(box.IsOpen, "the same Escape does not skip the conversation behind the map");
            Assert.AreEqual(line, box.CurrentLine);
            yield return this.Tap(keyboard.escapeKey, 2);
            Assert.IsFalse(box.IsOpen, "with the map closed, Escape skips the conversation");
            target.gameObject.SetActive(false);
            yield return null;

            // A boulder within reach with the Mass Mitt equipped.
            Assert.IsNotNull(mitt.Item, "the reveal set up the run's Mass Mitt");
            navigator.GetComponent<Inventory>().Add(mitt.Item);
            navigator.GetComponent<Equipment>().Equip(mitt.Item);
            Assert.IsTrue(mitt.IsEquipped);
            Boulder boulder = Boulder.Create(null, (Vector2)navigator.transform.position + new Vector2(1.2f * K, 0f), null, null);
            yield return new WaitForFixedUpdate();
            yield return null;

            yield return this.Tap(keyboard.mKey, 2);
            Assert.IsTrue(map.IsOpen);
            yield return this.Tap(keyboard.eKey, 2);
            Assert.IsNull(mitt.Held, "Interact does not grab a boulder while the map is open");
            yield return this.Tap(keyboard.mKey, 2);
            Assert.IsFalse(map.IsOpen);
            Assert.IsNull(mitt.Held, "nor on the frame the map closes");

            yield return this.Tap(keyboard.eKey, 2);
            Assert.AreSame(boulder, mitt.Held, "with the map closed, Interact grabs it");
        }

        // ---------- Facing and sealed fog ----------

        [UnityTest]
        public IEnumerator PlayerFacing_ResetsOnTeleportAndNewRun_NeverKeepingTheLastRunsDirection()
        {
            yield return Load();
            var mover = navigator.GetComponent<Game.Tracer.PlayerMover>();
            yield return this.TapThroughPhysics(keyboard.aKey);
            Assert.That(Vector2.Distance(Vector2.left, mover.Facing), Is.LessThan(1e-3f), "walked west");

            loop.StartRun(world.Seed + 1);
            yield return null;
            Assert.AreEqual(Facing.East, navigator.Facing);
            Assert.That(Vector2.Distance(Vector2.right, mover.Facing), Is.LessThan(1e-3f), "a new run faces the entry facing");
            Assert.That(Vector2.Distance(Vector2.right, map.PlayerDirection), Is.LessThan(1e-3f), "and so does the map arrow");

            navigator.TeleportTo(ScienceScreen(), Facing.North);
            yield return null;
            Assert.That(Vector2.Distance(Vector2.up, mover.Facing), Is.LessThan(1e-3f), "a teleport faces its facing");
            Vector2 netUp = map.Net.ToNet(navigator.Current.Face, Facing.North).ToVector();
            Assert.That(Vector2.Distance(netUp, map.PlayerNetDirection), Is.LessThan(1e-3f));
        }

        [UnityTest]
        public IEnumerator SealedFace_StaysDarkUntilVisited()
        {
            yield return Load();
            world.SealedThemes.Add(Theme.Math);
            loop.StartRun(world.Seed + 1);
            yield return null;
            FaceId sealedFace = world.Model.FaceOf(Theme.Math);
            Assert.IsTrue(world.Model.IsSealed(sealedFace));
            var screen = new ScreenAddress(sealedFace, 1, 1);
            MapCellView view = map.CellView(screen);
            Assert.IsFalse(view.Visited);
            Assert.IsFalse(view.Sealed, "an unvisited screen never shows it is sealed");
            Assert.AreEqual(CubeMap.FogColor, view.Color, "an unvisited sealed screen is as dark as any other");

            tracker.Visit(screen);
            view = map.CellView(screen);
            Assert.IsTrue(view.Sealed);
            Assert.AreEqual(CubeMap.SealedColor, view.Color, "once visited, the sealed style shows");
            world.SealedThemes.Clear();
        }

        [UnityTest]
        public IEnumerator DebugKeys_AreIgnoredWhileTheMapIsOpen_ExceptTheOverlayToggle()
        {
            yield return Load();
            int seed = world.Seed;
            bool overlay = debug.OverlayVisible;
            Assert.IsTrue(map.Open());
            yield return this.Tap(keyboard.f10Key, 2);
            yield return this.Tap(keyboard.f11Key, 2);
            Assert.AreEqual(seed, world.Seed, "F10 does nothing while the map is open");
            Assert.AreEqual(1, tracker.Visits.Count, "F11 does nothing while the map is open");
            yield return this.Tap(keyboard.f1Key, 2);
            Assert.AreNotEqual(overlay, debug.OverlayVisible, "F1 still toggles the overlay");
            map.Close();
            yield return this.Tap(keyboard.f11Key, 2);
            Assert.Greater(tracker.Visits.Count, 1, "F11 works again once the map is closed");
        }

        // ---------- Truth ----------

        private static readonly int[] TruthSeeds = { 1, 7, 42 };

        /// <summary>The screen a world position lies on, from the face regions' layout (independent of the module tree).</summary>
        private ScreenAddress ScreenAtWorld(Vector2 position)
        {
            float faceStride = (world.FaceSize + 1) * CubeWorld.ScreenSize.x;
            var face = (FaceId)Mathf.FloorToInt(position.x / faceStride);
            return new ScreenAddress(face, world.CellAt(face, position));
        }

        private static string Key(MapMarkerKind kind, ScreenAddress screen, bool done) => $"{kind} {screen} {done}";

        [UnityTest]
        public IEnumerator Truth_OnSeeds1_7_42_EveryCellAndMarkerMatchesTheWorld()
        {
            yield return Load();
            foreach (int seed in TruthSeeds)
            {
                loop.StartRun(seed);
                yield return null;
                Assert.AreEqual(seed, world.Seed);
                Assert.IsTrue(debug.RevealMap(), "debug: visit every screen");
                yield return null;
                Assert.IsTrue(world.ScienceRevealed);
                Assert.IsTrue(map.Open());

                CubeModel model = world.Model;
                CubeNet net = map.Net;
                Assert.AreEqual(FaceId.Front, net.Centre, "Town in the middle of the cross");
                foreach (ScreenAddress s in model.AllScreens())
                {
                    MapCellView view = map.CellView(s);
                    Assert.IsTrue(view.Visited, $"seed {seed}: {s} visited");
                    Assert.AreEqual(CubeWorld.ThemeColor(model.ThemeOf(s.Face)), view.Color, $"seed {seed}: {s} colour");
                    Assert.IsTrue(net.TryGetScreen(view.NetCell, out ScreenAddress drawn));
                    Assert.AreEqual(s, drawn, $"seed {seed}: {s} drawn on its own cell");
                }

                // Every gate, item, trial and portal in the world, placed by its world position.
                var expected = new List<string>();
                foreach (Gate g in Object.FindObjectsByType<Gate>(FindObjectsSortMode.None))
                    expected.Add(Key(MapMarkerKind.Gate, ScreenAtWorld(g.transform.position), g.IsOpen));
                foreach (ItemPickup p in Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None))
                    if (!p.Collected) expected.Add(Key(MapMarkerKind.Item, ScreenAtWorld(p.transform.position), false));
                foreach (TrialRoom t in Object.FindObjectsByType<TrialRoom>(FindObjectsSortMode.None))
                    expected.Add(Key(MapMarkerKind.Trial, ScreenAtWorld(t.transform.position), t.IsComplete));
                foreach (CorePortal c in Object.FindObjectsByType<CorePortal>(FindObjectsSortMode.None))
                    expected.Add(Key(MapMarkerKind.Portal, ScreenAtWorld(c.transform.position), false));
                Assert.Greater(expected.Count(e => e.StartsWith("Gate")), 0, $"seed {seed} has gates");
                Assert.Greater(expected.Count(e => e.StartsWith("Item")), 0, $"seed {seed} has items");
                Assert.Greater(expected.Count(e => e.StartsWith("Trial")), 0, $"seed {seed} has trials");
                Assert.AreEqual(arena.Portals.Count, expected.Count(e => e.StartsWith("Portal")), $"seed {seed} portals");

                List<string> shown = map.Markers.Where(m => m.Kind != MapMarkerKind.Start)
                    .Select(m => Key(m.Kind, m.Screen, m.Done)).ToList();
                CollectionAssert.AreEquivalent(expected, shown, $"seed {seed}: markers match the world");
                Assert.AreEqual(1, map.Markers.Count(m => m.Kind == MapMarkerKind.Start));
                Assert.AreEqual(model.StartScreen, map.Markers.Single(m => m.Kind == MapMarkerKind.Start).Screen);
                Assert.IsTrue(world.HiddenCurrencyPickups.Count > 0, $"seed {seed} hides currency");
                Assert.AreEqual(expected.Count + 1, map.Markers.Count, "hidden currency is never on the map");

                // A trial solved since is shown as solved once the map refreshes (opening it refreshes).
                TrialRoom trial = Object.FindAnyObjectByType<TrialRoom>();
                trial.Complete();
                map.Refresh();
                ScreenAddress trialScreen = ScreenAtWorld(trial.transform.position);
                Assert.IsTrue(map.Markers.Any(m => m.Kind == MapMarkerKind.Trial && m.Screen == trialScreen && m.Done),
                    $"seed {seed}: a solved trial is marked done");
                map.Close();
                yield return null;
            }
        }
    }
}
