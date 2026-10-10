using System;
using System.Collections.Generic;
using Game.Tracer;
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
    /// While open it holds the talk pause (4.18, GamePause.BeginTalk): time stands still, so the player, every other NPC and
    /// enemy, the hazards, timers, wind and ice drivers and the core arena all wait; the box reads presses every frame, so it
    /// needs no time itself. Closing (or disabling) it lets go.
    /// <para>4.20: a conversation may end in a yes/no question (DialogueChoice). On its last line the two answers show; a
    /// fresh push on the project-wide Player/Move action picks one (left or up: the first, right or down: the second) and
    /// Interact confirms it. The answer's follow-up lines (DialogueChoice.Answer) then play in the same box, so the talk pause
    /// holds throughout. Esc closes it unanswered (the question is asked again next time). The direction keys only choose:
    /// while the box is open time stands still, and once a conversation that asked a question closes, the player waits for a
    /// fresh press (PlayerMover.StopUntilFreshPress), so a key still held from choosing never walks them off.</para>
    /// </summary>
    public class DialogueBox : MonoBehaviour
    {
        /// <summary>The prompt under a line with more to come, and under the last line.</summary>
        public const string NextPrompt = "[Interact] next   [Esc] skip";
        public const string ClosePrompt = "[Interact] close";

        /// <summary>The prompt under a question (4.20).</summary>
        public const string ChoicePrompt = "[Arrows] choose   [Interact] confirm";

        [SerializeField] private string interactActionPath = "Player/Interact";
        [SerializeField] private string moveActionPath = "Player/Move";

        /// <summary>The key that closes a conversation at once.</summary>
        public const Key SkipKey = Key.Escape;

        /// <summary>How far the Move action must be pushed (from below it) to pick an answer.</summary>
        public const float ChoosePush = 0.5f;

        private readonly List<string> lines = new List<string>();
        private InputAction interact;
        private InputAction move;
        private Vector2 lastMove;
        private DialogueChoice choice;
        private bool askedThisTalk;
        private GUIStyle textStyle;
        private GUIStyle nameStyle;
        private static DialogueBox shared;

        public bool IsOpen { get; private set; }
        public int Index { get; private set; }
        public string Speaker { get; private set; }
        public object Owner { get; private set; }
        public IReadOnlyList<string> Lines => lines;
        public string CurrentLine => IsOpen && Index < lines.Count ? lines[Index] : null;

        /// <summary>The question this conversation ends in (4.20), until it is answered; null for a plain conversation.</summary>
        public DialogueChoice Choice => choice;

        /// <summary>True while the question shows: its line is up and it waits for an answer.</summary>
        public bool ChoiceOpen => IsOpen && choice != null && Index == lines.Count - 1;

        /// <summary>The answer picked so far (true: the first, "yes"). Each question starts on the first.</summary>
        public bool SelectedYes { get; private set; } = true;

        /// <summary>The last answer given in this box (null until a question is answered; tests and tools read it).</summary>
        public bool? LastAnswer { get; private set; }

        /// <summary>Frame numbers of the last open and close (to keep one press from doing two things).</summary>
        public int OpenedFrame { get; private set; } = -1;
        public int ClosedFrame { get; private set; } = -1;

        public event Action<DialogueBox> Opened;
        public event Action<DialogueBox> Closed;

        /// <summary>Raised when a question is answered (4.20), with the answer (true: the first), before its follow-up shows.</summary>
        public event Action<DialogueBox, bool> Answered;

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
            move = NpcInput.Find(moveActionPath, this);
        }

        // The project-wide action is shared with every talker, so it is not disabled here. A box that goes away while open
        // (a scene unload, a rebuild) closes, so it never leaves the talk pause held.
        private void OnDisable()
        {
            interact = null;
            move = null;
            Close();
        }

        private void Update()
        {
            Vector2 before = lastMove;
            lastMove = move != null ? move.ReadValue<Vector2>() : Vector2.zero;
            if (!IsOpen || GamePause.Active) return; // the map is open: the conversation waits
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[SkipKey].wasPressedThisFrame)
            {
                Skip();
                return;
            }
            if (ChoiceOpen) Steer(before, lastMove);
            if (interact == null || Time.frameCount == OpenedFrame) return;
            if (!interact.WasPressedThisFrame()) return;
            if (ChoiceOpen) Choose(SelectedYes);
            else Advance();
        }

        /// <summary>A fresh push on the Move action picks an answer: left or up the first, right or down the second.</summary>
        private void Steer(Vector2 before, Vector2 now)
        {
            if (now.magnitude < ChoosePush || before.magnitude >= ChoosePush) return;
            if (Mathf.Abs(now.x) >= Mathf.Abs(now.y)) Select(now.x < 0f);
            else Select(now.y > 0f);
        }

        /// <summary>Closes the conversation at once (the skip key). Does nothing when closed.</summary>
        public void Skip() => Close();

        /// <summary>
        /// Opens the box with the given lines (replacing any open conversation). Returns false if there are none. With a
        /// question (4.20), the last line asks it and its answers show there.
        /// </summary>
        public bool Open(string speaker, IReadOnlyList<string> newLines, object owner = null, DialogueChoice question = null)
        {
            if (newLines == null || newLines.Count == 0) return false;
            lines.Clear();
            lines.AddRange(newLines);
            Speaker = speaker;
            Owner = owner;
            Index = 0;
            choice = question;
            SelectedYes = true;
            askedThisTalk = question != null;
            IsOpen = true;
            OpenedFrame = Time.frameCount;
            GamePause.BeginTalk(this);
            Opened?.Invoke(this);
            return true;
        }

        /// <summary>Picks an answer (true: the first) without confirming it. Does nothing unless the question shows.</summary>
        public void Select(bool yes)
        {
            if (ChoiceOpen) SelectedYes = yes;
        }

        /// <summary>
        /// Answers the question (true: the first answer): its follow-up lines (DialogueChoice.Answer) replace the conversation
        /// in the same box, or it closes when there are none. Returns false unless the question shows.
        /// </summary>
        public bool Choose(bool yes)
        {
            if (!ChoiceOpen) return false;
            DialogueChoice asked = choice;
            choice = null;
            SelectedYes = yes;
            LastAnswer = yes;
            Answered?.Invoke(this, yes);
            IReadOnlyList<string> next = asked.Answer != null ? asked.Answer(yes) : null;
            if (!IsOpen) return true; // the answer closed the conversation itself
            if (next == null || next.Count == 0)
            {
                Close();
                return true;
            }
            lines.Clear();
            lines.AddRange(next);
            Index = 0;
            OpenedFrame = Time.frameCount; // the confirming press never also pages the follow-up
            return true;
        }

        /// <summary>Shows the next line, or closes after the last. A question's line waits for its answer (Choose) instead.</summary>
        public void Advance()
        {
            if (!IsOpen || ChoiceOpen) return;
            Index++;
            if (Index >= lines.Count) Close();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Owner = null;
            choice = null;
            ClosedFrame = Time.frameCount;
            GamePause.EndTalk(this);
            // 4.20: the direction keys chose the answer; a key still held must not walk the player off once time runs again.
            if (askedThisTalk)
            {
                askedThisTalk = false;
                PlayerMover mover = FindAnyObjectByType<PlayerMover>();
                if (mover != null && mover.isActiveAndEnabled) mover.StopUntilFreshPress();
            }
            Closed?.Invoke(this);
        }

        /// <summary>The box's panel on a screen (pixels, GUI y down): centred, along the bottom 150 px (4.19 review: layout tests).</summary>
        public static Rect PanelRect(float screenWidth, float screenHeight)
        {
            float width = Mathf.Min(screenWidth - 32f, 760f);
            return new Rect((screenWidth - width) / 2f, screenHeight - 150f, width, 134f);
        }

        /// <summary>Where a question's two answers and its prompt sit in a panel (4.20): along its bottom row.</summary>
        public static Rect[] ChoiceRects(Rect panel) => new[]
        {
            new Rect(panel.x + 14f, panel.yMax - 26f, 120f, 22f),
            new Rect(panel.x + 134f, panel.yMax - 26f, 120f, 22f),
            new Rect(panel.xMax - 330f, panel.yMax - 26f, 320f, 22f),
        };

        private static readonly Color DimAnswer = new Color(0.7f, 0.7f, 0.7f);

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
            Rect rect = PanelRect(Screen.width, Screen.height);
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 14, rect.y + 8, rect.width - 28, 24), Speaker, nameStyle);
            GUI.Label(new Rect(rect.x + 14, rect.y + 34, rect.width - 28, rect.height - 60), CurrentLine, textStyle);
            if (ChoiceOpen)
            {
                // The two answers: the picked one in gold behind a pointer, the other dimmed.
                Rect[] at = ChoiceRects(rect);
                Color gold = nameStyle.normal.textColor;
                nameStyle.normal.textColor = SelectedYes ? gold : DimAnswer;
                GUI.Label(at[0], (SelectedYes ? "> " : "  ") + choice.YesLabel, nameStyle);
                nameStyle.normal.textColor = SelectedYes ? DimAnswer : gold;
                GUI.Label(at[1], (SelectedYes ? "  " : "> ") + choice.NoLabel, nameStyle);
                nameStyle.normal.textColor = gold;
                GUI.Label(at[2], ChoicePrompt, nameStyle);
                return;
            }
            string more = Index < lines.Count - 1 ? NextPrompt : ClosePrompt;
            GUI.Label(new Rect(rect.xMax - 250, rect.yMax - 26, 240, 22), more, nameStyle);
        }
    }

    /// <summary>
    /// A yes/no question a conversation ends in (4.20, DialogueBox): its two answers' labels and what answering does. Answer
    /// is called once, with true for the first answer, and returns the lines said next (none: the conversation ends).
    /// </summary>
    public sealed class DialogueChoice
    {
        public const string DefaultYes = "Yes";
        public const string DefaultNo = "No";

        public string YesLabel { get; }
        public string NoLabel { get; }
        public Func<bool, IReadOnlyList<string>> Answer { get; }

        public DialogueChoice(Func<bool, IReadOnlyList<string>> answer, string yesLabel = DefaultYes, string noLabel = DefaultNo)
        {
            Answer = answer;
            YesLabel = string.IsNullOrEmpty(yesLabel) ? DefaultYes : yesLabel;
            NoLabel = string.IsNullOrEmpty(noLabel) ? DefaultNo : noLabel;
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
