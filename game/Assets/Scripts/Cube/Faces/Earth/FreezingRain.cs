using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Freezing rain: the player walks slower in it, at Strength of its speed (never below PlayerMover's
    /// MinSpeedMultiplier). Since 4.25 it covers its whole screen (EarthPlan.RainArea): a faint wash over the floor and slanted
    /// drops falling all over it, wrapping round at the bottom.
    /// </summary>
    public class FreezingRain : HazardZone
    {
        /// <summary>The wash over the rainy screen (fainter since 4.25: it covers the whole floor).</summary>
        public static readonly Color Tint = new Color(0.6f, 0.75f, 1f, 0.3f);
        public static readonly Color DropColor = new Color(0.75f, 0.85f, 1f, 0.8f);

        /// <summary>How fast the drops fall (world units a second) and how far apart they start (first-screen units, scaled).</summary>
        public const float DropSpeed = 6f, DropSpacing = 1.5f * ScreenMath.ContentScale;

        private readonly List<Transform> drops = new List<Transform>();

        public override HazardKind Kind => HazardKind.FreezingRain;

        public override MoveModifier Modifier => new MoveModifier { SpeedMultiplier = Strength, Push = Vector2.zero, Grip = 1f };

        /// <summary>The falling drops (local positions inside the area).</summary>
        public IReadOnlyList<Transform> Drops => drops;

        private void Update()
        {
            float half = Area.height / 2f;
            float fall = DropSpeed * Time.deltaTime;
            foreach (Transform drop in drops)
            {
                if (drop == null) continue;
                Vector3 p = drop.localPosition;
                p.y -= fall;
                p.x -= fall * 0.27f; // slanted, as the drop leans
                if (p.y < -half)
                {
                    p.y += Area.height;
                    p.x += Area.height * 0.27f;
                }
                drop.localPosition = p;
            }
        }

        public static FreezingRain Create(Transform parent, ScreenAddress screen, Rect worldArea, float speed, Material material)
        {
            FreezingRain rain = Create<FreezingRain>("Freezing Rain", parent, screen, worldArea, speed, ArtKey.FreezingRain, Tint, material);
            // Slanted drops all over the area, offset row by row so they read as rain, not a grid.
            int cols = Mathf.Max(2, Mathf.FloorToInt(worldArea.width / DropSpacing));
            int rows = Mathf.Max(2, Mathf.FloorToInt(worldArea.height / DropSpacing));
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < cols; i++)
                {
                    Vector2 local = new Vector2(((i + 0.5f) / cols - 0.5f) * worldArea.width + (j % 2) * 0.35f * ScreenMath.ContentScale,
                        ((j + 0.5f) / rows - 0.5f) * worldArea.height);
                    SpriteRenderer drop = ArtCatalog.AddShape(rain.transform, "Drop", PartShape.Square, local, new Vector2(0.06f, 0.35f) * ScreenMath.ContentScale,
                        DropColor, -7, material);
                    drop.transform.localRotation = Quaternion.Euler(0f, 0f, 15f);
                    rain.drops.Add(drop.transform);
                }
            return rain;
        }
    }
}
