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
        /// <summary>
        /// Blocker: colourless, IMMOBILE, never matches or swaps. Destroyed by any
        /// adjacent clear or blast; if a whole move destroys none, one chocolate
        /// spreads onto a neighbouring normal candy at the end of the cascade.
        /// </summary>
        Chocolate,
        /// <summary>
        /// Objective piece: colourless but MOBILE — falls with gravity and can be
        /// swapped (the move is legal when the other tile makes a match). Cannot be
        /// destroyed; it "exits" when it reaches the bottom row.
        /// </summary>
        Ingredient,
        /// <summary>
        /// Jelly fish: made by a 2x2 square match, coloured, detonates by darting at
        /// the most urgent target on the board (jelly &gt; blockers &gt; a random candy)
        /// and hitting that one cell.
        /// </summary>
        Fish,
        /// <summary>
        /// Blocker: colourless, IMMOBILE, layered (1-3). Each adjacent match or blast
        /// hit removes one layer (tracked in <see cref="FrostingGrid"/>); the tile
        /// itself clears when the last layer goes.
        /// </summary>
        Frosting,
        /// <summary>
        /// Blocker: colourless but MOBILE (falls), never matches, destroyed by one
        /// hit — and it ABSORBS a striped beam (the ray stops at the swirl).
        /// </summary>
        Swirl,
        /// <summary>
        /// Blocker: colourless, immobile, INDESTRUCTIBLE. At the end of a move in
        /// which no chocolate broke, it oozes a fresh chocolate onto a neighbour —
        /// even when the board's chocolate died out entirely.
        /// </summary>
        ChocolateFountain,
        /// <summary>
        /// Threat: a coloured candy that matches and falls like a normal one, but
        /// carries a move countdown (tracked in <see cref="BombTimers"/>). Match it
        /// before the counter hits zero or the level is lost.
        /// </summary>
        Bomb,
        /// <summary>
        /// Mystery egg: colourless but MOBILE (falls, shuffles, swaps when the
        /// partner makes a match). Any adjacent clear or direct blast cracks the
        /// shell and it HATCHES into a random candy — usually plain, sometimes a
        /// special (weights live in the resolver). The hatchling lands dormant and
        /// joins play from the next wave on.
        /// </summary>
        MysteryEgg,
    }
}
