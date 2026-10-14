using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// 4.25 (playtest 7: "the screen has two black bars on both sides. Can we turn this into our menu? Right now it just hovers
    /// over the play area"): the HUD lives in two side columns beside the play area, never over it. Laid out in HudScale's
    /// virtual screen (at least 960x600 GUI units): the play area keeps the screen's 1.6 aspect, as tall as fits; the side bars
    /// either side are the columns. Where the window's own bars are narrower than PanelWidth (every window up to about 2.1:1,
    /// the 960x600 canvas, 16:9 and 4:3 included) the play area shrinks until both columns are PanelWidth wide, centred, with
    /// black bands above and below. The left column holds the player's status (CombatHud: hearts, the equipped item, keys and
    /// boosts, the fragment dice, the stone's and the mitt's lines), the right one the quest summary and the menu keys
    /// (SideHudView) and, in the core, the Demon's meter (CoreHud). The camera draws the play area only (ScreenCamera.Viewport,
    /// set by CubeWorld). Pure layout here, so the layout tests read the same rects the HUDs draw.
    /// </summary>
    public static class SideHud
    {
        /// <summary>The least width of a side column (GUI units).</summary>
        public const float PanelWidth = 125f;

        /// <summary>A column's padding, and its insets top and bottom (clear of the F1 debug bars, 4.19).</summary>
        public const float Pad = 8f, TopInset = 26f, BottomInset = 26f;

        /// <summary>The play area's aspect: the screen's (24 x 15).</summary>
        public static float Aspect => ScreenMath.DefaultScreenSize.x / ScreenMath.DefaultScreenSize.y;

        /// <summary>The play area and the two side columns on a screen (GUI units, y down).</summary>
        public readonly struct Frame
        {
            public readonly Rect Play, Left, Right;

            public Frame(Rect play, Rect left, Rect right)
            {
                Play = play;
                Left = left;
                Right = right;
            }

            public override string ToString() => $"play {Play}, left {Left}, right {Right}";
        }

        /// <summary>
        /// The frame of a virtual screen w x h: the play area at the screen's aspect, centred, as big as leaves at least
        /// PanelWidth either side; the columns are everything left and right of it, full height.
        /// </summary>
        public static Frame Layout(float w, float h)
        {
            float naturalBar = (w - h * Aspect) / 2f;
            float side = Mathf.Max(naturalBar, PanelWidth);
            float playW = Mathf.Max(0f, w - 2f * side);
            float playH = Mathf.Min(h, playW / Aspect);
            playW = playH * Aspect;
            var play = new Rect((w - playW) / 2f, (h - playH) / 2f, playW, playH);
            return new Frame(play, Rect.MinMaxRect(0f, 0f, play.xMin, h), Rect.MinMaxRect(play.xMax, 0f, w, h));
        }

        /// <summary>The current screen's frame (GUI units).</summary>
        public static Frame Current
        {
            get
            {
                Vector2 v = HudScale.VirtualSize(Screen.width, Screen.height);
                return Layout(v.x, v.y);
            }
        }

        /// <summary>The play area as the camera's normalized viewport (y up) on a real screen of these pixels.</summary>
        public static Rect CameraRect(float pixelWidth, float pixelHeight)
        {
            Vector2 v = HudScale.VirtualSize(pixelWidth, pixelHeight);
            Frame f = Layout(v.x, v.y);
            return new Rect(f.Play.x / v.x, 1f - f.Play.yMax / v.y, f.Play.width / v.x, f.Play.height / v.y);
        }

        /// <summary>A column's content area: inside its padding and insets.</summary>
        public static Rect Inner(Rect column) =>
            Rect.MinMaxRect(column.xMin + Pad, column.yMin + TopInset, column.xMax - Pad, column.yMax - BottomInset);

        /// <summary>A safe width of a character at a font size (GUI units): 0.58 em (the built-in font averages about 0.5).</summary>
        public static float CharWidth(int fontSize) => fontSize * 0.58f;

        /// <summary>A line's height at a font size.</summary>
        public static float LineHeight(int fontSize) => Mathf.Ceil(fontSize * 1.3f);

        /// <summary>The lines a text wraps to in a width (words kept whole where they fit; a long word is cut), at a safe width.</summary>
        public static List<string> Wrap(string text, int fontSize, float width)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;
            float cw = CharWidth(fontSize);
            int max = Mathf.Max(1, Mathf.FloorToInt(width / cw));
            foreach (string para in text.Split('\n'))
            {
                string line = "";
                foreach (string word in para.Split(' '))
                {
                    string w = word;
                    while (w.Length > max)
                    {
                        if (line.Length > 0) lines.Add(line);
                        lines.Add(w.Substring(0, max));
                        w = w.Substring(max);
                        line = "";
                    }
                    string next = line.Length == 0 ? w : line + " " + w;
                    if (next.Length > max)
                    {
                        lines.Add(line);
                        line = w;
                    }
                    else line = next;
                }
                lines.Add(line);
            }
            return lines;
        }

        /// <summary>The height a wrapped text needs in a width (Wrap's lines).</summary>
        public static float TextHeight(string text, int fontSize, float width) => Wrap(text, fontSize, width).Count * LineHeight(fontSize);

        /// <summary>
        /// Draws a wrapped text in its own lines (as Wrap breaks it), so what is drawn is what the layout measured; only the lines
        /// that fit the rect's height (4.25 review), the last one ending "..." when some are left out.
        /// </summary>
        public static void Label(Rect rect, string text, int fontSize, GUIStyle style)
        {
            float lh = LineHeight(fontSize);
            List<string> lines = Wrap(text, fontSize, rect.width);
            int fit = Mathf.Max(1, Mathf.FloorToInt((rect.height + 0.5f) / lh));
            float y = rect.y;
            for (int i = 0; i < lines.Count && i < fit; i++)
            {
                string line = lines[i];
                if (i == fit - 1 && lines.Count > fit) line = line.Length > 3 ? line.Substring(0, line.Length - 3) + "..." : "...";
                GUI.Label(new Rect(rect.x, y, rect.width + 40f, lh + 4f), line, style);
                y += lh;
            }
        }

        /// <summary>A filled rect in a colour (GUI).</summary>
        public static void Fill(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }

    /// <summary>
    /// 4.25: the side columns' backgrounds and the right column's quest summary and menu keys (SideHud). Added by CubeWorld; it
    /// draws behind the other HUDs (GUI.depth 10): black bands above and below the play area, a dark panel each side, then the
    /// first open quest (its title and step, from the run's QuestLog) and the keys for the map and the quest log.
    /// </summary>
    public class SideHudView : MonoBehaviour
    {
        public static readonly Color PanelColor = new Color(0.07f, 0.07f, 0.1f, 1f);
        public static readonly Color EdgeColor = new Color(0.3f, 0.3f, 0.38f, 1f);
        public static readonly Color TitleColor = new Color(1f, 0.85f, 0.4f);

        public const int HeaderSize = 14, TitleSize = 13, StepSize = 12, KeySize = 12;

        /// <summary>The quest summary's header.</summary>
        public const string QuestHeader = "Quest";

        /// <summary>The menu keys (filled with the real keys or buttons, InputPrompts).</summary>
        public static readonly string[] KeyLines = { "[M] map", "[Q] quest log" };

        private CubeWorld world;
        private GUIStyle header, title, step, keys;

        public CubeWorld World
        {
            get => world;
            set => world = value;
        }

        /// <summary>The quest the summary shows (the first open one of the log, in its order), or null.</summary>
        public QuestEntry Summary
        {
            get
            {
                QuestLog log = world != null ? world.Quests : null;
                if (log == null) return null;
                foreach (QuestEntry e in log.Ordered())
                    if (!e.Done) return e;
                return null;
            }
        }

        /// <summary>
        /// The summary's rects in a right column: the header, the title, the step, then the keys. 4.25 review: everything stays
        /// above bottom (the core meter's top while it shows): the step gives up whole lines (drawn with "...") so it fits.
        /// </summary>
        public static List<Rect> InfoLayout(Rect column, string questTitle, string questStep, IReadOnlyList<string> keyLines,
            float bottom = float.MaxValue)
        {
            Rect inner = SideHud.Inner(column);
            bottom = Mathf.Min(bottom, inner.yMax);
            float header = SideHud.LineHeight(HeaderSize);
            float title = SideHud.TextHeight(questTitle ?? QuestText.Empty, TitleSize, inner.width);
            float keys = 8f;
            foreach (string k in keyLines) keys += SideHud.TextHeight(k, KeySize, inner.width) + 2f;
            float step = questStep != null ? SideHud.TextHeight(questStep, StepSize, inner.width) : 0f;
            if (questStep != null)
            {
                float room = bottom - inner.y - header - title - keys - 6f;
                float lh = SideHud.LineHeight(StepSize);
                step = Mathf.Clamp(Mathf.Floor(room / lh) * lh, lh, step);
            }
            var rects = new List<Rect>();
            float y = inner.y;
            void Add(float height)
            {
                rects.Add(new Rect(inner.x, y, inner.width, height));
                y += height + 2f;
            }
            Add(header);
            Add(title);
            if (questStep != null) Add(step);
            y += 8f;
            foreach (string k in keyLines) Add(SideHud.TextHeight(k, KeySize, inner.width));
            return rects;
        }

        private CoreHud coreHud;
        private bool coreHudLooked;

        /// <summary>The top of the core meter in the right column while it shows (the summary stays above it), else the column's bottom.</summary>
        private float InfoBottom(Rect column)
        {
            if (!coreHudLooked)
            {
                coreHud = FindAnyObjectByType<CoreHud>();
                coreHudLooked = true;
            }
            if (coreHud == null || !coreHud.MeterVisible) return float.MaxValue;
            CoreHud.MeterRects m = CoreHud.MeterLayout(column, coreHud.MeterTitle, coreHud.BenefitsText,
                coreHud.ExitHintVisible ? CoreHud.ExitHint : null);
            return m.Title.yMin - 8f;
        }

        private void OnGUI()
        {
            if (world == null) return;
            GUI.depth = 10; // behind every other HUD
            Vector2 view = HudScale.Begin();
            SideHud.Frame f = SideHud.Layout(view.x, view.y);
            // Black above and below the play area; a dark panel either side, edged where it meets the play area.
            SideHud.Fill(Rect.MinMaxRect(f.Play.xMin, 0f, f.Play.xMax, f.Play.yMin), Color.black);
            SideHud.Fill(Rect.MinMaxRect(f.Play.xMin, f.Play.yMax, f.Play.xMax, view.y), Color.black);
            SideHud.Fill(f.Left, PanelColor);
            SideHud.Fill(f.Right, PanelColor);
            SideHud.Fill(new Rect(f.Left.xMax - 2f, 0f, 2f, view.y), EdgeColor);
            SideHud.Fill(new Rect(f.Right.xMin, 0f, 2f, view.y), EdgeColor);
            if (world.Model == null) return;
            MakeStyles();
            QuestEntry quest = Summary;
            string questTitle = quest != null ? quest.Title : QuestText.Empty;
            string questStep = quest != null ? InputPrompts.Fill(QuestLogView.StepLine(quest)) : null;
            var keyLines = new List<string>();
            foreach (string k in KeyLines) keyLines.Add(InputPrompts.Fill(k));
            List<Rect> rects = InfoLayout(f.Right, questTitle, questStep, keyLines, InfoBottom(f.Right));
            int r = 0;
            SideHud.Label(rects[r++], QuestHeader, HeaderSize, header);
            SideHud.Label(rects[r++], questTitle, TitleSize, title);
            if (questStep != null) SideHud.Label(rects[r++], questStep, StepSize, step);
            foreach (string k in keyLines) SideHud.Label(rects[r++], k, KeySize, keys);
        }

        private void MakeStyles()
        {
            if (header != null) return;
            header = new GUIStyle(GUI.skin.label) { fontSize = HeaderSize, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Overflow };
            header.normal.textColor = Color.white;
            title = new GUIStyle(header) { fontSize = TitleSize };
            title.normal.textColor = TitleColor;
            step = new GUIStyle(header) { fontSize = StepSize, fontStyle = FontStyle.Normal };
            step.normal.textColor = Color.white;
            keys = new GUIStyle(step) { fontSize = KeySize };
            keys.normal.textColor = new Color(0.8f, 0.85f, 1f);
        }
    }
}
