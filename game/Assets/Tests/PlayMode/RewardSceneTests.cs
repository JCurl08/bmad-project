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
    /// Roadblock rewards in the real Cube scene (4.7): the reveal builds every planned reward behind its roadblock (items
    /// on pedestals in alcoves or under lane mounds, hidden Jumbles* under mounds, chests everywhere else); a mound hides
    /// its reward until two swings of bare hands dig it away; chests give keys, boosts and fragment slots to the player
    /// (with the HUD line) and Jumbles* to the wallet; the run's end cashes in the keys, banks it all and the balance
    /// survives a reload; a new run takes the boosts back. Every test runs on its own empty save (IsolatedMetaSave).
    /// </summary>
    [IsolatedMetaSave]
    public class RewardSceneTests : InputTestFixture
    {
        private const float K = ScreenMath.ContentScale;
        private const string SceneName = "Cube";

        private CubeWorld world;
        private CubeNavigator navigator;
        private RunLoop loop;
        private RunWallet wallet;
        private CombatHud hud;
        private PlayerStats stats;
        private RunRewards rewards;
        private PlayerAttack attack;
        private Rigidbody2D body;
        private RunState runState;
        private CubeDebug debug;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
        }

        private IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            world = Object.FindAnyObjectByType<CubeWorld>();
            navigator = Object.FindAnyObjectByType<CubeNavigator>();
            loop = Object.FindAnyObjectByType<RunLoop>();
            wallet = Object.FindAnyObjectByType<RunWallet>();
            hud = Object.FindAnyObjectByType<CombatHud>();
            runState = Object.FindAnyObjectByType<RunState>();
            debug = Object.FindAnyObjectByType<CubeDebug>();
            stats = navigator.GetComponent<PlayerStats>();
            rewards = navigator.GetComponent<RunRewards>();
            attack = navigator.GetComponent<PlayerAttack>();
            body = navigator.GetComponent<Rigidbody2D>();
            Assert.IsNotNull(rewards, "The player keeps its roadblock rewards");
            Assert.AreSame(rewards, hud.Rewards, "The HUD shows them");
        }

        private void NoFaceEnemies()
        {
            world.GetComponent<BiologyFace>().SpawnEnemies = false;
            world.GetComponent<ChemistryFace>().SpawnEnemies = false;
            world.GetComponent<PhysicsFace>().SpawnEnemies = false;
            world.GetComponent<MathFace>().SpawnEnemies = false;
            world.GetComponent<EarthFace>().SpawnEnemies = false;
        }

        private IEnumerator Reveal(int seed)
        {
            NoFaceEnemies();
            world.Rebuild(seed);
            yield return null;
            NoFaceEnemies();
            FaceId first = world.Model.FaceOf(Theme.Biology);
            navigator.TeleportTo(new ScreenAddress(first, 0, 0), Facing.East);
            Assert.IsTrue(world.ScienceRevealed);
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        private void PlacePlayer(Vector2 at)
        {
            body.position = at;
            body.linearVelocity = Vector2.zero;
            navigator.transform.position = new Vector3(at.x, at.y, navigator.transform.position.z);
            Physics2D.SyncTransforms();
        }

        private static bool Shown(Component c) => c.GetComponentsInChildren<SpriteRenderer>(true).Any(r => r.enabled);

        [UnityTest]
        public IEnumerator Reveal_BuildsEveryPlannedReward_BehindItsRoadblock()
        {
            yield return Load();
            foreach (int seed in new[] { 7, 21, 42 })
            {
                yield return Reveal(seed);
                RewardPlacement plan = world.Rewards;
                Assert.IsNotNull(plan, $"seed {seed}");
                Assert.IsEmpty(RewardPlacement.FindProblems(world.Model, world.Layout, world.ItemPlacement, world.HiddenCurrency, plan,
                    world.Library, world.GetComponent<MathFace>().Plan.IslandAt));
                int chests = 0;
                foreach (RewardSpot spot in plan.Spots)
                {
                    string where = $"seed {seed}: {spot}";
                    if (spot.What.Kind == RewardKind.Equipment)
                    {
                        ItemPickup pickup = world.Pickups.Single(p => p != null && p.Item.HomeTheme == spot.What.Item);
                        Assert.IsNotNull(pickup.transform.Find("Pedestal"), $"{where}: on a pedestal");
                        if (spot.Where.IsLaneSpot)
                        {
                            Assert.IsInstanceOf<RewardMound>(pickup.Guard, $"{where}: under a mound");
                            Assert.IsFalse(Shown(pickup), $"{where}: hidden until dug");
                        }
                        else
                        {
                            Assert.IsNull(pickup.Guard, "The gate itself guards it");
                            Vector2 pocket = world.ModuleAt(spot.Where.Screen).Gates[spot.Where.Slot].PocketCentre;
                            Assert.Less(Vector2.Distance(pocket, pickup.transform.position), 0.01f, where);
                        }
                        continue;
                    }
                    if (spot.HiddenIndex >= 0)
                    {
                        HiddenCurrencyPickup coin = world.HiddenCurrencyPickups[spot.HiddenIndex];
                        Assert.IsInstanceOf<RewardMound>(coin.Guard, $"{where}: under a mound");
                        Assert.IsFalse(Shown(coin), where);
                        continue;
                    }
                    chests++;
                    RewardChest chest = world.RewardChests.Single(c => c.Spot.Index == spot.Index);
                    Assert.AreEqual(spot.What, chest.Spot.What);
                    Assert.IsTrue(chest.GetComponent<Collider2D>().isTrigger);
                    ScreenModule module = world.ModuleAt(spot.Where.Screen);
                    if (spot.Where.Kind == RoadblockKind.Mound)
                    {
                        Assert.IsInstanceOf<RewardMound>(chest.Guard, where);
                        Assert.IsFalse(Shown(chest), $"{where}: hidden under its mound");
                        Vector2 slot = module.HiddenItems[spot.Where.Slot].transform.position;
                        Assert.Less(Vector2.Distance(slot, chest.transform.position), 0.01f, where);
                    }
                    else
                    {
                        Assert.IsNull(chest.Guard, "A gate guards it with its body");
                        Assert.IsTrue(Shown(chest), $"{where}: seen behind its gate");
                        Vector2 pocket = module.Gates[spot.Where.Slot].PocketCentre;
                        Assert.Less(Vector2.Distance(pocket, chest.transform.position), 0.01f, where);
                    }
                    if (spot.What.Kind != RewardKind.Jumbles)
                        Assert.IsNotNull(chest.transform.Find("Icon"), $"{where}: its icon shows what waits there");
                }
                Assert.AreEqual(chests, world.RewardChests.Count, $"seed {seed}: one chest a planned chest");
                Assert.IsTrue(world.RewardMounds.All(m => m.GetComponent<Collider2D>().isTrigger), "A mound never blocks the way");
                Assert.AreEqual(plan.Spots.Count(s => s.Where.Kind == RoadblockKind.Mound), world.RewardMounds.Count);
            }
        }

        [UnityTest]
        public IEnumerator ALaneMound_HidesItsItem_UntilTwoBareSwingsDigItAway()
        {
            yield return Load();
            int seed = 7;
            RewardSpot spot = default;
            for (; seed < 60; seed++)
            {
                yield return Reveal(seed);
                bool OnLand(RewardSpot s) => s.What.Kind == RewardKind.Equipment && s.Where.IsLaneSpot &&
                                             !world.Layout.Faces[s.Face].IslandCells.Contains(s.Where.Screen.Cell);
                if (world.Rewards.Spots.Any(OnLand))
                {
                    spot = world.Rewards.Spots.First(OnLand);
                    break;
                }
            }
            Assert.AreEqual(RewardKind.Equipment, spot.What.Kind, "Some seed has an item under a lane mound");
            ItemPickup pickup = world.Pickups.Single(p => p != null && p.Item.HomeTheme == spot.What.Item);
            var mound = (RewardMound)pickup.Guard;
            Inventory inventory = navigator.GetComponent<Inventory>();

            navigator.TeleportTo(spot.Where.Screen, Facing.East);
            yield return new WaitForFixedUpdate();
            PlacePlayer(pickup.transform.position);
            yield return PhysicsSteps(5);
            Assert.IsFalse(pickup.Collected, "Standing on the mound takes nothing");
            Assert.IsFalse(inventory.Has(pickup.Item));

            // Two bare-handed swings from beside it, facing it.
            Vector2 at = (Vector2)mound.transform.position + Vector2.left * (attack.Reach);
            PlacePlayer(at);
            navigator.GetComponent<PlayerMover>().SetFacing(Vector2.right);
            Assert.IsNull(attack.EquippedItem, "Bare hands");
            for (int swing = 0; swing < RewardMound.HitsToOpen; swing++)
            {
                Assert.IsFalse(mound.IsOpen, $"Still shut before swing {swing + 1}");
                yield return WaitUntilOrGameTimeout(() => attack.Ready, 2f);
                Assert.GreaterOrEqual(attack.TryAttack(), 0, "The swing happened");
                yield return null;
            }
            Assert.IsTrue(mound.IsOpen, "Dug away");
            Assert.IsTrue(Shown(pickup), "The item shows");
            PlacePlayer(pickup.transform.position);
            yield return WaitUntilOrGameTimeout(() => pickup == null || pickup.Collected, 1f);
            Assert.IsTrue(inventory.Has(pickup.Item), "Then it is taken like any item");
        }

        [UnityTest]
        public IEnumerator Chests_GiveKeysBoostsAndJumbles_TheHudShowsThem_AndTheRunEndBanksThem_PastAReload()
        {
            yield return Load();
            yield return Reveal(21);
            int heart = stats.MaxHealth, speed = stats.Speed, power = stats.Power;
            RewardChest key = world.RewardChests.First(c => c.Kind == RewardKind.Key);
            RewardChest boost = world.RewardChests.First(c => c.Kind == RewardKind.Upgrade);
            RewardChest fragment = world.RewardChests.First(c => c.Kind == RewardKind.FragmentSlot);
            RewardChest jumbles = world.RewardChests.First(c => c.Kind == RewardKind.Jumbles);

            // A chest under a mound: touching it does nothing until the mound is dug, then opens it.
            RewardChest buried = world.RewardChests.First(c => c.Guard is RewardMound && c.Kind != RewardKind.Jumbles);
            navigator.TeleportTo(buried.Spot.Where.Screen, Facing.East);
            yield return new WaitForFixedUpdate();
            PlacePlayer(buried.transform.position);
            yield return PhysicsSteps(5);
            Assert.IsFalse(buried.IsOpen, "A buried chest stays shut");
            yield return RewardDig.Dig(navigator, (RewardMound)buried.Guard);
            PlacePlayer(buried.transform.position + Vector3.right * 0.01f);
            yield return WaitUntilOrGameTimeout(() => buried.IsOpen, 1f);
            Assert.IsTrue(buried.IsOpen, "Touching a dug-up chest opens it");
            Assert.IsTrue(PickupPopup.Active.Any(p => p.Text == RewardChest.PopupText(buried.Spot.What, buried.Theme)), "Its popup");
            Assert.IsFalse(buried.Open(navigator.gameObject), "A chest opens once");

            // The key.
            if (!key.IsOpen) Assert.IsTrue(key.Open(navigator.gameObject));
            Assert.AreEqual(1, rewards.Keys);
            StringAssert.Contains("Keys: 1", hud.RewardsText);

            if (!boost.IsOpen) Assert.IsTrue(boost.Open(navigator.gameObject));
            RunUpgrade kind = boost.Spot.What.Upgrade;
            Assert.AreEqual(heart + (kind == RunUpgrade.Heart ? 1 : 0), stats.MaxHealth);
            Assert.AreEqual(speed + (kind == RunUpgrade.Speed ? 1 : 0), stats.Speed);
            Assert.AreEqual(power + (kind == RunUpgrade.Power ? 1 : 0), stats.Power);
            StringAssert.Contains("Run boosts:", hud.RewardsText);

            if (!fragment.IsOpen) Assert.IsTrue(fragment.Open(navigator.gameObject));
            CollectionAssert.Contains(rewards.Fragments, (fragment.Spot.Face, fragment.Spot.What.Ordinal));

            Assert.IsTrue(jumbles.Open(navigator.gameObject));
            int chestJumbles = jumbles.Spot.What.Amount;
            Assert.AreEqual(chestJumbles, wallet.Earnings.Of(EarningSource.Chest));

            // The run ends: the key is cashed in, everything banks and is saved.
            Health health = navigator.GetComponent<Health>();
            health.InvulnerableSeconds = 0f;
            health.TakeDamage(1000f, null);
            yield return null;
            Assert.IsTrue(loop.ScreenOpen);
            int expected = chestJumbles + RewardPlacement.KeyCashIn;
            Assert.AreEqual(RewardPlacement.KeyCashIn, loop.LastRun.Earnings.Of(EarningSource.Keys));
            Assert.AreEqual(expected, loop.LastRun.Banked);
            Assert.AreEqual(0, rewards.Keys, "Cashed in");

            // The balance survives a reload (a new tab).
            yield return Load();
            Assert.AreEqual(expected, loop.Meta.Balance, "Jumbles* from roadblocks persist as meta currency");
        }

        [UnityTest]
        public IEnumerator AChest_OpensFromASwing_EvenUnderABoulder_ButNeverThroughAShutGate()
        {
            yield return Load();
            yield return Reveal(21);

            // Behind a shut gate a swing does nothing; once the gate is open it opens the chest.
            RewardChest gated = world.RewardChests.First(c => c.BlockingGate != null && !c.BlockingGate.IsOpen);
            Assert.IsFalse(gated.ReceiveAttack(null, navigator.gameObject), "No swinging through a shut gate");
            typeof(Gate).GetMethod("OpenNow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(gated.BlockingGate, null);
            Assert.IsTrue(gated.BlockingGate.IsOpen);
            Assert.IsTrue(gated.ReceiveAttack(null, navigator.gameObject), "A swing opens it once the gate is open");

            // A buried chest: dig it up, roll a boulder over it, and a swing still opens it.
            RewardChest buried = world.RewardChests.First(c => c.Guard is RewardMound);
            navigator.TeleportTo(buried.Spot.Where.Screen, Facing.East);
            yield return new WaitForFixedUpdate();
            yield return RewardDig.Dig(navigator, (RewardMound)buried.Guard);
            Assert.IsFalse(buried.IsOpen, "The swing that digs the mound away does not also open the chest");
            Boulder boulder = Boulder.Create(null, buried.transform.position, null, null);
            yield return PhysicsSteps(2);
            yield return WaitUntilOrGameTimeout(() => attack.Ready, 2f);
            Assert.GreaterOrEqual(attack.TryAttack(), 0);
            Assert.IsTrue(buried.IsOpen, "A swing opens a chest a boulder rests on");
            Object.Destroy(boulder.gameObject);
        }

        [UnityTest]
        public IEnumerator AChestBuiltAfterTheReveal_StillPays()
        {
            yield return Load();
            yield return Reveal(7);
            ScreenAddress screen = new ScreenAddress(world.Model.FaceOf(Theme.Math), 0, 0);
            var spot = new RewardSpot(999, new Roadblock(RoadblockKind.Puzzle, screen, 0), Reward.Jumbles(7), true);
            RewardChest late = world.BuildRewardChest(spot, world.ModuleAt(screen).transform, world.ScreenCenter(screen));
            CollectionAssert.Contains(world.RewardChests, late);
            Assert.IsTrue(late.Open(navigator.gameObject));
            Assert.AreEqual(7, wallet.Earnings.Of(EarningSource.Chest), "A puzzle goal's chest built after the reveal (4.10-4.12) pays");
        }

        [UnityTest]
        public IEnumerator Victory_BanksTheChestsAndTheCashedInKeys_WithNoPopupAtTheEnd()
        {
            yield return Load();
            yield return Reveal(42);
            RewardChest jumbles = world.RewardChests.First(c => c.Kind == RewardKind.Jumbles);
            Assert.IsTrue(jumbles.Open(navigator.gameObject));
            Assert.IsTrue(world.RewardChests.First(c => c.Kind == RewardKind.Key).Open(navigator.gameObject));
            yield return new WaitForSeconds(PickupPopup.Seconds + 0.2f); // the chest's popup is gone
            int popups = PickupPopup.Active.Count;
            Assert.IsTrue(runState.EndRun(true), "The run is won");
            yield return null;
            Assert.IsTrue(loop.LastRun.Victory);
            int expected = jumbles.Spot.What.Amount + RewardPlacement.KeyCashIn;
            Assert.AreEqual(expected, loop.LastRun.Banked, "A won run banks the chests and the cashed-in key");
            Assert.AreEqual(expected, MetaSave.Load(MetaSave.PrefsKey, out _).Balance, "Saved");
            Assert.AreEqual(1, loop.Meta.Victories);
            Assert.AreEqual(popups, PickupPopup.Active.Count, "No pickup popup once the run is over");
        }

        [UnityTest]
        public IEnumerator F10_DiscardsTheRun_ItsChestsAndKeysAreNotBanked()
        {
            yield return Load();
            yield return Reveal(42);
            StatBlock fresh = StatBlock.Of(stats);
            foreach (RewardChest c in world.RewardChests.Where(c => c.Kind != RewardKind.FragmentSlot)) c.Open(navigator.gameObject);
            Assert.Greater(wallet.Total, 0);
            Assert.Greater(rewards.Keys, 0);
            Assert.IsTrue(debug.StartNewRun(), "F10");
            yield return null;
            Assert.AreEqual(0, loop.Meta.Balance, "An abandoned run banks nothing");
            Assert.AreEqual(0, MetaSave.Load(MetaSave.PrefsKey, out _).Balance);
            Assert.AreEqual(0, wallet.Total);
            Assert.AreEqual(0, rewards.Keys, "Its keys are gone, not cashed in");
            Assert.AreEqual(fresh, StatBlock.Of(stats), "Its boosts are gone");
        }

        [UnityTest]
        public IEnumerator ANewRun_TakesTheBoostsBack_AndForgetsTheKeys()
        {
            yield return Load();
            yield return Reveal(42);
            StatBlock before = StatBlock.Of(stats);
            foreach (RewardChest chest in world.RewardChests.Where(c => c.Kind == RewardKind.Upgrade || c.Kind == RewardKind.Key))
                chest.Open(navigator.gameObject);
            Assert.Greater(rewards.Keys, 0);
            Assert.AreNotEqual(before, StatBlock.Of(stats));

            loop.Continue();
            yield return null;
            Assert.AreEqual(0, rewards.Keys);
            Assert.IsEmpty(rewards.Upgrades);
            Assert.AreEqual(loop.RunStats, StatBlock.Of(stats), "Base plus meta upgrades only");
            Assert.IsNull(hud.RewardsText);

            // A rebuild from elsewhere (the debug reroll) takes them back too.
            yield return Reveal(43);
            StatBlock fresh = StatBlock.Of(stats);
            world.RewardChests.First(c => c.Kind == RewardKind.Upgrade).Open(navigator.gameObject);
            world.Rebuild(44);
            yield return null;
            Assert.AreEqual(fresh, StatBlock.Of(stats));
        }
    }
}
