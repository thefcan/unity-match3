namespace Match3.Game
{
    /// <summary>
    /// Builds (or rebuilds, on restart) the board, resets score and moves,
    /// then hands control straight to <see cref="PlayingState"/>.
    /// </summary>
    public sealed class InitState : GameState
    {
        public InitState(GameManager game) : base(game) { }

        public override GamePhase Phase => GamePhase.Init;

        public override void Enter()
        {
            Game.BuildNewGame();
            Game.SetState(new PlayingState(Game));
        }
    }
}
