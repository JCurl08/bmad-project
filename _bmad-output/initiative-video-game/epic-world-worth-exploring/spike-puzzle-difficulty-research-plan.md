---
title: 'Puzzle difficulty research'
type: 'chore'
ticket: '2'
created: '2026-10-07'
status: 'done'
baseline_revision: 'c13df8c'
route: 'oneshot'
route_source: 'auto'
risk: 'low'
review: ''
review_source: ''
lenses_ran: []
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-video-game/change-playtest-3-world-redesign/change-playtest-3-world-redesign.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The user said: "For all the puzzles in the game I think we can make them a more reasonable difficulty by taking a look at some examples in other games on the market. Bridge crossing puzzles we can look at analogous puzzles, sliding puzzles there should be examples in games or documented online".

**Approach:** Survey shipped games' versions of each puzzle type. Write a research note, `puzzle-difficulty-research.md`, beside this epic. For each generator, it gives cited examples, what makes them fun, and numeric targets the generators and tests can check:
- island bridges
- sliding ice
- wind streams
- boulders and timed doors
- beak gates, stones, gap gates, weather wand gates
- trials

Targets cover solution length, branching, dead ends and red herrings, and the size of the difficulty steps between early and late screens. Each target must be measurable by a solver.

</frozen-after-approval>

## Tasks & Acceptance

- [x] Research note with citations and numeric targets per generator; a recommendation for the 4.12 island-bridge rule.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
- Done 2026-10-07: `puzzle-difficulty-research.md` gives cited examples, fairness rules and solver-measurable early/mid/late targets per generator, the ice and wind generator recipes, one-line verdicts on existing puzzles, and recommends "light every plank" (Euler trail; re-crossing a lit plank snuffs all, so any island can be a start) for 4.12. Some sources were 403 or not fetched and are marked [K]. No game code touched; not committed.
