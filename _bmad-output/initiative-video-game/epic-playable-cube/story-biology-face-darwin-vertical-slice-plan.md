---
title: 'Biology face (Darwin), vertical slice'
type: 'feature'
ticket: '8'
created: '2026-10-05'
status: 'built'
baseline_revision: 'dc5e0f1485484326b8c993760f5537d11f05b697'
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
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/stack.md'
  - '{project-root}/_bmad-output/initiative-video-game/epic-playable-cube/story-items-gates-one-way-gates-and-trial-rooms-plan.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Every shared system exists (cube, modules, items and gates, NPCs, combat, town), but no face plays like its theme yet. The Biology face is the vertical slice that proves one complete face end to end. It also sets the pattern Chemistry and Physics will copy.

**Approach:** Each run rolls a beak adaptation (thin or thick) as the Biology item. The thin beak presses distant buttons and pollinates flowers that grow a vine bridge on an adjacent screen. The thick beak breaks pots and rocks. Only the rolled beak's gates are required; the other beak's gates appear as optional extras. Add a harder Biology trial puzzle combining beak mechanics that fires the 1.4 trial event, Biology enemy types each weak to an item, finch NPCs on the face with Darwin-flavoured dialogue, and a difficulty parameter on the beak gates.

## Boundaries & Constraints

**Always:** The beak roll comes from the run seed on its own stream: same seed, same beak. Both beaks are real ItemDefinitions with HomeTheme Biology, and the 1.4 placer still treats Biology as one logical item (the rolled beak). Gates of the non-rolled beak only guard optional content, never a required pickup, and the seed sweep must enforce this. Beak gates reuse the 1.4 alcove and gate framework: rocks and pots are broken by attacking with the thick beak equipped; buttons are pressed at range with the thin beak; a flower pollinated with the thin beak opens a linked vine gate on an adjacent screen of the same face. The rolled beak still opens at least one gate on another face (1.4 rule). The trial fires `TrialRoom.Completed(currency)` once. Finch NPCs reuse the 1.5 factory with sparse first-run hints. Tone follows lore-and-tone.md. Faces stay 2×2 (4 screens).

**Never:** No Chemistry or Physics content (1.9, 1.10). No currency payout or shop (1.12); the trial only fires its event. No new gate rules that break the 1.4 sweep guarantees. No imported art.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Beak roll | Same seed twice; seeds 1–50 | Same beak for the same seed; both beaks occur across seeds | No error expected |
| Thick beak | Attack a rock or pot gate with the thick beak equipped | It breaks and the alcove opens | Other items or a bare attack don't break it |
| Thin button | Attack a distant button with the thin beak equipped, from beyond normal reach | The linked door opens | Out of thin-beak range does nothing |
| Pollinate | Attack a flower with the thin beak | A vine bridge opens its linked gate on an adjacent Biology screen | Flower already used does nothing |
| Optional gates | Run with the thin beak | Thick-beak gates exist but guard nothing required | Sweep flags any seed where they do |
| Off-home | Any seed | The rolled beak opens ≥1 gate on another face | Sweep reports the seed |
| Trial | Player completes the Biology trial with the rolled beak | Completion event fires once with its currency amount | Repeats don't fire |
| Enemies | Biology enemy hit with its weakness item vs anything else | Markedly more damage from the weakness | No error expected |
| Finch NPCs | Biology face revealed | Finch NPCs present, talkable, Darwin-flavoured, true hints | No error expected |
| Full run | Town → leave town → Biology face | Get the beak, open its gates (a thin-beak seed includes a pollinated bridge), finish the trial | No error expected |

</frozen-after-approval>

## Code Map

- `game/Assets/Scripts/Cube/Items/ItemPlacement.cs` -- theme-keyed placement on stream 3: `GatePlacement(screen, slot, Theme item)`, `PickupPlacement`, `ForRun`, `RunItems`, `HomeGatesPerItem` = 2, `OffHomeGatesPerItem` = 1. Keep Biology as one logical item. Optional off-beak gates go in unused slots as a separate, clearly typed list.
- `game/Assets/Scripts/Cube/Items/Gate.cs` -- `RequiredItem`, `TryOpen(toucher)`, `IsOpen`, `Opened`. The new beak gate kinds build on it.
- `game/Assets/Scripts/Cube/Items/ItemCatalog.cs` / `ItemDefinition.cs` / `Editor/ItemCatalogBuilder.cs` -- the catalog keeps existing entries. Biology now has two variants, and the catalog has to express "variants of a theme" with a seeded roll.
- `game/Assets/Scripts/Cube/Items/TrialRoom.cs` -- `Completed(int currency)`, fired once.
- `game/Assets/Scripts/Cube/Combat/PlayerAttack.cs` -- `TryAttack`, `Reach`, `HitboxSize`, `EquippedItem`, `Swung`. The thin beak needs longer reach for buttons and flowers.
- `game/Assets/Scripts/Cube/Combat/Enemy.cs` -- `Spawn(...)`, `Weakness`, `WeaknessMultiplier`.
- `game/Assets/Scripts/Cube/Npcs/NpcFactory.cs`, `Dialogue.cs`, `HintGenerator.cs`; `Town/TownPopulation.cs` -- the pattern for face NPC population on reveal (clear spots, own RNG stream).
- `game/Assets/Scripts/Cube/SeedSweep.cs` -- must also enforce the optional-gate rule.
- Used RNG streams: 2 layout, 3 items, 4 NPC parts, 5 NPC wander, 6 F7 enemy, 7 town. New streams start at 8.

## Tasks & Acceptance

**Execution:**
- [x] Items: `Thin Beak` and `Thick Beak` ItemDefinitions (HomeTheme Biology). Catalog support for theme variants plus a seeded `RollVariant(seed, theme)` (new stream). The builder adds them without overwriting entries.
- [x] `game/Assets/Scripts/Cube/Faces/Biology/` -- `BreakableGate` (thick: rocks and pots), `ButtonGate` (thin, ranged press opens a linked door), `FlowerVineGate` (thin pollinates a flower; a linked vine gate on an adjacent Biology screen opens). Each has a difficulty parameter (the number of beak gates on the face).
- [x] Placement: the rolled beak is the Biology item for ItemPlacement. Add optional non-rolled-beak gates in leftover Biology slots (and the matching visuals). Extend SeedSweep so optional gates never guard required content.
- [x] `BiologyTrial.cs` -- on one seeded Biology screen, a harder puzzle combining the rolled beak's mechanics (e.g. break three rocks in order, or press two distant buttons then pollinate). On success it calls `TrialRoom.Complete`.
- [x] Biology enemies: 1–2 types (e.g. "Seed Weevil", weak to the thick or thin beak; "Pollen Puff", weak to another built theme's item), placed on Biology screens by seed with clear spots.
- [x] `BiologyPopulation.cs` -- finch NPCs on Biology screens after the reveal, with Darwin-flavoured lines added to the Dialogue tables ("Adapt or get out of the niche!").
- [x] Wire everything into CubeWorld's reveal and CubeSceneBuilder, and regenerate the assets and scene in batch mode.
- [x] Tests: EditMode tests for the roll, the optional-gate sweep rule, and placement determinism. PlayMode tests for each I/O row, including a thin-beak seed walk-through with the pollinated bridge and a thick-beak seed breaking a rock.

**Acceptance Criteria:**
- Given the EditMode and PlayMode suites and the seed sweep in batch mode, when run, then all pass, including earlier stories', and the sweep reports 50/50 with no softlocks.
- Given the Cube scene, when the player plays a run onto the Biology face, then it reads as Darwin's face: finch NPCs, a beak to find, beak-specific gates and a trial.

## Implementation Notes

- Verified in batch mode after the review patches: ItemCatalogBuilder run, EditMode 115/115, PlayMode 63/63, seed sweep 50/50 including the optional-gate rule. WebGL build PENDING (user). Playthrough by eye PENDING (user).
- Checkpoint 1 was approved under the user's standing instruction (2026-10-05) to keep building while they are away. Review the plan on return.
- Verified in batch mode: Build Item Catalog (3 items kept + 2 variants), Create Cube Scene, EditMode 113/113, PlayMode 63/63, seed sweep "50/50 ... every item collectable with no softlocks, optional gates guarding nothing required" (22 thin, 28 thick; all 50 seeds show optional beak gates). WebGL build skipped per Verification. Not committed.
- Variants: `ItemCatalog` keeps `items` (the Biology placeholder stays as the base entry) and adds a `variants` list (Thin Beak, Thick Beak in `BiologyBeaks.VariantOrder`). `ItemFor(theme, seed)` returns the rolled variant (`ItemVariants.Roll`, stream 8); `VariantCounts()` feeds `ItemPlacement.ForRun`, CubeWorld, RunFacts and the sweep. `ItemDefinition.AttackReach` (thin beak 2.5) drives `PlayerAttack.EffectiveReach`; a long reach stretches the hitbox from the player out to that reach, so close targets are still hit.
- Placement: Biology stays one logical item. Optional gates (`OptionalGatePlacement`, up to 2 per non-rolled variant) are drawn after the pickups at the end of stream 3, so the 1.4 required gates and pickups are unchanged (tested). The sweep checks they sit on the home face, in unused slots, never use the rolled variant and guard no pickup, and runs `BiologyPlan.FindProblems`.
- Face hook: `IFaceContent` (BeginReveal, CreateGate, EndReveal, Clear) on the CubeWorld object; `BiologyFace` implements it. `Gate` gained `OpensOnTouch`, `OpenNow`, `OnOpened` and `ClosedColor` for face gates; swings reach face objects through `IAttackReceiver`.
- `BiologyPlan` (stream 9): thick runs make every beak gate a rock or pot; thin runs make the first home Biology gate a flower-vine gate (flower on a seeded adjacent screen at local (0, 2.6), in the vertical lane) and the rest distant-button doors (button in the horizontal lane at (-sign x * 5, sign y * 0.6)). Difficulty = beak gates on the gate's face; hits to open = 1 + (difficulty - 1) / 3.
- Trial: pieces sit beside the centre lanes (|x| or |y| = 1.8), picked on stream 10 at least 4.4 apart (more than a thin-beak swing's diagonal plus two piece radii, so one swing never reaches two); EditMode test checks every seed's trial fits. Thick: rocks 1-2-3 in order, a wrong rock resets. Thin: two buttons, then the flower. Currency 25.
- Population (stream 10, `FaceSpots` over Town's candidate rules): 3 finches (NPC index base 2000, Darwin line + sparse true hints, finch 0 names the rolled beak), 2 Seed Weevils (weak to the rolled beak) and 1 Pollen Puff (weak to the Chemistry item), confined to their quadrant with sight 4.
- Beak kinds are read from the items' ids (`BiologyBeaks.VariantKinds`), never from catalog order; the builder also keeps the canonical [Thin, Thick] order, and the sweep accepts the kinds.
- Earlier tests adapted: ItemSceneTests (expected placement via ForRun with variant counts, optional gates in slots, the walk-to-gate test uses a plain item) and CombatSceneTests (F7's fallback weakness is the run's beak).

## Review Triage Log

Pass 1 (quick lens): high 0, medium 4, low 2, false 0, maybe-false 0.

| Verdict | Route | Finding | Evidence / action |
|---|---|---|---|
| medium | patch | Beak kind resolved by catalog index can drift from the rolled item | BiologyBeaks assumes [Thin, Thick]; the builder can produce [Thick, Thin]. Now resolved by item id, with a sweep and test check. |
| medium | patch | One thin-beak swing can hit two trial pieces | Hitbox is about 3.05 long; spacing is 2.2. Placement now guarantees one piece per swing, with a test. |
| medium | patch | The 1.4 swept==played parity test misses beak variants | The tests called Run/ForRun without variantCounts. They now pass the real catalog's counts. |
| medium | patch | The 1.4 acceptance test can Assert.Ignore depending on the seed | The test now searches for a qualifying seed and always runs. |
| low | patch | The finch true-hint test is circular | Expected lines came from the same LinesFor call. It now checks the hints against the run facts. |
| low | defer | Optional (non-rolled) beak gates guard empty alcoves | This matches the user's decision ("optional extras for later runs"). Optional rewards belong with 1.12's hidden meta-currency items and are logged in deferred-work.md. |

## Design Notes

Pattern for 1.9 and 1.10. Keep face content in `Faces/<Theme>/` with three parts: gate behaviours, the trial and population. Wire them through one `IFaceContent` hook that CubeWorld calls on reveal, so Chemistry and Physics plug in the same way.

Thin-beak reach: about 2.5 units versus the default 0.85, applied only when the thin beak is equipped.

## Verification

**Commands:**
- Unity batch `-runTests -testPlatform EditMode` and `PlayMode` -- expected: result Passed, 0 failed.
- Unity batch `-quit -executeMethod CubeSceneBuilder.RunSeedSweep` -- expected: 50/50, no softlocks.
- WebGL build: skip during this session (low memory); the user builds later.
