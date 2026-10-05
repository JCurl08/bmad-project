---
title: "Campy Zelda Roguelite: 2-Week Build Plan"
type: build-plan
initiative: initiative-video-game
date: 2026-10-05
source: .memlog.md
---

# 2-Week Build Plan (solo, 14 days)

**Scope source:** MoSCoW in `.memlog.md`, with the later corrections applied. A final boss is MUST. The Must-tier boss is a single-phase Maxwell's Demon that sorts one thing (hot/cold); a simpler boss is an acceptable fallback. Maxwell's Demon with face-based sorting phases is SHOULD.

**Build order (from the synthesis):** seed-driven modular swap engine first, then one vertical-slice face end to end, then the remaining faces, then the boss, then meta upgrades, then the Should items, then polish.

**Content budget:** 6 faces x 3x3 = 54 screens. Town face is the fixed start. The 5 science faces are Darwin (Biology), Curie (Chemistry), Einstein (Physics), Euler (Math) and Earth & Atmosphere.

## Open decisions (settle on or before the day noted)
- [x] Engine/platform: **Unity, 2D** (decided). Project setup on Day 1
- [x] Euler face race: **polyhedra/shape people** (circles fast, triangles stable, rhombi glide) (decided)
- [x] Earth & Atmosphere race: **terrestrial vs avian dinosaurs** (decided)
- [x] Earth & Atmosphere signature item: **weather wand**, starts with one phenomenon (quake, rain, snow, tornado...) and gains more with upgrades (decided)

---

## Week 1: Engine, slice, faces

### Day 1: Engine decision and skeleton
Goal: a character walks between screens, Zelda-overworld style.
- [ ] Create the Unity 2D project (URP 2D or built-in), add a Unity `.gitignore` and Git LFS for art/audio, push to the repo
- [ ] Set up a top-down player controller: move, interact, use item
- [ ] Add player health and damage hooks, plus stat fields (health, defence, power, speed) for meta upgrades later
- [ ] Build one placeholder 3x3 grid with screen-edge transitions
- [ ] Add a debug overlay showing the current face, screen coordinates and seed

### Day 2: Modular swap engine (core system)
Goal: a seed builds the whole cube from interchangeable modules.
- [ ] Data model: 6 faces x 3x3 screens, with face adjacency (cube edges wrap to neighbouring faces)
- [ ] Screen modules: interchangeable screen templates with tagged entry/exit points and gate slots
- [ ] Seeded RNG service, used by every system that shuffles
- [ ] Town face is always the start; the 5 science faces are shuffled into cube positions **on leaving town**
- [ ] Debug: reroll seed, jump to any face/screen
- [ ] First WebGL build: check that it loads, plays audio after a click and saves/loads in the browser
- [ ] **Checkpoint (end of Day 2):** the same seed gives the same cube and a new seed gives a different layout. If not, fix this before anything else, because every other feature depends on it.

### Day 3: Gates, Lego NPCs, town
Goal: the shared systems every face reuses.
- [ ] Item gate framework: a gate type is bound to the item that opens it. Each face's item works mostly on its home face and in a few places elsewhere.
- [ ] Inventory/item-acquire flow
- [ ] Lego NPCs: head, torso and legs are swappable parts assembled from the seed through the swap engine
- [ ] Dialogue system with campy lines (placeholder text is fine)
- [ ] Town face, 3x3: a fixed hub containing one NPC of each present race. Treat it as a highly entropic mix of everything.

### Day 4: Vertical slice, Darwin face (part 1)
Goal: the signature puzzle and item working.
- [ ] Finch race NPCs (Lego heads = beaks)
- [ ] Beak items: a thick beak breaks pots/rocks, and a thin long beak presses distant buttons and pollinates flowers
- [ ] Pollinated flower grows a bridge or vine on an adjacent screen (biology gate)
- [ ] Place beak gates mostly on Darwin's face, with a few elsewhere on the cube

### Day 5: Vertical slice, Darwin face (part 2), end to end
Goal: one complete face inside a real run.
- [ ] Build all 9 Darwin screens from modules
- [ ] Full loop: town, then shuffle, then enter Darwin's face, solve, get the item, open gates, return
- [ ] Campy finch dialogue pass
- [ ] Note the per-face build time and use it to estimate the remaining 4 faces
- [ ] **Checkpoint (end of Day 5):** is the Darwin slice complete end to end? **If behind, cut faces from 3x3 to 2x2 (6 x 4 = 24 screens). Do not cut puzzle types.** Re-plan Days 6–9 on that basis.

### Day 6: Curie face (Chemistry)
- [ ] Decay item, re-obtainable, with a half-life timer that moves it through its stages
- [ ] Stage uses: the glowing isotope lights dark rooms, the unstable stage blasts walls, and the lead stage holds down switches
- [ ] Radioactive mushroom-people race NPCs
- [ ] Build 9 screens (or 4) and the gates

### Day 7: Einstein face (Physics)
- [ ] Boulder dragging, and a time-dilation field around mass
- [ ] Timed doors stay open longer near boulders, and more boulders slow time further
- [ ] Alien-like race NPCs
- [ ] Build 9 screens (or 4) and the gates

### Day 8: Euler face (Math)
- [ ] Bridge-crossing puzzle: cross every bridge exactly once to open the door (Eulerian path)
- [ ] Add a validator that checks each seeded bridge layout is solvable
- [ ] Polyhedra/shape-people NPCs (circles fast, triangles stable, rhombi glide)
- [ ] Build 9 screens (or 4) and the gates
- [ ] **Checkpoint (end of Day 8):** 4 science faces playable? If not, finish them in a stripped form (signature puzzle + item + gates only) and push Earth & Atmosphere to Day 9 morning only.

---

## Week 2: Earth face, boss, meta, Should items, polish

### Day 9: Earth & Atmosphere face
- [ ] Weather traversal hazards: wind pushes, rain, snow causes sliding, freezing rain slows
- [ ] Earthquake spots that open crevices the player must cross
- [ ] Centre screen is the calm hurricane eye (with 2x2 faces, use any one screen)
- [ ] Weather wand: one phenomenon at start (random per run), more via upgrades
- [ ] Dinosaur NPCs: terrestrial vs avian
- [ ] Build 9 screens (or 4) and the gates

### Day 10: Final boss (Must tier)
- [ ] Core arena, reachable from any face and open from the start (very hard but beatable)
- [ ] Single-phase Maxwell's Demon: the arena starts sorted (hot/cold), and the player wins by mixing it into chaos faster than the demon can re-sort
- [ ] Win and lose states that hook into the run loop
- [ ] **Checkpoint (end of Day 10):** is the boss working? If the demon is not working, fall back to a simpler boss (allowed by the log) and move on.

### Day 11: Meta progression and full run loop
- [ ] Meta currency earned per run
- [ ] Stat upgrade shop: health, defence, power, speed
- [ ] Persistence: save and load meta progression across runs
- [ ] Run reset on death or victory: new seed, abilities reset
- [ ] Tune first-run difficulty: the whole map can be explored but is very hard on the first run
- [ ] **Checkpoint (end of Day 11): MUST scope complete.** Playtest 3+ seeds from start to finish. Start the Should items only if this checkpoint is green; otherwise spend Days 12–13 on Must fixes.

### Day 12: Should items (1)
- [ ] Town talk-order draft: the first race you talk to becomes your ally (better advice, useful items such as healing), and the last race turns hostile for the run
- [ ] Campy snub dialogue for the last race ("Oh, NOW you have time for me?")
- [ ] Re-collected abilities come back as an improved version (ship this before persistent gear)
- [ ] Puzzle difficulty knob: puzzles scale with run conditions

### Day 13: Should items (2), then feature freeze
- [ ] Extra demon sorting phases built from the faces present in the run (Curie people by decay stage, finches by beak)
- [ ] Beat the demon multiple times to truly beat the game
- [ ] **Feature freeze at end of day.** Unfinished Should items move to the cut list.

### Day 14: Reserved buffer, polish and playtest (no new features)
- [ ] Seed sweep: run many seeds and check for softlocks and unreachable gates
- [ ] Campy dialogue and tone pass across all races
- [ ] Bug fixes from playtests
- [ ] Balance first-run difficulty and boss difficulty
- [ ] Build WebGL and publish to itch.io (Windows build as fallback)

---

## Cut order (drop first, top to bottom)
1. Multiple demon victories
2. Face-based demon sorting phases (keep single-phase hot/cold)
3. Puzzle difficulty knob
4. Town talk-order ally/enemy draft
5. Improved re-collected abilities
6. **Must-tier fallback:** faces 3x3 to 2x2 (54 to 24 screens). Never cut puzzle types.
7. **Must-tier fallback:** single-phase demon to a simpler boss

## Stretch (only after the Day 14 buffer is safe), mapped to the Could items
- [ ] Persistent gear: keep one piece of equipment between runs, with upgrade branches and ammo capacity upgrades
- [ ] Player starts colourless and gains colours from the abilities collected
- [ ] Rival race pairs: befriending one race angers its rival and locks the rival's region for the run
- [ ] Chaos-scaled dialogue: scrambled word tiles the player rearranges to decode hints
- [ ] Mid-run cube twists (on a timer or big events)
- [ ] Rotating storm bands around the hurricane eye
- [ ] Death-steered shuffle: the face you died on moves nearer the start face next run
- [ ] Gags: Newton cameo, and skeleton-key fingers on late-decay Curie people

## Won't (this build)
- Freud and Turing faces, dream layer
- 2 missing themes per run
- Janitor premise
