using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Cube.Tests
{
    /// <summary>I/O matrix rows for the seeded cube model (everything except runtime play).</summary>
    public class CubeModelTests
    {
        private static readonly int[] FaceSizes = { 2, 3 };
        private static readonly FaceId[] Faces = (FaceId[])Enum.GetValues(typeof(FaceId));
        private static readonly Facing[] Facings = (Facing[])Enum.GetValues(typeof(Facing));

        // ---------- RNG ----------

        [Test]
        public void Rng_MatchesPcg32ReferenceVector()
        {
            // pcg32-demo reference output for pcg32_srandom(42, 54).
            var rng = new SeededRng(42UL, 54UL);
            uint[] expected = { 0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e };
            foreach (uint value in expected) Assert.AreEqual(value, rng.NextUInt());
        }

        [Test]
        public void Rng_SameSeedSameSequence()
        {
            var a = new SeededRng(1234);
            var b = new SeededRng(1234);
            for (int i = 0; i < 100; i++) Assert.AreEqual(a.NextInt(1000), b.NextInt(1000));
        }

        [Test]
        public void Rng_NextIntStaysInRange_AndRejectsNonPositiveMax()
        {
            var rng = new SeededRng(7);
            var seen = new HashSet<int>();
            for (int i = 0; i < 1000; i++)
            {
                int v = rng.NextInt(5);
                Assert.That(v, Is.InRange(0, 4));
                seen.Add(v);
            }
            Assert.AreEqual(5, seen.Count, "NextInt(5) never produced some values");
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(0));
        }

        [Test]
        public void Rng_ShuffleIsAPermutation()
        {
            var list = Enumerable.Range(0, 20).ToList();
            new SeededRng(99).Shuffle(list);
            CollectionAssert.AreEquivalent(Enumerable.Range(0, 20), list);
            CollectionAssert.AreNotEqual(Enumerable.Range(0, 20), list);
        }

        // ---------- Seeded placement ----------

        private static string Placement(CubeModel m) => string.Join(",", Faces.Select(f => m.ThemeOf(f)));

        [Test]
        public void SameSeed_GivesIdenticalPlacementAndSealedSet()
        {
            var a = new CubeModel(1234);
            var b = new CubeModel(1234);
            foreach (FaceId face in Faces)
            {
                Assert.AreEqual(a.ThemeOf(face), b.ThemeOf(face), $"Theme differs on {face}");
                Assert.AreEqual(a.IsSealed(face), b.IsSealed(face), $"Sealed differs on {face}");
            }
        }

        [Test]
        public void DifferentSeeds_GiveAtLeastTwoPlacements()
        {
            var placements = new HashSet<string>();
            for (int seed = 1; seed <= 50; seed++) placements.Add(Placement(new CubeModel(seed)));
            Assert.GreaterOrEqual(placements.Count, 2);
        }

        [Test]
        public void TownIsFixedOnTheStartFace_AndNoFaceIsSealed([ValueSource(nameof(FaceSizes))] int n)
        {
            for (int seed = 1; seed <= 50; seed++)
            {
                var m = new CubeModel(seed, n);
                Assert.AreEqual(Theme.Town, m.ThemeOf(CubeModel.StartFace), $"seed {seed}");
                Assert.AreEqual(FaceId.Front, CubeModel.StartFace);
                Assert.AreEqual(CubeModel.StartFace, m.StartScreen.Face, $"seed {seed}");
                Assert.IsTrue(m.IsInside(m.StartScreen.Cell));
                Assert.IsFalse(m.IsSealed(m.StartScreen.Face));
                // 1.24: the run starts on Town's centre screen (1,1 at 3x3; 0,0 at 2x2, which has no centre cell).
                Assert.AreEqual(n == 3 ? new Vector2Int(1, 1) : Vector2Int.zero, m.StartScreen.Cell, $"seed {seed}");

                CollectionAssert.AreEquivalent(Enum.GetValues(typeof(Theme)), Faces.Select(m.ThemeOf),
                    $"seed {seed}: each theme must appear exactly once");
                foreach (FaceId face in Faces)
                {
                    Theme theme = m.ThemeOf(face);
                    Assert.AreEqual(face, m.FaceOf(theme));
                    Assert.IsTrue(CubeModel.IsBuilt(theme), $"{theme} is built");
                    Assert.IsFalse(m.IsSealed(face), $"seed {seed}: {face} ({theme}) is sealed");
                    // Sealing on purpose (tests, future content) still seals exactly that theme, never Town.
                    var sealedModel = new CubeModel(seed, n, new[] { Theme.EarthAtmosphere, Theme.Town });
                    Assert.AreEqual(theme == Theme.EarthAtmosphere, sealedModel.IsSealed(face), $"seed {seed}: {face} ({theme})");
                    Assert.AreEqual(theme, sealedModel.ThemeOf(face), "Sealing never moves a theme");
                }
            }
        }

        // ---------- Geometry ----------

        [Test]
        public void Bases_AreRightHandedAndDistinct()
        {
            var normals = new HashSet<Int3>();
            foreach (FaceId face in Faces)
            {
                FaceBasis b = CubeModel.Basis(face);
                Assert.AreEqual(b.Normal, Int3.Cross(b.Right, b.Up), $"{face}: Right x Up must equal Normal");
                Assert.AreEqual(1, Int3.Dot(b.Normal, b.Normal));
                Assert.IsTrue(normals.Add(b.Normal), $"{face}: duplicate normal");
            }
        }

        [Test]
        public void FaceNeighbours_AreFourDistinctAdjacentFaces_AndSymmetric()
        {
            foreach (FaceId face in Faces)
            {
                FaceBasis b = CubeModel.Basis(face);
                var neighbours = Facings.Select(e => CubeModel.NeighborFace(face, e)).ToList();
                Assert.AreEqual(4, neighbours.Distinct().Count(), $"{face}");
                foreach (FaceId g in neighbours)
                {
                    Assert.AreNotEqual(face, g);
                    Assert.AreNotEqual(-b.Normal, CubeModel.Basis(g).Normal, $"{face} cannot border its opposite");
                    Assert.IsTrue(Facings.Any(e => CubeModel.NeighborFace(g, e) == face),
                        $"{g} borders {face} but not the other way round");
                }
            }
        }

        [Test]
        public void InnerStep_EastFromOrigin_StaysOnFace()
        {
            var m = new CubeModel(1234, 2);
            foreach (FaceId face in Faces)
            {
                ScreenAddress next = CubeModel.Step(2, new ScreenAddress(face, 0, 0), Facing.East, out Facing facing);
                Assert.AreEqual(new ScreenAddress(face, 1, 0), next);
                Assert.AreEqual(Facing.East, facing);
                if (!m.IsSealed(face))
                {
                    Assert.IsTrue(m.TryStep(new ScreenAddress(face, 0, 0), Facing.East, out ScreenAddress t, out _));
                    Assert.AreEqual(new ScreenAddress(face, 1, 0), t);
                }
            }
        }

        /// <summary>Every (face, edge cell, outward direction) triple for face size n.</summary>
        private static IEnumerable<(ScreenAddress from, Facing dir)> EdgeExits(int n)
        {
            foreach (FaceId face in Faces)
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                        foreach (Facing dir in Facings)
                        {
                            Vector2Int c = new Vector2Int(x, y) + dir.ToVector();
                            if (c.x < 0 || c.y < 0 || c.x >= n || c.y >= n)
                                yield return (new ScreenAddress(face, x, y), dir);
                        }
        }

        [Test]
        public void EdgeCrossing_LandsOnGeometricNeighbour_WithCorrectFacing([ValueSource(nameof(FaceSizes))] int n)
        {
            int count = 0;
            foreach (var (from, dir) in EdgeExits(n))
            {
                count++;
                FaceBasis a = CubeModel.Basis(from.Face);
                ScreenAddress next = CubeModel.Step(n, from, dir, out Facing facing);
                FaceBasis b = CubeModel.Basis(next.Face);
                string ctx = $"{from} {dir} -> {next} {facing}";

                Assert.AreEqual(a.Direction(dir), b.Normal, $"{ctx}: wrong neighbour face");
                Assert.AreEqual(-a.Normal, b.Direction(facing), $"{ctx}: new facing must point away from the old face");
                Assert.IsTrue(next.Cell.x >= 0 && next.Cell.y >= 0 && next.Cell.x < n && next.Cell.y < n, $"{ctx}: off-face cell");

                // Matching edge cell: the cell must sit on the entry edge, i.e. stepping back leaves the face.
                Vector2Int behind = next.Cell - facing.ToVector();
                Assert.IsFalse(behind.x >= 0 && behind.y >= 0 && behind.x < n && behind.y < n, $"{ctx}: not on the entry edge");

                // The two cells touch in 3D: their centres (scaled by n) differ by exactly d - n_old.
                Assert.AreEqual(Centre(next, n) - Centre(from, n), a.Direction(dir) - a.Normal, $"{ctx}: cells do not touch");
            }
            Assert.AreEqual(6 * 4 * n, count, "Each face has 4n edge exits");
        }

        private static Int3 Centre(ScreenAddress s, int n)
        {
            FaceBasis b = CubeModel.Basis(s.Face);
            return b.Normal * n + b.Right * (2 * s.Cell.x + 1 - n) + b.Up * (2 * s.Cell.y + 1 - n);
        }

        [Test]
        public void EdgeCrossing_RoundTripReturnsToOrigin([ValueSource(nameof(FaceSizes))] int n)
        {
            foreach (var (from, dir) in EdgeExits(n))
            {
                ScreenAddress next = CubeModel.Step(n, from, dir, out Facing facing);
                ScreenAddress back = CubeModel.Step(n, next, facing.Opposite(), out Facing backFacing);
                Assert.AreEqual(from, back, $"{from} {dir} -> {next} {facing} -> {back}");
                Assert.AreEqual(dir.Opposite(), backFacing, $"{from} {dir}: return facing must be the reverse of the original step");

                // Reversing again reproduces the first crossing exactly.
                ScreenAddress again = CubeModel.Step(n, back, backFacing.Opposite(), out Facing againFacing);
                Assert.AreEqual(next, again);
                Assert.AreEqual(facing, againFacing);
            }
        }

        // ---------- Stop on a face crossing (1.25) ----------

        [Test]
        public void FaceCrossing_PressingTowardTheDoorwayBehind_ReturnsThroughTheSameEdge([ValueSource(nameof(FaceSizes))] int n)
        {
            // After a face crossing the player stands facing into the new face; the doorway it came through is behind it.
            // A fresh press toward that doorway (the reverse of the entry facing) walks back over the same edge, onto the
            // screen it left, facing the reverse of the way it first went, on every edge (straight, quarter- and half-turned).
            foreach (var (from, dir) in EdgeExits(n))
            {
                ScreenAddress next = CubeModel.Step(n, from, dir, out Facing entry);
                Facing back = entry.Opposite();
                Assert.IsTrue(ScreenEdges.IsFaceEdge(n, next.Cell, back), $"{from} {dir}: the doorway behind is a face edge");
                ScreenAddress again = CubeModel.Step(n, next, back, out Facing returned);
                Assert.AreEqual(from, again, $"{from} {dir} -> {next} facing {entry}, back {back}");
                Assert.AreEqual(dir.Opposite(), returned);
            }
        }

        [Test]
        public void FreshPressLatch_IgnoresHeldKeysUntilEveryKeyIsLetGoAndOneIsPressedAgain()
        {
            var latch = new Game.Tracer.FreshPressLatch();
            Assert.AreEqual(Vector2.up, latch.Filter(Vector2.up), "no wait: the input walks as it is");

            // Held through the crossing: still, however long the key stays down, and whatever else is pressed with it.
            latch.Hold();
            Assert.IsTrue(latch.Waiting);
            for (int i = 0; i < 50; i++) Assert.AreEqual(Vector2.zero, latch.Filter(Vector2.up));
            Assert.AreEqual(Vector2.zero, latch.Filter(new Vector2(1f, 1f).normalized), "a second key while one is held");
            Assert.AreEqual(Vector2.zero, latch.Filter(Vector2.right), "one key let go, another still held");
            Assert.IsTrue(latch.Waiting);

            // Everything let go, then a fresh press: it walks at once.
            Assert.AreEqual(Vector2.zero, latch.Filter(Vector2.zero));
            Assert.AreEqual(Vector2.zero, latch.Filter(new Vector2(0.05f, 0f)), "stick drift is not a press");
            Assert.AreEqual(Vector2.down, latch.Filter(Vector2.down));
            Assert.IsFalse(latch.Waiting);
            Assert.AreEqual(Vector2.left, latch.Filter(Vector2.left), "normal again");

            // Nothing held at the crossing: the first press walks.
            latch.Hold();
            Assert.AreEqual(Vector2.zero, latch.Filter(Vector2.zero));
            Assert.AreEqual(Vector2.right, latch.Filter(Vector2.right));

            // A teleport clears the wait.
            latch.Hold();
            latch.Clear();
            Assert.AreEqual(Vector2.up, latch.Filter(Vector2.up));
        }

        // ---------- Doorway edges (1.25) ----------

        private static bool Overlaps(Rect a, Rect b, float eps = 1e-4f) =>
            a.xMin < b.xMax - eps && b.xMin < a.xMax - eps && a.yMin < b.yMax - eps && b.yMin < a.yMax - eps;

        [Test]
        public void ScreenEdges_EveryEdgeIsAWallWithOneCentredDoorwayOnItsExitLane()
        {
            Vector2 half = ScreenEdges.Half;
            foreach (Facing side in Facings)
            {
                Rect door = ScreenEdges.DoorwayRect(side);
                Rect[] walls = ScreenEdges.WallRects(side);
                Assert.AreEqual(2, walls.Length);
                float width = side.IsHorizontal() ? door.height : door.width;
                Assert.GreaterOrEqual(width, 2f, $"{side}: the doorway is at least two tiles wide");
                Assert.AreEqual(2f * ScreenModule.ExitLaneHalfWidth, width, 1e-4f, $"{side}: the doorway is the exit lane's width");
                Vector2 along = side.IsHorizontal() ? new Vector2(0f, door.center.y) : new Vector2(door.center.x, 0f);
                Assert.AreEqual(Vector2.zero, along, $"{side}: the doorway is centred on its edge");
                foreach (Rect w in walls)
                {
                    Assert.IsFalse(Overlaps(w, door), $"{side}: a wall in the doorway");
                    Assert.AreEqual(ScreenEdges.EdgeWallThickness, side.IsHorizontal() ? w.width : w.height, 1e-4f);
                }
                // Walls and doorway together close the edge: the whole edge except the doorway is wall.
                float edgeLength = side.IsHorizontal() ? 2f * half.y - 2f * ScreenEdges.EdgeWallThickness : 2f * half.x;
                float covered = width;
                foreach (Rect w in walls) covered += side.IsHorizontal() ? w.height : w.width;
                Assert.AreEqual(edgeLength, covered, 1e-4f, $"{side}: walls and doorway cover the edge");
                // A point along the edge is walkable only inside the doorway.
                for (float t = -0.98f; t <= 0.98f; t += 0.02f)
                {
                    Vector2 p = side.IsHorizontal()
                        ? new Vector2(side.ToVector().x * (half.x - 0.5f), t * (half.y - ScreenEdges.EdgeWallThickness))
                        : new Vector2(t * half.x, side.ToVector().y * (half.y - 0.5f));
                    bool inWall = walls.Any(w => w.Contains(p));
                    Assert.AreNotEqual(door.Contains(p), inWall, $"{side} at {p}: wall or doorway, never both or neither");
                }
            }
        }

        [Test]
        public void ScreenEdges_WallsAndArchesStayInTheEdgeBand_OffTheLanes([ValueSource(nameof(FaceSizes))] int n)
        {
            Vector2 half = ScreenEdges.Half;
            var inner = Rect.MinMaxRect(-half.x + ScreenModule.EdgeClearance, -half.y + ScreenModule.EdgeClearance,
                half.x - ScreenModule.EdgeClearance, half.y - ScreenModule.EdgeClearance);
            float lane = ScreenModule.ExitLaneHalfWidth;
            var verticalLane = Rect.MinMaxRect(-lane, -half.y, lane, half.y);
            var horizontalLane = Rect.MinMaxRect(-half.x, -lane, half.x, lane);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    var cell = new Vector2Int(x, y);
                    foreach (Rect r in ScreenEdges.SolidRects(n, cell, _ => true))
                    {
                        Assert.IsFalse(Overlaps(r, inner), $"{cell}: {r} reaches past the edge band (content lives there)");
                        Assert.IsFalse(Overlaps(r, verticalLane) || Overlaps(r, horizontalLane), $"{cell}: {r} is in an exit lane");
                    }
                    int arches = Facings.Count(f => ScreenEdges.IsFaceEdge(n, cell, f));
                    Assert.AreEqual((x == 0 ? 1 : 0) + (x == n - 1 ? 1 : 0) + (y == 0 ? 1 : 0) + (y == n - 1 ? 1 : 0), arches,
                        $"{cell}: a gate arch on every face edge, none within the face");
                }
            }
            // An edge with no doorway (a sealed face beyond) is one solid wall.
            List<Rect> closed = ScreenEdges.SolidRects(n, Vector2Int.zero, f => f != Facing.West);
            Assert.IsTrue(closed.Any(r => r.Contains(ScreenEdges.DoorwayRect(Facing.West).center)), "the closed edge has no gap");
        }

        [Test]
        public void EdgeCrossing_KeepsNeighbouringEdgeCellsNeighbouring([ValueSource(nameof(FaceSizes))] int n)
        {
            foreach (FaceId face in Faces)
            {
                foreach (Facing dir in Facings)
                {
                    var landed = new List<ScreenAddress>();
                    for (int k = 0; k < n; k++)
                    {
                        Vector2Int cell = EdgeCell(dir, k, n);
                        landed.Add(CubeModel.Step(n, new ScreenAddress(face, cell), dir, out _));
                    }
                    Assert.AreEqual(1, landed.Select(s => s.Face).Distinct().Count(), $"{face} {dir}: one edge, one neighbour");
                    Assert.AreEqual(n, landed.Distinct().Count(), $"{face} {dir}: edge cells must map one-to-one");
                    for (int k = 1; k < n; k++)
                    {
                        Vector2Int delta = landed[k].Cell - landed[k - 1].Cell;
                        Assert.AreEqual(1, Mathf.Abs(delta.x) + Mathf.Abs(delta.y), $"{face} {dir}: order broken at {k}");
                    }
                }
            }
        }

        /// <summary>The k-th cell (in +x or +y order) on the edge a direction exits through.</summary>
        private static Vector2Int EdgeCell(Facing dir, int k, int n)
        {
            switch (dir)
            {
                case Facing.East: return new Vector2Int(n - 1, k);
                case Facing.West: return new Vector2Int(0, k);
                case Facing.North: return new Vector2Int(k, n - 1);
                default: return new Vector2Int(k, 0);
            }
        }

        [Test]
        public void WalkingStraightAroundTheCube_ReturnsToStart([ValueSource(nameof(FaceSizes))] int n)
        {
            foreach (FaceId face in Faces)
            {
                foreach (Facing start in Facings)
                {
                    var here = new ScreenAddress(face, 0, n - 1);
                    Facing facing = start;
                    for (int i = 0; i < 4 * n; i++) here = CubeModel.Step(n, here, facing, out facing);
                    Assert.AreEqual(new ScreenAddress(face, 0, n - 1), here, $"{face} heading {start}");
                    Assert.AreEqual(start, facing, $"{face} heading {start}");
                }
            }
        }

        [Test]
        public void KnownCrossingsFromTown()
        {
            const int n = 2;
            Assert.AreEqual(new ScreenAddress(FaceId.Right, 0, 1), CubeModel.Step(n, new ScreenAddress(FaceId.Front, 1, 1), Facing.East, out Facing f));
            Assert.AreEqual(Facing.East, f);
            Assert.AreEqual(new ScreenAddress(FaceId.Top, 1, 0), CubeModel.Step(n, new ScreenAddress(FaceId.Front, 1, 1), Facing.North, out f));
            Assert.AreEqual(Facing.North, f);
            Assert.AreEqual(new ScreenAddress(FaceId.Left, 1, 0), CubeModel.Step(n, new ScreenAddress(FaceId.Front, 0, 0), Facing.West, out f));
            Assert.AreEqual(Facing.West, f);
            Assert.AreEqual(new ScreenAddress(FaceId.Bottom, 0, 1), CubeModel.Step(n, new ScreenAddress(FaceId.Front, 0, 0), Facing.South, out f));
            Assert.AreEqual(Facing.South, f);
        }

        [Test]
        public void MapAlongEdge_AgreesWithStepAtCellCentres([ValueSource(nameof(FaceSizes))] int n)
        {
            foreach (FaceId face in Faces)
            {
                foreach (Facing dir in Facings)
                {
                    for (int k = 0; k < n; k++)
                    {
                        float t = (k + 0.5f) / n;
                        float t2 = CubeModel.MapAlongEdge(face, dir, t, out FaceId toFace, out Facing facing);
                        ScreenAddress next = CubeModel.Step(n, new ScreenAddress(face, EdgeCell(dir, k, n)), dir, out Facing stepFacing);
                        Assert.AreEqual(next.Face, toFace);
                        Assert.AreEqual(stepFacing, facing);
                        int along = facing.IsHorizontal() ? next.Cell.y : next.Cell.x;
                        Assert.AreEqual(along, Mathf.FloorToInt(t2 * n), $"{face} {dir} k={k}");
                    }
                }
            }
        }

        // ---------- Sealing and reachability ----------

        [Test]
        public void SealedBoundary_RefusesTheMove([ValueSource(nameof(FaceSizes))] int n)
        {
            int refused = 0;
            for (int seed = 1; seed <= 50; seed++)
            {
                // Nothing is sealed in play since Earth was built; the path is kept for faces sealed on purpose.
                var m = new CubeModel(seed, n, new[] { Theme.EarthAtmosphere });
                foreach (ScreenAddress from in m.AllScreens().Where(s => !m.IsSealed(s.Face)))
                {
                    foreach (Facing dir in Facings)
                    {
                        ScreenAddress geometric = CubeModel.Step(n, from, dir, out Facing geometricFacing);
                        bool ok = m.TryStep(from, dir, out ScreenAddress next, out Facing facing);
                        if (m.IsSealed(geometric.Face))
                        {
                            refused++;
                            Assert.IsFalse(ok, $"seed {seed}: {from} {dir} entered sealed {geometric.Face}");
                            Assert.AreEqual(from, next, "A refused move must leave the player in place");
                            Assert.AreEqual(dir, facing);
                        }
                        else
                        {
                            Assert.IsTrue(ok);
                            Assert.AreEqual(geometric, next);
                            Assert.AreEqual(geometricFacing, facing);
                        }
                    }
                }
            }
            Assert.Greater(refused, 0, "Expected some moves into sealed faces");
        }

        [Test]
        public void SeedSweep_EveryUnsealedScreenIsReachable([ValueSource(nameof(FaceSizes))] int n)
        {
            List<SeedSweep.SeedResult> results = SeedSweep.Run(1, 50, n);
            Assert.AreEqual(50, results.Count);
            Assert.IsTrue(results.All(r => r.Passed), SeedSweep.Describe(results));

            // Sanity: the sweep covers every screen (all 6 faces since Earth was built).
            var m = new CubeModel(1, n);
            Assert.AreEqual(6 * n * n, m.AllScreens().Count(s => !m.IsSealed(s.Face)));
        }

        [Test]
        public void SeedSweep_DescribeNamesFailingSeeds()
        {
            var results = new List<SeedSweep.SeedResult>
            {
                new SeedSweep.SeedResult { Seed = 3, Unreachable = new List<ScreenAddress>() },
                new SeedSweep.SeedResult { Seed = 17, Unreachable = new List<ScreenAddress> { new ScreenAddress(FaceId.Back, 1, 0) } },
            };
            string report = SeedSweep.Describe(results);
            StringAssert.Contains("Seed 17", report);
            StringAssert.Contains("Back (1,0)", report);
            StringAssert.DoesNotContain("Seed 3:", report);
        }
    }
}
