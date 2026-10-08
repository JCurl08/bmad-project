# To-do for you: playtest of Epic 4 so far (2026-10-08)

## Where things stand
Built overnight, each with a review, fixes and full verification, then committed locally. Nothing has been pushed.

| Story | Commit |
|---|---|
| 1.26 Island screen fixes | `f0907a9` |
| 4.1 Bigger screens, thinner walls | `a73e6aa` |
| 4.2 Puzzle difficulty research | `9babed8` (`epic-world-worth-exploring/puzzle-difficulty-research.md`) |
| 4.3 Character redesign | `318a4da` |
| 4.4 Race cultures and names | `90a8925` (`epic-world-worth-exploring/voices-and-names.md`) |
| 4.5 Towns on every face | `b3510ec` |
| 4.6 Enterable buildings | `1737e8a` |
| 4.7 Roadblock rewards | `36066c2` |
| 4.8 Fragments and face repair | `e6a33b8` |
| 4.9 Locked face-edge gates | `34f1fff` |
| 4.10 Sliding ice screens | `0f00d6b` |
| 4.11 Wind stream screens | `9a04552` |
| 4.12 Bridge puzzles, tuned | `4fea984` |
| 4.13 Boss lair | `3f3a938` |
| Not built yet | 4.14 Temples (optional), 4.15 Refactor sweep, 4.16 itch.io release 2, and 1.14 (the first release) |

Every story passed EditMode, all PlayMode batches and the 50-seed sweep. The sweep confirms every run can be finished and no order of key spending can lock you out. The boss check with no benefits still takes 78.6 / 90.5 / 105.5 s.

## How to play
Open `game/` in Unity, open `Assets/Scenes/Cube.unity`, and press Play.

**Controls**
- WASD to move
- Enter or mouse to attack
- E to interact or talk
- M or Tab for the map

**Dev keys**
- F1 overlay
- F3 NPCs; **Shift+F3** spawns one leader of each race
- F5 reroll the seed
- F6 jump to the next screen
- F7 enemy
- F8 leave the arena, a room or the lair
- F9 rest; **Shift+F9** gives every fragment of the face you're on
- F10 new run
- F11 reveal the map; **Shift+F11** unlocks every face-edge arch

## What to check
1. **Scale.** Screens are 24×15 tiles, walls are thinner, and people are drawn a third larger. Do people read big enough now?
2. **Characters (4.3).** One design per race, in your character's style. Leaders wear a gold crown and purple. Two things might be off:
   - the avian dinosaur may read like a penguin;
   - the shape person's triangle body is small.
3. **Voices and names (4.4).** Each race talks differently:
   - finches are Shakespearean;
   - Chemistry folk are emotional;
   - Physics folk muse;
   - Math folk are terse;
   - dinosaurs are rugged.

   **All names are my picks, so change any you like:**

   | Who | Name |
   |---|---|
   | Town leader | Mayor Muddleton |
   | Finch leader | Queen Wrennifer |
   | Chemistry leader | Duchess Flarella |
   | Physics leader | Grand Muser Yondrix |
   | Math leader | Chancellor Quadrilla |
   | Dinosaur leader | Chief Rockjaw |
   | Greeter | Hattie Hullo |
   | Boss | Primsworth the Demon |

   Region and item names still carry `*` until the naming brainstorm.
4. **Towns (4.5).** The centre screen of every face is a town: buildings in its race's style, a statue, a leader, residents and no enemies.
   - The statue sits off-centre, to keep the doorways clear.
   - The player draws over roofs when walking behind them.
5. **Buildings (4.6).** Walk north into the door that has a rug and a lantern.
   - Town and Chemistry have a shop, Biology and Physics a home, and Math and Earth the leader's hall.
   - Is entering too strict? You need to walk straight north, within 0.75 of the door's centre.
6. **Rewards (4.7).** Every gate hides a chest, and dirt mounds take two swings to dig. Rewards are:
   - Jumbles*
   - items
   - keys
   - boosts (+1 heart, speed or power, for the rest of the run)
   - fragments

   Does the dirt-mound tile read as "dig here"?
7. **Fragments and repair (4.8).**
   - Each face rolls a die number. Town is always 1; the others get 2–6.
   - The face numbered k hides k fragments.
   - Bring them all to that face's leader and the face is repaired: the statue turns gold.
   - Talk to the Mayor twice for Town's fragment.

   **Boss benefits (my mapping):**

   | Face repaired | Benefit in the boss fight |
   |---|---|
   | Town | +1 heart |
   | Biology | Particles drift toward you |
   | Chemistry | Fewer particles |
   | Physics | Demon restores more slowly |
   | Math | Wider gap |
   | Earth | Slower, rarer shots |

   Check the HUD dice row and the map tags in play; batch screenshots can't capture them.
8. **Locked face-edge gates (4.9).** Every edge between faces needs either a key or a face item.
   - You start with 1 key, the way out of Town.
   - Each face hides 2 more keys.
   - Chest keys are a separate pool and open the bonus alcoves behind optional gates, which hold 10 Jumbles* each.

   Does the key economy feel right?
9. **Ice screens (4.10).** Earth has 2 per run. Slide until you hit something; a pit sends you back to where you came in. Check the slide speed and how fair early screens feel.
10. **Wind screens (4.11).** Earth has 1 per run. Gusts carry you over the sky to islands and grip rocks.
    - Do the streaks and chevrons read clearly?
    - Is it confusing that stepping into the tail of a stream does nothing?
11. **Bridge puzzles (4.12).** Each crossing lights a plank. Re-crossing a lit plank turns them all off. Lighting every plank opens the chest.
    - The doorway that led nowhere is gone.
    - One doorway reaches the puzzle only over a broken span, which you fix with your Gap-Crosser*.
    - Do early screens make a fair tutorial?
12. **Boss lair (4.13).** Only one core portal leads to Primsworth. The others take you down into a lair room with 4 caged prisoners, who give true hints:
    - fight tips;
    - which repaired face grants which boss benefit;
    - the region of the real portal (every lair room says it).

    Once you know the real portal, the map shows it in gold. Walk south, or press F8, to leave a lair room. Are the prisoners readable?

## Not built, possible next steps
- **4.14 Temples (optional).** Each face's signature item would sit in a temple. Keep it, or drop it?
- **4.15 Refactor sweep.** Includes a 500-seed check that every run can be finished, since real runs use random seeds.
- **4.16 / 1.14 itch.io releases.** These need you: run Tracer → Build WebGL in Unity, upload the build, and play it in a browser.
- **Epic 2: every run plays differently.** Not started.
- **Push** when you're happy with the work: `git -C C:/Projects/bmad-project push`

Every design call is marked *Assumption* in the Implementation Notes of each `epic-world-worth-exploring/story-*-plan.md`, and its review fixes are in that plan's Review Triage Log.
