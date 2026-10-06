using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A thin-beak gate: a door in the alcove opening, linked to a distant BeakButton elsewhere on the same
    /// screen. Pecking the button with the thin beak (whose long reach gets there from afar) opens the door
    /// after HitsToOpen presses. The door itself ignores touches and swings.
    /// </summary>
    public class ButtonGate : BeakGate
    {
        public static readonly Color DoorColor = new Color(0.35f, 0.3f, 0.45f);

        /// <summary>Button position relative to the screen centre: the horizontal lane, across from the gate's quadrant.</summary>
        public const float ButtonLaneX = 5f;
        public const float ButtonLaneY = 0.6f;

        [SerializeField] private BeakButton button;

        public BeakButton Button => button;

        public override BeakGateKind Kind => BeakGateKind.Button;

        protected override Color ClosedColor => DoorColor;

        /// <summary>A press from the linked button. Returns true if it counted.</summary>
        public bool Press(ItemDefinition item) => RegisterHit(item);

        public void Link(BeakButton linked)
        {
            button = linked;
            if (linked != null) linked.Door = this;
        }

        protected override void OnOpened()
        {
            base.OnOpened();
            if (button != null) button.ShowPressed();
        }

        /// <summary>
        /// Where the button of a gate in this slot goes, relative to the screen centre: in the horizontal centre
        /// lane (always clear and walkable), on the far side from the gate's quadrant, a step toward that quadrant's
        /// half, so two gates on one screen never share a button spot.
        /// </summary>
        public static Vector2 ButtonLocal(Vector2 slotLocal)
        {
            float sx = slotLocal.x >= 0f ? 1f : -1f;
            float sy = slotLocal.y >= 0f ? 1f : -1f;
            return new Vector2(-sx * ButtonLaneX, sy * ButtonLaneY);
        }

        /// <summary>Turns a placeholder block into a button door opened by item, with its button at buttonWorld.</summary>
        public static ButtonGate Build(GameObject block, ItemDefinition item, Vector2 buttonWorld, Transform buttonParent,
            int difficulty, bool optional, Material material)
        {
            block.name = $"Button Door ({item})";
            var gate = block.AddComponent<ButtonGate>();
            gate.Difficulty = difficulty;
            gate.Optional = optional;
            gate.Visual = block.GetComponentInChildren<SpriteRenderer>();
            gate.RequiredItem = item;
            Color mark = item != null ? item.PlaceholderColor : Color.white;
            AddSprite(block.transform, "Beak Mark", PartShape.Circle, Vector2.zero, new Vector2(0.3f, 0.3f), mark, -3, material);
            gate.Link(BeakButton.Create(buttonParent, buttonWorld, item, material));
            return gate;
        }
    }
}
