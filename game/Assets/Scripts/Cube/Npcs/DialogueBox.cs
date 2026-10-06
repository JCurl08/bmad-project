using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Cube
{
    /// <summary>
    /// A simple on-screen dialogue box. Open shows the first line; the project-wide Player/Interact action
    /// advances, and it closes after the last line. The skip key (Escape) closes it at once (Skip). A press in the
    /// frame the box opened does not advance,
    /// and talkers ignore the press that closed it, so one press never both opens and skips.
    /// One shared box per scene (Shared creates it on demand).
    /// </summary>
    public class DialogueBox : MonoBehaviour
    {
        [SerializeField] private string interactActionPath = "Player/Interact";

        /// <summary>The key that closes a conversation at once.</summary>
        public const Key SkipKey = Key.Escape;

        private readonly List<string> lines = new List<string>();
        private InputAction interact;
        private GUIStyle textStyle;
        private GUIStyle nameStyle;
        private static DialogueBox shared;

        public bool IsOpen { get; private set; }
        public int Index { get; private set; }
        public string Speaker { get; private set; }
        public object Owner { get; private set; }
        public IReadOnlyList<string> Lines => lines;
        public string CurrentLine => IsOpen && Index < lines.Count ? lines[Index] : null;

        /// <summary>Frame numbers of the last open and close (to keep one press from doing two things).</summary>
        public int OpenedFrame { get; private set; } = -1;
        public int ClosedFrame { get; private set; } = -1;

        public event Action<DialogueBox> Opened;
        public event Action<DialogueBox> Closed;

        /// <summary>The scene's dialogue box, created if there is none.</summary>
        public static DialogueBox Shared
        {
            get
            {
                if (shared != null) return shared;
                shared = FindAnyObjectByType<DialogueBox>();
                if (shared == null) shared = new GameObject("Dialogue Box").AddComponent<DialogueBox>();
                return shared;
            }
        }

        private void Awake()
        {
            if (shared == null) shared = this;
        }

        private void OnDestroy()
        {
            if (shared == this) shared = null;
        }

        private void OnEnable()
        {
            interact = NpcInput.Find(interactActionPath, this);
        }

        // The project-wide action is shared with every talker, so it is not disabled here.
        private void OnDisable() => interact = null;

        private void Update()
        {
            if (!IsOpen) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[SkipKey].wasPressedThisFrame)
            {
                Skip();
                return;
            }
            if (interact == null || Time.frameCount == OpenedFrame) return;
            if (interact.WasPressedThisFrame()) Advance();
        }

        /// <summary>Closes the conversation at once (the skip key). Does nothing when closed.</summary>
        public void Skip() => Close();

        /// <summary>Opens the box with the given lines (replacing any open conversation). Returns false if there are none.</summary>
        public bool Open(string speaker, IReadOnlyList<string> newLines, object owner = null)
        {
            if (newLines == null || newLines.Count == 0) return false;
            lines.Clear();
            lines.AddRange(newLines);
            Speaker = speaker;
            Owner = owner;
            Index = 0;
            IsOpen = true;
            OpenedFrame = Time.frameCount;
            Opened?.Invoke(this);
            return true;
        }

        /// <summary>Shows the next line, or closes after the last.</summary>
        public void Advance()
        {
            if (!IsOpen) return;
            Index++;
            if (Index >= lines.Count) Close();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Owner = null;
            ClosedFrame = Time.frameCount;
            Closed?.Invoke(this);
        }

        private void OnGUI()
        {
            if (!IsOpen) return;
            if (textStyle == null)
            {
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true };
                textStyle.normal.textColor = Color.white;
                nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
                nameStyle.normal.textColor = new Color(1f, 0.85f, 0.4f);
            }
            float width = Mathf.Min(Screen.width - 32f, 760f);
            var rect = new Rect((Screen.width - width) / 2f, Screen.height - 150f, width, 134f);
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 14, rect.y + 8, rect.width - 28, 24), Speaker, nameStyle);
            GUI.Label(new Rect(rect.x + 14, rect.y + 34, rect.width - 28, rect.height - 60), CurrentLine, textStyle);
            string more = Index < lines.Count - 1 ? "[Interact] next   [Esc] skip" : "[Interact] close";
            GUI.Label(new Rect(rect.xMax - 250, rect.yMax - 26, 240, 22), more, nameStyle);
        }
    }

    /// <summary>Finds project-wide input actions for NPC code (enables, never disables: the actions are shared).</summary>
    internal static class NpcInput
    {
        public static InputAction Find(string path, UnityEngine.Object context)
        {
            InputAction action = InputSystem.actions != null ? InputSystem.actions.FindAction(path) : null;
            if (action == null)
            {
                Debug.LogError($"Input action '{path}' not found in the project-wide actions.", context);
                return null;
            }
            if (!action.enabled) action.Enable();
            return action;
        }
    }
}
