using Match3.Core;
using NUnit.Framework;
using static Match3.Tests.TestColors;

namespace Match3.Tests
{
    /// <summary>
    /// The in-level booster kit: inventory (starter pack, spend, serialize), the
    /// streak ladder's booster days, and the hammer's single-cell smash flowing
    /// through the normal wave machinery.
    /// </summary>
    public sealed class BoosterTests
    {
        private static Board LatinSquare(TileFactory factory) => Board.FromLayout(new[,]
        {
            { A, B, C },
            { B, C, A },
            { C, A, B },
        }, factory);

        // ---- Inventory -----------------------------------------------------------

        [Test]
        public void FreshState_ShipsWithTheStarterPack()
        {
            var state = new MetaState();
            Assert.AreEqual(3, state.BoosterCount(BoosterKind.Hammer));
            Assert.AreEqual(3, state.BoosterCount(BoosterKind.FreeSwap));
            Assert.AreEqual(3, state.BoosterCount(BoosterKind.Shuffle));
        }

        [Test]
        public void Spend_StopsAtAnEmptyShelf()
        {
            var state = new MetaState();
            Assert.IsTrue(state.TrySpendBooster(BoosterKind.Hammer));
            Assert.IsTrue(state.TrySpendBooster(BoosterKind.Hammer));
            Assert.IsTrue(state.TrySpendBooster(BoosterKind.Hammer));
            Assert.IsFalse(state.TrySpendBooster(BoosterKind.Hammer));
            Assert.AreEqual(0, state.BoosterCount(BoosterKind.Hammer));
            Assert.AreEqual(3, state.BoosterCount(BoosterKind.FreeSwap), "other shelves untouched");
        }

        [Test]
        public void Serializer_RoundtripsTheInventory()
        {
            var state = new MetaState { Hammers = 5, FreeSwaps = 0, Shuffles = 2 };
            MetaState restored = MetaSerializer.Deserialize(MetaSerializer.Serialize(state));
            Assert.AreEqual(5, restored.Hammers);
            Assert.AreEqual(0, restored.FreeSwaps);
            Assert.AreEqual(2, restored.Shuffles);
        }

        [Test]
        public void CorruptFile_FallsBackToTheStarterPack()
        {
            MetaState restored = MetaSerializer.Deserialize("hammers=notanumber\nstreak=4\n");
            Assert.AreEqual(3, restored.Hammers);
            Assert.AreEqual(0, restored.Streak, "corrupt input resets the whole state");
        }

        [Test]
        public void LegacyFileWithoutBoosterKeys_GainsTheStarterPack()
        {
            MetaState restored = MetaSerializer.Deserialize("lastClaimDay=5\nstreak=2\npendingKind=0\npendingAmount=0\n");
            Assert.AreEqual(2, restored.Streak);
            Assert.AreEqual(3, restored.Hammers);
            Assert.AreEqual(3, restored.FreeSwaps);
            Assert.AreEqual(3, restored.Shuffles);
        }

        [Test]
        public void StreakLadder_EvenDaysStockTheShelf()
        {
            StreakReward day2 = DailyStreak.RewardFor(2);
            Assert.AreEqual(StreakRewardKind.BoosterHammer, day2.Kind);
            Assert.AreEqual(2, day2.Amount);

            Assert.AreEqual(StreakRewardKind.BoosterFreeSwap, DailyStreak.RewardFor(4).Kind);
            Assert.AreEqual(StreakRewardKind.BoosterShuffle, DailyStreak.RewardFor(6).Kind);
        }

        // ---- Hammer ---------------------------------------------------------------

        [Test]
        public void Hammer_SmashesExactlyOneCell()
        {
            TileFactory factory = TestFactories.Scripted(5, D);
            Board board = LatinSquare(factory);
            Tile victim = board[new GridPosition(1, 1)].Value;

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom(new int[0]));
            ResolutionResult result = resolver.ResolveHammer(board, new GridPosition(1, 1));

            Assert.AreEqual(1, result.Steps.Count);
            CascadeStep step = result.Steps[0];
            Assert.AreEqual(1, step.Cleared.Count);
            Assert.AreEqual(victim.Id, step.Cleared[0].Tile.Id);
            Assert.AreEqual(10, step.Points, "one tile, no finale bonus outside the finale");
            Assert.AreEqual(1, step.Falls.Count, "the tile above drops into the hole");
        }

        [Test]
        public void Hammer_SetsOffASpecial()
        {
            TileFactory factory = TestFactories.Scripted(5, B, C, A);
            Board board = LatinSquare(factory);
            board.SetTile(new GridPosition(1, 1), factory.CreateSpecial(C, TileKind.StripedH));

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom(new int[0]));
            ResolutionResult result = resolver.ResolveHammer(board, new GridPosition(1, 1));

            Assert.AreEqual(1, result.Steps.Count);
            Assert.AreEqual(1, result.Steps[0].Detonations.Count);
            Assert.AreEqual(DetonationKind.Row, result.Steps[0].Detonations[0].Kind);
            Assert.AreEqual(30, result.Steps[0].Points, "3 cleared * 10; the 500 bonus is finale-only");
        }

        [Test]
        public void Hammer_PopsALock_TheCandySurvives()
        {
            TileFactory factory = TestFactories.Scripted(5);
            Board board = LatinSquare(factory);
            Tile caged = board[new GridPosition(1, 1)].Value;

            LockGrid locks = LockGrid.FromCells(3, 3, new[] { new GridPosition(1, 1) });
            board.AttachLocks(locks);
            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom(new int[0]));
            resolver.AttachLocks(locks);

            ResolutionResult result = resolver.ResolveHammer(board, new GridPosition(1, 1));

            Assert.AreEqual(1, result.Steps.Count);
            Assert.AreEqual(1, result.Steps[0].LockBreaks.Count);
            Assert.AreEqual(0, result.Steps[0].Cleared.Count);
            Assert.AreEqual(caged.Id, board[new GridPosition(1, 1)].Value.Id);
            Assert.AreEqual(0, locks.TotalRemaining);
        }

        [Test]
        public void Hammer_BouncesOffAnIngredient()
        {
            TileFactory factory = TestFactories.Scripted(5);
            Board board = LatinSquare(factory);
            board.SetTile(new GridPosition(1, 1), factory.CreateIngredient());

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom(new int[0]));
            ResolutionResult result = resolver.ResolveHammer(board, new GridPosition(1, 1));

            Assert.AreEqual(0, result.Steps.Count, "indestructible target — the caller refunds the booster");
            Assert.AreEqual(TileKind.Ingredient, board[new GridPosition(1, 1)].Value.Kind);
        }

        [Test]
        public void Hammer_DamagesJellyUnderTheCell()
        {
            TileFactory factory = TestFactories.Scripted(5, D);
            Board board = LatinSquare(factory);
            JellyGrid jelly = JellyGrid.BottomRows(3, 3, 1, 1);

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom(new int[0]));
            resolver.AttachJelly(jelly);

            ResolutionResult result = resolver.ResolveHammer(board, new GridPosition(1, 0));

            Assert.AreEqual(1, result.Steps.Count);
            Assert.AreEqual(1, result.Steps[0].JellyHits.Count);
            Assert.AreEqual(new GridPosition(1, 0), result.Steps[0].JellyHits[0].Position);
            Assert.AreEqual(0, result.Steps[0].JellyHits[0].RemainingLayers);
        }

        [Test]
        public void Hammer_CrumblesChocolate()
        {
            TileFactory factory = TestFactories.Scripted(5, D);
            Board board = LatinSquare(factory);
            board.SetTile(new GridPosition(1, 1), factory.CreateChocolate());

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom(new int[0]));
            ResolutionResult result = resolver.ResolveHammer(board, new GridPosition(1, 1));

            Assert.AreEqual(1, result.Steps.Count);
            Assert.AreEqual(1, result.Steps[0].Cleared.Count);
            Assert.AreEqual(TileKind.Chocolate, result.Steps[0].Cleared[0].Tile.Kind);
            foreach (CascadeStep step in result.Steps)
                Assert.AreEqual(0, step.ChocolateSpreads.Count, "a booster is not a move — no spread");
        }

        [Test]
        public void Hammer_OutsideTheBoard_DoesNothing()
        {
            TileFactory factory = TestFactories.Scripted(5);
            Board board = LatinSquare(factory);

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom(new int[0]));
            ResolutionResult result = resolver.ResolveHammer(board, new GridPosition(9, 9));

            Assert.AreEqual(0, result.Steps.Count);
        }
    }
}
