# To-do for you (from the overnight build session, 2026-10-05)

Things only you can do: they need your eyes, your accounts or a decision. In rough priority order.

## 1. Push to GitHub
BMad's build workflow never pushes on its own. Everything is committed locally on `main`.
```
git -C C:/Projects/bmad-project push
```
The first push uploads a Git LFS file (the template's welcome image), and the LFS hooks are installed.

## 2. Story 1.1: upload the tracer to itch.io (the open part of its acceptance)
1. Zip the contents of `game/Builds/WebGL` (index.html, Build/, TemplateData/), not the folder itself.
2. On itch.io, create a project with Kind = **HTML** and visibility **Restricted** (or Draft).
3. Upload the zip and tick **"This file will be played in the browser"**. 960×600 is a good viewport size.
4. Check in a fresh browser window:
   - The page loads and the scene shows.
   - There is no sound until your first key press or click, then a short tone plays.
   - Space increments the "Saved counter"; reload the tab and the value is still there.
   - Walking right through the doorway snaps the camera to the second screen.

**Rebuild first.** The build in `game/Builds/WebGL` is from story 1.1 and doesn't include the cube scene. Overnight, Claude Code stopped the WebGL rebuild because the laptop ran low on memory (IL2CPP WebGL builds are heavy), and it isn't restarted automatically. Before uploading, close other heavy apps, open `game/` in Unity, and run **Tracer → Build WebGL**. The first build takes about 15 minutes; later ones are faster. It needs to succeed, because the story 1.2 and 1.3 plans list that WebGL build as one of their checks.

## 3. Review what was approved on your behalf
You told me to keep working while you were away, so I approved the build plans myself. Skim each one tomorrow:
- `epic-playable-cube/story-seeded-cube-model-and-debug-tools-plan.md` (story 1.2)
- `epic-playable-cube/story-screen-modules-and-the-town-exit-shuffle-plan.md` (story 1.3)
- `epic-playable-cube/story-items-gates-one-way-gates-and-trial-rooms-plan.md` (story 1.4)
- `epic-playable-cube/story-lego-npcs-and-dialogue-plan.md` (story 1.5)
- `epic-playable-cube/story-health-enemies-and-item-weak-combat-plan.md` (story 1.6)
- `epic-playable-cube/story-town-face-hub-plan.md` (story 1.7)
- `epic-playable-cube/story-biology-face-darwin-vertical-slice-plan.md` (story 1.8)
- `epic-playable-cube/story-chemistry-face-curie-plan.md` (story 1.9)
- `epic-playable-cube/story-physics-face-einstein-plan.md` (story 1.10)

Design calls made without you, worth a quick yes or no:
- **Biology:** the beak gate "difficulty" is the number of hits needed (1, or 2 when the face has 4+ beak gates).
- **Chemistry:** isotope gates on *other* faces always use the lead stage, because glow and unstable expire too fast to cross faces. The dispenser takes spent lead back.
- **Physics:** the walk time to timed doors is tuned per door, so the required boulder count is exact. Speed upgrades are compensated.
- **Town:** NPCs are respawned (with identical results) when you first leave town.

Each plan's "Review Triage Log" lists what the reviewer found and what was patched or rejected.

## 4. Try it in the Unity editor
Open `game/` in Unity Hub (6000.6.4f1). Open `Assets/Scenes/Cube.unity` and press Play. Debug keys:
- **F1** overlay, **F2** slot markers, **F3** spawn one NPC of each race, **F4** toggle hostility for the nearest NPC's race, **F5** reroll the seed, **F6** jump to the next screen
- **Interact** (E or Enter) talks to the nearest NPC

Things worth checking by eye, since only automated tests have looked at them:
- Do face crossings look smooth, with no black flash?
- Do the NPCs read as Lego-style mixes, and is the dialogue campy enough?
- Do the gates, alcoves and item pickups make sense spatially?
- **F7** spawns a test enemy. Hit it bare (about 8 hits) versus with its weakness item (2 hits).
- Play each face: get the beak / isotope / Mass Mitt and use it on its gates and trial. Do the timed doors feel fair with keyboard movement?
