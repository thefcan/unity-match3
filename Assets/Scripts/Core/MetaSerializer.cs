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
                    // unknown keys: ignored (forward compatibility)
                }
            }
            return state;
        }
    }
}
