using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Game.Tracer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Game.Cube.Tests.TestInput;
using Object = UnityEngine.Object;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The boss lair (4.13) in the real Cube scene: on seeds 1-50 exactly one core portal leads to the arena and every other
    /// leads down into its face's lair room, with its prisoners behind bars talking in their voices; stepping into a lair
    /// portal goes down (the camera on the room's own screen, off the net, the player's screen still the portal's) and
    /// walking out of the exit puts the player back beside the portal, facing away from it, with no re-entry; the pause map
    /// shows the lair badge, the visited lair's barred portal and, once a prisoner names it (or the player uses it), the real
    /// portal in gold; and the debug keys behave down there (F3/F7 spawn on the walkway, F8 walks out, F6 and a new run take
    /// the player out).
    /// </summary>
    public class LairSceneTests : InputTestFixture
    {
        private const string SceneName = "Cube";
        private const float K = ScreenMath.ContentScale;

        private Keyboard keyboard;
        private CubeWorld world;
        private CubeNavigator navigator;
        private LairRooms lair;
        private CoreArena arena;
        private ScreenCamera screenCamera;
        private CubeMap map;
        private VisitTracker tracker;
        private CubeDebug debug;
        private Rigidbody2D body;
        private PlayerMover mover;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        public override void TearDown()
        {
            GamePause.Resume();
            Time.timeScale = 1f;
            DialogueBox box = DialogueBox.Shared;
            if (box != null && box.IsOpen) box.Close();
            base.TearDown();
        }

        private IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            world = Object.FindAnyObjectByType<CubeWorld>();
            navigator = Object.FindAnyObjectByType<CubeNavigator>();
            lair = Object.FindAnyObjectByType<LairRooms>();
            arena = Object.FindAnyObjectByType<CoreArena>();
            screenCamera = Object.FindAnyObjectByType<ScreenCamera>();
            map = Object.FindAnyObjectByType<CubeMap>();
            tracker = Object.FindAnyObjectByType<VisitTracker>();
            debug = Object.FindAnyObjectByType<CubeDebug>();
            body = navigator.GetComponent<Rigidbody2D>();
            mover = navigator.GetComponent<PlayerMover>();
            Assert.IsNotNull(lair, "The Cube scene has LairRooms");
            Assert.AreSame(world, lair.World);
            Assert.AreSame(navigator, lair.Navigator);
            Assert.AreSame(lair, arena.Lair, "The arena knows the lair");
            NoFaceEnemies();
            navigator.GetComponent<PlayerStats>().MaxHealth = 999;
        }

        private void NoFaceEnemies()
        {
            world.GetComponent<BiologyFace>().SpawnEnemies = false;
            world.GetComponent<ChemistryFace>().SpawnEnemies = false;
            world.GetComponent<PhysicsFace>().SpawnEnemies = false;
            world.GetComponent<MathFace>().SpawnEnemies = false;
            world.GetComponent<EarthFace>().SpawnEnemies = false;
        }

        private Vector2 PlayerPosition => body.position;

        /// <summary>A new run on a seed with the science faces revealed (a teleport onto a built face), the player in Town.</summary>
        private IEnumerator Reveal(int seed)
        {
            world.Rebuild(seed);
            yield return null;
            NoFaceEnemies();
            Assert.IsTrue(world.RevealScience() || world.ScienceRevealed);
            yield return null;
            navigator.TeleportTo(world.Model.StartScreen, Facing.East);
            yield return new WaitForFixedUpdate();
        }

        private CorePortal PortalOn(FaceId face) =>
            arena.Portals.Single(p => p.TryScreen(out ScreenAddress s) && s.Face == face);

        private ScreenAddress ScreenOf(CorePortal portal)
        {
            Assert.IsTrue(portal.TryScreen(out ScreenAddress s));
            return s;
        }

        /// <summary>Steers the player (the mover off, as CoreSceneTests' MoveTo) towards a point until a condition holds.</summary>
        private IEnumerator SteerTo(Vector2 to, Func<bool> stop, float timeout = 4f)
        {
            mover.enabled = false;
            float t = 0f;
            while (t < timeout && !stop())
            {
                Vector2 d = to - body.position;
                if (d.magnitude < 0.02f * K) break;
                body.linearVelocity = d.normalized * Mathf.Min(TimeField.BasePlayerSpeed, d.magnitude / Time.fixedDeltaTime);
                yield return new WaitForFixedUpdate();
                t += Time.fixedDeltaTime;
            }
            body.linearVelocity = Vector2.zero;
            mover.enabled = true;
        }

        /// <summary>Walks into a lair portal from beside it and checks the room; returns with the player down there.</summary>
        private IEnumerator WalkDown(CorePortal portal, string context)
        {
            ScreenAddress screen = ScreenOf(portal);
            Vector2 at = portal.transform.position;
            Vector2 centre = world.ScreenCenter(screen);
            navigator.PlaceOn(screen, LairRooms.ReturnPoint(at, centre), LairRooms.ReturnFacing(at, centre));
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(navigator.InLair, $"{context}: not down before stepping in");
            int entries = lair.Entries;
            yield return SteerTo(at, () => navigator.InLair);
            yield return null;
            Assert.IsTrue(navigator.InLair, $"{context}: stepping into a lair portal goes down");
            Assert.IsTrue(navigator.InInterior, $"{context}: a lair room is a kind of interior");
            Assert.IsFalse(navigator.InCoreArena, $"{context}: not the arena");
            Assert.IsFalse(arena.FightActive, $"{context}: no fight");
            Assert.AreEqual(entries + 1, lair.Entries, context);
            CheckRoom(screen, context);
        }

        private void CheckRoom(ScreenAddress screen, string context)
        {
            LairRoomView room = lair.Current;
            Assert.IsNotNull(room, context);
            Assert.AreEqual(screen.Face, room.Face, context);
            Assert.AreEqual(screen, room.PortalScreen, context);
            Assert.AreEqual(screen, navigator.Current, $"{context}: the player's screen stays the portal's");
            Assert.AreEqual(LairRooms.RoomScreen(screen.Face), ScreenMath.ScreenIndex(PlayerPosition), $"{context}: on the room's screen");
            Assert.IsFalse(world.TryScreenAt(PlayerPosition, out _), $"{context}: the room is off the cube net");
            Assert.IsTrue(room.Walkway.Contains(PlayerPosition), $"{context}: the player is on the walkway");
            Assert.IsTrue(world.LairProgress.IsVisited(screen.Face), $"{context}: the room counts as visited");

            IReadOnlyList<PrisonerPlan> plans = world.Lair.PrisonersOf(screen.Face);
            Assert.AreEqual(LairPlan.PrisonersPerRoom, room.Prisoners.Count(p => p != null), $"{context}: a prisoner in every cell");
            for (int i = 0; i < room.Prisoners.Count; i++)
            {
                NpcTalker prisoner = room.Prisoners[i];
                Assert.AreEqual(plans[i].Race, prisoner.Race, $"{context}: prisoner {i}'s race");
                CollectionAssert.AreEqual(world.Lair.LinesOf(plans[i], world.Model), prisoner.Lines, $"{context}: prisoner {i}'s hint");
                Assert.IsFalse(prisoner.IsHostile, $"{context}: prisoners are never hostile");
                Vector2 local = (Vector2)prisoner.transform.position - room.Centre;
                Assert.IsTrue(LairRoom.Cell(i).Contains(local), $"{context}: prisoner {i} is in their cell ({local})");
            }
            Assert.IsTrue(plans.Any(p => p.Hint == PrisonerHint.RealPortal), $"{context}: a hint-giver names the real portal");
        }

        /// <summary>Walks out (south) of the room and checks the player stands beside the portal, facing away from it.</summary>
        private IEnumerator WalkUp(CorePortal portal, string context)
        {
            ScreenAddress screen = ScreenOf(portal);
            int exits = lair.Exits;
            Vector2 back = LairRooms.ReturnPoint(portal.transform.position, world.ScreenCenter(screen));
            yield return null; // input state is only valid in dynamic update (never press right after WaitForFixedUpdate)
            yield return this.HoldUntil(keyboard.sKey, () => !navigator.InLair, 4f);
            Vector2 justOut = PlayerPosition;
            yield return null;
            Assert.IsFalse(navigator.InLair, $"{context}: walking out of the exit leaves the lair");
            Assert.IsFalse(navigator.InInterior, context);
            Assert.AreEqual(exits + 1, lair.Exits, context);
            Assert.IsNull(lair.Current, context);
            Assert.AreEqual(screen, navigator.Current, $"{context}: back on the portal's screen");
            Assert.Less(Vector2.Distance(back, justOut), 0.6f, $"{context}: beside the portal");
            Assert.AreEqual(LairRooms.ReturnFacing(portal.transform.position, world.ScreenCenter(screen)), navigator.Facing,
                $"{context}: facing away from the portal");
            Assert.AreEqual(ScreenMath.ScreenIndex(world.ScreenCenter(screen)), screenCamera.CurrentScreen, $"{context}: the camera is back");
            yield return PhysicsSteps(10);
            Assert.IsFalse(navigator.InLair, $"{context}: no re-entry while standing there");
            Assert.IsFalse(navigator.InCoreArena, context);
        }

        [UnityTest]
        public IEnumerator Seeds1To50_OnePortalLeadsToTheArena_EveryOtherIntoALairRoomWithHintGivers_AndBackOut()
        {
            yield return Load();
            for (int seed = 1; seed <= 50; seed++)
            {
                yield return Reveal(seed);
                string context = $"seed {seed}";
                Assert.AreEqual(world.Lair.PortalFaces.Count, arena.Portals.Count, $"{context}: a portal per laid-out face");
                int toArena = 0, toLair = 0;
                foreach (CorePortal portal in arena.Portals.ToList())
                {
                    ScreenAddress screen = ScreenOf(portal);
                    navigator.TeleportTo(screen, Facing.East);
                    yield return new WaitForFixedUpdate();
                    Assert.IsTrue(portal.TryEnter(navigator), context);
                    yield return new WaitForFixedUpdate();
                    if (navigator.InCoreArena)
                    {
                        toArena++;
                        Assert.IsTrue(portal.LeadsToArena, context);
                        Assert.AreSame(arena.RealPortal, portal, context);
                        Assert.AreEqual(world.Lair.RealFace, screen.Face, context);
                        Assert.IsTrue(arena.FightActive, $"{context}: the real portal starts the fight");
                        Assert.IsTrue(world.LairProgress.RealPortalKnown, $"{context}: using it makes it known");
                        Assert.IsTrue(arena.LeaveThroughExit(), context);
                        yield return new WaitForFixedUpdate();
                        Assert.IsFalse(arena.FightActive, context);
                        continue;
                    }
                    toLair++;
                    Assert.IsFalse(portal.LeadsToArena, context);
                    Assert.IsTrue(navigator.InLair, $"{context}: {screen.Face}'s portal leads down into the lair");
                    CheckRoom(screen, $"{context} {screen.Face}");
                    Assert.IsTrue(lair.Leave(), context);
                    Assert.AreEqual(screen, navigator.Current, $"{context}: walked back out by the portal");
                    Assert.IsFalse(navigator.InLair);
                }
                Assert.AreEqual(1, toArena, $"{context}: exactly one portal reaches the arena");
                Assert.AreEqual(arena.Portals.Count - 1, toLair, $"{context}: every other reaches a lair room");
            }
        }

        [UnityTest]
        public IEnumerator SteppingIntoALairPortal_GoesDown_AndWalkingOut_ReturnsBesideIt_OnSeveralSeeds()
        {
            yield return Load();
            foreach (int seed in new[] { 7, 21, 42 })
            {
                yield return Reveal(seed);
                foreach (FaceId face in world.Lair.LairFaces)
                {
                    CorePortal portal = PortalOn(face);
                    string context = $"seed {seed} {face} ({world.Model.ThemeOf(face)})";
                    yield return WalkDown(portal, context);
                    Assert.AreEqual(LairRooms.RoomScreen(face), screenCamera.CurrentScreen, $"{context}: the camera frames the room");
                    // Up to the bars: every prisoner can be talked to through them.
                    LairRoomView room = lair.Current;
                    for (int i = 0; i < room.Prisoners.Count; i++)
                    {
                        navigator.EnterInterior(navigator.Current, lair.CurrentPortal, room.Front(i), Facing.North, true);
                        yield return new WaitForFixedUpdate();
                        Assert.IsTrue(room.Prisoners[i].PlayerInRange(), $"{context}: prisoner {i} is in range from their bars");
                    }
                    navigator.EnterInterior(navigator.Current, lair.CurrentPortal, room.EntryPoint, Facing.North, true);
                    yield return new WaitForFixedUpdate();
                    yield return WalkUp(portal, context);
                }
                Assert.AreEqual(world.Lair.LairFaces.Count, lair.Rooms.Count, $"seed {seed}: a room per lair portal");
                Assert.AreEqual(lair.Entries, lair.Exits, seed.ToString());
            }
        }

        [UnityTest]
        public IEnumerator WalkingSouthIntoALairPortal_WithTheKeyHeld_StaysDown_AndWalkingOutWithItHeld_StaysOut()
        {
            // 4.13 review: the room's exit is just south of where the player arrives, so a held S used to walk them straight
            // back out (and, beside a portal north of them... back in). Find a lair portal the player walks into southwards.
            yield return Load();
            CorePortal portal = null;
            for (int seed = 1; seed <= 30 && portal == null; seed++)
            {
                yield return Reveal(seed);
                foreach (FaceId face in world.Lair.LairFaces)
                {
                    CorePortal p = PortalOn(face);
                    if (LairRooms.ReturnFacing(p.transform.position, world.ScreenCenter(ScreenOf(p))) != Facing.North) continue;
                    portal = p;
                    break;
                }
            }
            Assert.IsNotNull(portal, "a lair portal entered walking south");
            ScreenAddress screen = ScreenOf(portal);
            Vector2 at = portal.transform.position;
            navigator.PlaceOn(screen, at + Vector2.up * LairRooms.ReturnDistance, Facing.South);
            yield return new WaitForFixedUpdate();
            yield return null;

            // Hold S: into the portal, and on for a second more down in the room.
            Press(keyboard.sKey, queueEventOnly: true);
            yield return WaitUntilOrTimeout(() => navigator.InLair, 3f);
            Assert.IsTrue(navigator.InLair, "walking south into the portal goes down");
            Assert.IsTrue(mover.WaitingForFreshPress, "the held key no longer moves the player");
            yield return WaitRealSeconds(1f);
            Assert.IsTrue(navigator.InLair, "S still held: the player stays in the room");
            Assert.IsNotNull(lair.Current);
            Assert.Less(Vector2.Distance(lair.Current.EntryPoint, PlayerPosition), 0.1f, "standing where they arrived");
            Release(keyboard.sKey, queueEventOnly: true);
            yield return null;
            yield return null;

            // A fresh S walks out; held on, it never carries the player back into the portal north... south of them.
            Press(keyboard.sKey, queueEventOnly: true);
            yield return WaitUntilOrTimeout(() => !navigator.InLair, 4f);
            Assert.IsFalse(navigator.InLair, "a fresh press walks out");
            Vector2 back = LairRooms.ReturnPoint(at, world.ScreenCenter(screen));
            yield return WaitRealSeconds(1f);
            Release(keyboard.sKey, queueEventOnly: true);
            yield return null;
            Assert.IsFalse(navigator.InLair, "S still held: the player is not carried back into the portal");
            Assert.IsFalse(navigator.InCoreArena);
            Assert.Less(Vector2.Distance(back, PlayerPosition), 0.1f, "standing beside the portal, where they came out");
            Assert.AreEqual(1, lair.Entries);
        }

        [UnityTest]
        public IEnumerator TheMap_ShowsTheLairBadge_TheVisitedLair_AndTheRealPortalOnceAPrisonerNamesIt()
        {
            yield return Load();
            yield return Reveal(7);
            // 4.13 review: no VisitAll: the real portal's screen stays unvisited, and its marker must still show once known.
            var realScreen = ScreenOf(PortalOn(world.Lair.RealFace));
            Assert.IsFalse(tracker.Visits.IsVisited(realScreen), "the real portal's screen is unvisited");
            Assert.IsFalse(MapMarkers.Visible(MapMarkers.FromWorld(world), tracker.Visits).Any(m => m.Kind == MapMarkerKind.Portal && m.Screen == realScreen),
                "unknown and unvisited: the real portal does not show");
            int Detail(FaceId face) => MapMarkers.FromWorld(world).Single(m => m.Kind == MapMarkerKind.Portal && m.Screen.Face == face).Detail;
            foreach (FaceId face in world.Lair.PortalFaces)
                Assert.AreEqual(MapMarkers.PortalUnknown, Detail(face), $"{face}: unknown before any visit");

            FaceId down = world.Lair.LairFaces[0];
            CorePortal portal = PortalOn(down);
            yield return WalkDown(portal, "seed 7");
            Assert.AreEqual(MapMarkers.PortalLair, Detail(down), "the visited lair's portal shows barred");
            Assert.AreEqual(MapMarkers.PortalUnknown, Detail(world.Lair.RealFace), "the real one is still unknown");
            foreach (FaceId other in world.Lair.LairFaces.Skip(1))
                Assert.AreEqual(MapMarkers.PortalUnknown, Detail(other), $"{other}: not visited");

            // The pause map down there: the portal's screen, the lair badge, the marker at the portal.
            int visited = tracker.Visits.Count;
            yield return this.Tap(keyboard.mKey, holdFrames: 2);
            Assert.IsTrue(map.IsOpen, "M opens the map in the lair");
            Assert.IsTrue(GamePause.Active);
            Assert.IsTrue(map.PlayerInside);
            Assert.IsFalse(map.PlayerInCore);
            Assert.AreEqual(ScreenOf(portal), map.PlayerScreen, "the map shows the portal's screen");
            Assert.AreEqual(LairText.MapBadge, map.InsideLabel, "the lair badge");
            Assert.AreEqual((Vector2)portal.transform.position, map.PlayerMarkerPoint, "the marker stands at the portal");
            yield return this.Tap(keyboard.mKey, holdFrames: 2);
            Assert.IsFalse(map.IsOpen);
            Assert.AreEqual(visited, tracker.Visits.Count, "being down there visits no other screen");

            // The prisoner who names the real portal: talking to them makes it known, and the map marks it gold.
            LairRoomView room = lair.Current;
            int teller = room.Plans.ToList().FindIndex(p => p.Hint == PrisonerHint.RealPortal);
            Assert.GreaterOrEqual(teller, 0);
            int fighter = room.Plans.ToList().FindIndex(p => p.Hint == PrisonerHint.Fight);
            navigator.EnterInterior(navigator.Current, lair.CurrentPortal, room.Front(fighter), Facing.North, true);
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(room.Prisoners[fighter].TryTalk(), "the fight's teller talks");
            DialogueBox.Shared.Close();
            yield return null;
            yield return null;
            Assert.IsFalse(world.LairProgress.RealPortalKnown, "a fight hint does not tell the way");
            navigator.EnterInterior(navigator.Current, lair.CurrentPortal, room.Front(teller), Facing.North, true);
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(room.Prisoners[teller].TryTalk(), "the way's teller talks");
            StringAssert.Contains(InWorldText.Region(world.Model.ThemeOf(world.Lair.RealFace)), room.Prisoners[teller].Lines[1]);
            Assert.IsTrue(world.LairProgress.RealPortalKnown, "now the real portal is known");
            DialogueBox.Shared.Close();
            yield return null;
            MapMarker real = MapMarkers.FromWorld(world).Single(m => m.Kind == MapMarkerKind.Portal && m.Screen.Face == world.Lair.RealFace);
            Assert.AreEqual(MapMarkers.PortalReal, real.Detail, "the map marks the real portal");
            Assert.IsTrue(real.Done);
            Assert.IsFalse(tracker.Visits.IsVisited(realScreen), "its screen is still unvisited");
            Assert.IsTrue(MapMarkers.Visible(MapMarkers.FromWorld(world), tracker.Visits).Contains(real), "known: it shows on the map anyway");
            map.Refresh();
            Assert.IsTrue(map.Markers.Contains(real), "the map draws it");
            Assert.IsFalse(map.CellView(realScreen).Visited, "only the marker: its screen stays fogged");
            Assert.AreEqual(1, map.Markers.Count(m => !tracker.Visits.IsVisited(m.Screen)), "and nothing else on an unvisited screen");

            navigator.EnterInterior(navigator.Current, lair.CurrentPortal, room.EntryPoint, Facing.North, true);
            yield return new WaitForFixedUpdate();
            yield return WalkUp(portal, "seed 7");
            Assert.IsFalse(map.PlayerInside);
            Assert.IsNull(map.InsideLabel, "no badge outside");

            // A new run forgets it all.
            world.Rebuild(8);
            yield return null;
            Assert.IsFalse(world.LairProgress.RealPortalKnown);
            Assert.IsEmpty(world.LairProgress.Visited);
            Assert.AreEqual(0, lair.Rooms.Count, "a new run clears the rooms");
        }

        [UnityTest]
        public IEnumerator DebugKeys_InALairRoom_SpawnOnTheWalkway_F8WalksOut_F6AndANewRunTakeThePlayerOut()
        {
            yield return Load();
            yield return Reveal(21);
            CorePortal portal = PortalOn(world.Lair.LairFaces[0]);
            ScreenAddress screen = ScreenOf(portal);
            yield return WalkDown(portal, "seed 21");
            LairRoomView room = lair.Current;

            StringAssert.Contains(LairText.OverlayLabel, debug.OverlayText, "the overlay says where the player is");
            // 4.13 review: F4 does nothing down here (the nearest NPC is a prisoner), and never turns a race.
            yield return this.Tap(keyboard.f4Key, holdFrames: 2);
            foreach (Race race in RaceExtensions.All) Assert.IsFalse(world.Relations.IsHostile(race), $"F4 in the lair turns no race ({race})");
            Assert.IsNull(debug.ToggleNearestHostility(), "F4 picks no prisoner");
            Assert.AreEqual(room.Centre, debug.PlayerScreenCentre());
            yield return this.Tap(keyboard.f3Key, holdFrames: 2);
            Assert.AreEqual(RaceExtensions.All.Length, debug.DebugNpcs.Count, "F3 spawns");
            foreach (NpcTalker npc in debug.DebugNpcs)
                Assert.IsTrue(room.Walkway.Contains(npc.transform.position), $"F3 spawns on the walkway, never in a cell ({npc.transform.position})");
            yield return this.Tap(keyboard.f7Key, holdFrames: 2);
            Assert.AreEqual(1, debug.DebugEnemies.Count, "F7 spawns");
            Assert.IsTrue(room.Walkway.Contains(debug.DebugEnemies[0].transform.position), "F7 spawns on the walkway");
            Object.Destroy(debug.DebugEnemies[0].gameObject);
            yield return null;

            yield return this.Tap(keyboard.f8Key, holdFrames: 2);
            Assert.IsFalse(navigator.InLair, "F8 walks the player out");
            Assert.AreEqual(screen, navigator.Current);
            Assert.Less(Vector2.Distance(LairRooms.ReturnPoint(portal.transform.position, world.ScreenCenter(screen)), PlayerPosition), 0.3f,
                "beside the portal");
            Assert.IsFalse(debug.LeaveCoreArena(), "F8 outside does nothing");

            yield return WalkDown(portal, "seed 21 again");
            yield return this.Tap(keyboard.f6Key, holdFrames: 2);
            yield return PhysicsSteps(2);
            Assert.IsFalse(navigator.InLair, "F6 jumps out of the lair");
            Assert.IsNull(lair.Current);
            Assert.IsTrue(world.TryScreenAt(PlayerPosition, out _), "back on the net");

            yield return WalkDown(portal, "seed 21 a third time");
            int seed = world.Seed;
            yield return this.Tap(keyboard.f10Key, holdFrames: 2);
            yield return PhysicsSteps(2);
            Assert.AreNotEqual(seed, world.Seed, "F10 starts a new run");
            Assert.IsFalse(navigator.InLair, "a new run starts outside");
            Assert.AreEqual(world.Model.StartScreen, navigator.Current, "in Town");
            Assert.AreEqual(0, lair.Rooms.Count, "a new run clears the rooms");
            Assert.AreEqual(0, lair.Entries);
        }
    }
}
