using System;
using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>
    /// Every semantic thing the world draws with art. ArtCatalog maps each key to a sprite (a Kenney tile, see
    /// Assets/Art/ART-MAP.md); a key without a mapping falls back to its generated placeholder shape (FallbackShape).
    /// Values are explicit because the catalog asset stores them.
    /// </summary>
    public enum ArtKey
    {
        None = 0,

        Player = 1,

        Wall = 10,
        WallSealed = 11,
        ArenaWall = 12,

        FloorTown = 20,
        FloorBiology = 21,
        FloorChemistry = 22,
        FloorPhysics = 23,
        FloorMath = 24,
        FloorEarth = 25,
        FloorDark = 26,
        ArenaFloor = 27,

        GateGeneric = 30,
        GateRock = 31,
        GatePot = 32,
        GateButtonDoor = 33,
        GateVine = 34,
        GateDarkRoom = 35,
        GateCrackedWall = 36,
        GateLeadDoor = 37,
        GateTimedDoor = 38,
        VineBridge = 39,

        ItemGeneric = 50,
        ItemThinBeak = 51,
        ItemThickBeak = 52,
        ItemIsotope = 53,
        ItemMassMitt = 54,
        Jumbles = 55,

        EnemyGeneric = 60,
        EnemySeedWeevil = 61,
        EnemyPollenPuff = 62,
        EnemyFreeRadical = 63,
        EnemyRustMite = 64,
        EnemyQuantumFlea = 65,
        EnemyStaticCling = 66,

        Boulder = 70,
        BeakButton = 71,
        DoorSwitch = 72,
        LeadPlate = 73,
        Flower = 74,
        Dispenser = 75,
        TrialLamp = 76,
        GoalPad = 77,

        Portal = 80,
        Demon = 81,
        Particle = 82,

        RuinPillar = 90,
        RuinArch = 91,
        RuinStone = 92,
        RuinTile = 93,
        RuinBroken = 94,
    }

    /// <summary>What kind of thing a key draws. Two keys of different categories must never share a sprite.</summary>
    public enum ArtCategory
    {
        None, Player, Wall, Floor, Gate, Item, Currency, Enemy, Boulder, Switch, Plant, Prop, Portal, Demon, Particle, Ruin,
    }

    /// <summary>Pure facts about the art keys: category, fallback shape, tiling and the key for a theme, item or enemy.</summary>
    public static class ArtKeys
    {
        /// <summary>Every key except None.</summary>
        public static IEnumerable<ArtKey> All
        {
            get
            {
                foreach (ArtKey key in (ArtKey[])Enum.GetValues(typeof(ArtKey)))
                    if (key != ArtKey.None) yield return key;
            }
        }

        public static ArtCategory CategoryOf(ArtKey key)
        {
            int v = (int)key;
            if (key == ArtKey.None) return ArtCategory.None;
            if (key == ArtKey.Player) return ArtCategory.Player;
            if (v >= 10 && v < 20) return ArtCategory.Wall;
            if (v >= 20 && v < 30) return ArtCategory.Floor;
            if (v >= 30 && v < 50) return ArtCategory.Gate;
            if (key == ArtKey.Jumbles) return ArtCategory.Currency;
            if (v >= 50 && v < 60) return ArtCategory.Item;
            if (v >= 60 && v < 70) return ArtCategory.Enemy;
            switch (key)
            {
                case ArtKey.Boulder: return ArtCategory.Boulder;
                case ArtKey.BeakButton:
                case ArtKey.DoorSwitch:
                case ArtKey.LeadPlate:
                case ArtKey.GoalPad: return ArtCategory.Switch;
                case ArtKey.Flower: return ArtCategory.Plant;
                case ArtKey.Dispenser:
                case ArtKey.TrialLamp: return ArtCategory.Prop;
                case ArtKey.Portal: return ArtCategory.Portal;
                case ArtKey.Demon: return ArtCategory.Demon;
                case ArtKey.Particle: return ArtCategory.Particle;
            }
            return v >= 90 && v < 100 ? ArtCategory.Ruin : ArtCategory.None;
        }

        /// <summary>True for surfaces drawn by repeating the tile over their size (walls, floors, gates) instead of stretching it.</summary>
        public static bool IsTiled(ArtKey key)
        {
            ArtCategory category = CategoryOf(key);
            return category == ArtCategory.Wall || category == ArtCategory.Floor || category == ArtCategory.Gate;
        }

        /// <summary>The generated placeholder shape a key falls back to when it has no art.</summary>
        public static PartShape FallbackShape(ArtKey key)
        {
            switch (CategoryOf(key))
            {
                case ArtCategory.Player:
                case ArtCategory.Boulder:
                case ArtCategory.Particle:
                case ArtCategory.Portal: return PartShape.Circle;
                case ArtCategory.Item:
                case ArtCategory.Currency:
                case ArtCategory.Enemy:
                case ArtCategory.Plant: return PartShape.Diamond;
                case ArtCategory.Demon: return PartShape.Triangle;
                default: return PartShape.Square;
            }
        }

        /// <summary>The floor of a face theme (Town, the science faces).</summary>
        public static ArtKey Floor(Theme theme)
        {
            switch (theme)
            {
                case Theme.Town: return ArtKey.FloorTown;
                case Theme.Biology: return ArtKey.FloorBiology;
                case Theme.Chemistry: return ArtKey.FloorChemistry;
                case Theme.Physics: return ArtKey.FloorPhysics;
                case Theme.Math: return ArtKey.FloorMath;
                default: return ArtKey.FloorEarth;
            }
        }

        /// <summary>The art of an item, by its stable id (the beaks, the isotope, the Mass Mitt; anything else is generic).</summary>
        public static ArtKey Item(ItemDefinition item)
        {
            if (item == null) return ArtKey.ItemGeneric;
            switch (item.Id)
            {
                case BiologyBeaks.ThinId: return ArtKey.ItemThinBeak;
                case BiologyBeaks.ThickId: return ArtKey.ItemThickBeak;
                case ChemistryIsotope.Id: return ArtKey.ItemIsotope;
                case MassMittItem.Id: return ArtKey.ItemMassMitt;
                default: return ArtKey.ItemGeneric;
            }
        }

        public static ArtKey Enemy(BiologyEnemyKind kind) =>
            kind == BiologyEnemyKind.SeedWeevil ? ArtKey.EnemySeedWeevil : ArtKey.EnemyPollenPuff;

        public static ArtKey Enemy(ChemistryEnemyKind kind) =>
            kind == ChemistryEnemyKind.FreeRadical ? ArtKey.EnemyFreeRadical : ArtKey.EnemyRustMite;

        public static ArtKey Enemy(PhysicsEnemyKind kind) =>
            kind == PhysicsEnemyKind.QuantumFlea ? ArtKey.EnemyQuantumFlea : ArtKey.EnemyStaticCling;

        /// <summary>The floor tint of a theme: the green and the town floors keep their own colours, the rest take a soft theme tint.</summary>
        public static UnityEngine.Color FloorTint(Theme theme) =>
            theme == Theme.Town || theme == Theme.Biology
                ? UnityEngine.Color.white
                : UnityEngine.Color.Lerp(UnityEngine.Color.white, CubeWorld.ThemeColor(theme), 0.5f);
    }
}
