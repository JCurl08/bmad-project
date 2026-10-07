# Puzzle difficulty research (spike 4.2)

Difficulty targets for each puzzle generator, taken from a survey of shipped games. Entries 4.7 and 4.10–4.12 test against these numbers. All targets can be measured by a solver on a seed sweep.

**Source key.** [W] = fetched or found by web search on 2026-10-07 (linked). [K] = well-known knowledge that was not fetched here; treat its numbers as approximate.

## 0. Shared rules

**Screen budget.** A screen is about 24×15 tiles. Half-tile walls leave about 23×14 inside, and each doorway needs a 2×2 non-ice landing. A puzzle therefore has at most about **21×12 cells**, and the whole puzzle is always visible on one screen, so nothing is hidden by scrolling.

**Tiers.** Set each screen's tier from its face's die number (CAP-29): faces 2–3 are **early**, 4–5 are **mid**, 6 is **late**. Town has no puzzles. Each face's trial is one tier above the face, capped at late. On a face, a screen's tier can also rise by one step for screens farther from the town.

**Step size.** The main metric should grow by about **1.5× per tier, and never more than 2×**. Pokémon Crystal's Ice Path climbs 5 → 8 → 12 slides across its three ice patches [W: [lparchive](https://lparchive.org/Pokemon-Crystal-(by-Crosspeice)/Update%2041/), [StrategyWiki](https://strategywiki.org/wiki/Pok%C3%A9mon_Gold_and_Silver/Ice_Path)]. HG/SS climbs 3 → 7 → 8 [W: [StrategyWiki HGSS](https://strategywiki.org/wiki/Pok%C3%A9mon_HeartGold_and_SoulSilver/Ice_Path)].

**Fairness rules for every generator.** These are pass/fail rules in the sweep.

| Rule | Measure | Target |
|---|---|---|
| Rules are visible | Every mechanic shows its state on screen (lit or unlit, stream arrows, grip texture, pit edge) | Required; checked at review |
| Cheap retry | Seconds from a failure back to a fresh attempt, at base speed | ≤ 5 s early, ≤ 8 s mid, ≤ 12 s late |
| No hard traps | Reachable states that can reach neither the goal nor a doorway nor a reset | **0, always** |
| Failure never hurts | Damage from a pit, reset or wrong order | 0 hearts. Only position or puzzle state resets |
| No combat inside the puzzle | Enemies whose roaming area overlaps the puzzle cells | 0. Enemies may stand on the landings or other screens |
| Crossing is easier than the reward | Doorway-to-doorway route cost, compared with the reward route cost | ≤ 0.5× the reward route |
| Obvious what, not how | The goal (roadblock, lamps, flag) is visible from every doorway landing | Required (GMTK, "What makes a good puzzle" [W: [summary](https://www.resetera.com/threads/what-makes-a-good-puzzle-game-makers-toolkit.29537/)]) |

**Red herrings.** Here a red herring means an element that looks like progress but does not lie on any shortest solution. Use them sparingly: the GMTK and [Dis Pontibus devlog](https://marcosd.itch.io/dis-pontibus-2/devlog/228271/red-herrings) [W] both argue that clutter frustrates and only a meaningful decoy is fun. Targets: **0 early, 1 mid, 1–2 late**.

**Random-player solve rate (P_rand).** The share of uniformly random legal play-outs (at most 3× the minimum length, stopping at the first failure) that reach the goal. Measure it by Monte Carlo with 2,000 runs per screen. It stands in for "how much thinking is needed", like the `minjump` measure in the [Ohohcakester ice generator](https://github.com/Ohohcakester/Ice-Sliding-Puzzle/blob/master/IceSlide/Manual.txt) [W]. Each generator below gives its own bands.

## 1. Per generator

### 1a. Island bridges (Math, 4.12)

**Examples.**

| Game | What it is | Source |
|---|---|---|
| Minish Cap, Dark Hyrule Castle (2 rooms) | Blue floor tiles turn red when stepped on. Stepping on a red one stops progress, and a nearby floor switch resets the room. All tiles red opens the door. This is the closest match to "the bridge changes colour". | [W: [zeldadungeon](https://www.zeldadungeon.net/the-minish-cap-walkthrough/dark-hyrule-castle/), [Zelda Wiki: Tile](https://zelda.fandom.com/wiki/Tile)] |
| Pokémon R/S/E, Sootopolis Gym (3 floors, growing) | Step on every cracked ice tile; a tile used twice drops you a floor. ORAS added solid safe tiles that can be crossed any number of times. | [W: [Bulbapedia](https://bulbapedia.bulbagarden.net/wiki/Sootopolis_Gym), [Hole/Cracked tile](https://bulbapedia.bulbagarden.net/wiki/Cracked_tile)] |
| Professor Layton, Curious Village #30 / #39 | "One-line" puzzles that are Euler paths; the parity rule is the hint | [W: [Layton wiki](https://layton.fandom.com/wiki/Puzzle:One-Line_Puzzle_1)] |
| EulR, Path ONE (mobile) | Euler-path puzzle games built over many levels | [W: [EulR](https://play.google.com/store/apps/details?id=com.eulr.puzzle)] |
| Hashiwokakero (Nikoli) | Players draw bridges to match the numbers on islands. It is a deduction puzzle, not a walking one. | [W: [rules + generator](https://github.com/erthium/hashiwokakero), [Hashi solver](https://www.kakuro-online.com/hashi/)] |
| The Witness | Line puzzles: the rules are taught one at a time, then combined | [K] |
| Spirit Tracks, Tower of Spirits | Walk green → red to trace a symbol, following a Phantom's route | [W: [zeldadungeon ToS](https://www.zeldadungeon.net/spirit-tracks-walkthrough/tower-of-spirits-4/)] |

**Fair or fun when:**
- the state is fully visible (lit or unlit on every plank);
- a reset is instant and costs nothing;
- the graph is small enough to hold in your head;
- one parity "aha" exists: start on an odd island.

**Frustration traps:**
- a reset that is hidden or slow (Sootopolis makes you walk back up through trainers);
- accidental crossings: walking past the end of a plank, or a doorway lane that counts as a crossing (1.19's own review bug);
- the graph is too dense to see;
- an "impossible" screen that is really a deliberately isolated doorway, which the 1.25 playtest reported.

**Targets.** These are for the lit network: the set of whole planks that the rule counts. V = islands on that network, E = planks.

| Metric (solver) | Early | Mid | Late |
|---|---|---|---|
| Planks E | 5–7 | 8–11 | 12–16 |
| Islands V | 4–5 | 6–8 | 8–11 |
| Independent cycles (E − V + 1) | 1–2 | 3–4 | 4–6 |
| Odd-degree islands | 0 (a circuit: start anywhere) | 2, one of them a doorway island | 2, neither a doorway island |
| P_rand (random trail without re-crossing lights everything) | ≥ 40% | 10–40% | 2–10% |
| Broken span inside the lit network (it counts once mended) | 0 | 0 | 0–1 |
| Spread: share of lattice spots used | ≥ 50% | ≥ 50% | ≥ 50% |
| Red herrings (whole planks off the lit network, e.g. a lookout) | 0 | 1 | 1–2 |
| Retry cost (walk to any start island after a reset) | ≤ 5 s | ≤ 8 s | ≤ 12 s |

The spread row keeps the islands spread out, which is what the user liked.

### 1b. Sliding ice (Earth, 4.10)

**Examples.**

| Game | What it is | Source |
|---|---|---|
| Pokémon G/S/C, Ice Path | Slide until hitting a rock or wall. Crystal's solutions are 5, 8 and 12 slides; HG/SS's are 3–4, 7 and 8. | [W: [StrategyWiki](https://strategywiki.org/wiki/Pok%C3%A9mon_Gold_and_Silver/Ice_Path), [lparchive](https://lparchive.org/Pokemon-Crystal-(by-Crosspeice)/Update%2041/)] |
| Pokémon, Mahogany Gym (Pryce) and Snowpoint Gym (Candice) | Gym-floor ice sliders. Snowpoint adds snowballs and snow tiles that stop you, which act as grip. | [W: [Mahogany](https://pokemon.fandom.com/wiki/Mahogany_Town_Gym), [Snowpoint](https://www.thonky.com/pokemon-brilliant-diamond-shining-pearl/snowpoint-gym)] |
| Ocarina of Time, Ice Cavern | A pushed ice block slides until it hits something. Pushing it off a ledge respawns it at the entrance, a model reset. | [W: [StrategyWiki](https://strategywiki.org/wiki/The_Legend_of_Zelda:_Ocarina_of_Time/Ice_Cavern)] |
| Generators | BFS over stop cells: `minsteps`, `minjump` and `blockratio` (for example 0.3 on 10×10) | [W: [Ohohcakester](https://github.com/Ohohcakester/Ice-Sliding-Puzzle/blob/master/IceSlide/Manual.txt), [jwang5675](https://github.com/jwang5675/Ice-Puzzle-Generator), [Aycblok](https://github.com/mpewsey/Aycblok)] |

**Fair or fun when:**
- every stop is predictable (tile-snapped, four directions);
- the goal is visible;
- wrong slides land somewhere new rather than nowhere;
- a pit costs a few seconds, not a life.

**Frustration traps:**
- off-grid physics, where the stop depends on sub-tile position;
- slides so long that a mistake replays 20 s of input;
- pits on the only route, so precision outweighs thinking;
- spaces with no way back (hard traps);
- a single huge open field with almost no rocks, which makes for aimless wandering.

**Targets.** S = stop cells: cells where the player can come to rest.

| Metric (solver) | Early | Mid | Late |
|---|---|---|---|
| Ice area (cells) | ≈12×8 | ≈16×10 | ≈20×12 (max 21×12) |
| Min slides, entry → goal (BFS over stop cells) | 3–5 | 6–9 | 10–13 |
| Reachable stop cells | 8–15 | 15–30 | 25–45 |
| Mean out-degree (distinct new stops per stop) | 2.0–2.6 | 2.2–3.0 | 2.5–3.2 |
| Detour factor (tiles slid on the shortest solution ÷ Manhattan distance entry → goal) | 1.2–2.0 | 2.0–3.5 | 3.0–5.0 |
| Rock density inside the ice area | 6–10% | 9–13% | 12–16% |
| Pits | 0–1 | 2–4 | 4–7 |
| Pit-reset states (stops whose only exits lead to pits) | 0 | ≤ 2 | ≤ 4 |
| Hard traps | 0 | 0 | 0 |
| Grip tiles (excluding landings) | 3–5 | 2–3 | 0–2 |
| Shortest solutions (count of distinct optimal move strings) | any | ≤ 6 | ≤ 3 |
| P_rand (random slides, cap 3× min) | ≥ 50% | 15–50% | 3–15% |
| Red herrings (a stop within 3 tiles in a straight line of the goal but ≥ 3 slides from it) | 0 | 1 | 1–2 |
| Retry cost (slides from the entry to the deepest pit on the best route) | ≤ 3 | ≤ 5 | ≤ 7 |
| Longest single slide | ≤ 12 tiles | ≤ 15 | ≤ 18 |

### 1c. Wind streams (Earth, 4.11)

**Examples.**

| Game | What it is | Source |
|---|---|---|
| Pokémon spin tiles (Rocket Hideout and others) | Carried in the arrow's direction until a wall, a stop tile or another arrow. Gen 1 always ends on a stop tile. | [W: [Bulbapedia Spin tile](https://bulbapedia.bulbagarden.net/wiki/Spin_tile)] |
| Pokémon currents (Hoenn routes 132–134, Seafoam Islands) | One-way currents. Seafoam has you push boulders to block the current. | [W: [Route 134](https://bulbapedia.bulbagarden.net/wiki/Hoenn_Route_134), [Seafoam](https://bulbapedia.bulbagarden.net/wiki/Seafoam_Islands)] |
| Minish Cap, Palace of Winds | Fans and wind streams carry Link over gaps; a switch starts a fan | [W: [thonky](https://www.thonky.com/zelda-minish-cap/palace-of-winds)] |
| Phantom Hourglass, Temple of Wind | Gusts and geysers: avoid them or ride them for a boost | [W: [Zelda Wiki](https://zelda.fandom.com/wiki/Temple_of_Wind)] |
| Oracle of Ages, Jabu-Jabu | Water-level switches reroute the map, a "toggle changes the graph" puzzle | [W: [StrategyWiki](https://strategywiki.org/wiki/The_Legend_of_Zelda:_Oracle_of_Ages/Zora_Seas_and_Jabu-Jabu's_Belly)] |
| ALttP, conveyor floors (Ganon's Tower and others) | Conveyors that push but can be walked against | [W: [GameFAQs](https://gamefaqs.gamespot.com/boards/588436-the-legend-of-zelda-a-link-to-the-past/61655677)] |

**Fair or fun when:**
- the arrows show the direction and the whole path of every stream;
- rides are short (2–4 s);
- the stream visibly ends on safe ground;
- the one-way nature is the puzzle: you plan which current takes you where.

**Frustration traps:**
- streams that dump you far from where you were with no return;
- partial-control drift, where the result depends on how you held the stick;
- long rides repeated after every mistake;
- streams pushing the player into walls (already banned in 1.20).

**Targets.** R = floor regions: walkable areas connected without riding.

| Metric (solver) | Early | Mid | Late |
|---|---|---|---|
| Floor regions R | 3–5 | 5–8 | 7–11 |
| Streams | 3–5 | 5–8 | 7–11 |
| Min rides, entry → goal | 2–3 | 3–5 | 5–7 |
| Decoy streams (lead to a region off every shortest route) | 0–1 | 1–2 | 2–3 |
| Return cost from any reached region to the entry (rides) | ≤ 1 | ≤ 2 | ≤ 3 |
| Hard traps (regions with no way back to a doorway) | 0 | 0 | 0 |
| Fan switches (toggle a stream on or off; state = region × mask) | 0 | 0–1 | 1–2 |
| P_rand (random rides, cap 3× min) | ≥ 50% | 15–50% | 3–15% |
| Longest single ride | ≤ 10 tiles | ≤ 14 | ≤ 18 |
| Doorway pairs mutually reachable (both directions) | all connected pairs | all | all |

### 1d. Boulders and timed doors (Physics)

**Examples.**
- Pokémon Strength boulders push one tile at a time, and the puzzle resets when you leave the area. Seafoam drops boulders down holes to block currents [W: [Seafoam](https://bulbapedia.bulbagarden.net/wiki/Seafoam_Islands)].
- Zelda block rooms let you push (and in Ocarina, pull) blocks; leaving the room resets them [K].
- Sokoban research uses **box lines** as the difficulty measure: one straight push run counts as 1, so long hallway pushes add tedium, not difficulty (Taylor & Parberry, [Procedural Generation of Sokoban Levels](https://ianparberry.com/pubs/GAMEON-NA_METH_03.pdf) [W, summary via search; PDF 403]).
- Ocarina of Time's timed torches and switch-then-run doors [K].

**Fair or fun when:**
- dragging works both ways (the mitt), so there are no deadlocks;
- the timer is visible (1.10 has a bar);
- one boulder short fails clearly, with a visible margin.

**Frustration traps:**
- timing that only works with perfect keyboard play, a risk for pre-teens;
- long drag distances (tedium);
- boulders stuck behind an NPC or an enemy;
- a 24×15 rescale that stretches the switch-to-door run without retuning.

**Targets:**

| Metric (solver) | Early | Mid | Late |
|---|---|---|---|
| Required boulders | 1 | 2 | 3 |
| Box lines: straight drag runs to place all required boulders | 1–2 | 3–4 | 5–7 |
| Total drag distance (tiles) | ≤ 10 | ≤ 20 | ≤ 30 |
| Longest single drag | ≤ 8 | ≤ 10 | ≤ 12 |
| Spare (decoy-free) boulders | 1 | 0–1 | 0 |
| Timing slack at the required count (open time ÷ switch-to-threshold walk) | ≥ 1.35 | 1.20–1.30 | 1.12–1.20 |
| One short shuts early by at least | 15% | 12% | 10% |
| Boulders unreachable or useless | 0 | 0 | 0 |
| Trial: boulders shared between doors (box lines ≥ 4) | — | 2 booths | 2–3 booths |

### 1e. Gate puzzles (beak, stones, gap, weather wand)

**Examples.**
- Zelda bomb walls, hookshot targets and torches [K].
- Pokémon HM obstacles (Cut, Rock Smash, Strength) [K].
- Ordering puzzles: Minish Cap and Ocarina switch and torch rooms. The order is either given by a clue elsewhere in the room or timed [K].

These are **locks, not puzzles**: the decision is "have item → swing". That is fine for metroidvania pacing.

**Fair or fun when:**
- the gate's look names its key, the hint system's job;
- one or two hits open it.

**Frustration traps:**
- 3+ hits per gate (a grind);
- the same gate repeated with no twist.

**Targets:**

| Metric | Early | Mid | Late |
|---|---|---|---|
| Hits to open | 1 | 1–2 | 2 |
| Gates with a positional twist (aim from a spot you must reach, or a cross-screen effect like the flower → vine) | 0–20% | 25–40% | 40–60% |
| Steps between the gate and its trigger (button, flower) when split | 0 | 1 screen | 1–2 screens |

### 1f. Trials and ordering puzzles (every face's trial)

The ordering puzzle gets its difficulty from **how indirectly the order is clued**.

**Clue levels:**
- **0:** numbers are on the targets (pure execution).
- **1:** the order is shown elsewhere on the screen, for example on a mural, a statue or an NPC's line.
- **2:** the order follows from a rule, for example smallest shape to largest, or the die pips of the face.

**Targets:**

| Metric | Early | Mid | Late |
|---|---|---|---|
| Sequence length | 3 | 4 | 5–6 |
| Clue level | 1 | 1–2 | 2 |
| Wrong step | Resets the sequence (no damage) | same | same |
| Optional timer (lamps relight within T seconds) | none | none | T ≥ 1.5× the best route |

Trials sit one tier above their face. A trial on a late face uses the late column.

## 2. Recommended rule for Math island screens (4.12)

**Rule: "Light every plank".**
- On the reward network, every whole plank lights (turns face-yellow) the first time it is crossed.
- Crossing a lit plank again snuffs every plank. They all go dark, including that one, with a red flash and a fizzle.
- When every plank on the network is lit, the lamps run to the **reward roadblock** on that network's reward island, and it opens with the 1.15 effect.

This is an Euler trail, the Minish Cap Dark Hyrule Castle tiles moved onto bridges, and Layton's one-line puzzles made walkable. Reject Hashi as the main rule: it is a pen-and-paper deduction puzzle that needs a bridge-building interface and does not use the island walking the user liked. It could be a later trial variant (swing at a plank spot to build or remove it, with island numbers).

**Why this rule:**
- It is the user's own example: "bridge changing colour".
- It keeps spread-out islands and the doorway route-finding from 1.25.
- It has one learnable "aha": parity tells you where to start.
- The state is fully visible, and a reset is instant and free.

**Start anywhere.** Snuffing everything (not "everything except the re-crossed plank") means the player can make any island a clean start. Step out over a plank and step back: the second crossing snuffs all, and you stand on the island you wanted. No floor switch or flag island is needed, and every connected doorway is a valid entry. That satisfies 4.12's "solvable from each connected doorway".

**What counts:**
- **Whole planks** on the reward network count.
- **Broken spans** do not count before they are mended. Once mended, a span turns gold and is access only: crossing it never lights or snuffs. The one exception is the late knob, where a mended span counts as a normal plank.
- **Lookout planks** on the lone doorway's side are their own tiny network with nothing to light.
- **Accidental crossings.** A crossing counts only if the player enters the plank span from one island and leaves onto the other. 1.25's sampling rule stays, and lanes and doorway landings never count.

**Checking (solver):**
1. The reward network is connected over whole planks, plus mended spans when that knob is on.
2. The number of odd-degree islands is 0 or 2.
3. From every doorway whose island joins that network (using the gap-crosser* if the sweep holds it), every odd island is reachable when there are 2, or any island when there are 0.
4. Enumerate Euler trails by DFS (E ≤ 16) for P_rand and the trail count.
5. Doorway reachability (`DoorwayReach`) is unchanged, so the roadblock's reward feeds ItemPlacement and the sweep as a region on that island.

**Reset rules:**
- Leaving the screen through a doorway snuffs everything; the state is not saved per screen.
- A solved network stays solved for the run: planks stay lit and the roadblock stays open.

**Feedback.** This answers the user's "indication that it is activating something".

| Event | What the player sees and hears |
|---|---|
| Plank crossed | It goes from dark wood to glowing face-yellow, with a rising chime |
| Island complete (all its planks lit) | The island rim glows. This hints at parity without any text. |
| Progress | The roadblock carries one lamp per plank and fills as planks light |
| Re-cross | Every plank flashes red, then dark, with a fizzle; lamps empty |
| Solved | A light chain runs from the last plank to the roadblock, which opens |

**Text.** Under the subtle-science rule, InWorldText bans "exactly once". Shape people say something like: "A plank walked twice puts out every lamp. Odd islands make good beginnings." Check the wording against InWorldText.

**Knobs.** These match the table in §1a.
- E and V.
- Cycles (E − V + 1).
- Odd islands (0 / 2, at a doorway or not).
- P_rand band.
- A broken span inside the network (late).
- Red-herring planks.
- Lattice spread.
- **Fix for the "impossible" island:** make the lone doorway readable. Its island holds an NPC or sign ("the planks from here are gone; come round by another side") or a small coin roadblock of its own.

## 3. Generator recipes

### 3a. Sliding ice (4.10)

**Model:**
- **Grid.** Tiles are ice, rock, pit, grip or goal pad. Doorway landings are 2×2 grip. The player is tile-snapped on ice and moves in four directions. A slide runs at about 1.5× walk speed with no steering mid-slide; attack swings are disabled while sliding.
- **Stops.** A slide stops before a rock or wall, or on a grip tile or the goal pad.
- **Pits.** A pit makes the player fall: a short animation, a respawn on the entry landing, 0 damage.
- **Reward.** The goal pad sits in front of the reward roadblock.

**Generation.** Seeded on a new RngStreams entry.
1. **Frame.** Choose the ice area by tier (§1b) inside 21×12, centred. Doorway landings are fixed. Place the goal pad at Manhattan distance ≥ 0.5×(W+H) from the primary entry landing.
2. **Path-first, reverse construction.** From the goal pad, walk backwards K = target min slides + 1 steps. Each step picks a direction and a length of 2–(max slide). Put a rock just past each stop cell, or reuse a wall, rock or grip. Mark the carved cells *protected*, so no rocks or pits go on them.
3. **Fill.** Scatter rocks to the tier density on unprotected cells. Rocks must not sit on a doorway landing or in a lane mouth.
4. **Pits.** Place pits on unprotected cells where a wrong slide from a solution stop would pass or end. Pits punish wrong choices, never the route.
5. **Grip.** Add grip tiles by tier on unprotected cells that sit where many slides cross.
6. **Verify.**
   - Run a forward BFS over stop cells from **every connected doorway landing** to get min slides, reachable stops, out-degree, detour factor and P_rand.
   - Run a backward BFS on reversed edges from {goal, every landing}. A reachable stop outside it that cannot fall into a pit is a **hard trap**, and the screen is rejected.
   - Check that every pair of connected doorways is mutually reachable.
   - Check that the crossing costs ≤ 0.5× the reward route.
7. **Accept or repair.** Accept if every metric is inside its band. Otherwise mutate one rock or pit and re-verify, up to 200 times. Then re-seed, up to 20 times. Then fall back to a hand-made template from a small library (3 per tier). The sweep reports how often the fallback is used.

**Knobs:** area, K, density, pits, grip count, max slide length, the P_rand band, and the red-herring count.

### 3b. Wind streams (4.11)

**Model.**
- The screen is floor regions on a chasm (sky), with walls and stream lanes.
- A stream tile carries the player in its arrow's direction at about 2× walk speed. There is no steering and no fighting it, the Pokémon spin-tile model, so the solver is a clean graph.
- A ride ends on the first non-stream tile (floor) or on an **anchor** (a grip tile inside a stream), where control returns.
- **Bends.** A stream tile can bend, so one stream can turn up to 2 times.
- **Rules:**
  - Streams never end against a wall or over the chasm.
  - Walking into a stream from the side starts a ride.
  - The player cannot enter the chasm on foot (an edge collider).
  - No falls are possible.
- **Fans (mid/late).** A swing at a fan switch toggles its streams; the solver state is (region, fan mask).

**Generation:**
1. **Regions.** Place R regions by tier on a coarse lattice (like the island lattice), sized 2×2 to 4×3, with doorway regions fixed. Use the chasm as the background.
2. **Route first.** Pick a region sequence entry → … → goal of length = target min rides. Draw one stream per hop: straight or one bend, not crossing other streams, each ≤ the max ride length.
3. **Returns.** Add return streams until every region can reach a doorway region within the return-cost band. Shared returns are allowed.
4. **Decoys.** Add decoy streams by tier into side regions. Each must have a return.
5. **Fans (mid/late).** Fans gate one route stream. The switch sits in a region reachable before that stream.
6. **Verify.** Run a BFS over (region, mask) from every connected doorway to get min rides, decoys, return cost, P_rand and hard traps (must be 0). Also check that all connected doorway pairs are reachable both ways.
7. **Accept or repair.** Use the same loop as ice: mutate one stream, re-seed, then fall back to a template.

**Knobs:** R, the stream count, min rides, decoys, max ride length, fans, and the return-cost band.

The wind screen reads like a sky version of the Math islands. That is a nice echo, but give it its own art: clouds and arrows, not planks.

## 4. Existing puzzles: one-line takeaways

| Puzzle (story) | Takeaway | Verdict |
|---|---|---|
| Math island screens (1.25) | Route-finding only: 1–3 planks per route and no decision. The isolated doorway reads as "impossible". | **Too easy as a puzzle**, and unreadable at the lone doorway. Fix with §2. |
| Math bridge door (1.19, removed) | A 6-plank Euler trail with a flag start is early-tier difficulty. The user rejected it for its compact layout, not for the rule. | About right for early. Reuse the rule, spread out. |
| Math trial (flag behind a broken span) | Walk there with the gap-crosser*. | **Too easy.** Make it a late-tier §2 network, or the Hashi variant. |
| Gap gates | One swing with the item. | A lock, fine. Not a puzzle. |
| Beak gates (rock, pot, button, flower → vine) | 1–2 hits. Flower → vine across screens is the one real twist. | Locks, fine. Use the flower-style twist more at mid and late. |
| Stone gates (lamp, cracked wall, plate) | One swing with the rolled stone. | A lock, fine. |
| Weather wand gates | One swing with the rolled phenomenon. | A lock, fine. |
| Biology trial (rocks 1-2-3; buttons then flower) | Clue level 0, length 3. | **Too easy.** Raise to clue level 1–2 and length 4–5. |
| Chemistry trial (3 numbered targets in order) | Clue level 0, length 3. | **Too easy.** Same fix. |
| Earth trial (4 targets of the rolled kind) | No order, so just execution. | **Too easy.** Add an order with a clue, or move it onto an ice or wind screen. |
| Physics timed doors (1.10) | Required 1–3 boulders, ±10% timing margin, drag both ways. | About right for mid, but **too tight for early** with keyboard pre-teens. Use the §1d slack and retune after the 24×15 rescale. |
| Physics trial (2 booths sharing 2 boulders) | A real sequencing puzzle, box lines ≥ 4. | **About right** (mid to late). |
| Earth snow patches and wind bands (1.20) | Hazards with no decisions. | Replaced by 4.10 and 4.11. |
| Freezing rain and crevices (1.20) | Movement texture, with crevices flood-checked. | Fine as hazards. Keep them off the ice and wind puzzle screens. |

## Sources

- Pokémon Ice Path: [StrategyWiki G/S](https://strategywiki.org/wiki/Pok%C3%A9mon_Gold_and_Silver/Ice_Path), [StrategyWiki HG/SS](https://strategywiki.org/wiki/Pok%C3%A9mon_HeartGold_and_SoulSilver/Ice_Path), [LP archive Crystal](https://lparchive.org/Pokemon-Crystal-(by-Crosspeice)/Update%2041/)
- Pokémon gyms: [Sootopolis Gym](https://bulbapedia.bulbagarden.net/wiki/Sootopolis_Gym), [Cracked tile / Hole](https://bulbapedia.bulbagarden.net/wiki/Cracked_tile), [Mahogany Gym](https://pokemon.fandom.com/wiki/Mahogany_Town_Gym), [Snowpoint Gym](https://www.thonky.com/pokemon-brilliant-diamond-shining-pearl/snowpoint-gym)
- Pokémon currents and spin tiles: [Spin tile](https://bulbapedia.bulbagarden.net/wiki/Spin_tile), [Route 134](https://bulbapedia.bulbagarden.net/wiki/Hoenn_Route_134), [Seafoam Islands](https://bulbapedia.bulbagarden.net/wiki/Seafoam_Islands)
- Zelda: [Minish Cap Dark Hyrule Castle](https://www.zeldadungeon.net/the-minish-cap-walkthrough/dark-hyrule-castle/), [Zelda Wiki: Tile](https://zelda.fandom.com/wiki/Tile), [Palace of Winds](https://www.thonky.com/zelda-minish-cap/palace-of-winds), [Temple of Wind](https://zelda.fandom.com/wiki/Temple_of_Wind), [OoT Ice Cavern](https://strategywiki.org/wiki/The_Legend_of_Zelda:_Ocarina_of_Time/Ice_Cavern), [Oracle of Ages Jabu-Jabu](https://strategywiki.org/wiki/The_Legend_of_Zelda:_Oracle_of_Ages/Zora_Seas_and_Jabu-Jabu's_Belly), [Spirit Tracks ToS](https://www.zeldadungeon.net/spirit-tracks-walkthrough/tower-of-spirits-4/)
- Graph puzzles: [Layton One-Line Puzzle 1](https://layton.fandom.com/wiki/Puzzle:One-Line_Puzzle_1), [EulR](https://play.google.com/store/apps/details?id=com.eulr.puzzle), [Hashiwokakero generator/solver](https://github.com/erthium/hashiwokakero)
- Generators and metrics: [Ohohcakester Ice-Sliding-Puzzle manual](https://github.com/Ohohcakester/Ice-Sliding-Puzzle/blob/master/IceSlide/Manual.txt), [jwang5675 Ice-Puzzle-Generator](https://github.com/jwang5675/Ice-Puzzle-Generator), [Aycblok](https://github.com/mpewsey/Aycblok), [Taylor & Parberry, Sokoban](https://ianparberry.com/pubs/GAMEON-NA_METH_03.pdf)
- Design: [GMTK "What makes a good puzzle" discussion](https://www.resetera.com/threads/what-makes-a-good-puzzle-game-makers-toolkit.29537/), [How to make a good puzzle](https://www.gamedeveloper.com/design/how-to-make-a-good-puzzle---an-explorable-explanation), [Red herrings devlog](https://marcosd.itch.io/dis-pontibus-2/devlog/228271/red-herrings)
- Numbers marked as knowledge, not fetched [K]: Zelda block and torch rooms, The Witness, Pokémon HM obstacles.
