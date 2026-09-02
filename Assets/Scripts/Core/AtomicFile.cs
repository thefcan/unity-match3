using System;
using System.IO;

namespace Match3.Core
{
    /// <summary>
    /// Writes a save file the only way that is safe on a phone: to a sibling temp file
    /// first, then ONE replace onto the real path. The exposure is not theoretical —
    /// the game saves from OnApplicationPause, which is exactly the moment Android is
    /// most likely to kill the process. A torn write there does not announce itself:
    /// both save formats are tolerant "key=value" lines, so half a file loads as a
    /// perfectly valid, quietly smaller save.
    ///
    /// The replace also leaves the previous version behind as a ".bak", which
    /// <see cref="MetaSerializer"/>'s end marker lets a loader fall back to.
    /// </summary>
    public static class AtomicFile
    {
        public const string TempSuffix = ".tmp";
        public const string BackupSuffix = ".bak";

        public static void WriteAllText(string path, string contents)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Need a file path.", nameof(path));
            if (contents == null) throw new ArgumentNullException(nameof(contents));

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string temp = path + TempSuffix;
            File.WriteAllText(temp, contents);

            if (!File.Exists(path))
            {
                File.Move(temp, path);
                return;
            }

            try
            {
                File.Replace(temp, path, path + BackupSuffix);
            }
            catch (Exception e) when (e is PlatformNotSupportedException || e is IOException)
            {
                // Some filesystems (and some IL2CPP targets) refuse Replace. The
                // copy-then-move fallback has a hairline window where the real file is
                // missing, which still beats a half-written one: a missing save loads
                // as a fresh profile, a torn save loads as a wrong one.
                File.Copy(path, path + BackupSuffix, true);
                File.Delete(path);
                File.Move(temp, path);
            }
        }
    }
}
