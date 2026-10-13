---
title: 'Release readiness'
type: 'chore'
ticket: '21'
created: '2026-10-08'
status: 'built'
baseline_revision: 'c1c7865'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 1
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

- [x] localStorage save bridge, migration and a safe Save, with tests.
- [x] Shared HUD scale, with layout tests at 960×600, 1280×800 and 1920×1200.
- [x] Gamepad actions for every key-only control; real key names in prompts; tests.
- [x] Generation pre-warm; a test that the reveal frame is cheap once warm.
- [x] Release identity, and the version and seed on the run-end screen.
- [x] Abandon run, with confirm; tests.
- [x] Stripping, package and scene clean-up; tests still pass.
- [x] Non-Esc close keys.
- [x] Update player-guide.md and tools/README.md. Run the full verify with tools/verify.sh.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
- **Assumption: release identity.** `productName` is **"Entropy Cube"** (the working title) and `companyName` **"JCurl08"**, until the naming brainstorm. The version is **0.4.0** (`bundleVersion`; Epic 4, the first public build). In the editor and on desktop, PlayerPrefs live under company/product, so the editor's own dev save (under `DefaultCompany/game`) is no longer seen. Tests use their own key, so they are unaffected.
- **1. Saves** (`Run/MetaStorage.cs`, `Plugins/WebGL/MetaStorage.jslib`):
  - `IMetaStore` (Get/Set/Has/Delete/Flush), with three stores:
    - `PlayerPrefsStore`;
    - `MemoryStore`, for tests;
    - `BrowserStore`: localStorage through the jslib, keys prefixed `entropy-cube/` because the itch origin is shared. The `DllImport`s sit behind `#if UNITY_WEBGL && !UNITY_EDITOR`; anywhere else it throws and `Available` is false.
  - `MigratingStore(primary, legacy)`: a key missing from the primary is read once from the legacy store and copied over. The legacy copy is left as it was. Delete clears both stores, so a deleted save never comes back.
  - `MetaStorage.Choose(browserAvailable, ...)` picks the store. In a WebGL player with working localStorage it is the browser store migrating from PlayerPrefs; anywhere else, PlayerPrefs. The jslib probes localStorage and catches every error, so private modes or blocked storage fall back to PlayerPrefs.
  - `MetaSave.Load`, `Save` and `Delete` go through `MetaStorage.Current`. None of them ever throws: `Save` returns false and logs a warning.
  - The test isolation resets the store before and after each test.
  - *Assumption:* the migration only helps when the old PlayerPrefs save can be read. On itch, each new upload gets a new IndexedDB path, so a save from a pre-4.21 upload is already out of reach. From this build on, saves survive uploads.
- **2. HUD scale** (`HudScale.cs`):
  - `Factor = max(0.5, min(w/960, h/600))`. Each `OnGUI` calls `HudScale.Begin()`, which sets `GUI.matrix` and returns the virtual screen. That screen is always at least 960x600, so the 960x600 layouts fit at any size.
  - Used by: CombatHud, CoreHud, DialogueBox, CubeMap, QuestLogView, BetweenRunsScreen, PickupPopup (`WorldToGui`), the F1 bars and slot labels, and the Chemistry stone and Mass Mitt HUD lines.
  - The pure layout functions (`PanelRect`, `Layout`, `NetFrame`, `OverlayBars`) are unchanged and now take the virtual size, so `HudLayoutTests` passes unmodified.
  - The wall band is a fixed share of the height, so the F1 bars still lie exactly on the walls in real pixels (tested).
  - IMGUI maps the mouse through the matrix, so buttons keep working.
  - *Assumption:* below 960x600 the HUD shrinks (down to x0.5) rather than clipping.
- **3. Gamepad** (`InputSystem_Actions.inputactions`; `GameActions` and `InputPrompts` in `InputPrompts.cs`):
  - New Player actions, alongside the existing keys:
    - `QuestLog`: Q, D-pad up;
    - `Close`: Esc, Backspace, B;
    - `Continue`: Enter, numpad Enter, Start;
    - `Buy1`-`Buy4`: 1-4, numpad 1-4;
    - `MenuUp`/`MenuDown`: arrows, d-pad or stick;
    - `Confirm`: Space, A;
    - `Abandon`: X on keyboard and pad;
    - `Map` also gets Select.
  - On the between-runs screen a gold frame picks among the four upgrades and Continue. A takes the pick; Start continues from anywhere. The pick starts empty (review patch: Confirm does nothing until a row is picked), and a press in the frame the screen opens is ignored.
  - `InputPrompts.Fill` swaps prompt tokens (`[Interact]`, `[Esc]`, `[M]`, `[Q]`, `[Enter]`, `[Arrows]`, `[Abandon]`, `[Confirm]`, `[prev]`, `[next]`, `[1]`-`[4]`) for the names on the device in use, taken from the action's own bindings. The device in use is the one that last performed an action.
  - Applied in: the dialogue box (prompts and lines), the map's hints, the quest log (hints and steps), popups (lock and temple rules), the Mass Mitt HUD and the between-runs buttons.
  - The tour's quest-log, map and item steps now carry tokens. The tour's "press Q" stop is a state check, so the pad's D-pad up completes it.
  - *Assumptions:*
    - pad names use the Xbox style (A/B/X/Y, Select, Start);
    - the tour's spoken stop lines ("Press Q...", pinned by `QuestTests`) keep their keyboard wording; the quest-log step under them shows the real button.
- **4. Pre-warm** (`GenerationPrewarm.cs`; `CubeWorld.StartPrewarm`):
  - After every `Rebuild`, a coroutine waits one frame, then works through small steps, about 6 ms a frame:
    1. the run's `CubeLayout`;
    2. each ice, wind and island screen's `LayoutFor`, with the same keys, tiers and trial index as their `Create`;
    3. `TemplePlan.For` for every temple face.
  - `RevealScience` takes the warmed layout when it belongs to this run's model, and stops the coroutine. The plans then come from the caches.
  - No RNG side effects: each generator seeds its own stream. The fixture is unchanged.
  - `BoundedCache.Added` counts generations; it is exposed as `IcePlan`/`WindPlan`/`IslandPlan`/`TemplePlan.Generated`.
  - Measured in EditMode (the reveal's plans, cold then warm): seed 7 410 -> 0.3 ms, seed 21 154 -> 0.3 ms, seed 42 66 -> 0.2 ms.
  - In the scene, seed 42 warmed in 12 steps over 2 frames. The warmed reveal still took 377 ms in the editor: that is building GameObjects plus the editor-only `RunPlanCheck`, which release builds skip.
  - *Assumption:* nothing is warmed on the between-runs screen, because the next seed is drawn only on Continue. `EdgeLocks.ForRun` stays in `Rebuild`, on the Continue press.
  - *Note:* the budget is checked between steps, and one step (a temple or the whole layout) can take longer than 6 ms. A single Town frame may still run 50-150 ms in wasm. Check this in the browser.
- **5. Identity:** ProjectSettings as above. The between-runs screen shows "Build 0.4.0" (`BuildLine(Application.version)`) right-aligned on the seed's line.
- **6. Abandon run** (`QuestLogView.AskAbandon`, `RunLoop.Abandon`, `Health.Kill`, `DialogueChoice.startOnNo`):
  - With the quest log open, X (pad X) closes the log and asks in the dialogue box, with "Keep going" picked first. Esc, Backspace or B leaves the question unanswered.
  - A yes kills the player (`Health.Kill`), so the run ends through the existing death path: in a core fight through the arena's `Lose`, elsewhere through `RunLoop.OnPlayerDied`. `OnRunEnded` then banks the wallet, cashes in keys and counts the run, exactly as for a death.
  - The log's footer shows "[X] abandon this run".
- **7. Smaller build:**
  - `managedStrippingLevel` is WebGL: High, set in ProjectSettings and again in `WebGLBuilder`. `Assets/link.xml` keeps `Game.Cube`, `Game.Tracer` and `Unity.InputSystem` whole (they rely on reflection and serialization).
  - **Removed packages:** `com.unity.visualscripting`, `com.unity.timeline`, `com.unity.collab-proxy`, `com.unity.learn.iet-framework` (with the template's `Assets/Welcome/` folder that needed it).
  - **Removed modules:** ai, androidjni, cloth, terrainphysics, vehicles, wind, umbra, unityanalytics, video, unitywebrequestassetbundle, unitywebrequestaudio, unitywebrequesttexture, unitywebrequestwww.
  - **Kept modules** (used, or possibly referenced by URP and package editor code): terrain, particlesystem, physics, director, xr, uielements, and the rest.
  - **Scenes:**
    - `Tracer.unity` is out of the build list. `TracerSceneTests` loads it by path (`EditorSceneManager.LoadSceneAsyncInPlayMode`), and neither scene builder adds it back.
    - `WebGLBuilder` builds only `Cube.unity` and logs the version and size.
    - `SampleScene.unity` is deleted, and so are the 14 leftover `InitTestScene*.unity` files in `Assets/` (git-ignored, never in the build list).
  - **No build was made** (low memory): the build size is for the user to check.
- **8. No Esc needed:**
  - The map closes with M, Tab or Select; the log with Q or D-pad up. A dialogue pages to its end with Interact.
  - Backspace and the pad's B do everything Esc does (`Close`).
  - *Assumption:* "M, Q or Interact" means each panel's own key. Interact does not close the map or the log, because existing tests rely on Interact being ignored there.
- **Tests:**
  - New EditMode `ReleaseReadinessTests` (20):
    - the store: a failing store, the current store, the editor's choice, the WebGL choice and migration, the jslib importer;
    - the scale at 960x600, 1280x800 and 1920x1200 plus four other sizes, with every panel fitting and growing;
    - every action has keys and a pad binding, and the prompts' names;
    - the shop's pick;
    - the pre-warm on seeds 7, 21 and 42: a warmed reveal generates nothing and equals the cold one;
    - identity, stripping, the build scenes and the manifest.
  - New PlayMode `ReleaseReadinessSceneTests` (4, batch 8):
    - abandon with real keys: Keep going, Esc, then a yes ends the run like a death, banking 5 and counting the run;
    - a pad on the log, map, dialogue and abandon, plus Backspace;
    - the pad shop and Continue;
    - the pre-warm over frames, then a reveal that generates nothing.
  - The EditMode asmdef now references `Unity.InputSystem`.
- **Docs:** `player-guide.md` (the controls table with a pad column, no Esc needed, the HUD scale, abandon run, the between-runs screen on a pad, build and seed, saves). `tools/README.md` needed no change; batch 8 in `verify.sh` gained the new class.

- **Review patches (pass 1, quick lens; see the Review Triage Log):**
  - **Saves:**
    - The save carries a `revision`, raised by every `Save` (a pre-4.21 save reads as 0).
    - `MigratingStore.Get` reads both copies and returns the newer one. A legacy copy is copied into the primary only when it is newer or the primary has none; a failed copy is logged, and the legacy text is still returned. When the primary *fails* to read (not a missing key), the legacy text is returned but never copied over.
    - `Set` and `Flush` fall back to PlayerPrefs (flushed, with a warning) when the primary refuses, so a save is never lost silently. Only when both stores refuse does `Save` return false.
    - Delete flushes (`MetaSave.Delete`, and the legacy store in `MigratingStore.Delete`).
  - **jslib:** `EntropyStore_LastStatus` says whether the last Get found the key, found nothing, or failed. `BrowserStore.Get` throws on a failure, so a missing key and an error are told apart.
  - **jslib meta:** `MetaStorage.jslib.meta` has a full `PluginImporter` block: WebGL only, not Any, not the editor. Tested.
  - **Caches:** `Rebuild` clears the Ice, Wind, Island and Temple caches first, before the locks and the pre-warm. `BoundedCache` now evicts its oldest entry (a queue in insertion order) instead of clearing everything.
  - **Between-runs screen** (*choice: the first option*): the screen opens with nothing picked (`NoPick`), and Confirm (Space, A) does nothing until the arrows or the pad pick a row. From no pick, down lands on the first upgrade and up on Continue. So a stray Space or A never buys and never skips the shop; 1-4 and Enter/Start work as before.
  - **Yes/no answers:** Confirm (the pad's A, Space) answers as well as Interact. On a pad the prompt is `ChoicePromptPad` ("[Stick] choose   [A] confirm").
  - **Prompt device:** `InputPrompts.DeviceFor` switches only on a button or key (`ButtonControl`). Never on a stick, the pointer's movement or the Look action.
    - A joystick counts as a pad and shows the Gamepad bindings' names. The actions bind joysticks only for moving and swinging.
  - **Prompt cache:** `InputPrompts.Fill(text)` keeps filled strings per template and device (up to 512). The cache is cleared when the bindings change (`BoundControlsChanged`) or the actions asset changes.
  - **Tests:** the `InitTestScene` leftover assertion is dropped, since leftovers from killed runs must not fail the suite. New tests:
    - EditMode: the revision; a failed copy; a refused write falling back with the newer copy winning; an unreadable primary; oldest-first eviction; the importer is WebGL only.
    - PlayMode: device detection and the fill cache; A and Space with no pick; A answering the abandon question.
  - **Accepted: saves after the rename.** The editor's and desktop's old PlayerPrefs under `DefaultCompany/game` are not read. Reading another product's PlayerPrefs is not possible through the API, and only dev saves live there, so this is accepted as is.

## Verification

**First run (`r421`):** quick runs passed. EditMode 94/94 over the touched classes and PlayMode `ReleaseReadinessSceneTests` 4/4. The host's memory guard then stopped `tools/verify.sh r421 edit` before it produced results.

**Result after the review patches (2026-10-13, `tools/verify.sh v421b`, one part at a time):**
- `edit`: EditMode 432/432 (without `Slow`).
- `robust`: 1/1, the 500-seed test.
- PlayMode batches 0-8: 39/29/51/28/38/37/4/14/9, all passing. Batch 8 now holds `TownTourSceneTests` (4) and `ReleaseReadinessSceneTests` (5).
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s (unchanged).
- `sweep` (without `ALLOW_FIXTURE_CHANGE`): 50/50, 0 failing, **fixture unchanged**.
- No Unity process left running. Not committed.
- No WebGL build was made (low memory); the build and its size are left to the user.

## Review Triage Log

Pass 1 (quick lens), triaged by the coordinator: high 0, medium 3, low 9. All patched, except the rename note, which is accepted.

| Verdict | Route | Finding | Outcome |
|---|---|---|---|
| medium | patch | `MigratingStore.Get` let a failed copy into the primary lose the legacy save | The copy is in try/catch with a warning; the legacy text is returned anyway. Tested. |
| medium | patch | A write the primary refused (quota, blocked) was lost; no way to tell which copy is newer | Writes and flushes fall back to PlayerPrefs with a warning. The save carries a `revision` (raised on every save), and Get returns the newer copy, copying it into the primary. Tested. |
| medium | patch | Caches kept other runs' entries; a full `BoundedCache` cleared everything, warmed entries included | `Rebuild` clears the four caches first; `BoundedCache` evicts its oldest entry. Tested. |
| low | patch | Delete did not flush PlayerPrefs | `MetaSave.Delete` flushes, and so does the legacy store in `MigratingStore.Delete`. |
| low | patch | The jslib Get could not tell a missing key from an error, so an old save could be migrated over a newer one | `EntropyStore_LastStatus`; `BrowserStore.Get` throws on an error; an unreadable primary never gets a copy, and a copy never replaces a newer revision. Tested. |
| low | patch | `MetaStorage.jslib.meta` had no `PluginImporter` block | A WebGL-only `PluginImporter` block; tested (not Any, not the editor, not desktop). |
| low | patch | Space (Confirm) could buy on the between-runs screen | Nothing is picked on opening, and Confirm acts only on a row picked with the arrows or pad. Tested with A and Space. |
| low | patch | Yes/no answers took the pad's Y, but between runs A confirms | Confirm (A, Space) answers too; the pad prompt shows "[A] confirm". Tested with A. |
| low | patch | The prompt device switched on the pointer's movement and Look; a joystick had no names | Only button or key controls switch it; a joystick shows the Gamepad bindings' names. Tested. |
| low | patch | `InputPrompts.Fill` rebuilt the string every OnGUI | Cached per template and device; cleared on a bindings or asset change. Tested. |
| low | patch | The `InitTestScene` assertion could fail on leftovers from killed runs | Dropped. |
| low | defer (accepted) | The rename hides the old `DefaultCompany/game` PlayerPrefs | Not readable across products; dev saves only. Noted as accepted in the Implementation Notes. |
