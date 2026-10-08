---
title: 'Enterable buildings'
type: 'feature'
ticket: '6'
created: '2026-10-07'
status: 'built'
baseline_revision: 'b3510ec'
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
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** the user asked for "buildings you can enter" in each town (playtest 2).

**Approach:** in every town, at least the building with the marked door from 4.5 can be entered.
- Walking into the door loads an interior room: a sub-screen with walls, furniture in the race's style, and people (the leader's hall, a shop or a home).
- Walking out returns the player to that door, facing away from it.
- Interiors are a sub-screen system: they're not on the cube net, and the map shows the town screen while you're inside.
- One interior per town holds a shop counter. Meta-currency spending stays on the between-runs screen; the counter is decor plus a talkative keeper, with a hook for later epics.

</frozen-after-approval>

## Boundaries & Constraints

- The sweep stays 50/50 on seeds 1–50. Re-record determinism with a note.
- Subtle-science and voice text rules hold (InWorldText, voices-and-names.md).
- Readability tests stay green.
- PlayMode runs in class batches.
- Nothing from later Epic 4 stories, except the hooks they need.

## Tasks & Acceptance

- [x] Sub-screen/interior system (enter, exit, camera, navigator state, map).
- [x] An interior per town in the race's style, with NPCs.
- [x] Tests: enter and exit returns to the door on several seeds; the map stays correct; F-keys and the pause map work inside; the sweep and suites pass.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
- **The sub-screen system** mirrors the core arena.
  - `Town/TownInteriors` sits on the world object (added by the scene builder). Each face's room has its own screen below the faces: `RoomScreen(face)` = (2 x face, -6). The arena is at (0, -3), so `ScreenCamera` frames each room exactly.
  - `CubeNavigator` gains `InInterior`, `InteriorDoor`, `EnterInterior` and `PlaceOn`. While the player is inside, edge crossing is off and `Current` stays on the town's screen. As a result, the map, the visit fog, `HazardReceiver` (also off inside) and the debug tools need no other notion of where the player is. `TeleportTo` and `EnterCoreArena` clear the state.
  - **Going in:** every physics step, `TownInteriors` checks whether the player is walking into their town's marked door. They must be within 0.75 of the door's centre line, touching the sill, alive, with an enabled mover whose last physics step pushed at least 0.6 north into the door (`PlayerMover.MoveInput`, new; zero while waiting for a fresh press or disabled). Merely facing the door, being dead, or being frozen never pulls the player in (review patch). The player then arrives just inside the room's exit, facing north. Walking along the lane past the door, or into another building, does nothing (tested).
  - **Coming out:** the player walks south through the exit's gap, past the south wall's outer face (`TownInterior.ExitLine`). They land 0.5 in front of the door, facing south, away from it. Standing there does not re-enter, even turned to face the door, until the player presses north again. A layout check keeps the whole player circle at the return point clear of every race's town solids and every resident's and leader's wander box (review patch).
  - A room is built the first time it is entered and lasts for the run. `world.Rebuilt` removes them all. A new run (F10, F5 or the between-runs screen) or F6 takes the player out through the teleport.
- **Rooms** (`Town/TownInterior`, pure, and `TownInteriorView`): an 18 x 10 room with a taller back wall and one exit. The exit is a 2.2-wide gap in the south wall into a closed vestibule. There are 3 people of the town's race (index base 8000, named from their voice's pool), each wandering in a box that `Problems()` keeps clear of furniture, walls, the exit lane and each other.
  - *Assumption (one room per town):* the Town and Chemistry towns get a **shop**, Biology and Physics a **home**, and Math and Earth the **leader's hall**.
    - **Shop:** shelves, a counter, and a keeper behind it who says `Dialogue.KeeperLines`: a welcome, then that Jumbles\* (`InWorldText.Currency`) are spent between trips. The counter is decor, and `KeeperLines` is the hook for later stories. The keeper's label is "<Name> the <race> shopkeeper".
    - **Hall:** a throne on a dais, banners, pillars and a carpet, with three courtiers. *Assumption:* the throne is empty because the leader stays by the statue (4.5).
    - **Home:** a bed (a nest bed for the finches), a hearth, a table and stools, with a family of three.
    - Everyone except the keeper talks like the town's residents: a role line, then a run-true hint.
  - *Assumption:* the frozen intent's "one interior per town holds a shop counter" is read through the coordinator's brief, so each town's single room is one of the three kinds. Two towns have shops.
  - No gates, rewards or enemies (tested).
- **Art** (`Art/InteriorArt`, drawn in code in the `TownArt` and `NpcDesignArt` style: Kenney's palette, the two-pixel plum outline, point filter, 8 px a unit):
  - each race's floor and wall tiles;
  - furniture drawn at its rect's size, in the race's colours;
  - each race's keepsake (a barrel and crate, a seed perch, a bubbling pot, a telescope, a shape stack, a campfire).
  - `ART-MAP.md` documents it.
- **Map:** while the player is inside, the player marker sits at the door on the town's screen, with an orange "Inside the shop" (or hall, or home) badge in the map's header, between the title and the close hint, so it never covers a cell (review patch). It is 300 px wide, enough for "Inside the leader's hall" at 16 pt bold. `CubeMap.AllTexts` includes the three badges.
- **Debug:**
  - The F1 overlay names the room ("Inside the town's shop ... off the cube net").
  - F3 and F7 spawn inside the room.
  - F8 ("leave core/room") walks the player out of the door.
  - F6, F5 and F10 take the player out like any teleport.
  - With the map open, only F1 works, as before.
- **Determinism:** no new stream or signature. People's parts come from their own index (`NpcFactory.ChooseParts`), their names from the existing NpcNames stream, and the art from no draw at all. The fixture is unchanged.
- **Tests:**
  - New EditMode `TownInteriorTests` (7): every room is sound, each kind appears twice, the walls leave only the exit's gap, the rooms are off the net and clear of the arena (using the production `CubeWorld.FaceOrigin` and `ScreenCenter`), the keepers' lines (voice, text rule, the currency hook), the art's pixel style, the map badge's place and width, and the return point clear for every race.
  - The keeper lines join `Dialogue.SpokenLines` and `AllFixedText`, so the existing voice and text scans read them.
  - New PlayMode `TownInteriorSceneTests` (7), added to batch 5 in both `vpart.sh` and `verify.sh`:
    - walking in and out of every face's room on seeds 7, 21 and 42, with Town's before the reveal (it reveals nothing): the camera, `Current`, the people, the keeper, no enemies, the return point and facing, and no re-entry;
    - walking past the door, or into other buildings, does not go in;
    - the map inside: M pauses it, the town's screen and badge show, the marker sits at the door, and no new visits;
    - the debug keys inside.
    - review patches: standing at the door facing it (or dead against it) does not go in until the player pushes north; F5 inside; dying inside ends the run and the next run starts clean outside;
    - the walk-in test now runs with face enemies on: building or entering a room adds no enemy, and non-keepers' lines equal `Dialogue.For(...)`.
- **Regenerated:** the Cube scene (the new `TownInteriors` component). No builder assets changed.
- **Screenshots** (seed 7, one room per race, plus the Town's door just after walking out) are in the scratchpad's `snaps46/`. They came from a temporary snapshot test, since deleted.

## Verification

**Result (2026-10-08, `vpart.sh` steps in the foreground as `v46a`):**
- EditMode 287/287 (5 new).
- PlayMode batches 25/22/26/27/38/29/1, all passing. Batch 5 has the 4 new interior scene tests.
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s (unchanged).
- `IslandWalkTests`: 0 problems.
- Sweep 50/50, 0 failing.
- Fixture unchanged.
- Not committed.

**Result after the review patches (2026-10-08, `vpart.sh` steps in the foreground as `v46b`):**
- EditMode 289/289 (2 more: the map badge, the return point).
- PlayMode batches 25/22/26/27/38/32/1, all passing. Batch 5 has the 3 new interior scene tests.
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s (unchanged).
- `IslandWalkTests`: 0 problems.
- Sweep 50/50, 0 failing.
- Fixture unchanged.
- Not committed.

## Review Triage Log

Pass 1 (quick lens), triaged by the coordinator: high 0, medium 1, low 7, all patched.

| Verdict | Route | Finding | Outcome |
|---|---|---|---|
| medium | patch | `TownInteriorSceneTests` was in `vpart.sh` batch 5 but not in `verify.sh` | Patched: added to batch 5 of `verify.sh` too. |
| low | patch | Keeper lines said "coins", not the currency's name | Patched: built from `InWorldText.Currency` (Jumbles\*). The test checks for the name and that "coin" is gone. |
| low | patch | The map's inside badge under the town cell hid other cells | Patched: the badge is in the header (`InsideBadgeRect`), clear of the net, the title and the hint. An EditMode test checks its place at 3 widths and that the longest badge fits. |
| low | patch | Entry used the last facing, so a frozen or dead player facing the door could be pulled in | Patched: entry needs an enabled mover whose last physics step pushed north (`PlayerMover.MoveInput`) and a live player. PlayMode test. |
| low | patch | `TownInteriorTests` copied the face-origin formula | Patched: uses `CubeWorld.FaceOrigin` and `ScreenCenter`. |
| low | patch | Scene tests: the enemy check ran with enemies off; no test of the return point facing north; non-keeper lines unchecked | Patched: the walk-in test runs with face enemies and checks that rooms add none. A new test covers the return point facing north (and dead at the door). Non-keepers' lines equal `Dialogue.For`. |
| low | patch | No tests for F5 inside or the run ending inside | Patched: `F5Inside_...` and `DyingInside_EndsTheRun_...` (the next run starts clean outside, with no rooms left). |
| low | patch | The return point was not checked against the town's solids and residents | Patched: an EditMode check for every race covers the player circle against the town's solids and every resident's and leader's wander box. |
