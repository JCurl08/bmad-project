using System;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The run's outcome, one per scene. EndRun raises RunEnded(victory) exactly once per run; later calls are
    /// ignored. A world rebuild (a new run) resets it. Everything that ends a run (the core boss's outcome, and the
    /// RunLoop for a death outside a core fight) goes through here, so listeners (RunLoop) need only this one event.
    /// </summary>
    public class RunState : MonoBehaviour
    {
        [SerializeField] private CubeWorld world;

        /// <summary>Raised once per run, with true for a victory and false for a defeat.</summary>
        public event Action<bool> RunEnded;

        /// <summary>True once this run has ended (either way).</summary>
        public bool Ended { get; private set; }

        /// <summary>True when the run ended in a victory (meaningful only once Ended).</summary>
        public bool Victory { get; private set; }

        /// <summary>Runs ended since the scene started (one per run at most; handy for tests).</summary>
        public int EndCount { get; private set; }

        public CubeWorld World
        {
            get => world;
            set
            {
                if (world == value) return;
                Unsubscribe();
                world = value;
                if (isActiveAndEnabled) Subscribe();
            }
        }

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (world != null) world.Rebuilt += ResetRun;
        }

        private void Unsubscribe()
        {
            if (world != null) world.Rebuilt -= ResetRun;
        }

        /// <summary>Ends the run. Returns true (and raises RunEnded) only the first time in a run.</summary>
        public bool EndRun(bool victory)
        {
            if (Ended) return false;
            Ended = true;
            Victory = victory;
            EndCount++;
            RunEnded?.Invoke(victory);
            return true;
        }

        /// <summary>A new run: not ended. Called on every world rebuild.</summary>
        public void ResetRun()
        {
            Ended = false;
            Victory = false;
        }
    }
}
