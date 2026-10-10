# Player guide: how a run works

This describes the game as of commit `951bec4` (Epic 4 through 4.19), plus 4.20 (the Mayor's tour of Town in place of the lost-and-found trail, yes/no questions in conversations, and a much stronger Weightlands\* gem). Names ending in `*` are placeholders until the naming brainstorm.

## The goal
The world is a cube of 6 faces, each a 3×3 grid of screens. **Primsworth the Demon** hides in the cube's core. A run ends when you beat Primsworth or run out of hearts. Either way you keep your **Jumbles\*** (the currency) and spend them on permanent upgrades before the next run.

You don't have to do everything. A full run means getting out of Town, collecting items and keys, opening temples and winning their gems for boss help, finding the real core portal, and winning the fight.

## Controls
| Action | Key |
|---|---|
| Move | WASD or arrow keys |
| Swing / use your held item | Enter or left mouse button |
| Talk, open, interact | E |
| Switch held item | 1 (previous) / 2 (next) |
| Map (pauses the game) | M or Tab |
| Quest log (pauses the game) | Q (also from the map; Q or Esc closes it) |

**Talking pauses everything.** While a conversation is open you can't move, swing or switch items, and nothing else moves either: other people, enemies, hazards, timed doors, wind and ice, and the boss fight all wait. E pages through the lines; Esc skips to the end.

**Questions.** Some conversations end in a yes/no question (the two answers show along the bottom of the box). Pick with left/right or up/down (arrows or WASD), then press E to answer. Esc closes it without answering; you'll be asked again next time. The direction keys only choose: you won't walk off when the conversation ends, even if a key is still held (press it again to walk).

Your **swing uses the item you're holding**. Most puzzles and gates are solved by holding the right item and swinging at the thing.

## Step by step

### 1. Start in Town (the white face)
- You start on Town's centre screen, in the town square, with **1 key**.
- **Talk to everyone (E).** People give hints about where items are, in their own voice. Hints always tell the truth. Town's folk come from every face, so each speaks in their own race's voice; only Hattie Hullo (the greeter, waving) and the Mayor have Town's own look. Nobody in Town ever turns hostile: it's the one place everyone still mixes.
- **Talk to Mayor Muddleton** (purple top hat, by the statue). Town keeps one fragment of its own, and on your first talk he asks: **shall he show you round first?**
  - **Yes: the tour.** A glowing marker (a mint ring with a little gold flag) lights on a screen next to the square. Walk onto it and its sign says one short lesson with a tiny task; do the task and the next marker lights, one doorway on. The stops go round the square in a ring (where it starts and which way round changes each run):

    | Stop | Lesson | Tiny task |
    |---|---|---|
    | 1 | Moving between screens | Get there: walk through a doorway |
    | 2 | Talking | Talk to Gus Guidewell, the tour guide, beside the marker (E) |
    | 3 | Swinging and digging | Swing twice at the heap of dirt beside the marker |
    | 4 | Chests | Open the chest beside the marker (walk into it or swing at it): 3 Jumbles\* |
    | 5 | The map | Press M |
    | 6 | The quest log | Press Q |
    | 7 | Switching items | Press 1 or 2 (only if you hold two items or more; otherwise this stop is skipped) |
    | 8 | Locked arches | Get there: the marker stands by one of Town's arches. A keyhole takes a key (used up); a picture takes that face's item (kept). |

    The last stop hands you **Town's fragment**. Your quest log ("Mayor Muddleton's tour") always says the next stop and its task.
  - **No:** he hands you Town's fragment at once.
  - **Changed your mind?** Talk to the Mayor during the tour: he says where the next stop is and asks whether to skip the rest. Yes hands you the fragment at once; no keeps the tour going. The tour never stops you leaving Town: the arches work as usual, and the tour waits for you.
  - Take the fragment back to the Mayor and he **opens Town's temple** (the white-columned building east of the square). Solve its puzzle and you win **Town's gem: +1 heart in the boss fight**.
- **Errands:** in each town, one person has an errand: carry a word to someone else in that town. Other would-be quest-givers just gossip.
- **Quest log (Q):** every quest someone gives you is added: each leader's fragment quest (with how many you've found), the Mayor's tour or Town's fragment (with your next step) and each town's errand. Finished ones are marked Done.
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
| Weightlands\* | Blue | Alien folk | Heavy Mitt\* | Pick up boulders (E) and pile them by **timed doors**, then hit the door's switch and run. More boulders keep the door open longer. **E always drops the boulder you hold**, whichever way you face: it is set down where it is, or on the nearest free spot beside you. |
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
- **Bridges (Bridgelands\*):** every dark wooden plank glows green when you cross it. **Crossing a glowing plank again turns them all off** (a red flash), so you need a route that uses each plank exactly once. Leaving the screen unfinished turns them off too. Light them all and the chest opens.
  - **Grey planks never glow:** they are lookouts, or broken spans (gold once mended). They don't count.
  - A sign stands on each doorway's island, and arriving there says the rule.
  - Tip: count each island's planks. If two islands have an odd number, start on one of them.
  - One doorway reaches the puzzle only over a broken span, so use the Gap-Crosser\* there. Without it, go round by another doorway (its sign says so).
- **Trials:** each face has one harder puzzle (numbered targets, boulder booths and similar) that pays Jumbles\*. The temples (step 4) hold the hardest ones.

### 4. Open the temples and win their gems
- Each run, every face rolls a **die number**. Town is always 1; the other faces get 2–6.
- **The face numbered k hides k fragments** behind its roadblocks. The HUD dice show how many you've found, and the map shows tags such as "2/4".
- Every town has a **temple** (the fourth building, east of the crossroads, in its race's own style). It starts **sealed**: a glowing seal with chains over its door. Walking into it just tells you so.
- Bring all of a face's fragments to its **leader** (crowned, by the town statue on the centre screen). The leader **unseals the temple**: the seal is gone and the doorway glows. Your quest log marks the leader's quest done and adds the temple's. The HUD die gets a pale bar and the map tag says "temple".
- Town's one fragment comes from the Mayor (step 1: at the end of his tour, or at once), not from a chest.
- **Inside the temple** is one hard puzzle, a tougher take on the face's own (walk north into the door; the rule pops up as you arrive):

| Temple | Puzzle |
|---|---|
| Town | **Sliding picture.** Town's crest is cut into eight tiles on a 3×3 board with one gap; the whole crest hangs small by the west wall. Swing at a tile beside the gap and it slides in (a tile in its own place shows a gold pip in its corner). Put the crest back together; the last piece then fills the gap. The scramble always takes 14–18 slides at best. |
| Greenwood\* | **Finch song.** Four finch statues (red, gold, blue and green, with one to four crest feathers) stand round a perch. They sing a song (each finch glows, opens its beak and a note rises), then you swing at them in the same order. Each song is one note longer: 3, then 4, 5 and 6 notes. A wrong note and they sing that song again; you keep the round. |
| Ember Vale\* | **Lights out.** A bank of 15 crystals (5×3), some lit gold, some dark. Swinging at a crystal flips it and the crystals beside it (above, below, left, right). Light them all. It always takes at least 6 or 7 strikes. |
| Weightlands\* | Two booths behind timed doors that each need **three** boulders by them, and only three boulders. The temple lends you a grip, so press E (Interact) to grab a boulder even without the Heavy Mitt\*. Pile them by one door (off the gold dashed line), step on its switch across the room and dash straight along the line to the glowing band beside the booth; then drag them all over to the other door. Anywhere on the switch is fine. |
| Bridgelands\* | A big "light every plank" island network (the hardest kind). |
| Wildlands\* | A big ice screen or a big wind screen (it varies by run). |

  - In the crest, finch and crystal temples, **any swing counts**: you don't need the face's item, and nothing there hurts you. A swing that reaches two tiles or statues strikes only the one in front.
  - You can always walk back out south. **Leaving an unsolved temple resets its puzzle.** A solved one stays solved.
- **Solve it** and the face's fragments rise, spiral together and mend into the face's **gem**, which flies to you. The face is mended: its statue turns gold, the temple shows the gem over its door, the HUD die turns gold with a gem on its corner, and the map tag says "gem" in gold.
- Each gem helps you in the boss fight:

| Gem from | Help in the fight |
|---|---|
| Town | +1 heart |
| Greenwood\* | Particles drift toward you |
| Ember Vale\* | Fewer particles to mix |
| Weightlands\* | The Demon restores order much more slowly: a long rest between fetches, and it drags each one back at about a third of its speed (over three times as long between fetches) |
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
1. Talk to the Mayor and say yes to his tour (or no, if you know the ropes). Follow the glowing markers (check Q if you forget where the next one is), then bring Town's fragment back to him. That opens Town's temple: slide its crest back together for Town's gem.
2. Spend your key on a Town arch.
3. On that face, dig mounds and talk to people until you have its item and its 2 keys.
4. Use keys and items to open more arches. Grab fragments and chests as you go.
5. Open at least one or two temples and win their gems.
6. Try core portals. A lair tells you where the real one is.
7. Fight Primsworth.

## Stuck? Dev keys
| Key | What it does |
|---|---|
| F11 | Reveal the whole map |
| Shift+F11 | Unlock every arch |
| Shift+F9 | Get every fragment of the face you're on (then talk to its leader to open its temple) |
| F6 | Jump to the next screen |
| F8 | Leave the arena, a building, the lair or a temple |
| Shift+F8 | Solve the temple you're in |
| F9 | Rest to full health |
| F10 | New run |
| F5 | Reroll the seed |
| F1 | Debug overlay: two slim bars over the top and bottom walls (where you are and the seed on top, every dev key at the bottom), never over the floor |
| F3 | Spawn NPCs (Shift+F3: all six leaders) |
| F7 | Spawn an enemy |

**Combat tip:** most enemies are weak to one face's item. Switch items (1/2) and try them.
