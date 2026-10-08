using System;
using System.Collections.Generic;
using System.Text;

namespace Game.Cube
{
    /// <summary>
    /// Each run's die numbers (4.8, CAP-29): Town (the start face) is always 1, and the other five faces get 2-6 shuffled,
    /// on their own RNG stream (RngStreams.DieNumbers), so no other seeded system changes. The face numbered k holds k
    /// numbered fragments (RewardPlacement fills its fragment slots 1..k; Town's single fragment comes from its leader), so a
    /// run holds 21 fragments. Pure: the game (CubeWorld.Dice), the sweep and the tests roll the same numbers.
    /// </summary>
    public sealed class DieNumbers
    {
        public const ulong RngStream = RngStreams.DieNumbers;

        /// <summary>Town's number, every run.</summary>
        public const int TownNumber = 1;

        /// <summary>The highest die number (the most fragments a face holds).</summary>
        public const int Highest = 6;

        /// <summary>Fragments in a whole run: 1 + 2 + ... + 6.</summary>
        public const int FragmentsPerRun = 21;

        private readonly int[] numbers;

        public int Seed { get; }

        private DieNumbers(int seed, int[] numbers)
        {
            Seed = seed;
            this.numbers = numbers;
        }

        /// <summary>A face's die number: how many fragments it holds (1..6).</summary>
        public int Of(FaceId face) => numbers[(int)face];

        /// <summary>The face showing a number (1..6).</summary>
        public FaceId FaceNumbered(int number)
        {
            for (int f = 0; f < numbers.Length; f++)
                if (numbers[f] == number) return (FaceId)f;
            throw new ArgumentOutOfRangeException(nameof(number), number, "Die numbers run 1-6");
        }

        /// <summary>Rolls a run's numbers: the start face (Town) is 1, the other faces 2-6 in a seeded order.</summary>
        public static DieNumbers Roll(CubeModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            return Roll(model.Seed);
        }

        /// <summary>Rolls the numbers of a seed (the start face is always CubeModel.StartFace).</summary>
        public static DieNumbers Roll(int seed)
        {
            var rng = new SeededRng(unchecked((ulong)(uint)seed), RngStream);
            var rest = new List<int> { 2, 3, 4, 5, 6 };
            rng.Shuffle(rest);
            var numbers = new int[CubeSettings.FaceCount];
            int i = 0;
            for (int f = 0; f < CubeSettings.FaceCount; f++)
                numbers[f] = (FaceId)f == CubeModel.StartFace ? TownNumber : rest[i++];
            return new DieNumbers(seed, numbers);
        }

        /// <summary>Compact text ("Front=1 Right=4 ..."), for the determinism fixture.</summary>
        public string Signature()
        {
            var sb = new StringBuilder();
            for (int f = 0; f < numbers.Length; f++)
            {
                if (f > 0) sb.Append(' ');
                sb.Append((FaceId)f).Append('=').Append(numbers[f]);
            }
            return sb.ToString();
        }

        public override string ToString() => Signature();
    }
}
