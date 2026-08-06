using System.Linq;
using Match3.Core;
using NUnit.Framework;
using static Match3.Tests.TestColors;

namespace Match3.Tests
{
    /// <summary>
    /// The mystery egg (chapter 6): colourless, mobile, and cracked open by any
    /// adjacent clear or direct blast — hatching into a rolled candy that lands
    /// dormant and joins play from the next wave. The hatch roll order is fixed
    /// (kind, colour, then stripe orientation) and cells are walked row-major,
    /// so scripted randoms pin every outcome.
    /// </summary>
    public sealed class EggTests
    {
        private static CascadeResolver FullResolver(TileFactory factory, params int[] resolverDraws) =>
            new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom(resolverDraws));

        // A stable 3x3 with no matches and no possible-move interference; the egg
        // replaces the centre tile in most tests.
        private static Board StableBoard(TileFactory factory) => Board.FromLayout(new[,]
        {
            { B, C, D },
            { C, E, B },
            { B, A, C },
        }, factory);

        [Test]
        public void AdjacentMatch_HatchesTheEgg_AndRecordsIt()
        {
            TileFactory factory = TestFactories.Scripted(5, E, D, E);
            Board board = Board.FromLayout(new[,]
            {
                { B, C, D },
                { C, E, B },
                { A, A, A },
            }, factory);
            board.SetTile(new GridPosition(1, 1), factory.CreateMysteryEgg());

            CascadeResolver resolver = FullResolver(factory, 0, D);
            ResolutionResult result = resolver.Resolve(board);

            Assert.That(result.Steps.Count, Is.EqualTo(1));
            CascadeStep step = result.Steps[0];
            Assert.That(step.EggHatches.Count, Is.EqualTo(1));

            EggHatch hatch = step.EggHatches[0];
            Assert.That(hatch.Position, Is.EqualTo(new GridPosition(1, 1)));
            Assert.That(hatch.Replaced.Kind, Is.EqualTo(TileKind.MysteryEgg));
            Assert.That(hatch.Hatched.Kind, Is.EqualTo(TileKind.Normal));
            Assert.That(hatch.Hatched.ColorIndex, Is.EqualTo(D));

            // The shell's cell never clears — the hatchling morphs in and falls.
            Assert.That(step.Cleared.Count, Is.EqualTo(3));
            Assert.That(step.Cleared.Any(c => c.Tile.Kind == TileKind.MysteryEgg), Is.False);
            Assert.That(board[new GridPosition(1, 0)].Value.Id, Is.EqualTo(hatch.Hatched.Id));
        }

        [TestCase(0, TileKind.Normal)]
        [TestCase(69, TileKind.Normal)]
        [TestCase(85, TileKind.Wrapped)]
        [TestCase(94, TileKind.Wrapped)]
        [TestCase(95, TileKind.Fish)]
        [TestCase(99, TileKind.Fish)]
        public void HatchWeights_PinTheKindBoundaries(int kindRoll, TileKind expected)
        {
            TileFactory factory = TestFactories.Scripted(5);
            Board board = StableBoard(factory);
            var eggCell = new GridPosition(1, 1);
            board.SetTile(eggCell, factory.CreateMysteryEgg());

            CascadeResolver resolver = FullResolver(factory, kindRoll, E);
            ResolutionResult result = resolver.ResolveHammer(board, eggCell);

            Assert.That(result.Steps[0].EggHatches[0].Hatched.Kind, Is.EqualTo(expected));
            Assert.That(board[eggCell].Value.Kind, Is.EqualTo(expected));
            Assert.That(board[eggCell].Value.ColorIndex, Is.EqualTo(E));
        }

        [TestCase(70, 0, TileKind.StripedV)]
        [TestCase(84, 1, TileKind.StripedH)]
        public void StripedHatch_RollsItsOrientation(int kindRoll, int orientationRoll, TileKind expected)
        {
            TileFactory factory = TestFactories.Scripted(5);
            Board board = StableBoard(factory);
            var eggCell = new GridPosition(1, 1);
            board.SetTile(eggCell, factory.CreateMysteryEgg());

            CascadeResolver resolver = FullResolver(factory, kindRoll, E, orientationRoll);
            ResolutionResult result = resolver.ResolveHammer(board, eggCell);

            Assert.That(result.Steps[0].EggHatches[0].Hatched.Kind, Is.EqualTo(expected));
        }

        [Test]
        public void Hammer_CracksTheEgg_AndTheWaveStillRecords()
        {
            TileFactory factory = TestFactories.Scripted(5);
            Board board = StableBoard(factory);
            var eggCell = new GridPosition(1, 1);
            board.SetTile(eggCell, factory.CreateMysteryEgg());

            CascadeResolver resolver = FullResolver(factory, 0, E);
            ResolutionResult result = resolver.ResolveHammer(board, eggCell);

            // Nothing cleared, nothing fell — the hatch alone carries the step
            // (a hammer on an egg must never look like a refundable dud).
            Assert.That(result.HadMatches, Is.True);
            CascadeStep step = result.Steps[0];
            Assert.That(step.EggHatches.Count, Is.EqualTo(1));
            Assert.That(step.Cleared, Is.Empty);
            Assert.That(step.Falls, Is.Empty);
            Assert.That(step.Spawns, Is.Empty);
        }

        [Test]
        public void StripedBeam_CracksTheEgg_InsteadOfBeingAbsorbed()
        {
            TileFactory factory = TestFactories.Scripted(5, D, C);
            Board board = StableBoard(factory);
            board.SetTile(new GridPosition(0, 0), factory.CreateSpecial(A, TileKind.StripedH));
            board.SetTile(new GridPosition(1, 0), factory.CreateMysteryEgg());

            CascadeResolver resolver = FullResolver(factory, 0, E);
            ResolutionResult result = resolver.ResolveHammer(board, new GridPosition(0, 0));

            CascadeStep step = result.Steps[0];
            Assert.That(step.Detonations[0].Kind, Is.EqualTo(DetonationKind.Row));
            Assert.That(step.EggHatches.Count, Is.EqualTo(1));
            Assert.That(step.EggHatches[0].Position, Is.EqualTo(new GridPosition(1, 0)));
            // The striped source and the third row cell cleared; the egg cell did not.
            Assert.That(step.Cleared.Count, Is.EqualTo(2));
        }

        [Test]
        public void HatchedNormal_JoinsPlay_NextWave()
        {
            TileFactory factory = TestFactories.Scripted(5, E, E, D, D, C, E);
            Board board = Board.FromLayout(new[,]
            {
                { D, C, E, B },
                { B, D, C, B },
                { A, A, A, E },
            }, factory);
            board.SetTile(new GridPosition(3, 0), factory.CreateMysteryEgg());

            CascadeResolver resolver = FullResolver(factory, 0, B);
            ResolutionResult result = resolver.Resolve(board);

            // Wave 0: the bottom run clears and cracks the egg into a B — which
            // completes the B column and clears as a NEW match in wave 1.
            Assert.That(result.Steps.Count, Is.EqualTo(2));
            Assert.That(result.Steps[0].EggHatches.Count, Is.EqualTo(1));
            Assert.That(result.Steps[1].RunLengths, Is.EqualTo(new[] { 3 }));
            Assert.That(result.Steps[1].Cleared.Any(c => c.Position == new GridPosition(3, 0)), Is.True);
        }

        [Test]
        public void TwoEggs_HatchInRowMajorOrder()
        {
            TileFactory factory = TestFactories.Scripted(5, D, E, D);
            Board board = Board.FromLayout(new[,]
            {
                { C, D, C },
                { E, B, E },
                { A, A, A },
            }, factory);
            board.SetTile(new GridPosition(0, 1), factory.CreateMysteryEgg());
            board.SetTile(new GridPosition(2, 1), factory.CreateMysteryEgg());

            CascadeResolver resolver = FullResolver(factory, 0, A, 0, B);
            ResolutionResult result = resolver.Resolve(board);

            CascadeStep step = result.Steps[0];
            Assert.That(step.EggHatches.Count, Is.EqualTo(2));
            Assert.That(step.EggHatches[0].Position, Is.EqualTo(new GridPosition(0, 1)));
            Assert.That(step.EggHatches[0].Hatched.ColorIndex, Is.EqualTo(A));
            Assert.That(step.EggHatches[1].Position, Is.EqualTo(new GridPosition(2, 1)));
            Assert.That(step.EggHatches[1].Hatched.ColorIndex, Is.EqualTo(B));
        }

        [Test]
        public void Egg_FallsWithGravity()
        {
            TileFactory factory = TestFactories.Scripted(5);
            Board board = Board.FromLayout(new[,]
            {
                { B, E, B },
                { C, _, D },
                { D, A, C },
            }, factory);
            Tile egg = factory.CreateMysteryEgg();
            board.SetTile(new GridPosition(1, 2), egg);

            var falls = board.ApplyGravity();

            Assert.That(board[new GridPosition(1, 1)].Value.Kind, Is.EqualTo(TileKind.MysteryEgg));
            Assert.That(falls.Any(f => f.Tile.Id == egg.Id), Is.True);
        }

        [Test]
        public void Shuffle_KeepsTheEggOnBoard()
        {
            TileFactory factory = TestFactories.Seeded(5, seed: 11);
            Board board = Board.FromLayout(new[,]
            {
                { B, C, D },
                { C, D, B },
                { A, A, B },
            }, factory);
            board.SetTile(new GridPosition(2, 2), factory.CreateMysteryEgg());

            board.Shuffle(new SystemRandom(3));

            int eggs = 0;
            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                    if (board[new GridPosition(x, y)].Value.Kind == TileKind.MysteryEgg)
                        eggs++;
            Assert.That(eggs, Is.EqualTo(1));
        }

        [Test]
        public void ColorlessEgg_NeverFormsARun()
        {
            TileFactory factory = TestFactories.Scripted(5);
            Board board = Board.FromLayout(new[,]
            {
                { C, D, C },
                { A, E, A },
                { B, C, D },
            }, factory);
            board.SetTile(new GridPosition(1, 1), factory.CreateMysteryEgg());

            Assert.That(board.FindMatchRuns(), Is.Empty);
        }

        [Test]
        public void EggSwap_IsLegalOnlyWhenThePartnerMatches()
        {
            TileFactory factory = TestFactories.Scripted(5);
            Board board = Board.FromLayout(new[,]
            {
                { D, C, B },
                { C, E, D },
                { A, C, B },
            }, factory);
            var eggCell = new GridPosition(1, 1);
            board.SetTile(eggCell, factory.CreateMysteryEgg());

            // Pulling the (0,1) C into the egg's cell completes the C column.
            Assert.That(board.WouldSwapMatch(new GridPosition(0, 1), eggCell), Is.True);
            // Pulling the C from above achieves nothing — the egg gives no colour.
            Assert.That(board.WouldSwapMatch(eggCell, new GridPosition(1, 2)), Is.False);
        }

        [Test]
        public void CagedEgg_SleepsThroughAdjacentClears()
        {
            TileFactory factory = TestFactories.Scripted(5, E, D, E);
            Board board = Board.FromLayout(new[,]
            {
                { B, C, D },
                { C, E, B },
                { A, A, A },
            }, factory);
            var eggCell = new GridPosition(1, 1);
            board.SetTile(eggCell, factory.CreateMysteryEgg());
            LockGrid locks = LockGrid.FromCells(3, 3, new[] { eggCell });
            board.AttachLocks(locks);

            CascadeResolver resolver = FullResolver(factory);
            resolver.AttachLocks(locks);
            ResolutionResult result = resolver.Resolve(board);

            CascadeStep step = result.Steps[0];
            Assert.That(step.EggHatches, Is.Empty);
            Assert.That(step.LockBreaks, Is.Empty);
            Assert.That(board[eggCell].Value.Kind, Is.EqualTo(TileKind.MysteryEgg));
        }

        [Test]
        public void JellyUnderTheEgg_TakesNoDamage()
        {
            TileFactory factory = TestFactories.Scripted(5, E, D, E);
            Board board = Board.FromLayout(new[,]
            {
                { B, C, D },
                { C, E, B },
                { A, A, A },
            }, factory);
            var eggCell = new GridPosition(1, 1);
            board.SetTile(eggCell, factory.CreateMysteryEgg());
            var jelly = new JellyGrid(3, 3);
            jelly.Set(eggCell, 2);

            CascadeResolver resolver = FullResolver(factory, 0, D);
            resolver.AttachJelly(jelly);
            ResolutionResult result = resolver.Resolve(board);

            // The hatch cell never clears, so the jelly beneath it survives intact.
            Assert.That(result.Steps[0].EggHatches.Count, Is.EqualTo(1));
            Assert.That(result.Steps[0].JellyHits, Is.Empty);
            Assert.That(jelly.LayersAt(eggCell), Is.EqualTo(2));
        }

        [Test]
        public void Fish_PrefersTheEgg_OverPlainCandy()
        {
            TileFactory factory = TestFactories.Scripted(5, D);
            Board board = StableBoard(factory);
            var fishCell = new GridPosition(0, 0);
            var eggCell = new GridPosition(2, 2);
            board.SetTile(fishCell, factory.CreateSpecial(A, TileKind.Fish));
            board.SetTile(eggCell, factory.CreateMysteryEgg());

            CascadeResolver resolver = FullResolver(factory, 0, 0, E);
            ResolutionResult result = resolver.ResolveHammer(board, fishCell);

            CascadeStep step = result.Steps[0];
            Assert.That(step.FishStrikes.Count, Is.EqualTo(1));
            Assert.That(step.FishStrikes[0].To, Is.EqualTo(eggCell));
            Assert.That(step.EggHatches.Count, Is.EqualTo(1));
            Assert.That(step.Cleared.Count, Is.EqualTo(1));
        }

        [Test]
        public void ChocolateSpread_NeverEatsAnEgg()
        {
            TileFactory factory = TestFactories.Scripted(5, A, B, B, D, C, E, C, D, A, E, A, C);
            Board board = Board.FromLayout(new[,]
            {
                { D, E, D, C, A },
                { E, D, E, B, A },
                { B, A, B, E, B },
                { A, B, A, C, D },
            }, factory);
            // Chocolate in the far corner, walled in by eggs — and the whole right
            // side sits outside the clearing columns, so nothing falls beside it.
            board.SetTile(new GridPosition(4, 3), factory.CreateChocolate());
            board.SetTile(new GridPosition(3, 3), factory.CreateMysteryEgg());
            board.SetTile(new GridPosition(4, 2), factory.CreateMysteryEgg());

            CascadeResolver resolver = FullResolver(factory);
            var from = new GridPosition(1, 0);
            var to = new GridPosition(1, 1);
            board.Swap(from, to);
            ResolutionResult result = resolver.ResolveSwap(board, from, to);

            // Both of the chocolate's neighbours are eggs — nothing edible, no creep
            // (and the resolver's random is never even consulted).
            Assert.That(result.HadMatches, Is.True);
            Assert.That(result.Steps.SelectMany(s => s.ChocolateSpreads), Is.Empty);
            Assert.That(board[new GridPosition(3, 3)].Value.Kind, Is.EqualTo(TileKind.MysteryEgg));
            Assert.That(board[new GridPosition(4, 2)].Value.Kind, Is.EqualTo(TileKind.MysteryEgg));
        }

        [Test]
        public void SameSeeds_HatchTheSameCandies()
        {
            EggHatch RunOnce()
            {
                TileFactory factory = TestFactories.Seeded(5, seed: 7);
                Board board = Board.FromLayout(new[,]
                {
                    { B, C, D },
                    { C, E, B },
                    { A, A, A },
                }, factory);
                board.SetTile(new GridPosition(1, 1), factory.CreateMysteryEgg());
                var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SystemRandom(42));
                return resolver.Resolve(board).Steps[0].EggHatches[0];
            }

            EggHatch first = RunOnce();
            EggHatch second = RunOnce();

            Assert.That(second.Hatched.Kind, Is.EqualTo(first.Hatched.Kind));
            Assert.That(second.Hatched.ColorIndex, Is.EqualTo(first.Hatched.ColorIndex));
        }

        [Test]
        public void ClassicResolver_LeavesTheEggInert()
        {
            TileFactory factory = TestFactories.Scripted(5, E, D, E);
            Board board = Board.FromLayout(new[,]
            {
                { B, C, D },
                { C, E, B },
                { A, A, A },
            }, factory);
            board.SetTile(new GridPosition(1, 1), factory.CreateMysteryEgg());

            var resolver = new CascadeResolver(new ScoreConfig(10, 1));
            ResolutionResult result = resolver.Resolve(board);

            // No factory, no random — the egg is scenery: unhatched, uncleared,
            // and it still falls with everything else.
            Assert.That(result.Steps[0].EggHatches, Is.Empty);
            Assert.That(board[new GridPosition(1, 0)].Value.Kind, Is.EqualTo(TileKind.MysteryEgg));
        }

        [Test]
        public void Finale_BlastsCrackEggs()
        {
            TileFactory factory = TestFactories.Scripted(5, D, C);
            Board board = StableBoard(factory);
            board.SetTile(new GridPosition(0, 0), factory.CreateSpecial(A, TileKind.StripedH));
            board.SetTile(new GridPosition(2, 0), factory.CreateMysteryEgg());

            CascadeResolver resolver = FullResolver(factory, 0, E);
            ResolutionResult result = resolver.ResolveFinale(board, remainingMoves: 0);

            CascadeStep step = result.Steps[0];
            Assert.That(step.EggHatches.Count, Is.EqualTo(1));
            Assert.That(step.EggHatches[0].Position, Is.EqualTo(new GridPosition(2, 0)));
        }
    }
}
