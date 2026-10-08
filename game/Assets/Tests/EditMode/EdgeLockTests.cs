using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Locked face-edge gates (4.9, EdgeLocks): the cube's 12 edges; over seeds 1-50 no order of spending keys can soft-lock
    /// the run (every screen, island, item, fragment, key and core portal stays reachable from Town) and at least two edges
    /// can be the first opened, with variety across seeds; the check flags hand-made soft-locks; the locks are deterministic
    /// on their own stream; a key lock takes a key and uses it up, an item lock opens with its item only (EdgeLockState);
    /// chest keys open bonus alcoves only and their pool never feeds the arches.
    /// </summary>
    public class EdgeLockTests
    {
        private const string LibraryPath = "Assets/Modules/ModuleLibrary.asset";
        private const string ItemCatalogPath = "Assets/Items/ItemCatalog.asset";

        private static ModuleLibrary Library() => AssetDatabase.LoadAssetAtPath<ModuleLibrary>(LibraryPath);
        private static ItemCatalog Catalog() => AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogPath);

        private static LockWorld World(int seed)
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            var model = new CubeModel(seed);
            CubeLayout layout = CubeLayout.Generate(model, library);
            ItemPlacement items = ItemPlacement.ForRun(model, layout, library, catalog.Themes(), catalog.VariantCounts());
            HiddenCurrencyPlacement hidden = HiddenCurrencyPlacement.Generate(model, layout, library);
            IcePlan ice = IcePlan.Create(model, layout); // 4.10: the ice goals, as ForRun makes them
            List<PuzzleGoal> goals = PuzzleScreens.Goals(ice);
            RewardPlacement rewards = RewardPlacement.Generate(model, layout, items, hidden, library, goals);
            MathPlan math = MathPlan.Create(model, items, SeedSweep.ModuleLookup(layout, library));
            return new LockWorld(model, layout, items, rewards, PuzzleScreens.RegionsAt(math, ice), goals);
        }

        [Test]
        public void CubeEdges_AreTwelve_FourPerFace_AndEachSideMapsBack()
        {
            IReadOnlyList<CubeEdge> all = CubeEdge.All;
            Assert.AreEqual(12, all.Count);
            for (int f = 0; f < CubeSettings.FaceCount; f++)
            {
                var face = (FaceId)f;
                Assert.AreEqual(4, all.Count(e => e.Touches(face)), $"{face}");
                for (int s = 0; s < 4; s++)
                {
                    CubeEdge e = CubeEdge.Of(face, (Facing)s);
                    Assert.AreEqual((Facing)s, e.SideOn(face));
                    Assert.AreEqual(e, CubeEdge.Of(CubeModel.NeighborFace(face, (Facing)s), e.SideOn(CubeModel.NeighborFace(face, (Facing)s))));
                }
            }
        }

        [Test]
        public void Seeds1To50_NoSpendingOrderSoftLocks_AndSeveralFirstEdges()
        {
            var firstCounts = new Dictionary<int, int>();
            var firstFaces = new Dictionary<Theme, int>();
            var orders = new HashSet<string>();
            int keyLocks = 0, itemLocks = 0, states = 0, maxDraw = 0, draws = 0;
            var failures = new List<string>();
            for (int seed = 1; seed <= 50; seed++)
            {
                LockWorld world = World(seed);
                EdgeLocks locks = EdgeLocks.Generate(world, out LockReport report);
                if (!report.Passed) failures.Add($"seed {seed}: {string.Join("; ", report.Problems)}");
                if (locks.FellBack) failures.Add($"seed {seed} fell back");
                if (locks.Locks.Count != 12 || locks.Locks.Any(l => l.Kind == LockKind.Open)) failures.Add($"seed {seed}: not every face edge starts locked");
                if (report.FirstEdges.Count < EdgeLocks.MinFirstEdges) failures.Add($"seed {seed}: {report.FirstEdges.Count} first edges");
                firstCounts.TryGetValue(report.FirstEdges.Count, out int c);
                firstCounts[report.FirstEdges.Count] = c + 1;
                foreach (CubeEdge e in report.FirstEdges)
                {
                    Theme t = world.Model.ThemeOf(e.A == CubeModel.StartFace ? e.B : e.A);
                    firstFaces.TryGetValue(t, out int n);
                    firstFaces[t] = n + 1;
                }
                orders.Add(string.Join(",", report.FirstEdges.Select(e => world.Model.ThemeOf(e.A == CubeModel.StartFace ? e.B : e.A)).OrderBy(t => t)));
                keyLocks += locks.Count(LockKind.Key);
                itemLocks += locks.Count(LockKind.Item);
                states += report.States;
                draws += locks.Attempts;
                maxDraw = Mathf.Max(maxDraw, locks.Attempts);
            }
            Debug.Log($"Locks over 50 seeds: {keyLocks} key, {itemLocks} item; first edges per seed " +
                      string.Join(", ", firstCounts.OrderBy(e => e.Key).Select(e => $"{e.Key}:{e.Value}")) + "; first regions " +
                      string.Join(", ", firstFaces.OrderBy(e => e.Key).Select(e => $"{e.Key} {e.Value}")) +
                      $"; {orders.Count} different first-edge sets; {states} spending states; draws {draws} (max {maxDraw}).");
            Assert.IsEmpty(failures, string.Join("\n", failures));
            Assert.AreEqual(5, firstFaces.Count, "Every region is a first route on some seed");
            Assert.GreaterOrEqual(orders.Count, 10, "The first routes vary by seed");
        }

        [Test]
        public void Locks_AreDeterministic_AndOnTheirOwnStream()
        {
            Assert.AreEqual(25UL, EdgeLocks.RngStream);
            for (int seed = 1; seed <= 5; seed++)
                Assert.AreEqual(EdgeLocks.Generate(World(seed)).Signature(), EdgeLocks.Generate(World(seed)).Signature());
            Assert.AreNotEqual(EdgeLocks.Generate(World(1)).Signature(), EdgeLocks.Generate(World(2)).Signature());
            ItemCatalog catalog = Catalog();
            Assert.AreEqual(EdgeLocks.Generate(World(7)).Signature(),
                EdgeLocks.ForRun(new CubeModel(7), Library(), catalog.Themes(), catalog.VariantCounts()).Signature(),
                "The game's ForRun makes the sweep's locks");
        }

        /// <summary>
        /// The rebuild cost (4.9 review): CubeWorld.Rebuild runs EdgeLocks.ForRun (layout, items, rewards, islands and the
        /// lock check). Measured on seeds 1-50 after a warm-up; logged, and kept well under a frame budget's worth of hitch.
        /// </summary>
        [Test]
        public void ForRun_OnSeeds1To50_IsQuickEnoughForARebuild()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            EdgeLocks.ForRun(new CubeModel(1), library, catalog.Themes(), catalog.VariantCounts()); // warm-up (JIT, asset loads)
            var times = new List<double>();
            for (int seed = 1; seed <= 50; seed++)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                EdgeLocks.ForRun(new CubeModel(seed), library, catalog.Themes(), catalog.VariantCounts());
                watch.Stop();
                times.Add(watch.Elapsed.TotalMilliseconds);
            }
            Debug.Log($"EdgeLocks.ForRun over seeds 1-50: max {times.Max():0.0} ms, mean {times.Average():0.0} ms, " +
                      $"median {times.OrderBy(t => t).ElementAt(25):0.0} ms.");
            Assert.Less(times.Max(), 250.0, "A rebuild's lock work stays a short hitch");
        }

        [Test]
        public void Check_FlagsASoftLock_AndTooFewFirstEdges()
        {
            LockWorld world = World(7);
            // Every edge a key lock: one starting key and five found can never open enough of twelve in every order.
            var allKeys = new EdgeLocks(7, CubeEdge.All.Select(e => new EdgeLock(e, LockKind.Key)));
            LockReport report = EdgeLocks.Check(world, allKeys);
            Assert.IsTrue(report.Problems.Any(p => p.StartsWith("soft-lock")), string.Join("; ", report.Problems));
            // Town's edges all item locks: nothing opens first (Town holds no item).
            var itemTown = new EdgeLocks(7, CubeEdge.All.Select(e =>
                new EdgeLock(e, e.Touches(CubeModel.StartFace) ? LockKind.Item : LockKind.Key, Theme.Biology)));
            report = EdgeLocks.Check(world, itemTown);
            Assert.IsTrue(report.Problems.Any(p => p.StartsWith("only 0 edge")), string.Join("; ", report.Problems));
            // One key lock out of Town, every other edge open: only one first edge.
            CubeEdge only = CubeEdge.Of(CubeModel.StartFace, Facing.East);
            var one = new EdgeLocks(7, CubeEdge.All.Select(e => new EdgeLock(e,
                e == only ? LockKind.Key : e.Touches(CubeModel.StartFace) ? LockKind.Item : LockKind.Open, Theme.Math)));
            report = EdgeLocks.Check(world, one);
            Assert.IsTrue(report.Problems.Any(p => p.StartsWith("only 1 edge")), string.Join("; ", report.Problems));
        }

        [Test]
        public void LockState_AKeyLockTakesAKey_AndUsesItUp_AnItemLockOpensWithItsItemOnly()
        {
            CubeEdge keyEdge = CubeEdge.Of(CubeModel.StartFace, Facing.East);
            CubeEdge itemEdge = CubeEdge.Of(CubeModel.StartFace, Facing.North);
            var locks = new EdgeLocks(1, new[]
            {
                new EdgeLock(keyEdge, LockKind.Key), new EdgeLock(itemEdge, LockKind.Item, Theme.Physics),
            });
            var state = new EdgeLockState(locks);
            Assert.AreEqual(EdgeLocks.StartKeys, state.StartKeysLeft);
            Assert.IsTrue(state.IsLocked(keyEdge));
            Assert.IsTrue(state.IsLocked(CubeModel.StartFace, Facing.North));
            Assert.IsFalse(state.IsLocked(CubeEdge.Of(CubeModel.StartFace, Facing.South)), "An edge with no lock is open");

            int keys = 1;
            bool Spend()
            {
                if (keys <= 0) return false;
                keys--;
                return true;
            }
            // The item lock: only its item.
            Assert.AreEqual(LockOpenResult.NeedsItem, state.TryUnlock(itemEdge, Spend, t => t == Theme.Biology));
            Assert.IsTrue(state.IsLocked(itemEdge));
            Assert.AreEqual(1, keys, "A key never opens an item lock");
            Assert.AreEqual(LockOpenResult.Opened, state.TryUnlock(itemEdge, Spend, t => t == Theme.Physics));
            Assert.IsFalse(state.IsLocked(itemEdge));
            Assert.AreEqual(LockOpenResult.AlreadyOpen, state.TryUnlock(itemEdge, Spend, t => t == Theme.Physics));
            // The key lock: the starting key first, then a found one; used up.
            var opened = new List<CubeEdge>();
            state.Opened += e => opened.Add(e);
            Assert.AreEqual(LockOpenResult.Opened, state.TryUnlock(keyEdge, Spend, t => true));
            Assert.AreEqual(0, state.StartKeysLeft, "The starting key is used up first");
            Assert.AreEqual(1, keys);
            CollectionAssert.AreEqual(new[] { keyEdge }, opened);
            Assert.AreEqual(LockOpenResult.AlreadyOpen, state.TryUnlock(keyEdge, Spend, t => true));
            Assert.AreEqual(1, keys, "An open arch takes nothing");

            var second = new EdgeLockState(locks);
            Assert.IsTrue(second.TryUseStartKey());
            Assert.AreEqual(LockOpenResult.Opened, second.TryUnlock(keyEdge, Spend, t => false));
            Assert.AreEqual(0, keys, "Then a found key, used up");
            var third = new EdgeLockState(locks);
            Assert.IsTrue(third.TryUseStartKey());
            Assert.AreEqual(LockOpenResult.NeedsKey, third.TryUnlock(keyEdge, Spend, t => true));
            Assert.IsTrue(third.IsLocked(keyEdge));
        }

        [Test]
        public void ChestKeys_AreTheirOwnPool_TenARun_AndOpenNoArch()
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                ModuleLibrary library = Library();
                ItemCatalog catalog = Catalog();
                var model = new CubeModel(seed);
                CubeLayout layout = CubeLayout.Generate(model, library);
                ItemPlacement items = ItemPlacement.ForRun(model, layout, library, catalog.Themes(), catalog.VariantCounts());
                RewardPlacement rewards = RewardPlacement.Generate(model, layout, items, HiddenCurrencyPlacement.Generate(model, layout, library), library);
                Assert.AreEqual(RewardPlacement.ChestKeysPerRun, rewards.Count(RewardKind.ChestKey), "Ten a run, in the fragment slots above each face's number");
                foreach (FaceId face in layout.Faces.Keys)
                {
                    Assert.AreEqual(RewardPlacementTests.ExpectedKeys(rewards, face), rewards.Spots.Count(s => s.Face == face && s.What.Kind == RewardKind.Key), $"seed {seed} {face}");
                }
                Assert.IsTrue(rewards.Spots.Where(s => s.Where.Kind == RoadblockKind.OptionalGate).All(s => !s.Required &&
                    s.What.Kind == RewardKind.Jumbles && s.What.Amount == RewardPlacement.OptionalJumbles), "Bonus alcoves hold 10 Jumbles*");
            }
            var go = new GameObject("player");
            try
            {
                var rewards = go.AddComponent<RunRewards>();
                Assert.IsTrue(rewards.Grant(Reward.ChestKey(), FaceId.Right));
                Assert.AreEqual(1, rewards.ChestKeys);
                Assert.AreEqual(0, rewards.Keys, "A chest key is not an arch key");
                Assert.IsFalse(rewards.TrySpendKey(), "So it opens no arch");
                Assert.IsTrue(rewards.TrySpendChestKey());
                Assert.IsFalse(rewards.TrySpendChestKey());
                Assert.IsTrue(rewards.Grant(Reward.ChestKey(), FaceId.Right));
                Assert.IsTrue(rewards.Grant(Reward.Key(), FaceId.Right));
                Assert.AreEqual(2, rewards.CashInKeys(), "Both kinds left at the run's end are cashed in");
                Assert.AreEqual(0, rewards.ChestKeys);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void LockTexts_KeepTheTextRules()
        {
            foreach (string text in LockText.All())
                Assert.IsEmpty(InWorldText.Problems(text), text);
            Assert.AreEqual("Keys: 1 | Chest keys: 2", RewardText.HudLine(1, 2, null, false));
            Assert.AreEqual("Keys: 0", RewardText.HudLine(0, 0, null, true), "With locked arches the key count always shows");
            Assert.IsNull(RewardText.HudLine(0, 0, null, false));
        }
    }
}
