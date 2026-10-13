using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Game.Cube
{
    /// <summary>The project-wide input actions the game reads (4.21: every control is an action with keys and a pad button).</summary>
    public static class GameActions
    {
        public const string Move = "Player/Move";
        public const string Attack = "Player/Attack";
        public const string Interact = "Player/Interact";
        public const string Previous = "Player/Previous";
        public const string Next = "Player/Next";
        public const string Map = "Player/Map";
        public const string QuestLog = "Player/QuestLog";

        /// <summary>Closes a menu or skips a conversation: Esc, and Backspace (Esc also leaves browser fullscreen).</summary>
        public const string Close = "Player/Close";

        /// <summary>The between-runs screen's Continue: Enter, or the pad's Start.</summary>
        public const string Continue = "Player/Continue";

        /// <summary>The between-runs screen: the pad (or the arrow keys) moves a pick over the upgrades and Continue; Confirm takes it.</summary>
        public const string MenuUp = "Player/MenuUp";
        public const string MenuDown = "Player/MenuDown";
        public const string Confirm = "Player/Confirm";

        /// <summary>From the quest log: asks to abandon the run.</summary>
        public const string Abandon = "Player/Abandon";

        /// <summary>The between-runs screen's buy keys (1-4).</summary>
        public static string Buy(int index) => $"Player/Buy{index + 1}";

        /// <summary>Every action the game's code reads, for the binding checks.</summary>
        public static IEnumerable<string> All()
        {
            yield return Move;
            yield return Attack;
            yield return Interact;
            yield return Previous;
            yield return Next;
            yield return Map;
            yield return QuestLog;
            yield return Close;
            yield return Continue;
            yield return MenuUp;
            yield return MenuDown;
            yield return Confirm;
            yield return Abandon;
            for (int i = 0; i < 4; i++) yield return Buy(i);
        }

        /// <summary>A project-wide action, enabled (never disabled here: the actions are shared). Null when missing.</summary>
        public static InputAction Find(string path)
        {
            InputAction action = InputSystem.actions != null ? InputSystem.actions.FindAction(path) : null;
            if (action != null && !action.enabled) action.Enable();
            return action;
        }

        /// <summary>True when the action exists and was pressed this frame.</summary>
        public static bool Pressed(InputAction action) => action != null && action.WasPressedThisFrame();
    }

    /// <summary>
    /// The prompts' key names (4.21): a prompt is written with tokens ("[Interact] next   [Esc] skip"), and Fill swaps each
    /// for the real binding's name on the device in use: "[E] next   [Esc/Backspace] skip" on a keyboard, "[Y] next   [B]
    /// skip" on a pad. The names come from the action's own bindings (the keyboard's through InputControlPath; the pad's in
    /// the Xbox style, A/B/X/Y). The device in use is the one whose button or key last performed an action (4.21 review:
    /// sticks, the mouse's movement and Look never switch it); a keyboard until a pad button is pressed. A joystick counts as
    /// a pad and shows the pad bindings' names (the actions bind joysticks only for moving and swinging). Filled strings are
    /// cached per template and device, and the cache is dropped when the bindings change.
    /// </summary>
    public static class InputPrompts
    {
        public const string KeyboardGroup = "Keyboard&Mouse";
        public const string GamepadGroup = "Gamepad";

        /// <summary>Each token a prompt may hold, and its action.</summary>
        public static readonly IReadOnlyDictionary<string, string> Tokens = new Dictionary<string, string>
        {
            { "[Interact]", GameActions.Interact },
            { "[Esc]", GameActions.Close },
            { "[M]", GameActions.Map },
            { "[Q]", GameActions.QuestLog },
            { "[Enter]", GameActions.Continue },
            { "[Arrows]", GameActions.Move },
            { "[Abandon]", GameActions.Abandon },
            { "[Confirm]", GameActions.Confirm },
            { "[prev]", GameActions.Previous },
            { "[next]", GameActions.Next },
            { "[1]", GameActions.Buy(0) },
            { "[2]", GameActions.Buy(1) },
            { "[3]", GameActions.Buy(2) },
            { "[4]", GameActions.Buy(3) },
        };

        /// <summary>True while the player is using a gamepad (set by the last action performed; tests may set it).</summary>
        public static bool UsingGamepad { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            UsingGamepad = false;
            InputSystem.onActionChange -= OnActionChange;
            InputSystem.onActionChange += OnActionChange;
        }

        private static void OnActionChange(object obj, InputActionChange change)
        {
            if (change == InputActionChange.BoundControlsChanged)
            {
                ClearCache(); // a rebinding (or a device's layout) changed the names
                return;
            }
            if (change != InputActionChange.ActionPerformed || !(obj is InputAction action)) return;
            bool? pad = DeviceFor(action.name, action.activeControl);
            if (pad.HasValue) UsingGamepad = pad.Value;
        }

        /// <summary>
        /// Whether a performed control means a pad (true), a keyboard (false) or says nothing (null): only a button or key
        /// counts, never a stick, the pointer's movement or the Look action. Pure, for the tests.
        /// </summary>
        public static bool? DeviceFor(string actionName, InputControl control)
        {
            if (control == null || actionName == "Look" || !(control is ButtonControl)) return null;
            InputDevice device = control.device;
            if (device is Gamepad || device is Joystick) return true;
            if (device is Keyboard || device is Mouse) return false;
            return null;
        }

        private static readonly Dictionary<(string, bool), string> cache = new Dictionary<(string, bool), string>();
        private static InputActionAsset cachedFor;

        /// <summary>The most filled strings kept (dialogue lines pass through Fill too).</summary>
        public const int CacheLimit = 512;

        /// <summary>Filled strings kept now (tests).</summary>
        public static int CachedCount => cache.Count;

        public static void ClearCache()
        {
            cache.Clear();
            cachedFor = null;
        }

        /// <summary>
        /// A prompt with every token swapped for the device in use's binding names; cached per template and device (rebuilt when
        /// the device, the bindings or the actions asset change).
        /// </summary>
        public static string Fill(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('[') < 0) return text;
            InputActionAsset actions = InputSystem.actions;
            if (!ReferenceEquals(actions, cachedFor))
            {
                cache.Clear();
                cachedFor = actions;
            }
            bool pad = UsingGamepad;
            if (cache.TryGetValue((text, pad), out string filled)) return filled;
            filled = Fill(text, pad, actions);
            if (cache.Count >= CacheLimit) cache.Clear();
            cache[(text, pad)] = filled;
            return filled;
        }

        /// <summary>A prompt with its tokens swapped for one device's names, from an action asset (pure: tests pass the asset).</summary>
        public static string Fill(string text, bool gamepad, InputActionAsset actions)
        {
            if (string.IsNullOrEmpty(text) || actions == null || text.IndexOf('[') < 0) return text;
            foreach (KeyValuePair<string, string> token in Tokens)
            {
                if (text.IndexOf(token.Key, System.StringComparison.Ordinal) < 0) continue;
                string names = Names(actions.FindAction(token.Value), gamepad);
                if (names != null) text = text.Replace(token.Key, "[" + names + "]");
            }
            return text;
        }

        /// <summary>
        /// An action's binding names on one device, joined by "/" ("M/Tab"; "Arrows" for a keyboard's move; "Stick" for a
        /// pad's), or null when it has none on that device.
        /// </summary>
        public static string Names(InputAction action, bool gamepad)
        {
            if (action == null) return null;
            string group = gamepad ? GamepadGroup : KeyboardGroup;
            if (action.name == "Move") return gamepad ? "Stick" : "Arrows";
            var names = new List<string>();
            foreach (InputBinding b in action.bindings)
            {
                if (b.isComposite || b.isPartOfComposite || string.IsNullOrEmpty(b.path)) continue;
                if (!InGroup(b.groups, group)) continue;
                if (b.path.StartsWith("<Mouse>") || b.path.StartsWith("<Pen>")) continue;
                string name = gamepad ? PadName(b.path) : KeyName(b.path);
                if (!string.IsNullOrEmpty(name) && !names.Contains(name)) names.Add(name);
            }
            if (names.Count == 0) return null;
            var sb = new StringBuilder();
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0) sb.Append('/');
                sb.Append(names[i]);
            }
            return sb.ToString();
        }

        private static bool InGroup(string groups, string group)
        {
            if (string.IsNullOrEmpty(groups)) return false;
            foreach (string g in groups.Split(';'))
                if (g == group) return true;
            return false;
        }

        /// <summary>A keyboard binding's name: the key's own ("E", "Tab", "Enter"), Escape as "Esc"; numpad twins are left out.</summary>
        public static string KeyName(string path)
        {
            if (!path.StartsWith("<Keyboard>/")) return null;
            string control = path.Substring("<Keyboard>/".Length);
            if (control.StartsWith("numpad")) return null; // the main key's twin: one name is enough
            switch (control)
            {
                case "escape": return "Esc";
                case "enter": return "Enter";
                case "space": return "Space";
                case "backspace": return "Backspace";
                case "upArrow": return "Up";
                case "downArrow": return "Down";
                case "leftArrow": return "Left";
                case "rightArrow": return "Right";
                case "tab": return "Tab";
            }
            string readable = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            return string.IsNullOrEmpty(readable) ? control.ToUpperInvariant() : readable.ToUpperInvariant();
        }

        /// <summary>A pad binding's name, Xbox style: A/B/X/Y, Start, Select ("View"), "D-Pad Up", "Stick Up".</summary>
        public static string PadName(string path)
        {
            if (!path.StartsWith("<Gamepad>/")) return null;
            switch (path.Substring("<Gamepad>/".Length))
            {
                case "buttonSouth": return "A";
                case "buttonEast": return "B";
                case "buttonWest": return "X";
                case "buttonNorth": return "Y";
                case "start": return "Start";
                case "select": return "Select";
                case "dpad/up": return "D-Pad Up";
                case "dpad/down": return "D-Pad Down";
                case "dpad/left": return "D-Pad Left";
                case "dpad/right": return "D-Pad Right";
                case "leftStick/up": return null; // the d-pad's twin
                case "leftStick/down": return null;
                case "leftShoulder": return "LB";
                case "rightShoulder": return "RB";
                case "leftStickPress": return "LS";
                default: return null;
            }
        }
    }
}
