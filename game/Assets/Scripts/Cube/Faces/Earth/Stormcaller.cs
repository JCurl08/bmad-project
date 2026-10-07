using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>What the run's Stormcaller* calls up (one is rolled per run). None: no wand held.</summary>
    public enum Phenomenon
    {
        None = -1,

        /// <summary>Shakes the ground: knocks rock pillars down.</summary>
        Quake = 0,

        /// <summary>Calls a downpour: douses a wall of fire.</summary>
        Rain = 1,

        /// <summary>Calls a cold snap: freezes water into a crossing.</summary>
        Snow = 2,

        /// <summary>Calls a whirlwind: blows a debris pile away.</summary>
        Tornado = 3,
    }

    /// <summary>
    /// The Earth item as data, on the beak and stone pattern: the Stormcaller*, a wand with four variants (quake, rain, snow,
    /// tornado), listed in the catalog in VariantOrder. Each run rolls one (ItemVariants.Roll); it is picked up once, kept
    /// for the run and used by swinging it. The phenomenon of an item is always read from its id (KindOf), never from the
    /// catalog's order. The base entry ("earth-item") stays the theme's base item, so the placer still sees one logical
    /// Earth item. Subtle science: the text only talks about rocks, rain, snow and wind, never the sciences behind them.
    /// </summary>
    public static class StormcallerItem
    {
        /// <summary>The Earth base item's id (kept stable; the rolled wand replaces it in a run).</summary>
        public const string Id = "earth-item";

        /// <summary>The base item's name (never shown in a run: the rolled wand replaces it).</summary>
        public const string Name = "Stormcaller*";

        public const string QuakeId = "stormcaller-quake";
        public const string RainId = "stormcaller-rain";
        public const string SnowId = "stormcaller-snow";
        public const string TornadoId = "stormcaller-tornado";

        public static readonly Color ItemColor = new Color(0.95f, 0.6f, 0.25f);
        public static readonly Color QuakeColor = new Color(0.8f, 0.5f, 0.25f);
        public static readonly Color RainColor = new Color(0.35f, 0.6f, 1f);
        public static readonly Color SnowColor = new Color(0.85f, 0.95f, 1f);
        public static readonly Color TornadoColor = new Color(0.7f, 0.6f, 0.9f);

        /// <summary>The order the catalog builder lists the wands in. Consumers never read a phenomenon from it: they read it
        /// from the item id (KindOf, VariantKinds).</summary>
        public static readonly Phenomenon[] VariantOrder = { Phenomenon.Quake, Phenomenon.Rain, Phenomenon.Snow, Phenomenon.Tornado };

        public static int VariantCount => VariantOrder.Length;

        public static string IdOf(Phenomenon kind)
        {
            switch (kind)
            {
                case Phenomenon.Quake: return QuakeId;
                case Phenomenon.Rain: return RainId;
                case Phenomenon.Snow: return SnowId;
                case Phenomenon.Tornado: return TornadoId;
                default: return null;
            }
        }

        /// <summary>The phenomenon an item calls up, from its id; null for anything else (the base item included).</summary>
        public static Phenomenon? KindOf(ItemDefinition item)
        {
            if (item == null) return null;
            switch (item.Id)
            {
                case QuakeId: return Phenomenon.Quake;
                case RainId: return Phenomenon.Rain;
                case SnowId: return Phenomenon.Snow;
                case TornadoId: return Phenomenon.Tornado;
                default: return null;
            }
        }

        /// <summary>True if an item is a Stormcaller* of this phenomenon.</summary>
        public static bool Is(ItemDefinition item, Phenomenon kind) => kind != Phenomenon.None && KindOf(item) == kind;

        /// <summary>
        /// The phenomenon of each Earth variant in a catalog, in variant-index order, read from the items' ids (so the
        /// catalog's order never matters). Null when the catalog has no Earth variants or one is not a wand.
        /// </summary>
        public static List<Phenomenon> VariantKinds(ItemCatalog catalog)
        {
            if (catalog == null) return null;
            List<ItemDefinition> variants = catalog.VariantsOf(Theme.EarthAtmosphere);
            if (variants.Count == 0) return null;
            var kinds = new List<Phenomenon>(variants.Count);
            foreach (ItemDefinition item in variants)
            {
                Phenomenon? kind = KindOf(item);
                if (kind == null) return null;
                kinds.Add(kind.Value);
            }
            return kinds;
        }

        public static Color ColorOf(Phenomenon kind)
        {
            switch (kind)
            {
                case Phenomenon.Quake: return QuakeColor;
                case Phenomenon.Rain: return RainColor;
                case Phenomenon.Snow: return SnowColor;
                case Phenomenon.Tornado: return TornadoColor;
                default: return Color.grey;
            }
        }

        /// <summary>The phenomenon's word in names and lines ("quake", "rain", "snow", "tornado").</summary>
        public static string Word(Phenomenon kind)
        {
            switch (kind)
            {
                case Phenomenon.Quake: return "quake";
                case Phenomenon.Rain: return "rain";
                case Phenomenon.Snow: return "snow";
                case Phenomenon.Tornado: return "tornado";
                default: return "calm";
            }
        }

        /// <summary>A wand's in-world name ("Stormcaller* (quake)").</summary>
        public static string NameOf(Phenomenon kind) => $"{Name} ({Word(kind)})";

        /// <summary>What a wand does, as lines tell it.</summary>
        public static string UseOf(Phenomenon kind)
        {
            switch (kind)
            {
                case Phenomenon.Quake: return "swing it at rock pillars to knock them down";
                case Phenomenon.Rain: return "swing it at a wall of fire to douse it";
                case Phenomenon.Snow: return "swing it at water to freeze a crossing";
                case Phenomenon.Tornado: return "swing it at a debris pile to blow it away";
                default: return "";
            }
        }

        /// <summary>Every in-world string of the Earth item (names and uses), for the text checks.</summary>
        public static IEnumerable<string> AllText()
        {
            yield return Name;
            foreach (Phenomenon kind in VariantOrder)
            {
                yield return NameOf(kind);
                yield return UseOf(kind);
            }
        }

        /// <summary>An unsaved runtime base item (tests and tools); the catalog builder keeps the real asset.</summary>
        public static ItemDefinition CreateItem() => ItemDefinition.Create(Id, Name, Theme.EarthAtmosphere, ItemColor);

        /// <summary>An unsaved runtime wand of a phenomenon (tests and tools); the catalog builder makes the real assets.</summary>
        public static ItemDefinition CreateItem(Phenomenon kind) =>
            ItemDefinition.Create(IdOf(kind), NameOf(kind), Theme.EarthAtmosphere, ColorOf(kind));
    }
}
