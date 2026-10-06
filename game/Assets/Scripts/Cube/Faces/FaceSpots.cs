using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Seeded free spots on a face's screens for things a face adds after the reveal (NPCs, enemies, trial
    /// pieces). A spot uses Town's rules (TownPlan.Candidates over the module's blocked rects: off both centre
    /// lanes, inside the edge band, clear of walls and alcoves), keeps MinSpacing from every spot already taken
    /// on that screen, and must pass NpcFactory.IsClear (no solid collider there right now). Spots are in feet
    /// coordinates (an NPC's feet); things smaller than an NPC fit too.
    /// </summary>
    public sealed class FaceSpots
    {
        /// <summary>Minimum distance between two spots on a screen (keeps trial pieces apart for single swings).</summary>
        public const float MinSpacing = 2.2f;

        private readonly CubeWorld world;
        private readonly SeededRng rng;
        private readonly Dictionary<ScreenAddress, List<Vector2>> candidates = new Dictionary<ScreenAddress, List<Vector2>>();
        private readonly Dictionary<ScreenAddress, List<Vector2>> taken = new Dictionary<ScreenAddress, List<Vector2>>();

        public FaceSpots(CubeWorld world, SeededRng rng)
        {
            this.world = world;
            this.rng = rng;
        }

        /// <summary>Picks a free spot (feet, local to the screen centre) on a screen. False when none is left.</summary>
        public bool TryPick(ScreenAddress screen, out Vector2 local)
        {
            if (!candidates.TryGetValue(screen, out List<Vector2> list))
            {
                list = TownPlan.Candidates(TownPlan.BlockedRects(world.ModuleAt(screen)));
                candidates[screen] = list;
                taken[screen] = new List<Vector2>();
            }
            List<Vector2> used = taken[screen];
            var free = new List<Vector2>();
            foreach (Vector2 c in list)
            {
                bool near = false;
                foreach (Vector2 u in used)
                    if ((u - c).sqrMagnitude < MinSpacing * MinSpacing) { near = true; break; }
                if (!near) free.Add(c);
            }
            Vector2 centre = world.ScreenCenter(screen);
            while (free.Count > 0)
            {
                int pick = rng.NextInt(free.Count);
                Vector2 feet = free[pick];
                if (NpcFactory.IsClear(centre + feet, TownPlan.Margin))
                {
                    used.Add(feet);
                    local = feet;
                    return true;
                }
                free.RemoveAt(pick);
            }
            local = default;
            return false;
        }
    }
}
