---
title: 'Temples'
type: 'feature'
ticket: '14'
created: '2026-10-08'
status: 'draft'
baseline_revision: ''
route: 'full'
route_source: 'auto'
risk: 'medium'
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

**Problem:** In playtest 2 the user asked for "some sort of temple with an item to collect like in Zelda".

**This story is optional:** fragments and repairs (4.8) now give the boss benefits.

**Approach:**
- **Temple screen:** each face's signature item sits in a temple screen behind that face's hardest roadblock.
- **Look:** the temple is a distinct styled screen or interior, in the race's architecture.
- **Benefits:** the temple item does not grant a boss benefit.

</frozen-after-approval>

## Boundaries & Constraints

- The seed sweep stays at 50/50. Re-record determinism, with a note.
- The subtle-science and voice rules still hold (InWorldText, voices-and-names.md).
- The readability tests stay green.
- PlayMode tests run in class batches.
- Nothing from later Epic 4 stories, except the hooks they need.

## Tasks & Acceptance

- [ ] Temple screen or interior per face, in the race's style.
- [ ] The signature item is placed in the temple, behind the face's hardest roadblock; placement and the sweep are updated.
- [ ] Tests:
  - on seeds 1–50, each item is in its temple and reachable;
  - the suites pass.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
