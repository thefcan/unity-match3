using UnityEngine;

namespace Match3.Game
{
    /// <summary>
    /// Device-wide runtime setup, applied before the first scene: an explicit 60 fps
    /// cap (vSync off) so 90/120 Hz panels don't burn battery chasing refresh, and a
    /// persistent lifecycle object that saves progress when Android backgrounds the
    /// app (a backgrounded app can be killed without any further callback).
    /// </summary>
    internal static class GameBoot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;

            ApplyPrefs();
            Prefs.Changed += ApplyPrefs;

            var lifecycle = new GameObject("AppLifecycle");
            Object.DontDestroyOnLoad(lifecycle);
            lifecycle.AddComponent<AppLifecycle>();
        }

        /// <summary>Pushes the persisted options into the live systems that read them.</summary>
        private static void ApplyPrefs()
        {
            AudioManager.SfxEnabled = Prefs.SfxOn;
            Haptics.Enabled = Prefs.HapticsOn;
            CandySpriteLibrary.ColorblindMode = Prefs.ColorblindOn;
        }
    }

    /// <summary>
    /// Persistent lifecycle hooks: saves progress on pause/quit (Android can kill a
    /// backgrounded app without warning) and routes the hardware back button —
    /// pause toggle in the game scene, double-press-to-quit in the menu.
    /// </summary>
    internal sealed class AppLifecycle : MonoBehaviour
    {
        private float _lastBackPress = -10f;

        private void OnApplicationPause(bool paused)
        {
            if (!paused)
                return;
            ProgressService.SaveNow();
            PlayerPrefs.Save(); // float prefs (volume) defer their save to here
        }

        private void OnApplicationQuit()
        {
            ProgressService.SaveNow();
            PlayerPrefs.Save();
        }

        private void Update()
        {
            // Android maps the back button to Escape under the old Input Manager.
            if (!Input.GetKeyDown(KeyCode.Escape))
                return;

            var game = FindObjectOfType<GameManager>();
            if (game != null)
            {
                game.TogglePause();
                return;
            }

            if (Time.unscaledTime - _lastBackPress < 2f)
                Application.Quit();
            else
                _lastBackPress = Time.unscaledTime;
        }
    }
}
