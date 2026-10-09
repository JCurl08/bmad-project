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
    /// replaces the player marker. Inside a town building (4.6) the player stands on their town's screen, the marker at
    /// the building's door, with an "Inside the shop" (hall, home) badge in the header. Down in a lair room (4.13) it is the same, at the portal
    /// they came down by, with an "In the Demon's lair" badge. A portal whose lair room has been visited shows barred ("Lair"),
    /// and the real portal, once known, gold ("Core portal"); the rest show as plain portals. While open, gameplay pauses (GamePause: time stopped, player, attack and dialogue input
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
            { "You", "Start", "Town", "Mended town", "Gate", "Opened gate", "Item", "Trial", "Trial solved", "Portal", "Lair", "Core portal", "Locked arch" };

        /// <summary>Every piece of text the map can show (for the voice-rule tests).</summary>
        public static IEnumerable<string> AllTexts()
        {
            yield return Title;
            yield return CloseHint;
            yield return QuestText.MapHint;
            yield return CoreLabel;
            foreach (string label in LegendLabels) yield return label;
            foreach (InteriorKind kind in new[] { InteriorKind.Shop, InteriorKind.LeadersHall, InteriorKind.Home })
                yield return InsideText(kind);
            yield return LairText.MapBadge;
            yield return TempleText.MapBadge;
            yield return RewardText.FragmentCount(2, 4, false);
            yield return RewardText.FragmentCount(1, 1, true);
            yield return RewardText.FragmentCount(4, 4, true, false);
        }

        /// <summary>The map's header band (title, inside badge, close hint): the net starts below it.</summary>
        public const float HeaderHeight = 56f;

        /// <summary>The title's and the close hint's places in the header, at a screen width.</summary>
        public static Rect TitleRect(float w) => new Rect(16f, 8f, 200f, 36f);
        public static Rect CloseHintRect(float w) => new Rect(w - 216f, 14f, 200f, 24f);

        /// <summary>The quest log's key (4.18, QuestLogView), under the close hint.</summary>
        public static Rect QuestHintRect(float w) => new Rect(w - 216f, 38f, 200f, 18f);

        /// <summary>The inside badge's width: room for the longest badge ("Inside the leader's hall") at 16 pt bold.</summary>
        public const float InsideBadgeWidth = 300f;

        /// <summary>The inside badge's font size (bold).</summary>
        public const int InsideFontSize = 16;

        /// <summary>Where the inside badge sits: centred in the header, between the title and the close hint.</summary>
        public static Rect InsideBadgeRect(float w) => new Rect(Mathf.Round(w / 2f - InsideBadgeWidth / 2f), 11f, InsideBadgeWidth, 32f);

        /// <summary>The badge in the map's header while the player is inside a town building ("Inside the shop").</summary>
        public static string InsideText(InteriorKind kind) => $"Inside the {TownInterior.RoomName(kind)}";

        public static readonly Color FogColor = new Color(0.1f, 0.1f, 0.13f);
        public static readonly Color SealedColor = new Color(0.05f, 0.05f, 0.06f);

        private static readonly Color GateColor = new Color(0.85f, 0.85f, 0.85f);
        private static readonly Color ItemColor = new Color(1f, 0.82f, 0.25f);
        private static readonly Color TrialColor = new Color(0.45f, 0.9f, 1f);
        private static readonly Color PortalColor = new Color(1f, 0.4f, 0.8f);

        /// <summary>A lair portal once its room has been visited (4.13): dim, barred.</summary>
        private static readonly Color LairColor = new Color(0.66f, 0.6f, 0.74f);
        private static readonly Color StartColor = Color.white;

        /// <summary>A key-locked arch's padlock (4.9); an item lock's padlock takes the item's region colour.</summary>
        private static readonly Color LockColor = new Color(0.82f, 0.84f, 0.9f);
        private static readonly Color TownColor = new Color(1f, 0.62f, 0.3f);

        /// <summary>A mended face's town marker and fragment tag (4.8): gold.</summary>
        private static readonly Color MendedColor = new Color(1f, 0.9f, 0.3f);

        /// <summary>A face whose temple is open (4.14): its tag in pale lilac, "temple".</summary>
        private static readonly Color TempleTagColor = new Color(0.85f, 0.8f, 1f);
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
        private GUIStyle insideStyle;
        private GUIStyle tagStyle;

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

        /// <summary>True while the player is inside a town building (the map shows the town's screen with an inside badge).</summary>
        public bool PlayerInside => navigator != null && navigator.InInterior;

        /// <summary>The inside badge's text while the player is inside a town building (null outside).</summary>
        public string InsideLabel
        {
            get
            {
                if (!PlayerInside || world == null || world.Model == null) return null;
                if (navigator.InLair) return LairText.MapBadge;
                if (navigator.InTemple) return TempleText.MapBadge;
                return InsideText(TownInterior.KindFor(world.Model.ThemeOf(navigator.Face)));
            }
        }

        /// <summary>Where the player marker stands (world): the player, or inside a building the door they went in by.</summary>
        public Vector2 PlayerMarkerPoint => PlayerInside ? navigator.InteriorDoor : navigator != null ? (Vector2)navigator.transform.position : Vector2.zero;

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

        /// <summary>
        /// A face's fragment tag on the map (4.8): "found/count" of its fragments, or "mended"; null for a face with no
        /// visited screen yet (the fog) or without a run.
        /// </summary>
        public string FragmentTag(FaceId face)
        {
            if (world == null || world.Model == null || world.Dice == null || world.Model.IsSealed(face)) return null;
            if (tracker == null || tracker.Visits == null) return null;
            bool visited = false;
            for (int y = 0; y < world.FaceSize && !visited; y++)
                for (int x = 0; x < world.FaceSize && !visited; x++)
                    visited = tracker.Visits.IsVisited(new ScreenAddress(face, x, y));
            if (!visited) return null;
            RunRewards rewards = navigator != null ? navigator.GetComponent<RunRewards>() : null;
            return RewardText.FragmentCount(rewards != null ? rewards.FragmentsOf(face) : 0, world.Dice.Of(face),
                world.Repairs != null && world.Repairs.IsUnsealed(face), world.Repairs != null && world.Repairs.IsRepaired(face));
        }

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

        /// <summary>A face's fragment tag on the map (4.8), in pixels: wide enough for "mended" at the tag's font.</summary>
        public const float TagWidth = 54f, TagHeight = 16f;

        /// <summary>Where the net sits on a screen this size: its top-left corner and the screen-cell size (pixels).</summary>
        public static void NetFrame(float w, float h, int faceSize, out float left, out float top, out float cell)
        {
            cell = CellSize(w, h, faceSize);
            float faceSpan = cell * faceSize;
            float netW = CubeNet.Columns * faceSpan + (CubeNet.Columns - 1) * FaceGap;
            float netH = CubeNet.Rows * faceSpan + (CubeNet.Rows - 1) * FaceGap;
            left = Mathf.Floor((w - netW) / 2f);
            top = HeaderHeight + Mathf.Floor(Mathf.Max(0f, (h - HeaderHeight - LegendHeight(w) - netH) / 2f));
        }

        /// <summary>The pixel rect of a net slot (a face's 3x3 of screens, its frame excluded).</summary>
        public static Rect SlotRect(Vector2Int slot, int faceSize, float left, float top, float cell)
        {
            float span = cell * faceSize;
            return new Rect(left + slot.x * (span + FaceGap), top + (CubeNet.Rows - 1 - slot.y) * (span + FaceGap), span, span);
        }

        /// <summary>
        /// Where a face's fragment tag goes (4.8 review): never over a screen, always in an empty slot of the net touching the
        /// face, just past its frame. Tried in order: the slot above (the tag's bottom-left there), below (top-left), left
        /// (top-right), right (top-left), then the diagonals, below-right first (the corner touching the face). Town in the
        /// middle of the cross has all four sides taken, so its tag sits in the empty slot below-right of it.
        /// </summary>
        public static Rect FragmentTagRect(CubeNet net, FaceId face, float left, float top, float cell)
        {
            Vector2Int slot = net.PlacementOf(face).Slot;
            Rect f = SlotRect(slot, net.FaceSize, left, top, cell);
            const float pad = 3f; // past the face's 2-pixel frame
            bool Empty(int dx, int dy)
            {
                var s = new Vector2Int(slot.x + dx, slot.y + dy);
                return s.x >= 0 && s.y >= 0 && s.x < CubeNet.Columns && s.y < CubeNet.Rows && !net.TryGetFace(s, out _);
            }
            if (Empty(0, 1)) return new Rect(f.xMin, f.yMin - pad - TagHeight, TagWidth, TagHeight);
            if (Empty(0, -1)) return new Rect(f.xMin, f.yMax + pad, TagWidth, TagHeight);
            if (Empty(-1, 0)) return new Rect(f.xMin - pad - TagWidth, f.yMin, TagWidth, TagHeight);
            if (Empty(1, 0)) return new Rect(f.xMax + pad, f.yMin, TagWidth, TagHeight);
            if (Empty(1, -1)) return new Rect(f.xMax + pad, f.yMax + pad, TagWidth, TagHeight);
            if (Empty(-1, -1)) return new Rect(f.xMin - pad - TagWidth, f.yMax + pad, TagWidth, TagHeight);
            if (Empty(1, 1)) return new Rect(f.xMax + pad, f.yMin - pad - TagHeight, TagWidth, TagHeight);
            return new Rect(f.xMin - pad - TagWidth, f.yMin - pad - TagHeight, TagWidth, TagHeight);
        }

        /// <summary>The legend's narrowest column (pixels): a marker glyph and the longest label at the map's font size.</summary>
        public const float MinLegendStep = 130f;

        /// <summary>Height of one legend row (pixels).</summary>
        public const float LegendRowHeight = 24f;

        /// <summary>Labels per legend row on a screen this wide: as many as fit MinLegendStep apart, the rows evened out.</summary>
        public static int LegendColumns(float screenWidth)
        {
            int n = LegendLabels.Length;
            int fit = Mathf.Clamp(Mathf.FloorToInt((screenWidth - 32f) / MinLegendStep), 1, n);
            int rows = (n + fit - 1) / fit;
            return (n + rows - 1) / rows;
        }

        /// <summary>Legend rows on a screen this wide (two at 960 px, since 4.5 added "Town").</summary>
        public static int LegendRows(float screenWidth)
        {
            int columns = LegendColumns(screenWidth);
            return (LegendLabels.Length + columns - 1) / columns;
        }

        /// <summary>The space kept under the net for the legend: 52 px for one row, LegendRowHeight more per extra row.</summary>
        public static float LegendHeight(float screenWidth) => 52f + (LegendRows(screenWidth) - 1) * LegendRowHeight;

        /// <summary>The cell size (pixels) the net is drawn with on a screen of the given size: as big as fits, 8..64.</summary>
        public static float CellSize(float screenWidth, float screenHeight, int faceSize)
        {
            const float margin = 16f, header = 56f;
            float legend = LegendHeight(screenWidth);
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

            GUI.Label(TitleRect(w), Title, titleStyle);
            GUI.Label(CloseHintRect(w), CloseHint, textStyle);
            GUI.Label(QuestHintRect(w), QuestText.MapHint, textStyle);
            string inside = InsideLabel;
            if (inside != null)
            {
                // The inside badge, in the header line between the title and the close hint (it never covers the net):
                // a dark tab in the town marker's orange.
                Rect badge = InsideBadgeRect(w);
                Fill(badge, new Color(0.08f, 0.06f, 0.1f, 0.92f));
                Outline(badge, TownColor, MarkerOutlineWidth);
                GUI.color = TownColor;
                GUI.Label(badge, inside, insideStyle);
                GUI.color = Color.white;
            }

            CubeNet n = Net;
            int size = n.FaceSize;
            NetFrame(w, h, size, out float left, out float top, out float cell);
            float faceSpan = cell * size;
            float netH = CubeNet.Rows * faceSpan + (CubeNet.Rows - 1) * FaceGap;

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

            // Each visited face's fragments (4.8): a tag beside the face, in an empty slot of the net (FragmentTagRect, never
            // over a screen), "2/4", or "mended" in gold.
            foreach (NetPlacement p in n.Placements)
            {
                string tag = FragmentTag(p.Face);
                if (tag == null) continue;
                Rect tagRect = FragmentTagRect(n, p.Face, left, top, cell);
                Fill(tagRect, new Color(0.05f, 0.04f, 0.07f, 0.6f));
                GUI.color = world.Repairs != null && world.Repairs.IsRepaired(p.Face) ? MendedColor
                    : world.Repairs != null && world.Repairs.IsUnsealed(p.Face) ? TempleTagColor : Color.white;
                GUI.Label(tagRect, tag, tagStyle);
                GUI.color = Color.white;
            }

            float glyph = Mathf.Max(6f, Mathf.Round(cell * 0.2f));
            foreach (MapMarker m in Markers)
            {
                Rect r = CellRect(n.NetCell(m.Screen));
                Vector2 local = n.ToNetLocal(m.Screen.Face, m.Local);
                var at = new Vector2(r.x + local.x * r.width, r.y + (1f - local.y) * r.height);
                DrawMarker(m.Kind, m.Done, at, glyph, m.Detail);
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
                Vector2 local = n.ToNetLocal(here.Face, MapMarkers.LocalIn(world.ScreenCenter(here), PlayerMarkerPoint));
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
            yield return new KeyValuePair<string, Color>("Lair", LairColor);
            yield return new KeyValuePair<string, Color>("Core portal", MendedColor);
            yield return new KeyValuePair<string, Color>("Town", TownColor);
            yield return new KeyValuePair<string, Color>("Mended town", MendedColor);
            yield return new KeyValuePair<string, Color>("Locked arch", LockColor);
        }

        private static Rect Grow(Rect r, float by) => new Rect(r.x - by, r.y - by, r.width + 2f * by, r.height + 2f * by);

        private void DrawMarker(MapMarkerKind kind, bool done, Vector2 at, float s, int detail = 0)
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
                    if (detail == MapMarkers.PortalLair)
                    {
                        // A lair portal (4.13): a dim ring round a dim disc, barred.
                        Tex(Ring, new Rect(at.x - s * 0.75f, at.y - s * 0.75f, s * 1.5f, s * 1.5f), LairColor);
                        Tex(Disc, new Rect(at.x - s * 0.45f, at.y - s * 0.45f, s * 0.9f, s * 0.9f), LairColor);
                        float stripe = Mathf.Max(1f, Mathf.Round(s * 0.12f));
                        foreach (float dx in new[] { -0.25f, 0f, 0.25f })
                            Fill(new Rect(at.x + dx * s - stripe / 2f, at.y - s * 0.45f, stripe, s * 0.9f), dark);
                    }
                    else if (detail == MapMarkers.PortalReal)
                    {
                        // The real portal, once known (4.13): a gold ring round the pink core.
                        Tex(Ring, new Rect(at.x - s * 0.75f, at.y - s * 0.75f, s * 1.5f, s * 1.5f), MendedColor);
                        Tex(Disc, new Rect(at.x - s * 0.4f, at.y - s * 0.4f, s * 0.8f, s * 0.8f), PortalColor);
                        Tex(Disc, new Rect(at.x - s * 0.15f, at.y - s * 0.15f, s * 0.3f, s * 0.3f), MendedColor);
                    }
                    else
                    {
                        Tex(Ring, new Rect(at.x - s * 0.75f, at.y - s * 0.75f, s * 1.5f, s * 1.5f), PortalColor);
                        Tex(Disc, new Rect(at.x - s * 0.3f, at.y - s * 0.3f, s * 0.6f, s * 0.6f), PortalColor);
                    }
                    break;
                case MapMarkerKind.Lock:
                {
                    // A padlock (4.9): a pale body under a shackle, on a dark backing; a dark keyhole for a key lock, the
                    // item's region colour in its middle for an item lock.
                    var body = new Rect(at.x - s * 0.5f, at.y - s * 0.15f, s, s * 0.7f);
                    var shackle = new Rect(at.x - s * 0.3f, at.y - s * 0.55f, s * 0.6f, s * 0.45f);
                    Fill(Grow(body, o), dark);
                    Outline(Grow(shackle, o), dark, s * 0.18f + 2f * o);
                    Outline(shackle, LockColor, s * 0.18f);
                    Fill(body, LockColor);
                    if (detail > 0)
                        Fill(new Rect(at.x - s * 0.3f, at.y - s * 0.02f, s * 0.6f, s * 0.44f), CubeWorld.ThemeColor((Theme)(detail - 1)));
                    else
                        Fill(new Rect(at.x - s * 0.08f, at.y + s * 0.02f, s * 0.16f, s * 0.36f), dark);
                    break;
                }
                case MapMarkerKind.Town:
                    // A little house: a square under a stepped roof, on a dark backing; gold with a pennant once mended (4.8).
                    Color house = done ? MendedColor : TownColor;
                    var wall = new Rect(at.x - s * 0.55f, at.y - s * 0.15f, s * 1.1f, s * 0.7f);
                    var roof = new Rect(at.x - s * 0.75f, at.y - s * 0.45f, s * 1.5f, s * 0.3f);
                    var peak = new Rect(at.x - s * 0.35f, at.y - s * 0.7f, s * 0.7f, s * 0.25f);
                    Fill(Grow(wall, o), dark);
                    Fill(Grow(roof, o), dark);
                    Fill(Grow(peak, o), dark);
                    Fill(wall, house);
                    Fill(roof, house);
                    Fill(peak, house);
                    Fill(new Rect(at.x - s * 0.12f, at.y + s * 0.2f, s * 0.24f, s * 0.35f), dark);
                    if (done)
                    {
                        // A pennant on a pole over the peak.
                        var pole = new Rect(at.x - s * 0.05f, at.y - s * 1.15f, s * 0.12f, s * 0.5f);
                        var flag = new Rect(at.x + s * 0.07f, at.y - s * 1.15f, s * 0.45f, s * 0.25f);
                        Fill(Grow(pole, o), dark);
                        Fill(Grow(flag, o), dark);
                        Fill(pole, house);
                        Fill(flag, house);
                    }
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
            int columns = LegendColumns(w), rows = LegendRows(w);
            float step = (w - 32f) / columns;
            for (int i = 0; i < LegendLabels.Length; i++)
            {
                float x = 16f + (i % columns) * step;
                float y = h - 40f - (rows - 1 - i / columns) * LegendRowHeight;
                var at = new Vector2(x + s, y + 11f);
                switch (i)
                {
                    case 0: DrawPlayer(at, Vector2.right, s * 1.4f); break;
                    case 1: DrawMarker(MapMarkerKind.Start, false, at, s); break;
                    case 2: DrawMarker(MapMarkerKind.Town, false, at, s); break;
                    case 3: DrawMarker(MapMarkerKind.Town, true, at, s); break;
                    case 4: DrawMarker(MapMarkerKind.Gate, false, at, s); break;
                    case 5: DrawMarker(MapMarkerKind.Gate, true, at, s); break;
                    case 6: DrawMarker(MapMarkerKind.Item, false, at, s); break;
                    case 7: DrawMarker(MapMarkerKind.Trial, false, at, s); break;
                    case 8: DrawMarker(MapMarkerKind.Trial, true, at, s); break;
                    case 9: DrawMarker(MapMarkerKind.Portal, false, at, s); break;
                    case 10: DrawMarker(MapMarkerKind.Portal, false, at, s, MapMarkers.PortalLair); break;
                    case 11: DrawMarker(MapMarkerKind.Portal, true, at, s, MapMarkers.PortalReal); break;
                    default: DrawMarker(MapMarkerKind.Lock, false, at, s); break;
                }
                GUI.Label(new Rect(x + s * 2f + 6f, y, step - s * 2f - 6f, 24f), LegendLabels[i], textStyle);
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
            insideStyle = new GUIStyle(GUI.skin.label) { fontSize = InsideFontSize, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            tagStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0) };
            tagStyle.normal.textColor = Color.white;
            insideStyle.normal.textColor = Color.white;
        }
    }
}
