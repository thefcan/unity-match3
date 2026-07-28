using System;
using System.Text;

namespace Match3.Core
{
    /// <summary>
    /// MetaState &lt;-&gt; text, in the same hand-rolled "key=value" line style as
    /// ProgressSerializer (no JSON dependency, Core-testable). Tolerant on the way
    /// in: unknown keys are ignored and any parse failure returns a FRESH state —
    /// a corrupt meta file must never take the game down, losing a streak is the
    /// acceptable worst case.
    /// </summary>
    public static class MetaSerializer
    {
        public static string Serialize(MetaState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var sb = new StringBuilder();
            sb.Append("lastClaimDay=").Append(state.LastClaimDay).Append('\n');
            sb.Append("streak=").Append(state.Streak).Append('\n');
            sb.Append("pendingKind=").Append((int)state.PendingKind).Append('\n');
            sb.Append("pendingAmount=").Append(state.PendingAmount).Append('\n');
            sb.Append("hammers=").Append(state.Hammers).Append('\n');
            sb.Append("freeSwaps=").Append(state.FreeSwaps).Append('\n');
            sb.Append("shuffles=").Append(state.Shuffles).Append('\n');
            sb.Append("winStreak=").Append(state.WinStreak).Append('\n');
            sb.Append("levelInProgress=").Append(state.LevelInProgress ? 1 : 0).Append('\n');
            sb.Append("streakShields=").Append(state.StreakShields).Append('\n');
            sb.Append("lastChestStars=").Append(state.LastChestStars).Append('\n');
            sb.Append("lastSeenTownStage=").Append(state.LastSeenTownStage).Append('\n');
            sb.Append("missionDay=").Append(state.MissionDay).Append('\n');
            for (int i = 0; i < MissionCatalog.DailyCount; i++)
            {
                sb.Append("missionProgress").Append(i).Append('=').Append(state.MissionProgress[i]).Append('\n');
                sb.Append("missionClaimed").Append(i).Append('=').Append(state.MissionClaimed[i] ? 1 : 0).Append('\n');
            }
            sb.Append("rerolledSlot=").Append(state.RerolledSlot).Append('\n');
            sb.Append("missionWeek=").Append(state.MissionWeek).Append('\n');
            sb.Append("weeklyProgress=").Append(state.WeeklyProgress).Append('\n');
            sb.Append("weeklyClaimed=").Append(state.WeeklyClaimed ? 1 : 0).Append('\n');
            sb.Append("eventWindowId=").Append(state.EventWindowId).Append('\n');
            sb.Append("eventKindId=").Append(state.EventKindId).Append('\n');
            sb.Append("eventParam=").Append(state.EventParam).Append('\n');
            sb.Append("eventProgress=").Append(state.EventProgress).Append('\n');
            for (int i = 0; i < EventCalendar.TierCount; i++)
                sb.Append("eventClaimed").Append(i).Append('=').Append(state.EventTierClaimed[i] ? 1 : 0).Append('\n');
            sb.Append("eventRaceClaimed=").Append(state.EventRaceClaimed ? 1 : 0).Append('\n');
            for (int i = 0; i < EventCalendar.RaceTarget; i++)
                sb.Append("eventRaceLevel").Append(i).Append('=').Append(state.EventRaceLevels[i]).Append('\n');
            sb.Append("trophyGold=").Append(state.TrophyGold).Append('\n');
            sb.Append("trophySilver=").Append(state.TrophySilver).Append('\n');
            sb.Append("trophyBronze=").Append(state.TrophyBronze).Append('\n');
            sb.Append("eventToastHammers=").Append(state.EventToastHammers).Append('\n');
            sb.Append("eventToastFreeSwaps=").Append(state.EventToastFreeSwaps).Append('\n');
            sb.Append("eventToastShuffles=").Append(state.EventToastShuffles).Append('\n');
            sb.Append("eventToastShields=").Append(state.EventToastShields).Append('\n');
            sb.Append("eventToastTrophy=").Append(state.EventToastTrophy).Append('\n');
            sb.Append("rescues=").Append(state.Rescues).Append('\n');
            sb.Append("eventToastRescues=").Append(state.EventToastRescues).Append('\n');
            sb.Append("albumSalt=").Append(state.AlbumSalt).Append('\n');
            sb.Append("albumPacks=").Append(state.AlbumPacks).Append('\n');
            sb.Append("albumPacksOpened=").Append(state.AlbumPacksOpened).Append('\n');
            sb.Append("albumStarsCounted=").Append(state.AlbumStarsCounted).Append('\n');
            sb.Append("albumPity=").Append(state.AlbumPity).Append('\n');
            for (int page = 0; page < AlbumCatalog.PageCount; page++)
                sb.Append("albumPage").Append(page).Append('=').Append(state.AlbumPageOwned[page]).Append('\n');
            sb.Append("albumPagesRewarded=").Append(state.AlbumPagesRewarded).Append('\n');
            sb.Append("eventToastPacks=").Append(state.EventToastPacks).Append('\n');
            return sb.ToString();
        }

        public static MetaState Deserialize(string text)
        {
            var state = new MetaState();
            if (string.IsNullOrEmpty(text))
                return state;

            foreach (string rawLine in text.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0)
                    continue;

                int eq = line.IndexOf('=');
                if (eq <= 0 || eq == line.Length - 1)
                    continue;

                string key = line.Substring(0, eq);
                if (!int.TryParse(line.Substring(eq + 1), out int value))
                    return new MetaState(); // corrupt → fresh

                switch (key)
                {
                    case "lastClaimDay": state.LastClaimDay = Math.Max(0, value); break;
                    case "streak": state.Streak = Math.Max(0, value); break;
                    case "pendingKind": state.PendingKind = (StreakRewardKind)value; break;
                    case "pendingAmount": state.PendingAmount = Math.Max(0, value); break;
                    case "hammers": state.Hammers = Math.Max(0, value); break;
                    case "freeSwaps": state.FreeSwaps = Math.Max(0, value); break;
                    case "shuffles": state.Shuffles = Math.Max(0, value); break;
                    case "winStreak": state.WinStreak = Math.Max(0, value); break;
                    case "levelInProgress": state.LevelInProgress = value != 0; break;
                    case "streakShields": state.StreakShields = Math.Max(0, value); break;
                    case "lastChestStars": state.LastChestStars = Math.Max(0, value); break;
                    case "lastSeenTownStage": state.LastSeenTownStage = Math.Max(0, value); break;
                    case "missionDay": state.MissionDay = Math.Max(0, value); break;
                    case "missionProgress0": state.MissionProgress[0] = Math.Max(0, value); break;
                    case "missionProgress1": state.MissionProgress[1] = Math.Max(0, value); break;
                    case "missionProgress2": state.MissionProgress[2] = Math.Max(0, value); break;
                    case "missionClaimed0": state.MissionClaimed[0] = value != 0; break;
                    case "missionClaimed1": state.MissionClaimed[1] = value != 0; break;
                    case "missionClaimed2": state.MissionClaimed[2] = value != 0; break;
                    case "rerolledSlot": state.RerolledSlot = Math.Max(-1, value); break;
                    case "missionWeek": state.MissionWeek = Math.Max(0, value); break;
                    case "weeklyProgress": state.WeeklyProgress = Math.Max(0, value); break;
                    case "weeklyClaimed": state.WeeklyClaimed = value != 0; break;
                    case "eventWindowId": state.EventWindowId = Math.Max(0, value); break;
                    case "eventKindId": state.EventKindId = Math.Max(0, value); break;
                    case "eventParam": state.EventParam = Math.Max(0, value); break;
                    case "eventProgress": state.EventProgress = Math.Max(0, value); break;
                    case "eventClaimed0": state.EventTierClaimed[0] = value != 0; break;
                    case "eventClaimed1": state.EventTierClaimed[1] = value != 0; break;
                    case "eventClaimed2": state.EventTierClaimed[2] = value != 0; break;
                    case "eventRaceClaimed": state.EventRaceClaimed = value != 0; break;
                    case "eventRaceLevel0": state.EventRaceLevels[0] = Math.Max(0, value); break;
                    case "eventRaceLevel1": state.EventRaceLevels[1] = Math.Max(0, value); break;
                    case "eventRaceLevel2": state.EventRaceLevels[2] = Math.Max(0, value); break;
                    case "eventRaceLevel3": state.EventRaceLevels[3] = Math.Max(0, value); break;
                    case "eventRaceLevel4": state.EventRaceLevels[4] = Math.Max(0, value); break;
                    case "eventRaceLevel5": state.EventRaceLevels[5] = Math.Max(0, value); break;
                    case "eventRaceLevel6": state.EventRaceLevels[6] = Math.Max(0, value); break;
                    case "eventRaceLevel7": state.EventRaceLevels[7] = Math.Max(0, value); break;
                    case "eventRaceLevel8": state.EventRaceLevels[8] = Math.Max(0, value); break;
                    case "eventRaceLevel9": state.EventRaceLevels[9] = Math.Max(0, value); break;
                    case "trophyGold": state.TrophyGold = Math.Max(0, value); break;
                    case "trophySilver": state.TrophySilver = Math.Max(0, value); break;
                    case "trophyBronze": state.TrophyBronze = Math.Max(0, value); break;
                    case "eventToastHammers": state.EventToastHammers = Math.Max(0, value); break;
                    case "eventToastFreeSwaps": state.EventToastFreeSwaps = Math.Max(0, value); break;
                    case "eventToastShuffles": state.EventToastShuffles = Math.Max(0, value); break;
                    case "eventToastShields": state.EventToastShields = Math.Max(0, value); break;
                    case "eventToastTrophy": state.EventToastTrophy = Math.Max(0, value); break;
                    case "rescues": state.Rescues = Math.Max(0, value); break;
                    case "eventToastRescues": state.EventToastRescues = Math.Max(0, value); break;
                    case "albumSalt": state.AlbumSalt = Math.Max(0, value); break;
                    case "albumPacks": state.AlbumPacks = Math.Max(0, value); break;
                    case "albumPacksOpened": state.AlbumPacksOpened = Math.Max(0, value); break;
                    case "albumStarsCounted": state.AlbumStarsCounted = Math.Max(0, value); break;
                    case "albumPity": state.AlbumPity = Math.Max(0, value); break;
                    // Masks clamp Max FIRST then &63 — the other order would turn a
                    // negative into fabricated ownership ((-5) & 63 == 59).
                    case "albumPage0": state.AlbumPageOwned[0] = Math.Max(0, value) & 63; break;
                    case "albumPage1": state.AlbumPageOwned[1] = Math.Max(0, value) & 63; break;
                    case "albumPage2": state.AlbumPageOwned[2] = Math.Max(0, value) & 63; break;
                    case "albumPage3": state.AlbumPageOwned[3] = Math.Max(0, value) & 63; break;
                    case "albumPage4": state.AlbumPageOwned[4] = Math.Max(0, value) & 63; break;
                    case "albumPage5": state.AlbumPageOwned[5] = Math.Max(0, value) & 63; break;
                    case "albumPagesRewarded": state.AlbumPagesRewarded = Math.Max(0, value) & 63; break;
                    case "eventToastPacks": state.EventToastPacks = Math.Max(0, value); break;
                    // unknown keys: ignored (forward compatibility);
                    // files from before the booster patch simply keep the
                    // starter-pack defaults — everyone gets the gift once
                }
            }
            return state;
        }
    }
}
