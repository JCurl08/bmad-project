using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The core fight's HUD (OnGUI placeholder): while the player is in the arena, an "Order ↔ Entropy" meter (filled by the
    /// current phase's progress, with a mark at its goal) and the phase's name, at the bottom of the right side column (4.25:
    /// SideHud, never over the play area); once the fight ends, a campy victory or defeat banner above the play area's centre
    /// (the combat HUD's death message sits at the centre).
    /// </summary>
    public class CoreHud : MonoBehaviour
    {
        public const string MeterLabel = "Order ↔ Entropy";
        public const string VictoryMessage = "Entropy wins! The Partition cracks… everybody mingle!";
        public const string DefeatMessage = "Order prevails… " + InWorldText.DemonFull + " tidies you away. (For now.)";

        /// <summary>The exit door's hint, shown while the player can walk out of the arena.</summary>
        public const string ExitHint = "Walk into the doorway in the left wall to leave";

        /// <summary>Every line this HUD can show (for the text checks), the phase names included.</summary>
        public static System.Collections.Generic.IEnumerable<string> AllText()
        {
            yield return MeterLabel;
            yield return VictoryMessage;
            yield return DefeatMessage;
            yield return ExitHint;
            yield return new MixingPhase().Name;
            yield return new FallbackBoss().Name;
            foreach (string line in BossBenefits.AllText()) yield return line;
        }

        /// <summary>The help line over the meter (4.8: the fight's benefits from mended faces), or null with none.</summary>
        public string BenefitsText => arena != null ? BossBenefits.HudLine(arena.ActiveBenefits) : null;

        [SerializeField] private CoreArena arena;

        private GUIStyle labelStyle;
        private GUIStyle bannerStyle;
        private GUIStyle hintStyle;

        /// <summary>True while the exit hint shows: the player is in a fight they can still walk out of.</summary>
        public bool ExitHintVisible => arena != null && arena.PlayerInArena && arena.FightActive && arena.ExitAvailable;

        public CoreArena Arena
        {
            get => arena;
            set => arena = value;
        }

        /// <summary>True while the meter is drawn: the player is in the arena during or after a fight.</summary>
        public bool MeterVisible =>
            arena != null && arena.PlayerInArena && (arena.FightActive || arena.Outcome != CoreOutcome.None);

        /// <summary>The meter fill as drawn (the current phase's progress; full after a victory).</summary>
        public float MeterValue
        {
            get
            {
                if (arena == null) return 0f;
                if (arena.Outcome == CoreOutcome.Victory) return 1f;
                BossPhase phase = arena.CurrentPhase;
                return phase != null ? Mathf.Clamp01(phase.Progress) : arena.Meter;
            }
        }

        /// <summary>The banner text once the fight has ended, otherwise null.</summary>
        public string MessageText
        {
            get
            {
                if (arena == null) return null;
                switch (arena.Outcome)
                {
                    case CoreOutcome.Victory: return VictoryMessage;
                    case CoreOutcome.Defeat: return DefeatMessage;
                    default: return null;
                }
            }
        }

        private void OnGUI()
        {
            if (arena == null) return;
            Vector2 view = HudScale.Begin(); // 4.21: the shared scale
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = LabelSize, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Overflow };
                labelStyle.normal.textColor = Color.white;
                bannerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true,
                };
                bannerStyle.normal.textColor = new Color(1f, 0.6f, 0.85f);
                hintStyle = new GUIStyle(GUI.skin.label) { fontSize = HintSize, fontStyle = FontStyle.Italic, wordWrap = false, clipping = TextClipping.Overflow };
                hintStyle.normal.textColor = Color.white; // tinted by GUI.color where drawn
            }
            SideHud.Frame frame = SideHud.Layout(view.x, view.y);
            if (MeterVisible) DrawMeter(frame.Right);

            string message = MessageText;
            if (message == null || !arena.PlayerInArena) return;
            // Above the combat HUD's centred death panel (centre ± 60), on the play area.
            float width = Mathf.Min(640f, frame.Play.width);
            var panel = new Rect(frame.Play.center.x - width / 2f, frame.Play.center.y - 60f - 8f - 80f, width, 80f);
            SideHud.Fill(panel, new Color(0f, 0f, 0f, 0.75f));
            GUI.Label(panel, message, bannerStyle);
        }

        /// <summary>Font sizes of the meter's lines (GUI units).</summary>
        public const int LabelSize = 13, HintSize = 12;

        /// <summary>The meter bar's height.</summary>
        public const float BarHeight = 14f;

        /// <summary>Where the meter's parts go in the right column: title, bar, help, exit hint (help and hint may be empty).</summary>
        public struct MeterRects
        {
            public Rect Title, Bar, Help, Hint;
        }

        /// <summary>
        /// 4.25: the meter's layout at the bottom of the right side column (SideHud): its title and phase (wrapped), the bar, the
        /// help from the gems (wrapped) and the exit hint (wrapped), bottom up from the column's inner bottom.
        /// </summary>
        public static MeterRects MeterLayout(Rect column, string title, string help, string hint)
        {
            Rect inner = SideHud.Inner(column);
            var m = new MeterRects();
            float y = inner.yMax;
            if (hint != null)
            {
                float h = SideHud.TextHeight(hint, HintSize, inner.width);
                m.Hint = new Rect(inner.x, y - h, inner.width, h);
                y = m.Hint.yMin - 6f;
            }
            if (help != null)
            {
                float h = SideHud.TextHeight(help, HintSize, inner.width);
                m.Help = new Rect(inner.x, y - h, inner.width, h);
                y = m.Help.yMin - 6f;
            }
            m.Bar = new Rect(inner.x, y - BarHeight, inner.width, BarHeight);
            y = m.Bar.yMin - 4f;
            float th = SideHud.TextHeight(title, LabelSize, inner.width);
            m.Title = new Rect(inner.x, y - th, inner.width, th);
            return m;
        }

        /// <summary>The meter's title: its label and the phase's name.</summary>
        public string MeterTitle
        {
            get
            {
                BossPhase phase = arena != null ? arena.CurrentPhase : null;
                return phase != null ? $"{MeterLabel} ({phase.Name})" : MeterLabel;
            }
        }

        private void DrawMeter(Rect column)
        {
            string help = BenefitsText;
            string hint = ExitHintVisible ? ExitHint : null;
            MeterRects m = MeterLayout(column, MeterTitle, help, hint);
            SideHud.Label(m.Title, MeterTitle, LabelSize, labelStyle);
            GUI.color = new Color(0.55f, 0.6f, 0.9f); // order: cool, still, aligned
            GUI.DrawTexture(m.Bar, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.45f, 0.75f); // entropy: warm, lively
            GUI.DrawTexture(new Rect(m.Bar.x, m.Bar.y, m.Bar.width * MeterValue, m.Bar.height), Texture2D.whiteTexture);
            BossPhase phase = arena.CurrentPhase;
            float goal = phase != null ? Mathf.Clamp01(phase.Goal) : 1f;
            if (goal < 1f)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(m.Bar.x + m.Bar.width * goal - 1f, m.Bar.y - 4f, 3f, m.Bar.height + 8f), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
            if (help != null)
            {
                // Under the bar, in gold: what the mended faces do for this fight.
                GUI.color = new Color(1f, 0.88f, 0.45f);
                SideHud.Label(m.Help, help, HintSize, hintStyle);
                GUI.color = Color.white;
            }
            if (hint != null)
            {
                GUI.color = new Color(0.85f, 0.85f, 0.95f);
                SideHud.Label(m.Hint, hint, HintSize, hintStyle);
                GUI.color = Color.white;
            }
        }
    }
}
