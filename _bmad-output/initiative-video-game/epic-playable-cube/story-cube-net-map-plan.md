---
title: 'Cube-net map'
type: 'feature'
ticket: '21'
created: '2026-10-06'
status: 'built'
baseline_revision: '3f22c7f321cf6ea97aaf8346e1de2e84b8675a11'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/faces-and-races.md'
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/lore-and-tone.md'
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/stack.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** With 6 faces × 9 screens, shuffled every run and with edges that rotate between faces, players get lost. The user asked for a map and chose a cube-net pause map (CAP-26).

**Approach:** A pause key (M, with Tab as an alternate) opens a full-screen map showing the cube unfolded as a cross-shaped net:
- Town in the centre of the cross, its four neighbours around it, and the opposite face at the end.
- Each face is a 3×3 grid of screens.
- **Visited screens** are filled in their region colour; unvisited screens are dark.
- **Markers:**
  - you (with facing)
  - the Town start screen
  - discovered gates (seen on a visited screen), drawn in a neutral style but distinct for "opened"
  - items and pickups seen
  - trials (with done/not done)
  - core portals
- **Netting the cube:** the net shows each face rotated as it is actually connected to its neighbours on the cube, so walking off an edge on the map matches the game.
- **Lifecycle:** it updates as you explore and after the town-exit shuffle, resets for a new run, and pauses gameplay while open.

## Boundaries & Constraints

**Always:**
- **Truth:** the map only shows what the run really is, using CubeModel, CubeLayout and the placed gates, pickups, trials and portals. A test checks it matches the world on several seeds.
- **Fog:** only visited screens and things seen on them are revealed.
- **Orientation:** derived from the CubeModel face bases. For each net placement, the face's 3×3 cells are rotated so that cells shared across a net edge are adjacent on the cube. A test checks that, for every edge drawn adjacent in the net, the two cells touching it are real neighbours on the cube (via TryStep).
- **Colours:** region colours come from `CubeWorld.ThemeColor`, so story 1.22's Rubik's palette flows through automatically.
- **Pausing:** while the map is open, gameplay pauses (timeScale 0 or equivalent) and input to the player, attack and dialogue is ignored. Closing restores it.
- **Placement:** works in the core arena (shows a "core" indicator) and on the between-runs screen (closed or not openable there).
- **UI:** readable at 960×600 (the WebGL canvas), uGUI or OnGUI, consistent with the existing HUD. Placeholder names (e.g. "Bridgelands\*") appear only if face labels are shown, and the subtle-science rule applies to any text.
- **Input:** a Map action added to the project-wide input actions (M and Tab).

**Never:**
- No minimap (the user chose the pause map only).
- No fast travel.
- No revealing unvisited screens or hidden currency.
- No global colour or text rewrite (1.22).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Open/close | Press M (or Tab) | Map opens and the game pauses; press again to close and resume | Ignored on the between-runs screen |
| Fog | New run | Only Town's start screen and screens visited so far are lit | No error expected |
| Visit | Walk onto a new screen | That cell is lit in its region colour next time the map opens | No error expected |
| Player | Anywhere on the cube | Marker on the right face and cell, with facing | Core arena: "core" indicator |
| Orientation | Every net-adjacent face pair | Touching cells are real cube neighbours | Test fails otherwise |
| Markers | Gate, item, trial or portal on a visited screen | Shown; opened gates and done trials are styled differently | Unvisited: not shown |
| Shuffle | Leave town (reveal) | The map reflects the revealed layout | No error expected |
| New run | Continue or F10 | Fog resets; the new layout is shown | No error expected |
| Truth | Seeds 1, 7, 42 after visiting all screens (debug) | Every cell's face, theme and markers match the world | Test fails otherwise |

</frozen-after-approval>

## Code Map

- `game/Assets/Scripts/Cube/CubeModel.cs`: face bases (normal, right, up), `TryStep`, `Step`, `FaceWithNormal`. Use these to derive the net layout and per-face rotation.
- `game/Assets/Scripts/Cube/CubeWorld.cs`:
  - `ThemeColor(Theme)` (line 223)
  - `Layout`, `ModuleAt`, `ScienceRevealed`
  - `Rebuilt` and `Revealed` events
  - the placed gates, pickups, trials and portals (see how CoreArena builds portals and how faces register trials)
- `game/Assets/Scripts/Cube/CubeNavigator.cs`: `Current` (face and cell), `Facing`, `InCoreArena`. Visit tracking hooks here or in a new `VisitTracker`.
- `game/Assets/Scripts/Cube/Run/RunLoop.cs` (`ScreenOpen`, `StartRun`), `Combat/CombatHud.cs` (HUD style), `CubeDebug.cs` (F-keys; the overlay might add "M map").
- `game/Assets/Settings/InputSystem_Actions.inputactions`: add a Map action to the Player map (M, Tab).
- `game/Assets/Editor/CubeSceneBuilder.cs`: add the map component and regenerate the scene.

## Tasks & Acceptance

**Execution:**
- [ ] `Scripts/Cube/Map/CubeNet.cs` (pure): net placement per face plus the cell rotation from the face bases, so any Town position works. A test checks adjacency across every net edge.
- [ ] `VisitTracker.cs`: the visited set per run, fed by navigator screen changes and reset on rebuild.
- [ ] `MapMarkers.cs`: collects gates (opened or not), pickups, trials (done or not) and portals on visited screens from the live world.
- [ ] `CubeMap.cs`: the UI that draws the net (faces, cells, colours, markers, player), opens and closes on the Map action, and pauses.
- [ ] Input action, scene wiring, and the debug overlay line.
- [ ] Tests: EditMode (net adjacency for every orientation, visit reset, markers from data); PlayMode (open/close pauses, fog, visit lighting, player marker, shuffle and new run, truth on 3 seeds using a debug "visit all").

**Acceptance Criteria:**
- Given the full suites (PlayMode in class batches) and the 50-seed sweep, when run, then all pass.
- Given the Cube scene, when the player presses M while exploring, then the map shows the unfolded cube with visited screens, themselves, Town and discovered content, oriented so that walking off a face matches the map.

## Implementation Notes

- Checkpoint 1 approved under the user's standing instruction (2026-10-06).
- Verified after the review patches: EditMode 237/237, PlayMode 148/148 (six class batches), seed sweep 50/50. User playtest PENDING.

## Review Triage Log

Pass 1 (quick lens): high 0, medium 0, low 5, false 0, maybe-false 0. The reviewer independently re-derived the net orientation (sizes 1 to 3, every centre face, 4 rotations): 0 mismatches. The pause freezes all gameplay correctly.

| Verdict | Route | Finding | Evidence / action |
|---|---|---|---|
| low | patch | Map arrow and attack facing carry the previous run's direction; the entry-facing fallback never runs | PlayerMover.Facing never resets. It now resets on teleport and run start, with a test. |
| low | patch | Sealed faces show while unvisited (fog leak) | CellView ignored visited for sealed. Unvisited cells are now always dark. |
| low | patch | An Interact press on the map-closing frame can reach MassMitt.LateUpdate | Undefined LateUpdate order. Unpause is now ordered after gameplay. |
| low | patch | Debug F-keys work while paused, and RevealMap returns true with no world | Debug keys are now ignored while paused (except F1). |
| low | patch | No test that Interact and Escape are ignored while the map is open | Test added. |
| low | reject | F11 RevealMap goes beyond "visit all" and is taken by the browser in WebGL | Editor/dev-only debug convenience, used by the truth test. It doesn't affect players. |

## Verification

**Commands:**
- Unity batch `-runTests -testPlatform EditMode`; PlayMode with `-nographics` in `-testFilter` class batches; `-quit -executeMethod CubeSceneBuilder.RunSeedSweep` -- expected: all pass, 50/50.
