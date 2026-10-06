using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Curie's isotope dispenser: replaces the Chemistry item's one-shot pickup at the same spot (ItemPlacement's
    /// Chemistry PickupPlacement). It never runs out: touching it while holding no isotope hands out a fresh one
    /// (Isotope restarts it at the glow stage); touching it while holding a glowing or unstable one does nothing,
    /// since the Inventory already has it. Spent lead is taken back and swapped for a fresh isotope, and once a
    /// plate has used up the lead, the next touch refills too.
    /// </summary>
    public class IsotopeDispenser : ItemPickup
    {
        public const float PedestalSize = 0.75f;
        public const float IsotopeSize = 0.4f;

        public static readonly Color PedestalColor = new Color(0.22f, 0.2f, 0.26f);

        [SerializeField] private SpriteRenderer isotopeSprite;

        /// <summary>Isotopes handed out so far.</summary>
        public int Dispensed { get; private set; }

        protected override bool Refills => true;

        /// <summary>Spent lead handed back to the dispenser so far.</summary>
        public int LeadTakenBack { get; private set; }

        /// <summary>
        /// The dispenser takes a held lead isotope back (uses it up), so a fresh one follows: a lead sink that always
        /// exists. A glowing or unstable isotope is left alone (nothing happens).
        /// </summary>
        protected override void BeforeGive(Inventory inventory)
        {
            Isotope isotope = inventory.GetComponent<Isotope>();
            if (isotope != null && isotope.Is(Item, IsotopeStage.Lead) && isotope.ConsumeLead()) LeadTakenBack++;
        }

        protected override void OnGiven(Inventory inventory)
        {
            Dispensed++;
        }

        private void Update()
        {
            // The isotope on the pedestal pulses gently, so the dispenser reads as live.
            if (isotopeSprite == null) return;
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 4f);
            isotopeSprite.transform.localScale = new Vector3(IsotopeSize * pulse, IsotopeSize * pulse, 1f);
        }

        /// <summary>Builds a dispenser of item at a world position (a pedestal with a glowing isotope and a trigger).</summary>
        public static IsotopeDispenser Create(Transform parent, Vector2 position, ItemDefinition item, Material material)
        {
            var go = new GameObject($"Isotope Dispenser ({item})");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = Radius;
            ArtCatalog.AddSprite(go.transform, "Pedestal", ArtKey.Dispenser, Vector2.zero, new Vector2(PedestalSize, PedestalSize),
                Color.white, 4, material);
            BeakGate.AddSprite(go.transform, "Glow", PartShape.Circle, Vector2.zero, new Vector2(0.65f, 0.65f),
                new Color(ChemistryIsotope.GlowColor.r, ChemistryIsotope.GlowColor.g, ChemistryIsotope.GlowColor.b, 0.35f), 5, material);
            var dispenser = go.AddComponent<IsotopeDispenser>();
            dispenser.Item = item;
            dispenser.isotopeSprite = ArtCatalog.AddSprite(go.transform, "Isotope", ArtKeys.Item(item), Vector2.zero,
                new Vector2(IsotopeSize, IsotopeSize), Color.white, 6, material);
            return dispenser;
        }
    }
}
