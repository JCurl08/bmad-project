using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Cube
{
    /// <summary>
    /// Debug overlay and commands (editor and development builds only):
    /// F1 toggles the overlay (face, theme, cell, module, seed), F2 toggles slot markers (a sprite on
    /// every active slot of every module, with labels on the visible screen), F5 rerolls to a new random
    /// seed and rebuilds, F6 jumps to the next unsealed screen.
    /// </summary>
    public class CubeDebug : MonoBehaviour
    {
        [SerializeField] private CubeWorld world;
        [SerializeField] private CubeNavigator navigator;
        [SerializeField] private bool overlayVisible = true;
        [SerializeField] private bool slotMarkersVisible;

        private GUIStyle style;
        private GUIStyle labelStyle;

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

        public bool SlotMarkersVisible => slotMarkersVisible;

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

        private void OnEnable()
        {
            if (world == null) return;
            world.Rebuilt += ApplySlotMarkers;
            world.Revealed += ApplySlotMarkers;
        }

        private void OnDisable()
        {
            if (world == null) return;
            world.Rebuilt -= ApplySlotMarkers;
            world.Revealed -= ApplySlotMarkers;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || world == null) return;

            if (keyboard.f1Key.wasPressedThisFrame) overlayVisible = !overlayVisible;
            if (keyboard.f2Key.wasPressedThisFrame) SetSlotMarkersVisible(!slotMarkersVisible);
            if (keyboard.f5Key.wasPressedThisFrame) Reroll();
            if (keyboard.f6Key.wasPressedThisFrame) JumpToNextUnsealed();
        }

        /// <summary>Shows or hides the slot markers on every module in the world.</summary>
        public void SetSlotMarkersVisible(bool visible)
        {
            slotMarkersVisible = visible;
            ApplySlotMarkers();
        }

        private void ApplySlotMarkers()
        {
            if (world == null) return;
            foreach (ScreenModule module in world.AllModules)
            {
                if (module == null) continue;
                foreach (ModuleSlot slot in module.AllSlots)
                    slot.SetMarkerVisible(slotMarkersVisible, world.Sprite, world.Material);
            }
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
            if (world == null || world.Model == null) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 18 };
                style.normal.textColor = Color.white;
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
                labelStyle.normal.textColor = Color.white;
            }

            if (slotMarkersVisible) DrawSlotLabels();
            if (!overlayVisible) return;

            CubeModel model = world.Model;
            ScreenAddress here = navigator != null ? navigator.Current : model.StartScreen;
            string facing = navigator != null ? navigator.Facing.ToString() : "-";
            ScreenModule module = world.ModuleAt(here);
            string moduleText = module != null ? module.name : model.IsSealed(here.Face) ? "-" : "(unrevealed)";
            string text =
                $"Seed {model.Seed}   N={model.FaceSize}   Science {(world.ScienceRevealed ? "revealed" : "unrevealed")}\n" +
                $"Face {here.Face}   Theme {model.ThemeOf(here.Face)}{(model.IsSealed(here.Face) ? " (sealed)" : "")}\n" +
                $"Cell ({here.Cell.x},{here.Cell.y})   Entered facing {facing}\n" +
                $"Module {moduleText}\n" +
                "F1 overlay   F2 slots   F5 reroll   F6 next screen";

            var rect = new Rect(8, 8, 600, 124);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 8, rect.y + 4, rect.width - 16, rect.height - 8), text, style);
        }

        /// <summary>Labels next to every visible slot marker that is on screen.</summary>
        private void DrawSlotLabels()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            foreach (ScreenModule module in world.AllModules)
            {
                if (module == null) continue;
                foreach (ModuleSlot slot in module.AllSlots)
                {
                    if (!slot.MarkerVisible) continue;
                    Vector3 viewport = cam.WorldToViewportPoint(slot.transform.position);
                    if (viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) continue;
                    Vector3 screen = cam.WorldToScreenPoint(slot.transform.position);
                    var rect = new Rect(screen.x + 10f, Screen.height - screen.y - 10f, 160f, 22f);
                    GUI.color = slot.MarkerColor;
                    GUI.Label(rect, slot.Label, labelStyle);
                }
            }
            GUI.color = Color.white;
        }
    }
}
