using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The Chemistry face as pure data, on the beak pattern: the stone roll (each stage across seeds 1-50, same seed same
    /// stone), the catalog's three stones (placeholder names, stable ids, stage by id), plan determinism, required gates
    /// on the rolled stone with one off the home face, the other stones' optional gates guarding nothing required (the
    /// sweep flags violations), the difficulty parameter, the trial fitting on every seed, the mushrooms' lines and the
    /// subtle-science text rule for every Chemistry string.
    /// </summary>
    public class ChemistryTests
    {
        private const string LibraryPath = "Assets/Modules/ModuleLibrary.asset";
        private const string ItemCatalogPath = "Assets/Items/ItemCatalog.asset";
        private const int Seeds = 50;

        /// <summary>Science words in-world text never uses (lore-and-tone.md, subtle science), plus the real patron's name.</summary>
        private static readonly string[] BannedWords =
        {
            "isotope", "radioactive", "radioactivity", "radiation", "radium", "polonium", "uranium", "decay", "half-life",
            "half life", "element", "atom", "nuclear", "neutron", "proton", "electron", "chemical", "curie",
        };

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

        /// <summary>The placement the game makes for a seed (catalog themes and variants, as CubeWorld does).</summary>
        private static ItemPlacement Place(int seed, ModuleLibrary library, ItemCatalog catalog, out CubeModel model)
        {
            model = new CubeModel(seed);
            return ItemPlacement.ForRun(model, CubeLayout.Generate(model, library), library, catalog.Themes(),
                catalog.VariantCounts());
        }

        private static ChemistryPlan Plan(CubeModel model, ItemPlacement p, ItemCatalog catalog) =>
            ChemistryPlan.Create(model, p, ChemistryIsotope.VariantKinds(catalog));

        // ---------- The roll and the catalog ----------

        [Test]
        public void Roll_EachStoneComesUpAcrossSeeds1To50_AndTheSameSeedRollsTheSameStone()
        {
            ItemCatalog catalog = Catalog();
            var seen = new HashSet<IsotopeStage>();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                ItemDefinition rolled = catalog.ItemFor(Theme.Chemistry, seed);
                IsotopeStage? stage = ChemistryIsotope.KindOf(rolled);
                Assert.IsNotNull(stage, $"seed {seed}: the run's Chemistry item is a stone ({rolled})");
                Assert.AreSame(rolled, catalog.ItemFor(Theme.Chemistry, seed), $"seed {seed}: same seed, same stone");
                Assert.AreEqual(catalog.RollVariant(seed, Theme.Chemistry), catalog.RollVariant(seed, Theme.Chemistry));
                Assert.AreEqual(2, catalog.UnrolledVariants(Theme.Chemistry, seed).Count);
                CollectionAssert.DoesNotContain(catalog.UnrolledVariants(Theme.Chemistry, seed), rolled);
                seen.Add(stage.Value);
            }
            CollectionAssert.AreEquivalent(ChemistryIsotope.VariantOrder, seen, "Every stone is rolled at least once");
        }

        [Test]
        public void Catalog_HasTheThreeStones_AsChemistryVariants_WithPlaceholderNames_AndKeepsTheBaseEntry()
        {
            ItemCatalog catalog = Catalog();
            ItemDefinition baseItem = catalog.ForTheme(Theme.Chemistry);
            Assert.IsNotNull(baseItem);
            Assert.AreEqual("chemistry-item", baseItem.Id, "The base id stays stable");
            Assert.AreEqual(ChemistryIsotope.Name, baseItem.DisplayName);
            Assert.AreEqual(5, catalog.Items.Count, "Existing entries are kept");

            List<ItemDefinition> stones = catalog.VariantsOf(Theme.Chemistry);
            CollectionAssert.AreEqual(new[] { "glowstone", "wildstone", "leadstone" }, stones.Select(s => s.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "Glowstone*", "Wildstone*", "Leadstone*" }, stones.Select(s => s.DisplayName).ToArray());
            CollectionAssert.AreEqual(ChemistryIsotope.VariantOrder, ChemistryIsotope.VariantKinds(catalog));
            foreach (ItemDefinition stone in stones)
            {
                Assert.AreEqual(Theme.Chemistry, stone.HomeTheme);
                Assert.AreEqual(0f, stone.AttackReach, 1e-4f, "Stones swing at the default reach");
                Assert.AreNotEqual(ArtKey.ItemGeneric, ArtKeys.Item(stone), $"{stone} has its own art key");
            }
            Assert.AreEqual(3, stones.Select(ArtKeys.Item).Distinct().Count(), "A key per stone");
            Assert.AreEqual(3, catalog.VariantCounts()[Theme.Chemistry]);
            CollectionAssert.Contains(catalog.Themes(), Theme.Chemistry, "Still one logical Chemistry item for the placer");
        }

        [Test]
        public void StageKinds_AreReadFromTheItemIds_NeverFromTheCatalogOrder()
        {
            ItemDefinition glow = ChemistryIsotope.CreateItem(IsotopeStage.Glow);
            ItemDefinition wild = ChemistryIsotope.CreateItem(IsotopeStage.Unstable);
            ItemDefinition lead = ChemistryIsotope.CreateItem(IsotopeStage.Lead);
            ItemDefinition baseItem = ChemistryIsotope.CreateItem();
            var shuffled = ScriptableObject.CreateInstance<ItemCatalog>();
            try
            {
                Assert.AreEqual(IsotopeStage.Glow, ChemistryIsotope.KindOf(glow));
                Assert.AreEqual(IsotopeStage.Unstable, ChemistryIsotope.KindOf(wild));
                Assert.AreEqual(IsotopeStage.Lead, ChemistryIsotope.KindOf(lead));
                Assert.IsNull(ChemistryIsotope.KindOf(baseItem), "The base item is no stone");
                Assert.IsNull(ChemistryIsotope.KindOf(null));

                shuffled.Set(new List<ItemDefinition> { baseItem });
                shuffled.SetVariants(new List<ItemDefinition> { lead, glow, wild });
                List<IsotopeStage> kinds = ChemistryIsotope.VariantKinds(shuffled);
                CollectionAssert.AreEqual(new[] { IsotopeStage.Lead, IsotopeStage.Glow, IsotopeStage.Unstable }, kinds);

                // The plan's stone follows the rolled item's id, whatever its catalog index.
                ModuleLibrary library = Library();
                for (int seed = 1; seed <= 10; seed++)
                {
                    var model = new CubeModel(seed);
                    ItemPlacement p = ItemPlacement.ForRun(model, CubeLayout.Generate(model, library), library, shuffled.Themes(),
                        shuffled.VariantCounts());
                    ChemistryPlan plan = ChemistryPlan.Create(model, p, kinds);
                    Assert.AreEqual(ChemistryIsotope.KindOf(shuffled.ItemFor(Theme.Chemistry, seed)), plan.Stage, $"seed {seed}");
                    foreach (IsotopeGateSpec s in plan.Gates.Where(g => g.Optional))
                        Assert.AreNotEqual(plan.Stage, s.Stage, s.ToString());
                    Assert.IsEmpty(ChemistryPlan.FindProblems(model, p, kinds), $"seed {seed}");
                }
            }
            finally
            {
                Object.DestroyImmediate(shuffled);
                Object.DestroyImmediate(glow);
                Object.DestroyImmediate(wild);
                Object.DestroyImmediate(lead);
                Object.DestroyImmediate(baseItem);
            }
        }

        // ---------- The plan ----------

        [Test]
        public void Plan_IsDeterministic_OnItsOwnStream_AndHasNoProblems()
        {
            Assert.AreEqual(11UL, ChemistryPlan.RngStream);
            Assert.AreEqual(12UL, ChemistryFace.PopulationRngStream);
            Assert.AreNotEqual(BiologyPlan.RngStream, ChemistryPlan.RngStream);
            Assert.AreNotEqual(BiologyFace.PopulationRngStream, ChemistryFace.PopulationRngStream);

            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            var signatures = new HashSet<string>();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                ItemPlacement p = Place(seed, library, catalog, out CubeModel model);
                ChemistryPlan a = Plan(model, p, catalog);
                ChemistryPlan b = Plan(new CubeModel(seed), Place(seed, library, catalog, out _), catalog);
                Assert.IsNotNull(a, $"seed {seed}");
                Assert.AreEqual(a.Signature(), b.Signature(), $"seed {seed}: same seed, same plan");
                Assert.AreEqual(ChemistryIsotope.KindOf(catalog.ItemFor(Theme.Chemistry, seed)), a.Stage, $"seed {seed}");
                Assert.IsEmpty(ChemistryPlan.FindProblems(model, p, ChemistryIsotope.VariantKinds(catalog)), $"seed {seed}");
                Assert.AreEqual(model.FaceOf(Theme.Chemistry), a.TrialScreen.Face);
                signatures.Add(a.Signature());
            }
            Assert.Greater(signatures.Count, Seeds / 2, "Plans vary across seeds");
        }

        [Test]
        public void Plan_RequiredGatesUseTheRolledStone_OneOffTheHomeFace_OtherStonesAreOptionalOnChemistry()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                ItemPlacement p = Place(seed, library, catalog, out CubeModel model);
                ChemistryPlan plan = Plan(model, p, catalog);
                string context = $"seed {seed} ({plan.Stage})";
                int required = p.Gates.Count(g => g.Item == Theme.Chemistry);
                Assert.AreEqual(required, plan.Gates.Count(s => !s.Optional), $"{context}: a stage gate for every Chemistry gate");
                foreach (IsotopeGateSpec s in plan.Gates.Where(g => !g.Optional))
                {
                    Assert.AreEqual(plan.Stage, s.Stage, $"{context}: {s}");
                    Assert.IsTrue(p.TryGetGate(s.Screen, s.Slot, out GatePlacement g) && g.Item == Theme.Chemistry, s.ToString());
                }
                Assert.IsTrue(plan.Gates.Any(s => !s.Optional && s.Screen.Face != plan.Face),
                    $"{context}: the rolled stone opens a gate on another face");

                List<IsotopeGateSpec> optional = plan.Gates.Where(s => s.Optional).ToList();
                foreach (IsotopeStage other in ChemistryIsotope.VariantOrder.Where(s => s != plan.Stage))
                    Assert.IsTrue(optional.Any(s => s.Stage == other), $"{context}: optional {other} gates exist");
                foreach (IsotopeGateSpec s in optional)
                {
                    Assert.AreNotEqual(plan.Stage, s.Stage, $"{context}: {s}");
                    Assert.AreEqual(plan.Face, s.Screen.Face, $"{context}: {s} sits on Chemistry");
                    Assert.IsTrue(p.TryGetOptionalGate(s.Screen, s.Slot, out OptionalGatePlacement o) && o.Item == Theme.Chemistry);
                    Assert.IsFalse(p.Pickups.Any(k => k.IsGuarded && k.Screen == s.Screen && k.GuardSlot == s.Slot),
                        $"{context}: {s} guards a pickup");
                }
                Assert.IsEmpty(SeedSweep.FindOptionalGateProblems(model, p), context);
            }
        }

        [Test]
        public void Plan_Difficulty_IsTheStageGatesOnTheFace_AndSetsTheSwings()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            for (int seed = 1; seed <= 10; seed++)
            {
                ItemPlacement p = Place(seed, library, catalog, out CubeModel model);
                ChemistryPlan plan = Plan(model, p, catalog);
                foreach (IsotopeGateSpec s in plan.Gates)
                    Assert.AreEqual(plan.Gates.Count(g => g.Screen.Face == s.Screen.Face), s.Difficulty, s.ToString());
            }
            // Tuned like the beak gates: one swing, plus one per GatesPerExtraHit gates beyond the first on the face.
            Assert.AreEqual(3, ChemistryPlan.GatesPerExtraHit(2));
            Assert.AreEqual(7, ChemistryPlan.GatesPerExtraHit(3));
            Assert.AreEqual(1, ChemistryPlan.HitsFor(1, 3));
            Assert.AreEqual(1, ChemistryPlan.HitsFor(7, 3));
            Assert.AreEqual(2, ChemistryPlan.HitsFor(8, 3));
            Assert.AreEqual(3, ChemistryPlan.HitsFor(15, 3));
            Assert.AreEqual(2, ChemistryPlan.HitsFor(4, 2));
            Assert.AreEqual(1, ChemistryPlan.HitsFor(0, 3), "Clamped to one swing");
        }

        [Test]
        public void SweepRules_FlagOptionalGatesGuardingRequiredContent_UsingTheRolledStone_OrOffChemistry_AndNoOffFaceGate()
        {
            var model = new CubeModel(11);
            FaceId bio = model.FaceOf(Theme.Biology), chem = model.FaceOf(Theme.Chemistry);
            int rolled = 0; // Glowstone*
            var required = new[]
            {
                new GatePlacement(new ScreenAddress(chem, 0, 0), 0, Theme.Chemistry),
                new GatePlacement(new ScreenAddress(bio, 0, 0), 0, Theme.Chemistry),
            };
            var pickup = new PickupPlacement(Theme.Chemistry, new ScreenAddress(chem, 1, 1));
            var rolls = new Dictionary<Theme, int> { [Theme.Chemistry] = rolled };
            IReadOnlyList<IsotopeStage> order = ChemistryIsotope.VariantOrder; // hand-made variants, listed in this order

            var ok = new ItemPlacement(11, required, new[] { pickup }, new[]
            {
                new OptionalGatePlacement(new ScreenAddress(chem, 1, 0), 0, Theme.Chemistry, 2),
                new OptionalGatePlacement(new ScreenAddress(chem, 0, 1), 0, Theme.Chemistry, 1),
            }, rolls);
            Assert.IsEmpty(ChemistryPlan.FindProblems(model, ok, order));
            Assert.IsEmpty(SeedSweep.FindOptionalGateProblems(model, ok));
            ChemistryPlan plan = ChemistryPlan.Create(model, ok, order);
            Assert.AreEqual(IsotopeStage.Glow, plan.Stage);
            CollectionAssert.AreEquivalent(new[] { IsotopeStage.Lead, IsotopeStage.Unstable },
                plan.Gates.Where(s => s.Optional).Select(s => s.Stage));

            // Room for both other stones, but one has no optional gate: flagged.
            var missingOne = new ItemPlacement(11, required, new[] { pickup }, new[]
            {
                new OptionalGatePlacement(new ScreenAddress(chem, 1, 0), 0, Theme.Chemistry, 2),
                new OptionalGatePlacement(new ScreenAddress(chem, 0, 1), 0, Theme.Chemistry, 2),
            }, rolls);
            Assert.IsTrue(ChemistryPlan.FindProblems(model, missingOne, order).Any(x => x.Contains("no optional Unstable gate")));

            // An optional gate guarding the stone's pickup: the sweep's optional-gate rule flags it.
            var guarding = new ItemPlacement(11, required, new[] { new PickupPlacement(Theme.Chemistry, new ScreenAddress(chem, 1, 0), 0) },
                new[] { new OptionalGatePlacement(new ScreenAddress(chem, 1, 0), 0, Theme.Chemistry, 2) }, rolls);
            Assert.IsTrue(SeedSweep.FindOptionalGateProblems(model, guarding).Any(x => x.Contains("guards required content")));

            // An optional gate of the rolled stone, and one off Chemistry.
            var wrong = new ItemPlacement(11, required, new[] { pickup }, new[]
            {
                new OptionalGatePlacement(new ScreenAddress(chem, 1, 0), 0, Theme.Chemistry, rolled),
                new OptionalGatePlacement(new ScreenAddress(bio, 1, 0), 0, Theme.Chemistry, 1),
            }, rolls);
            List<string> sweep = SeedSweep.FindOptionalGateProblems(model, wrong);
            Assert.IsTrue(sweep.Any(x => x.Contains("rolled variant")), string.Join("; ", sweep));
            Assert.IsTrue(sweep.Any(x => x.Contains("home face")), string.Join("; ", sweep));
            List<string> chemistry = ChemistryPlan.FindProblems(model, wrong, order);
            Assert.IsTrue(chemistry.Any(x => x.Contains("belong on Chemistry")), string.Join("; ", chemistry));

            // No required gate off the home face.
            var homeOnly = new ItemPlacement(11, new[] { required[0] }, new[] { pickup }, null, rolls);
            Assert.IsTrue(ChemistryPlan.FindProblems(model, homeOnly, order).Any(x => x.Contains("no gate on another face")));

            Assert.IsNull(ChemistryPlan.Create(model, new ItemPlacement(11, required, new[] { pickup }), order),
                "No rolled Chemistry variant: no stones in the run");
            Assert.IsNull(ChemistryPlan.Create(model, ok, null), "No stone kinds: no guessing from the catalog order");
        }

        [Test]
        public void Sweep_Passes50Of50_WithTheChemistryRules()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            List<SeedSweep.SeedResult> results = SeedSweep.Run(catalog: library, itemThemes: catalog.Themes(),
                variantCounts: catalog.VariantCounts(), biologyVariantKinds: BiologyBeaks.VariantKinds(catalog),
                chemistryVariantKinds: ChemistryIsotope.VariantKinds(catalog));
            string report = SeedSweep.Describe(results);
            Assert.IsTrue(results.All(r => r.Passed), report);
            StringAssert.Contains("the rolled stone opening a gate on another face", report);
            StringAssert.Contains("optional gates guarding nothing required", report);
            StringAssert.DoesNotContain("dispenser", report);
        }

        [Test]
        public void TrialFitRule_IsCheckedWhenModulesAreKnown()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            ItemPlacement p = Place(1, library, catalog, out CubeModel model);
            List<string> missing = ChemistryPlan.FindProblems(model, p, ChemistryIsotope.VariantKinds(catalog), _ => null);
            Assert.IsTrue(missing.Any(x => x.Contains("trial screen")), string.Join("; ", missing));
            CubeLayout layout = CubeLayout.Generate(model, library);
            Assert.IsEmpty(ChemistryPlan.FindProblems(model, p, ChemistryIsotope.VariantKinds(catalog), SeedSweep.ModuleLookup(layout, library)));
        }

        [Test]
        public void PlatesButtonsFlowersAndOpenPickups_NeverShareASpot()
        {
            // Any slot quadrant against any other: a lead plate is never where a Biology button would be.
            var slots = new[] { new Vector2(4.9f, 1.4f), new Vector2(-3.8f, -1.4f), new Vector2(-4.9f, 1.4f), new Vector2(3.4f, -1.4f) };
            foreach (Vector2 a in slots)
                foreach (Vector2 b in slots)
                    Assert.Greater(Vector2.Distance(LeadPlateGate.PlateLocal(a), ButtonGate.ButtonLocal(b)), LeadPlate.Radius + BeakButton.Radius);

            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            List<BeakKind> kinds = BiologyBeaks.VariantKinds(catalog);
            const float minGap = 1.0f;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                ItemPlacement p = Place(seed, library, catalog, out CubeModel model);
                CubeLayout layout = CubeLayout.Generate(model, library);
                var spots = new Dictionary<ScreenAddress, List<(string what, Vector2 at)>>();
                void Add(ScreenAddress screen, string what, Vector2 at)
                {
                    if (!spots.TryGetValue(screen, out var list)) spots[screen] = list = new List<(string, Vector2)>();
                    list.Add((what, at));
                }
                Vector2 SlotLocal(ScreenAddress screen, int slot)
                {
                    Assert.IsTrue(layout.TryGetFace(screen.Face, out FaceLayout fl));
                    ScreenModule module = library.Module(fl.Theme, fl.ModuleAt(screen.Cell));
                    return module.Gates[slot].transform.position - module.transform.position;
                }

                foreach (BeakGateSpec s in BiologyPlan.Create(model, p, kinds).Gates)
                {
                    if (s.Kind == BeakGateKind.Button) Add(s.Screen, $"button {s}", ButtonGate.ButtonLocal(SlotLocal(s.Screen, s.Slot)));
                    if (s.Kind == BeakGateKind.FlowerVine) Add(s.FlowerScreen, $"flower {s}", FlowerVineGate.FlowerLocal);
                }
                foreach (IsotopeGateSpec s in Plan(model, p, catalog).Gates) // optional plates included
                    if (s.Stage == IsotopeStage.Lead) Add(s.Screen, $"plate {s}", LeadPlateGate.PlateLocal(SlotLocal(s.Screen, s.Slot)));
                foreach (PickupPlacement k in p.Pickups)
                    if (!k.IsGuarded) Add(k.Screen, $"pickup {k}", CubeWorld.OpenPickupOffset);

                foreach (KeyValuePair<ScreenAddress, List<(string what, Vector2 at)>> e in spots)
                    for (int i = 0; i < e.Value.Count; i++)
                        for (int j = i + 1; j < e.Value.Count; j++)
                            Assert.Greater(Vector2.Distance(e.Value[i].at, e.Value[j].at), minGap,
                                $"seed {seed} {e.Key}: {e.Value[i].what} and {e.Value[j].what} share a spot");
            }
        }

        [Test]
        public void PhysicsMarksTheRealLeadPlates_ReadFromTheItemIds_InAnyCatalogOrder()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            var reversed = ScriptableObject.CreateInstance<ItemCatalog>();
            try
            {
                reversed.Set(catalog.Items.ToList());
                reversed.SetVariants(catalog.VariantsOf(Theme.Biology)
                    .Concat(catalog.VariantsOf(Theme.Chemistry).AsEnumerable().Reverse()).ToList());
                List<IsotopeStage> kinds = ChemistryIsotope.VariantKinds(reversed);
                CollectionAssert.AreEqual(ChemistryIsotope.VariantOrder.Reverse(), kinds);
                List<BeakKind> beaks = BiologyBeaks.VariantKinds(reversed);
                int plates = 0, wrongOrderDiffers = 0;
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var model = new CubeModel(seed);
                    CubeLayout layout = CubeLayout.Generate(model, library);
                    ItemPlacement p = ItemPlacement.ForRun(model, layout, library, reversed.Themes(), reversed.VariantCounts());
                    var moduleAt = SeedSweep.ModuleLookup(layout, library);

                    // The real plates, straight from the items the reveal builds the gates with.
                    bool leadRun = ChemistryIsotope.KindOf(reversed.ItemFor(Theme.Chemistry, seed)) == IsotopeStage.Lead;
                    var real = new List<ScreenAddress>();
                    foreach (GatePlacement g in p.Gates)
                        if (g.Item == Theme.Chemistry && leadRun) real.Add(g.Screen);
                    foreach (OptionalGatePlacement o in p.OptionalGates)
                        if (o.Item == Theme.Chemistry &&
                            ChemistryIsotope.KindOf(reversed.VariantsOf(Theme.Chemistry)[o.Variant]) == IsotopeStage.Lead)
                            real.Add(o.Screen);

                    PhysicsPlan physics = PhysicsPlan.Create(model, p, moduleAt, beaks, PhysicsPlan.DefaultMaxRequired, kinds);
                    PhysicsPlan byOrder = PhysicsPlan.Create(model, p, moduleAt, beaks, PhysicsPlan.DefaultMaxRequired,
                        ChemistryIsotope.VariantOrder);
                    foreach (ScreenAddress screen in physics.Doors.Select(d => d.Screen).Append(physics.TrialScreen).Distinct())
                    {
                        int marked = physics.OtherMarks(screen).Count(m => m.What.StartsWith("lead plate"));
                        Assert.AreEqual(real.Count(s => s == screen), marked, $"seed {seed} {screen}: Physics marks the real plates");
                        plates += marked;
                        if (byOrder.OtherMarks(screen).Count(m => m.What.StartsWith("lead plate")) != marked) wrongOrderDiffers++;
                    }
                }
                Assert.Greater(plates, 0, "Some Physics screens carry a Leadstone* plate");
                Assert.Greater(wrongOrderDiffers, 0, "Reading stages from the catalog order would mark the wrong plates");
            }
            finally
            {
                Object.DestroyImmediate(reversed);
            }
        }

        // ---------- The trial ----------

        [Test]
        public void Trial_FitsBesideTheLanes_OnEverySeedsTrialScreen()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                ItemPlacement p = Place(seed, library, catalog, out CubeModel model);
                ChemistryPlan plan = Plan(model, p, catalog);
                Assert.IsTrue(CubeLayout.Generate(model, library).TryGetFace(plan.Face, out FaceLayout fl));
                ScreenModule module = library.Module(Theme.Chemistry, fl.ModuleAt(plan.TrialScreen.Cell));
                // The trial takes the first draws of the population stream, as ChemistryFace.EndReveal does.
                var rng = new SeededRng(unchecked((ulong)(uint)seed), ChemistryFace.PopulationRngStream);
                List<Vector2> picked = ChemistryFace.TrialSpots(module, rng);
                Assert.AreEqual(ChemistryTrial.PieceCount, picked.Count, $"seed {seed}: the trial fits on {plan.TrialScreen}");
                for (int i = 0; i < picked.Count; i++)
                    for (int j = i + 1; j < picked.Count; j++)
                        Assert.GreaterOrEqual(Vector2.Distance(picked[i], picked[j]), BiologyTrial.PieceSpacing - 1e-4f);
            }
            Assert.AreEqual(ChemistryTrialPieceKind.Lamp, ChemistryTrial.KindFor(IsotopeStage.Glow));
            Assert.AreEqual(ChemistryTrialPieceKind.CrackedWall, ChemistryTrial.KindFor(IsotopeStage.Unstable));
            Assert.AreEqual(ChemistryTrialPieceKind.Plate, ChemistryTrial.KindFor(IsotopeStage.Lead));
        }

        // ---------- Mushroom lines ----------

        [Test]
        public void MushroomLines_NameTheRolledStone_TellTheChangingStonesLore_AndEndWithATrueHint()
        {
            ItemCatalog catalog = Catalog();
            foreach (int seed in new[] { 3, 4, 5, 6, 7, 8 })
            {
                var model = new CubeModel(seed);
                var facts = new RunFacts(model);
                IsotopeStage stage = ChemistryIsotope.KindOf(catalog.ItemFor(Theme.Chemistry, seed)).Value;
                for (int i = 0; i < ChemistryPopulation.MushroomCount(model.FaceSize); i++)
                {
                    NpcSpec spec = ChemistryPopulation.MushroomSpec(seed, i);
                    Assert.AreEqual(Race.Mushroom, spec.Race);
                    Assert.AreEqual(spec.Signature(), ChemistryPopulation.MushroomSpec(seed, i).Signature());
                    List<string> lines = ChemistryPopulation.LinesFor(spec, facts, HintDensity.Sparse, stage, i);
                    Assert.AreEqual(Dialogue.MaxLines, lines.Count, "A flavour line, then the hint");
                    if (i == ChemistryPopulation.StoneTeller)
                    {
                        Assert.AreEqual(Dialogue.StoneLine(stage), lines[0], "Mushroom 0 names this visit's stone");
                        StringAssert.Contains(ChemistryIsotope.StageName(stage), lines[0]);
                        foreach (IsotopeStage other in ChemistryIsotope.VariantOrder.Where(s => s != stage))
                            StringAssert.DoesNotContain(ChemistryIsotope.StageName(other), lines[0]);
                    }
                    else if (i == ChemistryPopulation.LoreTeller) Assert.AreEqual(Dialogue.StoneLoreLine, lines[0]);
                    else CollectionAssert.Contains(Dialogue.CurieLines, lines[0]);
                    Hint hint = HintGenerator.Generate(facts, spec.HintKind, spec.Salt, HintDensity.Sparse)[0];
                    Assert.AreEqual(hint.Text, lines[1], "The first hint ends the conversation");
                }
            }
            // The stone lines are true: each names its stone and what it opens.
            StringAssert.Contains("dark doorway", Dialogue.StoneLine(IsotopeStage.Glow));
            StringAssert.Contains("cracked wall", Dialogue.StoneLine(IsotopeStage.Unstable));
            StringAssert.Contains("pressure plate", Dialogue.StoneLine(IsotopeStage.Lead));
            foreach (string line in Dialogue.StoneLines) StringAssert.Contains("swing", line);
            // The lore: the stones turn into one another between visits.
            string lore = Dialogue.StoneLoreLine.ToLowerInvariant();
            StringAssert.Contains("stones", lore);
            StringAssert.Contains("come back", lore);
            StringAssert.DoesNotContain("round and round", lore, "No fixed order: each run rolls its stone");
            Assert.IsTrue(Dialogue.CurieLines.Any(l => l.Contains("skeleton-key fingers")), "The skeleton-key gag stays");
            CollectionAssert.IsSubsetOf(Dialogue.StoneLines.Append(Dialogue.StoneLoreLine).Concat(Dialogue.CurieLines),
                Dialogue.AllFixedText().ToList());
        }

        // ---------- The subtle-science text rule ----------

        /// <summary>Every in-game string the Chemistry face shows (lines, names, HUD uses, hint nickname, catalog names).</summary>
        private static List<string> ChemistryStrings()
        {
            var strings = new List<string>();
            strings.AddRange(Dialogue.CurieLines);
            strings.Add(Dialogue.StoneLoreLine);
            strings.AddRange(Dialogue.StoneLines);
            Dialogue.RaceFlavour mushroom = Dialogue.Flavours[Race.Mushroom];
            strings.Add(mushroom.Wares);
            strings.Add(mushroom.Metal);
            strings.Add(mushroom.Errand);
            strings.AddRange(mushroom.Rumours);
            strings.AddRange(mushroom.Greetings);
            strings.AddRange(ChemistryIsotope.AllText());
            strings.Add(HintGenerator.ItemNickname(Theme.Chemistry));
            ItemCatalog catalog = Catalog();
            strings.Add(catalog.ForTheme(Theme.Chemistry).DisplayName);
            strings.AddRange(catalog.VariantsOf(Theme.Chemistry).Select(i => i.DisplayName));
            strings.AddRange(catalog.VariantsOf(Theme.Chemistry).Select(PickupFeedback.ItemText));
            return strings;
        }

        [Test]
        public void ChemistryText_UsesNoBannedScienceWords_AndPlaceholderNamesCarryAStar()
        {
            List<string> strings = ChemistryStrings();
            Assert.Greater(strings.Count, 20);
            foreach (string text in strings)
            {
                string lower = text.ToLowerInvariant();
                foreach (string banned in BannedWords)
                    StringAssert.DoesNotContain(banned, lower, $"\"{text}\" uses a science word");
                StringAssert.DoesNotContain(Dialogue.ForbiddenWord, lower, text);
            }

            // Proper names: a capitalised word that does not start a sentence is a name, and names carry a star.
            var notNames = new HashSet<string> { "I", "I'm", "I'll", "I've", "I'd" };
            foreach (string text in strings)
            {
                string[] words = text.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < words.Length; i++)
                {
                    string word = words[i].TrimStart('.', '"', '\'', '(', '+', '*');
                    if (word.Length == 0 || !char.IsUpper(word[0])) continue;
                    bool sentenceStart = i == 0 || ".!?:".IndexOf(words[i - 1][words[i - 1].Length - 1]) >= 0;
                    int end = 0;
                    while (end < word.Length && (char.IsLetter(word[end]) || word[end] == '\'' || word[end] == '-')) end++;
                    string core = word.Substring(0, end);
                    bool starred = end < word.Length && word[end] == '*';
                    if (sentenceStart || notNames.Contains(core)) continue;
                    Assert.IsTrue(starred, $"\"{core}\" looks like a name without its star in \"{text}\"");
                }
            }
            StringAssert.Contains("Marcel*", string.Join(" ", Dialogue.Flavours[Race.Mushroom].Rumours));

            // Placeholder names: every stone, the base item and the patron carry a trailing star wherever they appear.
            var names = ChemistryIsotope.VariantOrder.Select(ChemistryIsotope.StageName).Append(ChemistryIsotope.Name).ToList();
            foreach (string name in names) StringAssert.EndsWith("*", name);
            foreach (string text in strings)
            {
                foreach (string bare in names.Select(n => n.TrimEnd('*')).Append("Glowmother").Append("Marcel"))
                {
                    int at = text.IndexOf(bare, System.StringComparison.Ordinal);
                    while (at >= 0)
                    {
                        int after = at + bare.Length;
                        Assert.IsTrue(after < text.Length && text[after] == '*', $"\"{bare}\" without its star in \"{text}\"");
                        at = text.IndexOf(bare, after, System.StringComparison.Ordinal);
                    }
                }
            }
        }
    }
}
