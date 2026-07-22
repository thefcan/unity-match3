using Match3.Game;
using UnityEngine;

namespace Match3.View
{
    /// <summary>
    /// Sizes the orthographic camera so the whole board fits on any portrait screen.
    /// Orthographic size is HALF the visible height, so:
    ///   - to fit the board's width: (boardWidth/2 + padding) / aspect
    ///   - to fit its height (plus HUD headroom): boardHeight/2 + verticalMargin
    /// and we take whichever is larger. Re-fits whenever the resolution changes
    /// (foldables, split screen, editor window resize) — the check is two int compares.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFitter : MonoBehaviour
    {
        [SerializeField] private LevelConfig levelConfig;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private float horizontalPadding = 0.6f;
        [SerializeField] private float verticalMargin = 2.5f;

        private Camera _cam;
        private int _fittedWidth;
        private int _fittedHeight;

        private void Start()
        {
            _cam = GetComponent<Camera>();
            Fit();
        }

        private void Update()
        {
            if (Screen.width != _fittedWidth || Screen.height != _fittedHeight)
                Fit();
        }

        private void Fit()
        {
            _fittedWidth = Screen.width;
            _fittedHeight = Screen.height;

            float fitWidth = (levelConfig.width * cellSize * 0.5f + horizontalPadding) / _cam.aspect;
            float fitHeight = levelConfig.height * cellSize * 0.5f + verticalMargin;

            _cam.orthographicSize = Mathf.Max(fitWidth, fitHeight);
        }
    }
}
