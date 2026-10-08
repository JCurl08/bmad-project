using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Game.Tracer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The Math mechanics on objects built in the test (far from anything a scene may hold), one test per I/O row: an
    /// island screen's water is solid, its islands and planks carry the player, and a broken span stops it until the
    /// gap-crosser* is swung at the gap (other items and bare swings do nothing), after which it walks over; a gap gate
    /// opens when the gap-crosser* is swung at it (a crossing is laid), and other items, bare swings and touches do
    /// nothing; stepping onto the trial island fires the trial once; Math enemies take markedly more damage from their
    /// weakness; (4.12) crossing a network plank lights it, crossing a lit one again snuffs every plank with a red flash, a
    /// mended span shows gold and lights nothing, an island whose planks are all lit glows round its rim, and leaving the
    /// screen snuffs an unsolved network; and (1.26) a lone doorway's island (hand-made: generated layouts have none since
    /// 4.12) has a lookout sign and says go round.
    /// </summary>
    public class MathComponentTests : InputTestFixture
    {
        /// <summary>Lengths and speeds below are in the first 16x10 screens' units, scaled like the game's (4.1).</summary>
        private const float K = ScreenMath.ContentScale;

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

        private GameObject Player(Vector2 position)
        {
            var go = Track(new GameObject("Test Player"));
            go.transform.position = position;
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            go.AddComponent<CircleCollider2D>().radius = 0.4f * K;
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

        private static readonly Vector2Int N = IslandLayout.DoorSpot(Facing.North), E = IslandLayout.DoorSpot(Facing.East),
            S = IslandLayout.DoorSpot(Facing.South), W = IslandLayout.DoorSpot(Facing.West), C = new Vector2Int(2, 1),
            EastOfC = new Vector2Int(3, 1), WestOfC = new Vector2Int(1, 1), SouthWest = new Vector2Int(0, 0);

        /// <summary>
        /// N, C and S joined; W joins S by way of the south-west island (a long plank under the empty spot between); E reaches
        /// C over a broken span (via the island east of C); maybe a trial west of C. With puzzle (4.12) C is the reward island,
        /// so its whole-plank component is the lit network: N-C, C-S, S-SW and SW-W (a trail from N or W).
        /// </summary>
        private static IslandLayout Layout(bool trial, bool puzzle = false)
        {
            var spots = new List<Vector2Int> { N, E, S, W, C, EastOfC, SouthWest };
            var links = new List<(int, int, bool)> { (0, 4, false), (4, 2, false), (1, 5, false), (5, 4, true), (2, 6, false), (6, 3, false) };
            if (trial)
            {
                spots.Add(WestOfC);
                links.Add((4, 7, true));
            }
            return new IslandLayout(spots, links, new[] { 0, 1, 2, 3 }, trial ? 7 : -1, puzzle ? 4 : -1);
        }

        /// <summary>1.25's shape with a lone west doorway (no plank): only hand-made since 4.12.</summary>
        private static IslandLayout LoneLayout() =>
            new IslandLayout(new[] { N, E, S, W, C, EastOfC }, new[] { (0, 4, false), (4, 2, false), (1, 5, false), (5, 4, true) }, new[] { 0, 1, 2, 3 });

        /// <summary>Drives the player's body one way for a while (the mover is off, so nothing else steers it), then stops it.</summary>
        private static IEnumerator Push(Rigidbody2D body, Vector2 direction, float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                body.linearVelocity = direction * 6f * K;
                yield return new WaitForFixedUpdate();
            }
            body.linearVelocity = Vector2.zero;
            yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator IslandScreen_WaterIsSolid_PlanksCarryThePlayer_AndABrokenSpanBlocksUntilTheGapCrosserMendsIt()
        {
            IslandLayout layout = Layout(false);
            Assert.IsEmpty(layout.Problems("test", _ => true));
            IslandScreen screen = Track(IslandScreen.Create("Test Islands", null, layout, Origin, null).gameObject).GetComponent<IslandScreen>();
            Assert.AreEqual(1, screen.Gaps.Count);
            Assert.IsTrue(screen.Water.All(w => !w.isTrigger && w.enabled), "The water is solid");
            GameObject player = Player(Origin + IslandLayout.Arrival(Facing.South));
            player.GetComponent<Game.Tracer.PlayerMover>().enabled = false;
            var body = player.GetComponent<Rigidbody2D>();
            yield return new WaitForFixedUpdate();

            // South doorway's island, up the planks to the north doorway's island.
            yield return Push(body, Vector2.up, 1.2f); // 1.2 s since 4.1: the half-tile walls put the south arrival further out
            Assert.AreEqual(0, layout.IslandAt(body.position - Origin), $"Walked the planks up to the north island (at {body.position - Origin})");

            // Back to the middle island, then west into the water: the shore stops it.
            yield return Push(body, Vector2.down, 0.45f);
            Assert.AreEqual(4, layout.IslandAt(body.position - Origin), $"On the middle island (at {body.position - Origin})");
            Vector2 middle = body.position;
            yield return Push(body, Vector2.left, 1f);
            Assert.Greater(body.position.x - Origin.x, layout.Islands[4].xMin - 0.05f * K, "The water stops the player at the shore");
            yield return Push(body, Vector2.right, (middle.x - body.position.x) / (6f * K));

            // East over the broken span: the gap stops it.
            yield return Push(body, Vector2.right, 1f);
            BrokenSpan gap = screen.Gaps[0];
            Assert.Less(body.position.x, gap.transform.position.x - layout.Bridges[gap.Bridge].Gap.width / 2f + 0.01f * K, "The gap stops the player");
            Assert.IsFalse(gap.IsMended);

            // Swings: bare and another item do nothing; the gap-crosser* lays the plank.
            player.GetComponent<Game.Tracer.PlayerMover>().SetFacing(Vector2.right);
            Assert.AreEqual(0, Swing(player, null), "A bare swing");
            Assert.AreEqual(0, Swing(player, other), "Another item");
            Assert.IsFalse(gap.IsMended);
            int mended = 0;
            screen.Mended += (_, __) => mended++;
            Assert.AreEqual(1, Swing(player, crosser), "The gap-crosser* reaches the gap");
            Assert.IsTrue(gap.IsMended);
            Assert.IsFalse(gap.Solid.enabled, "The gap stops blocking");
            Assert.AreEqual(1, mended);
            Assert.AreEqual(0, Swing(player, crosser), "Mended: nothing more");

            // Now it walks over to the east doorway's island.
            yield return Push(body, Vector2.right, 0.92f);
            Assert.AreEqual(1, layout.IslandAt(body.position - Origin), $"Over the mended span to the east island (at {body.position - Origin})");
        }

        [UnityTest]
        public IEnumerator IslandScreen_ACrossedPlankLights_ALitOneCrossedAgainSnuffsAllWithARedFlash_ASpanShowsGold_AndLeavingResets()
        {
            IslandLayout layout = Layout(false, true);
            Assert.AreEqual(4, layout.NetworkPlanks.Count, "N-C, C-S, S-SW and SW-W light; the span and nothing else");
            IslandScreen screen = Track(IslandScreen.Create("Test Islands", null, layout, Origin, null).gameObject).GetComponent<IslandScreen>();
            Assert.IsFalse(screen.GetComponentsInChildren<SpriteRenderer>().Any(r => r.name.StartsWith("Island")), "No island pads");
            Assert.AreEqual(layout.Islands.Count, screen.transform.Cast<Transform>().Count(t => t.name.StartsWith("Shore ")), "A shoreline per island");
            Assert.IsEmpty(screen.Signs, "No lone doorway, no sign");

            GameObject player = Player(Origin + IslandLayout.Arrival(Facing.South));
            player.GetComponent<Game.Tracer.PlayerMover>().enabled = false;
            var body = player.GetComponent<Rigidbody2D>();
            screen.Player = player.transform;
            var events = new List<(int Plank, CrossResult Result)>();
            screen.Crossed += (_, k, r) => events.Add((k, r));
            for (int k = 0; k < layout.Bridges.Count; k++)
            {
                Assert.AreEqual(PlankLight.Unlit, screen.Lights[k]);
                Assert.IsFalse(screen.Glow(k).enabled, $"Plank {k} starts dark");
            }
            yield return new WaitForFixedUpdate();
            yield return null;

            // South doorway's island, up over C to the north one: the two planks crossed light up, Math's yellow.
            yield return Push(body, Vector2.up, 1.2f);
            yield return null;
            Assert.AreEqual(0, layout.IslandAt(body.position - Origin));
            int sc = layout.Bridges.ToList().FindIndex(b => b.Joins(2) && b.Joins(4)), cn = layout.Bridges.ToList().FindIndex(b => b.Joins(0) && b.Joins(4));
            CollectionAssert.AreEqual(new[] { (sc, CrossResult.Lit), (cn, CrossResult.Lit) }, events, "S-C then C-N lit");
            foreach (int k in new[] { sc, cn })
            {
                Assert.AreEqual(PlankLight.Lit, screen.Lights[k]);
                Assert.IsTrue(screen.Glow(k).enabled);
                Assert.AreEqual(IslandScreen.LitGlow, screen.Glow(k).color);
            }
            Assert.AreEqual(2, screen.Lights.LitCount);
            Assert.IsTrue(screen.Rim(layout.DoorIsland(Facing.North)).activeSelf, "The north island's only plank is lit: its rim glows");
            Assert.IsTrue(screen.Rim(4).activeSelf, "C's planks (N and S) are all lit: its rim glows");
            Assert.IsFalse(screen.Rim(layout.DoorIsland(Facing.South)).activeSelf, "The south island still has a dark plank (to the south-west)");

            // Back down over C-N: a lit plank crossed again snuffs every plank with a red flash.
            yield return Push(body, Vector2.down, 0.45f);
            yield return null;
            Assert.AreEqual(4, layout.IslandAt(body.position - Origin), $"Back on C (at {body.position - Origin})");
            Assert.AreEqual((cn, CrossResult.Snuffed), events.Last());
            Assert.AreEqual(0, screen.Lights.LitCount);
            Assert.IsTrue(screen.Flashing);
            foreach (int k in layout.NetworkPlanks) Assert.AreEqual(IslandScreen.SnuffGlow, screen.Glow(k).color, $"plank {k} flashes red");
            Assert.IsFalse(screen.Rim(4).activeSelf);
            yield return new WaitForSeconds(IslandScreen.FlashSeconds + 0.1f);
            foreach (int k in layout.NetworkPlanks) Assert.IsFalse(screen.Glow(k).enabled, $"plank {k} dark after the flash");

            // A mended span shows the gap-crosser*'s gold and never lights or snuffs.
            int broken = Enumerable.Range(0, layout.Bridges.Count).First(k => layout.Bridges[k].Broken);
            Assert.IsFalse(layout.IsNetworkPlank(broken));
            Assert.IsTrue(screen.Mend(broken));
            Assert.AreEqual(PlankLight.Mended, screen.Lights[broken]);
            Assert.AreEqual(IslandScreen.MendedGlow, screen.Glow(broken).color);
            Assert.AreEqual(CrossResult.None, screen.Cross(broken), "Crossing the mended span changes nothing");
            Assert.AreNotEqual(IslandScreen.LitGlow, IslandScreen.MendedGlow);

            // Down to the south island again (C-S lights), then off the screen: the unsolved network goes dark.
            yield return Push(body, Vector2.down, 0.6f);
            yield return null;
            Assert.AreEqual(1, screen.Lights.LitCount, $"C-S lit (at {body.position - Origin})");
            body.position = Origin + new Vector2(0f, -ScreenEdges.Half.y - 2f * K);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(0, screen.Lights.LitCount, "Leaving the screen snuffs an unsolved network");
            Assert.IsFalse(screen.Flashing, "No flash for leaving");
        }

        [UnityTest]
        public IEnumerator IslandScreen_ALoneDoorway_HasItsLookoutSign_AndSaysGoRound()
        {
            IslandLayout layout = LoneLayout();
            IslandScreen screen = Track(IslandScreen.Create("Test Islands", null, layout, Origin, null).gameObject).GetComponent<IslandScreen>();
            Assert.AreEqual(1, screen.Signs.Count);
            Assert.AreEqual(layout.DoorIsland(Facing.West), layout.IslandAt((Vector2)screen.Signs[0].transform.position - Origin));
            GameObject player = Player(Origin + IslandLayout.Arrival(Facing.South));
            player.GetComponent<Game.Tracer.PlayerMover>().enabled = false;
            var body = player.GetComponent<Rigidbody2D>();
            screen.Player = player.transform;
            int lone = PickupPopup.Active.Count(p => p != null && p.Text == IslandScreen.LoneText);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(lone, PickupPopup.Active.Count(p => p != null && p.Text == IslandScreen.LoneText), "No 'go round' on joined islands");
            body.position = Origin + IslandLayout.Arrival(Facing.West);
            yield return new WaitForFixedUpdate();
            yield return null;
            yield return null;
            Assert.AreEqual(lone + 1, PickupPopup.Active.Count(p => p != null && p.Text == IslandScreen.LoneText), "The lone doorway says go round");
            yield return null;
            Assert.AreEqual(lone + 1, PickupPopup.Active.Count(p => p != null && p.Text == IslandScreen.LoneText), "Only once while it stays");
            foreach (PickupPopup p in PickupPopup.Active.ToList())
                if (p != null) Object.Destroy(p.gameObject);
        }

        [UnityTest]
        public IEnumerator GapGate_TheGapCrosserSwungAtIt_LaysACrossingAndOpensIt_OtherItemsOrBareDoNothing()
        {
            GameObject player = Player(Origin);
            GameObject block = Block(Origin + Vector2.down * 1.1f * K);
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
        public IEnumerator Trial_SteppingOntoTheTrialIsland_FiresOnce_AndRepeatsDoNot()
        {
            IslandLayout layout = Layout(true);
            Assert.IsEmpty(layout.Problems("test", _ => true));
            IslandScreen screen = Track(IslandScreen.Create("Test Islands", null, layout, Origin, null).gameObject).GetComponent<IslandScreen>();
            var go = new GameObject("Math Trial");
            go.transform.SetParent(screen.transform, false);
            var trial = go.AddComponent<MathTrial>();
            trial.Configure(screen, layout.TrialIsland, MathPlan.TrialCurrency, null);
            var paid = new List<int>();
            trial.Completed += c => paid.Add(c);
            GameObject player = Player(Origin + layout.Islands[4].center);
            player.GetComponent<Game.Tracer.PlayerMover>().enabled = false;
            var body = player.GetComponent<Rigidbody2D>();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(trial.IsComplete, "Not yet on the trial island");

            // Over the mended span onto the trial island: it pays once.
            BrokenSpan gap = screen.Gaps.First(g => layout.Bridges[g.Bridge].Joins(layout.TrialIsland));
            Assert.IsTrue(screen.Mend(gap.Bridge));
            yield return Push(body, Vector2.left, 0.8f);
            Assert.AreEqual(layout.TrialIsland, layout.IslandAt(body.position - Origin), $"On the trial island (at {body.position - Origin})");
            Assert.IsTrue(trial.IsComplete);
            CollectionAssert.AreEqual(new[] { MathPlan.TrialCurrency }, paid);

            // Off and on again: nothing more.
            yield return Push(body, Vector2.right, 0.6f);
            yield return Push(body, Vector2.left, 0.6f);
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
                Enemy weak = MathEnemies.Spawn(kind, weakness, Origin + new Vector2(50f * K, (int)kind * 5f * K), new Rect(Origin, Vector2.one * 100f),
                    null, null, null);
                Enemy plain = MathEnemies.Spawn(kind, weakness, Origin + new Vector2(60f * K, (int)kind * 5f * K), new Rect(Origin, Vector2.one * 100f),
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
