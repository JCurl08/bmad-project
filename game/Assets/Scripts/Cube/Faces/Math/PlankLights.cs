using System;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>How a plank of an island screen shows (1.26, visual only): as built, walked, or a broken span mended.</summary>
    public enum PlankLight { Unlit = 0, Walked = 1, Mended = 2 }

    /// <summary>
    /// The lights of an island screen's planks (1.26) as pure state: a whole plank lights up (Walked) the first time the
    /// player stands on it; a broken span changes colour (Mended) when the gap-crosser* mends it, and a mended span stays
    /// Mended (walking it changes nothing). Standing on a broken span's unmended ends lights nothing. It never gates anything:
    /// IslandScreen only colours the planks from it.
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

        /// <summary>The plank a point (local to the screen centre) stands on, or -1 (an island, the water).</summary>
        public int PlankAt(Vector2 local)
        {
            for (int k = 0; k < states.Length; k++)
                if (layout.Bridges[k].Plank.Contains(local)) return k;
            return -1;
        }

        /// <summary>
        /// The player stands at a point (local): a whole plank under it lights up if it was unlit. Returns that plank, or -1
        /// when nothing changed (no plank, already lit, or a broken span: its light is the mend's).
        /// </summary>
        public int Walk(Vector2 local)
        {
            int k = PlankAt(local);
            if (k < 0 || layout.Bridges[k].Broken || states[k] != PlankLight.Unlit) return -1;
            states[k] = PlankLight.Walked;
            return k;
        }

        /// <summary>A broken span is mended: it shows Mended for good. False for a whole plank or one mended already.</summary>
        public bool Mend(int plank)
        {
            if (plank < 0 || plank >= states.Length || !layout.Bridges[plank].Broken || states[plank] == PlankLight.Mended) return false;
            states[plank] = PlankLight.Mended;
            return true;
        }
    }
}
