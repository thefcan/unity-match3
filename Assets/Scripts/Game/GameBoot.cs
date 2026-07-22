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

            var lifecycle = new GameObject("AppLifecycle");
            Object.DontDestroyOnLoad(lifecycle);
            lifecycle.AddComponent<AppLifecycle>();
        }
    }

    /// <summary>Flushes saved progress on pause/quit — the Android "about to be killed" hooks.</summary>
    internal sealed class AppLifecycle : MonoBehaviour
    {
        private void OnApplicationPause(bool paused)
        {
            if (paused)
                ProgressService.SaveNow();
        }

        private void OnApplicationQuit()
        {
            ProgressService.SaveNow();
        }
    }
}
