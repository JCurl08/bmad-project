using System.Collections.Generic;
using System.Text;

namespace Game.Cube
{
    /// <summary>
    /// Reachability check: breadth-first search from the Town start screen over CubeModel.TryStep.
    /// Every screen on an unsealed face must be reachable. Shared by the tests and the editor menu.
    /// </summary>
    public static class SeedSweep
    {
        public const int DefaultFirstSeed = 1;
        public const int DefaultSeedCount = 50;

        public struct SeedResult
        {
            public int Seed;
            public List<ScreenAddress> Unreachable;
            public bool Passed => Unreachable.Count == 0;
        }

        /// <summary>Screens on unsealed faces that cannot be reached from the start screen.</summary>
        public static List<ScreenAddress> FindUnreachable(CubeModel model)
        {
            var visited = new HashSet<ScreenAddress> { model.StartScreen };
            var queue = new Queue<ScreenAddress>();
            queue.Enqueue(model.StartScreen);
            while (queue.Count > 0)
            {
                ScreenAddress current = queue.Dequeue();
                for (int f = 0; f < 4; f++)
                {
                    if (model.TryStep(current, (Facing)f, out ScreenAddress next, out _) && visited.Add(next))
                        queue.Enqueue(next);
                }
            }

            var unreachable = new List<ScreenAddress>();
            foreach (ScreenAddress screen in model.AllScreens())
                if (!model.IsSealed(screen.Face) && !visited.Contains(screen))
                    unreachable.Add(screen);
            return unreachable;
        }

        /// <summary>Checks seeds firstSeed .. firstSeed + count - 1.</summary>
        public static List<SeedResult> Run(int firstSeed = DefaultFirstSeed, int count = DefaultSeedCount,
            int faceSize = CubeSettings.DefaultFaceSize)
        {
            var results = new List<SeedResult>(count);
            for (int s = firstSeed; s < firstSeed + count; s++)
                results.Add(new SeedResult { Seed = s, Unreachable = FindUnreachable(new CubeModel(s, faceSize)) });
            return results;
        }

        /// <summary>Human-readable report naming every failing seed and its unreachable screens.</summary>
        public static string Describe(IEnumerable<SeedResult> results)
        {
            var failures = new StringBuilder();
            int total = 0, failed = 0;
            foreach (SeedResult r in results)
            {
                total++;
                if (r.Passed) continue;
                failed++;
                failures.Append("Seed ").Append(r.Seed).Append(": unreachable ")
                    .AppendLine(string.Join(", ", r.Unreachable));
            }
            return $"Seed sweep: {total - failed}/{total} seeds fully reachable, {failed} failing.\n{failures}";
        }
    }
}
