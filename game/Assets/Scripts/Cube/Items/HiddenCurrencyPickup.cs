using System;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A hidden meta-currency item ("a stray Jumble") in a module's hidden-item slot. When something with an Inventory
    /// (the player) touches its trigger it is collected once: Collected is raised (the RunWallet adds Amount) and the
    /// pickup is removed. It never goes into the inventory and blocks nothing. Since 4.7 it lies under a mound (Guard), dug
    /// away before it can be taken.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class HiddenCurrencyPickup : MonoBehaviour
    {
        public const float Radius = 0.35f * ScreenMath.ContentScale;
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

        /// <summary>The mound over it (4.7: a hidden spot is dug before its Jumbles* can be taken); null for none.</summary>
        public IRewardGuard Guard { get; set; }

        private void OnTriggerEnter2D(Collider2D other) => TryCollect(other);

        private void OnTriggerStay2D(Collider2D other) => TryCollect(other);

        private void TryCollect(Collider2D other)
        {
            if (!IsCollected && (Guard == null || Guard.IsOpen) && ItemPickup.FindInventory(other) != null) Collect();
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
