namespace Match3.Core
{
    /// <summary>
    /// The kind of special-special (or bomb-normal) swap the player performed.
    /// <see cref="None"/> means "not an activation swap — apply normal match rules".
    /// </summary>
    public enum SwapKind
    {
        None,
        /// <summary>Striped + striped: a full row + column cross through the landing cell.</summary>
        StripedStriped,
        /// <summary>Striped + wrapped: three rows and three columns centred on the landing cell.</summary>
        StripedWrapped,
        /// <summary>Wrapped + wrapped: a 5x5 blast at each of the two swap cells.</summary>
        WrappedWrapped,
        /// <summary>Colour bomb + normal tile: clears every tile of that colour.</summary>
        BombNormal,
        /// <summary>Colour bomb + striped: every tile of that colour turns striped and detonates.</summary>
        BombStriped,
        /// <summary>Colour bomb + wrapped: clears the wrapped's colour; the wrapped then double-blasts.</summary>
        BombWrapped,
        /// <summary>Colour bomb + colour bomb: clears the whole board.</summary>
        BombBomb,
    }

    /// <summary>
    /// Classifies what swapping two tiles means, purely from their kinds. Striped and
    /// wrapped candies swapped with a NORMAL tile are deliberately <see cref="SwapKind.None"/>:
    /// they only activate when matched — the Candy Crush rule. A colour bomb, by
    /// contrast, activates on ANY swap (it can never colour-match: it has no colour).
    /// </summary>
    public static class SwapRules
    {
        public static SwapKind Classify(Tile a, Tile b)
        {
            if (a.IsColorBomb && b.IsColorBomb) return SwapKind.BombBomb;

            if (a.IsColorBomb || b.IsColorBomb)
            {
                Tile other = a.IsColorBomb ? b : a;
                if (other.IsStriped) return SwapKind.BombStriped;
                if (other.Kind == TileKind.Wrapped) return SwapKind.BombWrapped;
                return SwapKind.BombNormal;
            }

            if (a.IsStriped && b.IsStriped) return SwapKind.StripedStriped;

            if ((a.IsStriped && b.Kind == TileKind.Wrapped) ||
                (a.Kind == TileKind.Wrapped && b.IsStriped))
                return SwapKind.StripedWrapped;

            if (a.Kind == TileKind.Wrapped && b.Kind == TileKind.Wrapped)
                return SwapKind.WrappedWrapped;

            return SwapKind.None;
        }

        /// <summary>True when swapping these two tiles fires a combo even without a colour match.</summary>
        public static bool IsActivationSwap(Tile a, Tile b) => Classify(a, b) != SwapKind.None;
    }
}
