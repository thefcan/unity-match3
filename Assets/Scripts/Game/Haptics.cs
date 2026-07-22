using UnityEngine;

namespace Match3.Game
{
    /// <summary>
    /// Tiny Android vibration wrapper. Handheld.Vibrate() is one fixed ~400 ms buzz —
    /// far too heavy per detonation — so this talks to the Vibrator service with
    /// explicit millisecond pulses. No-ops in the editor, on other platforms, and
    /// when the settings toggle is off.
    /// </summary>
    public static class Haptics
    {
        /// <summary>Settings gate — initialized from <see cref="Prefs.HapticsOn"/> at boot.</summary>
        public static bool Enabled = true;

        public static void Light() => Vibrate(20);
        public static void Medium() => Vibrate(40);
        public static void Heavy() => Vibrate(70);

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject _vibrator;
        private static bool _resolved;

        private static void Vibrate(long milliseconds)
        {
            if (!Enabled)
                return;

            if (!_resolved)
            {
                _resolved = true;
                try
                {
                    using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
                catch
                {
                    _vibrator = null; // no vibrator / permission quirk — stay silent forever
                }
            }

            try
            {
                _vibrator?.Call("vibrate", milliseconds);
            }
            catch
            {
                // never let a haptic tick throw into gameplay
            }
        }
#else
        private static void Vibrate(long milliseconds)
        {
            _ = milliseconds;
        }
#endif
    }
}
