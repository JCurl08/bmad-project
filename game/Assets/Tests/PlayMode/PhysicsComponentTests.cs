using System.Collections;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The Physics mechanics on objects built in the test (far from anything a scene may hold), one test per I/O row:
    /// without the Mass Mitt a boulder never moves; with it a grabbed boulder follows the player, stops at walls,
    /// never shoves the player, and is let go on a second press or when the player gets too far; a timed door run
    /// from its switch at base speed shuts before the player arrives with no mass (and one boulder short) and stays
    /// open with the required boulders beside it; more mass keeps it open longer (monotonic); the trial needs the
    /// same boulders moved from one booth to the other and fires once; Physics enemies take markedly more damage
    /// from their weakness. The player is moved at exactly the base speed by setting its velocity every physics step.
    /// </summary>
    public class PhysicsComponentTests : InputTestFixture
    {
        private static readonly Vector2 Origin = new Vector2(9000f, 7000f);

        private readonly List<Object> created = new List<Object>();
        private ItemDefinition mittItem, other;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            mittItem = Track(MassMittItem.CreateItem());
            other = Track(ItemDefinition.Create("other", "Other", Theme.Chemistry, Color.magenta));
        }

        public override void TearDown()
        {
            foreach (Object o in created)
                if (o != null) Object.Destroy(o);
            created.Clear();
            base.TearDown();
        }

        private T Track<T>(T o) where T : Object
        {
            created.Add(o);
            return o;
        }

        /// <summary>A player like the Cube scene's (dynamic body, radius 0.4) with a MassMitt; no PlayerMover, so tests drive its velocity.</summary>
        private MassMitt MakePlayer(Vector2 position, bool withMitt)
        {
            var go = Track(new GameObject("Test Player"));
            go.transform.position = position;
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            go.AddComponent<CircleCollider2D>().radius = TimeField.PlayerRadius;
            go.AddComponent<Inventory>();
            go.AddComponent<Health>();
            go.AddComponent<PlayerStats>();
            go.AddComponent<Equipment>();
            go.AddComponent<PlayerAttack>().Cooldown = 0f;
            var mitt = go.AddComponent<MassMitt>();
            mitt.Item = mittItem;
            mitt.ShowHud = false;
            if (withMitt) mitt.Inventory.Add(mittItem);
            return mitt;
        }

        private Boulder MakeBoulder(Vector2 at) => Track(Boulder.Create(null, at, null, null).gameObject).GetComponent<Boulder>();

        private GameObject Wall(Vector2 centre, Vector2 size)
        {
            var go = Track(new GameObject("Wall"));
            go.transform.position = centre;
            go.AddComponent<BoxCollider2D>().size = size;
            return go;
        }

        /// <summary>A timed door as PhysicsFace builds one in a slot: block with root collider and "Visual" child.</summary>
        private TimedDoorGate MakeDoor(DoorLayout layout)
        {
            var block = Track(new GameObject("Block"));
            block.transform.position = Origin + layout.DoorLocal;
            var visual = new GameObject("Visual");
            visual.transform.SetParent(block.transform, false);
            visual.AddComponent<SpriteRenderer>().sprite = NpcFactory.ShapeSprite(PartShape.Square);
            block.AddComponent<BoxCollider2D>().size = new Vector2(layout.Width, layout.Thickness);
            return TimedDoorGate.Build(block, mittItem, layout.Opening, layout.PocketDepth, Origin + layout.SwitchLocal, null,
                layout.BaseSeconds, layout.Required, null);
        }

        /// <summary>A door above the lane on the right, its switch at the far left of the lane (like a slot door's).</summary>
        private static DoorLayout SlotLikeDoor(int required) =>
            new DoorLayout(new Vector2(4.9f, 1.4f), Vector2.down, GateSlot.GateWidth, GateSlot.GateThickness, 1.9f, required)
                .WithSwitch(new Vector2(-6.3f, 0.6f));

        /// <summary>Spots in the lane beside a door's threshold, inside its field and clear of the run from the switch.</summary>
        private static Vector2[] BesideTheDoor(DoorLayout layout)
        {
            float below = layout.Opening.y < 0f ? -1.25f : 1.25f;
            Vector2 q = layout.FieldLocal;
            return new[] { q + new Vector2(0f, below), q + new Vector2(-0.95f, below), q + new Vector2(0.95f, below) };
        }

        private static IEnumerator Steps(int steps = 3)
        {
            for (int i = 0; i < steps; i++) yield return new WaitForFixedUpdate();
            yield return null;
        }

        private static void Put(Rigidbody2D body, Vector2 at)
        {
            body.position = at;
            body.transform.position = new Vector3(at.x, at.y, body.transform.position.z);
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }

        /// <summary>Moves the body toward a point at speed (every physics step) until it gets there, stop() holds, or time runs out.</summary>
        private static IEnumerator MoveTo(Rigidbody2D body, Vector2 to, float speed, System.Func<bool> stop = null, float timeout = 4f)
        {
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
        }

        /// <summary>Stands on the door's switch, then runs at base speed to its threshold. Returns via door.IsOpen.</summary>
        private IEnumerator RunFromSwitch(Rigidbody2D body, TimedDoorGate door)
        {
            Put(body, door.Switch.transform.position);
            yield return Steps(2);
            Assert.IsTrue(door.IsAjar || door.IsOpen, "Stepping on the switch opens the door");
            yield return MoveTo(body, door.Threshold, TimeField.BasePlayerSpeed, () => door.IsOpen);
            yield return Steps(2);
        }

        // ---------- No mitt ----------

        [UnityTest]
        public IEnumerator NoMitt_ABoulderCannotBeMoved_ByPressPushOrSwing()
        {
            MassMitt mitt = MakePlayer(Origin, false);
            var body = mitt.Body;
            Boulder boulder = MakeBoulder(Origin + new Vector2(0f, -1.0f));
            yield return Steps();
            Vector2 start = boulder.Position;

            Assert.IsFalse(mitt.Interact(), "Interact without the mitt grabs nothing");
            Assert.IsFalse(mitt.Toggle(boulder));
            Assert.IsFalse(mitt.Grab(boulder));
            mitt.GetComponent<PlayerAttack>().TryAttack(); // bare swing, facing down at it
            Assert.IsFalse(boulder.IsGrabbed, "A bare swing grabs nothing");

            // Another item equipped: still nothing.
            mitt.Inventory.Add(other);
            mitt.GetComponent<Equipment>().Equip(other);
            Assert.IsFalse(mitt.Interact());
            Assert.GreaterOrEqual(mitt.GetComponent<PlayerAttack>().TryAttack(), 0, "The swing happens");
            Assert.IsFalse(boulder.IsGrabbed, "A swing with another item grabs nothing");

            // Walk straight into it for a second: it does not budge, and the player stops against it.
            yield return MoveTo(body, Origin + new Vector2(0f, -3f), TimeField.BasePlayerSpeed, null, 1f);
            yield return Steps();
            Assert.Less(Vector2.Distance(start, boulder.Position), 1e-3f, "Pushing does not move a boulder");
            Assert.Greater(body.position.y, boulder.Position.y + Boulder.Radius + TimeField.PlayerRadius - 0.1f, "The boulder stops the player");
        }

        // ---------- Drag ----------

        [UnityTest]
        public IEnumerator Drag_TheBoulderFollows_StopsAtWalls_NeverShovesThePlayer_AndIsLetGo()
        {
            MassMitt mitt = MakePlayer(Origin, true);
            Rigidbody2D body = mitt.Body;
            Assert.AreSame(mittItem, mitt.GetComponent<Equipment>().Equipped, "The mitt is equipped");
            Boulder boulder = MakeBoulder(Origin + new Vector2(1.2f, 0f));
            yield return Steps();

            Assert.IsTrue(mitt.Interact(), "Interact with the mitt grabs the nearest boulder");
            Assert.AreSame(boulder, mitt.Held);
            Assert.IsTrue(boulder.IsGrabbed);
            StringAssert.Contains("dragging", mitt.HudText);

            // It follows the player (pulled to the left, keeping its offset).
            yield return MoveTo(body, Origin + new Vector2(-3f, 0f), TimeField.BasePlayerSpeed);
            yield return Steps();
            Assert.AreSame(boulder, mitt.Held, "Still held after the drag");
            Assert.Less(Vector2.Distance(boulder.Position, body.position + new Vector2(1.2f, 0f)), 0.2f, "The boulder follows at its offset");

            // Pushed into a wall, it stops at the wall; the player is stopped by the wall too and is never pushed through it.
            GameObject wall = Wall(Origin + new Vector2(2.05f, 0f), new Vector2(0.1f, 4f));
            float wallFace = 2.0f;
            yield return MoveTo(body, Origin + new Vector2(4f, 0f), TimeField.BasePlayerSpeed, null, 1.5f);
            yield return Steps(5);
            Assert.LessOrEqual(boulder.Position.x - Origin.x, wallFace - Boulder.Radius + 0.03f, "The boulder stops at the wall");
            Assert.LessOrEqual(body.position.x - Origin.x, wallFace - TimeField.PlayerRadius + 0.03f, "The player is not pushed into the wall");
            Assert.IsNull(mitt.Held, "Stuck behind the player, the boulder is let go");

            // Let go while overlapping: the player is not shoved out (no depenetration push).
            Vector2 standing = body.position;
            yield return Steps(5);
            Assert.Less(Vector2.Distance(standing, body.position), 0.02f, "Letting go never shoves the player");

            // Walk away, then back into it: once apart they collide again, and an unheld boulder does not move.
            yield return MoveTo(body, Origin + new Vector2(-1f, 0f), TimeField.BasePlayerSpeed);
            yield return Steps(3);
            Vector2 resting = boulder.Position;
            yield return MoveTo(body, Origin + new Vector2(3f, 0f), TimeField.BasePlayerSpeed, null, 0.8f);
            yield return Steps(3);
            Assert.Less(Vector2.Distance(resting, boulder.Position), 1e-3f, "An unheld boulder never moves");
            Assert.Less(body.position.x, boulder.Position.x - Boulder.Radius - TimeField.PlayerRadius + 0.1f, "The player bumps into it again");
            Object.Destroy(wall);

            // A second press lets go; a swing grabs and a second swing lets go.
            Assert.IsTrue(mitt.Interact());
            Assert.AreSame(boulder, mitt.Held);
            Assert.IsTrue(mitt.Interact());
            Assert.IsNull(mitt.Held, "A second press lets go");

            Put(body, boulder.Position + Vector2.up * 1.0f); // above it, facing down (no PlayerMover: down)
            yield return Steps();
            var attack = mitt.GetComponent<PlayerAttack>();
            attack.TryAttack();
            Assert.AreSame(boulder, mitt.Held, "A swing with the mitt grabs");
            attack.TryAttack();
            Assert.IsNull(mitt.Held, "A second swing lets go");

            // Too far: the player leaves (a teleport), the boulder is let go.
            Assert.IsTrue(mitt.Interact());
            Put(body, body.position + new Vector2(0f, 6f));
            yield return Steps(3);
            Assert.IsNull(mitt.Held, "Moving away lets go");
            Assert.IsFalse(boulder.IsGrabbed);
        }

        // ---------- Timed doors ----------

        [UnityTest]
        public IEnumerator TimedDoor_NoMass_ShutsBeforeThePlayerArrives_OneShortToo_RequiredMassPasses()
        {
            const int required = 3;
            DoorLayout layout = SlotLikeDoor(required);
            Assert.IsFalse(TimeField.Passable(layout.BaseSeconds, 0f, layout.WalkSeconds));
            MassMitt mitt = MakePlayer(Origin + layout.SwitchLocal + Vector2.down * 3f, true);
            Rigidbody2D body = mitt.Body;
            TimedDoorGate door = MakeDoor(layout);
            yield return Steps();
            Assert.IsFalse(door.IsAjar);
            Assert.IsTrue(door.Solid.enabled, "Shut until the switch is stepped on");
            Assert.AreEqual(0f, door.MassInField);

            // No mass: it shuts before the player gets there.
            yield return RunFromSwitch(body, door);
            Assert.IsFalse(door.IsOpen, "Impassable with no mass");
            Assert.GreaterOrEqual(door.Closings, 1, "It shut on its timer");
            Assert.IsTrue(door.Solid.enabled);

            // One boulder short: still shuts first.
            Vector2[] spots = BesideTheDoor(layout);
            var boulders = new List<Boulder>();
            for (int i = 0; i < required - 1; i++) boulders.Add(MakeBoulder(Origin + spots[i]));
            yield return Steps();
            Assert.AreEqual(required - 1, door.MassInField, 1e-4f);
            yield return RunFromSwitch(body, door);
            Assert.IsFalse(door.IsOpen, $"Impassable with {required - 1} boulders");

            // The required boulders: it is still open when the player arrives, and stays open for good.
            boulders.Add(MakeBoulder(Origin + spots[required - 1]));
            yield return Steps();
            Assert.AreEqual(required, door.MassInField, 1e-4f);
            yield return RunFromSwitch(body, door);
            Assert.IsTrue(door.IsOpen, $"Passable with {required} boulders");
            Assert.IsFalse(door.Solid.enabled);
            yield return new WaitForSeconds(door.OpenSecondsNow + 0.3f);
            Assert.IsTrue(door.IsOpen, "Latched: it never shuts on the player again");
            Assert.IsFalse(door.Solid.enabled);
        }

        [UnityTest]
        public IEnumerator TimedDoor_AFasterPlayer_StillNeedsTheRequiredBoulders()
        {
            const int required = 2;
            DoorLayout layout = SlotLikeDoor(required);
            MassMitt mitt = MakePlayer(Origin + layout.SwitchLocal + Vector2.down * 3f, true);
            Rigidbody2D body = mitt.Body;
            var mover = mitt.gameObject.AddComponent<Game.Tracer.PlayerMover>();
            mover.enabled = false; // the test drives the velocity; the mover still reports the effective speed
            mitt.GetComponent<PlayerStats>().Speed = 3; // x1.5
            float speed = mover.EffectiveSpeed;
            Assert.AreEqual(TimeField.BasePlayerSpeed * 1.5f, speed, 1e-3f);
            TimedDoorGate door = MakeDoor(layout);
            Vector2[] spots = BesideTheDoor(layout);
            MakeBoulder(Origin + spots[0]);
            yield return Steps();
            // Unscaled, one boulder would be enough at this speed; scaled, it is not.
            Assert.IsTrue(TimeField.Passable(layout.BaseSeconds, required - 1, layout.WalkSeconds * TimeField.BasePlayerSpeed / speed));

            IEnumerator Run()
            {
                Put(body, door.Switch.transform.position);
                yield return Steps(2);
                Assert.IsTrue(door.IsAjar || door.IsOpen);
                Assert.AreEqual(layout.BaseSeconds * TimeField.BasePlayerSpeed / speed, door.ScaledBaseSeconds, 1e-3f, "Open time scaled to the speed");
                yield return MoveTo(body, door.Threshold, speed, () => door.IsOpen);
                yield return Steps(2);
            }

            yield return Run();
            Assert.IsFalse(door.IsOpen, "Sped up, one boulder short is still impassable");
            MakeBoulder(Origin + spots[1]);
            yield return Steps();
            yield return Run();
            Assert.IsTrue(door.IsOpen, "Sped up, the required boulders still pass");
        }

        [UnityTest]
        public IEnumerator TimedDoor_MoreMass_KeepsItOpenLonger_Monotonic()
        {
            DoorLayout layout = SlotLikeDoor(2);
            TimedDoorGate door = MakeDoor(layout);
            Vector2[] spots = BesideTheDoor(layout);
            float last = 0f;
            for (int m = 0; m <= 3; m++)
            {
                if (m > 0) MakeBoulder(Origin + spots[m - 1]);
                yield return Steps();
                int shut = door.Closings;
                door.Trigger();
                Assert.IsTrue(door.IsAjar);
                float t = 0f;
                while (door.Closings == shut && t < 10f)
                {
                    yield return new WaitForFixedUpdate();
                    t += Time.fixedDeltaTime;
                }
                float expected = TimeField.OpenSeconds(layout.BaseSeconds, m);
                Assert.AreEqual(expected, t, 0.05f, $"{m} boulders: open base x dilation");
                Assert.Greater(t, last, $"{m} boulders keep it open longer than {m - 1}");
                last = t;
            }
        }

        // ---------- Trial ----------

        [UnityTest]
        public IEnumerator Trial_TheSameBouldersMoveFromOneBoothToTheOther_AndCompletionFiresOnce()
        {
            const int required = PhysicsPlan.TrialRequired;
            DoorLayout a = PhysicsPlan.BoothDoor(-3.5f, 1, required).WithSwitch(new Vector2(6.3f, 0.6f));
            DoorLayout b = PhysicsPlan.BoothDoor(3.5f, -1, required).WithSwitch(new Vector2(-6.3f, -0.6f));
            var spec = new PhysicsTrialSpec(default, new[]
            {
                new TrialBoothSpec(PhysicsPlan.BoothFootprint(-3.5f, 1), a),
                new TrialBoothSpec(PhysicsPlan.BoothFootprint(3.5f, -1), b),
            }, new[] { new Vector2(-5f, -3f), new Vector2(5f, 3f) }, required);
            var go = Track(new GameObject("Trial"));
            go.transform.position = Origin;
            var trial = go.AddComponent<PhysicsTrial>();
            trial.Configure(mittItem, spec, Origin, PhysicsPlan.TrialCurrency, null);
            var fired = new List<int>();
            trial.Completed += c => fired.Add(c);
            MassMitt mitt = MakePlayer(Origin + new Vector2(0f, -3.5f), true);
            Rigidbody2D body = mitt.Body;
            yield return Steps();
            Assert.AreEqual(2, trial.Doors.Count);
            Assert.AreEqual(required, trial.Boulders.Count, "Only enough boulders for one door at a time");

            // Booth B with only other (non-trial) boulders beside it: they weigh nothing there, so it shuts first.
            Vector2[] besideB = BesideTheDoor(b);
            var foreign = new List<Boulder>();
            for (int i = 0; i < required; i++) foreign.Add(MakeBoulder(Origin + besideB[i]));
            yield return Steps();
            Assert.AreEqual(0f, trial.Doors[1].MassInField, "Non-trial boulders cannot dilate a booth");
            Assert.AreEqual(required, TimeField.MassNear(trial.Doors[1].FieldCentre), 1e-4f, "They are in its field");
            yield return RunFromSwitch(body, trial.Doors[1]);
            Assert.IsFalse(trial.Doors[1].IsOpen, "Booth B does not pass on other boulders");
            foreach (Boulder f in foreign) Object.Destroy(f.gameObject);
            yield return Steps();

            // The boulders beside booth A: in.
            Vector2[] nearA = BesideTheDoor(a);
            for (int i = 0; i < required; i++) trial.Boulders[i].Teleport(Origin + nearA[i]);
            yield return RunFromSwitch(body, trial.Doors[0]);
            Assert.IsTrue(trial.Doors[0].IsOpen, "Booth A passes with the boulders beside it");
            Assert.AreEqual(1, trial.Progress);
            Assert.IsFalse(trial.IsComplete);
            Assert.AreEqual(0f, trial.Doors[1].MassInField, "Booth A's boulders do nothing for booth B");

            // The same boulders moved over to booth B: in, and the trial completes once.
            Vector2[] nearB = BesideTheDoor(b);
            for (int i = 0; i < required; i++) trial.Boulders[i].Teleport(Origin + nearB[i]);
            yield return RunFromSwitch(body, trial.Doors[1]);
            Assert.IsTrue(trial.Doors[1].IsOpen, $"Booth B passes with the same boulders (mass {trial.Doors[1].MassInField}, openings {trial.Doors[1].Openings}, closings {trial.Doors[1].Closings}, player {body.position - Origin}, threshold {trial.Doors[1].Threshold - Origin}, boulders {string.Join(" ", trial.Boulders.Select(x => (x.Position - Origin).ToString()))})");
            Assert.IsTrue(trial.IsComplete);
            CollectionAssert.AreEqual(new[] { PhysicsPlan.TrialCurrency }, fired, "Completed fires once with the currency");

            // Repeats do nothing.
            yield return RunFromSwitch(body, trial.Doors[0]);
            Assert.IsFalse(trial.Complete());
            Assert.AreEqual(1, fired.Count, "Repeats do not fire");
        }

        // ---------- Enemies ----------

        [UnityTest]
        public IEnumerator Enemies_TakeMarkedlyMoreDamageFromTheirWeakness()
        {
            var isotope = Track(ChemistryIsotope.CreateItem());
            foreach (PhysicsEnemyKind kind in new[] { PhysicsEnemyKind.QuantumFlea, PhysicsEnemyKind.StaticCling })
            {
                ItemDefinition weakness = kind == PhysicsEnemyKind.QuantumFlea ? mittItem : isotope;
                ItemDefinition notIt = kind == PhysicsEnemyKind.QuantumFlea ? isotope : mittItem;
                var bounds = new Rect(Origin + new Vector2(-2f, -2f), new Vector2(4f, 4f));
                Enemy weak = PhysicsEnemies.Spawn(kind, weakness, Origin, bounds, null, null, null);
                Track(weak.gameObject);
                Enemy plain = PhysicsEnemies.Spawn(kind, weakness, Origin + new Vector2(1.5f, 0f), bounds, null, null, null);
                Track(plain.gameObject);
                Assert.AreSame(weakness, weak.Weakness);
                Assert.AreEqual(kind, weak.GetComponent<PhysicsEnemy>().Kind);
                StringAssert.Contains(PhysicsEnemies.Name(kind), weak.name);
                yield return null;

                float byWeakness = weak.Health.TakeDamage(1f, weakness);
                float byOther = plain.Health.TakeDamage(1f, notIt);
                Assert.Greater(byWeakness, byOther * 2.5f, $"{kind}: the weakness hits markedly harder");
            }
        }
    }
}
