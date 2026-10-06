using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Story 1.12's pure parts: the meta save (JSON round trip, PlayerPrefs round trip, old/unknown versions, extra fields,
    /// corrupt or missing saves), the upgrade cost curve and purchases, the stats a run starts with, the run earnings
    /// counting each source once, the hint density from the run count, the seed source, and the hidden-currency
    /// placement (determinism, own stream, count, slots, sweep rules and their failure reports).
    /// </summary>
    [IsolatedMetaSave]
    public class MetaRunTests
    {
        private const string LibraryPath = "Assets/Modules/ModuleLibrary.asset";

        private static ModuleLibrary Library()
        {
            var library = AssetDatabase.LoadAssetAtPath<ModuleLibrary>(LibraryPath);
            Assert.IsNotNull(library, $"{LibraryPath} missing: run Cube > Build Module Library");
            return library;
        }

        /// <summary>The library's module and hidden-slot shape without its gate slots.</summary>
        private sealed class HiddenOnlyCatalog : IModuleCatalog, IHiddenSlotCatalog
        {
            private readonly ModuleLibrary library;
            public HiddenOnlyCatalog(ModuleLibrary library) => this.library = library;
            public int PoolSize(Theme theme) => library.PoolSize(theme);
            public int CoreSlotCount(Theme theme, int moduleIndex) => library.CoreSlotCount(theme, moduleIndex);
            public int HiddenSlotCount(Theme theme, int moduleIndex) => library.HiddenSlotCount(theme, moduleIndex);
        }

        // ---------- Save ----------

        private static MetaSave Sample()
        {
            var save = new MetaSave { Balance = 135, RunCount = 4, Victories = 2 };
            save.SetUpgradeLevel(UpgradeShop.Id(UpgradeStat.Health), 2);
            save.SetUpgradeLevel(UpgradeShop.Id(UpgradeStat.Speed), 5);
            save.IncrementAbility("biology-thin");
            save.IncrementAbility("biology-thin");
            save.IncrementAbility("chemistry-item");
            return save;
        }

        private static void AssertSame(MetaSave expected, MetaSave actual)
        {
            Assert.AreEqual(expected.Balance, actual.Balance, "balance");
            Assert.AreEqual(expected.RunCount, actual.RunCount, "run count");
            Assert.AreEqual(expected.Victories, actual.Victories, "victories");
            foreach (UpgradeStat stat in UpgradeShop.All)
                Assert.AreEqual(UpgradeShop.Level(expected, stat), UpgradeShop.Level(actual, stat), stat.ToString());
            Assert.AreEqual(expected.AbilityCounts.Count, actual.AbilityCounts.Count, "ability entries");
            foreach (MetaSave.Entry e in expected.AbilityCounts)
                Assert.AreEqual(e.value, actual.AbilityCount(e.id), e.id);
        }

        [Test]
        public void Save_JsonRoundTrip_KeepsEverything()
        {
            MetaSave save = Sample();
            string json = save.ToJson();
            StringAssert.Contains("\"version\":1", json);
            MetaSave back = MetaSave.FromJson(json, out MetaLoadStatus status);
            Assert.AreEqual(MetaLoadStatus.Loaded, status);
            Assert.AreEqual(MetaSave.CurrentVersion, back.Version);
            AssertSame(save, back);
            Assert.AreEqual(2, back.AbilityCount("biology-thin"));
            Assert.AreEqual(0, back.AbilityCount("never-seen"));
        }

        [Test]
        public void Save_PlayerPrefsRoundTrip_UsesTheKey()
        {
            Assert.AreEqual(IsolatedMetaSaveAttribute.TestKey, MetaSave.PrefsKey, "Tests never touch the real save");
            MetaSave save = Sample();
            save.Save();
            Assert.IsTrue(PlayerPrefs.HasKey(MetaSave.PrefsKey));
            MetaSave back = MetaSave.Load(MetaSave.PrefsKey, out MetaLoadStatus status);
            Assert.AreEqual(MetaLoadStatus.Loaded, status);
            AssertSame(save, back);
        }

        [Test]
        public void Statics_ResetToTheRealKeyAndFreshSeeds()
        {
            Assert.AreEqual(IsolatedMetaSaveAttribute.TestKey, MetaSave.PrefsKey);
            Assert.IsTrue(RunLoop.KeepSceneSeed);
            MetaSave.ResetStatics();
            Assert.AreEqual(MetaSave.DefaultPrefsKey, MetaSave.PrefsKey, "Play start goes back to the real key");
            var after = new IsolatedMetaSaveAttribute();
            after.BeforeTest(null);
            after.AfterTest(null);
            Assert.AreEqual(MetaSave.DefaultPrefsKey, MetaSave.PrefsKey, "Restored after a test");
            Assert.IsFalse(RunLoop.KeepSceneSeed);
            after.BeforeTest(null); // back to isolation for the rest of this test
        }

        [Test]
        public void Save_Missing_StartsFresh()
        {
            MetaSave save = MetaSave.Load(MetaSave.PrefsKey, out MetaLoadStatus status);
            Assert.AreEqual(MetaLoadStatus.Fresh, status);
            Assert.AreEqual(0, save.Balance);
            Assert.AreEqual(0, save.RunCount);
            Assert.AreEqual(0, save.Victories);
            Assert.IsEmpty(save.Upgrades);
            Assert.IsEmpty(save.AbilityCounts);
            Assert.AreEqual(MetaSave.CurrentVersion, save.Version);
        }

        [TestCase("{not json at all")]
        [TestCase("garbage")]
        [TestCase("[1,2,3]")]
        [TestCase("{\"balance\": }")]
        public void Save_Corrupt_StartsFreshWithoutThrowing(string text)
        {
            PlayerPrefs.SetString(MetaSave.PrefsKey, text);
            MetaSave save = null;
            MetaLoadStatus status = MetaLoadStatus.Loaded;
            LogAssert_IgnoreWarnings(() => save = MetaSave.Load(MetaSave.PrefsKey, out status));
            Assert.AreEqual(MetaLoadStatus.Corrupt, status);
            Assert.AreEqual(0, save.Balance);
            Assert.AreEqual(0, save.RunCount);
            Assert.AreEqual(text, PlayerPrefs.GetString(MetaSave.PrefsKey + MetaSave.CorruptSuffix), "The bad text is kept aside");
            // The next save overwrites it cleanly.
            save.Balance = 7;
            save.Save();
            Assert.AreEqual(7, MetaSave.Load().Balance);
        }

        private static void LogAssert_IgnoreWarnings(System.Action action)
        {
            bool before = UnityEngine.TestTools.LogAssert.ignoreFailingMessages;
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try { action(); }
            finally { UnityEngine.TestTools.LogAssert.ignoreFailingMessages = before; }
        }

        [Test]
        public void Save_OlderVersion_LoadsKnownFieldsAndUpgrades()
        {
            // A pre-versioned save (no version field) and an explicit version 0.
            foreach (string json in new[]
                     {
                         "{\"balance\":40,\"runCount\":3,\"victories\":1,\"upgrades\":[{\"id\":\"power\",\"value\":2}]}",
                         "{\"version\":0,\"balance\":40,\"runCount\":3,\"victories\":1,\"upgrades\":[{\"id\":\"power\",\"value\":2}]}",
                     })
            {
                MetaSave save = MetaSave.FromJson(json, out MetaLoadStatus status);
                Assert.AreEqual(MetaLoadStatus.Upgraded, status, json);
                Assert.AreEqual(MetaSave.CurrentVersion, save.Version, "Brought up to date");
                Assert.AreEqual(40, save.Balance);
                Assert.AreEqual(3, save.RunCount);
                Assert.AreEqual(1, save.Victories);
                Assert.AreEqual(2, UpgradeShop.Level(save, UpgradeStat.Power));
                StringAssert.Contains($"\"version\":{MetaSave.CurrentVersion}", save.ToJson());
            }
        }

        [Test]
        public void Save_NewerVersionAndExtraFields_LoadKnownFieldsWithoutCrashing()
        {
            const string json = "{\"version\":99,\"balance\":12,\"runCount\":2,\"futureThing\":{\"a\":[1,2]}," +
                                "\"upgrades\":[{\"id\":\"health\",\"value\":1,\"extra\":true},{\"id\":\"warp\",\"value\":3}]," +
                                "\"abilityCounts\":[{\"id\":\"physics-item\",\"value\":4}],\"cosmetics\":[\"hat\"]}";
            MetaSave save = MetaSave.FromJson(json, out MetaLoadStatus status);
            Assert.AreEqual(MetaLoadStatus.Newer, status);
            Assert.AreEqual(MetaSave.CurrentVersion, save.Version, "Never claims a newer version than the data it holds");
            Assert.AreEqual(12, save.Balance);
            Assert.AreEqual(2, save.RunCount);
            Assert.AreEqual(1, UpgradeShop.Level(save, UpgradeStat.Health));
            Assert.AreEqual(3, save.UpgradeLevel("warp"), "Unknown upgrade ids are kept");
            Assert.AreEqual(4, save.AbilityCount("physics-item"));
        }

        [Test]
        public void Save_NewerVersion_IsBackedUpBeforeTheFirstOverwrite()
        {
            const string newer = "{\"version\":7,\"balance\":12,\"warpDrive\":{\"level\":3}}";
            PlayerPrefs.SetString(MetaSave.PrefsKey, newer);
            MetaSave save = MetaSave.Load(MetaSave.PrefsKey, out MetaLoadStatus status);
            Assert.AreEqual(MetaLoadStatus.Newer, status);
            Assert.AreEqual(newer, PlayerPrefs.GetString(MetaSave.PrefsKey + MetaSave.NewerSuffix), "The original text is kept");
            save.Balance = 20;
            save.Save();
            StringAssert.Contains($"\"version\":{MetaSave.CurrentVersion}", PlayerPrefs.GetString(MetaSave.PrefsKey));
            // A later load (now a current save) leaves the first backup alone.
            MetaSave.Load(MetaSave.PrefsKey, out MetaLoadStatus again);
            Assert.AreEqual(MetaLoadStatus.Loaded, again);
            Assert.AreEqual(newer, PlayerPrefs.GetString(MetaSave.PrefsKey + MetaSave.NewerSuffix));
        }

        [Test]
        public void Save_BadValues_AreCleaned()
        {
            const string json = "{\"version\":1,\"balance\":-50,\"runCount\":-1,\"victories\":-3," +
                                "\"upgrades\":[{\"id\":\"\",\"value\":2},{\"id\":\"speed\",\"value\":-4},{\"id\":\"power\",\"value\":1},{\"id\":\"power\",\"value\":3}]," +
                                "\"abilityCounts\":null}";
            MetaSave save = MetaSave.FromJson(json, out MetaLoadStatus status);
            Assert.AreEqual(MetaLoadStatus.Loaded, status);
            Assert.AreEqual(0, save.Balance);
            Assert.AreEqual(0, save.RunCount);
            Assert.AreEqual(0, save.Victories);
            Assert.AreEqual(0, UpgradeShop.Level(save, UpgradeStat.Speed));
            Assert.AreEqual(3, UpgradeShop.Level(save, UpgradeStat.Power), "Duplicates merge (highest wins)");
            Assert.IsFalse(save.Upgrades.Any(e => string.IsNullOrEmpty(e.id)));
            Assert.IsNotNull(save.AbilityCounts);
        }

        [Test]
        public void Save_EmptyOrNull_IsFresh()
        {
            foreach (string json in new[] { null, "", "   " })
            {
                MetaSave save = MetaSave.FromJson(json, out MetaLoadStatus status);
                Assert.AreEqual(MetaLoadStatus.Fresh, status);
                Assert.AreEqual(0, save.Balance);
            }
            MetaSave empty = MetaSave.FromJson("{}", out MetaLoadStatus emptyStatus);
            Assert.AreEqual(MetaLoadStatus.Upgraded, emptyStatus, "{} has no version: treated as old");
            Assert.AreEqual(0, empty.Balance);
        }

        // ---------- Upgrades ----------

        [Test]
        public void Cost_RisesPerLevel()
        {
            Assert.AreEqual(5, UpgradeShop.MaxLevel);
            int[] expected = { 20, 40, 60, 80, 100 };
            for (int level = 0; level < UpgradeShop.MaxLevel; level++)
                Assert.AreEqual(expected[level], UpgradeShop.Cost(level), $"level {level} -> {level + 1}");
            for (int level = 1; level < UpgradeShop.MaxLevel; level++)
                Assert.Greater(UpgradeShop.Cost(level), UpgradeShop.Cost(level - 1));
        }

        [Test]
        public void Buy_SpendsAndLevelsUp_UntilMaxed()
        {
            var save = new MetaSave { Balance = 1000 };
            int spent = 0;
            for (int level = 0; level < UpgradeShop.MaxLevel; level++)
            {
                PurchaseResult result = UpgradeShop.TryBuy(save, UpgradeStat.Health);
                Assert.IsTrue(result.Bought, result.Message);
                spent += UpgradeShop.Cost(level);
                Assert.AreEqual(level + 1, UpgradeShop.Level(save, UpgradeStat.Health));
                Assert.AreEqual(1000 - spent, save.Balance);
            }
            PurchaseResult maxed = UpgradeShop.TryBuy(save, UpgradeStat.Health);
            Assert.AreEqual(PurchaseOutcome.Maxed, maxed.Outcome);
            Assert.AreEqual(1000 - spent, save.Balance, "Refused: nothing spent");
            Assert.AreEqual(UpgradeShop.MaxLevel, UpgradeShop.Level(save, UpgradeStat.Health));
            Assert.IsNotEmpty(maxed.Message);
            Assert.AreEqual(0, UpgradeShop.NextCost(save, UpgradeStat.Health));
        }

        [Test]
        public void Buy_TooPoor_IsRefusedWithAMessage()
        {
            var save = new MetaSave { Balance = 19 };
            PurchaseResult result = UpgradeShop.TryBuy(save, UpgradeStat.Defence);
            Assert.AreEqual(PurchaseOutcome.NotEnough, result.Outcome);
            Assert.AreEqual(19, save.Balance);
            Assert.AreEqual(0, UpgradeShop.Level(save, UpgradeStat.Defence));
            StringAssert.Contains("Not enough", result.Message);
            save.Balance = 20;
            Assert.IsTrue(UpgradeShop.TryBuy(save, UpgradeStat.Defence).Bought, "Exactly enough buys");
            Assert.AreEqual(0, save.Balance);
        }

        [Test]
        public void Apply_AddsOnePointPerLevel()
        {
            var save = new MetaSave();
            save.SetUpgradeLevel("health", 2);
            save.SetUpgradeLevel("defence", 1);
            save.SetUpgradeLevel("power", 3);
            save.SetUpgradeLevel("speed", 9); // clamped to the max
            StatBlock stats = UpgradeShop.Apply(StatBlock.Default, save);
            Assert.AreEqual(new StatBlock(PlayerStats.DefaultMaxHealth + 2, 1, 4, 1 + UpgradeShop.MaxLevel), stats);
            Assert.AreEqual(StatBlock.Default, UpgradeShop.Apply(StatBlock.Default, new MetaSave()));
        }

        [Test]
        public void Copy_NeverSaysSort()
        {
            var texts = new List<string> { BetweenRunsScreen.VictoryTitle, BetweenRunsScreen.DefeatTitle, BetweenRunsScreen.ContinueLabel };
            foreach (UpgradeStat stat in UpgradeShop.All)
            {
                texts.Add(UpgradeShop.Name(stat));
                texts.Add(UpgradeShop.Blurb(stat));
                for (int level = 0; level <= UpgradeShop.MaxLevel; level++) texts.Add(UpgradeShop.EffectText(stat, 1, level));
            }
            var save = new MetaSave { Balance = 5 };
            texts.Add(UpgradeShop.TryBuy(save, UpgradeStat.Power).Message);
            save.Balance = 999;
            texts.Add(UpgradeShop.TryBuy(save, UpgradeStat.Power).Message);
            save.SetUpgradeLevel("power", UpgradeShop.MaxLevel);
            texts.Add(UpgradeShop.TryBuy(save, UpgradeStat.Power).Message);
            foreach (string text in texts)
                StringAssert.DoesNotContain("sort", text.ToLowerInvariant(), text);
        }

        // ---------- Earnings ----------

        [Test]
        public void Earnings_EachSourceCountsOnce()
        {
            var e = new RunEarnings();
            Assert.IsTrue(e.Add(EarningSource.Trial, "trial:1", 25));
            Assert.IsFalse(e.Add(EarningSource.Trial, "trial:1", 25), "Re-triggering doesn't double-count");
            Assert.IsTrue(e.Add(EarningSource.Trial, "trial:2", 25));
            Assert.IsTrue(e.Add(EarningSource.Boss, "boss", RunWallet.BossVictory));
            Assert.IsFalse(e.Add(EarningSource.Boss, "boss", RunWallet.BossVictory));
            Assert.IsTrue(e.Add(EarningSource.Hidden, "hidden:0", 5));
            Assert.IsFalse(e.Add(EarningSource.Hidden, "hidden:0", 5));
            Assert.IsFalse(e.Add(EarningSource.Hidden, "hidden:1", -5), "Negative amounts are refused");
            Assert.AreEqual(50, e.Of(EarningSource.Trial));
            Assert.AreEqual(2, e.CountOf(EarningSource.Trial));
            Assert.AreEqual(60, e.Of(EarningSource.Boss));
            Assert.AreEqual(5, e.Of(EarningSource.Hidden));
            Assert.AreEqual(115, e.Total);
            RunEarnings copy = e.Copy();
            e.Reset();
            Assert.AreEqual(0, e.Total);
            Assert.IsTrue(e.Add(EarningSource.Boss, "boss", 60), "A new run pays again");
            Assert.AreEqual(115, copy.Total, "A copy keeps the old run");
        }

        [Test]
        public void Economy_OneUpgradeAfterADecentFirstRun()
        {
            // Design notes: trial 25 each (3 faces), boss 60, hidden 5 each (4-6 per run); cost 20 x (level + 1), max 5.
            int decent = 2 * 25 + 4 * HiddenCurrencyPlacement.Amount;
            Assert.GreaterOrEqual(decent, UpgradeShop.Cost(0));
            Assert.AreEqual(60, RunWallet.BossVictory);
            Assert.AreEqual(5, HiddenCurrencyPlacement.Amount);
            Assert.AreEqual(25, BiologyPlan.TrialCurrency);
            Assert.AreEqual(25, ChemistryPlan.TrialCurrency);
            Assert.AreEqual(25, PhysicsPlan.TrialCurrency);
        }

        // ---------- Hint density and seeds ----------

        [Test]
        public void HintDensity_SparseOnFirstRunOnly()
        {
            Assert.AreEqual(HintDensity.Sparse, HintGenerator.DensityFor(0));
            Assert.AreEqual(HintGenerator.FirstRunDensity, HintGenerator.DensityFor(0));
            Assert.AreEqual(HintDensity.Full, HintGenerator.DensityFor(1));
            Assert.AreEqual(HintDensity.Full, HintGenerator.DensityFor(25));
        }

        [Test]
        public void NextSeed_NeverRepeatsTheCurrentOne_AndVariesWithTime()
        {
            var seen = new HashSet<int>();
            for (long t = 0; t < 200; t++)
            {
                int seed = RunLoop.NextSeed(1234, 638000000000000000L + t * 10000);
                Assert.AreNotEqual(1234, seed);
                Assert.GreaterOrEqual(seed, 0);
                seen.Add(seed);
            }
            Assert.Greater(seen.Count, 190, "Different times give different seeds");
            Assert.AreEqual(RunLoop.NextSeed(7, 99), RunLoop.NextSeed(7, 99), "Same inputs, same seed (replayable logic)");
        }

        // ---------- Hidden currency placement ----------

        [Test]
        public void Hidden_SameSeedSamePlacement_OwnStream()
        {
            ModuleLibrary library = Library();
            Assert.AreEqual(16UL, HiddenCurrencyPlacement.RngStream);
            for (int seed = 1; seed <= 30; seed++)
            {
                var model = new CubeModel(seed);
                CubeLayout layout = CubeLayout.Generate(model, library);
                string a = HiddenCurrencyPlacement.Generate(model, layout, library).Signature();
                string b = HiddenCurrencyPlacement.Generate(new CubeModel(seed), CubeLayout.Generate(new CubeModel(seed), library), library).Signature();
                Assert.AreEqual(a, b, $"seed {seed}");
                // Generating it changes no other placement (own stream, pure).
                string items = ItemPlacement.ForRun(model, layout, library, null).Signature();
                HiddenCurrencyPlacement.Generate(model, layout, library);
                Assert.AreEqual(items, ItemPlacement.ForRun(model, layout, library, null).Signature());
            }
            var signatures = new HashSet<string>();
            for (int seed = 1; seed <= 30; seed++)
            {
                var model = new CubeModel(seed);
                signatures.Add(HiddenCurrencyPlacement.Generate(model, CubeLayout.Generate(model, library), library).Signature());
            }
            Assert.Greater(signatures.Count, 20, "Seeds vary the placement");
        }

        [Test]
        public void Hidden_SweepRulesHoldOverFiftySeeds()
        {
            ModuleLibrary library = Library();
            var counts = new HashSet<int>();
            for (int seed = SeedSweep.DefaultFirstSeed; seed < SeedSweep.DefaultFirstSeed + SeedSweep.DefaultSeedCount; seed++)
            {
                var model = new CubeModel(seed);
                CubeLayout layout = CubeLayout.Generate(model, library);
                HiddenCurrencyPlacement placement = HiddenCurrencyPlacement.Generate(model, layout, library);
                List<string> problems = HiddenCurrencyPlacement.FindProblems(model, layout, placement, library,
                    SeedSweep.ModuleLookup(layout, library));
                Assert.IsEmpty(problems, $"seed {seed}: {string.Join("; ", problems)}");
                counts.Add(placement.Spots.Count);
                foreach (HiddenCurrencySpot spot in placement.Spots)
                {
                    Assert.IsTrue(CubeLayout.IsLaidOut(model, spot.Screen.Face), $"seed {seed}: {spot} on a built face");
                    // Clear of every gate slot's pocket: no gate (required or optional) can stand in front of it.
                    ScreenModule module = SeedSweep.ModuleLookup(layout, library)(spot.Screen);
                    Vector2 local = module.HiddenItems[spot.Slot].transform.position - module.transform.position;
                    foreach (GateSlot gate in module.Gates)
                    {
                        Vector2 pocket = gate.PocketCentre - (Vector2)module.transform.position;
                        Assert.Greater((local - pocket).magnitude, 1.2f, $"seed {seed}: {spot} next to a gate pocket");
                    }
                }
            }
            Assert.IsTrue(counts.All(c => c >= HiddenCurrencyPlacement.MinCount && c <= HiddenCurrencyPlacement.MaxCount));
            Assert.Greater(counts.Count, 1, "The count varies with the seed");
        }

        [Test]
        public void Hidden_EveryLibrarySlotIsFair()
        {
            ModuleLibrary library = Library();
            foreach (ModuleLibrary.ThemePool pool in library.SciencePools)
            {
                foreach (ScreenModule module in pool.Modules)
                {
                    Assert.Greater(module.HiddenItems.Length, 0, module.name);
                    for (int i = 0; i < module.HiddenItems.Length; i++)
                        Assert.IsNull(HiddenCurrencyPlacement.GeometryProblem(module, i), $"{module.name} hidden slot {i}");
                }
            }
        }

        [Test]
        public void Hidden_SweepReportsBadPlacements()
        {
            ModuleLibrary library = Library();
            const int seed = 1234;
            var model = new CubeModel(seed);
            CubeLayout layout = CubeLayout.Generate(model, library);
            FaceId built = Enumerable.Range(0, CubeSettings.FaceCount).Select(f => (FaceId)f).First(f => CubeLayout.IsLaidOut(model, f));
            var good = new HiddenCurrencySpot(new ScreenAddress(built, 0, 0), 0);

            var tooFew = new HiddenCurrencyPlacement(seed, new[] { good });
            Assert.IsTrue(HiddenCurrencyPlacement.FindProblems(model, layout, tooFew, library).Any(p => p.Contains("expected")));

            var spots = new List<HiddenCurrencySpot> { good, good, new HiddenCurrencySpot(new ScreenAddress(CubeModel.StartFace, 0, 0), 0),
                new HiddenCurrencySpot(new ScreenAddress(built, 1, 0), 7) };
            List<string> problems = HiddenCurrencyPlacement.FindProblems(model, layout, new HiddenCurrencyPlacement(seed, spots), library);
            Assert.IsTrue(problems.Any(p => p.Contains("used twice")), string.Join("; ", problems));
            Assert.IsTrue(problems.Any(p => p.Contains("not on a built science face")), string.Join("; ", problems));
            Assert.IsTrue(problems.Any(p => p.Contains("hidden-item slots")), string.Join("; ", problems));

            // The sweep names the failing seed.
            var result = new SeedSweep.SeedResult
            {
                Seed = seed, Unreachable = new List<ScreenAddress>(), HiddenProblems = problems,
            };
            Assert.IsFalse(result.Passed);
            Assert.IsFalse(result.ItemsChecked, "Hidden currency has its own field");
            StringAssert.Contains($"Seed {seed}: hidden currency", SeedSweep.Describe(new[] { result }));
        }

        [Test]
        public void Hidden_GeometryCatchesAlcovesAndLanes()
        {
            var root = new GameObject("Fake Module");
            try
            {
                var module = root.AddComponent<ScreenModule>();
                var gate = new GameObject("Gate").AddComponent<GateSlot>();
                gate.transform.SetParent(root.transform, false);
                gate.transform.localPosition = new Vector3(4.9f, 1.4f, 0f);
                gate.Opening = Facing.South;
                gate.PocketOffset = new Vector2(0f, 1f);
                var inAlcove = new GameObject("Hidden 0").AddComponent<HiddenItemSlot>();
                inAlcove.transform.SetParent(root.transform, false);
                inAlcove.transform.localPosition = new Vector3(4.9f, 2.4f, 0f);
                var inLane = new GameObject("Hidden 1").AddComponent<HiddenItemSlot>();
                inLane.transform.SetParent(root.transform, false);
                inLane.transform.localPosition = new Vector3(0.5f, 3f, 0f);
                var fair = new GameObject("Hidden 2").AddComponent<HiddenItemSlot>();
                fair.transform.SetParent(root.transform, false);
                fair.transform.localPosition = new Vector3(-6f, -3f, 0f);

                StringAssert.Contains("alcove", HiddenCurrencyPlacement.GeometryProblem(module, 0));
                StringAssert.Contains("lane", HiddenCurrencyPlacement.GeometryProblem(module, 1));
                Assert.IsNull(HiddenCurrencyPlacement.GeometryProblem(module, 2));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SeedSweep_ChecksHiddenCurrency()
        {
            List<SeedSweep.SeedResult> results = SeedSweep.Run(count: 10, catalog: Library());
            foreach (SeedSweep.SeedResult r in results)
            {
                Assert.IsNotNull(r.HiddenCurrency, $"seed {r.Seed}");
                Assert.IsTrue(r.HiddenChecked);
                Assert.IsEmpty(r.HiddenProblems, $"seed {r.Seed}: {string.Join("; ", r.HiddenProblems)}");
            }
            // A catalog that knows hidden slots but not gate slots checks hidden currency without claiming items were checked.
            SeedSweep.SeedResult only = SeedSweep.Run(count: 1, catalog: new HiddenOnlyCatalog(Library()))[0];
            Assert.IsTrue(only.HiddenChecked);
            Assert.IsFalse(only.ItemsChecked, "ItemsChecked keeps its meaning");
            StringAssert.Contains("hidden meta currency", SeedSweep.Describe(results));
        }
    }
}
