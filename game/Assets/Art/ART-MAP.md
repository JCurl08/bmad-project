# Art map

Every sprite in the game comes from one place: the `ArtCatalog` (`Assets/Resources/ArtCatalog.asset`, built by
**Cube > Build Art Catalog**, `Editor/ArtCatalogBuilder.cs`). It maps a semantic `ArtKey` to one tile of the Kenney
**Tiny Dungeon** (D) or **Tiny Town** (T) pack (CC0, see `CREDITS.md`). A key with no mapping logs once and falls back to
its generated placeholder shape, so nothing renders invisible.

- **Import:** `Editor/ArtImportSettings.cs` imports everything under `Assets/Art/Kenney/` as pixel art: single sprites,
  16 pixels per unit (one tile is one world unit), point filter, uncompressed, no mipmaps, full-rect meshes.
- **Tiling:** walls, floors and gates repeat their tile over their size (one tile per world unit) instead of stretching
  it. A gate's art is one tile deep, set back into its alcove so its lane-side edge sits on the unchanged collider.
- **Gates vs ruins:** no gate uses a tile of the ruin families (the Town arch pieces T 111-114, the Dungeon columns
  D 57-59), so a solid gate never reads as a walkable ruin (a test checks it).
- **Tints:** a tint separates variants (floor themes, enemy kinds, the arena halves, a gate's item colour), but every
  category differs by shape. No tile is used by two keys (a test checks it).
- **Generated in code (not Kenney):** NPC parts (outlined race silhouettes, `NpcPartArt`), and marks, pips, effects,
  the membrane and the Demon's door (`ArtCatalog.Shape`). Newton's apple has no tile and stays a red circle.

| Key | Tile | Why |
|---|---|---|
| Player | D 112 | green-tunic hero: the only green adventurer, unlike every enemy |
| Wall | D 40 | light stone bricks |
| WallSealed | D 14 | dark stone bricks for the edges into sealed faces |
| ArenaWall | T 126 | grey castle bricks around the core arena |
| FloorTown | T 43 | stone slabs in grass: the ruins town |
| FloorBiology | T 1 | grass with tufts: Darwin's green face |
| FloorChemistry | D 37 | grey lab planks, tinted violet |
| FloorPhysics | T 109 | plain light stone, tinted blue |
| FloorMath | D 48 | plain sand, tinted gold |
| FloorEarth | D 49 | speckled sand, tinted teal |
| FloorDark | D 0 | dark dungeon floor for unrevealed and sealed faces |
| ArenaFloor | D 42 | slabbed sand, tinted warm and cool for the arena halves |
| GateGeneric | D 77 | iron bars: a plain item gate |
| GateRock | D 24 | rubble: a rock to break with the thick beak (also the Biology trial's rocks) |
| GatePot | D 82 | barrel: a pot to break with the thick beak |
| GateButtonDoor | D 46 | wooden door: opened by a distant button |
| GateVine | T 5 | bush: the bramble a pollinated flower grows away |
| GateDarkRoom | D 10 | pitch-black doorway (the arch's middle piece, so it tiles into one dark opening): needs a glowing isotope |
| GateCrackedWall | D 28 | brick wall broken open in the middle: blast it with an unstable isotope (also the Chemistry trial's walls) |
| GateLeadDoor | T 125 | heavy steel door: opened by lead on its plate |
| GateTimedDoor | T 89 | wooden door in a pale stone frame (unlike the button door's dark frame): swings open, then shut again |
| VineBridge | T 2 | flowery grass: the grown vine bridge |
| ItemGeneric | T 107 | sack: an item with no art of its own |
| ItemThinBeak | D 131 | long spear: the thin beak's long reach |
| ItemThickBeak | D 117 | hammer: the thick beak breaks rocks and pots |
| ItemIsotope | D 114 | green potion: the glowing isotope |
| ItemMassMitt | D 74 | anvil: the closest thing to a heavy mitt (mass); the packs have no gauntlet |
| Jumbles | T 93 | gold coin: hidden Jumbles |
| EnemyGeneric | D 109 | cyclops |
| EnemySeedWeevil | D 122 | spider: a crawling seed weevil |
| EnemyPollenPuff | D 121 | ghost, tinted yellow: a drifting pollen puff |
| EnemyFreeRadical | D 108 | slime: a jittery free radical |
| EnemyRustMite | D 110 | crab: a crusty rust mite |
| EnemyQuantumFlea | D 120 | bat: a twitchy flea |
| EnemyStaticCling | D 124 | grey rat: a clingy ball of static |
| Boulder | D 56 | round rock |
| BeakButton | T 95 | target: peck it from afar (also the Biology trial's buttons) |
| DoorSwitch | D 32 | crystal floor switch: step on it |
| LeadPlate | D 31 | pressure plate: holds lead |
| Flower | T 17 | sprout, tinted as a bud then a bloom (also the Biology trial's flowers) |
| Dispenser | D 55 | metal cabinet: the isotope dispenser |
| TrialLamp | D 29 | flame banner: a lamp to light |
| GoalPad | D 60 | corner brackets: a target zone for boulders |
| Portal | T 104 | blue well in an arch: the way into the core |
| Demon | D 19 | carved demon face: Maxwell's Demon |
| Particle | D 102 | round disc, tinted warm or cool |
| RuinPillar | D 58 | stone column |
| RuinArch | T 114 | broken arch |
| RuinStone | D 65 | standing stone |
| RuinTile | D 61 | inlaid tile |
| RuinBroken | D 64 | broken column |

Tile numbers are the files `Tiles/tile_NNNN.png` of each pack (row-major, 12 per row in `Tilemap/tilemap_packed.png`).
