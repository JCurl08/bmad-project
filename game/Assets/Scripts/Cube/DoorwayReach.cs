using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Where the player can walk, doorway by doorway (1.25), as pure data: the cube's screens joined through their
    /// doorways (CubeModel.TryStep), where an island screen (islandAt: Math's, from MathPlan) splits into its islands. A
    /// place is a (screen, region): region 0 is the whole of an ordinary screen (all four doorways and everything in it
    /// join), and on an island screen each island is its own region, joined to others only by its planks (broken spans only
    /// with the gap-crosser*). A doorway leads from the island it lands on to the place behind it on the next screen. Every
    /// join works both ways (doorways, planks, mended spans), so the player is never stranded: wherever it can get to, it
    /// can walk back the way it came, out of the doorway it came in by. The seed sweep (SeedSweep, MathPlan) collects every
    /// item with it.
    /// </summary>
    public sealed class DoorwayReach
    {
        private readonly CubeModel model;
        private readonly Func<ScreenAddress, IslandLayout> islandAt;
        private readonly Func<ScreenAddress, Facing, bool> doorOpen;
        private readonly Dictionary<bool, HashSet<(ScreenAddress, int)>> cache = new Dictionary<bool, HashSet<(ScreenAddress, int)>>();

        /// <summary>
        /// The walk over a run's cube. doorOpen (optional, 4.9) shuts doorways: a side of a screen it answers false for leads
        /// nowhere (a locked face-edge arch, EdgeLocks). It answers the same for both sides of a doorway, so every join stays
        /// two-way.
        /// </summary>
        public DoorwayReach(CubeModel model, Func<ScreenAddress, IslandLayout> islandAt = null, Func<ScreenAddress, Facing, bool> doorOpen = null)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.islandAt = islandAt;
            this.doorOpen = doorOpen;
        }

        /// <summary>The island layout of a screen, or null for an ordinary screen.</summary>
        public IslandLayout IslandsOn(ScreenAddress screen) => islandAt?.Invoke(screen);

        /// <summary>The region a doorway of a screen lands on (0 on an ordinary screen; -1 for a side with no doorway).</summary>
        public int RegionOfDoor(ScreenAddress screen, Facing side)
        {
            IslandLayout islands = IslandsOn(screen);
            return islands == null ? 0 : islands.DoorIsland(side);
        }

        /// <summary>The region a point (local to its screen's centre) stands in: 0 on an ordinary screen, its island (or -1 in the water) on an island screen.</summary>
        public int RegionAt(ScreenAddress screen, Vector2 local)
        {
            IslandLayout islands = IslandsOn(screen);
            return islands == null ? 0 : islands.IslandAt(local);
        }

        /// <summary>Every place (screen, region) the player can walk to from the start, with or without the gap-crosser*.</summary>
        public HashSet<(ScreenAddress Screen, int Region)> Reachable(bool gapCrosser)
        {
            if (cache.TryGetValue(gapCrosser, out HashSet<(ScreenAddress, int)> done)) return done;
            var seen = new HashSet<(ScreenAddress, int)>();
            var queue = new Queue<(ScreenAddress, int)>();
            var start = (model.StartScreen, RegionAt(model.StartScreen, Vector2.zero));
            seen.Add(start);
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                (ScreenAddress screen, int region) = queue.Dequeue();
                foreach ((ScreenAddress, int) next in Neighbours(screen, region, gapCrosser))
                    if (seen.Add(next)) queue.Enqueue(next);
            }
            cache[gapCrosser] = seen;
            return seen;
        }

        /// <summary>The places one step from a place: over a plank (an island screen) or through a doorway onto the next screen.</summary>
        public IEnumerable<(ScreenAddress Screen, int Region)> Neighbours(ScreenAddress screen, int region, bool gapCrosser)
        {
            IslandLayout islands = IslandsOn(screen);
            if (islands != null)
                foreach (IslandBridge b in islands.Bridges)
                    if (b.Joins(region) && (!b.Broken || gapCrosser)) yield return (screen, b.Other(region));
            for (int f = 0; f < 4; f++)
            {
                var side = (Facing)f;
                if (RegionOfDoor(screen, side) != region) continue;
                if (doorOpen != null && !doorOpen(screen, side)) continue;
                if (!model.TryStep(screen, side, out ScreenAddress next, out Facing facing)) continue;
                int there = RegionOfDoor(next, facing.Opposite());
                if (there >= 0) yield return (next, there);
            }
        }

        /// <summary>True when the player can stand at a point (local to its screen's centre) with or without the gap-crosser*.</summary>
        public bool CanReach(ScreenAddress screen, Vector2 local, bool gapCrosser)
        {
            int region = RegionAt(screen, local);
            return region >= 0 && Reachable(gapCrosser).Contains((screen, region));
        }

        /// <summary>
        /// Problems with the walk (empty when all is well): every ordinary screen reachable without any item (so the core
        /// portals, the gates and the pickups on them never wait on an island route), every island of every island screen
        /// reachable with the gap-crosser*, and every join two-way (from every place the player can get to, it can walk
        /// back to the start).
        /// </summary>
        public List<string> Problems()
        {
            var problems = new List<string>();
            HashSet<(ScreenAddress Screen, int Region)> bare = Reachable(false), all = Reachable(true);
            foreach (ScreenAddress screen in model.AllScreens())
            {
                if (model.IsSealed(screen.Face)) continue;
                IslandLayout islands = IslandsOn(screen);
                if (islands == null)
                {
                    if (!bare.Contains((screen, 0))) problems.Add($"{screen} cannot be reached without the gap-crosser*");
                    continue;
                }
                for (int i = 0; i < islands.Islands.Count; i++)
                    if (!all.Contains((screen, i))) problems.Add($"island {i} of {screen} can never be reached");
            }
            // Two-way: every step has its way back.
            foreach (bool gap in new[] { false, true })
                foreach ((ScreenAddress screen, int region) in Reachable(gap))
                    foreach ((ScreenAddress, int) next in Neighbours(screen, region, gap))
                    {
                        bool back = false;
                        foreach ((ScreenAddress, int) b in Neighbours(next.Item1, next.Item2, gap))
                            if (b.Item1 == screen && b.Item2 == region) back = true;
                        if (!back) problems.Add($"stranded: from {screen} region {region} to {next.Item1} region {next.Item2} there is no way back");
                    }
            return problems;
        }
    }
}
