---
title: 'Bridge puzzles: colour and solvability'
type: 'bugfix'
ticket: '17'
created: '2026-10-08'
status: 'built'
baseline_revision: 'bb7f29f'
route: 'full'
route_source: 'auto'
risk: 'high'
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

**Problem:** In the playtest of 4.12 (2026-10-08), the user said "bridges don't change colour" and "I think some of the bridge puzzles are still impossible". The tests say otherwise: IslandWalkTests, IslandPuzzleSceneTests and the sweep all pass. So either the game behaves differently in real play than in the tests, or the rule can't be read in play.

**Approach:**
1. Reproduce in the real scene the way a player plays:
   - continuous held movement, diagonals, walking along a plank's length;
   - entering from each doorway, including the gap doorway;
   - with and without the Gap-Crosser\*;
   - at the editor's real frame rate (graphics on), not only in batch.
2. Find the root cause of each complaint. Check these candidates:
   - Crossing detection misses real walking paths, for example the doorway landings or the island edges.
   - Lit colour is too close to dark on the yellow Math floor.
   - Lookout or red-herring planks never light, so they read as "broken".
   - The rule that re-crossing a lit plank snuffs everything is invisible or unexplained.
   - A layout really is impossible from the doorway the player used.
3. Fix the causes, and make the rule readable in play:
   - unmistakable lit and dark plank colours;
   - planks outside the puzzle look clearly different;
   - a short sign or line at each island screen explains the rule.
4. Add tests that walk with real, held input along realistic paths, so this class of bug is caught.

</frozen-after-approval>

## Boundaries & Constraints

- The seed sweep stays at 50/50. Re-record determinism, with a note.
- The subtle-science and voice rules still hold (InWorldText, voices-and-names.md).
- The readability tests stay green.
- PlayMode tests run in class batches.
- Nothing from later Epic 4 stories, except the hooks they need.

## Tasks & Acceptance

- [x] Reproduce both complaints. Record the root cause(s) in the notes.
- [x] Fix detection and/or solvability. Readability: clear lit and dark planks, distinct non-puzzle planks, and an in-world rule hint.
- [x] Runtime test on seeds 1–50: from every network doorway, walk the solver's trail with held, continuous input (not tile taps). Every plank lights and the chest opens. Also a graphics-on frame-rate check if feasible.
- [x] Full verify.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
- **Reproduction: crossing detection and solvability are not the bug.** The new PlayMode `IslandTrailWalkTests` plays like a player:
  - held WASD, never released between legs, eight ways;
  - the mover on at its own speed, and no teleport once the walk starts;
  - in through the doorway from the next screen;
  - a frame clock of 60 fps game time against the 50 Hz physics, with the player drawn interpolated;
  - legs alternate between careful (lined up at the plank's mouth) and direct (holding the diagonal at the far island, sliding along the shore into the mouth, off the far end and on);
  - it follows the solver's own plan: to a clean start (out and back to snuff) when the doorway's island is not one, then the trail, then onto the chest.

  On the unfixed game: 91 of 100 walks (one network doorway per screen, seeds 1-50) and every doorway of seeds 1-8 (gap doorways with the Gap-Crosser\* mending the span by a swing) lit plank after plank and opened the chest. Not one crossing was missed or doubled: not from doorway landings, not at island edges, not with diagonal entries, not at the real frame clock. No collider, water rect or narrow plank stopped the player.

  The other 9 walks all hit the same thing, one real if small defect: a plank crossed within 0.45 s of a snuff showed **red**, not lit, until the flash ended.
- **Root causes (readability; the screenshots `before-*` and `after-*` are in the scratchpad's `snaps417/`):**
  1. **Lit looked like land.** A lit plank was white wood under Math's yellow at 55%. Between yellow islands with yellow rims, that read as more island floor, not as a plank lighting up: "bridges don't change colour".
  2. **Planks that never light looked like puzzle planks.** Lookouts (the red herrings) and broken spans had the same dark wood as unlit network planks. A player crossing them saw nothing happen.
  3. **The gap doorway looked impossible.** One open doorway in three or four is the gap doorway. Its island holds only lookouts and the broken span to the network, and Math is where the Gap-Crosser\* is found, so players often arrive without it. From there, every plank that can be walked never lights, and the way on is a gap. Nothing on the screen said why: "some of the bridge puzzles are still impossible".
  4. **The snuff rule was invisible.** Exploring naturally (over and back) snuffs everything. Only a Shape NPC elsewhere on the face states the rule, and the red flash could also hide a freshly lit plank (the defect above).
- **Fixes (`IslandScreen`; no layout, RNG or placement change):**
  - **Lit is mint green, nearly opaque** (`LitGlow` 0.3/1/0.55 at 0.85), and so are the lamps, rims and chain (`LampOn`, `RimColor`). Its hue is far from the yellow floor, the water, the dark wood and the snuff's red.
  - **Planks that never light are grey.** Lookouts, and broken spans until mended, are grey-tinted wood under a grey wash (`OffNetworkPlankColor`, `OffNetworkWash`, `Wash(k)`, `ShowsGrey(k)`). A mended span turns the Gap-Crosser\*'s gold.
  - **A lit plank ends the red flash at once**, so it shows lit straight away.
  - **A rule sign on every doorway's island.** It shows a green plank over a dark one; the gap doorway's shows a grey plank over a gap. It stands at `RuleSignSpot`: `SignSpot` farther aside of the walk in (0.85 scaled, stepping back to 0.6 where the plank mouths need it), clear of the mouths.
  - **Arriving on a doorway's island says the rule** while the puzzle is unsolved (`RuleTextFor`; a 6 s popup, four short lines).
    - `RuleText` (after review): "A plank glows when you cross it. / If you cross a glowing plank, then they all go dark. / If you leave unfinished, then they all go dark too. / If every plank that can glow does, then the chest is free. / Grey planks never glow."
    - The gap doorway's `GapDoorText`: "Grey planks never glow. / The planks that can glow lie past the gap. / If you carry the Gap-Crosser\*, then swing it at the gap. / If not, then go round by another doorway." Once its span is mended, `GapDoorMendedText`: "Grey planks never glow. / The gap is spanned. The planks that can glow lie past it. / If you cross a glowing plank, then they all go dark."
    - Both are in the text scans (`InWorldTextTests`, `MathTests`), with no banned word (no "edge", "graph" or "exactly once").
  - `player-guide.md` describes the green, grey and sign rules.
- **Tests:**
  - New PlayMode `IslandTrailWalkTests` (batch 6 in `vpart.sh` and `verify.sh`, with `IslandWalkTests`; about 2 min):
    - every island screen of seeds 1-50 from one network doorway, taking turns by seed and screen (100 walks, 1340 crossings, 49 snuffs, 100 chests);
    - every doorway of every screen on seeds 1-8, a fresh run each, gap doorways with the Gap-Crosser\* (64 walks, 16 by the gap doorway);
    - the real frame clock (no captured delta time, 60 fps asked for) on seeds 1-2.

    Each walk checks:
    - the arrival's rule popup;
    - grey exactly on the planks that never light;
    - after each crossing, exactly the planks lit, the crossed one glowing (not hidden by a flash) and one lamp per plank;
    - the snuff;
    - a mended span gold;
    - solved only on the last plank, and the chest opened by walking onto it.

    *Sampling* (the plan's "every network doorway on 1-50" is about 350 walks): every screen is covered once, and every doorway on 8 seeds.
  - New EditMode `IslandPuzzleTests.Readability_...`: the lit colour's hue gaps and opacity, grey's low saturation, the texts (rules, line length), and on seeds 1-50 every doorway's rule text and every rule sign's spot (on the island, aside of the walk, clear of the mouths).
  - Graphics on: the real-frame-clock test also passed in a rendering batch run (Direct3D 12, about 57 fps, 4 walks).
- **Determinism:** layouts are unchanged, so nothing was re-recorded (the sweep's fixture check reads "fixture unchanged").

- **Review patches (pass 1):**
  - **The rule popup is said once per unsolved attempt, and only on walking in.**
    - `ForgetSteps(teleport)` marks a teleport. `CubeNavigator.SetPosition` passes false for a walked face crossing and for the sealed-edge clamp.
    - A visit that came on screen by teleport never says the rule (`WalkedIn`).
    - `RuleSaid` holds until the player leaves the screen unsolved.
    - The screen's own popup is not shown again while it is still up. The first patch matched any popup with the same text, and the trail test caught that it silenced the next screen's rule.
  - **The trail test proves where the popup comes from.**
    - It records the popups up just before the walk back in. Exactly one new popup with the doorway's text must appear, standing over that doorway's island.
    - A teleport onto the screen must say nothing.
    - Walking straight out and back in while the words are still up must not say them again.
    - It waits up to 0.5 s for the arrival, because the screen reads the drawn, interpolated position a step behind the body.
    - The walk out now lets go of the keys and presses again after a face crossing. The new out-and-back exposed a stuck fresh-press wait, but in the test's walker, not the game.
  - **Texts.** The rule also says leaving darkens an unsolved puzzle ("If you leave unfinished, then they all go dark too."). Every text says "planks that can glow". The gap doorway has a mended line. All lines are at most 60 characters and in the text scans.
  - **Test hygiene.** `targetFrameRate` is saved and restored in Setup and TearDown. `held` is reset in Setup. The real-frame-clock test guards a screen with no network doorway.
  - **Draw order.** Every layer has its own order: plank -8, grey wash -7, glow -6, shore -5, rim -4, signs -3 to 0. Rendered and checked in `snaps417/review-*`.
  - **Rule sign spot.**
    - `RuleSignSpot` checks the whole sign, board and post (`SignFootprint`, `SignPost`), against the island and the plank mouths.
    - It steps from 0.85 through 0.75 and 0.6 down to `MinRuleSignOffWalk` (0.4, scaled) aside of the walk in, and no fallback goes nearer.
    - `RuleSignProblems` checks every doorway on seeds 1-50 (the plans) and on seeds 1-500 x 3 tiers (generation, inside the existing generator test): 0 problems.
  - **Contrast.** The EditMode test composes the unlit plank (the plank art's measured average under `PlankColor`) and the grey one (under its tint and wash). It wants grey at least 0.2 lighter in luma, and lit at least 0.4 lighter than unlit. The screenshots measure unlit 0.28, grey 0.58 and lit 0.73; before the fix, the lookout was 0.27, the same as unlit.

## Verification

**Result (2026-10-08, `vpart.sh` steps in the foreground as `v417a`):**
- EditMode 368/368.
- PlayMode batches 39/29/46/28/38/37/4, all passing. Batch 6 is `IslandWalkTests` (0 problems) and the 3 new `IslandTrailWalkTests` (0 problems).
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s.
- Sweep 50/50, 0 failing. Islands: 100 screens (40 early, 38 mid, 22 late), 0 templates, 0 doorway checks outside a target.
- Fixture unchanged.
- Not committed.

**Result after the review patches (2026-10-08, `vpart.sh` steps in the foreground as `v417b`):**
- EditMode 368/368.
- PlayMode batches 39/29/46/28/38/37/4, all passing:
  - `IslandWalkTests`: 0 problems.
  - `IslandTrailWalkTests`: 100 walks (seeds 1-50), 64 doorways (seeds 1-8, 16 by the gap doorway) and the real frame clock, 0 problems.
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s.
- Sweep 50/50, 0 failing.
- Fixture unchanged.
- Not committed.

## Review Triage Log

Pass 1 (quick lens), triaged by the coordinator: high 0, medium 3, low 7. 9 patched, 1 routed to 4.15.

| # | Verdict | Route | Finding | Outcome |
|---|---|---|---|---|
| 1 | medium | patch | The rule popup could repeat, and teleports triggered it | Once per unsolved attempt (`RuleSaid`, reset on leaving), only on walking in (`WalkedIn`; `ForgetSteps(teleport)`), never while the screen's own popup is still up. |
| 2 | medium | patch | The trail test did not prove the popup came from walking in | It records popups before the walk back in and wants exactly one new one with the doorway's text over that island. A teleport says nothing, and walking straight back in does not repeat it. |
| 3 | medium | patch | `RuleText` did not say that leaving darkens an unsolved puzzle | "If you leave unfinished, then they all go dark too." (if/then, 49 characters). |
| 4 | low | patch | `targetFrameRate` was not restored on every exit | Saved in Setup, restored in TearDown. |
| 5 | low | patch | `held` carried over between tests | Reset in Setup. |
| 6 | low | patch | The real-frame-clock test had no guard for zero network doorways | Guarded: the screen is reported and skipped. |
| 7 | low | patch | The wash shared its sorting order with the shore and glow | Its own order. All the screen's layers are distinct (-8 to 0). |
| 8 | low | patch | `RuleSignSpot` checked only the board, and the fallback could near the walk in | Board and post checked against the island and the mouths. Steps stop at 0.4 aside of the walk. Checked on seeds 1-500 x 3 tiers. |
| 9 | low | patch | `GapDoorText` was wrong once the span was mended, and the wording differed | `GapDoorMendedText` once mended. "planks that can glow" everywhere. |
| 10 | low | patch | No contrast check between the unlit and grey planks | EditMode composite luma gap of at least 0.2. Screenshot: 0.28 vs 0.58. |
| 11 | low | defer | Registering the new class in a batch lives only in the scratchpad scripts | Routed to 4.15, which moves the verify scripts into the repo. |
