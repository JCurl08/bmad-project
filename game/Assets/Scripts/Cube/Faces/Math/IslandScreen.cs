using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A Math island screen in the world (1.25), built from its IslandLayout: solid water over the whole screen inside its
    /// edge walls (WaterRects: solid colliders, so nobody walks or wades it), the islands as land, plank bridges over the
    /// water between some of them, and the broken spans' gaps (BrokenSpan: solid until the gap-crosser* is swung at them,
    /// which lays the missing plank for good). Every doorway lands on its own island, so the player can always walk back
    /// out the way it came.
    /// </summary>
    public class IslandScreen : MonoBehaviour
    {
        public static readonly Color WaterColor = new Color(0.22f, 0.45f, 0.8f, 1f);
        public static readonly Color PlankColor = Color.white;
        public static readonly Color MendedColor = new Color(1f, 0.92f, 0.6f);

        public const string MendedText = "The gap is spanned!";

        private const int WaterOrder = -9;
        private const int PlankOrder = -8;
        private const int IslandOrder = -7;

        private readonly List<BrokenSpan> gaps = new List<BrokenSpan>();
        private readonly List<Collider2D> water = new List<Collider2D>();
        private Material material;

        public IslandLayout Layout { get; private set; }

        /// <summary>The screen centre the layout's positions are relative to.</summary>
        public Vector2 Centre { get; private set; }

        /// <summary>The gaps of the broken spans (mended ones included), in plank order.</summary>
        public IReadOnlyList<BrokenSpan> Gaps => gaps;

        /// <summary>The solid water pieces.</summary>
        public IReadOnlyList<Collider2D> Water => water;

        /// <summary>Raised when the gap-crosser* mends a broken span, with the plank's index.</summary>
        public event Action<IslandScreen, int> Mended;

        /// <summary>The world position of an island's middle.</summary>
        public Vector2 IslandWorld(int island) => Centre + Layout.Islands[island].center;

        /// <summary>
        /// The water as rects (local): the water area less the islands, the planks and the gaps, cut on every island and
        /// plank edge and merged into rows and columns.
        /// </summary>
        public static List<Rect> WaterRects(IslandLayout layout)
        {
            Rect area = IslandLayout.WaterArea;
            var land = new List<Rect>(layout.Islands);
            foreach (IslandBridge b in layout.Bridges) land.Add(b.Plank);
            var xs = new SortedSet<float> { area.xMin, area.xMax };
            var ys = new SortedSet<float> { area.yMin, area.yMax };
            foreach (Rect r in land)
            {
                xs.Add(r.xMin);
                xs.Add(r.xMax);
                ys.Add(r.yMin);
                ys.Add(r.yMax);
            }
            var xl = new List<float>(xs);
            var yl = new List<float>(ys);
            bool IsWater(float x, float y)
            {
                if (!area.Contains(new Vector2(x, y))) return false;
                foreach (Rect r in land)
                    if (r.Contains(new Vector2(x, y))) return false;
                return true;
            }
            // Row by row: runs of water cells, then runs stacked when they line up.
            var open = new Dictionary<(float, float), Rect>();
            var done = new List<Rect>();
            for (int j = 0; j + 1 < yl.Count; j++)
            {
                float cy = (yl[j] + yl[j + 1]) / 2f;
                var runs = new List<(float, float)>();
                int i = 0;
                while (i + 1 < xl.Count)
                {
                    if (!IsWater((xl[i] + xl[i + 1]) / 2f, cy))
                    {
                        i++;
                        continue;
                    }
                    int k = i;
                    while (k + 1 < xl.Count && IsWater((xl[k] + xl[k + 1]) / 2f, cy)) k++;
                    runs.Add((xl[i], xl[k]));
                    i = k;
                }
                var next = new Dictionary<(float, float), Rect>();
                foreach ((float x0, float x1) in runs)
                    next[(x0, x1)] = open.TryGetValue((x0, x1), out Rect r)
                        ? Rect.MinMaxRect(r.xMin, r.yMin, r.xMax, yl[j + 1])
                        : Rect.MinMaxRect(x0, yl[j], x1, yl[j + 1]);
                foreach (KeyValuePair<(float, float), Rect> e in open)
                    if (!next.ContainsKey(e.Key)) done.Add(e.Value);
                open = next;
            }
            done.AddRange(open.Values);
            return done;
        }

        /// <summary>Builds the screen's water, islands, planks and gaps (layout local to screenCentre) under this object.</summary>
        public void Configure(IslandLayout layout, Vector2 screenCentre, Material material)
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            Centre = screenCentre;
            this.material = material;
            transform.position = screenCentre;
            gaps.Clear();
            water.Clear();

            List<Rect> waterRects = WaterRects(layout);
            for (int i = 0; i < waterRects.Count; i++)
            {
                Rect r = waterRects[i];
                SpriteRenderer sprite = ArtCatalog.AddShape(transform, $"Water {i}", PartShape.Square, r.center, r.size, WaterColor,
                    WaterOrder, material);
                // The collider on its own object, so it keeps world-unit sizes.
                var go = new GameObject($"Water {i} (solid)");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = r.center;
                var box = go.AddComponent<BoxCollider2D>();
                box.size = r.size;
                water.Add(box);
                sprite.name = $"Water {i}";
            }
            for (int i = 0; i < layout.Islands.Count; i++)
            {
                Rect r = layout.Islands[i];
                ArtCatalog.AddSprite(transform, $"Island {i}", ArtKey.Island, r.center, r.size, Color.white, IslandOrder, material);
            }
            for (int k = 0; k < layout.Bridges.Count; k++)
            {
                IslandBridge b = layout.Bridges[k];
                if (!b.Broken)
                {
                    Plank($"Plank {k}", b.Plank, b.Vertical, material);
                    continue;
                }
                Rect gap = b.Gap;
                Rect near = b.Vertical ? Rect.MinMaxRect(b.Plank.xMin, b.Plank.yMin, b.Plank.xMax, gap.yMin)
                    : Rect.MinMaxRect(b.Plank.xMin, b.Plank.yMin, gap.xMin, b.Plank.yMax);
                Rect far = b.Vertical ? Rect.MinMaxRect(b.Plank.xMin, gap.yMax, b.Plank.xMax, b.Plank.yMax)
                    : Rect.MinMaxRect(gap.xMax, b.Plank.yMin, b.Plank.xMax, b.Plank.yMax);
                Plank($"Plank {k}", near, b.Vertical, material);
                Plank($"Plank {k} (far)", far, b.Vertical, material);
                gaps.Add(BrokenSpan.Create(this, k, gap, b.Vertical, material));
            }
        }

        private SpriteRenderer Plank(string name, Rect r, bool vertical, Material material)
        {
            // The plank art runs along x; a north-south plank is turned a quarter.
            Vector2 size = vertical ? new Vector2(r.height, r.width) : r.size;
            SpriteRenderer plank = ArtCatalog.AddSprite(transform, name, ArtKey.PlankBridge, r.center, size, PlankColor, PlankOrder, material, false);
            if (vertical) plank.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            return plank;
        }

        /// <summary>
        /// Mends a broken span (the gap-crosser* swung at its gap): the missing plank is laid and the gap stops blocking.
        /// False when the plank is not a gap (whole, or mended already).
        /// </summary>
        public bool Mend(int bridge)
        {
            foreach (BrokenSpan gap in gaps)
            {
                if (gap == null || gap.Bridge != bridge || gap.IsMended) continue;
                IslandBridge b = Layout.Bridges[bridge];
                gap.ShowMended(Plank($"Plank {bridge} (laid)", b.Gap, b.Vertical, material));
                PickupPopup.Show(Centre + b.Gap.center, MendedText, MendedColor);
                Mended?.Invoke(this, bridge);
                return true;
            }
            return false;
        }

        /// <summary>An island screen object for a layout under parent.</summary>
        public static IslandScreen Create(string name, Transform parent, IslandLayout layout, Vector2 screenCentre, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var screen = go.AddComponent<IslandScreen>();
            screen.Configure(layout, screenCentre, material);
            return screen;
        }
    }

    /// <summary>
    /// The gap in a broken span: a one-square hole in the plank (the gap art) with the gap-crosser*'s mark, solid so nobody
    /// steps over it. Swinging the gap-crosser* at it mends the span (IslandScreen.Mend): the plank is laid and the gap
    /// stops blocking, for good. Any other item, or a bare swing, does nothing.
    /// </summary>
    public class BrokenSpan : MonoBehaviour, IAttackReceiver
    {
        private IslandScreen owner;
        private SpriteRenderer hole;
        private SpriteRenderer mark;
        private BoxCollider2D solid;

        public IslandScreen Owner => owner;

        /// <summary>The broken plank (an index into the layout's Bridges).</summary>
        public int Bridge { get; private set; }

        public bool IsMended { get; private set; }

        /// <summary>The solid part that stops the player until the span is mended.</summary>
        public Collider2D Solid => solid;

        public bool ReceiveAttack(ItemDefinition item, GameObject attacker)
        {
            if (owner == null || IsMended || !GapCrosserItem.Is(item)) return false;
            return owner.Mend(Bridge);
        }

        /// <summary>The plank is laid: the hole and the mark go and the gap stops blocking.</summary>
        public void ShowMended(SpriteRenderer laid)
        {
            IsMended = true;
            if (solid != null) solid.enabled = false;
            if (hole != null) hole.gameObject.SetActive(false);
            if (mark != null) mark.gameObject.SetActive(false);
            if (laid != null) laid.color = IslandScreen.MendedColor;
        }

        public static BrokenSpan Create(IslandScreen owner, int bridge, Rect gap, bool vertical, Material material)
        {
            var go = new GameObject($"Gap {bridge}");
            go.transform.SetParent(owner.transform, false);
            go.transform.localPosition = gap.center;
            var span = go.AddComponent<BrokenSpan>();
            span.owner = owner;
            span.Bridge = bridge;
            span.solid = go.AddComponent<BoxCollider2D>();
            span.solid.size = gap.size;
            Vector2 art = vertical ? new Vector2(gap.height, gap.width) : gap.size;
            span.hole = ArtCatalog.AddSprite(go.transform, "Hole", ArtKey.GateGap, Vector2.zero, art, GapGate.GapColor * 1.6f, -8, material, false);
            if (vertical) span.hole.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            span.mark = ArtCatalog.AddShape(go.transform, "Gap Mark", PartShape.Diamond, Vector2.zero, new Vector2(0.3f, 0.3f),
                GapCrosserItem.ItemColor, -6, material);
            return span;
        }
    }
}
