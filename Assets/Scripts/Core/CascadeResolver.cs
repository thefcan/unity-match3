using System;
using System.Collections.Generic;
using System.Linq;

namespace Match3.Core
{
    /// <summary>
    /// Resolves a board until it is stable: find matches -> create specials -> expand
    /// detonations -> score -> clear -> apply gravity -> refill -> repeat while new
    /// matches (or a primed wrapped candy) keep the cascade alive.
    ///
    /// NOTE: Resolve MUTATES the board (that's its job — the board must end up in the
    /// post-cascade state) and returns a step-by-step recording of what it did.
    /// The view animates the recording; it never recomputes rules.
    ///
    /// Two modes:
    ///   - classic (ScoreConfig-only constructor): no special candies are ever created —
    ///     exactly the original match-3 behaviour, kept for tests and simple callers;
    ///   - full (factory + random constructor): match shapes mint specials via
    ///     <see cref="SpecialMatchAnalyzer"/>, specials caught in a clear detonate via
    ///     <see cref="DetonationRules"/>, and <see cref="ResolveSwap"/> understands
    ///     special+special / bomb combos (<see cref="SwapRules"/>).
    /// </summary>
    public sealed class CascadeResolver
    {
        private const int MaxIngredientsOnBoard = 2;

        private readonly ScoreConfig _scoreConfig;
        private readonly TileFactory _factory; // null => classic mode (no special creation)
        private readonly IRandom _random;      // bomb+striped orientations, ingredient columns, chocolate spread
        private JellyGrid _jelly;              // null => level has no jelly
        private LockGrid _locks;               // null => level has no locks
        private int _ingredientsToSpawn;       // refill injection budget (CollectIngredients levels)

        public CascadeResolver(ScoreConfig scoreConfig)
            : this(scoreConfig, null, null)
        {
        }

        public CascadeResolver(ScoreConfig scoreConfig, TileFactory factory, IRandom random)
        {
            _scoreConfig = scoreConfig ?? throw new ArgumentNullException(nameof(scoreConfig));
            _factory = factory;
            _random = random;
        }

        /// <summary>
        /// Attaches the level's jelly layer: every cleared (or special-creation) cell
        /// damages it one layer per wave, recorded as <see cref="JellyHit"/>s. Pass
        /// null for levels without jelly.
        /// </summary>
        public void AttachJelly(JellyGrid jelly)
        {
            _jelly = jelly;
        }

        /// <summary>
        /// Attaches the level's lock layer (also attach it to the Board for mobility).
        /// Locks ABSORB hits: a locked cell hit by a match or blast breaks its lock,
        /// keeps its candy, and is recorded as a <see cref="LockBreak"/>.
        /// </summary>
        public void AttachLocks(LockGrid locks)
        {
            _locks = locks;
        }

        /// <summary>
        /// Arms the refill injector for CollectIngredients levels: up to
        /// <paramref name="totalCount"/> ingredients enter through top-row refills,
        /// never more than <see cref="MaxIngredientsOnBoard"/> in play at once.
        /// </summary>
        public void AttachIngredients(int totalCount)
        {
            _ingredientsToSpawn = Math.Max(0, totalCount);
        }

        /// <summary>Resolves without swap context — cascade-made matches only (and shuffle settling).</summary>
        public ResolutionResult Resolve(Board board) => ResolveInternal(board, null, null);

        /// <summary>
        /// Resolves a COMMITTED swap (call after <see cref="Board.Swap"/>). Knowing the
        /// swap cells lets wave 0 fire special+special combos and place created specials
        /// at the cell the player actually touched. Returns an empty result — without
        /// mutating anything — when the swap achieved nothing, so the caller can revert.
        /// </summary>
        public ResolutionResult ResolveSwap(Board board, GridPosition from, GridPosition to) =>
            ResolveInternal(board, from, to);

        private ResolutionResult ResolveInternal(Board board, GridPosition? swapFrom, GridPosition? swapTo)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));

            var steps = new List<CascadeStep>();
            int cascadeIndex = 0;
            bool chocolateDestroyed = false; // any chocolate cleared during this whole move?

            // Wrapped candies that blasted once and survive, waiting to re-detonate at
            // their post-gravity position next wave. Tracked by Id: falls preserve tiles.
            var primedWrapped = new HashSet<int>();

            while (true)
            {
                // ---- Per-wave working state ------------------------------------------
                var clearSet = new HashSet<GridPosition>();
                var detonations = new List<Detonation>();
                var creations = new List<SpecialCreation>();
                var creationCells = new HashSet<GridPosition>();
                var processedIds = new HashSet<int>(); // specials already handled this wave
                var pending = new Queue<GridPosition>(); // specials waiting to detonate
                var runLengths = new List<int>();

                // Adds a detonation and folds its area into the clear set; any special
                // the blast reaches is queued, which is what makes chains work.
                void EmitDetonation(Tile source, GridPosition origin, DetonationKind kind, List<GridPosition> area)
                {
                    detonations.Add(new Detonation(source, origin, kind, area));
                    foreach (GridPosition cell in area)
                    {
                        if (creationCells.Contains(cell)) continue; // freshly created specials survive the wave
                        bool locked = _locks != null && _locks.HasLock(cell);
                        // A LOCKED special never chains: the lock absorbs the hit
                        // (the lock pass below turns the cell into a LockBreak).
                        if (clearSet.Add(cell) && !locked &&
                            board[cell] is { } hit && hit.IsSpecial && !processedIds.Contains(hit.Id))
                            pending.Enqueue(cell);
                    }
                }

                // ---- 1. Seed the wave: combo swap, or matches + primed second blasts ---
                bool comboWave = false;
                if (cascadeIndex == 0 && swapFrom is { } fromPos && swapTo is { } toPos &&
                    board[fromPos] is { } fromTile && board[toPos] is { } toTile)
                {
                    SwapKind combo = SwapRules.Classify(fromTile, toTile);
                    if (combo != SwapKind.None)
                    {
                        comboWave = true;
                        BuildComboWave(board, combo, fromPos, fromTile, toPos, toTile,
                                       clearSet, creations, processedIds, pending, EmitDetonation);
                    }
                }

                if (!comboWave)
                {
                    List<MatchRun> runs = board.FindMatchRuns();
                    // A stray ingredient sitting on the bottom row keeps the cascade
                    // alive one more wave so its exit gets processed and recorded.
                    if (runs.Count == 0 && primedWrapped.Count == 0 && !HasBottomIngredient(board))
                        break;

                    // Union the runs into the set of cells to clear; an L / T shape's
                    // shared corner collapses to one cell, cleared and scored once.
                    foreach (MatchRun run in runs)
                    {
                        runLengths.Add(run.Length);
                        foreach (GridPosition pos in run.Positions)
                            clearSet.Add(pos);
                    }

                    // Match shapes mint special candies. A creation cell MORPHS instead
                    // of clearing, so it leaves the clear set. Classic mode skips this.
                    if (_factory != null && runs.Count > 0)
                    {
                        GridPosition? planFrom = cascadeIndex == 0 ? swapFrom : null;
                        GridPosition? planTo = cascadeIndex == 0 ? swapTo : null;
                        foreach (SpecialPlan plan in SpecialMatchAnalyzer.Analyze(board, runs, planFrom, planTo))
                        {
                            // A locked cell can't host a fresh special — the lock
                            // absorbs the match; its run cells just clear normally.
                            if (_locks != null && _locks.HasLock(plan.Position))
                                continue;

                            Tile replaced = board[plan.Position].Value;
                            Tile created = _factory.CreateSpecial(plan.ColorIndex, plan.Kind);
                            creations.Add(new SpecialCreation(created, replaced, plan.Position, plan.SourcePositions));
                            creationCells.Add(plan.Position);
                            clearSet.Remove(plan.Position);
                        }
                    }

                    // Primed wrapped candies fire their SECOND blast now, at wherever
                    // gravity left them — and this time they are consumed.
                    foreach (int id in primedWrapped.ToList())
                    {
                        primedWrapped.Remove(id);
                        if (FindTilePosition(board, id) is { } wrappedPos && board[wrappedPos] is { } wrappedTile)
                        {
                            processedIds.Add(id);
                            clearSet.Add(wrappedPos);
                            EmitDetonation(wrappedTile, wrappedPos, DetonationKind.Blast3x3,
                                           DetonationRules.BlastArea(board, wrappedPos, 1));
                        }
                    }
                }

                // ---- 2. Detonation expansion: specials caught in the clear go off -----
                foreach (GridPosition pos in clearSet.ToList())
                {
                    if (_locks != null && _locks.HasLock(pos))
                        continue; // the lock absorbs the hit — the special stays dormant
                    if (board[pos] is { } tile && tile.IsSpecial && !processedIds.Contains(tile.Id))
                        pending.Enqueue(pos);
                }

                while (pending.Count > 0)
                {
                    GridPosition pos = pending.Dequeue();
                    if (board[pos] is not { } tile || !tile.IsSpecial || !processedIds.Add(tile.Id))
                        continue;

                    switch (tile.Kind)
                    {
                        case TileKind.StripedH:
                            EmitDetonation(tile, pos, DetonationKind.Row, DetonationRules.RowArea(board, pos.Y));
                            break;
                        case TileKind.StripedV:
                            EmitDetonation(tile, pos, DetonationKind.Column, DetonationRules.ColumnArea(board, pos.X));
                            break;
                        case TileKind.Wrapped:
                            // First blast: 3x3, but the wrapped itself SURVIVES, primed
                            // to re-detonate next wave (the classic double blast).
                            EmitDetonation(tile, pos, DetonationKind.Blast3x3, DetonationRules.BlastArea(board, pos, 1));
                            primedWrapped.Add(tile.Id);
                            clearSet.Remove(pos);
                            break;
                        case TileKind.ColorBomb:
                            // A bomb set off by a blast targets the most common colour.
                            EmitDetonation(tile, pos, DetonationKind.ColorClear,
                                           DetonationRules.AreaFor(board, pos, TileKind.ColorBomb));
                            break;
                    }
                }

                // ---- 2b. Locks absorb their hits: break the lock, keep the candy ------
                var lockBreaks = new List<LockBreak>();
                if (_locks != null && _locks.TotalRemaining > 0)
                {
                    foreach (GridPosition pos in clearSet.ToList())
                    {
                        if (!_locks.HasLock(pos))
                            continue;
                        _locks.Break(pos);
                        lockBreaks.Add(new LockBreak(pos));
                        clearSet.Remove(pos);
                    }
                }

                // ---- 2c. Ingredients are indestructible — pull them out of any blast --
                foreach (GridPosition pos in clearSet.ToList())
                    if (board[pos] is { } shielded && shielded.Kind == TileKind.Ingredient)
                        clearSet.Remove(pos);

                // ---- 2d. Chocolate next to anything cleared crumbles -------------------
                var adjacencySeeds = new List<GridPosition>(clearSet);
                foreach (SpecialCreation creation in creations)
                    adjacencySeeds.Add(creation.Position); // the morph cell was matched too
                foreach (GridPosition seed in adjacencySeeds)
                {
                    foreach (GridPosition n in OrthogonalNeighbors(board, seed))
                        if (board[n] is { } t && t.Kind == TileKind.Chocolate)
                            clearSet.Add(n);
                }
                foreach (GridPosition pos in clearSet)
                {
                    if (board[pos] is { } t && t.Kind == TileKind.Chocolate)
                    {
                        chocolateDestroyed = true; // covers adjacency AND direct blast hits
                        break;
                    }
                }

                // ---- 2e. Ingredients standing on the bottom row exit this wave ---------
                var ingredientExits = new List<IngredientExit>();
                for (int x = 0; x < board.Width; x++)
                {
                    var pos = new GridPosition(x, 0);
                    if (board[pos] is { } t && t.Kind == TileKind.Ingredient)
                        ingredientExits.Add(new IngredientExit(t, pos));
                }

                if (clearSet.Count == 0 && creations.Count == 0 &&
                    lockBreaks.Count == 0 && ingredientExits.Count == 0)
                    break; // e.g. a lone primed wrapped that vanished — nothing to do

                // ---- 3. Snapshot + score (on the final clear set) ---------------------
                List<ClearedTile> cleared = clearSet
                    .Select(pos => new ClearedTile(board[pos].Value, pos))
                    .ToList();
                int points = _scoreConfig.PointsFor(cleared.Count, cascadeIndex);

                // Jelly takes one hit per matched cell — creation cells were matched
                // too (the special morphs on top of the jelly it just damaged).
                var jellyHits = new List<JellyHit>();
                if (_jelly != null)
                {
                    foreach (GridPosition pos in clearSet)
                        if (_jelly.Damage(pos))
                            jellyHits.Add(new JellyHit(pos, _jelly.LayersAt(pos)));
                    foreach (SpecialCreation creation in creations)
                        if (!clearSet.Contains(creation.Position) && _jelly.Damage(creation.Position))
                            jellyHits.Add(new JellyHit(creation.Position, _jelly.LayersAt(creation.Position)));
                }

                // ---- 4. Mutate: clear, morph creations in, gravity, refill ------------
                board.ClearTiles(clearSet);
                foreach (SpecialCreation creation in creations)
                {
                    // Bomb+striped conversions are consumed by their own blast in the
                    // same wave — they are recorded for the view but never land.
                    if (!clearSet.Contains(creation.Position))
                        board.SetTile(creation.Position, creation.Created);
                }
                if (ingredientExits.Count > 0)
                {
                    var exitCells = new List<GridPosition>(ingredientExits.Count);
                    foreach (IngredientExit exit in ingredientExits)
                        exitCells.Add(exit.Position);
                    board.ClearTiles(exitCells);
                }

                List<TileFall> falls = board.ApplyGravity();
                List<TileSpawn> spawns = board.Refill();
                InjectIngredientSpawn(board, spawns);

                steps.Add(new CascadeStep(cascadeIndex, cleared, falls, spawns, points, runLengths,
                                          creations, detonations, jellyHits, lockBreaks,
                                          Array.Empty<ChocolateSpread>(), ingredientExits));
                cascadeIndex++;
            }

            // ---- 5. End of move: ignored chocolate creeps ------------------------------
            // Only after a real player move (swap context + at least one wave), and only
            // when the whole move destroyed no chocolate — the classic pressure rule.
            if (steps.Count > 0 && swapFrom.HasValue && _factory != null && !chocolateDestroyed &&
                TrySpreadChocolate(board) is { } spread)
            {
                steps.Add(new CascadeStep(cascadeIndex,
                                          Array.Empty<ClearedTile>(), Array.Empty<TileFall>(),
                                          Array.Empty<TileSpawn>(), 0, Array.Empty<int>(),
                                          Array.Empty<SpecialCreation>(), Array.Empty<Detonation>(),
                                          Array.Empty<JellyHit>(), Array.Empty<LockBreak>(),
                                          new[] { spread }, Array.Empty<IngredientExit>()));
            }

            return new ResolutionResult(steps);
        }

        /// <summary>
        /// Picks one (chocolate, victim) pair — victim must be a NORMAL, unlocked
        /// candy — mutates the board, and reports the spread. Null when no chocolate
        /// remains or nothing edible borders one. Deterministic via the injected random.
        /// </summary>
        private ChocolateSpread? TrySpreadChocolate(Board board)
        {
            var pairs = new List<(GridPosition from, GridPosition to)>();
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    var pos = new GridPosition(x, y);
                    if (board[pos] is not { } tile || tile.Kind != TileKind.Chocolate)
                        continue;

                    foreach (GridPosition n in OrthogonalNeighbors(board, pos))
                    {
                        if (board[n] is { } victim && victim.Kind == TileKind.Normal &&
                            (_locks == null || !_locks.HasLock(n)))
                            pairs.Add((pos, n));
                    }
                }
            }

            if (pairs.Count == 0)
                return null;

            (GridPosition from, GridPosition to) = pairs[_random != null ? _random.Next(pairs.Count) : 0];
            Tile consumed = board[to].Value;
            Tile spawned = _factory.CreateChocolate();
            board.SetTile(to, spawned);
            return new ChocolateSpread(from, to, consumed, spawned);
        }

        /// <summary>
        /// Turns one freshly refilled top-row tile into an ingredient while the level
        /// still owes some and fewer than <see cref="MaxIngredientsOnBoard"/> are in
        /// play — at most one per wave, so they trickle in like Candy Crush cherries.
        /// </summary>
        private void InjectIngredientSpawn(Board board, List<TileSpawn> spawns)
        {
            if (_ingredientsToSpawn <= 0 || _factory == null || spawns.Count == 0)
                return;
            if (CountIngredients(board) >= MaxIngredientsOnBoard)
                return;

            var candidates = new List<int>();
            for (int i = 0; i < spawns.Count; i++)
                if (spawns[i].Position.Y == board.Height - 1)
                    candidates.Add(i);
            if (candidates.Count == 0)
                for (int i = 0; i < spawns.Count; i++)
                    candidates.Add(i);

            int pick = candidates[_random != null ? _random.Next(candidates.Count) : 0];
            TileSpawn chosen = spawns[pick];
            Tile ingredient = _factory.CreateIngredient();
            board.SetTile(chosen.Position, ingredient);
            spawns[pick] = new TileSpawn(ingredient, chosen.Position, chosen.SpawnHeightOffset);
            _ingredientsToSpawn--;
        }

        private static bool HasBottomIngredient(Board board)
        {
            for (int x = 0; x < board.Width; x++)
                if (board[new GridPosition(x, 0)] is { } tile && tile.Kind == TileKind.Ingredient)
                    return true;
            return false;
        }

        private static int CountIngredients(Board board)
        {
            int count = 0;
            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                    if (board[new GridPosition(x, y)] is { } tile && tile.Kind == TileKind.Ingredient)
                        count++;
            return count;
        }

        private static IEnumerable<GridPosition> OrthogonalNeighbors(Board board, GridPosition pos)
        {
            var left = new GridPosition(pos.X - 1, pos.Y);
            if (board.IsInside(left)) yield return left;
            var right = new GridPosition(pos.X + 1, pos.Y);
            if (board.IsInside(right)) yield return right;
            var down = new GridPosition(pos.X, pos.Y - 1);
            if (board.IsInside(down)) yield return down;
            var up = new GridPosition(pos.X, pos.Y + 1);
            if (board.IsInside(up)) yield return up;
        }

        /// <summary>
        /// Seeds wave 0 for a special+special (or bomb) swap. Both swapped tiles are
        /// consumed by the combo; chains beyond the initial shape are handled by the
        /// caller's generic detonation expansion.
        /// </summary>
        private void BuildComboWave(
            Board board, SwapKind combo,
            GridPosition fromPos, Tile fromTile, GridPosition toPos, Tile toTile,
            HashSet<GridPosition> clearSet, List<SpecialCreation> creations,
            HashSet<int> processedIds, Queue<GridPosition> pending,
            Action<Tile, GridPosition, DetonationKind, List<GridPosition>> emit)
        {
            switch (combo)
            {
                case SwapKind.StripedStriped:
                    ConsumeSwappedPair();
                    emit(toTile, toPos, DetonationKind.Cross, DetonationRules.CrossArea(board, toPos));
                    break;

                case SwapKind.StripedWrapped:
                    ConsumeSwappedPair();
                    emit(toTile, toPos, DetonationKind.TripleCross, DetonationRules.TripleCrossArea(board, toPos));
                    break;

                case SwapKind.WrappedWrapped:
                    ConsumeSwappedPair();
                    emit(fromTile, fromPos, DetonationKind.Blast5x5, DetonationRules.BlastArea(board, fromPos, 2));
                    emit(toTile, toPos, DetonationKind.Blast5x5, DetonationRules.BlastArea(board, toPos, 2));
                    break;

                case SwapKind.BombNormal:
                {
                    var (bombPos, bombTile, _, otherTile) = SplitBombPair();
                    ConsumeBomb(bombPos, bombTile);
                    emit(bombTile, bombPos, DetonationKind.ColorClear,
                         DetonationRules.ColorArea(board, otherTile.ColorIndex));
                    break;
                }

                case SwapKind.BombWrapped:
                {
                    // The bomb wipes the wrapped's colour; the wrapped is in that area,
                    // so it chains with its normal double-blast behaviour.
                    var (bombPos, bombTile, _, wrappedTile) = SplitBombPair();
                    ConsumeBomb(bombPos, bombTile);
                    emit(bombTile, bombPos, DetonationKind.ColorClear,
                         DetonationRules.ColorArea(board, wrappedTile.ColorIndex));
                    break;
                }

                case SwapKind.BombStriped:
                {
                    // Every tile of the striped's colour turns striped (random
                    // orientation), then they ALL go off, in board order. The
                    // conversions are recorded as creations so the view can show the
                    // morph, but their cells clear in the same wave — they never land.
                    var (bombPos, bombTile, _, stripedTile) = SplitBombPair();
                    ConsumeBomb(bombPos, bombTile);

                    int color = stripedTile.ColorIndex;
                    for (int x = 0; x < board.Width; x++)
                    {
                        for (int y = 0; y < board.Height; y++)
                        {
                            var pos = new GridPosition(x, y);
                            if (board[pos] is not { } tile || tile.ColorIndex != color)
                                continue;

                            if (_locks != null && _locks.HasLock(pos))
                            {
                                // The wipe reaches it, but the lock absorbs the hit —
                                // the generic lock pass turns this into a LockBreak.
                                clearSet.Add(pos);
                                continue;
                            }

                            if (tile.Kind == TileKind.Normal && _factory != null && _random != null)
                            {
                                TileKind kind = _random.Next(2) == 0 ? TileKind.StripedH : TileKind.StripedV;
                                Tile converted = _factory.CreateSpecial(color, kind);
                                creations.Add(new SpecialCreation(converted, tile, pos, new[] { pos }));
                                clearSet.Add(pos);
                                emit(converted, pos,
                                     kind == TileKind.StripedH ? DetonationKind.Row : DetonationKind.Column,
                                     kind == TileKind.StripedH
                                         ? DetonationRules.RowArea(board, pos.Y)
                                         : DetonationRules.ColumnArea(board, pos.X));
                            }
                            else if (tile.IsSpecial)
                            {
                                // Existing specials of that colour (including the swapped
                                // striped itself) chain with their own behaviour.
                                clearSet.Add(pos);
                                pending.Enqueue(pos);
                            }
                            else
                            {
                                clearSet.Add(pos);
                            }
                        }
                    }
                    break;
                }

                case SwapKind.BombBomb:
                {
                    // A full wipe is a full wipe: nothing chains, everything clears.
                    for (int x = 0; x < board.Width; x++)
                        for (int y = 0; y < board.Height; y++)
                            if (board[new GridPosition(x, y)] is { } tile)
                                processedIds.Add(tile.Id);
                    emit(toTile, toPos, DetonationKind.BoardClear, DetonationRules.BoardArea(board));
                    break;
                }
            }

            void ConsumeSwappedPair()
            {
                processedIds.Add(fromTile.Id);
                processedIds.Add(toTile.Id);
                clearSet.Add(fromPos);
                clearSet.Add(toPos);
            }

            void ConsumeBomb(GridPosition bombPos, Tile bombTile)
            {
                processedIds.Add(bombTile.Id);
                clearSet.Add(bombPos);
            }

            (GridPosition bombPos, Tile bombTile, GridPosition otherPos, Tile otherTile) SplitBombPair() =>
                fromTile.IsColorBomb
                    ? (fromPos, fromTile, toPos, toTile)
                    : (toPos, toTile, fromPos, fromTile);
        }

        private static GridPosition? FindTilePosition(Board board, int tileId)
        {
            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                {
                    var pos = new GridPosition(x, y);
                    if (board[pos] is { } tile && tile.Id == tileId)
                        return pos;
                }
            return null;
        }
    }
}
