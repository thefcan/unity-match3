using System.Collections;
using UnityEngine;

namespace Match3.Game
{
    /// <summary>
    /// Recovers a dead board (no possible moves). The clock and input pause (this phase
    /// isn't Playing/Resolving), the HUD announces the shuffle, the board is rearranged
    /// into a playable layout, the views glide to their new cells, and play resumes.
    /// Shuffle is best-effort — it gives up after 100 attempts and accepts the last
    /// permutation — so this state CHECKS the result: a board that still has no move
    /// (blocker-heavy levels) ends the level instead of soft-locking the player.
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

            // The 100-attempt fallback can still land on a dead board. Failing the
            // level routes into the normal fail panel (rescue offer included) —
            // infinitely better than a board nobody can move.
            if (Game.Board.HasPossibleMove())
                Game.SetState(new PlayingState(Game));
            else
                Game.SetState(new LevelFailedState(Game));
        }
    }
}
