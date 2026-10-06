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

        private KeyControl KeyFor(Facing direction)
        {
            switch (direction)
            {
                case Facing.North: return keyboard.wKey;
                case Facing.East: return keyboard.dKey;
                case Facing.South: return keyboard.sKey;
                default: return keyboard.aKey;
            }
        }

        /// <summary>The screen a straight walk from the start screen leaves the face from.</summary>
        private ScreenAddress ExitScreen(Facing direction)
        {
            ScreenAddress here = world.Model.StartScreen;
            while (world.Model.IsInside(here.Cell + direction.ToVector()))
                here = new ScreenAddress(here.Face, here.Cell + direction.ToVector());
            return here;
        }

        private static IEnumerator WaitUntilOrTimeout(Func<bool> condition, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < end)
                yield return null;
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

            ScreenAddress arrived = default;
            bool crossed = false;
            Press(KeyFor(direction), queueEventOnly: true);
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
            Release(KeyFor(direction), queueEventOnly: true);
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
        }

        [UnityTest]
        public IEnumerator WalkTowardSealedFace_IsBlocked()
        {
            yield return LoadCubeScene();
            CubeModel model = world.Model;

            // Two of the five science faces are sealed, so at least one of Town's four neighbours is.
            Facing direction = new[] { Facing.East, Facing.North, Facing.West, Facing.South }
                .First(d => model.IsSealed(CubeModel.NeighborFace(CubeModel.StartFace, d)));

            // Walk until the player stops advancing (pressed against the wall), with a generous timeout.
            Press(KeyFor(direction), queueEventOnly: true);
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
            Release(KeyFor(direction), queueEventOnly: true);
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
            Press(keyboard.f5Key, queueEventOnly: true);
            yield return null;
            yield return null;
            Release(keyboard.f5Key, queueEventOnly: true);
            yield return null;
            Assert.AreNotEqual(oldSeed, world.Seed, "F5 did not reroll the seed");
            Assert.AreEqual(world.Seed, world.Model.Seed);
            Assert.AreEqual(world.Model.StartScreen, navigator.Current, "Reroll should return the player to the start screen");

            ScreenAddress before = navigator.Current;
            Press(keyboard.f6Key, queueEventOnly: true);
            yield return null;
            yield return null;
            Release(keyboard.f6Key, queueEventOnly: true);
            yield return null;
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
    }
}
