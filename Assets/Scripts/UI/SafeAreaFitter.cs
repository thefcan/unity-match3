using UnityEngine;

namespace Match3.UI
{
    /// <summary>
    /// Pins this RectTransform to <see cref="Screen.safeArea"/> so HUD elements never
    /// sit under a notch, camera cutout or gesture bar. Attach to a full-stretch child
    /// of the Canvas and parent the HUD inside it. Re-applies automatically when the
    /// safe area or resolution changes (foldables, split screen); the per-frame check
    /// is two struct compares — no allocation.
    /// </summary>
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private Rect _appliedSafeArea = new Rect(-1f, -1f, -1f, -1f);
        private Vector2Int _appliedScreen;

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != _appliedSafeArea ||
                Screen.width != _appliedScreen.x || Screen.height != _appliedScreen.y)
            {
                Apply();
            }
        }

        private void Apply()
        {
            _appliedSafeArea = Screen.safeArea;
            _appliedScreen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0)
                return;

            var rect = (RectTransform)transform;
            Vector2 min = _appliedSafeArea.position;
            Vector2 max = _appliedSafeArea.position + _appliedSafeArea.size;
            rect.anchorMin = new Vector2(min.x / Screen.width, min.y / Screen.height);
            rect.anchorMax = new Vector2(max.x / Screen.width, max.y / Screen.height);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
