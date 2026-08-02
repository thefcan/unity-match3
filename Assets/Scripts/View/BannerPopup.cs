using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Match3.View
{
    /// <summary>
    /// The cascade-celebration word ("SWEET!" → "DELICIOUS!") popping over the
    /// board, one per wave depth. Pooled like ScorePopup (same dead-instance
    /// drain — the static pool outlives scenes, its objects do not). At most one
    /// banner lives at a time: a deeper wave replaces the current word.
    /// </summary>
    public sealed class BannerPopup : MonoBehaviour
    {
        private static readonly Stack<BannerPopup> Pool = new Stack<BannerPopup>();
        private static BannerPopup _live;

        private TextMeshPro _text;
        private Coroutine _routine;

        /// <summary>The word for a cascade depth; null below the celebration floor.</summary>
        public static string TextFor(int cascadeIndex)
        {
            switch (cascadeIndex)
            {
                case 0:
                case 1: return null;
                case 2: return "SWEET!";
                case 3: return "TASTY!";
                case 4: return "DIVINE!";
                default: return "DELICIOUS!";
            }
        }

        public static void Spawn(Vector3 position, string text)
        {
            if (_live != null)
                _live.Finish(); // one banner max — the deeper wave takes the stage

            BannerPopup banner = null;
            while (Pool.Count > 0 && banner == null)
                banner = Pool.Pop(); // drain destroyed instances (the ScorePopup lesson)
            if (banner == null)
                banner = Create();

            _live = banner;
            banner.transform.position = position;
            banner.transform.localScale = Vector3.zero;
            banner._text.text = text;
            banner.gameObject.SetActive(true);
            banner._routine = banner.StartCoroutine(banner.Play());
        }

        private static BannerPopup Create()
        {
            var go = new GameObject(nameof(BannerPopup));
            var banner = go.AddComponent<BannerPopup>();
            banner._text = go.AddComponent<TextMeshPro>();
            Match3.UI.UiTheme.ApplyFont(banner._text, Match3.UI.UiTheme.TitleFont);
            banner._text.fontSize = 8f;
            banner._text.fontStyle = FontStyles.Bold;
            banner._text.alignment = TextAlignmentOptions.Center;
            banner._text.color = Match3.UI.UiTheme.Gold;
            banner._text.sortingOrder = 25; // over popups and particles
            banner._text.rectTransform.sizeDelta = new Vector2(6f, 1.4f);
            return banner;
        }

        private IEnumerator Play()
        {
            Color gold = Match3.UI.UiTheme.Gold;
            if (Match3.Game.Prefs.ReducedMotionOn)
            {
                // Informative, so it stays — but as a plain fade, no punch or rise.
                transform.localScale = Vector3.one;
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.15f)
                {
                    _text.color = new Color(gold.r, gold.g, gold.b, t);
                    yield return null;
                }
                yield return new WaitForSeconds(0.5f);
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.2f)
                {
                    _text.color = new Color(gold.r, gold.g, gold.b, 1f - t);
                    yield return null;
                }
            }
            else
            {
                _text.color = gold;
                // The house pop: overshoot to 1.25 at 70%, settle to 1.
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.25f)
                {
                    float scale = t < 0.7f ? Mathf.Lerp(0f, 1.25f, t / 0.7f) : Mathf.Lerp(1.25f, 1f, (t - 0.7f) / 0.3f);
                    transform.localScale = Vector3.one * scale;
                    yield return null;
                }
                transform.localScale = Vector3.one;
                yield return new WaitForSeconds(0.35f);
                Vector3 start = transform.position;
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.2f)
                {
                    transform.position = start + Vector3.up * (0.5f * t);
                    _text.color = new Color(gold.r, gold.g, gold.b, 1f - t);
                    yield return null;
                }
            }
            Finish();
        }

        private void Finish()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            if (_live == this)
                _live = null;
            gameObject.SetActive(false);
            Pool.Push(this);
        }
    }
}
