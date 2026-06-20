namespace Match3.Game
{
    /// <summary>
    /// Terminal state: the clock ran out. Announces the result (the UI listens via the
    /// GameEnded event) and waits. The only way out is <see cref="GameManager.Restart"/>,
    /// which the game-over panel's button calls to loop back to <see cref="InitState"/>
    /// and start a fresh run at level 1.
    /// </summary>
    public sealed class GameOverState : GameState
    {
        public GameOverState(GameManager game) : base(game) { }

        public override GamePhase Phase => GamePhase.GameOver;

        public override void Enter()
        {
            Game.RaiseGameEnded();
        }
    }
}
