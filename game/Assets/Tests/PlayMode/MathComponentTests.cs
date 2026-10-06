using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The Math mechanics on objects built in the test (far from anything a scene may hold), one test per I/O row: a
    /// bridge door opens (with the gate-open effect) when its layout is walked crossing every bridge exactly once, and not
    /// before; crossing a bridge twice resets every bridge with visible feedback (a red flash and a popup); the layout
    /// follows the real player by itself; a gap gate opens when the gap-crosser* is swung at it (a crossing is laid), and
    /// other items, bare swings and touches do nothing; the trial fires once; Math enemies take markedly more damage from
    /// their weakness.
    /// </summary>
    public class MathComponentTests : InputTestFixture
    {
        private static readonly Vector2 Origin = new Vector2(-9000f, 7000f);

        private readonly List<Object> created = new List<Object>();
        private ItemDefinition crosser, other;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            crosser = Track(GapCrosserItem.CreateItem());
            other = Track(ItemDefinition.Create("other", "Other", Theme.Physics, Color.cyan));
        }

        public override void TearDown()
        {
            foreach (Object o in created)
                if (o != null) Object.Destroy(o);
            created.Clear();
            base.TearDown();
        }

        private T Track<T>(T o) where T : Object
        {
            created.Add(o);
            return o;
        }

        private static BridgeLayout Layout(ulong seed, int bridges = 6) =>
            BridgeLayout.Generate(MathPlan.DoorCols, MathPlan.DoorRows, bridges, new SeededRng(seed, MathPlan.RngStream))
                .Translated(new Vector2(0f, 3f));

        private GameObject Player(Vector2 position)
        {
            var go = Track(new GameObject("Test Player"));
            go.transform.position = position;
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            go.AddComponent<CircleCollider2D>().radius = 0.4f;
            go.AddComponent<Game.Tracer.PlayerMover>();
            go.AddComponent<Inventory>();
            go.AddComponent<Health>();
            go.AddComponent<PlayerStats>();
            go.AddComponent<Equipment>();
            go.AddComponent<PlayerAttack>().Cooldown = 0f;
            return go;
        }

        private GameObject Block(Vector2 at)
        {
            var go = Track(new GameObject("Block"));
            go.transform.position = at;
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.AddComponent<SpriteRenderer>().sprite = NpcFactory.ShapeSprite(PartShape.Square);
            go.AddComponent<BoxCollider2D>().size = new Vector2(GateSlot.GateWidth, GateSlot.GateThickness);
            return go;
        }

        private static int Swing(GameObject player, ItemDefinition item)
        {
            var attack = player.GetComponent<PlayerAttack>();
            var inventory = player.GetComponent<Inventory>();
            if (item != null && !inventory.Has(item)) inventory.Add(item);
            player.GetComponent<Equipment>().Equip(item);
            attack.TryAttack();
            return attack.LastReceiversHit;
        }

        /// <summary>Points a player passes walking from island a to island b along their bridge (island, plank, island).</summary>
        public static IEnumerable<Vector2> Steps(BridgeLayout layout, int a, int b)
        {
            Vector2 from = layout.Islands[a], to = layout.Islands[b];
            for (int k = 0; k <= 10; k++) yield return Vector2.Lerp(from, to, k / 10f);
        }

        private BridgePuzzle Puzzle(BridgeLayout layout, Transform player = null)
        {
            BridgePuzzle puzzle = BridgePuzzle.Create("Test Bridges", null, layout, Origin, player, Color.red, null);
            Track(puzzle.gameObject);
            return puzzle;
        }

        /// <summary>Walks a layout along islands (world positions fed straight to the puzzle). Returns every result.</summary>
        private static List<BridgeWalk.Result> Walk(BridgePuzzle puzzle, IList<int> islands)
        {
            var results = new List<BridgeWalk.Result> { puzzle.ObserveWorld(puzzle.IslandWorld(islands[0])) };
            for (int k = 0; k + 1 < islands.Count; k++)
                foreach (Vector2 p in Steps(puzzle.Layout, islands[k], islands[k + 1]))
                    results.Add(puzzle.ObserveWorld(puzzle.Centre + p));
            return results;
        }

        [UnityTest]
        public IEnumerator BridgeDoor_OpensWhenEveryBridgeIsCrossedExactlyOnce_WithTheOpenEffect_AndNotBefore()
        {
            GameObject player = Player(Origin + new Vector2(0f, -3f));
            BridgeLayout layout = Layout(5);
            BridgePuzzle puzzle = Puzzle(layout);
            BridgeDoorGate door = BridgeDoorGate.Build(Block(Origin + new Vector2(4f, -1.4f)), crosser, puzzle, 5, null);
            int opened = 0;
            door.Opened += _ => opened++;
            yield return null;

            player.GetComponent<Inventory>().Add(crosser);
            Assert.IsFalse(door.TryOpen(player.GetComponent<Collider2D>()), "Touching it, even with the gap-crosser*, does nothing");
            Assert.IsNotNull(door.transform.Find("Bridge Mark"), "The door's flag mark");

            List<int> path = layout.Solve();
            // All but the last bridge: still shut.
            Walk(puzzle, path.Take(path.Count - 1).ToList());
            Assert.IsFalse(door.IsOpen);
            Assert.AreEqual(layout.BridgeCount - 1, puzzle.Walk.CrossedCount);
            Assert.AreEqual(BridgePuzzle.CrossedColor, puzzle.BridgeRenderers.Where((r, i) => puzzle.Walk.Crossed(i)).First().color,
                "Walked bridges change colour");

            int effects = GateOpenEffect.Played;
            foreach (Vector2 p in Steps(layout, path[path.Count - 2], path[path.Count - 1]))
                puzzle.ObserveWorld(Origin + p);
            Assert.IsTrue(puzzle.IsSolved);
            Assert.IsTrue(door.IsOpen, "Every bridge exactly once opens the door");
            Assert.IsFalse(door.Solid.enabled);
            Assert.AreEqual(1, opened);
            Assert.AreEqual(effects + 1, GateOpenEffect.Played, "The gate-open effect plays");
            Assert.IsTrue(PickupPopup.Active.Any(x => x != null && x.Text == BridgePuzzle.SolvedText));
        }

        [UnityTest]
        public IEnumerator BridgeDoor_CrossingABridgeTwice_ResetsEveryBridge_WithAFlashAndAPopup()
        {
            BridgeLayout layout = Layout(9);
            BridgePuzzle puzzle = Puzzle(layout);
            BridgeDoorGate door = BridgeDoorGate.Build(Block(Origin + new Vector2(4f, -1.4f)), crosser, puzzle, 5, null);
            var reasons = new List<BridgeWalk.Result>();
            puzzle.ResetPuzzle += (_, r) => reasons.Add(r);
            yield return null;

            List<int> path = layout.Solve();
            Walk(puzzle, new[] { path[0], path[1], path[2] });
            Assert.AreEqual(2, puzzle.Walk.CrossedCount);
            // Back over the bridge just crossed.
            foreach (Vector2 p in Steps(layout, path[2], path[1])) puzzle.ObserveWorld(Origin + p);
            CollectionAssert.AreEqual(new[] { BridgeWalk.Result.CrossedTwice }, reasons);
            Assert.AreEqual(1, puzzle.Resets);
            Assert.AreEqual(0, puzzle.Walk.CrossedCount, "Every bridge is reset");
            Assert.IsFalse(door.IsOpen);
            Assert.IsTrue(puzzle.BridgeRenderers.All(r => r.color == BridgePuzzle.ResetColor), "The bridges flash red");
            Assert.IsTrue(PickupPopup.Active.Any(x => x != null && x.Text == BridgePuzzle.CrossedTwiceText), "A popup says why");

            yield return new WaitForSeconds(BridgePuzzle.ResetFlashSeconds + 0.1f);
            Assert.IsTrue(puzzle.BridgeRenderers.All(r => r.color == BridgePuzzle.PlankColor), "The flash ends");

            // Starting again from the flag still works.
            Walk(puzzle, path);
            Assert.IsTrue(door.IsOpen);
        }

        [UnityTest]
        public IEnumerator Puzzle_FollowsTheRealPlayer_FrameByFrame()
        {
            GameObject player = Player(Origin + new Vector2(0f, -3f));
            player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            BridgeLayout layout = Layout(21);
            BridgePuzzle puzzle = Puzzle(layout, player.transform);
            yield return null;
            List<int> path = layout.Solve();
            player.transform.position = puzzle.IslandWorld(path[0]);
            yield return null;
            Assert.IsTrue(puzzle.Walk.IsActive, "Stepping on the flag starts the walk");
            for (int k = 0; k + 1 < path.Count; k++)
                foreach (Vector2 p in Steps(layout, path[k], path[k + 1]))
                {
                    player.transform.position = Origin + p;
                    yield return null;
                }
            Assert.IsTrue(puzzle.IsSolved);
        }

        [UnityTest]
        public IEnumerator GapGate_TheGapCrosserSwungAtIt_LaysACrossingAndOpensIt_OtherItemsOrBareDoNothing()
        {
            GameObject player = Player(Origin);
            GameObject block = Block(Origin + Vector2.down * 1.1f);
            GapGate gate = GapGate.Build(block, crosser, null);
            int opened = 0;
            gate.Opened += _ => opened++;
            yield return null;

            player.GetComponent<Inventory>().Add(crosser);
            Assert.IsFalse(gate.TryOpen(player.GetComponent<Collider2D>()), "Touching it does nothing");
            Assert.AreEqual(0, Swing(player, null), "A bare swing: shut");
            Assert.AreEqual(0, Swing(player, other), "Another item: shut");
            Assert.IsFalse(gate.IsOpen);
            Assert.IsNull(gate.Crossing);

            int effects = GateOpenEffect.Played;
            Assert.AreEqual(1, Swing(player, crosser));
            Assert.IsTrue(gate.IsOpen, "The gap-crosser* lays a crossing over the gap");
            Assert.IsFalse(gate.Solid.enabled);
            Assert.IsNotNull(gate.Crossing);
            Assert.AreEqual(1, opened);
            Assert.AreEqual(effects + 1, GateOpenEffect.Played);
            Assert.IsTrue(player.GetComponent<Inventory>().Has(crosser), "The gap-crosser* is kept");
            Assert.AreEqual(0, Swing(player, crosser), "Already open: nothing more");
        }

        [UnityTest]
        public IEnumerator Trial_SolvingTheBiggerLayout_FiresOnce_AndRepeatsDoNot()
        {
            BridgeLayout layout = BridgeLayout.Generate(MathPlan.TrialCols, MathPlan.TrialRows, MathPlan.TrialBridges,
                new SeededRng(4UL, MathPlan.RngStream)).Translated(new Vector2(0f, 3f));
            var go = Track(new GameObject("Math Trial"));
            var trial = go.AddComponent<MathTrial>();
            trial.Configure(layout, Origin, null, MathPlan.TrialCurrency, null);
            var paid = new List<int>();
            trial.Completed += c => paid.Add(c);
            yield return null;

            List<int> path = layout.Solve();
            Walk(trial.Puzzle, path.Take(path.Count - 1).ToList());
            Assert.IsFalse(trial.IsComplete);
            Walk(trial.Puzzle, path.Skip(path.Count - 2).ToList());
            Assert.IsTrue(trial.IsComplete);
            CollectionAssert.AreEqual(new[] { MathPlan.TrialCurrency }, paid);

            Walk(trial.Puzzle, path);
            Assert.IsFalse(trial.Complete(), "Completing again does nothing");
            Assert.AreEqual(1, paid.Count, "Fires once");
        }

        [UnityTest]
        public IEnumerator Enemies_TakeMarkedlyMoreDamageFromTheirWeakness()
        {
            var mitt = Track(MassMittItem.CreateItem());
            foreach (MathEnemyKind kind in new[] { MathEnemyKind.DeadEnd, MathEnemyKind.LooseEnd })
            {
                ItemDefinition weakness = kind == MathEnemyKind.DeadEnd ? crosser : mitt;
                ItemDefinition notWeakness = kind == MathEnemyKind.DeadEnd ? other : crosser;
                Enemy weak = MathEnemies.Spawn(kind, weakness, Origin + new Vector2(50f, (int)kind * 5f), new Rect(Origin, Vector2.one * 100f),
                    null, null, null);
                Enemy plain = MathEnemies.Spawn(kind, weakness, Origin + new Vector2(60f, (int)kind * 5f), new Rect(Origin, Vector2.one * 100f),
                    null, null, null);
                Track(weak.gameObject);
                Track(plain.gameObject);
                weak.DestroyOnDeath = false;
                plain.DestroyOnDeath = false;
                Assert.AreEqual(kind, weak.GetComponent<MathEnemy>().Kind);
                Assert.AreSame(weakness, weak.Weakness);
                StringAssert.Contains(MathEnemies.Name(kind), weak.name);
                yield return null;

                float byWeakness = weak.Health.TakeDamage(1f, weakness);
                float byOther = plain.Health.TakeDamage(1f, notWeakness);
                Assert.Greater(byWeakness, byOther * 2.5f, $"{kind}: the weakness hits markedly harder");
            }
        }
    }
}
