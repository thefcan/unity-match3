using System;
using Match3.Core;
using NUnit.Framework;
using static Match3.Tests.TestColors;

namespace Match3.Tests
{
    public sealed class BoardTests
    {
        [Test]
        public void InitialFill_FillsEveryCell()
        {
            var board = new Board(8, 8, TestFactories.Seeded());

            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                    Assert.That(board[new GridPosition(x, y)].HasValue, $"Cell ({x},{y}) should be filled.");
        }

        [Test]
        public void InitialFill_NeverStartsWithMatches([Range(0, 29)] int seed)
        {
            // Parameterised over 30 seeds: the no-starting-match guarantee must hold
            // for ANY random sequence, not just a lucky one.
            var board = new Board(8, 8, TestFactories.Seeded(colorCount: 5, seed: seed));

            Assert.That(board.FindMatches(), Is.Empty);
        }

        [Test]
        public void Swap_ExchangesTheTwoTiles()
        {
            var board = Board.FromLayout(new[,]
            {
                { C, D, C },
                { A, C, D },
                { B, A, C },
            }, TestFactories.Seeded());

            var left = new GridPosition(0, 0);
            var right = new GridPosition(1, 0);
            int leftId = board[left].Value.Id;
            int rightId = board[right].Value.Id;

            board.Swap(left, right);

            Assert.That(board[left].Value.Id, Is.EqualTo(rightId));
            Assert.That(board[right].Value.Id, Is.EqualTo(leftId));
        }

        [Test]
        public void Swap_OutsideBoard_Throws()
        {
            var board = new Board(8, 8, TestFactories.Seeded());

            Assert.Throws<ArgumentOutOfRangeException>(
                () => board.Swap(new GridPosition(0, 0), new GridPosition(-1, 0)));
        }

        [Test]
        public void WouldSwapMatch_DetectsAMatchCreatingSwap()
        {
            // Swapping the B at (0,0) with the A above it completes "A A A" on the bottom row.
            var board = Board.FromLayout(new[,]
            {
                { C, D, C },
                { A, C, D },
                { B, A, A },
            }, TestFactories.Seeded());

            Assert.That(board.WouldSwapMatch(new GridPosition(0, 0), new GridPosition(0, 1)), Is.True);
        }

        [Test]
        public void WouldSwapMatch_ReturnsFalseForUselessSwap_AndRestoresBoard()
        {
            var board = Board.FromLayout(new[,]
            {
                { C, D, C },
                { A, C, D },
                { B, A, A },
            }, TestFactories.Seeded());

            var a = new GridPosition(1, 0);
            var b = new GridPosition(2, 0);
            int idA = board[a].Value.Id;
            int idB = board[b].Value.Id;

            bool result = board.WouldSwapMatch(a, b);

            Assert.That(result, Is.False);
            // The probe must leave no trace: same tiles back in the same cells.
            Assert.That(board[a].Value.Id, Is.EqualTo(idA));
            Assert.That(board[b].Value.Id, Is.EqualTo(idB));
        }

        [Test]
        public void AdjacencyIsOrthogonalOnly()
        {
            var origin = new GridPosition(3, 3);

            Assert.That(origin.IsAdjacentTo(new GridPosition(4, 3)), Is.True);
            Assert.That(origin.IsAdjacentTo(new GridPosition(3, 2)), Is.True);
            Assert.That(origin.IsAdjacentTo(new GridPosition(4, 4)), Is.False, "Diagonals are not adjacent.");
            Assert.That(origin.IsAdjacentTo(new GridPosition(5, 3)), Is.False, "Two cells away is not adjacent.");
            Assert.That(origin.IsAdjacentTo(origin), Is.False, "A cell is not adjacent to itself.");
        }
    }

    public sealed class TileFactoryTests
    {
        [Test]
        public void EveryTileGetsAUniqueId()
        {
            var factory = TestFactories.Seeded();

            var first = factory.Create();
            var second = factory.Create();
            var third = factory.Create(colorIndex: 2);

            Assert.That(second.Id, Is.Not.EqualTo(first.Id));
            Assert.That(third.Id, Is.Not.EqualTo(second.Id));
        }

        [Test]
        public void CreateExcluding_NeverReturnsAnExcludedColor()
        {
            var factory = TestFactories.Seeded(colorCount: 5, seed: 42);
            var excluded = new[] { 0, 3 };

            for (int i = 0; i < 100; i++)
            {
                var tile = factory.CreateExcluding(excluded);
                // Has.No.Member instead of Does.Not.Contain: Unity ships NUnit 3.5,
                // where the Contain overload on a negated expression is string-only.
                Assert.That(excluded, Has.No.Member(tile.ColorIndex));
            }
        }

        [Test]
        public void CreateExcluding_AllColorsExcluded_Throws()
        {
            var factory = TestFactories.Seeded(colorCount: 3);

            Assert.Throws<InvalidOperationException>(
                () => factory.CreateExcluding(new[] { 0, 1, 2 }));
        }

        [Test]
        public void FillWithoutMatches_SquareExclusionBranch_IsFullyScripted()
        {
            // Hand-traced fill on 3x3/3 colours: draws land A A | 1 | A | 1 | B | 0 | C | A,
            // where the bracketed values are CreateExcluding picks. Cell (1,1)'s left,
            // below-left and below neighbours all agree on A, so ONLY the square branch
            // fires there: allowed = [B, C], scripted pick 1 -> C. Deterministic proof
            // of the exclusion the sampling tests only make probable.
            var factory = TestFactories.Scripted(colorCount: 3, draws: new[] { A, A, 1, A, 1, B, 0, C, A });
            var board = new Board(3, 3, factory);

            Assert.That(board[new GridPosition(1, 1)].Value.ColorIndex, Is.EqualTo(C),
                "the square exclusion must veto A at (1,1)");
            Assert.That(board.FindMatches(), Is.Empty);
            Assert.That(board.FindSquares(), Is.Empty);
        }

        [Test]
        public void TwoColourFactory_IsRejectedAtBoardConstruction()
        {
            // With two colours a cell whose left-pair and below-pair disagree would
            // exclude BOTH — the ctor guards the documented 3-colour minimum up front
            // instead of letting the fill dead-end mid-board.
            var factory = TestFactories.Scripted(colorCount: 2, draws: new[] { A, B, A, B });

            Assert.Throws<ArgumentException>(() => new Board(3, 3, factory));
        }
    }
}
