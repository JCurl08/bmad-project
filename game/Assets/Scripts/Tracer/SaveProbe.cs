using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Tracer
{
    /// <summary>
    /// Save round trip: reads a PlayerPrefs counter on start (a missing key reads as 0),
    /// increments and saves it on a key press, and shows it on screen.
    /// </summary>
    public class SaveProbe : MonoBehaviour
    {
        public const string CounterKey = "tracer.counter";

        [SerializeField] private Key incrementKey = Key.Space;

        private GUIStyle style;

        public int Counter { get; private set; }

        private void Start()
        {
            Counter = PlayerPrefs.GetInt(CounterKey, 0);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[incrementKey].wasPressedThisFrame) return;

            Counter++;
            PlayerPrefs.SetInt(CounterKey, Counter);
            PlayerPrefs.Save();
        }

        private void OnGUI()
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 20 };
                style.normal.textColor = Color.white;
            }
            GUI.Label(new Rect(12, 8, 640, 32), $"Saved counter: {Counter}   ({incrementKey} to increment)", style);
        }
    }
}
