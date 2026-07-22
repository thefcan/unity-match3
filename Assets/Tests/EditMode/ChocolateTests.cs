using System.Linq;
using Match3.Core;
using NUnit.Framework;
using static Match3.Tests.TestColors;

namespace Match3.Tests
{
    /// <summary>
    /// Chocolate: destroyed by adjacent clears, immobile under gravity, and creeping
    /// onto a neighbouring candy when a whole move ignores it.
    /// </summary>
    public sealed class ChocolateTests
    {
        [Test]
        public void AdjacentClearDestroysChocolate_AndCountsTheObjective()
        {
            TileFactory factory = TestFactories.Scripted(5, E, D, E, A);
            Board board = Board.FromLayout(new[,]
            {
                { B, C, D },
                { C, D, B },
                { A, A, A },
            }, factory);
            board.SetTile(new GridPosition(1, 1), factory.CreateChocolate());

            var resolver = new CascadeResolver(new ScoreConfig(10, 1));
            ResolutionResult result = resolver.Resolve(board);

            CascadeStep step = result.Steps[0];
            Assert.That(step.Cleared.Count(c => c.Tile.Kind == TileKind.Chocolate), Is.EqualTo(1),
                        "the chocolate above the match crumbled");

            var tracker = new ObjectiveTracker(new[] { new Objective(ObjectiveType.ClearChocolate, 0, 1) });
            foreach (CascadeStep s in result.Steps)
                tracker.Consume(s);
            Assert.That(tracker.AllComplete, Is.True);

            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                    Assert.That(board[new GridPosition(x, y)].Value.Kind, Is.Not.EqualTo(TileKind.Chocolate));
        }

        [Test]
        public void IgnoredChocolateSpreads_AfterARealMove()
        {
            TileFactory factory = TestFactories.Scripted(5, A, B, B, A, A, B);
            Board board = Board.FromLayout(new[,]
            {
                { D, E, D, C },
                { E, D, E, B },
                { B, A, B, E },
                { A, B, A, C },
            }, factory);
            board.SetTile(new GridPosition(3, 3), factory.CreateChocolate());

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom(1));
            var from = new GridPosition(1, 0);
            var to = new GridPosition(1, 1);
            board.Swap(from, to);
            ResolutionResult result = resolver.ResolveSwap(board, from, to);

            Assert.That(result.Steps.Count, Is.EqualTo(2), "one clear wave + the spread step");
            CascadeStep spreadStep = result.Steps[1];
            Assert.That(spreadStep.ChocolateSpreads.Count, Is.EqualTo(1));
            Assert.That(spreadStep.Cleared, Is.Empty);

            ChocolateSpread spread = spreadStep.ChocolateSpreads[0];
            Assert.That(spread.From, Is.EqualTo(new GridPosition(3, 3)));
            Assert.That(spread.To, Is.EqualTo(new GridPosition(3, 2)));
            Assert.That(spread.Consumed.Kind, Is.EqualTo(TileKind.Normal));
            Assert.That(board[spread.To].Value.Kind, Is.EqualTo(TileKind.Chocolate));
        }

        [Test]
        public void NoSpreadWithoutSwapContext_AndChocolateBlocksGravity()
        {
            TileFactory factory = TestFactories.Scripted(5, E, E, D);
            Board board = Board.FromLayout(new[,]
            {
                { B, C, D },
                { C, D, B },
                { A, A, A },
            }, factory);
            board.SetTile(new GridPosition(2, 2), factory.CreateChocolate());

            // Full mode with a random that must never be consumed: a cascade-only
            // resolve (shuffle settling) is not a player move, so no spread.
            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom());
            ResolutionResult result = resolver.Resolve(board);

            Assert.That(result.Steps.All(s => s.ChocolateSpreads.Count == 0), Is.True);
            Assert.That(board[new GridPosition(2, 2)].Value.Kind, Is.EqualTo(TileKind.Chocolate),
                        "far from the match — survives in place");

            CascadeStep step = result.Steps[0];
            Assert.That(step.Falls.All(f => f.From != new GridPosition(2, 2)), Is.True,
                        "chocolate never falls");
            Assert.That(step.Spawns.Any(s => s.Position == new GridPosition(2, 1)), Is.True,
                        "the hole under the chocolate refills");
        }

        [Test]
        public void SpreadIsDeterministic_AndTargetsOnlyNormalCandies()
        {
            TileFactory factory = TestFactories.Scripted(5, B, C, A, B, B, A);
            Board board = Board.FromLayout(new[,]
            {
                { C, D, D, C },
                { E, D, E, B },
                { B, A, B, E },
                { A, B, A, C },
            }, factory);
            board.SetTile(new GridPosition(0, 3), factory.CreateChocolate());
            board.SetTile(new GridPosition(1, 3), factory.CreateSpecial(D, TileKind.Wrapped));

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom(1));
            var from = new GridPosition(1, 0);
            var to = new GridPosition(1, 1);
            board.Swap(from, to);
            ResolutionResult result = resolver.ResolveSwap(board, from, to);

            CascadeStep last = result.Steps[result.Steps.Count - 1];
            Assert.That(last.ChocolateSpreads.Count, Is.EqualTo(1));
            ChocolateSpread spread = last.ChocolateSpreads[0];
            Assert.That(spread.To, Is.EqualTo(new GridPosition(0, 2)), "scripted pick lands on a NORMAL candy");
            Assert.That(spread.Consumed.Kind, Is.EqualTo(TileKind.Normal));
        }

        [Test]
        public void AnyDestroyedChocolateSuppressesTheSpread()
        {
            TileFactory factory = TestFactories.Scripted(5, A, B, B, A, A, B, C);
            Board board = Board.FromLayout(new[,]
            {
                { D, E, D, C },
                { E, D, E, B },
                { B, A, B, E },
                { A, B, A, C },
            }, factory);
            board.SetTile(new GridPosition(3, 0), factory.CreateChocolate()); // adjacent to the match
            board.SetTile(new GridPosition(3, 3), factory.CreateChocolate()); // far away

            // Empty SequenceRandom: if the resolver wrongly tries to spread, the
            // scripted random throws and the test fails loudly.
            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom());
            var from = new GridPosition(1, 0);
            var to = new GridPosition(1, 1);
            board.Swap(from, to);
            ResolutionResult result = resolver.ResolveSwap(board, from, to);

            Assert.That(result.Steps.SelectMany(s => s.Cleared).Count(c => c.Tile.Kind == TileKind.Chocolate),
                        Is.EqualTo(1), "the chocolate next to the match crumbled");
            Assert.That(result.Steps.All(s => s.ChocolateSpreads.Count == 0), Is.True,
                        "a move that destroyed chocolate never spreads");
            Assert.That(board[new GridPosition(3, 3)].Value.Kind, Is.EqualTo(TileKind.Chocolate));
        }

        [Test]
        public void ChocolateIsImmobileAndUnswappable()
        {
            TileFactory factory = TestFactories.Seeded();
            Board board = Board.FromLayout(new[,]
            {
                { A, B, C },
                { B, C, A },
                { C, A, B },
            }, factory);
            board.SetTile(new GridPosition(1, 1), factory.CreateChocolate());

            Assert.That(board.IsImmobile(new GridPosition(1, 1)), Is.True);
            Assert.That(board[new GridPosition(1, 1)].Value.IsSpecial, Is.False,
                        "chocolate is a blocker, not a detonating special");
        }
    }
}
