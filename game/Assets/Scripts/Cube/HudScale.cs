using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The one scale every OnGUI panel draws at (4.21): the HUDs were laid out in pixels for the 960x600 WebGL canvas, so on a
    /// bigger window, fullscreen or a HiDPI screen their text was tiny. Each OnGUI calls Begin, which sets GUI.matrix to
    /// Factor and returns the virtual screen it lays out in: the real screen divided by Factor. Factor is the smaller of
    /// width/960 and height/600, so the virtual screen is always at least 960 wide and 600 tall: a layout that fits 960x600
    /// fits at any size, and everything (text, hearts, the map) grows with the window. IMGUI maps the mouse through the matrix,
    /// so buttons keep working. Pure helpers (Factor, VirtualSize, ToGui) for the layout tests.
    /// </summary>
    public static class HudScale
    {
        /// <summary>The size the HUDs were laid out for (the WebGL canvas).</summary>
        public const float BaseWidth = 960f;

        public const float BaseHeight = 600f;

        /// <summary>The scale never goes below this (a tiny window still shows readable, if cut-off, text).</summary>
        public const float MinFactor = 0.5f;

        /// <summary>The scale for a real screen: min(width/960, height/600), at least MinFactor. 1 at 960x600.</summary>
        public static float Factor(float screenWidth, float screenHeight)
        {
            if (screenWidth <= 0f || screenHeight <= 0f) return 1f;
            return Mathf.Max(MinFactor, Mathf.Min(screenWidth / BaseWidth, screenHeight / BaseHeight));
        }

        /// <summary>The virtual screen (GUI units) a real screen is laid out in.</summary>
        public static Vector2 VirtualSize(float screenWidth, float screenHeight)
        {
            float s = Factor(screenWidth, screenHeight);
            return new Vector2(screenWidth / s, screenHeight / s);
        }

        /// <summary>A rect in GUI units as real screen pixels (tests: what the player sees).</summary>
        public static Rect ToPixels(Rect gui, float screenWidth, float screenHeight)
        {
            float s = Factor(screenWidth, screenHeight);
            return new Rect(gui.x * s, gui.y * s, gui.width * s, gui.height * s);
        }

        /// <summary>
        /// A screen point (pixels, y up, as Camera.WorldToScreenPoint gives) in GUI units (y down), for a label over a world
        /// point (popups, debug labels).
        /// </summary>
        public static Vector2 ToGui(Vector3 screenPoint, float screenWidth, float screenHeight)
        {
            float s = Factor(screenWidth, screenHeight);
            return new Vector2(screenPoint.x / s, (screenHeight - screenPoint.y) / s);
        }

        /// <summary>The current screen's scale.</summary>
        public static float Current => Factor(Screen.width, Screen.height);

        /// <summary>
        /// Starts an OnGUI at the shared scale: sets GUI.matrix and returns the virtual screen size to lay out in (use it in
        /// place of Screen.width and Screen.height).
        /// </summary>
        public static Vector2 Begin()
        {
            float w = Screen.width, h = Screen.height;
            float s = Factor(w, h);
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            return new Vector2(w / s, h / s);
        }

        /// <summary>A world point as a GUI point on the current screen (null when behind the camera).</summary>
        public static Vector2? WorldToGui(Camera cam, Vector3 world)
        {
            if (cam == null) return null;
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z < 0f) return null;
            return ToGui(screen, Screen.width, Screen.height);
        }
    }
}
