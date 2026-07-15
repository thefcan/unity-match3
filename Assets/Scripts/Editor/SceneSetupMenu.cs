using UnityEditor;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// Registers MainMenu + Game in the build scene list (required for
    /// SceneManager.LoadScene by name). Idempotent — safe to re-run; also the
    /// fallback if the checked-in EditorBuildSettings.asset ever gets clobbered
    /// by an open editor.
    /// </summary>
    public static class SceneSetupMenu
    {
        [MenuItem("Match3/Setup/Add Scenes To Build")]
        public static void AddScenesToBuild()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Game.unity", true),
            };
            Debug.Log("SceneSetupMenu: build scene list set to MainMenu + Game.");
        }
    }
}
