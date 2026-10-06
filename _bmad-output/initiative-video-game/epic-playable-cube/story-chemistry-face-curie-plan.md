---
title: 'Chemistry face (Curie)'
type: 'feature'
ticket: '9'
created: '2026-10-05'
status: 'built'
baseline_revision: 'aa16c294582999e83b4021798ea63e596395cf9d'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/faces-and-races.md'
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/lore-and-tone.md'
  - '{project-root}/_bmad-output/initiative-video-game/epic-playable-cube/story-biology-face-darwin-vertical-slice-plan.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The Chemistry face is still placeholder modules with a placeholder item. Curie's face is where the game's entropy theme is most literal (radioactive decay), so it needs its signature item and puzzles.

**Approach:** The Chemistry item is a re-obtainable isotope that decays on a half-life timer through three stages. Glow lights dark-room gates, unstable blasts cracked-wall gates, and lead holds a pressure-plate gate open when dropped on it. A dispenser at the item's pickup spot hands out a fresh isotope whenever the player has none. Add a Chemistry trial combining stages, Chemistry enemies, mushroom-people NPCs with Curie-flavoured dialogue, and a half-life difficulty parameter. Plug into the same `IFaceContent` hook Biology uses.

## Boundaries & Constraints

**Always:** Follow the Biology pattern: `Faces/Chemistry/` holds gate behaviours, the trial and the population, wired through IFaceContent, with plan and population on their own new RNG streams (11 and up; 2 to 10 are taken). The isotope advances on its timer: glow → unstable → lead. Lead stays until it is dropped on a plate, which consumes it, and the dispenser can then hand out a new one. The dispenser refills only when the player holds no isotope. Each gate opens only to its stage: glow at dark-room gates, unstable at cracked walls, lead dropped on a plate. Chemistry stays one logical item for the 1.4 placer, and the item opens at least one gate on another face. The seed sweep must enforce that the dispenser is reachable without passing any Chemistry-stage gate, so a player can always get a fresh isotope before a stage gate. The trial fires `TrialRoom.Completed(currency)` once. Tone follows lore-and-tone.md (skeleton-key fingers gag welcome). Faces stay 2×2.

**Never:** No Physics content (1.10). No currency payout (1.12). No changes that break Biology's or 1.4's sweep guarantees. No imported art.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Dispense | Player without an isotope touches the dispenser | Receives a fresh isotope at the glow stage | Holding one already: nothing happens |
| Decay | Time passes while holding the isotope | glow → unstable → lead at the half-life thresholds | Lead stays lead |
| Glow gate | Touch a dark-room gate while the isotope glows | It opens | Wrong stage: stays shut |
| Unstable gate | Attack a cracked-wall gate with the unstable isotope equipped | It blasts open | Wrong stage: stays shut |
| Lead plate | Use or drop lead on a pressure plate | The plate's door opens and the lead is consumed | Wrong stage: no effect |
| Re-obtain | Lead consumed, back at the dispenser | A fresh glow isotope | No error expected |
| Off-home | Any seed | The isotope opens ≥1 gate on another face | Sweep reports the seed |
| Dispenser first | Any seed | The dispenser is reachable without passing a Chemistry-stage gate | Sweep reports the seed |
| Trial | Solve the Chemistry trial using two or more stages | Completion fires once | Repeats don't fire |
| Enemies | Chemistry enemy hit with its weakness vs other | Markedly more damage from the weakness | No error expected |
| Mushroom NPCs | Chemistry face revealed | Talkable, Curie-flavoured, with true hints | No error expected |

</frozen-after-approval>

## Code Map

- `game/Assets/Scripts/Cube/Faces/IFaceContent.cs` -- hook: `Theme`, `BeginReveal(world)`, `CreateGate(FaceGateRequest)` (null falls back to a plain Gate), `EndReveal`, `Clear`. Biology implements it in `Faces/Biology/BiologyFace.cs`. Copy that structure.
- `game/Assets/Scripts/Cube/Faces/Biology/` -- `BiologyPlan` (stream 9: pure, seeded, `FindProblems` for the sweep), `BiologyPopulation` (stream 10), `BiologyTrial`, `BreakableGate`, `ButtonGate`, `BeakGate`, `TrialTarget`, `BiologyEnemies`. Mirror these names for Chemistry.
- `game/Assets/Scripts/Cube/Faces/FaceSpots.cs` -- shared clear-spot logic for face content.
- `game/Assets/Scripts/Cube/Items/Gate.cs` (extension points for mechanic-opened gates), `ItemPickup.cs` (removes itself once collected; the dispenser replaces this for Chemistry), `ItemPlacement.cs` (`PickupPlacement`, stream 3).
- `game/Assets/Scripts/Cube/Combat/IAttackReceiver.cs`, `PlayerAttack.cs` (`EquippedItem`), `Equipment.cs`.
- `game/Assets/Scripts/Cube/SeedSweep.cs` -- add a Chemistry `FindProblems` with the dispenser-first rule, as Biology does.
- `game/Assets/Editor/ItemCatalogBuilder.cs` -- the Chemistry item already exists as a placeholder. Replace or rename it to the isotope while keeping its id stable.

## Tasks & Acceptance

**Execution:**
- [x] `Faces/Chemistry/Isotope.cs` -- the stage state (glow, unstable, lead) and timer for the held isotope, with a half-life difficulty parameter. The player's held isotope drives the equipped item's stage. A HUD hint shows the current stage.
- [x] `IsotopeDispenser.cs` -- replaces the one-shot pickup at the Chemistry pickup spot and refills when the player has none.
- [x] Gates: `DarkRoomGate` (glow), `CrackedWallGate` (unstable), `LeadPlateGate` (lead dropped or used on a plate consumes the lead and opens the door).
- [x] `ChemistryPlan.cs` (pure: stage per gate across the face's gates, with each stage used at least once when there are enough gates; trial screen; `FindProblems` including dispenser-first) and the sweep wiring.
- [x] `ChemistryTrial.cs` -- for example, light a dark corridor (glow), then blast a cracked wall (unstable), then weigh a plate (lead) within the timing. Fires the trial event.
- [x] Chemistry enemies: 1–2 types (e.g. "Free Radical", weak to the isotope; "Rust Mite", weak to another built item).
- [x] `ChemistryPopulation.cs` -- mushroom-people NPCs with Curie-flavoured lines ("Glow responsibly!", skeleton-key fingers).
- [x] Wire into CubeWorld (IFaceContent) and CubeSceneBuilder, regenerate in batch mode, and update the item catalog builder.
- [x] Tests: EditMode tests for decay timing, plan determinism, dispenser-first sweep and stage coverage. PlayMode tests for each I/O row and a full run from Town through dispenser, stage gates and trial.

**Acceptance Criteria:**
- Given the EditMode and PlayMode suites and the seed sweep in batch mode, when run, then all pass, including earlier stories', with 50/50 and no softlocks.
- Given the Cube scene, when the player reaches the Chemistry face, then it reads as Curie's: mushroom people, a glowing isotope that decays, and stage gates and a trial.

## Implementation Notes

- Verified in batch mode: Build Item Catalog (Chemistry entry renamed to "Isotope" in place, id `chemistry-item` kept, 3 items + 2 variants), Create Cube Scene, seed sweep "50/50 ... the isotope dispenser before any Chemistry-stage gate, 0 failing" (isotope gates: 50 glow, 50 unstable, 50 lead; 19 seeds with the dispenser behind another item's gate), EditMode 125/125, PlayMode 74/74. WebGL build skipped per Verification. Not committed.
- Isotope (`Faces/Chemistry/Isotope.cs`): `ChemistryIsotope` holds the pure timing (`StageAt(age, halfLife)`: glow until H, unstable until H * 5/3, then lead for good; default H = 12 gives 0-12 / 12-20 / lead). The `Isotope` component on the player watches its Inventory: every add of the isotope item restarts it at glow and bumps a `Serial`; the age runs while held; `ConsumeLead` removes the item (new `Inventory.Remove`/`ItemRemoved`). A halo sprite and an OnGUI line with a coloured pip (above the CombatHud) show the stage. Half-life is set from `ChemistryFace.HalfLife` (the difficulty knob) on every reveal.
- Dispenser: `IsotopeDispenser` derives from `ItemPickup` (new `Refills`/`OnGiven` hooks: a refilling pickup stays and never sets Collected). It refills only when the player holds no isotope because `Inventory.Add` refuses an item already held. CubeWorld builds pickups through a new optional `IFacePickupContent` hook (BiologyFace is unchanged), so the dispenser sits at the Chemistry pickup's spot and stays in `CubeWorld.Pickups`.
- Gates: `IsotopeGate` base; `DarkRoomGate` opens on touch while glowing (new virtual `Gate.CanOpenFor`) or on a glowing swing; `CrackedWallGate` opens only to a swing with the unstable isotope equipped; `LeadPlateGate` links a `LeadPlate` in the horizontal lane (`ButtonGate.ButtonLocal`): stepping on it holding lead, or swinging lead at it, uses up the lead and opens the door. A plate whose door is open takes no more lead.
- Plan (stream 11): every off-home Chemistry gate is lead (the timed stages only last 20 s at the default, too short to cross faces); home gates get glow and unstable (plus lead when there is no off-home gate) in a seeded order, then seeded stages. `FindProblems` checks a stage per gate, timed stages on the home face, an off-home gate, all three stages with 3+ gates, the trial on Chemistry, and `DispenserProblems` (dispenser-first: the dispenser's guard chain is reachable and never meets a Chemistry gate or loops). SeedSweep.Run adds these as "chemistry:" problems.
- Trial (`ChemistryTrial`, currency 25): lamp (glow, touch or swing), cracked wall (unstable, swing equipped), plate (lead), in order, all with the same isotope (`Serial`), which is the "within the timing" rule. Once the tracked isotope is used up or replaced the puzzle resets, and a fresh glowing isotope relights the lamp. Review fixes: the dispenser takes held lead back and swaps it for a fresh isotope (a lead sink that never depends on the trial); FindProblems checks the trial fits (with modules from the sweep) and a coverage rule `RequiredStages(home, offHome)` that Create always meets; lead plates sit at `LeadPlateGate.PlateLocal` (x = ±3.2), never on a Biology button spot (EditMode test over 50 seeds); `ItemPickup.PickedUp` fires per give. Pieces use Biology's lane-side spots and spacing, taking the first draws of the population stream (12); an EditMode test checks every seed's trial fits. The trial plate always takes lead, so spent lead can always be dropped (otherwise a player holding lead after every plate door was open could never get a fresh glowing isotope).
- Population (stream 12): 3 mushrooms (NPC index base 3000) with a Curie line (`Dialogue.CurieLines`: "Glow responsibly!", skeleton-key fingers) and sparse true hints; mushroom 0 adds `Dialogue.IsotopeStageLine` (the mechanics). Enemies: 2 Free Radicals (weak to the isotope) and 1 Rust Mite (weak to the run's Biology beak, else Physics'), quadrant-bound with sight 4.
- Earlier test adapted: ItemSceneTests' plain-item walk test now skips themes with face content (the isotope no longer opens gates by plain touch).
- Verified in batch mode after the review patches: EditMode 128/128, PlayMode 77/77, seed sweep 50/50 including dispenser-first and trial-fit. WebGL build PENDING (user). Playthrough by eye PENDING (user).
- Checkpoint 1 was approved under the user's standing instruction (2026-10-05) to keep building while they are away. Review the plan on return.

## Review Triage Log

Pass 1 (quick lens): high 1, medium 3, low 3, false 0, maybe-false 0.

| Verdict | Route | Finding | Evidence / action |
|---|---|---|---|
| high | patch | Softlock if the trial doesn't fit: no always-accepting lead sink | Inventory refuses a refill while lead is held, and the trial plate was the only sink. The dispenser now takes lead back, and FindProblems checks the trial fits. |
| medium | patch | Lit lamp ignores a touch by a fresh isotope, then the wall swing resets the puzzle with the lamp dark | The Done guard in Touch skipped OnPiece. The lamp now relights with the new serial, with a test through Touch and attack. |
| medium | patch | Trial stranded on a consumed isotope's serial | TakeLead left progress at 1. It now resets when the tracked isotope is consumed. |
| medium | patch | Stage coverage not guaranteed by Create | It held only by luck on the swept seeds. Create now enforces it, with a test. |
| low | patch | ItemPickup.PickedUp "raised once" contract broken by refills | Contract fixed. |
| low | patch | Lead plate can coincide with a Biology button spot | Both used ButtonLocal. Positions are now disjoint, with a check. |
| low | patch | RngStream comment wrong | Fixed. |

## Design Notes

Suggested timing: glow 0–12 s, unstable 12–20 s, then lead. Half-life is the difficulty knob (shorter means harder). The HUD shows the stage with a coloured pip.

## Verification

**Commands:**
- Unity batch `-runTests -testPlatform EditMode` and `PlayMode` -- expected: result Passed, 0 failed.
- Unity batch `-quit -executeMethod CubeSceneBuilder.RunSeedSweep` -- expected: 50/50, no softlocks.
- WebGL build: skip during this session (low memory); the user builds later.
