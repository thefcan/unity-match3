using System;
using System.IO;
using Match3.Core;
using UnityEngine;

namespace Match3.Game
{
    /// <summary>
    /// Persistence + clock glue for the daily streak (ProgressService's sibling —
    /// separate file, separate format, so progress stays untouched). All rules live
    /// in Core's DailyStreak; this class only supplies "today" from the device's
    /// LOCAL date (a daily ritual should roll over at the player's midnight) and
    /// reads/writes meta.sav.
    /// </summary>
    public static class MetaService
    {
        private static readonly DateTime Epoch = new DateTime(2024, 1, 1);
        private static MetaState _current;

        public static MetaState Current => _current ??= Load();

        public static int TodayDayNumber => (int)(DateTime.Now.Date - Epoch).TotalDays;

        public static StreakStatus Status =>
            DailyStreak.Evaluate(Current.LastClaimDay, TodayDayNumber, Current.Streak);

        /// <summary>The streak day the NEXT claim would land on (drives the calendar UI).</summary>
        public static int NextClaimStreakDay
        {
            get
            {
                switch (Status)
                {
                    case StreakStatus.Claimable: return Current.Streak + 1;
                    case StreakStatus.Broken: return 1;
                    default: return Current.Streak; // already claimed — show today's slot
                }
            }
        }

        /// <summary>
        /// Claims today's reward if possible. A broken streak restarts at day 1.
        /// The reward is stored as PENDING and applied to the next moves-mode level.
        /// </summary>
        public static StreakReward? Claim()
        {
            StreakStatus status = Status;
            if (status == StreakStatus.AlreadyClaimed)
                return null;

            Current.Streak = status == StreakStatus.Broken ? 1 : Current.Streak + 1;
            Current.LastClaimDay = TodayDayNumber;
            StreakReward reward = DailyStreak.RewardFor(Current.Streak);
            Current.SetPendingReward(reward);
            Save();
            return reward;
        }

        /// <summary>Takes the pending reward (if any) — called once per level build.</summary>
        public static StreakReward? ConsumePendingReward()
        {
            if (!Current.HasPendingReward)
                return null;

            StreakReward reward = Current.TakePendingReward();
            Save();
            return reward;
        }

        public static void Save()
        {
            try
            {
                File.WriteAllText(SavePath, MetaSerializer.Serialize(Current));
            }
            catch (IOException)
            {
                // full disk / permissions — losing a streak beat is acceptable
            }
        }

        private static MetaState Load()
        {
            try
            {
                return File.Exists(SavePath)
                    ? MetaSerializer.Deserialize(File.ReadAllText(SavePath))
                    : new MetaState();
            }
            catch (Exception)
            {
                return new MetaState();
            }
        }

        private static string SavePath => Path.Combine(Application.persistentDataPath, "meta.sav");
    }
}
