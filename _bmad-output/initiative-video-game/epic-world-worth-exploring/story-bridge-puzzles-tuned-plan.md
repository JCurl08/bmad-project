---
title: 'Bridge puzzles, tuned'
type: 'feature'
ticket: '12'
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

**Problem:**
- The user liked the spread-out island screens. They also want "some indication that it is activating something. Like the bridge changing colour".
- The user asked that puzzles get a reasonable difficulty, based on similar puzzles in other games.
- The research note recommends a "light every plank" rule (an Euler trail).

**Approach:** Rework Math's island screens into a "light every plank" puzzle:
- **Lighting:** each whole plank on the reward network lights up the first time it is crossed.
- **Reset:** crossing a lit plank again snuffs every plank, with a red flash. That lets any island be a clean start.
- **Goal:** when every plank is lit, a light chain runs to the reward roadblock (4.7) and opens it.
- **Persistence:** leaving the screen resets an unsolved puzzle. A solved one stays solved for the run.
- **Broken spans:** they count once mended, and then show gold.
- **Feedback:** lamps on the roadblock show the count, and an island's rim glows when all its planks are lit.
- **Difficulty:** the tiers come from the research targets (plank counts, odd islands, random-walk success rate).
- **Lone doorway:** keep 1.26's readable lone doorway, or replace it with something better that the research suggests.

</frozen-after-approval>

## Boundaries & Constraints

- The seed sweep stays at 50/50. Re-record determinism, with a note.
- The subtle-science and voice rules still hold (InWorldText, voices-and-names.md).
- The readability tests stay green.
- PlayMode tests run in class batches.
- Nothing from later Epic 4 stories, except the hooks they need.

## Tasks & Acceptance

- [ ] Puzzle rule and state (light, snuff, solved), with feedback lamps and rim glow.
- [ ] Generator and solver to the targets (connectivity, parity, start reachable from each doorway, exhaustive trail count up to 16 planks).
- [ ] Reward roadblock integration and sweep.
- [ ] Tests:
  - every island screen on seeds 1–50 is solvable from each connected doorway, within the targets;
  - the roadblock opens only when the goal is met;
  - a runtime test solves one;
  - the suites and the sweep pass.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
