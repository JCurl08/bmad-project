using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Tracer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Game.Cube.Tests.TestInput;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The run loop in the real Cube scene (story 1.12), one test per I/O row: death on a face ends the run once and opens
    /// the between-runs screen; the boss outcome (win or loss) ends it once through RunState; trials, the boss and hidden
    /// items each pay once; the run's earnings are banked and saved at run end; purchases spend, level up, save and are
    /// refused when too poor or maxed; Continue starts a new run (new seed, Town, empty-handed, full health with the
    /// upgrade applied, full hints after the first run); progress survives a scene reload; a corrupt or older save loads
    /// without crashing; and the hidden currency sits in hidden-item slots on built faces, reachable and ungated. Every
    /// test runs on its own empty save (IsolatedMetaSave).
    /// </summary>
    [IsolatedMetaSave]
    public class RunLoopSceneTests : InputTestFixture
    {
        private const string SceneName = "Cube";

        private CubeWorld world;
        private CubeNavigator navigator;
        private RunState runState;
        private CoreArena arena;
        private RunLoop loop;
        private RunWallet wallet;
        private BetweenRunsScreen screen;
        private TownPopulation town;
        private CubeDebug debug;
        private PlayerStats stats;
        private Health health;
        private Inventory inventory;
        private Rigidbody2D body;
        private List<bool> runEvents;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            base.TearDown();
        }

        private IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            world = UnityEngine.Object.FindAnyObjectByType<CubeWorld>();
            navigator = UnityEngine.Object.FindAnyObjectByType<CubeNavigator>();
            runState = UnityEngine.Object.FindAnyObjectByType<RunState>();
            arena = UnityEngine.Object.FindAnyObjectByType<CoreArena>();
            loop = UnityEngine.Object.FindAnyObjectByType<RunLoop>();
            wallet = UnityEngine.Object.FindAnyObjectByType<RunWallet>();
            screen = UnityEngine.Object.FindAnyObjectByType<BetweenRunsScreen>();
            town = UnityEngine.Object.FindAnyObjectByType<TownPopulation>();
            debug = UnityEngine.Object.FindAnyObjectByType<CubeDebug>();
            Assert.IsNotNull(loop, "The Cube scene has a RunLoop");
            Assert.IsNotNull(wallet, "The Cube scene has a RunWallet");
            Assert.IsNotNull(screen, "The Cube scene has the between-runs screen");
            Assert.AreSame(loop, screen.Loop);
            Assert.AreSame(runState, loop.RunState);
            Assert.AreSame(world, loop.World);
            Assert.AreSame(wallet, loop.Wallet);
            stats = navigator.GetComponent<PlayerStats>();
            Assert.AreSame(stats, loop.Player);
            health = navigator.GetComponent<Health>();
            inventory = navigator.GetComponent<Inventory>();
            body = navigator.GetComponent<Rigidbody2D>();
            runEvents = new List<bool>();
            runState.RunEnded += v => runEvents.Add(v);
        }

        private void NoFaceEnemies()
        {
            world.GetComponent<BiologyFace>().SpawnEnemies = false;
            world.GetComponent<ChemistryFace>().SpawnEnemies = false;
            world.GetComponent<PhysicsFace>().SpawnEnemies = false;
        }

        private IEnumerable<FaceId> BuiltFaces()
        {
            for (int f = 0; f < CubeSettings.FaceCount; f++)
                if (CubeLayout.IsLaidOut(world.Model, (FaceId)f)) yield return (FaceId)f;
        }

        /// <summary>A fresh run on a seed, enemies off, then onto a built face (which reveals the science faces).</summary>
        private IEnumerator Reveal(int seed = 1234)
        {
            NoFaceEnemies();
            world.Rebuild(seed);
            yield return null;
            NoFaceEnemies();
            navigator.TeleportTo(new ScreenAddress(BuiltFaces().First(), 0, 0), Facing.East);
            Assert.IsTrue(world.ScienceRevealed);
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        private void Kill()
        {
            health.InvulnerableSeconds = 0f;
            health.TakeDamage(1000f, null);
            Assert.IsTrue(health.IsDead);
        }

        private void PlacePlayer(Vector2 at)
        {
            body.position = at;
            body.linearVelocity = Vector2.zero;
            navigator.transform.position = new Vector3(at.x, at.y, navigator.transform.position.z);
            Physics2D.SyncTransforms();
        }

        private List<TrialRoom> Trials()
        {
            var trials = new List<TrialRoom>();
            TrialRoom bio = world.GetComponent<BiologyFace>().Trial;
            TrialRoom chem = world.GetComponent<ChemistryFace>().Trial;
            TrialRoom phys = world.GetComponent<PhysicsFace>().Trial;
            foreach (TrialRoom t in new[] { bio, chem, phys })
                if (t != null) trials.Add(t);
            return trials;
        }

        private static MetaSave Saved() => MetaSave.Load(MetaSave.PrefsKey, out _);

        // ---------- Start state ----------

        [UnityTest]
        public IEnumerator FirstRun_FreshSave_BaseStats_SparseHints()
        {
            yield return Load();
            Assert.AreEqual(IsolatedMetaSaveAttribute.TestKey, MetaSave.PrefsKey);
            Assert.AreEqual(MetaLoadStatus.Fresh, loop.LoadStatus);
            Assert.AreEqual(0, loop.Meta.RunCount);
            Assert.AreEqual(0, loop.Meta.Balance);
            Assert.AreEqual(StatBlock.Default, loop.BaseStats);
            Assert.AreEqual(StatBlock.Default, StatBlock.Of(stats), "No upgrades: base stats");
            Assert.AreEqual(HintDensity.Sparse, loop.HintDensity);
            Assert.AreEqual(HintDensity.Sparse, town.HintDensity, "Sparse hints on the first run");
            Assert.AreEqual(HintDensity.Sparse, world.GetComponent<BiologyFace>().HintDensity);
            Assert.AreEqual(HintDensity.Sparse, world.GetComponent<ChemistryFace>().HintDensity);
            Assert.AreEqual(HintDensity.Sparse, world.GetComponent<PhysicsFace>().HintDensity);
            Assert.AreEqual(HintDensity.Sparse, debug.HintDensity);
            Assert.IsFalse(loop.ScreenOpen);
            Assert.IsFalse(screen.Visible);
        }

        [UnityTest]
        public IEnumerator FirstRun_GetsAFreshSeed_UnlessTheSceneSeedIsKept()
        {
            yield return Load();
            int sceneSeed = world.Seed; // kept by the test override
            RunLoop.KeepSceneSeed = false;
            yield return Load();
            Assert.AreNotEqual(sceneSeed, world.Seed, "A launch starts on a fresh seed, not the scene's");
            Assert.AreEqual(world.Seed, world.Model.Seed);
            Assert.AreEqual(world.Seed, town.PopulatedSeed, "The town is built for that seed");
            int first = world.Seed;
            yield return Load();
            Assert.AreNotEqual(first, world.Seed, "Every launch differs");
        }

        [UnityTest]
        public IEnumerator RebuildAfterDeath_ResetsThePlayer_LikeContinue()
        {
            yield return Load();
            inventory.Add(world.ItemFor(Theme.Biology));
            Kill();
            yield return null;
            Assert.IsTrue(loop.ScreenOpen);
            world.Rebuild(99); // the debug reroll
            yield return null;
            Assert.IsFalse(loop.ScreenOpen);
            Assert.IsFalse(health.IsDead, "Not left dead and frozen");
            Assert.AreEqual(health.Max, health.Current, 1e-4f);
            Assert.IsEmpty(inventory.Items);
            Assert.IsTrue(navigator.GetComponent<PlayerMover>().enabled);
            Assert.IsTrue(navigator.GetComponent<PlayerAttack>().enabled);
            Assert.IsTrue(navigator.GetComponent<Equipment>().enabled);
            Assert.AreEqual(world.Model.StartScreen, navigator.Current);
        }

        // ---------- Death on a face ----------

        [UnityTest]
        public IEnumerator DeathOnAFace_EndsTheRunOnce_AndOpensTheScreen()
        {
            yield return Load();
            Assert.AreEqual(CubeModel.StartFace, navigator.Face, "In Town");
            Kill();
            yield return null;
            CollectionAssert.AreEqual(new[] { false }, runEvents, "RunEnded(false) once");
            Assert.IsTrue(runState.Ended);
            Assert.IsFalse(runState.Victory);
            Assert.IsTrue(loop.ScreenOpen);
            Assert.IsTrue(screen.Visible);
            Assert.AreEqual(BetweenRunsScreen.DefeatTitle, screen.Title);
            Assert.IsFalse(loop.LastRun.Victory);
            Assert.AreEqual(world.Seed, loop.LastRun.Seed, "The seed is shown for replays");
            Assert.AreEqual(5, screen.EarningsLines().Count);
            Assert.AreEqual(1, loop.Meta.RunCount);
            Assert.AreEqual(1, Saved().RunCount, "Saved at run end");
            Assert.AreEqual(0, Saved().Victories);

            // Nothing more ends it.
            health.ResetHealth();
            Kill();
            Assert.IsFalse(runState.EndRun(false));
            yield return null;
            Assert.AreEqual(1, runEvents.Count, "Fires once");
            Assert.AreEqual(1, loop.Meta.RunCount, "Counted once");
        }

        // ---------- Boss outcomes ----------

        private IEnumerator EnterArena(int seed)
        {
            yield return Reveal(seed);
            arena.UseFallbackBoss = true;
            Assert.IsNotEmpty(arena.Portals);
            Assert.IsTrue(arena.Portals[0].TryEnter(navigator));
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.IsTrue(arena.FightActive);
        }

        [UnityTest]
        public IEnumerator BossVictory_EndsTheRunOnce_PaysTheBoss_AndShowsTheOutcome()
        {
            yield return Load();
            yield return EnterArena(1234);
            stats.MaxHealth = 999;
            health.ResetHealth();
            var boss = (FallbackBoss)arena.CurrentPhase;
            for (int i = 0; i < boss.HitsNeeded && arena.FightActive; i++) arena.DemonHit(null);
            yield return WaitUntilOrGameTimeout(() => !arena.FightActive, 1f);
            Assert.AreEqual(CoreOutcome.Victory, arena.Outcome);
            CollectionAssert.AreEqual(new[] { true }, runEvents, "No double end");
            Assert.AreEqual(RunWallet.BossVictory, loop.LastRun.Earnings.Of(EarningSource.Boss));
            Assert.AreEqual(RunWallet.BossVictory, loop.LastRun.Banked);
            Assert.AreEqual(RunWallet.BossVictory, loop.Meta.Balance);
            Assert.AreEqual(1, loop.Meta.Victories);
            Assert.AreEqual(BetweenRunsScreen.VictoryTitle, screen.Title);
            Assert.IsTrue(screen.Visible);
            Assert.IsFalse(navigator.GetComponent<PlayerMover>().enabled, "Control stops while the screen is up");
            Assert.IsFalse(navigator.GetComponent<PlayerAttack>().enabled);
            Assert.IsFalse(navigator.GetComponent<Equipment>().enabled, "No item cycling behind the screen");
            MetaSave saved = Saved();
            Assert.AreEqual(RunWallet.BossVictory, saved.Balance);
            Assert.AreEqual(1, saved.Victories);

            // Later events change nothing.
            Assert.IsFalse(arena.DemonHit(null));
            Kill();
            yield return null;
            Assert.AreEqual(1, runEvents.Count);
            Assert.AreEqual(RunWallet.BossVictory, loop.Meta.Balance, "Banked once");
        }

        [UnityTest]
        public IEnumerator BossDefeat_EndsTheRunOnce_ThroughTheArena()
        {
            yield return Load();
            yield return EnterArena(1234);
            Kill();
            yield return null;
            Assert.AreEqual(CoreOutcome.Defeat, arena.Outcome);
            CollectionAssert.AreEqual(new[] { false }, runEvents, "One end, not two");
            Assert.AreEqual(1, runState.EndCount);
            Assert.IsTrue(loop.ScreenOpen);
            Assert.AreEqual(BetweenRunsScreen.DefeatTitle, screen.Title);
            Assert.AreEqual(0, loop.LastRun.Earnings.Of(EarningSource.Boss));
            Assert.AreEqual(0, loop.Meta.Victories);
        }

        // ---------- Earn and bank ----------

        [UnityTest]
        public IEnumerator Earn_TrialsAndHiddenItemsPayOnce_ThenBankOnRunEnd()
        {
            yield return Load();
            yield return Reveal(1234);
            List<TrialRoom> trials = Trials();
            Assert.IsNotEmpty(trials, "The run has trials");
            int expected = 0;
            foreach (TrialRoom trial in trials)
            {
                Assert.IsTrue(trial.Complete());
                Assert.IsFalse(trial.Complete());
                expected += trial.Currency;
            }
            Assert.AreEqual(expected, wallet.Earnings.Of(EarningSource.Trial));
            Assert.AreEqual(trials.Count, wallet.Earnings.CountOf(EarningSource.Trial));

            // One hidden item by walking onto it, the rest directly; re-collecting pays nothing.
            List<HiddenCurrencyPickup> hidden = world.HiddenCurrencyPickups.ToList();
            Assert.GreaterOrEqual(hidden.Count, HiddenCurrencyPlacement.MinCount);
            HiddenCurrencyPickup first = hidden[0];
            ScreenModule home = first.GetComponentInParent<ScreenModule>();
            Assert.IsNotNull(home, "A hidden item sits in a module");
            navigator.TeleportTo(AddressOf(home), Facing.East);
            yield return new WaitForFixedUpdate();
            PlacePlayer(first.transform.position);
            yield return WaitUntilOrGameTimeout(() => first == null || first.IsCollected, 1f);
            Assert.IsTrue(first == null || first.IsCollected, "Touching a hidden item collects it");
            Assert.AreEqual(HiddenCurrencyPlacement.Amount, wallet.Earnings.Of(EarningSource.Hidden));
            yield return null;
            for (int i = 1; i < hidden.Count; i++)
            {
                Assert.IsTrue(hidden[i].Collect());
                Assert.IsFalse(hidden[i].Collect(), "Re-triggering doesn't double-count");
            }
            int hiddenTotal = hidden.Count * HiddenCurrencyPlacement.Amount;
            Assert.AreEqual(hiddenTotal, wallet.Earnings.Of(EarningSource.Hidden));
            Assert.AreEqual(expected + hiddenTotal, wallet.Total);
            Assert.IsFalse(wallet.Add(EarningSource.Hidden, "hidden:0", 5), "Same key: no double count");

            Kill();
            yield return null;
            Assert.AreEqual(expected + hiddenTotal, loop.LastRun.Banked, "Banked on defeat too");
            Assert.AreEqual(expected + hiddenTotal, loop.Meta.Balance);
            Assert.AreEqual(expected + hiddenTotal, Saved().Balance, "Saved");
            Assert.IsFalse(wallet.Add(EarningSource.Boss, "boss", 60), "Nothing pays after the run ended");
        }

        private ScreenAddress AddressOf(ScreenModule module)
        {
            foreach (ScreenAddress address in world.Model.AllScreens())
                if (world.ModuleAt(address) == module) return address;
            return navigator.Current;
        }

        // ---------- Buy ----------

        [UnityTest]
        public IEnumerator Buy_SpendsLevelsAndSaves_OrIsRefusedWithAMessage()
        {
            yield return Load();
            Kill();
            yield return null;
            Assert.IsTrue(loop.ScreenOpen);

            PurchaseResult poor = loop.Buy(UpgradeStat.Health);
            Assert.AreEqual(PurchaseOutcome.NotEnough, poor.Outcome);
            Assert.AreEqual(poor.Message, loop.LastMessage);
            Assert.AreEqual(0, UpgradeShop.Level(loop.Meta, UpgradeStat.Health));

            loop.Meta.Balance = 50;
            PurchaseResult bought = loop.Buy(UpgradeStat.Health);
            Assert.IsTrue(bought.Bought, bought.Message);
            Assert.AreEqual(1, UpgradeShop.Level(loop.Meta, UpgradeStat.Health));
            Assert.AreEqual(30, loop.Meta.Balance);
            MetaSave saved = Saved();
            Assert.AreEqual(1, UpgradeShop.Level(saved, UpgradeStat.Health), "Saved after the purchase");
            Assert.AreEqual(30, saved.Balance);
            Assert.AreEqual(PlayerStats.DefaultMaxHealth, stats.MaxHealth, "Applied next run, not now");
            StringAssert.Contains("40", screen.UpgradeLine(UpgradeStat.Health), "Next cost shown");

            loop.Meta.SetUpgradeLevel(UpgradeShop.Id(UpgradeStat.Power), UpgradeShop.MaxLevel);
            loop.Meta.Balance = 1000;
            PurchaseResult maxed = loop.Buy(UpgradeStat.Power);
            Assert.AreEqual(PurchaseOutcome.Maxed, maxed.Outcome);
            Assert.AreEqual(1000, loop.Meta.Balance);
            StringAssert.Contains("MAXED", screen.UpgradeLine(UpgradeStat.Power));
        }

        // ---------- The full loop ----------

        [UnityTest]
        public IEnumerator FullLoop_Die_Buy_Continue_NewRunWithTheUpgrade_ThenReloadKeepsIt()
        {
            yield return Load();
            yield return Reveal(1234);
            int oldSeed = world.Seed;

            // Pick up an item this run (per-ability count), twice over (counted once).
            ItemDefinition item = world.ItemFor(Theme.Biology);
            Assert.IsNotNull(item);
            Assert.IsTrue(inventory.Add(item));
            inventory.Remove(item);
            inventory.Add(item);
            Assert.AreEqual(1, loop.Meta.AbilityCount(item.Id));

            // Earn enough for one Health level, then die on a face.
            foreach (TrialRoom trial in Trials()) trial.Complete();
            Assert.GreaterOrEqual(wallet.Total, UpgradeShop.Cost(0));
            Kill();
            yield return null;
            Assert.IsTrue(screen.Visible);
            int balance = loop.Meta.Balance;
            Assert.GreaterOrEqual(balance, UpgradeShop.Cost(0));

            Assert.IsTrue(loop.Buy(UpgradeStat.Health).Bought);
            int newSeed = loop.Continue();
            yield return null;
            yield return null;

            Assert.AreNotEqual(oldSeed, newSeed, "A new seed");
            Assert.AreEqual(newSeed, world.Seed);
            Assert.IsFalse(loop.ScreenOpen);
            Assert.IsFalse(screen.Visible);
            Assert.IsFalse(runState.Ended, "A new run");
            Assert.IsFalse(world.ScienceRevealed);
            Assert.AreEqual(world.Model.StartScreen, navigator.Current, "Back in Town");
            Assert.IsFalse(navigator.InCoreArena);
            Assert.IsEmpty(inventory.Items, "Empty inventory");
            Assert.IsNull(navigator.GetComponent<Equipment>().Equipped, "Bare hands");
            Assert.AreEqual(IsotopeStage.None, navigator.GetComponent<Isotope>().Stage);
            Assert.IsNull(navigator.GetComponent<MassMitt>().Held);
            Assert.AreEqual(PlayerStats.DefaultMaxHealth + 1, stats.MaxHealth, "The upgrade is applied");
            Assert.IsFalse(health.IsDead);
            Assert.AreEqual(stats.MaxHealth, health.Max, 1e-4f);
            Assert.AreEqual(health.Max, health.Current, 1e-4f, "Full health");
            Assert.IsTrue(navigator.GetComponent<PlayerMover>().enabled, "Control is back");
            Assert.IsTrue(navigator.GetComponent<PlayerAttack>().enabled);
            Assert.IsTrue(navigator.GetComponent<Equipment>().enabled);
            Assert.AreEqual(0, wallet.Total, "Nothing earned yet this run");
            Assert.IsFalse(wallet.Banked);
            Assert.AreEqual(HintDensity.Full, town.HintDensity, "Full hints after the first run");
            Assert.AreEqual(HintDensity.Full, world.GetComponent<BiologyFace>().HintDensity);
            Assert.AreEqual(HintDensity.Full, debug.HintDensity);
            yield return WaitUntilOrGameTimeout(() => !town.Pending && town.PopulatedSeed == newSeed, 1f);
            Assert.AreEqual(newSeed, town.PopulatedSeed, "Town repopulated for the new run");

            // The new run plays: an item pickup counts again (once per run).
            inventory.Add(item);
            Assert.AreEqual(2, loop.Meta.AbilityCount(item.Id));

            // The run ends again; the new run's death works as before.
            Kill();
            yield return null;
            Assert.AreEqual(2, runEvents.Count);
            Assert.AreEqual(2, loop.Meta.RunCount);

            // Reload (a new browser tab / editor play): everything comes back.
            MetaSave before = loop.Meta;
            yield return Load();
            Assert.AreEqual(MetaLoadStatus.Loaded, loop.LoadStatus);
            Assert.AreEqual(before.Balance, loop.Meta.Balance);
            Assert.AreEqual(2, loop.Meta.RunCount);
            Assert.AreEqual(0, loop.Meta.Victories);
            Assert.AreEqual(1, UpgradeShop.Level(loop.Meta, UpgradeStat.Health));
            Assert.AreEqual(2, loop.Meta.AbilityCount(item.Id));
            Assert.AreEqual(PlayerStats.DefaultMaxHealth + 1, stats.MaxHealth, "Upgrades apply from the start");
            Assert.AreEqual(health.Max, health.Current, 1e-4f);
            Assert.AreEqual(HintDensity.Full, town.HintDensity);
        }

        [UnityTest]
        public IEnumerator Continue_FromAVictoryInTheArena_GoesBackToTown()
        {
            yield return Load();
            yield return EnterArena(77);
            var boss = (FallbackBoss)arena.CurrentPhase;
            for (int i = 0; i < boss.HitsNeeded && arena.FightActive; i++) arena.DemonHit(null);
            yield return WaitUntilOrGameTimeout(() => !arena.FightActive, 1f);
            Assert.IsTrue(loop.ScreenOpen);
            Assert.IsTrue(navigator.InCoreArena);
            loop.Continue();
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.IsFalse(navigator.InCoreArena);
            Assert.AreEqual(world.Model.StartScreen, navigator.Current);
            Assert.AreEqual(CoreOutcome.None, arena.Outcome, "The arena is reset");
            Assert.IsFalse(runState.Ended);
            Assert.IsTrue(navigator.GetComponent<PlayerMover>().enabled);
        }

        // ---------- Persistence edge cases ----------

        [UnityTest]
        public IEnumerator CorruptSave_StartsFresh_WithoutCrashing()
        {
            PlayerPrefs.SetString(MetaSave.PrefsKey, "{\"balance\": oops");
            yield return Load();
            Assert.AreEqual(MetaLoadStatus.Corrupt, loop.LoadStatus);
            Assert.AreEqual(0, loop.Meta.Balance);
            Assert.AreEqual(StatBlock.Default, StatBlock.Of(stats));
            Kill();
            yield return null;
            Assert.AreEqual(1, Saved().RunCount, "The next save replaces the corrupt one");
        }

        [UnityTest]
        public IEnumerator OlderSaveWithExtras_LoadsKnownFields_AndUpgradesTheVersion()
        {
            PlayerPrefs.SetString(MetaSave.PrefsKey,
                "{\"balance\":33,\"runCount\":1,\"upgrades\":[{\"id\":\"speed\",\"value\":2}],\"pets\":[\"newt\"]}");
            yield return Load();
            Assert.AreEqual(MetaLoadStatus.Upgraded, loop.LoadStatus);
            Assert.AreEqual(33, loop.Meta.Balance);
            Assert.AreEqual(1 + 2, stats.Speed, "Speed upgrades apply at start");
            Assert.AreEqual(HintDensity.Full, town.HintDensity, "Not the first run");
            Kill();
            yield return null;
            Assert.AreEqual(MetaSave.CurrentVersion, Saved().Version, "Written back at the current version");
        }

        // ---------- Hidden items ----------

        [UnityTest]
        public IEnumerator HiddenItems_SitInHiddenSlotsOnBuiltFaces_ReachableAndUngated()
        {
            yield return Load();
            foreach (int seed in new[] { 1234, 7, 42 })
            {
                yield return Reveal(seed);
                HiddenCurrencyPlacement placement = world.HiddenCurrency;
                Assert.IsNotNull(placement);
                List<string> problems = HiddenCurrencyPlacement.FindProblems(world.Model, world.Layout, placement, world.Library,
                    SeedSweep.ModuleLookup(world.Layout, world.Library));
                Assert.IsEmpty(problems, $"seed {seed}: {string.Join("; ", problems)}");
                Assert.AreEqual(placement.Spots.Count, world.HiddenCurrencyPickups.Count);
                for (int i = 0; i < placement.Spots.Count; i++)
                {
                    HiddenCurrencySpot spot = placement.Spots[i];
                    HiddenCurrencyPickup pickup = world.HiddenCurrencyPickups[i];
                    Assert.IsTrue(CubeLayout.IsLaidOut(world.Model, spot.Screen.Face));
                    HiddenItemSlot slot = world.ModuleAt(spot.Screen).HiddenItems[spot.Slot];
                    Assert.Less(Vector2.Distance(slot.transform.position, pickup.transform.position), 0.01f, $"seed {seed}: {spot}");
                    Assert.IsTrue(pickup.GetComponent<Collider2D>().isTrigger, "Blocks nothing");
                    foreach (Gate gate in world.Gates.Concat(world.OptionalGates))
                        Assert.Greater(Vector2.Distance(gate.transform.position, pickup.transform.position), 1f, "Not at a gate");
                }
            }
        }
    }
}
