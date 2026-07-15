using System;
using System.Collections.Generic;
using Match3.Core;
using NUnit.Framework;
using static Match3.Tests.TestColors;

namespace Match3.Tests
{
    /// <summary>
    /// Moves-mode win condition: per-colour collection counted from cleared tiles,
    /// score objectives from step points, and completion across multiple waves.
    /// </summary>
    public sealed class ObjectiveTrackerTests
    {
        /// <summary>A minimal step: only Cleared and Points matter to the tracker.</summary>
        private static CascadeStep Step(int points, params (int color, int count)[] cleared)
        {
            var tiles = new List<ClearedTile>();
            int id = 1000 + points;
            foreach ((int color, int count) in cleared)
                for (int i = 0; i < count; i++)
                    tiles.Add(new ClearedTile(new Tile(id++, color), new GridPosition(0, 0)));

            return new CascadeStep(0, tiles, new List<TileFall>(), new List<TileSpawn>(), points, new List<int>());
        }

        [Test]
        public void CollectColor_CountsOnlyMatchingTiles_AcrossSteps()
        {
            var tracker = new ObjectiveTracker(new[] { new Objective(ObjectiveType.CollectColor, A, 5) });

            tracker.Consume(Step(30, (A, 2), (B, 4)));
            Assert.That(tracker.Progress(0), Is.EqualTo(2));
            Assert.That(tracker.AllComplete, Is.False);

            tracker.Consume(Step(40, (A, 3)));
            Assert.That(tracker.Progress(0), Is.EqualTo(5));
            Assert.That(tracker.AllComplete, Is.True);
        }

        [Test]
        public void ScoreObjective_TracksAccumulatedPoints()
        {
            var tracker = new ObjectiveTracker(new[] { new Objective(ObjectiveType.Score, 0, 100) });

            tracker.Consume(Step(60));
            Assert.That(tracker.Progress(0), Is.EqualTo(60));
            Assert.That(tracker.IsComplete(0), Is.False);

            tracker.Consume(Step(40));
            Assert.That(tracker.IsComplete(0), Is.True, "meeting the target exactly completes it");
            Assert.That(tracker.Score, Is.EqualTo(100));
        }

        [Test]
        public void AllComplete_RequiresEveryObjective()
        {
            var tracker = new ObjectiveTracker(new[]
            {
                new Objective(ObjectiveType.Score, 0, 50),
                new Objective(ObjectiveType.CollectColor, B, 3),
            });

            tracker.Consume(Step(80, (A, 3)));
            Assert.That(tracker.IsComplete(0), Is.True);
            Assert.That(tracker.AllComplete, Is.False, "the B collection is still open");

            tracker.Consume(Step(10, (B, 3)));
            Assert.That(tracker.AllComplete, Is.True);
        }

        [Test]
        public void Progress_IsClampedToTheTarget_ForDisplay()
        {
            var tracker = new ObjectiveTracker(new[] { new Objective(ObjectiveType.CollectColor, A, 3) });

            tracker.Consume(Step(100, (A, 9)));

            Assert.That(tracker.Progress(0), Is.EqualTo(3), "overshoot never displays as 9/3");
        }

        [Test]
        public void ColorBombClears_DoNotCountForAnyColor()
        {
            var tracker = new ObjectiveTracker(new[] { new Objective(ObjectiveType.CollectColor, A, 2) });

            // A cleared colour bomb carries NoColor (-1) — it must not count as any colour.
            var bomb = new Tile(1, Tile.NoColor, TileKind.ColorBomb);
            var step = new CascadeStep(0,
                new List<ClearedTile> { new ClearedTile(bomb, new GridPosition(0, 0)) },
                new List<TileFall>(), new List<TileSpawn>(), 10, new List<int>());

            tracker.Consume(step);

            Assert.That(tracker.Progress(0), Is.Zero);
        }

        [Test]
        public void EmptyObjectiveList_Throws()
        {
            Assert.Throws<ArgumentException>(() => new ObjectiveTracker(Array.Empty<Objective>()));
        }
    }

    public sealed class StarCalculatorTests
    {
        private static readonly int[] Thresholds = { 100, 200, 300 };

        [Test]
        public void BelowFirstThreshold_IsZeroStars()
        {
            Assert.That(StarCalculator.Calculate(99, Thresholds), Is.Zero);
        }

        [Test]
        public void ThresholdBoundaries_AwardTheirStar()
        {
            Assert.That(StarCalculator.Calculate(100, Thresholds), Is.EqualTo(1));
            Assert.That(StarCalculator.Calculate(199, Thresholds), Is.EqualTo(1));
            Assert.That(StarCalculator.Calculate(200, Thresholds), Is.EqualTo(2));
            Assert.That(StarCalculator.Calculate(300, Thresholds), Is.EqualTo(3));
        }

        [Test]
        public void StarsCapAtThree()
        {
            Assert.That(StarCalculator.Calculate(9999, new[] { 1, 2, 3, 4, 5 }), Is.EqualTo(3));
        }
    }
}
