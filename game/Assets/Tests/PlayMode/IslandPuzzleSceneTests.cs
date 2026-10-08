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
    /// A Math island screen's "light every plank" puzzle solved in the real Cube scene with the real keys (4.12). On a late
    /// screen (its starts are no doorway's island, so the player has to make a clean start):
    /// a teleport onto the far end of a plank the player stood on is no crossing (4.12 review);
    /// the reward chest stands on its plinth under bars, with one dark lamp per network plank, and stays shut when touched;
    /// a plank lit and then the screen left by its doorway comes back dark; the player walks to a start, steps out and back
    /// (the second crossing snuffs everything with a red flash, leaving it on the start), then walks the solver's trail plank by
    /// plank: each crossing lights one more plank and one more lamp, an island's rim glows once its planks are all lit, and
    /// the chest stays shut until the last plank; then the light chain runs to the roadblock, its bars lift, and walking onto
    /// the chest opens it. Leaving and coming back, the solved screen stays lit.
    /// </summary>
    public class IslandPuzzleSceneTests : InputTestFixture
    {
        private const float K = ScreenMath.ContentScale;
        private const string SceneName = "Cube";

        private Keyboard keyboard;
        private CubeWorld world;
        private CubeNavigator navigator;
        private MathFace math;
        private PlayerMover mover;
        private Rigidbody2D body;
        private float baseSpeed;
        private float savedCapture;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            savedCapture = Time.captureDeltaTime;
        }

        public override void TearDown()
        {
            Time.captureDeltaTime = savedCapture;
            if (mover != null) mover.Speed = baseSpeed;
            base.TearDown();
        }

        private IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            world = Object.FindAnyObjectByType<CubeWorld>();
            navigator = Object.FindAnyObjectByType<CubeNavigator>();
            math = world.GetComponent<MathFace>();
            mover = navigator.GetComponent<PlayerMover>();
            body = navigator.GetComponent<Rigidbody2D>();
            baseSpeed = mover.Speed;
            navigator.GetComponent<PlayerStats>().MaxHealth = 999;
        }

        private IEnumerator StartRun(int seed)
        {
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
            Assert.IsNotNull(math.Plan);
            yield return null;
        }

        /// <summary>The first seed whose Math tier is late (no start is a doorway's island).</summary>
        private static int LateSeed()
        {
            for (int seed = 1; seed <= 200; seed++)
            {
                var model = new CubeModel(seed);
                if (IslandPlan.TierOf(model) == PuzzleTier.Late) return seed;
            }
            Assert.Fail("no late Math face in seeds 1-200");
            return -1;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator ALateIslandScreen_IsSolvedWithTheRealKeys_TheRoadblockOpeningOnlyOnceEveryPlankIsLit()
        {
            int seed = LateSeed();
            yield return Load();
            yield return StartRun(seed);
            Time.captureDeltaTime = Time.fixedDeltaTime;

            IslandScreenSpec spec = math.Plan.IslandScreens[0];
            IslandLayout layout = spec.Layout;
            Assert.AreEqual(PuzzleTier.Late, layout.Tier);
            IslandScreen screen = math.IslandScreens.First(s => s.Layout == layout);
            Vector2 centre = screen.Centre;
            RewardChest chest = screen.Chest;
            Assert.IsNotNull(chest, "CubeWorld handed the roadblock's chest to the screen");
            Assert.AreSame(screen, chest.Guard, "the screen keeps the chest shut");
            Assert.AreEqual(layout.RewardIsland, layout.IslandAt((Vector2)chest.transform.position - centre), "on the reward island");
            int planks = layout.NetworkPlanks.Count;
            Assert.AreEqual(planks, screen.Lamps.Count, "one lamp per plank");
            Assert.IsTrue(screen.Lamps.All(l => l.color == IslandScreen.LampOff));
            Assert.IsTrue(screen.Bars.All(b => b.enabled), "the bars are down");

            // The chest stays shut when touched before the puzzle is solved.
            Facing door = new[] { Facing.North, Facing.East, Facing.South, Facing.West }
                .First(d => layout.DoorIsland(d) >= 0 && layout.OnNetwork(layout.DoorIsland(d)));
            int doorIsland = layout.DoorIsland(door);
            Assert.IsFalse(layout.Solver.Starts.Contains(doorIsland), "late: the doorway's island is no start");
            // A teleport is never a crossing (4.12 review): stood on the island next to the doorway's, then on the plank
            // between, then teleported onto the doorway's island at the plank's far end.
            int k0 = layout.NetworkPlanks.First(k => layout.Bridges[k].Joins(doorIsland));
            int a0 = layout.Bridges[k0].Other(doorIsland);
            navigator.TeleportTo(spec.Screen, door.Opposite());
            yield return PutPlayerAt(centre + layout.Islands[a0].center);
            yield return PutPlayerAt(centre + layout.Bridges[k0].Plank.center);
            Assert.AreEqual(k0, screen.Walker.OnPlank, "on the plank, from its near island");
            navigator.TeleportTo(spec.Screen, door.Opposite());
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(doorIsland, layout.IslandAt(body.position - centre), "teleported onto the doorway's island");
            Assert.AreEqual(0, screen.Lights.LitCount, "a teleport is no crossing");

            navigator.TeleportTo(spec.Screen, door.Opposite());
            yield return PutPlayerAt(chest.transform.position);
            yield return new WaitForSeconds(0.2f);
            Assert.IsFalse(chest.IsOpen, "shut while the planks are dark");

            // In by the doorway; one plank lit, then out of the doorway and back: the unsolved screen is dark again.
            navigator.TeleportTo(spec.Screen, door.Opposite());
            yield return PutPlayerAt(centre + IslandLayout.Arrival(door));
            yield return WalkTo(Spot(layout, doorIsland), doorIsland, layout);
            int first = layout.NetworkPlanks.First(k => layout.Bridges[k].Joins(doorIsland));
            int over = layout.Bridges[first].Other(doorIsland);
            yield return WalkTo(Spot(layout, over), over, layout);
            Assert.AreEqual(1, screen.Lights.LitCount, "a crossed plank lights");
            Assert.AreEqual(IslandScreen.LampOn, screen.Lamps[0].color, "and its lamp");
            yield return Teleport(spec.Screen, door, doorIsland, layout);
            yield return HoldUntil(door, () => navigator.Current != spec.Screen, 4f);
            Assert.AreNotEqual(spec.Screen, navigator.Current, "walked out");
            Assert.AreEqual(0, screen.Lights.LitCount, "leaving snuffs an unsolved screen");
            Facing back = ScreenEdges.IsFaceEdge(world.FaceSize, spec.Screen.Cell, door) ? navigator.Facing.Opposite() : door.Opposite();
            yield return HoldUntil(back, () => navigator.Current == spec.Screen, 4f);
            Assert.AreEqual(spec.Screen, navigator.Current, "back in");
            yield return WalkTo(Spot(layout, doorIsland), doorIsland, layout);

            // To a start, then out and back over one of its planks: the second crossing snuffs everything (a red flash)
            // and leaves the player on the start with nothing lit.
            int start = layout.Solver.Starts.Where(layout.Solver.CleanStart)
                .OrderBy(s => layout.Solver.Hops(doorIsland, s, false, true)).First();
            List<int> route = Route(layout, doorIsland, start);
            for (int i = 1; i < route.Count; i++) yield return WalkTo(Spot(layout, route[i]), route[i], layout);
            Assert.AreEqual(route.Count - 1, screen.Lights.LitCount, "the walk there lit its planks");
            int step = layout.NetworkPlanks.First(k => layout.Bridges[k].Joins(start) && screen.Lights[k] == PlankLight.Unlit);
            int aside = layout.Bridges[step].Other(start);
            yield return WalkTo(Spot(layout, aside), aside, layout);
            var events = new List<CrossResult>();
            screen.Crossed += (_, __, r) => events.Add(r);
            yield return WalkTo(Spot(layout, start), start, layout);
            CollectionAssert.AreEqual(new[] { CrossResult.Snuffed }, events, "crossed again: snuffed");
            Assert.AreEqual(0, screen.Lights.LitCount);
            Assert.IsTrue(screen.Flashing, "a red flash");
            Assert.IsTrue(layout.NetworkPlanks.All(k => screen.Glow(k).enabled && screen.Glow(k).color == IslandScreen.SnuffGlow), "every plank flashes red");
            yield return new WaitForSeconds(IslandScreen.FlashSeconds + 0.1f);

            // The solver's trail, plank by plank: one more plank and one more lamp each time, rims once an island is done.
            List<int> trail = layout.Solver.Trail(start);
            Assert.AreEqual(planks, trail.Count);
            int at = start;
            for (int i = 0; i < trail.Count; i++)
            {
                int next = layout.Bridges[trail[i]].Other(at);
                yield return WalkTo(Spot(layout, next), next, layout);
                Assert.AreEqual(PlankLight.Lit, screen.Lights[trail[i]], $"plank {trail[i]} lit");
                Assert.AreEqual(i + 1, screen.Lights.LitCount, $"after {i + 1} planks");
                Assert.AreEqual(i + 1, screen.Lamps.Count(l => l.color == IslandScreen.LampOn), "one lamp per lit plank");
                for (int island = 0; island < layout.Islands.Count; island++)
                    if (layout.OnNetwork(island))
                        Assert.AreEqual(screen.Lights.IslandComplete(island), screen.Rim(island).activeSelf, $"island {island}'s rim");
                if (i + 1 < trail.Count)
                {
                    Assert.IsFalse(screen.Lights.Solved);
                    Assert.IsFalse(screen.RoadblockOpen, "the roadblock stays shut");
                    Assert.IsFalse(chest.IsOpen, "the chest stays shut");
                }
                at = next;
            }
            Assert.IsTrue(screen.Lights.Solved, "every plank lit");
            Assert.IsTrue(screen.ChainRunning, "the light chain runs to the roadblock");
            Assert.IsTrue(Enumerable.Range(0, layout.Islands.Count).Where(layout.OnNetwork).All(i => screen.Rim(i).activeSelf), "every rim glows");
            Assert.IsFalse(screen.RoadblockOpen);
            yield return new WaitForSeconds(IslandScreen.ChainSeconds + 0.2f);
            Assert.IsTrue(screen.RoadblockOpen, "the chain reached the roadblock");
            Assert.IsTrue(screen.Bars.All(b => !b.enabled), "its bars lifted");

            // Onto the chest: it opens.
            List<int> toChest = Route(layout, at, layout.RewardIsland);
            for (int i = 1; i < toChest.Count; i++) yield return WalkTo(Spot(layout, toChest[i]), toChest[i], layout);
            yield return PutPlayerAt(chest.transform.position);
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(chest.IsOpen, "the chest opens once the roadblock is open");

            // Out of the doorway and back: the solved screen stays lit.
            yield return Teleport(spec.Screen, door, doorIsland, layout);
            yield return HoldUntil(door, () => navigator.Current != spec.Screen, 4f);
            Assert.IsTrue(screen.Lights.Solved && screen.Lights.LitCount == planks, "a solved screen stays lit when left");
            back = ScreenEdges.IsFaceEdge(world.FaceSize, spec.Screen.Cell, door) ? navigator.Facing.Opposite() : door.Opposite();
            yield return HoldUntil(back, () => navigator.Current == spec.Screen, 4f);
            yield return WalkTo(Spot(layout, doorIsland), doorIsland, layout);
            Assert.AreEqual(planks, screen.Lights.LitCount, "and when come back to");
            Assert.IsTrue(screen.RoadblockOpen);
        }

        private Vector2 Spot(IslandLayout layout, int island) =>
            world.ScreenCenter(math.Plan.IslandScreens.First(s => s.Layout == layout).Screen) + IslandLayout.SpotCentre(layout.Spots[island]);

        /// <summary>Fewest whole planks from one island to another (a list of islands), over the lit network.</summary>
        private static List<int> Route(IslandLayout layout, int from, int to)
        {
            var previous = new Dictionary<int, int> { [from] = from };
            var queue = new Queue<int>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                int at = queue.Dequeue();
                if (at == to)
                {
                    var route = new List<int> { at };
                    while (at != from) route.Add(at = previous[at]);
                    route.Reverse();
                    return route;
                }
                foreach (int k in layout.NetworkPlanks)
                {
                    IslandBridge b = layout.Bridges[k];
                    if (!b.Joins(at) || previous.ContainsKey(b.Other(at))) continue;
                    previous[b.Other(at)] = at;
                    queue.Enqueue(b.Other(at));
                }
            }
            Assert.Fail($"no route from island {from} to {to}");
            return null;
        }

        /// <summary>To the doorway's island by teleport (no plank crossed), on its middle.</summary>
        private IEnumerator Teleport(ScreenAddress screen, Facing door, int doorIsland, IslandLayout layout)
        {
            navigator.TeleportTo(screen, door.Opposite());
            yield return PutPlayerAt(world.ScreenCenter(screen) + IslandLayout.Arrival(door));
            yield return WalkTo(Spot(layout, doorIsland), doorIsland, layout);
        }

        private IEnumerator PutPlayerAt(Vector2 at)
        {
            body.position = at;
            body.linearVelocity = Vector2.zero;
            navigator.transform.position = new Vector3(at.x, at.y, navigator.transform.position.z);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        private IEnumerator HoldUntil(Facing toward, System.Func<bool> condition, float gameSeconds)
        {
            yield return null;
            mover.Speed = baseSpeed;
            Press(KeyFor(keyboard, toward), queueEventOnly: true);
            yield return WaitUntilOrGameTimeout(condition, gameSeconds);
            Release(KeyFor(keyboard, toward), queueEventOnly: true);
            yield return null;
            yield return null;
        }

        /// <summary>
        /// Walks with one key straight toward a point (world) until it is reached, slowing for the last step so the player
        /// stops on it; it must end on the island expected, within reach of the point.
        /// </summary>
        private IEnumerator WalkTo(Vector2 target, int expect, IslandLayout layout)
        {
            Vector2 centre = world.ScreenCenter(math.Plan.IslandScreens.First(s => s.Layout == layout).Screen);
            Vector2 d = target - body.position;
            Facing toward = Mathf.Abs(d.x) >= Mathf.Abs(d.y) ? (d.x > 0f ? Facing.East : Facing.West) : (d.y > 0f ? Facing.North : Facing.South);
            Vector2 axis = toward.ToVector();
            float step = Time.fixedDeltaTime;
            yield return null;
            mover.Speed = baseSpeed;
            Press(KeyFor(keyboard, toward), queueEventOnly: true);
            float end = Time.time + d.magnitude / baseSpeed + 1.5f;
            Vector2 last = body.position;
            int still = 0;
            while (Time.time < end)
            {
                yield return null;
                float remaining = Vector2.Dot(target - body.position, axis);
                if (remaining < 0.02f * K) break;
                mover.Speed = Mathf.Clamp(remaining / step, 0.2f * K, baseSpeed);
                still = (body.position - last).sqrMagnitude < 1e-6f ? still + 1 : 0;
                last = body.position;
                if (still > 8) break;
            }
            mover.Speed = 0f;
            Release(KeyFor(keyboard, toward), queueEventOnly: true);
            yield return null;
            yield return new WaitForFixedUpdate();
            yield return null;
            mover.Speed = baseSpeed;
            Vector2 local = body.position - centre;
            Assert.AreEqual(expect, layout.IslandAt(local), $"walking to island {expect} stopped at {local}");
            Assert.Less((body.position - target).magnitude, 0.3f * K, $"walking to island {expect} stopped short at {local}");
        }
    }
}
