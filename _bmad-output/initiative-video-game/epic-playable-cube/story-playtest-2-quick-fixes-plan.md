---
title: 'Playtest 2 quick fixes'
type: 'bug'
ticket: '24'
created: '2026-10-07'
status: 'built'
baseline_revision: '89be8df'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/faces-and-races.md'
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/lore-and-tone.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The user's second playtest (2026-10-07) asked for "the smaller faster fixes first". The quick ones are:
- **Two-way connections:** "the screens don't connect in both directions".
- **Start screen:** "Start should be its own face and it should be in the middle of the face, with some town people around".
- **"Entropy" in text:** "We can leave entropy in text".
- **Floors:** "Add a bit more texture to the floors". "blue face texture is a bit noisy".
- **Fake items:** "Some things look like objects but cannot be collected. A key in the wildlands for example".
- **Math bridges:** "The bridges in the math face are too close together, I would like them to connect actual islands, either over water or crevices". "Gap crosser can cross a single grid square… make some of the bridges broken… or have bridges on islands that are split by a crevice".

**Approach:**
1. **Two-way connections.** Find and fix why walking off a screen edge and straight back doesn't return the player to the screen they came from. Cover the within-face, face-to-face and rotated-edge cases.
2. **Start screen.** The run starts on the Town face's centre screen, with Town people around the start position.
3. **"Entropy" in text.** Remove "entropy" from the banned words. Where 1.22 replaced it, restore the earlier wording; the user will pick an in-game term later.
4. **Floors.** Add subtle texture to every region floor (pattern or tile variation). Calm the Weightlands* (Physics, blue) floor.
5. **Fake items.** Remove or replace every decor tile that reads as a collectible (keys, coins, potions, chests, gems…) on all faces.
6. **Math bridges.** Bridge-door islands are real, separate land masses with water or crevice tiles between them, and bridges are visibly spaced apart. Some bridges are broken (a one-square gap) or cross a one-square crevice that only the Gap-Crosser* spans. The bridge puzzle stays solvable.

## Boundaries & Constraints

**Always:**
- **Connections:** stepping off any edge and immediately back returns to the same screen, face and edge. An EditMode test over every screen and edge at N=2 and N=3 checks this; a PlayMode test walks a face crossing and back.
- **Solvability:** the seed sweep stays 50/50 with no softlocks, Math rules included. Re-record determinism only where a fix intentionally moves content (start cell, Math layouts), with a note.
- **Readability:** floor texture stays low-contrast. The 1.15/1.22 contrast and readability tests keep passing.
- **Text rules:** `InWorldText` stays the single banned-word source; scientist names stay banned.
- **Testing:** PlayMode runs in class batches with `-nographics`, in the foreground.

**Never:**
- No edge-gate redesign, temples, per-face towns, new puzzles, whole-screen weather, screen or character scaling, or character redesign. Those go to the course correction.
- No boss tuning.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Round trip | Any screen and edge, N=2,3 | Step out, then back, returns to the origin screen | Test fails |
| Run start | New run, any seed | Player on Town centre (1,1) with townsfolk nearby | No error expected |
| Text | "entropy" in a line | Allowed; scientist names still flagged | Scan test |
| Decor | Any face | No collectible-looking decor | By-eye check plus an art-key test |
| Bridges | Math bridge door, seeds 1–50 | Islands separated by water or crevice; some one-square breaks; solvable | Sweep fails |

</frozen-after-approval>

## Code Map

- Connections: `CubeModel.TryStep`/`Step`, `CubeNavigator` (edge detection, entry position or facing mapping across rotated edges), `CubeWorld` screen placement.
- Start: `CubeNavigator`/`RunLoop.StartRun` start cell, `TownPopulation` placement, `SeedSweep` start, `CubeMap` Town start marker.
- Text: `InWorldText.cs` (banned list), the Order/Mixing meter label (`Core/CoreHud.cs` or `CubeDebug`), text scan tests.
- Floors and decor: `Art/ArtCatalog.cs`, `Editor/ArtCatalogBuilder.cs`, `Editor/ModuleLibraryBuilder.cs`, `Assets/Art/ART-MAP.md`; the Earth decor that uses a key tile.
- Math: `Faces/Math/MathPlan.cs` (islands, bridges, BridgeWalk), `BridgePuzzle`, `GapGate`, sweep Math rules.

## Tasks & Acceptance

**Execution:**
- [x] Two-way edge fix, with EditMode and PlayMode round-trip tests.
- [x] Start on Town centre with townsfolk around.
- [x] Unban "entropy" and restore the pre-1.22 wording.
- [x] Floor texture pass, with a calmer Physics floor.
- [x] Remove collectible-looking decor.
- [x] Math islands over water or crevice, spaced bridges, some broken spans for the Gap-Crosser*.
- [x] Regenerate catalogs and scene; re-record determinism with a note if content moved.
- [x] Verify: EditMode, PlayMode batches, sweep.

**Acceptance Criteria:**
- Given the suites and sweep, when run, then all pass.
- Given the scene (HITL), when the user plays, then edges return both ways, the start is centred in Town, floors look textured but calm, nothing fake looks collectible, and Math bridges cross real gaps.

## Implementation Notes

- Checkpoint 1 approved under the user's standing instruction (2026-10-07). The user asked for quick fixes first and will check before the end of the day.
- **Two-way connections: root cause.** `CubeModel.Step` and `CubeNavigator` were geometrically correct. A new PlayMode diagnostic walked off every edge screen of every face and back with the reverse of the entry facing, and it passed; a within-face walk over every screen pair also passed. The bug was in the controls. Each face is drawn in a fixed orientation, so 12 of the 24 face-edge exits arrive turned: the edges of Right, Left, Top and Bottom are quarter-turned, and Back↔Top and Back↔Bottom are half-turned. Two things went wrong:
  - **Half-turned edges never connected.** Holding Up off Back's north edge arrived on Top walking south. The still-held Up then walked straight back off the same edge on the next physics step, so the player bounced between the two faces.
  - **The reverse key didn't lead back on turned edges.** For example, after going Up from Right onto Top, pressing Down walked along Top instead of returning.
- **Two-way connections: fix.** `EdgeInputFrame` (pure) plus `IMoveInputFrame` (a hook in `PlayerMover`, implemented by `CubeNavigator`). After a crossing, the raw input is turned by that crossing's quarter turns, so:
  - the held key keeps walking the same way into the new face;
  - the reverse key walks straight back over the same edge, where the opposite turn cancels the frame.

  The turn is kept per key (review patch): every key held at the crossing keeps the turn until it is released, and the reverse of the crossing key takes it when pressed. Any other fresh key walks as drawn. Controls return to normal once no turned key is held and either a fresh non-reverse key is pressed or 0.6 s of game time passes with nothing held. They also reset when the mover stops feeding the frame for 0.6 s (frozen or disabled), when the map opens (`GamePause.Paused`), and on any teleport. The model, the navigator geometry and the map are unchanged.
- **Two-way connections: tests.**
  - EditMode (`CubeModelTests`), at N=2 and N=3:
    - every screen with every key, then the reverse key, returns to the same screen with normal controls (12n turned exits);
    - holding the key across every edge never leaves the new face again;
    - the frame resets after a release, a turn or a teleport.
  - PlayMode (`CubeSceneTests.EveryFaceEdge_CrossAndStraightBack_ReturnsToTheSameScreen`): real keys over all 72 edge screens. It crosses, keeps holding for 0.25 s (no re-crossing), then walks back with the reverse key to the same screen, facing and normal controls.
- **Start.**
  - `CubeModel.StartScreen` is now Town's centre: `CubeModel.StartCell(n)` = ((n-1)/2, (n-1)/2), which is (1,1) at 3x3 and stays (0,0) at 2x2.
  - The sweep's BFS, the map's start marker, `VisitTracker`, F8 and new runs all read `StartScreen`.
  - `CubeSceneBuilder` places the player there.
  - `TownPlan.Spots` puts the first `StartScreenNpcs` = 4 Town NPCs (the greeter first) on the start screen. The rest go round the other screens, and `TownTests` checks it.
- **Text.**
  - "entropy" is off `InWorldText.BannedWords`. Scientist names are still banned, and a test asserts both.
  - `CoreHud` is back to the pre-1.22 wording: "Order ↔ Entropy" and "Entropy wins! The Partition cracks… everybody mingle!". 1.22 replaced only these two strings (checked with `git diff 8be3e09 a414268`).
- **Fake items.** The Earth fossil at each quake crevice was Tiny Town 117, a key. Two Town ruin pieces also read as pickups (D 61, a framed crate; D 64, a mallet-shaped post). New tiles:
  - Fossil: T 92 (a dug-up mound);
  - RuinTile: T 122 (a flagstone slab);
  - RuinBroken: D 59 (a column stub).

  A new test (`ReadabilityTests.DecorTiles_NeverLookLikeSomethingToPickUp`) keeps ruins, floors, walls, hazards, the fossil, islands and goal pads off a list of item-looking tiles: keys, coins, bags, chests, potions, flasks, gems, tools, weapons and wands. `ART-MAP.md` is updated.
- **Floors.**
  - `FloorPattern` overlays every region floor with one texel per tile. The overlay sprite covers exactly the 0.98 floor block from its bottom-left corner, where Unity starts the floor's tiling, so every texel sits on one tile (checked with a zoomed render). The shading comes in soft 3-tile patches plus the odd tile picked out, at most 7% black (half that as white), less on Town and the Weightlands*.
  - The shade is a hash of theme, screen and tile, so it is deterministic and touches no RNG stream. It is drawn on the floor's sorting order, nudged toward the camera.
  - The Weightlands* floor tile is now D 37 (fine deck plates) instead of T 76, whose dark notches and borders were the noise. It also gets a 50% glaze of its own average colour, which halves the line contrast without changing the colour.
  - The 1.15/1.22 contrast tests pass. New tests cover the pattern bounds and the glaze colour.
- **Math bridges: layout.**
  - Islands are bigger (r 0.55), on a wide lattice: 2.4 between columns and 2.1 between rows, with a compact fallback of 2.0 x 1.9 and a last-resort tight lattice of 1.85 x 1.85. The tight lattice was added because the N=2 sweep had no room for the trial on 6 seeds. Every bridge spans at least 0.75 of water.
  - A layout takes one half of its screen (three columns each side of the north-south lane, two rows). Its innermost bridges cross the north-south lane straight over it; planks are floor decoration, so the lane stays walkable and the lane-walk test still sees no walk start or reset.
  - Water is drawn as a pool round each island plus a channel under each bridge, so islands read as separate land masses. No channel is drawn inside the north-south exit lane under a lane-crossing bridge, so the lane reads open.
- **Math bridges: broken bridges.**
  - In each run the first door with an eligible bridge gets a broken bridge; other doors get one on a seeded coin flip. An eligible bridge is long enough (`CanBreak`) and doesn't cross the lane.
  - A broken bridge has a one-square gap (0.9) with the gap-crosser*'s mark. Swinging the Gap-Crosser* at it (`BrokenSpan`, a trigger `IAttackReceiver`) lays the plank, and only then does the bridge count. Reaching the far island over an unmended gap resets the walk (`BridgeWalk.Result.Broken`, with a popup).
  - Water and the gap are not solid, the same as before. Wading resets the walk, so the puzzle still can't be cheated.
- **Math bridges: sweep rules.** In `MathPlan.Problems`:
  - a door with a broken bridge opens only once the Gap-Crosser* is held;
  - broken bridges must be eligible;
  - the trial never has one.

  ItemPlacement already treats every Math gate as needing the Math item, so this adds no new softlocks. The sweep report now counts doors with a broken bridge. New `MathTests` cover broken walks, the island spacing and how often doors are broken, and the softlock rule. `MathSceneTests` checks that a broken door's unmended walk fails, then swings the Gap-Crosser* at each gap with the real attack and walks it.
- **Regenerated:**
  - the art catalog (decor and Physics floor tiles);
  - the module library (ruin sprites are baked into the prefabs; the diff is regenerated file IDs);
  - the Cube scene (player start position).
- **Determinism re-recorded.** Fields that changed:
  - `model` (50 seeds): `start=` is now Front (1,1);
  - `mathPlan` (50 seeds): the new lattice and layouts, plus `broken k` in door layout signatures;
  - `physicsPlan` (5 seeds): Physics keeps its boulders and switches off Math content marks, which moved with the layouts. Positions moved, and on a few doors the boulder counts changed too: seed 7's off-home timed door at Bottom (0,0) went from "needs 2 (+0 spare)" to "needs 3 (+1 spare)". The sweep still proves every door has enough reachable boulders.

  No other field changed. Town NPC spots are not in the fixture.
- **Skipped / for the course correction:** solid water or crevices. A solid moat would cut the exit lanes and edge band that every module keeps open, so islands stay wadeable as before (wading resets the walk).

## Review Triage Log

Independent review (2026-10-07), triaged by the coordinator.

| Verdict | Route | Finding | Outcome |
|---|---|---|---|
| medium | patch | `EdgeInputFrame` reset on a perpendicular key, which flipped held keys: release Up after Up+Right across Back→Top and Right turns 180°; a sidestep then Up brings the bounce back | Patched. The turn is now per key: keys held at the crossing keep it until released, the reverse key takes it, and other fresh keys walk as drawn. New EditMode test `InputFrame_HeldKeysKeepTheTurnUntilReleased_…` covers both cases. The PlayMode round trip now holds the key continuously through the crossing. |
| medium | patch | The frame survived a map pause or a frozen mover (the grace only ticked in FixedUpdate) | Patched. The frame resets on `GamePause.Paused` (new event, raised when the map opens), when `Map` isn't called for 0.6 s of game time, and in `CubeNavigator.OnDisable`. Test: `InputFrame_EndsWhenTheMoverStopsFeedingIt_OrTheMapPausesTheGame`. |
| nit | patch | Unused `Map(Facing)` overload | Removed. |
| low | patch | The determinism note left out the boulder-count changes | The note now says counts changed on a few doors (seed 7, Bottom (0,0): 2 to 3). |
| low | patch | Floor pattern texels (0.98) straddled tiles | Patched. The sprite rect is now 15.68 x 9.8 of a 16x10 texture at 1 ppu, matching the floor block and its bottom-left tiling origin. A test checks the rect and size; a zoomed render confirms it by eye. |
| low | patch | The decor ban test missed the TrialLamp, Particle, DoorSwitch, Portal and gate keys | Extended (all gate-category keys included). No key uses an item-looking tile. |
| low | patch | Water channel cut across the north-south lane | Patched. Lane-crossing bridges now get two channel pieces outside the lane. `MathSceneTests` asserts no channel overlaps the lane. |
| low | reject | Town NPC distribution | At N=3 there are 16 to 20 Town NPCs, so the other 8 screens still get some. |

Also during verification:
- `CubeMapSceneTests` still assumed the start was (0,0) and walked to (1,0). It now walks to the screen east of `StartScreen`.
- One `EarthSceneTests` run had a Physics NPC that wandered 0.004 inside the 0.3 tolerance of a gate-front keep-out. NPCs wander their quadrant, so this check depends on timing. It didn't reproduce on the rerun and isn't touched.

## Verification

**Commands:**
- `verify.sh <tag>` in the scratchpad: EditMode, six PlayMode class batches, sweep.

**Result (2026-10-07, `verify.sh r124c`, after the review patches):** EditMode 257/257; PlayMode batches 25/23/26/25/35/24, all passing; skill checks Victory (seeds 1234, 7, 42); sweep 50/50, 0 failing. Math in the sweep: 131 bridge doors (6 bridges each), 92 of them with a broken bridge, at least one on all 50 seeds; 169 gap gates (50 off-home); the trial fits on 50 seeds. `FIXTURE CHANGED` is the intentional re-record described above. Not committed.
