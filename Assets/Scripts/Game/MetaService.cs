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
        /// Board rewards (moves / special starts) are stored as PENDING and applied
        /// to the next moves-mode level; booster grants go STRAIGHT into the
        /// inventory — there is nothing to defer.
        /// </summary>
        public static StreakReward? Claim()
        {
            StreakStatus status = Status;
            if (status == StreakStatus.AlreadyClaimed)
                return null;

            Current.Streak = status == StreakStatus.Broken ? 1 : Current.Streak + 1;
            Current.LastClaimDay = TodayDayNumber;
            StreakReward reward = DailyStreak.RewardFor(Current.Streak);
            switch (reward.Kind)
            {
                case StreakRewardKind.BoosterHammer: Current.AddBoosters(BoosterKind.Hammer, reward.Amount); break;
                case StreakRewardKind.BoosterFreeSwap: Current.AddBoosters(BoosterKind.FreeSwap, reward.Amount); break;
                case StreakRewardKind.BoosterShuffle: Current.AddBoosters(BoosterKind.Shuffle, reward.Amount); break;
                default: Current.SetPendingReward(reward); break;
            }
            Save();
            return reward;
        }

        // ---- Star chest -------------------------------------------------------------------

        /// <summary>
        /// Converts any newly earned stars into chest rewards (repeating 20-star
        /// chests + milestones), banks them, and advances the counted watermark.
        /// </summary>
        public static ChestReward CollectChests(int totalStars, out int chestsOpened, out int rescuesGranted)
        {
            ChestReward reward = StarChest.Collect(totalStars, Current.LastChestStars, out chestsOpened);
            // Milestone chests also mint one fail-panel Rescue each — counted
            // against the pre-advance watermark, so a re-claim can never re-mint.
            rescuesGranted = StarChest.MilestonesCrossed(totalStars, Current.LastChestStars);
            if (chestsOpened > 0)
            {
                Current.AddBoosters(BoosterKind.Hammer, reward.Hammers);
                Current.AddBoosters(BoosterKind.FreeSwap, reward.FreeSwaps);
                Current.AddBoosters(BoosterKind.Shuffle, reward.Shuffles);
                Current.StreakShields += reward.StreakShields;
                Current.Rescues += rescuesGranted;
                Current.AlbumPacks += chestsOpened; // every chest carries a sticker pack
            }
            Current.LastChestStars = Math.Max(Current.LastChestStars, totalStars);
            Save();
            return reward;
        }

        // ---- Rescues (fail-panel continues) ----------------------------------------------

        public static int Rescues => Current.Rescues;

        /// <summary>Spends one Rescue and persists; false when the shelf is empty.</summary>
        public static bool TrySpendRescue()
        {
            if (Current.Rescues <= 0)
                return false;
            Current.Rescues--;
            Save();
            return true;
        }

        /// <summary>Grants a mission/chest reward bundle to the inventory (no watermark).</summary>
        public static void GrantReward(ChestReward reward)
        {
            Current.AddBoosters(BoosterKind.Hammer, reward.Hammers);
            Current.AddBoosters(BoosterKind.FreeSwap, reward.FreeSwaps);
            Current.AddBoosters(BoosterKind.Shuffle, reward.Shuffles);
            Current.StreakShields += reward.StreakShields;
            Save();
        }

        // ---- Win streak -----------------------------------------------------------------

        public static int WinStreak => Current.WinStreak;

        /// <summary>A moves-mode level is starting — an unfinished previous one breaks the streak.</summary>
        public static void RegisterLevelStart()
        {
            WinStreakRules.RegisterStart(Current);
            Save();
        }

        /// <summary>The level concluded (won or failed) — advance or reset the streak.</summary>
        public static void RegisterLevelOutcome(bool won)
        {
            WinStreakRules.RegisterOutcome(Current, won);
            Save();
        }

        // ---- Booster inventory ---------------------------------------------------------

        public static int BoosterCount(BoosterKind kind) => Current.BoosterCount(kind);

        public static void AddBoosters(BoosterKind kind, int amount)
        {
            Current.AddBoosters(kind, amount);
            Save();
        }

        /// <summary>Spends one booster and persists; false when the shelf is empty.</summary>
        public static bool TrySpendBooster(BoosterKind kind)
        {
            if (!Current.TrySpendBooster(kind))
                return false;
            Save();
            return true;
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

        /// <summary>
        /// Flushes only if the state was ever touched. Mission and event progress
        /// accumulate in memory during a level (their docstrings say "no disk
        /// write"), so a backgrounded app used to lose them — while a booster spent
        /// in the same level hit the disk immediately.
        /// Lazy-safe on purpose: touching Current would CREATE and persist a blank
        /// state on a first launch that never played.
        /// </summary>
        public static void SaveNow()
        {
            if (_current != null)
                Save();
        }

        public static void Save()
        {
            try
            {
                // Temp-then-replace, and it leaves the previous save as a ".bak" that
                // Load falls back to when the live file turns up truncated.
                AtomicFile.WriteAllText(SavePath, MetaSerializer.Serialize(Current));
            }
            catch (Exception)
            {
                // Full disk, permissions, or a serializer that choked — losing a
                // streak beat is acceptable, crashing the app is not. (Load and
                // FileProgressRepository already catch this broadly.)
            }
        }

        private static MetaState Load()
        {
            try
            {
                if (!File.Exists(SavePath))
                    return new MetaState();

                if (MetaSerializer.TryDeserialize(File.ReadAllText(SavePath), out MetaState state))
                    return state;

                // No end marker. Either the file predates the marker (it parsed fine —
                // nothing to do) or it was cut short, which the tolerant parser reads as
                // a valid but SMALLER save: rescues, album packs and every owned sticker
                // sit at the tail. If the atomic writer left a backup, that one is whole
                // by construction, so prefer it over a possibly-shaved live file.
                string backup = SavePath + AtomicFile.BackupSuffix;
                if (File.Exists(backup) &&
                    MetaSerializer.TryDeserialize(File.ReadAllText(backup), out MetaState restored))
                    return restored;

                return state;
            }
            catch (Exception)
            {
                return new MetaState();
            }
        }

        private static string SavePath => Path.Combine(Application.persistentDataPath, "meta.sav");
    }
}
