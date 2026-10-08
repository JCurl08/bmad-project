using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Earth's wind-stream screens (4.11) as pure data: the ride rules on a hand-made layout (a stream carries the player along
    /// its arrows onto land or a grip anchor, a stream that ends where it set off is no move, the sky and walls are no way on),
    /// the solver's measures, the generator's determinism, every wind screen of seeds 1-50 solvable from each of its doorways
    /// inside its tier's targets with no hard traps, seeds 1-500 at every tier with no template, the screens' place on the face
    /// (Earth's wind module, one a run, beside no ice screen, never the town or the core; no hazard, trial, gate, pickup or
    /// population there), the regions DoorwayReach reads and the sweep flagging an unsolvable screen.
    /// </summary>
    public class WindTests
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

        private static int C(int x, int y) => WindLayout.Index(new Vector2Int(x, y));

        /// <summary>
        /// An early screen by hand (top row first). Island A (4-5, 7-8) with the goal at (5,8); island B (13-14, 4-5) split from
        /// its twin B' (16-17, 4-5) by a wall at (15, 4-5). Streams: ring -> A up column 4; A -> ring up column 5 from (5,9);
        /// A -> the grip anchor at (9,7) east along row 7, and on from it east then down onto B; B -> ring down column 13.
        /// </summary>
        private static readonly string[] Hand =
        {
            "_____________________", // y 12
            "_....^.............._", // y 11
            "_....^.............._", // y 10
            "_....^.............._", // y 9
            "_..._G.............._", // y 8
            "_...__>>>+>>>v......_", // y 7
            "_...^........v......_", // y 6
            "_...^........__#__.._", // y 5
            "_...^........__#__.._", // y 4
            "_...^........v......_", // y 3
            "_...^........v......_", // y 2
            "_...^........v......_", // y 1
            "_____________________", // y 0
        };

        /// <summary>The hand-made screen with the ring's stream up to A taken away: the goal's island is cut off.</summary>
        private static string[] Stranded()
        {
            string[] rows = (string[])Hand.Clone();
            for (int y = 1; y <= 6; y++) rows[12 - y] = rows[12 - y].Remove(4, 1).Insert(4, ".");
            return rows;
        }

        private static WindLayout HandMade() => WindLayout.FromRows(PuzzleTier.Early, _ => true, Hand);

        [Test]
        public void HandMade_IsWhatItSays()
        {
            WindLayout w = HandMade();
            Assert.AreEqual(WindTile.Floor, w.TileAt(C(0, 0)), "the ring");
            Assert.AreEqual(WindTile.Goal, w.TileAt(C(5, 8)));
            Assert.AreEqual(WindTile.Grip, w.TileAt(C(9, 7)));
            Assert.AreEqual(WindTile.Wall, w.TileAt(C(15, 5)));
            Assert.AreEqual(WindTile.Stream, w.TileAt(C(4, 1)));
            Assert.AreEqual(Facing.North, w.DirectionAt(C(4, 1)));
        }

        [Test]
        public void Rides_FollowTheArrows_OntoLandOrGrip_AndNeverBackWhereTheySetOff()
        {
            WindLayout w = HandMade();
            // From the ring at (4,0), north onto the stream: up column 4 onto island A at (4,7).
            Assert.AreEqual(C(4, 7), w.RideFrom(C(4, 0), C(4, 1), out int length));
            Assert.AreEqual(6, length, "six stream tiles");
            CollectionAssert.AreEqual(new[] { C(4, 1), C(4, 2), C(4, 3), C(4, 4), C(4, 5), C(4, 6), C(4, 7) }, w.RidePath(C(4, 1)));
            // From A at (5,7), east along row 7 onto the grip anchor at (9,7), where the ride stops.
            Assert.AreEqual(C(9, 7), w.RideFrom(C(5, 7), C(6, 7), out length));
            Assert.AreEqual(3, length);
            // From the anchor on: east, then down column 13 onto island B at (13,5).
            Assert.AreEqual(C(13, 5), w.RideFrom(C(9, 7), C(10, 7), out _));
            // From B down column 13 to the ring.
            Assert.AreEqual(C(13, 0), w.RideFrom(C(13, 4), C(13, 3), out _));
            // A stream that ends where it set off is no move (B stepping into the stream that lands on it).
            Assert.Less(w.RideFrom(C(13, 5), C(13, 6), out _), 0, "back onto B: no move");
            Assert.Less(w.RideFrom(C(9, 7), C(8, 7), out _), 0, "back onto the anchor: no move");
            Assert.Less(w.RideFrom(C(4, 7), C(3, 7), out _), 0, "the sky is no move");
            Assert.AreEqual(w.NodeOf(C(13, 4)), w.NodeOf(C(14, 5)), "B is one region");
            Assert.AreNotEqual(w.NodeOf(C(14, 5)), w.NodeOf(C(16, 5)), "the wall splits B from its twin");
            Assert.AreEqual(WindLayout.HomeNode, w.NodeOf(C(20, 12)), "the ring is one region, the home");
        }

        [Test]
        public void Measure_OnAHandMadeScreen_GivesTheResearchNotesMetrics()
        {
            WindLayout w = HandMade();
            foreach (Facing side in w.Doors())
            {
                WindMetrics m = w.Measure(side);
                Assert.IsTrue(m.Solvable, $"{side}");
                Assert.AreEqual(1, m.MinRides, $"{side}: the ring rides straight onto the goal's island");
                Assert.AreEqual(0, m.Crossing, "every doorway opens onto the ring");
                Assert.AreEqual(0, m.Traps);
                Assert.AreEqual(5, m.Regions, "ring, A, the anchor, B and its twin");
                Assert.AreEqual(1, m.Unreached, "the twin B' is never reached");
                Assert.AreEqual(4, m.Streams, "ring->A, A->anchor->B (one stream), B->ring, A->ring");
                Assert.AreEqual(1, m.Anchors);
                Assert.AreEqual(2, m.Walls);
                Assert.AreEqual(2, m.ReturnCost, "the anchor rides to B, then home");
                Assert.AreEqual(2, m.Decoys, "into the anchor and on to B: off the one-ride route");
                Assert.AreEqual(6, m.LongestRide);
                // One ride from the ring reaches the goal's island, the only way off the ring: the random player always wins.
                Assert.AreEqual(1.0, m.RandomSolveRate, 1e-9, "P_rand");
                Assert.That(m.RandomSolveRate, Is.InRange(WindTargets.Early.RandomLo, WindTargets.Early.RandomHi), "inside early's band");
            }
            Assert.IsTrue(w.Problems("hand").Any(p => p.Contains("unreached")), "the stranded twin is flagged");
            Assert.AreEqual(0, w.DoorRegion(Facing.East));
            Assert.AreEqual(1, w.RegionAt(WindLayout.CellLocal(C(4, 8))), "the goal's island");
            Assert.AreEqual(0, w.RegionAt(WindLayout.CellLocal(C(13, 4))), "B: reached");
            Assert.AreEqual(-1, w.RegionAt(WindLayout.CellLocal(C(16, 4))), "the twin: never reached");
            Assert.AreEqual(-1, w.RegionAt(WindLayout.CellLocal(C(10, 10))), "the sky");
            CollectionAssert.AreEqual(new[] { 1 }, w.Joins(0, false).ToArray());
            CollectionAssert.AreEqual(new[] { 0 }, w.Joins(1, false).ToArray());
        }

        [Test]
        public void Problems_FlagBrokenStreams_AndAnUnreachableGoal()
        {
            string[] rows = (string[])Hand.Clone();
            rows[12 - 9] = "_....<.............._"; // A's way home now points into the sky
            WindLayout broken = WindLayout.FromRows(PuzzleTier.Early, _ => true, rows);
            Assert.IsTrue(broken.Problems("broken").Any(p => p.Contains("over the sky")), string.Join("; ", broken.Problems("broken")));
            string[] stuck = Stranded();
            WindLayout walled = WindLayout.FromRows(PuzzleTier.Early, _ => true, stuck);
            Assert.IsFalse(walled.Measure(Facing.South).Solvable);
            Assert.IsEmpty(walled.Joins(0, true).ToArray());
            Assert.IsTrue(walled.Problems("walled").Any(p => p.Contains("never be reached")));
        }

        [Test]
        public void Generation_IsDeterministic_AndOnItsOwnStream()
        {
            foreach (PuzzleTier tier in PuzzleTiers.All)
            {
                string a = WindLayout.Generate(new SeededRng(99UL, RngStreams.WindScreens), tier, _ => true).Signature();
                string b = WindLayout.Generate(new SeededRng(99UL, RngStreams.WindScreens), tier, _ => true).Signature();
                Assert.AreEqual(a, b, $"{tier}");
                Assert.AreNotEqual(a, WindLayout.Generate(new SeededRng(100UL, RngStreams.WindScreens), tier, _ => true).Signature());
            }
            Assert.AreEqual(RngStreams.WindScreens, WindPlan.RngStream);
            var model = new CubeModel(7);
            CubeLayout layout = CubeLayout.Generate(model, Library());
            string warm = WindPlan.Create(model, layout).Signature();
            WindPlan.ClearCache();
            Assert.AreEqual(warm, WindPlan.Create(model, layout).Signature());
        }

        /// <summary>
        /// The story's acceptance: every wind screen of seeds 1-50 is solvable from each connected doorway inside its tier's
        /// targets (WindPlan.Problems: every metric of the research note, no hard traps, the crossing at most half the reward
        /// route, sound streams), each tier appears, no template stands in, and the generation is quick enough for a rebuild.
        /// </summary>
        [Test]
        public void EveryWindScreen_Seeds1To50_IsSolvableFromEachDoorway_InsideItsTiersTargets()
        {
            ModuleLibrary library = Library();
            Assert.GreaterOrEqual(library.WindModuleIndex(Theme.EarthAtmosphere), 0, "Earth has its wind module");
            WindPlan.ClearCache();
            var problems = new List<string>();
            var tiers = new HashSet<PuzzleTier>();
            var stats = new WindStats();
            var times = new List<double>();
            int screens = 0, doorways = 0, templates = 0;
            for (int seed = 1; seed <= 50; seed++)
            {
                var model = new CubeModel(seed);
                CubeLayout layout = CubeLayout.Generate(model, library);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                WindPlan wind = WindPlan.Create(model, layout);
                watch.Stop();
                times.Add(watch.Elapsed.TotalMilliseconds);
                Assert.IsNotNull(wind, $"seed {seed}");
                foreach (string p in wind.Problems(model)) problems.Add($"seed {seed}: {p}");
                foreach (WindScreenSpec s in wind.Screens)
                {
                    screens++;
                    tiers.Add(s.Tier);
                    stats.Add(s);
                    if (s.Layout != null) doorways += s.Layout.Doors().Count;
                    if (s.Layout != null && s.Layout.FromTemplate) templates++;
                    if (s.Layout == null) continue;
                    WindTargets t = s.Layout.Targets;
                    foreach (Facing side in s.Layout.Doors())
                    {
                        WindMetrics m = s.Layout.Measure(side);
                        Assert.That(m.RandomSolveRate, Is.InRange(t.RandomLo, t.RandomHi), $"seed {seed} {s} from {side}: P_rand in {s.Tier}'s band");
                        Assert.That(m.MinRides, Is.InRange(t.MinRidesLo, t.MinRidesHi), $"seed {seed} {s} from {side}: min rides");
                        Assert.LessOrEqual(m.ReturnCost, t.ReturnMax, $"seed {seed} {s} from {side}: return cost");
                    }
                    for (int c = 0; c < WindLayout.Cells; c++)
                        if (WindLayout.IsDoorFront(c))
                            Assert.AreNotEqual(WindTile.Stream, s.Layout.TileAt(c), $"seed {seed} {s}: no stream in front of a doorway");
                }
            }
            Debug.Log($"Wind: {screens} screens, {doorways} doorways solver-checked, {templates} templates; generation per run (cold) " +
                      $"max {times.Max():0.0} ms, mean {times.Average():0.0} ms.\n{stats.Describe()}");
            Assert.IsEmpty(problems, string.Join("\n", problems.Take(30)));
            Assert.AreEqual(50, screens, "one wind screen a run");
            Assert.AreEqual(200, doorways, "four doorways each");
            Assert.AreEqual(3, tiers.Count, "every tier appears");
            Assert.AreEqual(0, templates, "no template stands in");
            // Logged above; the bound is generous so a slow machine never flakes it (measured max about 330 ms).
            Assert.Less(times.Max(), 2000.0, "a run's wind screen generates within a rebuild's budget, cold");
        }

        /// <summary>
        /// Generate never returns an unsolved layout, and never needs its template: over seeds 1-500 at every tier (generation
        /// only), every layout is inside every target from every doorway and none is a template.
        /// </summary>
        [Test]
        public void Generate_OverSeeds1To500_AtEveryTier_IsInsideEveryTarget_WithNoTemplateFallback()
        {
            var problems = new List<string>();
            int templates = 0;
            foreach (PuzzleTier tier in PuzzleTiers.All)
                for (int seed = 1; seed <= 500; seed++)
                {
                    WindLayout wind = WindLayout.Generate(new SeededRng(((ulong)(uint)seed << 32) | 0x57u, RngStreams.WindScreens), tier, _ => true);
                    Assert.IsNotNull(wind);
                    if (wind.FromTemplate) templates++;
                    foreach (string p in wind.Problems($"{tier} seed {seed}")) problems.Add(p);
                }
            Debug.Log($"Wind generation over seeds 1-500 x 3 tiers: {templates} template fallbacks.");
            Assert.IsEmpty(problems, string.Join("\n", problems.Take(20)));
            Assert.AreEqual(0, templates, "no template fallback");
        }

        [Test]
        public void Templates_AreInsideEveryTargetFromEveryDoorway()
        {
            foreach (PuzzleTier tier in PuzzleTiers.All)
            {
                WindLayout template = WindLayout.Template(tier, _ => true);
                Assert.IsEmpty(template.Problems($"{tier} template"));
                Assert.IsTrue(template.Joins(0, false).Contains(1) && template.Joins(1, false).Contains(0), $"{tier}: its regions join");
            }
        }

        [Test]
        public void WindScreens_OnePerRun_BesideNoOtherPuzzle_HoldNoHazardTrialGatePickupOrHiddenSpot()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            for (int seed = 1; seed <= 50; seed++)
            {
                var model = new CubeModel(seed);
                CubeLayout layout = CubeLayout.Generate(model, library);
                WindPlan wind = WindPlan.Create(model, layout);
                Assert.AreEqual(1, wind.Screens.Count, $"seed {seed}");
                FaceLayout fl = layout.Faces[wind.Face];
                ItemPlacement items = ItemPlacement.ForRun(model, layout, library, catalog.Themes(), catalog.VariantCounts());
                EarthPlan earth = EarthPlan.Create(model, items, SeedSweep.ModuleLookup(layout, library), StormcallerItem.VariantKinds(catalog));
                CollectionAssert.AreEquivalent(wind.Screens.Select(s => s.Screen).ToList(), earth.WindScreens.ToList(), $"seed {seed}");
                foreach (WindScreenSpec s in wind.Screens)
                {
                    Assert.IsTrue(SeedSweep.ModuleLookup(layout, library)(s.Screen).IsWindScreen, $"seed {seed} {s}");
                    Assert.AreNotEqual(fl.CoreCell, s.Screen.Cell, $"seed {seed}: the core");
                    Assert.IsFalse(TownSquare.IsTown(s.Screen, model.FaceSize), $"seed {seed}: the town");
                    foreach (Vector2Int ice in fl.IslandCells)
                        Assert.AreNotEqual(1, Mathf.Abs(ice.x - s.Screen.Cell.x) + Mathf.Abs(ice.y - s.Screen.Cell.y), $"seed {seed}: beside an ice screen");
                    Assert.IsNull(earth.HazardOn(s.Screen), $"seed {seed}: a hazard on {s}");
                    Assert.AreNotEqual(s.Screen, earth.TrialScreen, $"seed {seed}: the trial on {s}");
                    Assert.IsFalse(items.Gates.Any(g => g.Screen == s.Screen) || items.OptionalGates.Any(g => g.Screen == s.Screen) ||
                                   items.Pickups.Any(p => p.Screen == s.Screen), $"seed {seed}: something placed on {s}");
                }
                Assert.IsFalse(earth.Hazards.Any(h => (int)h.Kind == 0), "no wind bands any more");
            }
        }

        [Test]
        public void Rewards_PutAChestOnTheWindGoal_ReachableThroughItsRegions_AndTheSweepFlagsAnUnsolvableScreen()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            var model = new CubeModel(7);
            CubeLayout layout = CubeLayout.Generate(model, library);
            ItemPlacement items = ItemPlacement.ForRun(model, layout, library, catalog.Themes(), catalog.VariantCounts());
            HiddenCurrencyPlacement hidden = HiddenCurrencyPlacement.Generate(model, layout, library);
            IcePlan ice = IcePlan.Create(model, layout);
            WindPlan wind = WindPlan.Create(model, layout);
            List<PuzzleGoal> goals = PuzzleScreens.Goals(ice, wind);
            Assert.AreEqual(3, goals.Count, "two ice goals, then the wind goal");
            Assert.AreEqual(wind.Screens[0].Screen, goals[2].Screen);
            RewardPlacement rewards = RewardPlacement.Generate(model, layout, items, hidden, library, goals);
            for (int i = 0; i < goals.Count; i++)
                Assert.IsTrue(rewards.TryGet(new Roadblock(RoadblockKind.Puzzle, goals[i].Screen, i), out _), $"goal {i} holds a reward");
            MathPlan math = MathPlan.Create(model, items, SeedSweep.ModuleLookup(layout, library));
            System.Func<ScreenAddress, IScreenRegions> regions = PuzzleScreens.RegionsAt(math, ice, wind);
            Assert.IsEmpty(RewardPlacement.FindProblems(model, layout, items, hidden, rewards, library, regions, goals));
            var reach = new DoorwayReach(model, regions);
            Assert.IsEmpty(reach.Problems());
            Assert.IsTrue(reach.CanReach(goals[2].Screen, goals[2].Local, false), "the wind goal is reachable");

            // An unsolvable screen in its place: its goal is never reached, so its reward is flagged.
            WindLayout walled = WindLayout.FromRows(PuzzleTier.Early, _ => true, Stranded());
            ScreenAddress broken = goals[2].Screen;
            System.Func<ScreenAddress, IScreenRegions> cut = s => s == broken ? walled : regions(s);
            var cutGoals = new List<PuzzleGoal>(goals) { [2] = new PuzzleGoal(broken, WindLayout.CellLocal(walled.Goal)) };
            List<string> problems = RewardPlacement.FindProblems(model, layout, items, hidden, rewards, library, cut, cutGoals);
            Assert.IsTrue(problems.Any(p => p.Contains("can never be reached")), string.Join("; ", problems));
        }

        [Test]
        public void TheGrid_FitsInsideTheEdgeWalls_AndTheStripLeavesEachDoorwaysMouthOpen()
        {
            Rect grid = WindLayout.GridRect;
            Vector2 inside = ScreenEdges.Half - Vector2.one * ScreenEdges.EdgeWallThickness;
            Assert.LessOrEqual(grid.xMax, inside.x);
            Assert.LessOrEqual(grid.yMax, inside.y);
            List<(Rect Rect, bool Mouth)> rects = IceScreen.StripRects(_ => true, grid);
            Assert.AreEqual(4, rects.Count(r => r.Mouth));
            foreach ((Rect r, bool _) in rects) Assert.IsFalse(r.Overlaps(grid), "the strip stays off the grid");
            for (int f = 0; f < 4; f++)
            {
                int door = WindLayout.DoorCell((Facing)f);
                Assert.IsTrue(WindLayout.IsRing(door));
                Assert.AreEqual((Facing)f, WindLayout.DoorwayOf(door));
            }
        }
    }
}
