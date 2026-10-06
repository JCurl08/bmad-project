# Faces and Races

The world has 6 faces of 3×3 screens each. The Town face is fixed as the start. The built science faces are rearranged each run, and faces not built yet are sealed. Each face ends in a harder trial puzzle, and the final boss lives in the cube's core.

| Face | Inspiration (designer only, never said in game) | Race | Signature puzzle / item | Gate rule |
|---|---|---|---|---|
| Town (hub) | none | One NPC of every race, all getting along, among ancient ruins that mix every face's style | Talk-order draft (`town-draft-and-friendship.md`) | n/a |
| Biology | Darwin | Finch people filling ecological niches. Beaks are their swappable heads. | Beak adaptation, random per run. Thin beak: presses distant buttons, pollinates flowers that grow bridges or vines on an adjacent screen. Thick beak: breaks pots and rocks. | Beak gates are densest on Biology, with a few elsewhere |
| Chemistry | Curie | | Isotope stage item, one rolled per run (Glowing, Unstable or Lead), kept and used by attacking. Glowing lights dark rooms, Unstable breaks cracked walls, Lead weighs down plates. Lore: the stages transform into one another between runs. No timer, no dispenser. | Same home-face rule |
| Physics | Einstein | Alien-like people | Boulders slow time near timed doors. More mass means more slowing. | Same home-face rule |
| Math | Euler | Shape people with human-like parts: a round face, a triangular body and rectangular legs. Circles are fast, triangles stable, rhombi glide. | Cross every bridge exactly once (an Eulerian path); every layout must be solvable. Item: a gap-crosser* that lays a crossing over one gap; gap gates guard alcoves on Math and elsewhere. Upgrades (epic-varied-runs) let it cross more gaps. | Same home-face rule |
| Earth & Atmosphere | Anning + Richter (not locked) | Dinosaurs: terrestrial (ground, quakes) vs avian (sky, weather) | Hazards: wind pushes, snow causes sliding, freezing rain slows. Quake crevices must be crossed and expose fossils. The centre screen is the calm hurricane eye. Item: a weather wand rolling one phenomenon per run, each with its own gate: quake knocks down rock pillars, rain douses a fire wall, snow freezes water to walk across, tornado blows away a debris pile. Other phenomena's gates are optional. Upgrades (epic-varied-runs) add phenomena. | Same home-face rule |

## Cross-face interactions (optional flavour, not required)
- The weather wand affects other faces: rain grows Darwin's vines, a quake topples Einstein's boulders, snow freezes rivers, and a tornado blows enemies off cliffs.
- Avian dinosaurs are distant cousins of Darwin's finches.

## NPC part slots (CAP-9)
| Slot | Controls |
|---|---|
| Head | Knowledge: which hint the NPC gives |
| Torso | Role: merchant, quest-giver, smith, and so on |
| Legs | Movement: stands still, wanders or flees |

Mismatched parts are a source of comedy. For example, a sage's head on a goblin's body gives confidently wrong advice.

## Demon phases (CAP-11 / CAP-15)
- **Must tier:** a single phase. The arena starts ordered (e.g. hot on one side, cold on the other) and the player wins by mixing it faster than the demon can restore order.
- **Should tier:** extra phases drawn from the races: Curie people by decay stage, finches by beak or feet, shape people by shape. The demon's ordering is always shown through imagery and never called "sorting" in-game.

## Region colours (Rubik's cube)
| Face | Colour |
|---|---|
| Town | White |
| Biology | Green |
| Chemistry | Red |
| Physics | Blue |
| Math | Yellow |
| Earth & Atmosphere | Orange |
