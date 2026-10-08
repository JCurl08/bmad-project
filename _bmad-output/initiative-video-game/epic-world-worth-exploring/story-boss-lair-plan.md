---
title: 'Boss lair'
type: 'feature'
ticket: '13'
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

**Problem:** The user said: "Last thing is the different entrances to the boss, I would like only one to actually lead to the boss fight, the others lead to an underground part that functions as a part of the lair. Inside you can see people trapped and they can give you hints on how to defeat the boss."

**Approach:**
- **Portals:** one seeded core portal leads to the arena. Every other portal leads to an underground lair room: a sub-screen like the arena and the 4.6 interiors.
- **Prisoners:** each lair room holds trapped people from the races, behind bars, in their voices. They give hints about Primsworth the Demon (how the fight works) and about the repair benefits (which face gives what), and possibly where the real portal is.
- **Leaving:** the player can always walk back out.
- **Map:** shows lair rooms once visited and marks the real portal once it's known.

</frozen-after-approval>

## Boundaries & Constraints

- The seed sweep stays at 50/50. Re-record determinism, with a note.
- The subtle-science and voice rules still hold (InWorldText, voices-and-names.md).
- The readability tests stay green.
- PlayMode tests run in class batches.
- Nothing from later Epic 4 stories, except the hooks they need.

## Tasks & Acceptance

- [ ] Real-portal roll on its own stream; lair rooms; exit back to the portal.
- [ ] Prisoner NPCs with hint lines in voice (fight mechanics, benefits, the real portal's face).
- [ ] Map lair markers.
- [ ] Tests:
  - on seeds 1–50, exactly one portal reaches the arena and every other one reaches a lair room with a hint-giver;
  - the player can walk back out;
  - the suites and the sweep pass.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
