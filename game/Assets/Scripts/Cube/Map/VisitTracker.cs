using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>Pure set of the screens visited in one run (the map's fog: only these are lit).</summary>
    public sealed class VisitSet
    {
        private readonly HashSet<ScreenAddress> visited = new HashSet<ScreenAddress>();

        public int Count => visited.Count;

        public IEnumerable<ScreenAddress> All => visited;

        /// <summary>Marks a screen visited. Returns true when it was new.</summary>
        public bool Visit(ScreenAddress screen) => visited.Add(screen);

        public bool IsVisited(ScreenAddress screen) => visited.Contains(screen);

        public void Clear() => visited.Clear();
    }

    /// <summary>
    /// The screens the player has visited this run, for the cube-net map. It follows the navigator's screen after every
    /// frame (outside the core arena) and resets on every world rebuild (a new run: Continue, F10, the reroll), when only
    /// the Town start screen is visited again. VisitAll (debug) lights every unsealed screen.
    /// </summary>
    [DefaultExecutionOrder(100)] // after the navigator and the debug jumps have moved the player this frame
    public class VisitTracker : MonoBehaviour
    {
        [SerializeField] private CubeWorld world;
        [SerializeField] private CubeNavigator navigator;

        private readonly VisitSet visits = new VisitSet();
        private CubeWorld subscribed;

        public CubeWorld World
        {
            get => world;
            set
            {
                Unsubscribe();
                world = value;
                if (isActiveAndEnabled) Subscribe();
            }
        }

        public CubeNavigator Navigator
        {
            get => navigator;
            set => navigator = value;
        }

        public VisitSet Visits => visits;

        /// <summary>Runs tracked so far (each rebuild starts a new one).</summary>
        public int RunsTracked { get; private set; }

        /// <summary>Raised when a screen is visited for the first time this run.</summary>
        public event Action<ScreenAddress> Visited;

        /// <summary>Raised after a reset (a new run).</summary>
        public event Action Reset;

        public bool IsVisited(ScreenAddress screen) => visits.IsVisited(screen);

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();

        private void Start()
        {
            if (world != null && world.Model != null && visits.Count == 0) ResetRun();
        }

        private void LateUpdate() => Track();

        /// <summary>Marks the player's current screen visited (nothing while in the core arena). Returns true if it was new.</summary>
        public bool Track()
        {
            if (world == null || world.Model == null || navigator == null || navigator.InCoreArena) return false;
            return Visit(navigator.Current);
        }

        /// <summary>Marks a screen visited. Returns true when it was new this run.</summary>
        public bool Visit(ScreenAddress screen)
        {
            if (!visits.Visit(screen)) return false;
            Visited?.Invoke(screen);
            return true;
        }

        /// <summary>Starts a new run's fog: forgets every visit, then visits the Town start screen.</summary>
        public void ResetRun()
        {
            visits.Clear();
            RunsTracked++;
            if (world != null && world.Model != null) Visit(world.Model.StartScreen);
            Reset?.Invoke();
        }

        /// <summary>Debug: visits every screen on an unsealed face (the map then shows the whole run).</summary>
        public void VisitAll()
        {
            if (world == null || world.Model == null) return;
            foreach (ScreenAddress screen in world.Model.AllScreens())
                if (!world.Model.IsSealed(screen.Face)) Visit(screen);
        }

        private void Subscribe()
        {
            if (world == null || subscribed == world) return;
            Unsubscribe();
            subscribed = world;
            world.Rebuilt += ResetRun;
        }

        private void Unsubscribe()
        {
            if (subscribed == null) return;
            subscribed.Rebuilt -= ResetRun;
            subscribed = null;
        }
    }
}
