---
title: 'Temples'
type: 'feature'
ticket: '14'
created: '2026-10-08'
status: 'built'
baseline_revision: 'a5f7880'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'coordinator'
lenses_ran: ['quick']
review_loop_iteration: 1
context:
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/faces-and-races.md'
  - '{project-root}/_bmad-output/initiative-video-game/change-playtest-3-world-redesign/change-playtest-3-world-redesign.md'
  - '{project-root}/_bmad-output/initiative-video-game/epic-world-worth-exploring/puzzle-difficulty-research.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** On 2026-10-08 the user redesigned the temples: "Temples become unlocked when you fix all the fregments of a face by collecting them and giving them to the village leader. The temples should contain a difficult puzzles based on the puzzle of that face, then when you complete the puzzle some magic fixes the fregements and gives you a gem that gives you an upgrade for the boss."

**Approach:**
- **The temple.** Each face's town has a temple building, drawn in the race's own style. It stays sealed until all of the face's fragments have been handed to its leader. Handing them in no longer grants the boss benefit directly: the leader opens the temple instead.
- **Inside the temple.** The temple is an interior sub-screen, following the 4.6 interior and 4.13 lair pattern. It holds one difficult puzzle based on that face's own puzzle:

  | Face | Temple puzzle |
  |---|---|
  | Math | A late-tier "light every plank" bridge puzzle |
  | Earth | A late-tier ice or wind puzzle |
  | Physics | A hard boulders-and-timed-door puzzle |
  | Chemistry | A hard stone-order puzzle |
  | Biology | A hard beak-target puzzle |
  | Town | A short mixed puzzle (assumption: Town has no puzzle of its own) |

  Each puzzle is solver-checked, or proven solvable by construction, at a difficulty at or above the face's hardest tier in the research note.
- **The reward.** Solving the puzzle plays a short magic moment: the fragments mend and the face is repaired. The player receives that face's **gem**, which grants the face's boss benefit (the 4.8 mapping). The HUD and map show gems.
- **Getting out.** The player can always leave the temple, and the puzzle resets on leaving until it is solved.

</frozen-after-approval>

## Boundaries & Constraints

- The seed sweep stays at 50/50. Re-record determinism, with a note.
- The subtle-science and voice rules still hold (InWorldText, voices-and-names.md).
- The readability tests stay green.
- PlayMode tests run in class batches.
- Nothing from later Epic 4 stories, except the hooks they need.

## Tasks & Acceptance

- [x] The leader hand-in unseals the temple. The face is repaired and its benefit granted only through the temple gem. Update the 4.8 tests and the lair hint wording ("mending X" becomes the temple gem).
- [x] Temple building per town, plus a temple interior sub-screen, with art in the race's style.
- [x] One hard puzzle per face, reusing that face's generators or solvers at the top tier (or above), with a solvability check per seed.
- [x] Solve effect: fragments mended, gem granted, benefit applied in the arena; gem HUD and map.
- [x] Sweep: every temple puzzle on seeds 1–50 is solvable. EditMode check over seeds 1–500 for the generated ones.
- [x] Tests: hand-in opens the temple; a runtime test solves one temple per face type; the gem changes its arena parameter. Run a full verify. Screenshots.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
- **Two steps per face** (`FaceRepairs`): `Unseal` (the hand-in) and `Repair` (the gem). `IsRepaired` = `HasGem`; `Benefits` still reads the repaired faces, so the arena code is unchanged and a gem is what grants the 4.8 benefit. `CubeWorld.UnsealTemple` opens the town's temple door; `RepairFace` is now called only by the temple's solve.
- **Hand-in** (`LeaderQuest`): with all k held the leader returns `QuestStep.Unsealed` and says `Dialogue.UnsealTemplates` (in voice: the temple is open, solving it mends the fragments, its gem's help in plain words). While the temple is open but unsolved: `QuestStep.TempleOpen` (`TempleOpenLines`); after the gem: `Mended` (`MendedLines`, now mentioning the gem). `RepairTemplates`/`RepairConversation` are gone. The hand-in completes the leader's quest (Town: its side quest) and adds the temple quest (`QuestLog.TempleId`, `QuestKind.Temple`), done when the gem is taken (`TempleView.Solve`, and `RefreshLeaders` as a backstop).
- **The temple building**: every town's fourth building (east of the crossroads, `TownLayout.TempleBuilding` = 3) is `TownBuildingKind.Temple`, same footprint as before, so `TownPlan` spots and the town signature are unchanged (Town keeps its market stall; each science town loses one of its two alternating house kinds there). `TempleArt` draws a temple per race in three looks (sealed: seal and chains; open: glowing doorway; mended: gem over the door). `TownSquareView.TempleUnsealed` / `Repaired` switch the look; a `TempleDoor` marks the sill.
- **The room** (`TempleRooms` on the world object, `TempleView`, `TempleRoom`): a whole screen per face on row -12 (below the lair rooms), the race's interior tiles, walls with one south doorway. `CubeNavigator.EnterInterior(..., temple: true)` sets `InTemple` (also `InInterior`); `IceSlider` and `WindRider` now drive inside a temple. Going in uses `TownInteriors.AtDoor` (now also by sill and facing); a sealed door says `TempleText.SealedPopup` (3 s cooldown). Arrival = `IslandLayout.Arrival(South)`; leaving = past the screen's south edge, back to `ReturnPoint` (in front of the door, facing south). Both ways call `StopUntilFreshPress` (the 4.13 held-key fix); the return point is checked clear of every race's town solids and wander boxes. F8 leaves, Shift+F8 solves (debug), any teleport leaves. **Reset:** leaving an unsolved temple (on foot, F8, teleport) destroys its room, so it is rebuilt fresh; a solved room stays.
- **Puzzles** (`TemplePlan`, pure, new stream `RngStreams.Temples` = 31, seeded by run seed and face, cached). All at the late tier:
  - **Math:** `IslandLayout.Generate(late, doorways N+S, no trial)`. *Assumption:* the network is made for a north and a south doorway, the north one's island standing against the temple's solid north wall; south-only found no design on the lattice (0/20 in the dotnet harness). Template: `TempleTemplates.IslandText`.
  - **Earth:** one draw picks ice or wind (25/25 on seeds 1-50). `IceLayout.Generate(late, south only)` (template `TempleTemplates.IceRows`, because the 4.10 late template fails with one doorway); `WindLayout.Generate(late, south only)` (its own late template holds).
  - **Physics** (`TempleBooths`, dealt, `PhysicsTrial` builds it): two booths, each door needing 3 boulders, exactly 3 shared boulders. Each switch stands across the room level with its door's threshold, so the dash is a straight keyboard line (the late slack 1.14 leaves no room for an 8-way detour). `Problems` proves timing (open with 3, shut at least 10% early with 2), runway clear of booths, fields apart, starts outside every field, room for 3 boulders per field off the runway. *Assumption:* the temple lends its grip (`MassMitt.TempleGrip`: E grabs without the Heavy Mitt\*).
  - **Chemistry / Biology / Town** (`TempleOrderSpec` + `TempleTargets`, dealt): targets on a 7x3 grid (3 apart, clear of the altar and the lane in), struck in an order given by a rule (clue level 2): stones brightest first (6, with a timer of max(6 s, 1.5x the best route)), seeds smallest first (6), Town's tokens fewest pips first (4, each token in a different science face's colour). A wrong strike (or the timer) puts all back with no damage. One swing striking several targets counts only the nearest. *Assumptions:* any swing counts in a temple (no face item needed); **Town's "short mixed puzzle"** is the four mixed tokens (Town has no puzzle of its own).
- **Difficulty** (seed 1, `Stats`; the sweep prints them): islands 13 planks, 12 islands, 2 cycles, 2 odd (none at a doorway), P_rand 9.3%, 8 trails; ice min 11 slides, 42 stops, P_rand 3.2%, 6 pits, 1 red herring, shortest 3; wind (seed 2) 8 regions, 10 streams, min 5 rides, 3 decoys, return 2, P_rand 14.4%; booths 2 booths x 3 boulders, 6 box lines, after the review patch, dashes 1.38-1.51 s (on the line, to the switch's far side), the door open 1.04x the longest dash and one short shutting 10.2% before the shortest, least drag 20.2 tiles; stones 6, timer 8.5 s = 1.58x the 5.38 s route, P_rand 1/720; seeds 6, P_rand 1/720; Town 4, P_rand 1/24. The generators' own targets (IceTargets/WindTargets/IslandTargets late) are what `Problems` enforces.
- **Solve moment** (`TempleView.Solve`, `TempleMagic`): the face's k fragments (hovering dim round the altar or the goal) spiral in for 1.2 s, mend into the gem with a burst (0.5 s), and the gem flies to the player (0.7 s). The repair is immediate (popups: "... is mended!", "+1 gem: <benefit>"); the statue turns gold.
- **HUD and map:** the die gets a pale bar when the temple is open and a gem diamond in the region's colour on a gold die once won; `FragmentsText` reads "2/4", "temple" or "gem"; the map tag likewise ("temple" lilac, "gem" gold); the map badge inside reads "Inside the temple"; the core HUD says "Help from your gems: ...".
- **Lair hints:** `LairText.BenefitFact` now says "the gem from the temple of <region> ...". Scans (`TempleTests`, the existing text and voice scans) read the new lines.
- **Sweep:** `SeedSweep` runs `TemplePlan.FindProblems` on every seed (`TempleProblems`), and the builder prints a "Temples" line with the kinds and sample stats. Result: every temple solvable on 50/50 seeds, 300 temples, 0 templates.
- **Determinism:** re-recorded twice, byte-identical. New: `temples` (50). Changed: `streams` (50: the `temples` stream appended). Everything else unchanged (the town and lair signatures carry no building kinds or hint text).
- **Tests:**
  - New EditMode `TempleTests` (11): every temple on seeds 1-50 solvable and of its face's kind (no template); islands, ice and wind generated over seeds 1-500 with no template and no problem (about 65 s); the templates; the ordering specs over seeds 1-500 (rule, spacing, timer >= 1.5x); the booths over 200 seeds (3 open, 2 shut early, drag <= 30 tiles); unseal vs gem and the benefit (gap height); the quest log; one temple per town with a clear return point and 18 distinct looks; pixel style; rooms off the net; the words.
  - New PlayMode `TempleSceneTests` (11, 13 after the review, new batch 7 in `vpart.sh` and `verify.sh`): the sealed door turns the player away; the Mayor's hand-in unseals; in with W held (no bounce), out with S held (back at the return point, facing away, no re-entry); an unsolved temple resets (on foot and by teleport), a solved one stays; the ordering temples (Town, Biology, Chemistry) solved with real walking and Enter swings after a wrong strike; the stones' timer; the ice and wind temples solved with real keys along the solver's route; the island temple lit plank by plank with real keys; the Physics temple: a boulder grabbed with E without the mitt and dragged, then each door opened by a real dash with 3 boulders (placed by teleport), and one short stays shut; the gem changes the arena gap while an open temple does not.
  - Updated: `FaceRepairTests`, `FaceRepairSceneTests` (the hand-in unseals, the temple's gem mends), `QuestSceneTests`, `LairTests`, `RngStreamsTests`, `TownSquareTests` and `TownInteriorTests` (the temple kind and badge), `DeterminismTests`. The skill check (no gems) is unchanged: 78.6, 90.5 and 105.5 s.
- **Regenerated:** the Cube scene (the `TempleRooms` component). No builder assets or catalogs changed.
- **Docs:** `player-guide.md`, `voices-and-names.md`, `ART-MAP.md`.
- **Screenshots** (seed 7; Earth's ice and wind on their first seeds) are in the scratchpad's `snaps414/`: each race's temple sealed, Town's open and mended, every interior puzzle (seeds, stones, booths, islands, tokens, ice, wind) and two frames of the magic moment. They came from a temporary snapshot test, since deleted. IMGUI (HUD, map) is not captured in batch mode; the tests cover it.

- **Review patches (pass 1):**
  - **The Physics dash is fair from anywhere on the switch.** The switches stand on a dash line below the booths (`TempleBooths.DashY`), and each door latches when the player reaches its **latch band** (`LatchBand`, a trigger strip across the line beside the booth, on the switch's side, as tall as the whole switch-touch spread). The corridor of every parallel dash (switch radius plus player radius either side, `Corridor`) is clear of both booths, and the door's time comes from the dash (`TempleBooths.BaseSeconds`, set on the door by `TempleView`): with 3 boulders it is open for the longest dash (leaving from the switch's far side), with 2 it shuts at least 10% before the shortest (leaving on the line). The dash is drawn on the floor (gold dashes and the band). `FieldRoom` keeps the boulders' room off the corridor.
  - **Popups:** a wrong strike shows only "Wrong order. They all go dark." (no rule), replacing the last one; the rule is said once per attempt (`TempleView.RuleSaid`); the hand-in's popups stand a line apart (unseal, quest done above, the temple's quest above that; a quest given and done at once below), and the solve's too.
  - **The temple's grip** shows its own HUD line without the mitt: "[Interact] grab a boulder" / "[Interact] drop the boulder" (`MassMitt.TempleGrabText`, `TempleDropText`, in the text scans).
  - **The temple's puzzle is made at the unseal** (`CubeWorld.UnsealTemple` warms `TemplePlan`'s cache), never on the first frame inside.
  - **The Shape trail person** says Town's temple opens (`QuestLines.Give`).
  - **Tests:** `TempleSceneTests.WalkIn` unseals through the real hand-in (fragments, then the leader's `LeaderQuest.Talk`), so every family's run checks the temple quest joins the log and is completed on the solve; the ordering run checks a second wrong strike replaces the popup and the rule is said once. New: `APhysicsDash_FromTheSwitchsOuterEdge_StillMakesIt_WithThreeBoulders` (pressed 0.55K above the line, dashed straight, latched on both doors) and `ThePhysicsTemple_IsSolvedWithTheRealKeys_AllThreeBouldersDraggedFromFieldToField` (all three boulders dragged with E and the keys into one field, a dash, then all three dragged over to the other field, a dash). The EditMode booth test checks the dash timing, the corridor and the band on 200 variants.
  - Determinism re-recorded twice (byte-identical): only `temples` (50) changed against the first 4.14 recording (the switches moved onto the dash line).

## Verification

**Result (2026-10-09, `vpart.sh` steps in the foreground as `v414b`):**
- EditMode 389/389 (11 new in `TempleTests`).
- PlayMode batches 39/29/54/28/38/37/4/11, all passing. Batch 7 is the new `TempleSceneTests`.
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s (unchanged; no gems).
- `IslandWalkTests` and `IslandTrailWalkTests`: passing.
- Sweep 50/50, 0 failing. Temples: every temple solvable on 50/50 seeds (300 temples: Islands 50, Ice 25, Wind 25, Booths 50, StoneOrder 50, SeedOrder 50, TownOrder 50; 0 from a template).
- `FIXTURE CHANGED` is the intended re-record described above (two recordings byte-identical), and `DeterminismTests` passes.
- Not committed.

**Result after the review patches (2026-10-09, `vpart.sh` steps in the foreground as `v414c`):**
- EditMode 389/389.
- PlayMode batches 39/29/54/28/38/37/4/13, all passing. Batch 7 has the 2 new Physics tests.
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s (unchanged).
- Sweep 50/50, 0 failing; every temple solvable on 50/50 seeds, 0 templates.
- `FIXTURE CHANGED` is the re-record (against the baseline; only `temples` moved since v414b), and `DeterminismTests` passes.
- Not committed.

## Review Triage Log

Pass 1 (quick lens), triaged by the coordinator: high 0, medium 1, low 6, all patched.

| Verdict | Route | Finding | Outcome |
|---|---|---|---|
| medium | patch | The Physics dash was unfair off the line: touching the switch near its edge sent the player into a booth wall or past the threshold | Patched: switches on a dash line below the booths; each door latches at a tall latch band beside its booth; the whole touch corridor clears both booths; the door's time comes from the dash (open for the longest, one short shuts 10% before the shortest); the dash is drawn on the floor. PlayMode test: pressed 0.55K above the line, both doors latch with 3 boulders. |
| low | patch | Popups stacked: wrong strikes, the rule, and the hand-in's quest notices | Patched: a wrong strike replaces the last popup and no longer repeats the rule; the rule once per attempt; the hand-in's and the solve's popups a line apart. Tested in the ordering run. |
| low | patch | The MassMitt HUD named the Heavy Mitt\* in the temple without it | Patched: "[Interact] grab a boulder" / "[Interact] drop the boulder"; tested and in the text scans. |
| low | patch | The temple's puzzle was generated on the first frame inside | Patched: made at the unseal (`CubeWorld.UnsealTemple`). |
| low | patch | The Shape trail person said Town is mended on the hand-in | Patched: "then Town's temple opens". |
| low | patch | The scene tests unsealed directly, so the temple quest's completion was not proven | Patched: `WalkIn` uses the real hand-in; every family's solve asserts the temple quest is done with its closing step. |
| low | patch | No Physics test dragged the boulders with real keys between the fields | Patched: `ThePhysicsTemple_IsSolvedWithTheRealKeys_AllThreeBouldersDraggedFromFieldToField`. |
