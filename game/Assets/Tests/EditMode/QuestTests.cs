using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Playtest 4 polish (4.18), pure: the quest log (add once, steps, done, the order it shows, live leader progress, its
    /// words in the text rule, its layout at 960x600 and its key on the map); one errand per town, so no two people on a face
    /// give the same quest; Town's lost-and-found trail, completable on seeds 1-50 on its own stream; and the talk pause
    /// (a conversation stops time like the map, and the press that closes it is not the player's).
    /// </summary>
    public class QuestTests
    {
        private const string LibraryPath = "Assets/Modules/ModuleLibrary.asset";
        private const string ItemCatalogPath = "Assets/Items/ItemCatalog.asset";
        private const int N = CubeSettings.DefaultFaceSize;

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

        [TearDown]
        public void ResetPause()
        {
            // Leave no pause behind for other tests (statics survive between tests).
            GamePause.Release(QuestLogView.PauseKey);
            GamePause.Resume();
            GamePause.EndTalk(this);
            Time.timeScale = 1f;
        }

        // ---------- The quest log ----------

        [Test]
        public void QuestLog_AddsEachQuestOnce_UpdatesItsStep_MarksItDone_AndShowsOpenOnesFirst()
        {
            var log = new QuestLog();
            var added = new List<string>();
            var changed = new List<string>();
            log.Added += e => added.Add(e.Id);
            log.Changed += e => changed.Add(e.Id);

            QuestEntry town = log.Add(QuestLog.TownId, QuestKind.TownSide, CubeModel.StartFace, "Mayor Muddleton", QuestText.TownTitle,
                QuestText.TownAsk("Pipwick", "on Town's north-east screen"));
            QuestEntry errand = log.Add(QuestLog.ErrandId(FaceId.Right), QuestKind.Errand, FaceId.Right, "Wrenna",
                QuestText.ErrandTitle("Wrenna"), QuestText.ErrandStep("Wrenna", "Pipwick", Theme.Biology));
            Assert.AreSame(town, log.Add(QuestLog.TownId, QuestKind.TownSide, CubeModel.StartFace, "x", "x", "x"), "a quest is added once");
            CollectionAssert.AreEqual(new[] { QuestLog.TownId, QuestLog.ErrandId(FaceId.Right) }, added);
            Assert.AreEqual(2, log.Entries.Count);
            Assert.AreEqual(2, log.OpenCount);

            Assert.IsTrue(log.SetStep(QuestLog.TownId, QuestText.TownReturn));
            Assert.IsFalse(log.SetStep(QuestLog.TownId, QuestText.TownReturn), "no change, no event");
            Assert.AreEqual(QuestText.TownReturn, town.Step);
            Assert.IsTrue(log.Complete(QuestLog.TownId, QuestText.TownDone));
            Assert.IsFalse(log.Complete(QuestLog.TownId, QuestText.TownDone), "done once");
            Assert.IsFalse(log.SetStep(QuestLog.TownId, "again"), "a done quest stays done");
            Assert.IsTrue(town.Done);
            Assert.AreEqual(QuestText.TownDone, town.Step);
            CollectionAssert.AreEqual(new[] { QuestLog.TownId, QuestLog.TownId }, changed);
            Assert.AreEqual(1, log.OpenCount);
            CollectionAssert.AreEqual(new[] { errand, town }, log.Ordered(), "open quests first, then done ones");
            Assert.IsFalse(log.SetStep("nope", "x"));
            Assert.IsNull(log.Get("nope"));
        }

        [Test]
        public void QuestLog_LeaderQuests_ReadTheirProgressLive_AndAreDoneOnceTheFaceIsMended()
        {
            var model = new CubeModel(21);
            DieNumbers dice = DieNumbers.Roll(model);
            FaceId face = model.FaceOf(Theme.Biology);
            int count = dice.Of(face);
            var log = new QuestLog();
            log.Add(QuestLog.LeaderId(face), QuestKind.Leader, face, "Queen Wrennifer", QuestText.LeaderTitle(Theme.Biology),
                QuestText.LeaderStep(Theme.Biology, "Queen Wrennifer", count, 0), QuestText.FragmentProgress(0, count));
            var go = new GameObject("Player");
            try
            {
                RunRewards rewards = go.AddComponent<RunRewards>();
                var repairs = new FaceRepairs();
                Assert.IsTrue(rewards.Grant(Reward.Fragment(1), face));
                Assert.IsTrue(rewards.Grant(Reward.Fragment(2), face));
                log.RefreshLeaders(model, dice, repairs, rewards);
                QuestEntry entry = log.Get(QuestLog.LeaderId(face));
                Assert.AreEqual(QuestText.FragmentProgress(2, count), entry.Progress, "live progress from the player's fragments");
                StringAssert.Contains("Queen Wrennifer", entry.Step);
                Assert.IsFalse(entry.Done);
                for (int i = 3; i <= count; i++) rewards.Grant(Reward.Fragment(i), face);
                log.RefreshLeaders(model, dice, repairs, rewards);
                StringAssert.Contains("Take them to", entry.Step, "all held: take them in");
                repairs.Repair(face);
                log.RefreshLeaders(model, dice, repairs, rewards);
                Assert.IsTrue(entry.Done);
                Assert.AreEqual(QuestText.LeaderDone(Theme.Biology, "Queen Wrennifer"), entry.Step);
                Assert.IsNull(entry.Progress);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void QuestLogWords_KeepTheTextRule()
        {
            foreach (string text in QuestText.AllTexts().Concat(CubeMap.AllTexts()))
            {
                CollectionAssert.IsEmpty(InWorldText.Problems(text), text);
                StringAssert.DoesNotContain("{", text, "every placeholder filled");
            }
        }

        [Test]
        public void QuestLogLayout_FitsEveryQuestOfARun_ReadablyAt960x600()
        {
            const float w = 960f, h = 600f;
            // A run's most quests: five science leaders, Town's side quest and six errands.
            const int most = 5 + 1 + 6;
            Assert.GreaterOrEqual(QuestLogView.MaxShown(h), most, "every quest of a run fits");
            List<Rect> rects = QuestLogView.Layout(w, h, most);
            Assert.AreEqual(most, rects.Count);
            var screen = new Rect(0f, 0f, w, h);
            for (int i = 0; i < rects.Count; i++)
            {
                Assert.IsTrue(screen.Contains(rects[i].min) && screen.Contains(rects[i].max), $"entry {i} on screen: {rects[i]}");
                Assert.GreaterOrEqual(rects[i].y, QuestLogView.HeaderHeight, "under the header");
                Assert.GreaterOrEqual(rects[i].width, 400f, "wide enough for a step in two lines");
                for (int j = i + 1; j < rects.Count; j++) Assert.IsFalse(rects[i].Overlaps(rects[j]), $"entries {i} and {j} overlap");
            }
            Assert.AreEqual(1, QuestLogView.Layout(w, h, 3).Select(r => r.x).Distinct().Count(), "a short log is one column");
            Assert.GreaterOrEqual(QuestLogView.TitleFontSize, 16);
            Assert.GreaterOrEqual(QuestLogView.StepFontSize, 14);
            // Every step a quest can show fits two lines of its column (about 7.5 px a character at 15 pt).
            float charsPerLine = rects[0].width / 7.5f;
            foreach (string text in QuestText.AllTexts())
                Assert.LessOrEqual(text.Length, 2 * charsPerLine + 20, text);

            // The map's header says how to open the log, clear of the inside badge.
            foreach (float width in new[] { 800f, 960f, 1280f, 1920f })
            {
                Rect hint = CubeMap.QuestHintRect(width);
                Assert.LessOrEqual(hint.yMax, CubeMap.HeaderHeight, "in the header");
                Assert.IsFalse(hint.Overlaps(CubeMap.CloseHintRect(width)), "under the close hint");
                Assert.IsFalse(hint.Overlaps(CubeMap.InsideBadgeRect(width)), $"{width}: clear of the inside badge");
            }
            CollectionAssert.Contains(CubeMap.AllTexts().ToList(), QuestText.MapHint);
        }

        // ---------- One errand per face ----------

        /// <summary>Each voice's errand without its name: the words after the target's name.</summary>
        private static List<string> ErrandStems() => Voices.All
            .Select(v => Dialogue.Flavours[v].Errand)
            .Select(e => e.Substring(e.IndexOf("{name}", System.StringComparison.Ordinal) + "{name}".Length))
            .ToList();

        private static int ErrandCount(IEnumerable<string> lines) => lines.Count(l => ErrandStems().Any(l.Contains));

        [Test]
        public void EveryTown_HasOneErrandPlan_AGiverLabelledAsSuch_AndADifferentTarget_OverSeeds1To50()
        {
            // The plan only: who says what in play is read from the spawned people (QuestSceneTests, seeds 1-50).
            Assert.IsTrue(ErrandStems().All(s => s.Length >= 12), "every errand has a telling stem");
            Assert.AreEqual(ErrandStems().Count, ErrandStems().Distinct().Count(), "every voice's errand is its own");
            int faces = 0;
            for (int seed = 1; seed <= 50; seed++)
            {
                var model = new CubeModel(seed);
                Dictionary<FaceId, ErrandPlan> errands = Errands.ForRun(model);
                for (int f = 0; f < CubeSettings.FaceCount; f++)
                {
                    var face = (FaceId)f;
                    Theme theme = model.ThemeOf(face);
                    if (face != CubeModel.StartFace && !CubeLayout.IsLaidOut(model, face)) continue;
                    Assert.IsTrue(errands.TryGetValue(face, out ErrandPlan plan), $"seed {seed}: {face} ({theme}) has its errand");
                    faces++;
                    Assert.AreNotEqual(plan.GiverIndex, plan.TargetIndex);
                    List<NpcSpec> people = Errands.People(seed, N, theme);
                    Assert.AreEqual(people[plan.TargetIndex].Signature(), plan.Target.Signature(), "the target is one of the town's people");
                    Assert.AreEqual(people[plan.GiverIndex].Signature(), plan.Giver.Signature(), "so is the giver");
                    Assert.AreEqual(NpcSpec.RoleName(NpcRole.QuestGiver).ToLowerInvariant(), plan.Giver.Kind.Split(' ').Last(),
                        $"seed {seed}: the errand's giver is labelled a quest-giver ({plan.Giver.DisplayName})");
                    int firstQuestGiver = people.FindIndex(p => p.Role == NpcRole.QuestGiver);
                    Assert.AreEqual(firstQuestGiver < 0 ? 0 : firstQuestGiver, plan.GiverIndex, "the first quest-giver gives it");
                }
            }
            Assert.AreEqual(50 * 6, faces, "every face of every seed has its errand");
        }

        [Test]
        public void SpareQuestGivers_PointToTheErrandGiver_OrSayPlainlyTheyHaveNone_AndTheErrandNamesItsTarget()
        {
            foreach (Race race in RaceExtensions.All)
            {
                NpcSpec folk = NpcFactory.ChooseParts(7, race, 3);
                NpcPart questTorso = RacePartSets.For(race).Torsos.First(t => t.Role == NpcRole.QuestGiver);
                NpcSpec giver = folk.With(questTorso);
                string pointing = Dialogue.RoleLine(giver, "Bessie");
                string plain = Dialogue.RoleLine(giver);
                foreach (string spare in new[] { pointing, plain })
                {
                    Assert.AreEqual(0, ErrandCount(new[] { spare }), $"{race}: a quest-giver with no errand gives none: {spare}");
                    Assert.LessOrEqual(spare.Length, Dialogue.MaxLineLength);
                    StringAssert.DoesNotContain("{", spare);
                }
                StringAssert.Contains("Bessie", pointing, $"{race}: points to the face's errand-giver, true before and after it is given");
                StringAssert.DoesNotContain("Bessie", plain);
                Assert.AreEqual(pointing, Dialogue.FlavourLine(giver, "Bessie"));
                string errand = Dialogue.ErrandLine(giver, "Bessie");
                StringAssert.Contains("Bessie", errand);
                Assert.AreEqual(1, ErrandCount(new[] { errand }), $"{race}: {errand}");
                Assert.LessOrEqual(errand.Length, Dialogue.MaxLineLength);
                CollectionAssert.IsEmpty(InWorldText.Problems(errand), errand);
            }
        }

        // ---------- Town's lost-and-found trail ----------

        [Test]
        public void TownTrail_IsCompletable_OnSeeds1To50_TwoOrThreeTownsfolk_WithAFavour_OnItsOwnStream()
        {
            ModuleLibrary library = Library();
            var lengths = new HashSet<int>();
            var favourSteps = new HashSet<int>();
            var problems = new List<string>();
            for (int seed = 1; seed <= 50; seed++)
            {
                TownTrail trail = TownTrail.For(seed, N);
                Assert.IsNotNull(trail, $"seed {seed}: a trail");
                List<NpcSpec> specs = TownPlan.Specs(seed, N);
                List<TownSpot> spots = TownPlan.Spots(seed, N, specs.Count, cell => TownPlan.BlockedRects(library.TownModule(cell, N)));
                foreach (string p in TownTrail.FindProblems(seed, N, trail, spots)) problems.Add($"seed {seed}: {p}");
                lengths.Add(trail.Steps.Count);
                favourSteps.Add(trail.FavourStep);
                Assert.AreEqual(trail.Signature(), TownTrail.For(seed, N).Signature(), "the same seed, the same trail");
                // The trail never takes the greeter, the square (its errand) or the Mayor.
                foreach (int index in trail.Steps.Append(trail.Helper))
                    Assert.GreaterOrEqual(index, TownTrail.FirstOffSquare(N));
                // Its lines name the right people.
                List<string> lines = TownTrail.Lines(trail, specs, spots).ToList();
                StringAssert.Contains(specs[trail.Steps[0]].Name, lines[0], "the Mayor names the first person");
                Assert.IsTrue(lines.Any(l => l.Contains(specs[trail.Helper].Name)), "the favour names the helper");
                Assert.IsTrue(SeedSweep.FindQuestProblems(new CubeModel(seed), library, out _).Count == 0,
                    $"seed {seed}: " + string.Join("; ", SeedSweep.FindQuestProblems(new CubeModel(seed), library, out _)));
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
            CollectionAssert.AreEquivalent(new[] { 2, 3 }, lengths, "trails of two and of three townsfolk");
            Assert.Greater(favourSteps.Count, 1, "the favour falls on different steps");
            Assert.AreNotEqual(TownTrail.For(1, N).Signature(), TownTrail.For(2, N).Signature(), "seeded");
            Assert.IsNull(TownTrail.For(1, 2), "no town, no trail");
            // The sweep flags a broken trail.
            List<NpcSpec> s1 = TownPlan.Specs(1, N);
            var none = new List<TownSpot>(new TownSpot[s1.Count]);
            Assert.IsNotEmpty(TownTrail.FindProblems(1, N, TownTrail.For(1, N), none), "people with no spot");
            Assert.IsNotEmpty(TownTrail.FindProblems(1, N, null, none), "no trail");
        }

        // ---------- The talk pause ----------

        [Test]
        public void TalkPause_StopsTimeLikeTheMap_AndTheClosingFrameIsTheConversations()
        {
            Time.timeScale = 1f;
            Assert.IsFalse(GamePause.Frozen);
            GamePause.BeginTalk(this);
            Assert.IsTrue(GamePause.Talking);
            Assert.IsFalse(GamePause.Active, "a conversation is not a menu: the dialogue box still reads presses");
            Assert.IsTrue(GamePause.InputBlocked, "the player waits");
            Assert.AreEqual(0f, Time.timeScale, "everything else holds");

            // The map opens over it and closes again: still paused for the talk.
            GamePause.Pause();
            Assert.IsTrue(GamePause.Active);
            GamePause.Resume();
            Assert.AreEqual(0f, Time.timeScale, "the talk still holds time");

            GamePause.EndTalk(this);
            Assert.IsFalse(GamePause.Talking);
            Assert.AreEqual(1f, Time.timeScale, "time comes back");
            Assert.IsTrue(GamePause.InputBlocked, "the press that closed the talk is the talk's");

            // The quest log holds its own pause, and the time scale it found comes back.
            Time.timeScale = 2f;
            GamePause.Hold(QuestLogView.PauseKey);
            Assert.IsTrue(GamePause.Active);
            Assert.AreEqual(0f, Time.timeScale);
            GamePause.Release(QuestLogView.PauseKey);
            Assert.AreEqual(2f, Time.timeScale);
            Time.timeScale = 1f;
        }
    }
}
