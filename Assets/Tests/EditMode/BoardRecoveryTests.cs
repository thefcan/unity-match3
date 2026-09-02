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
        public void Shuffle_SettlesTheCampaignBoard_NotJustASmallOne([Range(0, 39)] int seed)
        {
            // The real campaign board, and its hardest deal to settle: 8x8 with only
            // four colours (levels 1-3) and squares live. A blind permutation lands
            // settled about once in two hundred tries, so this is exactly where the
            // shuffle used to give up and concede a board with a match already on it.
            var board = new Board(8, 8, TestFactories.Seeded(colorCount: 4, seed: seed));
            board.SetSquaresLive(true);

            board.Shuffle(new SystemRandom(seed + 1000));

            Assert.That(board.FindMatches(), Is.Empty, "a shuffled board must not start matched");
            Assert.That(board.FindSquares(), Is.Empty, "...nor with a live 2x2 square");
            Assert.That(board.HasPossibleMove(), Is.True, "...and must leave a move to make");
        }

        [Test]
        public void Shuffle_NeverParksAnIngredientOnTheExitRow([Range(0, 19)] int seed)
        {
            // An ingredient on row 0 is handed in by the very next resolve, whatever
            // the player does — a free delivery straight out of a shuffle.
            TileFactory factory = TestFactories.Seeded(colorCount: 5, seed: seed);
            var board = new Board(8, 8, factory);
            board.SetSquaresLive(true);
            board.SetTile(new GridPosition(2, 5), factory.CreateIngredient());
            board.SetTile(new GridPosition(5, 6), factory.CreateIngredient());

            board.Shuffle(new SystemRandom(seed + 2000));

            for (int x = 0; x < board.Width; x++)
            {
                Assert.That(board[new GridPosition(x, 0)].Value.Kind, Is.Not.EqualTo(TileKind.Ingredient),
                            $"an ingredient was parked on the exit row at x={x}");
            }
        }

        [Test]
        public void AShuffledBoard_StillBouncesAUselessSwap([Range(0, 9)] int seed)
        {
            // The coupling that makes the two tests above matter: HadMatches is "did
            // this resolve do anything", so a board that arrives already matched (or
            // with an ingredient on the floor) turns the player's next gesture —
            // however useless — into a spent move with a free cascade attached.
            var board = new Board(8, 8, TestFactories.Seeded(colorCount: 4, seed: seed));
            board.SetSquaresLive(true);
            board.Shuffle(new SystemRandom(seed + 3000));

            var resolver = new CascadeResolver(new ScoreConfig(10, 1),
                                               TestFactories.Seeded(colorCount: 4, seed: seed),
                                               new SystemRandom(seed));
            (GridPosition a, GridPosition b) = FirstUselessSwap(board);
            board.Swap(a, b);
            ResolutionResult result = resolver.ResolveSwap(board, a, b);

            Assert.That(result.HadMatches, Is.False,
                        "a swap that matches nothing must bounce back for free");
        }

        /// <summary>Any adjacent pair whose swap forms nothing — every board has one.</summary>
        private static (GridPosition, GridPosition) FirstUselessSwap(Board board)
        {
            for (int x = 0; x + 1 < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    var a = new GridPosition(x, y);
                    var b = new GridPosition(x + 1, y);
                    if (board.WouldSwapMatch(a, b)) continue;
                    // Specials would activate on contact; a plain pair cannot.
                    if (board[a] is { } ta && ta.IsSpecial) continue;
                    if (board[b] is { } tb && tb.IsSpecial) continue;
                    return (a, b);
                }
            }
            throw new InvalidOperationException("no useless swap on this board");
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
