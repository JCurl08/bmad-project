# Player guide: how a run works

This describes the game as of story 4.13 (commit `3f3a938`). Names ending in `*` are placeholders until the naming brainstorm.

## The goal
The world is a cube of 6 faces, each a 3×3 grid of screens. **Primsworth the Demon** hides in the cube's core. A run ends when you beat Primsworth or run out of hearts. Either way you keep your **Jumbles\*** (the currency) and spend them on permanent upgrades before the next run.

You don't have to do everything. A full run means getting out of Town, collecting items and keys, mending faces for boss help, finding the real core portal, and winning the fight.

## Controls
| Action | Key |
|---|---|
| Move | WASD or arrow keys |
| Swing / use your held item | Enter or left mouse button |
| Talk, open, interact | E |
| Switch held item | 1 (previous) / 2 (next) |
| Map (pauses the game) | M or Tab |

Your **swing uses the item you're holding**. Most puzzles and gates are solved by holding the right item and swinging at the thing.

## Step by step

### 1. Start in Town (the white face)
- You start on Town's centre screen, in the town square, with **1 key**.
- **Talk to everyone (E).** People give hints about where items are, in their own voice. Hints always tell the truth.
- **Talk to Mayor Muddleton** (crown, by the statue). He hands you **Town's fragment**. Talk to him again and he mends Town, which gives you **+1 heart in the boss fight**.
- The building with a rug and lantern at its door can be entered: walk straight north into the door.

### 2. Leave Town through a gate
- Each screen edge is a wall with one doorway in the middle. The doorways **between faces** are stone arches, and every arch starts **locked**:
  - **Keyhole plate:** needs a key. Swing at it, or press E. It uses up 1 key.
  - **Item icon plate:** needs that face's item. Walk into it or swing at it while holding the item. Nothing is used up.
- Your starting key opens one of Town's keyhole arches. **Choose which face to explore first.** The others open later, with keys or items you find.
- When you first leave Town, the other faces are revealed and shuffled for this run. Crossing to a new face **stops you**; press a direction again to walk on.

### 3. Explore a face
Every science face has a colour and a race of people:

| Face | Colour | People | The face's item | What the item opens |
|---|---|---|---|---|
| Greenwood\* | Green | Finches | Thin Beak\* or Thick Beak\* (one each run) | Thin: distant buttons and flowers. Thick: pots and rocks. |
| Ember Vale\* | Red | Mushroom folk | Glowstone\*, Wildstone\* or Leadstone\* (one each run) | Glow: dark rooms. Wild: cracked walls. Lead: pressure plates. |
| Weightlands\* | Blue | Alien folk | Heavy Mitt\* | Pick up boulders (E) and pile them by **timed doors**, then hit the door's switch and run. More boulders keep the door open longer. |
| Bridgelands\* | Yellow | Shape people | Gap-Crosser\* | Swing at a gap gate or a broken bridge to lay a plank. |
| Wildlands\* | Orange | Dinosaurs | Stormcaller\* (one weather each run) | Quake knocks down rock pillars, rain puts out fire walls, snow freezes water, tornado blows debris away. |

**Finding the face's item:**
- Look for **dirt mounds**. Swing at one twice, even empty-handed, to dig up what's under it.
- Look for **chests behind gates**. Walk into a chest or swing at it to open it.
- Ask the locals: their hints name the screen.
- A face's own item is never locked behind its own gate.

**What's hidden behind roadblocks:**
- **Jumbles\***: currency, kept after the run.
- **Items**: the face items above.
- **Keys**: each face hides 2. They open keyhole arches.
- **Chest keys**: open the pale-blue keyhole gates in front of bonus chests, worth 10 Jumbles\* each.
- **Run boosts**: +1 heart, faster feet or a harder hit, until the run ends.
- **Fragments**: see step 4.

**Puzzle screens:**
- **Ice (Wildlands\*):** you slide in a straight line until something stops you. Pits drop you back at the doorway you came in by, with no damage. Grip tiles stop you. Slide onto the gold pad to open the chest.
- **Wind (Wildlands\*):** step into a gust and it carries you to the next island or grip rock. You can't steer while riding. Reach the gold ring to open the chest.
- **Bridges (Bridgelands\*):** every plank lights up when you cross it. **Crossing a lit plank again turns them all off**, so you need a route that uses each plank exactly once. Light them all and the chest opens.
  - Tip: count each island's planks. If two islands have an odd number, start on one of them.
  - One doorway reaches the puzzle only over a broken span, so use the Gap-Crosser\* there.
- **Trials:** each face has one harder puzzle (numbered targets, boulder booths and similar) that pays Jumbles\*.

### 4. Mend faces for help against the boss
- Each run, every face rolls a **die number**. Town is always 1; the other faces get 2–6.
- **The face numbered k hides k fragments** behind its roadblocks. The HUD dice show how many you've found, and the map shows tags such as "2/4".
- Bring all of a face's fragments to its **leader** (crowned, by the town statue on the centre screen). The face is mended, and its statue turns gold.
- Each mended face helps you in the boss fight:

| Mended face | Help in the fight |
|---|---|
| Town | +1 heart |
| Greenwood\* | Particles drift toward you |
| Ember Vale\* | Fewer particles to mix |
| Weightlands\* | The Demon restores order more slowly |
| Bridgelands\* | Wider gaps in the Demon's wall |
| Wildlands\* | The Demon's shots are slower and rarer |

### 5. Find the real core portal
- Each science face has one **core portal** (pink). **Only one of them leads to Primsworth.**
- The others drop you into a **lair room** with four caged prisoners. Talk to them: they give fight tips, tell you which mended face helps how, and say **where the real portal is**. Walk south out of the room to return.
- Once you know the real portal, the map marks it in gold.

### 6. Beat Primsworth the Demon
- The arena is split by the Demon's wall. **Mix the particles by knocking them through the gaps** faster than the Demon can sort them back.
- The Demon glows and draws a line before it shoots, so step aside.
- It tires as the fight goes on.
- Need to leave? Walk out of the door in the **west wall** (straight west into the recess). The fight resets.

### 7. Between runs
After you win or die, the between-runs screen shows what you earned. Spend Jumbles\* on permanent upgrades (Extra Heart, Thick Skin, Big Bonk, Zoomies), then **Continue** into a new, freshly shuffled run.

## A good first run
1. Talk to the Mayor twice. That gives you Town's fragment and mends Town.
2. Spend your key on a Town arch.
3. On that face, dig mounds and talk to people until you have its item and its 2 keys.
4. Use keys and items to open more arches. Grab fragments and chests as you go.
5. Mend at least one or two faces.
6. Try core portals. A lair tells you where the real one is.
7. Fight Primsworth.

## Stuck? Dev keys
| Key | What it does |
|---|---|
| F11 | Reveal the whole map |
| Shift+F11 | Unlock every arch |
| Shift+F9 | Get every fragment of the face you're on (then talk to its leader) |
| F6 | Jump to the next screen |
| F8 | Leave the arena, a building or the lair |
| F9 | Rest to full health |
| F10 | New run |
| F5 | Reroll the seed |
| F1 | Debug overlay |
| F3 | Spawn NPCs (Shift+F3: all six leaders) |
| F7 | Spawn an enemy |

**Combat tip:** most enemies are weak to one face's item. Switch items (1/2) and try them.
