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
        [Tooltip("Candy sprite lookup. Left empty, Resources/CandySpriteLibrary is loaded automatically.")]
        [SerializeField] private CandySpriteLibrary spriteLibrary;

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

        [Header("Special candy timings (seconds)")]
        [Tooltip("Extra pop delay per cell of distance from a detonation's origin — blasts read as travelling outward.")]
        [SerializeField] private float detonationStagger = 0.035f;
        [SerializeField] private float maxDetonationDelay = 0.35f;
        [SerializeField] private float convergeDuration = 0.16f;
        [SerializeField] private float morphDuration = 0.3f;

        private readonly Dictionary<int, TileView> _viewsById = new Dictionary<int, TileView>();
        private Board _board;
        private LevelConfig _config;
        private TileView _hintA;
        private TileView _hintB;

        /// <summary>Spawns a view for every tile. Safe to call again on restart — old views return to the pool.</summary>
        public void Initialize(Board board, LevelConfig config, JellyGrid jelly = null)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _config = config != null ? config : throw new ArgumentNullException(nameof(config));

            if (spriteLibrary == null)
                spriteLibrary = Resources.Load<CandySpriteLibrary>("CandySpriteLibrary");

            foreach (TileView view in _viewsById.Values)
                tilePool.Release(view);
            _viewsById.Clear();

            BuildJellyOverlay(jelly);

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

        // ---- Jelly overlay -----------------------------------------------------------
        // Jelly belongs to CELLS: translucent rounded quads UNDER the tiles (sorting
        // order -1). The resolver reports layer removals as JellyHits per wave; the
        // view restyles or pops the overlay from that recording alone.

        private readonly Dictionary<GridPosition, SpriteRenderer> _jellyViews = new Dictionary<GridPosition, SpriteRenderer>();
        private Transform _jellyRoot;

        private static readonly Color JellySingle = new Color(0.98f, 0.55f, 0.75f, 0.42f);
        private static readonly Color JellyDouble = new Color(0.95f, 0.32f, 0.6f, 0.62f);

        private void BuildJellyOverlay(JellyGrid jelly)
        {
            if (_jellyRoot != null)
                Destroy(_jellyRoot.gameObject);
            _jellyViews.Clear();

            if (jelly == null || jelly.IsClear)
                return;

            _jellyRoot = new GameObject("JellyOverlay").transform;
            _jellyRoot.SetParent(transform, false);

            var sprite = Resources.Load<Sprite>("UI/ui_round");
            for (int x = 0; x < jelly.Width; x++)
            {
                for (int y = 0; y < jelly.Height; y++)
                {
                    var pos = new GridPosition(x, y);
                    int layers = jelly.LayersAt(pos);
                    if (layers == 0)
                        continue;

                    var go = new GameObject($"Jelly_{x}_{y}");
                    go.transform.SetParent(_jellyRoot, false);
                    go.transform.position = GridToWorld(pos);
                    var renderer = go.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprite;
                    renderer.sortingOrder = -1;
                    renderer.color = layers >= 2 ? JellyDouble : JellySingle;
                    if (sprite != null)
                    {
                        // scale the sprite's natural world size down to one cell
                        float worldSize = sprite.rect.width / sprite.pixelsPerUnit;
                        go.transform.localScale = Vector3.one * (cellSize * 0.96f / worldSize);
                    }
                    _jellyViews[pos] = renderer;
                }
            }
        }

        private void ApplyJellyHits(CascadeStep step)
        {
            foreach (JellyHit hit in step.JellyHits)
            {
                if (!_jellyViews.TryGetValue(hit.Position, out SpriteRenderer renderer))
                    continue;

                if (hit.RemainingLayers <= 0)
                {
                    EffectsView.TileBurst(renderer.transform.position, JellyDouble, 8);
                    _jellyViews.Remove(hit.Position);
                    Destroy(renderer.gameObject);
                }
                else
                {
                    renderer.color = hit.RemainingLayers >= 2 ? JellyDouble : JellySingle;
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
            AudioManager.Play(Sfx.Shuffle);
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
            AudioManager.Play(Sfx.Swap);
            var moves = new List<IEnumerator>();
            foreach (GridPosition pos in new[] { a, b })
            {
                if (_board[pos] is { } tile && _viewsById.TryGetValue(tile.Id, out TileView view))
                    moves.Add(view.MoveTo(GridToWorld(pos), swapDuration));
            }
            yield return RunAll(moves);
        }

        /// <summary>
        /// Plays one cascade wave: clear the popped tiles (staggered outward from any
        /// detonation origin; match tiles that fund a special converge into its cell),
        /// morph the newly created specials, then animate falls and spawns together.
        /// </summary>
        public IEnumerator PlayStep(CascadeStep step)
        {
            PlayDetonationJuice(step);
            if (step.Cleared.Count > 0)
                AudioManager.Play(Sfx.Pop, 1f + 0.08f * step.CascadeIndex); // combos climb in pitch

            Dictionary<GridPosition, float> delays = BuildDetonationDelays(step);

            // Cells that fund a SURVIVING creation fly into the morph point instead of
            // popping in place. (Creations whose replaced tile is also in Cleared were
            // consumed within the wave — bomb+striped conversions — and just pop.)
            var convergeTargets = new Dictionary<GridPosition, Vector3>();
            foreach (SpecialCreation creation in step.Creations)
            {
                if (IsCleared(step, creation.Replaced.Id)) continue;
                foreach (GridPosition source in creation.SourcePositions)
                    if (source != creation.Position)
                        convergeTargets[source] = GridToWorld(creation.Position);
            }

            var clears = new List<IEnumerator>();
            foreach (ClearedTile cleared in step.Cleared)
            {
                clears.Add(convergeTargets.TryGetValue(cleared.Position, out Vector3 target)
                    ? ConvergeAndRelease(cleared, target)
                    : PopAndRelease(cleared, delays.TryGetValue(cleared.Position, out float delay) ? delay : 0f));
            }
            yield return RunAll(clears);

            ApplyJellyHits(step);

            if (step.Points > 0 && step.Cleared.Count > 0)
                ScorePopup.Spawn(Centroid(step.Cleared), step.Points, Color.white);

            // Morphs rebind the replaced tile's view to the created special — this must
            // land before falls, which reference the created tile's Id.
            var morphs = new List<IEnumerator>();
            foreach (SpecialCreation creation in step.Creations)
            {
                if (IsCleared(step, creation.Replaced.Id)) continue;
                if (_viewsById.TryGetValue(creation.Replaced.Id, out TileView view))
                {
                    _viewsById.Remove(creation.Replaced.Id);
                    _viewsById[creation.Created.Id] = view;
                    (Sprite sprite, Color color) = VisualFor(creation.Created);
                    morphs.Add(view.MorphTo(creation.Created, sprite, color, morphDuration));
                }
            }
            if (morphs.Count > 0)
            {
                AudioManager.Play(Sfx.SpecialCreate);
                yield return RunAll(morphs);
            }

            List<IEnumerator> moves = step.Falls.Select(AnimateFall)
                .Concat(step.Spawns.Select(AnimateSpawn))
                .ToList();
            yield return RunAll(moves);
        }

        private static bool IsCleared(CascadeStep step, int tileId) =>
            step.Cleared.Any(cleared => cleared.Tile.Id == tileId);

        /// <summary>
        /// Per-cell pop delays so a lane or blast reads as travelling outward from its
        /// origin. Overlapping detonations keep the SMALLEST delay (first hit wins).
        /// </summary>
        private Dictionary<GridPosition, float> BuildDetonationDelays(CascadeStep step)
        {
            var delays = new Dictionary<GridPosition, float>();
            foreach (Detonation detonation in step.Detonations)
            {
                foreach (GridPosition cell in detonation.Area)
                {
                    int distance = Mathf.Abs(cell.X - detonation.Origin.X) + Mathf.Abs(cell.Y - detonation.Origin.Y);
                    float delay = Mathf.Min(distance * detonationStagger, maxDetonationDelay);
                    if (!delays.TryGetValue(cell, out float existing) || delay < existing)
                        delays[cell] = delay;
                }
            }
            return delays;
        }

        private IEnumerator PopAndRelease(ClearedTile cleared, float delay)
        {
            if (!_viewsById.TryGetValue(cleared.Tile.Id, out TileView view))
                yield break;

            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            EffectsView.TileBurst(view.transform.position, BurstColorFor(cleared.Tile));
            yield return view.Pop(popDuration);

            _viewsById.Remove(cleared.Tile.Id);
            tilePool.Release(view);
        }

        private Color BurstColorFor(Tile tile) =>
            tile.ColorIndex >= 0 && tile.ColorIndex < _config.tileColors.Length
                ? _config.tileColors[tile.ColorIndex]
                : Color.white;

        /// <summary>Sounds + blast bursts + camera shakes for every special that went off this wave.</summary>
        private void PlayDetonationJuice(CascadeStep step)
        {
            const int maxSounds = 4; // a bomb+striped combo fires many lanes — don't stack 10 clips
            int sounds = 0;

            foreach (Detonation detonation in step.Detonations)
            {
                Vector3 origin = GridToWorld(detonation.Origin);
                bool playSound = sounds++ < maxSounds;

                switch (detonation.Kind)
                {
                    case DetonationKind.Row:
                    case DetonationKind.Column:
                    case DetonationKind.Cross:
                    case DetonationKind.TripleCross:
                        if (playSound) AudioManager.Play(Sfx.LineClear);
                        EffectsView.BlastBurst(origin, Color.white);
                        break;

                    case DetonationKind.Blast3x3:
                    case DetonationKind.Blast5x5:
                        if (playSound) AudioManager.Play(Sfx.WrappedBlast);
                        EffectsView.BlastBurst(origin, new Color(1f, 0.7f, 0.3f));
                        EffectsView.Shake(detonation.Kind == DetonationKind.Blast5x5 ? 0.2f : 0.12f);
                        break;

                    case DetonationKind.ColorClear:
                    case DetonationKind.BoardClear:
                        if (playSound) AudioManager.Play(Sfx.ColorBomb);
                        EffectsView.BlastBurst(origin, new Color(0.8f, 0.5f, 1f));
                        EffectsView.Shake(0.22f, 0.3f);
                        break;
                }
            }
        }

        private Vector3 Centroid(IReadOnlyList<ClearedTile> cleared)
        {
            Vector3 sum = Vector3.zero;
            foreach (ClearedTile tile in cleared)
                sum += GridToWorld(tile.Position);
            return sum / cleared.Count;
        }

        private IEnumerator ConvergeAndRelease(ClearedTile cleared, Vector3 target)
        {
            if (!_viewsById.TryGetValue(cleared.Tile.Id, out TileView view))
                yield break;

            yield return view.MoveTo(target, convergeDuration);

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
            (Sprite sprite, Color color) = VisualFor(tile);
            view.Bind(tile, sprite, color);
            _viewsById[tile.Id] = view;
            return view;
        }

        /// <summary>
        /// Candy sprite (drawn untinted) when the library has one; otherwise the
        /// prefab's default sprite with a kind-aware tint as a fallback.
        /// </summary>
        private (Sprite sprite, Color color) VisualFor(Tile tile)
        {
            Sprite sprite = spriteLibrary != null ? spriteLibrary.For(tile.ColorIndex, tile.Kind) : null;
            return sprite != null ? (sprite, Color.white) : (null, TintFor(tile));
        }

        /// <summary>
        /// Fallback tinting when no candy sprite exists: the colour bomb has NO palette
        /// colour (ColorIndex is -1 — indexing would throw), and striped/wrapped get a
        /// shifted tone so they read as different at a glance.
        /// </summary>
        private Color TintFor(Tile tile)
        {
            if (tile.IsColorBomb)
                return new Color(0.25f, 0.2f, 0.3f);

            Color baseColor = _config.tileColors[tile.ColorIndex];
            switch (tile.Kind)
            {
                case TileKind.StripedH:
                case TileKind.StripedV:
                    return Color.Lerp(baseColor, Color.white, 0.45f);
                case TileKind.Wrapped:
                    return Color.Lerp(baseColor, Color.black, 0.35f);
                default:
                    return baseColor;
            }
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
