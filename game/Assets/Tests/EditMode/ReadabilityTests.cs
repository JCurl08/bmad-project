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
    /// conversations are at most two lines ending on a true hint, and the death text is plain.
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
            ArtKey.GateCrackedWall, ArtKey.GateLeadDoor, ArtKey.GateTimedDoor,
        };

        [Test]
        public void GateTiles_AreNotFromTheRuinFamilies_SoAGateNeverReadsAsAWalkableRuin()
        {
            // Ruin tile families: the Town arch pieces (111-114) and the Dungeon columns (57-59).
            var ruinFamilies = new HashSet<string>();
            for (int t = 111; t <= 114; t++) ruinFamilies.Add($"Tiny Town/tile_{t:0000}");
            for (int t = 57; t <= 59; t++) ruinFamilies.Add($"Tiny Dungeon/tile_{t:0000}");
            Dictionary<ArtKey, string> tiles = Catalog().Entries.ToDictionary(e => e.Key, e => e.Tile);
            foreach (ArtKey ruin in new[] { ArtKey.RuinArch, ArtKey.RuinPillar }) ruinFamilies.Add(tiles[ruin]);
            foreach (ArtKey gate in GateKeys.Concat(new[] { ArtKey.VineBridge }))
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

                // A plain item pickup and the isotope dispenser, both as the reveal places them behind a gate.
                GameObject plain = world.CreateBlock("Pickup", root.transform, pocket, Vector2.one * CubeWorld.PickupSize, Color.white,
                    5, false, ArtKey.ItemThickBeak);
                CubeWorld.TuckIntoPocket(plain.transform, slot);
                IsotopeDispenser dispenser = IsotopeDispenser.Create(root.transform, pocket, ChemistryIsotope.CreateItem(), null);
                CubeWorld.TuckIntoPocket(dispenser.transform, slot);

                float gateFar = Vector2.Dot((Vector2)gate.Visual.bounds.center - (Vector2)slot.transform.position, dir) +
                                Mathf.Abs(Vector2.Dot(gate.Visual.bounds.extents, dir));
                Assert.AreEqual(ArtCatalog.GateArtReach(thickness), gateFar, 1e-3f, "Gate art reaches one tile into the pocket");
                foreach (SpriteRenderer r in plain.GetComponentsInChildren<SpriteRenderer>()
                             .Concat(dispenser.GetComponentsInChildren<SpriteRenderer>()))
                {
                    float centre = Vector2.Dot((Vector2)r.bounds.center - (Vector2)slot.transform.position, dir);
                    float half = Mathf.Abs(Vector2.Dot(r.bounds.extents, dir));
                    Assert.Greater(centre - half, gateFar, $"{r.name} overlaps the closed gate art");
                    Assert.LessOrEqual(centre + half, back + 1e-3f, $"{r.name} pokes through the pocket back wall");
                    Assert.Greater(gate.Visual.sortingOrder, r.sortingOrder, $"The closed gate draws in front of {r.name}");
                }
                Assert.AreEqual(pocket, (Vector2)plain.transform.position, "The pickup itself (its trigger) stays at the pocket centre");
                Assert.AreEqual(pocket, (Vector2)dispenser.transform.position);
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
                ArtKeys.Item(ChemistryIsotope.CreateItem()), ArtKeys.Item(MassMittItem.CreateItem()),
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

        // ---------- NPC parts ----------

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
