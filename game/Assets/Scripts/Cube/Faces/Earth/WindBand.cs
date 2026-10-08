using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A wind band: pushes a player standing in it along its direction, by Strength of the player's speed (a bounded push:
    /// PlayerMover never lets it exceed MaxPushFraction, so walking against it always makes headway). Its streaks and
    /// arrows show the way it blows.
    /// </summary>
    public class WindBand : HazardZone
    {
        public static readonly Color Tint = new Color(0.85f, 0.95f, 1f, 0.35f);
        public static readonly Color ArrowColor = new Color(1f, 1f, 1f, 0.55f);

        [SerializeField] private Vector2 direction = Vector2.right;

        public override HazardKind Kind => HazardKind.Wind;

        /// <summary>Which way it blows (unit length).</summary>
        public Vector2 Direction
        {
            get => direction;
            set => direction = value.sqrMagnitude > 1e-6f ? value.normalized : Vector2.right;
        }

        public override MoveModifier Modifier => new MoveModifier
        {
            SpeedMultiplier = 1f,
            Push = direction * Strength,
            Grip = 1f,
        };

        public static WindBand Create(Transform parent, ScreenAddress screen, Rect worldArea, Vector2 direction, float strength,
            Material material)
        {
            WindBand band = Create<WindBand>("Wind Band", parent, screen, worldArea, strength, ArtKey.WindBand, Tint, material);
            band.Direction = direction;
            // Arrows along the band, pointing the way it blows.
            bool horizontal = Mathf.Abs(direction.x) > Mathf.Abs(direction.y);
            float length = horizontal ? worldArea.width : worldArea.height;
            int count = Mathf.Max(2, Mathf.FloorToInt(length / (2.5f * ScreenMath.ContentScale)));
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            for (int i = 0; i < count; i++)
            {
                float t = (i + 0.5f) / count - 0.5f;
                Vector2 local = horizontal ? new Vector2(t * length, 0f) : new Vector2(0f, t * length);
                SpriteRenderer arrow = ArtCatalog.AddShape(band.transform, $"Arrow {i + 1}", PartShape.Triangle, local,
                    new Vector2(0.45f, 0.6f) * ScreenMath.ContentScale, ArrowColor, -7, material);
                arrow.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
            return band;
        }
    }
}
