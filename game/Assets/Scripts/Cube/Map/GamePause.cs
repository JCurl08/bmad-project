using System;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The gameplay pause the cube-net map puts the game in (CubeMap): time is stopped (timeScale 0) by the map, and the
    /// player's Update-driven input (attack, item cycling, the Mass Mitt, talking and the dialogue box) checks Active and
    /// ignores presses while it is set. Movement and every physics step stop with time itself. Reset on every play start.
    /// </summary>
    public static class GamePause
    {
        /// <summary>True while gameplay is paused (the map is open): player, attack and dialogue input is ignored.</summary>
        public static bool Active { get; private set; }

        private static float savedTimeScale = 1f;

        /// <summary>Raised when gameplay pauses (the map opens), e.g. so the player's turned controls return to normal.</summary>
        public static event Action Paused;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active = false;
            savedTimeScale = 1f;
            Paused = null;
        }

        /// <summary>Pauses: stops time (remembering the time scale) and sets Active. Does nothing when already paused.</summary>
        public static void Pause()
        {
            if (Active) return;
            Active = true;
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            Paused?.Invoke();
        }

        /// <summary>Resumes: restores the time scale saved by Pause and clears Active. Does nothing when not paused.</summary>
        public static void Resume()
        {
            if (!Active) return;
            Active = false;
            // Only put time back if nothing else changed it meanwhile.
            if (Time.timeScale == 0f) Time.timeScale = savedTimeScale;
        }
    }
}
