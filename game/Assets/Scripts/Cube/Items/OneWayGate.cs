using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A one-way gate (the reusable "commitment" mechanic: e.g. lowering down a cliff you cannot climb
    /// back up). It can be crossed travelling in AllowedDirection and blocks travel the other way.
    /// Built on PlatformEffector2D: the gate's local up is turned to face AllowedDirection, so contacts
    /// from behind (moving the allowed way) pass through and contacts from the far side are solid.
    /// Not placed on the cube in this story; face stories place it.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D), typeof(PlatformEffector2D))]
    public class OneWayGate : MonoBehaviour
    {
        [Tooltip("The direction of travel that may cross the gate (in the face's frame).")]
        [SerializeField] private Facing allowedDirection = Facing.East;

        [Tooltip("Length of the gate across the passage.")]
        [SerializeField, Min(0.1f)] private float width = 2f;

        [Tooltip("Thickness of the gate along the allowed direction.")]
        [SerializeField, Min(0.05f)] private float thickness = 0.3f;

        public Facing AllowedDirection
        {
            get => allowedDirection;
            set
            {
                allowedDirection = value;
                Apply();
            }
        }

        public float Width
        {
            get => width;
            set
            {
                width = Mathf.Max(0.1f, value);
                Apply();
            }
        }

        private void Awake() => Apply();

        private void OnValidate()
        {
            if (isActiveAndEnabled) Apply();
        }

        /// <summary>True when travelling in this world direction crosses the gate; false when it is blocked.</summary>
        public bool Allows(Vector2 travel) => Vector2.Dot(travel, allowedDirection.ToVector()) > 0f;

        private void Apply()
        {
            // Local up = allowed direction: rotate the gate so +y points that way.
            Vector2 d = allowedDirection.ToVector();
            float angle = Mathf.Atan2(-d.x, d.y) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            var box = GetComponent<BoxCollider2D>();
            box.size = new Vector2(width, thickness);
            box.usedByEffector = true;

            var effector = GetComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.useOneWayGrouping = true;
            effector.useSideFriction = false;
            effector.useSideBounce = false;
            effector.rotationalOffset = 0f;
            // Only the far face (local up, facing along the allowed direction) is solid, so it stops
            // anything coming back against the allowed direction; the near face lets travellers through.
            effector.surfaceArc = 180f;
        }
    }
}
