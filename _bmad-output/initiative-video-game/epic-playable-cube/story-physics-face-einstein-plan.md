---
title: 'Physics face (Einstein)'
type: 'feature'
ticket: '10'
created: '2026-10-05'
status: 'built'
baseline_revision: '2f0fd44d90edaf478a38592052343b63e696d377'
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
  - '{project-root}/_bmad-output/initiative-video-game/epic-playable-cube/story-chemistry-face-curie-plan.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The Physics face is still placeholder content. Einstein's signature puzzle (mass slows time near timed doors) is the last face mechanic the Must tier needs.

**Approach:** The Physics item is a "Mass Mitt" that lets the player grab and drag boulders. Each timed door is opened by a switch and then closes on a timer. Boulders near the door add mass, and mass slows the door's local time, so it stays open longer. Enough boulders turn an impassable door into a passable one. The number of boulders needed is the difficulty parameter. Add a Physics trial with several timed doors, Physics enemies, alien NPCs with Einstein-flavoured dialogue, and a sweep rule that enough reachable boulders exist for every required timed door. Plug into IFaceContent like Biology and Chemistry.

## Boundaries & Constraints

**Always:** Follow the Biology and Chemistry pattern (`Faces/Physics/`, IFaceContent, plan and population on new RNG streams 13 and up; 2 to 12 are taken). Physics stays one logical item for the 1.4 placer, and the Mass Mitt opens at least one gate on another face. Each off-home timed door brings its own boulders on its screen. Without the mitt, boulders can't be moved. The time-dilation factor depends only on the total boulder mass within a radius of the door: more mass, slower closing. Each required timed door has its required boulder count reachable on its own screen, and the seed sweep enforces it. Boulders never block exit lanes, the screen centre, or another face's interactables (reuse the shared spot rules from 1.8 and 1.9). The trial fires `TrialRoom.Completed(currency)` once. Tone follows lore-and-tone.md (Newton apple cameo welcome). Faces stay 2×2.

**Never:** No currency payout (1.12). No changes that break Biology's, Chemistry's or 1.4's sweep guarantees. No physics tricks that let boulders push the player through walls. No imported art.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| No mitt | Player without the Mass Mitt pushes a boulder | It does not move | No error expected |
| Drag | Player with the mitt grabs and moves a boulder | It follows, and stops at walls | Released when the player moves away or presses again |
| Timed door, no mass | Hit the switch, walk to the door | The door closes before the player arrives (impassable) | No error expected |
| Timed door, required mass | Required boulders beside the door, hit the switch | The door stays open long enough to pass | No error expected |
| More mass | More boulders than required | Even slower closing (monotonic) | No error expected |
| Off-home | Any seed | The Mass Mitt opens ≥1 gate on another face, with its boulders on that screen | Sweep reports the seed |
| Enough boulders | Any seed | Every required timed door has ≥ its required boulder count reachable on its screen | Sweep reports the seed and door |
| Trial | Solve the Physics trial (several timed doors) | Completion fires once | Repeats don't fire |
| Enemies | Physics enemy hit with its weakness vs other | Markedly more damage from the weakness | No error expected |
| Alien NPCs | Physics face revealed | Talkable, Einstein-flavoured, with true hints | No error expected |

</frozen-after-approval>

## Code Map

- `game/Assets/Scripts/Cube/Faces/IFaceContent.cs`, and `Faces/Biology/*` and `Faces/Chemistry/*` -- the pattern to mirror. Chemistry has `ChemistryPlan` (stream 11), `ChemistryFace` (population stream 12), `IsotopeGate`, `LeadPlate`, `ChemistryTrial`/`ChemistryTrialPiece`, `ChemistryEnemies`, `ChemistryPopulation`.
- `game/Assets/Scripts/Cube/Faces/FaceSpots.cs`; `Faces/Chemistry/LeadPlateGate.cs` (`PlateLocal` x = ±3.2 in the horizontal lane); `Faces/Biology/ButtonGate.cs` (`ButtonLocal` x = ±5). New switch, door and boulder spots must not collide with these (the spot test from 1.9 checks pairs within 1.0).
- `game/Assets/Scripts/Cube/Items/Gate.cs` (mechanic-opened gates), `ItemPickup.cs`, `ItemPlacement.cs`; `Editor/ItemCatalogBuilder.cs` (the Physics placeholder becomes the Mass Mitt, keeping its id stable).
- `game/Assets/Scripts/Cube/Combat/*` -- the enemy base, `IAttackReceiver`, `PlayerAttack`.
- `game/Assets/Scripts/Cube/SeedSweep.cs` -- add a Physics `FindProblems` (enough boulders per required door, off-home door with its boulders).
- `game/Assets/Scripts/Tracer/PlayerMover.cs` -- the player is a dynamic body. Dragging must not let boulders shove the player into walls.

## Tasks & Acceptance

**Execution:**
- [x] `Faces/Physics/Boulder.cs` (mass, draggable only with the mitt via `MassMitt.cs`, which grabs on Interact or attack with the mitt equipped and releases on a second press or when too far) and `TimeField.cs` (a door queries the total boulder mass within a radius).
- [x] `TimedDoorGate.cs` (a switch opens it, and it closes after `baseOpenSeconds × dilation(mass)`) plus `DoorSwitch.cs`. With no mass the door must be impassable from the switch's distance at player speed; with the required mass it must be passable.
- [x] `PhysicsPlan.cs` -- pure. Boulder spots per gate (required count plus 0–1 spare), difficulty parameter, trial screen, `FindProblems` (enough reachable boulders per door, off-home door boulders, spot collisions with other faces' interactables). Wire it into the sweep.
- [x] `PhysicsTrial.cs` -- for example, two or three timed doors in a row sharing a limited set of boulders, so the player must move boulders between doors. Fires the trial event.
- [x] Physics enemies: 1–2 types (e.g. "Quantum Flea", weak to the Mass Mitt; "Static Cling", weak to another built item).
- [x] `PhysicsPopulation.cs` -- alien NPCs with Einstein lines ("It's all relative, darling!"). Optional Newton cameo NPC.
- [x] Wire into CubeWorld (IFaceContent) and CubeSceneBuilder, update the item catalog builder, and regenerate in batch mode.
- [x] Tests: EditMode tests for the dilation maths (monotonic, impassable at 0, passable at the required count), plan determinism, and the sweep rules. PlayMode tests for each I/O row and a full run from Town through the mitt, a timed door with boulders, and the trial.

**Acceptance Criteria:**
- Given the EditMode and PlayMode suites and the seed sweep in batch mode, when run, then all pass, including earlier stories', with 50/50 and no softlocks.
- Given the Cube scene, when the player reaches the Physics face, then it reads as Einstein's: aliens, boulders, a mitt, timed doors that stay open longer with more mass, and a trial.

## Implementation Notes

- Verified in batch mode after the review patches: EditMode 144/144, PlayMode 87/87, seed sweep 50/50 including the boulder and Physics-trial rules. WebGL build PENDING (user). Playthrough by eye PENDING (user), especially whether timed doors feel fair with keyboard movement.
- Checkpoint 1 was approved under the user's standing instruction (2026-10-05) to keep building while they are away. Review the plan on return.
- Verified in batch mode: Build Item Catalog (Physics entry renamed to "Mass Mitt" in place, id `physics-item` kept, 3 items + 2 variants), Create Cube Scene, seed sweep "50/50 ... every timed door with enough reachable boulders on its screen, the Physics trial fitting, 0 failing" (150 timed doors, 50 off-home, needing 1/2/3 boulders 51/48/51; the trial fits on all 50 seeds), EditMode 142/142, PlayMode 86/86. WebGL build skipped per Verification. Not committed. Playthrough by eye PENDING (user).
- Maths (`TimeField`): dilation = 1 + 0.6 × mass (1 per boulder), as suggested. Deviations from the Design Notes, needed to fit 2×2 screens: the field radius is 2.75 (not 3) and it is measured around the door's threshold (a point in the lane 0.7 in front of the door), not the door's centre; and the base open time is per door, `BaseSecondsFor(walk, required) = walk / dilation(required − 0.5)`, where walk is the switch-to-threshold time at base speed 6. With radius 3 around the door centre, screens with two doors had no legal starting spot outside every field. A switch 16.8 units away (2.8 s) does not fit on a 16-unit screen. So the required count maps exactly whatever the switch distance: one boulder short shuts ≥10% early, the required count stays open ≥10% longer.
- Door (`TimedDoorGate`, `DoorSwitch`, `DoorLatch`, `BoulderBlocker`): the switch is a floor trigger in the horizontal lane on the far side of the screen. The clock restarts while the player stands on it and again on leaving it. The door's local clock runs at 1/dilation(mass now), checked every physics step. Reaching the threshold (or the doorway) while it is open latches it open for good, so it never shuts on a player. The doorway and pocket are a BoulderBlocker, so a boulder is never lost behind a shut door. Visuals: a faint field ring, mass pips (lit per boulder in the field), and a timer bar while open.
- Boulder and mitt (`Boulder`, `MassMitt`): boulders sit on kinematic bodies (nothing pushes them). A grabbed boulder keeps its offset, circle-casts each move, stops at walls, doors, other boulders, NPCs, enemies and blockers, and stays within its home screen clear of the edge band. The player and a held boulder ignore each other, and that collision returns only once they are apart, so a boulder never shoves the player. Grab with Interact or a mitt swing, with the mitt equipped. Release on a second press, unequipping, getting stuck more than 1.0 behind, or moving away more than 2.6 (a screen crossing). HUD line above the isotope's.
- Plan (`PhysicsPlan`, stream 13; population stream 14): required counts are seeded 1..MaxRequired (the `PhysicsFace.MaxRequired` knob, default 3), plus 0–1 spare per door. On screens too cramped for the rolled counts, the counts come down (spares first, never below 1). With modules, it lays out each door's switch (fixed candidate order), the trial (first screen in a seeded order where it fits without costing doors boulders), and boulders. Each screen's layout uses its own derived seed. Boulders start off both lanes, inside the edge band, out of walls, outside every field, clear of other faces' interactables (Biology buttons, flowers and trial pieces; Chemistry plates and trial pieces; open pickups; hidden-item and core slots; gap ≥ 1.05), apart, and reachable to their door by a grid search. `FindProblems`/`LayoutProblems` re-check all of it, plus the timing (impassable at 0 and at required − 1, passable at required), room for the boulders near each door off the run, and an off-home door. SeedSweep runs it as "physics:" problems (shared `SeedSweep.ModuleLookup`).
- Trial (`PhysicsTrial`, currency 25): two booths (small walled rooms beside the lane, each behind a timed door with its own switch), with fields more than 2 × radius apart. There are exactly TrialRequired = 2 boulders, so the same boulders must be moved from one booth to the other. Completion fires once when both booth doors are latched.
- Population: 3 aliens (index base 4000) with an Einstein line (`Dialogue.EinsteinLines`, incl. "It's all relative, darling!") and sparse true hints; alien 0 adds `Dialogue.MassMittLine` (the mechanics). Sir Isaac Newton cameo (townsfolk parts, `Dialogue.NewtonLines`, `AppleBonk` drops an apple on him every 4 s). Enemies: 2 Quantum Fleas (weak to the mitt) and 1 Static Cling (weak to the isotope, else the beak), quadrant-bound.
- Earlier test adapted: ItemSceneTests' plain-item walk test now removes the PhysicsFace hook first, since every built face now has face content. The plain-gate fallback is still exercised with the Physics item.
- Scene test note: beyond one real drag with the mitt, boulders are set beside each door directly (the drag itself is covered by the component tests). Every run moves the player at exactly base speed.

## Review Triage Log

Pass 1 (quick lens): high 0, medium 2, low 2, false 0, maybe-false 0. Two findings (the trial bypass and the sweep gap) share one root cause and are grouped.

| Verdict | Route | Finding | Evidence / action |
|---|---|---|---|
| medium | patch | Trial shared-boulder rule bypassed by door boulders on the trial screen, and the sweep doesn't report it | MassNear counts every boulder in the radius, and TryTrial doesn't exclude door screens. Booth doors now count only trial boulders, with a LayoutProblems rule and a test. |
| medium | patch | Door passability only exact at base speed, and 1.12 Speed upgrades break it | Open time is now scaled by base/effective speed at switch activation, with a test. |
| low | patch | One Interact press can both open or close a dialogue and grab or release a boulder | It missed the OpenedFrame/ClosedFrame guard NpcTalker uses. Added. |
| low | patch | A swing over two boulders ends on an arbitrary one | It toggled per receiver. Now one boulder per swing: release if holding, else grab the nearest. |

## Design Notes

Suggested maths: open time = base × (1 + k × mass), with base 1.0 s, k 0.6 per boulder (mass 1 each) and a field radius of 3. Make the switch-to-door distance need about 2.8 s at base player speed, so 2 boulders (2.2 s open) fail and 3 (2.8 s) pass. Tune so the difficulty parameter (required count 1 to 3) maps cleanly.

## Verification

**Commands:**
- Unity batch `-runTests -testPlatform EditMode` and `PlayMode` -- expected: result Passed, 0 failed.
- Unity batch `-quit -executeMethod CubeSceneBuilder.RunSeedSweep` -- expected: 50/50, no softlocks.
- WebGL build: skip during this session (low memory); the user builds later.
