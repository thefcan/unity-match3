using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests
{
    public sealed class ProgressMergerTests
    {
        [Test]
        public void Merge_TakesTheMaxStarsPerLevel()
        {
            var local = new PlayerProgress();
            local.RecordResult(1, 3);
            local.RecordResult(2, 1);

            var cloud = new PlayerProgress();
            cloud.RecordResult(2, 2);
            cloud.RecordResult(3, 1);

            PlayerProgress merged = ProgressMerger.Merge(local, cloud);

            Assert.That(merged.StarsFor(1), Is.EqualTo(3));
            Assert.That(merged.StarsFor(2), Is.EqualTo(2), "max wins on conflicts");
            Assert.That(merged.StarsFor(3), Is.EqualTo(1));
            Assert.That(merged.HighestUnlocked, Is.EqualTo(4));
        }

        [Test]
        public void Merge_IsOrderIndependentAndIdempotent()
        {
            var a = new PlayerProgress();
            a.RecordResult(1, 2);
            var b = new PlayerProgress();
            b.RecordResult(1, 3);
            b.RecordResult(2, 1);

            PlayerProgress ab = ProgressMerger.Merge(a, b);
            PlayerProgress ba = ProgressMerger.Merge(b, a);
            PlayerProgress again = ProgressMerger.Merge(ab, ba);

            Assert.That(ProgressMerger.AreEquivalent(ab, ba), Is.True);
            Assert.That(ProgressMerger.AreEquivalent(ab, again), Is.True, "merging merges changes nothing");
        }

        [Test]
        public void Merge_HandlesNullAndEmptySides()
        {
            var local = new PlayerProgress();
            local.RecordResult(5, 2);

            PlayerProgress fromNull = ProgressMerger.Merge(local, null);
            Assert.That(fromNull.StarsFor(5), Is.EqualTo(2));

            PlayerProgress bothEmpty = ProgressMerger.Merge(new PlayerProgress(), new PlayerProgress());
            Assert.That(bothEmpty.HighestUnlocked, Is.EqualTo(1));
        }

        [Test]
        public void AreEquivalent_SpotsDifferences()
        {
            var a = new PlayerProgress();
            a.RecordResult(1, 2);
            var b = new PlayerProgress();
            b.RecordResult(1, 2);

            Assert.That(ProgressMerger.AreEquivalent(a, b), Is.True);
            b.RecordResult(2, 0);
            Assert.That(ProgressMerger.AreEquivalent(a, b), Is.False);
        }

        [Test]
        public void ScoreBounds_MatchTheCloudCodeScript()
        {
            // cloudcode/submit_score.js hard-codes the same numbers — if you change
            // these, change the script (and redeploy it) in the same commit.
            Assert.That(ScoreBounds.MaxPlausiblePointsPerSecond, Is.EqualTo(400));
            Assert.That(ScoreBounds.MinRunSeconds, Is.EqualTo(5));
        }
    }
}
