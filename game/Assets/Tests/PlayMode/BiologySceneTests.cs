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
    /// Darwin's face in the real Cube scene. Full runs from Town onto the Biology face: a thin-beak seed picks up
    /// the beak, pollinates the flower whose vine bridge opens a gate on the adjacent screen, presses every
    /// distant button (one on another face) and finishes the trial; a thick-beak seed breaks every rock and pot
    /// (one on another face) and finishes the rock trial. Also: the reveal builds exactly the planned beak gates
    /// (the other beak's as optional extras), finch NPCs are present, talkable, Darwin-flavoured and true, and
    /// Biology enemies are placed and weak to an item.
    /// </summary>
    public class BiologySceneTests : InputTestFixture
    {
        private const string SceneName = "Cube";
        private const float Timeout = 6f;
        private const float ThinDistance = 2.0f;

        private Keyboard keyboard;
        private CubeWorld world;
        private CubeNavigator navigator;
        private BiologyFace biology;
        private Inventory inventory;
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
            biology = world.GetComponent<BiologyFace>();
            Assert.IsNotNull(biology, "The CubeWorld carries the BiologyFace content hook");
            CollectionAssert.Contains(world.FaceContents, biology);
            inventory = navigator.GetComponent<Inventory>();
            attack = navigator.GetComponent<PlayerAttack>();
            body = navigator.GetComponent<Rigidbody2D>();
            // A long scripted run: keep the player alive so stray contacts never end it early.
            navigator.GetComponent<PlayerStats>().MaxHealth = 999;
        }

        /// <summary>The placement the game makes for a seed (pure), for picking test seeds.</summary>
        private ItemPlacement PlacementFor(int seed, out CubeModel model)
        {
            model = new CubeModel(seed);
            return ItemPlacement.ForRun(model, CubeLayout.Generate(model, world.Library), world.Library,
                world.ItemCatalog.Themes(), world.ItemCatalog.VariantCounts());
        }

        /// <summary>The first seed with this beak whose beak pickup lies in the open.</summary>
        private int SeedWith(BeakKind beak)
        {
            for (int seed = 1; seed < 500; seed++)
            {
                if (BiologyBeaks.Roll(seed) != beak) continue;
                ItemPlacement p = PlacementFor(seed, out _);
                if (p.TryGetPickup(Theme.Biology, out PickupPlacement pickup) && !pickup.IsGuarded) return seed;
            }
            Assert.Fail($"No {beak} seed with an open beak pickup");
            return 0;
        }

        /// <summary>A new run on the seed in Town, then off Town onto the Biology face (the reveal).</summary>
        private IEnumerator StartRunOntoBiology(int seed, bool enemies)
        {
            world.Rebuild(seed);
            yield return null;
            Assert.AreEqual(CubeModel.StartFace, navigator.Face, "A run starts in Town");
            Assert.IsFalse(world.ScienceRevealed);
            biology.SpawnEnemies = enemies;
            navigator.TeleportTo(new ScreenAddress(world.Model.FaceOf(Theme.Biology), 0, 0), Facing.East);
            Assert.IsTrue(world.ScienceRevealed, "Leaving Town reveals the science faces");
            Assert.IsNotNull(biology.Plan);
            yield return null;
        }

        private IEnumerator PickUpBeak()
        {
            Assert.IsTrue(world.ItemPlacement.TryGetPickup(Theme.Biology, out PickupPlacement pickup));
            Assert.IsFalse(pickup.IsGuarded);
            ItemDefinition beak = world.ItemFor(Theme.Biology);
            navigator.TeleportTo(pickup.Screen, Facing.East);
            yield return null;
            yield return this.HoldUntil(keyboard.sKey, () => inventory.Has(beak), Timeout);
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(inventory.Has(beak), "Walking onto the beak picks it up");
            Assert.AreSame(beak, navigator.GetComponent<Equipment>().Equipped, "The beak is equipped");
        }

        /// <summary>Turns the player to face a direction with a short move press.</summary>
        private IEnumerator Face(Facing direction)
        {
            yield return this.TapThroughPhysics(KeyFor(keyboard, direction));
            Assert.AreEqual((Vector2)direction.ToVector(), attack.Facing, "Facing");
        }

        /// <summary>Stands distance away from a target on its screen, facing it, and swings once.</summary>
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

        private BeakGate GateFor(BeakGateSpec spec) =>
            world.ModuleAt(spec.Screen).Gates[spec.Slot].GetComponentInChildren<BeakGate>(true);

        private ScreenAddress ScreenOf(Vector2 position, FaceId face) => new ScreenAddress(face, world.CellAt(face, position));

        /// <summary>
        /// How to reach a trial piece (local to its screen centre) from the centre lane beside it: the facing,
        /// and its distance from the lane's centre line.
        /// </summary>
        private static Facing FromLane(Vector2 local, out float laneDistance)
        {
            if (Mathf.Abs(Mathf.Abs(local.y) - BiologyTrial.LaneSideOffset) < 0.01f)
            {
                laneDistance = Mathf.Abs(local.y);
                return local.y >= 0f ? Facing.North : Facing.South;
            }
            laneDistance = Mathf.Abs(local.x);
            return local.x >= 0f ? Facing.East : Facing.West;
        }

        [UnityTest]
        public IEnumerator Reveal_BuildsThePlannedBeakGates_TheOtherBeaksAsOptionalExtras()
        {
            yield return Load();
            foreach (BeakKind beak in new[] { BeakKind.Thin, BeakKind.Thick })
            {
                int seed = SeedWith(beak);
                yield return StartRunOntoBiology(seed, false);
                BiologyPlan plan = biology.Plan;
                Assert.AreEqual(beak, plan.Beak);
                Assert.AreEqual(beak, BiologyBeaks.KindOf(biology.BeakItem));
                Assert.AreEqual(plan.Gates.Count, biology.BeakGates.Count);
                Assert.AreEqual(world.ItemPlacement.OptionalGates.Count, world.OptionalGates.Count);
                Assert.Greater(world.OptionalGates.Count, 0, "This seed shows the other beak's gates");
                foreach (BeakGateSpec spec in plan.Gates)
                {
                    BeakGate gate = GateFor(spec);
                    Assert.IsNotNull(gate, spec.ToString());
                    Assert.AreEqual(spec.Kind, gate.Kind);
                    Assert.AreEqual(spec.Optional, gate.Optional);
                    Assert.AreEqual(spec.Difficulty, gate.Difficulty);
                    Assert.AreEqual(spec.Beak, BiologyBeaks.KindOf(gate.RequiredItem), spec.ToString());
                    Assert.IsFalse(gate.IsOpen);
                    if (spec.Optional)
                        Assert.IsFalse(world.ItemPlacement.Pickups.Any(p => p.IsGuarded && p.Screen == spec.Screen && p.GuardSlot == spec.Slot),
                            "Optional gates guard nothing required");
                }
                Assert.IsNotNull(biology.Trial, "The trial is built");
                Assert.AreEqual(plan.TrialScreen, ScreenOf(biology.Trial.transform.position, plan.Face));
                Assert.IsFalse(biology.Trial.IsComplete);
            }
        }

        [UnityTest]
        public IEnumerator ThinBeakRun_PicksUpTheBeak_PollinatesTheBridge_PressesButtons_FinishesTheTrial()
        {
            yield return Load();
            int seed = SeedWith(BeakKind.Thin);
            yield return StartRunOntoBiology(seed, false);
            BiologyPlan plan = biology.Plan;
            yield return PickUpBeak();
            Assert.AreEqual(BiologyBeaks.ThinReach, attack.EffectiveReach, 1e-4f);

            // The pollinated bridge: the flower on one screen opens the vine gate on the adjacent screen.
            BeakGateSpec vineSpec = plan.Gates.Single(s => s.Kind == BeakGateKind.FlowerVine);
            var vine = (FlowerVineGate)GateFor(vineSpec);
            Vector2 flower = vine.Flower.transform.position;
            Assert.AreEqual(vineSpec.FlowerScreen, ScreenOf(flower, plan.Face), "The flower is on the adjacent screen");
            for (int hit = 0; hit < vine.HitsToOpen; hit++)
            {
                Assert.IsFalse(vine.IsOpen);
                yield return StrikeFrom(vineSpec.FlowerScreen, flower, Facing.North, ThinDistance);
            }
            Assert.IsTrue(vine.IsOpen, $"Pollinating the flower on {vineSpec.FlowerScreen} opens {vineSpec}");
            Assert.IsNotNull(vine.Bridge, "A vine bridge grows");
            Assert.IsTrue(vine.Flower.Used);

            // Every required button door, including the one on another face.
            foreach (BeakGateSpec spec in plan.Gates.Where(s => !s.Optional && s.Kind == BeakGateKind.Button))
            {
                var door = (ButtonGate)GateFor(spec);
                Vector2 button = door.Button.transform.position;
                Facing toward = button.x >= world.ScreenCenter(spec.Screen).x ? Facing.East : Facing.West;
                for (int hit = 0; hit < door.HitsToOpen; hit++)
                    yield return StrikeFrom(spec.Screen, button, toward, ThinDistance);
                Assert.IsTrue(door.IsOpen, $"Pecking the distant button opens {spec}");
            }
            Assert.IsTrue(plan.Gates.Any(s => !s.Optional && s.Screen.Face != plan.Face && GateFor(s).IsOpen),
                "The thin beak opened a gate on another face");

            // The thick-beak extras stay shut for the thin beak.
            BeakGateSpec optional = plan.Gates.First(s => s.Optional);
            GateSlot slot = world.ModuleAt(optional.Screen).Gates[optional.Slot];
            yield return StrikeFrom(optional.Screen, slot.transform.position, slot.Opening.Opposite(), 1.0f);
            Assert.IsFalse(GateFor(optional).IsOpen, "A thick-beak gate ignores the thin beak");

            // The trial: two distant buttons, then the flower.
            BiologyTrial trial = biology.Trial;
            var fired = new List<int>();
            trial.Completed += c => fired.Add(c);
            Vector2 centre = world.ScreenCenter(plan.TrialScreen);
            foreach (TrialTarget target in trial.Targets)
            {
                Vector2 local = (Vector2)target.transform.position - centre;
                Facing toward = FromLane(local, out float distance); // from the lane, beyond normal reach
                yield return StrikeFrom(plan.TrialScreen, target.transform.position, toward, distance);
                Assert.IsTrue(target.Done, $"Trial {target.Kind} {target.Index + 1} reached from {distance:0.0} away");
            }
            Assert.IsTrue(trial.IsComplete);
            CollectionAssert.AreEqual(new[] { BiologyPlan.TrialCurrency }, fired, "The trial fires once with its currency");
        }

        [UnityTest]
        public IEnumerator ThickBeakRun_PicksUpTheBeak_BreaksRocksAndPots_FinishesTheTrial()
        {
            yield return Load();
            int seed = SeedWith(BeakKind.Thick);
            yield return StartRunOntoBiology(seed, false);
            BiologyPlan plan = biology.Plan;
            yield return PickUpBeak();

            foreach (BeakGateSpec spec in plan.Gates.Where(s => !s.Optional))
            {
                Assert.AreEqual(BeakGateKind.Breakable, spec.Kind);
                BeakGate gate = GateFor(spec);
                GateSlot slot = world.ModuleAt(spec.Screen).Gates[spec.Slot];
                for (int hit = 0; hit < gate.HitsToOpen; hit++)
                    yield return StrikeFrom(spec.Screen, slot.transform.position, slot.Opening.Opposite(), 1.0f);
                Assert.IsTrue(gate.IsOpen, $"The thick beak breaks {spec}");
                Assert.IsFalse(gate.Solid.enabled, "The alcove is open");
            }
            Assert.IsTrue(plan.Gates.Any(s => !s.Optional && s.Screen.Face != plan.Face),
                "The thick beak has a gate on another face");

            // Thin-beak extras: their buttons ignore the thick beak.
            BeakGateSpec optional = plan.Gates.First(s => s.Optional);
            var door = (ButtonGate)GateFor(optional);
            Vector2 button = door.Button.transform.position;
            Facing side = button.x >= world.ScreenCenter(optional.Screen).x ? Facing.East : Facing.West;
            yield return StrikeFrom(optional.Screen, button, side, 0.8f);
            Assert.IsFalse(door.IsOpen, "A thin-beak button ignores the thick beak");

            // The trial: a wrong rock resets, then rocks 1, 2, 3 in order.
            BiologyTrial trial = biology.Trial;
            var fired = new List<int>();
            trial.Completed += c => fired.Add(c);
            Vector2 centre = world.ScreenCenter(plan.TrialScreen);
            IEnumerator Strike(TrialTarget rock)
            {
                Vector2 local = (Vector2)rock.transform.position - centre;
                yield return StrikeFrom(plan.TrialScreen, rock.transform.position, FromLane(local, out _), 0.9f);
            }
            yield return Strike(trial.Targets[0]);
            Assert.AreEqual(1, trial.Progress);
            yield return Strike(trial.Targets[2]);
            Assert.AreEqual(0, trial.Progress, "A wrong rock puts every rock back");
            foreach (TrialTarget rock in trial.Targets)
            {
                yield return Strike(rock);
                Assert.IsTrue(rock.Done, $"Rock {rock.Index + 1}");
            }
            Assert.IsTrue(trial.IsComplete);
            CollectionAssert.AreEqual(new[] { BiologyPlan.TrialCurrency }, fired);
        }

        private bool OnFace(Vector2 position, FaceId face, ScreenAddress? screen)
        {
            Vector2 local = position - world.FaceOrigin(face);
            bool on = local.x > 0f && local.y > 0f && local.x < world.FaceExtent.x && local.y < world.FaceExtent.y;
            return on && (screen == null || world.CellAt(face, position) == screen.Value.Cell);
        }

        /// <summary>Checks a hint's claim against the objects the reveal actually built, not against the generator's input.</summary>
        private void AssertHintTrueInWorld(Hint hint, string context)
        {
            string text = hint.Text;
            ScreenAddress? screen = hint.Precise ? hint.Screen : (ScreenAddress?)null;
            Assert.AreEqual(world.Model.ThemeOf(hint.Face), hint.FaceTheme, $"{context}: {text}");
            StringAssert.Contains(HintGenerator.ThemeName(hint.FaceTheme), text, context);
            switch (hint.Kind)
            {
                case HintKind.ItemLocation:
                    Assert.IsTrue(world.Pickups.Any(p => p != null && p.Item.HomeTheme == hint.Item && OnFace(p.transform.position, hint.Face, screen)),
                        $"{context}: no {hint.Item} pickup there ({text})");
                    StringAssert.Contains(HintGenerator.ItemNickname(hint.Item), text, context);
                    break;
                case HintKind.GateLocation:
                    Assert.IsTrue(world.Gates.Any(g => g != null && g.RequiredItem.HomeTheme == hint.Item && OnFace(g.transform.position, hint.Face, screen)),
                        $"{context}: no {hint.Item} gate there ({text})");
                    StringAssert.Contains(HintGenerator.ThemeName(hint.Item) + " gate", text, context);
                    break;
                case HintKind.CoreEntrance:
                    Assert.IsTrue(world.AllModules.Any(m => m.ActiveCoreEntrances().Count > 0 && OnFace(m.transform.position, hint.Face, screen)),
                        $"{context}: no core entrance there ({text})");
                    StringAssert.Contains("core", text, context);
                    break;
                default:
                    Assert.AreEqual(world.Model.FaceOf(hint.FaceTheme), hint.Face, $"{context}: {text}");
                    bool nextDoor = HintGenerator.TryTownEdge(hint.Face, out _);
                    StringAssert.Contains(nextDoor ? "next door" : "long way", text, context);
                    break;
            }
        }

        [UnityTest]
        public IEnumerator FinchNpcs_ArePresent_Talkable_DarwinFlavoured_WithTrueHints()
        {
            yield return Load();
            int seed = SeedWith(BeakKind.Thin);
            yield return StartRunOntoBiology(seed, true);
            FaceId face = biology.Plan.Face;
            Assert.AreEqual(BiologyPopulation.FinchCount, biology.Finches.Count);
            RunFacts facts = new RunFacts(world.Model, world.Layout, world.ItemPlacement);
            for (int i = 0; i < biology.Finches.Count; i++)
            {
                NpcTalker finch = biology.Finches[i];
                Assert.IsNotNull(finch);
                Assert.AreEqual(Race.Finch, finch.Race);
                Vector2 local = (Vector2)finch.transform.position - world.FaceOrigin(face);
                Assert.IsTrue(local.x > 0f && local.y > 0f && local.x < world.FaceExtent.x && local.y < world.FaceExtent.y,
                    "Finches live on the Biology face");
                IReadOnlyList<string> lines = finch.Lines;
                Assert.AreEqual(Dialogue.MaxLines, lines.Count, "A flavour line, then the hint");
                if (i > 0) CollectionAssert.Contains(Dialogue.DarwinLines, lines[0], "A Darwin line");

                // The hint line (after the flavour line) must be sparse and true for what the world actually built:
                // its pickups, gates, core entrances and faces.
                List<Hint> hints = HintGenerator.Generate(facts, finch.Spec.HintKind, finch.Spec.Salt, HintGenerator.FirstRunDensity);
                Assert.AreEqual(HintGenerator.SparseCount, hints.Count, "First-run hints are sparse");
                Assert.AreEqual(hints[0].Text, lines[1], $"finch {i}: its hint line");
                foreach (Hint hint in hints) AssertHintTrueInWorld(hint, $"finch {i}");

                if (i == 0)
                {
                    BeakKind? actual = BiologyBeaks.KindOf(world.ItemFor(Theme.Biology));
                    Assert.IsTrue(actual.HasValue);
                    Assert.AreEqual(Dialogue.BeakLine(actual.Value == BeakKind.Thin), lines[0],
                        "The beak line names the beak the world actually placed");
                }
            }

            NpcTalker talker = biology.Finches[0];
            Vector2 near = (Vector2)talker.transform.position + Vector2.left * 0.9f;
            navigator.TeleportTo(ScreenOf(talker.transform.position, face), Facing.East);
            body.position = near;
            navigator.transform.position = new Vector3(near.x, near.y, navigator.transform.position.z);
            yield return null;
            Assert.IsTrue(talker.TryTalk(), "A finch can be talked to");
            Assert.IsTrue(talker.Box.IsOpen);
            talker.Box.Close();

            // Enemies: placed on Biology screens, a Seed Weevil weak to the beak and a Pollen Puff weak to another item.
            Assert.AreEqual(BiologyFace.WeevilCount + BiologyFace.PuffCount, biology.Enemies.Count);
            foreach (Enemy enemy in biology.Enemies)
            {
                Vector2 e = (Vector2)enemy.transform.position - world.FaceOrigin(face);
                Assert.IsTrue(e.x > 0f && e.y > 0f && e.x < world.FaceExtent.x && e.y < world.FaceExtent.y);
                BiologyEnemyKind kind = enemy.GetComponent<BiologyEnemy>().Kind;
                ItemDefinition expectedWeakness = kind == BiologyEnemyKind.SeedWeevil
                    ? world.ItemFor(Theme.Biology) : world.ItemFor(Theme.Chemistry);
                Assert.AreSame(expectedWeakness, enemy.Weakness, enemy.name);
            }
        }
    }
}
