---
title: 'Towns on every face'
type: 'feature'
ticket: '5'
created: '2026-10-07'
status: 'built'
baseline_revision: '90a8925'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/faces-and-races.md'
  - '{project-root}/_bmad-output/initiative-video-game/playtest-2-feedback.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The user said "Still missing the towns in the center of each face". From playtest 2, they asked for "more distinguishing features for the towns. Distinctive building designs, buildings you can enter, statues, etc. More people in the town than the other tiles, no enemies. I would like one character to be a leader in each of the towns who give you a quest to help the respective regions."

**Approach:** Every face's centre screen becomes a town:
- **Buildings:** in the race's own architectural style:
  - finch nests and treehouses
  - Chemistry mushroom huts with bubbling vents
  - Physics observatories and domes
  - Math geometric blocks and towers
  - dinosaur lodges and cabins
  - Town cottages with a market
- **Statue:** one per town, in a central plaza.
- **People:** more than on any other screen of that face. No enemies.
- **Leader:** one named leader (from 4.3/4.4) who greets the player and states the region's quest. The quest text is a hook that 4.8's fragments fill in. Until then, the leader asks for help "mending" the region in-voice.
- **Displaced content:** whatever sat on the centre screen moves to another screen of the same face. That covers gates, Physics doors and boulders, Math islands and gap gates, the Earth eye, the trials and core portals.
- **Town face:** its start screen is already the centre, and gets the same treatment.

Doors that lead into buildings come in 4.6. Buildings are solid for now, and the door that will become enterable is marked.

</frozen-after-approval>

## Boundaries & Constraints

- **Placement:** layouts keep the centre screen as a town module per face (seeded or fixed per race). `ItemPlacement`, face plans and the sweep never put content there. The sweep stays 50/50, and determinism is re-recorded with a note.
- **Art:** Kenney Tiny Town/Dungeon tiles where they fit; otherwise drawn in code in the same style, as in 4.3. Readability tests stay green.
- **Text:** subtle-science and voice rules (4.4).

## Tasks & Acceptance

- [x] Town module per race (buildings, plaza, statue, marked door), placed at the centre of each face.
- [x] Move centre-screen content off; plans, placement and sweep exclude town screens.
- [x] Town population: more NPCs than any other screen of the face, no enemies, the leader with quest-hook lines.
- [x] Map shows towns (a marker or a name).
- [x] Tests:
  - on seeds 1–50, every face centre is a town with buildings, a statue and a leader;
  - the NPC count is greater than on the other screens;
  - there are 0 enemies;
  - displaced content still appears;
  - the sweep and suites pass.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
- **Where a town is.** `Town/TownSquare` holds the rules: a face's town is its centre cell (`CubeModel.StartCell`, 1,1 at 3x3), on every face of at least 3x3 (`TownSquare.HasTown`).
  - *Assumption:* a 2x2 face has no centre, so it has no town. The game always runs at 3x3; the N = 2 tests keep the old behaviour.
  - Each science pool ends with an exits-only town module (`<Theme> Town Square`, flagged `ScreenModule.IsTownScreen`, before Math's island module). The Town face has its own (`ModuleLibrary.TownSquareModule`), which `TownModule` returns for the centre. The other eight Town ruin screens are unchanged.
  - `CubeLayout` gives the centre the town module, never draws it at random, and keeps the core and island screens off it. The core cell is drawn again while it lands on the town, and the island shuffle skips the town after shuffling, so seeds that missed the centre draw as before.
- **Displaced content and where it went.**
  - Core portals, Math island screens and gap gates, Physics doors and boulders, gates and hidden spots: the town module has no slots, so they land on the face's other screens through the usual draws.
  - Open pickups and the Biology and Chemistry trials: `TownSquare.PickCellIndex` draws again when a draw hits the town.
  - The Physics trial: the town is dropped from its seeded order after the shuffle.
  - Biology flowers never grow on the town (dropped from the candidate screens).
  - **Earth:** *decision:* the calm eye stays at the centre and is the Earth town (as `faces-and-races.md` says). Its trial fallback skips it. Its soft ring stays as decor under the plaza.
  - Face NPCs and enemies: `FaceContent.ShuffledScreens` drops the town after the shuffle, and `FaceSpots.TryPick` refuses town screens.
  - `SeedSweep.FindTownProblems` checks all of this on every seed, plus each town's layout, and the hidden spots too. The sweep report gains a "Towns" line (300 over 50 seeds).
- **The towns** (`Town/TownLayout`, pure; `TownSquareView` builds them when `CubeWorld.PlaceModule` places a town module):
  - Four buildings stand in a row in the north half, their doors opening onto the horizontal lane. A paved plaza covers the crossroads and the south half, with the statue just south-east of the crossroads. The leader's stand is beside the statue, and six resident stands are spread through the south quadrants.
  - All solids keep `KeepsExitsOpen`, so the four doorways and both lanes stay clear (tested in EditMode and PlayMode). Buildings and the statue have box colliders. `TownPlan.BlockedRects` adds the layout's solids for a town module, so pure plans and the built town agree.
  - Styles: Town has a stone cottage, a cottage, a market stall with a cart, and a cottage. Finches have nests and treehouses. Chemistry folk have vent huts (with animated bubbles) and mushroom huts. Physics folk have domes and observatories. Shape people have cubes with a ball on top, and towers. Dinosaurs have cabins and lodges.
  - *Assumption:* the layout is fixed per race (the boundaries allowed "seeded or fixed"). Every race uses the same frame.
  - One door per town (the second building, west of the crossroads) is marked for 4.6. It has a `TownDoor` component (sill, facing, building kind), a red rug and a lantern. `TownSquareView.MarkedDoor` exposes it.
- **Art.**
  - The Town cottages use Kenney Tiny Town tiles: roofs T 52/54 and T 48/50, walls with doors T 85/75 and T 90/79. The cart is T 57. These are 9 new `ArtKey`s in a new `Building` category, which the readability test now treats as decor. The art catalog was rebuilt (82 keys).
  - Everything else is drawn in code (`Art/TownArt`) in `NpcDesignArt`'s style: Kenney's palette, a 2-pixel plum outline, point filter, at the people's 8 px per unit.
  - The statue is the race's leader design mapped onto stone greys with a touch of the region colour, on a pedestal.
  - The plaza is a translucent flagstone tiling. `ART-MAP.md` documents all of this.
- **People.**
  - Science faces: a new `TownSquares` component on the world (added by the scene builder) runs on the reveal. It spawns each face's leader (`NpcFactory.ChooseLeader`; Chief Rockjaw for Earth) at the leader stand, kept within 0.3 of it. It also spawns `TownSquare.ResidentCount` (6) residents of the race (index base 7000), who talk with run-true hints.
  - Town face: `TownPopulation` spawns Mayor Muddleton (`Leader`). Its first `TownPlan.StartScreenNpcsFor(3)` = 6 NPCs (the greeter first) take the town's stands in a seeded order; before, 4 stood on random start-screen spots.
  - So each town has 7 people, more than any other screen of its face (tested on seeds 1-50 in PlayMode).
  - Leaders now say two lines: their greeting, then their region's quest hook in voice (`Dialogue.QuestLines` / `QuestLine` / `LeaderConversation`: "bring back what was lost ... mend it"). *Assumption:* the hook replaces a leader's hint line (`MaxLines` stays 2). The hook for 4.8 is `Dialogue.QuestLine(race)` and `TownSquares.LeaderOf(face)` / `TownPopulation.Leader`. `voices-and-names.md` notes it.
- **Map:** a new `MapMarkerKind.Town` (an orange house on a dark backing, top middle of the screen) marks every visited face's town. The legend gains "Town".
- **Determinism:** re-recorded twice; the two recordings are byte-identical, and `DeterminismTests` passes.
  - Changed (50 seeds each): `layout` (the town module index on every centre, a few cores redrawn); `items` and `hidden` (the centre's slots are gone, so every draw shifts); `biologyPlan`, `chemistryPlan`, `physicsPlan`, `mathPlan` and `earthPlan` (they follow the placement); and `biologyPopulation`, `chemistryPopulation`, `physicsPopulation`, `mathPopulation` and `earthPopulation` (the screen order now drops the town).
  - New: `townNpcs` (residents and leaders).
  - Unchanged: `model`, `npcs`, `mathNpcs`, `earthNpcs`, `town` and `streams`.
- **Tests.**
  - New EditMode `TownSquareTests` (7 tests):
    - every race's town (style, statue, one marked door, lanes and doorways clear);
    - the library's town modules;
    - seeds 1-50: every centre is its town and nothing is placed there;
    - displaced content still turns up (cores, gates, pickups, every trial, islands, doors and boulders, the calm eye);
    - the leaders' quest hooks;
    - the Town face's stands;
    - the town art's pixel style.
  - New PlayMode `TownSceneTests.EveryFacesCentre_IsItsTown_...OverSeeds1To50`: buildings, statue, marked door, leader near the statue saying the hook, more NPCs than any other screen of the face, no enemies, lanes free of town solids (about 10 s).
  - Updated:
    - module filters skip town modules (`CubeLayoutTests`, `ItemPlacementTests`, `MetaRunTests`, whose fake catalog forwards `TownModuleIndex`);
    - the map scene test expects a town marker on every face's centre;
    - the F5 test counts the Mayor;
    - `CubeSceneTests`: the F2 slot check now uses a ruin screen beside the town for gate and hidden slots, and the west wall probe for a plain screen moved to (2,1), and new probes test the town's west and east walls above its buildings (review patch);
    - the voice and text scans include the residents and the quest lines.
- **Screenshots** (seed 7, one town per race) are in the scratchpad's `snaps45/`. They came from a temporary snapshot test, since deleted.
- **Regenerated:** the art catalog, the module library (6 new town-square prefabs; the others only change object IDs) and the Cube scene.

## Verification

**Result (2026-10-07, `vpart.sh` steps in the foreground as `v45b`):**
- EditMode 282/282 (7 new).
- PlayMode batches 25/22/26/27/38/25/1, all passing. Batch 5 has the new town scene test.
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s (unchanged).
- `IslandWalkTests`: 0 problems.
- Sweep 50/50, 0 failing, towns 300/300.
- `FIXTURE CHANGED` is the intended re-record described above.
- Not committed.

**Result after the review patches (2026-10-07, `vpart.sh` steps in the foreground as `v45c`):**
- EditMode 282/282.
- PlayMode batches 25/22/26/27/38/25/1, all passing.
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s.
- `IslandWalkTests`: 0 problems.
- Sweep 50/50, 0 failing; towns: 300 checked (the Town face's centre is checked too).
- Fixture unchanged since v45b (`FIXTURE CHANGED` is against the baseline), and `DeterminismTests` passes.
- No builder or scene regeneration was needed.
- Not committed.

## Review Triage Log

Pass 1 (quick lens), triaged by the coordinator: high 0, medium 0, low 9; all patched, plus one note.

| Verdict | Route | Finding | Outcome |
|---|---|---|---|
| low | patch | Shift+F3's debug leaders used index 0 upward, so the first batch equalled the town leaders | Patched: `CubeDebug.DebugLeaderIndexBase` (0x1000) plus the batch; town leaders use `TownSquare.LeaderIndex` (0). Tested for 4 batches per race. |
| low | patch | `Dialogue.Lines` / `FlavourLine` gave a leader the old line and hint while `For` gave the quest conversation | Patched: `Lines` is the one source (a leader always gets `LeaderConversation`), `For` goes through it, and `FlavourLine` is its first line. Tested. |
| low | patch | `TownPopulation`'s Mayor and stands were keyed on `HasTown(faceSize)` | Patched: keyed on the placed start module being a town square (`TownPlan.Spots` takes an `isTown` callback; the pure default is the rule). |
| low | patch | The map legend (9 labels) did not fit 960 px | Patched: it wraps into rows of columns at least `CubeMap.MinLegendStep` (150 px) wide: 2 rows of 5 at 960 px. The net keeps `LegendHeight` free. EditMode test. |
| low | patch | The Earth plaza and the calm-eye ring shared sorting order -9 | Patched: the plaza is at -8 (`TownSquareView.PlazaOrder`, the rug at -7), over the ring (`EarthFace.EyeOrder` -9), every time. |
| low | patch | The sweep's "Towns" line counted modules, not checks, and the Town face's centre went unchecked | Patched: `FindTownProblems` also checks the Town face's centre module (town square, Town theme, no slots, sound layout) and counts the towns it checked (`SeedResult.TownsChecked`). The report and the sweep summary now name only what is checked. |
| low | patch | The per-face text scans (`FaceText`) missed the quest lines | Patched: `FaceText.Flavour` yields `QuestLine`. |
| low | patch | Tests: no wall probe on the town; the "beside" screen was diagonal; the trial check did not prove a trial was displaced; the determinism `Screens` reused the code under test | Patched: wall probes on the town's west and east walls, above its buildings (5.6 up). The F2 check uses (0,1) west of the town. The test replays the old Chemistry and Physics trial draws, asserts some seeds would have used the centre, and checks the replay matches the new plan where it did not. `DeterminismTests.Screens` is an independent copy again (shuffle, then drop the centre). |
| note | defer | `TownSquares.LeaderOf` and `TownPopulation.Leader` freshness across rebuilds | For the 4.8 plan (coordinator). |
