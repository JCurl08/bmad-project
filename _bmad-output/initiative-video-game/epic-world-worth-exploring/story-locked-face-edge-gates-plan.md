---
title: 'Locked face-edge gates'
type: 'feature'
ticket: '9'
created: '2026-10-08'
status: 'built'
baseline_revision: 'e6a33b8'
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

**Problem:** From playtest 2:
- "We should make the edges smaller, you enter each screen through a gate or door if they are on the edge of a face."
- "Gates should be locked at first, requiring certain equipment to access. Each edge can have a different gate allowing users to gain access to the world in different ways."

**Approach:**
- **Locks.** Face-edge doorways (the arches from 1.25) start locked. Each needs either a key (from 4.7's roadblock rewards; a key is used up) or a specific piece of equipment, such as a face's signature item.
- **Different locks on different edges,** so the player has several possible first routes out of Town. The world opens in different orders on different seeds.
- **Readability.** A locked arch shows clearly what it needs: a keyhole, or the item's icon.
- **The sweep** proves that every screen, fragment, core portal and required item is reachable, and that at least two different edges can be the first one opened.

</frozen-after-approval>

## Boundaries & Constraints

- The seed sweep stays at 50/50. Re-record determinism, with a note.
- The subtle-science and voice rules still hold (InWorldText, voices-and-names.md).
- The readability tests stay green.
- PlayMode tests run in class batches.
- Nothing from later Epic 4 stories, except the hooks they need.

## Tasks & Acceptance

- [x] Lock model per face edge: a seeded lock kind (key or a specific item), on its own stream.
- [x] Locked arch visuals and interaction: swing or interact to open, keys used up, with a HUD key count.
- [x] Item placement, the sweep and DoorwayReach handle locks.
- [x] Tests:
  - on seeds 1–50, everything is reachable;
  - there are at least 2 possible first edges;
  - a key opens a key lock and is used up;
  - an item lock opens with that item only;
  - the suites pass.

## Routed from the 4.7 review

- **(medium) Unopenable bonus chests.** About 16 bonus chests a run sit behind optional gates that need variants this run never rolls, so they can't be opened. They become key-locked bonus alcoves: spending a key opens one, worth 10 Jumbles\* instead of the 5 the key cashes in for. Progression locks on face edges must not be starvable. Either they use a separate face-key pool, or every required lock has its own key, reachable before it and not spendable elsewhere. The sweep proves that required keys are at least required locks, however the player spends.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
- **Lock model** (`Locks/EdgeLocks`, pure, new stream `RngStreams.EdgeLocks` = 25). A lock belongs to a cube edge (`CubeEdge`, the 12 face pairs), and every arch along it shares it: the 3 doorways at 3x3, on both faces. Kinds are `Key` (used up) and `Item` (a face item's home theme; any variant opens it and nothing is used up). `Open` exists only for the fallback.
  - The draw: Town's 4 edges get 2 or 3 key locks and item locks on the rest. Each other edge is a key lock one time in three, else an item lock with a random run item.
  - Each draw is checked (`EdgeLocks.Check`) and redrawn on the same stream until it passes, up to 64 draws. *Assumption:* the fallback (Town's edges keyed, the rest open) is a safety net only. The sweep reports it, and it never happens on seeds 1–50 (at most 8 draws, 76 in all).
  - `EdgeLocks.ForRun(model, library, themes, variants)` is the one source for the game and the fixture, and the sweep builds the same `LockWorld`. `CubeWorld.Rebuild` makes the plan before Town's walls are built, so Town's arches are locked from the start.
- **The soft-lock proof** (`LockWorld.Explore`). A state is the set of key locks opened, in any order. For each state, the player collects everything it can reach doorway by doorway (`DoorwayReach` with a new `doorOpen` filter: locked doorways lead nowhere). Item locks open for free once their item is held and their arch can be walked to. Keys are `StartKeys` plus the keys found, minus the ones spent.
  - Moves spend a key on any key lock whose arch can be walked to, including a wasted one between two places already reached.
  - Every reachable state must still be able to finish: every screen and island of the unlocked cube, every item, and every required reward (fragments, keys, items' alcoves). Core portals stand on screens, so they are covered.
  - At least `MinFirstEdges` (2) edges must be openable first. Problems name the spent edges and the first thing that can no longer be reached.
- **Keys** (*assumption, the design call*):
  - The player starts every run with 1 key (`EdgeLocks.StartKeys`, held in `EdgeLockState`, never cashed in), which is the only way out of Town.
  - Each science face now hides 2 arch keys (`KeysPerFace` 1 → 2). They go to its first unguarded pool spots, never behind an item gate. A face whose pool is short of room (Math with its island screens; 2 faces over 50 seeds) has 1: the first key, then the boost, then the second key.
  - The HUD line always shows `Keys: N` while the run has locks (found keys plus the starting key), plus `Chest keys: N`.
- **Bonus alcoves** (routed from 4.7; *assumption, the design call: a separate pool*):
  - Chest keys are a new reward kind (`RewardKind.ChestKey`, signature `C`). They fill the fragment slots above each face's die number, which were plain Jumbles\* chests in 4.8. That is 6 − k a face and always 10 a run (`ChestKeysPerRun`). No pool draw changes, and the dice still decide only what the reserved slots hold.
  - Every optional gate gets an `AlcoveLock`: a pale-blue keyhole plate and a trigger. A swing or Interact with a chest key spends it and opens the gate for good (`Gate.ForceOpen`). The bonus chest behind it pays 10 Jumbles\*, against 5 for cashing the key in.
  - Chest keys never open an arch, and arch keys never open an alcove, so bonus spending can't starve progression. Both kinds left at the run's end are cashed in for 5 (`RunRewards.CashInKeys`).
  - About 16 alcoves a run against 10 chest keys, so the player chooses which to open.
- **Arches** (`Locks/LockedArch`, built by `CubeWorld.BuildEdgeWalls` for every still-locked face-edge doorway):
  - Iron bars and a solid collider fill the doorway. A plate stands in front, on the lane: a gold keyhole (`TownArt.Keyhole`) for a key lock, or the item's icon on a gold-rimmed dark plate (`TownArt.LockPlate`, so it never reads as a pickup).
  - A swing, or Interact within 3.3 units, tries the lock. An item lock also opens when walked into with the item. Walking into a key lock says "Locked: swing or [Interact] to use a key", or "Locked: it needs a key". An item lock says "Locked: it needs the Heavy Mitt\*".
  - Opening an edge opens all its arches on both faces (`EdgeLockState.Opened`). Arches built later on the reveal come up open.
  - `CubeNavigator` treats a locked edge as a wall (a backstop for the collider). Shift+F11 (debug) unlocks every arch.
  - All the art is drawn in code, so no art catalog, module library or scene regeneration was needed.
- **Map:** `MapMarkerKind.Lock` puts a padlock just inside each still-locked face-edge doorway of a visited screen ("once seen"). It is pale with a dark keyhole for a key lock, or with the item's region colour for an item lock. Opened arches have none. The legend gains "Locked arch" (11 labels, still 2 rows at 960 px).
- **Text** (`LockText`, `RewardText.ChestKeyPopup` "+1 chest key", the HUD line) is voice-neutral and in the InWorldText scans. It uses item names only through their starred names.
- **Sweep:** `SeedSweep.Run` checks the locks wherever the rewards are checked, on 3x3 faces only. *Assumption:* the N = 2 library sweeps keep the unlocked cube, as they keep no towns. The builder report gains a "Locks" line.
- **Determinism:** re-recorded twice, and the two recordings are byte-identical.
  - Changed: `rewards` (50 seeds: chest keys above k, a second key, one more pool spot from the 9-spot reservation) and `streams` (the new `edgeLocks` stream).
  - New: `locks`.
  - Every other line is unchanged.
- **Tests:**
  - New EditMode `EdgeLockTests` (7):
    - the 12 edges;
    - seeds 1–50 soft-lock-free, every edge locked, at least 2 first edges, every region a first route, at least 10 different first-route sets;
    - determinism, and `ForRun` matching the sweep;
    - the check flags a soft-lock and too few first edges;
    - a key lock takes the starting key, then a found key, used up; an item lock opens with its item only;
    - chest keys: 10 a run, their own pool, cashed in;
    - the text rules.
  - New PlayMode `LockSceneTests` (5, batch 2 in `vpart.sh` and `verify.sh`):
    - Town's 12 arches locked as planned with their plates, and `Keys: 1`;
    - a locked arch is a wall;
    - a swing spends the key and opens the whole edge, through to the reveal, with the far side open; the next key arch refuses, then opens by Interact with a found key;
    - an item arch opens with its item only;
    - a chest key opens an alcove (an arch key doesn't), and its chest pays 10;
    - the map's lock markers appear on seen screens and go once opened.
  - Updated:
    - edge-walking tests unlock first (`CubeSceneTests` ×3, `TownSceneTests`, `IslandWalkTests`);
    - the map truth test expects the lock markers;
    - the reward tests: keys 1–2, 6 kinds, the HUD's starting key;
    - `FaceRepairTests`: the slots above k are chest keys;
    - `RngStreamsTests` (stream 25);
    - `DeterminismTests` (`locks`);
    - the text scan.
- **Screenshots** (seed 7) are in the scratchpad's `snaps49/`: Town's key and item arches, an opened key arch, a science face's item arch, and alcoves with their keyholes. They came from a temporary snapshot test, since deleted. IMGUI (the HUD and map) cannot be captured in batch mode.

## Verification

**Result (2026-10-08, `vpart.sh` steps in the foreground as `v49c`):**
- EditMode 325/325 (7 new).
- PlayMode batches 39/22/42/28/38/32/1, all passing. Batch 2 has the 5 new `LockSceneTests`.
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s (unchanged).
- `IslandWalkTests`: passing.
- Sweep 50/50, 0 failing.
  - Locks: 237 key and 363 item locks; no spending order soft-locks on 50/50 seeds (845 states explored).
  - First edges: 2 on 33 seeds, 3 on 17. First routes by region: Biology 26, Chemistry 27, Physics 16, Math 25, Earth 23. That is 18 different sets of first routes. Draws kept: 76 in all, at most 8; 0 fallbacks.
  - Rewards: 498 keys, 500 chest keys, 1000 fragments; every kind on 50/50 seeds.
- `FIXTURE CHANGED` is the intended re-record described above, and `DeterminismTests` passes.
- Not committed.

**Result after the review patches (2026-10-08, `vpart.sh` steps in the foreground as `v49d`):**
- EditMode 326/326 (1 new: the `ForRun` timing).
- PlayMode batches 39/22/46/28/38/32/1, all passing. Batch 2 has 4 more `LockSceneTests`.
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s (unchanged).
- `IslandWalkTests`: passing.
- Sweep 50/50, 0 failing; the lock stats are the same as v49c (the same draws are kept).
- The fixture is byte-identical to v49c (`FIXTURE CHANGED` is against the baseline).
- Rebuild cost: `EdgeLocks.ForRun` on seeds 1–50, after a warm-up, takes 42.9 ms at most (mean 6.9 ms, median 4.8 ms). It was 65.0 ms at most before rejected draws stopped at their first soft-lock.
- Not committed.

## Review Triage Log

Pass 1 (quick lens), triaged by the coordinator: high 0, medium 1, low 3, all patched.

| Verdict | Route | Finding | Outcome |
|---|---|---|---|
| medium | patch | One Interact press could spend a key while also talking, advancing a conversation or using the Heavy Mitt\* | `LockedArch` and `AlcoveLock` read Interact in `LateUpdate`, through `LockInput.PressIsFree`. It never takes a press while the map is open, while a conversation is open or opened or closed that frame (MassMitt's guard), while an NPC with lines is in talking range (`NpcTalker.AnyInRange`), while the Mitt would act (`MassMitt.WouldInteract`), or while the player is dead or frozen. `LockInput.Claim` lets one press try at most one lock. PlayMode tests: an NPC beside an arch takes the press and no key is spent (with nobody there, the press uses the key); the Mitt grabs a boulder beside an alcove and no chest key is spent. |
| low | patch | On a Math island screen, an arch could be tried from an island the model says cannot reach that doorway | `LockedArch.TryOpen` accepts a try only from the doorway's own island (`SameRegion`, the same rule as `RegionOfDoor`); otherwise it returns `OutOfReach`. PlayMode test on the first seed with a locked arch on an island screen. |
| low | patch | Tests: no real swing opened an alcove, and the per-face key count was only checked as a range | New `ARealSwing_WithAChestKey_OpensABonusAlcove` (a `PlayerAttack` swing from in front of the gate). The key count is now exact per face (`RewardPlacementTests.ExpectedKeys`, from the face's pool size in the placement: 1 key for a pool of 7–8, 2 for 9 or more), in `RewardPlacementTests` and `EdgeLockTests`. |
| low | patch | A rebuild could hitch while `EdgeLocks.ForRun` checks the draws | Measured in a new EditMode test (seeds 1–50): 65.0 ms at most at first. Trimmed: a draw being tried stops at its first soft-lock (`Check(..., stopAtFirstProblem)`; the kept draw is still fully explored). Now 42.9 ms at most, under the ~50 ms budget; the test guards 250 ms. Same draws kept, so locks and fixture are unchanged. |
