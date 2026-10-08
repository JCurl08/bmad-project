using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The cube-net map's pure parts (story 21): the cross net places every face once for any centre face and turn, every
    /// edge drawn adjacent in the net joins real cube neighbours (TryStep), net cells and screens round-trip, directions
    /// turn with their face; the visit set and tracker reset to the start screen; markers are filtered by the fog; and the
    /// map's text keeps the voice rules and fits the 960x600 canvas.
    /// </summary>
    public class CubeMapTests
    {
        private static readonly FaceId[] Faces = (FaceId[])Enum.GetValues(typeof(FaceId));
        private static readonly Facing[] Facings = (Facing[])Enum.GetValues(typeof(Facing));
        private static readonly int[] FaceSizes = { 1, 2, 3 };

        private static IEnumerable<CubeNet> EveryNet(int n)
        {
            foreach (FaceId centre in Faces)
                for (int turns = 0; turns < 4; turns++)
                    yield return CubeNet.Cross(centre, n, turns);
        }

        // ---------- Net placement ----------

        [Test]
        public void TownNet_IsACross_TownInTheMiddle_OppositeFaceAtTheEndOfTheEastArm()
        {
            CubeNet net = CubeNet.For(new CubeModel(1));
            Assert.AreEqual(FaceId.Front, net.Centre);
            Assert.AreEqual(CubeNet.CentreSlot, net.PlacementOf(FaceId.Front).Slot);
            Assert.AreEqual(0, net.PlacementOf(FaceId.Front).Turns);
            Assert.AreEqual(FaceId.Back, net.Opposite);
            Assert.AreEqual(new Vector2Int(3, 1), net.PlacementOf(FaceId.Back).Slot);
            foreach (Facing d in Facings)
            {
                Assert.IsTrue(net.TryGetFace(CubeNet.CentreSlot + d.ToVector(), out FaceId face));
                Assert.AreEqual(CubeModel.NeighborFace(FaceId.Front, d), face, $"Town's {d} neighbour sits {d} of it");
            }
            Assert.AreEqual(new Vector2Int(12, 9), net.SizeInCells);
        }

        [Test]
        public void EveryCentreAndTurn_PlacesEveryFaceOnce_InTheCrossSlots()
        {
            var crossSlots = new HashSet<Vector2Int>
            {
                new Vector2Int(1, 1), new Vector2Int(0, 1), new Vector2Int(2, 1), new Vector2Int(3, 1),
                new Vector2Int(1, 2), new Vector2Int(1, 0),
            };
            foreach (int n in FaceSizes)
            {
                foreach (CubeNet net in EveryNet(n))
                {
                    var faces = net.Placements.Select(p => p.Face).ToList();
                    CollectionAssert.AreEquivalent(Faces, faces, $"centre {net.Centre}");
                    CollectionAssert.AreEquivalent(crossSlots, net.Placements.Select(p => p.Slot).ToList());
                    Assert.AreEqual(CubeModel.FaceWithNormal(-CubeModel.Basis(net.Centre).Normal), net.Opposite);
                }
            }
        }

        [Test]
        public void EveryEdgeDrawnAdjacent_JoinsRealCubeNeighbours_ForEveryOrientation()
        {
            foreach (int n in FaceSizes)
            {
                var model = new CubeModel(7, n);
                foreach (CubeNet net in EveryNet(n))
                {
                    int crossFace = 0;
                    Vector2Int size = net.SizeInCells;
                    for (int y = 0; y < size.y; y++)
                    {
                        for (int x = 0; x < size.x; x++)
                        {
                            var a = new Vector2Int(x, y);
                            if (!net.TryGetScreen(a, out ScreenAddress from)) continue;
                            foreach (Facing netDir in Facings)
                            {
                                if (!net.TryGetScreen(a + netDir.ToVector(), out ScreenAddress to)) continue;
                                Facing onFace = net.FromNet(from.Face, netDir);
                                Assert.IsTrue(model.TryStep(from, onFace, out ScreenAddress stepped, out Facing arrived),
                                    $"centre {net.Centre} turned {net.PlacementOf(net.Centre).Turns}: {from} {onFace}");
                                Assert.AreEqual(to, stepped,
                                    $"centre {net.Centre} turned {net.PlacementOf(net.Centre).Turns}: net {netDir} from {from}");
                                // Walking on keeps the net direction: the arrival facing points the same way in the net.
                                Assert.AreEqual(netDir, net.ToNet(to.Face, arrived), $"{from} -> {to}");
                                if (from.Face != to.Face) crossFace++;
                            }
                        }
                    }
                    // Five joined face pairs in the cross, n cells each, checked from both sides.
                    Assert.AreEqual(5 * n * 2, crossFace, $"centre {net.Centre}");
                }
            }
        }

        [Test]
        public void NetCells_AndScreens_RoundTrip_AndTheCornersAreEmpty()
        {
            foreach (int n in FaceSizes)
            {
                var model = new CubeModel(1, n);
                foreach (CubeNet net in EveryNet(n))
                {
                    var used = new HashSet<Vector2Int>();
                    foreach (ScreenAddress s in model.AllScreens())
                    {
                        Vector2Int cell = net.NetCell(s);
                        Assert.IsTrue(used.Add(cell), $"{s} shares a net cell");
                        Assert.IsTrue(net.TryGetScreen(cell, out ScreenAddress back));
                        Assert.AreEqual(s, back);
                    }
                    Assert.AreEqual(6 * n * n, used.Count);
                    Assert.IsFalse(net.TryGetScreen(new Vector2Int(0, 0), out _), "lower-left corner is empty");
                    Assert.IsFalse(net.TryGetScreen(new Vector2Int(3 * n, 2 * n), out _), "upper-right corner is empty");
                    Assert.IsFalse(net.TryGetScreen(new Vector2Int(-1, n), out _));
                    Assert.IsFalse(net.TryGetScreen(new Vector2Int(4 * n, n), out _));
                }
            }
        }

        [Test]
        public void PointsAndDirections_TurnWithTheirFace()
        {
            const int n = 3;
            foreach (CubeNet net in EveryNet(n))
            {
                foreach (FaceId face in Faces)
                {
                    foreach (Facing f in Facings)
                    {
                        Vector2 turned = net.ToNetDirection(face, f.ToVector());
                        Assert.AreEqual((Vector2)net.ToNet(face, f).ToVector(), turned, $"{face} {f}");
                        Assert.AreEqual(f, net.FromNet(face, net.ToNet(face, f)));
                    }
                    for (int y = 0; y < n; y++)
                    {
                        for (int x = 0; x < n; x++)
                        {
                            var s = new ScreenAddress(face, x, y);
                            Vector2Int cell = net.NetCell(s) - net.PlacementOf(face).Slot * n;
                            // The middle of a screen, as a point of the whole face, lands in the middle of its net cell.
                            Vector2 middle = net.ToNetLocal(face, new Vector2((x + 0.5f) / n, (y + 0.5f) / n)) * n;
                            Assert.That(Vector2.Distance(middle, (Vector2)cell + Vector2.one * 0.5f), Is.LessThan(1e-4f), s.ToString());
                        }
                    }
                }
            }
        }

        // ---------- Visits ----------

        [Test]
        public void VisitSet_CountsEachScreenOnce_AndClears()
        {
            var set = new VisitSet();
            var a = new ScreenAddress(FaceId.Front, 0, 0);
            var b = new ScreenAddress(FaceId.Right, 1, 2);
            Assert.IsTrue(set.Visit(a));
            Assert.IsFalse(set.Visit(a), "a second visit is not new");
            Assert.IsTrue(set.Visit(b));
            Assert.AreEqual(2, set.Count);
            Assert.IsTrue(set.IsVisited(b));
            set.Clear();
            Assert.AreEqual(0, set.Count);
            Assert.IsFalse(set.IsVisited(a));
        }

        [Test]
        public void VisitTracker_ResetLeavesOnlyTheStartScreen_AndVisitAllLightsEveryUnsealedScreen()
        {
            ArtCatalog.Use(AssetDatabase.LoadAssetAtPath<ArtCatalog>("Assets/Resources/ArtCatalog.asset"));
            var root = new GameObject("Map Test");
            try
            {
                var world = root.AddComponent<CubeWorld>();
                world.SealedThemes.Add(Theme.Math);
                world.Rebuild(42);
                var tracker = root.AddComponent<VisitTracker>();
                tracker.World = world;

                tracker.Visit(new ScreenAddress(FaceId.Front, 2, 2));
                tracker.Visit(new ScreenAddress(FaceId.Top, 1, 1));
                int resets = tracker.RunsTracked;
                tracker.ResetRun();
                Assert.AreEqual(resets + 1, tracker.RunsTracked);
                Assert.AreEqual(1, tracker.Visits.Count, "a new run's fog: only the start screen");
                Assert.IsTrue(tracker.IsVisited(world.Model.StartScreen));

                tracker.VisitAll();
                int unsealed = world.Model.AllScreens().Count(s => !world.Model.IsSealed(s.Face));
                Assert.AreEqual(5 * 9, unsealed);
                Assert.AreEqual(unsealed, tracker.Visits.Count);
                FaceId sealedFace = world.Model.FaceOf(Theme.Math);
                Assert.IsFalse(tracker.IsVisited(new ScreenAddress(sealedFace, 0, 0)), "sealed faces are never lit");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                ArtCatalog.Use(null);
            }
        }

        // ---------- Markers ----------

        [Test]
        public void Markers_OnlyOnVisitedScreens_KeepTheirState()
        {
            var visited = new ScreenAddress(FaceId.Right, 1, 1);
            var hidden = new ScreenAddress(FaceId.Right, 2, 1);
            var all = new List<MapMarker>
            {
                new MapMarker(MapMarkerKind.Gate, visited, new Vector2(0.2f, 0.3f), true),
                new MapMarker(MapMarkerKind.Trial, visited, new Vector2(0.5f, 0.5f)),
                new MapMarker(MapMarkerKind.Item, hidden, new Vector2(0.5f, 0.5f)),
                new MapMarker(MapMarkerKind.Portal, hidden, new Vector2(0.5f, 0.5f)),
            };
            var set = new VisitSet();
            Assert.IsEmpty(MapMarkers.Visible(all, set), "nothing visited, nothing shown");
            set.Visit(visited);
            List<MapMarker> shown = MapMarkers.Visible(all, set);
            Assert.AreEqual(2, shown.Count);
            Assert.IsTrue(shown.All(m => m.Screen == visited));
            Assert.IsTrue(shown.Single(m => m.Kind == MapMarkerKind.Gate).Done, "an opened gate stays opened");
            Assert.IsFalse(shown.Single(m => m.Kind == MapMarkerKind.Trial).Done);
            set.Visit(hidden);
            Assert.AreEqual(4, MapMarkers.Visible(all, set).Count);
        }

        [Test]
        public void MarkerPositions_AreFractionsOfTheirScreen_Clamped()
        {
            Vector2 centre = new Vector2(40f, 25f);
            Vector2 size = CubeWorld.ScreenSize;
            Assert.AreEqual(new Vector2(0.5f, 0.5f), MapMarkers.LocalIn(centre, centre));
            Assert.AreEqual(new Vector2(0f, 1f), MapMarkers.LocalIn(centre, centre + new Vector2(-size.x, size.y)));
            Vector2 q = MapMarkers.LocalIn(centre, centre + new Vector2(size.x / 4f, -size.y / 4f));
            Assert.That(Vector2.Distance(new Vector2(0.75f, 0.25f), q), Is.LessThan(1e-5f));
            var m = new MapMarker(MapMarkerKind.Item, default, new Vector2(2f, -1f));
            Assert.AreEqual(new Vector2(1f, 0f), m.Local);
        }

        // ---------- Text and size ----------

        private static readonly string[] ScienceWords =
        {
            "isotope", "radioactive", "relativity", "evolution", "niche", "element", "darwin", "curie", "einstein", "euler",
            "biology", "chemistry", "physics", "math", "science", "earth",
        };

        [Test]
        public void MapText_KeepsTheVoiceRules()
        {
            foreach (string text in CubeMap.AllTexts())
            {
                string lower = text.ToLowerInvariant();
                StringAssert.DoesNotContain(Dialogue.ForbiddenWord, lower, text);
                foreach (string word in ScienceWords) StringAssert.DoesNotContain(word, lower, text);
            }
        }

        [Test]
        public void TheNet_FitsTheWebGLCanvas_AtAReadableSize()
        {
            float cell = CubeMap.CellSize(960f, 600f, CubeSettings.DefaultFaceSize);
            Assert.That(cell, Is.GreaterThanOrEqualTo(32f), "screens are big enough to read");
            float netW = CubeNet.Columns * CubeSettings.DefaultFaceSize * cell + 3 * 6f;
            float netH = CubeNet.Rows * CubeSettings.DefaultFaceSize * cell + 2 * 6f;
            Assert.That(netW, Is.LessThanOrEqualTo(960f - 32f));
            Assert.That(netH, Is.LessThanOrEqualTo(600f - 56f - CubeMap.LegendHeight(960f)));

            // The legend (4.5 added "Town") fits the 960 px canvas: rows of columns at least MinLegendStep wide, every label
            // (about 8 px a character at the map's font, after a marker glyph) inside its column.
            int columns = CubeMap.LegendColumns(960f), rows = CubeMap.LegendRows(960f);
            Assert.GreaterOrEqual(columns * rows, CubeMap.LegendLabels.Length);
            float step = (960f - 32f) / columns;
            Assert.GreaterOrEqual(step, CubeMap.MinLegendStep);
            foreach (string label in CubeMap.LegendLabels)
                Assert.LessOrEqual(30f + label.Length * 8f, step, label);
            Assert.AreEqual(2, rows, "Thirteen labels (4.13 added \"Portal\" and \"Lair\") still wrap into two rows at 960 px");
        }

        [Test]
        public void FragmentTags_SitBesideTheirFaces_NeverOverAScreen_EachOther_OrTheHeaderAndLegend()
        {
            foreach (Vector2 screen in new[] { new Vector2(960f, 600f), new Vector2(1280f, 720f), new Vector2(1920f, 1080f), new Vector2(800f, 600f) })
            for (int seed = 1; seed <= 5; seed++)
            {
                CubeNet net = CubeNet.For(new CubeModel(seed));
                CubeMap.NetFrame(screen.x, screen.y, net.FaceSize, out float left, out float top, out float cell);
                var faces = net.Placements.Select(p => CubeMap.SlotRect(p.Slot, net.FaceSize, left, top, cell)).ToList();
                var tags = new List<Rect>();
                foreach (NetPlacement p in net.Placements)
                {
                    Rect tag = CubeMap.FragmentTagRect(net, p.Face, left, top, cell);
                    string where = $"{screen} seed {seed} {p.Face} at {p.Slot}";
                    foreach (Rect face in faces)
                    {
                        // The face's frame is drawn 2 px round its screens.
                        var framed = new Rect(face.x - 2f, face.y - 2f, face.width + 4f, face.height + 4f);
                        Assert.IsFalse(tag.Overlaps(framed), $"{where}: the tag {tag} covers a face {framed}");
                    }
                    foreach (Rect other in tags) Assert.IsFalse(tag.Overlaps(other), $"{where}: two tags overlap");
                    Assert.GreaterOrEqual(tag.xMin, 0f, where);
                    Assert.LessOrEqual(tag.xMax, screen.x, where);
                    Assert.GreaterOrEqual(tag.yMin, CubeMap.HeaderHeight, $"{where}: under the header");
                    Assert.LessOrEqual(tag.yMax, screen.y - CubeMap.LegendHeight(screen.x), $"{where}: above the legend");
                    // Beside its own face: within a few pixels of its frame.
                    Rect own = CubeMap.SlotRect(p.Slot, net.FaceSize, left, top, cell);
                    var near = new Rect(own.x - 8f - CubeMap.TagWidth, own.y - 8f - CubeMap.TagHeight, own.width + 16f + 2f * CubeMap.TagWidth,
                        own.height + 16f + 2f * CubeMap.TagHeight);
                    Assert.IsTrue(near.Contains(tag.min) && near.Contains(tag.max), $"{where}: next to its face");
                    tags.Add(tag);
                }
                Assert.GreaterOrEqual(CubeMap.TagWidth, 6f * 8f, "\"mended\" fits (about 8 px a character)");
            }
        }
    }
}
