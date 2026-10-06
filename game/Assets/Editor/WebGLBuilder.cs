using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Tracer > Build WebGL: itch.io-compatible WebGL build (Gzip with decompression fallback)
/// written to game/Builds/WebGL.
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
            ? $"WebGL build succeeded:\n{System.IO.Path.GetFullPath(OutputPath)}\n\nZip the folder's contents and upload to itch.io."
            : $"WebGL build {report.summary.result}. See the Console for errors.";
        EditorUtility.DisplayDialog("Tracer", message, "OK");
    }

    /// <summary>Also usable from the command line: -executeMethod WebGLBuilder.BuildBatch</summary>
    public static void BuildBatch()
    {
        BuildReport report = Build();
        EditorApplication.Exit(report != null && report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    private static BuildReport Build()
    {
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            Debug.LogError("WebGLBuilder: no enabled build scenes. Run Tracer > Create Tracer Scene first.");
            return null;
        }

        // itch.io serves Gzip builds reliably when the decompression fallback is on.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        // 16:10 canvas so the page shows exactly one 16x10 screen.
        PlayerSettings.defaultWebScreenWidth = 960;
        PlayerSettings.defaultWebScreenHeight = 600;
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
        Debug.Log($"WebGLBuilder: {report.summary.result}, {report.summary.totalErrors} errors, output {OutputPath}");
        return report;
    }
}
