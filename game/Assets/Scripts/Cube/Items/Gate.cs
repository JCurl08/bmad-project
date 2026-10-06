using System;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A gate bound to the item that opens it (gates are data: the required ItemDefinition is all a gate
    /// knows). Solid until something whose Inventory holds that item touches it; then it opens for good:
    /// the collider is switched off and the visual fades.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class Gate : MonoBehaviour
    {
        private const float OpenAlpha = 0.2f;

        [SerializeField] private ItemDefinition requiredItem;
        [SerializeField] private SpriteRenderer visual;

        private BoxCollider2D solid;

        public ItemDefinition RequiredItem
        {
            get => requiredItem;
            set
            {
                requiredItem = value;
                ApplyVisual();
            }
        }

        public SpriteRenderer Visual
        {
            get => visual;
            set
            {
                visual = value;
                ApplyVisual();
            }
        }

        public bool IsOpen { get; private set; }

        /// <summary>The blocking collider (disabled once open).</summary>
        public BoxCollider2D Solid => solid != null ? solid : solid = GetComponent<BoxCollider2D>();

        /// <summary>Raised once, when the gate opens.</summary>
        public event Action<Gate> Opened;

        private void Awake()
        {
            solid = GetComponent<BoxCollider2D>();
            ApplyVisual();
        }

        private void OnCollisionEnter2D(Collision2D collision) => TryOpen(collision.collider);

        private void OnCollisionStay2D(Collision2D collision) => TryOpen(collision.collider);

        /// <summary>Opens the gate if the toucher holds the required item. Returns true if it is open afterwards.</summary>
        public bool TryOpen(Collider2D toucher)
        {
            if (IsOpen) return true;
            Inventory inventory = ItemPickup.FindInventory(toucher);
            if (inventory == null || requiredItem == null || !inventory.Has(requiredItem)) return false;
            Open();
            return true;
        }

        private void Open()
        {
            IsOpen = true;
            Solid.enabled = false;
            ApplyVisual();
            Opened?.Invoke(this);
        }

        private void ApplyVisual()
        {
            if (visual == null) return;
            Color color = requiredItem != null ? requiredItem.PlaceholderColor : Color.grey;
            if (IsOpen) color.a = OpenAlpha;
            visual.color = color;
        }
    }
}
