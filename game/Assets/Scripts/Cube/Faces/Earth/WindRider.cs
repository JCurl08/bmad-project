using Game.Tracer;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The player on a wind screen (4.11): PlayerMover's IMoveDriver while the player stands on a WindScreen's grid. The player
    /// rests on a tile's centre (coming onto the grid, it settles on the tile it stands on, else the nearest walkable one; never
    /// the goal pad). A press of a direction (the stronger axis of the stick) sets off one move by the layout's own rules
    /// (WindLayout.RideFrom), so play and solver never disagree:
    /// onto a walkable tile (the ring, an island, the goal pad, a grip anchor's neighbour of its own region) it steps one tile
    /// at walking speed, and holding the key keeps stepping;
    /// onto a stream tile it rides: the wind carries it along the arrows at RideFactor times the walking speed, with no
    /// steering, onto the first tile that is not a stream (land or a grip anchor), where it stops and waits for a fresh press
    /// (PlayerMover.StopUntilFreshPress), so a held key never walks it on into the next stream; a stream that would only bring
    /// it back where it set off (one that ends here) does not take it;
    /// onto the sky or a wall it does nothing;
    /// from a ring tile in a doorway's width, toward that doorway, it walks off the grid as usual.
    /// Swings are off while riding (PlayerAttack asks Busy). Standing on the goal pad opens the chest's guard.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class WindRider : MonoBehaviour, IMoveDriver
    {
        /// <summary>A ride runs at this many times the walking speed (research note 3b: about 2x).</summary>
        public const float RideFactor = 2f;

        /// <summary>Physics steps a move may stand still before the backstop puts the player on its tile.</summary>
        public const int StallSteps = 8;

        private enum Mode { Idle, Moving, Leaving }

        private Rigidbody2D body;
        private CubeNavigator navigator;
        private Mode mode;
        private WindScreen screen;
        private int cell = -1, target = -1;
        private readonly List<int> waypoints = new List<int>();
        private int next;
        private bool riding, settling;
        private float moveSpeed;
        private Facing leaving;
        private Vector2 lastPos, lastInput;
        private int stalled;

        public WindScreen Screen => screen;

        /// <summary>The tile the player rests on, or is moving to (-1 off the grid).</summary>
        public int Cell => mode == Mode.Moving ? target : cell;

        /// <summary>True while a ride is under way (no swinging then).</summary>
        public bool Busy => screen != null && mode == Mode.Moving && riding;

        public bool IsRiding => Busy;

        /// <summary>True while resting on a tile's centre on the grid.</summary>
        public bool AtRest => screen != null && mode == Mode.Idle && cell >= 0;

        /// <summary>Rides finished so far (tests read it).</summary>
        public int Rides { get; private set; }

        /// <summary>Times the stall backstop had to put the player on its tile (should stay 0; tests read it).</summary>
        public int Backstops { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            navigator = GetComponent<CubeNavigator>();
        }

        private void Reset(WindScreen now)
        {
            screen = now;
            mode = Mode.Idle;
            cell = target = -1;
            waypoints.Clear();
            riding = settling = false;
        }

        /// <summary>Forgets any move under way (a teleport, a face crossing, a rebuild: CubeNavigator calls it on every placement).</summary>
        public void Clear() => Reset(null);

        public bool Drive(Vector2 input, float speed, float deltaTime, out Vector2 velocity)
        {
            velocity = Vector2.zero;
            if (body == null) return false;
            if (navigator != null && (navigator.InCoreArena || navigator.InInterior))
            {
                if (!ReferenceEquals(screen, null)) Reset(null);
                return false;
            }
            Vector2 pos = body.position;
            lastInput = input;
            WindScreen here = mode == Mode.Moving && screen != null ? screen : WindScreen.At(pos);
            // ReferenceEquals: a destroyed screen (a rebuild) compares equal to null in Unity, yet must still reset the state.
            if (!ReferenceEquals(here, screen) || (screen == null && !ReferenceEquals(screen, null))) Reset(here);
            if (screen == null) return false;
            WindLayout layout = screen.Layout;

            if (mode == Mode.Leaving)
            {
                Vector2 way = WindLayout.Step(leaving);
                if (Vector2.Dot(input, way) > 0.5f)
                {
                    velocity = way * speed;
                    return true;
                }
                mode = Mode.Idle;
                cell = -1;
            }

            if (mode == Mode.Moving)
            {
                Vector2 to = screen.CellWorld(waypoints[next]), left = to - pos;
                float step = moveSpeed * deltaTime;
                stalled = (pos - lastPos).sqrMagnitude < 1e-8f ? stalled + 1 : 0;
                lastPos = pos;
                if (stalled >= StallSteps)
                {
                    stalled = 0;
                    Backstops++;
                    Teleport(screen.CellWorld(target));
                    Arrive(layout);
                    return true;
                }
                // Past whole waypoints this step (a ride's corners), keeping the speed.
                while (left.magnitude <= step + 1e-4f && next < waypoints.Count - 1)
                {
                    next++;
                    to = screen.CellWorld(waypoints[next]);
                    left = to - pos;
                }
                if (left.magnitude > step + 1e-4f)
                {
                    velocity = left.normalized * moveSpeed;
                    return true;
                }
                velocity = left / deltaTime; // lands exactly on the tile this step
                Arrive(layout);
                return true;
            }

            // Idle: settle on a tile first.
            if (cell < 0 || (screen.CellWorld(cell) - pos).sqrMagnitude > 0.02f * 0.02f)
            {
                int near = cell >= 0 ? cell : NearestStandable(layout, pos - screen.Centre);
                if (near < 0) return false;
                bool arriving = cell < 0;
                cell = -1;
                StartMove(new List<int> { near }, speed, false);
                settling = arriving;
                return Drive(input, speed, deltaTime, out velocity);
            }
            if (input.magnitude < 0.5f) return true;
            Facing facing = Mathf.Abs(input.x) >= Mathf.Abs(input.y)
                ? (input.x > 0f ? Facing.East : Facing.West)
                : (input.y > 0f ? Facing.North : Facing.South);
            Vector2Int nextPos = WindLayout.Pos(cell) + WindLayout.Step(facing);
            if (!WindLayout.InGrid(nextPos))
            {
                // Off the grid: only a doorway's width of the ring leads out, toward its doorway.
                if (WindLayout.DoorwayOf(cell) == facing && layout.HasDoor(facing))
                {
                    mode = Mode.Leaving;
                    leaving = facing;
                    velocity = (Vector2)WindLayout.Step(facing) * speed;
                }
                return true;
            }
            int n = WindLayout.Index(nextPos);
            if (layout.IsWalkable(n))
            {
                StartMove(new List<int> { n }, speed, false);
                return true;
            }
            if (layout.RideFrom(cell, n, out _) < 0) return true; // the sky, a wall, or a stream that would bring it back
            StartMove(layout.RidePath(n), speed, true);
            return Drive(Vector2.zero, speed, deltaTime, out velocity);
        }

        private void StartMove(List<int> path, float speed, bool ride)
        {
            waypoints.Clear();
            waypoints.AddRange(path);
            next = 0;
            target = path[path.Count - 1];
            riding = ride;
            moveSpeed = Mathf.Max(0.1f, speed) * (ride ? RideFactor : 1f);
            mode = Mode.Moving;
            stalled = 0;
            lastPos = new Vector2(float.MaxValue, float.MaxValue);
        }

        private void Arrive(WindLayout layout)
        {
            cell = target;
            target = -1;
            mode = Mode.Idle;
            bool landed = riding;
            bool arrivedFacingAStream = settling && HeldKeyWouldRide(layout);
            if (riding) Rides++;
            riding = settling = false;
            // Landed, or just come onto the grid (a doorway, a teleport) with the held key pointing at a stream: control comes
            // back with a fresh press, so a held key never carries the player into a stream by surprise. Coming in by a doorway
            // the key points at the sky (no stream ever lies in front of a doorway), so walking in and out stays one press.
            if ((landed || arrivedFacingAStream) && TryGetComponent(out PlayerMover mover)) mover.StopUntilFreshPress();
            if (cell == layout.Goal) screen.ReachGoal();
        }

        /// <summary>True when the key held now, from the tile the player rests on, would set off a ride.</summary>
        private bool HeldKeyWouldRide(WindLayout layout)
        {
            if (cell < 0 || lastInput.magnitude < 0.5f) return false;
            Facing facing = Mathf.Abs(lastInput.x) >= Mathf.Abs(lastInput.y)
                ? (lastInput.x > 0f ? Facing.East : Facing.West)
                : (lastInput.y > 0f ? Facing.North : Facing.South);
            Vector2Int next = WindLayout.Pos(cell) + WindLayout.Step(facing);
            return WindLayout.InGrid(next) && layout.RideFrom(cell, WindLayout.Index(next), out _) >= 0;
        }

        /// <summary>
        /// The tile a player coming onto the grid settles on: the walkable tile it stands on (within half a tile), else the
        /// nearest walkable one (never the goal pad: only a step onto it counts).
        /// </summary>
        public static int NearestStandable(WindLayout layout, Vector2 local)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int c = 0; c < WindLayout.Cells; c++)
            {
                WindTile t = layout.TileAt(c);
                if (!WindLayout.IsWalkableTile(t) || t == WindTile.Goal) continue;
                float d = (WindLayout.CellLocal(c) - local).sqrMagnitude;
                if (d < bestDistance - 1e-6f)
                {
                    bestDistance = d;
                    best = c;
                }
            }
            return best;
        }

        private void Teleport(Vector2 world)
        {
            transform.position = new Vector3(world.x, world.y, transform.position.z);
            body.position = world;
            body.linearVelocity = Vector2.zero;
        }
    }
}
