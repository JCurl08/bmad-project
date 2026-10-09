using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The hand-drawn NPC designs: one per race (dinosaurs have a ground and an avian one), each with a leader variant, plus
    /// Town's greeter (4.18: only she and the Mayor, the Townsfolk leader, keep a Town look of their own).
    /// </summary>
    public enum NpcLook { Townsfolk = 0, Finch = 1, Chemistry = 2, Physics = 3, Shape = 4, GroundDino = 5, AvianDino = 6, Greeter = 7 }

    /// <summary>
    /// The NPC designs (4.3): one hand-drawn 16x16 pixel sprite per race in the player's style (the Kenney Tiny Dungeon hero,
    /// D 112): a big head over a short body, front-facing, a thick two-pixel dark plum outline, Kenney's palette and the
    /// feet on the tile's bottom edge. Each grid is authored in rows top-down with one character per pixel (see Palette);
    /// '.' is empty and the outline is added around the silhouette in code (Outlined), so the grids hold only the figure.
    /// '1' and '2' are the seeded accent (a light and a dark tone): each NPC takes one of its race's AccentCount pairs
    /// from its salt, a small tint variation. A leader is its own grid with a gold crown and royal trim, and never varies.
    /// Drawn white-tinted (the colours are baked); the hostile tint and the hit flash work on the one renderer as before.
    /// </summary>
    public static class NpcDesignArt
    {
        /// <summary>Pixels per side of a design (one Kenney tile).</summary>
        public const int Size = 16;

        /// <summary>Accent pairs per race: the seeded variation.</summary>
        public const int AccentCount = 3;

        /// <summary>The outline (Kenney's dark plum, the player's own outline colour).</summary>
        public static readonly Color32 OutlineColor = new Color32(0x3f, 0x26, 0x31, 255);

        /// <summary>The gold of a leader's crown.</summary>
        public static readonly Color32 CrownGold = Hex("fee761");

        // Kenney Tiny Dungeon's palette (the Endesga-like colours its characters use), one character each.
        private static readonly Dictionary<char, string> Palette = new Dictionary<char, string>
        {
            { 'O', "3f2631" }, // outline, also used inside the figure (like Kenney's)
            { 'k', "262b44" }, // eyes
            { 'w', "ffffff" }, // glints, teeth
            { 'f', "f7c282" }, // skin
            { 'g', "e19a65" }, // skin shade
            { 'b', "bd6c4a" }, // brown
            { 'c', "763b36" }, // dark brown
            { 'h', "ead4aa" }, // cream
            { 'e', "e8b796" }, // peach
            { 'i', "f6757a" }, // blush pink
            { 'r', "e43b44" }, // red
            { 'R', "a22633" }, // dark red
            { 'o', "f77622" }, // orange
            { 'y', "feae34" }, // gold shade
            { 'Y', "fee761" }, // gold
            { 'l', "63c74d" }, // green
            { 'G', "3e8948" }, // dark green
            { 'm', "43e1b3" }, // mint
            { 'j', "25956a" }, // jade
            { 't', "2ce8f5" }, // cyan
            { 'u', "0099db" }, // blue
            { 'U', "124e89" }, // dark blue
            { 's', "c0cbdc" }, // pale grey
            { 'S', "8b9bb4" }, // grey
            { 'n', "5a6988" }, // slate
            { 'N', "3a4466" }, // dark slate
            { 'v', "d176d0" }, // lilac
            { 'V', "9b4ca3" }, // violet
            { 'P', "68386c" }, // royal purple
        };

        /// <summary>Each look's accent pairs (light, dark), picked per NPC by its salt; a leader always takes the first.</summary>
        private static readonly Dictionary<NpcLook, string[]> Accents = new Dictionary<NpcLook, string[]>
        {
            { NpcLook.Townsfolk, new[] { "5a69883a4466", "bd6c4a763b36", "25956a3e8948" } },  // cloak: slate, brown, green
            { NpcLook.Greeter, new[] { "0099db124e89", "e43b44a22633", "63c74d3e8948" } },  // dress: blue, red, green
            { NpcLook.Finch, new[] { "bd6c4a763b36", "8b9bb45a6988", "f77622bd6c4a" } },      // plumage: brown, slate, rust
            { NpcLook.Chemistry, new[] { "d176d09b4ca3", "2ce8f50099db", "63c74d3e8948" } },  // cap: lilac, cyan, green
            { NpcLook.Physics, new[] { "c0cbdc8b9bb4", "d176d09b4ca3", "feae34f77622" } },    // suit: silver, lilac, amber
            { NpcLook.Shape, new[] { "0099db124e89", "d176d09b4ca3", "43e1b325956a" } },      // body: blue, lilac, mint
            { NpcLook.GroundDino, new[] { "e43b44a22633", "0099db124e89", "c0cbdc8b9bb4" } }, // bandana: red, blue, grey
            { NpcLook.AvianDino, new[] { "f77622bd6c4a", "feae34f77622", "f6757ab55088" } },  // crest: orange, gold, pink
        };

        // ------------------------------------------------------------------------------------------ the designs

        // A townsperson passing through (Sage Conkwell, a Town captive): a hooded travelling cloak in the accent, a satchel strap, boots. Not a farmer (4.18).
        private static readonly string[] Townsfolk =
        {
            "................",
            "................",
            "......1111......",
            ".....111111.....",
            "....11222211....",
            "....12ffff21....",
            "....12kffk21....",
            "....12ffff21....",
            "....112gg211....",
            "...1111111b11...",
            "...11111b1111...",
            "..f1111b11112f..",
            "..f11b1111112f..",
            "...1111111112...",
            "...1111111112...",
            ".....cc..cc.....",
        };

        // Mayor Muddleton (4.18): a royal purple top hat with a gold band and brim, a grand white moustache, a dark suit with the gold chain of office and a red sash.
        private static readonly string[] TownsfolkLeader =
        {
            "................",
            "......PPPP......",
            "......PPPP......",
            ".....YYYYYY.....",
            "...YPPPPPPPPY...",
            "....ffffffff....",
            "....ffkffkff....",
            "....gssffssg....",
            ".....ssssss.....",
            "......ffff......",
            "...NNNYwwYNNN...",
            "..fNNNrYYNNNNf..",
            "..fNNNNrNNNNNf..",
            "...NNNNNrNNNN...",
            "...NNNNNNrNNN...",
            ".....cc..cc.....",
        };

        // Hattie Hullo, Town's greeter (4.18): an auburn bun with a flower, a cheery wave, a dress in the accent under a white apron.
        private static readonly string[] Greeter =
        {
            "................",
            "................",
            "......RRR.......",
            ".....RrrrR...f..",
            "....RRRRRRvY.f..",
            "....RffffffR.f..",
            "....ffkffkff.f..",
            "....ffkffkff.f..",
            "....iffOOffi.1..",
            ".....ffffff..1..",
            "....1111111111..",
            "...f11wwww11....",
            "...f11wwww11....",
            "...111wwww111...",
            "...2222222222...",
            ".....cc..cc.....",
        };

        // A finch: a round fluffy bird in the accent plumage, big glinting eyes, an orange beak, a cream breast, blushing.
        private static readonly string[] Finch =
        {
            "................",
            "................",
            "................",
            "......1111......",
            ".....111111.....",
            "....11111111....",
            "....1kw11kw1....",
            "....1kk11kk1....",
            "....i1oYYo1i....",
            "....11hooh11....",
            "...21hhhhhh12...",
            "..221hhhhhh122..",
            "..221hhhhhh122..",
            "...21hhhhhh12...",
            "....11hhhh11....",
            ".....oo..oo.....",
        };

        // The finches' leader: a crowned head over a proper white ruff and a royal purple mantle.
        private static readonly string[] FinchLeader =
        {
            "................",
            "................",
            ".....Y.YY.Y.....",
            ".....YYYYYY.....",
            ".....yyrryy.....",
            "....11111111....",
            "....1kw11kw1....",
            "....1kk11kk1....",
            "....i1oYYo1i....",
            "...w11hooh11w...",
            "..wswswswswsws..",
            "..PP1hhhhhh1PP..",
            "..PV1hhhhhh1VP..",
            "..PV21hhhh12VP..",
            "..PYYY1hh1YYYP..",
            ".....oo..oo.....",
        };

        // A Chemistry person: a spotted mushroom cap in the accent, a cream face, a white coat over cream stalk legs.
        private static readonly string[] Chemistry =
        {
            "................",
            "................",
            ".....111111.....",
            "...1111w111111..",
            "..11w111111w11..",
            "..222222222222..",
            "....hhhhhhhh....",
            "....hhkhhkhh....",
            "....hhkhhkhh....",
            "....ehhOOhhe....",
            ".....hhhhhh.....",
            "...wwwwwwwwww...",
            "..hhswwwwwwshh..",
            "..hhOswwwwsOhh..",
            ".....hh..hh.....",
            ".....SS..SS.....",
        };

        // The Chemistry leader: a crown on a bigger cap, a royal purple robe with a gold hem, a bubbling flask on a staff.
        private static readonly string[] ChemistryLeader =
        {
            "................",
            ".....Y.YY.Y.....",
            "...11YYYYYY11...",
            "..11w111111w11..",
            "..222222222222..",
            "....hhhhhhhh.m..",
            "....hhkhhkhh.c..",
            "....hhkhhkhh.c..",
            "....ehhOOhhe.c..",
            ".....hhhhhh..c..",
            "...PVVVYYVVVPc..",
            "..hhPVVYYVVPhc..",
            "..hhPVVYYVVPhc..",
            "...PyyyyyyyyPc..",
            ".....hh..hh..c..",
            ".....SS..SS..c..",
        };

        // A Physics person: green, big-headed, two antennae with glowing tips, big dark eyes, a jumpsuit in the accent.
        private static readonly string[] Physics =
        {
            "................",
            "...t........t...",
            "....l......l....",
            ".....llllll.....",
            "....llllllll....",
            "...llllllllll...",
            "...lkkllllkkl...",
            "...lkwllllwkl...",
            "....llllllll....",
            ".....llOOll.....",
            "......GllG......",
            "...1111111111...",
            "..ll21111112ll..",
            "..llOt2222tOll..",
            ".....11..11.....",
            ".....NN..NN.....",
        };

        // The Physics leader: a wild white mane round its head, a gold circlet, a royal purple robe with a gold hem.
        private static readonly string[] PhysicsLeader =
        {
            "................",
            "...t...ww...t...",
            "..ww.wYwwYw.ww..",
            "..wwwYYYYYYwww..",
            ".wwwllllllllwww.",
            "..wlllllllllllw.",
            "..wlkkllllkklw..",
            "..wlkwllllwklw..",
            "...wllllllllw...",
            ".....llOOll.....",
            "......GllG......",
            "...PVVVYYVVVP...",
            "..llPVVYYVVPll..",
            "..llPVVYYVVPll..",
            "...PyyyyyyyyP...",
            ".....NN..NN.....",
        };

        // A shape person (4.18: less human, Humpty-Dumpty style): an egg-shaped body in the accent (light left, dark right) with the face on it, a belt with a gold buckle, little stick arms with white gloves, little legs.
        private static readonly string[] Shape =
        {
            "................",
            "................",
            "................",
            "......1122......",
            ".....111222.....",
            "....11111222....",
            "....1wk11wk2....",
            "...11kk11kk22...",
            "...11i1OO1i22...",
            "..N1111111222N..",
            "..wkkkkYkkkkkw..",
            "...1111112222...",
            "....11111222....",
            ".....111222.....",
            "......N..N......",
            ".....kk..kk.....",
        };

        // The shape people's leader: a royal purple egg under a crown of three gold triangles, a gold belt and a ruler staff.
        private static readonly string[] ShapeLeader =
        {
            "................",
            ".....Y.YY.Y.....",
            ".....YYYYYY..t..",
            "......VVPP...c..",
            ".....VVVPPP..c..",
            "....VVVVVPPP.c..",
            "....VwkVVwkP.c..",
            "...VVkkVVkkPPc..",
            "...VViVOOViPPc..",
            "..NVVVVVVVPPPc..",
            "..wYYYYYYYYYYw..",
            "...VVVVVVPPPPc..",
            "....VVVVVPPP.c..",
            ".....VVVPPP..c..",
            "......N..N...c..",
            ".....kk..kk..c..",
        };

        // A ground dinosaur: a green, toothy, big-snouted head with back spikes, a cream belly, a bandana in the accent.
        private static readonly string[] GroundDino =
        {
            "................",
            "................",
            ".....o.oo.o.....",
            ".....llllll.....",
            "....lklllklG....",
            "....llllllll....",
            "...llllllllll...",
            "...lwOwOOwOwl...",
            "....GGGGGGGG....",
            ".....211112.....",
            "....ll1111ll....",
            "...lllhhhhlll...",
            "..lll.hhhh.lll..",
            "..G.llhhhhll....",
            "...GGl....lGG...",
            "...ww......ww...",
        };

        // The ground dinosaurs' leader: crowned, a royal purple cape over the shoulders and a bone club.
        private static readonly string[] GroundDinoLeader =
        {
            "................",
            ".....Y.YY.Y.....",
            ".....YYYYYY.....",
            ".....yyrryy.....",
            "....lklllklG.ww.",
            "....llllllll.hh.",
            "...llllllllll.h.",
            "...lwOwOOwOwl.h.",
            "....GGGGGGGG..h.",
            ".....PVVVVP...h.",
            "...PVVhhhhVVPlh.",
            "..PVVlhhhhlVVPh.",
            "..PVl.hhhh.lVPh.",
            "..PVllhhhhllVP..",
            "..PYGl....lGYP..",
            "...ww......ww...",
        };

        // An avian dinosaur (4.18: a feathered raptor, not a penguin), seen side-on facing right: a blue head with a crest in the accent, a hooked gold beak, a long feathered tail with accent tips, a wing of dark feathers, a cream throat and long gold legs with clawed feet.
        private static readonly string[] AvianDino =
        {
            "................",
            "................",
            "........1.1.....",
            ".......21uuu....",
            ".......2uuwku...",
            "........uuuuuu..",
            "........uuwOwO..",
            ".......uuuuu....",
            "..1uuuuuuuuhh...",
            "...2uuuUUUuhh...",
            "......uU1U1uh...",
            "......uuUUuu....",
            ".......uuuu.....",
            ".......y..y.....",
            "......yy..yy....",
            ".....yyy.yyy....",
        };

        // The avian dinosaurs' leader: crowned among long crest plumes, a royal purple mantle with a gold hem over the wings.
        private static readonly string[] AvianDinoLeader =
        {
            "................",
            "........Y.Y.Y...",
            "....11..YYYYY...",
            ".....2221uuu....",
            ".......2uuwku...",
            "........uuuuuu..",
            "........uuwOwO..",
            ".......uuuuu....",
            "..1uuuPPPPPhh...",
            "...2uuPVVVPhh...",
            "......PVVVPuh...",
            "......PYYYPu....",
            ".....PPPPPP.....",
            ".......y..y.....",
            "......yy..yy....",
            ".....yyy.yyy....",
        };

        // ------------------------------------------------------------------------------------------ lookups

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>The design a race (and, for dinosaurs, its variant) is drawn with.</summary>
        public static NpcLook LookOf(Race race, DinoVariant variant)
        {
            switch (race)
            {
                case Race.Finch: return NpcLook.Finch;
                case Race.Mushroom: return NpcLook.Chemistry;
                case Race.Alien: return NpcLook.Physics;
                case Race.Shape: return NpcLook.Shape;
                case Race.Dinosaur: return variant == DinoVariant.Avian ? NpcLook.AvianDino : NpcLook.GroundDino;
                default: return NpcLook.Townsfolk;
            }
        }

        /// <summary>The design of an NPC spec: its race's, or the greeter's own (4.18, TownPlan.IsGreeter).</summary>
        public static NpcLook LookOf(NpcSpec spec) => TownPlan.IsGreeter(spec) && !spec.IsLeader ? NpcLook.Greeter : LookOf(spec.Race, spec.Variant);

        /// <summary>
        /// The accent an NPC wears, from its salt (hashed, so it does not follow the salt's other uses; no random draw, so
        /// no stream or signature changes). Leaders always wear the first.
        /// </summary>
        public static int AccentOf(uint salt) => (int)(((salt ^ 0x9E3779B9u) * 0x85EBCA6Bu) >> 16) % AccentCount;

        /// <summary>The accent of an NPC spec (0 for a leader).</summary>
        public static int AccentOf(NpcSpec spec) => spec.IsLeader ? 0 : AccentOf(spec.Salt);

        /// <summary>A design's grid, rows top-down (tests read it).</summary>
        public static string[] Grid(NpcLook look, bool leader)
        {
            switch (look)
            {
                case NpcLook.Finch: return leader ? FinchLeader : Finch;
                case NpcLook.Chemistry: return leader ? ChemistryLeader : Chemistry;
                case NpcLook.Physics: return leader ? PhysicsLeader : Physics;
                case NpcLook.Shape: return leader ? ShapeLeader : Shape;
                case NpcLook.GroundDino: return leader ? GroundDinoLeader : GroundDino;
                case NpcLook.AvianDino: return leader ? AvianDinoLeader : AvianDino;
                case NpcLook.Greeter: return leader ? TownsfolkLeader : Greeter;
                default: return leader ? TownsfolkLeader : Townsfolk;
            }
        }

        /// <summary>
        /// The finished design as texture pixels (Size x Size, row 0 at the bottom, like Texture2D): the grid's colours
        /// with the accent filled in and the outline added around the silhouette. Transparent pixels have alpha 0.
        /// </summary>
        public static Color32[] Pixels(NpcLook look, bool leader, int accent)
        {
            string[] grid = Grid(look, leader);
            if (grid.Length != Size) throw new InvalidOperationException($"{look} grid has {grid.Length} rows");
            string[] pairs = Accents[look];
            string pair = pairs[((leader ? 0 : accent) % pairs.Length + pairs.Length) % pairs.Length];
            Color32 light = Hex(pair.Substring(0, 6)), dark = Hex(pair.Substring(6, 6));

            var pixels = new Color32[Size * Size];
            var filled = new bool[Size * Size];
            for (int row = 0; row < Size; row++)
            {
                string line = grid[row];
                if (line.Length != Size) throw new InvalidOperationException($"{look} grid row {row} is {line.Length} wide");
                int y = Size - 1 - row;
                for (int x = 0; x < Size; x++)
                {
                    char ch = line[x];
                    if (ch == '.' || ch == ' ') continue;
                    Color32 colour;
                    if (ch == '1') colour = light;
                    else if (ch == '2') colour = dark;
                    else if (Palette.TryGetValue(ch, out string hex)) colour = Hex(hex);
                    else throw new InvalidOperationException($"{look} grid has an unknown colour '{ch}'");
                    pixels[y * Size + x] = colour;
                    filled[y * Size + x] = true;
                }
            }
            Outlined(pixels, filled);
            return pixels;
        }

        /// <summary>
        /// Adds the two-pixel outline: every empty pixel touching the figure (also diagonally), then every empty pixel
        /// beside that ring (not diagonally, which rounds its corners), as Kenney's characters are drawn.
        /// </summary>
        private static void Outlined(Color32[] pixels, bool[] filled)
        {
            var ring = new bool[filled.Length];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                if (filled[y * Size + x]) continue;
                for (int dy = -1; dy <= 1 && !ring[y * Size + x]; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= Size || ny >= Size || !filled[ny * Size + nx]) continue;
                    ring[y * Size + x] = true;
                    break;
                }
            }
            var outer = new bool[filled.Length];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int i = y * Size + x;
                if (filled[i] || ring[i]) continue;
                outer[i] = (x > 0 && ring[i - 1]) || (x < Size - 1 && ring[i + 1]) ||
                           (y > 0 && ring[i - Size]) || (y < Size - 1 && ring[i + Size]);
            }
            for (int i = 0; i < pixels.Length; i++)
                if (ring[i] || outer[i]) pixels[i] = OutlineColor;
        }

        /// <summary>How many pixel rows a design reaches up from its bottom edge: its top opaque row (outline included), plus one.</summary>
        public static int DrawnRows(NpcLook look, bool leader)
        {
            Color32[] px = Pixels(look, leader, 0);
            for (int y = Size - 1; y >= 0; y--)
                for (int x = 0; x < Size; x++)
                    if (px[y * Size + x].a > 0) return y + 1;
            return 0;
        }

        /// <summary>A design's sprite (made once, then cached): Size pixels across, one world unit before scaling, point-filtered.</summary>
        public static Sprite SpriteFor(NpcLook look, bool leader, int accent)
        {
            if (leader) accent = 0;
            accent = (accent % AccentCount + AccentCount) % AccentCount;
            string key = $"{look}/{(leader ? "leader" : "folk")}/{accent}";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = $"NPC {key}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(Pixels(look, leader, accent));
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            sprite.name = $"NPC {key}";
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>The sprite of an NPC spec: its race's design (its leader's if it leads), in its accent.</summary>
        public static Sprite SpriteFor(NpcSpec spec) => SpriteFor(LookOf(spec), spec.IsLeader, AccentOf(spec));

        private static Color32 Hex(string hex) => new Color32(
            Convert.ToByte(hex.Substring(0, 2), 16), Convert.ToByte(hex.Substring(2, 2), 16), Convert.ToByte(hex.Substring(4, 2), 16), 255);
    }
}
