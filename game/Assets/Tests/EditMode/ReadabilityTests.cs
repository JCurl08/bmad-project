using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The readability pass (1.15): the art catalog maps every key the game uses to a Kenney sprite imported as 16 px per
    /// unit pixel art, no sprite is shared between two categories (or two enemies, items or gate kinds), a missing key
    /// logs once and falls back to its generated shape, NPC parts are outlined race silhouettes (never catalog art),
    /// conversations are at most two lines ending on a true hint, and the death text is plain. 4.3: every race's NPC
    /// design (and its leader's) is an outlined 16 px figure in the player's style, distinct by silhouette.
    /// </summary>
    public class ReadabilityTests
    {
        private const string CatalogAssetPath = "Assets/Resources/ArtCatalog.asset";

        private static ArtCatalog Catalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ArtCatalog>(CatalogAssetPath);
            Assert.IsNotNull(catalog, $"No art catalog at {CatalogAssetPath} (Cube > Build Art Catalog)");
            return catalog;
        }

        [TearDown]
        public void RestoreCatalog() => ArtCatalog.Use(null);

        // ---------- Catalog ----------

        [Test]
        public void EveryArtKey_MapsToAKenneySprite()
        {
            ArtCatalog catalog = Catalog();
            foreach (ArtKey key in ArtKeys.All)
            {
                Assert.IsTrue(catalog.TryGetSprite(key, out Sprite sprite), $"{key} has no art");
                string path = AssetDatabase.GetAssetPath(sprite);
                StringAssert.StartsWith("Assets/Art/Kenney/", path, $"{key} is not a Kenney sprite");
                Assert.AreNotEqual(ArtCategory.None, ArtKeys.CategoryOf(key), $"{key} has no category");
            }
            ArtCatalog.Use(catalog);
            foreach (ArtKey key in ArtKeys.All) Assert.IsNotNull(ArtCatalog.Get(key));
            Assert.IsEmpty(ArtCatalog.MissingKeys, "No key fell back");
        }

        [Test]
        public void KenneySprites_ArePixelArt_OneTilePerWorldUnit()
        {
            foreach (ArtCatalog.Entry entry in Catalog().Entries)
            {
                Sprite sprite = entry.Sprite;
                Assert.AreEqual(16f, sprite.pixelsPerUnit, entry.Key.ToString());
                Assert.AreEqual(new Vector2(1f, 1f), (Vector2)sprite.bounds.size, $"{entry.Key}: one tile is one world unit");
                Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode, entry.Key.ToString());
                Assert.AreEqual(1, sprite.texture.mipmapCount, $"{entry.Key}: no mipmaps");
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, entry.Key.ToString());
                Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode, entry.Key.ToString());
            }
        }

        [Test]
        public void NoSprite_IsSharedBetweenKeys_SoEveryCategoryAndVariantLooksDifferent()
        {
            var byTexture = new Dictionary<Texture2D, List<ArtKey>>();
            foreach (ArtCatalog.Entry entry in Catalog().Entries)
            {
                if (!byTexture.TryGetValue(entry.Sprite.texture, out List<ArtKey> keys)) byTexture[entry.Sprite.texture] = keys = new List<ArtKey>();
                keys.Add(entry.Key);
            }
            foreach (KeyValuePair<Texture2D, List<ArtKey>> pair in byTexture)
            {
                int categories = pair.Value.Select(ArtKeys.CategoryOf).Distinct().Count();
                Assert.AreEqual(1, categories, $"{pair.Key.name} is shared across categories: {string.Join(", ", pair.Value)}");
                Assert.AreEqual(1, pair.Value.Count, $"{pair.Key.name} is shared: {string.Join(", ", pair.Value)}");
            }
            Assert.AreEqual(Catalog().Entries.Count, Catalog().Entries.Select(e => e.Tile).Distinct().Count(), "A tile is used twice");
        }

        private static readonly ArtKey[] GateKeys =
        {
            ArtKey.GateGeneric, ArtKey.GateRock, ArtKey.GatePot, ArtKey.GateButtonDoor, ArtKey.GateVine, ArtKey.GateDarkRoom,
            ArtKey.GateCrackedWall, ArtKey.GateLeadDoor, ArtKey.GateTimedDoor, ArtKey.GateGap,
            ArtKey.GateRockPillars, ArtKey.GateFireWall, ArtKey.GateWater, ArtKey.GateDebris,
        };

        /// <summary>
        /// Tiles that read as something to pick up (1.24: "some things look like objects but cannot be collected"): the
        /// Tiny Town key, coin, bags, beehive, tools and weapons; the Tiny Dungeon chests, crate-like boxes, ring, shield,
        /// swords, potions, flasks, hammers, axes, anvil, ladder, wands and spears.
        /// </summary>
        private static readonly HashSet<string> ItemLookingTiles = new HashSet<string>(
            new[] { 93, 94, 107, 115, 116, 117, 118, 119, 127, 128, 129, 130, 131 }.Select(t => $"Tiny Town/tile_{t:0000}")
                .Concat(new[] { 54, 55, 61, 64, 68, 74, 89, 90, 91, 92, 101, 103, 104, 105, 106, 107, 113, 114, 115, 116, 117, 118, 119,
                    125, 126, 127, 128, 129, 130, 131 }.Select(t => $"Tiny Dungeon/tile_{t:0000}")));

        [Test]
        public void DecorTiles_NeverLookLikeSomethingToPickUp()
        {
            Dictionary<ArtKey, string> tiles = Catalog().Entries.ToDictionary(e => e.Key, e => e.Tile);
            var decor = ArtKeys.All.Where(k =>
            {
                ArtCategory c = ArtKeys.CategoryOf(k);
                // Decor, and every non-pickup object that stays put in the world: gates, switches, lamps, portals, particles.
                return c == ArtCategory.Ruin || c == ArtCategory.Floor || c == ArtCategory.Hazard || c == ArtCategory.Wall ||
                       c == ArtCategory.Gate || k == ArtKey.Fossil || k == ArtKey.GoalPad ||
                       k == ArtKey.TrialLamp || k == ArtKey.Particle || k == ArtKey.DoorSwitch || k == ArtKey.Portal;
            }).ToList();
            Assert.That(decor, Has.Member(ArtKey.Fossil).And.Member(ArtKey.RuinTile).And.Member(ArtKey.RuinBroken)
                .And.Member(ArtKey.TrialLamp).And.Member(ArtKey.Particle).And.Member(ArtKey.DoorSwitch).And.Member(ArtKey.Portal)
                .And.Member(ArtKey.GateGeneric).And.Member(ArtKey.PlankBridge).And.Member(ArtKey.GateDebris));
            foreach (ArtKey key in decor)
                Assert.IsFalse(ItemLookingTiles.Contains(tiles[key]), $"{key} uses an item-looking tile ({tiles[key]})");
            // And the real pickups do use them (the list is about what reads as collectible).
            Assert.IsTrue(ItemLookingTiles.Contains(tiles[ArtKey.Jumbles]));
            Assert.IsTrue(ItemLookingTiles.Contains(tiles[ArtKey.ItemGlowstone]));
        }

        [Test]
        public void FloorPattern_IsLight_Fixed_AndVaried_AndCalmestOnTheWeightlands()
        {
            var model = new CubeModel(1);
            foreach (Theme theme in new[] { Theme.Town, Theme.Biology, Theme.Chemistry, Theme.Physics, Theme.Math, Theme.EarthAtmosphere })
            {
                float max = 0f;
                var shades = new HashSet<float>();
                foreach (ScreenAddress screen in model.AllScreens())
                {
                    for (int y = 0; y < (int)CubeWorld.ScreenSize.y; y++)
                    {
                        for (int x = 0; x < (int)CubeWorld.ScreenSize.x; x++)
                        {
                            float s = FloorPattern.Shade(theme, screen, new Vector2Int(x, y));
                            Assert.AreEqual(s, FloorPattern.Shade(theme, screen, new Vector2Int(x, y)), "The same every time");
                            max = Mathf.Max(max, Mathf.Abs(s));
                            shades.Add(Mathf.Round(s * 1000f));
                        }
                    }
                }
                Assert.LessOrEqual(max, FloorPattern.MaxShade * FloorPattern.Strength(theme) + 1e-6f, $"{theme}: low contrast");
                Assert.LessOrEqual(FloorPattern.MaxShade, 0.1f + 1e-6f, "At most a tenth of white or black over a floor");
                Assert.Greater(max, 0.5f * FloorPattern.MaxShade * FloorPattern.Strength(theme), $"{theme}: some texture shows");
                Assert.Greater(shades.Count, 20, $"{theme}: the tiles vary");
            }
            Assert.Less(FloorPattern.Strength(Theme.Physics), FloorPattern.Strength(Theme.Biology), "The Weightlands* floor is the calmest");
            // The overlay covers exactly the floor block (FloorFraction of the screen), one texel per unit from its
            // bottom-left corner, where the floor's tiles start: every texel sits on one tile.
            Sprite sprite = FloorPattern.For(Theme.Math, new ScreenAddress(FaceId.Top, 0, 0));
            Assert.AreEqual(1f, sprite.pixelsPerUnit);
            Assert.AreEqual(Vector2.zero, sprite.rect.position);
            Assert.AreEqual((Vector2)(CubeWorld.ScreenSize * FloorPattern.FloorFraction), (Vector2)sprite.bounds.size);
            Assert.AreEqual(CubeWorld.ScreenSize, new Vector2(sprite.texture.width, sprite.texture.height)); // one texel per floor tile (24 x 15)
            // Two screens of a face differ.
            Assert.AreNotEqual(
                Enumerable.Range(0, 160).Select(i => FloorPattern.Shade(Theme.Math, new ScreenAddress(FaceId.Top, 0, 0), new Vector2Int(i % 16, i / 16))).ToList(),
                Enumerable.Range(0, 160).Select(i => FloorPattern.Shade(Theme.Math, new ScreenAddress(FaceId.Top, 1, 0), new Vector2Int(i % 16, i / 16))).ToList());
        }

        [Test]
        public void GateTiles_AreNotFromTheRuinFamilies_SoAGateNeverReadsAsAWalkableRuin()
        {
            // Ruin tile families: the Town arch pieces (111-114) and the Dungeon columns (57-59).
            var ruinFamilies = new HashSet<string>();
            for (int t = 111; t <= 114; t++) ruinFamilies.Add($"Tiny Town/tile_{t:0000}");
            for (int t = 57; t <= 59; t++) ruinFamilies.Add($"Tiny Dungeon/tile_{t:0000}");
            Dictionary<ArtKey, string> tiles = Catalog().Entries.ToDictionary(e => e.Key, e => e.Tile);
            foreach (ArtKey ruin in new[] { ArtKey.RuinArch, ArtKey.RuinPillar }) ruinFamilies.Add(tiles[ruin]);
            foreach (ArtKey gate in GateKeys.Concat(new[] { ArtKey.VineBridge, ArtKey.PlankBridge }))
                Assert.IsFalse(ruinFamilies.Contains(tiles[gate]), $"{gate} uses a ruin tile ({tiles[gate]})");
        }

        [Test]
        public void AGuardedPickup_SitsBehindTheClosedGateArt_NeverOverlappingIt_AndTheGateDrawsInFront()
        {
            ArtCatalog.Use(Catalog());
            var library = AssetDatabase.LoadAssetAtPath<ModuleLibrary>("Assets/Modules/ModuleLibrary.asset");
            GateSlot prefabSlot = library.TownModules.SelectMany(m => m.Gates).First();
            var root = new GameObject("Pocket Test");
            try
            {
                var world = root.AddComponent<CubeWorld>();
                var slot = new GameObject("Gate Slot").AddComponent<GateSlot>();
                slot.transform.SetParent(root.transform, false);
                slot.Opening = prefabSlot.Opening;
                slot.PocketOffset = prefabSlot.PocketOffset;
                ItemDefinition item = BiologyBeaks.CreateItem(BeakKind.Thick);
                GameObject block = world.CreateBlock("Gate", slot.transform, slot.transform.position, CubeWorld.GateSize(slot),
                    Color.white, -4, true);
                var gate = block.AddComponent<Gate>();
                gate.Visual = block.GetComponentInChildren<SpriteRenderer>();
                gate.RequiredItem = item;
                ArtCatalog.DressGate(gate, ArtKey.GateGeneric);
                Assert.IsTrue(gate.ArtDressed);
                Assert.AreEqual(CubeWorld.GateSize(slot), gate.Solid.size, "The collider is unchanged");

                Vector2 pocket = slot.PocketCentre;
                float thickness = GateSlot.GateThickness;
                float back = 2f * slot.PocketOffset.magnitude - thickness / 2f; // the pocket's back wall, from the slot
                Vector2 dir = slot.PocketOffset.normalized;

                // A plain item pickup (every item, the Chemistry stones included, is one), as the reveal places it behind a gate.
                GameObject plain = world.CreateBlock("Pickup", root.transform, pocket, Vector2.one * CubeWorld.PickupSize, Color.white,
                    5, false, ArtKey.ItemThickBeak);
                CubeWorld.TuckIntoPocket(plain.transform, slot);

                float gateFar = Vector2.Dot((Vector2)gate.Visual.bounds.center - (Vector2)slot.transform.position, dir) +
                                Mathf.Abs(Vector2.Dot(gate.Visual.bounds.extents, dir));
                Assert.AreEqual(ArtCatalog.GateArtReach(thickness), gateFar, 1e-3f, "Gate art reaches one tile into the pocket");
                foreach (SpriteRenderer r in plain.GetComponentsInChildren<SpriteRenderer>())
                {
                    float centre = Vector2.Dot((Vector2)r.bounds.center - (Vector2)slot.transform.position, dir);
                    float half = Mathf.Abs(Vector2.Dot(r.bounds.extents, dir));
                    Assert.Greater(centre - half, gateFar, $"{r.name} overlaps the closed gate art");
                    Assert.LessOrEqual(centre + half, back + 1e-3f, $"{r.name} pokes through the pocket back wall");
                    Assert.Greater(gate.Visual.sortingOrder, r.sortingOrder, $"The closed gate draws in front of {r.name}");
                }
                Assert.AreEqual(pocket, (Vector2)plain.transform.position, "The pickup itself (its trigger) stays at the pocket centre");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheGame_PicksDistinctKeys_ForEachItemEnemyGateAndFloor()
        {
            var items = new[]
            {
                ArtKeys.Item(BiologyBeaks.CreateItem(BeakKind.Thin)), ArtKeys.Item(BiologyBeaks.CreateItem(BeakKind.Thick)),
                ArtKeys.Item(ChemistryIsotope.CreateItem(IsotopeStage.Glow)), ArtKeys.Item(ChemistryIsotope.CreateItem(IsotopeStage.Unstable)),
                ArtKeys.Item(ChemistryIsotope.CreateItem(IsotopeStage.Lead)), ArtKeys.Item(MassMittItem.CreateItem()),
                ArtKeys.Item(ItemDefinition.Create("other", "Other", Theme.Math, Color.white)),
            };
            Assert.AreEqual(items.Length, items.Distinct().Count(), "Each item has its own art");
            Assert.IsTrue(items.All(k => ArtKeys.CategoryOf(k) == ArtCategory.Item));

            var enemies = new[]
            {
                ArtKey.EnemyGeneric,
                ArtKeys.Enemy(BiologyEnemyKind.SeedWeevil), ArtKeys.Enemy(BiologyEnemyKind.PollenPuff),
                ArtKeys.Enemy(ChemistryEnemyKind.FreeRadical), ArtKeys.Enemy(ChemistryEnemyKind.RustMite),
                ArtKeys.Enemy(PhysicsEnemyKind.QuantumFlea), ArtKeys.Enemy(PhysicsEnemyKind.StaticCling),
            };
            Assert.AreEqual(enemies.Length, enemies.Distinct().Count(), "Each enemy type has its own art");
            Assert.IsTrue(enemies.All(k => ArtKeys.CategoryOf(k) == ArtCategory.Enemy));

            Theme[] themes = { Theme.Town, Theme.Biology, Theme.Chemistry, Theme.Physics, Theme.Math, Theme.EarthAtmosphere };
            Assert.AreEqual(themes.Length, themes.Select(ArtKeys.Floor).Distinct().Count(), "Each face theme has its own floor");

            var gates = new[]
            {
                ArtKey.GateGeneric, ArtKey.GateRock, ArtKey.GatePot, ArtKey.GateButtonDoor, ArtKey.GateVine, ArtKey.GateDarkRoom,
                ArtKey.GateCrackedWall, ArtKey.GateLeadDoor, ArtKey.GateTimedDoor,
            };
            Assert.IsTrue(gates.All(k => ArtKeys.CategoryOf(k) == ArtCategory.Gate && ArtKeys.IsTiled(k)));
            Assert.AreEqual(ArtCategory.Player, ArtKeys.CategoryOf(ArtKey.Player));
            Assert.AreEqual(ArtCategory.Wall, ArtKeys.CategoryOf(ArtKey.Wall));
        }

        [Test]
        public void AMissingKey_LogsOnce_AndFallsBackToItsGeneratedShape()
        {
            var empty = ScriptableObject.CreateInstance<ArtCatalog>();
            try
            {
                ArtCatalog.Use(empty);
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no art for Boulder"));
                Sprite first = ArtCatalog.Get(ArtKey.Boulder);
                Assert.AreSame(ArtCatalog.Shape(ArtKeys.FallbackShape(ArtKey.Boulder)), first, "Falls back to the generated shape");
                Assert.AreSame(first, ArtCatalog.Get(ArtKey.Boulder));
                LogAssert.NoUnexpectedReceived(); // logged once only
                CollectionAssert.Contains(ArtCatalog.MissingKeys.ToList(), ArtKey.Boulder);

                // A renderer pointed at a missing key still shows something: the shape, scaled to the size.
                var go = new GameObject("Fallback");
                try
                {
                    var renderer = go.AddComponent<SpriteRenderer>();
                    LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no art for Wall"));
                    ArtCatalog.Apply(renderer, ArtKey.Wall, new Vector2(3f, 1f));
                    Assert.IsNotNull(renderer.sprite);
                    Assert.AreEqual(SpriteDrawMode.Simple, renderer.drawMode);
                    Assert.AreEqual(new Vector3(3f, 1f, 1f), go.transform.localScale);
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            }
            finally
            {
                Object.DestroyImmediate(empty);
            }
        }

        [Test]
        public void TheFlashMaterial_DrawsWhite_WithTheFlashShader()
        {
            ArtCatalog.Use(Catalog());
            Material flash = ArtCatalog.Flash;
            Assert.IsNotNull(flash);
            Assert.AreEqual(HitFlash.ShaderName, flash.shader.name);
            Assert.IsTrue(flash.shader.isSupported);
        }

        // ---------- NPC parts (the swappable parts, drawn only with NpcFactory.PartArt on) ----------

        [Test]
        public void NpcParts_AreOutlinedRaceSilhouettes_NeverCatalogArt()
        {
            var catalogTextures = new HashSet<Texture2D>(Catalog().Entries.Select(e => e.Sprite.texture));
            foreach (Race race in RaceExtensions.All)
            {
                RacePartSet set = RacePartSets.For(race);
                foreach (PartSlot slot in new[] { PartSlot.Head, PartSlot.Torso, PartSlot.Legs })
                {
                    var looks = new List<string>();
                    foreach (NpcPart part in set.Slot(slot))
                    {
                        byte[] pixels = NpcPartArt.Pixels(race, part, out int w, out int h);
                        Assert.AreEqual(Mathf.RoundToInt(part.Size.x * NpcPartArt.PixelsPerUnit), w);
                        Assert.AreEqual(Mathf.RoundToInt(part.Size.y * NpcPartArt.PixelsPerUnit), h);
                        int drawn = pixels.Count(p => p != 0);
                        Assert.Greater(drawn, w * h / 5, $"{part.Id}: too little silhouette");
                        Assert.Greater(pixels.Count(NpcPartArt.IsOutline), w, $"{part.Id}: no outline");
                        // Every drawn pixel next to an empty one (or the border) is outline: the silhouette is closed.
                        for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            byte v = pixels[y * w + x];
                            if (v == 0 || NpcPartArt.IsOutline(v)) continue;
                            bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1 || pixels[y * w + x - 1] == 0 ||
                                        pixels[y * w + x + 1] == 0 || pixels[(y - 1) * w + x] == 0 || pixels[(y + 1) * w + x] == 0;
                            Assert.IsFalse(edge, $"{part.Id}: open silhouette at {x},{y}");
                        }
                        looks.Add(string.Join("", pixels));

                        Sprite sprite = ArtCatalog.NpcPart(race, part);
                        Assert.IsFalse(catalogTextures.Contains(sprite.texture), $"{part.Id} uses catalog art");
                        Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode);
                    }
                    Assert.AreEqual(looks.Count, looks.Distinct().Count(), $"{race} {slot}: two parts look the same");
                }
            }

            // The races' heads differ from each other (a beak, a cap, antennae, a polyhedron, a snout, a hat).
            var heads = RaceExtensions.All
                .Select(r => string.Join("", NpcPartArt.Pixels(r, RacePartSets.For(r).Heads[0], out _, out _))).ToList();
            Assert.AreEqual(heads.Count, heads.Distinct().Count(), "Two races share a head silhouette");
        }

        // ---------- NPC designs (4.3) ----------

        private static readonly NpcLook[] Looks = (NpcLook[])System.Enum.GetValues(typeof(NpcLook));

        /// <summary>The player's tile (D 112) as texture pixels, read from its PNG.</summary>
        private static Color32[] PlayerPixels()
        {
            Assert.IsTrue(Catalog().TryGetSprite(ArtKey.Player, out Sprite sprite));
            var texture = new Texture2D(2, 2);
            try
            {
                Assert.IsTrue(texture.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite))));
                Assert.AreEqual(NpcDesignArt.Size, texture.width);
                return texture.GetPixels32();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static bool Opaque(Color32[] px, int x, int y) =>
            x >= 0 && y >= 0 && x < NpcDesignArt.Size && y < NpcDesignArt.Size && px[y * NpcDesignArt.Size + x].a > 0;

        private static bool[] Mask(Color32[] px) => px.Select(p => p.a > 0).ToArray();

        private static int TopRow(Color32[] px)
        {
            for (int y = NpcDesignArt.Size - 1; y >= 0; y--)
                for (int x = 0; x < NpcDesignArt.Size; x++)
                    if (Opaque(px, x, y)) return y;
            return -1;
        }

        [Test]
        public void NpcDesigns_AreOutlinedSixteenPixelFigures_InThePlayersStyle()
        {
            Color32[] player = PlayerPixels();
            int playerArea = player.Count(p => p.a > 0);
            Color32 outline = NpcDesignArt.OutlineColor;
            Assert.IsTrue(player.Any(p => p.a > 0 && p.r == outline.r && p.g == outline.g && p.b == outline.b),
                "The designs use the player's own outline colour");
            foreach (NpcLook look in Looks)
            foreach (bool leader in new[] { false, true })
            for (int accent = 0; accent < NpcDesignArt.AccentCount; accent++)
            {
                string name = $"{look}{(leader ? " leader" : "")} accent {accent}";
                Color32[] px = NpcDesignArt.Pixels(look, leader, accent);
                Assert.AreEqual(NpcDesignArt.Size * NpcDesignArt.Size, px.Length, name);
                int area = px.Count(p => p.a > 0);
                Assert.That(area, Is.InRange(playerArea * 0.85f, playerArea * 1.3f), $"{name}: about the player's bulk");
                Assert.That(TopRow(px), Is.InRange(TopRow(player) - 2, NpcDesignArt.Size - 1), $"{name}: about the player's height");
                Assert.IsTrue(Enumerable.Range(0, NpcDesignArt.Size).Any(x => Opaque(px, x, 0)), $"{name}: stands on the tile's bottom edge, like the player");
                for (int y = 0; y < NpcDesignArt.Size; y++)
                for (int x = 0; x < NpcDesignArt.Size; x++)
                {
                    Color32 c = px[y * NpcDesignArt.Size + x];
                    if (c.a == 0) continue;
                    Assert.AreEqual(255, c.a, $"{name}: crisp pixels");
                    bool isOutline = c.r == outline.r && c.g == outline.g && c.b == outline.b;
                    if (isOutline) continue;
                    // A closed, thick outline: no figure pixel touches empty space, even diagonally (the feet may meet the
                    // tile's bottom edge, as the player's do).
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (ny < 0) continue;
                        Assert.IsTrue(Opaque(px, nx, ny), $"{name}: figure pixel {x},{y} is not outlined");
                    }
                }
                // Two pixels thick at the sides: the outline ring is followed by more outline or the figure, never a gap.
                for (int y = 1; y < NpcDesignArt.Size; y++)
                {
                    int left = 0;
                    while (left < NpcDesignArt.Size && !Opaque(px, left, y)) left++;
                    if (left == NpcDesignArt.Size) continue;
                    Assert.IsTrue(Opaque(px, left + 1, y), $"{name}: the outline is two pixels thick at row {y}");
                }

                Sprite sprite = NpcDesignArt.SpriteFor(look, leader, accent);
                Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode, name);
                Assert.AreEqual(NpcDesignArt.Size, sprite.texture.width, name);
            }
        }

        [Test]
        public void NpcDesigns_EachRaceHasItsOwnSilhouette_AndItsLeaderStandsOut()
        {
            Color32 purple = new Color32(0x68, 0x38, 0x6c, 255);
            bool Royal(Color32 c) => c.a > 0 && c.r == purple.r && c.g == purple.g && c.b == purple.b;
            bool Gold(Color32 c) => c.a > 0 && c.r == NpcDesignArt.CrownGold.r && c.g == NpcDesignArt.CrownGold.g && c.b == NpcDesignArt.CrownGold.b;
            int Diff(bool[] a, bool[] b) => a.Where((v, i) => v != b[i]).Count();

            // Every race (and dinosaur kind) differs from every other by shape, not only colour.
            for (int i = 0; i < Looks.Length; i++)
            for (int j = i + 1; j < Looks.Length; j++)
            {
                int d = Diff(Mask(NpcDesignArt.Pixels(Looks[i], false, 0)), Mask(NpcDesignArt.Pixels(Looks[j], false, 0)));
                Assert.GreaterOrEqual(d, 12, $"{Looks[i]} and {Looks[j]} share a silhouette ({d} pixels differ)");
            }
            // The mapping is pinned race by race, so swapping two designs fails here.
            Assert.AreEqual(NpcLook.Finch, NpcDesignArt.LookOf(Race.Finch, DinoVariant.None));
            Assert.AreEqual(NpcLook.Chemistry, NpcDesignArt.LookOf(Race.Mushroom, DinoVariant.None));
            Assert.AreEqual(NpcLook.Physics, NpcDesignArt.LookOf(Race.Alien, DinoVariant.None));
            Assert.AreEqual(NpcLook.Shape, NpcDesignArt.LookOf(Race.Shape, DinoVariant.None));
            Assert.AreEqual(NpcLook.Townsfolk, NpcDesignArt.LookOf(Race.Townsfolk, DinoVariant.None));
            Assert.AreEqual(NpcLook.GroundDino, NpcDesignArt.LookOf(Race.Dinosaur, DinoVariant.Terrestrial));
            Assert.AreEqual(NpcLook.AvianDino, NpcDesignArt.LookOf(Race.Dinosaur, DinoVariant.Avian));
            Assert.AreEqual(NpcLook.GroundDino, NpcDesignArt.LookOf(Race.Dinosaur, DinoVariant.None), "A dinosaur without a kind is drawn as a ground one");
            // And by what each design shows: the finch's orange beak, the alien's green skin, the mushroom's spotted cap
            // (white pixels on its cap rows), the townsperson's straw hat, the ground dinosaur's teeth, the avian's blue.
            bool Has(NpcLook look, string hex) => NpcDesignArt.Pixels(look, false, 0).Any(c => c.a > 0 && $"{c.r:x2}{c.g:x2}{c.b:x2}" == hex);
            Assert.IsTrue(Has(NpcLook.Finch, "f77622"), "The finch has its orange beak");
            Assert.IsTrue(Has(NpcLook.Physics, "63c74d"), "The Physics folk are green");
            Assert.IsTrue(Has(NpcLook.AvianDino, "0099db"), "The avian dinosaur is blue");
            Assert.IsTrue(Has(NpcLook.Townsfolk, "feae34"), "The townsperson wears a straw hat");
            Assert.AreEqual(Looks.Length - 1, RaceExtensions.All.Select(r => NpcDesignArt.LookOf(r, DinoVariant.None)).Distinct().Count(),
                "One design per race (the dinosaurs' two kinds add one)");

            foreach (NpcLook look in Looks)
            {
                Color32[] folk = NpcDesignArt.Pixels(look, false, 0);
                Color32[] leader = NpcDesignArt.Pixels(look, true, 0);
                int d = Diff(Mask(folk), Mask(leader));
                Assert.GreaterOrEqual(d, 12, $"{look}: the leader's silhouette is too close to its people's ({d} pixels differ)");
                // The leader's marks: a gold crown on top and royal purple trim, which no ordinary NPC wears.
                int crownRows = 0;
                for (int y = NpcDesignArt.Size - 6; y < NpcDesignArt.Size; y++)
                    if (Enumerable.Range(0, NpcDesignArt.Size).Any(x => Gold(leader[y * NpcDesignArt.Size + x]))) crownRows++;
                Assert.GreaterOrEqual(crownRows, 2, $"{look}: the leader wears a gold crown");
                Assert.GreaterOrEqual(leader.Count(Royal), 6, $"{look}: the leader wears royal purple");
                for (int accent = 0; accent < NpcDesignArt.AccentCount; accent++)
                    Assert.AreEqual(0, NpcDesignArt.Pixels(look, false, accent).Count(Royal), $"{look} accent {accent}: royal purple is the leaders'");
                // A leader never varies; ordinary NPCs vary only by tint (the same silhouette in every accent).
                Assert.AreEqual(leader, NpcDesignArt.Pixels(look, true, 2), $"{look}: leaders do not vary");
                var accents = Enumerable.Range(0, NpcDesignArt.AccentCount).Select(a => NpcDesignArt.Pixels(look, false, a)).ToList();
                Assert.AreEqual(accents.Count, accents.Select(a => string.Join(",", a.Select(c => $"{c.r}.{c.g}.{c.b}.{c.a}"))).Distinct().Count(),
                    $"{look}: every accent looks different");
                foreach (Color32[] a in accents)
                    Assert.AreEqual(0, Diff(Mask(folk), Mask(a)), $"{look}: an accent changes the silhouette");
            }

            // The accent is seeded by the salt (no random draw), and every accent turns up.
            var seen = new HashSet<int>();
            for (uint salt = 0; salt < 64; salt++)
            {
                int accent = NpcDesignArt.AccentOf(salt);
                Assert.That(accent, Is.InRange(0, NpcDesignArt.AccentCount - 1));
                Assert.AreEqual(accent, NpcDesignArt.AccentOf(salt));
                seen.Add(accent);
            }
            Assert.AreEqual(NpcDesignArt.AccentCount, seen.Count);
        }

        // ---------- Dialogue and text ----------

        [Test]
        public void Conversations_AreAtMostTwoLines_EndingOnTheFirstTrueHint()
        {
            var library = AssetDatabase.LoadAssetAtPath<ModuleLibrary>("Assets/Modules/ModuleLibrary.asset");
            var items = AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Items/ItemCatalog.asset");
            for (int seed = 1; seed <= 10; seed++)
            {
                var model = new CubeModel(seed);
                CubeLayout layout = CubeLayout.Generate(model, library);
                ItemPlacement placement = ItemPlacement.ForRun(model, layout, library, items.Themes(), items.VariantCounts());
                var facts = new RunFacts(model, layout, placement);
                foreach (HintDensity density in new[] { HintDensity.Sparse, HintDensity.Full })
                foreach (Race race in RaceExtensions.All)
                {
                    NpcSpec spec = NpcFactory.ChooseParts(seed, race, 0);
                    List<string> lines = Dialogue.For(spec, facts, density);
                    Assert.AreEqual(Dialogue.MaxLines, lines.Count, $"seed {seed} {race}");
                    Assert.AreEqual(Dialogue.RoleLine(spec), lines[0], "The flavour line");
                    Assert.AreEqual(HintGenerator.Generate(facts, spec.HintKind, spec.Salt, density)[0].Text, lines[1],
                        "The first hint, which HintGenerator makes true for the run");
                    foreach (string line in lines)
                        StringAssert.DoesNotContain(Dialogue.ForbiddenWord, line.ToLowerInvariant(), line);
                }
            }
            CollectionAssert.AreEqual(new[] { "flavour" }, Dialogue.Conversation("flavour", new List<Hint>()));
            Assert.AreEqual(2, PhysicsPopulation.NewtonLines().Count);
        }

        [Test]
        public void TheDeathText_IsPlain_OnTheHudAndTheBetweenRunsScreen()
        {
            Assert.AreEqual("You were defeated", CombatHud.DeathMessage);
            Assert.AreEqual("You were defeated", BetweenRunsScreen.DefeatTitle);
        }
    }
}
