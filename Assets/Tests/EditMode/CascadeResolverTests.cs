using System.Linq;
using Match3.Core;
using NUnit.Framework;
using static Match3.Tests.TestColors;

namespace Match3.Tests
{
    public sealed class CascadeResolverTests
    {
        private static CascadeResolver DefaultResolver() =>
            new CascadeResolver(new ScoreConfig(pointsPerTile: 10, cascadeMultiplierStep: 1));

        [Test]
        public void StableBoard_ResolvesToZeroSteps()
        {
            var board = Board.FromLayout(new[,]
            {
                { A, B, A },
                { B, A, B },
                { A, B, A },
            }, TestFactories.Seeded());

            var result = DefaultResolver().Resolve(board);

            Assert.That(result.HadMatches, Is.False);
            Assert.That(result.Steps, Is.Empty);
            Assert.That(result.TotalPoints, Is.Zero);
        }

        [Test]
        public void SingleMatch_ClearsFallsAndRefills()
        {
            // Refill draws are scripted so the new tiles can't accidentally re-match:
            // bottom row clears -> 3 spawns drawn as D, C, E.
            var board = Board.FromLayout(new[,]
            {
                { B, D, C },
                { C, B, D },
                { A, A, A },
            }, TestFactories.Scripted(5, draws: new[] { D, C, E }));

            var result = DefaultResolver().Resolve(board);

            Assert.That(result.Steps.Count, Is.EqualTo(1));

            CascadeStep step = result.Steps[0];
            Assert.That(step.Cleared.Count, Is.EqualTo(3));
            Assert.That(step.Falls.Count, Is.EqualTo(6), "Both rows above the cleared row fall one cell.");
            Assert.That(step.Spawns.Count, Is.EqualTo(3), "One new tile per column.");
            Assert.That(step.Points, Is.EqualTo(3 * 10), "Wave 0 scores at 1x multiplier.");

            Assert.That(board.FindMatches(), Is.Empty, "The board must end up stable.");
        }

        [Test]
        public void GravityDrivenCascade_ResolvesInTwoWavesWithMultiplier()
        {
            // Wave 0: the vertical A A A in column 0 clears, and the B above it falls
            // to the floor — completing B B B across the bottom row (the B's in columns
            // 1 and 2 were already there). Wave 1 clears that row. Refill colours are
            // scripted so no third wave can occur.
            var board = Board.FromLayout(new[,]
            {
                { B, D, C, D },
                { A, C, D, C },
                { A, D, C, D },
                { A, B, B, C },
            }, TestFactories.Scripted(5, draws: new[] { D, C, E, A, B, A }));

            var result = DefaultResolver().Resolve(board);

            Assert.That(result.Steps.Count, Is.EqualTo(2), "Expected exactly one chain reaction.");

            CascadeStep wave0 = result.Steps[0];
            Assert.That(wave0.CascadeIndex, Is.EqualTo(0));
            Assert.That(wave0.Cleared.Select(c => c.Position), Is.EquivalentTo(new[]
            {
                new GridPosition(0, 0), new GridPosition(0, 1), new GridPosition(0, 2),
            }));
            // The B drops from the top of column 0 all the way to the floor.
            Assert.That(wave0.Falls.Count, Is.EqualTo(1));
            Assert.That(wave0.Falls[0].From, Is.EqualTo(new GridPosition(0, 3)));
            Assert.That(wave0.Falls[0].To, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(wave0.Points, Is.EqualTo(3 * 10 * 1));

            CascadeStep wave1 = result.Steps[1];
            Assert.That(wave1.CascadeIndex, Is.EqualTo(1));
            Assert.That(wave1.Cleared.Select(c => c.Position), Is.EquivalentTo(new[]
            {
                new GridPosition(0, 0), new GridPosition(1, 0), new GridPosition(2, 0),
            }));
            Assert.That(wave1.Points, Is.EqualTo(3 * 10 * 2), "Wave 1 scores at 2x multiplier.");

            Assert.That(result.TotalPoints, Is.EqualTo(30 + 60));
            Assert.That(board.FindMatches(), Is.Empty, "The board must end up stable.");
        }

        [Test]
        public void ResolutionLeavesEveryCellFilled()
        {
            var board = Board.FromLayout(new[,]
            {
                { B, D, C },
                { C, B, D },
                { A, A, A },
            }, TestFactories.Scripted(5, draws: new[] { D, C, E }));

            DefaultResolver().Resolve(board);

            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                    Assert.That(board[new GridPosition(x, y)].HasValue, $"Cell ({x},{y}) should be refilled.");
        }
    }

    public sealed class ScoreConfigTests
    {
        [Test]
        public void WaveZero_ScoresBasePoints()
        {
            var config = new ScoreConfig(pointsPerTile: 10, cascadeMultiplierStep: 1);

            Assert.That(config.PointsFor(clearedCount: 3, cascadeIndex: 0), Is.EqualTo(30));
        }

        [Test]
        public void LaterWaves_ScaleLinearlyWithCascadeIndex()
        {
            var config = new ScoreConfig(pointsPerTile: 10, cascadeMultiplierStep: 1);

            Assert.That(config.PointsFor(4, 1), Is.EqualTo(80), "4 tiles at 2x.");
            Assert.That(config.PointsFor(4, 2), Is.EqualTo(120), "4 tiles at 3x.");
        }

        [Test]
        public void CustomMultiplierStep_IsApplied()
        {
            var config = new ScoreConfig(pointsPerTile: 5, cascadeMultiplierStep: 2);

            Assert.That(config.PointsFor(3, 1), Is.EqualTo(45), "3 * 5 * (1 + 1*2).");
        }
    }
}
