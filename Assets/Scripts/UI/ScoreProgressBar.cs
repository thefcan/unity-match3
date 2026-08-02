using System.Collections;
using Match3.Core;
using Match3.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.UI
{
    /// <summary>
    /// The thin gold score bar hugging the HUD top card's bottom edge (Stitch
    /// "Luminous Confection" design): dark pill track, gold fill, and a small
    /// right-aligned "1,640 / 1,670" caption. In moves mode it tracks the level's
    /// Score objective — the objective chips deliberately skip that goal (see
    /// <see cref="ObjectiveBarView"/>) so its long counter never crowds a pill.
    /// In time attack it tracks the rolling level target. Pure observer.
    /// </summary>
    public sealed class ScoreProgressBar : MonoBehaviour
    {
        private GameManager _game;
        private RectTransform _fill;
        private TMP_Text _caption;
        private Coroutine _glide;
        private float _shownFraction; // what the fill currently displays (the glide's "from")

        /// <summary>Builds the track + fill + caption inside <paramref name="topBar"/>.</summary>
        public static ScoreProgressBar Attach(Transform topBar, GameManager game)
        {
            var trackGo = new GameObject("ScoreTrack", typeof(RectTransform), typeof(Image));
            trackGo.transform.SetParent(topBar, false);
            trackGo.SetActive(false); // OnEnable must fire with _game already wired
            var track = (RectTransform)trackGo.transform;
            track.anchorMin = new Vector2(0f, 0f);
            track.anchorMax = new Vector2(1f, 0f);
            track.pivot = new Vector2(0.5f, 0f);
            track.sizeDelta = new Vector2(-64f, 18f);
            track.anchoredPosition = new Vector2(0f, 30f);
            var trackImage = trackGo.GetComponent<Image>();
            UiTheme.ApplySprite(trackImage, UiTheme.Pill, UiTheme.Slot);
            trackImage.raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(trackGo.transform, false);
            var fill = (RectTransform)fillGo.transform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f); // Refresh drives anchorMax.x — no width math
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            var fillImage = fillGo.GetComponent<Image>();
            UiTheme.ApplySprite(fillImage, UiTheme.Pill, UiTheme.Gold);
            fillImage.raycastTarget = false;

            var captionGo = new GameObject("Caption", typeof(RectTransform));
            captionGo.transform.SetParent(topBar, false);
            var captionRect = (RectTransform)captionGo.transform;
            captionRect.anchorMin = captionRect.anchorMax = new Vector2(1f, 0f);
            captionRect.pivot = new Vector2(1f, 0f);
            captionRect.sizeDelta = new Vector2(430f, 34f);
            captionRect.anchoredPosition = new Vector2(-34f, 54f);
            var caption = captionGo.AddComponent<TextMeshProUGUI>();
            caption.fontSize = 26f;
            caption.alignment = TextAlignmentOptions.MidlineRight;
            caption.color = UiTheme.TextDim;
            caption.enableWordWrapping = false;
            caption.raycastTarget = false;
            UiTheme.ApplyFont(caption, UiTheme.BodyFont);

            var bar = trackGo.AddComponent<ScoreProgressBar>();
            bar._game = game;
            bar._fill = fill;
            bar._caption = caption;
            trackGo.SetActive(true);
            return bar;
        }

        private void OnEnable()
        {
            if (_game == null) return;
            _game.ScoreChanged += HandleScoreChanged;
            _game.ObjectivesChanged += Refresh;
            _game.LevelChanged += HandleLevelChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (_game == null) return;
            _game.ScoreChanged -= HandleScoreChanged;
            _game.ObjectivesChanged -= Refresh;
            _game.LevelChanged -= HandleLevelChanged;
        }

        private void HandleScoreChanged(int score) => Refresh();
        private void HandleLevelChanged(int level) => Refresh();

        private void Refresh()
        {
            int current = _game.Score;
            int target = 0;

            if (_game.Mode == GameMode.Moves)
            {
                ObjectiveTracker tracker = _game.Objectives;
                if (tracker != null)
                {
                    for (int i = 0; i < tracker.Count; i++)
                    {
                        if (tracker.At(i).Type != ObjectiveType.Score)
                            continue;
                        target = tracker.At(i).TargetAmount;
                        current = tracker.Progress(i);
                        break;
                    }
                }
            }
            else
            {
                target = _game.CurrentTarget; // time attack: the rolling level target
            }

            if (target <= 0)
            {
                // No score goal this level — a quiet empty track, no caption.
                Snap(0f);
                _caption.text = string.Empty;
                return;
            }

            float fraction = Mathf.Clamp01(current / (float)target);
            if (fraction > 0f)
                fraction = Mathf.Max(fraction, 0.05f); // a sliced pill needs a sliver to read as round
            // Caption tells the truth instantly; only the fill glides. Progress
            // never animates DOWNWARD (restart/new level) — that snaps, mirroring
            // HudView's score==0 rule.
            _caption.text = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0:N0} / {1:N0}", current, target);
            if (fraction < _shownFraction || Prefs.ReducedMotionOn)
                Snap(fraction);
            else if (fraction > _shownFraction)
            {
                StopGlide();
                _glide = StartCoroutine(GlideTo(fraction));
            }
        }

        private void Snap(float fraction)
        {
            StopGlide();
            _shownFraction = fraction;
            _fill.anchorMax = new Vector2(fraction, 1f);
        }

        private IEnumerator GlideTo(float target)
        {
            float from = _shownFraction;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.3f)
            {
                _shownFraction = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t));
                _fill.anchorMax = new Vector2(_shownFraction, 1f);
                yield return null;
            }
            _glide = null;
            Snap(target);
        }

        private void StopGlide()
        {
            if (_glide != null)
            {
                StopCoroutine(_glide);
                _glide = null;
            }
        }
    }
}
