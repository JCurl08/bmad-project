---
title: 'Earth and Atmosphere face'
type: 'feature'
ticket: '20'
created: '2026-10-06'
status: 'built'
baseline_revision: 'd74788fae9fac282f046c2ce6fb43c7ac0644cbc'
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
  - '{project-root}/_bmad-output/initiative-video-game/epic-playable-cube/story-math-face-plan.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Earth & Atmosphere is the last sealed face. The course correction requires all 5 science faces built at 3×3 (CAP-8).

**Approach:** Unseal Earth & Atmosphere and build it at 3×3 through `IFaceContent`, following the Math and Chemistry pattern.
- **Weather hazards** on the face's screens:
  - wind bands push the player
  - snow patches make movement slide (low friction or momentum)
  - freezing-rain patches slow the player
  - quake crevices must be crossed or avoided
  - the centre screen is a calm "eye" with no hazards
- **Signature item:** a weather wand\*. Each run rolls one phenomenon (quake, rain, snow or tornado), which is a variant like the beak and the stones, kept for the run and used by swinging. Each phenomenon opens its own gate (user decision):
  - quake knocks down rock pillars
  - rain douses a wall of fire
  - snow freezes water so you can walk across
  - tornado blows away a debris pile

  The other phenomena's gates are optional extras. The rolled phenomenon opens at least one gate on another face.
- **Also:** a harder trial built around the rolled phenomenon, enemies weak to items, and terrestrial and avian dinosaur NPCs.

## Boundaries & Constraints

**Always:**
- **Reuse:** the IFaceContent pattern, FaceContent and FaceSpots (with spot filters), the variant system (`ItemCatalog` variants, `ItemVariants`, `ItemPlacement` optional gates round-robin, kind by item id, never by catalog order), the alcove gate framework, `IAttackReceiver`, the enemy base, the NPC factory and dinosaur parts, and the art catalog.
- **RNG streams:** new streams come from `RngStreams` (19 Earth plan, 20 Earth population).
- **Sweep rules (50 seeds):**
  - required content is reachable, and no hazard makes a required path impassable (hazards slow or deflect but never fully block an exit lane)
  - the rolled phenomenon opens ≥1 off-face gate
  - optional gates guard nothing required
  - the trial fits
- **Hazard rules:**
  - Hazards affect movement only through a single modifier hook on the player mover, so speed upgrades and the Physics door speed scaling still compose correctly.
  - Hazards never apply in the core arena or in Town.
  - The calm-eye screen is the face's centre cell (1,1) at 3×3.
- **Difficulty:** hazard strength, scaled per screen count, is the difficulty parameter.
- **Physics compatibility:** Physics `OtherFaceMarks` and other faces' spot filters account for Earth's off-face gates.
- **Text:** follows the subtle-science and `*` rules. Never use "weather", "atmosphere", "meteorology", "seismic", "geology", "tectonic", "climate" or "Richter". Example placeholder names: "Stormcaller\*" for the wand, "the Old Digger\*" and "the Sky-Watcher\*" for the patrons, "the Wildlands\*" for the region (also used in generated hints).
- **Determinism:** signatures are re-recorded.
- **Boss skill check:** stays green. It enters from a controlled state since 1.19.

**Never:**
- No map (1.21).
- No global colour or text pass (1.22). The region colour stays the current theme colour; Earth becomes orange in 1.22.
- No wand upgrades or multiple phenomena (Epic 2).
- No hazards that can trap the player or push them through walls.
- No change to other faces' behaviour beyond what unsealing forces on placement.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Unsealed | Any seed | All 6 faces are built; nothing is sealed | No error expected |
| Roll | Seeds 1–50 | Each phenomenon rolled at least once; same seed, same phenomenon | No error expected |
| Quake | Swing the quake wand at a rock-pillar gate | Pillars fall, the gate opens | Other phenomena or items: no effect |
| Rain | Swing the rain wand at a fire wall | Fire is doused, the gate opens | Other: no effect |
| Snow | Swing the snow wand at a water gate | Water freezes into a walkable crossing, the gate opens | Other: no effect |
| Tornado | Swing the tornado wand at a debris pile | Debris blows away, the gate opens | Other: no effect |
| Optional | Run with any phenomenon | Other phenomena's gates exist and guard nothing required | Sweep flags violations |
| Wind | Player stands in a wind band | Pushed in the band's direction at bounded speed | Never through walls, never fully blocks a lane |
| Snow patch | Player moves on snow | Slides (momentum) | Can always leave the patch |
| Freezing rain | Player in freezing rain | Slower | Never below a minimum speed |
| Crevice | Quake crevice on a screen | Impassable strip with a guaranteed way around or across | Sweep verifies the exits stay connected |
| Calm eye | Centre screen (1,1) | No hazards | No error expected |
| Trial | Solve the trial with the rolled phenomenon | Completion fires once | Repeats don't fire |
| Enemies | Earth enemy hit with its weakness vs other | Markedly more damage | No error expected |
| Dinosaurs | Earth face revealed | Terrestrial and avian dinosaur NPCs, talkable, with subtle-science lines and true hints | No error expected |
| Text | All Earth strings, including generated hints | No banned words; placeholders carry `*` | Test fails otherwise |

</frozen-after-approval>

## Code Map

- `game/Assets/Scripts/Cube/CubeModel.cs` `IsBuilt`: add `Theme.EarthAtmosphere`. Afterwards nothing is sealed; keep the sealed-face code paths working for future use. Tests that assumed a sealed face need updating (the 1.19 sealed-wall walk test picks a seed where Earth borders Town).
- `game/Assets/Scripts/Cube/Faces/Math/*`, `Faces/Chemistry/*` (1.18 variant stones), and `Faces/Biology/*` (beak variants) -- patterns to mirror. `Faces/FaceContent.cs` and `FaceSpots.cs` (spot filters).
- `game/Assets/Scripts/Tracer/PlayerMover.cs` -- add a single movement-modifier hook (speed multiplier, plus an external push and slide/inertia), used by hazards. `IMoveSpeedScale` already composes speed upgrades. `Faces/Physics/TimeField.cs` and `DoorSwitch` read player speed for timed doors, so hazards must not break that maths (the player isn't in Earth hazards while on Physics).
- `game/Assets/Scripts/Cube/Items/ItemCatalog.cs`, `ItemVariants.cs`, `ItemPlacement.cs`, `Editor/ItemCatalogBuilder.cs` (Earth variants: Stormcaller\* quake/rain/snow/tornado, base entry renamed in place with its id kept).
- `game/Assets/Scripts/Cube/Faces/Physics/PhysicsPlan.Layout.cs` `OtherFaceMarks` (account for Earth gates), plus `SeedSweep.cs`, `RngStreams.cs`, `Npcs/HintGenerator.cs` (`ThemeName(EarthAtmosphere)` becomes "Wildlands\*"), `Npcs/Dialogue.cs`, `Art/NpcPartArt.cs` (dinosaur parts exist), `Art/ArtCatalog.cs`, `Editor/ArtCatalogBuilder.cs`, `ART-MAP.md` (keys for pillars, fire wall, water, debris, wind, snow, rain, crevice, wand variants and Earth enemies; reuse Kenney tiles where they fit), `Editor/CubeSceneBuilder.cs` (EarthFace).
- Tests: `EarthTests` (EditMode), plus `EarthComponentTests` and `EarthSceneTests` (PlayMode). Update the sealed-face tests and determinism.

## Tasks & Acceptance

**Execution:**
- [x] Unseal Earth and fix the sealed-face assumptions in tests and the sweep.
- [x] Variants: a 4-phenomenon wand (kind by id), its art keys and catalog entries.
- [x] Gates: RockPillarGate (quake), FireWallGate (rain), FrozenWaterGate (snow), DebrisGate (tornado), each attack-to-open by its phenomenon.
- [x] Hazards: the movement-modifier hook on PlayerMover, then WindBand, SnowPatch, FreezingRain and QuakeCrevice placed by EarthPlan, with the calm eye at (1,1). Sweep rules for lane connectivity and no trapping.
- [x] EarthPlan (stream 19): gates, hazards, trial screen, difficulty, `FindProblems`. Wire it into the sweep and the Physics OtherFaceMarks.
- [x] EarthTrial around the rolled phenomenon, firing once. EarthEnemies (one weak to the wand, one to another face's item). EarthPopulation (stream 20) with terrestrial and avian dinosaurs and subtle-science lines.
- [x] EarthFace (IFaceContent), CubeSceneBuilder wiring, regenerated catalog, art and scene, determinism re-recorded.
- [x] Tests for every I/O row and the text rule.

**Acceptance Criteria:**
- Given the full suites and the 50-seed sweep, when run, then all pass, with the Earth rules included and all 6 faces built.
- Given the Cube scene, when the player reaches the Earth face, then hazards, the rolled wand's gates, the trial and the dinosaurs all work and read clearly.

## Implementation Notes

- Checkpoint 1 approved under the user's standing instruction (2026-10-06).
- Verified after the review patches: EditMode 226/226, seed sweep 50/50, PlayMode 138/138 run as six class batches (CoreSceneTests 13 + 125), because a single full run had been stopped by the host for low memory. User playtest PENDING.
- Built 2026-10-06 (not committed). Verified in batch mode: EditMode 224/224; seed sweep 50/50 with the Earth rules (Stormcallers* 10 quake / 10 rain / 20 snow / 10 tornado; 643 Earth gates, 50 off-home, 343 optional; hazards 103 wind / 97 snow / 97 freezing rain / 95 crevices; level 2 on every seed; the Earth trial fits on 50 seeds). Item catalog, art catalog and Cube scene regenerated; determinism re-recorded. PlayMode run PENDING (the batch run was stopped by the host for low memory before it finished). Playthrough by eye PENDING (user).
- **Unsealed:** `CubeModel.IsBuilt` includes Earth; nothing is sealed. The sealed path stays: `CubeModel(seed, n, sealedThemes)` and `CubeWorld.SealedThemes` seal a theme on purpose (the sealed-wall walk test and the sealed model tests use it).
- **Hook:** `PlayerMover` reads one `IMoveModifier` (speed multiplier, push as a fraction of speed, grip) and clamps it (speed >= 0.5x, push <= 0.5x the slowed speed, grip >= 0.03). `EffectiveSpeed` is unchanged, so speed upgrades and Physics door timing are untouched. `HazardReceiver` (on the player) is the only implementer; it returns None off the Earth face and in the core arena.
- **Plan (`EarthPlan`, stream 19):** required gates use the rolled phenomenon (kind by id); optional ones their own. Calm: the eye (N/2,N/2) and any Earth screen holding a Physics timed door. One hazard per other screen, kinds dealt round a shuffled order; level = 1 + (hazard screens - 1) / PerScreen(2, N), cap 3, sets strengths. Crevices: strips 3/2.4/1.8 x 0.6 off lanes and edge band, clear of walls, gate fronts, slots and trial pieces, and a flood fill rejects any strip that cuts a spot off (falls back to wind). Trial: 4 targets of the rolled kind, on a hazard screen.
- **Compat:** Physics `OtherFaceMarks` adds `EarthPlan.ContentMarks` (gate fronts, crevices, trial pieces); Math `OtherFaceMarks` adds `EarthPlan.GateFrontMarks` (layouts and Math's spot filter). Hints call Earth "Wildlands*"; item nickname "storm-calling stick"; dinosaur text fixed (no real name, no "weather").
- **Review fixes (2026-10-06):** Earth gate fronts for Physics come from the placement (optional gates included, kinds or not); Biology, Chemistry, Physics and Math spawns use `FaceContent.ClearOfSlots` (every gate front, hidden-item and core slot); trial spots keep clear of gate fronts, slots, buttons, plates and flowers (re-checked in `Problems`); crevice floods count trial pieces as solid; a reset trial piece re-solidifies only once nothing stands in it; an unknown phenomenon gets `Dialogue.NeutralWandLine`; `PlayerMover` caches the modifier lookup (`RefreshModifier` after adding one). Sweep 50/50, determinism re-recorded, EditMode 226/226 (EarthTests 12/12 after a threshold fix), PlayMode Earth 14/14 and Math/Biology/Chemistry/Physics/Cube/Item scene tests 22/22. Full PlayMode not run here (memory).

## Review Triage Log

Pass 1 (quick lens): high 0, medium 5, low 3, false 0, maybe-false 0. Plus 1 pending item (PlayMode run stopped by the host for low memory).

| Verdict | Route | Finding | Evidence / action |
|---|---|---|---|
| medium | patch | Physics content can sit in front of optional Earth gates (live on seeds 37 and 44) | OtherFaceMarks built EarthPlan with null kinds, dropping optional gates. Now uses GateFrontMarks, and the test is extended to 50 seeds. |
| medium | patch | Other faces' spawns ignore Earth off-face gate fronts (frozen constraint) | Only Math had a filter. Biology, Chemistry and Physics (and Town) now filter gate fronts and slots. |
| medium | patch | Earth trial pieces ignore gate fronts and slots | Solid pieces could block an alcove or portal. TrialSpotsOn is now filtered and rechecked. |
| medium | patch | Crevice flood ignores solid trial pieces | The 0.65 gap is smaller than the 0.8 player. Pieces are now in the flood solids, with a test. |
| medium | patch | Trial reset re-solidifies pieces inside the player | Colliders are now deferred until the player is clear, with a test. |
| low | patch | Wand None maps to the quake line | Now a neutral line. |
| low | patch | Text test covers only dinosaur hints | Now covers all Earth-mentioning generated hints. |
| low | patch | Uncached GetComponent<IMoveModifier> | Cached. |
| n/a | pending | Full PlayMode not yet run (host stopped it for low memory) | Re-run with the user's permission before commit. |

## Verification

**Commands:**
- Unity batch `-runTests -testPlatform EditMode` and `-testPlatform PlayMode -nographics` -- expected: Passed, 0 failed.
- Unity batch `-quit -executeMethod CubeSceneBuilder.RunSeedSweep` -- expected: 50/50.
