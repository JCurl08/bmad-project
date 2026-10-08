using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A tornado gate: a debris pile across the alcove opening. Swinging the tornado Stormcaller* at it whips up a
    /// whirlwind that blows the debris away (a swirl is left behind) and the way opens. Anything else does nothing.
    /// </summary>
    public class DebrisGate : PhenomenonGate
    {
        public static readonly Color DebrisColor = new Color(0.55f, 0.42f, 0.3f);
        public static readonly Color SwirlColor = new Color(0.8f, 0.75f, 0.95f, 0.5f);

        [SerializeField] private Material swirlMaterial;

        public override Phenomenon Kind => Phenomenon.Tornado;

        protected override Color ClosedColor => DebrisColor;

        /// <summary>The swirl left once the debris is blown away (null before).</summary>
        public SpriteRenderer Swirl { get; private set; }

        protected override void OnOpened()
        {
            base.OnOpened();
            if (Visual != null) Visual.enabled = false; // the debris is gone, not just faded
            Swirl = BeakGate.AddSprite(transform, "Swirl", PartShape.Diamond, ArtCentre, new Vector2(0.7f, 0.7f) * ScreenMath.ContentScale, SwirlColor, AboveArt,
                swirlMaterial);
            Swirl.transform.localRotation = Quaternion.Euler(0f, 0f, 20f);
        }

        public static DebrisGate Build(GameObject block, ItemDefinition item, bool optional, Material material)
        {
            DebrisGate gate = Setup<DebrisGate>(block, $"Debris Gate ({item})", item, optional, material);
            gate.swirlMaterial = material;
            ArtCatalog.DressGate(gate, ArtKey.GateDebris);
            return gate;
        }
    }
}
