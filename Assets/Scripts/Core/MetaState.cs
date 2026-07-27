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

        /// <summary>Each shield absorbs ONE streak break (fail or abandon). Chest loot.</summary>
        public int StreakShields;

        /// <summary>Star total already converted into chests (see <see cref="StarChest"/>).</summary>
        public int LastChestStars;

        /// <summary>Highest Candy Town global stage already celebrated (see <see cref="TownRules"/>).</summary>
        public int LastSeenTownStage;

        // ---- Daily/weekly missions (definitions regenerate from the day number;
        // only progress, claims and the one reroll are state) -----------------------
        public int MissionDay;
        public readonly int[] MissionProgress = new int[MissionCatalog.DailyCount];
        public readonly bool[] MissionClaimed = new bool[MissionCatalog.DailyCount];
        /// <summary>-1 = the daily reroll is still available; otherwise the slot it replaced.</summary>
        public int RerolledSlot = -1;
        public int MissionWeek;
        public int WeeklyProgress;
        public bool WeeklyClaimed;

        // ---- Time-limited events (the active window's kind/param are SNAPSHOTTED
        // here at open — see Events.cs — so past windows never re-derive; meta.sav
        // stores ints only). APPEND: every field below is serialized. ------------
        /// <summary>Window id the stored event fields belong to; only ever rolls forward.</summary>
        public int EventWindowId;
        /// <summary>(int)EventKind snapshot taken when the window opened.</summary>
        public int EventKindId;
        /// <summary>CandyRush colour snapshot; 0 for the other kinds.</summary>
        public int EventParam;
        /// <summary>Objective count, or race ticks (the kinds are exclusive).</summary>
        public int EventProgress;
        public readonly bool[] EventTierClaimed = new bool[EventCalendar.TierCount];
        public bool EventRaceClaimed;
        /// <summary>Levels already ticked this race window (0 = empty slot) — each level races once.</summary>
        public readonly int[] EventRaceLevels = new int[EventCalendar.RaceTarget];

        // Permanent trophy shelf (race podium; a finished midweek tier 3 mints bronze).
        public int TrophyGold;
        public int TrophySilver;
        public int TrophyBronze;

        // One-shot "banked while you were away" toast, cleared once shown.
        public int EventToastHammers;
        public int EventToastFreeSwaps;
        public int EventToastShuffles;
        public int EventToastShields;
        /// <summary>0 none, 1 bronze, 2 silver, 3 gold.</summary>
        public int EventToastTrophy;

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
