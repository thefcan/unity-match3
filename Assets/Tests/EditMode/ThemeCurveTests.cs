using System;
using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests
{
    /// <summary>
    /// The ambience drift contract: chapter anchors land exactly, and no two
    /// consecutive levels ever differ sharply (the "yavaş yavaş" guarantee).
    /// </summary>
    public sealed class ThemeCurveTests
    {
        private const float MaxPerLevelDrift = 0.02f; // colour channels move at most this much per level

        [Test]
        public void LevelOne_IsThePurpleNightAnchor()
        {
            ThemeParameters theme = ThemeCurve.For(1);

            Assert.That(theme.BgTop.R, Is.EqualTo(0.12f).Within(0.0001f));
            Assert.That(theme.BgTop.G, Is.EqualTo(0.09f).Within(0.0001f));
            Assert.That(theme.BgTop.B, Is.EqualTo(0.24f).Within(0.0001f));
            Assert.That(theme.Chapter, Is.Zero);
        }

        [Test]
        public void ChapterStarts_LandExactlyOnTheirAnchors()
        {
            ThemeParameters ocean = ThemeCurve.For(21);
            Assert.That(ocean.BgTop.G, Is.EqualTo(0.17f).Within(0.0001f), "level 21 is the ocean-teal anchor");
            Assert.That(ocean.Chapter, Is.EqualTo(1));

            ThemeParameters plum = ThemeCurve.For(41);
            Assert.That(plum.BgTop.R, Is.EqualTo(0.23f).Within(0.0001f), "level 41 is the dusk-plum anchor");
            Assert.That(plum.Chapter, Is.EqualTo(2));
        }

        [Test]
        public void ConsecutiveLevels_NeverShiftSharply()
        {
            for (int level = 1; level < 80; level++)
            {
                ThemeParameters a = ThemeCurve.For(level);
                ThemeParameters b = ThemeCurve.For(level + 1);

                float drift = MaxChannelDelta(a.BgTop, b.BgTop);
                drift = Math.Max(drift, MaxChannelDelta(a.BgBottom, b.BgBottom));
                drift = Math.Max(drift, MaxChannelDelta(a.Card, b.Card));
                drift = Math.Max(drift, MaxChannelDelta(a.Slot, b.Slot));

                Assert.That(drift, Is.LessThanOrEqualTo(MaxPerLevelDrift),
                    $"levels {level}->{level + 1} drift too sharply ({drift:0.000})");
            }
        }

        [Test]
        public void PastTheLastChapter_ThePaletteHoldsSteady()
        {
            ThemeParameters far = ThemeCurve.For(500);
            ThemeParameters farther = ThemeCurve.For(1000);

            Assert.That(MaxChannelDelta(far.BgTop, farther.BgTop), Is.EqualTo(0f).Within(0.0001f));
        }

        private static float MaxChannelDelta(ThemeColor a, ThemeColor b)
        {
            float dr = Math.Abs(a.R - b.R);
            float dg = Math.Abs(a.G - b.G);
            float db = Math.Abs(a.B - b.B);
            return Math.Max(dr, Math.Max(dg, db));
        }
    }

    /// <summary>The 60-level campaign: chapter rhythm repeats, difficulty steps up gently.</summary>
    public sealed class ExtendedCampaignTests
    {
        [Test]
        public void EveryCampaignLevel_IsWellFormed([Range(21, 60)] int level)
        {
            LevelParameters parameters = LevelCurve.For(level);

            Assert.That(parameters.MovesLimit, Is.GreaterThanOrEqualTo(10));
            Assert.That(parameters.ColorCount, Is.EqualTo(5), "only the campaign's first levels teach with 4 colours");
            Assert.That(parameters.Objectives, Is.Not.Empty);
            foreach (Objective objective in parameters.Objectives)
            {
                Assert.That(objective.TargetAmount, Is.Positive);
                if (objective.Type == ObjectiveType.CollectColor)
                {
                    Assert.That(objective.ColorIndex, Is.InRange(0, parameters.ColorCount - 1));
                    Assert.That(objective.TargetAmount, Is.LessThanOrEqualTo(40),
                        "collection goals must stay reachable within the move budget");
                }
            }
            Assert.That(parameters.StarScores, Is.Ordered.Ascending);
        }

        [Test]
        public void ChapterZero_IsUntouchedByTheExpansion()
        {
            // The original 20 levels must keep their exact numbers — saves and
            // shipped assets depend on them. Spot-check the landmarks.
            Assert.That(LevelCurve.For(1).MovesLimit, Is.EqualTo(24));
            Assert.That(LevelCurve.For(1).ColorCount, Is.EqualTo(4));
            Assert.That(LevelCurve.For(8).JellyRows, Is.EqualTo(2));
            Assert.That(LevelCurve.For(16).JellyLayers, Is.EqualTo(2));
            Assert.That(LevelCurve.For(20).StarScores[0], Is.EqualTo(2400));
        }

        [Test]
        public void LaterChapters_RepeatTheRhythm_SlightlyHarder()
        {
            LevelParameters l1 = LevelCurve.For(1);
            LevelParameters l21 = LevelCurve.For(21);
            LevelParameters l41 = LevelCurve.For(41);

            Assert.That(l21.MovesLimit, Is.EqualTo(l1.MovesLimit - 1), "chapter 1 starts one move tighter");
            Assert.That(l41.MovesLimit, Is.EqualTo(l1.MovesLimit - 2), "chapter 2 two moves tighter");
            Assert.That(l21.StarScores[0], Is.GreaterThan(l1.StarScores[0]));
            Assert.That(l41.StarScores[0], Is.GreaterThan(l21.StarScores[0]));

            // Jelly follows the chapter-local rhythm: none at a chapter start...
            Assert.That(l21.JellyRows, Is.Zero);
            Assert.That(l41.JellyRows, Is.Zero);
            // ...arriving at chapter-level 8 (global 28 / 48).
            Assert.That(LevelCurve.For(28).JellyRows, Is.EqualTo(2));
            Assert.That(LevelCurve.For(48).JellyRows, Is.EqualTo(2));
        }

        [Test]
        public void ChapterStarts_NeverOpenWithScoreOnlyTutorials()
        {
            // Levels 21/22 and 41/42 are not tutorials — they must carry collection goals.
            foreach (int level in new[] { 21, 22, 41, 42 })
            {
                LevelParameters parameters = LevelCurve.For(level);
                bool hasCollection = false;
                foreach (Objective objective in parameters.Objectives)
                    if (objective.Type == ObjectiveType.CollectColor)
                        hasCollection = true;
                Assert.That(hasCollection, Is.True, $"level {level} should have a collection goal");
            }
        }
    }
}
