using Match3.Core;

namespace Match3.Game
{
    /// <summary>
    /// Persistence + clock glue for the daily/weekly missions. All generation and
    /// counting rules live in Core's <see cref="MissionCatalog"/>; this class rolls
    /// the day/week over (monotonically — a clock pulled backwards freezes rather
    /// than regenerates), feeds resolved waves into progress, and pays claims into
    /// the booster inventory. Wave progress is persisted by the level-outcome save,
    /// so mid-level file writes never happen.
    /// </summary>
    public static class MissionService
    {
        /// <summary>Rolls the mission day/week forward when the calendar moved.</summary>
        public static void EnsureFresh()
        {
            MetaState state = MetaService.Current;
            int today = MetaService.TodayDayNumber;
            bool changed = false;

            if (today > state.MissionDay)
            {
                state.MissionDay = today;
                for (int i = 0; i < MissionCatalog.DailyCount; i++)
                {
                    state.MissionProgress[i] = 0;
                    state.MissionClaimed[i] = false;
                }
                state.RerolledSlot = -1;
                changed = true;
            }

            int week = today / 7;
            if (week > state.MissionWeek)
            {
                state.MissionWeek = week;
                state.WeeklyProgress = 0;
                state.WeeklyClaimed = false;
                changed = true;
            }

            if (changed)
                MetaService.Save();
        }

        /// <summary>The mission in a daily slot, honouring the day's one reroll.</summary>
        public static MissionDef DefForSlot(int slot)
        {
            MetaState state = MetaService.Current;
            return state.RerolledSlot == slot
                ? MissionCatalog.RerollFor(state.MissionDay, slot)
                : MissionCatalog.DailyFor(state.MissionDay)[slot];
        }

        public static MissionDef WeeklyDef => MissionCatalog.WeeklyFor(MetaService.Current.MissionWeek);

        /// <summary>Feeds one resolved wave into every unclaimed mission (no disk write).</summary>
        public static void ConsumeStep(CascadeStep step)
        {
            EnsureFresh();
            MetaState state = MetaService.Current;

            for (int slot = 0; slot < MissionCatalog.DailyCount; slot++)
            {
                if (state.MissionClaimed[slot])
                    continue;
                MissionDef def = DefForSlot(slot);
                int gain = MissionCatalog.CountFor(def, step);
                if (gain > 0)
                    state.MissionProgress[slot] = System.Math.Min(def.Target, state.MissionProgress[slot] + gain);
            }

            if (!state.WeeklyClaimed)
            {
                MissionDef weekly = WeeklyDef;
                int gain = MissionCatalog.CountFor(weekly, step);
                if (gain > 0)
                    state.WeeklyProgress = System.Math.Min(weekly.Target, state.WeeklyProgress + gain);
            }
        }

        /// <summary>A moves-mode level was won — the WinLevels missions advance here.</summary>
        public static void RegisterWin()
        {
            EnsureFresh();
            MetaState state = MetaService.Current;

            for (int slot = 0; slot < MissionCatalog.DailyCount; slot++)
            {
                if (state.MissionClaimed[slot])
                    continue;
                MissionDef def = DefForSlot(slot);
                if (def.Type == MissionType.WinLevels)
                    state.MissionProgress[slot] = System.Math.Min(def.Target, state.MissionProgress[slot] + 1);
            }

            if (!state.WeeklyClaimed && WeeklyDef.Type == MissionType.WinLevels)
                state.WeeklyProgress = System.Math.Min(WeeklyDef.Target, state.WeeklyProgress + 1);

            MetaService.Save(); // level outcomes already hit the disk — piggyback
        }

        /// <summary>Pays a finished daily slot into the inventory. False when not finished.</summary>
        public static bool Claim(int slot)
        {
            EnsureFresh();
            MetaState state = MetaService.Current;
            MissionDef def = DefForSlot(slot);
            if (state.MissionClaimed[slot] || state.MissionProgress[slot] < def.Target)
                return false;

            state.MissionClaimed[slot] = true;
            MetaService.GrantReward(MissionCatalog.RewardForSlot(slot));
            return true;
        }

        public static bool ClaimWeekly()
        {
            EnsureFresh();
            MetaState state = MetaService.Current;
            if (state.WeeklyClaimed || state.WeeklyProgress < WeeklyDef.Target)
                return false;

            state.WeeklyClaimed = true;
            MetaService.GrantReward(MissionCatalog.WeeklyReward());
            return true;
        }

        /// <summary>Swaps one unclaimed slot for the day's alternate mission. Once per day.</summary>
        public static bool Reroll(int slot)
        {
            EnsureFresh();
            MetaState state = MetaService.Current;
            if (state.RerolledSlot != -1 || state.MissionClaimed[slot])
                return false;

            state.RerolledSlot = slot;
            state.MissionProgress[slot] = 0;
            MetaService.Save();
            return true;
        }

        private static readonly string[] ColorNames = { "red", "green", "blue", "yellow", "purple" };

        /// <summary>Short English line for the mission list UI.</summary>
        public static string Describe(MissionDef def)
        {
            switch (def.Type)
            {
                case MissionType.ClearColor:
                    return $"Clear {def.Target} {ColorNames[System.Math.Clamp(def.Param, 0, ColorNames.Length - 1)]} candies";
                case MissionType.MakeStriped: return $"Create {def.Target} striped candies";
                case MissionType.MakeWrapped: return $"Create {def.Target} wrapped candies";
                case MissionType.MakeCombo: return $"Set off {def.Target} big combos";
                case MissionType.WinLevels: return $"Win {def.Target} levels";
                case MissionType.ClearJelly: return $"Pop {def.Target} jelly squares";
                case MissionType.ClearChocolate: return $"Crumble {def.Target} chocolates";
                default: return $"Deliver {def.Target} ingredients";
            }
        }
    }
}
