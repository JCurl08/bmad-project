---
title: 'Sliding ice screens'
type: 'feature'
ticket: '10'
created: '2026-10-08'
status: 'built'
baseline_revision: '34f1fff'
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

**Problem:** The user said: "The ice puzzles should be actual sliding puzzles, the entire screen is ice and the character moves in a straight line until they are stopped by something, fall into a pit or they reach a tile with grip."

**Approach:** On the Earth & Atmosphere face, the snow/ice hazard becomes whole-screen ice puzzle screens:
- **Sliding:** the player slides straight, in grid steps, until a rock or wall stops them, they fall into a pit (no damage, back to the entry doorway), or they reach a grip tile.
- **Generation:** a generator plus a solver, following the recipe and tier targets in `puzzle-difficulty-research.md` (minimum slides, number of reachable stops, rock density, pits, grip). The solution is carved backward from the goal, and there are no trap states.
- **Goal:** a reward roadblock from 4.7, plus routes between doorways where the screen connects them.
- **Tier:** set by the face's die number (4.8) if available; otherwise seeded.

</frozen-after-approval>

## Boundaries & Constraints

- The seed sweep stays at 50/50. Re-record determinism, with a note.
- The subtle-science and voice rules still hold (InWorldText, voices-and-names.md).
- The readability tests stay green.
- PlayMode tests run in class batches.
- Nothing from later Epic 4 stories, except the hooks they need.

## Tasks & Acceptance

- [x] Ice grid model, solver and generator, on its own stream, at the research targets.
- [x] Runtime sliding movement (grid-snapped), pits that respawn at the entry, grip tiles, and art.
- [x] Earth plan, placement and sweep integration (DoorwayReach-style reachability through ice).
- [x] Tests:
  - every ice screen on seeds 1–50 is solver-solvable from each connected doorway, within the targets;
  - a runtime test performs one solution;
  - the suites and the sweep pass.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
- **Where.** Earth's pool ends with a new exits-only module, `EarthAtmosphere E Ice` (`ScreenModule.IsIceScreen`). `ModuleLibrary.IslandModuleIndex` now returns a theme's whole-screen puzzle module (`IsPuzzleScreen`: islands or ice), so `CubeLayout` gives Earth 2 ice screens at 3x3 exactly as it gives Math its island screens (stream 21, never the town, the core screen or side by side). No gate, pickup, hidden spot or core portal lands there (no slots); `ItemPlacement` draws an open pickup's screen again while it lands on an ice screen.
- **Grid** (`IceLayout`, pure). 21x11 one-unit tiles centred on the screen (cell (10,5) on the centre), inside the half-tile walls. Tiles: ice, rock, pit, grip, goal pad, snowbank, landing lane, camp.
  - The ice area is centred, by tier: 13x7, 17x9, 21x11. *Assumption:* odd sizes for the research note's ≈12x8, 16x10, 20x12, so the camp sits on the centre cell.
  - *Design call:* every doorway's lane (grip) runs from its doorway to the ice, and its slide runs along a clear line to the **camp**, a plus of grip on the centre (with a small fire). So every doorway starts the same puzzle, every crossing is 2 slides (lane, camp, lane), and DoorwayReach sees the doorways joined like an ordinary screen.
  - Movement rules (`Slide`): onto ice it slides in whole tiles until a rock, bank or edge stops it (on the tile before), it reaches grip, a lane, the camp or the goal pad (stops on it), or it falls into a pit. A step within a lane or the camp is a walk.
- **Solver** (`Puzzles/PuzzleGraph`, generic, for 4.11 too): explores resting places and moves from a `MoveSource`, then gives distances (both ways), traps, shortest-route counts, the best route, out-degree, pit resets and P_rand. *Assumption:* P_rand is computed exactly (a DP over the uniform random player, capped at 3x min), the limit of the note's 2,000-run Monte Carlo, so the sweep stays deterministic.
- **Metrics** (`IceMetrics`, `IceTargets`): every row of research table 1b, per doorway. *Assumptions:* the camp is the shared entry for the detour factor; early's min slides is 4-5 (not 3-5), because a crossing costs 2 and may be at most half the reward route; stops count the camp and goal once and leave the landings out; red herrings are stops on the goal's row or column within 3 tiles that are 3+ moves from it (or never reach it); retry cost is the moves up to and including a fall from the deepest route stop with a pit beside it.
- **Generator** (`IceLayout.Generate`, new stream `RngStreams.IceScreens` = 26, the screen in the seed). The frame is fixed; the goal goes at least 5/7/9 from the camp; rocks, pits and grip are dealt at the tier's counts (plus rocks on 1-3 of the camp's corners); then a search moves, adds or removes one rock, pit, grip tile or the goal at a time and keeps the change when the distance from every target (`IceTargets.Score`) does not grow (light annealing), 800 steps an attempt, up to 30 attempts. *Assumption:* this local search replaces the note's carve-then-repair recipe. A layout is kept only when `Problems` is empty from every doorway; past the retry budget a hand-checked template per tier stands in (`IceLayout.Templates`, review patch), so Generate never returns an unsolved layout. No template was needed on seeds 1-500 at any tier, nor in the sweep (652 search steps a screen on average).
- **Tier** from Earth's die number (4.8): 2-3 early, 4-5 mid, 6 late (`PuzzleTiers`). *Assumption:* no extra step for screens far from town.
- **Plan** (`IcePlan`): the ice screens from the layout, their layouts (cached per seed and screen, so a run generates each once), the goals (`PuzzleGoal`s on the goal pads) and `Problems` (every target, from every doorway, no traps, landings match the doorways). `PuzzleScreens.RegionsAt` joins Math's islands and Earth's ice for DoorwayReach; `PuzzleScreens.Goals` feeds `RewardPlacement.Generate`.
- **DoorwayReach** now reads `IScreenRegions` (IslandLayout and IceLayout implement it). An ice screen has 2 regions: 0 (every doorway, the camp and every reachable stop) and 1 (the goal pad), joined when the solver reaches the goal and back when the goal reaches a landing or a pit.
- **Rewards and locks.** The game (`CubeWorld`), `EdgeLocks.ForRun`, the sweep and the determinism fixture all pass the ice goals into `RewardPlacement.Generate` and the regions (and goals) into `FindProblems` and `LockWorld` (which now takes `puzzles`, so a puzzle reward sits in its goal region). Each ice goal is a 4.7 Puzzle roadblock in Earth's pool. The 4.9 proof still holds: no spending order soft-locks on 50/50 seeds, 2-3 first edges, 0 fallbacks; only 5 seeds' kept lock draws changed.
- **Runtime.**
  - `IceScreen` draws every tile (`IceArt`, in code: ice with a glint and tile seams, boulders, dark pits with a pale rim, gritty grip, the gold-ringed goal pad, snowbanks) and puts solid snowbanks over the strip between the grid and the walls, except each doorway's mouth. Their colliders keep 0.15 off the grid, so a player on a border tile never touches them.
  - `CubeWorld.PlaceRewards` builds each puzzle chest on its goal and hands it to the screen's `IPuzzleRewardHost`; the `IceScreen` is the chest's guard, open once the player comes to rest on the pad.
  - `IceSlider` (on the player; the scene builder adds it, and `EarthFace` adds it if missing) is a new `PlayerMover` hook, `IMoveDriver`, which takes movement over on an ice grid. It settles the player on a tile (coming in by a doorway: its lane), runs each move by `IceLayout.Slide` (slides at 1.5x walk speed, no steering), and steps grip one tile at a time (holding keeps stepping). A pit sinks the player for 0.6 s, with no damage, and puts it back on the lane it last came in by. Walking off a lane toward its doorway leaves the grid as usual. A stall backstop exists but never fired in the tests.
  - `CubeWorld.LandingPoint` puts teleports on a doorway's lane; `LockedArch.SameRegion` counts a doorway's lane and mouth.
  - Earth's dinosaurs and enemies never spawn on ice screens.
- **Snow hazard removed.** `HazardKind.Snow` (1, never reused), `SnowPatch`, `SnowGrip` and their tests are gone. `EarthPlan` deals wind, freezing rain and crevices round the other screens, never on an ice screen (`IceScreens`, `IsIce`) and never the trial there; its Problems check it. The dinosaurs' hazard line now explains the ice. `PlayerMover`'s grip modifier stays (generic, unused).
- **Wand interaction** (*design call*):
  - The snow Stormcaller* freezes water. The ice screens have no water (their pits are holes, not pools), so it does nothing there and never changes the solver's puzzle.
  - No swing happens while sliding or falling (`PlayerAttack` asks `IceSlider.Busy`). A swing while at rest works as anywhere, and hits nothing on the ice.
  - Freezing rain, wind and crevices never land on ice screens.
- **Text:** the "Made it!" and "Whoops! Back to the start." popups and the new hazard line are in the InWorldText scan.
- **Determinism:** re-recorded twice, and the two recordings are byte-identical.
  - New: `ice` (50).
  - Changed: `layout` (50: Earth's 2 ice cells), and so `items` (50), `hidden` (50), `earthPlan` (50), `rewards` (50), `streams` (50: the `iceScreens` stream). These follow the new placement: `biologyPlan` (21), `chemistryPlan` (15), `mathPlan` (11), `physicsPlan` (8), `locks` (5).
  - Everything else is unchanged.
- **Tests:**
  - New EditMode `IceTests` (10): the slide rules on a hand-made screen, its metrics and regions, tiers, determinism, every ice screen of seeds 1-50 inside every target from each doorway (and a run's cold generation under 250 ms), Generate over seeds 1-500 at every tier never returning an unsolved layout, the templates inside every target, nothing else on ice screens, and rewards and reach through the ice regions, with an unsolvable screen flagged. `EarthTests` keeps a pure test of PlayerMover's grip path (kept for the generic modifier hook; no hazard uses it now).
  - New PlayMode `IceSceneTests` (3, batch 1 in `vpart.sh` and `verify.sh`), with real keys, on the first seed whose ice screen has a pit a 3+-tile slide reaches (a mid or late screen):
    - land by teleport, walk out of a doorway onto the next screen and back in onto its lane;
    - walk and slide to a stop beside that pit, slide in (a swing mid-slide does nothing), sink, and come back on the landing at full size, unhurt, waiting for a fresh press;
    - rest on a tile beside the goal pad: the chest stays shut;
    - play the solver's fewest-moves solution move by move, each slide resting where the solver says; resting on the goal pad opens the chest, and the stall backstop never fired;
    - a screen lost mid-fall and a reroll mid-slide leave no slide, fall or shrinking behind, and swings work again;
    - a teleport onto an unrevealed ice screen (the F6 case) reveals first and lands on the lane of the doorway behind the arrival, never on or beside the goal.
  - Updated: the Earth tests (snow out, ice-aware), the layout, town and module tests (Earth's ice module), `RngStreamsTests`, `DeterminismTests`, `EdgeLockTests` and `FaceRepairTests` (with ice goals), `RewardSceneTests` (the puzzle chest), and the text scan.
- **Cost:** generating a run's 2 ice screens takes 35 ms on average, 107 ms at most (EditMode, cold cache). Rebuild runs it once in `EdgeLocks.ForRun`, and the reveal reuses the cache. *Assumption:* acceptable at the start of a run. The 4.9 `ForRun` timing test (max 30.5 ms) measures warm caches, because other tests generate the screens first.
- **Regenerated:** the module library (the new module; the other prefabs only gained the serialized `iceScreen` flag) and the Cube scene (`IceSlider` on the player). The art catalog and item catalog are unchanged. `ART-MAP.md` notes the in-code ice art.
- **Screenshots** are in the scratchpad's `snaps410/`: one early, one mid and one late screen, and a mid screen with the goal reached. They came from a temporary snapshot test, since deleted.

- **Review patches (pass 1):**
  - `IceSlider` resets on any placement: `CubeNavigator.SetPosition` calls `IceSlider.Clear` on teleports, crossings and rebuilds. It also resets when its screen is destroyed (`ReferenceEquals`, since a destroyed screen compares equal to null). A reset ends any fall at full size. `Busy` and `IsFalling` need a live screen.
  - **Generate never returns an unsolved layout.** A layout is kept only with no `Problems` from any doorway. Past the 30-attempt budget, the tier's template stands in. The sweep reports template use (0 on seeds 1-50) and the reveal logs an error if `IcePlan.Problems` is not empty.
  - **After a pit respawn:**
    - the player waits for a fresh press (`StopUntilFreshPress`);
    - the jump to the landing turns interpolation off for two steps, and the scale comes back after the jump;
    - with no entry recorded, it lands on the nearest landing.
  - **Landing on an ice screen:**
    - `TeleportTo` reveals before it asks `LandingPoint`, so an F6 onto an unrevealed ice screen lands on the lane of the doorway behind the arrival;
    - settling never picks the goal pad, and prefers the tile the player already stands on.
  - Cosmetics: the ItemPlacement summary (ice screens), the ScreenModule tooltip indent, the SeedSweep spacing. Fixture unchanged by the patches (the same layouts are kept).
## Verification

**Result (2026-10-08, `vpart.sh` steps in the foreground as `v410a`):**
- EditMode 334/334.
- PlayMode batches 39/22/46/28/38/32/1, all passing. Batch 1 has the new `IceSceneTests`; the snow test is gone.
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s (unchanged).
- `IslandWalkTests`: passing.
- Sweep 50/50, 0 failing. Ice: 100 screens (50 early, 30 mid, 20 late), all 400 doorway checks inside every target, no hard traps. Locks: no spending order soft-locks on 50/50 seeds.
- `FIXTURE CHANGED` is the intended re-record described above.
- Not committed.

**Result after the review patches (2026-10-08, `vpart.sh` steps as `v410b`):**
- EditMode 337/337 (3 new: seeds 1-500 at every tier with 0 template fallbacks, the templates, and the grip path; the cold-generation guard is under 250 ms, measured max 106 ms).
- PlayMode batches 39/24/46/28/38/32/1, all passing. Batch 1 has the 3 `IceSceneTests`.
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s.
- `IslandWalkTests`: passing.
- Sweep 50/50, 0 failing. Ice: 100 screens, 0 from the template, 0 seeds failing. Locks: no soft-locks on 50/50 seeds.
- The fixture is byte-identical to v410a (`FIXTURE CHANGED` is against the baseline).
- Not committed.

## Review Triage Log

Pass 1 (quick lens), triaged by the coordinator: high 0, medium 2, low 7, all patched.

| Verdict | Route | Finding | Outcome |
|---|---|---|---|
| medium | patch | IceSlider kept a destroyed screen's state (Unity-null compare), leaving a fall shrunk and swings blocked | Resets on `ReferenceEquals` change and on every navigator placement (`Clear`); `Busy` needs a live screen. PlayMode test: a screen lost mid-fall and a reroll mid-slide leave swings working and the scale normal. |
| medium | patch | Generate could return its best miss (an unsolved layout) | Kept only with no `Problems`; template per tier past the budget; reveal logs an error on any ice problem. EditMode tests: seeds 1-500 x 3 tiers (0 problems, 0 fallbacks) and the templates. |
| low | patch | A held key after a pit respawn walked the player out of the doorway | `StopUntilFreshPress` after the respawn; asserted in the scene test. |
| low | patch | F6 onto an unrevealed ice screen landed at the centre (no entry) | `TeleportTo` reveals first, so `LandingPoint` gives the lane; settling never picks the goal; the respawn falls back to the nearest landing. PlayMode test over 3 seeds x 2 screens x 4 facings. |
| low | patch | A teleport mid-move onto the same ice screen kept Moving | `CubeNavigator.SetPosition` clears the slider. |
| low | patch | Respawn drew an in-between frame at full size | Interpolation off for two steps; the scale is restored after the jump. |
| low | patch | Tests: a conditional swing check, no shut-chest-beside-the-pad check, no cold timing guard, the plan's runtime-test text, the dead grip path | The pit slide is 3+ tiles and the swing is tried unconditionally; the chest stays shut beside the pad; the cold generation is under 250 ms; the plan text is fixed; the grip path is kept with a pure EditMode test (generic hook). |
| low | patch | Cosmetics | ItemPlacement summary, ScreenModule indent, SeedSweep spacing. |
| note | none | Camera offset in the snapshots | The snapshot harness; no change (coordinator). |
