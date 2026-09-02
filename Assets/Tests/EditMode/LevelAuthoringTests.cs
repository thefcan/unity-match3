using System.Collections.Generic;
using System.Linq;
using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests
{
    /// <summary>
    /// Invariants over the WHOLE authored campaign, rather than the landmark levels the
    /// chapter tests pin. Every act builder computes its cells from the chapter level
    /// (<c>4 - half</c>, <c>3 + half</c>, and friends), so one careless edit can push a
    /// blocker off the board or drop two blockers onto the same cell. Both mistakes are
    /// silent in the generator and only surface at runtime — the first as an exception
    /// inside InitState, the second as a blocker that quietly loses to whichever list
    /// GameManager plants last.
    /// </summary>
    public sealed class LevelAuthoringTests
    {
        /// <summary>The cell lists that become TILES on the board, and so must not collide.</summary>
        private static IEnumerable<(string name, IEnumerable<GridPosition> cells)> TileBlockers(LevelParameters p)
        {
            yield return ("chocolate", p.ChocolateCells);
            yield return ("frosting", p.FrostingCells.Select(f => f.Position));
            yield return ("swirl", p.SwirlCells);
            yield return ("fountain", p.FountainCells);
            yield return ("egg", p.EggCells);
        }

        [Test]
        public void EveryAuthoredCell_LiesInsideItsOwnBoard()
        {
            for (int level = 1; level <= LevelCurve.LevelCount; level++)
            {
                LevelParameters p = LevelCurve.For(level);
                var lists = TileBlockers(p).ToList();
                lists.Add(("lock", p.LockCells));
                lists.Add(("tutorial", p.TutorialCells));

                foreach ((string name, IEnumerable<GridPosition> cells) in lists)
                {
                    foreach (GridPosition cell in cells)
                    {
                        bool inside = cell.X >= 0 && cell.X < p.Width && cell.Y >= 0 && cell.Y < p.Height;
                        Assert.That(inside, Is.True,
                                    $"level {level}: {name} cell ({cell.X},{cell.Y}) is outside the {p.Width}x{p.Height} board");
                    }
                }
            }
        }

        [Test]
        public void NoTwoBlockersFightOverTheSameCell()
        {
            // Locks are deliberately excluded: a caged egg (chapter 6's finale act) or a
            // locked candy over frosting is a real, tested combination — the lock lives
            // in its own grid, not in the tile.
            for (int level = 1; level <= LevelCurve.LevelCount; level++)
            {
                LevelParameters p = LevelCurve.For(level);
                var taken = new Dictionary<GridPosition, string>();

                foreach ((string name, IEnumerable<GridPosition> cells) in TileBlockers(p))
                {
                    var local = new HashSet<GridPosition>();
                    foreach (GridPosition cell in cells)
                    {
                        Assert.That(local.Add(cell), Is.True,
                                    $"level {level}: {name} lists ({cell.X},{cell.Y}) twice");
                        Assert.That(taken.ContainsKey(cell), Is.False,
                                    $"level {level}: {name} lands on ({cell.X},{cell.Y}), already claimed by " +
                                    (taken.TryGetValue(cell, out string other) ? other : "?"));
                        taken[cell] = name;
                    }
                }
            }
        }

        [Test]
        public void EveryLevel_AsksForSomething()
        {
            for (int level = 1; level <= LevelCurve.LevelCount; level++)
            {
                LevelParameters p = LevelCurve.For(level);
                // (A zero target cannot get this far — Objective's own constructor
                // rejects it — so the only thing left to check is that one exists.)
                Assert.That(p.Objectives.Count, Is.GreaterThan(0), $"level {level} has no objective at all");
            }
        }

        [Test]
        public void StarBars_RiseInOrder()
        {
            for (int level = 1; level <= LevelCurve.LevelCount; level++)
            {
                LevelParameters p = LevelCurve.For(level);
                Assert.That(p.StarScores.Count, Is.EqualTo(3), $"level {level} needs exactly three star bars");
                Assert.That(p.StarScores[1], Is.GreaterThan(p.StarScores[0]), $"level {level}: 2-star must beat 1-star");
                Assert.That(p.StarScores[2], Is.GreaterThan(p.StarScores[1]), $"level {level}: 3-star must beat 2-star");
            }
        }

        [Test]
        public void TheThreeStarBar_NeverOutrunsTheMoveBudget()
        {
            // The bar climbs (+100/level, +1100/chapter) while the budget shrinks, so
            // the score a player must average PER MOVE rises across the campaign: 42 at
            // level 1, 1053 at level 120. That drift is intentional difficulty; what
            // this pins is that it stays drift. A curve edit that lifts the bar or cuts
            // the budget hard enough to push a level past the current worst case has
            // changed the game's reward economy, and should say so out loud rather than
            // shipping as a formula tweak.
            const int worstCaseSoFar = 1100;

            for (int level = 1; level <= LevelCurve.LevelCount; level++)
            {
                LevelParameters p = LevelCurve.For(level);
                Assert.That(p.MovesLimit, Is.GreaterThan(0), $"level {level} hands over no moves");
                int perMove = p.StarScores[2] / p.MovesLimit;
                Assert.That(perMove, Is.LessThanOrEqualTo(worstCaseSoFar),
                            $"level {level} asks for {perMove} points per move to 3-star it");
            }
        }
    }
}
