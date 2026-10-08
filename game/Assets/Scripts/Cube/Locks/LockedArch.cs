using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Cube
{
    /// <summary>
    /// A locked face-edge arch (4.9): iron bars across a face-edge doorway (a solid collider over the doorway) with a plate in
    /// front showing what opens it: a gold keyhole (a key) or the item's own icon on a dark disc. A swing at it, or Interact
    /// within InteractRange, tries the lock (EdgeLockState.TryUnlock: a key is used up; an item is only shown); an item lock
    /// also opens when the player walks into it holding the item. A refusal pops up what it needs. Opening the edge opens
    /// every arch along it, on both faces (CubeWorld hooks EdgeLockState.Opened to ShowOpen).
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class LockedArch : MonoBehaviour, IAttackReceiver
    {
        /// <summary>How near the doorway the player must stand for Interact to try the lock.</summary>
        public const float InteractRange = 2.2f * ScreenMath.ContentScale;

        /// <summary>How far in from the edge the plate stands (in front of the arch, on the exit lane).</summary>
        public const float PlateInset = ScreenModule.EdgeClearance + 0.6f * ScreenMath.ContentScale;

        public const float PlateSize = 0.9f * ScreenMath.ContentScale;

        /// <summary>The fewest seconds between two "it needs ..." popups from one arch.</summary>
        public const float RefusalCooldown = 1.5f;

        private static readonly Color BarColor = new Color(0.22f, 0.2f, 0.26f);

        [SerializeField] private string interactActionPath = "Player/Interact";

        private readonly List<SpriteRenderer> art = new List<SpriteRenderer>();
        private InputAction interact;
        private float refusedAt = -10f;
        private Transform player;

        public CubeWorld World { get; private set; }
        public CubeEdge Edge { get; private set; }
        public ScreenAddress Screen { get; private set; }
        public Facing Side { get; private set; }

        /// <summary>The world point of the doorway's middle.</summary>
        public Vector2 DoorwayCentre { get; private set; }

        /// <summary>The plate's renderer (the keyhole, or the item's icon).</summary>
        public SpriteRenderer Plate { get; private set; }

        public bool IsOpen => World == null || World.Locks == null || !World.Locks.IsLocked(Edge);

        public EdgeLock Lock => World != null && World.Locks != null ? World.Locks.LockOf(Edge) : new EdgeLock(Edge, LockKind.Open);

        /// <summary>The last attempt's result (tests).</summary>
        public LockOpenResult LastResult { get; private set; } = LockOpenResult.AlreadyOpen;

        /// <summary>Builds a locked arch over a doorway of a screen (its art and solid; CubeWorld calls it while building the walls).</summary>
        public static LockedArch Build(CubeWorld world, Transform parent, ScreenAddress screen, Facing side, Material material)
        {
            Rect door = ScreenEdges.DoorwayRect(side);
            Vector2 centre = world.ScreenCenter(screen);
            var go = new GameObject($"Locked Arch {screen.Cell.x},{screen.Cell.y} {side}");
            go.transform.SetParent(parent, false);
            go.transform.position = centre + door.center;
            go.AddComponent<BoxCollider2D>().size = door.size;
            LockedArch arch = go.AddComponent<LockedArch>();
            arch.World = world;
            arch.Screen = screen;
            arch.Side = side;
            arch.Edge = CubeEdge.Of(screen.Face, side);
            arch.DoorwayCentre = centre + ScreenEdges.DoorwayCentre(side);
            arch.BuildArt(material, door.size);
            return arch;
        }

        private void BuildArt(Material material, Vector2 doorSize)
        {
            // Iron bars across the doorway: four uprights and a rail, inside the wall band.
            bool across = !Side.IsHorizontal(); // a north or south doorway runs along x
            float span = across ? doorSize.x : doorSize.y, depth = across ? doorSize.y : doorSize.x;
            for (int i = 0; i < 4; i++)
            {
                float t = -span / 2f + span * (i + 0.5f) / 4f;
                Vector2 at = across ? new Vector2(t, 0f) : new Vector2(0f, t);
                Vector2 size = across ? new Vector2(0.3f, depth + 0.3f) : new Vector2(depth + 0.3f, 0.3f);
                art.Add(Quad("Bar " + i, at, size, BarColor, -3, material));
            }
            art.Add(Quad("Rail", Vector2.zero, across ? new Vector2(span, 0.22f) : new Vector2(0.22f, span), BarColor, -3, material));

            // The plate in front: what opens it.
            Vector2 plateAt = -(Vector2)Side.ToVector() * (PlateInset - depth / 2f);
            EdgeLock l = Lock;
            if (l.Kind == LockKind.Item)
            {
                // The item's icon on a gold-rimmed lock plate (the keyhole plate's rim), so it never reads as a pickup.
                var back = new GameObject("Plate Back");
                back.transform.SetParent(transform, false);
                back.transform.localPosition = plateAt;
                back.transform.localScale = Vector3.one * 0.9f;
                var backRenderer = back.AddComponent<SpriteRenderer>();
                backRenderer.sprite = TownArt.LockPlate();
                if (material != null) backRenderer.sharedMaterial = material;
                backRenderer.sortingOrder = 5;
                art.Add(backRenderer);
                ItemDefinition item = World.ItemFor(l.Item);
                Plate = ArtCatalog.AddSprite(transform, "Plate", ArtKeys.Item(item), plateAt, Vector2.one * PlateSize, Color.white, 6, material);
            }
            else
            {
                var go = new GameObject("Plate");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = plateAt;
                go.transform.localScale = Vector3.one * 1.1f;
                Plate = go.AddComponent<SpriteRenderer>();
                Plate.sprite = TownArt.Keyhole(false);
                if (material != null) Plate.sharedMaterial = material;
                Plate.sortingOrder = 6;
            }
            art.Add(Plate);
            if (IsOpen) ShowOpen();
        }

        private SpriteRenderer Quad(string name, Vector2 at, Vector2 size, Color color, int order, Material material, Sprite sprite = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = at;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite != null ? sprite : ArtCatalog.Shape(PartShape.Square);
            Vector2 native = r.sprite.bounds.size;
            go.transform.localScale = new Vector3(size.x / native.x, size.y / native.y, 1f);
            if (material != null) r.sharedMaterial = material;
            r.color = color;
            r.sortingOrder = order;
            return r;
        }

        /// <summary>The arch is open: no bars, no plate, nothing solid.</summary>
        public void ShowOpen()
        {
            var solid = GetComponent<BoxCollider2D>();
            if (solid != null) solid.enabled = false;
            foreach (SpriteRenderer r in art)
                if (r != null) r.enabled = false;
        }

        private void OnEnable() => interact = NpcInput.Find(interactActionPath, this);

        private void OnDisable() => interact = null;

        /// <summary>Interact, after every Update: only a press nothing else takes (LockInput).</summary>
        private void LateUpdate()
        {
            if (interact == null || !interact.WasPressedThisFrame() || IsOpen) return;
            Transform p = ResolvePlayer();
            if (p == null || Vector2.Distance(p.position, DoorwayCentre) > InteractRange || !LockInput.PressIsFree(p) || !LockInput.Claim()) return;
            TryOpen(p.gameObject, true);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            // Walking into an item lock with the item opens it; anything else only says what it needs.
            if (IsOpen || ItemPickup.FindInventory(collision.collider) == null) return;
            GameObject who = collision.collider.attachedRigidbody != null ? collision.collider.attachedRigidbody.gameObject : collision.gameObject;
            if (Lock.Kind == LockKind.Item) TryOpen(who, true);
            else
            {
                RunRewards rewards = who.GetComponentInParent<RunRewards>();
                bool hasKey = rewards != null ? rewards.KeysHeld > 0 : World.Locks.StartKeysLeft > 0;
                if (hasKey) Hint(who, LockText.UseKey);
                else Refuse(who, LockOpenResult.NeedsKey);
            }
        }

        /// <summary>A swing tries the lock, for the swinger.</summary>
        public bool ReceiveAttack(ItemDefinition item, GameObject attacker)
        {
            if (IsOpen || attacker == null) return false;
            return TryOpen(attacker, true) == LockOpenResult.Opened;
        }

        /// <summary>Tries the lock for a player (its RunRewards keys, its Inventory items), with a popup when shown. Tests call it.</summary>
        public LockOpenResult TryOpen(GameObject who, bool popup = false)
        {
            if (World == null || World.Locks == null || who == null) return LastResult = LockOpenResult.AlreadyOpen;
            // 4.9 review: on a Math island screen, only from the island this doorway lands on (as the sweep's model, RegionOfDoor).
            if (!IsOpen && !SameRegion(who.transform.position)) return LastResult = LockOpenResult.OutOfReach;
            RunRewards rewards = who.GetComponentInParent<RunRewards>();
            Inventory inventory = who.GetComponentInParent<Inventory>();
            EdgeLock l = Lock;
            LastResult = World.Locks.TryUnlock(Edge, () => rewards != null && rewards.TrySpendKey(), theme =>
            {
                if (inventory == null) return false;
                foreach (ItemDefinition i in inventory.Items)
                    if (i != null && i.HomeTheme == theme) return true;
                return false;
            });
            if (!popup) return LastResult;
            if (LastResult == LockOpenResult.Opened)
                PickupPopup.Show(who.transform.position, l.Kind == LockKind.Item ? LockText.ItemOpened(ItemName(l.Item)) : LockText.KeyOpened,
                    RewardChest.PopupColor, 1.6f);
            else Refuse(who, LastResult);
            return LastResult;
        }

        /// <summary>
        /// True when a world point stands where this doorway's region is (4.9 review): anywhere on an ordinary screen; on an ice
        /// screen (4.10), its landing's region or its mouth; on a Math
        /// island screen, only on the island the doorway lands on (DoorwayReach.RegionOfDoor), so the runtime matches the model.
        /// </summary>
        public bool SameRegion(Vector2 worldPosition)
        {
            MathFace math = World != null ? World.GetComponent<MathFace>() : null;
            IScreenRegions regions = math != null && math.Plan != null ? math.Plan.IslandAt(Screen) : null;
            if (regions == null && World != null && World.Ice != null) regions = World.Ice.At(Screen); // 4.10: an ice screen
            if (regions == null && World != null && World.Wind != null) regions = World.Wind.At(Screen); // 4.11: a wind screen
            if (regions == null) return true;
            Vector2 local = worldPosition - World.ScreenCenter(Screen);
            int door = regions.DoorRegion(Side);
            // On an ice screen the doorway's mouth (the strip off the grid, between the arch and its landing) is its landing's.
            if (regions is IceLayout && IceLayout.CellAt(local) < 0) return door >= 0;
            if (regions is WindLayout && WindLayout.CellAt(local) < 0) return door >= 0;
            return door >= 0 && regions.RegionAt(local) == door;
        }

        private void Refuse(GameObject who, LockOpenResult result)
        {
            if (result != LockOpenResult.NeedsKey && result != LockOpenResult.NeedsItem) return;
            Hint(who, result == LockOpenResult.NeedsKey ? LockText.NeedsKey : LockText.NeedsItem(ItemName(Lock.Item)));
        }

        private void Hint(GameObject who, string text)
        {
            if (Time.time - refusedAt < RefusalCooldown) return;
            refusedAt = Time.time;
            PickupPopup.Show(who.transform.position, text, new Color(1f, 0.85f, 0.5f), 1.6f);
        }

        private string ItemName(Theme theme)
        {
            ItemDefinition item = World != null ? World.ItemFor(theme) : null;
            return item != null ? item.ToString() : theme.ToString();
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
