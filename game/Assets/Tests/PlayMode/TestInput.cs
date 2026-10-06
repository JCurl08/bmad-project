using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Shared keyboard input and waiting for the PlayMode tests (extension methods on InputTestFixture, whose Press and
    /// Release they use). They encode the workarounds the suites rely on:
    /// every press and release is queued (queueEventOnly), so it lands in the next input update rather than mid-frame;
    /// a tapped key is held for at least one frame (the scene tests hold it for two, Tap's holdFrames) and a frame
    /// passes after the release;
    /// a key that must reach FixedUpdate is never pressed right after a WaitForFixedUpdate (input state is only valid
    /// in the dynamic update) and is held across two physics steps (TapThroughPhysics);
    /// and several keys held together change the keyboard's whole state in one event (SetKeys), so a diagonal never
    /// passes through a one-key frame.
    /// </summary>
    public static class TestInput
    {
        /// <summary>Presses a key, holds it for holdFrames frames, releases it and waits one more frame.</summary>
        public static IEnumerator Tap(this InputTestFixture input, ButtonControl key, int holdFrames = 1)
        {
            input.Press(key, queueEventOnly: true);
            for (int i = 0; i < holdFrames; i++) yield return null;
            input.Release(key, queueEventOnly: true);
            yield return null;
        }

        /// <summary>Holds a key until condition holds or timeout real seconds pass, then releases it (no frame after).</summary>
        public static IEnumerator HoldUntil(this InputTestFixture input, ButtonControl key, Func<bool> condition, float timeout)
        {
            input.Press(key, queueEventOnly: true);
            yield return WaitUntilOrTimeout(condition, timeout);
            input.Release(key, queueEventOnly: true);
        }

        /// <summary>
        /// A tap the physics step sees: waits a frame (never press right after WaitForFixedUpdate), holds the key across
        /// two physics steps and a frame, releases it, then lets one physics step and one frame pass.
        /// </summary>
        public static IEnumerator TapThroughPhysics(this InputTestFixture input, ButtonControl key)
        {
            yield return null; // input state is only valid in dynamic update
            input.Press(key, queueEventOnly: true);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;
            input.Release(key, queueEventOnly: true);
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        /// <summary>The WASD key that moves the player toward a facing.</summary>
        public static KeyControl KeyFor(Keyboard keyboard, Facing direction)
        {
            switch (direction)
            {
                case Facing.North: return keyboard.wKey;
                case Facing.East: return keyboard.dKey;
                case Facing.South: return keyboard.sKey;
                default: return keyboard.aKey;
            }
        }

        /// <summary>The WASD keys for an 8-way direction (each axis -1, 0 or 1; any sign counts); none for zero.</summary>
        public static Key[] WasdKeys(Vector2Int dir)
        {
            var keys = new List<Key>();
            if (dir.x > 0) keys.Add(Key.D);
            if (dir.x < 0) keys.Add(Key.A);
            if (dir.y > 0) keys.Add(Key.W);
            if (dir.y < 0) keys.Add(Key.S);
            return keys.ToArray();
        }

        /// <summary>Sets the keyboard's whole state in one queued event: exactly these keys down, every other key up.</summary>
        public static void SetKeys(Keyboard keyboard, params Key[] keys) =>
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

        /// <summary>Waits until condition holds or timeout real (unscaled) seconds pass.</summary>
        public static IEnumerator WaitUntilOrTimeout(Func<bool> condition, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < end)
                yield return null;
        }

        /// <summary>Waits until condition holds or timeout game (scaled) seconds pass, for tests that speed time up.</summary>
        public static IEnumerator WaitUntilOrGameTimeout(Func<bool> condition, float timeout)
        {
            float end = Time.time + timeout;
            while (!condition() && Time.time < end)
                yield return null;
        }

        /// <summary>Lets steps physics steps run, then one frame.</summary>
        public static IEnumerator PhysicsSteps(int steps = 3)
        {
            for (int i = 0; i < steps; i++) yield return new WaitForFixedUpdate();
            yield return null;
        }

        /// <summary>Waits seconds of real (unscaled) time.</summary>
        public static IEnumerator WaitRealSeconds(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
                yield return null;
        }
    }
}
