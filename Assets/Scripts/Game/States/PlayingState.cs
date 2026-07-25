using Match3.Core;

namespace Match3.Game
{
    /// <summary>
    /// The only state that accepts player input. Validates the gesture
    /// (on-board + adjacent) and hands the swap to <see cref="ResolvingState"/>.
    /// Whether the swap actually matches anything is decided there — an invalid
    /// swap still deserves its try-and-bounce-back animation.
    /// </summary>
    public sealed class PlayingState : GameState
    {
        public PlayingState(GameManager game) : base(game) { }

        public override GamePhase Phase => GamePhase.Playing;

        public override void OnSwapRequested(GridPosition from, GridPosition to)
        {
            if (!Game.Board.IsInside(from) || !Game.Board.IsInside(to))
                return;

            if (!from.IsAdjacentTo(to))
                return;

            // Locked candies and chocolate can't be dragged at all.
            if (Game.Board.IsImmobile(from) || Game.Board.IsImmobile(to))
                return;

            // An armed free-swap turns this gesture into the booster: no match
            // requirement, no bounce-back, no move spent.
            if (Game.ArmedBooster == BoosterKind.FreeSwap)
            {
                Game.SetState(new FreeSwapResolvingState(Game, from, to));
                return;
            }

            Game.SetState(new ResolvingState(Game, from, to));
        }

        public override void OnTapRequested(GridPosition cell)
        {
            if (Game.ArmedBooster != BoosterKind.Hammer)
                return;
            if (!Game.Board.IsInside(cell))
                return;

            Game.SetState(new HammerResolvingState(Game, cell));
        }
    }
}
