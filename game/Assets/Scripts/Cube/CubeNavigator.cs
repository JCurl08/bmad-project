using System.Collections;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Moves the player between cube faces. When the player leaves a face's outer edge it asks
    /// CubeModel.TryStep for the next screen and facing, then places the player at the matching entry
    /// point on the next face, keeping the position along the edge. A step into a sealed face is a wall.
    /// Arriving on a built science face (the first crossing off Town, or a debug teleport) while the science
    /// faces are unrevealed reveals (lays out) them before the player is moved.
    /// While the player is outside the cube (in the core arena, via EnterCoreArena) edge crossing is off; any teleport onto a
    /// screen (a new run, a debug jump) turns it back on.
    /// Inside a town building (4.6, TownInteriors: EnterInterior) edge crossing is off too, and Current is the town's screen
    /// (the room is not on the cube net); walking out (ReturnTo) or any teleport puts the player back on the net.
    /// A face crossing stops the player (1.25): it arrives facing into the new face, just inside the doorway it came
    /// through, and stands still until every direction is let go and a key is pressed again (PlayerMover.StopUntilFreshPress),
    /// so a held key never carries it on (or, over an edge the flat world draws turned, straight back out). Pressing toward
    /// the doorway behind it walks back over the same edge. Crossings within a face stay smooth.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class CubeNavigator : MonoBehaviour
    {
        [SerializeField] private CubeWorld world;
        /// <summary>
        /// How far inside the entry edge the player's centre is placed after a face crossing (world units). Shared with
        /// IslandLayout.Arrival, so the sweep's island checks use the real arrival point.
        /// </summary>
        public const float EntryInset = 0.75f * ScreenMath.ContentScale;

        private Rigidbody2D body;

        public CubeWorld World
        {
            get => world;
            set => world = value;
        }

        public FaceId Face { get; private set; } = CubeModel.StartFace;

        /// <summary>Entry facing: the facing set by the last edge crossing or teleport (East on start). Not updated while walking within a face.</summary>
        public Facing Facing { get; private set; } = Facing.East;

        public ScreenAddress Current =>
            InInterior ? interiorTown :
            world != null ? new ScreenAddress(Face, world.CellAt(Face, Position)) : new ScreenAddress(Face, Vector2Int.zero);

        /// <summary>True while the player is inside a town building (4.6): edges are not checked and Current is the town's screen.</summary>
        public bool InInterior { get; private set; }

        /// <summary>The marked door the player went in by (world, its sill), while InInterior: where the map shows them.</summary>
        public Vector2 InteriorDoor { get; private set; }

        private ScreenAddress interiorTown;

        /// <summary>True while the player is in the core arena (outside every face): edges are not checked and Current is meaningless.</summary>
        public bool InCoreArena { get; private set; }

        /// <summary>Number of completed face crossings; handy for tests and the overlay.</summary>
        public int Crossings { get; private set; }

        private Vector2 Position => body != null ? body.position : (Vector2)transform.position;

        private RigidbodyInterpolation2D savedInterpolation;
        private int restoreInterpolationIn;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            savedInterpolation = body.interpolation;
        }

        private void OnEnable()
        {
            if (world != null) world.Rebuilt += OnWorldRebuilt;
            StartCoroutine(AfterPhysicsLoop());
        }

        private void OnDisable()
        {
            if (world != null) world.Rebuilt -= OnWorldRebuilt;
            if (restoreInterpolationIn > 0) RestoreInterpolation();
        }

        /// <summary>
        /// Runs after every physics step (WaitForFixedUpdate), so a body that the step carried past a
        /// face edge is moved before anything renders it outside the face.
        /// </summary>
        private IEnumerator AfterPhysicsLoop()
        {
            var wait = new WaitForFixedUpdate();
            while (true)
            {
                yield return wait;
                if (restoreInterpolationIn > 0 && --restoreInterpolationIn == 0) RestoreInterpolation();
                CheckEdges();
            }
        }

        private void Start()
        {
            if (world != null && world.Model != null) TeleportTo(world.Model.StartScreen, Facing.East);
        }

        private void OnWorldRebuilt()
        {
            // A new run: the player starts with nothing.
            if (TryGetComponent(out Inventory inventory)) inventory.Clear();
            TeleportTo(world.Model.StartScreen, Facing.East);
        }

        /// <summary>
        /// Places the player on a screen (its centre; on a Math island screen, a doorway's island: CubeWorld.LandingPoint),
        /// facing the given way: the entry facing, and the PlayerMover's facing too (so a new run or a jump never keeps the
        /// last run's direction).
        /// </summary>
        public void TeleportTo(ScreenAddress address, Facing facing)
        {
            RevealIfArriving(address.Face); // 4.10: first, so the landing point knows the puzzle screens (an ice screen's lane)
            PlaceOn(address, world.LandingPoint(address, facing), facing);
        }

        /// <summary>
        /// Puts the player back on a screen at a given point (walking out of a town building puts them outside its door),
        /// facing the given way; like TeleportTo otherwise (edge crossing back on, the mover's facing set, no wait for a
        /// fresh press).
        /// </summary>
        public void PlaceOn(ScreenAddress address, Vector2 position, Facing facing)
        {
            RevealIfArriving(address.Face);
            InCoreArena = false;
            InInterior = false;
            Face = address.Face;
            Facing = facing;
            if (TryGetComponent(out PlayerMover mover))
            {
                mover.SetFacing(facing.ToVector());
                mover.ClearFreshPressWait();
            }
            SetPosition(position);
        }

        /// <summary>
        /// Goes into a town building (4.6, TownInteriors): snaps the player into its room (off the cube net) facing the given
        /// way, suspends edge crossing and keeps Current on the town's screen (door: the marked door's sill, for the map)
        /// until the player walks out (PlaceOn) or any teleport.
        /// </summary>
        public void EnterInterior(ScreenAddress town, Vector2 door, Vector2 position, Facing facing)
        {
            InCoreArena = false;
            InInterior = true;
            interiorTown = town;
            InteriorDoor = door;
            Face = town.Face;
            Facing = facing;
            if (TryGetComponent(out PlayerMover mover))
            {
                mover.SetFacing(facing.ToVector());
                mover.ClearFreshPressWait();
            }
            if (body != null) body.linearVelocity = Vector2.zero;
            SetPosition(position);
        }

        /// <summary>
        /// Leaves the cube for a place outside every face (the core arena): snaps the player there and suspends edge
        /// crossing until the next TeleportTo.
        /// </summary>
        public void EnterCoreArena(Vector2 position)
        {
            InCoreArena = true;
            InInterior = false;
            if (TryGetComponent(out PlayerMover mover)) mover.ClearFreshPressWait();
            SetPosition(position);
        }

        /// <summary>Reveals the science layout when arriving on a built science face while it is still unrevealed.</summary>
        private void RevealIfArriving(FaceId face)
        {
            if (world != null && world.Model != null && !world.ScienceRevealed && CubeLayout.IsLaidOut(world.Model, face))
                world.RevealScience();
        }

        private void CheckEdges()
        {
            if (world == null || world.Model == null || InCoreArena || InInterior) return;

            Vector2 extent = world.FaceExtent;
            Vector2 origin = world.FaceOrigin(Face);
            Vector2 local = Position - origin;

            Facing direction;
            float t;
            if (local.x >= extent.x) { direction = Facing.East; t = local.y / extent.y; }
            else if (local.x < 0f) { direction = Facing.West; t = local.y / extent.y; }
            else if (local.y >= extent.y) { direction = Facing.North; t = local.x / extent.x; }
            else if (local.y < 0f) { direction = Facing.South; t = local.x / extent.x; }
            else return;
            t = Mathf.Clamp01(t);

            var exitCell = new ScreenAddress(Face, world.CellAt(Face, Position));
            bool locked = world.Locks != null && world.Locks.IsLocked(Face, direction);
            if (locked || !world.Model.TryStep(exitCell, direction, out ScreenAddress next, out Facing newFacing))
            {
                // Sealed, or a locked arch (4.9; its bars are a collider too, this is the backstop): behave like a wall. (The world also builds a collider here; this is the backstop.)
                // Clamp only the axis being exited so a corner hit does not shove the player sideways.
                Vector2 clamped = local;
                Vector2 velocity = body != null ? body.linearVelocity : Vector2.zero;
                if (direction.IsHorizontal())
                {
                    clamped.x = Mathf.Clamp(local.x, EntryInset, extent.x - EntryInset);
                    velocity.x = 0f;
                }
                else
                {
                    clamped.y = Mathf.Clamp(local.y, EntryInset, extent.y - EntryInset);
                    velocity.y = 0f;
                }
                SetPosition(origin + clamped);
                if (body != null) body.linearVelocity = velocity;
                return;
            }

            // Leaving town for the first time in this run: lay out the science faces before arriving.
            RevealIfArriving(next.Face);

            float newT = CubeModel.MapAlongEdge(Face, direction, t, out _, out _);
            Face = next.Face;
            Facing = newFacing;
            Crossings++;
            SetPosition(world.FaceOrigin(Face) + EntryPoint(newFacing, newT, extent));
            // Arrive facing into the new face and stand still until a fresh press (held keys are ignored).
            if (body != null) body.linearVelocity = Vector2.zero;
            if (TryGetComponent(out PlayerMover arrived))
            {
                arrived.SetFacing(newFacing.ToVector());
                arrived.StopUntilFreshPress();
            }
        }

        /// <summary>Just inside the edge the player enters through, at fraction t along that edge.</summary>
        private Vector2 EntryPoint(Facing facing, float t, Vector2 extent)
        {
            const float margin = 0.01f;
            float alongX = Mathf.Clamp(t * extent.x, margin, extent.x - margin);
            float alongY = Mathf.Clamp(t * extent.y, margin, extent.y - margin);
            switch (facing)
            {
                case Facing.East: return new Vector2(EntryInset, alongY);
                case Facing.West: return new Vector2(extent.x - EntryInset, alongY);
                case Facing.North: return new Vector2(alongX, EntryInset);
                default: return new Vector2(alongX, extent.y - EntryInset);
            }
        }

        /// <summary>
        /// Snaps the player without interpolation, so no in-between frame is drawn. Interpolation stays off
        /// for two physics steps so its history starts from the new position, then is restored.
        /// </summary>
        private void SetPosition(Vector2 position)
        {
            // 4.10: any placement (a teleport, a crossing) ends an ice move under way; the slider settles afresh.
            if (TryGetComponent(out IceSlider ice)) ice.Clear();
            if (TryGetComponent(out WindRider wind)) wind.Clear(); // 4.11: and a ride
            IslandScreen.ForgetSteps(); // 4.12 review: and no island walk reads a teleport as a plank crossing
            if (body != null)
            {
                body.interpolation = RigidbodyInterpolation2D.None;
                restoreInterpolationIn = 2;
            }
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            if (body != null) body.position = position;
        }

        private void RestoreInterpolation()
        {
            restoreInterpolationIn = 0;
            if (body != null) body.interpolation = savedInterpolation;
        }
    }
}
