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

        /// <summary>Bottom rows covered in jelly (0 = no jelly on this level).</summary>
        public int JellyRows { get; }

        /// <summary>Layers per jelly cell (1 or 2).</summary>
        public int JellyLayers { get; }

        public LevelParameters(int width, int height, int colorCount, int movesLimit, int movesBonusPoints,
                               IReadOnlyList<Objective> objectives, IReadOnlyList<int> starScores,
                               int jellyRows = 0, int jellyLayers = 1)
        {
            Width = width;
            Height = height;
            ColorCount = colorCount;
            MovesLimit = movesLimit;
            MovesBonusPoints = movesBonusPoints;
            Objectives = objectives ?? throw new ArgumentNullException(nameof(objectives));
            StarScores = starScores ?? throw new ArgumentNullException(nameof(starScores));
            JellyRows = jellyRows;
            JellyLayers = jellyLayers;
        }
    }

    /// <summary>
    /// The campaign's difficulty curve: a pure function level-number -> parameters,
    /// shared by the editor generator and CLI tooling so the shipped level assets
    /// always come from one source of truth.
    ///
    /// The campaign is organised in 20-level CHAPTERS (matching <see cref="ThemeCurve"/>'s
    /// ambience drift): every chapter repeats the same teaching rhythm — score goals,
    /// then colour collections, doubled collections at 6, jelly from 8 — while each
    /// chapter starts slightly harder than the last (one move fewer, higher score
    /// bars, bigger collections). Chapter 0 (levels 1-20) is EXACTLY the original
    /// curve, so existing saves, assets and tests keep their meaning.
    /// </summary>
    public static class LevelCurve
    {
        public const int LevelCount = 60;
        public const int ChapterLength = 20;

        public static LevelParameters For(int level)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level), "Levels are 1-based.");

            int chapter = (level - 1) / ChapterLength;          // 0-based chapter index
            int cl = (level - 1) % ChapterLength + 1;           // 1..20 within the chapter

            // Only the very first levels of the campaign teach with 4 colours.
            int colorCount = chapter == 0 && cl <= 3 ? 4 : 5;
            int movesLimit = Math.Max(16, 24 - cl / 4) - Math.Min(chapter, 2);
            int scoreTarget = 500 + cl * 130 + chapter * 900;

            var objectives = new List<Objective>();
            if (chapter == 0 && cl <= 2)
            {
                objectives.Add(new Objective(ObjectiveType.Score, 0, scoreTarget));
            }
            else
            {
                int primaryColor = (cl - 3 + chapter + colorCount) % colorCount;
                objectives.Add(new Objective(ObjectiveType.CollectColor, primaryColor, Math.Min(14 + cl, 34) + chapter * 2));

                if (cl >= 6)
                {
                    int secondaryColor = (primaryColor + 2) % colorCount;
                    objectives.Add(new Objective(ObjectiveType.CollectColor, secondaryColor, Math.Min(10 + cl, 30) + chapter * 2));
                }
                if (cl % 3 == 0)
                    objectives.Add(new Objective(ObjectiveType.Score, 0, scoreTarget));
            }

            // Jelly arrives at chapter-level 8 (two floor rows), widens to three rows
            // at 13, and doubles its layers from 16 — the late-chapter spike.
            int jellyRows = cl < 8 ? 0 : cl < 13 ? 2 : 3;
            int jellyLayers = cl < 16 ? 1 : 2;
            if (jellyRows > 0)
                objectives.Add(new Objective(ObjectiveType.ClearJelly, 0, jellyRows * 8 * jellyLayers));

            int oneStar = 400 + cl * 100 + chapter * 1100;
            var starScores = new[] { oneStar, oneStar * 3 / 2, oneStar * 2 };

            return new LevelParameters(8, 8, colorCount, movesLimit, 30, objectives, starScores, jellyRows, jellyLayers);
        }
    }
}
