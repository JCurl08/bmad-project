# To-do for you: playtest of the full cube (updated 2026-10-06)

## Where things stand
Every Epic 1 story except the release is built, reviewed and committed locally. Nothing has been pushed.

| Story | Commit |
|---|---|
| 1.1–1.12 (first slice: Town, Biology, Chemistry, Physics, boss, run loop) | `c8fc729` … `afea9cc` |
| 1.13 Refactor sweep | `882d539` |
| 1.15 Readability pass | `f6fc12c` |
| 1.16 Dev keys F8/F9/F10 | `e051208` |
| 1.17 Full cube at 3x3 | `a842e45` |
| 1.18 Isotope as a stage item | `b11b4e2` |
| 1.19 Math face | `d74788f` |
| 1.20 Earth and Atmosphere face | `3f22c7f` |
| 1.21 Cube-net map | `8be3e09` |
| 1.22 Look and tone pass | `a414268` |
| 1.23 Refactor sweep (full cube) | `bf1af0b` |
| **1.14 First itch.io release** | **needs you (section 4)** |

All automated checks pass: EditMode, PlayMode (run in class batches), and the 50-seed sweep with no softlocks.

Names ending in `*` are placeholders for the naming brainstorm you wanted to do later.

## 1. Play it (Unity: open `game/`, scene `Assets/Scenes/Cube.unity`, press Play)

**Controls**
- WASD/arrows: move
- Attack: mouse or Enter
- E: interact
- Previous/Next: cycle items
- **M or Tab: map (pauses the game)**

**Dev keys**
- F1 overlay (off by default)
- F2 slots
- F3 NPCs
- F4 hostility
- F5 reroll seed
- F6 jump to next screen
- F7 test enemy
- **F8 leave the boss arena**
- **F9 rest (full health)**
- **F10 new run**
- **F11 reveal the whole map**

While the map is open, only F1 works.

**What to check: your feedback from the last playtest**
- **Rubik's colours:** Town white, Greenwood* (Biology) green, Ember Vale* (Chemistry) red, Weightlands* (Physics) blue, Bridgelands* (Math) yellow, Wildlands* (Earth) orange. Is it colourful enough? Two concerns to judge by eye:
  - Are red items and gates easy to see on the red floor?
  - Math's floor reads gold rather than lemon yellow (no paler tile exists in the pack).
- **Full cube:** 6 faces of 3×3 screens. Does it feel populated and not too big?
- **Map (M):** the cube unfolds as a cross; visited screens are lit, and you, gates, items, trials and portals are marked. Does walking off a face match what the map shows?
- **Stones (the old isotope):** it is now a real item, in three variants: Glowstone*, Wildstone* and Leadstone*. Each run gives you one; the lore says they change into each other between visits. There is no timer or dispenser. Is it clear now?
- **People:** shape people have round faces, triangle bodies and rectangle legs. The finches were redrawn. Better?
- **Bridgelands\* (Math):**
  - the Gap-Crosser* lays a plank over gap gates;
  - bridge doors are a walk-every-bridge-once puzzle.
- **Wildlands\* (Earth):**
  - weather hazards: wind, snow, freezing rain and crevices;
  - the Stormcaller* wand comes in quake, rain, snow and tornado variants. Quake knocks down rock pillars, rain douses a fire wall, snow freezes water, and tornado blows away debris;
  - the dinosaur NPCs.
- **Subtle science:** no character should name a scientist or a science. Shout if anything sounds too on-the-nose.
- **Boss arena:** a doorway on the west wall lets you walk out mid-fight (walk straight west into it). The boss aim is 60–120 s for a steady player.

## 2. One decision for you
- **The word "entropy" in game text.** It is currently banned (as too on-the-nose), so the meter reads "Order ↔ Mixing". Keep it banned, or allow it?

## 3. Push to GitHub
I never push.
```
git -C C:/Projects/bmad-project push
```

## 4. Story 1.14: first itch.io release
1. Close heavy apps. The WebGL build needs a lot of memory, and it was stopped by low memory here.
2. In Unity, run **Tracer → Build WebGL** (about 15 minutes). The output goes to `game/Builds/WebGL`.
3. Zip the **contents** of that folder (index.html, Build/, TemplateData/).
4. On itch.io: create a new project. Set Kind = HTML and visibility to Restricted or Draft. Upload the zip, tick "played in the browser", and set the viewport to 960×600.
5. In a fresh browser window:
   - play a full run;
   - buy an upgrade;
   - reload, and check the upgrade is still there.
6. Tell me "finish 1.14".

## 5. Later
- The naming brainstorm for every `*` name.
- Open items are listed in `deferred-work.md`.
- Each story's decisions and review history are in `epic-playable-cube/story-*-plan.md` (Implementation Notes and Review Triage Log).

## Small things to know
- Keep Unity closed while I run tests: batch mode can't open the project while the editor has it.
- Run PlayMode tests with `-nographics`, in class batches (the laptop runs low on memory otherwise).
- `UV_SYSTEM_CERTS=true` and `UV_PYTHON=3.14` are set for your account so BMad works behind the corporate proxy.
