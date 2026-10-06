using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Runtime behaviour of the item framework, on objects built in the test (far from anything a scene
    /// may hold): pickups add once, gates stay solid without their item and open with it, one-way gates
    /// pass one way and block the other, and a trial room fires its completion event once.
    /// </summary>
    public class ItemComponentTests
    {
        private static readonly Vector2 Origin = new Vector2(5000f, 5000f);
        private const float Speed = 5f;

        private readonly List<Object> created = new List<Object>();
        private ItemDefinition item;

        [SetUp]
        public void SetUp()
        {
            item = Track(ItemDefinition.Create("test-item", "Test Item", Theme.Biology, Color.green));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created)
                if (o != null) Object.Destroy(o);
            created.Clear();
        }

        private T Track<T>(T o) where T : Object
        {
            created.Add(o);
            return o;
        }

        private Rigidbody2D MakePlayer(Vector2 position, out Inventory inventory)
        {
            var go = Track(new GameObject("Test Player"));
            go.transform.position = position;
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            go.AddComponent<CircleCollider2D>().radius = 0.4f;
            inventory = go.AddComponent<Inventory>();
            return body;
        }

        private ItemPickup MakePickup(Vector2 position)
        {
            var go = Track(new GameObject("Test Pickup"));
            go.transform.position = position;
            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = ItemPickup.Radius;
            var pickup = go.AddComponent<ItemPickup>();
            pickup.Item = item;
            return pickup;
        }

        /// <summary>Drives the body at a constant velocity for a while (physics steps).</summary>
        private static IEnumerator Push(Rigidbody2D body, Vector2 velocity, float seconds)
        {
            var wait = new WaitForFixedUpdate();
            int steps = Mathf.CeilToInt(seconds / Time.fixedDeltaTime);
            for (int i = 0; i < steps; i++)
            {
                body.linearVelocity = velocity;
                yield return wait;
            }
            body.linearVelocity = Vector2.zero;
            yield return wait;
        }

        [UnityTest]
        public IEnumerator Pickup_AddsItemOnce_RemovesPickup_SecondPickupOfOwnedItemHasNoEffect()
        {
            ItemPickup first = MakePickup(Origin);
            ItemPickup second = MakePickup(Origin + new Vector2(2f, 0f));
            Rigidbody2D body = MakePlayer(Origin + new Vector2(-3f, 0f), out Inventory inventory);
            int added = 0, firstEvents = 0, secondEvents = 0;
            inventory.ItemAdded += _ => added++;
            first.PickedUp += (_, __) => firstEvents++;
            second.PickedUp += (_, __) => secondEvents++;

            yield return Push(body, new Vector2(Speed, 0f), 1.5f);

            Assert.Greater(body.position.x, (Origin + new Vector2(2f, 0f)).x, "Player should have walked over both pickups");
            Assert.IsTrue(inventory.Has(item));
            Assert.AreEqual(1, inventory.Items.Count);
            Assert.AreEqual(1, added, "ItemAdded must be raised once");
            Assert.AreEqual(1, firstEvents, "PickedUp must be raised once");
            Assert.IsTrue(first == null, "The collected pickup must be removed");
            Assert.IsTrue(second != null, "A pickup of an item already held has no effect");
            Assert.IsFalse(second.Collected);
            Assert.AreEqual(0, secondEvents);
            Assert.IsFalse(inventory.Add(item), "Adding an owned item again does nothing");
        }

        private Gate MakeGate(Vector2 position)
        {
            var go = Track(new GameObject("Test Gate"));
            go.transform.position = position;
            var gate = go.AddComponent<Gate>();
            gate.Solid.size = new Vector2(0.2f, 2f);
            gate.RequiredItem = item;
            return gate;
        }

        [UnityTest]
        public IEnumerator Gate_WithoutItem_StaysSolid()
        {
            Gate gate = MakeGate(Origin);
            Rigidbody2D body = MakePlayer(Origin + new Vector2(-2f, 0f), out _);

            yield return Push(body, new Vector2(Speed, 0f), 1.5f);

            Assert.IsFalse(gate.IsOpen);
            Assert.IsTrue(gate.Solid.enabled);
            Assert.Less(body.position.x, Origin.x - 0.1f - 0.4f + 0.05f, "Player passed a closed gate");
            Assert.Greater(body.position.x, Origin.x - 1f, "Player never reached the gate");
        }

        [UnityTest]
        public IEnumerator Gate_WithItem_OpensOnTouch_AndLetsThePlayerThrough()
        {
            Gate gate = MakeGate(Origin);
            Rigidbody2D body = MakePlayer(Origin + new Vector2(-2f, 0f), out Inventory inventory);
            inventory.Add(item);
            int opened = 0;
            gate.Opened += _ => opened++;

            yield return Push(body, new Vector2(Speed, 0f), 1.5f);

            Assert.IsTrue(gate.IsOpen);
            Assert.IsFalse(gate.Solid.enabled);
            Assert.AreEqual(1, opened);
            Assert.Greater(body.position.x, Origin.x + 1f, "Player did not get through the open gate");
        }

        private OneWayGate MakeOneWay(Vector2 position, Facing allowed)
        {
            var go = Track(new GameObject("Test One-Way Gate"));
            go.transform.position = position;
            var gate = go.AddComponent<OneWayGate>();
            gate.Width = 2f;
            gate.AllowedDirection = allowed;
            return gate;
        }

        [UnityTest]
        public IEnumerator OneWayGate_Forward_Crosses()
        {
            MakeOneWay(Origin, Facing.East);
            Rigidbody2D body = MakePlayer(Origin + new Vector2(-2f, 0f), out _);

            yield return Push(body, new Vector2(Speed, 0f), 1.2f);

            Assert.Greater(body.position.x, Origin.x + 1f, "Crossing in the allowed direction must succeed");
        }

        [UnityTest]
        public IEnumerator OneWayGate_Back_IsBlocked_FromTheFarSide_AndAfterCrossing()
        {
            MakeOneWay(Origin, Facing.North);
            Rigidbody2D body = MakePlayer(Origin + new Vector2(0f, 2f), out _);

            // Pushing against it from the far side.
            yield return Push(body, new Vector2(0f, -Speed), 1.2f);
            Assert.Greater(body.position.y, Origin.y + 0.15f + 0.4f - 0.05f, "Pushing back through a one-way gate must be blocked");
            Assert.Less(body.position.y, Origin.y + 1f, "Player never reached the gate");

            // Committed: cross from the near side, then try to come back.
            body.position = Origin + new Vector2(0f, -2f);
            yield return Push(body, new Vector2(0f, Speed), 1.2f);
            Assert.Greater(body.position.y, Origin.y + 1f, "Crossing northward must succeed");
            yield return Push(body, new Vector2(0f, -Speed), 1.2f);
            Assert.Greater(body.position.y, Origin.y, "Coming back after crossing must be blocked");
        }

        [UnityTest]
        public IEnumerator TrialRoom_FiresCompletedOnce_WithItsCurrency()
        {
            var go = Track(new GameObject("Test Trial Room"));
            go.transform.position = Origin;
            var zone = go.AddComponent<BoxCollider2D>();
            zone.isTrigger = true;
            zone.size = new Vector2(1f, 1f);
            var trial = go.AddComponent<TrialRoom>();
            trial.Currency = 25;
            var payouts = new List<int>();
            trial.Completed += amount => payouts.Add(amount);

            Rigidbody2D body = MakePlayer(Origin + new Vector2(-3f, 0f), out _);
            yield return Push(body, new Vector2(Speed, 0f), 1.2f);   // in and out
            yield return Push(body, new Vector2(-Speed, 0f), 1.2f);  // and back through again

            Assert.IsTrue(trial.IsComplete);
            CollectionAssert.AreEqual(new[] { 25 }, payouts, "Completed must fire exactly once with the room's currency");
            Assert.IsFalse(trial.Complete(), "A second trigger does not fire again");
            Assert.AreEqual(1, payouts.Count);
        }
    }
}
