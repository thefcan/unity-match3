using System.Linq;
using Match3.Core;
using NUnit.Framework;
using static Match3.Tests.TestColors;

namespace Match3.Tests
{
    /// <summary>Pure geometry of blast areas — no resolver involved.</summary>
    public sealed class DetonationRulesTests
    {
        [Test]
        public void RowArea_SkipsEmptyCells()
        {
            var board = Board.FromLayout(new[,]
            {
                { A, B, C },
                { B, C, A },
                { A, _, B },
            }, TestFactories.Seeded());

            var area = DetonationRules.RowArea(board, 0);

            Assert.That(area, Is.EquivalentTo(new[] { new GridPosition(0, 0), new GridPosition(2, 0) }));
        }

        [Test]
        public void BlastArea_IsClampedAtTheCorner()
        {
            var board = Board.FromLayout(new[,]
            {
                { A, B, C },
                { B, C, A },
                { C, A, B },
            }, TestFactories.Seeded());

            var area = DetonationRules.BlastArea(board, new GridPosition(0, 0), radius: 1);

            Assert.That(area, Has.Count.EqualTo(4), "a corner 3x3 clamps to 2x2");
        }

        [Test]
        public void CrossArea_CountsTheSharedCellOnce()
        {
            var board = Board.FromLayout(new[,]
            {
                { A, B, C, D },
                { B, C, D, A },
                { C, D, A, B },
            }, TestFactories.Seeded());

            var area = DetonationRules.CrossArea(board, new GridPosition(1, 1));

            Assert.That(area, Has.Count.EqualTo(4 + 3 - 1));
            Assert.That(area.Distinct().Count(), Is.EqualTo(area.Count));
        }

        [Test]
        public void MostCommonColor_TieBreaksToTheLowestIndex()
        {
            // B and C appear 3 times each; A twice; D once. The tie goes to B (< C).
            var board = Board.FromLayout(new[,]
            {
                { B, C, A },
                { C, B, D },
                { A, B, C },
            }, TestFactories.Seeded());

            Assert.That(DetonationRules.MostCommonColor(board), Is.EqualTo(B));
        }
    }

    /// <summary>
    /// Specials detonating inside the resolver: lanes, the wrapped double blast at its
    /// post-gravity cell, blast-triggered colour bombs, and morphs staying uncleared.
    /// </summary>
    public sealed class SpecialDetonationTests
    {
        private static CascadeResolver FullResolver(TileFactory factory, IRandom random = null) =>
            new CascadeResolver(new ScoreConfig(pointsPerTile: 10, cascadeMultiplierStep: 1),
                                factory, random ?? new SystemRandom(0));

        [Test]
        public void StripedInAMatch_ClearsItsWholeColumn()
        {
            var factory = TestFactories.Scripted(5, draws: new[] { C, E, A, B, C });
            var board = Board.FromLayout(new[,]
            {
                { B, C, D },
                { C, D, B },
                { A, A, A },
            }, factory);
            board.SetTile(new GridPosition(0, 0), factory.CreateSpecial(A, TileKind.StripedV));

            var result = FullResolver(factory).Resolve(board);

            CascadeStep wave0 = result.Steps[0];
            Assert.That(wave0.Detonations, Has.Count.EqualTo(1));
            Assert.That(wave0.Detonations[0].Kind, Is.EqualTo(DetonationKind.Column));
            Assert.That(wave0.Cleared.Select(c => c.Position), Is.EquivalentTo(new[]
            {
                new GridPosition(0, 0), new GridPosition(1, 0), new GridPosition(2, 0), // the match
                new GridPosition(0, 1), new GridPosition(0, 2),                          // the column blast
            }));
        }

        [Test]
        public void WrappedInAMatch_BlastsSurvivesAndBlastsAgainAfterGravity()
        {
            var factory = TestFactories.Seeded();
            var board = Board.FromLayout(new[,]
            {
                { B, C, D, B },
                { C, D, B, C },
                { A, A, A, D },
                { D, B, C, B },
            }, factory);
            Tile wrapped = factory.CreateSpecial(A, TileKind.Wrapped);
            board.SetTile(new GridPosition(1, 1), wrapped);

            var result = FullResolver(factory).Resolve(board);

            Assert.That(result.Steps.Count, Is.GreaterThanOrEqualTo(2));

            CascadeStep wave0 = result.Steps[0];
            Assert.That(wave0.Detonations.Any(d => d.Source.Id == wrapped.Id && d.Kind == DetonationKind.Blast3x3));
            Assert.That(wave0.Cleared.Select(c => c.Tile.Id), Has.No.Member(wrapped.Id),
                "the wrapped survives its first blast");
            Assert.That(wave0.Cleared, Has.Count.EqualTo(8), "3x3 around (1,1) minus the wrapped itself");

            // Everything under it cleared, so the wrapped fell to the floor before
            // its second, self-consuming blast.
            CascadeStep wave1 = result.Steps[1];
            Detonation second = wave1.Detonations.Single(d => d.Source.Id == wrapped.Id);
            Assert.That(second.Origin, Is.EqualTo(new GridPosition(1, 0)), "second blast fires at the post-gravity cell");
            Assert.That(wave1.Cleared.Select(c => c.Tile.Id), Does.Contain(wrapped.Id),
                "the second blast consumes the wrapped");
        }

        [Test]
        public void BlastTriggeredColorBomb_ClearsTheMostCommonColor()
        {
            var factory = TestFactories.Seeded();
            var board = Board.FromLayout(new[,]
            {
                { B, C, D, C },
                { C, D, B, D },
                { D, B, C, B },
                { A, A, A, B },
            }, factory);
            board.SetTile(new GridPosition(0, 0), factory.CreateSpecial(A, TileKind.StripedH));
            Tile bomb = factory.CreateColorBomb();
            board.SetTile(new GridPosition(3, 0), bomb);

            var result = FullResolver(factory).Resolve(board);

            // B, C and D are tied at 4 tiles each -> the tie breaks to B.
            CascadeStep wave0 = result.Steps[0];
            Detonation colorClear = wave0.Detonations.Single(d => d.Kind == DetonationKind.ColorClear);
            Assert.That(colorClear.Source.Id, Is.EqualTo(bomb.Id));
            Assert.That(colorClear.Area, Is.EquivalentTo(new[]
            {
                new GridPosition(0, 3), new GridPosition(2, 2), new GridPosition(1, 1), new GridPosition(3, 1),
            }));
            Assert.That(wave0.Cleared.Select(c => c.Tile.Id), Does.Contain(bomb.Id), "the bomb is consumed");
        }

        [Test]
        public void CreatedSpecial_IsNotCleared_AndLandsOnTheBoard()
        {
            var factory = TestFactories.Scripted(5, draws: new[] { E, C, E });
            var board = Board.FromLayout(new[,]
            {
                { B, C, D, B },
                { C, D, B, C },
                { D, B, C, D },
                { A, A, A, A },
            }, factory);

            var result = FullResolver(factory).Resolve(board);

            Assert.That(result.Steps.Count, Is.EqualTo(1));
            CascadeStep step = result.Steps[0];

            Assert.That(step.Creations, Has.Count.EqualTo(1));
            SpecialCreation creation = step.Creations[0];
            Assert.That(creation.Position, Is.EqualTo(new GridPosition(2, 0)));
            Assert.That(step.Cleared.Select(c => c.Position), Has.No.Member(creation.Position),
                "the creation cell morphs — it does not pop");
            Assert.That(step.Cleared, Has.Count.EqualTo(3));
            Assert.That(step.RunLengths, Is.EquivalentTo(new[] { 4 }));

            Tile landed = board[new GridPosition(2, 0)].Value;
            Assert.That(landed.Id, Is.EqualTo(creation.Created.Id), "the special stays where it was created");
            Assert.That(landed.Kind, Is.EqualTo(TileKind.StripedV));
        }

        [Test]
        public void ClassicResolver_NeverCreatesSpecials()
        {
            var board = Board.FromLayout(new[,]
            {
                { B, C, D, B },
                { C, D, B, C },
                { D, B, C, D },
                { A, A, A, A },
            }, TestFactories.Scripted(5, draws: new[] { E, C, E, B }));

            var result = new CascadeResolver(new ScoreConfig(10, 1)).Resolve(board);

            Assert.That(result.Steps[0].Creations, Is.Empty);
            Assert.That(result.Steps[0].Cleared, Has.Count.EqualTo(4), "all four matched tiles clear");
        }
    }
}
