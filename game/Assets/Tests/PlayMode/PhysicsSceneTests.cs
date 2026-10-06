using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Tracer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Game.Cube.Tests.TestInput;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Einstein's face in the real Cube scene. The reveal builds the planned timed doors (with their switches and their
    /// own boulders, off-home ones included) and the trial, and the player's Mass Mitt tracks the run's Physics item.
    /// A full run from Town: walk onto the mitt; at a timed door, the run from the switch fails with no boulders, a
    /// boulder is grabbed and dragged with the mitt, the door's required boulders go beside it and the run from the
    /// switch at base speed gets through; every other door (the off-home one included) the same way; then the trial,
    /// moving the same boulders from one booth to the other. Aliens are present, talkable, Einstein-flavoured and
    /// true, Newton gets bonked, and Physics enemies are weak to their items. Runs move the player at exactly the
    /// base speed (velocity set every physics step); beyond the one real drag, boulders are set beside each door
    /// directly (the drag itself is covered by the component tests).
    /// </summary>
    public class PhysicsSceneTests : InputTestFixture
    {
        private const string SceneName = "Cube";
        private const float Timeout = 6f;

        private Keyboard keyboard;
        private CubeWorld world;
        private CubeNavigator navigator;
        private PhysicsFace physics;
        private Inventory inventory;
        private MassMitt mitt;
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
            physics = world.GetComponent<PhysicsFace>();
            Assert.IsNotNull(physics, "The CubeWorld carries the PhysicsFace content hook");
            CollectionAssert.Contains(world.FaceContents, physics);
            inventory = navigator.GetComponent<Inventory>();
            mitt = navigator.GetComponent<MassMitt>();
            Assert.IsNotNull(mitt, "The player carries a MassMitt");
            body = navigator.GetComponent<Rigidbody2D>();
            Assert.AreEqual(TimeField.BasePlayerSpeed, navigator.GetComponent<PlayerMover>().Speed, "The base speed the doors are timed for");
            Assert.AreEqual(TimeField.PlayerRadius, navigator.GetComponent<CircleCollider2D>().radius, 1e-4f);
            navigator.GetComponent<PlayerStats>().MaxHealth = 999;
        }

        private ItemPlacement PlacementFor(int seed)
        {
            var model = new CubeModel(seed);
            return ItemPlacement.ForRun(model, CubeLayout.Generate(model, world.Library), world.Library,
                world.ItemCatalog.Themes(), world.ItemCatalog.VariantCounts());
        }

        /// <summary>The first seed whose mitt lies in the open (walkable straight down from its screen centre).</summary>
        private int SeedWithOpenMitt()
        {
            for (int seed = 1; seed < 500; seed++)
                if (PlacementFor(seed).TryGetPickup(Theme.Physics, out PickupPlacement pickup) && !pickup.IsGuarded) return seed;
            Assert.Fail("No seed with an open Mass Mitt");
            return 0;
        }

        private IEnumerator StartRunOntoPhysics(int seed, bool enemies)
        {
            world.Rebuild(seed);
            yield return null;
            Assert.AreEqual(CubeModel.StartFace, navigator.Face, "A run starts in Town");
            Assert.IsFalse(world.ScienceRevealed);
            Assert.IsFalse(mitt.IsHeld, "A run starts with no mitt");
            physics.SpawnEnemies = enemies;
            world.GetComponent<BiologyFace>().SpawnEnemies = false;
            world.GetComponent<ChemistryFace>().SpawnEnemies = false;
            navigator.TeleportTo(new ScreenAddress(world.Model.FaceOf(Theme.Physics), 0, 0), Facing.East);
            Assert.IsTrue(world.ScienceRevealed, "Leaving Town reveals the science faces");
            Assert.IsNotNull(physics.Plan);
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

        /// <summary>Moves the player toward a point at speed, every physics step, until it arrives or stop() holds.</summary>
        private IEnumerator MoveTo(Vector2 to, float speed, Func<bool> stop = null, float timeout = 4f)
        {
            var mover = navigator.GetComponent<PlayerMover>();
            mover.enabled = false;
            float t = 0f;
            while (t < timeout && (stop == null || !stop()))
            {
                Vector2 d = to - body.position;
                if (d.magnitude < 0.02f) break;
                body.linearVelocity = d.normalized * Mathf.Min(speed, d.magnitude / Time.fixedDeltaTime);
                yield return new WaitForFixedUpdate();
                t += Time.fixedDeltaTime;
            }
            body.linearVelocity = Vector2.zero;
            mover.enabled = true;
        }

        /// <summary>Stands on a door's switch, then runs at base speed to its threshold.</summary>
        private IEnumerator RunFromSwitch(ScreenAddress screen, TimedDoorGate door)
        {
            yield return PutPlayerAt(screen, door.Switch.transform.position);
            Assert.IsTrue(door.IsAjar || door.IsOpen, $"Stepping on the switch opens {door.name}");
            yield return MoveTo(door.Threshold, TimeField.BasePlayerSpeed, () => door.IsOpen);
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        /// <summary>World spots in the lane beside a door's threshold, inside its field, clear of the run from its switch.</summary>
        private static Vector2[] BesideTheDoor(TimedDoorGate door)
        {
            float below = door.Opening.y < 0f ? -1.25f : 1.25f;
            Vector2 q = door.FieldCentre;
            return new[] { q + new Vector2(0f, below), q + new Vector2(-0.95f, below), q + new Vector2(0.95f, below) };
        }

        /// <summary>A direction from a point along which the player fits at near and at far (no solid collider but boulders).</summary>
        private static Vector2 ClearDirection(Vector2 from, float near, float far)
        {
            Physics2D.SyncTransforms();
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI / 8f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                if (Fits(from + dir * near) && Fits(from + dir * far) && Fits(from + dir * (near + far) / 2f) &&
                    Fits(from + dir * 0.4f) && Fits(from + dir * 0.8f)) return dir;
            }
            Assert.Fail($"No room beside the boulder at {from}");
            return Vector2.up;
        }

        private static bool Fits(Vector2 at)
        {
            foreach (Collider2D c in Physics2D.OverlapCircleAll(at, TimeField.PlayerRadius + 0.05f))
                if (c != null && c.enabled && !c.isTrigger && c.GetComponentInParent<CubeNavigator>() == null &&
                    c.GetComponent<Boulder>() == null) return false;
            return true;
        }

        private ScreenAddress ScreenOf(Vector2 position, FaceId face) => new ScreenAddress(face, world.CellAt(face, position));

        private List<Boulder> BouldersOn(ScreenAddress screen) =>
            physics.Boulders.Where(b => b != null && OnScreen(b.Position, screen)).ToList();

        private bool OnScreen(Vector2 position, ScreenAddress screen)
        {
            Vector2 local = position - world.ScreenCenter(screen);
            return Mathf.Abs(local.x) < CubeWorld.ScreenSize.x / 2f && Mathf.Abs(local.y) < CubeWorld.ScreenSize.y / 2f;
        }

        [UnityTest]
        public IEnumerator Reveal_BuildsThePlannedTimedDoors_WithTheirSwitchesAndBoulders_AndTheTrial()
        {
            yield return Load();
            yield return StartRunOntoPhysics(SeedWithOpenMitt(), false);
            PhysicsPlan plan = physics.Plan;
            ItemDefinition item = world.ItemFor(Theme.Physics);
            Assert.AreEqual(MassMittItem.Id, item.Id);
            Assert.AreEqual(MassMittItem.Name, item.DisplayName);
            Assert.AreSame(item, mitt.Item, "The player's mitt tracks the run's Physics item");
            Assert.IsTrue(plan.HasLayout);
            Assert.AreEqual(plan.Doors.Count, physics.Doors.Count);
            int boulders = 0;
            foreach (TimedDoorSpec spec in plan.Doors)
            {
                TimedDoorGate door = physics.DoorFor(spec);
                Assert.IsNotNull(door, spec.ToString());
                Assert.AreSame(door, world.ModuleAt(spec.Screen).Gates[spec.Slot].GetComponentInChildren<Gate>(true), "In its slot");
                Assert.AreSame(item, door.RequiredItem);
                CollectionAssert.Contains(world.Gates, door);
                Assert.IsFalse(door.IsOpen);
                Assert.IsFalse(door.IsAjar);
                Assert.AreEqual(spec.Required, door.Required);
                Assert.AreEqual(spec.Layout.BaseSeconds, door.BaseSeconds, 1e-4f);
                Vector2 centre = world.ScreenCenter(spec.Screen);
                Assert.Less(Vector2.Distance(centre + spec.Layout.SwitchLocal, door.Switch.transform.position), 1e-3f, "Its switch");
                Assert.Less(Vector2.Distance(centre + spec.Layout.FieldLocal, door.FieldCentre), 1e-3f, "Its field");
                Assert.AreEqual(0f, door.MassInField, $"{spec}: no boulder starts in its field");
                List<Boulder> mine = BouldersOn(spec.Screen);
                Assert.GreaterOrEqual(mine.Count, spec.Required + spec.Spare, $"{spec}: its own boulders on its screen");
                foreach (Vector2 local in spec.Boulders)
                    Assert.IsTrue(mine.Any(b => Vector2.Distance(b.Position, centre + local) < 1e-3f), $"{spec}: a boulder at {local}");
                boulders += spec.Boulders.Count;
            }
            Assert.AreEqual(boulders, physics.Boulders.Count);
            Assert.IsTrue(plan.Doors.Any(d => !d.OnHome && physics.DoorFor(d) != null), "A timed door on another face");

            Assert.IsNotNull(physics.Trial, "The trial is built");
            Assert.AreEqual(plan.TrialScreen, ScreenOf(physics.Trial.transform.position, plan.Face));
            Assert.AreEqual(PhysicsPlan.TrialBooths, physics.Trial.Doors.Count);
            Assert.AreEqual(PhysicsPlan.TrialRequired, physics.Trial.Boulders.Count);
            Assert.IsFalse(physics.Trial.IsComplete);
        }

        [UnityTest]
        public IEnumerator FullRun_TownToTheMitt_ThroughEveryTimedDoorWithBoulders_AndTheTrial()
        {
            yield return Load();
            yield return StartRunOntoPhysics(SeedWithOpenMitt(), false);
            PhysicsPlan plan = physics.Plan;
            Assert.IsTrue(world.ItemPlacement.TryGetPickup(Theme.Physics, out PickupPlacement pickup));

            // The mitt: walk onto it from its screen centre.
            navigator.TeleportTo(pickup.Screen, Facing.East);
            yield return null;
            yield return this.HoldUntil(keyboard.sKey, () => mitt.IsHeld, Timeout);
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(mitt.IsHeld, "Walking onto the mitt picks it up");
            navigator.GetComponent<Equipment>().Equip(mitt.Item);
            Assert.IsTrue(mitt.IsEquipped);
            StringAssert.Contains("Mass Mitt", mitt.HudText);

            // Where every boulder starts (boulders set beside one door go back before the next door's run).
            var starts = new Dictionary<Boulder, Vector2>();
            foreach (Boulder b in physics.Boulders.Concat(physics.Trial.Boulders)) starts[b] = b.Position;
            void ResetBoulders(ScreenAddress screen)
            {
                foreach (KeyValuePair<Boulder, Vector2> e in starts)
                    if (OnScreen(e.Value, screen)) e.Key.Teleport(e.Value);
            }

            bool first = true;
            foreach (TimedDoorSpec spec in plan.Doors)
            {
                TimedDoorGate door = physics.DoorFor(spec);
                ResetBoulders(spec.Screen);
                List<Boulder> mine = BouldersOn(spec.Screen);
                Assert.GreaterOrEqual(mine.Count, spec.Required, $"{spec}: enough boulders on its screen");
                if (first)
                {
                    // No boulders beside it: the run from the switch fails.
                    yield return RunFromSwitch(spec.Screen, door);
                    Assert.IsFalse(door.IsOpen, $"{spec}: impassable with no mass");
                    Assert.GreaterOrEqual(door.Closings, 1);

                    // A real drag: stand clear beside one of the door's boulders, grab it with the mitt and pull it a little.
                    Boulder dragged = mine[0];
                    Vector2 from = dragged.Position;
                    Vector2 away = ClearDirection(from, 1.0f, 1.8f);
                    yield return PutPlayerAt(spec.Screen, from + away * 1.0f);
                    Assert.IsTrue(mitt.Grab(dragged), "The mitt grabs the boulder");
                    Assert.AreSame(dragged, mitt.Held);
                    yield return MoveTo(body.position + away * 0.8f, 3f);
                    yield return new WaitForFixedUpdate();
                    yield return new WaitForFixedUpdate();
                    Assert.Greater(Vector2.Distance(from, dragged.Position), 0.5f, "The boulder follows the mitt");
                    Assert.IsTrue(mitt.Interact(), "A second press lets go");
                    Assert.IsNull(mitt.Held);
                    first = false;
                }

                // Its required boulders beside it (boulders from its own screen), then the run.
                Vector2[] spots = BesideTheDoor(door);
                for (int i = 0; i < spec.Required; i++) mine[i].Teleport(spots[i]);
                yield return new WaitForFixedUpdate();
                Assert.GreaterOrEqual(door.MassInField, spec.Required, $"{spec}: its boulders beside it");
                yield return RunFromSwitch(spec.Screen, door);
                Assert.IsTrue(door.IsOpen, $"{spec}: passable with its {spec.Required} boulders");
                Assert.IsFalse(door.Solid.enabled);
            }
            Assert.IsTrue(plan.Doors.Any(d => !d.OnHome && physics.DoorFor(d).IsOpen), "The mitt opened a door on another face");
            Assert.IsTrue(physics.Doors.All(d => d.IsOpen), "Every timed door is open");

            // The trial: the same boulders, first beside one booth, then beside the other.
            PhysicsTrial trial = physics.Trial;
            ResetBoulders(plan.TrialScreen);
            var fired = new List<int>();
            trial.Completed += c => fired.Add(c);
            for (int k = 0; k < trial.Doors.Count; k++)
            {
                TimedDoorGate booth = trial.Doors[k];
                Vector2[] spots = BesideTheDoor(booth);
                for (int i = 0; i < trial.Boulders.Count; i++) trial.Boulders[i].Teleport(spots[i]);
                yield return new WaitForFixedUpdate();
                Assert.AreEqual(PhysicsPlan.TrialRequired, booth.MassInField, 1e-4f, $"booth {k}: the shared boulders beside it");
                yield return RunFromSwitch(plan.TrialScreen, booth);
                Assert.IsTrue(booth.IsOpen, $"booth {k} passes with the shared boulders");
            }
            Assert.IsTrue(trial.IsComplete);
            CollectionAssert.AreEqual(new[] { PhysicsPlan.TrialCurrency }, fired, "The trial fires once with its currency");
            Assert.IsFalse(trial.Complete(), "Repeats do not fire");
            Assert.AreEqual(1, fired.Count);
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
        public IEnumerator Aliens_ArePresent_Talkable_EinsteinFlavoured_WithTrueHints_NewtonIsBonked_AndEnemiesAreWeakToItems()
        {
            yield return Load();
            yield return StartRunOntoPhysics(SeedWithOpenMitt(), true);
            FaceId face = physics.Plan.Face;
            Assert.AreEqual(PhysicsPopulation.AlienCount, physics.Aliens.Count);
            var facts = new RunFacts(world.Model, world.Layout, world.ItemPlacement);
            for (int i = 0; i < physics.Aliens.Count; i++)
            {
                NpcTalker alien = physics.Aliens[i];
                Assert.IsNotNull(alien);
                Assert.AreEqual(Race.Alien, alien.Race);
                Assert.IsTrue(OnFace(alien.transform.position, face, null), "Aliens live on the Physics face");
                IReadOnlyList<string> lines = alien.Lines;
                CollectionAssert.Contains(Dialogue.Flavours[Race.Alien].Greetings, lines[0], "An alien greeting");
                CollectionAssert.Contains(Dialogue.EinsteinLines, lines[1], "An Einstein line");
                int mittLines = i == 0 ? 1 : 0;
                List<string> hintLines = lines.Skip(3).Take(lines.Count - 3 - mittLines).ToList();
                List<Hint> hints = HintGenerator.Generate(facts, alien.Spec.HintKind, alien.Spec.Salt, HintGenerator.FirstRunDensity);
                Assert.AreEqual(HintGenerator.SparseCount, hintLines.Count, "First-run hints are sparse");
                CollectionAssert.AreEqual(hints.Select(h => h.Text), hintLines, $"alien {i}: its hint lines");
                foreach (Hint hint in hints) AssertHintTrueInWorld(hint, $"alien {i}");
                if (i == 0) Assert.AreEqual(Dialogue.MassMittLine, lines[lines.Count - 1]);
            }

            NpcTalker talker = physics.Aliens[0];
            Vector2 near = (Vector2)talker.transform.position + Vector2.left * 0.9f;
            navigator.TeleportTo(ScreenOf(talker.transform.position, face), Facing.East);
            body.position = near;
            navigator.transform.position = new Vector3(near.x, near.y, navigator.transform.position.z);
            yield return null;
            Assert.IsTrue(talker.TryTalk(), "An alien can be talked to");
            Assert.IsTrue(talker.Box.IsOpen);
            talker.Box.Close();

            Assert.IsNotNull(physics.Newton, "Sir Isaac Newton visits");
            Assert.IsTrue(OnFace(physics.Newton.transform.position, face, null));
            CollectionAssert.IsSubsetOf(Dialogue.NewtonLines, physics.Newton.Lines.ToList());
            var bonk = physics.Newton.GetComponent<AppleBonk>();
            Assert.IsNotNull(bonk);
            yield return new WaitForSeconds(AppleBonk.FallSeconds + 0.1f);
            Assert.GreaterOrEqual(bonk.Bonks, 1, "An apple has bonked Newton");

            Assert.AreEqual(PhysicsFace.FleaCount + PhysicsFace.ClingCount, physics.Enemies.Count);
            foreach (Enemy enemy in physics.Enemies)
            {
                Assert.IsTrue(OnFace(enemy.transform.position, face, null), "Physics enemies live on the Physics face");
                PhysicsEnemyKind kind = enemy.GetComponent<PhysicsEnemy>().Kind;
                ItemDefinition expected = kind == PhysicsEnemyKind.QuantumFlea ? world.ItemFor(Theme.Physics) : world.ItemFor(Theme.Chemistry);
                Assert.AreSame(expected, enemy.Weakness, enemy.name);
            }
        }
    }
}
