using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports the Kenney packs (Assets/Art/Kenney/**) as pixel art: single sprites at 16 pixels per unit (one tile is one
/// world unit), point filtering, no compression, no mipmaps, clamped, full-rect meshes (so walls, floors and gates can tile
/// them). Apply() reimports any texture there whose settings differ (Cube > Build Art Catalog runs it first).
/// </summary>
public class ArtImportSettings : AssetPostprocessor
{
    public const string Root = "Assets/Art/Kenney";
    public const float PixelsPerUnit = 16f;

    public static bool Applies(string path) => path.Replace('\\', '/').StartsWith(Root + "/");

    private void OnPreprocessTexture()
    {
        if (!Applies(assetPath)) return;
        Configure((TextureImporter)assetImporter);
    }

    /// <summary>Sets the pixel-art settings on an importer.</summary>
    public static void Configure(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spriteExtrude = 0;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
    }

    /// <summary>True if an importer already has the pixel-art settings.</summary>
    public static bool IsConfigured(TextureImporter importer)
    {
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        return importer.textureType == TextureImporterType.Sprite &&
               importer.spriteImportMode == SpriteImportMode.Single &&
               Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit) &&
               importer.filterMode == FilterMode.Point &&
               importer.textureCompression == TextureImporterCompression.Uncompressed &&
               !importer.mipmapEnabled &&
               importer.wrapMode == TextureWrapMode.Clamp &&
               settings.spriteMeshType == SpriteMeshType.FullRect;
    }

    /// <summary>Reimports every Kenney texture whose settings differ. Returns how many it reimported.</summary>
    [MenuItem("Cube/Reimport Kenney Art")]
    public static int Apply()
    {
        var stale = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer && !IsConfigured(importer)) stale.Add(path);
        }
        if (stale.Count == 0) return 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string path in stale)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Configure(importer);
                importer.SaveAndReimport();
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        Debug.Log($"Cube: reimported {stale.Count} Kenney textures as pixel art.");
        return stale.Count;
    }
}
