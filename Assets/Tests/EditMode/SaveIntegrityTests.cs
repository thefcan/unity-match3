using System;
using System.IO;
using System.Reflection;
using Match3.Core;
using NUnit.Framework;
using static Match3.Tests.TestColors;

namespace Match3.Tests
{
    /// <summary>
    /// What survives a save. Three failure modes, all silent in the old code: a write
    /// interrupted halfway (the tolerant parser reads half a file as a valid, smaller
    /// save), a serializer whose indexed keys were written by a loop but read by a
    /// hand-typed switch, and the Sugar Crush finale paying out mission credit for
    /// specials it minted for the player.
    /// </summary>
    public sealed class SaveIntegrityTests
    {
        // ---- Round-trip completeness ---------------------------------------------------

        /// <summary>
        /// Every field set to something that is NOT its default. The
        /// <see cref="EveryFieldOfAFullState_SurvivesARoundTrip"/> test below refuses to
        /// pass if any field is left at its default, so a new MetaState field that
        /// nobody serialized shows up here as a failure rather than as a lost save.
        /// </summary>
        private static MetaState FullyPopulated()
        {
            var state = new MetaState
            {
                LastClaimDay = 41, Streak = 5, PendingKind = StreakRewardKind.StartWrapped, PendingAmount = 7,
                Hammers = 11, FreeSwaps = 12, Shuffles = 13, Rescues = 7,
                WinStreak = 4, LevelInProgress = true, StreakShields = 2,
                LastChestStars = 240, LastSeenTownStage = 3,
                MissionDay = 42, RerolledSlot = 1, MissionWeek = 6, WeeklyProgress = 9, WeeklyClaimed = true,
                EventWindowId = 17, EventKindId = 2, EventParam = 3, EventProgress = 8, EventRaceClaimed = true,
                TrophyGold = 1, TrophySilver = 2, TrophyBronze = 3,
                EventToastHammers = 4, EventToastFreeSwaps = 5, EventToastShuffles = 6,
                EventToastShields = 7, EventToastTrophy = 3, EventToastRescues = 2,
                AlbumSalt = 12345, AlbumPacks = 9, AlbumPacksOpened = 21, AlbumStarsCounted = 300,
                AlbumPity = 4, AlbumPagesRewarded = 63, EventToastPacks = 5,
            };

            for (int i = 0; i < MissionCatalog.DailyCount; i++)
            {
                state.MissionProgress[i] = 10 + i; // distinct: a slot mix-up must show
                state.MissionClaimed[i] = true;
            }
            for (int i = 0; i < EventCalendar.TierCount; i++)
                state.EventTierClaimed[i] = true;
            for (int i = 0; i < EventCalendar.RaceTarget; i++)
                state.EventRaceLevels[i] = 20 + i;
            for (int page = 0; page < AlbumCatalog.PageCount; page++)
                state.AlbumPageOwned[page] = (page * 7 + 1) & 63;

            return state;
        }

        [Test]
        public void EveryFieldOfAFullState_SurvivesARoundTrip()
        {
            MetaState original = FullyPopulated();
            var fresh = new MetaState();

            MetaState restored = MetaSerializer.Deserialize(MetaSerializer.Serialize(original));

            foreach (FieldInfo field in typeof(MetaState).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object mine = field.GetValue(original);
                object theirs = field.GetValue(restored);
                object untouched = field.GetValue(fresh);

                if (mine is Array array)
                {
                    var other = (Array)theirs;
                    var blank = (Array)untouched;
                    Assert.That(other.Length, Is.EqualTo(array.Length), $"{field.Name} changed length");
                    for (int i = 0; i < array.Length; i++)
                    {
                        Assert.That(array.GetValue(i), Is.Not.EqualTo(blank.GetValue(i)),
                                    $"{field.Name}[{i}] is still at its default — populate it in FullyPopulated()");
                        Assert.That(other.GetValue(i), Is.EqualTo(array.GetValue(i)),
                                    $"{field.Name}[{i}] did not survive the round trip");
                    }
                    continue;
                }

                Assert.That(mine, Is.Not.EqualTo(untouched),
                            $"{field.Name} is still at its default — populate it in FullyPopulated()");
                Assert.That(theirs, Is.EqualTo(mine), $"{field.Name} did not survive the round trip");
            }
        }

        [Test]
        public void IndexedKeys_AreReadBackForEverySlotTheWriterEmits()
        {
            // The writer LOOPS on DailyCount / TierCount / RaceTarget / PageCount; the
            // reader used to name each index by hand. The two agreed only by luck.
            MetaState restored = MetaSerializer.Deserialize(MetaSerializer.Serialize(FullyPopulated()));

            Assert.That(restored.MissionProgress[MissionCatalog.DailyCount - 1],
                        Is.EqualTo(10 + MissionCatalog.DailyCount - 1));
            Assert.That(restored.EventTierClaimed[EventCalendar.TierCount - 1], Is.True);
            Assert.That(restored.EventRaceLevels[EventCalendar.RaceTarget - 1],
                        Is.EqualTo(20 + EventCalendar.RaceTarget - 1));
            Assert.That(restored.AlbumPageOwned[AlbumCatalog.PageCount - 1],
                        Is.EqualTo(((AlbumCatalog.PageCount - 1) * 7 + 1) & 63));
        }

        [Test]
        public void AnOutOfRangeIndexedKey_IsIgnoredRatherThanThrowing()
        {
            string text = MetaSerializer.Serialize(new MetaState()) +
                          "missionProgress99=4\nalbumPage-1=63\neventRaceLevel4000=2\n";

            Assert.DoesNotThrow(() => MetaSerializer.Deserialize(text));
        }

        // ---- Truncation ----------------------------------------------------------------

        [Test]
        public void AWholeFile_IsRecognisedAsWhole()
        {
            string text = MetaSerializer.Serialize(FullyPopulated());

            Assert.That(MetaSerializer.TryDeserialize(text, out MetaState state), Is.True);
            Assert.That(state.Rescues, Is.EqualTo(7));
        }

        [Test]
        public void ATruncatedFile_IsRecognisedAsPartial([Values(25, 50, 75, 99)] int percent)
        {
            // Without the end marker this is invisible: the tail keys (rescues, album
            // packs, every owned sticker) simply revert to their starter defaults and
            // the save loads as a smaller, perfectly valid one.
            string whole = MetaSerializer.Serialize(FullyPopulated());
            string cut = whole.Substring(0, whole.Length * percent / 100);

            // (out _ is unavailable here: TestColors defines `_` as the empty cell.)
            Assert.That(MetaSerializer.TryDeserialize(cut, out MetaState partial), Is.False);
            Assert.That(partial, Is.Not.Null);
        }

        [Test]
        public void AnEmptyFile_IsPartialToo_AndParsesAsFresh()
        {
            Assert.That(MetaSerializer.TryDeserialize("", out MetaState state), Is.False);
            Assert.That(state.Hammers, Is.EqualTo(new MetaState().Hammers));
        }

        [Test]
        public void ThePartialParse_IsStillHandedBack()
        {
            // Callers with no backup to fall back on keep what was readable — losing the
            // tail beats resetting the whole profile.
            string whole = MetaSerializer.Serialize(FullyPopulated());
            string cut = whole.Substring(0, whole.Length / 2);

            MetaSerializer.TryDeserialize(cut, out MetaState state);

            Assert.That(state.Hammers, Is.EqualTo(11), "the head of the file was readable");
        }

        // ---- Atomic writes -------------------------------------------------------------

        [Test]
        public void AtomicWrite_LeavesNoTempBehind_AndKeepsThePreviousVersionAsBackup()
        {
            string path = Path.Combine(Path.GetTempPath(), "match3-atomic-" + Guid.NewGuid().ToString("N") + ".sav");
            try
            {
                AtomicFile.WriteAllText(path, "first");
                Assert.That(File.ReadAllText(path), Is.EqualTo("first"));
                Assert.That(File.Exists(path + AtomicFile.TempSuffix), Is.False, "the temp file must be gone");

                AtomicFile.WriteAllText(path, "second");
                Assert.That(File.ReadAllText(path), Is.EqualTo("second"));
                Assert.That(File.Exists(path + AtomicFile.TempSuffix), Is.False);
                Assert.That(File.ReadAllText(path + AtomicFile.BackupSuffix), Is.EqualTo("first"),
                            "the replace must leave the previous save recoverable");
            }
            finally
            {
                foreach (string suffix in new[] { "", AtomicFile.TempSuffix, AtomicFile.BackupSuffix })
                    if (File.Exists(path + suffix)) File.Delete(path + suffix);
            }
        }

        [Test]
        public void AtomicWrite_SurvivesALeftoverTempFromAnEarlierCrash()
        {
            string path = Path.Combine(Path.GetTempPath(), "match3-atomic-" + Guid.NewGuid().ToString("N") + ".sav");
            try
            {
                AtomicFile.WriteAllText(path, "first");
                File.WriteAllText(path + AtomicFile.TempSuffix, "garbage from a killed process");

                AtomicFile.WriteAllText(path, "second");

                Assert.That(File.ReadAllText(path), Is.EqualTo("second"));
                Assert.That(File.Exists(path + AtomicFile.TempSuffix), Is.False);
            }
            finally
            {
                foreach (string suffix in new[] { "", AtomicFile.TempSuffix, AtomicFile.BackupSuffix })
                    if (File.Exists(path + suffix)) File.Delete(path + suffix);
            }
        }

        [Test]
        public void ProgressRepository_RoundTripsThroughTheAtomicWriter()
        {
            string path = Path.Combine(Path.GetTempPath(), "match3-progress-" + Guid.NewGuid().ToString("N") + ".sav");
            try
            {
                var repository = new FileProgressRepository(path);
                var progress = new PlayerProgress();
                progress.RecordResult(1, 3);
                progress.RecordResult(2, 1);

                repository.Save(progress);
                PlayerProgress loaded = repository.Load();

                Assert.That(loaded.StarsFor(1), Is.EqualTo(3));
                Assert.That(loaded.StarsFor(2), Is.EqualTo(1));
                Assert.That(File.Exists(path + AtomicFile.TempSuffix), Is.False);
            }
            finally
            {
                foreach (string suffix in new[] { "", AtomicFile.TempSuffix, AtomicFile.BackupSuffix })
                    if (File.Exists(path + suffix)) File.Delete(path + suffix);
            }
        }

        // ---- The finale is a payout, not play ------------------------------------------

        /// <summary>A 3x3 with no natural runs — the finale's blank canvas (FinaleTests' idiom).</summary>
        private static Board LatinSquare(TileFactory factory) => Board.FromLayout(new[,]
        {
            { A, B, C },
            { B, C, A },
            { C, A, B },
        }, factory);

        [Test]
        public void TheFinale_MintsStripedCandies_ButCreditsNoMission()
        {
            var rng = new SystemRandom(42);
            var factory = new TileFactory(5, rng);
            Board board = LatinSquare(factory);
            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, rng);

            ResolutionResult finale = resolver.ResolveFinale(board, 10); // 10 moves -> 8 striped

            int striped = 0, wrapped = 0, specialist = 0, minted = 0;
            foreach (CascadeStep step in finale.Steps)
            {
                minted += step.Creations.Count;
                striped += MissionCatalog.CountFor(new MissionDef(MissionType.MakeStriped, 0, 6), step);
                wrapped += MissionCatalog.CountFor(new MissionDef(MissionType.MakeWrapped, 0, 4), step);
                specialist += EventRules.CountFor(EventKind.SpecialistWeek, 0, step);
                Assert.That(step.IsFinale, Is.True, "every finale wave carries the marker");
            }

            Assert.That(minted, Is.GreaterThan(0), "the finale really did create specials");
            Assert.That(striped, Is.EqualTo(0), "a won level must not finish a make-striped mission by itself");
            Assert.That(wrapped, Is.EqualTo(0));
            Assert.That(specialist, Is.EqualTo(0), "nor a SpecialistWeek tier sized for three days of play");
        }

        [Test]
        public void AnOrdinaryFourRun_StillCredits()
        {
            // The other half of the rule: normal play is untouched.
            TileFactory factory = TestFactories.Seeded();
            Board board = Board.FromLayout(new[,]
            {
                { B, C, D, E },
                { C, D, E, B },
                { A, A, A, A },
            }, factory);

            var resolver = new CascadeResolver(new ScoreConfig(10, 1), factory, new SystemRandom(1));
            ResolutionResult result = resolver.Resolve(board);

            int striped = 0, specialist = 0;
            foreach (CascadeStep step in result.Steps)
            {
                Assert.That(step.IsFinale, Is.False, "an ordinary resolve is not a finale");
                striped += MissionCatalog.CountFor(new MissionDef(MissionType.MakeStriped, 0, 6), step);
                specialist += EventRules.CountFor(EventKind.SpecialistWeek, 0, step);
            }

            Assert.That(striped, Is.EqualTo(1), "a 4-run mints one striped candy, and it counts");
            Assert.That(specialist, Is.EqualTo(1));
        }
    }
}
