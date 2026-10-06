---
title: 'Town face hub'
type: 'feature'
ticket: '7'
created: '2026-10-05'
status: 'built'
baseline_revision: 'b3a2de4c80fb1d19b4644a7bfcd03a4ece1c395c'
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
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/town-draft-and-friendship.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The Town face is four plain placeholder modules with nobody in it. The spec makes it the busy, mismatched, last unpartitioned place where every race lives together, and the start of every run. The Epic 2 town draft will need one NPC of each race standing there.

**Approach:** Dress the Town modules as mixed-style ruins, using decor pieces in every face's theme colours and shapes. Spawn one NPC of each face race (Finch, Mushroom, Alien, Shape, Dinosaur) plus a few Townsfolk at every run start, in deterministic clear spots on Town screens. Each NPC is talkable with sparse first-run hints. A small welcome sign or gossip line sets the tone. Exits stay the existing cube crossings.

## Boundaries & Constraints

**Always:** NPC spawn positions and parts come from the run seed and are identical for the same seed. Spawn spots never overlap colliders (use NpcFactory.IsClear) and never block the four exit lanes or the screen centre. Exactly one NPC per face race, plus 2 to 4 Townsfolk. Town NPCs are rebuilt on every run start (Rebuild) and reveal. Dialogue follows lore-and-tone.md: campy, never explicit, never "sort", and with the Partition named as the old split. Decor is visual only, or uses colliders that satisfy ScreenModule.KeepsExitsOpen.

**Never:** No talk-order draft or ally/enemy logic (Epic 2, CAP-14). No shop logic. No change to the shuffle, layout, item placement or the sweep's guarantees. Story 1.5's F3 debug spawn stays as it is.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Run start | New run, any seed | Player on the Town start screen; Town has one NPC of each of the 5 face races plus 2–4 Townsfolk | No error expected |
| Same seed | Two runs with seed 1234 | Identical Town NPC parts and positions | No error expected |
| Clear spots | Any seed 1–50 | No Town NPC overlaps a collider, an exit lane or the screen centre | Spawner falls back to another valid spot, never an invalid one |
| Talk | Player talks to each Town NPC | Each opens dialogue with race- and role-flavoured lines and a true sparse hint | No error expected |
| Exit | Player walks off any Town edge into an unsealed face | Lands on the shuffled cube (science revealed) | Sealed edges stay walled |
| Reroll | F5 | Old Town NPCs removed, new seed's Town NPCs spawned | No duplicates left behind |

</frozen-after-approval>

## Code Map

- `game/Assets/Editor/ModuleLibraryBuilder.cs` -- Town modules are built from `Designs[i]` with `MirrorFor(Theme.Town, i)` (around lines 112 to 117). Add mixed-style ruin decor here. Regenerate the library.
- `game/Assets/Scripts/Cube/Npcs/NpcFactory.cs` -- `ChooseParts(seed, race, index)`, `Spawn(spec, lines, position, bounds, ...)`, `IsClear(feet, margin)`, RNG stream 4. Race enum: Finch, Mushroom, Alien, Shape, Dinosaur, Townsfolk.
- `game/Assets/Scripts/Cube/Npcs/HintGenerator.cs` (`RunFacts.Of(world)` is valid before the reveal), `Dialogue.cs`.
- `game/Assets/Scripts/Cube/CubeDebug.cs` -- the F3 ring spawn and its clear-spot search (a pattern to reuse). Its streams: 6 for the F7 enemy. Town spawns need their own new stream (7).
- `game/Assets/Scripts/Cube/CubeWorld.cs` -- `Rebuild`, `Rebuilt`, `ScreenCenter`, `FaceOrigin`, `ModuleAt`.
- `game/Assets/Scripts/Cube/Modules/ScreenModule.cs` -- `KeepsExitsOpen`, `ExitLaneHalfWidth` 1.25, `EdgeClearance` 1.5.

## Tasks & Acceptance

**Execution:**
- [x] `ModuleLibraryBuilder.cs` -- Town modules get ruin decor (broken pillars and arches tinted in each face theme's colour, with mismatched shapes). Any decor with a collider must pass KeepsExitsOpen. Regenerate in batch mode.
- [x] `game/Assets/Scripts/Cube/Town/TownPopulation.cs` -- on CubeWorld rebuild, compute deterministic spawn spots per Town screen (own RNG stream), validated clear and off the lanes and centre. Spawn the 5 face-race NPCs and 2–4 Townsfolk with sparse hints. Clear them on the next rebuild. Expose the spawned list (for Epic 2's draft).
- [x] `game/Assets/Scripts/Cube/Town/TownSign.cs` (or one Townsfolk greeter line) -- a campy welcome mentioning the Partition and that the town is "the last place everyone still mixes".
- [x] `game/Assets/Editor/CubeSceneBuilder.cs` -- add TownPopulation to the Cube scene and regenerate the scene in batch mode.
- [x] Tests: EditMode tests for spawn-spot determinism and validity across 50 seeds (pure spot computation against module geometry). PlayMode tests for run start (counts per race), talking to each, F5 replacing the population, and walking off a Town edge into the cube.

**Acceptance Criteria:**
- Given the EditMode and PlayMode suites and the seed sweep in batch mode, when run, then all pass, including earlier stories'.
- Given the Cube scene in Play mode, when the run starts, then the Town looks busier than other faces and has one NPC of each face race plus a few Townsfolk, all talkable.

## Implementation Notes

- Verified in batch mode after the review patches: EditMode 101/101, PlayMode 52/52, seed sweep 50/50. WebGL build PENDING (user). Visual check PENDING (user).
- Checkpoint 1 was approved under the user's standing instruction (2026-10-05) to keep building while they are away. Review the plan on return.
- Verified in batch mode: EditMode 101/101 (6 new TownTests), PlayMode 52/52 (5 new TownSceneTests), seed sweep 50/50. WebGL build PENDING (user). Visual check of the Town ruins in Play mode PENDING (user).
- Pure spot logic lives in `Scripts/Cube/Town/TownPlan.cs` (stream 7): candidates on a 0.25 grid per Town screen, valid when the NPC footprint plus 0.15 margin passes `KeepsExitsOpen` and misses the module's colliders and alcove footprints; the greeter goes on the start screen, the rest round-robin over the other screens in a seeded order. At runtime `NpcFactory.IsClear` can veto a pick (the next candidate is tried); it never fires in the tests, so runtime spots equal the pure ones.
- Each Town NPC moves only inside its own quadrant (`TownPlan.QuadrantBounds`), so wander/flee/hostile movement never enters an exit lane or the centre.
- After a rebuild the old NPCs are deactivated and destroyed at once; the new population spawns on the next frame so colliders the rebuild destroyed are gone before `IsClear` runs. The first population happens in `Start`.
- On reveal the Town population is despawned and respawned at once (same seed: same specs and spots) with post-reveal lines; Epic 2 must not rely on Town NPC object identity across the reveal. The greeter is identified by its spec (`TownPlan.IsGreeter`: Townsfolk, index base), and `TownPopulation.Specs` keeps the spec of each spawned NPC, so a skipped spot never shifts the welcome.
- Review fixes: `NpcMover.BoundedVelocity` now steers a body that was pushed outside its bounds straight back in (at least `ReturnSpeed`), shared with `EnemyBrain`; `RuinSize(Arch)` covers the drawn extent (1.55 high); `TownSpot.Equals` compares exact components.
- Townsfolk mix parts cheaply (Design Notes): head and torso from any race's set, legs from the Townsfolk set; built in `TownPlan.TownsfolkParts`, `NpcFactory` unchanged. Town NPCs use part index base 1000 so they never repeat F3 debug batches.
- The welcome is a greeter line (Townsfolk 0) from `TownSign.WelcomeLines`; there is no physical sign post.
- Ruin decor is visual only (no colliders), one piece per science face per Town module, shapes rotating per module; crowded modules get pieces scaled to 0.8 or 0.6. Town pillars are tinted in mixed science colours. Science prefabs were regenerated byte-identically in content; their fileID churn was reverted.

## Review Triage Log

Pass 1 (quick lens): high 0, medium 2, low 4, false 0, maybe-false 0. Plus 1 pending HITL item.

| Verdict | Route | Finding | Evidence / action |
|---|---|---|---|
| medium | patch | Town NPCs only refreshed on reveal, but the frozen intent says "rebuilt" | The literal reading is cheap and deterministic (same specs and spots). They are now respawned on reveal. Epic 2's draft records its results in RaceRelations, not on the NPC objects. |
| medium | patch | Greeter picked by list position | A skipped spot shifts the welcome lines and breaks the spec mapping. It now identifies the greeter by its spec. |
| low | patch | A pushed NPC can stay in an exit lane | BoundedVelocity clamped against the current position. It now steers back inside. |
| low | patch | Arch lintel overshoots its placement box by about 0.04 | Cosmetic. The size now covers the drawn extent. |
| low | patch | TownSpot Equals and GetHashCode are inconsistent | A direct correction. |
| low | reject | Rebuilds missed while disabled, or World set after OnEnable | Not reachable with the scene wiring, and the fix would add guards. |
| n/a | pending HITL | AC 2 "Town looks busier" by eye | On the user's list. |

## Design Notes

The Townsfolk flavour gag: Townsfolk are already mixed, so their heads and torsos can come from any race's part set, the "mismatched" look the spec asks for. If NpcFactory can't mix race part sets cheaply, keep Townsfolk as their own set and note it.

## Verification

**Commands:**
- Unity batch `-runTests -testPlatform EditMode` and `PlayMode` -- expected: result Passed, 0 failed.
- Unity batch `-quit -executeMethod CubeSceneBuilder.RunSeedSweep` -- expected: still 50/50.
- WebGL build: skip during this session (low memory); the user builds later.
