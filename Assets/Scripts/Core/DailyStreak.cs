namespace Match3.Core
{
    public enum StreakStatus
    {
        /// <summary>Today's reward can be claimed (fresh player or consecutive day).</summary>
        Claimable,
        /// <summary>Already claimed today (or the clock rolled backwards).</summary>
        AlreadyClaimed,
        /// <summary>A day was missed — the streak resets to day 1 on the next claim.</summary>
        Broken,
    }

    public enum StreakRewardKind
    {
        ExtraMoves,
        StartStriped,
        StartWrapped,
        StartColorBomb,
    }

    /// <summary>What a daily claim grants — applied to the NEXT moves-mode level.</summary>
    public readonly struct StreakReward
    {
        public readonly StreakRewardKind Kind;
        public readonly int Amount;

        public StreakReward(StreakRewardKind kind, int amount)
        {
            Kind = kind;
            Amount = amount;
        }
    }

    /// <summary>
    /// Daily login-streak rules — pure and clock-free: callers pass day numbers
    /// (days since an arbitrary epoch), so tests can play out any calendar. The
    /// reward ladder repeats every 7 days and peaks at a colour-bomb head start.
    /// A device-clock rollback counts as AlreadyClaimed (mild anti-cheat: going
    /// backwards can never farm extra claims).
    /// </summary>
    public static class DailyStreak
    {
        public const int CycleLength = 7;

        public static StreakStatus Evaluate(int lastClaimDay, int todayDay, int streak)
        {
            if (streak <= 0 || lastClaimDay <= 0)
                return StreakStatus.Claimable; // never claimed before

            if (todayDay <= lastClaimDay)
                return StreakStatus.AlreadyClaimed;

            return todayDay == lastClaimDay + 1 ? StreakStatus.Claimable : StreakStatus.Broken;
        }

        /// <summary>The reward for the Nth consecutive day (1-based, cycles every 7).</summary>
        public static StreakReward RewardFor(int streakDay)
        {
            int day = ((streakDay - 1) % CycleLength + CycleLength) % CycleLength + 1;
            switch (day)
            {
                case 1: return new StreakReward(StreakRewardKind.ExtraMoves, 2);
                case 2: return new StreakReward(StreakRewardKind.ExtraMoves, 3);
                case 3: return new StreakReward(StreakRewardKind.StartStriped, 1);
                case 4: return new StreakReward(StreakRewardKind.ExtraMoves, 4);
                case 5: return new StreakReward(StreakRewardKind.StartWrapped, 1);
                case 6: return new StreakReward(StreakRewardKind.ExtraMoves, 5);
                default: return new StreakReward(StreakRewardKind.StartColorBomb, 1);
            }
        }
    }
}
