using System;
using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests
{
    /// <summary>
    /// The chapter-5 campaign (levels 81-100): frosting → bombs+swirls → the
    /// fountain-fed fish showcase → the mixed finale — plus the tutorial lines on
    /// every act opener, the golden-dawn theme anchor and the new music mood.
    /// Levels 1-80 must not move by a single bit.
    /// </summary>
    public sealed class Chapter5Tests
    {
        [Test]
        public void EveryChapterFiveLevel_IsWellFormed([Range(81, 100)] int level)
        {
            LevelParameters parameters = LevelCurve.For(level);

            Assert.That(parameters.MovesLimit, Is.GreaterThanOrEqualTo(10));
            Assert.That(parameters.ColorCount, Is.EqualTo(5));
            Assert.That(parameters.Objectives, Is.Not.Empty);
            foreach (Objective objective in parameters.Objectives)
            {
                Assert.That(objective.TargetAmount, Is.Positive);
                if (objective.Type == ObjectiveType.CollectColor)
                    Assert.That(objective.TargetAmount, Is.LessThanOrEqualTo(42));
            }
            Assert.That(parameters.StarScores, Is.Ordered.Ascending);
        }

        [Test]
        public void LevelCount_IsNowOneHundred()
        {
            Assert.That(LevelCurve.LevelCount, Is.EqualTo(100));
        }

        [Test]
        public void ActOne_FrostingShelf_WidensAndThickens()
        {
            LevelParameters l81 = LevelCurve.For(81);
            Assert.That(l81.FrostingCells.Count, Is.EqualTo(4));
            foreach (FrostingCell cell in l81.FrostingCells)
                Assert.That(cell.Layers, Is.EqualTo(1));
            AssertFrostingObjective(l81, 4);

            LevelParameters l83 = LevelCurve.For(83);
            Assert.That(l83.FrostingCells.Count, Is.EqualTo(8), "the shelf spans the full row by 83");
            AssertFrostingObjective(l83, 12);

            LevelParameters l85 = LevelCurve.For(85);
            Assert.That(l85.FrostingCells.Count, Is.EqualTo(12), "a second slab arrives low");
            AssertFrostingObjective(l85, 20);
        }

        [Test]
        public void ActTwo_BombsRampAndSwirlsArrive()
        {
            LevelParameters l86 = LevelCurve.For(86);
            Assert.That(l86.BombCount, Is.EqualTo(3));
            Assert.That(l86.BombTimerMoves, Is.EqualTo(10));
            Assert.That(l86.SwirlCells.Count, Is.Zero, "bombs get one clean level first");

            LevelParameters l88 = LevelCurve.For(88);
            Assert.That(l88.SwirlCells.Count, Is.EqualTo(2));

            LevelParameters l90 = LevelCurve.For(90);
            Assert.That(l90.BombCount, Is.EqualTo(7));
            Assert.That(l90.BombTimerMoves, Is.EqualTo(9));
            Assert.That(l90.SwirlCells.Count, Is.EqualTo(4));
        }

        [Test]
        public void ActThree_FountainsFeedTheChocolateGoal()
        {
            LevelParameters l91 = LevelCurve.For(91);
            Assert.That(l91.FountainCells.Count, Is.EqualTo(1));
            Assert.That(l91.ChocolateCells.Count, Is.Zero, "all chocolate comes from the fountain");
            AssertObjective(l91, ObjectiveType.ClearChocolate, 3);

            LevelParameters l95 = LevelCurve.For(95);
            Assert.That(l95.FountainCells.Count, Is.EqualTo(2));
            AssertObjective(l95, ObjectiveType.ClearChocolate, 7);
        }

        [Test]
        public void ActFour_MixesEverything_TwoMovesTighter()
        {
            LevelParameters l96 = LevelCurve.For(96);
            Assert.That(l96.FrostingCells.Count, Is.EqualTo(4));
            Assert.That(l96.SwirlCells.Count, Is.EqualTo(2));
            Assert.That(l96.BombCount, Is.Zero);
            AssertFrostingObjective(l96, 8);
            Assert.That(l96.MovesLimit, Is.EqualTo(16), "18 for chapter-level 16, minus the finale's 2");

            LevelParameters l100 = LevelCurve.For(100);
            Assert.That(l100.BombCount, Is.EqualTo(2));
            Assert.That(l100.FountainCells.Count, Is.EqualTo(1));
            Assert.That(l100.MovesLimit, Is.EqualTo(15));
        }

        [Test]
        public void ActOpeners_CarryOneLineTutorials()
        {
            Assert.That(LevelCurve.For(61).TutorialText, Is.EqualTo("MATCH BESIDE THE CAGES"));
            Assert.That(LevelCurve.For(61).TutorialCells.Count, Is.EqualTo(2), "points at the two cages");
            Assert.That(LevelCurve.For(66).TutorialText, Is.EqualTo("STOP THE CHOCOLATE SPREAD"));
            Assert.That(LevelCurve.For(71).TutorialText, Is.EqualTo("CARRY CHERRIES TO THE FLOOR"));
            Assert.That(LevelCurve.For(81).TutorialText, Is.EqualTo("MATCH BESIDE THE FROSTING"));
            Assert.That(LevelCurve.For(86).TutorialText, Is.EqualTo("DEFUSE BOMBS BEFORE ZERO"));
            Assert.That(LevelCurve.For(91).TutorialText, Is.EqualTo("A 2X2 MAKES A FISH"));
            Assert.That(LevelCurve.For(96).TutorialText, Is.EqualTo("EVERY BLOCKER AT ONCE"));

            // Non-opener levels stay silent.
            Assert.That(LevelCurve.For(82).TutorialText, Is.Empty);
            Assert.That(LevelCurve.For(45).TutorialText, Is.Empty);
        }

        [Test]
        public void TutorialCells_PointAtTheLevelsOwnLayout()
        {
            LevelParameters l81 = LevelCurve.For(81);
            foreach (GridPosition cell in l81.TutorialCells)
            {
                bool isFrosting = false;
                foreach (FrostingCell frosting in l81.FrostingCells)
                    if (frosting.Position.Equals(cell))
                        isFrosting = true;
                Assert.That(isFrosting, Is.True, $"{cell} is not a frosting cell");
            }

            LevelParameters l61 = LevelCurve.For(61);
            foreach (GridPosition cell in l61.TutorialCells)
                Assert.That(l61.LockCells, Does.Contain(cell));
        }

        [Test]
        public void LevelsOneToEighty_AreUntouched()
        {
            Assert.That(LevelCurve.For(1).MovesLimit, Is.EqualTo(24));
            Assert.That(LevelCurve.For(20).StarScores[0], Is.EqualTo(2400));
            Assert.That(LevelCurve.For(60).StarScores[0], Is.EqualTo(4600));

            LevelParameters l80 = LevelCurve.For(80);
            Assert.That(l80.LockCells.Count, Is.EqualTo(4));
            Assert.That(l80.ChocolateCells.Count, Is.EqualTo(4));
            Assert.That(l80.IngredientCount, Is.EqualTo(4));
            Assert.That(l80.FrostingCells.Count, Is.Zero);
            Assert.That(l80.BombCount, Is.Zero);
            Assert.That(l80.SwirlCells.Count, Is.Zero);
            Assert.That(l80.FountainCells.Count, Is.Zero);
        }

        [Test]
        public void Level81_OpensOnTheGoldenDawnAnchor_AndOldThemesHold()
        {
            ThemeParameters dawn = ThemeCurve.For(81);
            Assert.That(dawn.BgTop.R, Is.EqualTo(0.27f).Within(0.0001f));
            Assert.That(dawn.Chapter, Is.EqualTo(4));

            // Level 80 keeps its exact pre-expansion colour.
            ThemeParameters level80 = ThemeCurve.For(80);
            Assert.That(level80.BgTop.R, Is.EqualTo(0.26f + (0.27f - 0.26f) * (19f / 20f)).Within(0.0001f));

            // Level 100 sits 19/20ths of the way into the candy-garden drift.
            ThemeParameters level100 = ThemeCurve.For(100);
            Assert.That(level100.BgTop.G, Is.EqualTo(0.19f + (0.24f - 0.19f) * (19f / 20f)).Within(0.0001f));
        }

        [Test]
        public void MusicChapterFour_HasItsOwnMood_AndOldChaptersAreFrozen()
        {
            // The first four moods must never change — their wavs are shipped.
            Assert.That(MusicComposer.SamplesPerBeat(0), Is.EqualTo((int)Math.Round(44100 * 60.0 / 84)));
            Assert.That(MusicComposer.SamplesPerBeat(1), Is.EqualTo((int)Math.Round(44100 * 60.0 / 90)));
            Assert.That(MusicComposer.SamplesPerBeat(2), Is.EqualTo((int)Math.Round(44100 * 60.0 / 86)));
            Assert.That(MusicComposer.SamplesPerBeat(3), Is.EqualTo((int)Math.Round(44100 * 60.0 / 94)));

            Assert.That(MusicComposer.SamplesPerBeat(4), Is.Not.EqualTo(MusicComposer.SamplesPerBeat(3)),
                        "candy garden runs at its own tempo");

            float[] first = MusicComposer.ComposeStereo(4);
            float[] second = MusicComposer.ComposeStereo(4);
            for (int i = 0; i < 1000; i++)
                Assert.That(second[i], Is.EqualTo(first[i]), "chapter 4 must be deterministic");
        }

        private static void AssertFrostingObjective(LevelParameters parameters, int expectedLayers) =>
            AssertObjective(parameters, ObjectiveType.ClearFrosting, expectedLayers);

        private static void AssertObjective(LevelParameters parameters, ObjectiveType type, int expectedAmount)
        {
            foreach (Objective objective in parameters.Objectives)
            {
                if (objective.Type == type)
                {
                    Assert.That(objective.TargetAmount, Is.EqualTo(expectedAmount));
                    return;
                }
            }
            Assert.Fail($"no {type} objective found");
        }
    }
}
