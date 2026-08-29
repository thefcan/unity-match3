using System;
using System.Collections.Generic;
using Match3.Core;
using NUnit.Framework;
using static Match3.Tests.TestColors;

namespace Match3.Tests
{
    /// <summary>
    /// Day-2 economy: the star chest math, the Candy Town stage curve, streak
    /// shields, and the deterministic daily/weekly mission catalog with its
    /// step-counting rules.
    /// </summary>
    public sealed class EconomyTests
    {
        // ---- Star chest ------------------------------------------------------------

        [Test]
        public void RepeatingChests_CountWindowCrossings()
        {
            Assert.AreEqual(2, StarChest.RepeatingChestsEarned(45, 0));
            Assert.AreEqual(0, StarChest.RepeatingChestsEarned(45, 40));
            Assert.AreEqual(1, StarChest.RepeatingChestsEarned(60, 45));
            Assert.AreEqual(0, StarChest.RepeatingChestsEarned(19, 0));
        }

        [Test]
        public void Milestones_CountOnlyNewlyCrossedOnes()
        {
            Assert.AreEqual(1, StarChest.MilestonesCrossed(60, 59));
            Assert.AreEqual(2, StarChest.MilestonesCrossed(130, 50));
            Assert.AreEqual(0, StarChest.MilestonesCrossed(59, 0));
            Assert.AreEqual(1, StarChest.MilestonesCrossed(300, 240));
            // Chapter 6 pushed the campaign to 120 levels = 360 stars; the last
            // milestone must exist or the final 60 stars pay nothing.
            Assert.AreEqual(1, StarChest.MilestonesCrossed(360, 300));
            Assert.AreEqual(LevelCurve.LevelCount * 3, StarChest.Milestones[StarChest.Milestones.Length - 1]);
        }

        [Test]
        public void Collect_SumsRepeatingAndMilestoneRewards()
        {
            // 55 -> 60 stars: repeating chest #3 (2/1/0/0) + the 60-star milestone (2/2/2/1).
            ChestReward reward = StarChest.Collect(60, 55, out int opened);
            Assert.AreEqual(2, opened);
            Assert.AreEqual(4, reward.Hammers);
            Assert.AreEqual(3, reward.FreeSwaps);
            Assert.AreEqual(2, reward.Shuffles);
            Assert.AreEqual(1, reward.StreakShields);
        }

        [Test]
        public void Collect_NothingNew_PaysNothing()
        {
            ChestReward reward = StarChest.Collect(45, 45, out int opened);
            Assert.AreEqual(0, opened);
            Assert.AreEqual(0, reward.Hammers + reward.FreeSwaps + reward.Shuffles + reward.StreakShields);
        }

        [Test]
        public void ChestProgress_IsStarsIntoTheCurrentWindow()
        {
            Assert.AreEqual(5, StarChest.StarsTowardNextChest(45));
            Assert.AreEqual(0, StarChest.StarsTowardNextChest(40));
        }

        // ---- Candy Town -------------------------------------------------------------

        [Test]
        public void TownStages_FollowTheTwelveStarCurve()
        {
            Assert.AreEqual(0, TownRules.StageFor(0));
            Assert.AreEqual(0, TownRules.StageFor(11));
            Assert.AreEqual(1, TownRules.StageFor(12));
            Assert.AreEqual(4, TownRules.StageFor(59));
            Assert.AreEqual(5, TownRules.StageFor(60));
            Assert.AreEqual(5, TownRules.StageFor(200));
        }

        [Test]
        public void TownStages_ReportStarsToTheNextStage()
        {
            Assert.AreEqual(12, TownRules.StarsToNextStage(0));
            Assert.AreEqual(12, TownRules.StarsToNextStage(12));
            Assert.AreEqual(1, TownRules.StarsToNextStage(59));
            Assert.AreEqual(0, TownRules.StarsToNextStage(60));
        }

        [Test]
        public void TownStages_GlobalIndexSpansChapters()
        {
            Assert.AreEqual(12, TownRules.GlobalStage(2, 30));
            Assert.AreEqual(0, TownRules.GlobalStage(0, 0));
        }

        // ---- Streak shields -----------------------------------------------------------

        [Test]
        public void Shield_AbsorbsOneFail()
        {
            var state = new MetaState { WinStreak = 4, StreakShields = 1 };
            WinStreakRules.RegisterStart(state);
            WinStreakRules.RegisterOutcome(state, won: false);

            Assert.AreEqual(4, state.WinStreak, "the shield ate the break");
            Assert.AreEqual(0, state.StreakShields);

            WinStreakRules.RegisterStart(state);
            WinStreakRules.RegisterOutcome(state, won: false);
            Assert.AreEqual(0, state.WinStreak, "no shield left — the streak resets");
        }

        [Test]
        public void Shield_AbsorbsAnAbandonToo()
        {
            var state = new MetaState { WinStreak = 2, StreakShields = 1 };
            WinStreakRules.RegisterStart(state);
            WinStreakRules.RegisterStart(state); // abandoned mid-run

            Assert.AreEqual(2, state.WinStreak);
            Assert.AreEqual(0, state.StreakShields);
        }

        // ---- Missions -------------------------------------------------------------------

        [Test]
        public void DailyMissions_AreDeterministicAndDistinct()
        {
            MissionDef[] first = MissionCatalog.DailyFor(731);
            MissionDef[] second = MissionCatalog.DailyFor(731);

            Assert.AreEqual(MissionCatalog.DailyCount, first.Length);
            var types = new HashSet<MissionType>();
            for (int i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(first[i].Type, second[i].Type);
                Assert.AreEqual(first[i].Param, second[i].Param);
                Assert.AreEqual(first[i].Target, second[i].Target);
                Assert.IsTrue(types.Add(first[i].Type), "types within a day never repeat");
            }
        }

        [Test]
        public void Reroll_NeverDuplicatesTheDay()
        {
            for (int day = 100; day < 130; day++)
            {
                MissionDef[] daily = MissionCatalog.DailyFor(day);
                MissionDef reroll = MissionCatalog.RerollFor(day, 1);
                foreach (MissionDef def in daily)
                    Assert.AreNotEqual(def.Type, reroll.Type);
            }
        }

        [Test]
        public void WeeklyMissions_CycleThreeShapes()
        {
            Assert.AreEqual(MissionType.WinLevels, MissionCatalog.WeeklyFor(0).Type);
            Assert.AreEqual(MissionType.ClearColor, MissionCatalog.WeeklyFor(1).Type);
            Assert.AreEqual(MissionType.MakeStriped, MissionCatalog.WeeklyFor(2).Type);
            Assert.AreEqual(MissionType.WinLevels, MissionCatalog.WeeklyFor(3).Type);
        }

        [Test]
        public void CountFor_ReadsTheStepsCorrectly()
        {
            TileFactory factory = TestFactories.Scripted(5);
            Board board = Board.FromLayout(new[,]
            {
                { A, B, C },
                { B, C, A },
                { C, A, B },
            }, factory);

            Tile a1 = board[new GridPosition(0, 2)].Value; // colour A (top row of the layout)
            Tile a2 = board[new GridPosition(2, 1)].Value; // colour A
            Tile b1 = board[new GridPosition(1, 2)].Value; // colour B
            Tile chocolate = factory.CreateChocolate();
            Tile striped = factory.CreateSpecial(A, TileKind.StripedH);

            var step = new CascadeStep(0,
                new List<ClearedTile>
                {
                    new ClearedTile(a1, new GridPosition(0, 2)),
                    new ClearedTile(a2, new GridPosition(2, 1)),
                    new ClearedTile(b1, new GridPosition(1, 2)),
                    new ClearedTile(chocolate, new GridPosition(0, 0)),
                },
                Array.Empty<TileFall>(), Array.Empty<TileSpawn>(), 0, Array.Empty<int>(),
                new List<SpecialCreation> { new SpecialCreation(striped, a1, new GridPosition(0, 2), new[] { new GridPosition(0, 2) }) },
                new List<Detonation> { new Detonation(striped, new GridPosition(0, 2), DetonationKind.Cross, new List<GridPosition>()) },
                new List<JellyHit> { new JellyHit(new GridPosition(0, 0), 0), new JellyHit(new GridPosition(1, 0), 1) },
                Array.Empty<LockBreak>(), Array.Empty<ChocolateSpread>(),
                new List<IngredientExit> { new IngredientExit(factory.CreateIngredient(), new GridPosition(1, 0)) });

            Assert.AreEqual(2, MissionCatalog.CountFor(new MissionDef(MissionType.ClearColor, A, 60), step));
            Assert.AreEqual(1, MissionCatalog.CountFor(new MissionDef(MissionType.MakeStriped, 0, 6), step));
            Assert.AreEqual(0, MissionCatalog.CountFor(new MissionDef(MissionType.MakeWrapped, 0, 4), step));
            Assert.AreEqual(1, MissionCatalog.CountFor(new MissionDef(MissionType.MakeCombo, 0, 3), step));
            Assert.AreEqual(2, MissionCatalog.CountFor(new MissionDef(MissionType.ClearJelly, 0, 12), step));
            Assert.AreEqual(1, MissionCatalog.CountFor(new MissionDef(MissionType.ClearChocolate, 0, 5), step));
            Assert.AreEqual(1, MissionCatalog.CountFor(new MissionDef(MissionType.CollectIngredients, 0, 3), step));
            Assert.AreEqual(0, MissionCatalog.CountFor(new MissionDef(MissionType.WinLevels, 0, 3), step));
        }

        [Test]
        public void Serializer_RoundtripsTheEconomyFields()
        {
            var state = new MetaState
            {
                StreakShields = 2,
                LastChestStars = 45,
                LastSeenTownStage = 7,
                MissionDay = 900,
                RerolledSlot = 1,
                MissionWeek = 128,
                WeeklyProgress = 9,
                WeeklyClaimed = true,
            };
            state.MissionProgress[1] = 7;
            state.MissionClaimed[2] = true;

            MetaState restored = MetaSerializer.Deserialize(MetaSerializer.Serialize(state));

            Assert.AreEqual(2, restored.StreakShields);
            Assert.AreEqual(45, restored.LastChestStars);
            Assert.AreEqual(7, restored.LastSeenTownStage);
            Assert.AreEqual(900, restored.MissionDay);
            Assert.AreEqual(7, restored.MissionProgress[1]);
            Assert.IsTrue(restored.MissionClaimed[2]);
            Assert.AreEqual(1, restored.RerolledSlot);
            Assert.AreEqual(128, restored.MissionWeek);
            Assert.AreEqual(9, restored.WeeklyProgress);
            Assert.IsTrue(restored.WeeklyClaimed);
            Assert.AreEqual(-1, new MetaState().RerolledSlot, "fresh state: reroll available");
        }
    }
}
