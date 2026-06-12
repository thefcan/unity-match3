using Match3.Core;

namespace Match3.Game
{
    public enum GamePhase
    {
        Init,
        Playing,
        Resolving,
        GameOver,
    }

    /// <summary>
    /// STATE PATTERN: each phase of the game is its own class with Enter/Exit hooks
    /// and its own reaction to input. The win over a switch/enum approach: behaviour
    /// that only exists in one phase lives in one file, and "input is ignored while
    /// animations play" needs no flags — ResolvingState simply doesn't override
    /// <see cref="OnSwapRequested"/>, so the request falls through to this no-op.
    ///
    /// States are plain C# classes (not MonoBehaviours): they hold a reference to the
    /// <see cref="GameManager"/> as their context and borrow its coroutine runner
    /// when they need one.
    /// </summary>
    public abstract class GameState
    {
        protected GameManager Game { get; }

        protected GameState(GameManager game)
        {
            Game = game;
        }

        public abstract GamePhase Phase { get; }

        public virtual void Enter() { }

        public virtual void Exit() { }

        /// <summary>Called for every swap gesture; only PlayingState acts on it.</summary>
        public virtual void OnSwapRequested(GridPosition from, GridPosition to) { }
    }
}
