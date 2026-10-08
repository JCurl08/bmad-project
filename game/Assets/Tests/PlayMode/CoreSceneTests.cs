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
    /// scripted keyboard player (8 directions, starting kit, pulses on, no boss benefits) wins in 60–120 s; and each boss
    /// benefit (4.8) measurably changes its arena parameter.
    /// Long waits run at TimeScale (physics steps are unchanged, so behaviour is the same in game time). The player is
    /// turned and (for the bot) moved with the WASD keys through the real PlayerMover, so facing is one of 8 directions.
    /// </summary>
    public class CoreSceneTests : InputTestFixture
    {
        private const string SceneName = "Cube";
        private const float TimeScale = 2f;
        private const float SkillMinSeconds = 60f;
        private const float SkillMaxSeconds = 120f;

        /// <summary>Lengths below are in the first 16x10 screens' units, scaled like the game's (4.1).</summary>
        private const float K = ScreenMath.ContentScale;

        /// <summary>
        /// Skill-check fights start on a multiple of this game time: the next multiple at least one spacing past the
        /// current fixed time. Game code times short windows against Time.time (a knocked particle's, the player's
        /// invulnerability), and whether a window's last physics step still counts depends on float rounding at the
        /// absolute time, so the same fight started at another game time can play out differently. Aligning the start
        /// takes out the jitter of the tests before it (their frame-paced waits shift the game time by a few seconds).
        /// Per-seed win times reproduce only for the same test history (the same fixtures before this one, in the same
        /// order, and the same seed order); another history can land on another multiple and give other times.
        /// </summary>
        private const float SkillStartSpacing = 400f;

        /// <summary>The aligned start for a skill-check fight: the next multiple of SkillStartSpacing at least one spacing ahead.</summary>
        private static float NextSkillStart(float now) => (Mathf.Floor(now / SkillStartSpacing) + 2f) * SkillStartSpacing;

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
            world.GetComponent<MathFace>().SpawnEnemies = false;
            world.GetComponent<EarthFace>().SpawnEnemies = false;
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

        /// <summary>
        /// A new run on a seed, straight into the fight from Town with the science faces unrevealed: the physics world holds
        /// only Town, the player and the arena, so a timing check measures the fight alone. (The fight's outcome is
        /// sensitive to every physics body in the scene, however far away: any face content changes the solver's order.)
        /// Portal entry itself is covered by Enter_WalkingIntoThePortalOnEveryBuiltFace.
        /// </summary>
        private IEnumerator EnterFightFromTown(int seed)
        {
            yield return StartFightFromTown(seed);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.IsTrue(arena.FightActive, "Still fighting a step later");
        }

        /// <summary>
        /// As EnterFightFromTown, but returns in the physics phase right after the controlled entry, before the fight's
        /// first step runs, so a caller that then waits only on physics steps (the skill-check bot) sees every fight step
        /// and no rendered frame in between. With alignStart, the fight starts exactly at the next aligned game time
        /// (NextSkillStart, fixed-step time): the wait runs fast-forward in physics steps, then Town's NPCs are placed
        /// afresh in that same step, so the physics world at the first fight step does not depend on frame timing.
        /// </summary>
        private IEnumerator StartFightFromTown(int seed, bool alignStart = false)
        {
            world.Rebuild(seed);
            yield return null;
            NoFaceEnemies();
            arena.UseFallbackBoss = false;
            Assert.IsFalse(world.ScienceRevealed, "No face content in the physics world");
            yield return new WaitForFixedUpdate();
            if (alignStart)
            {
                float startAt = NextSkillStart(Time.fixedTime);
                Time.timeScale = 100f;
                while (Time.fixedTime < startAt - Time.fixedDeltaTime / 2f) yield return new WaitForFixedUpdate();
                Time.timeScale = 1f;
                // The NPCs have wandered since the rebuild for however many steps frames gave them: place them again now.
                UnityEngine.Object.FindAnyObjectByType<TownPopulation>().Populate();
            }
            Assert.IsTrue(arena.Enter(navigator));
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
                if (d.magnitude < 0.02f * K) break;
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
        private void Hold(Vector2Int dir) => Hold(dir, false);

        /// <summary>
        /// As Hold(dir); with now, the input system processes the change at once (not at the next rendered frame), so a
        /// caller in the physics phase has the keys read by the very next physics step (PlayerMover reads them in
        /// FixedUpdate). The skill-check bot uses this to act once per physics step, independent of frame timing.
        /// </summary>
        private void Hold(Vector2Int dir, bool now)
        {
            dir = new Vector2Int(Math.Sign(dir.x), Math.Sign(dir.y));
            if (dir == held) return;
            SetKeys(keyboard, WasdKeys(dir));
            held = dir;
            if (now) InputSystem.Update();
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
                    PlacePlayer(at + fromCentre * 1.5f * K);
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
            Vector2 spot = gap - new Vector2(3f, 3f) * K;
            particle.Teleport(spot);
            PlacePlayer(spot - dir * 1.6f * K);
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
            Vector2 doorSpot = c + new Vector2(-3f, 0f) * K;
            Vector2 toDoor = Vector2.right;
            Assert.IsFalse(demon.DoorOpen);
            second.Teleport(doorSpot);
            PlacePlayer(doorSpot - toDoor * 1.6f * K);
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
            for (int i = 0; i < 6; i++) warm[i].Teleport(c + new Vector2(1.6f + (i % 2) * 4.5f, -3.5f + 1.4f * (i / 2) + (i % 2) * 0.7f) * K);
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
            for (int i = 0; i < 6; i++) warm[i].Teleport(c + new Vector2(2f + (i % 3) * 1.5f, -3.6f + 0.9f * (i / 3)) * K);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(arena.FightActive, "Six across is not yet enough");
            Assert.IsEmpty(runEvents);
            warm[6].Teleport(c + new Vector2(6.5f, 3.6f) * K);
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
            Assert.IsFalse(arena.ExitAvailable, "No exit once the fight is won (the run ends anyway)");
            Assert.IsFalse(Physics2D.GetIgnoreCollision(arena.ExitBlocker, navigator.GetComponent<Collider2D>()), "The doorway is shut");
            Assert.IsFalse(arena.ExitDoor.activeSelf, "The exit door is gone");
            Assert.IsFalse(arena.LeaveThroughExit(), "and cannot be used");
            Assert.IsFalse(hud.ExitHintVisible);

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
            Assert.IsTrue(arena.ExitDoor.activeSelf, "A new run brings the exit door back");
        }

        [UnityTest]
        public IEnumerator Exit_WalkingIntoTheDoorMidFight_ReturnsToThePortalsScreen_ResetsTheFight_AndEndsNoRun()
        {
            yield return Load();
            yield return EnterThroughAPortal();
            stats.MaxHealth = 999;
            CorePortal portal = arena.Portals[0];
            Assert.IsTrue(world.TryScreenAt(portal.transform.position, out ScreenAddress portalScreen), "The portal sits on a face");
            Assert.AreEqual(portalScreen, arena.ExitScreen, "The arena remembers the portal's screen");
            int fights = arena.FightIndex;

            // A visible doorway on the arena's edge: cut into the west wall, its recess outside the playfield.
            Assert.IsTrue(arena.ExitAvailable);
            Assert.IsTrue(arena.ExitDoor.activeSelf, "The exit door shows during the fight");
            Assert.IsNotEmpty(arena.ExitDoor.GetComponentsInChildren<SpriteRenderer>());
            Assert.Less(arena.ExitDoorCentre.x, arena.Inner.xMin, "In the wall, outside the walkable inside");
            Assert.LessOrEqual(arena.Recess.xMax, arena.Inner.xMin + 1e-4f, "The recess lies outside the playfield");
            Assert.Less(arena.ExitZone.xMax, arena.Inner.xMin - 0.4f * K, "Leaving takes walking deep into the recess");
            Assert.Greater(arena.ExitZone.yMin, arena.EntryPoint.y + 2f * K, "Far from where the player arrives");
            Assert.IsTrue(arena.ExitBlocker.enabled && !arena.ExitBlocker.isTrigger, "A solid block fills the doorway");
            Assert.IsTrue(arena.Particles.All(p => p.GetComponent<Collider2D>() == null ||
                                                   !Physics2D.GetIgnoreCollision(arena.ExitBlocker, p.GetComponent<Collider2D>())),
                "Particles are stopped by it");
            Assert.IsFalse(arena.DoorwayOpen, "Shut until the player walks straight in");
            Assert.IsFalse(navigator.GetComponents<Collider2D>().Any(c => Physics2D.GetIgnoreCollision(arena.ExitBlocker, c)),
                "The whole wall stops the player too, until then");
            Assert.IsTrue(hud.ExitHintVisible, "The HUD says how to leave");

            // A particle knocked at the doorway bounces back: it never gets into the recess.
            Particle stray = arena.Particles.First(p => p.Home == ArenaSide.Warm);
            stray.Teleport(new Vector2(arena.Inner.xMin + 0.6f * K, arena.Centre.y + CoreArena.ExitOffsetY));
            stray.GetComponent<Rigidbody2D>().linearVelocity = new Vector2(-8f, 0f) * K;
            for (int i = 0; i < 40; i++)
            {
                yield return new WaitForFixedUpdate();
                Assert.Greater(stray.Position.x, arena.Inner.xMin - 0.05f * K, "No particle enters the recess");
            }

            // Walk west into the door with the move keys.
            Vector2 start = new Vector2(arena.Inner.xMin + 1.6f * K, arena.Centre.y + CoreArena.ExitOffsetY);
            PlacePlayer(start);
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(navigator.InCoreArena, "Standing near the door is not leaving");
            Hold(new Vector2Int(-1, 0));
            yield return WaitUntilOrGameTimeout(() => !navigator.InCoreArena, 3f);
            Hold(Vector2Int.zero);

            Assert.IsFalse(navigator.InCoreArena, "Walked out");
            Assert.AreEqual(portalScreen.Face, navigator.Face, "Back on the portal's face");
            Assert.AreEqual(portalScreen, navigator.Current, "on the portal's screen");
            Assert.AreEqual(1, arena.Exits);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.IsFalse(arena.FightActive, "The fight is abandoned");
            Assert.IsEmpty(arena.Particles, "and cleared");
            Assert.IsEmpty(runEvents, "No run end");
            Assert.IsFalse(runState.Ended);
            Assert.AreEqual(CoreOutcome.None, arena.Outcome);
            Assert.IsFalse(arena.LeaveThroughExit(), "Nothing to leave from outside the arena");

            // Back in through the same portal: a fresh, ordered fight.
            Assert.IsTrue(portal.TryEnter(navigator));
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.IsTrue(arena.FightActive);
            Assert.AreEqual(fights + 1, arena.FightIndex, "A new fight");
            Assert.AreEqual(arena.ParticlesPerSide * 2, arena.Particles.Count);
            Assert.IsTrue(arena.Particles.All(p => p.IsHome), "Ordered again");
            Assert.IsTrue(arena.LeaveThroughExit(), "The exit works again");
            Assert.AreEqual(portalScreen, navigator.Current);
            Assert.IsEmpty(runEvents);

            // A controlled entry (no portal, as the debug tools and the skill check use) has the same door; it leads back
            // to the screen the player entered from.
            ScreenAddress from = navigator.Current;
            Assert.IsTrue(arena.Enter(navigator));
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(arena.FightActive);
            Assert.IsTrue(arena.ExitAvailable);
            Assert.IsTrue(arena.ExitDoor.activeSelf);
            Assert.AreEqual(from, arena.ExitScreen);
            Assert.IsTrue(arena.LeaveThroughExit());
            Assert.AreEqual(from, navigator.Current);
            Assert.IsEmpty(runEvents);
        }

        [UnityTest]
        public IEnumerator Exit_WalkingAlongOrIntoTheWestWall_OutsideTheDoorway_NeverLeaves()
        {
            yield return Load();
            yield return EnterFightFromTown(1234);
            stats.MaxHealth = 999;
            float x = arena.Inner.xMin + 0.45f * K;
            float doorY = arena.Centre.y + CoreArena.ExitOffsetY;

            // Pressing into the wall just below and just above the doorway.
            foreach (float y in new[] { doorY - CoreArena.ExitHalfHeight - 0.5f * K, doorY + CoreArena.ExitHalfHeight + 0.45f * K })
            {
                PlacePlayer(new Vector2(x + 0.5f * K, y));
                Hold(new Vector2Int(-1, 0));
                for (int i = 0; i < 40; i++) yield return new WaitForFixedUpdate();
                Hold(Vector2Int.zero);
                Assert.IsTrue(navigator.InCoreArena, $"Pushing into the wall at y {y - arena.Centre.y:0.0} does not leave");
            }

            // Walking along the wall, up past the doorway and down again, without turning into it.
            PlacePlayer(new Vector2(x, doorY - 2.2f * K));
            Hold(new Vector2Int(0, 1));
            yield return WaitUntilOrGameTimeout(() => body.position.y > doorY + 1.3f * K, 3f);
            Hold(new Vector2Int(0, -1));
            yield return WaitUntilOrGameTimeout(() => body.position.y < doorY - 1.3f * K, 3f);
            Hold(Vector2Int.zero);
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(navigator.InCoreArena, "Walking along the wall past the doorway does not leave");
            Assert.IsFalse(arena.DoorwayOpen, "nor opens the doorway");
            Assert.IsTrue(arena.FightActive, "and the fight goes on");
            Assert.AreEqual(0, arena.Exits);

            // Stepping just into the doorway's mouth (not deep into the recess) does not leave either.
            PlacePlayer(new Vector2(arena.Inner.xMin - 0.2f * K, doorY));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(navigator.InCoreArena, "In the doorway's mouth");
            Assert.IsTrue(arena.FightActive);
            Assert.IsEmpty(runEvents);
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
            PlacePlayer(c + new Vector2(-1.8f, -2f) * K);
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
            PlacePlayer(c + new Vector2(-4f, 2.5f) * K);
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
            if ((body.position + side * 2f * K).y > c.y + CoreArena.InnerHalf.y - 0.5f * K) side = -side;
            yield return MoveTo(body.position + side * 2f * K, TimeField.BasePlayerSpeed);
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
            Assert.IsFalse(arena.ExitAvailable, "No exit once the fight is lost");
            Assert.IsFalse(arena.ExitDoor.activeSelf, "The exit door is gone after a defeat");
            Assert.IsFalse(arena.FightActive);

            // Further events are ignored: mixing the arena now does nothing.
            Vector2 c = arena.Centre;
            List<Particle> warm = Of(ArenaSide.Warm);
            for (int i = 0; i < 8; i++) warm[i].Teleport(c + new Vector2(2f + (i % 4) * 1.3f, -3f + 2f * (i / 4)) * K);
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
            PlacePlayer(arena.Centre + new Vector2(-5f, 0f) * K);
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
                        .Any(d => d > 0.05f * K), "The particles drifted");
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

        [UnityTest]
        public IEnumerator DevKeys_F8LeavesTheArena_F9Rests_F10StartsANewRun()
        {
            yield return Load();
            var debug = UnityEngine.Object.FindAnyObjectByType<CubeDebug>();
            Assert.IsFalse(debug.LeaveCoreArena(), "F8 does nothing outside the arena");

            yield return EnterThroughAPortal();
            Assert.IsTrue(arena.FightActive);
            Assert.IsTrue(debug.LeaveCoreArena(), "F8 in the arena");
            Assert.IsFalse(navigator.InCoreArena);
            Assert.AreEqual(world.Model.StartScreen, navigator.Current, "back on the Town start screen");
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.IsFalse(arena.FightActive, "the fight is abandoned");

            var health = navigator.GetComponent<Health>();
            health.TakeDamage(2f, null);
            Assert.Less(health.Current, health.Max);
            Assert.IsTrue(debug.Rest(), "F9 while alive");
            Assert.AreEqual(health.Max, health.Current, 1e-4f, "full health");

            int before = world.Seed;
            Assert.IsTrue(debug.StartNewRun(), "F10");
            Assert.AreNotEqual(before, world.Seed, "a new seed");
            Assert.AreEqual(world.Model.StartScreen, navigator.Current, "starts in Town");
            Assert.IsFalse(world.ScienceRevealed, "a fresh, unrevealed run");
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
            private const float Deadband = 0.15f * K;
            private const float Behind = 1.7f * K;

            private static readonly Vector2Int[] Dirs =
            {
                new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(0, 1), new Vector2Int(-1, 1),
                new Vector2Int(-1, 0), new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1),
            };

            private readonly CoreSceneTests t;
            private readonly Dictionary<Particle, int> skip = new Dictionary<Particle, int>();
            private Particle target;
            private Vector2Int dir;
            private int targetSince;

            /// <summary>Physics steps decided so far: the bot's clock (its timers count steps, not Time.time, so they never
            /// depend on where game time stood when the fight began).</summary>
            private int step;

            private static int Steps(float seconds) => Mathf.RoundToInt(seconds / Time.fixedDeltaTime);

            public int Swings;
            public int Dodges;

            public Bot(CoreSceneTests tests) => t = tests;

            private CoreArena Arena => t.arena;
            private Vector2 Pos => t.body.position;

            /// <summary>The keys to hold for the next physics step, and whether to swing now.</summary>
            public Vector2Int Decide(out bool swing)
            {
                step++;
                swing = false;
                if (Dodge(out Vector2Int away))
                {
                    Dodges++;
                    return away;
                }

                ArenaSide side = Arena.SideOf(Pos);
                bool stale = step - targetSince > Steps(4f);
                if (target == null || !target.IsHome || target.Side != side || target.Knocked || stale)
                {
                    if (target != null && stale) skip[target] = step + Steps(4f);
                    Choose(side);
                }
                if (target == null) return CrossOver(side);

                Vector2 p = target.Position;
                Vector2 d = Unit(dir);
                Vector2 rel = Pos - p;
                float behind = Vector2.Dot(rel, -d);
                float lateral = Mathf.Abs(rel.x * d.y - rel.y * d.x);
                if (lateral < 0.25f * K && behind > 0.6f * K && behind < Behind + 0.6f * K)
                {
                    if (behind > t.attack.Reach + 0.15f * K) return dir; // walk in along the line: this also faces it
                    if (Vector2.Angle(t.mover.Facing, d) > 0.5f) return Steer(p - d * Behind, p, d); // back off and come again
                    if (!t.attack.Ready) return Vector2Int.zero;
                    // Swing on the move, still holding the keys, as a keyboard player does.
                    swing = true;
                    Swings++;
                    skip[target] = step + Steps(1f);
                    target = null;
                    return dir;
                }
                return Steer(p - d * Behind, p, d);
            }

            /// <summary>Keys toward a goal (per-axis dead band), going round the target particle if it is in the way.</summary>
            private Vector2Int Steer(Vector2 goal, Vector2 p, Vector2 d)
            {
                if (SegmentDistance(p, Pos, goal) < 0.85f * K && Vector2.Distance(Pos, goal) > 0.3f * K)
                {
                    Vector2 perp = Vector2.Perpendicular(d);
                    if (Vector2.Dot(Pos - p, perp) < 0f) perp = -perp;
                    goal = p + perp * 1.3f * K - d * 0.4f * K;
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
                const float m = 0.5f * K;
                if (spot.x < r.xMin + m || spot.x > r.xMax - m || spot.y < r.yMin + m || spot.y > r.yMax - m) return false;
                return Arena.SideOf(spot) == side && Mathf.Abs(spot.x - Arena.MembraneX) > 0.65f * K;
            }

            private void Choose(ArenaSide side)
            {
                target = null;
                targetSince = step;
                float best = float.MaxValue;
                foreach (Particle p in Arena.Particles)
                {
                    if (p == null || !p.IsHome || p.Side != side || p.Knocked || p.Nudged) continue;
                    if (skip.TryGetValue(p, out int until) && step < until) continue;
                    foreach (Vector2Int k in Dirs)
                    {
                        Vector2 d = Unit(k);
                        if (!Valid(p.Position - d * Behind, side) || !Valid(p.Position - d * t.attack.Reach, side)) continue;
                        float miss = Simulate(p.Position, d);
                        float score = miss * 4f + (miss > 0f ? 3f * K : 0f) + 0.5f * Vector2.Distance(Pos, p.Position - d * Behind);
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
                const float step = 0.05f * K, r = Particle.Radius;
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
                Vector2 before = gap - new Vector2(s * 1.2f * K, 0f);
                Vector2 after = gap + new Vector2(s * 1.2f * K, 0f);
                bool lined = Mathf.Abs(Pos.y - gap.y) < 0.25f * K;
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
                    if (miss.magnitude > 1.1f * K) continue;
                    away = Away(miss.magnitude > 0.05f * K ? miss : Vector2.Perpendicular(v));
                    return true;
                }
                MaxwellDemon demon = Arena.Demon;
                if (demon.Telegraphing)
                {
                    Vector2 rel = Pos - demon.Position;
                    float along = Vector2.Dot(rel, demon.TelegraphAim);
                    Vector2 lateral = rel - demon.TelegraphAim * along;
                    if (along > -0.5f * K && lateral.magnitude < 1.1f * K)
                    {
                        away = Away(lateral.magnitude > 0.05f * K ? lateral : Vector2.Perpendicular(demon.TelegraphAim));
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
                    Vector2 ahead = Pos + Unit(k) * 1.2f * K;
                    if (ahead.x < r.xMin + 0.5f * K || ahead.x > r.xMax - 0.5f * K || ahead.y < r.yMin + 0.5f * K || ahead.y > r.yMax - 0.5f * K) continue;
                    float dot = Vector2.Dot(Unit(k), want.normalized);
                    if (dot <= bestDot) continue;
                    bestDot = dot;
                    best = k;
                }
                return best;
            }
        }

        // Runs first in the fixture: the physics engine keeps internal state across scene loads (which of its proxy ids
        // are free, so the order it builds and solves contacts in), and the tests before it would leave that state
        // depending on their frame timing. First, each seed's fight sees the same engine history every run, as long as
        // the test history is the same (the fixtures sorted before this one in the batch, and the seed order).
        [UnityTest, Order(0)]
        public IEnumerator SkillCheck_AKeyboardPlayerWithTheStartingKit_WinsIn60To120Seconds([Values(1234, 7, 42)] int seed)
        {
            yield return Load();
            yield return StartFightFromTown(seed, alignStart: true);
            Assert.AreEqual(PlayerStats.DefaultMaxHealth, stats.MaxHealth, "Base stats");
            Assert.AreEqual(1, stats.Power);
            Assert.AreEqual(1, stats.Speed);
            Assert.AreEqual(0, stats.Defence);
            Assert.IsNull(attack.EquippedItem, "Bare hands");
            Assert.IsEmpty(navigator.GetComponent<Inventory>().Items);
            Assert.IsInstanceOf<MixingPhase>(arena.CurrentPhase);
            Assert.IsTrue(arena.Demon.Firing, "Order pulses on");
            Assert.IsTrue(arena.Demon.Restoring);

            // The bot acts once per physics step, in the physics phase (its keys applied at once, read by the next step),
            // never per rendered frame: the fight's steps and the bot's inputs pair up the same way however frames fall,
            // so a seed's win time is the same every run. Time is counted in fight steps.
            var bot = new Bot(this);
            int steps = 0;
            float peak = 0f;
            int maxSteps = Mathf.CeilToInt((SkillMaxSeconds + 30f) / Time.fixedDeltaTime);
            Time.timeScale = TimeScale;
            while (arena.FightActive && steps < maxSteps)
            {
                // The Demon follows and aims at the player's Transform, which interpolation (a visual setting; batch runs
                // draw nothing) moves between physics steps by however far the last rendered frame got. Off, the Transform
                // is the body's pose at every step, so the Demon sees the same player every run. (Entering the arena
                // turns interpolation back on two steps in, hence every step.) A known gap: in real play the Demon aims at
                // the interpolated Transform, so this check does not cover that frame-dependent lag.
                if (body.interpolation != RigidbodyInterpolation2D.None) body.interpolation = RigidbodyInterpolation2D.None;
                Vector2Int keys = bot.Decide(out bool swing);
                Hold(keys, now: true);
                if (swing) attack.TryAttack();
                yield return new WaitForFixedUpdate();
                steps++;
                peak = Mathf.Max(peak, arena.Meter);
            }
            Hold(Vector2Int.zero, now: true);
            Time.timeScale = 1f;
            float took = steps * Time.fixedDeltaTime;
            Debug.Log($"Skill check (seed {seed}): outcome {arena.Outcome} after {took:0.0} s, {bot.Swings} swings, {bot.Dodges} dodge steps, " +
                      $"peak meter {peak:0.00}, Demon restored {arena.Demon.Restored} and fired {arena.Demon.PulsesFired}, health {health.Current}/{health.Max}");
            Assert.AreEqual(0, arena.Exits, "The bot never walks out through the exit door");
            Assert.IsTrue(arena.ExitDoor != null, "The door was there all along");
            Assert.AreEqual(CoreOutcome.Victory, arena.Outcome, $"The bot wins (meter peaked at {peak:0.00}, health {health.Current})");
            Assert.GreaterOrEqual(took, SkillMinSeconds, "Steady pushing takes a while");
            Assert.LessOrEqual(took, SkillMaxSeconds, "but wins in time");
            Assert.IsFalse(health.IsDead);
            CollectionAssert.AreEqual(new[] { true }, runEvents);
        }
            // ---------------------------------------------------------------- boss benefits (4.8)

        private static IEnumerable<BossBenefit> EveryBenefit() => BossBenefits.All;

        /// <summary>What a test can measure of the arena in a fight: the built arena's own values, not the tuning.</summary>
        private struct ArenaReading
        {
            public int Particles;
            public float GapTop, GapBottom;
            public float Attraction;
            public float RestoreScale, NudgeSpeed;
            public float MaxHealth, Health;
            public float PulseInterval, PulseSpeed;
        }

        private ArenaReading Read()
        {
            Transform built = arena.transform.Find("Core Arena (built)");
            Bounds north = built.Find("Membrane North").GetComponent<BoxCollider2D>().bounds;
            Bounds south = built.Find("Membrane South").GetComponent<BoxCollider2D>().bounds;
            // A pulse fired now (destroyed before any physics step): its real speed.
            OrderPulse probe = arena.Demon.FirePulse(Vector2.left);
            float speed = probe.Velocity.magnitude;
            UnityEngine.Object.DestroyImmediate(probe.gameObject);
            List<Particle> live = arena.Particles.Where(p => p != null).ToList();
            return new ArenaReading
            {
                Particles = live.Count,
                GapTop = arena.Inner.yMax - north.max.y,
                GapBottom = south.min.y - arena.Inner.yMin,
                Attraction = live.Max(p => p.Attraction),
                RestoreScale = arena.Demon.RestoreScale,
                NudgeSpeed = arena.Demon.NudgeSpeedNow,
                MaxHealth = health.Max,
                Health = health.Current,
                PulseInterval = arena.Demon.PulseIntervalNow,
                PulseSpeed = speed,
            };
        }

        /// <summary>Game seconds from one order pulse to the next in the current fight (its telegraph included), run fast.</summary>
        private IEnumerator MeasurePulseGap(Action<float> result)
        {
            health.InvulnerableSeconds = 1000f; // the measurement must not end the fight
            Time.timeScale = 4f;
            int start = arena.Demon.PulsesFired, steps = 0, firstAt = -1;
            while (arena.FightActive && steps < 1200)
            {
                yield return new WaitForFixedUpdate();
                steps++;
                int fired = arena.Demon.PulsesFired - start;
                if (fired == 1 && firstAt < 0) firstAt = steps;
                if (fired >= 2) break;
            }
            Time.timeScale = 1f;
            result(firstAt >= 0 ? (steps - firstAt) * Time.fixedDeltaTime : -1f);
        }

        /// <summary>
        /// 4.8: each boss benefit measurably changes its parameter in a real fight, and only it: the particles spawned, the
        /// membrane gaps' height (from the built colliders), the particles' pull, the Demon's restore pace, the player's
        /// hearts (given at the start, taken back on leaving) and the pulses (speed of a fired pulse; for SlowerShots, the
        /// measured time between two pulses). A fight with no benefit first, on the same seed, is the baseline.
        /// </summary>
        [UnityTest]
        public IEnumerator EachBenefit_MeasurablyChangesItsArenaParameter_AndOnlyIt([ValueSource(nameof(EveryBenefit))] BossBenefit benefit)
        {
            yield return Load();
            arena.BenefitsOverride = BossBenefit.None;
            yield return StartFightFromTown(1234);
            yield return new WaitForFixedUpdate();
            ArenaReading before = Read();
            Assert.AreEqual(2 * CoreArena.DefaultParticlesPerSide, before.Particles, "Baseline: the 1.11 arena");
            Assert.AreEqual(CoreArena.GapHeight, before.GapTop, 1e-3f);
            Assert.AreEqual(PlayerStats.DefaultMaxHealth, before.MaxHealth);
            float gapBefore = -1f;
            if (benefit == BossBenefit.SlowerShots) yield return MeasurePulseGap(g => gapBefore = g);

            navigator.TeleportTo(world.Model.StartScreen, Facing.East);
            yield return PhysicsSteps(2);
            Assert.IsFalse(arena.FightActive, "Leaving abandons the fight");
            health.InvulnerableSeconds = 0f;
            health.ResetHealth();

            arena.BenefitsOverride = benefit;
            Assert.IsTrue(arena.Enter(navigator));
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(benefit, arena.ActiveBenefits);
            ArenaReading after = Read();
            StringAssert.Contains(BossBenefits.Label(benefit), hud.BenefitsText,
                "The core HUD names the help");

            bool particles = after.Particles != before.Particles;
            bool gap = Mathf.Abs(after.GapTop - before.GapTop) > 1e-3f;
            bool pull = after.Attraction != before.Attraction;
            bool restore = after.RestoreScale != before.RestoreScale || after.NudgeSpeed != before.NudgeSpeed;
            bool hearts = after.MaxHealth != before.MaxHealth;
            bool pulses = after.PulseInterval != before.PulseInterval || Mathf.Abs(after.PulseSpeed - before.PulseSpeed) > 1e-3f;
            var changed = new List<string>();
            if (particles) changed.Add("particles");
            if (gap) changed.Add("gap");
            if (pull) changed.Add("pull");
            if (restore) changed.Add("restore");
            if (hearts) changed.Add("hearts");
            if (pulses) changed.Add("pulses");
            Debug.Log($"Benefit {benefit}: changed {string.Join(", ", changed)}; particles {before.Particles}->{after.Particles}, " +
                      $"gap {before.GapTop:0.###}->{after.GapTop:0.###}, pull {before.Attraction:0.###}->{after.Attraction:0.###}, " +
                      $"restore x{before.RestoreScale}->x{after.RestoreScale} nudge {before.NudgeSpeed:0.###}->{after.NudgeSpeed:0.###}, " +
                      $"hearts {before.MaxHealth}->{after.MaxHealth}, pulses every {before.PulseInterval}->{after.PulseInterval} s at " +
                      $"{before.PulseSpeed:0.###}->{after.PulseSpeed:0.###}");
            switch (benefit)
            {
                case BossBenefit.FewerParticles:
                    CollectionAssert.AreEqual(new[] { "particles" }, changed);
                    Assert.AreEqual(2 * (CoreArena.DefaultParticlesPerSide - ArenaTuning.FewerParticlesBy), after.Particles, "12, not 16");
                    Assert.AreEqual(after.Particles / 2, arena.Particles.Count(p => p.Home == ArenaSide.Warm), "Half a side each");
                    break;
                case BossBenefit.WiderGap:
                    CollectionAssert.AreEqual(new[] { "gap" }, changed);
                    Assert.AreEqual(ArenaTuning.WideGapHeight, after.GapTop, 1e-3f, "The north gap, from the membrane's collider");
                    Assert.AreEqual(ArenaTuning.WideGapHeight, after.GapBottom, 1e-3f, "and the south gap");
                    Assert.AreEqual(arena.CurrentGapHeight, after.GapTop, 1e-3f);
                    break;
                case BossBenefit.Attraction:
                    CollectionAssert.AreEqual(new[] { "pull" }, changed);
                    Assert.IsTrue(arena.Particles.All(p => p.Attraction == ArenaTuning.AttractSpeed), "Every particle is drawn to the player");
                    break;
                case BossBenefit.SlowerRestore:
                    CollectionAssert.AreEqual(new[] { "restore" }, changed);
                    Assert.Greater(after.RestoreScale, before.RestoreScale);
                    Assert.Less(after.NudgeSpeed, before.NudgeSpeed);
                    break;
                case BossBenefit.ExtraHeart:
                    CollectionAssert.AreEqual(new[] { "hearts" }, changed);
                    Assert.AreEqual(before.MaxHealth + 1f, after.MaxHealth, "One more heart");
                    Assert.AreEqual(after.MaxHealth, after.Health, "and it is filled");
                    break;
                case BossBenefit.SlowerShots:
                    CollectionAssert.AreEqual(new[] { "pulses" }, changed);
                    Assert.Less(after.PulseSpeed, before.PulseSpeed, "A fired pulse is slower");
                    float gapAfter = -1f;
                    yield return MeasurePulseGap(g => gapAfter = g);
                    Debug.Log($"Pulse gap: {gapBefore:0.00} s -> {gapAfter:0.00} s");
                    Assert.Greater(gapBefore, 0f);
                    Assert.Greater(gapAfter, gapBefore + 1.5f, "Pulses come about 2 s further apart");
                    break;
            }

            // Leaving takes the fight's extra heart back.
            navigator.TeleportTo(world.Model.StartScreen, Facing.East);
            yield return PhysicsSteps(2);
            Assert.IsFalse(arena.FightActive);
            Assert.AreEqual(PlayerStats.DefaultMaxHealth, health.Max, "The extra heart was for the fight only");
            Assert.AreEqual(0, arena.HeartsGranted);
            arena.BenefitsOverride = null;
        }

        // ---------------------------------------------------------------- 4.8 review: behaviour, and the extra heart's takeback

        /// <summary>
        /// A fight on seed 1234 with a benefit (a rebuild first, so each fight replays the same drift), the Demon idle: the mean
        /// speed of the warm particles within the pull's range towards the player over 3 s.
        /// </summary>
        private IEnumerator MeanSpeedTowardsThePlayer(BossBenefit benefit, Action<float> result)
        {
            arena.BenefitsOverride = benefit;
            yield return StartFightFromTown(1234);
            arena.SetDemonDuties(false, false);
            float sum = 0f;
            int samples = 0;
            for (int step = 0; step < 150; step++)
            {
                yield return new WaitForFixedUpdate();
                Vector2 player = body.position;
                foreach (Particle p in arena.Particles)
                {
                    if (p == null || p.Home != ArenaSide.Warm) continue;
                    Vector2 to = player - p.Position;
                    if (to.magnitude > ArenaTuning.AttractRange || to.magnitude < 1e-3f) continue;
                    sum += Vector2.Dot(p.Body.linearVelocity, to.normalized);
                    samples++;
                }
            }
            result(samples > 0 ? sum / samples : 0f);
            navigator.TeleportTo(world.Model.StartScreen, Facing.East);
            yield return PhysicsSteps(2);
        }

        /// <summary>4.8 review: Attraction measured on the particles' motion: their velocity towards the player rises.</summary>
        [UnityTest]
        public IEnumerator Attraction_RaisesTheParticlesVelocityTowardsThePlayer()
        {
            yield return Load();
            float without = 0f, with = 0f;
            yield return MeanSpeedTowardsThePlayer(BossBenefit.None, v => without = v);
            yield return MeanSpeedTowardsThePlayer(BossBenefit.Attraction, v => with = v);
            arena.BenefitsOverride = null;
            Debug.Log($"Attraction: mean speed towards the player {without:0.###} -> {with:0.###} (pull {ArenaTuning.AttractSpeed:0.###})");
            Assert.Greater(with, without + 0.5f * ArenaTuning.AttractSpeed, "Particles drift towards the player");
        }

        /// <summary>
        /// A fight on seed 1234 with a benefit, five warm particles moved across, the Demon restoring (no pulses): the mean game
        /// time between its restorations (fetch and pause), over the first four.
        /// </summary>
        private IEnumerator TimeBetweenFetches(BossBenefit benefit, Action<float> result)
        {
            arena.BenefitsOverride = benefit;
            yield return StartFightFromTown(1234);
            arena.SetDemonDuties(true, false);
            List<Particle> warm = Of(ArenaSide.Warm);
            Vector2 c = arena.Centre;
            for (int i = 0; i < 5; i++) warm[i].Teleport(c + new Vector2(2f + (i % 3) * 1.5f, -3.6f + 0.9f * (i / 3)) * K);
            var times = new List<int>();
            int last = arena.Demon.Restored, steps = 0;
            Time.timeScale = 4f;
            while (times.Count < 4 && steps < 2000 && arena.FightActive)
            {
                yield return new WaitForFixedUpdate();
                steps++;
                if (arena.Demon.Restored > last)
                {
                    last = arena.Demon.Restored;
                    times.Add(steps);
                }
            }
            Time.timeScale = 1f;
            result(times.Count >= 2 ? (times[times.Count - 1] - times[0]) * Time.fixedDeltaTime / (times.Count - 1) : -1f);
            navigator.TeleportTo(world.Model.StartScreen, Facing.East);
            yield return PhysicsSteps(2);
        }

        /// <summary>4.8 review: SlowerRestore measured on the Demon's work: the time between its fetches rises.</summary>
        [UnityTest]
        public IEnumerator SlowerRestore_SpacesOutTheDemonsFetches()
        {
            yield return Load();
            float without = 0f, with = 0f;
            yield return TimeBetweenFetches(BossBenefit.None, v => without = v);
            yield return TimeBetweenFetches(BossBenefit.SlowerRestore, v => with = v);
            arena.BenefitsOverride = null;
            Debug.Log($"Slower restore: time between fetches {without:0.00} s -> {with:0.00} s");
            Assert.Greater(without, 0f, "The Demon fetched four strays");
            Assert.Greater(with, without * 1.3f, "and is clearly slower with the benefit");
        }

        /// <summary>A new run on a seed, ExtraHeart on, the player hurt by damage first; then into the fight.</summary>
        private IEnumerator EnterWithAnExtraHeart(int seed, float damage)
        {
            arena.BenefitsOverride = BossBenefit.ExtraHeart;
            world.Rebuild(seed);
            yield return null;
            NoFaceEnemies();
            arena.UseFallbackBoss = false;
            health.InvulnerableSeconds = 0f;
            if (damage > 0f) health.TakeDamage(damage, null);
            yield return new WaitForFixedUpdate();
        }

        /// <summary>4.8 review (medium): entering and leaving again and again, hurt, never heals: the extra heart is the fight's only.</summary>
        [UnityTest]
        public IEnumerator ExtraHeart_EnteringAndLeavingTwice_WhileHurt_GivesNoNetHeal()
        {
            yield return Load();
            yield return EnterWithAnExtraHeart(1234, 2f);
            float before = health.Current;
            Assert.AreEqual(PlayerStats.DefaultMaxHealth - 2f, before, 1e-4f);
            for (int round = 0; round < 2; round++)
            {
                Assert.IsTrue(arena.Enter(navigator));
                yield return new WaitForFixedUpdate();
                Assert.IsTrue(arena.FightActive);
                Assert.AreEqual(PlayerStats.DefaultMaxHealth + 1f, health.Max, $"round {round}: one more heart");
                Assert.AreEqual(before + 1f, health.Current, 1e-4f, $"round {round}: added filled");
                navigator.TeleportTo(world.Model.StartScreen, Facing.East);
                yield return PhysicsSteps(2);
                Assert.IsFalse(arena.FightActive, $"round {round}: abandoned");
                Assert.AreEqual(PlayerStats.DefaultMaxHealth, health.Max, $"round {round}: the heart is taken back");
                Assert.AreEqual(before, health.Current, 1e-4f, $"round {round}: and no health was gained");
            }
            arena.BenefitsOverride = null;
        }

        /// <summary>4.8 review: the extra heart is taken back however the fight ends: a win, a loss, the exit door, F8, a rebuild.</summary>
        [UnityTest]
        public IEnumerator ExtraHeart_IsTakenBack_WithNoNetHeal_HoweverTheFightEnds([Values("win", "loss", "exit", "f8", "rebuild")] string ending)
        {
            yield return Load();
            yield return EnterWithAnExtraHeart(1234, ending == "loss" ? 0f : 1f);
            if (ending == "loss")
            {
                stats.MaxHealth = 1;
                Assert.AreEqual(1f, health.Current, 1e-4f);
            }
            int maxBefore = stats.MaxHealth;
            float before = health.Current;
            Assert.IsTrue(arena.Enter(navigator));
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(maxBefore + 1, stats.MaxHealth, "One more heart for the fight");
            Assert.AreEqual(1, arena.HeartsGranted);
            switch (ending)
            {
                case "win":
                    List<Particle> warm = Of(ArenaSide.Warm);
                    Vector2 c = arena.Centre;
                    for (int i = 0; i < 7; i++) warm[i].Teleport(c + new Vector2(2f + (i % 3) * 1.5f, -3.6f + 0.9f * (i / 3)) * K);
                    yield return WaitUntilOrGameTimeout(() => !arena.FightActive, 1f);
                    Assert.AreEqual(CoreOutcome.Victory, arena.Outcome);
                    break;
                case "loss":
                    health.TakeDamage(1000f, null);
                    yield return null;
                    Assert.AreEqual(CoreOutcome.Defeat, arena.Outcome);
                    break;
                case "exit":
                    Assert.IsTrue(arena.LeaveThroughExit(), "Out through the exit door");
                    yield return new WaitForFixedUpdate();
                    break;
                case "f8":
                    Assert.IsTrue(UnityEngine.Object.FindAnyObjectByType<CubeDebug>().LeaveCoreArena(), "F8 leaves the arena");
                    yield return PhysicsSteps(2);
                    break;
                default:
                    world.Rebuild(4321);
                    yield return null;
                    break;
            }
            Assert.IsFalse(arena.FightActive, ending);
            Assert.AreEqual(0, arena.HeartsGranted, $"{ending}: taken back");
            Assert.AreEqual(maxBefore, stats.MaxHealth, $"{ending}: the max is back");
            Assert.AreEqual((float)maxBefore, health.Max, 1e-4f, ending);
            Assert.LessOrEqual(health.Current, before + 1e-4f, $"{ending}: no net heal");
            if (ending == "exit" || ending == "f8" || ending == "win" || ending == "rebuild")
                Assert.AreEqual(before, health.Current, 1e-4f, $"{ending}: untouched, the health it came in with");
            arena.BenefitsOverride = null;
        }
    }
}
