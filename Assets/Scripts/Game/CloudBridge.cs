using System;

namespace Match3.Game
{
    /// <summary>
    /// The narrow seam between the game and the OPTIONAL cloud assembly
    /// (Match3.Cloud only compiles when the Unity Gaming Services packages are
    /// installed — see its asmdef defineConstraints). The game never references the
    /// cloud side; the cloud side pushes status and progress-refresh signals through
    /// here, and the UI reads them. With no cloud assembly, the defaults simply say
    /// "offline" forever.
    /// </summary>
    public static class CloudBridge
    {
        /// <summary>Shown in the settings panel's cloud row.</summary>
        public static string StatusText { get; private set; } = "Cloud sync: offline";

        public static event Action StatusChanged;

        /// <summary>Raised after a cloud merge changed the local progress (menu re-reads it).</summary>
        public static event Action ProgressRefreshed;

        public static void SetStatus(string text)
        {
            StatusText = text ?? "Cloud sync: offline";
            StatusChanged?.Invoke();
        }

        public static void RaiseProgressRefreshed()
        {
            ProgressRefreshed?.Invoke();
        }
    }
}
