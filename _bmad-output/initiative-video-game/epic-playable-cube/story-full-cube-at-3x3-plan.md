---
title: 'Full cube at 3x3'
type: 'feature'
ticket: '17'
created: '2026-10-06'
status: 'built'
baseline_revision: '101d1dcf2789f71ab93940cb01380576a5df6836'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/stack.md'
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/faces-and-races.md'
  - '{project-root}/_bmad-output/initiative-video-game/change-playtest-feedback-full-cube/change-playtest-feedback-full-cube.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The approved course correction makes the Must tier a full cube of 6 faces at 3×3 screens (CAP-1). Everything is built and tested at 2×2: Town has only 4 modules, and the face plans, trial fits, boulder reach and hidden-currency placement have only been exercised at N=2 (some sweep tests also ran at N=3).

**Approach:** Set the single face-size setting to 3 and add enough Town modules for 9 distinct Town screens. Then make every existing system hold at 3×3: Biology, Chemistry and Physics plans, trials and populations, item and gate placement, hidden currency, town population, the core portals, the run loop and the seed sweep. Math and Earth & Atmosphere stay sealed until stories 1.19 and 1.20. Re-record the 1.13 determinism signatures, because the layout changes by design.

## Boundaries & Constraints

**Always:**
- **One setting:** N is the single setting `CubeSettings.DefaultFaceSize = 3`, and nothing hard-codes 2.
- **Town:** has 9 distinct modules, one per cell, and stays the fixed start.
- **Every sweep rule passes at N=3 on 50 seeds:** reachability, core entrances, item collection with no softlocks, optional gates, Chemistry trial fit, Physics boulders and trial, hidden currency.
- **Still deterministic:** the same seed gives the same run.
- **Tests:** existing tests keep their meaning. Tests that hard-coded 2×2 geometry are updated to derive from N.
- **Determinism fixture:** re-recorded once, with a note in Implementation Notes.
- **Tuning:** the boss, combat and economy are unchanged.
- **Density:** tune per-face content counts so a 3×3 face still feels populated. Use roughly the same number per screen, not per face, where the design was per-screen.

**Never:**
- No Math or Earth content (1.19 and 1.20).
- No isotope rework (1.18).
- No new art beyond Town module variety.
- No change to screen size (16×10).
- No WebGL build in this story.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Size | New run, any seed | Every face is 3×3 (9 screens); Town shows 9 distinct modules | No error expected |
| Navigation | Walk off every edge of every face | Lands on the correct adjacent face/cell at 3×3 | Sealed faces still block |
| Sweep | Seeds 1–50 at N=3 | Every rule passes (reachability, core entrance per built face, items without softlocks, optional gates, Chemistry trial fit, Physics boulders and trial, hidden currency) | Sweep names failing seed and rule |
| Faces | Full run through Biology, Chemistry, Physics at N=3 | Their items, gates and trials work as before | No error expected |
| Boss/run loop | Enter core from each built face; die; continue | Unchanged behaviour at 3×3 | No error expected |
| Determinism | Same seed twice | Identical run; fixture re-recorded once | Test fails on drift |

</frozen-after-approval>

## Code Map

- `game/Assets/Scripts/Cube/CubeTypes.cs` -- `CubeSettings.DefaultFaceSize = 2` (the one setting). `CubeModel`, `CubeWorld` and `SeedSweep` all read it.
- `game/Assets/Editor/ModuleLibraryBuilder.cs` -- `Designs` (4 entries) drives both Town modules (one per design, around line 123) and the science pools (around line 134). Town needs 9 distinct modules, so add Town-only designs or variants and keep the science pools.
- `game/Assets/Scripts/Cube/Modules/ModuleLibrary.cs` -- `TownModule(cell, faceSize)` wraps an index (the deferred item). It must index 9 modules at N=3 with no wrap.
- Face plans: `Faces/Biology/BiologyPlan.cs`, `Faces/Chemistry/ChemistryPlan.cs`, `Faces/Physics/PhysicsPlan*.cs` (split in 1.13), `Items/ItemPlacement.cs`, `Items/HiddenCurrencyPlacement.cs`, `Town/TownPlan.cs`, `Faces/FaceContent.cs` / `FaceSpots.cs`. Check every count that was per face against per screen.
- Tests: `Tests/EditMode/*` (several sweeps already parameterise N=2/N=3), `Tests/PlayMode/*SceneTests.cs` (some walk fixed distances on 2×2 faces). `Tests/EditMode/DeterminismTests.cs` plus `DeterminismSignatures.txt`: re-record with `-executeMethod Game.Cube.Tests.DeterminismSignatures.Record`.
- `deferred-work.md` -- the Town-at-3×3 item is resolved here.

## Tasks & Acceptance

**Execution:**
- [x] Set `DefaultFaceSize = 3`. Search for and remove any remaining 2-assumptions.
- [x] Add at least 5 more Town designs (9 total), visually distinct and following `KeepsExitsOpen`. Index them per cell with no wrap at N=3, and keep wrap only as a fallback for larger N. Regenerate the library.
- [x] Re-tune per-face content counts (gates, NPCs, enemies, boulders, hidden currency, trial placement) so density per screen stays similar.
- [x] Fix any plan or sweep rule that fails at N=3, with the root cause noted.
- [x] Update tests that assume 2×2 geometry so they derive from N.
- [x] Regenerate the Cube scene and re-record the determinism signatures.
- [x] Full verification: EditMode, PlayMode (`-nographics`) and the sweep.

**Acceptance Criteria:**
- Given the full suites and the 50-seed sweep at N=3, when run, then all pass.
- Given the Cube scene, when a run starts, then all faces are 3×3, Town shows 9 distinct screens, and Biology, Chemistry and Physics play as before.

## Implementation Notes

- Verified in batch mode after the review patches: EditMode 198/198, PlayMode 120/120, seed sweep 50/50 at N=3. Known gap until 1.18: far Chemistry glow/unstable gates may be out of timer reach.
- Checkpoint 1 approved under the user's standing instruction (2026-10-06: "You can keep iterating on yourself as you go").
- **N:** `CubeSettings.DefaultFaceSize = 3`. No other 2-assumption was found in game code; tests that sweep N keep `{2, 3}`.
- **Town:** 5 Town-only designs (E Plaza, F Colonnade, G Walls, H Garden, I Ring) follow the 4 shared ones, so there are 9 distinct Town modules. `ModuleLibrary.TownModule` indexes per cell and only wraps when N needs more than the library has. Ruin shapes now step by 1 for modules 0-4 and by 2 for 5-9, so every module mixes its shapes differently. Modules 0-3 are unchanged.
- **Density** (`CubeSettings.PerScreen`, tuned on 4 screens, rounded half up; the same values at N=2):
  - Enemies: 2+1 per face becomes 5+2.
  - Face NPCs: 3 becomes 7.
  - Town NPCs: the whole Town population scales per screen (7-9 on 4 screens becomes 16-20 on 9). The 5 race NPCs stay one each, so Townsfolk go from 2-4 to 11-15.
  - Flower-vine gates in a thin-beak run: 1 becomes 2 (`BiologyPlan.FlowerVineCount`), each flower on its own adjacent Biology screen. The sweep rule expects that count, or fewer only when there are fewer home gates.
  - Home gates per item: 2 becomes 5.
  - Optional gates per variant: 2 becomes 5.
  - Beak hits step: 3 becomes 7 gates per extra hit (`BiologyPlan.HitsFor(difficulty, faceSize)`; the beak gate builders take faceSize, so hits are fixed when the gate is built), so a gate takes the same hits as before at the same gates per screen.
  - Unchanged on purpose: off-home gates (1 per item, a per-item rule), the trial (1 per face), the core entrance (1 per face), and boulders (already per door on its own screen).
  - Hidden currency stays 4-6 per run at 5 each. That was an economy decision ("about 4-6 per run" in 1.12), and economy is unchanged.
- **Root cause fixed:** `TownPlan.BlockedRects` now snaps rect edges to 0.001. On a placed module, `InverseTransformPoint` put edges at, for example, -5.4999995 instead of the prefab's -5.5. A spot that only touched an edge on the prefab then overlapped it on the instance, so the live Town spots differed from the seed's pure spots. This showed on the new Ring module at (2,2). FaceSpots and PhysicsPlan use the same rects, so the swept and played layouts can't drift the same way.
- **Determinism fixture** re-recorded for the final N=3 content (550 lines change against the baseline, which is the intended N=3 layout change) with `Game.Cube.Tests.DeterminismSignatures.Record`.
- **Verified 2026-10-06 in batch mode:** EditMode 198/198, PlayMode 120/120 (`-nographics`), seed sweep 50/50 at N=3. The sweep found 300 timed doors (50 off-home), the Physics trial fit on 50/50 seeds, and 242 hidden items. The module library and the Cube scene were regenerated. Playthrough by eye is PENDING (user).

## Review Triage Log

Pass 1 (quick lens): high 0, medium 2, low 5, false 0, maybe-false 0.

| Verdict | Route | Finding | Evidence / action |
|---|---|---|---|
| medium | defer | Chemistry timed stages (glow and unstable) may not reach far home gates at 3×3 | 12 s glow × 6 u/s ≈ 72 u, versus corner-to-corner ≈ 52 u plus detours, and no distance rule exists. The timer is removed by story 1.18 (next), so it is resolved there. A user playtest between 1.17 and 1.18 may hit it. |
| medium | patch | Town NPC density per screen drops about 35% | Only Townsfolk were scaled. The total per screen is now scaled. |
| low | patch | One flower-vine per face regardless of N | The flower share drops from 1/2 to 1/5. Now scaled per screen, with the sweep rule updated. |
| low | patch | Beak gate hits depend on FaceSize set after Build | Now fixed at construction. |
| low | patch | Beak difficulty component test restates the formula | Now concrete tuned cases on real gates. |
| low | patch | Town distinctness test throws NRE on a missing module | Now asserts not-null first. |
| low | reject | Chemistry stage-variety test weaker at N=3 | Chemistry stages are rewritten in 1.18, whose tests replace this one. |

## Verification

**Commands:**
- Unity batch `-runTests -testPlatform EditMode` and `-testPlatform PlayMode -nographics` -- expected: Passed, 0 failed.
- Unity batch `-quit -executeMethod CubeSceneBuilder.RunSeedSweep` -- expected: 50/50 at N=3.
