using System;

namespace Match3.Core
{
    public enum StickerRarity
    {
        Common,
        Rare,
        Epic,
        Legendary,
    }

    /// <summary>How the UI finds a sticker's art. Tile covers everything the
    /// CandySpriteLibrary serves through For(); the rest are special load paths.</summary>
    public enum StickerArt
    {
        Tile,     // library.For(Param, Kind)
        LockCage, // library.LockCage (the cage is a grid overlay, not a TileKind)
        Frosting, // library.FrostingSprite(Param) — Param = layer 1..3
        Town,     // Resources UI/Town/town_stage{Param} — Param = stage 1..5
        Trophy,   // UiTheme.TrophySprite (star pip fallback)
    }

    /// <summary>One album sticker: where it lives, how rare it is, what it looks like.</summary>
    public readonly struct StickerDef
    {
        public readonly int Page;
        public readonly int Slot;
        public readonly StickerRarity Rarity;
        public readonly StickerArt Art;
        public readonly TileKind Kind;
        public readonly int Param;

        public StickerDef(int page, int slot, StickerRarity rarity, StickerArt art, TileKind kind, int param)
        {
            Page = page;
            Slot = slot;
            Rarity = rarity;
            Art = art;
            Kind = kind;
            Param = param;
        }
    }

    /// <summary>
    /// The sticker album's fixed catalog: six themed pages of six stickers,
    /// celebrating the game's own bestiary (no new art — every sticker is a
    /// sprite the game already ships). Pages are ordered by rarity, so sticker
    /// ids ascend with rarity too — the pity scan leans on that.
    ///
    /// APPEND ONLY: sticker ids (page*6+slot) are persisted as ownership bits
    /// in meta.sav, and the pack roll's tier ranges derive from this table —
    /// reordering or resizing shipped pages reshuffles every player's album.
    /// New pages must be APPENDED with new ids.
    /// </summary>
    public static class AlbumCatalog
    {
        public const int PageCount = 6;
        public const int SlotsPerPage = 6;
        public const int StickerCount = PageCount * SlotsPerPage;
        public const int PackSize = 3;
        public const int StarsPerPack = 10;
        /// <summary>A dupe rolled while pity sits at this value converts into the
        /// first unowned sticker instead (check-then-increment).</summary>
        public const int PityThreshold = 4;
        /// <summary>Completing the whole album mints this many fail-panel Rescues.</summary>
        public const int AlbumRescues = 5;

        private static readonly string[] PageNames =
        {
            "CANDIES", "STRIPED", "WRAPPED", "FISH", "BLOCKERS", "TOWN",
        };

        private static readonly StickerRarity[] PageRarities =
        {
            StickerRarity.Common, StickerRarity.Common,
            StickerRarity.Rare, StickerRarity.Rare,
            StickerRarity.Epic, StickerRarity.Legendary,
        };

        public static string PageName(int page) => PageNames[page];

        public static StickerRarity PageRarity(int page) => PageRarities[page];

        /// <summary>The sticker with id = page*6 + slot. Pure table lookup.</summary>
        public static StickerDef Get(int stickerId)
        {
            int page = stickerId / SlotsPerPage;
            int slot = stickerId % SlotsPerPage;
            StickerRarity rarity = PageRarities[page];
            switch (page)
            {
                case 0: // CANDIES: the five silhouettes + the ingredient cherries
                    return slot < 5
                        ? new StickerDef(page, slot, rarity, StickerArt.Tile, TileKind.Normal, slot)
                        : new StickerDef(page, slot, rarity, StickerArt.Tile, TileKind.Ingredient, 0);
                case 1: // STRIPED: five striped candies + the colour bomb
                    return slot < 5
                        ? new StickerDef(page, slot, rarity, StickerArt.Tile, TileKind.StripedV, slot)
                        : new StickerDef(page, slot, rarity, StickerArt.Tile, TileKind.ColorBomb, 0);
                case 2: // WRAPPED: five wrapped candies + the licorice cage
                    return slot < 5
                        ? new StickerDef(page, slot, rarity, StickerArt.Tile, TileKind.Wrapped, slot)
                        : new StickerDef(page, slot, rarity, StickerArt.LockCage, TileKind.Normal, 0);
                case 3: // FISH: the five jelly fish + chocolate
                    return slot < 5
                        ? new StickerDef(page, slot, rarity, StickerArt.Tile, TileKind.Fish, slot)
                        : new StickerDef(page, slot, rarity, StickerArt.Tile, TileKind.Chocolate, 0);
                case 4: // BLOCKERS: frosting 1-3, swirl, fountain, bomb
                    switch (slot)
                    {
                        case 0:
                        case 1:
                        case 2: return new StickerDef(page, slot, rarity, StickerArt.Frosting, TileKind.Frosting, slot + 1);
                        case 3: return new StickerDef(page, slot, rarity, StickerArt.Tile, TileKind.Swirl, 0);
                        case 4: return new StickerDef(page, slot, rarity, StickerArt.Tile, TileKind.ChocolateFountain, 0);
                        default: return new StickerDef(page, slot, rarity, StickerArt.Tile, TileKind.Bomb, 0);
                    }
                default: // TOWN: the five town stages + the trophy
                    return slot < 5
                        ? new StickerDef(page, slot, rarity, StickerArt.Town, TileKind.Normal, slot + 1)
                        : new StickerDef(page, slot, rarity, StickerArt.Trophy, TileKind.Normal, 0);
            }
        }

        /// <summary>Booster bundle for completing a page (auto-granted inside the
        /// opening — never a claim button, which is what lets the golden cover be
        /// derived from AlbumPagesRewarded == 63 alone).</summary>
        public static ChestReward PageReward(int page)
        {
            switch (page)
            {
                case 0:
                case 1: return new ChestReward(1, 1, 1, 0);
                case 2:
                case 3: return new ChestReward(2, 1, 1, 0);
                case 4: return new ChestReward(2, 2, 2, 1);
                default: return new ChestReward(3, 3, 3, 1);
            }
        }

        /// <summary>The TOWN page mints a Rescue instead of a gold trophy — the
        /// trophy shelf stays the race's prestige ledger.</summary>
        public static int PageRescues(int page) => page == PageCount - 1 ? 1 : 0;

        public static ChestReward AlbumReward() => new ChestReward(2, 2, 2, 2);
    }

    /// <summary>One opened pack slot: which sticker, and how it landed.</summary>
    public struct PackSlotResult
    {
        public int StickerId;
        public bool WasNew;
        public bool ForcedByPity;
        public BoosterKind BoosterPaid; // meaningful only when !WasNew
    }

    /// <summary>Everything one pack did — the ceremony renders this verbatim.</summary>
    public struct PackResult
    {
        public PackSlotResult[] Slots;
        /// <summary>Bit per page completed by THIS pack (one pack can close several).</summary>
        public int PagesCompletedMask;
        public bool AlbumCompleted;
    }

    /// <summary>
    /// The album's pure rules: deterministic pack rolls (salt + pack ordinal),
    /// the pity ladder that guarantees completion, dupe payouts, page/album
    /// rewards, and the star-pack watermark. All mutation happens here so the
    /// service can persist one save per pack and EditMode tests can cover
    /// everything — the trophy/rescue mint doctrine, one file up.
    /// </summary>
    public static class AlbumRules
    {
        public static bool IsOwned(MetaState state, int stickerId) =>
            (state.AlbumPageOwned[stickerId / AlbumCatalog.SlotsPerPage]
                >> (stickerId % AlbumCatalog.SlotsPerPage) & 1) != 0;

        public static int OwnedCount(MetaState state)
        {
            int count = 0;
            for (int page = 0; page < AlbumCatalog.PageCount; page++)
            {
                int mask = state.AlbumPageOwned[page];
                while (mask != 0)
                {
                    count += mask & 1;
                    mask >>= 1;
                }
            }
            return count;
        }

        public static bool IsPageComplete(MetaState state, int page) =>
            state.AlbumPageOwned[page] == (1 << AlbumCatalog.SlotsPerPage) - 1;

        /// <summary>The permanent golden cover: every page reward has been paid.</summary>
        public static bool IsAlbumComplete(MetaState state) =>
            state.AlbumPagesRewarded == (1 << AlbumCatalog.PageCount) - 1;

        /// <summary>
        /// Rolls the salt once. The candidate comes from the Game layer (a Guid
        /// hash — day-derived seeds would give same-day installs identical
        /// albums). Masked positive BEFORE storing: the serializer's house
        /// Math.Max(0, …) clamp would silently zero a negative salt on every
        /// load and re-roll the stream each session.
        /// </summary>
        public static void EnsureSalt(MetaState state, int candidate)
        {
            if (state.AlbumSalt != 0)
                return;
            int salt = candidate & 0x7FFFFFFF;
            state.AlbumSalt = salt == 0 ? 1 : salt;
        }

        /// <summary>
        /// Star packs: one per ten TOTAL stars, against a monotone watermark
        /// (StarChest idiom). A total below the watermark (progress wipe) mints
        /// nothing and the watermark holds. Returns the packs minted.
        /// </summary>
        public static int SettleStarPacks(MetaState state, int totalStars)
        {
            int minted = Math.Max(0,
                totalStars / AlbumCatalog.StarsPerPack - state.AlbumStarsCounted / AlbumCatalog.StarsPerPack);
            state.AlbumPacks += minted;
            state.AlbumStarsCounted = Math.Max(state.AlbumStarsCounted, totalStars);
            return minted;
        }

        /// <summary>The 0-99 band → rarity split (60/25/10/5).</summary>
        public static StickerRarity RarityForBand(int band)
        {
            if (band < 60) return StickerRarity.Common;
            if (band < 85) return StickerRarity.Rare;
            if (band < 95) return StickerRarity.Epic;
            return StickerRarity.Legendary;
        }

        /// <summary>Deterministic sticker for (salt, pack ordinal, slot). Rarity
        /// band first, then a uniform pick inside the tier's contiguous id range.</summary>
        public static int RollSticker(int salt, int packIndex, int slot)
        {
            // The +1s keep pack 0 / slot 0 from degenerating to Hash(salt).
            uint seed = Hash((uint)salt
                ^ (uint)(packIndex + 1) * 0x9E3779B9u
                ^ (uint)(slot + 1) * 0x85EBCA6Bu);
            StickerRarity rarity = RarityForBand((int)(seed % 100u));
            TierRange(rarity, out int start, out int size);
            return start + (int)(Hash(seed) % (uint)size);
        }

        /// <summary>
        /// Opens one pack: rolls three slots, marks ownership, pays dupes one
        /// booster each, applies page/album rewards straight onto the state
        /// (BankOutgoing mould — the caller does exactly ONE save afterwards,
        /// never GrantReward). False when the shelf is empty.
        /// </summary>
        public static bool TryOpenPack(MetaState state, out PackResult result)
        {
            result = default;
            if (state.AlbumPacks <= 0 || state.AlbumSalt == 0)
                return false;

            var slots = new PackSlotResult[AlbumCatalog.PackSize];
            int packIndex = state.AlbumPacksOpened;
            for (int slot = 0; slot < AlbumCatalog.PackSize; slot++)
            {
                int stickerId = RollSticker(state.AlbumSalt, packIndex, slot);
                if (!IsOwned(state, stickerId))
                {
                    Own(state, stickerId);
                    state.AlbumPity = 0;
                    slots[slot] = new PackSlotResult { StickerId = stickerId, WasNew = true };
                }
                else if (state.AlbumPity >= AlbumCatalog.PityThreshold
                         && TryFirstUnowned(state, out int forced))
                {
                    // The pity conversion: rarity ascends with id, so the first
                    // unowned id IS the cheapest missing sticker.
                    Own(state, forced);
                    state.AlbumPity = 0;
                    slots[slot] = new PackSlotResult { StickerId = forced, WasNew = true, ForcedByPity = true };
                }
                else
                {
                    state.AlbumPity++;
                    var paid = (BoosterKind)(stickerId % 3);
                    state.AddBoosters(paid, 1);
                    slots[slot] = new PackSlotResult { StickerId = stickerId, BoosterPaid = paid };
                }
            }

            state.AlbumPacks--;
            state.AlbumPacksOpened++;

            // Page rewards — auto-granted, once, on the open that fills the mask.
            int rewardedBefore = state.AlbumPagesRewarded;
            int completedMask = 0;
            for (int page = 0; page < AlbumCatalog.PageCount; page++)
            {
                int bit = 1 << page;
                if (!IsPageComplete(state, page) || (state.AlbumPagesRewarded & bit) != 0)
                    continue;
                state.AlbumPagesRewarded |= bit;
                completedMask |= bit;
                Apply(state, AlbumCatalog.PageReward(page));
                state.Rescues += AlbumCatalog.PageRescues(page);
            }

            bool albumCompleted = rewardedBefore != (1 << AlbumCatalog.PageCount) - 1
                                  && IsAlbumComplete(state);
            if (albumCompleted)
            {
                Apply(state, AlbumCatalog.AlbumReward());
                state.Rescues += AlbumCatalog.AlbumRescues;
            }

            result = new PackResult
            {
                Slots = slots,
                PagesCompletedMask = completedMask,
                AlbumCompleted = albumCompleted,
            };
            return true;
        }

        /// <summary>A top-two race finish mints one pack — behind the same 3-win
        /// gate as the trophies and rescues, on the claim AND the bank path.</summary>
        public static int PackForRace(int placement, int ticks) =>
            ticks < 3 ? 0 : placement <= 2 ? 1 : 0;

        private static void Own(MetaState state, int stickerId) =>
            state.AlbumPageOwned[stickerId / AlbumCatalog.SlotsPerPage] |=
                1 << (stickerId % AlbumCatalog.SlotsPerPage);

        private static bool TryFirstUnowned(MetaState state, out int stickerId)
        {
            for (int id = 0; id < AlbumCatalog.StickerCount; id++)
            {
                if (!IsOwned(state, id))
                {
                    stickerId = id;
                    return true;
                }
            }
            stickerId = -1;
            return false;
        }

        private static void Apply(MetaState state, ChestReward reward)
        {
            state.AddBoosters(BoosterKind.Hammer, reward.Hammers);
            state.AddBoosters(BoosterKind.FreeSwap, reward.FreeSwaps);
            state.AddBoosters(BoosterKind.Shuffle, reward.Shuffles);
            state.StreakShields += reward.StreakShields;
        }

        private static void TierRange(StickerRarity rarity, out int start, out int size)
        {
            switch (rarity)
            {
                case StickerRarity.Common: start = 0; size = 12; break;
                case StickerRarity.Rare: start = 12; size = 12; break;
                case StickerRarity.Epic: start = 24; size = 6; break;
                default: start = 30; size = 6; break;
            }
        }

        private static uint Hash(uint x)
        {
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            return x;
        }
    }
}
