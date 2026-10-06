using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The core fight's HUD (OnGUI placeholder): while the player is in the arena, an "Order ↔ Entropy" meter at the bottom
    /// right (filled by the current phase's progress, with a mark at its goal) and the phase's name; once the fight ends, a
    /// campy victory or defeat banner above the centre (the combat HUD's death message sits at the centre).
    /// </summary>
    public class CoreHud : MonoBehaviour
    {
        public const string MeterLabel = "Order ↔ Entropy";
        public const string VictoryMessage = "Entropy wins! The Partition cracks… everybody mingle!";
        public const string DefeatMessage = "Order prevails… the Demon tidies you away. (For now.)";

        [SerializeField] private CoreArena arena;

        private GUIStyle labelStyle;
        private GUIStyle bannerStyle;

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
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
                labelStyle.normal.textColor = Color.white;
                bannerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true,
                };
                bannerStyle.normal.textColor = new Color(1f, 0.6f, 0.85f);
            }

            if (MeterVisible) DrawMeter();

            string message = MessageText;
            if (message == null || !arena.PlayerInArena) return;
            // Above the combat HUD's centred death panel (centre ± 60 px).
            var panel = new Rect(Screen.width / 2f - 320f, Screen.height / 2f - 60f - 8f - 80f, 640f, 80f);
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(panel, message, bannerStyle);
        }

        private void DrawMeter()
        {
            // Bottom right, above the dialogue band (bottom 150 px); the hearts sit bottom left.
            const float width = 340f, barHeight = 18f, margin = 12f, dialogueBand = 150f;
            float x = Screen.width - width - margin;
            float y = Screen.height - dialogueBand - margin - 66f;
            var back = new Rect(x - 8f, y - 6f, width + 16f, 72f);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(back, Texture2D.whiteTexture);
            GUI.color = Color.white;
            BossPhase phase = arena.CurrentPhase;
            string title = phase != null ? $"{MeterLabel}   ({phase.Name})" : MeterLabel;
            GUI.Label(new Rect(x, y, width, 24f), title, labelStyle);

            var bar = new Rect(x, y + 28f, width, barHeight);
            GUI.color = new Color(0.55f, 0.6f, 0.9f); // order: cool, still, aligned
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.45f, 0.75f); // entropy: warm, lively
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * MeterValue, bar.height), Texture2D.whiteTexture);
            float goal = phase != null ? Mathf.Clamp01(phase.Goal) : 1f;
            if (goal < 1f)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(bar.x + bar.width * goal - 1f, bar.y - 4f, 3f, bar.height + 8f), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }
    }
}
