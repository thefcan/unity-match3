using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>TEST-COPY ONLY: headless macOS player build for the smoke run.</summary>
    public static class SmokeBuild
    {
        public static void Build()
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
                scenes = new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Game.unity" };
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = "SmokeBuild/match3.app",
                target = BuildTarget.StandaloneOSX,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("SMOKE BUILD RESULT: " + report.summary.result);
        }
    }
}
