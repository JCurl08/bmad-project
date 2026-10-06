using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// An unstable gate: a cracked wall across the alcove opening. Swinging at it with the unstable isotope
    /// equipped blasts it open; a glowing or lead isotope, another item, a bare swing or a touch does nothing.
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

        public bool ReceiveAttack(ItemDefinition item, GameObject attacker)
        {
            if (IsOpen || item == null || item != RequiredItem) return false;
            return Matches(Isotope.On(attacker)) && OpenNow();
        }

        protected override void OnOpened()
        {
            base.OnOpened();
            Transform crack = transform.Find("Crack");
            if (crack != null) crack.gameObject.SetActive(false);
            Color scorch = ChemistryIsotope.UnstableColor;
            scorch.a = 0.35f;
            Scorch = BeakGate.AddSprite(transform, "Blast Scorch", PartShape.Diamond, Vector2.zero, Solid.size * 0.9f, scorch, -3,
                blastMaterial);
        }

        public static CrackedWallGate Build(GameObject block, ItemDefinition item, Material material)
        {
            CrackedWallGate gate = Setup<CrackedWallGate>(block, $"Cracked Wall Gate ({item})", item, PartShape.Triangle, material);
            gate.blastMaterial = material;
            Vector2 size = gate.Solid.size;
            Vector2 crack = size.x >= size.y ? new Vector2(size.x * 0.8f, 0.08f) : new Vector2(0.08f, size.y * 0.8f);
            BeakGate.AddSprite(block.transform, "Crack", PartShape.Square, Vector2.zero, crack, CrackColor, -3, material);
            return gate;
        }
    }
}
