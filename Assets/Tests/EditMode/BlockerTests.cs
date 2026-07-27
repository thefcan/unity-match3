using System.Linq;
using Match3.Core;
using NUnit.Framework;
using static Match3.Tests.TestColors;

namespace Match3.Tests
{
    /// <summary>
    /// The chapter-5 blockers: layered frosting (damage instead of clearing),
    /// licorice swirls (fall, absorb striped beams, one hit), the chocolate
    /// fountain (indestructible chocolate source) and countdown bomb candies.
    /// </summary>
    public sealed class BlockerTests
    {
        private static Board Latin4(TileFactory factory) => Board.FromLayout(new[,]
        {
            { A, B, C, D },
            { B, C, D, A },
            { C, D, A, B },
            { D, A, B, C },
        }, factory);

        // ---- Frosting ---------------------------------------------------------------

        [Test]
        public void AdjacentMatch_PeelsOneFrostingLayer()
        {
            TileFactory factory = TestFactories.Scripted(5, E, E, E);
            Board board = Board.FromLayout(new[,]
            {
                { B, D, C },
                { A, A, A },
                { C, D, B },
            }, factory);
            var cell = new GridPosition(1, 2);
            board.SetTile(cell, factory.CreateFrosting());
            var frosting = new FrostingGrid(3, 3);
            frosting.Set(cell, 2);

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom());
            resolver.AttachFrosting(frosting);
            ResolutionResult result = resolver.Resolve(board);

            CascadeStep step = result.Steps[0];
            Assert.That(step.FrostingHits.Count, Is.EqualTo(1));
            Assert.That(step.FrostingHits[0].Position, Is.EqualTo(cell));
            Assert.That(step.FrostingHits[0].RemainingLayers, Is.EqualTo(1));
            Assert.That(step.Cleared.Count, Is.EqualTo(3), "the run clears; the frosting tile survives");
            Assert.That(board[cell].Value.Kind, Is.EqualTo(TileKind.Frosting));
            Assert.That(frosting.TotalRemaining, Is.EqualTo(1));
        }

        [Test]
        public void LastFrostingLayer_ClearsTheTile()
        {
            TileFactory factory = TestFactories.Scripted(5, D, E, D, E);
            Board board = Board.FromLayout(new[,]
            {
                { B, D, C },
                { A, A, A },
                { C, D, B },
            }, factory);
            var cell = new GridPosition(1, 2);
            board.SetTile(cell, factory.CreateFrosting());
            Tile frostingTile = board[cell].Value;
            var frosting = new FrostingGrid(3, 3);
            frosting.Set(cell, 1);

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom());
            resolver.AttachFrosting(frosting);
            ResolutionResult result = resolver.Resolve(board);

            CascadeStep step = result.Steps[0];
            Assert.That(step.FrostingHits[0].RemainingLayers, Is.EqualTo(0));
            Assert.That(step.Cleared.Any(c => c.Tile.Id == frostingTile.Id), Is.True,
                        "the last layer lets the clear through");
            Assert.That(frosting.IsClear, Is.True);
            Assert.That(board[cell].Value.Kind, Is.Not.EqualTo(TileKind.Frosting), "the cell refilled with candy");
        }

        [Test]
        public void Blast_DamagesFrosting_WithoutAdjacency()
        {
            TileFactory factory = TestFactories.Scripted(5, D, E);
            Board board = Board.FromLayout(new[,]
            {
                { A, B, C },
                { B, C, A },
                { C, A, B },
            }, factory);
            board.SetTile(new GridPosition(0, 1), factory.CreateSpecial(C, TileKind.StripedH));
            var cell = new GridPosition(2, 1);
            board.SetTile(cell, factory.CreateFrosting());
            var frosting = new FrostingGrid(3, 3);
            frosting.Set(cell, 2);

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom());
            resolver.AttachFrosting(frosting);
            ResolutionResult result = resolver.ResolveHammer(board, new GridPosition(0, 1));

            CascadeStep step = result.Steps[0];
            Assert.That(step.FrostingHits.Count, Is.EqualTo(1), "the beam hits the frosting cell directly");
            Assert.That(step.FrostingHits[0].RemainingLayers, Is.EqualTo(1));
            Assert.That(board[cell].Value.Kind, Is.EqualTo(TileKind.Frosting));
        }

        [Test]
        public void Frosting_IsAGravityFloor()
        {
            TileFactory factory = TestFactories.Scripted(5, E);
            Board board = Board.FromLayout(new[,]
            {
                { A, B, C },
                { B, C, A },
                { C, A, B },
            }, factory);
            var cell = new GridPosition(1, 1);
            board.SetTile(cell, factory.CreateFrosting());
            var frosting = new FrostingGrid(3, 3);
            frosting.Set(cell, 2);
            Tile above = board[new GridPosition(1, 2)].Value;

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom());
            resolver.AttachFrosting(frosting);
            resolver.ResolveHammer(board, new GridPosition(1, 0));

            Assert.That(board[new GridPosition(1, 2)].Value.Id, Is.EqualTo(above.Id),
                        "nothing falls past the frosting; the hole under it refills instead");
            Assert.That(frosting.LayersAt(cell), Is.EqualTo(1), "the smash next door still peeled a layer");
        }

        [Test]
        public void ClearFrostingObjective_CountsLayers()
        {
            var tracker = new ObjectiveTracker(new[] { new Objective(ObjectiveType.ClearFrosting, 0, 2) });
            var step = new CascadeStep(0,
                System.Array.Empty<ClearedTile>(), System.Array.Empty<TileFall>(),
                System.Array.Empty<TileSpawn>(), 0, System.Array.Empty<int>(),
                System.Array.Empty<SpecialCreation>(), System.Array.Empty<Detonation>(),
                System.Array.Empty<JellyHit>(), System.Array.Empty<LockBreak>(),
                System.Array.Empty<ChocolateSpread>(), System.Array.Empty<IngredientExit>(),
                System.Array.Empty<FishStrike>(),
                new[] { new FrostingHit(new GridPosition(0, 0), 1), new FrostingHit(new GridPosition(1, 0), 0) },
                System.Array.Empty<BombTick>());

            tracker.Consume(step);

            Assert.That(tracker.Progress(0), Is.EqualTo(2));
            Assert.That(tracker.AllComplete, Is.True);
        }

        [Test]
        public void FishStrike_PrefersFrosting_OverChocolate()
        {
            TileFactory factory = TestFactories.Scripted(5, D, E);
            Board board = Board.FromLayout(new[,]
            {
                { A, B, C },
                { B, C, A },
                { C, A, B },
            }, factory);
            board.SetTile(new GridPosition(1, 1), factory.CreateSpecial(C, TileKind.Fish));
            var frostingCell = new GridPosition(0, 0);
            board.SetTile(frostingCell, factory.CreateFrosting());
            var frosting = new FrostingGrid(3, 3);
            frosting.Set(frostingCell, 1);
            board.SetTile(new GridPosition(2, 2), factory.CreateChocolate());

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom(0));
            resolver.AttachFrosting(frosting);
            ResolutionResult result = resolver.ResolveHammer(board, new GridPosition(1, 1));

            CascadeStep step = result.Steps[0];
            Assert.That(step.FishStrikes[0].To, Is.EqualTo(frostingCell));
            Assert.That(board[new GridPosition(2, 2)].Value.IsChocolate, Is.True, "the chocolate was spared");
        }

        // ---- Swirl ------------------------------------------------------------------

        [Test]
        public void Swirl_AbsorbsAStripedBeam()
        {
            TileFactory factory = TestFactories.Seeded(5, 21);
            Board board = Latin4(factory);
            board.SetTile(new GridPosition(0, 1), factory.CreateSpecial(C, TileKind.StripedH));
            board.SetTile(new GridPosition(2, 1), factory.CreateSwirl());
            Tile swirl = board[new GridPosition(2, 1)].Value;
            Tile beyond = board[new GridPosition(3, 1)].Value;

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SystemRandom(21));
            ResolutionResult result = resolver.ResolveHammer(board, new GridPosition(0, 1));

            CascadeStep step = result.Steps[0];
            Assert.That(step.Cleared.Any(c => c.Tile.Id == swirl.Id), Is.True, "the swirl eats the ray and dies");
            Assert.That(step.Cleared.Any(c => c.Tile.Id == beyond.Id), Is.False, "the cell behind it is shielded");
        }

        [Test]
        public void Swirl_Falls_LikeACandy()
        {
            TileFactory factory = TestFactories.Seeded(5, 22);
            Board board = Latin4(factory);
            board.SetTile(new GridPosition(1, 3), factory.CreateSwirl());
            Tile swirl = board[new GridPosition(1, 3)].Value;

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SystemRandom(22));
            ResolutionResult result = resolver.ResolveHammer(board, new GridPosition(1, 1));

            CascadeStep step = result.Steps[0];
            Assert.That(step.Falls.Any(f => f.Tile.Id == swirl.Id), Is.True);
            Assert.That(board[new GridPosition(1, 2)].Value.Id, Is.EqualTo(swirl.Id));
        }

        [Test]
        public void Swirl_DiesToAnAdjacentMatch()
        {
            TileFactory factory = TestFactories.Scripted(5, D, E, D, E);
            Board board = Board.FromLayout(new[,]
            {
                { B, D, C },
                { A, A, A },
                { C, D, B },
            }, factory);
            board.SetTile(new GridPosition(1, 2), factory.CreateSwirl());
            Tile swirl = board[new GridPosition(1, 2)].Value;

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom());
            ResolutionResult result = resolver.Resolve(board);

            Assert.That(result.Steps[0].Cleared.Any(c => c.Tile.Id == swirl.Id), Is.True);
        }

        [Test]
        public void Swirl_NeverJoinsAColourRun()
        {
            TileFactory factory = TestFactories.Scripted(5);
            Board board = Board.FromLayout(new[,]
            {
                { B, D, C },
                { A, A, A },
                { C, D, B },
            }, factory);
            board.SetTile(new GridPosition(1, 1), factory.CreateSwirl()); // breaks the run

            Assert.That(board.FindMatchRuns().Count, Is.EqualTo(0));
            Assert.That(board.FindSquares().Count, Is.EqualTo(0));
        }

        // ---- Chocolate fountain -----------------------------------------------------

        [Test]
        public void Fountain_OozesChocolate_EvenWhenTheBloodlineIsExtinct()
        {
            TileFactory factory = TestFactories.Scripted(5, E, D, E);
            Board board = Board.FromLayout(new[,]
            {
                { D, C, B, D },
                { B, D, C, A },
                { A, A, D, B },
                { C, B, A, C },
            }, factory);
            var fountainPos = new GridPosition(3, 3);
            board.SetTile(fountainPos, factory.CreateFountain());

            var from = new GridPosition(2, 0);
            var to = new GridPosition(2, 1);
            board.Swap(from, to);
            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom(0));
            ResolutionResult result = resolver.ResolveSwap(board, from, to);

            CascadeStep last = result.Steps[result.Steps.Count - 1];
            Assert.That(last.ChocolateSpreads.Count, Is.EqualTo(1));
            Assert.That(last.ChocolateSpreads[0].From, Is.EqualTo(fountainPos));
            Assert.That(board[last.ChocolateSpreads[0].To].Value.IsChocolate, Is.True);
        }

        [Test]
        public void Fountain_StaysQuiet_WhenChocolateBrokeThisMove()
        {
            TileFactory factory = TestFactories.Scripted(5, E, D, D, E);
            Board board = Board.FromLayout(new[,]
            {
                { D, C, B, D },
                { B, D, C, A },
                { A, A, D, B },
                { C, B, A, C },
            }, factory);
            board.SetTile(new GridPosition(3, 3), factory.CreateFountain());
            board.SetTile(new GridPosition(0, 0), factory.CreateChocolate()); // crumbles with the match

            var from = new GridPosition(2, 0);
            var to = new GridPosition(2, 1);
            board.Swap(from, to);
            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom());
            ResolutionResult result = resolver.ResolveSwap(board, from, to);

            Assert.That(result.Steps.All(s => s.ChocolateSpreads.Count == 0), Is.True);
        }

        [Test]
        public void Fountain_ShrugsOffAHammer()
        {
            TileFactory factory = TestFactories.Scripted(5);
            Board board = Board.FromLayout(new[,]
            {
                { A, B, C },
                { B, C, A },
                { C, A, B },
            }, factory);
            board.SetTile(new GridPosition(1, 1), factory.CreateFountain());

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom());
            ResolutionResult result = resolver.ResolveHammer(board, new GridPosition(1, 1));

            Assert.That(result.Steps.Count, Is.EqualTo(0), "nothing to record — the caller should refund");
            Assert.That(board[new GridPosition(1, 1)].Value.Kind, Is.EqualTo(TileKind.ChocolateFountain));
        }

        // ---- Bomb candies -----------------------------------------------------------

        [Test]
        public void BombTimers_ArmTickDefuse()
        {
            var timers = new BombTimers();
            timers.Arm(7, 3);
            timers.Arm(9, 1);

            Assert.That(timers.Tick(7), Is.EqualTo(3), "the birth move never burns the fuse");
            Assert.That(timers.Tick(9), Is.EqualTo(1));

            Assert.That(timers.Tick(7), Is.EqualTo(2));
            Assert.That(timers.Tick(9), Is.EqualTo(0), "zero means exploded");

            timers.Defuse(7);
            Assert.That(timers.TryGet(7, out int gone), Is.False);
            Assert.That(gone, Is.EqualTo(0));
            Assert.That(timers.Count, Is.EqualTo(1));
        }

        [Test]
        public void MatchingABomb_DefusesIt()
        {
            TileFactory factory = TestFactories.Scripted(5, D, E, D);
            Board board = Board.FromLayout(new[,]
            {
                { B, D, C },
                { A, A, A },
                { C, D, B },
            }, factory);
            Tile bomb = factory.CreateBomb(A);
            board.SetTile(new GridPosition(2, 1), bomb);
            var timers = new BombTimers();
            timers.Arm(bomb.Id, 5);

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom());
            resolver.AttachBombs(timers, 0, 5);
            ResolutionResult result = resolver.Resolve(board);

            Assert.That(result.Steps[0].Cleared.Any(c => c.Tile.Id == bomb.Id), Is.True,
                        "a bomb candy matches by colour like a normal one");
            Assert.That(timers.Count, Is.EqualTo(0));
        }

        [Test]
        public void ResolveBombTick_CountsDownAndReportsThePosition()
        {
            TileFactory factory = TestFactories.Scripted(5);
            Board board = Board.FromLayout(new[,]
            {
                { A, B, C },
                { B, C, A },
                { C, A, B },
            }, factory);
            Tile bomb = factory.CreateBomb(A);
            var cell = new GridPosition(2, 0);
            board.SetTile(cell, bomb);
            var timers = new BombTimers();
            timers.Arm(bomb.Id, 2);

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom());
            resolver.AttachBombs(timers, 0, 2);

            ResolutionResult first = resolver.ResolveBombTick(board);
            Assert.That(first.Steps.Count, Is.EqualTo(1));
            Assert.That(first.Steps[0].BombTicks[0].Position, Is.EqualTo(cell));
            Assert.That(first.Steps[0].BombTicks[0].Remaining, Is.EqualTo(2), "the arming move is free");

            ResolutionResult second = resolver.ResolveBombTick(board);
            Assert.That(second.Steps[0].BombTicks[0].Remaining, Is.EqualTo(1));

            ResolutionResult third = resolver.ResolveBombTick(board);
            Assert.That(third.Steps[0].BombTicks[0].Remaining, Is.EqualTo(0), "boom");
        }

        [Test]
        public void Dispenser_InjectsAnArmedBomb_ThroughTheRefill()
        {
            TileFactory factory = TestFactories.Scripted(5, D, E, D);
            Board board = Board.FromLayout(new[,]
            {
                { B, D, C },
                { A, A, A },
                { C, D, B },
            }, factory);
            var timers = new BombTimers();

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom(1));
            resolver.AttachBombs(timers, 1, 7);
            ResolutionResult result = resolver.Resolve(board);

            CascadeStep step = result.Steps[0];
            TileSpawn bombSpawn = step.Spawns.Single(s => s.Tile.Kind == TileKind.Bomb);
            Assert.That(timers.TryGet(bombSpawn.Tile.Id, out int remaining), Is.True);
            Assert.That(remaining, Is.EqualTo(7));
            Assert.That(board[bombSpawn.Position].Value.Kind, Is.EqualTo(TileKind.Bomb));
            Assert.That(bombSpawn.Tile.ColorIndex, Is.EqualTo(E), "the bomb keeps the colour the refill rolled");
        }

        [Test]
        public void Dispenser_NeverExceedsTwoBombsInPlay()
        {
            TileFactory factory = TestFactories.Scripted(5, D, E, D);
            Board board = Board.FromLayout(new[,]
            {
                { B, D, C },
                { A, A, A },
                { C, D, B },
            }, factory);
            var timers = new BombTimers();
            Tile bombA = factory.CreateBomb(B);
            Tile bombB = factory.CreateBomb(C);
            board.SetTile(new GridPosition(0, 0), bombA);
            board.SetTile(new GridPosition(2, 0), bombB);
            timers.Arm(bombA.Id, 9);
            timers.Arm(bombB.Id, 9);

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SequenceRandom());
            resolver.AttachBombs(timers, 3, 9);
            ResolutionResult result = resolver.Resolve(board);

            Assert.That(result.Steps[0].Spawns.All(s => s.Tile.Kind != TileKind.Bomb), Is.True);
            Assert.That(timers.Count, Is.EqualTo(2));
        }
    }
}
