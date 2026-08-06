using System.Linq;
using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests
{
    /// <summary>
    /// Chapter 6 (levels 101-120): the mystery-egg chapter — meet the egg, eggs
    /// over the jelly encore, eggs behind frosting, the scramble finale. Landmark
    /// pins keep 1-100 bit-identical through the expansion.
    /// </summary>
    public sealed class Chapter6Tests
    {
        [Test]
        public void LevelCount_IsNowOneHundredTwenty()
        {
            Assert.That(LevelCurve.LevelCount, Is.EqualTo(120));
        }

        [Test]
        public void EveryChapterSixLevel_IsWellFormed([Range(101, 120)] int level)
        {
            LevelParameters parameters = LevelCurve.For(level);

            Assert.That(parameters.ColorCount, Is.EqualTo(5));
            Assert.That(parameters.MovesLimit, Is.GreaterThanOrEqualTo(15));
            Assert.That(parameters.StarScores, Is.Ordered.Ascending);
            Assert.That(parameters.EggCells, Is.Not.Empty);
            Assert.That(parameters.EggCells.Count, Is.EqualTo(parameters.EggCells.Distinct().Count()));
            foreach (GridPosition cell in parameters.EggCells)
            {
                Assert.That(cell.X, Is.InRange(0, 7));
                Assert.That(cell.Y, Is.InRange(0, 7));
            }

            Objective eggs = parameters.Objectives.Single(o => o.Type == ObjectiveType.HatchEggs);
            Assert.That(eggs.TargetAmount, Is.EqualTo(parameters.EggCells.Count));
        }

        [Test]
        public void ActOne_TheClutchWidens()
        {
            LevelParameters opener = LevelCurve.For(101);
            Assert.That(opener.EggCells, Is.EqualTo(new[]
            {
                new GridPosition(3, 4), new GridPosition(4, 4),
            }));

            Assert.That(LevelCurve.For(105).EggCells.Count, Is.EqualTo(8));
        }

        [Test]
        public void ActTwo_BringsTheJellyEncore()
        {
            LevelParameters level = LevelCurve.For(106);

            Assert.That(level.JellyRows, Is.EqualTo(2));
            Assert.That(level.JellyLayers, Is.EqualTo(1));
            Assert.That(level.EggCells.Count, Is.EqualTo(4));
            Objective jelly = level.Objectives.Single(o => o.Type == ObjectiveType.ClearJelly);
            Assert.That(jelly.TargetAmount, Is.EqualTo(16)); // 2 rows × 8 cells × 1 layer
        }

        [Test]
        public void ActThree_HidesEggsBehindFrosting()
        {
            LevelParameters opener = LevelCurve.For(111);
            Assert.That(opener.FrostingCells.Count, Is.EqualTo(4));
            Assert.That(opener.FrostingCells.All(c => c.Layers == 1), Is.True);
            Assert.That(opener.EggCells.Count, Is.EqualTo(4));

            LevelParameters thickened = LevelCurve.For(113);
            Objective frosting = thickened.Objectives.Single(o => o.Type == ObjectiveType.ClearFrosting);
            Assert.That(frosting.TargetAmount, Is.EqualTo(8));
        }

        [Test]
        public void ActFour_IsTheScrambleFinale()
        {
            LevelParameters opener = LevelCurve.For(116);
            Assert.That(opener.LockCells.Count, Is.EqualTo(4));
            Assert.That(opener.EggCells.Count, Is.EqualTo(6));
            Assert.That(opener.MovesLimit, Is.EqualTo(16));
            Assert.That(opener.BombCount, Is.EqualTo(0));

            LevelParameters last = LevelCurve.For(120);
            Assert.That(last.BombCount, Is.EqualTo(2));
            Assert.That(last.BombTimerMoves, Is.EqualTo(9));
        }

        [Test]
        public void ActOpeners_CarryTheTutorials()
        {
            Assert.That(LevelCurve.For(101).TutorialText, Is.EqualTo("MATCH BESIDE THE MYSTERY EGG"));
            Assert.That(LevelCurve.For(106).TutorialText, Is.EqualTo("CRACK EGGS, CLEAR THE JELLY"));
            Assert.That(LevelCurve.For(111).TutorialText, Is.EqualTo("EGGS WAIT BEHIND THE FROSTING"));
            Assert.That(LevelCurve.For(116).TutorialText, Is.EqualTo("THE FINAL SCRAMBLE"));
            Assert.That(LevelCurve.For(102).TutorialText, Is.Empty);
            Assert.That(LevelCurve.For(117).TutorialText, Is.Empty);
        }

        [Test]
        public void TutorialCells_PointAtTheOpenersOwnEggs()
        {
            LevelParameters opener = LevelCurve.For(101);
            Assert.That(opener.TutorialCells, Is.EqualTo(opener.EggCells));
        }

        [Test]
        public void LevelsOneToOneHundred_AreUntouched()
        {
            // The pre-expansion landmarks, re-pinned across the new chapter guard.
            Assert.That(LevelCurve.For(1).MovesLimit, Is.EqualTo(24));
            Assert.That(LevelCurve.For(60).StarScores[0], Is.EqualTo(4600));
            Assert.That(LevelCurve.For(81).FrostingCells.Count, Is.EqualTo(4));
            Assert.That(LevelCurve.For(81).EggCells, Is.Empty);

            LevelParameters last = LevelCurve.For(100);
            Assert.That(last.MovesLimit, Is.EqualTo(15));
            Assert.That(last.BombCount, Is.EqualTo(2));
            Assert.That(last.FountainCells.Count, Is.EqualTo(1));
            Assert.That(last.EggCells, Is.Empty);
        }

        [Test]
        public void Theme_Chapter6OpensInTheCandyGarden()
        {
            ThemeParameters opener = ThemeCurve.For(101);
            Assert.That(opener.BgTop.R, Is.EqualTo(0.13f).Within(0.0001f));
            Assert.That(opener.BgTop.G, Is.EqualTo(0.24f).Within(0.0001f));
            Assert.That(opener.Chapter, Is.EqualTo(5));
        }

        [Test]
        public void Theme_DriftsTowardBerryTwilight_AfterTheGarden()
        {
            // The berry tail is REDDER than the garden — the R channel must climb
            // across chapter 6 (0.13 → 0.22) without touching earlier levels.
            Assert.That(ThemeCurve.For(120).BgTop.R, Is.GreaterThan(ThemeCurve.For(110).BgTop.R));
            Assert.That(ThemeCurve.For(110).BgTop.R, Is.GreaterThan(ThemeCurve.For(101).BgTop.R));
        }

        [Test]
        public void HatchObjective_CountsAcrossSteps()
        {
            var tracker = new ObjectiveTracker(new[] { new Objective(ObjectiveType.HatchEggs, 0, 3) });
            var factory = TestFactories.Seeded(5);
            Tile shell = factory.CreateMysteryEgg();
            Tile chick = factory.Create(0);

            CascadeStep StepWithHatches(int count)
            {
                var hatches = Enumerable.Range(0, count)
                    .Select(i => new EggHatch(new GridPosition(i, 0), shell, chick))
                    .ToList();
                return new CascadeStep(0,
                    System.Array.Empty<ClearedTile>(), System.Array.Empty<TileFall>(),
                    System.Array.Empty<TileSpawn>(), 0, System.Array.Empty<int>(),
                    System.Array.Empty<SpecialCreation>(), System.Array.Empty<Detonation>(),
                    System.Array.Empty<JellyHit>(), System.Array.Empty<LockBreak>(),
                    System.Array.Empty<ChocolateSpread>(), System.Array.Empty<IngredientExit>(),
                    System.Array.Empty<FishStrike>(), System.Array.Empty<FrostingHit>(),
                    System.Array.Empty<BombTick>(), hatches);
            }

            tracker.Consume(StepWithHatches(2));
            Assert.That(tracker.AllComplete, Is.False);
            tracker.Consume(StepWithHatches(1));
            Assert.That(tracker.AllComplete, Is.True);
        }
    }
}
