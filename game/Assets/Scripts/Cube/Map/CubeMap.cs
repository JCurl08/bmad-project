using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Cube
{
    /// <summary>What the map shows for one screen.</summary>
    public readonly struct MapCellView
    {
        public readonly ScreenAddress Screen;
        public readonly Vector2Int NetCell;
        public readonly bool Visited;

        /// <summary>True only for a visited screen on a sealed face (an unvisited one never shows it is sealed).</summary>
        public readonly bool Sealed;

        /// <summary>The region colour (CubeWorld.ThemeColor of its face's theme) when visited, else the dark fog colour.</summary>
        public readonly Color Color;

        public MapCellView(ScreenAddress screen, Vector2Int netCell, bool visited, bool sealedFace, Color color)
        {
            Screen = screen;
            NetCell = netCell;
            Visited = visited;
            Sealed = sealedFace;
            Color = color;
        }
    }

    /// <summary>
    /// The pause map (CAP-26): the Player/Map action (M, or Tab) opens a full-screen cube net (CubeNet: Town in the middle
    /// of the cross, its neighbours round it, the opposite face at the end) with every face's 3x3 screens. Screens visited
    /// this run (VisitTracker) are filled in their region colour (CubeWorld.ThemeColor); the rest stay dark. Markers
    /// (MapMarkers) show the player and which way they face, the Town start screen, gates (opened ones hollow), item
    /// pickups, trials (solved ones filled) and core portals, on visited screens only. In the core arena a "core" banner
    /// replaces the player marker. While open, gameplay pauses (GamePause: time stopped, player, attack and dialogue input
    /// ignored); pressing the key again (or Escape) closes it and resumes. It cannot open while the between-runs screen is
    /// up and closes if that screen opens. Drawn with OnGUI, like the other HUDs, and readable on the 960x600 canvas.
    /// </summary>
    [DefaultExecutionOrder(1000)] // LateUpdate after every gameplay script's (e.g. MassMitt's): see LateUpdate
    public class CubeMap : MonoBehaviour
    {
        public const string MapActionPath = "Player/Map";
        public const string Title = "Map";
        public const string CloseHint = "[M] / [Tab] close";
        public const string CoreLabel = "You are in the core";

        /// <summary>The legend's labels, in drawing order (one per marker style).</summary>
        public static readonly string[] LegendLabels =
            { "You", "Start", "Gate", "Opened gate", "Item", "Trial", "Trial solved", "Core portal" };

        /// <summary>Every piece of text the map can show (for the voice-rule tests).</summary>
        public static IEnumerable<string> AllTexts()
        {
            yield return Title;
            yield return CloseHint;
            yield return CoreLabel;
            foreach (string label in LegendLabels) yield return label;
        }

        public static readonly Color FogColor = new Color(0.1f, 0.1f, 0.13f);
        public static readonly Color SealedColor = new Color(0.05f, 0.05f, 0.06f);

        private static readonly Color GateColor = new Color(0.85f, 0.85f, 0.85f);
        private static readonly Color ItemColor = new Color(1f, 0.82f, 0.25f);
        private static readonly Color TrialColor = new Color(0.45f, 0.9f, 1f);
        private static readonly Color PortalColor = new Color(1f, 0.4f, 0.8f);
        private static readonly Color StartColor = Color.white;
        private static readonly Color PlayerColor = new Color(1f, 0.35f, 0.3f);

        [SerializeField] private CubeWorld world;
        [SerializeField] private CubeNavigator navigator;
        [SerializeField] private VisitTracker tracker;
        [SerializeField] private RunLoop loop;

        private InputAction mapAction;
        private CubeNet net;
        private List<MapMarker> allMarkers = new List<MapMarker>();
        private CubeWorld subscribed;

        private Texture2D disc;
        private Texture2D ring;
        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle bannerStyle;

        public CubeWorld World
        {
            get => world;
            set
            {
                Unsubscribe();
                world = value;
                net = null;
                if (isActiveAndEnabled) Subscribe();
            }
        }

        public CubeNavigator Navigator
        {
            get => navigator;
            set => navigator = value;
        }

        public VisitTracker Tracker
        {
            get => tracker;
            set => tracker = value;
        }

        public RunLoop Loop
        {
            get
            {
                if (loop == null) loop = FindAnyObjectByType<RunLoop>();
                return loop;
            }
            set => loop = value;
        }

        public bool IsOpen { get; private set; }

        /// <summary>False with no world yet or while the between-runs screen is up.</summary>
        public bool CanOpen => world != null && world.Model != null && !(Loop != null && Loop.ScreenOpen);

        /// <summary>The net for the current run's model (Town in the middle).</summary>
        public CubeNet Net
        {
            get
            {
                if (world == null || world.Model == null) return null;
                if (net == null || net.FaceSize != world.Model.FaceSize) net = CubeNet.For(world.Model);
                return net;
            }
        }

        /// <summary>True while the player is in the core arena (the map shows the core banner instead of a player marker).</summary>
        public bool PlayerInCore => navigator != null && navigator.InCoreArena;

        /// <summary>The screen the player marker is on (meaningless in the core arena).</summary>
        public ScreenAddress PlayerScreen => navigator != null ? navigator.Current : default;

        /// <summary>
        /// The way the player faces, in the face's frame: PlayerMover.Facing (the last move direction, reset to the entry
        /// facing on every teleport and run start); the navigator's entry facing when the player has no mover.
        /// </summary>
        public Vector2 PlayerDirection
        {
            get
            {
                if (navigator == null) return Vector2.right;
                if (navigator.TryGetComponent(out PlayerMover mover)) return mover.Facing;
                return navigator.Facing.ToVector();
            }
        }

        /// <summary>The way the player faces, in the net's frame (what the arrow on the map shows).</summary>
        public Vector2 PlayerNetDirection => Net != null && navigator != null ? Net.ToNetDirection(PlayerScreen.Face, PlayerDirection) : Vector2.right;

        /// <summary>The markers on visited screens, as of the last refresh (opening the map refreshes them).</summary>
        public IReadOnlyList<MapMarker> Markers => MapMarkers.Visible(allMarkers, tracker != null ? tracker.Visits : null);

        /// <summary>What the map shows for a screen right now.</summary>
        public MapCellView CellView(ScreenAddress screen)
        {
            CubeModel model = world.Model;
            bool visited = tracker != null && tracker.IsVisited(screen);
            // Fog first: an unvisited screen is always dark, sealed or not (nothing about it is revealed).
            bool sealedShown = visited && model.IsSealed(screen.Face);
            Color color = !visited ? FogColor : sealedShown ? SealedColor : CubeWorld.ThemeColor(model.ThemeOf(screen.Face));
            return new MapCellView(screen, Net.NetCell(screen), visited, sealedShown, color);
        }

        /// <summary>Opens the map and pauses the game. Returns false when it cannot open (or is already open).</summary>
        public bool Open()
        {
            if (IsOpen || !CanOpen) return false;
            if (tracker != null) tracker.Track();
            Refresh();
            IsOpen = true;
            GamePause.Pause();
            return true;
        }

        /// <summary>Closes the map and resumes the game. Returns false when it was closed.</summary>
        public bool Close()
        {
            if (!IsOpen) return false;
            IsOpen = false;
            GamePause.Resume();
            return true;
        }

        public bool Toggle() => IsOpen ? Close() : Open();

        /// <summary>Re-reads the markers from the live world (gates opened, items taken, trials solved since).</summary>
        public void Refresh()
        {
            allMarkers = MapMarkers.FromWorld(world);
        }

        private void OnEnable()
        {
            mapAction = InputSystem.actions != null ? InputSystem.actions.FindAction(MapActionPath) : null;
            if (mapAction == null) Debug.LogError($"CubeMap: input action '{MapActionPath}' not found in the project-wide actions.", this);
            else mapAction.Enable();
            Subscribe();
        }

        private void OnDisable()
        {
            Close();
            Unsubscribe();
            mapAction = null; // shared project-wide action: never disabled here
        }

        private void OnDestroy()
        {
            if (disc != null) Destroy(disc);
            if (ring != null) Destroy(ring);
        }

        // LateUpdate, and late in the execution order (DefaultExecutionOrder 1000): every gameplay Update and LateUpdate
        // (DialogueBox, NpcTalker, PlayerAttack, Equipment, MassMitt) has already run this frame, still paused, when a press
        // closes the map; a press is only "this frame's" for one frame, so none of them ever sees the closing frame's input
        // (e.g. Escape never also skips the conversation behind the map, Interact never grabs a boulder).
        private void LateUpdate()
        {
            if (IsOpen && !CanOpen)
            {
                Close();
                return;
            }
            if (mapAction != null && mapAction.WasPressedThisFrame())
            {
                Toggle();
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (IsOpen && keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Close();
        }

        private void Subscribe()
        {
            if (world == null || subscribed == world) return;
            Unsubscribe();
            subscribed = world;
            world.Rebuilt += OnWorldChanged;
            world.Revealed += OnWorldChanged;
        }

        private void Unsubscribe()
        {
            if (subscribed == null) return;
            subscribed.Rebuilt -= OnWorldChanged;
            subscribed.Revealed -= OnWorldChanged;
            subscribed = null;
        }

        /// <summary>A new run or the science reveal: the net and markers follow the new layout.</summary>
        private void OnWorldChanged()
        {
            net = null;
            Refresh();
        }

        // ---------------- Drawing ----------------

        /// <summary>Gap between faces in the net (pixels).</summary>
        private const float FaceGap = 6f;

        /// <summary>The cell size (pixels) the net is drawn with on a screen of the given size: as big as fits, 8..64.</summary>
        public static float CellSize(float screenWidth, float screenHeight, int faceSize)
        {
            const float margin = 16f, header = 56f, legend = 52f;
            float availW = screenWidth - 2f * margin - (CubeNet.Columns - 1) * FaceGap;
            float availH = screenHeight - header - legend - margin - (CubeNet.Rows - 1) * FaceGap;
            float cell = Mathf.Floor(Mathf.Min(availW / (CubeNet.Columns * faceSize), availH / (CubeNet.Rows * faceSize)));
            return Mathf.Clamp(cell, 8f, 64f);
        }

        private void OnGUI()
        {
            if (!IsOpen || Net == null) return;
            GUI.depth = -50; // over the HUDs, under the between-runs screen
            EnsureStyles();

            float w = Screen.width, h = Screen.height;
            GUI.color = new Color(0.02f, 0.02f, 0.05f, 0.94f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(16f, 8f, 200f, 36f), Title, titleStyle);
            GUI.Label(new Rect(w - 216f, 14f, 200f, 24f), CloseHint, textStyle);

            CubeNet n = Net;
            int size = n.FaceSize;
            float cell = CellSize(w, h, size);
            float faceSpan = cell * size;
            float netW = CubeNet.Columns * faceSpan + (CubeNet.Columns - 1) * FaceGap;
            float netH = CubeNet.Rows * faceSpan + (CubeNet.Rows - 1) * FaceGap;
            float left = Mathf.Floor((w - netW) / 2f);
            float top = 56f + Mathf.Floor(Mathf.Max(0f, (h - 56f - 52f - netH) / 2f));

            Rect CellRect(Vector2Int netCell)
            {
                int slotX = netCell.x / size, slotY = netCell.y / size;
                int rowFromTop = CubeNet.Rows * size - 1 - netCell.y;
                float x = left + netCell.x * cell + slotX * FaceGap;
                float y = top + rowFromTop * cell + (CubeNet.Rows - 1 - slotY) * FaceGap;
                return new Rect(x, y, cell, cell);
            }

            // Faces: a frame, then each screen (fog or region colour, checkered like the world's floors).
            foreach (NetPlacement p in n.Placements)
            {
                float fx = left + p.Slot.x * (faceSpan + FaceGap);
                float fy = top + (CubeNet.Rows - 1 - p.Slot.y) * (faceSpan + FaceGap);
                Fill(new Rect(fx - 2f, fy - 2f, faceSpan + 4f, faceSpan + 4f), new Color(0.35f, 0.35f, 0.42f));
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        MapCellView view = CellView(new ScreenAddress(p.Face, x, y));
                        Color c = view.Color;
                        if (view.Visited && ((x + y) & 1) == 1) c = Color.Lerp(c, Color.black, 0.15f);
                        Rect r = CellRect(view.NetCell);
                        Fill(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f), c);
                    }
                }
            }

            float glyph = Mathf.Max(6f, Mathf.Round(cell * 0.2f));
            foreach (MapMarker m in Markers)
            {
                Rect r = CellRect(n.NetCell(m.Screen));
                Vector2 local = n.ToNetLocal(m.Screen.Face, m.Local);
                var at = new Vector2(r.x + local.x * r.width, r.y + (1f - local.y) * r.height);
                DrawMarker(m.Kind, m.Done, at, glyph);
            }

            if (PlayerInCore)
            {
                var banner = new Rect(w / 2f - 180f, top + netH / 2f - 24f, 360f, 48f);
                Fill(banner, new Color(0.2f, 0.02f, 0.15f, 0.9f));
                GUI.color = PortalColor;
                GUI.Label(banner, CoreLabel, bannerStyle);
                GUI.color = Color.white;
            }
            else if (navigator != null)
            {
                ScreenAddress here = PlayerScreen;
                Rect r = CellRect(n.NetCell(here));
                // The current screen: a dark ring with a white one inside, so it shows on every region colour.
                Outline(r, MarkerOutlineColor, MarkerOutlineWidth);
                Outline(new Rect(r.x + MarkerOutlineWidth, r.y + MarkerOutlineWidth, r.width - 2f * MarkerOutlineWidth,
                    r.height - 2f * MarkerOutlineWidth), Color.white, MarkerOutlineWidth);
                Vector2 local = n.ToNetLocal(here.Face,
                    MapMarkers.LocalIn(world.ScreenCenter(here), navigator.transform.position));
                var at = new Vector2(r.x + local.x * r.width, r.y + (1f - local.y) * r.height);
                DrawPlayer(at, PlayerNetDirection, glyph * 1.4f);
            }

            DrawLegend(w, h, glyph);
        }

        /// <summary>Every marker sits on a dark outline (or dark backing) this wide, so it reads on every region colour.</summary>
        public const float MarkerOutlineWidth = 2f;

        /// <summary>The marker outline's colour.</summary>
        public static readonly Color MarkerOutlineColor = Color.black;

        /// <summary>The fill colour of each marker (and the player and the current-screen ring), for the contrast check.</summary>
        public static IEnumerable<KeyValuePair<string, Color>> MarkerFills()
        {
            yield return new KeyValuePair<string, Color>("You", PlayerColor);
            yield return new KeyValuePair<string, Color>("Here", Color.white);
            yield return new KeyValuePair<string, Color>("Start", StartColor);
            yield return new KeyValuePair<string, Color>("Gate", GateColor);
            yield return new KeyValuePair<string, Color>("Item", ItemColor);
            yield return new KeyValuePair<string, Color>("Trial", TrialColor);
            yield return new KeyValuePair<string, Color>("Portal", PortalColor);
        }

        private static Rect Grow(Rect r, float by) => new Rect(r.x - by, r.y - by, r.width + 2f * by, r.height + 2f * by);

        private void DrawMarker(MapMarkerKind kind, bool done, Vector2 at, float s)
        {
            float o = MarkerOutlineWidth;
            Color dark = MarkerOutlineColor;
            switch (kind)
            {
                case MapMarkerKind.Start:
                    // A dark ring round the white one, so it shows on Town's white as well as on the dark fog.
                    var box = new Rect(at.x - s / 2f, at.y - s / 2f, s, s);
                    Outline(Grow(box, o), dark, o);
                    Outline(box, StartColor, 2f);
                    Outline(new Rect(box.x + 2f, box.y + 2f, box.width - 4f, box.height - 4f), dark, o);
                    break;
                case MapMarkerKind.Gate:
                    var bar = new Rect(at.x - s * 0.8f, at.y - s * 0.3f, s * 1.6f, s * 0.6f);
                    Fill(Grow(bar, o), dark);
                    if (done) Outline(bar, GateColor, 1f);
                    else Fill(bar, GateColor);
                    break;
                case MapMarkerKind.Item:
                    var square = new Rect(at.x - s / 2f, at.y - s / 2f, s, s);
                    Fill(Grow(square, o), dark);
                    Fill(square, ItemColor);
                    break;
                case MapMarkerKind.Trial:
                    Tex(Disc, new Rect(at.x - s * 0.6f - o, at.y - s * 0.6f - o, s * 1.2f + 2f * o, s * 1.2f + 2f * o), dark);
                    Tex(done ? Disc : Ring, new Rect(at.x - s * 0.6f, at.y - s * 0.6f, s * 1.2f, s * 1.2f), TrialColor);
                    break;
                case MapMarkerKind.Portal:
                    Tex(Disc, new Rect(at.x - s * 0.75f - o, at.y - s * 0.75f - o, s * 1.5f + 2f * o, s * 1.5f + 2f * o), dark);
                    Tex(Ring, new Rect(at.x - s * 0.75f, at.y - s * 0.75f, s * 1.5f, s * 1.5f), PortalColor);
                    Tex(Disc, new Rect(at.x - s * 0.3f, at.y - s * 0.3f, s * 0.6f, s * 0.6f), PortalColor);
                    break;
            }
        }

        private void DrawPlayer(Vector2 at, Vector2 netDirection, float s)
        {
            // A dark ring, a white ring, then the red disc: readable on red, white and every other region colour.
            float o = MarkerOutlineWidth;
            Tex(Disc, new Rect(at.x - s / 2f - 2f * o, at.y - s / 2f - 2f * o, s + 4f * o, s + 4f * o), MarkerOutlineColor);
            Tex(Disc, new Rect(at.x - s / 2f - o, at.y - s / 2f - o, s + 2f * o, s + 2f * o), Color.white);
            Tex(Disc, new Rect(at.x - s / 2f, at.y - s / 2f, s, s), PlayerColor);
            // The facing: a nub out of the disc (net y is up; GUI y is down).
            Vector2 d = netDirection.sqrMagnitude > 1e-6f ? netDirection.normalized : Vector2.right;
            var tip = new Vector2(at.x + d.x * s * 0.75f, at.y - d.y * s * 0.75f);
            float nub = Mathf.Max(3f, s * 0.4f);
            Tex(Disc, new Rect(tip.x - nub / 2f - 1f, tip.y - nub / 2f - 1f, nub + 2f, nub + 2f), MarkerOutlineColor);
            Tex(Disc, new Rect(tip.x - nub / 2f, tip.y - nub / 2f, nub, nub), Color.white);
        }

        private void DrawLegend(float w, float h, float s)
        {
            float y = h - 40f;
            float x = 16f;
            float step = Mathf.Max(100f, (w - 32f) / LegendLabels.Length);
            for (int i = 0; i < LegendLabels.Length; i++)
            {
                var at = new Vector2(x + s, y + 11f);
                switch (i)
                {
                    case 0: DrawPlayer(at, Vector2.right, s * 1.4f); break;
                    case 1: DrawMarker(MapMarkerKind.Start, false, at, s); break;
                    case 2: DrawMarker(MapMarkerKind.Gate, false, at, s); break;
                    case 3: DrawMarker(MapMarkerKind.Gate, true, at, s); break;
                    case 4: DrawMarker(MapMarkerKind.Item, false, at, s); break;
                    case 5: DrawMarker(MapMarkerKind.Trial, false, at, s); break;
                    case 6: DrawMarker(MapMarkerKind.Trial, true, at, s); break;
                    default: DrawMarker(MapMarkerKind.Portal, false, at, s); break;
                }
                GUI.Label(new Rect(x + s * 2f + 6f, y, step - s * 2f - 6f, 24f), LegendLabels[i], textStyle);
                x += step;
            }
        }

        private static void Fill(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private static void Outline(Rect r, Color c, float t)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        private static void Tex(Texture2D texture, Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, texture);
            GUI.color = Color.white;
        }

        private Texture2D Disc => disc != null ? disc : disc = Circle(false);
        private Texture2D Ring => ring != null ? ring : ring = Circle(true);

        /// <summary>A white anti-aliased disc (or ring) on transparency, 32 px.</summary>
        private static Texture2D Circle(bool hollow)
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[size * size];
            float c = (size - 1) / 2f, outer = size / 2f - 0.5f, inner = outer - 6f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01(outer - d + 0.5f);
                    if (hollow) a *= Mathf.Clamp01(d - inner + 0.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = Color.white;
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            textStyle.normal.textColor = new Color(0.9f, 0.9f, 0.95f);
            bannerStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bannerStyle.normal.textColor = Color.white;
        }
    }
}
