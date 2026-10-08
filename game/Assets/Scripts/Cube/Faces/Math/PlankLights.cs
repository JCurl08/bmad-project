using System;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>How a plank of an island screen shows: dark, lit (a network plank crossed, 4.12), or a broken span mended (gold).</summary>
    public enum PlankLight { Unlit = 0, Lit = 1, Mended = 2 }

    /// <summary>What one crossing of a plank did to the island puzzle (4.12).</summary>
    public enum CrossResult
    {
        /// <summary>Nothing: a plank off the lit network, a mended span, or the puzzle is solved already.</summary>
        None = 0,

        /// <summary>A dark network plank lit up.</summary>
        Lit = 1,

        /// <summary>A lit plank was crossed again: every plank went dark.</summary>
        Snuffed = 2,

        /// <summary>The last dark plank lit up: the network is solved, for the rest of the run.</summary>
        Solved = 3,
    }

    /// <summary>
    /// An island screen's "light every plank" state (4.12) as pure data. Each whole plank of the lit network
    /// (IslandLayout.IsNetworkPlank) lights the first time it is crossed; crossing a lit one again snuffs every plank (so any
    /// island can be a clean start: step out and back); lighting the last one solves the network, which then stays lit for
    /// the run. Leaving the screen resets an unsolved network (Reset). A broken span lights nothing: mended, it shows gold
    /// and is crossed for free, and so are planks off the network (the lookouts). 1.26's plank lights are this state now.
    /// </summary>
    public sealed class PlankLights
    {
        private readonly IslandLayout layout;
        private readonly PlankLight[] states;

        public PlankLights(IslandLayout layout)
        {
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
            states = new PlankLight[layout.Bridges.Count];
        }

        public int Count => states.Length;

        public PlankLight this[int plank] => states[plank];

        /// <summary>True once every network plank has been lit (it stays so for the run).</summary>
        public bool Solved { get; private set; }

        /// <summary>The network planks lit now.</summary>
        public int LitCount { get; private set; }

        /// <summary>The planks to light.</summary>
        public int NetworkCount => layout.NetworkPlanks.Count;

        /// <summary>The plank a point (local to the screen centre) stands on, or -1 (an island, the water).</summary>
        public int PlankAt(Vector2 local)
        {
            for (int k = 0; k < states.Length; k++)
                if (layout.Bridges[k].Plank.Contains(local)) return k;
            return -1;
        }

        /// <summary>The player crossed a plank from one island to the other (PlankWalker): lights it, snuffs all, or solves.</summary>
        public CrossResult Cross(int plank)
        {
            if (Solved || plank < 0 || plank >= states.Length || !layout.IsNetworkPlank(plank)) return CrossResult.None;
            if (states[plank] == PlankLight.Lit)
            {
                Snuff();
                return CrossResult.Snuffed;
            }
            states[plank] = PlankLight.Lit;
            LitCount++;
            if (LitCount < NetworkCount) return CrossResult.Lit;
            Solved = true;
            return CrossResult.Solved;
        }

        private void Snuff()
        {
            foreach (int k in layout.NetworkPlanks) states[k] = PlankLight.Unlit;
            LitCount = 0;
        }

        /// <summary>The player left the screen: an unsolved network goes dark. True when something was lit.</summary>
        public bool Reset()
        {
            if (Solved || LitCount == 0) return false;
            Snuff();
            return true;
        }

        /// <summary>True when an island of the network has every one of its network planks lit (its rim glows).</summary>
        public bool IslandComplete(int island)
        {
            bool any = false;
            foreach (int k in layout.NetworkPlanks)
            {
                if (!layout.Bridges[k].Joins(island)) continue;
                if (states[k] != PlankLight.Lit) return false;
                any = true;
            }
            return any;
        }

        /// <summary>A broken span is mended: it shows Mended for good. False for a whole plank or one mended already.</summary>
        public bool Mend(int plank)
        {
            if (plank < 0 || plank >= states.Length || !layout.Bridges[plank].Broken || states[plank] == PlankLight.Mended) return false;
            states[plank] = PlankLight.Mended;
            return true;
        }
    }

    /// <summary>
    /// Turns where the player stands into plank crossings (4.12), pure: a crossing counts only when the player steps off one
    /// island onto a plank and then off that plank onto the island at its other end. Stepping onto a plank and back, a
    /// doorway's lane or landing, or standing in the doorway's mouth never counts.
    /// </summary>
    public sealed class PlankWalker
    {
        private readonly IslandLayout layout;

        public PlankWalker(IslandLayout layout) => this.layout = layout ?? throw new ArgumentNullException(nameof(layout));

        /// <summary>The island the player last stood on (-1: none yet, or it left the screen).</summary>
        public int LastIsland { get; private set; } = -1;

        /// <summary>The plank the player stepped onto from LastIsland (-1: none).</summary>
        public int OnPlank { get; private set; } = -1;

        /// <summary>How finely Step samples the way between two positions (so a frame that jumps a whole plank still counts).</summary>
        public const float SampleStep = 0.1f * Game.Tracer.ScreenMath.ContentScale;

        private Vector2? previous;
        private readonly System.Collections.Generic.List<(int Plank, int From, int To)> crossings =
            new System.Collections.Generic.List<(int, int, int)>();

        /// <summary>Every crossing the last Step made, in order (one, normally; more only for a long jump in one sample).</summary>
        public System.Collections.Generic.IReadOnlyList<(int Plank, int From, int To)> Crossings => crossings;

        /// <summary>
        /// The player stands at a point (local): the plank it just crossed (from, to), or -1. The way from the last point is
        /// sampled every SampleStep (4.12 review), so a single sample that jumps from one island to the next over a plank's
        /// span still counts as crossing it; Crossings lists every crossing found, the last one is returned.
        /// </summary>
        public int Step(Vector2 local, out int from, out int to)
        {
            from = to = -1;
            crossings.Clear();
            if (previous.HasValue)
            {
                Vector2 a = previous.Value;
                int n = Mathf.CeilToInt((local - a).magnitude / SampleStep);
                for (int i = 1; i < n; i++) StepOne(Vector2.Lerp(a, local, (float)i / n));
            }
            previous = local;
            StepOne(local);
            if (crossings.Count == 0) return -1;
            (int plank, int f, int t) = crossings[crossings.Count - 1];
            from = f;
            to = t;
            return plank;
        }

        private void StepOne(Vector2 local)
        {
            int island = layout.IslandAt(local);
            if (island >= 0)
            {
                if (LastIsland >= 0 && island != LastIsland && OnPlank >= 0)
                {
                    IslandBridge b = layout.Bridges[OnPlank];
                    if (b.Joins(LastIsland) && b.Other(LastIsland) == island) crossings.Add((OnPlank, LastIsland, island));
                }
                LastIsland = island;
                OnPlank = -1;
                return;
            }
            for (int k = 0; k < layout.Bridges.Count; k++)
            {
                if (!layout.Bridges[k].Plank.Contains(local)) continue;
                if (OnPlank != k) OnPlank = LastIsland >= 0 && layout.Bridges[k].Joins(LastIsland) ? k : -1;
                break;
            }
        }

        /// <summary>The player left the screen, or was put somewhere new (any teleport): forget where it was.</summary>
        public void Leave()
        {
            LastIsland = -1;
            OnPlank = -1;
            previous = null;
        }
    }
}
