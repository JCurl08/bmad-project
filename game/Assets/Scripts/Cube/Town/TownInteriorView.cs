using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// One town's room (4.6), built from its race's TownInterior on its own screen below the faces (TownInteriors builds
    /// it the first time the player goes in, and removes it on a rebuild): the floor and walls in the race's tiles
    /// (InteriorArt), solid walls with the exit's gap and vestibule, the furniture (solid where its layout says), a doormat
    /// and an arrow at the exit, and PeopleCount people of the race on their stands, each wandering in their own box. A
    /// shop's keeper stands behind the counter and talks shop in the race's voice (Dialogue.KeeperLines); everyone else
    /// talks like the town's residents (a role line, then a run-true hint). No gates, rewards or enemies.
    /// </summary>
    public class TownInteriorView : MonoBehaviour
    {
        /// <summary>Sorting orders: the floor, rugs over it, the walls and furniture under people (NpcFactory.SortingOrder 6).</summary>
        public const int FloorOrder = -10, RugOrder = -9, WallOrder = -4, FurnitureOrder = -3;

        private readonly List<NpcTalker> people = new List<NpcTalker>();
        private readonly List<BoxCollider2D> solids = new List<BoxCollider2D>();

        /// <summary>The room's layout (its race's).</summary>
        public TownInterior Interior { get; private set; }

        /// <summary>The face whose town this room belongs to.</summary>
        public FaceId Face { get; private set; }

        /// <summary>The town's screen (the screen the map shows while the player is inside).</summary>
        public ScreenAddress Town { get; private set; }

        /// <summary>The room's centre (world): its screen's centre.</summary>
        public Vector2 Centre => transform.position;

        /// <summary>Where the player arrives (world).</summary>
        public Vector2 EntryPoint => Centre + TownInterior.EntryPoint;

        /// <summary>The walkable room (world).</summary>
        public Rect Inner => new Rect(Centre + TownInterior.Inner.position, TownInterior.Inner.size);

        /// <summary>The room's people (the keeper first in a shop); destroyed ones become null.</summary>
        public IReadOnlyList<NpcTalker> People => people;

        /// <summary>The shop's keeper, or null.</summary>
        public NpcTalker Keeper { get; private set; }

        /// <summary>Every solid collider of the room: walls first, then furniture.</summary>
        public IReadOnlyList<BoxCollider2D> Solids => solids;

        /// <summary>True when the player's centre has walked out through the exit (below its line, inside the vestibule).</summary>
        public bool IsPastExit(Vector2 world)
        {
            return TownInterior.IsPastExit(world - Centre);
        }

        /// <summary>Builds a town's room at a centre (world), its people talking to player.</summary>
        public static TownInteriorView Build(CubeWorld world, FaceId face, ScreenAddress town, Vector2 centre, Transform parent,
            Transform player, HintDensity density)
        {
            Theme theme = world.Model.ThemeOf(face);
            var go = new GameObject($"Interior {face} ({theme})");
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            TownInteriorView view = go.AddComponent<TownInteriorView>();
            view.Face = face;
            view.Town = town;
            view.Lay(TownInterior.For(theme), world.Material);
            view.Populate(world, player, density);
            return view;
        }

        private void Lay(TownInterior interior, Material material)
        {
            Interior = interior;
            Theme theme = interior.Theme;
            Rect inner = TownInterior.Inner;

            SpriteRenderer floor = Tiled("Floor", inner, InteriorArt.Floor(theme), FloorOrder, material);
            floor.color = Color.white;
            SpriteRenderer vestibule = Tiled("Vestibule Floor", TownInterior.Vestibule, InteriorArt.Floor(theme), FloorOrder, material);
            vestibule.color = new Color(0.6f, 0.6f, 0.6f);

            // Solid walls in the race's wall tile, the north (back) wall drawn taller, as seen from the front.
            Sprite wallTile = InteriorArt.Wall(theme);
            List<Rect> walls = TownInterior.Walls;
            for (int i = 0; i < walls.Count; i++)
            {
                Rect solid = walls[i];
                var wall = new GameObject($"Wall {i}");
                wall.transform.SetParent(transform, false);
                wall.transform.localPosition = solid.center;
                var box = wall.AddComponent<BoxCollider2D>();
                box.size = solid.size;
                solids.Add(box);
                Rect art = i == 0 ? Rect.MinMaxRect(solid.xMin, solid.yMin, solid.xMax, TownInterior.RoomHalf.y + TownInterior.NorthWallHeight) : solid;
                if (i == 1 || i == 2) art = Rect.MinMaxRect(art.xMin, art.yMin, art.xMax, TownInterior.RoomHalf.y + TownInterior.NorthWallHeight);
                SpriteRenderer r = Tiled("Art", art, wallTile, WallOrder, material, wall.transform, solid.center);
                r.color = Color.white;
                // A dark cap on the wall's outer side, so the room reads as one block against the dark.
                Rect cap = i == 0 ? Rect.MinMaxRect(art.xMin, art.yMax - 0.125f, art.xMax, art.yMax) : art;
                if (i == 0) ArtCatalog.AddShape(wall.transform, "Cap", PartShape.Square, cap.center - solid.center, cap.size,
                    TownArt.OutlineColor, WallOrder + 1, material);
            }

            // The exit: a dark threshold in the gap, the doormat inside it and an arrow pointing out.
            Vector2 gap = new Vector2(0f, -TownInterior.RoomHalf.y - TownInterior.WallThickness / 2f);
            SpriteRenderer mat = Renderer("Exit Doormat", new Vector2(0f, -TownInterior.RoomHalf.y + 0.35f), RugOrder + 1, material, transform);
            mat.sprite = TownArt.Doormat();
            SpriteRenderer arrow = ArtCatalog.AddShape(transform, "Exit Arrow", PartShape.Triangle, gap + new Vector2(0f, -0.9f),
                new Vector2(0.75f, 0.7f), new Color(1f, 0.95f, 0.6f, 0.85f), FurnitureOrder, material);
            arrow.transform.localRotation = Quaternion.Euler(0f, 0f, 180f); // apex down: out through the exit

            foreach (InteriorFurniture piece in interior.Furniture)
            {
                var go = new GameObject(piece.Piece.ToString());
                go.transform.SetParent(transform, false);
                go.transform.localPosition = piece.IsSolid ? piece.Solid.center : piece.Art.center;
                if (piece.IsSolid)
                {
                    var box = go.AddComponent<BoxCollider2D>();
                    box.size = piece.Solid.size;
                    solids.Add(box);
                }
                bool flat = piece.Piece == InteriorPiece.Rug || piece.Piece == InteriorPiece.Carpet;
                int order = flat ? RugOrder : piece.Piece == InteriorPiece.Banner ? WallOrder + 2 : FurnitureOrder;
                SpriteRenderer art = Renderer("Art", piece.Art.center - (Vector2)go.transform.localPosition, order, material, go.transform);
                art.sprite = InteriorArt.Piece(piece.Piece, theme, piece.Art.size);
                Vector2 drawn = art.sprite.bounds.size;
                art.transform.localScale = new Vector3(piece.Art.width / drawn.x, piece.Art.height / drawn.y, 1f);
            }
        }

        private void Populate(CubeWorld world, Transform player, HintDensity density)
        {
            Race race = TownSquare.RaceOf(Interior.Theme);
            RunFacts facts = RunFacts.Of(world);
            // The face's errand-giver, whom a spare quest-giver points to (4.18).
            ErrandPlan errand = Interior.Theme == Theme.Town
                ? (FindAnyObjectByType<TownPopulation>() is TownPopulation town && town != null ? town.Errand : null)
                : Errands.For(world.Seed, world.FaceSize, Face, Interior.Theme);
            string errandGiver = errand?.Giver.Name;
            // Town's people never turn hostile (4.18 review: Town is where everyone still mixes).
            RaceRelations relations = Interior.Theme == Theme.Town ? null : world.Relations;
            for (int i = 0; i < Interior.Stands.Count; i++)
            {
                InteriorStand stand = Interior.Stands[i];
                // 4.18: Town's own building is run by folk of every race, like its square (only the greeter and the Mayor look Town's own).
                Race person = Interior.Theme == Theme.Town ? TownPlan.InteriorRace(world.Seed, i) : race;
                NpcSpec spec = NpcFactory.ChooseParts(world.Seed, person, TownInterior.IndexBase + i);
                List<string> lines;
                if (stand.Keeper)
                {
                    spec = spec.Named(spec.Name, $"{NpcSpec.RaceName(person, spec.Variant).ToLowerInvariant()} shopkeeper");
                    lines = Dialogue.KeeperLines(spec.Voice);
                }
                else lines = Dialogue.For(spec, facts, density, errandGiver);
                var bounds = new Rect(Centre + stand.Bounds.position, stand.Bounds.size);
                NpcTalker npc = NpcFactory.Spawn(spec, lines, Centre + stand.Feet, bounds, relations, player, transform, world.Material);
                npc.name = $"Interior {(stand.Keeper ? "Keeper" : "Person")} {Face} {i} {spec.DisplayName}";
                people.Add(npc);
                if (stand.Keeper) Keeper = npc;
            }
        }

        private SpriteRenderer Tiled(string name, Rect local, Sprite tile, int order, Material material, Transform parent = null,
            Vector2 parentLocal = default)
        {
            SpriteRenderer r = Renderer(name, local.center - parentLocal, order, material, parent != null ? parent : transform);
            r.sprite = tile;
            r.drawMode = SpriteDrawMode.Tiled;
            r.tileMode = SpriteTileMode.Continuous;
            r.size = local.size;
            return r;
        }

        private static SpriteRenderer Renderer(string name, Vector2 localPosition, int order, Material material, Transform parent)
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
