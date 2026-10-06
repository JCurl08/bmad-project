using System;
using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>
    /// PCG32 (XSH RR) random source. Integer-only, so one seed gives the same sequence on every
    /// platform. Use this for all seeded content; never UnityEngine.Random or System.Random.
    /// </summary>
    public sealed class SeededRng
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private const ulong DefaultStream = 1442695040888963407UL;

        private ulong state;
        private readonly ulong increment;

        public SeededRng(ulong seed, ulong stream = DefaultStream)
        {
            increment = (stream << 1) | 1UL;
            state = 0UL;
            NextUInt();
            state += seed;
            NextUInt();
        }

        public SeededRng(int seed) : this(unchecked((ulong)(uint)seed)) { }

        /// <summary>Next 32-bit value of the PCG32 sequence.</summary>
        public uint NextUInt()
        {
            ulong old = state;
            state = unchecked(old * Multiplier + increment);
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
        }

        /// <summary>Uniform integer in [0, max). Unbiased (rejection sampling).</summary>
        public int NextInt(int max)
        {
            if (max <= 0) throw new ArgumentOutOfRangeException(nameof(max), "max must be positive");
            uint bound = (uint)max;
            uint threshold = unchecked(0u - bound) % bound; // 2^32 mod bound
            while (true)
            {
                uint r = NextUInt();
                if (r >= threshold) return (int)(r % bound);
            }
        }

        /// <summary>In-place Fisher-Yates shuffle.</summary>
        public void Shuffle<T>(IList<T> list)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = NextInt(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
