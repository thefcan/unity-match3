using System;
using System.Collections.Generic;
using System.Linq;

namespace Match3.Core
{
    /// <summary>What the campaign is asking of the player right now.</summary>
    public enum CampaignGoalKind
    {
        /// <summary>There is a level they have never beaten: play it.</summary>
        Continue,
        /// <summary>Every level is beaten, but some are short of three stars.</summary>
        Improve,
        /// <summary>Every level is beaten with three stars — nothing left to earn.</summary>
        Perfect,
    }

    /// <summary>The level the main menu should point at, and why.</summary>
    public readonly struct CampaignGoal
    {
        public CampaignGoalKind Kind { get; }
        public int Level { get; }

        public CampaignGoal(CampaignGoalKind kind, int level)
        {
            Kind = kind;
            Level = level;
        }
    }

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

        /// <summary>
        /// Where a campaign of <paramref name="levelCount"/> levels should send the
        /// player next. Beating the last level leaves <see cref="HighestUnlocked"/> one
        /// past the end, and a menu that simply clamps it reads "Continue – Level 120"
        /// forever: the game never admits it was finished, and never offers the one
        /// thing still worth doing. So once everything is beaten the goal becomes the
        /// EARLIEST level short of three stars — earliest because those are the
        /// cheapest stars to go back for — and only when none is left does it say so.
        /// </summary>
        public CampaignGoal NextGoal(int levelCount)
        {
            if (levelCount < 1)
                return new CampaignGoal(CampaignGoalKind.Continue, 1);

            if (HighestUnlocked <= levelCount)
                return new CampaignGoal(CampaignGoalKind.Continue, HighestUnlocked);

            for (int level = 1; level <= levelCount; level++)
            {
                if (StarsFor(level) < 3)
                    return new CampaignGoal(CampaignGoalKind.Improve, level);
            }
            return new CampaignGoal(CampaignGoalKind.Perfect, levelCount);
        }

        /// <summary>Records a win; a worse re-run never downgrades the stored stars.</summary>
        public void RecordResult(int level, int stars)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
            if (stars < 0) throw new ArgumentOutOfRangeException(nameof(stars));

            _starsByLevel[level] = Math.Max(StarsFor(level), stars);
        }

        /// <summary>Completed levels and their stars, ordered by level (for serialization).</summary>
        /// <summary>Sum of best stars across every completed level — the album's
        /// pack watermark source (no catalog dependency, O(completed levels)).</summary>
        public int TotalStars => _starsByLevel.Values.Sum();

        public IEnumerable<KeyValuePair<int, int>> Entries =>
            _starsByLevel.OrderBy(entry => entry.Key);
    }
}
