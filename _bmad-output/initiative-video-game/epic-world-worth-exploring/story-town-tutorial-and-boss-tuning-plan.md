---
title: 'Town tutorial and boss tuning'
type: 'feature'
ticket: '20'
created: '2026-10-08'
status: 'built'
baseline_revision: '951bec4'
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

**Problem:** The user, after a full playthrough on 2026-10-09:
1. "final boss difficulty. He sorts really fast making it quite difficult, maybe one of the gems could make him sort a lot slower."
2. "Add a small tutorial. I think we could use the squares on the town face as a little tutorial where you have to go to each in a specific order, then at the very end you get the fragment. But you can choose to skip the tutorial at the beginning when you talk to the elder and you answer his question."

**Approach:**
1. **Boss.** Make the Weightlands\* (Physics) gem's "slower restore" a much stronger benefit. Target at least 3× the time between fetches and at most 0.5× the steering speed, tuned so the change is clearly felt. The fight with no gems stays the same, so the skill check still takes 60–120 s with the same times. Update the lair hints and leader lines that describe this gem.
2. **Dialogue choice.** The dialogue box can ask a yes/no question, chosen with left/right (or up/down) and confirmed with Interact. The talk pause holds while the question is open. The voice and text rules still apply.
3. **Town tutorial.**
   - **The question.** On the first talk, Mayor Muddleton ("the elder") asks if the player wants to be shown around.
   - **Yes.** The tutorial visits Town's screens in a set order, about 5–8 stops, each marked by a glowing marker or sign. Each stop teaches one thing with a short line and a tiny task:
     - moving between screens
     - talking (E)
     - swinging and digging a mound
     - opening a chest
     - the map (M)
     - the quest log (Q)
     - switching items (1/2), if the player holds any
     - how locked face arches work (key vs item)

     At the last stop the player gets Town's fragment, then hands it to the Mayor as usual, which unseals Town's temple. The quest log tracks the tutorial step by step.
   - **No.** The Mayor gives the fragment at once.
   - **Replaces the lost-and-found trail.** The tutorial replaces 4.18's lost-and-found trail as the source of Town's fragment. Remove the trail, or keep it only as a fallback.
   - **Never a soft-lock.** The tutorial can't soft-lock: the player can always leave Town, and the Mayor can always be asked to skip.

</frozen-after-approval>

## Boundaries & Constraints

- The seed sweep stays at 50/50. Re-record determinism, with a note.
- The subtle-science and voice rules still hold (InWorldText, voices-and-names.md).
- The readability tests stay green.
- PlayMode tests run in class batches.
- Nothing from later Epic 4 stories, except the hooks they need.

## Tasks & Acceptance

- [x] Stronger Weightlands\* gem, measured in the arena. Hints and lines updated. The no-gem skill check is unchanged.
- [x] Yes/no dialogue choice, with tests.
- [x] Town tutorial: stops, markers, lessons, the fragment at the end, the skip path, and the quest log. The sweep checks it on seeds 1–50. A real-input PlayMode test walks it, and another takes the skip path.
- [x] Remove or retire the lost-and-found trail (keep determinism notes).
- [x] Update `player-guide.md`. Re-record determinism with a note. Full verify via `tools/verify.sh`.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
- **Stronger gem** (`ArenaTuning`):
  - SlowerRestore's pause between fetches goes from x2 to **x5** (`SlowRestoreScale`).
  - Its steering goes from x0.75 to **x0.3** (`SlowNudgeFraction`: 6.75 to 2.03 world units a second; x0.35 at first, slowed in the review patch, below).
  - `MaxwellDemon.FetchTimeoutNow` stretches the 4 s give-up by the same factor, so a slow fetch still finishes. At full speed it is exactly `FetchTimeout`, so the fight with no gems is unchanged.
  - **Measured** in `CoreSceneTests.SlowerRestore_SpacesOutTheDemonsFetches` (it now asserts at least 3x): the time between fetches went from 1.39 s to 4.45 s (3.2x, over three intervals) as first built, and after the review patch goes from **1.43 s to 4.94 s (3.45x)**, measured over seven intervals. The skill check is unchanged: 78.6, 90.5 and 105.5 s.
  - *Assumption:* the fatigue pause late in the fight is scaled x5 too, so the gem helps more as the fight drags on. **Note (review):** late in the fight, with this gem, the Demon is near-idle. At 90 s its pause is about 2.9 s, x5 is about 14.5 s, plus a slow fetch of about 4 s: one fetch every 18 s or so.
  - Words: Grand Muser Yondrix's unseal line now says "far, far more slowly", and the lair hint "far more slowly, resting long between each one". The HUD label stays "slower tidying", so the HUD layout is unchanged.
- **The yes/no choice** (`DialogueBox`, `DialogueChoice`):
  - The question is the conversation's last line.
  - A fresh push on Player/Move picks the answer: left or up the first, right or down the second (arrows, WASD or the stick). Interact confirms. Esc closes the question unanswered, and it is asked again next time.
  - The answer's follow-up lines play in the same box, so the talk pause holds throughout.
  - No key leaks into movement: time stands still while choosing, and when a conversation that asked a question closes, `PlayerMover.StopUntilFreshPress` is called.
  - `NpcTalker.SetChoice` lets a `Talking` listener attach a question; `LeaderQuest.LastChoice` uses it.
  - The prompt "[Arrows] choose   [Interact] confirm" is in the text scans.
- **The Mayor's tour** (`Quests/TownTour.cs`, with the pure plan and `TownTourText`; `Quests/TownTourQuest.cs` in play). `TownPopulation` binds it after every population, as it did the trail. The progress is `QuestLog.Tour`.
  - **The question.** The Mayor's first talk ends in "Shall I show you round first?".
    - Yes starts the tour.
    - No hands Town's fragment over at once (`Declined`).
    - Mid-tour he says where the next stop is and asks whether to skip the rest. Yes hands the fragment over at once.

    The usual hand-in then unseals Town's temple. `QuestStep.Offered` and `Touring` are new; `Lost` stays only as a retired number.
  - **The stops.** One per screen round the square, in a ring, each teaching one lesson in a fixed order. Each stop has a glowing marker (`TownArt.TourGlow`, plus a small flag sign, `TourSign`), and only the current stop's marker shows. Walking onto it says its line, under the label "Mayor Muddleton's tour". Once its task is done, the next marker lights, with the popup "Well done! Next stop: …".
    1. Moving between screens: getting there is the task.
    2. Talking: talk to **Gus Guidewell** the tour guide. He is a new fixed name, a Townsfolk who stands still and has no race relations.
    3. Digging: dig up the tour's heap of dirt, a `RewardMound` south of the marker. It hides nothing.
    4. Chests: open the tour's chest. It holds 3 Jumbles\*, paid through `RunWallet` under the key "chest:tour", and it is not in `CubeWorld.RewardChests`.
    5. The map: press M.
    6. The quest log: press Q.
    7. Switching items: press 1 or 2. *Assumption:* this stop comes up only for a player holding two or more items, since switching needs two; otherwise it is skipped.
    8. Locked arches: on a corner screen, with the marker in the lane leading to the outer arch. This stop gives Town's fragment.
  - **The things the tour builds.** The heap, the chest and the guide are built when the tour starts, once per run. A reveal re-populating Town keeps them; a rebuild clears them.
    - Each task is a state check (the heap open, the chest open, the guide talked to), so a task done early completes its stop on arrival.
    - Anything the tour could not build counts as done, so it can never soft-lock.
  - **The order** is seeded on **stream 30, reused**: `RngStreams.TownTrail` is renamed `TownTour`, still 30, and documented as kept for Town's quest.
    - Two draws: the start (one of the four screens beside the square) and the direction (clockwise or not).
    - Each stop is one doorway on from the last, or two when the item stop is skipped.
    - Over seeds 1–50, the first stop is the middle east screen on 8 seeds, middle north on 11, middle south on 11 and middle west on 20. The tour goes clockwise on 21 seeds.
  - *Assumption:* the tour runs only on a 3x3 Town (the play size), whose ring is the whole rim. On any other size `TownTour.For` gives null, and the Mayor falls back to 4.8's gift (`MayorFoundLine`, `QuestStep.Gave`).
  - **Never a soft-lock.** The tour cannot block the way, and the Mayor can always skip it:
    - the markers have no colliders;
    - the heap and the chest are triggers;
    - the guide stands on a `TownPlan`-valid spot, off the lanes;
    - the arches are untouched.
  - **The quest log** has one entry (`QuestLog.TownId`, now "town:fragment").
    - On a yes it is "Mayor Muddleton's tour", stepping on stop by stop ("Tour stop 3: walk to the glowing marker …", then the stop's task).
    - On a no it is "Town's fragment".

    Either way it ends with the hand-in.
- **The trail is retired.** Removed, since nothing else used them:
  - `TownTrail.cs` and `TownTrailQuest.cs` (with their metas);
  - `TownTrailProgress`;
  - the trail's `QuestLines` tables;
  - its `QuestText` steps.
- **Sweep and robustness.** `SeedSweep.FindQuestProblems` runs `TownTour.FindProblems`. It checks:
  - the eight lessons in order, each on its own Town screen off the square;
  - the first stop beside the square, each next stop one doorway on, and every stop's screen reachable;
  - every marker and thing in a clear lane, off the module's solids;
  - the Locks stop on the face's rim, on its arch's side;
  - the guide's spot found (the pure `GuideSpot`, clear of the planned Town spots);
  - every line, with its real places, in the text rule and the box.

  `RobustnessTests` (seeds 1–500) already requires the quest checks to run and pass, so it includes the tour. The builder prints a new "Quests:" line.
- **Tests:**
  - EditMode `QuestTests`:
    - the tour over seeds 1–50: every ring start and both directions are seen, and a broken tour is flagged;
    - the tour's words and the quest log's steps.
  - EditMode `FaceRepairTests`:
    - the Mayor's rule now offers the tour;
    - the fallback with no tour;
    - the gem's magnitudes: the pause at least x3, the steering at most x0.5.
  - New PlayMode **`TownTourSceneTests`** (4 cases, in a new **batch 8** in `tools/verify.sh`):
    - **the question, with real arrows and WASD:** the talk pause holds, the player does not move, a held key does not leak, Esc leaves it unanswered, and no gives the fragment;
    - **the tour, walked stop by stop with real keys, then the hand-in:** on seed 7 (two items held: all 8 stops) and seed 21 (7 stops);
    - **the skip:** no keeps the tour going, yes gives the fragment, leaving Town mid-tour and coming back carries on, nothing the tour puts down blocks the way, and a new run resets it.
  - Updated:
    - `QuestSceneTests`: the trail walk is removed, and the seeds 1–50 check now proves the guide's spawned spot is the pure one;
    - `FaceRepairSceneTests`: the Mayor's no gives the fragment;
    - `TownSceneTests`, `CoreSceneTests`, `RngStreamsTests` and `DeterminismTests`.
- **Determinism:** re-recorded twice, and the two recordings are byte-identical.
  - `townTrail` becomes `townTour`, in 50 lines ("start 6 cw stops Move(0,1) Talk(0,2) …").
  - In the `streams` lines only the key's name changed (`townTrail=` to `townTour=`); stream 30's draws hash the same.
  - Nothing else moved.
- **Docs:**
  - `player-guide.md`: questions, the tour's table, the skip and the gem;
  - `voices-and-names.md`: the tour's lines, the guide and the gem's wording;
  - `tools/README.md`: batches 0–8.
- **Screenshots** (seeds 7 and 21) are in the scratchpad's `snaps420/`. `s*-stop{1,2,3,4,8}-*` show the glow and flag, the guide, the heap, the chest and the arch stop. They came from a temporary test, since deleted. IMGUI (the box and its answers) is not drawn in batch mode, so the tests cover it.

- **Review patches (pass 1, quick lens; see the Review Triage Log):**
  - **`TownTourQuest`:**
    - A no, a skip and the last stop each record their outcome (`Declined`, `Skipped`, the final `Stage`) only once `GiveFragment` really succeeds. `GiveFragment` now returns whether it did. If it fails at the last stop, the stop stays done and is retried the next frame.
    - `Update` returns early once `Stage >= Stops.Count`.
  - **Stick drift:** `FreshPressLatch.ReleasedBelow` goes from 0.1 to **0.3**, the usual stick deadzone.
    - So a resting stick's drift counts as let go, and drift is never a press. Keys give 0 or 1, and a diagonal 0.71, so keyboard input is unaffected.
    - The EditMode latch test gains a drift case. The face-crossing latch tests pass, in batch 3 among others.
  - **Gem measurement:** the arena test keeps five warm strays across throughout (each one fetched home is sent back over) and averages **seven intervals**, not three.
    - Over that window x0.35 steering measured only 2.92x, so the steering is slowed to **x0.3**, keeping the x5 pause. It now measures 1.43 s to 4.94 s (3.45x).
    - The late-fight note is added above.
  - **Walking out of Town:** the skip test, renamed `MidTour_NothingBlocksWalkingOutOfTown_TownStaysPeaceful_AndTheMayorCanSkipTheRest`, now walks with real keys:
    - down the Dig and Chest stops' lanes, over the heap and the chest and out through the doorway beyond;
    - then from the Locks stop's screen out through its (opened) arch onto another face, which reveals the faces;
    - then back in Town the tour carries on, with the same guide.
  - **Town hostility:** that test also presses F4 beside a Town resident and turns every race hostile. Every Town NPC, the Mayor and the guide stay non-hostile with `Relations == null`.
  - **`FindProblems` negatives:**
    - New EditMode `QuestTests.TownTourProblems_CatchWrongOrder_FarStops_MarkersOffALaneOrOnASolid_AndALocksStopOffTheRim`, using `TownTour.Custom`. It covers swapped lessons, a stop two doorways off, a marker off the lanes, a marker on a solid, a Locks stop whose arch faces inwards, too few stops and a stop on the square.
    - The guide-spot validity check, which could never fail (`GuideSpot` only picks valid candidates), is removed. "No room for the guide" stays tested.
  - **Things after the tour:** once the tour ends (its last stop, or a skip), `RetireProps` runs, and `Bind` runs it too after a re-population.
    - The guide gets ordinary town lines (`Dialogue.For`).
    - An undug heap and an unopened chest are removed; ones already dug or opened stay.
    - The chest pays only while the tour runs.
    - Both PlayMode tour tests check this.
  - Determinism is unchanged by the patches.

## Verification

**Result (2026-10-09, `tools/verify.sh v420b`, one part at a time in the foreground):**
- `edit`: EditMode 406/406.
- `robust`: 1/1, the 500-seed test.
- PlayMode batches 0–8: 39/29/51/28/38/37/4/14/4, all passing. Batch 2 lost its 3 trail-walk cases; batch 8 is new.
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s (unchanged).
- `sweep` (with `ALLOW_FIXTURE_CHANGE=1`): 50/50, 0 failing, and the tour is walkable on 50/50 seeds. It was run again as `v420c` after a fix to the summary's wording, with the same result.
- `FIXTURE CHANGED (allowed)`: this is the intended re-record described above. `DeterminismTests` passes.
- Not committed.

**Result after the review patches (2026-10-09, `tools/verify.sh v420d`, one part at a time in the foreground):**
- `edit`: EditMode 407/407 (1 new: the `FindProblems` negatives).
- `robust`: 1/1.
- PlayMode batches 0–8: 39/29/51/28/38/37/4/14/4, all passing. Batch 3 holds the face-crossing latch tests and batch 8 the reworked skip test.
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s (unchanged).
- Gem: 1.43 s to 4.94 s between fetches (3.45x, over seven intervals).
- `sweep`: 50/50, 0 failing. Without the flag it reports `FIXTURE CHANGED` against `HEAD`. The fixture is byte-identical to the 4.20 re-record (the patches change no signature), so it was run again with `ALLOW_FIXTURE_CHANGE=1`: `FIXTURE CHANGED (allowed)`. `DeterminismTests` passes.
- Not committed.

## Review Triage Log

Pass 1 (quick lens), triaged by the coordinator: high 0, medium 0, low 7, all patched.

| Verdict | Route | Finding | Outcome |
|---|---|---|---|
| low | patch | `TownTourQuest` set `Skipped`, `Declined` and the final `Stage` before `GiveFragment` succeeded, and `Update` could index past the last stop | Patched: `GiveFragment` returns whether it succeeded, and each outcome is recorded only after it does (a failed last stop retries the next frame). `Update` returns once `Stage >= Stops.Count`. |
| low | patch | The fresh-press release (below 0.1) was beaten by gamepad stick drift | Patched: `FreshPressLatch.ReleasedBelow` is 0.3, the stick deadzone, so drift counts as let go and is never a press. The latch EditMode test gains a drift case, and the face-crossing tests pass. |
| low | patch | The ≥3x gem assertion stood on three intervals | Patched: five strays stay across throughout and seven intervals are averaged. That showed x0.35 steering at 2.92x, so the steering is x0.3 (pause still x5): 3.45x. The near-idle late-fight Demon is noted. |
| low | patch | "NothingBlocksLeavingTown" never walked out | Patched: with real keys the test walks over the heap and the chest and out through the doorways beyond, then out of Town through the Locks stop's arch, and the tour carries on. Renamed to what it checks. |
| low | patch | The trail walk's Town hostility coverage was lost | Patched: after F4 and every race hostile, every Town NPC, the Mayor and the guide stay non-hostile with `Relations == null`. |
| low | patch | `FindProblems` had no negatives for most checks, and a guide-spot check that could never fail | Patched: a new EditMode test covers wrong order, far stops, a marker off a lane or on a solid, a Locks stop off the rim, too few stops and a stop on the square. The dead check is removed. |
| low | patch | After a skip or finish the guide still gave the tour and the chest still paid | Patched: `RetireProps` gives the guide ordinary lines and removes an undug heap or unopened chest. The chest pays only while the tour runs. Checked in both PlayMode tour tests. |

