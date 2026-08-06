using System;
using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>One frosting slab in a level layout: where it sits and how thick it starts.</summary>
    public readonly struct FrostingCell
    {
        public GridPosition Position { get; }
        public int Layers { get; }

        public FrostingCell(GridPosition position, int layers)
        {
            Position = position;
            Layers = layers;
        }
    }

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

        /// <summary>Frosting slabs (position + starting layers). Chapter 5 onwards.</summary>
        public IReadOnlyList<FrostingCell> FrostingCells { get; }

        /// <summary>Cells starting as licorice swirls.</summary>
        public IReadOnlyList<GridPosition> SwirlCells { get; }

        /// <summary>Cells holding an indestructible chocolate fountain.</summary>
        public IReadOnlyList<GridPosition> FountainCells { get; }

        /// <summary>How many bomb candies the level dispenses (0 = none).</summary>
        public int BombCount { get; }

        /// <summary>Moves on a freshly dispensed bomb's countdown.</summary>
        public int BombTimerMoves { get; }

        /// <summary>One short tutorial line for the level's first look ("" = none).</summary>
        public string TutorialText { get; }

        /// <summary>Cells highlighted through the tutorial dim.</summary>
        public IReadOnlyList<GridPosition> TutorialCells { get; }

        /// <summary>Cells starting as mystery eggs. Chapter 6 onwards.</summary>
        public IReadOnlyList<GridPosition> EggCells { get; }

        public LevelParameters(int width, int height, int colorCount, int movesLimit, int movesBonusPoints,
                               IReadOnlyList<Objective> objectives, IReadOnlyList<int> starScores,
                               int jellyRows = 0, int jellyLayers = 1,
                               IReadOnlyList<GridPosition> lockCells = null,
                               IReadOnlyList<GridPosition> chocolateCells = null,
                               int ingredientCount = 0,
                               IReadOnlyList<FrostingCell> frostingCells = null,
                               IReadOnlyList<GridPosition> swirlCells = null,
                               IReadOnlyList<GridPosition> fountainCells = null,
                               int bombCount = 0, int bombTimerMoves = 9,
                               string tutorialText = null,
                               IReadOnlyList<GridPosition> tutorialCells = null,
                               IReadOnlyList<GridPosition> eggCells = null)
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
            FrostingCells = frostingCells ?? Array.Empty<FrostingCell>();
            SwirlCells = swirlCells ?? Array.Empty<GridPosition>();
            FountainCells = fountainCells ?? Array.Empty<GridPosition>();
            BombCount = bombCount;
            BombTimerMoves = bombTimerMoves;
            TutorialText = tutorialText ?? string.Empty;
            TutorialCells = tutorialCells ?? Array.Empty<GridPosition>();
            EggCells = eggCells ?? Array.Empty<GridPosition>();
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
        public const int LevelCount = 120;
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

                // Chapters 3+ drop the secondary collection: the blocker goals below
                // take its slot (and the objective bar stays at four chips max).
                if (cl >= 6 && chapter < 3)
                {
                    int secondaryColor = (primaryColor + 2) % colorCount;
                    objectives.Add(new Objective(ObjectiveType.CollectColor, secondaryColor, Math.Min(10 + cl, 30) + chapter * 2));
                }
                if (cl % 3 == 0)
                    objectives.Add(new Objective(ObjectiveType.Score, 0, scoreTarget));
            }

            // Chapter 3 (levels 61-80) teaches the first blocker wave in five-level
            // acts: locks → chocolate → ingredients → the mixed finale. Its layouts
            // are FROZEN — chapter 4 gets its own act builder below.
            var lockCells = new List<GridPosition>();
            var chocolateCells = new List<GridPosition>();
            int ingredientCount = 0;
            if (chapter == 3)
                BuildBlockerAct(cl, objectives, lockCells, chocolateCells, ref ingredientCount);

            // Chapter 4 (levels 81-100): frosting → bombs+swirls → the fish showcase
            // fed by fountains → the everything-finale ("almost win" pressure).
            // Its layouts are FROZEN — chapter 6 gets its own act builder below.
            var frostingCells = new List<FrostingCell>();
            var swirlCells = new List<GridPosition>();
            var fountainCells = new List<GridPosition>();
            var eggCells = new List<GridPosition>();
            int bombCount = 0;
            int bombTimer = 9;
            if (chapter == 4)
            {
                BuildChapter5Act(cl, objectives, frostingCells, swirlCells, fountainCells,
                                 ref bombCount, ref bombTimer);
                // The finale act runs two moves tighter — near-miss pressure, only here.
                if (cl >= 16)
                    movesLimit = Math.Max(15, movesLimit - 2);
            }

            // Chapter 5 (levels 101-120): the mystery-egg chapter — meet the egg,
            // eggs over a jelly encore, eggs behind frosting, the scramble finale.
            if (chapter == 5)
            {
                BuildChapter6Act(cl, objectives, eggCells, frostingCells, lockCells,
                                 ref bombCount, ref bombTimer);
                if (cl >= 16)
                    movesLimit = Math.Max(15, movesLimit - 2);
            }

            // Jelly arrives at chapter-level 8 (two floor rows), widens to three rows
            // at 13, and doubles its layers from 16 — the late-chapter spike.
            // Chapters 3+ replace jelly entirely with the blocker mechanics.
            int jellyRows = chapter >= 3 ? 0 : cl < 8 ? 0 : cl < 13 ? 2 : 3;
            int jellyLayers = cl < 16 ? 1 : 2;
            // Chapter 6's act 2 brings jelly back UNDER the eggs — the campaign's one
            // late jelly encore; kept gentle (two rows, single layers).
            if (chapter == 5 && cl >= 6 && cl <= 10)
            {
                jellyRows = 2;
                jellyLayers = 1;
            }
            if (jellyRows > 0)
                objectives.Add(new Objective(ObjectiveType.ClearJelly, 0, jellyRows * 8 * jellyLayers));

            int oneStar = 400 + cl * 100 + chapter * 1100;
            var starScores = new[] { oneStar, oneStar * 3 / 2, oneStar * 2 };

            (string tutorialText, IReadOnlyList<GridPosition> tutorialCells) =
                TutorialFor(level, lockCells, chocolateCells, frostingCells, eggCells);

            return new LevelParameters(8, 8, colorCount, movesLimit, 30, objectives, starScores,
                                       jellyRows, jellyLayers, lockCells, chocolateCells, ingredientCount,
                                       frostingCells, swirlCells, fountainCells, bombCount, bombTimer,
                                       tutorialText, tutorialCells, eggCells);
        }

        /// <summary>
        /// Chapter 4's blocker acts on the 8x8 board (y0 = gravity floor):
        /// frosting shelf → bomb dispenser (+ beam-bending swirls) → fountains feeding
        /// the fish showcase → the mixed finale.
        /// </summary>
        private static void BuildChapter5Act(int cl, List<Objective> objectives,
                                             List<FrostingCell> frostingCells, List<GridPosition> swirlCells,
                                             List<GridPosition> fountainCells,
                                             ref int bombCount, ref int bombTimer)
        {
            if (cl <= 5)
            {
                // Act 1 — frosting: a widening, thickening shelf across row 5, then a
                // second low slab. Objective counts LAYERS.
                int half = Math.Min(cl + 1, 4); // widths 4, 6, 8, 8, 8
                for (int x = 4 - half; x <= 3 + half; x++)
                {
                    int layers = cl >= 3 && x >= 2 && x <= 5 ? 2 : 1;
                    frostingCells.Add(new FrostingCell(new GridPosition(x, 5), layers));
                }
                if (cl >= 4)
                    for (int x = 2; x <= 5; x++)
                        frostingCells.Add(new FrostingCell(new GridPosition(x, 2), cl == 5 ? 2 : 1));

                int totalLayers = 0;
                foreach (FrostingCell cell in frostingCells)
                    totalLayers += cell.Layers;
                objectives.Add(new Objective(ObjectiveType.ClearFrosting, 0, totalLayers));
            }
            else if (cl <= 10)
            {
                // Act 2 — countdown bombs, with swirls arriving to bend the beams.
                bombCount = cl - 3;               // 3..7 bombs across the level
                bombTimer = cl <= 7 ? 10 : 9;
                if (cl >= 7)
                {
                    swirlCells.Add(new GridPosition(1, 6));
                    swirlCells.Add(new GridPosition(6, 6));
                }
                if (cl >= 9)
                {
                    swirlCells.Add(new GridPosition(3, 3));
                    swirlCells.Add(new GridPosition(4, 3));
                }
            }
            else if (cl <= 15)
            {
                // Act 3 — the fish showcase: fountains keep oozing chocolate, and the
                // 2x2 fish is the cleanest counter. The objective feeds on the ooze.
                fountainCells.Add(new GridPosition(0, 7));
                if (cl >= 14)
                    fountainCells.Add(new GridPosition(7, 7));
                objectives.Add(new Objective(ObjectiveType.ClearChocolate, 0, 3 + (cl - 11)));
            }
            else
            {
                // Act 4 — the mixed finale: frosting core, swirl guards, late bombs,
                // and one fountain on the very last level.
                for (int x = 2; x <= 5; x++)
                    frostingCells.Add(new FrostingCell(new GridPosition(x, 5), 2));
                swirlCells.Add(new GridPosition(1, 6));
                swirlCells.Add(new GridPosition(6, 6));
                if (cl >= 18)
                {
                    bombCount = 2;
                    bombTimer = 9;
                }
                if (cl == 20)
                    fountainCells.Add(new GridPosition(0, 7));
                objectives.Add(new Objective(ObjectiveType.ClearFrosting, 0, 8));
            }
        }

        /// <summary>
        /// Chapter 6's egg acts on the 8x8 board (y0 = gravity floor): meet the egg,
        /// eggs over the jelly encore, eggs behind a frosting shelf, the scramble
        /// finale. Every level carries a HatchEggs objective matching its clutch.
        /// </summary>
        private static void BuildChapter6Act(int cl, List<Objective> objectives,
                                             List<GridPosition> eggCells, List<FrostingCell> frostingCells,
                                             List<GridPosition> lockCells,
                                             ref int bombCount, ref int bombTimer)
        {
            if (cl <= 5)
            {
                // Act 1 — meet the egg: a widening clutch across row 4, then a low pair.
                int half = Math.Min(cl, 3); // widths 2, 4, 6, 6, 6
                for (int x = 4 - half; x <= 3 + half; x++)
                    eggCells.Add(new GridPosition(x, 4));
                if (cl >= 4)
                {
                    eggCells.Add(new GridPosition(3, 2));
                    eggCells.Add(new GridPosition(4, 2));
                }
            }
            else if (cl <= 10)
            {
                // Act 2 — eggs over the jelly encore (the caller re-enables the rows).
                int half = Math.Min(cl - 4, 3); // widths 4, 6, 6, 6, 6
                for (int x = 4 - half; x <= 3 + half; x++)
                    eggCells.Add(new GridPosition(x, 5));
                if (cl >= 9)
                {
                    eggCells.Add(new GridPosition(0, 7));
                    eggCells.Add(new GridPosition(7, 7));
                }
            }
            else if (cl <= 15)
            {
                // Act 3 — eggs waiting behind a frosting shelf: crack through to reach them.
                for (int x = 2; x <= 5; x++)
                    frostingCells.Add(new FrostingCell(new GridPosition(x, 5), cl >= 13 ? 2 : 1));
                for (int x = 2; x <= 5; x++)
                    eggCells.Add(new GridPosition(x, 3));
                if (cl >= 14)
                {
                    eggCells.Add(new GridPosition(1, 3));
                    eggCells.Add(new GridPosition(6, 3));
                }

                int totalLayers = 0;
                foreach (FrostingCell cell in frostingCells)
                    totalLayers += cell.Layers;
                objectives.Add(new Objective(ObjectiveType.ClearFrosting, 0, totalLayers));
            }
            else
            {
                // Act 4 — the scramble finale: a caged bar over the clutch, corner
                // eggs, and late bombs turning up the heat.
                for (int x = 2; x <= 5; x++)
                    lockCells.Add(new GridPosition(x, 5));
                for (int x = 2; x <= 5; x++)
                    eggCells.Add(new GridPosition(x, 4));
                eggCells.Add(new GridPosition(0, 7));
                eggCells.Add(new GridPosition(7, 7));
                if (cl >= 18)
                {
                    bombCount = 2;
                    bombTimer = 9;
                }
            }

            objectives.Add(new Objective(ObjectiveType.HatchEggs, 0, eggCells.Count));
        }

        /// <summary>
        /// The one-line tutorial for each act opener (chapter 3's retrofits included).
        /// Highlight cells come from the level's own layout so the overlay always
        /// points at something real.
        /// </summary>
        private static (string text, IReadOnlyList<GridPosition> cells) TutorialFor(
            int level, List<GridPosition> lockCells, List<GridPosition> chocolateCells,
            List<FrostingCell> frostingCells, List<GridPosition> eggCells)
        {
            switch (level)
            {
                case 61:
                    return ("MATCH BESIDE THE CAGES", lockCells.ToArray());
                case 66:
                    return ("STOP THE CHOCOLATE SPREAD", chocolateCells.ToArray());
                case 71:
                    return ("CARRY CHERRIES TO THE FLOOR",
                            new[] { new GridPosition(3, 0), new GridPosition(4, 0) });
                case 81:
                {
                    var cells = new List<GridPosition>();
                    for (int i = 0; i < frostingCells.Count && i < 4; i++)
                        cells.Add(frostingCells[i].Position);
                    return ("MATCH BESIDE THE FROSTING", cells);
                }
                case 86:
                    return ("DEFUSE BOMBS BEFORE ZERO", Array.Empty<GridPosition>());
                case 91:
                    return ("A 2X2 MAKES A FISH", Array.Empty<GridPosition>());
                case 96:
                    return ("EVERY BLOCKER AT ONCE", Array.Empty<GridPosition>());
                case 101:
                {
                    var cells = new List<GridPosition>();
                    for (int i = 0; i < eggCells.Count && i < 4; i++)
                        cells.Add(eggCells[i]);
                    return ("MATCH BESIDE THE MYSTERY EGG", cells);
                }
                case 106:
                {
                    var cells = new List<GridPosition>();
                    for (int i = 0; i < eggCells.Count && i < 4; i++)
                        cells.Add(eggCells[i]);
                    return ("CRACK EGGS, CLEAR THE JELLY", cells);
                }
                case 111:
                {
                    var cells = new List<GridPosition>();
                    for (int i = 0; i < frostingCells.Count && i < 4; i++)
                        cells.Add(frostingCells[i].Position);
                    return ("EGGS WAIT BEHIND THE FROSTING", cells);
                }
                case 116:
                    return ("THE FINAL SCRAMBLE", Array.Empty<GridPosition>());
                default:
                    return (string.Empty, Array.Empty<GridPosition>());
            }
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
