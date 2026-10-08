using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Game.Tracer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Game.Cube.Tests.TestInput;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The Bridgekeeper*'s face in the real Cube scene. The Math face is unsealed and revealed with the rest; the reveal
    /// builds the planned gap gates (on Math and at least one on another face), the island screens (solid water, islands,
    /// planks, broken spans) and the trial on its island. With the real player and keys: an island screen is walked through
    /// from one doorway to the doorway its planks join, out onto the next screen; the gap-crosser* swung at the trial's
    /// broken span lays the plank and stepping onto the trial island completes it once; swinging the gap-crosser* at every
    /// gap gate (the off-face one included) opens it. Shape people are present, explain the islands and the gap-crosser*,
    /// and stand off the island screens.
    /// </summary>
    public class MathSceneTests : InputTestFixture
    {
        /// <summary>Lengths and speeds below are in the first 16x10 screens' units, scaled like the game's (4.1).</summary>
        private const float K = ScreenMath.ContentScale;

        private const string SceneName = "Cube";

        private Keyboard keyboard;
        private CubeWorld world;
        private CubeNavigator navigator;
        private MathFace math;
        private Inventory inventory;
        private PlayerAttack attack;
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
            Assert.IsNotNull(world);
            math = world.GetComponent<MathFace>();
            Assert.IsNotNull(math, "The CubeWorld carries the MathFace content hook");
            CollectionAssert.Contains(world.FaceContents, math);
            inventory = navigator.GetComponent<Inventory>();
            attack = navigator.GetComponent<PlayerAttack>();
            body = navigator.GetComponent<Rigidbody2D>();
            navigator.GetComponent<PlayerStats>().MaxHealth = 999;
        }

        private IEnumerator StartRunOntoMath(int seed)
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
            Assert.IsFalse(world.Model.IsSealed(world.Model.FaceOf(Theme.Math)), "Math is unsealed");
            world.RevealScience();
            Assert.IsTrue(world.ScienceRevealed);
            Assert.IsNotNull(math.Plan);
            yield return null;
        }

        private IEnumerator PutPlayerAt(ScreenAddress screen, Vector2 at)
        {
            if (navigator.Current != screen) navigator.TeleportTo(screen, Facing.East);
            body.position = at;
            body.linearVelocity = Vector2.zero;
            navigator.transform.position = new Vector3(at.x, at.y, navigator.transform.position.z);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        /// <summary>Walks the real player with one key at a time to a point straight along x or y from it (world).</summary>
        private IEnumerator WalkTo(Vector2 target, float timeout = 4f)
        {
            Vector2 d = target - body.position;
            Facing toward = Mathf.Abs(d.x) >= Mathf.Abs(d.y)
                ? (d.x > 0f ? Facing.East : Facing.West)
                : (d.y > 0f ? Facing.North : Facing.South);
            Vector2 axis = toward.ToVector();
            Press(KeyFor(keyboard, toward), queueEventOnly: true);
            yield return WaitUntilOrTimeout(() => Vector2.Dot(target - body.position, axis) < 0.1f, timeout);
            Release(KeyFor(keyboard, toward), queueEventOnly: true);
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        /// <summary>The islands along whole (or, with gap, any) planks from one island to another, both included; null when none.</summary>
        private static List<int> Route(IslandLayout layout, int from, int to, bool gap)
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
                foreach (IslandBridge b in layout.Bridges)
                    if (b.Joins(at) && (gap || !b.Broken) && !previous.ContainsKey(b.Other(at)))
                    {
                        previous[b.Other(at)] = at;
                        queue.Enqueue(b.Other(at));
                    }
            }
            return null;
        }

        /// <summary>Walks a route of islands, island middle to island middle along the lattice lines (world).</summary>
        private IEnumerator WalkRoute(Vector2 centre, IslandLayout layout, List<int> route)
        {
            for (int k = 1; k < route.Count; k++)
            {
                Vector2 to = centre + IslandLayout.SpotCentre(layout.Spots[route[k]]);
                yield return WalkTo(to);
                Assert.AreEqual(route[k], layout.IslandAt(body.position - centre), $"Walked onto island {route[k]} (at {body.position - centre})");
            }
        }

        [UnityTest]
        public IEnumerator Reveal_BuildsGapGatesAndIslandScreens_AsPlanned_OneGapOffFace_TheTrial_AndShapePeopleOffTheIslands()
        {
            yield return Load();
            yield return StartRunOntoMath(1);
            MathPlan plan = math.Plan;
            FaceId face = world.Model.FaceOf(Theme.Math);
            foreach (MathGateSpec spec in plan.Gates)
            {
                Gate gate = world.ModuleAt(spec.Screen).Gates[spec.Slot].GetComponentInChildren<Gate>(true);
                Assert.IsNotNull(gate, $"{spec}");
                CollectionAssert.Contains(world.Gates, gate);
                Assert.IsInstanceOf<GapGate>(gate, $"{spec}");
                Assert.IsFalse(gate.IsOpen);
            }
            Assert.AreEqual(plan.Gates.Count, math.Gaps.Count, "Every gap gate is built");
            Assert.IsTrue(plan.Gates.Any(g => g.Screen.Face != face), "A gap gate on another face");
            Assert.IsTrue(plan.Gates.Any(g => g.Screen.Face == face), "A gap gate on Math");

            Assert.AreEqual(CubeLayout.IslandScreensPerFace(world.FaceSize), math.IslandScreens.Count, "Every island screen is built");
            foreach (IslandScreen screen in math.IslandScreens)
            {
                IslandScreenSpec spec = plan.IslandScreens.First(s => world.ScreenCenter(s.Screen) == screen.Centre);
                Assert.AreSame(spec.Layout, screen.Layout);
                Assert.IsTrue(world.ModuleAt(spec.Screen).IsIslandScreen);
                Assert.IsNotEmpty(screen.Water);
                Assert.AreEqual(spec.Layout.Bridges.Count(b => b.Broken), screen.Gaps.Count);
                // Every doorway lands on land: no water where an arriving player stands.
                foreach (Facing side in new[] { Facing.North, Facing.East, Facing.South, Facing.West })
                {
                    Vector2 arrival = screen.Centre + IslandLayout.Arrival(side);
                    Assert.IsFalse(screen.Water.Any(w => w.OverlapPoint(arrival)), $"{spec}: the {side} doorway lands in the water");
                }
            }
            // A teleport onto an island screen (a new run, the F6 jump) lands on the island of the doorway behind the
            // arrival facing, never in the water.
            foreach (IslandScreen screen in math.IslandScreens)
            {
                ScreenAddress at = plan.IslandScreens.First(s => s.Layout == screen.Layout).Screen;
                foreach (Facing facing in new[] { Facing.North, Facing.East, Facing.South, Facing.West })
                {
                    navigator.TeleportTo(at, facing);
                    yield return new WaitForFixedUpdate();
                    Vector2 local = body.position - screen.Centre;
                    Assert.AreEqual(screen.Layout.DoorIsland(facing.Opposite()), screen.Layout.IslandAt(local),
                        $"{at} facing {facing}: landed at {local}");
                }
            }
            var debug = Object.FindAnyObjectByType<CubeDebug>();
            ScreenAddress before = plan.IslandScreens[0].Screen;
            ScreenAddress previous = world.Model.AllScreens().TakeWhile(s => s != before).Last();
            navigator.TeleportTo(previous, Facing.East);
            Assert.AreEqual(before, debug.JumpToNextUnsealed(), "F6 jumps onto the island screen");
            yield return new WaitForFixedUpdate();
            Assert.GreaterOrEqual(plan.IslandScreens[0].Layout.IslandAt(body.position - world.ScreenCenter(before)), 0, "F6 lands on an island");

            Assert.IsNotNull(math.Trial, "The trial is built");
            Assert.AreEqual(plan.TrialScreen, new ScreenAddress(face, world.CellAt(face, math.Trial.transform.position)));
            Assert.IsFalse(world.Model.IsSealed(face));

            Assert.AreEqual(MathPopulation.ShapeCount(world.FaceSize), math.Shapes.Count(s => s != null));
            foreach (NpcTalker shape in math.Shapes)
            {
                Assert.AreEqual(Race.Shape, shape.Race);
                var screen = new ScreenAddress(face, world.CellAt(face, shape.transform.position));
                Assert.IsFalse(plan.IsIslandScreen(screen), $"{shape.name} spawned on an island screen");
            }
            Assert.AreEqual(Dialogue.IslandLine, math.Shapes[MathPopulation.IslandTeller].Lines[0]);
            Assert.AreEqual(Dialogue.GapCrosserLine, math.Shapes[MathPopulation.GapTeller].Lines[0]);
        }

        [UnityTest]
        public IEnumerator AnIslandScreen_IsWalkedFromADoorwayToTheDoorwayItsPlanksJoin_AndTheGapCrosserSpansTheWayToTheTrial()
        {
            yield return Load();
            yield return StartRunOntoMath(2);
            IslandScreenSpec spec = math.Plan.IslandScreens.First(s => s.HasTrial);
            IslandLayout layout = spec.Layout;
            ScreenAddress screenAt = spec.Screen;
            IslandScreen screen = math.IslandScreens.First(s => s.Layout == layout);
            Vector2 centre = world.ScreenCenter(screenAt);
            var sides = new[] { Facing.North, Facing.East, Facing.South, Facing.West };
            int[] whole = layout.Components(false);
            Facing from = sides.First(a => sides.Any(b => b != a && whole[layout.DoorIsland(a)] == whole[layout.DoorIsland(b)]));
            Facing to = sides.First(b => b != from && whole[layout.DoorIsland(from)] == whole[layout.DoorIsland(b)]);

            // In by one doorway, over the whole planks, out by the other.
            yield return PutPlayerAt(screenAt, centre + IslandLayout.Arrival(from));
            Assert.AreEqual(layout.DoorIsland(from), layout.IslandAt(body.position - centre), "Arrived on the doorway's island");
            yield return WalkTo(centre + IslandLayout.SpotCentre(layout.Spots[layout.DoorIsland(from)]));
            yield return WalkRoute(centre, layout, Route(layout, layout.DoorIsland(from), layout.DoorIsland(to), false));
            Assert.IsTrue(world.Model.TryStep(screenAt, to, out ScreenAddress next, out _));
            Vector2 outside = centre + Vector2.Scale(to.ToVector(), CubeWorld.ScreenSize / 2f) + (Vector2)to.ToVector() * 1.5f * K;
            yield return WalkTo(outside);
            Assert.AreEqual(next, navigator.Current, $"Walked out of {screenAt} by its {to} doorway");

            // The water is solid: from the doorway's island, walking into the water stops at the shore.
            yield return PutPlayerAt(screenAt, centre + IslandLayout.SpotCentre(layout.Spots[layout.DoorIsland(from)]));
            int door = layout.DoorIsland(from);
            Facing across = sides.First(d => d != from && !layout.Bridges.Any(b => b.Joins(door) &&
                layout.Spots[b.Other(door)] == layout.Spots[door] + d.ToVector()));
            yield return WalkTo(body.position + (Vector2)across.ToVector() * 3f * K, 1f);
            Assert.AreEqual(door, layout.IslandAt(body.position - centre), $"The water stopped the player at the shore ({across})");

            // The trial: over the joined islands to the one its broken span leaves from, the gap-crosser* swung at the gap,
            // then onto the trial island. It pays once.
            IslandBridge span = layout.Bridges.First(b => b.Broken && b.Joins(layout.TrialIsland));
            int near = span.Other(layout.TrialIsland);
            BrokenSpan gap = screen.Gaps.First(g => layout.Bridges[g.Bridge].Joins(layout.TrialIsland));
            yield return PutPlayerAt(screenAt, centre + IslandLayout.SpotCentre(layout.Spots[layout.DoorIsland(from)]));
            yield return WalkRoute(centre, layout, Route(layout, layout.DoorIsland(from), near, false));
            Vector2 toward = (layout.Islands[layout.TrialIsland].center - layout.Islands[near].center).normalized;
            Facing facing = sides.First(f => Vector2.Dot(f.ToVector(), toward) > 0.9f);
            // Without the gap-crosser* the gap stops the walk.
            yield return WalkTo(centre + IslandLayout.SpotCentre(layout.Spots[layout.TrialIsland]), 1.5f);
            Assert.AreNotEqual(layout.TrialIsland, layout.IslandAt(body.position - centre), "The gap stops the player");
            Assert.IsFalse(math.Trial.IsComplete);
            // The swing, with the gap-crosser* in hand, facing the gap from just before it.
            ItemDefinition crosser = world.ItemFor(Theme.Math);
            if (!inventory.Has(crosser)) inventory.Add(crosser);
            navigator.GetComponent<Equipment>().Equip(crosser);
            yield return new WaitForSeconds(attack.Cooldown + 0.02f);
            yield return PutPlayerAt(screenAt, (Vector2)gap.transform.position - toward * (IslandLayout.GapLength / 2f + 0.45f * K));
            navigator.GetComponent<Game.Tracer.PlayerMover>().SetFacing(facing.ToVector());
            attack.TryAttack();
            yield return null;
            Assert.IsTrue(gap.IsMended, "The gap-crosser* spans the gap");
            int paid = 0;
            math.Trial.Completed += _ => paid++;
            yield return WalkTo(centre + IslandLayout.SpotCentre(layout.Spots[layout.TrialIsland]));
            Assert.AreEqual(layout.TrialIsland, layout.IslandAt(body.position - centre), "Over the mended span onto the trial island");
            Assert.IsTrue(math.Trial.IsComplete);
            yield return WalkTo(centre + IslandLayout.SpotCentre(layout.Spots[near]));
            yield return WalkTo(centre + IslandLayout.SpotCentre(layout.Spots[layout.TrialIsland]));
            Assert.AreEqual(1, paid, "The trial fires once");
            navigator.GetComponent<Equipment>().Equip(null);
        }

        [UnityTest]
        public IEnumerator TheGapCrosser_OpensEveryGapGate_TheOffFaceOneIncluded()
        {
            yield return Load();
            yield return StartRunOntoMath(2);
            MathPlan plan = math.Plan;
            ItemDefinition crosser = world.ItemFor(Theme.Math);
            Assert.AreEqual(GapCrosserItem.Id, crosser.Id);
            if (!inventory.Has(crosser)) inventory.Add(crosser);
            navigator.GetComponent<Equipment>().Equip(crosser);
            foreach (MathGateSpec spec in plan.Gates)
            {
                GateSlot slot = world.ModuleAt(spec.Screen).Gates[spec.Slot];
                var gap = slot.GetComponentInChildren<GapGate>(true);
                Facing toward = slot.Opening.Opposite();
                if (navigator.Current != spec.Screen) navigator.TeleportTo(spec.Screen, Facing.East);
                yield return this.TapThroughPhysics(KeyFor(keyboard, toward));
                yield return new WaitForSeconds(attack.Cooldown + 0.02f);
                yield return PutPlayerAt(spec.Screen, (Vector2)slot.transform.position - (Vector2)toward.ToVector() * 1.0f * K);
                Assert.GreaterOrEqual(attack.TryAttack(), 0, "The swing happened");
                yield return null;
                Assert.IsTrue(gap.IsOpen, $"{spec}: the gap-crosser* lays a crossing");
                Assert.IsNotNull(gap.Crossing);
            }
            Assert.IsTrue(plan.Gates.Any(g => !g.OnHome), "The off-face gap gate was among them");
        }
    }
}
