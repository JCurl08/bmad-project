---
title: 'Doorway edges and island screens'
type: 'feature'
ticket: '25'
created: '2026-10-07'
status: 'built'
baseline_revision: 'b589350'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-video-game/playtest-2-feedback.md'
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/faces-and-races.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The user rejected 1.24's turned-input fix: "I don't like this". Their alternative:
- "a doorway/gate at the edges of each face. If a user moves through the game their movement stops (even if holding), then they have to press again to move."
- "The entire edge of the screen shouldn't be an entrance, just a clearly marked gate in the middle. The rest of the edge should be an uncrossable wall."

They also rejected the Math bridges: "I don't want the puzzle to be a single block of connected bridges, instead the bridges are spread out around a single screen, requiring you to think about how you access the screen."

**User decisions (2026-10-07):**
- Doorways go on **every** screen edge. Face-edge doorways look clearly different, like a stone gate arch.
- Movement stops only at **face-edge** crossings.
- Locked face-edge gates come later, in the redesign. Doorways are open for now.
- Math uses **island screens**: the whole screen is solid water dotted with islands. Doorways land on different islands, bridges are spread out between them, and some are broken spans that only the Gap-Crosser\* crosses. You work out a route.

**Approach:**
1. **Doorway edges.**
   - Every screen's four edges are uncrossable walls except one clearly marked doorway centred on each side that leads somewhere. The doorway sits on the existing exit lanes.
   - Face-edge doorways get distinct gate-arch art. Within-face doorways are simpler openings.
   - The map shows doorways where cheap; this is optional.
2. **Stop on a face crossing.**
   - Crossing a face-edge doorway stops the player, facing into the new face. Held keys are ignored until every direction is released and a key is pressed again.
   - This replaces `EdgeInputFrame`/`IMoveInputFrame`, which are removed. Within-face crossings stay smooth.
3. **Island screens (Math).**
   - They replace the bridge-door gate/puzzle. A Math island screen is solid water with islands.
   - Each doorway lands on an island. Bridges, spread out across the screen, join some islands.
   - The island graph is deliberately not fully connected:
     - some doorway pairs connect only through a broken span, so they need the Gap-Crosser\*;
     - some doorways connect to nothing else on the screen, so the player must come round through another screen and doorway.
   - Pickups or gate rewards that used to sit behind bridge doors sit on islands, reachable only from the right doorway or with the Gap-Crosser\*.
   - Gap gates elsewhere are unchanged.

## Boundaries & Constraints

**Always:**
- **Solvability is proven:**
  - The sweep models reachability per doorway: which doorways and spots of a screen connect, given the items held.
  - Every seed stays 50/50 with no softlocks. That includes the start, every required item, the trial, and the boss portals.
  - Island screens never strand the player: every island the player can land on can be left by its doorway, so walking back out of the doorway is always possible.
- **Content stays reachable:** walls along edges must not trap content that previously sat near edges. The edge band and lanes stay free of blocking content. Doorways are wide enough (≥2 tiles) for the player, enemies and pushed boulders where the game needs that (Physics boulders crossing screens, if they do).
- **Doorway rules on other faces:**
  - one-way gates, trials and core portals keep working through doorways;
  - Earth wind can't push the player through a wall.
- **Tests:**
  - EditMode: doorway geometry, island-graph rules and per-doorway reachability.
  - PlayMode:
    - the edge is blocked except at the doorway;
    - a face crossing stops the player until a fresh press;
    - an island screen is walked through on one seed.
  - Remove or adapt the EdgeInputFrame tests. Keep a round-trip test: cross a face edge, then press back to return.
- **Determinism:** re-record with a note listing the changed fields.
- **Test runs:** PlayMode in class batches with `-nographics`, in the foreground.

**Never:**
- No locked edge gates, towns, temples, quests, weather or scale changes. Those belong to the course correction.
- No boss tuning.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Edge wall | Walk into an edge away from the doorway | Blocked | No error expected |
| Doorway | Walk through a centred doorway | Next screen, same face, smooth | No error expected |
| Face gate | Hold a key through a face-edge doorway | Arrive on the new face facing inward, then stop; moves only on a fresh press | No error expected |
| Return | Fresh press back towards the gate | Return through the same gate | No error expected |
| Island screen | Enter from doorway A | Reach only the islands A connects to; a broken span needs the Gap-Crosser* | The sweep proves the route |
| Strand | Any island the player lands on | Its doorway is always usable to leave | Sweep rule |
| Sweep | Seeds 1–50, N=3 (and N=2 for the tests) | 50/50, no softlocks | Fails with a report |

</frozen-after-approval>

## Code Map

- Edges:
  - `Editor/ModuleLibraryBuilder.cs` (module walls and lanes)
  - `CubeWorld` screen building and walls
  - `CubeNavigator` edge detection; crossing only through the doorway span
  - `ArtCatalog`/`ArtCatalogBuilder` (doorway and gate-arch art keys; Kenney tiles listed in `Assets/Art/ART-MAP.md`)
  - `Map/CubeMap` (optional doorway marks)
- Stop on crossing: `PlayerMover` (remove the `IMoveInputFrame` hook or replace it with a "wait for fresh press" latch), `CubeNavigator`, `EdgeInputFrame.cs` (remove), and its tests in `CubeModelTests`/`CubeSceneTests`.
- Island screens: `Faces/Math/MathPlan.cs`, `BridgeLayout.cs`, `BridgePuzzle.cs`, `BridgeWalk`, `BrokenSpan`, `MathFace`, `SeedSweep` Math rules, `ItemPlacement` (rewards behind bridge doors), `HintGenerator` gate looks for Math, `Dialogue` lines on bridges.
- The sweep: `SeedSweep`/`CubeSceneBuilder.RunSeedSweep` reachability, made doorway-aware.

## Tasks & Acceptance

**Execution:**
- [x] Doorway walls on all screen edges, with distinct face-edge gate arches.
- [x] Stop-on-face-crossing latch; remove EdgeInputFrame.
- [x] Island screens replace bridge doors, with per-doorway reachability in plan, sweep and placement.
- [x] Update hints and dialogue for the islands (no "walk every bridge once" wording; subtle-science rule).
- [x] Regenerate the module library, art and scene; re-record determinism with a note.
- [x] Tests, and the full verify.

**Acceptance Criteria:**
- Given the suites and the sweep, when run, then all pass.
- Given the scene (HITL), when the user plays, then edges are walls with centred doorways, face gates stop movement, and Math island screens need route-finding.

## Implementation Notes

- Checkpoint 1: design choices answered by the user on 2026-10-07; plan self-approved under the standing instruction.
- **Doorway edges.**
  - `Modules/ScreenEdges` holds the pure geometry. Every screen edge is a one-tile wall (`EdgeWallThickness` 1) with one centred doorway the exit lane's width (2.5 tiles), on the lane.
  - `CubeWorld.BuildEdgeWalls` builds the walls: Town's with the run, each science face's on its reveal. They use the `Wall` art in the face's wall tint.
  - A doorway onto another face gets a gate arch: new key `DoorArch` = Tiny Town 78, a stone archway with an open middle. It is 5 wide and one tile deep (inside the wall band since the review), with the lintel on the edge; its open middle is exactly the doorway and its posts stand on the wall. A doorway within a face is a plain gap.
  - Edges into a sealed face keep the old whole sealed wall.
  - Everything solid stays in the edge band and off the lanes, so no content placed under the `KeepsExitsOpen` rules meets a wall. Tested in EditMode for both N.
  - Doorway marks on the map (optional) were skipped.
- **Stop on face crossings.**
  - `EdgeInputFrame` and `IMoveInputFrame` are removed, along with their tests.
  - `PlayerMover` gains `FreshPressLatch` (pure, EditMode-tested). After every face crossing `CubeNavigator` stops the body and turns the player to face into the new face, then calls `StopUntilFreshPress`.
  - Held keys are ignored until every direction is let go and a key is pressed again. The let-go is watched every frame as well as every physics step, so a quick release counts.
  - Teleports and the core arena clear the latch. Crossings within a face stay smooth.
  - Pressing toward the doorway behind (the reverse of the entry facing) walks back over the same edge.
  - `GamePause.Paused` (added in 1.24 only for the frame) is removed (review).
- **Island screens: where.**
  - Math's pool ends with an island module, `Math E Islands`: only its four exits, with no obstacles, gate slots, hidden-item slots or core entrances.
  - `CubeLayout` never draws it at random. It gives it to `IslandScreensPerFace(n)` screens: `PerScreen(1, n)`, which is 2 at 3x3 and 1 at 2x2.
  - The screens are chosen on a new stream, `RngStreams.IslandScreens` = 21, after the face's other draws. They are never the core screen and never side by side.
  - The rest of the layout therefore draws as before. `ItemPlacement` simply finds no slots on island screens, so no gate, pickup behind a gate, hidden Jumbles, core portal or Physics boulder ever lands there.
- **Island screens: the graph.**
  - `Faces/Math/IslandLayout` (pure) covers the water area inside the walls with islands on a 5 x 3 lattice of spots (columns 2.8 apart, rows 2.7 apart).
  - Each doorway's island sits on a fixed spot and reaches from the wall to cover its doorway, so an arriving player always stands on land. Inner islands are 1.4 x 1.3.
  - Planks only join lattice neighbours, shore to shore, 1.2 wide, so they never cross. Every gap between islands is real water (at least 0.75).
  - `Generate` deals the doorways into roles:
    - two doorways are joined over whole planks;
    - the third reaches that network only over a broken span (one long link on its route is broken);
    - the fourth joins nothing else on the screen, sometimes with a lookout island or two.
  - Routes go, two times in three, by way of a seeded inner waypoint, so they wander over the whole screen instead of crossing in the middle.
  - On the trial screen, a trial island hangs off the joined network by a broken span.
  - `Problems` checks:
    - every doorway lands on its own island and the island reaches the doorway;
    - islands are separate land and planks run shore to shore;
    - broken spans are long enough for their one-square gap (1.0 plus 0.2 of plank each side);
    - every island joins some doorway;
    - the screen is not fully connected: a whole-plank pair exists, a pair joins only over a broken span, and one doorway joins none;
    - the trial island is reached only with the gap-crosser*.
- **Island screens: in the world.**
  - `IslandScreen` lays solid water as merged box colliders: the water area less the islands and planks.
  - Islands use the `Island` pad art and planks the `PlankBridge` art.
  - A broken span is a `BrokenSpan`: a solid gap with the gap art and the gap-crosser*'s mark. It is the existing mechanic, now owned by the island screen. Swinging the gap-crosser* at it lays the plank for good and the gap stops blocking.
  - The Math trial (`MathTrial`) is now a flag on the trial island. Stepping on it pays `TrialCurrency` once. The bigger every-plank-once layout is gone with the rest of the bridge puzzle.
  - Math's shape people and enemies never spawn on island screens.
  - `BridgeLayout`, `BridgeWalk`, `BridgePuzzle`, `BridgeDoorGate`, `MathGateKind`, `GateLooks.PlankDoor` and the `GateBridgeDoor` art key are removed. Every Math gate, on Math and off it, is now a gap gate.
- **Rewards.**
  - Bridge doors never guarded a required pickup. Only Math's own pickup lives on Math, and `ItemPlacement` never puts a pickup behind its own item's gate, so their alcoves were empty. Nothing required had to move.
  - The rewards on islands now are:
    - the trial, which sits behind a broken span, so the gap-crosser* is needed;
    - an open Math pickup on an island screen. It stands at `OpenPickupOffset`, which is on the south doorway's island, so it is reached only through that doorway and whatever the south island's planks join.
- **Per-doorway reachability (sweep model).**
  - `DoorwayReach` (pure) walks places of the form (screen, region). An ordinary screen is one region; on an island screen each island is its own region.
  - Joins are:
    - doorways: from the island a doorway lands on to the place behind it on the next screen;
    - whole planks;
    - broken spans, only with the gap-crosser*.
  - Every join works both ways, and `Problems` checks that each step has its way back, so the player is never stranded: it can always walk back out of the doorway it came in by. `Problems` also checks that every ordinary screen is reachable without the gap-crosser* (so core portals, gates and pickups never wait on an island route) and every island with it.
  - `MathPlan.Problems` adds the layout rules and the trial's reachability, and collects every item doorway by doorway. Its Math gap gates need the gap-crosser*.
  - `SeedSweep.FindItemProblems` now collects through `DoorwayReach` over Math's island screens, so an open pickup on an island screen counts only where its island is reachable. This is how the sweep's placement check models it.
  - The 50-seed sweep passes at N=3. The N=2 library sweep in `ItemPlacementTests` and `MathTests` passes too.
- **Text.**
  - New `Dialogue.IslandLine` (told by `MathPopulation.IslandTeller`): "a doorway only takes you to the islands its planks reach … go round and come in by another doorway".
  - The `GapCrosserLine` now mentions "a plank with a hole in it".
  - The Bridgekeeper* lines are rewritten around islands and going round, and the shape-person errand is to "reach the island with the flag".
  - Hint looks for Math say "a gap".
  - `InWorldText` is the rule source. It now also bans Königsberg (three spellings), "exactly once" and "seven bridges", and a Math text test checks that no Math string says "once".
- **Regenerated:**
  - the art catalog (`DoorArch` added, `GateBridgeDoor` gone);
  - the module library (`Math E Islands` added; other prefabs unchanged);
  - the Cube scene (regenerated object IDs only).
- **Determinism re-recorded.** Fields that changed:
  - `layout` (50 seeds): two Math cells now hold the island module;
  - `items` (50) and `hidden` (50): the free gate and hidden slots changed with the island cells;
  - `streams` (50): a new first-draws entry for `islandScreens`;
  - `mathPlan` (50): island screens instead of bridge layouts;
  - `physicsPlan` (13), `chemistryPlan` (13), `earthPlan` (10) and `biologyPlan` (8): these follow the new placement.

  `model`, the NPC fields and the Town field are unchanged.
- **Review patches (pass 1).**
  - Teleports onto a screen (a new run, F6, leaving the core) now go through `CubeWorld.LandingPoint`: the screen's centre, but on an island screen the middle of the island of the doorway behind the arrival facing (else the south doorway's, else any doorway's), never the water. `MathSceneTests` teleports onto every island screen with every facing, and F6 onto one.
  - The gate arch no longer reaches past the wall: `ArchDepth` is the wall's depth, and the extra post colliders are gone (the posts stand on the wall pieces). Pool design A's core entrance moved from (-2.5, -3.5) to (-2.5, -3.3), so its portal ring keeps off the edge wall and arch. New EditMode test `GateArchesAndEdgeWalls_StayClearOfEveryModulesPortals_GateSlotsAlcovesAndHiddenItems` checks every module against all four arches and edge walls.
  - `GamePause.Paused` removed; stale "bridge doors" comments in `CubeSceneBuilder` fixed; the unused `IslandLayout.Joined` removed.
  - Determinism re-recorded again for the moved core entrance: only `physicsPlan` changed (43 seeds; Physics keeps its boulders and switches off core-entrance slots, so their spots moved). No other field changed.

## Review Triage Log

Pass 1 (quick lens), triaged by the coordinator: high 0, medium 0, low 6.

| Verdict | Route | Finding | Outcome |
|---|---|---|---|
| low | patch | F6 and other centre teleports land in the water on an island screen | Patched: `CubeWorld.LandingPoint` puts the player on a doorway's island; PlayMode test. |
| low | patch | Arch posts and art overlap pool design A's core portal on bottom-row core cells | Patched: the arch stays inside the wall band (no posts beyond it) and design A's core entrance moved up 0.2; EditMode test that arches and edge walls keep clear of every module's portals, gate slots, alcoves and hidden items. Determinism re-recorded (`physicsPlan` only). |
| low | patch | `GamePause.Paused` unused | Removed, with its comment. |
| low | patch | Stale "bridge doors" comments in `CubeSceneBuilder` | Fixed. |
| low | patch | Unused `IslandLayout.Joined` | Removed. |
| low | reject | A generic hint opener ("Somebody once mumbled") says "once" | The ban targets the bridge-puzzle wording, not the word "once" in general; the Math-string check covers Math's own lines only. |

## Verification

**Commands:**
- `verify.sh <tag>` in the scratchpad.

**Result (2026-10-07, `verify.sh v125b`, plus a rerun of EditMode after two library tests were taught to skip the island module):** EditMode 257/257 (the verify run itself had 255/257: `Library_EveryGateSlotHasAnAlcove_ClearOfLanes` and `Hidden_EveryLibrarySlotIsFair` still expected slots on the island module; fixed and rerun). PlayMode batches 25/21/26/26/35/24, all passing. Skill checks Victory (seeds 1234, 7 and 42). Sweep 50/50, 0 failing. Math in the sweep: 100 island screens (963 islands, 763 planks, 150 broken spans, 58 lone doorways with a lookout island); 300 gap gates (50 off-home); the trial island on 50 seeds; the gap-crosser* on an island on 10 seeds. `FIXTURE CHANGED` is the intentional re-record described above. Not committed.
- Earlier, a parts-1-and-2-only run (`v125a`) hit the known `EarthSceneTests` NPC-wander flake: a Physics NPC drifted 0.05 inside a gate-front keep-out. It did not recur in `v125b`.

**Result after the review patches (2026-10-07, `verify.sh v125c`):** EditMode 258/258. PlayMode batches 25/21/26/26/35/24, all passing. Skill checks Victory (seeds 1234, 7 and 42). Sweep 50/50, 0 failing. `FIXTURE CHANGED` is the intentional re-record (1.25's, plus `physicsPlan` on 43 seeds for the moved design-A core entrance). Not committed.
