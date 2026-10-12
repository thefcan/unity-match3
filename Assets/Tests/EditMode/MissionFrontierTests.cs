using System.Collections.Generic;
using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests
{
    /// <summary>
    /// Daily missions follow the player. A mission asking for content the player has
    /// not unlocked is not a hard mission, it is a dead slot — and before the frontier
    /// filter a quarter of every roll was jelly or chocolate regardless of whether the
    /// player had ever seen either.
    /// </summary>
    public sealed class MissionFrontierTests
    {
        private const int Days = 1500; // ~4 years of dailies

        private static bool Has(MissionDef[] missions, MissionType type)
        {
            foreach (MissionDef mission in missions)
                if (mission.Type == type)
                    return true;
            return false;
        }

        /// <summary>The first frontier at which a type becomes reachable (levels are 1-based).</summary>
        private static int DebutOf(MissionType type)
        {
            for (int frontier = 1; frontier <= LevelCurve.LevelCount + 1; frontier++)
                if (MissionCatalog.IsReachable(type, frontier))
                    return frontier;
            return int.MaxValue;
        }

        [Test]
        public void AnUnknownFrontier_ChangesNothing()
        {
            // 0 is what a save from before the snapshot carries. A player who updates
            // mid-day must keep the exact missions their stored slot progress belongs to.
            for (int day = 1; day <= Days; day++)
            {
                MissionDef[] before = MissionCatalog.DailyFor(day);
                MissionDef[] legacy = MissionCatalog.DailyFor(day, 0);
                for (int slot = 0; slot < MissionCatalog.DailyCount; slot++)
                {
                    Assert.That(legacy[slot].Type, Is.EqualTo(before[slot].Type), $"day {day} slot {slot}");
                    Assert.That(legacy[slot].Param, Is.EqualTo(before[slot].Param));
                    Assert.That(legacy[slot].Target, Is.EqualTo(before[slot].Target));
                }
            }
        }

        [Test]
        public void APlayerWithEverythingUnlocked_GetsTheSameMissionsAsAlways()
        {
            // Unreachable rolls are SKIPPED, not remapped, so for a veteran the sequence
            // of draws is untouched — byte-identical output, not merely similar.
            int veteran = LevelCurve.LevelCount + 1;
            for (int day = 1; day <= Days; day++)
            {
                MissionDef[] before = MissionCatalog.DailyFor(day);
                MissionDef[] after = MissionCatalog.DailyFor(day, veteran);
                for (int slot = 0; slot < MissionCatalog.DailyCount; slot++)
                {
                    Assert.That(after[slot].Type, Is.EqualTo(before[slot].Type), $"day {day} slot {slot}");
                    Assert.That(after[slot].Param, Is.EqualTo(before[slot].Param));
                    Assert.That(after[slot].Target, Is.EqualTo(before[slot].Target));
                }
                Assert.That(MissionCatalog.RerollFor(day, 1, veteran).Type,
                            Is.EqualTo(MissionCatalog.RerollFor(day, 1).Type), $"day {day} reroll");
            }
        }

        [Test]
        public void ABeginner_IsNeverAskedForJellyOrChocolate()
        {
            for (int day = 1; day <= Days; day++)
            {
                MissionDef[] missions = MissionCatalog.DailyFor(day, 1);
                Assert.That(Has(missions, MissionType.ClearJelly), Is.False, $"day {day}: jelly at level 1");
                Assert.That(Has(missions, MissionType.ClearChocolate), Is.False, $"day {day}: chocolate at level 1");

                for (int slot = 0; slot < MissionCatalog.DailyCount; slot++)
                {
                    MissionType reroll = MissionCatalog.RerollFor(day, slot, 1).Type;
                    Assert.That(reroll, Is.Not.EqualTo(MissionType.ClearJelly), $"day {day} reroll {slot}");
                    Assert.That(reroll, Is.Not.EqualTo(MissionType.ClearChocolate), $"day {day} reroll {slot}");
                }
            }
        }

        [Test]
        public void TheDaysMissions_AreStillThreeDistinctTypes_AtEveryFrontier()
        {
            foreach (int frontier in new[] { 1, 7, 8, 40, 65, 66, 100, LevelCurve.LevelCount + 1 })
            {
                for (int day = 1; day <= 200; day++)
                {
                    MissionDef[] missions = MissionCatalog.DailyFor(day, frontier);
                    var types = new HashSet<MissionType>();
                    foreach (MissionDef mission in missions)
                        types.Add(mission.Type);
                    Assert.That(types.Count, Is.EqualTo(MissionCatalog.DailyCount),
                                $"frontier {frontier} day {day}");

                    MissionType reroll = MissionCatalog.RerollFor(day, 0, frontier).Type;
                    Assert.That(types.Contains(reroll), Is.False,
                                $"frontier {frontier} day {day}: the reroll duplicated a slot");
                }
            }
        }

        [Test]
        public void AMechanic_JoinsTheRollExactlyWhenItsFirstLevelUnlocks()
        {
            foreach (MissionType type in new[] { MissionType.ClearJelly, MissionType.ClearChocolate })
            {
                int debut = DebutOf(type);
                Assert.That(debut, Is.GreaterThan(1), $"{type} should be gated behind real progress");
                Assert.That(debut, Is.LessThanOrEqualTo(LevelCurve.LevelCount), $"{type} never appears in the campaign");

                bool seenBefore = false, seenAt = false;
                for (int day = 1; day <= Days; day++)
                {
                    seenBefore |= Has(MissionCatalog.DailyFor(day, debut - 1), type);
                    seenAt |= Has(MissionCatalog.DailyFor(day, debut), type);
                }
                Assert.That(seenBefore, Is.False, $"{type} was offered one level before it exists");
                Assert.That(seenAt, Is.True, $"{type} never showed up once it was reachable");
            }
        }

        [Test]
        public void TheGate_MatchesWhereTheCampaignActuallyIntroducesIt()
        {
            // The thresholds are derived from LevelCurve; this re-derives them
            // independently so a silent change in either place shows up.
            int firstJelly = 0, firstChocolate = 0;
            for (int level = LevelCurve.LevelCount; level >= 1; level--)
            {
                LevelParameters p = LevelCurve.For(level);
                if (p.JellyRows > 0) firstJelly = level;
                if (p.ChocolateCells.Count > 0 || p.FountainCells.Count > 0) firstChocolate = level;
            }

            Assert.That(DebutOf(MissionType.ClearJelly), Is.EqualTo(firstJelly));
            Assert.That(DebutOf(MissionType.ClearChocolate), Is.EqualTo(firstChocolate));
        }

        [Test]
        public void EnoughTypesStayReachable_ForTheRollToTerminate()
        {
            // DailyFor needs DailyCount distinct types and RerollFor needs one more that
            // is none of them. Fewer reachable types than that and those loops spin
            // forever — a hang, not a failure — so the margin is pinned here, for every
            // frontier, against the roll table itself.
            for (int frontier = 1; frontier <= LevelCurve.LevelCount + 1; frontier++)
            {
                int reachable = 0;
                foreach (MissionType type in MissionCatalog.RollableTypes)
                    if (MissionCatalog.IsReachable(type, frontier))
                        reachable++;

                Assert.That(reachable, Is.GreaterThanOrEqualTo(MissionCatalog.DailyCount + 1),
                            $"frontier {frontier} leaves only {reachable} rollable mission types");
            }
        }
    }
}
