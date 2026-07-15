using System.Linq;
using Match3.Core;
using NUnit.Framework;
using static Match3.Tests.TestColors;

namespace Match3.Tests
{
    /// <summary>
    /// Special+special (and colour bomb) swaps resolved through ResolveSwap. Each test
    /// builds a match-free checkerboard, plants specials with SetTile, commits the
    /// swap exactly like ResolvingState does, then resolves it.
    /// </summary>
    public sealed class SwapComboTests
    {
        private static readonly int[,] Checkerboard =
        {
            { A, B, A, B },
            { C, D, C, D },
            { A, B, A, B },
            { C, D, C, D },
        };

        private static CascadeResolver FullResolver(TileFactory factory, IRandom random = null) =>
            new CascadeResolver(new ScoreConfig(pointsPerTile: 10, cascadeMultiplierStep: 1),
                                factory, random ?? new SystemRandom(0));

        private static ResolutionResult Swap(Board board, CascadeResolver resolver, GridPosition from, GridPosition to)
        {
            board.Swap(from, to);
            return resolver.ResolveSwap(board, from, to);
        }

        [Test]
        public void SwapRules_ClassifyEveryPair()
        {
            var f = TestFactories.Seeded();
            Tile normal = f.Create(A);
            Tile stripedH = f.CreateSpecial(A, TileKind.StripedH);
            Tile stripedV = f.CreateSpecial(B, TileKind.StripedV);
            Tile wrapped = f.CreateSpecial(C, TileKind.Wrapped);
            Tile bomb = f.CreateColorBomb();

            Assert.That(SwapRules.Classify(normal, normal), Is.EqualTo(SwapKind.None));
            Assert.That(SwapRules.Classify(stripedH, normal), Is.EqualTo(SwapKind.None), "striped only activates when matched");
            Assert.That(SwapRules.Classify(wrapped, normal), Is.EqualTo(SwapKind.None));
            Assert.That(SwapRules.Classify(stripedH, stripedV), Is.EqualTo(SwapKind.StripedStriped));
            Assert.That(SwapRules.Classify(wrapped, stripedV), Is.EqualTo(SwapKind.StripedWrapped));
            Assert.That(SwapRules.Classify(wrapped, wrapped), Is.EqualTo(SwapKind.WrappedWrapped));
            Assert.That(SwapRules.Classify(bomb, normal), Is.EqualTo(SwapKind.BombNormal));
            Assert.That(SwapRules.Classify(bomb, stripedH), Is.EqualTo(SwapKind.BombStriped));
            Assert.That(SwapRules.Classify(bomb, wrapped), Is.EqualTo(SwapKind.BombWrapped));
            Assert.That(SwapRules.Classify(bomb, bomb), Is.EqualTo(SwapKind.BombBomb));
        }

        [Test]
        public void StripedPlusStriped_ClearsACross()
        {
            var factory = TestFactories.Seeded();
            var board = Board.FromLayout(Checkerboard, factory);
            board.SetTile(new GridPosition(1, 0), factory.CreateSpecial(A, TileKind.StripedH));
            board.SetTile(new GridPosition(1, 1), factory.CreateSpecial(B, TileKind.StripedV));

            var result = Swap(board, FullResolver(factory), new GridPosition(1, 0), new GridPosition(1, 1));

            CascadeStep wave0 = result.Steps[0];
            Detonation cross = wave0.Detonations.Single(d => d.Kind == DetonationKind.Cross);
            Assert.That(cross.Origin, Is.EqualTo(new GridPosition(1, 1)), "the cross fires at the landing cell");
            Assert.That(wave0.Cleared, Has.Count.EqualTo(4 + 4 - 1), "full row + full column, shared cell once");
            Assert.That(wave0.RunLengths, Is.Empty, "a combo wave is not a colour match");
        }

        [Test]
        public void StripedPlusWrapped_ClearsThreeRowsAndThreeColumns()
        {
            var factory = TestFactories.Seeded();
            var board = Board.FromLayout(Checkerboard, factory);
            board.SetTile(new GridPosition(1, 1), factory.CreateSpecial(A, TileKind.StripedH));
            board.SetTile(new GridPosition(1, 2), factory.CreateSpecial(B, TileKind.Wrapped));

            var result = Swap(board, FullResolver(factory), new GridPosition(1, 1), new GridPosition(1, 2));

            CascadeStep wave0 = result.Steps[0];
            Assert.That(wave0.Detonations.Single().Kind, Is.EqualTo(DetonationKind.TripleCross));
            // Rows y1..y3 and columns x0..x2 on a 4x4 board: 12 + 12 - 9 overlap = 15.
            Assert.That(wave0.Cleared, Has.Count.EqualTo(15));
        }

        [Test]
        public void WrappedPlusWrapped_FiresTwoBigBlasts_AndConsumesBoth()
        {
            var factory = TestFactories.Seeded();
            var board = Board.FromLayout(Checkerboard, factory);
            Tile wrappedA = factory.CreateSpecial(A, TileKind.Wrapped);
            Tile wrappedB = factory.CreateSpecial(B, TileKind.Wrapped);
            board.SetTile(new GridPosition(1, 1), wrappedA);
            board.SetTile(new GridPosition(1, 2), wrappedB);

            var result = Swap(board, FullResolver(factory), new GridPosition(1, 1), new GridPosition(1, 2));

            CascadeStep wave0 = result.Steps[0];
            Assert.That(wave0.Detonations.Count(d => d.Kind == DetonationKind.Blast5x5), Is.EqualTo(2));
            // NUnit 3.5: chained .And.Contain doesn't compile — assert separately.
            Assert.That(wave0.Cleared.Select(c => c.Tile.Id), Does.Contain(wrappedA.Id),
                "combo-consumed wrappeds do NOT survive for a second blast");
            Assert.That(wave0.Cleared.Select(c => c.Tile.Id), Does.Contain(wrappedB.Id));
            // 5x5 blasts centred at (1,1) and (1,2) each cover the whole 4x4 board.
            Assert.That(wave0.Cleared, Has.Count.EqualTo(16));
        }

        [Test]
        public void BombPlusNormal_ClearsEveryTileOfThatColor_WithoutBouncing()
        {
            var factory = TestFactories.Seeded();
            var board = Board.FromLayout(Checkerboard, factory);
            Tile bomb = factory.CreateColorBomb();
            board.SetTile(new GridPosition(1, 1), bomb); // replaces the D at (1,1)

            // Drag the bomb DOWN onto the D at (1,0): no colour match anywhere, yet
            // the swap must still resolve (activation swaps never bounce).
            var result = Swap(board, FullResolver(factory), new GridPosition(1, 1), new GridPosition(1, 0));

            Assert.That(result.HadMatches, Is.True);
            CascadeStep wave0 = result.Steps[0];
            Assert.That(wave0.RunLengths, Is.Empty);
            Detonation clear = wave0.Detonations.Single(d => d.Kind == DetonationKind.ColorClear);
            Assert.That(clear.Source.Id, Is.EqualTo(bomb.Id));
            // All four D tiles (one of them now sits at (1,1)) plus the bomb itself.
            Assert.That(wave0.Cleared, Has.Count.EqualTo(5));
            Assert.That(wave0.Cleared.Select(c => c.Tile.Id), Does.Contain(bomb.Id));
        }

        [Test]
        public void BombPlusBomb_ClearsTheWholeBoard()
        {
            var factory = TestFactories.Seeded();
            var board = Board.FromLayout(Checkerboard, factory);
            board.SetTile(new GridPosition(1, 1), factory.CreateColorBomb());
            board.SetTile(new GridPosition(1, 2), factory.CreateColorBomb());

            var result = Swap(board, FullResolver(factory), new GridPosition(1, 1), new GridPosition(1, 2));

            CascadeStep wave0 = result.Steps[0];
            Assert.That(wave0.Detonations.Single().Kind, Is.EqualTo(DetonationKind.BoardClear));
            Assert.That(wave0.Cleared, Has.Count.EqualTo(16));
        }

        [Test]
        public void BombPlusStriped_ConvertsThatColorToStriped_AndDetonatesThemAll()
        {
            var factory = TestFactories.Seeded();
            var board = Board.FromLayout(Checkerboard, factory);
            Tile striped = factory.CreateSpecial(A, TileKind.StripedH);
            Tile bomb = factory.CreateColorBomb();
            board.SetTile(new GridPosition(1, 0), striped); // replaces the D at (1,0)
            board.SetTile(new GridPosition(1, 1), bomb);

            // Scripted orientations for the four converted A tiles, in board order
            // (x asc, then y asc): (0,1)->H, (0,3)->V, (2,1)->H, (2,3)->V.
            var resolver = FullResolver(factory, new SequenceRandom(0, 1, 0, 1));
            var result = Swap(board, resolver, new GridPosition(1, 0), new GridPosition(1, 1));

            CascadeStep wave0 = result.Steps[0];

            Assert.That(wave0.Creations, Has.Count.EqualTo(4), "every plain A morphs into a striped");
            Assert.That(wave0.Creations.Select(c => c.Created.Kind),
                Is.EquivalentTo(new[] { TileKind.StripedH, TileKind.StripedV, TileKind.StripedH, TileKind.StripedV }));
            foreach (SpecialCreation conversion in wave0.Creations)
            {
                Assert.That(wave0.Cleared.Select(c => c.Position), Does.Contain(conversion.Position),
                    "conversions are consumed by their own blast in the same wave");
            }

            // Two row detonations (y1 twice collapses to distinct sources), two column
            // detonations, plus the original striped's own row: 5 in total.
            Assert.That(wave0.Detonations.Count(d => d.Kind == DetonationKind.Row), Is.EqualTo(3));
            Assert.That(wave0.Detonations.Count(d => d.Kind == DetonationKind.Column), Is.EqualTo(2));

            // Rows y1 (twice), column x0, column x2, row y1 from the swapped striped,
            // plus the bomb cell: y1(4) + x0(+3) + x2(+3) + bomb(1,0) = 11 cells.
            Assert.That(wave0.Cleared, Has.Count.EqualTo(11));
            Assert.That(wave0.Cleared.Select(c => c.Tile.Id), Does.Contain(bomb.Id));
            Assert.That(wave0.Cleared.Select(c => c.Tile.Id), Does.Contain(striped.Id));
        }

        [Test]
        public void BombPlusWrapped_ClearsTheColor_AndTheWrappedDoubleBlasts()
        {
            var factory = TestFactories.Seeded();
            var board = Board.FromLayout(Checkerboard, factory);
            Tile wrapped = factory.CreateSpecial(A, TileKind.Wrapped);
            Tile bomb = factory.CreateColorBomb();
            board.SetTile(new GridPosition(1, 0), wrapped); // replaces the D
            board.SetTile(new GridPosition(1, 1), bomb);

            var result = Swap(board, FullResolver(factory), new GridPosition(1, 0), new GridPosition(1, 1));

            CascadeStep wave0 = result.Steps[0];
            Assert.That(wave0.Detonations.Any(d => d.Kind == DetonationKind.ColorClear && d.Source.Id == bomb.Id));
            Assert.That(wave0.Detonations.Any(d => d.Kind == DetonationKind.Blast3x3 && d.Source.Id == wrapped.Id),
                "the wrapped is caught in its own colour's clear and blasts");
            Assert.That(wave0.Cleared.Select(c => c.Tile.Id), Has.No.Member(wrapped.Id),
                "first blast: the wrapped survives, primed");

            Detonation second = result.Steps
                .Skip(1)
                .SelectMany(s => s.Detonations)
                .Single(d => d.Source.Id == wrapped.Id);
            Assert.That(second.Kind, Is.EqualTo(DetonationKind.Blast3x3), "second blast consumes it next wave");
        }

        [Test]
        public void UselessSwap_ReturnsEmptyResult_WithoutMutatingTheBoard()
        {
            var factory = TestFactories.Seeded();
            var board = Board.FromLayout(Checkerboard, factory);
            var a = new GridPosition(0, 0);
            var b = new GridPosition(0, 1);

            board.Swap(a, b);
            int idAtA = board[a].Value.Id;
            int idAtB = board[b].Value.Id;

            var result = FullResolver(factory).ResolveSwap(board, a, b);

            Assert.That(result.HadMatches, Is.False);
            Assert.That(result.Steps, Is.Empty);
            Assert.That(board[a].Value.Id, Is.EqualTo(idAtA), "a no-op resolve must not mutate");
            Assert.That(board[b].Value.Id, Is.EqualTo(idAtB));
        }
    }
}
