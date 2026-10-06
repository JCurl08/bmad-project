using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The one place sprites come from. A ScriptableObject (Resources/ArtCatalog, built by Cube > Build Art Catalog) maps
    /// each ArtKey to a sprite (Kenney Tiny Dungeon / Tiny Town tiles, imported as 16 px per unit pixel art, see
    /// Assets/Art/ART-MAP.md). Get(key) returns that sprite, or, for a key with no mapping (or no catalog at all), logs
    /// once and returns the key's generated placeholder shape, so nothing ever renders invisible. Shape(shape) gives the
    /// generated shapes themselves (marks, pips, effects), and NpcPart(...) the outlined, race-specific NPC parts
    /// (NpcPartArt). The catalog also holds the white hit-flash material (HitFlash).
    /// </summary>
    [CreateAssetMenu(menuName = "Cube/Art Catalog", fileName = "ArtCatalog")]
    public class ArtCatalog : ScriptableObject
    {
        /// <summary>Resources path of the catalog asset.</summary>
        public const string ResourcePath = "ArtCatalog";

        [Serializable]
        public struct Entry
        {
            public ArtKey Key;
            public Sprite Sprite;

            [Tooltip("The source tile, e.g. 'Tiny Dungeon/tile_0112' (documentation and the duplicate check).")]
            public string Tile;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        [SerializeField] private Material flashMaterial;

        private Dictionary<ArtKey, Entry> lookup;

        private static ArtCatalog instance;
        private static bool loaded;
        private static readonly HashSet<ArtKey> Missing = new HashSet<ArtKey>();
        private static readonly Dictionary<PartShape, Sprite> Shapes = new Dictionary<PartShape, Sprite>();
        private static Material runtimeFlash;

        public IReadOnlyList<Entry> Entries => entries;

        public Material FlashMaterial => flashMaterial;

        /// <summary>The project's catalog (loaded from Resources once), or null if it has not been built.</summary>
        public static ArtCatalog Instance
        {
            get
            {
                if (instance != null || loaded) return instance;
                loaded = true;
                instance = Resources.Load<ArtCatalog>(ResourcePath);
                if (instance == null) Debug.LogWarning("ArtCatalog: no catalog at Resources/" + ResourcePath + "; every sprite uses its generated shape.");
                return instance;
            }
        }

        /// <summary>Replaces the catalog in use (tests and tools); null goes back to loading the project's.</summary>
        public static void Use(ArtCatalog catalog)
        {
            instance = catalog;
            loaded = catalog != null;
            Missing.Clear();
        }

        /// <summary>Sets the mapping (the builder).</summary>
        public void Set(List<Entry> newEntries, Material flash)
        {
            entries = newEntries ?? new List<Entry>();
            flashMaterial = flash;
            lookup = null;
        }

        /// <summary>This catalog's sprite for a key, if it maps one.</summary>
        public bool TryGetSprite(ArtKey key, out Sprite sprite)
        {
            if (lookup == null)
            {
                lookup = new Dictionary<ArtKey, Entry>();
                foreach (Entry e in entries)
                    if (e.Sprite != null) lookup[e.Key] = e;
            }
            sprite = lookup.TryGetValue(key, out Entry entry) ? entry.Sprite : null;
            return sprite != null;
        }

        /// <summary>True if the project's catalog maps the key to art.</summary>
        public static bool TryGet(ArtKey key, out Sprite sprite)
        {
            ArtCatalog catalog = Instance;
            if (catalog != null && catalog.TryGetSprite(key, out sprite)) return true;
            sprite = null;
            return false;
        }

        /// <summary>The key's art, or (logged once per key) its generated placeholder shape. Never null.</summary>
        public static Sprite Get(ArtKey key) => Get(key, ArtKeys.FallbackShape(key));

        /// <summary>The key's art, or (logged once per key) the given generated shape. Never null.</summary>
        public static Sprite Get(ArtKey key, PartShape fallback)
        {
            if (TryGet(key, out Sprite sprite)) return sprite;
            if (key != ArtKey.None && Missing.Add(key))
                Debug.LogWarning($"ArtCatalog: no art for {key}; using a generated {fallback}.");
            return Shape(fallback);
        }

        /// <summary>Keys that had no art when asked for (each logged once).</summary>
        public static IReadOnlyCollection<ArtKey> MissingKeys => Missing;

        /// <summary>The white material a HitFlash swaps in: the catalog's, else one made from the shader (null if neither exists).</summary>
        public static Material Flash
        {
            get
            {
                ArtCatalog catalog = Instance;
                if (catalog != null && catalog.flashMaterial != null) return catalog.flashMaterial;
                if (runtimeFlash != null) return runtimeFlash;
                Shader shader = Shader.Find(HitFlash.ShaderName);
                if (shader != null) runtimeFlash = new Material(shader) { name = "Hit Flash (runtime)" };
                return runtimeFlash;
            }
        }

        /// <summary>
        /// Points a renderer at a key's art at a world size: tiled keys (walls, floors, gates; or as tiled says) repeat the
        /// tile over the size (one tile per world unit), the rest are scaled to it. Without art, the generated shape
        /// (fallback, else the key's own) is scaled to the size.
        /// </summary>
        public static void Apply(SpriteRenderer renderer, ArtKey key, Vector2 size, bool? tiled = null, PartShape? fallback = null)
        {
            if (renderer == null) return;
            bool art = TryGet(key, out Sprite sprite);
            if (!art) sprite = Get(key, fallback ?? ArtKeys.FallbackShape(key));
            renderer.sprite = sprite;
            if (art && (tiled ?? ArtKeys.IsTiled(key)))
            {
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.tileMode = SpriteTileMode.Continuous;
                renderer.size = size;
                renderer.transform.localScale = Vector3.one;
                return;
            }
            renderer.drawMode = SpriteDrawMode.Simple;
            Vector2 bounds = sprite.bounds.size;
            renderer.transform.localScale = new Vector3(size.x / bounds.x, size.y / bounds.y, 1f);
        }

        /// <summary>A child sprite of a key's art (or its fallback shape) at a size, colour and sorting order.</summary>
        public static SpriteRenderer AddSprite(Transform parent, string name, ArtKey key, Vector2 localPosition, Vector2 size,
            Color color, int sortingOrder, Material material, bool? tiled = null, PartShape? fallback = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var renderer = go.AddComponent<SpriteRenderer>();
            Apply(renderer, key, size, tiled, fallback);
            if (material != null) renderer.sharedMaterial = material;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        /// <summary>A child sprite of a generated shape (marks, pips, effects), one world unit across before scaling.</summary>
        public static SpriteRenderer AddShape(Transform parent, string name, PartShape shape, Vector2 localPosition, Vector2 size,
            Color color, int sortingOrder, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Shape(shape);
            if (material != null) renderer.sharedMaterial = material;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        /// <summary>
        /// Sorting order of dressed gate art: above everything that can sit in its pocket (pickups, the isotope dispenser,
        /// hidden Jumbles), so a closed gate is always drawn in front of what it guards. Its marks draw one above it.
        /// </summary>
        public const int GateArtOrder = 7;

        /// <summary>How far dressed gate art reaches from its slot into the pocket: one tile, less the half-thickness in front.</summary>
        public static float GateArtReach(float thickness) => 1f - thickness / 2f;

        /// <summary>Child names of gate dressing that follow the gate's art when it is moved into the alcove.</summary>
        private static readonly string[] GateMarks = { "Beak Mark", "Stage Mark", "Crack", "Mass Pip" };

        /// <summary>
        /// Dresses a gate's visual with its kind's art: the tile repeated along the gate (one tile deep), set back into the
        /// alcove so its lane-side edge sits on the (unchanged) collider, with its marks moved onto it. pocketDirection points
        /// from the gate into the pocket; when null it comes from the GateSlot the gate sits in (none: centred). Without
        /// art the placeholder block is left as it is.
        /// </summary>
        public static void DressGate(Gate gate, ArtKey key, Vector2? pocketDirection = null)
        {
            if (gate == null || gate.Visual == null || !TryGet(key, out Sprite sprite)) return;
            Vector2 size = gate.Solid.size;
            bool across = size.x >= size.y;
            float length = across ? size.x : size.y;
            float thickness = across ? size.y : size.x;
            SpriteRenderer visual = gate.Visual;
            visual.sprite = sprite;
            visual.drawMode = SpriteDrawMode.Tiled;
            visual.tileMode = SpriteTileMode.Continuous;
            visual.size = across ? new Vector2(length, 1f) : new Vector2(1f, length);
            visual.transform.localScale = Vector3.one;
            visual.transform.localRotation = Quaternion.identity;
            visual.sortingOrder = GateArtOrder;

            Vector2 dir = pocketDirection ?? SlotPocketDirection(gate);
            float component = across ? dir.y : dir.x;
            float side = Mathf.Abs(component) > 1e-4f ? Mathf.Sign(component) : 0f;
            Vector2 offset = (across ? Vector2.up : Vector2.right) * (side * (1f - thickness) / 2f);
            visual.transform.localPosition = new Vector3(offset.x, offset.y, visual.transform.localPosition.z);
            foreach (Transform child in gate.transform)
            {
                if (child == visual.transform) continue;
                foreach (string mark in GateMarks)
                {
                    if (!child.name.StartsWith(mark, StringComparison.Ordinal)) continue;
                    child.localPosition += (Vector3)offset;
                    if (child.TryGetComponent(out SpriteRenderer markRenderer)) markRenderer.sortingOrder = GateArtOrder + 1;
                    break;
                }
            }
            gate.ArtDressed = true;
            gate.RefreshVisual();
        }

        private static Vector2 SlotPocketDirection(Gate gate)
        {
            GateSlot slot = gate.GetComponentInParent<GateSlot>();
            return slot != null ? slot.PocketOffset : Vector2.zero;
        }

        /// <summary>A white generated shape, one world unit across (32 px), made in code once.</summary>
        public static Sprite Shape(PartShape shape)
        {
            if (Shapes.TryGetValue(shape, out Sprite cached) && cached != null) return cached;
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[size * size];
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    float u = (px + 0.5f) / size * 2f - 1f; // -1..1
                    float v = (py + 0.5f) / size * 2f - 1f;
                    bool inside;
                    switch (shape)
                    {
                        case PartShape.Circle: inside = u * u + v * v <= 1f; break;
                        case PartShape.Triangle: inside = Mathf.Abs(u) <= (1f - v) / 2f; break; // apex up
                        case PartShape.Diamond: inside = Mathf.Abs(u) + Mathf.Abs(v) <= 1f; break;
                        default: inside = true; break;
                    }
                    pixels[py * size + px] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = $"Shape {shape}";
            Shapes[shape] = sprite;
            return sprite;
        }

        /// <summary>The outlined, race-specific sprite of an NPC part (generated in code, see NpcPartArt).</summary>
        public static Sprite NpcPart(Race race, NpcPart part) => NpcPartArt.SpriteFor(race, part);
    }
}
