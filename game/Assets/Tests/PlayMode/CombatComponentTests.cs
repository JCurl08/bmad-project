using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Tracer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using static Game.Cube.Tests.TestInput;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Combat on objects built in the test (far from anything a scene may hold): weakness versus other
    /// hits through Player/Attack, an enemy dying once and being removed, contact damage and the
    /// invulnerability window, defence, the player's death stopping movement and attack, a hostile NPC
    /// chasing and hurting the player and reverting when the flag clears, item cycling with
    /// Player/Previous and Player/Next, and the speed stat.
    /// </summary>
    public class CombatComponentTests : InputTestFixture
    {
        private static readonly Vector2 Origin = new Vector2(-5000f, -5000f);
        private const float Timeout = 5f;

        private readonly List<Object> created = new List<Object>();
        private Keyboard keyboard;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
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

        private ItemDefinition Item(string id, Color color) => Track(ItemDefinition.Create(id, id, Theme.Biology, color));

        private static Rect BoundsAround(Vector2 centre) => new Rect(centre - new Vector2(4f, 3f), new Vector2(8f, 6f));

        /// <summary>A player like the Cube scene's: dynamic body, collider, mover, inventory and combat parts.</summary>
        private GameObject MakePlayer(Vector2 position, bool kinematic = false)
        {
            var go = Track(new GameObject("Test Player"));
            go.transform.position = position;
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            if (kinematic) body.bodyType = RigidbodyType2D.Kinematic;
            go.AddComponent<CircleCollider2D>().radius = 0.4f;
            go.AddComponent<PlayerMover>();
            go.AddComponent<Inventory>();
            go.AddComponent<Health>();
            go.AddComponent<PlayerStats>();
            go.AddComponent<Equipment>();
            go.AddComponent<PlayerAttack>();
            return go;
        }

        private Enemy MakeEnemy(ItemDefinition weakness, Vector2 position, Transform player, bool brain = true)
        {
            Enemy enemy = Enemy.Spawn(weakness, position, BoundsAround(Origin), player);
            Track(enemy.gameObject);
            enemy.GetComponent<EnemyBrain>().enabled = brain;
            return enemy;
        }

        [UnityTest]
        public IEnumerator WeaknessHit_DealsAtLeastThreeTimesAnOtherOrBareHit()
        {
            ItemDefinition weak = Item("weak", Color.green), other = Item("other", Color.blue);
            GameObject player = MakePlayer(Origin);
            var inventory = player.GetComponent<Inventory>();
            var equipment = player.GetComponent<Equipment>();
            var attack = player.GetComponent<PlayerAttack>();
            inventory.Add(weak);
            inventory.Add(other);
            Assert.AreSame(weak, equipment.Equipped, "The first item picked up is equipped");

            Vector2 front = Origin + player.GetComponent<PlayerMover>().Facing * attack.Reach;
            Enemy enemy = MakeEnemy(weak, front, player.transform, brain: false);
            Health health = enemy.Health;
            yield return null;

            yield return this.Tap(keyboard.enterKey);
            float weakHit = Enemy.DefaultHealth - health.Current;
            Assert.AreEqual(4f, weakHit, 1e-4f, "Player/Attack with the weakness item");

            yield return new WaitForSeconds(attack.Cooldown + 0.05f);
            equipment.Equip(other);
            yield return this.Tap(keyboard.enterKey);
            float otherHit = Enemy.DefaultHealth - weakHit - health.Current;
            Assert.AreEqual(1f, otherHit, 1e-4f, "Another item does base damage");

            yield return new WaitForSeconds(attack.Cooldown + 0.05f);
            equipment.Equip(null);
            float before = health.Current;
            yield return this.Tap(keyboard.enterKey);
            float bareHit = before - health.Current;
            Assert.AreEqual(1f, bareHit, 1e-4f, "A bare attack still does base damage");
            Assert.GreaterOrEqual(weakHit, 3f * otherHit);

            // Power scales outgoing damage.
            player.GetComponent<PlayerStats>().Power = 3;
            yield return new WaitForSeconds(attack.Cooldown + 0.05f);
            before = health.Current;
            yield return this.Tap(keyboard.enterKey);
            Assert.AreEqual(1.5f, before - health.Current, 1e-4f, "Power 3 = x1.5");
        }

        [UnityTest]
        public IEnumerator Attack_HasACooldown_AndMissesWhatIsBehind()
        {
            ItemDefinition weak = Item("weak", Color.green);
            GameObject player = MakePlayer(Origin);
            var attack = player.GetComponent<PlayerAttack>();
            Enemy behind = MakeEnemy(weak, Origin - player.GetComponent<PlayerMover>().Facing * attack.Reach, player.transform, false);
            Enemy front = MakeEnemy(weak, Origin + player.GetComponent<PlayerMover>().Facing * attack.Reach, player.transform, false);
            yield return null;

            Assert.AreEqual(1, attack.TryAttack());
            Assert.AreEqual(-1, attack.TryAttack(), "A second swing inside the cooldown does nothing");
            Assert.AreEqual(Enemy.DefaultHealth - 1f, front.Health.Current, 1e-4f);
            Assert.AreEqual(Enemy.DefaultHealth, behind.Health.Current, 1e-4f);
        }

        [UnityTest]
        public IEnumerator Attack_CountsOnlyTargetsThatTookDamage()
        {
            GameObject player = MakePlayer(Origin);
            var attack = player.GetComponent<PlayerAttack>();
            Vector2 front = Origin + player.GetComponent<PlayerMover>().Facing * attack.Reach;
            Enemy enemy = MakeEnemy(null, front, player.transform, false);
            enemy.Health.InvulnerableSeconds = 10f;
            NpcSpec spec = NpcFactory.ChooseParts(1, Race.Mushroom, 0);
            spec = spec.With(RacePartSets.For(Race.Mushroom).Legs.First(l => l.Movement == Movement.Stand));
            NpcTalker npc = NpcFactory.Spawn(spec, new[] { "hi" }, front + new Vector2(0.75f, -0.3f), BoundsAround(Origin), new RaceRelations(), player.transform);
            Track(npc.gameObject);
            int reported = -1;
            attack.Swung += (_, n) => reported = n;
            yield return null;
            Physics2D.SyncTransforms();
            Assert.IsTrue(Physics2D.OverlapBoxAll(front, attack.HitboxSize, -90f).Any(c => c.attachedRigidbody != null && c.attachedRigidbody.gameObject == npc.gameObject),
                "Control: the peaceful NPC is inside the swing");

            Assert.AreEqual(1, attack.TryAttack(), "The enemy is hit; the peaceful NPC in the box is not counted");
            Assert.AreEqual(1, reported);
            Assert.AreEqual(Enemy.DefaultHealth - 1f, enemy.Health.Current, 1e-4f, "Each target is hit once per swing");
            Assert.AreEqual(npc.GetComponent<Health>().Max, npc.GetComponent<Health>().Current, 1e-4f);

            yield return new WaitForSeconds(attack.Cooldown + 0.05f);
            Assert.AreEqual(0, attack.TryAttack(), "An invulnerable enemy and a peaceful NPC count as no hits");
            Assert.AreEqual(0, reported);
        }

        [UnityTest]
        public IEnumerator DeadEnemyNotRemoved_StopsMovingAndDealsNoContactDamage()
        {
            GameObject player = MakePlayer(Origin, kinematic: true);
            Health playerHealth = player.GetComponent<Health>();
            Enemy enemy = MakeEnemy(null, Origin + new Vector2(2.5f, 0f), player.transform);
            enemy.DestroyOnDeath = false;
            var brain = enemy.GetComponent<EnemyBrain>();
            var body = enemy.GetComponent<Rigidbody2D>();
            yield return WaitUntilOrTimeout(() => brain.Chasing && body.linearVelocity.sqrMagnitude > 0.1f, Timeout);
            Assert.IsTrue(brain.Chasing, "Control: the living enemy chases");

            enemy.Health.TakeDamage(100f, null);
            Assert.IsTrue(enemy.Health.IsDead);
            Assert.IsTrue(enemy != null && enemy.gameObject.activeInHierarchy, "DestroyOnDeath=false keeps it");
            Vector2 at = body.position;
            yield return new WaitForSeconds(1f);
            Assert.IsFalse(brain.Alive);
            Assert.IsFalse(brain.Chasing);
            Assert.Less(Vector2.Distance(at, body.position), 0.02f, "A dead enemy stops moving");
            Assert.AreEqual(playerHealth.Max, playerHealth.Current, 1e-4f, "A dead enemy deals no contact damage");

            // Even when touching the player.
            body.position = Origin + new Vector2(0.8f, 0f);
            Physics2D.SyncTransforms();
            Assert.AreEqual(0f, brain.TryContactDamage(), 1e-5f);
        }

        [UnityTest]
        public IEnumerator EnemyDies_DiedRaisedOnce_AndIsRemoved()
        {
            ItemDefinition weak = Item("weak", Color.green);
            GameObject player = MakePlayer(Origin);
            player.GetComponent<Inventory>().Add(weak);
            var attack = player.GetComponent<PlayerAttack>();
            Enemy enemy = MakeEnemy(weak, Origin + player.GetComponent<PlayerMover>().Facing * attack.Reach, player.transform, false);
            int died = 0, healthDied = 0;
            enemy.Died += _ => died++;
            enemy.Health.Died += _ => healthDied++;
            GameObject enemyObject = enemy.gameObject;
            yield return null;

            Assert.AreEqual(1, attack.TryAttack());
            yield return new WaitForSeconds(attack.Cooldown + 0.05f);
            Assert.AreEqual(1, attack.TryAttack());
            Assert.AreEqual(1, died);
            Assert.AreEqual(1, healthDied);
            yield return null;
            Assert.IsTrue(enemyObject == null, "A dead enemy is removed");
            Assert.IsFalse(Enemy.Active.Contains(enemy));
        }

        [UnityTest]
        public IEnumerator ContactDamage_ThenBrieflyInvulnerable()
        {
            GameObject player = MakePlayer(Origin, kinematic: true);
            Health health = player.GetComponent<Health>();
            int damaged = 0;
            health.Damaged += (_, __, ___) => damaged++;
            MakeEnemy(null, Origin + new Vector2(1.5f, 0f), player.transform);

            yield return WaitUntilOrTimeout(() => damaged > 0, Timeout);
            Assert.AreEqual(1, damaged, "The chasing enemy reached the player");
            Assert.AreEqual(PlayerStats.DefaultMaxHealth - 1f, health.Current, 1e-4f);
            Assert.IsTrue(health.IsInvulnerable);

            yield return new WaitForSeconds(health.InvulnerableSeconds * 0.6f);
            Assert.AreEqual(1, damaged, "Repeated contact during invulnerability does nothing");

            yield return WaitUntilOrTimeout(() => damaged > 1, Timeout);
            Assert.AreEqual(2, damaged, "Contact hurts again once the window ends");
            Assert.AreEqual(PlayerStats.DefaultMaxHealth - 2f, health.Current, 1e-4f);
        }

        [UnityTest]
        public IEnumerator Defence_LowersContactDamage_MinimumAQuarter()
        {
            GameObject player = MakePlayer(Origin, kinematic: true);
            Health health = player.GetComponent<Health>();
            var stats = player.GetComponent<PlayerStats>();
            stats.Defence = 1;
            Enemy enemy = MakeEnemy(null, Origin + new Vector2(1.2f, 0f), player.transform);
            enemy.ContactDamage = 3f;

            yield return WaitUntilOrTimeout(() => health.Current < health.Max, Timeout);
            Assert.AreEqual(health.Max - 3f * 0.85f, health.Current, 1e-4f, "3 contact damage x 0.85 (1 defence)");

            stats.Defence = 20; // 3 x 0.85^20 is about 0.12: below the floor
            float before = health.Current;
            yield return WaitUntilOrTimeout(() => health.Current < before, Timeout);
            Assert.AreEqual(before - CombatMath.MinHit, health.Current, 1e-4f, "Defence never takes a hit below a quarter heart");
        }

        [UnityTest]
        public IEnumerator PlayerDies_DiedOnce_MovementAttackCyclingAndTalkingStop_FurtherDamageIgnored()
        {
            GameObject player = MakePlayer(Origin);
            ItemDefinition a = Item("Alpha Item", Color.green), b = Item("Beta Item", Color.blue);
            player.GetComponent<Inventory>().Add(a);
            player.GetComponent<Inventory>().Add(b);
            var equipment = player.GetComponent<Equipment>();
            var box = Track(new GameObject("Test Dialogue Box")).AddComponent<DialogueBox>();
            NpcSpec spec = NpcFactory.ChooseParts(1, Race.Mushroom, 0);
            spec = spec.With(RacePartSets.For(Race.Mushroom).Legs.First(l => l.Movement == Movement.Stand));
            NpcTalker npc = NpcFactory.Spawn(spec, new[] { "hi" }, Origin + new Vector2(1.2f, 0f), BoundsAround(Origin), new RaceRelations(), player.transform);
            Track(npc.gameObject);
            npc.Box = box;
            var stats = player.GetComponent<PlayerStats>();
            Health health = player.GetComponent<Health>();
            var attack = player.GetComponent<PlayerAttack>();
            int died = 0, statsDied = 0, swings = 0;
            health.Died += _ => died++;
            stats.Died += _ => statsDied++;
            attack.Swung += (_, __) => swings++;
            yield return null;
            Assert.IsTrue(npc.TryTalk(), "Control: a living player can talk");
            box.Close();
            yield return null;

            health.TakeDamage(100f, null);
            Assert.IsTrue(health.IsDead);
            Assert.AreEqual(0f, health.TakeDamage(1f, null), 1e-5f, "Further damage is ignored");
            Assert.AreEqual(1, died);
            Assert.AreEqual(1, statsDied);
            Assert.IsFalse(player.GetComponent<PlayerMover>().enabled, "Movement stops");
            Assert.IsFalse(attack.enabled, "Attack stops");
            Assert.IsFalse(equipment.enabled, "Item cycling stops");

            Vector2 at = player.transform.position;
            Press(keyboard.dKey, queueEventOnly: true);
            yield return this.Tap(keyboard.enterKey);
            yield return new WaitForSeconds(0.3f);
            Release(keyboard.dKey, queueEventOnly: true);
            yield return null;
            Assert.Less(Vector2.Distance(at, player.transform.position), 0.01f, "A dead player does not move");
            Assert.AreEqual(0, swings, "A dead player does not attack");
            Assert.AreEqual(1, died);

            yield return this.Tap(keyboard.digit2Key);
            Assert.AreSame(a, equipment.Equipped, "A dead player cannot cycle items");
            yield return this.Tap(keyboard.eKey);
            Assert.IsFalse(box.IsOpen, "A dead player cannot talk");
            Assert.IsFalse(npc.TryTalk());
        }

        [UnityTest]
        public IEnumerator HostileNpc_ChasesAndHurtsThePlayer_CanBeHurt_RevertsWhenTheFlagClears()
        {
            var relations = new RaceRelations();
            GameObject player = MakePlayer(Origin, kinematic: true);
            Health playerHealth = player.GetComponent<Health>();
            NpcSpec spec = NpcFactory.ChooseParts(1, Race.Shape, 0);
            spec = spec.With(RacePartSets.For(Race.Shape).Legs.First(l => l.Movement == Movement.Stand));
            NpcTalker npc = NpcFactory.Spawn(spec, new[] { "hi" }, Origin + new Vector2(2.5f, 0f), BoundsAround(Origin), relations, player.transform);
            Track(npc.gameObject);
            var hostile = npc.GetComponent<HostileNpc>();
            var npcHealth = npc.GetComponent<Health>();
            var brain = npc.GetComponent<EnemyBrain>();
            var mover = npc.GetComponent<NpcMover>();
            Assert.IsNotNull(hostile, "NPCs carry the dormant enemy parts");
            yield return null;

            Assert.IsFalse(hostile.Hostile);
            Assert.IsFalse(brain.enabled);
            Assert.IsTrue(mover.enabled);
            Assert.AreEqual(0f, npcHealth.TakeDamage(1f, null), 1e-5f, "A peaceful NPC cannot be hurt");

            relations.SetHostile(Race.Shape, true);
            Assert.IsTrue(hostile.Hostile);
            Assert.IsTrue(brain.enabled);
            Assert.IsFalse(mover.enabled);
            Assert.IsTrue(npc.enabled, "The talker stays (it refuses while hostile)");

            yield return WaitUntilOrTimeout(() => playerHealth.Current < playerHealth.Max, Timeout);
            Assert.Less(playerHealth.Current, playerHealth.Max, "The hostile NPC chased and hurt the player");
            Assert.AreEqual(1f, npcHealth.TakeDamage(1f, null), 1e-5f, "A hostile NPC can be hurt");

            relations.SetHostile(Race.Shape, false);
            Assert.IsFalse(hostile.Hostile);
            Assert.IsFalse(brain.enabled);
            Assert.IsFalse(npcHealth.enabled);
            Assert.IsTrue(mover.enabled, "NpcMover restored");
            Assert.IsTrue(npc.enabled, "NpcTalker restored");
            Assert.IsFalse(npc.IsHostile);
            float after = playerHealth.Current;
            yield return new WaitForSeconds(playerHealth.InvulnerableSeconds + 0.5f);
            Assert.AreEqual(after, playerHealth.Current, 1e-5f, "A reverted NPC stops attacking");
        }

        [UnityTest]
        public IEnumerator ItemCycle_NextAndPrevious_ChangeTheEquippedItem_ShownOnTheHud()
        {
            GameObject player = MakePlayer(Origin);
            var inventory = player.GetComponent<Inventory>();
            var equipment = player.GetComponent<Equipment>();
            var hud = Track(new GameObject("Test HUD")).AddComponent<CombatHud>();
            hud.Health = player.GetComponent<Health>();
            hud.Equipment = equipment;
            yield return null;

            yield return this.Tap(keyboard.digit2Key);
            Assert.IsNull(equipment.Equipped, "With no items, Next does nothing");
            StringAssert.Contains(CombatHud.BareHandsLabel, hud.EquippedText);

            ItemDefinition a = Item("Alpha Item", Color.green), b = Item("Beta Item", Color.blue);
            inventory.Add(a);
            inventory.Add(b);
            Assert.AreSame(a, equipment.Equipped);

            yield return this.Tap(keyboard.digit2Key);
            Assert.AreSame(b, equipment.Equipped, "Next");
            StringAssert.Contains("Beta Item", hud.EquippedText);
            yield return this.Tap(keyboard.digit2Key);
            Assert.AreSame(a, equipment.Equipped, "Next wraps");
            yield return this.Tap(keyboard.digit1Key);
            Assert.AreSame(b, equipment.Equipped, "Previous wraps");
            StringAssert.Contains("Beta Item", hud.EquippedText);
        }

        [UnityTest]
        public IEnumerator SpeedStat_MakesThePlayerFaster()
        {
            GameObject player = MakePlayer(Origin);
            var stats = player.GetComponent<PlayerStats>();
            var mover = player.GetComponent<PlayerMover>();
            yield return null;

            float slow = 0f, fast = 0f;
            for (int run = 0; run < 2; run++)
            {
                stats.Speed = run == 0 ? 1 : 3;
                var body = player.GetComponent<Rigidbody2D>();
                body.position = Origin;
                body.linearVelocity = Vector2.zero;
                yield return new WaitForFixedUpdate();
                yield return null; // input state is only valid in dynamic update
                Press(keyboard.dKey, queueEventOnly: true);
                yield return new WaitForSeconds(0.5f);
                float speed = body.linearVelocity.magnitude;
                Release(keyboard.dKey, queueEventOnly: true);
                yield return null;
                if (run == 0) slow = speed;
                else fast = speed;
            }
            Assert.AreEqual(mover.Speed, slow, 0.05f, "Speed 1 = base speed");
            Assert.AreEqual(mover.Speed * 1.5f, fast, 0.05f, "Speed 3 = x1.5");
            Assert.AreEqual(mover.Speed * 1.5f, mover.EffectiveSpeed, 1e-4f);
        }
    }
}
