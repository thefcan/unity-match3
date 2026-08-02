using System.Collections;
using System.Collections.Generic;
using Match3.Game;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Match3.UI
{
    /// <summary>
    /// The house UI tween kit: panel opens and closes, the shared pop curve,
    /// canvas fades and score count-ups. Everything runs on UNSCALED time (the
    /// pause panel lives at Time.timeScale = 0) and on the CALLER's MonoBehaviour,
    /// so a dying panel takes its tweens with it. Reduced-motion policy is baked
    /// in: scale beats turn instant, informative fades just run at half length.
    /// </summary>
    public static class UiTween
    {
        private const float OpenDuration = 0.18f;
        private const float OpenFade = 0.15f;
        private const float CloseFade = 0.12f;

        // One live open/close tween per panel root, so re-toggling mid-fade
        // replaces the old tween instead of letting two fades fight the alpha.
        private static readonly Dictionary<GameObject, Coroutine> Live = new Dictionary<GameObject, Coroutine>();

        /// <summary>
        /// Activates the panel and plays fade-in + card SoftOpen. Raycasts block
        /// from frame one — the tween is dressing, never an input gap. Falls back
        /// to an instant show when the host can't run coroutines yet (build-time
        /// calls on an inactive hierarchy) or under reduced motion.
        /// </summary>
        public static void OpenPanel(MonoBehaviour host, GameObject root, Transform card)
        {
            CanvasGroup group = EnsureGroup(root);
            Stop(host, root);
            root.SetActive(true);
            group.blocksRaycasts = true;

            if (host == null || !host.isActiveAndEnabled || Prefs.ReducedMotionOn)
            {
                group.alpha = 1f;
                if (card != null) card.localScale = Vector3.one;
                return;
            }

            group.alpha = 0f;
            if (card != null) card.localScale = Vector3.one * 0.9f;
            Live[root] = host.StartCoroutine(OpenRoutine(root, group, card));
        }

        /// <summary>Fades the panel out, then deactivates. Instant in the same fallback cases as OpenPanel.</summary>
        public static void ClosePanel(MonoBehaviour host, GameObject root)
        {
            if (root == null || !root.activeSelf)
                return;

            Stop(host, root);
            CanvasGroup group = EnsureGroup(root);
            if (host == null || !host.isActiveAndEnabled || Prefs.ReducedMotionOn)
            {
                group.alpha = 1f;
                root.SetActive(false);
                return;
            }

            group.blocksRaycasts = false; // a closing panel shouldn't eat one last tap
            Live[root] = host.StartCoroutine(CloseRoutine(root, group));
        }

        private static IEnumerator OpenRoutine(GameObject root, CanvasGroup group, Transform card)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / OpenDuration)
            {
                group.alpha = Mathf.Clamp01(t * OpenDuration / OpenFade);
                if (card != null)
                    card.localScale = Vector3.one * SoftOpenScale(t);
                yield return null;
            }
            group.alpha = 1f;
            if (card != null) card.localScale = Vector3.one;
            Live.Remove(root);
        }

        private static IEnumerator CloseRoutine(GameObject root, CanvasGroup group)
        {
            float from = group.alpha;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / CloseFade)
            {
                group.alpha = Mathf.Lerp(from, 0f, t);
                yield return null;
            }
            root.SetActive(false);
            group.alpha = 1f;             // reset — the next open (or a plain SetActive) starts fresh
            group.blocksRaycasts = true;
            Live.Remove(root);
        }

        /// <summary>0.9 → 1.06 → 1.0 — the soft "card takes the stage" curve.</summary>
        private static float SoftOpenScale(float t)
        {
            return t < 0.6f
                ? Mathf.Lerp(0.9f, 1.06f, Mathf.SmoothStep(0f, 1f, t / 0.6f))
                : Mathf.Lerp(1.06f, 1f, (t - 0.6f) / 0.4f);
        }

        /// <summary>The house pop: 0 → 1.25 at 70% → settle at 1 (the star-pip curve, shared).</summary>
        public static IEnumerator ScalePop(Transform target, float duration = 0.22f)
        {
            if (Prefs.ReducedMotionOn || duration <= 0f)
            {
                target.localScale = Vector3.one;
                yield break;
            }
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / duration)
            {
                float scale = t < 0.7f ? Mathf.Lerp(0f, 1.25f, t / 0.7f) : Mathf.Lerp(1.25f, 1f, (t - 0.7f) / 0.3f);
                target.localScale = Vector3.one * scale;
                yield return null;
            }
            target.localScale = Vector3.one;
        }

        /// <summary>Plain alpha fade. Informative, so reduced motion keeps it — at half length.</summary>
        public static IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
        {
            if (Prefs.ReducedMotionOn)
                duration *= 0.5f;
            if (duration > 0f)
            {
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / duration)
                {
                    group.alpha = Mathf.Lerp(from, to, t);
                    yield return null;
                }
            }
            group.alpha = to;
        }

        /// <summary>HudView.TweenScoreTo's UI twin — unscaled, and always lands exactly on target.</summary>
        public static IEnumerator CountUp(TMP_Text text, int from, int to, float duration, string format = "{0:N0}")
        {
            if (Prefs.ReducedMotionOn)
                duration *= 0.5f;
            if (duration > 0f)
            {
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / duration)
                {
                    int value = Mathf.RoundToInt(Mathf.Lerp(from, to, t));
                    text.text = string.Format(System.Globalization.CultureInfo.InvariantCulture, format, value);
                    yield return null;
                }
            }
            text.text = string.Format(System.Globalization.CultureInfo.InvariantCulture, format, to);
        }

        private static CanvasGroup EnsureGroup(GameObject root)
        {
            var group = root.GetComponent<CanvasGroup>();
            return group != null ? group : root.AddComponent<CanvasGroup>();
        }

        private static void Stop(MonoBehaviour host, GameObject root)
        {
            if (Live.TryGetValue(root, out Coroutine tween))
            {
                Live.Remove(root);
                if (host != null && host.isActiveAndEnabled && tween != null)
                    host.StopCoroutine(tween);
            }

            if (Live.Count > 24) // scene changes leave dead keys behind — sweep rarely
            {
                var dead = new List<GameObject>();
                foreach (GameObject key in Live.Keys)
                    if (key == null)
                        dead.Add(key);
                foreach (GameObject key in dead)
                    Live.Remove(key);
            }
        }
    }

    /// <summary>
    /// Tactile press for every runtime-built button: 0.94 squeeze on pointer-down,
    /// a 1.04 overshoot on release. Purely cosmetic — Button.onClick is untouched,
    /// and the press-down stays even under reduced motion (it is the "I felt that"
    /// acknowledgement, mirroring TileView.PressIn).
    /// </summary>
    public sealed class PressableButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private Vector3 _baseScale;
        private Coroutine _tween;
        private bool _down;

        private void Awake() => _baseScale = transform.localScale;

        public void OnPointerDown(PointerEventData eventData)
        {
            _down = true;
            Play(ScaleTo(_baseScale * 0.94f, 0.06f));
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_down)
                return;
            _down = false;
            if (Prefs.ReducedMotionOn)
            {
                Snap();
                return;
            }
            Play(ReleaseCurve());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_down)
                return; // finger slid off — quietly return to rest
            _down = false;
            Snap();
        }

        private void OnDisable()
        {
            _tween = null;
            _down = false;
            transform.localScale = _baseScale;
        }

        private void Play(IEnumerator routine)
        {
            if (_tween != null)
                StopCoroutine(_tween);
            _tween = StartCoroutine(routine);
        }

        private void Snap()
        {
            if (_tween != null)
                StopCoroutine(_tween);
            _tween = null;
            transform.localScale = _baseScale;
        }

        private IEnumerator ReleaseCurve()
        {
            yield return ScaleTo(_baseScale * 1.04f, 0.05f);
            yield return ScaleTo(_baseScale, 0.05f);
            _tween = null;
        }

        private IEnumerator ScaleTo(Vector3 target, float duration)
        {
            Vector3 start = transform.localScale;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / duration)
            {
                transform.localScale = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            transform.localScale = target;
        }
    }
}
