using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Match3.View
{
    /// <summary>
    /// Floating "+120" world-space labels, pooled so steady-state cascades allocate
    /// nothing. Runtime-built (3D TextMeshPro, not UGUI) — no canvas or scene wiring.
    /// </summary>
    public sealed class ScorePopup : MonoBehaviour
    {
        private const float RiseDistance = 0.9f;
        private const float Duration = 0.7f;

        private static readonly Stack<ScorePopup> Pool = new Stack<ScorePopup>();

        private TextMeshPro _text;

        public static void Spawn(Vector3 position, int points, Color color)
        {
            ScorePopup popup = Pool.Count > 0 ? Pool.Pop() : Create();
            popup.transform.position = position;
            popup._text.text = $"+{points}";
            popup._text.color = color;
            popup.gameObject.SetActive(true);
            popup.StartCoroutine(popup.RiseAndFade());
        }

        private static ScorePopup Create()
        {
            var go = new GameObject(nameof(ScorePopup));
            var popup = go.AddComponent<ScorePopup>();
            popup._text = go.AddComponent<TextMeshPro>();
            popup._text.fontSize = 5f;
            popup._text.fontStyle = FontStyles.Bold;
            popup._text.alignment = TextAlignmentOptions.Center;
            popup._text.sortingOrder = 20;
            popup._text.rectTransform.sizeDelta = new Vector2(3f, 1f);
            return popup;
        }

        private IEnumerator RiseAndFade()
        {
            Vector3 start = transform.position;
            Color color = _text.color;

            for (float t = 0f; t < 1f; t += Time.deltaTime / Duration)
            {
                transform.position = start + Vector3.up * (RiseDistance * Mathf.SmoothStep(0f, 1f, t));
                _text.color = new Color(color.r, color.g, color.b, 1f - t * t);
                yield return null;
            }

            gameObject.SetActive(false);
            Pool.Push(this);
        }
    }
}
