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
        private FrostingGrid _frosting; // layer ledger — drives per-layer sprite swaps
        private BombTimers _bombs;      // countdown source for the badge overlays
        private TileView _hintA;
        private TileView _hintB;

        /// <summary>Spawns a view for every tile. Safe to call again on restart — old views return to the pool.</summary>
        public void Initialize(Board board, LevelConfig config, JellyGrid jelly = null, LockGrid locks = null,
                               FrostingGrid frosting = null, BombTimers bombs = null)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _config = config != null ? config : throw new ArgumentNullException(nameof(config));
            _frosting = frosting;
            _bombs = bombs;

            if (spriteLibrary == null)
                spriteLibrary = Resources.Load<CandySpriteLibrary>("CandySpriteLibrary");

            foreach (TileView view in _viewsById.Values)
                tilePool.Release(view);
            _viewsById.Clear();
            ClearBombBadges();

            BuildBackdrop(board);
            BuildJellyOverlay(jelly);
            BuildLockOverlay(locks);

            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    var pos = new GridPosition(x, y);
                    if (board[pos] is { } tile)
                        SpawnView(tile, GridToWorld(pos));
                }
            }

            RebindFrostingSprites();
        }

        /// <summary>
        /// Frosting renders per remaining LAYER (thicker stack = taller-looking slab).
        /// The generic sprite path only knows the kind, so frosting tiles get their
        /// layer-accurate sprite in a second pass — and again after every hit.
        /// </summary>
        private void RebindFrostingSprites()
        {
            if (_frosting == null || spriteLibrary == null)
                return;

            for (int x = 0; x < _board.Width; x++)
            {
                for (int y = 0; y < _board.Height; y++)
                {
                    var pos = new GridPosition(x, y);
                    if (_board[pos] is { } tile && tile.Kind == TileKind.Frosting &&
                        _viewsById.TryGetValue(tile.Id, out TileView view))
                    {
                        Sprite sprite = spriteLibrary.FrostingSprite(_frosting.LayersAt(pos));
                        if (sprite != null)
                            view.Bind(tile, sprite, Color.white);
                    }
                }
            }
        }

        // ---- Board backdrop ------------------------------------------------------------
        // The Stitch design sinks the board into a rounded container. One sliced
        // sprite behind everything (order -20) — zero per-frame cost.

        private SpriteRenderer _backdrop;

        private void BuildBackdrop(Board board)
        {
            if (_backdrop == null)
            {
                var go = new GameObject("BoardBackdrop");
                go.transform.SetParent(transform, false);
                _backdrop = go.AddComponent<SpriteRenderer>();
                _backdrop.sprite = Resources.Load<Sprite>("UI/ui_round"); // FullRect + 48px border → sliced-safe
                _backdrop.drawMode = SpriteDrawMode.Sliced;
                _backdrop.sortingOrder = -20; // under tiles (0), jelly (-1) and cages (+1)
            }

            if (_backdrop.sprite == null)
                return;

            Color card = Match3.UI.UiTheme.ThemeCard; // re-tinted per level on restart
            _backdrop.color = new Color(card.r, card.g, card.b, 0.55f);
            _backdrop.size = new Vector2(board.Width + 0.7f, board.Height + 0.7f) * cellSize;
            _backdrop.transform.localPosition = Vector3.zero;
        }

        // ---- Jelly overlay -----------------------------------------------------------
        // Jelly belongs to CELLS: translucent rounded quads UNDER the tiles (sorting
        // order -1). The resolver reports layer removals as JellyHits per wave; the
        // view restyles or pops the overlay from that recording alone.

        private readonly Dictionary<GridPosition, SpriteRenderer> _jellyViews = new Dictionary<GridPosition, SpriteRenderer>();
        private readonly Stack<SpriteRenderer> _jellyPool = new Stack<SpriteRenderer>();
        private Transform _jellyRoot;
        private static Sprite _jellySprite; // Resources asset — safe to cache across boards

        private static readonly Color JellySingle = new Color(0.98f, 0.55f, 0.75f, 0.42f);
        private static readonly Color JellyDouble = new Color(0.95f, 0.32f, 0.6f, 0.62f);

        private void BuildJellyOverlay(JellyGrid jelly)
        {
            foreach (SpriteRenderer view in _jellyViews.Values)
                ReleaseJellyView(view);
            _jellyViews.Clear();

            if (jelly == null || jelly.IsClear)
                return;

            if (_jellyRoot == null)
            {
                _jellyRoot = new GameObject("JellyOverlay").transform;
                _jellyRoot.SetParent(transform, false);
            }
            if (_jellySprite == null)
                _jellySprite = Resources.Load<Sprite>("UI/ui_round");

            for (int x = 0; x < jelly.Width; x++)
            {
                for (int y = 0; y < jelly.Height; y++)
                {
                    var pos = new GridPosition(x, y);
                    int layers = jelly.LayersAt(pos);
                    if (layers == 0)
                        continue;

                    SpriteRenderer renderer = GetJellyView();
                    renderer.transform.position = GridToWorld(pos);
                    renderer.color = layers >= 2 ? JellyDouble : JellySingle;
                    _jellyViews[pos] = renderer;
                }
            }
        }

        private SpriteRenderer GetJellyView()
        {
            while (_jellyPool.Count > 0)
            {
                SpriteRenderer pooled = _jellyPool.Pop();
                if (pooled != null)
                {
                    pooled.gameObject.SetActive(true);
                    return pooled;
                }
            }

            var go = new GameObject("Jelly");
            go.transform.SetParent(_jellyRoot, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _jellySprite;
            renderer.sortingOrder = -1;
            if (_jellySprite != null)
            {
                // scale the sprite's natural world size down to one cell
                float worldSize = _jellySprite.rect.width / _jellySprite.pixelsPerUnit;
                go.transform.localScale = Vector3.one * (cellSize * 0.96f / worldSize);
            }
            return renderer;
        }

        private void ReleaseJellyView(SpriteRenderer view)
        {
            if (view == null)
                return;
            view.gameObject.SetActive(false);
            _jellyPool.Push(view);
        }

        // ---- Lock overlay ------------------------------------------------------------
        // Licorice cages render ON TOP of their candy (sorting +1). The resolver
        // reports breaks as LockBreaks; the overlay pops off from the recording alone.

        private readonly Dictionary<GridPosition, SpriteRenderer> _lockViews = new Dictionary<GridPosition, SpriteRenderer>();
        private readonly Stack<SpriteRenderer> _lockPool = new Stack<SpriteRenderer>();
        private Transform _lockRoot;

        private static readonly Color LockFallbackTint = new Color(0.55f, 0.5f, 0.65f, 0.85f);

        private void BuildLockOverlay(LockGrid locks)
        {
            foreach (SpriteRenderer view in _lockViews.Values)
                ReleaseLockView(view);
            _lockViews.Clear();

            if (locks == null || locks.IsClear)
                return;

            if (_lockRoot == null)
            {
                _lockRoot = new GameObject("LockOverlay").transform;
                _lockRoot.SetParent(transform, false);
            }

            for (int x = 0; x < locks.Width; x++)
            {
                for (int y = 0; y < locks.Height; y++)
                {
                    var pos = new GridPosition(x, y);
                    if (!locks.HasLock(pos))
                        continue;

                    SpriteRenderer renderer = GetLockView();
                    renderer.transform.position = GridToWorld(pos);
                    _lockViews[pos] = renderer;
                }
            }
        }

        private SpriteRenderer GetLockView()
        {
            while (_lockPool.Count > 0)
            {
                SpriteRenderer pooled = _lockPool.Pop();
                if (pooled != null)
                {
                    pooled.gameObject.SetActive(true);
                    return pooled;
                }
            }

            // The generated cage sprite when available; a tinted outline until the
            // sprite menu has been re-run (Match3 > Generate > Candy Sprites).
            Sprite sprite = spriteLibrary != null ? spriteLibrary.LockCage : null;
            bool fallback = sprite == null;
            if (fallback)
                sprite = Resources.Load<Sprite>("UI/ui_round_outline");

            var go = new GameObject("Lock");
            go.transform.SetParent(_lockRoot, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 1; // cage sits over the candy
            renderer.color = fallback ? LockFallbackTint : Color.white;
            if (sprite != null)
            {
                float worldSize = sprite.rect.width / sprite.pixelsPerUnit;
                go.transform.localScale = Vector3.one * (cellSize * 0.98f / worldSize);
            }
            return renderer;
        }

        private void ReleaseLockView(SpriteRenderer view)
        {
            if (view == null)
                return;
            view.gameObject.SetActive(false);
            _lockPool.Push(view);
        }

        private void ApplyLockBreaks(CascadeStep step)
        {
            foreach (LockBreak lockBreak in step.LockBreaks)
            {
                if (!_lockViews.TryGetValue(lockBreak.Position, out SpriteRenderer view))
                    continue;
                EffectsView.TileBurst(view.transform.position, LockFallbackTint, 10);
                _lockViews.Remove(lockBreak.Position);
                ReleaseLockView(view);
            }
            if (step.LockBreaks.Count > 0)
                AudioManager.Play(Sfx.Pop, 0.7f);
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
                    ReleaseJellyView(renderer);
                }
                else
                {
                    renderer.color = hit.RemainingLayers >= 2 ? JellyDouble : JellySingle;
                }
            }
        }

        private static readonly Color FrostingBurst = new Color(0.85f, 0.92f, 1f);

        /// <summary>
        /// Surviving frosting swaps to its thinner sprite with an ice burst; a cell
        /// that lost its last layer is in Cleared and pops through the normal path.
        /// </summary>
        private void ApplyFrostingHits(CascadeStep step)
        {
            if (step.FrostingHits.Count == 0)
                return;

            foreach (FrostingHit hit in step.FrostingHits)
            {
                EffectsView.TileBurst(GridToWorld(hit.Position), FrostingBurst, 10);
                if (hit.RemainingLayers <= 0)
                    continue;

                if (_board[hit.Position] is { } tile && tile.Kind == TileKind.Frosting &&
                    _viewsById.TryGetValue(tile.Id, out TileView view) && spriteLibrary != null)
                {
                    Sprite sprite = spriteLibrary.FrostingSprite(hit.RemainingLayers);
                    if (sprite != null)
                        view.Bind(tile, sprite, Color.white);
                }
            }
            AudioManager.Play(Sfx.Pop, 0.6f);
        }

        // ---- Bomb countdown badges ---------------------------------------------------
        // A little dark plate with the remaining-move number rides ON the bomb candy.
        // Badges live under their own root and chase the tile views in LateUpdate, so
        // pooling never leaks a badge onto a reused candy view.

        private readonly Dictionary<int, TMPro.TextMeshPro> _bombBadges = new Dictionary<int, TMPro.TextMeshPro>();
        private Transform _bombBadgeRoot;

        /// <summary>
        /// Badges chase their candies every frame. The steady-state pass walks the
        /// BADGES (at most two on a board) instead of all 64 tile views — the full
        /// scan only runs on the frame the armed set actually changes, which is
        /// once or twice a level.
        /// </summary>
        private void LateUpdate()
        {
            if (_bombs == null)
                return;

            List<int> stale = null;
            foreach (KeyValuePair<int, TMPro.TextMeshPro> entry in _bombBadges)
            {
                if (entry.Value == null || !_bombs.TryGet(entry.Key, out _) ||
                    !_viewsById.TryGetValue(entry.Key, out TileView view))
                {
                    (stale ??= new List<int>()).Add(entry.Key); // defused, popped or exited
                    continue;
                }
                entry.Value.transform.position = view.transform.position + new Vector3(0f, -0.03f, 0f);
            }

            if (stale != null)
            {
                foreach (int id in stale)
                {
                    if (_bombBadges[id] != null)
                        Destroy(_bombBadges[id].gameObject);
                    _bombBadges.Remove(id);
                }
            }

            // A count mismatch is the ONLY way a bomb can be missing a badge, so it
            // gates the expensive pass (a same-frame defuse+arm leaves the counts
            // equal for one frame, then the removal above forces the scan).
            if (_bombs.Count == _bombBadges.Count)
                return;

            foreach (KeyValuePair<int, TileView> entry in _viewsById)
            {
                if (!_bombs.TryGet(entry.Key, out int remaining) || _bombBadges.ContainsKey(entry.Key))
                    continue;

                TMPro.TextMeshPro badge = CreateBombBadge();
                badge.text = remaining.ToString();
                _bombBadges[entry.Key] = badge;
                badge.transform.position = entry.Value.transform.position + new Vector3(0f, -0.03f, 0f);
            }
        }

        private TMPro.TextMeshPro CreateBombBadge()
        {
            if (_bombBadgeRoot == null)
            {
                _bombBadgeRoot = new GameObject("BombBadges").transform;
                _bombBadgeRoot.SetParent(transform, false);
            }

            var go = new GameObject("BombBadge");
            go.transform.SetParent(_bombBadgeRoot, false);
            var text = go.AddComponent<TMPro.TextMeshPro>();
            Match3.UI.UiTheme.ApplyFont(text, Match3.UI.UiTheme.ButtonFont);
            text.fontSize = 4.2f;
            text.fontStyle = TMPro.FontStyles.Bold;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.color = Color.white;
            text.sortingOrder = 6; // over the candy and the cage overlays
            var rect = text.rectTransform;
            rect.sizeDelta = new Vector2(1f, 1f);
            return text;
        }

        /// <summary>
        /// Re-reads every badge from the timers. Badge text normally only changes
        /// inside ApplyBombTicks, so a rescue's re-armed fuse would keep showing a
        /// stale red "0" until the next committed move without this.
        /// </summary>
        public void RefreshBombBadges()
        {
            if (_bombs == null)
                return;
            foreach (KeyValuePair<int, TMPro.TextMeshPro> entry in _bombBadges)
            {
                if (entry.Value == null || !_bombs.TryGet(entry.Key, out int remaining))
                    continue;
                entry.Value.text = remaining.ToString();
                entry.Value.color = remaining <= 2 ? new Color(1f, 0.35f, 0.3f) : Color.white;
            }
        }

        private void ClearBombBadges()
        {
            foreach (TMPro.TextMeshPro badge in _bombBadges.Values)
                if (badge != null)
                    Destroy(badge.gameObject);
            _bombBadges.Clear();
        }

        /// <summary>Countdown numbers refresh; a zero flashes red and rattles the board.</summary>
        private void ApplyBombTicks(CascadeStep step)
        {
            bool exploded = false;
            foreach (BombTick tick in step.BombTicks)
            {
                if (_bombBadges.TryGetValue(tick.Tile.Id, out TMPro.TextMeshPro badge) && badge != null)
                {
                    badge.text = Mathf.Max(0, tick.Remaining).ToString();
                    if (tick.Remaining <= 2)
                        badge.color = new Color(1f, 0.35f, 0.3f);
                }
                if (tick.Remaining == 0)
                {
                    exploded = true;
                    EffectsView.BlastBurst(GridToWorld(tick.Position), new Color(1f, 0.4f, 0.2f));
                }
            }

            if (step.BombTicks.Count > 0)
                AudioManager.Play(Sfx.Button, 0.5f); // a dry tick under the countdown
            if (exploded)
            {
                AudioManager.Play(Sfx.ColorBomb);
                EffectsView.Shake(0.3f, 0.35f);
                Haptics.Heavy();
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
        /// Level-start reveal: every candy grows in on a diagonal stagger ((x+y)
        /// order — a curtain sweeping from the bottom-left corner), ≤0.58s on an
        /// 8×8. Runs during the Init phase, so input is naturally still blocked.
        /// </summary>
        public IEnumerator AnimateBoardIntro()
        {
            if (Match3.Game.Prefs.ReducedMotionOn)
                yield break;

            var reveals = new List<IEnumerator>();
            for (int x = 0; x < _board.Width; x++)
            {
                for (int y = 0; y < _board.Height; y++)
                {
                    var pos = new GridPosition(x, y);
                    if (_board[pos] is { } tile && _viewsById.TryGetValue(tile.Id, out TileView view))
                        reveals.Add(view.GrowInAfter((x + y) * 0.02f, appearDuration));
                }
            }
            yield return RunAll(reveals);
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

        /// <summary>
        /// Re-applies every tile's sprite in place (colorblind toggle) without touching
        /// board state or positions. Bind also resets scale, which is fine — the
        /// settings panel is only reachable while the board is idle.
        /// </summary>
        public void RefreshTileVisuals()
        {
            if (_board == null)
                return;

            for (int x = 0; x < _board.Width; x++)
            {
                for (int y = 0; y < _board.Height; y++)
                {
                    var pos = new GridPosition(x, y);
                    if (_board[pos] is { } tile && _viewsById.TryGetValue(tile.Id, out TileView view))
                    {
                        (Sprite sprite, Color color) = VisualFor(tile);
                        view.Bind(tile, sprite, color);
                        view.SetSpecialShimmer(IsShimmerKind(tile.Kind)); // Bind cleared it
                    }
                }
            }
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
        /// The bounce-back variant of <see cref="AnimateSwap"/>: same glide, but it
        /// SOUNDS and FEELS like a refusal — low-pitched thunk, light haptic, and a
        /// detached head-shake on both candies (input reopens during the shake).
        /// </summary>
        public IEnumerator AnimateSwapRevert(GridPosition a, GridPosition b)
        {
            AudioManager.Play(Sfx.Swap, 0.75f);
            Match3.Game.Haptics.Light();
            var moves = new List<IEnumerator>();
            var shaken = new List<TileView>(2);
            foreach (GridPosition pos in new[] { a, b })
            {
                if (_board[pos] is { } tile && _viewsById.TryGetValue(tile.Id, out TileView view))
                {
                    moves.Add(view.MoveTo(GridToWorld(pos), swapDuration));
                    shaken.Add(view);
                }
            }
            yield return RunAll(moves);
            foreach (TileView view in shaken)
                view.StartWiggle();
        }

        // ---- Press feedback (view-only; swap detection never waits on this) ----------

        private TileView _pressedTileView;

        public void ShowPressFeedback(GridPosition cell)
        {
            ClearPressFeedback();
            if (_board != null && _board.IsInside(cell) &&
                _board[cell] is { } tile && _viewsById.TryGetValue(tile.Id, out TileView view))
            {
                _pressedTileView = view;
                view.PressIn();
            }
        }

        public void ClearPressFeedback()
        {
            if (_pressedTileView != null)
            {
                _pressedTileView.PressOut();
                _pressedTileView = null;
            }
        }

        /// <summary>
        /// World centroid of the last wave's cleared cells — the launch pad for the
        /// objective bar's fly-to-chip sparks. View bookkeeping only, no rules.
        /// </summary>
        public Vector3 LastClearCentroid { get; private set; }

        /// <summary>
        /// Plays one cascade wave: clear the popped tiles (staggered outward from any
        /// detonation origin; match tiles that fund a special converge into its cell),
        /// morph the newly created specials, then animate falls and spawns together.
        /// </summary>
        public IEnumerator PlayStep(CascadeStep step)
        {
            ApplyBombTicks(step); // countdown-only steps carry nothing else
            ApplyLockBreaks(step); // cages shatter first — their candies stay
            PlayDetonationJuice(step);
            if (step.Cleared.Count > 0)
            {
                LastClearCentroid = Centroid(step.Cleared);
                AudioManager.Play(Sfx.Pop, 1f + 0.08f * step.CascadeIndex); // combos climb in pitch
                string banner = BannerPopup.TextFor(step.CascadeIndex);
                if (banner != null)
                    BannerPopup.Spawn(BannerAnchor(LastClearCentroid), banner); // fire-and-forget
            }

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

            // A detonating fish DARTS to its (first) target instead of popping in
            // place — the strike recording carries the flight path.
            var fishFlights = new Dictionary<int, Vector3>();
            foreach (FishStrike strike in step.FishStrikes)
                if (!fishFlights.ContainsKey(strike.Fish.Id))
                    fishFlights[strike.Fish.Id] = GridToWorld(strike.To);

            var clears = new List<IEnumerator>();
            foreach (ClearedTile cleared in step.Cleared)
            {
                if (fishFlights.TryGetValue(cleared.Tile.Id, out Vector3 dartTarget))
                    clears.Add(FlyAndRelease(cleared, dartTarget));
                else if (convergeTargets.TryGetValue(cleared.Position, out Vector3 target))
                    clears.Add(ConvergeAndRelease(cleared, target));
                else
                    clears.Add(PopAndRelease(cleared, delays.TryGetValue(cleared.Position, out float delay) ? delay : 0f));
            }
            yield return RunAll(clears);

            ApplyJellyHits(step);
            ApplyFrostingHits(step);

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
                // The freshly minted specials start breathing once the morph lands.
                foreach (SpecialCreation creation in step.Creations)
                    if (_viewsById.TryGetValue(creation.Created.Id, out TileView minted))
                        minted.SetSpecialShimmer(IsShimmerKind(creation.Created.Kind));
            }

            // Mystery eggs crack open: the shell's view rebinds to its hatchling with
            // the same squash-morph the specials use — BEFORE the falls play, because
            // this wave's falls already carry the hatchling's id.
            if (step.EggHatches.Count > 0)
            {
                AudioManager.Play(Sfx.SpecialCreate, 1.15f);
                var hatches = new List<IEnumerator>();
                foreach (EggHatch hatch in step.EggHatches)
                {
                    // A puff of shell fragments as the crack starts (reduced motion
                    // shrinks the burst inside TileBurst itself).
                    EffectsView.TileBurst(GridToWorld(hatch.Position), new Color(0.96f, 0.9f, 0.75f), 8);
                    if (_viewsById.TryGetValue(hatch.Replaced.Id, out TileView view))
                    {
                        _viewsById.Remove(hatch.Replaced.Id);
                        _viewsById[hatch.Hatched.Id] = view;
                        (Sprite sprite, Color color) = VisualFor(hatch.Hatched);
                        hatches.Add(view.MorphTo(hatch.Hatched, sprite, color, morphDuration));
                    }
                }
                yield return RunAll(hatches);
                foreach (EggHatch hatch in step.EggHatches)
                    if (_viewsById.TryGetValue(hatch.Hatched.Id, out TileView chick))
                        chick.SetSpecialShimmer(IsShimmerKind(hatch.Hatched.Kind));
            }

            // Ingredients that reached the floor slide out below the board.
            if (step.IngredientExits.Count > 0)
            {
                AudioManager.Play(Sfx.SpecialCreate, 0.8f);
                var exits = new List<IEnumerator>();
                foreach (IngredientExit exit in step.IngredientExits)
                    if (_viewsById.TryGetValue(exit.Tile.Id, out TileView view))
                        exits.Add(ExitAndRelease(exit, view));
                yield return RunAll(exits);
            }

            List<IEnumerator> moves = step.Falls.Select(AnimateFall)
                .Concat(step.Spawns.Select(AnimateSpawn))
                .ToList();
            yield return RunAll(moves);

            // The end-of-move chocolate creep: the eaten candy squashes into a block.
            foreach (ChocolateSpread spread in step.ChocolateSpreads)
            {
                if (_viewsById.TryGetValue(spread.Consumed.Id, out TileView victim))
                {
                    AudioManager.Play(Sfx.WrappedBlast, 0.6f);
                    _viewsById.Remove(spread.Consumed.Id);
                    (Sprite sprite, Color color) = VisualFor(spread.Spawned);
                    yield return victim.MorphTo(spread.Spawned, sprite, color, morphDuration);
                    _viewsById[spread.Spawned.Id] = victim;
                }
            }
        }

        private IEnumerator ExitAndRelease(IngredientExit exit, TileView view)
        {
            Vector3 target = GridToWorld(new GridPosition(exit.Position.X, -1));
            yield return view.MoveTo(target, 0.25f);
            _viewsById.Remove(exit.Tile.Id);
            tilePool.Release(view);
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
                yield return Wait(delay);

            EffectsView.TileBurst(view.transform.position, BurstColorFor(cleared.Tile));
            yield return view.Pop(popDuration);

            _viewsById.Remove(cleared.Tile.Id);
            tilePool.Release(view);
        }

        private Color BurstColorFor(Tile tile) =>
            tile.ColorIndex >= 0 && tile.ColorIndex < _config.tileColors.Length
                ? _config.tileColors[tile.ColorIndex]
                : Color.white;

        /// <summary>Sounds + blast bursts + camera shakes + haptics for every special that went off this wave.</summary>
        private void PlayDetonationJuice(CascadeStep step)
        {
            const int maxSounds = 4; // a bomb+striped combo fires many lanes — don't stack 10 clips
            int sounds = 0;
            bool haptic = false; // one pulse per wave, scaled to its biggest detonation

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
                        FireLaneBeams(detonation);
                        if (!haptic) { haptic = true; Haptics.Light(); }
                        break;

                    case DetonationKind.Blast3x3:
                    case DetonationKind.Blast5x5:
                        if (playSound) AudioManager.Play(Sfx.WrappedBlast);
                        EffectsView.BlastBurst(origin, new Color(1f, 0.7f, 0.3f));
                        EffectsView.BlastRing(origin,
                            (detonation.Kind == DetonationKind.Blast5x5 ? 2.5f : 1.5f) * CellSize(),
                            new Color(1f, 0.8f, 0.45f));
                        EffectsView.Shake(detonation.Kind == DetonationKind.Blast5x5 ? 0.2f : 0.12f);
                        if (!haptic) { haptic = true; Haptics.Medium(); }
                        break;

                    case DetonationKind.ColorClear:
                    case DetonationKind.BoardClear:
                        if (playSound) AudioManager.Play(Sfx.ColorBomb);
                        EffectsView.BlastBurst(origin, new Color(0.8f, 0.5f, 1f));
                        FireStreaks(detonation, origin);
                        EffectsView.Shake(0.22f, 0.3f);
                        if (!haptic) { haptic = true; Haptics.Heavy(); }
                        break;

                    case DetonationKind.FishStrike:
                        if (playSound) AudioManager.Play(Sfx.Swap, 1.4f); // a light "whoosh"
                        EffectsView.TileBurst(origin, new Color(0.55f, 0.85f, 1f), 8);
                        break;
                }
            }
        }

        /// <summary>
        /// Up to four beams sweeping outward along the detonation's recorded lanes.
        /// Extents come from Detonation.Area (never re-derived) and the tip speed is
        /// cellSize/detonationStagger, so the front reaches each cell exactly when
        /// its staggered pop fires.
        /// </summary>
        private void FireLaneBeams(Detonation detonation)
        {
            float cell = CellSize();
            float speed = cell / Mathf.Max(0.001f, detonationStagger);
            Vector3 origin = GridToWorld(detonation.Origin);

            int left = 0, right = 0, down = 0, up = 0;
            foreach (GridPosition pos in detonation.Area)
            {
                if (pos.Y == detonation.Origin.Y)
                {
                    left = Mathf.Max(left, detonation.Origin.X - pos.X);
                    right = Mathf.Max(right, pos.X - detonation.Origin.X);
                }
                if (pos.X == detonation.Origin.X)
                {
                    down = Mathf.Max(down, detonation.Origin.Y - pos.Y);
                    up = Mathf.Max(up, pos.Y - detonation.Origin.Y);
                }
            }

            float width = 0.35f * cell;
            if (right > 0) EffectsView.LaneBeam(origin, Vector3.right, right * cell, speed, width, Color.white);
            if (left > 0) EffectsView.LaneBeam(origin, Vector3.left, left * cell, speed, width, Color.white);
            if (up > 0) EffectsView.LaneBeam(origin, Vector3.up, up * cell, speed, width, Color.white);
            if (down > 0) EffectsView.LaneBeam(origin, Vector3.down, down * cell, speed, width, Color.white);
        }

        /// <summary>Colour-bomb tendrils to an even sample (max 12) of its victims.</summary>
        private void FireStreaks(Detonation detonation, Vector3 origin)
        {
            const int maxStreaks = 12;
            int count = detonation.Area.Count;
            if (count == 0)
                return;

            var targets = new List<Vector3>(Mathf.Min(count, maxStreaks));
            int stride = Mathf.Max(1, count / maxStreaks);
            for (int i = 0; i < count && targets.Count < maxStreaks; i += stride)
                targets.Add(GridToWorld(detonation.Area[i]));
            EffectsView.Streaks(origin, targets, new Color(0.85f, 0.6f, 1f));
        }

        private Vector3 Centroid(IReadOnlyList<ClearedTile> cleared)
        {
            Vector3 sum = Vector3.zero;
            foreach (ClearedTile tile in cleared)
                sum += GridToWorld(tile.Position);
            return sum / cleared.Count;
        }

        /// <summary>Clamps a banner position into the board's middle band so the
        /// word never hides under the HUD or the booster tray.</summary>
        private Vector3 BannerAnchor(Vector3 centroid)
        {
            float minY = GridToWorld(new GridPosition(0, 1)).y;
            float maxY = GridToWorld(new GridPosition(0, _board.Height - 2)).y;
            centroid.y = Mathf.Clamp(centroid.y, minY, maxY);
            return centroid;
        }

        /// <summary>World size of one cell — beams and rings are sized off it.</summary>
        private float CellSize() =>
            Mathf.Abs(GridToWorld(new GridPosition(1, 0)).x - GridToWorld(new GridPosition(0, 0)).x);

        private IEnumerator ConvergeAndRelease(ClearedTile cleared, Vector3 target)
        {
            if (!_viewsById.TryGetValue(cleared.Tile.Id, out TileView view))
                yield break;

            yield return view.MoveTo(target, convergeDuration);

            _viewsById.Remove(cleared.Tile.Id);
            tilePool.Release(view);
        }

        /// <summary>The fish darts to its target cell, splashes and is gone.</summary>
        private IEnumerator FlyAndRelease(ClearedTile cleared, Vector3 target)
        {
            if (!_viewsById.TryGetValue(cleared.Tile.Id, out TileView view))
                yield break;

            yield return view.MoveTo(target, 0.22f);
            EffectsView.TileBurst(target, new Color(0.55f, 0.85f, 1f), 14);

            _viewsById.Remove(cleared.Tile.Id);
            tilePool.Release(view);
        }

        private IEnumerator AnimateFall(TileFall fall)
        {
            if (!_viewsById.TryGetValue(fall.Tile.Id, out TileView view))
                yield break;

            float duration = FallDuration(fall.From.Y - fall.To.Y);
            yield return view.FallTo(GridToWorld(fall.To), duration);
        }

        private IEnumerator AnimateSpawn(TileSpawn spawn)
        {
            // New tiles start above the board (stacked per column via SpawnHeightOffset)
            // and fall into place, so refills read as "pouring in from the top".
            var startCell = new GridPosition(spawn.Position.X, _board.Height - 1 + spawn.SpawnHeightOffset);
            TileView view = SpawnView(spawn.Tile, GridToWorld(startCell));

            float duration = FallDuration(startCell.Y - spawn.Position.Y);
            yield return view.FallTo(GridToWorld(spawn.Position), duration);
        }

        private TileView SpawnView(Tile tile, Vector3 worldPosition)
        {
            TileView view = tilePool.Get();
            view.transform.position = worldPosition;
            (Sprite sprite, Color color) = VisualFor(tile);
            view.Bind(tile, sprite, color);
            view.SetSpecialShimmer(IsShimmerKind(tile.Kind));
            _viewsById[tile.Id] = view;
            return view;
        }

        /// <summary>Which kinds breathe while idle — the "I am valuable" ambient cue.</summary>
        private static bool IsShimmerKind(TileKind kind) =>
            kind == TileKind.StripedH || kind == TileKind.StripedV ||
            kind == TileKind.Wrapped || kind == TileKind.Fish ||
            kind == TileKind.Bomb || kind == TileKind.ColorBomb;

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
            if (tile.Kind == TileKind.Chocolate)
                return new Color(0.36f, 0.22f, 0.12f);
            if (tile.Kind == TileKind.Ingredient)
                return new Color(0.95f, 0.88f, 0.72f);
            if (tile.Kind == TileKind.Frosting)
                return new Color(0.85f, 0.9f, 1f);
            if (tile.Kind == TileKind.Swirl)
                return new Color(0.2f, 0.17f, 0.22f);
            if (tile.Kind == TileKind.ChocolateFountain)
                return new Color(0.26f, 0.15f, 0.08f);
            if (tile.Kind == TileKind.MysteryEgg)
                return new Color(0.94f, 0.9f, 0.8f); // cream shell (ColorIndex is -1)

            Color baseColor = _config.tileColors[tile.ColorIndex];
            switch (tile.Kind)
            {
                case TileKind.StripedH:
                case TileKind.StripedV:
                    return Color.Lerp(baseColor, Color.white, 0.45f);
                case TileKind.Wrapped:
                    return Color.Lerp(baseColor, Color.black, 0.35f);
                case TileKind.Fish:
                    return Color.Lerp(baseColor, new Color(0.6f, 0.9f, 1f), 0.35f);
                case TileKind.Bomb:
                    return Color.Lerp(baseColor, Color.black, 0.25f);
                default:
                    return baseColor;
            }
        }

        private float FallDuration(int cellsFallen) =>
            Mathf.Max(minFallDuration, cellsFallen * fallDurationPerCell);

        // WaitForSeconds allocates; detonations request dozens per wave, so identical
        // delays share one cached instance (keyed by millisecond to bound the table).
        private static readonly Dictionary<int, WaitForSeconds> WaitCache = new Dictionary<int, WaitForSeconds>();

        private static WaitForSeconds Wait(float seconds)
        {
            int key = Mathf.RoundToInt(seconds * 1000f);
            if (!WaitCache.TryGetValue(key, out WaitForSeconds wait))
            {
                wait = new WaitForSeconds(key / 1000f);
                WaitCache[key] = wait;
            }
            return wait;
        }

        /// <summary>Shared countdown for one RunAll batch — replaces a per-routine closure alloc.</summary>
        private sealed class RunCounter
        {
            public int Remaining;
        }

        /// <summary>
        /// Runs several animation routines concurrently and finishes when the last
        /// one does — a coroutine-flavoured Promise.all / CompletableFuture.allOf.
        /// </summary>
        private IEnumerator RunAll(List<IEnumerator> routines)
        {
            var counter = new RunCounter { Remaining = routines.Count };
            foreach (IEnumerator routine in routines)
                StartCoroutine(RunThenSignal(routine, counter));

            while (counter.Remaining > 0)
                yield return null;
        }

        private IEnumerator RunThenSignal(IEnumerator routine, RunCounter counter)
        {
            yield return routine;
            counter.Remaining--;
        }
    }
}
