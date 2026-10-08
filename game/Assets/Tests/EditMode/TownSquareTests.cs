using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Towns on every face (4.5), pure: each race's town (TownLayout) has buildings in its own style, a statue and a marked
    /// door, and keeps the doorways and lanes clear; on seeds 1-50 every face's centre is its town and nothing else is ever
    /// placed there, while the content that used to sit on centres (cores, islands, trials, gates, pickups, hidden spots)
    /// still turns up elsewhere on its face; the leaders greet the player with their region's quest hook in their voice; the
    /// Town face's town takes its first NPCs on its stands; and the town art keeps the people's pixel style.
    /// </summary>
    public class TownSquareTests
    {
        private const string LibraryPath = "Assets/Modules/ModuleLibrary.asset";
        private const string ItemCatalogPath = "Assets/Items/ItemCatalog.asset";
        private const int N = CubeSettings.DefaultFaceSize;

        private static readonly Theme[] AllThemes =
            { Theme.Town, Theme.Biology, Theme.Chemistry, Theme.Physics, Theme.Math, Theme.EarthAtmosphere };

        private static ModuleLibrary Library()
        {
            var library = AssetDatabase.LoadAssetAtPath<ModuleLibrary>(LibraryPath);
            Assert.IsNotNull(library, $"{LibraryPath} missing: run Cube > Build Module Library");
            return library;
        }

        private static ItemCatalog Catalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogPath);
            Assert.IsNotNull(catalog, $"{ItemCatalogPath} missing: run Cube > Build Item Catalog");
            return catalog;
        }

        // ---------- The towns ----------

        [Test]
        public void EveryRacesTown_HasBuildingsInItsOwnStyle_AStatue_AndOneMarkedDoor_AndKeepsEveryDoorwayAndLaneClear()
        {
            var kindsSeen = new Dictionary<TownBuildingKind, Theme>();
            Vector2 half = ScreenEdges.Half;
            float lane = ScreenModule.ExitLaneHalfWidth;
            Rect vertical = Rect.MinMaxRect(-lane, -half.y, lane, half.y), horizontal = Rect.MinMaxRect(-half.x, -lane, half.x, lane);
            foreach (Theme theme in AllThemes)
            {
                TownLayout town = TownLayout.For(theme);
                CollectionAssert.IsEmpty(town.Problems(), $"{theme}: {string.Join("; ", town.Problems())}");
                Assert.GreaterOrEqual(town.Buildings.Count, 3, $"{theme}: buildings");
                Assert.AreEqual(1, town.Buildings.Count(b => b.Enterable), $"{theme}: one door marked for 4.6");
                Assert.GreaterOrEqual(town.Buildings.Select(b => b.Kind).Distinct().Count(), 2, $"{theme}: more than one kind of building");
                foreach (TownBuilding b in town.Buildings)
                {
                    Assert.AreEqual(theme, TownLayout.ThemeOf(b.Kind), $"{theme}: {b} is in another race's style");
                    if (kindsSeen.TryGetValue(b.Kind, out Theme other)) Assert.AreEqual(theme, other, $"{b.Kind} is shared by two races");
                    kindsSeen[b.Kind] = theme;
                }
                Assert.Greater(town.StatueSolid.width * town.StatueSolid.height, 0f, $"{theme}: a statue");
                Assert.IsTrue(town.Plaza.Overlaps(town.StatueSolid), $"{theme}: the statue stands in the plaza");
                Assert.IsTrue(town.Plaza.Contains(Vector2.zero), $"{theme}: the plaza is central");
                Assert.GreaterOrEqual(town.Stands.Count, TownSquare.ResidentCount, $"{theme}: room for the residents");

                // The four doorways and both lanes stay clear, so the town never blocks a way through.
                foreach (Rect solid in town.Solids)
                {
                    Assert.IsTrue(ScreenModule.KeepsExitsOpen(solid), $"{theme}: {solid}");
                    Assert.IsFalse(solid.Overlaps(vertical) || solid.Overlaps(horizontal), $"{theme}: {solid} is on a lane");
                    foreach (Facing side in Enum.GetValues(typeof(Facing)))
                        Assert.IsFalse(solid.Overlaps(ScreenEdges.DoorwayRect(side)), $"{theme}: {solid} blocks the {side} doorway");
                }
            }
            Assert.AreEqual(Enum.GetValues(typeof(TownBuildingKind)).Length, kindsSeen.Count, "Every kind of building is used");
        }

        [Test]
        public void Library_EveryFaceHasATownModule_ExitsOnly_AfterTheRegularModules()
        {
            ModuleLibrary library = Library();
            Assert.IsNotNull(library.TownSquareModule, "Town's own town module");
            Assert.AreSame(library.TownSquareModule, library.TownModule(TownSquare.Cell(N), N), "The start screen is Town's town");
            Assert.AreEqual(Theme.Town, library.TownSquareModule.Theme);
            var modules = new List<ScreenModule> { library.TownSquareModule };
            foreach (Theme theme in CubeModel.ScienceThemes)
            {
                IReadOnlyList<ScreenModule> pool = library.Pool(theme);
                int town = library.TownModuleIndex(theme);
                Assert.GreaterOrEqual(town, 0, $"{theme} has a town module");
                Assert.AreEqual(pool.Count - (theme == Theme.Math || theme == Theme.EarthAtmosphere ? 2 : 1), town, $"{theme}: the town module ends the regular modules");
                Assert.AreEqual(theme, pool[town].Theme);
                modules.Add(pool[town]);
            }
            foreach (ScreenModule module in modules)
            {
                Assert.IsTrue(module.IsTownScreen, module.name);
                Assert.IsFalse(module.IsPuzzleScreen, module.name);
                Assert.AreEqual(4, module.Exits.Length, module.name);
                Assert.IsEmpty(module.Gates, $"{module.name}: no gate slot");
                Assert.IsEmpty(module.HiddenItems, $"{module.name}: no hidden spot");
                Assert.IsEmpty(module.CoreEntrances, $"{module.name}: no core portal");
                Assert.IsEmpty(module.GetComponentsInChildren<BoxCollider2D>(true), $"{module.name}: the town is built on it");
                // Its town's solids count as blocked for every plan that reads the module.
                List<Rect> blocked = TownPlan.BlockedRects(module);
                Assert.AreEqual(TownLayout.For(module.Theme).Solids.Count, blocked.Count, module.name);
            }
        }

        [Test]
        public void Seeds1To50_EveryFaceCentreIsItsTown_AndNothingElseIsEverPlacedThere()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            List<BeakKind> beaks = BiologyBeaks.VariantKinds(catalog);
            List<IsotopeStage> stones = ChemistryIsotope.VariantKinds(catalog);
            List<Phenomenon> wands = StormcallerItem.VariantKinds(catalog);
            Vector2Int centre = TownSquare.Cell(N);
            for (int seed = 1; seed <= 50; seed++)
            {
                var model = new CubeModel(seed);
                CubeLayout layout = CubeLayout.Generate(model, library);
                ItemPlacement placement = ItemPlacement.ForRun(model, layout, library, catalog.Themes(), catalog.VariantCounts());
                Func<ScreenAddress, ScreenModule> moduleAt = SeedSweep.ModuleLookup(layout, library);
                List<string> problems = SeedSweep.FindTownProblems(model, layout, library, placement, beaks, stones, wands, moduleAt);
                CollectionAssert.IsEmpty(problems, $"seed {seed}: {string.Join("; ", problems)}");

                Assert.AreEqual(model.StartScreen.Cell, centre, "Town's start screen is its town");
                Assert.IsTrue(library.TownModule(centre, N).IsTownScreen, $"seed {seed}: Town's centre");
                Assert.AreEqual(5, layout.Faces.Count);
                foreach (FaceLayout fl in layout.Faces.Values)
                {
                    Assert.AreEqual(centre, fl.TownCell, $"seed {seed} {fl.Face}");
                    Assert.IsTrue(moduleAt(new ScreenAddress(fl.Face, centre)).IsTownScreen, $"seed {seed} {fl.Face}: the centre is its town");
                    Assert.AreNotEqual(centre, fl.CoreCell, $"seed {seed} {fl.Face}: the core portal moved off the centre");
                    CollectionAssert.DoesNotContain(fl.IslandCells, centre, $"seed {seed} {fl.Face}: an island screen moved off the centre");
                    // Every other cell holds a regular module (never a town or island module drawn at random).
                    for (int y = 0; y < N; y++)
                        for (int x = 0; x < N; x++)
                        {
                            var cell = new Vector2Int(x, y);
                            if (cell == centre) continue;
                            Assert.IsFalse(moduleAt(new ScreenAddress(fl.Face, cell)).IsTownScreen, $"seed {seed} {fl.Face} {cell}");
                        }
                }
                foreach (HiddenCurrencySpot spot in HiddenCurrencyPlacement.Generate(model, layout, library).Spots)
                    Assert.IsFalse(TownSquare.IsTown(spot.Screen, N), $"seed {seed}: {spot} in a town");
            }
        }

        [Test]
        public void DisplacedContent_StillTurnsUp_ElsewhereOnItsFace_OverSeeds1To50()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            List<BeakKind> beaks = BiologyBeaks.VariantKinds(catalog);
            List<IsotopeStage> stones = ChemistryIsotope.VariantKinds(catalog);
            List<Phenomenon> wands = StormcallerItem.VariantKinds(catalog);
            int coreWasCentre = 0, trialsWereCentre = 0, trialsWouldHaveBeenCentre = 0;
            int centreIndex = TownSquare.Cell(N).y * N + TownSquare.Cell(N).x;
            for (int seed = 1; seed <= 50; seed++)
            {
                var model = new CubeModel(seed);
                CubeLayout layout = CubeLayout.Generate(model, library);
                ItemPlacement placement = ItemPlacement.ForRun(model, layout, library, catalog.Themes(), catalog.VariantCounts());
                Func<ScreenAddress, ScreenModule> moduleAt = SeedSweep.ModuleLookup(layout, library);
                CollectionAssert.IsEmpty(SeedSweep.FindCoreProblems(model, layout, library), $"seed {seed}: a core portal on every face");

                // Every face keeps its gates and its item's pickup.
                foreach (Theme theme in CubeModel.ScienceThemes)
                {
                    FaceId face = model.FaceOf(theme);
                    Assert.Greater(placement.Gates.Count(g => g.Screen.Face == face && g.Item == theme), 0, $"seed {seed}: {theme} gates");
                    Assert.IsTrue(placement.TryGetPickup(theme, out PickupPlacement pickup) && pickup.Screen.Face == face, $"seed {seed}: {theme} pickup");
                }

                // Every face's trial (and Math's islands, Physics' doors and boulders, Earth's calm eye) is still there.
                BiologyPlan biology = BiologyPlan.Create(model, placement, beaks);
                Assert.AreEqual(model.FaceOf(Theme.Biology), biology.TrialScreen.Face);
                ChemistryPlan chemistry = ChemistryPlan.Create(model, placement, stones);
                Assert.AreEqual(model.FaceOf(Theme.Chemistry), chemistry.TrialScreen.Face);
                PhysicsPlan physics = PhysicsPlan.Create(model, placement, moduleAt, beaks, PhysicsPlan.DefaultMaxRequired, stones);
                Assert.IsNotNull(physics.Trial, $"seed {seed}: the Physics trial fits");
                Assert.Greater(physics.Doors.Count, 0, $"seed {seed}: timed doors");
                Assert.IsTrue(physics.Doors.All(d => d.Boulders.Count >= d.Required), $"seed {seed}: boulders by every door");
                MathPlan math = MathPlan.Create(model, placement, moduleAt, beaks, stones);
                Assert.AreEqual(CubeLayout.IslandScreensPerFace(N), math.IslandScreens.Count, $"seed {seed}: island screens");
                Assert.IsTrue(math.HasTrial, $"seed {seed}: the Math trial island");
                Assert.Greater(math.Gates.Count, 0, $"seed {seed}: gap gates");
                EarthPlan earth = EarthPlan.Create(model, placement, moduleAt, wands, beaks, stones);
                Assert.IsTrue(earth.TrialSpots != null && earth.TrialSpots.Count >= EarthTrial.PieceCount, $"seed {seed}: the Earth trial fits");
                Assert.AreEqual(new ScreenAddress(earth.Face, TownSquare.Cell(N)), earth.Eye, "The calm eye hosts the Earth town");
                Assert.IsNull(earth.HazardOn(earth.Eye), $"seed {seed}: the town is calm");

                // Count how often the old draws would have used the centre: the content really was displaced.
                var old = new SeededRng(unchecked((ulong)(uint)seed), CubeLayout.RngStream);
                if (old.NextInt(N * N) == N * N / 2) coreWasCentre++;
                foreach (ScreenAddress t in new[] { biology.TrialScreen, chemistry.TrialScreen, physics.TrialScreen, earth.TrialScreen })
                    if (TownSquare.IsTown(t, N)) trialsWereCentre++;

                // The old trial draws, replayed: Chemistry's trial is its plan stream's first draw; Physics' is the head of its
                // seeded screen order, after one required and one spare draw per Physics gate. Where the old draw was the centre
                // the trial now stands elsewhere; where it was not, the new plan kept the old screen.
                var chemistryRng = new SeededRng(unchecked((ulong)(uint)seed), ChemistryPlan.RngStream);
                int oldChemistry = chemistryRng.NextInt(N * N);
                if (oldChemistry == centreIndex) trialsWouldHaveBeenCentre++;
                else Assert.AreEqual(new Vector2Int(oldChemistry % N, oldChemistry / N), chemistry.TrialScreen.Cell, $"seed {seed}: the replay matches");
                var physicsRng = new SeededRng(unchecked((ulong)(uint)seed), PhysicsPlan.RngStream);
                foreach (GatePlacement g in placement.Gates.Where(g => g.Item == Theme.Physics))
                {
                    physicsRng.NextInt(PhysicsPlan.DefaultMaxRequired);
                    physicsRng.NextInt(2);
                }
                var order = new List<ScreenAddress>();
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                        order.Add(new ScreenAddress(model.FaceOf(Theme.Physics), x, y));
                physicsRng.Shuffle(order);
                if (TownSquare.IsTown(order[0], N)) trialsWouldHaveBeenCentre++;
            }
            Assert.Greater(coreWasCentre, 0, "Some seeds drew the centre for a core portal before towns; it moved");
            Assert.AreEqual(0, trialsWereCentre);
            Assert.Greater(trialsWouldHaveBeenCentre, 0, "Some seeds drew the centre for a trial before towns; it moved");
        }

        // ---------- People ----------

        [Test]
        public void Leaders_GreetThePlayer_ThenGiveTheirRegionsQuestHook_InTheirVoice()
        {
            foreach (Race race in RaceExtensions.All)
            {
                NpcSpec leader = NpcFactory.ChooseLeader(7, race, 0);
                List<string> lines = Dialogue.For(leader, null, HintDensity.Full);
                CollectionAssert.AreEqual(new[] { Dialogue.LeaderLine(race), Dialogue.QuestLine(race) }, lines, $"{race}");
                Assert.LessOrEqual(lines.Count, Dialogue.MaxLines);
                string quest = Dialogue.QuestLine(race);
                StringAssert.Contains("mend", quest, $"{race}: asks for help mending the region");
                Assert.LessOrEqual(quest.Length, Dialogue.MaxLineLength, race.ToString());
                CollectionAssert.IsEmpty(InWorldText.Problems(quest), $"{race}: {quest}");
                if (race != Race.Townsfolk)
                {
                    Theme theme = AllThemes.Single(t => TownSquare.RaceOf(t) == race);
                    StringAssert.Contains(InWorldText.Region(theme).Replace("the ", ""), quest, $"{race}: names its region");
                }
            }
            // One source of a leader's lines: For, Lines (whatever hints are given) and FlavourLine agree.
            foreach (Race race in RaceExtensions.All)
            {
                NpcSpec leader = NpcFactory.ChooseLeader(9, race, TownSquare.LeaderIndex);
                List<string> said = Dialogue.LeaderConversation(race);
                CollectionAssert.AreEqual(said, Dialogue.For(leader, null, HintDensity.Sparse), $"{race}: For");
                CollectionAssert.AreEqual(said, Dialogue.Lines(leader, null), $"{race}: Lines without hints");
                CollectionAssert.AreEqual(said, Dialogue.Lines(leader, new[] { new Hint(HintKind.FaceTheme, false, FaceId.Right, Theme.Biology, Theme.Biology, default, "a hint") }), $"{race}: Lines with hints");
                Assert.AreEqual(said[0], Dialogue.FlavourLine(leader), $"{race}: FlavourLine");
                // Shift+F3's debug leaders never are a town's leader.
                for (int batch = 0; batch < 4; batch++)
                    Assert.AreNotEqual(TownSquare.LeaderSpec(9, AllThemes.First(t => TownSquare.RaceOf(t) == race)).Signature(),
                        NpcFactory.ChooseLeader(9, race, CubeDebug.DebugLeaderIndexBase + batch).Signature(), $"{race}: debug batch {batch}");
            }
            Assert.AreEqual(NpcNames.Leader(Race.Dinosaur), TownSquare.LeaderSpec(3, Theme.EarthAtmosphere).Name, "Chief Rockjaw leads the Earth town");
            Assert.AreEqual(DinoVariant.Terrestrial, TownSquare.LeaderSpec(3, Theme.EarthAtmosphere).Variant);
            Assert.AreEqual(NpcNames.Leader(Race.Townsfolk), TownSquare.LeaderSpec(3, Theme.Town).Name, "Mayor Muddleton leads Town");
            foreach (Theme theme in AllThemes)
            {
                Assert.IsTrue(TownSquare.LeaderSpec(5, theme).IsLeader);
                Assert.AreEqual(TownSquare.RaceOf(theme), TownSquare.LeaderSpec(5, theme).Race);
            }
            foreach (Theme theme in CubeModel.ScienceThemes)
                for (int i = 0; i < TownSquare.ResidentCount; i++)
                {
                    NpcSpec resident = TownSquare.ResidentSpec(5, theme, i);
                    Assert.AreEqual(TownSquare.RaceOf(theme), resident.Race);
                    Assert.IsFalse(resident.IsLeader);
                }
        }

        [Test]
        public void TownFace_ItsTownTakesTheFirstNpcsOnItsStands_SoItIsTheBusiestTownScreen()
        {
            ModuleLibrary library = Library();
            TownLayout town = TownLayout.For(Theme.Town);
            Assert.AreEqual(town.Stands.Count, TownPlan.StartScreenNpcsFor(N));
            Assert.AreEqual(TownPlan.StartScreenNpcs, TownPlan.StartScreenNpcsFor(2), "A face too small for a town keeps the old start crowd");
            for (int seed = 1; seed <= 50; seed++)
            {
                int count = TownPlan.Specs(seed, N).Count;
                List<TownSpot> spots = TownPlan.Spots(seed, N, count, cell => TownPlan.BlockedRects(library.TownModule(cell, N)));
                var start = new ScreenAddress(CubeModel.StartFace, TownSquare.Cell(N));
                List<TownSpot> onStart = spots.Where(s => s.Screen == start).ToList();
                Assert.AreEqual(town.Stands.Count, onStart.Count, $"seed {seed}: the town's stands are filled");
                CollectionAssert.AreEquivalent(town.Stands, onStart.Select(s => s.Local), $"seed {seed}: on the stands");
                // The town (and the Mayor besides) outnumbers every other Town screen.
                int busiestOther = spots.Where(s => s.Screen != start).GroupBy(s => s.Screen).Select(g => g.Count()).DefaultIfEmpty(0).Max();
                Assert.Greater(onStart.Count + 1, busiestOther, $"seed {seed}");
            }
        }

        // ---------- Art ----------

        [Test]
        public void TownArt_IsOutlinedPixelArt_AtThePeoplesSize_AndEveryBuildingAndStatueLooksItsOwn()
        {
            Assert.AreEqual(8f, TownLayout.PixelsPerUnit, "The people's pixel size");
            var silhouettes = new Dictionary<TownBuildingKind, bool[]>();
            foreach (TownBuildingKind kind in Enum.GetValues(typeof(TownBuildingKind)))
            {
                TownArt.Canvas c = TownArt.BuildingCanvas(kind);
                int opaque = c.Pixels.Count(p => p.a > 0);
                int outline = c.Pixels.Count(p => p.a > 0 && p.r == TownArt.OutlineColor.r && p.g == TownArt.OutlineColor.g && p.b == TownArt.OutlineColor.b);
                Assert.Greater(opaque, TownArt.BuildingPixels * TownArt.BuildingPixels / 3, $"{kind}: a solid building");
                Assert.Greater(outline, 4 * TownArt.BuildingPixels, $"{kind}: outlined in the people's plum");
                // The canvas edge is the outline's outer ring or empty: nothing is cut off.
                for (int x = 0; x < c.Width; x++)
                    foreach (int y in new[] { 0, c.Height - 1 })
                    {
                        Color32 p = c.Pixels[y * c.Width + x];
                        Assert.IsTrue(p.a == 0 || (p.r == TownArt.OutlineColor.r && p.b == TownArt.OutlineColor.b), $"{kind}: cut off at ({x},{y})");
                    }
                silhouettes[kind] = c.Pixels.Select(p => p.a > 0).ToArray();
                Sprite sprite = TownArt.Building(kind);
                Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode, kind.ToString());
                Assert.AreEqual(TownLayout.PixelsPerUnit, sprite.pixelsPerUnit, kind.ToString());
                Assert.AreEqual((TownArt.BuildingPixels + 2 * TownArt.Margin) / TownLayout.PixelsPerUnit, sprite.bounds.size.x, 1e-4f, kind.ToString());
            }
            // The cottages are Kenney tiles in the game (their code-drawn fallback is a plain block), so they are left out.
            var kinds = silhouettes.Keys.Where(k => !TownArt.IsKenney(k)).ToList();
            foreach (TownBuildingKind a in kinds)
                foreach (TownBuildingKind b in kinds)
                {
                    if (a >= b) continue;
                    int differ = silhouettes[a].Zip(silhouettes[b], (x, y) => x != y).Count(d => d);
                    Assert.Greater(differ, 20, $"{a} and {b} look alike");
                }

            var statues = new List<string>();
            foreach (Theme theme in AllThemes)
            {
                TownArt.Canvas c = TownArt.StatueCanvas(theme);
                Assert.Greater(c.Pixels.Count(p => p.a > 0), 150, $"{theme}: a statue");
                foreach (Color32 p in c.Pixels.Where(p => p.a > 0))
                {
                    Color.RGBToHSV(p, out _, out float saturation, out _);
                    Assert.Less(saturation, 0.45f, $"{theme}: the statue is stone, not paint");
                }
                statues.Add(string.Join("", c.Pixels.Select(p => p.a > 0 ? '#' : '.')));
                Assert.AreEqual(new Vector2(TownArt.StatueWidth, TownArt.StatueHeight) / TownLayout.PixelsPerUnit,
                    TownLayout.For(theme).StatueArt.size, $"{theme}: the statue's art rect fits its sprite");
            }
            Assert.AreEqual(statues.Count, statues.Distinct().Count(), "Every race's statue is its own leader");
        }
    }
}
