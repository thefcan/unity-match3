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

            Game.SetState(new ResolvingState(Game, from, to));
        }
    }
}
