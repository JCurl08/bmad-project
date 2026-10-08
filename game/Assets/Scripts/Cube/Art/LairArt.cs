using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The boss lair's art (4.13), drawn in code like the town interiors' (InteriorArt, TownArt): Kenney's palette, point
    /// filtering, 8 pixels a unit, the two-pixel plum outline round each piece. The lair is the Demon's: dark slate
    /// flagstones with a violet seam, dark brick walls, straw in the cells, iron bars, pillars, wall torches and a pink
    /// swirl in the vestibule (the way back up). Made once and cached.
    /// </summary>
    public static class LairArt
    {
        public static float PixelsPerUnit => TownLayout.PixelsPerUnit;

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        private static readonly Color32 O = Hex("3f2631"), Sl = Hex("5a6988"), DSl = Hex("3a4466"), Gy = Hex("8b9bb4"),
            Pg = Hex("c0cbdc"), Vi = Hex("9b4ca3"), Li = Hex("d176d0"), Gs = Hex("feae34"), Gd = Hex("fee761"), Or = Hex("f77622"),
            Rd = Hex("e43b44"), Br = Hex("bd6c4a"), Db = Hex("763b36"), Cr = Hex("ead4aa"), Pk = Hex("f6757a"),
            Brick = Hex("4b3d5e"), Night = Hex("2e3450");

        // The tiles' pixels, drawn once (4.13 review: the sprites were cached, but every call redrew the pixels first).
        private static Color32[] floorPixels, wallPixels, strawPixels;

        /// <summary>The hall's floor tile: dark slate flagstones, offset course by course, a violet seam here and there.</summary>
        public static Sprite Floor => Tile("Lair Floor", FloorPixels());

        /// <summary>The walls' tile: dark bricks.</summary>
        public static Sprite Wall => Tile("Lair Wall", WallPixels());

        /// <summary>The cells' floor tile: straw on slate.</summary>
        public static Sprite Straw => Tile("Lair Straw", StrawPixels());

        /// <summary>The floor tile's pixels (drawn once; tests read them).</summary>
        public static Color32[] FloorPixels() => floorPixels ??= DrawFloor();

        /// <summary>The wall tile's pixels (drawn once).</summary>
        public static Color32[] WallPixels() => wallPixels ??= DrawWall();

        /// <summary>The straw tile's pixels (drawn once).</summary>
        public static Color32[] StrawPixels() => strawPixels ??= DrawStraw();

        private static Color32[] DrawFloor()
        {
            const int n = InteriorArt.TilePixels;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int row = y / 8, shift = row % 2 == 0 ? 0 : 4;
                bool seam = y % 8 == 7 || (x + shift) % 8 == 7;
                px[y * n + x] = seam ? ((x * 3 + y) % 11 == 0 ? Vi : O) : (x + y) % 7 == 0 ? Sl : DSl;
            }
            return px;
        }

        private static Color32[] DrawWall()
        {
            const int n = InteriorArt.TilePixels;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int course = y / 4, shift = course % 2 == 0 ? 0 : 4;
                bool mortar = y % 4 == 3 || (x + shift) % 8 == 7;
                px[y * n + x] = mortar ? O : (x + 2 * y) % 9 == 0 ? Sl : Brick;
            }
            return px;
        }

        private static Color32[] DrawStraw()
        {
            const int n = InteriorArt.TilePixels;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                bool strand = (x * 5 + y * 3) % 17 == 0 || (x * 3 + y * 7) % 23 == 0;
                px[y * n + x] = strand ? ((x + y) % 3 == 0 ? Gs : Br) : (x * 7 + y * 5) % 19 == 0 ? Sl : Night;
            }
            return px;
        }

        /// <summary>A cell's iron bars across a front this many units wide (and the bars' drawn height above the solid).</summary>
        public static Sprite Bars(Vector2 size)
        {
            string key = $"Lair Bars {size.x:0.##}x{size.y:0.##}";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            Sprite sprite = BarsCanvas(size).ToSprite(key);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>A stone pillar this many units wide and tall.</summary>
        public static Sprite Pillar(Vector2 size) => Piece($"Lair Pillar {size.x:0.##}x{size.y:0.##}", size, (c, w, h) =>
        {
            c.Rect(0, 0, w - 1, 2, O);
            c.Rect(1, 3, w - 2, h - 4, Sl);
            for (int x = 2; x < w - 2; x += 3) c.Rect(x, 3, x, h - 4, DSl);
            c.Rect(0, h - 3, w - 1, h - 1, Vi);
        });

        /// <summary>A torch on the wall: a bracket and a flame.</summary>
        public static Sprite Torch(Vector2 size) => Piece($"Lair Torch {size.x:0.##}x{size.y:0.##}", size, (c, w, h) =>
        {
            c.Rect(w / 2 - 1, 0, w / 2, h / 2, Db);
            c.Rect(w / 2 - 2, h / 2 - 1, w / 2 + 1, h / 2, Br);
            c.Ellipse((w - 1) / 2f, h * 0.7f, w / 2f - 1, h * 0.28f, Rd);
            c.Ellipse((w - 1) / 2f, h * 0.66f, w / 3f, h * 0.18f, Or);
            c.Ellipse((w - 1) / 2f, h * 0.62f, w / 6f, h * 0.1f, Gd);
        });

        /// <summary>The swirl in the vestibule that takes the player back up (the portal they came down by).</summary>
        public static Sprite Swirl(Vector2 size) => Piece($"Lair Swirl {size.x:0.##}x{size.y:0.##}", size, (c, w, h) =>
        {
            c.Ellipse((w - 1) / 2f, (h - 1) / 2f, w / 2f, h / 2f, Vi);
            c.Ellipse((w - 1) / 2f, (h - 1) / 2f, w / 2f - 2, h / 2f - 2, Li);
            c.Ellipse((w - 1) / 2f, (h - 1) / 2f, w / 4f, h / 4f, Pk);
            c.Ellipse((w - 1) / 2f, (h - 1) / 2f, w / 8f, h / 8f, Cr);
        });

        private delegate void Draw(TownArt.Canvas c, int w, int h);

        private static Sprite Piece(string key, Vector2 size, Draw draw)
        {
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            TownArt.Canvas canvas = PieceCanvas(size, draw);
            Sprite sprite = canvas.ToSprite(key);
            Cache[key] = sprite;
            return sprite;
        }

        private static TownArt.Canvas PieceCanvas(Vector2 size, Draw draw)
        {
            int cw = Mathf.Max(8, Mathf.RoundToInt(size.x * PixelsPerUnit)), ch = Mathf.Max(8, Mathf.RoundToInt(size.y * PixelsPerUnit));
            var c = new TownArt.Canvas(cw, ch);
            draw(c, cw - 2 * TownArt.Margin, ch - 2 * TownArt.Margin);
            c.Outline();
            return c;
        }

        /// <summary>
        /// The bars' finished pixels (tests read them). They are not outlined round the whole figure (that would fill the gaps
        /// between them): each bar and rail carries its own plum edge, and the gaps stay see-through.
        /// </summary>
        /// <summary>Pixels from one bar to the next (each bar two pixels wide, so the rest see-through).</summary>
        public const int BarStep = 10;

        public static TownArt.Canvas BarsCanvas(Vector2 size)
        {
            int w = Mathf.Max(8, Mathf.RoundToInt(size.x * PixelsPerUnit)), h = Mathf.Max(8, Mathf.RoundToInt(size.y * PixelsPerUnit));
            var c = new TownArt.Canvas(w, h, 0, 0);
            c.Rect(0, 0, w - 1, 2, TownArt.OutlineColor);           // the sill, edged
            c.Rect(0, 1, w - 1, 1, Sl);
            c.Rect(0, h - 2, w - 1, h - 1, TownArt.OutlineColor);   // a thin top rail, edged
            c.Rect(0, h - 1, w - 1, h - 1, Gy);
            // 4.13 review: thin, sparse bars (a pale bar and its plum edge, eight see-through pixels between), so each
            // prisoner's whole design reads through them.
            for (int x = 3; x + 1 < w; x += BarStep)
            {
                c.Rect(x, 3, x, h - 3, Pg);
                c.Rect(x + 1, 3, x + 1, h - 3, TownArt.OutlineColor);
            }
            return c;
        }

        private static Sprite Tile(string key, Color32[] pixels)
        {
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            const int n = InteriorArt.TilePixels;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                name = key,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        private static Color32 Hex(string hex) => new Color32(
            System.Convert.ToByte(hex.Substring(0, 2), 16), System.Convert.ToByte(hex.Substring(2, 2), 16),
            System.Convert.ToByte(hex.Substring(4, 2), 16), 255);
    }
}
