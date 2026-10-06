using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The Physics face as pure data: the time-dilation maths (monotonic, impassable with no mass and with one boulder
    /// too few, passable with the required count), the catalog's Mass Mitt (the placeholder's stable id), plan
    /// determinism, the sweep rules (enough reachable boulders per door, an off-home door, no boulder starting in a
    /// time field or on another face's interactable), the trial fitting on every seed, and the aliens' Einstein lines.
    /// </summary>
    public class PhysicsTests
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

        private static ItemPlacement Place(int seed, ModuleLibrary library, ItemCatalog catalog, out CubeModel model,
            out Func<ScreenAddress, ScreenModule> moduleAt)
        {
            model = new CubeModel(seed);
            CubeLayout layout = CubeLayout.Generate(model, library);
            moduleAt = SeedSweep.ModuleLookup(layout, library);
            return ItemPlacement.ForRun(model, layout, library, catalog.Themes(), catalog.VariantCounts());
        }

        private static PhysicsPlan Plan(int seed, ModuleLibrary library, ItemCatalog catalog, out CubeModel model,
            out ItemPlacement placement, out Func<ScreenAddress, ScreenModule> moduleAt)
        {
            placement = Place(seed, library, catalog, out model, out moduleAt);
            return PhysicsPlan.Create(model, placement, moduleAt, BiologyBeaks.VariantKinds(catalog));
        }

        // ---------- Dilation maths ----------

        [Test]
        public void Dilation_GrowsWithMass_Monotonically()
        {
            Assert.AreEqual(0.6f, TimeField.K, 1e-6f);
            Assert.AreEqual(1f, TimeField.Dilation(0f), 1e-6f, "No mass, no slowing");
            Assert.AreEqual(1.6f, TimeField.Dilation(1f), 1e-5f);
            Assert.AreEqual(2.8f, TimeField.Dilation(3f), 1e-5f);
            float last = 0f;
            for (int m = 0; m <= 8; m++)
            {
                float open = TimeField.OpenSeconds(1f, m * TimeField.BoulderMass);
                Assert.Greater(open, last, $"More mass ({m}) keeps the door open longer");
                last = open;
            }
            Assert.AreEqual(1f, TimeField.Dilation(-2f), 1e-6f, "Negative mass is no mass");
        }

        [Test]
        public void Dilation_IsImpassableWithNoMass_AndWithOneTooFew_PassableWithTheRequiredCount()
        {
            foreach (float walk in new[] { PhysicsPlan.MinWalkSeconds, 1.4f, 1.75f, 2.8f })
            {
                for (int required = 1; required <= PhysicsPlan.MaxRequiredCap; required++)
                {
                    float baseSeconds = TimeField.BaseSecondsFor(walk, required);
                    string context = $"walk {walk}s, needs {required}";
                    Assert.IsFalse(TimeField.Passable(baseSeconds, 0f, walk), $"{context}: impassable with no mass");
                    Assert.IsFalse(TimeField.Passable(baseSeconds, required - 1, walk), $"{context}: impassable one boulder short");
                    Assert.IsTrue(TimeField.Passable(baseSeconds, required, walk), $"{context}: passable with the required boulders");
                    Assert.IsTrue(TimeField.Passable(baseSeconds, required + 2, walk), $"{context}: more mass passes too");
                    // The walk sits between the two open times with room to spare either side (10%+).
                    Assert.Less(TimeField.OpenSeconds(baseSeconds, required - 1), walk * 0.9f, context);
                    Assert.Greater(TimeField.OpenSeconds(baseSeconds, required), walk * 1.1f, context);
                }
            }
            // The design notes' example: base 1 s, k 0.6, so 2 boulders (2.2 s) fail a 2.5 s walk and 3 (2.8 s) pass.
            Assert.IsFalse(TimeField.Passable(1f, 2f, 2.5f));
            Assert.IsTrue(TimeField.Passable(1f, 3f, 2.5f));
        }

        [Test]
        public void WalkSeconds_IsTheDistanceLessBothTouches_AtBasePlayerSpeed()
        {
            Assert.AreEqual(6f, TimeField.BasePlayerSpeed);
            float walk = TimeField.WalkSeconds(Vector2.zero, new Vector2(10f, 0f));
            float touches = TimeField.SwitchRadius + TimeField.ThresholdRadius + 2f * TimeField.PlayerRadius;
            Assert.AreEqual((10f - touches) / 6f, walk, 1e-5f);
            Assert.AreEqual(0f, TimeField.WalkSeconds(Vector2.zero, Vector2.zero));
        }

        // ---------- The item ----------

        [Test]
        public void Catalog_PhysicsItem_IsTheMassMitt_WithThePlaceholdersId_AndNoVariants()
        {
            ItemCatalog catalog = Catalog();
            ItemDefinition mitt = catalog.ForTheme(Theme.Physics);
            Assert.IsNotNull(mitt);
            Assert.AreEqual("physics-item", mitt.Id, "The id stays stable");
            Assert.AreEqual(MassMittItem.Id, mitt.Id);
            Assert.AreEqual(MassMittItem.Name, mitt.DisplayName);
            Assert.AreEqual("Mass Mitt", mitt.DisplayName);
            Assert.AreEqual(Theme.Physics, mitt.HomeTheme);
            Assert.IsEmpty(catalog.VariantsOf(Theme.Physics), "One logical item for the 1.4 placer");
            for (int seed = 1; seed <= Seeds; seed++)
                Assert.AreSame(mitt, catalog.ItemFor(Theme.Physics, seed));
        }

        // ---------- The plan ----------

        [Test]
        public void Plan_IsDeterministic_OnItsOwnStreams_AndHasNoProblems()
        {
            Assert.AreEqual(13UL, PhysicsPlan.RngStream);
            Assert.AreEqual(14UL, PhysicsFace.PopulationRngStream);
            var taken = new[] { ItemPlacement.RngStream, TownPlan.RngStream, BiologyPlan.RngStream, BiologyFace.PopulationRngStream,
                ChemistryPlan.RngStream, ChemistryFace.PopulationRngStream };
            CollectionAssert.DoesNotContain(taken, PhysicsPlan.RngStream);
            CollectionAssert.DoesNotContain(taken, PhysicsFace.PopulationRngStream);

            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            var signatures = new HashSet<string>();
            var requiredSeen = new HashSet<int>();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                PhysicsPlan a = Plan(seed, library, catalog, out CubeModel model, out ItemPlacement p, out var moduleAt);
                PhysicsPlan b = Plan(seed, library, catalog, out _, out _, out _);
                Assert.IsNotNull(a, $"seed {seed}");
                Assert.IsTrue(a.HasLayout);
                Assert.AreEqual(a.Signature(), b.Signature(), $"seed {seed}: same seed, same plan");
                List<string> problems = PhysicsPlan.FindProblems(model, p, moduleAt, BiologyBeaks.VariantKinds(catalog));
                Assert.IsEmpty(problems, $"seed {seed}: {string.Join("; ", problems)}");
                Assert.AreEqual(p.Gates.Count(g => g.Item == Theme.Physics), a.Doors.Count, $"seed {seed}: a timed door per Physics gate");
                Assert.IsTrue(a.Doors.Any(d => !d.OnHome), $"seed {seed}: the mitt opens a door on another face");
                Assert.AreEqual(model.FaceOf(Theme.Physics), a.TrialScreen.Face);
                foreach (TimedDoorSpec d in a.Doors)
                {
                    Assert.That(d.Required, Is.InRange(1, PhysicsPlan.MaxRequiredCap), d.ToString());
                    Assert.That(d.Spare, Is.InRange(0, 1), d.ToString());
                    Assert.AreEqual(d.Required + d.Spare, d.Boulders.Count, $"seed {seed}: {d} brings its own boulders");
                    Assert.AreEqual(d.Required, d.Layout.Required);
                    requiredSeen.Add(d.Required);
                }
                signatures.Add(a.Signature());
            }
            Assert.Greater(signatures.Count, Seeds / 2, "Plans vary across seeds");
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, requiredSeen, "The difficulty spans 1 to 3 boulders");
        }

        [Test]
        public void Plan_DifficultyKnob_CapsTheRequiredCount()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            for (int seed = 1; seed <= 10; seed++)
            {
                ItemPlacement p = Place(seed, library, catalog, out CubeModel model, out var moduleAt);
                PhysicsPlan easy = PhysicsPlan.Create(model, p, moduleAt, BiologyBeaks.VariantKinds(catalog), 1);
                Assert.IsTrue(easy.Doors.All(d => d.Required == 1), $"seed {seed}");
                Assert.IsEmpty(PhysicsPlan.FindProblems(model, p, moduleAt, BiologyBeaks.VariantKinds(catalog), 1));
            }
        }

        [Test]
        public void Plan_WithoutModules_HasTheSameDoorsButNoLayout_AndNoPhysicsPickupMeansNoPlan()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            ItemPlacement p = Place(3, library, catalog, out CubeModel model, out _);
            PhysicsPlan bare = PhysicsPlan.Create(model, p);
            Assert.IsFalse(bare.HasLayout);
            Assert.IsNull(bare.Trial);
            Assert.AreEqual(p.Gates.Count(g => g.Item == Theme.Physics), bare.Doors.Count);
            Assert.IsTrue(bare.Doors.All(d => d.Layout == null && d.Boulders.Count == 0));
            Assert.IsEmpty(PhysicsPlan.FindProblems(model, p));

            var noPickup = new ItemPlacement(3, p.Gates, p.Pickups.Where(k => k.Item != Theme.Physics));
            Assert.IsNull(PhysicsPlan.Create(model, noPickup), "No Physics pickup: Physics is not an item of the run");
        }

        // ---------- Sweep rules ----------

        [Test]
        public void SweepRule_FlagsADoorWithTooFewReachableBoulders()
        {
            PhysicsPlan plan = Plan(1, Library(), Catalog(), out _, out _, out _);
            Assert.IsEmpty(plan.LayoutProblems());
            TimedDoorSpec door = plan.Doors[0];
            PhysicsPlan short1 = plan.WithDoor(0, door.WithBoulders(door.Boulders.Take(door.Required - 1).ToList()));
            List<string> problems = short1.LayoutProblems();
            Assert.IsTrue(problems.Any(x => x.Contains("reachable boulders") && x.Contains(door.Screen.ToString())), string.Join("; ", problems));
        }

        [Test]
        public void SweepRule_FlagsABoulderStartingInATimeField_OrInAWall_OrOnAnotherFacesInteractable()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            PhysicsPlan plan = Plan(1, library, catalog, out _, out _, out var moduleAt);
            TimedDoorSpec door = plan.Doors[0];

            // In the field (beside the threshold): the door would start dilated.
            var inField = door.Boulders.ToList();
            inField[0] = door.Layout.FieldLocal + new Vector2(0f, door.Layout.Opening.y * 1.2f);
            List<string> a = plan.WithDoor(0, door.WithBoulders(inField)).LayoutProblems();
            Assert.IsTrue(a.Any(x => x.Contains("starts in the time field")), string.Join("; ", a));

            // In a wall: inside the door's own alcove.
            var inWall = door.Boulders.ToList();
            inWall[0] = door.Layout.DoorLocal - door.Layout.Opening * 1.2f;
            List<string> b = plan.WithDoor(0, door.WithBoulders(inWall)).LayoutProblems();
            Assert.IsTrue(b.Any(x => x.Contains("inside a wall")), string.Join("; ", b));

            // On another face's interactable: the first seed whose door screen holds one.
            for (int seed = 1; seed <= Seeds; seed++)
            {
                PhysicsPlan p = Plan(seed, library, catalog, out _, out _, out _);
                for (int i = 0; i < p.Doors.Count; i++)
                {
                    ScreenMark mark = p.OtherMarks(p.Doors[i].Screen).FirstOrDefault(m => !m.What.Contains("slot"));
                    if (mark.What == null) continue;
                    var onIt = p.Doors[i].Boulders.ToList();
                    onIt[0] = mark.Local;
                    List<string> c = p.WithDoor(i, p.Doors[i].WithBoulders(onIt)).LayoutProblems();
                    Assert.IsTrue(c.Any(x => x.Contains("sits on")), $"seed {seed}: {string.Join("; ", c)}");
                    return;
                }
            }
            Assert.Fail("No seed puts a timed door on a screen with another face's interactable");
        }

        [Test]
        public void SweepRule_NonTrialBoulders_CannotSatisfyATrialBooth()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                PhysicsPlan plan = Plan(seed, library, catalog, out _, out _, out _);
                Assert.IsTrue(plan.Trial.CountsOnlyOwnBoulders, $"seed {seed}: booths weigh only the trial's boulders");
                if (!plan.Doors.Any(d => d.Screen == plan.Trial.Screen)) continue;
                Assert.IsEmpty(plan.LayoutProblems());
                // Booths that weighed every boulder: the screen's door boulders could stand in for the shared ones.
                PhysicsTrialSpec t = plan.Trial;
                var open = new PhysicsTrialSpec(t.Screen, t.Booths, t.Boulders, t.Required, false);
                List<string> problems = plan.WithTrial(open).LayoutProblems();
                Assert.IsTrue(problems.Any(x => x.Contains("non-trial boulders can dilate it")), $"seed {seed}: {string.Join("; ", problems)}");
                return;
            }
            Assert.Fail("No seed puts a timed door on the trial screen");
        }

        [Test]
        public void SpeedScale_KeepsTheRequiredCountExact_ForAFasterPlayer()
        {
            Assert.AreEqual(1f, TimeField.SpeedScale(TimeField.BasePlayerSpeed), 1e-6f);
            float fast = TimeField.BasePlayerSpeed * 1.5f; // two speed points
            const float walk = 1.6f;
            float fastWalk = walk * TimeField.BasePlayerSpeed / fast;
            for (int required = 1; required <= PhysicsPlan.MaxRequiredCap; required++)
            {
                float scaled = TimeField.BaseSecondsFor(walk, required) * TimeField.SpeedScale(fast);
                Assert.IsFalse(TimeField.Passable(scaled, required - 1, fastWalk), $"needs {required}: still impassable one short");
                Assert.IsTrue(TimeField.Passable(scaled, required, fastWalk), $"needs {required}: passable with the count");
            }
        }

        [Test]
        public void SweepRule_FlagsAMittWithNoDoorOnAnotherFace()
        {
            var model = new CubeModel(5);
            FaceId phys = model.FaceOf(Theme.Physics);
            var placement = new ItemPlacement(5, new[]
            {
                new GatePlacement(new ScreenAddress(phys, 0, 0), 0, Theme.Physics),
                new GatePlacement(new ScreenAddress(phys, 1, 0), 0, Theme.Physics),
            }, new[] { new PickupPlacement(Theme.Physics, new ScreenAddress(phys, 1, 1)) });
            List<string> problems = PhysicsPlan.FindProblems(model, placement);
            Assert.IsTrue(problems.Any(x => x.Contains("no gate on another face")), string.Join("; ", problems));
        }

        [Test]
        public void TheSeedSweep_RunsThePhysicsRules_AndPassesEverySeed()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            List<SeedSweep.SeedResult> results = SeedSweep.Run(catalog: library, itemThemes: catalog.Themes(),
                variantCounts: catalog.VariantCounts(), biologyVariantKinds: BiologyBeaks.VariantKinds(catalog));
            string report = SeedSweep.Describe(results);
            Assert.IsTrue(results.All(r => r.Passed), report);
            StringAssert.Contains("every timed door with enough reachable boulders on its screen", report);
            StringAssert.Contains("the Physics trial fitting", report);
        }

        // ---------- Spots ----------

        /// <summary>Other faces' interactables on every screen, recomputed here from their own plans.</summary>
        private static Dictionary<ScreenAddress, List<(string what, Vector2 at)>> OtherSpots(CubeModel model, ItemPlacement p,
            Func<ScreenAddress, ScreenModule> moduleAt, ItemCatalog catalog)
        {
            var spots = new Dictionary<ScreenAddress, List<(string, Vector2)>>();
            void Add(ScreenAddress s, string what, Vector2 at)
            {
                if (!spots.TryGetValue(s, out var list)) spots[s] = list = new List<(string, Vector2)>();
                list.Add((what, at));
            }
            Vector2 SlotLocal(ScreenAddress s, int slot)
            {
                ScreenModule m = moduleAt(s);
                return m.Gates[slot].transform.position - m.transform.position;
            }
            BiologyPlan bio = BiologyPlan.Create(model, p, BiologyBeaks.VariantKinds(catalog));
            foreach (BeakGateSpec s in bio.Gates)
            {
                if (s.Kind == BeakGateKind.Button) Add(s.Screen, $"button {s}", ButtonGate.ButtonLocal(SlotLocal(s.Screen, s.Slot)));
                if (s.Kind == BeakGateKind.FlowerVine) Add(s.FlowerScreen, $"flower {s}", FlowerVineGate.FlowerLocal);
            }
            var bioRng = new SeededRng(unchecked((ulong)(uint)model.Seed), BiologyFace.PopulationRngStream);
            foreach (Vector2 v in BiologyTrial.PickSpots(BiologyTrial.LaneSideSpots(TownPlan.BlockedRects(moduleAt(bio.TrialScreen))),
                         BiologyTrial.PiecesFor(bio.Beak), bioRng))
                Add(bio.TrialScreen, "Biology trial piece", v);
            ChemistryPlan chem = ChemistryPlan.Create(model, p);
            foreach (IsotopeGateSpec s in chem.Gates)
                if (s.Stage == IsotopeStage.Lead) Add(s.Screen, $"plate {s}", LeadPlateGate.PlateLocal(SlotLocal(s.Screen, s.Slot)));
            foreach (Vector2 v in ChemistryPlan.TrialSpots(model.Seed, moduleAt(chem.TrialScreen)))
                Add(chem.TrialScreen, "Chemistry trial piece", v);
            foreach (PickupPlacement k in p.Pickups)
                if (!k.IsGuarded) Add(k.Screen, $"pickup {k}", CubeWorld.OpenPickupOffset);
            return spots;
        }

        [Test]
        public void Spots_SwitchesAndBoulders_NeverShareASpotWithAnotherFacesInteractable_AndBouldersStartClear()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            const float minGap = 1.0f;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                PhysicsPlan plan = Plan(seed, library, catalog, out CubeModel model, out ItemPlacement p, out var moduleAt);
                Dictionary<ScreenAddress, List<(string what, Vector2 at)>> others = OtherSpots(model, p, moduleAt, catalog);
                var mine = new List<(ScreenAddress screen, string what, Vector2 at, bool boulder)>();
                foreach (TimedDoorSpec d in plan.Doors)
                {
                    mine.Add((d.Screen, $"switch of {d}", d.Layout.SwitchLocal, false));
                    foreach (Vector2 b in d.Boulders) mine.Add((d.Screen, $"boulder of {d}", b, true));
                }
                foreach (TrialBoothSpec booth in plan.Trial.Booths) mine.Add((plan.Trial.Screen, "trial switch", booth.Layout.SwitchLocal, false));
                foreach (Vector2 b in plan.Trial.Boulders) mine.Add((plan.Trial.Screen, "trial boulder", b, true));

                foreach (var m in mine)
                {
                    string context = $"seed {seed} {m.screen}: {m.what}";
                    if (others.TryGetValue(m.screen, out var list))
                        foreach (var o in list)
                            Assert.Greater(Vector2.Distance(m.at, o.at), minGap, $"{context} shares a spot with {o.what}");
                    if (!m.boulder) continue;
                    // Off both lanes and the screen centre, inside the edge band, out of every wall and alcove.
                    var square = new Rect(m.at - Vector2.one * Boulder.Radius, Vector2.one * (2f * Boulder.Radius));
                    Assert.IsTrue(ScreenModule.KeepsExitsOpen(square), $"{context} blocks a lane or an edge");
                    foreach (Rect r in TownPlan.BlockedRects(moduleAt(m.screen)))
                    {
                        // The boulder is a circle: its centre stays a radius away from every wall and alcove.
                        float dx = Mathf.Max(r.xMin - m.at.x, 0f, m.at.x - r.xMax);
                        float dy = Mathf.Max(r.yMin - m.at.y, 0f, m.at.y - r.yMax);
                        Assert.GreaterOrEqual(Mathf.Sqrt(dx * dx + dy * dy), Boulder.Radius, $"{context} is inside a wall or alcove");
                    }
                    // Outside every time field on its screen: no door starts dilated.
                    foreach (TimedDoorSpec d in plan.Doors.Where(x => x.Screen == m.screen))
                        Assert.Greater(Vector2.Distance(m.at, d.Layout.FieldLocal), TimeField.FieldRadius, $"{context} starts in {d}'s field");
                    if (plan.Trial.Screen == m.screen)
                        foreach (TrialBoothSpec booth in plan.Trial.Booths)
                            Assert.Greater(Vector2.Distance(m.at, booth.Layout.FieldLocal), TimeField.FieldRadius, $"{context} starts in a booth's field");
                }
                // Switches sit in the horizontal lane, on the far side of the screen from their door.
                foreach (TimedDoorSpec d in plan.Doors)
                {
                    Assert.Less(Mathf.Abs(d.Layout.SwitchLocal.y), ScreenModule.ExitLaneHalfWidth, d.ToString());
                    Assert.AreNotEqual(Mathf.Sign(d.Layout.DoorLocal.x), Mathf.Sign(d.Layout.SwitchLocal.x), d.ToString());
                    Assert.GreaterOrEqual(d.Layout.WalkSeconds, PhysicsPlan.MinWalkSeconds);
                }
            }
        }

        [Test]
        public void Trial_FitsOnEverySeed_TwoBoothsApart_SharingExactlyEnoughBoulders()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                PhysicsPlan plan = Plan(seed, library, catalog, out CubeModel model, out _, out var moduleAt);
                PhysicsTrialSpec trial = plan.Trial;
                Assert.IsNotNull(trial, $"seed {seed}: the trial fits");
                Assert.AreEqual(plan.TrialScreen, trial.Screen);
                Assert.AreEqual(model.FaceOf(Theme.Physics), trial.Screen.Face);
                Assert.AreEqual(PhysicsPlan.TrialBooths, trial.Booths.Count);
                Assert.AreEqual(PhysicsPlan.TrialRequired, trial.Required);
                Assert.AreEqual(trial.Required, trial.Boulders.Count, "Only enough boulders for one door at a time");
                Assert.Greater(Vector2.Distance(trial.Booths[0].Layout.FieldLocal, trial.Booths[1].Layout.FieldLocal), 2f * TimeField.FieldRadius,
                    $"seed {seed}: no boulder can dilate both booths at once");
                foreach (TrialBoothSpec booth in trial.Booths)
                {
                    Assert.IsTrue(ScreenModule.KeepsExitsOpen(booth.Footprint), $"seed {seed}: a booth blocks a lane");
                    foreach (Rect r in TownPlan.BlockedRects(moduleAt(trial.Screen)))
                        Assert.IsFalse(r.Overlaps(booth.Footprint), $"seed {seed}: a booth overlaps a wall");
                    Assert.IsTrue(booth.Layout.HasSwitch);
                    Assert.IsFalse(TimeField.Passable(booth.Layout.BaseSeconds, trial.Required - 1, booth.Layout.WalkSeconds));
                    Assert.IsTrue(TimeField.Passable(booth.Layout.BaseSeconds, trial.Required, booth.Layout.WalkSeconds));
                }
            }
        }

        // ---------- Alien lines ----------

        [Test]
        public void AlienLines_AreEinsteinFlavoured_WithTheRelativeLine_AndTheMittLine_AndNewtonIsBonked()
        {
            var model = new CubeModel(3);
            var facts = new RunFacts(model);
            for (int i = 0; i < PhysicsPopulation.AlienCount; i++)
            {
                NpcSpec spec = PhysicsPopulation.AlienSpec(3, i);
                Assert.AreEqual(Race.Alien, spec.Race);
                Assert.AreEqual(spec.Signature(), PhysicsPopulation.AlienSpec(3, i).Signature());
                List<string> lines = PhysicsPopulation.LinesFor(spec, facts, HintDensity.Sparse, i == 0);
                Assert.AreEqual(Dialogue.MaxLines, lines.Count, "A flavour line, then the hint");
                if (i == 0) Assert.AreEqual(Dialogue.MassMittLine, lines[0], "Alien 0's flavour is the mitt line");
                else
                {
                    CollectionAssert.Contains(Dialogue.EinsteinLines, lines[0]);
                    CollectionAssert.DoesNotContain(lines, Dialogue.MassMittLine);
                }
                Hint hint = HintGenerator.Generate(facts, spec.HintKind, spec.Salt, HintDensity.Sparse)[0];
                Assert.AreEqual(hint.Text, lines[1], "The first hint ends the conversation");
                foreach (string line in lines)
                    StringAssert.DoesNotContain(Dialogue.ForbiddenWord, line.ToLowerInvariant());
            }
            Assert.IsTrue(Dialogue.EinsteinLines.Any(l => l.Contains("It's all relative, darling!")));
            CollectionAssert.Contains(Dialogue.AllFixedText().ToList(), Dialogue.MassMittLine);
            foreach (string line in Dialogue.EinsteinLines.Concat(Dialogue.NewtonLines))
                CollectionAssert.Contains(Dialogue.AllFixedText().ToList(), line);
            Assert.IsTrue(Dialogue.NewtonLines.Any(l => l.ToLowerInvariant().Contains("apple")), "Newton gets bonked by apples");
            // The mitt line is true: it names the switch, the mitt, the boulders and what mass does.
            string mittLine = Dialogue.MassMittLine.ToLowerInvariant();
            foreach (string word in new[] { "switch", "mass mitt", "boulder", "slower", "longer" })
                StringAssert.Contains(word, mittLine);
            Assert.AreEqual(Race.Townsfolk, PhysicsPopulation.NewtonSpec(3).Race);
        }
    }
}
