using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Tracer > Build WebGL: itch.io-compatible WebGL build (Gzip with decompression fallback) written to game/Builds/WebGL.
/// 4.21: only the game's scene (CubeSceneBuilder.ScenePath: the Tracer scene and test scenes are never built), managed
/// stripping High (Assets/link.xml keeps the game's and the Input System's assemblies whole), and the build's version and
/// size in the log.
/// </summary>
public static class WebGLBuilder
{
    public const string OutputPath = "Builds/WebGL";

    [MenuItem("Tracer/Build WebGL")]
    public static void BuildFromMenu()
    {
        BuildReport report = Build();
        if (report == null) return;

        bool ok = report.summary.result == BuildResult.Succeeded;
        string message = ok
            ? $"WebGL build {PlayerSettings.bundleVersion} succeeded ({SizeText(report)}):\n{System.IO.Path.GetFullPath(OutputPath)}\n\nZip the folder's contents and upload to itch.io."
            : $"WebGL build {report.summary.result}. See the Console for errors.";
        EditorUtility.DisplayDialog("Tracer", message, "OK");
    }

    /// <summary>Also usable from the command line: -executeMethod WebGLBuilder.BuildBatch</summary>
    public static void BuildBatch()
    {
        BuildReport report = Build();
        EditorApplication.Exit(report != null && report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    /// <summary>The scenes a release build holds: the enabled build scenes that are the game's (never Tracer or a test scene).</summary>
    public static string[] ReleaseScenes() =>
        EditorBuildSettings.scenes.Where(s => s.enabled && s.path == CubeSceneBuilder.ScenePath).Select(s => s.path).ToArray();

    private static string SizeText(BuildReport report) => $"{report.summary.totalSize / (1024f * 1024f):0.0} MB";

    private static BuildReport Build()
    {
        string[] scenes = ReleaseScenes();
        if (scenes.Length == 0)
        {
            Debug.LogError($"WebGLBuilder: {CubeSceneBuilder.ScenePath} is not an enabled build scene. Run Cube > Create Cube Scene first.");
            return null;
        }

        // itch.io serves Gzip builds reliably when the decompression fallback is on.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        // 16:10 canvas so the page shows exactly one 24x15 screen.
        PlayerSettings.defaultWebScreenWidth = 960;
        PlayerSettings.defaultWebScreenHeight = 600;
        // 4.21: a smaller build.
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.High);
        PlayerSettings.stripEngineCode = true;
        AssetDatabase.SaveAssets();

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputPath,
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"WebGLBuilder: {report.summary.result}, {report.summary.totalErrors} errors, {PlayerSettings.productName} " +
                  $"{PlayerSettings.bundleVersion}, {SizeText(report)}, output {OutputPath}");
        return report;
    }
}
