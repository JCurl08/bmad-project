---
title: 'Screen modules and the town-exit shuffle'
type: 'feature'
ticket: '3'
created: '2026-10-05'
status: 'built'
baseline_revision: 'f0cf94e53b141c222496d57ed1a69de8985cd358'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/stack.md'
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/faces-and-races.md'
  - '{project-root}/_bmad-output/initiative-video-game/epic-playable-cube/story-seeded-cube-model-and-debug-tools-plan.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 1.2 places themes on faces but every screen is an empty floor. Later stories need real screens with known places for gates, hidden items and the core entrance. The spec also says the shuffle happens when the player leaves town, not at run start.

**Approach:** Add hand-built screen modules with tagged slots: an exit per side, gate slots, hidden-item slots and a core-entrance slot. A seeded layout assigns one module to every screen. Town uses its fixed modules. The science faces get their theme placement and modules only when the player first leaves town, and unbuilt faces stay sealed. Science faces whose story is not done use placeholder modules. Extend the debug overlay and the seed sweep.

## Boundaries & Constraints

**Always:** The same run seed reproduces the identical layout: face themes, module per screen and the chosen core-entrance slot per built face. Town is fixed and is the start of every run. Every module keeps its four side exits open, so connectivity stays a cube-level property and 1.2's sweep logic still holds. Each built science face gets exactly one active core-entrance slot. Modules are built by an editor script (no hand-written scene or prefab YAML). Reuse CubeModel, CubeWorld, CubeNavigator and CubeDebug from 1.2; N still comes only from CubeSettings.

**Never:** No items, gates, NPCs, enemies or the core arena; slots are markers only, filled by stories 1.4 to 1.11. Do not change how the 1.2 geometry and adjacency work. No new randomness outside SeededRng.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Run start | New run, any seed | Player on Town start screen; science faces show as unrevealed (not yet laid out) | No error expected |
| Leave town | Player crosses any Town edge into a science face for the first time | Science layout is generated from the run seed; player lands on the laid-out face | No error expected |
| Same seed | Two runs with seed 1234, leave town in both | Identical themes, module per screen and core-entrance slot per face | No error expected |
| Different seeds | Seeds 1 to 50 | At least two different module layouts | No error expected |
| Sealed stays sealed | Leave town on any seed | Math and Earth faces stay sealed and are never laid out | No error expected |
| Core entrance | Any seed | Each built science face has exactly one active core-entrance slot on a reachable screen | Sweep reports the seed and face |
| Slots visible | Debug slot view on | Every exit, gate, hidden-item and core-entrance slot on the current screen shows a labelled marker | No error expected |

</frozen-after-approval>

## Code Map

- `game/Assets/Scripts/Cube/CubeModel.cs` -- seed → theme per face, `TryStep`, `IsSealed`, `StartScreen`. Theme placement happens in the constructor today; the leave-town reveal must not change which themes go where for a given seed.
- `game/Assets/Scripts/Cube/CubeWorld.cs` -- builds placeholder floors per face in `Rebuild(seed)`, with an empty screen column gap between faces and walls on sealed edges. It raises `Rebuilt`. Module instantiation goes here.
- `game/Assets/Scripts/Cube/CubeNavigator.cs` -- detects edge crossings after the physics step (coroutine on WaitForFixedUpdate) and teleports with interpolation off. The leave-town event comes from its first crossing off Town.
- `game/Assets/Scripts/Cube/CubeDebug.cs` -- overlay (F1), reroll (F5), jump (F6). Add the slot view here.
- `game/Assets/Scripts/Cube/SeedSweep.cs` -- breadth-first reachability per seed, shared by tests and the menu. Extend it with the core-entrance check.
- `game/Assets/Editor/CubeSceneBuilder.cs` -- builds Cube.unity; menus Cube > Create Cube Scene and Cube > Seed Sweep (both usable with -executeMethod).
- `game/Assets/Tests/EditMode/CubeModelTests.cs`, `game/Assets/Tests/PlayMode/CubeSceneTests.cs` -- existing suites; keep them passing.

## Tasks & Acceptance

**Execution:**
- [x] `game/Assets/Scripts/Cube/Modules/ScreenModule.cs` + slot marker components (`ExitSlot` with Facing, `GateSlot`, `HiddenItemSlot`, `CoreEntranceSlot`) -- a module is a prefab of one 16×10 screen with child markers.
- [x] `game/Assets/Scripts/Cube/Modules/ModuleLibrary.cs` -- ScriptableObject listing modules per theme (Town modules indexed by cell; science themes list a pool) -- the source the layout draws from.
- [x] `game/Assets/Scripts/Cube/Modules/CubeLayout.cs` -- pure: given CubeModel + library sizes + seed, produces a module index per science screen and one core-entrance choice per built face, using SeededRng from a stream derived from the run seed -- deterministic and testable without scenes.
- [x] `game/Assets/Scripts/Cube/CubeWorld.cs` -- a run starts with only Town laid out and the science faces unrevealed. `RevealScience()` lays out the built science faces from CubeLayout and instantiates their modules. Only the chosen core-entrance slot is active per face. Sealed faces are unchanged.
- [x] `game/Assets/Scripts/Cube/CubeNavigator.cs` -- on the first crossing off Town in a run, call `RevealScience()` before teleporting.
- [x] `game/Assets/Scripts/Cube/CubeDebug.cs` -- F2 toggles slot markers (sprite + label per slot) on all screens.
- [x] `game/Assets/Scripts/Cube/SeedSweep.cs` -- also report per seed whether every built face has exactly one reachable core-entrance slot.
- [x] `game/Assets/Editor/ModuleLibraryBuilder.cs` -- menu Cube > Build Module Library. Generates the placeholder modules as prefabs from code: the 4 Town modules, and for each science theme a pool of at least 4 layouts with interior obstacles. Every module has four open side exits, 1–2 gate slots and 1 hidden-item slot, and at least one module per theme has a core-entrance slot. It also creates the ModuleLibrary asset, and Create Cube Scene wires it in.
- [x] Tests: EditMode `CubeLayoutTests` (same seed identical, different seeds differ, exactly one core entrance per built face, sealed faces get no modules, library invariants: every module has four exits) and PlayMode additions (run starts unrevealed, crossing off Town reveals and lands on a laid-out screen, F2 shows markers).
- [x] Run Build Module Library and Create Cube Scene in batch mode so the assets and scene are committed.

**Acceptance Criteria:**
- Given the EditMode and PlayMode suites and the Cube > Seed Sweep in batch mode, when run, then all pass and the sweep reports 50/50 seeds with a reachable core entrance on every built face.
- Given a WebGL build, when built with WebGLBuilder.BuildBatch, then it succeeds.

## Implementation Notes

- Verified in batch mode after the review patches: EditMode 56/56, PlayMode 11/11, seed sweep 50/50 with core entrances reachable. WebGL build PENDING (user).
- Checkpoint 1 was approved under the user's standing instruction (2026-10-05) to keep building while they are away. Review the plan on return.
- Verified in batch mode: Build Module Library (4 Town modules, 5 science pools of 4: Biology, Chemistry, Physics, plus Math and Earth for later), Create Cube Scene (CubeWorld wired to Assets/Modules/ModuleLibrary.asset), EditMode 56/56, PlayMode 11/11, seed sweep "50/50 seeds fully reachable with a reachable core entrance on every built face". WebGL build skipped per Verification.
- Exits stay open by rule: every obstacle avoids a 1.5-unit edge band and the two 2.5-unit centre lanes (ScreenModule.KeepsExitsOpen, checked by the builder and by CubeLayoutTests). This also keeps screen centres free for teleports and keeps the 1.2 straight-walk tests valid.
- Layout RNG is `new SeededRng(seed, stream: 2)`; order per built face: core cell, core-capable module, core slot, then the other cells. Core-capable modules per science pool: "A Pillars" (1 slot) and "C Corners" (2 slots).
- The reveal fires whenever the player arrives on a built science face (crossing or debug teleport) while unrevealed, which in normal play is the first crossing off Town; F5 reroll starts a new, unrevealed run. Exit slots whose edge leads into a sealed face are deactivated (that edge is a wall).
- Not committed: the generated assets and scene are on disk, uncommitted, for review.

## Review Triage Log

Pass 1 (quick lens): high 0, medium 1, low 5, false 1, maybe-false 0. Plus 1 pending HITL item.

| Verdict | Route | Finding | Evidence / action |
|---|---|---|---|
| medium | patch | Exit slots bordering a sealed face sit inside the sealed wall | Later stories fill exits by position. Exits into sealed faces are now deactivated, with a test assertion. |
| low | patch | An F6 jump onto an unrevealed face leaves dark faces with no modules | Reveal only triggered on a crossing that starts on Town. It now reveals on arrival at any built science face. |
| low | defer | Town modules wrap at N=3 (4 modules for 9 cells) | The 3×3 expansion is Should tier in epic-varied-runs and is logged in deferred-work.md. |
| low | reject | The sweep's "exactly one core entrance" check is tautological on layout data | The PlayMode AssertLaidOut checks the instantiated slots. A stronger check would need instances the pure sweep doesn't have. |
| low | reject | The F2 test doesn't check the OnGUI labels | Debug-only cosmetic, and the label text is constant. |
| low | reject | DrawSlotLabels allocates in OnGUI | Debug-only, F2 only; the fix would add caching. |
| false | reject | The "commit assets" task is ticked but not committed | Step 5 of the build commits after review. |
| n/a | pending HITL | AC "WebGL build succeeds" is unverified | The build was skipped after Claude Code stopped an IL2CPP build for low memory. The user runs Tracer > Build WebGL. |

## Design Notes

Two stages, one seed. The CubeModel already fixes themes per face from the seed at run start. CubeLayout uses a separate RNG stream derived from the same seed, for example `new SeededRng(seed, stream: 2)`. Revealing on leave-town therefore does not depend on how long the player spent in town or what they did there, and it stays deterministic. When story 2.x adds the town draft as a shuffle input, it extends the derivation.

"Unrevealed" science faces look dark and unpatterned until the reveal. After the review patch, arriving on any built science face reveals them, whether by crossing or by a debug jump.

## Verification

**Commands:**
- Unity batch `-runTests -testPlatform EditMode` and `PlayMode` -- expected: result Passed, 0 failed.
- Unity batch `-quit -executeMethod CubeSceneBuilder.RunSeedSweep` -- expected: log "50/50" with core entrances reachable.
- WebGL build: skip during this session. The machine ran low on memory during IL2CPP builds, so the user runs Tracer > Build WebGL later.
