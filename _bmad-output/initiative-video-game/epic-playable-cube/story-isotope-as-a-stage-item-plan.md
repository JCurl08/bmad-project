---
title: 'Isotope as a stage item'
type: 'feature'
ticket: '18'
created: '2026-10-06'
status: 'built'
baseline_revision: 'a842e4594cc47e01c7b0b83552842d1b43935b1c'
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

**Problem:** In the playtest the isotope was confusing:
- The timer changes stages on its own.
- It isn't clear how to use it (touch, attack or step).
- Getting it means a dispenser rather than a normal item.

At 3×3, far gates are also out of reach of the timed stages. The user wants "an actual item, not a pickup. Maybe the item could be a specific stage but the lore explains that they all transform into each other."

**Approach:** Rework CAP-5 to follow the beak pattern from story 1.8.
- **Item variants:** the Chemistry item becomes three variants, Glowing, Unstable and Lead (placeholder names marked `*`). Each run rolls one. It is picked up once, kept for the run, and used by attacking:
  - Glowing lights a dark-room gate.
  - Unstable breaks a cracked wall.
  - Lead weighs down a plate.
- **Removed:** no timer, no dispenser, no stage changes during a run.
- **Optional gates:** the other stages' gates become optional extras that guard nothing required, as with the non-rolled beak.
- **Trial:** the Chemistry trial is redesigned around the rolled stage.
- **Lore:** one in-world lore line says the stones change from one state to the next over time (between runs). It is subtle, with no science words.

## Boundaries & Constraints

**Always:**
- **Reuse the variant system** (`ItemCatalog.RollVariant`/`ItemFor`/`VariantsOf`, `ItemVariants`, optional gates in `ItemPlacement`, and the sweep's optional-gate rule) exactly as Biology does. Chemistry stays one logical item for the placer.
- **Stage resolution:** stage kinds are resolved from the item id, never from catalog order (lesson from 1.8).
- **One rule for using items:** all stages are used by attacking with the stage item equipped. Dark-room gates no longer open on touch, and plates open when attacked with Lead, not when stepped on.
- **Placement rules:** the rolled stage opens at least one gate on another face (sweep rule). Gates of the other stages are optional and guard nothing required (sweep rule).
- **Difficulty:** a difficulty parameter (the number of stage gates on the face) feeds each stage gate.
- **Determinism:** determinism signatures are re-recorded.
- **Text:** in-world text follows the subtle-science rule (no "isotope", "radioactive", "decay", "half-life", "lead" as a chemistry term is fine as an everyday word but avoid "element"). The item and stage names get `*` placeholders, e.g. "Glowstone*", "Wildstone*", "Leadstone*". The HUD stage label shows the item's name, not a timer.
- **Feedback:** the 1.15 pickup popup, hit flash and gate-open effect still apply.

**Never:**
- No other faces' changes beyond Chemistry, the catalog builder, placement, the sweep and HUD wiring.
- No change to Biology or Physics behaviour.
- No new art pack. Reuse ArtCatalog keys, with a key per stage item.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Roll | Seeds 1–50 | Each stage is rolled at least once; same seed, same stage | No error expected |
| Pickup | Player touches the Chemistry item | Item added once, equipped, popup shown; nothing respawns | Already held: nothing |
| Glowing | Attack a dark-room gate with Glowing | It opens | Other items or bare attack: no effect |
| Unstable | Attack a cracked wall with Unstable | It opens | Other items: no effect |
| Lead | Attack a plate with Lead | The plate's door opens (the item is not consumed) | Other items: no effect |
| Optional | Run with any stage | Other stages' gates exist, guard nothing required | Sweep flags violations |
| Off-face | Any seed | The rolled stage opens ≥1 gate on another face | Sweep reports the seed |
| Trial | Rolled stage | Chemistry trial solvable with that stage only, fires once | Repeats don't fire |
| No timer | Hold the item for 60 s | Stage never changes | No error expected |
| Text | All Chemistry in-game strings | No banned science words; placeholder names carry `*` | A test fails otherwise |

</frozen-after-approval>

## Code Map

- `game/Assets/Scripts/Cube/Faces/Chemistry/`:
  - `Isotope.cs` (timer and stage state on the player): replace it with a stage read from the equipped item, or remove the component.
  - `IsotopeDispenser.cs`: remove, together with its pickup override hook usage.
  - `IsotopeGate.cs`, `DarkRoomGate.cs`, `CrackedWallGate.cs`, `LeadPlate.cs` and `LeadPlateGate.cs`: switch them to attack-to-use with the stage item.
  - `ChemistryPlan.cs`: stage per gate, sweep rules (drop dispenser-first, keep off-face).
  - `ChemistryTrial.cs` and `ChemistryTrialPiece.cs`: redesign around the rolled stage.
  - `ChemistryFace.cs` (IFaceContent) and `ChemistryPopulation.cs`: lines.
- Pattern to copy: `Faces/Biology/BiologyBeaks.cs` (the variant kind comes from the item id), `BiologyPlan.cs` (rolled variant and optional gates), `BeakGate`, `BreakableGate` (attack-to-open via `IAttackReceiver`).
- `game/Assets/Editor/ItemCatalogBuilder.cs`: add the three stage variants under Chemistry and keep id stability. `Items/ItemCatalog.cs` and `Items/ItemVariants.cs`.
- `game/Assets/Scripts/Cube/SeedSweep.cs`: remove the dispenser-first rule, keep the Chemistry trial fit, add the Chemistry optional-gate rule if it isn't generic.
- `game/Assets/Scripts/Cube/Art/ArtCatalog.cs` and `Editor/ArtCatalogBuilder.cs`: an item key per stage.
- `game/Assets/Scripts/Cube/Run/RunLoop.cs` (`ResetPlayer` resets the isotope) and `Combat/CombatHud.cs` / the isotope stage label from 1.15.
- Tests: `Tests/EditMode/ChemistryTests.cs`, `Tests/PlayMode/ChemistryComponentTests.cs` and `ChemistrySceneTests.cs`. Rewrite them for the new design, keep the hint-truth checks, and add a subtle-science text test for Chemistry strings.

## Tasks & Acceptance

**Execution:**
- [x] Catalog: three Chemistry stage variants with `*` names, an ArtCatalog key each, and stage kind by id.
- [x] Remove the timer, the dispenser and stage changes. Route the player's held stage from the equipped item.
- [x] Stage gates opened by attacking with the matching stage item, with a difficulty parameter.
- [x] ChemistryPlan: the rolled stage's gates are required, other stages' gates optional, off-face rule kept, dispenser-first dropped. Wire it into the sweep.
- [x] Chemistry trial redesigned around the rolled stage (e.g. three of that stage's targets in an order or pattern), firing once.
- [x] Lines: a subtle "the stones change between visits" lore line, and an NPC hint naming the rolled stone*.
- [x] Regenerate the catalog, library and scene as needed, and re-record the determinism signatures.
- [x] Tests rewritten for every I/O row, plus the text rule.

**Acceptance Criteria:**
- Given the full suites and the 50-seed sweep, when run, then all pass.
- Given the Cube scene, when the player gets the Chemistry item, then it stays the same, is used by attacking, opens its gates and the trial, and no timer or dispenser exists.

## Implementation Notes

- Checkpoint 1 approved under the user's standing instruction (2026-10-06).
- Verified in batch mode after the review patches: EditMode 199/199, PlayMode 121/121, seed sweep 50/50. User playtest PENDING.
- Verified 2026-10-06 in batch mode: EditMode 198/198, PlayMode 120/120 (`-nographics`), seed sweep 50/50 (13 Glowstone*, 21 Wildstone*, 16 Leadstone*; 649 stage gates, 349 optional). Item catalog, art catalog and Cube scene regenerated; determinism fixture re-recorded. Playthrough by eye PENDING (user). Not committed.
- **Catalog:** variants `glowstone`/`wildstone`/`leadstone` ("Glowstone*", "Wildstone*", "Leadstone*", assets `Assets/Items/<Name>.asset`), canonical order beaks then stones. The base entry keeps id `chemistry-item` and is renamed in place to "Changing Stone*". Stage is read by id (`ChemistryIsotope.KindOf`/`VariantKinds`). ArtKey `ItemIsotope` (53) renamed `ItemGlowstone`; new `ItemWildstone` 56 (D 115, red potion) and `ItemLeadstone` 57 (D 113, grey flask); `ArtKey.Dispenser` removed.
- **No timer/dispenser:** `IsotopeDispenser` deleted; `IFacePickupContent`, `ItemPickup.Refills`/`BeforeGive`/`OnGiven`, `Isotope.On`, `LeadPlate.ClearLead` and `ChemistryPlan.CountOf` removed (review fix). The `Isotope` component (same file/GUID) is now a stateless reader of the held stone: label = item name, HUD line = name + use, halo; `RunLoop.ResetPlayer` no longer resets it.
- **Gates:** `IsotopeGate` follows `BeakGate`: `Difficulty`/`FaceSize`/`Optional`, `HitsToOpen = ChemistryPlan.HitsFor` (same tuning as beaks). Dark room and cracked wall open to swings; plates (`LeadPlate`) react only to a swing with the Leadstone*, not to steps, and the stone is kept.
- **Plan/placement:** required gates = rolled stone; optional gates = other stones on Chemistry. `ItemPlacement` now deals optional gates one per non-rolled variant per round (identical draws for Biology's single other beak), so both other stones show up at N=3 on every seed. `ChemistryPlan.FindProblems` checks stones, off-face gate, difficulty, trial fit and (when there are at least 2 optional gates) one optional gate per other stone; dispenser-first and stage-coverage rules removed. `SeedSweep.Run` takes `chemistryVariantKinds`.
- **Trial:** three numbered targets of the rolled stone's kind (lamps / cracked blocks / plates) struck in order; a wrong one resets; fires once. Same 3 spots on the population stream as before.
- **Lines:** mushroom 0 names the rolled stone (`Dialogue.StoneLines`), mushroom 1 says `StoneLoreLine` (stones change between visits), others say patron lines rewritten around "the Glowmother*" (no real name, no science words); mushroom rumour/greeting and the Chemistry hint nickname ("changing stone") also cleaned. Text test bans isotope/radioactive/decay/half-life/element/curie etc. and checks the `*` on placeholder names.
- **Determinism:** fixture changes are Chemistry optional gates and roll in `items` (required gates and pickups unchanged), `chemistryPlan`, `chemistryPopulation`, and `physicsPlan` on 8 seeds (off-home Chemistry gates on the Physics face are no longer always lead plates, so boulder spots shift; Physics code unchanged).

## Review Triage Log

Pass 1 (quick lens): high 0, medium 3, low 4, false 1, maybe-false 0.

| Verdict | Route | Finding | Evidence / action |
|---|---|---|---|
| medium | patch | Physics resolves the stone kind from catalog order (the 1.8 bug class) | OtherFaceMarks called ChemistryPlan.Create without kinds. Kinds are now threaded through, the order fallback removed, and a reversed-catalog test added. |
| medium | patch | A new pickup is equipped only with an empty hand | A player holding the beak couldn't use the stone on gates. Every new item now auto-equips, with a test. |
| medium | defer | Generated hints name "Chemistry" (subtle-science rule) | Shared HintGenerator text for all faces. Story 1.22 rewrites all in-world text, and this is added to its scope explicitly. |
| low | patch | Lore line implies a fixed stone order, but the roll is random | Reworded to be order-free. |
| low | patch | "old Marcel" lacks the `*` | Added, and the text test extended. |
| low | patch | Dead code from the dispenser and timer, plus stale docs | Removed and fixed. |
| false | reject | Physics layouts change on 8 seeds | An intended consequence: off-home Chemistry gates now vary by rolled stone. The Physics code is unchanged and adapts, and the sweep passes. |

- **Review fixes:** `ChemistryPlan.Create`/`FindProblems` require the id-derived stone kinds (no catalog-order fallback; `ChemistryIsotope.FromVariant`/`Roll` deleted); `PhysicsPlan.Create`/`FindProblems` take `chemistryVariantKinds` (PhysicsFace, SeedSweep, CubeSceneBuilder, tests pass them), with a reversed-catalog test proving Physics marks the real Leadstone* plates. `Equipment` now equips every newly added item (combat tests adapted; new test: holding a beak, picking up a stone equips it). Lore line no longer implies a fixed order; "old Marcel*" starred and the text test flags unstarred capitalised names.

## Verification

**Commands:**
- Unity batch `-runTests -testPlatform EditMode` and `-testPlatform PlayMode -nographics` -- expected: Passed, 0 failed.
- Unity batch `-quit -executeMethod CubeSceneBuilder.RunSeedSweep` -- expected: 50/50.
