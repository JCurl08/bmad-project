using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The Chemistry mechanics on objects built in the test (far from anything a scene may hold), one test per
    /// I/O row: the dispenser hands out a fresh glowing isotope only to a player holding none; the isotope decays
    /// glow, unstable, lead on its half-life timer and lead stays lead; a dark-room gate opens only to a glowing
    /// touch, a cracked wall only to a swing with the unstable isotope equipped, a lead plate only to lead (which
    /// it uses up); after that the dispenser refills; the trial needs one isotope through three stages and fires
    /// once; Chemistry enemies take markedly more damage from their weakness.
    /// </summary>
    public class ChemistryComponentTests : InputTestFixture
    {
        private static readonly Vector2 Origin = new Vector2(-9000f, -7000f);

        private readonly List<Object> created = new List<Object>();
        private ItemDefinition isotopeItem, other;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            isotopeItem = Track(ChemistryIsotope.CreateItem());
            other = Track(ItemDefinition.Create("other", "Other", Theme.Physics, Color.cyan));
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

        /// <summary>A player like the Cube scene's (dynamic body, so triggers fire), facing down, with an Isotope.</summary>
        private Isotope MakePlayer(Vector2 position)
        {
            var go = Track(new GameObject("Test Player"));
            go.transform.position = position;
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            go.AddComponent<CircleCollider2D>().radius = 0.4f;
            go.AddComponent<Game.Tracer.PlayerMover>();
            go.AddComponent<Inventory>();
            go.AddComponent<Health>();
            go.AddComponent<PlayerStats>();
            go.AddComponent<Equipment>();
            go.AddComponent<PlayerAttack>().Cooldown = 0f;
            var isotope = go.AddComponent<Isotope>();
            isotope.Item = isotopeItem;
            isotope.ShowHud = false;
            return isotope;
        }

        /// <summary>A gate block as CubeWorld.CreateBlock makes it: solid root collider, "Visual" child sprite.</summary>
        private GameObject Block(Vector2 at)
        {
            var go = Track(new GameObject("Block"));
            go.transform.position = at;
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.AddComponent<SpriteRenderer>().sprite = NpcFactory.ShapeSprite(PartShape.Square);
            go.AddComponent<BoxCollider2D>().size = new Vector2(GateSlot.GateWidth, GateSlot.GateThickness);
            return go;
        }

        /// <summary>Hands the player a fresh isotope (as a dispenser would) and ages it into a stage.</summary>
        private static void Give(Isotope isotope, IsotopeStage stage)
        {
            if (!isotope.IsHeld) isotope.Inventory.Add(isotope.Item);
            isotope.Age = ChemistryIsotope.StageStart(stage, isotope.HalfLife) + 0.1f;
            Assert.AreEqual(stage, isotope.Stage);
        }

        private static int Swing(Isotope isotope, ItemDefinition item)
        {
            var attack = isotope.GetComponent<PlayerAttack>();
            if (item != null && !isotope.Inventory.Has(item)) isotope.Inventory.Add(item);
            attack.GetComponent<Equipment>().Equip(item);
            attack.TryAttack();
            return attack.LastReceiversHit;
        }

        private static IEnumerator Physics(int steps = 3)
        {
            for (int i = 0; i < steps; i++) yield return new WaitForFixedUpdate();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Dispense_GivesAFreshGlowingIsotope_OnlyToAPlayerHoldingNone()
        {
            Isotope isotope = MakePlayer(Origin + new Vector2(0f, 3f));
            IsotopeDispenser dispenser = Track(IsotopeDispenser.Create(null, Origin, isotopeItem, null)).GetComponent<IsotopeDispenser>();
            Track(dispenser.gameObject);
            yield return Physics();
            Assert.IsFalse(isotope.IsHeld);
            Assert.AreEqual(IsotopeStage.None, isotope.Stage);

            var body = isotope.GetComponent<Rigidbody2D>();
            body.position = Origin;
            yield return Physics();
            Assert.IsTrue(isotope.IsHeld, "Touching the dispenser hands out an isotope");
            Assert.AreEqual(IsotopeStage.Glow, isotope.Stage, "A fresh isotope glows");
            Assert.AreEqual(1, dispenser.Dispensed);
            Assert.AreEqual(1, isotope.Serial);
            Assert.AreSame(isotopeItem, isotope.GetComponent<Equipment>().Equipped, "The first item is equipped");
            Assert.IsNotNull(dispenser, "The dispenser stays");
            Assert.IsFalse(dispenser.Collected);

            // Holding one already (any stage): nothing happens.
            isotope.Age = 13f;
            body.position = Origin + new Vector2(0f, 3f);
            yield return Physics();
            body.position = Origin;
            yield return Physics();
            Assert.AreEqual(1, dispenser.Dispensed, "No second isotope while holding one");
            Assert.AreEqual(1, isotope.Serial);
            Assert.AreEqual(IsotopeStage.Unstable, isotope.Stage, "The held isotope is not refreshed");
        }

        [UnityTest]
        public IEnumerator Decay_GlowThenUnstableThenLead_OverTime_AndLeadStaysLead()
        {
            Isotope isotope = MakePlayer(Origin);
            isotope.HalfLife = 0.5f; // glow 0-0.5 s, unstable 0.5-0.83 s, then lead
            var seen = new List<IsotopeStage>();
            isotope.StageChanged += (_, s) => seen.Add(s);
            isotope.Inventory.Add(isotopeItem);
            Assert.AreEqual(IsotopeStage.Glow, isotope.Stage);
            StringAssert.Contains("Glowing", isotope.HudText);

            float start = Time.time;
            while (isotope.Stage == IsotopeStage.Glow && Time.time - start < 3f) yield return null;
            Assert.AreEqual(IsotopeStage.Unstable, isotope.Stage);
            Assert.GreaterOrEqual(Time.time - start, 0.45f, "Glow lasts the half-life");
            StringAssert.Contains("Unstable", isotope.HudText);
            while (isotope.Stage == IsotopeStage.Unstable && Time.time - start < 3f) yield return null;
            Assert.AreEqual(IsotopeStage.Lead, isotope.Stage);
            Assert.GreaterOrEqual(Time.time - start, 0.78f, "Unstable lasts two thirds of the half-life more");

            yield return new WaitForSeconds(1f);
            Assert.AreEqual(IsotopeStage.Lead, isotope.Stage, "Lead stays lead");
            Assert.IsTrue(isotope.IsHeld, "Lead stays until a plate takes it");
            StringAssert.Contains("Lead", isotope.HudText);
            CollectionAssert.AreEqual(new[] { IsotopeStage.Glow, IsotopeStage.Unstable, IsotopeStage.Lead }, seen);
        }

        [UnityTest]
        public IEnumerator GlowGate_OpensToAGlowingTouch_WrongStageStaysShut()
        {
            Isotope isotope = MakePlayer(Origin);
            Collider2D toucher = isotope.GetComponent<Collider2D>();
            DarkRoomGate gate = DarkRoomGate.Build(Block(Origin + new Vector2(20f, 0f)), isotopeItem, null);
            yield return null;

            Assert.IsFalse(gate.TryOpen(toucher), "No isotope: shut");
            isotope.Inventory.Add(other);
            Assert.IsFalse(gate.TryOpen(toucher), "Another item: shut");
            foreach (IsotopeStage wrong in new[] { IsotopeStage.Unstable, IsotopeStage.Lead })
            {
                Give(isotope, wrong);
                Assert.IsFalse(gate.TryOpen(toucher), $"{wrong}: shut");
                Assert.IsTrue(gate.Solid.enabled);
            }
            isotope.Inventory.Remove(isotopeItem);
            Give(isotope, IsotopeStage.Glow);
            Assert.IsTrue(gate.TryOpen(toucher), "A glowing touch lights the dark room");
            Assert.IsTrue(gate.IsOpen);
            Assert.IsFalse(gate.Solid.enabled);
            Assert.IsTrue(isotope.IsHeld, "Glow is not used up");
        }

        [UnityTest]
        public IEnumerator UnstableGate_BlastsOpenToASwingWithTheUnstableIsotopeEquipped_Only()
        {
            Isotope isotope = MakePlayer(Origin);
            CrackedWallGate gate = CrackedWallGate.Build(Block(Origin + Vector2.down * 1.1f), isotopeItem, null);
            int opened = 0;
            gate.Opened += _ => opened++;
            yield return null;

            Give(isotope, IsotopeStage.Glow);
            Assert.AreEqual(0, Swing(isotope, isotopeItem), "Glowing: no blast");
            Give(isotope, IsotopeStage.Lead);
            Assert.AreEqual(0, Swing(isotope, isotopeItem), "Lead: no blast");
            isotope.Inventory.Remove(isotopeItem);
            Give(isotope, IsotopeStage.Unstable);
            Assert.AreEqual(0, Swing(isotope, null), "Unstable held but a bare swing: no blast");
            Assert.AreEqual(0, Swing(isotope, other), "Another item: no blast");
            Assert.IsFalse(gate.TryOpen(isotope.GetComponent<Collider2D>()), "Touching never blasts it");
            Assert.IsFalse(gate.IsOpen);

            Assert.AreEqual(1, Swing(isotope, isotopeItem));
            Assert.IsTrue(gate.IsOpen, "The unstable isotope blasts the cracked wall");
            Assert.IsFalse(gate.Solid.enabled);
            Assert.IsNotNull(gate.Scorch);
            Assert.AreEqual(1, opened);
        }

        [UnityTest]
        public IEnumerator LeadPlate_DroppedOrUsed_OpensItsDoor_AndUsesUpTheLead_WrongStageDoesNothing()
        {
            Isotope isotope = MakePlayer(Origin + new Vector2(0f, 3f));
            var body = isotope.GetComponent<Rigidbody2D>();
            LeadPlateGate dropped = LeadPlateGate.Build(Block(Origin + new Vector2(20f, 0f)), isotopeItem, Origin, null, null);
            Track(dropped.Plate.gameObject);
            yield return Physics();

            // Wrong stages: standing on the plate does nothing.
            foreach (IsotopeStage wrong in new[] { IsotopeStage.Glow, IsotopeStage.Unstable })
            {
                Give(isotope, wrong);
                body.position = Origin;
                yield return Physics();
                Assert.IsFalse(dropped.IsOpen, $"{wrong} on the plate: no effect");
                Assert.IsTrue(isotope.IsHeld, $"{wrong} is not taken");
                body.position = Origin + new Vector2(0f, 3f);
                yield return Physics();
            }

            // Lead dropped on it (stepping on): the door opens and the lead is used up.
            Give(isotope, IsotopeStage.Lead);
            body.position = Origin;
            yield return Physics();
            Assert.IsTrue(dropped.IsOpen, "Lead holds the plate down: the door opens");
            Assert.IsFalse(isotope.IsHeld, "The lead is consumed");
            Assert.AreEqual(IsotopeStage.None, isotope.Stage);
            Assert.AreEqual(1, dropped.Plate.LeadTaken);

            // Lead used on a plate (a swing with lead equipped) works too; an open door's plate takes no more lead.
            body.position = Origin + new Vector2(0f, 3f);
            yield return Physics();
            LeadPlateGate used = LeadPlateGate.Build(Block(Origin + new Vector2(30f, 0f)), isotopeItem,
                Origin + new Vector2(0f, 3f) + Vector2.down * 1.1f, null, null);
            Track(used.Plate.gameObject);
            Give(isotope, IsotopeStage.Lead);
            Assert.IsFalse(dropped.Plate.TryDrop(isotope), "An open door's plate wants no more lead");
            Assert.AreEqual(1, Swing(isotope, isotopeItem));
            Assert.IsTrue(used.IsOpen, "Using lead on a plate opens its door");
            Assert.IsFalse(isotope.IsHeld);
        }

        [UnityTest]
        public IEnumerator ReObtain_AfterTheLeadIsUsedUp_TheDispenserGivesAFreshGlowingIsotope()
        {
            Isotope isotope = MakePlayer(Origin);
            IsotopeDispenser dispenser = IsotopeDispenser.Create(null, Origin, isotopeItem, null);
            Track(dispenser.gameObject);
            var body = isotope.GetComponent<Rigidbody2D>();
            yield return Physics();
            Assert.AreEqual(1, dispenser.Dispensed);

            isotope.Age = 25f;
            Assert.AreEqual(IsotopeStage.Lead, isotope.Stage);
            body.position = Origin + new Vector2(0f, 3f);
            yield return Physics();
            Assert.IsTrue(isotope.ConsumeLead(), "A plate takes the lead");
            Assert.IsFalse(isotope.IsHeld);

            body.position = Origin;
            yield return Physics();
            Assert.IsTrue(isotope.IsHeld, "Back at the dispenser: a new isotope");
            Assert.AreEqual(IsotopeStage.Glow, isotope.Stage, "Fresh, at the glow stage");
            Assert.Less(isotope.Age, 1f);
            Assert.AreEqual(2, dispenser.Dispensed);
            Assert.AreEqual(2, isotope.Serial);
            Assert.IsFalse(isotope.ConsumeLead(), "Only lead can be used up");
        }

        [UnityTest]
        public IEnumerator Trial_OneIsotopeThroughThreeStages_InOrder_FiresOnce()
        {
            Isotope isotope = MakePlayer(Origin + new Vector2(0f, 10f));
            var go = Track(new GameObject("Chemistry Trial"));
            go.transform.position = Origin;
            var trial = go.AddComponent<ChemistryTrial>();
            trial.Configure(isotopeItem, new[] { Origin, Origin + new Vector2(5f, 0f), Origin + new Vector2(10f, 0f) }, 25, null);
            var fired = new List<int>();
            trial.Completed += c => fired.Add(c);
            int resets = 0;
            trial.ResetPuzzle += _ => resets++;
            yield return null;

            // Wrong stages and wrong order do nothing.
            Give(isotope, IsotopeStage.Glow);
            Assert.IsFalse(trial.OnPiece(trial.Wall, isotope, true), "The wall wants the unstable stage");
            Give(isotope, IsotopeStage.Unstable);
            Assert.IsFalse(trial.OnPiece(trial.Lamp, isotope, false), "The lamp wants the glow");
            Assert.IsFalse(trial.OnPiece(trial.Wall, isotope, true), "The wall comes after the lamp");
            Assert.AreEqual(0, trial.Progress);

            // A first isotope lights the lamp, then a second one (a new dispense) restarts the puzzle.
            isotope.Inventory.Remove(isotopeItem);
            Give(isotope, IsotopeStage.Glow);
            Assert.IsTrue(trial.OnPiece(trial.Lamp, isotope, false));
            Assert.AreEqual(1, trial.Progress);
            isotope.Inventory.Remove(isotopeItem);
            Give(isotope, IsotopeStage.Unstable);
            Assert.IsTrue(trial.OnPiece(trial.Wall, isotope, true), "A different isotope resets");
            Assert.AreEqual(0, trial.Progress);
            Assert.AreEqual(1, resets);

            // One isotope: glow lights the lamp, unstable blasts the wall, its lead weighs the plate.
            isotope.Inventory.Remove(isotopeItem);
            Give(isotope, IsotopeStage.Glow);
            Assert.IsTrue(trial.OnPiece(trial.Lamp, isotope, false), "Glow lights the lamp");
            Assert.IsTrue(trial.Lamp.Done);
            Give(isotope, IsotopeStage.Unstable);
            Assert.IsFalse(trial.OnPiece(trial.Wall, isotope, false), "The wall needs a swing");
            Assert.IsTrue(trial.OnPiece(trial.Wall, isotope, true), "Unstable blasts the wall");
            Assert.IsTrue(trial.Wall.Done);
            Assert.IsFalse(trial.Wall.GetComponent<Collider2D>().enabled, "The wall is gone");
            Assert.IsEmpty(fired);
            Give(isotope, IsotopeStage.Lead);
            Assert.IsTrue(trial.Plate.TryDrop(isotope), "Lead weighs the plate");
            Assert.IsTrue(trial.IsComplete);
            CollectionAssert.AreEqual(new[] { 25 }, fired, "Completion fires once with its currency");

            // Repeats don't fire.
            Give(isotope, IsotopeStage.Glow);
            Assert.IsFalse(trial.OnPiece(trial.Lamp, isotope, false));
            Give(isotope, IsotopeStage.Lead);
            Assert.IsTrue(trial.Plate.TryDrop(isotope), "The trial plate still takes spent lead");
            Assert.IsFalse(trial.Complete());
            CollectionAssert.AreEqual(new[] { 25 }, fired);
        }

        /// <summary>A trial with lamp at Origin, wall 5 right, plate 10 right.</summary>
        private ChemistryTrial MakeTrial()
        {
            var go = Track(new GameObject("Chemistry Trial"));
            go.transform.position = Origin;
            var trial = go.AddComponent<ChemistryTrial>();
            trial.Configure(isotopeItem, new[] { Origin, Origin + new Vector2(5f, 0f), Origin + new Vector2(10f, 0f) }, 25, null);
            return trial;
        }

        [UnityTest]
        public IEnumerator Trial_AFreshIsotopesGlowRelightsTheLamp_ThroughTouchAndSwing_ThenCompletes()
        {
            Isotope isotope = MakePlayer(Origin + new Vector2(0f, 3f));
            var body = isotope.GetComponent<Rigidbody2D>();
            ChemistryTrial trial = MakeTrial();
            var fired = new List<int>();
            trial.Completed += c => fired.Add(c);
            yield return Physics();

            // The first isotope lights the lamp by touch.
            Give(isotope, IsotopeStage.Glow);
            int first = isotope.Serial;
            body.position = Origin;
            yield return Physics();
            Assert.IsTrue(trial.Lamp.Done, "A glowing touch lights the lamp");
            Assert.AreEqual(first, trial.IsotopeSerial);

            // A fresh isotope (a new dispense) glowing on the lamp relights it for the new isotope.
            body.position = Origin + new Vector2(0f, 3f);
            yield return Physics();
            isotope.Inventory.Remove(isotopeItem);
            Give(isotope, IsotopeStage.Glow);
            int second = isotope.Serial;
            Assert.AreNotEqual(first, second);
            body.position = Origin;
            yield return Physics();
            Assert.IsTrue(trial.Lamp.Done, "The lamp is lit again");
            Assert.AreEqual(second, trial.IsotopeSerial, "...for the new isotope");
            Assert.AreEqual(1, trial.Progress);

            // The same isotope, unstable: a real swing blasts the wall (player above it, facing down).
            Give(isotope, IsotopeStage.Unstable);
            body.position = Origin + new Vector2(5f, 0.9f);
            yield return Physics();
            Assert.GreaterOrEqual(Swing(isotope, isotopeItem), 0);
            Assert.IsTrue(trial.Wall.Done, "The unstable isotope's swing blasts the wall");
            Assert.AreEqual(2, trial.Progress);

            // Its lead, dropped by stepping on the plate, completes the trial.
            Give(isotope, IsotopeStage.Lead);
            body.position = Origin + new Vector2(10f, 0f);
            yield return Physics();
            Assert.IsTrue(trial.IsComplete);
            CollectionAssert.AreEqual(new[] { 25 }, fired);
        }

        [UnityTest]
        public IEnumerator Trial_RestartsWhenTheTrackedIsotopeIsUsedUpEarly()
        {
            Isotope isotope = MakePlayer(Origin + new Vector2(0f, 3f));
            var body = isotope.GetComponent<Rigidbody2D>();
            ChemistryTrial trial = MakeTrial();
            int resets = 0;
            trial.ResetPuzzle += _ => resets++;
            yield return Physics();

            Give(isotope, IsotopeStage.Glow);
            body.position = Origin;
            yield return Physics();
            Assert.AreEqual(1, trial.Progress);

            // Its lead goes on the trial plate before the wall was blasted: the puzzle restarts.
            body.position = Origin + new Vector2(0f, 3f);
            yield return Physics();
            Give(isotope, IsotopeStage.Lead);
            body.position = Origin + new Vector2(10f, 0f);
            yield return Physics();
            Assert.IsFalse(isotope.IsHeld, "The plate took the lead");
            Assert.AreEqual(0, trial.Progress, "Progress does not hang on a used-up isotope");
            Assert.IsFalse(trial.Lamp.Done, "The lamp goes dark");
            Assert.AreEqual(1, resets);

            // Lit again, then the isotope is used up elsewhere (another plate): the trial notices and restarts.
            body.position = Origin + new Vector2(0f, 3f);
            yield return Physics();
            Give(isotope, IsotopeStage.Glow);
            body.position = Origin;
            yield return Physics();
            Assert.AreEqual(1, trial.Progress);
            isotope.Age = 25f;
            Assert.IsTrue(isotope.ConsumeLead());
            body.position = Origin + new Vector2(0f, 3f);
            yield return Physics();
            Assert.AreEqual(0, trial.Progress);
            Assert.IsFalse(trial.Lamp.Done);
        }

        [UnityTest]
        public IEnumerator Dispenser_TakesSpentLeadBack_AndHandsOutAFreshIsotope()
        {
            Isotope isotope = MakePlayer(Origin + new Vector2(0f, 3f));
            IsotopeDispenser dispenser = IsotopeDispenser.Create(null, Origin, isotopeItem, null);
            Track(dispenser.gameObject);
            var body = isotope.GetComponent<Rigidbody2D>();
            yield return Physics();
            Give(isotope, IsotopeStage.Lead);
            int before = isotope.Serial;

            body.position = Origin;
            yield return Physics();
            Assert.IsTrue(isotope.IsHeld);
            Assert.AreEqual(IsotopeStage.Glow, isotope.Stage, "Lead swapped for a fresh glowing isotope");
            Assert.AreEqual(before + 1, isotope.Serial);
            Assert.AreEqual(1, dispenser.LeadTakenBack);
            Assert.AreEqual(1, dispenser.Dispensed);
        }

        [UnityTest]
        public IEnumerator Enemies_TakeMarkedlyMoreDamageFromTheirWeakness()
        {
            var beak = Track(BiologyBeaks.CreateItem(BeakKind.Thick));
            foreach (ChemistryEnemyKind kind in new[] { ChemistryEnemyKind.FreeRadical, ChemistryEnemyKind.RustMite })
            {
                ItemDefinition weakness = kind == ChemistryEnemyKind.FreeRadical ? isotopeItem : beak;
                Enemy weak = ChemistryEnemies.Spawn(kind, weakness, Origin + new Vector2(50f, (int)kind * 5f), new Rect(Origin, Vector2.one * 100f),
                    null, null, null);
                Enemy plain = ChemistryEnemies.Spawn(kind, weakness, Origin + new Vector2(60f, (int)kind * 5f), new Rect(Origin, Vector2.one * 100f),
                    null, null, null);
                Track(weak.gameObject);
                Track(plain.gameObject);
                weak.DestroyOnDeath = false;
                plain.DestroyOnDeath = false;
                Assert.AreEqual(kind, weak.GetComponent<ChemistryEnemy>().Kind);
                Assert.AreSame(weakness, weak.Weakness);
                StringAssert.Contains(ChemistryEnemies.Name(kind), weak.name);
                yield return null;

                float byWeakness = weak.Health.TakeDamage(1f, weakness);
                float byOther = plain.Health.TakeDamage(1f, other);
                Assert.Greater(byWeakness, byOther * 2.5f, $"{kind}: the weakness hits markedly harder");
            }
        }
    }
}
