using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Math's "light every plank" island screens (4.12) as pure data: the rule (a crossed plank lights, a lit one crossed
    /// again snuffs all, the last one solves it for good, leaving resets an unsolved screen, spans and lookouts light
    /// nothing), the walker that turns steps into crossings, the solver (connectivity, parity, a clean start from each
    /// doorway, the exhaustive trail count and P_rand) on hand-made networks, the tiers and their targets, every island
    /// screen of seeds 1-50 lit from each connected doorway inside its tier's targets, the generator over seeds 1-500 at
    /// every tier with no problem and no template, the templates, determinism on its own stream, and the reward roadblock
    /// reachable only when the screen can be solved.
    /// </summary>
    public class IslandPuzzleTests
    {
        private const string LibraryPath = "Assets/Modules/ModuleLibrary.asset";
        private const string ItemCatalogPath = "Assets/Items/ItemCatalog.asset";
        private static readonly Facing[] Sides = { Facing.North, Facing.East, Facing.South, Facing.West };

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

        private static readonly Vector2Int N = IslandLayout.DoorSpot(Facing.North), E = IslandLayout.DoorSpot(Facing.East),
            S = IslandLayout.DoorSpot(Facing.South), W = IslandLayout.DoorSpot(Facing.West), C = new Vector2Int(2, 1),
            EastOfC = new Vector2Int(3, 1), SouthWest = new Vector2Int(0, 0), WestOfC = new Vector2Int(1, 1);

        /// <summary>
        /// By hand: N-C, C-S, S-SW (a long plank) and SW-W whole, the lit network round the reward island C; E over a broken
        /// span to the island east of C (the gap doorway), with a lookout plank from E's island down to (4,0).
        /// </summary>
        private static IslandLayout HandMade() =>
            new IslandLayout(new[] { N, E, S, W, C, EastOfC, SouthWest, new Vector2Int(4, 0) },
                new[] { (0, 4, false), (4, 2, false), (2, 6, false), (6, 3, false), (1, 5, false), (5, 4, true), (1, 7, false) },
                new[] { 0, 1, 2, 3 }, -1, 4, PuzzleTier.Early);

        private static int Plank(IslandLayout layout, int a, int b) => layout.Bridges.ToList().FindIndex(x => x.Joins(a) && x.Other(a) == b);

        // ---------- The rule ----------

        [Test]
        public void TheRule_ACrossedPlankLights_ALitOneCrossedAgainSnuffsAll_TheLastSolvesForGood_AndLeavingResetsAnUnsolvedScreen()
        {
            IslandLayout layout = HandMade();
            Assert.IsEmpty(layout.Problems("hand-made", _ => true).Where(p => !p.Contains("from the")), "structurally sound");
            int nc = Plank(layout, 0, 4), cs = Plank(layout, 4, 2), ssw = Plank(layout, 2, 6), sww = Plank(layout, 6, 3);
            int span = Plank(layout, 5, 4), lookout = Plank(layout, 1, 7), ce = Plank(layout, 1, 5);
            CollectionAssert.AreEquivalent(new[] { nc, cs, ssw, sww }, layout.NetworkPlanks, "the reward island's whole-plank component");
            Assert.IsFalse(layout.IsNetworkPlank(span) || layout.IsNetworkPlank(lookout) || layout.IsNetworkPlank(ce), "off the network");

            var lights = new PlankLights(layout);
            Assert.AreEqual(4, lights.NetworkCount);
            Assert.AreEqual(CrossResult.Lit, lights.Cross(nc));
            Assert.AreEqual(PlankLight.Lit, lights[nc]);
            Assert.IsTrue(lights.IslandComplete(0), "N's only plank is lit");
            Assert.IsFalse(lights.IslandComplete(4), "C still has a dark plank");
            Assert.AreEqual(CrossResult.Lit, lights.Cross(cs));
            Assert.IsTrue(lights.IslandComplete(4));
            Assert.AreEqual(2, lights.LitCount);
            // Spans, lookouts and planks off the network light nothing and snuff nothing.
            Assert.AreEqual(CrossResult.None, lights.Cross(span));
            Assert.AreEqual(CrossResult.None, lights.Cross(lookout));
            Assert.AreEqual(2, lights.LitCount);
            // A lit plank crossed again: every plank goes dark, that one too.
            Assert.AreEqual(CrossResult.Snuffed, lights.Cross(cs));
            Assert.AreEqual(0, lights.LitCount);
            Assert.IsTrue(layout.NetworkPlanks.All(k => lights[k] == PlankLight.Unlit));
            Assert.IsFalse(lights.IslandComplete(0));
            // Leaving resets an unsolved screen.
            Assert.AreEqual(CrossResult.Lit, lights.Cross(nc));
            Assert.IsTrue(lights.Reset());
            Assert.AreEqual(0, lights.LitCount);
            Assert.IsFalse(lights.Reset(), "nothing lit, nothing to reset");
            // A trail from N: N-C, C-S, S-SW, SW-W solves it, for good.
            Assert.AreEqual(CrossResult.Lit, lights.Cross(nc));
            Assert.AreEqual(CrossResult.Lit, lights.Cross(cs));
            Assert.AreEqual(CrossResult.Lit, lights.Cross(ssw));
            Assert.AreEqual(CrossResult.Solved, lights.Cross(sww));
            Assert.IsTrue(lights.Solved);
            Assert.AreEqual(CrossResult.None, lights.Cross(sww), "solved: a crossing changes nothing");
            Assert.IsFalse(lights.Reset(), "a solved screen stays solved when left");
            Assert.IsTrue(layout.NetworkPlanks.All(k => lights[k] == PlankLight.Lit));
            // Mending: spans only, once.
            Assert.IsTrue(lights.Mend(span));
            Assert.AreEqual(PlankLight.Mended, lights[span]);
            Assert.IsFalse(lights.Mend(span));
            Assert.IsFalse(lights.Mend(nc), "a whole plank is never mended");
            Assert.AreNotEqual(IslandScreen.GlowFor(PlankLight.Lit), IslandScreen.GlowFor(PlankLight.Mended), "lit and mended differ");
            Assert.AreEqual(0f, IslandScreen.GlowFor(PlankLight.Unlit).a, "unlit has no glow");
        }

        [Test]
        public void TheWalker_CountsACrossingOnlyFromOneIslandOverAPlankOntoTheOther()
        {
            IslandLayout layout = HandMade();
            var walker = new PlankWalker(layout);
            int nc = Plank(layout, 0, 4), cs = Plank(layout, 4, 2);
            Vector2 north = layout.Islands[0].center, middle = layout.Islands[4].center, south = layout.Islands[2].center;
            Vector2 onNc = layout.Bridges[nc].Plank.center, onCs = layout.Bridges[cs].Plank.center;
            Assert.AreEqual(-1, walker.Step(onNc, out _, out _), "starting on a plank: nothing");
            Assert.AreEqual(-1, walker.Step(middle, out _, out _), "arriving from a plank it never stepped onto from an island: nothing");
            Assert.AreEqual(-1, walker.Step(onNc, out _, out _));
            Assert.AreEqual(-1, walker.Step(middle, out _, out _), "onto a plank and back: no crossing");
            Assert.AreEqual(-1, walker.Step(onNc, out _, out _));
            Assert.AreEqual(nc, walker.Step(north, out int from, out int to), "C over the plank onto N: a crossing");
            Assert.AreEqual((4, 0), (from, to));
            Assert.AreEqual(-1, walker.Step(north + Vector2.right * 0.1f, out _, out _), "walking about on N: nothing");
            Assert.AreEqual(-1, walker.Step(onNc, out _, out _));
            Assert.AreEqual(nc, walker.Step(middle, out _, out _), "and back: a crossing again");
            Assert.AreEqual(-1, walker.Step(onCs, out _, out _));
            walker.Leave();
            Assert.AreEqual(-1, walker.Step(south, out _, out _), "after leaving the screen the walk starts afresh");
            Assert.AreEqual(2, walker.LastIsland);
            // The water and the doorway's mouth (outside the water area) are no crossing either.
            Assert.AreEqual(-1, walker.Step(IslandLayout.WaterArea.min - Vector2.one, out _, out _));

            // A frame that skips the whole plank (one sample on C, the next on N) still crosses it (4.12 review): the way
            // between the samples is walked too.
            walker.Leave();
            Assert.AreEqual(-1, walker.Step(middle, out _, out _));
            Assert.AreEqual(nc, walker.Step(north, out from, out to), "C to N in one sample: over the plank between");
            Assert.AreEqual((4, 0), (from, to));
            Assert.AreEqual(1, walker.Crossings.Count);
            // Two planks in one long jump: both crossings are listed (N over C onto S).
            Assert.AreEqual(cs, walker.Step(south, out _, out _));
            CollectionAssert.AreEqual(new[] { nc, cs }, walker.Crossings.Select(c => c.Plank).ToList());
            // A teleport (Leave) between two samples is never a crossing, even onto the plank's far island.
            Assert.AreEqual(-1, walker.Step(onCs, out _, out _));
            walker.Leave();
            Assert.AreEqual(-1, walker.Step(middle, out _, out _), "put on C by a teleport from the plank: no crossing");
        }

        [Test]
        public void Problems_WantEveryDoorwayButTheGapOneOnTheLitNetwork()
        {
            Assert.IsFalse(HandMade().PuzzleProblems("hand-made").Any(p => p.Contains("off the lit network")));
            // W cut off from the network (and so alone): two doorways off it.
            var cut = new IslandLayout(new[] { N, E, S, W, C, EastOfC, SouthWest, new Vector2Int(4, 0) },
                new[] { (0, 4, false), (4, 2, false), (2, 6, false), (1, 5, false), (5, 4, true), (1, 7, false) },
                new[] { 0, 1, 2, 3 }, -1, 4, PuzzleTier.Early);
            Assert.IsTrue(cut.Problems("cut", _ => true).Any(p => p.Contains("2 doorways off the lit network")),
                string.Join("; ", cut.Problems("cut", _ => true)));
        }

        // ---------- The solver ----------

        private static IslandDesign Design(int[] spots, (int, int, bool)[] links, int reward, params (int Side, int Island)[] doors)
        {
            var d = new IslandDesign { Reward = reward };
            d.Spots.AddRange(spots);
            d.Links.AddRange(links);
            foreach ((int side, int island) in doors) d.DoorIslands[side] = island;
            return d;
        }

        [Test]
        public void TheSolver_ChecksConnectivityParityAndAStartFromEachDoorway_AndCountsEveryTrailExhaustively()
        {
            int Sp(int x, int y) => IslandLattice.Spot(x, y);
            // A path A-B-C: odd A and C (the starts), P_rand 2/3 (from B a random player is stuck after one plank), 2 trails.
            var path = new IslandSolver(Design(new[] { Sp(1, 0), Sp(1, 1), Sp(1, 2) }, new[] { (0, 1, false), (1, 2, false) }, 1, (0, 2)));
            CollectionAssert.AreEquivalent(new[] { 0, 2 }, path.Starts);
            Assert.AreEqual(2.0 / 3.0, path.RandomSolveRate(), 1e-9);
            Assert.AreEqual(2, path.TrailCount());
            IslandMetrics m = path.Measure(0, _ => true);
            Assert.IsTrue(m.Connected && m.Solvable, "the north doorway stands on a start (an odd island)");
            Assert.AreEqual(0, m.StartWalk);
            // From B the starts have one plank each (no clean start: stepping out and back leaves the player on B), so a
            // doorway landing on B cannot start a trail.
            var fromMiddle = new IslandSolver(Design(new[] { Sp(1, 0), Sp(1, 1), Sp(1, 2) }, new[] { (0, 1, false), (1, 2, false) }, 1, (1, 1)));
            Assert.IsFalse(fromMiddle.Measure(1, _ => true).Solvable);

            // A 2x2 loop: no odd island, every island a start, P_rand 1, 4 starts x 2 directions = 8 trails.
            var loop = new IslandSolver(Design(new[] { Sp(1, 0), Sp(2, 0), Sp(1, 1), Sp(2, 1) },
                new[] { (0, 1, false), (0, 2, false), (1, 3, false), (2, 3, false) }, 0, (2, 1)));
            Assert.AreEqual(0, loop.Odd.Count);
            Assert.AreEqual(4, loop.Starts.Count);
            Assert.AreEqual(1.0, loop.RandomSolveRate(), 1e-9);
            Assert.AreEqual(8, loop.TrailCount());
            Assert.IsTrue(loop.Measure(2, _ => true).Solvable);

            // A star: four odd islands, no trail at all.
            var star = new IslandSolver(Design(new[] { Sp(2, 1), Sp(1, 1), Sp(3, 1), Sp(2, 0), Sp(2, 2) },
                new[] { (0, 1, false), (0, 2, false), (0, 3, false), (0, 4, false) }, 0, (2, 3)));
            Assert.AreEqual(4, star.Odd.Count);
            Assert.IsEmpty(star.Starts);
            Assert.AreEqual(0, star.TrailCount());
            Assert.IsFalse(star.Measure(2, _ => true).Solvable);

            // A doorway whose island no plank or span joins to the network: not connected.
            var apart = new IslandSolver(Design(new[] { Sp(1, 0), Sp(1, 1), Sp(4, 1) }, new[] { (0, 1, false) }, 0, (1, 2)));
            Assert.IsFalse(apart.Measure(1, _ => true).Connected);

            // The hand-made screen: the gap doorway (E) reaches C over the span; the network is a path N..W whose starts (N and
            // W) are dead ends, so the north and west doorways stand on a start but from the east no clean start can be made:
            // the screen is not solvable from every doorway, and its roadblock never joins.
            IslandLayout hand = HandMade();
            IslandMetrics east = hand.Measure(Facing.East);
            Assert.IsTrue(east.Connected, east.ToString());
            Assert.IsFalse(east.Solvable, "from C, stepping out to N or W and back lands on C again");
            Assert.AreEqual(4, east.Entry, "over the span onto C");
            Assert.AreEqual(2, east.RedHerrings, "the gap doorway's own plank and its lookout are off the network");
            Assert.AreEqual(0, east.BrokenInNetwork);
            IslandMetrics north = hand.Measure(Facing.North);
            Assert.IsTrue(north.Solvable);
            Assert.AreEqual(0, north.StartWalk, "N is a start itself");
            Assert.IsTrue(hand.Measure(Facing.West).Solvable);
            Assert.IsFalse(hand.Solvable, "every connected doorway must be able to solve it");
            Assert.IsFalse(hand.Joins(hand.RewardIsland, true).Contains(hand.GoalRegion));
            // A trail the solver gives lights everything under the rule.
            List<int> trail = hand.Solver.Trail(hand.DoorIsland(Facing.North));
            Assert.AreEqual(4, trail.Count);
            var lights = new PlankLights(hand);
            foreach (int k in trail) lights.Cross(k);
            Assert.IsTrue(lights.Solved);
        }

        // ---------- Tiers and targets ----------

        [Test]
        public void Tiers_FollowMathsDieNumber_AndTheTargetsFollowTheResearchNote()
        {
            ModuleLibrary library = Library();
            for (int seed = 1; seed <= 20; seed++)
            {
                var model = new CubeModel(seed);
                IslandPlan plan = IslandPlan.Create(model, CubeLayout.Generate(model, library));
                Assert.AreEqual(PuzzleTiers.OfDieNumber(DieNumbers.Roll(model).Of(model.FaceOf(Theme.Math))), plan.Tier, $"seed {seed}");
                foreach (IslandScreenSpec s in plan.Screens) Assert.AreEqual(plan.Tier, s.Layout.Tier, $"seed {seed} {s}");
            }
            // The research note's planks, odd islands and P_rand per tier.
            Assert.AreEqual((5, 7), (IslandTargets.Early.MinPlanks, IslandTargets.Early.MaxPlanks));
            Assert.AreEqual((8, 11), (IslandTargets.Mid.MinPlanks, IslandTargets.Mid.MaxPlanks));
            Assert.AreEqual((12, 16), (IslandTargets.Late.MinPlanks, IslandTargets.Late.MaxPlanks));
            Assert.AreEqual(0, IslandTargets.Early.OddIslands);
            Assert.AreEqual((2, 1), (IslandTargets.Mid.OddIslands, IslandTargets.Mid.OddDoorIslands));
            Assert.AreEqual((2, 0), (IslandTargets.Late.OddIslands, IslandTargets.Late.OddDoorIslands));
            Assert.AreEqual(0.40, IslandTargets.Early.MinRandom);
            Assert.AreEqual((0.10, 0.40), (IslandTargets.Mid.MinRandom, IslandTargets.Mid.MaxRandom));
            Assert.AreEqual((0.02, 0.10), (IslandTargets.Late.MinRandom, IslandTargets.Late.MaxRandom));
            Assert.AreEqual(16, IslandTargets.MaxExhaustivePlanks);
            Assert.AreEqual(28UL, IslandPlan.RngStream);
        }

        /// <summary>
        /// The story's acceptance: every island screen of seeds 1-50 is lit from each connected doorway inside its tier's
        /// targets (IslandPlan.Problems: a clean start reachable, the tier's planks, parity and P_rand, the crossing at most
        /// half the reward route), no template, every tier seen; the generation time is logged (a cold cache).
        /// </summary>
        [Test]
        public void EveryIslandScreen_Seeds1To50_IsLitFromEachConnectedDoorway_InsideItsTiersTargets()
        {
            ModuleLibrary library = Library();
            Assert.GreaterOrEqual(library.IslandModuleIndex(Theme.Math), 0, "Math has its island module");
            IslandPlan.ClearCache();
            var problems = new List<string>();
            var tiers = new HashSet<PuzzleTier>();
            var stats = new IslandStats();
            var times = new List<double>();
            int screens = 0, doorways = 0;
            for (int seed = 1; seed <= 50; seed++)
            {
                var model = new CubeModel(seed);
                CubeLayout layout = CubeLayout.Generate(model, library);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                IslandPlan plan = IslandPlan.Create(model, layout);
                watch.Stop();
                times.Add(watch.Elapsed.TotalMilliseconds);
                Assert.IsNotNull(plan, $"seed {seed}");
                foreach (string p in plan.Problems(model)) problems.Add($"seed {seed}: {p}");
                foreach (IslandScreenSpec s in plan.Screens)
                {
                    screens++;
                    stats.Add(s);
                    if (s.Layout == null) continue;
                    tiers.Add(s.Layout.Tier);
                    if (s.Layout.FromTemplate) problems.Add($"seed {seed}: {s} from the template");
                    foreach (Facing side in Sides)
                    {
                        if (s.Layout.DoorIsland(side) < 0) continue;
                        IslandMetrics m = s.Layout.Measure(side);
                        if (!m.Connected) continue;
                        doorways++;
                        if (!m.Solvable) problems.Add($"seed {seed}: {s} cannot be lit from its {side} doorway");
                    }
                }
            }
            Debug.Log($"Islands: {screens} screens, {doorways} doorways solver-checked; generation per run (2 screens, cold) " +
                      $"max {times.Max():0.0} ms, mean {times.Average():0.0} ms.\n{stats.Describe()}");
            Assert.IsEmpty(problems, string.Join("\n", problems.Take(30)));
            Assert.AreEqual(100, screens, "two island screens a run");
            Assert.AreEqual(400, doorways, "every doorway joins the network (one over its span)");
            Assert.AreEqual(3, tiers.Count, "every tier appears");
            Assert.Less(times.Max(), 3000.0, "a run's island screens generate within a rebuild's budget, cold");
        }

        /// <summary>Generate never returns an unsolved layout: seeds 1-500 at every tier, 0 problems and 0 template fallbacks.</summary>
        [Test]
        public void Generate_OverSeeds1To500_AtEveryTier_HasNoProblem_AndNeverFallsBackToTheTemplate()
        {
            var problems = new List<string>();
            int templates = 0;
            long steps = 0;
            foreach (PuzzleTier tier in PuzzleTiers.All)
                for (int seed = 1; seed <= 500; seed++)
                {
                    IslandLayout layout = IslandLayout.Generate(new SeededRng(((ulong)(uint)seed << 32) | 0x412u, RngStreams.IslandPuzzles),
                        tier, _ => true, seed % 2 == 0);
                    Assert.IsNotNull(layout, $"{tier} seed {seed}");
                    steps += layout.SearchSteps;
                    if (layout.FromTemplate) templates++;
                    foreach (string p in layout.Problems($"{tier} seed {seed}", _ => true)) problems.Add(p);
                    if (!layout.HasPuzzle) problems.Add($"{tier} seed {seed}: no puzzle");
                }
            Debug.Log($"Island generation over seeds 1-500 x 3 tiers: {templates} template fallbacks, {steps / 1500} search steps on average.");
            Assert.IsEmpty(problems, string.Join("\n", problems.Take(20)));
            Assert.AreEqual(0, templates, "no template fallbacks");
        }

        [Test]
        public void Templates_AreInsideEveryTargetFromEveryDoorway_WithAndWithoutTheTrial()
        {
            foreach (PuzzleTier tier in PuzzleTiers.All)
                foreach (bool trial in new[] { true, false })
                {
                    IslandLayout template = IslandLayout.Template(tier, _ => true, trial);
                    Assert.IsNotNull(template);
                    Assert.AreEqual(trial, template.HasTrial);
                    Assert.IsEmpty(template.Problems($"{tier} template", _ => true));
                    Assert.IsTrue(template.Solvable, $"{tier}");
                }
            Assert.IsNull(IslandLayout.Template(PuzzleTier.Mid, d => d != Facing.West, false), "four doorways only");
        }

        [Test]
        public void Generation_IsDeterministic_OnItsOwnStream_AndThePlanIsTheSameColdOrCached()
        {
            foreach (PuzzleTier tier in PuzzleTiers.All)
            {
                string a = IslandLayout.Generate(new SeededRng(99UL, RngStreams.IslandPuzzles), tier, _ => true, true).Signature();
                string b = IslandLayout.Generate(new SeededRng(99UL, RngStreams.IslandPuzzles), tier, _ => true, true).Signature();
                Assert.AreEqual(a, b, $"{tier}");
                Assert.AreNotEqual(a, IslandLayout.Generate(new SeededRng(100UL, RngStreams.IslandPuzzles), tier, _ => true, true).Signature());
            }
            var model = new CubeModel(7);
            CubeLayout layout = CubeLayout.Generate(model, Library());
            string warm = IslandPlan.Create(model, layout).Signature();
            IslandPlan.ClearCache();
            Assert.AreEqual(warm, IslandPlan.Create(model, layout).Signature());
            // MathPlan reads the same layouts, and the trial stays on the screen MathPlan's stream picks.
            ItemCatalog catalog = Catalog();
            ItemPlacement items = ItemPlacement.ForRun(model, layout, Library(), catalog.Themes(), catalog.VariantCounts());
            MathPlan math = MathPlan.Create(model, items, SeedSweep.ModuleLookup(layout, Library()));
            IslandPlan islands = IslandPlan.Create(model, layout);
            foreach (IslandScreenSpec s in islands.Screens) Assert.AreSame(s.Layout, math.IslandAt(s.Screen), $"{s}");
            Assert.AreEqual(islands.Screens.First(s => s.HasTrial).Screen, math.TrialScreen);
        }

        // ---------- The reward roadblock ----------

        /// <summary>
        /// The roadblock opens only when the goal is met: its region joins the reward island only while every doorway can
        /// light the network. On the run, every island goal holds a reward the sweep reaches; an unsolvable screen in its
        /// place (four odd islands) leaves its reward unreachable, and the sweep flags it.
        /// </summary>
        [Test]
        public void Rewards_PutAChestOnEveryIslandRoadblock_ReachableOnlyThroughASolvableNetwork()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            var model = new CubeModel(7);
            CubeLayout layout = CubeLayout.Generate(model, library);
            ItemPlacement items = ItemPlacement.ForRun(model, layout, library, catalog.Themes(), catalog.VariantCounts());
            HiddenCurrencyPlacement hidden = HiddenCurrencyPlacement.Generate(model, layout, library);
            IcePlan ice = IcePlan.Create(model, layout);
            WindPlan wind = WindPlan.Create(model, layout);
            IslandPlan islands = IslandPlan.Create(model, layout);
            List<PuzzleGoal> goals = PuzzleScreens.Goals(ice, wind, islands);
            int first = goals.Count - islands.Screens.Count;
            Assert.AreEqual(5, goals.Count, "2 ice, 1 wind, then 2 islands");
            RewardPlacement rewards = RewardPlacement.Generate(model, layout, items, hidden, library, goals);
            for (int i = first; i < goals.Count; i++)
                Assert.IsTrue(rewards.TryGet(new Roadblock(RoadblockKind.Puzzle, goals[i].Screen, i), out _), $"island goal {i} holds a reward");
            MathPlan math = MathPlan.Create(model, items, SeedSweep.ModuleLookup(layout, library));
            System.Func<ScreenAddress, IScreenRegions> regions = PuzzleScreens.RegionsAt(math, ice, wind, islands);
            Assert.IsEmpty(RewardPlacement.FindProblems(model, layout, items, hidden, rewards, library, regions, goals));
            var reach = new DoorwayReach(model, regions);
            Assert.IsEmpty(reach.Problems());
            for (int i = first; i < goals.Count; i++)
            {
                IslandLayout l = islands.At(goals[i].Screen);
                Assert.AreEqual(l.GoalRegion, l.RegionAt(goals[i].Local), "the chest stands in the roadblock's region");
                Assert.AreEqual(l.RewardIsland, l.IslandAt(goals[i].Local), "on the reward island");
                Assert.IsTrue(reach.CanReach(goals[i].Screen, goals[i].Local, false), $"{goals[i].Screen}: solvable, so the roadblock is reachable");
                CollectionAssert.Contains(l.Joins(l.RewardIsland, false).ToList(), l.GoalRegion);
                CollectionAssert.AreEqual(new[] { l.RewardIsland }, l.Joins(l.GoalRegion, false).ToList(), "and back");
            }

            // An unsolvable screen in its place: a star of planks round C (four odd islands), so no trail lights it all.
            var spots = new[] { N, E, S, W, C, EastOfC, WestOfC };
            var star = new IslandLayout(spots, new[] { (0, 4, false), (4, 2, false), (4, 6, false), (6, 3, false), (4, 5, false), (1, 5, true) },
                new[] { 0, 1, 2, 3 }, -1, 4, PuzzleTier.Mid);
            Assert.IsFalse(star.Solvable);
            Assert.IsFalse(star.Joins(star.RewardIsland, true).Contains(star.GoalRegion), "the roadblock never opens");
            Assert.IsTrue(star.Problems("star", _ => true).Any(p => p.Contains("no clean start")));
            ScreenAddress broken = goals[first].Screen;
            System.Func<ScreenAddress, IScreenRegions> stuck = s => s == broken ? star : regions(s);
            var stuckGoals = new List<PuzzleGoal>(goals) { [first] = new PuzzleGoal(broken, star.GoalSpot) };
            List<string> problems = RewardPlacement.FindProblems(model, layout, items, hidden, rewards, library, stuck, stuckGoals);
            Assert.IsTrue(problems.Any(p => p.Contains("can never be reached")), string.Join("; ", problems));
            Assert.IsTrue(new DoorwayReach(model, stuck).Problems().Any(p => p.Contains("can never be reached")));
        }
    }
}
