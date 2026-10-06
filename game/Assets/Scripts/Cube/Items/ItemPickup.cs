using System;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// An item lying in the world. When something with an Inventory touches its trigger, the item is
    /// added (once), PickedUp is raised and the pickup is removed. Touching it while already holding the
    /// item has no effect. A refilling pickup (Refills, e.g. the Chemistry IsotopeDispenser) stays in the world
    /// and gives the item again whenever a toucher no longer holds it.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ItemPickup : MonoBehaviour
    {
        public const float Radius = 0.4f;

        [SerializeField] private ItemDefinition item;

        private bool collected;

        public ItemDefinition Item
        {
            get => item;
            set => item = value;
        }

        public bool Collected => collected;

        /// <summary>
        /// Raised on every give, with the inventory that took the item: once for a one-shot pickup, once per
        /// dispense for a refilling one (Refills).
        /// </summary>
        public event Action<ItemPickup, Inventory> PickedUp;

        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other) => TryCollect(other);

        private void OnTriggerStay2D(Collider2D other) => TryCollect(other);

        /// <summary>True for a pickup that stays and refills (gives the item to anyone not holding it).</summary>
        protected virtual bool Refills => false;

        /// <summary>Hook called when a toucher with an inventory reaches the pickup, before it tries to give the item.</summary>
        protected virtual void BeforeGive(Inventory inventory) { }

        /// <summary>Hook called after every give, before PickedUp is raised.</summary>
        protected virtual void OnGiven(Inventory inventory) { }

        private void TryCollect(Collider2D other)
        {
            if (collected || item == null) return;
            Inventory inventory = FindInventory(other);
            if (inventory == null) return;
            BeforeGive(inventory);
            if (!inventory.Add(item)) return;
            if (!Refills) collected = true;
            OnGiven(inventory);
            PickedUp?.Invoke(this, inventory);
            if (!Refills) Destroy(gameObject);
        }

        /// <summary>The inventory on the collider's body (or its parents), if any.</summary>
        public static Inventory FindInventory(Collider2D other)
        {
            if (other == null) return null;
            if (other.attachedRigidbody != null &&
                other.attachedRigidbody.TryGetComponent(out Inventory onBody)) return onBody;
            return other.GetComponentInParent<Inventory>();
        }
    }
}
