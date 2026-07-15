namespace Match3.Core
{
    /// <summary>
    /// What a tile IS beyond its colour. Normal tiles only ever clear by matching;
    /// every other kind is a "special candy" that detonates a region when it is
    /// matched, caught in another special's blast, or swapped with another special.
    ///
    /// Naming: StripedH clears a horizontal ROW, StripedV clears a vertical COLUMN.
    /// (Candy Crush convention: a horizontal 4-match creates the COLUMN-clearing
    /// candy and vice versa — the stripe is perpendicular to the match.)
    /// </summary>
    public enum TileKind
    {
        Normal,
        /// <summary>Clears its entire row when detonated.</summary>
        StripedH,
        /// <summary>Clears its entire column when detonated.</summary>
        StripedV,
        /// <summary>Explodes a 3x3 area — twice (it survives its first blast, primed, and re-detonates after gravity).</summary>
        Wrapped,
        /// <summary>Colour bomb: has no colour of its own; clears every tile of one colour.</summary>
        ColorBomb,
    }
}
