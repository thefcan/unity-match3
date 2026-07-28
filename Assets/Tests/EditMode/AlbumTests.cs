using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests
{
    /// <summary>
    /// The sticker album: catalog integrity, deterministic pack rolls, the pity
    /// ladder's completion guarantee, dupe payouts, page/album rewards, the
    /// star-pack watermark, and the meta.sav roundtrip for all 13 new keys.
    /// </summary>
    public sealed class AlbumTests
    {
        private static MetaState Fresh(int salt = 7, int packs = 99)
        {
            var state = new MetaState { AlbumPacks = packs };
            AlbumRules.EnsureSalt(state, salt);
            return state;
        }

        // ---- Catalog -----------------------------------------------------------------

        [Test]
        public void Catalog_Has36UniqueStickers_SixPerPage()
        {
            Assert.AreEqual(36, AlbumCatalog.StickerCount);
            for (int id = 0; id < AlbumCatalog.StickerCount; id++)
            {
                StickerDef def = AlbumCatalog.Get(id);
                Assert.AreEqual(id / 6, def.Page);
                Assert.AreEqual(id % 6, def.Slot);
            }
        }

        [Test]
        public void Catalog_RarityBands_MatchThePages()
        {
            Assert.AreEqual(StickerRarity.Common, AlbumCatalog.PageRarity(0));
            Assert.AreEqual(StickerRarity.Common, AlbumCatalog.PageRarity(1));
            Assert.AreEqual(StickerRarity.Rare, AlbumCatalog.PageRarity(2));
            Assert.AreEqual(StickerRarity.Rare, AlbumCatalog.PageRarity(3));
            Assert.AreEqual(StickerRarity.Epic, AlbumCatalog.PageRarity(4));
            Assert.AreEqual(StickerRarity.Legendary, AlbumCatalog.PageRarity(5));
            Assert.AreEqual(StickerRarity.Legendary, AlbumCatalog.Get(35).Rarity);
        }

        // ---- Rolls -------------------------------------------------------------------

        [Test]
        public void Roll_IsDeterministic_PerSaltPackAndSlot()
        {
            Assert.AreEqual(AlbumRules.RollSticker(7, 3, 1), AlbumRules.RollSticker(7, 3, 1));
            // Each input perturbs the stream (probabilistically distinct for these values).
            bool anyDiffers = AlbumRules.RollSticker(7, 3, 1) != AlbumRules.RollSticker(8, 3, 1)
                              || AlbumRules.RollSticker(7, 3, 1) != AlbumRules.RollSticker(7, 4, 1)
                              || AlbumRules.RollSticker(7, 3, 1) != AlbumRules.RollSticker(7, 3, 2);
            Assert.IsTrue(anyDiffers);
        }

        [Test]
        public void Roll_RarityWeights_BandAFull0To99Sweep()
        {
            for (int band = 0; band < 100; band++)
            {
                StickerRarity expected = band < 60 ? StickerRarity.Common
                    : band < 85 ? StickerRarity.Rare
                    : band < 95 ? StickerRarity.Epic
                    : StickerRarity.Legendary;
                Assert.AreEqual(expected, AlbumRules.RarityForBand(band), $"band {band}");
            }
        }

        // ---- Opening packs -----------------------------------------------------------

        [Test]
        public void OpenPack_ThreeSlots_ConsumesOnePackAdvancesSeedIndex()
        {
            MetaState state = Fresh(packs: 2);
            Assert.IsTrue(AlbumRules.TryOpenPack(state, out PackResult result));
            Assert.AreEqual(3, result.Slots.Length);
            Assert.AreEqual(1, state.AlbumPacks);
            Assert.AreEqual(1, state.AlbumPacksOpened);
        }

        [Test]
        public void OpenPack_NoPacks_ReturnsFalseUntouched()
        {
            MetaState state = Fresh(packs: 0);
            Assert.IsFalse(AlbumRules.TryOpenPack(state, out _));
            Assert.AreEqual(0, state.AlbumPacksOpened);
        }

        [Test]
        public void Dupe_PaysOneBooster_ByStickerIdModThree()
        {
            MetaState state = Fresh();
            int id = AlbumRules.RollSticker(state.AlbumSalt, 0, 0);
            // Own the first roll up front so slot 0 is a guaranteed dupe.
            state.AlbumPageOwned[id / 6] |= 1 << (id % 6);
            var expected = (BoosterKind)(id % 3);
            int before = state.BoosterCount(expected);

            AlbumRules.TryOpenPack(state, out PackResult result);
            Assert.IsFalse(result.Slots[0].WasNew);
            Assert.AreEqual(expected, result.Slots[0].BoosterPaid);
            Assert.AreEqual(before + 1, state.BoosterCount(expected));
        }

        // ---- Pity --------------------------------------------------------------------

        [Test]
        public void Pity_FifthConsecutiveDupe_ForcesConversion()
        {
            MetaState state = Fresh();
            int id = AlbumRules.RollSticker(state.AlbumSalt, 0, 0);
            state.AlbumPageOwned[id / 6] |= 1 << (id % 6);
            state.AlbumPity = AlbumCatalog.PityThreshold; // 4 wasted dupes already seen

            AlbumRules.TryOpenPack(state, out PackResult result);
            Assert.IsTrue(result.Slots[0].WasNew);
            Assert.IsTrue(result.Slots[0].ForcedByPity);
        }

        [Test]
        public void Pity_ForcedPick_IsFirstUnownedRarityAscIdAsc()
        {
            MetaState state = Fresh();
            int id = AlbumRules.RollSticker(state.AlbumSalt, 0, 0);
            state.AlbumPageOwned[id / 6] |= 1 << (id % 6);
            // Also own stickers 0 and 1 so the scan has something to skip.
            state.AlbumPageOwned[0] |= 0b11;
            state.AlbumPity = AlbumCatalog.PityThreshold;

            AlbumRules.TryOpenPack(state, out PackResult result);
            int expected = id == 2 ? 3 : 2; // first id outside {0, 1, id}
            Assert.AreEqual(expected, result.Slots[0].StickerId);
        }

        [Test]
        public void Pity_ResetsOnNaturalNewSticker()
        {
            MetaState state = Fresh();
            state.AlbumPity = 3;
            AlbumRules.TryOpenPack(state, out PackResult result);
            Assert.IsTrue(result.Slots[0].WasNew); // empty album: first roll is always new
            // After a pack with at least one new sticker the pity cannot exceed
            // the number of trailing dupes in that same pack.
            Assert.LessOrEqual(state.AlbumPity, 2);
        }

        // ---- Completion guarantees ---------------------------------------------------

        [Test]
        public void Completion_WorstCase_Within59Packs()
        {
            MetaState state = Fresh(salt: 1, packs: 59);
            int packsUsed = 0;
            while (AlbumRules.OwnedCount(state) < AlbumCatalog.StickerCount
                   && AlbumRules.TryOpenPack(state, out _))
                packsUsed++;

            Assert.AreEqual(AlbumCatalog.StickerCount, AlbumRules.OwnedCount(state),
                $"album incomplete after {packsUsed} packs");
            Assert.LessOrEqual(packsUsed, 59);
        }

        [Test]
        public void EndgameLegendariesOnly_ForcedEveryFifthSlot()
        {
            MetaState state = Fresh(packs: 10);
            for (int id = 0; id < 35; id++) // everything but the last legendary
                state.AlbumPageOwned[id / 6] |= 1 << (id % 6);

            int packsUsed = 0;
            while (!AlbumRules.IsOwned(state, 35) && AlbumRules.TryOpenPack(state, out _))
                packsUsed++;

            Assert.IsTrue(AlbumRules.IsOwned(state, 35));
            Assert.LessOrEqual(packsUsed, 2); // pity forces within 5 slots
        }

        // ---- Star packs --------------------------------------------------------------

        [Test]
        public void StarPacks_WatermarkMintsFloorTens()
        {
            var state = new MetaState();
            Assert.AreEqual(30, AlbumRules.SettleStarPacks(state, 300)); // the veteran splash
            Assert.AreEqual(300, state.AlbumStarsCounted);
            Assert.AreEqual(0, AlbumRules.SettleStarPacks(state, 300)); // idempotent
            Assert.AreEqual(1, AlbumRules.SettleStarPacks(state, 313)); // 31st ten
        }

        [Test]
        public void StarPacks_TotalBelowWatermark_MintsNothingHoldsWatermark()
        {
            var state = new MetaState { AlbumStarsCounted = 200 };
            Assert.AreEqual(0, AlbumRules.SettleStarPacks(state, 50)); // progress wipe
            Assert.AreEqual(200, state.AlbumStarsCounted);
        }

        [Test]
        public void Salt_RollsOncePositiveNonzero_AndSticks()
        {
            var state = new MetaState();
            AlbumRules.EnsureSalt(state, int.MinValue); // negative candidate → masked positive
            Assert.Greater(state.AlbumSalt, 0);
            int first = state.AlbumSalt;
            AlbumRules.EnsureSalt(state, 999); // second call must not re-roll
            Assert.AreEqual(first, state.AlbumSalt);

            var zeroCase = new MetaState();
            AlbumRules.EnsureSalt(zeroCase, 0);
            Assert.AreEqual(1, zeroCase.AlbumSalt);
        }

        // ---- Page & album rewards ----------------------------------------------------

        [Test]
        public void PageComplete_PaysEscalatingReward_OncePerPage()
        {
            // Everything owned except sticker 5; pages 1-5 already rewarded, so
            // ONLY page 0's bundle is in play. Pity primed: whatever slot 0
            // rolls, sticker 5 lands (naturally or by conversion).
            MetaState state = Fresh();
            state.AlbumPageOwned[0] = 0b011111;
            for (int page = 1; page < 6; page++)
                state.AlbumPageOwned[page] = 63;
            state.AlbumPagesRewarded = 0b111110;
            state.AlbumPity = AlbumCatalog.PityThreshold;

            int hammersBefore = state.Hammers;
            AlbumRules.TryOpenPack(state, out PackResult result);
            Assert.AreEqual(1, result.PagesCompletedMask & 1);
            Assert.GreaterOrEqual(state.Hammers, hammersBefore + 1); // (1,1,1,0) + capstone landed

            // Re-opening never re-pays: the rewarded bit blocks it.
            int shieldsAfter = state.StreakShields;
            state.AlbumPacks++;
            AlbumRules.TryOpenPack(state, out PackResult second);
            Assert.AreEqual(0, second.PagesCompletedMask);
            Assert.AreEqual(shieldsAfter, state.StreakShields); // no bundle, only dupe boosters
        }

        [Test]
        public void OnePack_CanCompleteTwoPages_BothPay()
        {
            // Page 1 filled but not yet rewarded, page 0 one short: the open that
            // lands sticker 5 must pay BOTH bundles in one PagesCompletedMask.
            MetaState state = Fresh();
            state.AlbumPageOwned[0] = 0b011111;
            state.AlbumPageOwned[1] = 63;
            for (int page = 2; page < 6; page++)
                state.AlbumPageOwned[page] = 63;
            state.AlbumPagesRewarded = 0b111100; // pages 2-5 already paid
            state.AlbumPity = AlbumCatalog.PityThreshold;

            AlbumRules.TryOpenPack(state, out PackResult result);
            Assert.AreEqual(0b11, result.PagesCompletedMask & 0b11);
        }

        [Test]
        public void AlbumComplete_PaysCapstoneExactlyOnce_PagesRewarded63()
        {
            MetaState state = Fresh();
            for (int page = 0; page < 5; page++)
                state.AlbumPageOwned[page] = 63;
            state.AlbumPagesRewarded = 0b011111;
            state.AlbumPageOwned[5] = 0b011111; // trophy sticker missing
            state.AlbumPity = AlbumCatalog.PityThreshold; // guarantees sticker 35 this open

            int rescuesBefore = state.Rescues;
            AlbumRules.TryOpenPack(state, out PackResult result);
            Assert.IsTrue(result.AlbumCompleted);
            Assert.AreEqual(63, state.AlbumPagesRewarded);
            // Page-5 rescue (1) + capstone rescues (5).
            Assert.AreEqual(rescuesBefore + 1 + AlbumCatalog.AlbumRescues, state.Rescues);

            state.AlbumPacks++;
            AlbumRules.TryOpenPack(state, out PackResult again);
            Assert.IsFalse(again.AlbumCompleted); // once, ever
        }

        [Test]
        public void PageFiveReward_MintsARescueNotAGoldTrophy()
        {
            MetaState state = Fresh();
            for (int page = 0; page < 5; page++)
                state.AlbumPageOwned[page] = 63;
            state.AlbumPagesRewarded = 0b011111;
            state.AlbumPageOwned[5] = 0b011111;
            state.AlbumPity = AlbumCatalog.PityThreshold;

            int goldBefore = state.TrophyGold;
            AlbumRules.TryOpenPack(state, out _);
            Assert.AreEqual(goldBefore, state.TrophyGold); // the race keeps its ledger
        }

        [Test]
        public void OpenPack_MutationsMatchItemizedResult()
        {
            MetaState state = Fresh();
            int hammers = state.Hammers, swaps = state.FreeSwaps, shuffles = state.Shuffles;
            int shields = state.StreakShields, rescues = state.Rescues;

            AlbumRules.TryOpenPack(state, out PackResult result);

            int expHammers = 0, expSwaps = 0, expShuffles = 0;
            foreach (PackSlotResult slot in result.Slots)
            {
                if (slot.WasNew)
                    continue;
                switch (slot.BoosterPaid)
                {
                    case BoosterKind.Hammer: expHammers++; break;
                    case BoosterKind.FreeSwap: expSwaps++; break;
                    default: expShuffles++; break;
                }
            }
            int expShields = 0, expRescues = 0;
            for (int page = 0; page < 6; page++)
            {
                if ((result.PagesCompletedMask & (1 << page)) == 0)
                    continue;
                ChestReward reward = AlbumCatalog.PageReward(page);
                expHammers += reward.Hammers;
                expSwaps += reward.FreeSwaps;
                expShuffles += reward.Shuffles;
                expShields += reward.StreakShields;
                expRescues += AlbumCatalog.PageRescues(page);
            }
            if (result.AlbumCompleted)
            {
                ChestReward cap = AlbumCatalog.AlbumReward();
                expHammers += cap.Hammers;
                expSwaps += cap.FreeSwaps;
                expShuffles += cap.Shuffles;
                expShields += cap.StreakShields;
                expRescues += AlbumCatalog.AlbumRescues;
            }

            Assert.AreEqual(hammers + expHammers, state.Hammers);
            Assert.AreEqual(swaps + expSwaps, state.FreeSwaps);
            Assert.AreEqual(shuffles + expShuffles, state.Shuffles);
            Assert.AreEqual(shields + expShields, state.StreakShields);
            Assert.AreEqual(rescues + expRescues, state.Rescues);
        }

        // ---- Serialization -----------------------------------------------------------

        [Test]
        public void Serializer_RoundtripsAlbumFields()
        {
            var state = new MetaState
            {
                AlbumSalt = 123456,
                AlbumPacks = 7,
                AlbumPacksOpened = 12,
                AlbumStarsCounted = 210,
                AlbumPity = 3,
                AlbumPagesRewarded = 0b101,
                EventToastPacks = 2,
            };
            state.AlbumPageOwned[0] = 63;
            state.AlbumPageOwned[3] = 0b010101;

            MetaState restored = MetaSerializer.Deserialize(MetaSerializer.Serialize(state));
            Assert.AreEqual(123456, restored.AlbumSalt);
            Assert.AreEqual(7, restored.AlbumPacks);
            Assert.AreEqual(12, restored.AlbumPacksOpened);
            Assert.AreEqual(210, restored.AlbumStarsCounted);
            Assert.AreEqual(3, restored.AlbumPity);
            Assert.AreEqual(63, restored.AlbumPageOwned[0]);
            Assert.AreEqual(0b010101, restored.AlbumPageOwned[3]);
            Assert.AreEqual(0, restored.AlbumPageOwned[5]);
            Assert.AreEqual(0b101, restored.AlbumPagesRewarded);
            Assert.AreEqual(2, restored.EventToastPacks);
        }

        [Test]
        public void LegacyFileWithoutAlbumKeys_GetsFreshAlbumDefaults()
        {
            MetaState state = MetaSerializer.Deserialize("streak=3\nhammers=7\n");
            Assert.AreEqual(0, state.AlbumSalt);
            Assert.AreEqual(1, state.AlbumPacks); // the starter pack
            Assert.AreEqual(0, state.AlbumPageOwned[0]);
        }

        [Test]
        public void CorruptAlbumValue_WipesTheWholeFile()
        {
            MetaState state = MetaSerializer.Deserialize("hammers=9\nalbumPacks=three\n");
            Assert.AreEqual(3, state.Hammers); // fresh starter pack, not 9
            Assert.AreEqual(1, state.AlbumPacks);
        }

        [Test]
        public void PageMask_ClampOrder_NegativeToZeroOverflowTo63()
        {
            MetaState negative = MetaSerializer.Deserialize("albumPage2=-5\n");
            Assert.AreEqual(0, negative.AlbumPageOwned[2]); // Max first — no fabricated ownership

            MetaState overflow = MetaSerializer.Deserialize("albumPage2=127\n");
            Assert.AreEqual(63, overflow.AlbumPageOwned[2]);
        }

    }
}
