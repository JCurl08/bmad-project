---
title: 'Boss lair'
type: 'feature'
ticket: '13'
created: '2026-10-08'
status: 'built'
baseline_revision: '4fea984'
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

**Problem:** The user said: "Last thing is the different entrances to the boss, I would like only one to actually lead to the boss fight, the others lead to an underground part that functions as a part of the lair. Inside you can see people trapped and they can give you hints on how to defeat the boss."

**Approach:**
- **Portals:** one seeded core portal leads to the arena. Every other portal leads to an underground lair room: a sub-screen like the arena and the 4.6 interiors.
- **Prisoners:** each lair room holds trapped people from the races, behind bars, in their voices. They give hints about Primsworth the Demon (how the fight works) and about the repair benefits (which face gives what), and possibly where the real portal is.
- **Leaving:** the player can always walk back out.
- **Map:** shows lair rooms once visited and marks the real portal once it's known.

</frozen-after-approval>

## Boundaries & Constraints

- The seed sweep stays at 50/50. Re-record determinism, with a note.
- The subtle-science and voice rules still hold (InWorldText, voices-and-names.md).
- The readability tests stay green.
- PlayMode tests run in class batches.
- Nothing from later Epic 4 stories, except the hooks they need.

## Tasks & Acceptance

- [x] Real-portal roll on its own stream; lair rooms; exit back to the portal.
- [x] Prisoner NPCs with hint lines in voice (fight mechanics, benefits, the real portal's face).
- [x] Map lair markers.
- [x] Tests:
  - on seeds 1–50, exactly one portal reaches the arena and every other one reaches a lair room with a hint-giver;
  - the player can walk back out;
  - the suites and the sweep pass.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
- **The real portal** (`Core/LairPlan`, pure, new stream `RngStreams.Lair` = 29). Every laid-out science face has one core portal. `LairPlan.ForRun(model)` picks one as the real portal from the model alone, so `CubeWorld.Lair` is rolled on every rebuild (like the dice) and is known before the reveal. Draws, in order: the real face, a fight-fact offset, a benefit offset, then a race offset for each lair room.
  - `CorePortal.TryEnter` sends the player into the arena only through the real portal, and using it marks it known. Every other portal calls `LairRooms.Enter`. `CorePortal.LeadsToArena` and `CoreArena.RealPortal` are there for tests and tools.
  - *Assumption:* without a `LairRooms` in the scene, every portal still leads to the arena, so component-only scenes behave as before.
- **Lair rooms** (`Core/LairRooms` on the world object, added by the scene builder; `LairRoom`, pure; `LairRoomView`). They follow the 4.6 interiors pattern:
  - Each face has its own screen, `RoomScreen(face)` = (2 x face, -9), one row below the town rooms.
  - `CubeNavigator.EnterInterior(..., lair: true)` sets `InLair` as well as `InInterior`. `Current` stays on the portal's screen, so the map, the visit fog, the hazards and the edge check need no other change. Any teleport clears it.
  - The shell is a town room's (`TownInterior` walls, exit gap, vestibule, `ExitLine`). The player arrives just inside the exit, facing north.
  - **Leaving:** walking south through the exit (or F8) puts the player `ReturnDistance` (2.25) from the portal, towards the screen's centre, facing that way.
    - That point is 0.975 clear of the portal's trigger whichever way the centre lies. `ReturnClearance` (0.5) is the floor, tested in every direction and on every module's core slot. So the player never falls back in until they walk into it again.
    - Going down and coming back both call `PlayerMover.StopUntilFreshPress` (review patch). A key still held (S, say) never walks the player straight back out of the room's exit, or back into the portal.
  - A room is built on first entry and lasts for the run; `world.Rebuilt` clears them all.
  - Four barred cells line the back wall: stone dividers and iron bars, one prisoner per cell, each wandering a small box behind their bars. `LairRoom.Problems` checks that every prisoner stays in their cell, and that from the walkway spot before each cell the prisoner is always within talking range and nearer than any other prisoner.
  - Two pillars stand in the hall. There are no gates, rewards or enemies.
- **Prisoners** (`LairPlan.PrisonersOf`, `LairText`): four a room, of four consecutive races from the room's race offset, so every voice is heard over the seeds.
  - Their parts come from `NpcFactory.ChooseParts` at index 9000 + 4k + i (k: the room's order), and their names from their voice's pool. This keeps the name ordinal under 16, so no name carries a numeral (tested).
  - The label reads "Name the <race> captive".
  - Prisoners have no race relations, so they are never hostile. F4 does nothing down in a lair room, and `ToggleNearestHostility` skips prisoners anywhere, so a prisoner never flips `world.Relations` (review patch).
  - Each says two lines: a captive line in their voice, then one true hint. The hint is the voice's full-density hint opener (`HintGenerator.VoiceTable`, picked by salt), then a fact worded the same in every voice, then the opener's end.
    - Prisoner 0 tells a fight fact, a different one in each room, from six:
      - mixing the warm and cool motes beats him;
      - the gaps at both ends of his wall stay open;
      - he tidies fast but tires;
      - he glows and draws a line before he shoots;
      - a knock makes him lose his grip on a mote;
      - there is a door out in the west wall.
    - Prisoners 1 and 2 tell what mending a face gives, following `BossBenefits.Of`. Room k tells benefits 2k and 2k+1 (offset), so three or more rooms tell all six.
    - Prisoner 3 names the region whose portal is the real way down.
  - *Design call (the optional hint):* every lair room has a prisoner who names the real portal's region, so one wrong turn is enough to find the right portal.
- **Map** (`MapMarkers.PortalDetail`, `CubeWorld.LairProgress`, fresh each run). A portal marker's `Detail` is one of three:
  - `PortalUnknown`: the plain pink glyph.
  - `PortalLair`: once the portal's lair room has been visited, a dim ring with three dark bars.
  - `PortalReal`: once the real portal is known, a gold ring round a pink core (`Done`).
  - *Assumption:* the real portal is "known" once the player talks to the prisoner who names it (`LairRoomView.Told`), or goes through it into the arena. Merely entering a lair room does not count.
  - Once known, the real portal's marker shows even on a screen the player has not visited (`MapMarkers.Visible`, review patch). Only the marker shows; the screen stays fogged.
  - Down in a lair room, the player marker stands at the portal, with an "In the Demon's lair" badge in the header (the 4.6 badge slot).
  - The legend gains "Portal" and "Lair", and "Core portal" now means the known real one: 13 labels. `MinLegendStep` drops from 150 to 130, so the legend stays at two rows at 960 px; the longest label (12 characters) still fits.
- **Debug:**
  - The F1 overlay reads "In the lair below Face … (off the cube net)".
  - F3 and F7 spawn on the room's walkway, never in a cell (`CubeDebug.SpawnBounds`).
  - F4 does nothing (see Prisoners).
  - F8 walks the player out beside the portal.
  - F5, F6 and F10 take the player out like any teleport.
  - Shift+F9 uses the portal's face.
- **Sweep:** `SeedSweep.Run` runs `LairPlan.FindProblems` wherever the core is checked. It checks:
  - exactly one portal goes to the arena, on a laid-out face whose core screen is reachable, and every other leads to a lair;
  - every room has four prisoners with a fight hint, benefit hints and the real portal's region;
  - every line keeps the text rule and the length limit;
  - all six benefits are told.

  The builder gains a "Lair" line. The real portal counts as "proven reachable" only on seeds where the lock check actually ran and passed (review patch). The lock check covers every core portal under every key-spending order.
- **Determinism:** re-recorded twice, and the two recordings are byte-identical.
  - New: the `lair` lines (the real face and each room's prisoners).
  - Changed: the `streams` lines, with the `lair` stream appended; the earlier streams' draws are unchanged.
  - Every other signature is unchanged.
- **Skill check:** it still enters the arena by the controlled entry (`CoreArena.Enter`), which the lair leaves alone, at the same times (78.6, 90.5 and 105.5 s). The PlayMode tests that went through `Portals[0]` now use `arena.RealPortal`. The every-face portal walk expects the lair for every face but the real one.
- **Text:** every new line is in `Dialogue.SpokenLines` and `AllFixedText` (`LairText`), so the voice, length, banned-word and star scans read them. The map badge joins `CubeMap.AllTexts`. `voices-and-names.md` and `ART-MAP.md` are updated.
- **Art** (`Art/LairArt`, drawn in code: Kenney's palette, 8 px a unit, point filter):
  - a slate flagstone floor, dark brick walls and straw cell floors;
  - thin, sparse iron bars: a pale bar and its plum edge every 10 pixels, at least three quarters see-through, with a thin top rail;
  - the bars are only 1.3 units tall (`LairRoom.BarsArtHeight`), and the prisoners stand further forward in their cells (feet at y 2.2 to 2.5). The bars cover at most the lower half of each design, so the whole prisoner reads (review patch);
  - pillars, torches and a pink way-up swirl in the vestibule;
  - the tile pixel arrays are drawn once and cached, like the sprites (review patch).
- **Tests:**
  - New EditMode `LairTests` (10, 2 of them from the review):
    - seeds 1–50: exactly one portal leads to the arena and every other into a room with hint-givers, and every science region holds the real portal on some seed;
    - the plan's seeding and variety;
    - the prisoners: their voices, the text rule, true facts, no numerals in names, every benefit and fight fact told, a different fight fact in each room;
    - the room layout;
    - the rooms sit off the net, clear of the arena and the town rooms; the return point and facing;
    - `FindProblems` flags a missing plan and an unreachable real portal;
    - the map's legend and badge;
    - the art's pixel style and the cached pixel arrays;
    - review: the bars are at least 75% see-through and cover at most half a prisoner;
    - review: the return point clears the portal's trigger by `ReturnClearance` in every direction, and on every module's core slot stays inside the screen's walls, clear of its solids and gate slots.
  - New PlayMode `LairSceneTests` (5, 1 of them from the review), in batch 5 in both `vpart.sh` and `verify.sh`:
    - seeds 1–50 in the real scene, trying every portal:
      - exactly one reaches the arena; the fight starts and the portal becomes known;
      - every other reaches its lair room, with four prisoners saying their planned lines;
      - the player walks back out to the portal's screen.
    - stepping into each lair portal on seeds 7, 21 and 42, then walking out with S:
      - the camera frames the room's screen, off the net;
      - every prisoner is in range from their bars;
      - the player comes back beside the portal, facing away, with no re-entry.
    - review: walking south into a lair portal with S held:
      - the player goes down and, with S still held for a second, stays at the arrival point;
      - a fresh S walks out, and with S still held the player stays beside the portal.
    - the map, with no `VisitAll` first:
      - an unvisited lair portal shows as unknown, and a visited one barred;
      - inside, M shows the lair badge, with the marker at the portal;
      - a fight hint does not reveal the way, but the prisoner who names it does;
      - the known real portal's marker then shows on its unvisited screen, the screen stays fogged, and no other marker shows on an unvisited screen;
      - a new run forgets it all.
    - the debug keys inside; F4 turns no race.
  - Updated:
    - `CoreSceneTests`: the real portal; every other face leads to the lair;
    - `RunLoopSceneTests`: the real portal;
    - `CubeMapTests`: two legend rows with 13 labels;
    - `RngStreamsTests`: stream 29;
    - `DeterminismTests`: the `lair` line.
- **Regenerated:** the Cube scene (the new `LairRooms` component, wired to the arena and the debug tools). No builder asset changed.
- **Screenshots** (seed 7: each lair room on arrival and at the bars, a portal screen, and walking out) are in the scratchpad's `snaps413/`. The ones after the visual patch are `after-*.png`. They came from a temporary snapshot test, since deleted. IMGUI (the map and the dialogue box) cannot be captured in batch mode.

## Verification

**Result (2026-10-08, `vpart.sh` steps in the foreground as `v413a`):**
- EditMode 365/365 (8 new).
- PlayMode batches 39/29/46/28/38/36/1, all passing. Batch 5 has the 4 new `LairSceneTests`.
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s (unchanged).
- `IslandWalkTests`: passing.
- Sweep 50/50, 0 failing.
  - Lair: exactly one portal leads to the arena on 50/50 seeds. The real portal by region: Biology 13, Chemistry 8, Physics 7, Math 9, Earth 13.
  - 200 lair rooms with 800 prisoners, and the real portal is proven reachable on 50/50 seeds.
- `FIXTURE CHANGED` is the intended re-record described above, and `DeterminismTests` passes.
- Not committed.

**Result after the review patches (2026-10-08, `vpart.sh` steps in the foreground as `v413b`):**
- EditMode 367/367 (2 more).
- PlayMode batches 39/29/46/28/38/37/1, all passing. Batch 5 has 1 more `LairSceneTests`.
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s (unchanged).
- `IslandWalkTests`: passing.
- Sweep 50/50, 0 failing.
  - The lair line is the same as in v413a.
  - The real portal is now counted as proven reachable only where the lock check ran: 50/50 seeds.
- The fixture is byte-identical to v413a. `FIXTURE CHANGED` is against the baseline, and `DeterminismTests` passes.
- Not committed.

## Review Triage Log

Pass 1 (quick lens), triaged by the coordinator: high 0, medium 2, low 3, plus one visual item. All patched.

| Verdict | Route | Finding | Outcome |
|---|---|---|---|
| medium | patch | A held key bounced the player out of a lair room (the exit is just south of the arrival point), or back into the portal on leaving | Patched: `LairRooms.Enter` and `Leave` call `PlayerMover.StopUntilFreshPress`. The return point is 0.975 clear of the portal's trigger in every direction (`ReturnClearance` 0.5 is the floor). PlayMode test: holding S into a portal entered southwards, the player stays down; holding S on the way out, they stay out. EditMode test: the clearance in every direction, and on every module's core slot (inside the walls, clear of solids and gate slots). |
| medium | patch | The map showed the known real portal only on visited screens; the map test hid this with `VisitAll` | Patched: `MapMarkers.Visible` keeps the known real portal's marker on any screen, and the screen stays fogged. The map test no longer calls `VisitAll`; it checks the unvisited screen, the fog, and that nothing else shows there. |
| low | patch | F4 in a lair could flip `world.Relations` from a prisoner's race | Patched: F4 does nothing in a lair, and `ToggleNearestHostility` skips prisoners anywhere. The plan line is corrected, and the debug test checks that no race turns. |
| low | patch | The sweep report counted the real portal as proven reachable where the lock check had not run | Patched: it counts only where the lock check ran and passed. |
| low | patch | `LairArt` redrew the tile pixels on every call (only the sprites were cached) | Patched: the pixel arrays are drawn once and cached; tested. |
| visual | patch | Prisoners' bodies were mostly hidden behind the bars | Patched: the bars are thin and sparse (at least 75% see-through) and only 1.3 units tall with a thin rail, and the prisoners stand further forward, so each whole design reads (tested; `snaps413/after-*.png`). |
