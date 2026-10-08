using System.Collections.Generic;
using System.Linq;
using Game.Cube;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Cube > Build Art Catalog: reimports the Kenney packs as pixel art (ArtImportSettings), then writes the ArtCatalog asset
/// (Assets/Resources/ArtCatalog.asset) mapping every ArtKey to its chosen tile, and the white hit-flash material. The
/// mapping and the reason for each pick are in Map below and in Assets/Art/ART-MAP.md. Also works from the command line
/// via -executeMethod ArtCatalogBuilder.Build.
/// </summary>
public static class ArtCatalogBuilder
{
    public const string CatalogPath = "Assets/Resources/" + ArtCatalog.ResourcePath + ".asset";
    public const string FlashMaterialPath = "Assets/Art/Materials/HitFlash.mat";

    private const string Dungeon = "Tiny Dungeon";
    private const string Town = "Tiny Town";

    /// <summary>One pick: the key, the pack, the tile number and why (ART-MAP.md says the same).</summary>
    public readonly struct Pick
    {
        public readonly ArtKey Key;
        public readonly string Pack;
        public readonly int Tile;
        public readonly string Why;

        public Pick(ArtKey key, string pack, int tile, string why)
        {
            Key = key;
            Pack = pack;
            Tile = tile;
            Why = why;
        }

        public string Path => $"{ArtImportSettings.Root}/{Pack}/Tiles/tile_{Tile:0000}.png";
        public string Name => $"{Pack}/tile_{Tile:0000}";
    }

    /// <summary>The mapping: every ArtKey to one Kenney tile. No tile is used twice.</summary>
    public static readonly Pick[] Map =
    {
        new Pick(ArtKey.Player, Dungeon, 112, "green-tunic hero: the only green adventurer, unlike every enemy"),

        new Pick(ArtKey.Wall, Dungeon, 40, "light stone bricks"),
        new Pick(ArtKey.WallSealed, Dungeon, 14, "dark stone bricks for the edges into sealed faces"),
        new Pick(ArtKey.ArenaWall, Town, 126, "grey castle bricks around the core arena"),
        new Pick(ArtKey.DoorArch, Town, 78, "stone archway with an open middle: the gate where a screen's doorway leads onto another face (stretched over the doorway, not tiled)"),

        new Pick(ArtKey.FloorTown, Town, 109, "plain pale stone: the white ruins town"),
        new Pick(ArtKey.FloorBiology, Town, 1, "grass with tufts, a little greener: the green Greenwood*"),
        new Pick(ArtKey.FloorChemistry, Dungeon, 53, "plain sand, tinted deep red: the Ember Vale*"),
        new Pick(ArtKey.FloorPhysics, Dungeon, 37, "fine metal deck plates, tinted blue: the Weightlands* (calmer than the old stone panels)"),
        new Pick(ArtKey.FloorMath, Dungeon, 48, "plain sand, tinted gold-yellow: Bridgelands*"),
        new Pick(ArtKey.FloorEarth, Dungeon, 49, "speckled sand, tinted orange: Wildlands*"),
        new Pick(ArtKey.FloorDark, Dungeon, 0, "dark dungeon floor for unrevealed and sealed faces"),
        new Pick(ArtKey.ArenaFloor, Dungeon, 42, "slabbed sand, tinted warm and cool for the arena halves"),

        new Pick(ArtKey.GateGeneric, Dungeon, 77, "iron bars: a plain item gate"),
        new Pick(ArtKey.GateRock, Dungeon, 24, "rubble: a rock to break with the thick beak"),
        new Pick(ArtKey.GatePot, Dungeon, 82, "barrel: a pot to break with the thick beak"),
        new Pick(ArtKey.GateButtonDoor, Dungeon, 46, "wooden door: opened by a distant button"),
        new Pick(ArtKey.GateVine, Town, 5, "bush: the bramble a pollinated flower grows away"),
        new Pick(ArtKey.GateDarkRoom, Dungeon, 10, "pitch-black doorway (the arch's middle piece, so it tiles into one dark opening): light it with the Glowstone*"),
        new Pick(ArtKey.GateCrackedWall, Dungeon, 28, "brick wall broken open in the middle: blast it with the Wildstone*"),
        new Pick(ArtKey.GateLeadDoor, Town, 125, "heavy steel door: opened by weighing its plate down with the Leadstone*"),
        new Pick(ArtKey.GateTimedDoor, Town, 89, "wooden door in a pale stone frame: swings open, then shut again"),
        new Pick(ArtKey.VineBridge, Town, 2, "flowery grass: the grown vine bridge"),
        new Pick(ArtKey.PlankBridge, Dungeon, 80, "planks on rails: a plank between islands, and the crossing the gap-crosser* lays"),
        new Pick(ArtKey.GateGap, Dungeon, 12, "floor crumbled into pits: a gap nobody can step over"),
        new Pick(ArtKey.GateRockPillars, Dungeon, 18, "a stone pillar standing in the way: the quake Stormcaller* knocks the pillars down"),
        new Pick(ArtKey.GateFireWall, Dungeon, 41, "iron fire grate, tinted flame orange: a wall of fire the rain Stormcaller* douses"),
        new Pick(ArtKey.GateWater, Dungeon, 44, "a pool under a stone lip, tinted blue: water the snow Stormcaller* freezes into a crossing"),
        new Pick(ArtKey.GateDebris, Town, 106, "a fallen log: a debris pile the tornado Stormcaller* blows away"),

        new Pick(ArtKey.ItemGeneric, Town, 107, "sack: an item with no art of its own"),
        new Pick(ArtKey.ItemThinBeak, Dungeon, 131, "long spear: the thin beak's long reach"),
        new Pick(ArtKey.ItemThickBeak, Dungeon, 117, "hammer: the thick beak breaks rocks and pots"),
        new Pick(ArtKey.ItemGlowstone, Dungeon, 114, "green potion: the Glowstone*"),
        new Pick(ArtKey.ItemMassMitt, Dungeon, 74, "anvil: the closest thing to a heavy mitt (mass)"),
        new Pick(ArtKey.Jumbles, Town, 93, "gold coin: hidden Jumbles"),
        new Pick(ArtKey.ItemWildstone, Dungeon, 115, "red potion: the Wildstone*"),
        new Pick(ArtKey.ItemLeadstone, Dungeon, 113, "grey flask: the Leadstone*"),
        new Pick(ArtKey.ItemGapCrosser, Dungeon, 68, "short ladder: the gap-crosser* lays it over a gap"),
        new Pick(ArtKey.ItemWandQuake, Dungeon, 127, "wand with a red tip: the quake Stormcaller*"),
        new Pick(ArtKey.ItemWandRain, Dungeon, 128, "wand with a blue tip: the rain Stormcaller*"),
        new Pick(ArtKey.ItemWandSnow, Dungeon, 125, "wand with a white tip: the snow Stormcaller*"),
        new Pick(ArtKey.ItemWandTornado, Dungeon, 129, "staff with a violet gem: the tornado Stormcaller*"),

        new Pick(ArtKey.EnemyGeneric, Dungeon, 109, "cyclops"),
        new Pick(ArtKey.EnemySeedWeevil, Dungeon, 122, "spider: a crawling seed weevil"),
        new Pick(ArtKey.EnemyPollenPuff, Dungeon, 121, "ghost, tinted yellow: a drifting pollen puff"),
        new Pick(ArtKey.EnemyFreeRadical, Dungeon, 108, "slime: a jittery free radical"),
        new Pick(ArtKey.EnemyRustMite, Dungeon, 110, "crab: a crusty rust mite"),
        new Pick(ArtKey.EnemyQuantumFlea, Dungeon, 120, "bat: a twitchy flea"),
        new Pick(ArtKey.EnemyStaticCling, Dungeon, 124, "grey rat: a clingy ball of static"),
        new Pick(ArtKey.EnemyDeadEnd, Dungeon, 111, "hooded figure with red eyes: a stubborn dead end"),
        new Pick(ArtKey.EnemyLooseEnd, Town, 105, "bomb with a fizzing fuse: a loose end"),
        new Pick(ArtKey.EnemySquall, Dungeon, 84, "grey-bearded figure in a swirling robe, tinted pale blue: a grumpy squall"),
        new Pick(ArtKey.EnemyRumbler, Dungeon, 123, "curled orange creature: a rumbling rock grub"),

        new Pick(ArtKey.Boulder, Dungeon, 56, "round rock"),
        new Pick(ArtKey.BeakButton, Town, 95, "target: peck it from afar"),
        new Pick(ArtKey.DoorSwitch, Dungeon, 32, "crystal floor switch: step on it"),
        new Pick(ArtKey.LeadPlate, Dungeon, 31, "pressure plate: the Leadstone* weighs it down"),
        new Pick(ArtKey.Flower, Town, 17, "sprout, tinted as a bud then a bloom"),
        new Pick(ArtKey.TrialLamp, Dungeon, 29, "flame banner: a lamp to light"),
        new Pick(ArtKey.GoalPad, Dungeon, 60, "corner brackets: a target zone for boulders"),
        new Pick(ArtKey.Fossil, Town, 92, "a mound of dug-up earth: where a quake crevice laid the ground bare (not the key tile, which read as a pickup)"),

        new Pick(ArtKey.WindBand, Dungeon, 62, "white diagonal streaks: a wind band (arrows show which way it blows)"),
        new Pick(ArtKey.SnowPatch, Town, 121, "pale speckled ground, tinted white: a snow patch you slide on"),
        new Pick(ArtKey.FreezingRain, Town, 77, "grey-blue slush, tinted icy blue: freezing rain that slows you"),
        new Pick(ArtKey.Crevice, Dungeon, 2, "a dark drop under a stone lip: a quake crevice you go round"),

        new Pick(ArtKey.Portal, Town, 104, "blue well in an arch: the way into the core"),
        new Pick(ArtKey.Demon, Dungeon, 19, "carved demon face: the Demon*"),
        new Pick(ArtKey.Particle, Dungeon, 102, "round disc, tinted warm or cool"),

        new Pick(ArtKey.RuinPillar, Dungeon, 58, "stone column"),
        new Pick(ArtKey.RuinArch, Town, 114, "broken arch"),
        new Pick(ArtKey.RuinStone, Dungeon, 65, "standing stone"),
        new Pick(ArtKey.RuinTile, Town, 122, "speckled flagstone slab (not the framed crate, which read as a pickup)"),
        new Pick(ArtKey.RuinBroken, Dungeon, 59, "broken column stub (not the mallet-shaped post, which read as a tool)"),

        new Pick(ArtKey.HouseRoofRedLeft, Town, 52, "red roof, left end: a Town cottage's roof (4.5)"),
        new Pick(ArtKey.HouseRoofRedRight, Town, 54, "red roof, right end"),
        new Pick(ArtKey.HouseWallWoodDoor, Town, 85, "wooden wall with a door at its left end: the cottage's door"),
        new Pick(ArtKey.HouseWallWoodRight, Town, 75, "wooden wall, right end"),
        new Pick(ArtKey.HouseRoofGreyLeft, Town, 48, "grey slate roof, left end: a Town stone cottage's roof"),
        new Pick(ArtKey.HouseRoofGreyRight, Town, 50, "grey slate roof, right end"),
        new Pick(ArtKey.HouseWallStoneDoor, Town, 90, "stone wall with a door at its left end: the stone cottage's door"),
        new Pick(ArtKey.HouseWallStoneRight, Town, 79, "stone wall, right end"),
        new Pick(ArtKey.MarketCart, Town, 57, "a cart of goods: parked by the Town market stall"),

        new Pick(ArtKey.Chest, Dungeon, 89, "closed wooden chest: a roadblock's reward (4.7)"),
        new Pick(ArtKey.ChestOpen, Dungeon, 91, "the chest with its lid up: taken"),
        new Pick(ArtKey.Pedestal, Dungeon, 54, "low stone stand under a face item behind a roadblock"),
        new Pick(ArtKey.RewardKey, Town, 117, "iron key over a chest: a key"),
        new Pick(ArtKey.RewardBoost, Dungeon, 116, "blue tonic over a chest: a boost for this run"),
        new Pick(ArtKey.RewardFragment, Dungeon, 101, "small square shard, tinted the face's colour, over a chest: a fragment slot"),
        new Pick(ArtKey.Mound, Dungeon, 66, "a heap of brown dirt: a hidden spot to dig away with any swing"),
    };

    [MenuItem("Cube/Build Art Catalog")]
    public static void Build()
    {
        ArtImportSettings.Apply();

        var entries = new List<ArtCatalog.Entry>();
        foreach (Pick pick in Map)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pick.Path);
            if (sprite == null)
            {
                Debug.LogError($"ArtCatalogBuilder: no sprite at {pick.Path} for {pick.Key}.");
                continue;
            }
            entries.Add(new ArtCatalog.Entry { Key = pick.Key, Sprite = sprite, Tile = pick.Name });
        }

        Material flash = LoadOrCreateFlashMaterial();

        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        var catalog = AssetDatabase.LoadAssetAtPath<ArtCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<ArtCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        catalog.Set(entries, flash);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        ArtCatalog.Use(catalog);

        List<ArtKey> unmapped = ArtKeys.All.Where(k => entries.All(e => e.Key != k)).ToList();
        string missing = unmapped.Count == 0 ? "" : $"; no art for {string.Join(", ", unmapped)}";
        Debug.Log($"Cube: built art catalog at {CatalogPath} ({entries.Count} keys{missing}).");
    }

    /// <summary>Loads the catalog asset, building it first if it does not exist.</summary>
    public static ArtCatalog LoadOrBuild()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<ArtCatalog>(CatalogPath);
        if (catalog != null) return catalog;
        Build();
        return AssetDatabase.LoadAssetAtPath<ArtCatalog>(CatalogPath);
    }

    private static Material LoadOrCreateFlashMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(FlashMaterialPath);
        if (material != null) return material;
        Shader shader = Shader.Find(HitFlash.ShaderName);
        if (shader == null)
        {
            Debug.LogError($"ArtCatalogBuilder: shader {HitFlash.ShaderName} not found; hits will flash by tint.");
            return null;
        }
        if (!AssetDatabase.IsValidFolder("Assets/Art/Materials")) AssetDatabase.CreateFolder("Assets/Art", "Materials");
        material = new Material(shader) { name = "HitFlash" };
        AssetDatabase.CreateAsset(material, FlashMaterialPath);
        return material;
    }
}
