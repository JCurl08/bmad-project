using System;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Runtime cube world. Owns the CubeModel for the current seed and lays each face out as an
    /// N x N block of screens in its own world region (one empty screen column between faces), with
    /// placeholder floors tinted per theme. Sealed faces are drawn dark, and edges that lead into a
    /// sealed face get a wall.
    /// </summary>
    public class CubeWorld : MonoBehaviour
    {
        private const float WallThickness = 1f;

        [SerializeField] private int seed = 1234;
        [SerializeField] private Sprite sprite;
        [SerializeField] private Material material;

        private Transform generated;
        private static Sprite fallbackSprite;

        public CubeModel Model { get; private set; }
        public int Seed => Model != null ? Model.Seed : seed;
        /// <summary>N, read from the model (built from CubeSettings.DefaultFaceSize).</summary>
        public int FaceSize => Model != null ? Model.FaceSize : CubeSettings.DefaultFaceSize;

        /// <summary>One screen is ScreenMath.DefaultScreenSize (16x10); never duplicated here.</summary>
        public static Vector2 ScreenSize => ScreenMath.DefaultScreenSize;

        /// <summary>World size of one face: N screens by N screens.</summary>
        public Vector2 FaceExtent => ScreenSize * FaceSize;

        /// <summary>Raised after every (re)build, once the new model and visuals exist.</summary>
        public event Action Rebuilt;

        public void Configure(Sprite floorSprite, Material spriteMaterial, int startSeed)
        {
            sprite = floorSprite;
            material = spriteMaterial;
            seed = startSeed;
        }

        private void Awake()
        {
            if (Model == null) Rebuild(seed);
        }

        /// <summary>Builds the model for the given seed and regenerates every face's visuals.</summary>
        public void Rebuild(int newSeed)
        {
            seed = newSeed;
            Model = new CubeModel(newSeed, CubeSettings.DefaultFaceSize);

            if (generated != null) Destroy(generated.gameObject);
            generated = new GameObject("Generated Faces").transform;
            generated.SetParent(transform, false);

            for (int f = 0; f < CubeSettings.FaceCount; f++)
                BuildFace((FaceId)f);

            Rebuilt?.Invoke();
        }

        /// <summary>Lower-left corner of a face's region; aligned to the screen grid so ScreenCamera snaps cleanly.</summary>
        public Vector2 FaceOrigin(FaceId face)
        {
            return new Vector2((int)face * (FaceSize + 1) * ScreenSize.x, 0f);
        }

        public Vector2 ScreenCenter(ScreenAddress address)
        {
            return FaceOrigin(address.Face) + ScreenMath.ScreenCenter(address.Cell, ScreenSize);
        }

        /// <summary>The cell of the face that contains (or is nearest to) a world position.</summary>
        public Vector2Int CellAt(FaceId face, Vector2 worldPosition)
        {
            Vector2Int index = ScreenMath.ScreenIndex(worldPosition - FaceOrigin(face), ScreenSize);
            return new Vector2Int(Mathf.Clamp(index.x, 0, FaceSize - 1), Mathf.Clamp(index.y, 0, FaceSize - 1));
        }

        public static Color ThemeColor(Theme theme)
        {
            switch (theme)
            {
                case Theme.Town: return new Color(0.55f, 0.47f, 0.33f);
                case Theme.Biology: return new Color(0.22f, 0.50f, 0.25f);
                case Theme.Chemistry: return new Color(0.45f, 0.30f, 0.55f);
                case Theme.Physics: return new Color(0.22f, 0.38f, 0.62f);
                case Theme.Math: return new Color(0.60f, 0.55f, 0.25f);
                default: return new Color(0.30f, 0.55f, 0.60f);
            }
        }

        private void BuildFace(FaceId face)
        {
            Theme theme = Model.ThemeOf(face);
            bool sealedFace = Model.IsSealed(face);
            var root = new GameObject($"Face {face} ({theme}{(sealedFace ? ", sealed" : "")})").transform;
            root.SetParent(generated, false);

            Color baseColor = sealedFace ? Color.Lerp(ThemeColor(theme), Color.black, 0.8f) : ThemeColor(theme);
            for (int y = 0; y < FaceSize; y++)
            {
                for (int x = 0; x < FaceSize; x++)
                {
                    var address = new ScreenAddress(face, x, y);
                    // Checkerboard shading so neighbouring screens are distinguishable.
                    Color color = ((x + y) & 1) == 0 ? baseColor : Color.Lerp(baseColor, Color.black, 0.15f);
                    CreateBlock($"Screen {x},{y}", root, ScreenCenter(address), ScreenSize * 0.98f, color, -10, false);
                }
            }

            if (sealedFace) return;

            // Edges into a sealed face are walls (inside the face region, so the player never leaves it).
            Vector2 origin = FaceOrigin(face);
            Vector2 extent = FaceExtent;
            var wallColor = new Color(0.12f, 0.12f, 0.14f);
            for (int e = 0; e < 4; e++)
            {
                var edge = (Facing)e;
                if (!Model.IsSealed(CubeModel.NeighborFace(face, edge))) continue;

                Vector2 centre, size;
                switch (edge)
                {
                    case Facing.North:
                        centre = origin + new Vector2(extent.x / 2f, extent.y - WallThickness / 2f);
                        size = new Vector2(extent.x, WallThickness);
                        break;
                    case Facing.South:
                        centre = origin + new Vector2(extent.x / 2f, WallThickness / 2f);
                        size = new Vector2(extent.x, WallThickness);
                        break;
                    case Facing.East:
                        centre = origin + new Vector2(extent.x - WallThickness / 2f, extent.y / 2f);
                        size = new Vector2(WallThickness, extent.y);
                        break;
                    default:
                        centre = origin + new Vector2(WallThickness / 2f, extent.y / 2f);
                        size = new Vector2(WallThickness, extent.y);
                        break;
                }
                CreateBlock($"Sealed Wall {edge}", root, centre, size, wallColor, 0, true);
            }
        }

        private GameObject CreateBlock(string name, Transform parent, Vector2 centre, Vector2 size, Color color,
            int sortingOrder, bool collider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = centre;

            // Visual is a scaled child so the collider on the parent keeps world-unit sizes.
            Sprite s = sprite != null ? sprite : GetFallbackSprite();
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            Vector2 spriteSize = s.bounds.size;
            visual.transform.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = s;
            if (material != null) renderer.sharedMaterial = material;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;

            if (collider) go.AddComponent<BoxCollider2D>().size = size;
            return go;
        }

        private static Sprite GetFallbackSprite()
        {
            if (fallbackSprite != null) return fallbackSprite;
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            fallbackSprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return fallbackSprite;
        }
    }
}
