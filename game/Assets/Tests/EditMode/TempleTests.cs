using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Tracer;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The temples (4.14), pure: every face's temple holds a hard version of its own puzzle (late tier), solvable from its
    /// one doorway on seeds 1-50 and, where it is generated (islands, ice, wind), on seeds 1-500 with no template; the
    /// hand-checked templates hold every target; the dealt booths prove their timing and room; 4.19's sliding crest,
    /// crystals and finch song over seeds 1-500, with an independent check of the search (IDA*, every strike set);
    /// a hand-in unseals and only the gem repairs (and gives the benefit); the quest log follows; the town's temple, its
    /// art and its return point; the rooms off the net; and the words keep the text rule.
    /// </summary>
    public class TempleTests
    {
        private static readonly Theme[] AllThemes =
            { Theme.Town, Theme.Biology, Theme.Chemistry, Theme.Physics, Theme.Math, Theme.EarthAtmosphere };

        [Test]
        public void EveryTemple_OnSeeds1To50_IsSolvable_AndItsFacesKind_AtTheLateTier()
        {
            var kinds = new Dictionary<TemplePuzzleKind, int>();
            var samples = new Dictionary<TemplePuzzleKind, string>();
            for (int seed = 1; seed <= 50; seed++)
            {
                var model = new CubeModel(seed);
                List<FaceId> faces = TemplePlan.TempleFaces(model);
                Assert.AreEqual(6, faces.Count, $"seed {seed}: a temple on every face");
                CollectionAssert.IsEmpty(TemplePlan.FindProblems(model), $"seed {seed}");
                foreach (FaceId face in faces)
                {
                    TemplePuzzle t = TemplePlan.For(model, face);
                    Assert.AreEqual(model.ThemeOf(face), t.Theme);
                    CollectionAssert.Contains(TemplePlan.KindsFor(t.Theme), t.Kind);
                    Assert.IsFalse(t.FromTemplate, $"seed {seed} {t}: no template needed");
                    kinds[t.Kind] = kinds.TryGetValue(t.Kind, out int c) ? c + 1 : 1;
                    if (!samples.ContainsKey(t.Kind)) samples[t.Kind] = t.Stats();
                }
            }
            Assert.Greater(kinds[TemplePuzzleKind.Ice], 5, "Earth draws ice on some seeds");
            Assert.Greater(kinds[TemplePuzzleKind.Wind], 5, "and wind on others");
            foreach (KeyValuePair<TemplePuzzleKind, string> e in samples) Debug.Log($"Temple {e.Key}: {e.Value}");
        }

        [Test, Timeout(900000)] // 4.25: the harder wind tiers take longer to generate (about 4 min here in the editor)
        public void TheGeneratedTemples_OverSeeds1To500_NeverNeedATemplate_AndKeepEveryTarget()
        {
            var clock = Stopwatch.StartNew();
            int islandsSteps = 0;
            for (int seed = 1; seed <= 500; seed++)
            {
                IslandLayout islands = IslandLayout.Generate(TemplePlan.RngFor(seed, FaceId.Front), TemplePuzzle.Tier, TemplePlan.IslandDoor, false);
                Assert.IsNotNull(islands, $"seed {seed}: an island network");
                Assert.IsFalse(islands.FromTemplate, $"seed {seed}: islands from the search");
                CollectionAssert.IsEmpty(islands.Problems("islands", TemplePlan.IslandDoor), $"seed {seed} islands");
                Assert.IsTrue(islands.Solvable);
                islandsSteps += islands.SearchSteps;

                SeededRng iceRng = TemplePlan.RngFor(seed, FaceId.Back);
                iceRng.NextInt(2); // Earth's kind draw comes first on its stream
                IceLayout ice = IceLayout.Generate(iceRng, TemplePuzzle.Tier, TemplePlan.SouthOnly);
                Assert.IsFalse(ice.FromTemplate, $"seed {seed}: ice from the search");
                CollectionAssert.IsEmpty(ice.Problems("ice"), $"seed {seed} ice");

                SeededRng windRng = TemplePlan.RngFor(seed, FaceId.Back);
                windRng.NextInt(2);
                WindLayout wind = WindLayout.Generate(windRng, TemplePuzzle.Tier, TemplePlan.SouthOnly);
                Assert.IsFalse(wind.FromTemplate, $"seed {seed}: wind from the search");
                CollectionAssert.IsEmpty(wind.Problems("wind"), $"seed {seed} wind");
            }
            Debug.Log($"Temple generation, seeds 1-500 (islands, ice and wind each): {clock.ElapsedMilliseconds} ms, " +
                      $"island search steps {islandsSteps / 500} a screen");
        }

        [Test]
        public void TheTempleTemplates_KeepEveryTarget()
        {
            IslandLayout islands = TempleTemplates.Islands();
            CollectionAssert.IsEmpty(islands.Problems("template", TemplePlan.IslandDoor));
            Assert.IsTrue(islands.Solvable);
            Assert.AreEqual(PuzzleTier.Late, islands.Tier);
            CollectionAssert.IsEmpty(TempleTemplates.Ice().Problems("template"));
            CollectionAssert.IsEmpty(WindLayout.Template(TemplePuzzle.Tier, TemplePlan.SouthOnly).Problems("template"));
            CollectionAssert.IsEmpty(new TemplePuzzle(1, FaceId.Front, Theme.Math, TemplePuzzleKind.Islands, islands: islands, fromTemplate: true).Problems());
            CollectionAssert.IsEmpty(new TemplePuzzle(1, FaceId.Front, Theme.EarthAtmosphere, TemplePuzzleKind.Ice, ice: TempleTemplates.Ice(), fromTemplate: true).Problems());
        }

        // ---------------------------------------------------------------- 4.19: the sliding crest, the crystals, the finches

        [Test]
        public void TheSlidingCrest_OverSeeds1To500_IsAScrambleOfLegalMoves_OptimalIn14To18_AndItsSolutionSolvesIt()
        {
            // The search itself: every board reachable from the solved picture (half the orderings), the hardest 31 moves.
            Assert.AreEqual(0, TempleSliding.Distance(TempleSliding.Solved()));
            Assert.AreEqual(-1, TempleSliding.Distance(new[] { 2, 1, 3, 4, 5, 6, 7, 8, 0 }), "one swap away: never solvable");
            Assert.AreEqual(1, TempleSliding.Distance(new[] { 1, 2, 3, 4, 5, 6, 7, 0, 8 }));
            var lengths = new Dictionary<int, int>();
            int minWalk = int.MaxValue, maxWalk = 0;
            for (int seed = 1; seed <= 500; seed++)
            {
                TempleSliding t = TempleSliding.Deal(TemplePlan.RngFor(seed, FaceId.Front));
                Assert.IsFalse(t.FromTemplate, $"seed {seed}: from the walk");
                CollectionAssert.IsEmpty(t.Problems($"seed {seed}"));
                int d = t.OptimalMoves;
                Assert.That(d, Is.InRange(TempleSliding.MinMoves, TempleSliding.MaxMoves));
                Assert.GreaterOrEqual(t.WalkMoves, d, "made by at least as many legal moves");
                lengths[d] = lengths.TryGetValue(d, out int c) ? c + 1 : 1;
                minWalk = Mathf.Min(minWalk, t.WalkMoves);
                maxWalk = Mathf.Max(maxWalk, t.WalkMoves);
                // An independent check of the optimal length on the first seeds: iterative deepening (IDA*, Manhattan).
                if (seed <= 12) Assert.AreEqual(d, IdaStar(t.Tiles.ToArray()), $"seed {seed}: IDA* agrees");
            }
            Assert.AreEqual(16, new TempleSliding(TempleSliding.TemplateTiles, 0, true).OptimalMoves, "the template is 16 moves");
            CollectionAssert.IsEmpty(new TempleSliding(TempleSliding.TemplateTiles, 0, true).Problems("template"));
            Debug.Log($"Sliding crest, seeds 1-500: optimal lengths {string.Join(", ", lengths.OrderBy(e => e.Key).Select(e => $"{e.Key}: {e.Value}"))}; " +
                      $"walks {minWalk}-{maxWalk}; seed 1: {TempleSliding.Deal(TemplePlan.RngFor(1, FaceId.Front)).Stats()}");
        }

        /// <summary>IDA* with the Manhattan distance (admissible): the optimal number of slides, found without the table.</summary>
        private static int IdaStar(int[] start)
        {
            int bound = Manhattan(start);
            while (bound <= 31)
            {
                int next = Search(start, 0, bound, -1);
                if (next < 0) return -next - 1;
                bound = next;
            }
            return -1;
        }

        private static int Search(int[] t, int g, int bound, int previousGap)
        {
            int f = g + Manhattan(t);
            if (f > bound) return f;
            if (TempleSliding.IsSolved(t)) return -(g + 1);
            int min = int.MaxValue;
            int gap = TempleSliding.GapOf(t);
            foreach (int cell in TempleSliding.Neighbours(gap))
            {
                if (cell == previousGap) continue;
                int r = Search(TempleSliding.Slide(t, cell), g + 1, bound, gap);
                if (r < 0) return r;
                min = Mathf.Min(min, r);
            }
            return min;
        }

        private static int Manhattan(int[] t)
        {
            int m = 0;
            for (int i = 0; i < t.Length; i++)
            {
                if (t[i] == 0) continue;
                int home = t[i] - 1;
                m += Mathf.Abs(home / 3 - i / 3) + Mathf.Abs(home % 3 - i % 3);
            }
            return m;
        }

        [Test]
        public void TheCrystals_OverSeeds1To500_AreLitIn6To7Strikes_ByTheSearch_AndByEveryStrikeSet()
        {
            (int boards, int hardest) = TempleLights.Reach();
            Assert.AreEqual(4096, boards, "the 5x3 bank: 4,096 boards can be lit");
            Assert.AreEqual(8, hardest, "the hardest needs 8 strikes");
            Assert.AreEqual(0, TempleLights.LeastStrikes(TempleLights.AllLit));
            // A strike flips the crystal and those beside it (a corner: three; the middle: five).
            Assert.AreEqual(3, TempleLights.LitCount(TempleLights.Flips(0)));
            Assert.AreEqual(5, TempleLights.LitCount(TempleLights.Flips(7)));
            var least = new Dictionary<int, int>();
            for (int seed = 1; seed <= 500; seed++)
            {
                TempleLights l = TempleLights.Deal(TemplePlan.RngFor(seed, FaceId.Left));
                Assert.IsFalse(l.FromTemplate, $"seed {seed}: dealt");
                CollectionAssert.IsEmpty(l.Problems($"seed {seed}"));
                int d = l.LeastStrikesToSolve;
                Assert.That(d, Is.InRange(TempleLights.MinStrikes, TempleLights.MaxStrikes));
                least[d] = least.TryGetValue(d, out int c) ? c + 1 : 1;
                // An independent check on the first seeds: every set of strikes (2^15, order does not matter, a crystal
                // struck twice is not struck), the fewest that light it all.
                if (seed <= 12) Assert.AreEqual(d, LeastByEveryStrikeSet(l.Start), $"seed {seed}: the strike sets agree");
            }
            CollectionAssert.IsEmpty(new TempleLights(Template(), TempleLights.TemplateStrikes, true).Problems("template"));
            Debug.Log($"Crystals, seeds 1-500: least strikes {string.Join(", ", least.OrderBy(e => e.Key).Select(e => $"{e.Key}: {e.Value}"))}; " +
                      $"seed 1: {TempleLights.Deal(TemplePlan.RngFor(1, FaceId.Left)).Stats()}");
        }

        private static int Template()
        {
            int b = TempleLights.AllLit;
            foreach (int i in TempleLights.TemplateStrikes) b = TempleLights.Strike(b, i);
            return b;
        }

        private static int LeastByEveryStrikeSet(int board)
        {
            int best = -1;
            for (int set = 0; set < 1 << TempleLights.Count; set++)
            {
                int b = board, n = 0;
                for (int i = 0; i < TempleLights.Count; i++)
                    if ((set & (1 << i)) != 0)
                    {
                        b = TempleLights.Strike(b, i);
                        n++;
                    }
                if (b == TempleLights.AllLit && (best < 0 || n < best)) best = n;
            }
            return best;
        }

        [Test]
        public void TheFinchSong_OverSeeds1To500_IsSixNotes_NoFinchTwiceInARow_EverySingerUsed_AndEachFinchStruckAlone()
        {
            var firsts = new int[TempleSong.Finches];
            for (int seed = 1; seed <= 500; seed++)
            {
                TempleSong song = TempleSong.Deal(TemplePlan.RngFor(seed, FaceId.Bottom));
                Assert.IsFalse(song.FromTemplate, $"seed {seed}: dealt");
                CollectionAssert.IsEmpty(song.Problems($"seed {seed}"));
                Assert.AreEqual(TempleSong.Length, song.Notes.Count);
                firsts[song.Notes[0]]++;
            }
            foreach (int n in firsts) Assert.Greater(n, 60, "every finch starts some songs");
            // The rounds: 3, 4, 5, 6 notes (18 strikes), a replay well inside the late tier's cheap retry.
            Assert.AreEqual(4, TempleSong.Rounds);
            Assert.AreEqual(18, TempleSong.TotalStrikes);
            Assert.LessOrEqual(TempleSong.SongSeconds(TempleSong.Length), 12f);
            CollectionAssert.IsEmpty(new TempleSong(TempleSong.TemplateNotes, true).Problems("template"));
            Assert.IsTrue(new TempleSong(new[] { 0, 0, 1, 2, 3, 1 }).Problems("bad").Any(p => p.Contains("same finch")));
            Assert.IsTrue(new TempleSong(new[] { 0, 1, 0, 1, 0, 1 }).Problems("bad").Any(p => p.Contains("never sings")));
            Debug.Log($"Finch song: {TempleSong.Deal(TemplePlan.RngFor(1, FaceId.Bottom)).Stats()}");
        }

        [Test]
        public void TheNewTempleArt_ReadsApart()
        {
            // Nine different crest pieces, so every tile is its own.
            var pieces = new HashSet<string>();
            TownArt.Canvas crest = TempleArt.CrestCanvas();
            for (int cell = 0; cell < 9; cell++)
            {
                int col = cell % 3, row = 2 - cell / 3;
                var px = new List<string>();
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                    {
                        Color32 c = crest.Pixels[(row * 16 + y) * crest.Width + col * 16 + x];
                        px.Add($"{c.r},{c.g},{c.b}");
                    }
                Assert.IsTrue(pieces.Add(string.Join(";", px)), $"crest piece {cell} is its own");
                Sprite piece = TempleArt.CrestPiece(cell + 1);
                Assert.AreEqual(FilterMode.Point, piece.texture.filterMode);
                Assert.AreEqual(2f, piece.rect.width / piece.pixelsPerUnit, 1e-4f, "a piece is 2 units across");
            }
            // The crystals: where lit and dark differ, lit reads much brighter.
            TownArt.Canvas lit = TempleArt.CrystalCanvas(true), dark = TempleArt.CrystalCanvas(false);
            float litSum = 0f, darkSum = 0f;
            int differ = 0;
            for (int i = 0; i < lit.Pixels.Length; i++)
            {
                if (lit.Pixels[i].Equals(dark.Pixels[i])) continue;
                litSum += Luminance(lit.Pixels[i]);
                darkSum += Luminance(dark.Pixels[i]);
                differ++;
            }
            Assert.Greater(differ, 40, "the gem itself changes");
            Assert.Greater(litSum / differ, darkSum / differ + 0.25f, "lit vs dark");
            // The finches: four looks, each singing look apart from its still one.
            var finches = new HashSet<string>();
            for (int f = 0; f < TempleSong.Finches; f++)
            {
                Assert.IsTrue(finches.Add(Pixels(TempleArt.FinchCanvas(f, false))), $"finch {f} looks its own");
                Assert.AreNotEqual(Pixels(TempleArt.FinchCanvas(f, false)), Pixels(TempleArt.FinchCanvas(f, true)), $"finch {f} sings visibly");
                Assert.AreEqual(TownLayout.PixelsPerUnit, TempleArt.Finch(f, true).pixelsPerUnit);
                Assert.IsNotNull(TempleArt.Note(f));
            }
        }

        private static string Pixels(TownArt.Canvas c) => string.Join(";", c.Pixels.Select(p => $"{p.r},{p.g},{p.b},{p.a}"));

        private static float Luminance(Color32 p) => (0.2126f * p.r + 0.7152f * p.g + 0.0722f * p.b) / 255f;

        [Test]
        public void ThePhysicsTemple_EveryVariant_StaysOpenAQuarterLongerThanItsDash_WithThreeBoulders_AndShutsEarlyWithTwo()
        {
            // 4.25: the doors time out (no latch), so a booth counts only when the player runs into it in time; the timer is
            // at least 1.25 times the dash with three boulders, and with two it shuts at least 10% before the dash ends.
            var dashes = new List<float>();
            for (int seed = 1; seed <= 200; seed++)
            {
                PhysicsTrialSpec spec = TempleBooths.Deal(TemplePlan.RngFor(seed, FaceId.Right));
                CollectionAssert.IsEmpty(TempleBooths.Problems($"seed {seed}", spec));
                Assert.AreEqual(TempleBooths.Required, spec.Boulders.Count, "exactly the shared three");
                foreach (TrialBoothSpec b in spec.Booths)
                {
                    DoorLayout d = b.Layout;
                    float dash = TempleBooths.DashSeconds(d);
                    dashes.Add(dash);
                    Assert.GreaterOrEqual(TempleBooths.OpenSeconds(d, 3f), 1.25f * dash - 1e-4f, "three boulders: open 1.25 times the dash");
                    Assert.LessOrEqual(TempleBooths.OpenSeconds(d, 2f), 0.9f * dash, "two: shut at least 10% before the dash ends");
                    Assert.Greater(TempleBooths.OpenSeconds(d, 3f), 1.8f, "a longer timer than before 4.25 (about 1.5 s)");
                    Assert.AreEqual(0f, d.Opening.y, 1e-4f, "the door faces across the room");
                    Rect corridor = TempleBooths.Corridor(d);
                    Assert.IsTrue(corridor.Contains(d.SwitchLocal), "the switch is on the dash's lane");
                    Assert.IsTrue(TempleBooths.Stall(d).Contains(d.SwitchLocal), "in its stall");
                    Assert.AreEqual(TempleBooths.StallInner, TempleBooths.Strip(d).height, 1e-4f, "the strip spans the stall");
                    foreach (TrialBoothSpec other in spec.Booths)
                        if (other != b) Assert.IsFalse(corridor.Overlaps(other.Footprint), "the dash clears the other booth");
                }
                Assert.LessOrEqual(TempleBooths.DragTiles(spec), 30f, "the late tier's drag");
            }
            TestContext.WriteLine($"dashes {dashes.Min():0.00}-{dashes.Max():0.00} s, open with three {1.25f * dashes.Min():0.00}-{1.25f * dashes.Max():0.00} s");
        }

        [Test]
        public void AHandIn_UnsealsTheTemple_OnlyTheGemRepairs_AndGivesTheBenefit()
        {
            var model = new CubeModel(7);
            FaceId math = model.FaceOf(Theme.Math);
            var repairs = new FaceRepairs();
            var unsealed = new List<FaceId>();
            repairs.TempleUnsealed += f => unsealed.Add(f);
            Assert.IsTrue(repairs.Unseal(math));
            Assert.IsFalse(repairs.Unseal(math), "once");
            CollectionAssert.AreEqual(new[] { math }, unsealed);
            Assert.IsTrue(repairs.IsUnsealed(math));
            Assert.IsFalse(repairs.IsRepaired(math));
            Assert.IsFalse(repairs.HasGem(math));
            Assert.AreEqual(BossBenefit.None, repairs.Benefits(model), "an open temple gives no help yet");
            Assert.AreEqual(ArenaTuning.Base().GapHeight, ArenaTuning.For(repairs.Benefits(model)).GapHeight);
            Assert.IsTrue(repairs.Repair(math), "the gem");
            Assert.IsTrue(repairs.HasGem(math));
            Assert.AreEqual(BossBenefit.WiderGap, repairs.Benefits(model));
            Assert.AreEqual(ArenaTuning.WideGapHeight, ArenaTuning.For(repairs.Benefits(model)).GapHeight, "the gem widens the gaps");
        }

        [Test]
        public void TheQuestLog_ClosesTheLeadersQuest_OnTheHandIn_AndTheTemplesOnTheGem()
        {
            var model = new CubeModel(7);
            FaceId face = model.FaceOf(Theme.Biology);
            var log = new QuestLog();
            var repairs = new FaceRepairs();
            log.Add(QuestLog.LeaderId(face), QuestKind.Leader, face, "Queen Wrennifer", QuestText.LeaderTitle(Theme.Biology),
                QuestText.LeaderStep(Theme.Biology, "Queen Wrennifer", 3, 0));
            repairs.Unseal(face);
            log.Add(QuestLog.TempleId(face), QuestKind.Temple, face, "Queen Wrennifer", QuestText.TempleTitle(Theme.Biology), QuestText.TempleStep(Theme.Biology));
            log.RefreshLeaders(model, DieNumbers.Roll(model), repairs, null);
            Assert.IsTrue(log.IsDone(QuestLog.LeaderId(face)), "the hand-in ends the leader's quest");
            Assert.IsFalse(log.IsDone(QuestLog.TempleId(face)), "the temple waits");
            repairs.Repair(face);
            log.RefreshLeaders(model, DieNumbers.Roll(model), repairs, null);
            Assert.IsTrue(log.IsDone(QuestLog.TempleId(face)), "the gem ends the temple's");
            Assert.AreEqual(QuestText.TempleDone(Theme.Biology), log.Get(QuestLog.TempleId(face)).Step);
        }

        [Test]
        public void EveryTown_HasOneTemple_InItsRacesStyle_WithAClearReturnPoint()
        {
            float r = TimeField.PlayerRadius;
            var looks = new HashSet<string>();
            foreach (Theme theme in AllThemes)
            {
                TownLayout layout = TownLayout.For(theme);
                CollectionAssert.IsEmpty(layout.Problems(), theme.ToString());
                Assert.AreEqual(1, layout.Buildings.Count(b => b.Kind == TownBuildingKind.Temple), $"{theme}: one temple");
                TownBuilding temple = layout.Temple;
                Assert.IsFalse(temple.Enterable, "the temple is not the marked building");
                Vector2 p = TempleRooms.ReturnPointLocal(temple);
                var circle = new Rect(p - Vector2.one * r, Vector2.one * 2f * r);
                foreach (Rect solid in layout.Solids)
                    Assert.IsFalse(solid.Overlaps(circle), $"{theme}: the return point meets {solid}");
                var reaches = layout.Stands.Select(feet => TownPlan.QuadrantBounds(feet)).ToList();
                reaches.Add(new Rect(layout.LeaderStand - TownSquares.LeaderRoom, TownSquares.LeaderRoom * 2f));
                foreach (Rect box in reaches)
                {
                    Rect reach = Rect.MinMaxRect(box.xMin - NpcFactory.BodyWidth / 2f, box.yMin, box.xMax + NpcFactory.BodyWidth / 2f,
                        box.yMax + NpcFactory.MaxBodyHeight);
                    Assert.IsFalse(reach.Overlaps(circle), $"{theme}: a resident can wander onto the temple's return point ({box})");
                }
                Assert.Greater(temple.Door.y - p.y, r + TownInteriors.DoorReach, $"{theme}: away from the door's reach");
                foreach (TempleLook look in new[] { TempleLook.Sealed, TempleLook.Open, TempleLook.Mended })
                    looks.Add(string.Join(",", TempleArt.Canvas(theme, look).Pixels.Select(px => $"{px.r}{px.g}{px.b}{px.a}")));
            }
            Assert.AreEqual(18, looks.Count, "every race's temple, sealed, open and mended, looks its own");
        }

        [Test]
        public void TheTempleArt_KeepsThePixelStyle()
        {
            Color32 outline = TownArt.OutlineColor;
            foreach (TownArt.Canvas c in TempleArt.AllCanvases())
            {
                Assert.AreEqual(TownArt.BuildingPixels + 2 * TownArt.Margin, c.Width);
                Assert.Greater(c.Pixels.Count(p => p.a > 0 && p.r == outline.r && p.g == outline.g && p.b == outline.b), 40, "outlined");
            }
            foreach (Theme theme in AllThemes)
            {
                foreach (Sprite s in new[] { TempleArt.Building(theme, TempleLook.Sealed), TempleArt.Gem(theme), TempleArt.Shard(theme), TempleArt.Altar(theme) })
                {
                    Assert.AreEqual(FilterMode.Point, s.texture.filterMode, s.name);
                    Assert.AreEqual(TownLayout.PixelsPerUnit, s.pixelsPerUnit, s.name);
                }
                Assert.AreSame(TempleArt.Gem(theme), TempleArt.Gem(theme), "cached");
            }
        }

        [Test]
        public void TheTempleRooms_SitOffTheNet_ClearOfTheArena_TheTownRooms_AndTheLair()
        {
            var taken = new HashSet<Vector2Int> { new Vector2Int(0, -3) };
            for (int f = 0; f < CubeSettings.FaceCount; f++)
            {
                taken.Add(TownInteriors.RoomScreen((FaceId)f));
                taken.Add(LairRooms.RoomScreen((FaceId)f));
            }
            var mine = new HashSet<Vector2Int>();
            for (int f = 0; f < CubeSettings.FaceCount; f++)
            {
                Vector2Int screen = TempleRoom.RoomScreen((FaceId)f);
                Assert.Less(screen.y, 0, "below the faces");
                Assert.IsFalse(taken.Contains(screen), $"{(FaceId)f}: a screen of its own");
                Assert.IsTrue(mine.Add(screen));
            }
            // The way in and out: the arrival is inside the floor, south, and walking south leaves.
            Assert.IsTrue(TempleRoom.Floor.Contains(TempleRoom.EntryPoint));
            Assert.Less(TempleRoom.ExitLine, TempleRoom.EntryPoint.y - TimeField.PlayerRadius);
            List<Rect> walls = TempleRoom.Walls;
            Assert.AreEqual(5, walls.Count, "three whole walls and the south one either side of its doorway");
        }

        [Test]
        public void TheTempleWords_KeepTheTextRule_AndFitTheirLines()
        {
            var texts = TempleText.All().Concat(QuestText.AllTexts()).Concat(RewardText.All()).ToList();
            foreach (Theme theme in AllThemes)
            {
                texts.Add(LairText.BenefitFact(theme));
                texts.Add(RewardText.UnsealPopup(theme));
            }
            texts.AddRange(MassMitt.AllHudText);
            foreach (string text in texts)
                CollectionAssert.IsEmpty(InWorldText.Problems(text), text);
            foreach (string text in TempleText.All())
                foreach (string line in text.Split('\n'))
                    Assert.LessOrEqual(line.Length, TempleText.MaxLineLength, line);
            foreach (Theme theme in AllThemes)
                StringAssert.Contains("gem", LairText.BenefitFact(theme), "the lair's hints tell of the temple's gem");
        }
    }
}
