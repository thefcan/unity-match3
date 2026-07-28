using Match3.Core;

namespace Match3.Game
{
    /// <summary>
    /// Clock + disk glue for the Candy Calendar. Every rule — windows, counting,
    /// banking, the race — lives in Core's <see cref="EventCalendar"/> and
    /// <see cref="EventRules"/>; this class only supplies the day number, the
    /// campaign depth (for the blocker substitution) and the save calls, riding
    /// the exact same rails as MissionService: wave progress persists via the
    /// level-outcome save, and <see cref="RegisterWin"/> deliberately writes
    /// nothing because MissionService.RegisterWin's save (one line below it at
    /// the call site) flushes the shared MetaState.
    /// </summary>
    public static class EventService
    {
        /// <summary>Rolls the window forward when the calendar moved (banking the old one).</summary>
        public static void EnsureFresh()
        {
            if (EventRules.EnsureWindow(MetaService.Current, MetaService.TodayDayNumber,
                                        ProgressService.Current.HighestUnlocked))
                MetaService.Save();
        }

        public static EventPhase PhaseToday
        {
            get
            {
                EnsureFresh();
                return EventCalendar.PhaseFor(MetaService.TodayDayNumber);
            }
        }

        public static bool IsWindowActive
        {
            get
            {
                EnsureFresh();
                return EventRules.IsWindowActive(MetaService.Current, MetaService.TodayDayNumber);
            }
        }

        public static EventDef CurrentDef
        {
            get
            {
                EnsureFresh();
                MetaState state = MetaService.Current;
                return EventCalendar.DefForKind(EventRules.KindOf(state), state.EventParam);
            }
        }

        public static int Progress
        {
            get
            {
                EnsureFresh();
                return MetaService.Current.EventProgress;
            }
        }

        public static int DaysLeftToday
        {
            get
            {
                EnsureFresh();
                return EventCalendar.DaysLeft(MetaService.TodayDayNumber);
            }
        }

        /// <summary>Anything the badge should pulse for: an earned tier, a finished race, or a banked toast.</summary>
        public static bool HasClaimable
        {
            get
            {
                EnsureFresh();
                MetaState state = MetaService.Current;
                if (state.EventToastHammers > 0 || state.EventToastFreeSwaps > 0
                    || state.EventToastShuffles > 0 || state.EventToastShields > 0
                    || state.EventToastTrophy > 0 || state.EventToastRescues > 0
                    || state.EventToastPacks > 0)
                    return true;
                if (!EventRules.IsWindowActive(state, MetaService.TodayDayNumber))
                    return false;

                if (EventRules.KindOf(state) == EventKind.Race)
                    return !state.EventRaceClaimed && state.EventProgress >= EventCalendar.RaceTarget;

                EventDef def = EventCalendar.DefForKind(EventRules.KindOf(state), state.EventParam);
                for (int tier = 0; tier < EventCalendar.TierCount; tier++)
                    if (state.EventProgress >= def.TierTarget(tier) && !state.EventTierClaimed[tier])
                        return true;
                return false;
            }
        }

        /// <summary>Feeds one resolved wave into the active window (no disk write — mission idiom).</summary>
        public static void ConsumeStep(CascadeStep step)
        {
            EnsureFresh();
            EventRules.ApplyStep(MetaService.Current, MetaService.TodayDayNumber, step);
        }

        /// <summary>A moves-mode win (stars already relaxed-capped by the caller). No save here.</summary>
        public static void RegisterWin(int level, int stars)
        {
            EnsureFresh();
            EventRules.RegisterWin(MetaService.Current, MetaService.TodayDayNumber, level, stars);
        }

        /// <summary>Pays one earned tier into the inventory (GrantReward saves — one write for flag + loot).</summary>
        public static bool ClaimTier(int tier)
        {
            EnsureFresh();
            if (!EventRules.TryClaimTier(MetaService.Current, MetaService.TodayDayNumber, tier, out ChestReward reward))
                return false;
            MetaService.GrantReward(reward);
            return true;
        }

        /// <summary>Pays a finished race the moment the player crosses the line.</summary>
        public static bool ClaimRace()
        {
            EnsureFresh();
            if (!EventRules.TryClaimRace(MetaService.Current, MetaService.TodayDayNumber,
                                         out ChestReward reward, out int _))
                return false;
            MetaService.GrantReward(reward);
            return true;
        }

        /// <summary>The one-shot "banked while you were away" toast; saves when taken.</summary>
        public static bool TryTakeBankedToast(out ChestReward reward, out int trophy, out int rescues, out int packs)
        {
            EnsureFresh();
            if (!EventRules.TryTakeBankedToast(MetaService.Current, out reward, out trophy, out rescues, out packs))
                return false;
            MetaService.Save();
            return true;
        }

        // ---- Race view helpers ----------------------------------------------------

        public static uint RaceSeed => EventCalendar.WindowSeed(MetaService.Current.EventWindowId);

        /// <summary>A bot's current progress against the player's tick count.</summary>
        public static int BotProgressNow(int botIndex) =>
            EventRules.BotProgress(RaceSeed, botIndex, MetaService.Current.EventProgress);

        public static int RacePlacementNow =>
            EventRules.RacePlacement(RaceSeed, MetaService.Current.EventProgress);

        // ---- Strings (UI copy stays in the Game layer, mission-Describe precedent) --

        public static string Title
        {
            get
            {
                switch (EventRules.KindOf(MetaService.Current))
                {
                    case EventKind.SpecialistWeek: return "SPECIALIST WEEK";
                    case EventKind.BlockerBash: return "BLOCKER BASH";
                    case EventKind.StarSprint: return "STAR SPRINT";
                    case EventKind.Race: return "WEEKEND RACE";
                    default: return "CANDY RUSH";
                }
            }
        }

        private static readonly string[] ColorNames = { "red", "green", "blue", "yellow", "purple" };

        /// <summary>One-line brief under the title; tier rows carry the numbers.</summary>
        public static string Describe(EventDef def)
        {
            switch (def.Kind)
            {
                case EventKind.SpecialistWeek: return "Create special candies";
                case EventKind.BlockerBash: return "Chip away jelly, ice and chocolate";
                case EventKind.StarSprint: return "Collect stars from level wins";
                case EventKind.Race: return $"Win {EventCalendar.RaceTarget} different levels first!";
                default:
                    return $"Clear {ColorNames[System.Math.Clamp(def.Param, 0, ColorNames.Length - 1)]} candies";
            }
        }

        // Five distinct racers per weekend: a seeded start plus a step coprime to
        // the list length walks 5 different indices, so no weekend doubles a name.
        private static readonly string[] BotNames =
        {
            "Bonbon Betty", "Sir Lollipop", "Coco Truffle", "Wafer Wanda", "Gummy Gus",
            "Taffy Tina", "Marshmallow Max", "Jelly Jules", "Rocky Roadie", "Pralinka",
            "Fudge Freddy", "Minty Mo", "Caramelle", "Nougat Nan", "Sherbet Shane",
            "Toffee Tom", "Licorice Liz", "Peppermint Pia", "Sprinkle Sam", "Butterscotch Bo",
        };

        private static readonly int[] CoprimeSteps = { 1, 3, 7, 9, 11, 13, 17, 19 };

        public static string BotName(int botIndex)
        {
            uint seed = RaceSeed;
            int start = (int)(EventCalendar.Hash(seed ^ 0xB0B5u) % (uint)BotNames.Length);
            int step = CoprimeSteps[EventCalendar.Hash(seed ^ 0x51DEu) % (uint)CoprimeSteps.Length];
            return BotNames[(start + botIndex * step) % BotNames.Length];
        }
    }
}
