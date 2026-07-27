using System;
using System.Collections.Generic;
using Match3.Core;
using NUnit.Framework;
using static Match3.Tests.TestColors;

namespace Match3.Tests
{
    /// <summary>
    /// The Candy Calendar: deterministic event windows, tier and race rules, and —
    /// for the first time in this repo — real coverage of the monotonic
    /// clock-rollback freeze (the mission layer's version lives in the Game
    /// assembly and could never be tested from EditMode).
    ///
    /// Day-number anchors: the app epoch 2024-01-01 is a Monday, and day 938
    /// (2026-07-27) is one too. 939 = Tuesday (window 268), 942 = Friday
    /// (window 269), 945 = Monday, 946 = Tuesday (window 270).
    /// </summary>
    public sealed class EventTests
    {
        private const int Mon = 938;
        private const int Tue = 939;
        private const int Fri = 942;
        private const int NextMon = 945;
        private const int NextTue = 946;

        // ---- Calendar --------------------------------------------------------------

        [Test]
        public void Phase_MapsTheWeek_AndIsNegativeSafe()
        {
            Assert.AreEqual(EventPhase.Off, EventCalendar.PhaseFor(0));       // epoch Monday
            Assert.AreEqual(EventPhase.Midweek, EventCalendar.PhaseFor(1));
            Assert.AreEqual(EventPhase.Midweek, EventCalendar.PhaseFor(3));
            Assert.AreEqual(EventPhase.Weekend, EventCalendar.PhaseFor(4));
            Assert.AreEqual(EventPhase.Weekend, EventCalendar.PhaseFor(6));
            Assert.AreEqual(EventPhase.Off, EventCalendar.PhaseFor(7));
            Assert.AreEqual(EventPhase.Off, EventCalendar.PhaseFor(Mon));
            Assert.AreEqual(EventPhase.Off, EventCalendar.PhaseFor(-7));      // pre-epoch Monday
            Assert.AreEqual(EventPhase.Weekend, EventCalendar.PhaseFor(-1));  // pre-epoch Sunday
        }

        [Test]
        public void WindowId_GrowsAcrossTheCalendar_MondayIsSentinel()
        {
            Assert.AreEqual(0, EventCalendar.WindowIdFor(1));
            Assert.AreEqual(0, EventCalendar.WindowIdFor(3));
            Assert.AreEqual(1, EventCalendar.WindowIdFor(4));
            Assert.AreEqual(1, EventCalendar.WindowIdFor(6));
            Assert.AreEqual(-1, EventCalendar.WindowIdFor(7));
            Assert.AreEqual(2, EventCalendar.WindowIdFor(8));
            Assert.AreEqual(268, EventCalendar.WindowIdFor(Tue));
            Assert.AreEqual(269, EventCalendar.WindowIdFor(Fri));
            Assert.AreEqual(270, EventCalendar.WindowIdFor(NextTue));

            Assert.AreEqual(1, EventCalendar.WindowStartDay(0));
            Assert.AreEqual(4, EventCalendar.WindowStartDay(1));
            Assert.AreEqual(8, EventCalendar.WindowStartDay(2));
            Assert.AreEqual(Tue, EventCalendar.WindowStartDay(268));
            Assert.AreEqual(Fri, EventCalendar.WindowStartDay(269));
        }

        [Test]
        public void DaysLeft_CountsDownInsideEachWindow()
        {
            Assert.AreEqual(0, EventCalendar.DaysLeft(Mon));
            Assert.AreEqual(3, EventCalendar.DaysLeft(Tue));
            Assert.AreEqual(2, EventCalendar.DaysLeft(Tue + 1));
            Assert.AreEqual(1, EventCalendar.DaysLeft(Tue + 2));
            Assert.AreEqual(3, EventCalendar.DaysLeft(Fri));
            Assert.AreEqual(2, EventCalendar.DaysLeft(Fri + 1));
            Assert.AreEqual(1, EventCalendar.DaysLeft(Fri + 2));
        }

        [Test]
        public void WindowSeed_IsStablePerWindow()
        {
            Assert.AreEqual(EventCalendar.WindowSeed(268), EventCalendar.WindowSeed(268));
            Assert.AreNotEqual(EventCalendar.WindowSeed(268), EventCalendar.WindowSeed(269));
            Assert.AreNotEqual(EventCalendar.WindowSeed(268), EventCalendar.WindowSeed(270));
        }

        [Test]
        public void MidweekKinds_CycleAllFour()
        {
            Assert.AreEqual(EventKind.CandyRush, EventCalendar.KindFor(0, 100));
            Assert.AreEqual(EventKind.SpecialistWeek, EventCalendar.KindFor(2, 100));
            Assert.AreEqual(EventKind.BlockerBash, EventCalendar.KindFor(4, 100));
            Assert.AreEqual(EventKind.StarSprint, EventCalendar.KindFor(6, 100));
            Assert.AreEqual(EventKind.CandyRush, EventCalendar.KindFor(8, 100)); // cycle repeats
        }

        [Test]
        public void OddWindows_AlwaysRace()
        {
            Assert.AreEqual(EventKind.Race, EventCalendar.KindFor(1, 100));
            Assert.AreEqual(EventKind.Race, EventCalendar.KindFor(269, 1)); // regardless of campaign depth
        }

        [Test]
        public void CandyRushColour_DeterministicAndInRange()
        {
            for (int windowId = 0; windowId <= 40; windowId += 8)
            {
                int first = EventCalendar.ParamFor(windowId, EventKind.CandyRush);
                int second = EventCalendar.ParamFor(windowId, EventKind.CandyRush);
                Assert.AreEqual(first, second);
                Assert.IsTrue(first >= 0 && first <= 4);
            }
            Assert.AreEqual(0, EventCalendar.ParamFor(268, EventKind.StarSprint)); // only rush is coloured
        }

        [Test]
        public void BlockerBash_SubstitutedBeforeJellyContent()
        {
            Assert.AreEqual(EventKind.BlockerBash, EventCalendar.KindFor(4, EventCalendar.BlockerContentLevel));
            Assert.AreEqual(EventKind.CandyRush, EventCalendar.KindFor(4, EventCalendar.BlockerContentLevel - 1));
        }

        [Test]
        public void Targets_RebuildFromTheSnapshotKind()
        {
            EventDef rush = EventCalendar.DefForKind(EventKind.CandyRush, 3);
            Assert.AreEqual(3, rush.Param);
            Assert.AreEqual(70, rush.Tier1);
            Assert.AreEqual(160, rush.Tier2);
            Assert.AreEqual(280, rush.Tier3);

            Assert.AreEqual(32, EventCalendar.DefForKind(EventKind.SpecialistWeek, 0).Tier3);
            Assert.AreEqual(60, EventCalendar.DefForKind(EventKind.BlockerBash, 0).Tier3);
            Assert.AreEqual(24, EventCalendar.DefForKind(EventKind.StarSprint, 0).Tier3);
            Assert.AreEqual(EventCalendar.RaceTarget, EventCalendar.DefForKind(EventKind.Race, 0).Tier3);

            // A kind id from a newer build degrades to CandyRush, never to Race.
            var future = new MetaState { EventKindId = 99 };
            Assert.AreEqual(EventKind.CandyRush, EventRules.KindOf(future));
        }

        // ---- Step counting ---------------------------------------------------------

        private static CascadeStep BuildStep(TileFactory factory, Board board)
        {
            Tile a1 = board[new GridPosition(0, 2)].Value; // colour A
            Tile a2 = board[new GridPosition(2, 1)].Value; // colour A
            Tile b1 = board[new GridPosition(1, 2)].Value; // colour B
            Tile chocolate = factory.CreateChocolate();
            Tile striped = factory.CreateSpecial(A, TileKind.StripedH);
            Tile wrapped = factory.CreateSpecial(B, TileKind.Wrapped);
            Tile fish = factory.CreateSpecial(C, TileKind.Fish);
            Tile bomb = factory.CreateSpecial(A, TileKind.ColorBomb);

            var origin = new GridPosition(0, 2);
            return new CascadeStep(0,
                new List<ClearedTile>
                {
                    new ClearedTile(a1, new GridPosition(0, 2)),
                    new ClearedTile(a2, new GridPosition(2, 1)),
                    new ClearedTile(b1, new GridPosition(1, 2)),
                    new ClearedTile(chocolate, new GridPosition(0, 0)),
                },
                Array.Empty<TileFall>(), Array.Empty<TileSpawn>(), 0, Array.Empty<int>(),
                new List<SpecialCreation>
                {
                    new SpecialCreation(striped, a1, origin, new[] { origin }),
                    new SpecialCreation(wrapped, b1, origin, new[] { origin }),
                    new SpecialCreation(fish, a1, origin, new[] { origin }),
                    new SpecialCreation(bomb, a1, origin, new[] { origin }), // colour bombs are not "specialist" fodder
                },
                Array.Empty<Detonation>(),
                new List<JellyHit> { new JellyHit(new GridPosition(0, 0), 0), new JellyHit(new GridPosition(1, 0), 1) },
                new List<LockBreak> { new LockBreak(new GridPosition(2, 0)) },
                Array.Empty<ChocolateSpread>(), Array.Empty<IngredientExit>(),
                Array.Empty<FishStrike>(),
                new List<FrostingHit> { new FrostingHit(new GridPosition(2, 2), 1) },
                Array.Empty<BombTick>());
        }

        private static CascadeStep BuildStepOn3x3()
        {
            TileFactory factory = TestFactories.Scripted(5);
            Board board = Board.FromLayout(new[,]
            {
                { A, B, C },
                { B, C, A },
                { C, A, B },
            }, factory);
            return BuildStep(factory, board);
        }

        [Test]
        public void CountFor_CandyRush_OnlyTheSeededColour()
        {
            CascadeStep step = BuildStepOn3x3();
            Assert.AreEqual(2, EventRules.CountFor(EventKind.CandyRush, A, step));
            Assert.AreEqual(1, EventRules.CountFor(EventKind.CandyRush, B, step));
            Assert.AreEqual(0, EventRules.CountFor(EventKind.CandyRush, D, step));
        }

        [Test]
        public void CountFor_Specialist_CountsStripedWrappedFish()
        {
            CascadeStep step = BuildStepOn3x3();
            Assert.AreEqual(3, EventRules.CountFor(EventKind.SpecialistWeek, 0, step));
        }

        [Test]
        public void CountFor_BlockerBash_SumsAllBlockerLayers()
        {
            // 2 jelly + 1 frosting + 1 lock + 1 chocolate cleared = 5
            CascadeStep step = BuildStepOn3x3();
            Assert.AreEqual(5, EventRules.CountFor(EventKind.BlockerBash, 0, step));
        }

        [Test]
        public void CountFor_WinDrivenKinds_IgnoreSteps()
        {
            CascadeStep step = BuildStepOn3x3();
            Assert.AreEqual(0, EventRules.CountFor(EventKind.StarSprint, 0, step));
            Assert.AreEqual(0, EventRules.CountFor(EventKind.Race, 0, step));
        }

        [Test]
        public void ApplyStep_AdvancesAndClampsAtTierThree()
        {
            var state = new MetaState();
            EventRules.EnsureWindow(state, 953, 100); // window 272: 136 % 4 == 0 → CandyRush
            Assert.AreEqual(EventKind.CandyRush, EventRules.KindOf(state));
            state.EventParam = A; // pin the colour so the hand-built step counts

            CascadeStep step = BuildStepOn3x3();
            EventRules.ApplyStep(state, 953, step);
            Assert.AreEqual(2, state.EventProgress);

            state.EventProgress = 279;
            EventRules.ApplyStep(state, 953, step);
            Assert.AreEqual(280, state.EventProgress); // clamped at tier 3

            state.EventProgress = 5;
            EventRules.ApplyStep(state, Tue, step); // a different (inactive) window's day
            Assert.AreEqual(5, state.EventProgress);
        }

        [Test]
        public void TiersEarned_StepAtEachTarget()
        {
            EventDef def = EventCalendar.DefForKind(EventKind.CandyRush, 0);
            Assert.AreEqual(0, EventRules.TiersEarned(def, 69));
            Assert.AreEqual(1, EventRules.TiersEarned(def, 70));
            Assert.AreEqual(1, EventRules.TiersEarned(def, 159));
            Assert.AreEqual(2, EventRules.TiersEarned(def, 160));
            Assert.AreEqual(3, EventRules.TiersEarned(def, 280));
        }

        [Test]
        public void TierRewards_EscalateAndEndWithAShield()
        {
            ChestReward tier1 = EventRules.TierReward(0);
            Assert.AreEqual(1, tier1.Hammers);
            Assert.AreEqual(0, tier1.FreeSwaps);
            Assert.AreEqual(1, tier1.Shuffles);
            Assert.AreEqual(0, tier1.StreakShields);

            ChestReward tier2 = EventRules.TierReward(1);
            Assert.AreEqual(2, tier2.Hammers);
            Assert.AreEqual(1, tier2.FreeSwaps);
            Assert.AreEqual(1, tier2.Shuffles);
            Assert.AreEqual(0, tier2.StreakShields);

            ChestReward tier3 = EventRules.TierReward(2);
            Assert.AreEqual(2, tier3.Hammers);
            Assert.AreEqual(2, tier3.FreeSwaps);
            Assert.AreEqual(2, tier3.Shuffles);
            Assert.AreEqual(1, tier3.StreakShields);
        }

        // ---- Window lifecycle ------------------------------------------------------

        [Test]
        public void EnsureWindow_FirstCall_SnapshotsTheWindow()
        {
            var state = new MetaState();
            Assert.IsTrue(EventRules.EnsureWindow(state, Tue, 100));
            Assert.AreEqual(268, state.EventWindowId);
            Assert.AreEqual(EventKind.BlockerBash, EventRules.KindOf(state)); // 134 % 4 == 2
            Assert.AreEqual(0, state.EventProgress);
            Assert.IsTrue(EventRules.IsWindowActive(state, Tue));
            Assert.IsFalse(EventRules.IsWindowActive(state, Fri));
        }

        [Test]
        public void EnsureWindow_SameWindow_ChangesNothing()
        {
            var state = new MetaState();
            EventRules.EnsureWindow(state, Tue, 100);
            state.EventProgress = 9;
            Assert.IsFalse(EventRules.EnsureWindow(state, Tue + 1, 100)); // Wednesday, same window
            Assert.AreEqual(268, state.EventWindowId);
            Assert.AreEqual(9, state.EventProgress);
        }

        [Test]
        public void EnsureWindow_ClockPulledBack_FreezesStateUntouched()
        {
            var state = new MetaState();
            EventRules.EnsureWindow(state, Tue, 100);
            state.EventProgress = 22;

            Assert.IsFalse(EventRules.EnsureWindow(state, Tue - 7, 100));  // last week's Tuesday
            Assert.IsFalse(EventRules.EnsureWindow(state, -30, 100));      // pre-epoch clock
            Assert.AreEqual(268, state.EventWindowId);
            Assert.AreEqual(22, state.EventProgress);
            Assert.AreEqual(3, state.Hammers); // nothing banked
            Assert.IsFalse(EventRules.IsWindowActive(state, Tue - 7));     // frozen = invisible, not wrong
        }

        [Test]
        public void EnsureWindow_Rollover_BanksUnclaimedEarnedTiersOnly()
        {
            var state = new MetaState();
            EventRules.EnsureWindow(state, Tue, 100);       // BlockerBash: tiers 15/35/60
            state.EventProgress = 35;                        // tiers 1 and 2 earned
            state.EventTierClaimed[0] = true;                // tier 1 already claimed in-window

            Assert.IsTrue(EventRules.EnsureWindow(state, Fri, 100));
            // Only tier 2 banked: (2,1,1,0) on top of the 3/3/3 starter pack.
            Assert.AreEqual(5, state.Hammers);
            Assert.AreEqual(4, state.FreeSwaps);
            Assert.AreEqual(4, state.Shuffles);
            Assert.AreEqual(0, state.StreakShields);
            Assert.AreEqual(2, state.EventToastHammers);
            Assert.AreEqual(0, state.EventToastTrophy);
            // And the new window is a clean weekend race.
            Assert.AreEqual(269, state.EventWindowId);
            Assert.AreEqual(EventKind.Race, EventRules.KindOf(state));
            Assert.AreEqual(0, state.EventProgress);
            Assert.IsFalse(state.EventTierClaimed[0]);
        }

        [Test]
        public void EnsureWindow_ManyMissedWindows_BanksOnlyTheStoredOne()
        {
            var state = new MetaState();
            EventRules.EnsureWindow(state, Tue, 100);        // BlockerBash
            state.EventProgress = 60;                         // all three tiers earned, none claimed

            Assert.IsTrue(EventRules.EnsureWindow(state, 960, 100)); // three weeks later
            // One window's full ladder: (1,0,1,0)+(2,1,1,0)+(2,2,2,1) = (5,3,4,1) + bronze.
            Assert.AreEqual(8, state.Hammers);
            Assert.AreEqual(6, state.FreeSwaps);
            Assert.AreEqual(7, state.Shuffles);
            Assert.AreEqual(1, state.StreakShields);
            Assert.AreEqual(1, state.TrophyBronze);
            Assert.AreEqual(1, state.EventToastTrophy);
            Assert.AreEqual(274, state.EventWindowId);       // day 960 = Tuesday of week 137
        }

        [Test]
        public void EnsureWindow_ForwardBackForward_NeverBanksTwice()
        {
            var state = new MetaState();
            EventRules.EnsureWindow(state, Tue, 100);
            state.EventProgress = 35;
            EventRules.EnsureWindow(state, Fri, 100);        // banks tiers 1+2 = (3,1,2,0)
            int hammers = state.Hammers;
            int toastHammers = state.EventToastHammers;

            Assert.IsFalse(EventRules.EnsureWindow(state, Tue, 100)); // clock back into the old window
            Assert.IsFalse(EventRules.EnsureWindow(state, Fri, 100)); // and forward again
            Assert.AreEqual(hammers, state.Hammers);
            Assert.AreEqual(toastHammers, state.EventToastHammers);
            Assert.AreEqual(269, state.EventWindowId);
        }

        [Test]
        public void EnsureWindow_MondayIsIdle_TuesdayBanksTheWeekend()
        {
            var state = new MetaState();
            EventRules.EnsureWindow(state, Fri, 100);        // weekend race, window 269
            for (int level = 1; level <= 4; level++)
                EventRules.RegisterWin(state, Fri, level, 3);
            Assert.AreEqual(4, state.EventProgress);

            Assert.IsFalse(EventRules.EnsureWindow(state, NextMon, 100)); // Monday: sentinel, idle
            Assert.AreEqual(269, state.EventWindowId);

            Assert.IsTrue(EventRules.EnsureWindow(state, NextTue, 100));
            // 4 ticks always earn at least the participation nod (1,0,1,0).
            Assert.IsTrue(state.Hammers > 3);
            Assert.IsTrue(state.Shuffles > 3);
            Assert.IsTrue(state.EventToastHammers > 0);
            Assert.AreEqual(270, state.EventWindowId);
            Assert.AreEqual(EventKind.StarSprint, EventRules.KindOf(state)); // 135 % 4 == 3
            Assert.AreEqual(0, state.EventProgress);
            Assert.AreEqual(0, state.EventRaceLevels[0]);
        }

        [Test]
        public void TryClaimTier_GatesAndPaysBronzeOnCompletion()
        {
            var state = new MetaState();
            EventRules.EnsureWindow(state, 953, 100);        // CandyRush window 272
            state.EventProgress = 280;

            Assert.IsTrue(EventRules.TryClaimTier(state, 953, 0, out ChestReward first));
            Assert.AreEqual(1, first.Hammers);
            Assert.IsFalse(EventRules.TryClaimTier(state, 953, 0, out ChestReward none)); // no double claim
            Assert.IsTrue(EventRules.TryClaimTier(state, 953, 2, out ChestReward last));
            Assert.AreEqual(1, last.StreakShields);
            Assert.AreEqual(1, state.TrophyBronze);          // tier 3 mints the bronze
            Assert.IsFalse(EventRules.TryClaimTier(state, 953, 3, out none)); // out of range
            Assert.IsFalse(EventRules.TryClaimTier(state, Tue, 1, out none)); // different window's day
        }

        [Test]
        public void BankedToast_TakenOnceThenEmpty()
        {
            var state = new MetaState();
            EventRules.EnsureWindow(state, Tue, 100);
            state.EventProgress = 60;
            EventRules.EnsureWindow(state, Fri, 100);        // banks the full ladder + bronze

            Assert.IsTrue(EventRules.TryTakeBankedToast(state, out ChestReward reward, out int trophy));
            Assert.AreEqual(5, reward.Hammers);
            Assert.AreEqual(1, reward.StreakShields);
            Assert.AreEqual(1, trophy);
            Assert.IsFalse(EventRules.TryTakeBankedToast(state, out ChestReward drained, out int drainedTrophy)); // one-shot
            Assert.AreEqual(0, state.EventToastHammers);
        }

        // ---- Weekend race ----------------------------------------------------------

        [Test]
        public void BotProgress_DeterministicMonotoneAndStepBounded()
        {
            for (uint seed = 1; seed <= 40; seed++)
            {
                for (int bot = 0; bot < EventCalendar.BotCount; bot++)
                {
                    Assert.AreEqual(0, EventRules.BotProgress(seed, bot, 0));
                    int previous = 0;
                    for (int ticks = 1; ticks <= 12; ticks++)
                    {
                        int increment = EventRules.BotIncrement(seed, bot, ticks);
                        Assert.IsTrue(increment >= 0 && increment <= 2);

                        int progress = EventRules.BotProgress(seed, bot, ticks);
                        Assert.AreEqual(EventRules.BotProgress(seed, bot, ticks), progress);
                        Assert.AreEqual(previous + increment, progress);
                        previous = progress;
                    }
                }
            }
        }

        [Test]
        public void EffectiveRate_FrontLoadsThenFatigues()
        {
            Assert.AreEqual(55, EventRules.BotBaseRatePercent(0));
            Assert.AreEqual(95, EventRules.BotBaseRatePercent(4));

            Assert.AreEqual(90, EventRules.EffectiveRatePercent(55, 1));   // sprint
            Assert.AreEqual(100, EventRules.EffectiveRatePercent(95, 3));  // capped sprint
            Assert.AreEqual(80, EventRules.EffectiveRatePercent(80, 5));   // cruise
            Assert.AreEqual(30, EventRules.EffectiveRatePercent(55, 8));   // fatigue
            Assert.AreEqual(15, EventRules.EffectiveRatePercent(35, 10));  // fatigue floor
        }

        [Test]
        public void RacePlacement_TieRulesFavourThePlayer()
        {
            // Unfinished: only bots STRICTLY ahead outrank the player.
            bool sawTieAtFirst = false;
            bool sawStrictLead = false;
            for (uint seed = 0; seed < 2000 && (!sawTieAtFirst || !sawStrictLead); seed++)
            {
                for (int ticks = 1; ticks <= 4; ticks++)
                {
                    int ahead = 0, tied = 0;
                    for (int bot = 0; bot < EventCalendar.BotCount; bot++)
                    {
                        int progress = EventRules.BotProgress(seed, bot, ticks);
                        if (progress > ticks) ahead++;
                        else if (progress == ticks) tied++;
                    }

                    if (ahead == 0 && tied > 0 && !sawTieAtFirst)
                    {
                        Assert.AreEqual(1, EventRules.RacePlacement(seed, ticks)); // ties never demote
                        sawTieAtFirst = true;
                    }
                    if (ahead > 0 && !sawStrictLead)
                    {
                        Assert.AreEqual(1 + ahead, EventRules.RacePlacement(seed, ticks));
                        sawStrictLead = true;
                    }
                }
            }
            Assert.IsTrue(sawTieAtFirst);
            Assert.IsTrue(sawStrictLead);

            // Finished: a bot must cross on an EARLIER tick to win — reaching the
            // target on the player's finishing tick is a tie, and ties lose.
            bool sawSameTickFinishTie = false;
            for (uint seed = 0; seed < 2000 && !sawSameTickFinishTie; seed++)
            {
                bool anyCrossedEarlier = false, anyCrossedSameTick = false;
                for (int bot = 0; bot < EventCalendar.BotCount; bot++)
                {
                    int atNine = EventRules.BotProgress(seed, bot, EventCalendar.RaceTarget - 1);
                    int atTen = EventRules.BotProgress(seed, bot, EventCalendar.RaceTarget);
                    if (atNine >= EventCalendar.RaceTarget) anyCrossedEarlier = true;
                    else if (atTen >= EventCalendar.RaceTarget) anyCrossedSameTick = true;
                }
                if (!anyCrossedEarlier && anyCrossedSameTick)
                {
                    Assert.AreEqual(1, EventRules.RacePlacement(seed, EventCalendar.RaceTarget));
                    sawSameTickFinishTie = true;
                }
            }
            Assert.IsTrue(sawSameTickFinishTie);
        }

        [Test]
        public void RaceRewards_PodiumParticipationAndTrophyGates()
        {
            Assert.AreEqual(3, EventRules.RaceReward(1, 10).Hammers);
            Assert.AreEqual(1, EventRules.RaceReward(1, 10).StreakShields);
            Assert.AreEqual(2, EventRules.RaceReward(2, 10).Hammers);
            Assert.AreEqual(1, EventRules.RaceReward(3, 10).Hammers);
            Assert.AreEqual(0, EventRules.RaceReward(3, 10).StreakShields);
            Assert.AreEqual(1, EventRules.RaceReward(5, 2).Hammers);   // participation
            Assert.AreEqual(0, EventRules.RaceReward(1, 0).Hammers);   // never raced → nothing

            Assert.AreEqual(3, EventRules.RaceTrophy(1, 10));
            Assert.AreEqual(2, EventRules.RaceTrophy(2, 3));
            Assert.AreEqual(1, EventRules.RaceTrophy(3, 3));
            Assert.AreEqual(0, EventRules.RaceTrophy(4, 10));
            Assert.AreEqual(0, EventRules.RaceTrophy(1, 2));           // a fluke weekend earns no gold
        }

        [Test]
        public void RegisterWin_RaceTicksDistinctLevels_AndStarSprintAddsStars()
        {
            var race = new MetaState();
            EventRules.EnsureWindow(race, Fri, 100);
            EventRules.RegisterWin(race, Fri, 5, 3);
            EventRules.RegisterWin(race, Fri, 5, 3);         // same level again: no tick
            Assert.AreEqual(1, race.EventProgress);
            EventRules.RegisterWin(race, Fri, 6, 1);
            Assert.AreEqual(2, race.EventProgress);
            for (int level = 10; level < 30; level++)        // plenty of distinct wins…
                EventRules.RegisterWin(race, Fri, level, 2);
            Assert.AreEqual(EventCalendar.RaceTarget, race.EventProgress); // …cap at the finish line
            EventRules.RegisterWin(race, NextMon, 40, 3);    // off-day: inert
            Assert.AreEqual(EventCalendar.RaceTarget, race.EventProgress);

            Assert.IsTrue(EventRules.TryClaimRace(race, Fri, out ChestReward reward, out int placement));
            Assert.IsTrue(placement >= 1 && placement <= 1 + EventCalendar.BotCount);
            Assert.IsTrue(reward.Hammers > 0);
            int trophies = race.TrophyGold + race.TrophySilver + race.TrophyBronze;
            Assert.AreEqual(placement <= 3 ? 1 : 0, trophies); // only the podium mints one
            Assert.IsFalse(EventRules.TryClaimRace(race, Fri, out ChestReward second, out int secondPlace)); // one claim only

            var sprint = new MetaState();
            EventRules.EnsureWindow(sprint, NextTue, 100);   // window 270 → StarSprint
            EventRules.RegisterWin(sprint, NextTue, 12, 3);
            EventRules.RegisterWin(sprint, NextTue, 13, 2);
            Assert.AreEqual(5, sprint.EventProgress);
            sprint.EventProgress = 23;
            EventRules.RegisterWin(sprint, NextTue, 14, 3);
            Assert.AreEqual(24, sprint.EventProgress);       // clamped at tier 3
        }

        // ---- Serialization ---------------------------------------------------------

        [Test]
        public void Serializer_RoundtripsEventAndTrophyFields()
        {
            var state = new MetaState
            {
                EventWindowId = 269,
                EventKindId = (int)EventKind.Race,
                EventParam = 2,
                EventProgress = 7,
                EventRaceClaimed = true,
                TrophyGold = 1,
                TrophySilver = 2,
                TrophyBronze = 3,
                EventToastHammers = 4,
                EventToastFreeSwaps = 5,
                EventToastShuffles = 6,
                EventToastShields = 1,
                EventToastTrophy = 2,
            };
            state.EventTierClaimed[1] = true;
            state.EventRaceLevels[0] = 41;
            state.EventRaceLevels[9] = 88;

            MetaState restored = MetaSerializer.Deserialize(MetaSerializer.Serialize(state));
            Assert.AreEqual(269, restored.EventWindowId);
            Assert.AreEqual((int)EventKind.Race, restored.EventKindId);
            Assert.AreEqual(2, restored.EventParam);
            Assert.AreEqual(7, restored.EventProgress);
            Assert.IsFalse(restored.EventTierClaimed[0]);
            Assert.IsTrue(restored.EventTierClaimed[1]);
            Assert.IsTrue(restored.EventRaceClaimed);
            Assert.AreEqual(41, restored.EventRaceLevels[0]);
            Assert.AreEqual(0, restored.EventRaceLevels[5]);
            Assert.AreEqual(88, restored.EventRaceLevels[9]);
            Assert.AreEqual(1, restored.TrophyGold);
            Assert.AreEqual(2, restored.TrophySilver);
            Assert.AreEqual(3, restored.TrophyBronze);
            Assert.AreEqual(4, restored.EventToastHammers);
            Assert.AreEqual(5, restored.EventToastFreeSwaps);
            Assert.AreEqual(6, restored.EventToastShuffles);
            Assert.AreEqual(1, restored.EventToastShields);
            Assert.AreEqual(2, restored.EventToastTrophy);
        }

        [Test]
        public void LegacyFileWithoutEventKeys_GetsFreshEventDefaults()
        {
            MetaState state = MetaSerializer.Deserialize("streak=3\nhammers=7\nwinStreak=2\n");
            Assert.AreEqual(7, state.Hammers);               // old fields still honoured
            Assert.AreEqual(0, state.EventWindowId);
            Assert.AreEqual(0, state.EventProgress);
            Assert.IsFalse(state.EventRaceClaimed);
            Assert.AreEqual(0, state.EventRaceLevels[0]);
            Assert.AreEqual(0, state.TrophyGold + state.TrophySilver + state.TrophyBronze);

            // And the poison rule still holds: one bad event value wipes the file.
            MetaState wiped = MetaSerializer.Deserialize("hammers=7\neventProgress=ten\n");
            Assert.AreEqual(3, wiped.Hammers);               // fresh starter pack, not 7
        }
    }
}
