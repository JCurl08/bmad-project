using System;
using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// An Earth ice screen in the world (4.10), built from its IceLayout: a tile sprite on every grid cell (ice, rocks, pits,
    /// grip, the landings, the camp with its fire, the goal pad: IceArt), snowbanks over the strip between the grid and the
    /// edge walls (solid, except each doorway's mouth, so walking in always leads onto the landing), and the reward chest on
    /// the goal pad, kept shut until the player comes to rest on the pad (it is the chest's guard). The grid needs no other
    /// colliders: on the grid the player's IceSlider moves tile by tile through the layout's own rules. Every built screen is
    /// listed (Active) so the slider finds the one under the player.
    /// </summary>
    public class IceScreen : MonoBehaviour, IPuzzleRewardHost, IRewardGuard
    {
        /// <summary>Every enabled ice screen.</summary>
        public static readonly List<IceScreen> Active = new List<IceScreen>();

        public const int TileOrder = -9;

        /// <summary>Said once on reaching the goal pad.</summary>
        public const string GoalText = "Made it!";

        public static readonly Color GoalTextColor = new Color(1f, 0.9f, 0.45f);

        private readonly List<Collider2D> banks = new List<Collider2D>();
        private SpriteRenderer[] cellArt = Array.Empty<SpriteRenderer>();

        public IceLayout Layout { get; private set; }
        public ScreenAddress Screen { get; private set; }

        /// <summary>The screen centre the layout's cells are relative to.</summary>
        public Vector2 Centre { get; private set; }

        /// <summary>The reward chest standing on the goal pad (null until CubeWorld hands it over).</summary>
        public RewardChest Chest { get; private set; }

        /// <summary>True once the player has come to rest on the goal pad (the chest's guard: it opens then).</summary>
        public bool GoalReached { get; private set; }

        bool IRewardGuard.IsOpen => GoalReached;

        /// <summary>The solid snowbanks round the grid.</summary>
        public IReadOnlyList<Collider2D> Banks => banks;

        /// <summary>Raised when the player first comes to rest on the goal pad.</summary>
        public event Action<IceScreen> Reached;

        /// <summary>The sprite on a cell.</summary>
        public SpriteRenderer CellArt(int cell) => cellArt[cell];

        public Vector2 CellWorld(int cell) => Centre + IceLayout.CellLocal(cell);

        /// <summary>True when a world point is on this screen's grid.</summary>
        public bool OnGrid(Vector2 world) => IceLayout.GridRect.Contains(world - Centre);

        /// <summary>The ice screen whose grid holds a world point, or null.</summary>
        public static IceScreen At(Vector2 world)
        {
            foreach (IceScreen s in Active)
                if (s != null && s.isActiveAndEnabled && s.Layout != null && s.OnGrid(world)) return s;
            return null;
        }

        private void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
        }

        private void OnDisable() => Active.Remove(this);

        public void Configure(ScreenAddress screen, IceLayout layout, Vector2 screenCentre, Material material)
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            Screen = screen;
            Centre = screenCentre;
            transform.position = screenCentre;
            cellArt = new SpriteRenderer[IceLayout.Cells];
            var tiles = new GameObject("Tiles").transform;
            tiles.SetParent(transform, false);
            for (int c = 0; c < IceLayout.Cells; c++)
            {
                var go = new GameObject($"Tile {IceLayout.Pos(c).x},{IceLayout.Pos(c).y} ({layout.TileAt(c)})");
                go.transform.SetParent(tiles, false);
                go.transform.localPosition = IceLayout.CellLocal(c);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = IceArt.For(layout.TileAt(c), c == IceLayout.CampCell);
                if (material != null) r.sharedMaterial = material;
                r.sortingOrder = TileOrder;
                cellArt[c] = r;
            }
            BuildBanks(material);
        }

        /// <summary>
        /// The strip between the grid and the edge walls: snowbank, solid, except at each doorway (the mouth, as wide as the
        /// doorway), which is grip leading onto the landing.
        /// </summary>
        public static List<(Rect Rect, bool Mouth)> StripRects(Func<Facing, bool> hasDoorway)
        {
            Vector2 h = ScreenEdges.Half;
            float t = ScreenEdges.EdgeWallThickness, d = ScreenEdges.DoorwayHalfWidth;
            Rect grid = IceLayout.GridRect;
            var rects = new List<(Rect, bool)>();
            void Split(Rect r, bool horizontal, bool door)
            {
                if (!door)
                {
                    rects.Add((r, false));
                    return;
                }
                if (horizontal)
                {
                    rects.Add((Rect.MinMaxRect(r.xMin, r.yMin, -d, r.yMax), false));
                    rects.Add((Rect.MinMaxRect(-d, r.yMin, d, r.yMax), true));
                    rects.Add((Rect.MinMaxRect(d, r.yMin, r.xMax, r.yMax), false));
                }
                else
                {
                    rects.Add((Rect.MinMaxRect(r.xMin, r.yMin, r.xMax, -d), false));
                    rects.Add((Rect.MinMaxRect(r.xMin, -d, r.xMax, d), true));
                    rects.Add((Rect.MinMaxRect(r.xMin, d, r.xMax, r.yMax), false));
                }
            }
            Split(Rect.MinMaxRect(-h.x + t, grid.yMax, h.x - t, h.y - t), true, hasDoorway(Facing.North));
            Split(Rect.MinMaxRect(-h.x + t, -h.y + t, h.x - t, grid.yMin), true, hasDoorway(Facing.South));
            Split(Rect.MinMaxRect(grid.xMax, grid.yMin, h.x - t, grid.yMax), false, hasDoorway(Facing.East));
            Split(Rect.MinMaxRect(-h.x + t, grid.yMin, grid.xMin, grid.yMax), false, hasDoorway(Facing.West));
            return rects;
        }

        /// <summary>
        /// How far a snowbank's collider keeps off the grid: a player resting on a border tile reaches TimeField.PlayerRadius
        /// (0.6) past its centre, 0.1 over the grid's edge, so the bank's solid part starts beyond that.
        /// </summary>
        public const float BankClearance = 0.15f;

        /// <summary>The solid part of a strip piece (local): the piece less BankClearance on its side facing the grid.</summary>
        public static Rect SolidOf(Rect piece)
        {
            Rect grid = IceLayout.GridRect;
            const float eps = 1e-4f;
            float xMin = piece.xMin, xMax = piece.xMax, yMin = piece.yMin, yMax = piece.yMax;
            if (Mathf.Abs(piece.yMin - grid.yMax) < eps) yMin += BankClearance;
            if (Mathf.Abs(piece.yMax - grid.yMin) < eps) yMax -= BankClearance;
            if (Mathf.Abs(piece.xMin - grid.xMax) < eps) xMin += BankClearance;
            if (Mathf.Abs(piece.xMax - grid.xMin) < eps) xMax -= BankClearance;
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private void BuildBanks(Material material)
        {
            banks.Clear();
            var strip = new GameObject("Snowbanks").transform;
            strip.SetParent(transform, false);
            int i = 0;
            foreach ((Rect r, bool mouth) in StripRects(side => Layout.HasDoor(side)))
            {
                var go = new GameObject(mouth ? $"Mouth {i}" : $"Snowbank {i}");
                go.transform.SetParent(strip, false);
                go.transform.localPosition = r.center;
                var art = go.AddComponent<SpriteRenderer>();
                art.sprite = mouth ? IceArt.Grip : IceArt.Bank;
                art.drawMode = SpriteDrawMode.Tiled;
                art.size = r.size;
                if (material != null) art.sharedMaterial = material;
                art.sortingOrder = TileOrder;
                if (!mouth)
                {
                    // The solid part keeps BankClearance off the grid, so a player resting on a border tile never touches it.
                    Rect solid = SolidOf(r);
                    var box = go.AddComponent<BoxCollider2D>();
                    box.offset = solid.center - r.center;
                    box.size = solid.size;
                    banks.Add(box);
                }
                i++;
            }
        }

        /// <summary>CubeWorld hands over the goal's chest: it stays shut until the goal is reached.</summary>
        public void AttachReward(RewardChest chest)
        {
            Chest = chest;
            if (chest != null) chest.Guard = this;
        }

        /// <summary>The player came to rest on the goal pad: the chest's guard opens (once).</summary>
        public void ReachGoal()
        {
            if (GoalReached) return;
            GoalReached = true;
            PickupPopup.Show(CellWorld(Layout.Goal), GoalText, GoalTextColor, 1.2f);
            Reached?.Invoke(this);
        }

        public static IceScreen Create(string name, Transform parent, ScreenAddress screen, IceLayout layout, Vector2 screenCentre,
            Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ice = go.AddComponent<IceScreen>();
            ice.Configure(screen, layout, screenCentre, material);
            return ice;
        }
    }
}
