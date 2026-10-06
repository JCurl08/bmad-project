using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Game.Cube.Tests
{
    /// <summary>
    /// NPC parts, hints and dialogue (pure): seeded part choice is deterministic and varied, the head
    /// decides the hint (and every hint is true for the run), the torso decides the role line, sparse
    /// density gives fewer and vaguer hints than full, and no line ever uses the forbidden word.
    /// </summary>
    public class NpcTests
    {
        private const string LibraryPath = "Assets/Modules/ModuleLibrary.asset";
        private const string ItemCatalogPath = "Assets/Items/ItemCatalog.asset";

        private static readonly string[] ScreenWords = { "screen", "north", "south", "east", "west", "centre", "edge" };

        private static RunFacts Facts(int seed)
        {
            var library = AssetDatabase.LoadAssetAtPath<ModuleLibrary>(LibraryPath);
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogPath);
            Assert.IsNotNull(library, $"{LibraryPath} missing: run Cube > Build Module Library");
            Assert.IsNotNull(catalog, $"{ItemCatalogPath} missing: run Cube > Build Item Catalog");
            var model = new CubeModel(seed);
            CubeLayout layout = CubeLayout.Generate(model, library);
            ItemPlacement placement = ItemPlacement.ForRun(model, layout, library, catalog.Themes());
            return new RunFacts(model, layout, placement);
        }

        /// <summary>Checks a hint's stated facts against the run data, and that its text says them.</summary>
        private static void AssertTrue(Hint hint, RunFacts facts, string context)
        {
            CubeModel model = facts.Model;
            string text = hint.Text;
            Assert.AreEqual(model.ThemeOf(hint.Face), hint.FaceTheme, $"{context}: face theme ({text})");
            StringAssert.Contains(HintGenerator.ThemeName(hint.FaceTheme), text, context);
            string place = hint.Precise ? HintGenerator.ScreenPhrase(hint.Screen, model.FaceSize) + " screen" : null;
            switch (hint.Kind)
            {
                case HintKind.ItemLocation:
                {
                    Assert.IsTrue(facts.Placement.TryGetPickup(hint.Item, out PickupPlacement p), context);
                    Assert.AreEqual(p.Screen.Face, hint.Face, $"{context}: item face ({text})");
                    if (hint.Precise) Assert.AreEqual(p.Screen, hint.Screen, $"{context}: item screen ({text})");
                    StringAssert.Contains(HintGenerator.ItemNickname(hint.Item), text, context);
                    break;
                }
                case HintKind.GateLocation:
                    Assert.IsTrue(facts.Placement.Gates.Any(g => g.Item == hint.Item && g.Screen.Face == hint.Face &&
                        (!hint.Precise || g.Screen == hint.Screen)), $"{context}: no such gate ({text})");
                    StringAssert.Contains(HintGenerator.ThemeName(hint.Item) + " gate", text, context);
                    break;
                case HintKind.CoreEntrance:
                {
                    Assert.IsTrue(facts.Layout.TryGetFace(hint.Face, out FaceLayout fl), $"{context}: core face ({text})");
                    if (hint.Precise) Assert.AreEqual(new ScreenAddress(fl.Face, fl.CoreCell), hint.Screen, $"{context}: core screen ({text})");
                    StringAssert.Contains("core", text, context);
                    break;
                }
                default:
                {
                    Assert.AreEqual(model.FaceOf(hint.FaceTheme), hint.Face, context);
                    bool adjacent = HintGenerator.TryTownEdge(hint.Face, out Facing edge);
                    if (hint.Precise)
                    {
                        if (adjacent)
                        {
                            Assert.AreEqual(hint.Face, CubeModel.NeighborFace(CubeModel.StartFace, edge), context);
                            StringAssert.Contains(HintGenerator.EdgeName(edge) + " edge", text, context);
                        }
                        else StringAssert.Contains("far side", text, context);
                    }
                    else StringAssert.Contains(adjacent ? "next door" : "long way", text, context);
                    if (model.IsSealed(hint.Face)) StringAssert.Contains("sealed", text, context);
                    place = null;
                    break;
                }
            }
            if (place != null) StringAssert.Contains(place, text, context);
        }

        // ---------- Parts ----------

        [Test]
        public void EveryRace_HasAtLeastThreePartsPerSlot_EachControllingSomethingDifferent()
        {
            foreach (Race race in RaceExtensions.All)
            {
                RacePartSet set = RacePartSets.For(race);
                Assert.GreaterOrEqual(set.Heads.Count, 3, $"{race} heads");
                Assert.GreaterOrEqual(set.Torsos.Count, 3, $"{race} torsos");
                Assert.GreaterOrEqual(set.Legs.Count, 3, $"{race} legs");
                Assert.AreEqual(set.Heads.Count, set.Heads.Select(h => h.Hint).Distinct().Count(), $"{race}: heads share a hint kind");
                Assert.AreEqual(set.Torsos.Count, set.Torsos.Select(t => t.Role).Distinct().Count(), $"{race}: torsos share a role");
                CollectionAssert.AreEquivalent(new[] { Movement.Stand, Movement.Wander, Movement.Flee }, set.Legs.Select(l => l.Movement), race.ToString());
                Assert.IsTrue(set.Heads.All(p => p.Slot == PartSlot.Head) && set.Torsos.All(p => p.Slot == PartSlot.Torso) &&
                              set.Legs.All(p => p.Slot == PartSlot.Legs), race.ToString());
            }
            var roles = RaceExtensions.All.SelectMany(r => RacePartSets.For(r).Torsos.Select(t => t.Role)).Distinct().ToList();
            CollectionAssert.IsSubsetOf(new[] { NpcRole.Merchant, NpcRole.QuestGiver, NpcRole.Smith, NpcRole.Gossip }, roles);
            var dino = RacePartSets.For(Race.Dinosaur);
            var variants = dino.Heads.Concat(dino.Torsos).Concat(dino.Legs).Select(p => p.Variant).Distinct().ToList();
            CollectionAssert.AreEquivalent(new[] { DinoVariant.Terrestrial, DinoVariant.Avian }, variants);
            Assert.AreEqual(Theme.Biology, Race.Finch.HomeTheme());
            Assert.AreEqual(Theme.Town, Race.Townsfolk.HomeTheme());
        }

        [Test]
        public void NpcStream_IsNotOneOfTheTakenStreams()
        {
            Assert.AreNotEqual(CubeLayout.RngStream, NpcFactory.RngStream);
            Assert.AreNotEqual(ItemPlacement.RngStream, NpcFactory.RngStream);
        }

        [Test]
        public void SameSeed_GivesIdenticalParts()
        {
            foreach (Race race in RaceExtensions.All)
                for (int index = 0; index < 8; index++)
                {
                    NpcSpec a = NpcFactory.ChooseParts(1234, race, index);
                    NpcSpec b = NpcFactory.ChooseParts(1234, race, index);
                    Assert.AreEqual(a.Signature(), b.Signature(), $"{race} #{index}");
                }
        }

        [Test]
        public void SeedsOneToTwenty_GiveVariedParts()
        {
            foreach (Race race in RaceExtensions.All)
            {
                var combos = new HashSet<string>();
                for (int seed = 1; seed <= 20; seed++)
                {
                    NpcSpec spec = NpcFactory.ChooseParts(seed, race, 0);
                    combos.Add($"{spec.Head.Id}/{spec.Torso.Id}/{spec.Legs.Id}");
                }
                Assert.GreaterOrEqual(combos.Count, 2, $"{race}: every seed gave the same parts");
            }
        }

        // ---------- Hints ----------

        [Test]
        public void HeadDecidesTheHint_DifferentHeadsGiveDifferentTrueHints()
        {
            foreach (HintDensity density in new[] { HintDensity.Full, HintDensity.Sparse })
            {
                for (int seed = 1; seed <= 20; seed++)
                {
                    RunFacts facts = Facts(seed);
                    foreach (Race race in RaceExtensions.All)
                    {
                        NpcSpec spec = NpcFactory.ChooseParts(seed, race, 0);
                        IReadOnlyList<NpcPart> heads = RacePartSets.For(race).Heads;
                        var texts = new List<string>();
                        foreach (NpcPart head in heads)
                        {
                            NpcSpec swapped = spec.With(head);
                            Assert.AreEqual(spec.Torso, swapped.Torso);
                            Assert.AreEqual(spec.Legs, swapped.Legs);
                            List<Hint> hints = HintGenerator.Generate(facts, swapped.HintKind, swapped.Salt, density);
                            Assert.IsNotEmpty(hints, $"seed {seed} {race} {head}");
                            foreach (Hint hint in hints)
                            {
                                Assert.AreEqual(head.Hint, hint.Kind, $"seed {seed} {race} {head}: wrong kind");
                                AssertTrue(hint, facts, $"seed {seed} {race} {head} {density}");
                            }
                            List<string> lines = Dialogue.For(swapped, facts, density);
                            Assert.LessOrEqual(lines.Count, Dialogue.MaxLines, "At most a flavour line and the hint");
                            Assert.AreEqual(hints[0].Text, lines[lines.Count - 1], "The first (true) hint ends the conversation");
                            texts.Add(string.Join("|", hints.Select(h => h.Text)));
                        }
                        Assert.AreEqual(texts.Count, texts.Distinct().Count(), $"seed {seed} {race}: two heads gave the same hint");
                    }
                }
            }
        }

        [Test]
        public void SparseDensity_GivesFewerVaguerHintsThanFull()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                RunFacts facts = Facts(seed);
                foreach (HintKind kind in new[] { HintKind.ItemLocation, HintKind.GateLocation, HintKind.CoreEntrance, HintKind.FaceTheme })
                {
                    for (uint salt = 0; salt < 6; salt++)
                    {
                        List<Hint> full = HintGenerator.Generate(facts, kind, salt, HintDensity.Full);
                        List<Hint> sparse = HintGenerator.Generate(facts, kind, salt, HintDensity.Sparse);
                        Assert.Less(sparse.Count, full.Count, $"seed {seed} {kind}");
                        Assert.IsNotEmpty(sparse);
                        Assert.IsTrue(full.All(h => h.Precise), $"seed {seed} {kind}: full hints name the place");
                        foreach (Hint hint in sparse)
                        {
                            Assert.IsFalse(hint.Precise);
                            string lower = hint.Text.ToLowerInvariant();
                            foreach (string word in ScreenWords)
                                StringAssert.DoesNotContain(word, lower, $"seed {seed} {kind}: sparse hint names a screen: {hint.Text}");
                            AssertTrue(hint, facts, $"seed {seed} {kind} sparse");
                        }
                    }
                }
            }
        }

        [Test]
        public void WithoutLayoutOrPlacement_HintsFallBackToTrueFaceHints()
        {
            var facts = new RunFacts(new CubeModel(9));
            foreach (HintKind kind in new[] { HintKind.ItemLocation, HintKind.GateLocation, HintKind.CoreEntrance })
            {
                List<Hint> hints = HintGenerator.Generate(facts, kind, 3, HintDensity.Full);
                Assert.IsNotEmpty(hints);
                foreach (Hint hint in hints)
                {
                    Assert.AreEqual(HintKind.FaceTheme, hint.Kind);
                    AssertTrue(hint, facts, kind.ToString());
                }
            }
        }

        // ---------- Dialogue ----------

        [Test]
        public void TorsoDecidesTheRole_DifferentTorsosGiveDifferentRoleLines()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                RunFacts facts = Facts(seed);
                foreach (Race race in RaceExtensions.All)
                {
                    NpcSpec spec = NpcFactory.ChooseParts(seed, race, 0);
                    var roleLines = new List<string>();
                    foreach (NpcPart torso in RacePartSets.For(race).Torsos)
                    {
                        NpcSpec swapped = spec.With(torso);
                        List<string> lines = Dialogue.For(swapped, facts, HintDensity.Full);
                        string roleLine = Dialogue.RoleLine(swapped);
                        Assert.AreEqual(roleLine, lines[0], "The flavour line is the role line");
                        Assert.IsFalse(roleLine.Contains("{"), $"Unfilled token: {roleLine}");
                        roleLines.Add(roleLine);
                    }
                    Assert.AreEqual(roleLines.Count, roleLines.Distinct().Count(), $"seed {seed} {race}: two torsos gave the same role line");
                }
            }

            // The merchant pitch comes from the merchant table, flavoured by the race's wares.
            NpcPart merchantTorso = RacePartSets.For(Race.Mushroom).Torsos.First(t => t.Role == NpcRole.Merchant);
            NpcSpec merchant = NpcFactory.ChooseParts(1, Race.Mushroom, 0).With(merchantTorso);
            StringAssert.Contains("pores", Dialogue.RoleLine(merchant));
        }

        [Test]
        public void TablesAreSmall_AndRaceFlavourChangesTheLines()
        {
            foreach (string[] table in Dialogue.RoleLines.Values) Assert.That(table.Length, Is.InRange(3, 4));
            foreach (Dialogue.RaceFlavour f in Dialogue.Flavours.Values)
            {
                Assert.That(f.Rumours.Length, Is.InRange(3, 4));
                Assert.That(f.Greetings.Length, Is.InRange(1, 4));
            }
            var greetings = RaceExtensions.All.Select(r => Dialogue.Greeting(NpcFactory.ChooseParts(5, r, 0))).ToList();
            Assert.AreEqual(greetings.Count, greetings.Distinct().Count(), "Each race greets in its own voice");
        }

        [Test]
        public void NoLine_EverSaysTheForbiddenWord()
        {
            foreach (string text in Dialogue.AllFixedText())
                StringAssert.DoesNotContain(Dialogue.ForbiddenWord, text.ToLowerInvariant(), text);

            for (int seed = 1; seed <= 30; seed++)
            {
                RunFacts facts = Facts(seed);
                foreach (Race race in RaceExtensions.All)
                    for (int index = 0; index < 4; index++)
                        foreach (HintDensity density in new[] { HintDensity.Full, HintDensity.Sparse })
                        {
                            NpcSpec spec = NpcFactory.ChooseParts(seed, race, index);
                            foreach (string line in Dialogue.For(spec, facts, density))
                                StringAssert.DoesNotContain(Dialogue.ForbiddenWord, line.ToLowerInvariant(), line);
                            StringAssert.DoesNotContain(Dialogue.ForbiddenWord, spec.DisplayName.ToLowerInvariant());
                        }
            }
        }

        // ---------- Relations ----------

        [Test]
        public void RaceRelations_TogglesPerRace_RaisesChanged_AndResets()
        {
            var relations = new RaceRelations();
            var events = new List<(Race, bool)>();
            relations.Changed += (race, hostile) => events.Add((race, hostile));

            Assert.IsTrue(RaceExtensions.All.All(r => !relations.IsHostile(r)));
            Assert.IsTrue(relations.Toggle(Race.Mushroom));
            relations.SetHostile(Race.Mushroom, true); // no change, no event
            Assert.IsTrue(relations.IsHostile(Race.Mushroom));
            Assert.IsFalse(relations.IsHostile(Race.Finch));
            relations.SetHostile(Race.Shape, true);
            relations.Reset();
            Assert.IsTrue(RaceExtensions.All.All(r => !relations.IsHostile(r)));
            CollectionAssert.AreEqual(new[]
            {
                (Race.Mushroom, true), (Race.Shape, true), (Race.Mushroom, false), (Race.Shape, false),
            }, events);
        }
    }
}
