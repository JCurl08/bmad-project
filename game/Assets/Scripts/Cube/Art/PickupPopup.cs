using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A floating text popup ("+Thin Beak", "+5 Jumbles") that rises from a world point and fades over Seconds (about 1 s),
    /// then removes itself. Drawn with OnGUI like the rest of the HUD. PickupFeedback shows one on the player for every
    /// item added to its inventory and every Jumble the run earns.
    /// </summary>
    public class PickupPopup : MonoBehaviour
    {
        public const float Seconds = 1f;
        private const float Rise = 0.8f * ScreenMath.ContentScale;

        /// <summary>How far above its point a popup starts (world units).</summary>
        public const float StartHeight = 1f * ScreenMath.ContentScale;

        private float rise = Rise;

        private static readonly List<PickupPopup> active = new List<PickupPopup>();

        private Vector3 origin;
        private Color color = Color.white;
        private float age;
        private float seconds = Seconds;
        private GUIStyle style;

        /// <summary>Popups on screen now (tests).</summary>
        public static IReadOnlyList<PickupPopup> Active => active;

        public string Text { get; private set; }

        public float Age => age;

        /// <summary>The world point the text is drawn at right now (it rises as it ages).</summary>
        public Vector3 Position => origin + Vector3.up * (StartHeight + rise * Mathf.Clamp01(age / seconds));

        /// <summary>The label is never narrower or shorter than this (pixels); a longer text widens it to fit (1.26).</summary>
        public static readonly Vector2 MinLabelSize = new Vector2(300f, 30f);

        /// <summary>Padding round the measured text (pixels, both sides together).</summary>
        public const float LabelPadding = 24f;

        /// <summary>The label's size for text that measures textSize (GUIStyle.CalcSize): it fits the text on one line.</summary>
        public static Vector2 LabelSize(Vector2 textSize) =>
            new Vector2(Mathf.Max(MinLabelSize.x, textSize.x + LabelPadding), Mathf.Max(MinLabelSize.y, textSize.y + 4f));

        /// <summary>
        /// A popup of text at a world point, up for seconds (Seconds unless a longer line needs longer); it rises by rise
        /// world units over its life (negative: the usual rise; 0 keeps it still).
        /// </summary>
        public static PickupPopup Show(Vector3 worldPosition, string text, Color color, float seconds = Seconds, float rise = -1f)
        {
            var go = new GameObject("Pickup Popup " + text);
            var popup = go.AddComponent<PickupPopup>();
            popup.origin = worldPosition;
            popup.Text = text;
            popup.color = color;
            popup.seconds = Mathf.Max(0.1f, seconds);
            popup.rise = rise < 0f ? Rise : rise;
            return popup;
        }

        private void OnEnable() => active.Add(this);

        private void OnDisable() => active.Remove(this);

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= seconds) Destroy(gameObject);
        }

        private void OnGUI()
        {
            Camera cam = Camera.main;
            if (cam == null || string.IsNullOrEmpty(Text)) return;
            HudScale.Begin(); // 4.21: the shared scale
            Vector2? at = HudScale.WorldToGui(cam, Position);
            if (at == null) return;
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            float alpha = 1f - Mathf.Clamp01((age / seconds - 0.6f) / 0.4f);
            string text = InputPrompts.Fill(Text); // 4.21: "[Interact]" shows the real key or button
            Vector2 size = LabelSize(style.CalcSize(new GUIContent(text)));
            var rect = new Rect(at.Value.x - size.x / 2f, at.Value.y - size.y / 2f, size.x, size.y);
            GUI.color = new Color(0f, 0f, 0f, 0.8f * alpha);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
            GUI.color = new Color(color.r, color.g, color.b, alpha);
            GUI.Label(rect, text, style);
            GUI.color = Color.white;
        }
    }
}
