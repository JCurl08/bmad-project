---
title: 'Island screen fixes'
type: 'bugfix'
ticket: '26'
created: '2026-10-07'
status: 'built'
baseline_revision: 'd8b5e0a'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-video-game/change-playtest-3-world-redesign/change-playtest-3-world-redesign.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The user played 1.25's island screens. They said: "I like the change in scale of the bridge puzzles but I think one is impossible and I would like some indication that it is activating something. Like the bridge changing colour. I don't like the weird brown dots that they all connect to. I think we can just make these regular tiles."

**Approach:**
1. **Impossible screen.** Find out why an island screen seemed impossible. Check every island screen on seeds 1–50 at runtime: can the player reach every island the sweep says is reachable from each doorway, and mend every broken span with the Gap-Crosser\*?
   - If it's a real mismatch (collider gaps, plank widths, spans that can't be swung at, doorway landings), fix it.
   - If it's the deliberately isolated doorway, make the dead end readable. For example, a small lookout with a sign or view, so it doesn't look broken. Note it for the 4.12 puzzle rework.
2. **Floor tiles.** Replace the brown island pads with the region's normal floor tiles. Islands read as land by their floor and a shoreline edge, not by brown blobs.
3. **Bridges show activity.** A plank lights up (changes colour) once the player has walked it. A broken span changes colour when mended. It stays visual only, with no new gating.

</frozen-after-approval>

## Boundaries & Constraints

- **Gameplay:** no change except fixing real bugs. Nothing from Epic 4.
- **Tests and sweep:** a runtime test walks every island screen (seeds 1–50) from each connected doorway, and EditMode tests cover the lighting state. The sweep stays 50/50.
- **Determinism:** re-record only if a fix moves content, with a note.
- **PlayMode runs:** class batches, `-nographics`, in the foreground.

## Tasks & Acceptance

- [x] Root-cause the "impossible" screen; fix it, or make the dead end readable.
- [x] Islands use region floor tiles.
- [x] Planks light up when walked; mended spans change colour.
- [x] Tests; full verify.

**Acceptance:** the suites and the sweep pass. HITL: islands look like land, bridges light up, and no island screen feels impossible.

## Implementation Notes

- Checkpoint 1 approved: the user approved the course correction (2026-10-07).
- **Root cause: the lone doorway, not a bug.** The new PlayMode test `IslandWalkTests` checks the sweep's model against the real game.
  - **What it covers:** every island screen on seeds 1–50 (100 screens, 400 doorways). Each doorway is teleported to, walked onto, walked out of and back in by.
  - **The walk:** the player uses the real keys, colliders and swing. It visits every island that `DoorwayReach` says the doorway reaches, plank centre to plank centre. It does this twice: without the gap-crosser\* (every broken span on the way must stop it), then with it (every gap is mended by walking up to it and swinging).
  - **Result:** no mismatch. 1876 islands were walked to and 150 spans mended by a swing. Broken spans stopped the player 250 times. No collider gaps, water seams, narrow planks, unhittable spans or bad doorway landings.
  - **So the "impossible" screen was the lone doorway.** Every island screen has one doorway whose islands join no other doorway. 42 of the 100 are a bare pad in open water; the rest have a lookout island or two. They looked broken, not deliberate.
  - **Not changed:** the graph. Noted for the 4.12 puzzle rework.
- **The dead end now reads as one.**
  - `IslandLayout.IsLone(island)` marks the islands of a doorway that joins no other, even with the gap-crosser\*. It needs two or more doorways, and the trial's islands are never lone.
  - The lone doorway's island carries a lookout sign: a post and a framed board whose arrow points back out of the doorway. It stands to one side of the walk in, away from any plank.
  - Arriving on a lone doorway's islands from anywhere else pops up "Only water here. Go round!" for 2.5 s.
  - `PickupPopup.Show` gained an optional duration for this.
  - Visual only: no colliders, and the graph is unchanged.
- **Islands are floor.**
  - The `Island` pads are gone. The water already left holes for the islands, so the islands are now the face's own floor (`FloorMath`, with its tint, checker and texture) showing through.
  - Each island has a generated shoreline on every edge that meets the water (never on the wall): a pale foam line with a band of translucent wet sand inside it.
  - The `Island` art key (D 66) is removed from `ArtKey`, `ArtCatalogBuilder` and `ART-MAP.md`. The art catalog and the Cube scene were regenerated (one catalog entry gone; the scene changed object IDs only).
- **Bridges light up (visual only).**
  - `PlankLights` holds the pure state: `Unlit`, `Walked` or `Mended`.
  - A whole plank becomes `Walked` the first time the player stands on it. A broken span becomes `Mended` when the gap-crosser\* mends it, for good; walking it changes nothing, and its unmended ends light nothing.
  - `IslandScreen` reads where the player is every frame (it finds the `CubeNavigator`).
  - Unlit planks are a shade dim. A walked plank shows full brightness under a soft green glow; a mended span (all three pieces) shows the gap-crosser\*'s gold. The old `BrokenSpan.ShowMended(laid)` tint moved into the screen's light.
- **Tests.**
  - EditMode (`MathTests`): plank lights; `IsLone` and the sign's spot over 300 layouts; the shoreline lies on the island along every water edge and never along a wall.
  - The new `LoneText` is in the in-world text scan and in the Math strings check.
  - PlayMode (`MathComponentTests`): a walked plank lights up once, a mended span turns gold, the lone doorway has its sign and says go round once per arrival, and there are no pads.
  - `IslandWalkTests` also checks the scene's lights and signs after its walk: 613 planks lit, every mended span gold, and 100 signs, one per lone doorway.
  - `IslandWalkTests` runs in about 75 s at one physics step per frame (`Time.captureDeltaTime`). It is its own batch, the 7th, added to `verify.sh` in the scratchpad.
- **Determinism:** no content moved (no layout, RNG or placement change), so nothing was re-recorded.
- **Review patches (pass 1).**
  - **`IslandWalkTests` restores what it changes in `TearDown`** (`Time.captureDeltaTime` and the player's speed), not only in the iterator's `finally`.
  - **It checks lighting along its own path.** Every whole plank the walker crossed must show Walked, and every span it crossed must show Mended. A span counts as mended only if the walker swung at it, so loops in the graph can't cause false failures. `planksLit > 0` and `spansMended > 0` are asserted.
  - **A stop at a gap must be at the gap's near edge:** the player's centre one player radius short of it, within 0.1. A stop anywhere earlier fails with where the player stood. This applies both to the "blocked without the gap-crosser\*" checks and before every swing.
  - **`IslandScreen.SignSpot` searches the doorway island on a 0.05 grid.** The spot it picks must satisfy three rules:
    - the whole board (`SignBoard`, 0.86 x 0.54) stands on the island;
    - the spot is at least `SignOffWalk` (0.6) aside of the walk in;
    - the board is at least half its width clear of every plank mouth (`PlankMouths`, measured with `RectGap`).
    The nearest such spot to one side of the island's middle wins. The EditMode test checks all three rules over 300 layouts (more than 100 of them have a lookout plank mouth).
  - **`PickupPopup` sizes its label to the text.** It measures the text with `GUIStyle.CalcSize`, turns word wrap off, and never goes below the old 300 x 30 (`LabelSize`). An EditMode test measures "Only water here. Go round!" with the built-in font and checks the label fits it.
  - **The recurring `EarthSceneTests` flake is fixed in game code.**
    - `NpcMover` gains keep-out circles (`SetKeepOut`). A wander target inside one is drawn again, up to 8 draws, and the NPC pauses if none is clear. A step that would end inside a circle loses its inward part (`KeptOutVelocity`), so the NPC slides round. One already inside, pushed there, may still walk out.
    - `FaceContent.KeepOffSlots` gives every face NPC (the shared population loop and the Physics cameo) its screen's slot marks at the full spawn keep-out (`m.Radius + SlotKeepOut`). Before this fix, wandering could carry an NPC past the test's keep-out margin. The test was not loosened.
    - EditMode test: `NpcMover_KeptOutVelocity_NeverCarriesABodyIntoAKeepOutCircle...`.
    - The fixture is unchanged: spawn spots did not move.

## Review Triage Log

Pass 1 (quick lens), triaged by the coordinator: high 0, medium 1, low 6, plus the recurring Earth flake.

| Verdict | Route | Finding | Outcome |
|---|---|---|---|
| medium | patch | `IslandWalkTests` restores `Time.captureDeltaTime` only in the iterator's `finally` | Patched: restored, with the player's speed, in `TearDown`. |
| low | patch | No lighting assertion on the scene path | Patched: crossed planks Walked, crossed spans Mended, `planksLit > 0`, `spansMended > 0`. |
| low | patch | "Blocked by the gap" passes on any early stop | Patched: the stop must be at the gap's near edge (one player radius, ±0.1), or the test fails. |
| low | patch | "Every broken span mended" could false-fail on loops | Patched: only spans the walker swung at are checked. Each claimed island must still be reached. |
| low | patch | The sign could sit by a plank mouth | Patched: a grid search keeps the board at least half its width clear of every mouth, on the island and off the walk in; EditMode test. |
| low | patch | The pop-up label (300 px) might clip "Only water here. Go round!" | Patched: the label is sized to the measured text with no wrap; EditMode `CalcSize` test. |
| low | reject | The message repeats on every arrival | By design. |
| flake | patch | `EarthSceneTests`: an NPC wanders to 1.88 of a gate front against 1.9 (three runs) | Fixed in game code: NPC wander keep-out circles at the full spawn keep-out; EditMode test. The test is unchanged. |

## Verification

- `verify.sh <tag>` in the scratchpad.

**Result (2026-10-07, `verify.sh v126a`):**
- EditMode 261/261.
- PlayMode batches 25/22/26/26/35/24/1:
  - batch 1 had 21/22 on the run. `EarthSceneTests` hit the known NPC-wander flake: a Physics alien stood 1.88 from a gate front against 1.9. It is unrelated to this story. A foreground rerun of batch 1 (`md-v126a_b1`) passed 22/22.
  - batch 6 is the new `IslandWalkTests`: 0 problems.
- Skill checks: Victory on seeds 1234, 7 and 42.
- Sweep 50/50, 0 failing.
- Fixture unchanged.
- The run's Claude-side task was reaped for low memory part-way through, but the script itself ran to the end.
- Not committed.

**Result after the review patches (2026-10-07, `verify.sh v126b`):**
- EditMode 263/263.
- PlayMode batches 25/22/26/26/35/24/1, all passing; batch 6 is `IslandWalkTests` with 0 problems.
- Skill checks: Victory on seeds 1234, 7 and 42.
- Sweep 50/50, 0 failing.
- Fixture unchanged.
- Not committed.
