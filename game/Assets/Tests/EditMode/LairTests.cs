using System.Collections.Generic;
using System.Linq;
using Game.Tracer;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The boss lair (4.13), pure: on seeds 1-50 exactly one core portal (the real one, on a reachable screen) leads to the
    /// arena and every other into a lair room whose prisoners give a fight hint, benefit hints and the real portal's region,
    /// in their own voices and under the text rule; the plan is seeded on its own stream and varies by seed; the rooms
    /// together tell every face's benefit; the lair room's cells keep every prisoner talkable from the walkway; the rooms
    /// sit on their own screens off the net; and the art keeps the pixel style.
    /// </summary>
    public class LairTests
    {
        private const string LibraryPath = "Assets/Modules/ModuleLibrary.asset";
        private const int Seeds = 50;

        private static ModuleLibrary Library()
        {
            var library = AssetDatabase.LoadAssetAtPath<ModuleLibrary>(LibraryPath);
            Assert.IsNotNull(library, $"{LibraryPath} missing");
            return library;
        }

        [Test]
        public void Seeds1To50_ExactlyOnePortalLeadsToTheArena_EveryOtherIntoALairRoomWithHintGivers()
        {
            ModuleLibrary library = Library();
            var realThemes = new HashSet<Theme>();
            var realFaces = new HashSet<FaceId>();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var model = new CubeModel(seed);
                CubeLayout layout = CubeLayout.Generate(model, library);
                LairPlan plan = LairPlan.ForRun(model);
                Assert.IsNotNull(plan, $"seed {seed}");
                List<string> problems = LairPlan.FindProblems(model, layout, plan, SeedSweep.FindReachable(model));
                CollectionAssert.IsEmpty(problems, $"seed {seed}: {string.Join("; ", problems)}");

                Assert.AreEqual(layout.Faces.Count, plan.PortalFaces.Count, $"seed {seed}: a portal per laid-out face");
                Assert.AreEqual(1, plan.PortalFaces.Count(plan.IsReal), $"seed {seed}: exactly one portal to the arena");
                Assert.AreEqual(plan.PortalFaces.Count - 1, plan.PortalFaces.Count(plan.IsLair), $"seed {seed}: every other into the lair");
                Assert.AreNotEqual(CubeModel.StartFace, plan.RealFace, $"seed {seed}: Town has no portal");
                foreach (FaceId face in plan.LairFaces)
                {
                    IReadOnlyList<PrisonerPlan> room = plan.PrisonersOf(face);
                    Assert.AreEqual(LairPlan.PrisonersPerRoom, room.Count, $"seed {seed} {face}");
                    Assert.AreEqual(1, room.Count(p => p.Hint == PrisonerHint.RealPortal), $"seed {seed} {face}: one names the real portal");
                    Assert.AreEqual(1, room.Count(p => p.Hint == PrisonerHint.Fight), $"seed {seed} {face}: one tells of the fight");
                    Assert.AreEqual(2, room.Count(p => p.Hint == PrisonerHint.Benefit), $"seed {seed} {face}: two tell of benefits");
                    Assert.AreEqual(room.Count, room.Select(p => p.Race).Distinct().Count(), $"seed {seed} {face}: four races");
                }
                Assert.IsEmpty(plan.PrisonersOf(plan.RealFace), "No lair under the real portal");
                realThemes.Add(model.ThemeOf(plan.RealFace));
                realFaces.Add(plan.RealFace);
            }
            Assert.AreEqual(5, realThemes.Count, "Every science region holds the real portal on some seed");
            Assert.GreaterOrEqual(realFaces.Count, 4, "and it sits on different faces");
        }

        [Test]
        public void ThePlan_IsSeeded_OnItsOwnStream()
        {
            Assert.AreEqual(RngStreams.Lair, LairPlan.RngStream);
            var signatures = new HashSet<string>();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var model = new CubeModel(seed);
                string a = LairPlan.ForRun(model).Signature(), b = LairPlan.ForRun(new CubeModel(seed)).Signature();
                Assert.AreEqual(a, b, $"seed {seed}: the same seed gives the same lair");
                signatures.Add(a);
            }
            Assert.Greater(signatures.Count, 45, "Seeds give different lairs");
            Assert.IsNull(LairPlan.ForRun(null));
        }

        [Test]
        public void Prisoners_SpeakInTheirVoices_UnderTheTextRule_AndTellTrueFacts()
        {
            ModuleLibrary library = Library();
            var voices = new HashSet<Voice>();
            var topics = new HashSet<int>();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var model = new CubeModel(seed);
                LairPlan plan = LairPlan.ForRun(model);
                var told = new HashSet<Theme>();
                var roomTopics = new List<int>();
                foreach (FaceId face in plan.LairFaces)
                foreach (PrisonerPlan prisoner in plan.PrisonersOf(face))
                {
                    NpcSpec spec = plan.SpecOf(prisoner);
                    Assert.AreEqual(prisoner.Race, spec.Race);
                    StringAssert.EndsWith(LairText.CaptiveKind, spec.Kind, "Labelled a captive");
                    StringAssert.DoesNotMatch(@" [IVX]+$", spec.Name, "A prisoner's name comes straight from their pool, with no numeral");
                    voices.Add(spec.Voice);
                    List<string> lines = plan.LinesOf(prisoner, model);
                    Assert.AreEqual(Dialogue.MaxLines, lines.Count);
                    List<string> spoken = Dialogue.SpokenLines(spec.Voice).ToList();
                    foreach (string line in lines)
                    {
                        Assert.LessOrEqual(line.Length, Dialogue.MaxLineLength, line);
                        CollectionAssert.IsEmpty(InWorldText.Problems(line), line);
                        StringAssert.DoesNotContain(Dialogue.ForbiddenWord, line.ToLowerInvariant(), line);
                        CollectionAssert.Contains(spoken, line, $"{spec.Voice}: the voice tests read \"{line}\"");
                    }
                    switch (prisoner.Hint)
                    {
                        case PrisonerHint.Fight:
                            StringAssert.Contains(LairText.FightFacts[prisoner.Topic], lines[1]);
                            roomTopics.Add(prisoner.Topic);
                            topics.Add(prisoner.Topic);
                            break;
                        case PrisonerHint.Benefit:
                            StringAssert.Contains("mending " + InWorldText.Region(prisoner.BenefitTheme), lines[1]);
                            StringAssert.Contains(LairText.BenefitFact(prisoner.BenefitTheme), lines[1]);
                            told.Add(prisoner.BenefitTheme);
                            break;
                        default:
                            StringAssert.Contains(InWorldText.Region(model.ThemeOf(plan.RealFace)), lines[1], "Names the real portal's region");
                            break;
                    }
                }
                CollectionAssert.AreEquivalent(LairPlan.BenefitThemes, told, $"seed {seed}: the rooms tell every face's benefit");
                Assert.AreEqual(roomTopics.Count, roomTopics.Distinct().Count(), $"seed {seed}: each room tells a different fight fact");
            }
            Assert.AreEqual(Voices.All.Length, voices.Count, "Every voice is heard down there over the seeds");
            Assert.AreEqual(LairText.FightFacts.Length, topics.Count, "Every fight fact is told over the seeds");

            // The benefit facts follow the repair mapping (BossBenefits.Of) and the leaders' repair lines.
            StringAssert.Contains("one more heart", LairText.BenefitFact(Theme.Town));
            StringAssert.Contains("drift toward you", LairText.BenefitFact(Theme.Biology));
            StringAssert.Contains("fewer", LairText.BenefitFact(Theme.Chemistry));
            StringAssert.Contains("more slowly", LairText.BenefitFact(Theme.Physics));
            StringAssert.Contains("widens the gaps", LairText.BenefitFact(Theme.Math));
            StringAssert.Contains("slower and come less often", LairText.BenefitFact(Theme.EarthAtmosphere));
            foreach (string text in LairText.AllFixedText())
            {
                CollectionAssert.IsEmpty(InWorldText.BannedIn(text), text);
                CollectionAssert.Contains(Dialogue.AllFixedText().ToList(), text, "The text scan reads it");
            }
        }

        [Test]
        public void TheLairRoom_KeepsEveryPrisonerInTheirCell_AndTalkableFromTheWalkway()
        {
            List<string> problems = LairRoom.Problems();
            CollectionAssert.IsEmpty(problems, string.Join("; ", problems));
            Assert.AreEqual(LairPlan.PrisonersPerRoom, LairRoom.CellCount);
            for (int i = 0; i < LairRoom.CellCount; i++)
            {
                Assert.IsTrue(LairRoom.Cell(i).Contains(LairRoom.PrisonerStart(i)), $"prisoner {i} starts in their cell");
                Assert.Less(Vector2.Distance(LairRoom.Front(i), LairRoom.PrisonerStart(i)), NpcTalker.DefaultRange, "talkable through the bars");
                Assert.IsTrue(LairRoom.Walkway.Contains(LairRoom.Front(i)));
            }
            // The shell is a town room's: one exit, and the player's walkway reaches it from the arrival point.
            Assert.IsTrue(LairRoom.Walkway.Contains(TownInterior.EntryPoint));
            Assert.IsTrue(TownInterior.ExitLane.Contains(TownInterior.EntryPoint));
            Assert.Less(LairRoom.BarsY + LairRoom.BarsThickness, LairRoom.PrisonerFeetMin, "The bars stand in front of the prisoners' feet");
        }

        [Test]
        public void LairRooms_SitOnTheirOwnScreens_OffTheNet_ClearOfTheArenaAndTheTownRooms()
        {
            var model = new CubeModel(1);
            var faceScreens = new HashSet<Vector2Int>();
            var go = new GameObject("World (layout only)");
            try
            {
                CubeWorld world = go.AddComponent<CubeWorld>();
                foreach (ScreenAddress s in model.AllScreens()) faceScreens.Add(ScreenMath.ScreenIndex(world.ScreenCenter(s)));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
            var taken = new HashSet<Vector2Int> { CoreArena.DefaultArenaScreen };
            for (int f = 0; f < CubeSettings.FaceCount; f++) taken.Add(TownInteriors.RoomScreen((FaceId)f));
            var rooms = new HashSet<Vector2Int>();
            for (int f = 0; f < CubeSettings.FaceCount; f++)
            {
                Vector2Int screen = LairRooms.RoomScreen((FaceId)f);
                Assert.IsTrue(rooms.Add(screen), $"Face {f}'s lair room has its own screen");
                Assert.IsFalse(faceScreens.Contains(screen), "Not on the cube net");
                Assert.IsFalse(taken.Contains(screen), "Not the arena's or a town room's screen");
                Assert.Less(screen.y, TownInteriors.RoomRow, "Below the town rooms");
                Assert.AreEqual(screen, ScreenMath.ScreenIndex(LairRooms.RoomCentre((FaceId)f)), "Its centre is on its screen");
            }
            foreach (Vector2Int a in rooms)
                foreach (Vector2Int b in rooms)
                    if (a != b) Assert.Greater((a - b).sqrMagnitude, 1, "Lair rooms are never side by side");

            // Walking out puts the player beside the portal, towards its screen's centre, clear of its trigger.
            var portal = new Vector2(10f, 4f);
            var centre = new Vector2(12f, 7.5f);
            Vector2 back = LairRooms.ReturnPoint(portal, centre);
            Assert.AreEqual(LairRooms.ReturnDistance, Vector2.Distance(portal, back), 1e-4f);
            Assert.Greater(LairRooms.ReturnDistance, CorePortal.Radius + TimeField.PlayerRadius + 0.2f, "Off the portal's trigger");
            Assert.AreEqual(Facing.North, LairRooms.ReturnFacing(portal, centre));
            Assert.AreEqual(Facing.West, LairRooms.ReturnFacing(portal, new Vector2(2f, 5f)));
        }

        [Test]
        public void FindProblems_FlagsABrokenLair()
        {
            var model = new CubeModel(3);
            CubeLayout layout = CubeLayout.Generate(model, Library());
            LairPlan plan = LairPlan.ForRun(model);
            CollectionAssert.IsNotEmpty(LairPlan.FindProblems(model, layout, null, SeedSweep.FindReachable(model)), "No plan");
            var nothingReachable = new HashSet<ScreenAddress> { model.StartScreen };
            List<string> unreachable = LairPlan.FindProblems(model, layout, plan, nothingReachable);
            Assert.IsTrue(unreachable.Any(p => p.Contains("real portal") && p.Contains("unreachable")), string.Join("; ", unreachable));
            CollectionAssert.IsEmpty(LairPlan.FindProblems(model, layout, plan, SeedSweep.FindReachable(model)));
        }

        [Test]
        public void TheMap_ShowsTheLairAndTheRealPortal_InTheLegendAndHeader()
        {
            CollectionAssert.Contains(CubeMap.LegendLabels, "Lair");
            CollectionAssert.Contains(CubeMap.LegendLabels, "Core portal");
            CollectionAssert.Contains(CubeMap.AllTexts().ToList(), LairText.MapBadge);
            Assert.LessOrEqual(LairText.MapBadge.Length, CubeMap.InsideText(InteriorKind.LeadersHall).Length, "The badge fits the header tab");
            Assert.AreEqual(2, CubeMap.LegendRows(960f), "Still two legend rows at 960 px");
        }

        [Test]
        public void TheLairArt_KeepsThePixelStyle()
        {
            Color32 outline = TownArt.OutlineColor;
            foreach (Color32[] tile in new[] { LairArt.FloorPixels(), LairArt.WallPixels(), LairArt.StrawPixels() })
            {
                Assert.AreEqual(InteriorArt.TilePixels * InteriorArt.TilePixels, tile.Length);
                Assert.IsTrue(tile.All(p => p.a == 255), "Tiles are opaque");
                Assert.Greater(tile.Distinct().Count(), 1, "Tiles are patterned");
            }
            TownArt.Canvas bars = LairArt.BarsCanvas(new Vector2(LairRoom.CellWidth, LairRoom.BarsArtHeight));
            Assert.AreEqual(Mathf.RoundToInt(3.6f * LairArt.PixelsPerUnit), bars.Width, "8 pixels a unit");
            Assert.IsTrue(bars.Pixels.Any(p => p.r == outline.r && p.g == outline.g && p.b == outline.b && p.a == 255), "The plum outline");
            Assert.IsTrue(bars.Pixels.Any(p => p.a == 0), "Gaps between the bars");
            Sprite sprite = LairArt.Bars(new Vector2(LairRoom.CellWidth, LairRoom.BarsArtHeight));
            Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode);
            Assert.AreSame(sprite, LairArt.Bars(new Vector2(LairRoom.CellWidth, LairRoom.BarsArtHeight)), "Cached");
            // 4.13 review: the tiles' pixels are drawn once, too.
            Assert.AreSame(LairArt.FloorPixels(), LairArt.FloorPixels());
            Assert.AreSame(LairArt.WallPixels(), LairArt.WallPixels());
            Assert.AreSame(LairArt.StrawPixels(), LairArt.StrawPixels());
        }

        [Test]
        public void ThePrisoners_WholeDesignsRead_ThroughAndOverTheirBars()
        {
            // 4.13 review: the bars are thin and sparse: between the sill and the rail, at least three quarters see-through.
            TownArt.Canvas bars = LairArt.BarsCanvas(new Vector2(LairRoom.CellWidth, LairRoom.BarsArtHeight));
            int open = 0, all = 0;
            for (int y = 3; y < bars.Height - 2; y++)
            for (int x = 0; x < bars.Width; x++)
            {
                all++;
                if (bars.Pixels[y * bars.Width + x].a == 0) open++;
            }
            Assert.GreaterOrEqual(open, all * 3 / 4, $"{open} of {all} pixels see-through");
            // And low: they cover at most the lower half of a prisoner's drawn design, even at the back of the cell.
            float covered = LairRoom.BarsY + LairRoom.BarsArtHeight - LairRoom.PrisonerFeetMin;
            Assert.LessOrEqual(covered, NpcFactory.DesignSize / 2f, "the bars cover at most half a prisoner");
            Assert.Greater(LairRoom.PrisonerFeetMin, LairRoom.BarsY + LairRoom.BarsThickness, "still behind the bars");
        }

        [Test]
        public void TheReturnPoint_ClearsThePortalsTrigger_InEveryDirection_AndStaysOnItsScreen()
        {
            // Whichever way the screen's centre lies from the portal (and at any distance), the player put back beside it
            // stands at least ReturnClearance clear of its trigger.
            Assert.GreaterOrEqual(LairRooms.ReturnDistance - LairRooms.PortalContact, LairRooms.ReturnClearance);
            for (int deg = 0; deg < 360; deg += 5)
            foreach (float d in new[] { 0f, 0.3f, 1f, 3f, 9f })
            {
                var portal = new Vector2(4f, -2f);
                Vector2 centre = portal + new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad)) * d;
                Vector2 back = LairRooms.ReturnPoint(portal, centre);
                Assert.GreaterOrEqual(Vector2.Distance(portal, back) - LairRooms.PortalContact, LairRooms.ReturnClearance - 1e-4f,
                    $"{deg} degrees, {d} away");
            }
            // On every module with a core slot: the return point lies inside the screen's walls, clear of the module's solids
            // and gate slots, and the player there stands ReturnClearance clear of the portal.
            ModuleLibrary library = Library();
            Vector2 half = ScreenMath.DefaultScreenSize / 2f - Vector2.one * (ScreenEdges.EdgeWallThickness + TimeField.PlayerRadius);
            int checkedSlots = 0;
            foreach (ModuleLibrary.ThemePool pool in library.SciencePools)
            foreach (ScreenModule module in pool.Modules)
            foreach (CoreEntranceSlot slot in module.CoreEntrances)
            {
                Vector2 portal = slot.transform.position - module.transform.position;
                Vector2 back = LairRooms.ReturnPoint(portal, Vector2.zero);
                string context = $"{module.name} {slot.name}";
                Assert.LessOrEqual(Mathf.Abs(back.x), half.x, context);
                Assert.LessOrEqual(Mathf.Abs(back.y), half.y, context);
                Assert.GreaterOrEqual(Vector2.Distance(portal, back) - LairRooms.PortalContact, LairRooms.ReturnClearance, context);
                var body = new Rect(back - Vector2.one * TimeField.PlayerRadius, Vector2.one * 2f * TimeField.PlayerRadius);
                foreach (BoxCollider2D box in module.GetComponentsInChildren<BoxCollider2D>(true))
                {
                    if (box.isTrigger) continue;
                    Vector2 c = (Vector2)(box.transform.position - module.transform.position) + box.offset;
                    Vector2 size = Vector2.Scale(box.size, box.transform.lossyScale);
                    Assert.IsFalse(new Rect(c - size / 2f, size).Overlaps(body), $"{context}: the return point is in {box.name}");
                }
                foreach (GateSlot gate in module.Gates)
                {
                    Vector2 g = gate.transform.position - module.transform.position;
                    Vector2 size = CubeWorld.GateSize(gate);
                    Assert.IsFalse(new Rect(g - size / 2f, size).Overlaps(body), $"{context}: the return point is in a gate");
                }
                checkedSlots++;
            }
            Assert.Greater(checkedSlots, 0, "Modules with core slots");
        }
    }
}
