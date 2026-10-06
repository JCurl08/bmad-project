---
title: 'Readability pass'
type: 'feature'
ticket: '15'
created: '2026-10-06'
status: 'built'
baseline_revision: 'bbf43ad236a9b19f6f2446e1b418703b84c57951'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/lore-and-tone.md'
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/faces-and-races.md'
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/stack.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** On the user's first playtest, the game was "far too confusing". Every object is a code-drawn coloured square or circle, so the player, NPCs, enemies, items, gates and walls can't be told apart. There is little feedback when something happens. NPC dialogue is too long for efficient testing. The death text "You fell apart" read as an undiscussed mechanic.

**Approach:**
- **World art:** use the Kenney Tiny Dungeon and Tiny Town sprites (CC0, already in `game/Assets/Art/Kenney`) for the player, walls, floors, doors and gates, items, pickups, enemies, boulders, switches, plates, portals and the core arena. Map them through one central art catalog.
- **NPCs:** keep the stacked head, torso and leg Lego parts, redrawn as clean outlined parts with a distinct silhouette per race (the user's choice).
- **Feedback:** add clear feedback for hits, gate openings, pickups and the isotope stage.
- **Dialogue:** trim each NPC conversation to one flavour line plus the hint, and add a skip key.
- **Death text:** change it to plain wording.

## Boundaries & Constraints

**Always:**
- **Art catalog:** every sprite goes through one art catalog that maps a semantic key (e.g. Player, Wall, Floor(theme), Gate(kind), Item(id), Enemy(type), Boulder, Portal) to a sprite. A missing mapping falls back to today's generated shape, so nothing renders invisible.
- **Pixel-art import:** the Kenney textures import as pixel art (point filter, no compression artefacts, 16 pixels per unit, so one tile is one world unit) and are sliced or used per tile.
- **Distinctness:**
  - The player, every enemy type, NPCs and each item look clearly different from each other and from walls and floors.
  - Each gate kind looks like its mechanic: rock or pot, button door, vine, dark room, cracked wall, lead-plate door, timed door.
  - Floors stay visually distinct per face theme.
- **NPC parts:** keep the head, torso and leg slots and their seeded mixing. Each race's parts get a recognisable silhouette and outline (a finch beak, a mushroom cap, alien antennae, a polyhedron body, a dinosaur snout). No Kenney whole-body characters for NPCs.
- **Feedback:**
  - A brief white flash on anything that takes damage.
  - A short open effect on every gate.
  - A floating popup on pickups ("+Thin Beak", "+5 Jumbles").
  - The isotope stage shown as a clear label and colour near the player.
- **Dialogue:** at most two lines per conversation (one flavour line, then the hint), and the first hint is still true. A skip key (Escape) closes dialogue instantly.
- **Death text:** "You were defeated" in both the HUD and the between-runs screen.
- **Unchanged:** gameplay, layout, randomness, colliders and tuning. Visuals and text only, plus the skip key. Text follows lore-and-tone.md and never says "sort".

**Never:**
- No new mechanics, and no changes to collider sizes or positions that affect the seed sweep or tests.
- No other art packs or downloads.
- No attribution UI is needed (CC0). The credits file already exists in `game/Assets/Art/CREDITS.md`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Catalog | Every semantic key used by the game | Resolves to a Kenney sprite or an outlined part | A missing key logs once and falls back to the generated shape |
| Distinct | Player, each enemy type, each item, each gate kind, wall, floor | No two categories share the same sprite | A test fails on duplicates across categories |
| Hit flash | Any Health takes damage | Its sprites flash white for about 0.1 s | No error expected |
| Gate open | Any gate opens | Short open effect, then the open state | No error expected |
| Pickup popup | Item or Jumbles collected | Floating text near the player for about 1 s | No error expected |
| Isotope | The isotope changes stage | Label and colour near the player update | No error expected |
| Dialogue | Talk to any NPC | ≤2 lines: flavour line, then hint | No error expected |
| Skip | Escape while dialogue is open | Closes immediately | No effect when closed |
| Death | Player dies | "You were defeated" on the HUD and between-runs screen | No error expected |
| Determinism | Seeds 1–50 | Determinism signatures unchanged | The 1.13 determinism test fails if changed |

</frozen-after-approval>

## Code Map

- `game/Assets/Art/Kenney/Tiny Dungeon/` and `Tiny Town/`: `Tiles/tile_0000.png` … `tile_0131.png` (16×16), plus `Tilemap/tilemap_packed.png` and `Preview.png` to see the layout. Read the preview images to choose tiles. Dungeon has walls, floors, doors, chests, about 30 characters and monsters (rows 7–9), and potions, swords, keys and bombs. Town has grass, trees, roofs, fences and paths.
- Sprite creation today is spread over about 33 files. The shared helpers are:
  - `Cube/Npcs/NpcFactory.cs`: `ShapeSprite(PartShape)`, which generates a texture.
  - `Cube/Faces/Biology/BeakGate.cs`: `AddSprite(...)`.
  - `Cube/CubeWorld.cs`: `GetFallbackSprite()` and floor and wall blocks.
  - The other users are the Combat, Core, the three Faces folders, Items/Gate, Modules/ModuleSlot and NpcTalker.
- Text:
  - `Cube/Combat/CombatHud.cs`: `DeathMessage`.
  - `Cube/Run/BetweenRunsScreen.cs`: `DefeatTitle`.
  - `Cube/Npcs/Dialogue.cs`, plus the face `*Population.cs` `LinesFor` methods: conversation building.
  - `Cube/Npcs/DialogueBox.cs`: advance and close.
- Tests that assert dialogue structure, such as the 1.5 NpcTests and the Biology, Chemistry and Physics scene tests (greeting, then flavour, then hints), need updating to the new two-line shape. Keep the truth checks on hints.
- `Tests/EditMode/DeterminismTests.cs` plus `DeterminismSignatures.txt` (from 1.13): the signatures must stay unchanged, because this story changes visuals only.

## Tasks & Acceptance

**Execution:**
- [x] `Editor/ArtImportSettings.cs` (AssetPostprocessor for `Assets/Art/Kenney/**`): point filter, 16 PPU, no compression, no mipmaps. Reimport.
- [x] `Scripts/Cube/Art/ArtCatalog.cs` plus an `ArtKey` enum or struct: a ScriptableObject built by `Editor/ArtCatalogBuilder.cs`, mapping each key to a chosen Kenney tile. `ArtCatalog.Get(key)` falls back to the generated shape. Document the mapping in `Assets/Art/ART-MAP.md` (key → tile file and why).
- [x] Route all sprite creation through the catalog: player, walls and floors per theme, doors and every gate kind, items and pickups (beaks, isotope, Mass Mitt, hidden Jumbles), enemies (each type), boulders, switches, plates, flowers, portals, particles and the Demon, trial pieces.
- [x] NPC parts: redraw `ShapeSprite` parts as outlined, race-specific silhouettes (generated in code, or composed from small pixel masks). Keep the slots and seeded mixing.
- [x] Feedback: `HitFlash` (on Health damage), `GateOpenEffect` (on Gate.Opened), `PickupPopup` (on Inventory.ItemAdded and wallet earnings), and an isotope stage label.
- [x] Dialogue: two-line conversations (flavour, then hint) for Town, faces and debug spawns, and Escape skips.
- [x] Death text: "You were defeated" (HUD and between-runs screen).
- [x] Regenerate the module library and the Cube scene in batch mode if the builders changed.
- [x] Tests: EditMode tests for catalog completeness (every key used maps or falls back, no cross-category duplicates), the two-line dialogue shape with true hints, the death text, and determinism unchanged. PlayMode tests for the hit flash, gate effect, pickup popup, isotope label and Escape skip. Update the existing dialogue-structure tests.

**Acceptance Criteria:**
- Given the EditMode and PlayMode suites and the seed sweep in batch mode, when run, then all pass, and the determinism signatures are unchanged.
- Given the Cube scene, when the user plays (HITL), then they can tell things apart at a glance, see feedback for hits, gates and pickups, and get through conversations in one or two presses.

## Implementation Notes

- Checkpoint 1 approved by the user (2026-10-06, "let's continue").
- Verified in batch mode after the review patches: EditMode 197/197, PlayMode 119/119, seed sweep 50/50, determinism signatures unchanged. HITL readability check by the user PENDING.

## Review Triage Log

Pass 1 (quick lens): high 0, medium 3, low 5, false 0, maybe-false 0. The reviewer confirmed no collider, layout or randomness change (colliders checked against HEAD; determinism fixture unchanged).

| Verdict | Route | Finding | Evidence / action |
|---|---|---|---|
| medium | patch | Gate art overlaps the guarded pickup, which draws in front of the closed door | Art spans −0.1 to 0.9 and the pickup spans 0.55 to 1.4. They are now separated, with a geometry test. |
| medium | patch | Lazily created swing and halo renderers can copy the flash material and stay white | They copied sharedMaterial mid-flash. They now use the original material, with a test. |
| medium | patch | Cracked-wall gate tile is an arch piece, nearly the same as RuinArch, so a solid gate reads as walkable | The tiles were re-picked, with a distinctness check. |
| low | patch | Timed door drawn as a window tile; dark-room gate tiles a partial arch | The tiles were re-picked. |
| low | patch | Timed door plays the open effect twice | Now plays once per opening. |
| low | patch | Ruin art exceeds its placement box beyond the 0.15 gap | Art now fits its RuinSize box. |
| low | patch | Dead "Isotope Label" exclusion | Removed. |

## Design Notes

Suggested picks (the implementer verifies against the preview images):
- Player: a hero character from Dungeon rows 7–9.
- Walls: Dungeon stone. Town floor: Town grass or path.
- Face floors: Dungeon floors tinted per theme (Biology green, Chemistry violet, Physics blue).
- Gates: Dungeon doors for button and timed doors, rocks for breakable gates, Town trees or hedges for vines.
- Items: Dungeon potion for the isotope, a key-like or other tool for the beaks, a gauntlet-like tile or the closest match for the Mass Mitt.
- Hidden Jumbles: coin or chest. Enemies: distinct monster tiles per type.

Tinting is allowed to separate variants, but each category should differ by shape, not only by colour.

## Verification

**Commands:**
- Unity batch `-runTests -testPlatform EditMode` and `PlayMode -nographics` -- expected: result Passed, 0 failed.
- Unity batch `-quit -executeMethod CubeSceneBuilder.RunSeedSweep` -- expected: 50/50.
- HITL: the user plays the Cube scene and confirms readability.
