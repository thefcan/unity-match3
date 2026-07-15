using System;
using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// Maps a final score to a 0-3 star rating against a level's ascending thresholds.
    /// Meeting a threshold exactly earns its star (>=, not >).
    /// </summary>
    public static class StarCalculator
    {
        public static int Calculate(int score, IReadOnlyList<int> thresholds)
        {
            if (thresholds == null) throw new ArgumentNullException(nameof(thresholds));

            int stars = 0;
            for (int i = 0; i < thresholds.Count && stars < 3; i++)
                if (score >= thresholds[i])
                    stars++;
            return stars;
        }
    }
}
