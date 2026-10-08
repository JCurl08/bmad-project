---
title: 'Wind stream screens'
type: 'feature'
ticket: '11'
created: '2026-10-08'
status: 'built'
baseline_revision: '0f00d6b'
route: 'full'
route_source: 'auto'
risk: 'high'
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

**Problem:** The user said: "Wind could behave similarly, they are pushed in a single direction if they move into a wind stream."

**Approach:** The wind hazard becomes wind-stream puzzle screens on the Earth & Atmosphere face:
- **Streams:** a stream carries the player one way, with no steering inside it, until they leave it. Every stream ends on floor or a grip tile.
- **Generation:** a generator plus a solver, at the research targets for wind streams (regions, minimum rides, decoys, the cost of riding back to the entry). Fan switches can appear from the mid tier upward.
- **Goal:** a reward roadblock (4.7), plus doorway routes.

</frozen-after-approval>

## Boundaries & Constraints

- The seed sweep stays at 50/50. Re-record determinism, with a note.
- The subtle-science and voice rules still hold (InWorldText, voices-and-names.md).
- The readability tests stay green.
- PlayMode tests run in class batches.
- Nothing from later Epic 4 stories, except the hooks they need.

## Tasks & Acceptance

- [x] Stream model, solver and generator, on its own stream, at the research targets.
- [x] Runtime stream movement and stream art, including visible flow.
- [x] Earth plan, placement and sweep integration.
- [x] Tests:
  - every wind screen on seeds 1–50 is solvable and within the targets;
  - a runtime test rides one solution;
  - the suites and the sweep pass.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).

- **How many** (*design call*): Earth gets **1 wind screen a run** beside its 2 ice screens (`CubeLayout.WindScreensPerFace`: 1 at 3x3, 0 on the town-less 2x2 test faces). At 3x3 the 8 non-town screens form a ring; less the core, at most 4 puzzle screens fit apart, and only in one pattern, so 2 wind screens would leave Earth almost no ordinary screens (gates, slots, the trial, hazards). 1 always fits.
- **Where.** A new exits-only module `EarthAtmosphere E Wind` (`ScreenModule.IsWindScreen`, `ModuleLibrary.WindModuleIndex`, `IModuleCatalog.WindModuleIndex`) sits just before the ice module. `CubeLayout.PuzzleCellsFor` keeps the island/ice draw unchanged, then walks the same shuffled order again for the wind cell: never the core, the town, an ice cell or beside any puzzle cell. `FaceLayout.WindCells` and `IsPuzzleCell` (ItemPlacement, RewardPlacement and the town check treat wind like ice). `WindPlan.Problems` re-checks count, town, core and adjacency.
- **Grid** (`WindLayout`, pure). 21x13 one-unit tiles centred (cell (10,6) on the centre); it fits inside the half-tile walls with room for the player on the border. *Design call:* the border is the **ring path**: every doorway opens onto it, so all doorways share one region, a crossing costs 0 rides, and early's 3-5 regions are possible (4 separate landings would already be 5). Inside: sky, floating islands 2x2 to 4x3 (twins of 5 wide split by a **wall** column), the goal pad on an island, streams, and **grip anchors** inside streams.
  - Rules (`Ride`, `RideFrom`): stepping from land onto a stream tile, from any side, rides it along its arrows with no steering to the first non-stream tile (floor, goal or grip). A ride that ends where it set off is no move. Streams never end over sky or against a wall, never merge or loop, bend at most twice, and touch land only at their first and last tile (`Problems` checks all of it).
- **Solver:** the 4.10 `PuzzleGraph` over regions (nodes are each region's smallest cell); one move per stream tile set off from.
- **Metrics** (`WindMetrics`, `WindTargets`): every row of research table 1c, per doorway. *Assumptions:*
  - a stream is counted land to land: an anchor is a stop inside its stream, and only a second stream leaving an anchor (a fork) counts on its own;
  - anchors count as regions;
  - a decoy is a ride from a reached region onto a region on no shortest route;
  - return cost is measured to the doorway's region (the ring);
  - every region must be reached and every stream ridable (no clutter);
  - ride length is the stream tiles crossed;
  - retry cost is the return-cost row (rides), as the note's wind table gives it.
- **Fan switches: skipped** (allowed by the story). Mid's 0-1 is met by 0; late's 1-2 is **not met**. Reported as `fans 0 (not built)` in the sweep.
- **Generator** (`WindLayout.Generate`, new stream `RngStreams.WindScreens` = 27, the screen in the seed). Deal islands by tier, the goal, then the recipe's route first (ring, islands, the goal's island), the goal's way home, back-streams from half the route regions, and decoys into a spare region with their way home. A local search follows: add a stream (a BFS router over tile, way and turns), remove one, retarget one, add or remove an anchor, fork off an anchor, or move the goal. A change is kept while `WindTargets.Score` does not grow, with light annealing. Budgets are 400/600/800 steps an attempt by tier; an attempt more than 3 off the targets at half-time is dropped; up to 80 attempts. A layout is kept only with no `Problems` from any doorway; otherwise the tier's hand-checked template (`WindLayout.Templates`). *Assumption:* late deals 3-5 islands; more islands needed more streams than the 11 allowed. Templates on seeds 1-500 at every tier: 0. In the sweep: 0, with 1651 search steps a screen on average.
- **Tier** from Earth's die number, as 4.10.
- **Plan** (`WindPlan`): the screens, cached layouts, `Goals` and `Problems`. `PuzzleScreens.Goals(ice, wind)` puts the ice goals first, then the wind goal. `RegionsAt(math, ice, wind)`. Wind regions for DoorwayReach are as ice: 0 is everything the doorways reach, 1 is the goal's island, joined when solvable and back when the goal can ride home.
- **Rewards and locks.** `CubeWorld` (`Wind`), `EdgeLocks.ForRun`, the sweep, the determinism fixture and the tests pass the wind goal and regions everywhere the ice ones go. The wind goal is a 4.7 Puzzle roadblock in Earth's pool. The 4.9 proof holds: no soft-lock on 50/50 seeds, 2-3 first edges, 0 fallbacks; 5 seeds' kept lock draws changed. `LockedArch.SameRegion` reads wind regions (the mouth strip counts as the doorway's).
- **Runtime.**
  - `WindScreen` draws every tile (`WindArt`, in code): sky with cloud wisps and island undersides, flagstone ring, grass islands, rock walls, stream lanes with chevrons turned per tile, floating grip rocks and the gold-ringed goal. **Visible flow:** white streaks run along every stream, spaced about one per 1.6 tiles at 3.5 u/s, following its bends. Solid cliffs fill the strip round the grid, except the doorway mouths. The reward chest stands on the goal; `WindScreen` is its guard and opens it once the player steps onto the pad.
  - `WindRider` is a second `IMoveDriver`. `PlayerMover` now asks every driver in order, and the first that drives wins.
    - Land is walked one tile at a time; holding a key keeps stepping.
    - A step onto a stream rides it at 2x walk speed through its corners. After landing, the player waits for a fresh press, so a held key never walks it into the next stream.
    - A stream that ends here does nothing. Sky and walls block.
    - The three ring tiles in a doorway's width lead out through it.
    - Reset patterns are as `IceSlider`: `ReferenceEquals` screen change, `CubeNavigator.SetPosition` calls `Clear`, there is a stall backstop, and `PlayerAttack` blocks swings while `Busy`.
  - `CubeWorld.LandingPoint` puts teleports on the ring at the doorway behind the arrival; the reveal comes first, as in 4.10.
  - `EarthFace` builds the wind screens and adds the rider if missing. Dinosaurs and enemies never spawn on wind screens.
- **Wind hazard removed.** `HazardKind.Wind` (0, never reused), `WindBand`, `WindPush` and their tests are gone. Earth deals freezing rain and crevices round the remaining screens. A crevice with no room becomes freezing rain, where it was a wind band. `EarthPlan` keeps hazards and the trial off wind screens (`WindScreens`, `IsWind`); its Problems check it. The dinosaurs' hazard line now explains the wind (199 characters, within the 200 box). The `WindBand` art key stays in the catalog unused (*assumption*: the art catalog is unchanged). Freezing rain and crevices are otherwise as they were.
- **Walls and grip by tier:** early screens are the simplest: 2 rides, mostly no anchors and never walls (a twin needs its own two streams, and early allows at most 5). Mid and late use both (late averages 3 anchors and 1.8 walls).
- **Determinism:** re-recorded twice, and the two recordings are byte-identical.
  - New: `wind` (50).
  - Changed: `layout` (50: Earth's ice module index moved and the wind cell), `items`, `hidden`, `earthPlan`, `rewards` and `streams` (50 each), `biologyPlan` (21), `chemistryPlan` (20), `mathPlan` (13), `physicsPlan` (10), `locks` (5).
- **Tests:**
  - New EditMode `WindTests` (11):
    - the ride rules and the metrics on a hand-made screen;
    - broken streams and an unreachable goal flagged;
    - determinism;
    - every wind screen of seeds 1-50 inside every target from each doorway, with no template; a run's cold generation is under 400 ms (measured max 331 ms, mean 38 ms);
    - seeds 1-500 at every tier with 0 problems and 0 template fallbacks;
    - the templates inside every target;
    - one wind screen a run, beside no other puzzle, with nothing else on it;
    - rewards and reach through the wind regions, with an unsolvable screen flagged;
    - the grid and strip geometry.
  - New PlayMode `WindSceneTests` (3, batch 1 in `vpart.sh` and `verify.sh`), with real keys:
    - the screen is built (tiles, cliffs, a flow per stream, the streaks moving, the chest shut on the goal);
    - land by teleport on the ring, walk out of a doorway and back in;
    - ride the solver's fewest-rides solution ride by ride, each landing where the solver says; a swing mid-ride does nothing; stepping back into the stream that just landed does nothing; stepping on the goal opens the chest; no damage and no backstop;
    - a reroll mid-ride leaves no ride and swings work;
    - an F6 teleport onto an unrevealed wind screen lands on the ring at the doorway behind.
  - Updated: the layout, town, meta-run, Earth (wind out), Earth component and scene (rain stands in for the wind band), reward scene (a wind guard), RNG, determinism, edge-lock and face-repair tests, and the text scan.
- **Cost** (*assumption: acceptable at a run's start*): generating a run's wind screen takes 38 ms on average and 331 ms at most (a late screen, cold). Late averages about 90 ms over seeds 1-500.
- **Regenerated:** the module library (the new module; the other prefabs only gained the serialized `windScreen` flag) and the Cube scene (`WindRider` on the player). `ART-MAP.md` notes the wind art.
- **Screenshots** of an early, a mid and a late screen are in the scratchpad's `snaps411/`. They came from a temporary snapshot test, since deleted. The camera offset is the snapshot harness's, as in 4.10.

- **Review patches (pass 1):**
  - **Doorways:**
    - No stream ever lies on the 3 sky tiles in front of a doorway's width (`WindLayout.IsDoorFront`). The router skips them, `Problems` flags them, and the mid template was replaced by one that respects it.
    - Coming onto the grid (a doorway, a teleport) with a key held that points at a stream, the rider waits for a fresh press, as it does after every landing; a held key never causes a surprise ride. Walking in by a doorway the key points at sky (no stream lies there), so walking in and straight back out stays one press. An earlier version that always waited on arrival broke `CubeSceneTests`' edge walk (a held walk back out of a wind screen never crossed), and v411e fixed it.
    - With the stricter router, one of the 1,500 generations in the seeds 1-500 test needed more than the 80-attempt budget, so the budget is now 160. Only that rare case slows; no seed in 1-50 changed because of it.
  - **Fans** are explicitly excluded from `WindTargets`: not built, never checked, reported as "fans 0 (not built)". The sweep headline now reads "inside every built target of its tier (fan switches not built)".
  - **Crevices:** when a dealt crevice has no room, the next freezing-rain screen with room takes it instead (from its own RNG copy, so that screen's rain is unchanged if it also fails). `EarthPlan.CreviceRoomless` relaxes "a crevice on the face" only when no screen has room. No seed in 1-50 hit the fallback, so `earthPlan` is unchanged.
  - **Text:**
    - The hazard line is 171 characters and says "Up high, follow the gusts" (no arrows).
    - New `Dialogue.GripLine` for dinosaur 2 (a ground dinosaur, `EarthPopulation.GripTeller`): grip rocks stop a ride.
    - Both are in the text scans and the voice check.
  - **Tests:**
    - The cold-generation bound is 2000 ms; the time is logged (max 344 ms).
    - Seeds 1-50 assert each doorway's P_rand, min rides and return cost against the tier's band, and that no stream lies in front of a doorway; the hand-made screen's P_rand is exactly 1.
    - New PlayMode tests: the screen destroyed mid-ride (no teleport) and a reroll mid-ride both leave no ride and swings working; a key held through a landing, and through walking in by a doorway, never rides on and waits for a fresh press.
  - **Determinism:** re-recorded twice (byte-identical). Against the first 4.11 recording only `wind` (50) changed.

## Verification

**Result (2026-10-08, `vpart.sh` steps in the foreground as `v411b`, the sweep re-run as `v411c` after a wording fix):**
- EditMode 348/348.
- PlayMode batches 39/26/46/28/38/32/1, all passing. Batch 1 has the new `WindSceneTests`.
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s (unchanged).
- `IslandWalkTests`: passing.
- Sweep 50/50, 0 failing.
  - Wind: 50 screens (25 early, 15 mid, 10 late), every one of the 200 doorway checks inside every target, no hard traps, 0 from the template.
  - Ice: unchanged, 100 screens, 0 from the template.
  - Locks: no spending order soft-locks on 50/50 seeds.
- `FIXTURE CHANGED` is the intended re-record described above. The fixture is byte-identical across the two recordings.
- Not committed.

**Result after the review patches (2026-10-08, `vpart.sh` steps as `v411d`, partial):**
- EditMode 348/348.
- PlayMode batches 0/1/2: 39/27/46, all passing. Batch 1 has the 4 `WindSceneTests`.
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s.
- Batches 3-6 and the sweep were first run outside this session:
  - batch 4 passed (38/38);
  - batch 3 failed 1 of 28: `CubeSceneTests.EveryFaceEdge_CrossStopsTheWalk_AndAFreshPressBackReturnsToTheSameScreen` never crossed out of a wind screen.
- **Root cause:** the rider always waited for a fresh press on settling, which swallowed a held walk out of a wind screen's doorway. The game was wrong, not the test.
- **Fix:** the rider waits only when the held key points at a stream; `WindSceneTests` was adapted to match.

**Result after the fix (2026-10-08, `vpart.sh` steps as `v411e`):**
- Batch 3: 28/28.
- Batch 1: 27/27, re-run because WindRider changed.
- Batch 5: 32/32.
- Batch 6: 1/1.
- Sweep 50/50, 0 failing.
  - Wind: 50 screens, 0 from the template, every doorway inside every built target.
  - Ice: unchanged.
  - Locks: no soft-locks on 50/50 seeds, 0 fallbacks.
- `FIXTURE CHANGED` is the intended re-record (against the first 4.11 recording only `wind` changed).
- Together with v411d's EditMode (348/348) and batches 0, 2 and 4, every part passes.
- The fixture was re-recorded and checked by the EditMode run; against the first 4.11 recording only `wind` changed.
- Not committed.

## Review Triage Log

Pass 1 (quick lens), triaged by the coordinator: high 0, medium 1, low 6, all patched.

| Verdict | Route | Finding | Outcome |
|---|---|---|---|
| medium | patch | A stream head could sit in front of a doorway, and a held key walking in could set off a ride | No stream on the 3 tiles in front of any doorway (router and `Problems`); the rider waits for a fresh press after settling onto the grid. PlayMode test: a key held through walking in and through a landing never rides on. Re-recorded (`wind` only). |
| low | patch | The cold-generation timing assert could flake | Logged, bound 2000 ms. |
| low | patch | Fans were in the targets but never checked | Explicitly not built and not checked; the sweep headline says "every built target". |
| low | patch | The destroyed-screen branch was only reached via a reroll | A PlayMode test destroys the WindScreen mid-ride with no teleport. |
| low | patch | No test of the fresh-press wait with a held key | A PlayMode test holds the key through a landing and through an arrival. |
| low | patch | The P_rand check was only "above 0" | Hand-made exactly 1; seeds 1-50 checked against each tier's band. |
| low | patch | "No crevice on the face" could fail if both crevice screens fell back; the hazard line was long and spoke of arrows | An owed crevice moves to the next rain screen with room, and the rule is relaxed only when none has room. The hazard line is 171 characters and says "follow the gusts"; a ground dinosaur's new line explains that grip rocks stop a ride. |
