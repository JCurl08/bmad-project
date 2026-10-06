using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>Darwin's beak adaptations: the Biology item is one of these per run.</summary>
    public enum BeakKind { Thin = 0, Thick = 1 }

    /// <summary>
    /// The two beak variants as data. The catalog lists them in VariantOrder (variant index = BeakKind), so the
    /// seeded roll (ItemVariants.Roll) maps straight to a beak. Thin beak: long reach, presses distant buttons
    /// and pollinates flowers. Thick beak: breaks rocks and pots.
    /// </summary>
    public static class BiologyBeaks
    {
        public const string ThinId = "thin-beak";
        public const string ThickId = "thick-beak";
        public const string ThinName = "Thin Beak";
        public const string ThickName = "Thick Beak";

        /// <summary>Swing reach with the thin beak equipped (the default reach is 0.85).</summary>
        public const float ThinReach = 2.5f;

        public static readonly Color ThinColor = new Color(0.55f, 0.95f, 0.85f);
        public static readonly Color ThickColor = new Color(0.85f, 0.6f, 0.3f);

        /// <summary>Variant order in the catalog: index 0 is the thin beak, 1 the thick beak.</summary>
        public static readonly BeakKind[] VariantOrder = { BeakKind.Thin, BeakKind.Thick };

        public static int VariantCount => VariantOrder.Length;

        /// <summary>The beak of a variant index (as rolled by ItemVariants.Roll).</summary>
        public static BeakKind FromVariant(int variant) =>
            variant >= 0 && variant < VariantOrder.Length ? VariantOrder[variant] : BeakKind.Thin;

        public static int VariantOf(BeakKind beak) => (int)beak;

        /// <summary>The run's beak for a seed (the same roll the catalog makes).</summary>
        public static BeakKind Roll(int seed) => FromVariant(ItemVariants.Roll(seed, Theme.Biology, VariantCount));

        public static BeakKind Other(BeakKind beak) => beak == BeakKind.Thin ? BeakKind.Thick : BeakKind.Thin;

        /// <summary>The beak an item is, from its id; null for anything else.</summary>
        public static BeakKind? KindOf(ItemDefinition item)
        {
            if (item == null) return null;
            if (item.Id == ThinId) return BeakKind.Thin;
            if (item.Id == ThickId) return BeakKind.Thick;
            return null;
        }

        /// <summary>
        /// The beak of each Biology variant in a catalog, in variant-index order, read from the items' ids (so
        /// the catalog's order never matters). Null when the catalog has no Biology variants or one is not a beak.
        /// </summary>
        public static List<BeakKind> VariantKinds(ItemCatalog catalog)
        {
            if (catalog == null) return null;
            List<ItemDefinition> variants = catalog.VariantsOf(Theme.Biology);
            if (variants.Count == 0) return null;
            var kinds = new List<BeakKind>(variants.Count);
            foreach (ItemDefinition item in variants)
            {
                BeakKind? kind = KindOf(item);
                if (kind == null) return null;
                kinds.Add(kind.Value);
            }
            return kinds;
        }

        public static string Name(BeakKind beak) => beak == BeakKind.Thin ? ThinName : ThickName;

        /// <summary>Unsaved runtime beak items (tests and tools); the catalog builder makes the real assets.</summary>
        public static ItemDefinition CreateItem(BeakKind beak)
        {
            ItemDefinition item = beak == BeakKind.Thin
                ? ItemDefinition.Create(ThinId, ThinName, Theme.Biology, ThinColor)
                : ItemDefinition.Create(ThickId, ThickName, Theme.Biology, ThickColor);
            if (beak == BeakKind.Thin) item.AttackReach = ThinReach;
            return item;
        }
    }
}
