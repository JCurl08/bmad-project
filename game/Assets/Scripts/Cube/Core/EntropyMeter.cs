using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>The two halves of the core arena: warm on the left of the membrane, cool on the right.</summary>
    public enum ArenaSide { Warm = 0, Cool = 1 }

    /// <summary>
    /// The arena's Order ↔ Entropy meter, pure so it can be tested on its own. Each particle has a home side; the meter is
    /// (particles on the wrong side) ÷ (total ÷ 2), clamped to 1. So 0 is fully ordered (every particle home) and a fully
    /// mixed arena (about half of each kind across) reads 1.
    /// </summary>
    public static class EntropyMeter
    {
        /// <summary>The meter value at which the mixing phase is won.</summary>
        public const float DefaultWinThreshold = 0.8f;

        /// <summary>The side of the membrane (at membraneX) a world x lies on. Exactly on the membrane counts as cool.</summary>
        public static ArenaSide SideOf(float x, float membraneX) => x < membraneX ? ArenaSide.Warm : ArenaSide.Cool;

        /// <summary>The meter for a count of particles on the wrong side out of a total. No particles: 0.</summary>
        public static float Compute(int wrong, int total)
        {
            if (total <= 0 || wrong <= 0) return 0f;
            return Mathf.Clamp01(wrong / (total / 2f));
        }

        /// <summary>How many particles are away from home, given each one's x and home side.</summary>
        public static int CountWrong(IReadOnlyList<float> xs, IReadOnlyList<ArenaSide> homes, float membraneX)
        {
            int wrong = 0;
            int count = Mathf.Min(xs.Count, homes.Count);
            for (int i = 0; i < count; i++)
                if (SideOf(xs[i], membraneX) != homes[i]) wrong++;
            return wrong;
        }

        /// <summary>The meter for particles at xs with the given home sides.</summary>
        public static float Compute(IReadOnlyList<float> xs, IReadOnlyList<ArenaSide> homes, float membraneX) =>
            Compute(CountWrong(xs, homes, membraneX), Mathf.Min(xs.Count, homes.Count));

        /// <summary>True when the meter has reached the threshold.</summary>
        public static bool Reached(float meter, float threshold) => meter >= threshold - 1e-5f;

        /// <summary>The fewest particles that must be away from home to reach the threshold.</summary>
        public static int WrongNeeded(int total, float threshold)
        {
            if (total <= 0) return 0;
            for (int wrong = 0; wrong <= total; wrong++)
                if (Reached(Compute(wrong, total), threshold)) return wrong;
            return total + 1;
        }
    }
}
