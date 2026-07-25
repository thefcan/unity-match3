namespace Match3.Core
{
    /// <summary>
    /// The in-level booster tools (the genre's standard trio). Inventory lives in
    /// <see cref="MetaState"/> and is spent by the Game layer; none of them consume
    /// a move (the Royal Match rule) and none are available in time attack.
    /// </summary>
    public enum BoosterKind
    {
        /// <summary>Smash one cell: detonates specials, pops locks, crumbles chocolate.</summary>
        Hammer,
        /// <summary>Swap two adjacent candies with no match requirement and no bounce-back.</summary>
        FreeSwap,
        /// <summary>Reshuffle the board into a guaranteed-playable layout.</summary>
        Shuffle,
    }
}
