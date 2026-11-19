using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectInfinity.EditorTools
{
    /// <summary>
    /// One-click project setup: URP pipeline asset, linear colour, input handling, player identity,
    /// mobile/desktop settings, and build entry points for Windows, Android and iOS.
    /// </summary>
    public static class ProjectConfigurator
    {
        private const string SettingsFolder = "Assets/Settings";
        private const string PipelinePath = SettingsFolder + "/URP_ProjectInfinity.asset";
        private const string RendererPath = SettingsFolder + "/URP_Renderer.asset";
        private const string ScenePath = "Assets/Scenes/Prototype_Auralis.unity";

        [MenuItem("Project Infinity/1. Configure Project", priority = 1)]
        public static void ConfigureFromMenu()
        {
            Configure();
            EditorUtility.DisplayDialog("Project Infinity",
                "Project configured: URP, linear colour, input handling, player identity and targets.\n\n" +
                "Next: Project Infinity → 2. Build Prototype Scene.", "OK");
        }

        public static bool Configure()
        {
            bool ok = ConfigureUrp();
            ConfigurePlayer();
            ConfigureInputHandling();
            ConfigureQuality();
            AssetDatabase.SaveAssets();
            Debug.Log("[Project Infinity] Project configuration applied.");
            return ok;
        }

        private static bool ConfigureUrp()
        {
            ContentSeeder.EnsureFolder(SettingsFolder);

            try
            {
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
                if (pipeline == null)
                {
                    var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
                    if (rendererData == null)
                    {
                        rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                        AssetDatabase.CreateAsset(rendererData, RendererPath);
                    }

                    pipeline = CreatePipelineAsset(rendererData);
                    if (pipeline == null)
                    {
                        Debug.LogError("[Project Infinity] URP pipeline creation returned null.");
                        return false;
                    }

                    AssetDatabase.CreateAsset(pipeline, PipelinePath);
                }

                GraphicsSettings.defaultRenderPipeline = pipeline;
                QualitySettings.renderPipeline = pipeline;
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[Project Infinity] URP setup failed. Assign a URP asset manually in " +
                               "Project Settings → Graphics.\n" + exception);
                return false;
            }
        }

        /// <summary>
        /// URP's factory signature has moved between versions, so it is resolved by reflection.
        /// A missing factory is reported instead of failing to compile.
        /// </summary>
        private static UniversalRenderPipelineAsset CreatePipelineAsset(UniversalRendererData rendererData)
        {
            foreach (var method in typeof(UniversalRenderPipelineAsset).GetMethods(
                         System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (method.Name != "Create") continue;
                var parameters = method.GetParameters();
                if (parameters.Length != 1) continue;
                if (!parameters[0].ParameterType.IsAssignableFrom(typeof(UniversalRendererData))) continue;

                return method.Invoke(null, new object[] { rendererData }) as UniversalRenderPipelineAsset;
            }

            Debug.LogWarning("[Project Infinity] Could not find UniversalRenderPipelineAsset.Create(); " +
                             "creating a bare pipeline asset. Assign renderers manually if the screen is pink.");
            return ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Aurora Forge";
            PlayerSettings.productName = "Project Infinity";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.auroraforge.projectinfinity");
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.auroraforge.projectinfinity");
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, "com.auroraforge.projectinfinity");

            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);

            PlayerSettings.iOS.targetOSVersionString = "13.0";
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Medium);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Medium);
        }

        /// <summary>
        /// Both input backends are enabled so the Input System package and any legacy UI paths work.
        /// </summary>
        private static void ConfigureInputHandling()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;

            var serialized = new SerializedObject(assets[0]);
            var handler = serialized.FindProperty("activeInputHandler");
            if (handler != null && handler.intValue != 2)
            {
                handler.intValue = 2; // 0 = legacy, 1 = new, 2 = both
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[Project Infinity] Input handling set to 'Both' (new Input System + legacy).");
            }
        }

        private static void ConfigureQuality()
        {
            QualitySettings.vSyncCount = 0;
            QualitySettings.antiAliasing = 2;
            QualitySettings.shadowDistance = 55f;
            Application.targetFrameRate = 60;
        }

        // ---------------------------------------------------------------- builds

        [MenuItem("Project Infinity/Build/Windows x64", priority = 40)]
        public static void BuildWindows()
        {
            Build(BuildTarget.StandaloneWindows64, "Builds/Windows/ProjectInfinity.exe");
        }

        [MenuItem("Project Infinity/Build/Android APK", priority = 41)]
        public static void BuildAndroid()
        {
            Build(BuildTarget.Android, "Builds/Android/ProjectInfinity.apk");
        }

        [MenuItem("Project Infinity/Build/iOS Xcode Project", priority = 42)]
        public static void BuildIos()
        {
            Build(BuildTarget.iOS, "Builds/iOS");
        }

        private static void Build(BuildTarget target, string outputPath)
        {
            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("Project Infinity",
                    "Build the prototype scene first (Project Infinity → 2. Build Prototype Scene).", "OK");
                return;
            }

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                target = target,
                locationPathName = outputPath,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            Debug.Log($"[Project Infinity] Build {target}: {summary.result} " +
                      $"({summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors)");

            if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.LogError("[Project Infinity] Build failed. See the console for the first error; " +
                               "missing platform modules are the usual cause.");
            }
        }
    }
}
