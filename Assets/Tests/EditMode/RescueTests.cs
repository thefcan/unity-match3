using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests
{
    /// <summary>
    /// The fail-panel Rescue economy: the starter pack, the three mint sources
    /// (star-chest milestones, the weekly mission's +1 lives in the Game layer,
    /// race podiums), bomb-fuse re-arming, and the structural-abandon semantics
    /// the deferred streak break now leans on.
    /// </summary>
    public sealed class RescueTests
    {
        private const int Fri = 942; // window 269 = weekend race (see EventTests anchors)

        // ---- Inventory & serialization ---------------------------------------------

        [Test]
        public void FreshState_ShipsWithTwoRescues()
        {
            Assert.AreEqual(2, new MetaState().Rescues);
        }

        [Test]
        public void Serializer_RoundtripsRescueFields()
        {
            var state = new MetaState { Rescues = 5, EventToastRescues = 1 };
            MetaState restored = MetaSerializer.Deserialize(MetaSerializer.Serialize(state));
            Assert.AreEqual(5, restored.Rescues);
            Assert.AreEqual(1, restored.EventToastRescues);
        }

        [Test]
        public void LegacyFileWithoutRescueKeys_GainsTheStarterPair()
        {
            MetaState state = MetaSerializer.Deserialize("streak=3\nhammers=7\n");
            Assert.AreEqual(2, state.Rescues); // everyone gets the gift once
            Assert.AreEqual(0, state.EventToastRescues);
        }

        [Test]
        public void CorruptRescueValue_WipesTheWholeFile()
        {
            MetaState state = MetaSerializer.Deserialize("hammers=9\nrescues=two\n");
            Assert.AreEqual(3, state.Hammers); // fresh starter pack, not 9
            Assert.AreEqual(2, state.Rescues);
        }

        // ---- Race podium mints -----------------------------------------------------

        [Test]
        public void RescueFor_TopTwoWithARealRun_MintsOne()
        {
            Assert.AreEqual(1, EventRules.RescueFor(1, 10));
            Assert.AreEqual(1, EventRules.RescueFor(2, 3));
            Assert.AreEqual(0, EventRules.RescueFor(3, 10)); // bronze pays boosters, not continues
            Assert.AreEqual(0, EventRules.RescueFor(1, 2));  // the trophy gate mirrored
        }

        [Test]
        public void ClaimedRace_MintsTheRescueWithThePodium()
        {
            var state = new MetaState();
            EventRules.EnsureWindow(state, Fri, 100);
            for (int level = 1; level <= 20; level++)
                EventRules.RegisterWin(state, Fri, level, 2); // finish the race

            Assert.IsTrue(EventRules.TryClaimRace(state, Fri, out _, out int placement));
            int expected = placement <= 2 ? 1 : 0;
            Assert.AreEqual(2 + expected, state.Rescues); // starter 2 + podium mint
        }

        [Test]
        public void BankedRace_BanksTheRescueAndToastsIt()
        {
            var state = new MetaState();
            EventRules.EnsureWindow(state, Fri, 100);
            for (int level = 1; level <= 20; level++)
                EventRules.RegisterWin(state, Fri, level, 2); // finished but never claimed

            uint seed = EventCalendar.WindowSeed(state.EventWindowId);
            int placement = EventRules.RacePlacement(seed, state.EventProgress);
            int expected = EventRules.RescueFor(placement, state.EventProgress);

            Assert.IsTrue(EventRules.EnsureWindow(state, 946, 100)); // next Tuesday banks it
            Assert.AreEqual(2 + expected, state.Rescues);
            Assert.AreEqual(expected, state.EventToastRescues);

            Assert.IsTrue(EventRules.TryTakeBankedToast(state, out _, out _, out int rescues));
            Assert.AreEqual(expected, rescues);
            Assert.AreEqual(0, state.EventToastRescues);
        }

        // ---- Bomb fuse re-arming (the rescue's board repair) -----------------------

        [Test]
        public void RearmedZeroFuse_ReadsFullAndSkipsItsFirstTick()
        {
            var bombs = new BombTimers();
            bombs.Arm(7, 1);
            bombs.Tick(7);              // birth move: fresh-skip returns 1
            Assert.AreEqual(0, bombs.Tick(7)); // boom

            bombs.Arm(7, 5);            // the rescue re-arm
            Assert.IsTrue(bombs.TryGet(7, out int remaining));
            Assert.AreEqual(5, remaining);
            Assert.AreEqual(5, bombs.Tick(7)); // fresh again: the rescued move is free
            Assert.AreEqual(4, bombs.Tick(7));
        }

        [Test]
        public void ShortFuse_BumpedToTheFloor_KeepsTicking()
        {
            var bombs = new BombTimers();
            bombs.Arm(3, 9);
            bombs.Tick(3); // fresh-skip
            for (int i = 0; i < 8; i++)
                bombs.Tick(3);
            Assert.IsTrue(bombs.TryGet(3, out int remaining));
            Assert.AreEqual(1, remaining); // one move from disaster

            bombs.Arm(3, 3);            // the rescue floor bump
            Assert.AreEqual(3, bombs.Tick(3)); // fresh-skip protects the rescued move
            Assert.AreEqual(2, bombs.Tick(3));
        }

        // ---- Deferred streak break: the structural abandon carries every decline ---

        [Test]
        public void DecliningARescue_BreaksTheStreakExactlyOnce_AtTheNextStart()
        {
            var state = new MetaState { WinStreak = 4 };
            WinStreakRules.RegisterStart(state);   // the level that will fail
            // The fail panel no longer registers an outcome; the player declines
            // and retries: the next start finds LevelInProgress still true.
            Assert.AreEqual(4, state.WinStreak);   // untouched at the panel
            WinStreakRules.RegisterStart(state);   // Retry -> BuildNewGame
            Assert.AreEqual(0, state.WinStreak);   // broken exactly once
            WinStreakRules.RegisterOutcome(state, won: true);
            Assert.AreEqual(1, state.WinStreak);   // and life goes on
        }

        [Test]
        public void DecliningWithAShield_ConsumesExactlyOneShield()
        {
            var state = new MetaState { WinStreak = 4, StreakShields = 1 };
            WinStreakRules.RegisterStart(state);
            WinStreakRules.RegisterStart(state);   // decline -> retry
            Assert.AreEqual(4, state.WinStreak);   // shield absorbed the break
            Assert.AreEqual(0, state.StreakShields);
        }

        [Test]
        public void RescuedLevel_LeavesNoMetaTrace_SoAWinContinuesTheStreak()
        {
            var state = new MetaState { WinStreak = 4 };
            WinStreakRules.RegisterStart(state);
            // Rescue: no outcome registered, LevelInProgress stays true, play on.
            Assert.IsTrue(state.LevelInProgress);
            WinStreakRules.RegisterOutcome(state, won: true); // the rescued win
            Assert.AreEqual(5, state.WinStreak);
            Assert.IsFalse(state.LevelInProgress);
        }
    }
}
