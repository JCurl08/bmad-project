using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Game.Tracer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Game.Cube.Tests
{
    /// <summary>
    /// NPC runtime behaviour on objects built in the test (far from anything a scene may hold): Interact in
    /// range opens the dialogue, advances it and closes it after the last line; out of range it does
    /// nothing; a hostile race refuses until the flag is cleared; and stand / wander / flee legs move as
    /// they should.
    /// </summary>
    public class NpcComponentTests : InputTestFixture
    {
        /// <summary>Lengths and speeds below are in the first 16x10 screens' units, scaled like the game's (4.1).</summary>
        private const float K = ScreenMath.ContentScale;

        private static readonly Vector2 Origin = new Vector2(-5000f, 5000f);
        private static readonly string[] TestLines = { "one", "two", "three" };

        private readonly List<Object> created = new List<Object>();
        private Keyboard keyboard;
        private Transform player;
        private DialogueBox box;
        private RaceRelations relations;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            player = Track(new GameObject("Test Player")).transform;
            player.position = Origin + new Vector2(1f * K, 0f);
            box = Track(new GameObject("Test Dialogue Box")).AddComponent<DialogueBox>();
            relations = new RaceRelations();
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

        private static Rect BoundsAround(Vector2 centre) => new Rect(centre - new Vector2(4f * K, 3f * K), new Vector2(8f * K, 6f * K));

        private NpcTalker MakeNpc(Movement movement, Vector2 position, Race race = Race.Mushroom)
        {
            NpcSpec spec = NpcFactory.ChooseParts(1, race, 0);
            spec = spec.With(RacePartSets.For(race).Legs.First(l => l.Movement == movement));
            NpcTalker npc = NpcFactory.Spawn(spec, TestLines, position, BoundsAround(Origin), relations, player);
            Track(npc.gameObject);
            npc.Box = box;
            return npc;
        }

        /// <summary>Canvas pixels per world unit on the 960x600 canvas for a screen this many units high.</summary>
        private static float PixelsPerUnit(float screenHeight) => 600f / screenHeight;

        [UnityTest]
        public IEnumerator People_AreDrawnAThirdLargerOnTheCanvas_ThanOnTheFirstScreens_WithTheirCollidersUnchanged()
        {
            // 4.1 review: people read about 1.33x larger on the canvas than on the first 16x10 screens; visual only.
            Assert.AreEqual(4f / 3f, CubeSettings.PeopleArtScale, 1e-5f);
            NpcTalker npc = MakeNpc(Movement.Stand, Origin);
            Enemy enemy = Track(Enemy.Spawn(null, Origin + new Vector2(6f * K, 0f), BoundsAround(Origin)).gameObject).GetComponent<Enemy>();
            yield return null;

            // NPC: the parts' authored height (first screens' units) against the drawn height.
            NpcSpec spec = npc.Spec;
            float authored = spec.Legs.Size.y + spec.Torso.Size.y + spec.Head.Size.y;
            SpriteRenderer[] parts = npc.GetComponentsInChildren<SpriteRenderer>();
            float top = parts.Max(p => p.bounds.max.y), bottom = parts.Min(p => p.bounds.min.y);
            float drawn = top - bottom;
            float canvasRatio = drawn * PixelsPerUnit(CubeWorld.ScreenSize.y) / (authored * PixelsPerUnit(10f));
            Assert.AreEqual(CubeSettings.PeopleArtScale, canvasRatio, 0.01f, "An NPC reads 1.33x as tall on the canvas as before");
            var box = npc.GetComponent<BoxCollider2D>();
            Assert.AreEqual(authored * ScreenMath.ContentScale, box.size.y, 1e-3f, "Its collider only scales with the screen");
            Assert.AreEqual(NpcFactory.BodyWidth, box.size.x, 1e-4f);

            // Enemy: art against its body.
            SpriteRenderer art = enemy.transform.Find("Visual").GetComponent<SpriteRenderer>();
            float enemyRatio = art.bounds.size.y * PixelsPerUnit(CubeWorld.ScreenSize.y) / (0.95f * PixelsPerUnit(10f));
            Assert.AreEqual(CubeSettings.PeopleArtScale, enemyRatio, 0.01f, "An enemy reads 1.33x as big on the canvas as before");
            Assert.AreEqual(0.4f * ScreenMath.ContentScale, enemy.GetComponent<CircleCollider2D>().radius, 1e-4f, "Its collider only scales with the screen");
        }

        [UnityTest]
        public IEnumerator Spawn_StacksThreePartSprites_WithACollider()
        {
            NpcTalker npc = MakeNpc(Movement.Stand, Origin);
            yield return null;
            SpriteRenderer[] parts = npc.GetComponentsInChildren<SpriteRenderer>();
            Assert.AreEqual(3, parts.Length);
            Assert.Less(parts.Single(p => p.name == "Legs").transform.position.y, parts.Single(p => p.name == "Torso").transform.position.y);
            Assert.Less(parts.Single(p => p.name == "Torso").transform.position.y, parts.Single(p => p.name == "Head").transform.position.y);
            Assert.IsNotNull(npc.GetComponent<Collider2D>());
            Assert.AreEqual(Movement.Stand, npc.GetComponent<NpcMover>().Movement);
        }

        [UnityTest]
        public IEnumerator Interact_InRange_OpensAdvancesAndClosesAfterTheLastLine()
        {
            NpcTalker npc = MakeNpc(Movement.Stand, Origin);
            yield return null;

            yield return this.Tap(keyboard.eKey);
            Assert.IsTrue(box.IsOpen, "Interact in range did not open the dialogue");
            Assert.AreSame(npc, box.Owner);
            Assert.AreEqual(npc.Spec.DisplayName, box.Speaker);
            Assert.AreEqual("one", box.CurrentLine, "The opening press must not also advance");

            yield return this.Tap(keyboard.eKey);
            Assert.AreEqual("two", box.CurrentLine);
            yield return this.Tap(keyboard.eKey);
            Assert.AreEqual("three", box.CurrentLine);
            yield return this.Tap(keyboard.eKey);
            Assert.IsFalse(box.IsOpen, "The box must close after the last line");
            Assert.IsNull(box.CurrentLine);
        }

        [UnityTest]
        public IEnumerator Interact_OutOfRange_DoesNothing()
        {
            MakeNpc(Movement.Stand, Origin);
            player.position = Origin + new Vector2(NpcTalker.DefaultRange + 1f * K, 0f);
            yield return null;

            yield return this.Tap(keyboard.eKey);
            Assert.IsFalse(box.IsOpen);
        }

        [UnityTest]
        public IEnumerator HostileRace_RefusesToTalk_ClearingTheFlagRestoresTalking()
        {
            NpcTalker npc = MakeNpc(Movement.Stand, Origin, Race.Shape);
            int refused = 0;
            npc.Refused += _ => refused++;
            yield return null;

            relations.SetHostile(Race.Shape, true);
            yield return this.Tap(keyboard.eKey);
            Assert.IsFalse(box.IsOpen, "A hostile NPC opened a dialogue");
            Assert.AreEqual(1, refused);

            relations.SetHostile(Race.Mushroom, true); // another race: no effect on this NPC
            relations.SetHostile(Race.Shape, false);
            yield return this.Tap(keyboard.eKey);
            Assert.IsTrue(box.IsOpen, "Clearing the flag must restore talking");

            relations.SetHostile(Race.Shape, true);
            Assert.IsFalse(box.IsOpen, "Turning hostile mid-conversation closes it");
        }

        [UnityTest]
        public IEnumerator StandLegs_StayPut()
        {
            NpcTalker npc = MakeNpc(Movement.Stand, Origin);
            player.position = Origin + new Vector2(0.5f * K, 0f);
            yield return new WaitForSeconds(1f);
            Assert.Less(Vector2.Distance(npc.transform.position, Origin), 1e-3f);
        }

        [UnityTest]
        public IEnumerator WanderLegs_MoveWithinTheirScreenBounds()
        {
            NpcTalker npc = MakeNpc(Movement.Wander, Origin);
            player.position = Origin + new Vector2(100f, 0f);
            Rect bounds = npc.GetComponent<NpcMover>().Bounds;
            float furthest = 0f;
            float end = Time.time + 3f;
            while (Time.time < end)
            {
                yield return new WaitForFixedUpdate();
                Vector2 at = npc.transform.position;
                furthest = Mathf.Max(furthest, Vector2.Distance(at, Origin));
                Assert.IsTrue(at.x >= bounds.xMin - 1e-3f && at.x <= bounds.xMax + 1e-3f &&
                              at.y >= bounds.yMin - 1e-3f && at.y <= bounds.yMax + 1e-3f, $"Wandered out of bounds to {at}");
            }
            Assert.Greater(furthest, 0.5f * K, "Wander legs never moved");
        }

        private GameObject MakeWall(Vector2 centre, Vector2 size)
        {
            var go = Track(new GameObject("Test Wall"));
            go.transform.position = centre;
            go.AddComponent<BoxCollider2D>().size = size;
            return go;
        }

        private Gate MakeClosedGate(Vector2 centre, Vector2 size)
        {
            var go = Track(new GameObject("Test Gate"));
            go.transform.position = centre;
            var gate = go.AddComponent<Gate>();
            gate.Solid.size = size;
            gate.RequiredItem = Track(ItemDefinition.Create("npc-test-item", "NPC Test Item", Theme.Physics, Color.blue));
            return gate;
        }

        private static void AssertInside(Bounds b, Rect interior, string what)
        {
            const float tolerance = 0.05f * K;
            Assert.IsTrue(b.min.x >= interior.xMin - tolerance && b.max.x <= interior.xMax + tolerance &&
                          b.min.y >= interior.yMin - tolerance && b.max.y <= interior.yMax + tolerance,
                $"{what}: NPC collider {b.min}..{b.max} entered a wall or gate (interior {interior})");
        }

        [UnityTest]
        public IEnumerator WanderLegs_InAWalledPen_NeverEnterAWallOrClosedGate()
        {
            // A pen around Origin: walls left, top and bottom, a closed gate on the right. The mover's bounds
            // (8 x 6) are much larger, so only the colliders keep it in.
            var interior = new Rect(Origin + new Vector2(-1.5f * K, -0.5f * K), new Vector2(3f * K, 2.4f * K));
            MakeWall(Origin + new Vector2(-1.6f * K, 0.7f * K), new Vector2(0.2f * K, 3f * K));
            MakeWall(Origin + new Vector2(0f, -0.6f * K), new Vector2(3.4f * K, 0.2f * K));
            MakeWall(Origin + new Vector2(0f, 2.0f * K), new Vector2(3.4f * K, 0.2f * K));
            Gate gate = MakeClosedGate(Origin + new Vector2(1.6f * K, 0.7f * K), new Vector2(0.2f * K, 3f * K));
            NpcTalker npc = MakeNpc(Movement.Wander, Origin);
            player.position = Origin + new Vector2(100f, 0f);
            var body = npc.GetComponent<Collider2D>();
            float furthest = 0f;
            float end = Time.time + 4f;
            while (Time.time < end)
            {
                yield return new WaitForFixedUpdate();
                AssertInside(body.bounds, interior, "Wander");
                furthest = Mathf.Max(furthest, Vector2.Distance(npc.transform.position, Origin));
            }
            Assert.IsFalse(gate.IsOpen, "An NPC must not open a gate");
            Assert.Greater(furthest, 0.3f * K, "Wander legs never moved inside the pen");
        }

        [UnityTest]
        public IEnumerator FleeLegs_StopAtAWall()
        {
            MakeWall(Origin + new Vector2(-1.2f * K, 0.5f * K), new Vector2(0.2f * K, 4f * K));
            NpcTalker npc = MakeNpc(Movement.Flee, Origin);
            player.position = Origin + new Vector2(0.8f * K, 0.5f * K);
            yield return new WaitForSeconds(1f);
            Bounds b = npc.GetComponent<Collider2D>().bounds;
            Assert.GreaterOrEqual(b.min.x, Origin.x - 1.1f * K - 0.05f * K, "Fled through the wall");
            Assert.Less(npc.transform.position.x, Origin.x - 0.3f * K, "Did not flee toward the wall at all");
        }

        [UnityTest]
        public IEnumerator SeveralInRange_OnlyTheNearestAnswers()
        {
            MakeNpc(Movement.Stand, Origin, Race.Finch);                              // 1.0 from the player
            NpcTalker near = MakeNpc(Movement.Stand, Origin + new Vector2(1.8f * K, 0f), Race.Alien); // 0.8
            yield return null;

            yield return this.Tap(keyboard.eKey);
            Assert.IsTrue(box.IsOpen);
            Assert.AreSame(near, box.Owner, "A farther NPC answered");
        }

        [UnityTest]
        public IEnumerator DestroyingTheSpeaker_ClosesTheBox()
        {
            NpcTalker npc = MakeNpc(Movement.Stand, Origin);
            yield return null;
            yield return this.Tap(keyboard.eKey);
            Assert.IsTrue(box.IsOpen);

            Object.Destroy(npc.gameObject);
            yield return null;
            Assert.IsFalse(box.IsOpen, "The box still shows a destroyed NPC's lines");
        }

        [UnityTest]
        public IEnumerator HostileInRange_DoesNotRefuse_PressesThatAdvanceAnotherConversation()
        {
            NpcTalker hostile = MakeNpc(Movement.Stand, Origin + new Vector2(1.8f * K, 0f), Race.Shape); // nearest
            NpcTalker friend = MakeNpc(Movement.Stand, Origin, Race.Finch);
            int refused = 0;
            hostile.Refused += _ => refused++;
            relations.SetHostile(Race.Shape, true);
            yield return null;

            Assert.IsTrue(box.Open(friend.Spec.DisplayName, TestLines, friend));
            yield return this.Tap(keyboard.eKey);
            Assert.AreEqual("two", box.CurrentLine);
            Assert.AreEqual(0, refused, "Refused was raised by a press meant for the open conversation");
        }

        [UnityTest]
        public IEnumerator HostilityChangedWhileInactive_TintIsRightOnReenable()
        {
            NpcTalker npc = MakeNpc(Movement.Stand, Origin, Race.Alien);
            SpriteRenderer head = npc.GetComponentsInChildren<SpriteRenderer>().Single(r => r.name == "Head");
            Color normal = head.color;
            yield return null;

            relations.SetHostile(Race.Alien, true);
            Color hostile = head.color;
            Assert.AreNotEqual(normal, hostile);
            npc.gameObject.SetActive(false);
            relations.SetHostile(Race.Alien, false);
            npc.gameObject.SetActive(true);
            Assert.AreEqual(normal, head.color, "Stale hostile tint after re-enabling");
        }

        [UnityTest]
        public IEnumerator FleeLegs_MoveAwayWhenThePlayerIsNear()
        {
            NpcTalker npc = MakeNpc(Movement.Flee, Origin);
            NpcMover mover = npc.GetComponent<NpcMover>();
            player.position = Origin + new Vector2(mover.FleeRadius + 3f * K, 0f);
            yield return new WaitForSeconds(0.5f);
            Assert.Less(Vector2.Distance(npc.transform.position, Origin), 1e-3f, "Flee legs moved with nobody near");

            player.position = Origin + new Vector2(1f * K, 0f);
            yield return new WaitForSeconds(0.6f);
            Vector2 at = npc.transform.position;
            Assert.Greater(Vector2.Distance(at, player.position), 2f * K, "Did not get away from the player");
            Assert.Less(at.x, Origin.x - 0.5f * K, "Did not flee away from the player (westward)");
            Assert.IsTrue(mover.Bounds.Contains(at) || Mathf.Approximately(at.x, mover.Bounds.xMin), "Fled out of bounds");
        }
    }
}
