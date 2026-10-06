using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A boulder of the Physics face: mass for Einstein's time field (TimeField.MassNear counts it). It sits on a
    /// kinematic body, so nothing can push it: the player, enemies and NPCs just bump into it. Only the Mass Mitt
    /// moves it: a grabbed boulder keeps its offset from the player and follows, each physics step casting its
    /// circle along the move and stopping short of anything solid (walls, closed doors, other boulders, NPCs,
    /// enemies) and of a timed door's doorway (BoulderBlocker), and never leaving its Bounds (its home screen,
    /// clear of the edge band). The player and the grabbed boulder ignore each other while it is held, and that
    /// collision only comes back once they are apart, so a boulder can never shove the player (into a wall or
    /// anywhere else). Swinging the mitt at it grabs or lets go (IAttackReceiver).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class Boulder : MonoBehaviour, IAttackReceiver
    {
        public const float Radius = 0.4f;

        /// <summary>Gap kept between a moving boulder and what it bumps into.</summary>
        private const float Skin = 0.02f;

        /// <summary>The boulder art's tint, at rest and while the Mass Mitt holds it.</summary>
        public static readonly Color StoneColor = new Color(0.92f, 0.9f, 0.88f);
        public static readonly Color GrabbedColor = new Color(0.75f, 0.7f, 1f);

        private static readonly List<Boulder> active = new List<Boulder>();

        [SerializeField] private float mass = TimeField.BoulderMass;
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private bool bounded;
        [SerializeField] private Rect bounds;

        private Rigidbody2D rb;
        private CircleCollider2D circle;
        private MassMitt holder;
        private Vector2 offset;
        private readonly List<Collider2D> ignoring = new List<Collider2D>();
        private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();

        /// <summary>Every enabled boulder (what the time field weighs).</summary>
        public static IReadOnlyList<Boulder> All => active;

        /// <summary>Who placed this boulder (a PhysicsTrial for its own boulders), or null; see TimeField.MassNear.</summary>
        public Object Owner { get; set; }

        public float Mass
        {
            get => mass;
            set => mass = Mathf.Max(0f, value);
        }

        /// <summary>The mitt holding this boulder, or null.</summary>
        public MassMitt Holder => holder;

        public bool IsGrabbed => holder != null;

        /// <summary>Where the boulder may go (world rect for its centre); unbounded until set.</summary>
        public Rect? Bounds
        {
            get => bounded ? bounds : (Rect?)null;
            set
            {
                bounded = value.HasValue;
                if (value.HasValue) bounds = value.Value;
            }
        }

        public Rigidbody2D Body => rb != null ? rb : rb = GetComponent<Rigidbody2D>();
        public CircleCollider2D Circle => circle != null ? circle : circle = GetComponent<CircleCollider2D>();

        public Vector2 Position => Body.position;

        private void OnEnable()
        {
            if (!active.Contains(this)) active.Add(this);
        }

        private void OnDisable()
        {
            active.Remove(this);
            if (holder != null) holder.Release();
            RestoreCollisions(true);
        }

        /// <summary>Called by a MassMitt: the boulder now follows its player at the current offset.</summary>
        public void AttachTo(MassMitt mitt)
        {
            if (mitt == null) return;
            if (holder != null && holder != mitt) holder.Release();
            holder = mitt;
            offset = Position - mitt.Body.position;
            foreach (Collider2D c in mitt.Colliders)
            {
                if (c == null) continue;
                Physics2D.IgnoreCollision(Circle, c, true);
                if (!ignoring.Contains(c)) ignoring.Add(c);
            }
            if (body != null) body.color = GrabbedColor;
        }

        /// <summary>Called by a MassMitt when it lets go (collisions come back once the player is clear).</summary>
        public void Detach(MassMitt mitt)
        {
            if (holder != mitt) return;
            holder = null;
            if (body != null) body.color = StoneColor;
            RestoreCollisions(false);
        }

        /// <summary>Moves the boulder to a position at once (tools and tests); a held boulder is let go first.</summary>
        public void Teleport(Vector2 position)
        {
            if (holder != null) holder.Release();
            Body.position = position;
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            Physics2D.SyncTransforms();
        }

        public bool ReceiveAttack(ItemDefinition item, GameObject attacker)
        {
            if (item == null || attacker == null) return false;
            MassMitt mitt = attacker.GetComponentInParent<MassMitt>();
            if (mitt == null || mitt.Item != item) return false;
            return mitt.SwingAt(this, attacker.GetComponentInParent<PlayerAttack>());
        }

        private void FixedUpdate()
        {
            if (holder != null) Follow();
            else if (ignoring.Count > 0) RestoreCollisions(false);
        }

        private void Follow()
        {
            Vector2 from = Position;
            Vector2 target = holder.Body.position + offset;
            if (bounded)
                target = new Vector2(Mathf.Clamp(target.x, bounds.xMin, bounds.xMax), Mathf.Clamp(target.y, bounds.yMin, bounds.yMax));
            Vector2 delta = target - from;
            float distance = delta.magnitude;
            // Stuck far behind (a wall, a door), or the player went away (left the screen): let go, without moving.
            if (distance > MassMitt.StuckDistance || Vector2.Distance(holder.Body.position, from) > MassMitt.LetGoDistance)
            {
                holder.Release();
                return;
            }
            if (distance > 1e-4f)
            {
                Vector2 dir = delta / distance;
                float allowed = Mathf.Min(distance, AllowedMove(from, dir, distance));
                if (allowed > 0f) Body.MovePosition(from + dir * allowed);
            }
        }

        /// <summary>How far the boulder may move along dir before it would touch something it must not enter.</summary>
        private float AllowedMove(Vector2 from, Vector2 dir, float distance)
        {
            var filter = new ContactFilter2D();
            filter.NoFilter();
            filter.useTriggers = true;
            hits.Clear();
            Physics2D.CircleCast(from, Radius * 0.98f, dir, filter, hits, distance + Skin);
            float allowed = distance;
            foreach (RaycastHit2D hit in hits)
            {
                Collider2D other = hit.collider;
                if (other == null || other == Circle || !other.enabled) continue;
                if (holder != null && other.attachedRigidbody != null && other.attachedRigidbody == holder.Body) continue;
                if (other.isTrigger && other.GetComponent<BoulderBlocker>() == null) continue;
                if (hit.distance <= 0f)
                {
                    // Already touching: only a move deeper into it is blocked.
                    Vector2 toOther = other.ClosestPoint(from) - from;
                    if (toOther.sqrMagnitude < 1e-8f) toOther = (Vector2)other.bounds.center - from;
                    if (Vector2.Dot(dir, toOther) <= 0f) continue;
                    return 0f;
                }
                allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - Skin));
            }
            return allowed;
        }

        private void RestoreCollisions(bool force)
        {
            for (int i = ignoring.Count - 1; i >= 0; i--)
            {
                Collider2D c = ignoring[i];
                if (c == null)
                {
                    ignoring.RemoveAt(i);
                    continue;
                }
                if (!force && Circle.enabled && c.enabled && Physics2D.Distance(Circle, c).distance < 0.02f) continue; // still overlapping
                if (Circle != null) Physics2D.IgnoreCollision(Circle, c, false);
                ignoring.RemoveAt(i);
            }
        }

        /// <summary>Builds a boulder at a world position (kinematic body, solid circle, grey stone), bounded or not.</summary>
        public static Boulder Create(Transform parent, Vector2 position, Rect? bounds, Material material, float boulderMass = TimeField.BoulderMass)
        {
            var go = new GameObject("Boulder");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = position;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            go.AddComponent<CircleCollider2D>().radius = Radius;
            var boulder = go.AddComponent<Boulder>();
            boulder.mass = Mathf.Max(0f, boulderMass);
            boulder.Bounds = bounds;
            boulder.body = ArtCatalog.AddSprite(go.transform, "Stone", ArtKey.Boulder, Vector2.zero,
                new Vector2(Radius * 2.4f, Radius * 2.4f), StoneColor, 3, material);
            return boulder;
        }

        /// <summary>The bounds of a boulder whose home screen is centred here: the screen clear of its edge band.</summary>
        public static Rect ScreenBounds(Vector2 screenCentre)
        {
            Vector2 half = Game.Tracer.ScreenMath.DefaultScreenSize / 2f -
                           new Vector2(ScreenModule.EdgeClearance + Radius, ScreenModule.EdgeClearance + Radius);
            return new Rect(screenCentre - half, half * 2f);
        }
    }
}
