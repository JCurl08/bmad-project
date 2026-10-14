using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Tracer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Game.Cube.Tests.TestInput;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Earth's wind-stream screens in the Cube scene (4.11), played with the real keys: the screen is built as planned (tiles,
    /// cliffs, the flow's streaks moving, the chest on the goal pad, shut); a player lands on the ring by a doorway, walks out
    /// of it onto the next screen and back in; the solver's fewest-rides route is played ride by ride, each ride landing where
    /// the solver says, a swing mid-ride does nothing, and standing on the goal pad opens the chest; a stream that only ends
    /// where it set off never takes the player; a reroll mid-ride leaves nothing behind; and a teleport onto an unrevealed wind
    /// screen lands on the ring at the doorway behind the arrival.
    /// </summary>
    public class WindSceneTests : InputTestFixture
    {
        private const string SceneName = "Cube";

        private Keyboard keyboard;
        private CubeWorld world;
        private CubeNavigator navigator;
        private EarthFace earth;
        private WindRider rider;
        private Rigidbody2D body;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        private IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            world = Object.FindAnyObjectByType<CubeWorld>();
            navigator = Object.FindAnyObjectByType<CubeNavigator>();
            earth = world.GetComponent<EarthFace>();
            rider = navigator.GetComponent<WindRider>();
            body = navigator.GetComponent<Rigidbody2D>();
            Assert.IsNotNull(rider, "The player rides with its WindRider");
            navigator.GetComponent<PlayerStats>().MaxHealth = 999;
        }

        private IEnumerator Reveal(int seed)
        {
            world.Rebuild(seed);
            yield return null;
            foreach (IFaceContent content in world.FaceContents)
            {
                if (content is BiologyFace b) b.SpawnEnemies = false;
                if (content is ChemistryFace c) c.SpawnEnemies = false;
                if (content is PhysicsFace p) p.SpawnEnemies = false;
                if (content is MathFace m) m.SpawnEnemies = false;
                if (content is EarthFace e) e.SpawnEnemies = false;
            }
            world.RevealScience();
            world.UnlockAllEdges();
            yield return null;
        }

        /// <summary>One ride of a route: walk to Cell (in the region the player is in), then step Facing onto the stream; it lands on End.</summary>
        private struct Ride
        {
            public int From, Cell, End;
            public Facing Facing;
        }

        /// <summary>The solver's fewest-rides route from a resting place to the goal's (breadth first over WindLayout.RideFrom).</summary>
        private static List<Ride> Route(WindLayout wind, int start)
        {
            int goal = wind.NodeOf(wind.Goal);
            var prev = new Dictionary<int, Ride> { [start] = default };
            var queue = new Queue<int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int u = queue.Dequeue();
                foreach (int c in wind.CellsOf(u))
                    for (int f = 0; f < 4; f++)
                    {
                        Vector2Int p = WindLayout.Pos(c) + WindLayout.Step((Facing)f);
                        if (!WindLayout.InGrid(p)) continue;
                        int end = wind.RideFrom(c, WindLayout.Index(p), out _);
                        if (end < 0) continue;
                        int to = wind.NodeOf(end);
                        if (prev.ContainsKey(to)) continue;
                        prev[to] = new Ride { From = u, Cell = c, End = end, Facing = (Facing)f };
                        if (to == goal)
                        {
                            var route = new List<Ride>();
                            for (int at = to; at != start; at = prev[at].From) route.Insert(0, prev[at]);
                            return route;
                        }
                        queue.Enqueue(to);
                    }
            }
            return null;
        }

        /// <summary>A walk within a region: the tiles from one cell to another, one step each (breadth first).</summary>
        private static List<Facing> Walk(WindLayout wind, int from, int to)
        {
            var prev = new Dictionary<int, (int Cell, Facing Way)> { [from] = (-1, Facing.North) };
            var queue = new Queue<int>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                int u = queue.Dequeue();
                if (u == to) break;
                for (int f = 0; f < 4; f++)
                {
                    Vector2Int p = WindLayout.Pos(u) + WindLayout.Step((Facing)f);
                    if (!WindLayout.InGrid(p)) continue;
                    int n = WindLayout.Index(p);
                    if (prev.ContainsKey(n) || !wind.IsWalkable(n) || wind.NodeOf(n) != wind.NodeOf(from)) continue;
                    // Never across the goal pad on the way (stepping onto it opens the chest): only as the end.
                    if (n == wind.Goal && n != to) continue;
                    prev[n] = (u, (Facing)f);
                    queue.Enqueue(n);
                }
            }
            Assert.IsTrue(prev.ContainsKey(to), $"a walk from {WindLayout.Pos(from)} to {WindLayout.Pos(to)}");
            var steps = new List<Facing>();
            for (int at = to; at != from; at = prev[at].Cell) steps.Insert(0, prev[at].Way);
            return steps;
        }

        /// <summary>Taps one direction and waits until the player rests again.</summary>
        private IEnumerator Move(Facing facing)
        {
            yield return this.TapThroughPhysics(KeyFor(keyboard, facing));
            yield return WaitUntilOrTimeout(() => rider.AtRest, 6f);
        }

        private IEnumerator WalkTo(WindLayout wind, int to)
        {
            foreach (Facing f in Walk(wind, rider.Cell, to))
            {
                int before = rider.Cell;
                yield return Move(f);
                Assert.AreEqual(WindLayout.Index(WindLayout.Pos(before) + WindLayout.Step(f)), rider.Cell, "one tile a tap");
            }
            Assert.AreEqual(to, rider.Cell);
        }

        /// <summary>The first seed whose wind screen needs at least three rides (a mid or late screen, with more to it).</summary>
        private static int SeedWithALongRoute(ModuleLibrary library, out ScreenAddress screen)
        {
            for (int seed = 1; seed <= 50; seed++)
            {
                var model = new CubeModel(seed);
                WindPlan plan = WindPlan.Create(model, CubeLayout.Generate(model, library));
                foreach (WindScreenSpec s in plan.Screens)
                {
                    if (s.Layout.Measure(s.Layout.Doors()[0]).MinRides < 3) continue;
                    screen = s.Screen;
                    return seed;
                }
            }
            screen = default;
            return -1;
        }

        [UnityTest]
        public IEnumerator AWindScreen_IsSolvedWithTheRealKeys_RideByRide_AndTheChestOpensOnTheGoal()
        {
            yield return Load();
            int seed = SeedWithALongRoute(world.Library, out ScreenAddress where);
            Assert.Greater(seed, 0, "some seed has a wind screen of three or more rides");
            yield return Reveal(seed);
            Assert.AreEqual(world.Wind.Screens.Count, earth.WindScreens.Count, "every planned wind screen is built");
            WindScreen screen = earth.WindScreens.Single(s => s.Screen == where);
            WindLayout wind = screen.Layout;
            Assert.AreEqual(WindLayout.Cells, screen.GetComponentsInChildren<SpriteRenderer>().Count(r => r.transform.parent.name == "Tiles"));
            Assert.IsNotEmpty(screen.Cliffs, "cliffs close the strip round the grid");
            Assert.AreEqual(wind.StreamStarts.Count, screen.FlowCount, "every stream shows its flow");
            Vector3[] before = screen.Streaks.Select(t => t.localPosition).ToArray();
            yield return WaitRealSeconds(0.3f);
            Vector3[] after = screen.Streaks.Select(t => t.localPosition).ToArray();
            Assert.IsTrue(before.Zip(after, (a, b) => (a - b).sqrMagnitude > 1e-4f).Any(), "the streaks flow");
            RewardChest chest = screen.Chest;
            Assert.IsNotNull(chest, "the goal pad holds the chest");
            Assert.Less(Vector2.Distance(chest.transform.position, screen.CellWorld(wind.Goal)), 0.01f);
            Assert.IsFalse(chest.IsOpen);

            // Land on the ring by a doorway, walk out of it onto the next screen and back in.
            Facing door = wind.Doors().First(d => world.Model.TryStep(where, d, out ScreenAddress n, out _) && n.Face == where.Face);
            navigator.TeleportTo(where, door.Opposite());
            yield return PhysicsSteps(3);
            yield return WaitUntilOrTimeout(() => rider.AtRest, 3f);
            Assert.AreSame(screen, rider.Screen);
            Assert.AreEqual(WindLayout.DoorCell(door), rider.Cell, "a teleport lands on the ring at the doorway");
            yield return this.HoldUntil(KeyFor(keyboard, door), () => rider.Screen == null && navigator.Current != where, 6f);
            yield return PhysicsSteps(5);
            Assert.AreNotEqual(where, navigator.Current, "walked out of the doorway");
            yield return this.HoldUntil(KeyFor(keyboard, door.Opposite()), () => rider.Screen != null, 6f);
            yield return PhysicsSteps(3);
            yield return WaitUntilOrTimeout(() => rider.AtRest, 3f);
            Assert.AreEqual(WindLayout.HomeNode, wind.NodeOf(rider.Cell), "walking back in settles on the ring");

            Health health = navigator.GetComponent<Health>();
            float hearts = health.Current;
            PlayerAttack attack = navigator.GetComponent<PlayerAttack>();

            // The solution: the solver's route, ride by ride with the keys.
            int start = wind.NodeOf(rider.Cell);
            List<Ride> route = Route(wind, start);
            Assert.IsNotNull(route);
            Assert.AreEqual(wind.Measure(door).MinRides, route.Count, "the solver's fewest rides");
            bool swungMidRide = false;
            for (int i = 0; i < route.Count; i++)
            {
                Ride r = route[i];
                yield return WalkTo(wind, r.Cell);
                int rides = rider.Rides;
                if (i == 0)
                {
                    // The first ride: a swing while the wind carries the player does nothing.
                    yield return null;
                    Press(KeyFor(keyboard, r.Facing), queueEventOnly: true);
                    yield return WaitUntilOrTimeout(() => rider.IsRiding, 3f);
                    Release(KeyFor(keyboard, r.Facing), queueEventOnly: true);
                    Assert.IsTrue(rider.IsRiding, "riding");
                    int swings = attack.SwingCount;
                    yield return this.Tap(keyboard.enterKey);
                    swungMidRide = attack.SwingCount != swings;
                    yield return WaitUntilOrTimeout(() => rider.AtRest, 6f);
                }
                else yield return Move(r.Facing);
                Assert.AreEqual(rides + 1, rider.Rides, $"ride {i + 1} happened");
                Assert.AreEqual(r.End, rider.Cell, $"ride {i + 1}: from {WindLayout.Pos(r.Cell)} toward {r.Facing} it lands on {WindLayout.Pos(r.End)}");
                if (i == 0)
                {
                    // Back into the stream it came by: a stream that ends here never takes the player back.
                    foreach (Facing f in new[] { Facing.North, Facing.East, Facing.South, Facing.West })
                    {
                        Vector2Int p = WindLayout.Pos(r.End) + WindLayout.Step(f);
                        if (!WindLayout.InGrid(p) || wind.TileAt(WindLayout.Index(p)) != WindTile.Stream) continue;
                        if (wind.Next(WindLayout.Index(p)) != r.End) continue;
                        yield return Move(f);
                        Assert.AreEqual(r.End, rider.Cell, "a stream that lands here does not carry the player back");
                        Assert.AreEqual(rides + 1, rider.Rides);
                        break;
                    }
                }
            }
            Assert.IsFalse(swungMidRide, "no swing while riding");
            Assert.AreEqual(wind.NodeOf(wind.Goal), wind.NodeOf(rider.Cell), "on the goal's island");
            Assert.IsFalse(screen.GoalReached || chest.IsOpen, "not yet: the pad itself opens it");
            yield return WalkTo(wind, wind.Goal);
            Assert.IsTrue(screen.GoalReached, "standing on the goal pad");
            yield return PhysicsSteps(3);
            Assert.IsTrue(chest.IsOpen, "the chest opens on the goal");
            Assert.AreEqual(hearts, health.Current, "the wind never hurts");
            Assert.AreEqual(0, rider.Backstops, "no move was ever held back by a collider");
        }

        /// <summary>The first seed whose wind screen is of a tier (the Earth die number decides it).</summary>
        private static int SeedWithTier(ModuleLibrary library, PuzzleTier tier, out ScreenAddress screen)
        {
            for (int seed = 1; seed <= 80; seed++)
            {
                var model = new CubeModel(seed);
                WindPlan plan = WindPlan.Create(model, CubeLayout.Generate(model, library));
                foreach (WindScreenSpec s in plan.Screens)
                {
                    if (s.Tier != tier || s.Layout == null || s.Layout.FromTemplate) continue;
                    screen = s.Screen;
                    return seed;
                }
            }
            screen = default;
            return -1;
        }

        /// <summary>Rides a list of rides with the real keys: walk to each ride's tile, tap its way, check where it lands.</summary>
        private IEnumerator RideAll(WindLayout wind, List<Ride> rides, string what)
        {
            for (int i = 0; i < rides.Count; i++)
            {
                Ride r = rides[i];
                yield return WalkTo(wind, r.Cell);
                int count = rider.Rides;
                yield return Move(r.Facing);
                Assert.AreEqual(count + 1, rider.Rides, $"{what}: ride {i + 1} happened");
                Assert.AreEqual(r.End, rider.Cell, $"{what}: ride {i + 1} lands on {WindLayout.Pos(r.End)}");
            }
        }

        [UnityTest]
        public IEnumerator ALateWindScreen_TakesNineRidesOrMore_ADecoyLeadsAstray_AndTheRealKeysStillSolveIt()
        {
            // 4.25 ("very simple, requiring just 2 crossings"): a late screen needs at least nine rides; one of the ring's rides
            // is a decoy (off every shortest route); riding it costs rides, and the route from there still reaches the goal.
            yield return Load();
            int seed = SeedWithTier(world.Library, PuzzleTier.Late, out ScreenAddress where);
            Assert.Greater(seed, 0, "some seed has a late wind screen");
            yield return Reveal(seed);
            WindScreen screen = earth.WindScreens.Single(s => s.Screen == where);
            WindLayout wind = screen.Layout;
            Facing door = wind.Doors()[0];
            WindMetrics m = wind.Measure(door);
            Assert.GreaterOrEqual(m.MinRides, 9, "nine rides or more to the goal");
            Assert.GreaterOrEqual(m.Decoys, WindTargets.Late.DecoysLo, "decoys");
            Assert.GreaterOrEqual(m.Choices, WindTargets.Late.ChoicesLo, "route choices");
            navigator.TeleportTo(where, door.Opposite());
            yield return PhysicsSteps(3);
            yield return WaitUntilOrTimeout(() => rider.AtRest, 3f);
            int start = wind.NodeOf(rider.Cell);
            List<Ride> best = Route(wind, start);
            Assert.AreEqual(m.MinRides, best.Count);

            // A decoy: a ride from a resting place of the route that leaves every shortest route (the rest from there is longer).
            List<Ride> stray = null, recover = null;
            int at = start, done = 0;
            for (int i = 0; i <= best.Count && stray == null; i++)
            {
                foreach (int c in wind.CellsOf(at))
                {
                    for (int f = 0; f < 4 && stray == null; f++)
                    {
                        Vector2Int p = WindLayout.Pos(c) + WindLayout.Step((Facing)f);
                        if (!WindLayout.InGrid(p)) continue;
                        int end = wind.RideFrom(c, WindLayout.Index(p), out _);
                        if (end < 0 || wind.NodeOf(end) == wind.NodeOf(wind.Goal)) continue;
                        List<Ride> rest = Route(wind, wind.NodeOf(end));
                        if (rest == null || 1 + rest.Count + done <= best.Count) continue;
                        stray = best.Take(done).Append(new Ride { From = at, Cell = c, End = end, Facing = (Facing)f }).ToList();
                        recover = rest;
                    }
                    if (stray != null) break;
                }
                if (stray != null || i == best.Count) break;
                at = wind.NodeOf(best[i].End);
                done = i + 1;
            }
            Assert.IsNotNull(stray, "a decoy ride somewhere along the route");
            yield return RideAll(wind, stray, "to the decoy");
            int taken = rider.Rides;
            yield return RideAll(wind, recover, "back on course");
            Assert.AreEqual(wind.NodeOf(wind.Goal), wind.NodeOf(rider.Cell), "on the goal's island after all");
            Assert.Greater(stray.Count + recover.Count, m.MinRides, "the decoy cost rides");
            yield return WalkTo(wind, wind.Goal);
            Assert.IsTrue(screen.GoalReached, "standing on the goal pad");
            yield return PhysicsSteps(3);
            Assert.IsTrue(screen.Chest.IsOpen, "the chest opens");
            Assert.AreEqual(0, rider.Backstops);
            Assert.Greater(rider.Rides, taken);
        }

        [UnityTest]
        public IEnumerator ALostScreenOrARerollMidRide_LeavesNoRide_AndSwingsWork()
        {
            yield return Load();
            int seed = SeedWithALongRoute(world.Library, out ScreenAddress where);
            foreach (bool destroy in new[] { true, false })
            {
                yield return Reveal(seed);
                WindScreen screen = earth.WindScreens.Single(s => s.Screen == where);
                WindLayout wind = screen.Layout;
                Facing door = wind.Doors()[0];
                navigator.TeleportTo(where, door.Opposite());
                yield return PhysicsSteps(3);
                yield return WaitUntilOrTimeout(() => rider.AtRest, 3f);
                Ride first = Route(wind, wind.NodeOf(rider.Cell))[0];
                yield return WalkTo(wind, first.Cell);
                yield return null;
                Press(KeyFor(keyboard, first.Facing), queueEventOnly: true);
                yield return WaitUntilOrTimeout(() => rider.IsRiding, 3f);
                Release(KeyFor(keyboard, first.Facing), queueEventOnly: true);
                Assert.IsTrue(rider.IsRiding);
                // The screen goes (as a rebuild destroys it, without any teleport), or the run is rerolled, mid-ride.
                if (destroy) Object.Destroy(screen.gameObject);
                else world.Rebuild(seed + 1);
                yield return PhysicsSteps(4);
                string what = destroy ? "the screen lost mid-ride" : "a reroll mid-ride";
                Assert.IsFalse(rider.Busy, $"{what}: no ride left over");
                if (destroy) Assert.IsNull(rider.Screen, $"{what}: off every wind grid");
                PlayerAttack attack = navigator.GetComponent<PlayerAttack>();
                yield return WaitRealSeconds(attack.Cooldown + 0.05f);
                int swings = attack.SwingCount;
                yield return this.Tap(keyboard.enterKey, holdFrames: 2);
                Assert.Greater(attack.SwingCount, swings, $"{what}: swings work again");
            }
        }

        [UnityTest]
        public IEnumerator AHeldKey_NeverRidesOnBySurprise_ALandingOrAnArrivalWaitsForAFreshPress()
        {
            yield return Load();
            int seed = SeedWithALongRoute(world.Library, out ScreenAddress where);
            yield return Reveal(seed);
            WindLayout wind = earth.WindScreens.Single(s => s.Screen == where).Layout;
            PlayerMover mover = navigator.GetComponent<PlayerMover>();
            Facing door = wind.Doors().First(d => world.Model.TryStep(where, d, out ScreenAddress n, out _) && n.Face == where.Face);
            navigator.TeleportTo(where, door.Opposite());
            yield return PhysicsSteps(3);
            yield return WaitUntilOrTimeout(() => rider.AtRest, 3f);

            // Hold the key through a ride's landing: the player stops where it lands and waits.
            Ride first = Route(wind, wind.NodeOf(rider.Cell))[0];
            yield return WalkTo(wind, first.Cell);
            int rides = rider.Rides;
            yield return null;
            Press(KeyFor(keyboard, first.Facing), queueEventOnly: true);
            yield return WaitUntilOrTimeout(() => rider.Rides > rides, 6f);
            yield return PhysicsSteps(30); // still held
            Assert.AreEqual(rides + 1, rider.Rides, "one ride, however long the key is held");
            Assert.IsTrue(rider.AtRest);
            Assert.AreEqual(first.End, rider.Cell, "it stays where it landed");
            Assert.IsTrue(mover.WaitingForFreshPress, "the held key waits for a fresh press");
            Release(KeyFor(keyboard, first.Facing), queueEventOnly: true);
            yield return null;

            // Walk out of a doorway and back in with the key held all the way: it stops on the ring, never riding on.
            navigator.TeleportTo(where, door.Opposite());
            yield return PhysicsSteps(3);
            yield return WaitUntilOrTimeout(() => rider.AtRest, 3f);
            yield return this.HoldUntil(KeyFor(keyboard, door), () => rider.Screen == null && navigator.Current != where, 6f);
            yield return PhysicsSteps(5);
            rides = rider.Rides;
            Press(KeyFor(keyboard, door.Opposite()), queueEventOnly: true);
            yield return WaitUntilOrTimeout(() => rider.Screen != null && rider.AtRest, 6f);
            int arrived = rider.Cell;
            yield return PhysicsSteps(30); // still held
            Assert.AreEqual(WindLayout.HomeNode, wind.NodeOf(rider.Cell), "on the ring");
            Assert.AreEqual(arrived, rider.Cell, "it stays where it came onto the grid: the sky lies ahead (no stream before a doorway)");
            Assert.AreEqual(rides, rider.Rides, "no ride by surprise");
            Release(KeyFor(keyboard, door.Opposite()), queueEventOnly: true);
            yield return null;

            // Put onto the ring right beside a stream the ring sets off on, the key held toward it: it waits for a fresh
            // press instead of riding off.
            int ring = -1;
            Facing toward = Facing.North;
            foreach (int head in wind.StreamStarts)
                for (int f = 0; f < 4 && ring < 0; f++)
                {
                    Vector2Int p = WindLayout.Pos(head) - WindLayout.Step((Facing)f);
                    if (!WindLayout.InGrid(p) || !WindLayout.IsRing(WindLayout.Index(p))) continue;
                    if (wind.RideFrom(WindLayout.Index(p), head, out _) < 0) continue;
                    ring = WindLayout.Index(p);
                    toward = (Facing)f;
                }
            Assert.GreaterOrEqual(ring, 0, "some stream sets off from the ring");
            WindScreen screen = earth.WindScreens.Single(s => s.Screen == where);
            Press(KeyFor(keyboard, toward), queueEventOnly: true);
            yield return PhysicsSteps(2);
            mover.ClearFreshPressWait(); // a test placement, not a press
            rider.Clear();
            body.position = screen.CellWorld(ring);
            navigator.transform.position = screen.CellWorld(ring);
            rides = rider.Rides;
            yield return PhysicsSteps(30); // still held
            Assert.AreEqual(ring, rider.Cell, "it stays beside the stream");
            Assert.AreEqual(rides, rider.Rides, "no ride by surprise");
            Assert.IsTrue(mover.WaitingForFreshPress, "an arrival facing a stream with the key held waits for a fresh press");
            Release(KeyFor(keyboard, toward), queueEventOnly: true);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ATeleportOntoAnUnrevealedWindScreen_LandsOnTheRingAtTheDoorwayBehind()
        {
            yield return Load();
            for (int seed = 1; seed <= 3; seed++)
            {
                var model = new CubeModel(seed);
                WindPlan plan = WindPlan.Create(model, CubeLayout.Generate(model, world.Library));
                world.Rebuild(seed);
                yield return null;
                Assert.IsFalse(world.ScienceRevealed, "still unrevealed (the F6 case)");
                foreach (WindScreenSpec s in plan.Screens)
                    foreach (Facing facing in new[] { Facing.North, Facing.East, Facing.South, Facing.West })
                    {
                        navigator.TeleportTo(s.Screen, facing);
                        yield return PhysicsSteps(3);
                        yield return WaitUntilOrTimeout(() => rider.AtRest, 3f);
                        Assert.IsTrue(world.ScienceRevealed);
                        Assert.IsNotNull(rider.Screen, $"seed {seed} {s} {facing}: on the wind grid");
                        Assert.AreEqual(WindLayout.DoorCell(facing.Opposite()), rider.Cell, $"seed {seed} {s} {facing}: the doorway behind");
                    }
            }
        }
    }
}
