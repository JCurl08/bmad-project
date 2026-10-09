using System;
using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The temples (4.14): every town's temple door (TownSquareView.TempleDoor) leads, once its face's fragments are handed
    /// in (CubeWorld.UnsealTemple), into the face's temple room (TempleView), a whole screen off the cube net on its own row
    /// (TempleRoom.RoomScreen) like the town rooms (4.6) and the lair (4.13). CubeNavigator.EnterInterior (as a temple) keeps
    /// the player's screen on the town's while inside, and lets the ice and wind drivers run there.
    /// Every physics step it watches the player: pushing into an unsealed temple's door (as TownInteriors.AtDoor: on its line,
    /// touching its sill, alive, pushing in) goes in, arriving just inside the south doorway facing north; pushing into a
    /// sealed one says it is sealed. Walking south out past the screen's edge (or F8) puts the player back in front of the
    /// door, facing away, and both ways wait for a fresh press (the 4.13 held-key fix), so a held key never walks them back.
    /// Leaving a temple that is not solved (on foot, F8, any teleport) throws its room away: the puzzle starts afresh next
    /// time. A solved temple stays solved. Inside Physics' temple the player's MassMitt has the temple's grip. A rebuild
    /// (a new run) clears every room.
    /// </summary>
    public class TempleRooms : SubScreenRooms<TempleView>
    {
        /// <summary>How far in front of the door (beyond touching it) a player walking out is put (as TownInteriors).</summary>
        public const float ReturnGap = TownInteriors.ReturnGap;

        /// <summary>Real seconds between two "sealed" popups at the same door.</summary>
        public const float SealedPopupSeconds = 3f;

        private float sealedPopupAt = float.NegativeInfinity;

        /// <summary>The door the player went in by (null outside).</summary>
        public TempleDoor CurrentDoor { get; private set; }

        /// <summary>Times a sealed door turned the player away (with its popup).</summary>
        public int SealedBumps { get; private set; }

        protected override string RootName => "Temple Rooms";

        /// <summary>Where a player walking out of a temple's door is put (world): in front of it, facing away.</summary>
        public static Vector2 ReturnPoint(TempleDoor door) => TownInteriors.ReturnPoint(door.Sill, door.Facing);

        /// <summary>Where a player walking out of a temple's door is put, local to its town's centre.</summary>
        public static Vector2 ReturnPointLocal(TownBuilding temple) => TownInteriors.ReturnPoint(temple.Door, temple.DoorFacing);

        private void FixedUpdate()
        {
            CubeNavigator player = ResolveNavigator();
            if (World == null || World.Model == null || player == null) return;
            if (Current != null && !player.InTemple)
            {
                // Taken out some other way (a teleport, a new run): the temple resets unless solved.
                TempleView was = Current;
                Current = null;
                CurrentDoor = null;
                Gone(was);
            }
            Vector2 p = SubScreens.PlayerPosition(player);
            if (Current != null)
            {
                if (Current.IsPastExit(p)) Leave();
                return;
            }
            if (player.InCoreArena || player.InInterior) return;
            TempleDoor door = DoorAt(player.Current);
            if (door == null || !TownInteriors.AtDoor(door.Sill, door.Facing, p, player)) return;
            if (!door.Unsealed)
            {
                if (Time.realtimeSinceStartup - sealedPopupAt < SealedPopupSeconds) return;
                sealedPopupAt = Time.realtimeSinceStartup;
                SealedBumps++;
                PickupPopup.Show(door.Sill + new Vector2(0f, 1.5f), TempleText.SealedPopup, new Color(0.9f, 0.85f, 1f), 2.5f);
                return;
            }
            Enter(door);
        }

        /// <summary>The temple door of the town on a screen, or null (no town there, or not built).</summary>
        public TempleDoor DoorAt(ScreenAddress screen)
        {
            CubeWorld world = World;
            if (world == null || world.Model == null || !TownSquare.IsTown(screen, world.FaceSize)) return null;
            TownSquareView view = world.TownViewOf(screen.Face);
            return view != null ? view.TempleDoor : null;
        }

        /// <summary>
        /// Goes into a temple through its door (unsealed): builds its room if it is not standing, then moves the player just
        /// inside its south doorway, facing north, and says the rule. False for a sealed door, or without a player or world.
        /// </summary>
        public bool Enter(TempleDoor door)
        {
            CubeNavigator player = ResolveNavigator();
            CubeWorld world = World;
            if (door == null || !door.Unsealed || player == null || world == null || world.Model == null) return false;
            if (!world.TryScreenAt(door.Sill, out ScreenAddress town)) return false;
            TempleView room = RoomOf(town);
            if (room == null) return false;
            Current = room;
            CurrentDoor = door;
            player.EnterInterior(town, door.Sill, room.EntryPoint, Facing.North, false, true);
            if (player.TryGetComponent(out PlayerMover mover)) mover.StopUntilFreshPress();
            if (room.Puzzle.Kind == TemplePuzzleKind.Booths && player.TryGetComponent(out MassMitt mitt)) mitt.TempleGrip = true;
            Entries++;
            room.ShowRule();
            RaiseEntered(room);
            return true;
        }

        /// <summary>Walks the player out of the temple: back in front of its door, facing away. False outside.</summary>
        public bool Leave()
        {
            CubeNavigator player = ResolveNavigator();
            if (Current == null || CurrentDoor == null || player == null || !player.InTemple)
            {
                Current = null;
                CurrentDoor = null;
                return false;
            }
            TempleView room = Current;
            TempleDoor door = CurrentDoor;
            Current = null;
            CurrentDoor = null;
            SubScreens.PlaceOut(player, room.Town, ReturnPoint(door), door.Facing, true);
            Exits++;
            Gone(room);
            return true;
        }

        /// <summary>The player is out of a temple: the grip goes back, and an unsolved puzzle is thrown away (it resets).</summary>
        private void Gone(TempleView room)
        {
            CubeNavigator player = ResolveNavigator();
            if (player != null && player.TryGetComponent(out MassMitt mitt)) mitt.TempleGrip = false;
            if (room != null && !room.IsSolved) Discard(room.Face, room);
            RaiseLeft(room);
        }

        /// <summary>The temple room of a town (built if it is not standing), or null for a screen that is no town.</summary>
        public TempleView RoomOf(ScreenAddress town)
        {
            CubeWorld world = World;
            if (world == null || world.Model == null || !TownSquare.IsTown(town, world.FaceSize)) return null;
            TempleView room = Standing(town.Face);
            if (room != null) return room;
            room = TempleView.Build(world, town.Face, town, TempleRoom.RoomCentre(town.Face), RoomRoot());
            Keep(town.Face, room);
            return room;
        }

        protected override void Cleared()
        {
            CurrentDoor = null;
            SealedBumps = 0;
            CubeNavigator player = ResolveNavigator();
            if (player != null && player.TryGetComponent(out MassMitt mitt)) mitt.TempleGrip = false;
        }
    }
}
