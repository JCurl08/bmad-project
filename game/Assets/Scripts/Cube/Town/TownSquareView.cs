using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A town built on a town module (4.5) from its race's TownLayout, when CubeWorld places the module: the plaza's paving,
    /// each building (a solid collider over its base and its art: Kenney tiles for the Town's cottages, TownArt for the
    /// rest), the statue in stone on its pedestal (solid), the marked door's TownDoor with its doormat and lantern, and a
    /// vent hut's bubbles. Everything lives under the module, so a rebuild removes it with the module.
    /// Once its face is repaired (4.8, CubeWorld.RepairFace) the statue is lit: a gold halo behind it, the stone warmed to
    /// gold, and a banner in the region's colour on each side. Art only, no colliders: a repair never moves anything.
    /// </summary>
    public class TownSquareView : MonoBehaviour
    {
        /// <summary>Sorting order of the town's art: over the floor, the plaza (PlazaOrder) and the edge walls and arches, under people.</summary>
        public const int ArtOrder = -3;

        /// <summary>
        /// Sorting order of the plaza's paving (the doormat draws one above it): over the floor (-10) and over the Earth calm
        /// eye's soft ring (EarthFace, -9), so where the eye hosts the Earth town its ring always lies under the paving.
        /// </summary>
        public const int PlazaOrder = -8;

        private readonly List<BoxCollider2D> buildings = new List<BoxCollider2D>();

        /// <summary>The town's layout (its race's).</summary>
        public TownLayout Layout { get; private set; }

        /// <summary>The town's race (its face theme).</summary>
        public Theme Theme => Layout != null ? Layout.Theme : Theme.Town;

        /// <summary>Each building's solid collider, in TownLayout.Buildings order.</summary>
        public IReadOnlyList<BoxCollider2D> Buildings => buildings;

        /// <summary>The statue's solid collider.</summary>
        public BoxCollider2D Statue { get; private set; }

        /// <summary>The marked door (story 4.6 makes it enterable).</summary>
        public TownDoor MarkedDoor { get; private set; }

        /// <summary>The tint a lit statue's stone takes (4.8).</summary>
        public static readonly Color LitStatueTint = new Color(1f, 0.9f, 0.55f);

        /// <summary>A repair banner's scale (its art is 12 x 26 pixels at 8 a unit).</summary>
        public const float BannerScale = 0.8f;

        private SpriteRenderer statueArt;
        private GameObject repairArt;
        private bool repaired;

        /// <summary>The repair's art (halo and banners), or null until the face is repaired.</summary>
        public GameObject RepairArt => repairArt;

        /// <summary>The statue's art.</summary>
        public SpriteRenderer StatueArt => statueArt;

        /// <summary>True once its face is repaired (4.8; since 4.14 its temple's gem taken): the statue is lit and the banners fly.</summary>
        public bool Repaired
        {
            get => repaired;
            set
            {
                repaired = value;
                if (value && repairArt == null) BuildRepairArt();
                if (repairArt != null) repairArt.SetActive(value);
                if (statueArt != null) statueArt.color = value ? LitStatueTint : Color.white;
                ShowTemple();
            }
        }

        private bool templeUnsealed;
        private SpriteRenderer templeArt;

        /// <summary>The temple's door (4.14): where the player goes in once the temple is unsealed.</summary>
        public TempleDoor TempleDoor { get; private set; }

        /// <summary>The temple's art (sealed, open or mended).</summary>
        public SpriteRenderer TempleArtRenderer => templeArt;

        /// <summary>True once the face's fragments are handed in (4.14): the seal is gone and the doorway glows.</summary>
        public bool TempleUnsealed
        {
            get => templeUnsealed;
            set
            {
                templeUnsealed = value;
                ShowTemple();
            }
        }

        /// <summary>How the temple looks now.</summary>
        public TempleLook TempleLook => repaired ? TempleLook.Mended : templeUnsealed ? TempleLook.Open : TempleLook.Sealed;

        private void ShowTemple()
        {
            if (templeArt != null && Layout != null) templeArt.sprite = TempleArt.Building(Layout.Theme, TempleLook);
            if (TempleDoor != null) TempleDoor.Unsealed = templeUnsealed || repaired;
        }

        /// <summary>The halo behind the statue and a banner each side of it (no colliders).</summary>
        private void BuildRepairArt()
        {
            if (Layout == null) return;
            Material material = statueArt != null ? statueArt.sharedMaterial : null;
            repairArt = new GameObject("Repair");
            repairArt.transform.SetParent(transform, false);
            Rect art = Layout.StatueArt;
            SpriteRenderer halo = NewRenderer("Halo", repairArt.transform, art.center, material, ArtOrder - 1);
            halo.sprite = TownArt.RepairHalo();
            float across = Mathf.Max(art.width, art.height) * 1.5f;
            halo.transform.localScale = Vector3.one * (across / (halo.sprite.rect.width / halo.sprite.pixelsPerUnit));
            foreach (float side in new[] { -1f, 1f })
            {
                // Poles at the statue's shoulders, the cloths flying outwards, clear of the leader's stand to the east.
                SpriteRenderer banner = NewRenderer(side < 0f ? "Banner West" : "Banner East", repairArt.transform,
                    new Vector2(side < 0f ? art.xMin + 0.2f : art.xMax - 0.2f, art.yMax - 0.3f), material, ArtOrder + 1);
                banner.sprite = TownArt.RepairBanner(Layout.Theme);
                banner.transform.localScale = Vector3.one * BannerScale;
                if (side < 0f) banner.flipX = true;
            }
        }

        /// <summary>Builds the town of a town module's theme on it (once). Returns the view, or null for any other module.</summary>
        public static TownSquareView Build(ScreenModule module, Material material)
        {
            if (module == null || !module.IsTownScreen) return null;
            TownSquareView existing = module.GetComponentInChildren<TownSquareView>(true);
            if (existing != null) return existing;
            var go = new GameObject("Town");
            go.transform.SetParent(module.transform, false);
            go.transform.localPosition = Vector3.zero;
            TownSquareView view = go.AddComponent<TownSquareView>();
            view.Lay(TownLayout.For(module.Theme), material);
            return view;
        }

        private void Lay(TownLayout layout, Material material)
        {
            Layout = layout;

            // The plaza's paving, tiled over the crossroads and the south half.
            SpriteRenderer plaza = NewRenderer("Plaza", transform, layout.Plaza.center, material, PlazaOrder);
            plaza.sprite = TownArt.Paving();
            plaza.drawMode = SpriteDrawMode.Tiled;
            plaza.tileMode = SpriteTileMode.Continuous;
            plaza.size = layout.Plaza.size;

            for (int i = 0; i < layout.Buildings.Count; i++)
            {
                TownBuilding b = layout.Buildings[i];
                var root = new GameObject($"Building {i} {b.Kind}");
                root.transform.SetParent(transform, false);
                root.transform.localPosition = b.Solid.center;
                var solid = root.AddComponent<BoxCollider2D>();
                solid.size = b.Solid.size;
                buildings.Add(solid);
                Vector2 artCentre = b.Art.center - b.Solid.center;
                if (b.Kind == TownBuildingKind.Temple)
                {
                    // 4.14: the temple, in the race's style, sealed until the face's fragments are handed in.
                    templeArt = NewRenderer("Art", root.transform, artCentre, material, ArtOrder);
                    TempleDoor = AddTempleDoor(b);
                    ShowTemple();
                }
                else if (!TownArt.IsKenney(b.Kind) || !LayKenneyCottage(root.transform, b, artCentre, material))
                {
                    SpriteRenderer art = NewRenderer("Art", root.transform, artCentre, material, ArtOrder);
                    art.sprite = TownArt.Building(b.Kind);
                }
                if (b.Kind == TownBuildingKind.VentHut)
                {
                    var vent = new GameObject("Vent Bubbles");
                    vent.transform.SetParent(root.transform, false);
                    vent.transform.localPosition = artCentre + TownArt.VentTop;
                    vent.AddComponent<TownBubbles>().Configure(material);
                }
                if (b.Kind == TownBuildingKind.MarketStall)
                {
                    // The market's cart, parked by the stall (Kenney), on the side away from the crossroads.
                    float side = b.Solid.center.x >= 0f ? 1f : -1f;
                    Vector2 at = new Vector2(side * (b.Solid.width / 2f - 0.6f), -b.Solid.height / 2f + 0.55f);
                    ArtCatalog.AddSprite(root.transform, "Cart", ArtKey.MarketCart, at, Vector2.one * 1.1f, Color.white, ArtOrder + 1, material, false);
                }
                if (b.Enterable) MarkedDoor = AddDoor(b, material);
            }

            // The statue: its leader in stone on a pedestal, solid at its base.
            var statue = new GameObject("Statue");
            statue.transform.SetParent(transform, false);
            statue.transform.localPosition = layout.StatueSolid.center;
            Statue = statue.AddComponent<BoxCollider2D>();
            Statue.size = layout.StatueSolid.size;
            SpriteRenderer figure = NewRenderer("Art", statue.transform, layout.StatueArt.center - layout.StatueSolid.center, material, ArtOrder);
            figure.sprite = TownArt.Statue(layout.Theme);
            statueArt = figure;
        }

        /// <summary>A Town cottage from Kenney tiles (two roof tiles over a door tile and a wall tile); false without the art.</summary>
        private static bool LayKenneyCottage(Transform root, TownBuilding b, Vector2 artCentre, Material material)
        {
            bool stone = b.Kind == TownBuildingKind.StoneCottage;
            ArtKey[] tiles = stone
                ? new[] { ArtKey.HouseRoofGreyLeft, ArtKey.HouseRoofGreyRight, ArtKey.HouseWallStoneDoor, ArtKey.HouseWallStoneRight }
                : new[] { ArtKey.HouseRoofRedLeft, ArtKey.HouseRoofRedRight, ArtKey.HouseWallWoodDoor, ArtKey.HouseWallWoodRight };
            foreach (ArtKey key in tiles)
                if (!ArtCatalog.TryGet(key, out _)) return false;
            float tile = b.Art.width / 2f;
            Vector2 bottomLeft = artCentre - b.Art.size / 2f;
            for (int i = 0; i < tiles.Length; i++)
            {
                int column = i % 2, row = i < 2 ? 1 : 0;
                Vector2 at = bottomLeft + new Vector2((column + 0.5f) * tile, (row + 0.5f) * (b.Art.height / 2f));
                ArtCatalog.AddSprite(root, $"Tile {tiles[i]}", tiles[i], at, new Vector2(tile, b.Art.height / 2f), Color.white, ArtOrder,
                    material, false);
            }
            return true;
        }

        private TempleDoor AddTempleDoor(TownBuilding b)
        {
            var go = new GameObject("Temple Door");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = b.Door;
            TempleDoor door = go.AddComponent<TempleDoor>();
            door.Configure(b.DoorFacing, Layout.Theme);
            return door;
        }

        private TownDoor AddDoor(TownBuilding b, Material material)
        {
            var go = new GameObject("Marked Door");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = b.Door;
            TownDoor door = go.AddComponent<TownDoor>();
            door.Configure(b.DoorFacing, b.Kind, Layout.Theme);
            SpriteRenderer mat = NewRenderer("Doormat", go.transform, new Vector2(0f, -0.3f), material, PlazaOrder + 1);
            mat.sprite = TownArt.Doormat();
            SpriteRenderer lantern = NewRenderer("Lantern", go.transform, new Vector2(0.95f, 1.3f), material, ArtOrder + 1);
            lantern.sprite = TownArt.Lantern();
            return door;
        }

        private static SpriteRenderer NewRenderer(string name, Transform parent, Vector2 localPosition, Material material, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var renderer = go.AddComponent<SpriteRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.color = Color.white;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}
