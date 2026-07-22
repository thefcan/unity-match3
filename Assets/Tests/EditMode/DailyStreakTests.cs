using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests
{
    public sealed class DailyStreakTests
    {
        // ---- Evaluate -----------------------------------------------------------

        [Test]
        public void FreshPlayerCanClaim()
        {
            Assert.AreEqual(StreakStatus.Claimable, DailyStreak.Evaluate(0, 100, 0));
        }

        [Test]
        public void SameDayIsAlreadyClaimed()
        {
            Assert.AreEqual(StreakStatus.AlreadyClaimed, DailyStreak.Evaluate(100, 100, 3));
        }

        [Test]
        public void NextDayContinuesTheStreak()
        {
            Assert.AreEqual(StreakStatus.Claimable, DailyStreak.Evaluate(100, 101, 3));
        }

        [Test]
        public void MissedDayBreaksTheStreak()
        {
            Assert.AreEqual(StreakStatus.Broken, DailyStreak.Evaluate(100, 102, 3));
            Assert.AreEqual(StreakStatus.Broken, DailyStreak.Evaluate(100, 250, 3));
        }

        [Test]
        public void ClockRollbackCountsAsAlreadyClaimed()
        {
            // Setting the device clock backwards must never farm extra claims.
            Assert.AreEqual(StreakStatus.AlreadyClaimed, DailyStreak.Evaluate(100, 99, 3));
        }

        // ---- RewardFor ----------------------------------------------------------

        [Test]
        public void RewardLadderMatchesTheDesign()
        {
            StreakReward day1 = DailyStreak.RewardFor(1);
            Assert.AreEqual(StreakRewardKind.ExtraMoves, day1.Kind);
            Assert.AreEqual(2, day1.Amount);

            Assert.AreEqual(StreakRewardKind.StartStriped, DailyStreak.RewardFor(3).Kind);
            Assert.AreEqual(StreakRewardKind.StartWrapped, DailyStreak.RewardFor(5).Kind);
            Assert.AreEqual(StreakRewardKind.StartColorBomb, DailyStreak.RewardFor(7).Kind);
        }

        [Test]
        public void RewardCycleRepeatsEverySevenDays()
        {
            for (int day = 1; day <= 7; day++)
            {
                StreakReward a = DailyStreak.RewardFor(day);
                StreakReward b = DailyStreak.RewardFor(day + 7);
                Assert.AreEqual(a.Kind, b.Kind, $"day {day}");
                Assert.AreEqual(a.Amount, b.Amount, $"day {day}");
            }
        }

        // ---- MetaState + serializer ----------------------------------------------

        [Test]
        public void MetaRoundTripsThroughSerializer()
        {
            var state = new MetaState { LastClaimDay = 812, Streak = 12 };
            state.SetPendingReward(new StreakReward(StreakRewardKind.StartWrapped, 1));

            MetaState restored = MetaSerializer.Deserialize(MetaSerializer.Serialize(state));

            Assert.AreEqual(812, restored.LastClaimDay);
            Assert.AreEqual(12, restored.Streak);
            Assert.AreEqual(StreakRewardKind.StartWrapped, restored.PendingKind);
            Assert.AreEqual(1, restored.PendingAmount);
            Assert.IsTrue(restored.HasPendingReward);
        }

        [Test]
        public void CorruptMetaFallsBackToFreshState()
        {
            MetaState restored = MetaSerializer.Deserialize("streak=twelve\nlastClaimDay=5");
            Assert.AreEqual(0, restored.Streak);
            Assert.AreEqual(0, restored.LastClaimDay);
            Assert.IsFalse(restored.HasPendingReward);
        }

        [Test]
        public void UnknownKeysAreIgnored()
        {
            MetaState restored = MetaSerializer.Deserialize("futureKey=9\nstreak=4\nlastClaimDay=200");
            Assert.AreEqual(4, restored.Streak);
            Assert.AreEqual(200, restored.LastClaimDay);
        }

        [Test]
        public void TakePendingRewardClearsIt()
        {
            var state = new MetaState();
            state.SetPendingReward(new StreakReward(StreakRewardKind.ExtraMoves, 5));

            StreakReward taken = state.TakePendingReward();

            Assert.AreEqual(5, taken.Amount);
            Assert.IsFalse(state.HasPendingReward);
        }
    }
}
