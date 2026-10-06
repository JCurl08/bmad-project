using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Cube
{
    /// <summary>
    /// Debug overlay and commands (editor and development builds only):
    /// F1 toggles the overlay (face, theme, cell, seed), F5 rerolls to a new random seed and rebuilds,
    /// F6 jumps to the next unsealed screen.
    /// </summary>
    public class CubeDebug : MonoBehaviour
    {
        [SerializeField] private CubeWorld world;
        [SerializeField] private CubeNavigator navigator;
        [SerializeField] private bool overlayVisible = true;

        private GUIStyle style;

        public CubeWorld World
        {
            get => world;
            set => world = value;
        }

        public CubeNavigator Navigator
        {
            get => navigator;
            set => navigator = value;
        }

        public bool OverlayVisible => overlayVisible;

        /// <summary>True only in the editor and in development builds.</summary>
        public static bool Available
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return Debug.isDebugBuild;
#else
                return false;
#endif
            }
        }

        private void Awake()
        {
            if (!Available) enabled = false;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || world == null) return;

            if (keyboard.f1Key.wasPressedThisFrame) overlayVisible = !overlayVisible;
            if (keyboard.f5Key.wasPressedThisFrame) Reroll();
            if (keyboard.f6Key.wasPressedThisFrame) JumpToNextUnsealed();
        }

        /// <summary>Rebuilds the world from a new random seed (differs from the current one).</summary>
        public int Reroll()
        {
            int current = world.Seed;
            var entropy = new SeededRng(unchecked((ulong)DateTime.UtcNow.Ticks ^ (ulong)(uint)current));
            int next;
            do next = (int)(entropy.NextUInt() & 0x7FFFFFFF);
            while (next == current);
            world.Rebuild(next);
            return next;
        }

        /// <summary>Teleports to the next screen (in face/cell order, wrapping) that is not on a sealed face.</summary>
        public ScreenAddress JumpToNextUnsealed()
        {
            CubeModel model = world.Model;
            ScreenAddress current = navigator != null ? navigator.Current : model.StartScreen;
            var all = new System.Collections.Generic.List<ScreenAddress>(model.AllScreens());
            int index = Mathf.Max(0, all.IndexOf(current));
            for (int k = 1; k <= all.Count; k++)
            {
                ScreenAddress candidate = all[(index + k) % all.Count];
                if (model.IsSealed(candidate.Face) || candidate == current) continue;
                if (navigator != null) navigator.TeleportTo(candidate, navigator.Facing);
                return candidate;
            }
            return current;
        }

        private void OnGUI()
        {
            if (!overlayVisible || world == null || world.Model == null) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 18 };
                style.normal.textColor = Color.white;
            }

            CubeModel model = world.Model;
            ScreenAddress here = navigator != null ? navigator.Current : model.StartScreen;
            string facing = navigator != null ? navigator.Facing.ToString() : "-";
            string text =
                $"Seed {model.Seed}   N={model.FaceSize}\n" +
                $"Face {here.Face}   Theme {model.ThemeOf(here.Face)}{(model.IsSealed(here.Face) ? " (sealed)" : "")}\n" +
                $"Cell ({here.Cell.x},{here.Cell.y})   Entered facing {facing}\n" +
                "F1 overlay   F5 reroll   F6 next screen";

            var rect = new Rect(8, 8, 520, 100);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 8, rect.y + 4, rect.width - 16, rect.height - 8), text, style);
        }
    }
}
