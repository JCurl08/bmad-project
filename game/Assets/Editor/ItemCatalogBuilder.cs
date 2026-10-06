using System.Collections.Generic;
using Game.Cube;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Cube > Build Item Catalog: creates a placeholder ItemDefinition for each built science theme
/// (Biology, Chemistry, Physics) and the ItemCatalog asset that lists them. Existing catalog entries
/// are kept and only themes the catalog has no item for are filled in, so a face story's real item
/// (wherever its asset lives) is never replaced.
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
        new Placeholder { Theme = Theme.Chemistry, Id = "chemistry-item", Name = "Chemistry Item", Color = new Color(0.9f, 0.5f, 1f) },
        new Placeholder { Theme = Theme.Physics, Id = "physics-item", Name = "Physics Item", Color = new Color(0.45f, 0.75f, 1f) },
    };

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
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"Cube: built item catalog at {CatalogPath} ({items.Count} items).");
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
