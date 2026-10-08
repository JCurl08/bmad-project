using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// One lair room (4.13), built by LairRooms on its own screen below the faces the first time its portal is used, and
    /// removed on a rebuild: a town interior's shell (TownInterior's walls, exit gap and vestibule) in the lair's dark stone
    /// (LairArt), the barred cells along the back wall (LairRoom), pillars and torches, a pink swirl in the vestibule (the
    /// way back up) and an arrow at the exit, and LairPlan.PrisonersPerRoom prisoners, one behind each cell's bars,
    /// wandering their small box and talking in their own voice (LairPlan.LinesOf: a captive line, then a true hint). The
    /// prisoners are never hostile (no race relations: they want out). No gates, rewards or enemies.
    /// </summary>
    public class LairRoomView : MonoBehaviour
    {
        /// <summary>Sorting orders, as the town interiors': floor, decor over it, walls and furniture under people.</summary>
        public const int FloorOrder = -10, RugOrder = -9, WallOrder = -4, FurnitureOrder = -3, BarsOrder = 9;

        private readonly List<NpcTalker> prisoners = new List<NpcTalker>();
        private readonly List<BoxCollider2D> solids = new List<BoxCollider2D>();

        /// <summary>The face whose portal leads down here.</summary>
        public FaceId Face { get; private set; }

        /// <summary>The portal's screen (the screen the map shows while the player is down here).</summary>
        public ScreenAddress PortalScreen { get; private set; }

        public Vector2 Centre => transform.position;

        /// <summary>Where the player arrives (world): just inside the exit, facing north towards the cells.</summary>
        public Vector2 EntryPoint => Centre + TownInterior.EntryPoint;

        /// <summary>The walkable room (world), cells included.</summary>
        public Rect Inner => new Rect(Centre + TownInterior.Inner.position, TownInterior.Inner.size);

        /// <summary>The player's part of the room (world): below the bars.</summary>
        public Rect Walkway => new Rect(Centre + LairRoom.Walkway.position, LairRoom.Walkway.size);

        /// <summary>The prisoners, cell by cell (west to east); destroyed ones become null.</summary>
        public IReadOnlyList<NpcTalker> Prisoners => prisoners;

        /// <summary>The prisoners' plans, cell by cell.</summary>
        public IReadOnlyList<PrisonerPlan> Plans { get; private set; } = Array.Empty<PrisonerPlan>();

        /// <summary>Every solid collider: the walls, the dividers and bars, the pillars.</summary>
        public IReadOnlyList<BoxCollider2D> Solids => solids;

        /// <summary>The talking spot before cell i (world).</summary>
        public Vector2 Front(int i) => Centre + LairRoom.Front(i);

        /// <summary>Raised when the player talks to a prisoner (the map learns the real portal from the one who names it).</summary>
        public event Action<PrisonerPlan> Told;

        /// <summary>True when the player's centre has walked out through the exit (below its line, inside the vestibule).</summary>
        public bool IsPastExit(Vector2 world)
        {
            Vector2 local = world - Centre;
            return local.y < TownInterior.ExitLine && Mathf.Abs(local.x) <= TownInterior.ExitHalfWidth + 0.5f;
        }

        /// <summary>Builds a face's lair room at a centre (world), its prisoners talking to player.</summary>
        public static LairRoomView Build(CubeWorld world, LairPlan plan, FaceId face, ScreenAddress portalScreen, Vector2 centre,
            Transform parent, Transform player)
        {
            var go = new GameObject($"Lair {face}");
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            LairRoomView view = go.AddComponent<LairRoomView>();
            view.Face = face;
            view.PortalScreen = portalScreen;
            view.Lay(world.Material);
            view.Populate(world, plan, player);
            return view;
        }

        private void Lay(Material material)
        {
            Tiled("Floor", TownInterior.Inner, LairArt.Floor, FloorOrder, material);
            SpriteRenderer vestibule = Tiled("Vestibule Floor", TownInterior.Vestibule, LairArt.Floor, FloorOrder, material);
            vestibule.color = new Color(0.7f, 0.7f, 0.7f);
            for (int i = 0; i < LairRoom.CellCount; i++)
                Tiled($"Cell {i} Straw", LairRoom.Cell(i), LairArt.Straw, RugOrder, material);

            // The shell's walls, the back and side walls drawn taller (seen from the front), as in the town rooms.
            List<Rect> walls = TownInterior.Walls;
            for (int i = 0; i < walls.Count; i++)
            {
                Rect solid = walls[i];
                Rect art = solid;
                if (i <= 2) art = Rect.MinMaxRect(art.xMin, art.yMin, art.xMax, TownInterior.RoomHalf.y + TownInterior.NorthWallHeight);
                Solid($"Wall {i}", solid, art, LairArt.Wall, WallOrder, material);
            }
            // The stone between the cells, drawn up into the back wall.
            List<Rect> dividers = LairRoom.Dividers;
            for (int i = 0; i < dividers.Count; i++)
            {
                Rect d = dividers[i];
                Solid($"Divider {i}", d, Rect.MinMaxRect(d.xMin, d.yMin, d.xMax, TownInterior.RoomHalf.y + TownInterior.NorthWallHeight), LairArt.Wall,
                    WallOrder + 1, material);
            }
            // Each cell's bars: a thin solid along the front, drawn tall over the prisoner's feet (in front of them).
            for (int i = 0; i < LairRoom.CellCount; i++)
            {
                Rect bars = LairRoom.Bars(i);
                var art = Rect.MinMaxRect(bars.xMin, bars.yMin, bars.xMax, bars.yMin + LairRoom.BarsArtHeight);
                GameObject go = Solid($"Bars {i}", bars, Rect.zero, null, 0, material);
                SpriteRenderer r = Renderer("Art", art.center - bars.center, BarsOrder, material, go.transform);
                r.sprite = LairArt.Bars(art.size);
                Fit(r, art.size);
            }
            foreach (Rect pillar in LairRoom.Pillars)
            {
                var art = Rect.MinMaxRect(pillar.xMin - 0.1f, pillar.yMin, pillar.xMax + 0.1f, pillar.yMin + 3f);
                GameObject go = Solid("Pillar", pillar, Rect.zero, null, 0, material);
                SpriteRenderer r = Renderer("Art", art.center - pillar.center, FurnitureOrder, material, go.transform);
                r.sprite = LairArt.Pillar(art.size);
                Fit(r, art.size);
            }
            // Torches on the back wall over the dividers.
            for (int i = 1; i < dividers.Count - 1; i++)
            {
                SpriteRenderer torch = Renderer($"Torch {i}", new Vector2(dividers[i].center.x, TownInterior.RoomHalf.y + 0.6f), WallOrder + 2,
                    material, transform);
                torch.sprite = LairArt.Torch(new Vector2(0.8f, 1.2f));
                Fit(torch, new Vector2(0.8f, 1.2f));
            }

            // The way back up: a swirl in the vestibule and an arrow pointing out through the exit.
            Rect vest = TownInterior.Vestibule;
            SpriteRenderer swirl = Renderer("Way Up", new Vector2(0f, vest.yMin + 0.6f), RugOrder + 1, material, transform);
            swirl.sprite = LairArt.Swirl(new Vector2(1.8f, 1f));
            Fit(swirl, new Vector2(1.8f, 1f));
            Vector2 gap = new Vector2(0f, -TownInterior.RoomHalf.y - TownInterior.WallThickness / 2f);
            SpriteRenderer arrow = ArtCatalog.AddShape(transform, "Exit Arrow", PartShape.Triangle, gap + new Vector2(0f, -0.9f),
                new Vector2(0.75f, 0.7f), new Color(1f, 0.95f, 0.6f, 0.85f), FurnitureOrder, material);
            arrow.transform.localRotation = Quaternion.Euler(0f, 0f, 180f); // apex down: out through the exit
        }

        private void Populate(CubeWorld world, LairPlan plan, Transform player)
        {
            IReadOnlyList<PrisonerPlan> room = plan.PrisonersOf(Face);
            Plans = room;
            for (int i = 0; i < room.Count && i < LairRoom.CellCount; i++)
            {
                PrisonerPlan prisoner = room[i];
                NpcSpec spec = plan.SpecOf(prisoner);
                List<string> lines = plan.LinesOf(prisoner, world.Model);
                Rect box = LairRoom.PrisonerBox(i);
                var bounds = new Rect(Centre + box.position, box.size);
                NpcTalker npc = NpcFactory.Spawn(spec, lines, Centre + LairRoom.PrisonerStart(i), bounds, null, player, transform, world.Material);
                npc.name = $"Lair Prisoner {Face} {i} {spec.DisplayName}";
                npc.Talking += _ => Told?.Invoke(prisoner);
                prisoners.Add(npc);
            }
        }

        private GameObject Solid(string name, Rect solid, Rect art, Sprite tile, int order, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = solid.center;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = solid.size;
            solids.Add(box);
            if (tile != null) Tiled("Art", art, tile, order, material, go.transform, solid.center);
            return go;
        }

        private static void Fit(SpriteRenderer r, Vector2 size)
        {
            Vector2 drawn = r.sprite.bounds.size;
            r.transform.localScale = new Vector3(size.x / drawn.x, size.y / drawn.y, 1f);
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
