using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tracer.Tests
{
    /// <summary>
    /// I/O matrix rows "Walk across", "Walk back" and "Screen bounds", run against the real Tracer scene
    /// (the only build scene, created by Tracer > Create Tracer Scene).
    /// </summary>
    public class TracerSceneTests : InputTestFixture
    {
        private const string SceneName = "Tracer";
        private const float Timeout = 6f;

        private Keyboard keyboard;
        private Transform player;
        private ScreenCamera screenCamera;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        private IEnumerator LoadTracerScene()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;

            var mover = UnityEngine.Object.FindAnyObjectByType<PlayerMover>();
            Assert.IsNotNull(mover, "Player not found in the Tracer scene");
            player = mover.transform;
            screenCamera = UnityEngine.Object.FindAnyObjectByType<ScreenCamera>();
            Assert.IsNotNull(screenCamera, "ScreenCamera not found in the Tracer scene");
            Assert.AreEqual(new Vector2Int(0, 0), screenCamera.CurrentScreen, "Player should start in screen A");
        }

        private static IEnumerator WaitUntilOrTimeout(Func<bool> condition, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < end)
                yield return null;
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
                yield return null;
        }

        private void AssertCameraOnScreen(Vector2Int screen)
        {
            Assert.AreEqual(screen, screenCamera.CurrentScreen);
            Vector2 expected = ScreenMath.ScreenCenter(screen);
            Vector3 actual = screenCamera.transform.position;
            Assert.AreEqual(expected.x, actual.x, 1e-4f, "Camera x not centred on the screen");
            Assert.AreEqual(expected.y, actual.y, 1e-4f, "Camera y not centred on the screen");
            Assert.AreEqual(5f, screenCamera.GetComponent<Camera>().orthographicSize, 1e-4f, "Camera must show one 16x10 screen");
        }

        [UnityTest]
        public IEnumerator WalkAcross_ThenBack_CameraSnapsBetweenScreens()
        {
            yield return LoadTracerScene();

            // Walk across: hold right through the doorway into screen B.
            Press(keyboard.dKey, queueEventOnly: true);
            yield return WaitUntilOrTimeout(() => player.position.x > 18f, Timeout);
            Release(keyboard.dKey, queueEventOnly: true);
            yield return null;
            yield return null;

            Assert.Greater(player.position.x, 18f, "Player did not reach screen B");
            AssertCameraOnScreen(new Vector2Int(1, 0));

            // Walk back: hold left into screen A.
            Press(keyboard.aKey, queueEventOnly: true);
            yield return WaitUntilOrTimeout(() => player.position.x < 14f, Timeout);
            Release(keyboard.aKey, queueEventOnly: true);
            yield return null;
            yield return null;

            Assert.Less(player.position.x, 14f, "Player did not return to screen A");
            AssertCameraOnScreen(new Vector2Int(0, 0));
        }

        [UnityTest]
        public IEnumerator ScreenBounds_OuterWallsBlockThePlayer()
        {
            yield return LoadTracerScene();

            // Push into the bottom-left corner (outer walls on both axes, no neighbour screen).
            Press(keyboard.aKey, queueEventOnly: true);
            Press(keyboard.sKey, queueEventOnly: true);
            yield return WaitSeconds(1.5f);

            Vector3 p = player.position;
            const float wallInner = 1f;   // wall thickness
            const float radius = 0.4f;    // player collider radius
            const float tolerance = 0.05f;
            Assert.GreaterOrEqual(p.x, wallInner + radius - tolerance, "Player passed through the left wall");
            Assert.GreaterOrEqual(p.y, wallInner + radius - tolerance, "Player passed through the bottom wall");
            Assert.AreEqual(new Vector2Int(0, 0), screenCamera.CurrentScreen);

            Release(keyboard.aKey, queueEventOnly: true);
            Release(keyboard.sKey, queueEventOnly: true);

            // Push into the top wall as well.
            Press(keyboard.wKey, queueEventOnly: true);
            yield return WaitSeconds(1.5f);
            Release(keyboard.wKey, queueEventOnly: true);
            Assert.LessOrEqual(player.position.y, 10f - wallInner - radius + tolerance, "Player passed through the top wall");
            Assert.AreEqual(new Vector2Int(0, 0), screenCamera.CurrentScreen);
        }
    }
}
