---
title: 'Fragments and face repair'
type: 'feature'
ticket: '8'
created: '2026-10-08'
status: 'built'
baseline_revision: '36066c2'
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

**Problem:** The user said: "Each face should have 1-6 fragements to collect (the town face awards you with the 1 fragement and the other faces can randomly have 2-6 of their fragments available to collect, each with a unique number like a dice). Once you collect all the fragements the village leader for that face will help you repare it and it gives you some sort of benefit for the final boss fight, one reduces the number of things to unsort, one increase the gap that you have to push them through, one makes them slightly attracted to you so it is easer to push, one makes the boss slower at resorting." The user picked the two other benefits: +1 heart, and slower, rarer shots.

**Approach:**
1. **Die numbers.** Each run rolls a die number per face: Town is 1, and the other five faces get 2–6, shuffled per run. Use a new RNG stream.
2. **Fragments.** The face numbered k has k fragments, numbered 1..k and shown like die pips. They sit in the fragment slots of the roadblock rewards from 4.7. Town's single fragment is awarded by the Mayor's quest (talk, then receive it), so the Town face is repaired once you hand it back.
3. **The leader's quest.** Each leader's quest line (4.5) names the count, in voice. Returning with all k fragments and talking to the leader repairs the face. The repair shows as a visible change in the town (a lit statue or a banner) and on the map.
4. **Boss benefits.** Each repaired face grants one benefit. The default mapping is fixed per face theme, and the plan can choose it:
   - fewer particles to un-sort;
   - a wider membrane gap;
   - particles slightly drawn towards the player;
   - the Demon restores order more slowly;
   - +1 heart for the fight;
   - the Demon's shots are slower and rarer.
5. **HUD and map.** They show the fragments collected per face and which faces are repaired.

Note from the 4.5 review: read `TownPopulation.Leader` and `TownSquares.LeaderOf(face)` fresh each time. The reveal re-populates the town, so cached references go stale.

</frozen-after-approval>

## Boundaries & Constraints

- The seed sweep stays at 50/50. Re-record determinism, with a note.
- The subtle-science and voice rules still hold (InWorldText, voices-and-names.md).
- The readability tests stay green.
- PlayMode tests run in class batches.
- Nothing from later Epic 4 stories, except the hooks they need.

## Tasks & Acceptance

- [x] Roll die numbers; place fragments in reward slots; Town's single fragment comes from the Mayor.
- [x] Leader quest (count, hand-in, repair state, a visible repair in the town and on the map).
- [x] Boss benefits applied in CoreArena/MaxwellDemon from the repaired faces. The no-benefit skill check stays at 60–120 s.
- [x] HUD and map fragment display.
- [x] Tests:
  - on seeds 1–50, every fragment is reachable (sweep), Town is always 1, and the other die numbers vary;
  - a hand-in repairs the face;
  - each benefit measurably changes its arena parameter;
  - the suites and the sweep pass.

## Routed from the 4.7 review

- **(medium) Fragment slots pay nothing.** Each face has 6 numbered fragment slots, but 4.7 only records them. Fill slots 1..k on the face numbered k. Slots above k must give something useful (Jumbles\* or nothing visible): no empty-looking shards.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
- **Die numbers** (`Rewards/DieNumbers`, pure, new stream `RngStreams.DieNumbers` = 24). Town (the start face) is always 1. The other five faces get 2–6, shuffled per run. They are numbered by face, so by region too, since regions are placed per run. `CubeWorld.Dice` rolls them on every rebuild, so the HUD knows Town's number before the reveal.
- **Fragments** (routed from the 4.7 review). `RewardPlacement.Generate` takes the dice (it defaults to the run's own). Each face keeps 4.7's 6 reserved slots and every draw:
  - slots 1..k on the face numbered k hold fragments 1..k (`Reward.Fragment(ordinal)`, with no Jumbles\* fallback any more);
  - the slots above k hold plain 5-Jumbles\* chests: no shard and no die on them.

  So only what those slots hold changes. A test swaps in other dice and checks that every roadblock, and every other reward, stays the same. Each run has 20 fragments on the science faces, plus Town's 1.
  - A fragment chest keeps its shard icon. It gains a die badge (`TownArt.DieFace`, the number as pips) and the popup "Fragment 3 of 5 from the Greenwood\*".
  - `RunRewards` holds each fragment once (`FragmentsOf`, `HasFragment`).
- **The quest** (`Town/LeaderQuest`). `TownSquares.SpawnLeader` (now given the face) puts it on every leader. Each talk raises `NpcTalker.Talking` (new, with `SetLines`) and reads the run fresh: `world.Dice`, `world.Repairs` and the player's `RunRewards`. No state lives on the leader, so a re-spawned Mayor (the reveal re-populates Town) carries on, as the 4.5 review asked.
  - Science face numbered k: the leader's greeting, then the quest naming k and how many are held (in words, in voice: `Dialogue.QuestTemplates`). With all k held, the leader takes them: the repair line (`RepairTemplates`, which says in voice what the face does in the core), and `CubeWorld.RepairFace`. After that, a thank-you (`MendedLines`).
  - *Assumption (Town):* the first talk with the Mayor gives Town's one fragment ("look, I found it! Hold on to it, then hand it back"), and the next talk hands it back and mends Town. That is the plan's "talk, receive it, hand it back".
  - The hand-in happens as the conversation opens.
- **Repair state**: `CubeWorld.Repairs` (`FaceRepairs`), a fresh one on every rebuild. `RepairFace` records the face and lights its town (`TownSquareView.Repaired`):
  - the statue tinted gold (`LitStatueTint`);
  - a soft gold halo behind it;
  - a pennant in the region's colour at each shoulder.

  This is art only, with no colliders, so a repair never moves anything and the skill check's physics world is unchanged.
- **Boss benefits** (`Rewards/BossBenefits`, `ArenaTuning`). *Assumption (the plan's call):* the mapping is fixed per theme:

  | Face | Benefit | Why |
  |---|---|---|
  | Town | +1 heart | the townsfolk have your back |
  | Biology | Attraction | drawn like finches to seed |
  | Chemistry | Fewer particles | they fizz some away |
  | Physics | Slower restore | time drags for the Demon |
  | Math | Wider gap | bridges, gaps |
  | Earth | Slower, rarer shots | a headwind |

  Magnitudes:
  - **Fewer particles:** 8 to 6 a side (16 to 12).
  - **Wider gap:** 1.5 to 2.25 old units (x1.5; 2.25 to 3.375 world).
  - **Attraction:** a pull of 0.3 old units a second towards the player, within 9, added to the drift target (under the speed at which a particle coasts).
  - **Slower restore:** the pause between fetches x2, and the fetch steering speed x0.75 (6.75 to 5.06 world).
  - **+1 heart:** for the fight. It is added filled at the start and taken back when the fight is won, lost, abandoned (exit door, F8, a teleport) or rebuilt, with no net heal (review patch). The arena records the player's health when it gives the heart; on takeback, current health is capped at that value (`Health.CapCurrent`).
    - *Assumption:* the coordinator asked for the recorded health to be restored exactly. Capping gives exactly that whenever no damage was taken. It also never heals damage taken in an abandoned fight, beyond what the extra heart absorbed.
  - **Slower shots:** every 4 to 6 s, at 6.5 to 4.5 old units a second (9.75 to 6.75 world). The measured gap between pulses went from 4.84 to 6.84 s.
- **Benefits in the fight.** `CoreArena` reads `RunBenefits` at each fight's start (`BenefitsOverride` is for tests) and applies `ArenaTuning.For`:
  - the particle count;
  - `LayMembrane`, which resizes the two segments, and only when the height changes;
  - `Particle.Attraction`;
  - `MaxwellDemon.ApplyTuning` (`RestoreScale`, `NudgeSpeedNow`, `PulseIntervalNow`, `PulseSpeedNow`);
  - the hearts.

  With no benefits every value and float operation is the 1.11 arena's (x1f, the same constants), so the skill check is unchanged: 78.6, 90.5 and 105.5 s on seeds 1234, 7 and 42.
  - `CoreHud` shows a gold line over the meter: "Help from mended faces: +1 heart, wider gaps".
- **HUD:** `CombatHud` gains a row of dice ("Fragments" and one die per face whose region is known: Town from the start, the rest once revealed), in die-number order. Each die has a pip per fragment, lit once found, a region-coloured frame, and gold once mended. `FragmentsText` ("Fragments: Town mended, the Greenwood\* 2/4") is its text form.
- **Map:** a mended face's town marker is gold with a pennant (legend "Mended town", now 10 labels, still 2 rows at 960 px). Each visited face shows a small translucent tag beside it: "2/4", or "mended" in gold (`CubeMap.FragmentTag`). After the review patch the tag sits in an empty slot of the net touching the face, never over a screen (`CubeMap.FragmentTagRect`):
  - the slot above if empty, then below, left, right, then the diagonals;
  - Town, in the middle, gets the empty slot to its lower right.
- **Debug:** Shift+F9 gives every fragment of the face the player stands on (`CubeDebug.GatherFragments`), so a talk with its leader mends it. F9 alone still rests.
- **Sweep:**
  - `RewardPlacement.FindProblems` now checks the dice (Town 1, the others 2–6 once each) and that the face numbered k holds exactly fragments 1..k, required and reachable.
  - The report gains a "Fragments" line: 1000 over 50 seeds, all proven reachable; Town numbered 1 on 50/50 seeds; every region saw every number from 2 to 6.
  - After the review patch, a fragment counts as "proven reachable" only on seeds where the reward check ran and found no problem.
- **Determinism:** re-recorded twice; the two recordings are byte-identical.
  - Changed: the `rewards` lines (the slots above k are now J5 instead of F) and the `streams` lines (the new `dice` stream is appended).
  - New: the `dice` lines.
  - Every other signature is unchanged.
- **Text:**
  - All new lines are in voice and in the scans. `Dialogue.LeaderQuestLines` covers every count 2–6 and every found count from 0 to count; it feeds `AllFixedText`, the voice-marker test and `FaceText`.
  - `BossBenefits.AllText` joins `CoreHud.AllText`.
  - The fragment tags join `CubeMap.AllTexts`.
  - No banned words: the benefit labels never say "particle"; they say "fewer to mix" and "drawn to you".
  - `voices-and-names.md` and `ART-MAP.md` are updated.
- **Tests:**
  - New EditMode `FaceRepairTests` (16 cases):
    - dice over seeds 1–50 (Town 1; the others 2–6, varying by face and by region; at least 35 orders);
    - every fragment placed and reachable, with the slots above k as Jumbles\*;
    - the dice change only the reserved slots;
    - the sweep flags a missing fragment;
    - a hand-in of all k repairs the face (and a short one does not), and the quest names the count;
    - the Mayor's give-then-mend;
    - quest text rules;
    - the theme-to-benefit mapping;
    - no benefits is the 1.11 arena value for value;
    - each benefit changes exactly its parameters, in the player's favour (one case per benefit);
    - the die badges' pips and the repair art's pixel style.
  - New PlayMode `FaceRepairSceneTests` (3 cases, batch 2 in both `vpart.sh` and `verify.sh`):
    - the Mayor gives the fragment, then mends Town, with the statue lit, the map marker, the HUD and the map tag, and +1 heart in the next fight, taken back on leaving; a new run forgets it all;
    - on seeds 21 and 42, the face numbered 5 has its 5 badged chests, its leader refuses a 4-of-5 hand-in and then mends the face with all five.
  - Review patches add to `CoreSceneTests` (and `CubeMapTests`):
    - `Attraction_RaisesTheParticlesVelocityTowardsThePlayer`: the warm particles' mean speed towards the player goes from 0.06 to 0.44, against a pull of 0.45. Both fights replay the same drift after a rebuild.
    - `SlowerRestore_SpacesOutTheDemonsFetches`: the time between the Demon's restorations goes from 1.39 s to 2.05 s.
    - `ExtraHeart_EnteringAndLeavingTwice_WhileHurt_GivesNoNetHeal`.
    - `ExtraHeart_IsTakenBack_WithNoNetHeal_HoweverTheFightEnds` (5 cases: a win, a loss, the exit door, F8 and a rebuild mid-fight).
    - EditMode `FragmentTags_SitBesideTheirFaces_NeverOverAScreen_EachOther_OrTheHeaderAndLegend`, at 4 screen sizes.
  - `CoreSceneTests` gains `EachBenefit_MeasurablyChangesItsArenaParameter_AndOnlyIt` (6 cases). It runs after the skill check, against a no-benefit baseline fight, and measures:
    - the particles spawned;
    - the gap height from the built membrane colliders;
    - each particle's pull;
    - the Demon's restore pace;
    - `Health.Max`;
    - a fired pulse's speed, and the measured time between two pulses.
  - Updated: `RewardPlacementTests` (fragments 1..k, `Fragment(ordinal)`, the new popups), `RngStreamsTests` (stream 24), `DeterminismTests` (the `dice` line), `TownSceneTests` (leaders say the quest with their face's count), `VoicesAndNamesTests` and `FaceText` (the new lines), the `CubeMapTests` message, and the popup check in `RewardSceneTests`.
- **Screenshots** (seed 7) are in the scratchpad's `snaps48/`: Town before and after mending, a fragment chest with its die badge, each region's mended town, and the arena with every benefit. They came from a temporary snapshot test, since deleted. IMGUI (the HUD and map) cannot be captured in batch mode, so the tests cover those.
- No builder asset, art catalog or scene regeneration was needed: all the art is drawn in code and there are no new scene components.

## Verification

**Result (2026-10-08, `vpart.sh` steps in the foreground as `v48a`):**
- EditMode 317/317.
- PlayMode batches 31/22/37/28/38/32/1, all passing.
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s (unchanged; no benefits).
- `IslandWalkTests`: passing.
- Sweep 50/50, 0 failing; 1000 fragments, all proven reachable; Town numbered 1 on 50/50 seeds.
- `FIXTURE CHANGED` is the intended re-record described above, and `DeterminismTests` passes.
- Not committed.

**Result after the review patches (2026-10-08, `vpart.sh` steps in the foreground as `v48b`):**
- EditMode 318/318 (1 more: the map tag layout).
- PlayMode batches 39/22/37/28/38/32/1, all passing. Batch 0 has the 8 new review tests.
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s (unchanged).
- `IslandWalkTests`: passing.
- Sweep 50/50, 0 failing; 1000 fragments, all proven reachable.
- Fixture unchanged since v48a (`FIXTURE CHANGED` is against the baseline), and `DeterminismTests` passes.
- Not committed.

## Review Triage Log

Pass 1 (quick lens), triaged by the coordinator: high 0, medium 1, low 3, all patched.

| Verdict | Route | Finding | Outcome |
|---|---|---|---|
| medium | patch | Entering and abandoning the fight with ExtraHeart gained +1 current health each cycle (the heart was added filled; taking back the max only capped health) | Patched: the arena records the player's health when it gives the heart, and on takeback (a win, a loss, an abandon by the exit door, F8 or a teleport, or a rebuild) caps current health at it (`Health.CapCurrent`), so no ending ever heals. PlayMode tests: a hurt enter-and-leave twice, and each of the five endings. |
| low | patch | The map's fragment tag covered the screens in each face's top-left corner | Patched: the tag sits beside its face in an empty net slot (`CubeMap.FragmentTagRect`, smaller and translucent). An EditMode layout test at 4 screen sizes checks it never covers a face, another tag, the header or the legend. |
| low | patch | The sweep report counted fragments as proven reachable even where the reward check had not run | Patched: they count only where `RewardsChecked` and no reward problems. |
| low | patch | The Attraction and SlowerRestore tests read tuning values, not behaviour | Patched: new PlayMode tests measure the particles' velocity towards the player (0.06 to 0.44) and the time between the Demon's fetches (1.39 s to 2.05 s), each against a no-benefit fight on the same seed. |
