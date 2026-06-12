using System.Linq;
using Match3.Core;
using NUnit.Framework;
using static Match3.Tests.TestColors;

namespace Match3.Tests
{
    public sealed class GravityTests
    {
        [Test]
        public void TilesFallIntoGapsBelowThem()
        {
            var board = Board.FromLayout(new[,]
            {
                { A, B, C },
                { _, _, A },
                { B, _, _ },
            }, TestFactories.Seeded());

            var falls = board.ApplyGravity();

            // col 0: A falls one cell onto the B. col 1: B falls to the floor.
            // col 2: A and C each fall one cell, preserving their order.
            Assert.That(falls.Count, Is.EqualTo(4));

            Assert.That(board[new GridPosition(0, 0)].Value.ColorIndex, Is.EqualTo(B));
            Assert.That(board[new GridPosition(0, 1)].Value.ColorIndex, Is.EqualTo(A));
            Assert.That(board[new GridPosition(1, 0)].Value.ColorIndex, Is.EqualTo(B));
            Assert.That(board[new GridPosition(2, 0)].Value.ColorIndex, Is.EqualTo(A));
            Assert.That(board[new GridPosition(2, 1)].Value.ColorIndex, Is.EqualTo(C));
        }

        [Test]
        public void GravityPreservesTileIdentity()
        {
            var board = Board.FromLayout(new[,]
            {
                { A, B, C },
                { _, _, A },
                { B, _, _ },
            }, TestFactories.Seeded());

            int fallingId = board[new GridPosition(1, 2)].Value.Id; // the B at the top of col 1

            var falls = board.ApplyGravity();

            var fall = falls.Single(f => f.Tile.Id == fallingId);
            Assert.That(fall.From, Is.EqualTo(new GridPosition(1, 2)));
            Assert.That(fall.To, Is.EqualTo(new GridPosition(1, 0)));
            Assert.That(board[new GridPosition(1, 0)].Value.Id, Is.EqualTo(fallingId),
                "The exact same tile (same Id) must land at the bottom.");
        }

        [Test]
        public void StableBoard_ProducesNoFalls()
        {
            var board = Board.FromLayout(new[,]
            {
                { A, B, A },
                { B, A, B },
                { A, B, A },
            }, TestFactories.Seeded());

            Assert.That(board.ApplyGravity(), Is.Empty);
        }

        [Test]
        public void Refill_FillsAllGapsFromTheTop()
        {
            var board = Board.FromLayout(new[,]
            {
                { _, _, _ },
                { _, B, A },
                { B, A, B },
            }, TestFactories.Seeded());

            board.ApplyGravity(); // gaps are already on top, but mirror production order
            var spawns = board.Refill();

            Assert.That(spawns.Count, Is.EqualTo(4));

            // Every cell is now filled.
            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                    Assert.That(board[new GridPosition(x, y)].HasValue);

            // Column 0 received two spawns, stacked 1 and 2 rows above the board
            // so the view can drop them in as a column.
            var column0 = spawns.Where(s => s.Position.X == 0).OrderBy(s => s.Position.Y).ToList();
            Assert.That(column0.Count, Is.EqualTo(2));
            Assert.That(column0[0].SpawnHeightOffset, Is.EqualTo(1));
            Assert.That(column0[1].SpawnHeightOffset, Is.EqualTo(2));
        }
    }
}
