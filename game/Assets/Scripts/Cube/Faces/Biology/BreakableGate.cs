using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A thick-beak gate: a rock or a pot wedged in the alcove opening. Attacking it with the thick beak
    /// equipped breaks it (after HitsToOpen hits); other items, a bare swing or touching do nothing.
    /// </summary>
    public class BreakableGate : BeakGate, IAttackReceiver
    {
        public static readonly Color RockColor = new Color(0.55f, 0.53f, 0.5f);
        public static readonly Color PotColor = new Color(0.78f, 0.42f, 0.25f);

        [SerializeField] private BreakableLook look;

        public BreakableLook Look
        {
            get => look;
            set
            {
                look = value;
                ApplyVisual();
            }
        }

        public override BeakGateKind Kind => BeakGateKind.Breakable;

        protected override Color ClosedColor => look == BreakableLook.Pot ? PotColor : RockColor;

        public bool ReceiveAttack(ItemDefinition item, GameObject attacker) => RegisterHit(item);

        /// <summary>
        /// Turns a placeholder block (root with a BoxCollider2D and a "Visual" child sprite) into a breakable
        /// gate opened by item. The beak's colour shows as a small mark so the player can tell which beak it wants.
        /// </summary>
        public static BreakableGate Build(GameObject block, ItemDefinition item, BreakableLook look, int difficulty,
            bool optional, Material material)
        {
            block.name = $"{look} Gate ({item})";
            var gate = block.AddComponent<BreakableGate>();
            gate.look = look;
            gate.Difficulty = difficulty;
            gate.Optional = optional;
            gate.Visual = block.GetComponentInChildren<SpriteRenderer>();
            gate.RequiredItem = item;
            Color mark = item != null ? item.PlaceholderColor : Color.white;
            AddSprite(block.transform, "Beak Mark", PartShape.Triangle, Vector2.zero, new Vector2(0.35f, 0.35f), mark, -3, material);
            ArtCatalog.DressGate(gate, look == BreakableLook.Pot ? ArtKey.GatePot : ArtKey.GateRock);
            return gate;
        }
    }
}
