using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// An order pulse: a neat little square the Demon fires in a straight line after a telegraph. It passes over walls and
    /// particles, and on touching the player deals its damage through the player's Health (so defence and the
    /// invulnerability window apply), then vanishes. It also vanishes on leaving the arena.
    /// </summary>
    public class OrderPulse : MonoBehaviour
    {
        public const float Radius = 0.3f;

        /// <summary>The player's collider radius in the Cube scene (contact distance = Radius + this).</summary>
        public const float PlayerRadius = 0.4f;

        private Health target;
        private Rect bounds;
        private Transform spin;

        public Vector2 Velocity { get; private set; }
        public float Damage { get; private set; }
        public Vector2 Position => transform.position;

        /// <summary>Damage actually dealt by this pulse (0 if it missed or met invulnerability).</summary>
        public float Dealt { get; private set; }

        public void Configure(Vector2 velocity, float damage, Health player, Rect arenaBounds)
        {
            Velocity = velocity;
            Damage = damage;
            target = player;
            bounds = arenaBounds;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector2 next = (Vector2)transform.position + Velocity * dt;
            transform.position = new Vector3(next.x, next.y, transform.position.z);
            if (spin != null) spin.Rotate(0f, 0f, 360f * dt);

            if (target != null && target.isActiveAndEnabled && !target.IsDead)
            {
                Rigidbody2D playerBody = target.GetComponent<Rigidbody2D>();
                Vector2 playerAt = playerBody != null ? playerBody.position : (Vector2)target.transform.position;
                if (Vector2.Distance(playerAt, next) <= Radius + PlayerRadius)
                {
                    Dealt = target.TakeDamage(Damage, null);
                    Vanish();
                    return;
                }
            }
            if (!bounds.Contains(next)) Vanish();
        }

        private void Vanish()
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        public static OrderPulse Spawn(Vector2 at, Vector2 velocity, float damage, Health player, Rect arenaBounds,
            Transform parent, Material material)
        {
            var go = new GameObject("Order Pulse");
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = new Vector3(Radius * 2f, Radius * 2f, 1f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = ArtCatalog.Shape(PartShape.Square);
            if (material != null) renderer.sharedMaterial = material;
            renderer.color = new Color(0.85f, 0.9f, 1f);
            renderer.sortingOrder = 12;
            var pulse = go.AddComponent<OrderPulse>();
            pulse.spin = visual.transform;
            pulse.Configure(velocity, damage, player, arenaBounds);
            return pulse;
        }
    }
}
