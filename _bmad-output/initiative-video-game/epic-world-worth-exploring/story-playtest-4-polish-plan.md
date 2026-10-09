---
title: 'Playtest 4 polish'
type: 'feature'
ticket: '18'
created: '2026-10-08'
status: 'built'
baseline_revision: '2c22a74'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 1
context:
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/faces-and-races.md'
  - '{project-root}/_bmad-output/initiative-video-game/change-playtest-3-world-redesign/change-playtest-3-world-redesign.md'
  - '{project-root}/_bmad-output/initiative-video-game/epic-world-worth-exploring/puzzle-difficulty-research.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Playtest of 4.13, 2026-10-08. The user's notes:
1. "town fragement - make a side quest"
2. "players can't move during dialog and pause other players". The user confirmed this means: freeze the player and pause the other NPCs and enemies while a conversation is open.
4. "white face should be more white than grey"
5. "the farm looking people are kind of confusing and out of place". The user chose to replace them with a mix of all races.
6. "avian dinosaur is more like a penguin, if you can find a better sprite then please change it"
7. "The shape person is cute, maybe make them look less human like but I like the humpty dumpty style"
8. "Add a quest tracker. Each time someone gives you a quest add it to the log."
9. "Many NPCs have the same quest, maybe we can remove some of the duplicates."

**Approach:**
- **Dialogue freeze.** While a conversation is open:
  - the player can't move, swing or use items;
  - every other NPC and enemy pauses, including Demon shots and particles in the arena, and hazards;
  - wind, ice and the timers (Physics timed doors) hold too.
- **Quest log.** Every quest a character gives is added to a log you can view, either on the pause map or with a key such as Q. It holds:
  - leader quests, with their fragment progress;
  - the Town side quest;
  - NPC errands.

  Entries update as progress is made and are marked done.
- **Fewer duplicate quests.** Each errand or quest line is given by at most one NPC per face. Other NPCs give hints or flavour instead.
- **Town side quest (lost-and-found).**
  - The Mayor says Town's fragment went missing.
  - A seeded trail of 2–3 townsfolk across Town's screens each sends you to the next.
  - One of them wants a small favour, such as something from a Town chest or a word with another townsperson.
  - The last one hands over the fragment. The Mayor then mends Town.
- **Town look.**
  - The Town floor reads white, not grey, while keeping contrast.
  - Town's residents become a mix of all races.
  - Only the Mayor and the greeter (Hattie Hullo) keep a unique town look, without the farm or straw-hat style.
- **Avian dinosaur.** Redesign it to read as a feathered raptor or bird-dino, not a penguin. Use a Kenney tile if one fits, otherwise draw it in code.
- **Shape person.** Make it less human and more Humpty Dumpty: an egg or round geometric body with little limbs, keeping its charm.

</frozen-after-approval>

## Boundaries & Constraints

- The seed sweep stays at 50/50. Re-record determinism, with a note.
- The subtle-science and voice rules still hold (InWorldText, voices-and-names.md).
- The readability tests stay green.
- PlayMode tests run in class batches.
- Nothing from later Epic 4 stories, except the hooks they need.

## Tasks & Acceptance

- [x] Dialogue freeze (player, NPCs, enemies, hazards, timers, arena) with tests.
- [x] Quest log (data, HUD or map view, entries from every quest-giver, progress, done) with tests.
- [x] Deduplicate quest-giver lines per face. Add a test that no two NPCs on a face give the same quest.
- [x] Town lost-and-found side quest on its own RNG stream. The sweep proves it is completable on seeds 1–50. Add a PlayMode test that walks it.
- [x] Whiter Town floor; townsfolk become a mixed-race cast; new Mayor and greeter looks.
- [x] Avian dinosaur and shape person redesigns, with the readability tests updated.
- [x] Re-record determinism with a note. Run a full verify. Take screenshots.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
- **Dialogue freeze: one central talk pause.** `GamePause` now has two holders:
  - a menu (the map, the quest log): `Active`, as before;
  - a conversation: `Talking`. Any open `DialogueBox` holds it (`BeginTalk` on open, `EndTalk` on close, and on disable, so a destroyed box never leaves it held).

  Either stops time (timeScale 0, with the old time scale restored once nothing holds it). So every physics step, timer, hazard, NPC, enemy, timed door, ice and wind driver, and the arena (Demon, motes, shots) holds in one place.
  - *Assumption:* timeScale 0 is right because the box needs no time. It reads presses every frame, and its frame-count guards still work.
  - `GamePause.InputBlocked` (a menu, a talk, or the frame a talk closed in) is what the player's input checks: attack, item switching, the Heavy Mitt\* and locks. The press that closes a talk never swings or grabs.
- **Quest log** (`Quests/QuestLog`, `QuestText`, `QuestLogView`):
  - `CubeWorld.Quests` is a fresh log per rebuild; Town's trail progress lives in it too. So re-spawned characters (the reveal re-populates Town) carry on.
  - Entries are added when given, and hold the giver, title, step, progress and done:
    - a science leader's quest (`LeaderQuest`: on the first talk; live fragment progress via `RefreshLeaders` whenever the log opens; done on the hand-in);
    - Town's side quest (step by step);
    - each town's errand.
  - A popup ("New quest: …  [Q]", "Quest done: …") shows over the character. It waits out the talk pause.
  - `QuestLogView` is on the Cube Map object (scene regenerated):
    - Q toggles it, and it pauses like the map;
    - Q on the map swaps to the log, M from the log swaps back, Esc closes;
    - it is drawn in up to two columns (7 a column at 600 px tall), open quests first, done ones dimmed and marked "Done";
    - the map header shows "[Q] quest log" under its close hint.
  - *Assumption:* Q is read straight from the keyboard. No input action was added.
- **Fewer duplicate quests** (`Quests/Errands`, `ErrandQuest`):
  - Each town (Town and every laid-out science face) has exactly one errand: carry a word from one of the town's people to another.
    - On a science face, the people are the 6 residents.
    - On Town, they are the 5 face visitors on the square (never the greeter).
  - The giver is the first quest-giver by parts, else the first person, labelled a quest-giver (`Errands.Dress`). The target is the next person. There is no draw.
  - Every other quest-giver says their voice's new `SpareQuest` quip, then gossip.
  - *Assumption:* errands became completable "carry a word" errands, so the log can mark them done. The old errands were flavour with nothing to complete.
  - The face populations never spoke role lines, so they needed no change.
- **Town lost-and-found** (`Quests/TownTrail`, pure, new stream `RngStreams.TownTrail` = 30; `TownTrailQuest` in play):
  - The trail is 2–3 Town people past the square's stands, each preferring a different screen. One of them (seeded) first wants a favour: a word carried to a helper, then the answer brought back.
  - The Mayor says the fragment went missing and names who had it last, where they really stand. He then reminds who to ask next.
  - The last person grants `Reward.Fragment(1)` on Town. The existing hand-in (`LeaderQuest`, `QuestStep.Repaired`) mends Town, so 4.14's hooks are unchanged.
  - `QuestStep.Lost` is new. `Gave` stays in the enum but no longer happens.
  - *Assumption:* the favour is always "a word with another townsperson". Town chests are behind keys and locks, so a chest favour could block the trail.
  - Sweep: `SeedSweep.FindQuestProblems` checks the trail on seeds 1–50 against the planned Town spots (found spots on distinct reachable screens off the square, a helper, every line in the text rule and the box) and every town's errand. Result: completable on 50/50 (23 trails of 2, 27 of 3).
- **Town look:**
  - **Floor:** Town's floor gets a warm white glaze (`FloorPattern.GlazeAlpha` 0.7, `TownGlazeColor`) and a lighter checker (`CheckerShade` 6 % instead of 15 %). It reads about (233, 234, 234) and (219, 220, 220), not blue-grey (192, 203, 220). The contrast tests now compare against the floor as seen (`FloorPattern.Seen`) and stay green; a new test checks the white.
  - **Residents:** Town's residents are folk of every face race. Resident *i* takes the five races in turn from a seeded start (`TownPlan.ResidentRace`), with that race's parts, name and voice.
    - Town's shop people and keeper follow the same rule (`TownPlan.InteriorRace`).
    - *Assumption:* residents speak their own race's voice, like the visitors already did. The Town voice is now only the greeter's, the Mayor's, Sage Conkwell's and Town captives'.
  - **Greeter:** Hattie Hullo has her own design (`NpcLook.Greeter`): an auburn bun with a flower, a wave, and a dress under an apron.
  - **Mayor:** a purple top hat with a gold band, a white moustache, and a dark suit with the chain of office and a red sash.
  - **Generic Townsfolk:** the design for the odd townsperson elsewhere (Conkwell, captives) is a hooded traveller's cloak.
  - No straw hat is left anywhere.
- **Avian dinosaur:** a blue feathered raptor seen side-on, with a crest, a toothed beak, a dark wing, a long tail and long clawed legs. Its leader has a crown, plumes and a purple mantle.
  - *Assumption:* no Kenney tile fits, so it is drawn in code.
  - The dinosaur leader is always terrestrial (4.4), so the avian leader design is unused in play.
- **Shape person:** a Humpty-Dumpty egg in the accent colour, with the face on it, a belted middle with a gold buckle, little stick arms with gloves, and little legs. Its leader is a purple egg with a three-triangle crown and a ruler staff.
- **Determinism:** re-recorded twice, and the two recordings are byte-identical.
  - Changed: `town` (the mixed residents) and `streams` (the `townTrail` stream appended; the earlier streams' draws are unchanged).
  - New: `townTrail` lines.
  - Every other signature is unchanged, `townNpcs` included.
- **Tests (none removed):**
  - New EditMode `QuestTests` (8):
    - the log's add, step, done and order;
    - live leader progress;
    - the log's words under the text rule;
    - the layout at 960×600 (12 quests fit in two columns) and the map hint's place;
    - one errand per face, with no two people on a face giving the same quest, over seeds 1–50;
    - the spare quest-givers;
    - the trail on seeds 1–50;
    - the talk pause.
  - New EditMode `InWorldTextTests.TownFloor_ReadsWhite_NotGrey_OnBothCheckerShades`.
  - New PlayMode `QuestSceneTests` (7, batch 2 in `vpart.sh` and `verify.sh`):
    - the freeze with real presses (the player, the NPCs, fixed time; the box still pages);
    - enemies, a timed door's timer and the arena (Demon, motes, shots) holding;
    - Q, the map swap and Esc;
    - every quest joining the log, updating and being marked done (Town's errand, side quest, a leader, a face's errand);
    - Town's trail walked with real presses on seeds 7, 21 and 42.
  - Updated:
    - `ReadabilityTests`: the new looks' cues replace "straw hat"; the raptor is not mirror-symmetric; the greeter look; the race count.
    - `InWorldTextTests`: floors as seen; the Weightlands\* glaze keeps its colour.
    - `TownTests`: mixed residents.
    - `FaceRepairTests` and `FaceRepairSceneTests`: the Mayor reports the fragment lost, and a found fragment mends Town.
    - `TownSceneTests` and `TownInteriorSceneTests`: the expected lines, races and the errand-giver.
    - `RngStreamsTests` and `DeterminismTests`: stream 30.
- **Docs:** `player-guide.md` (controls, dialogue pause, Town's side quest, errands, quest log), `voices-and-names.md` (who speaks Town, Town's residents, the trail lines, errands) and `ART-MAP.md` (the new designs, Town's floor).
- **Screenshots** (seeds 7 and 21) are in the scratchpad's `snaps418/`:
  - `s7-town-square`, `s21-town-square`: the white floor, the greeter, the Mayor and the mixed folk;
  - `s*-trail*-<name>`: each trail person on their screen;
  - `lineup-town`, `lineup-math`: every design (row 1 folk, row 2 leaders, row 3 another accent; the greeter, then the townsperson, finch, mushroom, alien, shape, ground and avian dinosaur);
  - `earth-town`, `math-town`.

  They came from a temporary snapshot test, since deleted. The quest log is IMGUI, which batch mode cannot capture, so the tests cover it.
- **Review patches (pass 1, quick lens; see the Review Triage Log):**
  - **Town never turns hostile:** Town's people have no race relations, so F4 or a hostile race never touches them. This covers the whole Town population, the Mayor (`SpawnLeader(..., peaceful: true)`) and the people in Town's shop. The lore reason: Town is where everyone still mixes. Science-face residents keep their race's relations.
  - **Trail readiness:**
    - The new PlayMode check over seeds 1–50 shows the spawned Town spots equal the sweep's pure spots, with the physics accept and the module town check included. So the sweep's trail is the one in play, and the trail is `Ready` on every seed.
    - If a trail is ever not ready, the Mayor falls back to 4.8's gift (`Dialogue.MayorFoundLine`, `QuestStep.Gave`; `LeaderQuest.Lines(..., townTrailReady)`).
  - **`GiveFragment`:** the trail moves to `FragmentGiven` only once `RunRewards.Grant` succeeds.
  - **Spare quest-givers:** each voice's `SpareQuest` now says "no errand from me, but {name} here in town has one", naming the face's real errand-giver, so it is true before and after the errand is given. `SpareQuestPlain` covers the case with no errand-giver (debug spawns).
    - Town dresses its giver only when both the giver and the target have a spot. Otherwise there is no errand, and the spare lines are plain.
    - Science towns always spawn all six residents.
    - The seeds 1–50 PlayMode check confirms every town has exactly one errand-giver among the spawned people.
  - **Q** does nothing while a conversation is open (`GamePause.Talking`).
  - **Tests:**
    - `FaceRepairSceneTests` checks the Mayor's real lines ("gone missing" and the trail's first person) and walks the trail through `TownTrailQuest.Talk` to get the fragment.
    - The dedup check now reads who says what from the spawned NPCs (towns, town buildings and face populations) on seeds 1–50 (`QuestSceneTests.OverSeeds1To50_...`). The EditMode test keeps only the plan's checks, and the vacuous "no repeated quest line" assert is gone.
    - New: the trail walk first presses F4 beside a trail person and then turns every race hostile; Town stays peaceful and the trail completes. `FaceRepairTests.WithNoReadyTrail_TheMayorFallsBack_...`. Q while talking is covered in the freeze test.
  - Determinism is unchanged by the patches.

## Verification

**Result after the review patches (2026-10-09, `vpart.sh` steps in the foreground as `v418c`):**
- EditMode 378/378.
- PlayMode batches 39/29/54/28/38/37/4, all passing. Batch 2 has `QuestSceneTests` (8).
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s (unchanged).
- Sweep 50/50, 0 failing. Quests: Town's trail completable on 50/50 seeds (23 trails of 2, 27 of 3), and one errand in every town.
- `FIXTURE CHANGED` is the intended 4.18 re-record (town, streams, new townTrail); the review patches changed no signature, and `DeterminismTests` passes.
- Regenerated: the Cube scene (`QuestLogView`).
- Not committed.

## Review Triage Log

Pass 1 (quick lens), triaged by the coordinator: high 0, medium 2, low 5, all patched.

| Verdict | Route | Finding | Outcome |
|---|---|---|---|
| medium | patch | Town residents could turn hostile (F4 or relations), blocking the trail, errands or the shop | Patched: Town's people, the Mayor and Town's shop people have no race relations. The trail walk presses F4 and turns every race hostile, and still completes. |
| medium | patch | The sweep's pure Town spots might differ from the runtime spots (physics accept), so a "Ready" trail is not guaranteed; no fallback | Patched: a PlayMode check over seeds 1–50 shows the runtime spots equal the pure ones and the trail is Ready; if not Ready, the Mayor falls back to handing the fragment over (EditMode test). |
| low | patch | `GiveFragment` set `FragmentGiven`/`Stage` before `Grant` | Patched: only after `Grant` succeeds. |
| low | patch | SpareQuest lines ("someone else gave you this face's errand") were false before the errand was given; a giver could be dressed without a receiver | Patched: the line names the real errand-giver ("…but {name} here in town has one"), with a plain variant; Town dresses its giver only when both have spots. Tested over seeds 1–50 from the spawned people. |
| low | patch | Q opened the quest log during a conversation | Patched: Q does nothing while talking; tested. |
| low | patch | `FaceRepairSceneTests` compared the Mayor's lines with his own `Preview()` (tautology) | Patched: checks "gone missing" and the trail's first person, and walks the trail via `TownTrailQuest.Talk` to get the fragment. |
| low | patch | The dedup test rebuilt the conversations from the model and had a vacuous assert | Patched: who says what is read from the spawned NPCs on seeds 1–50 (PlayMode); the vacuous assert is dropped. |
