using System;
using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>Everything that defines one authored level, engine-free.</summary>
    public readonly struct LevelParameters
    {
        public int Width { get; }
        public int Height { get; }
        public int ColorCount { get; }
        public int MovesLimit { get; }
        public int MovesBonusPoints { get; }
        public IReadOnlyList<Objective> Objectives { get; }
        public IReadOnlyList<int> StarScores { get; }

        public LevelParameters(int width, int height, int colorCount, int movesLimit, int movesBonusPoints,
                               IReadOnlyList<Objective> objectives, IReadOnlyList<int> starScores)
        {
            Width = width;
            Height = height;
            ColorCount = colorCount;
            MovesLimit = movesLimit;
            MovesBonusPoints = movesBonusPoints;
            Objectives = objectives ?? throw new ArgumentNullException(nameof(objectives));
            StarScores = starScores ?? throw new ArgumentNullException(nameof(starScores));
        }
    }

    /// <summary>
    /// The campaign's difficulty curve: a pure function level-number -> parameters,
    /// shared by the editor generator and CLI tooling so the 20 shipped level assets
    /// always come from one source of truth. Early levels teach (4 colours, score
    /// goals); colour-collection goals arrive at 3, doubled collections at 6, and the
    /// move budget slowly tightens while score bars rise.
    /// </summary>
    public static class LevelCurve
    {
        public const int LevelCount = 20;

        public static LevelParameters For(int level)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level), "Levels are 1-based.");

            int colorCount = level <= 3 ? 4 : 5;
            int movesLimit = Math.Max(16, 24 - level / 4);
            int scoreTarget = 500 + level * 130;

            var objectives = new List<Objective>();
            if (level <= 2)
            {
                objectives.Add(new Objective(ObjectiveType.Score, 0, scoreTarget));
            }
            else
            {
                int primaryColor = (level - 3) % colorCount;
                objectives.Add(new Objective(ObjectiveType.CollectColor, primaryColor, 14 + level));

                if (level >= 6)
                {
                    int secondaryColor = (primaryColor + 2) % colorCount;
                    objectives.Add(new Objective(ObjectiveType.CollectColor, secondaryColor, 10 + level));
                }
                if (level % 3 == 0)
                    objectives.Add(new Objective(ObjectiveType.Score, 0, scoreTarget));
            }

            int oneStar = 400 + level * 100;
            var starScores = new[] { oneStar, oneStar * 3 / 2, oneStar * 2 };

            return new LevelParameters(8, 8, colorCount, movesLimit, 30, objectives, starScores);
        }
    }
}
