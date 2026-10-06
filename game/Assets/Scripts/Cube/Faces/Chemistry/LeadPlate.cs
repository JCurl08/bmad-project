using UnityEngine;

namespace Game.Cube
{
    /// <summary>What a lead plate is for: a door to open (LeadPlateGate) or the trial's last step (ChemistryTrial).</summary>
    public interface ILeadSink
    {
        /// <summary>True while the plate takes lead (a closed door; the trial plate always does).</summary>
        bool WantsLead { get; }

        /// <summary>Lead from the isotope with this serial was dropped on the plate (and used up).</summary>
        void TakeLead(int isotopeSerial);
    }

    /// <summary>
    /// A pressure plate that only lead holds down. Stepping onto it while holding a lead isotope drops the lead on
    /// it, and swinging at it with the lead isotope equipped uses it there: either way the lead is used up (the
    /// dispenser can then hand out a fresh isotope) and the plate's sink reacts. A glowing or unstable isotope,
    /// another item or nothing does nothing, and a plate whose sink no longer wants lead (an open door) leaves
    /// the lead in the player's hands.
    /// </summary>
    public class LeadPlate : MonoBehaviour, IAttackReceiver
    {
        public const float Radius = 0.4f;

        /// <summary>The plate art's tint while it waits for lead, and once lead holds it down.</summary>
        public static readonly Color PlateColor = new Color(0.95f, 0.95f, 0.9f);
        public static readonly Color PressedColor = new Color(0.55f, 0.56f, 0.6f);

        [SerializeField] private ItemDefinition item;
        [SerializeField] private SpriteRenderer top;
        [SerializeField] private Material material;

        private SpriteRenderer slug;

        /// <summary>The isotope item whose lead the plate takes.</summary>
        public ItemDefinition Item
        {
            get => item;
            set => item = value;
        }

        public ILeadSink Sink { get; set; }

        /// <summary>Lead dropped on this plate so far.</summary>
        public int LeadTaken { get; private set; }

        private void OnTriggerEnter2D(Collider2D other) => TryDrop(Isotope.On(other));

        private void OnTriggerStay2D(Collider2D other) => TryDrop(Isotope.On(other));

        public bool ReceiveAttack(ItemDefinition swung, GameObject attacker)
        {
            if (swung == null || swung != item) return false;
            return TryDrop(Isotope.On(attacker));
        }

        /// <summary>Drops the isotope's lead on the plate if it is lead and the plate wants it. Returns true if it did.</summary>
        public bool TryDrop(Isotope isotope)
        {
            if (isotope == null || Sink == null || !Sink.WantsLead || !isotope.Is(item, IsotopeStage.Lead)) return false;
            int serial = isotope.Serial;
            if (!isotope.ConsumeLead()) return false;
            LeadTaken++;
            ShowLead();
            Sink.TakeLead(serial);
            return true;
        }

        /// <summary>Shows a lead slug weighing the plate down.</summary>
        public void ShowLead()
        {
            if (top != null) top.color = PressedColor;
            if (slug == null)
                slug = BeakGate.AddSprite(transform, "Lead Slug", PartShape.Circle, Vector2.zero, new Vector2(0.35f, 0.35f),
                    ChemistryIsotope.LeadColor, -1, material);
        }

        /// <summary>Clears the lead slug (the trial plate after a reset).</summary>
        public void ClearLead()
        {
            if (top != null) top.color = PlateColor;
            if (slug != null) Destroy(slug.gameObject);
            slug = null;
        }

        public static LeadPlate Create(Transform parent, Vector2 world, ItemDefinition item, Material material)
        {
            var go = new GameObject($"Lead Plate ({item})");
            go.transform.SetParent(parent, false);
            go.transform.position = world;
            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = Radius;
            BeakGate.AddSprite(go.transform, "Rim", PartShape.Square, Vector2.zero, new Vector2(Radius * 2.2f, Radius * 2.2f),
                ChemistryIsotope.LeadColor, -3, material);
            var plate = go.AddComponent<LeadPlate>();
            plate.item = item;
            plate.material = material;
            plate.top = ArtCatalog.AddSprite(go.transform, "Top", ArtKey.LeadPlate, Vector2.zero, new Vector2(Radius * 2.1f, Radius * 2.1f),
                PlateColor, -2, material);
            return plate;
        }
    }
}
