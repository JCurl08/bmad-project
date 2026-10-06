---
title: 'Lego NPCs and dialogue'
type: 'feature'
ticket: '5'
created: '2026-10-05'
status: 'built'
baseline_revision: '47ca0b008d80646414eb26ec6fb03a954a2bdc91'
route: 'full'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/faces-and-races.md'
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/lore-and-tone.md'
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/stack.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The game has no characters. The town (1.7), each face (1.8 to 1.10) and the later town draft (Epic 2) all need NPCs whose parts are swapped by the seed, who talk in a campy voice, and whose whole race can turn hostile.

**Approach:** Build an NPC from three seeded part slots per race: the head decides the hint it gives, the torso its role (including merchant) and the legs how it moves. Add a dialogue system with campy lines and a sparse hint set for the first run, plus a per-race hostility flag that silences that race's NPCs (enemy behaviour comes in 1.6). Provide a factory and a debug spawn; placement in the world belongs to the town and face stories.

## Boundaries & Constraints

**Always:** All six races get part sets with placeholder art drawn in code: Finch (Biology), Mushroom (Chemistry), Alien (Physics), Shape (Math), Dinosaur (Earth & Atmosphere, terrestrial and avian variants), and a Townsfolk/mixed set. Parts come from the run seed through SeededRng on a new stream, so the same seed gives the same NPCs. Hints are generated from the real run data (CubeModel, CubeLayout and ItemPlacement), so they are true. Dialogue follows lore-and-tone.md: campy and never explicit, and it never uses the word "sort". Talking uses the project-wide `Player/Interact` action. Roles include at least merchant, quest-giver, smith and gossip.

**Never:** No combat or enemy behaviour for hostile NPCs (1.6). No town hub or face placement (1.7 to 1.10). No shop logic behind the merchant role (1.12 and later); the role only changes their lines. No imported art.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Same seed | Two NPC sets from seed 1234 | Identical parts per race slot | No error expected |
| Seed variety | Seeds 1 to 20 | At least two different part combinations for some race | No error expected |
| Head → hint | Two NPCs that differ only in head | They give different hints, each true for the run | No error expected |
| Torso → role | Two NPCs that differ only in torso | Different role line (e.g. merchant pitch vs gossip) | No error expected |
| Legs → movement | Stand / wander / flee legs | Stand stays put, wander moves within its screen, flee moves away when the player is near | No error expected |
| Talk | Player in range presses Interact | Dialogue box opens with that NPC's lines; Interact advances; it closes after the last line | Interact out of range does nothing |
| Hostile race | Hostility flag set for the NPC's race | NPC refuses to talk (no dialogue opens) | Clearing the flag restores talking |
| Sparse first run | Hint density set to sparse | Fewer, vaguer hints than full density | No error expected |

</frozen-after-approval>

## Code Map

- `game/Assets/Scripts/Cube/CubeModel.cs`, `Modules/CubeLayout.cs`, `Items/ItemPlacement.cs` -- run facts that hints are generated from (theme per face, core-entrance cell per face, item pickup screens, gate locations).
- `game/Assets/Scripts/Cube/SeededRng.cs` -- streams 2 (layout) and 3 (items) are taken. Use a new stream for NPC parts.
- `game/Assets/Scripts/Cube/CubeWorld.cs` -- `Model`, `Layout`, `ItemPlacement`, `ScreenCenter`, and the `Rebuilt`/`Revealed` events.
- `game/Assets/Scripts/Cube/CubeDebug.cs` -- F1 overlay, F2 slots, F5 reroll, F6 jump. F3 is free for a debug NPC spawn.
- `game/Assets/Settings/InputSystem_Actions.inputactions` -- has `Player/Interact`.
- `game/Assets/Scripts/Tracer/PlayerMover.cs` -- pattern for using the project-wide actions with OnEnable/OnDisable.

## Tasks & Acceptance

**Execution:**
- [x] `game/Assets/Scripts/Cube/Npcs/` -- `Race` enum, `NpcPart` data (slot, id, colour/shape for placeholder drawing, and a hint kind, role or movement depending on the slot), and `RacePartSets` (at least 3 heads, 3 torsos and 3 legs per race, defined in code or as ScriptableObjects made by an editor builder).
- [x] `NpcFactory.cs` -- pure part choice from (seed, race, index), plus `Spawn(...)` that builds a GameObject with three stacked placeholder sprites, a collider and the behaviours.
- [x] `HintGenerator.cs` -- pure. It turns run facts into hint lines by kind (item location, gate location, core-entrance face, a face's theme), at full or sparse density. Sparse names only the face or theme, not the screen.
- [x] `Dialogue.cs` (lines plus the campy role and race flavour lines) and `DialogueBox.cs` (simple on-screen box, Interact to advance and close).
- [x] `NpcTalker.cs` (talk when the player is in range and presses Interact, refused while its race is hostile) and `NpcMover.cs` (stand, wander inside its screen bounds, flee from the player).
- [x] `RaceRelations.cs` -- a per-run hostility flag per race, with a `Changed` event.
- [x] `CubeDebug.cs` -- F3 spawns one NPC of each race around the player. F4 toggles hostility for the race of the nearest NPC.
- [x] Tests: EditMode tests for determinism, variety, head → hint (truth checked against the run data), torso → role lines, sparse versus full, and that no line contains "sort". PlayMode tests for talk open/advance/close, refusal while hostile, and each movement type.

**Acceptance Criteria:**
- Given the EditMode and PlayMode suites and the seed sweep in batch mode, when run, then all pass, including earlier stories' tests.
- Given the Cube scene, when F3 is pressed and the player talks to each NPC, then each gives race- and role-flavoured campy lines and a true hint.

## Implementation Notes

- Verified in batch mode after the review patches: EditMode 84/84, PlayMode 34/34, seed sweep 50/50. WebGL build PENDING (user). The NPCs haven't been looked at in the running editor yet (user to-do).
- Checkpoint 1 was approved under the user's standing instruction (2026-10-05) to keep building while they are away. Review the plan on return.
- Verified in batch mode: EditMode 84/84 (11 new), PlayMode 28/28 (9 new), seed sweep 50/50 with no softlocks. WebGL build skipped per Verification.
- Parts are in code (`RacePartSets`): every race has 4 heads (one per HintKind), 4 torsos (one per role) and 3 legs (one per movement), so any two parts in a slot differ in what they control. Dinosaur parts are tagged terrestrial or avian; the NPC's variant comes from its head (then its torso) and picks its greeting.
- Part choice (`NpcFactory.ChooseParts`) seeds its own PCG32 from (seed, race, index) packed into 64 bits, on stream 4. The NPC's salt (one more draw) picks the hint subject and the line variants. `NpcMover` wander targets use stream 5.
- Hints come from `RunFacts` (model, layout, placement). `RunFacts.Of(world)` generates the layout and placement before the reveal the same way the reveal does; a scene test checks they match. Without a layout or placement (no library or catalog), item, gate and core hints fall back to face-theme hints. Full density gives 2 hints naming the screen in compass words (or the town edge for face hints); Sparse gives 1 that names only the theme. `HintGenerator.FirstRunDensity` is Sparse, and the F3 spawn uses it (`CubeDebug.HintDensity`).
- A conversation is: race greeting, role line (role template filled with race nouns), then hints. `DialogueBox` is one shared OnGUI box. A press in the frame it opened does not advance, and talkers ignore the frame it closed. The project-wide Interact action is enabled but never disabled by NPC code, because it is shared. `WasPressedThisFrame` ignores the action's Hold interaction.
- `RaceRelations` is owned by `CubeWorld.Relations` and reset on every rebuild. A hostile NPC refuses (raises `Refused`), is tinted red, and closes its open conversation. F3 spawns six NPCs in a ring inside the player's screen; F4 toggles the nearest NPC's race; a rebuild removes debug NPCs. The overlay lists hostile races.
- Not committed: code, tests and the new .meta files are on disk for review.

## Review Triage Log

Pass 1 (quick lens): high 0, medium 2, low 3, false 1, maybe-false 0.

| Verdict | Route | Finding | Evidence / action |
|---|---|---|---|
| medium | patch | NPCs pass through module walls and closed gates and can shove the player | Kinematic MovePosition gets no collision response against static colliders. Movement now respects colliders, with a collision-free F3 spawn and a test. |
| medium | patch | Dialogue box stays open after a rebuild, showing the old run's hints | ClearNpcs destroyed the NPCs without closing the shared box. The box now closes when its owner is disabled or destroyed. |
| low | patch | Stale hostile tint after re-enable | OnEnable didn't call ApplyTint. Fixed. |
| low | patch | An arbitrary in-range NPC answers | It went by script order. Now the nearest in-range talker answers. |
| low | patch | A hostile NPC fires a false Refused on another conversation's presses | Branch order. The open-box check now comes first. |
| false | reject | Townsfolk set is not "mixed" | In the plan, "Townsfolk/mixed" names the set. The town's mix comes from one NPC of every race (story 1.7), not from mixing parts. |

## Design Notes

Line flavour comes from a role line plus a race verbal tic. For example, a Mushroom merchant: "Spores for sale! …fine, *hints* for sale." A Shape gossip: "Between you and me, the triangles are unstable." Hints read as in-world gossip ("They say the beak-thing is somewhere on the Chemistry face"). Keep the tables small, with 3 to 4 lines per role and race.

## Verification

**Commands:**
- Unity batch `-runTests -testPlatform EditMode` and `PlayMode` -- expected: result Passed, 0 failed.
- Unity batch `-quit -executeMethod CubeSceneBuilder.RunSeedSweep` -- expected: still 50/50 with no softlocks.
- WebGL build: skip during this session (low memory); the user builds later.
