---
title: 'Bigger screens, thinner walls'
type: 'refactor'
ticket: '1'
created: '2026-10-07'
status: 'built'
baseline_revision: 'f0907a9'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/spec-entropy-cube.md'
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/stack.md'
  - '{project-root}/_bmad-output/initiative-video-game/change-playtest-3-world-redesign/change-playtest-3-world-redesign.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The user said: "make the walls on the side of the screen a bit narrower since it seems to crowd each panel, we could also increase the size of each screen". Earlier they said "the people are all really small… scale the size of each screen up and increase the size of the people at the same time". They chose about 2× the screen area.

**Approach:** One screen-size change, done first so that every later Epic 4 story builds on the final size:
1. **Screen size:** `ScreenMath.DefaultScreenSize` goes from 16×10 to 24×15 world units. The camera frames one screen on the 960×600 canvas.
2. **Walls:** half a tile thick, with the doorway still centred and at least 2.5 wide.
3. **People:** the player, NPCs and enemies are drawn larger, so they read about as big on screen as before, or a little bigger. Their colliders grow by the same factor, and movement speed is retuned so that crossing a screen takes about as long as before relative to its size.
4. **Content:** modules, layouts, face plans, spot filters, lanes, the edge band, island screens, the core arena and the boss scale with the screen or are re-laid out for it. Content density per screen stays the same or grows a little; the screens should not feel emptier.

</frozen-after-approval>

## Boundaries & Constraints

- **No new content** (towns, rewards and so on come in later stories). Puzzles keep their rules.
- **Boss:** the skill check stays 60–120 s on its seeds. Arena tuning may be rescaled to hold that.
- **Physics timed doors:** retuned so the required boulder counts hold. Use the 4.2 research note (`puzzle-difficulty-research.md`) on timing slack.
- **Sweep:** 50/50 on seeds 1–50. The determinism fixture is re-recorded, and must be stable across two runs.
- **Text:** the subagent rules of `InWorldText` are unchanged.
- **WebGL:** the build is left to the user. Note any memory or size concern.
- **Testing:** PlayMode in class batches.

## Code Map

- `Tracer/ScreenMath.cs` (`DefaultScreenSize`), `CubeWorld.ScreenSize`, camera setup.
- `Modules/ScreenEdges.cs` (wall thickness, doorway, band).
- `Editor/ModuleLibraryBuilder.cs`: module geometry is in world units, so scale or re-author it.
- Lanes and the edge band.
- Face plans' spot lattices: `BiologyPlan`, `ChemistryPlan`, `PhysicsPlan` (boulders, doors, timing), `MathPlan`/`IslandLayout`, `EarthPlan`.
- `TownPlan` spots, `FaceContent` spot filters, `CoreArena` and `MaxwellDemon` (arena size, particle count, gaps).
- `PlayerMover` speed and collider, `NpcFactory` scale, the enemy prefabs.
- Tests that hard-code 16×10 coordinates.

## Tasks & Acceptance

- [x] Screen size 24×15 and the camera.
- [x] Half-tile walls.
- [x] Character, NPC and enemy scale and colliders; speed tuning.
- [x] Modules, plans and spots rescaled.
- [x] Arena and timed doors retuned.
- [x] Regenerate everything; re-record determinism twice and compare.
- [x] Full verify.

**Acceptance:** all suites and the sweep pass. Screenshots of Town, one screen per face, an island screen and the arena look roomier, and people look bigger.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
- **Approach: one units change.**
  - `ScreenMath.DefaultScreenSize` is 24×15, and a new `ScreenMath.ContentScale` = 1.5 (24/16 = 15/10).
  - Every world length and every speed that was authored for 16×10 is multiplied by it. Times, counts, damage and the per-mass dilation are unchanged.
  - So every distance ÷ speed is the same as before: crossing a screen, timed-door walks, the boss fight and NPC wander timing all keep their timings.
  - Authored tables stay in the first units and are scaled once where they are used: module `Block`s and `Scaled(...)` in `ModuleLibraryBuilder`, ruin sizes, NPC part sizes in `NpcFactory`. Other constants read `x * ScreenMath.ContentScale`.
  - The camera needs no code change. `ScreenCamera` and the scene builders read `DefaultScreenSize`, so the ortho size is 7.5 and the 960×600 canvas shows 40 px per unit (60 before).
- **Walls and band.**
  - `ScreenEdges.EdgeWallThickness` is 0.5.
  - Doorways are the lane width, `ExitLaneHalfWidth` 1.875 (3.75 wide, centred).
  - `ScreenModule.EdgeClearance` = wall + 0.75 = 1.25: the old 0.5 clear strip inside the wall, scaled.
  - The content area grows from 19.5×10.5 (scaled) to 21.5×12.5. Anything derived from the band (boulder bounds and reach grid, Town and face spot candidates, booth limit, Earth crevice grid) uses the extra room.
  - Gate arches are `ArchWidth` 7.5 (twice the doorway, so the art's open middle is the doorway) and `ArchDepth` = `EdgeClearance`. They no longer fit in a half-tile wall: the posts reach 0.75 into the clear strip (visual only, no collider; module content stays out of the band). The arch EditMode test now checks that the arch stands on the wall and stays in the edge band.
  - Sealed-face walls and the Tracer scene's walls follow the same rules: sealed walls at `EdgeWallThickness`; the Tracer scene scaled.
- **People.**
  - The player's collider is 0.6 (`TimeField.PlayerRadius`) and its sprite 1.5 units. NPC parts, body width and height are ×1.5, and so is the enemy body radius and art.
  - Their colliders read exactly as big on the canvas as before (×1.5 world at 2/3 the pixels per unit). Since the review their art is drawn `CubeSettings.PeopleArtScale` (4/3) larger again, so people read about 1.33× larger on the canvas than before, and 2 tiles tall (see Review patches).
  - Speeds: player 6 → 9 (crosses 24 units in the old 2.7 s), NPC wander, flee and return, enemy chase 2.2 → 3.3, and sight radii ×1.5. Reaches (attack 0.85 → 1.275, thin beak 2.5 → 3.75; the item catalog builder now keeps an existing Thin Beak asset's reach in step), talk range and hitboxes ×1.5.
- **Re-laid out, not just scaled.**
  - Math island lattice: the columns spread over the wider water area (outer at 0.8 of its half-width, as before: 4.6 apart instead of 4.2).
  - The rows stay at 2.7×1.5 = 4.05, because the south doorway's island must still hold the open pickup at `OpenPickupOffset` (0, −3.75). A first try with spread rows failed 10 sweep seeds on exactly that.
  - Doorway islands now run from the wall to `DoorIslandReach` (0.9) past their lattice line (`DoorIslandDepth(side)`), so they grow into the extra height.
  - Earth crevice candidates use a centred grid over the band. Physics booths may sit out to the band (10.75 instead of 9.75).
- **Content density:** the same per screen (counts are per screen and unchanged). More rolled things fit, because the band gives room:
  - timed doors needing 1/2/3 boulders went from 110/87/103 to 96/90/114, because fewer cramped screens lower their rolled counts;
  - gate boulders went from 716 to 757;
  - Earth crevices went from 95 to 99, against 103 → 99 wind bands, a fallback kind.
- **Timed doors (research note §1d).** `TimeField.Slack(required)` sets the open time at the required count over the switch walk:
  - 1 boulder: 1.35 (early band ≥ 1.35). One short shuts 15.6% early (target 15%).
  - 2 boulders: 1.20 (mid band 1.20–1.30). One short shuts 12.7% early (target 12%).
  - 3 boulders: 1.14 (late band 1.12–1.20). One short shuts 10.4% early (target 10%).
  - Each is as loose as the "one short" bound allows with dilation 0.6 per boulder. The old rule gave 1.23 / 1.16 / 1.12.
  - Required counts are still exact: no mass, and required − 1, are impassable; required passes. The sweep checks this, and an EditMode test checks the bands.
- **Arena and boss.**
  - Pure scaling: `CoreArena.InnerHalf` = screen half − `WallThickness` (0.75) = 22.5×13.5. Membrane, door, gaps, exit, particle spots, particle radius, knock 9 → 13.5, drift and its acceleration, Demon nudge and move speeds, pulse speed 6.5 → 9.75 and hit radii are all ×1.5.
  - The Demon's timings (restore interval, fatigue, pulses) are unchanged. No retune was needed.
  - Skill check (keyboard bot, its distances scaled too): 78.6 s (seed 1234), 90.5 s (7) and 105.5 s (42), all inside 60–120. It was 94–105 s before.
- **Regenerated:** art catalog (unchanged content), module library (all 30 prefabs), item catalog (Thin Beak reach 3.75), Tracer scene, Cube scene.
- **Determinism.** The fixture was recorded twice and the two recordings are byte-identical; `DeterminismTests` passes on it.
  - Changed: `physicsPlan` (positions and timings), `earthPlan` (hazard rects), and `biologyPopulation` and `chemistryPopulation` (trial spots), 50 seeds each.
  - Unchanged: `model`, `layout`, `items`, `hidden`, `streams`, `town`, `mathPlan`, and every NPC and population field.
- **Tests.**
  - Hand-made geometry in the PlayMode and EditMode tests is multiplied by a local `K = ScreenMath.ContentScale`, written `x * K` next to the old number.
  - Changed by intent: `DefaultScreenSize_Is24By15...`, the 24×15 floor pattern, the arch and edge-wall probes for half-tile walls, the timed-door slack bands, and `BasePlayerSpeed` 9.
  - `MathComponentTests` pushes north for 1.2 s instead of 1.1 s, because the south arrival is a unit further out.
- **Screenshots** are before/after of seed 7 in the scratchpad's `snaps41/`: Town start, (0,0) on each science face, an island screen and the arena. They came from a temporary snapshot test, since deleted.
  - Content reads the same size on the canvas; the walls are a third of their old on-screen thickness; the floor reaches almost to the screen edge.
  - People were the same size on screen in this first pass; the review patch draws them 1.33× larger (`after2-*.png`).
- **WebGL (build left to the user).**
  - No new assets, and object and collider counts are the same. Floor-pattern textures are 24×15 texels per screen instead of 16×10, a few KB in all.
  - Tiled walls draw more but thinner tiles. World coordinates span about 280 units. No memory or size concern is expected.
  - One thing to eyeball: pixel art is now drawn at 2.5 screen px per art pixel (3.75 before). Both are non-integer, but the smaller ratio may show more uneven pixel widths.
- **Review patches (pass 1).**
  - **People read bigger on screen (the user's intent: "the people are all really small").**
    - New `CubeSettings.PeopleArtScale` = 4/3, visual only. It applies to the player's sprite (now 2 units, 80 canvas px against 60 before), NPC part sprites (stacked up from the feet), enemy art (`Enemy.VisualSize`) and the weakness-mark offset. Newton's apple falls on the drawn head.
    - Colliders, speeds, spots and every gameplay distance are unchanged: the NPC collider keeps the parts' own height and `MaxBodyHeight`, and the player keeps radius 0.6.
    - Sorting is unchanged: people draw over walls (−5), arches (−4) and floors, as before, so bigger sprites overlap walls rather than clip.
    - Two new PlayMode tests check that the drawn height on the 960×600 canvas is 1.33× the first screens' height while the colliders only scale with the screen: `NpcComponentTests.People_AreDrawnAThirdLarger...` (an NPC and an enemy) and `CubeSceneTests.Player_IsDrawnAThirdLarger...`.
    - Screenshots: `snaps41/after2-town-start.png`, `after2-face-Biology-00.png` and `after2-face-Physics-00.png`.
  - **Unscaled art:** ×`ContentScale` for Biology trial-target pips, Earth trial-piece pip size, the quake crevice's fossil, Newton's apple, the Physics trial goal pad, the debris gate's swirl and Earth's calm-eye ring. A grep finds no other literal art size left unscaled.
  - **Arrival:** `CubeNavigator.EntryInset` (1.125, was a serialized field) is now one shared constant. `IslandLayout.Arrival` uses it, so the island checks in the sweep and tests use the point where a face crossing really puts the player (1.125 inside the edge, was 0.95). No layout changed.
  - **Timed-door test margin:** at 3 boulders, the EditMode check "one short closes before 0.9 of the walk" passes by only 0.4% of the walk (0.896). That is accepted: the research target for one short at 3 boulders is 10% early, and 10.4% is met.
  - **Determinism:** no position changed (the art scale is visual, and arrival only feeds checks). `DeterminismTests` passes on the fixture as recorded, so it was not re-recorded.

## Verification

**Result (2026-10-07, `verify.sh` parts run in the foreground as `v41b`):**
- The background `v41a` run was stopped for low memory during EditMode, so every part was rerun in the foreground.
- EditMode 263/263.
- PlayMode batches 25/22/26/26/35/24/1, all passing.
- Skill checks Victory in 78.6 s (seed 1234), 90.5 s (7) and 105.5 s (42).
- `IslandWalkTests`: 0 problems.
- Sweep 50/50, 0 failing.
- `FIXTURE CHANGED` is the intentional re-record described above. The fixture was recorded twice, the two recordings are identical, and `DeterminismTests` passes on it.
- Not committed. WebGL build left to the user.

**Result after the review patches (2026-10-07, `verify.sh` steps run in the foreground as `v41c`):**
- EditMode 263/263.
- PlayMode batches 25/22/26/27/36/24/1, all passing. Batches 3 and 4 each have one new test: the player's and the people's drawn size.
- Skill checks Victory in 78.6 s, 90.5 s and 105.5 s (unchanged).
- `IslandWalkTests`: 0 problems.
- Sweep 50/50, 0 failing.
- `FIXTURE CHANGED` is the first pass's re-record; the review patches changed no signature.
- Not committed.

## Review Triage Log

Pass 1 (quick lens), triaged by the coordinator: high 0, medium 0, low 7, plus one user-intent item.

| Verdict | Route | Finding | Outcome |
|---|---|---|---|
| intent | patch | People read the same size on screen; the user asked for bigger people | Patched: `CubeSettings.PeopleArtScale` 4/3, visual only (player, NPCs, enemies). PlayMode tests check the canvas height ratio and the unchanged colliders. `after2-*` screenshots. |
| low | patch | Biology trial-target pips not scaled | Patched: ×`ContentScale`. |
| low | patch | Earth trial-piece pip size not scaled | Patched. |
| low | patch | Quake crevice fossil not scaled | Patched. |
| low | patch | Newton's apple not scaled | Patched (it also falls on the larger drawn head). |
| low | patch | Physics trial goal pad not scaled | Patched. |
| low | patch | Debris gate swirl not scaled | Patched. |
| low | patch | Calm-eye ring not scaled | Patched. |
| low | patch | `IslandLayout.Arrival` and the navigator's entry inset differ | Patched: one shared constant, `CubeNavigator.EntryInset`. |
| note | defer | The 3-boulder "one short before 0.9 of the walk" check passes by 0.4% | Accepted and noted: the research target (10% early) is met at 10.4%. |
