namespace Match3.Game
{
    /// <summary>
    /// Moves mode defeat: the move budget ran dry with objectives unfinished. Purely
    /// an announcement state — the result panel offers the retry.
    /// </summary>
    public sealed class LevelFailedState : GameState
    {
        public LevelFailedState(GameManager game) : base(game)
        {
        }

        public override GamePhase Phase => GamePhase.LevelFailed;

        public override void Enter()
        {
            Game.RaiseLevelFailed();
        }
    }
}
