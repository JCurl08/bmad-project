using System;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A hidden meta-currency item ("a stray Jumble") in a module's hidden-item slot. When something with an Inventory
    /// (the player) touches its trigger it is collected once: Collected is raised (the RunWallet adds Amount) and the
    /// pickup is removed. It never goes into the inventory and blocks nothing.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class HiddenCurrencyPickup : MonoBehaviour
    {
        public const float Radius = 0.35f;
        public static readonly Color PlaceholderColor = new Color(1f, 0.8f, 0.25f);

        [SerializeField, Min(0)] private int amount = HiddenCurrencyPlacement.Amount;
        [SerializeField] private int index;

        /// <summary>Raised once, when collected.</summary>
        public event Action<HiddenCurrencyPickup> Collected;

        public int Amount
        {
            get => amount;
            set => amount = Mathf.Max(0, value);
        }

        /// <summary>Its index in the run's HiddenCurrencyPlacement (a stable id within the run).</summary>
        public int Index
        {
            get => index;
            set => index = value;
        }

        public bool IsCollected { get; private set; }

        private void OnTriggerEnter2D(Collider2D other) => TryCollect(other);

        private void OnTriggerStay2D(Collider2D other) => TryCollect(other);

        private void TryCollect(Collider2D other)
        {
            if (!IsCollected && ItemPickup.FindInventory(other) != null) Collect();
        }

        /// <summary>Collects it (first call only). Returns true if this call collected it.</summary>
        public bool Collect()
        {
            if (IsCollected) return false;
            IsCollected = true;
            Collected?.Invoke(this);
            Destroy(gameObject);
            return true;
        }
    }
}
