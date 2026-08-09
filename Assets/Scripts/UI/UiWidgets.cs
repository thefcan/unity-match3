using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Match3.UI
{
    /// <summary>
    /// Shared runtime widgets — the pieces every panel builds identically. They
    /// live here so a style decision is made ONCE: seven panels used to repeat
    /// the same dozen lines for their Close pill, and two of them had already
    /// drifted (a bigger, bolder, pink one).
    /// </summary>
    public static class UiWidgets
    {
        /// <summary>The house dismiss button: a wide slot-coloured pill with a dim, unshouty label.</summary>
        public static Button ClosePill(Transform parent, Vector2 anchoredPosition, UnityAction onClick,
                                       string label = "Close")
        {
            var go = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(600f, 110f);
            rect.anchoredPosition = anchoredPosition;

            var image = go.GetComponent<Image>();
            UiTheme.ApplySprite(image, UiTheme.Pill, UiTheme.Slot);

            var button = go.GetComponent<Button>();
            go.AddComponent<PressableButton>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.fontSize = 42f;
            text.fontStyle = FontStyles.Normal;
            text.alignment = TextAlignmentOptions.Center;
            text.color = UiTheme.TextDim;
            text.raycastTarget = false;
            text.text = label;
            UiTheme.ApplyFont(text, UiTheme.ButtonFont);
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return button;
        }
    }
}
