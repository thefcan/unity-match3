namespace Match3.Core
{
    /// <summary>
    /// Meta progression that isn't per-level stars: the daily streak and the reward
    /// waiting to be applied to the next level. Deliberately separate from
    /// PlayerProgress so the existing progress file format (and its tests, and its
    /// future cloud sync) stay untouched.
    /// </summary>
    public sealed class MetaState
    {
        /// <summary>Day number (days since the app epoch) of the last claim; 0 = never.</summary>
        public int LastClaimDay;

        /// <summary>Consecutive days claimed; 0 = no active streak.</summary>
        public int Streak;

        public StreakRewardKind PendingKind;

        /// <summary>0 = no reward waiting.</summary>
        public int PendingAmount;

        public bool HasPendingReward => PendingAmount > 0;

        public StreakReward TakePendingReward()
        {
            var reward = new StreakReward(PendingKind, PendingAmount);
            PendingAmount = 0;
            PendingKind = StreakRewardKind.ExtraMoves;
            return reward;
        }

        public void SetPendingReward(StreakReward reward)
        {
            PendingKind = reward.Kind;
            PendingAmount = reward.Amount;
        }
    }
}
