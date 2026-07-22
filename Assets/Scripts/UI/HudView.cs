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

        private static readonly string[] ColorNames = { "Red", "Green", "Blue", "Yellow", "Purple" };

        private int _displayedScore;
        private Coroutine _scoreTween;
        private float _bonusFlashUntil;
        private int _shownTenths = int.MinValue;

        private void Awake()
        {
            // Apply the Figma design language (UiTheme fonts) to the scene-authored
            // labels — a code-side restyle, so the scene needs no re-wiring.
            UiTheme.ApplyFont(scoreText, UiTheme.ButtonFont);
            UiTheme.ApplyFont(timeText, UiTheme.ButtonFont);
            UiTheme.ApplyFont(targetText, UiTheme.BodyFont);
            UiTheme.ApplyFont(levelText, UiTheme.BodyFont);
            UiTheme.ApplyFont(messageText, UiTheme.TitleFont);
            if (levelText != null)
                levelText.color = UiTheme.TextDim;

            // The score and clock change many times a second; a nested Canvas confines
            // their mesh rebuilds to a tiny canvas instead of dirtying the whole HUD.
            IsolateFrequentText(scoreText);
            IsolateFrequentText(timeText);
        }

        private static void IsolateFrequentText(TMP_Text text)
        {
            if (text != null && text.GetComponent<Canvas>() == null)
                text.gameObject.AddComponent<Canvas>();
        }

        private void OnEnable()
        {
            gameManager.ScoreChanged += HandleScoreChanged;
            gameManager.LevelChanged += HandleLevelChanged;
            gameManager.TimeBonusAwarded += HandleTimeBonus;
            gameManager.LevelCompleted += HandleLevelCompleted;
            gameManager.ShuffleStarted += HandleShuffleStarted;
            gameManager.PhaseChanged += HandlePhaseChanged;
            gameManager.MovesChanged += HandleMovesChanged;
            gameManager.ObjectivesChanged += HandleObjectivesChanged;
        }

        private void OnDisable()
        {
            gameManager.ScoreChanged -= HandleScoreChanged;
            gameManager.LevelChanged -= HandleLevelChanged;
            gameManager.TimeBonusAwarded -= HandleTimeBonus;
            gameManager.LevelCompleted -= HandleLevelCompleted;
            gameManager.ShuffleStarted -= HandleShuffleStarted;
            gameManager.PhaseChanged -= HandlePhaseChanged;
            gameManager.MovesChanged -= HandleMovesChanged;
            gameManager.ObjectivesChanged -= HandleObjectivesChanged;
        }

        private void Update()
        {
            // The clock label doubles as the MOVES counter in moves mode (no scene
            // change needed) — moves update via events, so Update leaves it alone.
            if (gameManager.Mode == GameMode.Moves)
                return;

            // The clock changes every frame, so the HUD polls it instead of making the
            // GameManager fire 60 events a second. Discrete values come via events.
            // Rebuild the label only when the displayed tenth changes — 10 string
            // allocs + canvas dirties per second instead of one per frame.
            float seconds = Mathf.Max(0f, gameManager.TimeLeft);
            int tenths = Mathf.FloorToInt(seconds * 10f);
            if (tenths != _shownTenths)
            {
                _shownTenths = tenths;
                timeText.text = $"{tenths * 0.1f:0.0}s";
            }

            if (Time.time < _bonusFlashUntil)
                timeText.color = bonusFlashColor;
            else
                timeText.color = seconds <= lowTimeThreshold ? lowTimeColor : normalTimeColor;
        }

        private void HandleMovesChanged(int movesLeft)
        {
            if (gameManager.Mode != GameMode.Moves)
                return;

            _shownTenths = int.MinValue; // the clock label was repurposed — drop its cache
            timeText.text = $"Moves {movesLeft}";
            timeText.color = movesLeft <= 3 ? lowTimeColor : normalTimeColor;
        }

        private void HandleObjectivesChanged()
        {
            if (gameManager.Mode != GameMode.Moves || gameManager.Objectives == null)
                return;

            // The icon chips (ObjectiveBarView) supersede the text summary when present.
            // Presence-based rather than a static flag: the bar attaches AFTER this view
            // subscribes, so a flag would lag one event behind (text + chips both visible).
            targetText.text = ObjectiveBarPresent ? string.Empty : ObjectiveSummary(gameManager.Objectives);
        }

        private ObjectiveBarView _objectiveBar;

        private bool ObjectiveBarPresent
        {
            get
            {
                if (_objectiveBar == null)
                    _objectiveBar = FindObjectOfType<ObjectiveBarView>();
                return _objectiveBar != null;
            }
        }

        /// <summary>"Score 450/600 | Red 12/30" — one entry per objective (fallback when no icon bar).</summary>
        private static string ObjectiveSummary(Match3.Core.ObjectiveTracker tracker)
        {
            var parts = new System.Text.StringBuilder();
            for (int i = 0; i < tracker.Count; i++)
            {
                if (i > 0) parts.Append("  |  ");

                Match3.Core.Objective objective = tracker.At(i);
                string label = objective.Type switch
                {
                    Match3.Core.ObjectiveType.Score => "Score",
                    Match3.Core.ObjectiveType.ClearJelly => "Jelly",
                    Match3.Core.ObjectiveType.ClearChocolate => "Chocolate",
                    Match3.Core.ObjectiveType.CollectIngredients => "Ingredients",
                    _ => ColorNames[Mathf.Clamp(objective.ColorIndex, 0, ColorNames.Length - 1)],
                };

                parts.Append($"{label} {tracker.Progress(i)}/{objective.TargetAmount}");
            }
            return parts.ToString();
        }

        private void HandleLevelChanged(int level)
        {
            levelText.text = $"Level {level}";
            if (gameManager.Mode == GameMode.Moves && gameManager.Objectives != null)
                targetText.text = ObjectiveBarPresent ? string.Empty : ObjectiveSummary(gameManager.Objectives);
            else
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
