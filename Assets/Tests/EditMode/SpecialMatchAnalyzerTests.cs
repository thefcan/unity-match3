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
