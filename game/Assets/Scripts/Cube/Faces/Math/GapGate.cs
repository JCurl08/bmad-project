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
    /// A gap gate: a gap across an alcove opening that nobody can step over. 4.25: swinging the gap-crosser* at it (its
    /// RequiredItem) hops the player over the gap (GapHop), into the alcove or back out of it; nothing is built and the gate
    /// never opens, so every crossing needs the item. Any other item, a bare swing or a touch does nothing. A small mark in
    /// the gap-crosser*'s colour says what it wants. (A chest key on a bonus alcove still opens it for good: ForceOpen.)
    /// </summary>
    public class GapGate : Gate, IAttackReceiver
    {
        public static readonly Color GapColor = new Color(0.25f, 0.2f, 0.18f);

        /// <summary>How far in front of the opening (toward the lane) the gap's front reaches, and the room other content keeps from it.</summary>
        public const float FrontDepth = 0.6f * ScreenMath.ContentScale;
        public const float FrontRadius = 1.0f * ScreenMath.ContentScale;

        protected override bool OpensOnTouch => false;

        protected override Color ClosedColor => GapColor;

        /// <summary>Times the gap-crosser* hopped the player over this gap (4.25).</summary>
        public int Hops { get; private set; }

        /// <summary>The way across the gap (unit): along the alcove's depth, across the gate's thin side.</summary>
        public Vector2 Axis => Solid.bounds.size.x >= Solid.bounds.size.y ? Vector2.up : Vector2.right;

        /// <summary>The gap's half size along its axis (world units).</summary>
        public float HalfDepth => Mathf.Min(Solid.bounds.size.x, Solid.bounds.size.y) / 2f;

        /// <summary>The front of a gap gate in a slot (local to the screen centre): just in front of the opening, toward the lane.</summary>
        public static Vector2 FrontLocal(Vector2 slotLocal, Vector2 pocketOffset)
        {
            Vector2 into = pocketOffset.sqrMagnitude > 1e-6f ? pocketOffset.normalized : Vector2.zero;
            return slotLocal - into * FrontDepth;
        }

        public bool ReceiveAttack(ItemDefinition item, GameObject attacker)
        {
            if (IsOpen || item == null || item != RequiredItem || attacker == null) return false;
            Vector2 centre = Solid.bounds.center;
            if (!GapHop.TryHop(attacker, Solid, centre, Axis, HalfDepth)) return false;
            Hops++;
            return true;
        }

        protected override void OnOpened()
        {
            Transform mark = transform.Find("Gap Mark");
            if (mark != null) mark.gameObject.SetActive(false);
        }

        public static GapGate Build(GameObject block, ItemDefinition item, Material material)
        {
            block.name = $"Gap Gate ({item})";
            var gate = block.AddComponent<GapGate>();
            gate.Visual = block.GetComponentInChildren<SpriteRenderer>();
            gate.RequiredItem = item;
            BeakGate.AddSprite(block.transform, "Gap Mark", PartShape.Diamond, Vector2.zero, new Vector2(0.32f, 0.32f) * ScreenMath.ContentScale,
                item != null ? item.PlaceholderColor : GapCrosserItem.ItemColor, -3, material);
            ArtCatalog.DressGate(gate, ArtKey.GateGap);
            return gate;
        }
    }
}
