using System.Collections;
using Game.Tracer;
using NUnit.Framework;
using UnityEngine;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Digs a mound (4.7) the way a player does: stands beside it on clear ground, faces it and swings RewardMound.HitsToOpen
    /// times, waiting out the attack cooldown. The scene tests use it wherever a reward lies under a mound.
    /// </summary>
    public static class RewardDig
    {
        public static IEnumerator Dig(CubeNavigator navigator, RewardMound mound)
        {
            Assert.IsNotNull(mound, "A mound to dig");
            var attack = navigator.GetComponent<PlayerAttack>();
            var body = navigator.GetComponent<Rigidbody2D>();
            var mover = navigator.GetComponent<PlayerMover>();
            Vector2 centre = mound.transform.position;
            Vector2 side = Vector2.right;
            foreach (Vector2 dir in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down })
            {
                if (!Clear(centre - dir * Stand(attack), navigator.gameObject)) continue;
                side = dir;
                break;
            }
            Vector2 at = centre - side * Stand(attack);
            body.position = at;
            body.linearVelocity = Vector2.zero;
            navigator.transform.position = new Vector3(at.x, at.y, navigator.transform.position.z);
            Physics2D.SyncTransforms();
            mover.SetFacing(side);
            for (int swing = 0; swing < RewardMound.HitsToOpen && !mound.IsOpen; swing++)
            {
                yield return TestInput.WaitUntilOrGameTimeout(() => attack.Ready, 2f);
                mover.SetFacing(side);
                Assert.GreaterOrEqual(attack.TryAttack(), 0, "The swing happened");
                yield return null;
            }
            Assert.IsTrue(mound.IsOpen, $"Two swings dig the mound at {centre} away");
        }

        /// <summary>How far from the mound's centre the player stands: a little past the swing's reach, so the swing still
        /// covers the mound but the player never touches what lies under it.</summary>
        private static float Stand(PlayerAttack attack) => attack.Reach + 0.3f * ScreenMath.ContentScale;

        /// <summary>True when the player's circle at a point touches no solid collider but its own.</summary>
        private static bool Clear(Vector2 at, GameObject player)
        {
            foreach (Collider2D c in Physics2D.OverlapCircleAll(at, TimeField.PlayerRadius))
            {
                Rigidbody2D rb = c.attachedRigidbody;
                if (!c.isTrigger && (rb == null || rb.gameObject != player)) return false;
            }
            return true;
        }
    }
}
