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

    /// <summary>
    /// What the ground and air around the player do to its movement this step (the Cube's Earth hazards): a speed
    /// multiplier (freezing rain), a push as a fraction of the player's speed (wind) and a grip (snow: below 1 the
    /// velocity only eases toward the wanted one, so the player slides). None leaves movement exactly as it was.
    /// </summary>
    public struct MoveModifier
    {
        /// <summary>Multiplies the walking speed (1 = unchanged); PlayerMover clamps it to [MinSpeedMultiplier, 1].</summary>
        public float SpeedMultiplier;

        /// <summary>A push, as a fraction of the walking speed (its direction and strength); clamped to MaxPushFraction.</summary>
        public Vector2 Push;

        /// <summary>How much of the way to the wanted velocity each physics step goes (1 = all of it, no sliding).</summary>
        public float Grip;

        public static MoveModifier None => new MoveModifier { SpeedMultiplier = 1f, Push = Vector2.zero, Grip = 1f };

        public bool IsNone => SpeedMultiplier >= 1f && Push.sqrMagnitude < 1e-8f && Grip >= 1f;

        public override string ToString() => $"speed x{SpeedMultiplier:0.##}, push {Push}, grip {Grip:0.###}";
    }

    /// <summary>
    /// The one movement-modifier hook (CAP-8 hazards): a component on the player that implements this is read by
    /// PlayerMover every physics step. It changes how the player moves, never the player's speed itself
    /// (EffectiveSpeed), so speed upgrades and anything that reads EffectiveSpeed (Physics' timed doors) are unaffected.
    /// </summary>
    public interface IMoveModifier
    {
        MoveModifier CurrentMoveModifier { get; }
    }

    /// <summary>
    /// The stop on a face crossing (1.25): once held, the player stands still, whatever keys are down, until every
    /// direction has been let go and a key is pressed again. Pure: PlayerMover feeds it the raw input every physics step.
    /// </summary>
    public sealed class FreshPressLatch
    {
        /// <summary>An input shorter than this counts as every direction let go.</summary>
        public const float ReleasedBelow = 0.1f;

        private bool sawRelease;

        /// <summary>True while the player waits for a fresh press (input is ignored).</summary>
        public bool Waiting { get; private set; }

        /// <summary>Stops the player until every direction is let go and a key is pressed again.</summary>
        public void Hold()
        {
            Waiting = true;
            sawRelease = false;
        }

        /// <summary>Back to normal movement (a teleport, a new run).</summary>
        public void Clear()
        {
            Waiting = false;
            sawRelease = false;
        }

        /// <summary>The input the player walks with this step: zero while waiting; the raw input once a fresh press ends the wait.</summary>
        public Vector2 Filter(Vector2 raw)
        {
            if (!Waiting) return raw;
            if (raw.magnitude < ReleasedBelow)
            {
                sawRelease = true;
                return Vector2.zero;
            }
            if (!sawRelease) return Vector2.zero;
            Clear();
            return raw;
        }
    }

    /// <summary>
    /// Top-down movement driven by the project-wide Player/Move action. The velocity is the input times EffectiveSpeed,
    /// changed only through the player's IMoveModifier (if any), within bounds that keep every way passable: a hazard can
    /// slow the player to no less than MinSpeedMultiplier of its speed, push it by at most MaxPushFraction of that slowed
    /// speed (so walking against a push always makes headway), and make it slide (grip no lower than MinGrip).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMover : MonoBehaviour
    {
        /// <summary>The slowest a modifier can make the player: this fraction of its speed.</summary>
        public const float MinSpeedMultiplier = 0.5f;

        /// <summary>The strongest push a modifier can give: this fraction of the player's (slowed) speed.</summary>
        public const float MaxPushFraction = 0.5f;

        /// <summary>The least grip a modifier can give (per physics step), so a slide always ends.</summary>
        public const float MinGrip = 0.03f;

        [SerializeField, Min(0f)] private float speed = 6f * ScreenMath.ContentScale; // 9: crosses a 24-wide screen in the 2.7 s the first screens took at 6
        [SerializeField] private string moveActionPath = "Player/Move";

        private Rigidbody2D body;
        private InputAction move;
        private IMoveSpeedScale speedScale;
        private bool speedScaleResolved;
        private IMoveModifier modifier;
        private bool modifierResolved;
        private readonly FreshPressLatch latch = new FreshPressLatch();

        /// <summary>True while the player stands still after a face crossing, waiting for every key to be let go and a fresh press.</summary>
        public bool WaitingForFreshPress => latch.Waiting;

        /// <summary>
        /// Stops the player at once and keeps it still (no walking, no push, no slide) until every direction is let go and
        /// a key is pressed again (the Cube's face crossings).
        /// </summary>
        public void StopUntilFreshPress()
        {
            latch.Hold();
            if (body != null) body.linearVelocity = Vector2.zero;
            MoveInput = Vector2.zero;
        }

        /// <summary>Ends a StopUntilFreshPress wait (a teleport or a new run).</summary>
        public void ClearFreshPressWait() => latch.Clear();

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

        /// <summary>The player's movement modifier this step, clamped to the mover's bounds (None without one).</summary>
        public MoveModifier Modifier
        {
            get
            {
                // Looked up once (a missing one is cached too, like the speed scale); RefreshModifier looks again after
                // content adds one. A destroyed one is dropped.
                if (!modifierResolved)
                {
                    modifier = GetComponent<IMoveModifier>();
                    modifierResolved = true;
                }
                if (modifier is Object o && o == null) modifier = null;
                return modifier != null ? Clamp(modifier.CurrentMoveModifier) : MoveModifier.None;
            }
        }

        /// <summary>Looks the player's IMoveModifier up again on the next step (call after adding or replacing one).</summary>
        public void RefreshModifier()
        {
            modifier = null;
            modifierResolved = false;
        }

        /// <summary>A modifier within the mover's bounds: slows to no less than MinSpeedMultiplier, pushes by at most MaxPushFraction, grips at least MinGrip.</summary>
        public static MoveModifier Clamp(MoveModifier m) => new MoveModifier
        {
            SpeedMultiplier = Mathf.Clamp(float.IsNaN(m.SpeedMultiplier) ? 1f : m.SpeedMultiplier, MinSpeedMultiplier, 1f),
            Push = Vector2.ClampMagnitude(m.Push, MaxPushFraction),
            Grip = Mathf.Clamp(float.IsNaN(m.Grip) ? 1f : m.Grip, MinGrip, 1f),
        };

        /// <summary>
        /// The velocity the player is steered to for an input (length at most 1) at a speed under a modifier: the input
        /// at the slowed speed plus the push (a fraction of that slowed speed). Pure.
        /// </summary>
        public static Vector2 TargetVelocity(Vector2 input, float speed, MoveModifier m)
        {
            m = Clamp(m);
            float walk = Mathf.Max(0f, speed) * m.SpeedMultiplier;
            return Vector2.ClampMagnitude(input, 1f) * walk + m.Push * walk;
        }

        /// <summary>The next velocity from the current one: the target at once with full grip, else eased toward it. Pure.</summary>
        public static Vector2 NextVelocity(Vector2 current, Vector2 target, MoveModifier m)
        {
            float grip = Clamp(m).Grip;
            return grip >= 1f ? target : Vector2.Lerp(current, target, grip);
        }

        /// <summary>
        /// The last non-zero move direction (unit length; down until the player first moves). A teleport (a new run, a
        /// debug jump) resets it with SetFacing.
        /// </summary>
        public Vector2 Facing { get; private set; } = Vector2.down;

        /// <summary>
        /// The move input the last physics step acted on (after the fresh-press wait; zero while waiting, before the first
        /// step, and once the mover is disabled): what the player is pushing toward right now.
        /// </summary>
        public Vector2 MoveInput { get; private set; }

        /// <summary>Sets the facing (e.g. to the entry facing after a teleport). A zero direction is ignored.</summary>
        public void SetFacing(Vector2 direction)
        {
            if (direction.sqrMagnitude > 1e-6f) Facing = direction.normalized;
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
            // A disabled mover (e.g. the player died) must not leave the body gliding on its last velocity.
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private void Update()
        {
            // Watch for the let-go every frame too, so a quick release between two physics steps still counts.
            if (latch.Waiting && move != null && move.ReadValue<Vector2>().magnitude < FreshPressLatch.ReleasedBelow)
                latch.Filter(Vector2.zero);
        }

        private void FixedUpdate()
        {
            Vector2 input = move != null ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f) : Vector2.zero;
            if (latch.Waiting)
            {
                input = latch.Filter(input);
                if (latch.Waiting)
                {
                    MoveInput = Vector2.zero;
                    body.linearVelocity = Vector2.zero;
                    return;
                }
            }
            MoveInput = input;
            if (input.sqrMagnitude > 1e-4f) Facing = input.normalized;
            MoveModifier m = Modifier;
            if (m.IsNone)
            {
                body.linearVelocity = input * EffectiveSpeed;
                return;
            }
            body.linearVelocity = NextVelocity(body.linearVelocity, TargetVelocity(input, EffectiveSpeed, m), m);
        }
    }
}
