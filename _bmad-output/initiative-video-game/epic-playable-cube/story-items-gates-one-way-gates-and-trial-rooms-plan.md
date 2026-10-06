---
title: 'Items, gates, one-way gates and trial rooms'
type: 'feature'
ticket: '4'
created: '2026-10-05'
status: 'built'
baseline_revision: '2823f40012fd578725dc8941bc4ef2a8fb2f63c6'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/stack.md'
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/faces-and-races.md'
  - '{project-root}/_bmad-output/initiative-video-game/epic-playable-cube/story-screen-modules-and-the-town-exit-shuffle-plan.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Screens have gate slots but nothing that uses them, the player cannot pick anything up, and there is no reusable "commitment closes doors" mechanic. Each face story (1.8 to 1.10) needs a shared item and gate framework to plug its signature item into, plus a trial-room completion event that pays out later (1.12).

**Approach:** Add an inventory and item pickups. Items are data (an item definition per built theme, with placeholders until each face story replaces its own). Gates are data bound to the item that opens them, and a seeded placer puts them into module gate slots, mostly on the item's home face with at least one elsewhere. Each gate guards a small alcove. Add a one-way gate component, a trial-room completion event carrying a currency amount, and a softlock check in the seed sweep that simulates collecting items.

## Boundaries & Constraints

**Always:** Placement is deterministic from the run seed: use a separate SeededRng stream, and the same seed gives the same item and gate placement. Gates are data. A gate kind maps to exactly one item, and adding a face's real item later must not need placer changes. Each built theme's item has at least one gate on its home face and at least one on another built face. Item pickups sit on their home face. Modules keep all four side exits open, so gates only close alcoves, never screen connectivity. The sweep must fail any seed where a required item can only be reached through a gate needing that item, or through a cycle. Reuse CubeWorld, CubeLayout, CubeNavigator, SeedSweep and the module builder from 1.3.

**Never:** No real face mechanics (beaks, decay, boulders): those are 1.8 to 1.10. No combat or enemies (1.6), and no meta currency or shop (1.12); the trial event only carries an amount. Do not place one-way gates on the cube in this story: the component and its tests only.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Pickup | Player touches an item pickup | Item added to inventory, pickup removed, event raised | Picking up an owned item again has no effect |
| Gate closed | Player without the item touches its gate | Gate stays solid | No error expected |
| Gate opens | Player holding the item touches its gate | Gate opens and the alcove behind it is reachable | No error expected |
| Off-home gate | Any seed | Every built theme's item has ≥1 gate on its home face and ≥1 on another built face | Sweep reports the seed |
| One-way forward | Player crosses a one-way gate in its allowed direction | Crossing succeeds | No error expected |
| One-way back | Player pushes against it from the far side | Blocked | No error expected |
| Trial complete | A trial room's completion condition is met | Completion event fires once with its currency amount | A second trigger does not fire again |
| Softlock | Placement where a required item is only behind its own gate, or behind a cycle | Sweep flags the seed | Sweep names the seed and the item |
| Determinism | Same seed twice | Identical item and gate placement | No error expected |

</frozen-after-approval>

## Code Map

- `game/Assets/Scripts/Cube/Modules/GateSlot.cs`, `HiddenItemSlot.cs`, `ScreenModule.cs` -- slot markers. A module has 1–2 gate slots. `ScreenModule.KeepsExitsOpen` defines the clear edge band (1.5) and centre lanes (half-width 1.25).
- `game/Assets/Editor/ModuleLibraryBuilder.cs` -- generates the module prefabs (`Child("Gate i", ...)` adds a GateSlot at about line 185). Extend it to build an alcove (three wall pieces) around each gate slot, with the open side facing the screen centre, and regenerate the library.
- `game/Assets/Scripts/Cube/Modules/CubeLayout.cs` -- pure, seeded layout (`SeededRng(seed, stream: 2)`, `FaceLayout.ModuleAt`). Item and gate placement is a new pure step alongside it, on a new stream.
- `game/Assets/Scripts/Cube/CubeWorld.cs` -- `RevealScience()`, `ModuleAt`, `AllModules`, `ScreenCenter`. Gates and pickups are instantiated when the science faces are revealed.
- `game/Assets/Scripts/Cube/SeedSweep.cs` -- reachability and core-entrance checks. Add the item-collection simulation.
- `game/Assets/Scripts/Tracer/PlayerMover.cs` -- the player (Rigidbody2D, CircleCollider2D radius 0.4).

## Tasks & Acceptance

**Execution:**
- [x] `game/Assets/Scripts/Cube/Items/ItemDefinition.cs` (ScriptableObject: id, display name, home theme, placeholder colour) and `ItemCatalog.cs` (one item per built theme) -- items as data. The editor builder creates placeholder items for Biology, Chemistry and Physics.
- [x] `game/Assets/Scripts/Cube/Items/Inventory.cs` (on the player: `Has`, `Add`, `ItemAdded` event) and `ItemPickup.cs` (trigger, adds once).
- [x] `game/Assets/Scripts/Cube/Items/Gate.cs` -- holds its required ItemDefinition. Solid until a player with the item touches it, then opens (collider off, visual changes).
- [x] `game/Assets/Scripts/Cube/Items/ItemPlacement.cs` -- pure and seeded. It picks each item's pickup screen on its home face, and gate slots per item (≥1 home, ≥1 on another built face). Some pickups may sit inside another item's gated alcove to create an order, but never a softlock. Testable without scenes.
- [x] `game/Assets/Scripts/Cube/CubeWorld.cs` -- on reveal, instantiate pickups and gates from ItemPlacement. Unused gate slots get no gate, and their alcove stays open.
- [x] `game/Assets/Scripts/Cube/Items/OneWayGate.cs` -- passable from one side, blocking from the other.
- [x] `game/Assets/Scripts/Cube/Items/TrialRoom.cs` -- `Completed(int currency)` event, fired at most once. It is a base for face trials, with a placeholder completion trigger for tests.
- [x] `game/Assets/Scripts/Cube/SeedSweep.cs` -- simulate collection: start with no items, repeatedly collect reachable pickups whose guarding gate (if any) is open given the items held. Fail if any item stays uncollected. Report the seed and item.
- [x] Editor: ModuleLibraryBuilder adds alcoves, a new builder creates the item assets, and CubeSceneBuilder wires the catalog. Regenerate the library and the Cube scene in batch mode.
- [x] Tests: EditMode tests for placement (determinism, home and off-home gate counts, pickups on their home face, the sweep over 50 seeds, and a hand-made softlocked placement the sweep must flag). PlayMode tests for pickup, closed and open gates, one-way gate both directions, and the trial event firing once.

**Acceptance Criteria:**
- Given the EditMode and PlayMode suites and Cube > Seed Sweep in batch mode, when run, then all pass and the sweep reports 50/50 seeds with no softlocks.
- Given the Cube scene, when the player picks up an item and walks to one of its gates on another face, then the gate opens.

## Implementation Notes

- Verified in batch mode after the review patches: EditMode 73/73, PlayMode 19/19, seed sweep 50/50 with no softlocks. WebGL build PENDING (user).
- Checkpoint 1 was approved under the user's standing instruction (2026-10-05) to keep building while they are away. Review the plan on return.
- Verified in batch mode: Build Module Library, Create Cube Scene (also builds Assets/Items: three placeholder items + ItemCatalog, wired into CubeWorld; the player gets an Inventory), EditMode 71/71, PlayMode 19/19, seed sweep "50/50 seeds fully reachable with a reachable core entrance on every built face, every item collectable with no softlocks". WebGL build skipped per Verification.
- Alcoves: every gate slot was moved out of the centre lanes into a quadrant pocket (inner 2.0 wide x 1.75 deep, 0.2 walls, from |y| 1.3 to 3.45, open toward the horizontal lane). The A Pillars upper-right pillar moved from x 4 to 2.6 to make room. The back wall stops at 3.45, not 3.5, because the serialized wall rect overshot KeepsExitsOpen by float error. GateSlot now carries Opening and PocketOffset; Town modules get alcoves too but never gates.
- Placement (`ItemPlacement`, stream 3): items are keyed by home theme. Per item: 1 home + 1 off-home gate first, then 1 extra home gate when a slot is free (HomeGatesPerItem 2). Pickups are taken in shuffled order; each goes behind another item's gate on its home face with probability 1/2 when that keeps the graph acyclic, otherwise in the open at screen centre + (0, -2.5) (vertical lane). Gates go only on science faces.
- The sweep runs items only when the catalog implements IGateSlotCatalog (ModuleLibrary does), so the 1.3 fake-catalog tests are unchanged. It also checks gate counts, distinct slots and pickups on their home face.
- OneWayGate uses PlatformEffector2D, with the gate rotated so local up is the allowed direction. A reroll (Rebuilt) clears the player's Inventory.
- Not committed: code, regenerated prefabs, items and scene are on disk for review.

## Review Triage Log

Pass 1 (quick lens): high 0, medium 2, low 3, false 0, maybe-false 0.

| Verdict | Route | Finding | Evidence / action |
|---|---|---|---|
| medium | patch | Sweep and runtime build placement from different item lists | The item set drives the RNG draws, so a catalog change could put an unchecked placement in front of players. Both now use one source, with a test that they match. |
| medium | patch | ItemCatalogBuilder rebuilds the list from the placeholder paths | This would silently undo a face story's real item. It now fills only missing themes. |
| low | patch | Sweep labels non-cycle stuck pickups as "(cycle)" | Misreported cause. It now distinguishes own gate, stuck guard and cycle. |
| low | reject | Placement throws when a face runs out of gate slots, leaving a half-revealed state | Not reachable with today's library (≥4 slots per face, 6 required gates). The sweep calls the same Generate, so it would fail first. |
| low | reject | Acceptance test teleports instead of walking to the gate | Walkability is guaranteed by KeepsExitsOpen and the pocket-geometry tests (pockets open toward the clear centre lanes). A cross-face walking test would add a lot for little. |

## Design Notes

Alcove geometry: a gate slot becomes a 2×2 pocket walled on three sides, with the gate on the fourth side facing the screen interior. The guarded point is the pocket centre. A hidden item or pickup assigned "behind" a gate goes there. Alcoves must still satisfy KeepsExitsOpen.

Item ordering stays simple at this size: with 3 items, a pickup is placed behind another item's gate only when the dependency graph stays acyclic. The sweep is the safety net either way.

## Verification

**Commands:**
- Unity batch `-runTests -testPlatform EditMode` and `PlayMode` -- expected: result Passed, 0 failed.
- Unity batch `-quit -executeMethod CubeSceneBuilder.RunSeedSweep` -- expected: 50/50 seeds, no softlocks.
- WebGL build: skip during this session (low memory); the user builds later.
