using System;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Base for a face's trial puzzle room. When its completion condition is met, Completed fires once
    /// with the room's currency amount (the meta currency and shop that spend it are story 1.12).
    /// Face trials derive from this and call Complete(); the placeholder trigger here completes the
    /// room when something with an Inventory (the player) touches a trigger collider on it.
    /// </summary>
    public class TrialRoom : MonoBehaviour
    {
        [SerializeField, Min(0)] private int currency = 10;

        [Tooltip("Placeholder completion condition: the player touching a trigger collider on this object.")]
        [SerializeField] private bool completeOnPlayerTouch = true;

        public int Currency
        {
            get => currency;
            set => currency = Mathf.Max(0, value);
        }

        public bool CompleteOnPlayerTouch
        {
            get => completeOnPlayerTouch;
            set => completeOnPlayerTouch = value;
        }

        public bool IsComplete { get; private set; }

        /// <summary>Raised at most once per room, with the currency amount it pays out.</summary>
        public event Action<int> Completed;

        /// <summary>Marks the room complete. Only the first call fires Completed; returns true if it did.</summary>
        public bool Complete()
        {
            if (IsComplete) return false;
            IsComplete = true;
            Completed?.Invoke(currency);
            return true;
        }

        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            if (completeOnPlayerTouch && ItemPickup.FindInventory(other) != null) Complete();
        }
    }
}
