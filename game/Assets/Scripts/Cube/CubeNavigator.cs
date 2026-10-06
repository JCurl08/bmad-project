using System.Collections;
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
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class CubeNavigator : MonoBehaviour
    {
        [SerializeField] private CubeWorld world;
        [Tooltip("How far inside the entry edge the player is placed after crossing (world units).")]
        [SerializeField, Min(0.05f)] private float entryInset = 0.75f;

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
            world != null ? new ScreenAddress(Face, world.CellAt(Face, Position)) : new ScreenAddress(Face, Vector2Int.zero);

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

        /// <summary>Places the player at the centre of a screen.</summary>
        public void TeleportTo(ScreenAddress address, Facing facing)
        {
            RevealIfArriving(address.Face);
            InCoreArena = false;
            Face = address.Face;
            Facing = facing;
            SetPosition(world.ScreenCenter(address));
        }

        /// <summary>
        /// Leaves the cube for a place outside every face (the core arena): snaps the player there and suspends edge
        /// crossing until the next TeleportTo.
        /// </summary>
        public void EnterCoreArena(Vector2 position)
        {
            InCoreArena = true;
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
            if (world == null || world.Model == null || InCoreArena) return;

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
            if (!world.Model.TryStep(exitCell, direction, out ScreenAddress next, out Facing newFacing))
            {
                // Sealed: behave like a wall. (The world also builds a collider here; this is the backstop.)
                // Clamp only the axis being exited so a corner hit does not shove the player sideways.
                Vector2 clamped = local;
                Vector2 velocity = body != null ? body.linearVelocity : Vector2.zero;
                if (direction.IsHorizontal())
                {
                    clamped.x = Mathf.Clamp(local.x, entryInset, extent.x - entryInset);
                    velocity.x = 0f;
                }
                else
                {
                    clamped.y = Mathf.Clamp(local.y, entryInset, extent.y - entryInset);
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
        }

        /// <summary>Just inside the edge the player enters through, at fraction t along that edge.</summary>
        private Vector2 EntryPoint(Facing facing, float t, Vector2 extent)
        {
            const float margin = 0.01f;
            float alongX = Mathf.Clamp(t * extent.x, margin, extent.x - margin);
            float alongY = Mathf.Clamp(t * extent.y, margin, extent.y - margin);
            switch (facing)
            {
                case Facing.East: return new Vector2(entryInset, alongY);
                case Facing.West: return new Vector2(extent.x - entryInset, alongY);
                case Facing.North: return new Vector2(alongX, entryInset);
                default: return new Vector2(alongX, extent.y - entryInset);
            }
        }

        /// <summary>
        /// Snaps the player without interpolation, so no in-between frame is drawn. Interpolation stays off
        /// for two physics steps so its history starts from the new position, then is restored.
        /// </summary>
        private void SetPosition(Vector2 position)
        {
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
