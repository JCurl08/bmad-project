using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Science layout (CubeLayout) and module library invariants: determinism, variety, exactly one core
    /// entrance per built face, no modules for Town or sealed faces, and every module keeps four open exits.
    /// </summary>
    public class CubeLayoutTests
    {
        private const string LibraryPath = "Assets/Modules/ModuleLibrary.asset";
        private static readonly int[] FaceSizes = { 2, 3 };
        private static readonly FaceId[] Faces = (FaceId[])Enum.GetValues(typeof(FaceId));
        private static readonly Theme[] BuiltSciences = { Theme.Biology, Theme.Chemistry, Theme.Physics, Theme.Math, Theme.EarthAtmosphere };

        /// <summary>Four modules per theme; modules 0 and 2 have one and two core slots.</summary>
        private sealed class FakeCatalog : IModuleCatalog
        {
            private readonly int[] coreSlots;
            public FakeCatalog(params int[] coreSlots) => this.coreSlots = coreSlots;
            public int PoolSize(Theme theme) => coreSlots.Length;
            public int CoreSlotCount(Theme theme, int moduleIndex) => coreSlots[moduleIndex];
        }

        private static readonly FakeCatalog Fake = new FakeCatalog(1, 0, 2, 0);

        private static ModuleLibrary LoadLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<ModuleLibrary>(LibraryPath);
            Assert.IsNotNull(library, $"{LibraryPath} missing: run Cube > Build Module Library");
            return library;
        }

        // ---------- Layout ----------

        [Test]
        public void SameSeed_GivesIdenticalLayout([ValueSource(nameof(FaceSizes))] int n)
        {
            foreach (IModuleCatalog catalog in new IModuleCatalog[] { Fake, LoadLibrary() })
            {
                string a = CubeLayout.Generate(new CubeModel(1234, n), catalog).Signature();
                string b = CubeLayout.Generate(new CubeModel(1234, n), catalog).Signature();
                Assert.IsNotEmpty(a);
                Assert.AreEqual(a, b);
            }
        }

        [Test]
        public void SameSeed_GivesSameThemesAsTheModel()
        {
            // The layout reads themes from the model and uses its own RNG stream: placement is untouched.
            var model = new CubeModel(1234);
            var before = Faces.Select(model.ThemeOf).ToList();
            CubeLayout layout = CubeLayout.Generate(model, Fake);
            CollectionAssert.AreEqual(before, Faces.Select(model.ThemeOf).ToList());
            foreach (KeyValuePair<FaceId, FaceLayout> pair in layout.Faces)
                Assert.AreEqual(model.ThemeOf(pair.Key), pair.Value.Theme);
        }

        [Test]
        public void DifferentSeeds_GiveAtLeastTwoModuleLayouts()
        {
            foreach (IModuleCatalog catalog in new IModuleCatalog[] { Fake, LoadLibrary() })
            {
                var layouts = new HashSet<string>();
                var moduleOnly = new HashSet<string>();
                for (int seed = 1; seed <= 50; seed++)
                {
                    var model = new CubeModel(seed);
                    CubeLayout layout = CubeLayout.Generate(model, catalog);
                    layouts.Add(layout.Signature());
                    // Same face/theme placement but different modules also counts: compare per theme.
                    moduleOnly.Add(string.Join(",", BuiltSciences.Select(t =>
                    {
                        FaceLayout fl = layout.Faces[model.FaceOf(t)];
                        return string.Join(" ", Enumerable.Range(0, 4).Select(i => fl.ModuleAt(new Vector2Int(i % 2, i / 2))));
                    })));
                }
                Assert.GreaterOrEqual(layouts.Count, 2);
                Assert.GreaterOrEqual(moduleOnly.Count, 2, "Module choice never varies with the seed");
            }
        }

        [Test]
        public void EveryBuiltFaceHasExactlyOneReachableCoreEntrance_AndOnlyBuiltSciencesAreLaidOut(
            [ValueSource(nameof(FaceSizes))] int n)
        {
            foreach (IModuleCatalog catalog in new IModuleCatalog[] { Fake, LoadLibrary() })
            {
                for (int seed = 1; seed <= 50; seed++)
                {
                    var model = new CubeModel(seed, n);
                    CubeLayout layout = CubeLayout.Generate(model, catalog);

                    var expected = Faces.Where(f => f != CubeModel.StartFace && !model.IsSealed(f)).ToList();
                    CollectionAssert.AreEquivalent(expected, layout.Faces.Keys, $"seed {seed}");
                    Assert.AreEqual(5, layout.Faces.Count, $"seed {seed}: every science face is built");
                    Assert.IsFalse(layout.TryGetFace(CubeModel.StartFace, out _), "Town uses its fixed modules");
                    foreach (FaceId face in Faces.Where(model.IsSealed))
                        Assert.IsFalse(layout.TryGetFace(face, out _), $"seed {seed}: sealed {face} got modules");

                    // The sealed-face path still works: a face sealed on purpose gets no modules.
                    var sealedModel = new CubeModel(seed, n, new[] { Theme.EarthAtmosphere });
                    CubeLayout sealedLayout = CubeLayout.Generate(sealedModel, catalog);
                    Assert.AreEqual(4, sealedLayout.Faces.Count, $"seed {seed}: sealed Earth is not laid out");
                    Assert.IsFalse(sealedLayout.TryGetFace(sealedModel.FaceOf(Theme.EarthAtmosphere), out _));

                    foreach (FaceLayout fl in layout.Faces.Values)
                    {
                        Assert.IsTrue(model.IsInside(fl.CoreCell));
                        int coreModule = fl.ModuleAt(fl.CoreCell);
                        Assert.That(fl.CoreSlot, Is.InRange(0, catalog.CoreSlotCount(fl.Theme, coreModule) - 1));
                        for (int y = 0; y < n; y++)
                            for (int x = 0; x < n; x++)
                                Assert.That(fl.ModuleAt(new Vector2Int(x, y)), Is.InRange(0, catalog.PoolSize(fl.Theme) - 1));
                    }

                    List<string> problems = SeedSweep.FindCoreProblems(model, layout, catalog);
                    Assert.IsEmpty(problems, $"seed {seed}: {string.Join("; ", problems)}");
                }
            }
        }

        [Test]
        public void CoreSlotChoice_UsesEverySlotOfAMultiSlotModule()
        {
            var seen = new HashSet<int>();
            for (int seed = 1; seed <= 200; seed++)
            {
                CubeLayout layout = CubeLayout.Generate(new CubeModel(seed), Fake);
                foreach (FaceLayout fl in layout.Faces.Values)
                    if (fl.ModuleAt(fl.CoreCell) == 2) seen.Add(fl.CoreSlot);
            }
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, seen);
        }

        [Test]
        public void MissingCoreModule_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => CubeLayout.Generate(new CubeModel(1), new FakeCatalog(0, 0)));
            Assert.Throws<InvalidOperationException>(() => CubeLayout.Generate(new CubeModel(1), new FakeCatalog()));
        }

        // ---------- Sweep ----------

        [Test]
        public void SeedSweep_WithLibrary_AllSeedsPassCoreCheck([ValueSource(nameof(FaceSizes))] int n)
        {
            List<SeedSweep.SeedResult> results = SeedSweep.Run(1, 50, n, LoadLibrary());
            Assert.AreEqual(50, results.Count);
            Assert.IsTrue(results.All(r => r.CoreChecked));
            Assert.IsTrue(results.All(r => r.Passed), SeedSweep.Describe(results));
            StringAssert.Contains("50/50", SeedSweep.Describe(results));
            StringAssert.Contains("core entrance", SeedSweep.Describe(results));
        }

        [Test]
        public void SeedSweep_DescribeNamesCoreProblems()
        {
            var results = new List<SeedSweep.SeedResult>
            {
                new SeedSweep.SeedResult
                {
                    Seed = 9, Unreachable = new List<ScreenAddress>(),
                    CoreProblems = new List<string> { "Right (Biology) has 0 active core entrances" },
                },
            };
            string report = SeedSweep.Describe(results);
            StringAssert.Contains("0/1", report);
            StringAssert.Contains("Seed 9: core entrance Right (Biology)", report);
        }

        // ---------- Library invariants ----------

        private static IEnumerable<ScreenModule> AllLibraryModules(ModuleLibrary library) =>
            library.TownModules.Concat(library.SciencePools.SelectMany(p => p.Modules)).Where(m => !m.IsPuzzleScreen && !m.IsTownScreen);

        private static float RectDistance(Rect r, Vector2 p)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
            float dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        [Test]
        public void GateArchesAndEdgeWalls_StayClearOfEveryModulesPortals_GateSlotsAlcovesAndHiddenItems()
        {
            // Any screen side can be a face edge (with its gate arch), so every module is checked against all four arches and
            // every edge wall: a core portal's ring, a hidden Jumbles*, a gate slot and its alcove never meet them.
            ModuleLibrary library = LoadLibrary();
            float portal = CorePortal.Radius * 1.3f, hidden = 0.35f;
            var solids = new List<(string What, Rect Rect)>();
            foreach (Facing side in Enum.GetValues(typeof(Facing)))
            {
                solids.Add(($"{side} gate arch", ScreenEdges.ArchRect(side)));
                foreach (Rect w in ScreenEdges.WallRects(side)) solids.Add(($"{side} wall", w));
                // 4.1: the walls are half a tile; the arch (no collider) stands on the wall and reaches through the edge
                // band's clear strip, never past the band, so module content (kept out of the band) never meets it.
                Rect arch = ScreenEdges.ArchRect(side);
                Vector2 h = ScreenEdges.Half;
                float e = ScreenModule.EdgeClearance + 1e-4f;
                Rect band = side == Facing.North ? Rect.MinMaxRect(-h.x, h.y - e, h.x, h.y + 1e-4f)
                    : side == Facing.South ? Rect.MinMaxRect(-h.x, -h.y - 1e-4f, h.x, -h.y + e)
                    : side == Facing.East ? Rect.MinMaxRect(h.x - e, -h.y, h.x + 1e-4f, h.y)
                    : Rect.MinMaxRect(-h.x - 1e-4f, -h.y, -h.x + e, h.y);
                Assert.IsTrue(band.Contains(arch.min) && band.Contains(arch.max), $"{side}: the arch stays in the edge band");
                Assert.IsTrue(ScreenEdges.WallRects(side).Any(w => w.Overlaps(arch)), $"{side}: the arch stands on the wall");
            }
            foreach (ScreenModule module in AllLibraryModules(library).Concat(new[] { library.Pool(Theme.Math).Last() }))
            {
                Vector2 origin = module.transform.position;
                foreach ((string what, Rect rect) in solids)
                {
                    foreach (CoreEntranceSlot c in module.CoreEntrances)
                        Assert.GreaterOrEqual(RectDistance(rect, (Vector2)c.transform.position - origin), portal,
                            $"{module.name}: a core portal meets the {what}");
                    foreach (HiddenItemSlot h in module.HiddenItems)
                        Assert.GreaterOrEqual(RectDistance(rect, (Vector2)h.transform.position - origin), hidden,
                            $"{module.name}: a hidden item meets the {what}");
                    foreach (GateSlot g in module.Gates)
                    {
                        Vector2 slot = (Vector2)g.transform.position - origin;
                        Assert.Greater(RectDistance(rect, slot), GateSlot.GateWidth / 2f, $"{module.name}: a gate slot meets the {what}");
                        Assert.Greater(RectDistance(rect, slot + g.PocketOffset), 0.5f, $"{module.name}: a gate's pocket meets the {what}");
                    }
                    Transform alcoves = module.transform.Find("Alcoves");
                    if (alcoves == null) continue;
                    foreach (BoxCollider2D box in alcoves.GetComponentsInChildren<BoxCollider2D>(true))
                    {
                        var r = new Rect((Vector2)box.transform.position - origin + box.offset - box.size / 2f, box.size);
                        Assert.IsFalse(r.Overlaps(rect), $"{module.name}: an alcove wall meets the {what}");
                    }
                }
            }
        }

        [Test]
        public void Library_MathEndsWithItsIslandModule_OnlyExits_AndTheLayoutGivesItToTwoScreensApart_NeverTheCore()
        {
            ModuleLibrary library = LoadLibrary();
            IReadOnlyList<ScreenModule> math = library.Pool(Theme.Math);
            Assert.AreEqual(math.Count - 1, library.IslandModuleIndex(Theme.Math), "Math's island module ends its pool");
            ScreenModule island = math[math.Count - 1];
            Assert.IsTrue(island.IsIslandScreen);
            Assert.AreEqual(4, island.Exits.Length);
            Assert.IsEmpty(island.Gates);
            Assert.IsEmpty(island.HiddenItems);
            Assert.IsEmpty(island.CoreEntrances);
            Assert.IsEmpty(island.GetComponentsInChildren<BoxCollider2D>(true), "No obstacles: MathFace fills it");
            // 4.10: Earth ends its pool with its ice module the same way (only exits).
            IReadOnlyList<ScreenModule> earth = library.Pool(Theme.EarthAtmosphere);
            Assert.AreEqual(earth.Count - 1, library.IslandModuleIndex(Theme.EarthAtmosphere), "Earth's ice module ends its pool");
            ScreenModule ice = earth[earth.Count - 1];
            Assert.IsTrue(ice.IsIceScreen && !ice.IsIslandScreen);
            Assert.AreEqual(4, ice.Exits.Length);
            Assert.IsEmpty(ice.Gates);
            Assert.IsEmpty(ice.HiddenItems);
            Assert.IsEmpty(ice.CoreEntrances);
            // 4.11: and its wind module just before the ice (only exits too).
            Assert.AreEqual(earth.Count - 2, library.WindModuleIndex(Theme.EarthAtmosphere), "Earth's wind module comes just before its ice");
            ScreenModule windModule = earth[earth.Count - 2];
            Assert.IsTrue(windModule.IsWindScreen && !windModule.IsIceScreen && windModule.IsPuzzleScreen);
            Assert.AreEqual(4, windModule.Exits.Length);
            Assert.IsEmpty(windModule.Gates);
            Assert.IsEmpty(windModule.HiddenItems);
            Assert.IsEmpty(windModule.CoreEntrances);
            foreach (Theme theme in BuiltSciences.Where(t => t != Theme.Math && t != Theme.EarthAtmosphere))
                Assert.AreEqual(-1, library.IslandModuleIndex(theme), $"{theme} has no island screens");
            foreach (Theme theme in BuiltSciences.Where(t => t != Theme.EarthAtmosphere))
                Assert.AreEqual(-1, library.WindModuleIndex(theme), $"{theme} has no wind screens");
            foreach (int n in FaceSizes)
                for (int seed = 1; seed <= 50; seed++)
                {
                    var model = new CubeModel(seed, n);
                    CubeLayout layout = CubeLayout.Generate(model, library);
                    foreach (FaceLayout fl in layout.Faces.Values)
                    {
                        var cells = new List<Vector2Int>();
                        for (int y = 0; y < n; y++)
                            for (int x = 0; x < n; x++)
                                if (fl.ModuleAt(new Vector2Int(x, y)) == library.IslandModuleIndex(fl.Theme)) cells.Add(new Vector2Int(x, y));
                        CollectionAssert.AreEqual(fl.IslandCells, cells, $"seed {seed} {fl.Face}");
                        Assert.AreEqual(fl.Theme == Theme.Math || fl.Theme == Theme.EarthAtmosphere ? CubeLayout.IslandScreensPerFace(n) : 0, cells.Count, $"seed {seed} {fl.Face}");
                        CollectionAssert.DoesNotContain(cells, fl.CoreCell);
                        // 4.11: the wind cells hold the wind module, never the core or the town.
                        var winds = new List<Vector2Int>();
                        for (int y = 0; y < n; y++)
                            for (int x = 0; x < n; x++)
                                if (fl.ModuleAt(new Vector2Int(x, y)) == library.WindModuleIndex(fl.Theme)) winds.Add(new Vector2Int(x, y));
                        CollectionAssert.AreEqual(fl.WindCells, winds, $"seed {seed} {fl.Face}");
                        Assert.AreEqual(fl.Theme == Theme.EarthAtmosphere ? CubeLayout.WindScreensPerFace(n) : 0, winds.Count, $"seed {seed} {fl.Face}");
                        CollectionAssert.DoesNotContain(winds, fl.CoreCell);
                        if (fl.TownCell.HasValue) CollectionAssert.DoesNotContain(winds, fl.TownCell.Value);
                        // No two puzzle screens (islands, ice, wind) side by side.
                        var puzzles = cells.Concat(winds).ToList();
                        foreach (Vector2Int a in puzzles)
                            foreach (Vector2Int b in puzzles)
                                Assert.AreNotEqual(1, Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y), $"seed {seed}: puzzle screens side by side");
                    }
                }
            Assert.AreEqual(2, CubeLayout.IslandScreensPerFace(3));
            Assert.AreEqual(1, CubeLayout.IslandScreensPerFace(2));
            Assert.AreEqual(1, CubeLayout.WindScreensPerFace(3), "one wind screen at 3x3 (beside two ice screens)");
            Assert.AreEqual(0, CubeLayout.WindScreensPerFace(2), "none on the town-less test faces");
        }

        [Test]
        public void Library_HasTownModulesForEveryCell_AndAPoolOfFourPerBuiltScience()
        {
            ModuleLibrary library = LoadLibrary();
            int n = CubeSettings.DefaultFaceSize;
            Assert.GreaterOrEqual(library.TownModules.Count, n * n);
            Assert.IsTrue(library.TownModules.All(m => m != null && m.Theme == Theme.Town));

            // Every Town cell gets its own module (no wrap at N), and no two of them share a layout.
            var cellModules = new List<ScreenModule>();
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    cellModules.Add(library.TownModule(new Vector2Int(x, y), n));
            Assert.AreEqual(n * n, cellModules.Distinct().Count(), "Each Town cell has a distinct module");
            Assert.AreEqual(n * n, cellModules.Select(TownLayoutKey).Distinct().Count(), "Each Town module has its own layout");
            foreach (Theme theme in BuiltSciences)
            {
                IReadOnlyList<ScreenModule> pool = library.Pool(theme);
                Assert.GreaterOrEqual(pool.Count, 4, $"{theme} pool");
                Assert.IsTrue(pool.All(m => m != null && m.Theme == theme), $"{theme} pool has a wrong-theme module");
                Assert.IsTrue(pool.Any(m => m.CoreEntranceCount > 0), $"{theme} needs a module with a core entrance");
            }
            Assert.IsTrue(library.TownModules.All(m => m.CoreEntranceCount == 0), "Town has no core entrance");
        }

        /// <summary>A Town module's layout: its walls (solid colliders and alcoves) as rounded rects.</summary>
        private static string TownLayoutKey(ScreenModule module) =>
            string.Join(";", TownPlan.BlockedRects(module).Select(r => $"{r.xMin:0.00},{r.yMin:0.00},{r.width:0.00},{r.height:0.00}")
                .OrderBy(s => s, StringComparer.Ordinal));

        [Test]
        public void Library_EveryModuleHasFourOpenExits_GatesAndOneHiddenItem()
        {
            ModuleLibrary library = LoadLibrary();
            Vector2 half = CubeWorld.ScreenSize / 2f;
            foreach (ScreenModule module in AllLibraryModules(library))
            {
                string name = module.name;
                ExitSlot[] exits = module.Exits;
                Assert.AreEqual(4, exits.Length, $"{name}: exits");
                CollectionAssert.AreEquivalent(Enum.GetValues(typeof(Facing)), exits.Select(e => e.Facing), $"{name}: one exit per side");
                foreach (ExitSlot exit in exits)
                {
                    // The exit marker sits on its own side, in the centre lane.
                    Vector2 p = exit.transform.localPosition;
                    Vector2 d = exit.Facing.ToVector();
                    Assert.Greater(Vector2.Dot(p, d), (exit.Facing.IsHorizontal() ? half.x : half.y) - ScreenModule.EdgeClearance,
                        $"{name}: {exit.Label} not at its edge");
                }
                Assert.That(module.Gates.Length, Is.InRange(1, 2), $"{name}: gate slots");
                Assert.AreEqual(1, module.HiddenItems.Length, $"{name}: hidden-item slots");

                // Obstacles stay out of the edge band and both centre lanes, so all four exits stay open.
                var obstacles = module.GetComponentsInChildren<BoxCollider2D>(true);
                Assert.Greater(obstacles.Length, 0, $"{name}: needs interior obstacles");
                foreach (BoxCollider2D box in obstacles)
                {
                    var rect = new Rect((Vector2)box.transform.localPosition + box.offset - box.size / 2f, box.size);
                    Assert.IsTrue(ScreenModule.KeepsExitsOpen(rect), $"{name}: {box.name} blocks an exit lane or edge");
                    foreach (ModuleSlot slot in module.AllSlots)
                        Assert.IsFalse(rect.Contains(slot.transform.localPosition), $"{name}: {slot.Label} is inside {box.name}");
                }
            }
        }
    }
}
