using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class SteamPostBuildProcessor : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.StandaloneWindows &&
            report.summary.platform != BuildTarget.StandaloneWindows64)
        {
            return;
        }

        string sourcePath = Path.Combine(Directory.GetCurrentDirectory(), "steam_appid.txt");
        if (!File.Exists(sourcePath))
        {
            Debug.LogWarning("[SteamPostBuild] steam_appid.txt not found in project root.");
            return;
        }

        string buildDir = Path.GetDirectoryName(report.summary.outputPath);
        if (string.IsNullOrEmpty(buildDir))
        {
            return;
        }

        string destPath = Path.Combine(buildDir, "steam_appid.txt");
        File.Copy(sourcePath, destPath, true);
        Debug.Log($"[SteamPostBuild] Successfully copied steam_appid.txt to: {destPath}");
    }
}
