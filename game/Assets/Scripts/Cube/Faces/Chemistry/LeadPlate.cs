using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>What a pressure plate opens: its LeadPlateGate door (the trial's plates are ChemistryTrialPieces).</summary>
    public interface ILeadSink
    {
        /// <summary>True while the plate reacts to the Leadstone* (its door is still closed).</summary>
        bool WantsLead { get; }

        /// <summary>The plate was struck with its Leadstone*. Returns true if the sink counted it.</summary>
        bool TakeLead(LeadPlate plate, ItemDefinition item);
    }

    /// <summary>
    /// A pressure plate only the Leadstone* weighs down: swinging at it with the plate's Leadstone* equipped presses it
    /// and its sink reacts. The stone is not used up. Stepping on it, a bare swing or any other item (another stone
    /// included) does nothing, and a plate whose sink no longer wants it (an open door) ignores swings.
    /// </summary>
    public class LeadPlate : MonoBehaviour, IAttackReceiver
    {
        public const float Radius = 0.4f * ScreenMath.ContentScale;

        /// <summary>The plate art's tint while it waits, and once the stone has weighed it down.</summary>
        public static readonly Color PlateColor = new Color(0.95f, 0.95f, 0.9f);
        public static readonly Color PressedColor = new Color(0.55f, 0.56f, 0.6f);

        [SerializeField] private ItemDefinition item;
        [SerializeField] private SpriteRenderer top;
        [SerializeField] private Material material;

        private SpriteRenderer slug;

        /// <summary>The Leadstone* item that presses the plate.</summary>
        public ItemDefinition Item
        {
            get => item;
            set => item = value;
        }

        public ILeadSink Sink { get; set; }

        /// <summary>Counted presses so far.</summary>
        public int Presses { get; private set; }

        /// <summary>True while the pressed look shows.</summary>
        public bool Pressed => slug != null;

        public bool ReceiveAttack(ItemDefinition swung, GameObject attacker)
        {
            if (swung == null || swung != item || !ChemistryIsotope.Is(swung, IsotopeStage.Lead)) return false;
            if (Sink == null || !Sink.WantsLead || !Sink.TakeLead(this, swung)) return false;
            Presses++;
            return true;
        }

        /// <summary>Shows the plate weighed down (a grey slug on it).</summary>
        public void ShowLead()
        {
            if (top != null) top.color = PressedColor;
            if (slug == null)
                slug = BeakGate.AddSprite(transform, "Lead Slug", PartShape.Circle, Vector2.zero, new Vector2(0.35f, 0.35f) * ScreenMath.ContentScale,
                    ChemistryIsotope.LeadColor, -1, material);
        }

        public static LeadPlate Create(Transform parent, Vector2 world, ItemDefinition item, Material material)
        {
            var go = new GameObject($"Lead Plate ({item})");
            go.transform.SetParent(parent, false);
            go.transform.position = world;
            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true; // swings find it; walking over it does nothing
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
