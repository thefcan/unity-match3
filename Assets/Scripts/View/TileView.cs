using System.Collections;
using Match3.Core;
using UnityEngine;

namespace Match3.View
{
    /// <summary>
    /// The visual for one tile: a SpriteRenderer plus the animations it can perform.
    /// Holds NO game rules — it only knows which logical tile it currently represents
    /// (<see cref="TileId"/>) so BoardView can find it again after falls and swaps.
    /// Instances are reused via <see cref="TilePool"/>, hence Bind/ResetForPool.
    /// </summary>
    public sealed class TileView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        public int TileId { get; private set; }

        private Vector3 _baseScale;
        private Sprite _defaultSprite;
        private Coroutine _hintRoutine;
        private Coroutine _shimmerRoutine;
        private bool _shimmerWanted;
        // Scale animations "claim" the transform; the idle shimmer only breathes
        // while nothing else owns the scale, so tweens never fight each other.
        private int _scaleClaims;

        private void Awake()
        {
            // Remember the prefab's authored scale (e.g. 0.9 for grid gaps) so pop
            // animations and pool reuse can always restore it — and the authored
            // sprite, so tint-only binds can undo a previous candy-sprite bind.
            _baseScale = transform.localScale;
            _defaultSprite = spriteRenderer.sprite;
        }

        /// <summary>Tint-only bind (fallback when no candy sprite exists for the tile).</summary>
        public void Bind(Tile tile, Color color)
        {
            Bind(tile, null, color);
        }

        /// <summary>Re-purposes this (possibly pooled) view for a new logical tile.</summary>
        public void Bind(Tile tile, Sprite sprite, Color color)
        {
            TileId = tile.Id;
            spriteRenderer.sprite = sprite != null ? sprite : _defaultSprite;
            spriteRenderer.color = color;
            transform.localScale = _baseScale;
            transform.localRotation = Quaternion.identity; // a pooled wiggle must never leak a tilt
            _scaleClaims = 0;
            _shimmerWanted = false;
            if (_shimmerRoutine != null)
            {
                StopCoroutine(_shimmerRoutine);
                _shimmerRoutine = null;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            name = $"Tile_{tile.Id}"; // debug-only: SetName is a native call + string alloc per bind
#endif
        }

        /// <summary>
        /// Ambient "I am valuable" breathing for special candies (1.0 → 1.04 sine),
        /// phase-offset per tile so specials never pulse in lockstep. Transform-only:
        /// no material changes, no extra sprites, zero added draw calls. BoardView
        /// decides eligibility; this view stays rule-free.
        /// </summary>
        public void SetSpecialShimmer(bool on)
        {
            _shimmerWanted = on && !Match3.Game.Prefs.ReducedMotionOn;
            if (_shimmerRoutine != null)
            {
                StopCoroutine(_shimmerRoutine);
                _shimmerRoutine = null;
            }
            if (_shimmerWanted && isActiveAndEnabled)
                _shimmerRoutine = StartCoroutine(Shimmer());
        }

        private IEnumerator Shimmer()
        {
            const float frequency = 1.6f;
            const float amplitude = 0.04f;
            float phase = TileId % 7 * 0.35f;
            while (true)
            {
                if (_scaleClaims > 0)
                {
                    yield return null; // someone else owns the scale — breathe later
                    continue;
                }
                phase += Time.deltaTime * frequency * Mathf.PI * 2f;
                transform.localScale = _baseScale * (1f + amplitude * (0.5f + 0.5f * Mathf.Sin(phase)));
                yield return null;
            }
        }

        /// <summary>
        /// Squash-swap-stretch: shrink, take on the new tile's identity and look mid-
        /// squash, then overshoot back — the "a special candy is born" beat.
        /// </summary>
        public IEnumerator MorphTo(Tile tile, Sprite sprite, Color color, float duration)
        {
            _scaleClaims++;
            try
            {
                yield return ScaleTo(_baseScale * 0.35f, duration * 0.35f);

                TileId = tile.Id;
                if (sprite != null) spriteRenderer.sprite = sprite;
                spriteRenderer.color = color;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                name = $"Tile_{tile.Id}";
#endif

                yield return ScaleTo(_baseScale * 1.25f, duration * 0.4f);
                yield return ScaleTo(_baseScale, duration * 0.25f);
            }
            finally
            {
                _scaleClaims--;
            }
        }

        public void ResetForPool()
        {
            StopAllCoroutines();
            _hintRoutine = null;
            _shimmerRoutine = null;
            _pressRoutine = null;
            _shimmerWanted = false;
            _scaleClaims = 0;
            transform.localScale = _baseScale;
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>Vanish (shrink to nothing) — used for the level-transition wipe.</summary>
        public IEnumerator ShrinkOut(float duration)
        {
            _scaleClaims++;
            try
            {
                yield return ScaleTo(Vector3.zero, duration);
            }
            finally
            {
                _scaleClaims--;
            }
        }

        /// <summary>Pop back in (grow from nothing) — the level-transition reveal.</summary>
        public IEnumerator GrowIn(float duration)
        {
            _scaleClaims++;
            try
            {
                transform.localScale = Vector3.zero;
                yield return ScaleTo(_baseScale, duration);
            }
            finally
            {
                _scaleClaims--;
            }
        }

        /// <summary>
        /// Gently pulses the tile's scale to draw the eye, looping until stopped.
        /// Used by the idle hint to highlight a still-available move.
        /// </summary>
        public void StartHintPulse()
        {
            StopHintPulse();
            _scaleClaims++;
            _hintRoutine = StartCoroutine(HintPulse());
        }

        public void StopHintPulse()
        {
            if (_hintRoutine != null)
            {
                StopCoroutine(_hintRoutine);
                _hintRoutine = null;
                _scaleClaims--;
            }
            transform.localScale = _baseScale;
        }

        private IEnumerator HintPulse()
        {
            const float speed = 4f;
            const float amplitude = 0.18f;
            float phase = 0f;
            while (true)
            {
                phase += Time.deltaTime * speed;
                float pulse = 1f + amplitude * (0.5f + 0.5f * Mathf.Sin(phase));
                transform.localScale = _baseScale * pulse;
                yield return null;
            }
        }

        private IEnumerator ScaleTo(Vector3 target, float duration)
        {
            if (duration <= 0f)
            {
                transform.localScale = target;
                yield break;
            }

            Vector3 start = transform.localScale;
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                transform.localScale = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            transform.localScale = target;
        }

        /// <summary>Eased move — a hand-rolled tween (SmoothStep ≈ DOTween's ease in/out) to stay dependency-free.</summary>
        public IEnumerator MoveTo(Vector3 target, float duration)
        {
            if (duration <= 0f)
            {
                transform.position = target;
                yield break;
            }

            Vector3 start = transform.position;
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                transform.position = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
                yield return null; // resume next frame
            }
            transform.position = target;
        }

        /// <summary>
        /// Gravity fall: same duration as MoveTo but with a quadratic ease-in — the
        /// candy accelerates like it's actually dropping — then a detached landing
        /// squash. The squash runs on THIS view's coroutines so pool release kills it.
        /// </summary>
        public IEnumerator FallTo(Vector3 target, float duration)
        {
            if (duration <= 0f)
            {
                transform.position = target;
                yield break;
            }

            Vector3 start = transform.position;
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                transform.position = Vector3.Lerp(start, target, t * t); // ease-in: gravity
                yield return null;
            }
            transform.position = target;

            if (!Match3.Game.Prefs.ReducedMotionOn)
                StartCoroutine(LandingSquash());
        }

        private IEnumerator LandingSquash()
        {
            // Vertical squash on impact, tiny overshoot on the way back — never
            // blocks the wave (the caller already yielded on the fall itself).
            _scaleClaims++;
            try
            {
                Vector3 squashed = new Vector3(_baseScale.x * 1.12f, _baseScale.y * 0.86f, _baseScale.z);
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.05f)
                {
                    transform.localScale = Vector3.Lerp(_baseScale, squashed, t);
                    yield return null;
                }
                Vector3 overshoot = _baseScale * 1.04f;
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.07f)
                {
                    transform.localScale = Vector3.Lerp(squashed, overshoot, Mathf.SmoothStep(0f, 1f, t));
                    yield return null;
                }
                transform.localScale = _baseScale;
            }
            finally
            {
                _scaleClaims--;
            }
        }

        /// <summary>Touch registered: a small press-down. Kept even under reduced
        /// motion — it is the accessibility-critical "I felt that" signal.</summary>
        public void PressIn()
        {
            StopHintPulse();
            ReleasePress();
            _scaleClaims++;
            _pressRoutine = StartCoroutine(ScaleTo(_baseScale * 0.92f, 0.05f));
        }

        public void PressOut()
        {
            ReleasePress();
            _scaleClaims++;
            _pressRoutine = StartCoroutine(PressRelease());
        }

        private Coroutine _pressRoutine;

        private void ReleasePress()
        {
            if (_pressRoutine != null)
            {
                StopCoroutine(_pressRoutine);
                _pressRoutine = null;
                _scaleClaims--;
            }
        }

        private IEnumerator PressRelease()
        {
            yield return ScaleTo(_baseScale * 1.06f, 0.05f);
            yield return ScaleTo(_baseScale, 0.05f);
            ReleasePress();
        }

        /// <summary>Detached "no" head-shake for an invalid swap (z ±5° over 0.12s).</summary>
        public void StartWiggle()
        {
            if (Match3.Game.Prefs.ReducedMotionOn)
                return;
            StartCoroutine(Wiggle());
        }

        private IEnumerator Wiggle()
        {
            const float duration = 0.12f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                float angle = 5f * Mathf.Sin(t * Mathf.PI * 2f); // one full +/- oscillation
                transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                yield return null;
            }
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>Clear effect: briefly bulge, then shrink to nothing.</summary>
        public IEnumerator Pop(float duration)
        {
            Vector3 bulge = _baseScale * 1.25f;
            float bulgeTime = duration * 0.35f;
            float shrinkTime = duration - bulgeTime;

            for (float t = 0f; t < 1f; t += Time.deltaTime / bulgeTime)
            {
                transform.localScale = Vector3.Lerp(_baseScale, bulge, t);
                yield return null;
            }

            for (float t = 0f; t < 1f; t += Time.deltaTime / shrinkTime)
            {
                transform.localScale = Vector3.Lerp(bulge, Vector3.zero, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            transform.localScale = Vector3.zero;
        }
    }
}
