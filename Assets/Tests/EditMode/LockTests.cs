using System.Linq;
using Match3.Core;
using NUnit.Framework;
using static Match3.Tests.TestColors;

namespace Match3.Tests
{
    /// <summary>The lock cell-state grid itself.</summary>
    public sealed class LockGridTests
    {
        [Test]
        public void FromCells_PlacesAndCounts()
        {
            var locks = LockGrid.FromCells(3, 3, new[] { new GridPosition(0, 0), new GridPosition(2, 1) });

            Assert.That(locks.HasLock(new GridPosition(0, 0)), Is.True);
            Assert.That(locks.HasLock(new GridPosition(2, 1)), Is.True);
            Assert.That(locks.HasLock(new GridPosition(1, 1)), Is.False);
            Assert.That(locks.TotalRemaining, Is.EqualTo(2));
        }

        [Test]
        public void Break_RemovesOnceAndOnlyOnce()
        {
            var locks = LockGrid.FromCells(3, 3, new[] { new GridPosition(1, 1) });

            Assert.That(locks.Break(new GridPosition(1, 1)), Is.True);
            Assert.That(locks.Break(new GridPosition(1, 1)), Is.False);
            Assert.That(locks.TotalRemaining, Is.Zero);
            Assert.That(locks.IsClear, Is.True);
        }
    }

    /// <summary>Locks in play: absorbing matches and blasts, pinning cells, hiding moves.</summary>
    public sealed class LockResolveTests
    {
        [Test]
        public void MatchBreaksTheLock_AndTheCandySurvives()
        {
            TileFactory factory = TestFactories.Scripted(5, D, E); // two refills
            Board board = Board.FromLayout(new[,]
            {
                { A, B, C },
                { A, C, B },
                { A, B, C },
            }, factory);
            var locks = LockGrid.FromCells(3, 3, new[] { new GridPosition(0, 0) });
            board.AttachLocks(locks);

            var resolver = new CascadeResolver(new ScoreConfig(10, 1));
            resolver.AttachLocks(locks);
            ResolutionResult result = resolver.Resolve(board);

            Assert.That(result.Steps.Count, Is.EqualTo(1));
            CascadeStep step = result.Steps[0];
            Assert.That(step.LockBreaks.Count, Is.EqualTo(1));
            Assert.That(step.LockBreaks[0].Position, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(step.Cleared.Count, Is.EqualTo(2), "only the two unlocked run cells clear");

            Assert.That(board[new GridPosition(0, 0)].Value.ColorIndex, Is.EqualTo(A), "the locked candy survived");
            Assert.That(locks.TotalRemaining, Is.Zero);
            Assert.That(board.IsImmobile(new GridPosition(0, 0)), Is.False, "freed after the break");
        }

        [Test]
        public void BlastBreaksTheLock_ButTheLockedSpecialStaysDormant()
        {
            TileFactory factory = TestFactories.Scripted(5, A, B, C, E, C); // five refills
            Board board = Board.FromLayout(new[,]
            {
                { B, C, D, E },
                { A, D, E, B },
                { A, C, C, D },
                { A, B, C, E },
            }, factory);
            // (0,1) joins the vertical A-run as a striped-H; (2,1) is a LOCKED striped-V.
            board.SetTile(new GridPosition(0, 1), factory.CreateSpecial(A, TileKind.StripedH));
            board.SetTile(new GridPosition(2, 1), factory.CreateSpecial(D, TileKind.StripedV));
            var locks = LockGrid.FromCells(4, 4, new[] { new GridPosition(2, 1) });
            board.AttachLocks(locks);

            var resolver = new CascadeResolver(new ScoreConfig(10, 1));
            resolver.AttachLocks(locks);
            ResolutionResult result = resolver.Resolve(board);

            CascadeStep step = result.Steps[0];
            Assert.That(step.Detonations.Any(d => d.Kind == DetonationKind.Row), Is.True, "the striped-H fired");
            Assert.That(step.Detonations.Any(d => d.Kind == DetonationKind.Column), Is.False,
                        "the LOCKED striped-V must not chain");
            Assert.That(step.LockBreaks.Count, Is.EqualTo(1));

            Tile survivor = board[new GridPosition(2, 1)].Value;
            Assert.That(survivor.Kind, Is.EqualTo(TileKind.StripedV), "candy kept, lock gone");
            Assert.That(locks.TotalRemaining, Is.Zero);
        }

        [Test]
        public void Gravity_TreatsLockedCellsAsFloors()
        {
            TileFactory factory = TestFactories.Seeded();
            Board board = Board.FromLayout(new[,]
            {
                { A, B, C },
                { B, C, A },
                { C, A, B },
                { A, B, C },
            }, factory);
            var locks = LockGrid.FromCells(3, 4, new[] { new GridPosition(1, 1) });
            board.AttachLocks(locks);
            Tile locked = board[new GridPosition(1, 1)].Value;

            // Hole BELOW the lock: nothing may fall past it.
            board.ClearTiles(new[] { new GridPosition(1, 0) });
            Assert.That(board.ApplyGravity(), Is.Empty);
            Assert.That(board[new GridPosition(1, 0)], Is.Null, "the hole under the lock stays");
            Assert.That(board[new GridPosition(1, 1)].Value.Id, Is.EqualTo(locked.Id));

            // Hole ABOVE the lock: the tile two above falls onto the lock's roof.
            board.ClearTiles(new[] { new GridPosition(1, 2) });
            var falls = board.ApplyGravity();
            Assert.That(falls.Count, Is.EqualTo(1));
            Assert.That(falls[0].From, Is.EqualTo(new GridPosition(1, 3)));
            Assert.That(falls[0].To, Is.EqualTo(new GridPosition(1, 2)));
        }

        [Test]
        public void Shuffle_LeavesImmobilePiecesPlanted()
        {
            TileFactory factory = TestFactories.Seeded();
            Board board = Board.FromLayout(new[,]
            {
                { A, B, C },
                { B, C, A },
                { C, A, B },
            }, factory);
            board.SetTile(new GridPosition(1, 1), factory.CreateChocolate());
            var locks = LockGrid.FromCells(3, 3, new[] { new GridPosition(0, 0) });
            board.AttachLocks(locks);
            Tile lockedTile = board[new GridPosition(0, 0)].Value;

            board.Shuffle(new SequenceRandom(new int[700])); // all-zero draws; fallback path is fine

            Assert.That(board[new GridPosition(1, 1)].Value.Kind, Is.EqualTo(TileKind.Chocolate));
            Assert.That(board[new GridPosition(0, 0)].Value.Id, Is.EqualTo(lockedTile.Id));
        }

        [Test]
        public void FindPossibleMove_NeverSuggestsAnImmobileCell()
        {
            TileFactory factory = TestFactories.Seeded();
            Board board = Board.FromLayout(new[,]
            {
                { D, C, B },
                { E, D, A },
                { A, A, C },
            }, factory);

            // Sanity: without locks the ONLY move is swapping (2,0) with (2,1).
            (GridPosition, GridPosition)? move = board.FindPossibleMove();
            Assert.That(move, Is.Not.Null);
            Assert.That(move.Value.Item1, Is.EqualTo(new GridPosition(2, 0)));
            Assert.That(move.Value.Item2, Is.EqualTo(new GridPosition(2, 1)));

            board.AttachLocks(LockGrid.FromCells(3, 3, new[] { new GridPosition(2, 1) }));
            Assert.That(board.HasPossibleMove(), Is.False, "the one move needs a locked cell — board is dead");
        }

        [Test]
        public void JellyUnderALockedCellIsProtected()
        {
            TileFactory factory = TestFactories.Scripted(5, E, E);
            Board board = Board.FromLayout(new[,]
            {
                { B, C, D },
                { C, D, B },
                { A, A, A },
            }, factory);
            var locks = LockGrid.FromCells(3, 3, new[] { new GridPosition(1, 0) });
            board.AttachLocks(locks);
            JellyGrid jelly = JellyGrid.BottomRows(3, 3, rows: 1, layers: 1);

            var resolver = new CascadeResolver(new ScoreConfig(10, 1));
            resolver.AttachLocks(locks);
            resolver.AttachJelly(jelly);
            ResolutionResult result = resolver.Resolve(board);

            CascadeStep step = result.Steps[0];
            Assert.That(jelly.LayersAt(new GridPosition(1, 0)), Is.EqualTo(1), "the lock absorbed the hit");
            Assert.That(step.JellyHits.Count, Is.EqualTo(2), "the two unlocked cells damaged their jelly");
            Assert.That(step.JellyHits.Any(hit => hit.Position == new GridPosition(1, 0)), Is.False);
        }
    }
}
