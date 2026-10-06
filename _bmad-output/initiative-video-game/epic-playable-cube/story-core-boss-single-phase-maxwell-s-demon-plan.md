---
title: "Core boss: single-phase Maxwell's Demon"
type: 'feature'
ticket: '11'
created: '2026-10-05'
status: 'built'
baseline_revision: '9178495d7e6078876cfb132503a628dd2428eded'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/faces-and-races.md'
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/lore-and-tone.md'
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/stack.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A run has no ending. Every built face has a core-entrance slot leading nowhere, and the spec's final boss (CAP-11), Maxwell's Demon, the force of order behind the Partition, doesn't exist yet.

**Approach:** Each built face's active core-entrance slot becomes a portal into a core arena, open from run 1. The arena starts ordered: particles are split into a warm side and a cool side by a central membrane with a door that the Demon works. The player wins by mixing the arena (driving particles across the membrane) until an entropy meter fills, faster than the Demon can restore order. The Demon restores order by moving stray particles back, opening and shutting its door, and firing avoidable "order pulses" that damage the player. Win and loss each raise one run-end event (victory or defeat) for story 1.12. The phase structure has a slot for later phases (Epic 2), and this story ships one phase.

## Boundaries & Constraints

**Always:** The portal works from every built face's active core-entrance slot, from run 1, with nothing required. A skilled player with only the starting kit (bare attack, base stats) can win. Particles are pushed by attacks (bare or any item) and drift on their own. An entropy meter (0 = fully ordered, 1 = fully mixed) is computed from particle positions; the player wins at a threshold (e.g. 0.8). The Demon's restore rate is tuned so idle play loses ground, but steady pushing wins in roughly 60–120 s. Loss means player death in the arena. A `RunEnded(bool victory)` event fires exactly once per run and is raised from one shared place (a RunState) that 1.12 will consume. In-game text and imagery speak of the Partition and order and never use the word "sort": the meter is labelled "Order ↔ Entropy". A boss-phase list exists with phase 1 implemented, and adding phases needs no arena rewrite. Keep a fallback simpler boss (e.g. hit the Demon N times while dodging pulses) behind a setting, in case the mixing phase misbehaves.

**Never:** No run reset, meta currency or upgrades (1.12). No multiple victories or face-based phases (Epic 2, CAP-15). No imported art. Do not affect the cube sweep guarantees.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Enter | Player steps into the core portal on any built face, run 1 | Player is moved into the core arena and the fight starts | No error expected |
| Ordered start | Arena begins | Warm particles on one side, cool on the other; meter near 0 | No error expected |
| Push | Player attacks a particle | It is knocked away in the facing direction and can cross the membrane when the door is open or through gaps | No error expected |
| Restore | Player idle | The Demon moves strays back and the meter falls over time | No error expected |
| Win | Meter reaches the threshold | Demon defeated, `RunEnded(true)` once, victory text shown | Further events ignored |
| Pulse | Order pulse hits the player | Damage per 1.6 rules (defence and invulnerability apply) | No error expected |
| Lose | Player dies in the arena | `RunEnded(false)` once, defeat text | Further events ignored |
| Fallback | Fallback setting on | Simpler boss: hit the Demon N times; win and lose raise the same event | No error expected |
| Skill check | Scripted "skilled player" bot with starting kit | Wins within 150 s | Test fails if not |

</frozen-after-approval>

## Code Map

- `game/Assets/Scripts/Cube/Modules/CoreEntranceSlot.cs`, `ScreenModule.SetActiveCoreEntrance` (`CubeWorld.cs` around line 400) -- active core slot per built face. Attach a portal trigger here on reveal.
- `game/Assets/Scripts/Cube/CubeWorld.cs` -- world layout: faces sit in their own regions with gaps (`FaceOrigin`). Put the arena in its own region far from the faces.
- `game/Assets/Scripts/Cube/CubeNavigator.cs` -- `TeleportTo`, `SetPosition` (snaps with interpolation off). The arena entry must not trigger cube edge crossing: suspend the navigator while in the arena.
- `game/Assets/Scripts/Cube/Combat/*` -- `Health`, `PlayerStats` (`Died`), `PlayerAttack` (`IAttackReceiver`, `Swung`), `CombatHud`, `CombatMath`.
- `game/Assets/Scripts/Tracer/ScreenCamera.cs` -- the camera snaps to its target's 16×10 screen. The arena should be exactly one screen, or the camera framing must be handled.
- `game/Assets/Editor/CubeSceneBuilder.cs` -- add the arena and RunState to the Cube scene.

## Tasks & Acceptance

**Execution:**
- [x] `game/Assets/Scripts/Cube/Run/RunState.cs` -- one per scene. `RunEnded(bool victory)` fires once per run and is reset on world rebuild. 1.12 builds on this.
- [x] `game/Assets/Scripts/Cube/Core/CorePortal.cs` -- trigger on each built face's active core slot. It moves the player into the arena and suspends cube navigation.
- [x] `CoreArena.cs` -- one 16×10 screen region: walls, a central membrane with the Demon's door, about 16 particles (8 warm, 8 cool) spawned ordered, and the entropy meter calculation.
- [x] `Particle.cs` (`IAttackReceiver`: knockback on hit, gentle drift, bounces off walls) and `MaxwellDemon.cs` (moves along the membrane, opens and shuts its door to restore order, nudges strays home, fires periodic order pulses with a telegraph).
- [x] `BossPhase` abstraction plus `MixingPhase` (Must), and `FallbackBoss` behind a setting.
- [x] `CoreHud.cs` -- "Order ↔ Entropy" meter, plus victory and defeat text in the campy tone ("Entropy wins! The Partition cracks…").
- [x] Wire everything into CubeWorld reveal and CubeSceneBuilder, and regenerate the scene in batch mode.
- [x] Tests: EditMode tests for the meter maths and the phase list. PlayMode tests for every I/O row, including the skilled-player bot (a scripted controller pushing particles toward the less-mixed side) winning within 150 s with base stats, death raising defeat, the event once, and the fallback.

**Acceptance Criteria:**
- Given the EditMode and PlayMode suites and the seed sweep in batch mode, when run, then all pass, including earlier stories'.
- Given the Cube scene, when the player enters the core from any built face on run 1, then the fight is winnable with the starting kit and both outcomes raise the run-end event.

## Implementation Notes

- Verified in batch mode after the review patches: EditMode 153/153, PlayMode 99/99 (about 5 min; the skill-check bot fights take most of it), seed sweep 50/50. WebGL build PENDING (user). Human playtest of the boss PENDING (user). The bot's win time depends on frame rate (about 94–105 s), with a 15 s margin to the 120 s bound. Watch for flakiness on slower machines.
- Checkpoint 1 was approved under the user's standing instruction (2026-10-05) to keep building while they are away. Review the plan on return.
- Verified in batch mode: Create Cube Scene (adds Run State, Core Arena, Core HUD), EditMode 153/153, PlayMode 97/97 (10 new in `CoreSceneTests`; after the review fixes `CoreSceneTests` has 12 and passes, with `CubeSceneTests` and EditMode `CoreBossTests` re-run; full suites not re-run here), seed sweep 50/50 (unchanged). WebGL build skipped per Verification. Not committed. Playthrough by eye PENDING (user).
- Files: `Run/RunState.cs`; `Core/` `CorePortal`, `CoreArena`, `Particle`, `MaxwellDemon`, `OrderPulse`, `BossPhase` (`BossPhase`, `MixingPhase`, `FallbackBoss`, `BossFight` = the phase list, `IBossArena`), `EntropyMeter` (pure meter maths, `ArenaSide`), `CoreHud`. `CubeNavigator` gains `InCoreArena`/`EnterCoreArena` (edge checks off until the next `TeleportTo`). `CubeDebug` knows the arena: F1 shows "Core arena" instead of a face and cell, F3/F7 spawn inside the arena's screen (`CubeDebug.CoreArena`, wired by the scene builder). No change to `PlayerMover`.
- Arena: screen (0, -3) (centre (8, -25)), below the faces, so `ScreenCamera` frames it exactly. Inner 15 x 9, membrane at the centre x, door 2 tall in the middle, always-open gaps 1.5 tall at both ends. 8 warm (left) + 8 cool (right) particles in columns at x = ±3, ±5. Portals: `CoreArena` subscribes to `CubeWorld.Revealed` and puts a `CorePortal` (trigger, radius 0.45) on every active core-entrance slot. Leaving the arena mid-fight (debug jump) abandons the fight; a rebuild resets arena, RunState and navigation. After a victory the player stays in the arena (no exit until 1.12's run reset; F5 reroll leaves).
- Meter: wrong ÷ (total ÷ 2), clamped; win at 0.8 = 7 of 16 away. Particles: knock 9 u/s along the attacker's facing (bare or any item), damping 1, bounciness 0.9, drift 0.35 u/s turning away from outer walls; drift direction changes every 100 physics steps. Drift RNG: `CoreArena.DriftSeed(run seed, fight index in this run, particle index)` (SplitMix64 mixing, no bit packing), stream 15; the fight index resets on every rebuild, so a seed replays its drift exactly. Demon: holds a stray (nearest the door, not just knocked), steers it to the door at 4.5 u/s, opens it, steers it home, shuts it; a knock breaks its hold. Pulses: every 4 s (first after 2.5 s), 0.8 s telegraph (flash + aim line, Demon holds still), 6.5 u/s, 1 damage through `Health.TakeDamage` (defence and invulnerability apply). Fallback (`CoreArena.UseFallbackBoss`): pulses only, 10 swings on the Demon win. Win/lose: the arena settles its outcome and raises `FightEnded` first; `RunState.EndRun` is the last step.
- Final tuning (the Demon tires, Landauer-style): pause between fetches `RestoreIntervalAt(t) = RestoreInterval 0.2 s + RestoreFatigue 0.06 × max(0, t − FreshSeconds 45 s)`, with a fetch taking about 1 s. So it restores about one particle every 1.2 s for the first 45 s, about one every 2 s at 60 s, and about one every 3–4 s from 80–90 s; this replaces the Design Notes' flat "1 per 3–4 s". Unchanged: `GapHeight` 1.5, `Particle.KnockSpeed` 9, threshold 0.8. Skill check: a keyboard bot (WASD through the real `PlayerMover`, 8 directions, swings on the move, base stats, bare hands, pulses on, dodging in 8 directions) wins in 94–105 s on seeds 1234/7/42 (74–105 s over six seeds); the test asserts 60–120 s. Idle with pulses on: 6 strays go home within 15 s. A flat restore rate (with or without the earlier meter escalation) gave 10 s to over 150 s across seeds, which is why the rate is time-based.

## Review Triage Log

Pass 1 (quick lens): high 1, medium 2, low 1, false 0, maybe-false 0. The pacing and bot-realism findings share one root cause and are grouped.

| Verdict | Route | Finding | Evidence / action |
|---|---|---|---|
| high | patch | Pacing breaks the Always rule (60–120 s): the bot wins in 15–26 s with any-angle aim via a production hook | The bot set velocity and aim directly, and the test only asserted <150 s. Now an 8-direction bot with pulses on, the game retuned to the window, and the window asserted. |
| medium | patch | Same seed doesn't reproduce particle drift (fightCount never reset; bit collision) | Breaks stack.md "a seed must reproduce a run exactly". Now per-run fight index and collision-free RNG derivation, with a test. |
| medium | patch | A RunEnded listener that rebuilds the world would reset the arena before FightEnded | EndRun ran before FightEnded. EndRun is now the last step. Story 1.12 depends on this. |
| low | patch | Debug overlay and F3/F7 wrong in the arena | SuspendAt kept the origin face. Now an in-arena state is exposed. |

## Design Notes

Entropy meter: for each particle, take its side relative to its "home" side. Meter = (number on the wrong side) ÷ (total ÷ 2), clamped to 1. The Demon restores about 1 particle every 3–4 s at base; the player's push rate should comfortably beat that when aiming at the door or gaps. Gaps: two small gaps at the membrane ends that the Demon cannot block, so mixing is always possible.

## Verification

**Commands:**
- Unity batch `-runTests -testPlatform EditMode` and `PlayMode` -- expected: result Passed, 0 failed.
- Unity batch `-quit -executeMethod CubeSceneBuilder.RunSeedSweep` -- expected: 50/50.
- WebGL build: skip during this session (low memory); the user builds later.
