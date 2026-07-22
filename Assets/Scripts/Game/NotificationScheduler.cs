using System;
using UnityEngine;
#if MOBILE_NOTIFICATIONS && UNITY_ANDROID && !UNITY_EDITOR
using Unity.Notifications.Android;
using UnityEngine.Android;
#endif

namespace Match3.Game
{
    /// <summary>
    /// Local (offline) re-engagement notifications: a repeating "daily treat is
    /// ready" ping and a one-shot "streak about to melt" reminder. Everything is
    /// compiled out unless the com.unity.mobile.notifications package is present
    /// (MOBILE_NOTIFICATIONS comes from Match3.Game.asmdef's versionDefines), so the
    /// project builds cleanly before AND after the package import.
    ///
    /// Permission policy: the Android 13+ POST_NOTIFICATIONS prompt is only ever
    /// triggered from a user action (claiming a reward / flipping the settings
    /// toggle) — never at boot.
    /// </summary>
    public static class NotificationScheduler
    {
        /// <summary>User-action entry point: may show the permission prompt, then schedules.</summary>
        public static void EnsurePermissionThenSchedule()
        {
            RequestPermission();
            Reschedule();
        }

        /// <summary>
        /// Silent entry point (boot / toggle-off): cancels everything when disabled,
        /// otherwise re-schedules. Never prompts for permission.
        /// </summary>
        public static void Reschedule()
        {
#if MOBILE_NOTIFICATIONS && UNITY_ANDROID && !UNITY_EDITOR
            AndroidNotificationCenter.CancelAllScheduledNotifications();
            if (!Prefs.NotificationsOn)
                return;

            var channel = new AndroidNotificationChannel(
                "daily", "Daily treats", "Daily reward reminders", Importance.Default);
            AndroidNotificationCenter.RegisterNotificationChannel(channel);

            var daily = new AndroidNotification
            {
                Title = "Candy Match",
                Text = "Your daily treat is ready!",
                FireTime = NextLocalTime(20, 0),
                RepeatInterval = TimeSpan.FromDays(1),
            };
            AndroidNotificationCenter.SendNotification(daily, "daily");

            // One-shot streak saver: only meaningful if there's a streak to lose.
            if (MetaService.Current.Streak > 0 && MetaService.Status == StreakStatus.AlreadyClaimed)
            {
                var streakSaver = new AndroidNotification
                {
                    Title = "Candy Match",
                    Text = $"Your {MetaService.Current.Streak}-day streak is about to melt!",
                    FireTime = NextLocalTime(21, 30),
                };
                AndroidNotificationCenter.SendNotification(streakSaver, "daily");
            }
#endif
        }

        private static void RequestPermission()
        {
#if MOBILE_NOTIFICATIONS && UNITY_ANDROID && !UNITY_EDITOR
            const string permission = "android.permission.POST_NOTIFICATIONS";
            if (!Permission.HasUserAuthorizedPermission(permission))
                Permission.RequestUserPermission(permission);
#endif
        }

#if MOBILE_NOTIFICATIONS && UNITY_ANDROID && !UNITY_EDITOR
        private static DateTime NextLocalTime(int hour, int minute)
        {
            DateTime now = DateTime.Now;
            var candidate = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0);
            return candidate > now ? candidate : candidate.AddDays(1);
        }
#endif
    }
}
