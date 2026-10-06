using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// NPC movement chosen by its legs: Stand stays put; Wander walks between seeded random points inside
    /// its bounds (its screen), pausing between them and giving up on a point it cannot reach; Flee stands
    /// until the player comes within FleeRadius, then moves directly away, never leaving its bounds.
    /// Drives a dynamic Rigidbody2D (no gravity, rotation frozen) by velocity in FixedUpdate, so module
    /// walls and closed gates stop the NPC, and the physics solver (not a kinematic shove) settles contacts
    /// with the player, who therefore cannot be pushed into a wall.
    /// </summary>
    public class NpcMover : MonoBehaviour
    {
        /// <summary>RNG stream for wander targets (NPC parts use NpcFactory.RngStream).</summary>
        public const ulong RngStream = NpcFactory.RngStream + 1;

        [SerializeField] private Movement movement;
        [SerializeField] private Rect bounds;
        [SerializeField] private Transform player;
        [SerializeField, Min(0f)] private float wanderSpeed = 1.5f;
        [SerializeField, Min(0f)] private float fleeSpeed = 3.5f;
        [SerializeField, Min(0f)] private float fleeRadius = 3f;
        [SerializeField, Min(0f)] private float pauseSeconds = 0.6f;

        private Rigidbody2D body;
        private SeededRng rng = new SeededRng(0UL, RngStream);
        private Vector2 target;
        private bool hasTarget;
        private float pause;
        private float giveUpIn;

        public Movement Movement => movement;
        public Rect Bounds => bounds;
        public float FleeRadius => fleeRadius;

        public Transform Player
        {
            get => player;
            set => player = value;
        }

        private Vector2 Position => body != null ? body.position : (Vector2)transform.position;

        public void Configure(Movement legs, Rect area, Transform playerTransform, uint salt)
        {
            movement = legs;
            bounds = area;
            if (playerTransform != null) player = playerTransform;
            rng = new SeededRng(salt, RngStream);
            hasTarget = false;
            pause = 0f;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector2 velocity = Vector2.zero;
            switch (movement)
            {
                case Movement.Wander: velocity = Wander(dt); break;
                case Movement.Flee: velocity = Flee(); break;
            }
            Drive(velocity, dt);
        }

        private Vector2 Wander(float dt)
        {
            if (pause > 0f)
            {
                pause -= dt;
                return Vector2.zero;
            }
            Vector2 here = Position;
            if (!hasTarget)
            {
                target = new Vector2(
                    bounds.xMin + bounds.width * rng.NextInt(1001) / 1000f,
                    bounds.yMin + bounds.height * rng.NextInt(1001) / 1000f);
                hasTarget = true;
                // A wall or closed gate may block the way: give up after the straight-line time plus slack.
                giveUpIn = Vector2.Distance(here, target) / Mathf.Max(0.01f, wanderSpeed) + 1f;
            }
            giveUpIn -= dt;
            Vector2 toTarget = target - here;
            float step = wanderSpeed * dt;
            if (toTarget.magnitude <= step || giveUpIn <= 0f)
            {
                hasTarget = false;
                pause = pauseSeconds;
                return toTarget.magnitude <= step ? toTarget / dt : Vector2.zero;
            }
            return toTarget.normalized * wanderSpeed;
        }

        private Vector2 Flee()
        {
            Transform threat = ResolvePlayer();
            if (threat == null) return Vector2.zero;
            Vector2 away = Position - (Vector2)threat.position;
            if (away.sqrMagnitude > fleeRadius * fleeRadius) return Vector2.zero;
            Vector2 direction = away.sqrMagnitude > 1e-6f ? away.normalized : Vector2.right;
            return direction * fleeSpeed;
        }

        /// <summary>Sets the body's velocity, trimmed so the next step stays inside bounds. Colliders do the rest.</summary>
        private void Drive(Vector2 velocity, float dt)
        {
            Vector2 here = Position;
            velocity = BoundedVelocity(here, velocity, bounds, dt);
            if (body != null) body.linearVelocity = velocity;
            else transform.position = (Vector3)(here + velocity * dt) + Vector3.forward * transform.position.z;
        }

        /// <summary>Speed at which a body pushed outside its bounds walks back in (at least; faster if it was moving faster).</summary>
        public const float ReturnSpeed = 1.5f;

        /// <summary>
        /// The velocity trimmed so one step of dt from here stays inside bounds (an empty rect means no bounds).
        /// A body already outside (e.g. pushed out) is steered straight back toward the nearest point inside,
        /// whatever it wanted to do. Shared with EnemyBrain.
        /// </summary>
        public static Vector2 BoundedVelocity(Vector2 here, Vector2 velocity, Rect bounds, float dt)
        {
            if (dt <= 0f || (bounds.width <= 0f && bounds.height <= 0f)) return velocity;
            var inside = new Vector2(Mathf.Clamp(here.x, bounds.xMin, bounds.xMax), Mathf.Clamp(here.y, bounds.yMin, bounds.yMax));
            Vector2 back = inside - here;
            if (back.sqrMagnitude > 0f)
            {
                float speed = Mathf.Max(velocity.magnitude, ReturnSpeed);
                return back.magnitude <= speed * dt ? back / dt : back.normalized * speed;
            }
            Vector2 next = here + velocity * dt;
            next = new Vector2(Mathf.Clamp(next.x, bounds.xMin, bounds.xMax), Mathf.Clamp(next.y, bounds.yMin, bounds.yMax));
            return (next - here) / dt;
        }

        private Transform ResolvePlayer()
        {
            if (player != null) return player;
            var mover = FindAnyObjectByType<PlayerMover>();
            if (mover != null) player = mover.transform;
            return player;
        }
    }
}
