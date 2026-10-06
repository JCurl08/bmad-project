---
title: 'Run loop, meta currency and upgrades'
type: 'feature'
ticket: '12'
created: '2026-10-05'
status: 'built'
baseline_revision: '343a8cdc12b31d1b15921bc688a08b4935c6a0e2'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/stack.md'
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/lore-and-tone.md'
  - '{project-root}/_bmad-output/initiative-video-game/epic-playable-cube/story-core-boss-single-phase-maxwell-s-demon-plan.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A run can end (RunState from 1.11), but nothing happens after it. There is no new run, no reward, and no reason to come back. CAP-12 and CAP-13 need a real roguelite loop: run, end, spend, run again, with progress that survives closing the browser.

**Approach:** Player death anywhere, or the core outcome, ends the run through RunState. A between-runs screen shows what this run earned and lets the player spend meta currency on stat upgrades (health, defence, power, speed). It then starts a new run: new seed, back in Town, abilities and inventory reset, full health. Meta currency is earned from trial completions, the core boss and hidden meta-currency items placed in module hidden-item slots. Meta progress is saved in a versioned, extensible save through PlayerPrefs (works on WebGL and desktop), including run count, victories and per-ability collection counts for Epic 2. First-run tuning: NPC hints are sparse on the first run (run count 0) and full afterwards.

## Boundaries & Constraints

**Always:** RunState stays the single place a run ends: death outside the core arena calls `EndRun(false)`, and the arena's own outcome already calls it. Currency earned during a run is banked when the run ends, win or lose (roguelite-friendly), and shown on the between-runs screen. Upgrade costs rise per level, there are max levels, and the effects go through PlayerStats only. The save is JSON in PlayerPrefs with a `version` field. Unknown or extra fields are tolerated, a missing or corrupt save starts fresh without crashing, and saving happens at run end and after each purchase. The new run's seed comes from a non-repeating source (e.g. time-based), but the seed is shown so a run can be replayed via debug. Hidden meta-currency items are placed by seed in hidden-item slots on built faces (own RNG stream, 16 and up; 2 to 15 are taken), never behind a required gate and never blocking lanes. Hint density comes from the saved run count. Text follows lore-and-tone.md and never says "sort".

**Never:** No run-only currency (CAP-24, cut). No kept gear, friendship or other Could items. No town draft (Epic 2). Do not place rewards behind the non-rolled beak's optional gates: that beak can't be obtained in the run, so the reward would be unreachable. This is deferred to epic-stretch (kept gear, CAP-19). No WebGL build in this session.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Death on a face | Player health reaches 0 outside the arena | Run ends (defeat), between-runs screen shows earnings | RunEnded fires once |
| Boss outcome | Arena win or loss | Run ends via RunState, screen shows the outcome and earnings | No double end |
| Earn | Trial completes; boss beaten; hidden item collected | Run earnings rise by each amount once | Re-triggering doesn't double-count |
| Bank | Run ends | Earnings added to the saved meta balance, save written | No error expected |
| Buy | Enough balance, Health not maxed | Level +1, balance minus cost, stat applied next run, saved | Insufficient balance or max level: refused with a message |
| New run | Press Continue on the between-runs screen | New seed, Town start, empty inventory, full health with upgrades applied, sparse→full hints after the first run | No error expected |
| Persistence | Reload (browser tab or editor play) | Balance, levels, run count, victories and per-ability counts restored | Corrupt or missing save: fresh defaults, no crash |
| Version | Save with an older or unknown version or extra fields | Loads known fields, keeps or ignores extras, upgrades version | No crash |
| Hidden items | Any seed | Placed in hidden-item slots on built faces, reachable, never behind a required or optional gate | Sweep reports the seed |

</frozen-after-approval>

## Code Map

- `game/Assets/Scripts/Cube/Run/RunState.cs` -- `RunEnded(bool)`, `EndRun(victory)` (returns false if already ended), `ResetRun()`, `Ended`, `Victory`, `EndCount`, `World`. Extend it into the loop; don't fork it.
- `game/Assets/Scripts/Cube/Core/CoreArena.cs` -- raises FightEnded, then `RunState.EndRun` as the last step. Its victory currency comes from here.
- `game/Assets/Scripts/Cube/Items/TrialRoom.cs` -- `Completed(int currency)`, fired once, used by each face's trial.
- `game/Assets/Scripts/Cube/Combat/PlayerStats.cs`, `Health.cs` (`Died`), `Equipment.cs`; `Items/Inventory.cs`; `Faces/Chemistry/Isotope.cs`, `Faces/Physics/MassMitt.cs` -- state that must reset on a new run.
- `game/Assets/Scripts/Cube/CubeWorld.cs` -- `Rebuild(seed)`, `Rebuilt`, `Revealed`. A new run equals a rebuild with a new seed plus a player reset.
- `game/Assets/Scripts/Cube/Modules/HiddenItemSlot.cs`, `Items/ItemPlacement.cs`, `SeedSweep.cs` -- hidden items need their own placement and a sweep rule.
- `game/Assets/Scripts/Cube/Npcs/HintGenerator.cs` (`FirstRunDensity`) and the hint-density fields on CubeDebug, BiologyFace and others -- drive them from the saved run count.
- `game/Assets/Scripts/Tracer/SaveProbe.cs` -- the existing PlayerPrefs pattern from 1.1.

## Tasks & Acceptance

**Execution:**
- [x] `game/Assets/Scripts/Cube/Run/MetaSave.cs` -- versioned JSON save (balance, upgrade levels, runCount, victories, per-ability collection counts keyed by item id), load and save through PlayerPrefs, tolerant parse.
- [x] `RunWallet.cs` -- this run's earnings (trial, boss, hidden items), each source counted once, banked on RunEnded.
- [x] `RunLoop.cs` -- subscribes to RunEnded and player death (death outside the arena ends the run). It shows the between-runs screen, applies purchases, and starts the next run (new seed, world rebuild, player reset: health, inventory, equipment, isotope, mitt, position in Town).
- [x] `UpgradeShop.cs` + `BetweenRunsScreen.cs` -- OnGUI screen showing earnings, balance, the four upgrades with level, cost and next effect, and Continue. Campy copy.
- [x] `HiddenCurrencyPlacement.cs` -- pure, seeded, own stream, in hidden-item slots on built faces with sweep rules (reachable, not gated). `HiddenCurrencyPickup.cs` adds to RunWallet.
- [x] Hint density from MetaSave.runCount for town, face populations and debug.
- [x] Per-ability counts: increment on first pickup of each item per run.
- [x] Wire everything into CubeSceneBuilder and regenerate the scene in batch mode.
- [x] Tests: EditMode tests for the save round trip, version and corrupt handling, cost curve, wallet once-only, and placement determinism and sweep. PlayMode tests for each I/O row, including a full loop (die → screen → buy → continue → new run with the stat applied), and persistence across a simulated reload.

**Acceptance Criteria:**
- Given the EditMode and PlayMode suites and the seed sweep in batch mode, when run, then all pass, including earlier stories'.
- Given the Cube scene, when the player dies or finishes the boss, then they see earnings, can buy an upgrade, start a new run in Town with the upgrade applied, and the progress survives a reload.

## Implementation Notes

- Verified in batch mode after the review patches: EditMode 182/182, PlayMode 112/112, seed sweep 50/50. WebGL build PENDING (user). Playthrough by eye PENDING (user): economy and upgrade feel.
- Checkpoint 1 was approved under the user's standing instruction (2026-10-05) to keep building while they are away. Review the plan on return.
- Verified in batch mode: Create Cube Scene (adds Run Wallet, Run Loop, Between Runs Screen), EditMode 179/179 (26 new in `MetaRunTests`), PlayMode 110/110 with `-nographics` (11 new in `RunLoopSceneTests`), seed sweep 50/50 (242 hidden items over 50 seeds). WebGL build skipped per Verification. Not committed. Playthrough by eye PENDING (user).
- Files: `Run/` `MetaSave` (versioned JSON in PlayerPrefs, key `entropy.meta`; keyed lists for upgrades and per-ability counts; tolerant parse: unknown fields ignored, missing ones default, negatives clamped, duplicates merged, version 0/missing upgraded to 1, a newer version kept, unreadable text starts fresh and is kept under `entropy.meta.corrupt`), `UpgradeShop` (`UpgradeStat`, `StatBlock`, cost 20 x (level + 1), max 5, +1 stat point per level, applied only through PlayerStats), `RunWallet` (+ pure `RunEarnings`: one payout per source key per run; stops once the run ended; `Bank` once per run), `RunLoop`, `BetweenRunsScreen`; `Items/` `HiddenCurrencyPlacement` (stream 16, 4-6 items, `IHiddenSlotCatalog` on ModuleLibrary, `FindProblems` for the sweep) and `HiddenCurrencyPickup`. Hint density: `IHintDensityTarget` on TownPopulation, the three faces and CubeDebug; `HintGenerator.DensityFor(runCount)`; RunLoop sets them at start and on every rebuild. `Isotope.ResetForRun`. CubeWorld places the hidden items on reveal (`HiddenCurrency`, `HiddenCurrencyPickups`); SeedSweep checks them when the catalog knows hidden slots.
- Loop: RunLoop listens to `PlayerStats.Died` (ignored during a live core fight with the player in the arena, so the arena's own `EndRun` stays the one end) and `RunState.RunEnded` (bank, `runCount++`, `victories++` on a win, save, freeze player control, open the screen). Continue = `NextSeed(current, clock ticks)` (never the current seed) then `StartRun(seed)`: densities, `world.Rebuild`, player reset (inventory, bare hands, isotope, mitt, base + upgrades, full health, control). The screen shows the ended run's seed; debug builds get a "Replay seed" button (`StartRun(seed)`). A rebuild from elsewhere (F5) closes the screen and gives a living player control back, but only Continue resets stats, health and items (keeps earlier stories' tests that tweak stats then rebuild unchanged). Per-ability counts: `Inventory.ItemAdded`, once per item id per run (the isotope's re-dispense does not count again); saved with the run end.
- Currency is called "Jumbles". Upgrades: Extra Heart, Thick Skin, Big Bonk, Zoomies. Keys 1-4 buy and Enter continues on the screen, as well as the buttons. No text says "sort" (EditMode test).
- Test isolation: every test in `Game.Cube.Tests` runs on its own empty save through `[SetUpFixture, IsolatedMetaSave]` (`MetaSaveIsolation.cs` in both test assemblies; the key becomes `entropy.meta.tests`, deleted before and after each test). An assembly-level `ITestAction` is not applied by the Unity test runner (tried first; that run wrote test data to the real key, which was then deleted from the editor's PlayerPrefs registry).
- Review fixes: defence is now multiplicative (`CombatMath.Incoming` = raw x 0.85^defence, floor 0.25 per hit; CombatHud shows health as a number too), so each Thick Skin level really cuts damage (-15%/-28%/-39%/-48%/-56%); the 1.6 defence tests use the new rule. The session's first run gets a fresh seed like Continue (`RunLoop` runs before `CubeWorld` via `DefaultExecutionOrder(-100)` and calls `CubeWorld.StartOnSeed`), unless `RunLoop.KeepSceneSeed` (tests) or the inspector's `keepSceneSeed` (debug) is set. Statics (`MetaSave.PrefsKey`, `RunLoop.KeepSceneSeed`) are restored after every test and reset on play start (`SubsystemRegistration`). A newer-version save is backed up once under `entropy.meta.newer` (status `Newer`) and written back as the current version. A rebuild while the player is dead resets the player fully; the freeze also disables Equipment. SeedSweep reports hidden currency in its own `HiddenProblems` field.
- Risks: Speed at level 5 is x2.25 movement -- tuning by eye pending. The core skill-check bot's time depends on frame rate: without `-nographics` it took 126 s and 141 s on seeds 1234 and 7 (over the 120 s bound); with `-nographics` it took 94-114 s, close to story 1.11's 94-105 s. Run PlayMode with `-nographics`, or loosen that test.

## Review Triage Log

Pass 1 (quick lens): high 1, medium 3, low 5, false 0, maybe-false 0.

| Verdict | Route | Finding | Evidence / action |
|---|---|---|---|
| high | patch | The Thick Skin (Defence) upgrade has no effect | All player damage is 1 and Incoming = max(1, raw − defence). Changed to raw × 0.85^defence with a 0.25 floor and fractional hearts. This supersedes the 1.6 design note's "minimum 1" (decided under the user's standing instruction, 2026-10-05). |
| medium | patch | Every launch starts on seed 1234 | Breaks the non-repeating seed rule. A fresh seed is now picked at session start, with a test override. |
| medium | patch | Test key static leaks into manual play (domain reload off), so later tests wipe the user's progress | The static is now restored and reset via RuntimeInitializeOnLoadMethod. |
| medium | patch | A newer-version save is stripped but keeps the newer version number | The original is backed up and the supported version is written. |
| low | patch | F5 after death leaves the player stuck | Full reset on rebuild while dead. |
| low | patch | FreezePlayer misses Equipment | Disabled too. |
| low | patch | Hidden-currency sweep sets ItemsChecked | Now its own field. |
| low | reject | Configure at runtime skips the base-stat capture | Not reachable: the scene serializes the fields before Awake, and the fix adds paths. |
| low | reject | FindObjectsByType sweep for hint density | Runs a few times per run or debug rebuild, so the cost is negligible. |

## Design Notes

Suggested economy: trial 25 each (3 faces), boss victory 60, hidden items 5 each (about 4–6 per run). Upgrade cost = 20 × (level + 1), max level 5. One upgrade after a decent first run, and a few runs to max one stat.

## Verification

**Commands:**
- Unity batch `-runTests -testPlatform EditMode` and `PlayMode` -- expected: result Passed, 0 failed.
- Unity batch `-quit -executeMethod CubeSceneBuilder.RunSeedSweep` -- expected: 50/50, no softlocks.
- WebGL build: skip during this session (low memory); the user builds later.
