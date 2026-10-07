using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A rain gate: a wall of fire across the alcove opening. Swinging the rain Stormcaller* at it calls a downpour that
    /// douses it: a puff of steam is left and the way opens. Anything else does nothing.
    /// </summary>
    public class FireWallGate : PhenomenonGate
    {
        public static readonly Color FireColor = new Color(1f, 0.45f, 0.15f);
        public static readonly Color SteamColor = new Color(0.85f, 0.9f, 0.95f, 0.45f);

        [SerializeField] private Material steamMaterial;

        public override Phenomenon Kind => Phenomenon.Rain;

        protected override Color ClosedColor => FireColor;

        /// <summary>The steam left once doused (null before).</summary>
        public SpriteRenderer Steam { get; private set; }

        protected override void OnOpened()
        {
            base.OnOpened();
            Vector2 size = Solid.size;
            float w = Mathf.Max(size.x, size.y);
            Steam = BeakGate.AddSprite(transform, "Steam", PartShape.Circle, ArtCentre, new Vector2(w * 0.7f, w * 0.45f), SteamColor,
                AboveArt, steamMaterial);
        }

        public static FireWallGate Build(GameObject block, ItemDefinition item, bool optional, Material material)
        {
            FireWallGate gate = Setup<FireWallGate>(block, $"Fire Wall Gate ({item})", item, optional, material);
            gate.steamMaterial = material;
            ArtCatalog.DressGate(gate, ArtKey.GateFireWall);
            return gate;
        }
    }
}
