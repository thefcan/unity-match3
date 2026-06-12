using Match3.Game;
using UnityEngine;

namespace Match3.View
{
    /// <summary>
    /// Sizes the orthographic camera so the whole board fits on any portrait screen.
    /// Orthographic size is HALF the visible height, so:
    ///   - to fit the board's width: (boardWidth/2 + padding) / aspect
    ///   - to fit its height (plus HUD headroom): boardHeight/2 + verticalMargin
    /// and we take whichever is larger.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFitter : MonoBehaviour
    {
        [SerializeField] private LevelConfig levelConfig;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private float horizontalPadding = 0.6f;
        [SerializeField] private float verticalMargin = 2.5f;

        private void Start()
        {
            var cam = GetComponent<Camera>();

            float fitWidth = (levelConfig.width * cellSize * 0.5f + horizontalPadding) / cam.aspect;
            float fitHeight = levelConfig.height * cellSize * 0.5f + verticalMargin;

            cam.orthographicSize = Mathf.Max(fitWidth, fitHeight);
        }
    }
}
