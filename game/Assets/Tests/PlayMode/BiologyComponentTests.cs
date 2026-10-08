using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Game.Tracer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The Biology beak mechanics on objects built in the test (far from anything a scene may hold): the thick
    /// beak breaks a rock gate while other items, bare swings and touches do not; the thin beak presses a
    /// distant button from beyond normal reach but not from beyond its own; pollinating a flower grows its vine
    /// bridge once; difficulty adds hits; the trial fires once for each beak; Biology enemies take markedly
    /// more damage from their weakness.
    /// </summary>
    public class BiologyComponentTests : InputTestFixture
    {
        /// <summary>Lengths and speeds below are in the first 16x10 screens' units, scaled like the game's (4.1).</summary>
        private const float K = ScreenMath.ContentScale;

        private static readonly Vector2 Origin = new Vector2(-7000f, -7000f);

        private readonly List<Object> created = new List<Object>();
        private ItemDefinition thin, thick, other;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            thin = Track(BiologyBeaks.CreateItem(BeakKind.Thin));
            thick = Track(BiologyBeaks.CreateItem(BeakKind.Thick));
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

        /// <summary>A player like the Cube scene's, facing down (its default), with every test item owned.</summary>
        private PlayerAttack MakePlayer(Vector2 position)
        {
            var go = Track(new GameObject("Test Player"));
            go.transform.position = position;
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
            go.AddComponent<CircleCollider2D>().radius = 0.4f * K;
            go.AddComponent<Game.Tracer.PlayerMover>();
            var inventory = go.AddComponent<Inventory>();
            go.AddComponent<Health>();
            go.AddComponent<PlayerStats>();
            go.AddComponent<Equipment>();
            var attack = go.AddComponent<PlayerAttack>();
            attack.Cooldown = 0f;
            inventory.Add(thin);
            inventory.Add(thick);
            inventory.Add(other);
            return attack;
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

        private static int Swing(PlayerAttack attack, ItemDefinition item)
        {
            attack.GetComponent<Equipment>().Equip(item);
            attack.TryAttack();
            return attack.LastReceiversHit;
        }

        [UnityTest]
        public IEnumerator ThickBeak_BreaksARockGate_OtherItemsBareAndTouchDoNot()
        {
            PlayerAttack attack = MakePlayer(Origin);
            BreakableGate gate = BreakableGate.Build(Block(Origin + Vector2.down * 1.1f * K), thick, BreakableLook.Rock, 1, false, null);
            int opened = 0;
            gate.Opened += _ => opened++;
            yield return null;

            Swing(attack, null);
            Swing(attack, other);
            Swing(attack, thin);
            Assert.IsFalse(gate.TryOpen(attack.GetComponent<Collider2D>()), "Touching never opens a beak gate");
            Assert.IsFalse(gate.IsOpen, "Only the thick beak breaks it");
            Assert.IsTrue(gate.Solid.enabled);

            Assert.AreEqual(1, Swing(attack, thick));
            Assert.IsTrue(gate.IsOpen, "The thick beak breaks the rock");
            Assert.IsFalse(gate.Solid.enabled, "The alcove is open");
            Assert.AreEqual(0, Swing(attack, thick), "A broken rock ignores more swings");
            Assert.AreEqual(1, opened);
        }

        [UnityTest]
        public IEnumerator ThinBeak_PressesADistantButton_FromBeyondNormalReach_ButNotBeyondItsOwn()
        {
            PlayerAttack attack = MakePlayer(Origin);
            ButtonGate near = ButtonGate.Build(Block(Origin + new Vector2(30f * K, 0f)), thin, Origin + Vector2.down * 2.3f * K,
                null, 1, false, null);
            Track(near.Button.gameObject);
            ButtonGate far = ButtonGate.Build(Block(Origin + new Vector2(40f * K, 0f)), thin, Origin + new Vector2(0f, -3.8f * K),
                null, 1, false, null);
            Track(far.Button.gameObject);
            yield return null;

            attack.GetComponent<Equipment>().Equip(thick);
            Assert.AreEqual(attack.Reach, attack.EffectiveReach, 1e-4f);
            attack.GetComponent<Equipment>().Equip(thin);
            Assert.AreEqual(BiologyBeaks.ThinReach, attack.EffectiveReach, 1e-4f, "The thin beak reaches about 2.5");

            Swing(attack, thick);
            Swing(attack, other);
            Assert.IsFalse(near.IsOpen, "Normal reach does not get to a button 2.3 away (and only the thin beak counts)");

            Assert.AreEqual(1, Swing(attack, thin));
            Assert.IsTrue(near.IsOpen, "The thin beak presses the distant button and the linked door opens");
            Assert.IsTrue(near.Button.IsPressed);
            Assert.IsFalse(near.Solid.enabled);
            Assert.IsFalse(far.IsOpen, "A button out of thin-beak range is untouched");
        }

        [UnityTest]
        public IEnumerator Pollinating_AFlower_GrowsItsVineBridge_Once()
        {
            PlayerAttack attack = MakePlayer(Origin);
            FlowerVineGate vine = FlowerVineGate.Build(Block(Origin + new Vector2(16f * K, 0f)), thin, Origin + Vector2.down * 2f * K,
                null, 1, false, null);
            Track(vine.Flower.gameObject);
            yield return null;

            Swing(attack, thick);
            Assert.IsFalse(vine.IsOpen, "The thick beak cannot pollinate");
            Assert.IsNull(vine.Bridge);

            Assert.AreEqual(1, Swing(attack, thin));
            Assert.IsTrue(vine.IsOpen, "Pollinating the flower opens its linked vine gate");
            Assert.IsNotNull(vine.Bridge, "A vine bridge grows across the opening");
            Assert.IsTrue(vine.Flower.Used);
            Assert.AreEqual(0, Swing(attack, thin), "A used flower does nothing");
        }

        [UnityTest]
        public IEnumerator Difficulty_MoreBeakGatesOnTheFace_MeansMoreHits()
        {
            PlayerAttack attack = MakePlayer(Origin);
            // Tuned 2x2 face: four beak gates on the face take two hits each, three take one.
            BreakableGate gate = BreakableGate.Build(Block(Origin + Vector2.down * 1.1f * K), thick, BreakableLook.Pot, 4, false, null, 2);
            BreakableGate fewer = BreakableGate.Build(Block(Origin + new Vector2(30f * K, 0f)), thick, BreakableLook.Pot, 3, false, null, 2);
            // 3x3 face: the same density per screen, so eight gates take two hits and seven take one.
            BreakableGate big = BreakableGate.Build(Block(Origin + new Vector2(40f * K, 0f)), thick, BreakableLook.Rock, 8, false, null, 3);
            BreakableGate bigFewer = BreakableGate.Build(Block(Origin + new Vector2(50f * K, 0f)), thick, BreakableLook.Rock, 7, false, null, 3);
            yield return null;
            Assert.AreEqual(2, gate.HitsToOpen);
            Assert.AreEqual(1, fewer.HitsToOpen);
            Assert.AreEqual(2, big.HitsToOpen);
            Assert.AreEqual(1, bigFewer.HitsToOpen);
            Swing(attack, thick);
            Assert.IsFalse(gate.IsOpen);
            Assert.AreEqual(1, gate.Hits);
            Swing(attack, thick);
            Assert.IsTrue(gate.IsOpen);
        }

        private BiologyTrial MakeTrial(BeakKind beak, ItemDefinition item, IReadOnlyList<Vector2> positions, List<int> fired)
        {
            var go = Track(new GameObject("Trial"));
            go.transform.position = Origin + new Vector2(60f * K, 0f);
            var trial = go.AddComponent<BiologyTrial>();
            trial.Configure(beak, item, positions, BiologyPlan.TrialCurrency, null);
            trial.Completed += c => fired.Add(c);
            return trial;
        }

        [UnityTest]
        public IEnumerator ThickTrial_RocksInOrder_FiresOnce_AndAWrongRockResets()
        {
            var fired = new List<int>();
            Vector2 c = Origin + new Vector2(60f * K, 0f);
            BiologyTrial trial = MakeTrial(BeakKind.Thick, thick,
                new[] { c + new Vector2(-3f * K, 0f), c, c + new Vector2(3f * K, 0f) }, fired);
            yield return null;
            IReadOnlyList<TrialTarget> rocks = trial.Targets;
            Assert.AreEqual(3, rocks.Count);
            Assert.IsFalse(trial.CompleteOnPlayerTouch);

            Assert.IsFalse(rocks[0].ReceiveAttack(thin, null), "Only the rolled beak counts");
            Assert.IsTrue(rocks[0].ReceiveAttack(thick, null));
            Assert.IsTrue(rocks[0].Done);
            Assert.IsTrue(rocks[2].ReceiveAttack(thick, null), "A wrong rock resets the puzzle");
            Assert.IsFalse(rocks[0].Done);
            Assert.AreEqual(0, trial.Progress);

            // Through real swings: stand above each rock in turn (facing down) with the thick beak.
            PlayerAttack attack = MakePlayer(Origin);
            for (int i = 0; i < 3; i++)
            {
                attack.transform.position = (Vector2)rocks[i].transform.position + Vector2.up * 0.9f * K;
                Swing(attack, thick);
            }
            Assert.IsTrue(trial.IsComplete);
            CollectionAssert.AreEqual(new[] { BiologyPlan.TrialCurrency }, fired, "Completed fires once with its currency");
            rocks[2].ReceiveAttack(thick, null);
            Assert.IsFalse(trial.Complete(), "Repeats don't fire");
            Assert.AreEqual(1, fired.Count);
        }

        [UnityTest]
        public IEnumerator ThinTrial_TwoButtonsThenTheFlower_FiresOnce()
        {
            var fired = new List<int>();
            Vector2 c = Origin + new Vector2(80f * K, 0f);
            BiologyTrial trial = MakeTrial(BeakKind.Thin, thin,
                new[] { c + new Vector2(-3f * K, 0f), c + new Vector2(3f * K, 0f), c + new Vector2(0f, 3f * K) }, fired);
            yield return null;
            IReadOnlyList<TrialTarget> t = trial.Targets;
            Assert.AreEqual(TrialTargetKind.Flower, t[2].Kind);

            Assert.IsFalse(t[2].ReceiveAttack(thin, null), "The flower waits for both buttons");
            Assert.IsTrue(t[0].ReceiveAttack(thin, null));
            Assert.IsFalse(t[2].ReceiveAttack(thin, null));
            Assert.IsFalse(t[1].ReceiveAttack(thick, null), "Only the rolled beak counts");
            Assert.IsTrue(t[1].ReceiveAttack(thin, null));
            Assert.IsTrue(t[2].ReceiveAttack(thin, null));
            Assert.IsTrue(trial.IsComplete);
            Assert.IsFalse(t[2].ReceiveAttack(thin, null));
            CollectionAssert.AreEqual(new[] { BiologyPlan.TrialCurrency }, fired);
        }

        [UnityTest]
        public IEnumerator BiologyEnemies_TakeMarkedlyMoreDamageFromTheirWeakness()
        {
            var bounds = new Rect(Origin + new Vector2(90f * K, -5f * K), new Vector2(10f * K, 10f * K));
            Enemy weevil = BiologyEnemies.Spawn(BiologyEnemyKind.SeedWeevil, thick, bounds.center, bounds, null, null, null);
            Enemy puff = BiologyEnemies.Spawn(BiologyEnemyKind.PollenPuff, other, bounds.center + Vector2.right * 3f * K, bounds, null, null, null);
            Track(puff.gameObject);
            Track(weevil.gameObject);
            weevil.GetComponent<EnemyBrain>().enabled = false;
            puff.GetComponent<EnemyBrain>().enabled = false;
            weevil.Health.InvulnerableSeconds = 0f;
            puff.Health.InvulnerableSeconds = 0f;
            yield return null;

            StringAssert.StartsWith("Seed Weevil", weevil.name);
            StringAssert.StartsWith("Pollen Puff", puff.name);
            Assert.AreEqual(BiologyEnemyKind.PollenPuff, puff.GetComponent<BiologyEnemy>().Kind);

            float weevilWeak = weevil.Health.TakeDamage(1f, thick);
            float weevilOther = weevil.Health.TakeDamage(1f, thin);
            float puffWeak = puff.Health.TakeDamage(1f, other);
            float puffBeak = puff.Health.TakeDamage(1f, thick);
            Assert.GreaterOrEqual(weevilWeak, 3f * weevilOther, "Seed Weevil: weak to the beak");
            Assert.GreaterOrEqual(puffWeak, 3f * puffBeak, "Pollen Puff: weak to another face's item");
        }
    }
}
