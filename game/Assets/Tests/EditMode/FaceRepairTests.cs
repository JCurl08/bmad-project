using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Fragments and face repair (4.8): the die numbers (Town always 1, the others 2-6 and varying) on seeds 1-50, the
    /// fragments they place (face numbered k: fragments 1..k, all reachable; the slots above k chest keys since 4.9, the
    /// 4.7 draws untouched), the leaders' quest (naming the count; a hand-in of all k mends the face; the Mayor gives
    /// Town's one fragment, then mends Town), and the boss benefits (fixed per theme; each changes exactly its arena
    /// parameters in the player's favour; none gives the 1.11 arena value for value).
    /// </summary>
    public class FaceRepairTests
    {
        private const string LibraryPath = "Assets/Modules/ModuleLibrary.asset";
        private const string ItemCatalogPath = "Assets/Items/ItemCatalog.asset";

        private static ModuleLibrary Library()
        {
            var library = AssetDatabase.LoadAssetAtPath<ModuleLibrary>(LibraryPath);
            Assert.IsNotNull(library, $"{LibraryPath} missing");
            return library;
        }

        private static ItemCatalog Catalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogPath);
            Assert.IsNotNull(catalog, $"{ItemCatalogPath} missing");
            return catalog;
        }

        private sealed class Run
        {
            public CubeModel Model;
            public CubeLayout Layout;
            public ItemPlacement Items;
            public HiddenCurrencyPlacement Hidden;
            public DieNumbers Dice;
            public RewardPlacement Rewards;
            public MathPlan Math;
            public IcePlan Ice;
            public WindPlan Wind;
            public IslandPlan Islands;
        }

        private static Run Make(int seed, DieNumbers dice = null)
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            var run = new Run { Model = new CubeModel(seed) };
            run.Layout = CubeLayout.Generate(run.Model, library);
            run.Items = ItemPlacement.ForRun(run.Model, run.Layout, library, catalog.Themes(), catalog.VariantCounts());
            run.Hidden = HiddenCurrencyPlacement.Generate(run.Model, run.Layout, library);
            run.Dice = dice ?? DieNumbers.Roll(run.Model);
            run.Ice = IcePlan.Create(run.Model, run.Layout, run.Dice);
            run.Wind = WindPlan.Create(run.Model, run.Layout, run.Dice);
            run.Islands = IslandPlan.Create(run.Model, run.Layout, run.Dice);
            run.Rewards = RewardPlacement.Generate(run.Model, run.Layout, run.Items, run.Hidden, library, PuzzleScreens.Goals(run.Ice, run.Wind, run.Islands), run.Dice);
            run.Math = MathPlan.Create(run.Model, run.Items, SeedSweep.ModuleLookup(run.Layout, library), dice: run.Dice);
            return run;
        }

        private static List<string> Problems(Run run, RewardPlacement rewards = null) =>
            RewardPlacement.FindProblems(run.Model, run.Layout, run.Items, run.Hidden, rewards ?? run.Rewards, Library(),
                PuzzleScreens.RegionsAt(run.Math, run.Ice, run.Wind, run.Islands), PuzzleScreens.Goals(run.Ice, run.Wind, run.Islands), run.Dice);

        // ---------- Die numbers ----------

        [Test]
        public void DieNumbers_TownIsAlways1_TheOthersAre2To6_AndTheyVary_OverSeeds1To50()
        {
            var byFace = new Dictionary<FaceId, HashSet<int>>();
            var byTheme = new Dictionary<Theme, HashSet<int>>();
            var orders = new HashSet<string>();
            for (int seed = 1; seed <= 50; seed++)
            {
                var model = new CubeModel(seed);
                DieNumbers dice = DieNumbers.Roll(model);
                Assert.AreEqual(dice.Signature(), DieNumbers.Roll(model).Signature(), $"seed {seed}: the same seed rolls the same numbers");
                Assert.AreEqual(1, dice.Of(CubeModel.StartFace), $"seed {seed}: Town is 1");
                Assert.AreEqual(Theme.Town, model.ThemeOf(CubeModel.StartFace));
                var others = new List<int>();
                for (int f = 0; f < CubeSettings.FaceCount; f++)
                {
                    var face = (FaceId)f;
                    if (face == CubeModel.StartFace) continue;
                    others.Add(dice.Of(face));
                    if (!byFace.TryGetValue(face, out HashSet<int> seen)) byFace[face] = seen = new HashSet<int>();
                    seen.Add(dice.Of(face));
                    Theme theme = model.ThemeOf(face);
                    if (!byTheme.TryGetValue(theme, out HashSet<int> themeSeen)) byTheme[theme] = themeSeen = new HashSet<int>();
                    themeSeen.Add(dice.Of(face));
                }
                CollectionAssert.AreEquivalent(new[] { 2, 3, 4, 5, 6 }, others, $"seed {seed}: the others are 2-6 once each");
                for (int k = 1; k <= 6; k++) Assert.AreEqual(k, dice.Of(dice.FaceNumbered(k)));
                orders.Add(dice.Signature());
            }
            foreach (KeyValuePair<FaceId, HashSet<int>> e in byFace)
                CollectionAssert.AreEquivalent(new[] { 2, 3, 4, 5, 6 }, e.Value, $"{e.Key} shows every number over the seeds");
            foreach (KeyValuePair<Theme, HashSet<int>> e in byTheme)
                Assert.GreaterOrEqual(e.Value.Count, 4, $"{e.Key}'s face gets varied numbers");
            Assert.GreaterOrEqual(orders.Count, 35, "The order varies from run to run");
        }

        // ---------- Fragments ----------

        [Test]
        public void EveryFragment_IsPlacedAndReachable_TheSlotsAboveKAreChestKeys_OverSeeds1To50()
        {
            for (int seed = 1; seed <= 50; seed++)
            {
                Run run = Make(seed);
                CollectionAssert.IsEmpty(Problems(run), $"seed {seed}");
                int total = 0;
                foreach (FaceId face in run.Layout.Faces.Keys)
                {
                    int k = run.Dice.Of(face);
                    List<RewardSpot> fragments = run.Rewards.FragmentSlots(face);
                    CollectionAssert.AreEqual(Enumerable.Range(1, k), fragments.Select(s => s.What.Ordinal), $"seed {seed} {face}");
                    Assert.IsTrue(fragments.All(s => s.Required && s.What.Amount == 0), "Required, and a fragment is no Jumbles*");
                    total += fragments.Count;
                    Assert.AreEqual(RewardPlacement.FragmentSlotsPerFace - k,
                        run.Rewards.Spots.Count(s => s.Face == face && s.What.Kind == RewardKind.ChestKey), "4.9: a chest key in each slot above k");
                }
                Assert.AreEqual(DieNumbers.FragmentsPerRun - DieNumbers.TownNumber, total, $"seed {seed}: 2+3+4+5+6 on the science faces");
            }
        }

        [Test]
        public void TheDieNumbers_DecideOnlyWhatTheReservedSlotsHold_EveryOtherRewardIsAsIn47()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                Run run = Make(seed);
                Run other = Make(seed, DieNumbers.Roll(seed + 1000));
                Assert.AreEqual(run.Rewards.Spots.Count, other.Rewards.Spots.Count);
                int differ = 0;
                for (int i = 0; i < run.Rewards.Spots.Count; i++)
                {
                    RewardSpot a = run.Rewards.Spots[i], b = other.Rewards.Spots[i];
                    Assert.AreEqual(a.Where, b.Where, $"seed {seed}: same roadblocks in the same order");
                    Assert.AreEqual(a.Required, b.Required);
                    if (a.What.Equals(b.What)) continue;
                    differ++;
                    // Only a fragment and a chest key (4.9: a slot above k) swap places, in the reserved slots.
                    var kinds = new[] { a.What.Kind, b.What.Kind };
                    CollectionAssert.AreEquivalent(new[] { RewardKind.FragmentSlot, RewardKind.ChestKey }, kinds, $"seed {seed}: {a} vs {b}");
                    RewardSpot above = a.What.Kind == RewardKind.ChestKey ? a : b;
                    Assert.AreEqual(-1, above.HiddenIndex);
                }
                Assert.Greater(differ, 0, $"seed {seed}: other dice, other fragment counts");
            }
        }

        [Test]
        public void TheSweep_FlagsAFaceMissingAFragment()
        {
            Run run = Make(5);
            FaceId face = run.Layout.Faces.Keys.First();
            RewardSpot victim = run.Rewards.FragmentSlots(face).Last();
            var broken = new RewardPlacement(run.Model.Seed, run.Rewards.Spots.Select(s =>
                s.Index == victim.Index ? new RewardSpot(s.Index, s.Where, Reward.Jumbles(5), true) : s));
            Assert.IsTrue(Problems(run, broken).Any(p => p.Contains("holds")), string.Join("\n", Problems(run, broken)));
        }

        // ---------- The leaders' quest ----------

        [Test]
        public void AHandIn_OfAllKFragments_RepairsTheFace_AndTheQuestNamesTheCount()
        {
            var go = new GameObject("Player");
            try
            {
                RunRewards rewards = go.AddComponent<RunRewards>();
                var repairs = new FaceRepairs();
                const FaceId face = FaceId.Back;
                const int count = 4;
                for (int found = 0; found < count; found++)
                {
                    List<string> said = LeaderQuest.Lines(Race.Shape, face, count, rewards, repairs, true, out QuestStep step);
                    Assert.AreEqual(QuestStep.Asked, step, $"{found} of {count}: still asking");
                    CollectionAssert.AreEqual(Dialogue.QuestConversation(Race.Shape, count, found), said);
                    StringAssert.Contains("four", said[1], "The quest names the count");
                    StringAssert.Contains(Dialogue.CountWord(found), said[1], "and how many are held");
                    Assert.IsFalse(repairs.IsRepaired(face));
                    Assert.IsTrue(rewards.Grant(Reward.Fragment(found + 1), face));
                }
                List<string> preview = LeaderQuest.Lines(Race.Shape, face, count, rewards, repairs, false, out QuestStep looked);
                Assert.AreEqual(QuestStep.Asked, looked, "A preview never hands in");
                CollectionAssert.AreEqual(Dialogue.QuestConversation(Race.Shape, count, count), preview);

                List<string> handIn = LeaderQuest.Lines(Race.Shape, face, count, rewards, repairs, true, out QuestStep handed);
                Assert.AreEqual(QuestStep.Repaired, handed, "All four: the leader mends the face");
                CollectionAssert.AreEqual(Dialogue.RepairConversation(Race.Shape, count), handIn);
                Assert.IsTrue(repairs.Repair(face));
                Assert.IsFalse(repairs.Repair(face), "Once");
                Assert.AreEqual(BossBenefit.WiderGap, BossBenefits.Of(Theme.Math));

                LeaderQuest.Lines(Race.Shape, face, count, rewards, repairs, true, out QuestStep after);
                Assert.AreEqual(QuestStep.Mended, after);

                // Another face's fragments never count.
                LeaderQuest.Lines(Race.Finch, FaceId.Top, 2, rewards, repairs, true, out QuestStep other);
                Assert.AreEqual(QuestStep.Asked, other);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TheMayor_SaysTownsFragmentWentMissing_ThenTakesItBackOnceFound_AndMendsTown()
        {
            // 4.18: the Mayor no longer hands the fragment over (Town's lost-and-found trail does, TownTrailQuest). Without
            // it he only says it went missing; once the player holds it, handing it in mends Town.
            var go = new GameObject("Player");
            try
            {
                RunRewards rewards = go.AddComponent<RunRewards>();
                var repairs = new FaceRepairs();
                const FaceId town = CubeModel.StartFace;
                List<string> first = LeaderQuest.Lines(Race.Townsfolk, town, 1, rewards, repairs, true, out QuestStep lost);
                Assert.AreEqual(QuestStep.Lost, lost);
                CollectionAssert.AreEqual(new[] { Dialogue.LeaderLine(Race.Townsfolk), Dialogue.MayorGiftLine }, first);
                StringAssert.Contains("missing", Dialogue.MayorGiftLine, "The Mayor says it went missing");
                Assert.IsFalse(rewards.HasFragment(town, 1), "The Mayor never hands the fragment over");
                Assert.IsFalse(repairs.IsRepaired(town));

                Assert.IsTrue(rewards.Grant(Reward.Fragment(1), town), "Found on the trail");
                List<string> second = LeaderQuest.Lines(Race.Townsfolk, town, 1, rewards, repairs, true, out QuestStep handed);
                Assert.AreEqual(QuestStep.Repaired, handed);
                CollectionAssert.AreEqual(Dialogue.RepairConversation(Race.Townsfolk, 1), second);
                repairs.Repair(town);
                Assert.AreEqual(BossBenefit.ExtraHeart, repairs.Benefits(new CubeModel(3)));
                LeaderQuest.Lines(Race.Townsfolk, town, 1, rewards, repairs, true, out QuestStep after);
                Assert.AreEqual(QuestStep.Mended, after);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void WithNoReadyTrail_TheMayorFallsBack_ToHandingTownsFragmentOver()
        {
            // 4.18 review: if Town's trail cannot run (a trail person found no spot), the Mayor hands the fragment over (4.8).
            var go = new GameObject("Player");
            try
            {
                RunRewards rewards = go.AddComponent<RunRewards>();
                var repairs = new FaceRepairs();
                const FaceId town = CubeModel.StartFace;
                List<string> preview = LeaderQuest.Lines(Race.Townsfolk, town, 1, rewards, repairs, false, out QuestStep asked, false);
                Assert.AreEqual(QuestStep.Asked, asked);
                Assert.IsFalse(rewards.HasFragment(town, 1), "a preview never gives it");
                List<string> first = LeaderQuest.Lines(Race.Townsfolk, town, 1, rewards, repairs, true, out QuestStep gave, false);
                Assert.AreEqual(QuestStep.Gave, gave);
                CollectionAssert.AreEqual(new[] { Dialogue.LeaderLine(Race.Townsfolk), Dialogue.MayorFoundLine }, first);
                CollectionAssert.AreEqual(preview, first);
                Assert.IsTrue(rewards.HasFragment(town, 1));
                LeaderQuest.Lines(Race.Townsfolk, town, 1, rewards, repairs, true, out QuestStep handed, false);
                Assert.AreEqual(QuestStep.Repaired, handed, "the next talk mends Town");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EveryQuestLine_NamesItsCount_InItsVoice_AndKeepsTheTextRules()
        {
            foreach (Race race in RaceExtensions.All)
            {
                foreach (string line in Dialogue.LeaderQuestLines(race))
                {
                    Assert.LessOrEqual(line.Length, Dialogue.MaxLineLength, line);
                    CollectionAssert.IsEmpty(InWorldText.Problems(line), line);
                    StringAssert.DoesNotContain("{", line, "Every placeholder filled");
                }
                if (race == Race.Townsfolk) continue;
                for (int count = 2; count <= 6; count++)
                {
                    string quest = Dialogue.QuestLine(race, count, 1);
                    StringAssert.Contains(Dialogue.CountWord(count), quest, $"{race}: names {count}");
                    StringAssert.Contains("mend", quest);
                    StringAssert.Contains(Dialogue.CountWord(count), Dialogue.RepairLine(race, count), $"{race}: the hand-in names {count}");
                }
            }
            foreach (string text in BossBenefits.AllText()) CollectionAssert.IsEmpty(InWorldText.Problems(text), text);
            CollectionAssert.IsEmpty(InWorldText.Problems(RewardText.FragmentsLine(new[] { (Theme.Town, 1, 1, true), (Theme.Math, 2, 6, false) })));
        }

        [Test]
        public void TheDieBadges_ShowTheirNumberAsPips_AndTheRepairArtIsPixelArt()
        {
            for (int n = 1; n <= 6; n++)
            {
                Sprite die = TownArt.DieFace(n);
                Assert.AreEqual(FilterMode.Point, die.texture.filterMode);
                Assert.AreEqual(TownLayout.PixelsPerUnit, die.pixelsPerUnit);
                Color32[] px = die.texture.GetPixels32();
                int w = die.texture.width, pips = 0;
                for (int y = TownArt.Margin; y < TownArt.Margin + 7; y++)
                    for (int x = TownArt.Margin; x < TownArt.Margin + 7; x++)
                    {
                        Color32 p = px[y * w + x];
                        if (p.a > 0 && p.r == TownArt.OutlineColor.r && p.g == TownArt.OutlineColor.g && p.b == TownArt.OutlineColor.b) pips++;
                    }
                Assert.AreEqual(n, pips, $"Die {n} shows {n} pips");
                Assert.AreSame(die, TownArt.DieFace(n), "Cached");
            }
            foreach (Theme theme in new[] { Theme.Town, Theme.Biology, Theme.Chemistry, Theme.Physics, Theme.Math, Theme.EarthAtmosphere })
            {
                Sprite banner = TownArt.RepairBanner(theme);
                Assert.AreEqual(FilterMode.Point, banner.texture.filterMode, theme.ToString());
                Assert.AreEqual(TownLayout.PixelsPerUnit, banner.pixelsPerUnit);
            }
            Assert.AreEqual(FilterMode.Point, TownArt.RepairHalo().texture.filterMode);
        }

        // ---------- Boss benefits ----------

        [Test]
        public void EachFaceTheme_GivesItsOwnBenefit_AndRepairedFacesAddUp()
        {
            var themes = new[] { Theme.Town, Theme.Biology, Theme.Chemistry, Theme.Physics, Theme.Math, Theme.EarthAtmosphere };
            CollectionAssert.AreEquivalent(BossBenefits.All, themes.Select(BossBenefits.Of), "Six themes, six benefits, one each");
            Assert.AreEqual(BossBenefit.ExtraHeart, BossBenefits.Of(Theme.Town));
            Assert.AreEqual(BossBenefit.Attraction, BossBenefits.Of(Theme.Biology));
            Assert.AreEqual(BossBenefit.FewerParticles, BossBenefits.Of(Theme.Chemistry));
            Assert.AreEqual(BossBenefit.SlowerRestore, BossBenefits.Of(Theme.Physics));
            Assert.AreEqual(BossBenefit.WiderGap, BossBenefits.Of(Theme.Math));
            Assert.AreEqual(BossBenefit.SlowerShots, BossBenefits.Of(Theme.EarthAtmosphere));
            var model = new CubeModel(9);
            BossBenefit two = BossBenefits.From(new[] { CubeModel.StartFace, model.FaceOf(Theme.Math) }, model);
            Assert.AreEqual(BossBenefit.ExtraHeart | BossBenefit.WiderGap, two);
            Assert.AreEqual(BossBenefit.None, BossBenefits.From(new FaceId[0], model));
            Assert.IsNull(BossBenefits.HudLine(BossBenefit.None));
            Assert.AreEqual("Help from mended faces: +1 heart, wider gaps", BossBenefits.HudLine(two));
        }

        [Test]
        public void NoBenefits_IsThe111Arena_ValueForValue()
        {
            ArenaTuning t = ArenaTuning.For(BossBenefit.None);
            Assert.AreEqual(CoreArena.DefaultParticlesPerSide, t.ParticlesPerSide);
            Assert.AreEqual(CoreArena.GapHeight, t.GapHeight);
            Assert.AreEqual(0f, t.Attract);
            Assert.AreEqual(1f, t.RestoreScale);
            Assert.AreEqual(MaxwellDemon.NudgeSpeed, t.NudgeSpeed);
            Assert.AreEqual(0, t.Hearts);
            Assert.AreEqual(MaxwellDemon.PulseInterval, t.PulseInterval);
            Assert.AreEqual(MaxwellDemon.PulseSpeed, t.PulseSpeed);
            Assert.AreEqual(Vector2.zero, Particle.Pull(Vector2.zero, Vector2.right, 0f), "No pull without the benefit");
        }

        private static IEnumerable<BossBenefit> Benefits() => BossBenefits.All;

        [Test]
        public void EachBenefit_ChangesExactlyItsArenaParameters_InThePlayersFavour([ValueSource(nameof(Benefits))] BossBenefit benefit)
        {
            ArenaTuning b = ArenaTuning.Base(), t = ArenaTuning.For(benefit);
            var changed = new List<string>();
            if (t.ParticlesPerSide != b.ParticlesPerSide) changed.Add("particles");
            if (t.GapHeight != b.GapHeight) changed.Add("gap");
            if (t.Attract != b.Attract) changed.Add("attract");
            if (t.RestoreScale != b.RestoreScale || t.NudgeSpeed != b.NudgeSpeed) changed.Add("restore");
            if (t.Hearts != b.Hearts) changed.Add("hearts");
            if (t.PulseInterval != b.PulseInterval || t.PulseSpeed != b.PulseSpeed) changed.Add("pulses");
            switch (benefit)
            {
                case BossBenefit.FewerParticles:
                    CollectionAssert.AreEqual(new[] { "particles" }, changed);
                    Assert.AreEqual(6, t.ParticlesPerSide, "8 a side become 6 (16 become 12)");
                    break;
                case BossBenefit.WiderGap:
                    CollectionAssert.AreEqual(new[] { "gap" }, changed);
                    Assert.AreEqual(b.GapHeight * 1.5f, t.GapHeight, 1e-4f, "Gaps half as tall again");
                    break;
                case BossBenefit.Attraction:
                    CollectionAssert.AreEqual(new[] { "attract" }, changed);
                    Assert.Greater(t.Attract, 0f);
                    Assert.Less(t.Attract + Particle.DriftSpeed, 2f * Particle.DriftSpeed, "Slight: under the speed a particle coasts at");
                    Vector2 pull = Particle.Pull(Vector2.zero, new Vector2(3f, 4f), t.Attract);
                    Assert.AreEqual(t.Attract, pull.magnitude, 1e-4f, "Pulled at AttractSpeed");
                    Assert.Greater(Vector2.Dot(pull, new Vector2(3f, 4f)), 0f, "towards the player");
                    Assert.AreEqual(Vector2.zero, Particle.Pull(Vector2.zero, Vector2.right * (ArenaTuning.AttractRange + 1f), t.Attract), "within range only");
                    break;
                case BossBenefit.SlowerRestore:
                    CollectionAssert.AreEqual(new[] { "restore" }, changed);
                    Assert.AreEqual(2f, t.RestoreScale, "The pause between fetches doubles");
                    Assert.Less(t.NudgeSpeed, b.NudgeSpeed, "and fetching is slower");
                    break;
                case BossBenefit.ExtraHeart:
                    CollectionAssert.AreEqual(new[] { "hearts" }, changed);
                    Assert.AreEqual(1, t.Hearts);
                    break;
                case BossBenefit.SlowerShots:
                    CollectionAssert.AreEqual(new[] { "pulses" }, changed);
                    Assert.Greater(t.PulseInterval, b.PulseInterval, "Rarer");
                    Assert.Less(t.PulseSpeed, b.PulseSpeed, "Slower");
                    break;
                default:
                    Assert.Fail($"No expectation for {benefit}");
                    break;
            }
            // All six together change all six parameters.
            BossBenefit every = BossBenefits.All.Aggregate(BossBenefit.None, (x, y) => x | y);
            ArenaTuning all = ArenaTuning.For(every);
            Assert.AreEqual(6, all.ParticlesPerSide);
            Assert.AreEqual(1, all.Hearts);
        }
    }
}
