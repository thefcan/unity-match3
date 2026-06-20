using Match3.Game;
using TMPro;
using UnityEngine;

namespace Match3.UI
{
    /// <summary>
    /// Heads-up display: score, level, target and the countdown clock. A pure OBSERVER —
    /// it reacts to GameManager's events (and polls the clock) and never calls into game
    /// logic. Delete this object from the scene and the game still runs; that's the
    /// decoupling test.
    ///
    /// The score counts up with a small tween (rewarding on cascades). The clock is read
    /// every frame in <see cref="Update"/> rather than via an event, and turns red when
    /// low and flashes when a big match adds bonus seconds.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text targetText;
        [SerializeField] private TMP_Text levelText;
        [Tooltip("Optional big centre banner for 'Level Complete!'. Leave empty to skip.")]
        [SerializeField] private TMP_Text messageText;

        [Header("Score tween")]
        [SerializeField] private float scoreTweenDuration = 0.4f;

        [Header("Clock colours")]
        [SerializeField] private Color normalTimeColor = Color.white;
        [SerializeField] private Color lowTimeColor = new Color(0.95f, 0.30f, 0.25f);
        [SerializeField] private Color bonusFlashColor = new Color(0.30f, 0.85f, 0.40f);
        [SerializeField] private float lowTimeThreshold = 10f;
        [SerializeField] private float bonusFlashDuration = 0.5f;

        private int _displayedScore;
        private Coroutine _scoreTween;
        private float _bonusFlashUntil;

        private void OnEnable()
        {
            gameManager.ScoreChanged += HandleScoreChanged;
            gameManager.LevelChanged += HandleLevelChanged;
            gameManager.TimeBonusAwarded += HandleTimeBonus;
            gameManager.LevelCompleted += HandleLevelCompleted;
            gameManager.ShuffleStarted += HandleShuffleStarted;
            gameManager.PhaseChanged += HandlePhaseChanged;
        }

        private void OnDisable()
        {
            gameManager.ScoreChanged -= HandleScoreChanged;
            gameManager.LevelChanged -= HandleLevelChanged;
            gameManager.TimeBonusAwarded -= HandleTimeBonus;
            gameManager.LevelCompleted -= HandleLevelCompleted;
            gameManager.ShuffleStarted -= HandleShuffleStarted;
            gameManager.PhaseChanged -= HandlePhaseChanged;
        }

        private void Update()
        {
            // The clock changes every frame, so the HUD polls it instead of making the
            // GameManager fire 60 events a second. Discrete values come via events.
            float seconds = Mathf.Max(0f, gameManager.TimeLeft);
            timeText.text = $"{seconds:0.0}s";

            if (Time.time < _bonusFlashUntil)
                timeText.color = bonusFlashColor;
            else
                timeText.color = seconds <= lowTimeThreshold ? lowTimeColor : normalTimeColor;
        }

        private void HandleLevelChanged(int level)
        {
            levelText.text = $"Level {level}";
            targetText.text = $"Target {gameManager.CurrentTarget}";
        }

        private void HandleLevelCompleted()
        {
            // Fires while the clock is paused, before the level number advances —
            // so gameManager.Level is still the level the player just cleared.
            SetBanner($"Level {gameManager.Level} Complete!");
        }

        private void HandleShuffleStarted()
        {
            SetBanner("No Moves!\nShuffling...");
        }

        private void HandlePhaseChanged(Match3.Game.GamePhase phase)
        {
            // Banners belong to the paused states; clear them the moment play resumes.
            if (phase == Match3.Game.GamePhase.Playing)
                SetBanner(string.Empty);
        }

        private void SetBanner(string text)
        {
            if (messageText != null)
                messageText.text = text;
        }

        private void HandleTimeBonus(float seconds)
        {
            _bonusFlashUntil = Time.time + bonusFlashDuration;
        }

        private void HandleScoreChanged(int score)
        {
            if (score == 0) // fresh level/game — snap, don't tween downwards
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
