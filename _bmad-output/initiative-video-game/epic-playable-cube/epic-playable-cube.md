---
type: epic
title: "One complete run, town to boss"
parent: initiative-video-game
covers: [CAP-1, CAP-2, CAP-3, CAP-4, CAP-5, CAP-6, CAP-7, CAP-8, CAP-10, CAP-11, CAP-12, CAP-13, CAP-25, CAP-26, CAP-27]
after: []
assignee: ""
risk: high
---

# One complete run, town to boss

## Description

Delivers every Must capability in the spec. In a WebGL build, a player starts in town and leaves onto a seeded, shuffled cube of 3×3 faces. All five science faces are built, each with its signature puzzle, item, gates, race and trial puzzle, and a cube-net map shows the way. They fight item-weak enemies, beat the core boss, and end the run. Meta currency buys stat upgrades that persist into the next run. This epic also lays the platform baseline and the shared systems that epic-varied-runs extends.

## Outcome

A complete, replayable run is playable in the browser. The signal is the spec's success signal, met at the Must tier.

## Requirements

The spec is the requirement source: CAP-1 to CAP-8, CAP-10 to CAP-13 and CAP-25 to CAP-27 in `spec-entropy-cube.md`, with detail in its companions. CAP-9 (swappable NPC parts) moved to epic-varied-runs; the parts built here (entries 5, 22) stay in code for it. The rewritten CAP-2, CAP-7, CAP-8, CAP-11 and CAP-13 are completed by epic-world-worth-exploring.

## Done when

1. A WebGL build on itch.io plays a full seeded run: town, a shuffled 3×3 cube with all 5 science faces (puzzle, item, gates, trial), a cube-net map, the core boss (or walking out of its arena), run end, persisted stat upgrades, next run.
2. The seed sweep finds no softlock across 3+ seeds, and the same seed reproduces the same cube.
3. Every enemy type has an item that makes it markedly easier (CAP-10).
4. Upgrades and meta currency survive closing and reopening the browser tab.
5. The Must fallback is allowed if its checkpoint triggers: a simpler boss instead of the single-phase demon.

## Boundaries

All Must-tier capabilities. Not the Should tier (epic-varied-runs) or the Could tier (epic-stretch). Hooks this epic must expose for epic-varied-runs:
- an NPC role slot that includes merchant
- a per-race hostility flag that makes NPCs attack using enemy behaviour
- a difficulty parameter on each puzzle generator (e.g. boulder count)
- a phase slot on the boss
- per-ability version tracking in the save
- the "commitment closes doors" mechanic, delivered here through one-way gates



## References

- spec — _bmad-output/initiative-video-game/spec-entropy-cube/spec-entropy-cube.md, Capabilities CAP-1 to CAP-8, CAP-10 to CAP-13 and CAP-25 to CAP-27, Constraints
- change — _bmad-output/initiative-video-game/change-playtest-feedback-full-cube/change-playtest-feedback-full-cube.md
- design — _bmad-output/initiative-video-game/spec-entropy-cube/faces-and-races.md
- design — _bmad-output/initiative-video-game/spec-entropy-cube/lore-and-tone.md
- conventions — _bmad-output/initiative-video-game/spec-entropy-cube/stack.md
- constraint — _bmad-output/initiative-video-game/spec-entropy-cube/scope-tiers.md, Checkpoints and Must-tier fallback
- plan — _bmad-output/initiative-video-game/brainstorm-campy-zelda-roguelite/build-plan-2-weeks.md, Days 1–11

## Notes

- Decision: target is to finish by the end of the week of 2026-10-05, with longer days (user, 2026-10-05).
- Decision: each face ends in a harder trial puzzle instead of a miniboss (user, 2026-10-05).
- Decision (superseded 2026-10-06 by change-playtest-feedback-full-cube): ship Biology, Chemistry and Physics at 2×2, with the other faces sealed (user, 2026-10-05).
- Decision: Unity project in `game/`, Universal 2D (URP); built interactively with bmad-build, so no checkpoints are set on entries (user, 2026-10-05).
- Decision: entry 1 is the tracer bullet, including the first itch.io upload. Biology is the vertical slice before the other faces, the boss comes after combat, meta progression comes last, and a Refactor sweep closes before release (2026-10-05).
- Decision: the seed-sweep tool and debug tools are deliverables of this epic, since the checkpoints and Done when depend on them (2026-10-05).
- Decision: in a run, the beak type not rolled still has its gates placed, but they are optional (user approved, 2026-10-05).
- Decision: town has every race, including the sealed faces' shape people and dinosaurs, with placeholder art (user approved, 2026-10-05).
- Decision: 14 entries, later 23 after the course correction, accepted as one lane with one owner, so no split (user approved 2026-10-05; re-confirmed through the approved change 2026-10-06).
- Decision: added entry 15 'Readability pass' before the release after the user's first playtest found the placeholder visuals and long dialogue confusing; world art from Kenney (CC0), NPCs keep stacked Lego parts redrawn clearly; 'fell apart' death text replaced (user, 2026-10-06).
- Decision: entries 16–22 added after the first playtest (dev keys, full 3×3 cube, isotope stage item, Math face, Earth & Atmosphere face, cube-net map, look and tone pass), all before the release (14). See change-playtest-feedback-full-cube (user approved, 2026-10-06).
- Decision: set check on the re-slice made 18 to 22 sequential (shared files), added entry 23 (second refactor sweep) before the release, and added CAP-3, CAP-25 and difficulty parameters to the face entries (2026-10-06).
- Decision: after playtest 3, CAP-9 moves to epic-varied-runs; entry 26 (island screen fixes) is added before the release (14); the redesign goes to the new epic-world-worth-exploring, built next. See change-playtest-3-world-redesign (user approved, 2026-10-07).
