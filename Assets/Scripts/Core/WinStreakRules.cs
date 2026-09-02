using System;

namespace Match3.Core
{
    /// <summary>
    /// The consecutive-win streak ("Butler's Gift" pattern): every win in a row
    /// pre-loads more special candies onto the next board, and ANY miss resets it —
    /// a loss, or abandoning a level mid-run (quit to menu, restart, app killed).
    /// Abandonment is caught structurally: a level marks itself "in progress" at
    /// start, and a new start while the previous one never concluded counts as a
    /// fail. Pure rules over <see cref="MetaState"/>; the Game layer supplies
    /// persistence and timing.
    /// </summary>
    public static class WinStreakRules
    {
        /// <summary>The preload ladder caps at striped + wrapped + colour bomb.</summary>
        public const int MaxPreloadTier = 3;

        /// <summary>Call when a moves-mode level begins. An unfinished previous level breaks the streak.</summary>
        public static void RegisterStart(MetaState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.LevelInProgress)
                BreakStreak(state); // the last level was abandoned
            state.LevelInProgress = true;
        }

        /// <summary>Call exactly once when the level concludes.</summary>
        public static void RegisterOutcome(MetaState state, bool won)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (won)
                state.WinStreak++;
            else
                BreakStreak(state);
            state.LevelInProgress = false;
        }

        /// <summary>A shield (chest loot) absorbs one break; otherwise the streak resets.</summary>
        private static void BreakStreak(MetaState state)
        {
            if (state.WinStreak <= 0)
                return; // nothing to protect — spending a shield here is pure waste
            if (state.StreakShields > 0)
                state.StreakShields--;
            else
                state.WinStreak = 0;
        }

        /// <summary>How many specials the next board starts with (1 win = striped, 2 = +wrapped, 3+ = +bomb).</summary>
        public static int PreloadCount(int winStreak) =>
            Math.Min(Math.Max(winStreak, 0), MaxPreloadTier);
    }
}
