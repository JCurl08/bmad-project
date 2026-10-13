---
title: 'Release readiness'
type: 'chore'
ticket: '21'
created: '2026-10-08'
status: 'in-progress'
baseline_revision: 'c1c7865'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: ''
review_source: ''
lenses_ran: []
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/faces-and-races.md'
  - '{project-root}/_bmad-output/initiative-video-game/change-playtest-3-world-redesign/change-playtest-3-world-redesign.md'
  - '{project-root}/_bmad-output/initiative-video-game/epic-world-worth-exploring/puzzle-difficulty-research.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The 2026-10-13 polish review (`polish-review-2026-10-13.md`, Tier 1) found these issues to fix before a public itch.io release. The user said: "let's document and start with tier 1".

**Approach:** One release-readiness pass:
1. **Saves survive itch re-uploads.** Meta progress uses a browser localStorage key that does not change between builds: a small `.jslib` bridge in WebGL, with PlayerPrefs elsewhere. On first load it migrates an existing PlayerPrefs save. `MetaSave.Save` never throws (try/catch plus a warning).
2. **The HUD scales with the screen.** Every OnGUI panel (CombatHud, CoreHud, DialogueBox, CubeMap, QuestLogView, BetweenRunsScreen, PickupPopup labels and the F1 bars) uses one shared scale based on 960×600, so text stays readable at any size or DPI.
3. **Everything works on a gamepad.** The quest log, close/skip, the shop's buy keys and Continue become input actions with gamepad bindings, alongside the existing keys. The tour's "press Q" step accepts the gamepad. Prompts show the real key or button names instead of "[Interact]".
4. **No generation hitch.** Puzzle, temple and lock generation is pre-warmed over several frames while the player is in Town, or on the between-runs screen before a run. The reveal frame does no heavy generation once the cache is warm.
5. **Release identity.** Set a real product name and company in player settings, and a version (for example 0.4.0). The run-end screen shows the version and seed.
6. **Abandon run.** An "Abandon run" option on the map or quest log, with a yes/no confirm, ends the run like a death so the player can never get stuck.
7. **Smaller build.**
   - Managed stripping set to High.
   - Remove unused packages: visual scripting, timeline, collab and the learn framework, plus unused modules where safe.
   - Remove `Tracer.unity` and the leftover InitTestScene and Sample scenes from the build list. Check TracerSceneTests first, and keep test scenes that tests need.
   - Report the build size if a build is possible; otherwise leave the build to the user.
8. **No Esc needed.** The map, quest log and dialogue also close with M, Q or Interact, so players never need Esc in browser fullscreen.

</frozen-after-approval>

## Boundaries & Constraints

- The seed sweep stays at 50/50. Re-record determinism, with a note.
- The subtle-science and voice rules still hold (InWorldText, voices-and-names.md).
- The readability tests stay green.
- PlayMode tests run in class batches.
- Nothing from later Epic 4 stories, except the hooks they need.

## Tasks & Acceptance

- [ ] localStorage save bridge, migration and a safe Save, with tests.
- [ ] Shared HUD scale, with layout tests at 960×600, 1280×800 and 1920×1200.
- [ ] Gamepad actions for every key-only control; real key names in prompts; tests.
- [ ] Generation pre-warm; a test that the reveal frame is cheap once warm.
- [ ] Release identity, and the version and seed on the run-end screen.
- [ ] Abandon run, with confirm; tests.
- [ ] Stripping, package and scene clean-up; tests still pass.
- [ ] Non-Esc close keys.
- [ ] Update player-guide.md and tools/README.md. Run the full verify with tools/verify.sh.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
