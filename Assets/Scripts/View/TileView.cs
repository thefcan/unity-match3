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
            name = $"Tile_{tile.Id}";
        }

        /// <summary>
        /// Squash-swap-stretch: shrink, take on the new tile's identity and look mid-
        /// squash, then overshoot back — the "a special candy is born" beat.
        /// </summary>
        public IEnumerator MorphTo(Tile tile, Sprite sprite, Color color, float duration)
        {
            yield return ScaleTo(_baseScale * 0.35f, duration * 0.35f);

            TileId = tile.Id;
            if (sprite != null) spriteRenderer.sprite = sprite;
            spriteRenderer.color = color;
            name = $"Tile_{tile.Id}";

            yield return ScaleTo(_baseScale * 1.25f, duration * 0.4f);
            yield return ScaleTo(_baseScale, duration * 0.25f);
        }

        public void ResetForPool()
        {
            StopAllCoroutines();
            _hintRoutine = null;
            transform.localScale = _baseScale;
        }

        /// <summary>Vanish (shrink to nothing) — used for the level-transition wipe.</summary>
        public IEnumerator ShrinkOut(float duration)
        {
            yield return ScaleTo(Vector3.zero, duration);
        }

        /// <summary>Pop back in (grow from nothing) — the level-transition reveal.</summary>
        public IEnumerator GrowIn(float duration)
        {
            transform.localScale = Vector3.zero;
            yield return ScaleTo(_baseScale, duration);
        }

        /// <summary>
        /// Gently pulses the tile's scale to draw the eye, looping until stopped.
        /// Used by the idle hint to highlight a still-available move.
        /// </summary>
        public void StartHintPulse()
        {
            StopHintPulse();
            _hintRoutine = StartCoroutine(HintPulse());
        }

        public void StopHintPulse()
        {
            if (_hintRoutine != null)
            {
                StopCoroutine(_hintRoutine);
                _hintRoutine = null;
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
