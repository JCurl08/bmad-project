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
    /// hand-checked templates hold every target; the dealt puzzles (booths, targets) prove their timing, order and room;
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

        [Test]
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

        [Test]
        public void TheOrderingTemples_OverSeeds1To500_KeepTheirRule_Spacing_AndTimer()
        {
            foreach (TemplePuzzleKind kind in new[] { TemplePuzzleKind.SeedOrder, TemplePuzzleKind.StoneOrder, TemplePuzzleKind.TownOrder })
                for (int seed = 1; seed <= 500; seed++)
                {
                    TempleOrderSpec spec = TempleOrderSpec.Deal(TemplePlan.RngFor(seed, FaceId.Top), kind);
                    CollectionAssert.IsEmpty(spec.Problems($"{kind} seed {seed}"));
                    Assert.AreEqual(TempleOrderSpec.CountFor(kind), spec.Count);
                    if (kind == TemplePuzzleKind.StoneOrder)
                        Assert.GreaterOrEqual(spec.TimerSeconds, TempleOrderSpec.TimerSlack * spec.TimedRouteSeconds, "T at least 1.5x the best route");
                    else Assert.AreEqual(0f, spec.TimerSeconds);
                }
            // The late column: six targets for the faces' own, a timer for the stones; Town's is short.
            Assert.GreaterOrEqual(TempleOrderSpec.SeedCount, 5);
            Assert.GreaterOrEqual(TempleOrderSpec.StoneCount, 5);
            Assert.Less(TempleOrderSpec.TownCount, TempleOrderSpec.SeedCount);
            // A broken rule is caught.
            TempleOrderSpec good = TempleOrderSpec.Deal(TemplePlan.RngFor(3, FaceId.Top), TemplePuzzleKind.SeedOrder);
            var marks = good.Marks.ToList();
            marks.Reverse();
            var bad = new TempleOrderSpec(good.Kind, good.Spots.ToList(), marks, good.Looks.ToList(), 0f);
            Assert.IsTrue(bad.Problems("bad").Any(p => p.Contains("rule")));
        }

        [Test]
        public void ThePhysicsTemple_EveryVariant_OpensWithThreeBoulders_AndShutsEarlyWithTwo()
        {
            for (int seed = 1; seed <= 200; seed++)
            {
                PhysicsTrialSpec spec = TempleBooths.Deal(TemplePlan.RngFor(seed, FaceId.Right));
                CollectionAssert.IsEmpty(TempleBooths.Problems($"seed {seed}", spec));
                Assert.AreEqual(TempleBooths.Required, spec.Boulders.Count, "exactly the shared three");
                foreach (TrialBoothSpec b in spec.Booths)
                {
                    DoorLayout d = b.Layout;
                    float baseSeconds = TempleBooths.BaseSeconds(d);
                    // 4.14 review: fair from anywhere on the switch, and one short still fails from the best start.
                    Assert.GreaterOrEqual(TimeField.OpenSeconds(baseSeconds, 3f), TempleBooths.LongDash(d), "three boulders: open for the longest dash");
                    Assert.LessOrEqual(TimeField.OpenSeconds(baseSeconds, 2f), 0.9f * TempleBooths.ShortDash(d), "two: shut at least 10% before the shortest");
                    Assert.Less(TimeField.OpenSeconds(baseSeconds, 3f), 1.21f * TempleBooths.ShortDash(d), "the late slack is tight");
                    Rect corridor = TempleBooths.Corridor(d);
                    Assert.LessOrEqual(corridor.yMin, TempleBooths.DashY - TempleBooths.SwitchTouch - TimeField.PlayerRadius + 1e-4f);
                    foreach (TrialBoothSpec other in spec.Booths)
                        Assert.IsFalse(corridor.Overlaps(other.Footprint), "every parallel dash clears the booths");
                    Rect band = TempleBooths.LatchBand(d);
                    Assert.LessOrEqual(band.yMin, TempleBooths.DashY - TempleBooths.SwitchTouch);
                    Assert.GreaterOrEqual(band.yMax, TempleBooths.DashY + TempleBooths.SwitchTouch, "the band meets every parallel dash");
                    Assert.AreEqual(TempleBooths.DashY, d.SwitchLocal.y, "the switch is on the dash line");
                }
                Assert.LessOrEqual(TempleBooths.DragTiles(spec), 30f, "the late tier's drag");
            }
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
