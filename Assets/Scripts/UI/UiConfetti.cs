using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.UI
{
    /// <summary>
    /// The win-card celebration: two fans of candy-coloured dots from the card's
    /// top corners. UI Images on purpose — world-space particles render BEHIND a
    /// screen-space canvas. ONE driver coroutine moves every piece using
    /// pre-allocated state arrays: no per-piece coroutines, no per-burst allocs.
    /// Lives inside the result panel's overlay, so hiding the panel kills it.
    /// </summary>
    public sealed class UiConfetti : MonoBehaviour
    {
        private const int MaxPieces = 24;
        private const float Life = 1.2f;

        private readonly RectTransform[] _pieces = new RectTransform[MaxPieces];
        private readonly Image[] _images = new Image[MaxPieces];
        private readonly Vector2[] _velocities = new Vector2[MaxPieces];
        private readonly float[] _spins = new float[MaxPieces];
        private readonly float[] _lives = new float[MaxPieces];
        private readonly Vector3[] _corners = new Vector3[4];
        private Coroutine _driver;

        /// <summary>Builds the (initially dormant) piece pool under <paramref name="overlay"/>.</summary>
        public static UiConfetti Attach(Transform overlay)
        {
            var go = new GameObject(nameof(UiConfetti), typeof(RectTransform));
            go.transform.SetParent(overlay, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var confetti = go.AddComponent<UiConfetti>();
            confetti.BuildPieces();
            return confetti;
        }

        private void BuildPieces()
        {
            for (int i = 0; i < MaxPieces; i++)
            {
                var pieceGo = new GameObject($"Piece{i}", typeof(RectTransform), typeof(Image));
                pieceGo.transform.SetParent(transform, false);
                _pieces[i] = (RectTransform)pieceGo.transform;
                _images[i] = pieceGo.GetComponent<Image>();
                UiTheme.ApplySprite(_images[i], UiTheme.Round, Color.white);
                _images[i].raycastTarget = false;
                pieceGo.SetActive(false);
            }
        }

        /// <summary>Launches a burst from the card's two top corners (reduced motion: a third of the pieces).</summary>
        public void Burst(RectTransform card)
        {
            int count = Match3.Game.Prefs.ReducedMotionOn ? MaxPieces / 3 : MaxPieces;

            // The card's top corners in THIS rect's local space — via world corners,
            // because the card may still be mid-SoftOpen scale.
            card.GetWorldCorners(_corners); // 0=BL 1=TL 2=TR 3=BR
            Vector2 left = transform.InverseTransformPoint(_corners[1]);
            Vector2 right = transform.InverseTransformPoint(_corners[2]);

            for (int i = 0; i < count; i++)
            {
                bool fromLeft = (i & 1) == 0;
                float angle = 90f + Random.Range(-70f, 70f); // a fan around straight up
                float speed = Random.Range(650f, 1150f);
                _velocities[i] = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * speed;
                _spins[i] = Random.Range(-540f, 540f);
                _lives[i] = Life;
                _pieces[i].anchoredPosition = fromLeft ? left : right;
                _pieces[i].localRotation = Quaternion.identity;
                float size = Random.Range(18f, 32f);
                _pieces[i].sizeDelta = new Vector2(size, size);
                _images[i].color = UiTheme.CandyColors[Random.Range(0, UiTheme.CandyColors.Length)];
                _pieces[i].gameObject.SetActive(true);
            }
            for (int i = count; i < MaxPieces; i++)
            {
                _lives[i] = 0f;
                _pieces[i].gameObject.SetActive(false);
            }

            if (_driver == null)
                _driver = StartCoroutine(Drive());
        }

        private IEnumerator Drive()
        {
            const float gravity = -2600f; // canvas pixels/s² — a quick, cartoonish fall
            int alive = 1;
            while (alive > 0)
            {
                alive = 0;
                float dt = Time.unscaledDeltaTime;
                for (int i = 0; i < MaxPieces; i++)
                {
                    if (_lives[i] <= 0f)
                        continue;
                    _lives[i] -= dt;
                    if (_lives[i] <= 0f)
                    {
                        _pieces[i].gameObject.SetActive(false);
                        continue;
                    }
                    alive++;
                    _velocities[i].y += gravity * dt;
                    _pieces[i].anchoredPosition += _velocities[i] * dt;
                    _pieces[i].Rotate(0f, 0f, _spins[i] * dt);
                    Color color = _images[i].color;
                    color.a = Mathf.Clamp01(_lives[i] / 0.3f); // the last 0.3s fades out
                    _images[i].color = color;
                }
                yield return null;
            }
            _driver = null;
        }

        private void OnDisable()
        {
            // The overlay hid (or the scene is going away) — coroutines are already
            // dead, so make the bookkeeping match.
            _driver = null;
            for (int i = 0; i < MaxPieces; i++)
            {
                _lives[i] = 0f;
                if (_pieces[i] != null)
                    _pieces[i].gameObject.SetActive(false);
            }
        }
    }
}
