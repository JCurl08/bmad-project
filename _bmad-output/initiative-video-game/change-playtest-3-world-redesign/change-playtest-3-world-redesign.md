---
type: sprint-change-proposal
title: "Playtest 3: a world worth exploring"
initiative: initiative-video-game
date: 2026-10-07
mode: batch
status: approved
scope: major
---

# Sprint Change Proposal: playtest 3, a world worth exploring

## 1. Issue summary

**Trigger:** the user's playtests of stories 1.24 (`b589350`) and 1.25 (`d8b5e0a`). Combined with the open items in `playtest-2-feedback.md`, the feedback moves the game from "a shuffled cube of puzzle screens" to "a world of towns, cultures and rewards worth exploring".

**Evidence, from the user's words, grouped:**
- **Island screens** (built in 1.25):
  - Liked: "I like the change in scale of the bridge puzzles".
  - Problems: "one is impossible"; it needs "some indication that it is activating something. Like the bridge changing colour"; and "I don't like the weird brown dots… just make these regular tiles".
- **Characters:**
  - "more meaningful names" for all characters.
  - Each race needs "a separate culture and personality… reflected by their manner of speech":
    - Math: logical and terse, but helpful.
    - Chemistry: emotionally reactive.
    - Finches: almost Shakespearean and proper.
    - Physics: philosophical and abstract.
    - Earth: rugged and outdoorsy (the user's choice).
  - From playtest 2: drop the swappable-part look and make "nice looking characters with a similar design to the main character".
- **Towns:** "Still missing the towns in the center of each face". Playtest 2 asked for distinctive buildings, buildings you can enter, statues, more people than on other screens, no enemies, and one leader per town who gives a quest for that region.
- **Scale:** narrower edge walls and bigger screens. The user chose about 2× the screen area, with bigger people.
- **Ice and wind:**
  - Ice is a real sliding puzzle: the whole screen is ice, and you slide straight until stopped, until you fall into a pit, or until you reach a tile with grip.
  - Wind streams push you in one direction.
- **Puzzle difficulty and rewards:**
  - Set puzzle difficulty by looking at puzzles in shipped games.
  - Every roadblock hides something: coins, equipment, an upgrade, a key or a fragment. Items no longer just lie on the ground.
  - Every screen has something: a puzzle, enemies or an object.
- **Fragments:**
  - Each face has a die number. Town is 1; the other faces get 2–6 at random each run. A face has that many fragments, each numbered.
  - Once all of a face's fragments are found, its town leader repairs the face, which grants a boss benefit.
  - The benefits named: fewer things to un-sort, a wider gap, particles drawn towards you, and a slower boss. The user chose two more: +1 heart, and slower, less frequent shots.
- **Boss entrances:** only one portal leads to the boss. The others lead to an underground lair where trapped people give hints.
- **Still open from playtest 2:**
  - locked face-edge gates, each needing equipment, giving several ways into the world;
  - a temple per face;
  - whole-screen weather.

**Issue type:** a stakeholder redesign after hands-on play, plus one suspected defect: an island screen that may be impossible.

## 2. Impact analysis

**Epic impact**
- **Epic 1 (playable cube)** closes almost as is.
  - It adds one fix story, 1.26, for the island screens. Then comes the release (1.14) as a playable baseline.
  - CAP-9 (swappable NPC parts) leaves Epic 1, as the user asked in playtest 2.
- **New Epic 4: "A world worth exploring"**, built next, before the old Epic 2.
  - It holds the redesign: scale, characters, cultures and names, towns, fragments and leaders, roadblock rewards, locked edge gates, sliding ice and wind, puzzle difficulty, and the boss lair.
- **Epic 2 (varied runs)** moves after Epic 4.
  - It gains CAP-9 (the swappable-part system as a variety layer on top of the new character designs).
  - The town draft (CAP-14) will build on the new towns and leaders.
- **Epic 3 (stretch)** is unchanged. CAP-23's storm bands and CAP-21's rival pairs fit the new weather and cultures.

**Built work affected**
- **Screen size** (16×10 tiles today) is assumed everywhere: modules, layouts, plans, spot filters, the sweep, tests and the determinism fixture. Doubling the area touches all of it, so it goes first in Epic 4.
- **Characters:** the `NpcPartArt` parts system stays in code for Epic 2. Epic 4 adds one hand-made design per race (and per role for leaders), in the player's style.
- **Text:** Dialogue, HintGenerator and every face's population lines get rewritten in each race's voice. The subtle-science rule and InWorldText still apply.
- **Face centres:**
  - Every face's centre screen becomes a town. Math, Physics and other content on centre screens moves off.
  - Earth's calm "eye" centre already fits.
  - Town's start screen is already its centre (1.24).
- **Items and gates:**
  - Optional gates and hidden items become roadblocks with rewards.
  - ItemPlacement gains reward kinds: coins, equipment, upgrade, key and fragment.
  - The sweep proves that every fragment and the real boss portal can be reached.
- **Earth (1.20):** the hazards become whole-screen sliding-ice and wind-stream puzzle screens. Freezing rain and crevices stay as they are, or are folded in.
- **Math (1.25):** island screens are kept and made into real puzzles: crossing a bridge lights it, and the reward is behind a roadblock. Brown pads become floor tiles.
- **Core:**
  - Portals: one leads to the arena and the rest lead to lair rooms.
  - The arena reads boss benefits: particle count, gap width, attraction, Demon restore speed, +1 heart, and shot rate.
  - The skill check (60–120 s with the starting kit, no benefits) stays.

**Artifact conflicts.** These need a spec update through `bmad-spec`:
- **CAP-2:** locked face-edge gates, and walls with doorways.
- **CAP-7:** island screens and bridge lighting replace "exactly-once door". The success measure changes.
- **CAP-8:** sliding ice and wind streams replace "hazards change movement".
- **CAP-9:** moves to the Should tier (Epic 2).
- **CAP-11:** one real portal, plus boss benefits.
- **CAP-13:** coins come from roadblocks too.
- **New capabilities:**
  - CAP-28: a town on every face, with leaders and enterable buildings.
  - CAP-29: fragments and repairs that give boss benefits.
  - CAP-30: roadblock rewards, and something on every screen.
  - CAP-31: the boss lair with trapped hint-givers.
  - CAP-32: race cultures and names.
- **Constraints:** screen size about 24×15 tiles. Character art is one design per race in the player's style.
- **Companions:**
  - `faces-and-races.md`: cultures, voices, towns, leaders, benefits.
  - `lore-and-tone.md`: names; placeholder `*` stays only on region and item names until the naming brainstorm.
  - `scope-tiers.md`.

There is no PRD, architecture or UX document; the spec is the source.

**Technical impact**
- A large re-record of determinism.
- A new sweep model, with keys, locked edges, fragments and the lair.
- Two new puzzle generators, sliding ice and wind streams, each with a solver.
- Interior rooms for enterable buildings: a sub-screen system.
- The WebGL size and memory budget needs a check after the scale change.

## 3. Recommended approach

**Direct adjustment plus a new epic.** The user chose this: finish and ship Epic 1 as a baseline, then build Epic 4.

**Why:**
- The current build is a complete, verified run, and shipping it de-risks the itch.io pipeline.
- The redesign is big enough to deserve its own done-when.
- Scale first, then systems (fragments, rewards, edges), then content (towns, puzzles, lair), then text and art polish. Each later story then builds on the final screen size.

**Effort:** about 15 stories. At the current pace of 1–3 hours of agent time per story, verified and reviewed, that is about 3–4 days, so roughly 2026-10-11. It stays inside the 14-day limit (2026-10-19), with the user playtesting between batches.

**Risk:** high, mostly from the scale change and the sweep model. It is mitigated by:
- the research spike before the puzzle stories;
- solver-checked generators;
- a refactor sweep before the release.

## 4. Detailed change proposals

### Epic 1: add story 1.26, before 1.14

```
Entry 26 — "Island screen fixes"
Description:
- Investigate the island screen the user found impossible: a runtime mismatch with the sweep, or the deliberately isolated doorway reading as a bug. Fix it, or make the dead end readable.
- Replace the brown island pads with the region's floor tiles.
- Show activity: a bridge plank lights up (changes colour) once walked or mended, and a mended broken span changes colour.
Verify: every island screen on seeds 1–50 is walked by a runtime test from each connected doorway; all suites and the sweep pass; HITL look.
after = [25]; hitl = true; risk = medium
Entry 14 (release): after = [26]
```

### New Epic 4: "A world worth exploring"

**Outcome:** a player explores a bigger, warmer cube with these features:
- a town in the middle of every face, with its own culture and leader;
- every roadblock hides a reward;
- fragments let leaders repair their faces and give boss benefits;
- puzzles at a tuned difficulty;
- a boss lair behind false portals.

**Done when:**
1. Every face's centre screen is a town with distinctive buildings, a statue, an enterable building, a leader, extra townsfolk and no enemies.
2. Every roadblock hides a reward, and every screen holds a puzzle, enemies or an object.
3. All fragments of a face are reachable on seeds 1–50. Repairing a face applies its benefit to the boss fight.
4. Sliding-ice and wind-stream screens are solver-checked, with difficulty within the spike's targets.
5. Only one core portal reaches the boss. The others reach lair rooms with hint-giving prisoners.
6. All characters have names and race voices. The sweep is 50/50, and it ships to itch.io.

**Stories, in build order:**

| # | Story | Summary |
|---|---|---|
| 4.1 | Bigger screens, thinner walls | Screens about 24×15 tiles (about 2× the area) on the 960×600 canvas. Edge walls half a tile. People drawn larger. Modules, layouts, plans, the sweep and tests are rescaled. Determinism is re-recorded. |
| 4.2 | Puzzle difficulty research (spike) | Survey shipped games' versions of each puzzle: sliding ice (e.g. Pokémon ice paths), currents and wind (Zelda-style), bridge or Euler puzzles and island-bridge puzzles (e.g. Hashiwokakero), boulder and Sokoban-lite puzzles, and lamp or ordering puzzles. Write difficulty targets per generator (solution length, branching, red herrings). |
| 4.3 | Character redesign | One hand-made design per race, in the player's style: shape people, finches, Chemistry folk, Physics folk, dinosaurs (ground and avian) and townsfolk. Leaders get a distinct variant. The parts system stays in code for Epic 2. |
| 4.4 | Race cultures and names | Each race gets a voice: Math logical and terse but helpful; Chemistry emotionally reactive; finches Shakespearean and proper; Physics philosophical and abstract; Earth rugged and outdoorsy; townsfolk a friendly mix. Every character gets a meaningful name, unstarred. All dialogue and hints are rewritten. The subtle-science rule holds. |
| 4.5 | Towns on every face | Each face's centre screen is a town with buildings in its race's style, a statue, more people, no enemies and a leader. Centre-screen content moves off. |
| 4.6 | Enterable buildings | At least one building per town can be entered: an interior room with people or a shop counter. It hooks into the map. |
| 4.7 | Roadblock rewards | Every gate, optional gate and hidden spot hides a reward: coins, equipment, an upgrade, a key or a fragment. No loose ground pickups. Every screen has a puzzle, enemies or an object. ItemPlacement and the sweep are extended. |
| 4.8 | Fragments and face repair | Die numbers per face each run (Town 1, the others 2–6 shuffled). The face numbered k has k numbered fragments. The leader's quest: bring them all, then the face is repaired. HUD and map show fragments. The boss benefits are fewer particles, a wider gap, particles drawn to you, a slower Demon restore, +1 heart, and slower, less frequent shots. Which face grants which is seeded or fixed (decided in the plan). |
| 4.9 | Locked face-edge gates | Face-edge doorways start locked, each needing a key or a piece of equipment. Different edges have different locks, giving several ways into the world. The sweep proves reachability. |
| 4.10 | Sliding ice screens | The whole screen is ice: slide straight until blocked, a pit (fall, then respawn at the entry) or a grip tile. A generator with a solver, at the spike's difficulty targets. It replaces the snow and ice hazard. |
| 4.11 | Wind stream screens | Streams push the player one way until they leave the stream. The puzzles use streams, walls and grip. Generator plus solver. It replaces the wind hazard. |
| 4.12 | Bridge puzzles, tuned | Island screens use the spike's best match, e.g. lighting every bridge as you cross it to open a reward roadblock. Difficulty is set from the spike. |
| 4.13 | Boss lair | One seeded portal leads to the arena. The others lead to underground lair rooms with trapped people who give hints about the Demon and about the repair benefits. The map shows the lair. |
| 4.14 | Temples (optional) | Each face's signature item sits in a temple screen behind the face's hardest roadblock, Zelda-style. **Assumption:** fragments and repairs replace "a temple item gives a boss benefit". The user may drop or reshape this. |
| 4.15 | Refactor sweep | Cleanup. |
| 4.16 | itch.io release 2 | HITL: build, upload and play. |

### Spec update, applied with `bmad-spec`

- Rewrite: CAP-2, CAP-7, CAP-8, CAP-11 and CAP-13.
- Move CAP-9 to the Should tier (Epic 2).
- Add CAP-28 to CAP-32.
- Constraints: about 24×15-tile screens; one character design per race.
- Companions: `faces-and-races.md` (cultures, voices, towns, leaders, benefit list), `lore-and-tone.md` (the name rules) and `scope-tiers.md`.

## 5. Implementation handoff

**Scope:** major. It needs a new epic, an updated spec and resequenced epics. Since this is a solo project, I do the planning (`bmad-spec`, then `bmad-ticket`) and the build, and the user approves and playtests.

**Steps after approval:**
1. Add story 1.26 to Epic 1 and set 1.14 after it. Build 1.26 now.
2. Update the spec through `bmad-spec`, appending to the memlog and re-deriving.
3. Create Epic 4 with `bmad-ticket` and set the epic order to 1 → 4 → 2 → 3.
4. Build Epic 4 story by story, under the standing self-approval instruction. Stop for the user at real design calls. Suggested playtest points: after 4.5, after 4.9, and after 4.13.
5. The user releases 1.14, and later 4.16.

**Success criteria:** Epic 4's done-when list above; the sweep at 50/50; the boss skill check still within 60–120 s with no benefits.
