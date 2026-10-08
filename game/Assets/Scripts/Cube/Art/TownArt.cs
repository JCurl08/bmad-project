using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The towns' art (4.5), drawn in code in the NPC designs' style (NpcDesignArt): Kenney's palette, a two-pixel dark plum
    /// outline added round every figure, point filtering, and the people's pixel size (TownLayout.PixelsPerUnit, 8 pixels a
    /// unit), so a building, a statue and a person read as one set. The Town's cottages are Kenney Tiny Town roof, wall and
    /// door tiles instead (TownSquareView lays them; the packs have nothing for the other races' styles). Sprites are made
    /// once per kind and cached.
    /// </summary>
    public static class TownArt
    {
        /// <summary>A building's figure in pixels (4 x 4 units), and the outline margin round it.</summary>
        public const int BuildingPixels = 32, Margin = 2;

        /// <summary>The statue's canvas in pixels: a pedestal under its leader's design, outline included.</summary>
        public const int StatueWidth = 20, StatueHeight = 24;

        /// <summary>The outline: the NPCs' dark plum.</summary>
        public static Color32 OutlineColor => NpcDesignArt.OutlineColor;

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        // Kenney's palette, as NpcDesignArt names it.
        private static readonly Color32 O = Hex("3f2631"), W = Hex("ffffff"),
            Br = Hex("bd6c4a"), Db = Hex("763b36"), Cr = Hex("ead4aa"), Pe = Hex("e8b796"), Pk = Hex("f6757a"),
            Rd = Hex("e43b44"), DRd = Hex("a22633"), Or = Hex("f77622"), Gs = Hex("feae34"), Gd = Hex("fee761"),
            Gr = Hex("63c74d"), DGr = Hex("3e8948"), Mi = Hex("43e1b3"), Ja = Hex("25956a"), Cy = Hex("2ce8f5"),
            Bl = Hex("0099db"), DBl = Hex("124e89"), Pg = Hex("c0cbdc"), Gy = Hex("8b9bb4"), Sl = Hex("5a6988"),
            DSl = Hex("3a4466"), Li = Hex("d176d0"), Vi = Hex("9b4ca3");

        /// <summary>A canvas of pixels (row 0 at the bottom, like Texture2D) that remembers which pixels the figure fills.</summary>
        public sealed class Canvas
        {
            public readonly int Width, Height;
            public readonly Color32[] Pixels;
            private readonly bool[] filled;

            /// <summary>The figure's origin: drawing coordinates start Margin pixels in from the canvas corner.</summary>
            public readonly int OriginX, OriginY;

            public Canvas(int width, int height, int originX = Margin, int originY = Margin)
            {
                Width = width;
                Height = height;
                OriginX = originX;
                OriginY = originY;
                Pixels = new Color32[width * height];
                filled = new bool[width * height];
            }

            public bool IsFilled(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && filled[y * Width + x];

            /// <summary>Sets one figure pixel (figure coordinates; outside the canvas is ignored).</summary>
            public void Px(int fx, int fy, Color32 c)
            {
                int x = fx + OriginX, y = fy + OriginY;
                if (x < 0 || y < 0 || x >= Width || y >= Height) return;
                Pixels[y * Width + x] = c;
                filled[y * Width + x] = true;
            }

            /// <summary>Fills an inclusive rectangle (figure coordinates).</summary>
            public void Rect(int x0, int y0, int x1, int y1, Color32 c)
            {
                for (int y = Math.Min(y0, y1); y <= Math.Max(y0, y1); y++)
                    for (int x = Math.Min(x0, x1); x <= Math.Max(x0, x1); x++)
                        Px(x, y, c);
            }

            /// <summary>Fills an ellipse round (cx, cy) with radii rx, ry, only rows yMin..yMax (pixel centres inside).</summary>
            public void Ellipse(float cx, float cy, float rx, float ry, Color32 c, int yMin = int.MinValue, int yMax = int.MaxValue)
            {
                for (int y = Mathf.FloorToInt(cy - ry); y <= Mathf.CeilToInt(cy + ry); y++)
                {
                    if (y < yMin || y > yMax) continue;
                    for (int x = Mathf.FloorToInt(cx - rx); x <= Mathf.CeilToInt(cx + rx); x++)
                    {
                        float dx = (x - cx) / rx, dy = (y - cy) / ry;
                        if (dx * dx + dy * dy <= 1f) Px(x, y, c);
                    }
                }
            }

            /// <summary>A roof triangle: full width xLeft..xRight on row yBase, narrowing evenly to one pixel on row yApex.</summary>
            public void Triangle(int xLeft, int xRight, int yBase, int yApex, Color32 c)
            {
                float mid = (xLeft + xRight) / 2f, half = (xRight - xLeft) / 2f;
                for (int y = yBase; y <= yApex; y++)
                {
                    float t = yApex == yBase ? 0f : (float)(y - yBase) / (yApex - yBase);
                    float h = half * (1f - t);
                    Rect(Mathf.RoundToInt(mid - h), y, Mathf.RoundToInt(mid + h), y, c);
                }
            }

            /// <summary>A line of pixels from a to b (figure coordinates), thickness 1.</summary>
            public void Line(int x0, int y0, int x1, int y1, Color32 c)
            {
                int steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
                for (int i = 0; i <= steps; i++)
                {
                    float t = steps == 0 ? 0f : (float)i / steps;
                    Px(Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), Mathf.RoundToInt(Mathf.Lerp(y0, y1, t)), c);
                }
            }

            /// <summary>A door on the ground, centred on cx: a dark frame round a panel, with a gold knob.</summary>
            public void Door(float cx, int width, int height, Color32 frame, Color32 panel)
            {
                int x0 = Mathf.RoundToInt(cx - width / 2f), x1 = x0 + width - 1;
                Rect(x0, 0, x1, height - 1, frame);
                Rect(x0 + 1, 0, x1 - 1, height - 2, panel);
                Px(x1 - 1, height / 2, Gd);
            }

            /// <summary>A framed window (inclusive rect): an outline-coloured frame round its glass.</summary>
            public void Window(int x0, int y0, int x1, int y1, Color32 glass)
            {
                Rect(x0, y0, x1, y1, O);
                Rect(x0 + 1, y0 + 1, x1 - 1, y1 - 1, glass);
            }

            /// <summary>
            /// The outline round the whole figure, as NpcDesignArt draws it: every empty pixel touching the figure (also
            /// diagonally), then every empty pixel beside that ring (not diagonally, which rounds its corners).
            /// </summary>
            public void Outline()
            {
                var ring = new bool[filled.Length];
                for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    if (filled[y * Width + x]) continue;
                    for (int dy = -1; dy <= 1 && !ring[y * Width + x]; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (!IsFilled(x + dx, y + dy)) continue;
                        ring[y * Width + x] = true;
                        break;
                    }
                }
                var outer = new bool[filled.Length];
                for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x;
                    if (filled[i] || ring[i]) continue;
                    outer[i] = (x > 0 && ring[i - 1]) || (x < Width - 1 && ring[i + 1]) ||
                               (y > 0 && ring[i - Width]) || (y < Height - 1 && ring[i + Width]);
                }
                for (int i = 0; i < Pixels.Length; i++)
                {
                    if (!ring[i] && !outer[i]) continue;
                    Pixels[i] = OutlineColor;
                    filled[i] = true;
                }
            }

            /// <summary>The canvas as a point-filtered sprite at the people's pixel size, pivoted on its centre.</summary>
            public Sprite ToSprite(string name)
            {
                var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
                {
                    name = name,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                texture.SetPixels32(Pixels);
                texture.Apply();
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, Width, Height), new Vector2(0.5f, 0.5f), TownLayout.PixelsPerUnit);
                sprite.name = name;
                return sprite;
            }
        }

        /// <summary>True for the kinds drawn from Kenney tiles (TownSquareView lays them) rather than in code.</summary>
        public static bool IsKenney(TownBuildingKind kind) => kind == TownBuildingKind.Cottage || kind == TownBuildingKind.StoneCottage;

        /// <summary>
        /// A building's sprite (made once): its BuildingPixels-square figure with the outline round it, so it is drawn
        /// (BuildingPixels + 2 Margin) / PixelsPerUnit units across, centred on its art rect. Kenney-tile kinds are drawn here too
        /// (in their colours) as a fallback for when the catalog is missing.
        /// </summary>
        public static Sprite Building(TownBuildingKind kind)
        {
            string key = $"Town {kind}";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            Canvas c = BuildingCanvas(kind);
            Sprite sprite = c.ToSprite(key);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>A building's finished pixels (tests read them).</summary>
        public static Canvas BuildingCanvas(TownBuildingKind kind)
        {
            int size = BuildingPixels + 2 * Margin;
            var c = new Canvas(size, size);
            switch (kind)
            {
                case TownBuildingKind.Treehouse: Treehouse(c); break;
                case TownBuildingKind.Nest: Nest(c); break;
                case TownBuildingKind.MushroomHut: MushroomHut(c, Rd, DRd, false); break;
                case TownBuildingKind.VentHut: MushroomHut(c, Li, Vi, true); break;
                case TownBuildingKind.Observatory: Observatory(c); break;
                case TownBuildingKind.Dome: Dome(c); break;
                case TownBuildingKind.Tower: Tower(c); break;
                case TownBuildingKind.Block: Block(c); break;
                case TownBuildingKind.Lodge: Lodge(c); break;
                case TownBuildingKind.Cabin: Cabin(c); break;
                case TownBuildingKind.MarketStall: MarketStall(c); break;
                default: Cottage(c, kind == TownBuildingKind.StoneCottage); break;
            }
            c.Outline();
            return c;
        }

        /// <summary>Where a VentHut's vent pipe tops out, from the building's art centre (world units): its bubbles rise from here.</summary>
        public static Vector2 VentTop => new Vector2(24.5f - BuildingPixels / 2f, 32f - BuildingPixels / 2f) / TownLayout.PixelsPerUnit;

        // ------------------------------------------------------------------ the buildings (figure 0..31 square, door at the bottom)

        private static void Treehouse(Canvas c)
        {
            // Bushes at the trunk's foot, the trunk with its door, the plank hut on its platform, the leafy crown.
            c.Ellipse(5, 1, 5, 3, DGr, 0);
            c.Ellipse(27, 1, 4, 3, DGr, 0);
            c.Rect(11, 0, 20, 13, Br);
            c.Rect(18, 0, 20, 13, Db);
            c.Px(13, 10, Db); c.Px(12, 8, Db); c.Px(19, 4, O);
            c.Door(15.5f, 6, 8, O, Db);
            c.Rect(3, 12, 28, 13, Db);
            c.Rect(5, 14, 26, 21, Br);
            for (int y = 16; y <= 20; y += 2) c.Rect(5, y, 26, y, Db);
            c.Window(8, 15, 12, 19, Gd);
            c.Window(19, 15, 23, 19, Gd);
            c.Ellipse(16, 25, 15.5f, 6.5f, DGr, 21);
            c.Ellipse(9, 27, 7, 4, Gr);
            c.Ellipse(22, 28, 7, 3.5f, Gr);
            c.Ellipse(15, 30, 6, 2, Gr);
            c.Px(6, 27, DGr); c.Px(24, 29, DGr); c.Px(14, 25, DGr);
        }

        private static void Nest(Canvas c)
        {
            // A woven bowl with a round door, a straw rim and a leafy dome on top, a feather for a flag.
            c.Ellipse(16, 8, 15.5f, 10, Br, 0, 16);
            for (int y = 2; y <= 14; y += 4)
                for (int x = 1; x <= 30; x += 3)
                    c.Px(x + (y / 4) % 2, y, (x / 3) % 2 == 0 ? Gs : Db);
            c.Ellipse(16, 4, 4, 5.5f, O, 0);
            c.Ellipse(16, 4, 3, 4.5f, Db, 0);
            c.Px(18, 4, Gd);
            c.Ellipse(16, 16.5f, 15.5f, 2.5f, Gs);
            c.Ellipse(16, 16.5f, 12, 1.2f, Db);
            c.Ellipse(16, 18, 12, 9, DGr, 18);
            c.Ellipse(12, 22, 5, 3, Gr);
            c.Ellipse(20, 24, 4, 2.5f, Gr);
            c.Window(5, 7, 8, 10, Gd);
            c.Window(23, 7, 26, 10, Gd);
            c.Rect(16, 27, 16, 30, Pk);
            c.Rect(17, 28, 17, 31, Or);
        }

        private static void MushroomHut(Canvas c, Color32 cap, Color32 rim, bool vent)
        {
            // A cream stalk with its door and round windows under a giant spotted cap; the vent hut has a bubbling pipe.
            if (vent)
            {
                c.Rect(23, 18, 26, 29, Gy);
                c.Rect(23, 18, 23, 29, Pg);
                c.Rect(22, 29, 27, 31, Sl);
            }
            c.Rect(7, 0, 24, 14, Cr);
            c.Rect(21, 0, 24, 14, Pe);
            c.Door(15.5f, 6, 9, Db, Br);
            c.Ellipse(10, 8, 1.7f, 1.7f, O);
            c.Ellipse(10, 8, 1f, 1f, Cy);
            c.Ellipse(21, 8, 1.7f, 1.7f, O);
            c.Ellipse(21, 8, 1f, 1f, Cy);
            c.Ellipse(16, 14, 16, 13.5f, cap, 15);
            c.Rect(1, 14, 30, 15, rim);
            c.Ellipse(9, 20, 2.2f, 2, W);
            c.Ellipse(18, 24, 2.6f, 2, W);
            c.Ellipse(24, 18, 1.6f, 1.5f, W);
            c.Ellipse(13, 26, 1.4f, 1, W);
            c.Ellipse(26, 22, 1, 1, W);
        }

        private static void Observatory(Canvas c)
        {
            // A round stone tower, banded, under a blue dome with a slit and a telescope peeking out.
            c.Rect(7, 0, 24, 17, Pg);
            c.Rect(21, 0, 24, 17, Gy);
            c.Rect(7, 6, 24, 6, Gy);
            c.Rect(7, 12, 24, 12, Gy);
            c.Door(15.5f, 6, 9, Sl, DBl);
            c.Window(9, 13, 11, 15, Cy);
            c.Window(20, 13, 22, 15, Cy);
            c.Rect(5, 17, 26, 18, Sl);
            c.Ellipse(15.5f, 18, 10, 10, Bl, 19);
            c.Ellipse(12.5f, 22, 3, 3, Cy, 19);
            c.Rect(15, 19, 16, 27, DBl);
            for (int i = 0; i < 9; i++)
            {
                c.Px(17 + i, 22 + i, Gy);
                c.Px(17 + i, 23 + i, Sl);
            }
            c.Px(26, 31, Gd);
            c.Px(26, 30, Gd);
        }

        private static void Dome(Canvas c)
        {
            // A low pale dome with blue bands, a round-topped door, porthole windows and an antenna.
            c.Ellipse(15.5f, 0, 15.5f, 21, Pg, 0);
            c.Ellipse(19, 0, 12, 19, Gy, 0, 0);
            for (int y = 6; y <= 13; y += 7)
                for (int x = 0; x < 32; x++)
                    if (c.IsFilled(x + c.OriginX, y + c.OriginY)) c.Px(x, y, Bl);
            c.Rect(13, 0, 18, 7, DBl);
            c.Ellipse(15.5f, 7, 3, 2.2f, DBl);
            c.Px(17, 4, Gd);
            c.Ellipse(6, 9, 1.8f, 1.8f, O);
            c.Ellipse(6, 9, 1f, 1f, Cy);
            c.Ellipse(25, 9, 1.8f, 1.8f, O);
            c.Ellipse(25, 9, 1f, 1f, Cy);
            c.Rect(15, 21, 16, 27, Sl);
            c.Ellipse(15.5f, 28.5f, 1.8f, 1.8f, Cy);
        }

        private static void Tower(Canvas c)
        {
            // A tall square tower under a pointed triangle roof, a round window high up.
            c.Rect(9, 0, 22, 22, Bl);
            c.Rect(19, 0, 22, 22, DBl);
            c.Rect(9, 11, 22, 11, DBl);
            c.Door(15.5f, 6, 9, DBl, DSl);
            c.Ellipse(15.5f, 17, 2.6f, 2.6f, O);
            c.Ellipse(15.5f, 17, 1.6f, 1.6f, Gd);
            c.Triangle(7, 24, 23, 31, Gd);
            for (int y = 23; y <= 31; y++)
                for (int x = 16; x <= 24; x++)
                    if (c.IsFilled(x + c.OriginX, y + c.OriginY)) c.Px(x, y, Gs);
        }

        private static void Block(Canvas c)
        {
            // A cube drawn in a slant: a lilac front with its door and a triangle sign, a darker side, a pale top; a ball on top.
            c.Rect(2, 0, 21, 17, Li);
            for (int i = 1; i <= 4; i++)
            {
                c.Rect(2 + i, 17 + i, 21 + i, 17 + i, Pg); // the top face, slanting back
                c.Rect(21 + i, i, 21 + i, 17 + i, Vi);   // the side face
            }
            c.Door(9.5f, 6, 9, Vi, DSl);
            c.Triangle(13, 19, 9, 13, Gd);
            // A ball on the roof: circle, triangle and square, the shape people's own motifs.
            c.Ellipse(13, 26.5f, 5, 5, Bl, 22);
            c.Ellipse(14.5f, 25.5f, 3.2f, 3.2f, DBl, 22);
            c.Ellipse(12, 28, 1.6f, 1.6f, Cy);
        }

        private static void Lodge(Canvas c)
        {
            // Log walls with round log ends, a steep roof, warm windows and a bone over the door.
            c.Rect(2, 0, 29, 15, Br);
            for (int y = 2; y <= 14; y += 3) c.Rect(2, y, 29, y, Db);
            for (int y = 0; y <= 15; y += 3)
            {
                c.Px(1, y + 1, Pe);
                c.Px(30, y + 1, Pe);
            }
            c.Triangle(0, 31, 16, 31, DGr);
            for (int y = 18; y <= 29; y += 3)
                for (int x = 0; x < 32; x++)
                    if (c.IsFilled(x + c.OriginX, y + c.OriginY) && (x + y) % 4 == 0) c.Px(x, y, Gr);
            c.Rect(3, 15, 28, 16, Db);
            c.Door(15.5f, 7, 10, Db, O);
            c.Px(18, 5, Gd);
            c.Rect(12, 11, 19, 11, Cr);
            c.Rect(11, 10, 11, 12, Cr);
            c.Rect(20, 10, 20, 12, Cr);
            c.Window(4, 6, 8, 10, Gd);
            c.Window(23, 6, 27, 10, Gd);
        }

        private static void Cabin(Canvas c)
        {
            // A small log cabin under a slanted red roof, a stone chimney with a puff of smoke.
            c.Rect(22, 16, 26, 27, Gy);
            for (int y = 18; y <= 26; y += 3) c.Rect(22, y, 26, y, Sl);
            c.Rect(21, 27, 27, 28, Sl);
            c.Rect(3, 0, 28, 13, Br);
            for (int y = 2; y <= 12; y += 3) c.Rect(3, y, 28, y, Db);
            for (int y = 14; y <= 20; y++) c.Rect(1 + (y - 14) / 2, y, 30 - (y - 14) * 2, y, DRd);
            c.Rect(1, 14, 30, 14, Rd);
            c.Door(11.5f, 6, 9, Db, O);
            c.Window(18, 5, 23, 9, Gd);
            c.Ellipse(25, 30.5f, 2, 1.3f, Pg);
        }

        private static void MarketStall(Canvas c)
        {
            // A counter piled with goods between two posts, under a red-and-white striped awning with a scalloped edge.
            c.Rect(3, 9, 4, 21, Db);
            c.Rect(27, 9, 28, 21, Db);
            c.Rect(1, 0, 30, 8, Br);
            c.Rect(1, 8, 30, 8, Db);
            for (int x = 5; x <= 27; x += 6) c.Rect(x, 0, x, 7, Db);
            c.Ellipse(8, 10.5f, 2, 1.8f, Rd);
            c.Ellipse(13, 10.5f, 2, 1.8f, Gd);
            c.Ellipse(18, 10.5f, 2, 1.8f, Gr);
            c.Ellipse(23, 10.5f, 2, 1.8f, Or);
            c.Px(8, 12, DGr); c.Px(18, 12, DGr);
            for (int x = 0; x <= 31; x++)
            {
                Color32 stripe = (x / 4) % 2 == 0 ? Rd : W;
                c.Rect(x, 21, x, 27, stripe);
                int scallop = x % 4 == 0 || x % 4 == 3 ? 21 : 20;
                c.Rect(x, scallop, x, 20, stripe);
            }
            c.Rect(0, 27, 31, 28, DRd);
        }

        /// <summary>The fallback cottage, in the Kenney tiles' colours (a red or grey roof over a wooden or stone wall).</summary>
        private static void Cottage(Canvas c, bool stone)
        {
            c.Rect(0, 0, 31, 15, stone ? Pg : Br);
            c.Rect(0, 16, 31, 31, stone ? Sl : Rd);
            for (int y = 19; y <= 29; y += 4) c.Rect(0, y, 31, y, stone ? Gy : Or);
            c.Door(TownLayout.DoorOffset(TownBuildingKind.Cottage) * TownLayout.PixelsPerUnit, 8, 12, Db, Br);
            c.Window(20, 5, 26, 11, Cy);
        }

        // ------------------------------------------------------------------ the statue

        /// <summary>
        /// A town's statue (made once per race): its leader's design carved in stone (NpcDesignArt's leader pixels mapped
        /// onto a stone ramp tinted with the region colour, the outline kept) on a pedestal, StatueWidth x StatueHeight pixels.
        /// </summary>
        public static Sprite Statue(Theme theme)
        {
            string key = $"Town Statue {theme}";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            Sprite sprite = StatueCanvas(theme).ToSprite(key);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>The statue's finished pixels (tests read them).</summary>
        public static Canvas StatueCanvas(Theme theme)
        {
            var c = new Canvas(StatueWidth, StatueHeight, 0, 0);
            // The pedestal: a stepped stone block, its own outline round it.
            c.Rect(3, 2, 16, 7, Gy);
            c.Rect(2, 2, 17, 3, Sl);
            c.Rect(3, 7, 16, 7, Pg);
            c.Rect(14, 4, 16, 6, Sl);
            c.Outline();
            // The leader in stone, standing on the pedestal.
            Race race = TownSquare.RaceOf(theme);
            DinoVariant variant = race == Race.Dinosaur ? DinoVariant.Terrestrial : DinoVariant.None;
            Color32[] design = NpcDesignArt.Pixels(NpcDesignArt.LookOf(race, variant), true, 0);
            Color region = CubeWorld.ThemeColor(theme);
            for (int y = 0; y < NpcDesignArt.Size; y++)
            for (int x = 0; x < NpcDesignArt.Size; x++)
            {
                Color32 p = design[y * NpcDesignArt.Size + x];
                if (p.a == 0) continue;
                int cx = x + 2, cy = y + 7;
                if (cx < 0 || cy < 0 || cx >= c.Width || cy >= c.Height) continue;
                c.Pixels[cy * c.Width + cx] = IsOutline(p) ? OutlineColor : Stone(p, region);
            }
            return c;
        }

        /// <summary>A colour carved in stone: its brightness on a grey ramp, a touch of the region colour.</summary>
        public static Color32 Stone(Color32 p, Color region)
        {
            float lum = (0.299f * p.r + 0.587f * p.g + 0.114f * p.b) / 255f;
            float shade = Mathf.Lerp(0.42f, 0.95f, Mathf.InverseLerp(0.15f, 0.95f, lum));
            Color grey = new Color(shade, shade, shade * 1.04f);
            Color tinted = Color.Lerp(grey, grey * region, 0.18f);
            tinted.a = 1f;
            return tinted;
        }

        private static bool IsOutline(Color32 p) => p.r == OutlineColor.r && p.g == OutlineColor.g && p.b == OutlineColor.b;

        // ------------------------------------------------------------------ small pieces

        /// <summary>The marked door's doormat (4.6's way in): a woven red rug with a brown border and a stripe, outlined.</summary>
        public static Sprite Doormat()
        {
            const string key = "Town Doormat";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            var c = new Canvas(16, 8);
            c.Rect(0, 0, 11, 3, Db);
            c.Rect(1, 1, 10, 2, DRd);
            c.Rect(2, 1, 9, 1, Rd);
            c.Px(3, 2, Or); c.Px(6, 2, Or); c.Px(9, 2, Or);
            c.Outline();
            Sprite sprite = c.ToSprite(key);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>The marked door's lantern, hung beside it: a warm light in a dark frame.</summary>
        public static Sprite Lantern()
        {
            const string key = "Town Lantern";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            var c = new Canvas(9, 11);
            c.Rect(1, 0, 3, 0, Db);
            c.Rect(0, 1, 4, 5, Db);
            c.Rect(1, 2, 3, 4, Gd);
            c.Rect(2, 6, 2, 6, Db);
            c.Outline();
            Sprite sprite = c.ToSprite(key);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// One tile of plaza paving (16 pixels a unit, like the floors; two units square, drawn tiled): pale flagstones with
        /// soft joints, translucent so the region's floor colour shows through.
        /// </summary>
        public static Sprite Paving()
        {
            const string key = "Town Paving";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            const int size = 32;
            var pixels = new Color32[size * size];
            var stone = new Color32(255, 255, 255, 46);
            var joint = new Color32(0x3f, 0x26, 0x31, 52);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Two courses of flagstones a row, offset like brickwork.
                int row = y / 8, shift = row % 2 == 0 ? 0 : 6;
                bool joinRow = y % 8 == 7;
                bool joinCol = (x + shift) % 12 == 11;
                pixels[y * size + x] = joinRow || joinCol ? joint : stone;
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = key,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        // ------------------------------------------------------------------ fragments and repair (4.8)

        /// <summary>Where each pip sits on a 7x7 die face (figure pixels), for 1..6 pips, like a real die.</summary>
        public static readonly Vector2Int[][] PipLayouts =
        {
            new Vector2Int[0],
            new[] { new Vector2Int(3, 3) },
            new[] { new Vector2Int(1, 5), new Vector2Int(5, 1) },
            new[] { new Vector2Int(1, 5), new Vector2Int(3, 3), new Vector2Int(5, 1) },
            new[] { new Vector2Int(1, 1), new Vector2Int(1, 5), new Vector2Int(5, 1), new Vector2Int(5, 5) },
            new[] { new Vector2Int(1, 1), new Vector2Int(1, 5), new Vector2Int(3, 3), new Vector2Int(5, 1), new Vector2Int(5, 5) },
            new[] { new Vector2Int(1, 1), new Vector2Int(1, 3), new Vector2Int(1, 5), new Vector2Int(5, 1), new Vector2Int(5, 3), new Vector2Int(5, 5) },
        };

        /// <summary>A fragment's die badge: a cream die face showing its number as plum pips (1-6), outlined.</summary>
        public static Sprite DieFace(int pips)
        {
            pips = Mathf.Clamp(pips, 1, 6);
            string key = "Die Face " + pips;
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            var c = new Canvas(11, 11);
            c.Rect(0, 0, 6, 6, Cr);
            c.Px(0, 0, Pe); c.Px(6, 0, Pe); c.Px(0, 6, Pe); c.Px(6, 6, Pe); // softened corners
            foreach (Vector2Int p in PipLayouts[pips]) c.Px(p.x, p.y, O);
            c.Outline();
            Sprite sprite = c.ToSprite(key);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// A lock's keyhole plate (4.9): a gold plate with a dark keyhole for a key-locked arch, a pale-blue one for a key-locked
        /// bonus alcove (a chest key's lock).
        /// </summary>
        public static Sprite Keyhole(bool chest)
        {
            string key = chest ? "Keyhole Chest" : "Keyhole";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            Color32 rim = chest ? Bl : Gs, plate = chest ? Cy : Gd;
            var c = new Canvas(11, 13);
            c.Rect(0, 0, 6, 8, rim);
            c.Rect(1, 1, 5, 7, plate);
            // The keyhole: a round head over a slot.
            c.Px(3, 7, O); c.Px(2, 6, O); c.Px(3, 6, O); c.Px(4, 6, O); c.Px(2, 5, O); c.Px(3, 5, O); c.Px(4, 5, O);
            c.Px(3, 4, O); c.Px(3, 3, O); c.Px(3, 2, O);
            c.Outline();
            Sprite sprite = c.ToSprite(key);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>An item lock's plate (4.9): a dark plate in the keyhole plate's gold rim, for the item's icon to sit on.</summary>
        public static Sprite LockPlate()
        {
            const string key = "Lock Plate";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            var c = new Canvas(14, 14);
            c.Rect(0, 0, 9, 9, Gs);
            c.Rect(1, 1, 8, 8, DSl);
            c.Px(0, 0, Gd); c.Px(9, 0, Gd); c.Px(0, 9, Gd); c.Px(9, 9, Gd); // rivets
            c.Outline();
            Sprite sprite = c.ToSprite(key);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>A mended town's banner (4.8): a pennant in the region's colour with a gold band, on a dark pole.</summary>
        public static Sprite RepairBanner(Theme theme)
        {
            string key = "Repair Banner " + theme;
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            Color32 cloth = (Color32)CubeWorld.ThemeColor(theme);
            cloth.a = 255;
            var c = new Canvas(12, 26);
            c.Rect(0, 0, 0, 21, Db);            // the pole
            c.Px(0, 22, Gd);                    // its gold finial
            c.Rect(1, 9, 6, 20, cloth);         // the cloth
            c.Rect(1, 17, 6, 18, Gd);           // a gold band
            c.Px(3, 8, cloth); c.Px(4, 8, cloth); // a swallowtail notch's sides
            c.Px(1, 8, cloth); c.Px(6, 8, cloth);
            c.Px(1, 7, cloth); c.Px(6, 7, cloth);
            c.Outline();
            Sprite sprite = c.ToSprite(key);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>A mended town's statue glow (4.8): a soft gold halo, brightest at its heart, drawn behind the statue.</summary>
        public static Sprite RepairHalo()
        {
            const string key = "Repair Halo";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            const int size = 32;
            var c = new Canvas(size, size, 0, 0);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - size / 2f) / (size / 2f), dy = (y + 0.5f - size / 2f) / (size / 2f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 1f) continue;
                // Three hard steps of gold (pixel style: no smooth gradient), fainter outwards.
                byte alpha = d < 0.45f ? (byte)150 : d < 0.75f ? (byte)95 : (byte)45;
                c.Px(x, y, new Color32(Gd.r, Gd.g, Gd.b, alpha));
            }
            Sprite sprite = c.ToSprite(key);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>A bubble from a vent hut's pipe: a mint ring with a glint.</summary>
        public static Sprite Bubble()
        {
            const string key = "Town Bubble";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            var c = new Canvas(7, 7, 0, 0);
            c.Ellipse(3, 3, 3, 3, Ja);
            c.Ellipse(3, 3, 2, 2, Mi);
            c.Px(2, 4, W);
            Sprite sprite = c.ToSprite(key);
            Cache[key] = sprite;
            return sprite;
        }

        private static Color32 Hex(string hex) => new Color32(
            Convert.ToByte(hex.Substring(0, 2), 16), Convert.ToByte(hex.Substring(2, 2), 16), Convert.ToByte(hex.Substring(4, 2), 16), 255);
    }
}
