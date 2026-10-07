---
title: 'Refactor sweep (full cube)'
type: 'refactor'
ticket: '23'
created: '2026-10-06'
status: 'built'
baseline_revision: 'a41426852e995f60cd63cfab6da35d3b310c7496'
route: 'full'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/stack.md'
  - '{project-root}/_bmad-output/initiative-video-game/deferred-work.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Stories 1.15 to 1.22 added two faces, a stone and wand variant system, a map, an art catalog and a text rule in one day. The reviews deferred a few cleanup items:
- The boss skill-check bot depends on frame timing.
- Test isolation is uneven.
- `GateLooks.Planned` re-implements each face's build conditions, which is a drift risk.
- Several near-copies exist across the five faces.

**Approach:** Cleanup only, with no gameplay change. Every determinism signature stays unchanged, and every suite and the sweep pass.

## Boundaries & Constraints

**Always:**
- **No behaviour change for players.**
- **Determinism:** `DeterminismSignatures.txt` must be unchanged without re-recording, and the seed sweep passes 50/50.
- **Tests keep their meaning.** A test may be made more robust (frame-independent, isolated) but never weaker. The skill check keeps its 60–120 s window and its seeds.
- **Extract only true duplicates.** If two copies differ in behaviour, leave them apart.
- **Testing:** PlayMode runs in `-testFilter` class batches, in the foreground, with no leftover Unity processes.

**Never:**
- No new features, tuning, art or text changes.
- No changes to planning files except this plan and `deferred-work.md` (mark resolved items).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Determinism | Seeds 1–50 | Signatures identical to the baseline commit | Test fails |
| Skill check | CoreSceneTests alone ×2, and after other classes | The same win time per seed every run; all in 60–120 s | Test fails |
| Gate looks | Seeds 1–50 | Hint gate looks equal the gates actually built (one source of truth) | Test fails |
| Suites | EditMode; PlayMode class batches; sweep | All pass | No error expected |

</frozen-after-approval>

## Code Map

- Deferred items (`_bmad-output/initiative-video-game/deferred-work.md`):
  - Skill-check variation: the bot reacts per rendered frame. Make it fixed-step (decide in `FixedUpdate` or a WaitForFixedUpdate loop), so win time is frame-independent. The 1.22 "wait 3 frames" settle can then go if no longer needed.
  - CoreSceneTests isolation: run-to-run variation for 1234 and 7.
- `Npcs/HintGenerator.cs` `GateLooks.Planned`: duplicates face build conditions. Have each face plan expose "gate kind/look per placed gate" (pure), and use that from both the face's CreateGate and the hints, so there's one source.
- Duplicated patterns across `Faces/Biology|Chemistry|Physics|Math|Earth`:
  - population spawn loops
  - enemy spawning
  - text tests' collectors
  - variant kind-by-id helpers (`BiologyBeaks`, `ChemistryIsotope`, `Stormcaller`)
  - spot filters (FaceContent already shared)

  Extract only where the copies are identical.
- Large files: `Faces/Math/MathPlan.cs`, `Faces/Earth/EarthPlan.cs`, `Core/CoreArena.cs`, `CubeWorld.cs`. Split into partials only where seams are clean, as PhysicsPlan was split in 1.13.
- `Tests/EditMode/DeterminismTests.cs` plus the fixture: run, don't re-record.

## Tasks & Acceptance

**Execution:**
- [x] Make the skill-check bot frame-independent, and confirm the same win time per seed across ≥3 runs (alone and after other classes).
- [x] One source for gate looks (face plans expose per-gate kind/look; faces and hints both read it). Add a test that looks equal the built gates.
- [x] Extract identical duplicates across the five faces (population and enemy spawn helpers, variant kind-by-id helper, text-test collectors).
- [ ] Optional partial-class splits of the largest files. (Skipped: not needed for the cleanup, and no clean seam worth the risk this pass.)
- [x] Mark resolved items in `deferred-work.md`.
- [x] Verify: EditMode, PlayMode class batches, sweep, and determinism unchanged.

**Acceptance Criteria:**
- Given the full suites and the sweep, when run, then all pass and determinism signatures are unchanged without re-recording.

## Implementation Notes

- Checkpoint 1 approved under the user's standing instruction (2026-10-06).
- **Skill check (frame-independent).** The bot decides once per physics step in a `WaitForFixedUpdate` loop; `Hold(dir, now: true)` calls `InputSystem.Update()` so the next step's `PlayerMover.FixedUpdate` reads the keys. Win time is counted in fight steps, and the bot's own timers (stale target, skip) count steps instead of `Time.time`. The 3-frame settle is removed. `StartFightFromTown` returns in the physics phase right after `Enter`; `EnterFightFromTown` wraps it for the other tests.
- **Further drift found and pinned (test-side only, no game change):** (1) game code times short windows against `Time.time` (`Particle.Knocked`, `Health` invulnerability), and whether a window's last step counts depends on float rounding at the absolute time, so each fight now starts on the next multiple of 400 s game time at least 400 s past the current time (fast-forwarded at time scale 100, Town NPCs placed again in that step), which absorbs the few seconds of frame-paced jitter from earlier tests; (2) the physics engine keeps internal state across scene loads that earlier tests leave frame-dependent, so the skill check runs first in its fixture (`[Order(0)]`); (3) the Demon follows and aims at the player's Transform, which interpolation moves by frame timing, so player interpolation is off during the fight (a visual setting). Known gap: real play aims at the interpolated Transform, so the check does not cover that lag.
- **Scope of reproducibility:** per-seed skill-check times reproduce only for the same test history (the same fixtures sorted before CoreSceneTests in the batch, and the same seed order). Another history can start the fights on other multiples and give other times; nothing more general is claimed.
- **Physics hint look, last match:** the Physics hint now uses the last matching door at a slot (`PhysicsPlan.DoorIndex`, what `PhysicsFace` builds) instead of "any matching door with a switch". It differs only if two doors share a slot, and it cannot move determinism (no RNG draw changes; the fixture is unchanged).
- **Skill-check times (aligned start, final):** 1234 / 7 / 42 = 109.6 / 81.0 / 83.5 s, identical (incl. swings and dodge steps) in CoreSceneTests alone, in a batch after CombatComponentTests and CombatSceneTests, and in verify.sh batch 0 (with CubeMapSceneTests). (The earlier fixed 400/800/1200 s start gave 109.6 / 68.1 / 83.5 s.) Window 60-120 s and seeds unchanged.
- **Gate looks, one source.** Each face plan gains `ForRun` (the reveal's plan-or-null conditions), `Builds` (the per-gate build condition, item checks included; Biology builds every planned gate) and `GateLook` (the look of the gate the face builds, or null for a plain gate). `BiologyFace`, `ChemistryFace`, `PhysicsFace`, `MathFace` and `EarthFace` use `ForRun`/`Builds`; `GateLooks.Planned` uses `ForRun` and `GateLook` and no longer repeats any condition. New PlayMode test `CubeSceneTests.GateLooks_HintsNameTheGateTheRevealBuilt_OnSeeds1To50` maps each built gate component to its look and checks it equals `HintGenerator.LookOf` for every required gate on seeds 1-50 (PlayMode, since only the reveal builds the components).
- **Extracted (identical copies only, same call order):** `FaceContent.SpawnPopulation` (the NPC spawn loop of all five faces; Physics' Newton stays its own) and `FaceContent.SpawnEnemies` (the two-kind enemy loop of all five faces); `ItemVariants.KindsOf` behind `BiologyBeaks`/`ChemistryIsotope`/`StormcallerItem.VariantKinds`; EditMode `FaceText.Flavour`/`PartNames` for the Chemistry, Math and Earth text tests; a duplicate "Gate looks" source in `InWorldTextTests.FixedStrings`.
- **Left apart on purpose:** the items' `KindOf` id tables (different ids per item), the per-face name-star checks in the text tests (their sentence-start rules differ), the population `LinesFor`/`FlavourFor` helpers (different lines per face), the per-face `CreateGate` construction switches (different components).
- **Verification (verify.sh):** EditMode 245/245 (incl. determinism; `DeterminismSignatures.txt` unchanged, never re-recorded); PlayMode batches 25/25, 23/23, 26/26, 24/24, 35/35, 24/24; sweep 50/50 (re-run as r23b after the review patches: same results).

- Final verification: EditMode 245/245; PlayMode 157/157 in six class batches (+1 new GateLooks test; this run's batch 5 includes Game.Tracer ProbeTests and TracerSceneTests, 6 tests the 1.22 batch run had filtered under the wrong namespace); sweep 50/50; determinism fixture unchanged. Skill check 109.6/81.0/83.5 s.

## Review Triage Log

Pass 1 (quick lens): high 0, medium 2, low 4, false 0, maybe-false 0. Checked OK:
- extractions keep RNG draw order and count;
- `ForRun`/`Builds` match the old conditions;
- hints use the same item as the built gate;
- the skill check is not weakened (same seeds, 60–120 s window);
- timeScale is restored in TearDown.

| Verdict | Route | Finding | Evidence / action |
|---|---|---|---|
| medium | patch | Fixed 400/800/1200 s start fails if earlier fixtures use ≥400 s; "after other classes" was never exercised (CubeMap sorts after Core) | Start aligned to the next spacing multiple past now. Verified CoreSceneTests alone, after the Combat classes, and in verify.sh: identical times. |
| medium | patch | `[Order(0)]` and the pinned start hide engine state carried over from earlier tests; "same time every run" holds only for one test history | Documented as history-scoped in the test and in the notes. |
| low | patch | Interpolation off means real-play aim lag is uncovered | Known-gap comment added. |
| low | reject | Settle after Rebuild went from 3 frames to 1 for every caller of EnterFightFromTown | All 15 Core tests pass in three different histories. |
| low | patch | Physics hint: "any door with a switch" became "last matching door" | Now matches what the face builds; matters only if two doors share a slot. Noted in the plan. |
| low | patch | PhysicsPlan doc comments misplaced | Fixed. |

## Verification

**Commands:**
- Unity batch EditMode; PlayMode `-nographics` `-testFilter` class batches, foreground; sweep. Expected: all pass, 50/50, fixture unchanged (`git diff --quiet` on `DeterminismSignatures.txt`).
