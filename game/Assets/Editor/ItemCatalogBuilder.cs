using System.Collections.Generic;
using System.Linq;
using Game.Cube;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Cube > Build Item Catalog: creates a placeholder ItemDefinition for each built science theme
/// (Biology, Chemistry, Physics) and the ItemCatalog asset that lists them. Existing catalog entries
/// are kept and only themes the catalog has no item for are filled in, so a face story's real item
/// (wherever its asset lives) is never replaced.
/// It also adds the Biology beak variants (Thin Beak, Thick Beak; BiologyBeaks.VariantOrder) to the catalog's
/// variant list when they are missing, keeping every existing variant; each run uses the rolled one.
/// The Chemistry item follows the beak pattern: three stone variants (Glowstone*, Wildstone*, Leadstone*;
/// ChemistryIsotope.VariantOrder) are added to the variant list when missing, and each run uses the rolled one. The
/// Chemistry base entry keeps its asset and its id ("chemistry-item"); a 1.4 placeholder or 1.9 "Isotope" name is
/// renamed in place to ChemistryIsotope.Name.
/// The Physics item is Einstein's Mass Mitt (MassMittItem), renamed in place the same way (id "physics-item"), also
/// one item with no variants.
/// Also works from the command line via -executeMethod ItemCatalogBuilder.Build.
/// </summary>
public static class ItemCatalogBuilder
{
    public const string Root = "Assets/Items";
    public const string CatalogPath = Root + "/ItemCatalog.asset";

    private struct Placeholder
    {
        public Theme Theme;
        public string Id, Name;
        public Color Color;
    }

    private static readonly Placeholder[] Placeholders =
    {
        new Placeholder { Theme = Theme.Biology, Id = "biology-item", Name = "Biology Item", Color = new Color(0.45f, 1f, 0.45f) },
        new Placeholder { Theme = Theme.Chemistry, Id = ChemistryIsotope.Id, Name = ChemistryIsotope.Name, Color = ChemistryIsotope.ItemColor },
        new Placeholder { Theme = Theme.Physics, Id = MassMittItem.Id, Name = MassMittItem.Name, Color = MassMittItem.ItemColor },
    };

    /// <summary>Earlier names of the Chemistry base item (the 1.4 placeholder, the 1.9 isotope), renamed on the next build.</summary>
    private static readonly string[] OldChemistryNames = { "Chemistry Item", "Isotope" };

    /// <summary>The 1.4 placeholder name of the Physics item, renamed to the Mass Mitt on the next build.</summary>
    private const string PlaceholderPhysicsName = "Physics Item";

    [MenuItem("Cube/Build Item Catalog")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "Items");

        var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<ItemCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        // Keep every existing entry; add a placeholder only for themes the catalog does not cover.
        var items = new List<ItemDefinition>();
        foreach (ItemDefinition existing in catalog.Items)
            if (existing != null) items.Add(existing);
        foreach (Placeholder p in Placeholders)
        {
            if (catalog.ForTheme(p.Theme) != null) continue;
            string path = $"{Root}/{p.Theme} Item.asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                item.Set(p.Id, p.Name, p.Theme, p.Color);
                AssetDatabase.CreateAsset(item, path);
            }
            items.Add(item);
        }

        catalog.Set(items);

        // The Chemistry base entry is renamed in place (same asset, same id "chemistry-item", so every reference and the
        // placer are unchanged); only its name and colour change. Runs use the rolled stone variant instead.
        ItemDefinition chemistry = catalog.ForTheme(Theme.Chemistry);
        if (chemistry != null && chemistry.Id == ChemistryIsotope.Id && System.Array.IndexOf(OldChemistryNames, chemistry.DisplayName) >= 0)
        {
            chemistry.Set(chemistry.Id, ChemistryIsotope.Name, Theme.Chemistry, ChemistryIsotope.ItemColor);
            EditorUtility.SetDirty(chemistry);
        }

        // The Physics item is Einstein's Mass Mitt, renamed in place the same way (same asset, id "physics-item").
        ItemDefinition physics = catalog.ForTheme(Theme.Physics);
        if (physics != null && physics.Id == MassMittItem.Id && physics.DisplayName == PlaceholderPhysicsName)
        {
            physics.Set(physics.Id, MassMittItem.Name, Theme.Physics, MassMittItem.ItemColor);
            EditorUtility.SetDirty(physics);
        }

        // Theme variants: keep what is there, add the beaks if missing (in roll order).
        var variants = new List<ItemDefinition>();
        foreach (ItemDefinition existing in catalog.Variants)
            if (existing != null) variants.Add(existing);
        foreach (BeakKind beak in BiologyBeaks.VariantOrder)
        {
            string id = beak == BeakKind.Thin ? BiologyBeaks.ThinId : BiologyBeaks.ThickId;
            if (variants.Exists(v => v.Id == id)) continue;
            string path = $"{Root}/{BiologyBeaks.Name(beak)}.asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item == null)
            {
                ItemDefinition template = BiologyBeaks.CreateItem(beak);
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                item.Set(template.Id, template.DisplayName, template.HomeTheme, template.PlaceholderColor);
                item.AttackReach = template.AttackReach;
                Object.DestroyImmediate(template);
                AssetDatabase.CreateAsset(item, path);
            }
            variants.Add(item);
        }
        // The Chemistry stones, the same way (in roll order, assets named after the stones without the placeholder star).
        foreach (IsotopeStage stage in ChemistryIsotope.VariantOrder)
        {
            string id = ChemistryIsotope.IdOf(stage);
            if (variants.Exists(v => v.Id == id)) continue;
            string path = $"{Root}/{ChemistryIsotope.StageName(stage).TrimEnd('*')}.asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item == null)
            {
                ItemDefinition template = ChemistryIsotope.CreateItem(stage);
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                item.Set(template.Id, template.DisplayName, template.HomeTheme, template.PlaceholderColor);
                Object.DestroyImmediate(template);
                AssetDatabase.CreateAsset(item, path);
            }
            variants.Add(item);
        }
        // Canonical order: the beaks in BiologyBeaks.VariantOrder first, then the stones in ChemistryIsotope.VariantOrder
        // (consumers read the kind from the item id anyway), every other variant after them in its existing order.
        List<ItemDefinition> ordered = variants
            .Select((item, index) => (item, index))
            .OrderBy(e => CanonicalIndex(e.item, e.index))
            .Select(e => e.item)
            .ToList();
        variants = ordered;
        catalog.SetVariants(variants);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"Cube: built item catalog at {CatalogPath} ({items.Count} items, {variants.Count} variants).");
    }

    /// <summary>Sort key of a variant: beaks, then stones, then anything else by its existing position.</summary>
    private static int CanonicalIndex(ItemDefinition item, int index)
    {
        if (BiologyBeaks.KindOf(item) is BeakKind beak) return System.Array.IndexOf(BiologyBeaks.VariantOrder, beak);
        if (ChemistryIsotope.KindOf(item) is IsotopeStage stage)
            return BiologyBeaks.VariantCount + System.Array.IndexOf(ChemistryIsotope.VariantOrder, stage);
        return BiologyBeaks.VariantCount + ChemistryIsotope.VariantCount + index;
    }

    /// <summary>Loads the catalog asset, building it first if it does not exist.</summary>
    public static ItemCatalog LoadOrBuild()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
        if (catalog != null) return catalog;
        Build();
        return AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
    }
}
