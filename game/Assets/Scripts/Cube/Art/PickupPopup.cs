using System.Collections.Generic;
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
        private const float Rise = 0.8f;
        private const float StartHeight = 1f;

        private static readonly List<PickupPopup> active = new List<PickupPopup>();

        private Vector3 origin;
        private Color color = Color.white;
        private float age;
        private GUIStyle style;

        /// <summary>Popups on screen now (tests).</summary>
        public static IReadOnlyList<PickupPopup> Active => active;

        public string Text { get; private set; }

        public float Age => age;

        /// <summary>The world point the text is drawn at right now (it rises as it ages).</summary>
        public Vector3 Position => origin + Vector3.up * (StartHeight + Rise * Mathf.Clamp01(age / Seconds));

        public static PickupPopup Show(Vector3 worldPosition, string text, Color color)
        {
            var go = new GameObject("Pickup Popup " + text);
            var popup = go.AddComponent<PickupPopup>();
            popup.origin = worldPosition;
            popup.Text = text;
            popup.color = color;
            return popup;
        }

        private void OnEnable() => active.Add(this);

        private void OnDisable() => active.Remove(this);

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= Seconds) Destroy(gameObject);
        }

        private void OnGUI()
        {
            Camera cam = Camera.main;
            if (cam == null || string.IsNullOrEmpty(Text)) return;
            Vector3 screen = cam.WorldToScreenPoint(Position);
            if (screen.z < 0f) return;
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            float alpha = 1f - Mathf.Clamp01((age / Seconds - 0.6f) / 0.4f);
            var rect = new Rect(screen.x - 150f, Screen.height - screen.y - 15f, 300f, 30f);
            GUI.color = new Color(0f, 0f, 0f, 0.8f * alpha);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), Text, style);
            GUI.color = new Color(color.r, color.g, color.b, alpha);
            GUI.Label(rect, Text, style);
            GUI.color = Color.white;
        }
    }
}
