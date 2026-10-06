---
title: 'Refactor sweep'
type: 'refactor'
ticket: '13'
created: '2026-10-06'
status: 'in-progress'
baseline_revision: 'afea9ccb94a1ec0cce6cc6711c57e622695d72c8'
route: 'full'
route_source: 'auto'
risk: 'low'
review: ''
review_source: ''
lenses_ran: []
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/stack.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Twelve stories built in one night left repeated patterns: random-stream numbers scattered across 15 files and coordinated only by comments, near-identical test input helpers in 13 PlayMode files, and similar population and enemy spawn code in each face. That makes Epic 2 riskier to build, because a new stream number or input helper is easy to get wrong.

**Approach:** Cleanup only, with no behaviour change. Centralise the RNG stream ids in one registry with a uniqueness test. Extract a shared PlayMode test input helper and use it across the test files. Extract only the face-content duplication that is clearly identical into shared helpers. Every existing test and the seed sweep must pass unchanged in outcome.

## Boundaries & Constraints

**Always:** No gameplay, tuning, layout or randomness changes. Every stream keeps its current numeric value, so the same seed gives the identical run. Existing test assertions keep their meaning. Tests may be moved onto shared helpers but not weakened. Each extraction must remove real duplication; when two near-copies differ in behaviour, leave them separate. The full EditMode and PlayMode suites and the seed sweep pass afterwards with the same totals or more.

**Never:** No new features, no fixes for deferred items, no scene or asset regeneration unless a renamed type forces it (and then the generated content must be equivalent). Do not touch _bmad or _bmad-output planning files except this plan.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Determinism | Seeds 1–50 before and after the refactor | Identical CubeLayout, ItemPlacement, face plan and hidden-currency signatures | A test compares against signatures recorded before the change |
| Stream registry | All stream ids | Unique, and equal to their old values | Duplicate fails a test |
| Suites | Full EditMode and PlayMode, plus the sweep | All pass | No error expected |

</frozen-after-approval>

## Code Map

- RNG streams (value → owner): 2 `CubeLayout`, 3 `ItemPlacement`, 4 `NpcFactory`, 5 `NpcMover` (= NpcFactory+1), 6 `CubeDebug.EnemySpawnRngStream`, 7 `TownPlan`, 8 `ItemVariants`, 9 `BiologyPlan`, 10 `BiologyFace.PopulationRngStream`, 11 `ChemistryPlan`, 12 `ChemistryFace.PopulationRngStream`, 13 `PhysicsPlan`, 14 `PhysicsFace.PopulationRngStream`, 15 `CoreArena`, 16 `HiddenCurrencyPlacement`. `SeededRng.cs` has a DefaultStream.
- PlayMode tests with their own keyboard and press helpers: Biology, Chemistry and Physics scene tests, the Combat component and scene tests, Core, Cube, Item, Npc component and scene, Probe, Town and Tracer scene tests. Several share the "don't press right after WaitForFixedUpdate, hold for two frames, set the full keyboard state in one event" workarounds.
- Face content: `Faces/Biology|Chemistry|Physics/*Face.cs`, `*Population.cs`, `*Enemies.cs`, and `Faces/FaceSpots.cs`, which is already shared.
- Largest files: `Faces/Physics/PhysicsPlan.cs` (1142 lines), `Core/CoreArena.cs` (603), `CubeWorld.cs` (540). Split only where a seam is obvious; this is optional.

## Tasks & Acceptance

**Execution:**
- [ ] Before any change, record determinism signatures for seeds 1–50 (layout, item placement, each face plan and hidden currency) into a test fixture file under `Tests/EditMode/`.
- [ ] `game/Assets/Scripts/Cube/RngStreams.cs` -- one static class with named constants. Existing `RngStream` constants become aliases of it (or callers switch to it). A test checks the values are unique and equal to the recorded ones.
- [ ] `game/Assets/Tests/PlayMode/TestInput.cs` (shared): press, hold, key-state and frame-safe helpers that encode the known workarounds. Move the PlayMode tests onto it where they duplicate it.
- [ ] Face content: extract the clearly identical parts of population and enemy spawning into shared helpers in `Faces/` only where the three copies match.
- [ ] Optional: split PhysicsPlan into its natural parts (layout, doors, trial, problems) if the seams are clean.
- [ ] Run the full suites and the sweep, and confirm determinism signatures match.

**Acceptance Criteria:**
- Given the full EditMode and PlayMode suites and the seed sweep, when run, then all pass, and the determinism signatures for seeds 1–50 are unchanged.

## Implementation Notes

- STATUS (2026-10-06): code complete and UNCOMMITTED. EditMode 186/186 passed, and determinism signatures for seeds 1–50 are unchanged. The PlayMode run was stopped by Claude Code for low system memory and was not restarted automatically. Still to run: PlayMode with `-nographics` (set env `FACE_SNAPSHOT_OUT` to compare face spawn positions against the implementer's `faces-before.txt` in the session scratchpad, if still present), and the seed sweep. Then delete `Tests/PlayMode/TmpFaceSnapshot.cs` (+ .meta), commit, and run the review.
- Checkpoint 1 was approved under the user's standing instruction (2026-10-05) to keep building while they are away. Review the plan on return.

## Verification

**Commands:**
- Unity batch `-runTests -testPlatform EditMode` and `PlayMode` (with `-nographics`) -- expected: result Passed, 0 failed.
- Unity batch `-quit -executeMethod CubeSceneBuilder.RunSeedSweep` -- expected: 50/50.
