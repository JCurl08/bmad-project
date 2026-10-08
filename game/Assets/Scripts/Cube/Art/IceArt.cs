using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The ice screens' tiles (4.10), drawn in code as 16-pixel tiles (one unit each, point filtered, like InteriorArt): every
    /// rule shows on its tile. Ice is pale blue with a glint and a faint seam at each tile's edge, so the whole-tile steps
    /// read; a rock is a grey boulder standing on ice; a pit is a dark hole with a pale rim; grip is crunchy off-white snow
    /// with grit (the landings and the camp too, the camp's centre with a small fire); the goal pad is grip ringed in gold;
    /// snowbanks are drifts with a blue shadow.
    /// </summary>
    public static class IceArt
    {
        public const int TilePixels = 16;
        public const float PixelsPerUnit = TilePixels;

        public static readonly Color IceLight = new Color(0.80f, 0.92f, 0.99f), IceMid = new Color(0.66f, 0.84f, 0.96f);
        public static readonly Color IceSeam = new Color(0.56f, 0.76f, 0.92f), Glint = new Color(0.97f, 1f, 1f);
        public static readonly Color Snow = new Color(0.95f, 0.94f, 0.9f), Grit = new Color(0.62f, 0.55f, 0.45f), GritLight = new Color(0.8f, 0.76f, 0.68f);
        public static readonly Color RockLight = new Color(0.62f, 0.62f, 0.66f), RockDark = new Color(0.38f, 0.38f, 0.43f), Outline = new Color(0.2f, 0.2f, 0.26f);
        public static readonly Color PitDeep = new Color(0.05f, 0.08f, 0.2f), PitWall = new Color(0.16f, 0.26f, 0.48f), PitRim = new Color(0.9f, 0.97f, 1f);
        public static readonly Color Gold = new Color(1f, 0.8f, 0.2f), GoldDark = new Color(0.75f, 0.5f, 0.1f);
        public static readonly Color Drift = new Color(0.97f, 0.98f, 1f), DriftShadow = new Color(0.78f, 0.85f, 0.95f);
        public static readonly Color Fire = new Color(1f, 0.55f, 0.15f), FireCore = new Color(1f, 0.9f, 0.35f), Log = new Color(0.45f, 0.28f, 0.14f);

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Ice => Tile("Ice", IcePixels());
        public static Sprite Rock => Tile("Ice Rock", RockPixels());
        public static Sprite Pit => Tile("Ice Pit", PitPixels());
        public static Sprite Grip => Tile("Ice Grip", GripPixels(false));
        public static Sprite Goal => Tile("Ice Goal", GoalPixels());
        public static Sprite Bank => Tile("Ice Bank", BankPixels());
        public static Sprite CampFire => Tile("Ice Camp Fire", GripPixels(true));

        /// <summary>The sprite of a tile (the camp's centre gets its fire).</summary>
        public static Sprite For(IceTile tile, bool campCentre = false)
        {
            switch (tile)
            {
                case IceTile.Rock: return Rock;
                case IceTile.Pit: return Pit;
                case IceTile.Grip:
                case IceTile.Landing: return Grip;
                case IceTile.Camp: return campCentre ? CampFire : Grip;
                case IceTile.Goal: return Goal;
                case IceTile.Bank: return Bank;
                default: return Ice;
            }
        }

        private static uint Hash(int x, int y, int salt) => (uint)(x * 73856093 ^ y * 19349663 ^ salt * 83492791) % 1000u;

        private static Color32[] IcePixels()
        {
            var px = new Color32[TilePixels * TilePixels];
            for (int y = 0; y < TilePixels; y++)
                for (int x = 0; x < TilePixels; x++)
                {
                    Color c = Color.Lerp(IceMid, IceLight, (x + y) / 30f);
                    if (x == 0 || y == 0) c = IceSeam;
                    int d = x - y;
                    if ((d == 5 || d == 6) && x > 6 && x < 13) c = Glint; // a glint: slippery
                    if (d == -6 && x > 2 && x < 6) c = Glint;
                    px[y * TilePixels + x] = c;
                }
            return px;
        }

        private static Color32[] RockPixels()
        {
            Color32[] px = IcePixels();
            for (int y = 0; y < TilePixels; y++)
                for (int x = 0; x < TilePixels; x++)
                {
                    float dx = (x - 7.5f) / 6.8f, dy = (y - 6.8f) / 6.0f;
                    float r = dx * dx + dy * dy;
                    if (r > 1f) continue;
                    Color c = r > 0.78f ? Outline : (dx - dy < -0.3f ? RockLight : RockDark);
                    if (r < 0.78f && Hash(x, y, 3) < 90) c = Color.Lerp(c, Outline, 0.35f);
                    if (r < 0.2f && dx < 0 && dy > 0) c = Color.Lerp(RockLight, Color.white, 0.35f);
                    px[y * TilePixels + x] = c;
                }
            return px;
        }

        private static Color32[] PitPixels()
        {
            Color32[] px = IcePixels();
            for (int y = 0; y < TilePixels; y++)
                for (int x = 0; x < TilePixels; x++)
                {
                    float dx = (x - 7.5f) / 7.0f, dy = (y - 7.5f) / 6.6f;
                    float r = dx * dx + dy * dy;
                    if (r > 1f) continue;
                    Color c = r > 0.8f ? PitRim : r > 0.5f || dy > 0.35f ? PitWall : PitDeep;
                    if (r > 0.8f && Hash(x, y, 5) < 300) c = Color.Lerp(PitRim, IceSeam, 0.5f); // a jagged rim
                    px[y * TilePixels + x] = c;
                }
            return px;
        }

        private static Color32[] GripPixels(bool fire)
        {
            var px = new Color32[TilePixels * TilePixels];
            for (int y = 0; y < TilePixels; y++)
                for (int x = 0; x < TilePixels; x++)
                {
                    uint h = Hash(x, y, 1);
                    Color c = h < 70 ? Grit : h < 160 ? GritLight : Snow;
                    if (x == 0 || y == 0) c = Color.Lerp(Snow, GritLight, 0.6f);
                    px[y * TilePixels + x] = c;
                }
            if (fire)
            {
                for (int x = 3; x <= 12; x++)
                {
                    px[3 * TilePixels + x] = Log;
                    px[4 * TilePixels + (15 - x)] = Log;
                }
                for (int y = 5; y <= 12; y++)
                    for (int x = 4; x <= 11; x++)
                    {
                        float dx = (x - 7.5f) / (3.6f - (y - 5) * 0.4f), dy = (y - 5) / 8f;
                        if (Mathf.Abs(dx) > 1f) continue;
                        px[y * TilePixels + x] = Mathf.Abs(dx) < 0.45f && dy < 0.6f ? FireCore : Fire;
                    }
            }
            return px;
        }

        private static Color32[] GoalPixels()
        {
            Color32[] px = GripPixels(false);
            for (int y = 0; y < TilePixels; y++)
                for (int x = 0; x < TilePixels; x++)
                {
                    float dx = x - 7.5f, dy = y - 7.5f, r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r >= 5.6f && r < 7.2f) px[y * TilePixels + x] = r < 6.3f ? Gold : GoldDark;
                }
            return px;
        }

        private static Color32[] BankPixels()
        {
            var px = new Color32[TilePixels * TilePixels];
            for (int y = 0; y < TilePixels; y++)
                for (int x = 0; x < TilePixels; x++)
                {
                    // Soft drifts: a wavy shadow line on the lower part of the tile.
                    float wave = 4f + 1.5f * Mathf.Sin(x * 0.8f);
                    Color c = y < wave ? DriftShadow : Drift;
                    if (Hash(x, y, 7) < 40) c = Color.Lerp(c, DriftShadow, 0.5f);
                    px[y * TilePixels + x] = c;
                }
            return px;
        }

        private static Sprite Tile(string key, Color32[] pixels)
        {
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            var texture = new Texture2D(TilePixels, TilePixels, TextureFormat.RGBA32, false)
            {
                name = key,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, TilePixels, TilePixels), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0,
                SpriteMeshType.FullRect);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }
    }
}
