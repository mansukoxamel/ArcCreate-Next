using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace ArcCreate.EditorScripts
{
    internal static class WindowsBuild
    {
        private static readonly string[] Scenes =
        {
            "Assets/Scenes/Boot.unity",
            "Assets/Scenes/Compose.unity",
            "Assets/Scenes/Gameplay.unity",
        };

        [MenuItem("ArcCreate Next/Build Windows")]
        public static void Build()
        {
            PlayerSettings.SetManagedStrippingLevel(
                NamedBuildTarget.Standalone,
                ManagedStrippingLevel.Low);
            AssetDatabase.SaveAssets();

            BuildReport report = BuildPipeline.BuildPlayer(
                Scenes,
                "Build/ArcCreateNext/ArcCreateNext.exe",
                BuildTarget.StandaloneWindows64,
                BuildOptions.None);

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"Windows build failed: {report.summary.result}");
            }
        }
    }
}
