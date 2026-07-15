using System.Collections;
using Match3.Core;

namespace Match3.Game
{
    /// <summary>
    /// Owns the full life of one move: commit the swap, bounce it back if it matched
    /// nothing, otherwise play every cascade wave (scoring and awarding bonus time as
    /// it goes) and then decide where to go next — advance a level, end the run, or
    /// hand control back to the player. Player input is implicitly ignored for the
    /// whole duration: this state simply doesn't handle it.
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

            // The resolver computes ALL cascade waves up front (including special-candy
            // combos the swap may have fired) and mutates the board to its final state.
            // A swap that achieves nothing returns an empty result WITHOUT mutating,
            // so reverting below is safe. Activation swaps (bomb + anything,
            // special + special) always produce steps — they never bounce.
            ResolutionResult result = Game.Resolver.ResolveSwap(Game.Board, _from, _to);

            if (!result.HadMatches)
            {
                // Useless swap: revert the logic and animate the bounce-back.
                // In time-attack mode a swap costs only the clock, so nothing else
                // happens — Toon Blast-style forgiveness.
                Game.Board.Swap(_from, _to);
                yield return Game.BoardView.AnimateSwap(_from, _to);
                Game.SetState(new PlayingState(Game));
                yield break;
            }

            // Animate the recording wave by wave, scoring each one as its animation
            // lands and topping up the clock for big matches.
            foreach (CascadeStep step in result.Steps)
            {
                yield return Game.BoardView.PlayStep(step);
                Game.AddScore(step.Points);

                int bigMatches = step.BigMatchCount(Game.Config.bonusMatchSize);
                if (bigMatches > 0)
                    Game.AddTime(bigMatches * Game.Config.bonusSeconds);
            }

            // Reaching the target clears the level and loops on (endless). A clutch
            // cascade can carry you over even if the clock hit 0 mid-resolve, so the
            // level check comes first; only then do we honour a dead clock. The actual
            // level bump happens after the celebration beat in LevelCompleteState.
            if (Game.Score >= Game.CurrentTarget)
            {
                Game.SetState(new LevelCompleteState(Game));
            }
            else if (Game.TimeLeft <= 0f)
            {
                Game.SetState(new GameOverState(Game));
            }
            else if (!Game.Board.HasPossibleMove())
            {
                // Dead board — recover before handing control back.
                Game.SetState(new ShuffleState(Game));
            }
            else
            {
                Game.SetState(new PlayingState(Game));
            }
        }
    }
}
