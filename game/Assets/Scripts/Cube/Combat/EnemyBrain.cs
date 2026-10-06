using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Enemy behaviour: when the player is within sight and inside this enemy's bounds (its screen), chase
    /// them, staying inside the bounds; on touching the player, deal the Enemy's contact damage (the
    /// player's invulnerability window stops repeats). Drives a dynamic Rigidbody2D by velocity, like
    /// NpcMover, so walls and closed gates stop it and contacts are solved by physics, never by a shove.
    /// Stops when the player is dead, and for good once its own Health is dead. Disabling it stops the body.
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    public class EnemyBrain : MonoBehaviour
    {
        public const float DefaultChaseSpeed = 2.2f;
        public const float DefaultSightRadius = 7f;

        /// <summary>Gap (world units) between colliders that still counts as touching.</summary>
        public const float ContactGap = 0.06f;

        [SerializeField] private Rect bounds;
        [SerializeField] private Transform player;
        [SerializeField, Min(0f)] private float chaseSpeed = DefaultChaseSpeed;
        [SerializeField, Min(0f)] private float sightRadius = DefaultSightRadius;

        private Rigidbody2D body;
        private Enemy enemy;
        private Health ownHealth;
        private Collider2D[] ownColliders;
        private Health playerHealth;
        private Collider2D[] playerColliders = new Collider2D[0];
        private Transform resolvedFor;

        public Rect Bounds => bounds;

        public float ChaseSpeed
        {
            get => chaseSpeed;
            set => chaseSpeed = Mathf.Max(0f, value);
        }

        public float SightRadius
        {
            get => sightRadius;
            set => sightRadius = Mathf.Max(0f, value);
        }

        public Transform Player
        {
            get => player;
            set => player = value;
        }

        /// <summary>False once this enemy's own Health is dead or its Enemy is disabled.</summary>
        public bool Alive => enemy != null && enemy.enabled && (ownHealth == null || !ownHealth.IsDead);

        /// <summary>True while chasing (the player is in sight and inside the bounds).</summary>
        public bool Chasing { get; private set; }

        public void Configure(Rect area, Transform playerTransform)
        {
            bounds = area;
            if (playerTransform != null) player = playerTransform;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            enemy = GetComponent<Enemy>();
            ownHealth = GetComponent<Health>();
        }

        private void OnDisable()
        {
            Chasing = false;
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private void FixedUpdate()
        {
            if (!Alive)
            {
                // A dead (but not removed) or switched-off enemy stands still and deals no contact damage.
                Chasing = false;
                if (body != null) body.linearVelocity = Vector2.zero;
                return;
            }

            Transform target = ResolvePlayer();
            Vector2 here = body != null ? body.position : (Vector2)transform.position;
            Vector2 velocity = Vector2.zero;
            Chasing = false;
            if (target != null && (playerHealth == null || !playerHealth.IsDead))
            {
                Vector2 toTarget = (Vector2)target.position - here;
                bool inSight = toTarget.sqrMagnitude <= sightRadius * sightRadius;
                if (inSight && InBounds(target.position))
                {
                    Chasing = true;
                    if (toTarget.sqrMagnitude > 1e-4f) velocity = toTarget.normalized * chaseSpeed;
                }
                TryContactDamage();
            }

            float dt = Time.fixedDeltaTime;
            velocity = NpcMover.BoundedVelocity(here, velocity, bounds, dt);
            if (body != null) body.linearVelocity = velocity;
            else transform.position = (Vector3)(here + velocity * dt) + Vector3.forward * transform.position.z;
        }

        /// <summary>True if the point is inside the bounds, with a little slack (no bounds: always).</summary>
        private bool InBounds(Vector2 point)
        {
            if (bounds.width <= 0f && bounds.height <= 0f) return true;
            const float slack = 1f;
            return point.x >= bounds.xMin - slack && point.x <= bounds.xMax + slack &&
                   point.y >= bounds.yMin - slack && point.y <= bounds.yMax + slack;
        }

        /// <summary>Deals contact damage if touching the player. Returns the damage taken (0 if none).</summary>
        public float TryContactDamage()
        {
            ResolvePlayer();
            if (!isActiveAndEnabled || !Alive || playerHealth == null || enemy == null || enemy.ContactDamage <= 0f) return 0f;
            if (ownColliders == null || ownColliders.Length == 0) ownColliders = GetComponents<Collider2D>();
            foreach (Collider2D mine in ownColliders)
            {
                if (mine == null || !mine.enabled) continue;
                foreach (Collider2D theirs in playerColliders)
                {
                    if (theirs == null || !theirs.enabled) continue;
                    ColliderDistance2D gap = mine.Distance(theirs);
                    if (gap.isValid && gap.distance <= ContactGap)
                        return playerHealth.TakeDamage(enemy.ContactDamage, null);
                }
            }
            return 0f;
        }

        private Transform ResolvePlayer()
        {
            if (player == null)
            {
                var mover = FindAnyObjectByType<PlayerMover>();
                if (mover != null) player = mover.transform;
            }
            if (player != null && resolvedFor != player)
            {
                resolvedFor = player;
                playerHealth = player.GetComponentInParent<Health>();
                Rigidbody2D playerBody = player.GetComponentInParent<Rigidbody2D>();
                playerColliders = playerBody != null
                    ? playerBody.GetComponentsInChildren<Collider2D>()
                    : player.GetComponentsInChildren<Collider2D>();
            }
            return player;
        }
    }
}
