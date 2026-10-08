---
title: 'Bridge puzzles, tuned'
type: 'feature'
ticket: '12'
created: '2026-10-08'
status: 'built'
baseline_revision: '9a04552'
route: 'full'
route_source: 'auto'
risk: 'medium'
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

**Problem:**
- The user liked the spread-out island screens. They also want "some indication that it is activating something. Like the bridge changing colour".
- The user asked that puzzles get a reasonable difficulty, based on similar puzzles in other games.
- The research note recommends a "light every plank" rule (an Euler trail).

**Approach:** Rework Math's island screens into a "light every plank" puzzle:
- **Lighting:** each whole plank on the reward network lights up the first time it is crossed.
- **Reset:** crossing a lit plank again snuffs every plank, with a red flash. That lets any island be a clean start.
- **Goal:** when every plank is lit, a light chain runs to the reward roadblock (4.7) and opens it.
- **Persistence:** leaving the screen resets an unsolved puzzle. A solved one stays solved for the run.
- **Broken spans:** they count once mended, and then show gold.
- **Feedback:** lamps on the roadblock show the count, and an island's rim glows when all its planks are lit.
- **Difficulty:** the tiers come from the research targets (plank counts, odd islands, random-walk success rate).
- **Lone doorway:** keep 1.26's readable lone doorway, or replace it with something better that the research suggests.

</frozen-after-approval>

## Boundaries & Constraints

- The seed sweep stays at 50/50. Re-record determinism, with a note.
- The subtle-science and voice rules still hold (InWorldText, voices-and-names.md).
- The readability tests stay green.
- PlayMode tests run in class batches.
- Nothing from later Epic 4 stories, except the hooks they need.

## Tasks & Acceptance

- [x] Puzzle rule and state (light, snuff, solved), with feedback lamps and rim glow.
- [x] Generator and solver to the targets (connectivity, parity, start reachable from each doorway, exhaustive trail count up to 16 planks).
- [x] Reward roadblock integration and sweep.
- [x] Tests:
  - every island screen on seeds 1–50 is solvable from each connected doorway, within the targets;
  - the roadblock opens only when the goal is met;
  - a runtime test solves one;
  - the suites and the sweep pass.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
- **The rule** (`PlankLights`, pure, reworked from 1.26). The lit network is every whole plank of the reward island's whole-plank component (`IslandSolver`). A crossing (`PlankWalker`: off one island, onto a plank, off it onto the island at its other end; onto a plank and back, lanes and landings never count) of a dark network plank lights it; of a lit one snuffs every plank (that one too); the last dark plank solves the network for the run. Leaving the screen resets an unsolved network (no flash); a solved one stays lit. Broken spans never light or snuff: mended, they show gold. Planks off the network (the lookouts) never light. `PlankLight.Walked` became `Lit`.
- **Doorway routing and the lone doorway** (*design call*): **the lone doorway is gone.** The doorways are still dealt into roles (`IslandGenerator`): every open doorway but the third sits on the lit network (its island is a network island), and the third (the gap doorway) reaches it only over one broken span, so the gap-crosser* keeps its job on island screens and `DoorwayReach` still has a gap-only join. The gap doorway's own island carries the tier's red herrings (lookout planks off the network). `IslandLayout.Problems` now flags a lone doorway (and no longer requires one); 1.26's sign and "go round" popup stay for a hand-made lone doorway but generated layouts have none. Why: the research names the isolated doorway as the "impossible" trap, and with doorways on the network every connected doorway is a valid entry ("start anywhere").
- **Lattice:** still 5x3 spots. *Assumption:* a plank may also join two spots two apart in one row or column over an empty middle spot (a long plank); two long planks never share a middle (they would cross). Without it the grid is bipartite, and the early circuit of 5-7 planks would only ever be a 6-ring.
- **Solver** (`IslandSolver`, pure, no Unity types; `IslandMetrics`): connectivity (the doorway reaches the network, over spans with the gap-crosser*), parity (0 or 2 odd islands), a start reachable from each doorway (a start of the trail that is the entry island, or has 2+ planks so stepping out and back snuffs everything and leaves the player on it), the exhaustive trail count (a DP over (island, lit mask), up to `MaxExhaustivePlanks` = 16) and P_rand, exactly. Also red herrings, spans inside the network, spread, start walk, crossing vs reward route, retry seconds (walk at 9 u/s to the nearest clean start, plus out and back), and hard traps (0: a snuff is always one crossing away).
  - *Assumption (P_rand):* a random player starts on a uniformly random network island and crosses a uniformly random dark plank until none is left; computed exactly (the limit of the note's Monte Carlo), so the sweep stays deterministic.
  - *Assumption (crossing):* a mended span counts as no planks (it is the gap-crosser*'s lock, crossed in one step); otherwise an early loop could never hold the gap doorway's crossing under half its reward route.
  - *Assumption (spread):* the share of the lattice inside the network's bounding box.
- **Targets** (`IslandTargets`): planks, odd islands (and how many are a doorway's) and P_rand are the note's own: early 5-7, 0 odd, P_rand >= 40%; mid 8-11, 2 odd (one a doorway's), 10-40%; late 12-16, 2 odd (no doorway's), 2-10%. Red herrings 0 / 1 / 1-2, spans inside 0, spread >= 50%, retry <= 5/8/12 s, crossing <= half the reward route. *Assumption (lattice-bound):* islands and cycles are relaxed where the 5x3 lattice cannot hold the note's numbers beside the gap doorway, its lookouts and the trial: early 5-7 islands (a single loop; an even-only network of 2 cycles needs 7+ islands), mid 7-10 islands and 2-4 cycles (note 6-8, 3-4), late 9-12 islands and 2-6 cycles (note 8-11, 4-6). In practice mid and late have 2-3 cycles. The broken-span-inside-the-network knob (late 0-1) is not used (always 0).
- **Generator** (`IslandGenerator`, pure; new stream `RngStreams.IslandPuzzles` = 28, the screen in the seed). Shuffle the doorways into roles, join the network doorways by shortest paths, then a local search toggles one candidate plank at a time and keeps the change while the distance from the targets does not grow (light annealing), 1200 steps an attempt, up to 300 attempts. The score covers planks, islands, cycles, connectivity, the network doorways, parity (and doorway odd islands), a clean start, an inner island for the reward, spread, the doorway-to-doorway crossing, the gap doorway's span (and its crossing) and room for the lookouts and trial, then P_rand. A network at 0 is dealt the rest: the reward island (a random inner network island), the gap span (the first option, in a seeded order, whose crossing passes), the lookouts and the trial island behind a broken span; the design is kept only if every doorway meets every target and `IslandLayout.Problems` (geometry) is empty. Past the budget a hand-checked template per tier (`IslandLayout.Templates`, four doorways, gap doorway east, trial dropped when not wanted) stands in.
  - Seeds 1-500 x 3 tiers: 0 problems, 0 templates, 4482 search steps on average. Seeds 1-50 in the sweep: 0 templates, 3752 steps a screen. Cold generation of a run's 2 screens: mean 32 ms, max 186 ms (EditMode).
  - A harness outside Unity (dotnet, scratchpad `islandh/`) compiled the pure files for tuning; the targets were checked there first (the note's islands/cycles are infeasible on the lattice: 0/20 late, 2/20 mid in a first prototype).
- **Plan** (`IslandPlan`, like `IcePlan`): Math's island screens from the layout (row by row), tier from Math's die number (4.8), the trial on the screen MathPlan's stream picks (its first draw, unchanged), layouts cached per seed, screen, tier, doorways and trial. `MathPlan.Create` reads the same layouts (`IslandPlan.LayoutFor`; new optional `dice`). `Goals()` are the reward islands' middles; `PuzzleScreens.Goals(ice, wind, islands)` appends them after Earth's, so Earth's goal numbers are unchanged; `RegionsAt` takes the plan too.
- **Roadblock and regions.** `IslandLayout` is still `IScreenRegions`: one more region, the roadblock (`GoalRegion`, within the chest's reach of `GoalSpot`), joined both ways to the reward island only while every connected doorway can solve the screen (`Solvable`), as the ice goal pads are. The game (`CubeWorld`, which logs any island problem at the reveal), `EdgeLocks.ForRun`, the sweep and the fixture pass the island goals to `RewardPlacement` and `LockWorld`. Math's pool gains 2 puzzle spots, so Math now hides 2 arch keys (500 keys over 50 seeds, was 498).
- **Runtime** (`IslandScreen`, now `IPuzzleRewardHost` and `IRewardGuard`): dark planks (dark wood), lit ones under Math's yellow, a 0.45 s red flash on a snuff, gold mended spans, a yellow rim on an island whose network planks are all lit; the chest on a dark plinth with one lamp per network plank (lit as planks light) under iron bars; solving runs a 0.8 s chain of yellow dots from the last plank to the chest, then the bars lift, "Every plank lit. The chest is free." pops up, and the chest opens on touch. No audio exists in the game, so the note's chime and fizzle are not built (*assumption*). All art is drawn in code (`ART-MAP.md`); no catalog, library or scene regeneration.
- **Text:** `Dialogue.IslandLine` now tells the rule in the shape voice: "On the islands, a plank lights when crossed. If you cross a lit one again, then every lamp goes out. Light them all to free the chest. Count each island's planks: odd ones make good beginnings." (193 characters; no "once"). `SolvedText` joins the text scans.
- **Determinism:** re-recorded twice; the recordings are byte-identical. New: `islands` (50). Changed: `mathPlan` (50: the new layouts), `rewards` (50: Math's pool), `streams` (50: `islandPuzzles`), `locks` (4: kept draws). Nothing else changed.
- **4.9 lock proof:** holds. No spending order soft-locks on 50/50 seeds, 2-3 first edges, 0 fallbacks. `EdgeLocks.ForRun` over seeds 1-50 now takes up to 114.9 ms (mean 29.3 ms; it was 42.9 ms max), under the test's 250 ms guard, because ForRun now also generates the island screens when the cache is cold.
- **Tests:**
  - New EditMode `IslandPuzzleTests` (9): the rule; the walker; the solver on hand-made networks (a path: P_rand exactly 2/3 and 2 trails; a 2x2 loop: 8 trails; a star: no trail; a doorway off the network; a hand-made screen whose dead-end starts leave the gap doorway unable to start, so its roadblock never joins); tiers and the note's bands; every island screen of seeds 1-50 lit from all 400 connected doorways inside its targets with no template; `Generate` over seeds 1-500 at every tier with 0 problems and 0 fallbacks; the templates; determinism on stream 28 and MathPlan sharing the layouts; the roadblock reachable only through a solvable network (an unsolvable star screen is flagged by the reward check and DoorwayReach).
  - New PlayMode `IslandPuzzleSceneTests` (1, batch 1 in `vpart.sh` and `verify.sh`), real keys on the first seed with a late Math face: the chest shut on touch; one plank lit then the screen left by its doorway comes back dark; walk to a start and step out and back (snuffed, red flash); the solver's trail plank by plank (one more lamp each, rims as islands complete, the chest shut until the last); the chain, the bars lift, the chest opens; left and come back, still solved.
  - `IslandWalkTests` stays meaningful: it still walks every island the model claims from every doorway (now including long planks), with and without the gap-crosser*, and its light check now replays the rule over the walker's own crossings (3568 crossings, 359 snuffs) instead of "walked stays lit". The roadblock's region is left out of the islands it walks to.
  - Updated: `MathTests` (no lone doorway, a lone doorway flagged, long planks, the new line), `MathComponentTests` (lit, snuff with flash, gold span, rim, reset on leaving; the lone sign on a hand-made layout), `MathSceneTests` (a doorway island's open-water side), `RewardSceneTests` (an island chest's guard), `RngStreamsTests`, `DeterminismTests`, `EdgeLockTests`, `FaceRepairTests`, the text scan.
- **Screenshots** of an early, a mid and a late screen (fresh, half lit, the red flash, the chain, open) are in the scratchpad's `snaps412/`, from a temporary snapshot test, since deleted. The camera offset is the snapshot harness's, as before.

- **Review patches (pass 1):**
  - **A teleport is never a crossing.** `CubeNavigator.SetPosition` (every teleport and placement) calls `IslandScreen.ForgetSteps`, which makes every enabled island screen's walker forget its last island, plank and sample (`IslandScreen.Active`). The lights are left as they are. PlayMode check in `IslandPuzzleSceneTests`: the player stands on an island, then on the plank to the doorway's island, then is teleported onto that island; nothing lights.
  - **A frame that skips a plank still crosses it.** `PlankWalker.Step` samples the way from the last position every `SampleStep` (0.15 units), so one sample jumping from island A to island B over their plank counts as crossing it. A long jump over several planks lists them all (`Crossings`), and `IslandScreen` applies each one. EditMode checks are in the walker test.
  - **`IslandLayout.Solvable` is cached** (a lazy field: the layout is immutable). `EdgeLocks.ForRun` over seeds 1-50 now takes up to 26.3 ms (mean 8.1 ms), down from 114.9 ms.
  - **The gap doorway's spot counts as occupied** in the generator's validity check, so no long plank in the search passes over that island (`Problems` already rejected such designs at the end). The search therefore takes different paths. Re-recorded twice, byte-identical. Against the first 4.12 recording only `mathPlan` (49 seeds) and `islands` (49) changed; `rewards`, `locks` and `streams` are the same. Seeds 1-500 x 3 tiers: still 0 problems and 0 templates (3532 search steps on average).
  - **`IslandWalkTests` reads the runtime again.** After every crossing leg it compares the screen's `Lights` (lit count, solved, and each network plank) with the replay, and at the end it checks that the screens leave lit exactly the planks the replay does (374), with `planksLit > 0`.
  - **Problems check the roles:** with three or more doorways exactly one doorway's island is off the lit network (none with fewer). EditMode test `Problems_WantEveryDoorwayButTheGapOneOnTheLitNetwork`.

## Verification

**Result (2026-10-08, `vpart.sh` steps in the foreground as `v412a`):**
- EditMode 356/356 (a first run had 355: the new island line was 208 characters against the dialogue box's 200; shortened and rerun).
- PlayMode batches 39/29/46/28/38/32/1, all passing. Batch 1 has the new `IslandPuzzleSceneTests`. Batch 2 first failed 1 (`RewardSceneTests` expected only ice or wind guards on puzzle chests); patched and rerun, 46/46.
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s (unchanged).
- `IslandWalkTests`: passing, 0 problems.
- Sweep 50/50, 0 failing. Islands: 100 screens (40 early, 38 mid, 22 late), 0 templates, 0 doorway checks outside a target. Locks: no soft-locks on 50/50 seeds, 0 fallbacks.
- `FIXTURE CHANGED` is the intended re-record described above.
- Not committed.

**Result after the review patches (2026-10-08, `vpart.sh` steps in the foreground as `v412b`):**
- EditMode 357/357 (1 new: the role check).
- PlayMode batches 39/29/46/28/38/32/1, all passing.
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s.
- `IslandWalkTests`: 0 problems (3585 crossings, 362 snuffs, 374 planks left lit, matching the replay).
- Sweep 50/50, 0 failing. Islands: 100 screens (40 early, 38 mid, 22 late), 0 templates, 0 doorway checks outside a target. Per tier, planks 6-7 / 8-11 / 13-14 and P_rand 100% / 11.1-23.8% / 6.7-9.3%, the same bands as before. Locks: no soft-locks on 50/50 seeds, 0 fallbacks.
- `FIXTURE CHANGED` is the intended re-record: 1 against the first 4.12 recording, and against the baseline the changes listed above.
- Not committed.

## Review Triage Log

Pass 1 (quick lens), triaged by the coordinator: high 0, medium 0, low 5, plus 1 note, all patched.

| Verdict | Route | Finding | Outcome |
|---|---|---|---|
| low | patch | A teleport on the same screen could read as a crossing: the walker kept its last island and plank | `CubeNavigator.SetPosition` calls `IslandScreen.ForgetSteps`, which resets every walker. PlayMode test: stood on a plank, then teleported onto its far island; nothing lights. |
| low | patch | A frame skip from island A to island B across a plank was missed | `PlankWalker.Step` samples the segment between samples, so every crossing on it counts (`Crossings`). EditMode tests: one sample over a plank, and one over two planks. |
| low | patch | `Solvable` was recomputed on every region join | Now a lazy cached field. `ForRun` max is 26.3 ms (was 114.9 ms). |
| low | patch | The generator's validity check did not count the gap doorway's spot as occupied | It does now. Re-recorded: `mathPlan` and `islands` on 49 seeds; 0 problems and 0 templates on seeds 1-500. |
| low | patch | `IslandWalkTests` no longer asserted anything read from `screen.Lights` | Runtime is compared with the replay after every leg, and at the end the planks left lit must match the replay, with `planksLit > 0`. |
| note | patch | Problems did not check that every doorway but one is on the network | `PuzzleProblems` wants exactly one doorway off the network with 3+ doorways, none otherwise. EditMode test. |
