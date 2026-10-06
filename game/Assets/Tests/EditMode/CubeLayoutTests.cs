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
        private static readonly Theme[] BuiltSciences = { Theme.Biology, Theme.Chemistry, Theme.Physics };

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
                    Assert.AreEqual(3, layout.Faces.Count, $"seed {seed}: Biology, Chemistry and Physics");
                    Assert.IsFalse(layout.TryGetFace(CubeModel.StartFace, out _), "Town uses its fixed modules");
                    foreach (FaceId face in Faces.Where(model.IsSealed))
                        Assert.IsFalse(layout.TryGetFace(face, out _), $"seed {seed}: sealed {face} got modules");

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
            library.TownModules.Concat(library.SciencePools.SelectMany(p => p.Modules));

        [Test]
        public void Library_HasTownModulesForEveryCell_AndAPoolOfFourPerBuiltScience()
        {
            ModuleLibrary library = LoadLibrary();
            int n = CubeSettings.DefaultFaceSize;
            Assert.GreaterOrEqual(library.TownModules.Count, n * n);
            Assert.IsTrue(library.TownModules.All(m => m != null && m.Theme == Theme.Town));
            foreach (Theme theme in BuiltSciences)
            {
                IReadOnlyList<ScreenModule> pool = library.Pool(theme);
                Assert.GreaterOrEqual(pool.Count, 4, $"{theme} pool");
                Assert.IsTrue(pool.All(m => m != null && m.Theme == theme), $"{theme} pool has a wrong-theme module");
                Assert.IsTrue(pool.Any(m => m.CoreEntranceCount > 0), $"{theme} needs a module with a core entrance");
            }
            Assert.IsTrue(library.TownModules.All(m => m.CoreEntranceCount == 0), "Town has no core entrance");
        }

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
