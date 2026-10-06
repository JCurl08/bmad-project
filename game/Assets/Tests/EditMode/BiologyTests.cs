using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The Biology face as pure data: the seeded beak roll, the catalog's beak variants, placement determinism
    /// with variants, the optional-gate sweep rule (optional gates never guard required content), and the
    /// Biology plan (gate kinds, the thin-beak flower bridge, difficulty, the trial screen), plus finch lines.
    /// </summary>
    public class BiologyTests
    {
        private const string LibraryPath = "Assets/Modules/ModuleLibrary.asset";
        private const string ItemCatalogPath = "Assets/Items/ItemCatalog.asset";
        private const int Seeds = 50;

        private static readonly Dictionary<Theme, int> BeakCounts = new Dictionary<Theme, int> { [Theme.Biology] = 2 };

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

        private static ItemPlacement Place(int seed, ModuleLibrary library, out CubeModel model)
        {
            model = new CubeModel(seed);
            CubeLayout layout = CubeLayout.Generate(model, library);
            return ItemPlacement.ForRun(model, layout, library, null, BeakCounts);
        }

        // ---------- The roll ----------

        [Test]
        public void BeakRoll_SameSeedSameBeak_AndBothBeaksOccurOverSeeds1To50()
        {
            var seen = new HashSet<BeakKind>();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                BeakKind a = BiologyBeaks.Roll(seed);
                Assert.AreEqual(a, BiologyBeaks.Roll(seed), $"seed {seed}");
                Assert.AreEqual(ItemVariants.Roll(seed, Theme.Biology, 2), ItemVariants.Roll(seed, Theme.Biology, 2));
                seen.Add(a);
            }
            CollectionAssert.AreEquivalent(new[] { BeakKind.Thin, BeakKind.Thick }, seen);
        }

        [Test]
        public void BeakRoll_IsItsOwnStream_IndependentOfOtherThemes()
        {
            Assert.AreEqual(8UL, ItemVariants.RngStream);
            Assert.AreEqual(0, ItemVariants.Roll(5, Theme.Biology, 1), "One variant: always 0");
            int differs = Enumerable.Range(1, Seeds).Count(s =>
                ItemVariants.Roll(s, Theme.Biology, 2) != ItemVariants.Roll(s, Theme.Chemistry, 2));
            Assert.Greater(differs, 0, "Rolls are keyed by theme");
        }

        [Test]
        public void Catalog_HasBothBeaks_AsBiologyVariants_InRollOrder_AndKeepsItsItems()
        {
            ItemCatalog catalog = Catalog();
            Assert.AreEqual(3, catalog.Items.Count, "Existing entries are kept");
            List<ItemDefinition> beaks = catalog.VariantsOf(Theme.Biology);
            Assert.AreEqual(2, beaks.Count);
            Assert.AreEqual(BiologyBeaks.ThinId, beaks[0].Id);
            Assert.AreEqual(BiologyBeaks.ThickId, beaks[1].Id);
            foreach (ItemDefinition beak in beaks) Assert.AreEqual(Theme.Biology, beak.HomeTheme);
            Assert.AreEqual(BiologyBeaks.ThinReach, beaks[0].AttackReach, 1e-4f);
            Assert.AreEqual(0f, beaks[1].AttackReach, 1e-4f);
            Assert.AreEqual(2, catalog.VariantCounts()[Theme.Biology]);
            Assert.IsFalse(catalog.VariantCounts().ContainsKey(Theme.Chemistry));

            for (int seed = 1; seed <= Seeds; seed++)
            {
                ItemDefinition rolled = catalog.ItemFor(Theme.Biology, seed);
                Assert.AreEqual(BiologyBeaks.Roll(seed), BiologyBeaks.KindOf(rolled), $"seed {seed}");
                Assert.AreSame(rolled, catalog.ItemFor(Theme.Biology, seed));
                Assert.AreEqual(1, catalog.UnrolledVariants(Theme.Biology, seed).Count);
                Assert.AreNotSame(rolled, catalog.UnrolledVariants(Theme.Biology, seed)[0]);
                Assert.AreSame(catalog.ForTheme(Theme.Chemistry), catalog.ItemFor(Theme.Chemistry, seed));
            }
        }

        // ---------- Placement ----------

        [Test]
        public void Placement_WithVariants_IsDeterministic_AndKeepsThe14RequiredPlacement()
        {
            ModuleLibrary library = Library();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                ItemPlacement a = Place(seed, library, out CubeModel model);
                ItemPlacement b = Place(seed, library, out _);
                Assert.AreEqual(a.Signature(), b.Signature(), $"seed {seed}");
                Assert.AreEqual((int)BiologyBeaks.Roll(seed), a.RolledVariant(Theme.Biology));

                // Optional gates come last on the stream: the required gates and pickups are unchanged.
                ItemPlacement plain = ItemPlacement.ForRun(model, CubeLayout.Generate(model, library), library, null);
                CollectionAssert.AreEqual(plain.Gates, a.Gates, $"seed {seed}");
                CollectionAssert.AreEqual(plain.Pickups, a.Pickups, $"seed {seed}");
                Assert.IsEmpty(plain.OptionalGates);
            }
        }

        [Test]
        public void OptionalGates_AreTheOtherBeaks_OnBiology_InFreeSlots_GuardingNothing()
        {
            ModuleLibrary library = Library();
            int seedsWithOptional = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                ItemPlacement p = Place(seed, library, out CubeModel model);
                int rolled = p.RolledVariant(Theme.Biology);
                if (p.OptionalGates.Count > 0) seedsWithOptional++;
                Assert.LessOrEqual(p.OptionalGates.Count, ItemPlacement.OptionalGatesPerVariant);
                foreach (OptionalGatePlacement o in p.OptionalGates)
                {
                    Assert.AreEqual(Theme.Biology, o.Item);
                    Assert.AreNotEqual(rolled, o.Variant, $"seed {seed}: {o}");
                    Assert.AreEqual(model.FaceOf(Theme.Biology), o.Screen.Face);
                    Assert.IsFalse(p.TryGetGate(o.Screen, o.Slot, out _), $"seed {seed}: {o} shares a required slot");
                    Assert.IsFalse(p.Pickups.Any(k => k.IsGuarded && k.Screen == o.Screen && k.GuardSlot == o.Slot),
                        $"seed {seed}: {o} guards a pickup");
                }
                Assert.IsEmpty(SeedSweep.FindOptionalGateProblems(model, p), $"seed {seed}");
            }
            Assert.Greater(seedsWithOptional, Seeds / 2, "Most runs show the other beak's gates as extras");
        }

        [Test]
        public void Sweep_FlagsAnOptionalGateThatGuardsRequiredContent()
        {
            var model = new CubeModel(11);
            FaceId bio = model.FaceOf(Theme.Biology), chem = model.FaceOf(Theme.Chemistry);
            var gates = new List<GatePlacement>
            {
                new GatePlacement(new ScreenAddress(bio, 0, 0), 0, Theme.Biology),
                new GatePlacement(new ScreenAddress(chem, 0, 0), 0, Theme.Biology),
            };
            // The Biology pickup sits behind an optional (other beak) gate: the run would need the unrolled beak.
            var pickups = new List<PickupPlacement> { new PickupPlacement(Theme.Biology, new ScreenAddress(bio, 1, 1), 0) };
            var optional = new List<OptionalGatePlacement> { new OptionalGatePlacement(new ScreenAddress(bio, 1, 1), 0, Theme.Biology, 1) };
            var placement = new ItemPlacement(model.Seed, gates, pickups, optional, new Dictionary<Theme, int> { [Theme.Biology] = 0 });

            List<string> problems = SeedSweep.FindItemProblems(model, placement, new[] { Theme.Biology });
            string all = string.Join("; ", problems);
            Assert.IsTrue(problems.Any(p => p.Contains("guards required content")), all);
            Assert.IsTrue(problems.Any(p => p.Contains("softlock: Biology")), all);

            // An optional gate with the rolled variant, or in a required gate's slot, is flagged too.
            var bad = new ItemPlacement(model.Seed, gates, new[] { new PickupPlacement(Theme.Biology, new ScreenAddress(bio, 1, 1)) },
                new[] { new OptionalGatePlacement(new ScreenAddress(bio, 0, 0), 0, Theme.Biology, 0) },
                new Dictionary<Theme, int> { [Theme.Biology] = 0 });
            List<string> badProblems = SeedSweep.FindOptionalGateProblems(model, bad);
            Assert.IsTrue(badProblems.Any(p => p.Contains("rolled variant")), string.Join("; ", badProblems));
            Assert.IsTrue(badProblems.Any(p => p.Contains("shares its slot")), string.Join("; ", badProblems));
        }

        [Test]
        public void Sweep_50Seeds_WithTheBeakVariants_HasNoProblems()
        {
            ItemCatalog catalog = Catalog();
            List<SeedSweep.SeedResult> results = SeedSweep.Run(catalog: Library(), itemThemes: catalog.Themes(),
                variantCounts: catalog.VariantCounts());
            Assert.AreEqual(Seeds, results.Count);
            Assert.IsTrue(results.All(r => r.Passed), SeedSweep.Describe(results));
            Assert.IsTrue(results.All(r => r.Placement.RolledVariant(Theme.Biology) >= 0));
        }

        // ---------- The plan ----------

        [Test]
        public void Plan_GateKindsFollowTheRolledBeak_ThinRunsHaveOneAdjacentFlowerBridge()
        {
            ModuleLibrary library = Library();
            int thin = 0, thick = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                ItemPlacement p = Place(seed, library, out CubeModel model);
                BiologyPlan plan = BiologyPlan.Create(model, p);
                Assert.IsNotNull(plan);
                Assert.AreEqual(BiologyBeaks.Roll(seed), plan.Beak);
                Assert.AreEqual(plan.Signature(), BiologyPlan.Create(model, p).Signature(), "deterministic");
                Assert.IsEmpty(BiologyPlan.FindProblems(model, p), $"seed {seed}");
                Assert.AreEqual(model.FaceOf(Theme.Biology), plan.TrialScreen.Face);

                int required = p.Gates.Count(g => g.Item == Theme.Biology);
                Assert.AreEqual(required + p.OptionalGates.Count, plan.Gates.Count);
                Assert.IsTrue(plan.Gates.Any(s => !s.Optional && s.Screen.Face != plan.Face), $"seed {seed}: off-home beak gate");
                foreach (BeakGateSpec s in plan.Gates)
                {
                    Assert.AreEqual(plan.DifficultyOn(s.Screen.Face), s.Difficulty);
                    BeakKind beak = s.Optional ? BiologyBeaks.Other(plan.Beak) : plan.Beak;
                    Assert.AreEqual(beak, s.Beak, s.ToString());
                    if (s.Beak == BeakKind.Thick) Assert.AreEqual(BeakGateKind.Breakable, s.Kind);
                    else Assert.AreNotEqual(BeakGateKind.Breakable, s.Kind);
                }
                List<BeakGateSpec> flowers = plan.Gates.Where(s => s.Kind == BeakGateKind.FlowerVine).ToList();
                if (plan.Beak == BeakKind.Thin)
                {
                    thin++;
                    Assert.AreEqual(1, flowers.Count, $"seed {seed}");
                    BeakGateSpec f = flowers[0];
                    Assert.AreEqual(plan.Face, f.Screen.Face);
                    Assert.AreEqual(f.Screen.Face, f.FlowerScreen.Face);
                    Assert.AreEqual(1, Mathf.Abs(f.Screen.Cell.x - f.FlowerScreen.Cell.x) + Mathf.Abs(f.Screen.Cell.y - f.FlowerScreen.Cell.y),
                        "The flower is on an adjacent screen");
                }
                else
                {
                    thick++;
                    Assert.IsEmpty(flowers);
                }
            }
            Assert.Greater(thin, 0);
            Assert.Greater(thick, 0);
        }

        [Test]
        public void Trial_FitsBesideTheLanes_OnEverySeedsTrialScreen()
        {
            ModuleLibrary library = Library();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                ItemPlacement p = Place(seed, library, out CubeModel model);
                BiologyPlan plan = BiologyPlan.Create(model, p);
                Assert.IsTrue(CubeLayout.Generate(model, library).TryGetFace(plan.Face, out FaceLayout fl));
                ScreenModule module = library.Module(Theme.Biology, fl.ModuleAt(plan.TrialScreen.Cell));
                List<Vector2> spots = BiologyTrial.LaneSideSpots(TownPlan.BlockedRects(module));
                int needed = BiologyTrial.PiecesFor(plan.Beak);
                var rng = new SeededRng(unchecked((ulong)(uint)seed), BiologyFace.PopulationRngStream);
                List<Vector2> picked = BiologyTrial.PickSpots(spots, needed, rng);
                Assert.AreEqual(needed, picked.Count, $"seed {seed}: the {plan.Beak} trial fits on {plan.TrialScreen}");
                for (int i = 0; i < picked.Count; i++)
                {
                    Vector2 a = picked[i];
                    bool besideLane = Mathf.Abs(Mathf.Abs(a.y) - BiologyTrial.LaneSideOffset) < 1e-3f ||
                                      Mathf.Abs(Mathf.Abs(a.x) - BiologyTrial.LaneSideOffset) < 1e-3f;
                    Assert.IsTrue(besideLane, $"seed {seed}: {a}");
                    for (int j = i + 1; j < picked.Count; j++)
                        Assert.GreaterOrEqual(Vector2.Distance(a, picked[j]), BiologyTrial.PieceSpacing - 1e-4f);
                }
            }
        }

        [Test]
        public void NoSingleSwing_CanReachTwoTrialPieces()
        {
            var go = new GameObject("Swing Probe");
            try
            {
                var inventory = go.AddComponent<Inventory>();
                var equipment = go.AddComponent<Equipment>();
                var attack = go.AddComponent<PlayerAttack>();
                ItemDefinition thinItem = BiologyBeaks.CreateItem(BeakKind.Thin);
                ItemDefinition thickItem = BiologyBeaks.CreateItem(BeakKind.Thick);
                inventory.Add(thinItem);
                inventory.Add(thickItem);
                foreach (ItemDefinition item in new[] { thinItem, thickItem })
                {
                    Assert.IsTrue(equipment.Equip(item));
                    attack.Hitbox(out _, out Vector2 size);
                    // Two pieces both touching one box are at most its diagonal plus both bound radii apart.
                    Assert.LessOrEqual(size.magnitude, BiologyTrial.MaxSwingDiagonal, item.ToString());
                    Assert.Greater(BiologyTrial.PieceSpacing, size.magnitude + 2f * BiologyTrial.PieceBoundRadius, item.ToString());
                }
                Assert.LessOrEqual(TrialTarget.RockSize / 2f * Mathf.Sqrt(2f), BiologyTrial.PieceBoundRadius);
                Assert.LessOrEqual(TrialTarget.TriggerRadius, BiologyTrial.PieceBoundRadius);
                Object.DestroyImmediate(thinItem);
                Object.DestroyImmediate(thickItem);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RolledBeakItem_MatchesThePlan_AndOptionalGateItemsMatchTheirSpecs_InAnyCatalogOrder()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            CollectionAssert.AreEqual(BiologyBeaks.VariantOrder, BiologyBeaks.VariantKinds(catalog), "Builder keeps the canonical order");

            // The same beaks in the other order: every consumer reads the beak from the item, so nothing disagrees.
            var reversed = ScriptableObject.CreateInstance<ItemCatalog>();
            try
            {
                reversed.Set(catalog.Items.ToList());
                reversed.SetVariants(catalog.VariantsOf(Theme.Biology).AsEnumerable().Reverse().ToList());
                foreach (ItemCatalog c in new[] { catalog, reversed })
                {
                    List<BeakKind> kinds = BiologyBeaks.VariantKinds(c);
                    for (int seed = 1; seed <= Seeds; seed++)
                    {
                        var model = new CubeModel(seed);
                        ItemPlacement p = ItemPlacement.ForRun(model, CubeLayout.Generate(model, library), library,
                            c.Themes(), c.VariantCounts());
                        BiologyPlan plan = BiologyPlan.Create(model, p, kinds);
                        Assert.AreEqual(BiologyBeaks.KindOf(c.ItemFor(Theme.Biology, seed)), plan.Beak, $"seed {seed}");
                        Assert.IsEmpty(BiologyPlan.FindProblems(model, p, kinds), $"seed {seed}");
                        foreach (OptionalGatePlacement o in p.OptionalGates)
                        {
                            Assert.IsTrue(plan.TryGetGate(o.Screen, o.Slot, true, out BeakGateSpec spec));
                            Assert.AreEqual(BiologyBeaks.KindOf(c.VariantsOf(o.Item)[o.Variant]), spec.Beak, $"seed {seed}: {o}");
                        }
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(reversed);
            }
        }

        [Test]
        public void Difficulty_SetsTheBeakHitsAGateTakes()
        {
            Assert.AreEqual(1, BiologyPlan.HitsFor(1));
            Assert.AreEqual(1, BiologyPlan.HitsFor(3));
            Assert.AreEqual(2, BiologyPlan.HitsFor(4));
            Assert.AreEqual(3, BiologyPlan.HitsFor(7));
            Assert.AreEqual(1, BiologyPlan.HitsFor(0));
        }

        [Test]
        public void ButtonSpots_SitInTheHorizontalLane_AndDifferPerQuadrant()
        {
            var spots = new HashSet<Vector2>();
            foreach (Vector2 slot in new[] { new Vector2(4.9f, 1.4f), new Vector2(-3.8f, -1.4f), new Vector2(-4.9f, 1.4f), new Vector2(3.4f, -1.4f) })
            {
                Vector2 b = ButtonGate.ButtonLocal(slot);
                Assert.Less(Mathf.Abs(b.y) + BeakButton.Radius, ScreenModule.ExitLaneHalfWidth, "in the lane");
                Assert.Less(Mathf.Abs(b.x) + BeakButton.Radius, CubeWorld.ScreenSize.x / 2f - ScreenModule.EdgeClearance + 1e-3f);
                Assert.Greater(Vector2.Distance(b, slot), BiologyBeaks.ThinReach, "Distant from its door");
                Assert.IsTrue(spots.Add(b));
            }
            Assert.Less(Mathf.Abs(FlowerVineGate.FlowerLocal.x) + PollinationFlower.Radius, ScreenModule.ExitLaneHalfWidth);
        }

        // ---------- Finch lines ----------

        [Test]
        public void FinchLines_AreDarwinFlavoured_AndTheBeakLineIsTrue()
        {
            var model = new CubeModel(3);
            var facts = new RunFacts(model);
            for (int i = 0; i < BiologyPopulation.FinchCount; i++)
            {
                NpcSpec spec = BiologyPopulation.FinchSpec(3, i);
                Assert.AreEqual(Race.Finch, spec.Race);
                Assert.AreEqual(spec.Signature(), BiologyPopulation.FinchSpec(3, i).Signature());
                foreach (BeakKind beak in new[] { BeakKind.Thin, BeakKind.Thick })
                {
                    List<string> lines = BiologyPopulation.LinesFor(spec, facts, HintDensity.Sparse, beak, i == 0);
                    Assert.AreEqual(Dialogue.MaxLines, lines.Count, "A flavour line, then the hint");
                    if (i == 0) Assert.AreEqual(Dialogue.BeakLine(beak == BeakKind.Thin), lines[0], "Finch 0's flavour is the beak line");
                    else
                    {
                        CollectionAssert.Contains(Dialogue.DarwinLines, lines[0]);
                        CollectionAssert.DoesNotContain(lines, Dialogue.BeakLines[0]);
                    }
                    Hint hint = HintGenerator.Generate(facts, spec.HintKind, spec.Salt, HintDensity.Sparse)[0];
                    Assert.AreEqual(hint.Text, lines[1], "The first hint ends the conversation");
                    foreach (string line in lines)
                        StringAssert.DoesNotContain(Dialogue.ForbiddenWord, line.ToLowerInvariant());
                }
            }
            StringAssert.Contains("thin", Dialogue.BeakLine(true));
            StringAssert.Contains("thick", Dialogue.BeakLine(false));
            Assert.IsTrue(Dialogue.DarwinLines.Any(l => l.Contains("Adapt or get out of the niche!")));
        }
    }
}
