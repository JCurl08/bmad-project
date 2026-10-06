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
/// The Chemistry item is Curie's isotope (ChemistryIsotope): a placeholder Chemistry entry is renamed to it in place,
/// keeping its asset and its id ("chemistry-item"). It stays one item with no variants.
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
        new Placeholder { Theme = Theme.Physics, Id = "physics-item", Name = "Physics Item", Color = new Color(0.45f, 0.75f, 1f) },
    };

    /// <summary>The 1.4 placeholder name of the Chemistry item, renamed to the isotope on the next build.</summary>
    private const string PlaceholderChemistryName = "Chemistry Item";

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

        // The Chemistry item is Curie's isotope: the 1.4 placeholder entry becomes it in place (same asset, same
        // id "chemistry-item", so every reference and the placer are unchanged); only its name and colour change.
        ItemDefinition chemistry = catalog.ForTheme(Theme.Chemistry);
        if (chemistry != null && chemistry.Id == ChemistryIsotope.Id && chemistry.DisplayName == PlaceholderChemistryName)
        {
            chemistry.Set(chemistry.Id, ChemistryIsotope.Name, Theme.Chemistry, ChemistryIsotope.ItemColor);
            EditorUtility.SetDirty(chemistry);
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
        // Canonical order: the beaks in BiologyBeaks.VariantOrder first (consumers read the beak from the item id
        // anyway), every other variant after them in its existing order.
        List<ItemDefinition> ordered = variants
            .Select((item, index) => (item, index))
            .OrderBy(e => BiologyBeaks.KindOf(e.item) is BeakKind k ? System.Array.IndexOf(BiologyBeaks.VariantOrder, k) : BiologyBeaks.VariantCount + e.index)
            .Select(e => e.item)
            .ToList();
        variants = ordered;
        catalog.SetVariants(variants);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"Cube: built item catalog at {CatalogPath} ({items.Count} items, {variants.Count} variants).");
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
