using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// The release build, as a script rather than a sequence of dialog boxes — so the
    /// bundle that goes to Play is reproducible, and so CI can produce one later
    /// without a human clicking through Build Settings.
    ///
    ///   Match3 > Build > Android AAB
    ///
    /// or headless (with the editor CLOSED — Unity locks the project):
    ///
    ///   Unity -batchmode -quit -nographics -projectPath . \
    ///     -executeMethod Match3.EditorTools.AndroidBuild.BuildAab -logFile build.log
    ///
    /// NOTE: this needs Android Build Support installed, and has not been run against
    /// a real SDK yet — see docs/RELEASE.md. It deliberately REFUSES to build a
    /// debug-signed bundle: Play rejects those, and discovering that after the upload
    /// costs more than the check.
    /// </summary>
    public static class AndroidBuild
    {
        private const string OutputFolder = "Build/Android";

        [MenuItem("Match3/Build/Android AAB")]
        public static void BuildAab()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new BuildFailedException(
                    "Android Build Support is not installed — Unity Hub > Installs > Add modules " +
                    "(Android SDK & NDK Tools + OpenJDK). See docs/RELEASE.md.");

            if (!PlayerSettings.Android.useCustomKeystore)
                throw new BuildFailedException(
                    "No custom keystore configured, so this would be a DEBUG-SIGNED bundle and Play " +
                    "would reject it. Project Settings > Player > Publishing Settings, then see " +
                    "docs/RELEASE.md section 2b.");

            string[] scenes = EnabledScenes();
            if (scenes.Length == 0)
                throw new BuildFailedException(
                    "No scenes in the build list — run Match3 > Setup > Add Scenes To Build.");

            Directory.CreateDirectory(OutputFolder);
            string output = Path.Combine(OutputFolder,
                $"CandyMatch-{PlayerSettings.bundleVersion}-{PlayerSettings.Android.bundleVersionCode}.aab");

            // App bundle, not APK: Play has required AAB for new apps since 2021.
            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Debugging; // symbols for crash reports

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None, // release: no development build, no script debugging
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Android build {summary.result} after {summary.totalTime}.");

            Debug.Log($"AAB built: {output} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime}). " +
                      $"applicationId={PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android)}, " +
                      $"version={PlayerSettings.bundleVersion} ({PlayerSettings.Android.bundleVersionCode})");
        }

        private static string[] EnabledScenes()
        {
            var scenes = new System.Collections.Generic.List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                if (scene.enabled)
                    scenes.Add(scene.path);
            return scenes.ToArray();
        }
    }
}
