using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The Chemistry face as pure data: the isotope's decay timing and half-life knob, the catalog's isotope (the
    /// placeholder's stable id), plan determinism, stage coverage (timed stages near the dispenser, lead off-home),
    /// the dispenser-first sweep rule, the trial fitting on every seed, and the mushrooms' Curie lines.
    /// </summary>
    public class ChemistryTests
    {
        private const string LibraryPath = "Assets/Modules/ModuleLibrary.asset";
        private const string ItemCatalogPath = "Assets/Items/ItemCatalog.asset";
        private const int Seeds = 50;

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

        // ---------- Decay ----------

        [Test]
        public void Decay_GlowsThenTurnsUnstableThenLead_AtTheHalfLifeThresholds()
        {
            const float h = ChemistryIsotope.DefaultHalfLife;
            Assert.AreEqual(12f, ChemistryIsotope.GlowEnds(h), 1e-4f, "Glow 0-12 s");
            Assert.AreEqual(20f, ChemistryIsotope.UnstableEnds(h), 1e-4f, "Unstable 12-20 s");
            Assert.AreEqual(IsotopeStage.Glow, ChemistryIsotope.StageAt(0f, h));
            Assert.AreEqual(IsotopeStage.Glow, ChemistryIsotope.StageAt(11.99f, h));
            Assert.AreEqual(IsotopeStage.Unstable, ChemistryIsotope.StageAt(12f, h));
            Assert.AreEqual(IsotopeStage.Unstable, ChemistryIsotope.StageAt(19.99f, h));
            Assert.AreEqual(IsotopeStage.Lead, ChemistryIsotope.StageAt(20f, h));
            foreach (float age in new[] { 25f, 100f, 1e6f })
                Assert.AreEqual(IsotopeStage.Lead, ChemistryIsotope.StageAt(age, h), "Lead stays lead");
            Assert.AreEqual(0f, ChemistryIsotope.StageStart(IsotopeStage.Glow, h));
            Assert.AreEqual(12f, ChemistryIsotope.StageStart(IsotopeStage.Unstable, h), 1e-4f);
            Assert.AreEqual(20f, ChemistryIsotope.StageStart(IsotopeStage.Lead, h), 1e-4f);
        }

        [Test]
        public void HalfLife_IsTheDifficultyKnob_ShorterMeansEveryStageComesSooner()
        {
            Assert.AreEqual(6f, ChemistryIsotope.GlowEnds(6f), 1e-4f);
            Assert.AreEqual(10f, ChemistryIsotope.UnstableEnds(6f), 1e-4f);
            Assert.AreEqual(IsotopeStage.Unstable, ChemistryIsotope.StageAt(8f, 6f), "Harder: unstable at 8 s");
            Assert.AreEqual(IsotopeStage.Glow, ChemistryIsotope.StageAt(8f, 12f), "Default: still glowing at 8 s");
            Assert.AreEqual(IsotopeStage.Lead, ChemistryIsotope.StageAt(10f, 6f));
            Assert.AreEqual(ChemistryIsotope.MinHalfLife, ChemistryIsotope.GlowEnds(0f), 1e-4f, "Clamped to the minimum");
        }

        // ---------- The item ----------

        [Test]
        public void Catalog_ChemistryItem_IsTheIsotope_WithThePlaceholdersId_AndNoVariants()
        {
            ItemCatalog catalog = Catalog();
            ItemDefinition isotope = catalog.ForTheme(Theme.Chemistry);
            Assert.IsNotNull(isotope);
            Assert.AreEqual("chemistry-item", isotope.Id, "The id stays stable");
            Assert.AreEqual(ChemistryIsotope.Id, isotope.Id);
            Assert.AreEqual(ChemistryIsotope.Name, isotope.DisplayName);
            Assert.AreEqual(Theme.Chemistry, isotope.HomeTheme);
            Assert.IsEmpty(catalog.VariantsOf(Theme.Chemistry), "One logical item for the 1.4 placer");
            for (int seed = 1; seed <= Seeds; seed++)
                Assert.AreSame(isotope, catalog.ItemFor(Theme.Chemistry, seed));
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
                ChemistryPlan a = ChemistryPlan.Create(model, p);
                ChemistryPlan b = ChemistryPlan.Create(new CubeModel(seed), Place(seed, library, catalog, out _));
                Assert.IsNotNull(a, $"seed {seed}");
                Assert.AreEqual(a.Signature(), b.Signature(), $"seed {seed}: same seed, same plan");
                Assert.IsEmpty(ChemistryPlan.FindProblems(model, p), $"seed {seed}");
                Assert.AreEqual(model.FaceOf(Theme.Chemistry), a.TrialScreen.Face);
                Assert.IsTrue(p.TryGetPickup(Theme.Chemistry, out PickupPlacement pickup));
                Assert.AreEqual(pickup, a.Dispenser, "The dispenser sits at the Chemistry pickup's spot");
                signatures.Add(a.Signature());
            }
            Assert.Greater(signatures.Count, Seeds / 2, "Plans vary across seeds");
        }

        [Test]
        public void Plan_StageCoverage_EveryStageUsed_TimedStagesNearTheDispenser_LeadOffHome()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            var homeStagesSeen = new HashSet<(IsotopeStage, IsotopeStage)>();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                ItemPlacement p = Place(seed, library, catalog, out CubeModel model);
                ChemistryPlan plan = ChemistryPlan.Create(model, p);
                int required = p.Gates.Count(g => g.Item == Theme.Chemistry);
                Assert.AreEqual(required, plan.Gates.Count, $"seed {seed}: a stage for every Chemistry gate");
                Assert.GreaterOrEqual(plan.Gates.Count, 3, $"seed {seed}");
                foreach (IsotopeStage stage in ChemistryPlan.Stages)
                    Assert.Greater(plan.CountOf(stage), 0, $"seed {seed}: some {stage} gate");
                Assert.IsTrue(plan.Gates.Any(s => s.Screen.Face != plan.Face), $"seed {seed}: the isotope opens a gate on another face");
                foreach (IsotopeGateSpec s in plan.Gates)
                {
                    Assert.IsTrue(p.TryGetGate(s.Screen, s.Slot, out GatePlacement g) && g.Item == Theme.Chemistry, s.ToString());
                    Assert.AreEqual(s.Screen.Face == plan.Face, s.OnHome);
                    if (!s.OnHome) Assert.AreEqual(IsotopeStage.Lead, s.Stage, $"seed {seed}: {s} is far from the dispenser");
                }
                List<IsotopeStage> home = plan.Gates.Where(s => s.OnHome).Select(s => s.Stage).ToList();
                if (home.Count == 2) homeStagesSeen.Add((home[0], home[1]));
            }
            Assert.Greater(homeStagesSeen.Count, 1, "The home stages are dealt by seed");
        }

        [Test]
        public void Plan_WithFewGates_CoversWhatItCan_AndWithManyEveryStage()
        {
            var model = new CubeModel(5);
            FaceId chem = model.FaceOf(Theme.Chemistry);
            var pickup = new PickupPlacement(Theme.Chemistry, new ScreenAddress(chem, 0, 0));

            // One home gate and one off-home gate: lead goes off-home, the home gate gets a timed stage.
            FaceId bio = model.FaceOf(Theme.Biology);
            var two = new ItemPlacement(5, new[]
            {
                new GatePlacement(new ScreenAddress(chem, 1, 0), 0, Theme.Chemistry),
                new GatePlacement(new ScreenAddress(bio, 0, 0), 0, Theme.Chemistry),
            }, new[] { pickup });
            ChemistryPlan small = ChemistryPlan.Create(model, two);
            Assert.AreEqual(2, small.Gates.Count);
            Assert.AreNotEqual(IsotopeStage.Lead, small.Gates[0].Stage, "The home gate gets a timed stage");
            Assert.AreEqual(IsotopeStage.Lead, small.Gates[1].Stage, "The off-home gate is lead");

            // Four home gates and no off-home gate: every stage, lead included, is used.
            var gates = new List<GatePlacement>();
            for (int i = 0; i < 4; i++) gates.Add(new GatePlacement(new ScreenAddress(chem, i % 2, i / 2), 0, Theme.Chemistry));
            ChemistryPlan big = ChemistryPlan.Create(model, new ItemPlacement(5, gates, new[] { pickup }));
            foreach (IsotopeStage stage in ChemistryPlan.Stages)
                Assert.Greater(big.CountOf(stage), 0, stage.ToString());

            Assert.IsNull(ChemistryPlan.Create(model, new ItemPlacement(5, gates, new PickupPlacement[0])),
                "No Chemistry pickup: Chemistry is not an item of the run");
        }

        [Test]
        public void Plan_SmallHandMadeLayouts_AlwaysMeetTheCoverageRuleFindProblemsChecks()
        {
            // (home gates, off-home gates) -> the stages the rule requires.
            var cases = new (int home, int off, IsotopeStage[] required)[]
            {
                (1, 0, new IsotopeStage[0]),
                (1, 1, new[] { IsotopeStage.Lead }),
                (1, 2, new[] { IsotopeStage.Lead }),
                (2, 0, new[] { IsotopeStage.Glow, IsotopeStage.Unstable }),
                (2, 1, ChemistryPlan.Stages),
                (3, 0, ChemistryPlan.Stages),
                (4, 0, ChemistryPlan.Stages),
            };
            foreach ((int home, int off, IsotopeStage[] required) in cases)
            {
                CollectionAssert.AreEquivalent(required, ChemistryPlan.RequiredStages(home, off), $"{home} home, {off} off-home");
                for (int seed = 1; seed <= 30; seed++)
                {
                    var model = new CubeModel(seed);
                    FaceId chem = model.FaceOf(Theme.Chemistry);
                    FaceId[] others = Enumerable.Range(0, CubeSettings.FaceCount).Select(f => (FaceId)f)
                        .Where(f => f != chem && CubeLayout.IsLaidOut(model, f)).ToArray();
                    var gates = new List<GatePlacement>();
                    for (int i = 0; i < home; i++) gates.Add(new GatePlacement(new ScreenAddress(chem, i % 2, i / 2), 0, Theme.Chemistry));
                    for (int i = 0; i < off; i++) gates.Add(new GatePlacement(new ScreenAddress(others[i % others.Length], i % 2, 0), 1, Theme.Chemistry));
                    var placement = new ItemPlacement(seed, gates, new[] { new PickupPlacement(Theme.Chemistry, new ScreenAddress(chem, 1, 1)) });
                    ChemistryPlan plan = ChemistryPlan.Create(model, placement);
                    string context = $"seed {seed}, {home} home, {off} off-home: {plan.Signature()}";
                    foreach (IsotopeStage stage in required) Assert.Greater(plan.CountOf(stage), 0, context);
                    Assert.IsTrue(plan.Gates.Where(s => s.OnHome).Any(s => s.Stage != IsotopeStage.Lead), $"{context}: a timed home gate");
                    Assert.IsTrue(plan.Gates.Where(s => !s.OnHome).All(s => s.Stage == IsotopeStage.Lead), context);
                    List<string> problems = ChemistryPlan.FindProblems(model, placement);
                    Assert.IsFalse(problems.Any(p => p.Contains(" gate (") || p.Contains("timed")), $"{context}: {string.Join("; ", problems)}");
                }
            }
        }

        [Test]
        public void TrialFitRule_IsCheckedWhenModulesAreKnown()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            ItemPlacement p = Place(1, library, catalog, out CubeModel model);
            List<string> missing = ChemistryPlan.FindProblems(model, p, _ => null);
            Assert.IsTrue(missing.Any(x => x.Contains("trial screen")), string.Join("; ", missing));
            CubeLayout layout = CubeLayout.Generate(model, library);
            List<string> real = ChemistryPlan.FindProblems(model, p, s =>
                layout.TryGetFace(s.Face, out FaceLayout fl) ? library.Module(fl.Theme, fl.ModuleAt(s.Cell)) : null);
            Assert.IsEmpty(real);
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
                foreach (IsotopeGateSpec s in ChemistryPlan.Create(model, p).Gates)
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

        // ---------- Dispenser first ----------

        [Test]
        public void DispenserFirst_HoldsOnEverySeed_AndTheSweepRunsIt()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            int guarded = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                ItemPlacement p = Place(seed, library, catalog, out CubeModel model);
                Assert.IsEmpty(ChemistryPlan.DispenserProblems(model, p), $"seed {seed}");
                if (p.TryGetPickup(Theme.Chemistry, out PickupPlacement k) && k.IsGuarded) guarded++;
            }
            Assert.Greater(guarded, 0, "Some seeds put the dispenser behind another item's gate (still no Chemistry gate first)");

            List<SeedSweep.SeedResult> results = SeedSweep.Run(catalog: library, itemThemes: catalog.Themes(),
                variantCounts: catalog.VariantCounts(), biologyVariantKinds: BiologyBeaks.VariantKinds(catalog));
            Assert.IsTrue(results.All(r => r.Passed), SeedSweep.Describe(results));
            StringAssert.Contains("isotope dispenser before any Chemistry-stage gate", SeedSweep.Describe(results));
        }

        [Test]
        public void DispenserFirst_FlagsADispenserBehindAChemistryStageGate_DirectlyOrDownTheChain()
        {
            var model = new CubeModel(11);
            FaceId bio = model.FaceOf(Theme.Biology), chem = model.FaceOf(Theme.Chemistry);
            var chemHome = new GatePlacement(new ScreenAddress(chem, 0, 0), 0, Theme.Chemistry);
            var chemOff = new GatePlacement(new ScreenAddress(bio, 0, 0), 0, Theme.Chemistry);
            var bioOnChem = new GatePlacement(new ScreenAddress(chem, 1, 1), 0, Theme.Biology);
            var bioHome = new GatePlacement(new ScreenAddress(bio, 1, 0), 0, Theme.Biology);
            var gates = new[] { chemHome, chemOff, bioOnChem, bioHome };

            // Fine: the dispenser behind a Biology gate whose beak lies in the open.
            var ok = new ItemPlacement(11, gates, new[]
            {
                new PickupPlacement(Theme.Biology, new ScreenAddress(bio, 1, 1)),
                new PickupPlacement(Theme.Chemistry, bioOnChem.Screen, bioOnChem.Slot),
            });
            Assert.IsEmpty(ChemistryPlan.DispenserProblems(model, ok));

            // Directly behind a Chemistry-stage gate.
            var direct = new ItemPlacement(11, gates, new[]
            {
                new PickupPlacement(Theme.Biology, new ScreenAddress(bio, 1, 1)),
                new PickupPlacement(Theme.Chemistry, chemHome.Screen, chemHome.Slot),
            });
            List<string> d = ChemistryPlan.DispenserProblems(model, direct);
            Assert.IsTrue(d.Any(p => p.Contains("dispenser-first") && p.Contains("Chemistry-stage gate")), string.Join("; ", d));

            // Down the chain: behind a Biology gate, whose beak is behind a Chemistry gate.
            var chain = new ItemPlacement(11, gates, new[]
            {
                new PickupPlacement(Theme.Biology, chemOff.Screen, chemOff.Slot),
                new PickupPlacement(Theme.Chemistry, bioOnChem.Screen, bioOnChem.Slot),
            });
            List<string> c = ChemistryPlan.DispenserProblems(model, chain);
            Assert.IsTrue(c.Any(p => p.Contains("dispenser-first") && p.Contains("Chemistry-stage gate")), string.Join("; ", c));
            Assert.IsTrue(ChemistryPlan.FindProblems(model, chain).Any(p => p.Contains("dispenser-first")), "FindProblems runs the rule");
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
                ChemistryPlan plan = ChemistryPlan.Create(model, p);
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
        }

        // ---------- Mushroom lines ----------

        [Test]
        public void MushroomLines_AreCurieFlavoured_WithTheSkeletonKeyGag_AndTheStageLine()
        {
            var model = new CubeModel(3);
            var facts = new RunFacts(model);
            for (int i = 0; i < ChemistryPopulation.MushroomCount; i++)
            {
                NpcSpec spec = ChemistryPopulation.MushroomSpec(3, i);
                Assert.AreEqual(Race.Mushroom, spec.Race);
                Assert.AreEqual(spec.Signature(), ChemistryPopulation.MushroomSpec(3, i).Signature());
                List<string> lines = ChemistryPopulation.LinesFor(spec, facts, HintDensity.Sparse, i == 0);
                CollectionAssert.Contains(Dialogue.Flavours[Race.Mushroom].Greetings, lines[0]);
                CollectionAssert.Contains(Dialogue.CurieLines, lines[1]);
                if (i == 0) Assert.AreEqual(Dialogue.IsotopeStageLine, lines[lines.Count - 1]);
                else CollectionAssert.DoesNotContain(lines, Dialogue.IsotopeStageLine);
                foreach (string line in lines)
                    StringAssert.DoesNotContain(Dialogue.ForbiddenWord, line.ToLowerInvariant());
            }
            Assert.IsTrue(Dialogue.CurieLines.Any(l => l.Contains("Glow responsibly!")));
            Assert.IsTrue(Dialogue.CurieLines.Any(l => l.Contains("skeleton-key fingers")));
            CollectionAssert.Contains(Dialogue.AllFixedText().ToList(), Dialogue.IsotopeStageLine);
            // The stage line is true: it names what each stage does, in decay order.
            string stageLine = Dialogue.IsotopeStageLine.ToLowerInvariant();
            Assert.Less(stageLine.IndexOf("glow"), stageLine.IndexOf("unstable"));
            Assert.Less(stageLine.IndexOf("unstable"), stageLine.IndexOf("lead"));
            StringAssert.Contains("dark room", stageLine);
            StringAssert.Contains("cracked wall", stageLine);
            StringAssert.Contains("plate", stageLine);
        }
    }
}
