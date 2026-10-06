using UnityEngine;

namespace Game.Tracer
{
    /// <summary>
    /// Pure screen-grid maths. Screens tile the world on a fixed-size grid;
    /// screen (0,0) spans [0, size.x) x [0, size.y).
    /// </summary>
    public static class ScreenMath
    {
        /// <summary>One screen is 16x10 world units (16 px per unit), so the camera's ortho size is 5.</summary>
        public static readonly Vector2 DefaultScreenSize = new Vector2(16f, 10f);

        /// <summary>Screen index = floor(position / screenSize) per axis.</summary>
        public static Vector2Int ScreenIndex(Vector2 worldPosition, Vector2 screenSize)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPosition.x / screenSize.x),
                Mathf.FloorToInt(worldPosition.y / screenSize.y));
        }

        public static Vector2Int ScreenIndex(Vector2 worldPosition)
        {
            return ScreenIndex(worldPosition, DefaultScreenSize);
        }

        /// <summary>World-space centre of the screen with the given index.</summary>
        public static Vector2 ScreenCenter(Vector2Int index, Vector2 screenSize)
        {
            return new Vector2((index.x + 0.5f) * screenSize.x, (index.y + 0.5f) * screenSize.y);
        }

        public static Vector2 ScreenCenter(Vector2Int index)
        {
            return ScreenCenter(index, DefaultScreenSize);
        }
    }
}
