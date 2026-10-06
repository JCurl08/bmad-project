---
title: 'Health, enemies and item-weak combat'
type: 'feature'
ticket: '6'
created: '2026-10-05'
status: 'built'
baseline_revision: '1b623285b37faf44d7e6c9dda79d99bb21103803'
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
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Nothing can hurt the player or be hurt, so combat-as-puzzle (CAP-10), death ending a run (1.12) and hostile races (1.5's flag) have nothing to stand on.

**Approach:** Give the player health, damage with brief invulnerability, a death event, and stat fields (health, defence, power, speed) that later upgrades change. Add a player attack that uses the equipped item, an enemy base whose weakness is an item (much more damage from that item), and contact damage. NPCs whose race is flagged hostile switch to enemy behaviour and attack on sight.

## Boundaries & Constraints

**Always:** Stats live in one component that movement, damage and attack read. Upgrades (1.12) only change stat values. Defence reduces incoming damage, power scales outgoing damage, and speed scales movement. Damage from an enemy's weakness item is markedly higher (at least 3×) than from anything else. The attack uses the `Player/Attack` action, and `Player/Previous` and `Player/Next` cycle the equipped item among owned items. With no item equipped, a bare attack still does base damage. Hostile NPCs reuse the same enemy behaviour and health, and turn back to talkers when the flag clears. Death raises an event and stops player control, and does nothing more.

**Never:** No run loop, respawn or currency on death or kill (1.12). No face-specific enemies (1.8 to 1.10 add them on this base). No knockback physics tricks that can push the player through walls. No new art beyond placeholder shapes.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Weakness hit | Enemy weak to item X, player attacks with X equipped | Damage ≥ 3× the non-weakness damage | No error expected |
| Other hit | Same enemy, bare attack or another item | Base damage scaled by power | No error expected |
| Enemy dies | Enemy health reaches 0 | Enemy removed, Died event raised once | No error expected |
| Contact damage | Enemy touches player | Player loses health reduced by defence, then is briefly invulnerable | Repeated contact during invulnerability does nothing |
| Defence | Defence stat raised | Incoming damage lower (minimum 1) | No error expected |
| Speed | Speed stat raised | Player moves faster | No error expected |
| Player dies | Player health reaches 0 | Died event raised once, movement and attack stop | Further damage is ignored |
| Hostile NPC | Race flagged hostile while the player is near | That race's NPCs chase and damage the player and can be hurt | Clearing the flag returns them to talking and they stop attacking |
| Item cycle | Player owns 2 items, presses Next | Equipped item changes; the HUD shows it | With no items, Next does nothing |

</frozen-after-approval>

## Code Map

- `game/Assets/Scripts/Tracer/PlayerMover.cs` -- Rigidbody2D mover with a `speed` field and a `Speed` property, on `Player/Move`. Speed must come from the stats component; keep the tracer scene working.
- `game/Assets/Scripts/Cube/Items/Inventory.cs`, `ItemDefinition.cs`, `ItemCatalog.cs` -- owned items (`Has`, `Add`, `ItemAdded`). Equipping builds on these.
- `game/Assets/Scripts/Cube/Npcs/RaceRelations.cs` (hostility flag and `Changed` event), `NpcTalker.cs` (refuses while hostile), `NpcMover.cs` (dynamic body with gravity 0, velocity-driven, stays inside its screen and respects walls).
- `game/Assets/Scripts/Cube/CubeDebug.cs` -- debug keys F1 to F6, with F3 spawning NPCs and F4 toggling hostility. F7 is free for a debug enemy spawn.
- `game/Assets/Editor/CubeSceneBuilder.cs` -- builds the Cube scene and player. Add the stats, health, attack and HUD components to the player.

## Tasks & Acceptance

**Execution:**
- [x] `game/Assets/Scripts/Cube/Combat/PlayerStats.cs` -- health (max), defence, power and speed, with a `Changed` event. PlayerMover reads speed from it when present.
- [x] `Health.cs` -- shared by the player, enemies and hostile NPCs: current/max, `TakeDamage(amount, sourceItem)`, invulnerability window, `Damaged` and `Died` events (Died fires once).
- [x] `PlayerAttack.cs` -- a short melee hitbox in front of the player's last move direction, on `Player/Attack`. It carries the equipped item. Cooldown.
- [x] `Equipment.cs` -- equipped item from the Inventory, cycled by `Player/Previous` and `Player/Next`. It auto-equips the first item picked up.
- [x] `Enemy.cs` + `EnemyBrain.cs` -- weakness ItemDefinition and damage multiplier, chase within its screen and contact damage. Movement respects colliders (same approach as NpcMover).
- [x] `HostileNpc.cs` -- on an NPC: when its race becomes hostile, enable the enemy brain and health. When the flag clears, disable them and restore NpcTalker and NpcMover.
- [x] `CombatHud.cs` -- hearts/health, the equipped item name, and a "You fell apart (entropy wins… this time)" message on death. Campy, never says "sort".
- [x] `CubeDebug.cs` -- F7 spawns a placeholder enemy weak to a random owned item (or the Biology placeholder) near the player.
- [x] Wire the player components in CubeSceneBuilder and regenerate the Cube scene in batch mode.
- [x] Tests: EditMode tests for damage maths (weakness multiplier, power, defence minimum 1, invulnerability). PlayMode tests for every I/O row (weakness vs other hit, contact damage plus invulnerability, death event once and control stopping, hostile NPC attacking and reverting, item cycle, speed stat).

**Acceptance Criteria:**
- Given the EditMode and PlayMode suites and the seed sweep in batch mode, when run, then all pass, including earlier stories'.
- Given the Cube scene, when F7 spawns an enemy and the player attacks it with and without its weakness item, then the difference is obvious (a few hits versus many).

## Implementation Notes

- Verified in batch mode after the review patches: EditMode 95/95, PlayMode 47/47, seed sweep 50/50. WebGL build PENDING (user). F7 check by hand PENDING (user).
- Checkpoint 1 was approved under the user's standing instruction (2026-10-05) to keep building while they are away. Review the plan on return.
- Verified in batch mode: EditMode 95/95 (11 new), PlayMode 45/45 (11 new), seed sweep 50/50. Cube scene regenerated. WebGL build skipped per Verification. F7 has not been tried in the running editor yet (user to-do).
- Assemblies: Game.Cube references Game.Tracer, so PlayerMover cannot see PlayerStats. Game.Tracer now has `IMoveSpeedScale`; PlayerStats implements it and PlayerMover multiplies its base speed by it (looked up lazily; the Tracer scene has none and is unchanged). PlayerMover also exposes `Facing` (last move direction, down at start) for the attack, and zeroes its velocity when disabled.
- Damage is float. `CombatMath` holds the rules: stat multiplier 1 + 0.25 per point above 1 (power and speed), incoming = max(1, raw - defence), weakness x4 (clamped to at least x3). `Health.TakeDamage` runs the amount through every enabled `IDamageModifier` on the object (PlayerStats = defence, Enemy = weakness). A disabled Health takes no damage, which is how peaceful NPCs are immune.
- Numbers: player 6 health, 0.8 s invulnerability; enemy 8 health, 0.1 s invulnerability, contact 1, chase 2.2 u/s, sight 7; attack base 1, cooldown 0.35 s, a 1.1 box 0.85 in front, hitting each Health once per swing. Contact damage is a collider-distance check in EnemyBrain.FixedUpdate (no knockback).
- Death: PlayerStats disables PlayerMover, PlayerAttack and Equipment and raises its own `Died`; NpcTalker refuses to talk while the player's Health is dead; nothing else. A dead enemy that is not removed (DestroyOnDeath=false) stops moving and deals no contact damage. The HUD (OnGUI, top right) shows hearts, the equipped item and the death message.
- Hostile NPCs: NpcFactory.Spawn attaches dormant Health, Enemy, EnemyBrain and HostileNpc. While hostile, NpcMover is off and the brain is on; NpcTalker stays enabled (it already refuses and tints). Hostile NPCs have no weakness item (none is defined per race yet) and die to 8 bare hits; damage taken while hostile is kept when the flag clears.
- F7 picks the weakness from owned items with a SeededRng(run seed, spawn count) on its own stream 6 (`CubeDebug.EnemySpawnRngStream`; NPC parts use 4, NPC wander 5); with none owned it uses the catalog Biology item. F7 enemies are removed on a rebuild.
- Not committed: code, tests and the new .meta files are on disk for review.

## Review Triage Log

Pass 1 (quick lens): high 0, medium 2, low 4, false 0, maybe-false 0. Plus 1 pending HITL item.

| Verdict | Route | Finding | Evidence / action |
|---|---|---|---|
| medium | patch | A dead enemy keeps chasing and dealing contact damage when DestroyOnDeath=false | The brain never checked its own Health. Face enemies build on this base. It now stops at death, with a test. |
| medium | patch | Death doesn't stop all player control (Equipment, NPC talk) | Breaks the Always rule. Both are now disabled or refused while dead. |
| low | patch | Hit count includes undamaged targets | `hit.Add` ran before TakeDamage. It now counts only applied damage. |
| low | patch | F7 RNG stream derived from NPC constants (6), but documented as 7 | Now its own named constant, with the docs fixed. |
| low | patch | HUD overlaps the debug overlay at 960×600 | Re-anchored. |
| low | patch | Uncached GetComponent every FixedUpdate in the tracer | The null result is now cached. |
| n/a | pending HITL | AC 2 F7 check by hand in the Cube scene | Covered by an automated scene test. The by-hand check is on the user's list. |

## Design Notes

Suggested numbers: base attack 1, weakness ×4, enemy health 8 (2 weakness hits, 8 bare hits), contact damage 1, player health 6, invulnerability 0.8 s. Damage taken = max(1, raw − defence). Damage dealt = base × power multiplier (power 1 = ×1.0, +0.25 per point).

## Verification

**Commands:**
- Unity batch `-runTests -testPlatform EditMode` and `PlayMode` -- expected: result Passed, 0 failed.
- Unity batch `-quit -executeMethod CubeSceneBuilder.RunSeedSweep` -- expected: still 50/50.
- WebGL build: skip during this session (low memory); the user builds later.
