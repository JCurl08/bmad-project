using System;
using System.Collections.Generic;
using System.Linq;
using Game.Tracer;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The Earth and Atmosphere face as pure data: every face is built and nothing is sealed (the sealed path still works
    /// for faces sealed on purpose); the catalog's Stormcaller* (the base id, four wand variants, each with its own art);
    /// the roll (every phenomenon on some seed, the same seed the same phenomenon, read from the id never the catalog
    /// order); plan rules on every seed (required gates the rolled wand's with one on another face, optional ones the
    /// others' guarding nothing; the calm eye at the centre; one hazard per other screen, every kind, strengths from the
    /// level inside PlayerMover's bounds; crevices off the lanes, clear of walls and slots, cutting nothing off; the trial
    /// fits), with the sweep running them and flagging broken plans; Physics and Math keep clear of Earth's content; the
    /// dinosaurs' lines; and the subtle-science text rule for every Earth string, generated hints included.
    /// </summary>
    public class EarthTests
    {
        private const string LibraryPath = "Assets/Modules/ModuleLibrary.asset";
        private const string ItemCatalogPath = "Assets/Items/ItemCatalog.asset";
        private const int Seeds = 50;

        /// <summary>Hand-made geometry below is in the first 16x10 screens' units, scaled like the game's (4.1).</summary>
        private const float K = ScreenMath.ContentScale;

        private static Rect R(float xMin, float yMin, float xMax, float yMax) => Rect.MinMaxRect(xMin * K, yMin * K, xMax * K, yMax * K);


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

        private static EarthPlan Plan(int seed, ModuleLibrary library, ItemCatalog catalog, out CubeModel model,
            out ItemPlacement placement, out Func<ScreenAddress, ScreenModule> moduleAt)
        {
            placement = Place(seed, library, catalog, out model, out moduleAt);
            return EarthPlan.Create(model, placement, moduleAt, StormcallerItem.VariantKinds(catalog), BiologyBeaks.VariantKinds(catalog),
                ChemistryIsotope.VariantKinds(catalog));
        }

        // ---------- Unsealed and catalog ----------

        [Test]
        public void AllSixFacesAreBuilt_NothingIsSealed_AndEarthIsAReachableItemOnEverySeed()
        {
            foreach (Theme theme in Enum.GetValues(typeof(Theme))) Assert.IsTrue(CubeModel.IsBuilt(theme), $"{theme} is built");
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                EarthPlan plan = Plan(seed, library, catalog, out CubeModel model, out ItemPlacement placement, out _);
                for (int f = 0; f < CubeSettings.FaceCount; f++) Assert.IsFalse(model.IsSealed((FaceId)f), $"seed {seed}: {(FaceId)f}");
                Assert.IsTrue(placement.TryGetPickup(Theme.EarthAtmosphere, out PickupPlacement pickup), $"seed {seed}: an Earth pickup");
                Assert.AreEqual(model.FaceOf(Theme.EarthAtmosphere), pickup.Screen.Face);
                Assert.IsNotNull(plan, $"seed {seed}: an Earth plan");
                Assert.AreEqual(6 * model.FaceSize * model.FaceSize, SeedSweep.FindReachable(model).Count, $"seed {seed}: every screen reachable");
            }

            // The sealed-face path still works for a face sealed on purpose: no Earth plan, no Earth item.
            var sealedModel = new CubeModel(1, CubeSettings.DefaultFaceSize, new[] { Theme.EarthAtmosphere });
            CubeLayout sealedLayout = CubeLayout.Generate(sealedModel, library);
            ItemPlacement sealedPlacement = ItemPlacement.ForRun(sealedModel, sealedLayout, library, catalog.Themes(), catalog.VariantCounts());
            Assert.IsFalse(sealedPlacement.TryGetPickup(Theme.EarthAtmosphere, out _));
            Assert.IsNull(EarthPlan.Create(sealedModel, sealedPlacement));
            Assert.IsEmpty(SeedSweep.FindUnreachable(sealedModel));
        }

        [Test]
        public void Catalog_EarthItem_IsTheStormcaller_WithFourWandVariantsByIdInRollOrder_EachWithItsOwnArt()
        {
            ItemCatalog catalog = Catalog();
            ItemDefinition baseItem = catalog.ForTheme(Theme.EarthAtmosphere);
            Assert.IsNotNull(baseItem, "The catalog has an Earth item");
            Assert.AreEqual(StormcallerItem.Id, baseItem.Id, "The base id stays stable");
            Assert.AreEqual(StormcallerItem.Name, baseItem.DisplayName);
            Assert.AreEqual(5, catalog.Items.Count, "One base item per science face");

            List<ItemDefinition> wands = catalog.VariantsOf(Theme.EarthAtmosphere);
            CollectionAssert.AreEqual(StormcallerItem.VariantOrder.Select(StormcallerItem.IdOf).ToArray(), wands.Select(w => w.Id).ToArray());
            CollectionAssert.AreEqual(StormcallerItem.VariantOrder, StormcallerItem.VariantKinds(catalog));
            foreach (ItemDefinition wand in wands)
            {
                Assert.AreEqual(Theme.EarthAtmosphere, wand.HomeTheme);
                StringAssert.StartsWith(StormcallerItem.Name, wand.DisplayName);
                Assert.AreEqual(ArtCategory.Item, ArtKeys.CategoryOf(ArtKeys.Item(wand)));
            }
            Assert.AreEqual(4, wands.Select(ArtKeys.Item).Distinct().Count(), "A key per wand");
            Assert.IsFalse(wands.Select(ArtKeys.Item).Contains(ArtKey.ItemGeneric));
            Assert.AreEqual(4, catalog.VariantCounts()[Theme.EarthAtmosphere]);
            CollectionAssert.Contains(catalog.Themes(), Theme.EarthAtmosphere, "Still one logical Earth item for the placer");
        }

        [Test]
        public void Roll_EveryPhenomenonOnSomeSeed_TheSameSeedTheSamePhenomenon_ReadFromTheIdNeverTheOrder()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            var seen = new HashSet<Phenomenon>();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                EarthPlan a = Plan(seed, library, catalog, out _, out _, out _);
                EarthPlan b = Plan(seed, library, catalog, out _, out _, out _);
                Assert.AreEqual(a.Rolled, b.Rolled, $"seed {seed}: the same seed rolls the same phenomenon");
                Assert.AreEqual(a.Signature(), b.Signature(), $"seed {seed}: the same plan");
                Assert.AreEqual(StormcallerItem.KindOf(catalog.ItemFor(Theme.EarthAtmosphere, seed)), a.Rolled, $"seed {seed}");
                Assert.AreNotEqual(Phenomenon.None, a.Rolled);
                seen.Add(a.Rolled);
            }
            CollectionAssert.AreEquivalent(StormcallerItem.VariantOrder, seen, "Seeds 1-50 roll every phenomenon");

            // Kinds are read from the ids: a shuffled catalog still gives every gate the right wand.
            var shuffled = ScriptableObject.CreateInstance<ItemCatalog>();
            var made = new List<Object> { shuffled };
            try
            {
                ItemDefinition baseItem = StormcallerItem.CreateItem();
                made.Add(baseItem);
                Assert.IsNull(StormcallerItem.KindOf(baseItem), "The base item is no wand");
                var reversed = StormcallerItem.VariantOrder.Reverse().Select(StormcallerItem.CreateItem).ToList();
                made.AddRange(reversed);
                var items = new List<ItemDefinition> { baseItem };
                foreach (Theme t in new[] { Theme.Biology, Theme.Chemistry, Theme.Physics, Theme.Math })
                    items.Add(catalog.ForTheme(t));
                shuffled.Set(items);
                shuffled.SetVariants(reversed);
                List<Phenomenon> kinds = StormcallerItem.VariantKinds(shuffled);
                CollectionAssert.AreEqual(StormcallerItem.VariantOrder.Reverse().ToArray(), kinds);
                for (int seed = 1; seed <= 10; seed++)
                {
                    var model = new CubeModel(seed);
                    ItemPlacement p = ItemPlacement.ForRun(model, CubeLayout.Generate(model, library), library, shuffled.Themes(),
                        shuffled.VariantCounts());
                    EarthPlan plan = EarthPlan.Create(model, p, null, kinds);
                    Assert.AreEqual(StormcallerItem.KindOf(shuffled.ItemFor(Theme.EarthAtmosphere, seed)), plan.Rolled, $"seed {seed}");
                    foreach (EarthGateSpec g in plan.Gates.Where(g => g.Optional))
                    {
                        Assert.AreNotEqual(plan.Rolled, g.Kind, g.ToString());
                        OptionalGatePlacement o = p.OptionalGates.First(x => x.Screen == g.Screen && x.Slot == g.Slot);
                        Assert.AreEqual(StormcallerItem.KindOf(shuffled.VariantsOf(Theme.EarthAtmosphere)[o.Variant]), g.Kind, g.ToString());
                    }
                    Assert.IsEmpty(EarthPlan.FindProblems(model, p, kinds), $"seed {seed}");
                }
            }
            finally
            {
                foreach (Object o in made) Object.DestroyImmediate(o);
            }
        }

        // ---------- Plan rules ----------

        [Test]
        public void Plan_RequiredGatesAreTheRolledWands_OneOnAnotherFace_OptionalGatesTheOthers_OnEarth_GuardingNothing()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                EarthPlan plan = Plan(seed, library, catalog, out CubeModel model, out ItemPlacement placement, out _);
                FaceId face = model.FaceOf(Theme.EarthAtmosphere);
                Assert.AreEqual(placement.Gates.Count(g => g.Item == Theme.EarthAtmosphere), plan.Gates.Count(g => !g.Optional));
                Assert.IsTrue(plan.Gates.Where(g => !g.Optional).All(g => g.Kind == plan.Rolled), $"seed {seed}");
                Assert.IsTrue(plan.Gates.Any(g => !g.Optional && g.Screen.Face != face), $"seed {seed}: the rolled wand opens a gate elsewhere");
                foreach (EarthGateSpec g in plan.Gates.Where(g => g.Optional))
                {
                    Assert.AreEqual(face, g.Screen.Face, $"seed {seed}: {g}");
                    Assert.AreNotEqual(plan.Rolled, g.Kind, $"seed {seed}: {g}");
                    Assert.IsFalse(placement.Pickups.Any(p => p.IsGuarded && p.Screen == g.Screen && p.GuardSlot == g.Slot),
                        $"seed {seed}: {g} guards a pickup");
                }
                if (plan.Gates.Count(g => g.Optional) >= 3)
                    CollectionAssert.IsSubsetOf(StormcallerItem.VariantOrder.Where(k => k != plan.Rolled).ToList(),
                        plan.Gates.Where(g => g.Optional).Select(g => g.Kind).Distinct().ToList(), $"seed {seed}: every other phenomenon has a gate");
                Assert.IsEmpty(SeedSweep.FindOptionalGateProblems(model, placement), $"seed {seed}");
            }
        }

        [Test]
        public void Plan_TheCalmEyeIsTheCentre_OneHazardPerOtherScreen_EveryKind_StrengthsFromTheLevel_WithinTheMoverBounds()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                EarthPlan plan = Plan(seed, library, catalog, out CubeModel model, out ItemPlacement placement, out _);
                FaceId face = model.FaceOf(Theme.EarthAtmosphere);
                Assert.AreEqual(new ScreenAddress(face, 1, 1), plan.Eye, "The calm eye is (1,1) at 3x3");
                Assert.IsNull(plan.HazardOn(plan.Eye), $"seed {seed}: the eye is calm");
                foreach (GatePlacement g in placement.Gates.Where(g => g.Item == Theme.Physics && g.Screen.Face == face))
                    Assert.IsNull(plan.HazardOn(g.Screen), $"seed {seed}: no hazard on the timed door's screen {g.Screen}");
                int hazardScreens = 0;
                for (int y = 0; y < model.FaceSize; y++)
                    for (int x = 0; x < model.FaceSize; x++)
                    {
                        var s = new ScreenAddress(face, x, y);
                        int count = plan.Hazards.Count(h => h.Screen == s);
                        Assert.AreEqual(plan.IsCalm(s) ? 0 : 1, count, $"seed {seed}: {s}");
                        if (!plan.IsCalm(s)) hazardScreens++;
                    }
                Assert.AreEqual(EarthPlan.LevelFor(hazardScreens, model.FaceSize), plan.Level, "The level is the difficulty parameter");
                Assert.IsTrue(plan.Hazards.All(h => h.Screen.Face == face), "Hazards only on Earth (never Town, never the core)");
                if (hazardScreens >= 4)
                    foreach (HazardKind k in Enum.GetValues(typeof(HazardKind)))
                        Assert.IsTrue(plan.Hazards.Any(h => h.Kind == k), $"seed {seed}: some {k}");
                foreach (HazardSpec h in plan.Hazards)
                {
                    Assert.IsTrue(h.IsPlaced, $"seed {seed}: {h}");
                    Assert.AreEqual(EarthPlan.StrengthOf(h.Kind, plan.Level), h.Strength, 1e-5f);
                    MoveModifier m = Modifier(h);
                    Assert.AreEqual(m.SpeedMultiplier, PlayerMover.Clamp(m).SpeedMultiplier, 1e-5f, $"{h}: within the mover's bounds");
                    Assert.AreEqual(m.Push, PlayerMover.Clamp(m).Push, $"{h}: within the mover's bounds");
                    Assert.AreEqual(m.Grip, PlayerMover.Clamp(m).Grip, 1e-5f, $"{h}: within the mover's bounds");
                    // Walking any way through it still makes headway (it slows or deflects, never blocks).
                    foreach (Vector2 dir in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down })
                        Assert.Greater(Vector2.Dot(PlayerMover.TargetVelocity(dir, TimeField.BasePlayerSpeed, m), dir), 1f, $"{h} toward {dir}");
                }
            }

            // Difficulty: more hazard screens per screen of the face, a higher level, never past the cap.
            Assert.AreEqual(0, EarthPlan.LevelFor(0, 3));
            Assert.AreEqual(1, EarthPlan.LevelFor(1, 3));
            Assert.LessOrEqual(EarthPlan.LevelFor(8, 3), EarthPlan.MaxLevel);
            Assert.GreaterOrEqual(EarthPlan.LevelFor(8, 3), EarthPlan.LevelFor(3, 3));
            Assert.AreEqual(EarthPlan.MaxLevel, EarthPlan.LevelFor(100, 3));
            for (int level = 1; level < EarthPlan.MaxLevel; level++)
            {
                Assert.Greater(EarthPlan.WindPush(level + 1), EarthPlan.WindPush(level), "Stronger wind");
                Assert.Less(EarthPlan.RainSpeed(level + 1), EarthPlan.RainSpeed(level), "Slower rain");
                Assert.Less(EarthPlan.SnowGrip(level + 1), EarthPlan.SnowGrip(level), "Slipperier snow");
            }
            Assert.Less(EarthPlan.WindPush(EarthPlan.MaxLevel), PlayerMover.MaxPushFraction);
            Assert.Greater(EarthPlan.RainSpeed(EarthPlan.MaxLevel), PlayerMover.MinSpeedMultiplier);
            Assert.Greater(EarthPlan.SnowGrip(EarthPlan.MaxLevel), PlayerMover.MinGrip);
        }

        private static MoveModifier Modifier(HazardSpec h)
        {
            switch (h.Kind)
            {
                case HazardKind.Wind: return new MoveModifier { SpeedMultiplier = 1f, Push = h.Direction * h.Strength, Grip = 1f };
                case HazardKind.Snow: return new MoveModifier { SpeedMultiplier = 1f, Push = Vector2.zero, Grip = h.Strength };
                case HazardKind.FreezingRain: return new MoveModifier { SpeedMultiplier = h.Strength, Push = Vector2.zero, Grip = 1f };
                default: return MoveModifier.None;
            }
        }

        [Test]
        public void Crevices_KeepOffTheLanesAndEdges_ClearOfWallsGateFrontsAndSlots_AndCutNothingOff()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            int crevices = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                EarthPlan plan = Plan(seed, library, catalog, out _, out _, out Func<ScreenAddress, ScreenModule> moduleAt);
                foreach (HazardSpec h in plan.Hazards.Where(h => h.Kind == HazardKind.Crevice))
                {
                    crevices++;
                    Assert.IsTrue(ScreenModule.KeepsExitsOpen(h.Area), $"seed {seed}: {h} keeps the exits open");
                    Assert.IsNull(EarthPlan.CreviceProblem(h.Area, moduleAt(h.Screen), plan.MarksOn(h.Screen)), $"seed {seed}: {h}");
                    foreach (ScreenMark m in plan.MarksOn(h.Screen))
                        Assert.IsFalse(h.Area.Contains(m.Local), $"seed {seed}: {h} sits on {m.What}");
                }
            }
            Assert.Greater(crevices, Seeds, "Crevices on most seeds");

            // Solid trial pieces count as solid: a strip that leaves only a gap between itself and a piece too narrow for the
            // player seals the nook behind them, and the flood fill catches it.
            var nook = new List<Rect>
            {
                R(2f, 1.8f, 2.3f, 3.3f), R(5f, 1.8f, 5.3f, 3.3f), R(2f, 3.3f, 5.3f, 3.45f),
            };
            Rect half = R(2.6f, 1.4f, 3.6f, 2f);
            Assert.IsNull(EarthPlan.CreviceProblem(half, nook, null), "Without the piece the player gets past the strip");
            var withPiece = new List<Rect>(nook) { EarthPlan.TrialPieceRect(new Vector2(4.35f, 1.75f) * K) };
            StringAssert.Contains("cut off", EarthPlan.CreviceProblem(half, withPiece, null), "With the piece the nook is sealed");

            // Every seed's crevice on the trial screen passes with the pieces solid.
            for (int seed = 1; seed <= Seeds; seed++)
            {
                EarthPlan plan = Plan(seed, library, catalog, out _, out _, out Func<ScreenAddress, ScreenModule> moduleAt);
                HazardSpec onTrial = plan.Hazards.FirstOrDefault(h => h.Kind == HazardKind.Crevice && h.Screen == plan.TrialScreen);
                if (onTrial == null) continue;
                List<Rect> solids = TownPlan.BlockedRects(moduleAt(onTrial.Screen));
                solids.AddRange(plan.TrialSpots.Select(EarthPlan.TrialPieceRect));
                Assert.IsNull(EarthPlan.CreviceProblem(onTrial.Area, solids, plan.MarksOn(onTrial.Screen)), $"seed {seed}: {onTrial}");
            }

            // A strip that closes a nook (with the walls around it) is caught by the flood fill.
            var walls = new List<Rect>
            {
                R(2f, 1.8f, 2.3f, 3.3f), R(5f, 1.8f, 5.3f, 3.3f), R(2f, 3.3f, 5.3f, 3.45f),
            };
            Rect closing = R(2.6f, 1.4f, 4.7f, 2f);
            Assert.IsNull(EarthPlan.CreviceProblem(R(-5f, -3.2f, -3f, -2.6f), walls, null), "An open strip is fine");
            StringAssert.Contains("cut off", EarthPlan.CreviceProblem(closing, walls, null));
            StringAssert.Contains("exit lane", EarthPlan.CreviceProblem(R(-1f, 1.5f, 1f, 2.1f), new List<Rect>(), null));
            StringAssert.Contains("sits on", EarthPlan.CreviceProblem(R(3f, 2f, 5f, 2.6f), new List<Rect>(),
                new[] { new ScreenMark("a hidden-item slot", new Vector2(4f, 2.3f) * K, 0.6f * K) }));
        }

        [Test]
        public void TrialPieces_KeepClearOfGateFrontsSlotsButtonsAndPlates_OnEverySeed()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                EarthPlan plan = Plan(seed, library, catalog, out _, out _, out Func<ScreenAddress, ScreenModule> moduleAt);
                Assert.AreEqual(EarthTrial.PieceCount, plan.TrialSpots.Count, $"seed {seed}");
                ScreenModule module = moduleAt(plan.TrialScreen);
                List<ScreenMark> marks = FaceContent.SlotMarks(module);
                marks.AddRange(plan.MarksOn(plan.TrialScreen).Where(m => m.What != "Earth trial piece"));
                foreach (Vector2 p in plan.TrialSpots)
                {
                    Assert.IsNull(EarthPlan.TrialSpotProblem(p, marks), $"seed {seed}");
                    Rect box = EarthPlan.TrialPieceRect(p);
                    foreach (ScreenMark m in marks)
                        Assert.Greater(Vector2.Distance(box.center, m.Local), m.Radius + EarthTrialPiece.Size / 2f, $"seed {seed}: on {m.What}");
                }
            }
            Assert.IsNotNull(EarthPlan.TrialSpotProblem(new Vector2(3f, 0.8f), new[] { new ScreenMark("gate front", new Vector2(3f, 0.8f), 1f) }));
        }

        [Test]
        public void OtherFacesSpawns_KeepClearOfEveryGateFrontAndSlot_ItsFilterLeavesRoom()
        {
            ModuleLibrary library = Library();
            foreach (ModuleLibrary.ThemePool pool in library.SciencePools)
            {
                int room = 0;
                foreach (ScreenModule module in pool.Modules)
                {
                    List<ScreenMark> marks = FaceContent.SlotMarks(module);
                    Assert.AreEqual(module.Gates.Length + module.HiddenItems.Length + module.CoreEntrances.Length, marks.Count);
                    List<Vector2> candidates = TownPlan.Candidates(TownPlan.BlockedRects(module));
                    List<Vector2> kept = candidates.Where(c => FaceContent.IsClearOfSlots(marks, c)).ToList();
                    Assert.Greater(kept.Count, 0, $"{module.name}: room left for NPCs");
                    room += kept.Count;
                    foreach (Vector2 c in kept)
                        foreach (ScreenMark m in marks)
                            Assert.GreaterOrEqual(Vector2.Distance(c, m.Local), m.Radius + FaceContent.SlotKeepOut, $"{module.name}: {c} on {m.What}");
                    foreach (GateSlot slot in module.Gates)
                    {
                        Vector2 front = EarthPlan.FrontLocal((Vector2)slot.transform.position - (Vector2)module.transform.position, slot.PocketOffset);
                        Assert.IsFalse(FaceContent.IsClearOfSlots(marks, front), $"{module.name}: a gate front is filtered");
                    }
                }
                Assert.Greater(room, 20, $"{pool.Theme}: room for a face's NPCs and enemies");
            }
        }

        [Test]
        public void TheSweep_RunsTheEarthRules_AndFlagsBrokenPlans()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            List<SeedSweep.SeedResult> results = SeedSweep.Run(1, Seeds, CubeSettings.DefaultFaceSize, library, catalog.Themes(),
                catalog.VariantCounts(), BiologyBeaks.VariantKinds(catalog), ChemistryIsotope.VariantKinds(catalog), StormcallerItem.VariantKinds(catalog));
            Assert.IsTrue(results.All(r => r.Passed), SeedSweep.Describe(results));
            StringAssert.Contains("Stormcaller*", SeedSweep.Describe(results));

            EarthPlan plan = Plan(1, library, catalog, out CubeModel model, out ItemPlacement placement, out Func<ScreenAddress, ScreenModule> moduleAt);
            Assert.IsEmpty(plan.Problems(model, placement));
            int wind = plan.Hazards.ToList().FindIndex(h => h.Kind == HazardKind.Wind);
            HazardSpec w = plan.Hazards[wind];
            var onEye = new HazardSpec(plan.Eye, HazardKind.Wind, w.Area, w.Direction, w.Strength);
            Assert.IsTrue(plan.WithHazard(wind, onEye).Problems(model, placement).Any(p => p.Contains("calm eye")), "A hazard on the eye");
            var gale = new HazardSpec(w.Screen, HazardKind.Wind, w.Area, w.Direction, 0.9f);
            Assert.IsTrue(plan.WithHazard(wind, gale).Problems(model, placement).Any(p => p.Contains("could stop the player")), "A push too strong");
            int crevice = plan.Hazards.ToList().FindIndex(h => h.Kind == HazardKind.Crevice);
            if (crevice >= 0)
            {
                HazardSpec c = plan.Hazards[crevice];
                var inLane = new HazardSpec(c.Screen, HazardKind.Crevice, Rect.MinMaxRect(-1.5f, -0.3f, 1.5f, 0.3f), Vector2.zero, 0f);
                Assert.IsTrue(plan.WithHazard(crevice, inLane).Problems(model, placement).Any(p => p.Contains("exit lane")), "A crevice in a lane");
            }
            int required = plan.Gates.ToList().FindIndex(g => !g.Optional);
            EarthGateSpec r = plan.Gates[required];
            Phenomenon wrong = StormcallerItem.VariantOrder.First(k => k != plan.Rolled);
            Assert.IsTrue(plan.WithGate(required, new EarthGateSpec(r.Screen, r.Slot, wrong, r.OnHome, false)).Problems(model, placement)
                .Any(p => p.Contains("needs another phenomenon")), "A required gate with another wand");
        }

        [Test]
        public void PhysicsAndMath_KeepClearOfEarthsContent_OnEverySeed()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            List<BeakKind> beaks = BiologyBeaks.VariantKinds(catalog);
            List<IsotopeStage> stones = ChemistryIsotope.VariantKinds(catalog);
            int offFace = 0, physicsScreens = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                EarthPlan earth = Plan(seed, library, catalog, out CubeModel model, out ItemPlacement placement,
                    out Func<ScreenAddress, ScreenModule> moduleAt);
                PhysicsPlan physics = PhysicsPlan.Create(model, placement, moduleAt, beaks, PhysicsPlan.DefaultMaxRequired, stones);
                MathPlan math = MathPlan.Create(model, placement, moduleAt, beaks, stones);
                foreach (KeyValuePair<ScreenAddress, List<ScreenMark>> e in earth.ContentMarks())
                {
                    // Physics weighs other faces' marks on the screens it lays out (its doors' and its trial's).
                    if (physics.Doors.Any(d => d.Screen == e.Key) || physics.TrialScreen == e.Key)
                    {
                        physicsScreens++;
                        IReadOnlyList<ScreenMark> avoided = physics.OtherMarks(e.Key);
                        foreach (ScreenMark m in e.Value)
                            Assert.IsTrue(avoided.Any(a => a.What == m.What && a.Local == m.Local), $"seed {seed}: Physics ignores {m}");
                    }
                    if (e.Key.Face == model.FaceOf(Theme.Math))
                    {
                        offFace++;
                        foreach (ScreenMark m in e.Value.Where(m => m.What.StartsWith("front of Earth")))
                            Assert.IsTrue(math.MarksOn(e.Key).Any(a => a.What == m.What && a.Local == m.Local), $"seed {seed}: Math ignores {m}");
                    }
                }
                Assert.IsEmpty(PhysicsPlan.FindProblems(model, placement, moduleAt, beaks, PhysicsPlan.DefaultMaxRequired, stones), $"seed {seed}");
            }
            Assert.Greater(offFace, 0, "Some Earth gates sit on the Math face");
            Assert.Greater(physicsScreens, 0, "Some Earth content shares a screen with Physics content");
        }

        // ---------- Dinosaurs ----------

        [Test]
        public void Dinosaurs_TerrestrialAndAvian_TheFirstNamesTheWand_TheSecondTheHazards_TheRestTheirPatron_AndEndWithATrueHint()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            for (int seed = 1; seed <= 10; seed++)
            {
                EarthPlan plan = Plan(seed, library, catalog, out CubeModel model, out ItemPlacement placement, out _);
                var facts = new RunFacts(model, CubeLayout.Generate(model, library), placement);
                for (int i = 0; i < EarthPopulation.DinoCount(model.FaceSize); i++)
                {
                    NpcSpec spec = EarthPopulation.DinoSpec(seed, i);
                    Assert.AreEqual(Race.Dinosaur, spec.Race);
                    Assert.AreEqual(EarthPopulation.VariantOf(i), spec.Variant, $"seed {seed} dinosaur {i}");
                    Assert.AreEqual(spec.Signature(), EarthPopulation.DinoSpec(seed, i).Signature(), "Deterministic");
                    List<string> lines = EarthPopulation.LinesFor(spec, facts, HintDensity.Full, plan.Rolled, i);
                    Assert.AreEqual(Dialogue.MaxLines, lines.Count);
                    if (i == EarthPopulation.WandTeller) Assert.AreEqual(Dialogue.WandLine(plan.Rolled), lines[0]);
                    else if (i == EarthPopulation.HazardTeller) Assert.AreEqual(Dialogue.HazardLine, lines[0]);
                    else if (spec.Variant == DinoVariant.Avian) CollectionAssert.Contains(Dialogue.SkyWatcherLines, lines[0]);
                    else CollectionAssert.Contains(Dialogue.OldDiggerLines, lines[0]);
                    Assert.AreEqual(HintGenerator.For(spec, facts, HintDensity.Full)[0].Text, lines[1]);
                }
            }
            Assert.GreaterOrEqual(EarthPopulation.DinoCount(CubeSettings.DefaultFaceSize), 2, "Both kinds every run");
            foreach (Phenomenon kind in StormcallerItem.VariantOrder)
            {
                StringAssert.Contains(StormcallerItem.Word(kind), Dialogue.WandLine(kind).ToLowerInvariant());
                StringAssert.Contains(StormcallerItem.Name, Dialogue.WandLine(kind));
            }
            // An unknown phenomenon gets a line that names none (never a false one).
            Assert.AreEqual(Dialogue.NeutralWandLine, Dialogue.WandLine(Phenomenon.None));
            StringAssert.Contains(StormcallerItem.Name, Dialogue.NeutralWandLine);
            foreach (Phenomenon kind in StormcallerItem.VariantOrder)
            {
                StringAssert.DoesNotContain(StormcallerItem.Word(kind), Dialogue.NeutralWandLine.ToLowerInvariant());
            }
            foreach (string word in new[] { "wind", "snow", "rain", "crevice", "calm" })
                StringAssert.Contains(word, Dialogue.HazardLine.ToLowerInvariant());
        }

        // ---------- Text ----------

        private static List<string> EarthStrings()
        {
            var strings = new List<string>(FaceText.Flavour(Race.Dinosaur));
            strings.AddRange(Dialogue.OldDiggerLines);
            strings.AddRange(Dialogue.SkyWatcherLines);
            strings.AddRange(Dialogue.WandLines);
            strings.Add(Dialogue.NeutralWandLine);
            strings.Add(Dialogue.HazardLine);
            strings.AddRange(StormcallerItem.AllText());
            strings.Add(HintGenerator.ItemNickname(Theme.EarthAtmosphere));
            strings.Add(HintGenerator.ThemeName(Theme.EarthAtmosphere));
            ItemCatalog catalog = Catalog();
            foreach (ItemDefinition item in catalog.VariantsOf(Theme.EarthAtmosphere).Append(catalog.ForTheme(Theme.EarthAtmosphere)))
            {
                strings.Add(item.DisplayName);
                strings.Add(PickupFeedback.ItemText(item));
            }
            return strings;
        }

        private static IEnumerable<string> EarthNames()
        {
            yield return EarthEnemies.Name(EarthEnemyKind.Squall);
            yield return EarthEnemies.Name(EarthEnemyKind.Rumbler);
            foreach (string name in FaceText.PartNames(Race.Dinosaur)) yield return name;
        }

        [Test]
        public void EarthText_UsesNoBannedWords_AndPlaceholderNamesCarryAStar()
        {
            List<string> strings = EarthStrings();
            var earthHints = new List<string>();
            Assert.Greater(strings.Count, 30);
            // The hints the dinosaurs actually speak, on every seed and density.
            var hints = new List<string>();
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                EarthPlan plan = Plan(seed, library, catalog, out CubeModel model, out ItemPlacement placement, out _);
                var facts = new RunFacts(model, CubeLayout.Generate(model, library), placement);
                foreach (HintDensity density in new[] { HintDensity.Sparse, HintDensity.Full })
                    for (int i = 0; i < EarthPopulation.DinoCount(model.FaceSize); i++)
                        hints.AddRange(EarthPopulation.LinesFor(EarthPopulation.DinoSpec(seed, i), facts, density, plan.Rolled, i).Skip(1));
            }
            Assert.Greater(hints.Count, 100);
            Assert.IsTrue(hints.Any(h => h.Contains(HintGenerator.EarthRegionName)), "Some hint names the Earth face, by its placeholder name");
            StringAssert.EndsWith("*", HintGenerator.EarthRegionName);
            Assert.AreEqual("Wildlands*", HintGenerator.ThemeName(Theme.EarthAtmosphere));

            // Every generated hint that talks about Earth, as any population speaks them (Town, every face: they all use
            // HintGenerator), and Town's whole conversations.
            for (int seed = 1; seed <= Seeds; seed++)
            {
                ItemPlacement placement = Place(seed, library, catalog, out CubeModel model, out _);
                var facts = new RunFacts(model, CubeLayout.Generate(model, library), placement);
                foreach (HintDensity density in new[] { HintDensity.Sparse, HintDensity.Full })
                {
                    foreach (HintKind kind in Enum.GetValues(typeof(HintKind)))
                        for (uint salt = 0; salt < 24; salt++)
                            foreach (Hint h in HintGenerator.Generate(facts, kind, salt, density))
                                if (h.FaceTheme == Theme.EarthAtmosphere || h.Item == Theme.EarthAtmosphere) earthHints.Add(h.Text);
                    foreach (NpcSpec spec in TownPlan.Specs(seed, model.FaceSize))
                        earthHints.AddRange(Dialogue.For(spec, facts, density));
                }
            }
            Assert.IsTrue(earthHints.Any(h => h.Contains(HintGenerator.EarthRegionName)));
            Assert.IsTrue(earthHints.Any(h => h.Contains(HintGenerator.ItemNickname(Theme.EarthAtmosphere))), "Item hints name the wand");

            foreach (string text in strings.Concat(EarthNames()).Concat(hints).Concat(earthHints))
            {
                string lower = text.ToLowerInvariant();
                CollectionAssert.IsEmpty(InWorldText.BannedIn(text), $"\"{text}\" uses a banned word"); // the shared list (InWorldText.BannedWords)
                StringAssert.DoesNotContain(Dialogue.ForbiddenWord, lower, text);
                StringAssert.DoesNotContain("mary anning", lower, $"\"{text}\" names a real person");
            }

            // Proper names: a capitalised word that does not start a sentence is a name, and a name (one word, or a run of
            // capitalised words like "Ember Vale*") carries a star at its end, except the chosen character names (4.4:
            // NpcNames), by the shared rule.
            foreach (string text in strings)
                CollectionAssert.IsEmpty(InWorldText.UnstarredNames(text), $"A name without its star in \"{text}\"");

            // Placeholder names carry a trailing star wherever they appear.
            foreach (string text in strings.Concat(hints).Concat(earthHints))
                foreach (string bare in new[] { "Stormcaller", "Wildlands" })
                {
                    int at = text.IndexOf(bare, StringComparison.Ordinal);
                    while (at >= 0)
                    {
                        int after = at + bare.Length;
                        Assert.IsTrue(after < text.Length && text[after] == '*', $"\"{bare}\" without its star in \"{text}\"");
                        at = text.IndexOf(bare, after, StringComparison.Ordinal);
                    }
                }
            StringAssert.Contains(NpcNames.GroundPatron, string.Join(" ", Dialogue.OldDiggerLines));
            StringAssert.Contains(NpcNames.SkyPatron, string.Join(" ", Dialogue.SkyWatcherLines));
        }
    }
}
