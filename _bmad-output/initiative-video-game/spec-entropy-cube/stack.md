# Stack and Build Conventions

## Stack
- Unity, 2D. Release is a **WebGL build on itch.io**, with a Windows desktop build as the fallback. Development and testing happen in the editor, and the first WebGL build is due by day 2.
- Use Unity's `PlayerPrefs` (or a JSON file through it) for saves, which works on WebGL and desktop. Start audio only after the first player input.
- Prefer built-in tools: Tilemap for screens, 2D physics, and a 2D render pipeline.
- The repository needs a Unity `.gitignore` and Git LFS for art and audio.
- The remote is the developer's personal GitHub account (`JCurl08/bmad-project`).

## Conventions
- **One seeded swap system, built first.** Face placement, screen modules, NPC parts, beak adaptations and the weather wand's starting phenomenon all come from one seeded random number source. A seed must reproduce a run exactly.
- **Screens** are about 24×15 tiles on the 960×600 canvas, with walls on every edge and one centred doorway per side. Non-puzzle screens are hand-built modules with tagged doorways and gate slots, which the shuffle arranges. Puzzle screens (islands, sliding ice, wind streams) are generated.
- **One "commitment" mechanic is reused** for the town-draft hostility, one-way gates (e.g. lowering down a cliff you can't climb back up) and rival-race lockouts (CAP-21).
- **Gates are data:** each gate type is bound to the item that opens it.
- **Puzzle generators validate solvability** with a solver and meet per-generator difficulty targets. The seed sweep checks every screen, fragment, locked edge and the real core portal are reachable, with no softlocks.
- **Any seed, not only the swept ones:** an EditMode test runs the sweep's checks, plus no template and no lock fallback, over seeds 1–500, and the game logs one error at reveal if a run's plans have any problem.
- **Verification** runs through `tools/verify.sh`: EditMode, the PlayMode tests in class batches, and the sweep with the determinism fixture check, one Unity process at a time. A test fails when a PlayMode test class is in no batch.
- **Debug tools from day 1:** an overlay with the face, screen and seed; a reroll-seed command; jump to any screen.
- **Upgrades change how the player traverses**, not just raw power.
