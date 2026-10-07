using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A snow patch: the player slides on it. Its grip (Strength) is how much of the way to the wanted velocity each physics
    /// step goes, so a player keeps sliding after letting go and turns slowly, but always gets where it steers (PlayerMover
    /// keeps the grip above MinGrip), so it can always leave the patch.
    /// </summary>
    public class SnowPatch : HazardZone
    {
        public static readonly Color Tint = new Color(1f, 1f, 1f, 0.8f);

        public override HazardKind Kind => HazardKind.Snow;

        public override MoveModifier Modifier => new MoveModifier { SpeedMultiplier = 1f, Push = Vector2.zero, Grip = Strength };

        public static SnowPatch Create(Transform parent, ScreenAddress screen, Rect worldArea, float grip, Material material) =>
            Create<SnowPatch>("Snow Patch", parent, screen, worldArea, grip, ArtKey.SnowPatch, Tint, material);
    }
}
