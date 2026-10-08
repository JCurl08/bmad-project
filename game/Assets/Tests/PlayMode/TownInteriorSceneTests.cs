using System.Collections;
using System.Linq;
using NUnit.Framework;
using Game.Tracer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Game.Cube.Tests.TestInput;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Enterable buildings (4.6) in the real Cube scene: on several seeds, walking north into every town's marked door
    /// goes into its race's room (its own screen off the net, its people in place, the camera on it, the player's screen
    /// still the town), and walking south out of the room's exit puts the player back outside that door facing away; walking
    /// past the door or into another building does nothing; the pause map works inside and shows the town with the inside
    /// badge; and the debug keys behave inside (F3/F7 spawn in the room, F8 walks out, F6 and F10 take the player out).
    /// </summary>
    public class TownInteriorSceneTests : InputTestFixture
    {
        private const string SceneName = "Cube";

        /// <summary>How far in front of a door (from the sill to the player's centre) the walks start.</summary>
        private const float Approach = 1.8f;

        private Keyboard keyboard;
        private CubeWorld world;
        private CubeNavigator navigator;
        private TownInteriors interiors;
        private ScreenCamera screenCamera;
        private CubeMap map;
        private VisitTracker tracker;
        private CubeDebug debug;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        public override void TearDown()
        {
            GamePause.Resume();
            Time.timeScale = 1f;
            base.TearDown();
        }

        private IEnumerator Load(bool faceEnemies = false)
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            world = Object.FindAnyObjectByType<CubeWorld>();
            navigator = Object.FindAnyObjectByType<CubeNavigator>();
            interiors = Object.FindAnyObjectByType<TownInteriors>();
            screenCamera = Object.FindAnyObjectByType<ScreenCamera>();
            map = Object.FindAnyObjectByType<CubeMap>();
            tracker = Object.FindAnyObjectByType<VisitTracker>();
            debug = Object.FindAnyObjectByType<CubeDebug>();
            Assert.IsNotNull(interiors, "The Cube scene has TownInteriors");
            Assert.AreSame(world, interiors.World);
            Assert.AreSame(navigator, interiors.Navigator);
            world.GetComponent<BiologyFace>().SpawnEnemies = faceEnemies;
            world.GetComponent<ChemistryFace>().SpawnEnemies = faceEnemies;
            world.GetComponent<PhysicsFace>().SpawnEnemies = faceEnemies;
            world.GetComponent<MathFace>().SpawnEnemies = faceEnemies;
            world.GetComponent<EarthFace>().SpawnEnemies = faceEnemies;
            navigator.GetComponent<PlayerStats>().MaxHealth = 999;
        }

        /// <summary>Every enemy that is not a (dormant) hostile NPC's.</summary>
        private static Enemy[] Enemies() =>
            Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None).Where(e => e.GetComponent<NpcTalker>() == null).ToArray();

        private ScreenAddress TownOf(FaceId face) => new ScreenAddress(face, TownSquare.Cell(world.FaceSize));

        private Vector2 PlayerPosition => navigator.GetComponent<Rigidbody2D>().position;

        /// <summary>Puts the player Approach in front of a point on a town's lane, facing a way, and lets a frame pass.</summary>
        private IEnumerator StandBefore(ScreenAddress town, Vector2 point, Facing facing)
        {
            navigator.PlaceOn(town, point + Vector2.down * Approach, facing);
            navigator.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForFixedUpdate();
            yield return null;
            yield return null;
        }

        /// <summary>Walks in through a town's marked door (north) and checks the room; returns with the player inside.</summary>
        private IEnumerator WalkIn(FaceId face, string context)
        {
            ScreenAddress town = TownOf(face);
            TownDoor door = interiors.DoorAt(town);
            Assert.IsNotNull(door, $"{context}: {face}'s town has a marked door");
            yield return StandBefore(town, door.Sill, Facing.North);
            Assert.IsFalse(navigator.InInterior, $"{context}: not inside before walking in");
            bool firstVisit = !interiors.Rooms.ContainsKey(face);
            int enemiesBefore = Enemies().Length;
            yield return this.HoldUntil(keyboard.wKey, () => navigator.InInterior, 3f);
            yield return null;
            yield return null;
            Assert.IsTrue(navigator.InInterior, $"{context}: walking into {face}'s marked door goes in");

            TownInteriorView room = interiors.Current;
            Assert.IsNotNull(room, context);
            Assert.AreEqual(face, room.Face, context);
            Assert.AreSame(door, interiors.CurrentDoor, context);
            Theme theme = world.Model.ThemeOf(face);
            Assert.AreEqual(TownInterior.KindFor(theme), room.Interior.Kind, $"{context}: {theme}'s room");
            Assert.AreEqual(town, navigator.Current, $"{context}: the player's screen stays the town");
            Assert.AreEqual(face, navigator.Face, context);
            Assert.AreEqual(TownInteriors.RoomScreen(face), screenCamera.CurrentScreen, $"{context}: the camera frames the room");
            Assert.IsFalse(world.TryScreenAt(PlayerPosition, out _), $"{context}: the room is off the cube net");
            Assert.IsTrue(room.Inner.Contains(PlayerPosition), $"{context}: the player is in the room");

            Race race = TownSquare.RaceOf(theme);
            Assert.AreEqual(TownInterior.PeopleCount, room.People.Count(p => p != null), $"{context}: people inside");
            foreach (NpcTalker person in room.People)
            {
                Assert.AreEqual(race, person.Race, $"{context}: {person.name} is of the town's race");
                Assert.IsTrue(room.Inner.Contains(person.transform.position), $"{context}: {person.name} is in the room");
                Assert.Greater(person.Lines.Count, 0, $"{context}: {person.name} talks");
            }
            bool shop = room.Interior.Kind == InteriorKind.Shop;
            Assert.AreEqual(shop, room.Keeper != null, $"{context}: a keeper only in a shop");
            if (shop) CollectionAssert.AreEqual(Dialogue.KeeperLines(room.Keeper.Spec.Voice), room.Keeper.Lines, $"{context}: the keeper talks shop");
            RunFacts facts = RunFacts.Of(world);
            foreach (NpcTalker person in room.People.Where(p => p != room.Keeper))
                CollectionAssert.AreEqual(Dialogue.For(person.Spec, facts, interiors.HintDensity), person.Lines,
                    $"{context}: {person.name} talks like the town's residents");
            // No enemies: building the room adds none (its people's dormant hostile parts aside), and none stands inside.
            Assert.AreEqual(enemiesBefore, Enemies().Length, $"{context}: {(firstVisit ? "building" : "entering")} the room adds no enemy");
            Assert.IsEmpty(room.GetComponentsInChildren<Enemy>(true).Where(e => e.GetComponent<NpcTalker>() == null), $"{context}: no enemy in the room");
            foreach (Enemy enemy in Enemies())
                Assert.IsFalse(room.Inner.Contains(enemy.transform.position), $"{context}: no enemy inside");
        }

        /// <summary>Walks out (south) of the room the player is in and checks they stand outside its door facing away.</summary>
        private IEnumerator WalkOut(FaceId face, string context)
        {
            TownDoor door = interiors.CurrentDoor;
            ScreenAddress town = TownOf(face);
            int exits = interiors.Exits;
            yield return this.HoldUntil(keyboard.sKey, () => !navigator.InInterior, 3f);
            Vector2 justOut = PlayerPosition;
            yield return null;
            yield return null;
            Assert.IsFalse(navigator.InInterior, $"{context}: walking out of the exit leaves the room");
            Assert.AreEqual(exits + 1, interiors.Exits, context);
            Assert.IsNull(interiors.Current, context);
            Assert.AreEqual(town, navigator.Current, $"{context}: back on the town");
            Assert.AreEqual(TownInteriors.ReturnPoint(door).x, justOut.x, 0.05f, $"{context}: outside the door");
            Assert.AreEqual(TownInteriors.ReturnPoint(door).y, justOut.y, 0.6f, $"{context}: just in front of the door");
            Assert.Less(justOut.y, door.Sill.y - TimeField.PlayerRadius, $"{context}: outside, not in the wall");
            Assert.AreEqual(Facing.South, navigator.Facing, $"{context}: facing away from the door");
            Assert.AreEqual(Vector2.down, navigator.GetComponent<PlayerMover>().Facing, $"{context}: facing away from the door");
            Assert.AreEqual(ScreenMath.ScreenIndex(world.ScreenCenter(town)), screenCamera.CurrentScreen, $"{context}: the camera is back on the town");
            // Standing there does not go straight back in.
            yield return PhysicsSteps(10);
            Assert.IsFalse(navigator.InInterior, $"{context}: no re-entry while facing away");
        }

        [UnityTest]
        public IEnumerator EveryTownsMarkedDoor_LeadsIntoItsRoom_AndOutAgain_OnSeveralSeeds()
        {
            yield return Load(faceEnemies: true);
            foreach (int seed in new[] { 7, 21, 42 })
            {
                world.Rebuild(seed);
                yield return null;
                yield return null;
                // Town's room works before the reveal too, and walking out of it never reveals the science faces.
                yield return WalkIn(CubeModel.StartFace, $"seed {seed}, before the reveal");
                yield return WalkOut(CubeModel.StartFace, $"seed {seed}, before the reveal");
                Assert.IsFalse(world.ScienceRevealed, $"seed {seed}: Town's room reveals nothing");

                world.RevealScience();
                yield return null;
                int checkedTowns = 0;
                for (int f = 0; f < CubeSettings.FaceCount; f++)
                {
                    var face = (FaceId)f;
                    if (world.Model.IsSealed(face)) continue;
                    string context = $"seed {seed}, {face} ({world.Model.ThemeOf(face)})";
                    yield return WalkIn(face, context);
                    yield return WalkOut(face, context);
                    checkedTowns++;
                }
                Assert.AreEqual(CubeSettings.FaceCount, checkedTowns, $"seed {seed}: every face's town");
                Assert.Greater(Enemies().Length, 0, $"seed {seed}: the faces have their enemies (so the no-enemy checks mean something)");
                Assert.AreEqual(CubeSettings.FaceCount, interiors.Rooms.Count, $"seed {seed}: a room per town");
                Assert.AreEqual(interiors.Entries, interiors.Exits, seed.ToString());
            }
        }

        [UnityTest]
        public IEnumerator WalkingPastTheDoor_OrIntoAnotherBuilding_DoesNotGoIn()
        {
            yield return Load();
            ScreenAddress town = world.Model.StartScreen;
            TownDoor door = interiors.DoorAt(town);
            Assert.IsNotNull(door);
            // Along the lane in front of the door, from west of it to east of it.
            navigator.PlaceOn(town, door.Sill + new Vector2(-3f, -(TimeField.PlayerRadius + 0.1f)), Facing.East);
            yield return null;
            yield return this.HoldUntil(keyboard.dKey, () => PlayerPosition.x > door.Sill.x + 2.5f, 2f);
            yield return null;
            Assert.Greater(PlayerPosition.x, door.Sill.x + 2f, "walked past the door");
            Assert.IsFalse(navigator.InInterior, "walking past the door along the lane does not go in");

            // North into the other buildings' doors.
            TownSquareView view = world.ModuleAt(town).GetComponentInChildren<TownSquareView>();
            foreach (TownBuilding b in view.Layout.Buildings.Where(b => !b.Enterable))
            {
                Vector2 sill = world.ScreenCenter(town) + b.Door;
                yield return StandBefore(town, sill, Facing.North);
                yield return this.HoldUntil(keyboard.wKey, () => navigator.InInterior, 0.6f);
                yield return null;
                Assert.IsFalse(navigator.InInterior, $"{b.Kind}'s door is not enterable");
            }
            Assert.AreEqual(0, interiors.Entries);
        }

        [UnityTest]
        public IEnumerator Inside_ThePauseMapShowsTheTown_WithTheInsideBadge_AndStillPauses()
        {
            yield return Load();
            world.RevealScience();
            yield return null;
            FaceId face = world.Model.FaceOf(Theme.Physics);
            yield return WalkIn(face, "Physics");
            int visited = tracker.Visits.Count;

            yield return this.Tap(keyboard.mKey, holdFrames: 2);
            Assert.IsTrue(map.IsOpen, "M opens the map inside a building");
            Assert.IsTrue(GamePause.Active);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(map.PlayerInside);
            Assert.IsFalse(map.PlayerInCore);
            Assert.AreEqual(TownOf(face), map.PlayerScreen, "the map shows the town's screen");
            Assert.IsTrue(map.CellView(TownOf(face)).Visited);
            Assert.AreEqual(CubeMap.InsideText(InteriorKind.Home), map.InsideLabel, "the inside badge names the room");
            Assert.AreEqual(interiors.CurrentDoor.Sill, map.PlayerMarkerPoint, "the marker stands at the door");

            Vector2 before = PlayerPosition;
            Press(keyboard.sKey, queueEventOnly: true);
            yield return WaitRealSeconds(0.3f);
            Release(keyboard.sKey, queueEventOnly: true);
            yield return null;
            Assert.AreEqual(before, PlayerPosition, "paused: the player does not move");
            Assert.IsTrue(navigator.InInterior);

            yield return this.Tap(keyboard.mKey, holdFrames: 2);
            Assert.IsFalse(map.IsOpen, "M closes it");
            Assert.IsFalse(GamePause.Active);
            Assert.AreEqual(visited, tracker.Visits.Count, "being inside visits no other screen");

            yield return WalkOut(face, "Physics");
            Assert.IsFalse(map.PlayerInside);
            Assert.IsNull(map.InsideLabel, "no badge outside");
        }

        [UnityTest]
        public IEnumerator DebugKeys_Inside_SpawnInTheRoom_F8WalksOut_F6AndANewRunTakeThePlayerOut()
        {
            yield return Load();
            ScreenAddress town = world.Model.StartScreen;
            yield return WalkIn(CubeModel.StartFace, "Town");
            Vector2Int roomScreen = TownInteriors.RoomScreen(CubeModel.StartFace);

            StringAssert.Contains(CubeDebug.InteriorLabel + " shop", debug.OverlayText, "the overlay names the room");
            yield return this.Tap(keyboard.f3Key, holdFrames: 2);
            Assert.AreEqual(RaceExtensions.All.Length, debug.DebugNpcs.Count, "F3 spawns");
            foreach (NpcTalker npc in debug.DebugNpcs)
                Assert.AreEqual(roomScreen, ScreenMath.ScreenIndex(npc.transform.position), "F3 spawns inside the room");
            yield return this.Tap(keyboard.f7Key, holdFrames: 2);
            Assert.AreEqual(1, debug.DebugEnemies.Count, "F7 spawns");
            Assert.AreEqual(roomScreen, ScreenMath.ScreenIndex(debug.DebugEnemies[0].transform.position), "F7 spawns inside the room");
            Object.Destroy(debug.DebugEnemies[0].gameObject);
            yield return null;

            TownDoor door = interiors.CurrentDoor;
            yield return this.Tap(keyboard.f8Key, holdFrames: 2);
            Assert.IsFalse(navigator.InInterior, "F8 walks the player out");
            Assert.AreEqual(town, navigator.Current);
            Assert.Less(Vector2.Distance(TownInteriors.ReturnPoint(door), PlayerPosition), 0.3f, "outside the door");
            Assert.AreEqual(Facing.South, navigator.Facing, "facing away");
            Assert.IsFalse(debug.LeaveCoreArena(), "F8 outside does nothing");

            yield return WalkIn(CubeModel.StartFace, "Town again");
            yield return this.Tap(keyboard.f6Key, holdFrames: 2);
            yield return PhysicsSteps(2);
            Assert.IsFalse(navigator.InInterior, "F6 jumps out of the room");
            Assert.IsNull(interiors.Current);
            Assert.AreNotEqual(TownInteriors.RoomScreen(CubeModel.StartFace), screenCamera.CurrentScreen);
            Assert.IsTrue(world.TryScreenAt(PlayerPosition, out _), "back on the net");

            navigator.TeleportTo(world.Model.StartScreen, Facing.East);
            yield return WalkIn(CubeModel.StartFace, "Town a third time");
            int seed = world.Seed;
            yield return this.Tap(keyboard.f10Key, holdFrames: 2);
            yield return PhysicsSteps(2);
            Assert.AreNotEqual(seed, world.Seed, "F10 starts a new run");
            Assert.IsFalse(navigator.InInterior, "a new run starts outside");
            Assert.AreEqual(world.Model.StartScreen, navigator.Current, "in Town");
            Assert.AreEqual(0, interiors.Rooms.Count, "a new run clears the rooms");
            Assert.AreEqual(0, interiors.Entries);

            yield return WalkIn(CubeModel.StartFace, "the new run's Town");
            yield return WalkOut(CubeModel.StartFace, "the new run's Town");
        }

        [UnityTest]
        public IEnumerator StandingAtTheDoor_FacingIt_DoesNotGoIn_UntilThePlayerPushesNorth()
        {
            yield return Load();
            ScreenAddress town = world.Model.StartScreen;
            TownDoor door = interiors.DoorAt(town);
            // Back at the return point, but turned to face the door: nothing happens while no key is pressed.
            navigator.PlaceOn(town, TownInteriors.ReturnPoint(door), Facing.North);
            yield return PhysicsSteps(15);
            Assert.IsFalse(navigator.InInterior, "facing the door from the return point does not go in");
            // Even touching the sill, facing it, standing still: no push, no entry.
            navigator.PlaceOn(town, door.Sill + Vector2.down * (TimeField.PlayerRadius + 0.05f), Facing.North);
            yield return PhysicsSteps(15);
            Assert.IsFalse(TownInteriors.AtDoor(door, PlayerPosition, navigator), "not walking into it");
            Assert.IsFalse(navigator.InInterior, "standing at the door facing it does not go in");
            // A dead player pressed against the door is never pulled in.
            var health = navigator.GetComponent<Health>();
            health.InvulnerableSeconds = 0f;
            health.TakeDamage(100000f, null);
            Assert.IsTrue(health.IsDead);
            Press(keyboard.wKey, queueEventOnly: true);
            yield return PhysicsSteps(15);
            Release(keyboard.wKey, queueEventOnly: true);
            yield return null;
            Assert.IsFalse(navigator.InInterior, "a dead player is not pulled inside");
            // A new run; from the return point, facing the door, pressing north goes in.
            Object.FindAnyObjectByType<RunLoop>().Continue();
            yield return null;
            yield return null;
            town = world.Model.StartScreen;
            door = interiors.DoorAt(town);
            navigator.PlaceOn(town, TownInteriors.ReturnPoint(door), Facing.North);
            yield return PhysicsSteps(5);
            Assert.IsFalse(navigator.InInterior);
            yield return null;
            yield return this.HoldUntil(keyboard.wKey, () => navigator.InInterior, 2f);
            Assert.IsTrue(navigator.InInterior, "pressing north goes in");
        }

        [UnityTest]
        public IEnumerator F5Inside_RerollsAndStartsOutside_WithNoRoomsLeft()
        {
            yield return Load();
            yield return WalkIn(CubeModel.StartFace, "Town");
            int seed = world.Seed;
            yield return this.Tap(keyboard.f5Key, holdFrames: 2);
            yield return PhysicsSteps(2);
            Assert.AreNotEqual(seed, world.Seed, "F5 rerolls");
            Assert.IsFalse(navigator.InInterior, "a reroll starts outside");
            Assert.IsNull(interiors.Current);
            Assert.AreEqual(0, interiors.Rooms.Count, "a reroll clears the rooms");
            Assert.AreEqual(world.Model.StartScreen, navigator.Current);
            Assert.AreEqual(ScreenMath.ScreenIndex(world.ScreenCenter(world.Model.StartScreen)), screenCamera.CurrentScreen);
            yield return WalkIn(CubeModel.StartFace, "Town after F5");
            yield return WalkOut(CubeModel.StartFace, "Town after F5");
        }

        [UnityTest]
        public IEnumerator DyingInside_EndsTheRun_AndTheNextRunStartsCleanOutside()
        {
            yield return Load();
            world.RevealScience();
            yield return null;
            FaceId face = world.Model.FaceOf(Theme.Chemistry);
            yield return WalkIn(face, "Chemistry");
            RunLoop loop = Object.FindAnyObjectByType<RunLoop>();
            var health = navigator.GetComponent<Health>();
            health.InvulnerableSeconds = 0f;
            health.TakeDamage(100000f, null);
            yield return null;
            Assert.IsTrue(health.IsDead);
            Assert.IsTrue(loop.ScreenOpen, "dying inside ends the run (the between-runs screen)");

            int seed = world.Seed;
            int newSeed = loop.Continue();
            yield return null;
            yield return PhysicsSteps(2);
            Assert.AreNotEqual(seed, newSeed);
            Assert.IsFalse(loop.ScreenOpen);
            Assert.IsFalse(health.IsDead, "alive again");
            Assert.IsTrue(navigator.GetComponent<PlayerMover>().enabled, "moving again");
            Assert.IsFalse(navigator.InInterior, "the next run starts outside");
            Assert.IsNull(interiors.Current);
            Assert.AreEqual(0, interiors.Rooms.Count, "no room left from the last run");
            Assert.AreEqual(0, interiors.Entries);
            Assert.AreEqual(world.Model.StartScreen, navigator.Current, "in Town");
            Assert.IsTrue(world.TryScreenAt(PlayerPosition, out _), "on the net");
            Assert.IsFalse(map.PlayerInside);
            yield return WalkIn(CubeModel.StartFace, "the next run's Town");
            yield return WalkOut(CubeModel.StartFace, "the next run's Town");
        }
    }
}
