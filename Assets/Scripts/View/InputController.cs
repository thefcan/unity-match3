using System;
using Match3.Core;
using UnityEngine;

namespace Match3.View
{
    /// <summary>
    /// Translates pointer gestures into ONE high-level event: "the player wants to
    /// swap these two cells". It never checks game rules or game phase — it always
    /// reports, and the current GameState decides whether the request means anything.
    /// That keeps input dumb and the decision-making in exactly one place.
    ///
    /// Gesture: press on a tile, drag past a small threshold; the dominant axis of
    /// the drag picks the neighbour. Works with mouse and (via Unity's built-in
    /// mouse emulation) single-finger touch.
    /// </summary>
    public sealed class InputController : MonoBehaviour
    {
        [SerializeField] private BoardView boardView;
        [Tooltip("How far (world units) the pointer must travel before it counts as a swipe.")]
        [SerializeField] private float dragThreshold = 0.35f;

        public event Action<GridPosition, GridPosition> SwapRequested;

        /// <summary>Press + release on one cell without crossing the drag threshold (booster targeting).</summary>
        public event Action<GridPosition> TapRequested;

        private Camera _camera;
        private GridPosition? _pressedCell;
        private Vector3 _pressedWorld;

        private void Awake()
        {
            // Camera.main does a tag search — fine once, wasteful every frame.
            _camera = Camera.main;
        }

        private void Update()
        {
            // Deliberately NOT an else-if chain: a press and release can land in the
            // SAME frame (fast taps, synthetic input) — the chain swallowed the Up
            // and the tap was lost with _pressedCell left stuck.
            if (Input.GetMouseButtonDown(0))
            {
                // Presses on UI (booster tray, pause button) must not leak onto the
                // board — an armed hammer would smash whatever sat behind the button.
                if (UnityEngine.EventSystems.EventSystem.current != null &&
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                    return;

                _pressedWorld = PointerWorldPosition();
                _pressedCell = boardView.WorldToGrid(_pressedWorld);
            }

            if (Input.GetMouseButton(0) && _pressedCell is { } from)
            {
                Vector3 drag = PointerWorldPosition() - _pressedWorld;
                if (drag.magnitude >= dragThreshold)
                {
                    GridPosition to = Mathf.Abs(drag.x) > Mathf.Abs(drag.y)
                        ? from.Offset(drag.x > 0 ? 1 : -1, 0)
                        : from.Offset(0, drag.y > 0 ? 1 : -1);

                    _pressedCell = null; // one swap per press
                    SwapRequested?.Invoke(from, to);
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                // A release that never crossed the drag threshold is a TAP.
                if (_pressedCell is { } cell &&
                    (PointerWorldPosition() - _pressedWorld).magnitude < dragThreshold)
                    TapRequested?.Invoke(cell);
                _pressedCell = null;
            }
        }

        private Vector3 PointerWorldPosition()
        {
            Vector3 world = _camera.ScreenToWorldPoint(Input.mousePosition);
            world.z = 0f;
            return world;
        }
    }
}
