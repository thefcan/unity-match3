using System;
using Match3.Core;
using NUnit.Framework;
using static Match3.Tests.TestColors;

namespace Match3.Tests
{
    /// <summary>
    /// Board- and factory-level behaviour of special candies: bombs never colour-match,
    /// SetTile morphs work, and a colour bomb keeps an otherwise dead board playable.
    /// </summary>
    public sealed class SpecialBoardTests
    {
        [Test]
        public void AdjacentColorBombs_NeverColorMatch()
        {
            var factory = TestFactories.Seeded();
            var board = Board.FromLayout(new[,]
            {
                { A, B, C },
                { B, C, A },
                { C, A, B },
            }, factory);
            board.SetTile(new GridPosition(0, 0), factory.CreateColorBomb());
            board.SetTile(new GridPosition(1, 0), factory.CreateColorBomb());
            board.SetTile(new GridPosition(2, 0), factory.CreateColorBomb());

            Assert.That(board.FindMatches(), Is.Empty, "three bombs in a row are not a colour run");
        }

        [Test]
        public void SetTile_ReplacesTheOccupant()
        {
            var factory = TestFactories.Seeded();
            var board = Board.FromLayout(new[,]
            {
                { A, B, C },
                { B, C, A },
                { C, A, B },
            }, factory);

            Tile striped = factory.CreateSpecial(D, TileKind.StripedH);
            board.SetTile(new GridPosition(1, 1), striped);

            Assert.That(board[new GridPosition(1, 1)].Value.Id, Is.EqualTo(striped.Id));
            Assert.That(board[new GridPosition(1, 1)].Value.Kind, Is.EqualTo(TileKind.StripedH));
        }

        [Test]
        public void LoneColorBomb_MakesADeadBoardPlayable()
        {
            var factory = TestFactories.Seeded(colorCount: 3);
            var board = Board.FromLayout(new[,]
            {
                { A, B, C },
                { B, C, A },
                { C, A, B },
            }, factory);
            Assert.That(board.HasPossibleMove(), Is.False, "the cyclic square is dead without specials");

            board.SetTile(new GridPosition(1, 1), factory.CreateColorBomb());

            Assert.That(board.HasPossibleMove(), Is.True, "swapping the bomb with any neighbour is a move");
            (GridPosition a, GridPosition b) = board.FindPossibleMove().Value;
            Assert.That(a == new GridPosition(1, 1) || b == new GridPosition(1, 1),
                "the suggested move involves the bomb");
        }

        [Test]
        public void Factory_MintsSpecialsWithKindAndColor()
        {
            var factory = TestFactories.Seeded();

            Tile striped = factory.CreateSpecial(B, TileKind.StripedV);
            Assert.That(striped.Kind, Is.EqualTo(TileKind.StripedV));
            Assert.That(striped.ColorIndex, Is.EqualTo(B));
            Assert.That(striped.IsSpecial, Is.True);
            Assert.That(striped.IsStriped, Is.True);
            Assert.That(striped.IsColorBomb, Is.False);

            Tile bomb = factory.CreateColorBomb();
            Assert.That(bomb.Kind, Is.EqualTo(TileKind.ColorBomb));
            Assert.That(bomb.ColorIndex, Is.EqualTo(Tile.NoColor));
            Assert.That(bomb.IsColorBomb, Is.True);

            Assert.That(striped.Id, Is.Not.EqualTo(bomb.Id), "ids stay unique across kinds");
        }

        [Test]
        public void Factory_SpecialWithNormalKind_IsJustANormalTile()
        {
            Tile tile = TestFactories.Seeded().CreateSpecial(C, TileKind.Normal);

            Assert.That(tile.Kind, Is.EqualTo(TileKind.Normal));
            Assert.That(tile.ColorIndex, Is.EqualTo(C));
        }

        [Test]
        public void Factory_SpecialWithInvalidColor_Throws()
        {
            var factory = TestFactories.Seeded(colorCount: 3);

            Assert.Throws<ArgumentOutOfRangeException>(() => factory.CreateSpecial(4, TileKind.StripedH));
            Assert.Throws<ArgumentOutOfRangeException>(() => factory.CreateSpecial(-1, TileKind.Wrapped));
        }
    }
}
