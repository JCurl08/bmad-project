using System;
using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The boss lair (4.13): every core portal but the run's real one (CubeWorld.Lair) leads down into its face's lair room
    /// (LairRoomView), a sub-screen off the cube net like the core arena and the town interiors: each face's room has its own
    /// screen below the faces (RoomScreen, a row under the town rooms', so ScreenCamera frames it exactly).
    /// CorePortal.TryEnter calls Enter: CubeNavigator.EnterInterior (as a lair) suspends edge crossing and keeps the player's
    /// screen (and so the map and the visit fog) on the portal's screen while they are down there; the player arrives just
    /// inside the room's exit facing north, towards the cells. Every physics step it watches the player: walking south out
    /// through the exit (the town rooms' ExitLine) puts the player back on the portal's screen, ReturnDistance from the
    /// portal towards the screen's centre and facing that way, so they never land on the portal again. Any teleport (a new
    /// run, the debug jumps) leaves a room; the debug F8 walks the player out (Leave). A room is built the first time it is
    /// entered and lasts for the run; a rebuild removes them all. Entering marks the room visited (CubeWorld.LairProgress),
    /// and talking to the prisoner who names the real portal's region makes it known; both show on the map.
    /// </summary>
    public class LairRooms : MonoBehaviour
    {
        /// <summary>The lair rooms' row of screens below the faces (the core arena is at y = -3, the town rooms at -6).</summary>
        public const int RoomRow = -9;

        /// <summary>How far from the portal, towards its screen's centre, a player walking out is put (clear of its trigger).</summary>
        public const float ReturnDistance = 1.5f * ScreenMath.ContentScale;

        /// <summary>How close the player's centre may come to a portal's before they touch its trigger.</summary>
        public const float PortalContact = CorePortal.Radius + TimeField.PlayerRadius;

        /// <summary>The least gap (4.13 review) between the player at the return point and the portal's trigger, whichever way out.</summary>
        public const float ReturnClearance = 0.5f;

        [SerializeField] private CubeWorld world;
        [SerializeField] private CubeNavigator navigator;

        private readonly Dictionary<FaceId, LairRoomView> rooms = new Dictionary<FaceId, LairRoomView>();
        private Transform root;
        private CubeWorld subscribed;

        /// <summary>Raised when the player goes down into a room, and when they come out.</summary>
        public event Action<LairRoomView> Entered, Left;

        public CubeWorld World
        {
            get => world;
            set
            {
                world = value;
                Subscribe();
            }
        }

        public CubeNavigator Navigator
        {
            get => navigator;
            set => navigator = value;
        }

        /// <summary>The room the player is in, or null.</summary>
        public LairRoomView Current { get; private set; }

        /// <summary>The portal the player came down by (world), while in a room.</summary>
        public Vector2 CurrentPortal { get; private set; }

        /// <summary>The rooms built this run, by face.</summary>
        public IReadOnlyDictionary<FaceId, LairRoomView> Rooms => rooms;

        /// <summary>Times the player went down, and came out on foot (or by F8), this run.</summary>
        public int Entries { get; private set; }
        public int Exits { get; private set; }

        /// <summary>A face's lair room screen: a row below the town rooms, every other column.</summary>
        public static Vector2Int RoomScreen(FaceId face) => new Vector2Int(2 * (int)face, RoomRow);

        /// <summary>A face's lair room centre (world).</summary>
        public static Vector2 RoomCentre(FaceId face) => ScreenMath.ScreenCenter(RoomScreen(face), ScreenMath.DefaultScreenSize);

        /// <summary>Where a player walking out of a lair room is put (world): from the portal towards its screen's centre.</summary>
        public static Vector2 ReturnPoint(Vector2 portal, Vector2 screenCentre)
        {
            Vector2 away = screenCentre - portal;
            if (away.sqrMagnitude < 1e-6f) away = Vector2.down;
            return portal + away.normalized * ReturnDistance;
        }

        /// <summary>The way a player walking out faces: the cardinal nearest the way from the portal to its screen's centre.</summary>
        public static Facing ReturnFacing(Vector2 portal, Vector2 screenCentre)
        {
            Vector2 away = screenCentre - portal;
            if (Mathf.Abs(away.x) >= Mathf.Abs(away.y)) return away.x >= 0f ? Facing.East : Facing.West;
            return away.y >= 0f ? Facing.North : Facing.South;
        }

        private void Awake()
        {
            if (world == null) world = GetComponent<CubeWorld>();
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            if (subscribed == null) return;
            subscribed.Rebuilt -= Clear;
            subscribed = null;
        }

        private void Subscribe()
        {
            if (!isActiveAndEnabled || world == null || subscribed == world) return;
            OnDisable();
            subscribed = world;
            world.Rebuilt += Clear;
        }

        private CubeNavigator ResolveNavigator()
        {
            if (navigator == null) navigator = FindAnyObjectByType<CubeNavigator>();
            return navigator;
        }

        private void FixedUpdate()
        {
            CubeNavigator player = ResolveNavigator();
            if (Current == null || player == null) return;
            if (!player.InLair)
            {
                // Taken out some other way (a teleport, a new run).
                Current = null;
                return;
            }
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            Vector2 p = body != null ? body.position : (Vector2)player.transform.position;
            if (Current.IsPastExit(p)) Leave();
        }

        /// <summary>True when a portal on a face leads down into a lair room this run (not the real one).</summary>
        public bool LeadsToLair(FaceId face) => world != null && world.Lair != null && world.Lair.IsLair(face);

        /// <summary>
        /// Goes down through a portal (on a screen) into its face's lair room: builds the room the first time, then moves the
        /// player just inside its exit, facing north. False without a player or a world, or for a portal that is the real one.
        /// </summary>
        public bool Enter(Vector2 portal, ScreenAddress screen, CubeNavigator player = null)
        {
            player = player != null ? player : ResolveNavigator();
            if (player == null || world == null || world.Model == null || !LeadsToLair(screen.Face)) return false;
            navigator = player;
            LairRoomView room = RoomOf(screen);
            if (room == null) return false;
            Current = room;
            CurrentPortal = portal;
            player.EnterInterior(screen, portal, room.EntryPoint, Facing.North, true);
            // 4.13 review: a key still held from walking into the portal (S, say) must not walk the player straight back out of
            // the exit just behind them: they stand until every key is let go and one is pressed again.
            if (player.TryGetComponent(out PlayerMover mover)) mover.StopUntilFreshPress();
            Entries++;
            world.LairProgress.Visit(screen.Face);
            Entered?.Invoke(room);
            return true;
        }

        /// <summary>Walks the player out of the room they are in: back by the portal they came down, facing away from it. False outside.</summary>
        public bool Leave()
        {
            CubeNavigator player = ResolveNavigator();
            if (Current == null || player == null || !player.InLair)
            {
                Current = null;
                return false;
            }
            LairRoomView room = Current;
            Current = null;
            Vector2 centre = world.ScreenCenter(room.PortalScreen);
            player.PlaceOn(room.PortalScreen, ReturnPoint(CurrentPortal, centre), ReturnFacing(CurrentPortal, centre));
            if (player.TryGetComponent(out Rigidbody2D body)) body.linearVelocity = Vector2.zero;
            // And back on the face, a key still held from walking out (S) never carries them into the portal again.
            if (player.TryGetComponent(out PlayerMover mover)) mover.StopUntilFreshPress();
            Exits++;
            Left?.Invoke(room);
            return true;
        }

        /// <summary>The lair room under a portal's screen (built the first time it is asked for), or null for a face with no lair.</summary>
        public LairRoomView RoomOf(ScreenAddress screen)
        {
            if (!LeadsToLair(screen.Face)) return null;
            if (rooms.TryGetValue(screen.Face, out LairRoomView room) && room != null) return room;
            if (root == null)
            {
                root = new GameObject("Lair Rooms").transform;
                root.SetParent(transform, false);
                root.position = Vector3.zero;
            }
            CubeNavigator player = ResolveNavigator();
            room = LairRoomView.Build(world, world.Lair, screen.Face, screen, RoomCentre(screen.Face), root,
                player != null ? player.transform : null);
            CubeWorld owner = world;
            room.Told += prisoner =>
            {
                if (prisoner.Hint == PrisonerHint.RealPortal && owner != null) owner.LairProgress.LearnRealPortal();
            };
            rooms[screen.Face] = room;
            return room;
        }

        /// <summary>Removes every room (a new run). The player, if inside, is taken out by the rebuild's teleport.</summary>
        public void Clear()
        {
            foreach (LairRoomView room in rooms.Values)
            {
                if (room == null) continue;
                room.gameObject.SetActive(false);
                Destroy(room.gameObject);
            }
            rooms.Clear();
            Current = null;
            Entries = 0;
            Exits = 0;
        }

        private void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
        }
    }
}
