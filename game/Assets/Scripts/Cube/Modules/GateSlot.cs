using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Where a gate may be placed. The slot sits in the opening of a small alcove (walled on the other
    /// three sides, built with the module); a gate placed here closes that alcove only, never a screen
    /// exit. The guarded point (where a pickup "behind" the gate goes) is the alcove's pocket centre.
    /// Gates are data bound to the item that opens them (see ItemPlacement and Gate).
    /// </summary>
    public class GateSlot : ModuleSlot
    {
        /// <summary>Width of a gate (the alcove opening between its side walls).</summary>
        public const float GateWidth = 2f;

        /// <summary>Thickness of a gate and of the alcove walls.</summary>
        public const float GateThickness = 0.2f;

        [Tooltip("Direction the alcove opens toward (the screen interior).")]
        [SerializeField] private Facing opening = Facing.South;

        [Tooltip("Offset from this slot (the opening) to the pocket centre, in module-local units.")]
        [SerializeField] private Vector2 pocketOffset = new Vector2(0f, 1f);

        public Facing Opening
        {
            get => opening;
            set => opening = value;
        }

        public Vector2 PocketOffset
        {
            get => pocketOffset;
            set => pocketOffset = value;
        }

        /// <summary>World position of the guarded pocket centre behind this slot.</summary>
        public Vector2 PocketCentre => (Vector2)transform.position + pocketOffset;

        public override string Label => "Gate";
        public override Color MarkerColor => new Color(1f, 0.55f, 0.1f);
    }
}
