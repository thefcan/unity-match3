using Match3.Core;
using NUnit.Framework;
using static Match3.Tests.TestColors;

namespace Match3.Tests
{
    /// <summary>
    /// Covers the per-run match info that drives the time-attack bonus: a single
    /// straight run of 4+ is a "big match" worth bonus seconds, and the resolver must
    /// carry each wave's run lengths through to the game layer.
    /// </summary>
    public sealed class MatchRunTests
    {
        [Test]
        public void FindMatchRuns_HorizontalFour_IsOneRunOfLengthFour()
        {
            var board = Board.FromLayout(new[,]
            {
                { A, A, A, A }, // <- horizontal 4-run on the top row
                { B, C, B, C },
                { C, B, C, B },
                { B, C, B, C },
            }, TestFactories.Seeded());

            var runs = board.FindMatchRuns();

            Assert.That(runs.Count, Is.EqualTo(1));
            Assert.That(runs[0].Length, Is.EqualTo(4));
        }

        [Test]
        public void FindMatchRuns_LShape_IsTwoSeparateRuns()
        {
            // Column 0 and the bottom row are both all-A and share the corner — that's
            // ONE shared cell but TWO runs (a 3-run vertically and a 3-run horizontally).
            var board = Board.FromLayout(new[,]
            {
                { A, B, C },
                { A, C, B },
                { A, A, A },
            }, TestFactories.Seeded());

            var runs = board.FindMatchRuns();

            Assert.That(runs.Count, Is.EqualTo(2));
            foreach (MatchRun run in runs)
                Assert.That(run.Length, Is.EqualTo(3));
        }

        [Test]
        public void Resolve_CarriesRunLengths_AndCountsBigMatches()
        {
            // Bottom row is a horizontal 4-match. Refill draws are scripted so the new
            // tiles can't form a second wave.
            var board = Board.FromLayout(new[,]
            {
                { B, C, B, C },
                { C, B, C, B },
                { A, A, A, A },
            }, TestFactories.Scripted(5, draws: new[] { B, C, D, E }));

            ResolutionResult result = new CascadeResolver(new ScoreConfig(10, 1)).Resolve(board);

            Assert.That(result.Steps.Count, Is.EqualTo(1));

            CascadeStep step = result.Steps[0];
            Assert.That(step.RunLengths, Is.EquivalentTo(new[] { 4 }));
            Assert.That(step.BigMatchCount(4), Is.EqualTo(1), "A 4-run counts as a big match at threshold 4.");
            Assert.That(step.BigMatchCount(5), Is.EqualTo(0), "...but not at threshold 5.");
        }

        [Test]
        public void ThreeMatch_IsNotABigMatch()
        {
            var board = Board.FromLayout(new[,]
            {
                { B, C, B },
                { C, B, C },
                { A, A, A },
            }, TestFactories.Scripted(5, draws: new[] { B, C, D }));

            ResolutionResult result = new CascadeResolver(new ScoreConfig(10, 1)).Resolve(board);

            Assert.That(result.Steps[0].RunLengths, Is.EquivalentTo(new[] { 3 }));
            Assert.That(result.Steps[0].BigMatchCount(4), Is.EqualTo(0));
        }
    }
}
