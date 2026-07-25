using System.Collections;
using Match3.Core;

namespace Match3.Game
{
    /// <summary>
    /// Booster: the hammer smash on one cell. No move is spent (the Royal Match
    /// rule), and the booster is only CONSUMED when the smash actually changed
    /// something — an indestructible target (an ingredient) refunds it by simply
    /// bouncing back to Playing with the inventory untouched.
    /// </summary>
    public sealed class HammerResolvingState : GameState
    {
        private readonly GridPosition _target;

        public HammerResolvingState(GameManager game, GridPosition target) : base(game)
        {
            _target = target;
        }

        public override GamePhase Phase => GamePhase.Resolving;

        public override void Enter()
        {
            Game.RunCoroutine(Smash());
        }

        private IEnumerator Smash()
        {
            ResolutionResult result = Game.Resolver.ResolveHammer(Game.Board, _target);
            if (!result.HadMatches)
            {
                Game.SetState(new PlayingState(Game));
                yield break;
            }

            MetaService.TrySpendBooster(BoosterKind.Hammer);
            Game.RaiseBoostersChanged();
            AudioManager.Play(Sfx.WrappedBlast);

            foreach (CascadeStep step in result.Steps)
            {
                yield return Game.BoardView.PlayStep(step);
                Game.AddScore(step.Points);
                Game.Objectives.Consume(step);
                Game.RaiseObjectivesChanged();
            }

            // A hammer can finish the level (last jelly, last chocolate) — honour it.
            // It can never FAIL one though: no move was spent.
            if (Game.Objectives.AllComplete)
                Game.SetState(new LevelWonState(Game));
            else if (!Game.Board.HasPossibleMove())
                Game.SetState(new ShuffleState(Game));
            else
                Game.SetState(new PlayingState(Game));
        }
    }
}
