using System.IO;
using System.Linq;
using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests
{
    /// <summary>Campaign progress: star bookkeeping, sequential unlock, and persistence.</summary>
    public sealed class PlayerProgressTests
    {
        [Test]
        public void FreshProfile_UnlocksOnlyLevelOne()
        {
            var progress = new PlayerProgress();

            Assert.That(progress.HighestUnlocked, Is.EqualTo(1));
            Assert.That(progress.IsUnlocked(1), Is.True);
            Assert.That(progress.IsUnlocked(2), Is.False);
        }

        [Test]
        public void CompletingALevel_UnlocksTheNext()
        {
            var progress = new PlayerProgress();

            progress.RecordResult(1, 2);

            Assert.That(progress.IsCompleted(1), Is.True);
            Assert.That(progress.IsUnlocked(2), Is.True);
            Assert.That(progress.IsUnlocked(3), Is.False);
        }

        [Test]
        public void ZeroStarWin_StillCompletesAndUnlocks()
        {
            var progress = new PlayerProgress();

            progress.RecordResult(1, 0);

            Assert.That(progress.IsCompleted(1), Is.True, "a scraped win still counts");
            Assert.That(progress.StarsFor(1), Is.Zero);
            Assert.That(progress.IsUnlocked(2), Is.True);
        }

        [Test]
        public void WorseRerun_NeverDowngradesStars()
        {
            var progress = new PlayerProgress();

            progress.RecordResult(3, 3);
            progress.RecordResult(3, 1);

            Assert.That(progress.StarsFor(3), Is.EqualTo(3));
        }
    }

    public sealed class ProgressSerializerTests
    {
        [Test]
        public void Roundtrip_PreservesEveryEntry()
        {
            var progress = new PlayerProgress();
            progress.RecordResult(1, 3);
            progress.RecordResult(2, 0);
            progress.RecordResult(7, 2);

            PlayerProgress restored = ProgressSerializer.Deserialize(ProgressSerializer.Serialize(progress));

            Assert.That(restored.StarsFor(1), Is.EqualTo(3));
            Assert.That(restored.IsCompleted(2), Is.True);
            Assert.That(restored.StarsFor(2), Is.Zero);
            Assert.That(restored.StarsFor(7), Is.EqualTo(2));
            Assert.That(restored.HighestUnlocked, Is.EqualTo(8));
        }

        [Test]
        public void CorruptInput_DegradesToAFreshProfile()
        {
            PlayerProgress restored = ProgressSerializer.Deserialize("!!! not progress data\n=5\nabc=def\n-1=2\n3=-4\n");

            Assert.That(restored.Entries.Count(), Is.Zero, "every malformed line is skipped");
            Assert.That(restored.HighestUnlocked, Is.EqualTo(1));
        }

        [Test]
        public void PartiallyCorruptInput_KeepsTheValidLines()
        {
            PlayerProgress restored = ProgressSerializer.Deserialize("1=3\ngarbage\n2=2\n");

            Assert.That(restored.StarsFor(1), Is.EqualTo(3));
            Assert.That(restored.StarsFor(2), Is.EqualTo(2));
        }
    }

    public sealed class FileProgressRepositoryTests
    {
        [Test]
        public void SaveThenLoad_RoundtripsThroughDisk()
        {
            string path = Path.Combine(Path.GetTempPath(), $"match3-progress-{System.Guid.NewGuid():N}.sav");
            try
            {
                var repository = new FileProgressRepository(path);
                var progress = new PlayerProgress();
                progress.RecordResult(1, 2);
                progress.RecordResult(2, 3);

                repository.Save(progress);
                PlayerProgress loaded = repository.Load();

                Assert.That(loaded.StarsFor(1), Is.EqualTo(2));
                Assert.That(loaded.StarsFor(2), Is.EqualTo(3));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void MissingFile_LoadsAFreshProfile()
        {
            var repository = new FileProgressRepository(Path.Combine(Path.GetTempPath(), $"match3-none-{System.Guid.NewGuid():N}.sav"));

            Assert.That(repository.Load().HighestUnlocked, Is.EqualTo(1));
        }

        // ---- The campaign's goal (what the main menu points at) -------------------------

        private static PlayerProgress Beaten(int levels, int stars)
        {
            var progress = new PlayerProgress();
            for (int level = 1; level <= levels; level++)
                progress.RecordResult(level, stars);
            return progress;
        }

        [Test]
        public void AFreshProfile_ContinuesAtLevelOne()
        {
            CampaignGoal goal = new PlayerProgress().NextGoal(120);

            Assert.That(goal.Kind, Is.EqualTo(CampaignGoalKind.Continue));
            Assert.That(goal.Level, Is.EqualTo(1));
        }

        [Test]
        public void MidCampaign_ContinuesAtTheFirstUnbeatenLevel()
        {
            CampaignGoal goal = Beaten(47, 1).NextGoal(120);

            Assert.That(goal.Kind, Is.EqualTo(CampaignGoalKind.Continue));
            Assert.That(goal.Level, Is.EqualTo(48), "low stars behind you never outrank a new level");
        }

        [Test]
        public void BeatingTheLastLevel_TurnsTheGoalToTheEarliestMissingStars()
        {
            // This is the moment the old menu got stuck on "Continue - Level 120".
            PlayerProgress progress = Beaten(120, 3);
            var gaps = new PlayerProgress();
            for (int level = 1; level <= 120; level++)
                gaps.RecordResult(level, level == 33 || level == 90 ? 2 : 3);

            CampaignGoal goal = gaps.NextGoal(120);

            Assert.That(goal.Kind, Is.EqualTo(CampaignGoalKind.Improve));
            Assert.That(goal.Level, Is.EqualTo(33), "the earliest gap, not the latest");
            Assert.That(progress.NextGoal(120).Kind, Is.EqualTo(CampaignGoalKind.Perfect));
        }

        [Test]
        public void APerfectCampaign_SaysSo_AndStaysInsideTheCatalog()
        {
            CampaignGoal goal = Beaten(120, 3).NextGoal(120);

            Assert.That(goal.Kind, Is.EqualTo(CampaignGoalKind.Perfect));
            Assert.That(goal.Level, Is.EqualTo(120), "never level 121 - there is no such level");
        }

        [Test]
        public void AZeroStarWin_StillCountsAsBeaten_ButIsTheFirstThingToImprove()
        {
            PlayerProgress progress = Beaten(120, 3);
            var withZero = new PlayerProgress();
            for (int level = 1; level <= 120; level++)
                withZero.RecordResult(level, level == 5 ? 0 : 3);

            Assert.That(withZero.NextGoal(120).Kind, Is.EqualTo(CampaignGoalKind.Improve));
            Assert.That(withZero.NextGoal(120).Level, Is.EqualTo(5));
            Assert.That(progress.NextGoal(120).Level, Is.EqualTo(120));
        }

        [Test]
        public void AnEmptyCatalog_FallsBackToLevelOne()
        {
            Assert.That(Beaten(3, 3).NextGoal(0).Level, Is.EqualTo(1));
        }
    }

    public sealed class LevelCurveTests
    {
        [Test]
        public void EveryCampaignLevel_IsWellFormed([Range(1, 20)] int level)
        {
            LevelParameters parameters = LevelCurve.For(level);

            Assert.That(parameters.MovesLimit, Is.GreaterThanOrEqualTo(10));
            Assert.That(parameters.ColorCount, Is.InRange(3, 5));
            Assert.That(parameters.Objectives, Is.Not.Empty);
            foreach (Objective objective in parameters.Objectives)
            {
                Assert.That(objective.TargetAmount, Is.Positive);
                if (objective.Type == ObjectiveType.CollectColor)
                    Assert.That(objective.ColorIndex, Is.InRange(0, parameters.ColorCount - 1));
            }
            Assert.That(parameters.StarScores, Is.Ordered.Ascending);
            Assert.That(parameters.StarScores, Has.Count.EqualTo(3));
        }
    }
}
