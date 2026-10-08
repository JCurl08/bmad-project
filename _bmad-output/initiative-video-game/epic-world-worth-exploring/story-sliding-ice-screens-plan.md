---
title: 'Sliding ice screens'
type: 'feature'
ticket: '10'
created: '2026-10-08'
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

- [ ] Ice grid model, solver and generator, on its own stream, at the research targets.
- [ ] Runtime sliding movement (grid-snapped), pits that respawn at the entry, grip tiles, and art.
- [ ] Earth plan, placement and sweep integration (DoorwayReach-style reachability through ice).
- [ ] Tests:
  - every ice screen on seeds 1–50 is solver-solvable from each connected doorway, within the targets;
  - a runtime test performs one solution;
  - the suites and the sweep pass.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
