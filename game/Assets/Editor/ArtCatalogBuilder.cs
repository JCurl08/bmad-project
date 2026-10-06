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

        new Pick(ArtKey.FloorTown, Town, 43, "stone slabs in grass: the ruins town"),
        new Pick(ArtKey.FloorBiology, Town, 1, "grass with tufts: Darwin's green face"),
        new Pick(ArtKey.FloorChemistry, Dungeon, 37, "grey lab planks, tinted violet"),
        new Pick(ArtKey.FloorPhysics, Town, 109, "plain light stone, tinted blue"),
        new Pick(ArtKey.FloorMath, Dungeon, 48, "plain sand, tinted gold"),
        new Pick(ArtKey.FloorEarth, Dungeon, 49, "speckled sand, tinted teal"),
        new Pick(ArtKey.FloorDark, Dungeon, 0, "dark dungeon floor for unrevealed and sealed faces"),
        new Pick(ArtKey.ArenaFloor, Dungeon, 42, "slabbed sand, tinted warm and cool for the arena halves"),

        new Pick(ArtKey.GateGeneric, Dungeon, 77, "iron bars: a plain item gate"),
        new Pick(ArtKey.GateRock, Dungeon, 24, "rubble: a rock to break with the thick beak"),
        new Pick(ArtKey.GatePot, Dungeon, 82, "barrel: a pot to break with the thick beak"),
        new Pick(ArtKey.GateButtonDoor, Dungeon, 46, "wooden door: opened by a distant button"),
        new Pick(ArtKey.GateVine, Town, 5, "bush: the bramble a pollinated flower grows away"),
        new Pick(ArtKey.GateDarkRoom, Dungeon, 10, "pitch-black doorway (the arch's middle piece, so it tiles into one dark opening): needs a glowing isotope"),
        new Pick(ArtKey.GateCrackedWall, Dungeon, 28, "brick wall broken open in the middle: blast it with an unstable isotope"),
        new Pick(ArtKey.GateLeadDoor, Town, 125, "heavy steel door: opened by lead on its plate"),
        new Pick(ArtKey.GateTimedDoor, Town, 89, "wooden door in a pale stone frame: swings open, then shut again"),
        new Pick(ArtKey.VineBridge, Town, 2, "flowery grass: the grown vine bridge"),

        new Pick(ArtKey.ItemGeneric, Town, 107, "sack: an item with no art of its own"),
        new Pick(ArtKey.ItemThinBeak, Dungeon, 131, "long spear: the thin beak's long reach"),
        new Pick(ArtKey.ItemThickBeak, Dungeon, 117, "hammer: the thick beak breaks rocks and pots"),
        new Pick(ArtKey.ItemIsotope, Dungeon, 114, "green potion: the glowing isotope"),
        new Pick(ArtKey.ItemMassMitt, Dungeon, 74, "anvil: the closest thing to a heavy mitt (mass)"),
        new Pick(ArtKey.Jumbles, Town, 93, "gold coin: hidden Jumbles"),

        new Pick(ArtKey.EnemyGeneric, Dungeon, 109, "cyclops"),
        new Pick(ArtKey.EnemySeedWeevil, Dungeon, 122, "spider: a crawling seed weevil"),
        new Pick(ArtKey.EnemyPollenPuff, Dungeon, 121, "ghost, tinted yellow: a drifting pollen puff"),
        new Pick(ArtKey.EnemyFreeRadical, Dungeon, 108, "slime: a jittery free radical"),
        new Pick(ArtKey.EnemyRustMite, Dungeon, 110, "crab: a crusty rust mite"),
        new Pick(ArtKey.EnemyQuantumFlea, Dungeon, 120, "bat: a twitchy flea"),
        new Pick(ArtKey.EnemyStaticCling, Dungeon, 124, "grey rat: a clingy ball of static"),

        new Pick(ArtKey.Boulder, Dungeon, 56, "round rock"),
        new Pick(ArtKey.BeakButton, Town, 95, "target: peck it from afar"),
        new Pick(ArtKey.DoorSwitch, Dungeon, 32, "crystal floor switch: step on it"),
        new Pick(ArtKey.LeadPlate, Dungeon, 31, "pressure plate: holds lead"),
        new Pick(ArtKey.Flower, Town, 17, "sprout, tinted as a bud then a bloom"),
        new Pick(ArtKey.Dispenser, Dungeon, 55, "metal cabinet: the isotope dispenser"),
        new Pick(ArtKey.TrialLamp, Dungeon, 29, "flame banner: a lamp to light"),
        new Pick(ArtKey.GoalPad, Dungeon, 60, "corner brackets: a target zone for boulders"),

        new Pick(ArtKey.Portal, Town, 104, "blue well in an arch: the way into the core"),
        new Pick(ArtKey.Demon, Dungeon, 19, "carved demon face: Maxwell's Demon"),
        new Pick(ArtKey.Particle, Dungeon, 102, "round disc, tinted warm or cool"),

        new Pick(ArtKey.RuinPillar, Dungeon, 58, "stone column"),
        new Pick(ArtKey.RuinArch, Town, 114, "broken arch"),
        new Pick(ArtKey.RuinStone, Dungeon, 65, "standing stone"),
        new Pick(ArtKey.RuinTile, Dungeon, 61, "inlaid tile"),
        new Pick(ArtKey.RuinBroken, Dungeon, 64, "broken column"),
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
