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
    /// give the same quest; the Mayor's tour of Town (4.20), walkable on seeds 1-50 on its own stream; and the talk pause
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

            QuestEntry town = log.Add(QuestLog.TownId, QuestKind.TownSide, CubeModel.StartFace, "Mayor Muddleton", QuestText.TourTitle,
                QuestText.TourStep(TourLesson.Dig, "on Town's north-east screen", false));
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

        // ---------- The Mayor's tour of Town (4.20) ----------

        [Test]
        public void TownTour_IsWalkable_OnSeeds1To50_EightLessonsInOrder_OneDoorwayApart_OnItsOwnStream()
        {
            ModuleLibrary library = Library();
            var starts = new HashSet<int>();
            var directions = new HashSet<int>();
            var problems = new List<string>();
            System.Func<Vector2Int, IReadOnlyList<Rect>> blockedFor = cell => TownPlan.BlockedRects(library.TownModule(cell, N));
            for (int seed = 1; seed <= 50; seed++)
            {
                TownTour tour = TownTour.For(seed, N);
                Assert.IsNotNull(tour, $"seed {seed}: a tour");
                List<NpcSpec> specs = TownPlan.Specs(seed, N);
                List<TownSpot> spots = TownPlan.Spots(seed, N, specs.Count, blockedFor);
                var model = new CubeModel(seed);
                foreach (string p in TownTour.FindProblems(seed, N, tour, spots, blockedFor, SeedSweep.FindReachable(model)))
                    problems.Add($"seed {seed}: {p}");
                foreach (string p in SeedSweep.FindQuestProblems(model, library, out TownTour swept)) problems.Add($"seed {seed} (sweep): {p}");
                starts.Add(tour.Start);
                directions.Add(tour.Direction);
                Assert.AreEqual(tour.Signature(), TownTour.For(seed, N).Signature(), "the same seed, the same tour");
                CollectionAssert.AreEqual(TownTour.Lessons, tour.Stops.Select(s => s.Lesson), "the lessons in their order");
                Assert.AreEqual(8, tour.Stops.Select(s => s.Screen).Distinct().Count(), "a screen a stop: all eight round the square");
                // The guide stands on the Talk screen, clear of every planned Town person there.
                TownSpot guide = TownTour.GuideSpot(tour, spots, blockedFor(tour.StopOf(TourLesson.Talk).Screen.Cell));
                Assert.IsTrue(guide.Found, $"seed {seed}: the guide has a spot");
                Assert.AreEqual(tour.StopOf(TourLesson.Talk).Screen, guide.Screen);
                foreach (TownSpot s in spots.Where(s => s.Found && s.Screen == guide.Screen))
                    Assert.IsFalse(TownPlan.Footprint(s.Local, TownPlan.Spacing / 2f).Overlaps(TownPlan.Footprint(guide.Local, TownPlan.Spacing / 2f)),
                        $"seed {seed}: the guide keeps clear of {s}");
                // The last stop stands by an arch on the face's rim, in the lane leading to it.
                TourStop locks = tour.StopOf(TourLesson.Locks);
                Vector2Int beyond = locks.Screen.Cell + locks.ArchSide.ToVector();
                Assert.IsTrue(beyond.x < 0 || beyond.y < 0 || beyond.x >= N || beyond.y >= N, $"seed {seed}: the arch is on the rim");
                Assert.Greater(Vector2.Dot(locks.Marker, (Vector2)locks.ArchSide.ToVector()), 0f, "the marker stands towards its arch");
            }
            Assert.IsEmpty(problems, string.Join("\n", problems.Take(30)));
            CollectionAssert.AreEquivalent(new[] { 0, 2, 4, 6 }, starts, "the tour starts beside the square on every side over the seeds");
            CollectionAssert.AreEquivalent(new[] { 1, -1 }, directions, "and goes round both ways");
            Assert.IsNull(TownTour.For(1, 2), "no town, no tour");
            Assert.IsNull(TownTour.For(1, 5), "only a 3x3 Town's ring is all rim");
            // The sweep flags a broken tour.
            Assert.IsNotEmpty(TownTour.FindProblems(1, N, null, null, blockedFor), "no tour");
            List<TownSpot> crowd = Enumerable.Range(0, 400).Select(i => new TownSpot(TownTour.For(1, N).StopOf(TourLesson.Talk).Screen,
                new Vector2(-12f + (i % 20) * 1.25f, -7.5f + (i / 20) * 0.8f))).ToList();
            Assert.IsTrue(TownTour.FindProblems(1, N, TownTour.For(1, N), crowd, blockedFor).Any(p => p.Contains("guide")), "no room for the guide");
            Assert.IsTrue(TownTour.FindProblems(1, N, TownTour.For(1, N), TownPlan.Spots(1, N, TownPlan.Specs(1, N).Count, blockedFor), blockedFor,
                new HashSet<ScreenAddress>()).Any(p => p.Contains("unreachable")), "an unreachable stop");
        }

        [Test]
        public void TownTourProblems_CatchWrongOrder_FarStops_MarkersOffALaneOrOnASolid_AndALocksStopOffTheRim()
        {
            // 4.20 review: every check in TownTour.FindProblems can fail.
            ModuleLibrary library = Library();
            System.Func<Vector2Int, IReadOnlyList<Rect>> blockedFor = cell => TownPlan.BlockedRects(library.TownModule(cell, N));
            const int seed = 3;
            TownTour good = TownTour.For(seed, N);
            List<TownSpot> spots = TownPlan.Spots(seed, N, TownPlan.Specs(seed, N).Count, blockedFor);
            List<string> Problems(IEnumerable<TourStop> stops, System.Func<Vector2Int, IReadOnlyList<Rect>> blocked = null) =>
                TownTour.FindProblems(seed, N, TownTour.Custom(seed, N, good.Start, good.Direction, stops), spots, blocked ?? blockedFor);
            Assert.IsEmpty(Problems(good.Stops), "the run's own tour is sound");
            List<TourStop> Edit(int k, System.Func<TourStop, TourStop> change)
            {
                List<TourStop> list = good.Stops.ToList();
                list[k] = change(list[k]);
                return list;
            }

            // Wrong order: two lessons swapped.
            List<TourStop> swapped = good.Stops.ToList();
            (swapped[2], swapped[3]) = (new TourStop(swapped[3].Lesson, swapped[2].Screen, swapped[2].Marker, swapped[2].Prop, swapped[2].ArchSide),
                new TourStop(swapped[2].Lesson, swapped[3].Screen, swapped[3].Marker, swapped[3].Prop, swapped[3].ArchSide));
            Assert.IsTrue(Problems(swapped).Any(p => p.Contains("teaches")), "lessons out of order");

            // A stop more than one doorway from the last: stops 3 and 5 trade screens.
            List<TourStop> far = good.Stops.ToList();
            TourStop a = far[3], b = far[5];
            far[3] = new TourStop(a.Lesson, b.Screen, a.Marker, a.Prop, a.ArchSide);
            far[5] = new TourStop(b.Lesson, a.Screen, b.Marker, b.Prop, b.ArchSide);
            Assert.IsTrue(Problems(far).Any(p => p.Contains("not one doorway on")), "a stop two doorways off");

            // A marker off the lanes.
            Assert.IsTrue(Problems(Edit(0, s0 => new TourStop(s0.Lesson, s0.Screen, new Vector2(6f, 4f), s0.Prop, s0.ArchSide)))
                .Any(p => p.Contains("off the lanes")), "a marker off the lanes");

            // A marker on a solid (a module that put something in the lane).
            Vector2Int moveCell = good.Stops[0].Screen.Cell;
            System.Func<Vector2Int, IReadOnlyList<Rect>> walled = cell => cell == moveCell
                ? new List<Rect>(blockedFor(cell)) { new Rect(-1f, -1f, 2f, 2f) }
                : blockedFor(cell);
            Assert.IsTrue(Problems(good.Stops, walled).Any(p => p.Contains("meets a solid")), "a marker on a solid");

            // The Locks stop with its arch facing in, not on the face's rim.
            int last = good.Stops.Count - 1;
            Assert.IsTrue(Problems(Edit(last, l =>
            {
                Vector2Int inward = CubeModel.StartCell(N) - l.Screen.Cell;
                Facing side = inward.y > 0 ? Facing.North : inward.y < 0 ? Facing.South : inward.x > 0 ? Facing.East : Facing.West;
                return new TourStop(l.Lesson, l.Screen, TownTour.ArchMarker(side), TownTour.ArchMarker(side), side);
            })).Any(p => p.Contains("not the face's rim")), "a Locks stop whose arch is not on the rim");

            // Too few stops, and a stop on the square.
            Assert.IsTrue(Problems(good.Stops.Take(6)).Any(p => p.Contains("6 stops")));
            var square = new ScreenAddress(CubeModel.StartFace, CubeModel.StartCell(N));
            Assert.IsTrue(Problems(Edit(1, s1 => new TourStop(s1.Lesson, square, s1.Marker, s1.Prop, s1.ArchSide)))
                .Any(p => p.Contains("on the square")));
        }

        [Test]
        public void TownTour_Words_KeepTheTextRule_FitTheBox_AndNameTheRightKeys()
        {
            foreach (string line in TownTourText.AllLines().Concat(QuestLines.MayorLines()))
            {
                Assert.LessOrEqual(line.Length, Dialogue.MaxLineLength, line);
                CollectionAssert.IsEmpty(InWorldText.Problems(line), line);
            }
            StringAssert.Contains("Interact (E)", TownTourText.StopLine(TourLesson.Talk));
            StringAssert.Contains("Swing at it twice", TownTourText.StopLine(TourLesson.Dig));
            StringAssert.Contains("Press M", TownTourText.StopLine(TourLesson.Map));
            StringAssert.Contains("Press Q", TownTourText.StopLine(TourLesson.QuestLog));
            StringAssert.Contains("Press 1 or 2", TownTourText.StopLine(TourLesson.Items));
            StringAssert.Contains("keyhole", TownTourText.StopLine(TourLesson.Locks));
            StringAssert.Contains("Town's fragment", TownTourText.StopLine(TourLesson.Locks), "the last stop gives the fragment");
            foreach (TourLesson lesson in TownTour.Lessons)
            {
                string before = QuestText.TourStep(lesson, QuestLines.SampleWhere, false), after = QuestText.TourStep(lesson, QuestLines.SampleWhere, true);
                CollectionAssert.IsEmpty(InWorldText.Problems(before), before);
                CollectionAssert.IsEmpty(InWorldText.Problems(after), after);
                StringAssert.Contains($"stop {(int)lesson + 1}", before);
            }
            CollectionAssert.IsEmpty(InWorldText.Problems(TownTourText.Speaker));
            CollectionAssert.IsEmpty(InWorldText.Problems(DialogueBox.ChoicePrompt));
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
