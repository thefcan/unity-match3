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
            // Sugar Crush: unused moves become striped candies and everything on the
            // board fires. The finale's own scoring REPLACES the old flat
            // moves-remaining bonus — the spectacle IS the bonus now.
            ResolutionResult finale = Game.Resolver.ResolveFinale(Game.Board, Game.MovesLeft);
            if (finale.Steps.Count > 0)
            {
                Game.RaiseFinaleStarted();
                foreach (CascadeStep step in finale.Steps)
                {
                    yield return Game.BoardView.PlayStep(step);
                    Game.AddScore(step.Points);
                    MissionService.ConsumeStep(step); // finale fireworks count too
                    EventService.ConsumeStep(step);
                }
            }

            int stars = StarCalculator.Cap(
                StarCalculator.Calculate(Game.Score, Game.LevelDefinition.starScores),
                Prefs.RelaxedOn);
            ProgressService.RecordWin(Game.Level, stars);
            MetaService.RegisterLevelOutcome(won: true);
            // Event progress writes no file of its own: MissionService.RegisterWin's
            // save (next line) flushes the shared MetaState — keep this line above it.
            EventService.RegisterWin(Game.Level, stars);
            MissionService.RegisterWin();

            yield return Game.BoardView.AnimateHideTiles();
            Game.RaiseLevelWon(stars);
        }
    }
}
