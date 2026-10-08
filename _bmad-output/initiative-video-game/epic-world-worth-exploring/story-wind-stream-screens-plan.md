---
title: 'Wind stream screens'
type: 'feature'
ticket: '11'
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

**Problem:** The user said: "Wind could behave similarly, they are pushed in a single direction if they move into a wind stream."

**Approach:** The wind hazard becomes wind-stream puzzle screens on the Earth & Atmosphere face:
- **Streams:** a stream carries the player one way, with no steering inside it, until they leave it. Every stream ends on floor or a grip tile.
- **Generation:** a generator plus a solver, at the research targets for wind streams (regions, minimum rides, decoys, the cost of riding back to the entry). Fan switches can appear from the mid tier upward.
- **Goal:** a reward roadblock (4.7), plus doorway routes.

</frozen-after-approval>

## Boundaries & Constraints

- The seed sweep stays at 50/50. Re-record determinism, with a note.
- The subtle-science and voice rules still hold (InWorldText, voices-and-names.md).
- The readability tests stay green.
- PlayMode tests run in class batches.
- Nothing from later Epic 4 stories, except the hooks they need.

## Tasks & Acceptance

- [ ] Stream model, solver and generator, on its own stream, at the research targets.
- [ ] Runtime stream movement and stream art, including visible flow.
- [ ] Earth plan, placement and sweep integration.
- [ ] Tests:
  - every wind screen on seeds 1–50 is solvable and within the targets;
  - a runtime test rides one solution;
  - the suites and the sweep pass.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
