---
title: Campy Zelda-like Roguelite - Brainstorm Intent
type: brainstorm-intent
initiative: initiative-video-game
date: 2026-10-05
source: .memlog.md
---

# Campy Zelda-like Roguelite - Intent

## Concept
A campy, lighthearted "Zelda roguelike": you play the embodiment of entropy, exploring a Rubik's-cube world whose science-themed faces reshuffle every run, on the way to defeat Maxwell's Demon, the force that imposes order.

## Goal & Constraints
- Feasible solo in **1-2 weeks**, with real complexity.
- **2D, built in Unity** (the developer wants Unity experience; learning curve is a schedule risk).
- **Audience:** like *Ocarina of Time*, pre-teens through adults. Camp stays Zelda-level (no explicit innuendo).
- Puzzle and adventure focus over combat; combat is puzzle-based (the right Zelda-style item makes specific enemies easier).
- Roguelike/metroidvania hybrid, kept small. Each run should flow through the map very differently.
- The whole map can be explored from run 1, but the first run is very hard (tougher enemies than the starting arsenal, NPCs share little, some gates are out of reach).

## Theme & Tone
- **Theme: entropy rules the world.** Arc runs from order to chaos: the world starts segregated like a solved cube and grows more chaotic as you progress. The lore itself is entropy.
- Player = entropy. Town = entropy's home (starts highly entropic). Curie's decay = entropy as a mechanic. Boss = order.
- **Story:** long ago every race lived mixed together. **The Partition** (Maxwell's Demon's doing) split them onto separate faces. The town, built in ancient ruins that mix every face's style, is the last unpartitioned place, and the player fights to undo the Partition. Entropy is the *positive* force here: mixing means living together. Never say "sort" in-game; show the demon's order through abstract imagery.
- **Tone:** very campy and lighthearted. Lego-like NPCs, mismatched-part comedy, grudge dialogue (e.g. "Oh, NOW you have time for me?").

## Core Loop
1. Start on the fixed **Town face**, which has one NPC of each race.
2. (Should) Talk order drafts the run: the first race you talk to becomes your ally (better advice, useful items like healing); the last turns hostile for the run. The ally pick also works as a difficulty selector.
3. Leaving town rolls the shuffle of the 5 science faces. Town talk order is the only influence on the shuffle; there are no other levers.
4. Explore 3x3 screen faces (one screen per grid square, like original Zelda). Collect each face's signature item, which opens gates mostly on its home face and a little elsewhere.
5. Reach Maxwell's Demon in the cube's core, reachable from any face. It is open from the start: very hard but beatable (a speedrun/skill route).
6. Die or win, spend meta currency on stat upgrades, and run again.

## World Structure
6 faces x 3x3 = **54 screens**. The Town face is a hub, not a science face.

| Face | Deity | Race | Signature puzzle / item |
|---|---|---|---|
| Town (hub, fixed start) | none | Busy and mismatched: one NPC of each race, all getting along, among ancient mixed-style ruins | Talk-order ally/enemy draft |
| Biology | Darwin | Finch-like (plant-like) bird people filling ecological niches | Swappable beaks as tools: a thin, long beak presses distant buttons and pollinates flowers (which grow into bridges or vines); a thick beak breaks pots and obstacles. You get a different adaptation each run |
| Chemistry | Curie | Radioactive mushroom-like people who decay through life stages (hard-shelled early, which smashes walls and grants defence; skeletons late, whose fingers are skeleton keys) | Radioactive decay item, re-obtainable, with a half-life timer: the glowing isotope lights dark rooms, the unstable stage blasts walls, the lead stage holds down switches |
| Physics | Einstein | Alien-like people | Time dilation: drag boulders near a timed door to keep it open; more mass slows time further |
| Math | Euler | Polyhedra/shape people with shape traits: circles are fast, triangles are stable, rhombi glide (the demon sorts them by shape) | Seven Bridges of Konigsberg: Eulerian-path puzzles (cross every bridge exactly once) |
| Earth & Atmosphere | Mary Anning + Charles Richter (favored) | Dinosaurs, as rivals: terrestrial (ground/quakes) vs avian (sky/weather). Avian dinos echo Darwin's finches | Weather + earthquakes. Storm hazards: wind pushes, snow makes you slide, freezing rain slows you. Quake crevices must be crossed and expose Anning's fossils. The center screen is the calm hurricane eye. **Item: weather wand**, which controls one phenomenon at first (quake, rain, snow, tornado) and gains more as it upgrades |

## Town Draft & Friendship
**Per run (SHOULD):** the first race you talk to is your **ally**, the middle ones are neutral, and the last is your **enemy** (that whole race attacks you). **Every race you skip is also an enemy.**
**Ally bonuses:** that face's item/ability gets an upgrade or extra ability for the run, its NPCs give more helpful information, and some otherwise inaccessible areas unlock.

**Persistent friendship score (COULD):**
| Source | Friendship |
|---|---|
| Talked to first / middle / last | +2 / +1 / +0 |
| Solve a race's problem while it's your enemy | +4 |
| Solve any other race's problem | +1 |

- **Threshold 1:** the race no longer turns hostile when talked to last, and solving its problems gives +1 instead of +4.
- **Late-game unlock:** you can deliberately skip a race that is past threshold 1, making it an enemy again so you can earn the +4 bonus.
- **Threshold 2:** the race is always a friend and gives ally bonuses every run, whatever the talk order.
- Design intent: helping your enemy is the fastest way to win them over, and over many runs the world un-partitions.

## MoSCoW Scope
Later corrections in the log take precedence.

**MUST**
- 54 screens (6 faces x 3x3), Town face as the fixed start; the 5 science faces shuffle on leaving town.
- One signature puzzle and item per face (see table); item gates.
- **A final boss.** Minimum: a single-phase Maxwell's Demon sorting one thing (e.g. hot/cold), which the player beats by mixing the arena into chaos faster than the demon can sort it. A simpler boss is an acceptable fallback.
- Boss in the cube's core, open from the start but hard.
- Meta currency buys stat upgrades: health, defence, power, speed.
- Lego-swappable NPCs (head = knowledge/hints, torso = role, legs = movement) with campy dialogue.

**SHOULD**
- Maxwell's Demon specifically, with extra sorting phases built from the run's faces (Curie people by decay state, finches by beaks/feet/traits). The sorted pairs don't need to be literally hot/cold.
- Town talk-order ally/enemy draft.
- The demon must be beaten multiple times to truly win (Hades-style).
- A knob that scales puzzle difficulty with run conditions.
- Re-collected abilities come back in improved versions.

**COULD**
- Persistent gear (one more kept item over time), upgrade branches, ammo capacity.
- The player starts colourless and gains colours from collected abilities.
- Rival race pairs (befriending one locks the rival's region).
- Chaos-scaled dialogue shown as scrambled word tiles to decode.
- Mid-run cube twists, a rotating storm around the hurricane eye, deaths steering the next shuffle.
- Gags: a Newton cameo, skeleton keys.

**WON'T (this time)**
- Freud (psychology) and Turing (CS) faces, and the dream layer.
- 2 missing themes per run.
- The janitor premise.

## Key Design Principles (synthesis)
1. **One modular swap engine, built first.** Screens, NPC parts, beaks and decay stages are all interchangeable modules shuffled by a seed.
2. **"Commitment closes doors" is a single mechanic.** The town draft, one-way gates (e.g. you can lower down a cliff but not climb back) and rival races all reuse it: build it once.
3. **Home-face item rule.** Each face's ability works mostly on its home face and a little everywhere else.
4. **Upgrades change how you traverse, not just raw power.**
5. **Improved re-collected abilities overlap with persistent gear.** Ship the improved abilities first if time is short.
6. **Keep the theme coherent end to end** (entropy vs order). Use it to decide what to cut.

## Risks & Fallbacks
- Learning Unity during a 2-week build: stick to built-in 2D tools (Tilemap, 2D physics) and keep the scope fallbacks ready.
- The Must list alone fills about 2 weeks solo. **If behind by day 5, cut screen count first** (2x2 faces = 24 screens), not puzzle types.
- Boss fallback: a single-phase demon or a simpler boss is acceptable.

## Open Questions
- Earth face deity: Anning + Richter are favored and shared, but not formally locked (deferred).
- Friendship threshold values (deferred; tune by playtesting).
- Henrietta Lacks: the deity concern is likely moot because Darwin holds biology. Flag it if she is reconsidered.
