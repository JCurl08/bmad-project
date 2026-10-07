using System;
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
    /// Runtime play against the real Cube scene (created by Cube > Create Cube Scene): walking off a
    /// Town edge lands where CubeModel predicts, walking toward a sealed face is blocked, and the
    /// F5 / F6 debug commands work.
    /// </summary>
    public class CubeSceneTests : InputTestFixture
    {
        private const string SceneName = "Cube";
        private const float WalkTimeout = 10f;

        private Keyboard keyboard;
        private CubeWorld world;
        private CubeNavigator navigator;
        private ScreenCamera screenCamera;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        private IEnumerator LoadCubeScene()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null; // (not WaitForFixedUpdate: input state is only valid in dynamic update)

            world = UnityEngine.Object.FindAnyObjectByType<CubeWorld>();
            navigator = UnityEngine.Object.FindAnyObjectByType<CubeNavigator>();
            screenCamera = UnityEngine.Object.FindAnyObjectByType<ScreenCamera>();
            Assert.IsNotNull(world, "CubeWorld not found in the Cube scene");
            Assert.IsNotNull(navigator, "CubeNavigator not found in the Cube scene");
            Assert.IsNotNull(screenCamera, "ScreenCamera not found in the Cube scene");
            Assert.IsNotNull(world.Model, "CubeWorld did not build its model");
            Assert.AreEqual(world.Model.StartScreen, navigator.Current, "Player should start on the Town start screen");
            Assert.AreEqual(Theme.Town, world.Model.ThemeOf(navigator.Face));
        }

        /// <summary>The screen a straight walk from the start screen leaves the face from.</summary>
        private ScreenAddress ExitScreen(Facing direction)
        {
            ScreenAddress here = world.Model.StartScreen;
            while (world.Model.IsInside(here.Cell + direction.ToVector()))
                here = new ScreenAddress(here.Face, here.Cell + direction.ToVector());
            return here;
        }


        [UnityTest]
        public IEnumerator WalkOffTownEdge_ArrivesOnAdjacentFaceAsModelPredicts()
        {
            yield return LoadCubeScene();
            CubeModel model = world.Model;

            // East first (the most natural walk), otherwise any direction into an unsealed face.
            Facing direction = new[] { Facing.East, Facing.North, Facing.West, Facing.South }
                .First(d => !model.IsSealed(CubeModel.NeighborFace(CubeModel.StartFace, d)));
            ScreenAddress exit = ExitScreen(direction);
            Assert.IsTrue(model.TryStep(exit, direction, out ScreenAddress expected, out Facing expectedFacing));

            // Every screen that belongs to a face; the camera must never show anything else (e.g. the gap).
            var faceScreens = new HashSet<Vector2Int>(
                model.AllScreens().Select(s => ScreenMath.ScreenIndex(world.ScreenCenter(s))));
            string offFace = null;
            void CheckCamera(string phase)
            {
                if (offFace == null && !faceScreens.Contains(screenCamera.CurrentScreen))
                    offFace = $"{phase}: camera on screen {screenCamera.CurrentScreen}, player at {navigator.transform.position}";
            }

            Assert.IsFalse(world.ScienceRevealed, "Science faces must stay unrevealed until leaving town");
            ScreenAddress arrived = default;
            bool crossed = false;
            Press(KeyFor(keyboard, direction), queueEventOnly: true);
            yield return WaitUntilOrTimeout(() =>
            {
                CheckCamera("before crossing");
                if (navigator.Face == CubeModel.StartFace) return false;
                crossed = true;
                arrived = navigator.Current;
                return true;
            }, WalkTimeout);
            // Keep walking a little after the crossing and keep watching the camera.
            float keepWalkingUntil = Time.realtimeSinceStartup + 0.5f;
            while (Time.realtimeSinceStartup < keepWalkingUntil)
            {
                CheckCamera("after crossing");
                yield return null;
            }
            Release(KeyFor(keyboard, direction), queueEventOnly: true);
            yield return null;
            yield return null;

            Assert.IsTrue(crossed, $"Player never left Town walking {direction}");
            Assert.IsNull(offFace, "Camera showed a screen outside every face");
            Assert.AreEqual(expected, arrived, $"Walking {direction} from {exit}");
            Assert.AreEqual(expectedFacing, navigator.Facing);
            Assert.AreEqual(1, navigator.Crossings);

            Vector2 local = (Vector2)navigator.transform.position - world.FaceOrigin(expected.Face);
            Assert.That(local.x, Is.InRange(0f, world.FaceExtent.x), "Player x outside the new face");
            Assert.That(local.y, Is.InRange(0f, world.FaceExtent.y), "Player y outside the new face");
            Assert.AreEqual(ScreenMath.ScreenIndex(navigator.transform.position), screenCamera.CurrentScreen,
                "Camera did not follow the player onto the new face");

            // Leaving town revealed the science layout, generated from the run seed alone.
            Assert.IsTrue(world.ScienceRevealed, "Crossing off Town did not reveal the science faces");
            Assert.IsNotNull(world.Layout);
            Assert.AreEqual(CubeLayout.Generate(new CubeModel(world.Seed), world.Library).Signature(),
                world.Layout.Signature(), "Revealed layout differs from the seed's layout");
            ScreenModule landedOn = world.ModuleAt(arrived);
            Assert.IsNotNull(landedOn, $"Landed on {arrived}, which has no module");
            Assert.AreEqual(model.ThemeOf(arrived.Face), landedOn.Theme);
            AssertLaidOut(model);
        }

        /// <summary>Every built science screen has a module of its theme; one active core entrance per face; sealed faces are empty.</summary>
        private void AssertLaidOut(CubeModel model)
        {
            foreach (FaceId face in Enum.GetValues(typeof(FaceId)))
            {
                var screens = model.AllScreens().Where(s => s.Face == face).ToList();
                if (model.IsSealed(face))
                {
                    Assert.IsTrue(screens.All(s => world.ModuleAt(s) == null), $"Sealed {face} has modules");
                    continue;
                }
                Assert.IsTrue(screens.All(s => world.ModuleAt(s) != null && world.ModuleAt(s).Theme == model.ThemeOf(face)),
                    $"{face} is not fully laid out with {model.ThemeOf(face)} modules");
                foreach (ScreenAddress s in screens)
                {
                    foreach (ExitSlot exit in world.ModuleAt(s).Exits)
                    {
                        bool open = model.TryStep(s, exit.Facing, out _, out _);
                        Assert.AreEqual(open, exit.gameObject.activeSelf,
                            $"{s} {exit.Label}: active exit slots must not face a sealed face, open ones must stay active");
                    }
                }
                if (face == CubeModel.StartFace) continue;
                int activeCores = screens.Sum(s => world.ModuleAt(s).ActiveCoreEntrances().Count);
                Assert.AreEqual(1, activeCores, $"{face} ({model.ThemeOf(face)}) active core entrances");
            }
        }

        [UnityTest]
        public IEnumerator RunStart_OnlyTownIsLaidOut_ScienceUnrevealed()
        {
            yield return LoadCubeScene();
            CubeModel model = world.Model;
            Assert.IsNotNull(world.Library, "CubeWorld has no ModuleLibrary wired in");
            Assert.IsFalse(world.ScienceRevealed);
            Assert.IsNull(world.Layout);
            foreach (ScreenAddress screen in model.AllScreens())
            {
                ScreenModule module = world.ModuleAt(screen);
                if (screen.Face == CubeModel.StartFace)
                {
                    Assert.IsNotNull(module, $"Town screen {screen} has no module");
                    Assert.AreEqual(Theme.Town, module.Theme);
                    Assert.AreEqual(world.Library.TownModule(screen.Cell, model.FaceSize).name,
                        module.name.Split(new[] { " @ " }, StringSplitOptions.None)[0], $"Town module on {screen} is not the fixed one");
                }
                else
                {
                    Assert.IsNull(module, $"{screen} was laid out before leaving town");
                }
            }
            // Town is N x N distinct screens: one module per cell, none repeated.
            List<ScreenModule> townModules = model.AllScreens().Where(s => s.Face == CubeModel.StartFace).Select(s => world.ModuleAt(s)).ToList();
            Assert.AreEqual(model.FaceSize * model.FaceSize, townModules.Count);
            foreach (ScreenAddress cell in model.AllScreens().Where(s => s.Face == CubeModel.StartFace))
                Assert.IsNotNull(world.ModuleAt(cell), $"Town cell {cell} has a module");
            Assert.AreEqual(townModules.Count, townModules.Select(m => m.name.Split(new[] { " @ " }, StringSplitOptions.None)[0]).Distinct().Count(),
                "Every Town screen shows its own module");

            // Arriving on a built science face by debug teleport also reveals; a second reveal is a no-op.
            ScreenAddress science = model.AllScreens().First(s => CubeLayout.IsLaidOut(model, s.Face));
            navigator.TeleportTo(science, Facing.East);
            Assert.IsTrue(world.ScienceRevealed, "Teleporting onto a science face did not reveal it");
            Assert.IsNotNull(world.ModuleAt(science));
            string signature = world.Layout.Signature();
            Assert.IsFalse(world.RevealScience());
            Assert.AreEqual(signature, world.Layout.Signature());
            yield return null;
            AssertLaidOut(model);
        }

        [UnityTest]
        public IEnumerator DebugF2_ShowsLabelledMarkersOnEverySlot()
        {
            yield return LoadCubeScene();
            var debug = UnityEngine.Object.FindAnyObjectByType<CubeDebug>();
            Assert.IsNotNull(debug);
            Assert.IsFalse(debug.SlotMarkersVisible, "Slot markers should start hidden");

            yield return this.Tap(keyboard.f2Key, holdFrames: 2);
            Assert.IsTrue(debug.SlotMarkersVisible, "F2 did not turn slot markers on");

            ScreenModule here = world.ModuleAt(navigator.Current);
            Assert.IsNotNull(here);
            var active = here.AllSlots.Where(s => s.gameObject.activeInHierarchy).ToList();
            int openSides = Enum.GetValues(typeof(Facing)).Cast<Facing>()
                .Count(f => world.Model.TryStep(navigator.Current, f, out _, out _));
            Assert.AreEqual(openSides, active.OfType<ExitSlot>().Count(), "One active exit per open side");
            Assert.GreaterOrEqual(active.OfType<GateSlot>().Count(), 1);
            Assert.AreEqual(1, active.OfType<HiddenItemSlot>().Count());
            foreach (ModuleSlot slot in active)
            {
                Assert.IsTrue(slot.MarkerVisible, $"{slot.Label} on {here.name} has no visible marker");
                Assert.IsNotEmpty(slot.Label);
            }

            // Modules created later (the science reveal) get markers too, including the active core entrance.
            world.RevealScience();
            yield return null;
            int coreMarkers = 0;
            foreach (ScreenModule module in world.AllModules)
            {
                foreach (ModuleSlot slot in module.AllSlots.Where(s => s.gameObject.activeInHierarchy))
                {
                    Assert.IsTrue(slot.MarkerVisible, $"{slot.Label} on {module.name} has no visible marker");
                    if (slot is CoreEntranceSlot) coreMarkers++;
                }
            }
            Assert.AreEqual(5, coreMarkers, "One core-entrance marker per built science face");

            yield return this.Tap(keyboard.f2Key, holdFrames: 2);
            Assert.IsFalse(debug.SlotMarkersVisible, "F2 did not turn slot markers off");
            Assert.IsTrue(world.AllModules.SelectMany(m => m.AllSlots).All(s => !s.MarkerVisible));
        }

        [UnityTest]
        public IEnumerator WalkTowardSealedFace_IsBlocked()
        {
            yield return LoadCubeScene();
            // Nothing is sealed in play since Earth was built; the sealed-face path is kept for faces sealed on purpose.
            // Seal Earth & Atmosphere and start a run on a seed where it borders Town.
            Theme[] sealedThemes = { Theme.EarthAtmosphere };
            int seed = Enumerable.Range(1, 200).First(s =>
            {
                var m = new CubeModel(s, CubeSettings.DefaultFaceSize, sealedThemes);
                return new[] { Facing.East, Facing.North, Facing.West, Facing.South }.Any(d => m.IsSealed(CubeModel.NeighborFace(CubeModel.StartFace, d)));
            });
            world.SealedThemes.Clear();
            world.SealedThemes.AddRange(sealedThemes);
            try
            {
                world.Rebuild(seed);
            }
            finally
            {
                world.SealedThemes.Clear();
            }
            yield return null;
            yield return null;
            CubeModel model = world.Model;
            Assert.IsTrue(model.IsSealed(model.FaceOf(Theme.EarthAtmosphere)), "Earth is sealed for this run");

            // Earth & Atmosphere is sealed and borders Town on this seed.
            Facing direction = new[] { Facing.East, Facing.North, Facing.West, Facing.South }
                .First(d => model.IsSealed(CubeModel.NeighborFace(CubeModel.StartFace, d)));

            // Walk until the player stops advancing (pressed against the wall), with a generous timeout.
            Press(KeyFor(keyboard, direction), queueEventOnly: true);
            float end = Time.realtimeSinceStartup + 20f;
            Vector3 last = navigator.transform.position;
            float stillSince = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup < end && Time.realtimeSinceStartup - stillSince < 0.5f)
            {
                yield return null;
                Vector3 now = navigator.transform.position;
                if ((now - last).sqrMagnitude > 1e-6f) stillSince = Time.realtimeSinceStartup;
                last = now;
            }
            Release(KeyFor(keyboard, direction), queueEventOnly: true);
            yield return null;

            Assert.AreEqual(CubeModel.StartFace, navigator.Face, $"Player crossed into sealed {CubeModel.NeighborFace(CubeModel.StartFace, direction)}");
            Assert.AreEqual(0, navigator.Crossings);
            Assert.AreEqual(ExitScreen(direction), navigator.Current, "Player should be stopped at the sealed edge");

            // Actually against the sealed edge: within wall thickness + collider radius (+ epsilon) of it.
            const float reach = 1f + 0.4f + 0.05f;
            Vector2 extent = world.FaceExtent;
            Vector2 local = (Vector2)navigator.transform.position - world.FaceOrigin(CubeModel.StartFace);
            Assert.That(local.x, Is.InRange(0f, extent.x));
            Assert.That(local.y, Is.InRange(0f, extent.y));
            switch (direction)
            {
                case Facing.East: Assert.GreaterOrEqual(local.x, extent.x - reach, "Player did not reach the east wall"); break;
                case Facing.West: Assert.LessOrEqual(local.x, reach, "Player did not reach the west wall"); break;
                case Facing.North: Assert.GreaterOrEqual(local.y, extent.y - reach, "Player did not reach the north wall"); break;
                default: Assert.LessOrEqual(local.y, reach, "Player did not reach the south wall"); break;
            }
        }

        [UnityTest]
        public IEnumerator DebugKeys_F5Rerolls_F6JumpsToAnotherUnsealedScreen()
        {
            yield return LoadCubeScene();
            var debug = UnityEngine.Object.FindAnyObjectByType<CubeDebug>();
            Assert.IsNotNull(debug, "CubeDebug not found in the Cube scene");
            Assert.IsTrue(debug.enabled, "Debug tools must be active in the editor");

            int oldSeed = world.Seed;
            yield return this.Tap(keyboard.f5Key, holdFrames: 2);
            Assert.AreNotEqual(oldSeed, world.Seed, "F5 did not reroll the seed");
            Assert.AreEqual(world.Seed, world.Model.Seed);
            Assert.AreEqual(world.Model.StartScreen, navigator.Current, "Reroll should return the player to the start screen");
            Assert.IsFalse(world.ScienceRevealed, "A reroll starts a new run with the science faces unrevealed");

            ScreenAddress before = navigator.Current;
            yield return this.Tap(keyboard.f6Key, holdFrames: 2);
            ScreenAddress after = navigator.Current;
            Assert.AreNotEqual(before, after, "F6 did not move the player");
            Assert.IsFalse(world.Model.IsSealed(after.Face), "F6 jumped onto a sealed face");

            bool overlay = debug.OverlayVisible;
            Press(keyboard.f1Key, queueEventOnly: true);
            yield return null;
            yield return null;
            Release(keyboard.f1Key, queueEventOnly: true);
            Assert.AreNotEqual(overlay, debug.OverlayVisible, "F1 did not toggle the overlay");
        }

        /// <summary>The look a hint should name for a gate the reveal built, read from its component alone.</summary>
        private static string BuiltLook(Gate gate)
        {
            switch (gate)
            {
                case BreakableGate b: return b.Look == BreakableLook.Pot ? GateLooks.Pot : GateLooks.Rock;
                case FlowerVineGate _: return GateLooks.Bramble;
                case ButtonGate _: return GateLooks.ButtonDoor;
                case DarkRoomGate _: return GateLooks.DarkDoorway;
                case CrackedWallGate _: return GateLooks.CrackedWall;
                case LeadPlateGate _: return GateLooks.PlatedDoor;
                case TimedDoorGate _: return GateLooks.SwingingDoor;
                case GapGate _: return GateLooks.Gap;
                case BridgeDoorGate _: return GateLooks.PlankDoor;
                case PhenomenonGate p: return GateLooks.Of(p.Kind);
                default: return gate.GetType() == typeof(Gate) ? GateLooks.Plain : $"(unknown gate {gate.GetType().Name})";
            }
        }

        /// <summary>
        /// One source of truth for gate looks: on seeds 1-50, every required gate's hint look (from the face plans, as
        /// RunFacts gives it to the hints) is the look of the gate the reveal actually built in that slot.
        /// </summary>
        [UnityTest]
        public IEnumerator GateLooks_HintsNameTheGateTheRevealBuilt_OnSeeds1To50()
        {
            yield return LoadCubeScene();
            var looks = new HashSet<string>();
            int count = 0;
            for (int seed = 1; seed <= 50; seed++)
            {
                world.Rebuild(seed);
                Assert.IsTrue(world.RevealScience(), $"seed {seed}: the reveal");
                RunFacts facts = RunFacts.Of(world);
                foreach (GatePlacement g in world.ItemPlacement.Gates)
                {
                    ScreenModule module = world.ModuleAt(g.Screen);
                    Assert.IsNotNull(module, $"seed {seed}: {g}");
                    Gate[] built = module.Gates[g.Slot].GetComponentsInChildren<Gate>(true);
                    Assert.AreEqual(1, built.Length, $"seed {seed}: one gate in {g}");
                    string look = BuiltLook(built[0]);
                    Assert.AreEqual(look, HintGenerator.LookOf(facts, g), $"seed {seed}: the hint look of {g}");
                    looks.Add(look);
                    count++;
                }
                yield return null; // let the last run's objects go
            }
            Assert.Greater(count, 250, "Every required gate of 50 runs");
            Assert.GreaterOrEqual(looks.Count, 8, $"Gates of every face's kinds: {string.Join(", ", looks)}");
        }
    }
}
