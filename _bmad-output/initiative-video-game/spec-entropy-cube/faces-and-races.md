# Faces and Races

The world has 6 faces of 3×3 screens each, every screen about 24×15 tiles. The Town face is fixed as the start. The science faces are rearranged each run. Each face's centre screen is a town (CAP-28) with a temple (CAP-29), each science face ends in a harder trial puzzle, and the final boss lives in the cube's core behind one real portal (CAP-11, CAP-31).

| Face | Inspiration (designer only, never said in game) | Race | Signature puzzle / item | Gate rule |
|---|---|---|---|---|
| Town (hub) | none | Folk of every race, all getting along, among ancient ruins that mix every face's style; residents speak their own race's voice, and only the greeter and the Mayor are Town's own | Talk-order draft (`town-draft-and-friendship.md`) | n/a |
| Biology | Darwin | Finch people filling ecological niches | Beak adaptation, random per run. Thin beak: presses distant buttons, pollinates flowers that grow bridges or vines on an adjacent screen. Thick beak: breaks pots and rocks. | Beak gates are densest on Biology, with a few elsewhere |
| Chemistry | Curie | Chemistry folk | Isotope stage item, one rolled per run (Glowing, Unstable or Lead), kept and used by attacking. Glowing lights dark rooms, Unstable breaks cracked walls, Lead weighs down plates. Lore: the stages transform into one another between runs. No timer, no dispenser. | Same home-face rule |
| Physics | Einstein | Alien-like people | Boulders slow time near timed doors. More mass means more slowing. | Same home-face rule |
| Math | Euler | Shape people with human-like parts: a round face, a triangular body and rectangular legs. Circles are fast, triangles stable, rhombi glide. | Island screens: water with islands, doorways on different islands, spread-out bridges. Crossing a bridge lights it mint green, unlike land; planks that never light (lookouts, unmended spans) are grey; a rule sign stands on each doorway's island and the rule is said on arrival; meeting the bridge goal (e.g. light every bridge) opens a reward roadblock. Item: a gap-crosser* that mends one broken span; gap gates guard alcoves on Math and elsewhere. Upgrades (epic-varied-runs) let it cross more gaps. | Same home-face rule |
| Earth & Atmosphere | Anning + Richter (not locked) | Dinosaurs: terrestrial (ground, quakes) vs avian (sky, weather) | Whole-screen weather puzzles. Sliding ice: the whole screen is ice; the player slides straight until blocked, into a pit (falls, back to the entry) or onto a grip tile. Wind streams: a stream pushes the player one way until they leave it. Quake crevices and freezing rain stay or fold in. The centre screen is the calm hurricane eye, which hosts the Earth town. Item: a weather wand rolling one phenomenon per run, each with its own gate: quake knocks down rock pillars, rain douses a fire wall, snow freezes water to walk across, tornado blows away a debris pile. Other phenomena's gates are optional. Upgrades (epic-varied-runs) add phenomena. | Same home-face rule |

Puzzle screens (islands, ice, wind) are generated and solver-checked, at per-generator difficulty targets (solution length, branching, red herrings) from a survey of shipped games' versions of each puzzle.

## Cultures and voices (CAP-32)
Every character has a meaningful name, chosen and unstarred. Each race's culture shows in how it speaks.

| Race | Voice |
|---|---|
| Shape people (Math) | Logical and terse, but helpful |
| Chemistry folk | Emotionally reactive |
| Finches (Biology) | Almost Shakespearean, and proper |
| Physics folk | Philosophical and abstract |
| Dinosaurs (Earth & Atmosphere) | Rugged and outdoorsy |
| Townsfolk (the greeter, the Mayor, Town captives) | A friendly mix |

The full voice guide, its tested markers and every name (pools, leaders, patrons, the greeter, the Demon) are in `epic-world-worth-exploring/voices-and-names.md`.

## Character art
One hand-made design per race, in the player character's style; leaders get a distinct variant. The swappable-part look is not the default; the parts system is kept for CAP-9 (Should). The avian dinosaur is a feathered raptor; shape people are Humpty-Dumpty eggs; Town's greeter and Mayor have their own designs. Town's floor reads white.

## Towns and leaders (CAP-28)
- Each face's centre screen is a town: buildings in its race's style, a statue, more people than other screens, and no enemies. Content that used to sit on centre screens moves off.
- At least one building per town can be entered: an interior room with people or a shop counter.
- Each town has one leader, who gives that region's quest: bring the face's fragments (CAP-29).
- Each town has a temple, sealed until its face's fragments are handed in (CAP-29).
- Each town has one errand (carry a word from one of its people to another). No two people on a face give the same quest; spare quest-givers chat instead. Every quest joins the quest log (CAP-33).
- An open conversation freezes the world (player, NPCs, enemies, timers, the arena).

## Exploration and rewards (CAP-2, CAP-30)
- Every screen edge is a wall with one centred doorway. Face-edge doorways are distinct gates that start locked, each needing a key or a piece of equipment; different edges have different locks.
- Every gate, optional gate and hidden spot hides a reward: coins (meta currency), equipment, an upgrade, a key or a fragment. Nothing lies loose on the ground.
- Every screen holds a puzzle, enemies or an object.

## Fragments and repair (CAP-29)
- Each run gives the faces die numbers: Town is 1, the other five faces get 2–6 shuffled. The face numbered k holds k numbered fragments (21 per run).
- Town's one fragment comes from a lost-and-found side quest: the Mayor names who had it last, and 2–3 townsfolk on different screens (one asking a favour first) lead to it.
- Bringing all of a face's fragments to its leader unseals the face's temple: a whole screen off the net with one doorway, holding a hard (late-tier) version of the face's own puzzle. Leaving it unsolved resets the puzzle.
- Solving the temple mends the fragments into the face's gem, which grants the face's boss benefit (fixed per face).

| Face | Temple puzzle |
|---|---|
| Town | Four tokens struck in order, fewest pips first |
| Biology | Six seeds struck in order, smallest first |
| Chemistry | Six stones struck in order, brightest first, against a timer |
| Physics | Two timed booths, each needing three boulders dragged into its field |
| Math | An island network to light |
| Earth & Atmosphere | A sliding-ice or a wind-stream screen |

| Face | Boss benefit | Effect in the arena |
|---|---|---|
| Chemistry | Fewer particles | Fewer things to mix |
| Math | Wider gap | Wider always-open gaps in the membrane between the arena's halves |
| Biology | Attraction | Particles are drawn towards the player |
| Physics | Slower restore | The demon restores order more slowly |
| Town | +1 heart | The player has one more heart |
| Earth & Atmosphere | Slower shots | The demon's order pulses are slower and less frequent |

## The core (CAP-11, CAP-31)
- Several core portals; only one, chosen by the seed, leads to the arena.
- Every other portal leads to an underground lair room where trapped people give hints about the Demon and about the repair benefits. The map shows the lair.
- Skill check: with the starting kit and no benefits, a skilled player wins in 60–120 s.

## Cross-face interactions (optional flavour, not required)
- The weather wand affects other faces: rain grows Darwin's vines, a quake topples Einstein's boulders, snow freezes rivers, and a tornado blows enemies off cliffs.
- Avian dinosaurs are distant cousins of Darwin's finches.

## NPC part slots (CAP-9, Should)
A variety layer on top of the per-race designs.

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
