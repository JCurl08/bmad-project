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
    /// no variants, its own art); the island screens (1.25): every doorway lands on its own island, islands are separate
    /// land in solid water, planks run shore to shore between lattice neighbours, and the islands deliberately do not all
    /// join (a pair of doorways needs the gap-crosser*, a doorway joins none, the trial waits behind a broken span); the
    /// water as solid rects; the walk doorway by doorway (DoorwayReach: per-doorway reachability, two-way, never stranded);
    /// plan determinism and its rules on every seed, the sweep running them and catching broken plans; Physics boulders
    /// clear of Math's gates; the shape people's human-like silhouettes and lines; and the subtle-science text rule for
    /// every Math string (no word of the old bridge puzzle).
    /// </summary>
    public class MathTests
    {
        private const string LibraryPath = "Assets/Modules/ModuleLibrary.asset";
        private const string ItemCatalogPath = "Assets/Items/ItemCatalog.asset";
        private const int Seeds = 50;
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

        private static ItemPlacement Place(int seed, ModuleLibrary library, ItemCatalog catalog, out CubeModel model,
            out Func<ScreenAddress, ScreenModule> moduleAt, int faceSize = CubeSettings.DefaultFaceSize)
        {
            model = new CubeModel(seed, faceSize);
            CubeLayout layout = CubeLayout.Generate(model, library);
            moduleAt = SeedSweep.ModuleLookup(layout, library);
            return ItemPlacement.ForRun(model, layout, library, catalog.Themes(), catalog.VariantCounts());
        }

        private static MathPlan Plan(int seed, ModuleLibrary library, ItemCatalog catalog, out CubeModel model,
            out ItemPlacement placement, out Func<ScreenAddress, ScreenModule> moduleAt, int faceSize = CubeSettings.DefaultFaceSize)
        {
            placement = Place(seed, library, catalog, out model, out moduleAt, faceSize);
            return MathPlan.Create(model, placement, moduleAt, BiologyBeaks.VariantKinds(catalog), ChemistryIsotope.VariantKinds(catalog));
        }

        private static IslandLayout Generated(ulong seed, bool trial) =>
            IslandLayout.Generate(new SeededRng(seed, MathPlan.RngStream), _ => true, trial);

        // ---------- Unsealed and catalog ----------

        [Test]
        public void Math_IsBuilt_AndMathIsAnItemOnEverySeed()
        {
            Assert.IsTrue(CubeModel.IsBuilt(Theme.Math));
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

        // ---------- Island screens ----------

        [Test]
        public void IslandLayouts_EveryDoorwayLandsOnItsOwnIsland_IslandsStandApart_AndPlanksRunShoreToShore()
        {
            Rect water = IslandLayout.WaterArea;
            for (ulong s = 0; s < 300; s++)
            {
                IslandLayout layout = Generated(s, s % 2 == 0);
                Assert.IsNotNull(layout, $"layout {s}");
                Assert.IsEmpty(layout.Problems($"layout {s}", _ => true));
                foreach (Facing side in Sides)
                {
                    int island = layout.DoorIsland(side);
                    Assert.GreaterOrEqual(island, 0, $"layout {s}: the {side} doorway lands on an island");
                    Assert.AreEqual(island, layout.IslandAt(IslandLayout.Arrival(side)), $"layout {s}: arriving by the {side} doorway");
                    Rect door = ScreenEdges.DoorwayRect(side);
                    Rect land = layout.Islands[island];
                    // The island meets the doorway across its whole width, at the wall's inner face.
                    if (side.IsHorizontal())
                        Assert.IsTrue(land.yMin <= door.yMin && land.yMax >= door.yMax, $"layout {s} {side}");
                    else
                        Assert.IsTrue(land.xMin <= door.xMin && land.xMax >= door.xMax, $"layout {s} {side}");
                }
                for (int i = 0; i < layout.Islands.Count; i++)
                {
                    Rect a = layout.Islands[i];
                    Assert.IsTrue(water.Contains(a.min) && water.Contains(a.max - new Vector2(1e-4f, 1e-4f)), $"layout {s}: island {i} in the water area");
                    for (int j = i + 1; j < layout.Islands.Count; j++)
                    {
                        Rect b = layout.Islands[j];
                        float dx = Mathf.Max(0f, Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax));
                        float dy = Mathf.Max(0f, Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax));
                        Assert.GreaterOrEqual(Mathf.Max(dx, dy), IslandLayout.MinWater - 1e-4f, $"layout {s}: islands {i} and {j} have water between them");
                    }
                }
                foreach (IslandBridge b in layout.Bridges)
                {
                    Assert.GreaterOrEqual(b.Across, 2f * TimeField.PlayerRadius + 0.2f, "a plank the player fits on");
                    Assert.Greater(b.Span, 0.5f, $"layout {s}: a plank spans real water");
                    if (b.Broken) Assert.GreaterOrEqual(b.Span, IslandLayout.GapLength + 2f * IslandLayout.MinPlankEnd - 1e-4f);
                }
            }
        }

        [Test]
        public void IslandLayouts_AreNotFullyJoined_APairNeedsTheGapCrosser_ADoorwayJoinsNone_AndTheTrialWaitsBehindABrokenSpan()
        {
            int lookouts = 0, longer = 0;
            for (ulong s = 0; s < 300; s++)
            {
                IslandLayout layout = Generated(s, s % 2 == 0);
                int[] whole = layout.Components(false), all = layout.Components(true);
                var doors = Sides.Select(layout.DoorIsland).ToList();
                // Two doorways join over whole planks; a third joins them only with the gap-crosser*; one joins none.
                Assert.IsTrue(doors.Any(a => doors.Any(b => a != b && whole[a] == whole[b])), $"layout {s}: a pair over whole planks");
                Assert.IsTrue(doors.Any(a => doors.Any(b => a != b && all[a] == all[b] && whole[a] != whole[b])), $"layout {s}: a pair over a broken span");
                Assert.IsTrue(doors.Any(a => doors.All(b => a == b || all[a] != all[b])), $"layout {s}: a doorway that joins none");
                Assert.IsTrue(layout.HasBroken);
                if (layout.HasTrial)
                {
                    Assert.IsEmpty(layout.DoorsReaching(layout.TrialIsland, false), $"layout {s}: the trial needs the gap-crosser*");
                    Assert.GreaterOrEqual(layout.DoorsReaching(layout.TrialIsland, true).Count, 2, $"layout {s}: the trial joins the pair");
                }
                // Every island joins some doorway (nobody lands where they cannot walk out).
                for (int i = 0; i < layout.Islands.Count; i++)
                    Assert.IsNotEmpty(layout.DoorsReaching(i, true), $"layout {s}: island {i}");
                if (layout.Islands.Count > (layout.HasTrial ? 5 : 4)) longer++;
                Facing lone = Sides.First(d => Sides.All(o => o == d || all[layout.DoorIsland(d)] != all[layout.DoorIsland(o)]));
                if (layout.Bridges.Any(b => b.Joins(layout.DoorIsland(lone)))) lookouts++;
            }
            Assert.Greater(lookouts, 25, "The lone doorway sometimes has a lookout island");
            Assert.Greater(longer, 200, "Routes run over inner islands, spread over the screen");
        }

        [Test]
        public void IslandLayout_Problems_CatchADoorwayInTheWater_AFullyJoinedScreen_AnIslandNobodyReaches_AndPlanksThatAreNotShoreToShore()
        {
            Vector2Int n = IslandLayout.DoorSpot(Facing.North), e = IslandLayout.DoorSpot(Facing.East),
                s = IslandLayout.DoorSpot(Facing.South), w = IslandLayout.DoorSpot(Facing.West), c = new Vector2Int(2, 1),
                east = new Vector2Int(3, 1), west = new Vector2Int(1, 1);
            var spots = new[] { n, e, s, w, c, east, west };
            int[] doors = { 0, 1, 2, 3 };
            // Good: N-C-S whole, E over a broken span to C, W alone.
            var good = new IslandLayout(spots.Take(6), new[] { (0, 4, false), (4, 2, false), (1, 5, false), (5, 4, true) }, doors);
            Assert.IsEmpty(good.Problems("good", _ => true));
            // Fully joined: W joins too.
            var joined = new IslandLayout(spots, new[] { (0, 4, false), (4, 2, false), (1, 5, false), (5, 4, true), (3, 6, false), (6, 4, false) }, doors);
            Assert.IsTrue(joined.Problems("joined", _ => true).Any(p => p.Contains("fully connected")));
            // Nothing over a broken span.
            var noGap = new IslandLayout(spots.Take(6), new[] { (0, 4, false), (4, 2, false), (1, 5, false), (5, 4, false) }, doors);
            Assert.IsTrue(noGap.Problems("no gap", _ => true).Any(p => p.Contains("broken span")));
            // An island joined to no doorway.
            var orphan = new IslandLayout(spots, new[] { (0, 4, false), (4, 2, false), (1, 5, false), (5, 4, true) }, doors);
            Assert.IsTrue(orphan.Problems("orphan", _ => true).Any(p => p.Contains("joins no doorway")));
            // A doorway with no island.
            Assert.IsTrue(new IslandLayout(spots.Take(6), new[] { (0, 4, false), (4, 2, false), (1, 5, false), (5, 4, true) }, new[] { 0, 1, 2, -1 })
                .Problems("no west island", _ => true).Any(p => p.Contains("lands in the water")));
            // Planks between islands that are not neighbours.
            var far = new IslandLayout(spots.Take(6), new[] { (0, 2, false), (1, 5, false), (5, 4, true) }, doors);
            Assert.IsTrue(far.Problems("far", _ => true).Any(p => p.Contains("not neighbours")));
            // A trial reached without the gap-crosser*.
            var openTrial = new IslandLayout(spots, new[] { (0, 4, false), (4, 2, false), (1, 5, false), (5, 4, true), (4, 6, false) }, doors, 6);
            Assert.IsTrue(openTrial.Problems("open trial", _ => true).Any(p => p.Contains("trial island")));
        }

        [Test]
        public void IslandWater_IsSolidRects_CoveringExactlyTheWaterAreaLessTheIslandsAndPlanks()
        {
            for (ulong s = 0; s < 40; s++)
            {
                IslandLayout layout = Generated(s, s % 2 == 0);
                List<Rect> water = IslandScreen.WaterRects(layout);
                Rect area = IslandLayout.WaterArea;
                for (float x = area.xMin + 0.05f; x < area.xMax; x += 0.1f)
                    for (float y = area.yMin + 0.05f; y < area.yMax; y += 0.1f)
                    {
                        var p = new Vector2(x, y);
                        bool land = layout.Islands.Any(r => r.Contains(p)) || layout.Bridges.Any(b => b.Plank.Contains(p));
                        int inWater = water.Count(r => r.Contains(p));
                        Assert.AreEqual(land ? 0 : 1, inWater, $"layout {s} at {p}: {(land ? "land in the water" : "a hole in the water")}");
                    }
            }
        }

        [Test]
        public void PlankLights_AWalkedPlankLightsOnce_ABrokenSpanOnlyWhenMended_AndAMendedSpanStaysMended()
        {
            for (ulong s = 0; s < 40; s++)
            {
                IslandLayout layout = Generated(s, s % 2 == 0);
                var lights = new PlankLights(layout);
                Assert.AreEqual(layout.Bridges.Count, lights.Count);
                for (int k = 0; k < lights.Count; k++) Assert.AreEqual(PlankLight.Unlit, lights[k], $"layout {s}: plank {k} starts unlit");
                // Islands and the water light nothing.
                foreach (Rect island in layout.Islands) Assert.AreEqual(-1, lights.Walk(island.center), $"layout {s}: an island");
                Assert.AreEqual(-1, lights.Walk(IslandLayout.WaterArea.min + new Vector2(0.05f, 0.05f)), $"layout {s}: the water");
                for (int k = 0; k < layout.Bridges.Count; k++)
                {
                    IslandBridge b = layout.Bridges[k];
                    Assert.AreEqual(k, lights.PlankAt(b.Plank.center));
                    if (b.Broken)
                    {
                        // Its ends light nothing; the mend changes its colour, for good.
                        Vector2 end = b.Vertical ? new Vector2(b.Plank.center.x, b.Plank.yMin + 0.1f) : new Vector2(b.Plank.xMin + 0.1f, b.Plank.center.y);
                        Assert.AreEqual(-1, lights.Walk(end), $"layout {s}: a broken span's end");
                        Assert.AreEqual(PlankLight.Unlit, lights[k]);
                        Assert.IsTrue(lights.Mend(k));
                        Assert.AreEqual(PlankLight.Mended, lights[k]);
                        Assert.IsFalse(lights.Mend(k), "Mended already");
                        Assert.AreEqual(-1, lights.Walk(b.Plank.center), "Walking a mended span changes nothing");
                        Assert.AreEqual(PlankLight.Mended, lights[k]);
                        continue;
                    }
                    Assert.IsFalse(lights.Mend(k), "A whole plank is never mended");
                    Assert.AreEqual(k, lights.Walk(b.Plank.center), $"layout {s}: plank {k} lights on the first step");
                    Assert.AreEqual(PlankLight.Walked, lights[k]);
                    Assert.AreEqual(-1, lights.Walk(b.Plank.center), "Only the first step lights it");
                    Assert.AreEqual(PlankLight.Walked, lights[k]);
                }
            }
            Assert.AreNotEqual(IslandScreen.GlowFor(PlankLight.Walked), IslandScreen.GlowFor(PlankLight.Mended), "Walked and mended differ");
            Assert.AreEqual(0f, IslandScreen.GlowFor(PlankLight.Unlit).a, "Unlit has no glow");
        }

        [Test]
        public void IslandLayout_IsLone_MarksExactlyTheIslandsOfADoorwayThatJoinsNoOther_AndItsSignStandsOnItsIsland()
        {
            int withLookout = 0, mouthed = 0;
            for (ulong s = 0; s < 300; s++)
            {
                IslandLayout layout = Generated(s, s % 2 == 0);
                int[] all = layout.Components(true);
                for (int i = 0; i < layout.Islands.Count; i++)
                {
                    bool expected = layout.DoorsReaching(i, true).Count == 1 && (!layout.HasTrial || all[layout.TrialIsland] != all[i]);
                    Assert.AreEqual(expected, layout.IsLone(i), $"layout {s}: island {i}");
                }
                Facing[] loneDoors = Sides.Where(d => layout.IsLone(layout.DoorIsland(d))).ToArray();
                Assert.AreEqual(1, loneDoors.Length, $"layout {s}: one lone doorway");
                Facing side = loneDoors[0];
                int door = layout.DoorIsland(side);
                Vector2 sign = IslandScreen.SignSpot(layout, side);
                Assert.AreEqual(door, layout.IslandAt(sign), $"layout {s}: the sign stands on the lone doorway's island");
                Assert.IsFalse(layout.Bridges.Any(b => b.Plank.Contains(sign)), $"layout {s}: not on a plank");
                // The whole board is on the island, aside of the walk in, and half its width clear of every plank mouth.
                Rect board = IslandScreen.SignBoard(sign), land = layout.Islands[door];
                Assert.IsTrue(board.xMin >= land.xMin - 1e-4f && board.xMax <= land.xMax + 1e-4f && board.yMin >= land.yMin - 1e-4f &&
                              board.yMax <= land.yMax + 1e-4f, $"layout {s}: the board stands on the island");
                float off = side.IsHorizontal() ? Mathf.Abs(sign.y) : Mathf.Abs(sign.x);
                Assert.GreaterOrEqual(off, IslandScreen.SignOffWalk - 1e-4f, $"layout {s}: the sign stands aside of the walk in");
                List<Rect> mouths = IslandScreen.PlankMouths(layout, door);
                Assert.AreEqual(layout.Bridges.Count(b => b.Joins(door)), mouths.Count);
                foreach (Rect m in mouths)
                {
                    Assert.IsTrue(Mathf.Abs(m.xMin - land.xMin) < 1e-4f || Mathf.Abs(m.xMax - land.xMax) < 1e-4f ||
                                  Mathf.Abs(m.yMin - land.yMin) < 1e-4f || Mathf.Abs(m.yMax - land.yMax) < 1e-4f, $"layout {s}: a mouth on the shore");
                    Assert.GreaterOrEqual(IslandScreen.RectGap(board, m), IslandScreen.SignBoardSize.x / 2f - 1e-4f,
                        $"layout {s}: the {side} sign's board keeps clear of the plank mouth {m}");
                }
                if (Enumerable.Range(0, layout.Islands.Count).Count(layout.IsLone) > 1) withLookout++;
                if (mouths.Count > 0) mouthed++;
                if (layout.HasTrial) Assert.IsFalse(layout.IsLone(layout.TrialIsland), $"layout {s}: the trial island is never lone");
            }
            Assert.Greater(withLookout, 25, "Some lone doorways have a lookout island");
            Assert.Greater(mouthed, 100, "Many signs share their island with a plank mouth");
            // With a single doorway there is no lone doorway (it is just the way in).
            IslandLayout single = IslandLayout.Generate(new SeededRng(1, MathPlan.RngStream), d => d == Facing.South, false);
            if (single != null) Assert.IsFalse(single.IsLone(single.DoorIsland(Facing.South)));
        }

        [Test]
        public void PickupPopup_TheLabelFitsTheLoneDoorwayLine_OnOneLine_AndShortLinesKeepTheOldSize()
        {
            var style = new GUIStyle
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), fontSize = 20, fontStyle = FontStyle.Bold, wordWrap = false
            };
            Vector2 text = style.CalcSize(new GUIContent(IslandScreen.LoneText));
            Assert.Greater(text.x, 100f, "The line measures");
            Vector2 label = PickupPopup.LabelSize(text);
            Assert.GreaterOrEqual(label.x, text.x + 8f, $"The label ({label.x}) fits the line ({text.x})");
            Assert.GreaterOrEqual(label.y, text.y);
            Assert.AreEqual(PickupPopup.MinLabelSize, PickupPopup.LabelSize(new Vector2(40f, 20f)));
        }

        [Test]
        public void IslandShore_RunsAlongEveryEdgeThatMeetsTheWater_OnTheIslandItself_AndNeverAlongAWall()
        {
            Rect area = IslandLayout.WaterArea;
            for (ulong s = 0; s < 40; s++)
            {
                IslandLayout layout = Generated(s, s % 2 == 0);
                for (int i = 0; i < layout.Islands.Count; i++)
                {
                    Rect island = layout.Islands[i];
                    List<(Rect Rect, bool Foam)> shore = IslandScreen.ShoreRects(island);
                    foreach ((Rect r, bool _) in shore)
                        Assert.IsTrue(r.xMin >= island.xMin - 1e-4f && r.xMax <= island.xMax + 1e-4f && r.yMin >= island.yMin - 1e-4f &&
                                      r.yMax <= island.yMax + 1e-4f, $"layout {s}: island {i}'s shore lies on the island");
                    // Every edge in the water has foam on it; an edge on the wall has none.
                    var edges = new[]
                    {
                        (new Vector2(island.center.x, island.yMin + 0.01f), island.yMin > area.yMin + 1e-4f),
                        (new Vector2(island.center.x, island.yMax - 0.01f), island.yMax < area.yMax - 1e-4f),
                        (new Vector2(island.xMin + 0.01f, island.center.y), island.xMin > area.xMin + 1e-4f),
                        (new Vector2(island.xMax - 0.01f, island.center.y), island.xMax < area.xMax - 1e-4f),
                    };
                    foreach ((Vector2 p, bool wet) in edges)
                        Assert.AreEqual(wet, shore.Any(e => e.Foam && e.Rect.Contains(p)), $"layout {s}: island {i} edge at {p}");
                    Assert.IsFalse(shore.Any(e => e.Rect.Contains(island.center)), "The island's middle is plain floor");
                }
            }
        }

        [Test]
        public void DoorwayReach_WalksDoorwayByDoorway_AnIslandScreenSplitsIntoItsIslands_AndEveryJoinIsTwoWay()
        {
            var model = new CubeModel(3);
            FaceId face = model.FaceOf(Theme.Math);
            var screen = new ScreenAddress(face, 1, 1);
            IslandLayout layout = Generated(5, true);
            var reach = new DoorwayReach(model, s => s == screen ? layout : null);
            Assert.IsEmpty(reach.Problems());
            HashSet<(ScreenAddress Screen, int Region)> bare = reach.Reachable(false), all = reach.Reachable(true);
            foreach (ScreenAddress s in model.AllScreens())
                if (s != screen) Assert.IsTrue(bare.Contains((s, 0)), $"{s} without the gap-crosser*");
            // Every doorway's island is reached from its own doorway; the trial only with the gap-crosser*.
            foreach (Facing side in Sides)
                Assert.IsTrue(bare.Contains((screen, layout.DoorIsland(side))), $"the {side} island, through its doorway");
            Assert.IsFalse(bare.Contains((screen, layout.TrialIsland)));
            Assert.IsTrue(all.Contains((screen, layout.TrialIsland)));
            // Within the screen, a doorway leads only to the islands its planks reach.
            int[] whole = layout.Components(false);
            foreach (Facing side in Sides)
            {
                int from = layout.DoorIsland(side);
                var walked = new HashSet<int> { from };
                var queue = new Queue<int>();
                queue.Enqueue(from);
                while (queue.Count > 0)
                    foreach ((ScreenAddress Screen, int Region) next in reach.Neighbours(screen, queue.Dequeue(), false))
                        if (next.Screen == screen && walked.Add(next.Region)) queue.Enqueue(next.Region);
                for (int i = 0; i < layout.Islands.Count; i++)
                    Assert.AreEqual(whole[i] == whole[from], walked.Contains(i), $"from the {side} doorway, island {i}");
            }
            // An unreachable island and a missing way back are both caught.
            var cutOff = new IslandLayout(new[] { IslandLayout.DoorSpot(Facing.North), IslandLayout.DoorSpot(Facing.East),
                IslandLayout.DoorSpot(Facing.South), IslandLayout.DoorSpot(Facing.West), new Vector2Int(1, 1) },
                new (int, int, bool)[0], new[] { 0, 1, 2, 3 });
            Assert.IsTrue(new DoorwayReach(model, s => s == screen ? cutOff : null).Problems().Any(p => p.Contains("can never be reached")));
        }

        // ---------- The plan ----------

        [Test]
        public void Plan_IsDeterministic_OnItsOwnStream_AndHasNoProblems_OnEverySeed()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                MathPlan a = Plan(seed, library, catalog, out CubeModel model, out ItemPlacement placement, out _);
                MathPlan b = Plan(seed, library, catalog, out _, out _, out _);
                Assert.AreEqual(a.Signature(), b.Signature(), $"seed {seed}");
                List<string> problems = a.Problems(model, placement);
                Assert.IsEmpty(problems, $"seed {seed}: {string.Join("; ", problems)}");
            }
            Assert.AreEqual(17UL, MathPlan.RngStream);
            Assert.AreEqual(21UL, CubeLayout.IslandRngStream);
        }

        [Test]
        public void Plan_EveryMathGateIsAGap_TwoIslandScreensApart_NoGateOnThem_AndOneHoldsTheTrial([ValueSource(nameof(FaceSizes))] int n)
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            int offFace = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                MathPlan plan = Plan(seed, library, catalog, out CubeModel model, out ItemPlacement placement, out var moduleAt, n);
                Assert.AreEqual(placement.Gates.Count(g => g.Item == Theme.Math), plan.Gates.Count, $"seed {seed}");
                Assert.IsTrue(plan.Gates.All(g => plan.GateLook(g.Screen, g.Slot, false) == GateLooks.Gap));
                offFace += plan.Gates.Count(g => !g.OnHome);
                Assert.AreEqual(CubeLayout.IslandScreensPerFace(n), plan.IslandScreens.Count, $"seed {seed}");
                foreach (IslandScreenSpec a in plan.IslandScreens)
                {
                    Assert.IsTrue(moduleAt(a.Screen).IsIslandScreen);
                    Assert.IsEmpty(moduleAt(a.Screen).Gates, "an island screen has no gate slot");
                    Assert.IsFalse(placement.Gates.Any(g => g.Screen == a.Screen) || placement.OptionalGates.Any(g => g.Screen == a.Screen));
                    foreach (IslandScreenSpec b in plan.IslandScreens)
                        Assert.AreNotEqual(1, Mathf.Abs(a.Screen.Cell.x - b.Screen.Cell.x) + Mathf.Abs(a.Screen.Cell.y - b.Screen.Cell.y),
                            $"seed {seed}: island screens side by side");
                }
                Assert.AreEqual(1, plan.IslandScreens.Count(s => s.HasTrial), $"seed {seed}: one trial");
                Assert.IsEmpty(plan.Problems(model, placement), $"seed {seed} at {n}x{n}");
            }
            Assert.GreaterOrEqual(offFace, Seeds, "The gap-crosser* opens a gate on another face every run");
        }

        private static readonly int[] FaceSizes = { 2, 3 };

        [Test]
        public void SweepRules_CatchAFullyJoinedScreen_AnIslandNobodyReaches_AMissingTrial_AndAPickupOnAnIslandOutOfReach()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            MathPlan plan = Plan(4, library, catalog, out CubeModel model, out ItemPlacement placement, out _);
            Assert.IsEmpty(plan.Problems(model, placement));
            int trialIndex = plan.IslandScreens.ToList().FindIndex(s => s.HasTrial);
            IslandLayout trial = plan.IslandScreens[trialIndex].Layout;

            // A layout whose islands all join.
            var spots = trial.Spots.ToList();
            var links = trial.Bridges.Select(b => (b.A, b.B, b.Broken)).ToList();
            int[] doors = Sides.Select(trial.DoorIsland).ToArray();
            var allJoined = new IslandLayout(spots, links.Select(l => (l.A, l.B, false)), doors, -1);
            List<string> problems = plan.WithIslands(trialIndex, allJoined).Problems(model, placement);
            Assert.IsTrue(problems.Any(p => p.Contains("trial")), string.Join("; ", problems));
            Assert.IsTrue(problems.Any(p => p.Contains("broken span") || p.Contains("fully connected")), string.Join("; ", problems));

            // No layout at all.
            problems = plan.WithIslands(trialIndex, null).Problems(model, placement);
            Assert.IsTrue(problems.Any(p => p.Contains("no island layout")), string.Join("; ", problems));

            // An island nobody reaches (no planks, and not a doorway's).
            var stray = new List<Vector2Int>(spots);
            Vector2Int free = Enumerable.Range(0, 5).SelectMany(x => Enumerable.Range(0, 3).Select(y => new Vector2Int(x, y)))
                .First(p => !spots.Contains(p) && IslandLayout.DoorAt(p) == null);
            stray.Add(free);
            problems = plan.WithIslands(trialIndex, new IslandLayout(stray, links, doors, trial.TrialIsland)).Problems(model, placement);
            Assert.IsTrue(problems.Any(p => p.Contains("can never be reached") || p.Contains("joins no doorway")), string.Join("; ", problems));

            // The Math pickup on an island screen's south island cut off from its own doorway: a softlock.
            IslandScreenSpec any = plan.IslandScreens[0];
            var onIsland = new ItemPlacement(placement.Seed, placement.Gates,
                placement.Pickups.Select(p => p.Item == Theme.Math ? new PickupPlacement(Theme.Math, any.Screen) : p).ToList(),
                placement.OptionalGates, placement.RolledVariants.ToDictionary(e => e.Key, e => e.Value));
            Assert.IsEmpty(plan.Problems(model, onIsland), "On the south doorway's island the pickup is reached through that doorway");
            Assert.IsTrue(new DoorwayReach(model, plan.IslandAt).CanReach(any.Screen, CubeWorld.OpenPickupOffset, false));
            var lost = Lost(any.Layout);
            problems = plan.WithIslands(0, lost).Problems(model, onIsland);
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
            StringAssert.Contains("island screens never fully joined", report);
            StringAssert.Contains("every item collected doorway by doorway", report);
            StringAssert.Contains("the gap-crosser* opening a gate on another face", report);
            StringAssert.Contains("the Math trial island reachable", report);
        }

        [Test]
        public void TheSweepsItemCollection_GoesDoorwayByDoorway_OnIslandScreens()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            MathPlan plan = Plan(4, library, catalog, out CubeModel model, out ItemPlacement placement, out _);
            IslandScreenSpec any = plan.IslandScreens[0];
            var onIsland = new ItemPlacement(placement.Seed, placement.Gates,
                placement.Pickups.Select(p => p.Item == Theme.Math ? new PickupPlacement(Theme.Math, any.Screen) : p).ToList(),
                placement.OptionalGates, placement.RolledVariants.ToDictionary(e => e.Key, e => e.Value));
            List<Theme> items = placement.Pickups.Select(p => p.Item).ToList();
            Assert.IsEmpty(SeedSweep.FindItemProblems(model, onIsland, items, plan.IslandAt));
            // With the south doorway's island cut off from the south doorway, the pickup is out of reach.
            var lost = Lost(any.Layout);
            List<string> problems = SeedSweep.FindItemProblems(model, onIsland, items, s => s == any.Screen ? lost : plan.IslandAt(s));
            Assert.IsTrue(problems.Any(p => p.Contains("softlock") && p.Contains("Math")), string.Join("; ", problems));
        }

        /// <summary>A layout whose south doorway's island is cut off: no doorway lands on it and no plank reaches it.</summary>
        private static IslandLayout Lost(IslandLayout layout)
        {
            int south = layout.DoorIsland(Facing.South);
            return new IslandLayout(layout.Spots, layout.Bridges.Where(b => !b.Joins(south)).Select(b => (b.A, b.B, b.Broken)),
                Sides.Select(d => d == Facing.South ? -1 : layout.DoorIsland(d)).ToArray(), layout.TrialIsland);
        }

        // ---------- Spots ----------

        [Test]
        public void PhysicsKeepsItsBouldersOffMathsGates_AndNothingElseStandsOnAnIslandScreen()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                MathPlan plan = Plan(seed, library, catalog, out CubeModel model, out ItemPlacement placement, out var moduleAt);
                PhysicsPlan physics = PhysicsPlan.Create(model, placement, moduleAt, BiologyBeaks.VariantKinds(catalog),
                    PhysicsPlan.DefaultMaxRequired, ChemistryIsotope.VariantKinds(catalog));
                Dictionary<ScreenAddress, List<ScreenMark>> marks = plan.ContentMarks();
                foreach (TimedDoorSpec door in physics.Doors)
                {
                    Assert.IsFalse(plan.IsIslandScreen(door.Screen), $"seed {seed}: a timed door on an island screen");
                    if (!marks.TryGetValue(door.Screen, out List<ScreenMark> list)) continue;
                    foreach (ScreenMark m in list)
                        foreach (Vector2 b in door.Boulders)
                            Assert.GreaterOrEqual(Vector2.Distance(b, m.Local), PhysicsPlan.Gap(Boulder.Radius, m.Radius) - 1e-3f,
                                $"seed {seed}: a boulder of {door} sits on {m.What}");
                }
                // An island screen holds no gate, hidden item or core entrance; an open pickup there stands on its south island.
                foreach (IslandScreenSpec s in plan.IslandScreens)
                {
                    ScreenModule module = moduleAt(s.Screen);
                    Assert.IsEmpty(module.Gates);
                    Assert.IsEmpty(module.HiddenItems);
                    Assert.IsEmpty(module.CoreEntrances);
                    foreach (PickupPlacement p in placement.Pickups.Where(p => p.Screen == s.Screen))
                        Assert.AreEqual(s.Layout.DoorIsland(Facing.South), s.Layout.IslandAt(CubeWorld.OpenPickupOffset), $"seed {seed}: {p}");
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
        public void ShapeLines_ExplainTheIslandsAndTheGapCrosser_TheRestTheBridgekeeper_AndEndWithATrueHint()
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
                    if (i == MathPopulation.IslandTeller) Assert.AreEqual(Dialogue.IslandLine, lines[0]);
                    else if (i == MathPopulation.GapTeller) Assert.AreEqual(Dialogue.GapCrosserLine, lines[0]);
                    else CollectionAssert.Contains(Dialogue.BridgekeeperLines, lines[0]);
                    Assert.AreEqual(HintGenerator.For(spec, facts, HintDensity.Full)[0].Text, lines[1]);
                }
            }
            StringAssert.Contains("doorway", Dialogue.IslandLine);
            StringAssert.Contains("round", Dialogue.IslandLine);
            StringAssert.Contains(GapCrosserItem.Name, Dialogue.GapCrosserLine);
        }

        // ---------- Text ----------

        private static List<string> MathStrings()
        {
            var strings = new List<string>(FaceText.Flavour(Race.Shape));
            strings.AddRange(Dialogue.BridgekeeperLines);
            strings.Add(Dialogue.IslandLine);
            strings.Add(Dialogue.GapCrosserLine);
            strings.Add(IslandScreen.MendedText);
            strings.Add(IslandScreen.LoneText);
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
            foreach (string name in FaceText.PartNames(Race.Shape)) yield return name;
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

            // The old bridge puzzle is gone from the words too (1.25): nothing echoes walking every bridge once.
            CollectionAssert.IsNotEmpty(InWorldText.BannedIn("Cross every bridge exactly once, like the seven bridges of Königsberg."));
            foreach (string text in strings.Concat(MathNames()).Concat(hints))
            {
                string lower = text.ToLowerInvariant();
                if (strings.Contains(text)) StringAssert.DoesNotContain("once", lower, $"\"{text}\" echoes the old walk-every-plank-once puzzle");
                CollectionAssert.IsEmpty(InWorldText.BannedIn(text), $"\"{text}\" uses a banned word"); // the shared list (InWorldText.BannedWords)
                StringAssert.DoesNotContain(Dialogue.ForbiddenWord, lower, text);
            }

            // Proper names: a capitalised word that does not start a sentence is a name, and names carry a star, except the
            // chosen character names (4.4: NpcNames), by the shared rule.
            foreach (string text in strings)
                CollectionAssert.IsEmpty(InWorldText.UnstarredNames(text), $"A name without its star in \"{text}\"");

            // Placeholder names carry a trailing star wherever they appear.
            StringAssert.EndsWith("*", GapCrosserItem.Name);
            foreach (string text in strings)
                foreach (string bare in new[] { "Gap-Crosser" })
                {
                    int at = text.IndexOf(bare, StringComparison.Ordinal);
                    while (at >= 0)
                    {
                        int after = at + bare.Length;
                        Assert.IsTrue(after < text.Length && text[after] == '*', $"\"{bare}\" without its star in \"{text}\"");
                        at = text.IndexOf(bare, after, StringComparison.Ordinal);
                    }
                }
            StringAssert.Contains(NpcNames.ShapePatron, string.Join(" ", Dialogue.BridgekeeperLines));
        }
    }
}
