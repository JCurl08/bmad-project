using System;
using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Runtime cube world. Owns the CubeModel for the current seed and lays each face out as an
    /// N x N block of screens in its own world region (one empty screen column between faces). Edges
    /// that lead into a sealed face get a wall.
    /// A run starts with only Town laid out (its fixed modules); the built science faces stay dark and
    /// unpatterned until RevealScience() (called on the first crossing off Town) lays them out from
    /// CubeLayout and instantiates their modules. Sealed faces are drawn dark and never laid out.
    /// </summary>
    public class CubeWorld : MonoBehaviour
    {
        private const float WallThickness = 1f;

        [SerializeField] private int seed = 1234;
        [SerializeField] private Sprite sprite;
        [SerializeField] private Material material;
        [SerializeField] private ModuleLibrary library;

        private Transform generated;
        private readonly Transform[] floorRoots = new Transform[CubeSettings.FaceCount];
        private readonly Transform[] moduleRoots = new Transform[CubeSettings.FaceCount];
        private readonly Dictionary<ScreenAddress, ScreenModule> modules = new Dictionary<ScreenAddress, ScreenModule>();
        private static Sprite fallbackSprite;

        public CubeModel Model { get; private set; }
        public int Seed => Model != null ? Model.Seed : seed;
        /// <summary>N, read from the model (built from CubeSettings.DefaultFaceSize).</summary>
        public int FaceSize => Model != null ? Model.FaceSize : CubeSettings.DefaultFaceSize;

        public ModuleLibrary Library => library;

        /// <summary>The science layout of this run; null until the science faces are revealed.</summary>
        public CubeLayout Layout { get; private set; }

        /// <summary>True once the built science faces have been laid out in this run.</summary>
        public bool ScienceRevealed { get; private set; }

        /// <summary>Sprite and material used for generated visuals (also used by debug markers).</summary>
        public Sprite Sprite => sprite != null ? sprite : GetFallbackSprite();
        public Material Material => material;

        /// <summary>One screen is ScreenMath.DefaultScreenSize (16x10); never duplicated here.</summary>
        public static Vector2 ScreenSize => ScreenMath.DefaultScreenSize;

        /// <summary>World size of one face: N screens by N screens.</summary>
        public Vector2 FaceExtent => ScreenSize * FaceSize;

        /// <summary>Raised after every (re)build, once the new model and visuals exist.</summary>
        public event Action Rebuilt;

        /// <summary>Raised once per run, after the science faces have been laid out.</summary>
        public event Action Revealed;

        public void Configure(Sprite floorSprite, Material spriteMaterial, int startSeed, ModuleLibrary moduleLibrary)
        {
            sprite = floorSprite;
            material = spriteMaterial;
            seed = startSeed;
            library = moduleLibrary;
        }

        private void Awake()
        {
            if (Model == null) Rebuild(seed);
        }

        /// <summary>
        /// Starts a new run: builds the model for the given seed, lays out Town and leaves the science
        /// faces unrevealed.
        /// </summary>
        public void Rebuild(int newSeed)
        {
            seed = newSeed;
            Model = new CubeModel(newSeed, CubeSettings.DefaultFaceSize);
            Layout = null;
            ScienceRevealed = false;
            modules.Clear();

            if (generated != null) Destroy(generated.gameObject);
            generated = NewChild("Generated Faces", transform);

            for (int f = 0; f < CubeSettings.FaceCount; f++)
                BuildFace((FaceId)f);

            Rebuilt?.Invoke();
        }

        /// <summary>
        /// Lays out the built science faces from CubeLayout (seeded by the run seed only) and instantiates
        /// their modules, with exactly one active core-entrance slot per face. Does nothing if already
        /// revealed in this run. Returns true if it revealed.
        /// </summary>
        public bool RevealScience()
        {
            if (Model == null || ScienceRevealed) return false;
            ScienceRevealed = true;
            if (library != null) Layout = CubeLayout.Generate(Model, library);
            else Debug.LogWarning("CubeWorld: no ModuleLibrary assigned; science faces revealed without modules.");

            for (int f = 0; f < CubeSettings.FaceCount; f++)
            {
                var face = (FaceId)f;
                if (!CubeLayout.IsLaidOut(Model, face)) continue;
                Transform parent = floorRoots[f].parent;
                Destroy(floorRoots[f].gameObject);
                floorRoots[f] = NewChild("Floor", parent);
                parent.name = $"Face {face} ({Model.ThemeOf(face)})";
                BuildFloor(face, floorRoots[f], ThemeColor(Model.ThemeOf(face)), true);
                BuildScienceModules(face);
            }

            Revealed?.Invoke();
            return true;
        }

        /// <summary>The module instance on a screen, or null (unrevealed or sealed face).</summary>
        public ScreenModule ModuleAt(ScreenAddress address) =>
            modules.TryGetValue(address, out ScreenModule module) ? module : null;

        /// <summary>Every module instance currently in the world.</summary>
        public IEnumerable<ScreenModule> AllModules => modules.Values;

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
            int f = (int)face;
            Theme theme = Model.ThemeOf(face);
            bool sealedFace = Model.IsSealed(face);
            bool town = face == CubeModel.StartFace;
            string state = sealedFace ? ", sealed" : town ? "" : ", unrevealed";
            Transform root = NewChild($"Face {face} ({theme}{state})", generated);
            floorRoots[f] = NewChild("Floor", root);
            moduleRoots[f] = NewChild("Modules", root);

            if (sealedFace)
            {
                BuildFloor(face, floorRoots[f], Color.Lerp(ThemeColor(theme), Color.black, 0.8f), true);
                return;
            }

            if (town)
            {
                BuildFloor(face, floorRoots[f], ThemeColor(theme), true);
                BuildTownModules();
            }
            else
            {
                // Unrevealed science face: dark and unpatterned until the player first leaves town.
                BuildFloor(face, floorRoots[f], new Color(0.08f, 0.08f, 0.09f), false);
            }

            BuildSealedWalls(face, root);
        }

        private void BuildFloor(FaceId face, Transform parent, Color baseColor, bool checkered)
        {
            for (int y = 0; y < FaceSize; y++)
            {
                for (int x = 0; x < FaceSize; x++)
                {
                    var address = new ScreenAddress(face, x, y);
                    // Checkerboard shading so neighbouring screens are distinguishable.
                    Color color = !checkered || ((x + y) & 1) == 0 ? baseColor : Color.Lerp(baseColor, Color.black, 0.15f);
                    CreateBlock($"Screen {x},{y}", parent, ScreenCenter(address), ScreenSize * 0.98f, color, -10, false);
                }
            }
        }

        private void BuildTownModules()
        {
            if (library == null) return;
            for (int y = 0; y < FaceSize; y++)
            {
                for (int x = 0; x < FaceSize; x++)
                {
                    var address = new ScreenAddress(CubeModel.StartFace, x, y);
                    ScreenModule prefab = library.TownModule(address.Cell, FaceSize);
                    if (prefab != null) PlaceModule(prefab, address, -1);
                }
            }
        }

        private void BuildScienceModules(FaceId face)
        {
            if (Layout == null || !Layout.TryGetFace(face, out FaceLayout faceLayout)) return;
            for (int y = 0; y < FaceSize; y++)
            {
                for (int x = 0; x < FaceSize; x++)
                {
                    var cell = new Vector2Int(x, y);
                    ScreenModule prefab = library.Module(faceLayout.Theme, faceLayout.ModuleAt(cell));
                    int core = cell == faceLayout.CoreCell ? faceLayout.CoreSlot : -1;
                    PlaceModule(prefab, new ScreenAddress(face, cell), core);
                }
            }
        }

        private void PlaceModule(ScreenModule prefab, ScreenAddress address, int activeCoreSlot)
        {
            ScreenModule module = Instantiate(prefab, moduleRoots[(int)address.Face]);
            module.name = $"{prefab.name} @ {address.Cell.x},{address.Cell.y}";
            module.transform.position = ScreenCenter(address);
            module.SetActiveCoreEntrance(activeCoreSlot);
            // Exits whose edge leads into a sealed face are closed (that edge is a wall).
            foreach (ExitSlot exit in module.Exits)
                exit.gameObject.SetActive(Model.TryStep(address, exit.Facing, out _, out _));
            modules[address] = module;
        }

        private void BuildSealedWalls(FaceId face, Transform root)
        {
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

        private static Transform NewChild(string name, Transform parent)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private GameObject CreateBlock(string name, Transform parent, Vector2 centre, Vector2 size, Color color,
            int sortingOrder, bool collider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = centre;

            // Visual is a scaled child so the collider on the parent keeps world-unit sizes.
            Sprite s = Sprite;
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
