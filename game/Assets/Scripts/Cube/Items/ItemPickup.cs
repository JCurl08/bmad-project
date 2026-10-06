using System;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// An item lying in the world. When something with an Inventory touches its trigger, the item is
    /// added (once), PickedUp is raised and the pickup is removed. Touching it while already holding the
    /// item has no effect.
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

        /// <summary>Raised once, with the inventory that took the item.</summary>
        public event Action<ItemPickup, Inventory> PickedUp;

        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other) => TryCollect(other);

        private void OnTriggerStay2D(Collider2D other) => TryCollect(other);

        private void TryCollect(Collider2D other)
        {
            if (collected || item == null) return;
            Inventory inventory = FindInventory(other);
            if (inventory == null || !inventory.Add(item)) return;
            collected = true;
            PickedUp?.Invoke(this, inventory);
            Destroy(gameObject);
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
