namespace Match3.Game
{
    /// <summary>
    /// Moves mode defeat: the move budget ran dry (or a bomb went off) with
    /// objectives unfinished. Purely an announcement state — the result panel
    /// offers the retry AND the rescue. Deliberately does NOT register the loss:
    /// a rescue keeps playing with no meta trace, and every decline path (retry,
    /// quit to menu, app kill) leaves LevelInProgress true so the next
    /// RegisterLevelStart breaks the streak via the structural abandon — exactly
    /// once. Accepted quirk: a shield earned between this panel and the next
    /// start can absorb that deferred break. Player-favorable, rare.
    /// </summary>
    public sealed class LevelFailedState : GameState
    {
        public LevelFailedState(GameManager game) : base(game)
        {
        }

        public override GamePhase Phase => GamePhase.LevelFailed;

        public override void Enter()
        {
            // Bare save: mission/event progress counted during the failed attempt
            // still reaches the disk (that flush used to ride the outcome call).
            MetaService.Save();
            Game.RaiseLevelFailed();
        }
    }
}
