namespace Match3.Game
{
    /// <summary>
    /// Terminal state: announces the result (the UI listens via the GameEnded event)
    /// and waits. The only way out is <see cref="GameManager.Restart"/>, which the
    /// game-over panel's button calls to loop back to <see cref="InitState"/>.
    /// </summary>
    public sealed class GameOverState : GameState
    {
        private readonly bool _won;

        public GameOverState(GameManager game, bool won) : base(game)
        {
            _won = won;
        }

        public override GamePhase Phase => GamePhase.GameOver;

        public override void Enter()
        {
            Game.RaiseGameEnded(_won);
        }
    }
}
