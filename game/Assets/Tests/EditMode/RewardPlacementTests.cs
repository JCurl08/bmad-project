using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Roadblock rewards (4.7, RewardPlacement): over seeds 1-50 every gate, optional gate and hidden spot hides a reward,
    /// nothing lies loose, no screen is empty, every reward kind appears, every face has its fragment slots 1-6, key and
    /// boost, and required rewards are reachable; the placement is deterministic, leaves the item and hidden-currency
    /// draws alone, and the sweep flags hand-broken placements. Also the run's keys, boosts and cash-in (RunRewards,
    /// RunWallet) and the puzzle-goal hook for 4.10-4.12.
    /// </summary>
    public class RewardPlacementTests
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
            public RewardPlacement Rewards;
            public MathPlan Math;
        }

        private static Run Make(int seed, IReadOnlyList<PuzzleGoal> puzzles = null)
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            var run = new Run { Model = new CubeModel(seed) };
            run.Layout = CubeLayout.Generate(run.Model, library);
            run.Items = ItemPlacement.ForRun(run.Model, run.Layout, library, catalog.Themes(), catalog.VariantCounts());
            run.Hidden = HiddenCurrencyPlacement.Generate(run.Model, run.Layout, library);
            run.Rewards = RewardPlacement.Generate(run.Model, run.Layout, run.Items, run.Hidden, library, puzzles);
            run.Math = MathPlan.Create(run.Model, run.Items, SeedSweep.ModuleLookup(run.Layout, library));
            return run;
        }

        private static List<string> Problems(Run run, RewardPlacement rewards = null, IReadOnlyList<PuzzleGoal> puzzles = null) =>
            RewardPlacement.FindProblems(run.Model, run.Layout, run.Items, run.Hidden, rewards ?? run.Rewards, Library(),
                run.Math != null ? run.Math.IslandAt : (System.Func<ScreenAddress, IslandLayout>)null, puzzles);

        /// <summary>
        /// The arch keys a face must hide (4.9), from its pool's size in the placement: the pool is every required reward of the
        /// face that is not an item or hidden currency. It takes the 6 fragment slots, then a key, the boost, and the second key, so
        /// a pool of 7-8 has 1 key and a pool of 9 or more has KeysPerFace (2).
        /// </summary>
        internal static int ExpectedKeys(RewardPlacement rewards, FaceId face)
        {
            int pool = rewards.Spots.Count(s => s.Face == face && s.Required && s.What.Kind != RewardKind.Equipment && s.HiddenIndex < 0);
            int afterSlots = pool - RewardPlacement.FragmentSlotsPerFace;
            if (afterSlots <= 0) return 0;
            return afterSlots <= RewardPlacement.UpgradesPerFace ? 1 : System.Math.Min(RewardPlacement.KeysPerFace, afterSlots - RewardPlacement.UpgradesPerFace);
        }

        // ---------- Seeds 1-50 ----------

        [Test]
        public void Sweep_Seeds1To50_EveryRoadblockHidesAReward_NoEmptyScreen_EveryKindAppears()
        {
            ItemCatalog catalog = Catalog();
            List<SeedSweep.SeedResult> results = SeedSweep.Run(1, 50, CubeSettings.DefaultFaceSize, Library(), catalog.Themes(),
                catalog.VariantCounts(), BiologyBeaks.VariantKinds(catalog), ChemistryIsotope.VariantKinds(catalog),
                StormcallerItem.VariantKinds(catalog));
            Assert.IsTrue(results.All(r => r.RewardsChecked), "The library knows hidden slots, so rewards are checked");
            Assert.IsTrue(results.All(r => r.Passed), SeedSweep.Describe(results));
            var kinds = new HashSet<RewardKind>();
            foreach (SeedSweep.SeedResult r in results)
            {
                Assert.IsEmpty(r.RewardProblems, $"seed {r.Seed}");
                foreach (RewardSpot s in r.Rewards.Spots) kinds.Add(s.What.Kind);
                // Every seed hides every kind, required: the fragments, a key, a boost, items and Jumbles*.
                foreach (RewardKind kind in new[] { RewardKind.Jumbles, RewardKind.Equipment, RewardKind.Upgrade, RewardKind.Key,
                             RewardKind.FragmentSlot, RewardKind.ChestKey })
                    Assert.Greater(r.Rewards.Count(kind, true), 0, $"seed {r.Seed}: no required {kind}");
            }
            Assert.AreEqual(6, kinds.Count, "Every reward kind appears across the seeds (chest keys since 4.9)");
            StringAssert.Contains("every roadblock hiding a reward", SeedSweep.Describe(results));
        }

        [Test]
        public void EveryGateOptionalGateAndHiddenSpot_HidesOneReward_AndNoPickupLiesLoose()
        {
            for (int seed = 1; seed <= 50; seed++)
            {
                Run run = Make(seed);
                foreach (GatePlacement g in run.Items.Gates)
                    Assert.IsTrue(run.Rewards.TryGet(new Roadblock(RoadblockKind.Gate, g.Screen, g.Slot), out RewardSpot s) && s.Required,
                        $"seed {seed}: {g}");
                foreach (OptionalGatePlacement g in run.Items.OptionalGates)
                {
                    Assert.IsTrue(run.Rewards.TryGet(new Roadblock(RoadblockKind.OptionalGate, g.Screen, g.Slot), out RewardSpot s), $"seed {seed}: {g}");
                    Assert.AreEqual(RewardKind.Jumbles, s.What.Kind, "An optional gate hides only a bonus");
                    Assert.IsFalse(s.Required, "Its variant is not this run's: never required");
                }
                for (int i = 0; i < run.Hidden.Spots.Count; i++)
                {
                    HiddenCurrencySpot h = run.Hidden.Spots[i];
                    Assert.IsTrue(run.Rewards.TryGet(new Roadblock(RoadblockKind.Mound, h.Screen, h.Slot), out RewardSpot s), $"seed {seed}: {h}");
                    Assert.AreEqual(i, s.HiddenIndex);
                }
                foreach (PickupPlacement p in run.Items.Pickups)
                {
                    Assert.IsTrue(run.Rewards.TryGet(RewardPlacement.RoadblockOf(p), out RewardSpot s), $"seed {seed}: {p} lies loose");
                    Assert.AreEqual(RewardKind.Equipment, s.What.Kind);
                    Assert.AreEqual(p.Item, s.What.Item);
                    if (!p.IsGuarded) Assert.IsTrue(s.Where.IsLaneSpot, "An open pickup is under a mound on its lane spot");
                }
                Assert.AreEqual(run.Rewards.Spots.Count, run.Rewards.Spots.Select(s => s.Where).Distinct().Count(), "One reward a roadblock");
                for (int i = 0; i < run.Rewards.Spots.Count; i++) Assert.AreEqual(i, run.Rewards.Spots[i].Index);
            }
        }

        [Test]
        public void EveryFace_NumberedK_HoldsFragments1ToK_AKey_AndABoost()
        {
            for (int seed = 1; seed <= 50; seed++)
            {
                Run run = Make(seed);
                DieNumbers dice = DieNumbers.Roll(run.Model);
                foreach (FaceId face in run.Layout.Faces.Keys)
                {
                    List<RewardSpot> slots = run.Rewards.FragmentSlots(face);
                    CollectionAssert.AreEqual(Enumerable.Range(1, dice.Of(face)), slots.Select(s => s.What.Ordinal),
                        $"seed {seed} {face}");
                    Assert.IsTrue(slots.All(s => s.Required), "Required");
                    Assert.AreEqual(ExpectedKeys(run.Rewards, face), run.Rewards.Spots.Count(s => s.Face == face && s.What.Kind == RewardKind.Key), $"seed {seed} {face}: 4.9 keys");
                    Assert.AreEqual(RewardPlacement.UpgradesPerFace, run.Rewards.Spots.Count(s => s.Face == face && s.What.Kind == RewardKind.Upgrade));
                    Assert.IsFalse(run.Rewards.Spots.Any(s => TownSquare.IsTown(s.Where.Screen, run.Model.FaceSize)), "Nothing in a town");
                }
            }
        }

        [Test]
        public void SameSeed_SameRewards_AndTheItemAndHiddenDrawsAreUnchanged()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            for (int seed = 1; seed <= 20; seed++)
            {
                Run a = Make(seed), b = Make(seed);
                Assert.AreEqual(a.Rewards.Signature(), b.Rewards.Signature(), $"seed {seed}");
                // The rewards draw only on their own stream: the item and hidden-currency placements are what they were.
                Assert.AreEqual(ItemPlacement.ForRun(a.Model, a.Layout, library, catalog.Themes(), catalog.VariantCounts()).Signature(),
                    a.Items.Signature());
                Assert.AreEqual(HiddenCurrencyPlacement.Generate(a.Model, a.Layout, library).Signature(), a.Hidden.Signature());
            }
            Assert.AreNotEqual(Make(1).Rewards.Signature(), Make(2).Rewards.Signature());
        }

        [Test]
        public void TheSweep_FlagsALooseItem_ARewardOutsideARoadblock_AnEmptyScreen_AndAnUnreachableReward()
        {
            Run run = Make(7);
            Assert.IsEmpty(Problems(run));
            List<RewardSpot> spots = run.Rewards.Spots.ToList();

            // An item whose reward is gone lies loose; its roadblock (and maybe its screen) is left empty too.
            int item = spots.FindIndex(s => s.What.Kind == RewardKind.Equipment);
            var loose = new RewardPlacement(7, spots.Where((s, i) => i != item));
            Assert.IsTrue(Problems(run, loose).Any(p => p.Contains("lies loose")), string.Join("; ", Problems(run, loose)));

            // A chest on a gate slot with no gate is a reward outside a roadblock.
            ScreenAddress screen = default;
            int slot = -1;
            foreach (FaceLayout fl in run.Layout.Faces.Values)
            {
                for (int y = 0; y < 3 && slot < 0; y++)
                    for (int x = 0; x < 3 && slot < 0; x++)
                    {
                        var s = new ScreenAddress(fl.Face, x, y);
                        int count = Library().GateSlotCount(fl.Theme, fl.ModuleAt(s.Cell));
                        for (int k = 0; k < count && slot < 0; k++)
                            if (!run.Items.TryGetGate(s, k, out _) && !run.Items.TryGetOptionalGate(s, k, out _))
                            {
                                screen = s;
                                slot = k;
                            }
                    }
                if (slot >= 0) break;
            }
            Assert.GreaterOrEqual(slot, 0, "Some gate slot is free on seed 7");
            var stray = new RewardPlacement(7, spots.Append(new RewardSpot(spots.Count, new Roadblock(RoadblockKind.Gate, screen, slot),
                Reward.Jumbles(5), true)));
            Assert.IsTrue(Problems(run, stray).Any(p => p.Contains("outside a roadblock")));

            // Taking every reward off a screen leaves it empty.
            ScreenAddress some = spots.First(s => s.Where.Kind == RoadblockKind.Mound && s.HiddenIndex < 0).Where.Screen;
            var bare = new RewardPlacement(7, spots.Where(s => s.Where.Screen != some));
            Assert.IsTrue(Problems(run, bare).Any(p => p.Contains("is empty")), string.Join("; ", Problems(run, bare)));

            // A required reward behind a gate whose item cannot be collected is unreachable.
            GatePlacement own = run.Items.Gates.First();
            PickupPlacement pickup = run.Items.Pickups.First(p => p.Item == own.Item);
            var stuckItems = new ItemPlacement(7, run.Items.Gates,
                run.Items.Pickups.Select(p => p.Item == own.Item ? new PickupPlacement(p.Item, own.Screen, own.Slot) : p),
                run.Items.OptionalGates, run.Items.RolledVariants.ToDictionary(e => e.Key, e => e.Value));
            var stuck = new Run { Model = run.Model, Layout = run.Layout, Items = stuckItems, Hidden = run.Hidden, Math = run.Math };
            stuck.Rewards = RewardPlacement.Generate(stuck.Model, stuck.Layout, stuckItems, stuck.Hidden, Library());
            Assert.IsTrue(Problems(stuck).Any(p => p.Contains("can never be collected")), $"{pickup}: " + string.Join("; ", Problems(stuck)));
        }

        [Test]
        public void TheSweep_FlagsARequiredRewardItCanNeverReach()
        {
            // A puzzle goal standing in the water of an island screen: no walk ever gets there.
            for (int seed = 1; seed <= 50; seed++)
            {
                Run plain = Make(seed);
                if (plain.Math == null || plain.Math.IslandScreens.Count == 0) continue;
                ScreenAddress screen = plain.Math.IslandScreens[0].Screen;
                var reach = new DoorwayReach(plain.Model, plain.Math.IslandAt);
                Vector2? water = null;
                for (float x = -10f; x <= 10f && water == null; x += 0.5f)
                    for (float y = -6f; y <= 6f && water == null; y += 0.5f)
                        if (reach.RegionAt(screen, new Vector2(x, y)) < 0) water = new Vector2(x, y);
                if (water == null) continue;
                var goals = new List<PuzzleGoal> { new PuzzleGoal(screen, water.Value) };
                Run run = Make(seed, goals);
                List<string> problems = Problems(run, null, goals);
                Assert.IsTrue(problems.Any(p => p.Contains("can never be reached")), string.Join("; ", problems));
                return;
            }
            Assert.Fail("No seed has an island screen with water");
        }

        [Test]
        public void EachFace_DrawsOnItsOwnSubStream_SoAPuzzleGoalOnOneFaceLeavesTheOthersAlone()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                Run plain = Make(seed);
                FaceLayout bio = plain.Layout.Faces.Values.First(f => f.Theme == Theme.Biology);
                var goals = new List<PuzzleGoal> { new PuzzleGoal(new ScreenAddress(bio.Face, 0, 0), Vector2.zero) };
                Run withGoal = Make(seed, goals);
                foreach (FaceId face in plain.Layout.Faces.Keys)
                {
                    if (face == bio.Face) continue;
                    string Of(Run r) => string.Join(" ", r.Rewards.Spots.Where(s => s.Face == face).Select(s => $"{s.Where}={s.What}"));
                    Assert.AreEqual(Of(plain), Of(withGoal), $"seed {seed}: {face} is unchanged");
                }
            }
            Assert.AreNotEqual(RewardPlacement.FaceStream(FaceId.Top), RewardPlacement.FaceStream(FaceId.Left));
            foreach (FaceId f in System.Enum.GetValues(typeof(FaceId)))
                Assert.Greater(RewardPlacement.FaceStream(f), 64UL, "Far above every registered stream");
        }

        [Test]
        public void NoBoulderStarts_OnAMoundOrInAChestsAlcove()
        {
            ModuleLibrary library = Library();
            ItemCatalog catalog = Catalog();
            for (int seed = 1; seed <= 50; seed++)
            {
                Run run = Make(seed);
                System.Func<ScreenAddress, ScreenModule> moduleAt = SeedSweep.ModuleLookup(run.Layout, library);
                PhysicsPlan plan = PhysicsPlan.Create(run.Model, run.Items, moduleAt, BiologyBeaks.VariantKinds(catalog),
                    PhysicsPlan.DefaultMaxRequired, ChemistryIsotope.VariantKinds(catalog));
                if (plan == null) continue;
                var starts = plan.Doors.SelectMany(d => d.Boulders.Select(b => (d.Screen, b))).ToList();
                if (plan.Trial != null) starts.AddRange(plan.Trial.Boulders.Select(b => (plan.Trial.Screen, b)));
                foreach ((ScreenAddress screen, Vector2 at) in starts)
                {
                    ScreenModule module = moduleAt(screen);
                    foreach (RewardSpot s in run.Rewards.Spots.Where(s => s.Where.Screen == screen))
                    {
                        Vector2 spot;
                        if (s.Where.IsLaneSpot) spot = CubeWorld.OpenPickupOffset;
                        else if (s.Where.Kind == RoadblockKind.Mound)
                            spot = (Vector2)module.HiddenItems[s.Where.Slot].transform.position - (Vector2)module.transform.position;
                        else if (s.Where.Kind == RoadblockKind.Gate || s.Where.Kind == RoadblockKind.OptionalGate)
                            spot = module.Gates[s.Where.Slot].PocketCentre - (Vector2)module.transform.position;
                        else continue;
                        Assert.GreaterOrEqual(Vector2.Distance(at, spot), Boulder.Radius, $"seed {seed}: a boulder starts on {s}");
                    }
                }
            }
        }

        [Test]
        public void PuzzleGoals_JoinTheirFacesPool_AndTheSweepAcceptsThem()
        {
            Run plain = Make(11);
            FaceLayout fl = plain.Layout.Faces.Values.First(f => f.Theme == Theme.Biology);
            var goals = new List<PuzzleGoal> { new PuzzleGoal(new ScreenAddress(fl.Face, 0, 0), Vector2.zero) };
            Run run = Make(11, goals);
            Assert.IsTrue(run.Rewards.TryGet(new Roadblock(RoadblockKind.Puzzle, goals[0].Screen, 0), out RewardSpot spot), "The goal hides a reward");
            Assert.IsTrue(spot.Required);
            Assert.IsEmpty(Problems(run, null, goals));
            Assert.IsTrue(Problems(run).Any(p => p.Contains("no such puzzle goal")), "Without the goals, the spot has no roadblock");
        }

        // ---------- The run's keys, boosts and Jumbles* ----------

        [Test]
        public void RunRewards_KeysAndBoosts_ChangeTheRun_AndAResetTakesThemBack()
        {
            var go = new GameObject("Player");
            try
            {
                go.AddComponent<Health>();
                PlayerStats stats = go.AddComponent<PlayerStats>();
                RunRewards rewards = go.AddComponent<RunRewards>();
                int heart = stats.MaxHealth, speed = stats.Speed, power = stats.Power;
                Assert.IsNull(rewards.HudText, "Nothing to show yet");

                Assert.IsTrue(rewards.Grant(Reward.Key(), FaceId.Top));
                Assert.IsTrue(rewards.Grant(Reward.Key(), FaceId.Left));
                Assert.IsTrue(rewards.Grant(Reward.Boost(RunUpgrade.Heart), FaceId.Top));
                Assert.IsTrue(rewards.Grant(Reward.Boost(RunUpgrade.Speed), FaceId.Top));
                Assert.IsTrue(rewards.Grant(Reward.Boost(RunUpgrade.Power), FaceId.Top));
                Assert.IsTrue(rewards.Grant(Reward.Fragment(3), FaceId.Back));
                Assert.IsFalse(rewards.Grant(Reward.Fragment(3), FaceId.Back), "A fragment is held once");
                Assert.AreEqual(1, rewards.FragmentsOf(FaceId.Back));
                Assert.IsTrue(rewards.HasFragment(FaceId.Back, 3));
                Assert.IsFalse(rewards.Grant(Reward.Jumbles(5), FaceId.Top), "Jumbles* go to the wallet");
                Assert.AreEqual(2, rewards.Keys);
                Assert.AreEqual((heart + 1, speed + 1, power + 1), (stats.MaxHealth, stats.Speed, stats.Power));
                CollectionAssert.AreEqual(new[] { (FaceId.Back, 3) }, rewards.Fragments);
                Assert.AreEqual("Keys: 2 | Run boosts: +1 heart, +speed, +power", rewards.HudText);
                Assert.IsEmpty(InWorldText.Problems(rewards.HudText));

                Assert.IsTrue(rewards.TrySpendKey(), "4.9's locks spend a key");
                Assert.AreEqual(1, rewards.CashInKeys(), "The run's end cashes in the rest");
                Assert.AreEqual(0, rewards.Keys);
                Assert.IsFalse(rewards.TrySpendKey());

                rewards.ResetRun();
                Assert.AreEqual((heart, speed, power), (stats.MaxHealth, stats.Speed, stats.Power), "A new run takes the boosts back");
                Assert.IsEmpty(rewards.Upgrades);
                Assert.IsEmpty(rewards.Fragments);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RunWallet_PaysTheKeysCashIn_AfterTheRunEnded_ButNeverOnceBanked()
        {
            var go = new GameObject("Wallet");
            try
            {
                RunWallet wallet = go.AddComponent<RunWallet>();
                RunState state = go.AddComponent<RunState>();
                wallet.Configure(null, null, state);
                int popups = 0, atEnd = 0;
                wallet.Earned += (_, __) => popups++;
                wallet.EarnedAtRunEnd += (_, __) => atEnd++;
                state.EndRun(false);
                Assert.IsFalse(wallet.Add(EarningSource.Chest, "chest:0", 5), "Nothing pays after the run ended");
                Assert.IsTrue(wallet.AddAtRunEnd(EarningSource.Keys, "key:0", RewardPlacement.KeyCashIn));
                Assert.IsTrue(wallet.AddAtRunEnd(EarningSource.Keys, "key:1", RewardPlacement.KeyCashIn));
                Assert.IsFalse(wallet.AddAtRunEnd(EarningSource.Keys, "key:1", RewardPlacement.KeyCashIn), "Once a key");
                Assert.AreEqual(0, popups, "No pickup popup once the run is over");
                Assert.AreEqual(2, atEnd, "The run's end pay has its own event");
                var save = new MetaSave();
                Assert.AreEqual(2 * RewardPlacement.KeyCashIn, wallet.Bank(save));
                Assert.AreEqual(2 * RewardPlacement.KeyCashIn, save.Balance);
                Assert.IsFalse(wallet.AddAtRunEnd(EarningSource.Keys, "key:2", RewardPlacement.KeyCashIn), "Never once banked");
                List<string> lines = BetweenRunsScreen.EarningsLinesFor(wallet.Earnings, 10, 10);
                CollectionAssert.Contains(lines, RewardText.KeysLine(2, 2 * RewardPlacement.KeyCashIn));
                CollectionAssert.Contains(lines, RewardText.ChestsLine(0, 0));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RewardText_KeepsTheTextRule()
        {
            foreach (string line in RewardText.All())
                Assert.IsEmpty(InWorldText.Problems(line), line);
            Assert.AreEqual("Fragment 3 of 5 from the Greenwood*", RewardText.FragmentPopup(Theme.Biology, 3, 5));
            Assert.AreEqual("Fragment 3 of 5 from the Greenwood*", RewardChest.PopupText(Reward.Fragment(3), Theme.Biology, 5));
            Assert.AreEqual("The Greenwood* is mended!", RewardText.RepairPopup(Theme.Biology));
            Assert.AreEqual("Town is mended!", RewardText.RepairPopup(Theme.Town));
            foreach (RunUpgrade u in new[] { RunUpgrade.Heart, RunUpgrade.Speed, RunUpgrade.Power })
                Assert.IsNotNull(RewardChest.PopupText(Reward.Boost(u), Theme.Math));
            Assert.IsNull(RewardChest.PopupText(Reward.Jumbles(5), Theme.Math), "A Jumbles* chest's popup is the wallet's");
        }
    }
}
