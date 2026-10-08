using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>The Math item, the gap-crosser*: one item with no variants (and, this epic, no upgrades).</summary>
    public static class GapCrosserItem
    {
        /// <summary>The Math item's id: the placeholder's ("math-item"), kept stable.</summary>
        public const string Id = "math-item";

        public const string Name = "Gap-Crosser*";

        public static readonly Color ItemColor = new Color(1f, 0.85f, 0.3f);

        /// <summary>An unsaved runtime gap-crosser* (tests and tools); the catalog builder keeps the real asset.</summary>
        public static ItemDefinition CreateItem() => ItemDefinition.Create(Id, Name, Theme.Math, ItemColor);

        public static bool Is(ItemDefinition item) => item != null && item.Id == Id;
    }

    /// <summary>
    /// A gap gate: a gap across an alcove opening that nobody can step over. Swinging the gap-crosser* at it (its
    /// RequiredItem) lays a crossing over that one gap and opens the gate for good, with the gate-open effect. Any other
    /// item, a bare swing or a touch does nothing. A small mark in the gap-crosser*'s colour says what it wants.
    /// </summary>
    public class GapGate : Gate, IAttackReceiver
    {
        public static readonly Color GapColor = new Color(0.25f, 0.2f, 0.18f);

        /// <summary>How far in front of the opening (toward the lane) the gap's front reaches, and the room other content keeps from it.</summary>
        public const float FrontDepth = 0.6f * ScreenMath.ContentScale;
        public const float FrontRadius = 1.0f * ScreenMath.ContentScale;

        [SerializeField] private Material crossingMaterial;

        protected override bool OpensOnTouch => false;

        protected override Color ClosedColor => GapColor;

        /// <summary>The crossing laid over the gap once open (null before).</summary>
        public SpriteRenderer Crossing { get; private set; }

        /// <summary>The front of a gap gate in a slot (local to the screen centre): just in front of the opening, toward the lane.</summary>
        public static Vector2 FrontLocal(Vector2 slotLocal, Vector2 pocketOffset)
        {
            Vector2 into = pocketOffset.sqrMagnitude > 1e-6f ? pocketOffset.normalized : Vector2.zero;
            return slotLocal - into * FrontDepth;
        }

        public bool ReceiveAttack(ItemDefinition item, GameObject attacker)
        {
            if (IsOpen || item == null || item != RequiredItem) return false;
            return OpenNow();
        }

        protected override void OnOpened()
        {
            Transform mark = transform.Find("Gap Mark");
            if (mark != null) mark.gameObject.SetActive(false);
            // The crossing: a plank laid across the gap, from the lane into the pocket.
            Vector2 size = Solid.size;
            bool across = size.x >= size.y;
            Vector2 plank = across ? new Vector2(0.9f, 1.3f) * ScreenMath.ContentScale : new Vector2(1.3f, 0.9f) * ScreenMath.ContentScale;
            Vector2 at = Visual != null ? (Vector2)Visual.transform.localPosition : Vector2.zero;
            Crossing = ArtCatalog.AddSprite(transform, "Crossing", ArtKey.PlankBridge, at, across ? new Vector2(plank.y, plank.x) : plank,
                Color.white, ArtCatalog.GateArtOrder + 1, crossingMaterial, false);
            Crossing.transform.localRotation = Quaternion.Euler(0f, 0f, across ? 90f : 0f);
        }

        public static GapGate Build(GameObject block, ItemDefinition item, Material material)
        {
            block.name = $"Gap Gate ({item})";
            var gate = block.AddComponent<GapGate>();
            gate.crossingMaterial = material;
            gate.Visual = block.GetComponentInChildren<SpriteRenderer>();
            gate.RequiredItem = item;
            BeakGate.AddSprite(block.transform, "Gap Mark", PartShape.Diamond, Vector2.zero, new Vector2(0.32f, 0.32f) * ScreenMath.ContentScale,
                item != null ? item.PlaceholderColor : GapCrosserItem.ItemColor, -3, material);
            ArtCatalog.DressGate(gate, ArtKey.GateGap);
            return gate;
        }
    }
}
