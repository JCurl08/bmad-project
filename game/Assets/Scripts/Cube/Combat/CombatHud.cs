using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The player's combat HUD (OnGUI placeholder): a row of hearts at the bottom left (one per health point, partly filled for
    /// a fraction, e.g. quarter hearts after defence) with the exact health as a number, the equipped item's name, and a campy
    /// message once the player has died.
    /// </summary>
    public class CombatHud : MonoBehaviour
    {
        public const string DeathMessage = "You fell apart (entropy wins… this time)";
        public const string BareHandsLabel = "Bare hands (a very sincere bonk)";

        [SerializeField] private Health health;
        [SerializeField] private Equipment equipment;

        private GUIStyle textStyle;
        private GUIStyle deathStyle;

        public Health Health
        {
            get => health;
            set => health = value;
        }

        public Equipment Equipment
        {
            get => equipment;
            set => equipment = value;
        }

        /// <summary>The equipped item line as shown.</summary>
        public string EquippedText =>
            "Equipped: " + (equipment != null && equipment.Equipped != null ? equipment.Equipped.ToString() : BareHandsLabel);

        /// <summary>Health as shown next to the hearts, to two decimals (defence makes quarter-heart hits).</summary>
        public string HealthText => health != null ? $"{health.Current:0.##}/{health.Max:0.##}" : "";

        /// <summary>The death message while the player is dead, otherwise null.</summary>
        public string MessageText => health != null && health.IsDead ? DeathMessage : null;

        private void OnGUI()
        {
            if (health == null) return;
            if (textStyle == null)
            {
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
                textStyle.normal.textColor = Color.white;
                deathStyle = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                deathStyle.normal.textColor = new Color(1f, 0.85f, 0.4f);
            }

            // Bottom left, just above the dialogue box's band (the bottom 150 px), so it never overlaps the debug
            // overlay (top left, about 156 px tall) or the dialogue box, at any width (the 960x600 WebGL canvas too).
            const float size = 22f, gap = 6f, margin = 12f, dialogueBand = 150f;
            int hearts = Mathf.CeilToInt(health.Max);
            float width = hearts * (size + gap) - gap;
            float x = margin + 8f;
            float y = Screen.height - dialogueBand - margin - (size + 40f) + 6f;
            var back = new Rect(x - 8f, y - 6f, Mathf.Max(width + 130f, 300f) + 16f, size + 40f);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(back, Texture2D.whiteTexture);
            for (int i = 0; i < hearts; i++)
            {
                float fill = Mathf.Clamp01(health.Current - i);
                var rect = new Rect(x + i * (size + gap), y, size, size);
                GUI.color = new Color(0.3f, 0.05f, 0.08f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                if (fill > 0f)
                {
                    GUI.color = new Color(1f, 0.25f, 0.35f);
                    GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * fill, rect.height), Texture2D.whiteTexture);
                }
            }
            GUI.color = Color.white;
            GUI.Label(new Rect(x + width + 10f, y - 2f, 120f, 26f), HealthText, textStyle);
            GUI.Label(new Rect(x, y + size + 4f, Mathf.Max(width, 300f), 26f), EquippedText, textStyle);

            string message = MessageText;
            if (message == null) return;
            var panel = new Rect(Screen.width / 2f - 320f, Screen.height / 2f - 60f, 640f, 120f);
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(panel, message, deathStyle);
        }
    }
}
