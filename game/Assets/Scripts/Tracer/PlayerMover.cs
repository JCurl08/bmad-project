using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Tracer
{
    /// <summary>Top-down movement driven by the project-wide Player/Move action.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMover : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float speed = 6f;
        [SerializeField] private string moveActionPath = "Player/Move";

        private Rigidbody2D body;
        private InputAction move;

        public float Speed
        {
            get => speed;
            set => speed = Mathf.Max(0f, value);
        }

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
        }

        private void FixedUpdate()
        {
            Vector2 input = move != null ? move.ReadValue<Vector2>() : Vector2.zero;
            body.linearVelocity = Vector2.ClampMagnitude(input, 1f) * speed;
        }
    }
}
