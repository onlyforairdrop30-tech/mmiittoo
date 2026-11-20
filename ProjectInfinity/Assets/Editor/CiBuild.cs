using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ProjectInfinity.EditorTools
{
    /// <summary>
    /// Batch entry point used by CI (.github/workflows/unity-ci.yml) via unity-builder's
    /// <c>buildMethod</c>. A fresh checkout has no generated scene, so this generates it first
    /// (same code path as the "Build Prototype Scene" menu item) and then builds a player.
    /// Failures throw <see cref="BuildFailedException"/> so the job goes red instead of passing
    /// with no artifact.
    /// </summary>
    public static class CiBuild
    {
        private const string OutputPath = "Builds/Linux/ProjectInfinity.x86_64";
        private const string WindowsOutputPath = "Builds/Windows/ProjectInfinity.exe";

        public static void BuildWindows64()
        {
            if (!File.Exists(PrototypeSceneBuilder.ScenePath))
            {
                Debug.Log("[Project Infinity] Scene missing from checkout; generating it before the build.");
                if (!PrototypeSceneBuilder.Build())
                {
                    throw new BuildFailedException(
                        "Prototype scene generation failed; see the errors logged above.");
                }
            }

            ProjectConfigurator.Configure();

            string directory = Path.GetDirectoryName(WindowsOutputPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { PrototypeSceneBuilder.ScenePath },
                target = BuildTarget.StandaloneWindows64,
                locationPathName = WindowsOutputPath,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.Log($"[Project Infinity] Windows build {summary.result}: " +
                      $"{summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} error(s).");

            if (summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException($"Build {summary.result} with {summary.totalErrors} error(s).");
            }
        }

        public static void BuildLinux64()
        {
            if (!File.Exists(PrototypeSceneBuilder.ScenePath))
            {
                Debug.Log("[Project Infinity] Scene missing from checkout; generating it before the CI build.");
                if (!PrototypeSceneBuilder.Build())
                {
                    throw new BuildFailedException(
                        "Prototype scene generation failed; see the errors logged above.");
                }
            }

            // Same one-click setup a developer runs, so the artifact is representative (URP, linear
            // colour, input handling, player identity).
            ProjectConfigurator.Configure();

            string directory = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { PrototypeSceneBuilder.ScenePath },
                target = BuildTarget.StandaloneLinux64,
                locationPathName = OutputPath,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.Log($"[Project Infinity] CI build {summary.result}: " +
                      $"{summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} error(s).");

            if (summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException($"Build {summary.result} with {summary.totalErrors} error(s).");
            }
        }
    }
}
