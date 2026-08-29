using System.Linq;
using Match3.Core;
using NUnit.Framework;
using static Match3.Tests.TestColors;

namespace Match3.Tests
{
    /// <summary>
    /// The Sugar Crush finale: unused moves convert normal candies to striped (4 per
    /// 5 moves), every special on the board fires, cleared specials pay a bonus, and
    /// chocolate never spreads (the level is already won).
    /// </summary>
    public sealed class FinaleTests
    {
        /// <summary>A 3x3 with no natural runs — the finale's blank canvas.</summary>
        private static Board LatinSquare(TileFactory factory) => Board.FromLayout(new[,]
        {
            { A, B, C },
            { B, C, A },
            { C, A, B },
        }, factory);

        [Test]
        public void NothingToCelebrate_ReturnsEmptyRecording()
        {
            TileFactory factory = TestFactories.Scripted(5);
            Board board = LatinSquare(factory);

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom(new int[0]));
            ResolutionResult result = resolver.ResolveFinale(board, 0);

            Assert.That(result.Steps.Count, Is.EqualTo(0));
        }

        [Test]
        public void ConversionQuota_IsFourStripedPerFiveMoves()
        {
            var rng = new SystemRandom(42);
            var factory = new TileFactory(5, rng);
            Board board = LatinSquare(factory);

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, rng);
            ResolutionResult result = resolver.ResolveFinale(board, 5);

            Assert.That(result.Steps.Count, Is.GreaterThan(0));
            CascadeStep conversionStep = result.Steps[0];
            Assert.That(conversionStep.Creations.Count, Is.EqualTo(4), "5 moves * 4/5 = 4 striped");
            Assert.That(conversionStep.Cleared.Count, Is.EqualTo(0), "the morph step clears nothing");
            foreach (SpecialCreation creation in conversionStep.Creations)
            {
                bool striped = creation.Created.Kind == TileKind.StripedH ||
                               creation.Created.Kind == TileKind.StripedV;
                Assert.That(striped, Is.True);
                Assert.That(creation.Created.ColorIndex, Is.EqualTo(creation.Replaced.ColorIndex),
                            "conversion keeps the candy's colour");
            }
        }

        [Test]
        public void FewMovesRoundDown_OneMove_MeansNoConversions()
        {
            TileFactory factory = TestFactories.Scripted(5);
            Board board = LatinSquare(factory);

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom(new int[0]));
            ResolutionResult result = resolver.ResolveFinale(board, 1); // 1 * 4 / 5 = 0

            Assert.That(result.Steps.Count, Is.EqualTo(0));
        }

        [Test]
        public void DormantStriped_FiresAndPaysTheBonus()
        {
            // Refill draws: three cells respawn on row y=2 (x = 0,1,2) after the row
            // blast at y=1 collapses one row — colours picked to create no new runs.
            TileFactory factory = TestFactories.Scripted(5, B, C, A);
            Board board = LatinSquare(factory);
            board.SetTile(new GridPosition(1, 1), factory.CreateSpecial(C, TileKind.StripedH));
            Tile striped = board[new GridPosition(1, 1)].Value;

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom(new int[0]));
            ResolutionResult result = resolver.ResolveFinale(board, 0);

            Assert.That(result.Steps.Count, Is.EqualTo(1));
            CascadeStep step = result.Steps[0];
            Assert.That(step.Detonations.Count, Is.EqualTo(1));
            Assert.That(step.Detonations[0].Kind, Is.EqualTo(DetonationKind.Row));
            Assert.That(step.Cleared.Any(c => c.Tile.Id == striped.Id), Is.True);
            Assert.That(step.Points, Is.EqualTo(30 + 500), "3 tiles * 10 + striped finale bonus");
        }

        [Test]
        public void LockedSpecial_PopsItsCageAndSurvives()
        {
            TileFactory factory = TestFactories.Scripted(5);
            Board board = LatinSquare(factory);
            board.SetTile(new GridPosition(1, 1), factory.CreateSpecial(C, TileKind.StripedH));
            Tile striped = board[new GridPosition(1, 1)].Value;

            LockGrid locks = LockGrid.FromCells(3, 3, new[] { new GridPosition(1, 1) });
            board.AttachLocks(locks);
            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom(new int[0]));
            resolver.AttachLocks(locks);

            ResolutionResult result = resolver.ResolveFinale(board, 0);

            Assert.That(result.Steps.Count, Is.EqualTo(1));
            Assert.That(result.Steps[0].LockBreaks.Count, Is.EqualTo(1));
            Assert.That(result.Steps[0].Cleared.Count, Is.EqualTo(0), "the lock absorbed the finale hit");
            Assert.That(board[new GridPosition(1, 1)].Value.Id, Is.EqualTo(striped.Id));
            Assert.That(locks.TotalRemaining, Is.EqualTo(0));
        }

        [Test]
        public void Wrapped_KeepsItsDoubleBlastInTheFinale()
        {
            var rng = new SystemRandom(7);
            var factory = new TileFactory(5, rng);
            Board board = LatinSquare(factory);
            board.SetTile(new GridPosition(1, 1), factory.CreateSpecial(A, TileKind.Wrapped));
            Tile wrapped = board[new GridPosition(1, 1)].Value;

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, rng);
            ResolutionResult result = resolver.ResolveFinale(board, 0);

            Assert.That(result.Steps.Count, Is.GreaterThanOrEqualTo(2), "first blast, then the primed second");
            bool consumedLater = result.Steps.Skip(1).Any(s => s.Cleared.Any(c => c.Tile.Id == wrapped.Id));
            Assert.That(consumedLater, Is.True, "the wrapped survives wave 0 and is consumed by its second blast");
            CascadeStep consuming = result.Steps.Skip(1).First(s => s.Cleared.Any(c => c.Tile.Id == wrapped.Id));
            Assert.That(consuming.Points, Is.GreaterThanOrEqualTo(1000), "wrapped finale bonus is in the wave score");
        }

        [Test]
        public void ChocolateNeverSpreads_DuringAFinale()
        {
            var rng = new SystemRandom(11);
            var factory = new TileFactory(5, rng);
            Board board = Board.FromLayout(new[,]
            {
                { A, B, C, D },
                { B, C, D, A },
                { C, D, A, B },
                { D, A, B, C },
            }, factory);
            board.SetTile(new GridPosition(3, 3), factory.CreateChocolate());
            board.SetTile(new GridPosition(0, 0), factory.CreateSpecial(D, TileKind.StripedH));

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, rng);
            ResolutionResult result = resolver.ResolveFinale(board, 0);

            Assert.That(result.Steps.Count, Is.GreaterThan(0));
            foreach (CascadeStep step in result.Steps)
                Assert.That(step.ChocolateSpreads.Count, Is.EqualTo(0));
        }

        [Test]
        public void SameSeed_SameFinale()
        {
            ResolutionResult Run()
            {
                var rng = new SystemRandom(1234);
                var factory = new TileFactory(5, rng);
                Board board = LatinSquare(factory);
                board.SetTile(new GridPosition(2, 2), factory.CreateSpecial(B, TileKind.Wrapped));
                var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, rng);
                return resolver.ResolveFinale(board, 5);
            }

            ResolutionResult first = Run();
            ResolutionResult second = Run();

            Assert.That(second.Steps.Count, Is.EqualTo(first.Steps.Count));
            Assert.That(second.TotalPoints, Is.EqualTo(first.TotalPoints));
            for (int i = 0; i < first.Steps[0].Creations.Count; i++)
            {
                Assert.That(second.Steps[0].Creations[i].Position,
                            Is.EqualTo(first.Steps[0].Creations[i].Position));
            }
        }

        [Test]
        public void ColorBomb_PaysTheBigBonus()
        {
            var rng = new SystemRandom(5);
            var factory = new TileFactory(5, rng);
            Board board = LatinSquare(factory);
            board.SetTile(new GridPosition(1, 1), factory.CreateColorBomb());

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, rng);
            ResolutionResult result = resolver.ResolveFinale(board, 0);

            Assert.That(result.Steps.Count, Is.GreaterThan(0));
            Assert.That(result.Steps[0].Detonations.Any(d => d.Kind == DetonationKind.ColorClear), Is.True);
            Assert.That(result.Steps[0].Points, Is.GreaterThanOrEqualTo(5000));
        }

        [Test]
        public void FinaleQuota_ScalesWithTheMovesHandedIn_SoAClampMatters()
        {
            // The relaxed budget (999) would convert the whole board; GameManager
            // clamps it to the level's authored limit (FinaleMoveBudget). This pins
            // WHY that clamp exists: quota = moves * 4/5, unbounded by the board.
            TileFactory factory = TestFactories.Seeded(5, seed: 4);
            Board board = new Board(8, 8, factory);
            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SystemRandom(9));

            int converted = 0;
            foreach (CascadeStep step in resolver.ResolveFinale(board, remainingMoves: 999).Steps)
                converted += step.Creations.Count;

            // 999 * 4/5 = 799 > 64 cells, so a relaxed win with no clamp turns
            // EVERY normal candy on the board into a striped one.
            Assert.That(converted, Is.GreaterThan(40));

            TileFactory factory2 = TestFactories.Seeded(5, seed: 4);
            Board board2 = new Board(8, 8, factory2);
            var resolver2 = new CascadeResolver(new ScoreConfig(10, 1), factory2, new SystemRandom(9));

            int clamped = 0;
            foreach (CascadeStep step in resolver2.ResolveFinale(board2, remainingMoves: 5).Steps)
                clamped += step.Creations.Count;

            Assert.That(clamped, Is.LessThan(converted));
        }
    }
}
