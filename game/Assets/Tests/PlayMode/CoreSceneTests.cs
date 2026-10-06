using System;
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
    /// Maxwell's Demon in the real Cube scene: walking into the core portal on every built face on run 1 moves the player
    /// into the arena (cube navigation suspended, the camera framing it) and starts an ordered fight; a swing knocks a
    /// particle along the facing direction across a membrane gap, and through the door only while it is open; an idle
    /// player loses ground as the Demon brings strays home; order pulses are telegraphed, avoidable and deal damage under
    /// the combat rules; winning and dying each raise RunState's event once (later events ignored); the fallback boss
    /// wins and loses the same way; a replayed seed drifts identically; the debug overlay and spawns know the arena; and a
    /// scripted keyboard player (8 directions, starting kit, pulses on) wins in 60–120 s.
    /// Long waits run at TimeScale (physics steps are unchanged, so behaviour is the same in game time). The player is
    /// turned and (for the bot) moved with the WASD keys through the real PlayerMover, so facing is one of 8 directions.
    /// </summary>
    public class CoreSceneTests : InputTestFixture
    {
        private const string SceneName = "Cube";
        private const float TimeScale = 2f;
        private const float SkillMinSeconds = 60f;
        private const float SkillMaxSeconds = 120f;

        private Keyboard keyboard;
        private Vector2Int held;

        private CubeWorld world;
        private CubeNavigator navigator;
        private CoreArena arena;
        private RunState runState;
        private CoreHud hud;
        private Rigidbody2D body;
        private PlayerStats stats;
        private Health health;
        private PlayerAttack attack;
        private PlayerMover mover;
        private List<bool> runEvents;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            held = Vector2Int.zero;
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            base.TearDown();
        }

        private IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            world = UnityEngine.Object.FindAnyObjectByType<CubeWorld>();
            navigator = UnityEngine.Object.FindAnyObjectByType<CubeNavigator>();
            arena = UnityEngine.Object.FindAnyObjectByType<CoreArena>();
            runState = UnityEngine.Object.FindAnyObjectByType<RunState>();
            hud = UnityEngine.Object.FindAnyObjectByType<CoreHud>();
            Assert.IsNotNull(world);
            Assert.IsNotNull(arena, "The Cube scene has the core arena");
            Assert.IsNotNull(runState, "The Cube scene has a RunState");
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<RunState>(FindObjectsSortMode.None).Length, "One RunState per scene");
            Assert.AreSame(runState, arena.RunState);
            Assert.IsNotNull(hud);
            Assert.AreSame(arena, hud.Arena);
            body = navigator.GetComponent<Rigidbody2D>();
            stats = navigator.GetComponent<PlayerStats>();
            health = navigator.GetComponent<Health>();
            attack = navigator.GetComponent<PlayerAttack>();
            mover = navigator.GetComponent<PlayerMover>();
            runEvents = new List<bool>();
            runState.RunEnded += v => runEvents.Add(v);
        }

        private void NoFaceEnemies()
        {
            world.GetComponent<BiologyFace>().SpawnEnemies = false;
            world.GetComponent<ChemistryFace>().SpawnEnemies = false;
            world.GetComponent<PhysicsFace>().SpawnEnemies = false;
        }

        /// <summary>A new run on a seed, enemies off, science revealed (teleport to a built face), then through its portal.</summary>
        private IEnumerator EnterThroughAPortal(int seed = 1234, bool fallback = false)
        {
            world.Rebuild(seed);
            yield return null;
            NoFaceEnemies();
            arena.UseFallbackBoss = fallback;
            FaceId face = BuiltFaces().First();
            navigator.TeleportTo(new ScreenAddress(face, 0, 0), Facing.East);
            Assert.IsTrue(world.ScienceRevealed);
            yield return new WaitForFixedUpdate();
            Assert.IsNotEmpty(arena.Portals, "Portals on the core slots");
            Assert.IsTrue(arena.Portals[0].TryEnter(navigator));
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.IsTrue(arena.FightActive, "Entering starts the fight");
            Assert.IsTrue(arena.PlayerInArena);
        }

        private IEnumerable<FaceId> BuiltFaces()
        {
            for (int f = 0; f < CubeSettings.FaceCount; f++)
                if (CubeLayout.IsLaidOut(world.Model, (FaceId)f)) yield return (FaceId)f;
        }

        private void PlacePlayer(Vector2 at)
        {
            body.position = at;
            body.linearVelocity = Vector2.zero;
            navigator.transform.position = new Vector3(at.x, at.y, navigator.transform.position.z);
            Physics2D.SyncTransforms();
        }

        /// <summary>Moves the player toward a point at speed, every physics step, until it arrives or stop() holds.</summary>
        private IEnumerator MoveTo(Vector2 to, float speed, Func<bool> stop = null, float timeout = 4f)
        {
            mover.enabled = false;
            float t = 0f;
            while (t < timeout && (stop == null || !stop()))
            {
                Vector2 d = to - body.position;
                if (d.magnitude < 0.02f) break;
                body.linearVelocity = d.normalized * Mathf.Min(speed, d.magnitude / Time.fixedDeltaTime);
                yield return new WaitForFixedUpdate();
                t += Time.fixedDeltaTime;
            }
            if (body != null) body.linearVelocity = Vector2.zero;
            mover.enabled = true;
        }

        /// <summary>
        /// Holds the WASD keys for an 8-way direction (each axis -1, 0 or 1); zero releases them. The keyboard's whole state
        /// changes in one event, so a diagonal is pressed and released cleanly (no one-key frame in between).
        /// </summary>
        private void Hold(Vector2Int dir)
        {
            dir = new Vector2Int(Math.Sign(dir.x), Math.Sign(dir.y));
            if (dir == held) return;
            SetKeys(keyboard, WasdKeys(dir));
            held = dir;
        }

        private static Vector2 Unit(Vector2Int dir) => ((Vector2)dir).normalized;

        /// <summary>Turns the player with a tap of the move keys (it steps a little), then stands still.</summary>
        private IEnumerator Face(Vector2Int dir)
        {
            Hold(dir);
            yield return WaitUntilOrGameTimeout(() => Vector2.Angle(mover.Facing, Unit(dir)) < 0.5f, 1f);
            Hold(Vector2Int.zero);
            yield return WaitUntilOrGameTimeout(() => body.linearVelocity.sqrMagnitude < 1e-6f, 1f);
            yield return new WaitForFixedUpdate();
            Assert.Less(Vector2.Angle(mover.Facing, Unit(dir)), 0.5f, "Facing set by the move keys");
        }

        private List<Particle> Of(ArenaSide home) => arena.Particles.Where(p => p != null && p.Home == home).ToList();

        private IEnumerator WaitAttackReady()
        {
            while (!attack.Ready) yield return null;
        }

        [UnityTest]
        public IEnumerator Enter_WalkingIntoThePortalOnEveryBuiltFace_Run1_StartsAnOrderedFight()
        {
            yield return Load();
            foreach (int seed in new[] { 1234, 7 })
            {
                world.Rebuild(seed);
                yield return null;
                foreach (FaceId face in BuiltFaces().ToList())
                {
                    world.Rebuild(seed);
                    yield return null;
                    NoFaceEnemies();
                    stats.MaxHealth = 999;
                    Assert.IsFalse(navigator.InCoreArena);
                    Assert.IsFalse(arena.FightActive);
                    Assert.IsEmpty(navigator.GetComponent<Inventory>().Items, "Run 1: nothing required");
                    Assert.IsFalse(runState.Ended);

                    navigator.TeleportTo(new ScreenAddress(face, 0, 0), Facing.East);
                    Assert.IsTrue(world.Layout.TryGetFace(face, out FaceLayout layout));
                    var coreScreen = new ScreenAddress(face, layout.CoreCell);
                    navigator.TeleportTo(coreScreen, Facing.East);
                    yield return new WaitForFixedUpdate();
                    ScreenModule module = world.ModuleAt(coreScreen);
                    List<CoreEntranceSlot> slots = module.ActiveCoreEntrances();
                    Assert.AreEqual(1, slots.Count);
                    CorePortal portal = slots[0].GetComponentInChildren<CorePortal>();
                    Assert.IsNotNull(portal, $"seed {seed} {face}: a portal on the active core slot");
                    CollectionAssert.Contains(arena.Portals, portal);

                    // Step in from just off the portal, on the screen-centre side.
                    Vector2 at = portal.transform.position;
                    Vector2 fromCentre = ((Vector2)world.ScreenCenter(coreScreen) - at).normalized;
                    PlacePlayer(at + fromCentre * 1.5f);
                    yield return new WaitForFixedUpdate();
                    int crossings = navigator.Crossings;
                    FaceId before = navigator.Face;
                    yield return MoveTo(at, TimeField.BasePlayerSpeed, () => navigator.InCoreArena);
                    Assert.IsTrue(navigator.InCoreArena, $"seed {seed} {face}: stepping into the portal leaves the cube");
                    Assert.AreEqual(1, portal.Entries);
                    Assert.IsTrue(arena.PlayerInArena, "The player is in the arena");
                    Assert.IsTrue(arena.FightActive, "The fight starts");

                    for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
                    yield return null;
                    Assert.AreEqual(crossings, navigator.Crossings, "No cube edge crossing from the arena");
                    Assert.AreEqual(before, navigator.Face);
                    Assert.IsTrue(arena.PlayerInArena, "Still in the arena");
                    ScreenCamera cam = UnityEngine.Object.FindAnyObjectByType<ScreenCamera>();
                    Assert.AreEqual(CoreArena.DefaultArenaScreen, cam.CurrentScreen, "The camera frames the arena's one screen");
                    Assert.Less(Vector2.Distance(cam.transform.position, arena.Centre), 1e-3f);

                    // Ordered start.
                    Assert.AreEqual(arena.ParticlesPerSide * 2, arena.Particles.Count);
                    Assert.AreEqual(8, Of(ArenaSide.Warm).Count);
                    Assert.AreEqual(8, Of(ArenaSide.Cool).Count);
                    Assert.IsTrue(Of(ArenaSide.Warm).All(p => p.Position.x < arena.MembraneX), "Warm on one side");
                    Assert.IsTrue(Of(ArenaSide.Cool).All(p => p.Position.x > arena.MembraneX), "Cool on the other");
                    Assert.AreEqual(0f, arena.Meter, 1e-6f, "The meter starts at 0");
                    Assert.IsTrue(hud.MeterVisible);
                    Assert.AreEqual("Order ↔ Entropy", CoreHud.MeterLabel);
                    Assert.IsInstanceOf<MixingPhase>(arena.CurrentPhase);
                    Assert.IsNull(hud.MessageText);
                }
            }

            // A new run brings the player back to Town and resets the arena.
            world.Rebuild(1234);
            yield return null;
            Assert.IsFalse(navigator.InCoreArena);
            Assert.AreEqual(CubeModel.StartFace, navigator.Face);
            Assert.IsFalse(arena.FightActive);
            Assert.IsFalse(hud.MeterVisible);
            Assert.IsEmpty(runEvents, "Entering raises no run-end event");
        }

        [UnityTest]
        public IEnumerator Push_KnocksAParticleAlongTheFacing_AcrossAGap_AndThroughTheDoorOnlyWhenOpen()
        {
            yield return Load();
            yield return EnterThroughAPortal();
            stats.MaxHealth = 999;
            arena.SetDemonDuties(false, false);
            MaxwellDemon demon = arena.Demon;
            Particle particle = Of(ArenaSide.Warm)[0];
            Vector2 c = arena.Centre;

            // Diagonally up-right toward the top gap from the warm side (a keyboard player faces 8 ways).
            var up = new Vector2Int(1, 1);
            Vector2 dir = Unit(up);
            Vector2 gap = arena.GapCentres[0];
            Vector2 spot = gap - new Vector2(3f, 3f);
            particle.Teleport(spot);
            PlacePlayer(spot - dir * 1.6f);
            yield return Face(up);
            particle.Teleport(spot);
            PlacePlayer(spot - dir * attack.Reach);
            yield return new WaitForFixedUpdate();
            yield return WaitAttackReady();
            attack.TryAttack();
            Assert.AreEqual(1, particle.Knocks, "The swing reaches the particle");
            Assert.Less(Vector2.Angle(particle.Body.linearVelocity, dir), 1f, "Knocked away in the facing direction");
            Assert.AreEqual(Particle.KnockSpeed, particle.Body.linearVelocity.magnitude, 0.01f);
            yield return WaitUntilOrGameTimeout(() => !particle.IsHome, 3f);
            Assert.IsFalse(particle.IsHome, "It crosses the membrane through the gap");

            // Straight at the shut door: it bounces back.
            Particle second = Of(ArenaSide.Warm)[1];
            Vector2 doorSpot = c + new Vector2(-3f, 0f);
            Vector2 toDoor = Vector2.right;
            Assert.IsFalse(demon.DoorOpen);
            second.Teleport(doorSpot);
            PlacePlayer(doorSpot - toDoor * 1.6f);
            yield return Face(Vector2Int.right);
            second.Teleport(doorSpot);
            PlacePlayer(doorSpot - toDoor * attack.Reach);
            yield return new WaitForFixedUpdate();
            yield return WaitAttackReady();
            attack.TryAttack();
            Assert.AreEqual(1, second.Knocks);
            bool crossed = false;
            float end = Time.time + 1.5f;
            while (Time.time < end)
            {
                crossed |= !second.IsHome;
                yield return new WaitForFixedUpdate();
            }
            Assert.IsFalse(crossed, "The shut door stops it");

            // Door open: it goes through.
            demon.SetDoor(true);
            second.Teleport(doorSpot);
            PlacePlayer(doorSpot - toDoor * attack.Reach);
            yield return new WaitForFixedUpdate();
            yield return WaitAttackReady();
            attack.TryAttack();
            Assert.AreEqual(2, second.Knocks);
            yield return WaitUntilOrGameTimeout(() => !second.IsHome, 2f);
            Assert.IsFalse(second.IsHome, "Through the open door");
            Assert.Greater(arena.Meter, 0f, "Mixing raises the meter");
        }

        [UnityTest]
        public IEnumerator Restore_AnIdlePlayer_LosesGroundAsTheDemonBringsStraysHome()
        {
            yield return Load();
            yield return EnterThroughAPortal();
            stats.MaxHealth = 999; // pulses stay on: the idle player is hit, but survives the 20 s
            List<Particle> warm = Of(ArenaSide.Warm);
            Vector2 c = arena.Centre;
            for (int i = 0; i < 6; i++) warm[i].Teleport(c + new Vector2(1.6f + (i % 2) * 4.5f, -3.5f + 1.4f * (i / 2) + (i % 2) * 0.7f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            float start = arena.Meter;
            Assert.AreEqual(0.75f, start, 1e-4f, "Six strays out of sixteen");
            Assert.IsTrue(arena.FightActive);

            Time.timeScale = TimeScale;
            var readings = new List<float> { start };
            for (int s = 0; s < 4; s++)
            {
                yield return new WaitForSeconds(5f);
                readings.Add(arena.Meter);
            }
            Time.timeScale = 1f;
            Debug.Log($"Restore: meter over 20 s idle: {string.Join(", ", readings.Select(r => r.ToString("0.00")))}; restored {arena.Demon.Restored}");
            Assert.GreaterOrEqual(arena.Demon.Restored, 3, "About one particle every 3–4 s");
            Assert.LessOrEqual(arena.Meter, start - 3f / 8f, "The meter falls while the player idles");
            Assert.IsTrue(arena.FightActive);
            Assert.Greater(arena.Demon.PulsesFired, 0, "The Demon kept firing");
            Assert.IsEmpty(runEvents);
        }

        [UnityTest]
        public IEnumerator Win_ReachingTheThreshold_DefeatsTheDemon_RaisesVictoryOnce_AndIgnoresLaterEvents()
        {
            yield return Load();
            yield return EnterThroughAPortal();
            stats.MaxHealth = 999;
            // RunEnded comes last, after the arena has settled its outcome and raised FightEnded.
            var order = new List<string>();
            arena.FightEnded += v => order.Add($"fight {v}");
            runState.RunEnded += v => order.Add($"run {v} {arena.Outcome} {hud.MessageText != null}");
            List<Particle> warm = Of(ArenaSide.Warm);
            Vector2 c = arena.Centre;
            for (int i = 0; i < 6; i++) warm[i].Teleport(c + new Vector2(2f + (i % 3) * 1.5f, -3.6f + 0.9f * (i / 3)));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(arena.FightActive, "Six across is not yet enough");
            Assert.IsEmpty(runEvents);
            warm[6].Teleport(c + new Vector2(6.5f, 3.6f));
            yield return WaitUntilOrGameTimeout(() => !arena.FightActive, 1f);

            Assert.AreEqual(CoreOutcome.Victory, arena.Outcome);
            CollectionAssert.AreEqual(new[] { true }, runEvents, "RunEnded(true) once");
            CollectionAssert.AreEqual(new[] { "fight True", "run True Victory True" }, order, "EndRun is the last step");
            Assert.IsTrue(runState.Ended);
            Assert.IsTrue(runState.Victory);
            Assert.IsTrue(arena.Demon.Defeated, "The Demon is defeated");
            Assert.AreEqual(CoreHud.VictoryMessage, hud.MessageText);
            StringAssert.Contains("Entropy wins! The Partition cracks", hud.MessageText);
            Assert.AreEqual(1f, hud.MeterValue, 1e-6f);
            Assert.IsEmpty(arena.Pulses, "No pulses after the win");

            // Further events are ignored.
            health.TakeDamage(999f, null);
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(health.IsDead);
            Assert.IsFalse(runState.EndRun(false));
            Assert.IsTrue(arena.Portals[0].TryEnter(navigator));
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(arena.FightActive, "No second fight in a finished run");
            CollectionAssert.AreEqual(new[] { true }, runEvents);
            Assert.AreEqual(CoreOutcome.Victory, arena.Outcome);

            // A new run resets the run state and the arena.
            health.ResetHealth();
            world.Rebuild(1234);
            yield return null;
            Assert.IsFalse(runState.Ended);
            Assert.AreEqual(CoreOutcome.None, arena.Outcome);
            Assert.IsNull(hud.MessageText);
            Assert.IsFalse(navigator.InCoreArena);
        }

        [UnityTest]
        public IEnumerator Pulses_AreTelegraphed_Avoidable_AndDamageUnderTheCombatRules()
        {
            yield return Load();
            yield return EnterThroughAPortal();
            MaxwellDemon demon = arena.Demon;
            arena.SetDemonDuties(false, false);
            Vector2 c = arena.Centre;
            float max = health.Max;
            Assert.AreEqual(0, stats.Defence);

            // A pulse straight at the player: base damage, through Health. Close to the Demon, so the second pulse
            // arrives well inside the invulnerability window.
            PlacePlayer(c + new Vector2(-1.8f, -2f));
            yield return new WaitForFixedUpdate();
            OrderPulse pulse = demon.FirePulse((Vector2)navigator.transform.position - demon.Position);
            yield return WaitUntilOrGameTimeout(() => pulse == null, 3f);
            Assert.AreEqual(CombatMath.Incoming(MaxwellDemon.DefaultPulseDamage, 0), max - health.Current, 1e-4f, "A hit");

            // Within the invulnerability window: no damage.
            Assert.IsTrue(health.IsInvulnerable);
            float before = health.Current;
            OrderPulse second = demon.FirePulse((Vector2)navigator.transform.position - demon.Position);
            yield return WaitUntilOrGameTimeout(() => second == null, 3f);
            Assert.AreEqual(before, health.Current, 1e-4f, "Invulnerability applies");

            // Defence applies.
            yield return new WaitForSeconds(health.InvulnerableSeconds + 0.1f);
            stats.Defence = 1;
            demon.PulseDamage = 3f;
            before = health.Current;
            OrderPulse third = demon.FirePulse((Vector2)navigator.transform.position - demon.Position);
            yield return WaitUntilOrGameTimeout(() => third == null, 3f);
            Assert.AreEqual(CombatMath.Incoming(3f, 1), before - health.Current, 1e-4f, "Defence applies");
            stats.Defence = 0;
            demon.PulseDamage = MaxwellDemon.DefaultPulseDamage;
            stats.MaxHealth = 999;
            health.ResetHealth();

            // The Demon's own pulses: a telegraph aimed where the player stands, then a pulse along it.
            yield return new WaitForSeconds(health.InvulnerableSeconds + 0.1f);
            PlacePlayer(c + new Vector2(-4f, 2.5f));
            demon.Firing = true;
            int fired = demon.PulsesFired;
            yield return WaitUntilOrGameTimeout(() => demon.Telegraphing, MaxwellDemon.PulseInterval + 1f);
            Assert.IsTrue(demon.Telegraphing, "A telegraph first");
            Assert.AreEqual(fired, demon.PulsesFired, "No pulse during the telegraph");
            Vector2 toPlayer = ((Vector2)navigator.transform.position - demon.Position).normalized;
            Assert.Less(Vector2.Angle(demon.TelegraphAim, toPlayer), 10f, "Aimed at the player");

            // Step off the line: the pulse misses.
            before = health.Current;
            Vector2 side = Vector2.Perpendicular(demon.TelegraphAim);
            if ((body.position + side * 2f).y > c.y + CoreArena.InnerHalf.y - 0.5f) side = -side;
            yield return MoveTo(body.position + side * 2f, TimeField.BasePlayerSpeed);
            yield return WaitUntilOrGameTimeout(() => demon.PulsesFired > fired, MaxwellDemon.TelegraphSeconds + 0.5f);
            Assert.AreEqual(fired + 1, demon.PulsesFired, "Then the pulse");
            yield return WaitUntilOrGameTimeout(() => arena.Pulses.Count == 0, 3f);
            Assert.AreEqual(before, health.Current, 1e-4f, "A player who steps aside is not hit");
            Assert.IsTrue(arena.FightActive);
        }

        [UnityTest]
        public IEnumerator Lose_DyingInTheArena_RaisesDefeatOnce_AndIgnoresLaterEvents()
        {
            yield return Load();
            yield return EnterThroughAPortal();
            stats.MaxHealth = 1;
            Assert.AreEqual(1f, health.Current, 1e-4f);
            var order = new List<string>();
            arena.FightEnded += v => order.Add($"fight {v}");
            runState.RunEnded += v => order.Add($"run {v} {arena.Outcome}");
            // Standing still: the first pulse lands.
            yield return WaitUntilOrGameTimeout(() => health.IsDead, MaxwellDemon.FirstPulseDelay + MaxwellDemon.TelegraphSeconds + 3f);
            Assert.IsTrue(health.IsDead, "An order pulse finishes the player");
            yield return null;
            Assert.AreEqual(CoreOutcome.Defeat, arena.Outcome);
            CollectionAssert.AreEqual(new[] { false }, runEvents, "RunEnded(false) once");
            CollectionAssert.AreEqual(new[] { "fight False", "run False Defeat" }, order, "EndRun is the last step");
            Assert.IsTrue(runState.Ended);
            Assert.IsFalse(runState.Victory);
            Assert.AreEqual(CoreHud.DefeatMessage, hud.MessageText);
            Assert.IsFalse(arena.FightActive);

            // Further events are ignored: mixing the arena now does nothing.
            Vector2 c = arena.Centre;
            List<Particle> warm = Of(ArenaSide.Warm);
            for (int i = 0; i < 8; i++) warm[i].Teleport(c + new Vector2(2f + (i % 4) * 1.3f, -3f + 2f * (i / 4)));
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            Assert.AreEqual(CoreOutcome.Defeat, arena.Outcome);
            Assert.IsFalse(runState.EndRun(true));
            CollectionAssert.AreEqual(new[] { false }, runEvents);
        }

        [UnityTest]
        public IEnumerator Fallback_HittingTheDemonNTimesWins_AndDyingLoses_ThroughTheSameEvent()
        {
            yield return Load();
            yield return EnterThroughAPortal(fallback: true);
            stats.MaxHealth = 999;
            var boss = arena.CurrentPhase as FallbackBoss;
            Assert.IsNotNull(boss, "The fallback setting runs the simpler boss");
            Assert.IsFalse(arena.Demon.Restoring);
            Assert.IsTrue(arena.Demon.Firing, "It still fires order pulses");
            Assert.IsTrue(hud.MeterVisible);

            yield return Face(Vector2Int.right);
            for (int i = 0; i < boss.HitsNeeded && arena.FightActive; i++)
            {
                yield return WaitAttackReady();
                health.ResetHealth();
                Vector2 demonAt = arena.Demon.Position;
                PlacePlayer(demonAt + Vector2.left * attack.Reach);
                yield return new WaitForFixedUpdate();
                int hits = arena.Demon.HitsTaken;
                attack.TryAttack();
                Assert.AreEqual(hits + 1, arena.Demon.HitsTaken, $"swing {i} lands on the Demon");
                yield return new WaitForFixedUpdate();
            }
            yield return WaitUntilOrGameTimeout(() => !arena.FightActive, 1f);
            Assert.AreEqual(CoreOutcome.Victory, arena.Outcome);
            CollectionAssert.AreEqual(new[] { true }, runEvents, "Victory through RunState");
            Assert.AreEqual(CoreHud.VictoryMessage, hud.MessageText);

            // A new run, fallback still on: dying loses through the same event.
            yield return EnterThroughAPortal(fallback: true);
            Assert.IsInstanceOf<FallbackBoss>(arena.CurrentPhase);
            stats.MaxHealth = 1;
            health.ResetHealth();
            PlacePlayer(arena.Centre + new Vector2(-5f, 0f));
            yield return WaitUntilOrGameTimeout(() => health.IsDead, MaxwellDemon.FirstPulseDelay + MaxwellDemon.TelegraphSeconds + 3f);
            Assert.IsTrue(health.IsDead);
            yield return null;
            Assert.AreEqual(CoreOutcome.Defeat, arena.Outcome);
            CollectionAssert.AreEqual(new[] { true, false }, runEvents);
        }

        // ---- Replay and debug tools ----

        [UnityTest]
        public IEnumerator Drift_TheSameSeedDriftsIdentically_AfterAReroll()
        {
            yield return Load();
            const int steps = 150;
            List<Vector2> first = null;
            foreach (int seed in new[] { 1234, 99, 1234 })
            {
                yield return EnterThroughAPortal(seed);
                arena.SetDemonDuties(false, false);
                Assert.AreEqual(1, arena.FightIndex, "The first fight of the run, whatever came before");
                while (arena.FightSteps < steps) yield return new WaitForFixedUpdate();
                Assert.AreEqual(steps, arena.FightSteps);
                List<Vector2> positions = arena.Particles.Select(p => p.Position).ToList();
                if (seed == 99)
                {
                    Assert.IsTrue(positions.Zip(first, (a, b) => Vector2.Distance(a, b)).Any(d => d > 1e-3f), "Another seed drifts differently");
                }
                else if (first == null)
                {
                    first = positions;
                    Assert.IsTrue(positions.Select((p, i) => Vector2.Distance(p, arena.OrderedSpot(i < 8 ? ArenaSide.Warm : ArenaSide.Cool, i % 8)))
                        .Any(d => d > 0.05f), "The particles drifted");
                }
                else
                {
                    for (int i = 0; i < positions.Count; i++)
                        Assert.Less(Vector2.Distance(positions[i], first[i]), 1e-3f, $"particle {i} replays its drift");
                }
            }
            Assert.AreNotEqual(CoreArena.DriftSeed(1234, 1, 0), CoreArena.DriftSeed(1234, 1, 1));
            Assert.AreNotEqual(CoreArena.DriftSeed(1234, 1, 0), CoreArena.DriftSeed(1234, 2, 0));
            Assert.AreNotEqual(CoreArena.DriftSeed(1234, 1, 256), CoreArena.DriftSeed(1234, 2, 0), "No bit collisions");
            Assert.AreEqual(CoreArena.DriftSeed(7, 3, 5), CoreArena.DriftSeed(7, 3, 5));
        }

        [UnityTest]
        public IEnumerator DebugTools_KnowThePlayerIsInTheCoreArena()
        {
            yield return Load();
            var debug = UnityEngine.Object.FindAnyObjectByType<CubeDebug>();
            Assert.IsNotNull(debug);
            Assert.AreSame(arena, debug.CoreArena);
            yield return EnterThroughAPortal();
            stats.MaxHealth = 999;
            Assert.IsTrue(navigator.InCoreArena);
            Assert.IsTrue(debug.PlayerInCoreArena);
            StringAssert.Contains(CubeDebug.CoreArenaLabel, debug.OverlayText, "F1 shows the core arena");
            StringAssert.DoesNotContain("Cell (", debug.OverlayText, "not a face cell");
            Assert.Less(Vector2.Distance(arena.Centre, debug.PlayerScreenCentre()), 1e-4f);
            List<NpcTalker> npcs = debug.SpawnNpcs();
            Assert.IsNotEmpty(npcs);
            foreach (NpcTalker npc in npcs) Assert.IsTrue(arena.Contains(npc.transform.position), "F3 spawns in the arena");
            Enemy enemy = debug.SpawnEnemy();
            Assert.IsTrue(arena.Contains(enemy.transform.position), "F7 spawns in the arena");
            Rect bounds = enemy.GetComponent<EnemyBrain>().Bounds;
            Assert.IsTrue(arena.Contains(bounds.min) && arena.Contains(bounds.max), "and keeps it there");

            navigator.TeleportTo(world.Model.StartScreen, Facing.East);
            Assert.IsFalse(debug.PlayerInCoreArena, "Back on a face");
            StringAssert.Contains("Cell (", debug.OverlayText);
            StringAssert.DoesNotContain(CubeDebug.CoreArenaLabel, debug.OverlayText);
        }

        // ---- The keyboard-player bot ----

        /// <summary>
        /// A scripted keyboard player with the starting kit: it holds the WASD keys (8 directions, the real PlayerMover at
        /// base speed, so it faces only where it last walked) and swings with PlayerAttack. It picks a particle still at home
        /// on its side and one of the 8 directions whose straight-line knock (bouncing off the walls and the membrane) runs
        /// through a membrane gap, or comes nearest one; walks to a spot behind the particle, walks in along that direction
        /// and swings. It steps off the line of a telegraphed or incoming order pulse, again in one of 8 directions.
        /// </summary>
        private sealed class Bot
        {
            private const float Deadband = 0.15f;
            private const float Behind = 1.7f;

            private static readonly Vector2Int[] Dirs =
            {
                new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(0, 1), new Vector2Int(-1, 1),
                new Vector2Int(-1, 0), new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1),
            };

            private readonly CoreSceneTests t;
            private readonly Dictionary<Particle, float> skip = new Dictionary<Particle, float>();
            private Particle target;
            private Vector2Int dir;
            private float targetSince;

            public int Swings;
            public int Dodges;

            public Bot(CoreSceneTests tests) => t = tests;

            private CoreArena Arena => t.arena;
            private Vector2 Pos => t.body.position;

            /// <summary>The keys to hold this frame, and whether to swing now.</summary>
            public Vector2Int Decide(out bool swing)
            {
                swing = false;
                if (Dodge(out Vector2Int away))
                {
                    Dodges++;
                    return away;
                }

                ArenaSide side = Arena.SideOf(Pos);
                bool stale = Time.time - targetSince > 4f;
                if (target == null || !target.IsHome || target.Side != side || target.Knocked || stale)
                {
                    if (target != null && stale) skip[target] = Time.time + 4f;
                    Choose(side);
                }
                if (target == null) return CrossOver(side);

                Vector2 p = target.Position;
                Vector2 d = Unit(dir);
                Vector2 rel = Pos - p;
                float behind = Vector2.Dot(rel, -d);
                float lateral = Mathf.Abs(rel.x * d.y - rel.y * d.x);
                if (lateral < 0.25f && behind > 0.6f && behind < Behind + 0.6f)
                {
                    if (behind > t.attack.Reach + 0.15f) return dir; // walk in along the line: this also faces it
                    if (Vector2.Angle(t.mover.Facing, d) > 0.5f) return Steer(p - d * Behind, p, d); // back off and come again
                    if (!t.attack.Ready) return Vector2Int.zero;
                    // Swing on the move, still holding the keys, as a keyboard player does.
                    swing = true;
                    Swings++;
                    skip[target] = Time.time + 1f;
                    target = null;
                    return dir;
                }
                return Steer(p - d * Behind, p, d);
            }

            /// <summary>Keys toward a goal (per-axis dead band), going round the target particle if it is in the way.</summary>
            private Vector2Int Steer(Vector2 goal, Vector2 p, Vector2 d)
            {
                if (SegmentDistance(p, Pos, goal) < 0.85f && Vector2.Distance(Pos, goal) > 0.3f)
                {
                    Vector2 perp = Vector2.Perpendicular(d);
                    if (Vector2.Dot(Pos - p, perp) < 0f) perp = -perp;
                    goal = p + perp * 1.3f - d * 0.4f;
                }
                return Keys(goal);
            }

            private Vector2Int Keys(Vector2 goal)
            {
                Vector2 delta = goal - Pos;
                return new Vector2Int(Mathf.Abs(delta.x) > Deadband ? Math.Sign(delta.x) : 0,
                    Mathf.Abs(delta.y) > Deadband ? Math.Sign(delta.y) : 0);
            }

            private static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
            {
                Vector2 ab = b - a;
                float k = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
                return Vector2.Distance(point, a + ab * k);
            }

            private bool Valid(Vector2 spot, ArenaSide side)
            {
                Rect r = Arena.Inner;
                const float m = 0.5f;
                if (spot.x < r.xMin + m || spot.x > r.xMax - m || spot.y < r.yMin + m || spot.y > r.yMax - m) return false;
                return Arena.SideOf(spot) == side && Mathf.Abs(spot.x - Arena.MembraneX) > 0.65f;
            }

            private void Choose(ArenaSide side)
            {
                target = null;
                targetSince = Time.time;
                float best = float.MaxValue;
                foreach (Particle p in Arena.Particles)
                {
                    if (p == null || !p.IsHome || p.Side != side || p.Knocked || p.Nudged) continue;
                    if (skip.TryGetValue(p, out float until) && Time.time < until) continue;
                    foreach (Vector2Int k in Dirs)
                    {
                        Vector2 d = Unit(k);
                        if (!Valid(p.Position - d * Behind, side) || !Valid(p.Position - d * t.attack.Reach, side)) continue;
                        float miss = Simulate(p.Position, d);
                        float score = miss * 4f + (miss > 0f ? 3f : 0f) + 0.5f * Vector2.Distance(Pos, p.Position - d * Behind);
                        if (score >= best) continue;
                        best = score;
                        target = p;
                        dir = k;
                    }
                }
            }

            /// <summary>
            /// Traces a knock from p along d (KnockSpeed ÷ damping of travel, less a margin), bouncing off the walls and the
            /// membrane (the door counts as shut unless it is open). 0 if it crosses a gap, else how near it came to one.
            /// </summary>
            private float Simulate(Vector2 p, Vector2 d)
            {
                const float step = 0.05f, r = Particle.Radius;
                float budget = Particle.KnockSpeed / Particle.Damping * 0.8f;
                Rect inner = Arena.Inner;
                float mx = Arena.MembraneX, cy = Arena.Centre.y;
                float solidHalf = Arena.MembraneHalfSpan + r;
                float half = CoreArena.MembraneThickness / 2f + r;
                bool doorOpen = Arena.Demon.DoorOpen;
                Vector2[] gaps = Arena.GapCentres;
                float miss = float.MaxValue;
                float side = Mathf.Sign(p.x - mx);
                for (float s = 0f; s < budget; s += step)
                {
                    p += d * step;
                    if (p.x < inner.xMin + r || p.x > inner.xMax - r) d.x = -d.x;
                    if (p.y < inner.yMin + r || p.y > inner.yMax - r) d.y = -d.y;
                    float dy = Mathf.Abs(p.y - cy);
                    bool solid = dy < solidHalf && !(doorOpen && dy < CoreArena.DoorHalfHeight - r);
                    if (Mathf.Abs(p.x - mx) < half && solid && Mathf.Sign(d.x) == -side) d.x = -d.x;
                    if (Mathf.Sign(p.x - mx) != side) return 0f;
                    foreach (Vector2 g in gaps) miss = Mathf.Min(miss, Vector2.Distance(p, g));
                }
                return miss;
            }

            /// <summary>No home particle left on this side: walk through the nearer gap to the other side.</summary>
            private Vector2Int CrossOver(ArenaSide side)
            {
                Vector2[] gaps = Arena.GapCentres;
                Vector2 gap = Vector2.Distance(Pos, gaps[0]) <= Vector2.Distance(Pos, gaps[1]) ? gaps[0] : gaps[1];
                float s = side == ArenaSide.Warm ? 1f : -1f;
                Vector2 before = gap - new Vector2(s * 1.2f, 0f);
                Vector2 after = gap + new Vector2(s * 1.2f, 0f);
                bool lined = Mathf.Abs(Pos.y - gap.y) < 0.25f;
                return Keys(lined ? after : before);
            }

            private bool Dodge(out Vector2Int away)
            {
                away = Vector2Int.zero;
                foreach (OrderPulse pulse in Arena.Pulses)
                {
                    Vector2 rel = Pos - pulse.Position;
                    Vector2 v = pulse.Velocity;
                    float time = Vector2.Dot(rel, v) / Mathf.Max(1e-4f, v.sqrMagnitude);
                    if (time < 0f || time > 0.9f) continue;
                    Vector2 miss = rel - v * time;
                    if (miss.magnitude > 1.1f) continue;
                    away = Away(miss.magnitude > 0.05f ? miss : Vector2.Perpendicular(v));
                    return true;
                }
                MaxwellDemon demon = Arena.Demon;
                if (demon.Telegraphing)
                {
                    Vector2 rel = Pos - demon.Position;
                    float along = Vector2.Dot(rel, demon.TelegraphAim);
                    Vector2 lateral = rel - demon.TelegraphAim * along;
                    if (along > -0.5f && lateral.magnitude < 1.1f)
                    {
                        away = Away(lateral.magnitude > 0.05f ? lateral : Vector2.Perpendicular(demon.TelegraphAim));
                        return true;
                    }
                }
                return false;
            }

            /// <summary>The 8-way direction nearest to want that does not run into a wall.</summary>
            private Vector2Int Away(Vector2 want)
            {
                Rect r = Arena.Inner;
                Vector2Int best = Vector2Int.zero;
                float bestDot = float.MinValue;
                foreach (Vector2Int k in Dirs)
                {
                    Vector2 ahead = Pos + Unit(k) * 1.2f;
                    if (ahead.x < r.xMin + 0.5f || ahead.x > r.xMax - 0.5f || ahead.y < r.yMin + 0.5f || ahead.y > r.yMax - 0.5f) continue;
                    float dot = Vector2.Dot(Unit(k), want.normalized);
                    if (dot <= bestDot) continue;
                    bestDot = dot;
                    best = k;
                }
                return best;
            }
        }

        [UnityTest]
        public IEnumerator SkillCheck_AKeyboardPlayerWithTheStartingKit_WinsIn60To120Seconds([Values(1234, 7, 42)] int seed)
        {
            yield return Load();
            yield return EnterThroughAPortal(seed);
            Assert.AreEqual(PlayerStats.DefaultMaxHealth, stats.MaxHealth, "Base stats");
            Assert.AreEqual(1, stats.Power);
            Assert.AreEqual(1, stats.Speed);
            Assert.AreEqual(0, stats.Defence);
            Assert.IsNull(attack.EquippedItem, "Bare hands");
            Assert.IsEmpty(navigator.GetComponent<Inventory>().Items);
            Assert.IsInstanceOf<MixingPhase>(arena.CurrentPhase);
            Assert.IsTrue(arena.Demon.Firing, "Order pulses on");
            Assert.IsTrue(arena.Demon.Restoring);

            var bot = new Bot(this);
            float start = Time.time;
            float peak = 0f;
            Time.timeScale = TimeScale;
            while (arena.FightActive && Time.time - start < SkillMaxSeconds + 30f)
            {
                Vector2Int keys = bot.Decide(out bool swing);
                Hold(keys);
                if (swing) attack.TryAttack();
                peak = Mathf.Max(peak, arena.Meter);
                yield return null;
            }
            Hold(Vector2Int.zero);
            Time.timeScale = 1f;
            float took = Time.time - start;
            Debug.Log($"Skill check (seed {seed}): outcome {arena.Outcome} after {took:0.0} s, {bot.Swings} swings, {bot.Dodges} dodge frames, " +
                      $"peak meter {peak:0.00}, Demon restored {arena.Demon.Restored} and fired {arena.Demon.PulsesFired}, health {health.Current}/{health.Max}");
            Assert.AreEqual(CoreOutcome.Victory, arena.Outcome, $"The bot wins (meter peaked at {peak:0.00}, health {health.Current})");
            Assert.GreaterOrEqual(took, SkillMinSeconds, "Steady pushing takes a while");
            Assert.LessOrEqual(took, SkillMaxSeconds, "but wins in time");
            Assert.IsFalse(health.IsDead);
            CollectionAssert.AreEqual(new[] { true }, runEvents);
        }
    }
}
