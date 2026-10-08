using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The portal on a built face's active core-entrance slot: a swirling pink trigger that, when the player steps into it,
    /// moves them into the core arena (CoreArena.Enter, which suspends cube navigation). Open from run 1, nothing required.
    /// CoreArena attaches one to every active core-entrance slot on each reveal. The arena remembers the portal's screen, so
    /// its exit door brings the player back here.
    /// Only the run's real portal (4.13, CubeWorld.Lair) leads to the arena; every other leads down into its face's lair room
    /// (LairRooms, the arena's Lair), whose exit brings the player back beside it. Without a lair (no LairRooms in the scene)
    /// every portal leads to the arena, as before.
    /// </summary>
    public class CorePortal : MonoBehaviour
    {
        public const float Radius = 0.45f * ScreenMath.ContentScale;
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

        /// <summary>The screen this portal stands on, if it stands on a face.</summary>
        public bool TryScreen(out ScreenAddress screen)
        {
            screen = default;
            return arena != null && arena.World != null && arena.World.TryScreenAt(transform.position, out screen);
        }

        /// <summary>True when this portal leads to the arena (the run's real one, or any portal without a lair); false when it leads down into a lair room.</summary>
        public bool LeadsToArena
        {
            get
            {
                LairRooms lair = arena != null ? arena.Lair : null;
                return lair == null || !TryScreen(out ScreenAddress here) || !lair.LeadsToLair(here.Face);
            }
        }

        /// <summary>
        /// Sends the player through: into the arena (the real portal, which the player now knows: LairProgress), or down into
        /// its face's lair room (4.13). Returns false without an arena.
        /// </summary>
        public bool TryEnter(CubeNavigator navigator)
        {
            if (arena == null || navigator == null || !isActiveAndEnabled) return false;
            ScreenAddress? screen = null;
            if (TryScreen(out ScreenAddress here)) screen = here;
            LairRooms lair = arena.Lair;
            if (screen.HasValue && lair != null && lair.LeadsToLair(here.Face))
            {
                if (!lair.Enter(transform.position, here, navigator)) return false;
                Entries++;
                return true;
            }
            if (!arena.Enter(navigator, screen)) return false;
            if (screen.HasValue && lair != null && arena.World.Lair != null) arena.World.LairProgress.LearnRealPortal();
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
            var ringRenderer = ring.AddComponent<SpriteRenderer>();
            ArtCatalog.Apply(ringRenderer, ArtKey.Portal, new Vector2(Radius * 2.6f, Radius * 2.6f));
            if (material != null) ringRenderer.sharedMaterial = material;
            ringRenderer.color = new Color(1f, 0.75f, 0.95f);
            ringRenderer.sortingOrder = 3;

            var swirl = new GameObject("Swirl");
            swirl.transform.SetParent(go.transform, false);
            swirl.transform.localScale = new Vector3(Radius * 0.9f, Radius * 0.9f, 1f);
            var swirlRenderer = swirl.AddComponent<SpriteRenderer>();
            swirlRenderer.sprite = ArtCatalog.Shape(PartShape.Diamond);
            if (material != null) swirlRenderer.sharedMaterial = material;
            swirlRenderer.color = new Color(1f, 0.3f, 0.75f, 0.45f);
            swirlRenderer.sortingOrder = 4;

            var portal = go.AddComponent<CorePortal>();
            portal.arena = owner;
            portal.swirl = swirl.transform;
            return portal;
        }
    }
}
