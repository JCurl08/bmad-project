using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A snow gate: deep water across the alcove opening. Swinging the snow Stormcaller* at it freezes the water into a
    /// crossing of ice the player walks over (the collider goes, the ice stays). Anything else does nothing.
    /// </summary>
    public class FrozenWaterGate : PhenomenonGate
    {
        public static readonly Color WaterColor = new Color(0.25f, 0.5f, 0.95f);
        public static readonly Color IceColor = new Color(0.85f, 0.95f, 1f, 0.9f);

        [SerializeField] private Material iceMaterial;

        public override Phenomenon Kind => Phenomenon.Snow;

        protected override Color ClosedColor => WaterColor;

        /// <summary>The ice crossing left once frozen (null before).</summary>
        public SpriteRenderer Ice { get; private set; }

        protected override void OnOpened()
        {
            base.OnOpened();
            Vector2 size = Solid.size;
            bool across = size.x >= size.y;
            Vector2 ice = across ? new Vector2(size.x, 1f) : new Vector2(1f, size.y);
            // The water itself, frozen white: the walkable crossing.
            Ice = ArtCatalog.AddSprite(transform, "Ice Crossing", ArtKey.GateWater, ArtCentre, ice, IceColor, AboveArt, iceMaterial, true);
        }

        public static FrozenWaterGate Build(GameObject block, ItemDefinition item, bool optional, Material material)
        {
            FrozenWaterGate gate = Setup<FrozenWaterGate>(block, $"Water Gate ({item})", item, optional, material);
            gate.iceMaterial = material;
            ArtCatalog.DressGate(gate, ArtKey.GateWater);
            return gate;
        }
    }
}
