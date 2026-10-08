using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Race cultures and names (4.4, voices-and-names.md). Every character has a chosen name without a star, from its
    /// voice's seeded pool or a fixed one (leaders, patrons, the greeter, the cameos, the Demon), and the names keep the
    /// subtle-science rule. Each race speaks in its own voice on every line it says, sampled over seeds 1-50: finches use
    /// archaic forms, shape people reason in if/then steps and never exclaim, mushroom folk flare up, aliens muse and
    /// question, dinosaurs talk like the outdoors. Every line fits the dialogue box.
    /// </summary>
    public class VoicesAndNamesTests
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

        /// <summary>Every NPC of a seed's run, by population (Town, each face, the cameo, the leaders).</summary>
        private static Dictionary<string, List<NpcSpec>> Populations(int seed, int n)
        {
            var populations = new Dictionary<string, List<NpcSpec>>
            {
                ["Town"] = TownPlan.Specs(seed, n),
                ["Finches"] = Enumerable.Range(0, BiologyPopulation.FinchCount(n)).Select(i => BiologyPopulation.FinchSpec(seed, i)).ToList(),
                ["Mushrooms"] = Enumerable.Range(0, ChemistryPopulation.MushroomCount(n)).Select(i => ChemistryPopulation.MushroomSpec(seed, i)).ToList(),
                ["Aliens"] = Enumerable.Range(0, PhysicsPopulation.AlienCount(n)).Select(i => PhysicsPopulation.AlienSpec(seed, i)).ToList(),
                ["Shapes"] = Enumerable.Range(0, MathPopulation.ShapeCount(n)).Select(i => MathPopulation.ShapeSpec(seed, i)).ToList(),
                ["Dinosaurs"] = Enumerable.Range(0, EarthPopulation.DinoCount(n)).Select(i => EarthPopulation.DinoSpec(seed, i)).ToList(),
                ["Cameo"] = new List<NpcSpec> { PhysicsPopulation.NewtonSpec(seed) },
                ["Leaders"] = RaceExtensions.All.Select(r => NpcFactory.ChooseLeader(seed, r, 0)).ToList(),
            };
            // The science faces' town residents (4.5), one population per town.
            foreach (Theme theme in CubeModel.ScienceThemes)
                populations[$"{theme} town"] = Enumerable.Range(0, TownSquare.ResidentCount).Select(i => TownSquare.ResidentSpec(seed, theme, i)).ToList();
            return populations;
        }

        // ---------- Names ----------

        [Test]
        public void EveryCharacter_HasAChosenName_WithNoStar_AndNoBannedWord()
        {
            foreach (string name in NpcNames.All())
            {
                Assert.IsFalse(name.Contains("*"), $"\"{name}\" is a chosen name: no star");
                CollectionAssert.IsEmpty(InWorldText.BannedIn(name), $"\"{name}\" names a scientist or a science word");
                CollectionAssert.IsEmpty(InWorldText.UnstarredNames("Say hi to " + name + " for me."), $"\"{name}\" reads as chosen mid-sentence");
                Assert.IsFalse(InWorldText.Placeholders().Any(p => p.TrimEnd('*') == name), $"\"{name}\" is not a placeholder");
            }
            foreach (Voice voice in Voices.All)
            {
                IReadOnlyList<string> pool = NpcNames.Pool(voice);
                Assert.GreaterOrEqual(pool.Count, 16, $"{voice}: a pool deep enough for a face");
                Assert.AreEqual(pool.Count, pool.Distinct().Count(), $"{voice}: no repeats in the pool");
            }
            Assert.AreEqual(NpcNames.All().Count(), NpcNames.All().Distinct().Count(), "Every name is used for one character only");

            int n = CubeSettings.DefaultFaceSize;
            var seenNames = new HashSet<string>();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                foreach (KeyValuePair<string, List<NpcSpec>> population in Populations(seed, n))
                {
                    foreach (NpcSpec spec in population.Value)
                    {
                        string context = $"seed {seed} {population.Key}: {spec.DisplayName}";
                        Assert.IsFalse(string.IsNullOrEmpty(spec.Name), context);
                        Assert.IsFalse(spec.Name.Contains("*") || spec.DisplayName.Contains("*"), $"{context}: a character's name carries no star");
                        CollectionAssert.Contains(NpcNames.All().ToList(), spec.Name, context);
                        StringAssert.StartsWith(spec.Name + " the ", spec.DisplayName, context);
                        CollectionAssert.IsEmpty(InWorldText.Problems(spec.DisplayName), context);
                        StringAssert.DoesNotContain(spec.Name, spec.Signature(), "Names are never in the determinism signatures");
                        seenNames.Add(spec.Name);
                    }
                    if (population.Key == "Cameo" || population.Key == "Leaders") continue;
                    // Within one population nobody shares a name (dinosaurs: within each kind's pool).
                    List<string> names = population.Value.Select(s => s.Voice + ":" + s.Name).ToList();
                    Assert.AreEqual(names.Count, names.Distinct().Count(), $"seed {seed} {population.Key}: two people share a name ({string.Join(", ", names)})");
                }
                // The fixed names.
                List<NpcSpec> town = TownPlan.Specs(seed, n);
                Assert.AreEqual(NpcNames.Greeter, town.Single(TownPlan.IsGreeter).Name);
                Assert.AreEqual(NpcNames.AppleSage, PhysicsPopulation.NewtonSpec(seed).Name);
                foreach (Race race in RaceExtensions.All)
                for (int i = 0; i < 4; i++)
                {
                    NpcSpec leader = NpcFactory.ChooseLeader(seed, race, i);
                    Assert.AreEqual(NpcNames.Leader(race), leader.Name, $"{race}: the leader is always the same person");
                    StringAssert.EndsWith("leader", leader.DisplayName);
                }
                // Names are seeded: the same seed names everyone the same way.
                CollectionAssert.AreEqual(Populations(seed, n)["Finches"].Select(s => s.Name).ToList(),
                    Populations(seed, n)["Finches"].Select(s => s.Name).ToList());
            }
            Assert.Greater(seenNames.Count, 80, "Seeds draw from the whole pools");
            Assert.AreNotEqual(string.Join(",", Populations(1, n)["Town"].Select(s => s.Name)),
                string.Join(",", Populations(2, n)["Town"].Select(s => s.Name)), "Different seeds, different names");

            // Every voice's pool is used by its people: the avian and ground dinosaurs draw from their own.
            Assert.AreEqual(Voice.Sky, Voices.Of(Race.Dinosaur, DinoVariant.Avian));
            Assert.AreEqual(Voice.Ground, Voices.Of(Race.Dinosaur, DinoVariant.Terrestrial));
            foreach (NpcSpec dino in Populations(3, n)["Dinosaurs"])
                CollectionAssert.Contains(NpcNames.Pool(dino.Voice).ToList(), dino.Name);
        }

        [Test]
        public void NamesStayUnique_WithinEveryPopulation_EvenOnAFiveByFiveFace_WhereThePoolsRunOut()
        {
            int pooledBeyond = 0;
            foreach (int n in new[] { 2, 3, 5 })
            for (int seed = 1; seed <= Seeds; seed++)
            foreach (KeyValuePair<string, List<NpcSpec>> population in Populations(seed, n))
            {
                if (population.Key == "Cameo" || population.Key == "Leaders") continue;
                List<string> names = population.Value.Select(s => s.Voice + ":" + s.Name).ToList();
                Assert.AreEqual(names.Count, names.Distinct().Count(), $"N={n} seed {seed} {population.Key}: a shared name ({string.Join(", ", names)})");
                foreach (NpcSpec spec in population.Value)
                {
                    CollectionAssert.IsEmpty(InWorldText.Problems(spec.DisplayName), $"N={n}: {spec.DisplayName}");
                    if (spec.Name.Contains(" II") || spec.Name.Contains(" III")) pooledBeyond++;
                }
            }
            Assert.Greater(pooledBeyond, 0, "A 5x5 face outgrows a pool, and the repeats carry a numeral");

            // A dinosaur that falls back to the other kind is named past every regular one of that kind.
            int most = EarthPopulation.DinoCount(14);
            Assert.Less(most / 2, EarthPopulation.FallbackOrdinal, "Regular ordinals (i / 2) stay below the fallback's");
            for (int seed = 1; seed <= 10; seed++)
            foreach (DinoVariant kind in new[] { DinoVariant.Terrestrial, DinoVariant.Avian })
            {
                var all = new List<string>();
                for (int ordinal = 0; ordinal <= most / 2; ordinal++)
                    all.Add(NpcNames.For(seed, Race.Dinosaur, kind, NpcNames.OffsetOf(EarthPopulation.IndexBase), ordinal));
                for (int i = 0; i < most; i++)
                    all.Add(NpcNames.For(seed, Race.Dinosaur, kind, NpcNames.OffsetOf(EarthPopulation.IndexBase), EarthPopulation.FallbackOrdinal + i));
                Assert.AreEqual(all.Count, all.Distinct().Count(), $"seed {seed} {kind}: a fallback repeats a name");
            }
        }

        [Test]
        public void TheGreeter_AndTheDinosaurLeader_HaveTheirOwnLabels()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                NpcSpec greeter = TownPlan.Specs(seed, CubeSettings.DefaultFaceSize).Single(TownPlan.IsGreeter);
                Assert.AreEqual("Hattie Hullo the greeter", greeter.DisplayName);
                for (int i = 0; i < 4; i++)
                {
                    NpcSpec chief = NpcFactory.ChooseLeader(seed, Race.Dinosaur, i);
                    Assert.AreEqual(DinoVariant.Terrestrial, chief.Variant, "Chief Rockjaw is a ground dinosaur");
                    Assert.AreEqual(Voice.Ground, chief.Voice);
                    Assert.AreEqual("Chief Rockjaw the terrestrial dinosaur leader", chief.DisplayName);
                    Assert.AreEqual(NpcLook.GroundDino, NpcDesignArt.LookOf(chief));
                }
            }
        }

        [Test]
        public void TheDemonAndThePatrons_GoByTheirChosenNames_InTheText()
        {
            StringAssert.Contains(NpcNames.Demon, CoreHud.DefeatMessage);
            StringAssert.Contains(NpcNames.Demon, BetweenRunsScreen.VictoryTitle);
            StringAssert.Contains(NpcNames.Demon, new FallbackBoss().Name);
            StringAssert.Contains(NpcNames.Greeter.Split(' ')[0], TownSign.WelcomeLines[0]);
            StringAssert.Contains(TownSign.LastMixedPlace, TownSign.WelcomeLines[0]);
            Assert.IsTrue(Dialogue.DarwinLines.Any(l => l.Contains(NpcNames.FinchPatron)));
            Assert.IsTrue(Dialogue.CurieLines.Any(l => l.Contains(NpcNames.MushroomPatron)));
            Assert.IsTrue(Dialogue.EinsteinLines.Any(l => l.Contains(NpcNames.AlienPatron)));
            Assert.IsTrue(Dialogue.BridgekeeperLines.Any(l => l.Contains(NpcNames.ShapePatron)));
            Assert.IsTrue(Dialogue.OldDiggerLines.Any(l => l.Contains(NpcNames.GroundPatron)));
            Assert.IsTrue(Dialogue.SkyWatcherLines.Any(l => l.Contains(NpcNames.SkyPatron)));
            foreach (Race race in RaceExtensions.All)
                StringAssert.Contains(NpcNames.Leader(race).Split(' ').Last(), Dialogue.LeaderLine(race), $"{race}: the leader names itself");
            foreach (string text in Dialogue.AllFixedText())
                StringAssert.DoesNotContain("Demon*", text);
        }

        // ---------- Voices ----------

        private static readonly Regex FinchMarkers = new Regex(
            @"\b(thee|thou|thy|thine|prithee|tis|twas|doth|dost|hath|hast|verily|forsooth|methinks|hark|morrow|saith|betwixt|nay|alas)\b",
            RegexOptions.IgnoreCase);

        private static readonly Regex ShapeMarkers = new Regex(@"\b(if|then|therefore|thus|hence|given)\b", RegexOptions.IgnoreCase);

        /// <summary>An emotional word or swing (an exclamation mark alone is not enough).</summary>
        private static readonly Regex MushroomMarkers = new Regex(
            @"\b(oh|ooh|ugh|argh|sob|sniff|gasp|eek|wow|yay|phew|aww|cry|crying|love|furious|happy|excited|thrilled|sad|bored|fizzing)\b",
            RegexOptions.IgnoreCase);

        /// <summary>A musing word (a question mark alone is not enough).</summary>
        private static readonly Regex AlienMarkers = new Regex(
            @"\b(perhaps|wonder|ponder|muse|mused|musing|consider|considered|truly|imagine|suppose|curious|ask|thought)\b",
            RegexOptions.IgnoreCase);

        private static readonly Regex OutdoorMarkers = new Regex(
            @"\b(trails?|camps?|campfire|ground|rocks?|rocky|cliffs?|ridge|mud|hills?|canyon|storm|clouds?|sky|wind|breeze|rain|raindrops|sun|snow|dirt|crevices?|bones?|scout|scouted|boots?|feathers?)\b",
            RegexOptions.IgnoreCase);

        private static List<Regex> excluded;

        /// <summary>
        /// The line without its character names (with any numeral), item names, item nicknames and region names, so a
        /// marker never comes from a name ("Cliff", "Breezy", "Grand Muser Yondrix", the storm-calling stick).
        /// </summary>
        private static string WithoutNames(string line)
        {
            if (excluded == null)
            {
                IEnumerable<string> names = NpcNames.All()
                    .Concat(Catalog().Items.Concat(Catalog().Variants).Where(i => i != null).Select(i => i.DisplayName))
                    .Concat(new[] { Theme.Town, Theme.Biology, Theme.Chemistry, Theme.Physics, Theme.Math, Theme.EarthAtmosphere }
                        .SelectMany(t => new[] { HintGenerator.ItemNickname(t), HintGenerator.ThemeName(t) }))
                    .Concat(new[] { MassMittItem.Name, GapCrosserItem.Name, StormcallerItem.Name, ChemistryIsotope.Name });
                excluded = names.Distinct().OrderByDescending(n => n.Length)
                    .Select(n => new Regex(@"\b" + Regex.Escape(n) + @"(\s+[IVX]+\b)?")).ToList(); // case-sensitive: "Cliff" the name, never "a cliff"
            }
            foreach (Regex name in excluded) line = name.Replace(line, " ");
            return line;
        }

        /// <summary>What a line in a voice must show (null when it keeps the voice).</summary>
        private static string VoiceProblem(Voice voice, string line)
        {
            string text = WithoutNames(line);
            switch (voice)
            {
                case Voice.Finch:
                    return FinchMarkers.IsMatch(text) ? null : "no archaic form (thee, thou, prithee, 'tis, doth...)";
                case Voice.Shape:
                    if (line.Contains("!")) return "an exclamation mark";
                    return ShapeMarkers.IsMatch(text) ? null : "no if/then/therefore/given reasoning";
                case Voice.Mushroom:
                    return MushroomMarkers.IsMatch(text) ? null : "no emotional word or swing";
                case Voice.Alien:
                    return AlienMarkers.IsMatch(text) ? null : "no musing word";
                case Voice.Ground:
                case Voice.Sky:
                    return OutdoorMarkers.IsMatch(text) ? null : "no outdoorsy words";
                default:
                    return null;
            }
        }

        /// <summary>The lines each voice says, sampled over seeds 1-50 at both densities: populations, Town, leaders, tables.</summary>
        private static Dictionary<Voice, HashSet<string>> SampledLines()
        {
            var lines = Voices.All.ToDictionary(v => v, v => new HashSet<string>());
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var model = new CubeModel(seed);
                int n = model.FaceSize;
                CubeLayout layout = CubeLayout.Generate(model, library);
                ItemPlacement placement = ItemPlacement.ForRun(model, layout, library, catalog.Themes(), catalog.VariantCounts());
                RunFacts facts = RunFacts.Planned(model, layout, placement, library, catalog);
                IsotopeStage stage = ChemistryIsotope.KindOf(catalog.ItemFor(Theme.Chemistry, seed)) ?? IsotopeStage.Glow;
                Phenomenon wand = StormcallerItem.KindOf(catalog.ItemFor(Theme.EarthAtmosphere, seed)) ?? Phenomenon.Quake;
                foreach (HintDensity density in new[] { HintDensity.Sparse, HintDensity.Full })
                {
                    void Add(NpcSpec spec, IEnumerable<string> said)
                    {
                        foreach (string line in said) lines[spec.Voice].Add(line);
                    }
                    for (int i = 0; i < BiologyPopulation.FinchCount(n); i++)
                    {
                        NpcSpec spec = BiologyPopulation.FinchSpec(seed, i);
                        foreach (BeakKind beak in new[] { BeakKind.Thin, BeakKind.Thick })
                            Add(spec, BiologyPopulation.LinesFor(spec, facts, density, beak, i == 0));
                    }
                    for (int i = 0; i < ChemistryPopulation.MushroomCount(n); i++)
                    {
                        NpcSpec spec = ChemistryPopulation.MushroomSpec(seed, i);
                        Add(spec, ChemistryPopulation.LinesFor(spec, facts, density, stage, i));
                    }
                    for (int i = 0; i < PhysicsPopulation.AlienCount(n); i++)
                    {
                        NpcSpec spec = PhysicsPopulation.AlienSpec(seed, i);
                        Add(spec, PhysicsPopulation.LinesFor(spec, facts, density, i == 0));
                    }
                    for (int i = 0; i < MathPopulation.ShapeCount(n); i++)
                    {
                        NpcSpec spec = MathPopulation.ShapeSpec(seed, i);
                        Add(spec, MathPopulation.LinesFor(spec, facts, density, i));
                    }
                    for (int i = 0; i < EarthPopulation.DinoCount(n); i++)
                    {
                        NpcSpec spec = EarthPopulation.DinoSpec(seed, i);
                        Add(spec, EarthPopulation.LinesFor(spec, facts, density, wand, i));
                    }
                    foreach (NpcSpec spec in TownPlan.Specs(seed, n))
                    {
                        List<string> said = Dialogue.For(spec, facts, density);
                        Add(spec, TownPlan.IsGreeter(spec) ? TownSign.GreeterLines(said) : said);
                    }
                    foreach (Theme theme in CubeModel.ScienceThemes)
                    for (int i = 0; i < TownSquare.ResidentCount; i++)
                    {
                        NpcSpec resident = TownSquare.ResidentSpec(seed, theme, i);
                        Add(resident, Dialogue.For(resident, facts, density));
                    }
                    foreach (Race race in RaceExtensions.All)
                    for (int i = 0; i < 3; i++)
                    {
                        NpcSpec leader = NpcFactory.ChooseLeader(seed, race, i);
                        Add(leader, Dialogue.For(leader, facts, density));
                        // Every role, in this race's voice (the torso picks the role line).
                        NpcSpec folk = NpcFactory.ChooseParts(seed, race, i);
                        foreach (NpcPart torso in RacePartSets.For(race).Torsos)
                            Add(folk.With(torso), Dialogue.For(folk.With(torso), facts, density));
                    }
                }
            }
            foreach (Voice voice in Voices.All)
                foreach (string line in Dialogue.SpokenLines(voice)) lines[voice].Add(line);
            lines[Voice.Finch].UnionWith(Dialogue.DarwinLines.Concat(Dialogue.BeakLines).Append(Dialogue.LeaderLine(Race.Finch)).Append(Dialogue.QuestLine(Race.Finch)));
            lines[Voice.Mushroom].UnionWith(Dialogue.CurieLines.Concat(Dialogue.StoneLines).Append(Dialogue.StoneLoreLine).Append(Dialogue.LeaderLine(Race.Mushroom)).Append(Dialogue.QuestLine(Race.Mushroom)));
            lines[Voice.Alien].UnionWith(Dialogue.EinsteinLines.Append(Dialogue.MassMittLine).Append(Dialogue.LeaderLine(Race.Alien)).Append(Dialogue.QuestLine(Race.Alien)));
            lines[Voice.Shape].UnionWith(Dialogue.BridgekeeperLines.Append(Dialogue.IslandLine).Append(Dialogue.GapCrosserLine).Append(Dialogue.LeaderLine(Race.Shape)).Append(Dialogue.QuestLine(Race.Shape)));
            lines[Voice.Ground].UnionWith(Dialogue.OldDiggerLines.Concat(Dialogue.WandLines).Append(Dialogue.NeutralWandLine).Append(Dialogue.GripLine).Append(Dialogue.LeaderLine(Race.Dinosaur)).Append(Dialogue.QuestLine(Race.Dinosaur)));
            lines[Voice.Sky].UnionWith(Dialogue.SkyWatcherLines.Append(Dialogue.HazardLine).Append(Dialogue.LeaderLine(Race.Dinosaur)).Append(Dialogue.QuestLine(Race.Dinosaur)));
            // 4.8: every quest (each count and found), repair and mended line, in its leader's voice (the dinosaur's in both).
            lines[Voice.Finch].UnionWith(Dialogue.LeaderQuestLines(Race.Finch));
            lines[Voice.Mushroom].UnionWith(Dialogue.LeaderQuestLines(Race.Mushroom));
            lines[Voice.Alien].UnionWith(Dialogue.LeaderQuestLines(Race.Alien));
            lines[Voice.Shape].UnionWith(Dialogue.LeaderQuestLines(Race.Shape));
            lines[Voice.Ground].UnionWith(Dialogue.LeaderQuestLines(Race.Dinosaur));
            lines[Voice.Sky].UnionWith(Dialogue.LeaderQuestLines(Race.Dinosaur));
            lines[Voice.Town].UnionWith(Dialogue.LeaderQuestLines(Race.Townsfolk));
            return lines;
        }

        [Test]
        public void EveryRace_SpeaksInItsOwnVoice_OnEveryLine_OverSeeds1To50()
        {
            Dictionary<Voice, HashSet<string>> lines = SampledLines();
            var offences = new List<string>();
            foreach (KeyValuePair<Voice, HashSet<string>> voice in lines)
            {
                Assert.Greater(voice.Value.Count, voice.Key == Voice.Town ? 50 : 40, $"{voice.Key}: enough lines sampled");
                foreach (string line in voice.Value)
                {
                    string problem = VoiceProblem(voice.Key, line);
                    if (problem != null) offences.Add($"[{voice.Key}] {problem}: \"{line}\"");
                }
            }
            Assert.IsEmpty(offences, $"{offences.Count} lines out of voice:\n" + string.Join("\n", offences.Take(40)));

            // Hints are in voice too: each voice's openers carry its markers, and the voices word the same fact differently.
            foreach (Voice voice in Voices.All)
            {
                HintGenerator.HintVoice wording = HintGenerator.VoiceTable[voice];
                foreach (string opener in wording.Full.Concat(wording.Sparse))
                    Assert.IsNull(VoiceProblem(voice, opener), $"{voice} opener \"{opener}\"");
                Assert.IsTrue(wording.Sealed.Contains("sealed"), $"{voice}: a sealed face is called sealed");
                if (voice == Voice.Shape)
                    foreach (string text in wording.AllText()) StringAssert.DoesNotContain("!", text);
            }
            RunFacts facts = RunFacts.Planned(new CubeModel(4), CubeLayout.Generate(new CubeModel(4), Library()),
                ItemPlacement.ForRun(new CubeModel(4), CubeLayout.Generate(new CubeModel(4), Library()), Library(), Catalog().Themes(),
                    Catalog().VariantCounts()), Library(), Catalog());
            List<string> sameHint = Voices.All.Select(v => HintGenerator.Generate(facts, HintKind.ItemLocation, 0, HintDensity.Full, v)[0].Text).ToList();
            Assert.AreEqual(sameHint.Count, sameHint.Distinct().Count(), "Each voice words a hint its own way:\n" + string.Join("\n", sameHint));
        }

        [Test]
        public void TheVoiceRules_CatchLinesOutOfVoice()
        {
            Assert.IsNotNull(VoiceProblem(Voice.Finch, "Hello there, how are you?"));
            Assert.IsNull(VoiceProblem(Voice.Finch, "'Tis a fine day."));
            Assert.IsNotNull(VoiceProblem(Voice.Shape, "If you go, then hurry!"), "Shape people never exclaim");
            Assert.IsNotNull(VoiceProblem(Voice.Shape, "Go round the islands."));
            Assert.IsNotNull(VoiceProblem(Voice.Mushroom, "The stones glow."));
            Assert.IsNotNull(VoiceProblem(Voice.Mushroom, "The stones glow!"), "An exclamation alone is not a feeling");
            Assert.IsNotNull(VoiceProblem(Voice.Alien, "The door is shut."));
            Assert.IsNotNull(VoiceProblem(Voice.Alien, "The door is shut?"), "A question alone is not musing");
            Assert.IsNotNull(VoiceProblem(Voice.Ground, "Nice to meet you."));
            Assert.IsNotNull(VoiceProblem(Voice.Ground, "Ask Cliff about the storm-calling stick."), "Names never count");
            Assert.IsNotNull(VoiceProblem(Voice.Ground, "A rockery and a sunny skylight."), "Whole words only");
            Assert.IsNotNull(VoiceProblem(Voice.Finch, "Pray take mine."), "Common words are no archaic form");
            Assert.IsNotNull(VoiceProblem(Voice.Alien, "I am Grand Muser Yondrix."), "A name is no musing");
        }

        // ---------- Punctuation ----------

        [Test]
        public void EveryVoicesHints_EndWithOneFittingMark_ForEveryOpenerAndKind()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            var badEnd = new Regex(@"([,;:]\s*[.!?]|[.!?]\s*[.!?]|\s[.!?])$");
            var failures = new List<string>();
            var seenOpeners = new HashSet<string>();
            for (int seed = 1; seed <= 10; seed++)
            {
                var model = new CubeModel(seed, CubeSettings.DefaultFaceSize, seed % 2 == 0 ? new[] { Theme.Physics } : null);
                CubeLayout layout = CubeLayout.Generate(model, library);
                ItemPlacement placement = ItemPlacement.ForRun(model, layout, library, catalog.Themes(), catalog.VariantCounts());
                RunFacts facts = RunFacts.Planned(model, layout, placement, library, catalog);
                foreach (Voice voice in Voices.All)
                {
                    HintGenerator.HintVoice wording = HintGenerator.VoiceTable[voice];
                    foreach (HintDensity density in new[] { HintDensity.Sparse, HintDensity.Full })
                    foreach (HintKind kind in Enum.GetValues(typeof(HintKind)))
                    for (uint salt = 0; salt < 24; salt++) // salt / 7 picks the opener: every opener of every density
                    foreach (Hint h in HintGenerator.Generate(facts, kind, salt, density, voice))
                    {
                        string[] openers = density == HintDensity.Full ? wording.Full : wording.Sparse;
                        string opener = openers.Single(o => h.Text.StartsWith(o + " "));
                        seenOpeners.Add(voice + opener);
                        // The sentence the opener starts (a vague gate hint adds the voice's remark after it).
                        string sentence = h.Text;
                        if (h.Kind == HintKind.GateLocation && !h.Precise)
                        {
                            StringAssert.EndsWith(wording.Rude, sentence, $"{voice}: the remark ends the vague gate hint");
                            sentence = sentence.Substring(0, sentence.Length - wording.Rude.Length);
                        }
                        string want = wording.EndFor(opener);
                        if (!sentence.EndsWith(want) || badEnd.IsMatch(sentence))
                            failures.Add($"{voice} [{opener}] wants \"{want}\": {h.Text}");
                        if (!wording.Questions.Contains(opener) && sentence.EndsWith("?"))
                            failures.Add($"{voice} [{opener}] a statement ends with \"?\": {h.Text}");
                        if (sentence.Contains(", or so it seems")) failures.Add($"{voice}: no musing tail on a hint: {h.Text}");
                    }
                }
            }
            Assert.IsEmpty(failures, string.Join("\n", failures.Distinct().Take(30)));
            Assert.AreEqual(Voices.All.Sum(v => HintGenerator.VoiceTable[v].Full.Length + HintGenerator.VoiceTable[v].Sparse.Length),
                seenOpeners.Count, "Every opener of every voice was checked");
            // The one question opener (the alien "could it be that") ends its hint with "?".
            Assert.AreEqual("?", HintGenerator.VoiceTable[Voice.Alien].EndFor("I wonder... could it be that"));
            foreach (Voice voice in Voices.All)
                foreach (string q in HintGenerator.VoiceTable[voice].Questions)
                    CollectionAssert.Contains(HintGenerator.VoiceTable[voice].Full.Concat(HintGenerator.VoiceTable[voice].Sparse).ToList(), q);
        }

        // ---------- Length ----------

        [Test]
        public void EveryLine_FitsTheDialogueBox()
        {
            var lines = new HashSet<string>(Dialogue.AllFixedText());
            lines.UnionWith(TownSign.WelcomeLines);
            foreach (HashSet<string> said in SampledLines().Values) lines.UnionWith(said);
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var model = new CubeModel(seed);
                CubeLayout layout = CubeLayout.Generate(model, library);
                ItemPlacement placement = ItemPlacement.ForRun(model, layout, library, catalog.Themes(), catalog.VariantCounts());
                RunFacts facts = RunFacts.Planned(model, layout, placement, library, catalog);
                foreach (Voice voice in Voices.All)
                foreach (HintDensity density in new[] { HintDensity.Sparse, HintDensity.Full })
                foreach (HintKind kind in Enum.GetValues(typeof(HintKind)))
                for (uint salt = 0; salt < 24; salt++)
                    foreach (Hint h in HintGenerator.Generate(facts, kind, salt, density, voice)) lines.Add(h.Text);
            }
            List<string> tooLong = lines.Where(l => l.Length > Dialogue.MaxLineLength).OrderByDescending(l => l.Length)
                .Select(l => $"{l.Length}: {l}").ToList();
            Assert.IsEmpty(tooLong, $"{tooLong.Count} lines longer than {Dialogue.MaxLineLength}:\n" + string.Join("\n", tooLong.Take(20)));
            Assert.Greater(lines.Count, 1000);
        }
    }
}
