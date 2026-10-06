using System.Collections;
using Game.Tracer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Combat in the real Cube scene: the player carries the combat parts and a HUD watches them, and an
    /// F7 enemy (weak to the Biology item when nothing is owned) falls to a couple of weakness hits but
    /// takes many bare ones.
    /// </summary>
    public class CombatSceneTests : InputTestFixture
    {
        private const string SceneName = "Cube";

        private Keyboard keyboard;
        private CubeWorld world;
        private CubeDebug debug;
        private GameObject player;

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
            debug = Object.FindAnyObjectByType<CubeDebug>();
            player = Object.FindAnyObjectByType<CubeNavigator>().gameObject;
            Assert.IsNotNull(world);
            Assert.IsNotNull(debug);
        }

        /// <summary>Spawns an enemy with F7 and puts it, standing still, right in front of the player.</summary>
        private IEnumerator SpawnEnemyInFront(System.Action<Enemy> result)
        {
            int before = debug.DebugEnemies.Count;
            yield return this.Tap(keyboard.f7Key);
            Assert.AreEqual(before + 1, debug.DebugEnemies.Count, "F7 spawned an enemy");
            Enemy enemy = debug.DebugEnemies[debug.DebugEnemies.Count - 1];
            enemy.GetComponent<EnemyBrain>().enabled = false;
            var attack = player.GetComponent<PlayerAttack>();
            Vector2 front = (Vector2)player.transform.position + attack.Facing * attack.Reach;
            enemy.GetComponent<Rigidbody2D>().position = front;
            enemy.transform.position = front;
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null; // input state is only valid in dynamic update
            result(enemy);
        }

        private IEnumerator SwingsToKill(Enemy enemy, System.Action<int> result)
        {
            var attack = player.GetComponent<PlayerAttack>();
            Health health = enemy.Health;
            int swings = 0;
            while (!health.IsDead && swings < 30)
            {
                yield return this.Tap(keyboard.enterKey);
                swings++;
                yield return new WaitForSeconds(attack.Cooldown + 0.05f);
            }
            result(swings);
        }

        [UnityTest]
        public IEnumerator Player_HasTheCombatParts_AndTheHudWatchesThem()
        {
            yield return Load();
            Assert.IsNotNull(player.GetComponent<Health>());
            Assert.IsNotNull(player.GetComponent<PlayerStats>());
            Assert.IsNotNull(player.GetComponent<Equipment>());
            Assert.IsNotNull(player.GetComponent<PlayerAttack>());
            Assert.AreEqual(PlayerStats.DefaultMaxHealth, player.GetComponent<Health>().Max, 1e-4f);
            var hud = Object.FindAnyObjectByType<CombatHud>();
            Assert.IsNotNull(hud, "The Cube scene has a CombatHud");
            Assert.AreSame(player.GetComponent<Health>(), hud.Health);
            Assert.AreSame(player.GetComponent<Equipment>(), hud.Equipment);
            Assert.IsNull(hud.MessageText);
            player.GetComponent<Health>().TakeDamage(100f, null);
            StringAssert.Contains("entropy wins", hud.MessageText);
            Assert.IsFalse(player.GetComponent<PlayerMover>().enabled);
        }

        [UnityTest]
        public IEnumerator F7Enemy_FallsToAFewWeaknessHits_ButTakesManyBareOnes()
        {
            yield return Load();
            ItemDefinition biology = world.ItemFor(Theme.Biology); // the run's beak
            Assert.IsNotNull(biology);

            Enemy bareTarget = null;
            yield return SpawnEnemyInFront(e => bareTarget = e);
            Assert.AreSame(biology, bareTarget.Weakness, "With nothing owned, F7 enemies are weak to the Biology item");
            int bare = 0;
            yield return SwingsToKill(bareTarget, n => bare = n);

            player.GetComponent<Inventory>().Add(biology);
            Assert.AreSame(biology, player.GetComponent<Equipment>().Equipped);
            Enemy weakTarget = null;
            yield return SpawnEnemyInFront(e => weakTarget = e);
            Assert.AreSame(biology, weakTarget.Weakness, "F7 picks an owned item");
            int weak = 0;
            yield return SwingsToKill(weakTarget, n => weak = n);

            Assert.AreEqual(8, bare, "Bare: many hits");
            Assert.AreEqual(2, weak, "Weakness: a couple of hits");
        }
    }
}
