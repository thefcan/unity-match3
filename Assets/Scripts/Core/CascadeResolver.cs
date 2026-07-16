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
        private readonly ScoreConfig _scoreConfig;
        private readonly TileFactory _factory; // null => classic mode (no special creation)
        private readonly IRandom _random;      // only used to pick bomb+striped conversion orientations
        private JellyGrid _jelly;              // null => level has no jelly

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
                        if (clearSet.Add(cell) && board[cell] is { } hit && hit.IsSpecial && !processedIds.Contains(hit.Id))
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
                    if (runs.Count == 0 && primedWrapped.Count == 0)
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
                    if (board[pos] is { } tile && tile.IsSpecial && !processedIds.Contains(tile.Id))
                        pending.Enqueue(pos);

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

                if (clearSet.Count == 0 && creations.Count == 0)
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
                List<TileFall> falls = board.ApplyGravity();
                List<TileSpawn> spawns = board.Refill();

                steps.Add(new CascadeStep(cascadeIndex, cleared, falls, spawns, points, runLengths,
                                          creations, detonations, jellyHits));
                cascadeIndex++;
            }

            return new ResolutionResult(steps);
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
