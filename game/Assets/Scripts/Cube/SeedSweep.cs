using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Reachability check: breadth-first search from the Town start screen over CubeModel.TryStep.
    /// Every screen on an unsealed face must be reachable. Given a module catalog it also lays out the
    /// science faces (CubeLayout) and checks that every built science face has exactly one core-entrance
    /// slot, on a reachable screen. Shared by the tests and the editor menu.
    /// </summary>
    public static class SeedSweep
    {
        public const int DefaultFirstSeed = 1;
        public const int DefaultSeedCount = 50;

        public struct SeedResult
        {
            public int Seed;
            public List<ScreenAddress> Unreachable;

            /// <summary>Core-entrance problems (face and reason); null when no catalog was given.</summary>
            public List<string> CoreProblems;

            public bool CoreChecked => CoreProblems != null;
            public bool Passed => Unreachable.Count == 0 && (CoreProblems == null || CoreProblems.Count == 0);
        }

        /// <summary>Every screen reachable from the start screen.</summary>
        public static HashSet<ScreenAddress> FindReachable(CubeModel model)
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
            return visited;
        }

        /// <summary>Screens on unsealed faces that cannot be reached from the start screen.</summary>
        public static List<ScreenAddress> FindUnreachable(CubeModel model)
        {
            HashSet<ScreenAddress> visited = FindReachable(model);
            var unreachable = new List<ScreenAddress>();
            foreach (ScreenAddress screen in model.AllScreens())
                if (!model.IsSealed(screen.Face) && !visited.Contains(screen))
                    unreachable.Add(screen);
            return unreachable;
        }

        /// <summary>
        /// Problems with the core entrances of a layout: every built science face needs exactly one
        /// active core-entrance slot on a reachable screen; Town and sealed faces must not be laid out.
        /// Empty when all is well.
        /// </summary>
        public static List<string> FindCoreProblems(CubeModel model, CubeLayout layout, IModuleCatalog catalog)
        {
            var problems = new List<string>();
            HashSet<ScreenAddress> reachable = FindReachable(model);
            for (int f = 0; f < CubeSettings.FaceCount; f++)
            {
                var face = (FaceId)f;
                Theme theme = model.ThemeOf(face);
                bool laidOut = layout.TryGetFace(face, out FaceLayout fl);
                if (!CubeLayout.IsLaidOut(model, face))
                {
                    if (laidOut) problems.Add($"{face} ({theme}) should not be laid out");
                    continue;
                }
                if (!laidOut)
                {
                    problems.Add($"{face} ({theme}) has no layout");
                    continue;
                }

                // Count active core entrances over the whole face: the slot is active only on CoreCell.
                int active = 0;
                for (int y = 0; y < model.FaceSize; y++)
                {
                    for (int x = 0; x < model.FaceSize; x++)
                    {
                        var cell = new Vector2Int(x, y);
                        int slots = catalog.CoreSlotCount(theme, fl.ModuleAt(cell));
                        if (cell == fl.CoreCell && fl.CoreSlot >= 0 && fl.CoreSlot < slots) active++;
                    }
                }
                if (active != 1)
                {
                    problems.Add($"{face} ({theme}) has {active} active core entrances");
                    continue;
                }
                var coreScreen = new ScreenAddress(face, fl.CoreCell);
                if (!reachable.Contains(coreScreen))
                    problems.Add($"{face} ({theme}) core entrance on unreachable {coreScreen}");
            }
            return problems;
        }

        /// <summary>
        /// Checks seeds firstSeed .. firstSeed + count - 1. With a catalog, also checks the core
        /// entrances of each seed's science layout.
        /// </summary>
        public static List<SeedResult> Run(int firstSeed = DefaultFirstSeed, int count = DefaultSeedCount,
            int faceSize = CubeSettings.DefaultFaceSize, IModuleCatalog catalog = null)
        {
            var results = new List<SeedResult>(count);
            for (int s = firstSeed; s < firstSeed + count; s++)
            {
                var model = new CubeModel(s, faceSize);
                var result = new SeedResult { Seed = s, Unreachable = FindUnreachable(model) };
                if (catalog != null)
                    result.CoreProblems = FindCoreProblems(model, CubeLayout.Generate(model, catalog), catalog);
                results.Add(result);
            }
            return results;
        }

        /// <summary>Human-readable report naming every failing seed, its unreachable screens and core problems.</summary>
        public static string Describe(IEnumerable<SeedResult> results)
        {
            var failures = new StringBuilder();
            int total = 0, failed = 0;
            bool coreChecked = false;
            foreach (SeedResult r in results)
            {
                total++;
                coreChecked |= r.CoreChecked;
                if (r.Passed) continue;
                failed++;
                if (r.Unreachable.Count > 0)
                    failures.Append("Seed ").Append(r.Seed).Append(": unreachable ")
                        .AppendLine(string.Join(", ", r.Unreachable));
                if (r.CoreProblems != null && r.CoreProblems.Count > 0)
                    failures.Append("Seed ").Append(r.Seed).Append(": core entrance ")
                        .AppendLine(string.Join("; ", r.CoreProblems));
            }
            string what = coreChecked
                ? "fully reachable with a reachable core entrance on every built face"
                : "fully reachable";
            return $"Seed sweep: {total - failed}/{total} seeds {what}, {failed} failing.\n{failures}";
        }
    }
}
