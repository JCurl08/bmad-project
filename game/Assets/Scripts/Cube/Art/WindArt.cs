using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The wind screens' tiles (4.11), drawn in code as 16-pixel tiles (one unit each, point filtered, like IceArt): every rule
    /// shows on its tile. The sky is a deep blue with soft cloud wisps, and the sky just below land shows the island's rocky
    /// underside, so the islands read as floating; the ring path round the screen is pale flagstones; islands are grass; a wall
    /// is a grey rock on grass; a stream tile is a lighter blue lane with a white chevron (WindScreen turns it to the stream's
    /// way), and animated white streaks run along every stream (WindScreen's flow); a grip anchor is a small floating rock with
    /// grit; the goal pad is grass ringed in gold; the cliffs round the grid are dark stone.
    /// </summary>
    public static class WindArt
    {
        public const int TilePixels = 16;
        public const float PixelsPerUnit = TilePixels;

        public static readonly Color SkyTop = new Color(0.42f, 0.66f, 0.93f), SkyLow = new Color(0.36f, 0.58f, 0.88f);
        public static readonly Color Cloud = new Color(0.93f, 0.96f, 1f), CloudShade = new Color(0.78f, 0.86f, 0.97f);
        public static readonly Color Lane = new Color(0.62f, 0.82f, 0.98f), LaneEdge = new Color(0.52f, 0.74f, 0.96f);
        public static readonly Color Arrow = new Color(1f, 1f, 1f), ArrowShade = new Color(0.82f, 0.9f, 1f);
        public static readonly Color Grass = new Color(0.45f, 0.72f, 0.33f), GrassDark = new Color(0.34f, 0.58f, 0.25f), GrassLight = new Color(0.6f, 0.82f, 0.42f);
        public static readonly Color Soil = new Color(0.52f, 0.36f, 0.22f), SoilDark = new Color(0.36f, 0.24f, 0.15f);
        public static readonly Color Flag = new Color(0.82f, 0.78f, 0.68f), FlagLine = new Color(0.62f, 0.58f, 0.5f);
        public static readonly Color RockLight = new Color(0.66f, 0.66f, 0.7f), RockDark = new Color(0.42f, 0.42f, 0.47f), Outline = new Color(0.22f, 0.22f, 0.27f);
        public static readonly Color Gold = new Color(1f, 0.8f, 0.2f), GoldDark = new Color(0.75f, 0.5f, 0.1f);
        public static readonly Color Cliff = new Color(0.36f, 0.33f, 0.31f), CliffLight = new Color(0.48f, 0.44f, 0.41f);
        public static readonly Color Streak = new Color(1f, 1f, 1f, 0.85f);

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Sky => Tile("Wind Sky", SkyPixels(false));
        public static Sprite SkyCloud => Tile("Wind Sky Cloud", SkyPixels(true));
        public static Sprite SkyUnder => Tile("Wind Sky Under Land", UnderPixels());
        public static Sprite Floor => Tile("Wind Grass", GrassPixels());
        public static Sprite Path => Tile("Wind Path", PathPixels());
        public static Sprite Wall => Tile("Wind Wall", WallPixels());
        public static Sprite Stream => Tile("Wind Stream", StreamPixels());
        public static Sprite Grip => Tile("Wind Grip", GripPixels());
        public static Sprite Goal => Tile("Wind Goal", GoalPixels());
        public static Sprite CliffTile => Tile("Wind Cliff", CliffPixels());
        public static Sprite StreakSprite => Tile("Wind Streak", StreakPixels(), 8, 2);

        /// <summary>
        /// The sprite of a cell of a layout: the ring path, an island's grass, a wall, a stream (pointing north: the screen turns
        /// it), grip, the goal, and the sky (its underside under land, else clear or a cloud wisp by the cell).
        /// </summary>
        public static Sprite For(WindLayout layout, int cell)
        {
            switch (layout.TileAt(cell))
            {
                case WindTile.Floor: return WindLayout.IsRing(cell) ? Path : Floor;
                case WindTile.Wall: return Wall;
                case WindTile.Stream: return Stream;
                case WindTile.Grip: return Grip;
                case WindTile.Goal: return Goal;
            }
            Vector2Int p = WindLayout.Pos(cell);
            var above = new Vector2Int(p.x, p.y + 1);
            if (WindLayout.InGrid(above))
            {
                WindTile t = layout.TileAt(WindLayout.Index(above));
                if ((t == WindTile.Floor || t == WindTile.Wall || t == WindTile.Goal) && !WindLayout.IsRing(WindLayout.Index(above))) return SkyUnder;
            }
            return Hash(p.x, p.y, 11) < 180 ? SkyCloud : Sky;
        }

        private static uint Hash(int x, int y, int salt) => (uint)(x * 73856093 ^ y * 19349663 ^ salt * 83492791) % 1000u;

        private static Color32[] SkyPixels(bool cloud)
        {
            var px = new Color32[TilePixels * TilePixels];
            for (int y = 0; y < TilePixels; y++)
                for (int x = 0; x < TilePixels; x++)
                {
                    Color c = Color.Lerp(SkyLow, SkyTop, y / 15f);
                    if (cloud)
                    {
                        float dx = (x - 7.5f) / 6.5f, dy = (y - 8f) / 2.6f;
                        float r = dx * dx + dy * dy;
                        if (r < 1f) c = Color.Lerp(Cloud, CloudShade, y < 7 ? 0.6f : 0.1f);
                        else if (r < 1.3f) c = Color.Lerp(c, Cloud, 0.35f);
                    }
                    px[y * TilePixels + x] = c;
                }
            return px;
        }

        private static Color32[] UnderPixels()
        {
            Color32[] px = SkyPixels(false);
            for (int x = 0; x < TilePixels; x++)
            {
                // The island's rocky underside hangs a few pixels into the sky below it, jagged.
                int depth = 4 + (int)(Hash(x, 0, 3) % 4);
                for (int y = TilePixels - depth; y < TilePixels; y++)
                    px[y * TilePixels + x] = y == TilePixels - depth ? SoilDark : (Hash(x, y, 9) < 250 ? SoilDark : Soil);
            }
            return px;
        }

        private static Color32[] GrassPixels()
        {
            var px = new Color32[TilePixels * TilePixels];
            for (int y = 0; y < TilePixels; y++)
                for (int x = 0; x < TilePixels; x++)
                {
                    uint h = Hash(x, y, 1);
                    Color c = h < 90 ? GrassDark : h < 160 ? GrassLight : Grass;
                    px[y * TilePixels + x] = c;
                }
            return px;
        }

        private static Color32[] PathPixels()
        {
            var px = new Color32[TilePixels * TilePixels];
            for (int y = 0; y < TilePixels; y++)
                for (int x = 0; x < TilePixels; x++)
                {
                    bool line = y == 0 || y == 8 || (y < 8 ? x == 5 : x == 12);
                    Color c = line ? FlagLine : (Hash(x, y, 2) < 80 ? Color.Lerp(Flag, FlagLine, 0.4f) : Flag);
                    px[y * TilePixels + x] = c;
                }
            return px;
        }

        private static Color32[] WallPixels()
        {
            Color32[] px = GrassPixels();
            for (int y = 0; y < TilePixels; y++)
                for (int x = 0; x < TilePixels; x++)
                {
                    float dx = (x - 7.5f) / 7.2f, dy = (y - 7f) / 6.8f;
                    float r = dx * dx + dy * dy;
                    if (r > 1f) continue;
                    Color c = r > 0.8f ? Outline : (dx - dy < -0.3f ? RockLight : RockDark);
                    if (r < 0.8f && Hash(x, y, 3) < 90) c = Color.Lerp(c, Outline, 0.35f);
                    px[y * TilePixels + x] = c;
                }
            return px;
        }

        private static Color32[] StreamPixels()
        {
            var px = new Color32[TilePixels * TilePixels];
            for (int y = 0; y < TilePixels; y++)
                for (int x = 0; x < TilePixels; x++)
                {
                    Color c = x <= 1 || x >= 14 ? LaneEdge : Lane;
                    // A chevron pointing up (north): two strokes meeting at the top.
                    int d = Mathf.Abs(x * 2 - 15);
                    int yTip = 12 - d / 2;
                    if (d <= 11 && (y == yTip || y == yTip - 1)) c = y == yTip ? Arrow : ArrowShade;
                    px[y * TilePixels + x] = c;
                }
            return px;
        }

        private static Color32[] GripPixels()
        {
            Color32[] px = StreamPixels();
            for (int y = 0; y < TilePixels; y++)
                for (int x = 0; x < TilePixels; x++)
                {
                    float dx = (x - 7.5f) / 7.4f, dy = (y - 8f) / 6.6f;
                    float r = dx * dx + dy * dy;
                    if (r > 1f) continue;
                    uint h = Hash(x, y, 4);
                    Color c = r > 0.82f ? Outline : h < 120 ? RockDark : h < 260 ? CliffLight : RockLight;
                    px[y * TilePixels + x] = c;
                }
            return px;
        }

        private static Color32[] GoalPixels()
        {
            Color32[] px = GrassPixels();
            for (int y = 0; y < TilePixels; y++)
                for (int x = 0; x < TilePixels; x++)
                {
                    float dx = x - 7.5f, dy = y - 7.5f, r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r >= 5.6f && r < 7.2f) px[y * TilePixels + x] = r < 6.3f ? Gold : GoldDark;
                }
            return px;
        }

        private static Color32[] CliffPixels()
        {
            var px = new Color32[TilePixels * TilePixels];
            for (int y = 0; y < TilePixels; y++)
                for (int x = 0; x < TilePixels; x++)
                {
                    Color c = Hash(x, y, 6) < 200 ? CliffLight : Cliff;
                    if ((x + y * 3) % 11 == 0) c = Outline;
                    px[y * TilePixels + x] = c;
                }
            return px;
        }

        private static Color32[] StreakPixels()
        {
            var px = new Color32[8 * 2];
            for (int y = 0; y < 2; y++)
                for (int x = 0; x < 8; x++)
                {
                    Color c = Streak;
                    c.a *= Mathf.Clamp01(x / 6f); // fades toward the tail (the streak flies toward +x)
                    px[y * 8 + x] = c;
                }
            return px;
        }

        private static Sprite Tile(string key, Color32[] pixels, int width = TilePixels, int height = TilePixels)
        {
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = key,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0,
                SpriteMeshType.FullRect);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }
    }
}
