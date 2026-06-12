using Match3.Core;
using NUnit.Framework;
using static Match3.Tests.TestColors;

namespace Match3.Tests
{
    /// <summary>
    /// Match detection rules. Layout arrays are written [row, column] with row 0 at
    /// the TOP (Board.FromLayout flips them), so each literal looks like the screen.
    /// Remember assertions use bottom-based Y: the bottom row of a 3-row layout is y = 0.
    /// </summary>
    public sealed class MatchDetectionTests
    {
        [Test]
        public void HorizontalRunOfThree_IsDetected()
        {
            var board = Board.FromLayout(new[,]
            {
                { A, B, C },
                { B, C, A },
                { A, A, A }, // <- bottom row match
            }, TestFactories.Seeded());

            var matches = board.FindMatches();

            Assert.That(matches, Is.EquivalentTo(new[]
            {
                new GridPosition(0, 0), new GridPosition(1, 0), new GridPosition(2, 0),
            }));
        }

        [Test]
        public void VerticalRunOfFour_IsDetected()
        {
            var board = Board.FromLayout(new[,]
            {
                { A, B, C },
                { A, C, B },
                { A, B, C },
                { A, C, B }, // col 0 is A A A A
            }, TestFactories.Seeded());

            var matches = board.FindMatches();

            Assert.That(matches, Is.EquivalentTo(new[]
            {
                new GridPosition(0, 0), new GridPosition(0, 1),
                new GridPosition(0, 2), new GridPosition(0, 3),
            }));
        }

        [Test]
        public void LShapedMatch_CountsSharedCellOnce()
        {
            // Column 0 and the bottom row are both all-A, sharing the corner (0, 0):
            // 3 + 3 cells with one overlap = 5 unique positions.
            var board = Board.FromLayout(new[,]
            {
                { A, B, C },
                { A, C, B },
                { A, A, A },
            }, TestFactories.Seeded());

            var matches = board.FindMatches();

            Assert.That(matches.Count, Is.EqualTo(5));
            Assert.That(matches, Does.Contain(new GridPosition(0, 0))); // the shared corner
        }

        [Test]
        public void Checkerboard_HasNoMatches()
        {
            var board = Board.FromLayout(new[,]
            {
                { A, B, A },
                { B, A, B },
                { A, B, A },
            }, TestFactories.Seeded());

            Assert.That(board.FindMatches(), Is.Empty);
        }

        [Test]
        public void RunOfTwo_IsNotAMatch()
        {
            var board = Board.FromLayout(new[,]
            {
                { A, A, B },
                { B, B, A },
                { A, A, B },
            }, TestFactories.Seeded());

            Assert.That(board.FindMatches(), Is.Empty);
        }

        [Test]
        public void EmptyCells_NeverMatch()
        {
            var board = Board.FromLayout(new[,]
            {
                { _, _, _ },
                { _, _, _ },
                { A, B, A },
            }, TestFactories.Seeded());

            Assert.That(board.FindMatches(), Is.Empty);
        }
    }
}
