# To-do for you (from the overnight build session, 2026-10-05 → 06)

## Where things stand
Epic 1, "One complete run, town to boss", has every feature story built, reviewed and committed locally:

| Story | Commit |
|---|---|
| 1.1 Unity project and WebGL tracer | `c8fc729` |
| 1.2 Seeded cube model and debug tools | `f0cf94e` |
| 1.3 Screen modules and the town-exit shuffle | `2823f40` |
| 1.4 Items, gates, one-way gates and trial rooms | `47ca0b0` |
| 1.5 Lego NPCs and dialogue | `1b62328` |
| 1.6 Health, enemies and item-weak combat | `b3a2de4` |
| 1.7 Town face hub | `dc5e0f1` |
| 1.8 Biology face (Darwin) | `aa16c29` |
| 1.9 Chemistry face (Curie) | `2f0fd44` |
| 1.10 Physics face (Einstein) | `9178495` |
| 1.11 Core boss: Maxwell's Demon | `343a8cd` |
| 1.12 Run loop, meta currency and upgrades | `afea9cc` |
| 1.13 Refactor sweep | **uncommitted, needs PlayMode verification (section 0)** |
| **1.14 First itch.io release** | **needs you (below)** |

The last full run of the automated checks before the refactor: EditMode 182/182, PlayMode 112/112, seed sweep 50/50 with no softlocks.

Nothing has been looked at by a person yet. Everything was verified only by automated tests run in Unity from the command line.

## 0. Finish verifying story 1.13 (refactor sweep)
The refactor is on disk but **not committed**. EditMode passed (186/186) and the run-to-run determinism is unchanged, but Claude Code stopped the PlayMode run for low memory before it finished. With heavy apps closed, either ask me to "verify and finish 1.13", or run it yourself:
```
"C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe" -batchmode -nographics -projectPath C:/Projects/bmad-project/game -runTests -testPlatform PlayMode -testResults playmode.xml
```
If it fails and you'd rather not debug, `git stash` (or `git checkout -- game && git clean -fd game/Assets`) drops the refactor and puts you back at the 1.12 commit, which is fully verified.

## 1. Push to GitHub
BMad's build workflow never pushes on its own.
```
git -C C:/Projects/bmad-project push
```

## 2. Build WebGL (needed for 1.14)
Overnight, Claude Code stopped the IL2CPP WebGL build because the laptop ran low on memory, and didn't restart it.
1. Close other heavy apps (browsers, Teams, Docker).
2. Open `game/` in Unity Hub (6000.6.4f1).
3. Run **Tracer → Build WebGL**. The first build takes about 15 minutes; it writes `game/Builds/WebGL`.

## 3. Play it by eye (the most valuable thing you can do)
Open `Assets/Scenes/Cube.unity` and press Play. Controls come from the default Input System actions: WASD/arrows to move, attack (mouse/Enter), Interact (E), Previous/Next to cycle items. Debug keys:
- **F1** overlay, **F2** slot markers, **F3** spawn NPCs, **F4** toggle hostility for the nearest NPC's race, **F5** reroll the seed, **F6** jump to the next screen, **F7** spawn a test enemy

What to check:
- **Town:** busy, mixed, a greeter who welcomes you. Is it charming?
- **Each face:** find its item (beak, isotope, Mass Mitt) and use it on its gates and trial. Is each one fun and understandable without explanation?
- **Physics timed doors:** are they fair with keyboard movement? (They're tuned so the required boulder count is exact.)
- **Boss:** enter through a pink portal on any face. **Aim: 60–120 s for a steady player.** It was only tuned against a bot that faces 8 directions, so it may feel too hard or too easy for a real person. The tuning values are listed in the 1.11 plan's Implementation Notes.
- **Run loop:** die, or finish the boss. Do the between-runs screen, the Jumbles economy and the upgrades (Extra Heart, Thick Skin, Big Bonk, Zoomies) feel good? Reload, and check your progress is still there.
- **Feel:** are face crossings smooth (no black flash), and is the dialogue campy enough?

Write down anything that feels off. Fixes can go through `bmad-correct-course`, or just ask me.

## 4. Story 1.14: first itch.io release
After the WebGL build:
1. Zip the **contents** of `game/Builds/WebGL` (index.html, Build/, TemplateData/).
2. On itch.io: new project, Kind = **HTML**, visibility **Restricted** or Draft. Upload the zip and tick **"This file will be played in the browser"** (viewport 960×600).
3. In a fresh browser window: play a full run, buy an upgrade, reload the tab and check it's still applied.
4. Then tell me "finish 1.14" and I'll record it and close Epic 1.

## 5. Decisions I made on your behalf (worth a yes or no)
You told me to keep going, so I approved every build plan and made these calls:
- **Biology:** beak gate difficulty is the number of hits (1, or 2 when a face has 4+ beak gates).
- **Chemistry:** isotope gates on other faces always use the lead stage (glow and unstable expire too fast to cross faces), and the dispenser takes spent lead back.
- **Physics:** door timing is tuned per door, so the required boulder count is exact, with compensation for speed upgrades.
- **Town:** NPCs are respawned (identically) when you first leave town.
- **Boss:** the Demon tires over the fight (Landauer's principle as the joke).
- **Run loop:**
  - Earnings are banked on death too.
  - Thick Skin is a percentage reduction: ×0.85 per level, minimum 0.25 per hit.
  - The currency is "Jumbles".
- **Deferred** (see `deferred-work.md`): Town modules at 3×3 (for Epic 2), and rewards behind the non-rolled beak's gates (Epic 3, kept gear).

Each plan file has its full review history (the "Review Triage Log" section) in `epic-playable-cube/story-*-plan.md`.

## 6. Small things to know
- **Registry edit:** an agent deleted two test values it had accidentally written under the real save key, in the editor's saved settings (`HKCU\Software\Unity\UnityEditor\DefaultCompany\game`). Tests now use their own key.
- **Flaky test:** the boss "skill check" test depends on frame rate. Run PlayMode tests with `-nographics`; with graphics on, the bot can go over 120 s.
- **Environment:** `UV_SYSTEM_CERTS=true` and `UV_PYTHON=3.14` are set for your Windows account so BMad scripts work behind the corporate proxy.
