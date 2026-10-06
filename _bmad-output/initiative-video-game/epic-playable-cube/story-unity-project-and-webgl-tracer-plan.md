---
title: 'Unity project and WebGL tracer'
type: 'chore'
ticket: '1'
created: '2026-10-05'
status: 'built'
baseline_revision: 'b394c2199057101d12d14fac84075a17e0e1e3e2'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/stack.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** No proof yet that the Unity project, the repo and a browser build work together. Every later story assumes a WebGL build that runs, saves and plays sound on itch.io.

**Approach:** Bring `game/` into the outer repo with LFS. Add a minimal tracer: a top-down player walks between two placeholder screens, audio starts after the first input, and a PlayerPrefs counter persists. Build it to WebGL with itch-compatible settings and upload it to a restricted itch.io page.

## Boundaries & Constraints

**Always:** Use the Input System (project is set to Input System only). Use URP 2D and built-in 2D tools. Use placeholder art generated in code or Unity's built-in sprites; no imported art. Saves go through PlayerPrefs. Audio may only start after the first player input. Commit only to the outer repo (`JCurl08/bmad-project`, personal account).

**Never:** Do not build the seeded cube, screen modules or the face system; those are stories 1.2 and 1.3. Do not commit `Library/`, `Temp/`, `Logs/`, `UserSettings/` or `Builds/`. Do not push to `JCurl08/game`. Do not hand-write scene or prefab YAML; create scenes through an editor script.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Walk across | Player holds right at screen A's right edge | Camera snaps to screen B, player continues | No error expected |
| Walk back | Player at screen B's left edge moving left | Camera snaps back to screen A | No error expected |
| Screen bounds | Player at an outer edge with no neighbour | Player is blocked by a wall | No error expected |
| First input | Page loaded, no input yet | Silence; first key or click starts the tone | No error expected |
| Save round trip | Counter incremented, tab reloaded | Counter shows the saved value | Missing key reads as 0 |

</frozen-after-approval>

## Code Map

- `game/` -- Unity 6000.6.4f1 project, Universal 2D template. Input System only (`activeInputHandler: 1`). Default input actions are at `game/Assets/InputSystem_Actions.inputactions` (Move action; reuse it).
- `game/.git/` -- empty nested repo made by Unity Hub (remote `JCurl08/game`). It must be removed so the outer repo tracks `game/` (hitl).
- `game/.gitignore` -- Unity template. It already ignores Library, Temp, Logs, UserSettings, Build and Builds. Keep it.
- `game/.gitattributes` -- Unity template with LFS rules for png, wav, psd, ttf and others. Keep it.
- `game/ProjectSettings/ProjectSettings.asset` -- `webGLCompressionFormat: 0` (Brotli) and `webGLDecompressionFallback: 0`. Set these through the build script, not by editing the file.
- `game/ProjectSettings/EditorBuildSettings.asset` -- currently lists SampleScene only. The tracer scene replaces it.
- `game/Assets/Welcome/`, `game/Assets/Scenes/SampleScene.unity` -- template leftovers. Leave them; they are not in scope.
- `.gitignore` (repo root) -- does not exist yet. Must ignore `_bmad/render/`.

## Tasks & Acceptance

**Execution:**
- [x] HITL: the user deletes `game/.git` (and optionally the empty GitHub repo `JCurl08/game`) -- the nested repo blocks the outer repo from tracking `game/`.
- [x] `.gitignore` (root) -- create it, ignoring `_bmad/render/` -- BMad's render cache.
- [x] `game/Assets/Scripts/Tracer/PlayerMover.cs` -- top-down movement from the Move action on a Rigidbody2D, with configurable speed -- the tracer's player.
- [x] `game/Assets/Scripts/Tracer/ScreenCamera.cs` -- snaps the camera to the screen containing the player, with screens on a fixed-size grid -- the Zelda-style screen transition that story 1.2 builds on.
- [x] `game/Assets/Scripts/Tracer/AudioUnlock.cs` -- generates a short tone with AudioClip.Create and plays it on the first key or pointer input only -- satisfies the browser audio rule.
- [x] `game/Assets/Scripts/Tracer/SaveProbe.cs` -- reads a PlayerPrefs int on start, increments it on a key press, saves it and shows it in on-screen text -- the save round trip.
- [x] `game/Assets/Scripts/Tracer/ScreenMath.cs` -- a pure static function from world position to screen index -- testable without play mode.
- [x] `game/Assets/Editor/TracerSceneBuilder.cs` -- a menu item, Tracer > Create Tracer Scene. It builds `Assets/Scenes/Tracer.unity` with two adjacent screens (walls from built-in sprites), the player, the camera and the probes, saves it, and sets it as the only build scene.
- [x] `game/Assets/Editor/WebGLBuilder.cs` -- a menu item, Tracer > Build WebGL. It sets Gzip compression with decompression fallback on, then builds to `game/Builds/WebGL` -- itch.io serves Gzip builds reliably with the fallback.
- [x] `game/Assets/Tests/EditMode/ScreenMathTests.cs` with an EditMode `.asmdef` -- tests the screen index at centres, edges and negative coordinates.
- [ ] HITL: the user runs Create Tracer Scene, then Build WebGL. They zip the contents of `Builds/WebGL`, upload the zip to a new restricted itch.io HTML project, and tick "This file will be played in the browser".
- [ ] Commit `game/` and the root `.gitignore` after review passes, then push to `JCurl08/bmad-project`. LFS rules apply on the first commit. The implementer does not commit or push; the orchestrator does this after review.

**Acceptance Criteria:**
- Given a fresh clone of the outer repo, when `game/` is opened in Unity 6000.6, then the project imports with no missing scripts, and git status shows no Library, Temp or Builds files.
- Given the tracer scene in Play mode, when the player walks from screen A to screen B and back, then the camera shows one full screen at a time and follows correctly.
- Given the itch.io page in a fresh browser, when the game loads, then it runs, the tone plays only after the first input, and the counter survives a tab reload.
- Given the EditMode tests, when they are run in the Test Runner, then all pass.

## Review Triage Log

Pass 1 (quick lens): high 0, medium 1, low 4, false 2, maybe-false 0. Plus 1 pending HITL item.

| Verdict | Route | Finding | Evidence / action |
|---|---|---|---|
| medium | patch | ScreenCamera duplicates the screen size instead of using ScreenMath | Camera and world grids drift if either changes, and story 1.2 reuses ScreenMath. Default now comes from ScreenMath.DefaultScreenSize. |
| low | patch | Dead `?? squareSprite` fallback in TracerSceneBuilder | LoadBuiltinSprite never returns null. Removed. |
| low | patch | NRE before the assert in TracerSceneTests | `.transform` is read on a possibly null result. Now asserts the component first. |
| low | patch | ProbeTests wipes the developer's real `tracer.counter` | Same PlayerPrefs store as Play mode. Now saves and restores it. |
| low | patch | PlayerMover never disables the shared Move action | Global action left enabled after the player is destroyed. OnDisable added. |
| false | reject | AC 4: no evidence the EditMode tests ran | Batch run `-runTests -testPlatform EditMode` passed 16/16, and so did the implementer's re-run. |
| false | reject | Plan Code Map has a stale input-actions path | The fix would edit this build's plan, which the triage rules reject. The code resolves actions through InputSystem.actions. |
| n/a | pending HITL | AC 3: itch.io upload and browser check not done | This is the user's step and is on their to-do list. The local WebGL build succeeded. |

## Design Notes

Screen size: one screen is 16×10 world units (classic Zelda proportions at 16 px per unit), so the camera's orthographic size is 5. Screen index = floor(position / screenSize) per axis. Story 1.2 reuses ScreenMath for cube screens.

The editor-script approach keeps scenes reproducible and lets an agent create content without hand-written YAML. The user only needs to click two menu items.

## Verification

**Commands:**
- `git -C C:/Projects/bmad-project status --short` -- expected: no Library, Temp, Logs, UserSettings or Builds paths.
- `git -C C:/Projects/bmad-project lfs ls-files` -- expected: runs without error (no binary assets yet).

**Manual checks (if no CLI):**
- Unity Test Runner > EditMode: all ScreenMath tests pass.
- Play mode: walk between screens. Built WebGL on itch.io: audio after the first input, counter persists after reload.
