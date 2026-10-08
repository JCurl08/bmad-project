---
title: 'Temples'
type: 'feature'
ticket: '14'
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

**Problem:** On 2026-10-08 the user redesigned the temples: "Temples become unlocked when you fix all the fregments of a face by collecting them and giving them to the village leader. The temples should contain a difficult puzzles based on the puzzle of that face, then when you complete the puzzle some magic fixes the fregements and gives you a gem that gives you an upgrade for the boss."

**Approach:**
- **The temple.** Each face's town has a temple building, drawn in the race's own style. It stays sealed until all of the face's fragments have been handed to its leader. Handing them in no longer grants the boss benefit directly: the leader opens the temple instead.
- **Inside the temple.** The temple is an interior sub-screen, following the 4.6 interior and 4.13 lair pattern. It holds one difficult puzzle based on that face's own puzzle:

  | Face | Temple puzzle |
  |---|---|
  | Math | A late-tier "light every plank" bridge puzzle |
  | Earth | A late-tier ice or wind puzzle |
  | Physics | A hard boulders-and-timed-door puzzle |
  | Chemistry | A hard stone-order puzzle |
  | Biology | A hard beak-target puzzle |
  | Town | A short mixed puzzle (assumption: Town has no puzzle of its own) |

  Each puzzle is solver-checked, or proven solvable by construction, at a difficulty at or above the face's hardest tier in the research note.
- **The reward.** Solving the puzzle plays a short magic moment: the fragments mend and the face is repaired. The player receives that face's **gem**, which grants the face's boss benefit (the 4.8 mapping). The HUD and map show gems.
- **Getting out.** The player can always leave the temple, and the puzzle resets on leaving until it is solved.

</frozen-after-approval>

## Boundaries & Constraints

- The seed sweep stays at 50/50. Re-record determinism, with a note.
- The subtle-science and voice rules still hold (InWorldText, voices-and-names.md).
- The readability tests stay green.
- PlayMode tests run in class batches.
- Nothing from later Epic 4 stories, except the hooks they need.

## Tasks & Acceptance

- [ ] The leader hand-in unseals the temple. The face is repaired and its benefit granted only through the temple gem. Update the 4.8 tests and the lair hint wording ("mending X" becomes the temple gem).
- [ ] Temple building per town, plus a temple interior sub-screen, with art in the race's style.
- [ ] One hard puzzle per face, reusing that face's generators or solvers at the top tier (or above), with a solvability check per seed.
- [ ] Solve effect: fragments mended, gem granted, benefit applied in the arena; gem HUD and map.
- [ ] Sweep: every temple puzzle on seeds 1–50 is solvable. EditMode check over seeds 1–500 for the generated ones.
- [ ] Tests: hand-in opens the temple; a runtime test solves one temple per face type; the gem changes its arena parameter. Run a full verify. Screenshots.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
