using System.Collections;
using UnityEngine;

namespace Match3.Game
{
    /// <summary>
    /// Recovers a dead board (no possible moves). The clock and input pause (this phase
    /// isn't Playing/Resolving), the HUD announces the shuffle, the board is rearranged
    /// into a guaranteed-playable layout, the views glide to their new cells, and play
    /// resumes. Reaching this state always exits to <see cref="PlayingState"/> because
    /// <see cref="Match3.Core.Board.Shuffle"/> guarantees a move exists afterwards.
    /// </summary>
    public sealed class ShuffleState : GameState
    {
        private readonly bool _announced;

        /// <param name="announced">
        /// True (dead board): show the "No moves!" banner beat first.
        /// False (shuffle BOOSTER): the player asked for this — skip the drama.
        /// </param>
        public ShuffleState(GameManager game, bool announced = true) : base(game)
        {
            _announced = announced;
        }

        public override GamePhase Phase => GamePhase.Shuffling;

        public override void Enter()
        {
            Game.RunCoroutine(Reshuffle());
        }

        private IEnumerator Reshuffle()
        {
            if (_announced)
            {
                Game.RaiseShuffleStarted();
                yield return new WaitForSeconds(0.6f); // let the "No moves!" banner read
            }

            Game.Board.Shuffle(Game.Random);
            yield return Game.BoardView.AnimateReshuffle();

            Game.SetState(new PlayingState(Game));
        }
    }
}
