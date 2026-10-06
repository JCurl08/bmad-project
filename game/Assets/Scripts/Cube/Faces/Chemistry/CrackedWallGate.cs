using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A Wildstone* gate: a cracked wall across the alcove opening. Swinging at it with the Wildstone* equipped blasts it
    /// open (after HitsToOpen swings); any other item, a bare swing or a touch does nothing.
    /// </summary>
    public class CrackedWallGate : IsotopeGate, IAttackReceiver
    {
        public static readonly Color WallColor = new Color(0.5f, 0.42f, 0.5f);
        public static readonly Color CrackColor = new Color(0.12f, 0.08f, 0.12f);

        [SerializeField] private Material blastMaterial;

        public override IsotopeStage Stage => IsotopeStage.Unstable;

        protected override Color ClosedColor => WallColor;

        /// <summary>The blast scorch left once open (null before).</summary>
        public SpriteRenderer Scorch { get; private set; }

        public bool ReceiveAttack(ItemDefinition item, GameObject attacker) => RegisterHit(item);

        protected override void OnOpened()
        {
            base.OnOpened();
            Transform crack = transform.Find("Crack");
            if (crack != null) crack.gameObject.SetActive(false);
            Color scorch = ChemistryIsotope.UnstableColor;
            scorch.a = 0.35f;
            Vector2 at = Visual != null ? (Vector2)Visual.transform.localPosition : Vector2.zero;
            int order = ArtDressed ? ArtCatalog.GateArtOrder + 1 : -3;
            Scorch = BeakGate.AddSprite(transform, "Blast Scorch", PartShape.Diamond, at, Solid.size * 0.9f, scorch, order, blastMaterial);
        }

        public static CrackedWallGate Build(GameObject block, ItemDefinition item, int difficulty, bool optional, Material material,
            int faceSize = CubeSettings.DefaultFaceSize)
        {
            CrackedWallGate gate = Setup<CrackedWallGate>(block, $"Cracked Wall Gate ({item})", item, PartShape.Triangle, difficulty,
                optional, faceSize, material);
            gate.blastMaterial = material;
            Vector2 size = gate.Solid.size;
            Vector2 crack = size.x >= size.y ? new Vector2(size.x * 0.8f, 0.08f) : new Vector2(0.08f, size.y * 0.8f);
            BeakGate.AddSprite(block.transform, "Crack", PartShape.Square, Vector2.zero, crack, CrackColor, -3, material);
            ArtCatalog.DressGate(gate, ArtKey.GateCrackedWall);
            return gate;
        }
    }
}
