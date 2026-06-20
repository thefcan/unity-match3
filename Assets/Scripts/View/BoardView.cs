using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Match3.Core;
using Match3.Game;
using UnityEngine;

namespace Match3.View
{
    /// <summary>
    /// Renders the board and animates what the core says happened. It keeps one
    /// dictionary — logical tile Id -> pooled TileView — and never inspects rules:
    /// every animation is driven by data the core produced (CascadeStep, positions).
    ///
    /// Also owns the grid&lt;-&gt;world mapping, with the board centred on this
    /// transform so the camera can simply look at the origin.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] private TilePool tilePool;

        [Header("Layout")]
        [SerializeField] private float cellSize = 1f;

        [Header("Animation timings (seconds)")]
        [SerializeField] private float swapDuration = 0.18f;
        [SerializeField] private float popDuration = 0.25f;
        [SerializeField] private float fallDurationPerCell = 0.08f;
        [SerializeField] private float minFallDuration = 0.12f;
        [SerializeField] private float vanishDuration = 0.22f;
        [SerializeField] private float appearDuration = 0.28f;
        [SerializeField] private float reshuffleDuration = 0.35f;

        private readonly Dictionary<int, TileView> _viewsById = new Dictionary<int, TileView>();
        private Board _board;
        private LevelConfig _config;
        private TileView _hintA;
        private TileView _hintB;

        /// <summary>Spawns a view for every tile. Safe to call again on restart — old views return to the pool.</summary>
        public void Initialize(Board board, LevelConfig config)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _config = config != null ? config : throw new ArgumentNullException(nameof(config));

            foreach (TileView view in _viewsById.Values)
                tilePool.Release(view);
            _viewsById.Clear();

            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    var pos = new GridPosition(x, y);
                    if (board[pos] is { } tile)
                        SpawnView(tile, GridToWorld(pos));
                }
            }
        }

        /// <summary>
        /// Shrinks every tile away (the level-transition wipe). Positions are untouched,
        /// so <see cref="AnimateShowTiles"/> brings the exact same arrangement back.
        /// </summary>
        public IEnumerator AnimateHideTiles()
        {
            yield return RunAll(_viewsById.Values.Select(v => v.ShrinkOut(vanishDuration)).ToList());
        }

        /// <summary>Pops every tile back in after the wipe.</summary>
        public IEnumerator AnimateShowTiles()
        {
            yield return RunAll(_viewsById.Values.Select(v => v.GrowIn(appearDuration)).ToList());
        }

        /// <summary>
        /// Glides every tile to its CURRENT board cell — call right after Board.Shuffle
        /// so the views animate from their old spots to the reshuffled layout.
        /// </summary>
        public IEnumerator AnimateReshuffle()
        {
            var moves = new List<IEnumerator>();
            for (int x = 0; x < _board.Width; x++)
            {
                for (int y = 0; y < _board.Height; y++)
                {
                    var pos = new GridPosition(x, y);
                    if (_board[pos] is { } tile && _viewsById.TryGetValue(tile.Id, out TileView view))
                        moves.Add(view.MoveTo(GridToWorld(pos), reshuffleDuration));
                }
            }
            yield return RunAll(moves);
        }

        /// <summary>Pulses the two tiles of a suggested move until <see cref="HideHint"/>.</summary>
        public void ShowHint(GridPosition a, GridPosition b)
        {
            HideHint();
            _hintA = ViewAt(a);
            _hintB = ViewAt(b);
            _hintA?.StartHintPulse();
            _hintB?.StartHintPulse();
        }

        public void HideHint()
        {
            _hintA?.StopHintPulse();
            _hintB?.StopHintPulse();
            _hintA = null;
            _hintB = null;
        }

        private TileView ViewAt(GridPosition pos)
        {
            return _board[pos] is { } tile && _viewsById.TryGetValue(tile.Id, out TileView view) ? view : null;
        }

        // ---- Grid <-> world mapping -------------------------------------------------

        /// <summary>
        /// World position of a cell's centre. Pure math, no bounds check — spawns
        /// deliberately use off-board rows above the top edge as start positions.
        /// </summary>
        public Vector3 GridToWorld(GridPosition pos)
        {
            Vector3 origin = Origin;
            return new Vector3(origin.x + pos.X * cellSize, origin.y + pos.Y * cellSize, 0f);
        }

        /// <summary>The cell under a world point, or null when off-board (input uses this).</summary>
        public GridPosition? WorldToGrid(Vector3 world)
        {
            if (_board == null)
                return null;

            Vector3 origin = Origin;
            var pos = new GridPosition(
                Mathf.RoundToInt((world.x - origin.x) / cellSize),
                Mathf.RoundToInt((world.y - origin.y) / cellSize));

            return _board.IsInside(pos) ? pos : (GridPosition?)null;
        }

        private Vector3 Origin =>
            transform.position - new Vector3(
                (_board.Width - 1) * 0.5f * cellSize,
                (_board.Height - 1) * 0.5f * cellSize,
                0f);

        // ---- Animations (all driven by core data) -----------------------------------

        /// <summary>
        /// Glides the views of the tiles at <paramref name="a"/> and <paramref name="b"/>
        /// to their cells' world positions. Call AFTER mutating the board: this is
        /// "make the visuals catch up with the truth", so the exact same call animates
        /// both the swap and the bounce-back revert.
        /// </summary>
        public IEnumerator AnimateSwap(GridPosition a, GridPosition b)
        {
            var moves = new List<IEnumerator>();
            foreach (GridPosition pos in new[] { a, b })
            {
                if (_board[pos] is { } tile && _viewsById.TryGetValue(tile.Id, out TileView view))
                    moves.Add(view.MoveTo(GridToWorld(pos), swapDuration));
            }
            yield return RunAll(moves);
        }

        /// <summary>
        /// Plays one cascade wave: pop the cleared tiles, then animate falls and
        /// newly spawned tiles dropping in together.
        /// </summary>
        public IEnumerator PlayStep(CascadeStep step)
        {
            yield return RunAll(step.Cleared.Select(PopAndRelease).ToList());

            List<IEnumerator> moves = step.Falls.Select(AnimateFall)
                .Concat(step.Spawns.Select(AnimateSpawn))
                .ToList();
            yield return RunAll(moves);
        }

        private IEnumerator PopAndRelease(ClearedTile cleared)
        {
            if (!_viewsById.TryGetValue(cleared.Tile.Id, out TileView view))
                yield break;

            yield return view.Pop(popDuration);

            _viewsById.Remove(cleared.Tile.Id);
            tilePool.Release(view);
        }

        private IEnumerator AnimateFall(TileFall fall)
        {
            if (!_viewsById.TryGetValue(fall.Tile.Id, out TileView view))
                yield break;

            float duration = FallDuration(fall.From.Y - fall.To.Y);
            yield return view.MoveTo(GridToWorld(fall.To), duration);
        }

        private IEnumerator AnimateSpawn(TileSpawn spawn)
        {
            // New tiles start above the board (stacked per column via SpawnHeightOffset)
            // and fall into place, so refills read as "pouring in from the top".
            var startCell = new GridPosition(spawn.Position.X, _board.Height - 1 + spawn.SpawnHeightOffset);
            TileView view = SpawnView(spawn.Tile, GridToWorld(startCell));

            float duration = FallDuration(startCell.Y - spawn.Position.Y);
            yield return view.MoveTo(GridToWorld(spawn.Position), duration);
        }

        private TileView SpawnView(Tile tile, Vector3 worldPosition)
        {
            TileView view = tilePool.Get();
            view.transform.position = worldPosition;
            view.Bind(tile, _config.tileColors[tile.ColorIndex]);
            _viewsById[tile.Id] = view;
            return view;
        }

        private float FallDuration(int cellsFallen) =>
            Mathf.Max(minFallDuration, cellsFallen * fallDurationPerCell);

        /// <summary>
        /// Runs several animation routines concurrently and finishes when the last
        /// one does — a coroutine-flavoured Promise.all / CompletableFuture.allOf.
        /// </summary>
        private IEnumerator RunAll(List<IEnumerator> routines)
        {
            int running = routines.Count;
            foreach (IEnumerator routine in routines)
                StartCoroutine(RunThenSignal(routine, () => running--));

            while (running > 0)
                yield return null;
        }

        private IEnumerator RunThenSignal(IEnumerator routine, Action onComplete)
        {
            yield return routine;
            onComplete();
        }
    }
}
