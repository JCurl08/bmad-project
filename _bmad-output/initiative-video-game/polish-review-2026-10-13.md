# Polish review (2026-10-13, game commit 0763d2d)

Four read-only reviews, merged and de-duplicated:
1. Player experience
2. Visuals (from the screenshot folders; some screenshots predate later fixes)
3. Balance and economy
4. Technical and WebGL readiness

Impact / effort uses H/M/L for impact and S/M/L for effort.

## Tier 1: before a public itch.io release
| # | Item | Impact / effort | Notes |
|---|---|---|---|
| 1 | **Save survives itch re-uploads** | H / M | WebGL PlayerPrefs live in IndexedDB under a path hashed from the build URL. Every upload gets a new path, so meta progress resets after each patch. Fix: a small jslib localStorage bridge in `Run/MetaSave.cs`. Also wrap `MetaSave.Save` in try/catch. |
| 2 | **Fresh WebGL build and smoke test** | H / S | The only build is from 5 October. Play one full run in Chrome and Firefox (Safari if possible) and time the hitches. Needs you. |
| 3 | **HUD scales with the screen** | H / M | Every OnGUI HUD uses fixed pixel sizes tuned for 960×600: CombatHud, CoreHud, DialogueBox, CubeMap, QuestLog and BetweenRuns. Text is tiny when fullscreen or on HiDPI screens. Fix: one shared GUI.matrix scale. |
| 4 | **Gamepad can finish the game** | H / S | Keyboard-only today: Q (quest log), Esc (close or skip), the 1–4 buy keys and Enter (continue). A pad player can't clear the tour's "press Q" stop or leave the shop. Make these input actions. |
| 5 | **Generation hitches** | H / M | The first step off Town runs the reveal synchronously. Temples take up to 345 ms cold, wind up to 331 ms, and wasm is likely 1.5–3× slower. Fix: pre-warm the caches over several frames while the player is in Town or on the between-runs screen. |
| 6 | **Release identity** | M / S | `productName: game`, `DefaultCompany`, version 1.0. Show the version and seed on the between-runs screen so bug reports can name a build. |
| 7 | **Abandon run** | M / M | In release, F10 is gated off, so a physics soft-lock (a boulder wedged in a corner) can only be escaped by reloading. Add an "Abandon run" option to the map or quest log. |
| 8 | **Smaller build** | M / S | 14 MB. Set stripping to High, remove unused packages (visual scripting, timeline, XR and others), drop Tracer.unity and the 14 InitTestScene files from the build. |
| 9 | **Esc vs fullscreen** | L / S | In the browser, Esc also exits fullscreen. Give the map, log and dialogue a second close key. |

## Tier 2: feel and juice
| # | Item | Impact / effort | Notes |
|---|---|---|---|
| 10 | **Audio** | H / M | The game is silent (Cube.unity has no AudioSource). Add generated SFX (swing, hit, hurt, pickup, chest, gate, plank light and snuff, dialogue blips) and one loop per face. The finch "song" temple needs actual notes. A title screen click can unlock browser audio. |
| 11 | **Title screen and premise beat** | H / S–M | Play, How to play, Options and Credits (`Art/CREDITS.md` exists but nothing shows it), plus a two-line intro: Primsworth tidied the cube, so mix it back. |
| 12 | **Pause / options menu** | H / M | Volume, text speed, UI scale, controls list, abandon run. Use P or Start, not Esc. |
| 13 | **Character animation** | H / M | Everything is a static sprite. Add a 2-frame walk bob or squash, a horizontal flip by facing, and an idle sway for NPCs. |
| 14 | **Hit feedback and transitions** | M / S–M | Hit-stop, knockback, a small shake on damage, blink while invulnerable. A short scroll or fade between screens, and a region-name banner on first entry to a face. |
| 15 | **Boss presentation** | H / M | The Demon is a one-tile square. Make it 2–3 tiles with an idle loop, put a named health or meter bar at the top, and give the arena floor some detail. |
| 16 | **Pixel density** | H / L | Kenney tiles are 16 px per unit, people and buildings 8 px per unit, NPC parts 32 px per unit. Pick one grid. This is the biggest art job. |
| 17 | **Face floors and walls** | M / M | Chemistry, Math and Earth floors read as flat colour blocks. Walls are the same grey stone everywhere. Doorways are plain black slits, while locked arches have their own chunky piece. Add per-face floor texture and wall trim. |
| 18 | **Readability touch-ups** | M / S | Shadows or rims on enemies (red on red, blue on blue). Bigger padlock icons. Mask the neighbouring-screen slivers at the screen edges. Remove the pale octagon on Earth screens (the old calm-eye ring) or give it an edge. Spread out crowded sprites and statues. |

## Tier 3: balance and economy
| # | Item | Impact / effort | Notes |
|---|---|---|---|
| 19 | **Boss with gems** | H / M | Untested, and probably trivial with all six gems (estimated under 20 s). Run bot fights with 0, 1 and 6 gems. Consider a "hold the meter for 3 s" win and a bonus of Jumbles\* for each unused gem. |
| 20 | **Weightlands gem vs the base fight** | H / S | Your call. The reviewer suggests ×2.5 / ×0.5 for the gem and an easier base fight instead (restore interval 0.2 → 0.4 s, tiring starts at 30 s rather than 45 s), aiming for 90–150 s for a human. |
| 21 | **Big Bonk is false** | M / S | Power never affects particle knock in the fight, but the shop text says it does. Scale knock by Power, capped. |
| 22 | **Economy runs dry** | H / M | About 350 Jumbles\* per full run against 1,200 to max everything, so everything is maxed after about 4 runs. Steepen costs, add sinks (the shopkeepers), or add new unlocks. Temples pay 0 while trials pay a third of a run's income. |
| 23 | **Die number should mean difficulty** | H / M | Physics doors, enemies, trials and temples ignore the die. Tier them, so Town's temple (first met, weakest gem) is easy and a face with die 6 is hard. |
| 24 | **Physics temple dash slack** | H / M | 1.04× the longest dash; the research band is 1.12–1.20. Start the door clock when the player leaves the switch. |
| 25 | **Early islands are trivial** | M / S | Random play solves them 100% of the time. Add a plank so they hold two odd islands. |
| 26 | **Too many ice screens** | M / S | Up to 3 in a run when Earth's temple is ice. Pick the temple type against the face's majority. |
| 27 | **Keys too plentiful; chest keys barely worth it** | M / S | 11 keys for about 5 keyed arches. A chest key nets +5 over cashing it in. |
| 28 | **Combat payoff** | M / M | Enemies drop nothing. Add 1–2 Jumbles\* or a heart, and scale them with the die. |
| 29 | **First run gets the fewest hints** | H / S | Hint density is Sparse on run 0. Invert it. |
| 30 | **The tour skips the core loop** | M / S | No combat stop and no summary of fragments → leader → temple → gem → boss. |

## Tier 4: longer term
- **Mid-run saves or checkpoints.** A reload loses the whole run.
- **Fast travel** between visited town statues, to cut backtracking.
- **Meta goals:** run stats, records, a lore collection.
- **Accessibility:**
  - region glyphs alongside colours (colour-blind players);
  - control rebinding;
  - typewriter text with a speed setting;
  - a "Saved" toast.
- **Housekeeping:**
  - strip `Debug.Log` calls in release;
  - cache the IceArt/WindArt pixel arrays;
  - an error overlay that shows the seed.
- **Content gaps from the storyboard:**
  - Primsworth has no lines;
  - no celebration lines;
  - the ice, wind and Greenwood\* screens have little rule text;
  - the greeting pools are never shown.

## Checked and fine
- **Debug features:** F-keys, the overlay and the Shift shortcuts are compiled out of release builds.
- **Tests:** 652 pass, plus the 50-seed sweep and the 500-seed robustness test.
- **Deferred work:** none left open.
