using System;
using System.Collections.Generic;
using System.Globalization;

namespace Game.Cube
{
    /// <summary>
    /// What the solver measures on an ice screen (4.10), from one doorway: the research note's section 1b metrics.
    /// <list type="bullet">
    /// <item>MinSlides: fewest moves from the doorway's landing to the goal pad (the slide from the landing to the camp included).</item>
    /// <item>Stops: the resting places the doorway reaches, the camp and the goal counted once each, the landings left out.</item>
    /// <item>OutDegree: the mean, over those stops, of the distinct other stops one move reaches (falls left out).</item>
    /// <item>Detour: tiles slid on the best route from the camp (fewest moves, then fewest tiles) over the Manhattan
    /// distance from the camp's centre to the goal. *Assumption:* the camp is the entry every doorway shares.</item>
    /// <item>RockDensity: rocks over the ice area's cells (the rectangle less the camp, the landings and their banks).</item>
    /// <item>PitResets: stops whose every move ends in a pit. Traps: stops that reach neither a landing nor a pit.</item>
    /// <item>Grips: grip tiles off the landings and the camp. ShortestSolutions: distinct shortest stop sequences.</item>
    /// <item>RandomSolveRate: P_rand, exactly (PuzzleGraph.RandomSolveRate), capped at 3x MinSlides moves.</item>
    /// <item>RedHerrings: stops within 3 tiles of the goal on its row or column, yet 3 or more moves from it.</item>
    /// <item>RetryCost: moves from the landing up to and including a fall from the deepest stop on the best route that has
    /// a pit beside it (0 with none). LongestSlide: the most tiles one reachable move covers.</item>
    /// <item>Crossing: the most moves from this doorway's landing to another doorway's.</item>
    /// </list>
    /// </summary>
    public sealed class IceMetrics
    {
        public bool Solvable;
        public int MinSlides;
        public int Stops;
        public float OutDegree;
        public float Detour;
        public int AreaCells;
        public int Rocks;
        public float RockDensity;
        public int Pits;
        public int PitResets;
        public int Traps;
        public int Grips;
        public long ShortestSolutions;
        public double RandomSolveRate;
        public int RedHerrings;
        public int RetryCost;
        public int LongestSlide;
        public int Crossing;

        public override string ToString()
        {
            string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
            return Solvable
                ? $"min {MinSlides}, stops {Stops}, out-degree {F(OutDegree)}, detour {F(Detour)}, rocks {Rocks} ({F(RockDensity * 100)}%), " +
                  $"pits {Pits}, pit resets {PitResets}, traps {Traps}, grip {Grips}, shortest {ShortestSolutions}, " +
                  $"P_rand {F(RandomSolveRate * 100)}%, red herrings {RedHerrings}, retry {RetryCost}, longest {LongestSlide}, crossing {Crossing}"
                : $"unsolvable (stops {Stops}, traps {Traps})";
        }
    }

    /// <summary>
    /// The research note's targets for sliding ice per tier (puzzle-difficulty-research.md 1b), as pass/fail bands, with
    /// the ice area each tier lays out. Departures, all marked as assumptions in the 4.10 plan:
    /// the areas are odd-sized so the camp sits on the centre cell (13x7, 17x9, 21x11 for about 12x8, 16x10, 20x12);
    /// early's minimum is 4 slides (not 3) because every crossing between two doorways takes 2 (landing, camp, landing),
    /// and a crossing may cost at most half the reward route.
    /// </summary>
    public sealed class IceTargets
    {
        public PuzzleTier Tier;
        public int Width, Height;
        public int MinSlidesLo, MinSlidesHi;
        public int StopsLo, StopsHi;
        public float OutDegreeLo, OutDegreeHi;
        public float DetourLo, DetourHi;
        public float RockLo, RockHi;
        public int PitsLo, PitsHi;
        public int PitResetMax;
        public int GripLo, GripHi;
        public long ShortestMax;
        public double RandomLo, RandomHi;
        public int HerringLo, HerringHi;
        public int RetryMax;
        public int LongestMax;

        /// <summary>The Manhattan distance the goal keeps from the camp's centre (so the route has somewhere to go).</summary>
        public int GoalDistance;

        /// <summary>A crossing between two doorways may cost at most this share of the reward route (research note section 0).</summary>
        public const float CrossingShare = 0.5f;

        public static readonly IceTargets Early = new IceTargets
        {
            Tier = PuzzleTier.Early, Width = 13, Height = 7, MinSlidesLo = 4, MinSlidesHi = 5, StopsLo = 8, StopsHi = 15,
            OutDegreeLo = 2.0f, OutDegreeHi = 2.6f, DetourLo = 1.2f, DetourHi = 2.0f, RockLo = 0.06f, RockHi = 0.10f,
            PitsLo = 0, PitsHi = 1, PitResetMax = 0, GripLo = 3, GripHi = 5, ShortestMax = long.MaxValue,
            RandomLo = 0.50, RandomHi = 1.0, HerringLo = 0, HerringHi = 0, RetryMax = 3, LongestMax = 12, GoalDistance = 5,
        };

        public static readonly IceTargets Mid = new IceTargets
        {
            Tier = PuzzleTier.Mid, Width = 17, Height = 9, MinSlidesLo = 6, MinSlidesHi = 9, StopsLo = 15, StopsHi = 30,
            OutDegreeLo = 2.2f, OutDegreeHi = 3.0f, DetourLo = 2.0f, DetourHi = 3.5f, RockLo = 0.09f, RockHi = 0.13f,
            PitsLo = 2, PitsHi = 4, PitResetMax = 2, GripLo = 2, GripHi = 3, ShortestMax = 6,
            RandomLo = 0.15, RandomHi = 0.50, HerringLo = 1, HerringHi = 1, RetryMax = 5, LongestMax = 15, GoalDistance = 7,
        };

        public static readonly IceTargets Late = new IceTargets
        {
            Tier = PuzzleTier.Late, Width = 21, Height = 11, MinSlidesLo = 10, MinSlidesHi = 13, StopsLo = 25, StopsHi = 45,
            OutDegreeLo = 2.5f, OutDegreeHi = 3.2f, DetourLo = 3.0f, DetourHi = 5.0f, RockLo = 0.12f, RockHi = 0.16f,
            PitsLo = 4, PitsHi = 7, PitResetMax = 4, GripLo = 0, GripHi = 2, ShortestMax = 3,
            RandomLo = 0.03, RandomHi = 0.15, HerringLo = 1, HerringHi = 2, RetryMax = 7, LongestMax = 18, GoalDistance = 9,
        };

        public static IceTargets For(PuzzleTier tier) => tier == PuzzleTier.Late ? Late : tier == PuzzleTier.Mid ? Mid : Early;

        /// <summary>The bands a measure misses (empty when the screen is inside every target).</summary>
        public List<string> Misses(IceMetrics m)
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
            Band("min slides", m.MinSlides, MinSlidesLo, MinSlidesHi);
            Band("reachable stops", m.Stops, StopsLo, StopsHi);
            Band("mean out-degree", m.OutDegree, OutDegreeLo, OutDegreeHi);
            Band("detour factor", m.Detour, DetourLo, DetourHi);
            Band("rock density", m.RockDensity, RockLo, RockHi);
            Band("pits", m.Pits, PitsLo, PitsHi);
            Band("pit-reset states", m.PitResets, 0, PitResetMax);
            Band("hard traps", m.Traps, 0, 0);
            Band("grip tiles", m.Grips, GripLo, GripHi);
            if (ShortestMax != long.MaxValue) Band("shortest solutions", m.ShortestSolutions, 1, ShortestMax);
            Band("P_rand", m.RandomSolveRate, RandomLo, RandomHi);
            Band("red herrings", m.RedHerrings, HerringLo, HerringHi);
            Band("retry cost", m.RetryCost, 0, RetryMax);
            Band("longest slide", m.LongestSlide, 1, LongestMax);
            if (m.Crossing > CrossingShare * m.MinSlides + 1e-6)
                misses.Add($"a crossing takes {m.Crossing} moves, over half the reward route's {m.MinSlides}");
            return misses;
        }

        /// <summary>How far a measure is from every band (0 inside all of them): what the generator's search drives down.</summary>
        public float Score(IceMetrics m)
        {
            float s = 0f;
            float Off(double v, double lo, double hi) => (float)(v < lo ? lo - v : v > hi ? v - hi : 0);
            s += 3f * Off(m.MinSlides, MinSlidesLo, MinSlidesHi);
            s += 0.5f * Off(m.Stops, StopsLo, StopsHi);
            s += 10f * Off(m.OutDegree, OutDegreeLo, OutDegreeHi);
            s += 5f * Off(m.Detour, DetourLo, DetourHi);
            s += 2f * Off(m.PitResets, 0, PitResetMax);
            s += 20f * m.Traps;
            if (ShortestMax != long.MaxValue && m.ShortestSolutions > ShortestMax)
                s += 2f * (float)Math.Log(m.ShortestSolutions / (double)ShortestMax, 2) + 1f;
            s += 20f * Off(m.RandomSolveRate, RandomLo, RandomHi);
            s += 2f * Off(m.RedHerrings, HerringLo, HerringHi);
            s += 2f * Off(m.RetryCost, 0, RetryMax);
            s += 1f * Off(m.LongestSlide, 1, LongestMax);
            if (m.Crossing > CrossingShare * m.MinSlides) s += 3f * (m.Crossing - CrossingShare * m.MinSlides);
            // Counts the generator sets directly (kept in band, but scored so a hand-made layout reads the same).
            s += 20f * Off(m.RockDensity, RockLo, RockHi);
            s += 2f * Off(m.Pits, PitsLo, PitsHi);
            s += 2f * Off(m.Grips, GripLo, GripHi);
            return s;
        }
    }
}
