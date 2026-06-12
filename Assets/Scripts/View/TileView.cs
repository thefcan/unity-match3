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

        private void Awake()
        {
            // Remember the prefab's authored scale (e.g. 0.9 for grid gaps) so pop
            // animations and pool reuse can always restore it.
            _baseScale = transform.localScale;
        }

        /// <summary>Re-purposes this (possibly pooled) view for a new logical tile.</summary>
        public void Bind(Tile tile, Color color)
        {
            TileId = tile.Id;
            spriteRenderer.color = color;
            transform.localScale = _baseScale;
            name = $"Tile_{tile.Id}";
        }

        public void ResetForPool()
        {
            StopAllCoroutines();
            transform.localScale = _baseScale;
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
