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

A vision to realize, plus a learning goal. The developer wants a campy, lighthearted Zelda-style roguelite, focused on puzzles and adventure, that one person can finish in 1–2 weeks, and wants to gain Unity experience while building it. The player is entropy itself, exploring a Rubik's-cube world whose science-themed faces are rearranged every run, to undo "the Partition" imposed by Maxwell's Demon. Every trade-off resolves toward three things: shipping a complete, replayable game inside the timebox, making each run play differently, and keeping the entropy-is-good theme coherent.

## Capabilities

Tier tags follow `scope-tiers.md`. Face and race detail is in `faces-and-races.md`.

- **CAP-1** [Must]
  - **intent:** Each run builds a seeded cube world of 6 faces × 3×3 screens. The Town face is always the start. The 5 science faces and their screens are rearranged when the player leaves town.
  - **success:** The same seed reproduces an identical layout. Two different seeds produce different face and screen arrangements. The run always starts on the Town face.
- **CAP-2** [Must]
  - **intent:** The player explores screen to screen, as in classic Zelda, crossing cube edges onto adjacent faces. The core is reachable from every face.
  - **success:** Across 3+ seeds, every screen can be reached by walking from the Town face (gated by items, never by layout), and the core entrance is reachable from each face.
- **CAP-3** [Must]
  - **intent:** Each science face holds one signature item. That item opens matching gates, mostly on its home face and in a few places elsewhere.
  - **success:** Each item opens at least one gate off its home face. No seed places a required gate where its item cannot be obtained first (seed sweep finds no softlocks).
- **CAP-4** [Must]
  - **intent:** On the Biology face (Darwin), the player gains a beak adaptation that is random per run. A thin beak presses distant buttons and pollinates flowers, which grow bridges or vines. A thick beak breaks pots and rocks.
  - **success:** In a playtest, each beak type opens its gate types, and a pollinated flower creates a crossing on an adjacent screen.
- **CAP-5** [Must]
  - **intent:** On the Chemistry face (Curie), the player uses a re-obtainable item that decays through timed stages. The glowing stage lights dark rooms, the unstable stage blasts walls, and the lead stage holds switches.
  - **success:** Each stage solves its puzzle type. The item advances on its half-life timer and can be re-obtained after it fully decays.
- **CAP-6** [Must]
  - **intent:** On the Physics face (Einstein), boulders moved near a timed door slow time, so the door stays open longer. More mass slows time further.
  - **success:** A door that can't be passed with no boulders becomes passable with the required number of boulders next to it.
- **CAP-7** [Must]
  - **intent:** The Math face (Euler) has cross-every-bridge-exactly-once puzzles.
  - **success:** Every generated bridge layout is checked as solvable, and the door opens only after an exactly-once crossing.
- **CAP-8** [Must]
  - **intent:** The Earth & Atmosphere face has weather hazards, quake crevices to cross, and a calm hurricane-eye centre screen. Its item is a weather wand that controls one phenomenon at first and gains more with upgrades.
  - **success:** Each hazard measurably changes movement (wind pushes, snow causes sliding, freezing rain slows), and the wand's phenomenon solves at least one gate on the face.
- **CAP-9** [Must]
  - **intent:** Each race's NPCs are assembled from swappable parts: the head carries knowledge and hints, the torso the role, and the legs the movement. They speak campy dialogue.
  - **success:** NPC parts vary between seeds, and each part slot visibly affects what that NPC does or says.
- **CAP-10** [Must]
  - **intent:** Combat is a puzzle: specific items make specific enemies easier to beat.
  - **success:** Every enemy type has at least one item that makes it markedly easier to beat.
- **CAP-11** [Must]
  - **intent:** A final boss in the cube's core is open from run 1: very hard but beatable. The Must-tier boss is a single-phase Maxwell's Demon, beaten by mixing an ordered arena into chaos faster than the demon can restore order. A simpler boss is an acceptable fallback.
  - **success:** A skilled player can beat the boss with the starting kit. The boss's win and loss states end the run correctly.
- **CAP-12** [Must]
  - **intent:** A run ends on death or victory. Abilities then reset and the next run gets a new seed. The whole map can be explored on the first run, but it is very hard: enemies outmatch the starting kit, NPCs share little, and some gates stay out of reach until later runs.
  - **success:** After a run ends, the next run starts in town with reset abilities and a new layout, and nothing on the map is permanently blocked for a first-run player.
- **CAP-13** [Must]
  - **intent:** During a run, the player finds meta currency from bosses and hidden items, and spends it after the run on persistent stat upgrades: health, defence, power and speed.
  - **success:** Upgrades bought in one session are still applied after quitting and relaunching.
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
- **CAP-24** [Should]
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

- One developer, 14 days. Must scope is complete by the day-11 checkpoint, and day 14 is reserved for polish and playtests.
- 2D in Unity, built with Unity's built-in 2D tools. The developer is learning Unity, so simple implementations win.
- Release target is **WebGL on itch.io**, with a Windows desktop build as the fallback. Saves must work in browser storage, audio may only start after the player's first input, and the build should stay small.
- Content budget is 54 screens. If behind at day 5, drop to 2×2 faces (24 screens). Never cut puzzle types.
- Audience is pre-teens to adults, like *Ocarina of Time*. Camp humour only, no explicit innuendo (`lore-and-tone.md`).
- The game never says "sort". The demon's order is shown through abstract imagery and named "the Partition".
- The order the player talks to races in town is the only player influence on the shuffle.
- Puzzle and adventure come first. Combat is never a pure skill gate.

## Non-goals

- Psychology (Freud) and Computer Science (Turing) faces, and the dream layer.
- Leaving science themes out of a run. All 5 appear in every run.
- Shuffle-nudging levers beyond talk order in town, including a death-steered shuffle.
- The janitor premise.
- 3D.

## Success signal

- A new player can start a fresh seed, draft allies in town, explore all 5 science faces, solve each face's signature puzzle and beat the core boss. Three or more seeds play start to finish with no softlocks, and two different seeds visibly route the player through the faces in different orders.

## Assumptions

- Faces are assembled by shuffling hand-built screen modules, not by generating rooms procedurally.
- Meta progression is saved locally: browser storage on WebGL, or the local disk on desktop.
- Run currency (CAP-24) is a Should, because the Must loop works with meta currency alone. By default it is spent at merchant-role NPCs.
- An "improved" re-collected ability (CAP-17) is improved relative to its version in the previous run.

## Open Questions

- What does run currency buy (healing, ammo, temporary items)?
- Does "bosses" include a miniboss on each face, or only the core boss?
- What is the final title? "Entropy Cube" is a working title.
- Should Anning and Richter be locked in as the earth-face deities? (deferred)
- What are the friendship threshold values? (deferred to playtesting)
