---
title: 'Bigger screens, thinner walls'
type: 'refactor'
ticket: '1'
created: '2026-10-07'
status: 'draft'
baseline_revision: ''
route: 'full'
route_source: 'auto'
risk: 'high'
review: ''
review_source: ''
lenses_ran: []
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

- [ ] Screen size 24×15 and the camera.
- [ ] Half-tile walls.
- [ ] Character, NPC and enemy scale and colliders; speed tuning.
- [ ] Modules, plans and spots rescaled.
- [ ] Arena and timed doors retuned.
- [ ] Regenerate everything; re-record determinism twice and compare.
- [ ] Full verify.

**Acceptance:** all suites and the sweep pass. Screenshots of Town, one screen per face, an island screen and the arena look roomier, and people look bigger.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
