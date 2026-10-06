using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The portal on a built face's active core-entrance slot: a swirling pink trigger that, when the player steps into it,
    /// moves them into the core arena (CoreArena.Enter, which suspends cube navigation). Open from run 1, nothing required.
    /// CoreArena attaches one to every active core-entrance slot on each reveal.
    /// </summary>
    public class CorePortal : MonoBehaviour
    {
        public const float Radius = 0.45f;
        public const float SpinDegreesPerSecond = 120f;

        [SerializeField] private CoreArena arena;

        private Transform swirl;

        public CoreArena Arena
        {
            get => arena;
            set => arena = value;
        }

        /// <summary>Times the player went through this portal.</summary>
        public int Entries { get; private set; }

        private void Update()
        {
            if (swirl != null) swirl.Rotate(0f, 0f, SpinDegreesPerSecond * Time.deltaTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Rigidbody2D body = other.attachedRigidbody;
            CubeNavigator navigator = body != null ? body.GetComponent<CubeNavigator>() : other.GetComponentInParent<CubeNavigator>();
            if (navigator != null) TryEnter(navigator);
        }

        /// <summary>Sends the player into the arena. Returns false without an arena.</summary>
        public bool TryEnter(CubeNavigator navigator)
        {
            if (arena == null || navigator == null || !isActiveAndEnabled) return false;
            if (!arena.Enter(navigator)) return false;
            Entries++;
            return true;
        }

        /// <summary>Builds a portal at a core-entrance slot.</summary>
        public static CorePortal Create(Transform slot, CoreArena owner, Material material)
        {
            var go = new GameObject("Core Portal");
            go.transform.SetParent(slot, false);
            go.transform.localPosition = Vector3.zero;
            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = Radius;

            var ring = new GameObject("Ring");
            ring.transform.SetParent(go.transform, false);
            ring.transform.localScale = new Vector3(Radius * 2.6f, Radius * 2.6f, 1f);
            var ringRenderer = ring.AddComponent<SpriteRenderer>();
            ringRenderer.sprite = NpcFactory.ShapeSprite(PartShape.Circle);
            if (material != null) ringRenderer.sharedMaterial = material;
            ringRenderer.color = new Color(1f, 0.25f, 0.6f, 0.85f);
            ringRenderer.sortingOrder = 3;

            var swirl = new GameObject("Swirl");
            swirl.transform.SetParent(go.transform, false);
            swirl.transform.localScale = new Vector3(Radius * 1.6f, Radius * 1.6f, 1f);
            var swirlRenderer = swirl.AddComponent<SpriteRenderer>();
            swirlRenderer.sprite = NpcFactory.ShapeSprite(PartShape.Diamond);
            if (material != null) swirlRenderer.sharedMaterial = material;
            swirlRenderer.color = new Color(0.25f, 0.05f, 0.3f);
            swirlRenderer.sortingOrder = 4;

            var portal = go.AddComponent<CorePortal>();
            portal.arena = owner;
            portal.swirl = swirl.transform;
            return portal;
        }
    }
}
