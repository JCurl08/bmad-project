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
- **Decor never looks collectible (1.24):** decor (ruins, the fossil mound, floors, hazards) never uses a tile that reads
  as a pickup: keys, coins, bags, chests, potions, flasks, gems, wands, tools or weapons (a test checks it).
- **Screen edges (1.25):** every screen's edges are walls (the `Wall` tile in the face's wall tint, one tile deep) with one
  centred doorway on each side. A doorway onto another face is framed by a stone gate arch (`DoorArch`, stretched over the
  doorway, one tile deep inside the wall band, with its lintel on the edge and its posts on the wall); a doorway within a face is a plain gap in the wall.
- **Island screens (1.25, 1.26):** Math's island screens are solid water (a generated flat blue) with the islands cut out
  of it, so each island is the face's own floor (`FloorMath`, its tint and texture) with a generated shoreline: a pale
  foam line on every edge that meets the water and a band of translucent wet sand inside it. `PlankBridge` planks join
  some of them, a shade dim until walked; a walked plank lights up under a soft green glow and a mended span under the
  gap-crosser*'s gold (generated overlays). A broken span shows the `GateGap` pit with the gap-crosser*'s diamond mark
  until it is mended. A lone doorway's island has a generated lookout sign (a post and a board whose arrow points back
  out of the doorway). The `Island` pad (D 66) is gone (1.26: the brown pads read as odd dots, not land).
- **Hazards:** the Earth hazards (wind, snow, freezing rain, crevices) are their own category, tiled over their area like
  floors, drawn translucent over the floor so the ground still reads.
- **Tints:** a tint separates variants (floor themes, enemy kinds, the arena halves, a gate's item colour), but every
  category differs by shape. No tile is used by two keys (a test checks it).
- **Region colours (Rubik's cube):** Town white, Biology green, Chemistry red, Physics blue, Math yellow, Earth &
  Atmosphere orange (`CubeWorld.ThemeColor`, also the map's colours). Floors take a per-tile tint of their region colour
  (`ArtKeys.FloorTint`: tints only darken a tile, so the grey panels carry blue, the sand red, gold and
  orange, and Town's pale stone stays white; a test checks each floor against its walls, gates and items); walls and alcoves take a soft tint of it, Town's pillars and ruin pieces
  the colours of the other regions.
- **Floor texture (1.24):** every region floor carries a light, fixed shading over its tiles (`FloorPattern`: soft
  patches a few tiles across and the odd tile picked out, at most 10 % white or black, less on the Weightlands*), drawn
  just above the floor. It is a hash of the theme, screen and tile, so it is the same every run and touches no RNG.
- **Generated in code (not Kenney):** NPC parts (outlined race silhouettes, `NpcPartArt`; shape people have a round
  face, a triangular body and rectangular legs, their circle, triangle and rhombus kinds shown by accessories; finches
  are drawn in full colour: a round fluffy body, a clear orange beak, big glinting eyes, blush and little orange feet), the water
  of an island screen and the Math trial's flag, and marks, pips, effects,
  the membrane and the Demon*'s door, the core arena's exit door, the wind arrows, rain drops and the calm eye's ring
  (`ArtCatalog.Shape`). The apple-bonked sage*'s apple has no tile and stays a red circle.

| Key | Tile | Why |
|---|---|---|
| Player | D 112 | green-tunic hero: the only green adventurer, unlike every enemy |
| Wall | D 40 | light stone bricks |
| WallSealed | D 14 | dark stone bricks for the edges into sealed faces |
| ArenaWall | T 126 | grey castle bricks around the core arena |
| DoorArch | T 78 | stone archway with an open middle: the gate where a screen's doorway leads onto another face (1.25; stretched, not tiled) |
| FloorTown | T 109 | plain pale stone: the white ruins town |
| FloorBiology | T 1 | grass with tufts, a little greener: the green Greenwood* |
| FloorChemistry | D 53 | plain sand, tinted deep red: the Ember Vale* |
| FloorPhysics | D 37 | fine metal deck plates, tinted blue: the Weightlands* (1.24: calmer than the stone panels T 76, whose dark notches and borders read as noise) |
| FloorMath | D 48 | plain sand, tinted gold-yellow: Bridgelands* |
| FloorEarth | D 49 | speckled sand, tinted orange: Wildlands* |
| FloorDark | D 0 | dark dungeon floor for unrevealed and sealed faces |
| ArenaFloor | D 42 | slabbed sand, tinted warm and cool for the arena halves |
| GateGeneric | D 77 | iron bars: a plain item gate |
| GateRock | D 24 | rubble: a rock to break with the thick beak (also the Biology trial's rocks) |
| GatePot | D 82 | barrel: a pot to break with the thick beak |
| GateButtonDoor | D 46 | wooden door: opened by a distant button |
| GateVine | T 5 | bush: the bramble a pollinated flower grows away |
| GateDarkRoom | D 10 | pitch-black doorway (the arch's middle piece, so it tiles into one dark opening): light it with the Glowstone* |
| GateCrackedWall | D 28 | brick wall broken open in the middle: blast it with the Wildstone* (also the Wildstone* trial's blocks) |
| GateLeadDoor | T 125 | heavy steel door: opened by weighing its plate down with the Leadstone* |
| GateTimedDoor | T 89 | wooden door in a pale stone frame (unlike the button door's dark frame): swings open, then shut again |
| VineBridge | T 2 | flowery grass: the grown vine bridge |
| PlankBridge | D 80 | planks on rails: a plank between two islands of a Math island screen, and the crossing the gap-crosser* lays (stretched, not tiled) |
| GateGap | D 12 | floor crumbled into pits: a gap nobody can step over (the gap-crosser* lays a crossing over it) |
| GateRockPillars | D 18 | a stone pillar standing in the way: the quake Stormcaller* knocks the pillars down (also the quake trial's pillars) |
| GateFireWall | D 41 | iron fire grate, tinted flame orange: a wall of fire the rain Stormcaller* douses (also the rain trial's fires) |
| GateWater | D 44 | a pool under a stone lip, tinted blue: water the snow Stormcaller* freezes into a crossing (also the snow trial's puddles) |
| GateDebris | T 106 | a fallen log: a debris pile the tornado Stormcaller* blows away (also the tornado trial's heaps) |
| ItemGeneric | T 107 | sack: an item with no art of its own |
| ItemThinBeak | D 131 | long spear: the thin beak's long reach |
| ItemThickBeak | D 117 | hammer: the thick beak breaks rocks and pots |
| ItemGlowstone | D 114 | green potion: the Glowstone* |
| ItemMassMitt | D 74 | anvil: the closest thing to a heavy mitt (mass); the packs have no gauntlet |
| Jumbles | T 93 | gold coin: hidden Jumbles |
| ItemWildstone | D 115 | red potion: the Wildstone* |
| ItemLeadstone | D 113 | grey flask: the Leadstone* |
| ItemGapCrosser | D 68 | short ladder: the gap-crosser* lays it over a gap |
| ItemWandQuake | D 127 | wand with a red tip: the quake Stormcaller* |
| ItemWandRain | D 128 | wand with a blue tip: the rain Stormcaller* |
| ItemWandSnow | D 125 | wand with a white tip: the snow Stormcaller* |
| ItemWandTornado | D 129 | staff with a violet gem: the tornado Stormcaller* |
| EnemyGeneric | D 109 | cyclops |
| EnemySeedWeevil | D 122 | spider: a crawling seed weevil |
| EnemyPollenPuff | D 121 | ghost, tinted yellow: a drifting pollen puff |
| EnemyFreeRadical | D 108 | slime: a jittery free radical |
| EnemyRustMite | D 110 | crab: a crusty rust mite |
| EnemyQuantumFlea | D 120 | bat: a twitchy flea |
| EnemyStaticCling | D 124 | grey rat: a clingy ball of static |
| EnemyDeadEnd | D 111 | hooded figure with red eyes: a stubborn dead end |
| EnemyLooseEnd | T 105 | bomb with a fizzing fuse: a loose end |
| EnemySquall | D 84 | grey-bearded figure in a swirling robe, tinted pale blue: a grumpy squall |
| EnemyRumbler | D 123 | curled orange creature: a rumbling rock grub |
| Boulder | D 56 | round rock |
| BeakButton | T 95 | target: peck it from afar (also the Biology trial's buttons) |
| DoorSwitch | D 32 | crystal floor switch: step on it |
| LeadPlate | D 31 | pressure plate: the Leadstone* weighs it down (also the Leadstone* trial's plates) |
| Flower | T 17 | sprout, tinted as a bud then a bloom (also the Biology trial's flowers) |
| TrialLamp | D 29 | flame banner: a lamp to light (the Glowstone* trial's lamps) |
| GoalPad | D 60 | corner brackets: a target zone for boulders |
| Fossil | T 92 | a mound of dug-up earth where a quake crevice laid the ground bare (1.24: was the key tile, which read as a pickup) |
| WindBand | D 62 | white diagonal streaks: a wind band (generated arrows show which way it blows) |
| SnowPatch | T 121 | pale speckled ground, tinted white: a snow patch you slide on |
| FreezingRain | T 77 | grey-blue slush, tinted icy blue: freezing rain that slows you (generated drops fall over it) |
| Crevice | D 2 | a dark drop under a stone lip: a quake crevice you go round |
| Portal | T 104 | blue well in an arch: the way into the core |
| Demon | D 19 | carved demon face: the Demon* |
| Particle | D 102 | round disc, tinted warm or cool |
| RuinPillar | D 58 | stone column |
| RuinArch | T 114 | broken arch |
| RuinStone | D 65 | standing stone |
| RuinTile | T 122 | speckled flagstone slab (1.24: was a framed crate) |
| RuinBroken | D 59 | broken column stub (1.24: was a mallet-shaped post) |

Tile numbers are the files `Tiles/tile_NNNN.png` of each pack (row-major, 12 per row in `Tilemap/tilemap_packed.png`).
