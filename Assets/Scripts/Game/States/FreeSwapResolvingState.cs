using System.Collections;
using Match3.Core;

namespace Match3.Game
{
    /// <summary>
    /// Booster: the free switch. Swaps two adjacent candies with NO match
    /// requirement and NO bounce-back — repositioning the board is the product, so
    /// the booster is consumed up front even when nothing matches. Matches that do
    /// happen resolve exactly like a normal move, except no move is spent.
    /// </summary>
    public sealed class FreeSwapResolvingState : GameState
    {
        private readonly GridPosition _from;
        private readonly GridPosition _to;

        public FreeSwapResolvingState(GameManager game, GridPosition from, GridPosition to) : base(game)
        {
            _from = from;
            _to = to;
        }

        public override GamePhase Phase => GamePhase.Resolving;

        public override void Enter()
        {
            Game.RunCoroutine(Swap());
        }

        private IEnumerator Swap()
        {
            MetaService.TrySpendBooster(BoosterKind.FreeSwap);
            Game.RaiseBoostersChanged();
            AudioManager.Play(Sfx.Swap);

            Game.Board.Swap(_from, _to);
            yield return Game.BoardView.AnimateSwap(_from, _to);

            // Empty result = the pair simply stays swapped (ResolveSwap never
            // mutates when nothing matched, so the board is exactly the new layout).
            ResolutionResult result = Game.Resolver.ResolveSwap(Game.Board, _from, _to);

            foreach (CascadeStep step in result.Steps)
            {
                yield return Game.BoardView.PlayStep(step);
                Game.AddScore(step.Points);
                Game.Objectives.Consume(step);
                Game.RaiseObjectivesChanged();
            }

            if (Game.Objectives.AllComplete)
                Game.SetState(new LevelWonState(Game));
            else if (!Game.Board.HasPossibleMove())
                Game.SetState(new ShuffleState(Game));
            else
                Game.SetState(new PlayingState(Game));
        }
    }
}
