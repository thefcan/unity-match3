using System.Linq;
using Match3.Core;
using NUnit.Framework;
using static Match3.Tests.TestColors;

namespace Match3.Tests
{
    /// <summary>
    /// Ingredients: colourless, mobile, indestructible; they enter through top-row
    /// refills on a budget and exit when they reach the bottom row.
    /// </summary>
    public sealed class IngredientTests
    {
        [Test]
        public void IngredientFallsAndExitsAtTheBottom()
        {
            TileFactory factory = TestFactories.Scripted(5, E, D, E, A);
            Board board = Board.FromLayout(new[,]
            {
                { B, C, D },
                { C, D, B },
                { A, A, A },
            }, factory);
            board.SetTile(new GridPosition(1, 1), factory.CreateIngredient());
            Tile ingredient = board[new GridPosition(1, 1)].Value;

            var resolver = new CascadeResolver(new ScoreConfig(10, 1));
            ResolutionResult result = resolver.Resolve(board);

            Assert.That(result.Steps.Count, Is.EqualTo(2), "clear wave, then the exit wave");
            Assert.That(result.Steps[0].Falls.Any(f => f.Tile.Id == ingredient.Id), Is.True,
                        "the ingredient fell into the cleared row");

            CascadeStep exitStep = result.Steps[1];
            Assert.That(exitStep.IngredientExits.Count, Is.EqualTo(1));
            Assert.That(exitStep.IngredientExits[0].Tile.Id, Is.EqualTo(ingredient.Id));
            Assert.That(exitStep.IngredientExits[0].Position, Is.EqualTo(new GridPosition(1, 0)));

            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                    Assert.That(board[new GridPosition(x, y)].Value.Kind, Is.Not.EqualTo(TileKind.Ingredient));

            var tracker = new ObjectiveTracker(new[] { new Objective(ObjectiveType.CollectIngredients, 0, 1) });
            foreach (CascadeStep step in result.Steps)
                tracker.Consume(step);
            Assert.That(tracker.AllComplete, Is.True);
        }

        [Test]
        public void IngredientsAreIndestructible_EvenInsideABlast()
        {
            TileFactory factory = TestFactories.Scripted(5, B, C, D, E);
            Board board = Board.FromLayout(new[,]
            {
                { A, B, C },
                { A, D, C },
                { A, B, E },
            }, factory);
            board.SetTile(new GridPosition(0, 1), factory.CreateSpecial(A, TileKind.StripedH));
            board.SetTile(new GridPosition(2, 1), factory.CreateIngredient());
            Tile ingredient = board[new GridPosition(2, 1)].Value;

            var resolver = new CascadeResolver(new ScoreConfig(10, 1));
            ResolutionResult result = resolver.Resolve(board);

            CascadeStep step = result.Steps[0];
            Assert.That(step.Detonations.Any(d => d.Kind == DetonationKind.Row), Is.True);
            Assert.That(step.Cleared.Any(c => c.Tile.Id == ingredient.Id), Is.False,
                        "the row blast passed through the ingredient");
            Assert.That(board[new GridPosition(2, 1)].Value.Id, Is.EqualTo(ingredient.Id));
        }

        [Test]
        public void RefillInjectionSpawnsIngredients_OnePerWave()
        {
            TileFactory factory = TestFactories.Scripted(5, D, B, B);
            Board board = Board.FromLayout(new[,]
            {
                { C, A, D },
                { D, A, C },
                { B, A, D },
                { B, C, E },
            }, factory);

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom(0));
            resolver.AttachIngredients(2);
            ResolutionResult result = resolver.Resolve(board);

            Assert.That(result.Steps[0].Spawns.Count(s => s.Tile.Kind == TileKind.Ingredient), Is.EqualTo(1),
                        "exactly one ingredient per wave, even with budget for two");
            TileSpawn spawn = result.Steps[0].Spawns.First(s => s.Tile.Kind == TileKind.Ingredient);
            Assert.That(spawn.Position.Y, Is.EqualTo(board.Height - 1), "ingredients enter at the top row");

            int onBoard = 0;
            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                    if (board[new GridPosition(x, y)].Value.Kind == TileKind.Ingredient)
                        onBoard++;
            Assert.That(onBoard, Is.EqualTo(1));
        }

        [Test]
        public void BombPlusIngredientIsNotAnActivationSwap()
        {
            TileFactory factory = TestFactories.Seeded();
            Tile bomb = factory.CreateColorBomb();
            Tile ingredient = factory.CreateIngredient();

            Assert.That(SwapRules.Classify(bomb, ingredient), Is.EqualTo(SwapKind.None));
            Assert.That(SwapRules.IsActivationSwap(ingredient, bomb), Is.False);
        }
    }
}
