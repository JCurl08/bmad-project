using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The Math face as pure data: the face is unsealed and built; the catalog's gap-crosser* (the placeholder's stable id,
    /// no variants, its own art); the bridge-layout validator (solvable only with an Eulerian path from the flag) and the
    /// generator (always solvable, exactly the asked bridges); the walk (nothing counts before the flag, every bridge once
    /// solves it, a bridge crossed twice or the water cut across resets it); plan determinism and its rules (off-home gates
    /// are gap gates, a mix on Math, difficulty sets the bridges, the trial is bigger and fits) on every seed, with the
    /// sweep running them; layouts clear of other faces' interactables and Physics boulders clear of the layouts; the shape
    /// people's human-like silhouettes; the shape people's lines; and the subtle-science text rule for every Math string.
    /// </summary>
    public class MathTests
    {
        private const string LibraryPath = "Assets/Modules/ModuleLibrary.asset";
        private const string ItemCatalogPath = "Assets/Items/ItemCatalog.asset";
        private const int Seeds = 50;

        private static readonly string[] BannedWords = { "math", "euler", "graph", "vertex", "vertices", "edge", "theorem" };

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

        private static ItemPlacement Place(int seed, ModuleLibrary library, ItemCatalog catalog, out CubeModel model,
            out Func<ScreenAddress, ScreenModule> moduleAt)
        {
            model = new CubeModel(seed);
            CubeLayout layout = CubeLayout.Generate(model, library);
            moduleAt = SeedSweep.ModuleLookup(layout, library);
            return ItemPlacement.ForRun(model, layout, library, catalog.Themes(), catalog.VariantCounts());
        }

        private static MathPlan Plan(int seed, ModuleLibrary library, ItemCatalog catalog, out CubeModel model,
            out ItemPlacement placement, out Func<ScreenAddress, ScreenModule> moduleAt)
        {
            placement = Place(seed, library, catalog, out model, out moduleAt);
            return MathPlan.Create(model, placement, moduleAt, BiologyBeaks.VariantKinds(catalog), ChemistryIsotope.VariantKinds(catalog));
        }

        /// <summary>A walk over every bridge exactly once from the entry (Hierholzer), as the islands visited in order.</summary>
        public static List<int> SolutionPath(BridgeLayout layout)
        {
            var used = new bool[layout.BridgeCount];
            var stack = new Stack<int>();
            var path = new List<int>();
            stack.Push(layout.Entry);
            while (stack.Count > 0)
            {
                int v = stack.Peek();
                int next = -1;
                for (int b = 0; b < layout.BridgeCount; b++)
                    if (!used[b] && layout.Touches(b, v))
                    {
                        next = b;
                        break;
                    }
                if (next < 0)
                {
                    path.Add(stack.Pop());
                    continue;
                }
                used[next] = true;
                stack.Push(layout.Other(next, v));
            }
            path.Reverse();
            return path;
        }

        /// <summary>Points a player passes walking from island a to island b along their bridge (island, plank, island).</summary>
        public static IEnumerable<Vector2> Steps(BridgeLayout layout, int a, int b)
        {
            Vector2 from = layout.Islands[a], to = layout.Islands[b];
            for (int k = 0; k <= 10; k++) yield return Vector2.Lerp(from, to, k / 10f);
        }

        // ---------- Unsealed and catalog ----------

        [Test]
        public void Math_IsBuilt_AndOnlyEarthStaysSealed_AndMathIsAnItemOnEverySeed()
        {
            Assert.IsTrue(CubeModel.IsBuilt(Theme.Math));
            Assert.IsFalse(CubeModel.IsBuilt(Theme.EarthAtmosphere));
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                MathPlan plan = Plan(seed, library, catalog, out CubeModel model, out ItemPlacement placement, out _);
                Assert.IsFalse(model.IsSealed(model.FaceOf(Theme.Math)), $"seed {seed}");
                Assert.IsTrue(placement.TryGetPickup(Theme.Math, out PickupPlacement pickup), $"seed {seed}: a Math pickup");
                Assert.AreEqual(model.FaceOf(Theme.Math), pickup.Screen.Face);
                Assert.IsNotNull(plan, $"seed {seed}: a Math plan");
                Assert.IsTrue(SeedSweep.FindReachable(model).Contains(new ScreenAddress(model.FaceOf(Theme.Math), 0, 0)),
                    $"seed {seed}: Math is reachable");
            }
        }

        [Test]
        public void Catalog_MathItem_IsTheGapCrosser_WithThePlaceholdersId_NoVariants_AndItsOwnArt()
        {
            ItemCatalog catalog = Catalog();
            ItemDefinition item = catalog.ForTheme(Theme.Math);
            Assert.IsNotNull(item, "The catalog has a Math item");
            Assert.AreEqual(GapCrosserItem.Id, item.Id);
            Assert.AreEqual("math-item", item.Id, "The placeholder's id is kept");
            Assert.AreEqual(GapCrosserItem.Name, item.DisplayName);
            StringAssert.EndsWith("*", item.DisplayName, "A placeholder name carries its star");
            Assert.IsEmpty(catalog.VariantsOf(Theme.Math), "One logical item, no variants");
            Assert.AreSame(item, catalog.ItemFor(Theme.Math, 7));
            Assert.AreEqual(ArtKey.ItemGapCrosser, ArtKeys.Item(item));
            CollectionAssert.Contains(catalog.Themes(), Theme.Math);
        }

        // ---------- Validator and generator ----------

        [Test]
        public void Validator_NeedsAWalkFromTheFlag_OverConnectedBridges_WithNoneOrTwoOddIslands()
        {
            Vector2[] square = { new Vector2(0, 0), new Vector2(2, 0), new Vector2(2, 2), new Vector2(0, 2) };
            // A path 0-1-2-3: solvable from an end, not from the middle.
            var path = new[] { new Vector2Int(0, 1), new Vector2Int(1, 2), new Vector2Int(2, 3) };
            Assert.IsEmpty(new BridgeLayout(square, path, 0).Problems("path from an end"));
            Assert.IsNotEmpty(new BridgeLayout(square, path, 1).Problems("path from the middle"));
            // A ring: every island even, solvable from anywhere.
            var ring = path.Append(new Vector2Int(3, 0)).ToArray();
            for (int e = 0; e < 4; e++) Assert.IsTrue(new BridgeLayout(square, ring, e).HasWalkFrom(e));
            // A star: four odd islands, never solvable.
            Vector2[] star = { new Vector2(0, 0), new Vector2(2, 0), new Vector2(-2, 0), new Vector2(0, 2), new Vector2(0, -2) };
            var spokes = new[] { new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(0, 3), new Vector2Int(0, 4) };
            for (int e = 0; e < 5; e++) Assert.IsFalse(new BridgeLayout(star, spokes, e).HasWalkFrom(e), $"star from {e}");
            // Two separate pieces: never solvable.
            var split = new[] { new Vector2Int(0, 1), new Vector2Int(2, 3) };
            Assert.IsFalse(new BridgeLayout(square, split, 0).HasWalkFrom(0));
            // Crossing bridges are malformed.
            var crossing = new[] { new Vector2Int(0, 2), new Vector2Int(1, 3) };
            Assert.IsTrue(new BridgeLayout(square, crossing, 0).Problems("x").Any(p => p.Contains("cross")));
        }

        [Test]
        public void Generator_AlwaysMakesASolvableLayout_WithExactlyTheAskedBridges_AndAJunctionWhenBigEnough()
        {
            for (int s = 0; s < 200; s++)
            {
                var rng = new SeededRng((ulong)s, MathPlan.RngStream);
                int bridges = MathPlan.MinDoorBridges + s % (MathPlan.MaxDoorBridges - MathPlan.MinDoorBridges + 1);
                BridgeLayout door = BridgeLayout.Generate(MathPlan.DoorCols, MathPlan.DoorRows, bridges, rng);
                Assert.IsNotNull(door, $"door {s}");
                Assert.AreEqual(bridges, door.BridgeCount);
                Assert.IsEmpty(door.Problems($"door {s}"));
                Assert.AreEqual(door.BridgeCount + 1, SolutionPath(door).Count, "The walk visits every bridge once");
                CollectionAssert.AreEqual(SolutionPath(door), door.Solve());
                if (bridges >= 5)
                    Assert.IsTrue(Enumerable.Range(0, door.Islands.Count).Any(i => door.Degree(i) >= 3), $"door {s}: a junction");

                BridgeLayout trial = BridgeLayout.Generate(MathPlan.TrialCols, MathPlan.TrialRows, MathPlan.TrialBridges, rng);
                Assert.IsNotNull(trial, $"trial {s}");
                Assert.AreEqual(MathPlan.TrialBridges, trial.BridgeCount);
                Assert.IsEmpty(trial.Problems($"trial {s}"));
            }
            Assert.Greater(MathPlan.TrialBridges, MathPlan.MaxDoorBridges, "The trial is bigger than any door");
        }

        // ---------- The walk ----------

        [Test]
        public void Walk_NothingCountsBeforeTheFlag_EveryBridgeOnceSolvesIt_TwiceOrCuttingAcrossResets()
        {
            var rng = new SeededRng(11UL, MathPlan.RngStream);
            BridgeLayout layout = BridgeLayout.Generate(MathPlan.DoorCols, MathPlan.DoorRows, 6, rng);
            List<int> solution = SolutionPath(layout);
            int notEntry = Enumerable.Range(0, layout.Islands.Count).First(i => i != layout.Entry && layout.Degree(i) > 0);

            // Before the flag: stepping on other islands and planks does nothing.
            var walk = new BridgeWalk(layout);
            Assert.AreEqual(BridgeWalk.Result.None, walk.Observe(layout.Islands[notEntry]));
            int plank = Enumerable.Range(0, layout.BridgeCount).First(b => !layout.Touches(b, layout.Entry));
            layout.Ends(plank, out Vector2 e0, out Vector2 e1);
            Assert.AreEqual(BridgeWalk.Result.None, walk.Observe((e0 + e1) / 2f));
            Assert.IsFalse(walk.IsActive);
            Assert.AreEqual(0, walk.CrossedCount);

            // From the flag, every bridge exactly once: solved, once.
            var results = new List<BridgeWalk.Result>();
            results.Add(walk.Observe(layout.Islands[layout.Entry]));
            Assert.AreEqual(BridgeWalk.Result.Started, results[0]);
            for (int k = 0; k + 1 < solution.Count; k++)
                foreach (Vector2 p in Steps(layout, solution[k], solution[k + 1]))
                    results.Add(walk.Observe(p));
            Assert.IsTrue(walk.IsSolved);
            Assert.AreEqual(1, results.Count(r => r == BridgeWalk.Result.Solved));
            Assert.AreEqual(layout.BridgeCount - 1, results.Count(r => r == BridgeWalk.Result.Crossed));
            Assert.IsFalse(results.Any(r => r == BridgeWalk.Result.CrossedTwice || r == BridgeWalk.Result.OffTrail));
            Assert.AreEqual(BridgeWalk.Result.None, walk.Observe(layout.Islands[layout.Entry]), "Solved for good");

            // A bridge crossed twice resets every bridge.
            walk = new BridgeWalk(layout);
            walk.Observe(layout.Islands[layout.Entry]);
            int a = solution[0], b = solution[1];
            foreach (Vector2 p in Steps(layout, a, b)) walk.Observe(p);
            Assert.AreEqual(1, walk.CrossedCount);
            BridgeWalk.Result back = BridgeWalk.Result.None;
            foreach (Vector2 p in Steps(layout, b, a))
            {
                BridgeWalk.Result r = walk.Observe(p);
                if (r != BridgeWalk.Result.None) back = r;
            }
            Assert.AreEqual(BridgeWalk.Result.CrossedTwice, back);
            Assert.AreEqual(0, walk.CrossedCount, "Every bridge is reset");
            Assert.IsFalse(walk.IsSolved);

            // Cutting across the water to an island no bridge joins resets too.
            walk = new BridgeWalk(layout);
            walk.Observe(layout.Islands[layout.Entry]);
            foreach (Vector2 p in Steps(layout, a, b)) walk.Observe(p);
            int far = Enumerable.Range(0, layout.Islands.Count).FirstOrDefault(i => i != b && layout.BridgeBetween(b, i) < 0);
            walk.Observe(layout.Islands[b] + new Vector2(0.7f, 0.7f)); // in the water
            Assert.AreEqual(BridgeWalk.Result.OffTrail, walk.Observe(layout.Islands[far]));
            Assert.AreEqual(0, walk.CrossedCount);
        }

        [Test]
        public void Walk_ABridgedNeighbourReachedThroughTheWater_IsOffTrail_AndSilentBeforeAnyCrossing()
        {
            var rng = new SeededRng(11UL, MathPlan.RngStream);
            BridgeLayout layout = BridgeLayout.Generate(MathPlan.DoorCols, MathPlan.DoorRows, 6, rng);
            List<int> path = layout.Solve();
            int a = path[0], b = path[1], c = path[2];

            // Through the water from the flag to its bridged neighbour, before any crossing: nothing counts, nothing to reset.
            var walk = new BridgeWalk(layout);
            Assert.AreEqual(BridgeWalk.Result.Started, walk.Observe(layout.Islands[a]));
            Vector2 side = Water(layout, a, b);
            Assert.AreEqual(BridgeWalk.Result.None, walk.Observe(side));
            BridgeWalk.Result silent = walk.Observe(layout.Islands[b]);
            Assert.AreNotEqual(BridgeWalk.Result.OffTrail, silent, "Silent before any bridge is crossed");
            Assert.AreNotEqual(BridgeWalk.Result.Crossed, silent, "The bridge was never walked");
            Assert.AreEqual(0, walk.CrossedCount);
            Assert.IsFalse(walk.Crossed(layout.BridgeBetween(a, b)));

            // After a real crossing, cutting through the water to the next bridged island is off the trail.
            walk = new BridgeWalk(layout);
            walk.Observe(layout.Islands[a]);
            foreach (Vector2 p in Steps(layout, a, b)) walk.Observe(p);
            Assert.AreEqual(1, walk.CrossedCount);
            walk.Observe(Water(layout, b, c));
            Assert.AreEqual(BridgeWalk.Result.OffTrail, walk.Observe(layout.Islands[c]));
            Assert.AreEqual(0, walk.CrossedCount, "Every bridge is reset");

            // A short plank crossed between two samples (island to island, along its line) still counts.
            walk = new BridgeWalk(layout);
            walk.Observe(layout.Islands[a]);
            Vector2 from = layout.Islands[a], to = layout.Islands[b];
            walk.Observe(from + (to - from).normalized * 0.45f);
            Assert.AreEqual(BridgeWalk.Result.Crossed, walk.Observe(to - (to - from).normalized * 0.45f));
        }

        /// <summary>A point in the water beside the bridge from a to b (halfway along, a plank's width and more off it).</summary>
        private static Vector2 Water(BridgeLayout layout, int a, int b)
        {
            Vector2 from = layout.Islands[a], to = layout.Islands[b];
            Vector2 normal = new Vector2(-(to - from).y, (to - from).x).normalized;
            return (from + to) / 2f + normal * 0.6f;
        }

        // ---------- Plan ----------

        [Test]
        public void Plan_IsDeterministic_OnItsOwnStream_AndHasNoProblems_OnEverySeed()
        {
            Assert.AreEqual(17UL, MathPlan.RngStream);
            Assert.AreEqual(18UL, MathFace.PopulationRngStream);
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                MathPlan a = Plan(seed, library, catalog, out CubeModel model, out ItemPlacement placement, out var moduleAt);
                MathPlan b = Plan(seed, library, catalog, out _, out _, out _);
                Assert.AreEqual(a.Signature(), b.Signature(), $"seed {seed}");
                List<string> problems = MathPlan.FindProblems(model, placement, moduleAt, BiologyBeaks.VariantKinds(catalog),
                    ChemistryIsotope.VariantKinds(catalog));
                Assert.IsEmpty(problems, $"seed {seed}: {string.Join("; ", problems)}");
            }
        }

        [Test]
        public void Plan_OffHomeGatesAreGaps_MathHasBoth_DifficultySetsTheBridges_AndTheTrialIsBiggerAndFits()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            int doors = 0, gaps = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                MathPlan plan = Plan(seed, library, catalog, out CubeModel model, out ItemPlacement placement, out _);
                FaceId math = model.FaceOf(Theme.Math);
                Assert.IsTrue(plan.Gates.Any(g => !g.OnHome && g.Kind == MathGateKind.Gap), $"seed {seed}: a gap gate on another face");
                Assert.IsTrue(plan.Gates.All(g => g.OnHome || g.Kind == MathGateKind.Gap), $"seed {seed}");
                Assert.IsTrue(plan.Gates.Any(g => g.OnHome && g.Kind == MathGateKind.Gap), $"seed {seed}: a gap gate on Math");
                Assert.IsTrue(plan.Gates.Any(g => g.OnHome && g.Kind == MathGateKind.BridgeDoor), $"seed {seed}: a bridge door on Math");
                int home = placement.Gates.Count(g => g.Item == Theme.Math && g.Screen.Face == math);
                foreach (MathGateSpec g in plan.Gates)
                {
                    Assert.AreEqual(home, g.Difficulty);
                    if (g.Kind != MathGateKind.BridgeDoor)
                    {
                        gaps++;
                        continue;
                    }
                    doors++;
                    Assert.AreEqual(math, g.Screen.Face, "Bridge doors are on Math");
                    Assert.IsNotNull(g.Layout, $"seed {seed}: {g}");
                    Assert.AreEqual(MathPlan.BridgesFor(home, model.FaceSize), g.Layout.BridgeCount);
                    Assert.IsTrue(g.Layout.HasWalkFrom(g.Layout.Entry));
                }
                Assert.IsNotNull(plan.TrialLayout, $"seed {seed}: the trial fits");
                Assert.AreEqual(math, plan.TrialScreen.Face);
                Assert.Greater(plan.TrialLayout.BridgeCount, MathPlan.MaxDoorBridges);
                Assert.IsTrue(plan.TrialLayout.HasWalkFrom(plan.TrialLayout.Entry));
            }
            Assert.Greater(doors, 0);
            Assert.Greater(gaps, 0);
        }

        [Test]
        public void Difficulty_MoreMathGatesOnTheFace_MeansMoreBridges_WithinTheLatticeLimit()
        {
            int previous = 0;
            for (int d = 1; d <= 30; d++)
            {
                int b = MathPlan.BridgesFor(d, 3);
                Assert.GreaterOrEqual(b, previous, "Monotonic");
                Assert.GreaterOrEqual(b, MathPlan.MinDoorBridges);
                Assert.LessOrEqual(b, MathPlan.MaxDoorBridges);
                previous = b;
            }
            Assert.Greater(MathPlan.BridgesFor(5, 3), MathPlan.BridgesFor(1, 3));
            Assert.LessOrEqual(MathPlan.MaxDoorBridges, (MathPlan.DoorCols - 1) * MathPlan.DoorRows + MathPlan.DoorCols * (MathPlan.DoorRows - 1));
        }

        [Test]
        public void SweepRules_FlagAnUnsolvableDoor_ASmallTrial_AMissingOffFaceGap_AndAPickupBehindADeadDoor()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            MathPlan plan = Plan(1, library, catalog, out CubeModel model, out ItemPlacement placement, out _);
            int door = plan.Gates.ToList().FindIndex(g => g.Kind == MathGateKind.BridgeDoor);
            Assert.GreaterOrEqual(door, 0);
            MathGateSpec doorSpec = plan.Gates[door];
            BridgeLayout good = doorSpec.Layout;
            Assert.AreEqual(6, doorSpec.Bridges, "Seed 1's doors have six bridges");
            // The full 3 x 2 lattice less its left link: six bridges, nothing overlapping or crossing, connected, but four
            // islands with an odd number of bridges (0, 1, 3 and 4), so no walk from the flag crosses every bridge once.
            float sp = BridgeLayout.Spacing;
            var lattice = new List<Vector2>();
            for (int row = 0; row < 2; row++)
                for (int col = 0; col < 3; col++)
                    lattice.Add(good.Centre + new Vector2((col - 1) * sp, (row - 0.5f) * sp));
            var fourOdd = new BridgeLayout(lattice, new[]
            {
                new Vector2Int(0, 1), new Vector2Int(1, 2), new Vector2Int(2, 5), new Vector2Int(4, 5), new Vector2Int(3, 4),
                new Vector2Int(1, 4),
            }, 0);
            Assert.AreEqual(4, Enumerable.Range(0, 6).Count(i => fourOdd.Degree(i) % 2 == 1));
            const string noWalk = "no walk from the flag crosses every bridge exactly once";
            List<string> own = fourOdd.Problems("four odd");
            Assert.AreEqual(1, own.Count, string.Join("; ", own));
            StringAssert.Contains(noWalk, own[0], "Only the walk is wrong with it");
            List<string> problems = plan.WithGate(door, doorSpec.WithLayout(fourOdd)).Problems(model, placement);
            Assert.IsTrue(problems.Any(p => p.Contains(doorSpec.ToString()) && p.Contains(noWalk)), string.Join("; ", problems));
            Assert.IsFalse(problems.Any(p => p.Contains("bridges") && p.Contains("its layout has")), "The bridge count is right");

            var smallTrial = BridgeLayout.Generate(MathPlan.DoorCols, MathPlan.DoorRows, 4, new SeededRng(3UL, 1UL))
                .Translated(plan.TrialLayout.Centre);
            problems = plan.WithTrial(smallTrial).Problems(model, placement);
            Assert.IsTrue(problems.Any(p => p.Contains("no more than a door")), string.Join("; ", problems));

            // Hand-made: every Math gate on Math, so the gap-crosser* opens nothing elsewhere.
            FaceId math = model.FaceOf(Theme.Math);
            var only = new ItemPlacement(1, new[]
            {
                new GatePlacement(new ScreenAddress(math, 0, 0), 0, Theme.Math),
                new GatePlacement(new ScreenAddress(math, 1, 0), 0, Theme.Math),
            }, new[] { new PickupPlacement(Theme.Math, new ScreenAddress(math, 1, 1)) });
            problems = MathPlan.FindProblems(model, only);
            Assert.IsTrue(problems.Any(p => p.Contains("no gate on another face")), string.Join("; ", problems));

            // A pickup behind a door whose layout has no walk is a softlock.
            int guard = plan.Gates.ToList().FindIndex(g => g.Kind == MathGateKind.BridgeDoor);
            MathGateSpec spec = plan.Gates[guard];
            var guarded = new ItemPlacement(1, placement.Gates, placement.Pickups
                .Select(p => p.Item == Theme.Biology ? new PickupPlacement(Theme.Biology, spec.Screen, spec.Slot) : p).ToList());
            var dead = new BridgeLayout(spec.Layout.Islands, new[] { new Vector2Int(0, 1), new Vector2Int(2, 3) }, 0);
            problems = plan.WithGate(guard, spec.WithLayout(dead)).Problems(model, guarded);
            Assert.IsTrue(problems.Any(p => p.Contains("softlock")), string.Join("; ", problems));
        }

        [Test]
        public void TheSeedSweep_RunsTheMathRules_AndPassesEverySeed()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            List<SeedSweep.SeedResult> results = SeedSweep.Run(catalog: library, itemThemes: catalog.Themes(),
                variantCounts: catalog.VariantCounts(), biologyVariantKinds: BiologyBeaks.VariantKinds(catalog),
                chemistryVariantKinds: ChemistryIsotope.VariantKinds(catalog));
            string report = SeedSweep.Describe(results);
            Assert.IsTrue(results.All(r => r.Passed), report);
            StringAssert.Contains("every bridge layout solvable from its flag", report);
            StringAssert.Contains("the gap-crosser* opening a gate on another face", report);
            StringAssert.Contains("the Math trial fitting", report);
        }

        // ---------- Spots ----------

        [Test]
        public void Layouts_KeepOffTheLanes_WallsAndOtherFacesInteractables_AndPhysicsKeepsItsBouldersOffThem()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                MathPlan plan = Plan(seed, library, catalog, out CubeModel model, out ItemPlacement placement, out var moduleAt);
                var layouts = plan.Gates.Where(g => g.Layout != null).Select(g => (g.Screen, g.Layout)).ToList();
                layouts.Add((plan.TrialScreen, plan.TrialLayout));
                foreach ((ScreenAddress screen, BridgeLayout layout) in layouts)
                {
                    foreach (Vector2 p in layout.Islands)
                    {
                        Assert.GreaterOrEqual(Mathf.Abs(p.y) - BridgeLayout.OnIslandRadius, ScreenModule.ExitLaneHalfWidth, $"seed {seed}: an island in the east-west lane");
                        Assert.GreaterOrEqual(Mathf.Abs(p.x) - BridgeLayout.OnIslandRadius, ScreenModule.ExitLaneHalfWidth, $"seed {seed}: an island in the north-south lane");
                    }
                    foreach (Vector2 p in layout.BridgeSamples(0.1f))
                        Assert.IsTrue(Mathf.Abs(p.x) - BridgeLayout.OnBridgeHalfWidth >= ScreenModule.ExitLaneHalfWidth &&
                                      Mathf.Abs(p.y) - BridgeLayout.OnBridgeHalfWidth >= ScreenModule.ExitLaneHalfWidth, $"seed {seed}: a bridge in a lane");
                    List<string> fit = MathPlan.FitProblems(layout, TownPlan.BlockedRects(moduleAt(screen)), plan.MarksOn(screen),
                        plan.LayoutsOn(screen));
                    Assert.IsEmpty(fit, $"seed {seed} {screen}: {string.Join("; ", fit)}");
                }

                // Physics keeps its switches and boulders clear of every Math layout and gap gate.
                PhysicsPlan physics = PhysicsPlan.Create(model, placement, moduleAt, BiologyBeaks.VariantKinds(catalog),
                    PhysicsPlan.DefaultMaxRequired, ChemistryIsotope.VariantKinds(catalog));
                Dictionary<ScreenAddress, List<ScreenMark>> marks = plan.ContentMarks();
                foreach (TimedDoorSpec door in physics.Doors)
                {
                    if (!marks.TryGetValue(door.Screen, out List<ScreenMark> list)) continue;
                    foreach (ScreenMark m in list)
                        foreach (Vector2 b in door.Boulders)
                            Assert.GreaterOrEqual(Vector2.Distance(b, m.Local), PhysicsPlan.Gap(Boulder.Radius, m.Radius) - 1e-3f,
                                $"seed {seed}: a boulder of {door} sits on {m.What}");
                }
            }
        }

        // ---------- Shape people ----------

        private static int Width(byte[] px, int w, int h, float v)
        {
            int y = Mathf.Clamp(Mathf.FloorToInt(v * h), 0, h - 1);
            int n = 0;
            for (int x = 0; x < w; x++)
                if (px[y * w + x] != 0) n++;
            return n;
        }

        private static List<(int start, int width)> RunSpans(byte[] px, int w, int h, float v)
        {
            int y = Mathf.Clamp(Mathf.FloorToInt(v * h), 0, h - 1);
            var spans = new List<(int, int)>();
            int start = -1;
            for (int x = 0; x <= w; x++)
            {
                bool body = x < w && px[y * w + x] != 0 && !NpcPartArt.IsOutline(px[y * w + x]);
                if (body && start < 0) start = x;
                if (!body && start >= 0)
                {
                    spans.Add((start, x - start));
                    start = -1;
                }
            }
            return spans;
        }

        private static int Runs(byte[] px, int w, int h, float v)
        {
            int y = Mathf.Clamp(Mathf.FloorToInt(v * h), 0, h - 1);
            int runs = 0;
            bool inside = false;
            for (int x = 0; x < w; x++)
            {
                byte c = px[y * w + x];
                bool body = c != 0 && !NpcPartArt.IsOutline(c);
                if (body && !inside) runs++;
                inside = body;
            }
            return runs;
        }

        [Test]
        public void ShapePeople_HaveRoundFaces_TriangularBodies_AndTwoRectangularLegs_WithTheirVarietyVisible()
        {
            RacePartSet set = RacePartSets.For(Race.Shape);
            foreach (NpcPart head in set.Heads)
            {
                byte[] px = NpcPartArt.Pixels(Race.Shape, head, out int w, out int h);
                int cheeks = Width(px, w, h, 0.4f), chin = Width(px, w, h, 0.06f);
                Assert.Greater(cheeks, w / 2 - 1, $"{head.Id}: a full round face");
                Assert.Less(chin, cheeks, $"{head.Id}: the face rounds off at the chin");
                Assert.AreNotEqual(0, px[Mathf.FloorToInt(0.4f * h) * w + w / 2], $"{head.Id}: a face in the middle");
            }
            foreach (NpcPart torso in set.Torsos)
            {
                byte[] px = NpcPartArt.Pixels(Race.Shape, torso, out int w, out int h);
                Assert.Less(Width(px, w, h, 0.93f) * 2, Width(px, w, h, 0.08f), $"{torso.Id}: a triangle, wide at the hem and pointed at the neck");
            }
            foreach (NpcPart legs in set.Legs)
            {
                byte[] px = NpcPartArt.Pixels(Race.Shape, legs, out int w, out int h);
                Assert.AreEqual(2, Runs(px, w, h, 0.7f), $"{legs.Id}: two separate legs");
                // Rectangular legs: each leg is the same width on every row from the knee to the hip.
                List<(int start, int width)> knee = RunSpans(px, w, h, 0.4f);
                for (float v = 0.4f; v <= 0.9f; v += 0.05f)
                    CollectionAssert.AreEqual(knee, RunSpans(px, w, h, v), $"{legs.Id}: a leg is not a rectangle at {v:0.00}");
            }
            // The variety still shows: circle, triangle and rhombus kinds look different in every slot.
            foreach (PartSlot slot in new[] { PartSlot.Head, PartSlot.Legs })
            {
                var looks = set.Slot(slot).Select(p => string.Join("", NpcPartArt.Pixels(Race.Shape, p, out _, out _))).ToList();
                Assert.AreEqual(looks.Count, looks.Distinct().Count(), $"{slot}: every kind looks different");
            }
            CollectionAssert.IsSubsetOf(new[] { PartShape.Circle, PartShape.Triangle, PartShape.Diamond },
                set.Legs.Select(l => l.Shape).ToList(), "Fast, stable and glide legs");
        }

        // ---------- Lines ----------

        [Test]
        public void ShapeLines_ExplainTheBridgesAndTheGapCrosser_TheRestTheBridgekeeper_AndEndWithATrueHint()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            for (int seed = 1; seed <= 10; seed++)
            {
                ItemPlacement placement = Place(seed, library, catalog, out CubeModel model, out _);
                var facts = new RunFacts(model, CubeLayout.Generate(model, library), placement);
                for (int i = 0; i < MathPopulation.ShapeCount(model.FaceSize); i++)
                {
                    NpcSpec spec = MathPopulation.ShapeSpec(seed, i);
                    Assert.AreEqual(Race.Shape, spec.Race);
                    List<string> lines = MathPopulation.LinesFor(spec, facts, HintDensity.Full, i);
                    Assert.AreEqual(Dialogue.MaxLines, lines.Count);
                    if (i == MathPopulation.BridgeTeller) Assert.AreEqual(Dialogue.BridgeLine, lines[0]);
                    else if (i == MathPopulation.GapTeller) Assert.AreEqual(Dialogue.GapCrosserLine, lines[0]);
                    else CollectionAssert.Contains(Dialogue.BridgekeeperLines, lines[0]);
                    Assert.AreEqual(HintGenerator.Generate(facts, spec.HintKind, spec.Salt, HintDensity.Full)[0].Text, lines[1]);
                }
            }
            StringAssert.Contains("exactly once", Dialogue.BridgeLine);
            StringAssert.Contains("reset", Dialogue.BridgeLine);
            StringAssert.Contains(GapCrosserItem.Name, Dialogue.GapCrosserLine);
        }

        // ---------- Text ----------

        private static List<string> MathStrings()
        {
            Dialogue.RaceFlavour shape = Dialogue.Flavours[Race.Shape];
            var strings = new List<string> { shape.Wares, shape.Metal, shape.Errand };
            strings.AddRange(shape.Rumours);
            strings.AddRange(shape.Greetings);
            strings.AddRange(Dialogue.BridgekeeperLines);
            strings.Add(Dialogue.BridgeLine);
            strings.Add(Dialogue.GapCrosserLine);
            strings.Add(BridgePuzzle.CrossedTwiceText);
            strings.Add(BridgePuzzle.OffTrailText);
            strings.Add(BridgePuzzle.SolvedText);
            strings.Add(GapCrosserItem.Name);
            strings.Add(HintGenerator.ItemNickname(Theme.Math));
            ItemDefinition item = Catalog().ForTheme(Theme.Math);
            strings.Add(item.DisplayName);
            strings.Add(PickupFeedback.ItemText(item));
            return strings;
        }

        private static IEnumerable<string> MathNames()
        {
            yield return MathEnemies.Name(MathEnemyKind.DeadEnd);
            yield return MathEnemies.Name(MathEnemyKind.LooseEnd);
            RacePartSet set = RacePartSets.For(Race.Shape);
            foreach (PartSlot slot in new[] { PartSlot.Head, PartSlot.Torso, PartSlot.Legs })
                foreach (NpcPart p in set.Slot(slot)) yield return p.Name;
        }

        [Test]
        public void MathText_UsesNoBannedWords_AndPlaceholderNamesCarryAStar()
        {
            List<string> strings = MathStrings();
            Assert.Greater(strings.Count, 15);
            // The hints the shape people actually speak, on every seed and density.
            var hints = new List<string>();
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                ItemPlacement placement = Place(seed, library, catalog, out CubeModel model, out _);
                var facts = new RunFacts(model, CubeLayout.Generate(model, library), placement);
                foreach (HintDensity density in new[] { HintDensity.Sparse, HintDensity.Full })
                    for (int i = 0; i < MathPopulation.ShapeCount(model.FaceSize); i++)
                        hints.AddRange(MathPopulation.LinesFor(MathPopulation.ShapeSpec(seed, i), facts, density, i).Skip(1));
            }
            Assert.Greater(hints.Count, 100);
            Assert.IsTrue(hints.Any(h => h.Contains(HintGenerator.MathRegionName)), "Some hint names the Math face, by its placeholder name");
            StringAssert.EndsWith("*", HintGenerator.MathRegionName);
            Assert.AreEqual(HintGenerator.MathRegionName, HintGenerator.ThemeName(Theme.Math));

            foreach (string text in strings.Concat(MathNames()).Concat(hints))
            {
                string lower = text.ToLowerInvariant();
                foreach (string banned in BannedWords)
                    StringAssert.DoesNotContain(banned, lower, $"\"{text}\" uses a banned word");
                StringAssert.DoesNotContain(Dialogue.ForbiddenWord, lower, text);
            }

            // Proper names: a capitalised word that does not start a sentence is a name, and names carry a star.
            var notNames = new HashSet<string> { "I", "I'm", "I'll", "I've", "I'd" };
            foreach (string text in strings)
            {
                string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < words.Length; i++)
                {
                    string word = words[i].TrimStart('.', '"', '\'', '(', '+', '*');
                    if (word.Length == 0 || !char.IsUpper(word[0])) continue;
                    string prev = i > 0 ? words[i - 1].TrimEnd('\'', '"', '*', ')') : "";
                    bool sentenceStart = i == 0 || (prev.Length > 0 && ".!?:".IndexOf(prev[prev.Length - 1]) >= 0);
                    int end = 0;
                    while (end < word.Length && (char.IsLetter(word[end]) || word[end] == '\'' || word[end] == '-')) end++;
                    string core = word.Substring(0, end);
                    bool starred = end < word.Length && word[end] == '*';
                    if (sentenceStart || notNames.Contains(core)) continue;
                    Assert.IsTrue(starred, $"\"{core}\" looks like a name without its star in \"{text}\"");
                }
            }

            // Placeholder names carry a trailing star wherever they appear.
            StringAssert.EndsWith("*", GapCrosserItem.Name);
            foreach (string text in strings)
                foreach (string bare in new[] { "Bridgekeeper", "Gap-Crosser" })
                {
                    int at = text.IndexOf(bare, StringComparison.Ordinal);
                    while (at >= 0)
                    {
                        int after = at + bare.Length;
                        Assert.IsTrue(after < text.Length && text[after] == '*', $"\"{bare}\" without its star in \"{text}\"");
                        at = text.IndexOf(bare, after, StringComparison.Ordinal);
                    }
                }
            StringAssert.Contains("Bridgekeeper*", string.Join(" ", Dialogue.BridgekeeperLines));
        }
    }
}
