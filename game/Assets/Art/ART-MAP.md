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
- **NPC designs (4.3, generated in code, `NpcDesignArt`):** every race has one hand-drawn 16×16 design in the player's
  style (the D 112 hero): a big head over a short body, front-facing, Kenney's palette, a two-pixel outline in the
  player's own dark plum (added around each figure in code) and the feet on the tile's bottom edge. They are drawn at
  the player's size (one first-screen tile × `PeopleArtScale`) and white-tinted. The packs have no non-human people,
  so no Kenney tile is used; Tiny Town has no characters at all.
  - Townsfolk (4.18: no farm look; Town's residents are now folk of every race, so this design is only for the odd
    townsperson elsewhere, such as Sage Conkwell and Town captives): a hooded travelling cloak, a satchel strap, boots.
    Town's greeter, Hattie Hullo, has her own design (`NpcLook.Greeter`): an auburn bun with a flower, a cheery wave, a
    dress under a white apron. Finches: a round fluffy bird with big glinting eyes, an orange beak, a cream breast and
    blush. Chemistry folk: a spotted mushroom cap, a cream face, a white coat. Physics folk: a green, big-eyed,
    two-antenna alien in a jumpsuit. Shape people (4.18, Humpty-Dumpty style): an egg-shaped body with the face on it, a
    belt with a gold buckle, little stick arms with white gloves and little legs. Ground dinosaurs: a green, toothy,
    spiky-backed snout with a bandana. Avian dinosaurs (4.18): a blue feathered raptor seen side-on, a crest, a toothed
    beak, a dark-feathered wing, a long tail and long clawed gold legs (no longer a front-facing penguin shape).
  - The Mayor (the Townsfolk leader, 4.18): a royal purple top hat with a gold band, a white moustache, a dark suit with
    the gold chain of office and a red sash.
  - Each NPC wears one of three seeded accent tints (tunic, plumage, cap, suit, body, bandana or crest; from its salt,
    no random draw).
  - Leaders (`NpcFactory.ChooseLeader`): a gold crown (gold circlet in a wild white mane for the Physics folk; among long
    plumes for the avian dinosaurs) and royal purple trim with a gold hem, which no ordinary NPC wears; most also carry
    a staff (the Town's, the Chemistry folk's flask staff, the shape people's ruler, a bone club for the ground dinosaurs)
    or a ruff (finches).
- **Towns (4.5):** every face's centre screen is its race's town (`TownLayout`, built by `TownSquareView`): four buildings
  in a row along the north half, their doors on the lane, a paved plaza over the crossroads and the south half, and the
  statue by the crossroads.
  - The Town's cottages are Kenney Tiny Town tiles, two by two, each tile two units (the people's pixel size): a red roof
    (T 52, 54) over a wooden wall with its door (T 85, 75), or a grey slate roof (T 48, 50) over a stone wall with its door
    (T 90, 79). The market's cart is T 57. These keys are the Building category: decor, never pickup-looking.
  - Every other building is drawn in code (`TownArt`) in the NPC designs' style: Kenney's palette, the two-pixel plum
    outline added round the figure, point filtering, 8 pixels a unit (the people's). Finches: a treehouse (a plank hut on a
    trunk under a leafy crown) and a woven nest with a leafy dome. Chemistry folk: giant spotted mushroom huts, the lilac
    ones with a vent pipe whose bubbles rise and fade (`TownBubbles`). Physics folk: a slit-domed observatory with a telescope
    and a low banded dome with an antenna. Shape people: a tall square tower with a pointed roof and a slanted cube with a
    ball on top. Dinosaurs: a log lodge with a bone over the door under a steep green roof and a cabin with a stone
    chimney. The Town's market stall: a counter of goods under a red-and-white striped awning.
  - The statue is the race's leader design (`NpcDesignArt`) carved in stone: its brightness mapped onto a grey ramp with a
    touch of the region colour, the outline kept, on a stepped pedestal.
  - The plaza is a tiled paving of pale flagstones with soft joints, translucent over the region's floor. The marked door
    (story 4.6's way in) has a red woven rug in front and a lantern beside it.
  - A mended face's town (4.8) lights its statue: the stone tinted gold, a soft three-step gold halo behind it
    (`TownArt.RepairHalo`), and a pennant on a dark pole at each shoulder in the region's colour with a gold band
    (`TownArt.RepairBanner`). Art only, no colliders.
- **Fragments (4.8):** a fragment's chest keeps 4.7's shard icon (Kenney D101, tinted the region colour) and gains a die
  badge beside it (`TownArt.DieFace`): a cream die face, outlined in plum, showing the fragment's number as plum pips laid
  out like a real die. The HUD draws the same pip layouts (`TownArt.PipLayouts`) for its row of dice.
- **Interiors (4.6):** the room behind each town's marked door (`TownInterior`, built by `TownInteriorView`) is drawn in
  code (`InteriorArt`) in the same style: tiled floors and walls in the race's colours (Town planks and plaster, finch
  woven straw and twigs, mushroom lilac moss and a spotted stalk wall, alien metal plates and lit panels, shape
  chequered tiles, dinosaur flagstones and logs), and outlined furniture drawn at its rect's size: counters and shelves
  of wares, a throne on a dais with banners, pillars and a carpet, a bed (a nest bed for the finches), a hearth, a table
  and stools, rugs, and each race's keepsake (a barrel and crate, a seed perch, a bubbling pot, a telescope, a stack of
  shapes, a campfire). The exit has the marked door's rug and a pale arrow pointing out.
- **Lair rooms (4.13):** the room under each wrong core portal (`LairRoom`, built by `LairRoomView`) is drawn in code
  (`LairArt`) in the same style, as the Demon's: dark slate flagstones with a violet seam, dark brick walls, four barred
  cells along the back wall with straw on their floors, stone pillars with a violet capital, wall torches over the cell
  walls, and a pink swirl in the vestibule (the way back up) under the pale exit arrow. The bars are iron uprights with a
  pale highlight and their own plum edge (not a whole-figure outline, so the gaps between them stay see-through), drawn
  over the prisoners' bodies with their heads above.
- **Generated in code (not Kenney):** NPC parts (outlined race silhouettes, `NpcPartArt`, drawn only with
  `NpcFactory.PartArt` on, kept for epic-varied-runs; shape people have a round
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
| FloorTown | T 109 | plain pale stone: the white ruins town (4.18: under a warm white glaze, `FloorPattern.GlazeAlpha` 0.7, and a lighter 6 % checker, so it reads white, not blue-grey) |
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
| HouseRoofRedLeft | T 52 | red roof, left end: a Town cottage's roof (4.5) |
| HouseRoofRedRight | T 54 | red roof, right end |
| HouseWallWoodDoor | T 85 | wooden wall with a door at its left end: the cottage's door |
| HouseWallWoodRight | T 75 | wooden wall, right end |
| HouseRoofGreyLeft | T 48 | grey slate roof, left end: a Town stone cottage's roof |
| HouseRoofGreyRight | T 50 | grey slate roof, right end |
| HouseWallStoneDoor | T 90 | stone wall with a door at its left end: the stone cottage's door |
| HouseWallStoneRight | T 79 | stone wall, right end |
| MarketCart | T 57 | a cart of goods: parked by the Town market stall |
| Chest | D 89 | closed wooden chest: a roadblock's reward (4.7) |
| ChestOpen | D 91 | the chest with its lid up: taken (drawn faded) |
| Pedestal | D 54 | low stone stand under a face item behind a roadblock |
| RewardKey | T 117 | iron key over a chest: a key |
| RewardBoost | D 116 | blue tonic over a chest: a boost for this run |
| RewardFragment | D 101 | small square shard, tinted the face's colour, over a chest: a fragment slot |
| Mound | D 66 | a heap of brown dirt: a hidden spot any swing digs away |

Tile numbers are the files `Tiles/tile_NNNN.png` of each pack (row-major, 12 per row in `Tilemap/tilemap_packed.png`).

## Drawn in code (4.10): the ice screens

`IceArt` draws 16-pixel tiles (one unit each, point filtered), no Kenney tile: ice (pale blue, a glint and a seam at each tile edge), rock (a grey boulder on ice), pit (a dark hole with a pale jagged rim), grip (crunchy off-white snow with grit; also the landing lanes and the camp, whose centre has a small fire), goal pad (grip ringed in gold, under the reward chest) and snowbank (drifts with a blue shadow, round the grid).

## Drawn in code (4.11): the wind screens

`WindArt` draws 16-pixel tiles (one unit each, point filtered), no Kenney tile: sky (deep blue, some tiles with a soft cloud wisp; the tile under land shows the island's rocky underside, so islands float), the ring path (pale flagstones round the screen, where every doorway opens), island grass, a wall (a grey rock on grass, splitting twin islands), a stream (a lighter blue lane with a white chevron, turned to the stream's way per tile), a grip anchor (a small floating rock on the stream), the goal pad (grass ringed in gold, under the reward chest) and the cliffs (dark stone, round the grid). `WindScreen` animates white streaks flowing along every stream.

## Drawn in code (4.12): the island puzzle

`IslandScreen` adds simple shapes, no new catalog key: a dark plank (`PlankBridge` tinted dark wood) lights under a glow in Math's yellow once crossed, and every plank of the lit network flashes red for 0.45 s when a lit one is crossed again; a mended span keeps the gap-crosser*'s gold; an island whose network planks are all lit gets a yellow rim on every water edge. The reward roadblock is a dark round plinth under the chest with one small lamp per network plank round its edge (dark, then yellow as planks light) and three iron bars with a top bar over the chest; solving sends a chain of yellow dots from the last plank to the chest, then the bars go.

## Drawn in code (4.14): the temples

`TempleArt` draws every town's temple (the fourth building, east of the crossroads) in its race's style, 32 pixels square with the plum outline like every town building: Town's white marble portico under a purple pediment, the finches' leafy shrine on trunk pillars, the mushroom folk's capped hall of stalk columns, the aliens' blue dome ringed in gold, the shape people's stepped golden pyramid with a blue peak, the dinosaurs' hall cut into a rock mound with horns over the door. Each comes in three looks: sealed (a round seal in the region's colour, crossed by two chains, over a dark door), open (the doorway glows warm) and mended (a gem in the region's colour over the door). It also draws each face's gem (a cut stone with a gold band and a glint), a fragment shard and the altar (a stone table with a cushion in the region's colour). A temple room is the race's interior floor and wall tiles (`InteriorArt`) round a whole screen; its puzzle is drawn by the face's own runtime (islands, ice, wind, Physics booths) or by `TempleTargets` (seeds as brown ovals growing by step, stones as glowing diamonds fading by step, Town's tokens as squares in a face's colour with a die face of pips, each on a dark plinth). No catalog key was added.
