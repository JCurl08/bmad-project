using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The look and tone pass (1.22). One scan over every player-visible string source: the fixed dialogue tables, the
    /// generated hints and every population's conversations for seeds 1-50 at both densities, the HUDs, popups and boss
    /// text, the item names, the map, the dialogue box and the between-runs screen. Each must keep InWorldText's rule: no
    /// real scientist's name, no science word of a face theme, and a star on every invented proper name. Also: hints name
    /// regions and gates by their look, the region colours follow a Rubik's cube, and finches are drawn in full colour.
    /// </summary>
    public class InWorldTextTests
    {
        private const string LibraryPath = "Assets/Modules/ModuleLibrary.asset";
        private const string ItemCatalogPath = "Assets/Items/ItemCatalog.asset";
        private const int Seeds = 50;

        private static ItemCatalog Catalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogPath);
            Assert.IsNotNull(catalog, $"{ItemCatalogPath} missing");
            return catalog;
        }

        private static ModuleLibrary Library()
        {
            var library = AssetDatabase.LoadAssetAtPath<ModuleLibrary>(LibraryPath);
            Assert.IsNotNull(library, $"{LibraryPath} missing");
            return library;
        }

        private static IEnumerable<ItemDefinition> AllItems(ItemCatalog catalog) =>
            catalog.Items.Concat(catalog.Variants).Where(i => i != null);

        /// <summary>Every fixed (seed-independent) player-visible string, by source.</summary>
        private static IEnumerable<(string Source, string Text)> FixedStrings()
        {
            foreach (string line in Dialogue.AllFixedText()) yield return ("Dialogue", line);
            foreach (string line in TownSign.WelcomeLines) yield return ("TownSign", line);
            yield return ("DialogueBox", DialogueBox.NextPrompt);
            yield return ("DialogueBox", DialogueBox.ClosePrompt);
            yield return ("CombatHud", CombatHud.DeathMessage);
            yield return ("CombatHud", CombatHud.BareHandsLabel);
            yield return ("CombatHud", "Equipped: " + CombatHud.BareHandsLabel);
            foreach (string line in CoreHud.AllText()) yield return ("CoreHud", line);
            yield return ("CoreHud", $"{CoreHud.MeterLabel}   ({new MixingPhase().Name})");
            yield return ("CoreHud", $"{CoreHud.MeterLabel}   ({new FallbackBoss().Name})");
            foreach (string line in CubeMap.AllTexts()) yield return ("CubeMap", line);
            yield return ("IslandScreen", IslandScreen.MendedText);
            yield return ("IslandScreen", IslandScreen.LoneText);
            yield return ("IslandScreen", IslandScreen.SolvedText);
            yield return ("IceScreen", IceScreen.GoalText);
            yield return ("IceSlider", IceSlider.FallText);
            yield return ("WindScreen", WindScreen.GoalText);
            foreach (string line in MassMitt.AllHudText) yield return ("MassMitt HUD", line);
            foreach (string line in ChemistryIsotope.AllText()) yield return ("ChemistryIsotope", line);
            foreach (IsotopeStage stage in ChemistryIsotope.VariantOrder)
                yield return ("Isotope HUD", $"{ChemistryIsotope.StageName(stage)}: {ChemistryIsotope.StageUse(stage)}");
            foreach (string line in StormcallerItem.AllText()) yield return ("StormcallerItem", line);
            foreach (string look in HintGenerator.AllGateLooks()) yield return ("Gate looks", look);
            foreach (Theme theme in new[] { Theme.Town, Theme.Biology, Theme.Chemistry, Theme.Physics, Theme.Math, Theme.EarthAtmosphere })
            {
                yield return ("Regions", HintGenerator.ThemeName(theme));
                yield return ("Nicknames", HintGenerator.ItemNickname(theme));
            }
            foreach (ItemDefinition item in AllItems(Catalog()))
            {
                yield return ("Item names", item.DisplayName);
                yield return ("Pickup popups", PickupFeedback.ItemText(item));
                yield return ("CombatHud", "Equipped: " + item);
            }
            foreach (int amount in new[] { 1, 5, 25 }) yield return ("Pickup popups", PickupFeedback.JumbleText(amount));
            foreach (string line in RewardText.All()) yield return ("Roadblock rewards", line);
            foreach (string line in LockText.All()) yield return ("Locks", line);

            // Enemy names (they name the enemy objects and may show in later UI).
            foreach (BiologyEnemyKind k in Enum.GetValues(typeof(BiologyEnemyKind))) yield return ("Enemies", BiologyEnemies.Name(k));
            foreach (ChemistryEnemyKind k in Enum.GetValues(typeof(ChemistryEnemyKind))) yield return ("Enemies", ChemistryEnemies.Name(k));
            foreach (PhysicsEnemyKind k in Enum.GetValues(typeof(PhysicsEnemyKind))) yield return ("Enemies", PhysicsEnemies.Name(k));
            foreach (MathEnemyKind k in Enum.GetValues(typeof(MathEnemyKind))) yield return ("Enemies", MathEnemies.Name(k));
            foreach (EarthEnemyKind k in Enum.GetValues(typeof(EarthEnemyKind))) yield return ("Enemies", EarthEnemies.Name(k));

            // The between-runs screen and the upgrade shop.
            yield return ("BetweenRuns", BetweenRunsScreen.VictoryTitle);
            yield return ("BetweenRuns", BetweenRunsScreen.DefeatTitle);
            yield return ("BetweenRuns", BetweenRunsScreen.ContinueLabel);
            yield return ("BetweenRuns", BetweenRunsScreen.SpendLabel);
            yield return ("BetweenRuns", BetweenRunsScreen.RunLine(3, 1234, 1));
            for (int i = 0; i < UpgradeShop.All.Length; i++) yield return ("BetweenRuns", BetweenRunsScreen.BuyLabel(i));
            var earnings = new RunEarnings();
            earnings.Add(EarningSource.Trial, "trial:1", 25);
            earnings.Add(EarningSource.Boss, "boss", RunWallet.BossVictory);
            earnings.Add(EarningSource.Hidden, "hidden:0", 5);
            foreach (string line in BetweenRunsScreen.EarningsLinesFor(earnings, 80, 120)) yield return ("BetweenRuns", line);
            foreach (UpgradeStat stat in UpgradeShop.All)
            {
                yield return ("UpgradeShop", UpgradeShop.Name(stat));
                yield return ("UpgradeShop", UpgradeShop.Blurb(stat));
                for (int level = 0; level <= UpgradeShop.MaxLevel; level++)
                    yield return ("BetweenRuns", BetweenRunsScreen.UpgradeLineFor(stat, level, 1));
                var save = new MetaSave { Balance = 0 };
                yield return ("UpgradeShop", UpgradeShop.TryBuy(save, stat).Message);
                save.Balance = 999;
                yield return ("UpgradeShop", UpgradeShop.TryBuy(save, stat).Message);
                save.SetUpgradeLevel(UpgradeShop.Id(stat), UpgradeShop.MaxLevel);
                yield return ("UpgradeShop", UpgradeShop.TryBuy(save, stat).Message);
            }
        }

        /// <summary>Every seeded string for seeds 1-50: all hints, every population's conversations, the speaker names.</summary>
        private static IEnumerable<(string Source, string Text)> SeededStrings()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var model = new CubeModel(seed);
                CubeLayout layout = CubeLayout.Generate(model, library);
                ItemPlacement placement = ItemPlacement.ForRun(model, layout, library, catalog.Themes(), catalog.VariantCounts());
                RunFacts facts = RunFacts.Planned(model, layout, placement, library, catalog);
                int n = model.FaceSize;
                BeakKind beak = BiologyBeaks.Roll(seed);
                IsotopeStage stage = ChemistryIsotope.KindOf(catalog.ItemFor(Theme.Chemistry, seed)) ?? IsotopeStage.Glow;
                Phenomenon wand = StormcallerItem.KindOf(catalog.ItemFor(Theme.EarthAtmosphere, seed)) ?? Phenomenon.Quake;
                foreach (HintDensity density in new[] { HintDensity.Sparse, HintDensity.Full })
                {
                    foreach (Voice voice in Voices.All) // 4.4: hints in every voice
                    foreach (HintKind kind in Enum.GetValues(typeof(HintKind)))
                        for (uint salt = 0; salt < 12; salt++)
                            foreach (Hint h in HintGenerator.Generate(facts, kind, salt, density, voice))
                                yield return ($"Hint {voice} (seed {seed})", h.Text);
                    foreach (NpcSpec spec in TownPlan.Specs(seed, n))
                    {
                        List<string> lines = Dialogue.For(spec, facts, density);
                        foreach (string line in lines) yield return ($"Town (seed {seed})", line);
                        foreach (string line in TownSign.GreeterLines(lines)) yield return ($"Town greeter (seed {seed})", line);
                        yield return ("Speaker", spec.DisplayName);
                    }
                    for (int i = 0; i < BiologyPopulation.FinchCount(n); i++)
                    {
                        NpcSpec spec = BiologyPopulation.FinchSpec(seed, i);
                        foreach (string line in BiologyPopulation.LinesFor(spec, facts, density, beak, i == 0)) yield return ($"Finch (seed {seed})", line);
                        yield return ("Speaker", spec.DisplayName);
                    }
                    for (int i = 0; i < ChemistryPopulation.MushroomCount(n); i++)
                    {
                        NpcSpec spec = ChemistryPopulation.MushroomSpec(seed, i);
                        foreach (string line in ChemistryPopulation.LinesFor(spec, facts, density, stage, i)) yield return ($"Mushroom (seed {seed})", line);
                        yield return ("Speaker", spec.DisplayName);
                    }
                    for (int i = 0; i < PhysicsPopulation.AlienCount(n); i++)
                    {
                        NpcSpec spec = PhysicsPopulation.AlienSpec(seed, i);
                        foreach (string line in PhysicsPopulation.LinesFor(spec, facts, density, i == 0)) yield return ($"Alien (seed {seed})", line);
                        yield return ("Speaker", spec.DisplayName);
                    }
                    for (int i = 0; i < MathPopulation.ShapeCount(n); i++)
                    {
                        NpcSpec spec = MathPopulation.ShapeSpec(seed, i);
                        foreach (string line in MathPopulation.LinesFor(spec, facts, density, i)) yield return ($"Shape (seed {seed})", line);
                        yield return ("Speaker", spec.DisplayName);
                    }
                    for (int i = 0; i < EarthPopulation.DinoCount(n); i++)
                    {
                        NpcSpec spec = EarthPopulation.DinoSpec(seed, i);
                        foreach (string line in EarthPopulation.LinesFor(spec, facts, density, wand, i)) yield return ($"Dinosaur (seed {seed})", line);
                        yield return ("Speaker", spec.DisplayName);
                    }
                }
                foreach (string line in PhysicsPopulation.NewtonLines()) yield return ("Apple sage", line);
                yield return ("Speaker", PhysicsPopulation.NewtonSpec(seed).DisplayName);
                // The science faces' town residents (4.5).
                foreach (Theme theme in CubeModel.ScienceThemes)
                    for (int i = 0; i < TownSquare.ResidentCount; i++)
                    {
                        NpcSpec resident = TownSquare.ResidentSpec(seed, theme, i);
                        yield return ("Speaker", resident.DisplayName);
                        foreach (string line in Dialogue.For(resident, facts, HintDensity.Full)) yield return ($"Town resident (seed {seed})", line);
                    }
                // Leaders (4.3, spawned by 4.5): a few per race, so both dinosaur kinds turn up.
                foreach (Race race in RaceExtensions.All)
                    for (int i = 0; i < 4; i++)
                    {
                        NpcSpec leader = NpcFactory.ChooseLeader(seed, race, i);
                        yield return ("Speaker", leader.DisplayName);
                        foreach (string line in Dialogue.For(leader, facts, HintDensity.Full)) yield return ($"Leader (seed {seed})", line);
                    }
            }
        }

        [Test]
        public void EveryPlayerVisibleString_KeepsTheSubtleScienceRule_AndStarsItsPlaceholderNames()
        {
            List<string> starred = AllItems(Catalog()).Select(i => i.DisplayName)
                .Concat(UpgradeShop.All.Select(UpgradeShop.Name))
                .Where(name => name.EndsWith("*")).Distinct().ToList();
            var seen = new HashSet<string>();
            var offences = new List<string>();
            int count = 0;
            foreach ((string source, string text) in FixedStrings().Concat(SeededStrings()))
            {
                count++;
                if (!seen.Add(text)) continue;
                List<string> problems = InWorldText.Problems(text, starred);
                if (problems.Count > 0) offences.Add($"[{source}] \"{text}\": {string.Join("; ", problems)}");
            }
            Assert.Greater(count, 5000, "The scan covers the generated text of 50 seeds");
            Assert.Greater(seen.Count, 300);
            Assert.IsEmpty(offences, $"{offences.Count} strings break the rule:\n" + string.Join("\n", offences.Take(40)));
        }

        [Test]
        public void TheRule_CatchesNamesScienceWordsAndMissingStars()
        {
            CollectionAssert.IsNotEmpty(InWorldText.Problems("Professor Darwin says hello."), "A scientist");
            CollectionAssert.IsNotEmpty(InWorldText.Problems("It's all relative, darling!"), "A theme word");
            CollectionAssert.IsNotEmpty(InWorldText.Problems("Grab the Mass Mitt* now."), "Mass");
            CollectionAssert.IsNotEmpty(InWorldText.Problems("A Chemistry gate blocks a nook."), "A science's name");
            CollectionAssert.IsNotEmpty(InWorldText.Problems("Ask the Old Digger about it."), "A name without its star");
            CollectionAssert.IsNotEmpty(InWorldText.Problems("off to the greenwood we go."), "A known placeholder without its star");
            // 4.4: characters have chosen names (NpcNames), unstarred; shouts in capitals are not names.
            CollectionAssert.IsEmpty(InWorldText.Problems("Ask Queen Wrennifer, or Granny Trowel, or even Primsworth the Demon. How *dare* they! CLANG!"));
            CollectionAssert.IsEmpty(InWorldText.Problems("Say hi to Pipwick II and Old Whenever's comb."), "A repeated name's numeral");
            CollectionAssert.IsNotEmpty(InWorldText.Problems("Ask Queen Gertrude about it."), "An unchosen name");
            // Strict: only whole character names are chosen, never a lone word of one, and only listed shouts.
            CollectionAssert.IsNotEmpty(InWorldText.Problems("Ask the Queen about it."), "\"Queen\" alone is no name");
            CollectionAssert.IsNotEmpty(InWorldText.Problems("Bow to Wrennifer the Great."), "Half a name");
            CollectionAssert.IsNotEmpty(InWorldText.Problems("Then ask the NASA folk."), "Capitals are not a free pass");
            CollectionAssert.IsNotEmpty(InWorldText.Problems("Never sort the stones."), "The forbidden word");
            CollectionAssert.IsEmpty(InWorldText.Problems("Welcome to Town! The Partition split us. Ask the Old Digger*."));
            CollectionAssert.IsEmpty(InWorldText.Problems("They say the Greenwood* is right next door to town."));
            // 1.24: "entropy" may be said (the user's call); scientists stay banned.
            CollectionAssert.IsEmpty(InWorldText.Problems("Entropy wins! Everything mixes, and entropy always grows."), "Entropy is allowed");
            CollectionAssert.IsEmpty(InWorldText.Problems(CoreHud.MeterLabel));
            CollectionAssert.IsNotEmpty(InWorldText.Problems("Maxwell built the Partition."), "Still a scientist");
            CollectionAssert.IsNotEmpty(InWorldText.Problems("ask newton about the mixing."), "Still a scientist");
            foreach (string name in InWorldText.Placeholders()) StringAssert.EndsWith("*", name);
            CollectionAssert.AreEquivalent(new[] { "Town", "Partition" }, InWorldText.ChosenNames);
        }

        [Test]
        public void Hints_NameRegionsAndGateLooks_NeverTheThemes()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            Assert.AreEqual("the Greenwood*", HintGenerator.ThemeName(Theme.Biology));
            Assert.AreEqual("the Ember Vale*", HintGenerator.ThemeName(Theme.Chemistry));
            Assert.AreEqual("the Weightlands*", HintGenerator.ThemeName(Theme.Physics));
            Assert.AreEqual("Bridgelands*", HintGenerator.ThemeName(Theme.Math));
            Assert.AreEqual("Wildlands*", HintGenerator.ThemeName(Theme.EarthAtmosphere));
            Assert.AreEqual("Town", HintGenerator.ThemeName(Theme.Town));
            string[] themeWords = { "Biology", "Chemistry", "Physics", "Math", "Earth", "EarthAtmosphere" };
            var gateHints = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var model = new CubeModel(seed);
                CubeLayout layout = CubeLayout.Generate(model, library);
                ItemPlacement placement = ItemPlacement.ForRun(model, layout, library, catalog.Themes(), catalog.VariantCounts());
                RunFacts facts = RunFacts.Planned(model, layout, placement, library, catalog);
                foreach (HintDensity density in new[] { HintDensity.Sparse, HintDensity.Full })
                foreach (HintKind kind in Enum.GetValues(typeof(HintKind)))
                for (uint salt = 0; salt < 8; salt++)
                foreach (Voice voice in Voices.All) // 4.4: every voice names regions and gate looks the same way
                foreach (Hint h in HintGenerator.Generate(facts, kind, salt, density, voice))
                {
                    foreach (string word in themeWords) StringAssert.DoesNotContain(word, h.Text, $"seed {seed}: {h.Text}");
                    StringAssert.DoesNotContain(" gate", h.Text, $"Gates go by their look: {h.Text}");
                    StringAssert.Contains(HintGenerator.ThemeName(h.FaceTheme), h.Text);
                    if (h.Kind != HintKind.GateLocation) continue;
                    gateHints++;
                    CollectionAssert.Contains(GateLooks.All().ToList(), h.GateLook);
                    StringAssert.Contains(h.GateLook, h.Text);
                }
                // The look is the run's own: it matches the rolled stone and wand.
                IsotopeStage stage = ChemistryIsotope.KindOf(catalog.ItemFor(Theme.Chemistry, seed)).Value;
                string stone = HintGenerator.GateLook(seed, Theme.Chemistry);
                Assert.AreEqual(stage == IsotopeStage.Glow ? "a dark doorway" : stage == IsotopeStage.Unstable ? "a cracked wall" : "a heavy plated door", stone);
                Phenomenon wand = StormcallerItem.KindOf(catalog.ItemFor(Theme.EarthAtmosphere, seed)).Value;
                StringAssert.Contains(wand == Phenomenon.Quake ? "pillars" : wand == Phenomenon.Rain ? "fire" : wand == Phenomenon.Snow ? "water" : "debris",
                    HintGenerator.GateLook(seed, Theme.EarthAtmosphere));
                BeakKind beak = BiologyBeaks.KindOf(catalog.ItemFor(Theme.Biology, seed)).Value;
                StringAssert.Contains(beak == BeakKind.Thin ? "button" : "rock", HintGenerator.GateLook(seed, Theme.Biology));
            }
            Assert.Greater(gateHints, 50);
        }

        [Test]
        public void GateHints_NameThePlainGate_WhereAFaceFallsBack()
        {
            ModuleLibrary library = Library();
            ItemCatalog real = Catalog();
            // A catalog without the Chemistry stones: the Ember Vale* builds plain gates (ChemistryFace has no stage gates).
            var catalog = ScriptableObject.CreateInstance<ItemCatalog>();
            try
            {
                catalog.Set(real.Items.ToList());
                catalog.SetVariants(real.Variants.Where(v => v != null && v.HomeTheme != Theme.Chemistry).ToList());
                int chemistryGates = 0;
                for (int seed = 1; seed <= 10; seed++)
                {
                    var model = new CubeModel(seed);
                    CubeLayout layout = CubeLayout.Generate(model, library);
                    ItemPlacement placement = ItemPlacement.ForRun(model, layout, library, catalog.Themes(), catalog.VariantCounts());
                    RunFacts facts = RunFacts.Planned(model, layout, placement, library, catalog);
                    RunFacts realFacts = RunFacts.Planned(model, layout,
                        ItemPlacement.ForRun(model, layout, library, real.Themes(), real.VariantCounts()), library, real);
                    foreach (GatePlacement g in placement.Gates.Where(g => g.Item == Theme.Chemistry))
                    {
                        chemistryGates++;
                        Assert.AreEqual(GateLooks.Plain, HintGenerator.LookOf(facts, g), $"seed {seed}: {g}");
                    }
                    foreach (Hint h in HintGenerator.Generate(facts, HintKind.GateLocation, 0, HintDensity.Full)
                                 .Concat(HintGenerator.Generate(facts, HintKind.GateLocation, 1, HintDensity.Full)))
                        if (h.Item == Theme.Chemistry) StringAssert.Contains("iron bars", h.Text);
                    // With the stones, the same seeds' Chemistry gates are themed, never plain.
                    foreach (GatePlacement g in realFacts.Placement.Gates.Where(g => g.Item == Theme.Chemistry))
                        Assert.AreEqual(GateLooks.Of(ChemistryIsotope.KindOf(real.ItemFor(Theme.Chemistry, seed)).Value),
                            HintGenerator.LookOf(realFacts, g));
                }
                Assert.Greater(chemistryGates, 0);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        // ---------- Colours ----------

        [Test]
        public void RegionColours_FollowARubiksCube_AndTheFloorsCarryThem()
        {
            Color town = CubeWorld.ThemeColor(Theme.Town);
            Assert.Greater(Mathf.Min(town.r, town.g, town.b), 0.85f, "Town is white");
            AssertDominant(CubeWorld.ThemeColor(Theme.Biology), 1, "Biology is green");
            AssertDominant(CubeWorld.ThemeColor(Theme.Chemistry), 0, "Chemistry is red");
            AssertDominant(CubeWorld.ThemeColor(Theme.Physics), 2, "Physics is blue");
            Color math = CubeWorld.ThemeColor(Theme.Math);
            Assert.Greater(math.r, 0.85f, "Math is yellow");
            Assert.Greater(math.g, 0.75f, "Math is yellow");
            Assert.Less(math.b, 0.3f, "Math is yellow");
            Color earth = CubeWorld.ThemeColor(Theme.EarthAtmosphere);
            Assert.Greater(earth.r, 0.9f, "Earth is orange");
            Assert.That(earth.g, Is.InRange(0.35f, 0.65f), "Earth is orange, not yellow or red");
            Assert.Less(earth.b, 0.25f, "Earth is orange");
            Theme[] themes = { Theme.Town, Theme.Biology, Theme.Chemistry, Theme.Physics, Theme.Math, Theme.EarthAtmosphere };
            Assert.AreEqual(themes.Length, themes.Select(CubeWorld.ThemeColor).Distinct().Count(), "Six distinct region colours");

            // Floors: Town stays white; each other floor's tint leans to its region's colour.
            Assert.AreEqual(Color.white, ArtKeys.FloorTint(Theme.Town));
            AssertDominant(ArtKeys.FloorTint(Theme.Biology), 1, "Biology floor");
            AssertDominant(ArtKeys.FloorTint(Theme.Chemistry), 0, "Chemistry floor");
            AssertDominant(ArtKeys.FloorTint(Theme.Physics), 2, "Physics floor");
            Color mathFloor = ArtKeys.FloorTint(Theme.Math), earthFloor = ArtKeys.FloorTint(Theme.EarthAtmosphere);
            Assert.Less(mathFloor.b, 0.5f * mathFloor.g, "Math floor is yellow");
            Assert.Greater(mathFloor.g, earthFloor.g + 0.2f, "Math's floor is yellower than Earth's");
            Assert.Less(earthFloor.b, 0.5f * earthFloor.r, "Earth floor is orange");
            Assert.AreEqual(themes.Length, themes.Select(ArtKeys.FloorTint).Distinct().Count(), "Six distinct floor tints");
        }

        // ---------- Readability on the region colours ----------

        private static float Lin(float c) => c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);

        /// <summary>WCAG relative luminance of an sRGB colour.</summary>
        private static float Luminance(Color c) => 0.2126f * Lin(c.r) + 0.7152f * Lin(c.g) + 0.0722f * Lin(c.b);

        /// <summary>WCAG contrast ratio (1..21).</summary>
        private static float ContrastRatio(Color a, Color b)
        {
            float la = Luminance(a), lb = Luminance(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }

        private static Vector3 Lab(Color c)
        {
            float r = Lin(c.r), g = Lin(c.g), b = Lin(c.b);
            float x = (0.4124f * r + 0.3576f * g + 0.1805f * b) / 0.95047f;
            float y = 0.2126f * r + 0.7152f * g + 0.0722f * b;
            float z = (0.0193f * r + 0.1192f * g + 0.9505f * b) / 1.08883f;
            float F(float t) => t > 0.008856f ? Mathf.Pow(t, 1f / 3f) : 7.787f * t + 16f / 116f;
            return new Vector3(116f * F(y) - 16f, 500f * (F(x) - F(y)), 200f * (F(y) - F(z)));
        }

        /// <summary>CIE76 colour difference.</summary>
        private static float DeltaE(Color a, Color b) => Vector3.Distance(Lab(a), Lab(b));

        /// <summary>Readable: a luminance contrast of at least 1.5:1, or a clear colour difference (ΔE ≥ 20).</summary>
        private static bool Distinct(Color a, Color b) => ContrastRatio(a, b) >= 1.5f || DeltaE(a, b) >= 20f;

        private static readonly Dictionary<ArtKey, Color> Averages = new Dictionary<ArtKey, Color>();

        /// <summary>The average colour of a key's Kenney tile (its opaque pixels), read from the PNG.</summary>
        private static Color TileAverage(ArtCatalog catalog, ArtKey key)
        {
            if (Averages.TryGetValue(key, out Color cached)) return cached;
            Assert.IsTrue(catalog.TryGetSprite(key, out Sprite sprite), $"{key} has art");
            var texture = new Texture2D(2, 2);
            try
            {
                Assert.IsTrue(texture.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite))));
                Color sum = Color.clear;
                int n = 0;
                foreach (Color32 p in texture.GetPixels32())
                {
                    if (p.a == 0) continue;
                    sum += (Color)p;
                    n++;
                }
                Color avg = sum / Mathf.Max(1, n);
                avg.a = 1f;
                return Averages[key] = avg;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static Color Tinted(Color tile, Color tint) => new Color(tile.r * tint.r, tile.g * tint.g, tile.b * tint.b, 1f);

        /// <summary>A dressed gate's tint over its art (Gate.VisualColor): a soft tint of its closed colour.</summary>
        private static Color GateTint(Color closed) => Color.Lerp(Color.white, closed, 0.35f);

        [Test]
        public void EveryRegionFloor_StandsApartFromItsWalls_GatesAndItems()
        {
            ArtCatalog art = AssetDatabase.LoadAssetAtPath<ArtCatalog>("Assets/Resources/ArtCatalog.asset");
            Assert.IsNotNull(art);
            ItemCatalog items = Catalog();
            // Every gate kind as dressed (its art under the tint of its closed colour); plain gates take their item's colour.
            var gates = new List<(string Name, Color Colour)>
            {
                ("rock", Tinted(TileAverage(art, ArtKey.GateRock), GateTint(BreakableGate.RockColor))),
                ("pot", Tinted(TileAverage(art, ArtKey.GatePot), GateTint(BreakableGate.PotColor))),
                ("button door", Tinted(TileAverage(art, ArtKey.GateButtonDoor), GateTint(ButtonGate.DoorColor))),
                ("bramble", Tinted(TileAverage(art, ArtKey.GateVine), GateTint(FlowerVineGate.BrambleColor))),
                ("dark doorway", Tinted(TileAverage(art, ArtKey.GateDarkRoom), GateTint(DarkRoomGate.DarkColor))),
                ("cracked wall", Tinted(TileAverage(art, ArtKey.GateCrackedWall), GateTint(CrackedWallGate.WallColor))),
                ("plated door", Tinted(TileAverage(art, ArtKey.GateLeadDoor), GateTint(LeadPlateGate.DoorColor))),
                ("timed door", Tinted(TileAverage(art, ArtKey.GateTimedDoor), GateTint(TimedDoorGate.DoorColor))),
                ("gap", Tinted(TileAverage(art, ArtKey.GateGap), GateTint(GapGate.GapColor))),
                ("pillars", Tinted(TileAverage(art, ArtKey.GateRockPillars), GateTint(RockPillarGate.PillarColor))),
                ("fire wall", Tinted(TileAverage(art, ArtKey.GateFireWall), GateTint(FireWallGate.FireColor))),
                ("water", Tinted(TileAverage(art, ArtKey.GateWater), GateTint(FrozenWaterGate.WaterColor))),
                ("debris", Tinted(TileAverage(art, ArtKey.GateDebris), GateTint(DebrisGate.DebrisColor))),
            };
            foreach (ItemDefinition item in AllItems(items))
                gates.Add(($"plain gate ({item})", Tinted(TileAverage(art, ArtKey.GateGeneric), GateTint(item.PlaceholderColor))));
            // Every item's pickup art (drawn untinted) and the hidden Jumbles*.
            var pickups = AllItems(items).Select(i => ($"{i}", TileAverage(art, ArtKeys.Item(i))))
                .Append(("Jumbles*", TileAverage(art, ArtKey.Jumbles))).ToList();

            var failures = new List<string>();
            Color wallTile = TileAverage(art, ArtKey.Wall);
            foreach (Theme theme in new[] { Theme.Town, Theme.Biology, Theme.Chemistry, Theme.Physics, Theme.Math, Theme.EarthAtmosphere })
            {
                Color floor = Tinted(TileAverage(art, ArtKeys.Floor(theme)), ArtKeys.FloorTint(theme));
                // Both shades of the checkered floor (CubeWorld.BuildFloor darkens every other screen by 15 %).
                foreach (Color shade in new[] { floor, Color.Lerp(floor, Color.black, 0.15f) })
                {
                    var against = new List<(string Name, Color Colour)> { ("walls", Tinted(wallTile, ArtKeys.WallTint(theme))) };
                    if (theme == Theme.Town)
                        foreach (Theme science in CubeModel.ScienceThemes)
                            against.Add(($"ruin pillar ({science})", Tinted(wallTile, ArtKeys.TownPillarTint(science))));
                    against.AddRange(gates);
                    against.AddRange(pickups);
                    foreach ((string name, Color colour) in against)
                        if (!Distinct(shade, colour))
                            failures.Add($"{theme} floor vs {name}: contrast {ContrastRatio(shade, colour):0.00}, ΔE {DeltaE(shade, colour):0.0}");
                }
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void NpcDesigns_AndTheirLeaders_StandApartFromEveryRegionFloor()
        {
            // 4.3: every NPC design in every accent (and every leader), averaged over its opaque pixels with its outline as
            // drawn, reads against both shades of every region's floor (people of every race meet in the Town).
            ArtCatalog art = AssetDatabase.LoadAssetAtPath<ArtCatalog>("Assets/Resources/ArtCatalog.asset");
            Assert.IsNotNull(art);
            var failures = new List<string>();
            foreach (NpcLook look in (NpcLook[])Enum.GetValues(typeof(NpcLook)))
            foreach (bool leader in new[] { false, true })
            for (int accent = 0; accent < (leader ? 1 : NpcDesignArt.AccentCount); accent++)
            {
                Color32[] pixels = NpcDesignArt.Pixels(look, leader, accent);
                Color sum = Color.clear;
                int n = 0;
                foreach (Color32 p in pixels)
                {
                    if (p.a == 0) continue;
                    sum += (Color)p;
                    n++;
                }
                Color figure = sum / Mathf.Max(1, n);
                figure.a = 1f;
                foreach (Theme theme in new[] { Theme.Town, Theme.Biology, Theme.Chemistry, Theme.Physics, Theme.Math, Theme.EarthAtmosphere })
                {
                    Color floor = Tinted(TileAverage(art, ArtKeys.Floor(theme)), ArtKeys.FloorTint(theme));
                    foreach (Color shade in new[] { floor, Color.Lerp(floor, Color.black, 0.15f) })
                        if (!Distinct(shade, figure))
                            failures.Add($"{look}{(leader ? " leader" : "")} accent {accent} on {theme}: contrast {ContrastRatio(shade, figure):0.00}, ΔE {DeltaE(shade, figure):0.0}");
                }
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void WeightlandsGlaze_IsTheFloorTilesOwnAverage_SoItCalmsTheLinesWithoutChangingTheColour()
        {
            ArtCatalog art = AssetDatabase.LoadAssetAtPath<ArtCatalog>("Assets/Resources/ArtCatalog.asset");
            Color tile = TileAverage(art, ArtKey.FloorPhysics);
            Color recorded = FloorPattern.PhysicsTileAverage;
            Assert.AreEqual(tile.r, recorded.r, 0.01f);
            Assert.AreEqual(tile.g, recorded.g, 0.01f);
            Assert.AreEqual(tile.b, recorded.b, 0.01f);
            Assert.That(FloorPattern.GlazeAlpha(Theme.Physics), Is.InRange(0.3f, 0.7f));
            foreach (Theme other in new[] { Theme.Town, Theme.Biology, Theme.Chemistry, Theme.Math, Theme.EarthAtmosphere })
                Assert.AreEqual(0f, FloorPattern.GlazeAlpha(other), $"{other} has no glaze");
        }

        [Test]
        public void MapMarkers_ReadOnEveryRegionColour()
        {
            Assert.GreaterOrEqual(CubeMap.MarkerOutlineWidth, 2f, "Markers carry an outline at least 2 px wide");
            var failures = new List<string>();
            foreach (Theme theme in new[] { Theme.Town, Theme.Biology, Theme.Chemistry, Theme.Physics, Theme.Math, Theme.EarthAtmosphere })
            {
                Color region = CubeWorld.ThemeColor(theme);
                foreach (Color cell in new[] { region, Color.Lerp(region, Color.black, 0.15f) }) // the map's checkered cells
                foreach (KeyValuePair<string, Color> marker in CubeMap.MarkerFills())
                {
                    // A marker reads when its fill or its dark outline stands out from the cell (WCAG 3:1, for graphics).
                    float best = Mathf.Max(ContrastRatio(marker.Value, cell), ContrastRatio(CubeMap.MarkerOutlineColor, cell));
                    if (best < 3f) failures.Add($"{marker.Key} on {theme}: {best:0.00}");
                }
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        private static void AssertDominant(Color c, int channel, string message)
        {
            float[] v = { c.r, c.g, c.b };
            for (int i = 0; i < 3; i++)
                if (i != channel) Assert.Greater(v[channel], v[i] + 0.2f, message);
        }

        // ---------- Finches ----------

        [Test]
        public void Finches_AreDrawnInFullColour_WithAClearBeakEyesAndFeet_AndKeepTheirSlotsAndMixing()
        {
            RacePartSet set = RacePartSets.For(Race.Finch);
            foreach (NpcPart part in set.Heads.Concat(set.Torsos).Concat(set.Legs))
            {
                Assert.IsTrue(NpcPartArt.BakedColour(Race.Finch, part), part.Id);
                Assert.AreEqual(Color.white, NpcPartArt.TintFor(Race.Finch, part), $"{part.Id}: drawn in its own colours, not tinted");
                // Even on another race's NPC (Town mixes parts), a finch part keeps its look.
                Assert.AreEqual(Color.white, NpcPartArt.TintFor(Race.Townsfolk, part));
            }
            NpcPart shroom = RacePartSets.For(Race.Mushroom).Heads[0];
            Assert.AreEqual(shroom.Color, NpcPartArt.TintFor(Race.Mushroom, shroom), "Other races are still tinted");

            foreach (NpcPart head in set.Heads)
            {
                Texture2D texture = NpcPartArt.SpriteFor(Race.Finch, head).texture;
                Color32[] pixels = texture.GetPixels32();
                Assert.Greater(pixels.Count(p => p.a > 0 && p.r > 230 && p.g > 140 && p.g < 200 && p.b < 90), 3, $"{head.Id}: an orange beak");
                Assert.Greater(pixels.Count(p => p.a > 0 && p.r == 255 && p.g == 255 && p.b == 255), 0, $"{head.Id}: a glint in the eye");
                Assert.Greater(pixels.Count(p => p.a > 0 && p.r > 240 && p.g > 120 && p.g < 160 && p.b > 130), 0, $"{head.Id}: a blush");
            }
            foreach (NpcPart legs in set.Legs)
            {
                Color32[] pixels = NpcPartArt.SpriteFor(Race.Finch, legs).texture.GetPixels32();
                Assert.Greater(pixels.Count(p => p.a > 0 && p.r > 230 && p.g > 140 && p.g < 200 && p.b < 90), 6, $"{legs.Id}: orange feet");
            }
            // The slots and the seeded mixing are unchanged: the same parts per slot, chosen the same way.
            Assert.AreEqual(4, set.Heads.Count);
            Assert.AreEqual(4, set.Torsos.Count);
            Assert.AreEqual(3, set.Legs.Count);
            Assert.AreEqual(NpcFactory.ChooseParts(7, Race.Finch, 2000).Signature(), BiologyPopulation.FinchSpec(7, 0).Signature());
        }
    }
}
