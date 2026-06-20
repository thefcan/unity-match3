using System.Collections;
using UnityEngine;

namespace Match3.Game
{
    /// <summary>
    /// A short celebration beat between levels: the clock and input both pause (this
    /// phase isn't <see cref="GamePhase.Playing"/> or <see cref="GamePhase.Resolving"/>,
    /// so GameManager.Update doesn't tick the timer and swaps fall through to no-ops),
    /// the HUD shows "Level X Complete!", then we advance and hand control back.
    ///
    /// Adding this as its own STATE — rather than an inline pause inside ResolvingState —
    /// is exactly what the State pattern buys: the "between levels" rules (frozen clock,
    /// ignored input, timed auto-advance) live in one place and can't leak elsewhere.
    /// </summary>
    public sealed class LevelCompleteState : GameState
    {
        public LevelCompleteState(GameManager game) : base(game) { }

        public override GamePhase Phase => GamePhase.LevelComplete;

        public override void Enter()
        {
            Game.RunCoroutine(Celebrate());
        }

        private IEnumerator Celebrate()
        {
            // Shrink the board away so the "Level Complete!" banner stands alone, then
            // announce. The clock is frozen for the whole pause (this phase isn't
            // Playing/Resolving, so GameManager.Update doesn't tick it).
            yield return Game.BoardView.AnimateHideTiles();
            Game.RaiseLevelCompleted();
            yield return new WaitForSeconds(Game.Config.levelCompletePauseSeconds);

            // Pop the SAME arrangement back, then bump level/target, refill the clock
            // and reset the score before playing on (entering Playing clears the banner).
            yield return Game.BoardView.AnimateShowTiles();
            Game.AdvanceLevel();
            Game.SetState(new PlayingState(Game));
        }
    }
}
