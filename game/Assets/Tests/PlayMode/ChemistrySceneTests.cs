using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Game.Cube.Tests.TestInput;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Curie's face in the real Cube scene. The reveal builds the planned stage gates and the isotope dispenser at the
    /// Chemistry pickup's spot. A full run from Town: walk onto the dispenser, open every glow gate with a glowing
    /// touch, blast every cracked wall with the unstable isotope, drop lead on every plate (including the one on
    /// another face), fetch fresh isotopes from the dispenser in between, and finish the trial with one isotope.
    /// Mushroom NPCs are present, talkable, Curie-flavoured and true, and Chemistry enemies are weak to their items.
    /// Decay waits are skipped by setting the isotope's age (the timing itself is covered by the component tests).
    /// </summary>
    public class ChemistrySceneTests : InputTestFixture
    {
        private const string SceneName = "Cube";
        private const float Timeout = 6f;

        private Keyboard keyboard;
        private CubeWorld world;
        private CubeNavigator navigator;
        private ChemistryFace chemistry;
        private Inventory inventory;
        private Isotope isotope;
        private PlayerAttack attack;
        private Rigidbody2D body;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        private IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            world = UnityEngine.Object.FindAnyObjectByType<CubeWorld>();
            navigator = UnityEngine.Object.FindAnyObjectByType<CubeNavigator>();
            Assert.IsNotNull(world);
            chemistry = world.GetComponent<ChemistryFace>();
            Assert.IsNotNull(chemistry, "The CubeWorld carries the ChemistryFace content hook");
            CollectionAssert.Contains(world.FaceContents, chemistry);
            inventory = navigator.GetComponent<Inventory>();
            isotope = navigator.GetComponent<Isotope>();
            Assert.IsNotNull(isotope, "The player carries an Isotope");
            attack = navigator.GetComponent<PlayerAttack>();
            body = navigator.GetComponent<Rigidbody2D>();
            navigator.GetComponent<PlayerStats>().MaxHealth = 999;
        }

        private ItemPlacement PlacementFor(int seed, out CubeModel model)
        {
            model = new CubeModel(seed);
            return ItemPlacement.ForRun(model, CubeLayout.Generate(model, world.Library), world.Library,
                world.ItemCatalog.Themes(), world.ItemCatalog.VariantCounts());
        }

        /// <summary>The first seed whose dispenser lies in the open (walkable straight from its screen centre).</summary>
        private int SeedWithOpenDispenser()
        {
            for (int seed = 1; seed < 500; seed++)
            {
                ItemPlacement p = PlacementFor(seed, out _);
                if (p.TryGetPickup(Theme.Chemistry, out PickupPlacement pickup) && !pickup.IsGuarded) return seed;
            }
            Assert.Fail("No seed with an open isotope dispenser");
            return 0;
        }

        private IEnumerator StartRunOntoChemistry(int seed, bool enemies)
        {
            world.Rebuild(seed);
            yield return null;
            Assert.AreEqual(CubeModel.StartFace, navigator.Face, "A run starts in Town");
            Assert.IsFalse(world.ScienceRevealed);
            Assert.IsFalse(isotope.IsHeld, "A run starts with no isotope");
            chemistry.SpawnEnemies = enemies;
            world.GetComponent<BiologyFace>().SpawnEnemies = false;
            navigator.TeleportTo(new ScreenAddress(world.Model.FaceOf(Theme.Chemistry), 0, 0), Facing.East);
            Assert.IsTrue(world.ScienceRevealed, "Leaving Town reveals the science faces");
            Assert.IsNotNull(chemistry.Plan);
            yield return null;
        }

        private IEnumerator PutPlayerAt(ScreenAddress screen, Vector2 at)
        {
            if (navigator.Current != screen) navigator.TeleportTo(screen, Facing.East);
            body.position = at;
            body.linearVelocity = Vector2.zero;
            navigator.transform.position = new Vector3(at.x, at.y, navigator.transform.position.z);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        /// <summary>Ages the held isotope into a stage (skips the decay wait).</summary>
        private void AgeInto(IsotopeStage stage)
        {
            Assert.IsTrue(isotope.IsHeld);
            isotope.Age = ChemistryIsotope.StageStart(stage, isotope.HalfLife) + 0.1f;
            Assert.AreEqual(stage, isotope.Stage);
        }

        /// <summary>Back to the dispenser for a fresh isotope (when none is held).</summary>
        private IEnumerator FreshIsotope()
        {
            Assert.IsFalse(isotope.IsHeld, "The dispenser refills only when no isotope is held");
            IsotopeDispenser dispenser = chemistry.Dispenser;
            int before = dispenser.Dispensed;
            yield return PutPlayerAt(chemistry.Plan.Dispenser.Screen, (Vector2)dispenser.transform.position + Vector2.up * 1.5f);
            yield return PutPlayerAt(chemistry.Plan.Dispenser.Screen, dispenser.transform.position);
            yield return WaitUntilOrTimeout(() => isotope.IsHeld, Timeout);
            Assert.IsTrue(isotope.IsHeld, "The dispenser hands out a fresh isotope");
            Assert.AreEqual(before + 1, dispenser.Dispensed);
            Assert.AreEqual(IsotopeStage.Glow, isotope.Stage, "Fresh isotopes glow");
            navigator.GetComponent<Equipment>().Equip(isotope.Item);
        }

        private IEnumerator Face(Facing direction)
        {
            yield return this.TapThroughPhysics(KeyFor(keyboard, direction));
            Assert.AreEqual((Vector2)direction.ToVector(), attack.Facing, "Facing");
        }

        /// <summary>Stands distance away from a target, facing it, and swings once.</summary>
        private IEnumerator StrikeFrom(ScreenAddress screen, Vector2 target, Facing toward, float distance)
        {
            if (navigator.Current != screen) navigator.TeleportTo(screen, Facing.East);
            yield return Face(toward);
            yield return new WaitForSeconds(attack.Cooldown + 0.02f);
            yield return PutPlayerAt(screen, target - (Vector2)toward.ToVector() * distance);
            Assert.GreaterOrEqual(attack.TryAttack(), 0, "The swing happened");
            yield return null;
        }

        private IsotopeGate GateFor(IsotopeGateSpec spec) =>
            world.ModuleAt(spec.Screen).Gates[spec.Slot].GetComponentInChildren<IsotopeGate>(true);

        private ScreenAddress ScreenOf(Vector2 position, FaceId face) => new ScreenAddress(face, world.CellAt(face, position));

        private static Facing FromLane(Vector2 local)
        {
            if (Mathf.Abs(Mathf.Abs(local.y) - BiologyTrial.LaneSideOffset) < 0.01f)
                return local.y >= 0f ? Facing.North : Facing.South;
            return local.x >= 0f ? Facing.East : Facing.West;
        }

        [UnityTest]
        public IEnumerator Reveal_BuildsThePlannedStageGates_TheDispenser_AndTheTrial()
        {
            yield return Load();
            yield return StartRunOntoChemistry(SeedWithOpenDispenser(), false);
            ChemistryPlan plan = chemistry.Plan;
            ItemDefinition item = world.ItemFor(Theme.Chemistry);
            Assert.AreEqual(ChemistryIsotope.Id, item.Id);
            Assert.AreSame(item, isotope.Item, "The player's isotope tracks the run's Chemistry item");
            Assert.AreEqual(plan.Gates.Count, chemistry.StageGates.Count);
            foreach (IsotopeGateSpec spec in plan.Gates)
            {
                IsotopeGate gate = GateFor(spec);
                Assert.IsNotNull(gate, spec.ToString());
                Assert.AreEqual(spec.Stage, gate.Stage, spec.ToString());
                Assert.AreSame(item, gate.RequiredItem);
                Assert.IsFalse(gate.IsOpen);
                CollectionAssert.Contains(world.Gates, gate);
            }
            Assert.IsTrue(plan.Gates.Any(s => s.Screen.Face != plan.Face), "A stage gate on another face");

            IsotopeDispenser dispenser = chemistry.Dispenser;
            Assert.IsNotNull(dispenser);
            CollectionAssert.Contains(world.Pickups, dispenser, "The dispenser replaces the one-shot pickup");
            Assert.AreEqual(1, world.Pickups.Count(p => p != null && p.Item.HomeTheme == Theme.Chemistry));
            Vector2 expected = world.ScreenCenter(plan.Dispenser.Screen) + CubeWorld.OpenPickupOffset;
            Assert.Less(Vector2.Distance(expected, dispenser.transform.position), 1e-3f, "At the Chemistry pickup's spot");

            Assert.IsNotNull(chemistry.Trial, "The trial is built");
            Assert.AreEqual(plan.TrialScreen, ScreenOf(chemistry.Trial.transform.position, plan.Face));
            Assert.IsFalse(chemistry.Trial.IsComplete);
        }

        [UnityTest]
        public IEnumerator FullRun_TownToDispenser_ThroughEveryStageGate_AndTheTrial()
        {
            yield return Load();
            int seed = SeedWithOpenDispenser();
            yield return StartRunOntoChemistry(seed, false);
            ChemistryPlan plan = chemistry.Plan;

            // The dispenser: walk onto it from its screen centre.
            navigator.TeleportTo(plan.Dispenser.Screen, Facing.East);
            yield return null;
            yield return this.HoldUntil(keyboard.sKey, () => isotope.IsHeld, Timeout);
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(isotope.IsHeld, "Walking onto the dispenser hands out an isotope");
            Assert.AreEqual(IsotopeStage.Glow, isotope.Stage);
            Assert.AreSame(isotope.Item, navigator.GetComponent<Equipment>().Equipped, "The isotope is equipped");
            StringAssert.Contains("Glowing", isotope.HudText);

            // Glow gates: walk into them while glowing (after checking a wrong stage leaves one shut).
            bool checkedWrong = false;
            foreach (IsotopeGateSpec spec in plan.Gates.Where(s => s.Stage == IsotopeStage.Glow))
            {
                var gate = (DarkRoomGate)GateFor(spec);
                GateSlot slot = world.ModuleAt(spec.Screen).Gates[spec.Slot];
                Facing toward = slot.Opening.Opposite();
                Vector2 before = (Vector2)slot.transform.position + (Vector2)slot.Opening.ToVector() * 1.1f;
                if (!checkedWrong)
                {
                    AgeInto(IsotopeStage.Unstable);
                    yield return PutPlayerAt(spec.Screen, before);
                    yield return this.HoldUntil(KeyFor(keyboard, toward), () => gate.IsOpen, 1.0f);
                    yield return null;
                    Assert.IsFalse(gate.IsOpen, "An unstable isotope does not light a dark room");
                    checkedWrong = true;
                }
                AgeInto(IsotopeStage.Glow);
                yield return PutPlayerAt(spec.Screen, before);
                yield return this.HoldUntil(KeyFor(keyboard, toward), () => gate.IsOpen, Timeout);
                yield return null;
                Assert.IsTrue(gate.IsOpen, $"A glowing touch opens {spec}");
            }

            // Cracked walls: swing the unstable isotope at them.
            foreach (IsotopeGateSpec spec in plan.Gates.Where(s => s.Stage == IsotopeStage.Unstable))
            {
                IsotopeGate gate = GateFor(spec);
                GateSlot slot = world.ModuleAt(spec.Screen).Gates[spec.Slot];
                AgeInto(IsotopeStage.Glow);
                yield return StrikeFrom(spec.Screen, slot.transform.position, slot.Opening.Opposite(), 1.0f);
                Assert.IsFalse(gate.IsOpen, "A glowing swing does not blast a cracked wall");
                AgeInto(IsotopeStage.Unstable);
                yield return StrikeFrom(spec.Screen, slot.transform.position, slot.Opening.Opposite(), 1.0f);
                Assert.IsTrue(gate.IsOpen, $"The unstable isotope blasts {spec}");
                Assert.IsFalse(gate.Solid.enabled);
            }

            // Lead plates (one is on another face): drop lead on each, fetching a fresh isotope for every plate.
            foreach (IsotopeGateSpec spec in plan.Gates.Where(s => s.Stage == IsotopeStage.Lead))
            {
                var gate = (LeadPlateGate)GateFor(spec);
                if (!isotope.IsHeld) yield return FreshIsotope();
                AgeInto(IsotopeStage.Lead);
                yield return PutPlayerAt(spec.Screen, gate.Plate.transform.position);
                yield return WaitUntilOrTimeout(() => gate.IsOpen, Timeout);
                Assert.IsTrue(gate.IsOpen, $"Lead on the plate opens {spec}");
                Assert.IsFalse(isotope.IsHeld, "The lead is used up");
            }
            Assert.IsTrue(plan.Gates.Any(s => s.Screen.Face != plan.Face && GateFor(s).IsOpen),
                "The isotope opened a gate on another face");
            Assert.IsTrue(plan.Gates.All(s => GateFor(s).IsOpen), "Every stage gate is open");

            // The trial, with one fresh isotope: light the lamp, blast the wall, weigh the plate.
            yield return FreshIsotope();
            ChemistryTrial trial = chemistry.Trial;
            var fired = new List<int>();
            trial.Completed += c => fired.Add(c);
            yield return PutPlayerAt(plan.TrialScreen, trial.Lamp.transform.position);
            yield return WaitUntilOrTimeout(() => trial.Lamp.Done, Timeout);
            Assert.IsTrue(trial.Lamp.Done, "A glowing touch lights the trial lamp");
            AgeInto(IsotopeStage.Unstable);
            Vector2 centre = world.ScreenCenter(plan.TrialScreen);
            Vector2 wall = trial.Wall.transform.position;
            yield return StrikeFrom(plan.TrialScreen, wall, FromLane(wall - centre), 0.9f);
            Assert.IsTrue(trial.Wall.Done, "The unstable isotope blasts the trial wall");
            AgeInto(IsotopeStage.Lead);
            yield return PutPlayerAt(plan.TrialScreen, trial.Plate.transform.position);
            yield return WaitUntilOrTimeout(() => trial.IsComplete, Timeout);
            Assert.IsTrue(trial.IsComplete);
            CollectionAssert.AreEqual(new[] { ChemistryPlan.TrialCurrency }, fired, "The trial fires once with its currency");

            // Re-obtain: after the trial's plate took the lead, the dispenser refills again.
            yield return FreshIsotope();
        }

        private bool OnFace(Vector2 position, FaceId face, ScreenAddress? screen)
        {
            Vector2 local = position - world.FaceOrigin(face);
            bool on = local.x > 0f && local.y > 0f && local.x < world.FaceExtent.x && local.y < world.FaceExtent.y;
            return on && (screen == null || world.CellAt(face, position) == screen.Value.Cell);
        }

        /// <summary>Checks a hint's claim against the objects the reveal actually built.</summary>
        private void AssertHintTrueInWorld(Hint hint, string context)
        {
            string text = hint.Text;
            ScreenAddress? screen = hint.Precise ? hint.Screen : (ScreenAddress?)null;
            Assert.AreEqual(world.Model.ThemeOf(hint.Face), hint.FaceTheme, $"{context}: {text}");
            switch (hint.Kind)
            {
                case HintKind.ItemLocation:
                    Assert.IsTrue(world.Pickups.Any(p => p != null && p.Item.HomeTheme == hint.Item && OnFace(p.transform.position, hint.Face, screen)),
                        $"{context}: no {hint.Item} pickup there ({text})");
                    break;
                case HintKind.GateLocation:
                    Assert.IsTrue(world.Gates.Any(g => g != null && g.RequiredItem.HomeTheme == hint.Item && OnFace(g.transform.position, hint.Face, screen)),
                        $"{context}: no {hint.Item} gate there ({text})");
                    break;
                case HintKind.CoreEntrance:
                    Assert.IsTrue(world.AllModules.Any(m => m.ActiveCoreEntrances().Count > 0 && OnFace(m.transform.position, hint.Face, screen)),
                        $"{context}: no core entrance there ({text})");
                    break;
                default:
                    Assert.AreEqual(world.Model.FaceOf(hint.FaceTheme), hint.Face, $"{context}: {text}");
                    break;
            }
        }

        [UnityTest]
        public IEnumerator MushroomNpcs_ArePresent_Talkable_CurieFlavoured_WithTrueHints_AndEnemiesAreWeakToItems()
        {
            yield return Load();
            yield return StartRunOntoChemistry(SeedWithOpenDispenser(), true);
            FaceId face = chemistry.Plan.Face;
            Assert.AreEqual(ChemistryPopulation.MushroomCount, chemistry.Mushrooms.Count);
            var facts = new RunFacts(world.Model, world.Layout, world.ItemPlacement);
            for (int i = 0; i < chemistry.Mushrooms.Count; i++)
            {
                NpcTalker mushroom = chemistry.Mushrooms[i];
                Assert.IsNotNull(mushroom);
                Assert.AreEqual(Race.Mushroom, mushroom.Race);
                Assert.IsTrue(OnFace(mushroom.transform.position, face, null), "Mushrooms live on the Chemistry face");
                IReadOnlyList<string> lines = mushroom.Lines;
                CollectionAssert.Contains(Dialogue.Flavours[Race.Mushroom].Greetings, lines[0], "A mushroom greeting");
                CollectionAssert.Contains(Dialogue.CurieLines, lines[1], "A Curie line");

                int stageLines = i == 0 ? 1 : 0;
                List<string> hintLines = lines.Skip(3).Take(lines.Count - 3 - stageLines).ToList();
                List<Hint> hints = HintGenerator.Generate(facts, mushroom.Spec.HintKind, mushroom.Spec.Salt, HintGenerator.FirstRunDensity);
                Assert.AreEqual(HintGenerator.SparseCount, hintLines.Count, "First-run hints are sparse");
                CollectionAssert.AreEqual(hints.Select(h => h.Text), hintLines, $"mushroom {i}: its hint lines");
                foreach (Hint hint in hints) AssertHintTrueInWorld(hint, $"mushroom {i}");
                if (i == 0) Assert.AreEqual(Dialogue.IsotopeStageLine, lines[lines.Count - 1]);
            }

            NpcTalker talker = chemistry.Mushrooms[0];
            Vector2 near = (Vector2)talker.transform.position + Vector2.left * 0.9f;
            navigator.TeleportTo(ScreenOf(talker.transform.position, face), Facing.East);
            body.position = near;
            navigator.transform.position = new Vector3(near.x, near.y, navigator.transform.position.z);
            yield return null;
            Assert.IsTrue(talker.TryTalk(), "A mushroom can be talked to");
            Assert.IsTrue(talker.Box.IsOpen);
            talker.Box.Close();

            Assert.AreEqual(ChemistryFace.RadicalCount + ChemistryFace.MiteCount, chemistry.Enemies.Count);
            foreach (Enemy enemy in chemistry.Enemies)
            {
                Assert.IsTrue(OnFace(enemy.transform.position, face, null), "Chemistry enemies live on the Chemistry face");
                ChemistryEnemyKind kind = enemy.GetComponent<ChemistryEnemy>().Kind;
                ItemDefinition expected = kind == ChemistryEnemyKind.FreeRadical ? world.ItemFor(Theme.Chemistry) : world.ItemFor(Theme.Biology);
                Assert.AreSame(expected, enemy.Weakness, enemy.name);
            }
        }
    }
}
