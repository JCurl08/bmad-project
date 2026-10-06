---
title: 'Seeded cube model and debug tools'
type: 'feature'
ticket: '2'
created: '2026-10-05'
status: 'built'
baseline_revision: 'c8fc72918f38b886569b888a8814704a960f9a5b'
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
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Every later story needs a cube world that one seed fully determines: which theme sits on which face, how screens connect across face edges, and which faces are sealed. Story 1.1 only proved a flat two-screen tracer.

**Approach:** Build a pure, engine-light cube model: a deterministic seeded RNG, 6 faces of N×N screens (N is a setting, 2 now), and edge adjacency derived from cube geometry. The Town face is fixed and the science themes are placed on the other faces by seed, with unbuilt themes sealed. Add a playable debug scene, a debug overlay, reroll and jump commands, a reachability seed sweep, and EditMode tests.

## Boundaries & Constraints

**Always:** Same seed gives an identical cube on every platform, so use our own RNG, never `UnityEngine.Random` or `System.Random`. Face size N is one setting read everywhere. Built themes this epic are Biology, Chemistry and Physics; Math and Earth & Atmosphere are sealed. The Town face is always the start face. Reuse `ScreenMath` and `ScreenCamera` from story 1.1 for screen size and snapping. Debug overlay and debug keys only run in the editor and development builds.

**Never:** No screen modules, gates, items, NPCs or the leave-town trigger; those are stories 1.3 to 1.7. Do not hand-type a 24-entry edge table with no derivation or invariant tests behind it. Do not break story 1.1's tracer scene or its tests.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Same seed | Two cubes from seed 1234 | Identical theme placement and identical sealed set | No error expected |
| Different seeds | Seeds 1 to 50 | At least two different theme placements | No error expected |
| Town fixed | Any seed | Town is on the start face; start screen is on Town | No error expected |
| Inner step | Step east from screen (0,0) on any face, N=2 | Lands on (1,0) on the same face | No error expected |
| Edge crossing | Step off any edge of any face | Lands on the geometrically adjacent face, at the matching edge cell, with the correct new facing | No error expected |
| Round trip | Step off an edge, then step back the opposite way | Returns to the original face, cell and facing | No error expected |
| Sealed boundary | Step toward a sealed face | Move is refused and the player stays put | No error expected |
| Face size 3 | N=3 | All invariants above still hold | No error expected |
| Sweep | Seeds 1 to 50 | Every screen on unsealed faces is reachable from the Town start screen | Sweep reports the failing seed |

</frozen-after-approval>

## Code Map

- `game/Assets/Scripts/Tracer/ScreenMath.cs` -- screen size 16×10 (`DefaultScreenSize`), `ScreenIndex`, `ScreenCenter`. Reuse; do not duplicate the size.
- `game/Assets/Scripts/Tracer/ScreenCamera.cs` -- snaps to the screen containing its target. Reuse for the cube scene.
- `game/Assets/Scripts/Tracer/PlayerMover.cs` -- Rigidbody2D mover on the project-wide `Player/Move` action, with OnEnable/OnDisable. Reuse.
- `game/Assets/Editor/TracerSceneBuilder.cs` -- pattern for building scenes from code (sprites on scaled "Visual" children, URP Sprite-Unlit-Default material). Follow the pattern; do not change the tracer scene.
- `game/Assets/Tests/EditMode/`, `game/Assets/Tests/PlayMode/` -- existing test assemblies (`Game.Tracer.Tests.*`). The tracer PlayMode tests load the `Tracer` scene by name, so it must stay in the build list.
- `game/Assets/Scripts/Tracer/Game.Tracer.asmdef` -- runtime assembly. New cube code goes in a new `Game.Cube` assembly that references Game.Tracer.

## Tasks & Acceptance

**Execution:**
- [ ] `game/Assets/Scripts/Cube/Game.Cube.asmdef` -- new runtime assembly (references Game.Tracer, Unity.InputSystem) -- keeps the cube model separate from the tracer.
- [ ] `game/Assets/Scripts/Cube/SeededRng.cs` -- small deterministic RNG (e.g. PCG32 or xorshift) with `NextInt(max)` and a Fisher–Yates `Shuffle` -- cross-platform determinism.
- [ ] `game/Assets/Scripts/Cube/CubeModel.cs` (plus small types as needed: `FaceId`, `Theme`, `ScreenAddress` = face + cell, `Facing`) -- built from (seed, faceSize). Town is on the start face, and the 5 science themes are shuffled onto the other faces. `IsSealed(face)` covers Math and Earth this epic. `TryStep(address, direction, out next)` returns the next screen and the new facing, or false into a sealed face. Adjacency comes from each face's 3D basis (normal, right, up), not a typed table.
- [ ] `game/Assets/Scripts/Cube/SeedSweep.cs` -- breadth-first reachability from the Town start screen over `TryStep`. It returns unreachable screens per seed -- shared by the test and the editor menu.
- [ ] `game/Assets/Scripts/Cube/CubeWorld.cs` + `CubeNavigator.cs` -- runtime. Lay each face out as an N×N block of 16×10 screens in its own world region (gaps between faces). Draw placeholder floors tinted per theme, with sealed faces shown dark. When the player leaves a face's outer edge, call `TryStep`, then move the player to the matching entry point on the next face, keeping the position along the edge. Treat a sealed step as a wall.
- [ ] `game/Assets/Scripts/Cube/CubeDebug.cs` -- overlay (face, theme, cell, seed); F1 toggles it, F5 rerolls to a new random seed and rebuilds, F6 jumps to the next unsealed screen. Editor and development builds only.
- [ ] `game/Assets/Editor/CubeSceneBuilder.cs` -- menu items Cube > Create Cube Scene (builds `Assets/Scenes/Cube.unity` with player, camera, CubeWorld and debug, and puts it first in the build list while keeping Tracer) and Cube > Seed Sweep (runs 50 seeds and logs the results).
- [ ] `game/Assets/Tests/EditMode/CubeModelTests.cs` -- covers every I/O matrix row except runtime play, including the round-trip and symmetry invariants for every face, edge and cell at N=2 and N=3, and the 50-seed sweep.
- [ ] `game/Assets/Tests/PlayMode/CubeSceneTests.cs` -- loads Cube.unity and walks the player off a Town edge. It checks the player arrives on the adjacent face as `CubeModel` predicts, and that walking toward a sealed face is blocked.
- [ ] Run Cube > Create Cube Scene in batch mode so the scene exists and is committed.

**Acceptance Criteria:**
- Given the EditMode and PlayMode suites in batch mode, when run, then all tests pass, including story 1.1's.
- Given the Cube scene in Play mode, when F5 is pressed, then the overlay shows a new seed and the theme placement can change. When F6 is pressed, the player jumps to another unsealed screen.
- Given a WebGL build, when it is made with Tracer > Build WebGL, then the build succeeds with the Cube scene first.

## Implementation Notes

- Verified in batch mode after the review patches: EditMode 43/43, PlayMode 9/9, seed sweep 50/50 reachable. Before the patches, a WebGL build succeeded with Cube first.
- PENDING: the WebGL rebuild after the patches was stopped by Claude Code for low system memory, so it was not re-run automatically. The user re-runs Tracer > Build WebGL.
- Adjacency comes from each face's 3D basis, using integer maths. PCG32 is checked against its reference output. Crossings are detected after the physics step, and teleports snap without interpolation.
- Checkpoint 1 was approved under the user's standing instruction (2026-10-05) to keep building while they are away. Review the plan on return.

## Review Triage Log

Pass 1 (quick lens): high 0, medium 2, low 4, false 0, maybe-false 0.

| Verdict | Route | Finding | Evidence / action |
|---|---|---|---|
| medium | patch | Face size N has two sources (CubeWorld's serialized field vs CubeSettings.DefaultFaceSize) | Breaks the "one setting read everywhere" rule. The field is removed and the world derives N from the model. |
| medium | patch | Black-screen flash on face crossings | The crossing is detected in FixedUpdate before the physics step, so the camera renders the empty gap. The crossing is now predicted and the teleport snaps without interpolation. |
| low | patch | Sealed-face PlayMode test can pass without reaching the wall | It held the key for 5 s, but the walk needs about 5.3 s, and nothing asserted the edge. It now walks until the player stops and asserts the edge. |
| low | patch | Sealed backstop clamps both axes | A direct correction: it now clamps only the exit axis. |
| low | patch | Overlay "Facing" goes stale within a face | Debug only. Relabelled as the entry facing. |
| low | reject | Rebuilt subscription / null World in TeleportTo | The scene builder always wires World before enable, so it isn't reachable in normal use, and the fix would only add guards. |

## Design Notes

Derive adjacency from geometry. Give each face a 3D basis: normal n, right r, up u, chosen so r × u = n. A cell (i, j) on face F with N cells per side sits at the 3D point n + r·x + u·y, with x, y in (−1, 1). Stepping past x = 1 moves onto the face whose normal equals r. On that face, recompute the cell from the same 3D point and derive the new facing from where the old step direction points in the new face's basis. Invariant tests (every face, edge and cell, round trip equals identity) catch any wrong basis.

Start face is Front (Town). Faces: Front, Right, Back, Left, Top, Bottom. The core (story 1.11) is not part of this model.

## Verification

**Commands:**
- `"/c/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe" -batchmode -nographics -projectPath C:/Projects/bmad-project/game -runTests -testPlatform EditMode -testResults <xml>` -- expected: result Passed, 0 failed.
- The same with `-testPlatform PlayMode` -- expected: result Passed, 0 failed.
- `... -quit -buildTarget WebGL -executeMethod WebGLBuilder.BuildBatch` -- expected: exit 0, "WebGLBuilder: Succeeded".
