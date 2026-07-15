using System;
using System.Text;

namespace Match3.Core
{
    /// <summary>
    /// Progress persistence format: one "level=stars" line per completed level.
    /// Hand-rolled instead of JSON so it stays dependency-free in the engine-free
    /// core (JsonUtility lives in UnityEngine) and trivially diffable. Parsing is
    /// deliberately forgiving — any malformed line is skipped, and fully corrupt
    /// input degrades to a fresh profile rather than an exception at startup.
    /// </summary>
    public static class ProgressSerializer
    {
        public static string Serialize(PlayerProgress progress)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));

            var builder = new StringBuilder();
            foreach (var entry in progress.Entries)
                builder.Append(entry.Key).Append('=').Append(entry.Value).Append('\n');
            return builder.ToString();
        }

        public static PlayerProgress Deserialize(string text)
        {
            var progress = new PlayerProgress();
            if (string.IsNullOrEmpty(text))
                return progress;

            foreach (string line in text.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0) continue;

                int separator = trimmed.IndexOf('=');
                if (separator <= 0) continue;

                if (int.TryParse(trimmed.Substring(0, separator), out int level) && level >= 1 &&
                    int.TryParse(trimmed.Substring(separator + 1), out int stars) && stars >= 0)
                {
                    progress.RecordResult(level, Math.Min(stars, 3));
                }
            }
            return progress;
        }
    }
}
