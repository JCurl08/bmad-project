using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The player on an ice screen (4.10): PlayerMover's IMoveDriver while the player stands on an IceScreen's grid. The
    /// player rests on a tile's centre (coming onto the grid, it settles on the nearest tile it can stand on: from a doorway,
    /// its landing). A press of a direction (the stronger axis of the stick) sets off one move by the layout's own rules
    /// (IceLayout.Slide), so play and solver never disagree:
    /// onto ice, it slides at SlideFactor times the walking speed, without steering, until a rock or bank stops it (on the tile
    /// before), it reaches grip, a landing, the camp or the goal pad (it stops on it), or it falls into a pit: it sinks
    /// (FallSeconds, no damage) and comes back on the landing it last came in by (or the nearest), standing still until a
    /// fresh press (PlayerMover.StopUntilFreshPress), so a held key never walks it back out of the doorway;
    /// onto a neighbouring grip tile (within a lane or the camp, or from grip onto grip) it steps one tile at walking speed,
    /// and holding the key keeps stepping;
    /// from a landing's last tile toward its doorway it walks off the grid as usual.
    /// Swings are off while sliding or falling (PlayerAttack asks Busy). Coming to rest on the goal pad opens the chest's guard.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class IceSlider : MonoBehaviour, IMoveDriver
    {
        /// <summary>A slide runs at this many times the walking speed (research note 3a: about 1.5x).</summary>
        public const float SlideFactor = 1.5f;

        /// <summary>How long a fall into a pit takes before the player is back on its landing (no damage).</summary>
        public const float FallSeconds = 0.6f;

        /// <summary>Said when the player climbs out on its landing after a fall.</summary>
        public const string FallText = "Whoops! Back to the start.";

        public static readonly Color FallTextColor = new Color(0.8f, 0.92f, 1f);

        private enum Mode { Idle, Moving, Falling, Leaving }

        private Rigidbody2D body;
        private CubeNavigator navigator;
        private Transform visual;
        private Vector3 visualScale = Vector3.one;
        private Mode mode;
        private IceScreen screen;
        private int cell = -1, target = -1, entry = -1;
        private bool sliding, falling;
        private float moveSpeed, fallLeft;
        private Facing leaving;
        private Vector2 lastPos;
        private int stalled;
        private RigidbodyInterpolation2D savedInterpolation;
        private int restoreInterpolationIn;

        /// <summary>Physics steps a move may stand still before the backstop puts the player on its tile.</summary>
        public const int StallSteps = 8;

        /// <summary>The ice screen the player is on (null off every ice grid).</summary>
        public IceScreen Screen => screen;

        /// <summary>The tile the player rests on, or is moving to (-1 off the grid).</summary>
        public int Cell => mode == Mode.Moving ? target : cell;

        /// <summary>The landing's tile a fall puts the player back on (the last landing it stood on).</summary>
        public int EntryCell => entry;

        /// <summary>True while a slide or a fall is under way (no swinging then).</summary>
        public bool Busy => screen != null && (mode == Mode.Falling || (mode == Mode.Moving && sliding));

        public bool IsSliding => mode == Mode.Moving && sliding;
        public bool IsFalling => screen != null && mode == Mode.Falling;

        /// <summary>True while resting on a tile's centre on the grid.</summary>
        public bool AtRest => screen != null && mode == Mode.Idle && cell >= 0;

        /// <summary>Times the stall backstop had to put the player on its tile (should stay 0; tests read it).</summary>
        public int Backstops { get; private set; }

        /// <summary>Falls into pits so far (tests read it).</summary>
        public int Falls { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            navigator = GetComponent<CubeNavigator>();
            visual = transform.Find("Visual");
            if (visual != null) visualScale = visual.localScale;
        }

        private void OnDisable() => EndFall();

        private void EndFall()
        {
            if (visual != null) visual.localScale = visualScale;
        }

        private void Reset(IceScreen now)
        {
            EndFall();
            screen = now;
            mode = Mode.Idle;
            cell = target = -1;
            entry = -1;
            falling = sliding = false;
        }

        /// <summary>
        /// Forgets any move under way (a teleport, a face crossing, a rebuild: CubeNavigator calls it on every placement), so the
        /// slider settles afresh on whatever grid the player now stands on; a fall's shrinking is undone at once.
        /// </summary>
        public void Clear() => Reset(null);

        public bool Drive(Vector2 input, float speed, float deltaTime, out Vector2 velocity)
        {
            velocity = Vector2.zero;
            if (body == null) return false;
            if (restoreInterpolationIn > 0 && --restoreInterpolationIn == 0) body.interpolation = savedInterpolation;
            if (navigator != null && (navigator.InCoreArena || navigator.InInterior))
            {
                if (!ReferenceEquals(screen, null)) Reset(null);
                return false;
            }
            Vector2 pos = body.position;
            if (mode == Mode.Falling && screen != null)
            {
                fallLeft -= deltaTime;
                if (visual != null) visual.localScale = visualScale * Mathf.Clamp01(fallLeft / FallSeconds);
                if (fallLeft > 0f) return true;
                int back = entry >= 0 ? entry : NearestLanding(screen, pos - screen.Centre);
                Teleport(screen.CellWorld(back), true);
                EndFall(); // after the jump, so the full-size player is never drawn on the pit
                cell = back;
                mode = Mode.Idle;
                // A held key must not walk the player straight back out of the doorway: wait for a fresh press.
                if (TryGetComponent(out PlayerMover mover)) mover.StopUntilFreshPress();
                PickupPopup.Show(screen.CellWorld(back), FallText, FallTextColor, 1.2f);
                return true;
            }
            IceScreen here = IceScreen.At(pos);
            // ReferenceEquals: a destroyed screen (a rebuild) compares equal to null in Unity, yet must still reset the state.
            if (!ReferenceEquals(here, screen)) Reset(here);
            if (screen == null) return false;
            IceLayout layout = screen.Layout;

            if (mode == Mode.Leaving)
            {
                Vector2 way = IceLayout.Step(leaving);
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
                Vector2 to = screen.CellWorld(target), left = to - pos;
                float step = moveSpeed * deltaTime;
                // A backstop: should anything solid ever hold the body back, it is put on its tile after a few still steps.
                stalled = (pos - lastPos).sqrMagnitude < 1e-8f ? stalled + 1 : 0;
                lastPos = pos;
                if (stalled >= StallSteps)
                {
                    stalled = 0;
                    Backstops++;
                    Teleport(to, false);
                    Arrive(layout);
                    return true;
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
                int near = cell >= 0 ? cell : NearestStandable(screen, pos - screen.Centre);
                if (near < 0) return false;
                cell = -1;
                StartMove(near, speed, false, false);
                return Drive(input, speed, deltaTime, out velocity);
            }
            NoteLanding(layout, cell);
            if (input.magnitude < 0.5f) return true;
            Facing facing = Mathf.Abs(input.x) >= Mathf.Abs(input.y)
                ? (input.x > 0f ? Facing.East : Facing.West)
                : (input.y > 0f ? Facing.North : Facing.South);
            Vector2Int next = IceLayout.Pos(cell) + IceLayout.Step(facing);
            if (!IceLayout.InGrid(next))
            {
                // Off the grid: only a landing's lane leads out, toward its doorway.
                if (layout.LandingOf(cell) == facing)
                {
                    mode = Mode.Leaving;
                    leaving = facing;
                    velocity = (Vector2)IceLayout.Step(facing) * speed;
                }
                return true;
            }
            int n = IceLayout.Index(next);
            int result = layout.Slide(cell, facing, out int end, out _);
            if (result == IceLayout.NoMove)
            {
                // A step within the same lane or camp is a walk; anything else is blocked.
                if (!layout.IsSolid(next) && layout.NodeOf(n) == layout.NodeOf(cell)) StartMove(n, speed, false, false);
                return true;
            }
            var t = layout.TileAt(n);
            bool slide = t == IceTile.Ice || t == IceTile.Pit;
            StartMove(end, speed, slide, result == PuzzleGraph.Fail);
            return Drive(Vector2.zero, speed, deltaTime, out velocity);
        }

        private void StartMove(int to, float speed, bool slide, bool fall)
        {
            target = to;
            sliding = slide;
            falling = fall;
            moveSpeed = Mathf.Max(0.1f, speed) * (slide ? SlideFactor : 1f);
            mode = Mode.Moving;
            stalled = 0;
            lastPos = new Vector2(float.MaxValue, float.MaxValue);
        }

        private void Arrive(IceLayout layout)
        {
            cell = target;
            target = -1;
            mode = Mode.Idle;
            if (falling)
            {
                falling = false;
                Falls++;
                mode = Mode.Falling;
                fallLeft = FallSeconds;
                return;
            }
            if (cell == layout.Goal) screen.ReachGoal();
            NoteLanding(layout, cell);
        }

        private void NoteLanding(IceLayout layout, int at)
        {
            Facing? landing = layout.LandingOf(at);
            if (landing.HasValue) entry = layout.Mouth(landing.Value);
        }

        /// <summary>A landing tile counts as nearest when it is within this of the player (coming in by a doorway's mouth).</summary>
        public const float LandingPull = 2.6f;

        /// <summary>
        /// The tile a player coming onto the grid settles on: the one it stands on (within half a tile), else a landing tile
        /// within LandingPull (so walking in by a doorway
        /// always lands on its lane, never on ice beside it), else the nearest tile it can stand on (not a rock, bank or pit,
        /// and never the goal pad: only a move onto it counts).
        /// </summary>
        public static int NearestStandable(IceScreen screen, Vector2 local)
        {
            int any = Nearest(screen.Layout, local, t => t != IceTile.Rock && t != IceTile.Bank && t != IceTile.Pit && t != IceTile.Goal);
            if (any >= 0 && (IceLayout.CellLocal(any) - local).magnitude < 0.5f) return any; // standing on it already
            int landing = Nearest(screen.Layout, local, t => t == IceTile.Landing);
            if (landing >= 0 && (IceLayout.CellLocal(landing) - local).magnitude <= LandingPull) return landing;
            return any;
        }

        private static int Nearest(IceLayout layout, Vector2 local, System.Func<IceTile, bool> fits)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int c = 0; c < IceLayout.Cells; c++)
            {
                if (!fits(layout.TileAt(c))) continue;
                float d = (IceLayout.CellLocal(c) - local).sqrMagnitude;
                if (d < bestDistance - 1e-6f)
                {
                    bestDistance = d;
                    best = c;
                }
            }
            return best;
        }

        private void Teleport(Vector2 world, bool snap)
        {
            if (snap && restoreInterpolationIn == 0)
            {
                // No in-between frame from the pit to the landing: interpolation off for two steps, as CubeNavigator does.
                savedInterpolation = body.interpolation;
                body.interpolation = RigidbodyInterpolation2D.None;
                restoreInterpolationIn = 2;
            }
            transform.position = new Vector3(world.x, world.y, transform.position.z);
            body.position = world;
            body.linearVelocity = Vector2.zero;
        }

        /// <summary>The lane's resting tile (its mouth) of the landing nearest a point local to the screen's centre.</summary>
        public static int NearestLanding(IceScreen screen, Vector2 local)
        {
            int lane = Nearest(screen.Layout, local, t => t == IceTile.Landing);
            return lane >= 0 ? screen.Layout.NodeOf(lane) : NearestStandable(screen, local);
        }
    }
}
