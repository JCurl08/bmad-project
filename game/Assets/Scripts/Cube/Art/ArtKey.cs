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
        DoorArch = 13,

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
        PlankBridge = 40,
        GateGap = 41,
        GateRockPillars = 43,
        GateFireWall = 44,
        GateWater = 45,
        GateDebris = 46,

        ItemGeneric = 50,
        ItemThinBeak = 51,
        ItemThickBeak = 52,
        ItemGlowstone = 53,
        ItemMassMitt = 54,
        Jumbles = 55,
        ItemWildstone = 56,
        ItemLeadstone = 57,
        ItemGapCrosser = 58,

        EnemyGeneric = 60,
        EnemySeedWeevil = 61,
        EnemyPollenPuff = 62,
        EnemyFreeRadical = 63,
        EnemyRustMite = 64,
        EnemyQuantumFlea = 65,
        EnemyStaticCling = 66,
        EnemyDeadEnd = 67,
        EnemyLooseEnd = 68,
        EnemySquall = 69,

        Boulder = 70,
        BeakButton = 71,
        DoorSwitch = 72,
        LeadPlate = 73,
        Flower = 74,
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

        // 100 and 101 were the wind band and the snow patch (hazards replaced by 4.10-4.11's puzzle screens); never reused.
        FreezingRain = 102,
        Crevice = 103,
        Fossil = 104,

        ItemWandQuake = 110,
        ItemWandRain = 111,
        ItemWandSnow = 112,
        ItemWandTornado = 113,
        EnemyRumbler = 114,

        // Town cottages (4.5): Kenney Tiny Town roof, wall and door tiles, laid two by two.
        HouseRoofRedLeft = 120,
        HouseRoofRedRight = 121,
        HouseWallWoodDoor = 122,
        HouseWallWoodRight = 123,
        HouseRoofGreyLeft = 124,
        HouseRoofGreyRight = 125,
        HouseWallStoneDoor = 126,
        HouseWallStoneRight = 127,
        MarketCart = 128,

        // Roadblock rewards (4.7): the chest, the item pedestal, the icons over a chest and the hidden spot's heap.
        Chest = 130,
        ChestOpen = 131,
        Pedestal = 132,
        RewardKey = 133,
        RewardBoost = 134,
        RewardFragment = 135,
        Mound = 136,
    }

    /// <summary>What kind of thing a key draws. Two keys of different categories must never share a sprite.</summary>
    public enum ArtCategory
    {
        None, Player, Wall, Floor, Gate, Item, Currency, Enemy, Boulder, Switch, Plant, Prop, Portal, Demon, Particle, Ruin, Hazard,
        Building, Reward,
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
                case ArtKey.FreezingRain:
                case ArtKey.Crevice: return ArtCategory.Hazard;
                case ArtKey.Fossil: return ArtCategory.Prop;
                case ArtKey.ItemWandQuake:
                case ArtKey.ItemWandRain:
                case ArtKey.ItemWandSnow:
                case ArtKey.ItemWandTornado: return ArtCategory.Item;
                case ArtKey.EnemyRumbler: return ArtCategory.Enemy;
                case ArtKey.Boulder: return ArtCategory.Boulder;
                case ArtKey.BeakButton:
                case ArtKey.DoorSwitch:
                case ArtKey.LeadPlate:
                case ArtKey.GoalPad: return ArtCategory.Switch;
                case ArtKey.Flower: return ArtCategory.Plant;
                case ArtKey.TrialLamp: return ArtCategory.Prop;
                case ArtKey.Portal: return ArtCategory.Portal;
                case ArtKey.Demon: return ArtCategory.Demon;
                case ArtKey.Particle: return ArtCategory.Particle;
                case ArtKey.Mound: return ArtCategory.Prop;
            }
            if (v >= 130 && v < 136) return ArtCategory.Reward;
            if (v >= 120 && v < 130) return ArtCategory.Building;
            return v >= 90 && v < 100 ? ArtCategory.Ruin : ArtCategory.None;
        }

        /// <summary>True for surfaces drawn by repeating the tile over their size (walls, floors, gates, hazards) instead of stretching it.</summary>
        public static bool IsTiled(ArtKey key)
        {
            ArtCategory category = CategoryOf(key);
            return category == ArtCategory.Wall || category == ArtCategory.Floor || category == ArtCategory.Gate ||
                   category == ArtCategory.Hazard;
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

        /// <summary>The art of an item, by its stable id (the beaks, the three Chemistry stones, the Mass Mitt, the gap-crosser*, the four Stormcallers*; anything else is generic).</summary>
        public static ArtKey Item(ItemDefinition item)
        {
            if (item == null) return ArtKey.ItemGeneric;
            switch (item.Id)
            {
                case BiologyBeaks.ThinId: return ArtKey.ItemThinBeak;
                case BiologyBeaks.ThickId: return ArtKey.ItemThickBeak;
                case ChemistryIsotope.GlowId: return ArtKey.ItemGlowstone;
                case ChemistryIsotope.UnstableId: return ArtKey.ItemWildstone;
                case ChemistryIsotope.LeadId: return ArtKey.ItemLeadstone;
                case MassMittItem.Id: return ArtKey.ItemMassMitt;
                case GapCrosserItem.Id: return ArtKey.ItemGapCrosser;
                case StormcallerItem.QuakeId: return ArtKey.ItemWandQuake;
                case StormcallerItem.RainId: return ArtKey.ItemWandRain;
                case StormcallerItem.SnowId: return ArtKey.ItemWandSnow;
                case StormcallerItem.TornadoId: return ArtKey.ItemWandTornado;
                default: return ArtKey.ItemGeneric;
            }
        }

        public static ArtKey Enemy(BiologyEnemyKind kind) =>
            kind == BiologyEnemyKind.SeedWeevil ? ArtKey.EnemySeedWeevil : ArtKey.EnemyPollenPuff;

        public static ArtKey Enemy(ChemistryEnemyKind kind) =>
            kind == ChemistryEnemyKind.FreeRadical ? ArtKey.EnemyFreeRadical : ArtKey.EnemyRustMite;

        public static ArtKey Enemy(PhysicsEnemyKind kind) =>
            kind == PhysicsEnemyKind.QuantumFlea ? ArtKey.EnemyQuantumFlea : ArtKey.EnemyStaticCling;

        public static ArtKey Enemy(MathEnemyKind kind) =>
            kind == MathEnemyKind.DeadEnd ? ArtKey.EnemyDeadEnd : ArtKey.EnemyLooseEnd;

        public static ArtKey Enemy(EarthEnemyKind kind) =>
            kind == EarthEnemyKind.Squall ? ArtKey.EnemySquall : ArtKey.EnemyRumbler;

        /// <summary>The tint of a face's walls (pillars, bars, alcoves): a soft trim of its region colour.</summary>
        public static UnityEngine.Color WallTint(Theme theme) =>
            UnityEngine.Color.Lerp(UnityEngine.Color.white, CubeWorld.ThemeColor(theme), 0.4f);

        /// <summary>The tint of a Town pillar that is a ruin of a science face (in that face's colour).</summary>
        public static UnityEngine.Color TownPillarTint(Theme science) =>
            UnityEngine.Color.Lerp(UnityEngine.Color.white, CubeWorld.ThemeColor(science), 0.5f);

        /// <summary>The tint of a Town ruin piece of a science face (visual only).</summary>
        public static UnityEngine.Color RuinTint(Theme science) =>
            UnityEngine.Color.Lerp(UnityEngine.Color.white, CubeWorld.ThemeColor(science), 0.6f);

        /// <summary>
        /// The floor tint of a theme, chosen per floor tile so the floor reads as its Rubik's region colour
        /// (CubeWorld.ThemeColor) while every dark-outlined sprite on it stays readable: Town's pale stone stays white, the
        /// grass gets a little greener, one plain sand goes red, the grey stone panels blue, another plain sand gold-yellow and
        /// the speckled sand orange (the tints multiply the tile, so they can only darken it).
        /// </summary>
        public static UnityEngine.Color FloorTint(Theme theme)
        {
            switch (theme)
            {
                case Theme.Town: return UnityEngine.Color.white;
                case Theme.Biology: return new UnityEngine.Color(0.78f, 1f, 0.72f);
                case Theme.Chemistry: return new UnityEngine.Color(0.9f, 0.3f, 0.26f);
                case Theme.Physics: return new UnityEngine.Color(0.36f, 0.48f, 0.9f);
                case Theme.Math: return new UnityEngine.Color(1f, 1f, 0.3f);
                default: return new UnityEngine.Color(1f, 0.68f, 0.4f);
            }
        }
    }
}
