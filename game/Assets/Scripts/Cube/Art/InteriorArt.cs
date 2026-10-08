using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The town interiors' art (4.6), drawn in code like the towns' (TownArt): Kenney's palette, the people's two-pixel
    /// plum outline round every piece of furniture, point filtering, at the people's pixel size (TownLayout.PixelsPerUnit,
    /// 8 a unit). Each race furnishes its room in its own colours (PaletteOf): the Town's plank floor and plaster walls,
    /// the finches' woven straw, the mushroom folk's soft lilac moss and spotted stalk walls, the aliens' metal plates and
    /// lit panels, the shape people's chequered tiles, the dinosaurs' flagstones and log walls; and its own keepsake in a
    /// corner. Floors and walls are tiles drawn tiled; furniture is drawn at its rect's size. Made once and cached.
    /// </summary>
    public static class InteriorArt
    {
        /// <summary>Pixels a unit: the people's.</summary>
        public static float PixelsPerUnit => TownLayout.PixelsPerUnit;

        /// <summary>A floor or wall tile's side in pixels (two units).</summary>
        public const int TilePixels = 16;

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        private static readonly Color32 O = Hex("3f2631"), W = Hex("ffffff"),
            Br = Hex("bd6c4a"), Db = Hex("763b36"), Cr = Hex("ead4aa"), Pe = Hex("e8b796"), Pk = Hex("f6757a"),
            Rd = Hex("e43b44"), DRd = Hex("a22633"), Or = Hex("f77622"), Gs = Hex("feae34"), Gd = Hex("fee761"),
            Gr = Hex("63c74d"), DGr = Hex("3e8948"), Mi = Hex("43e1b3"), Ja = Hex("25956a"), Cy = Hex("2ce8f5"),
            Bl = Hex("0099db"), DBl = Hex("124e89"), Pg = Hex("c0cbdc"), Gy = Hex("8b9bb4"), Sl = Hex("5a6988"),
            DSl = Hex("3a4466"), Li = Hex("d176d0"), Vi = Hex("9b4ca3");

        /// <summary>A race's furnishing colours.</summary>
        public readonly struct Palette
        {
            public readonly Color32 Floor, FloorDark, Wall, WallDark, Wood, WoodDark, Cloth, ClothDark, Accent;

            public Palette(Color32 floor, Color32 floorDark, Color32 wall, Color32 wallDark, Color32 wood, Color32 woodDark,
                Color32 cloth, Color32 clothDark, Color32 accent)
            {
                Floor = floor;
                FloorDark = floorDark;
                Wall = wall;
                WallDark = wallDark;
                Wood = wood;
                WoodDark = woodDark;
                Cloth = cloth;
                ClothDark = clothDark;
                Accent = accent;
            }
        }

        /// <summary>Each race's colours (its town's theme).</summary>
        public static Palette PaletteOf(Theme theme)
        {
            switch (theme)
            {
                case Theme.Biology: return new Palette(Gs, Br, Br, Db, Br, Db, Gr, DGr, Pk);
                case Theme.Chemistry: return new Palette(Li, Vi, Cr, Pe, Pe, Br, Li, Vi, Mi);
                case Theme.Physics: return new Palette(Pg, Gy, Sl, DSl, Gy, Sl, Bl, DBl, Cy);
                case Theme.Math: return new Palette(Pg, Li, Bl, DBl, Li, Vi, Gd, Gs, Rd);
                case Theme.EarthAtmosphere: return new Palette(Gy, Sl, Br, Db, Br, Db, DRd, Rd, Or);
                default: return new Palette(Br, Db, Cr, Pe, Br, Db, Rd, DRd, Gd);
            }
        }

        // ------------------------------------------------------------------ floors and walls (tiles)

        /// <summary>A race's floor tile (TilePixels square, two units), for tiled drawing.</summary>
        public static Sprite Floor(Theme theme) => Tile($"Interior Floor {theme}", FloorPixels(theme));

        /// <summary>A race's wall tile (TilePixels square), for tiled drawing.</summary>
        public static Sprite Wall(Theme theme) => Tile($"Interior Wall {theme}", WallPixels(theme));

        /// <summary>A floor tile's pixels (tests read them).</summary>
        public static Color32[] FloorPixels(Theme theme)
        {
            Palette p = PaletteOf(theme);
            const int n = TilePixels;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Color32 c = p.Floor;
                switch (theme)
                {
                    case Theme.Biology: // woven straw: a basket weave of light and dark strands
                        c = ((x / 4 + y / 4) % 2 == 0 ? y % 4 == 3 : x % 4 == 3) ? p.FloorDark : p.Floor;
                        break;
                    case Theme.Chemistry: // soft moss: lilac with darker tufts
                        c = (x * 7 + y * 13) % 11 == 0 || (x * 5 + y * 3) % 17 == 0 ? p.FloorDark : p.Floor;
                        break;
                    case Theme.Physics: // metal plates: seams every 8 pixels, a rivet in each corner
                        c = x % 8 == 7 || y % 8 == 7 ? p.FloorDark : (x % 8 == 1 && y % 8 == 1) ? Sl : p.Floor;
                        break;
                    case Theme.Math: // chequered tiles
                        c = (x / 8 + y / 8) % 2 == 0 ? p.Floor : p.FloorDark;
                        break;
                    case Theme.EarthAtmosphere: // flagstones, offset course by course
                    {
                        int row = y / 8, shift = row % 2 == 0 ? 0 : 5;
                        c = y % 8 == 7 || (x + shift) % 10 == 9 ? p.FloorDark : p.Floor;
                        break;
                    }
                    default: // the Town's planks: boards four pixels high, their joints staggered
                    {
                        int board = y / 4, shift = board * 5 % 16;
                        c = y % 4 == 3 || (x + shift) % 16 == 15 ? p.FloorDark : p.Floor;
                        break;
                    }
                }
                px[y * n + x] = c;
            }
            return px;
        }

        /// <summary>A wall tile's pixels (tests read them).</summary>
        public static Color32[] WallPixels(Theme theme)
        {
            Palette p = PaletteOf(theme);
            const int n = TilePixels;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Color32 c = p.Wall;
                switch (theme)
                {
                    case Theme.Biology: // woven twigs
                        c = (x + y) % 4 == 0 || (x - y + 32) % 8 == 0 ? p.WallDark : p.Wall;
                        break;
                    case Theme.Chemistry: // the stalk's cream, with red spots
                    {
                        float d1 = (x - 4) * (x - 4) + (y - 11) * (y - 11), d2 = (x - 12) * (x - 12) + (y - 4) * (y - 4);
                        c = d1 <= 4f || d2 <= 2f ? Rd : y % 8 == 0 ? p.WallDark : p.Wall;
                        break;
                    }
                    case Theme.Physics: // panels with a lit dot
                        c = x % 8 == 0 || y == 0 ? p.WallDark : (x % 8 == 4 && y == 10) ? Cy : p.Wall;
                        break;
                    case Theme.Math: // square tiles
                        c = x % 8 == 0 || y % 8 == 0 ? p.WallDark : p.Wall;
                        break;
                    case Theme.EarthAtmosphere: // logs, their round ends at the joins
                        c = y % 4 == 0 ? p.WallDark : (x == 15 && y % 4 == 2) ? Pe : p.Wall;
                        break;
                    default: // plaster with a timber beam
                        c = y % 8 == 0 ? Db : (x * 3 + y * 7) % 13 == 0 ? p.WallDark : p.Wall;
                        break;
                }
                px[y * n + x] = c;
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

        // ------------------------------------------------------------------ furniture

        /// <summary>
        /// A piece of furniture in a race's style, drawn for an art rect of the given size (units): a canvas of that many
        /// pixels (8 a unit), the figure inside a two-pixel margin, the outline round it. Cached per piece, race and size.
        /// </summary>
        public static Sprite Piece(InteriorPiece piece, Theme theme, Vector2 size)
        {
            string key = $"Interior {piece} {theme} {size.x:0.##}x{size.y:0.##}";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            Sprite sprite = PieceCanvas(piece, theme, size).ToSprite(key);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>A piece's finished pixels (tests read them).</summary>
        public static TownArt.Canvas PieceCanvas(InteriorPiece piece, Theme theme, Vector2 size)
        {
            int cw = Mathf.Max(8, Mathf.RoundToInt(size.x * PixelsPerUnit)), ch = Mathf.Max(8, Mathf.RoundToInt(size.y * PixelsPerUnit));
            var c = new TownArt.Canvas(cw, ch);
            int w = cw - 2 * TownArt.Margin, h = ch - 2 * TownArt.Margin;
            Palette p = PaletteOf(theme);
            switch (piece)
            {
                case InteriorPiece.Counter: Counter(c, w, h, p, theme); break;
                case InteriorPiece.Shelf: Shelf(c, w, h, p, theme); break;
                case InteriorPiece.Throne: Throne(c, w, h, p); break;
                case InteriorPiece.Banner: Banner(c, w, h, p); break;
                case InteriorPiece.Pillar: Pillar(c, w, h, p); break;
                case InteriorPiece.Bed: Bed(c, w, h, p, theme); break;
                case InteriorPiece.Table: Table(c, w, h, p); break;
                case InteriorPiece.Stool: Stool(c, w, h, p); break;
                case InteriorPiece.Hearth: Hearth(c, w, h, p); break;
                case InteriorPiece.Rug: Rug(c, w, h, p, false); break;
                case InteriorPiece.Carpet: Rug(c, w, h, p, true); break;
                default: Keepsake(c, w, h, p, theme); break;
            }
            c.Outline();
            return c;
        }

        private static void Counter(TownArt.Canvas c, int w, int h, Palette p, Theme theme)
        {
            // The front of the counter in planks, a lighter top, a trim in the accent; a few goods and a bell on top.
            int top = h - 1;
            c.Rect(0, 0, w - 1, top - 3, p.Wood);
            for (int x = 3; x < w - 1; x += 5) c.Rect(x, 0, x, top - 4, p.WoodDark);
            c.Rect(0, top - 3, w - 1, top - 3, p.Accent);
            c.Rect(0, top - 2, w - 1, top, Lighter(p.Wood));
            Color32[] goods = Goods(theme);
            for (int i = 0; i < 4; i++)
            {
                int x = 3 + i * (w - 6) / 4;
                c.Rect(x, top - 1, x + 2, top, goods[i % goods.Length]);
            }
            c.Ellipse(w - 5, top - 0.5f, 1.5f, 1.2f, Gd);
        }

        private static void Shelf(TownArt.Canvas c, int w, int h, Palette p, Theme theme)
        {
            // A dark frame with three boards, each lined with the race's wares.
            c.Rect(0, 0, w - 1, h - 1, p.WoodDark);
            c.Rect(1, 1, w - 2, h - 2, Darker(p.Wood));
            Color32[] goods = Goods(theme);
            int boards = 3, step = Mathf.Max(3, (h - 1) / boards);
            for (int b = 0; b < boards; b++)
            {
                int y = 1 + b * step;
                c.Rect(1, y, w - 2, y, p.Wood);
                for (int x = 2, i = b; x < w - 3; x += 4, i++)
                {
                    int tall = 1 + (i * 7 + b) % Mathf.Max(1, step - 2);
                    c.Rect(x, y + 1, x + 1, Mathf.Min(y + tall, y + step - 1), goods[i % goods.Length]);
                }
            }
        }

        private static void Throne(TownArt.Canvas c, int w, int h, Palette p)
        {
            // A two-step dais under a tall-backed throne in the race's cloth, gold-trimmed, a crown motif on its back.
            int dais = h / 3;
            c.Rect(0, 0, w - 1, dais / 2, p.WoodDark);
            c.Rect(2, dais / 2 + 1, w - 3, dais, p.Wood);
            int x0 = w / 2 - w / 5, x1 = w / 2 + w / 5;
            c.Rect(x0, dais + 1, x1, h - 1, p.ClothDark);
            c.Rect(x0 + 1, dais + 1, x1 - 1, h - 3, p.Cloth);
            c.Rect(x0 - 2, dais + 1, x0, dais + (h - dais) / 3, Gd);
            c.Rect(x1, dais + 1, x1 + 2, dais + (h - dais) / 3, Gd);
            c.Rect(x0, dais + (h - dais) / 3, x1, dais + (h - dais) / 3, Gd);
            int cx = (x0 + x1) / 2, cy = h - 6;
            c.Rect(cx - 2, cy, cx + 2, cy + 1, Gd);
            c.Px(cx - 2, cy + 2, Gd); c.Px(cx, cy + 2, Gd); c.Px(cx + 2, cy + 2, Gd);
        }

        private static void Banner(TownArt.Canvas c, int w, int h, Palette p)
        {
            // A rod with a hanging cloth, a swallowtail at the bottom and the accent's round emblem.
            c.Rect(0, h - 1, w - 1, h - 1, Gd);
            for (int y = 0; y < h - 1; y++)
            {
                int notch = y < 3 ? 3 - y : 0;
                for (int x = 1; x < w - 1; x++)
                    if (Mathf.Abs(x - (w - 1) / 2f) >= notch) c.Px(x, y, p.Cloth);
            }
            c.Rect(1, 3, 1, h - 2, p.ClothDark);
            c.Ellipse((w - 1) / 2f, h * 0.6f, 2.2f, 2.2f, p.Accent);
        }

        private static void Pillar(TownArt.Canvas c, int w, int h, Palette p)
        {
            // A base, a fluted shaft and a capital, in the walls' colours.
            c.Rect(0, 0, w - 1, 2, p.WallDark);
            c.Rect(1, 3, w - 2, h - 4, p.Wall);
            for (int x = 2; x < w - 2; x += 2) c.Rect(x, 3, x, h - 4, p.WallDark);
            c.Rect(0, h - 3, w - 1, h - 1, p.WallDark);
        }

        private static void Bed(TownArt.Canvas c, int w, int h, Palette p, Theme theme)
        {
            if (theme == Theme.Biology)
            {
                // A nest bed: a woven bowl with a soft green blanket and a feather.
                c.Ellipse((w - 1) / 2f, h * 0.45f, w / 2f, h * 0.42f, Br);
                for (int x = 0; x < w; x += 3) c.Px(x, h / 4, Db);
                c.Ellipse((w - 1) / 2f, h * 0.55f, w / 2f - 3, h * 0.25f, p.Cloth);
                c.Rect(w / 2, h - 5, w / 2, h - 2, Pk);
                return;
            }
            // A headboard at the top, the frame, a pillow and the blanket in the race's cloth.
            c.Rect(0, h - 4, w - 1, h - 1, p.WoodDark);
            c.Rect(1, 0, w - 2, h - 5, p.Wood);
            c.Rect(3, h - 9, w - 4, h - 5, W);
            c.Rect(2, 1, w - 3, h - 10, p.Cloth);
            for (int y = 3; y < h - 10; y += 4) c.Rect(2, y, w - 3, y, p.ClothDark);
        }

        private static void Table(TownArt.Canvas c, int w, int h, Palette p)
        {
            // Legs under a top, with a cup and a bowl on it.
            c.Rect(1, 0, 2, h / 2, p.WoodDark);
            c.Rect(w - 3, 0, w - 2, h / 2, p.WoodDark);
            c.Rect(0, h / 2, w - 1, h - 1, p.Wood);
            c.Rect(0, h / 2, w - 1, h / 2, p.WoodDark);
            c.Rect(w / 4, h - 3, w / 4 + 1, h - 1, W);
            c.Ellipse(w * 0.65f, h - 2, 2.5f, 1.2f, p.Accent);
        }

        private static void Stool(TownArt.Canvas c, int w, int h, Palette p)
        {
            c.Rect(1, 0, 1, h / 2, p.WoodDark);
            c.Rect(w - 2, 0, w - 2, h / 2, p.WoodDark);
            c.Ellipse((w - 1) / 2f, h * 0.65f, w / 2f, h * 0.3f, p.Wood);
        }

        private static void Hearth(TownArt.Canvas c, int w, int h, Palette p)
        {
            // A stone hearth with a mantel, a fire burning inside it.
            c.Rect(0, 0, w - 1, h - 3, Gy);
            for (int y = 2; y < h - 3; y += 3)
                for (int x = (y / 3) % 2 * 2; x < w; x += 4)
                    c.Px(x, y, Sl);
            c.Rect(0, h - 3, w - 1, h - 1, p.WoodDark);
            int x0 = w / 4, x1 = w - 1 - w / 4, top = h * 2 / 3;
            c.Rect(x0, 0, x1, top, O);
            c.Ellipse((x0 + x1) / 2f, 2, (x1 - x0) / 2f - 1, top * 0.6f, Rd, 0);
            c.Ellipse((x0 + x1) / 2f, 1, (x1 - x0) / 3f, top * 0.4f, Or, 0);
            c.Ellipse((x0 + x1) / 2f, 1, (x1 - x0) / 6f, top * 0.25f, Gd, 0);
        }

        private static void Rug(TownArt.Canvas c, int w, int h, Palette p, bool runner)
        {
            // A border in the dark cloth round the cloth, the accent in a pattern down the middle.
            c.Rect(0, 0, w - 1, h - 1, p.ClothDark);
            c.Rect(1, 1, w - 2, h - 2, p.Cloth);
            if (runner)
            {
                for (int y = 3; y < h - 3; y += 5) c.Rect(w / 2 - 1, y, w / 2, y + 1, p.Accent);
                c.Rect(2, 1, 2, h - 2, p.Accent);
                c.Rect(w - 3, 1, w - 3, h - 2, p.Accent);
                return;
            }
            for (int x = 3; x < w - 3; x += 4)
            {
                c.Px(x, h / 2, p.Accent);
                c.Px(x + 1, h / 2 + 1, p.Accent);
                c.Px(x + 1, h / 2 - 1, p.Accent);
            }
        }

        private static void Keepsake(TownArt.Canvas c, int w, int h, Palette p, Theme theme)
        {
            float cx = (w - 1) / 2f;
            switch (theme)
            {
                case Theme.Biology:
                    // A twig perch with a basket of seeds.
                    c.Rect((int)cx, 0, (int)cx + 1, h - 3, Db);
                    c.Rect(1, h - 4, w - 2, h - 3, Br);
                    c.Ellipse(cx, 3, w / 3f, 3, Br);
                    c.Ellipse(cx, 5, w / 3f - 1, 1.2f, Gs);
                    c.Px((int)cx - 2, 6, Cr); c.Px((int)cx + 2, 6, Cr);
                    break;
                case Theme.Chemistry:
                    // A big round pot on short legs, bubbling over in mint.
                    c.Rect(3, 0, 4, 2, Sl);
                    c.Rect(w - 5, 0, w - 4, 2, Sl);
                    c.Ellipse(cx, h * 0.38f, w / 2f - 1, h * 0.33f, DSl);
                    c.Rect(1, (int)(h * 0.62f), w - 2, (int)(h * 0.62f) + 1, Sl);
                    c.Ellipse(cx, h * 0.7f, w / 2f - 3, 1.5f, Mi);
                    c.Ellipse(cx - 3, h * 0.82f, 1.6f, 1.6f, Mi);
                    c.Ellipse(cx + 2, h * 0.92f, 1.2f, 1.2f, Ja);
                    break;
                case Theme.Physics:
                    // A telescope on a tripod, pointing at the ceiling.
                    c.Line(2, 0, (int)cx, h / 2, Sl);
                    c.Line(w - 3, 0, (int)cx, h / 2, Sl);
                    c.Rect((int)cx, 0, (int)cx, h / 2, Sl);
                    for (int i = 0; i < h / 2; i++)
                    {
                        c.Px((int)cx - 2 + i / 2, h / 2 + i, Bl);
                        c.Px((int)cx - 1 + i / 2, h / 2 + i, DBl);
                    }
                    c.Px((int)cx - 2 + (h / 2 - 1) / 2, h - 1, Cy);
                    break;
                case Theme.Math:
                    // A cube, a ball and a pyramid stacked: the shape people's own motifs.
                    c.Rect(1, 0, w - 2, h / 3, Li);
                    c.Rect(w * 2 / 3, 0, w - 2, h / 3, Vi);
                    c.Ellipse(cx, h / 3f + h / 6f, w / 4f, h / 6f, Bl);
                    c.Triangle((int)(cx - w / 4f), (int)(cx + w / 4f), h * 2 / 3, h - 1, Gd);
                    break;
                case Theme.EarthAtmosphere:
                    // A ring of stones round a little campfire, a bone laid by.
                    c.Ellipse(cx, h * 0.3f, w / 2f - 1, h * 0.28f, Gy);
                    c.Ellipse(cx, h * 0.32f, w / 2f - 3, h * 0.18f, O);
                    c.Ellipse(cx, h * 0.35f, w / 5f, h * 0.35f, Or, (int)(h * 0.3f));
                    c.Ellipse(cx, h * 0.38f, w / 9f, h * 0.22f, Gd, (int)(h * 0.3f));
                    c.Rect(2, h - 3, w / 2, h - 3, Cr);
                    c.Px(1, h - 2, Cr); c.Px(1, h - 4, Cr);
                    break;
                default:
                    // A barrel beside a crate.
                    c.Rect(0, 0, w / 2, h * 2 / 3, Br);
                    c.Rect(0, h / 3, w / 2, h / 3, Db);
                    c.Line(0, 0, w / 2, h * 2 / 3, Db);
                    c.Ellipse(w * 0.72f, h * 0.45f, w / 4.5f, h * 0.45f, Br);
                    c.Rect(w / 2 + 1, h / 4, w - 1, h / 4, Sl);
                    c.Rect(w / 2 + 1, h * 3 / 4, w - 1, h * 3 / 4, Sl);
                    break;
            }
        }

        /// <summary>The colours of a race's wares on shelves and counters.</summary>
        private static Color32[] Goods(Theme theme)
        {
            switch (theme)
            {
                case Theme.Biology: return new[] { Gs, Cr, Pk, Br };
                case Theme.Chemistry: return new[] { Mi, Cy, Pk, Gd, Li };
                case Theme.Physics: return new[] { Cy, Pg, Bl, Gd };
                case Theme.Math: return new[] { Rd, Bl, Gd, Li };
                case Theme.EarthAtmosphere: return new[] { Cr, Gy, Or, Br };
                default: return new[] { Rd, Gd, Gr, Bl, Cr };
            }
        }

        private static Color32 Lighter(Color32 c) => Color32.Lerp(c, W, 0.3f);

        private static Color32 Darker(Color32 c) => Color32.Lerp(c, O, 0.35f);

        private static Color32 Hex(string hex) => new Color32(
            Convert.ToByte(hex.Substring(0, 2), 16), Convert.ToByte(hex.Substring(2, 2), 16), Convert.ToByte(hex.Substring(4, 2), 16), 255);
    }
}
