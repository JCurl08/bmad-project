using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A thin-beak gate: a bramble in the alcove opening, linked to a PollinationFlower on an adjacent screen of
    /// the same face. Pollinating the flower with the thin beak (HitsToOpen times) grows a vine bridge here: the
    /// bramble opens and a leafy bridge spans the opening.
    /// </summary>
    public class FlowerVineGate : BeakGate
    {
        public static readonly Color BrambleColor = new Color(0.3f, 0.22f, 0.12f);
        public static readonly Color VineColor = new Color(0.3f, 0.85f, 0.3f);

        /// <summary>Flower position relative to its screen centre: the vertical centre lane, upper half.</summary>
        public static readonly Vector2 FlowerLocal = new Vector2(0f, 2.6f);

        [SerializeField] private PollinationFlower flower;
        [SerializeField] private Material bridgeMaterial;

        public PollinationFlower Flower => flower;

        public override BeakGateKind Kind => BeakGateKind.FlowerVine;

        protected override Color ClosedColor => BrambleColor;

        /// <summary>The vine bridge shown once open (null before).</summary>
        public SpriteRenderer Bridge { get; private set; }

        /// <summary>Pollination from the linked flower. Returns true if it counted.</summary>
        public bool Pollinate(ItemDefinition item) => RegisterHit(item);

        public void Link(PollinationFlower linked)
        {
            flower = linked;
            if (linked != null) linked.Vine = this;
        }

        protected override void OnOpened()
        {
            base.OnOpened();
            Vector2 size = Solid.size;
            Vector2 bridgeSize = size.x >= size.y ? new Vector2(size.x, 0.6f) : new Vector2(0.6f, size.y);
            Bridge = AddSprite(transform, "Vine Bridge", PartShape.Square, Vector2.zero, bridgeSize, VineColor, -3, bridgeMaterial);
            if (flower != null) flower.ShowBloom();
        }

        public static FlowerVineGate Build(GameObject block, ItemDefinition item, Vector2 flowerWorld, Transform flowerParent,
            int difficulty, bool optional, Material material)
        {
            block.name = $"Vine Gate ({item})";
            var gate = block.AddComponent<FlowerVineGate>();
            gate.bridgeMaterial = material;
            gate.Difficulty = difficulty;
            gate.Optional = optional;
            gate.Visual = block.GetComponentInChildren<SpriteRenderer>();
            gate.RequiredItem = item;
            Color mark = item != null ? item.PlaceholderColor : Color.white;
            AddSprite(block.transform, "Beak Mark", PartShape.Diamond, Vector2.zero, new Vector2(0.35f, 0.35f), mark, -3, material);
            gate.Link(PollinationFlower.Create(flowerParent, flowerWorld, item, material));
            return gate;
        }
    }
}
