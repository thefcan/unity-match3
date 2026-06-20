namespace Match3.Game
{
    /// <summary>
    /// Builds (or rebuilds, on restart) the board and resets the run, then hands control
    /// to <see cref="PlayingState"/> — unless the fresh board happens to be dead, in which
    /// case it shuffles first.
    /// </summary>
    public sealed class InitState : GameState
    {
        public InitState(GameManager game) : base(game) { }

        public override GamePhase Phase => GamePhase.Init;

        public override void Enter()
        {
            Game.BuildNewGame();

            if (Game.Board.HasPossibleMove())
                Game.SetState(new PlayingState(Game));
            else
                Game.SetState(new ShuffleState(Game));
        }
    }
}
