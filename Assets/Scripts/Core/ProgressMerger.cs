namespace Match3.Core
{
    /// <summary>
    /// Conflict-free merging for cloud sync: per-level MAX stars. Because
    /// <see cref="PlayerProgress.RecordResult"/> is grow-only, the merge is
    /// idempotent and order-independent (a CRDT-style join) — no sync ordering,
    /// retry, or duplicate delivery can ever LOSE progress.
    /// </summary>
    public static class ProgressMerger
    {
        public static PlayerProgress Merge(PlayerProgress local, PlayerProgress cloud)
        {
            var merged = new PlayerProgress();
            CopyInto(local, merged);
            CopyInto(cloud, merged);
            return merged;
        }

        /// <summary>True when both profiles store identical per-level stars.</summary>
        public static bool AreEquivalent(PlayerProgress a, PlayerProgress b)
        {
            if (a == null || b == null)
                return a == b;
            // Entries are ordered by level, so the text forms compare reliably.
            return ProgressSerializer.Serialize(a) == ProgressSerializer.Serialize(b);
        }

        private static void CopyInto(PlayerProgress source, PlayerProgress target)
        {
            if (source == null)
                return;
            foreach (System.Collections.Generic.KeyValuePair<int, int> entry in source.Entries)
                target.RecordResult(entry.Key, entry.Value);
        }
    }

    /// <summary>
    /// Server-side plausibility bounds for leaderboard submissions. The Cloud Code
    /// script (cloudcode/submit_score.js) MUST mirror these numbers — the test below
    /// pins them so a drift shows up as a failing build, not silent cheating headroom.
    /// </summary>
    public static class ScoreBounds
    {
        /// <summary>No legitimate time-attack run scores faster than this.</summary>
        public const int MaxPlausiblePointsPerSecond = 400;

        /// <summary>Runs shorter than this are bogus (instant game-over spam).</summary>
        public const int MinRunSeconds = 5;
    }
}
