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
    /// Locked face-edge gates in the real Cube scene (4.9): every face-edge arch out of Town starts locked as the run's
    /// EdgeLocks plan says, with its plate (a keyhole, or the item's icon) and the HUD's key count; walking into a locked
    /// arch never crosses; a swing spends the starting key and opens every arch along that edge (both faces), and the next
    /// key arch refuses until a key is found, then Interact opens it and uses the key up; an item lock opens with its item
    /// only; a chest key opens a bonus alcove (and only a chest key), whose chest then pays its 10 Jumbles*; the map shows
    /// the locks of the screens seen, and drops them once opened. Review patches: a real swing opens an alcove; an arch on a
    /// Math island screen is tried only from its doorway's island; an Interact press beside an NPC talks, and beside a boulder
    /// with the Heavy Mitt* grabs, and never also spends a key. Every test runs on its own empty save.
    /// </summary>
    [IsolatedMetaSave]
    public class LockSceneTests : InputTestFixture
    {
        private const float K = ScreenMath.ContentScale;
        private const string SceneName = "Cube";

        private CubeWorld world;
        private CubeNavigator navigator;
        private RunRewards rewards;
        private PlayerAttack attack;
        private Rigidbody2D body;
        private Inventory inventory;
        private CombatHud hud;
        private Keyboard keyboard;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        private IEnumerator Load(int seed = 7)
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            world = Object.FindAnyObjectByType<CubeWorld>();
            navigator = Object.FindAnyObjectByType<CubeNavigator>();
            hud = Object.FindAnyObjectByType<CombatHud>();
            rewards = navigator.GetComponent<RunRewards>();
            attack = navigator.GetComponent<PlayerAttack>();
            body = navigator.GetComponent<Rigidbody2D>();
            inventory = navigator.GetComponent<Inventory>();
            world.GetComponent<BiologyFace>().SpawnEnemies = false;
            world.GetComponent<ChemistryFace>().SpawnEnemies = false;
            world.GetComponent<PhysicsFace>().SpawnEnemies = false;
            world.GetComponent<MathFace>().SpawnEnemies = false;
            world.GetComponent<EarthFace>().SpawnEnemies = false;
            world.Rebuild(seed);
            yield return null;
            yield return new WaitForFixedUpdate();
        }

        /// <summary>Puts the player still on a screen at a point local to its centre, facing a way.</summary>
        private IEnumerator Stand(ScreenAddress screen, Vector2 local, Facing facing)
        {
            navigator.TeleportTo(screen, facing);
            Vector2 at = world.ScreenCenter(screen) + local;
            body.position = at;
            body.linearVelocity = Vector2.zero;
            navigator.transform.position = new Vector3(at.x, at.y, navigator.transform.position.z);
            navigator.GetComponent<PlayerMover>().SetFacing(facing.ToVector());
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        /// <summary>Just inside a doorway of a screen, facing out of it.</summary>
        private IEnumerator StandAt(LockedArch arch) =>
            Stand(arch.Screen, (Vector2)arch.Side.ToVector() * ((arch.Side.IsHorizontal() ? CubeWorld.ScreenSize.x : CubeWorld.ScreenSize.y) / 2f -
                ScreenModule.EdgeClearance - 0.4f * K), arch.Side);

        private List<LockedArch> TownArches() => world.LockedArches.Where(a => a != null && a.Screen.Face == CubeModel.StartFace).ToList();

        [UnityTest]
        public IEnumerator TownsArches_StartLocked_AsPlanned_WithTheirPlates_AndTheHudCountsTheKey()
        {
            yield return Load();
            Assert.IsNotNull(world.Locks.Plan, "The run has locks");
            List<LockedArch> arches = TownArches();
            int n = world.FaceSize;
            Assert.AreEqual(4 * n, arches.Count, "Every face-edge doorway out of Town is a locked arch");
            foreach (LockedArch arch in arches)
            {
                EdgeLock l = world.Locks.Plan.LockOf(arch.Edge);
                Assert.AreNotEqual(LockKind.Open, l.Kind);
                Assert.IsFalse(arch.IsOpen);
                Assert.IsTrue(arch.GetComponent<BoxCollider2D>().enabled, $"{arch.name}: barred");
                Assert.IsNotNull(arch.Plate, $"{arch.name}: shows what opens it");
                if (l.Kind == LockKind.Key) Assert.AreSame(TownArt.Keyhole(false), arch.Plate.sprite, $"{arch.name}: a keyhole");
                else Assert.AreNotSame(TownArt.Keyhole(false), arch.Plate.sprite, $"{arch.name}: the item's icon");
            }
            Assert.GreaterOrEqual(arches.Select(a => a.Edge).Distinct().Count(e => world.Locks.Plan.LockOf(e).Kind == LockKind.Key),
                EdgeLocks.MinTownKeyLocks, "Several key routes out of Town");
            Assert.AreEqual(1, rewards.KeysHeld, "The run starts with a key");
            StringAssert.StartsWith("Keys: 1", hud.RewardsText);

            // Walking into a locked arch never crosses.
            LockedArch any = arches[0];
            yield return StandAt(any);
            int crossings = navigator.Crossings;
            yield return this.HoldUntil(KeyFor(keyboard, any.Side), () => navigator.Crossings > crossings, 1.2f);
            Assert.AreEqual(crossings, navigator.Crossings, "A locked arch is a wall");
            Assert.AreEqual(CubeModel.StartFace, navigator.Face);
            Assert.IsFalse(world.ScienceRevealed);
        }

        [UnityTest]
        public IEnumerator AKeyArch_ASwingSpendsTheKey_OpensTheWholeEdge_TheNextRefusesUntilAKeyIsFound()
        {
            yield return Load();
            List<CubeEdge> keyEdges = TownArches().Select(a => a.Edge).Distinct()
                .Where(e => world.Locks.Plan.LockOf(e).Kind == LockKind.Key).ToList();
            Assert.GreaterOrEqual(keyEdges.Count, 2);
            LockedArch first = TownArches().First(a => a.Edge == keyEdges[0]);
            yield return StandAt(first);
            Assert.GreaterOrEqual(attack.TryAttack(), 0, "The swing happened");
            Assert.IsTrue(first.IsOpen, "A swing with a key opens it");
            Assert.AreEqual(0, rewards.KeysHeld, "The key is used up");
            StringAssert.StartsWith("Keys: 0", hud.RewardsText);
            foreach (LockedArch a in TownArches().Where(a => a.Edge == keyEdges[0]))
                Assert.IsFalse(a.GetComponent<BoxCollider2D>().enabled, $"{a.name}: every arch along the edge opens");

            // Through it: the reveal builds the far side's arches open.
            int crossings = navigator.Crossings;
            yield return this.HoldUntil(KeyFor(keyboard, first.Side), () => navigator.Crossings > crossings, 3f);
            Assert.AreEqual(crossings + 1, navigator.Crossings, "The opened arch leads on");
            Assert.IsTrue(world.ScienceRevealed);
            FaceId far = first.Edge.A == CubeModel.StartFace ? first.Edge.B : first.Edge.A;
            Assert.AreEqual(far, navigator.Face);
            Assert.IsFalse(world.LockedArches.Any(a => a != null && a.Edge == keyEdges[0] && a.GetComponent<BoxCollider2D>().enabled),
                "No bars on the far side either");
            Assert.IsTrue(world.LockedArches.Any(a => a != null && a.Screen.Face == far && !a.IsOpen), "The far face's other edges are locked");

            // The next key arch: no key, it refuses; a found key opens it by Interact, used up.
            LockedArch second = TownArches().First(a => a.Edge == keyEdges[1]);
            yield return StandAt(second);
            Assert.AreEqual(LockOpenResult.NeedsKey, second.TryOpen(navigator.gameObject));
            Assert.IsFalse(second.IsOpen);
            Assert.IsTrue(rewards.Grant(Reward.Key(), far));
            Assert.AreEqual(1, rewards.KeysHeld);
            yield return this.Tap(keyboard.eKey, 2);
            Assert.IsTrue(second.IsOpen, "Interact by the arch spends the found key");
            Assert.AreEqual(0, rewards.Keys);
        }

        [UnityTest]
        public IEnumerator AnItemArch_OpensWithItsItemOnly_AndNeverTakesAKey()
        {
            yield return Load();
            LockedArch arch = world.LockedArches.FirstOrDefault(a => a != null && world.Locks.Plan.LockOf(a.Edge).Kind == LockKind.Item);
            Assert.IsNotNull(arch, "Seed 7 has an item lock on Town");
            Theme needed = world.Locks.Plan.LockOf(arch.Edge).Item;
            yield return StandAt(arch);
            Assert.AreEqual(LockOpenResult.NeedsItem, arch.TryOpen(navigator.gameObject), "Bare-handed");
            Theme other = CubeModel.ScienceThemes.First(t => t != needed);
            Assert.IsTrue(inventory.Add(world.ItemFor(other)));
            Assert.AreEqual(LockOpenResult.NeedsItem, arch.TryOpen(navigator.gameObject), "Another face's item");
            Assert.IsFalse(arch.IsOpen);
            Assert.AreEqual(1, rewards.KeysHeld, "A key never opens an item lock");
            Assert.IsTrue(inventory.Add(world.ItemFor(needed)));
            Assert.GreaterOrEqual(attack.TryAttack(), 0);
            Assert.IsTrue(arch.IsOpen, $"The {needed} item opens it");
            Assert.AreEqual(1, rewards.KeysHeld, "and takes nothing");
        }

        [UnityTest]
        public IEnumerator AChestKey_OpensABonusAlcove_AndOnlyAChestKeyDoes()
        {
            yield return Load();
            navigator.TeleportTo(new ScreenAddress(world.Model.FaceOf(Theme.Biology), 0, 0), Facing.East);
            yield return null;
            Assert.IsTrue(world.ScienceRevealed);
            Assert.AreEqual(world.OptionalGates.Count, world.AlcoveLocks.Count, "Every optional gate is a key-locked bonus alcove");
            Assert.Greater(world.AlcoveLocks.Count, 0);
            AlcoveLock alcove = world.AlcoveLocks[0];
            RewardChest bonus = world.RewardChests.First(c => c.Spot.Where.Kind == RoadblockKind.OptionalGate && c.BlockingGate == alcove.Gate);
            Assert.AreEqual(RewardPlacement.OptionalJumbles, bonus.Spot.What.Amount);
            Assert.IsTrue(alcove.Plate.enabled, "A pale keyhole shows it takes a chest key");

            // No chest key: an arch key does not do.
            Assert.IsTrue(rewards.Grant(Reward.Key(), FaceId.Right));
            Assert.IsFalse(alcove.TryOpen(navigator.gameObject));
            Assert.IsFalse(alcove.Gate.IsOpen);
            Assert.AreEqual(1, rewards.Keys);

            Assert.IsTrue(rewards.Grant(Reward.ChestKey(), FaceId.Right));
            StringAssert.Contains("Chest keys: 1", hud.RewardsText);
            Assert.IsTrue(alcove.ReceiveAttack(null, navigator.gameObject), "A swing with a chest key opens it");
            Assert.IsTrue(alcove.Gate.IsOpen);
            Assert.IsFalse(alcove.Plate.enabled);
            Assert.AreEqual(0, rewards.ChestKeys, "Used up");
            Assert.AreEqual(1, rewards.Keys, "The arch key is kept");
            var wallet = Object.FindAnyObjectByType<RunWallet>();
            int before = wallet.Earnings.Of(EarningSource.Chest);
            Assert.IsTrue(bonus.Open(navigator.gameObject));
            Assert.AreEqual(before + RewardPlacement.OptionalJumbles, wallet.Earnings.Of(EarningSource.Chest), "The bonus pays 10");
        }

        /// <summary>The player just outside an alcove's gate, facing it (review: the alcove tests stand where a player would).</summary>
        private IEnumerator StandBefore(AlcoveLock alcove)
        {
            GateSlot slot = alcove.Gate.GetComponentInParent<GateSlot>();
            Assert.IsNotNull(slot, "An optional gate stands in its gate slot");
            Vector2 at = (Vector2)slot.transform.position + (Vector2)slot.Opening.ToVector() * 0.9f * K;
            ScreenAddress screen = world.Model.AllScreens().OrderBy(s => (world.ScreenCenter(s) - at).sqrMagnitude).First();
            yield return Stand(screen, at - world.ScreenCenter(screen), slot.Opening.Opposite());
        }

        private IEnumerator Reveal()
        {
            navigator.TeleportTo(new ScreenAddress(world.Model.FaceOf(Theme.Biology), 0, 0), Facing.East);
            yield return null;
            Assert.IsTrue(world.ScienceRevealed);
        }

        [UnityTest]
        public IEnumerator ARealSwing_WithAChestKey_OpensABonusAlcove()
        {
            yield return Load();
            yield return Reveal();
            AlcoveLock alcove = world.AlcoveLocks[0];
            yield return StandBefore(alcove);
            Assert.IsTrue(rewards.Grant(Reward.ChestKey(), FaceId.Right));
            Assert.GreaterOrEqual(attack.TryAttack(), 0, "The swing happened");
            Assert.IsTrue(alcove.Gate.IsOpen, "PlayerAttack's swing reaches the alcove's lock");
            Assert.AreEqual(0, rewards.ChestKeys, "and spends the chest key");
        }

        [UnityTest]
        public IEnumerator OnAnIslandScreen_AnArchIsTriedOnlyFromItsDoorwaysIsland()
        {
            yield return Load();
            LockedArch arch = null;
            IslandLayout islands = null;
            foreach (int seed in new[] { 7, 21, 42, 1, 2, 3, 4, 5 })
            {
                world.Rebuild(seed);
                yield return null;
                yield return Reveal();
                MathPlan plan = world.GetComponent<MathFace>().Plan;
                arch = world.LockedArches.FirstOrDefault(a => a != null && !a.IsOpen && plan.IslandAt(a.Screen) != null &&
                                                              plan.IslandAt(a.Screen).DoorIsland(a.Side) >= 0 &&
                                                              plan.IslandAt(a.Screen).Islands.Count > 1);
                if (arch == null) continue;
                islands = plan.IslandAt(arch.Screen);
                break;
            }
            Assert.IsNotNull(arch, "Some seed has a locked arch on an island screen");
            int door = islands.DoorIsland(arch.Side);
            int other = Enumerable.Range(0, islands.Islands.Count).First(i => i != door);
            Assert.IsTrue(rewards.Grant(Reward.Key(), FaceId.Right));
            foreach (Theme t in CubeModel.ScienceThemes) inventory.Add(world.ItemFor(t));
            yield return Stand(arch.Screen, islands.Islands[other].center, arch.Side);
            Assert.AreEqual(LockOpenResult.OutOfReach, arch.TryOpen(navigator.gameObject), "From another island, as the model says");
            Assert.IsFalse(arch.IsOpen);
            yield return Stand(arch.Screen, islands.Islands[door].center, arch.Side);
            Assert.AreEqual(LockOpenResult.Opened, arch.TryOpen(navigator.gameObject), "From the doorway's own island");
        }

        [UnityTest]
        public IEnumerator AnNpcBesideAnArch_TakesTheInteract_AndNoKeyIsSpent()
        {
            yield return Load();
            LockedArch arch = TownArches().First(a => world.Locks.Plan.LockOf(a.Edge).Kind == LockKind.Key);
            yield return StandAt(arch);
            List<NpcTalker> npcs = Object.FindAnyObjectByType<CubeDebug>().SpawnNpcs();
            NpcTalker target = npcs[0];
            foreach (NpcTalker other in Object.FindObjectsByType<NpcTalker>(FindObjectsSortMode.None))
                if (other != target) other.gameObject.SetActive(false);
            if (target.TryGetComponent(out NpcMover npcMover)) npcMover.enabled = false;
            Vector2 at = (Vector2)navigator.transform.position - (Vector2)arch.Side.ToVector() * 1f * K;
            target.GetComponent<Rigidbody2D>().position = at;
            target.transform.position = at;
            yield return new WaitForFixedUpdate();
            yield return null;

            yield return this.Tap(keyboard.eKey, 2);
            Assert.IsTrue(DialogueBox.Shared.IsOpen, "The press talks to the NPC");
            Assert.IsFalse(arch.IsOpen, "and never also tries the arch");
            Assert.AreEqual(1, rewards.KeysHeld, "No key spent");
            yield return this.Tap(keyboard.escapeKey, 2);
            Assert.IsFalse(DialogueBox.Shared.IsOpen);
            target.gameObject.SetActive(false);
            yield return null;
            yield return this.Tap(keyboard.eKey, 2);
            Assert.IsTrue(arch.IsOpen, "With nobody to talk to, Interact uses the key");
        }

        [UnityTest]
        public IEnumerator TheMitt_BesideAnAlcove_TakesTheInteract_AndNoChestKeyIsSpent()
        {
            yield return Load();
            yield return Reveal();
            var mitt = navigator.GetComponent<MassMitt>();
            Assert.IsNotNull(mitt.Item, "The reveal set up the run's Heavy Mitt*");
            AlcoveLock alcove = world.AlcoveLocks[0];
            yield return StandBefore(alcove);
            Assert.IsTrue(rewards.Grant(Reward.ChestKey(), FaceId.Right));
            inventory.Add(mitt.Item);
            navigator.GetComponent<Equipment>().Equip(mitt.Item);
            Assert.IsTrue(mitt.IsEquipped);
            Boulder boulder = Boulder.Create(null, (Vector2)navigator.transform.position + (Vector2)Vector2.Perpendicular(navigator.GetComponent<PlayerMover>().Facing) * 1.2f * K, null, null);
            yield return new WaitForFixedUpdate();
            yield return null;
            yield return this.Tap(keyboard.eKey, 2);
            Assert.AreSame(boulder, mitt.Held, "The press grabs the boulder");
            Assert.IsFalse(alcove.Gate.IsOpen, "and never also opens the alcove");
            Assert.AreEqual(1, rewards.ChestKeys, "No chest key spent");
        }

        [UnityTest]
        public IEnumerator TheMap_ShowsTheLocksOfTheScreensSeen_AndDropsThemOnceOpened()
        {
            yield return Load();
            var tracker = Object.FindAnyObjectByType<VisitTracker>();
            var map = Object.FindAnyObjectByType<CubeMap>();
            map.Refresh();
            Assert.IsFalse(map.Markers.Any(m => m.Kind == MapMarkerKind.Lock), "The start screen has no face edge");
            LockedArch arch = TownArches().First(a => world.Locks.Plan.LockOf(a.Edge).Kind == LockKind.Key);
            tracker.Visit(arch.Screen);
            map.Refresh();
            MapMarker marker = map.Markers.Single(m => m.Kind == MapMarkerKind.Lock && m.Screen == arch.Screen &&
                                                      Vector2.Dot(m.Local - new Vector2(0.5f, 0.5f), arch.Side.ToVector()) > 0.3f);
            Assert.AreEqual(0, marker.Detail, "A key lock");
            Assert.IsTrue(map.Markers.Where(m => m.Kind == MapMarkerKind.Lock).All(m => tracker.IsVisited(m.Screen)), "Only seen screens");
            LockedArch item = TownArches().FirstOrDefault(a => world.Locks.Plan.LockOf(a.Edge).Kind == LockKind.Item);
            if (item != null)
            {
                tracker.Visit(item.Screen);
                map.Refresh();
                Assert.IsTrue(map.Markers.Any(m => m.Kind == MapMarkerKind.Lock && m.Screen == item.Screen &&
                                                   m.Detail == 1 + (int)world.Locks.Plan.LockOf(item.Edge).Item), "An item lock shows its item's colour");
            }
            Assert.AreEqual(LockOpenResult.Opened, arch.TryOpen(navigator.gameObject));
            map.Refresh();
            Assert.IsFalse(map.Markers.Any(m => m.Kind == MapMarkerKind.Lock && m.Screen == arch.Screen &&
                                               Vector2.Dot(m.Local - new Vector2(0.5f, 0.5f), arch.Side.ToVector()) > 0.3f), "Opened: gone");
            Assert.IsTrue(CubeMap.LegendLabels.Contains("Locked arch"));
        }
    }
}
