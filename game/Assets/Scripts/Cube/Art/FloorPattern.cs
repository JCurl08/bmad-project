using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The light texture over a region floor (1.24: "add a bit more texture to the floors"): every floor tile of a screen
    /// is shaded a little lighter or darker, in soft patches a few tiles across with an odd tile picked out, so the plain
    /// floors stop reading as one flat colour. Visual only (no colliders, nothing reads it) and deterministic: the shade is
    /// a hash of the theme, the screen and the tile, so a screen always looks the same and no RNG stream is touched. It
    /// stays low-contrast (at most MaxShade of black, and half that of white, over the floor; the Weightlands* floor,
    /// whose tile already carries a pattern, gets less) so the readability rules for the floors keep holding.
    /// The Weightlands* floor also gets a glaze (Glaze): its own average colour laid half over it, which halves the
    /// contrast of its deck lines without changing its colour (the user found the blue floor noisy).
    /// </summary>
    public static class FloorPattern
    {
        /// <summary>The strongest shade on any floor: the overlay's largest alpha (white to lighten, black to darken).</summary>
        public const float MaxShade = 0.07f;

        /// <summary>The floor block's share of its screen (CubeWorld draws each screen's floor this size, centred).</summary>
        public const float FloorFraction = 0.98f;

        /// <summary>Lightening is gentler than darkening (white over a saturated floor reads chalky).</summary>
        public const float LightenScale = 0.5f;

        /// <summary>
        /// The average colour of the Weightlands* floor tile (Tiny Dungeon 37, its opaque pixels), for its glaze. A test
        /// checks it against the tile.
        /// </summary>
        public static readonly Color PhysicsTileAverage = new Color(0.541f, 0.597f, 0.690f);

        /// <summary>
        /// Town's glaze colour (4.18: "white face should be more white than grey"): a warm white laid over Town's pale
        /// blue-grey stone, so the floor reads white while its checker and texture still show.
        /// </summary>
        public static readonly Color TownGlazeColor = new Color(0.98f, 0.97f, 0.94f);

        /// <summary>
        /// How much of a theme's floor the glaze covers (0: none): the Weightlands* (its own average, calming its lines) and
        /// Town (4.18: warm white, so the floor reads white, not grey).
        /// </summary>
        public static float GlazeAlpha(Theme theme) => theme == Theme.Physics ? 0.5f : theme == Theme.Town ? 0.7f : 0f;

        /// <summary>The glaze over a theme's floor drawn in blockColor (the floor's tint, checker shade included).</summary>
        public static Color Glaze(Theme theme, Color blockColor)
        {
            Color glaze = theme == Theme.Town ? TownGlazeColor : PhysicsTileAverage;
            return new Color(glaze.r * blockColor.r, glaze.g * blockColor.g, glaze.b * blockColor.b, GlazeAlpha(theme));
        }

        /// <summary>
        /// How much darker every other screen of a face is drawn (CubeWorld.BuildFloor's checker): 15 %, but only 6 % on Town
        /// (4.18), whose white floor would otherwise read grey on every other screen.
        /// </summary>
        public static float CheckerShade(Theme? theme) => theme == Theme.Town ? 0.06f : 0.15f;

        /// <summary>
        /// The colour a region floor reads as on screen: its tile (tileAverage) under its tint (ArtKeys.FloorTint), on a light
        /// or a dark checker screen (CheckerShade), with its glaze over it. What the readability tests compare against.
        /// </summary>
        public static Color Seen(Theme theme, Color tileAverage, bool darkScreen)
        {
            Color block = ArtKeys.FloorTint(theme);
            if (darkScreen) block = Color.Lerp(block, Color.black, CheckerShade(theme));
            var floor = new Color(tileAverage.r * block.r, tileAverage.g * block.g, tileAverage.b * block.b, 1f);
            Color glaze = Glaze(theme, block);
            Color seen = Color.Lerp(floor, glaze, glaze.a);
            seen.a = 1f;
            return seen;
        }

        /// <summary>Patch size in tiles: the coarse grid the soft patches are interpolated from.</summary>
        private const int PatchTiles = 3;

        private static readonly Dictionary<(Theme, ScreenAddress), Sprite> Cache = new Dictionary<(Theme, ScreenAddress), Sprite>();

        /// <summary>How strong the texture is on a theme's floor (a fraction of MaxShade).</summary>
        public static float Strength(Theme theme)
        {
            switch (theme)
            {
                case Theme.Physics: return 0.45f; // its tile already has lines: keep it calm
                case Theme.Town: return 0.8f;
                default: return 1f;
            }
        }

        /// <summary>
        /// The shade of one floor tile, in [-MaxShade, MaxShade] (negative darkens, positive lightens). tile is the tile's
        /// index on the screen (0..15 across, 0..9 up).
        /// </summary>
        public static float Shade(Theme theme, ScreenAddress screen, Vector2Int tile)
        {
            int salt = ((int)theme * 7919) ^ ((int)screen.Face * 104729) ^ (screen.Cell.x * 1301) ^ (screen.Cell.y * 7727);
            // Soft patches: value noise on a coarse grid, smoothly interpolated.
            float gx = tile.x / (float)PatchTiles, gy = tile.y / (float)PatchTiles;
            int x0 = Mathf.FloorToInt(gx), y0 = Mathf.FloorToInt(gy);
            float fx = Smooth(gx - x0), fy = Smooth(gy - y0);
            float a = Noise(salt, x0, y0), b = Noise(salt, x0 + 1, y0), c = Noise(salt, x0, y0 + 1), d = Noise(salt, x0 + 1, y0 + 1);
            float patch = Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy); // -1..1
            // An odd tile picked out a little more (about one in nine).
            float speck = Noise(salt ^ 0x5bd1e995, tile.x, tile.y);
            float pick = Mathf.Abs(speck) > 0.78f ? Mathf.Sign(speck) * 0.6f : 0f;
            float shade = Mathf.Clamp(patch * 0.7f + pick, -1f, 1f);
            if (shade > 0f) shade *= LightenScale;
            return shade * MaxShade * Strength(theme);
        }

        /// <summary>The overlay sprite for one screen of a theme's floor: one texel per floor tile, cached per screen.</summary>
        public static Sprite For(Theme theme, ScreenAddress screen)
        {
            if (Cache.TryGetValue((theme, screen), out Sprite cached) && cached != null) return cached;
            Vector2Int size = Vector2Int.RoundToInt(ScreenMath.DefaultScreenSize);
            var texture = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = $"Floor pattern {theme} {screen}",
            };
            var pixels = new Color32[size.x * size.y];
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    float s = Shade(theme, screen, new Vector2Int(x, y));
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Abs(s) * 255f);
                    pixels[y * size.x + x] = s >= 0f ? new Color32(255, 255, 255, alpha) : new Color32(0, 0, 0, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            // The floor block is FloorFraction of the screen and its tiles start at its bottom-left corner (one per unit):
            // the sprite covers exactly the block, so texel (x, y) lies on floor tile (x, y) (the last row and column are
            // cut short, like the tiles).
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size.x * FloorFraction, size.y * FloorFraction),
                new Vector2(0.5f, 0.5f), 1f);
            sprite.name = texture.name;
            Cache[(theme, screen)] = sprite;
            return sprite;
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        /// <summary>A hash of (salt, x, y) as a value in [-1, 1].</summary>
        private static float Noise(int salt, int x, int y)
        {
            unchecked
            {
                uint h = (uint)salt * 0x9E3779B1u ^ (uint)x * 0x85EBCA77u ^ (uint)y * 0xC2B2AE3Du;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                return (h & 0xFFFF) / 32767.5f - 1f;
            }
        }
    }
}
