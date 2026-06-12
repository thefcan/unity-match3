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
            if (Input.GetMouseButtonDown(0))
            {
                _pressedWorld = PointerWorldPosition();
                _pressedCell = boardView.WorldToGrid(_pressedWorld);
            }
            else if (Input.GetMouseButton(0) && _pressedCell is { } from)
            {
                Vector3 drag = PointerWorldPosition() - _pressedWorld;
                if (drag.magnitude < dragThreshold)
                    return;

                GridPosition to = Mathf.Abs(drag.x) > Mathf.Abs(drag.y)
                    ? from.Offset(drag.x > 0 ? 1 : -1, 0)
                    : from.Offset(0, drag.y > 0 ? 1 : -1);

                _pressedCell = null; // one swap per press
                SwapRequested?.Invoke(from, to);
            }
            else if (Input.GetMouseButtonUp(0))
            {
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
