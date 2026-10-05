# Faces and Races

The world has 6 faces of N×N screens each (2×2 at Must tier, 3×3 at Should tier). The Town face is fixed as the start. The built science faces are rearranged each run, and faces not built yet are sealed. Each face ends in a harder trial puzzle, and the final boss lives in the cube's core.

| Face | Deity | Race | Signature puzzle / item | Gate rule |
|---|---|---|---|---|
| Town (hub) | none | One NPC of every race, all getting along, among ancient ruins that mix every face's style | Talk-order draft (`town-draft-and-friendship.md`) | n/a |
| Biology | Darwin | Finch people filling ecological niches. Beaks are their swappable heads. | Beak adaptation, random per run. Thin beak: presses distant buttons, pollinates flowers that grow bridges or vines on an adjacent screen. Thick beak: breaks pots and rocks. | Beak gates are densest on Biology, with a few elsewhere |
| Chemistry | Curie | Radioactive mushroom people who decay through life stages. Early: hard shell, smashes walls, grants defence. Late: skeletons whose fingers are skeleton keys. | Re-obtainable decay item on a half-life timer. Glowing: lights dark rooms. Unstable: blasts walls. Lead: holds pressure switches. | Same home-face rule |
| Physics | Einstein | Alien-like people | Boulders slow time near timed doors. More mass means more slowing. | Same home-face rule |
| Math | Euler | Polyhedra/shape people. Circles are fast, triangles stable, rhombi glide. | Cross every bridge exactly once (an Eulerian path). Every layout must be solvable. | Same home-face rule |
| Earth & Atmosphere | Anning + Richter (not locked) | Dinosaurs: terrestrial (ground, quakes) vs avian (sky, weather) | Hazards: wind pushes, snow causes sliding, freezing rain slows. Quake crevices must be crossed and expose fossils. The centre screen is the calm hurricane eye. Item: a weather wand that starts with one phenomenon (quake, rain, snow, tornado; random per run) and gains more through upgrades. | Same home-face rule |

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
