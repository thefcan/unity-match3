using System;

namespace Match3.Core
{
    /// <summary>Where today falls in the weekly event cadence.</summary>
    public enum EventPhase
    {
        /// <summary>Monday — no event, the anticipation beat between windows.</summary>
        Off,
        /// <summary>Tuesday-Thursday — a three-day objective event.</summary>
        Midweek,
        /// <summary>Friday-Sunday — the three-day bot race.</summary>
        Weekend,
    }

    public enum EventKind
    {
        // The ordinal is snapshotted into meta.sav (eventKindId), so these must
        // only ever be APPENDED — and a new kind also needs a debut window in
        // EventCalendar.RotationIntroWindow (see the FromRoll lesson there).
        /// <summary>Clear N candies of the seeded colour (Param = colour index).</summary>
        CandyRush,
        /// <summary>Create N special candies (striped, wrapped, fish).</summary>
        SpecialistWeek,
        /// <summary>Chip N blocker layers (jelly, frosting, locks, chocolate).</summary>
        BlockerBash,
        /// <summary>Earn N stars from moves-mode wins (relaxed's 1-star cap applies).</summary>
        StarSprint,
        /// <summary>Weekend race: win RaceTarget distinct levels before the bots do.</summary>
        Race,
    }

    /// <summary>One event: what to do and the three milestone targets.</summary>
    public readonly struct EventDef
    {
        public readonly EventKind Kind;
        public readonly int Param;
        public readonly int Tier1;
        public readonly int Tier2;
        public readonly int Tier3;

        public EventDef(EventKind kind, int param, int tier1, int tier2, int tier3)
        {
            Kind = kind;
            Param = param;
            Tier1 = tier1;
            Tier2 = tier2;
            Tier3 = tier3;
        }

        public int TierTarget(int tier) => tier <= 0 ? Tier1 : tier == 1 ? Tier2 : Tier3;
    }

    /// <summary>
    /// The deterministic weekly event calendar ("Candy Calendar"). Everything —
    /// which window is open, what it asks for, what the race bots do — derives
    /// from the day number and a seed, exactly like MissionCatalog: no clock, no
    /// server, no persistence in here. The app epoch (2024-01-01) is a Monday, so
    /// <c>dayNumber % 7 == 0</c> is always Monday.
    /// </summary>
    public static class EventCalendar
    {
        public const int WindowLengthDays = 3;
        public const int TierCount = 3;
        /// <summary>FROZEN: MetaSerializer writes one eventRaceLevelN key per slot.</summary>
        public const int RaceTarget = 10;
        public const int BotCount = 5;
        /// <summary>Jelly enters the campaign here — no BlockerBash before it.</summary>
        public const int BlockerContentLevel = 8;

        /// <summary>0 = Monday … 6 = Sunday, negative-safe.</summary>
        public static int DayOfWeekIndex(int dayNumber) => ((dayNumber % 7) + 7) % 7;

        public static EventPhase PhaseFor(int dayNumber)
        {
            int dow = DayOfWeekIndex(dayNumber);
            if (dow == 0)
                return EventPhase.Off;
            return dow <= 3 ? EventPhase.Midweek : EventPhase.Weekend;
        }

        /// <summary>
        /// Strictly increasing over calendar time (what the monotonic freeze keys
        /// on): Tue-Thu of week w is 2w, Fri-Sun is 2w+1. Monday is the sentinel -1.
        /// </summary>
        public static int WindowIdFor(int dayNumber)
        {
            EventPhase phase = PhaseFor(dayNumber);
            if (phase == EventPhase.Off)
                return -1;
            return WeekOf(dayNumber) * 2 + (phase == EventPhase.Weekend ? 1 : 0);
        }

        /// <summary>First day of a (non-negative) window id.</summary>
        public static int WindowStartDay(int windowId) =>
            windowId / 2 * 7 + (((windowId % 2) + 2) % 2 == 0 ? 1 : 4);

        /// <summary>Whole days the current window still covers, counting today (0 on Mondays).</summary>
        public static int DaysLeft(int dayNumber)
        {
            int dow = DayOfWeekIndex(dayNumber);
            if (dow == 0)
                return 0;
            return dow <= 3 ? 4 - dow : 7 - dow;
        }

        public static uint WindowSeed(int windowId) => Hash((uint)(windowId * 2654435761u + 8191u));

        // The midweek rotation. APPEND ONLY, and every appended kind needs a debut
        // windowId greater than any already-shipped date: windows before the debut
        // keep computing with the shorter cycle, so history never reshuffles (the
        // mission FromRoll modulus could never learn a 9th case — this table can).
        private static readonly EventKind[] MidweekRotation =
        {
            EventKind.CandyRush, EventKind.SpecialistWeek, EventKind.BlockerBash, EventKind.StarSprint,
        };
        private static readonly int[] RotationIntroWindow = { 0, 0, 0, 0 };

        /// <summary>
        /// The kind a window runs. Odd windows race; even ones cycle the midweek
        /// rotation, substituting CandyRush while the campaign hasn't taught
        /// blockers yet. Callers snapshot the result into MetaState at window
        /// open — nothing downstream ever re-derives it.
        /// </summary>
        public static EventKind KindFor(int windowId, int highestUnlocked)
        {
            if (((windowId % 2) + 2) % 2 == 1)
                return EventKind.Race;

            int available = 0;
            for (int i = 0; i < RotationIntroWindow.Length; i++)
                if (RotationIntroWindow[i] <= windowId)
                    available++;
            if (available == 0)
                return EventKind.CandyRush; // pre-epoch windows are never active anyway

            int index = ((windowId / 2 % available) + available) % available;
            for (int i = 0; i < MidweekRotation.Length; i++)
            {
                if (RotationIntroWindow[i] > windowId)
                    continue;
                if (index-- == 0)
                {
                    EventKind kind = MidweekRotation[i];
                    if (kind == EventKind.BlockerBash && highestUnlocked < BlockerContentLevel)
                        return EventKind.CandyRush;
                    return kind;
                }
            }
            return EventKind.CandyRush; // unreachable: index < available entries
        }

        public static int ParamFor(int windowId, EventKind kind) =>
            kind == EventKind.CandyRush ? (int)(Hash(WindowSeed(windowId)) % 5u) : 0;

        /// <summary>
        /// Targets rebuilt from a snapshot. A 3-day event lands around three
        /// daily-mission-days of volume at tier 3 (missions: 60-80 colour,
        /// 6 striped, 12 jelly) — a push, not a grind.
        /// </summary>
        public static EventDef DefForKind(EventKind kind, int param)
        {
            switch (kind)
            {
                case EventKind.SpecialistWeek: return new EventDef(kind, 0, 8, 18, 32);
                case EventKind.BlockerBash: return new EventDef(kind, 0, 15, 35, 60);
                case EventKind.StarSprint: return new EventDef(kind, 0, 6, 14, 24);
                case EventKind.Race: return new EventDef(kind, 0, RaceTarget, RaceTarget, RaceTarget);
                default: return new EventDef(EventKind.CandyRush, param, 70, 160, 280);
            }
        }

        private static int WeekOf(int dayNumber) =>
            (dayNumber - DayOfWeekIndex(dayNumber)) / 7; // numerator is a multiple of 7 → floor for any sign

        internal static uint Hash(uint x)
        {
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            return x;
        }
    }

    /// <summary>
    /// Window lifecycle, progress counting, claims and the weekend-race math —
    /// pure rules over <see cref="MetaState"/> in the WinStreakRules mould. The
    /// Game layer supplies the day number and the disk; everything here is
    /// EditMode-testable, including the clock-rollback freeze the mission layer
    /// never got coverage for.
    /// </summary>
    public static class EventRules
    {
        /// <summary>Tier payouts escalate and end with a shield; tier 3 also mints a bronze trophy.</summary>
        public static ChestReward TierReward(int tier)
        {
            switch (tier)
            {
                case 0: return new ChestReward(1, 0, 1, 0);
                case 1: return new ChestReward(2, 1, 1, 0);
                default: return new ChestReward(2, 2, 2, 1);
            }
        }

        /// <summary>
        /// Rolls the stored window forward when the calendar moved. Monotone: a
        /// clock pulled backwards (or Monday's -1) changes NOTHING — the event
        /// just goes invisible via <see cref="IsWindowActive"/> until real time
        /// returns. A genuine rollover first BANKS the outgoing window's earned
        /// but unclaimed rewards straight into the state (generosity: nothing a
        /// player earned is ever lost to the calendar) plus the one-shot toast
        /// fields, then snapshots the new window. Returns true when state
        /// changed and the caller should save.
        /// </summary>
        public static bool EnsureWindow(MetaState state, int dayNumber, int highestUnlocked)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            int current = EventCalendar.WindowIdFor(dayNumber);
            if (current <= state.EventWindowId)
                return false;

            BankOutgoing(state);

            EventKind kind = EventCalendar.KindFor(current, highestUnlocked);
            state.EventWindowId = current;
            state.EventKindId = (int)kind;
            state.EventParam = EventCalendar.ParamFor(current, kind);
            state.EventProgress = 0;
            for (int i = 0; i < state.EventTierClaimed.Length; i++)
                state.EventTierClaimed[i] = false;
            state.EventRaceClaimed = false;
            for (int i = 0; i < state.EventRaceLevels.Length; i++)
                state.EventRaceLevels[i] = 0;
            return true;
        }

        /// <summary>The stored window is the one the calendar shows today.</summary>
        public static bool IsWindowActive(MetaState state, int dayNumber) =>
            EventCalendar.PhaseFor(dayNumber) != EventPhase.Off
            && EventCalendar.WindowIdFor(dayNumber) == state.EventWindowId;

        public static EventKind KindOf(MetaState state)
        {
            // A save written by a NEWER build may carry a kind this build never
            // learned; degrade to CandyRush rather than misread it as Race.
            int id = state.EventKindId;
            return id >= (int)EventKind.CandyRush && id <= (int)EventKind.Race
                ? (EventKind)id
                : EventKind.CandyRush;
        }

        /// <summary>How much one resolved wave advances the objective kinds.</summary>
        public static int CountFor(EventKind kind, int param, CascadeStep step)
        {
            switch (kind)
            {
                case EventKind.CandyRush:
                {
                    int count = 0;
                    foreach (ClearedTile cleared in step.Cleared)
                        if (cleared.Tile.ColorIndex == param)
                            count++;
                    return count;
                }
                case EventKind.SpecialistWeek:
                {
                    int count = 0;
                    foreach (SpecialCreation creation in step.Creations)
                    {
                        switch (creation.Created.Kind)
                        {
                            case TileKind.StripedH:
                            case TileKind.StripedV:
                            case TileKind.Wrapped:
                            case TileKind.Fish:
                                count++;
                                break;
                        }
                    }
                    return count;
                }
                case EventKind.BlockerBash:
                {
                    int count = step.JellyHits.Count + step.FrostingHits.Count + step.LockBreaks.Count;
                    foreach (ClearedTile cleared in step.Cleared)
                        if (cleared.Tile.Kind == TileKind.Chocolate)
                            count++;
                    return count;
                }
                default:
                    return 0; // StarSprint and Race advance on level outcomes
            }
        }

        /// <summary>Feeds a wave into the active window (no-op when none is).</summary>
        public static void ApplyStep(MetaState state, int dayNumber, CascadeStep step)
        {
            if (!IsWindowActive(state, dayNumber))
                return;
            EventKind kind = KindOf(state);
            int gain = CountFor(kind, state.EventParam, step);
            if (gain <= 0)
                return;
            EventDef def = EventCalendar.DefForKind(kind, state.EventParam);
            state.EventProgress = Math.Min(def.Tier3, state.EventProgress + gain);
        }

        /// <summary>
        /// Feeds a moves-mode win into the active window: StarSprint banks the
        /// (already relaxed-capped) stars, the race ticks once per DISTINCT level
        /// — replaying the same level all weekend advances nobody.
        /// </summary>
        public static void RegisterWin(MetaState state, int dayNumber, int level, int stars)
        {
            if (!IsWindowActive(state, dayNumber))
                return;
            switch (KindOf(state))
            {
                case EventKind.StarSprint:
                {
                    EventDef def = EventCalendar.DefForKind(EventKind.StarSprint, 0);
                    state.EventProgress = Math.Min(def.Tier3, state.EventProgress + Math.Max(0, stars));
                    break;
                }
                case EventKind.Race:
                {
                    if (state.EventProgress >= EventCalendar.RaceTarget || level <= 0)
                        return;
                    for (int i = 0; i < state.EventRaceLevels.Length; i++)
                        if (state.EventRaceLevels[i] == level)
                            return; // this level already raced this window
                    for (int i = 0; i < state.EventRaceLevels.Length; i++)
                    {
                        if (state.EventRaceLevels[i] == 0)
                        {
                            state.EventRaceLevels[i] = level;
                            state.EventProgress++;
                            return;
                        }
                    }
                    break;
                }
            }
        }

        public static int TiersEarned(EventDef def, int progress)
        {
            int count = 0;
            for (int tier = 0; tier < EventCalendar.TierCount; tier++)
                if (progress >= def.TierTarget(tier))
                    count++;
            return count;
        }

        /// <summary>
        /// Claims one earned tier: flips the flag (and mints tier 3's bronze) in
        /// here; the caller grants the returned boosters through its own save
        /// path so the claim and the payout share one disk write.
        /// </summary>
        public static bool TryClaimTier(MetaState state, int dayNumber, int tier, out ChestReward reward)
        {
            reward = default;
            if (tier < 0 || tier >= EventCalendar.TierCount)
                return false;
            if (!IsWindowActive(state, dayNumber))
                return false;
            EventKind kind = KindOf(state);
            if (kind == EventKind.Race)
                return false;
            EventDef def = EventCalendar.DefForKind(kind, state.EventParam);
            if (state.EventProgress < def.TierTarget(tier) || state.EventTierClaimed[tier])
                return false;

            state.EventTierClaimed[tier] = true;
            if (tier == EventCalendar.TierCount - 1)
                state.TrophyBronze++;
            reward = TierReward(tier);
            return true;
        }

        /// <summary>Claims a FINISHED race on the spot (placement is final the moment the player crosses).</summary>
        public static bool TryClaimRace(MetaState state, int dayNumber, out ChestReward reward, out int placement)
        {
            reward = default;
            placement = 0;
            if (!IsWindowActive(state, dayNumber) || KindOf(state) != EventKind.Race)
                return false;
            if (state.EventRaceClaimed || state.EventProgress < EventCalendar.RaceTarget)
                return false;

            uint seed = EventCalendar.WindowSeed(state.EventWindowId);
            placement = RacePlacement(seed, state.EventProgress);
            state.EventRaceClaimed = true;
            ApplyTrophy(state, RaceTrophy(placement, state.EventProgress));
            reward = RaceReward(placement, state.EventProgress);
            return true;
        }

        /// <summary>Takes (and clears) the "banked while you were away" toast; false when there is none.</summary>
        public static bool TryTakeBankedToast(MetaState state, out ChestReward reward, out int trophy)
        {
            reward = new ChestReward(state.EventToastHammers, state.EventToastFreeSwaps,
                                     state.EventToastShuffles, state.EventToastShields);
            trophy = state.EventToastTrophy;
            bool any = reward.Hammers > 0 || reward.FreeSwaps > 0 || reward.Shuffles > 0
                       || reward.StreakShields > 0 || trophy > 0;
            state.EventToastHammers = 0;
            state.EventToastFreeSwaps = 0;
            state.EventToastShuffles = 0;
            state.EventToastShields = 0;
            state.EventToastTrophy = 0;
            return any;
        }

        // ---- Weekend race math ----------------------------------------------------

        /// <summary>Chance (percent) a bot gains ground on any given tick.</summary>
        public static int BotBaseRatePercent(int botIndex)
        {
            switch (botIndex)
            {
                case 0: return 55;
                case 1: return 70;
                case 2: return 80;
                case 3: return 90;
                default: return 95;
            }
        }

        /// <summary>
        /// The drama curve: bots sprint out of the gate (ticks 1-3) and fade in
        /// the home stretch (tick 8+). Shaped by the SEED and the tick alone —
        /// never by reading the player — so standings stay a pure function.
        /// </summary>
        public static int EffectiveRatePercent(int basePercent, int tick)
        {
            if (tick <= 3)
                return Math.Min(100, basePercent + 35);
            if (tick >= 8)
                return Math.Max(15, basePercent - 25);
            return basePercent;
        }

        /// <summary>0, 1 or 2 steps for one bot on one tick; expectation is exactly eff/100.</summary>
        public static int BotIncrement(uint seed, int botIndex, int tick)
        {
            int eff = EffectiveRatePercent(BotBaseRatePercent(botIndex), tick);
            uint roll = EventCalendar.Hash(seed ^ (uint)(botIndex + 1) * 0x9E3779B9u ^ (uint)tick * 0x85EBCA6Bu) % 100u;
            int twoChance = eff / 4;
            int oneChance = eff - 2 * twoChance;
            if (roll < (uint)twoChance)
                return 2;
            return roll < (uint)(twoChance + oneChance) ? 1 : 0;
        }

        /// <summary>A bot's total after the player's first <paramref name="ticks"/> ticks (monotone in ticks).</summary>
        public static int BotProgress(uint seed, int botIndex, int ticks)
        {
            int progress = 0;
            for (int tick = 1; tick <= ticks; tick++)
                progress += BotIncrement(seed, botIndex, tick);
            return progress;
        }

        /// <summary>
        /// 1-based rank among the six racers. A finished player is only beaten by
        /// bots that crossed on a strictly EARLIER tick; an unfinished one ranks
        /// below bots strictly ahead. All ties go to the player.
        /// </summary>
        public static int RacePlacement(uint seed, int ticks)
        {
            int ahead = 0;
            if (ticks >= EventCalendar.RaceTarget)
            {
                for (int bot = 0; bot < EventCalendar.BotCount; bot++)
                    if (BotProgress(seed, bot, EventCalendar.RaceTarget - 1) >= EventCalendar.RaceTarget)
                        ahead++;
            }
            else
            {
                for (int bot = 0; bot < EventCalendar.BotCount; bot++)
                    if (BotProgress(seed, bot, ticks) > ticks)
                        ahead++;
            }
            return 1 + ahead;
        }

        /// <summary>Podium pays big; anyone who actually raced (1+ wins) gets a participation nod.</summary>
        public static ChestReward RaceReward(int placement, int ticks)
        {
            if (ticks < 1)
                return default;
            switch (placement)
            {
                case 1: return new ChestReward(3, 3, 2, 1);
                case 2: return new ChestReward(2, 2, 1, 1);
                case 3: return new ChestReward(1, 1, 1, 0);
                default: return new ChestReward(1, 0, 1, 0);
            }
        }

        /// <summary>0 none, 1 bronze, 2 silver, 3 gold. Trophies demand a real run (3+ wins).</summary>
        public static int RaceTrophy(int placement, int ticks)
        {
            if (ticks < 3)
                return 0;
            switch (placement)
            {
                case 1: return 3;
                case 2: return 2;
                case 3: return 1;
                default: return 0;
            }
        }

        /// <summary>
        /// Pays the outgoing window's earned-but-unclaimed rewards into the state
        /// and the toast fields. Reads ONLY stored ints — never the calendar — so
        /// a finished window banks identically no matter when the player returns.
        /// </summary>
        private static void BankOutgoing(MetaState state)
        {
            int hammers = 0, freeSwaps = 0, shuffles = 0, shields = 0, trophy = 0;

            EventKind kind = KindOf(state);
            if (kind == EventKind.Race)
            {
                if (!state.EventRaceClaimed && state.EventProgress > 0)
                {
                    uint seed = EventCalendar.WindowSeed(state.EventWindowId);
                    int placement = RacePlacement(seed, state.EventProgress);
                    ChestReward reward = RaceReward(placement, state.EventProgress);
                    hammers += reward.Hammers;
                    freeSwaps += reward.FreeSwaps;
                    shuffles += reward.Shuffles;
                    shields += reward.StreakShields;
                    trophy = RaceTrophy(placement, state.EventProgress);
                }
            }
            else
            {
                EventDef def = EventCalendar.DefForKind(kind, state.EventParam);
                for (int tier = 0; tier < EventCalendar.TierCount; tier++)
                {
                    if (state.EventProgress < def.TierTarget(tier) || state.EventTierClaimed[tier])
                        continue;
                    ChestReward reward = TierReward(tier);
                    hammers += reward.Hammers;
                    freeSwaps += reward.FreeSwaps;
                    shuffles += reward.Shuffles;
                    shields += reward.StreakShields;
                    if (tier == EventCalendar.TierCount - 1)
                        trophy = 1;
                }
            }

            if (hammers == 0 && freeSwaps == 0 && shuffles == 0 && shields == 0 && trophy == 0)
                return;

            state.AddBoosters(BoosterKind.Hammer, hammers);
            state.AddBoosters(BoosterKind.FreeSwap, freeSwaps);
            state.AddBoosters(BoosterKind.Shuffle, shuffles);
            state.StreakShields += shields;
            ApplyTrophy(state, trophy);

            state.EventToastHammers += hammers;
            state.EventToastFreeSwaps += freeSwaps;
            state.EventToastShuffles += shuffles;
            state.EventToastShields += shields;
            if (trophy > state.EventToastTrophy)
                state.EventToastTrophy = trophy;
        }

        private static void ApplyTrophy(MetaState state, int trophy)
        {
            switch (trophy)
            {
                case 3: state.TrophyGold++; break;
                case 2: state.TrophySilver++; break;
                case 1: state.TrophyBronze++; break;
            }
        }
    }
}
