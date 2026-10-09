using System;
using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Enterable buildings (4.6): the marked door of every town (TownSquareView.MarkedDoor) leads into its building's room
    /// (TownInteriorView), a sub-screen that is not on the cube net. Like the core arena, each room has its own screen
    /// below the faces (RoomScreen, so ScreenCamera frames it exactly); CubeNavigator.EnterInterior suspends edge crossing
    /// and keeps the player's screen (and so the map and the visit fog) on the town while they are inside.
    /// Every physics step it watches the player: pushing north into a town's marked door (within DoorAim of it, alive, with
    /// an enabled mover whose last step pushed into the door) goes in, arriving just inside the room's exit facing north; walking south out through the exit puts
    /// the player back outside that door, facing away from it (south). Walking past a door along the lane, or into any
    /// other building, does nothing. A room is built the first time it is entered and lasts for the run; a rebuild (new
    /// run) removes them all, and any teleport (a new run, the debug jumps) leaves a room. The debug F8 walks the player
    /// out (Leave). Interiors hold no gates, rewards or enemies; the shop's counter is decor with a talkative keeper, a hook
    /// for later stories (spending stays on the between-runs screen).
    /// </summary>
    public class TownInteriors : MonoBehaviour, IHintDensityTarget
    {
        /// <summary>How far off the door's centre line (x) the player may be and still go in.</summary>
        public const float DoorAim = 0.75f;

        /// <summary>How close to the sill the player's edge must be (the building's wall stops them there).</summary>
        public const float DoorReach = 0.25f;

        /// <summary>The push's northward part the player needs to go in (straight or slightly diagonal into the door).</summary>
        public const float MinNorthFacing = 0.6f;

        /// <summary>How far in front of the door (beyond touching it) a player walking out is put.</summary>
        public const float ReturnGap = 0.5f;

        /// <summary>The rooms' row of screens below the faces (the core arena is at y = -3).</summary>
        public const int RoomRow = -6;

        [SerializeField] private CubeWorld world;
        [SerializeField] private CubeNavigator navigator;
        [SerializeField] private HintDensity hintDensity = HintGenerator.FirstRunDensity;

        private readonly Dictionary<FaceId, TownInteriorView> rooms = new Dictionary<FaceId, TownInteriorView>();
        private readonly Dictionary<ScreenModule, TownSquareView> views = new Dictionary<ScreenModule, TownSquareView>();
        private Transform root;
        private CubeWorld subscribed;

        /// <summary>Raised when the player goes into a room, and when they come out.</summary>
        public event Action<TownInteriorView> Entered, Left;

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

        public HintDensity HintDensity
        {
            get => hintDensity;
            set => hintDensity = value;
        }

        /// <summary>The room the player is in, or null.</summary>
        public TownInteriorView Current { get; private set; }

        /// <summary>The marked door the player went in by (null outside).</summary>
        public TownDoor CurrentDoor { get; private set; }

        /// <summary>The rooms built this run, by face.</summary>
        public IReadOnlyDictionary<FaceId, TownInteriorView> Rooms => rooms;

        /// <summary>Times the player went in, and came out on foot (or by F8), this run.</summary>
        public int Entries { get; private set; }
        public int Exits { get; private set; }

        /// <summary>A face's room screen: in a row below the faces, every other column, clear of the core arena's screen.</summary>
        public static Vector2Int RoomScreen(FaceId face) => new Vector2Int(2 * (int)face, RoomRow);

        /// <summary>A face's room centre (world).</summary>
        public static Vector2 RoomCentre(FaceId face) => ScreenMath.ScreenCenter(RoomScreen(face), ScreenMath.DefaultScreenSize);

        /// <summary>Where a player walking out of a town's marked door is put, local to the town's centre (ReturnPoint).</summary>
        public static Vector2 ReturnPointLocal(TownBuilding building) =>
            building.Door + (Vector2)building.DoorFacing.ToVector() * (TimeField.PlayerRadius + ReturnGap);

        /// <summary>Where a player walking out of a door is put (world): in front of it, facing away.</summary>
        public static Vector2 ReturnPoint(TownDoor door)
        {
            Vector2 away = door.Facing.ToVector();
            return door.Sill + away * (TimeField.PlayerRadius + ReturnGap);
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
            if (world == null || world.Model == null || player == null) return;
            if (Current != null && !player.InInterior)
            {
                // Taken out some other way (a teleport, a new run, the core).
                Current = null;
                CurrentDoor = null;
            }
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            Vector2 p = body != null ? body.position : (Vector2)player.transform.position;
            if (Current != null)
            {
                if (Current.IsPastExit(p)) Leave();
                return;
            }
            if (player.InCoreArena || player.InInterior) return;
            TownDoor door = DoorAt(player.Current);
            if (door == null || !AtDoor(door, p, player)) return;
            Enter(door);
        }

        /// <summary>The marked door of the town on a screen, or null (no town there, or not built).</summary>
        public TownDoor DoorAt(ScreenAddress screen)
        {
            if (world == null || world.Model == null || !TownSquare.IsTown(screen, world.FaceSize)) return null;
            ScreenModule module = world.ModuleAt(screen);
            if (module == null || !module.IsTownScreen) return null;
            if (!views.TryGetValue(module, out TownSquareView view) || view == null)
            {
                view = module.GetComponentInChildren<TownSquareView>(true);
                views[module] = view;
            }
            return view != null ? view.MarkedDoor : null;
        }

        /// <summary>
        /// True when the player, at p, walks into the door: on its centre line (DoorAim), touching its sill, alive, and
        /// pushing into it right now (an enabled mover whose last physics step moved at least MinNorthFacing into the door;
        /// a dead or frozen player, one waiting for a fresh press, or one merely facing the door is never pulled in).
        /// </summary>
        public static bool AtDoor(TownDoor door, Vector2 p, CubeNavigator player) => door != null && AtDoor(door.Sill, door.Facing, p, player);

        /// <summary>AtDoor for any door by its sill and facing (4.14: the temples' doors too).</summary>
        public static bool AtDoor(Vector2 sill, Facing facing, Vector2 p, CubeNavigator player)
        {
            if (player == null || !player.TryGetComponent(out PlayerMover mover) || !mover.isActiveAndEnabled) return false;
            if (player.TryGetComponent(out Health health) && health.IsDead) return false;
            Vector2 into = -(Vector2)facing.ToVector();
            Vector2 offset = p - sill;
            float along = Vector2.Dot(offset, into);   // negative: in front of the door
            float across = Mathf.Abs(Vector2.Dot(offset, new Vector2(into.y, -into.x)));
            if (across > DoorAim || along > 0f || along < -(TimeField.PlayerRadius + DoorReach)) return false;
            Vector2 push = mover.MoveInput;
            return push.sqrMagnitude > 1e-4f && Vector2.Dot(push.normalized, into) >= MinNorthFacing;
        }

        /// <summary>
        /// Goes in through a town's marked door: builds its room the first time, then moves the player to just inside the
        /// room's exit, facing in (north). False without a player or a world, or for a door of no town.
        /// </summary>
        public bool Enter(TownDoor door)
        {
            CubeNavigator player = ResolveNavigator();
            if (door == null || player == null || world == null || world.Model == null) return false;
            if (!world.TryScreenAt(door.Sill, out ScreenAddress town)) return false;
            TownInteriorView room = RoomOf(town);
            if (room == null) return false;
            Current = room;
            CurrentDoor = door;
            player.EnterInterior(town, door.Sill, room.EntryPoint, door.Facing.Opposite());
            Entries++;
            Entered?.Invoke(room);
            return true;
        }

        /// <summary>
        /// Walks the player out of the room they are in: back outside the door they came in by, facing away from it. False
        /// when the player is in no room.
        /// </summary>
        public bool Leave()
        {
            CubeNavigator player = ResolveNavigator();
            if (Current == null || CurrentDoor == null || player == null || !player.InInterior)
            {
                Current = null;
                CurrentDoor = null;
                return false;
            }
            TownInteriorView room = Current;
            TownDoor door = CurrentDoor;
            Current = null;
            CurrentDoor = null;
            player.PlaceOn(room.Town, ReturnPoint(door), door.Facing);
            if (player.TryGetComponent(out Rigidbody2D body)) body.linearVelocity = Vector2.zero;
            Exits++;
            Left?.Invoke(room);
            return true;
        }

        /// <summary>The room of a town (built the first time it is asked for), or null for a screen that is no town.</summary>
        public TownInteriorView RoomOf(ScreenAddress town)
        {
            if (world == null || world.Model == null || !TownSquare.IsTown(town, world.FaceSize)) return null;
            if (rooms.TryGetValue(town.Face, out TownInteriorView room) && room != null) return room;
            if (root == null)
            {
                root = new GameObject("Town Interiors").transform;
                root.SetParent(transform, false);
                root.position = Vector3.zero;
            }
            CubeNavigator player = ResolveNavigator();
            room = TownInteriorView.Build(world, town.Face, town, RoomCentre(town.Face), root, player != null ? player.transform : null,
                hintDensity);
            rooms[town.Face] = room;
            return room;
        }

        /// <summary>Removes every room (a new run). The player, if inside, is taken out by the rebuild's teleport.</summary>
        public void Clear()
        {
            foreach (TownInteriorView room in rooms.Values)
            {
                if (room == null) continue;
                room.gameObject.SetActive(false);
                Destroy(room.gameObject);
            }
            rooms.Clear();
            views.Clear();
            Current = null;
            CurrentDoor = null;
            Entries = 0;
            Exits = 0;
        }

        private void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
        }
    }
}
