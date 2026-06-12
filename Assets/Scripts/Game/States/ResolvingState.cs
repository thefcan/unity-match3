using System.Collections;
using Match3.Core;

namespace Match3.Game
{
    /// <summary>
    /// Owns the full life of one move: commit the swap, bounce it back if it matched
    /// nothing, otherwise play every cascade wave and then decide where to go next
    /// (back to Playing, or to GameOver). Player input is implicitly ignored for the
    /// whole duration — this state simply doesn't handle it.
    ///
    /// C# note for Java readers: a method returning IEnumerator + "yield return" is a
    /// generator (like a Java Iterator built by the compiler). Unity's coroutine
    /// scheduler resumes it once per frame — or after a nested animation finishes —
    /// which lets sequential-looking code drive multi-frame animations without
    /// callbacks or threads.
    /// </summary>
    public sealed class ResolvingState : GameState
    {
        private readonly GridPosition _from;
        private readonly GridPosition _to;

        public ResolvingState(GameManager game, GridPosition from, GridPosition to) : base(game)
        {
            _from = from;
            _to = to;
        }

        public override GamePhase Phase => GamePhase.Resolving;

        public override void Enter()
        {
            Game.RunCoroutine(ResolveMove());
        }

        private IEnumerator ResolveMove()
        {
            // Commit the swap in the logic first, then animate the views to match.
            // The board is the single source of truth; views only ever chase it.
            Game.Board.Swap(_from, _to);
            yield return Game.BoardView.AnimateSwap(_from, _to);

            if (Game.Board.FindMatches().Count == 0)
            {
                // Useless swap: revert the logic and animate the bounce-back.
                // No move is consumed — matching Toon Blast-style forgiveness.
                Game.Board.Swap(_from, _to);
                yield return Game.BoardView.AnimateSwap(_from, _to);
                Game.SetState(new PlayingState(Game));
                yield break;
            }

            Game.ConsumeMove();

            // The resolver computes ALL cascade waves up front and mutates the board
            // to its final state; we then animate the recording wave by wave,
            // scoring each one as its animation lands (the score ticks up mid-combo).
            ResolutionResult result = Game.Resolver.Resolve(Game.Board);
            foreach (CascadeStep step in result.Steps)
            {
                yield return Game.BoardView.PlayStep(step);
                Game.AddScore(step.Points);
            }

            if (Game.Score >= Game.Level.targetScore)
                Game.SetState(new GameOverState(Game, won: true));
            else if (Game.MovesLeft <= 0)
                Game.SetState(new GameOverState(Game, won: false));
            else
                Game.SetState(new PlayingState(Game));
        }
    }
}
