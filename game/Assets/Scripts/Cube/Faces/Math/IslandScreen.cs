using System;
using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A Math island screen in the world (1.25), built from its IslandLayout: solid water over the whole screen inside its
    /// edge walls (WaterRects: solid colliders, so nobody walks or wades it), the islands as land, plank bridges over the
    /// water between some of them, and the broken spans' gaps (BrokenSpan: solid until the gap-crosser* is swung at them,
    /// which lays the missing plank for good). Every doorway lands on its own island, so the player can always walk back
    /// out the way it came. 1.26: the islands are the region's own floor showing through the water (no pads), each with a
    /// shoreline (a foam line and a band of wet sand) where it meets the water; a lone doorway's island (IslandLayout.IsLone,
    /// none in 4.12's layouts) carries a lookout sign and says "go round".
    /// 4.12 "light every plank" (PlankLights, PlankWalker): crossing a dark plank of the lit network lights it Math's
    /// yellow; crossing a lit one again flashes every plank red and snuffs them all; an island whose network planks are all
    /// lit glows round its rim; the reward roadblock (the chest CubeWorld hands over, on the reward island, under bars, on a
    /// plinth with one lamp per plank) shows the count; lighting the last plank sends a chain of light from that plank to
    /// the roadblock, whose bars then lift (it is the chest's guard). Leaving the screen snuffs an unsolved network; a solved
    /// one stays lit for the run. Broken spans light nothing: mended, they show the gap-crosser*'s gold.
    /// </summary>
    public class IslandScreen : MonoBehaviour, IPuzzleRewardHost, IRewardGuard
    {
        public static readonly Color WaterColor = new Color(0.22f, 0.45f, 0.8f, 1f);

        /// <summary>A dark plank (unlit, or off the network): dark wood.</summary>
        public static readonly Color PlankColor = new Color(0.62f, 0.55f, 0.5f, 1f);

        /// <summary>A lit plank (or a mended span) shows its art at full brightness under its glow.</summary>
        public static readonly Color LitPlankColor = Color.white;

        /// <summary>The glow over a lit plank: Math's own yellow (CubeWorld.ThemeColor).</summary>
        public static readonly Color LitGlow = new Color(1f, 0.95f, 0.25f, 0.55f);

        /// <summary>Every network plank flashes this red when a lit one is crossed again, then goes dark.</summary>
        public static readonly Color SnuffGlow = new Color(1f, 0.2f, 0.15f, 0.65f);

        /// <summary>How long the red flash lasts.</summary>
        public const float FlashSeconds = 0.45f;

        /// <summary>The rim of an island whose network planks are all lit.</summary>
        public static readonly Color RimColor = new Color(1f, 0.92f, 0.3f, 0.95f);
        public const float RimWidth = 0.14f * ScreenMath.ContentScale;

        /// <summary>A lamp on the roadblock: dark, or lit for a lit plank.</summary>
        public static readonly Color LampOff = new Color(0.25f, 0.24f, 0.22f, 1f);
        public static readonly Color LampOn = new Color(1f, 0.95f, 0.35f, 1f);
        public static readonly Color PlinthColor = new Color(0.32f, 0.3f, 0.36f, 1f);
        public static readonly Color BarColor = new Color(0.2f, 0.2f, 0.24f, 1f);

        /// <summary>The plinth under the reward chest (its radius), with the lamps on a ring just inside its edge.</summary>
        public const float PlinthRadius = 0.62f * ScreenMath.ContentScale;
        public const float LampSize = 0.13f * ScreenMath.ContentScale;

        /// <summary>The light chain's run from the last plank to the roadblock, before its bars lift.</summary>
        public const float ChainSeconds = 0.8f;
        public const int ChainLinks = 10;

        /// <summary>Said when the light chain reaches the roadblock.</summary>
        public const string SolvedText = "Every plank lit. The chest is free.";

        /// <summary>The glow over a mended span (the gap-crosser*'s gold).</summary>
        public static readonly Color MendedGlow = new Color(1f, 0.8f, 0.15f, 0.55f);

        public static readonly Color MendedColor = new Color(1f, 0.92f, 0.6f);

        /// <summary>The shoreline: a foam line on the island's edge, and wet sand just inside it.</summary>
        public static readonly Color FoamColor = new Color(0.93f, 0.97f, 1f, 0.9f);
        public static readonly Color WetSandColor = new Color(0.45f, 0.36f, 0.1f, 0.4f);
        public const float FoamWidth = 0.07f * ScreenMath.ContentScale, WetSandWidth = 0.12f * ScreenMath.ContentScale;

        public static readonly Color SignColor = new Color(0.8f, 0.62f, 0.38f);
        public static readonly Color LoneTextColor = new Color(0.8f, 0.92f, 1f);

        public const string MendedText = "The gap is spanned!";

        /// <summary>Said on arriving on a lone doorway's islands (they join nothing else on the screen).</summary>
        public const string LoneText = "Only water here. Go round!";

        /// <summary>How long the lone doorway's words stay up.</summary>
        public const float LoneTextSeconds = 2.5f;

        private const int WaterOrder = -9;
        private const int PlankOrder = -8;
        private const int ShoreOrder = -7;
        private const int GlowOrder = -7;
        private const int SignOrder = -5;

        private readonly List<BrokenSpan> gaps = new List<BrokenSpan>();
        private readonly List<Collider2D> water = new List<Collider2D>();
        private readonly List<GameObject> signs = new List<GameObject>();
        private readonly Dictionary<int, List<SpriteRenderer>> plankArt = new Dictionary<int, List<SpriteRenderer>>();
        private readonly List<SpriteRenderer> lamps = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> bars = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> chain = new List<SpriteRenderer>();
        private SpriteRenderer[] glows = Array.Empty<SpriteRenderer>();
        private GameObject[] rims = Array.Empty<GameObject>();
        private bool[] lone = Array.Empty<bool>();
        private int lastIsland = -1;
        private bool onScreen;
        private float flashLeft;
        private float chainAge = -1f;
        private Vector2 chainFrom;
        private Transform roadblock;
        private Transform player;
        private float nextPlayerSearch;
        private Material material;

        /// <summary>Every enabled island screen (CubeNavigator tells them all when the player is put somewhere new).</summary>
        public static readonly List<IslandScreen> Active = new List<IslandScreen>();

        private void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
        }

        private void OnDisable() => Active.Remove(this);

        /// <summary>
        /// The player was put somewhere new (a teleport, any CubeNavigator placement; 4.12 review): every screen's walker
        /// forgets its last island and plank, so a teleport is never read as a crossing. The lights are left as they are.
        /// </summary>
        public static void ForgetSteps()
        {
            foreach (IslandScreen s in Active)
                if (s != null && s.Walker != null) s.Walker.Leave();
        }

        public IslandLayout Layout { get; private set; }

        /// <summary>The screen centre the layout's positions are relative to.</summary>
        public Vector2 Centre { get; private set; }

        /// <summary>The gaps of the broken spans (mended ones included), in plank order.</summary>
        public IReadOnlyList<BrokenSpan> Gaps => gaps;

        /// <summary>The solid water pieces.</summary>
        public IReadOnlyList<Collider2D> Water => water;

        /// <summary>The planks' lights (lit, mended) and the puzzle's state (solved).</summary>
        public PlankLights Lights { get; private set; }

        /// <summary>Turns the player's steps into plank crossings.</summary>
        public PlankWalker Walker { get; private set; }

        /// <summary>The reward chest on the roadblock (null until CubeWorld hands it over).</summary>
        public RewardChest Chest { get; private set; }

        /// <summary>True once the light chain has reached the roadblock and its bars lifted: the chest can be taken.</summary>
        public bool RoadblockOpen { get; private set; }

        bool IRewardGuard.IsOpen => RoadblockOpen;

        /// <summary>The roadblock's lamps, one per network plank (lit while that many planks are lit).</summary>
        public IReadOnlyList<SpriteRenderer> Lamps => lamps;

        /// <summary>The roadblock's bars over the chest (shown until it opens).</summary>
        public IReadOnlyList<SpriteRenderer> Bars => bars;

        /// <summary>True while the network flashes red after a snuff.</summary>
        public bool Flashing => flashLeft > 0f;

        /// <summary>True while the light chain runs to the roadblock.</summary>
        public bool ChainRunning => chainAge >= 0f;

        /// <summary>The glowing rim of an island (shown while all its network planks are lit), or null for an island off the network.</summary>
        public GameObject Rim(int island) => island >= 0 && island < rims.Length ? rims[island] : null;

        /// <summary>Raised on every crossing that changed the puzzle (lit, snuffed, solved), with the plank's index.</summary>
        public event Action<IslandScreen, int, CrossResult> Crossed;

        /// <summary>Raised when the light chain reaches the roadblock and it opens.</summary>
        public event Action<IslandScreen> Opened;

        /// <summary>The lookout signs on lone doorways' islands.</summary>
        public IReadOnlyList<GameObject> Signs => signs;

        /// <summary>The player whose steps light the planks (found in the scene when not set).</summary>
        public Transform Player
        {
            get => player;
            set => player = value;
        }

        /// <summary>Raised when the gap-crosser* mends a broken span, with the plank's index.</summary>
        public event Action<IslandScreen, int> Mended;


        /// <summary>The world position of an island's middle.</summary>
        public Vector2 IslandWorld(int island) => Centre + Layout.Islands[island].center;

        /// <summary>The colour a plank's glow shows for a light (clear while unlit).</summary>
        public static Color GlowFor(PlankLight light) =>
            light == PlankLight.Lit ? LitGlow : light == PlankLight.Mended ? MendedGlow : Color.clear;

        /// <summary>The glow over a plank (its renderer; disabled while unlit).</summary>
        public SpriteRenderer Glow(int plank) => glows[plank];

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

        /// <summary>
        /// The shoreline of an island (local rects): the foam line along each edge that meets the water (not an edge on the
        /// wall), then a band of wet sand just inside it. Both lie on the island, so the land the player can stand on is
        /// exactly what reads as land.
        /// </summary>
        public static List<(Rect Rect, bool Foam)> ShoreRects(Rect island)
        {
            Rect area = IslandLayout.WaterArea;
            var rects = new List<(Rect, bool)>();
            const float eps = 1e-4f;
            bool west = island.xMin > area.xMin + eps, east = island.xMax < area.xMax - eps;
            bool south = island.yMin > area.yMin + eps, north = island.yMax < area.yMax - eps;
            foreach ((float width, float inset, bool foam) in new[] { (FoamWidth, 0f, true), (WetSandWidth, FoamWidth, false) })
            {
                Rect r = Rect.MinMaxRect(island.xMin + (west ? inset : 0f), island.yMin + (south ? inset : 0f),
                    island.xMax - (east ? inset : 0f), island.yMax - (north ? inset : 0f));
                if (south) rects.Add((Rect.MinMaxRect(r.xMin, r.yMin, r.xMax, r.yMin + width), foam));
                if (north) rects.Add((Rect.MinMaxRect(r.xMin, r.yMax - width, r.xMax, r.yMax), foam));
                float y0 = r.yMin + (south ? width : 0f), y1 = r.yMax - (north ? width : 0f);
                if (west) rects.Add((Rect.MinMaxRect(r.xMin, y0, r.xMin + width, y1), foam));
                if (east) rects.Add((Rect.MinMaxRect(r.xMax - width, y0, r.xMax, y1), foam));
            }
            return rects;
        }

        /// <summary>The lookout sign's board (with its dark edge) and where it sits above the sign's spot.</summary>
        public static readonly Vector2 SignBoardSize = new Vector2(0.86f, 0.54f) * ScreenMath.ContentScale, SignBoardOffset = new Vector2(0f, 0.1f) * ScreenMath.ContentScale;

        /// <summary>The sign's spot keeps at least this far off the doorway's centre line (the walk in).</summary>
        public const float SignOffWalk = 0.6f * ScreenMath.ContentScale;

        /// <summary>The board of a sign standing at a spot (local rect).</summary>
        public static Rect SignBoard(Vector2 spot) => new Rect(spot + SignBoardOffset - SignBoardSize / 2f, SignBoardSize);

        /// <summary>Where the planks of an island leave it (local): each plank's end on the island's shore, a line across the plank.</summary>
        public static List<Rect> PlankMouths(IslandLayout layout, int island)
        {
            var mouths = new List<Rect>();
            Rect r = layout.Islands[island];
            foreach (IslandBridge b in layout.Bridges)
            {
                if (!b.Joins(island)) continue;
                Rect p = b.Plank;
                if (b.Vertical)
                {
                    float y = Mathf.Abs(p.yMin - r.yMax) < Mathf.Abs(p.yMax - r.yMin) ? p.yMin : p.yMax;
                    mouths.Add(Rect.MinMaxRect(p.xMin, y, p.xMax, y));
                }
                else
                {
                    float x = Mathf.Abs(p.xMin - r.xMax) < Mathf.Abs(p.xMax - r.xMin) ? p.xMin : p.xMax;
                    mouths.Add(Rect.MinMaxRect(x, p.yMin, x, p.yMax));
                }
            }
            return mouths;
        }

        /// <summary>The distance between two rects (0 when they touch or overlap).</summary>
        public static float RectGap(Rect a, Rect b)
        {
            float dx = Mathf.Max(0f, Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax));
            float dy = Mathf.Max(0f, Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Where a lone doorway's lookout sign stands (local): on the doorway's island with its whole board on the land,
        /// at least SignOffWalk aside of the walk in from the doorway, and with its board at least half its width clear of
        /// every plank mouth on the island. Of the spots that qualify, the one nearest the island's middle out to one side
        /// (searched on a 0.05 grid, so it is the same every time); with none, the spot farthest from the mouths.
        /// </summary>
        public static Vector2 SignSpot(IslandLayout layout, Facing side)
        {
            int door = layout.DoorIsland(side);
            Rect r = layout.Islands[door];
            List<Rect> mouths = PlankMouths(layout, door);
            float margin = SignBoardSize.x / 2f;
            Vector2 preferred = r.center + (side.IsHorizontal() ? Vector2.up : Vector2.right) * SignOffWalk;
            Vector2 best = preferred, fallback = preferred;
            float bestDistance = float.MaxValue, fallbackGap = -1f;
            const float step = 0.05f * ScreenMath.ContentScale;
            for (float x = r.xMin; x <= r.xMax + 1e-4f; x += step)
                for (float y = r.yMin; y <= r.yMax + 1e-4f; y += step)
                {
                    var spot = new Vector2(x, y);
                    Rect board = SignBoard(spot);
                    if (board.xMin < r.xMin - 1e-4f || board.xMax > r.xMax + 1e-4f || board.yMin < r.yMin - 1e-4f || board.yMax > r.yMax + 1e-4f)
                        continue;
                    if ((side.IsHorizontal() ? Mathf.Abs(y) : Mathf.Abs(x)) < SignOffWalk - 1e-4f) continue;
                    float gap = float.MaxValue;
                    foreach (Rect m in mouths) gap = Mathf.Min(gap, RectGap(board, m));
                    if (gap > fallbackGap + 1e-4f)
                    {
                        fallbackGap = gap;
                        fallback = spot;
                    }
                    if (gap < margin) continue;
                    float distance = (spot - preferred).sqrMagnitude;
                    if (distance < bestDistance - 1e-6f)
                    {
                        bestDistance = distance;
                        best = spot;
                    }
                }
            return bestDistance < float.MaxValue ? best : fallback;
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
            signs.Clear();
            plankArt.Clear();
            lamps.Clear();
            bars.Clear();
            chain.Clear();
            Lights = new PlankLights(layout);
            Walker = new PlankWalker(layout);
            lastIsland = -1;
            onScreen = false;
            flashLeft = 0f;
            chainAge = -1f;
            RoadblockOpen = false;
            Chest = null;
            roadblock = null;

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
            // The islands are the face's own floor, showing through the water; only their shorelines are drawn.
            lone = new bool[layout.Islands.Count];
            for (int i = 0; i < layout.Islands.Count; i++)
            {
                lone[i] = layout.IsLone(i);
                var shore = new GameObject($"Shore {i}").transform;
                shore.SetParent(transform, false);
                foreach ((Rect r, bool foam) in ShoreRects(layout.Islands[i]))
                    ArtCatalog.AddShape(shore, foam ? "Foam" : "Wet Sand", PartShape.Square, r.center, r.size, foam ? FoamColor : WetSandColor,
                        ShoreOrder, material);
            }
            glows = new SpriteRenderer[layout.Bridges.Count];
            for (int k = 0; k < layout.Bridges.Count; k++)
            {
                IslandBridge b = layout.Bridges[k];
                glows[k] = ArtCatalog.AddShape(transform, $"Plank {k} (glow)", PartShape.Square, b.Plank.center, b.Plank.size, Color.clear,
                    GlowOrder, material);
                glows[k].enabled = false;
                if (!b.Broken)
                {
                    Plank(k, $"Plank {k}", b.Plank, b.Vertical, material);
                    continue;
                }
                Rect gap = b.Gap;
                Rect near = b.Vertical ? Rect.MinMaxRect(b.Plank.xMin, b.Plank.yMin, b.Plank.xMax, gap.yMin)
                    : Rect.MinMaxRect(b.Plank.xMin, b.Plank.yMin, gap.xMin, b.Plank.yMax);
                Rect far = b.Vertical ? Rect.MinMaxRect(b.Plank.xMin, gap.yMax, b.Plank.xMax, b.Plank.yMax)
                    : Rect.MinMaxRect(gap.xMax, b.Plank.yMin, b.Plank.xMax, b.Plank.yMax);
                Plank(k, $"Plank {k}", near, b.Vertical, material);
                Plank(k, $"Plank {k} (far)", far, b.Vertical, material);
                gaps.Add(BrokenSpan.Create(this, k, gap, b.Vertical, material));
            }
            for (int f = 0; f < 4; f++)
            {
                int door = layout.DoorIsland((Facing)f);
                if (door >= 0 && lone[door]) signs.Add(BuildSign((Facing)f, material));
            }
            // 4.12: a glowing rim round each network island, shown once all its network planks are lit.
            rims = new GameObject[layout.Islands.Count];
            for (int i = 0; i < layout.Islands.Count; i++)
            {
                if (!layout.OnNetwork(i)) continue;
                var rim = new GameObject($"Rim {i}").transform;
                rim.SetParent(transform, false);
                foreach (Rect r in RimRects(layout.Islands[i]))
                    ArtCatalog.AddShape(rim, "Rim", PartShape.Square, r.center, r.size, RimColor, ShoreOrder + 1, material);
                rim.gameObject.SetActive(false);
                rims[i] = rim.gameObject;
            }
            for (int k = 0; k < layout.Bridges.Count; k++) ShowLight(k);
        }

        /// <summary>The rim of an island (local rects): a band along every edge that meets the water, on the island itself.</summary>
        public static List<Rect> RimRects(Rect island)
        {
            Rect area = IslandLayout.WaterArea;
            const float eps = 1e-4f;
            var rects = new List<Rect>();
            float w = RimWidth;
            if (island.yMin > area.yMin + eps) rects.Add(Rect.MinMaxRect(island.xMin, island.yMin, island.xMax, island.yMin + w));
            if (island.yMax < area.yMax - eps) rects.Add(Rect.MinMaxRect(island.xMin, island.yMax - w, island.xMax, island.yMax));
            if (island.xMin > area.xMin + eps) rects.Add(Rect.MinMaxRect(island.xMin, island.yMin, island.xMin + w, island.yMax));
            if (island.xMax < area.xMax - eps) rects.Add(Rect.MinMaxRect(island.xMax - w, island.yMin, island.xMax, island.yMax));
            return rects;
        }

        private SpriteRenderer Plank(int bridge, string name, Rect r, bool vertical, Material material)
        {
            // The plank art runs along x; a north-south plank is turned a quarter.
            Vector2 size = vertical ? new Vector2(r.height, r.width) : r.size;
            SpriteRenderer plank = ArtCatalog.AddSprite(transform, name, ArtKey.PlankBridge, r.center, size, PlankColor, PlankOrder, material, false);
            if (vertical) plank.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            if (!plankArt.TryGetValue(bridge, out List<SpriteRenderer> list)) plankArt[bridge] = list = new List<SpriteRenderer>();
            list.Add(plank);
            return plank;
        }

        /// <summary>A lone doorway's lookout sign: a post and a board with an arrow pointing back out of the doorway.</summary>
        private GameObject BuildSign(Facing side, Material material)
        {
            var go = new GameObject($"Lookout Sign ({side})");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = SignSpot(Layout, side);
            ArtCatalog.AddShape(go.transform, "Post", PartShape.Square, new Vector2(0f, -0.2f) * ScreenMath.ContentScale, new Vector2(0.1f, 0.55f) * ScreenMath.ContentScale,
                new Color(0.35f, 0.24f, 0.12f), SignOrder, material);
            ArtCatalog.AddShape(go.transform, "Board Edge", PartShape.Square, SignBoardOffset, SignBoardSize,
                new Color(0.3f, 0.2f, 0.1f), SignOrder + 1, material);
            ArtCatalog.AddShape(go.transform, "Board", PartShape.Square, new Vector2(0f, 0.1f) * ScreenMath.ContentScale, new Vector2(0.78f, 0.46f) * ScreenMath.ContentScale, SignColor,
                SignOrder + 2, material);
            SpriteRenderer arrow = ArtCatalog.AddShape(go.transform, "Arrow", PartShape.Triangle, new Vector2(0f, 0.1f) * ScreenMath.ContentScale,
                new Vector2(0.34f, 0.32f) * ScreenMath.ContentScale, new Color(0.25f, 0.16f, 0.08f), SignOrder + 3, material);
            arrow.transform.localRotation = Quaternion.Euler(0f, 0f, ScreenEdges.ArchAngle(side));
            return go;
        }

        /// <summary>Colours a plank (all its pieces, a laid span included) and its glow for its light.</summary>
        private void ShowLight(int bridge)
        {
            PlankLight light = Lights[bridge];
            bool flash = flashLeft > 0f && Layout.IsNetworkPlank(bridge);
            if (plankArt.TryGetValue(bridge, out List<SpriteRenderer> list))
                foreach (SpriteRenderer r in list)
                    if (r != null) r.color = light == PlankLight.Unlit && !flash ? PlankColor : LitPlankColor;
            SpriteRenderer glow = glows[bridge];
            if (glow == null) return;
            glow.color = flash ? SnuffGlow : GlowFor(light);
            glow.enabled = flash || light != PlankLight.Unlit;
        }

        /// <summary>Every plank's light, every rim and every lamp from the puzzle's state.</summary>
        private void ShowAll()
        {
            for (int k = 0; k < Layout.Bridges.Count; k++) ShowLight(k);
            for (int i = 0; i < rims.Length; i++)
                if (rims[i] != null) rims[i].SetActive(Lights.IslandComplete(i));
            for (int i = 0; i < lamps.Count; i++)
                if (lamps[i] != null) lamps[i].color = i < Lights.LitCount ? LampOn : LampOff;
        }

        /// <summary>
        /// The player standing at a world point (4.12): a crossing of a network plank (PlankWalker) lights it, snuffs every
        /// plank (a lit one crossed again: a red flash) or solves the network (the light chain starts); arriving on a lone
        /// doorway's islands from anywhere else says "go round". Off the screen it forgets where the player was, and an
        /// unsolved network goes dark.
        /// </summary>
        public void Visit(Vector2 world)
        {
            Vector2 local = world - Centre;
            Vector2 half = ScreenEdges.Half;
            if (Mathf.Abs(local.x) > half.x || Mathf.Abs(local.y) > half.y)
            {
                if (onScreen) Leave();
                return;
            }
            onScreen = true;
            Walker.Step(local, out _, out _);
            foreach ((int plank, _, _) in Walker.Crossings) Cross(plank);
            int island = Layout.IslandAt(local);
            if (island < 0) return;
            if (lone[island] && (lastIsland < 0 || !lone[lastIsland]))
                PickupPopup.Show(Centre + Layout.Islands[island].center, LoneText, LoneTextColor, LoneTextSeconds);
            lastIsland = island;
        }

        /// <summary>The player crossed a plank (from one island onto the other): the puzzle's response, shown.</summary>
        public CrossResult Cross(int plank)
        {
            CrossResult result = Lights.Cross(plank);
            if (result == CrossResult.None) return result;
            if (result == CrossResult.Snuffed) flashLeft = FlashSeconds;
            if (result == CrossResult.Solved)
            {
                chainFrom = Centre + Layout.Bridges[plank].Plank.center;
                StartChain();
            }
            ShowAll();
            Crossed?.Invoke(this, plank, result);
            return result;
        }

        /// <summary>The player left the screen: forget where it stood; an unsolved network goes dark (no flash).</summary>
        public void Leave()
        {
            onScreen = false;
            lastIsland = -1;
            Walker.Leave();
            if (!Lights.Reset()) return;
            flashLeft = 0f;
            ShowAll();
        }

        // ------------------------------------------------------------------ The reward roadblock (4.12)

        /// <summary>
        /// Takes the reward chest CubeWorld built on the goal (IPuzzleRewardHost): it stands on a plinth with one lamp per
        /// network plank round its edge, under bars, and stays shut (this screen is its guard) until the network is solved and
        /// the light chain has reached it.
        /// </summary>
        public void AttachReward(RewardChest chest)
        {
            if (chest == null) return;
            Chest = chest;
            chest.Guard = this;
            if (roadblock != null) Destroy(roadblock.gameObject);
            lamps.Clear();
            bars.Clear();
            roadblock = new GameObject("Reward Roadblock").transform;
            roadblock.SetParent(transform, false);
            roadblock.position = chest.transform.position;
            ArtCatalog.AddShape(roadblock, "Plinth", PartShape.Circle, Vector2.zero, Vector2.one * 2f * PlinthRadius, PlinthColor, 3, material);
            int count = Layout.NetworkPlanks.Count;
            float ring = PlinthRadius - LampSize * 0.8f;
            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.PI / 2f - 2f * Mathf.PI * i / Mathf.Max(1, count);
                var at = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring;
                lamps.Add(ArtCatalog.AddShape(roadblock, $"Lamp {i}", PartShape.Circle, at, Vector2.one * LampSize, LampOff, 4, material));
            }
            float h = RewardChest.Size * 0.62f;
            for (int i = -1; i <= 1; i++)
                bars.Add(ArtCatalog.AddShape(roadblock, $"Bar {i + 1}", PartShape.Square, new Vector2(i * RewardChest.Size * 0.32f, 0f),
                    new Vector2(0.07f * ScreenMath.ContentScale, 2f * h), BarColor, 7, material));
            bars.Add(ArtCatalog.AddShape(roadblock, "Bar Top", PartShape.Square, new Vector2(0f, h),
                new Vector2(RewardChest.Size * 0.8f, 0.08f * ScreenMath.ContentScale), BarColor, 7, material));
            if (Lights.Solved && !ChainRunning) OpenRoadblock(false);
            ShowAll();
        }

        private void StartChain()
        {
            chainAge = 0f;
            foreach (SpriteRenderer r in chain)
                if (r != null) Destroy(r.gameObject);
            chain.Clear();
            Vector2 to = Chest != null ? (Vector2)Chest.transform.position : Centre + Layout.GoalSpot;
            for (int i = 0; i < ChainLinks; i++)
            {
                Vector2 at = Vector2.Lerp(chainFrom, to, (i + 1f) / ChainLinks);
                SpriteRenderer dot = ArtCatalog.AddShape(transform, $"Light Chain {i}", PartShape.Circle, at - Centre,
                    Vector2.one * 0.22f * ScreenMath.ContentScale, LampOn, 8, material);
                dot.enabled = false;
                chain.Add(dot);
            }
        }

        /// <summary>The bars lift and the chest can be taken (after the chain).</summary>
        private void OpenRoadblock(bool say)
        {
            if (RoadblockOpen) return;
            RoadblockOpen = true;
            chainAge = -1f;
            foreach (SpriteRenderer b in bars)
                if (b != null) b.enabled = false;
            foreach (SpriteRenderer r in chain)
                if (r != null) Destroy(r.gameObject);
            chain.Clear();
            if (say) PickupPopup.Show(Centre + Layout.GoalSpot, SolvedText, LoneTextColor);
            Opened?.Invoke(this);
        }

        private void Update()
        {
            if (Layout == null) return;
            if (flashLeft > 0f)
            {
                flashLeft -= Time.deltaTime;
                if (flashLeft <= 0f)
                {
                    flashLeft = 0f;
                    ShowAll();
                }
            }
            if (chainAge >= 0f)
            {
                chainAge += Time.deltaTime;
                int shown = Mathf.Clamp(Mathf.CeilToInt(chainAge / ChainSeconds * ChainLinks), 0, ChainLinks);
                for (int i = 0; i < chain.Count; i++)
                    if (chain[i] != null) chain[i].enabled = i < shown;
                if (chainAge >= ChainSeconds) OpenRoadblock(true);
            }
            if (player == null)
            {
                if (Time.unscaledTime < nextPlayerSearch) return;
                nextPlayerSearch = Time.unscaledTime + 1f;
                CubeNavigator navigator = FindAnyObjectByType<CubeNavigator>();
                if (navigator == null) return;
                player = navigator.transform;
            }
            Visit(player.position);
        }

        /// <summary>
        /// Mends a broken span (the gap-crosser* swung at its gap): the missing plank is laid, the gap stops blocking and the
        /// span changes colour. False when the plank is not a gap (whole, or mended already).
        /// </summary>
        public bool Mend(int bridge)
        {
            foreach (BrokenSpan gap in gaps)
            {
                if (gap == null || gap.Bridge != bridge || gap.IsMended) continue;
                IslandBridge b = Layout.Bridges[bridge];
                Plank(bridge, $"Plank {bridge} (laid)", b.Gap, b.Vertical, material);
                gap.ShowMended();
                Lights.Mend(bridge);
                ShowLight(bridge);
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

        /// <summary>The plank is laid: the hole and the mark go and the gap stops blocking (the owner colours the span).</summary>
        public void ShowMended()
        {
            IsMended = true;
            if (solid != null) solid.enabled = false;
            if (hole != null) hole.gameObject.SetActive(false);
            if (mark != null) mark.gameObject.SetActive(false);
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
            span.mark = ArtCatalog.AddShape(go.transform, "Gap Mark", PartShape.Diamond, Vector2.zero, new Vector2(0.3f, 0.3f) * ScreenMath.ContentScale,
                GapCrosserItem.ItemColor, -6, material);
            return span;
        }
    }
}
