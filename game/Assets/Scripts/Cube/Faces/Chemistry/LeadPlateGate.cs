using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A Leadstone* gate: a heavy door across the alcove opening, linked to a LeadPlate on the same screen (in the
    /// horizontal centre lane at PlateLocal, nearer the centre than any Biology button). Swinging at the plate with the
    /// Leadstone* equipped weighs it down (HitsToOpen swings) and opens the door for good; the stone is kept. The door
    /// itself ignores touches and swings.
    /// </summary>
    public class LeadPlateGate : IsotopeGate, ILeadSink
    {
        public static readonly Color DoorColor = new Color(0.3f, 0.32f, 0.38f);

        [SerializeField] private LeadPlate plate;

        public LeadPlate Plate => plate;

        public override IsotopeStage Stage => IsotopeStage.Lead;

        protected override Color ClosedColor => DoorColor;

        /// <summary>Plate distance along the horizontal lane from the screen centre (Biology buttons sit at ButtonLaneX).</summary>
        public const float PlateLaneX = 3.2f;

        public bool WantsLead => !IsOpen;

        /// <summary>
        /// Where the plate of a gate in this slot goes, relative to the screen centre: in the horizontal centre lane,
        /// on the far side from the gate's quadrant, a step toward that quadrant's half (like ButtonGate.ButtonLocal,
        /// but at PlateLaneX instead of ButtonLaneX, so a plate never coincides with a Biology button).
        /// </summary>
        public static Vector2 PlateLocal(Vector2 slotLocal)
        {
            float sx = slotLocal.x >= 0f ? 1f : -1f;
            float sy = slotLocal.y >= 0f ? 1f : -1f;
            return new Vector2(-sx * PlateLaneX, sy * ButtonGate.ButtonLaneY);
        }

        public bool TakeLead(LeadPlate struck, ItemDefinition item) => RegisterHit(item);

        protected override void OnOpened()
        {
            base.OnOpened();
            if (plate != null) plate.ShowLead();
        }

        public void Link(LeadPlate linked)
        {
            plate = linked;
            if (linked != null) linked.Sink = this;
        }

        public static LeadPlateGate Build(GameObject block, ItemDefinition item, Vector2 plateWorld, Transform plateParent,
            int difficulty, bool optional, Material material, int faceSize = CubeSettings.DefaultFaceSize)
        {
            LeadPlateGate gate = Setup<LeadPlateGate>(block, $"Lead Plate Door ({item})", item, PartShape.Square, difficulty,
                optional, faceSize, material);
            gate.Link(LeadPlate.Create(plateParent, plateWorld, item, material));
            ArtCatalog.DressGate(gate, ArtKey.GateLeadDoor);
            return gate;
        }
    }
}
