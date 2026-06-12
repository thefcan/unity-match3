using Match3.Game;
using TMPro;
using UnityEngine;

namespace Match3.UI
{
    /// <summary>
    /// Heads-up display: score, remaining moves and the target. A pure OBSERVER —
    /// it reacts to GameManager's events and never calls into game logic. Delete
    /// this object from the scene and the game still runs; that's the decoupling test.
    ///
    /// The score label doesn't snap to new values: a small coroutine counts the
    /// displayed number up (restarting smoothly if more points land mid-tween),
    /// which makes cascades feel rewarding.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text movesText;
        [SerializeField] private TMP_Text targetText;
        [SerializeField] private float scoreTweenDuration = 0.4f;

        private int _displayedScore;
        private Coroutine _scoreTween;

        private void OnEnable()
        {
            gameManager.ScoreChanged += HandleScoreChanged;
            gameManager.MovesChanged += HandleMovesChanged;
        }

        private void OnDisable()
        {
            gameManager.ScoreChanged -= HandleScoreChanged;
            gameManager.MovesChanged -= HandleMovesChanged;
        }

        private void Start()
        {
            targetText.text = $"Target: {gameManager.Level.targetScore}";
        }

        private void HandleMovesChanged(int movesLeft)
        {
            movesText.text = $"Moves: {movesLeft}";
        }

        private void HandleScoreChanged(int score)
        {
            if (score == 0) // fresh game — snap, don't tween downwards
            {
                StopTween();
                _displayedScore = 0;
                scoreText.text = "0";
                return;
            }

            StopTween();
            _scoreTween = StartCoroutine(TweenScoreTo(score));
        }

        private System.Collections.IEnumerator TweenScoreTo(int target)
        {
            int from = _displayedScore;
            for (float t = 0f; t < 1f; t += Time.deltaTime / scoreTweenDuration)
            {
                _displayedScore = Mathf.RoundToInt(Mathf.Lerp(from, target, t));
                scoreText.text = _displayedScore.ToString();
                yield return null;
            }

            _displayedScore = target;
            scoreText.text = target.ToString();
        }

        private void StopTween()
        {
            if (_scoreTween != null)
            {
                StopCoroutine(_scoreTween);
                _scoreTween = null;
            }
        }
    }
}
