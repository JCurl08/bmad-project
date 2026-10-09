---
title: 'Playtest 5 fixes'
type: 'feature'
ticket: '19'
created: '2026-10-08'
status: 'built'
baseline_revision: '6f6aaa6'
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

**Problem:** The user played the 4.14–4.18 build all the way through ("it is really good") and gave three notes:
1. "stones should drop if you click the e button, it shouldn't matter which direction you face". This is the boulder the player holds with the Heavy Mitt\* or the temple grip.
2. "many temple puzzles are the same, white face, red face, green face are essentially the same puzzle. Could you introduce different puzzles for each? A standard video game puzzle is fine."
3. "the overlay menu covers the screen". The user confirmed this means the top-left HUD panel.

**Approach:**
1. **Dropping boulders.** Pressing Interact while holding a boulder always drops it, whichever way the player faces. Put it at the nearest free spot around the player and never refuse. Grabbing still uses facing.
2. **Distinct temple puzzles.** The user approved this mapping:
   - **Town:** a sliding-tile picture puzzle of the town crest, the classic "15-puzzle" (3×3 or 4×4 by difficulty). Generate it as a solvable scramble at a target number of moves.
   - **Ember Vale\* (Chemistry):** Lights Out. Striking a crystal flips it and its neighbours, and the goal is to light them all. Generate solvable boards at a target minimum number of presses.
   - **Greenwood\* (Biology):** birdsong Simon. Finch statues sing a growing sequence and the player strikes them back in order. A wrong strike replays the sequence; it does not reset the whole puzzle. The sequence length is set by difficulty.

   The Math, Earth and Physics temples stay as they are. Each new puzzle has readable visuals, an arrival rule line in the temple voice, and the existing solve and gem flow.
3. **HUD panel.** Move or reshape the top-left HUD so it no longer covers the play area at 960×600. For example: a slim bar along the top edge or over the wall band, compact icons instead of text lines, or a panel that fades while the player is under it. Readability and every HUD element stay.

</frozen-after-approval>

## Boundaries & Constraints

- The seed sweep stays at 50/50. Re-record determinism, with a note.
- The subtle-science and voice rules still hold (InWorldText, voices-and-names.md).
- The readability tests stay green.
- PlayMode tests run in class batches.
- Nothing from later Epic 4 stories, except the hooks they need.

## Tasks & Acceptance

- [x] Boulder drops on Interact in every facing. Add a test.
- [x] Town sliding-tile, Chemistry Lights Out and Biology Simon temples, each with a solver or proof, difficulty targets, art, rule text and real-input solve tests. The sweep and the 500-seed robustness test cover them.
- [x] HUD moved off the play area. Add a layout test at 960×600 and take screenshots.
- [x] Update `player-guide.md`. Re-record determinism (with a note). Run the full verify through `tools/verify.sh`.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
- **Drop on Interact** (`MassMitt.Drop`, `DropSpot`, `FreeForBoulder`): holding a boulder, Interact always drops it, whatever the facing, and never refuses. The boulder stays where it is if that spot is free (nothing solid, not the player) and within `GrabRange`. Otherwise it goes to the nearest free spot round the player: rings from just clear of the player's body, 0.3 apart, out to 3.6, 16 directions each, the boulder's own direction first, inside its bounds, with a clear line from the player. With no free spot it stays put. While a boulder is held, `NpcTalker` yields the press, so a person in talking range no longer swallows the drop. *Assumption:* a talk that opened on the same press was the likely cause of the "depends on facing" report; the code always released, but a nearby person could take the press. Grabbing is unchanged.
- **New temple puzzles** (`TempleGames.cs` pure; `TempleBoards.cs` in play). The kinds `StoneOrder`, `SeedOrder` and `TownOrder` became `Lights` (4), `Song` (5) and `Sliding` (6). Each is made from stream 31 (`TemplePlan.RngFor`), uses the existing `TempleView.Solve` (magic, gem, quest done), resets on leave, and is covered by the sweep and the 500-seed robustness test through `TemplePuzzle.Problems`. Any swing counts (as in 4.14). A swing that reaches several targets strikes only the one nearest the swing's centre (`TempleStrikeBoard`). Targets are triggers, so nothing traps the player. One popup at a time.
  - **Town, sliding crest** (`TempleSliding`, `TempleSlidingBoard`): a 3×3 board of the town crest with eight tiles and a gap. Strike a tile beside the gap to slide it in; any other tile wobbles and says `TileStuckPopup`. The whole crest hangs small by the west wall, and a tile in its home cell shows a gold pip. The scramble is a seeded walk of legal moves from solved (never undoing the last move, at least 18 moves). It stops on the first board whose optimal length, from a BFS over all 181,440 boards, is **14–18** (*assumption*: late tier, Town's "short" temple; a random board averages 22, the maximum is 31). Seeds 1–500: 14 moves ×126, 16 ×112, 18 ×262; walks of 18–40 moves; P_rand (random legal slides up to 3× optimal) about 0%. IDA* with Manhattan agrees with the BFS on 12 seeds.
  - **Ember Vale\*, Lights Out** (`TempleLights`, `TempleCrystals`): a 5×3 bank of crystals, lit gold or dark slate. A strike flips the crystal and its up, down, left and right neighbours, with a flash; the goal is all lit. The deal strikes 6–7 distinct crystals of the lit bank and keeps the board when the BFS least-strike count (over 2^15 boards; 4,096 reachable, the hardest 8) is **6–7** (*assumption*: the top fifth of boards). Seeds 1–500: 6 ×393, 7 ×107; P_rand (random strikes up to 3× least) about 0.2%. A brute force over every strike set agrees on 12 seeds. *Assumption:* 5×3, not 4×4 or 5×5, so the bank fits below the altar.
  - **Greenwood\*, birdsong Simon** (`TempleSong`, `TempleChoir`): four finch statues (red, gold, blue, green, with 1–4 crest feathers) in a plus round a perch. They sing a song of **6** (rounds of 3, 4, 5 and 6 notes; 18 strikes); a singing finch glows, opens its beak, grows a little and a note rises. A wrong strike says `WrongNotePopup` and replays the round's song, keeping the round. Strikes while they sing are ignored. The deal never puts the same finch twice in a row and uses every finch. A replay takes at most 5.8 s (the late cheap-retry bound is 12 s). *Assumptions:* length 6 is the late ordering column's 5–6; rounds start at 3 (the early length); the song is visual because the game has no sound; from the perch a swing reaches exactly one finch (`Problems` checks this).
  - **Text** (`TempleText`, voice-neutral, ≤60 characters a line, in the scans): three rule lines and `TileStuckPopup`, `WrongNotePopup`, `NextSongPopup`. Removed (no other user): `TempleOrderSpec`, `TempleTargets`, `TempleTarget`, `WrongPopup`, `TooSlowPopup`. `TempleOrderSpec.Grow` became `TempleRoom.Grow` (the booths use it). `TownArt.DieFace` stays (chests use it).
  - **Art** (`TempleArt`, code-drawn in the towns' style: Kenney palette, plum outline, 8 px a unit, point filter): the 48 px crest cut into nine 16 px pieces, lit and dark crystals, four finch statues in a still and a singing look, and a note. Layouts sit below the altar and keep out of the lane in (the south finch's trigger may reach into the grown lane; triggers never block).
- **HUD** (`CubeDebug`): the top-left panel was the F1 debug overlay, 820×170 at (8, 8), covering the floor. It is now two slim one-line bars over the screen's north and south wall bands (`OverlayBars`: the wall's height on screen, 20 px at 960×600). The top bar shows the seed and the player's location (`LocationLine`: face, region, cell, entry facing, module, or room or arena, and hostility). The bottom bar shows every key (`KeysLine`, Shift variants as "x/y"). The font is 12 px at 600 px tall and shrinks to fit, down to 10 px. `OverlayText` is still location plus keys, so the old scene tests hold. *Assumption:* "the overlay menu" was this F1 panel (the only top-left panel); the combat HUD and the item lines are unchanged. At windows narrower than 960 px the keys line may clip its end.
- **Tests:**
  - EditMode `TempleTests`: 3 new 500-seed tests (sliding with IDA*, crystals with brute force, song) and an art test, replacing the ordering test.
  - New EditMode `HudLayoutTests` (5 after the review; 8 cases): bars inside the wall bands at 960×600, 1280×800, 1920×1080 and 800×600; 20 px at 960×600; lines fit at 11 px; every key named.
  - PlayMode `TempleSceneTests` (14 after the review, batch 7). New with real keys: the Town crest solved tile by tile from the gap after a stuck strike; the crystals lit with their least strikes, each flip checked; the finch song struck back round by round after a wrong note that replays the round; a held boulder dropped with E in all eight facings, pinned half over the player (moved off), and with a person in talking range (no talk opens). The Town reset/stay-solved test now uses the crest. The stone-timer test is gone with the stones.
- **Determinism:** re-recorded twice, byte-identical. Only `temples` (50 lines) changed, and within them only the Town, Chemistry and Biology entries; the Math, Earth and Physics temples are unchanged.
- **Docs:** `player-guide.md` (temple table, swings, first run, boulder drop, F1), `voices-and-names.md` (temple popups), `ART-MAP.md`.
- **Screenshots** (seeds 21 and 7; scratchpad `snaps419/`): `s*-town-sliding-{fresh,halfway,solved-magic,solved}`, `s*-chemistry-crystals-{fresh,strike-flash,solved}` and `s*-biology-finches-{fresh,singing,solved}`, taken by a temporary test (since deleted). IMGUI is not drawn in batch mode, so `hud-before-960x600.png` and `hud-after-960x600.png` are the 960×600 gameplay frame with the old panel and the new bars drawn on in Arial at the code's rects and sizes (Pillow).

- **Review patches (pass 1, quick lens; see the Review Triage Log):**
  - **A swing drops like E:** `MassMitt.SwingAt` on a held boulder calls `Drop()`, so it uses `DropSpot` too.
  - **The drop's path:** `LineClear` circle-casts the boulder's radius from the player to the spot, as `Boulder.Follow` moves it, including a timed door's doorway (`BoulderBlocker`). Something already touching at the start blocks only a move deeper into it.
  - **The finches wait:** the first song starts when the rule popup is gone (`TempleView.RuleSeconds`, 6 s) or when the player first moves 0.3 from where they arrived, whichever comes first.
  - **The listening stone:** a grey stone with a carved note stands west of the perch (`TempleSong.StoneSpot`). Striking it whenever the finches aren't singing sings the round's song again, or starts the first song. The round is kept, and it doesn't count as a wrong note. `Problems` checks the stone is on the floor, clear of the finches, and that no swing from the perch reaches it. The rule line mentions "the grey stone".
  - **The rule popup:** for the crest, crystals and finches it stands still (`PickupPopup.Show(..., rise: 0)`) and low (`TempleView.BoardRuleSpot`, local y -5.45), below the board and above the doorway, so it covers neither the tiles, the crystals nor the finches.
  - **Strike counting:** `TempleStrikeBoard.Accepts` decides; a finch struck while the choir sings is neither reported by `Hit` nor counted in `Strikes`.
  - **F1 and the dialogue box:** the box (`DialogueBox.PanelRect`, the bottom 150 px) reaches into the bottom wall band, so the keys bar is hidden while a dialogue is open (`CubeDebug.BottomBarShown`). The top bar stays.
  - **Layout tests on real rects and real text:** `CombatHud.PanelRect` and `DialogueBox.PanelRect` are the rects their `OnGUI` draws. `CubeDebug.MeasuredWidth` and `FittedFontSize` measure with `GUIStyle.CalcSize` in Unity's built-in font, and the bars use the same fit. `HudLayoutTests` (+2) checks the combat panel (3, 5 and 10 hearts, with and without rewards and dice) against both bars, the dialogue box against them, and the keys line measured at 11 px or more. New PlayMode `TempleSceneTests.TheOverlaysRealLines_FitTheirBars_At960x600_Everywhere` measures the real `LocationLine` on every screen of every face, in a temple and in the core arena: all fit at 11 px or more, and the all-hostile line fits too.
  - **The drop test** now asserts the new behaviour in every one of the eight facings. The boulder is pinned at its bounds' edge or corner that way, half over the player, so its own spot isn't free; E sets it at `LastDropSpot`, free, off the player, round them and inside the room. The `|| OnlyThePlayerTouches` escape is gone. A swing drop is covered too.
  - The finch test checks that the song waits for a first step, that an ignored strike isn't counted, and the stone's replay with the real keys. The crest, crystal and finch tests check the rule stands below the board.
  - Determinism is unchanged by the patches.

## Verification

**Result (2026-10-09, `tools/verify.sh v419b`, one part at a time in the foreground):**
- `edit`: EditMode 405/405 (without `Slow`).
- `robust`: 1/1, the 500-seed test.
- PlayMode batches 0-7: 39/29/54/28/38/37/4/14, all passing. Batch 7 (`TempleSceneTests`) has 14: the three new temples with real keys, the drop test and the overlay-lines test.
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s (unchanged).
- `sweep` (with `ALLOW_FIXTURE_CHANGE=1`): 50/50, 0 failing. Every temple is solvable on 50/50 seeds: 300 temples (Islands 50, Ice 25, Wind 25, Booths 50, Lights 50, Song 50, Sliding 50), 0 from a template.
- `FIXTURE CHANGED (allowed)`: this is the intended re-record described above (only the `temples` lines). `DeterminismTests` passes.
- An earlier `v419a edit` run was stopped by the host for low memory; `v419b` reran every part.
- Not committed.

## Review Triage Log

Pass 1 (quick lens), triaged by the coordinator: high 0, medium 0, low 7, all patched.

| Verdict | Route | Finding | Outcome |
|---|---|---|---|
| low | patch | A mitt swing on a held boulder released it in place, not through `Drop()`/`DropSpot` | `SwingAt` calls `Drop()`; tested in the drop test. |
| low | patch | `DropSpot`'s path check was a thin line, not the boulder's circle | `LineClear` circle-casts the boulder's radius (as `Follow`), including `BoulderBlocker`s; a start overlap blocks only a move deeper in. |
| low | patch | The finches sang 1.5 s after arrival, under the 6 s rule popup; no way to hear the song again; the rule popup covered the finches and the board | The song waits for the popup to go or a first step; a listening stone replays the round's song (or starts it); the board temples' rule stands still and low, below the board. Tested in the finch, crest and crystal runs. |
| low | patch | Strikes ignored while the finches sing still counted in `Strikes` and were reported by `Hit` | `Accepts` gates both; tested. |
| low | patch | The F1 keys bar overlapped the dialogue box at 960x600 | Hidden while a dialogue is open (`BottomBarShown`); tested against `DialogueBox.PanelRect`. |
| low | patch | `HudLayoutTests` used a character-count estimate and no real HUD rects | Real `CombatHud.PanelRect` and `DialogueBox.PanelRect` (shared with `OnGUI`); text measured with `CalcSize` in the built-in font; a PlayMode test measures the real `LocationLine` everywhere. |
| low | patch | The eight-facing drop test let a boulder still on the player pass (`\|\| OnlyThePlayerTouches`) and never forced a move | Each facing pins the boulder half over the player at its bounds' edge or corner; E must set it free, off the player, round them, in the room; the escape is removed. |
