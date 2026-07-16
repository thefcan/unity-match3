using System.Collections.Generic;
using System.Linq;
using Match3.Core;
using NUnit.Framework;
using static Match3.Tests.TestColors;

namespace Match3.Tests
{
    /// <summary>The jelly cell-state grid: placement, damage, and bookkeeping.</summary>
    public sealed class JellyGridTests
    {
        [Test]
        public void BottomRows_CoversTheGravityFloor()
        {
            var jelly = JellyGrid.BottomRows(4, 5, rows: 2, layers: 1);

            Assert.That(jelly.LayersAt(new GridPosition(0, 0)), Is.EqualTo(1));
            Assert.That(jelly.LayersAt(new GridPosition(3, 1)), Is.EqualTo(1));
            Assert.That(jelly.LayersAt(new GridPosition(0, 2)), Is.Zero, "row 2 is above the jelly");
            Assert.That(jelly.TotalRemaining, Is.EqualTo(8));
        }

        [Test]
        public void Damage_PeelsOneLayerAtATime_AndFloorsAtZero()
        {
            var jelly = new JellyGrid(3, 3);
            var pos = new GridPosition(1, 1);
            jelly.Set(pos, 2);

            Assert.That(jelly.Damage(pos), Is.True);
            Assert.That(jelly.LayersAt(pos), Is.EqualTo(1));
            Assert.That(jelly.Damage(pos), Is.True);
            Assert.That(jelly.LayersAt(pos), Is.Zero);
            Assert.That(jelly.Damage(pos), Is.False, "no layer left to remove");
            Assert.That(jelly.IsClear, Is.True);
        }

        [Test]
        public void Damage_OutsideJelly_DoesNothing()
        {
            var jelly = new JellyGrid(3, 3);

            Assert.That(jelly.Damage(new GridPosition(0, 0)), Is.False);
            Assert.That(jelly.Damage(new GridPosition(9, 9)), Is.False, "out of bounds is a no-op");
        }
    }

    /// <summary>The resolver's jelly integration: cleared cells damage jelly, recorded per wave.</summary>
    public sealed class JellyResolveTests
    {
        [Test]
        public void ClearedCells_DamageJelly_AndRecordHits()
        {
            // Bottom row holds a ready-made A-A-A match; jelly covers the bottom row.
            var board = Board.FromLayout(new[,]
            {
                { B, C, B },
                { C, B, C },
                { A, A, A },
            }, TestFactories.Seeded());
            var jelly = JellyGrid.BottomRows(3, 3, rows: 1, layers: 1);

            var resolver = new CascadeResolver(new ScoreConfig(10, 1));
            resolver.AttachJelly(jelly);
            ResolutionResult result = resolver.Resolve(board);

            CascadeStep first = result.Steps[0];
            Assert.That(first.JellyHits, Has.Count.EqualTo(3), "each cleared floor cell takes a hit");
            Assert.That(first.JellyHits.All(hit => hit.RemainingLayers == 0), Is.True);
            Assert.That(jelly.IsClear, Is.True);
        }

        [Test]
        public void DoubleLayerJelly_NeedsTwoClears()
        {
            var board = Board.FromLayout(new[,]
            {
                { B, C, B },
                { C, B, C },
                { A, A, A },
            }, TestFactories.Seeded());
            var jelly = JellyGrid.BottomRows(3, 3, rows: 1, layers: 2);

            var resolver = new CascadeResolver(new ScoreConfig(10, 1));
            resolver.AttachJelly(jelly);
            resolver.Resolve(board);

            Assert.That(jelly.TotalRemaining, Is.EqualTo(3), "one layer of the double jelly survives the first clear");
            Assert.That(jelly.IsClear, Is.False);
        }

        [Test]
        public void SpecialCreationCell_DamagesJellyToo()
        {
            // A horizontal 4-run: the creation cell morphs instead of clearing, but it
            // was matched — its jelly must still take the hit.
            var board = Board.FromLayout(new[,]
            {
                { B, C, B, C },
                { C, B, C, B },
                { A, A, A, A },
            }, TestFactories.Seeded());
            var jelly = JellyGrid.BottomRows(4, 3, rows: 1, layers: 1);

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), TestFactories.Seeded(), new SequenceRandom(0, 0, 0, 0));
            resolver.AttachJelly(jelly);
            ResolutionResult result = resolver.Resolve(board);

            Assert.That(result.Steps[0].Creations, Has.Count.EqualTo(1), "the 4-run mints a striped candy");
            Assert.That(jelly.IsClear, Is.True, "all four matched cells damaged their jelly, morph cell included");
            Assert.That(result.Steps[0].JellyHits, Has.Count.EqualTo(4));
        }

        [Test]
        public void CellsWithoutJelly_ProduceNoHits()
        {
            var board = Board.FromLayout(new[,]
            {
                { A, A, A },
                { C, B, C },
                { B, C, B },
            }, TestFactories.Seeded());
            var jelly = JellyGrid.BottomRows(3, 3, rows: 1, layers: 1); // match is on the TOP row

            var resolver = new CascadeResolver(new ScoreConfig(10, 1));
            resolver.AttachJelly(jelly);
            ResolutionResult result = resolver.Resolve(board);

            Assert.That(result.Steps[0].JellyHits, Is.Empty);
            Assert.That(jelly.TotalRemaining, Is.EqualTo(3));
        }

        [Test]
        public void NoJellyAttached_StepsCarryEmptyHitLists()
        {
            var board = Board.FromLayout(new[,]
            {
                { A, A, A },
                { C, B, C },
                { B, C, B },
            }, TestFactories.Seeded());

            ResolutionResult result = new CascadeResolver(new ScoreConfig(10, 1)).Resolve(board);

            Assert.That(result.Steps[0].JellyHits, Is.Empty);
        }
    }

    public sealed class JellyObjectiveTests
    {
        private static CascadeStep StepWithJellyHits(int hits)
        {
            var jellyHits = new List<JellyHit>();
            for (int i = 0; i < hits; i++)
                jellyHits.Add(new JellyHit(new GridPosition(i, 0), 0));

            return new CascadeStep(0, new List<ClearedTile>(), new List<TileFall>(), new List<TileSpawn>(),
                                   0, new List<int>(), new List<SpecialCreation>(), new List<Detonation>(), jellyHits);
        }

        [Test]
        public void ClearJellyObjective_CountsRemovedLayers_AcrossSteps()
        {
            var tracker = new ObjectiveTracker(new[] { new Objective(ObjectiveType.ClearJelly, 0, 5) });

            tracker.Consume(StepWithJellyHits(3));
            Assert.That(tracker.Progress(0), Is.EqualTo(3));
            Assert.That(tracker.AllComplete, Is.False);

            tracker.Consume(StepWithJellyHits(2));
            Assert.That(tracker.AllComplete, Is.True);
        }

        [Test]
        public void Campaign_IntroducesJelly_AtLevelEight()
        {
            Assert.That(LevelCurve.For(7).JellyRows, Is.Zero);

            for (int level = 8; level <= 20; level++)
            {
                LevelParameters parameters = LevelCurve.For(level);
                Assert.That(parameters.JellyRows, Is.GreaterThan(0), $"level {level} should have jelly");

                Objective jellyObjective = parameters.Objectives.Single(o => o.Type == ObjectiveType.ClearJelly);
                int totalLayers = parameters.JellyRows * parameters.Width * parameters.JellyLayers;
                Assert.That(jellyObjective.TargetAmount, Is.EqualTo(totalLayers),
                    "the objective must demand exactly the jelly on the board");
            }
        }
    }
}
