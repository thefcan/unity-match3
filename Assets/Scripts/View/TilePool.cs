using System.Collections.Generic;
using UnityEngine;

namespace Match3.View
{
    /// <summary>
    /// OBJECT POOL PATTERN: tile views are recycled instead of destroyed.
    /// Instantiate/Destroy are expensive on mobile (allocation + GC spikes cause
    /// visible frame hitches mid-combo), so cleared tiles go back on a stack and
    /// come out again as "new" tiles at the top of the board. With an 8x8 board the
    /// pool stabilises quickly and steady-state play allocates no tile objects at all.
    /// </summary>
    public sealed class TilePool : MonoBehaviour
    {
        [SerializeField] private TileView tilePrefab;
        [Tooltip("Pre-instantiated on Awake. Board cells + headroom for tiles mid-animation.")]
        [SerializeField] private int initialCapacity = 80;

        private readonly Stack<TileView> _available = new Stack<TileView>();

        private void Awake()
        {
            // Pre-warm during load instead of paying Instantiate cost during play.
            for (int i = 0; i < initialCapacity; i++)
                _available.Push(CreateInstance());
        }

        public TileView Get()
        {
            // Pool empty (e.g. unusually deep cascade)? Grow rather than fail.
            TileView view = _available.Count > 0 ? _available.Pop() : CreateInstance();
            view.gameObject.SetActive(true);
            return view;
        }

        public void Release(TileView view)
        {
            view.ResetForPool();
            view.gameObject.SetActive(false);
            _available.Push(view);
        }

        private TileView CreateInstance()
        {
            TileView view = Instantiate(tilePrefab, transform);
            view.gameObject.SetActive(false);
            return view;
        }
    }
}
