---
title: 'Refactor sweep (world)'
type: 'refactor'
ticket: '15'
created: '2026-10-08'
status: 'built'
baseline_revision: '6b51e67'
route: 'full'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/faces-and-races.md'
  - '{project-root}/_bmad-output/initiative-video-game/change-playtest-3-world-redesign/change-playtest-3-world-redesign.md'
  - '{project-root}/_bmad-output/initiative-video-game/epic-world-worth-exploring/puzzle-difficulty-research.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Stories 4.1–4.14 added many systems in two days: scale, character art, voices, towns, interiors, rewards, fragments, locks, ice, wind, bridges, the lair and temples. Their reviews deferred cleanups. Real runs also use clock seeds, not only seeds 1–50, so the solvability guarantees must hold for any seed.

**Approach:** cleanup only, with no gameplay change.
- **Consolidate shared patterns:** puzzle-screen plumbing (islands, ice, wind), sub-screen rooms (arena, interiors, lair), and the reward and lock hooks.
- **Remove dead code** left over from the replaced hazards and puzzles.
- **Robustness for any seed:** an EditMode generation-only sweep over seeds 1–500. It checks layout, items, rewards, locks proof and puzzle screens, with zero problems and no fallbacks. A runtime guard logs an error if any generated plan has problems at reveal.
- **Determinism:** the fixture stays unchanged without re-recording.

</frozen-after-approval>

## Boundaries & Constraints

- The seed sweep stays at 50/50. The determinism fixture must stay unchanged.
- The subtle-science and voice rules still hold (InWorldText, voices-and-names.md).
- The readability tests stay green.
- PlayMode tests run in class batches.
- Nothing from later Epic 4 stories, except the hooks they need.

## Tasks & Acceptance

- [x] Consolidate the duplicates found in the 4.1–4.14 plans and reviews (extract only true duplicates; draw order unchanged).
- [x] Remove dead code.
- [x] Add the 500-seed generation-only robustness test and the reveal-time problem logging.
- [x] Verify: all suites pass, the sweep is 50/50, and the fixture is unchanged.

## Routed from earlier reviews

- From 4.17: the verify batch lists (`vpart.sh` and `verify.sh`) live only in the session scratchpad. Move a verify script into the repo, for example `tools/verify.sh`, with every PlayMode test class in a batch. Add a check that fails when a test class is in no batch.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
- **Consolidated (true duplicates only, behaviour and draw order unchanged):**
  - **Sub-screen rooms:** `SubScreenRooms<TRoom>` (new, `Scripts/Cube/SubScreenRooms.cs`) holds what `TownInteriors`, `LairRooms` and `TempleRooms` each copied: the serialized world and navigator (same field names, so the scene loads unchanged), the subscription to `Rebuilt`, `ResolveNavigator`, the per-face room dictionary and root object, `Current`, `Entries`/`Exits`, the `Entered`/`Left` events, `Clear` and `OnDestroy`. Each subclass keeps its own going in and out. `SubScreens` gives the room screen and centre per row (all three rows), the player's position, and `PlaceOut` (place, stop, and the 4.13 fresh-press wait where the copy had it: lair and temple, not town rooms). `TownInteriors.ReturnPoint(sill, facing)` is the one return-point formula (temples call it); `TownInterior.IsPastExit` is shared by the town and lair room views.
  - **Puzzle-screen plans:** `PuzzleScreenLayouts` (`DoorMask`, `HasDoor`, `ScreenSeed`) and `BoundedCache` replace the identical copies in `IcePlan`, `WindPlan` and `IslandPlan` (and `TemplePlan`'s cache, limit 128 kept). Same keys, same seeds, same streams.
  - The reveal-time puzzle checks in `CubeWorld` (three copies) are now one guard (below).
  - **Not merged (they differ):** the core arena (built once, fight state, its own rebuild handling); `IceScreen`/`WindScreen` (only `AttachReward`/`ReachGoal` match, about 15 lines; art, flows and strips differ); the popup de-duplication (`IslandScreen` skips the same words still up, `TempleTargets` replaces the last popup: two different rules); the reward and lock hooks were already shared (`IPuzzleRewardHost`, `LockInput.PressIsFree`).
- **Dead code removed:** `ArtKey.WindBand` and `SnowPatch` (enum, category, builder picks, the two `ArtCatalog.asset` entries, `ART-MAP.md` rows; values 100-101 never reused); `HazardSpec.Direction` (always zero since 4.11); `GamePause.IsHeld`; the unused leftovers of the replaced puzzles: `IslandDesign.Mirrored`, `IslandBridge.AsBroken`, `PlankLights.PlankAt`, `IslandScreen.IslandWorld`, `DoorwayReach.IslandsOn`, `IceLayout.IsGripLike`/`IsCrossLine`, `WindLayout.RegionOf`, `LairPlan.LairIndex`. Found by a reference scan (each had no caller in scripts, editor or tests).
- **Robustness for any seed:**
  - `RunPlanCheck` (pure, new): the puzzle screens' problems and template fallbacks always; on a whole cube with a library also the core entrances, the lair, the temples (and their templates) and a lock fallback; with the placement, the item softlock check and the reward check. `CubeWorld.RevealScience` logs one error naming every problem (replacing the three puzzle logs).
  - New EditMode `RobustnessTests` (2): seeds 1-500 run through the whole `SeedSweep` (every check flag set), no puzzle screen or temple from a template, `EdgeLocks.ForRun` never the fallback and equal to the swept locks, and `RunPlanCheck` silent on the same plans; plus a test that the guard names a fallen-back lock draw. **Result: 500/500 seeds, 0 problems, 2500 puzzle screens and 3000 temples with 0 templates, at most 37 lock draws (of 64). No issue found, nothing to fix.** Run time 378 s (sweep 271 s, `ForRun` and the guard 107 s). The raw generators at every tier stay in `IceTests`, `WindTests`, `IslandPuzzleTests` and `TempleTests`.
- **Repo verify script:** `tools/verify.sh` (+ `tools/README.md`, `tools/.gitattributes` keeping LF): parts edit, 0-7, sweep or all, one Unity at a time; Unity from `ProjectVersion.txt` and the Hub's install path per OS, the project from the script's folder, results in `$PROJECT/Logs/verify`; `UNITY`, `PROJECT`, `OUT` override; `--list` prints the batches. Same batches as the scratchpad's `vpart.sh`. New EditMode `VerifyScriptTests` (2) parse `BATCHES` and every PlayMode test class: each class in exactly one batch, no batched class missing.
- **Spec re-derived** (bmad-spec, memlog appended only): CAP-29 (temples and gems, Town's lost-and-found), new CAP-33 (quest log), the conversation freeze constraint, fixed benefits per face (open question closed), Town's mixed residents and looks, bridge readability in CAP-7, robustness for any seed; the optional-temple assumption retired. Self-validate events logged (both PASS).
- **Determinism:** nothing re-recorded; the fixture is unchanged.

## Verification

**Result (2026-10-08, `r415`, one Unity at a time):**
- EditMode 393/393 through the new `tools/verify.sh r415 edit` (13.5 min; 4 new tests).
- PlayMode batches (`vpart.sh`) 39/29/54/28/38/37/4/13, all passing. Skill checks unchanged: 78.6, 90.5 and 105.5 s. No reveal-guard error in any batch log.
- Sweep 50/50, 0 failing. Fixture unchanged.
- Not committed.

**Result after the review patches (2026-10-08, `r415b`, one Unity at a time):**
- `tools/verify.sh r415b edit`: EditMode 394/394 without the `Slow` category (8.7 min; 2 new tests).
- `tools/verify.sh r415b robust`: 1/1, the 500-seed test (5.0 min; sweep 182 s, all checks 274 s; 500/500, 0 templates, at most 37 lock draws).
- PlayMode batch 5 (sub-screen rooms) 37/37 and batch 7 (temples) 13/13 (`vpart.sh`); no reveal-guard error in their logs.
- Sweep 50/50, 0 failing. Fixture unchanged.
- Not committed (`tools/verify.sh` is staged only, for its executable bit).

## Review Triage Log

Pass 1 (quick lens), triaged by the coordinator: high 0, medium 2, low 6, all patched.

| Verdict | Route | Finding | Outcome |
|---|---|---|---|
| medium | patch | `verify.sh` passed a part on Unity's exit code alone | A test part also fails when its results XML is missing (deleted before each run), has no counts, a total of 0, or any failure; the summary says "no results". |
| medium | patch | The fixture check compared with the index, and FIXTURE CHANGED did not fail | `git diff --quiet HEAD --`; FIXTURE CHANGED sets the part's code to 1; `ALLOW_FIXTURE_CHANGE=1` reports it as allowed for an intended re-record. |
| low | patch | A part name could be taken as the tag (`verify.sh edit`) | Refused with exit 2 (edit, robust, sweep, all, or a number). |
| low | patch | `tools/verify.sh` had no executable bit | Staged with `git update-index --chmod=+x` (100755); not committed. |
| low | patch | The 500-seed test made every EditMode run about 6 minutes longer | `[Category("Slow")]`; `edit` runs `-testCategory "!Slow"`, the new `robust` part runs `Slow`, and `all` includes it; README updated. Both parts proven through the script. |
| low | patch | `RunPlanCheck` built island regions without Math's plan, and its reveal cost was unknown | Optional `math` (the run's `MathPlan`, from `MathFace`) gives Math's regions first, as the sweep does; the doc comment says why `IslandPlan` agrees without it. Measured cold (temples uncached), seeds 1-10: mean 144 ms, most 345 ms (new EditMode test logs it). Over 30 ms, so the guard runs only in the editor and development builds. |
| low | patch | `SubScreenRooms.Subscribe` called the virtual `OnDisable` | A private `Unsubscribe()`; `OnDisable` calls it. |
| low | patch | `VerifyScriptTests` missed classes at other indents, with `partial`/`static`, or in file-scoped namespaces, and counted commented-out code | Comments are blanked first (strings kept); outermost classes at any indent and modifiers, block or file-scoped namespaces; nested classes count for their outer class; commented batch lines are skipped. New test of those layouts. |
