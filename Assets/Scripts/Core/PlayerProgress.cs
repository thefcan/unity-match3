using System;
using System.Collections.Generic;
using System.Linq;

namespace Match3.Core
{
    /// <summary>
    /// The player's campaign progress: which levels are completed and the best star
    /// rating for each. Levels unlock sequentially — level N is playable once N-1 is
    /// completed (a 0-star win still completes a level; stars are a bonus).
    /// </summary>
    public sealed class PlayerProgress
    {
        private readonly Dictionary<int, int> _starsByLevel = new Dictionary<int, int>();

        /// <summary>Best stars earned on a level (0 for never-completed OR a 0-star win).</summary>
        public int StarsFor(int level) => _starsByLevel.TryGetValue(level, out int stars) ? stars : 0;

        public bool IsCompleted(int level) => _starsByLevel.ContainsKey(level);

        /// <summary>The highest level the player may attempt (1 on a fresh profile).</summary>
        public int HighestUnlocked =>
            _starsByLevel.Count == 0 ? 1 : _starsByLevel.Keys.Max() + 1;

        public bool IsUnlocked(int level) => level >= 1 && level <= HighestUnlocked;

        /// <summary>Records a win; a worse re-run never downgrades the stored stars.</summary>
        public void RecordResult(int level, int stars)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
            if (stars < 0) throw new ArgumentOutOfRangeException(nameof(stars));

            _starsByLevel[level] = Math.Max(StarsFor(level), stars);
        }

        /// <summary>Completed levels and their stars, ordered by level (for serialization).</summary>
        public IEnumerable<KeyValuePair<int, int>> Entries =>
            _starsByLevel.OrderBy(entry => entry.Key);
    }
}
