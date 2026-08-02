using System.Linq;
using Match3.Core;
using NUnit.Framework;
using static Match3.Tests.TestColors;

namespace Match3.Tests
{
    /// <summary>
    /// Covers the match-shape -> special-candy rules: 4 in a line makes a striped
    /// (perpendicular), an L / T makes a wrapped at the corner, 5+ makes a colour
    /// bomb, longest run wins, and each cell funds at most one special.
    /// </summary>
    public sealed class SpecialMatchAnalyzerTests
    {
        [Test]
        public void HorizontalFour_CreatesColumnClearingStriped_AtSwapCell()
        {
            var board = Board.FromLayout(new[,]
            {
                { B, C, D, B },
                { C, D, B, C },
                { D, B, C, D },
                { A, A, A, A },
            }, TestFactories.Seeded());

            var plans = SpecialMatchAnalyzer.Analyze(
                board, board.FindMatchRuns(),
                swapFrom: new GridPosition(1, 1), swapTo: new GridPosition(1, 0));

            Assert.That(plans, Has.Count.EqualTo(1));
            Assert.That(plans[0].Kind, Is.EqualTo(TileKind.StripedV), "horizontal match -> perpendicular stripes");
            Assert.That(plans[0].Position, Is.EqualTo(new GridPosition(1, 0)), "the special appears where the player swapped");
            Assert.That(plans[0].ColorIndex, Is.EqualTo(A));
            Assert.That(plans[0].SourcePositions, Has.Count.EqualTo(4));
        }

        [Test]
        public void HorizontalFour_WithoutSwap_CreatesAtRunMiddle()
        {
            var board = Board.FromLayout(new[,]
            {
                { B, C, D, B },
                { C, D, B, C },
                { D, B, C, D },
                { A, A, A, A },
            }, TestFactories.Seeded());

            var plans = SpecialMatchAnalyzer.Analyze(board, board.FindMatchRuns(), null, null);

            Assert.That(plans, Has.Count.EqualTo(1));
            Assert.That(plans[0].Position, Is.EqualTo(new GridPosition(2, 0)), "cascade-made specials appear mid-run");
        }

        [Test]
        public void SwapFrom_IsUsedWhenSwapToIsOutsideTheRun()
        {
            var board = Board.FromLayout(new[,]
            {
                { B, C, D, B },
                { C, D, B, C },
                { D, B, C, D },
                { A, A, A, A },
            }, TestFactories.Seeded());

            var plans = SpecialMatchAnalyzer.Analyze(
                board, board.FindMatchRuns(),
                swapFrom: new GridPosition(3, 0), swapTo: new GridPosition(3, 1));

            Assert.That(plans[0].Position, Is.EqualTo(new GridPosition(3, 0)));
        }

        [Test]
        public void VerticalFour_CreatesRowClearingStriped()
        {
            var board = Board.FromLayout(new[,]
            {
                { A, B, C, B },
                { A, C, B, C },
                { A, B, C, B },
                { A, C, B, C },
            }, TestFactories.Seeded());

            var plans = SpecialMatchAnalyzer.Analyze(board, board.FindMatchRuns(), null, null);

            Assert.That(plans, Has.Count.EqualTo(1));
            Assert.That(plans[0].Kind, Is.EqualTo(TileKind.StripedH), "vertical match -> row-clearing candy");
            Assert.That(plans[0].Position.X, Is.EqualTo(0));
        }

        [Test]
        public void FiveInLine_CreatesColorBomb()
        {
            var board = Board.FromLayout(new[,]
            {
                { B, C, D, B, C },
                { C, D, B, C, D },
                { D, B, C, D, B },
                { A, A, A, A, A },
            }, TestFactories.Seeded());

            var plans = SpecialMatchAnalyzer.Analyze(board, board.FindMatchRuns(), null, null);

            Assert.That(plans, Has.Count.EqualTo(1));
            Assert.That(plans[0].Kind, Is.EqualTo(TileKind.ColorBomb));
            Assert.That(plans[0].ColorIndex, Is.EqualTo(Tile.NoColor), "a colour bomb has no colour");
            Assert.That(plans[0].Position, Is.EqualTo(new GridPosition(2, 0)));
        }

        [Test]
        public void LShape_CreatesWrapped_AtTheCorner()
        {
            var board = Board.FromLayout(new[,]
            {
                { A, B, C },
                { A, C, B },
                { A, A, A },
            }, TestFactories.Seeded());

            var plans = SpecialMatchAnalyzer.Analyze(board, board.FindMatchRuns(), null, null);

            Assert.That(plans, Has.Count.EqualTo(1));
            Assert.That(plans[0].Kind, Is.EqualTo(TileKind.Wrapped));
            Assert.That(plans[0].Position, Is.EqualTo(new GridPosition(0, 0)), "the wrapped sits on the shared corner");
            Assert.That(plans[0].ColorIndex, Is.EqualTo(A));
            Assert.That(plans[0].SourcePositions, Has.Count.EqualTo(5), "both runs fund it, corner counted once");
        }

        [Test]
        public void TShape_CreatesWrapped_AtTheIntersection()
        {
            var board = Board.FromLayout(new[,]
            {
                { A, A, A },
                { B, A, C },
                { C, A, B },
            }, TestFactories.Seeded());

            var plans = SpecialMatchAnalyzer.Analyze(board, board.FindMatchRuns(), null, null);

            Assert.That(plans, Has.Count.EqualTo(1));
            Assert.That(plans[0].Kind, Is.EqualTo(TileKind.Wrapped));
            Assert.That(plans[0].Position, Is.EqualTo(new GridPosition(1, 2)));
        }

        [Test]
        public void FiveRun_TakesPriorityOverAnIntersectingRun()
        {
            // Column 0 is five As; the bottom row adds an intersecting three. The 5-run
            // wins (colour bomb) and spends the corner, so no wrapped is created.
            var board = Board.FromLayout(new[,]
            {
                { A, B, C, D, B },
                { A, C, D, B, C },
                { A, D, B, C, D },
                { A, B, C, D, B },
                { A, A, A, C, D },
            }, TestFactories.Seeded());

            var plans = SpecialMatchAnalyzer.Analyze(board, board.FindMatchRuns(), null, null);

            Assert.That(plans, Has.Count.EqualTo(1));
            Assert.That(plans[0].Kind, Is.EqualTo(TileKind.ColorBomb));
        }

        [Test]
        public void TwoSeparateFourRuns_CreateTwoStripeds()
        {
            var board = Board.FromLayout(new[,]
            {
                { A, A, A, A, C, B, C },
                { C, D, B, C, D, C, D },
                { D, C, D, B, C, D, B },
                { B, B, B, B, D, B, C },
            }, TestFactories.Seeded());

            var plans = SpecialMatchAnalyzer.Analyze(board, board.FindMatchRuns(), null, null);

            Assert.That(plans, Has.Count.EqualTo(2));
            Assert.That(plans.Select(p => p.Kind), Is.All.EqualTo(TileKind.StripedV));
            Assert.That(plans.Select(p => p.ColorIndex), Is.EquivalentTo(new[] { A, B }));
        }

        [Test]
        public void PlusShape_CreatesOneWrapped_AtTheCenter_SourcesDeduped()
        {
            // Two runs crossing at INTERIOR cells of both — the '+' shape most
            // match-3s special-case; here the generic intersecting-pair pass covers it.
            var board = Board.FromLayout(new[,]
            {
                { C, D, B, D, C },
                { D, C, A, C, D },
                { B, A, A, A, B },
                { D, C, A, C, D },
                { C, D, B, D, C },
            }, TestFactories.Seeded());

            var plans = SpecialMatchAnalyzer.Analyze(board, board.FindMatchRuns(), null, null);

            Assert.That(plans, Has.Count.EqualTo(1));
            Assert.That(plans[0].Kind, Is.EqualTo(TileKind.Wrapped));
            Assert.That(plans[0].Position, Is.EqualTo(new GridPosition(2, 2)), "the wrapped sits on the crossing");
            Assert.That(plans[0].SourcePositions, Has.Count.EqualTo(5), "3 + 3 with the shared center counted once");
        }

        [Test]
        public void SixInLine_IsOneMaximalRun_AndOneColorBomb()
        {
            var board = Board.FromLayout(new[,]
            {
                { B, C, D, B, C, D },
                { C, D, B, C, D, B },
                { A, A, A, A, A, A },
            }, TestFactories.Seeded());

            var runs = board.FindMatchRuns();
            Assert.That(runs, Has.Count.EqualTo(1), "a 6-run is one maximal run, never two overlapping shorter ones");
            Assert.That(runs[0].Length, Is.EqualTo(6));

            var plans = SpecialMatchAnalyzer.Analyze(board, runs, null, null);
            Assert.That(plans, Has.Count.EqualTo(1));
            Assert.That(plans[0].Kind, Is.EqualTo(TileKind.ColorBomb));
            Assert.That(plans[0].Position, Is.EqualTo(new GridPosition(3, 0)), "mid-run for a cascade-made shape");
        }

        [Test]
        public void BothSwappedCells_CompleteRuns_TwoSpecialsAtTheSwapCells()
        {
            // One swap finishes an A-four on the upper row AND a B-four on the lower:
            // each special must land on its own swapped cell.
            var board = Board.FromLayout(new[,]
            {
                { D, C, D, C, D },
                { A, A, B, A, C },
                { B, B, A, B, D },
                { C, D, C, D, C },
            }, TestFactories.Seeded());
            var from = new GridPosition(2, 1);
            var to = new GridPosition(2, 2);
            board.Swap(from, to);

            var plans = SpecialMatchAnalyzer.Analyze(board, board.FindMatchRuns(), swapFrom: from, swapTo: to);

            Assert.That(plans, Has.Count.EqualTo(2));
            Assert.That(plans.Select(p => p.Kind), Is.All.EqualTo(TileKind.StripedV));
            Assert.That(plans.Select(p => p.Position), Is.EquivalentTo(new[] { from, to }));
        }

        [Test]
        public void FiveRun_BombLandsAtTheSwapCell_NotTheMiddle()
        {
            var board = Board.FromLayout(new[,]
            {
                { B, C, D, B, C },
                { C, D, B, C, D },
                { D, B, C, D, B },
                { A, A, A, A, A },
            }, TestFactories.Seeded());

            var plans = SpecialMatchAnalyzer.Analyze(
                board, board.FindMatchRuns(),
                swapFrom: new GridPosition(1, 1), swapTo: new GridPosition(1, 0));

            Assert.That(plans[0].Kind, Is.EqualTo(TileKind.ColorBomb));
            Assert.That(plans[0].Position, Is.EqualTo(new GridPosition(1, 0)), "the bomb appears where the player swapped");
        }

        [Test]
        public void LShape_WrappedIgnoresTheSwapCell_AlwaysTheCorner()
        {
            var board = Board.FromLayout(new[,]
            {
                { A, B, C },
                { A, C, B },
                { A, A, A },
            }, TestFactories.Seeded());

            var plans = SpecialMatchAnalyzer.Analyze(
                board, board.FindMatchRuns(),
                swapFrom: new GridPosition(1, 2), swapTo: new GridPosition(0, 2));

            Assert.That(plans[0].Kind, Is.EqualTo(TileKind.Wrapped));
            Assert.That(plans[0].Position, Is.EqualTo(new GridPosition(0, 0)),
                "an L/T wrapped always sits on the corner — the swap cell preference is a straight-run rule");
        }

        [Test]
        public void ThreeByThreeBlock_WrapsOneCrossing_AndTheFarSquareStillFishes()
        {
            // A 3x3 monochrome blob is six 3-runs. The first crossing mints a
            // wrapped and its row+column spend every other RUN (any-cell rule) —
            // but the far-corner 2x2 touches neither, so it still mints a fish.
            // One blob, exactly two specials.
            var board = Board.FromLayout(new[,]
            {
                { C, D, C, D },
                { A, A, A, B },
                { A, A, A, C },
                { A, A, A, D },
            }, TestFactories.Seeded());

            var plans = SpecialMatchAnalyzer.Analyze(
                board, board.FindMatchRuns(), board.FindSquares(), null, null);

            Assert.That(plans, Has.Count.EqualTo(2));
            Assert.That(plans.Select(p => p.Kind),
                Is.EquivalentTo(new[] { TileKind.Wrapped, TileKind.Fish }));
        }

        [Test]
        public void CreationNeverReplacesAnExistingSpecial()
        {
            var factory = TestFactories.Seeded();
            var board = Board.FromLayout(new[,]
            {
                { B, C, D, B },
                { C, D, B, C },
                { D, B, C, D },
                { A, A, A, A },
            }, factory);
            // The swap cell already holds a special (same colour, so the run stands).
            board.SetTile(new GridPosition(1, 0), factory.CreateSpecial(A, TileKind.StripedH));

            var plans = SpecialMatchAnalyzer.Analyze(
                board, board.FindMatchRuns(),
                swapFrom: new GridPosition(1, 1), swapTo: new GridPosition(1, 0));

            Assert.That(plans, Has.Count.EqualTo(1));
            Assert.That(plans[0].Position, Is.EqualTo(new GridPosition(2, 0)),
                "the swap cell is skipped — a special must never overwrite another special");
        }
    }
}
