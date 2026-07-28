using System;
using Match3.Core;

namespace Match3.Game
{
    /// <summary>
    /// The sticker album's clock-and-disk shim over AlbumRules (the MissionService
    /// discipline: every public member settles first). Settling rolls the salt
    /// once and converts newly earned stars into packs against the watermark —
    /// which is also what hands a returning veteran their 30-pack splash on the
    /// first menu build after the update. All pack mutation lives in Core;
    /// this class only decides WHEN to save.
    /// </summary>
    public static class AlbumService
    {
        public static int Packs
        {
            get
            {
                EnsureAlbum();
                return MetaService.Current.AlbumPacks;
            }
        }

        public static int OwnedCount
        {
            get
            {
                EnsureAlbum();
                return AlbumRules.OwnedCount(MetaService.Current);
            }
        }

        public static bool IsOwned(int stickerId)
        {
            EnsureAlbum();
            return AlbumRules.IsOwned(MetaService.Current, stickerId);
        }

        public static bool IsPageComplete(int page)
        {
            EnsureAlbum();
            return AlbumRules.IsPageComplete(MetaService.Current, page);
        }

        /// <summary>The permanent golden cover.</summary>
        public static bool IsAlbumComplete
        {
            get
            {
                EnsureAlbum();
                return AlbumRules.IsAlbumComplete(MetaService.Current);
            }
        }

        /// <summary>
        /// Opens one pack. Everything — rolls, ownership, dupe boosters, page and
        /// album rewards — lands on the state inside Core, then ONE save. Never
        /// GrantReward (its internal save would split the transaction).
        /// </summary>
        public static bool TryOpenPack(out PackResult result)
        {
            EnsureAlbum();
            if (!AlbumRules.TryOpenPack(MetaService.Current, out result))
                return false;
            MetaService.Save();
            return true;
        }

        private static void EnsureAlbum()
        {
            MetaState state = MetaService.Current;
            bool changed = false;

            if (state.AlbumSalt == 0)
            {
                // A Guid hash, not a day-derived seed: same-day installs must not
                // share one album stream. EnsureSalt masks it positive.
                AlbumRules.EnsureSalt(state, Guid.NewGuid().GetHashCode());
                changed = true;
            }

            if (AlbumRules.SettleStarPacks(state, ProgressService.Current.TotalStars) > 0)
                changed = true;

            if (changed)
                MetaService.Save();
        }
    }
}
