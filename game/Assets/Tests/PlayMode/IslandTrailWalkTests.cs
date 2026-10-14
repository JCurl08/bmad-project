using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Tracer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Game.Cube.Tests.TestInput;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Math's island puzzles walked the way a player walks them (4.17): held keys, never let go between legs, eight ways
    /// (diagonals across an island, a straight hold along a plank and off its far end), the mover enabled at its own speed,
    /// and no teleport once the walk starts. The frame clock runs at 60 frames a second of game time against the 50 Hz
    /// physics step (Time.captureDeltaTime), as in the editor, so the player's drawn position is interpolated between steps.
    /// Each walk comes in through the doorway from the next screen (walked out and back in), then follows the solver's own
    /// plan from that doorway: to a clean start when the doorway's island is not one (stepping out and back over a plank to
    /// snuff), then the solver's trail. Every crossing must light exactly its plank (one more lamp, the plank's glow on),
    /// the snuff must snuff, the last plank solves it, the chain opens the roadblock and walking onto the chest opens it.
    /// The gap doorway is walked too on the seeds where every doorway is: with the gap-crosser* the span is mended by a
    /// swing on the way in.
    /// Legs alternate between a careful walk (lined up at the plank's mouth first) and a direct one (holding the diagonal
    /// toward the far island, sliding along the shore into the plank's mouth), as players do.
    /// </summary>
    public class IslandTrailWalkTests : InputTestFixture
    {
        private const float K = ScreenMath.ContentScale;
        private const string SceneName = "Cube";
        private static readonly Facing[] Sides = { Facing.North, Facing.East, Facing.South, Facing.West };

        /// <summary>The editor's frame clock: 60 frames a game second, against the 50 Hz physics step.</summary>
        public const float FrameSeconds = 1f / 60f;

        private static int FirstSeed => int.TryParse(System.Environment.GetEnvironmentVariable("TRAIL_FIRST"), out int v) ? v : 1;
        private static int LastSeed => int.TryParse(System.Environment.GetEnvironmentVariable("TRAIL_LAST"), out int v) ? v : 50;

        /// <summary>A key for an axis is held while the target is more than this off it (so a plank's walk is one key).</summary>
        private const float AxisBand = 0.15f * K;

        /// <summary>How near a waypoint counts as reached (the keys stay held: the next leg starts at once).</summary>
        private const float Reach = 0.35f * K;

        private Keyboard keyboard;
        private CubeWorld world;
        private CubeNavigator navigator;
        private MathFace math;
        private Inventory inventory;
        private Equipment equipment;
        private PlayerAttack attack;
        private PlayerMover mover;
        private Rigidbody2D body;
        private float baseSpeed;
        private float savedCapture;
        private int savedFrameRate;

        /// <summary>The popups up just before the player walked back in by the doorway (the walk-in's own popup is new).</summary>
        private readonly HashSet<PickupPopup> popupsBefore = new HashSet<PickupPopup>();

        private readonly List<string> failures = new List<string>();
        private string where;
        private bool aborted;
        private int walks, crossings, directLegs, careful, snuffs, chests, gapWalks;
        private Key[] held = new Key[0];
        private float realDeadline;

        /// <summary>A walk that runs past its real-time budget fails (with what it was doing) instead of hanging the run.</summary>
        private bool Overdue(string phase)
        {
            if (aborted) return true;
            if (Time.realtimeSinceStartup < realDeadline) return false;
            Fail($"{phase}: out of real time (game time {Time.time:0.0}, time scale {Time.timeScale}, paused {GamePause.Active}, at {body.position - world.ScreenCenter(navigator.Current)} on {navigator.Current}, fresh press wait {mover.WaitingForFreshPress})");
            return true;
        }

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            held = new Key[0]; // a new keyboard: nothing held yet
            savedCapture = Time.captureDeltaTime;
            savedFrameRate = Application.targetFrameRate;
        }

        public override void TearDown()
        {
            Time.captureDeltaTime = savedCapture;
            Application.targetFrameRate = savedFrameRate;
            if (mover != null) mover.Speed = baseSpeed;
            base.TearDown();
        }

        private IEnumerator Load()
        {
            failures.Clear(); // one fixture serves every test: each starts its own tally
            walks = crossings = directLegs = careful = snuffs = chests = gapWalks = 0;
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            world = Object.FindAnyObjectByType<CubeWorld>();
            navigator = Object.FindAnyObjectByType<CubeNavigator>();
            math = world.GetComponent<MathFace>();
            inventory = navigator.GetComponent<Inventory>();
            equipment = navigator.GetComponent<Equipment>();
            attack = navigator.GetComponent<PlayerAttack>();
            mover = navigator.GetComponent<PlayerMover>();
            body = navigator.GetComponent<Rigidbody2D>();
            baseSpeed = mover.Speed;
            navigator.GetComponent<PlayerStats>().MaxHealth = 999;
        }

        private IEnumerator StartRun(int seed)
        {
            SetHeld();
            world.Rebuild(seed);
            yield return null;
            foreach (IFaceContent content in world.FaceContents)
            {
                if (content is BiologyFace b) b.SpawnEnemies = false;
                if (content is ChemistryFace c) c.SpawnEnemies = false;
                if (content is PhysicsFace p) p.SpawnEnemies = false;
                if (content is EarthFace e) e.SpawnEnemies = false;
            }
            math.SpawnEnemies = false;
            world.RevealScience();
            world.UnlockAllEdges();
            Assert.IsNotNull(math.Plan, $"seed {seed}: the Math plan");
            yield return null;
        }

        /// <summary>The doorways of a screen whose islands are on the lit network (the ones a player can start from at once).</summary>
        private List<Facing> NetworkDoors(IslandScreenSpec spec) =>
            Sides.Where(s => world.Model.TryStep(spec.Screen, s, out _, out _) && spec.Layout.DoorIsland(s) >= 0 &&
                             spec.Layout.OnNetwork(spec.Layout.DoorIsland(s))).ToList();

        private List<Facing> OpenDoors(IslandScreenSpec spec) =>
            Sides.Where(s => world.Model.TryStep(spec.Screen, s, out _, out _) && spec.Layout.DoorIsland(s) >= 0).ToList();

        // ------------------------------------------------------------------ Tests

        /// <summary>Every island screen of seeds 1-50, from one network doorway each (they take turns by seed and screen).</summary>
        [UnityTest, Timeout(3600000)]
        public IEnumerator Seeds1To50_EveryScreenFromANetworkDoorway_TheSolversTrailWalkedWithHeldKeys_LightsEveryPlankAndOpensTheChest()
        {
            yield return Load();
            Time.captureDeltaTime = FrameSeconds;
            int screens = 0;
            for (int seed = FirstSeed; seed <= LastSeed; seed++)
            {
                yield return StartRun(seed);
                for (int s = 0; s < math.Plan.IslandScreens.Count; s++)
                {
                    IslandScreenSpec spec = math.Plan.IslandScreens[s];
                    List<Facing> doors = NetworkDoors(spec);
                    if (doors.Count == 0)
                    {
                        failures.Add($"seed {seed} {spec.Screen}: no doorway on the lit network");
                        continue;
                    }
                    screens++;
                    yield return WalkFrom(seed, spec, doors[(seed + s) % doors.Count], false);
                }
            }
            Report($"{screens} screens");
            Assert.AreEqual(2 * (LastSeed - FirstSeed + 1), screens, "every island screen walked");
        }

        /// <summary>Seeds 1-8: every doorway of every island screen, the gap doorway with the gap-crosser* (a fresh run each).</summary>
        [UnityTest, Timeout(3600000)]
        public IEnumerator Seeds1To8_EveryDoorwayOfEveryScreen_TheSolversTrailWalkedWithHeldKeys_LightsEveryPlankAndOpensTheChest()
        {
            yield return Load();
            Time.captureDeltaTime = FrameSeconds;
            int doorways = 0;
            for (int seed = 1; seed <= 8; seed++)
            {
                yield return StartRun(seed);
                int count = math.Plan.IslandScreens.Count;
                for (int s = 0; s < count; s++)
                {
                    List<Facing> doors = OpenDoors(math.Plan.IslandScreens[s]);
                    foreach (Facing door in doors)
                    {
                        yield return StartRun(seed); // a fresh, unsolved screen for each doorway
                        IslandScreenSpec spec = math.Plan.IslandScreens[s];
                        bool gap = !spec.Layout.OnNetwork(spec.Layout.DoorIsland(door));
                        doorways++;
                        yield return WalkFrom(seed, spec, door, gap);
                    }
                }
            }
            Report($"{doorways} doorways");
            Assert.Greater(gapWalks, 0, "gap doorways were walked with the gap-crosser*");
        }

        /// <summary>
        /// The editor's own frame clock (4.17): real, uneven frame times (no captured delta time, 60 frames a second asked
        /// for), the player drawn interpolated between physics steps. Seeds 1-2, every screen from one network doorway.
        /// </summary>
        [UnityTest, Timeout(1200000)]
        public IEnumerator RealFrameClock_Seeds1To2_TheSolversTrailWalkedWithHeldKeys_LightsEveryPlankAndOpensTheChest()
        {
            yield return Load();
            Time.captureDeltaTime = 0f;
            Application.targetFrameRate = 60; // put back by TearDown
            for (int seed = 1; seed <= 2; seed++)
            {
                yield return StartRun(seed);
                for (int s = 0; s < math.Plan.IslandScreens.Count; s++)
                {
                    IslandScreenSpec spec = math.Plan.IslandScreens[s];
                    List<Facing> doors = NetworkDoors(spec);
                    if (doors.Count == 0)
                    {
                        failures.Add($"seed {seed} {spec.Screen}: no doorway on the lit network");
                        continue;
                    }
                    yield return WalkFrom(seed, spec, doors[(seed + s + 1) % doors.Count], false);
                }
            }
            Report($"real frame clock, {Time.frameCount} frames");
        }

        private void Report(string what)
        {
            SetHeld();
            Debug.Log($"Island trail walk: {what}, {walks} walks ({gapWalks} by a gap doorway), {crossings} crossings " +
                      $"({directLegs} direct, {careful} careful), {snuffs} snuffs, {chests} chests opened; {failures.Count} problems.");
            Assert.IsEmpty(failures, string.Join("\n", failures));
            Assert.Greater(walks, 0);
            Assert.AreEqual(walks, chests, "every walk opened its chest");
        }

        // ------------------------------------------------------------------ One walk

        private IEnumerator WalkFrom(int seed, IslandScreenSpec spec, Facing door, bool gap)
        {
            IslandLayout layout = spec.Layout;
            IslandScreen screen = math.IslandScreens.First(x => x.Layout == layout);
            Vector2 centre = screen.Centre;
            where = $"seed {seed} {spec.Screen} ({layout.Tier}) {door} doorway{(gap ? " (gap, gap-crosser*)" : "")}";
            aborted = false;
            realDeadline = Time.realtimeSinceStartup + 90f;
            int doorIsland = layout.DoorIsland(door);
            if (gap)
            {
                ItemDefinition crosser = world.ItemFor(Theme.Math);
                if (!inventory.Has(crosser)) inventory.Add(crosser);
                equipment.Equip(crosser);
            }
            else equipment.Equip(null);

            // Onto the screen, then out by the doorway and back in, holding the keys.
            string rule = IslandScreen.RuleTextFor(layout, doorIsland);
            var beforeTeleport = new HashSet<PickupPopup>(PickupPopup.Active);
            navigator.TeleportTo(spec.Screen, door.Opposite());
            yield return null;
            yield return null;
            yield return null;
            if (PickupPopup.Active.Any(x => x != null && !beforeTeleport.Contains(x) && x.Text == rule))
                Fail("a teleport onto the screen said the rule (only walking in does)");
            yield return OutAndBackIn(spec.Screen, door, centre, layout);
            if (aborted) yield break;
            // The screen reads the drawn (interpolated) position, a step behind the body: give it a moment to see the arrival.
            float until = Time.time + 0.5f;
            while (!screen.RuleSaid && Time.time < until) yield return null;
            if (screen.Lights.LitCount != 0) Fail($"{screen.Lights.LitCount} planks lit before any crossing");
            // The rule is said by walking in (a popup that was not up before the walk back in), over the doorway's island
            // (the gap doorway says where the glowing planks are), and planks that never light show grey (4.17).
            List<PickupPopup> said = PickupPopup.Active.Where(x => x != null && !popupsBefore.Contains(x) && x.Text == rule).ToList();
            Vector2 over = centre + layout.Islands[doorIsland].center;
            if (!aborted && (rule == null || said.Count != 1))
                Fail($"walking in onto the doorway's island said {(gap ? "the gap doorway's line" : "the rule")} {said.Count} times (want once; walked in {screen.WalkedIn}, said {screen.RuleSaid}, popups up [{string.Join(" | ", PickupPopup.Active.Where(x => x != null).Select(x => x.Text.Split('\n')[0]))}], before {popupsBefore.Count})");
            else if (!aborted && (Mathf.Abs(said[0].Position.x - over.x) > 0.01f || said[0].Position.y < over.y - 0.01f || said[0].Position.y > over.y + 2f * K))
                Fail($"the rule popup stands at {(Vector2)said[0].Position - centre}, not over the doorway's island at {over - centre}");
            if (!gap && !aborted)
            {
                // Out and straight back in while the words are still up: they are not said again.
                yield return OutAndBackIn(spec.Screen, door, centre, layout);
                int again = PickupPopup.Active.Count(x => x != null && !popupsBefore.Contains(x) && x.Text == rule);
                if (!aborted && again != 0) Fail($"walking straight back in said the rule again ({again}) while it was still up");
            }
            if (aborted) yield break;
            for (int k = 0; k < layout.Bridges.Count && !aborted; k++)
            {
                bool grey = !layout.IsNetworkPlank(k);
                if (screen.ShowsGrey(k) != grey || screen.Wash(k).Any(w => w.enabled != grey) || (grey && screen.Wash(k).Count == 0))
                    Fail($"plank {k} ({(layout.IsNetworkPlank(k) ? "lit network" : layout.Bridges[k].Broken ? "broken span" : "lookout")}) shows grey {screen.ShowsGrey(k)}, want {grey}");
            }

            // The plan: (gap) over the span to the network; to a clean start (out and back to snuff); the trail; to the chest.
            IslandSolver solver = layout.Solver;
            int entry = doorIsland;
            int at = doorIsland;
            if (gap)
            {
                entry = Enumerable.Range(0, layout.Islands.Count).Where(layout.OnNetwork)
                    .OrderBy(i => solver.Hops(doorIsland, i, true)).First(i => solver.Hops(doorIsland, i, true) >= 0);
                List<int> way = Route(layout, doorIsland, entry, false);
                for (int i = 0; i < way.Count && !aborted; i++)
                {
                    int k = way[i];
                    if (layout.Bridges[k].Broken)
                    {
                        yield return HopSpan(screen, k, at, centre, layout);
                        if (aborted) yield break;
                    }
                    yield return Cross(screen, k, at, centre, layout, i);
                    at = layout.Bridges[k].Other(at);
                }
                if (aborted) yield break;
                if (screen.Lights.LitCount != 0) Fail($"the way in by the gap doorway lit {screen.Lights.LitCount} planks (lookouts and spans never light)");
                gapWalks++;
            }

            int start;
            if (solver.Starts.Contains(entry)) start = entry;
            else
            {
                start = solver.Starts.Where(solver.CleanStart).OrderBy(x => solver.Hops(entry, x, false, true)).First();
                List<int> route = Route(layout, entry, start, true);
                for (int i = 0; i < route.Count && !aborted; i++)
                {
                    yield return Cross(screen, route[i], at, centre, layout, i);
                    at = layout.Bridges[route[i]].Other(at);
                    Expect(screen, i + 1, route[i], $"on the way to the start {start}");
                }
                if (aborted) yield break;
                if (start != at) Fail($"the way to the start ended on {at}");
                int step = layout.NetworkPlanks.First(x => layout.Bridges[x].Joins(start) && screen.Lights[x] == PlankLight.Unlit);
                yield return Cross(screen, step, start, centre, layout, 0);
                Expect(screen, route.Count + 1, step, "stepping out of the start");
                var results = new List<CrossResult>();
                void Log(IslandScreen _, int __, CrossResult r) => results.Add(r);
                screen.Crossed += Log;
                yield return Cross(screen, step, layout.Bridges[step].Other(start), centre, layout, 1);
                screen.Crossed -= Log;
                if (aborted) yield break;
                if (results.Count != 1 || results[0] != CrossResult.Snuffed || screen.Lights.LitCount != 0)
                    Fail($"stepping back onto the start gave [{string.Join(", ", results)}], {screen.Lights.LitCount} lit (want a snuff)");
                else snuffs++;
                at = start;
            }
            if (aborted) yield break;

            List<int> trail = solver.Trail(start);
            if (trail == null || trail.Count != layout.NetworkPlanks.Count)
            {
                Fail($"the solver has no trail from {start}");
                yield break;
            }
            for (int i = 0; i < trail.Count && !aborted; i++)
            {
                yield return Cross(screen, trail[i], at, centre, layout, i);
                at = layout.Bridges[trail[i]].Other(at);
                Expect(screen, i + 1, trail[i], $"trail plank {i + 1}/{trail.Count}");
                if (!aborted && i + 1 < trail.Count && (screen.Lights.Solved || screen.RoadblockOpen)) Fail($"solved after {i + 1} of {trail.Count}");
            }
            if (aborted) yield break;
            if (!screen.Lights.Solved)
            {
                Fail("every trail plank crossed but the puzzle is not solved");
                yield break;
            }
            // The chain runs while the player walks on to the chest (keys held).
            List<int> toChest = Route(layout, at, layout.RewardIsland, true);
            for (int i = 0; i < toChest.Count && !aborted; i++)
            {
                yield return Cross(screen, toChest[i], at, centre, layout, i);
                at = layout.Bridges[toChest[i]].Other(at);
            }
            if (aborted) yield break;
            RewardChest chest = screen.Chest;
            if (chest == null)
            {
                Fail("no chest on the roadblock");
                yield break;
            }
            float end = Time.time + 6f;
            while (!chest.IsOpen && Time.time < end && !Overdue("onto the chest"))
            {
                HoldToward((Vector2)chest.transform.position, 0.02f * K);
                yield return null;
            }
            SetHeld();
            if (!screen.RoadblockOpen) Fail("the roadblock never opened");
            if (!chest.IsOpen) Fail($"walking onto the chest at {(Vector2)chest.transform.position - centre} did not open it (player at {body.position - centre})");
            else chests++;
            walks++;
            Debug.Log($"Island trail walk {walks}: {where}, {failures.Count} problems, t={Time.time:0}s game, {Time.realtimeSinceStartup:0}s real, frame {Time.frameCount}");
            yield return null;
        }

        /// <summary>The planks expected lit after a crossing: exactly lit of them, the last crossed one among them, glowing.</summary>
        private void Expect(IslandScreen screen, int lit, int plank, string what)
        {
            if (aborted) return;
            if (screen.Lights.LitCount != lit || screen.Lights[plank] != PlankLight.Lit)
                Fail($"{what}: plank {plank} shows {screen.Lights[plank]} with {screen.Lights.LitCount} lit (want {lit}); walker on island {screen.Walker.LastIsland}, plank {screen.Walker.OnPlank}, player at {body.position - screen.Centre}");
            else if (!screen.Glow(plank).enabled || screen.Flashing)
                Fail($"{what}: plank {plank} is lit but its glow is off");
            else if (screen.Lamps.Count(l => l.color == IslandScreen.LampOn) != lit)
                Fail($"{what}: {screen.Lamps.Count(l => l.color == IslandScreen.LampOn)} lamps on for {lit} planks");
        }

        private void Fail(string what)
        {
            Debug.Log($"Island trail walk problem: {where}: {what}");
            failures.Add($"{where}: {what}");
            aborted = true;
            SetHeld();
        }

        /// <summary>The planks of the fewest-plank way between two islands (networkOnly: whole network planks; else any plank or span).</summary>
        private static List<int> Route(IslandLayout layout, int from, int to, bool networkOnly)
        {
            var previous = new Dictionary<int, (int Island, int Plank)> { [from] = (from, -1) };
            var queue = new Queue<int>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                int at = queue.Dequeue();
                if (at == to) break;
                for (int k = 0; k < layout.Bridges.Count; k++)
                {
                    IslandBridge b = layout.Bridges[k];
                    if (!b.Joins(at) || previous.ContainsKey(b.Other(at))) continue;
                    if (networkOnly && !layout.IsNetworkPlank(k)) continue;
                    previous[b.Other(at)] = (at, k);
                    queue.Enqueue(b.Other(at));
                }
            }
            Assert.IsTrue(previous.ContainsKey(to), $"no way from island {from} to {to}");
            var route = new List<int>();
            for (int i = to; i != from; i = previous[i].Island) route.Add(previous[i].Plank);
            route.Reverse();
            return route;
        }

        // ------------------------------------------------------------------ Walking with held keys

        /// <summary>Exactly these keys down (one state event, so a diagonal never passes through a one-key frame).</summary>
        private void SetHeld(params Key[] keys)
        {
            if (keyboard == null) return;
            if (keys.Length == held.Length && keys.All(held.Contains)) return;
            held = keys;
            SetKeys(keyboard, keys);
        }

        /// <summary>Holds the 8-way keys toward a point (world): an axis's key while the point is more than band off it.</summary>
        private void HoldToward(Vector2 target, float band)
        {
            Vector2 d = target - body.position;
            var dir = new Vector2Int(Mathf.Abs(d.x) > band ? (int)Mathf.Sign(d.x) : 0, Mathf.Abs(d.y) > band ? (int)Mathf.Sign(d.y) : 0);
            SetHeld(WasdKeys(dir));
        }

        /// <summary>Holds toward a waypoint until within Reach (the keys stay held after), or fails when stuck or out of time.</summary>
        private IEnumerator HoldTo(Vector2 target, string leg, float slack = 3f)
        {
            float end = Time.time + (target - body.position).magnitude / baseSpeed + slack;
            Vector2 last = body.position;
            float stillSince = Time.time;
            while ((target - body.position).magnitude > Reach && !Overdue(leg))
            {
                if (mover.WaitingForFreshPress && held.Length > 0)
                {
                    SetHeld(); // a face crossing waits for every key let go, then a fresh press
                    yield return null;
                    continue;
                }
                HoldToward(target, AxisBand);
                if (held.Length == 0) break;
                yield return null;
                if ((body.position - last).sqrMagnitude > 1e-4f)
                {
                    last = body.position;
                    stillSince = Time.time;
                }
                if (Time.time - stillSince > 0.75f || Time.time > end)
                {
                    Fail($"{leg}: stuck at {body.position - world.ScreenCenter(navigator.Current)} holding [{string.Join("+", held)}] toward {target - world.ScreenCenter(navigator.Current)}");
                    yield break;
                }
            }
        }

        /// <summary>A plank's mouth on an island's shore (local): the middle of its end there.</summary>
        private static Vector2 Mouth(IslandBridge b, int island, IslandLayout layout)
        {
            Rect p = b.Plank;
            Vector2 toOther = layout.Islands[b.Other(island)].center - layout.Islands[island].center;
            if (b.Vertical) return new Vector2(p.center.x, toOther.y > 0 ? p.yMin : p.yMax);
            return new Vector2(toOther.x > 0 ? p.xMin : p.xMax, p.center.y);
        }

        private static Vector2 Along(IslandBridge b, int from, IslandLayout layout)
        {
            Vector2 toOther = layout.Islands[b.Other(from)].center - layout.Islands[from].center;
            return b.Vertical ? new Vector2(0f, Mathf.Sign(toOther.y)) : new Vector2(Mathf.Sign(toOther.x), 0f);
        }

        /// <summary>
        /// Crosses plank k from island from to its other island with held keys: a careful leg lines up on the plank's mouth
        /// first; a direct leg (every other one) holds the diagonal toward the far landing at once and slides along the
        /// shore into the mouth. Either way the walk goes on past the far shore onto the island, and the keys stay held.
        /// </summary>
        private IEnumerator Cross(IslandScreen screen, int k, int from, Vector2 centre, IslandLayout layout, int index)
        {
            if (aborted) yield break;
            IslandBridge b = layout.Bridges[k];
            int to = b.Other(from);
            Vector2 axis = Along(b, from, layout);
            Vector2 far = centre + Mouth(b, to, layout) + axis * (0.6f * K);
            bool direct = index % 2 == 1 || b.Broken; // 4.25: past a hopped gap there is no lining up behind it
            if (direct) directLegs++;
            else
            {
                careful++;
                yield return HoldTo(centre + Mouth(b, from, layout) - axis * (0.6f * K), $"lining up on plank {k} from island {from}");
                if (aborted) yield break;
            }
            yield return HoldTo(far, $"over plank {k} from island {from} to {to}{(direct ? " (direct)" : "")}");
            if (aborted) yield break;
            crossings++;
            int island = layout.IslandAt(body.position - centre);
            if (island != to) Fail($"over plank {k}: ended on island {island}, not {to}");
        }

        /// <summary>
        /// 4.25: walks up to a broken span's gap from an island, the gap stopping the player, then [Attack] (Enter, the real key)
        /// with the gap-crosser* hops it: the player lands past the gap, and the gap stays (grey, solid, unlit).
        /// </summary>
        private IEnumerator HopSpan(IslandScreen screen, int k, int from, Vector2 centre, IslandLayout layout)
        {
            IslandBridge b = layout.Bridges[k];
            BrokenSpan span = screen.Gaps.First(g => g.Bridge == k);
            Vector2 axis = Along(b, from, layout);
            yield return HoldTo(centre + Mouth(b, from, layout) - axis * (0.6f * K), $"lining up on broken plank {k}");
            if (aborted) yield break;
            // Hold on toward the gap until the gap stops the player.
            Vector2 gapNear = centre + b.Gap.center - axis * (b.Vertical ? b.Gap.height : b.Gap.width) / 2f;
            float end = Time.time + 4f;
            Vector2 last = body.position;
            int still = 0;
            while (Time.time < end && still < 6 && !Overdue("up to the gap"))
            {
                HoldToward(gapNear + axis, AxisBand);
                yield return null;
                still = (body.position - last).sqrMagnitude < 1e-6f ? still + 1 : 0;
                last = body.position;
            }
            SetHeld();
            int hops = span.Hops;
            float before = Vector2.Dot(body.position - centre - b.Gap.center, axis);
            yield return new WaitForSeconds(attack.Cooldown + 0.05f);
            yield return this.Tap(keyboard.enterKey);
            yield return null;
            float after = Vector2.Dot(body.position - centre - b.Gap.center, axis);
            if (span.Hops != hops + 1) Fail($"[Attack] at broken plank {k}'s gap from {body.position - centre} did not hop it");
            else if (before > 0f || after < IslandLayout.GapLength / 2f + TimeField.PlayerRadius - 0.02f)
                Fail($"the hop over broken plank {k} went from {before:0.00} to {after:0.00} along it (want from before the gap to past it)");
            else if (!screen.ShowsGrey(k) || !span.Solid.enabled || screen.Glow(k).enabled)
                Fail($"hopped broken plank {k} no longer shows a grey gap (grey {screen.ShowsGrey(k)}, solid {span.Solid.enabled})");
        }

        /// <summary>Walks out by the doorway onto the next screen and back in, keys held (a fresh press after a face crossing).</summary>
        private IEnumerator OutAndBackIn(ScreenAddress screen, Facing side, Vector2 centre, IslandLayout layout)
        {
            bool faceEdge = ScreenEdges.IsFaceEdge(world.FaceSize, screen.Cell, side);
            float end = Time.time + 4f;
            SetHeld(WasdKeys(Vector2Int.RoundToInt(side.ToVector())));
            while (navigator.Current == screen && Time.time < end && !Overdue("walking out"))
            {
                // Just in over a face crossing, the player waits for every key let go and a fresh press.
                if (mover.WaitingForFreshPress && held.Length > 0) SetHeld();
                else SetHeld(WasdKeys(Vector2Int.RoundToInt(side.ToVector())));
                yield return null;
            }
            if (navigator.Current == screen)
            {
                Fail($"could not walk out by the doorway (at {body.position - centre}; held [{string.Join("+", held)}], A down {keyboard.aKey.isPressed}, D down {keyboard.dKey.isPressed}, move {mover.MoveInput}, fresh wait {mover.WaitingForFreshPress}, locked {world.Locks?.IsLocked(navigator.Face, side)}, paused {GamePause.Active})");
                yield break;
            }
            Facing back = faceEdge ? navigator.Facing.Opposite() : side.Opposite();
            SetHeld();
            yield return null;
            yield return null;
            popupsBefore.Clear();
            popupsBefore.UnionWith(PickupPopup.Active);
            end = Time.time + 4f;
            SetHeld(WasdKeys(Vector2Int.RoundToInt(back.ToVector())));
            while (navigator.Current != screen && Time.time < end && !Overdue("walking back in")) yield return null;
            if (navigator.Current != screen) Fail($"pressing {back} did not come back in (at {navigator.Current})");
            // Keep walking in (a face crossing stops the player until a fresh press) until on the doorway's island.
            int doorIsland = layout.DoorIsland(side);
            end = Time.time + 3f;
            while (!Overdue("onto the doorway island") && layout.IslandAt(body.position - centre) != doorIsland && Time.time < end)
            {
                if (mover.WaitingForFreshPress && held.Length > 0) SetHeld();
                else SetHeld(WasdKeys(Vector2Int.RoundToInt(side.Opposite().ToVector())));
                yield return null;
            }
            if (!aborted && layout.IslandAt(body.position - centre) != doorIsland) Fail($"walking in did not reach the doorway's island (at {body.position - centre})");
        }
    }
}
