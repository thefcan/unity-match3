using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests
{
    /// <summary>
    /// The Butler's Gift win streak: wins grow it, a loss resets it, and abandoning
    /// a level mid-run (a start with the previous one unfinished) counts as a loss.
    /// </summary>
    public sealed class WinStreakTests
    {
        [Test]
        public void WinsGrow_AndAFailResets()
        {
            var state = new MetaState();

            WinStreakRules.RegisterStart(state);
            WinStreakRules.RegisterOutcome(state, won: true);
            WinStreakRules.RegisterStart(state);
            WinStreakRules.RegisterOutcome(state, won: true);
            Assert.AreEqual(2, state.WinStreak);

            WinStreakRules.RegisterStart(state);
            WinStreakRules.RegisterOutcome(state, won: false);
            Assert.AreEqual(0, state.WinStreak);
        }

        [Test]
        public void AbandoningALevel_BreaksTheStreak()
        {
            var state = new MetaState();
            WinStreakRules.RegisterStart(state);
            WinStreakRules.RegisterOutcome(state, won: true);
            WinStreakRules.RegisterStart(state);      // played...
            // ...and quit to the menu without finishing. The next start notices.
            WinStreakRules.RegisterStart(state);

            Assert.AreEqual(0, state.WinStreak);
            Assert.IsTrue(state.LevelInProgress, "the NEW level is in progress");
        }

        [Test]
        public void AnOutcome_ClosesTheInProgressFlag()
        {
            var state = new MetaState();
            WinStreakRules.RegisterStart(state);
            Assert.IsTrue(state.LevelInProgress);

            WinStreakRules.RegisterOutcome(state, won: true);
            Assert.IsFalse(state.LevelInProgress);
            Assert.AreEqual(1, state.WinStreak);
        }

        [Test]
        public void PreloadLadder_CapsAtThree()
        {
            Assert.AreEqual(0, WinStreakRules.PreloadCount(0));
            Assert.AreEqual(1, WinStreakRules.PreloadCount(1));
            Assert.AreEqual(2, WinStreakRules.PreloadCount(2));
            Assert.AreEqual(3, WinStreakRules.PreloadCount(3));
            Assert.AreEqual(3, WinStreakRules.PreloadCount(9));
            Assert.AreEqual(0, WinStreakRules.PreloadCount(-2));
        }

        [Test]
        public void Serializer_RoundtripsTheStreakFields()
        {
            var state = new MetaState { WinStreak = 4, LevelInProgress = true };
            MetaState restored = MetaSerializer.Deserialize(MetaSerializer.Serialize(state));
            Assert.AreEqual(4, restored.WinStreak);
            Assert.IsTrue(restored.LevelInProgress);
        }
    }

    /// <summary>Relaxed mode's star economy: completion pays one star, mastery none extra.</summary>
    public sealed class RelaxedModeTests
    {
        [Test]
        public void RelaxedWins_CapAtOneStar()
        {
            Assert.AreEqual(1, StarCalculator.Cap(3, relaxedMode: true));
            Assert.AreEqual(1, StarCalculator.Cap(2, relaxedMode: true));
            Assert.AreEqual(1, StarCalculator.Cap(1, relaxedMode: true));
            Assert.AreEqual(0, StarCalculator.Cap(0, relaxedMode: true));
        }

        [Test]
        public void NormalWins_PassThroughUncapped()
        {
            Assert.AreEqual(3, StarCalculator.Cap(3, relaxedMode: false));
            Assert.AreEqual(0, StarCalculator.Cap(0, relaxedMode: false));
        }

        [Test]
        public void LosingWithNoStreak_KeepsTheShield()
        {
            var state = new MetaState { WinStreak = 0, StreakShields = 1 };

            WinStreakRules.RegisterOutcome(state, won: false);

            // Nothing to protect: the shield is for BREAKING a streak, and repeated
            // retries on a hard level used to burn the whole shelf.
            Assert.That(state.StreakShields, Is.EqualTo(1));
            Assert.That(state.WinStreak, Is.EqualTo(0));
        }
    }
}
