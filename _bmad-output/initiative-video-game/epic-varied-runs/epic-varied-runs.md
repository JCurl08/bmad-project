---
type: epic
title: "Every run plays differently"
parent: initiative-video-game
covers: [CAP-7, CAP-8, CAP-14, CAP-15, CAP-16, CAP-17]
after: [epic-playable-cube]
assignee: ""
risk: medium
---

# Every run plays differently

## Description

Adds the Should tier on top of the playable run. First it completes the cube: it unseals the Math and Earth & Atmosphere faces, each with its trial puzzle, and expands every face from 2×2 to 3×3. Then talk order in town drafts allies and enemies, the demon gains race-based phases and needs several wins for the true ending, puzzles scale in difficulty, and re-collected abilities come back stronger. Each item uses a deliberately simple design (Requirements). This epic also owns the final polish day and the final itch.io publish.

## Outcome

Two runs of the same player feel different because of their own choices and their history. The signal is the spec's success signal, plus visible differences from the draft and ability levels between runs.

## Requirements

The spec is the requirement source: CAP-7, CAP-8 and CAP-14 to CAP-17, plus the 3×3 face size in CAP-1. Simple designs, proposed and awaiting the user's confirmation:
- CAP-14 draft: town records talk order. The ally's face item gains +1 level for the run and its NPCs use a "helpful" hint set. One ally-only gate per face opens. The last race and skipped races set the hostility flag, so their NPCs fight using enemy behaviour.
- CAP-15 demon: up to 2 extra phases, each drawn from a race present in the run and reusing the arena. The true ending comes after 3 wins, counted in the save.
- CAP-16 difficulty: one integer (1–3) from the number of completed runs feeds each puzzle generator's difficulty parameter.
- CAP-17 abilities: each ability stores a level in the save. Re-collecting it gives level +1, up to 3. Level 2 or 3 of the weather wand adds a phenomenon.

## Done when

1. All 5 science faces are playable at 3×3, each with its trial, and the seed sweep finds no softlocks.
2. Talking to races in a different order changes ally bonuses and hostile races in the next run.
3. The demon shows race-based phases that differ between seeds, and the true ending appears only after the required wins.
4. The same puzzle type appears at 2 or more difficulty levels across runs.
5. A re-collected ability is measurably stronger than in the previous run.
6. The final WebGL build is published on itch.io, with a Windows build as the fallback, after a polish pass and a final seed sweep.

## Boundaries

The Should tier only, plus the final release step. Anything not finished by the feature freeze is cut in the order in `scope-tiers.md`.

## References

- spec — _bmad-output/initiative-video-game/spec-entropy-cube/spec-entropy-cube.md, Capabilities CAP-7, CAP-8, CAP-14 to CAP-17
- design — _bmad-output/initiative-video-game/spec-entropy-cube/faces-and-races.md, rows Math and Earth & Atmosphere
- design — _bmad-output/initiative-video-game/spec-entropy-cube/town-draft-and-friendship.md, section Per-run draft
- design — _bmad-output/initiative-video-game/spec-entropy-cube/faces-and-races.md, section Demon phases
- constraint — _bmad-output/initiative-video-game/spec-entropy-cube/scope-tiers.md, Cut order
- plan — _bmad-output/initiative-video-game/brainstorm-campy-zelda-roguelite/build-plan-2-weeks.md, Days 12–14

## Notes

- Waits on epic-playable-cube because: it extends that epic's hooks (hostility flag, roles, boss phase slot, puzzle difficulty parameter, ability versions, save format).
- Assumption: the simple designs above are proposals for the user to confirm at this epic's inception.
