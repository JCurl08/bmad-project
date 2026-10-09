using System;
using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The rows of sub-screens below the cube net (4.15): the core arena at y = -3, then one room per face, every other
    /// column, on its own row: the town rooms (TownInteriors, -6), the lair rooms (LairRooms, -9) and the temples
    /// (TempleRoom, -12). Each room has a whole screen, so ScreenCamera frames it exactly.
    /// </summary>
    public static class SubScreens
    {
        /// <summary>A face's room screen on a row: every other column, so no two rooms share a screen.</summary>
        public static Vector2Int RoomScreen(int row, FaceId face) => new Vector2Int(2 * (int)face, row);

        /// <summary>A face's room centre (world) on a row.</summary>
        public static Vector2 RoomCentre(int row, FaceId face) =>
            ScreenMath.ScreenCenter(RoomScreen(row, face), ScreenMath.DefaultScreenSize);

        /// <summary>The player's position this physics step (its body's, else its transform's).</summary>
        public static Vector2 PlayerPosition(CubeNavigator player)
        {
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            return body != null ? body.position : (Vector2)player.transform.position;
        }

        /// <summary>
        /// Puts a player who walked out of a room back on the net: on screen at point, facing that way, at rest; with freshPress
        /// (the 4.13 held-key fix) they then stand until every key is let go and one is pressed again.
        /// </summary>
        public static void PlaceOut(CubeNavigator player, ScreenAddress screen, Vector2 point, Facing facing, bool freshPress)
        {
            player.PlaceOn(screen, point, facing);
            if (player.TryGetComponent(out Rigidbody2D body)) body.linearVelocity = Vector2.zero;
            if (freshPress && player.TryGetComponent(out PlayerMover mover)) mover.StopUntilFreshPress();
        }
    }

    /// <summary>
    /// The plumbing every set of sub-screen rooms shares (4.15: TownInteriors, LairRooms, TempleRooms): the world and the
    /// player (found on demand), one room per face built the first time it is asked for under one root object, the room the
    /// player is in, the entry and exit counts and events, and Clear on every rebuild of the world (a new run). What the
    /// rooms do (how the player goes in and comes out) is each subclass's own.
    /// </summary>
    public abstract class SubScreenRooms<TRoom> : MonoBehaviour where TRoom : Component
    {
        [SerializeField] private CubeWorld world;
        [SerializeField] private CubeNavigator navigator;

        private readonly Dictionary<FaceId, TRoom> rooms = new Dictionary<FaceId, TRoom>();
        private Transform root;
        private CubeWorld subscribed;

        /// <summary>Raised when the player goes into a room, and when they come out.</summary>
        public event Action<TRoom> Entered, Left;

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
        public TRoom Current { get; protected set; }

        /// <summary>The rooms standing this run, by face.</summary>
        public IReadOnlyDictionary<FaceId, TRoom> Rooms => rooms;

        /// <summary>Times the player went in, and came out on foot (or by F8), this run.</summary>
        public int Entries { get; protected set; }
        public int Exits { get; protected set; }

        /// <summary>The name of the object the rooms are built under.</summary>
        protected abstract string RootName { get; }

        protected virtual void Awake()
        {
            if (world == null) world = GetComponent<CubeWorld>();
        }

        protected virtual void OnEnable() => Subscribe();

        protected virtual void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (!isActiveAndEnabled || world == null || subscribed == world) return;
            Unsubscribe();
            subscribed = world;
            world.Rebuilt += Clear;
        }

        private void Unsubscribe()
        {
            if (subscribed == null) return;
            subscribed.Rebuilt -= Clear;
            subscribed = null;
        }

        protected CubeNavigator ResolveNavigator()
        {
            if (navigator == null) navigator = FindAnyObjectByType<CubeNavigator>();
            return navigator;
        }

        protected void RaiseEntered(TRoom room) => Entered?.Invoke(room);

        protected void RaiseLeft(TRoom room) => Left?.Invoke(room);

        /// <summary>A face's standing room, or null.</summary>
        protected TRoom Standing(FaceId face) => rooms.TryGetValue(face, out TRoom room) && room != null ? room : null;

        /// <summary>Keeps a face's newly built room.</summary>
        protected void Keep(FaceId face, TRoom room) => rooms[face] = room;

        /// <summary>Throws one room away (it is built afresh next time).</summary>
        protected void Discard(FaceId face, TRoom room)
        {
            rooms.Remove(face);
            if (room == null) return;
            room.gameObject.SetActive(false);
            Destroy(room.gameObject);
        }

        /// <summary>The object the rooms are built under (made the first time).</summary>
        protected Transform RoomRoot()
        {
            if (root == null)
            {
                root = new GameObject(RootName).transform;
                root.SetParent(transform, false);
                root.position = Vector3.zero;
            }
            return root;
        }

        /// <summary>Removes every room (a new run). The player, if inside, is taken out by the rebuild's teleport.</summary>
        public void Clear()
        {
            foreach (TRoom room in rooms.Values)
            {
                if (room == null) continue;
                room.gameObject.SetActive(false);
                Destroy(room.gameObject);
            }
            rooms.Clear();
            Current = null;
            Entries = 0;
            Exits = 0;
            Cleared();
        }

        /// <summary>A subclass's own state to reset in Clear.</summary>
        protected virtual void Cleared()
        {
        }

        protected virtual void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
        }
    }
}
