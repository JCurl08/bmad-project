using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Item and gate placement (ItemPlacement) and the item-collection sweep: determinism, gate counts on
    /// and off the home face, pickups on their home face, no softlock over 50 seeds, and hand-made
    /// softlocked placements the sweep must flag. Also the alcove and item-catalog asset invariants.
    /// </summary>
    public class ItemPlacementTests
    {
        private const string LibraryPath = "Assets/Modules/ModuleLibrary.asset";
        private const string ItemCatalogPath = "Assets/Items/ItemCatalog.asset";
        private static readonly int[] FaceSizes = { 2, 3 };
        private static readonly Theme[] BuiltSciences = { Theme.Biology, Theme.Chemistry, Theme.Physics };

        /// <summary>Four modules per theme with 1, 2, 1, 2 gate slots; modules 0 and 2 have core slots.</summary>
        private sealed class FakeCatalog : IModuleCatalog, IGateSlotCatalog
        {
            private readonly int[] coreSlots = { 1, 0, 2, 0 };
            private readonly int[] gateSlots;
            public FakeCatalog(params int[] gateSlots) => this.gateSlots = gateSlots;
            public int PoolSize(Theme theme) => coreSlots.Length;
            public int CoreSlotCount(Theme theme, int moduleIndex) => coreSlots[moduleIndex];
            public int GateSlotCount(Theme theme, int moduleIndex) => gateSlots[moduleIndex];
        }

        private static readonly FakeCatalog Fake = new FakeCatalog(1, 2, 1, 2);
        private static readonly FakeCatalog FakeOneSlot = new FakeCatalog(1, 1, 1, 1);

        private static ModuleLibrary LoadLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<ModuleLibrary>(LibraryPath);
            Assert.IsNotNull(library, $"{LibraryPath} missing: run Cube > Build Module Library");
            return library;
        }

        private static IEnumerable<IGateSlotCatalog> Catalogs() =>
            new IGateSlotCatalog[] { Fake, FakeOneSlot, LoadLibrary() };

        private static ItemPlacement Place(int seed, int n, IGateSlotCatalog catalog, out CubeModel model, out CubeLayout layout)
        {
            model = new CubeModel(seed, n);
            layout = CubeLayout.Generate(model, (IModuleCatalog)catalog);
            return ItemPlacement.Generate(model, layout, catalog);
        }

        // ---------- Placement ----------

        [Test]
        public void SameSeed_GivesIdenticalPlacement([ValueSource(nameof(FaceSizes))] int n)
        {
            foreach (IGateSlotCatalog catalog in Catalogs())
            {
                for (int seed = 1; seed <= 20; seed++)
                {
                    string a = Place(seed, n, catalog, out _, out _).Signature();
                    string b = Place(seed, n, catalog, out _, out _).Signature();
                    Assert.IsNotEmpty(a);
                    Assert.AreEqual(a, b, $"seed {seed}");
                }
            }
        }

        [Test]
        public void ItemListOrder_DoesNotChangeThePlacement()
        {
            var model = new CubeModel(77);
            CubeLayout layout = CubeLayout.Generate(model, Fake);
            string a = ItemPlacement.Generate(model, layout, Fake, BuiltSciences).Signature();
            string b = ItemPlacement.Generate(model, layout, Fake, BuiltSciences.Reverse()).Signature();
            Assert.AreEqual(a, b);
        }

        [Test]
        public void DifferentSeeds_GiveDifferentPlacements()
        {
            var seen = new HashSet<string>();
            var perTheme = new HashSet<string>();
            for (int seed = 1; seed <= 50; seed++)
            {
                ItemPlacement p = Place(seed, 2, LoadLibrary(), out CubeModel model, out _);
                seen.Add(p.Signature());
                // Relative to the home face, so it varies even if themes land on the same faces.
                perTheme.Add(string.Join(",", p.Pickups.Select(x => $"{x.Item}{x.Screen.Cell}{x.GuardSlot}")));
            }
            Assert.GreaterOrEqual(seen.Count, 2);
            Assert.GreaterOrEqual(perTheme.Count, 2, "Pickup placement never varies with the seed");
        }

        [Test]
        public void EveryItem_HasHomeAndOffHomeGates_AndItsPickupOnItsHomeFace([ValueSource(nameof(FaceSizes))] int n)
        {
            foreach (IGateSlotCatalog catalog in Catalogs())
            {
                for (int seed = 1; seed <= 50; seed++)
                {
                    ItemPlacement p = Place(seed, n, catalog, out CubeModel model, out CubeLayout layout);
                    string context = $"seed {seed}, N={n}, {catalog.GetType().Name}";
                    CollectionAssert.AreEquivalent(BuiltSciences, ItemPlacement.DefaultItems(layout), context);

                    foreach (Theme item in BuiltSciences)
                    {
                        FaceId home = model.FaceOf(item);
                        List<GatePlacement> gates = p.Gates.Where(g => g.Item == item).ToList();
                        int onHome = gates.Count(g => g.Screen.Face == home);
                        int offHome = gates.Count(g => g.Screen.Face != home && CubeLayout.IsLaidOut(model, g.Screen.Face));
                        Assert.GreaterOrEqual(onHome, 1, $"{context}: {item} home gates");
                        Assert.GreaterOrEqual(offHome, 1, $"{context}: {item} off-home gates");
                        Assert.GreaterOrEqual(onHome, offHome, $"{context}: {item} gates should be mostly on the home face");

                        List<PickupPlacement> pickups = p.Pickups.Where(x => x.Item == item).ToList();
                        Assert.AreEqual(1, pickups.Count, $"{context}: {item} pickups");
                        PickupPlacement pickup = pickups[0];
                        Assert.AreEqual(home, pickup.Screen.Face, $"{context}: {item} pickup off its home face");
                        Assert.IsTrue(model.IsInside(pickup.Screen.Cell));
                        if (pickup.IsGuarded)
                        {
                            Assert.IsTrue(p.TryGetGate(pickup.Screen, pickup.GuardSlot, out GatePlacement guard),
                                $"{context}: {pickup} has no guarding gate");
                            Assert.AreNotEqual(item, guard.Item, $"{context}: {item} pickup behind its own gate");
                        }
                    }

                    // Gates: only on laid-out faces, valid and distinct slots.
                    var used = new HashSet<(ScreenAddress, int)>();
                    foreach (GatePlacement g in p.Gates)
                    {
                        Assert.IsTrue(layout.TryGetFace(g.Screen.Face, out FaceLayout fl), $"{context}: {g} off the layout");
                        Assert.That(g.Slot, Is.InRange(0, catalog.GateSlotCount(fl.Theme, fl.ModuleAt(g.Screen.Cell)) - 1), $"{context}: {g}");
                        Assert.IsTrue(used.Add((g.Screen, g.Slot)), $"{context}: two gates in {g.Screen} #{g.Slot}");
                    }
                    // Each alcove holds at most one pickup.
                    var guarded = p.Pickups.Where(x => x.IsGuarded).Select(x => (x.Screen, x.GuardSlot)).ToList();
                    Assert.AreEqual(guarded.Count, guarded.Distinct().Count(), context);

                    List<string> problems = SeedSweep.FindItemProblems(model, p, BuiltSciences);
                    Assert.IsEmpty(problems, $"{context}: {string.Join("; ", problems)}");
                }
            }
        }

        [Test]
        public void SomeSeeds_PlaceAPickupBehindAnotherItemsGate()
        {
            // The ordering mechanic is used, not just allowed.
            int guarded = 0;
            for (int seed = 1; seed <= 50; seed++)
                guarded += Place(seed, 2, LoadLibrary(), out _, out _).Pickups.Count(x => x.IsGuarded);
            Assert.Greater(guarded, 0);
        }

        // ---------- Sweep ----------

        [Test]
        public void SeedSweep_WithLibrary_50SeedsNoSoftlocks([ValueSource(nameof(FaceSizes))] int n)
        {
            ItemCatalog catalog = LoadItemCatalog();
            List<SeedSweep.SeedResult> results = SeedSweep.Run(1, 50, n, LoadLibrary(), catalog.Themes(),
                catalog.VariantCounts(), BiologyBeaks.VariantKinds(catalog));
            string report = SeedSweep.Describe(results);
            Assert.AreEqual(50, results.Count);
            Assert.IsTrue(results.All(r => r.ItemsChecked), "The library knows gate slots, so items must be checked");
            Assert.IsTrue(results.All(r => r.Passed), report);
            StringAssert.Contains("50/50", report);
            StringAssert.Contains("no softlocks", report);
        }

        [Test]
        public void SweptPlacement_IsThePlayedPlacement()
        {
            // CubeWorld places items with ItemPlacement.ForRun over the catalog's themes and variant counts; the
            // sweep must check exactly that.
            ModuleLibrary library = LoadLibrary();
            ItemCatalog catalog = LoadItemCatalog();
            List<Theme> catalogThemes = catalog.Themes();
            Dictionary<Theme, int> counts = catalog.VariantCounts();
            Assert.IsNotEmpty(counts, "The catalog has the Biology beak variants");
            List<SeedSweep.SeedResult> results = SeedSweep.Run(1, 5, CubeSettings.DefaultFaceSize, library, catalogThemes, counts);
            foreach (SeedSweep.SeedResult r in results)
            {
                var model = new CubeModel(r.Seed);
                string played = ItemPlacement.ForRun(model, CubeLayout.Generate(model, library), library, catalogThemes, counts).Signature();
                Assert.IsNotNull(r.Placement, $"seed {r.Seed}");
                Assert.AreEqual(played, r.Placement.Signature(), $"seed {r.Seed}");
            }

            // A catalog that lacks an item changes the item list, and both sides follow it.
            var partial = new[] { Theme.Biology, Theme.Physics };
            SeedSweep.SeedResult one = SeedSweep.Run(7, 1, CubeSettings.DefaultFaceSize, library, partial, counts)[0];
            var m7 = new CubeModel(7);
            Assert.AreEqual(ItemPlacement.ForRun(m7, CubeLayout.Generate(m7, library), library, partial, counts).Signature(),
                one.Placement.Signature());
            Assert.IsFalse(one.Placement.Gates.Any(g => g.Item == Theme.Chemistry));
        }

        private static ItemCatalog LoadItemCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogPath);
            Assert.IsNotNull(catalog, $"{ItemCatalogPath} missing: run Cube > Build Item Catalog");
            return catalog;
        }

        /// <summary>A valid hand-made base: each item has a home and an off-home gate; pickups open.</summary>
        private static (CubeModel model, List<GatePlacement> gates, Dictionary<Theme, ScreenAddress> home) HandMadeBase()
        {
            var model = new CubeModel(1234);
            var gates = new List<GatePlacement>();
            var home = BuiltSciences.ToDictionary(t => t, t => new ScreenAddress(model.FaceOf(t), 0, 0));
            for (int i = 0; i < BuiltSciences.Length; i++)
            {
                Theme item = BuiltSciences[i];
                Theme other = BuiltSciences[(i + 1) % BuiltSciences.Length];
                gates.Add(new GatePlacement(new ScreenAddress(model.FaceOf(item), 1, 1), 0, item));
                gates.Add(new GatePlacement(new ScreenAddress(model.FaceOf(other), 1, 0), 0, item));
            }
            return (model, gates, home);
        }

        [Test]
        public void HandMadeValidPlacement_HasNoProblems()
        {
            (CubeModel model, List<GatePlacement> gates, Dictionary<Theme, ScreenAddress> home) = HandMadeBase();
            // Physics behind Chemistry's off-home gate on the Physics face: a valid order (Chemistry is open).
            const Theme physicsGuard = Theme.Chemistry;
            var pickups = new List<PickupPlacement>
            {
                new PickupPlacement(Theme.Biology, home[Theme.Biology]),
                new PickupPlacement(Theme.Chemistry, home[Theme.Chemistry]),
                new PickupPlacement(Theme.Physics, new ScreenAddress(model.FaceOf(Theme.Physics), 1, 0), 0),
            };
            var placement = new ItemPlacement(model.Seed, gates, pickups);
            Assert.IsTrue(placement.TryGetGate(pickups[2].Screen, 0, out GatePlacement guard));
            Assert.AreEqual(physicsGuard, guard.Item);
            Assert.IsEmpty(SeedSweep.FindItemProblems(model, placement, BuiltSciences));
        }

        [Test]
        public void SoftlockBehindOwnGate_IsFlagged_NamingSeedAndItem()
        {
            (CubeModel model, List<GatePlacement> gates, Dictionary<Theme, ScreenAddress> home) = HandMadeBase();
            // Biology's pickup sits behind Biology's own home gate.
            var pickups = new List<PickupPlacement>
            {
                new PickupPlacement(Theme.Biology, new ScreenAddress(model.FaceOf(Theme.Biology), 1, 1), 0),
                new PickupPlacement(Theme.Chemistry, home[Theme.Chemistry]),
                new PickupPlacement(Theme.Physics, home[Theme.Physics]),
            };
            List<string> problems = SeedSweep.FindItemProblems(model, new ItemPlacement(model.Seed, gates, pickups), BuiltSciences);
            Assert.AreEqual(1, problems.Count, string.Join("; ", problems));
            StringAssert.Contains("softlock", problems[0]);
            StringAssert.Contains("Biology", problems[0]);
            StringAssert.Contains("own gate", problems[0]);

            var result = new SeedSweep.SeedResult
            {
                Seed = model.Seed, Unreachable = new List<ScreenAddress>(), CoreProblems = new List<string>(),
                ItemProblems = problems,
            };
            Assert.IsFalse(result.Passed);
            string report = SeedSweep.Describe(new[] { result });
            StringAssert.Contains("0/1", report);
            StringAssert.Contains($"Seed {model.Seed}: items softlock: Biology", report);
        }

        [Test]
        public void SoftlockBehindAStuckGuard_NamesTheRealCause()
        {
            (CubeModel model, List<GatePlacement> gates, Dictionary<Theme, ScreenAddress> home) = HandMadeBase();
            // Biology is behind its own gate; Chemistry is behind Biology's off-home gate on the Chemistry face.
            var pickups = new List<PickupPlacement>
            {
                new PickupPlacement(Theme.Biology, new ScreenAddress(model.FaceOf(Theme.Biology), 1, 1), 0),
                new PickupPlacement(Theme.Chemistry, new ScreenAddress(model.FaceOf(Theme.Chemistry), 1, 0), 0),
                new PickupPlacement(Theme.Physics, home[Theme.Physics]),
            };
            var placement = new ItemPlacement(model.Seed, gates, pickups);
            Assert.IsTrue(placement.TryGetGate(pickups[1].Screen, 0, out GatePlacement guard));
            Assert.AreEqual(Theme.Biology, guard.Item);
            List<string> problems = SeedSweep.FindItemProblems(model, placement, BuiltSciences);
            string all = string.Join("; ", problems);
            Assert.AreEqual(2, problems.Count, all);
            Assert.IsTrue(problems.Any(p => p.Contains("softlock: Biology") && p.Contains("own gate")), all);
            Assert.IsTrue(problems.Any(p => p.Contains("softlock: Chemistry") && p.Contains("itself stuck")), all);
            Assert.IsFalse(problems.Any(p => p.Contains("cycle")), all);
        }

        [Test]
        public void SoftlockThroughACycle_IsFlagged()
        {
            var model = new CubeModel(1234);
            FaceId bio = model.FaceOf(Theme.Biology), chem = model.FaceOf(Theme.Chemistry), phys = model.FaceOf(Theme.Physics);
            var gates = new List<GatePlacement>
            {
                new GatePlacement(new ScreenAddress(bio, 0, 0), 0, Theme.Biology),
                new GatePlacement(new ScreenAddress(chem, 0, 1), 0, Theme.Biology),
                new GatePlacement(new ScreenAddress(chem, 0, 0), 0, Theme.Chemistry),
                new GatePlacement(new ScreenAddress(bio, 0, 1), 0, Theme.Chemistry),
                new GatePlacement(new ScreenAddress(phys, 0, 0), 0, Theme.Physics),
                new GatePlacement(new ScreenAddress(bio, 1, 1), 0, Theme.Physics),
            };
            // Biology behind a Chemistry gate, Chemistry behind a Biology gate: neither can ever be collected.
            var pickups = new List<PickupPlacement>
            {
                new PickupPlacement(Theme.Biology, new ScreenAddress(bio, 0, 1), 0),
                new PickupPlacement(Theme.Chemistry, new ScreenAddress(chem, 0, 1), 0),
                new PickupPlacement(Theme.Physics, new ScreenAddress(phys, 1, 1)),
            };
            List<string> problems = SeedSweep.FindItemProblems(model, new ItemPlacement(model.Seed, gates, pickups), BuiltSciences);
            Assert.AreEqual(2, problems.Count, string.Join("; ", problems));
            Assert.IsTrue(problems.Any(p => p.Contains("softlock: Biology") && p.Contains("cycle")), string.Join("; ", problems));
            Assert.IsTrue(problems.Any(p => p.Contains("softlock: Chemistry") && p.Contains("cycle")), string.Join("; ", problems));
        }

        [Test]
        public void MissingOffHomeGate_AndPickupOffHome_AreFlagged()
        {
            var model = new CubeModel(1234);
            FaceId bio = model.FaceOf(Theme.Biology), chem = model.FaceOf(Theme.Chemistry);
            var gates = new List<GatePlacement> { new GatePlacement(new ScreenAddress(bio, 0, 0), 0, Theme.Biology) };
            var pickups = new List<PickupPlacement> { new PickupPlacement(Theme.Biology, new ScreenAddress(chem, 0, 0)) };
            List<string> problems = SeedSweep.FindItemProblems(model, new ItemPlacement(model.Seed, gates, pickups), new[] { Theme.Biology });
            Assert.IsTrue(problems.Any(p => p.Contains("no gate on another built face")), string.Join("; ", problems));
            Assert.IsTrue(problems.Any(p => p.Contains("not its home face")), string.Join("; ", problems));
        }

        // ---------- Assets ----------

        [Test]
        public void Library_EveryGateSlotHasAnAlcove_ClearOfLanes()
        {
            ModuleLibrary library = LoadLibrary();
            foreach (ScreenModule module in library.TownModules.Concat(library.SciencePools.SelectMany(p => p.Modules)))
            {
                GateSlot[] slots = module.Gates;
                Transform alcoves = module.transform.Find("Alcoves");
                Assert.IsNotNull(alcoves, $"{module.name}: no alcoves");
                Assert.AreEqual(slots.Length, alcoves.childCount, $"{module.name}: one alcove per gate slot");
                var obstacles = module.GetComponentsInChildren<BoxCollider2D>(true)
                    .Select(b => new Rect((Vector2)b.transform.localPosition + b.offset - b.size / 2f, b.size)).ToList();
                for (int i = 0; i < slots.Length; i++)
                {
                    GateSlot slot = slots[i];
                    Transform alcove = alcoves.GetChild(i);
                    Assert.AreEqual(3, alcove.GetComponentsInChildren<BoxCollider2D>(true).Length, $"{module.name}: alcove {i} walls");

                    Vector2 gate = slot.transform.localPosition;
                    Vector2 pocket = gate + slot.PocketOffset;
                    Vector2 towardCentre = slot.Opening.ToVector();
                    Assert.Greater(Vector2.Dot(-gate, towardCentre), 0f, $"{module.name}: alcove {i} must open toward the screen centre");
                    Assert.Less(Vector2.Dot(slot.PocketOffset, towardCentre), 0f, $"{module.name}: pocket {i} must lie behind the gate");

                    // The gate itself keeps exits open, and the pocket is free space a player fits into.
                    bool across = slot.Opening == Facing.North || slot.Opening == Facing.South;
                    Vector2 size = across
                        ? new Vector2(GateSlot.GateWidth, GateSlot.GateThickness)
                        : new Vector2(GateSlot.GateThickness, GateSlot.GateWidth);
                    Assert.IsTrue(ScreenModule.KeepsExitsOpen(new Rect(gate - size / 2f, size)), $"{module.name}: gate {i} blocks a lane");
                    var playerBox = new Rect(pocket - Vector2.one * 0.45f, Vector2.one * 0.9f);
                    foreach (Rect o in obstacles)
                        Assert.IsFalse(o.Overlaps(playerBox), $"{module.name}: pocket {i} is blocked");
                }
            }
        }

        [Test]
        public void ItemCatalog_HasOnePlaceholderPerBuiltScience()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogPath);
            Assert.IsNotNull(catalog, $"{ItemCatalogPath} missing: run Cube > Build Item Catalog");
            CollectionAssert.AreEquivalent(BuiltSciences, catalog.Themes());
            Assert.AreEqual(BuiltSciences.Length, catalog.Items.Count);
            foreach (Theme theme in BuiltSciences)
            {
                ItemDefinition item = catalog.ForTheme(theme);
                Assert.IsNotNull(item, $"{theme} item");
                Assert.AreEqual(theme, item.HomeTheme);
                Assert.IsNotEmpty(item.Id);
                Assert.IsNotEmpty(item.DisplayName);
            }
            Assert.AreEqual(catalog.Items.Count, catalog.Items.Select(i => i.Id).Distinct().Count(), "Item ids must be unique");
        }
    }
}
