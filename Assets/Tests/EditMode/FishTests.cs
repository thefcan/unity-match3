using System.Linq;
using Match3.Core;
using NUnit.Framework;
using static Match3.Tests.TestColors;

namespace Match3.Tests
{
    /// <summary>
    /// The jelly fish: a 2x2 square match mints one (lowest-priority shape), and a
    /// detonating fish darts at the most urgent target on the board — jelly, then
    /// blockers, then a random plain candy — hitting exactly one cell.
    /// </summary>
    public sealed class FishTests
    {
        private static Board LatinSquare(TileFactory factory) => Board.FromLayout(new[,]
        {
            { A, B, C },
            { B, C, A },
            { C, A, B },
        }, factory);

        [Test]
        public void FindSquares_DetectsA2x2Block()
        {
            TileFactory factory = TestFactories.Scripted(5);
            Board board = Board.FromLayout(new[,]
            {
                { A, A, C },
                { A, A, B },
                { C, B, C },
            }, factory);

            var squares = board.FindSquares();

            Assert.That(squares.Count, Is.EqualTo(1));
            Assert.That(squares[0].Positions, Is.EquivalentTo(new[]
            {
                new GridPosition(0, 1), new GridPosition(1, 1),
                new GridPosition(0, 2), new GridPosition(1, 2),
            }));
        }

        [Test]
        public void SquareMatch_MintsAFish_InFullMode()
        {
            TileFactory factory = TestFactories.Scripted(5, D, E, D);
            Board board = Board.FromLayout(new[,]
            {
                { A, A, C },
                { A, A, B },
                { C, B, C },
            }, factory);

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom());
            ResolutionResult result = resolver.Resolve(board);

            Assert.That(result.Steps.Count, Is.GreaterThan(0));
            CascadeStep step = result.Steps[0];
            Assert.That(step.Creations.Count, Is.EqualTo(1));
            Assert.That(step.Creations[0].Created.Kind, Is.EqualTo(TileKind.Fish));
            Assert.That(step.Creations[0].Created.ColorIndex, Is.EqualTo(A));
            Assert.That(step.Creations[0].SourcePositions.Count, Is.EqualTo(4));
            Assert.That(step.Cleared.Count, Is.EqualTo(3), "three cells clear, the fourth morphs into the fish");
            Assert.That(board[step.Creations[0].Position].Value.IsFish, Is.True);
        }

        [Test]
        public void SquareMatch_NeverClears_InClassicMode()
        {
            TileFactory factory = TestFactories.Scripted(5);
            Board board = Board.FromLayout(new[,]
            {
                { A, A, C },
                { A, A, B },
                { C, B, C },
            }, factory);

            var resolver = new CascadeResolver(new ScoreConfig(10, 1)); // classic: no factory
            ResolutionResult result = resolver.Resolve(board);

            Assert.That(result.HadMatches, Is.False, "the classic resolver keeps 2x2 a dead shape");
        }

        [Test]
        public void SquareSharingARunOfFive_IsSpent_ColorBombWins()
        {
            TileFactory factory = TestFactories.Seeded(5, 3);
            Board board = Board.FromLayout(new[,]
            {
                { A, A, A, A, A },
                { A, A, B, C, D },
                { B, C, D, B, C },
            }, factory);

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SystemRandom(3));
            ResolutionResult result = resolver.Resolve(board);

            CascadeStep step = result.Steps[0];
            Assert.That(step.Creations.Count, Is.EqualTo(1));
            Assert.That(step.Creations[0].Created.Kind, Is.EqualTo(TileKind.ColorBomb));
        }

        [Test]
        public void SquareSharingAnLShape_IsSpent_WrappedWins()
        {
            TileFactory factory = TestFactories.Seeded(5, 4);
            Board board = Board.FromLayout(new[,]
            {
                { A, A, A, C },
                { A, A, D, B },
                { A, C, B, D },
                { C, D, C, B },
            }, factory);

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SystemRandom(4));
            ResolutionResult result = resolver.Resolve(board);

            CascadeStep step = result.Steps[0];
            Assert.That(step.Creations.Count, Is.EqualTo(1));
            Assert.That(step.Creations[0].Created.Kind, Is.EqualTo(TileKind.Wrapped));
        }

        [Test]
        public void SquareSharingARunOfFour_IsSpent_StripedWins()
        {
            TileFactory factory = TestFactories.Seeded(5, 5);
            Board board = Board.FromLayout(new[,]
            {
                { A, A, A, A },
                { A, A, C, B },
                { B, C, D, C },
            }, factory);

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SystemRandom(5));
            ResolutionResult result = resolver.Resolve(board);

            CascadeStep step = result.Steps[0];
            Assert.That(step.Creations.Count, Is.EqualTo(1));
            Assert.That(step.Creations[0].Created.Kind, Is.EqualTo(TileKind.StripedV),
                        "a horizontal 4-run yields the column-clearing candy; the square is spent");
        }

        [Test]
        public void SquareOverlappingAPlainTriple_StillMintsTheFish()
        {
            // A 3-run creates no special, so it spends nothing — the square wins.
            TileFactory factory = TestFactories.Seeded(5, 6);
            Board board = Board.FromLayout(new[,]
            {
                { A, C, B, D },
                { A, A, C, B },
                { A, A, D, C },
            }, factory);

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SystemRandom(6));
            ResolutionResult result = resolver.Resolve(board);

            CascadeStep step = result.Steps[0];
            Assert.That(step.Creations.Count, Is.EqualTo(1));
            Assert.That(step.Creations[0].Created.Kind, Is.EqualTo(TileKind.Fish));
        }

        [Test]
        public void FishStrike_PrefersJelly()
        {
            TileFactory factory = TestFactories.Scripted(5, D, E);
            Board board = LatinSquare(factory);
            board.SetTile(new GridPosition(1, 1), factory.CreateSpecial(C, TileKind.Fish));

            var jelly = new JellyGrid(3, 3);
            jelly.Set(new GridPosition(2, 0), 1);

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom(0));
            resolver.AttachJelly(jelly);
            ResolutionResult result = resolver.ResolveHammer(board, new GridPosition(1, 1));

            CascadeStep step = result.Steps[0];
            Assert.That(step.FishStrikes.Count, Is.EqualTo(1));
            Assert.That(step.FishStrikes[0].To, Is.EqualTo(new GridPosition(2, 0)));
            Assert.That(step.JellyHits.Any(hit => hit.Position.Equals(new GridPosition(2, 0))), Is.True);
            Assert.That(step.Detonations.Any(d => d.Kind == DetonationKind.FishStrike), Is.True);
        }

        [Test]
        public void FishStrike_PrefersChocolate_WhenNoJelly()
        {
            TileFactory factory = TestFactories.Scripted(5, D, E);
            Board board = LatinSquare(factory);
            board.SetTile(new GridPosition(1, 1), factory.CreateSpecial(C, TileKind.Fish));
            board.SetTile(new GridPosition(2, 2), factory.CreateChocolate());
            Tile chocolate = board[new GridPosition(2, 2)].Value;

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom(0));
            ResolutionResult result = resolver.ResolveHammer(board, new GridPosition(1, 1));

            CascadeStep step = result.Steps[0];
            Assert.That(step.FishStrikes[0].To, Is.EqualTo(new GridPosition(2, 2)));
            Assert.That(step.Cleared.Any(c => c.Tile.Id == chocolate.Id), Is.True,
                        "the dart hits the chocolate directly — no adjacency needed");
        }

        [Test]
        public void FishStrike_RandomNormalPick_FollowsTheInjectedRandom()
        {
            TileFactory factory = TestFactories.Scripted(5, D, E);
            Board board = LatinSquare(factory);
            board.SetTile(new GridPosition(1, 1), factory.CreateSpecial(C, TileKind.Fish));

            // Candidates in scan order: (0,0),(0,1),(0,2),(1,0),(1,2),(2,0),(2,1),(2,2).
            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom(3));
            ResolutionResult result = resolver.ResolveHammer(board, new GridPosition(1, 1));

            Assert.That(result.Steps[0].FishStrikes[0].To, Is.EqualTo(new GridPosition(1, 0)));
        }

        [Test]
        public void FishInAColourRun_ClearsAndStrikes()
        {
            TileFactory factory = TestFactories.Scripted(5, D, E, D, E);
            Board board = Board.FromLayout(new[,]
            {
                { B, C, D },
                { A, A, A },
                { C, D, B },
            }, factory);
            board.SetTile(new GridPosition(1, 1), factory.CreateSpecial(A, TileKind.Fish));

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom(0));
            ResolutionResult result = resolver.Resolve(board);

            CascadeStep step = result.Steps[0];
            Assert.That(step.FishStrikes.Count, Is.EqualTo(1));
            Assert.That(step.Cleared.Count, Is.EqualTo(4), "the three-run plus the dart's target");
        }

        [Test]
        public void FishFishSwap_FiresThreeStrikes()
        {
            TileFactory factory = TestFactories.Seeded(5, 7);
            Board board = LatinSquare(factory);
            var a = new GridPosition(1, 1);
            var b = new GridPosition(1, 2);
            board.SetTile(a, factory.CreateSpecial(C, TileKind.Fish));
            board.SetTile(b, factory.CreateSpecial(A, TileKind.Fish));

            board.Swap(a, b);
            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SystemRandom(7));
            ResolutionResult result = resolver.ResolveSwap(board, a, b);

            CascadeStep step = result.Steps[0];
            Assert.That(step.FishStrikes.Count, Is.EqualTo(3));
            Assert.That(step.Cleared.Count, Is.EqualTo(5), "both fish plus three distinct targets");
        }

        [Test]
        public void FishStripedSwap_DetonatesABeamAtEveryTarget()
        {
            TileFactory factory = TestFactories.Seeded(5, 8);
            Board board = Board.FromLayout(new[,]
            {
                { A, B, C, D },
                { B, C, D, A },
                { C, D, A, B },
                { D, A, B, C },
            }, factory);
            var a = new GridPosition(1, 1);
            var b = new GridPosition(1, 2);
            board.SetTile(a, factory.CreateSpecial(C, TileKind.Fish));
            board.SetTile(b, factory.CreateSpecial(A, TileKind.StripedH));

            board.Swap(a, b);
            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SystemRandom(8));
            ResolutionResult result = resolver.ResolveSwap(board, a, b);

            CascadeStep step = result.Steps[0];
            Assert.That(step.FishStrikes.Count, Is.EqualTo(3));
            Assert.That(step.Detonations.Count(d => d.Kind == DetonationKind.FishStrike), Is.EqualTo(3));
            Assert.That(step.Detonations.Count(d => d.Kind == DetonationKind.Row ||
                                                    d.Kind == DetonationKind.Column), Is.EqualTo(3),
                        "each dart carries the striped partner's beam");
        }

        [Test]
        public void FishWrappedSwap_BlastsAtEveryTarget()
        {
            TileFactory factory = TestFactories.Seeded(5, 9);
            Board board = Board.FromLayout(new[,]
            {
                { A, B, C, D },
                { B, C, D, A },
                { C, D, A, B },
                { D, A, B, C },
            }, factory);
            var a = new GridPosition(1, 1);
            var b = new GridPosition(1, 2);
            board.SetTile(a, factory.CreateSpecial(C, TileKind.Fish));
            board.SetTile(b, factory.CreateSpecial(A, TileKind.Wrapped));

            board.Swap(a, b);
            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SystemRandom(9));
            ResolutionResult result = resolver.ResolveSwap(board, a, b);

            CascadeStep step = result.Steps[0];
            Assert.That(step.FishStrikes.Count, Is.EqualTo(3));
            Assert.That(step.Detonations.Count(d => d.Kind == DetonationKind.Blast3x3), Is.EqualTo(3));
        }

        [Test]
        public void BombFishSwap_StrikesForTwoRounds()
        {
            TileFactory factory = TestFactories.Seeded(5, 10);
            Board board = Board.FromLayout(new[,]
            {
                { A, B, C, D },
                { B, C, D, A },
                { C, D, A, B },
                { D, A, B, C },
            }, factory);
            var a = new GridPosition(1, 1);
            var b = new GridPosition(1, 2);
            board.SetTile(a, factory.CreateColorBomb());
            board.SetTile(b, factory.CreateSpecial(A, TileKind.Fish));
            Tile bomb = board[a].Value;
            Tile fish = board[b].Value;

            board.Swap(a, b);
            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SystemRandom(10));
            ResolutionResult result = resolver.ResolveSwap(board, a, b);

            CascadeStep step = result.Steps[0];
            Assert.That(step.FishStrikes.Count, Is.EqualTo(6));
            Assert.That(step.Cleared.Any(c => c.Tile.Id == bomb.Id), Is.True);
            Assert.That(step.Cleared.Any(c => c.Tile.Id == fish.Id), Is.True);
        }

        [Test]
        public void SameSeed_SameStrikes()
        {
            ResolutionResult Run()
            {
                var rng = new SystemRandom(99);
                var factory = new TileFactory(5, rng);
                Board board = LatinSquare(factory);
                var a = new GridPosition(1, 1);
                var b = new GridPosition(1, 2);
                board.SetTile(a, factory.CreateSpecial(C, TileKind.Fish));
                board.SetTile(b, factory.CreateSpecial(A, TileKind.Fish));
                board.Swap(a, b);
                var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, rng);
                return resolver.ResolveSwap(board, a, b);
            }

            ResolutionResult first = Run();
            ResolutionResult second = Run();

            Assert.That(second.Steps[0].FishStrikes.Count, Is.EqualTo(first.Steps[0].FishStrikes.Count));
            for (int i = 0; i < first.Steps[0].FishStrikes.Count; i++)
                Assert.That(second.Steps[0].FishStrikes[i].To, Is.EqualTo(first.Steps[0].FishStrikes[i].To));
            Assert.That(second.TotalPoints, Is.EqualTo(first.TotalPoints));
        }

        [Test]
        public void InitialFill_NeverContainsASquare()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var factory = new TileFactory(5, new SystemRandom(seed));
                var board = new Board(6, 6, factory);
                Assert.That(board.FindSquares().Count, Is.EqualTo(0), $"seed {seed} produced a starting square");
                Assert.That(board.FindMatches().Count, Is.EqualTo(0));
            }
        }

        [Test]
        public void ThreeColourFill_StillWorks_WithTheSquareExclusion()
        {
            // The square exclusion never names a third colour (it always coincides
            // with a run exclusion when both fire), so 3 colours stay sufficient.
            for (int seed = 0; seed < 10; seed++)
            {
                var factory = new TileFactory(3, new SystemRandom(seed));
                var board = new Board(5, 5, factory);
                Assert.That(board.FindSquares().Count, Is.EqualTo(0));
                Assert.That(board.FindMatches().Count, Is.EqualTo(0));
            }
        }

        [Test]
        public void Finale_FishPaysTheStripedSizedBonus()
        {
            TileFactory factory = TestFactories.Scripted(5, D, E);
            Board board = LatinSquare(factory);
            board.SetTile(new GridPosition(1, 1), factory.CreateSpecial(C, TileKind.Fish));

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom(0));
            ResolutionResult result = resolver.ResolveFinale(board, 0);

            Assert.That(result.Steps.Count, Is.EqualTo(1));
            Assert.That(result.Steps[0].Points, Is.EqualTo(20 + 500),
                        "the fish and its target at 10 each, plus the finale bonus");
        }
    }
}
