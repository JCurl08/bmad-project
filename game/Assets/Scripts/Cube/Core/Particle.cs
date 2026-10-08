using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// One particle of the core arena: warm or cool, with a home side. A player swing (bare or any item) knocks it away in
    /// the swing's facing direction; otherwise it drifts gently, steering away from the outer walls, and bounces off
    /// walls, the membrane and other particles (a knocked particle coasts on damping until it is back at drifting pace). The Demon can take hold of it (Nudge) to steer it home; a knock breaks
    /// that hold. With the Attraction benefit (4.8) its drift also pulls it gently towards the player (Pull).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class Particle : MonoBehaviour, IAttackReceiver
    {
        public const float Radius = 0.3f * ScreenMath.ContentScale;
        public const float KnockSpeed = 9f * ScreenMath.ContentScale;
        public const float DriftSpeed = 0.35f * ScreenMath.ContentScale;
        public const float DriftAcceleration = 1.2f * ScreenMath.ContentScale;
        public const float Damping = 1f;
        public const float Mass = 0.3f;
        public const float DriftChangeSeconds = 2f;

        /// <summary>Within this distance of an outer wall the drift turns back inward.</summary>
        public const float WallShy = 1f * ScreenMath.ContentScale;

        /// <summary>After a knock, drift and the Demon leave the particle alone this long.</summary>
        public const float KnockedSeconds = 0.8f;

        public static readonly Color WarmColor = new Color(1f, 0.45f, 0.2f);
        public static readonly Color CoolColor = new Color(0.3f, 0.65f, 1f);

        private Rigidbody2D body;
        private CoreArena arena;
        private SeededRng rng;
        private Vector2 driftDirection;
        private int driftStepsLeft;
        private float knockedUntil = float.NegativeInfinity;
        private Vector2? nudgeTarget;
        private float nudgeSpeed;

        public ArenaSide Home { get; private set; }

        /// <summary>How fast its drift draws it towards the player (0: not at all; ArenaTuning.AttractSpeed with Attraction).</summary>
        public float Attraction { get; set; }

        /// <summary>
        /// The pull added to a drifting particle's target velocity: towards the player at attraction, within
        /// ArenaTuning.AttractRange (zero beyond it, on top of the player, or without attraction).
        /// </summary>
        public static Vector2 Pull(Vector2 position, Vector2 player, float attraction)
        {
            if (attraction <= 0f) return Vector2.zero;
            Vector2 to = player - position;
            float d = to.magnitude;
            if (d < 1e-3f || d > ArenaTuning.AttractRange) return Vector2.zero;
            return to / d * attraction;
        }

        public Rigidbody2D Body => body != null ? body : body = GetComponent<Rigidbody2D>();

        public Vector2 Position => Body.position;

        public ArenaSide Side => arena != null ? arena.SideOf(Position) : Home;

        public bool IsHome => Side == Home;

        /// <summary>Swings that knocked this particle.</summary>
        public int Knocks { get; private set; }

        /// <summary>True while recently knocked (the Demon will not grab it).</summary>
        public bool Knocked => Time.time < knockedUntil;

        /// <summary>True while the Demon is steering it.</summary>
        public bool Nudged => nudgeTarget.HasValue;

        public void Configure(CoreArena owner, ArenaSide home, SeededRng random)
        {
            arena = owner;
            Home = home;
            rng = random;
            PickDrift();
        }

        public bool ReceiveAttack(ItemDefinition item, GameObject attacker)
        {
            Vector2 direction = Vector2.zero;
            if (attacker != null)
            {
                var attack = attacker.GetComponent<PlayerAttack>();
                if (attack != null) direction = attack.Facing;
                else direction = Position - (Vector2)attacker.transform.position;
            }
            if (direction.sqrMagnitude < 1e-6f) direction = Vector2.down;
            Knock(direction.normalized);
            return true;
        }

        /// <summary>Knocks the particle along a unit direction at KnockSpeed, breaking any hold the Demon has on it.</summary>
        public void Knock(Vector2 direction)
        {
            nudgeTarget = null;
            Knocks++;
            knockedUntil = Time.time + KnockedSeconds;
            Body.linearVelocity = direction.normalized * KnockSpeed;
        }

        /// <summary>The Demon steers the particle toward a point at a speed (until Release or a knock).</summary>
        public void Nudge(Vector2 target, float speed)
        {
            nudgeTarget = target;
            nudgeSpeed = speed;
        }

        public void Release() => nudgeTarget = null;

        /// <summary>Moves the particle (tests and resets), at rest.</summary>
        public void Teleport(Vector2 position)
        {
            nudgeTarget = null;
            knockedUntil = float.NegativeInfinity;
            Body.position = position;
            Body.linearVelocity = Vector2.zero;
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector2 velocity = Body.linearVelocity;
            if (nudgeTarget.HasValue)
            {
                Vector2 to = nudgeTarget.Value - Position;
                float step = Mathf.Min(nudgeSpeed, to.magnitude / dt);
                Body.linearVelocity = to.sqrMagnitude > 1e-6f ? to.normalized * step : Vector2.zero;
                return;
            }
            // A knocked particle coasts (damping slows it) until it is back at drifting pace.
            if (Knocked || velocity.sqrMagnitude > 4f * DriftSpeed * DriftSpeed) return;

            if (--driftStepsLeft <= 0) PickDrift();
            Vector2 drift = driftDirection;
            if (arena != null) drift = arena.AwayFromWalls(Position, drift, WallShy);
            Vector2 target = drift * DriftSpeed;
            if (Attraction > 0f && arena != null && arena.PlayerTransform != null)
                target += Pull(Position, arena.PlayerTransform.position, Attraction);
            Body.linearVelocity = Vector2.MoveTowards(velocity, target, DriftAcceleration * dt);
        }

        private void PickDrift()
        {
            // Counted in physics steps, so a replayed run drifts identically whatever the frame timing.
            driftStepsLeft = Mathf.Max(1, Mathf.RoundToInt(DriftChangeSeconds / Time.fixedDeltaTime));
            if (rng == null)
            {
                driftDirection = Vector2.right;
                return;
            }
            float angle = rng.NextInt(360) * Mathf.Deg2Rad;
            driftDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        /// <summary>Builds a particle: a coloured disc on a light, bouncy dynamic body.</summary>
        public static Particle Spawn(CoreArena owner, ArenaSide home, Vector2 position, SeededRng random, Transform parent,
            Sprite sprite, Material material, PhysicsMaterial2D bounce)
        {
            var go = new GameObject(home == ArenaSide.Warm ? "Warm Particle" : "Cool Particle");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.mass = Mass;
            body.linearDamping = Damping;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var circle = go.AddComponent<CircleCollider2D>();
            circle.radius = Radius;
            circle.sharedMaterial = bounce;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            Vector2 spriteSize = sprite.bounds.size;
            visual.transform.localScale = new Vector3(Radius * 2f / spriteSize.x, Radius * 2f / spriteSize.y, 1f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            if (material != null) renderer.sharedMaterial = material;
            renderer.color = home == ArenaSide.Warm ? WarmColor : CoolColor;
            renderer.sortingOrder = 6;

            var particle = go.AddComponent<Particle>();
            particle.Configure(owner, home, random);
            return particle;
        }
    }
}
