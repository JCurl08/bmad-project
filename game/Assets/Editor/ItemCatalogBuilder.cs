using System.Collections.Generic;
using System.Linq;
using Game.Cube;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Cube > Build Item Catalog: creates a placeholder ItemDefinition for each built science theme
/// (Biology, Chemistry, Physics, Math, Earth) and the ItemCatalog asset that lists them. Existing catalog entries
/// are kept and only themes the catalog has no item for are filled in, so a face story's real item
/// (wherever its asset lives) is never replaced.
/// It also adds the Biology beak variants (Thin Beak, Thick Beak; BiologyBeaks.VariantOrder) to the catalog's
/// variant list when they are missing, keeping every existing variant; each run uses the rolled one.
/// The Chemistry item follows the beak pattern: three stone variants (Glowstone*, Wildstone*, Leadstone*;
/// ChemistryIsotope.VariantOrder) are added to the variant list when missing, and each run uses the rolled one. The
/// Chemistry base entry keeps its asset and its id ("chemistry-item"); a 1.4 placeholder or 1.9 "Isotope" name is
/// renamed in place to ChemistryIsotope.Name.
/// The Physics item is Einstein's Mass Mitt (MassMittItem), renamed in place the same way (id "physics-item"), also
/// one item with no variants. The Math item is the gap-crosser* (GapCrosserItem, id "math-item"), one item with no
/// variants; a "Math Item" placeholder is renamed to it in place.
/// The Earth item is the Stormcaller* (StormcallerItem), on the stone pattern: its base entry ("earth-item"; an "Earth Item"
/// placeholder or "EarthAtmosphere Item" name is renamed in place, id kept) and four wand variants (quake, rain, snow,
/// tornado; StormcallerItem.VariantOrder), added to the variant list when missing; each run uses the rolled one.
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
        new Placeholder { Theme = Theme.Math, Id = GapCrosserItem.Id, Name = GapCrosserItem.Name, Color = GapCrosserItem.ItemColor },
        new Placeholder { Theme = Theme.EarthAtmosphere, Id = StormcallerItem.Id, Name = StormcallerItem.Name, Color = StormcallerItem.ItemColor },
    };

    /// <summary>Placeholder names of the Earth base item, renamed to the Stormcaller* on the next build.</summary>
    private static readonly string[] PlaceholderEarthNames = { "Earth Item", "EarthAtmosphere Item" };

    /// <summary>The placeholder name of the Math item, renamed to the gap-crosser* on the next build.</summary>
    private const string PlaceholderMathName = "Math Item";

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
            string path = p.Theme == Theme.EarthAtmosphere ? $"{Root}/Earth Item.asset" : $"{Root}/{p.Theme} Item.asset";
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

        // The Math item is the gap-crosser*, renamed in place the same way (same asset, id "math-item").
        ItemDefinition math = catalog.ForTheme(Theme.Math);
        if (math != null && math.Id == GapCrosserItem.Id && math.DisplayName == PlaceholderMathName)
        {
            math.Set(math.Id, GapCrosserItem.Name, Theme.Math, GapCrosserItem.ItemColor);
            EditorUtility.SetDirty(math);
        }

        // The Earth base entry is the Stormcaller*, renamed in place the same way (same asset, id "earth-item").
        ItemDefinition earth = catalog.ForTheme(Theme.EarthAtmosphere);
        if (earth != null && earth.Id == StormcallerItem.Id && System.Array.IndexOf(PlaceholderEarthNames, earth.DisplayName) >= 0)
        {
            earth.Set(earth.Id, StormcallerItem.Name, Theme.EarthAtmosphere, StormcallerItem.ItemColor);
            EditorUtility.SetDirty(earth);
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
        // The Stormcallers*, the same way (in roll order, assets named "Stormcaller Quake" and so on).
        foreach (Phenomenon kind in StormcallerItem.VariantOrder)
        {
            string id = StormcallerItem.IdOf(kind);
            if (variants.Exists(v => v.Id == id)) continue;
            string word = StormcallerItem.Word(kind);
            string path = $"{Root}/Stormcaller {char.ToUpperInvariant(word[0])}{word.Substring(1)}.asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item == null)
            {
                ItemDefinition template = StormcallerItem.CreateItem(kind);
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

    /// <summary>Sort key of a variant: beaks, then stones, then wands, then anything else by its existing position.</summary>
    private static int CanonicalIndex(ItemDefinition item, int index)
    {
        if (BiologyBeaks.KindOf(item) is BeakKind beak) return System.Array.IndexOf(BiologyBeaks.VariantOrder, beak);
        if (ChemistryIsotope.KindOf(item) is IsotopeStage stage)
            return BiologyBeaks.VariantCount + System.Array.IndexOf(ChemistryIsotope.VariantOrder, stage);
        if (StormcallerItem.KindOf(item) is Phenomenon wand)
            return BiologyBeaks.VariantCount + ChemistryIsotope.VariantCount + System.Array.IndexOf(StormcallerItem.VariantOrder, wand);
        return BiologyBeaks.VariantCount + ChemistryIsotope.VariantCount + StormcallerItem.VariantCount + index;
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
