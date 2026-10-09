using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>How a town's temple looks (4.14): sealed, open (its fragments handed in) or mended (its gem taken).</summary>
    public enum TempleLook { Sealed = 0, Open = 1, Mended = 2 }

    /// <summary>
    /// The temples' art (4.14), drawn in code in the towns' style (TownArt: Kenney's palette, the plum outline, point filter,
    /// 8 pixels a unit). Each race's temple has its own shape: Town's marble portico, the finches' leafy shrine, the
    /// mushroom folk's capped hall, the aliens' ringed dome, the shape people's stepped pyramid, the dinosaurs' rock-hewn
    /// hall with horns. Sealed, a glowing seal in the region's colour with two chains crosses the door; open, the doorway
    /// glows warm; mended, a gem in the region's colour shines over the door. Also the gems, the fragment shards of the solve
    /// moment, and the altar. Made once and cached.
    /// </summary>
    public static class TempleArt
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        private static readonly Color32 O = Hex("3f2631"), W = Hex("ffffff"),
            Br = Hex("bd6c4a"), Db = Hex("763b36"), Cr = Hex("ead4aa"), Pe = Hex("e8b796"),
            Rd = Hex("e43b44"), DRd = Hex("a22633"), Or = Hex("f77622"), Gs = Hex("feae34"), Gd = Hex("fee761"),
            Gr = Hex("63c74d"), DGr = Hex("3e8948"), Cy = Hex("2ce8f5"),
            Bl = Hex("0099db"), DBl = Hex("124e89"), Pg = Hex("c0cbdc"), Gy = Hex("8b9bb4"), Sl = Hex("5a6988"),
            DSl = Hex("3a4466"), Li = Hex("d176d0"), Vi = Hex("9b4ca3");

        /// <summary>The door's place in the figure: centred, DoorWidth across and DoorHeight tall from the ground.</summary>
        public const int DoorLeft = 12, DoorRight = 19, DoorHeight = 11;

        /// <summary>A region's gem colour (its face colour, deepened for Town's white so it reads).</summary>
        public static Color32 GemColor(Theme theme)
        {
            if (theme == Theme.Town) return Li;
            Color32 c = CubeWorld.ThemeColor(theme);
            c.a = 255;
            return c;
        }

        /// <summary>A temple's sprite (BuildingPixels square plus the outline, as every town building).</summary>
        public static Sprite Building(Theme theme, TempleLook look)
        {
            string key = $"Temple {theme} {look}";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            Sprite sprite = Canvas(theme, look).ToSprite(key);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>A temple's finished pixels (tests read them).</summary>
        public static TownArt.Canvas Canvas(Theme theme, TempleLook look)
        {
            int size = TownArt.BuildingPixels + 2 * TownArt.Margin;
            var c = new TownArt.Canvas(size, size);
            switch (theme)
            {
                case Theme.Biology: Shrine(c); break;
                case Theme.Chemistry: CappedHall(c); break;
                case Theme.Physics: RingedDome(c); break;
                case Theme.Math: Pyramid(c); break;
                case Theme.EarthAtmosphere: RockHall(c); break;
                default: Portico(c); break;
            }
            Doorway(c, theme, look);
            c.Outline();
            return c;
        }

        private static void Doorway(TownArt.Canvas c, Theme theme, TempleLook look)
        {
            c.Rect(DoorLeft - 1, 0, DoorRight + 1, DoorHeight, O);
            if (look == TempleLook.Sealed)
            {
                // A dark door under a round seal in the region's colour, crossed by two chains.
                c.Rect(DoorLeft, 0, DoorRight, DoorHeight - 1, DSl);
                Color32 seal = GemColor(theme);
                c.Ellipse(15.5f, 5.5f, 3.6f, 3.6f, Gd);
                c.Ellipse(15.5f, 5.5f, 2.6f, 2.6f, seal);
                c.Px(15, 5, W); c.Px(16, 6, W);
                for (int i = 0; i <= 9; i++)
                {
                    c.Px(DoorLeft + (i * 7) / 9, 1 + i, Gy);
                    c.Px(DoorRight - (i * 7) / 9, 1 + i, Gy);
                }
                return;
            }
            // Open: a warm glow inside, brighter at the floor.
            for (int y = 0; y < DoorHeight; y++)
                c.Rect(DoorLeft, y, DoorRight, y, y < 3 ? Gd : y < 7 ? Gs : Or);
            if (look == TempleLook.Mended)
            {
                Color32 gem = GemColor(theme);
                c.Rect(14, DoorHeight + 2, 17, DoorHeight + 4, gem);
                c.Rect(15, DoorHeight + 1, 16, DoorHeight + 5, gem);
                c.Px(15, DoorHeight + 4, W);
            }
        }

        // ------------------------------------------------------------------ each race's temple (figure 0..31, door centred)

        private static void Portico(TownArt.Canvas c)
        {
            // Town: white marble columns on steps under a purple pediment with a gold trim.
            c.Rect(0, 0, 31, 1, Pg);
            c.Rect(1, 2, 30, 2, W);
            foreach (int x in new[] { 3, 8, 22, 27 })
            {
                c.Rect(x, 3, x + 2, 18, W);
                c.Rect(x + 2, 3, x + 2, 18, Pg);
            }
            c.Rect(9, 3, 22, 18, Cr);
            c.Rect(1, 19, 30, 21, W);
            c.Rect(1, 21, 30, 21, Gd);
            c.Triangle(0, 31, 22, 31, Vi);
            c.Triangle(4, 27, 23, 28, Li);
            c.Px(15, 26, Gd); c.Px(16, 26, Gd);
        }

        private static void Shrine(TownArt.Canvas c)
        {
            // Finches: two trunk pillars and a woven wall under a great leafy roof, a feather on top.
            c.Rect(1, 0, 30, 1, Db);
            c.Rect(3, 2, 6, 17, Br);
            c.Rect(25, 2, 28, 17, Br);
            c.Rect(5, 2, 6, 17, Db);
            c.Rect(7, 2, 24, 16, Gs);
            for (int y = 4; y <= 15; y += 3)
                for (int x = 7; x <= 24; x += 2) c.Px(x + (y / 3) % 2, y, Br);
            c.Ellipse(15.5f, 21, 16, 6.5f, DGr, 16);
            c.Ellipse(10, 23, 7, 4, Gr);
            c.Ellipse(21, 24, 7, 4, Gr);
            c.Ellipse(15.5f, 27, 6, 3, Gr);
            c.Rect(15, 29, 16, 31, Rd);
        }

        private static void CappedHall(TownArt.Canvas c)
        {
            // Mushroom folk: a cream hall of stalk columns under a wide spotted cap, a violet band.
            c.Rect(2, 0, 29, 1, Pe);
            c.Rect(4, 2, 27, 15, Cr);
            foreach (int x in new[] { 5, 9, 22, 26 }) c.Rect(x, 2, x, 15, Pe);
            c.Rect(3, 15, 28, 16, Vi);
            c.Ellipse(15.5f, 16, 16, 15, Rd, 17);
            c.Ellipse(8, 22, 2.4f, 2, W);
            c.Ellipse(17, 27, 2.8f, 2, W);
            c.Ellipse(24, 21, 1.8f, 1.6f, W);
            c.Ellipse(12, 29, 1.4f, 1, W);
            c.Rect(0, 17, 31, 17, DRd);
        }

        private static void RingedDome(TownArt.Canvas c)
        {
            // Aliens: a pale drum under a blue dome, a gold ring round it like an orbit, a star on top.
            c.Rect(3, 0, 28, 13, Pg);
            c.Rect(24, 0, 28, 13, Gy);
            c.Rect(3, 6, 28, 6, Bl);
            c.Ellipse(15.5f, 14, 12, 12, Bl, 14);
            c.Ellipse(12, 19, 3, 3, Cy, 14);
            for (int x = 0; x <= 31; x++)
            {
                float t = (x - 15.5f) / 15.5f;
                int y = Mathf.RoundToInt(18 + 2.5f * (1f - t * t)) - 2;
                c.Px(x, y, Gs);
                c.Px(x, y + 1, Gd);
            }
            c.Rect(15, 26, 16, 29, Sl);
            c.Px(15, 30, Gd); c.Px(16, 30, Gd); c.Px(14, 30, Gs); c.Px(17, 30, Gs); c.Px(15, 31, Gd);
        }

        private static void Pyramid(TownArt.Canvas c)
        {
            // Shape people: a stepped pyramid in golden blocks, a blue triangle at its peak.
            for (int step = 0; step < 6; step++)
            {
                int y0 = step * 4, inset = step * 3;
                c.Rect(inset, y0, 31 - inset, y0 + 3, step % 2 == 0 ? Gd : Gs);
                c.Rect(inset, y0 + 3, 31 - inset, y0 + 3, Or);
            }
            c.Triangle(11, 20, 24, 31, Bl);
            c.Triangle(13, 18, 25, 29, Cy);
        }

        private static void RockHall(TownArt.Canvas c)
        {
            // Dinosaurs: a hall cut into a rock mound, cracks and moss, two great horns over the door.
            c.Ellipse(15.5f, 0, 16, 26, Gy, 0);
            c.Ellipse(20, 0, 11, 22, Sl, 0, 0);
            for (int x = 0; x < 32; x++)
                if (c.IsFilled(x + c.OriginX, 14 + c.OriginY) && x % 5 == 0) c.Px(x, 14, DSl);
            c.Line(5, 6, 8, 12, DSl);
            c.Line(25, 9, 27, 4, DSl);
            c.Ellipse(9, 22, 4, 2, DGr);
            c.Ellipse(22, 23, 5, 2, Gr);
            // Horns curling up either side of the door.
            for (int i = 0; i < 7; i++)
            {
                c.Px(DoorLeft - 2 - i / 2, DoorHeight + 1 + i, Cr);
                c.Px(DoorRight + 2 + i / 2, DoorHeight + 1 + i, Cr);
            }
            c.Rect(DoorLeft - 1, DoorHeight + 1, DoorRight + 1, DoorHeight + 2, Cr);
        }

        // ------------------------------------------------------------------ gems, shards and the altar

        /// <summary>A face's gem: a cut stone in its region's colour, a white glint, a gold rim.</summary>
        public static Sprite Gem(Theme theme)
        {
            string key = "Temple Gem " + theme;
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            Color32 gem = GemColor(theme);
            var c = new TownArt.Canvas(13, 13);
            for (int y = 0; y <= 8; y++)
            {
                int half = y <= 5 ? y : 8 - y + 2;
                half = Mathf.Min(half, 4);
                c.Rect(4 - half, y, 4 + half, y, y == 5 ? Gd : gem);
            }
            c.Px(3, 6, W); c.Px(2, 5, W);
            c.Outline();
            Sprite sprite = c.ToSprite(key);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>One fragment shard (the solve moment): a jagged sliver in the region's colour.</summary>
        public static Sprite Shard(Theme theme)
        {
            string key = "Temple Shard " + theme;
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            Color32 gem = GemColor(theme);
            var c = new TownArt.Canvas(9, 10);
            c.Rect(1, 0, 2, 5, gem);
            c.Rect(2, 2, 3, 4, gem);
            c.Px(2, 5, W);
            c.Outline();
            Sprite sprite = c.ToSprite(key);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>The altar (TempleRoom.AltarRect's art): a stone table on steps, a cushion in the region's colour.</summary>
        public static Sprite Altar(Theme theme)
        {
            string key = "Temple Altar " + theme;
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            var c = new TownArt.Canvas(22, 18);
            c.Rect(0, 0, 17, 2, Gy);
            c.Rect(2, 3, 15, 9, Pg);
            c.Rect(13, 3, 15, 9, Gy);
            c.Rect(1, 10, 16, 11, W);
            c.Rect(6, 12, 11, 13, GemColor(theme));
            c.Outline();
            Sprite sprite = c.ToSprite(key);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Every temple pixel canvas (tests check the pixel style of each).</summary>
        public static IEnumerable<TownArt.Canvas> AllCanvases()
        {
            foreach (Theme theme in new[] { Theme.Town, Theme.Biology, Theme.Chemistry, Theme.Physics, Theme.Math, Theme.EarthAtmosphere })
                foreach (TempleLook look in new[] { TempleLook.Sealed, TempleLook.Open, TempleLook.Mended })
                    yield return Canvas(theme, look);
        }

        private static Color32 Hex(string hex) => new Color32(
            Convert.ToByte(hex.Substring(0, 2), 16), Convert.ToByte(hex.Substring(2, 2), 16), Convert.ToByte(hex.Substring(4, 2), 16), 255);
    }
}
