using System.Collections;

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

            // Moves mode opens with the diagonal grow-in curtain; the Init phase
            // itself keeps input blocked, so no extra gate is needed. Time attack
            // starts instantly (the ticking clock is the show), and reduced
            // motion skips straight to play.
            if (Game.Mode == GameMode.Moves && !Prefs.ReducedMotionOn)
            {
                Game.RunCoroutine(RevealThenStart());
                return;
            }

            StartPlay();
        }

        private IEnumerator RevealThenStart()
        {
            yield return Game.BoardView.AnimateBoardIntro();
            StartPlay();
        }

        private void StartPlay()
        {
            if (Game.Board.HasPossibleMove())
                Game.SetState(new PlayingState(Game));
            else
                Game.SetState(new ShuffleState(Game));
        }
    }
}
