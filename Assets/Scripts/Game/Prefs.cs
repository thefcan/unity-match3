using UnityEngine;

namespace Match3.Game
{
    /// <summary>
    /// The game's persisted OPTIONS (not progress — that's ProgressService). A thin
    /// typed wrapper over PlayerPrefs: settings are device-local preferences, exactly
    /// what PlayerPrefs is for, while progress stays in its own repository file.
    /// Setters save immediately (changes are rare) and raise <see cref="Changed"/> so
    /// live systems (audio, music, board sprites) can re-read what they care about.
    /// </summary>
    public static class Prefs
    {
        private const string MusicVolumeKey = "opt_music_volume";
        private const string SfxKey = "opt_sfx";
        private const string HapticsKey = "opt_haptics";
        private const string ColorblindKey = "opt_colorblind";
        private const string NotificationsKey = "opt_notifications";
        private const string RelaxedKey = "opt_relaxed";
        private const string ReducedMotionKey = "opt_reduced_motion";
        private const string BigTextKey = "opt_big_text";

        /// <summary>Raised after any setting changes. Listeners re-read the properties they use.</summary>
        public static event System.Action Changed;

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(MusicVolumeKey, 0.8f);
            set => SetFloat(MusicVolumeKey, Mathf.Clamp01(value));
        }

        public static bool SfxOn
        {
            get => PlayerPrefs.GetInt(SfxKey, 1) != 0;
            set => SetBool(SfxKey, value);
        }

        public static bool HapticsOn
        {
            get => PlayerPrefs.GetInt(HapticsKey, 1) != 0;
            set => SetBool(HapticsKey, value);
        }

        public static bool ColorblindOn
        {
            get => PlayerPrefs.GetInt(ColorblindKey, 0) != 0;
            set => SetBool(ColorblindKey, value);
        }

        public static bool NotificationsOn
        {
            get => PlayerPrefs.GetInt(NotificationsKey, 1) != 0;
            set => SetBool(NotificationsKey, value);
        }

        /// <summary>No move limit, wins cap at one star — the chill/no-fail mode.</summary>
        public static bool RelaxedOn
        {
            get => PlayerPrefs.GetInt(RelaxedKey, 0) != 0;
            set => SetBool(RelaxedKey, value);
        }

        /// <summary>Accessibility: no camera shake, far fewer particles.</summary>
        public static bool ReducedMotionOn
        {
            get => PlayerPrefs.GetInt(ReducedMotionKey, 0) != 0;
            set => SetBool(ReducedMotionKey, value);
        }

        /// <summary>Accessibility: ~10% larger UI via canvas reference resolution.</summary>
        public static bool BigTextOn
        {
            get => PlayerPrefs.GetInt(BigTextKey, 0) != 0;
            set => SetBool(BigTextKey, value);
        }

        private static void SetBool(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static void SetFloat(string key, float value)
        {
            // No immediate Save(): the volume slider fires per frame while dragging.
            // AppLifecycle flushes PlayerPrefs on pause/quit, which is what matters.
            PlayerPrefs.SetFloat(key, value);
            Changed?.Invoke();
        }
    }
}
