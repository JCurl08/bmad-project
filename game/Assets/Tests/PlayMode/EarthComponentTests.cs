using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Tracer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The Earth mechanics on objects built in the test (far from anything a scene may hold), one test per I/O row: each
    /// phenomenon's gate opens to one swing of its Stormcaller* (with the gate-open effect and its own picture: pillars
    /// fall, fire is doused, water freezes into a walkable crossing, debris blows away) and to nothing else; a wind band
    /// pushes the player at a bounded speed, never through a wall, and walking against it still makes headway; snow makes
    /// the player slide but it can always leave; freezing rain slows it, never below the minimum; a crevice is solid; the
    /// hazards only reach movement through PlayerMover's modifier hook, so EffectiveSpeed (speed upgrades, the Physics doors)
    /// is untouched; the trial fires once and resets on a wrong target; Earth enemies take markedly more damage from their
    /// weakness.
    /// </summary>
    public class EarthComponentTests : InputTestFixture
    {
        private static readonly Vector2 Origin = new Vector2(-9000f, -7000f);

        private readonly List<Object> created = new List<Object>();
        private Keyboard keyboard;
        private Dictionary<Phenomenon, ItemDefinition> wands;
        private ItemDefinition other;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            wands = StormcallerItem.VariantOrder.ToDictionary(k => k, k => Track(StormcallerItem.CreateItem(k)));
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
            go.AddComponent<CircleCollider2D>().radius = 0.4f;
            go.AddComponent<PlayerMover>();
            go.AddComponent<Inventory>();
            go.AddComponent<Health>();
            go.AddComponent<PlayerStats>();
            go.AddComponent<Equipment>();
            go.AddComponent<PlayerAttack>().Cooldown = 0f;
            go.AddComponent<HazardReceiver>();
            return go;
        }

        private GameObject Block(Vector2 at, Vector2? size = null)
        {
            var go = Track(new GameObject("Block"));
            go.transform.position = at;
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.AddComponent<SpriteRenderer>().sprite = NpcFactory.ShapeSprite(PartShape.Square);
            go.AddComponent<BoxCollider2D>().size = size ?? new Vector2(GateSlot.GateWidth, GateSlot.GateThickness);
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

        private static IEnumerator Steps(int count)
        {
            for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
        }

        // ---------- Gates ----------

        [UnityTest]
        public IEnumerator EachGate_OpensToOneSwingOfItsOwnStormcaller_OtherWandsItemsBareAndTouchDoNothing()
        {
            foreach (Phenomenon kind in StormcallerItem.VariantOrder)
            {
                Vector2 at = Origin + new Vector2((int)kind * 20f, 0f);
                GameObject player = Player(at);
                GameObject block = Block(at + Vector2.down * 1.1f);
                PhenomenonGate gate = PhenomenonGate.Build(kind, block, wands[kind], false, null);
                Assert.AreEqual(kind, gate.Kind);
                Assert.AreEqual(PhenomenonGate.ArtOf(kind) == ArtKey.GateRockPillars, gate is RockPillarGate);
                int opened = 0;
                gate.Opened += _ => opened++;
                yield return null;

                player.GetComponent<Inventory>().Add(wands[kind]);
                Assert.IsFalse(gate.TryOpen(player.GetComponent<Collider2D>()), $"{kind}: touching does nothing");
                Assert.AreEqual(0, Swing(player, null), $"{kind}: a bare swing does nothing");
                Assert.AreEqual(0, Swing(player, other), $"{kind}: another item does nothing");
                foreach (Phenomenon wrong in StormcallerItem.VariantOrder.Where(k => k != kind))
                    Assert.AreEqual(0, Swing(player, wands[wrong]), $"{kind}: the {wrong} wand does nothing");
                Assert.IsFalse(gate.IsOpen);
                Assert.IsTrue(gate.Solid.enabled);

                int effects = GateOpenEffect.Played;
                Assert.AreEqual(1, Swing(player, wands[kind]), $"{kind}: its own wand opens it");
                Assert.IsTrue(gate.IsOpen);
                Assert.IsFalse(gate.Solid.enabled, $"{kind}: the way is open");
                Assert.AreEqual(1, opened);
                Assert.AreEqual(effects + 1, GateOpenEffect.Played, $"{kind}: the gate-open effect plays");
                Assert.IsFalse(gate.transform.Find("Wand Mark").gameObject.activeSelf);
                switch (gate)
                {
                    case RockPillarGate pillars: Assert.IsNotNull(pillars.Rubble, "The pillars lie toppled"); break;
                    case FireWallGate fire: Assert.IsNotNull(fire.Steam, "The fire is doused to steam"); break;
                    case FrozenWaterGate water:
                        Assert.IsNotNull(water.Ice, "The water froze into a crossing");
                        Assert.IsTrue(water.Ice.enabled);
                        break;
                    case DebrisGate debris:
                        Assert.IsNotNull(debris.Swirl, "The debris blew away");
                        Assert.IsFalse(debris.Visual.enabled);
                        break;
                    default: Assert.Fail($"{kind}: unexpected gate {gate.GetType()}"); break;
                }
                Assert.IsTrue(player.GetComponent<Inventory>().Has(wands[kind]), "The wand is kept");
                Assert.AreEqual(0, Swing(player, wands[kind]), "Already open: nothing more");
            }
        }

        [UnityTest]
        public IEnumerator FrozenWater_IsWalkableOnceFrozen()
        {
            GameObject player = Player(Origin + new Vector2(200f, 0f));
            GameObject block = Block(player.transform.position + Vector3.down * 1.1f);
            FrozenWaterGate gate = FrozenWaterGate.Build(block, wands[Phenomenon.Snow], false, null);
            yield return null;
            Swing(player, wands[Phenomenon.Snow]);
            Assert.IsTrue(gate.IsOpen);
            // Walk straight across the ice: nothing solid stops the player.
            var body = player.GetComponent<Rigidbody2D>();
            player.GetComponent<PlayerMover>().enabled = false;
            Vector2 start = body.position;
            for (int i = 0; i < 25; i++)
            {
                body.linearVelocity = Vector2.down * 4f;
                yield return new WaitForFixedUpdate();
            }
            Assert.Less(body.position.y, start.y - 1.5f, "The player crossed the frozen water");
        }

        // ---------- Hazards ----------

        [UnityTest]
        public IEnumerator Wind_PushesThePlayerAlongTheBand_AtABoundedSpeed_NeverThroughAWall()
        {
            Vector2 at = Origin + new Vector2(300f, 0f);
            GameObject player = Player(at);
            var body = player.GetComponent<Rigidbody2D>();
            var mover = player.GetComponent<PlayerMover>();
            WindBand band = Track(WindBand.Create(null, default, new Rect(at - new Vector2(6f, 1.5f), new Vector2(12f, 3f)), Vector2.right,
                EarthPlan.WindPush(EarthPlan.MaxLevel), null)).GetComponent<WindBand>();
            Track(band.gameObject);
            yield return Steps(10);

            float push = EarthPlan.WindPush(EarthPlan.MaxLevel) * mover.EffectiveSpeed;
            Assert.AreEqual(push, body.linearVelocity.x, 0.05f, "Pushed along the band at its strength");
            Assert.AreEqual(0f, body.linearVelocity.y, 0.05f);
            Assert.LessOrEqual(body.linearVelocity.magnitude, PlayerMover.MaxPushFraction * mover.EffectiveSpeed + 1e-3f, "A bounded push");
            Assert.AreEqual(TimeField.BasePlayerSpeed, mover.EffectiveSpeed, 1e-4f, "The hazard never changes EffectiveSpeed");
            Assert.Greater(body.position.x, at.x, "It moved with the wind");

            // A wall downwind: the player is pressed against it, never through it.
            float wallX = body.position.x + 2f;
            Block(new Vector2(wallX + 0.5f, at.y), new Vector2(1f, 4f));
            yield return Steps(150);
            Assert.Less(body.position.x, wallX - 0.35f, "Never pushed through the wall");
        }

        [UnityTest]
        public IEnumerator Wind_WalkingAgainstIt_StillMakesHeadway()
        {
            Vector2 at = Origin + new Vector2(400f, 0f);
            GameObject player = Player(at);
            var body = player.GetComponent<Rigidbody2D>();
            Track(WindBand.Create(null, default, new Rect(at - new Vector2(8f, 1.5f), new Vector2(16f, 3f)), Vector2.left,
                EarthPlan.WindPush(EarthPlan.MaxLevel), null).gameObject);
            yield return null;
            Press(keyboard.dKey, queueEventOnly: true);
            yield return null;
            yield return Steps(30);
            yield return null; // input state is only valid in the dynamic update
            Release(keyboard.dKey, queueEventOnly: true);
            yield return null;
            Assert.Greater(body.position.x, at.x + 1f, "Walking into the wind still gets somewhere");

            // Pure: at every level and speed, against the strongest push a hazard may give, the walk still goes forward.
            foreach (float speed in new[] { 6f, 9f, 12f })
            {
                var worst = new MoveModifier { SpeedMultiplier = 0f, Push = Vector2.left * 10f, Grip = 1f };
                Vector2 v = PlayerMover.TargetVelocity(Vector2.right, speed, worst);
                Assert.Greater(v.x, 0f, $"speed {speed}: the clamped worst case still makes headway");
                for (int level = 1; level <= EarthPlan.MaxLevel; level++)
                {
                    var wind = new MoveModifier { SpeedMultiplier = 1f, Push = Vector2.left * EarthPlan.WindPush(level), Grip = 1f };
                    Assert.Greater(PlayerMover.TargetVelocity(Vector2.right, speed, wind).x, 0.4f * speed, $"level {level}");
                }
            }
        }

        [UnityTest]
        public IEnumerator Snow_MakesThePlayerSlide_ButItCanAlwaysLeave()
        {
            Vector2 at = Origin + new Vector2(500f, 0f);
            GameObject player = Player(at);
            var body = player.GetComponent<Rigidbody2D>();
            var mover = player.GetComponent<PlayerMover>();
            Track(SnowPatch.Create(null, default, new Rect(at - new Vector2(3f, 3f), new Vector2(6f, 6f)), EarthPlan.SnowGrip(EarthPlan.MaxLevel),
                null).gameObject);
            yield return Steps(2);
            Assert.Less(mover.Modifier.Grip, 1f, "On snow");

            // Momentum: a velocity with no input only eases away.
            body.linearVelocity = Vector2.right * 6f;
            yield return Steps(5);
            Assert.Greater(body.linearVelocity.x, 2f, "Still sliding a few steps after letting go");

            // Steering off the patch gets the player out (and at full speed off it).
            yield return null; // never press right after a physics step
            Press(keyboard.aKey, queueEventOnly: true);
            yield return null;
            float end = Time.time + 6f;
            while (Time.time < end && body.position.x > at.x - 3.6f) yield return new WaitForFixedUpdate();
            yield return Steps(20);
            yield return null; // input state is only valid in the dynamic update
            Release(keyboard.aKey, queueEventOnly: true);
            yield return null;
            Assert.Less(body.position.x, at.x - 3.4f, "The player can always leave the patch");
            Assert.IsTrue(mover.Modifier.IsNone, "Off the patch nothing slides");

            // Pure: with any grip a hazard may give, the velocity reaches the wanted one.
            var m = new MoveModifier { SpeedMultiplier = 1f, Push = Vector2.zero, Grip = 0f };
            Vector2 v = Vector2.zero;
            for (int i = 0; i < 400; i++) v = PlayerMover.NextVelocity(v, Vector2.right * 6f, m);
            Assert.AreEqual(6f, v.x, 0.01f, "Even the least grip gets there");
        }

        [UnityTest]
        public IEnumerator FreezingRain_SlowsThePlayer_NeverBelowTheMinimumSpeed()
        {
            Vector2 at = Origin + new Vector2(600f, 0f);
            GameObject player = Player(at);
            var body = player.GetComponent<Rigidbody2D>();
            var mover = player.GetComponent<PlayerMover>();
            Track(FreezingRain.Create(null, default, new Rect(at - new Vector2(8f, 2f), new Vector2(16f, 4f)), EarthPlan.RainSpeed(EarthPlan.MaxLevel),
                null).gameObject);
            Press(keyboard.dKey, queueEventOnly: true);
            yield return null;
            yield return Steps(15);
            float speed = body.linearVelocity.x;
            yield return null; // input state is only valid in the dynamic update
            Release(keyboard.dKey, queueEventOnly: true);
            yield return null;
            Assert.AreEqual(EarthPlan.RainSpeed(EarthPlan.MaxLevel) * mover.EffectiveSpeed, speed, 0.05f, "Slower in the rain");
            Assert.Less(speed, mover.EffectiveSpeed - 0.5f);
            Assert.GreaterOrEqual(speed, PlayerMover.MinSpeedMultiplier * mover.EffectiveSpeed - 1e-3f, "Never below the minimum");

            // Pure: no modifier can slow below the minimum, and speed upgrades scale with it.
            var hard = new MoveModifier { SpeedMultiplier = 0.01f, Push = Vector2.zero, Grip = 1f };
            Assert.AreEqual(PlayerMover.MinSpeedMultiplier * 6f, PlayerMover.TargetVelocity(Vector2.right, 6f, hard).x, 1e-4f);
            Assert.AreEqual(2f * PlayerMover.TargetVelocity(Vector2.right, 6f, hard).x, PlayerMover.TargetVelocity(Vector2.right, 12f, hard).x, 1e-4f,
                "A speed upgrade still counts in the rain");
        }

        [UnityTest]
        public IEnumerator Crevice_IsSolid_AndExposesAFossil()
        {
            Vector2 at = Origin + new Vector2(700f, 0f);
            QuakeCrevice crevice = QuakeCrevice.Create(null, default, new Rect(at + new Vector2(-1.5f, -1.3f), new Vector2(3f, 0.6f)), null);
            Track(crevice.gameObject);
            GameObject player = Player(at);
            var body = player.GetComponent<Rigidbody2D>();
            player.GetComponent<PlayerMover>().enabled = false;
            yield return null;
            Assert.IsNotNull(crevice.Fossil);
            Assert.IsFalse(crevice.Solid.isTrigger);
            for (int i = 0; i < 40; i++)
            {
                body.linearVelocity = Vector2.down * 4f;
                yield return new WaitForFixedUpdate();
            }
            Assert.Greater(body.position.y, at.y - 1f, "Nobody walks through a crevice");
        }

        [UnityTest]
        public IEnumerator Hazards_ReachMovementOnlyThroughTheModifierHook_AndNotWithoutAReceiver()
        {
            Vector2 at = Origin + new Vector2(800f, 0f);
            GameObject player = Player(at);
            var body = player.GetComponent<Rigidbody2D>();
            var mover = player.GetComponent<PlayerMover>();
            Track(WindBand.Create(null, default, new Rect(at - new Vector2(3f, 3f), new Vector2(6f, 6f)), Vector2.up, 0.3f, null).gameObject);
            yield return Steps(5);
            Assert.Greater(body.linearVelocity.y, 1f, "With the receiver the wind pushes");

            Object.Destroy(player.GetComponent<HazardReceiver>());
            yield return null;
            body.position = at;
            yield return Steps(5);
            Assert.IsTrue(mover.Modifier.IsNone, "Without the receiver nothing reaches the mover");
            Assert.AreEqual(Vector2.zero, body.linearVelocity, "No push without the hook");
        }

        // ---------- Trial ----------

        [UnityTest]
        public IEnumerator Trial_StruckInOrderWithTheRolledWand_FiresOnce_AWrongTargetResets_OthersDoNothing()
        {
            foreach (Phenomenon kind in StormcallerItem.VariantOrder)
            {
                var go = Track(new GameObject("Earth Trial"));
                var trial = go.AddComponent<EarthTrial>();
                Vector2 at = Origin + new Vector2(900f + (int)kind * 30f, 0f);
                var positions = Enumerable.Range(0, EarthTrial.PieceCount).Select(i => at + new Vector2(i * 4f, 0f)).ToList();
                trial.Configure(kind, wands[kind], positions, EarthPlan.TrialCurrency, null);
                var paid = new List<int>();
                int resets = 0;
                trial.Completed += c => paid.Add(c);
                trial.ResetPuzzle += _ => resets++;
                yield return null;
                Assert.AreEqual(EarthTrial.PieceCount, trial.Pieces.Count);
                Assert.IsTrue(trial.Pieces.All(p => p.Kind == kind));
                Assert.AreEqual(EarthTrialPiece.IsSolid(kind), !trial.Pieces[0].GetComponent<Collider2D>().isTrigger);

                Assert.IsFalse(trial.OnPieceHit(trial.Pieces[0], other), "Another item does nothing");
                Assert.IsFalse(trial.OnPieceHit(trial.Pieces[0], null), "A bare swing does nothing");
                foreach (Phenomenon wrong in StormcallerItem.VariantOrder.Where(k => k != kind))
                    Assert.IsFalse(trial.OnPieceHit(trial.Pieces[0], wands[wrong]), "Another wand does nothing");

                Assert.IsTrue(trial.OnPieceHit(trial.Pieces[0], wands[kind]));
                Assert.IsTrue(trial.OnPieceHit(trial.Pieces[2], wands[kind]), "A wrong target reacts...");
                Assert.AreEqual(1, resets, "...by putting every target back");
                Assert.AreEqual(0, trial.Progress);
                Assert.IsTrue(trial.Pieces.All(p => !p.Done));

                for (int i = 0; i < EarthTrial.PieceCount; i++) trial.OnPieceHit(trial.Pieces[i], wands[kind]);
                Assert.IsTrue(trial.IsComplete);
                CollectionAssert.AreEqual(new[] { EarthPlan.TrialCurrency }, paid);
                for (int i = 0; i < EarthTrial.PieceCount; i++) Assert.IsFalse(trial.OnPieceHit(trial.Pieces[i], wands[kind]));
                Assert.IsFalse(trial.Complete(), "Completing again does nothing");
                Assert.AreEqual(1, paid.Count, "Fires once");
            }
        }

        [UnityTest]
        public IEnumerator Trial_AReset_NeverTurnsAPieceSolidAroundThePlayer_ItBlocksAgainOnceClear()
        {
            var go = Track(new GameObject("Earth Trial"));
            var trial = go.AddComponent<EarthTrial>();
            Vector2 at = Origin + new Vector2(1300f, 0f);
            var positions = Enumerable.Range(0, EarthTrial.PieceCount).Select(i => at + new Vector2(i * 4f, 0f)).ToList();
            ItemDefinition wand = wands[Phenomenon.Quake];
            trial.Configure(Phenomenon.Quake, wand, positions, EarthPlan.TrialCurrency, null);
            yield return null;
            EarthTrialPiece first = trial.Pieces[0];
            var box = first.GetComponent<BoxCollider2D>();
            Assert.IsTrue(trial.OnPieceHit(first, wand));
            Assert.IsFalse(box.enabled, "Toppled: the way is open");

            // The player walks onto where the pillar stood, then a wrong target resets the trial.
            GameObject player = Player(positions[0]);
            player.GetComponent<PlayerMover>().enabled = false;
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(trial.OnPieceHit(trial.Pieces[2], wand));
            Assert.IsFalse(first.Done);
            Assert.IsFalse(box.enabled, "Never solid around the player");
            Assert.IsTrue(first.WaitingToBlock);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(box.enabled, "Still waiting while the player stands there");

            // Once the player steps off, it blocks again.
            var body = player.GetComponent<Rigidbody2D>();
            body.position = positions[0] + new Vector2(0f, -2f);
            player.transform.position = body.position;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(box.enabled, "Solid again once clear");
            Assert.IsFalse(first.WaitingToBlock);
        }

        // ---------- Enemies ----------

        [UnityTest]
        public IEnumerator EarthEnemies_TakeMarkedlyMoreDamageFromTheirWeakness()
        {
            ItemDefinition crosser = Track(GapCrosserItem.CreateItem());
            foreach (EarthEnemyKind kind in new[] { EarthEnemyKind.Squall, EarthEnemyKind.Rumbler })
            {
                ItemDefinition weakness = kind == EarthEnemyKind.Squall ? wands[Phenomenon.Rain] : crosser;
                ItemDefinition notWeakness = kind == EarthEnemyKind.Squall ? other : wands[Phenomenon.Rain];
                Enemy weak = EarthEnemies.Spawn(kind, weakness, Origin + new Vector2(1100f, (int)kind * 5f), new Rect(Origin, Vector2.one * 100f),
                    null, null, null);
                Enemy plain = EarthEnemies.Spawn(kind, weakness, Origin + new Vector2(1110f, (int)kind * 5f), new Rect(Origin, Vector2.one * 100f),
                    null, null, null);
                Track(weak.gameObject);
                Track(plain.gameObject);
                weak.DestroyOnDeath = false;
                plain.DestroyOnDeath = false;
                Assert.AreEqual(kind, weak.GetComponent<EarthEnemy>().Kind);
                Assert.AreSame(weakness, weak.Weakness);
                StringAssert.Contains(EarthEnemies.Name(kind), weak.name);
                yield return null;

                float byWeakness = weak.Health.TakeDamage(1f, weakness);
                float byOther = plain.Health.TakeDamage(1f, notWeakness);
                Assert.Greater(byWeakness, byOther * 2.5f, $"{kind}: the weakness hits markedly harder");
            }
        }
    }
}
