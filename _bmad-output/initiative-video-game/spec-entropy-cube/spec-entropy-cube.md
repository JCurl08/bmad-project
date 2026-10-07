---
id: SPEC-entropy-cube
companions:
  - faces-and-races.md
  - town-draft-and-friendship.md
  - lore-and-tone.md
  - scope-tiers.md
  - stack.md
  - ../brainstorm-campy-zelda-roguelite/build-plan-2-weeks.md
sources:
  - ../brainstorm-campy-zelda-roguelite/brainstorm-campy-zelda-roguelite.md
---

> **Canonical contract.** This SPEC and the files in `companions:` are the complete, preservation-validated contract for what to build, test, and validate. Source documents listed in frontmatter are for traceability — consult them only if you need narrative rationale or prose color this contract intentionally omits.

# Entropy Cube

## Why

A vision to realize, plus a learning goal. The developer wants a campy, lighthearted Zelda-style roguelite, focused on puzzles and adventure, that one person can finish in 1–2 weeks, and wants to gain Unity experience while building it. The player is entropy itself, exploring a Rubik's-cube world of towns, cultures and rewards, whose science-themed faces are rearranged every run, to undo "the Partition" imposed by Maxwell's Demon. Every trade-off resolves toward three things: shipping a complete, replayable game inside the timebox, making each run play differently, and keeping the entropy-is-good theme coherent.

## Capabilities

Tier tags follow `scope-tiers.md`. Face, race, town and benefit detail is in `faces-and-races.md`.

- **CAP-1** [Must]
  - **intent:** Each run builds a seeded cube world of 6 faces × 3×3 screens. The Town face is always the start. The science faces and their screens are rearranged when the player leaves town.
  - **success:** The same seed reproduces an identical layout. Two different seeds produce different face and screen arrangements. The run always starts on the Town face.
- **CAP-2** [Must]
  - **intent:** The player explores screen to screen, as in classic Zelda. Every screen edge is a wall with one centred doorway. Face-edge doorways are distinct gates that start locked, each needing a key or a piece of equipment, with different locks on different edges, so there are several ways into the world. The core is reachable from every face.
  - **success:** On seeds 1–50 the sweep proves every screen is reachable from Town using obtainable keys and equipment, and more than one locked edge can be the first one opened.
- **CAP-3** [Must]
  - **intent:** Each science face holds one signature item. That item opens matching gates, mostly on its home face and in a few places elsewhere.
  - **success:** Each item opens at least one gate off its home face. No seed places a required gate where its item cannot be obtained first (seed sweep finds no softlocks).
- **CAP-4** [Must]
  - **intent:** On the Biology face (Darwin), the player gains a beak adaptation that is random per run. A thin beak presses distant buttons and pollinates flowers, which grow bridges or vines. A thick beak breaks pots and rocks.
  - **success:** In a playtest, each beak type opens its gate types, and a pollinated flower creates a crossing on an adjacent screen.
- **CAP-5** [Must]
  - **intent:** On the Chemistry face, each run rolls one stage item (Glowing, Unstable or Lead) that the player keeps for the run and uses by attacking: glowing lights dark rooms, unstable breaks cracked walls, lead weighs down plates. In-world lore says the stages transform into one another between runs.
  - **success:** Each stage opens its gate type. Gates for the other stages are optional and guard nothing required. There is no timer and no dispenser.
- **CAP-6** [Must]
  - **intent:** On the Physics face (Einstein), boulders moved near a timed door slow time, so the door stays open longer. More mass slows time further.
  - **success:** A door that can't be passed with no boulders becomes passable with the required number of boulders next to it.
- **CAP-7** [Must]
  - **intent:** The Math face (Euler) has island screens: water with islands, doorways on different islands, and spread-out bridges. Crossing a bridge lights it, the gap-crosser mends broken spans, and meeting the bridge goal (e.g. lighting every bridge) opens a reward roadblock.
  - **success:** Every generated island screen is solver-checked as solvable from each connected doorway, its roadblock opens only when the goal is met, and its difficulty is within the puzzle research's targets.
- **CAP-8** [Must]
  - **intent:** The Earth & Atmosphere face has whole-screen weather puzzles. On sliding-ice screens the player slides straight until blocked, into a pit (back to the entry) or onto a grip tile. On wind-stream screens streams push the player one way until they leave the stream. Quake crevices and freezing rain stay or fold in. The centre is the calm hurricane eye. Its item is a weather wand that controls one phenomenon at first and gains more with upgrades.
  - **success:** Every generated ice and wind screen is solver-checked as solvable within the puzzle research's difficulty targets, and the wand's phenomenon solves at least one gate on the face.
- **CAP-10** [Must]
  - **intent:** Combat is a puzzle: specific items make specific enemies easier to beat.
  - **success:** Every enemy type has at least one item that makes it markedly easier to beat.
- **CAP-11** [Must]
  - **intent:** A final boss in the cube's core is open from run 1: very hard but beatable. It is a single-phase Maxwell's Demon, beaten by mixing an ordered arena into chaos faster than the demon can restore order. Only one seeded core portal leads to it (the others lead to the lair, CAP-31), and benefits from repaired faces (CAP-29) apply to the fight.
  - **success:** A skilled player beats the boss with the starting kit and no benefits within the 60–120 s skill check. Each benefit measurably changes its fight parameter. The boss's win and loss states end the run correctly.
- **CAP-12** [Must]
  - **intent:** A run ends on death or victory. Abilities then reset and the next run gets a new seed. The whole map can be explored on the first run, but it is very hard: enemies outmatch the starting kit, NPCs share little, and some gates stay out of reach until later runs.
  - **success:** After a run ends, the next run starts in town with reset abilities and a new layout, and nothing on the map is permanently blocked for a first-run player.
- **CAP-13** [Must]
  - **intent:** During a run, the player finds meta currency from the core boss, face trial puzzles (CAP-25) and roadblock rewards (coins, CAP-30), and spends it after the run on persistent stat upgrades: health, defence, power and speed.
  - **success:** Upgrades bought in one session are still applied after quitting and relaunching.
- **CAP-25** [Must]
  - **intent:** Each science face ends in a harder trial puzzle that combines its mechanic, and solving it pays meta currency. This replaces minibosses; the core final boss stays a boss.
  - **success:** Each built face's trial can be solved with that face's item and pays meta currency that is still there after the run ends.
- **CAP-26** [Must]
  - **intent:** A pause map shows the cube as an unfolded net of 3×3 faces, with visited screens in their region colour, the player, Town, discovered gates, items and trials, and the core portals.
  - **success:** For any seed, the map matches the run's real layout on every visited screen and updates after the shuffle.
- **CAP-27** [Must]
  - **intent:** The player can leave the core arena on foot while the fight is not won.
  - **success:** Walking out of the arena exit returns the player to the face they entered from, and the fight resets.
- **CAP-28** [Must]
  - **intent:** Each face's centre screen is a town: buildings in its race's style, a statue, at least one enterable building (an interior with people or a shop counter), more people than other screens, no enemies, and one leader who gives that region's quest.
  - **success:** On every seed each face's centre is a town with all of these, and entering then leaving a building returns the player to its door.
- **CAP-29** [Must]
  - **intent:** Each run gives the faces die numbers: Town 1, the others 2–6 shuffled. The face numbered k holds k numbered fragments. Bringing all of a face's fragments to its leader repairs the face and grants one boss benefit.
  - **success:** On seeds 1–50 every fragment is reachable, Town is always 1 and the other numbers vary between seeds, the HUD and map show fragments, and a repaired face's benefit applies in the arena.
- **CAP-30** [Must]
  - **intent:** Every gate, optional gate and hidden spot hides a reward: coins, equipment, an upgrade, a key or a fragment. Items no longer lie loose on the ground. Every screen holds a puzzle, enemies or an object.
  - **success:** The sweep on seeds 1–50 finds no reward outside a roadblock and no empty screen.
- **CAP-31** [Must]
  - **intent:** Every core portal except the real one leads to an underground lair room, where trapped people give hints about the Demon and the repair benefits. The map shows the lair.
  - **success:** On every seed exactly one portal reaches the arena, every other portal reaches a lair room with a hint-giver, and the player can walk back out.
- **CAP-32** [Must]
  - **intent:** Each race has its own culture, heard in its manner of speech (voices in `faces-and-races.md`), and every character has a meaningful name.
  - **success:** Every NPC line and hint is written in its race's voice, no character name carries a placeholder `*`, and the subtle-science text scan still passes.
- **CAP-9** [Should]
  - **intent:** Each race's NPCs are assembled from swappable parts: the head carries knowledge and hints, the torso the role, and the legs the movement. They speak campy dialogue. The parts are a variety layer on top of the per-race designs.
  - **success:** NPC parts vary between seeds, and each part slot visibly affects what that NPC does or says.
- **CAP-14** [Should]
  - **intent:** Talk order in town drafts the run. The first race the player talks to becomes an ally, and the last race and any skipped races become hostile for the run.
  - **success:** Ally bonuses apply (item upgrade or extra ability, better hints, unlocked areas, healing items), and every member of a hostile race attacks on sight.
- **CAP-15** [Should]
  - **intent:** The demon gains phases drawn from the faces' races (decay stages, beak types, shapes) and must be beaten multiple times to truly win.
  - **success:** Phases differ between seeds, and the true ending appears only after the required number of victories.
- **CAP-16** [Should]
  - **intent:** Puzzle difficulty scales with run conditions, for example more boulders or more bridges.
  - **success:** The same puzzle type appears at 2 or more difficulty levels across runs.
- **CAP-17** [Should]
  - **intent:** A re-collected ability returns as an improved version.
  - **success:** Collecting an ability in a later run gives a measurably stronger or expanded version than the previous run's.
- **CAP-24** [Could, cut for now]
  - **intent:** Enemies mainly drop a run-only currency that the player spends during the same run and loses when the run ends.
  - **success:** Run currency can be spent mid-run, resets to zero at the start of the next run, and never converts into meta currency.
- **CAP-18** [Could]
  - **intent:** Each race has a persistent friendship score with thresholds that remove hostility and later make the race a permanent ally. Rules are in `town-draft-and-friendship.md`.
  - **success:** Scores persist across sessions, and crossing each threshold changes town-draft outcomes as specified.
- **CAP-19** [Could]
  - **intent:** The player can keep gear between runs, with upgrade branches and ammo capacity upgrades.
  - **success:** Kept gear is present at the start of the next run with its chosen upgrades.
- **CAP-20** [Could]
  - **intent:** The player character starts colourless and gains a colour for each collected ability.
  - **success:** The character's appearance changes visibly with each ability collected.
- **CAP-21** [Could]
  - **intent:** Races come in rival pairs (e.g. terrestrial vs avian dinosaurs). Befriending one locks the rival's region for the run.
  - **success:** Allying with one race of a pair makes its rival's region inaccessible for that run.
- **CAP-22** [Could]
  - **intent:** In high-chaos seeds, hint dialogue appears as scrambled word tiles that the player rearranges to decode.
  - **success:** Rearranging the tiles correctly reveals the hint.
- **CAP-23** [Could]
  - **intent:** The cube twists during a run, and storm bands rotate around the hurricane eye.
  - **success:** The layout or weather bands change during a run without creating softlocks.

## Constraints

- One developer, 14 days at most (limit 2026-10-19). epic-playable-cube ships first as a playable baseline; epic-world-worth-exploring targets about 2026-10-11, with user playtests between batches. Should work follows.
- 2D in Unity, built with Unity's built-in 2D tools. The developer is learning Unity, so simple implementations win.
- Release target is **WebGL on itch.io**, with a Windows desktop build as the fallback. Saves must work in browser storage, audio may only start after the player's first input, and the build should stay small.
- The Must tier ships all 6 faces at 3×3 screens (54 screens), with all 5 science faces built.
- Screens are about 24×15 tiles (about twice the old 16×10 area) on the 960×600 canvas, with half-tile edge walls and people drawn larger. Modules, layouts, plans, the sweep and tests all use this size.
- Character art is one hand-made design per race in the player character's style; leaders are a distinct variant. The swappable-part look is not the default.
- Puzzle screens (islands, sliding ice, wind streams) are generated with a solver check, at difficulty targets taken from a survey of shipped games. Other screens are hand-built modules.
- In-world text never names a real scientist or uses explicit science vocabulary for the face themes. The analogy lives in mechanics and visuals only.
- Character names are chosen and carry no `*`. Region and item names keep a trailing `*` until a naming brainstorm replaces them.
- Region colours follow a Rubik's cube: Town white, Biology green, Chemistry red, Physics blue, Math yellow, Earth & Atmosphere orange.
- Audience is pre-teens to adults, like *Ocarina of Time*. Camp humour only, no explicit innuendo (`lore-and-tone.md`).
- The game never says "sort". The demon's order is shown through abstract imagery and named "the Partition".
- The order the player talks to races in town is the only player influence on the shuffle.
- Puzzle and adventure come first. Combat is never a pure skill gate.

## Non-goals

- Psychology (Freud) and Computer Science (Turing) faces, and the dream layer.
- Leaving built science faces out of a run. Every built face appears in every run.
- Shuffle-nudging levers beyond talk order in town, including a death-steered shuffle.
- The janitor premise.
- 3D.

## Success signal

- A new player starts a fresh seed in the Town face's town, explores all 5 science faces of a 3×3 cube through locked edge gates with the help of the map, meets each face's town and leader, earns rewards from roadblocks, gathers fragments to repair faces, finds the real portal past the lair, and beats the core boss or walks out of the arena. The sweep passes on 50 of 50 seeds with no softlocks, and two different seeds visibly route the player through the faces in different orders.

## Assumptions

- Meta progression is saved locally: browser storage on WebGL, or the local disk on desktop.
- Run currency (CAP-24) is cut for now. If it returns, it is spent at merchant-role NPCs.
- An "improved" re-collected ability (CAP-17) is improved relative to its version in the previous run.
- Fragments and face repairs replace the playtest-2 idea that a temple item gives a boss benefit. Temples are optional and the user may drop or reshape them.

## Open Questions

- What is the final title? "Entropy Cube" is a working title.
- Which face grants which boss benefit: seeded per run or fixed? (decided in story 4.8's plan)
- Should Anning and Richter be locked in as the earth-face deities? (deferred)
- What are the friendship threshold values? (deferred to playtesting)
