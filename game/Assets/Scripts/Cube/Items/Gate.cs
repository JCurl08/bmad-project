using System;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A gate bound to the item that opens it (gates are data: the required ItemDefinition is all a gate
    /// knows). Solid until something whose Inventory holds that item touches it; then it opens for good:
    /// the collider is switched off, a short GateOpenEffect plays and the visual fades. Face gates (the Biology beak gates, the Chemistry
    /// isotope gates) derive from it: they turn OpensOnTouch off and open through their own mechanic (OpenNow),
    /// or keep it on with their own touch condition (CanOpenFor), keeping the same IsOpen/Opened contract.
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

        /// <summary>
        /// True once ArtCatalog.DressGate has given the visual its kind's art: the closed colour is then only a soft tint
        /// over the art (VisualColor), so the art reads first.
        /// </summary>
        public bool ArtDressed { get; set; }

        /// <summary>The blocking collider (disabled once open).</summary>
        public BoxCollider2D Solid => solid != null ? solid : solid = GetComponent<BoxCollider2D>();

        /// <summary>Raised once, when the gate opens.</summary>
        public event Action<Gate> Opened;

        /// <summary>True for plain gates: touching with the item opens them. Face gates open by their own mechanic.</summary>
        protected virtual bool OpensOnTouch => true;

        protected virtual void Awake()
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
            if (!OpensOnTouch) return false;
            Inventory inventory = ItemPickup.FindInventory(toucher);
            if (inventory == null || requiredItem == null || !CanOpenFor(inventory)) return false;
            Open();
            return true;
        }

        /// <summary>
        /// Whether a toucher's inventory opens this gate (OpensOnTouch gates only): holding the required item by
        /// default. Face gates that open on touch under a condition (the Chemistry dark-room gate: a glowing
        /// isotope) override it.
        /// </summary>
        protected virtual bool CanOpenFor(Inventory inventory) => inventory.Has(requiredItem);

        /// <summary>Opens the gate for good (for face gates' own mechanics). Returns false if it was already open.</summary>
        protected bool OpenNow()
        {
            if (IsOpen) return false;
            Open();
            return true;
        }

        private void Open()
        {
            bool effect = PlaysOpenEffect; // read before OnOpened changes the gate's own state
            IsOpen = true;
            Solid.enabled = false;
            ApplyVisual();
            OnOpened();
            Opened?.Invoke(this);
            if (effect) GateOpenEffect.Play(this);
        }

        /// <summary>
        /// Whether opening for good plays the GateOpenEffect: true unless this opening sequence already played it (a timed
        /// door that swung ajar plays it then, not again when it latches).
        /// </summary>
        protected virtual bool PlaysOpenEffect => true;

        /// <summary>Hook for subclasses, called once when the gate opens (before Opened is raised).</summary>
        protected virtual void OnOpened() { }

        /// <summary>The visual's colour while closed (the item's placeholder colour by default).</summary>
        protected virtual Color ClosedColor => requiredItem != null ? requiredItem.PlaceholderColor : Color.grey;

        protected void ApplyVisual()
        {
            if (visual == null) return;
            Color color = VisualColor(ClosedColor);
            if (IsOpen) color.a = OpenAlpha;
            visual.color = color;
        }

        /// <summary>The colour the visual shows for a gate colour: the colour itself on a placeholder, a soft tint over art.</summary>
        protected Color VisualColor(Color color)
        {
            if (!ArtDressed) return color;
            Color tint = Color.Lerp(Color.white, color, 0.35f);
            tint.a = color.a;
            return tint;
        }

        /// <summary>Re-applies the visual's colour (after the art dressing changed how it is tinted).</summary>
        public void RefreshVisual() => ApplyVisual();
    }
}
