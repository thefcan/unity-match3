using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Match3.UI
{
    /// <summary>
    /// A black curtain over EVERYTHING (its own canvas, sorting order 999) for
    /// scene changes: fade out, load, fade back in. Lazy DontDestroyOnLoad
    /// singleton, AudioManager-style. Raycasts are blocked while the curtain is
    /// up, so a double-tap during the transition dies harmlessly. Reduced motion
    /// HALVES the durations rather than cutting — a hard cut is more jarring
    /// than a brisk fade.
    /// </summary>
    public sealed class ScreenFader : MonoBehaviour
    {
        private static ScreenFader _instance;

        private CanvasGroup _group;
        private bool _busy;

        /// <summary>Fades to black, loads <paramref name="sceneName"/>, fades back in.</summary>
        public static void LoadScene(string sceneName)
        {
            ScreenFader fader = Instance;
            if (fader._busy)
                return; // already mid-transition — the first tap wins
            fader.StartCoroutine(fader.FadeAndLoad(sceneName));
        }

        private static ScreenFader Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject(nameof(ScreenFader));
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<ScreenFader>();
                    _instance.Build();
                }
                return _instance;
            }
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999; // over every panel, veil and popup
            gameObject.AddComponent<GraphicRaycaster>();
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;

            var curtainGo = new GameObject("Curtain", typeof(RectTransform), typeof(Image));
            curtainGo.transform.SetParent(transform, false);
            var rect = (RectTransform)curtainGo.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            curtainGo.GetComponent<Image>().color = Color.black;
        }

        private IEnumerator FadeAndLoad(string sceneName)
        {
            _busy = true;
            _group.blocksRaycasts = true;

            // Unscaled throughout: the pause panel's Level Map exit restores
            // timeScale first, but the curtain must never depend on that.
            float outDuration = Match3.Game.Prefs.ReducedMotionOn ? 0.09f : 0.18f;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / outDuration)
            {
                _group.alpha = t;
                yield return null;
            }
            _group.alpha = 1f;

            SceneManager.LoadScene(sceneName);
            yield return null; // the new scene builds its UI under the curtain

            float inDuration = Match3.Game.Prefs.ReducedMotionOn ? 0.125f : 0.25f;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / inDuration)
            {
                _group.alpha = 1f - t;
                yield return null;
            }
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _busy = false;
        }
    }
}
