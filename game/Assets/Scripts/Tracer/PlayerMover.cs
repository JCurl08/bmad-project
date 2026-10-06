using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Tracer
{
    /// <summary>
    /// Scales the player's movement speed. A component on the player that implements this (the Cube's
    /// PlayerStats) is read by PlayerMover every physics step; without one the base speed is used as is.
    /// </summary>
    public interface IMoveSpeedScale
    {
        float MoveSpeedMultiplier { get; }
    }

    /// <summary>Top-down movement driven by the project-wide Player/Move action.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMover : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float speed = 6f;
        [SerializeField] private string moveActionPath = "Player/Move";

        private Rigidbody2D body;
        private InputAction move;
        private IMoveSpeedScale speedScale;
        private bool speedScaleResolved;

        /// <summary>Base speed in units per second (before any IMoveSpeedScale on the player).</summary>
        public float Speed
        {
            get => speed;
            set => speed = Mathf.Max(0f, value);
        }

        /// <summary>The speed actually applied: Speed times the speed scale's multiplier, if there is one.</summary>
        public float EffectiveSpeed
        {
            get
            {
                // Looked up once, on first use (the scale may be added after this component in tests and builders);
                // a missing scale is cached too.
                if (!speedScaleResolved)
                {
                    speedScale = GetComponent<IMoveSpeedScale>();
                    speedScaleResolved = true;
                }
                if (speedScale is Object o && o == null) speedScale = null;
                return speed * (speedScale != null ? Mathf.Max(0f, speedScale.MoveSpeedMultiplier) : 1f);
            }
        }

        /// <summary>The last non-zero move direction (unit length; down until the player first moves).</summary>
        public Vector2 Facing { get; private set; } = Vector2.down;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
        }

        private void OnEnable()
        {
            move = InputSystem.actions != null ? InputSystem.actions.FindAction(moveActionPath) : null;
            if (move == null)
            {
                Debug.LogError($"PlayerMover: input action '{moveActionPath}' not found in the project-wide actions.", this);
                return;
            }
            move.Enable();
        }

        private void OnDisable()
        {
            move?.Disable();
            move = null;
            // A disabled mover (e.g. the player died) must not leave the body gliding on its last velocity.
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private void FixedUpdate()
        {
            Vector2 input = move != null ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f) : Vector2.zero;
            if (input.sqrMagnitude > 1e-4f) Facing = input.normalized;
            body.linearVelocity = input * EffectiveSpeed;
        }
    }
}
