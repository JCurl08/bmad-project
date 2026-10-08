using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Earth's sliding ice screens (4.10) as pure data: the slide rules on a hand-made layout (stop before a rock, on grip and
    /// the goal, fall into a pit, lanes slide to the camp), the solver's measures, the generator's determinism, every ice
    /// screen of seeds 1-50 solvable from each of its doorways inside its tier's targets with no hard traps, the plan's place
    /// on the face (Earth's puzzle module, no hazard, trial, gate, pickup or population there), the regions DoorwayReach and
    /// the reward check read, and the sweep flagging an unsolvable screen.
    /// </summary>
    public class IceTests
    {
        private const string LibraryPath = "Assets/Modules/ModuleLibrary.asset";
        private const string ItemCatalogPath = "Assets/Items/ItemCatalog.asset";

        private static ModuleLibrary Library()
        {
            var library = AssetDatabase.LoadAssetAtPath<ModuleLibrary>(LibraryPath);
            Assert.IsNotNull(library, $"{LibraryPath} missing");
            return library;
        }

        private static ItemCatalog Catalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogPath);
            Assert.IsNotNull(catalog, $"{ItemCatalogPath} missing");
            return catalog;
        }

        private static int C(int x, int y) => IceLayout.Index(new Vector2Int(x, y));

        /// <summary>
        /// An early screen by hand (top row first): a rock at (6,6), grip at (6,4), a pit at (14,2), the goal at (15,8). The
        /// frame (banks, lanes, camp) comes from the tier whatever the text says.
        /// </summary>
        private static IceLayout HandMade(string goalRow = "...............G.....")
        {
            string e = new string('.', IceLayout.Cols);
            var rows = new List<string>
            {
                e, e, goalRow, e, "......#..............", e, "......:..............", e, "..............o......", e, e,
            };
            return IceLayout.FromRows(PuzzleTier.Early, _ => true, rows);
        }

        [Test]
        public void Slides_StopBeforeRocks_OnGripAndGoal_FallIntoPits_AndLanesSlideToTheCamp()
        {
            IceLayout ice = HandMade();
            Assert.AreEqual(new RectInt(4, 2, 13, 7), ice.Area, "early: 13x7, centred");
            Assert.AreEqual(IceTile.Rock, ice.TileAt(C(6, 6)));
            Assert.AreEqual(IceTile.Bank, ice.TileAt(C(3, 6)), "outside the area: snowbank");
            Assert.AreEqual(IceTile.Landing, ice.TileAt(C(10, 9)));
            Assert.AreEqual(IceTile.Camp, ice.TileAt(C(10, 5)));

            Assert.AreEqual(C(7, 6), ice.Slide(C(10, 6), Facing.West, out int end, out int length),
                "west from the camp's north arm stops before the rock");
            Assert.AreEqual(C(7, 6), end);
            Assert.AreEqual(3, length);
            Assert.AreEqual(C(6, 4), ice.Slide(C(10, 4), Facing.West, out end, out length), "stops on grip");
            Assert.AreEqual(4, length);
            Assert.AreEqual(C(11, 2), ice.Slide(C(11, 5), Facing.South, out end, out _), "stops before the bank");
            Assert.AreEqual(PuzzleGraph.Fail, ice.Slide(C(11, 2), Facing.East, out end, out _), "falls into the pit");
            Assert.AreEqual(C(14, 2), end);
            Assert.AreEqual(C(15, 8), ice.Slide(C(16, 8), Facing.West, out _, out length), "stops on the goal pad");
            Assert.AreEqual(1, length);
            Assert.AreEqual(IceLayout.CampCell, ice.Slide(C(10, 9), Facing.South, out _, out length), "the north lane slides to the camp");
            Assert.AreEqual(ice.Mouth(Facing.North), ice.Slide(C(10, 6), Facing.North, out _, out _), "and the camp back to it");
            Assert.AreEqual(IceLayout.NoMove, ice.Slide(C(10, 10), Facing.South, out _, out _), "a step within a lane is a walk");
            Assert.AreEqual(IceLayout.NoMove, ice.Slide(C(4, 8), Facing.North, out _, out _), "a bank right ahead goes nowhere");
            Assert.AreEqual(IceLayout.NoMove, ice.Slide(C(10, 9), Facing.East, out _, out _), "a lane never leaks sideways");
        }

        [Test]
        public void Measure_OnAHandMadeScreen_GivesTheResearchNotesMetrics()
        {
            IceLayout ice = HandMade();
            foreach (Facing side in ice.Doors())
            {
                IceMetrics m = ice.Measure(side);
                Assert.IsTrue(m.Solvable, $"{side}");
                Assert.AreEqual(3, m.MinSlides, $"{side}: the lane to the camp, north up column 11, east onto the goal");
                Assert.AreEqual(2, m.Crossing, $"{side}: every crossing is lane, camp, lane");
                Assert.AreEqual(0, m.Traps, $"{side}");
                Assert.AreEqual(1, m.Rocks);
                Assert.AreEqual(1, m.Pits);
                Assert.AreEqual(1, m.Grips);
                Assert.Greater(m.RandomSolveRate, 0.0);
                Assert.LessOrEqual(m.RandomSolveRate, 1.0);
                Assert.GreaterOrEqual(m.ShortestSolutions, 1);
            }
            // The goal's regions: the doorways land in 0, the goal pad is 1, joined both ways.
            Assert.AreEqual(0, ice.DoorRegion(Facing.East));
            Assert.AreEqual(1, ice.RegionAt(IceLayout.CellLocal(C(15, 8))));
            Assert.AreEqual(0, ice.RegionAt(Vector2.zero), "the camp");
            CollectionAssert.AreEqual(new[] { 1 }, ice.Joins(0, false).ToArray());
            CollectionAssert.AreEqual(new[] { 0 }, ice.Joins(1, false).ToArray());

            // A goal walled in by rocks: never reached, so region 1 joins nothing.
            string e = new string('.', IceLayout.Cols);
            var rows = new List<string> { e, e, ".............###.....", ".............#G#.....", ".............###.....", e, e, e, e, e, e };
            IceLayout walled = IceLayout.FromRows(PuzzleTier.Early, _ => true, rows);
            Assert.IsFalse(walled.Measure(Facing.South).Solvable);
            Assert.IsEmpty(walled.Joins(0, true).ToArray());
            Assert.IsTrue(walled.Problems("walled").Any(p => p.Contains("never be reached")));
        }

        [Test]
        public void Tiers_FollowTheDieNumber()
        {
            Assert.AreEqual(PuzzleTier.Early, PuzzleTiers.OfDieNumber(2));
            Assert.AreEqual(PuzzleTier.Early, PuzzleTiers.OfDieNumber(3));
            Assert.AreEqual(PuzzleTier.Mid, PuzzleTiers.OfDieNumber(4));
            Assert.AreEqual(PuzzleTier.Mid, PuzzleTiers.OfDieNumber(5));
            Assert.AreEqual(PuzzleTier.Late, PuzzleTiers.OfDieNumber(6));
            for (int seed = 1; seed <= 20; seed++)
            {
                var model = new CubeModel(seed);
                IcePlan ice = IcePlan.Create(model, CubeLayout.Generate(model, Library()));
                Assert.AreEqual(PuzzleTiers.OfDieNumber(DieNumbers.Roll(model).Of(model.FaceOf(Theme.EarthAtmosphere))), ice.Tier, $"seed {seed}");
            }
        }

        [Test]
        public void Generation_IsDeterministic_AndOnItsOwnStream()
        {
            foreach (PuzzleTier tier in PuzzleTiers.All)
            {
                string a = IceLayout.Generate(new SeededRng(99UL, RngStreams.IceScreens), tier, _ => true).Signature();
                string b = IceLayout.Generate(new SeededRng(99UL, RngStreams.IceScreens), tier, _ => true).Signature();
                Assert.AreEqual(a, b, $"{tier}");
                Assert.AreNotEqual(a, IceLayout.Generate(new SeededRng(100UL, RngStreams.IceScreens), tier, _ => true).Signature());
            }
            Assert.AreEqual(RngStreams.IceScreens, IcePlan.RngStream);
            // The plan is the same cold or cached.
            var model = new CubeModel(7);
            CubeLayout layout = CubeLayout.Generate(model, Library());
            string warm = IcePlan.Create(model, layout).Signature();
            IcePlan.ClearCache();
            Assert.AreEqual(warm, IcePlan.Create(model, layout).Signature());
        }

        /// <summary>
        /// The story's acceptance: every ice screen of seeds 1-50 is solvable from each connected doorway inside its tier's
        /// targets (IcePlan.Problems: every metric of the research note, no hard traps, the crossing at most half the reward
        /// route), each tier appears, and the generation time is logged (a cold cache) and kept short enough for a rebuild.
        /// </summary>
        [Test]
        public void EveryIceScreen_Seeds1To50_IsSolvableFromEachDoorway_InsideItsTiersTargets()
        {
            ModuleLibrary library = Library();
            Assert.GreaterOrEqual(library.IslandModuleIndex(Theme.EarthAtmosphere), 0, "Earth has its ice module");
            IcePlan.ClearCache();
            var problems = new List<string>();
            var tiers = new HashSet<PuzzleTier>();
            var stats = new IceStats();
            var times = new List<double>();
            int screens = 0, doorways = 0;
            for (int seed = 1; seed <= 50; seed++)
            {
                var model = new CubeModel(seed);
                CubeLayout layout = CubeLayout.Generate(model, library);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                IcePlan ice = IcePlan.Create(model, layout);
                watch.Stop();
                times.Add(watch.Elapsed.TotalMilliseconds);
                Assert.IsNotNull(ice, $"seed {seed}");
                foreach (string p in ice.Problems(model)) problems.Add($"seed {seed}: {p}");
                foreach (IceScreenSpec s in ice.Screens)
                {
                    screens++;
                    tiers.Add(s.Tier);
                    stats.Add(s);
                    if (s.Layout != null) doorways += s.Layout.Doors().Count;
                }
            }
            Debug.Log($"Ice: {screens} screens, {doorways} doorways solver-checked; generation per run (2 screens, cold) " +
                      $"max {times.Max():0.0} ms, mean {times.Average():0.0} ms.\n{stats.Describe()}");
            Assert.IsEmpty(problems, string.Join("\n", problems.Take(30)));
            Assert.AreEqual(100, screens, "two ice screens a run");
            Assert.AreEqual(3, tiers.Count, "every tier appears");
            Assert.Less(times.Max(), 250.0, "a run's ice screens generate within a rebuild's budget, cold");
        }

        /// <summary>
        /// Generate never returns an unsolved layout (4.10 review): over seeds 1-500 at every tier (generation only), every
        /// layout is inside every target from every doorway; how often the search ran out and the template stood in is logged.
        /// </summary>
        [Test]
        public void Generate_OverSeeds1To500_AtEveryTier_NeverReturnsAnUnsolvedLayout()
        {
            var problems = new List<string>();
            int templates = 0;
            foreach (PuzzleTier tier in PuzzleTiers.All)
                for (int seed = 1; seed <= 500; seed++)
                {
                    IceLayout ice = IceLayout.Generate(new SeededRng(((ulong)(uint)seed << 32) | 0x51u, RngStreams.IceScreens), tier, _ => true);
                    Assert.IsNotNull(ice);
                    if (ice.FromTemplate) templates++;
                    foreach (string p in ice.Problems($"{tier} seed {seed}")) problems.Add(p);
                }
            Debug.Log($"Ice generation over seeds 1-500 x 3 tiers: {templates} template fallbacks.");
            Assert.IsEmpty(problems, string.Join("\n", problems.Take(20)));
        }

        [Test]
        public void Templates_AreInsideEveryTargetFromEveryDoorway()
        {
            foreach (PuzzleTier tier in PuzzleTiers.All)
            {
                IceLayout template = IceLayout.Template(tier, _ => true);
                Assert.IsEmpty(template.Problems($"{tier} template"));
                Assert.IsTrue(template.Joins(0, false).Contains(1) && template.Joins(1, false).Contains(0), $"{tier}: its regions join");
            }
        }

        [Test]
        public void IceScreens_HoldNoHazardTrialGatePickupOrHiddenSpot()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            for (int seed = 1; seed <= 50; seed++)
            {
                var model = new CubeModel(seed);
                CubeLayout layout = CubeLayout.Generate(model, library);
                IcePlan ice = IcePlan.Create(model, layout);
                ItemPlacement items = ItemPlacement.ForRun(model, layout, library, catalog.Themes(), catalog.VariantCounts());
                EarthPlan earth = EarthPlan.Create(model, items, SeedSweep.ModuleLookup(layout, library), StormcallerItem.VariantKinds(catalog));
                CollectionAssert.AreEquivalent(ice.Screens.Select(s => s.Screen).ToList(), earth.IceScreens.ToList(), $"seed {seed}");
                foreach (IceScreenSpec s in ice.Screens)
                {
                    Assert.IsTrue(SeedSweep.ModuleLookup(layout, library)(s.Screen).IsIceScreen, $"seed {seed} {s}");
                    Assert.IsNull(earth.HazardOn(s.Screen), $"seed {seed}: a hazard on {s}");
                    Assert.AreNotEqual(s.Screen, earth.TrialScreen, $"seed {seed}: the trial on {s}");
                    Assert.IsFalse(items.Gates.Any(g => g.Screen == s.Screen) || items.OptionalGates.Any(g => g.Screen == s.Screen) ||
                                   items.Pickups.Any(p => p.Screen == s.Screen), $"seed {seed}: something placed on {s}");
                }
                Assert.IsFalse(earth.Hazards.Any(h => h.Kind == (HazardKind)1), "no snow patches any more");
            }
        }

        [Test]
        public void Rewards_PutAChestOnEveryIceGoal_ReachableThroughTheIceRegions_AndTheSweepFlagsAnUnsolvableScreen()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            var model = new CubeModel(7);
            CubeLayout layout = CubeLayout.Generate(model, library);
            ItemPlacement items = ItemPlacement.ForRun(model, layout, library, catalog.Themes(), catalog.VariantCounts());
            HiddenCurrencyPlacement hidden = HiddenCurrencyPlacement.Generate(model, layout, library);
            IcePlan ice = IcePlan.Create(model, layout);
            List<PuzzleGoal> goals = PuzzleScreens.Goals(ice);
            Assert.AreEqual(2, goals.Count);
            RewardPlacement rewards = RewardPlacement.Generate(model, layout, items, hidden, library, goals);
            for (int i = 0; i < goals.Count; i++)
                Assert.IsTrue(rewards.TryGet(new Roadblock(RoadblockKind.Puzzle, goals[i].Screen, i), out _), $"goal {i} holds a reward");
            MathPlan math = MathPlan.Create(model, items, SeedSweep.ModuleLookup(layout, library));
            System.Func<ScreenAddress, IScreenRegions> regions = PuzzleScreens.RegionsAt(math, ice);
            Assert.IsEmpty(RewardPlacement.FindProblems(model, layout, items, hidden, rewards, library, regions, goals));
            var reach = new DoorwayReach(model, regions);
            Assert.IsEmpty(reach.Problems());
            foreach (PuzzleGoal g in goals) Assert.IsTrue(reach.CanReach(g.Screen, g.Local, false), $"{g.Screen}: the goal pad is reachable");

            // An unsolvable screen in its place: the goal region is never reached, so its reward is flagged.
            string e = new string('.', IceLayout.Cols);
            IceLayout walled = IceLayout.FromRows(PuzzleTier.Early, _ => true,
                new List<string> { e, e, ".............###.....", ".............#G#.....", ".............###.....", e, e, e, e, e, e });
            ScreenAddress broken = goals[0].Screen;
            System.Func<ScreenAddress, IScreenRegions> stuck = s => s == broken ? walled : regions(s);
            var stuckGoals = new List<PuzzleGoal>(goals) { [0] = new PuzzleGoal(broken, IceLayout.CellLocal(walled.Goal)) };
            List<string> problems = RewardPlacement.FindProblems(model, layout, items, hidden, rewards, library, stuck, stuckGoals);
            Assert.IsTrue(problems.Any(p => p.Contains("can never be reached")), string.Join("; ", problems));
            Assert.IsTrue(new DoorwayReach(model, stuck).Problems().Any(p => p.Contains("can never be reached")));
        }

        [Test]
        public void StripRects_LeaveExactlyEachDoorwaysMouthOpen()
        {
            List<(Rect Rect, bool Mouth)> rects = IceScreen.StripRects(_ => true);
            Assert.AreEqual(4, rects.Count(r => r.Mouth));
            foreach ((Rect r, bool mouth) in rects)
            {
                Assert.IsFalse(r.Overlaps(IceLayout.GridRect), "the strip stays off the grid");
                if (mouth) Assert.AreEqual(ScreenEdges.DoorwayHalfWidth * 2f, Mathf.Max(r.width, r.height), 1e-3f, "a mouth is the doorway's width");
            }
            Assert.AreEqual(0, IceScreen.StripRects(_ => false).Count(r => r.Mouth));
        }
    }
}
