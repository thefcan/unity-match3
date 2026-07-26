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
                    // unknown keys: ignored (forward compatibility);
                    // files from before the booster patch simply keep the
                    // starter-pack defaults — everyone gets the gift once
                }
            }
            return state;
        }
    }
}
