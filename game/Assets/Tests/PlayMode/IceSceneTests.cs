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
    /// Earth's sliding ice screens in the Cube scene (4.10), played with the real keys: the screens are built as planned (tiles,
    /// banks, the chest on the goal pad, shut); a player walks out of a landing's doorway and back in, landing on its lane;
    /// one solution from the solver is performed move by move, each slide ending on the tile the solver says, and resting on
    /// the goal pad opens the chest; a slide into a pit sinks the player and puts it back on its landing with no damage; and
    /// no swing happens mid-slide.
    /// </summary>
    public class IceSceneTests : InputTestFixture
    {
        private const string SceneName = "Cube";

        /// <summary>The pit move the test takes slides at least this far, so a swing can be tried mid-slide.</summary>
        private const int MinPitSlide = 3;

        private Keyboard keyboard;
        private CubeWorld world;
        private CubeNavigator navigator;
        private EarthFace earth;
        private IceSlider slider;
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
            slider = navigator.GetComponent<IceSlider>();
            body = navigator.GetComponent<Rigidbody2D>();
            Assert.IsNotNull(slider, "The player slides with its IceSlider");
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

        /// <summary>One move of a route: from the resting place From, set off from Cell toward Facing, coming to rest on End (or falling).</summary>
        private struct Step
        {
            public int From, Cell, End;
            public Facing Facing;
            public bool Falls;
        }

        /// <summary>The solver's fewest-moves route from a landing to a target resting place (breadth first over IceLayout.Slide).</summary>
        private static List<Step> Route(IceLayout ice, int start, System.Func<int, Facing, int, int, bool> isTarget)
        {
            var prev = new Dictionary<int, Step> { [start] = default };
            var queue = new Queue<int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int u = queue.Dequeue();
                foreach (int c in ice.CellsOf(u))
                    for (int f = 0; f < 4; f++)
                    {
                        int to = ice.Slide(c, (Facing)f, out int end, out int length);
                        if (to == IceLayout.NoMove) continue;
                        var step = new Step { From = u, Cell = c, End = end, Facing = (Facing)f, Falls = to == PuzzleGraph.Fail };
                        if (isTarget(c, (Facing)f, to, length))
                        {
                            var route = new List<Step> { step };
                            for (int at = u; at != start; at = prev[at].From) route.Insert(0, prev[at]);
                            return route;
                        }
                        if (to < 0 || prev.ContainsKey(to)) continue;
                        prev[to] = step;
                        queue.Enqueue(to);
                    }
            }
            return null;
        }

        /// <summary>Taps one direction and waits until the player rests again (or falls and comes back).</summary>
        private IEnumerator Move(Facing facing)
        {
            yield return this.TapThroughPhysics(KeyFor(keyboard, facing));
            yield return WaitUntilOrTimeout(() => slider.AtRest, 6f);
        }

        /// <summary>Walks within the resting place the player is on (a lane or the camp) to a cell, one tile a tap.</summary>
        private IEnumerator WalkWithin(IceLayout ice, int to)
        {
            for (int guard = 0; guard < 12 && slider.Cell != to; guard++)
            {
                Vector2Int d = IceLayout.Pos(to) - IceLayout.Pos(slider.Cell);
                Facing f = d.x > 0 ? Facing.East : d.x < 0 ? Facing.West : d.y > 0 ? Facing.North : Facing.South;
                yield return Move(f);
            }
            Assert.AreEqual(to, slider.Cell, "walked within the lane or camp");
        }

        private IEnumerator Perform(IceLayout ice, List<Step> route, string what)
        {
            foreach (Step s in route)
            {
                yield return WalkWithin(ice, s.Cell);
                if (s.Falls) yield break;
                yield return Move(s.Facing);
                Assert.AreEqual(s.End, slider.Cell, $"{what}: from {IceLayout.Pos(s.Cell)} toward {s.Facing} it rests on {IceLayout.Pos(s.End)}");
            }
        }

        /// <summary>A seed whose ice screen has a pit the doorway can slide into (mid and late tiers have pits).</summary>
        private static int SeedWithAPit(ModuleLibrary library, out ScreenAddress screen)
        {
            for (int seed = 1; seed <= 50; seed++)
            {
                var model = new CubeModel(seed);
                IcePlan plan = IcePlan.Create(model, CubeLayout.Generate(model, library));
                foreach (IceScreenSpec s in plan.Screens)
                {
                    Facing door = s.Layout.Doors()[0];
                    if (Route(s.Layout, s.Layout.Mouth(door), (c, f, to, n) => to == PuzzleGraph.Fail && n >= MinPitSlide) == null) continue;
                    screen = s.Screen;
                    return seed;
                }
            }
            screen = default;
            return -1;
        }

        [UnityTest]
        public IEnumerator AnIceScreen_IsSolvedWithTheRealKeys_TheChestOpensOnTheGoal_AndAPitPutsThePlayerBack()
        {
            yield return Load();
            int seed = SeedWithAPit(world.Library, out ScreenAddress where);
            Assert.Greater(seed, 0, "some seed has a pit within reach");
            yield return Reveal(seed);
            IceScreen screen = earth.IceScreens.Single(s => s.Screen == where);
            IceLayout ice = screen.Layout;
            Assert.AreEqual(world.Ice.Screens.Count, earth.IceScreens.Count, "every planned ice screen is built");
            Assert.AreEqual(IceLayout.Cells, screen.GetComponentsInChildren<SpriteRenderer>().Count(r => r.transform.parent.name == "Tiles"));
            Assert.IsNotEmpty(screen.Banks, "snowbanks close the strip round the grid");
            RewardChest chest = screen.Chest;
            Assert.IsNotNull(chest, "the goal pad holds the chest");
            Assert.Less(Vector2.Distance(chest.transform.position, screen.CellWorld(ice.Goal)), 0.01f);
            Assert.IsFalse(chest.IsOpen);

            // Land on the screen by a doorway, walk out of it onto the next screen and back in: back on the lane.
            Facing door = ice.Doors().First(d => world.Model.TryStep(where, d, out ScreenAddress n, out _) && n.Face == where.Face);
            navigator.TeleportTo(where, door.Opposite());
            yield return PhysicsSteps(3);
            yield return WaitUntilOrTimeout(() => slider.AtRest, 3f);
            Assert.AreSame(screen, slider.Screen);
            Assert.AreEqual(ice.Mouth(door), ice.NodeOf(slider.Cell), "a teleport lands on the doorway's lane");
            yield return this.HoldUntil(KeyFor(keyboard, door), () => slider.Screen == null && navigator.Current != where, 6f);
            yield return PhysicsSteps(5);
            Assert.AreNotEqual(where, navigator.Current, "walked out of the doorway");
            yield return this.HoldUntil(KeyFor(keyboard, door.Opposite()), () => slider.Screen != null, 6f); // onto the grid: let go
            yield return PhysicsSteps(3);
            yield return WaitUntilOrTimeout(() => slider.AtRest, 3f);
            Assert.AreEqual(ice.Mouth(door), ice.NodeOf(slider.Cell), "walking back in settles on the lane");
            Assert.AreEqual(ice.Mouth(door), slider.EntryCell);

            // A pit: slide into it, sink, and come back on the landing with no damage.
            int start = ice.Mouth(door);
            Health health = navigator.GetComponent<Health>();
            PlayerMover mover = navigator.GetComponent<PlayerMover>();
            Transform visual = navigator.transform.Find("Visual");
            Vector3 scale = visual.localScale;
            float hearts = health.Current;
            List<Step> toPit = Route(ice, start, (c, f, to, n) => to == PuzzleGraph.Fail && n >= MinPitSlide);
            Step fatal = toPit[toPit.Count - 1];
            yield return Perform(ice, toPit, "to the pit");
            int swings = navigator.GetComponent<PlayerAttack>().SwingCount;
            yield return null; // never press right after a physics step
            Press(KeyFor(keyboard, fatal.Facing), queueEventOnly: true);
            yield return WaitUntilOrTimeout(() => slider.IsSliding, 3f);
            Release(KeyFor(keyboard, fatal.Facing), queueEventOnly: true);
            Assert.IsTrue(slider.IsSliding, $"sliding toward the pit ({MinPitSlide}+ tiles)");
            yield return this.Tap(keyboard.enterKey); // a swing mid-slide (or mid-fall) does nothing
            float until = Time.realtimeSinceStartup + 3f;
            while (slider.Falls == 0 && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(1, slider.Falls, "sinks into the pit");
            yield return WaitUntilOrTimeout(() => slider.AtRest, 3f);
            Assert.AreEqual(start, slider.Cell, "back on the landing it came in by");
            Assert.AreEqual(scale, visual.localScale, "full size again on the landing");
            Assert.IsTrue(mover.WaitingForFreshPress, "after a fall the player waits for a fresh press");
            Assert.AreEqual(hearts, health.Current, "a fall never hurts");
            Assert.AreEqual(swings, navigator.GetComponent<PlayerAttack>().SwingCount, "no swing while sliding or falling");

            // Resting on a tile beside the goal pad (the chest's trigger reaches it) leaves the chest shut.
            int beside = -1;
            foreach (Facing f in new[] { Facing.North, Facing.East, Facing.South, Facing.West })
            {
                Vector2Int p = IceLayout.Pos(ice.Goal) + IceLayout.Step(f);
                if (!IceLayout.InGrid(p)) continue;
                IceTile t = ice.TileAt(IceLayout.Index(p));
                if (t == IceTile.Ice || t == IceTile.Grip) beside = IceLayout.Index(p);
            }
            Assert.GreaterOrEqual(beside, 0, "the goal pad has an open neighbour");
            mover.ClearFreshPressWait(); // a test placement, not a press
            slider.Clear();
            body.position = screen.CellWorld(beside);
            navigator.transform.position = screen.CellWorld(beside);
            yield return PhysicsSteps(6);
            Assert.AreEqual(beside, slider.Cell);
            Assert.IsFalse(screen.GoalReached);
            Assert.IsFalse(chest.IsOpen, "beside the pad, the chest stays shut");
            navigator.TeleportTo(where, door.Opposite());
            yield return PhysicsSteps(3);
            yield return WaitUntilOrTimeout(() => slider.AtRest, 3f);
            Assert.AreEqual(start, slider.Cell, "back on the landing");

            // The solution: the solver's route to the goal, played with the keys; resting on the pad opens the chest.
            List<Step> solution = Route(ice, start, (c, f, to, n) => to == ice.Goal);
            Assert.IsNotNull(solution);
            Assert.AreEqual(ice.Measure(door).MinSlides, solution.Count, "the solver's fewest moves");
            yield return Perform(ice, solution, "the solution");
            Assert.AreEqual(ice.Goal, slider.Cell);
            Assert.IsTrue(screen.GoalReached, "resting on the goal pad");
            yield return PhysicsSteps(3);
            Assert.IsTrue(chest.IsOpen, "the chest opens on the goal");
            Assert.AreEqual(0, slider.Backstops, "no move was ever held back by a collider");
        }

        /// <summary>Taps the attack key and says whether a swing happened (the cooldown waited out first).</summary>
        private IEnumerator SwingWorks(System.Action<bool> result)
        {
            PlayerAttack attack = navigator.GetComponent<PlayerAttack>();
            yield return WaitRealSeconds(attack.Cooldown + 0.05f);
            int before = attack.SwingCount;
            yield return this.Tap(keyboard.enterKey, holdFrames: 2);
            result(attack.SwingCount > before);
        }

        [UnityTest]
        public IEnumerator ARerollOrALostScreen_MidSlideOrMidFall_LeavesSwingsWorking_AndTheScaleNormal()
        {
            yield return Load();
            int seed = SeedWithAPit(world.Library, out ScreenAddress where);
            Transform visual = navigator.transform.Find("Visual");
            Vector3 scale = visual.localScale;
            foreach (bool destroyMidFall in new[] { true, false })
            {
                yield return Reveal(seed);
                IceScreen screen = earth.IceScreens.Single(s => s.Screen == where);
                IceLayout ice = screen.Layout;
                Facing door = ice.Doors()[0];
                navigator.TeleportTo(where, door.Opposite());
                yield return PhysicsSteps(3);
                yield return WaitUntilOrTimeout(() => slider.AtRest, 3f);
                List<Step> toPit = Route(ice, ice.Mouth(door), (c, f, to, n) => to == PuzzleGraph.Fail && n >= MinPitSlide);
                Step fatal = toPit[toPit.Count - 1];
                yield return Perform(ice, toPit, "to the pit");
                yield return null;
                Press(KeyFor(keyboard, fatal.Facing), queueEventOnly: true);
                if (destroyMidFall)
                {
                    // The screen goes (as a rebuild destroys it) while the player sinks: the slider must let go.
                    yield return WaitUntilOrTimeout(() => slider.IsFalling, 3f);
                    Release(KeyFor(keyboard, fatal.Facing), queueEventOnly: true);
                    Assert.IsTrue(slider.IsFalling);
                    Object.Destroy(screen.gameObject);
                }
                else
                {
                    // A reroll (a new run) mid-slide.
                    yield return WaitUntilOrTimeout(() => slider.IsSliding, 3f);
                    Release(KeyFor(keyboard, fatal.Facing), queueEventOnly: true);
                    Assert.IsTrue(slider.IsSliding);
                    world.Rebuild(seed + 1);
                }
                yield return PhysicsSteps(4);
                Assert.IsFalse(slider.Busy, $"{(destroyMidFall ? "screen lost mid-fall" : "reroll mid-slide")}: no slide or fall left over");
                Assert.AreEqual(scale, visual.localScale, "full size");
                bool swung = false;
                yield return SwingWorks(s => swung = s);
                Assert.IsTrue(swung, "swings work again");
            }
        }

        [UnityTest]
        public IEnumerator ATeleportOntoAnUnrevealedIceScreen_LandsOnADoorwaysLane_NeverNearTheGoal()
        {
            yield return Load();
            for (int seed = 1; seed <= 3; seed++)
            {
                var model = new CubeModel(seed);
                IcePlan plan = IcePlan.Create(model, CubeLayout.Generate(model, world.Library));
                world.Rebuild(seed);
                yield return null;
                Assert.IsFalse(world.ScienceRevealed, "still unrevealed (the F6 case)");
                foreach (IceScreenSpec s in plan.Screens)
                    foreach (Facing facing in new[] { Facing.North, Facing.East, Facing.South, Facing.West })
                    {
                        navigator.TeleportTo(s.Screen, facing);
                        yield return PhysicsSteps(3);
                        yield return WaitUntilOrTimeout(() => slider.AtRest, 3f);
                        Assert.IsTrue(world.ScienceRevealed);
                        Assert.IsNotNull(slider.Screen, $"seed {seed} {s} {facing}: on the ice grid");
                        Assert.IsTrue(s.Layout.LandingOf(slider.Cell).HasValue, $"seed {seed} {s} {facing}: on a lane");
                        Assert.AreEqual(s.Layout.Mouth(facing.Opposite()), slider.EntryCell, "the doorway behind the arrival");
                        Vector2Int g = IceLayout.Pos(s.Layout.Goal), p = IceLayout.Pos(slider.Cell);
                        Assert.GreaterOrEqual(Mathf.Abs(g.x - p.x) + Mathf.Abs(g.y - p.y), 2, "never on the goal pad or beside it (the generator keeps it off the lanes)");
                    }
            }
        }
    }
}
