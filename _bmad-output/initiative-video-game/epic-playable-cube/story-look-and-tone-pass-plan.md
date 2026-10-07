---
title: 'Look and tone pass'
type: 'feature'
ticket: '22'
created: '2026-10-06'
status: 'built'
baseline_revision: '8be3e098eb338e5934c847210ad6978fc1a5acd3'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/lore-and-tone.md'
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/faces-and-races.md'
  - '{project-root}/_bmad-output/initiative-video-game/change-playtest-feedback-full-cube/change-playtest-feedback-full-cube.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The user's playtest asked for four things:
- more colour, with Rubik's-cube region colours ("biology green, chemistry is red, physics is blue")
- prettier finches ("a little ugly")
- a subtle science analogy ("characters shouldn't be aware that they are an analogy for a science theme"), with placeholder names marked `*` until a naming brainstorm
- a way out of the boss arena, since the player got locked in

**Approach:**
1. **Rubik's colours.** Apply them through `CubeWorld.ThemeColor` and the floors, accents and ruins: Town white, Biology green, Chemistry red, Physics blue, Math yellow, Earth & Atmosphere orange. The overall palette gets more colourful (saturated accents, coloured wall trims or floor patterns per region) while staying readable. The map picks this up through ThemeColor.
2. **Finches.** Redraw the finch NPC parts so they are cute and readable: a round body, a clear beak, eyes, little feet, and pleasant colours. Keep the head, torso and leg slots and the seeded mixing.
3. **Subtle science.** Rewrite every player-visible string (NPC lines, generated hints, HUD, popups, item names, trial, boss and between-runs text) to the subtle-science rule:
   - no real scientist names (Darwin, Curie, Einstein, Euler, Newton, Anning, Richter, Maxwell…)
   - no explicit science vocabulary for the themes
   - face themes get in-world `*` placeholder region names: Town stays Town; Biology "the Greenwood\*"; Chemistry "the Ember Vale\*"; Physics "the Weightlands\*"; Math "Bridgelands\*" (exists); Earth "Wildlands\*" (exists)
   - the boss is "the Demon\*"
   - patron figures get `*` placeholders
   - generated hints use region names and name gates by their look ("a mossy rock", "a dark doorway"), never "a Chemistry gate"
4. **Arena exit (CAP-27).** An exit door in the core arena lets the player walk out while the fight is not won. Walking out returns them to the face they entered from and resets the fight.

## Boundaries & Constraints

**Always:**
- **Visuals and text only, plus the arena exit.** No gameplay, layout, collider or RNG changes elsewhere. Determinism signatures for layouts must be unchanged. If text sits inside a recorded signature, re-record and explain.
- **One scan test** covers every player-visible string source:
  - fixed tables
  - generated hints for seeds 1–50 and both densities
  - HUD, popup and boss text
  - item display names
  - the between-runs screen

  It asserts:
  - no banned scientist names and no banned theme words (a shared list in one place)
  - every proper name that isn't a chosen name carries `*`
- **Colour mapping is exact:** Town white, Biology green, Chemistry red, Physics blue, Math yellow, Earth orange. Floors keep enough contrast with walls, gates and items for readability (the 1.15 distinctness tests keep passing).
- **Finches:** same slots and seeded mixing, an outline kept, and the 1.15 silhouette tests updated rather than removed.
- **Arena exit:**
  - The exit is a visible door on the arena's edge that can't be reached by particles.
  - Using it raises no run end.
  - Leaving abandons the fight (as F8 does) and puts the player on the screen and face of the portal they used.
  - It's blocked or hidden once the fight is won, because the run then ends anyway.
- **Tests:** PlayMode runs in `-testFilter` class batches (memory). The boss skill check stays green (it uses the controlled entry).

**Never:**
- No final names: they come from the later naming brainstorm, so every invented name carries `*`.
- No change to the boss fight's tuning.
- No new art pack.
- No removal of the F8 dev key.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Colours | Each face | Floors and accents in its Rubik's colour; the map uses the same | No error expected |
| Readability | Player, enemies, items, gates on every region colour | Still distinct (1.15 tests pass) | Test fails otherwise |
| Finch | Finch NPCs | Cute redesign, outlined, slots preserved | Silhouette test |
| Text scan | All player-visible strings, seeds 1–50 | No banned names or words; placeholders carry `*` | Test lists offending strings |
| Hints | Any generated hint | Uses region names and gate looks, never theme words | Test fails otherwise |
| Exit | Player walks into the arena exit mid-fight | Back on the entry face's portal screen; fight reset; no run end | No error expected |
| Exit after win | Fight won | Exit unavailable (run ends anyway) | No error expected |
| Determinism | Seeds 1–50 | Layout signatures unchanged | Re-record only for text-only fields, with a note |

</frozen-after-approval>

## Code Map

- `game/Assets/Scripts/Cube/CubeWorld.cs` `ThemeColor` (around line 223), floors and walls; `Art/ArtCatalog.cs` (floor tints and accents), `Editor/ArtCatalogBuilder.cs`, `Editor/ModuleLibraryBuilder.cs` (ruin tints), `Assets/Art/ART-MAP.md`; `Map/CubeMap.cs` (uses ThemeColor).
- `game/Assets/Scripts/Cube/Art/NpcPartArt.cs` -- finch parts (Race.Finch).
- Text sources:
  - `Npcs/Dialogue.cs`, `Npcs/HintGenerator.cs` (`ThemeName`, `ItemNickname`, gate wording)
  - each `Faces/*/…Population.cs`, `Town/TownSign.cs`
  - `Combat/CombatHud.cs`, `Art/PickupPopup.cs`/`PickupFeedback`, `Core/CoreHud.cs`, `Core/MaxwellDemon.cs` (boss name text)
  - `Run/BetweenRunsScreen.cs`, `Run/UpgradeShop.cs`
  - item display names in `Editor/ItemCatalogBuilder.cs` and the item assets
  - trial and gate labels
  - Code identifiers and comments may keep science names. Only player-visible strings change.
- Existing per-face text tests (Chemistry, Math, Earth) can be folded into the shared scan or kept alongside it.
- Arena: `Core/CoreArena.cs`, `Core/CorePortal.cs`, `CubeNavigator.EnterCoreArena`/`TeleportTo`, and `CubeDebug.LeaveCoreArena` (F8) as the reference behaviour.

## Tasks & Acceptance

**Execution:**
- [x] Rubik's colours in ThemeColor, floors, accents and ruins, plus a more colourful palette. Rebuild the art catalog and module library; keep colliders identical.
- [x] Finch redesign in NpcPartArt, with the tests updated.
- [x] A shared `InWorldText` (banned-word list, region names, placeholder rule) used by all text sources. Rewrite every player-visible string and the generated-hint wording.
- [x] Arena exit door, with entry-face return and fight reset.
- [x] Regenerate the scene, and check determinism for layouts.
- [x] Tests: the shared text scan, colour mapping, finch silhouette, readability (existing), and the arena exit.

**Acceptance Criteria:**
- Given EditMode, PlayMode in class batches, and the 50-seed sweep, when run, then all pass.
- Given the Cube scene, when the user plays (HITL), then each region shows its Rubik's colour, finches look nicer, no text names a science or scientist, and they can walk out of the boss arena.

## Implementation Notes

- Checkpoint 1 approved under the user's standing instruction (2026-10-06).
- Verified after the review patches: EditMode 245/245, PlayMode 150/150 (six class batches), seed sweep 50/50. User playtest PENDING (HITL).
- Floors: tints only darken a tile, so Town moved to the pale stone (T 109, white), Physics to grey stone panels (T 76,
  blue), Chemistry's grey planks go red, Math's sand gold-yellow (no pale warm tile exists, so it reads gold rather than
  lemon; the map shows true yellow), Earth's speckled sand orange.
- Placeholder names: regions Greenwood*, Ember Vale*, Weightlands*, Bridgelands*, Wildlands*; patrons Old Fledgling*,
  Glowmother*, Timekeeper*, Bridgekeeper*, Old Digger*, Sky-Watcher*; the apple-bonked sage*; the Demon*; Jumbles*; item
  and upgrade names starred (Thin Beak*, Heavy Mitt* replaces "Mass Mitt"). Chosen (no star): Town, the Partition.
- Arena exit (after review 2): no colliders of its own. The whole west wall stops everything; it lets the player alone
  through only when they walk straight west (facing west, within 0.25 of the doorway's line) into the doorway, and the
  recess behind it (2 units, past the screen edge) is kept by clamping the player to its lane. Leaving needs the centre
  1.6 past the wall line (steering at points just behind the wall never gets there). Present after any entry; hidden and
  shut once a fight ends. Root cause of the skill-check 127.8 s (seed 42): not the exit (it recurs with the exit fully
  disabled) but frame timing at fight start: the run start's extra work (planning gate looks) lengthened the rebuild
  frame, so its queued physics steps fell inside the fight's first frames. The skill check's controlled entry now lets
  the rebuild settle (3 frames) before entering; results match the baseline trajectories (72.3/107.8/85.7 s).
- Review fixes: Chemistry floor moved to sand D 53 (deep red) and Physics floor darkened, both for a tested floor
  contrast against walls, gates and items; map markers on 2 px dark outlines (tested on every region colour); debug
  overlay off by default and free of theme words; starred enemy names; one shared banned list; hint gate looks come
  from the face plans (plain iron bars where a face falls back).
- Determinism signatures unchanged (no text is in them); module prefabs rebuilt with identical geometry (colours only).
- Verified: EditMode 245/245; PlayMode 150/150 in five class batches; seed sweep 50/50 (before review). User playtest PENDING.

## Review Triage Log

Pass 1 (quick lens): high 1, medium 3, low 5, false 0, maybe-false 0. Checked OK: the exact Rubik's mapping, no collider, layout or RNG change in the module prefabs, no run end on exit, a safe exit landing.

| Verdict | Route | Finding | Evidence / action |
|---|---|---|---|
| high | patch | Arena exit fires on contact with the upper west wall, and controlled-entry gating hid it from the skill check | ExitReach 0.45 versus a 0.4 player radius over a 1.6 span. Redesigned as a recessed doorway outside the playfield, present after any entry, with a test. |
| medium | patch | Debug overlay on by default shows "Science"/"Theme Biology" | Defaulted off, now using region names. |
| medium | patch | Floor, wall, gate and item contrast unverified (Town light-on-light, blue-on-blue, red-on-red) | Adding a luminance contrast test and adjusting tints. |
| medium | patch | Map current-screen outline invisible on Town white; markers close to region colours | Double outline and stronger marker outlines, with a contrast test. |
| low | patch | Door visible after defeat | Now hidden once the fight is over. |
| low | patch | Enemy names "Quantum Flea" and "Free Radical" | Renamed and scanned. |
| low | patch | Duplicate banned lists and hard-coded placeholder literals | Consolidated into InWorldText. |
| low | patch | Gate-look hint false on a plain-gate fallback | Now derived from the built gate, with a test. |

## Verification

**Commands:**
- Unity batch EditMode; PlayMode with `-nographics` in `-testFilter` class batches, in the foreground; sweep. Expected: all pass, 50/50.
