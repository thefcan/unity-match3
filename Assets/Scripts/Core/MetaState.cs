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

        // In-level booster inventory. New (and corrupt-file) states start with a
        // small starter pack — generosity is the free game's whole economy.
        public int Hammers = 3;
        public int FreeSwaps = 3;
        public int Shuffles = 3;

        /// <summary>Consecutive moves-mode wins (see <see cref="WinStreakRules"/>).</summary>
        public int WinStreak;

        /// <summary>A level started but never concluded — set at start, cleared on win/fail.</summary>
        public bool LevelInProgress;

        public int BoosterCount(BoosterKind kind)
        {
            switch (kind)
            {
                case BoosterKind.Hammer: return Hammers;
                case BoosterKind.FreeSwap: return FreeSwaps;
                default: return Shuffles;
            }
        }

        public void AddBoosters(BoosterKind kind, int amount)
        {
            switch (kind)
            {
                case BoosterKind.Hammer: Hammers = System.Math.Max(0, Hammers + amount); break;
                case BoosterKind.FreeSwap: FreeSwaps = System.Math.Max(0, FreeSwaps + amount); break;
                default: Shuffles = System.Math.Max(0, Shuffles + amount); break;
            }
        }

        /// <summary>Decrements the counter when stock exists; false when the shelf is empty.</summary>
        public bool TrySpendBooster(BoosterKind kind)
        {
            if (BoosterCount(kind) <= 0)
                return false;
            AddBoosters(kind, -1);
            return true;
        }

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
