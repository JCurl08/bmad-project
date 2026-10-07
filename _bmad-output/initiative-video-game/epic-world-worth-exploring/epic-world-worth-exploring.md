---
type: epic
title: "A world worth exploring"
parent: initiative-video-game
covers: [CAP-2, CAP-7, CAP-8, CAP-11, CAP-13, CAP-28, CAP-29, CAP-30, CAP-31, CAP-32]
after: [epic-playable-cube]
assignee: ""
risk: high
---

# A world worth exploring

## Description

Turns the shuffled cube of puzzle screens into a world of towns, cultures and rewards, from the user's third playtest and the open playtest-2 items. Screens grow to about 24×15 tiles with thinner walls and larger people. Every face's centre becomes a town with its own culture, statue, enterable building and leader. Every roadblock hides a reward, and fragments brought to a face's leader repair the face and weaken the boss. Face edges start locked behind keys and equipment. Earth gets real sliding-ice and wind-stream puzzles, Math's island screens get a real goal, and difficulty is set by a survey of shipped games. Only one core portal reaches the boss; the others drop into a lair of trapped hint-givers. Every character gets a hand-made design in the player's style, a meaningful name and their race's voice. The epic builds scale first, then systems, then content, then text and art polish, so every later story works at the final screen size.

## Outcome

A player explores a bigger, warmer cube with a town and leader on every face, rewards behind every roadblock, fragments that buy boss benefits, tuned puzzles, and a lair behind false portals. The signal is the spec's success signal, with the sweep at 50/50 seeds.

## Requirements

The spec is the requirement source: the rewritten CAP-2 (locked edge gates), CAP-7 (island screens), CAP-8 (sliding ice and wind streams), CAP-11 (one real portal, boss benefits) and CAP-13 (coins from roadblocks), plus CAP-28 to CAP-32, in `spec-entropy-cube.md`, with towns, voices, fragments and benefits in `faces-and-races.md` and the name rules in `lore-and-tone.md`.

## Done when

1. Every face's centre screen is a town with distinctive buildings, a statue, an enterable building, a leader, extra townsfolk and no enemies.
2. Every roadblock hides a reward, and every screen holds a puzzle, enemies or an object.
3. All fragments of a face are reachable on seeds 1–50, and repairing a face applies its benefit to the boss fight.
4. Sliding-ice and wind-stream screens are solver-checked, with difficulty within the spike's targets.
5. Only one core portal reaches the boss; the others reach lair rooms with hint-giving prisoners.
6. All characters have names and race voices, the sweep is 50/50, the boss skill check holds at 60–120 s with no benefits, and the build ships to itch.io.

## Boundaries

The rewritten Must capabilities and CAP-28 to CAP-32. Not the swappable-part NPC system (CAP-9) or the town draft (CAP-14), which epic-varied-runs builds on top of this epic's designs and towns. Not the Could tier. Temples (entry 14) are optional. Spec non-goals apply.

## References

- spec — _bmad-output/initiative-video-game/spec-entropy-cube/spec-entropy-cube.md, Capabilities CAP-2, CAP-7, CAP-8, CAP-11, CAP-13, CAP-28 to CAP-32, Constraints
- change — _bmad-output/initiative-video-game/change-playtest-3-world-redesign/change-playtest-3-world-redesign.md
- design — _bmad-output/initiative-video-game/playtest-2-feedback.md, sections Towns and Bigger changes
- design — _bmad-output/initiative-video-game/spec-entropy-cube/faces-and-races.md
- design — _bmad-output/initiative-video-game/spec-entropy-cube/lore-and-tone.md
- conventions — _bmad-output/initiative-video-game/spec-entropy-cube/stack.md
- constraint — _bmad-output/initiative-video-game/spec-entropy-cube/scope-tiers.md, Checkpoints

## Notes

- Waits on epic-playable-cube because: it reworks that epic's complete, verified run (screen modules, ItemPlacement, sweep, determinism fixture, faces, boss arena, map), and the first release (1.14) ships before release 2.
- Decision: finish and ship epic-playable-cube as a baseline, then build this epic, before epic-varied-runs; epic order 1, 4, 2, 3 (user approved, 2026-10-07).
- Decision: screens about 24×15 tiles, about twice the area, with bigger people (user's choice, 2026-10-07).
- Decision: Earth's voice is rugged and outdoorsy; boss benefits add +1 heart and slower, less frequent shots to the four the user named (user's choices, 2026-10-07).
- Decision: die numbers per run, Town 1 and the others 2–6 shuffled; face k has k fragments; one real boss portal (user, 2026-10-07).
- Decision: 16 entries in one lane with one owner, past the typical range, accepted with the approved change; entries 4.1 to 4.16 follow its story table (2026-10-07).
- Decision: suggested user playtests after entries 5, 9 and 13; built under the standing self-approval instruction, stopping only for real design calls (2026-10-07).
- Assumption: fragments and face repairs replace "a temple item gives a boss benefit"; temples (entry 14) are optional and the user may drop or reshape them.
- Open question: which face grants which boss benefit, seeded or fixed; settled in entry 8's plan.
- Unknown: puzzle difficulty targets; entries 10, 11 and 12 wait on the spike (entry 2).
- Unknown: the WebGL size and memory budget after the scale change; checked in entry 1 and again at release (entry 16).
