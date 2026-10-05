---
type: initiative
title: "Entropy Cube: a campy 2D Zelda-like roguelite, shipped on itch.io"
parent: none
covers: [CAP-1, CAP-2, CAP-3, CAP-4, CAP-5, CAP-6, CAP-7, CAP-8, CAP-9, CAP-10, CAP-11, CAP-12, CAP-13, CAP-14, CAP-15, CAP-16, CAP-17, CAP-18, CAP-19, CAP-20, CAP-21, CAP-22, CAP-23, CAP-24, CAP-25]
after: []
assignee: ""
risk: medium
---

# Entropy Cube: a campy 2D Zelda-like roguelite, shipped on itch.io

## Description

A solo developer builds and ships a replayable 2D roguelite in Unity within 14 days at most, while learning Unity. The spec owns the capabilities, constraints and non-goals. This initiative delivers the Must and Should tiers.

## Outcome

Players on itch.io can play a full seeded run in the browser, and the developer gains working Unity experience. The signal is the spec's success signal.

## Done when

1. A WebGL build is live on itch.io and someone other than the developer can play it start to finish.
2. Three or more seeds play from town to the core boss with no softlock, confirmed by a seed sweep.
3. Every Must capability (CAP-1 to CAP-6, CAP-9 to CAP-13, CAP-25) is in the shipped build. Each Should capability (CAP-7, CAP-8, 3×3 faces, CAP-14 to CAP-17) is either present or cut following `scope-tiers.md`.
4. Two different seeds route the player through the faces in visibly different orders.

## Boundaries

One Unity project in one repo, with one owner. Not in scope: the spec's non-goals, and the Could tier (CAP-18 to CAP-24), which is deferred to epic-stretch. The tracer path is a player walking screen to screen on a seeded cube in a WebGL build.

- Touch point: itch.io (game page and WebGL upload). epic-playable-cube owns the first publish; epic-varied-runs owns the final publish.
- Touch point: GitHub remote JCurl08/bmad-project with Git LFS. Owner: epic-playable-cube.
- Touch point: Windows desktop fallback build. Owner: epic-varied-runs (final release step).

## References

- spec — _bmad-output/initiative-video-game/spec-entropy-cube/spec-entropy-cube.md
- constraint — same spec, section Constraints (timebox, WebGL, audience, "never say sort")
- plan — _bmad-output/initiative-video-game/brainstorm-campy-zelda-roguelite/build-plan-2-weeks.md (day order and checkpoints)

## Notes

- Decision: epics are cut by scope tier (Must, Should, Could), because each tier is a separately shippable outcome for one owner in one repo (user, 2026-10-05).
- Decision: platform baseline (Unity project, first WebGL build, repo setup) is folded into epic-playable-cube's opening story, not a separate epic (2026-10-05).
- Decision: aim to finish epic-playable-cube by the end of the week of 2026-10-05, working longer days (user, 2026-10-05).
- Assumption: epic-playable-cube publishes a first itch.io build when it closes, and epic-varied-runs owns the final polish day and the final publish.
- Decision: Could tier (epic-stretch) is a deliberate deferral outside this initiative's Done when (user, 2026-10-05).
- Decision: run currency (CAP-24) is cut for now (user, 2026-10-05).
- Decision: each science face ends in a harder trial puzzle instead of a miniboss (CAP-25). The core final boss stays a boss (user, 2026-10-05).
- Decision: Epic 1 ships Biology, Chemistry and Physics at 2×2, with the other faces sealed. Math, Earth & Atmosphere and 3×3 faces move to epic-varied-runs (user, 2026-10-05).
- Decision: the Unity project lives in `game/` (Universal 2D/URP), and stories are built interactively with bmad-build (user, 2026-10-05).
- Decision: the "commitment closes doors" mechanic and the save format are owned by epic-playable-cube. The save format is versioned and extensible, because epic-varied-runs and epic-stretch extend it (2026-10-05).
