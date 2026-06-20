using System;
using System.Collections.Generic;
using Match3.Core;
using NUnit.Framework;
using static Match3.Tests.TestColors;

namespace Match3.Tests
{
    /// <summary>
    /// Covers the "no moves left" recovery and the move-finding that also drives the
    /// idle hint: detecting a dead board, finding a real move, and shuffling into a
    /// layout that is both match-free and playable while preserving the colour mix.
    /// </summary>
    public sealed class BoardRecoveryTests
    {
        [Test]
        public void HasPossibleMove_TrueWhenASwapWouldMatch()
        {
            // Swapping the bottom-left B up with the A lines up "A A A" on the bottom row.
            var board = Board.FromLayout(new[,]
            {
                { C, D, C },
                { A, C, D },
                { B, A, A },
            }, TestFactories.Seeded());

            Assert.That(board.HasPossibleMove(), Is.True);
        }

        [Test]
        public void FindPossibleMove_ReturnsASwapThatActuallyMatches()
        {
            var board = Board.FromLayout(new[,]
            {
                { C, D, C },
                { A, C, D },
                { B, A, A },
            }, TestFactories.Seeded());

            var move = board.FindPossibleMove();

            Assert.That(move.HasValue, Is.True);
            Assert.That(board.WouldSwapMatch(move.Value.Item1, move.Value.Item2), Is.True);
        }

        [Test]
        public void CyclicLatinSquare_IsADeadBoard()
        {
            // Every row and column is a distinct A,B,C — no single swap can line up three.
            var board = Board.FromLayout(new[,]
            {
                { A, B, C },
                { B, C, A },
                { C, A, B },
            }, TestFactories.Seeded());

            Assert.That(board.FindMatches(), Is.Empty, "the cyclic square has no existing match");
            Assert.That(board.HasPossibleMove(), Is.False, "...and no swap can create one");
        }

        [Test]
        public void Shuffle_LeavesBoardMatchFreeAndPlayable([Range(0, 9)] int seed)
        {
            var board = new Board(6, 6, TestFactories.Seeded(colorCount: 5, seed: seed));
            Dictionary<int, int> before = ColorCounts(board);

            board.Shuffle(new SystemRandom(seed + 1000));

            Assert.That(board.FindMatches(), Is.Empty, "shuffle must not create instant matches");
            Assert.That(board.HasPossibleMove(), Is.True, "shuffle must leave at least one move");
            Assert.That(ColorCounts(board), Is.EqualTo(before), "shuffle only rearranges existing tiles");
        }

        [Test]
        public void Shuffle_NullRandom_Throws()
        {
            var board = new Board(6, 6, TestFactories.Seeded());
            Assert.Throws<ArgumentNullException>(() => board.Shuffle(null));
        }

        private static Dictionary<int, int> ColorCounts(Board board)
        {
            var counts = new Dictionary<int, int>();
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    if (board[new GridPosition(x, y)] is { } tile)
                        counts[tile.ColorIndex] = (counts.TryGetValue(tile.ColorIndex, out int c) ? c : 0) + 1;
                }
            }
            return counts;
        }
    }
}
