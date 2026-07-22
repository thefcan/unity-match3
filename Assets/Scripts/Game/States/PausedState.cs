using UnityEngine;

namespace Match3.Game
{
    /// <summary>
    /// Pause: freezes gameplay time (all tweens/coroutines are deltaTime-driven) and
    /// audio, ignores swaps. Only reachable FROM Playing (see GameManager.TogglePause)
    /// so no animation is ever frozen half-done. Exit() restores time even when the
    /// next state isn't Playing — restart from the pause menu goes through here too.
    /// </summary>
    public sealed class PausedState : GameState
    {
        public PausedState(GameManager game) : base(game) { }

        public override GamePhase Phase => GamePhase.Paused;

        public override void Enter()
        {
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        public override void Exit()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }
}
