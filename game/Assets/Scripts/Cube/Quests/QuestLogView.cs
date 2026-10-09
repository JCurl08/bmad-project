using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Cube
{
    /// <summary>
    /// The quest log (4.18): Q opens it (also from the map, whose header shows "[Q] quest log"), Q again or Escape closes
    /// it. While open it pauses the game like the map (GamePause.Hold). It lists every quest of the run's log
    /// (CubeWorld.Quests): open ones first, then done ones (dimmed, marked Done), each with its title, who gave it, its
    /// current step and progress (a leader's fragments are read live from the run on opening). Drawn with OnGUI in up to two
    /// columns (Layout), readable on the 960x600 canvas. It cannot open while the between-runs screen is up, and gives way
    /// to the map (opening the map closes it).
    /// </summary>
    [DefaultExecutionOrder(1001)] // after the map's LateUpdate (CubeMap is 1000), so Q on an open map swaps to the log
    public class QuestLogView : MonoBehaviour
    {
        /// <summary>The GamePause menu key the log holds while open.</summary>
        public const string PauseKey = "quests";

        public const Key OpenKey = Key.Q;

        /// <summary>The log's header band: entries start below it.</summary>
        public const float HeaderHeight = 60f;

        /// <summary>One entry's height: a title line and up to two lines of step.</summary>
        public const float EntryHeight = 66f;

        public const float Margin = 24f;
        public const float ColumnGap = 24f;
        public const int TitleFontSize = 17;
        public const int StepFontSize = 15;

        [SerializeField] private CubeWorld world;
        [SerializeField] private CubeNavigator navigator;
        [SerializeField] private CubeMap map;
        [SerializeField] private RunLoop loop;

        private GUIStyle headerStyle, hintStyle, titleStyle, stepStyle, doneStyle;

        public CubeWorld World
        {
            get => world != null ? world : world = FindAnyObjectByType<CubeWorld>();
            set => world = value;
        }

        public CubeNavigator Navigator
        {
            get => navigator != null ? navigator : navigator = FindAnyObjectByType<CubeNavigator>();
            set => navigator = value;
        }

        public CubeMap Map
        {
            get => map != null ? map : map = FindAnyObjectByType<CubeMap>();
            set => map = value;
        }

        public RunLoop Loop
        {
            get => loop != null ? loop : loop = FindAnyObjectByType<RunLoop>();
            set => loop = value;
        }

        public bool IsOpen { get; private set; }

        /// <summary>False with no run yet or while the between-runs screen is up.</summary>
        public bool CanOpen => World != null && World.Model != null && !(Loop != null && Loop.ScreenOpen);

        /// <summary>The run's log (fresh each run).</summary>
        public QuestLog Log => World != null ? World.Quests : null;

        /// <summary>Brings the leaders' live progress up to date (the player's fragments, the run's repairs).</summary>
        public void Refresh()
        {
            QuestLog log = Log;
            if (log == null) return;
            RunRewards rewards = Navigator != null ? Navigator.GetComponent<RunRewards>() : null;
            log.RefreshLeaders(World, rewards);
        }

        /// <summary>Opens the log (closing the map) and pauses the game. False when it cannot open or is open.</summary>
        public bool Open()
        {
            if (IsOpen || !CanOpen) return false;
            if (Map != null && Map.IsOpen) Map.Close();
            Refresh();
            IsOpen = true;
            GamePause.Hold(PauseKey);
            return true;
        }

        public bool Close()
        {
            if (!IsOpen) return false;
            IsOpen = false;
            GamePause.Release(PauseKey);
            return true;
        }

        public bool Toggle() => IsOpen ? Close() : Open();

        private void OnDisable() => Close();

        private void LateUpdate()
        {
            if (IsOpen && (!CanOpen || (Map != null && Map.IsOpen)))
            {
                Close(); // the run ended, or the map took over
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            // A conversation keeps Q to itself (4.18 review): the log opens only once the talk is over.
            if (keyboard[OpenKey].wasPressedThisFrame && !GamePause.Talking)
            {
                Toggle();
                return;
            }
            if (IsOpen && keyboard.escapeKey.wasPressedThisFrame) Close();
        }

        /// <summary>
        /// Where each of count entries sits on a w x h screen: one column, or two once one column is full; entries past what
        /// fits are left out (MaxShown). Pure, so tests check it at 960x600.
        /// </summary>
        public static List<Rect> Layout(float w, float h, int count)
        {
            var rects = new List<Rect>();
            int perColumn = PerColumn(h);
            int columns = count > perColumn ? 2 : 1;
            float columnWidth = (w - 2f * Margin - (columns - 1) * ColumnGap) / columns;
            for (int i = 0; i < count && i < perColumn * 2; i++)
            {
                int column = i / perColumn, row = i % perColumn;
                rects.Add(new Rect(Margin + column * (columnWidth + ColumnGap), HeaderHeight + row * EntryHeight, columnWidth, EntryHeight - 6f));
            }
            return rects;
        }

        /// <summary>How many entries fit in one column of a screen h tall.</summary>
        public static int PerColumn(float h) => Mathf.Max(1, Mathf.FloorToInt((h - HeaderHeight - Margin) / EntryHeight));

        /// <summary>The most entries a w x h screen shows.</summary>
        public static int MaxShown(float h) => PerColumn(h) * 2;

        /// <summary>An entry's title line: the title, then who gave it.</summary>
        public static string TitleLine(QuestEntry e) => $"{(e.Done ? QuestText.DoneLabel + ": " : "")}{e.Title}  ({e.Giver})";

        /// <summary>An entry's step line, with its progress.</summary>
        public static string StepLine(QuestEntry e) => e.Progress != null ? $"{e.Step} ({e.Progress})" : e.Step;

        private void OnGUI()
        {
            if (!IsOpen) return;
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
                headerStyle.normal.textColor = Color.white;
                hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.UpperRight };
                hintStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = TitleFontSize, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Clip };
                titleStyle.normal.textColor = new Color(1f, 0.85f, 0.4f);
                stepStyle = new GUIStyle(GUI.skin.label) { fontSize = StepFontSize, wordWrap = true };
                stepStyle.normal.textColor = Color.white;
                doneStyle = new GUIStyle(titleStyle);
                doneStyle.normal.textColor = new Color(0.55f, 0.85f, 0.55f);
            }
            float w = Screen.width, h = Screen.height;
            GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);
            GUI.DrawTexture(new Rect(0f, 0f, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(16f, 10f, 300f, 36f), QuestText.LogTitle, headerStyle);
            GUI.Label(new Rect(w - 236f, 16f, 220f, 24f), QuestText.CloseHint, hintStyle);

            QuestLog log = Log;
            List<QuestEntry> entries = log != null ? log.Ordered() : new List<QuestEntry>();
            if (entries.Count == 0)
            {
                GUI.Label(new Rect(Margin, HeaderHeight, w - 2f * Margin, 60f), QuestText.Empty, stepStyle);
                return;
            }
            List<Rect> rects = Layout(w, h, entries.Count);
            for (int i = 0; i < rects.Count; i++)
            {
                QuestEntry e = entries[i];
                Rect r = rects[i];
                GUI.color = e.Done ? new Color(1f, 1f, 1f, 0.06f) : new Color(1f, 1f, 1f, 0.1f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = e.Done ? new Color(1f, 1f, 1f, 0.7f) : Color.white;
                GUI.Label(new Rect(r.x + 8f, r.y + 2f, r.width - 16f, 24f), TitleLine(e), e.Done ? doneStyle : titleStyle);
                GUI.Label(new Rect(r.x + 8f, r.y + 24f, r.width - 16f, r.height - 24f), StepLine(e), stepStyle);
            }
            GUI.color = Color.white;
        }
    }
}
