using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Game.Tracer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Game.Cube.Tests.TestInput;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Curie's face in the real Cube scene. The reveal builds the planned stage gates (the rolled stone's, home and
    /// off-home, and the other stones' optional ones) and the stone as a plain one-shot pickup; no timer or dispenser
    /// exists. A full run for each stone, from Town: walk onto the stone (picked up once, equipped, a popup), open every
    /// required gate by swinging it (including the one on another face), show another stone's gate ignores it while its
    /// own stone opens it, and finish the trial; the stone never changes. Mushroom NPCs are present, talkable, name the
    /// rolled stone, tell the changing-stones lore, and their hints are true in the world; Chemistry enemies are weak to
    /// their items.
    /// </summary>
    public class ChemistrySceneTests : InputTestFixture
    {
        /// <summary>Lengths and speeds below are in the first 16x10 screens' units, scaled like the game's (4.1).</summary>
        private const float K = ScreenMath.ContentScale;

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
            world = Object.FindAnyObjectByType<CubeWorld>();
            navigator = Object.FindAnyObjectByType<CubeNavigator>();
            Assert.IsNotNull(world);
            chemistry = world.GetComponent<ChemistryFace>();
            Assert.IsNotNull(chemistry, "The CubeWorld carries the ChemistryFace content hook");
            CollectionAssert.Contains(world.FaceContents, chemistry);
            inventory = navigator.GetComponent<Inventory>();
            isotope = navigator.GetComponent<Isotope>();
            Assert.IsNotNull(isotope, "The player carries the held-stone display");
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

        /// <summary>The first seed rolling this stone whose pickup lies in the open (walkable from its screen centre).</summary>
        private int SeedWith(IsotopeStage stage)
        {
            for (int seed = 1; seed < 500; seed++)
            {
                if (ChemistryIsotope.KindOf(world.ItemCatalog.ItemFor(Theme.Chemistry, seed)) != stage) continue;
                ItemPlacement p = PlacementFor(seed, out _);
                if (p.TryGetPickup(Theme.Chemistry, out PickupPlacement pickup) && !pickup.IsGuarded) return seed;
            }
            Assert.Fail($"No {stage} seed with an open stone pickup");
            return 0;
        }

        private IEnumerator StartRunOntoChemistry(int seed, bool enemies)
        {
            world.Rebuild(seed);
            yield return null;
            Assert.AreEqual(CubeModel.StartFace, navigator.Face, "A run starts in Town");
            Assert.IsFalse(world.ScienceRevealed);
            Assert.IsFalse(isotope.IsHeld, "A run starts with no stone");
            chemistry.SpawnEnemies = enemies;
            world.GetComponent<BiologyFace>().SpawnEnemies = false;
            world.GetComponent<PhysicsFace>().SpawnEnemies = false;
            world.GetComponent<MathFace>().SpawnEnemies = false;
            world.GetComponent<EarthFace>().SpawnEnemies = false;
            navigator.TeleportTo(new ScreenAddress(world.Model.FaceOf(Theme.Chemistry), 0, 0), Facing.East);
            Assert.IsTrue(world.ScienceRevealed, "Leaving Town reveals the science faces");
            Assert.IsNotNull(chemistry.Plan);
            yield return null;
        }

        private IEnumerator PickUpStone()
        {
            Assert.IsTrue(world.ItemPlacement.TryGetPickup(Theme.Chemistry, out PickupPlacement pickup));
            Assert.IsFalse(pickup.IsGuarded);
            ItemDefinition stone = world.ItemFor(Theme.Chemistry);
            int popups = PickupPopup.Active.Count(p => p != null && p.Text == PickupFeedback.ItemText(stone));
            navigator.TeleportTo(pickup.Screen, Facing.East);
            yield return null;
            yield return this.HoldUntil(keyboard.sKey, () => inventory.Has(stone), Timeout);
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(inventory.Has(stone), "Walking onto the stone picks it up");
            Assert.AreSame(stone, navigator.GetComponent<Equipment>().Equipped, "The stone is equipped");
            Assert.AreSame(stone, isotope.Item);
            Assert.AreEqual(stone.DisplayName, isotope.StageLabel, "The label shows the stone's name");
            Assert.Greater(PickupPopup.Active.Count(p => p != null && p.Text == PickupFeedback.ItemText(stone)), popups, "The pickup popup");
            Assert.IsFalse(world.Pickups.Any(p => p != null && p.Item == stone), "The pickup is gone; nothing respawns");
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
            Vector2 at = target - (Vector2)toward.ToVector() * distance;
            body.position = at;
            body.linearVelocity = Vector2.zero;
            navigator.transform.position = new Vector3(at.x, at.y, navigator.transform.position.z);
            Physics2D.SyncTransforms();
            Assert.GreaterOrEqual(attack.TryAttack(), 0, "The swing happened");
            yield return null;
        }

        /// <summary>One swing at a stage gate: at its plate (from the lane, toward the plate) or at the gate itself.</summary>
        private IEnumerator StrikeGate(IsotopeGateSpec spec, IsotopeGate gate)
        {
            if (gate is LeadPlateGate door)
            {
                Vector2 plate = door.Plate.transform.position;
                Facing toward = plate.x >= world.ScreenCenter(spec.Screen).x ? Facing.East : Facing.West;
                yield return StrikeFrom(spec.Screen, plate, toward, 0.9f * K);
            }
            else
            {
                GateSlot slot = world.ModuleAt(spec.Screen).Gates[spec.Slot];
                yield return StrikeFrom(spec.Screen, slot.transform.position, slot.Opening.Opposite(), 1.0f * K);
            }
        }

        private IsotopeGate GateFor(IsotopeGateSpec spec) =>
            world.ModuleAt(spec.Screen).Gates[spec.Slot].GetComponentInChildren<IsotopeGate>(true);

        private ScreenAddress ScreenOf(Vector2 position, FaceId face) => new ScreenAddress(face, world.CellAt(face, position));

        private ItemDefinition VariantItem(IsotopeStage stage) =>
            world.ItemCatalog.VariantsOf(Theme.Chemistry).Single(i => ChemistryIsotope.KindOf(i) == stage);

        private static Facing FromLane(Vector2 local)
        {
            if (Mathf.Abs(Mathf.Abs(local.y) - BiologyTrial.LaneSideOffset) < 0.01f * K)
                return local.y >= 0f ? Facing.North : Facing.South;
            return local.x >= 0f ? Facing.East : Facing.West;
        }

        [UnityTest]
        public IEnumerator Reveal_BuildsThePlannedStageGates_TheOtherStonesAsOptionalExtras_AndAPlainStonePickup()
        {
            yield return Load();
            foreach (IsotopeStage stage in ChemistryIsotope.VariantOrder)
            {
                yield return StartRunOntoChemistry(SeedWith(stage), false);
                ChemistryPlan plan = chemistry.Plan;
                ItemDefinition stone = world.ItemFor(Theme.Chemistry);
                Assert.AreEqual(stage, plan.Stage);
                Assert.AreEqual(stage, ChemistryIsotope.KindOf(stone));
                Assert.AreSame(stone, chemistry.IsotopeItem);
                Assert.AreEqual(plan.Gates.Count, chemistry.StageGates.Count);
                Assert.AreEqual(world.ItemPlacement.OptionalGates.Count, world.OptionalGates.Count);
                foreach (IsotopeStage otherStage in ChemistryIsotope.VariantOrder.Where(s => s != stage))
                    Assert.IsTrue(plan.Gates.Any(s => s.Optional && s.Stage == otherStage), $"{stage} run: optional {otherStage} gates exist");
                foreach (IsotopeGateSpec spec in plan.Gates)
                {
                    IsotopeGate gate = GateFor(spec);
                    Assert.IsNotNull(gate, spec.ToString());
                    Assert.AreEqual(spec.Stage, gate.Stage, spec.ToString());
                    Assert.AreEqual(spec.Optional, gate.Optional);
                    Assert.AreEqual(spec.Difficulty, gate.Difficulty);
                    Assert.AreEqual(spec.Stage, ChemistryIsotope.KindOf(gate.RequiredItem), spec.ToString());
                    Assert.IsFalse(gate.IsOpen);
                    if (spec.Optional)
                    {
                        CollectionAssert.Contains(world.OptionalGates, gate);
                        Assert.IsFalse(world.ItemPlacement.Pickups.Any(p => p.IsGuarded && p.Screen == spec.Screen && p.GuardSlot == spec.Slot),
                            "Optional gates guard nothing required");
                    }
                    else
                    {
                        CollectionAssert.Contains(world.Gates, gate);
                        Assert.AreSame(stone, gate.RequiredItem);
                    }
                }
                Assert.IsTrue(plan.Gates.Any(s => !s.Optional && s.Screen.Face != plan.Face), "A stage gate on another face");

                ItemPickup pickup = world.Pickups.Single(p => p != null && p.Item.HomeTheme == Theme.Chemistry);
                Assert.AreSame(stone, pickup.Item, "The rolled stone lies in the world");
                Assert.AreEqual(typeof(ItemPickup), pickup.GetType(), "A plain one-shot pickup");

                Assert.IsNotNull(chemistry.Trial, "The trial is built");
                Assert.AreEqual(stage, chemistry.Trial.Stage);
                Assert.AreEqual(plan.TrialScreen, ScreenOf(chemistry.Trial.transform.position, plan.Face));
                Assert.IsFalse(chemistry.Trial.IsComplete);
            }
        }

        [UnityTest]
        public IEnumerator GlowstoneRun_FromTown_OpensEveryGate_AndTheTrial() => FullRun(IsotopeStage.Glow);

        [UnityTest]
        public IEnumerator WildstoneRun_FromTown_OpensEveryGate_AndTheTrial() => FullRun(IsotopeStage.Unstable);

        [UnityTest]
        public IEnumerator LeadstoneRun_FromTown_OpensEveryGate_AndTheTrial() => FullRun(IsotopeStage.Lead);

        private IEnumerator FullRun(IsotopeStage stage)
        {
            yield return Load();
            yield return StartRunOntoChemistry(SeedWith(stage), false);
            ChemistryPlan plan = chemistry.Plan;
            yield return PickUpStone();
            ItemDefinition stone = world.ItemFor(Theme.Chemistry);

            // Every required gate, including the one on another face: swing the stone at it.
            foreach (IsotopeGateSpec spec in plan.Gates.Where(s => !s.Optional))
            {
                IsotopeGate gate = GateFor(spec);
                Assert.AreEqual(stage, gate.Stage);
                for (int hit = 0; hit < gate.HitsToOpen; hit++)
                {
                    Assert.IsFalse(gate.IsOpen);
                    yield return StrikeGate(spec, gate);
                }
                Assert.IsTrue(gate.IsOpen, $"The {stone} opens {spec}");
                Assert.IsFalse(gate.Solid.enabled, "The alcove is open");
            }
            Assert.IsTrue(plan.Gates.Any(s => !s.Optional && s.Screen.Face != plan.Face && GateFor(s).IsOpen),
                "The stone opened a gate on another face");

            // Another stone's optional gate ignores the run's stone, and opens to its own (an extra for later runs).
            IsotopeGateSpec optional = plan.Gates.First(s => s.Optional);
            IsotopeGate extra = GateFor(optional);
            yield return StrikeGate(optional, extra);
            Assert.AreEqual(0, extra.Hits, $"{optional} ignores the {stone}");
            Assert.IsFalse(extra.IsOpen);
            ItemDefinition own = VariantItem(optional.Stage);
            inventory.Add(own);
            navigator.GetComponent<Equipment>().Equip(own);
            for (int hit = 0; hit < extra.HitsToOpen; hit++) yield return StrikeGate(optional, extra);
            Assert.IsTrue(extra.IsOpen, $"{optional} opens to the {own}");
            navigator.GetComponent<Equipment>().Equip(stone);

            // The trial: the stone's three targets in order, from the lane beside each.
            ChemistryTrial trial = chemistry.Trial;
            var fired = new List<int>();
            trial.Completed += c => fired.Add(c);
            Vector2 centre = world.ScreenCenter(plan.TrialScreen);
            foreach (ChemistryTrialPiece piece in trial.Pieces)
            {
                Assert.AreEqual(ChemistryTrial.KindFor(stage), piece.Kind);
                Vector2 local = (Vector2)piece.transform.position - centre;
                yield return StrikeFrom(plan.TrialScreen, piece.transform.position, FromLane(local), 0.9f * K);
                Assert.IsTrue(piece.Done, $"Trial target {piece.Index + 1} struck with the {stone}");
            }
            Assert.IsTrue(trial.IsComplete);
            CollectionAssert.AreEqual(new[] { ChemistryPlan.TrialCurrency }, fired, "The trial fires once with its currency");

            // The stone never changed.
            Assert.AreEqual(stage, isotope.Stage);
            Assert.IsTrue(inventory.Has(stone), "The stone is kept for the run");
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
        public IEnumerator MushroomNpcs_ArePresent_Talkable_NameTheRolledStone_WithTrueHints_AndEnemiesAreWeakToItems()
        {
            yield return Load();
            yield return StartRunOntoChemistry(SeedWith(IsotopeStage.Lead), true);
            FaceId face = chemistry.Plan.Face;
            IsotopeStage stage = chemistry.Plan.Stage;
            Assert.AreEqual(ChemistryPopulation.MushroomCount(chemistry.Plan.FaceSize), chemistry.Mushrooms.Count);
            var facts = RunFacts.Of(world);
            for (int i = 0; i < chemistry.Mushrooms.Count; i++)
            {
                NpcTalker mushroom = chemistry.Mushrooms[i];
                Assert.IsNotNull(mushroom);
                Assert.AreEqual(Race.Mushroom, mushroom.Race);
                Assert.IsTrue(OnFace(mushroom.transform.position, face, null), "Mushrooms live on the Chemistry face");
                IReadOnlyList<string> lines = mushroom.Lines;
                Assert.AreEqual(Dialogue.MaxLines, lines.Count, "A flavour line, then the hint");
                if (i == ChemistryPopulation.StoneTeller)
                {
                    Assert.AreEqual(Dialogue.StoneLine(stage), lines[0]);
                    StringAssert.Contains(world.ItemFor(Theme.Chemistry).DisplayName, lines[0], "It names the rolled stone");
                }
                else if (i == ChemistryPopulation.LoreTeller) Assert.AreEqual(Dialogue.StoneLoreLine, lines[0]);
                else CollectionAssert.Contains(Dialogue.CurieLines, lines[0], "A Glowmother* line");

                List<Hint> hints = HintGenerator.For(mushroom.Spec, facts, HintGenerator.FirstRunDensity);
                Assert.AreEqual(HintGenerator.SparseCount, hints.Count, "First-run hints are sparse");
                Assert.AreEqual(hints[0].Text, lines[1], $"mushroom {i}: its hint line");
                foreach (Hint hint in hints) AssertHintTrueInWorld(hint, $"mushroom {i}");
            }

            NpcTalker talker = chemistry.Mushrooms[0];
            Vector2 near = (Vector2)talker.transform.position + Vector2.left * 0.9f * K;
            navigator.TeleportTo(ScreenOf(talker.transform.position, face), Facing.East);
            body.position = near;
            navigator.transform.position = new Vector3(near.x, near.y, navigator.transform.position.z);
            yield return null;
            Assert.IsTrue(talker.TryTalk(), "A mushroom can be talked to");
            Assert.IsTrue(talker.Box.IsOpen);
            talker.Box.Close();

            Assert.AreEqual(ChemistryFace.RadicalCount(chemistry.Plan.FaceSize) + ChemistryFace.MiteCount(chemistry.Plan.FaceSize), chemistry.Enemies.Count);
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
