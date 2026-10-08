using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A whole-screen puzzle as DoorwayReach sees it (1.25 islands, 4.10 ice, 4.11 wind): the screen splits into
    /// regions, each doorway lands in one (or none), and regions join one another (some joins only with the gap-crosser*).
    /// Every join must have its way back (DoorwayReach.Problems checks it), so the player is never stranded.
    /// </summary>
    public interface IScreenRegions
    {
        /// <summary>How many regions the screen has (0..RegionCount-1).</summary>
        int RegionCount { get; }

        /// <summary>The region a doorway lands in (-1 for a side with no doorway).</summary>
        int DoorRegion(Facing side);

        /// <summary>The region a point (local to the screen's centre) stands in, or -1 (water, a rock, off the puzzle).</summary>
        int RegionAt(Vector2 local);

        /// <summary>The regions one step from a region (with or without the gap-crosser*).</summary>
        IEnumerable<int> Joins(int region, bool gapCrosser);
    }

    /// <summary>
    /// Where the player can walk, doorway by doorway (1.25), as pure data: the cube's screens joined through their
    /// doorways (CubeModel.TryStep), where a puzzle screen (regionsAt: Math's island screens from MathPlan, Earth's ice screens
    /// from IcePlan, 4.11 wind screens from WindPlan) splits into its regions. A place is a (screen, region): region 0 is the whole of an ordinary screen (all
    /// four doorways and everything in it join); on an island screen each island is its own region, joined to others only by
    /// its planks (broken spans only with the gap-crosser*); on an ice screen every doorway lands in region 0 (they join
    /// through the camp) and the goal pad is region 1, joined when the solver reaches it. A doorway leads from the region it
    /// lands on to the place behind it on the next screen. Every join works both ways (doorways, planks, mended spans, the
    /// ice route and its way back), so the player is never stranded: wherever it can get to, it can walk back the way it
    /// came, out of the doorway it came in by. The seed sweep (SeedSweep, MathPlan, RewardPlacement, LockWorld) collects
    /// every item and reward with it.
    /// </summary>
    public sealed class DoorwayReach
    {
        private readonly CubeModel model;
        private readonly Func<ScreenAddress, IScreenRegions> regionsAt;
        private readonly Func<ScreenAddress, Facing, bool> doorOpen;
        private readonly Dictionary<bool, HashSet<(ScreenAddress, int)>> cache = new Dictionary<bool, HashSet<(ScreenAddress, int)>>();

        /// <summary>
        /// The walk over a run's cube. doorOpen (optional, 4.9) shuts doorways: a side of a screen it answers false for leads
        /// nowhere (a locked face-edge arch, EdgeLocks). It answers the same for both sides of a doorway, so every join stays
        /// two-way.
        /// </summary>
        public DoorwayReach(CubeModel model, Func<ScreenAddress, IScreenRegions> regionsAt = null, Func<ScreenAddress, Facing, bool> doorOpen = null)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.regionsAt = regionsAt;
            this.doorOpen = doorOpen;
        }

        /// <summary>The regions of a screen, or null for an ordinary screen.</summary>
        public IScreenRegions RegionsOn(ScreenAddress screen) => regionsAt?.Invoke(screen);

        /// <summary>The island layout of a screen, or null (an ordinary screen, or another kind of puzzle).</summary>
        public IslandLayout IslandsOn(ScreenAddress screen) => RegionsOn(screen) as IslandLayout;

        /// <summary>The region a doorway of a screen lands on (0 on an ordinary screen; -1 for a side with no doorway).</summary>
        public int RegionOfDoor(ScreenAddress screen, Facing side)
        {
            IScreenRegions regions = RegionsOn(screen);
            return regions == null ? 0 : regions.DoorRegion(side);
        }

        /// <summary>The region a point (local to its screen's centre) stands in: 0 on an ordinary screen, its region (or -1) on a puzzle screen.</summary>
        public int RegionAt(ScreenAddress screen, Vector2 local)
        {
            IScreenRegions regions = RegionsOn(screen);
            return regions == null ? 0 : regions.RegionAt(local);
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

        /// <summary>The places one step from a place: a join within a puzzle screen, or through a doorway onto the next screen.</summary>
        public IEnumerable<(ScreenAddress Screen, int Region)> Neighbours(ScreenAddress screen, int region, bool gapCrosser)
        {
            IScreenRegions regions = RegionsOn(screen);
            if (regions != null)
                foreach (int other in regions.Joins(region, gapCrosser))
                    yield return (screen, other);
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
        /// portals, the gates and the pickups on them never wait on an island route), every region of every puzzle screen
        /// reachable with the gap-crosser* (an island, an ice screen's goal pad), and every join two-way (from every place the
        /// player can get to, it can walk back to the start).
        /// </summary>
        public List<string> Problems()
        {
            var problems = new List<string>();
            HashSet<(ScreenAddress Screen, int Region)> bare = Reachable(false), all = Reachable(true);
            foreach (ScreenAddress screen in model.AllScreens())
            {
                if (model.IsSealed(screen.Face)) continue;
                IScreenRegions regions = RegionsOn(screen);
                if (regions == null)
                {
                    if (!bare.Contains((screen, 0))) problems.Add($"{screen} cannot be reached without the gap-crosser*");
                    continue;
                }
                string what = regions is IslandLayout ? "island" : "region";
                for (int i = 0; i < regions.RegionCount; i++)
                    if (!all.Contains((screen, i))) problems.Add($"{what} {i} of {screen} can never be reached");
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
