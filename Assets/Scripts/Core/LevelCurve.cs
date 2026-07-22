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

        /// <summary>Cells starting under a licorice lock (empty on lock-free levels).</summary>
        public IReadOnlyList<GridPosition> LockCells { get; }

        /// <summary>Cells starting as chocolate blocks.</summary>
        public IReadOnlyList<GridPosition> ChocolateCells { get; }

        /// <summary>How many ingredients the level dispenses (0 = none).</summary>
        public int IngredientCount { get; }

        public LevelParameters(int width, int height, int colorCount, int movesLimit, int movesBonusPoints,
                               IReadOnlyList<Objective> objectives, IReadOnlyList<int> starScores,
                               int jellyRows = 0, int jellyLayers = 1,
                               IReadOnlyList<GridPosition> lockCells = null,
                               IReadOnlyList<GridPosition> chocolateCells = null,
                               int ingredientCount = 0)
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
            LockCells = lockCells ?? Array.Empty<GridPosition>();
            ChocolateCells = chocolateCells ?? Array.Empty<GridPosition>();
            IngredientCount = ingredientCount;
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
        public const int LevelCount = 80;
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

                // Chapter 3 drops the secondary collection: the blocker goals below
                // take its slot (and the objective bar stays at four chips max).
                if (cl >= 6 && chapter < 3)
                {
                    int secondaryColor = (primaryColor + 2) % colorCount;
                    objectives.Add(new Objective(ObjectiveType.CollectColor, secondaryColor, Math.Min(10 + cl, 30) + chapter * 2));
                }
                if (cl % 3 == 0)
                    objectives.Add(new Objective(ObjectiveType.Score, 0, scoreTarget));
            }

            // Chapter 3 (levels 61-80) teaches the blocker mechanics in five-level
            // acts: locks → chocolate → ingredients → the mixed finale.
            var lockCells = new List<GridPosition>();
            var chocolateCells = new List<GridPosition>();
            int ingredientCount = 0;
            if (chapter >= 3)
                BuildBlockerAct(cl, objectives, lockCells, chocolateCells, ref ingredientCount);

            // Jelly arrives at chapter-level 8 (two floor rows), widens to three rows
            // at 13, and doubles its layers from 16 — the late-chapter spike.
            // Chapter 3 replaces jelly entirely with the blocker mechanics.
            int jellyRows = chapter >= 3 ? 0 : cl < 8 ? 0 : cl < 13 ? 2 : 3;
            int jellyLayers = cl < 16 ? 1 : 2;
            if (jellyRows > 0)
                objectives.Add(new Objective(ObjectiveType.ClearJelly, 0, jellyRows * 8 * jellyLayers));

            int oneStar = 400 + cl * 100 + chapter * 1100;
            var starScores = new[] { oneStar, oneStar * 3 / 2, oneStar * 2 };

            return new LevelParameters(8, 8, colorCount, movesLimit, 30, objectives, starScores,
                                       jellyRows, jellyLayers, lockCells, chocolateCells, ingredientCount);
        }

        /// <summary>Chapter 3's blocker layouts on the 8x8 board (y0 = gravity floor).</summary>
        private static void BuildBlockerAct(int cl, List<Objective> objectives,
                                            List<GridPosition> lockCells, List<GridPosition> chocolateCells,
                                            ref int ingredientCount)
        {
            if (cl <= 5)
            {
                // Act 1 — locks: a widening bar across row 5, plus a low pair at the finale.
                int half = Math.Min(cl, 4); // widths 2, 4, 6, 8, 8
                for (int x = 4 - half; x <= 3 + half; x++)
                    lockCells.Add(new GridPosition(x, 5));
                if (cl == 5)
                {
                    lockCells.Add(new GridPosition(3, 2));
                    lockCells.Add(new GridPosition(4, 2));
                }
            }
            else if (cl <= 10)
            {
                // Act 2 — chocolate creeping in from the top corners.
                chocolateCells.Add(new GridPosition(0, 7));
                chocolateCells.Add(new GridPosition(7, 7));
                if (cl >= 7) { chocolateCells.Add(new GridPosition(3, 7)); chocolateCells.Add(new GridPosition(4, 7)); }
                if (cl >= 8) { chocolateCells.Add(new GridPosition(0, 6)); chocolateCells.Add(new GridPosition(7, 6)); }
                if (cl >= 9) { chocolateCells.Add(new GridPosition(3, 6)); chocolateCells.Add(new GridPosition(4, 6)); }
                if (cl >= 10) { chocolateCells.Add(new GridPosition(1, 7)); chocolateCells.Add(new GridPosition(6, 7)); }
                objectives.Add(new Objective(ObjectiveType.ClearChocolate, 0, chocolateCells.Count));
            }
            else if (cl <= 15)
            {
                // Act 3 — ingredients: 2, 2, 3, 3, 4 cherries to bring home.
                ingredientCount = 2 + (cl - 11) / 2;
                objectives.Add(new Objective(ObjectiveType.CollectIngredients, 0, ingredientCount));
            }
            else
            {
                // Act 4 — the mixed finale.
                for (int x = 2; x <= 5; x++)
                    lockCells.Add(new GridPosition(x, 5));
                chocolateCells.Add(new GridPosition(0, 7));
                chocolateCells.Add(new GridPosition(7, 7));
                if (cl >= 18) { chocolateCells.Add(new GridPosition(3, 7)); chocolateCells.Add(new GridPosition(4, 7)); }
                ingredientCount = cl >= 20 ? 4 : cl >= 18 ? 3 : 2;
                objectives.Add(new Objective(ObjectiveType.ClearChocolate, 0, chocolateCells.Count));
                objectives.Add(new Objective(ObjectiveType.CollectIngredients, 0, ingredientCount));
            }
        }
    }
}
