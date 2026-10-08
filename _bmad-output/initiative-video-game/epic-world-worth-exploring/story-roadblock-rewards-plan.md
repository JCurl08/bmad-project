---
title: 'Roadblock rewards'
type: 'feature'
ticket: '7'
created: '2026-10-07'
status: 'built'
baseline_revision: '1737e8a'
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
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** the user said: "I would also like to hide items behind some of the roadblocks, right now they are just on the ground to pick up. Each roadblock should have something hiding behind it: coins, equipment, upgrade, keys, or fragements… Each screen should have something interesting, a puzzle to get to the next screen, enemies to battle, or an object to collect."

**Approach:**
1. Every gate (required and optional), puzzle screen and hidden spot guards a reward. The reward kinds:
   - coins (meta currency, persisted like trial pay);
   - equipment (the face items);
   - an upgrade: a small in-run boost such as +1 max heart for the run, or a speed or power bump;
   - a key (consumed by locked face-edge gates in 4.9; until then a key can be held, shown on the HUD, and gives coins if unused at run end);
   - a fragment slot, filled in 4.8.
2. Loose ground pickups go: face items sit behind roadblocks too.
3. Every non-town screen holds at least one of: a puzzle or roadblock, enemies, or an object.
4. ItemPlacement and the sweep gain the reward kinds and prove that required rewards are reachable.

</frozen-after-approval>

## Boundaries & Constraints

- The sweep stays 50/50 on seeds 1–50. Re-record determinism with a note.
- Subtle-science and voice text rules hold (InWorldText, voices-and-names.md).
- Readability tests stay green.
- PlayMode runs in class batches.
- Nothing from later Epic 4 stories, except the hooks they need.

## Tasks & Acceptance

- [x] Reward model and placement (kinds, a reward per roadblock, no loose pickups) on its own RNG stream.
- [x] Reward visuals: a chest or pedestal behind the roadblock, with a pickup popup in voice-neutral text.
- [x] Coins persist as meta currency; keys and upgrades shown on the HUD.
- [x] Every screen has something (sweep rule).
- [x] Tests: the sweep reports no reward outside a roadblock and no empty screen; every kind appears across seeds; coins persist after a run; suites pass.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
- **Model** (`Rewards/RewardPlacement`, pure, new stream `RngStreams.Rewards` = 23, with one sub-stream per face since the review: `FaceStream`). It runs after `ItemPlacement` (3) and `HiddenCurrencyPlacement` (16) and changes none of their draws, so items, hidden currency and every face plan are unchanged.
  - Kinds (`RewardKind`): Jumbles\*, Equipment, Upgrade, Key, FragmentSlot.
  - Roadblocks (`RoadblockKind`): Gate, OptionalGate, Mound and Puzzle. A Mound is a hidden spot: a hidden-item slot, or `Roadblock.LaneSpot`.
  - A `RewardSpot` is a roadblock, a reward, `Required` and `HiddenIndex`.
- **Placement**, in this order:
  1. Each item where `ItemPlacement` put it. A guarded item stays in its gated alcove. An item that used to lie in the open now lies under a mound on the same lane spot (`CubeWorld.OpenPickupOffset`), so the positions, the face plans' keep-outs and `DoorwayReach` are unchanged.
  2. A mound over each hidden currency item, which keeps its 5 Jumbles\*.
  3. A bonus chest behind each optional gate.
  4. Each face's pool: its other required-gate alcoves and its puzzle goals, plus a filler mound on every screen that has no roadblock yet. Island screens count as puzzles. While the pool is under 8, extra mounds go on free hidden-item slots.
  5. The pool is shuffled. Its first 6 spots are fragment slots 1–6, then 1 key and 1 boost, and Jumbles\* chests (5) fill the rest.
- **Assumptions:**
  - *Assumption (in-run upgrades):* +1 heart (added filled), +speed or +power. Each is one `PlayerStats` point for the rest of the run, drawn evenly. A rebuild takes them back (`RunRewards.ResetRun`). The player reset forgets them before it sets the stats afresh.
  - *Assumption (mix per face):* every science face has 1 item, 6 fragment slots, 1 key, 1 boost, its hidden Jumbles\* and Jumbles\* chests for the rest. Optional gates hold bonus chests of 10. An unused key is cashed in for 5 Jumbles\* at the run's end.
  - *Assumption (fragment slots for 4.8):* every face reserves 6 numbered slots, all required and reachable. 4.8 fills slots 1..k on the face numbered k. The unused slots give their `Amount` (5 Jumbles\*) as the fallback.
    - The hooks are `RewardPlacement.FragmentSlots(face)` and `RunRewards.Fragments`.
    - Until 4.8, a slot shows a face-tinted shard over its chest, and taking it pops up "A shard of <region>".
  - *Assumption (keys until 4.9):* keys are held and shown on the HUD. `RunRewards.TrySpendKey` is the hook for 4.9. The RunLoop cashes in the keys left at the run's end (`RunWallet.AddAtRunEnd`, `EarningSource.Keys`) before it banks.
  - *Assumption (optional gates):* their variant is never this run's, so a bonus behind one can't be reached this run. The bonus is only ever Jumbles\* and never required, and the sweep enforces this. 4.9 might let keys open optional gates; that is the user's call.
  - *Assumption (hidden spots):* a mound is a trigger, so it never blocks lanes, boulders or people. Any swing opens it in 2 hits (`RewardMound.HitsToOpen`), including a bare-handed one.
  - *Assumption (puzzle goals):* the trials keep paying their Jumbles\* through `TrialRoom` as before. 4.10–4.12 pass their goals as `PuzzleGoal`s into `Generate`. Those goals join the face's pool, and the stories build them with `CubeWorld.BuildRewardChest`.
- **Runtime:**
  - `CubeWorld.PlaceRewards` runs after the items and the hidden currency, before `Revealed`.
  - Items stand on a pedestal. A mound covers its reward: the reward's sprites are hidden and its `Guard` keeps `ItemPickup`, `HiddenCurrencyPickup` and `RewardChest` shut until the mound is dug away.
  - A chest over a key, boost or fragment slot shows that reward's icon. Touching a chest opens it: the lid goes up and it fades.
  - Keys, boosts and fragments go to `RunRewards`, a new component on the player, with a popup from `RewardText`.
  - A Jumbles\* chest pays the `RunWallet` (`EarningSource.Chest`; its popup is the existing "+5 Jumbles\*" one). It is banked at the run's end and so persists as meta currency.
  - `CombatHud` shows a third line, "Keys: 1 | Run boosts: +1 heart, +speed". The between-runs screen gains the chests and keys lines (7 lines, 22 px apart).
- **Art:** 7 new keys in a new `Reward` category (Mound is a Prop):
  - Chest D89 and ChestOpen D91;
  - Pedestal D54;
  - RewardKey T117, RewardBoost D116 and RewardFragment D101;
  - Mound D66.
  The catalog was rebuilt with 89 keys, and `ART-MAP.md` was updated.
- **Sweep:**
  - `RewardPlacement.FindProblems` runs in `SeedSweep.Run`; its problems go in `SeedResult.RewardProblems`.
  - It checks that:
    - every reward stands behind a real roadblock, one reward per roadblock;
    - every gate, optional gate, item pickup and hidden spot hides a reward;
    - each face has fragment slots 1–6, its key and its boost;
    - no non-town screen is empty;
    - every required reward is reachable.
  - Reachability collects items doorway by doorway (`SeedSweep.CollectableItems`, shared with the item check). A reward behind a gate also needs that gate's item.
  - The report gains "Rewards" and "Reward mix" lines.
- **Determinism:** recorded twice, and the two recordings are byte-identical. Only the new `rewards` lines and the `streams` lines changed (the new stream was added); every other signature is unchanged.
- **Tests:**
  - New EditMode `RewardPlacementTests` (9). They cover the sweep on seeds 1–50, one reward per roadblock with no loose pickups, the per-face mix, determinism with unchanged item and hidden draws, and the sweep flagging a loose item, a reward outside a roadblock, an empty screen and an unreachable reward. They also cover the puzzle-goal hook, `RunRewards`, the wallet's key cash-in and the text rule.
  - New PlayMode `RewardSceneTests` (4), added to batch 2 in both `vpart.sh` and `verify.sh`:
    - every planned reward is built behind its roadblock on seeds 7, 21 and 42;
    - a lane mound hides its item until two bare-handed swings dig it away;
    - chests give a key, a boost, a fragment and Jumbles\*; the HUD shows them; the run's end cashes in the key and banks everything; the balance survives a scene reload;
    - a new run, or a rebuild, takes the boosts back.
  - Updated, because open items now need a dig: the Biology, Chemistry and Physics pick-ups, the plain-item walk in `ItemSceneTests`, and the hidden walk-on in `RunLoopSceneTests`. The between-runs screen now has 7 lines. `RewardText.All` joins the text scan, and `RngStreamsTests` records stream 23.
- **Regenerated:** the art catalog and the Cube scene (`RunRewards` on the player, wired to the HUD). The module library and the item catalog are unchanged.
- **Screenshots** (seed 7) are in the scratchpad's `snaps47/`. They came from a temporary snapshot test, since deleted, and show chests behind optional gates, a mound before and after digging, and an opened chest.

## Verification

**Result (2026-10-08, `vpart.sh` steps in the foreground as `v47b`):**
- EditMode 298/298 (9 new).
- PlayMode batches 25/22/30/27/38/32/1, all passing. Batch 2 has the 4 new `RewardSceneTests`.
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s (unchanged).
- `IslandWalkTests`: passing.
- Sweep 50/50, 0 failing.
  - Rewards: 3297 over 50 seeds (61–69 a run). That is 1047 Jumbles\* (785 bonus, 242 hidden), 250 equipment (172 under a lane mound), 250 boosts (79 heart, 77 speed, 94 power), 250 keys and 1500 fragment slots.
  - Every kind appears on 50/50 seeds.
  - Roadblocks: 1499 gates, 785 optional gates and 1013 mounds.
- `FIXTURE CHANGED` is the intended re-record described above.
- Not committed.

**Result after the review patches (2026-10-08, `vpart.sh` steps in the foreground as `v47c`):**
- EditMode 301/301 (3 new).
- PlayMode batches 25/22/34/28/38/32/1, all passing. Batch 2 has 4 more `RewardSceneTests`; batch 3 has the timed-door boost test.
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s.
- `IslandWalkTests`: passing.
- Sweep 50/50, 0 failing.
  - The totals and per-face mix are the same as v47b.
  - The boosts are now 115 heart, 67 speed and 68 power, from the per-face sub-streams.
- Determinism re-recorded twice, and the two recordings are identical. Against v47b only the `rewards` lines changed; every other line, including `streams`, is the same.
- Not committed.

## Review Triage Log

Pass 1 (quick lens), triaged by the coordinator: high 0, medium 2 (routed), low 6 (patched).

| Verdict | Route | Finding | Outcome |
|---|---|---|---|
| medium | routed to 4.9 | (1) The bonus chests behind optional gates can never be opened in a run (their variant is never rolled) | 4.9 adds key-locked bonus alcoves with a separate key pool. Unchanged here: bonus-only Jumbles\*, never required, enforced by the sweep. |
| medium | routed to 4.8 | (2) Fragment slots pay nothing yet (a shard and a popup only) | 4.8 fills the slots. Unused slots fall back to their `Amount`. |
| low | patch | (3) The key cash-in at the run's end raised `Earned`, so pickup popups showed over the between-runs screen | `RunWallet.AddAtRunEnd` raises its own `EarnedAtRunEnd` (no popup). Covered by an EditMode test and the PlayMode victory test. |
| low | patch | (4) The wallet hooked chests once, on `Revealed`, so chests built later (4.10–4.12 puzzle goals) would never pay | `CubeWorld.RewardChestBuilt` fires for every chest, and the wallet hooks each chest once (`HookChest`). PlayMode test `AChestBuiltAfterTheReveal_StillPays`. |
| low | patch | (5) One RNG stream for all faces, so a puzzle goal added on one face reshuffles the others | Each face draws on its own sub-stream, `RewardPlacement.FaceStream(face)` = (23 << 8) + 1 + face. EditMode test. Determinism re-recorded (`rewards` lines only). |
| low | patch | (6) Timed doors and the run's +speed boost | The boost is a `PlayerStats` point, so the switch already scales the door's open time through `PlayerMover.EffectiveSpeed`, as it does for meta upgrades. PlayMode test `TimedDoor_ARunSpeedBoost_IsScaledLikeAMetaUpgrade...`: one meta level plus one boost, one short is impassable and the required count passes. |
| low | patch | (7) A boulder resting on a chest could block it, since chests opened on touch only | `RewardChest` is an `IAttackReceiver`, so a swing opens it too. It ignores the swing that digs its mound away, and opens behind a gate only once the gate is open (`BlockingGate`). Boulder starts were already kept off hidden slots, the lane spot and alcoves; an EditMode test now proves it over seeds 1–50. |
| low | patch | (8) Test gaps | A shared `RewardDig.Dig` helper swings at a mound (twice, bare-handed) and is used by the Biology, Chemistry, Physics, Item, RunLoop and Reward scene tests instead of `Open()`. New tests: the sweep's "can never be reached" branch (a puzzle goal in island water), a won run banking chests and cashed-in keys with no popups, and F10 discarding a run's chests, keys and boosts. |
