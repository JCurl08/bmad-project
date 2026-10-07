using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A quake gate: rock pillars across the alcove opening. Swinging the quake Stormcaller* at it shakes them down: the
    /// pillars topple (left lying as rubble) and the way opens. Anything else does nothing.
    /// </summary>
    public class RockPillarGate : PhenomenonGate
    {
        public static readonly Color PillarColor = new Color(0.62f, 0.58f, 0.55f);
        public static readonly Color RubbleColor = new Color(0.7f, 0.65f, 0.6f, 0.6f);

        [SerializeField] private Material rubbleMaterial;

        public override Phenomenon Kind => Phenomenon.Quake;

        protected override Color ClosedColor => PillarColor;

        /// <summary>The toppled pillars left once open (null before).</summary>
        public SpriteRenderer Rubble { get; private set; }

        protected override void OnOpened()
        {
            base.OnOpened();
            Vector2 size = Solid.size;
            bool across = size.x >= size.y;
            Rubble = ArtCatalog.AddSprite(transform, "Toppled Pillars", ArtKey.GateRockPillars, ArtCentre,
                across ? new Vector2(0.45f, size.x * 0.8f) : new Vector2(size.y * 0.8f, 0.45f), RubbleColor, AboveArt, rubbleMaterial, false);
            // Lying down: turned a quarter, across the way it stood.
            Rubble.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        public static RockPillarGate Build(GameObject block, ItemDefinition item, bool optional, Material material)
        {
            RockPillarGate gate = Setup<RockPillarGate>(block, $"Rock Pillar Gate ({item})", item, optional, material);
            gate.rubbleMaterial = material;
            ArtCatalog.DressGate(gate, ArtKey.GateRockPillars);
            return gate;
        }
    }
}
