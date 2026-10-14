---
title: 'Playtest 7 fixes'
type: 'feature'
ticket: '25'
created: '2026-10-08'
status: 'built'
baseline_revision: '58748c9'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/faces-and-races.md'
  - '{project-root}/_bmad-output/initiative-video-game/change-playtest-3-world-redesign/change-playtest-3-world-redesign.md'
  - '{project-root}/_bmad-output/initiative-video-game/epic-world-worth-exploring/puzzle-difficulty-research.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Playtest 7 (2026-10-13). The user's notes:
1. "bolder puzzle a little broken. Any bolder should be able to activate any gate. For a screen with multiple gates only certain bolders activate certain gates. I think we can cut down the number of bolders to 3 for these screens as well."
2. "in the physics temple puzzle the gate remains open after activating the switch with 3 bolders, I think it should behave the same as the others, but I think they need a longer timer."
3. "more challenging wind puzzles, right now they are very simple requiring just 2 crossings."
4. "the rain areas should be rainy all over, not just a specific spot."
5. "crosser shouldn't create a permanent bridge, it should just let you jump over the spot."
6. "Right now the screen has two black bars on both sides. Can we turn this into our menu. Right now it just hovers overtoop of the play area and makes it difficult to see."
7. "Maybe give some hints on how to defeat the boss. One of the NPCs should provide it"

**Approach:**
1. **Boulders and timed doors.** Every boulder counts for every timed door it is near. Remove the per-door owner rule. A screen with more than one timed door gets 3 boulders, shared between the doors. Each door's required count and slack still hold. The sweep and the door solver prove each door can be opened with the shared boulders.
2. **Physics temple doors.** They time out like the face timed doors instead of latching open. The timer is longer, so a 3-boulder dash fits easily: at least 1.25× the longest dash (assumption: tune from the research slack bands). One boulder short must still shut first.
3. **Wind puzzles.** Raise every tier: more streams and regions, more rides to the goal (at least 4 early, 6 mid and 9 late, tuned within generator limits), real decoys and route choice. Build the late tier's fan switches if they fit. Update the targets and keep everything solver-checked, with the template fallback and the 500-seed robustness test.
4. **Rain.** A freezing-rain screen is rainy everywhere: the rain covers the whole screen and so does its slowing effect. Ice, wind and town screens never get rain.
5. **Gap-Crosser\*.** Using it beside a one-square gap (a broken span, gap gate or crevice where it applies) makes the player hop across. Nothing permanent is built, so each crossing needs the item. Apply this to every Gap-Crosser\* use: Math gap gates, island broken spans and temple gaps.
   - On islands, broken spans never light (unchanged). The hop takes the player across, and the plank puzzle rules stay intact.
   - Update DoorwayReach, the sweep and the puzzles so that reaching a place over a gap needs the item held, not a mend done earlier.
6. **HUD in the side bars.** The window's side letterbox bars become the HUD and menu: hearts, keys, boosts, dice, the quest summary and prompts. Nothing overlays the play area at 960×600 or any wider aspect ratio. When the window has no side bars, shrink the play area so the panels fit. Keep the shared HUD scale from 4.21.
7. **Boss tips.** A clear, findable NPC gives plain tips on beating Primsworth: mix the particles through the gaps faster than he sorts, dodge the shot after the glow, each gem helps. The NPC is a Town resident or a prisoner placed first in the lair (assumption: a named Town character near the Mayor). The tips add to the existing lair hints.

</frozen-after-approval>

## Boundaries & Constraints

- The seed sweep stays at 50/50. Re-record determinism, with a note.
- The subtle-science and voice rules still hold (InWorldText, voices-and-names.md).
- The readability tests stay green.
- PlayMode tests run in class batches.
- Nothing from later Epic 4 stories, except the hooks they need.

## Tasks & Acceptance

- [x] Shared boulders for every timed door, 3 boulders on multi-door screens; solver and sweep updated; tests.
- [x] Physics temple doors time out with a longer timer; one boulder short still fails; tests.
- [x] Harder wind tiers within new targets; solver, templates and the 500-seed test; real-input ride test.
- [x] Whole-screen rain; tests.
- [x] Gap-Crosser\* hop replaces mending everywhere; reach model and sweep updated; real-input tests.
- [x] HUD moved into the side bars; layout tests at 960×600, 16:9 and 4:3; screenshots.
- [x] Boss-tip NPC with in-voice lines; text scans pass.
- [x] Update player-guide.md. Re-record determinism with a note. Full verify through tools/verify.sh.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
- **1. Shared boulders** (`PhysicsPlan`, `PhysicsPlan.Layout`, `PhysicsTrial`, `TimeField`):
  - The per-door owner rule is gone (`TimedDoorGate.MassOwner`, `Boulder.Owner`, `PhysicsTrialSpec.CountsOnlyOwnBoulders` removed): every boulder weighs in every timed door's field it is in, booths included.
  - A screen with more than one timed door (its doors and, on the trial screen, the two booths) holds exactly `PhysicsPlan.SharedBoulders` = 3, each placed reachable to every door and booth there; spares are 0 there. They are built with the trial on its screen, else with the screen's first door (`TimedDoorSpec.Boulders`); `PhysicsPlan.BouldersOn`, `SharesBoulders` and `DoorCountOn` read the pool. A screen with one door keeps required + spare.
  - Sweep rules (`LayoutProblems`): each door's reachable boulders and room are counted over the screen's whole pool; a shared screen must hold exactly 3; the trial's screen must hold fewer boulders than both booths need at once (so they must still be moved). The trial placement may no longer lower a door's requirement but may drop its spare.
  - Every seed's trial screen shares (its two booths), so the face trial now has 3 boulders for two booths needing 2 each.
- **2. Physics temple doors time out** (`TimedDoorGate`, `DoorLatch`, `DoorSwitch.CreateStrip`, `PhysicsTrial` temple rules, `TempleSpecs.TempleBooths`, `TempleView`):
  - `TimedDoorGate.Latches` off: getting into the doorway while the door is open counts a pass (`Passed`, `Passes`, once per opening) and marks the booth done (its pad turns green); the clock pauses while the player stands in the doorway (`IsHeld`), and once they leave the door runs out and shuts again. `UsesThreshold` off (only the doorway counts) and `DilationPerBoulder` (the field's strength) are per door. Face doors and the face trial are unchanged (they still latch).
  - New temple layout: the booths stand against the side walls (1.5 in from them), one high (lane y 2.6 or 3.0) and one low (lane y -1.5 or -2.0), on opposite sides, their doors facing across the room. Each door's switch is a strip across a short stall (two solid posts) on the far side of the room on its lane, so the dash is one straight run along the lane into the doorway, the same length from anywhere on the strip (the 4.14 "longest/shortest dash" spread is gone). The latch bands are gone; the dash line is drawn from the stall to the doorway.
  - **Timer:** with three boulders a door stays open `DashSlack` = 1.25 times the dash; dashes are 1.53 s (stall at 7.2) or 1.57 s (7.6), so the door is open **1.91–1.97 s** (before 4.25 about 1.5–1.6 s). With two boulders it shuts after 1.37–1.41 s, **10.2% before the dash ends**.
  - *Assumption:* the face's field (0.6 a boulder) cannot give both 1.25× with three and a real miss with two (the most it allows with any margin is 1.27×), so the temple's field is stronger: `TempleBooths.FieldK` = 1.8 a boulder (dilation 1 + 1.8m). Three boulders: 6.4×, two: 4.6×.
  - Rule text: "Get into both booths to win the gem. … Step on a switch, then run in before it shuts."
- **3. Harder wind tiers** (`WindTargets`, `WindLayout`):
  - New targets (*assumption*: tuned on the generator over seeds 1–500, every band met with no template):

    | | Early | Mid | Late |
    |---|---|---|---|
    | Min rides | 4–5 | 6–7 | 9–10 |
    | Regions | 5–8 | 7–11 | 10–16 |
    | Streams | 4–8 | 6–11 | 8–16 |
    | Decoys | 1–3 | 2–4 | 3–5 |
    | Route choices (new, at least) | 1 | 2 | 3 |
    | Grip anchors (new, at most; never two in one stream) | 2 | 3 | 6 |
    | Return cost (at most) | 2 | 3 | 4 |
    | P_rand | 10–60% | 3–30% | 0.5–15% |
    | Longest ride | 12 | 14 | 18 |
    | Islands dealt | 3–5 | 4–6 | 5–7 |

  - A route choice (`WindMetrics.Choices`) is a resting place on a shortest route with rides on to two or more places.
  - Generator: a short route is lengthened with an anchor on one of its streams (`LengthenRoute`, in the deal and as a mutation while the route is too short); an island no ride reaches may sink into the sky (`SinkUnreached`) while the screen has regions to spare; anchors are never chained; the retry budget is 400 attempts.
  - **Measured over seeds 1–500 (generation only, least/mean/most):** early rides 4/4.06/5, regions 6/6.17/7, streams 6/7.83/8, decoys 1/1.29/3, P_rand 24/47/60%, anchors 0/1.5/2; mid rides 6/6.1/7, regions 8/8.5/10, streams 9/10.8/11, decoys 2/2.3/4, P_rand 8/23/30%, anchors 1/2.7/3; late rides 9/9.05/10, regions 11/12.1/14, streams 11/14.8/16, decoys 3/3.2/5, return 2/3.5/4, P_rand 2.5/10.3/15%, anchors 3/4.95/6. 0 templates at every tier. The late tier averages 75 ms a screen in a release .NET build; in the editor the 1,500-generation test takes about 4 minutes (its timeout is 15 minutes).
  - New templates (one per tier, found by the search, inside every target). The temples' wind puzzle is the late tier, so it now takes 9+ rides too.
  - **Fan switches: not built.** *Assumption:* they need a (region, fan mask) solver state, runtime toggles, art for a stream that is off and a real-input test; with route choices and decoys raised instead, that did not fit this story. The sweep still reports "fans 0 (not built)".
  - New PlayMode test: a late screen solved with the real keys after first riding a decoy and riding back on course (9+ rides).
- **4. Rain all over** (`EarthPlan.RainArea`, `FreezingRain`): a freezing-rain hazard covers its whole screen (24×15), so it slows everywhere on it; its wash is fainter (alpha 0.3) and its drops fall and wrap round. `EarthPlan.Problems` flags rain that does not cover its screen. Ice, wind, town and calm screens never get hazards (unchanged rule, tested again).
- **5. Gap-Crosser\* hop** (`GapHop`, `HopArc`, `GapGate`, `IslandScreen.Hop`, `BrokenSpan`, `PlankLights`):
  - Swinging the Gap-Crosser\* at a gap (a gap gate in an alcove's mouth, or a broken span's hole) hops the player across: it lands on the gap's middle line, just past its far side (the first free spot of 0.05–1.05 past), and the art jumps in a short arc. Nothing is built: the gate never opens (`IsOpen` stays false) and the span's gap stays solid and grey, so every crossing needs a swing, both ways. A blocked landing refuses.
  - Removed: mending (`PlankLight.Mended`, `IslandScreen.Mend`/`Mended`, `MendedGlow`, `MendedText`, `GapDoorMendedText`, `GapMended`, the laid plank, `GapGate.Crossing`).
  - The reach models already treat a gap as crossable only with the item held (`DoorwayReach.Reachable(true)`, `IslandLayout.Joins`, `IslandGraph`): since an item is never lost in a run, "held" and "carried" are the same, so the sweep and puzzle rules are unchanged; the docs now say hop, not mend. A broken span never lights and is crossed for free by the plank rule (unchanged).
  - Text: the gap doorway's line is now "If you carry the Gap-Crosser\*, then swing it to hop the gap."; the shape people's item line "It hops you over the gap and builds nothing. Therefore, one swing, one hop."
  - Temples have no gaps (the island temple's two doorways both lie on the network).
  - Real-input tests: every gap gate of a run hopped into and back out of with Enter (`MathSceneTests`); the trial island's span hopped three times with Enter; the trail walk hops its spans with Enter; the all-islands walk (`IslandWalkTests`) hops every span there and back.
- **6. HUD in the side columns** (`SideHud`, `SideHudView`, `CombatHud`, `CoreHud`, `ScreenCamera.Viewport`, `CubeWorld`):
  - The play area keeps the screen's 1.6 aspect, centred; each side column is at least 125 GUI units wide. On a window at least about 2.17:1 wide (2560×1080) the columns are the window's own bars and the play area keeps the full height; on narrower windows (the 960×600 canvas, 16:9, 4:3) the play area shrinks to leave 125 each side (960×600: 710×444 of 960×600), with black bands above and below. The camera draws only the play area (`ScreenCamera.Viewport`, set by `CubeWorld` to `SideHud.CameraRect`).
  - Left column (`CombatHud.StatusLayout`): hearts (five a row), health, the equipped item (wrapped), keys/chest keys/boosts (a line each), the fragment dice (three a row), then the held stone's and the Mass Mitt's lines (moved here from `Isotope` and `MassMitt`; the stone's tag over the head stays). Right column (`SideHudView`): the first open quest (title and step), the map and quest-log keys; in the core, the Demon's meter, its gem help and the exit hint at the bottom (`CoreHud.MeterLayout`). Death and victory banners centre on the play area.
  - *Assumptions:* the dialogue box, the map, the quest log and the between-runs screen still open over the middle (they pause the game); the F1 debug bars stay at the window's top and bottom (they never reach the floor); world popups stay in the world.
  - Tests: `HudLayoutTests` at 960×600, 1280×720, 1920×1080, 1024×768, 2560×1080 and 1280×800 (every status, quest, key and meter rect inside its column and off the play area, the camera rect equal to the play area); a PlayMode check that the scene's camera uses it.
  - Screenshots: composed (see below).
- **7. Captain Shuffles** (`BossTips`, `NpcNames.BossTipper`, `TownPopulation.BossTipper`): an old adventurer in the Town voice, standing still just north-west of Mayor Muddleton, north of the statue (the square's stand row is full, so his feet keep off the lanes and solids while his body may stand over the crossroads' edge; *assumption*). Two lines: "Captain Shuffles, retired, and I've tangled with Primsworth the Demon! Knock his warm and cool motes through the gaps at both ends of his wall, faster than he can tidy them back." / "When he glows and draws a line, step aside, love: a shot follows it. He tires as the fight drags on, so keep mixing. And every temple gem you win makes him easier!" Each is true of `MaxwellDemon`/`CoreArena` (mixing beats it, the end gaps always open, the telegraph, `RestoreFatigue`, the gems' benefits). They are in the Town voice scans, the text rule and the length rule. The no-gem skill check is unchanged.
- **Docs:** `player-guide.md` (shared boulders, the hop, wind, rain, the temple booths, the side columns, Captain Shuffles), `voices-and-names.md` (Captain Shuffles).
- **Screenshots** (scratchpad `snaps425/composed/`): the Town square with Captain Shuffles by the Mayor at 960x600, 1280x720, 1920x1080, 1024x768 and 2560x1080; a rainy Earth screen; three late wind screens (9 rides each); an island span before and after a hop. IMGUI is not drawn in batch mode, so each is the camera rendered at its real viewport plus the HUD drawn with Pillow from the rects and wrapped lines the layout code returns (a temporary snapshot test wrote them; deleted).
- **Determinism:** re-recorded twice, byte-identical. Changed: `physicsPlan` (shared boulders), `earthPlan` (rain covers its screen), `wind` (the new tiers) and `temples` (the new booths and the late wind), 50 lines each; nothing else.

## Verification

**Result (2026-10-13, `tools/verify.sh`, one part at a time in the background, one Unity at a time):**
- `v425a` (all parts, `ALLOW_FIXTURE_CHANGE=1`): robust 1/1 (seeds 1-500); PlayMode batches 0-6 and 8: 39/30/51/28/38/38/4/10, all passing; skill checks Victory in 78.6 s, 90.5 s and 105.5 s (unchanged); sweep 50/50, 0 failing, `FIXTURE CHANGED (allowed)` (the intended re-record above). Two failures were test set-ups, fixed and re-run: EditMode `TownSquareTests` (counted a door's own boulders, now its screen's) and batch 7's drop and carry spots (placed where the new booths stand).
- `v425c edit`: EditMode 442/442. `v425b 7`: batch 7 14/14.
- Sweep wind stats (seeds 1-50, least/mean/most): early rides 4/4.1/5, choices 2/2.6/3, P_rand 32/46/59%; mid rides 6/6/6, choices 3/4/5, P_rand 14/23/30%; late rides 9/9/9, choices 5/5.5/7, P_rand 4/10.7/15%. Temples: 300/300 solvable, booths "open 1.97 s with 3 (1.25x), one short shuts 10.2% early".
- Not committed.

## Review Triage Log

Pass 1 (coordinator review), all patched:

| Verdict | Finding | Outcome |
|---|---|---|
| high | A temple booth door's "held" state came from trigger Stay callbacks, which stop when the player's body sleeps, so the door could shut on a player idling inside | `TimedDoorGate.PlayerInDoorway` overlaps the doorway box each physics step (`Physics2D.OverlapBoxAll`); `IsHeld` reads it and `Close` refuses while the player is in the doorway. The temple test now idles 2.5 s inside a booth with the body put to sleep: the door stays open, then the player walks out and it shuts. |
| medium | The shared pool went with the screen's first door with a layout, which PhysicsFace never builds without a switch | The pool goes with the first door that has its switch (`PhysicsPlan.Builds`); EditMode check over seeds 1-50. |
| low-med | A hop with every landing spot taken refused silently | `GapHop` tries sideways offsets (0, ±0.45, ±0.9) at each clearance, then says "Something is in the way." (`GapHop.BlockedText`, in the text scans). Component test with a blocker past the gap. |
| low | The quest summary and the core meter could meet with all six gems and a long step | `SideHudView.InfoLayout` keeps above the meter's top while it shows; the step gives up whole lines ("..."). New 960x600 test with every gem's help and a 200+ character step. |
| low | Captain Shuffles had a dynamic full-height body with only his feet checked | Kinematic; a spot within 3 of the Mayor clear of the statue's art is preferred (none on the square today, so he still stands just north of the statue). |
| low | Cleanup | `IslandScreen.Hopped` and the static `GapHop.Hops` removed; `EarthPlan.AreaHazard` lost its unused rng; `FitBoulders` no longer lowers requirements on a shared screen. |

Re-verify (`v425r`, all parts, `ALLOW_FIXTURE_CHANGE=1` only because the 4.25 re-record still differs from HEAD): robust 1/1; PlayMode batches 0-8 39/30/51/28/38/38/4/14/10, all passing; skill checks unchanged; sweep 50/50, 0 failing. EditMode then failed one new assertion (the captain's statue-clear spot was 10 away); bounded to 3 from the Mayor and re-run (`v425s edit`): 443/443. The fixture did not move with these patches (a fresh record is byte-identical to the 4.25 one).
