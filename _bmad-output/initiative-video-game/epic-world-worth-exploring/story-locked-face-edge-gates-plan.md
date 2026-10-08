---
title: 'Locked face-edge gates'
type: 'feature'
ticket: '9'
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

**Problem:** From playtest 2:
- "We should make the edges smaller, you enter each screen through a gate or door if they are on the edge of a face."
- "Gates should be locked at first, requiring certain equipment to access. Each edge can have a different gate allowing users to gain access to the world in different ways."

**Approach:**
- **Locks.** Face-edge doorways (the arches from 1.25) start locked. Each needs either a key (from 4.7's roadblock rewards; a key is used up) or a specific piece of equipment, such as a face's signature item.
- **Different locks on different edges,** so the player has several possible first routes out of Town. The world opens in different orders on different seeds.
- **Readability.** A locked arch shows clearly what it needs: a keyhole, or the item's icon.
- **The sweep** proves that every screen, fragment, core portal and required item is reachable, and that at least two different edges can be the first one opened.

</frozen-after-approval>

## Boundaries & Constraints

- The seed sweep stays at 50/50. Re-record determinism, with a note.
- The subtle-science and voice rules still hold (InWorldText, voices-and-names.md).
- The readability tests stay green.
- PlayMode tests run in class batches.
- Nothing from later Epic 4 stories, except the hooks they need.

## Tasks & Acceptance

- [ ] Lock model per face edge: a seeded lock kind (key or a specific item), on its own stream.
- [ ] Locked arch visuals and interaction: swing or interact to open, keys used up, with a HUD key count.
- [ ] Item placement, the sweep and DoorwayReach handle locks.
- [ ] Tests:
  - on seeds 1–50, everything is reachable;
  - there are at least 2 possible first edges;
  - a key opens a key lock and is used up;
  - an item lock opens with that item only;
  - the suites pass.

## Routed from the 4.7 review

- **(medium) Unopenable bonus chests.** About 16 bonus chests a run sit behind optional gates that need variants this run never rolls, so they can't be opened. They become key-locked bonus alcoves: spending a key opens one, worth 10 Jumbles\* instead of the 5 the key cashes in for. Progression locks on face edges must not be starvable. Either they use a separate face-key pool, or every required lock has its own key, reachable before it and not spendable elsewhere. The sweep proves that required keys are at least required locks, however the player spends.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
