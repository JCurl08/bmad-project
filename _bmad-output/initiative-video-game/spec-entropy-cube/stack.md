# Stack and Build Conventions

## Stack
- Unity, 2D. Release is a **WebGL build on itch.io**, with a Windows desktop build as the fallback. Development and testing happen in the editor, and the first WebGL build is due by day 2.
- Use Unity's `PlayerPrefs` (or a JSON file through it) for saves, which works on WebGL and desktop. Start audio only after the first player input.
- Prefer built-in tools: Tilemap for screens, 2D physics, and a 2D render pipeline.
- The repository needs a Unity `.gitignore` and Git LFS for art and audio.
- The remote is the developer's personal GitHub account (`JCurl08/bmad-project`).

## Conventions
- **One seeded swap system, built first.** Face placement, screen modules, NPC parts, beak adaptations and the weather wand's starting phenomenon all come from one seeded random number source. A seed must reproduce a run exactly.
- **Screen modules** are hand-built templates with tagged entry/exit points and gate slots. The shuffle arranges modules; it doesn't generate rooms.
- **One "commitment" mechanic is reused** for the town-draft hostility, one-way gates (e.g. lowering down a cliff you can't climb back up) and rival-race lockouts (CAP-21).
- **Gates are data:** each gate type is bound to the item that opens it.
- **Puzzle generators validate solvability**, for example a check that each bridge layout is solvable. A seed sweep in the polish window checks for softlocks.
- **Debug tools from day 1:** an overlay with the face, screen and seed; a reroll-seed command; jump to any screen.
- **Upgrades change how the player traverses**, not just raw power.
