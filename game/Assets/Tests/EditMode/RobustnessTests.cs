using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Robustness for any seed (4.15): real runs start on clock seeds, not only the swept 1-50, so generation alone (no scene)
    /// is checked over seeds 1-500 with the game's catalogs. Every seed passes the whole seed sweep (layout and core entrances,
    /// items with no softlock, hidden currency, roadblock rewards, the face-edge locks over every order of spending keys, the
    /// lair, Town's trail and errands, the puzzle screens and the temples), no puzzle screen or temple falls back to its
    /// template, the game's own locks (EdgeLocks.ForRun) are the swept ones and never the fallback draw, and the reveal-time
    /// guard (RunPlanCheck, what CubeWorld logs) finds nothing. The raw generators at every tier over seeds 1-500 are
    /// IceTests', WindTests', IslandPuzzleTests' and TempleTests' own.
    /// </summary>
    public class RobustnessTests
    {
        private const string LibraryPath = "Assets/Modules/ModuleLibrary.asset";
        private const string ItemCatalogPath = "Assets/Items/ItemCatalog.asset";
        private const int FirstSeed = 1;
        private const int Seeds = 500;

        /// <summary>About 6 minutes: in the Slow category, which tools/verify.sh runs as its own part (robust).</summary>
        [Test, Timeout(1800000), Category("Slow")]
        public void EverySeed1To500_GeneratesWithNoProblem_NoTemplate_AndTheGamesLocksNeverFallBack()
        {
            var library = AssetDatabase.LoadAssetAtPath<ModuleLibrary>(LibraryPath);
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogPath);
            Assert.IsNotNull(library, $"{LibraryPath} missing");
            Assert.IsNotNull(catalog, $"{ItemCatalogPath} missing");
            List<Theme> themes = catalog.Themes();
            IReadOnlyDictionary<Theme, int> variants = catalog.VariantCounts();

            var clock = Stopwatch.StartNew();
            List<SeedSweep.SeedResult> results = SeedSweep.Run(FirstSeed, Seeds, CubeSettings.DefaultFaceSize, library, themes, variants,
                BiologyBeaks.VariantKinds(catalog), ChemistryIsotope.VariantKinds(catalog), StormcallerItem.VariantKinds(catalog));
            long sweepMs = clock.ElapsedMilliseconds;

            var problems = new StringBuilder();
            int templates = 0, puzzleScreens = 0, temples = 0, lockDraws = 0;
            foreach (SeedSweep.SeedResult r in results)
            {
                string what = $"seed {r.Seed}";
                bool everythingChecked = r.CoreChecked && r.ItemsChecked && r.HiddenChecked && r.RewardsChecked && r.LocksChecked &&
                                         r.IceChecked && r.WindChecked && r.IslandsChecked && r.LairChecked && r.QuestsChecked &&
                                         r.TemplesChecked;
                if (!everythingChecked) problems.AppendLine($"{what}: not every check ran");

                foreach (IceScreenSpec s in r.Ice?.Screens ?? new List<IceScreenSpec>())
                {
                    puzzleScreens++;
                    if (s.Layout == null || s.Layout.FromTemplate) { templates++; problems.AppendLine($"{what}: {s} is its template"); }
                }
                foreach (WindScreenSpec s in r.Wind?.Screens ?? new List<WindScreenSpec>())
                {
                    puzzleScreens++;
                    if (s.Layout == null || s.Layout.FromTemplate) { templates++; problems.AppendLine($"{what}: {s} is its template"); }
                }
                foreach (IslandScreenSpec s in r.Islands?.Screens ?? new List<IslandScreenSpec>())
                {
                    puzzleScreens++;
                    if (s.Layout == null || s.Layout.FromTemplate) { templates++; problems.AppendLine($"{what}: {s} is its template"); }
                }

                var model = new CubeModel(r.Seed);
                foreach (FaceId face in TemplePlan.TempleFaces(model))
                {
                    temples++;
                    TemplePuzzle temple = TemplePlan.For(model, face);
                    if (temple == null || temple.FromTemplate) { templates++; problems.AppendLine($"{what}: {face}'s temple is a template"); }
                }

                // The game's locks (CubeWorld: EdgeLocks.ForRun) are the swept ones, and never the fallback.
                EdgeLocks locks = EdgeLocks.ForRun(model, library, themes, variants);
                if (locks == null || locks.FellBack) problems.AppendLine($"{what}: the game's locks fell back");
                else if (r.Locks == null || locks.Signature() != r.Locks.Signature())
                    problems.AppendLine($"{what}: the game's locks differ from the swept ones");
                else lockDraws = System.Math.Max(lockDraws, locks.Attempts);

                // The reveal-time guard (what CubeWorld.RevealScience logs) on the same plans.
                CubeLayout layout = CubeLayout.Generate(model, library);
                MathPlan math = MathPlan.Create(model, r.Placement, SeedSweep.ModuleLookup(layout, library));
                List<string> guard = RunPlanCheck.Problems(model, layout, library, themes, r.Placement, r.HiddenCurrency, r.Rewards,
                    r.Ice, r.Wind, r.Islands, locks, r.Lair, DieNumbers.Roll(model), math);
                if (guard.Count > 0) problems.AppendLine($"{what}: the reveal guard says {string.Join("; ", guard)}");
            }
            long totalMs = clock.ElapsedMilliseconds;
            Debug.Log($"Robustness, seeds {FirstSeed}-{FirstSeed + Seeds - 1}: sweep {sweepMs} ms, all checks {totalMs} ms; " +
                      $"{puzzleScreens} puzzle screens and {temples} temples, {templates} templates; most lock draws {lockDraws}.");

            string report = SeedSweep.Describe(results);
            Assert.AreEqual(Seeds, results.Count);
            Assert.IsTrue(results.All(r => r.Passed), report);
            StringAssert.Contains($"{Seeds}/{Seeds} seeds", report);
            Assert.AreEqual(0, templates, problems.ToString());
            Assert.IsEmpty(problems.ToString(), problems.ToString());
        }

        [Test]
        public void TheRevealGuard_IsSilentOnSoundRuns_AndItsColdCostIsMeasured()
        {
            // What the guard costs at a reveal: the run's plans are made already, the temples not yet (their cache is cold).
            var library = AssetDatabase.LoadAssetAtPath<ModuleLibrary>(LibraryPath);
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogPath);
            Assert.IsNotNull(library, $"{LibraryPath} missing");
            Assert.IsNotNull(catalog, $"{ItemCatalogPath} missing");
            List<Theme> themes = catalog.Themes();
            const int count = 10;
            long total = 0, most = 0;
            for (int seed = 1; seed <= count; seed++)
            {
                SeedSweep.SeedResult r = SeedSweep.Run(seed, 1, CubeSettings.DefaultFaceSize, library, themes, catalog.VariantCounts(),
                    BiologyBeaks.VariantKinds(catalog), ChemistryIsotope.VariantKinds(catalog), StormcallerItem.VariantKinds(catalog))[0];
                var model = new CubeModel(seed);
                CubeLayout layout = CubeLayout.Generate(model, library);
                MathPlan math = MathPlan.Create(model, r.Placement, SeedSweep.ModuleLookup(layout, library));
                TemplePlan.ClearCache();
                var clock = Stopwatch.StartNew();
                List<string> problems = RunPlanCheck.Problems(model, layout, library, themes, r.Placement, r.HiddenCurrency, r.Rewards,
                    r.Ice, r.Wind, r.Islands, r.Locks, r.Lair, DieNumbers.Roll(model), math);
                long ms = clock.ElapsedMilliseconds;
                total += ms;
                most = System.Math.Max(most, ms);
                Assert.IsEmpty(problems, $"seed {seed}: {string.Join("; ", problems)}");
            }
            Debug.Log($"Reveal guard, seeds 1-{count}, temples cold: mean {total / count} ms, most {most} ms.");
        }

        [Test]
        public void TheRevealGuard_NamesAPlansProblems()
        {
            // A plan with a problem is named: a locks draw that fell back.
            var model = new CubeModel(3);
            var library = AssetDatabase.LoadAssetAtPath<ModuleLibrary>(LibraryPath);
            Assert.IsNotNull(library, $"{LibraryPath} missing");
            CubeLayout layout = CubeLayout.Generate(model, library);
            var fellBack = new EdgeLocks(model.Seed, new EdgeLock[0], EdgeLocks.MaxAttempts + 1);
            List<string> problems = RunPlanCheck.Problems(model, layout, library, null, null, null, null,
                IcePlan.Create(model, layout), WindPlan.Create(model, layout), IslandPlan.Create(model, layout), fellBack,
                LairPlan.ForRun(model));
            Assert.IsTrue(problems.Any(p => p.Contains("fell back")), string.Join("; ", problems));
            // And a sound run names nothing.
            List<string> sound = RunPlanCheck.Problems(model, layout, library, null, null, null, null,
                IcePlan.Create(model, layout), WindPlan.Create(model, layout), IslandPlan.Create(model, layout), null,
                LairPlan.ForRun(model));
            Assert.IsEmpty(sound, string.Join("; ", sound));
        }
    }
}
