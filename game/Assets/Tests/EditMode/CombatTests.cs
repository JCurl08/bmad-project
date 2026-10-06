using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Damage maths (pure and through Health with its modifiers): power scales outgoing damage, defence
    /// lowers incoming damage to a minimum of 1, an enemy's weakness item multiplies damage by at least 3,
    /// the invulnerability window ignores repeats, and Died fires once.
    /// </summary>
    public class CombatTests
    {
        private readonly List<Object> created = new List<Object>();
        private float now;

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created)
                if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        private ItemDefinition Item(string id)
        {
            var item = ItemDefinition.Create(id, id, Theme.Biology, Color.green);
            created.Add(item);
            return item;
        }

        private Health MakeHealth(float max, float invulnerable)
        {
            var go = new GameObject("Combat Test");
            created.Add(go);
            var health = go.AddComponent<Health>();
            health.SetMax(max);
            health.InvulnerableSeconds = invulnerable;
            health.Clock = () => now;
            now = 0f;
            return health;
        }

        private Enemy MakeEnemy(ItemDefinition weakness)
        {
            Health health = MakeHealth(Enemy.DefaultHealth, 0f);
            var enemy = health.gameObject.AddComponent<Enemy>();
            enemy.Weakness = weakness;
            return enemy;
        }

        [Test]
        public void StatMultiplier_IsOneAtOnePoint_PlusAQuarterPerPoint()
        {
            Assert.AreEqual(1f, CombatMath.StatMultiplier(1), 1e-5f);
            Assert.AreEqual(1.25f, CombatMath.StatMultiplier(2), 1e-5f);
            Assert.AreEqual(2f, CombatMath.StatMultiplier(5), 1e-5f);
        }

        [Test]
        public void Outgoing_IsBaseTimesPowerMultiplier()
        {
            Assert.AreEqual(1f, CombatMath.Outgoing(CombatMath.BaseAttack, 1), 1e-5f);
            Assert.AreEqual(1.5f, CombatMath.Outgoing(CombatMath.BaseAttack, 3), 1e-5f);
            Assert.Greater(CombatMath.Outgoing(1f, 2), CombatMath.Outgoing(1f, 1));
        }

        [Test]
        public void Incoming_DefenceCutsFifteenPercentPerPoint_NeverBelowAQuarter()
        {
            Assert.AreEqual(3f, CombatMath.Incoming(3f, 0), 1e-5f);
            Assert.AreEqual(3f * 0.85f, CombatMath.Incoming(3f, 1), 1e-5f);
            Assert.AreEqual(3f * 0.85f * 0.85f, CombatMath.Incoming(3f, 2), 1e-5f);
            Assert.AreEqual(0.85f, CombatMath.Incoming(1f, 1), 1e-5f, "Even a 1-damage hit is reduced");
            Assert.AreEqual(0.25f, CombatMath.Incoming(1f, 10), 1e-5f, "Defence never takes a hit below a quarter heart");
            Assert.AreEqual(0f, CombatMath.Incoming(0f, 0), 1e-5f, "No hit, no damage");
        }

        [Test]
        public void Weakness_MultipliesOnlyTheWeaknessItem_AtLeastThreeTimes()
        {
            ItemDefinition weak = Item("weak"), other = Item("other");
            float m = CombatMath.DefaultWeaknessMultiplier;
            Assert.AreEqual(4f, CombatMath.AgainstWeakness(1f, weak, weak, m), 1e-5f);
            Assert.AreEqual(1f, CombatMath.AgainstWeakness(1f, other, weak, m), 1e-5f);
            Assert.AreEqual(1f, CombatMath.AgainstWeakness(1f, null, weak, m), 1e-5f, "A bare hit is not the weakness");
            Assert.AreEqual(1f, CombatMath.AgainstWeakness(1f, null, null, m), 1e-5f, "No weakness: a bare hit is not multiplied");
            Assert.AreEqual(3f, CombatMath.AgainstWeakness(1f, weak, weak, 1.5f), 1e-5f, "The multiplier is clamped to at least 3");
        }

        [Test]
        public void EnemyHealth_WeaknessKillsInTwoHits_BareInEight()
        {
            ItemDefinition weak = Item("weak"), other = Item("other");

            Enemy enemy = MakeEnemy(weak);
            Health health = enemy.Health;
            float weakHit = health.TakeDamage(CombatMath.Outgoing(CombatMath.BaseAttack, 1), weak);
            float otherHitTarget = MakeEnemy(weak).Health.TakeDamage(CombatMath.Outgoing(CombatMath.BaseAttack, 1), other);
            Assert.GreaterOrEqual(weakHit, 3f * otherHitTarget, "Weakness hits must be at least 3x other hits");
            health.TakeDamage(1f, weak);
            Assert.IsTrue(health.IsDead, "Two weakness hits kill a default enemy");

            Health bare = MakeEnemy(weak).Health;
            int hits = 0;
            while (!bare.IsDead && hits < 100)
            {
                bare.TakeDamage(CombatMath.Outgoing(CombatMath.BaseAttack, 1), null);
                hits++;
            }
            Assert.AreEqual(8, hits, "Bare hits take many more swings");
        }

        [Test]
        public void PlayerDefence_ReducesDamageTaken_MinimumAQuarter()
        {
            Health health = MakeHealth(10f, 0f);
            var stats = health.gameObject.AddComponent<PlayerStats>();
            stats.MaxHealth = 10;
            stats.Defence = 2;
            Assert.AreEqual(5f * 0.7225f, health.TakeDamage(5f, null), 1e-5f);
            Assert.AreEqual(0.7225f, health.TakeDamage(1f, null), 1e-5f);
            stats.Defence = 0;
            Assert.AreEqual(2f, health.TakeDamage(2f, null), 1e-5f);
            Assert.AreEqual(10f - 5f * 0.7225f - 0.7225f - 2f, health.Current, 1e-5f);
        }

        [Test]
        public void ThickSkin_EachLevelReducesDamageTaken()
        {
            float previous = float.MaxValue;
            for (int level = 0; level <= UpgradeShop.MaxLevel; level++)
            {
                var save = new MetaSave();
                save.SetUpgradeLevel(UpgradeShop.Id(UpgradeStat.Defence), level);
                Health health = MakeHealth(10f, 0f);
                var stats = health.gameObject.AddComponent<PlayerStats>();
                UpgradeShop.Apply(new StatBlock(10, 0, 1, 1), save).ApplyTo(stats);
                float taken = health.TakeDamage(1f, null);
                Assert.Less(taken, previous, $"Thick Skin level {level} takes less than level {level - 1}");
                Assert.AreEqual(Mathf.Pow(0.85f, level), taken, 1e-5f);
                StringAssert.Contains($"-{Mathf.RoundToInt((1f - Mathf.Pow(0.85f, level)) * 100f)}%",
                    UpgradeShop.EffectText(UpgradeStat.Defence, 0, level));
                previous = taken;
                Object.DestroyImmediate(health.gameObject);
            }
        }

        [Test]
        public void Invulnerability_IgnoresDamageInsideTheWindow()
        {
            Health health = MakeHealth(6f, 0.8f);
            Assert.AreEqual(1f, health.TakeDamage(1f, null), 1e-5f);
            now = 0.5f;
            Assert.IsTrue(health.IsInvulnerable);
            Assert.AreEqual(0f, health.TakeDamage(1f, null), 1e-5f, "Damage during invulnerability does nothing");
            now = 0.81f;
            Assert.IsFalse(health.IsInvulnerable);
            Assert.AreEqual(1f, health.TakeDamage(1f, null), 1e-5f);
            Assert.AreEqual(4f, health.Current, 1e-5f);
        }

        [Test]
        public void Died_FiresOnce_FurtherDamageIgnored()
        {
            Health health = MakeHealth(2f, 0f);
            int died = 0, damaged = 0;
            health.Died += _ => died++;
            health.Damaged += (_, __, ___) => damaged++;
            health.TakeDamage(1.5f, null);
            health.TakeDamage(5f, null);
            Assert.IsTrue(health.IsDead);
            Assert.AreEqual(0f, health.Current, 1e-5f);
            Assert.AreEqual(0f, health.TakeDamage(5f, null), 1e-5f);
            Assert.AreEqual(1, died);
            Assert.AreEqual(2, damaged);
        }

        [Test]
        public void DisabledHealth_TakesNoDamage()
        {
            Health health = MakeHealth(6f, 0f);
            health.enabled = false;
            Assert.AreEqual(0f, health.TakeDamage(3f, null), 1e-5f);
            Assert.AreEqual(6f, health.Current, 1e-5f);
        }

        [Test]
        public void RaisingMaxHealth_AddsTheDifference()
        {
            Health health = MakeHealth(6f, 0f);
            var stats = health.gameObject.AddComponent<PlayerStats>();
            health.TakeDamage(2f, null);
            stats.MaxHealth = 8;
            Assert.AreEqual(8f, health.Max, 1e-5f);
            Assert.AreEqual(6f, health.Current, 1e-5f);
        }

        [Test]
        public void HudText_NeverSaysTheForbiddenWord()
        {
            StringAssert.DoesNotContain("sort", CombatHud.DeathMessage.ToLowerInvariant());
            StringAssert.DoesNotContain("sort", CombatHud.BareHandsLabel.ToLowerInvariant());
        }
    }
}
