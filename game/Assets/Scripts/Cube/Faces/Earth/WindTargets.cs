using System;
using System.Collections.Generic;
using System.Globalization;

namespace Game.Cube
{
    /// <summary>
    /// What the solver measures on a wind screen (4.11), from one doorway: the research note's section 1c metrics.
    /// <list type="bullet">
    /// <item>Regions (R): the walkable areas joined without riding (the ring, each island or twin half, each grip anchor).</item>
    /// <item>Streams: the screen's streams (one per first tile; an anchor splits a stream into two).</item>
    /// <item>MinRides: fewest rides from the doorway's region (the ring) to the goal pad's.</item>
    /// <item>Decoys: rides from a reached region onto a region that is on no shortest route to the goal.</item>
    /// <item>ReturnCost: the most rides from any reached region back to the doorway's region.</item>
    /// <item>Traps: reached regions with no rides back to a doorway. Unreached: regions the doorway never reaches (clutter).</item>
    /// <item>UnusedStreams: streams no reached region can set off on (clutter).</item>
    /// <item>RandomSolveRate: P_rand, exactly (PuzzleGraph.RandomSolveRate), capped at 3x MinRides rides.</item>
    /// <item>LongestRide: the most stream tiles one reachable ride crosses. Crossing: the most rides between two doorways.</item>
    /// <item>Fans: fan switches (always 0: 4.11 skips them, see WindTargets).</item>
    /// </list>
    /// </summary>
    public sealed class WindMetrics
    {
        public bool Solvable;
        public int Regions;
        public int Streams;
        public int MinRides;
        public int Decoys;
        public int ReturnCost;
        public int Traps;
        public int Unreached;
        public int UnusedStreams;
        public double RandomSolveRate;
        public int LongestRide;
        public int Crossing;
        public int Anchors;
        public int Walls;
        public long ShortestSolutions;
        public int Fans;

        public override string ToString()
        {
            string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
            return Solvable
                ? $"regions {Regions}, streams {Streams}, min rides {MinRides}, decoys {Decoys}, return {ReturnCost}, traps {Traps}, " +
                  $"P_rand {F(RandomSolveRate * 100)}%, longest {LongestRide}, anchors {Anchors}, walls {Walls}, crossing {Crossing}, " +
                  $"unreached {Unreached}, unused {UnusedStreams}"
                : $"unsolvable (regions {Regions}, unreached {Unreached}, traps {Traps})";
        }
    }

    /// <summary>
    /// The research note's targets for wind streams per tier (puzzle-difficulty-research.md 1c), as pass/fail bands, with the
    /// islands each tier deals. Departures, marked as assumptions in the 4.11 plan: fan switches are left out (the note allows
    /// 0-1 at mid; late's 1-2 is not met); anchors count as regions and split a stream in two; every region must be reachable
    /// and every stream ridable (no clutter); a crossing between doorways costs 0 rides (they share the ring).
    /// </summary>
    public sealed class WindTargets
    {
        public PuzzleTier Tier;
        public int RegionsLo, RegionsHi;
        public int StreamsLo, StreamsHi;
        public int MinRidesLo, MinRidesHi;
        public int DecoysLo, DecoysHi;
        public int ReturnMax;
        public double RandomLo, RandomHi;
        public int LongestMax;

        /// <summary>
        /// Fan switches the note asks for: NOT BUILT in 4.11 (the story allows it), so Misses and Score never check them; the
        /// sweep reports "fans 0 (not built)" and the targets checked are every built one.
        /// </summary>
        public int FansLo, FansHi;

        /// <summary>Islands dealt per screen, and the chance (percent) that one is a twin split by a wall.</summary>
        public int IslandsLo, IslandsHi, TwinPercent;

        /// <summary>A crossing between two doorways may cost at most this share of the reward route (research note section 0).</summary>
        public const float CrossingShare = 0.5f;

        public static readonly WindTargets Early = new WindTargets
        {
            Tier = PuzzleTier.Early, RegionsLo = 3, RegionsHi = 5, StreamsLo = 3, StreamsHi = 5, MinRidesLo = 2, MinRidesHi = 3,
            DecoysLo = 0, DecoysHi = 1, ReturnMax = 1, RandomLo = 0.50, RandomHi = 1.0, LongestMax = 10, FansLo = 0, FansHi = 0,
            IslandsLo = 2, IslandsHi = 3, TwinPercent = 25,
        };

        public static readonly WindTargets Mid = new WindTargets
        {
            Tier = PuzzleTier.Mid, RegionsLo = 5, RegionsHi = 8, StreamsLo = 5, StreamsHi = 8, MinRidesLo = 3, MinRidesHi = 5,
            DecoysLo = 1, DecoysHi = 2, ReturnMax = 2, RandomLo = 0.15, RandomHi = 0.50, LongestMax = 14, FansLo = 0, FansHi = 1,
            IslandsLo = 3, IslandsHi = 5, TwinPercent = 35,
        };

        public static readonly WindTargets Late = new WindTargets
        {
            Tier = PuzzleTier.Late, RegionsLo = 7, RegionsHi = 11, StreamsLo = 7, StreamsHi = 11, MinRidesLo = 5, MinRidesHi = 7,
            DecoysLo = 2, DecoysHi = 3, ReturnMax = 3, RandomLo = 0.03, RandomHi = 0.15, LongestMax = 18, FansLo = 1, FansHi = 2,
            IslandsLo = 3, IslandsHi = 5, TwinPercent = 45,
        };

        public static WindTargets For(PuzzleTier tier) => tier == PuzzleTier.Late ? Late : tier == PuzzleTier.Mid ? Mid : Early;

        /// <summary>The bands a measure misses (empty when the screen is inside every target).</summary>
        public List<string> Misses(WindMetrics m)
        {
            var misses = new List<string>();
            string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
            void Band(string what, double v, double lo, double hi)
            {
                if (v < lo - 1e-9 || v > hi + 1e-9) misses.Add($"{what} {F(v)} outside {F(lo)}-{F(hi)}");
            }
            if (!m.Solvable)
            {
                misses.Add("the goal can never be reached");
                return misses;
            }
            Band("regions", m.Regions, RegionsLo, RegionsHi);
            Band("streams", m.Streams, StreamsLo, StreamsHi);
            Band("min rides", m.MinRides, MinRidesLo, MinRidesHi);
            Band("decoy streams", m.Decoys, DecoysLo, DecoysHi);
            Band("return cost", m.ReturnCost, 0, ReturnMax);
            Band("hard traps", m.Traps, 0, 0);
            Band("unreached regions", m.Unreached, 0, 0);
            Band("unused streams", m.UnusedStreams, 0, 0);
            Band("P_rand", m.RandomSolveRate, RandomLo, RandomHi);
            Band("longest ride", m.LongestRide, 1, LongestMax);
            // Fans: not built (FansLo/FansHi), so not checked.
            if (m.Crossing > CrossingShare * m.MinRides + 1e-6)
                misses.Add($"a crossing takes {m.Crossing} rides, over half the reward route's {m.MinRides}");
            return misses;
        }

        /// <summary>How far a measure is from every band (0 inside all of them): what the generator's search drives down.</summary>
        public float Score(WindMetrics m)
        {
            if (!m.Solvable) return 1000f + 5f * m.Unreached + 20f * m.Traps;
            float s = 0f;
            float Off(double v, double lo, double hi) => (float)(v < lo ? lo - v : v > hi ? v - hi : 0);
            s += 3f * Off(m.MinRides, MinRidesLo, MinRidesHi);
            s += 1.5f * Off(m.Regions, RegionsLo, RegionsHi);
            s += 1.5f * Off(m.Streams, StreamsLo, StreamsHi);
            s += 2f * Off(m.Decoys, DecoysLo, DecoysHi);
            s += 3f * Off(m.ReturnCost, 0, ReturnMax);
            s += 20f * m.Traps;
            s += 4f * m.Unreached;
            s += 3f * m.UnusedStreams;
            s += 40f * Off(m.RandomSolveRate, RandomLo, RandomHi);
            s += 1f * Off(m.LongestRide, 1, LongestMax);
            if (m.Crossing > CrossingShare * m.MinRides) s += 3f * (m.Crossing - CrossingShare * m.MinRides);
            return s;
        }
    }
}
