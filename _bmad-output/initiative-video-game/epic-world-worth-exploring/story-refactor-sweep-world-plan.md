---
title: 'Refactor sweep (world)'
type: 'refactor'
ticket: '15'
created: '2026-10-08'
status: 'draft'
baseline_revision: ''
route: 'full'
route_source: 'auto'
risk: 'low'
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

**Problem:** Stories 4.1–4.14 added many systems in two days: scale, character art, voices, towns, interiors, rewards, fragments, locks, ice, wind, bridges, the lair and temples. Their reviews deferred cleanups. Real runs also use clock seeds, not only seeds 1–50, so the solvability guarantees must hold for any seed.

**Approach:** cleanup only, with no gameplay change.
- **Consolidate shared patterns:** puzzle-screen plumbing (islands, ice, wind), sub-screen rooms (arena, interiors, lair), and the reward and lock hooks.
- **Remove dead code** left over from the replaced hazards and puzzles.
- **Robustness for any seed:** an EditMode generation-only sweep over seeds 1–500. It checks layout, items, rewards, locks proof and puzzle screens, with zero problems and no fallbacks. A runtime guard logs an error if any generated plan has problems at reveal.
- **Determinism:** the fixture stays unchanged without re-recording.

</frozen-after-approval>

## Boundaries & Constraints

- The seed sweep stays at 50/50. The determinism fixture must stay unchanged.
- The subtle-science and voice rules still hold (InWorldText, voices-and-names.md).
- The readability tests stay green.
- PlayMode tests run in class batches.
- Nothing from later Epic 4 stories, except the hooks they need.

## Tasks & Acceptance

- [ ] Consolidate the duplicates found in the 4.1–4.14 plans and reviews (extract only true duplicates; draw order unchanged).
- [ ] Remove dead code.
- [ ] Add the 500-seed generation-only robustness test and the reveal-time problem logging.
- [ ] Verify: all suites pass, the sweep is 50/50, and the fixture is unchanged.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
