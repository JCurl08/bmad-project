using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Game.Cube.Tests.TestInput;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The Bridgekeeper*'s face in the real Cube scene. The Math face is unsealed and revealed with the rest; the reveal
    /// builds the planned gates (bridge doors with their island-and-bridge layouts on their screens, gap gates on Math and
    /// at least one on another face) and the trial. Walking a door's layout with the real player (every bridge exactly
    /// once) opens it; swinging the gap-crosser* at every gap gate (the off-face one included) opens it; walking the trial
    /// completes it once. Shape people are present (human-like parts), explain the doors and the gap-crosser*, and stand
    /// clear of the layouts.
    /// </summary>
    public class MathSceneTests : InputTestFixture
    {
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
            navigator.TeleportTo(new ScreenAddress(world.Model.FaceOf(Theme.Math), 0, 0), Facing.East);
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
            yield return null;
        }

        /// <summary>Walks a puzzle's solution with the real player, a short step per frame.</summary>
        private IEnumerator WalkSolution(ScreenAddress screen, BridgePuzzle puzzle)
        {
            List<int> path = puzzle.Layout.Solve();
            Assert.IsNotNull(path);
            yield return PutPlayerAt(screen, puzzle.IslandWorld(path[0]));
            for (int k = 0; k + 1 < path.Count; k++)
                for (int s = 1; s <= 8; s++)
                    yield return PutPlayerAt(screen, Vector2.Lerp(puzzle.IslandWorld(path[k]), puzzle.IslandWorld(path[k + 1]), s / 8f));
            body.position = world.ScreenCenter(screen);
        }

        [UnityTest]
        public IEnumerator Reveal_BuildsBridgeDoorsAndGapGates_AsPlanned_OneGapOffFace_TheTrial_AndShapePeople()
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
                if (spec.Kind == MathGateKind.Gap) Assert.IsInstanceOf<GapGate>(gate, $"{spec}");
                else
                {
                    var door = (BridgeDoorGate)gate;
                    Assert.IsNotNull(door.Puzzle, $"{spec}: its layout");
                    Assert.AreSame(spec.Layout, door.Puzzle.Layout);
                    Assert.AreEqual(world.ScreenCenter(spec.Screen), door.Puzzle.Centre);
                    Assert.AreEqual(spec.Bridges, door.Puzzle.BridgeRenderers.Count);
                }
                Assert.IsFalse(gate.IsOpen);
            }
            Assert.AreEqual(plan.Gates.Count(g => g.Kind == MathGateKind.Gap), math.Gaps.Count, "Every gap gate is built");
            Assert.IsTrue(plan.Gates.Any(g => g.Kind == MathGateKind.Gap && g.Screen.Face != face), "A gap gate on another face");
            Assert.IsTrue(plan.Gates.Any(g => g.Kind == MathGateKind.Gap && g.Screen.Face == face), "A gap gate on Math");
            Assert.IsNotEmpty(math.Doors);
            Assert.IsNotNull(math.Trial, "The trial is built");
            Assert.AreEqual(plan.TrialLayout, math.Trial.Puzzle.Layout);
            Assert.IsFalse(world.Model.IsSealed(face));

            Assert.AreEqual(MathPopulation.ShapeCount(world.FaceSize), math.Shapes.Count(s => s != null));
            foreach (NpcTalker shape in math.Shapes)
            {
                Assert.AreEqual(Race.Shape, shape.Race);
                ScreenAddress screen = new ScreenAddress(face, world.CellAt(face, shape.transform.position));
                Assert.IsFalse(plan.NearLayout(screen, (Vector2)shape.transform.position - world.ScreenCenter(screen), MathFace.LayoutKeepOut - 0.3f),
                    $"{shape.name} spawned on a layout");
            }
            Assert.AreEqual(Dialogue.BridgeLine, math.Shapes[MathPopulation.BridgeTeller].Lines[0]);
            Assert.AreEqual(Dialogue.GapCrosserLine, math.Shapes[MathPopulation.GapTeller].Lines[0]);
        }

        [UnityTest]
        public IEnumerator WalkingADoorsLayout_OpensIt_TheGapCrosserOpensEveryGapGate_AndTheTrialCompletesOnce()
        {
            yield return Load();
            yield return StartRunOntoMath(2);
            MathPlan plan = math.Plan;

            // Every bridge door, by walking its layout with the real player.
            foreach (BridgeDoorGate door in math.Doors)
            {
                MathGateSpec spec = plan.Gates.First(g => g.Layout == door.Puzzle.Layout);
                Assert.IsFalse(door.IsOpen);
                yield return WalkSolution(spec.Screen, door.Puzzle);
                Assert.IsTrue(door.Puzzle.IsSolved, $"{spec}: walked");
                Assert.IsTrue(door.IsOpen, $"{spec}: the door opens");
            }

            // Every gap gate (the off-face one included), by swinging the gap-crosser* at it.
            ItemDefinition crosser = world.ItemFor(Theme.Math);
            Assert.AreEqual(GapCrosserItem.Id, crosser.Id);
            inventory.Add(crosser);
            navigator.GetComponent<Equipment>().Equip(crosser);
            foreach (MathGateSpec spec in plan.Gates.Where(g => g.Kind == MathGateKind.Gap))
            {
                GateSlot slot = world.ModuleAt(spec.Screen).Gates[spec.Slot];
                var gap = slot.GetComponentInChildren<GapGate>(true);
                Facing toward = slot.Opening.Opposite();
                if (navigator.Current != spec.Screen) navigator.TeleportTo(spec.Screen, Facing.East);
                yield return this.TapThroughPhysics(KeyFor(keyboard, toward));
                yield return new WaitForSeconds(attack.Cooldown + 0.02f);
                yield return PutPlayerAt(spec.Screen, (Vector2)slot.transform.position - (Vector2)toward.ToVector() * 1.0f);
                Assert.GreaterOrEqual(attack.TryAttack(), 0, "The swing happened");
                yield return null;
                Assert.IsTrue(gap.IsOpen, $"{spec}: the gap-crosser* lays a crossing");
                Assert.IsNotNull(gap.Crossing);
            }

            // The trial: walked once, it pays once.
            int paid = 0;
            math.Trial.Completed += _ => paid++;
            yield return WalkSolution(plan.TrialScreen, math.Trial.Puzzle);
            Assert.IsTrue(math.Trial.IsComplete);
            yield return WalkSolution(plan.TrialScreen, math.Trial.Puzzle);
            Assert.AreEqual(1, paid, "The trial fires once");
        }

        [UnityTest]
        public IEnumerator WalkingThroughADoorScreen_AlongEitherExitLane_NeverTouchesItsBridges()
        {
            yield return Load();
            yield return StartRunOntoMath(1);
            Assert.IsNotEmpty(math.Doors);
            MathGateSpec spec = math.Plan.Gates.First(g => g.Layout != null);
            ScreenAddress screen = spec.Screen;
            var puzzles = math.Doors.Select(d => d.Puzzle).Where(p => p.Centre == world.ScreenCenter(screen)).ToList();
            if (math.Trial != null && math.Plan.TrialScreen == screen) puzzles.Add(math.Trial.Puzzle);
            Assert.IsNotEmpty(puzzles);
            int popups = PickupPopup.Active.Count(x => x != null &&
                (x.Text == BridgePuzzle.CrossedTwiceText || x.Text == BridgePuzzle.OffTrailText));
            Vector2 centre = world.ScreenCenter(screen);

            // North to south down the middle, then west to east across it, as a player passing through would.
            var route = new List<Vector2>();
            for (float y = 4.6f; y >= -4.6f; y -= 0.25f) route.Add(centre + new Vector2(0f, y));
            for (float x = -7.6f; x <= 7.6f; x += 0.25f) route.Add(centre + new Vector2(x, 0f));
            foreach (Vector2 at in route)
            {
                yield return PutPlayerAt(screen, at);
                foreach (BridgePuzzle p in puzzles)
                {
                    Assert.IsFalse(p.Walk.IsActive, $"Passing at {at - centre} started a walk");
                    Assert.AreEqual(0, p.Walk.CrossedCount);
                    Assert.AreEqual(0, p.Resets, $"Passing at {at - centre} reset a layout");
                }
            }
            Assert.AreEqual(popups, PickupPopup.Active.Count(x => x != null &&
                (x.Text == BridgePuzzle.CrossedTwiceText || x.Text == BridgePuzzle.OffTrailText)), "No reset feedback");
            Assert.IsTrue(puzzles.All(p => p.BridgeRenderers.All(r => r.color == BridgePuzzle.PlankColor)), "No bridge changed");
        }
    }
}
