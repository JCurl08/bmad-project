using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A patch of freezing rain: the player walks slower in it, at Strength of its speed (never below PlayerMover's
    /// MinSpeedMultiplier). Falling drops show where it is.
    /// </summary>
    public class FreezingRain : HazardZone
    {
        public static readonly Color Tint = new Color(0.6f, 0.75f, 1f, 0.55f);
        public static readonly Color DropColor = new Color(0.75f, 0.85f, 1f, 0.8f);

        public override HazardKind Kind => HazardKind.FreezingRain;

        public override MoveModifier Modifier => new MoveModifier { SpeedMultiplier = Strength, Push = Vector2.zero, Grip = 1f };

        public static FreezingRain Create(Transform parent, ScreenAddress screen, Rect worldArea, float speed, Material material)
        {
            FreezingRain rain = Create<FreezingRain>("Freezing Rain", parent, screen, worldArea, speed, ArtKey.FreezingRain, Tint, material);
            // A few slanted drops over the patch.
            int cols = Mathf.Max(2, Mathf.FloorToInt(worldArea.width / 1.5f));
            int rows = Mathf.Max(2, Mathf.FloorToInt(worldArea.height / 1.5f));
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < cols; i++)
                {
                    Vector2 local = new Vector2(((i + 0.5f) / cols - 0.5f) * worldArea.width + (j % 2) * 0.35f,
                        ((j + 0.5f) / rows - 0.5f) * worldArea.height);
                    SpriteRenderer drop = ArtCatalog.AddShape(rain.transform, "Drop", PartShape.Square, local, new Vector2(0.06f, 0.35f),
                        DropColor, -7, material);
                    drop.transform.localRotation = Quaternion.Euler(0f, 0f, 15f);
                }
            return rain;
        }
    }
}
