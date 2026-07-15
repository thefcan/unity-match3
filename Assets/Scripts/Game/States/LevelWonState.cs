using System.Collections;
using Match3.Core;

namespace Match3.Game
{
    /// <summary>
    /// Moves mode victory: bank the unused moves as bonus points BEFORE the star
    /// rating is computed (a fast clear should shine), sweep the tiles away, then
    /// announce the result — the result panel takes it from there.
    /// </summary>
    public sealed class LevelWonState : GameState
    {
        public LevelWonState(GameManager game) : base(game)
        {
        }

        public override GamePhase Phase => GamePhase.LevelWon;

        public override void Enter()
        {
            Game.RunCoroutine(Celebrate());
        }

        private IEnumerator Celebrate()
        {
            int bonus = Game.MovesLeft * Game.LevelDefinition.movesBonusPoints;
            if (bonus > 0)
                Game.AddScore(bonus);

            int stars = StarCalculator.Calculate(Game.Score, Game.LevelDefinition.starScores);
            ProgressService.RecordWin(Game.Level, stars);

            yield return Game.BoardView.AnimateHideTiles();
            Game.RaiseLevelWon(stars);
        }
    }
}
