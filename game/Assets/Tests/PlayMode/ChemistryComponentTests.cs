using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The Chemistry mechanics on objects built in the test (far from anything a scene may hold), one test per I/O row:
    /// a stone is a normal pickup (added once, equipped, a popup; nothing respawns); the Glowstone* lights a dark-room
    /// gate, the Wildstone* blasts a cracked wall and the Leadstone* weighs a plate down (its door opens and the stone is
    /// kept), each only when swung with that stone equipped (other stones, other items, bare swings, touches and steps do
    /// nothing); the difficulty sets the swings; a held stone never changes (60 s of game time); the trial for each stone
    /// needs its three targets in order and fires once; Chemistry enemies take markedly more damage from their weakness.
    /// </summary>
    public class ChemistryComponentTests : InputTestFixture
    {
        private static readonly Vector2 Origin = new Vector2(-9000f, -7000f);

        private readonly List<Object> created = new List<Object>();
        private ItemDefinition glow, wild, lead, other;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            glow = Track(ChemistryIsotope.CreateItem(IsotopeStage.Glow));
            wild = Track(ChemistryIsotope.CreateItem(IsotopeStage.Unstable));
            lead = Track(ChemistryIsotope.CreateItem(IsotopeStage.Lead));
            other = Track(ItemDefinition.Create("other", "Other", Theme.Physics, Color.cyan));
        }

        public override void TearDown()
        {
            foreach (Object o in created)
                if (o != null) Object.Destroy(o);
            created.Clear();
            Time.timeScale = 1f;
            base.TearDown();
        }

        private T Track<T>(T o) where T : Object
        {
            created.Add(o);
            return o;
        }

        private ItemDefinition Stone(IsotopeStage stage) =>
            stage == IsotopeStage.Glow ? glow : stage == IsotopeStage.Unstable ? wild : lead;

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
            go.AddComponent<PickupFeedback>();
            var isotope = go.AddComponent<Isotope>();
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

        /// <summary>A plain one-shot pickup of an item, as CubeWorld builds every item's pickup (stones included).</summary>
        private ItemPickup Pickup(Vector2 at, ItemDefinition item)
        {
            var go = Track(new GameObject($"Pickup ({item})"));
            go.transform.position = at;
            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = ItemPickup.Radius;
            var pickup = go.AddComponent<ItemPickup>();
            pickup.Item = item;
            return pickup;
        }

        /// <summary>Equips item (adding it when missing; null for bare hands) and swings once. Returns the receivers that reacted.</summary>
        private static int Swing(Isotope isotope, ItemDefinition item)
        {
            var attack = isotope.GetComponent<PlayerAttack>();
            if (item != null && !isotope.Inventory.Has(item)) isotope.Inventory.Add(item);
            attack.GetComponent<Equipment>().Equip(item);
            attack.TryAttack();
            return attack.LastReceiversHit;
        }

        private IsotopeGate BuildGate(IsotopeStage stage, ItemDefinition item, Vector2 at, int difficulty = 1)
        {
            GameObject block = Block(at);
            switch (stage)
            {
                case IsotopeStage.Glow: return DarkRoomGate.Build(block, item, difficulty, false, null);
                case IsotopeStage.Unstable: return CrackedWallGate.Build(block, item, difficulty, false, null);
                default:
                    LeadPlateGate gate = LeadPlateGate.Build(block, item, at, null, difficulty, false, null);
                    Track(gate.Plate.gameObject);
                    return gate;
            }
        }

        [UnityTest]
        public IEnumerator Pickup_AddsTheStoneOnce_EquipsIt_ShowsThePopup_AndNothingRespawns()
        {
            Isotope isotope = MakePlayer(Origin + new Vector2(0f, 3f));
            var body = isotope.GetComponent<Rigidbody2D>();
            ItemPickup pickup = Pickup(Origin, wild);
            int added = 0;
            isotope.Inventory.ItemAdded += _ => added++;
            yield return TestInput.PhysicsSteps();
            Assert.IsFalse(isotope.IsHeld);
            Assert.AreEqual(IsotopeStage.None, isotope.Stage);

            body.position = Origin;
            yield return TestInput.PhysicsSteps();
            Assert.IsTrue(isotope.IsHeld, "Touching the stone picks it up");
            Assert.AreSame(wild, isotope.Item);
            Assert.AreEqual(IsotopeStage.Unstable, isotope.Stage);
            Assert.AreSame(wild, isotope.GetComponent<Equipment>().Equipped, "The first item is equipped");
            Assert.IsTrue(isotope.IsEquipped);
            Assert.IsTrue(PickupPopup.Active.Any(p => p != null && p.Text == "+Wildstone*"), "The pickup popup shows");
            Assert.IsTrue(pickup == null, "A one-shot pickup: it is gone, nothing respawns");
            Assert.AreEqual(1, added);

            // Already held: a second pickup of the same stone does nothing.
            body.position = Origin + new Vector2(0f, 3f);
            yield return TestInput.PhysicsSteps();
            ItemPickup again = Pickup(Origin, wild);
            body.position = Origin;
            yield return TestInput.PhysicsSteps();
            Assert.AreEqual(1, added, "Added once");
            Assert.AreEqual(1, isotope.Inventory.Items.Count);
            Assert.IsFalse(again.Collected);
        }

        [UnityTest]
        public IEnumerator Pickup_WhileHoldingABeak_EquipsTheStone()
        {
            Isotope isotope = MakePlayer(Origin + new Vector2(0f, 3f));
            var body = isotope.GetComponent<Rigidbody2D>();
            ItemDefinition beak = Track(BiologyBeaks.CreateItem(BeakKind.Thick));
            isotope.Inventory.Add(beak);
            var equipment = isotope.GetComponent<Equipment>();
            Assert.AreSame(beak, equipment.Equipped, "Holding the beak");
            Pickup(Origin, lead);
            yield return TestInput.PhysicsSteps();

            body.position = Origin;
            yield return TestInput.PhysicsSteps();
            Assert.IsTrue(isotope.Inventory.Has(lead));
            Assert.AreSame(lead, equipment.Equipped, "The new stone is equipped, ready to use");
            Assert.IsTrue(isotope.IsEquipped);
            Assert.IsTrue(isotope.Inventory.Has(beak), "The beak is still owned");
        }

        [UnityTest]
        public IEnumerator Glowing_LightsADarkRoomGate_WhenSwungAtIt_OtherItemsOrABareSwingDoNothing()
        {
            Isotope isotope = MakePlayer(Origin);
            IsotopeGate gate = BuildGate(IsotopeStage.Glow, glow, Origin + Vector2.down * 1.1f);
            int opened = 0;
            gate.Opened += _ => opened++;
            yield return null;

            Assert.IsFalse(gate.TryOpen(isotope.GetComponent<Collider2D>()), "No stone: shut");
            isotope.Inventory.Add(glow);
            Assert.IsFalse(gate.TryOpen(isotope.GetComponent<Collider2D>()), "Touching it, even holding the Glowstone*, does nothing");
            Assert.AreEqual(0, Swing(isotope, null), "A bare swing: shut");
            Assert.AreEqual(0, Swing(isotope, other), "Another item: shut");
            Assert.AreEqual(0, Swing(isotope, wild), "Another stone: shut");
            Assert.AreEqual(0, Swing(isotope, lead), "Another stone: shut");
            Assert.IsFalse(gate.IsOpen);
            Assert.IsTrue(gate.Solid.enabled);

            Assert.AreEqual(1, Swing(isotope, glow));
            Assert.IsTrue(gate.IsOpen, "The Glowstone* lights the dark room");
            Assert.IsFalse(gate.Solid.enabled);
            Assert.AreEqual(1, opened);
            Assert.IsTrue(isotope.Inventory.Has(glow), "The stone is kept");
        }

        [UnityTest]
        public IEnumerator Unstable_BlastsACrackedWall_WhenSwungAtIt_OtherItemsDoNothing()
        {
            Isotope isotope = MakePlayer(Origin);
            var gate = (CrackedWallGate)BuildGate(IsotopeStage.Unstable, wild, Origin + Vector2.down * 1.1f);
            yield return null;

            isotope.Inventory.Add(wild);
            Assert.IsFalse(gate.TryOpen(isotope.GetComponent<Collider2D>()), "Touching never blasts it");
            Assert.AreEqual(0, Swing(isotope, null), "A bare swing: no blast");
            Assert.AreEqual(0, Swing(isotope, other), "Another item: no blast");
            Assert.AreEqual(0, Swing(isotope, glow), "Another stone: no blast");
            Assert.AreEqual(0, Swing(isotope, lead), "Another stone: no blast");
            Assert.IsFalse(gate.IsOpen);

            Assert.AreEqual(1, Swing(isotope, wild));
            Assert.IsTrue(gate.IsOpen, "The Wildstone* blasts the cracked wall");
            Assert.IsFalse(gate.Solid.enabled);
            Assert.IsNotNull(gate.Scorch);
            Assert.IsTrue(isotope.Inventory.Has(wild), "The stone is kept");
        }

        [UnityTest]
        public IEnumerator Lead_WeighsAPlateDown_WhenSwungAtIt_ItsDoorOpens_AndTheStoneIsKept()
        {
            Isotope isotope = MakePlayer(Origin + new Vector2(0f, 3f));
            var body = isotope.GetComponent<Rigidbody2D>();
            var gate = (LeadPlateGate)BuildGate(IsotopeStage.Lead, lead, Origin + new Vector2(20f, 0f));
            gate.Plate.transform.position = Origin + Vector2.down * 1.1f;
            yield return TestInput.PhysicsSteps();

            // Stepping onto the plate holding the Leadstone* does nothing: it is used by swinging.
            isotope.Inventory.Add(lead);
            body.position = Origin + Vector2.down * 1.1f;
            yield return TestInput.PhysicsSteps();
            Assert.IsFalse(gate.IsOpen, "Stepping on the plate does nothing");
            body.position = Origin;
            yield return TestInput.PhysicsSteps();

            Assert.AreEqual(0, Swing(isotope, null), "A bare swing: nothing");
            Assert.AreEqual(0, Swing(isotope, other), "Another item: nothing");
            Assert.AreEqual(0, Swing(isotope, glow), "Another stone: nothing");
            Assert.AreEqual(0, Swing(isotope, wild), "Another stone: nothing");
            Assert.IsFalse(gate.IsOpen);

            Assert.AreEqual(1, Swing(isotope, lead));
            Assert.IsTrue(gate.IsOpen, "The Leadstone* weighs the plate down: the door opens");
            Assert.IsFalse(gate.Solid.enabled);
            Assert.IsTrue(gate.Plate.Pressed);
            Assert.AreEqual(1, gate.Plate.Presses);
            Assert.IsTrue(isotope.Inventory.Has(lead), "The stone is not used up");
            Assert.AreEqual(IsotopeStage.Lead, isotope.Stage);
            Assert.AreEqual(0, Swing(isotope, lead), "An open door's plate wants nothing more");
        }

        [UnityTest]
        public IEnumerator Difficulty_TheStageGatesOnTheFace_SetsTheSwingsAGateTakes()
        {
            Isotope isotope = MakePlayer(Origin);
            // 8 stage gates on a 3x3 face: two swings (one per 7 gates beyond the first).
            IsotopeGate gate = BuildGate(IsotopeStage.Unstable, wild, Origin + Vector2.down * 1.1f, 8);
            var struck = new List<int>();
            gate.Struck += (_, n) => struck.Add(n);
            yield return null;
            Assert.AreEqual(8, gate.Difficulty);
            Assert.AreEqual(2, gate.HitsToOpen);

            Assert.AreEqual(1, Swing(isotope, wild));
            Assert.IsFalse(gate.IsOpen, "One swing is not enough at difficulty 8");
            Assert.AreEqual(0, Swing(isotope, glow), "Another stone does not count");
            Assert.AreEqual(1, gate.Hits);
            Assert.AreEqual(1, Swing(isotope, wild));
            Assert.IsTrue(gate.IsOpen);
            CollectionAssert.AreEqual(new[] { 1, 2 }, struck);
        }

        [UnityTest]
        public IEnumerator NoTimer_AHeldStoneNeverChanges_Over60SecondsOfGameTime()
        {
            Isotope isotope = MakePlayer(Origin);
            var seen = new List<IsotopeStage>();
            isotope.StageChanged += (_, s) => seen.Add(s);
            isotope.Inventory.Add(glow);
            Assert.AreEqual(IsotopeStage.Glow, isotope.Stage);
            string label = isotope.StageLabel;
            string hud = isotope.HudText;

            Time.timeScale = 30f;
            float start = Time.time;
            yield return new WaitForSeconds(60f);
            Time.timeScale = 1f;
            Assert.GreaterOrEqual(Time.time - start, 60f, "60 s of game time passed");
            Assert.AreEqual(IsotopeStage.Glow, isotope.Stage, "The stone never changes");
            Assert.AreSame(glow, isotope.Item);
            Assert.AreEqual(label, isotope.StageLabel);
            Assert.AreEqual(hud, isotope.HudText);
            CollectionAssert.AreEqual(new[] { IsotopeStage.Glow }, seen, "Only the pickup changed the stage");
        }

        private ChemistryTrial MakeTrial(IsotopeStage stage)
        {
            var go = Track(new GameObject("Chemistry Trial"));
            go.transform.position = Origin;
            var trial = go.AddComponent<ChemistryTrial>();
            trial.Configure(stage, Stone(stage), new[] { Origin, Origin + new Vector2(5f, 0f), Origin + new Vector2(10f, 0f) }, 25, null);
            return trial;
        }

        [UnityTest]
        public IEnumerator Trial_ForEachStone_ThreeTargetsInOrder_WithThatStoneOnly_FiresOnce()
        {
            foreach (IsotopeStage stage in ChemistryIsotope.VariantOrder)
            {
                ChemistryTrial trial = MakeTrial(stage);
                ItemDefinition stone = Stone(stage);
                var fired = new List<int>();
                trial.Completed += c => fired.Add(c);
                int resets = 0;
                trial.ResetPuzzle += _ => resets++;
                yield return null;

                Assert.AreEqual(ChemistryTrial.PieceCount, trial.Pieces.Count);
                Assert.IsTrue(trial.Pieces.All(p => p.Kind == ChemistryTrial.KindFor(stage)), $"{stage}: targets of its stone's kind");
                CollectionAssert.AreEqual(new[] { 0, 1, 2 }, trial.Pieces.Select(p => p.Index).ToArray());

                // Other stones, other items or bare hands do nothing.
                foreach (ItemDefinition wrong in new[] { glow, wild, lead, other, null }.Where(i => i != stone))
                    Assert.IsFalse(trial.OnPieceHit(trial.Pieces[0], wrong), $"{stage}: {wrong} does nothing");
                Assert.AreEqual(0, trial.Progress);

                // Out of order: everything comes back.
                Assert.IsTrue(trial.OnPieceHit(trial.Pieces[0], stone));
                Assert.IsTrue(trial.Pieces[0].Done);
                Assert.IsTrue(trial.OnPieceHit(trial.Pieces[2], stone), "A wrong target resets");
                Assert.AreEqual(0, trial.Progress);
                Assert.IsFalse(trial.Pieces[0].Done);
                Assert.AreEqual(1, resets);

                // In order: 1, 2, 3 completes it.
                for (int i = 0; i < ChemistryTrial.PieceCount; i++)
                {
                    Assert.IsEmpty(fired);
                    Assert.IsTrue(trial.OnPieceHit(trial.Pieces[i], stone), $"{stage}: target {i + 1}");
                }
                Assert.IsTrue(trial.IsComplete);
                CollectionAssert.AreEqual(new[] { 25 }, fired, "Completion fires once with its currency");
                if (stage == IsotopeStage.Unstable)
                    Assert.IsFalse(trial.Pieces[0].GetComponent<Collider2D>().enabled, "Blasted blocks are gone");

                // Repeats don't fire.
                Assert.IsFalse(trial.OnPieceHit(trial.Pieces[0], stone));
                Assert.IsFalse(trial.Complete());
                CollectionAssert.AreEqual(new[] { 25 }, fired);
            }
        }

        [UnityTest]
        public IEnumerator Trial_RealSwings_HitOneTargetEach()
        {
            Isotope isotope = MakePlayer(Origin + new Vector2(0f, 0.9f)); // above target 1, facing down
            ChemistryTrial trial = MakeTrial(IsotopeStage.Lead);
            var body = isotope.GetComponent<Rigidbody2D>();
            yield return TestInput.PhysicsSteps();
            for (int i = 0; i < ChemistryTrial.PieceCount; i++)
            {
                body.position = (Vector2)trial.Pieces[i].transform.position + new Vector2(0f, 0.9f);
                yield return TestInput.PhysicsSteps();
                Assert.AreEqual(1, Swing(isotope, lead), $"One swing reaches target {i + 1}");
                Assert.IsTrue(trial.Pieces[i].Done);
            }
            Assert.IsTrue(trial.IsComplete);
        }

        [UnityTest]
        public IEnumerator Enemies_TakeMarkedlyMoreDamageFromTheirWeakness()
        {
            var beak = Track(BiologyBeaks.CreateItem(BeakKind.Thick));
            foreach (ChemistryEnemyKind kind in new[] { ChemistryEnemyKind.FreeRadical, ChemistryEnemyKind.RustMite })
            {
                ItemDefinition weakness = kind == ChemistryEnemyKind.FreeRadical ? glow : beak;
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
