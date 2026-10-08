using System;
using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// An Earth wind-stream screen in the world (4.11), built from its WindLayout: a tile sprite on every grid cell (WindArt: the
    /// ring path, sky, islands, walls, grip anchors, the goal pad, and each stream tile's chevron turned its way), white streaks
    /// flowing along every stream (the flow, animated: so the way and the whole path of each stream show at a glance), cliffs
    /// over the strip between the grid and the edge walls (solid, except each doorway's mouth, so walking in always leads onto
    /// the ring), and the reward chest on the goal pad, kept shut until the player stands on the pad (it is the chest's guard).
    /// The grid needs no colliders: on it the player's WindRider moves tile by tile through the layout's own rules. Every built
    /// screen is listed (Active) so the rider finds the one under the player.
    /// </summary>
    public class WindScreen : MonoBehaviour, IPuzzleRewardHost, IRewardGuard
    {
        /// <summary>Every enabled wind screen.</summary>
        public static readonly List<WindScreen> Active = new List<WindScreen>();

        public const int TileOrder = -9, FlowOrder = -8;

        /// <summary>How fast the flow's streaks run along a stream (world units a second).</summary>
        public const float FlowSpeed = 3.5f;

        /// <summary>One streak per this many tiles of stream (at least one a stream).</summary>
        public const float StreakSpacing = 1.6f;

        /// <summary>Said once on reaching the goal pad.</summary>
        public const string GoalText = "Made it!";

        public static readonly Color GoalTextColor = new Color(1f, 0.9f, 0.45f);

        private readonly List<Collider2D> cliffs = new List<Collider2D>();
        private readonly List<Flow> flows = new List<Flow>();
        private SpriteRenderer[] cellArt = Array.Empty<SpriteRenderer>();

        /// <summary>One stream's flow: its path (local points, the first tile to the landing) and its streaks.</summary>
        private sealed class Flow
        {
            public Vector2[] Points;
            public float Length;
            public Transform[] Streaks;
        }

        public WindLayout Layout { get; private set; }
        public ScreenAddress Screen { get; private set; }
        public Vector2 Centre { get; private set; }
        public RewardChest Chest { get; private set; }
        public bool GoalReached { get; private set; }

        bool IRewardGuard.IsOpen => GoalReached;

        /// <summary>The solid cliffs round the grid.</summary>
        public IReadOnlyList<Collider2D> Cliffs => cliffs;

        /// <summary>The streams whose flow is drawn (one per stream start).</summary>
        public int FlowCount => flows.Count;

        /// <summary>Every streak of the flow (tests check they move).</summary>
        public IEnumerable<Transform> Streaks
        {
            get
            {
                foreach (Flow f in flows)
                    foreach (Transform t in f.Streaks) yield return t;
            }
        }

        public event Action<WindScreen> Reached;

        public SpriteRenderer CellArt(int cell) => cellArt[cell];

        public Vector2 CellWorld(int cell) => Centre + WindLayout.CellLocal(cell);

        public bool OnGrid(Vector2 world) => WindLayout.GridRect.Contains(world - Centre);

        /// <summary>The wind screen whose grid holds a world point, or null.</summary>
        public static WindScreen At(Vector2 world)
        {
            foreach (WindScreen s in Active)
                if (s != null && s.isActiveAndEnabled && s.Layout != null && s.OnGrid(world)) return s;
            return null;
        }

        private void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
        }

        private void OnDisable() => Active.Remove(this);

        public void Configure(ScreenAddress screen, WindLayout layout, Vector2 screenCentre, Material material)
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            Screen = screen;
            Centre = screenCentre;
            transform.position = screenCentre;
            cellArt = new SpriteRenderer[WindLayout.Cells];
            var tiles = new GameObject("Tiles").transform;
            tiles.SetParent(transform, false);
            for (int c = 0; c < WindLayout.Cells; c++)
            {
                var go = new GameObject($"Tile {WindLayout.Pos(c).x},{WindLayout.Pos(c).y} ({layout.TileAt(c)})");
                go.transform.SetParent(tiles, false);
                go.transform.localPosition = WindLayout.CellLocal(c);
                if (layout.TileAt(c) == WindTile.Stream)
                    go.transform.localRotation = Quaternion.Euler(0f, 0f, -90f * (int)layout.DirectionAt(c));
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = WindArt.For(layout, c);
                if (material != null) r.sharedMaterial = material;
                r.sortingOrder = TileOrder;
                cellArt[c] = r;
            }
            BuildFlows(material);
            BuildCliffs(material);
        }

        private void BuildFlows(Material material)
        {
            flows.Clear();
            var root = new GameObject("Flow").transform;
            root.SetParent(transform, false);
            foreach (int head in Layout.StreamStarts)
            {
                List<int> path = Layout.RidePath(head);
                if (path.Count < 2) continue;
                var points = new Vector2[path.Count + 1];
                // From the far edge of the first tile (where the wind comes in) to the middle of the landing.
                Vector2 first = WindLayout.CellLocal(path[0]), second = WindLayout.CellLocal(path[1]);
                points[0] = first - (second - first) * 0.5f;
                for (int i = 0; i < path.Count; i++) points[i + 1] = WindLayout.CellLocal(path[i]);
                float length = 0f;
                for (int i = 1; i < points.Length; i++) length += Vector2.Distance(points[i - 1], points[i]);
                int count = Mathf.Max(1, Mathf.RoundToInt(length / StreakSpacing));
                var flow = new Flow { Points = points, Length = length, Streaks = new Transform[count] };
                for (int i = 0; i < count; i++)
                {
                    var go = new GameObject($"Streak {head}.{i}");
                    go.transform.SetParent(root, false);
                    var r = go.AddComponent<SpriteRenderer>();
                    r.sprite = WindArt.StreakSprite;
                    if (material != null) r.sharedMaterial = material;
                    r.sortingOrder = FlowOrder;
                    flow.Streaks[i] = go.transform;
                }
                flows.Add(flow);
            }
            Animate(0f);
        }

        private void Update() => Animate(Time.time);

        /// <summary>Puts every streak where the flow carries it at a time: evenly spread along its stream, running its way.</summary>
        private void Animate(float time)
        {
            foreach (Flow f in flows)
                for (int i = 0; i < f.Streaks.Length; i++)
                {
                    float along = Mathf.Repeat(time * FlowSpeed + i * f.Length / f.Streaks.Length, f.Length);
                    for (int k = 1; k < f.Points.Length; k++)
                    {
                        float seg = Vector2.Distance(f.Points[k - 1], f.Points[k]);
                        if (along > seg && k < f.Points.Length - 1)
                        {
                            along -= seg;
                            continue;
                        }
                        Vector2 dir = (f.Points[k] - f.Points[k - 1]).normalized;
                        Vector2 at = f.Points[k - 1] + dir * Mathf.Min(along, seg);
                        // Off-centre by the streak's index, so the streaks of one stream read as several lines of wind.
                        Vector2 side = new Vector2(-dir.y, dir.x) * ((i % 3) - 1) * 0.22f;
                        f.Streaks[i].localPosition = at + side;
                        f.Streaks[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                        break;
                    }
                }
        }

        private void BuildCliffs(Material material)
        {
            cliffs.Clear();
            var strip = new GameObject("Cliffs").transform;
            strip.SetParent(transform, false);
            int i = 0;
            Rect grid = WindLayout.GridRect;
            foreach ((Rect r, bool mouth) in IceScreen.StripRects(side => Layout.HasDoor(side), grid))
            {
                var go = new GameObject(mouth ? $"Mouth {i}" : $"Cliff {i}");
                go.transform.SetParent(strip, false);
                go.transform.localPosition = r.center;
                var art = go.AddComponent<SpriteRenderer>();
                art.sprite = mouth ? WindArt.Path : WindArt.CliffTile;
                art.drawMode = SpriteDrawMode.Tiled;
                art.size = r.size;
                if (material != null) art.sharedMaterial = material;
                art.sortingOrder = TileOrder;
                if (!mouth)
                {
                    // Kept off the grid, so a player standing on the ring never touches a cliff.
                    Rect solid = IceScreen.SolidOf(r, grid);
                    var box = go.AddComponent<BoxCollider2D>();
                    box.offset = solid.center - r.center;
                    box.size = solid.size;
                    cliffs.Add(box);
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

        /// <summary>The player stood on the goal pad: the chest's guard opens (once).</summary>
        public void ReachGoal()
        {
            if (GoalReached) return;
            GoalReached = true;
            PickupPopup.Show(CellWorld(Layout.Goal), GoalText, GoalTextColor, 1.2f);
            Reached?.Invoke(this);
        }

        public static WindScreen Create(string name, Transform parent, ScreenAddress screen, WindLayout layout, Vector2 screenCentre,
            Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var wind = go.AddComponent<WindScreen>();
            wind.Configure(screen, layout, screenCentre, material);
            return wind;
        }
    }
}
