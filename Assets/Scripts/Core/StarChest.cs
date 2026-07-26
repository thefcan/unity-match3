using System;
using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>What one chest pays out. Shields protect the win streak from one miss.</summary>
    public readonly struct ChestReward
    {
        public readonly int Hammers;
        public readonly int FreeSwaps;
        public readonly int Shuffles;
        public readonly int StreakShields;

        public ChestReward(int hammers, int freeSwaps, int shuffles, int streakShields)
        {
            Hammers = hammers;
            FreeSwaps = freeSwaps;
            Shuffles = shuffles;
            StreakShields = streakShields;
        }
    }

    /// <summary>
    /// The star chest economy (Toon Blast pattern): every 20 stars fills a repeating
    /// chest, and fixed milestones pay a bigger one on top. Stars come from
    /// PlayerProgress's per-level MAXIMUM, so replaying a beaten level for the same
    /// stars can never farm chests — only first-time and improved results move the
    /// total. Pure math over (total, lastCounted); the Game layer persists
    /// lastCounted and grants the rewards.
    /// </summary>
    public static class StarChest
    {
        public const int StarsPerChest = 20;

        /// <summary>Cumulative-star milestones (60 per chapter, five chapters).</summary>
        public static readonly int[] Milestones = { 60, 120, 180, 240, 300 };

        /// <summary>How many repeating chests opened between the last counted total and now.</summary>
        public static int RepeatingChestsEarned(int totalStars, int lastCountedStars)
        {
            int total = Math.Max(0, totalStars);
            int last = Math.Max(0, lastCountedStars);
            return Math.Max(0, total / StarsPerChest - last / StarsPerChest);
        }

        /// <summary>The milestones crossed in (lastCounted, total].</summary>
        public static int MilestonesCrossed(int totalStars, int lastCountedStars)
        {
            int count = 0;
            foreach (int milestone in Milestones)
            {
                if (lastCountedStars < milestone && totalStars >= milestone)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// The repeating chest's contents cycle so no single booster floods the
        /// inventory; every third chest carries a streak shield.
        /// </summary>
        public static ChestReward RepeatingReward(int chestNumber)
        {
            switch (((chestNumber % 3) + 3) % 3)
            {
                case 0: return new ChestReward(2, 1, 0, 0);
                case 1: return new ChestReward(0, 2, 1, 0);
                default: return new ChestReward(1, 0, 1, 1);
            }
        }

        /// <summary>Milestone chests pay big and always shield the streak.</summary>
        public static ChestReward MilestoneReward() => new ChestReward(2, 2, 2, 1);

        /// <summary>Sums every reward newly earned by moving from lastCounted to total stars.</summary>
        public static ChestReward Collect(int totalStars, int lastCountedStars, out int chestsOpened)
        {
            int hammers = 0, swaps = 0, shuffles = 0, shields = 0;
            chestsOpened = 0;

            int firstChest = Math.Max(0, lastCountedStars) / StarsPerChest + 1;
            int repeating = RepeatingChestsEarned(totalStars, lastCountedStars);
            for (int i = 0; i < repeating; i++)
            {
                ChestReward reward = RepeatingReward(firstChest + i);
                hammers += reward.Hammers;
                swaps += reward.FreeSwaps;
                shuffles += reward.Shuffles;
                shields += reward.StreakShields;
                chestsOpened++;
            }

            int milestones = MilestonesCrossed(totalStars, lastCountedStars);
            for (int i = 0; i < milestones; i++)
            {
                ChestReward reward = MilestoneReward();
                hammers += reward.Hammers;
                swaps += reward.FreeSwaps;
                shuffles += reward.Shuffles;
                shields += reward.StreakShields;
                chestsOpened++;
            }

            return new ChestReward(hammers, swaps, shuffles, shields);
        }

        /// <summary>Stars still needed for the next repeating chest (for the progress bar).</summary>
        public static int StarsTowardNextChest(int totalStars) =>
            ((totalStars % StarsPerChest) + StarsPerChest) % StarsPerChest;
    }
}
