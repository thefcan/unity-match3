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

        private const int MaxBombsOnBoard = 2;

        private readonly ScoreConfig _scoreConfig;
        private readonly TileFactory _factory; // null => classic mode (no special creation)
        private readonly IRandom _random;      // bomb+striped orientations, ingredient columns, chocolate spread
        private JellyGrid _jelly;              // null => level has no jelly
        private LockGrid _locks;               // null => level has no locks
        private FrostingGrid _frosting;        // null => level has no frosting
        private BombTimers _bombs;             // null => level has no bomb candies
        private int _ingredientsToSpawn;       // refill injection budget (CollectIngredients levels)
        private int _bombsToSpawn;             // refill injection budget (bomb levels)
        private int _bombTimerMoves;           // countdown a freshly dispensed bomb starts with

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

        /// <summary>
        /// Attaches the level's frosting layer ledger. Frosting cells hold a
        /// <see cref="TileKind.Frosting"/> tile on the board; every adjacent match or
        /// direct blast peels one layer per wave (<see cref="FrostingHit"/>), and the
        /// tile itself clears with its last layer.
        /// </summary>
        public void AttachFrosting(FrostingGrid frosting)
        {
            _frosting = frosting;
        }

        /// <summary>
        /// Arms the bomb dispenser: up to <paramref name="totalCount"/> bomb candies
        /// enter through refills (never more than <see cref="MaxBombsOnBoard"/> in
        /// play), each starting a <paramref name="timerMoves"/>-move countdown in
        /// <paramref name="timers"/>. Clearing a bomb defuses it; the GAME layer
        /// decides which moves tick the survivors (<see cref="ResolveBombTick"/>).
        /// </summary>
        public void AttachBombs(BombTimers timers, int totalCount, int timerMoves)
        {
            _bombs = timers;
            _bombsToSpawn = Math.Max(0, totalCount);
            _bombTimerMoves = Math.Max(1, timerMoves);
        }

        /// <summary>Resolves without swap context — cascade-made matches only (and shuffle settling).</summary>
        public ResolutionResult Resolve(Board board) => ResolveInternal(board, null, null);

        /// <summary>
        /// Resolves a COMMITTED swap (call after <see cref="Board.Swap"/>). Knowing the
        /// swap cells lets wave 0 fire special+special combos and place created specials
        /// at the cell the player actually touched. Returns an empty result — without
        /// mutating anything — when the swap achieved nothing, so the caller can revert.
        /// <paramref name="countsAsMove"/> is false for the FREE-SWAP booster: like the
        /// hammer it costs no move, so it must not feed the end-of-move chocolate creep
        /// either ("a booster is not a move" — see <see cref="ResolveHammer"/>).
        /// </summary>
        public ResolutionResult ResolveSwap(Board board, GridPosition from, GridPosition to,
                                            bool countsAsMove = true) =>
            ResolveInternal(board, from, to, countsAsMove: countsAsMove);

        /// <summary>
        /// The end-of-level "Sugar Crush": unused moves convert normal candies into
        /// striped ones (four per five moves — Candy Crush's classic rate), then EVERY
        /// special on the board fires and the cascade runs to rest. Cleared specials
        /// pay a finale bonus on top of normal scoring (striped 500, wrapped 1000,
        /// colour bomb 5000). Chocolate never spreads during a finale (the level is
        /// already won). Returns an empty recording when there is nothing to celebrate.
        /// </summary>
        public ResolutionResult ResolveFinale(Board board, int remainingMoves)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));

            var steps = new List<CascadeStep>();

            int quota = Math.Max(0, remainingMoves) * 4 / 5;
            if (_factory != null && quota > 0)
            {
                var candidates = new List<GridPosition>();
                for (int x = 0; x < board.Width; x++)
                {
                    for (int y = 0; y < board.Height; y++)
                    {
                        var pos = new GridPosition(x, y);
                        if (board[pos] is { } tile && tile.Kind == TileKind.Normal &&
                            (_locks == null || !_locks.HasLock(pos)))
                            candidates.Add(pos);
                    }
                }

                var conversions = new List<SpecialCreation>();
                for (int i = 0; i < quota && candidates.Count > 0; i++)
                {
                    int pick = _random != null ? _random.Next(candidates.Count) : 0;
                    GridPosition pos = candidates[pick];
                    candidates.RemoveAt(pick);

                    Tile replaced = board[pos].Value;
                    TileKind kind = _random != null && _random.Next(2) == 0 ? TileKind.StripedV : TileKind.StripedH;
                    Tile striped = _factory.CreateSpecial(replaced.ColorIndex, kind);
                    conversions.Add(new SpecialCreation(striped, replaced, pos, new[] { pos }));
                    board.SetTile(pos, striped);
                }

                if (conversions.Count > 0)
                {
                    // A pure morph step: the view plays the conversions before the
                    // detonation waves below sweep them all up.
                    steps.Add(new CascadeStep(0,
                        Array.Empty<ClearedTile>(), Array.Empty<TileFall>(), Array.Empty<TileSpawn>(),
                        0, Array.Empty<int>(), conversions, Array.Empty<Detonation>(),
                        Array.Empty<JellyHit>(), Array.Empty<LockBreak>(),
                        Array.Empty<ChocolateSpread>(), Array.Empty<IngredientExit>(),
                        Array.Empty<FishStrike>(), Array.Empty<FrostingHit>(), Array.Empty<BombTick>(),
                        Array.Empty<EggHatch>(), isFinale: true));
                }
            }

            ResolutionResult blast = ResolveInternal(board, null, null, finale: true);
            steps.AddRange(blast.Steps);
            return new ResolutionResult(steps);
        }

        /// <summary>
        /// Booster: smash ONE cell (the hammer). The hit flows through the normal
        /// wave machinery, so a special detonates, a lock pops (absorbing the hit),
        /// jelly under the cell takes damage and chocolate crumbles — while an
        /// indestructible ingredient shrugs it off, returning an EMPTY recording
        /// (callers should refund the booster in that case). Never spreads
        /// chocolate: a booster is not a move.
        /// </summary>
        public ResolutionResult ResolveHammer(Board board, GridPosition target)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (!board.IsInside(target))
                return new ResolutionResult(Array.Empty<CascadeStep>());
            return ResolveInternal(board, null, null, hammer: target);
        }

        /// <summary>
        /// Ticks every armed bomb countdown by one and returns the ticks as a single
        /// recording step (empty when no bomb is armed). Deliberately a SEPARATE
        /// entry point: the game layer calls it only after moves that actually count
        /// (a committed swap in Moves mode) — boosters, shuffles and the finale
        /// leave the fuses frozen. A tick reaching 0 means the bomb exploded; the
        /// caller reads that off the step and fails the level.
        /// </summary>
        public ResolutionResult ResolveBombTick(Board board)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (_bombs == null || _bombs.Count == 0)
                return new ResolutionResult(Array.Empty<CascadeStep>());

            var ticks = new List<BombTick>();
            foreach (int id in _bombs.ArmedIds())
            {
                if (FindTilePosition(board, id) is not { } pos || board[pos] is not { } tile)
                {
                    // The tile is gone without a recorded clear (defensive) — drop it.
                    _bombs.Defuse(id);
                    continue;
                }
                ticks.Add(new BombTick(tile, pos, _bombs.Tick(id)));
            }

            if (ticks.Count == 0)
                return new ResolutionResult(Array.Empty<CascadeStep>());

            return new ResolutionResult(new[]
            {
                new CascadeStep(0,
                    Array.Empty<ClearedTile>(), Array.Empty<TileFall>(), Array.Empty<TileSpawn>(),
                    0, Array.Empty<int>(), Array.Empty<SpecialCreation>(), Array.Empty<Detonation>(),
                    Array.Empty<JellyHit>(), Array.Empty<LockBreak>(),
                    Array.Empty<ChocolateSpread>(), Array.Empty<IngredientExit>(),
                    Array.Empty<FishStrike>(), Array.Empty<FrostingHit>(), ticks),
            });
        }

        /// <summary>Finale-only bonus for each special candy consumed by the celebration.</summary>
        private static int FinaleBonus(IReadOnlyList<ClearedTile> cleared)
        {
            int bonus = 0;
            foreach (ClearedTile clear in cleared)
            {
                bonus += clear.Tile.Kind switch
                {
                    TileKind.StripedH => 500,
                    TileKind.StripedV => 500,
                    TileKind.Fish => 500,
                    TileKind.Wrapped => 1000,
                    TileKind.ColorBomb => 5000,
                    _ => 0,
                };
            }
            return bonus;
        }

        /// <summary>
        /// Where a detonating fish darts: the most urgent target wins — jelly first,
        /// then frosting, chocolate, a swirl, then any plain candy — with the injected
        /// random picking among equals. Cells already clearing this wave are skipped so
        /// a school spreads its strikes; indestructibles (ingredients, fountains) are
        /// never worth a dart. Null when the board offers no target at all.
        /// </summary>
        private GridPosition? PickFishTarget(Board board, HashSet<GridPosition> exclude)
        {
            var candidates = new List<GridPosition>();

            void Collect(Func<GridPosition, bool> qualifies)
            {
                if (candidates.Count > 0)
                    return;
                for (int x = 0; x < board.Width; x++)
                {
                    for (int y = 0; y < board.Height; y++)
                    {
                        var pos = new GridPosition(x, y);
                        if (!exclude.Contains(pos) && board[pos].HasValue && qualifies(pos))
                            candidates.Add(pos);
                    }
                }
            }

            Collect(pos => _jelly != null && _jelly.LayersAt(pos) > 0);
            Collect(pos => board[pos].Value.Kind == TileKind.Frosting);
            Collect(pos => board[pos].Value.Kind == TileKind.Chocolate);
            Collect(pos => board[pos].Value.Kind == TileKind.Swirl);
            Collect(pos => board[pos].Value.Kind == TileKind.MysteryEgg);
            Collect(pos => board[pos].Value.IsPlainCandy);
            Collect(pos => board[pos].Value.Kind != TileKind.Ingredient &&
                           board[pos].Value.Kind != TileKind.ChocolateFountain);

            if (candidates.Count == 0)
                return null;
            return candidates[_random != null ? _random.Next(candidates.Count) : 0];
        }

        private ResolutionResult ResolveInternal(Board board, GridPosition? swapFrom, GridPosition? swapTo,
                                                 bool finale = false, GridPosition? hammer = null,
                                                 bool countsAsMove = true)
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
                var fishStrikes = new List<FishStrike>();

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

                // One fish dart: pick the target, record the flight, hit the cell.
                // Returns the target so combos can stack their bonus detonation on it.
                GridPosition? FireFish(Tile fish, GridPosition from)
                {
                    GridPosition? target = PickFishTarget(board, clearSet);
                    if (target is { } to)
                    {
                        fishStrikes.Add(new FishStrike(fish, from, to));
                        EmitDetonation(fish, from, DetonationKind.FishStrike, new List<GridPosition> { to });
                    }
                    return target;
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
                                       clearSet, creations, processedIds, pending, EmitDetonation, FireFish);
                    }
                }

                if (!comboWave)
                {
                    List<MatchRun> runs = board.FindMatchRuns();
                    // 2x2 squares only exist as a match shape in FULL mode — the
                    // classic resolver must keep its original behaviour bit-for-bit.
                    List<MatchSquare> squares = _factory != null ? board.FindSquares() : null;
                    int squareCount = squares?.Count ?? 0;
                    bool seedFinale = finale && cascadeIndex == 0;
                    bool seedHammer = hammer.HasValue && cascadeIndex == 0;
                    // A stray ingredient sitting on the bottom row keeps the cascade
                    // alive one more wave so its exit gets processed and recorded.
                    if (runs.Count == 0 && squareCount == 0 && primedWrapped.Count == 0 &&
                        !seedFinale && !seedHammer && !HasBottomIngredient(board))
                        break;

                    // Hammer seeding: the smashed cell enters the clear set and the
                    // machinery below does the rest (detonation, lock, jelly, chocolate).
                    if (seedHammer)
                        clearSet.Add(hammer.Value);

                    // Finale seeding (Sugar Crush): wave 0 throws EVERY special on the
                    // board into the clear set — the expansion below fires them all.
                    // Locked cells join too: the lock pass turns those hits into plain
                    // LockBreaks (the cage pops, the candy survives).
                    if (seedFinale)
                    {
                        for (int x = 0; x < board.Width; x++)
                        {
                            for (int y = 0; y < board.Height; y++)
                            {
                                var pos = new GridPosition(x, y);
                                if (board[pos] is { } special && special.IsSpecial)
                                    clearSet.Add(pos);
                            }
                        }
                    }

                    // Union the runs into the set of cells to clear; an L / T shape's
                    // shared corner collapses to one cell, cleared and scored once.
                    // countedCells exists only when squares are in play — classic
                    // mode allocates nothing and stays bit-identical.
                    HashSet<GridPosition> countedCells = squareCount > 0 ? new HashSet<GridPosition>() : null;
                    foreach (MatchRun run in runs)
                    {
                        runLengths.Add(run.Length);
                        foreach (GridPosition pos in run.Positions)
                        {
                            clearSet.Add(pos);
                            countedCells?.Add(pos);
                        }
                    }

                    // Squares clear their four cells too, and count as a 4-length run
                    // for the big-match bonuses (time attack's clock top-up) — but
                    // only when the square shares no cell with a counted run or an
                    // earlier counted square: one blob never pays twice.
                    if (squareCount > 0)
                    {
                        foreach (MatchSquare square in squares)
                        {
                            bool overlaps = false;
                            foreach (GridPosition pos in square.Positions)
                            {
                                if (countedCells.Contains(pos))
                                {
                                    overlaps = true;
                                    break;
                                }
                            }

                            if (!overlaps)
                            {
                                runLengths.Add(4);
                                foreach (GridPosition pos in square.Positions)
                                    countedCells.Add(pos);
                            }

                            foreach (GridPosition pos in square.Positions)
                                clearSet.Add(pos);
                        }
                    }

                    // Match shapes mint special candies. A creation cell MORPHS instead
                    // of clearing, so it leaves the clear set. Classic mode skips this.
                    if (_factory != null && (runs.Count > 0 || squareCount > 0))
                    {
                        GridPosition? planFrom = cascadeIndex == 0 ? swapFrom : null;
                        GridPosition? planTo = cascadeIndex == 0 ? swapTo : null;
                        foreach (SpecialPlan plan in SpecialMatchAnalyzer.Analyze(board, runs, squares, planFrom, planTo))
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
                            EmitDetonation(tile, pos, DetonationKind.Row, DetonationRules.BeamRowArea(board, pos));
                            break;
                        case TileKind.StripedV:
                            EmitDetonation(tile, pos, DetonationKind.Column, DetonationRules.BeamColumnArea(board, pos));
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
                        case TileKind.Fish:
                            FireFish(tile, pos);
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

                // ---- 2c. Indestructibles (ingredients, fountains) shrug off any hit ---
                foreach (GridPosition pos in clearSet.ToList())
                    if (board[pos] is { } shielded &&
                        (shielded.Kind == TileKind.Ingredient || shielded.Kind == TileKind.ChocolateFountain))
                        clearSet.Remove(pos);

                // ---- 2c2. Frosting takes DAMAGE instead of clearing --------------------
                // One layer per wave per cell, whether the hit was a direct blast (the
                // cell is in the clear set) or an adjacent clear. Cells with layers
                // left leave the clear set (the tile survives); the last layer lets
                // the clear through.
                var frostingHits = new List<FrostingHit>();
                if (_frosting != null)
                {
                    var hitCells = new HashSet<GridPosition>();
                    var frostingSeeds = new List<GridPosition>();
                    foreach (GridPosition pos in clearSet)
                    {
                        if (board[pos] is { } t && t.Kind == TileKind.Frosting)
                            hitCells.Add(pos);
                        else
                            frostingSeeds.Add(pos);
                    }
                    foreach (SpecialCreation creation in creations)
                        frostingSeeds.Add(creation.Position);
                    foreach (GridPosition seed in frostingSeeds)
                    {
                        foreach (GridPosition n in OrthogonalNeighbors(board, seed))
                            if (board[n] is { } t && t.Kind == TileKind.Frosting)
                                hitCells.Add(n);
                    }

                    foreach (GridPosition pos in hitCells.OrderBy(p => p.X).ThenBy(p => p.Y))
                    {
                        _frosting.Damage(pos);
                        int remaining = _frosting.LayersAt(pos);
                        frostingHits.Add(new FrostingHit(pos, remaining));
                        if (remaining > 0)
                            clearSet.Remove(pos);
                        else
                            clearSet.Add(pos);
                    }
                }

                // ---- 2c3. Mystery eggs HATCH instead of clearing ----------------------
                // A direct hit (the egg is in the clear set) or any orthogonally
                // adjacent clear/creation cracks the shell. The cell never clears —
                // the egg morphs into its hatchling in place, dormant for the rest
                // of this wave (the next wave's match scan picks it up naturally).
                // Cells are walked row-major so the hatch rolls consume the injected
                // random in a board-derived order: same seed, same hatchlings.
                var eggHatches = new List<EggHatch>();
                if (HasEgg(board))
                {
                    var eggCells = new HashSet<GridPosition>();
                    var eggSeeds = new List<GridPosition>();
                    foreach (GridPosition pos in clearSet)
                    {
                        if (board[pos] is { } t && t.Kind == TileKind.MysteryEgg)
                            eggCells.Add(pos);
                        else
                            eggSeeds.Add(pos);
                    }
                    foreach (SpecialCreation creation in creations)
                        eggSeeds.Add(creation.Position);
                    foreach (GridPosition seed in eggSeeds)
                    {
                        foreach (GridPosition n in OrthogonalNeighbors(board, seed))
                            if (board[n] is { } t && t.Kind == TileKind.MysteryEgg &&
                                (_locks == null || !_locks.HasLock(n)))
                                eggCells.Add(n); // a caged egg sleeps through nearby clears
                    }

                    foreach (GridPosition pos in eggCells.OrderBy(p => p.X).ThenBy(p => p.Y))
                    {
                        clearSet.Remove(pos); // the shell cracks; the cell itself never clears
                        if (_factory == null || _random == null)
                            continue; // classic mode: the egg is inert scenery
                        Tile shell = board[pos].Value;
                        Tile hatched = RollHatch();
                        board.SetTile(pos, hatched);
                        eggHatches.Add(new EggHatch(pos, shell, hatched));
                    }
                }

                // ---- 2d. Chocolate (and swirls) next to anything cleared crumble ------
                var adjacencySeeds = new List<GridPosition>(clearSet);
                foreach (SpecialCreation creation in creations)
                    adjacencySeeds.Add(creation.Position); // the morph cell was matched too
                foreach (GridPosition seed in adjacencySeeds)
                {
                    foreach (GridPosition n in OrthogonalNeighbors(board, seed))
                        if (board[n] is { } t && (t.Kind == TileKind.Chocolate || t.Kind == TileKind.Swirl))
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

                if (clearSet.Count == 0 && creations.Count == 0 && lockBreaks.Count == 0 &&
                    ingredientExits.Count == 0 && frostingHits.Count == 0 && eggHatches.Count == 0)
                    break; // e.g. a lone primed wrapped that vanished — nothing to do

                // ---- 3. Snapshot + score (on the final clear set) ---------------------
                List<ClearedTile> cleared = clearSet
                    .Select(pos => new ClearedTile(board[pos].Value, pos))
                    .ToList();
                int points = _scoreConfig.PointsFor(cleared.Count, cascadeIndex);
                if (finale)
                    points += FinaleBonus(cleared);

                // A cleared bomb is a defused bomb — its countdown dies with it.
                if (_bombs != null)
                {
                    foreach (ClearedTile clear in cleared)
                        if (clear.Tile.Kind == TileKind.Bomb)
                            _bombs.Defuse(clear.Tile.Id);
                }

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
                InjectBombSpawn(board, spawns);

                steps.Add(new CascadeStep(cascadeIndex, cleared, falls, spawns, points, runLengths,
                                          creations, detonations, jellyHits, lockBreaks,
                                          Array.Empty<ChocolateSpread>(), ingredientExits,
                                          fishStrikes, frostingHits, Array.Empty<BombTick>(), eggHatches,
                                          isFinale: finale));
                cascadeIndex++;
            }

            // ---- 5. End of move: ignored chocolate creeps ------------------------------
            // Only after a real player MOVE (swap context + at least one wave, and the
            // free-swap booster doesn't count), and only when the whole move destroyed
            // no chocolate — the classic pressure rule.
            if (steps.Count > 0 && swapFrom.HasValue && countsAsMove && _factory != null && !chocolateDestroyed &&
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
        /// Picks one (source, victim) pair — the source is a chocolate block OR a
        /// chocolate fountain (the fountain is why an extinct bloodline revives), the
        /// victim a NORMAL, unlocked candy — mutates the board, and reports the
        /// spread. Null when nothing edible borders a source. Deterministic via the
        /// injected random.
        /// </summary>
        private ChocolateSpread? TrySpreadChocolate(Board board)
        {
            var pairs = new List<(GridPosition from, GridPosition to)>();
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    var pos = new GridPosition(x, y);
                    if (board[pos] is not { } tile ||
                        (tile.Kind != TileKind.Chocolate && tile.Kind != TileKind.ChocolateFountain))
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

        /// <summary>
        /// Turns one freshly refilled tile into an armed bomb candy while the level
        /// still owes some and fewer than <see cref="MaxBombsOnBoard"/> are in play —
        /// at most one per wave, keeping the colour the refill rolled (so the bomb
        /// can't complete a match the plain candy wouldn't have).
        /// </summary>
        private void InjectBombSpawn(Board board, List<TileSpawn> spawns)
        {
            if (_bombsToSpawn <= 0 || _factory == null || _bombs == null || spawns.Count == 0)
                return;
            if (CountBombs(board) >= MaxBombsOnBoard)
                return;

            var candidates = new List<int>();
            for (int i = 0; i < spawns.Count; i++)
                if (spawns[i].Position.Y == board.Height - 1 && spawns[i].Tile.Kind == TileKind.Normal)
                    candidates.Add(i);
            if (candidates.Count == 0)
                for (int i = 0; i < spawns.Count; i++)
                    if (spawns[i].Tile.Kind == TileKind.Normal)
                        candidates.Add(i);
            if (candidates.Count == 0)
                return;

            int pick = candidates[_random != null ? _random.Next(candidates.Count) : 0];
            TileSpawn chosen = spawns[pick];
            Tile bomb = _factory.CreateBomb(chosen.Tile.ColorIndex);
            board.SetTile(chosen.Position, bomb);
            spawns[pick] = new TileSpawn(bomb, chosen.Position, chosen.SpawnHeightOffset);
            _bombs.Arm(bomb.Id, _bombTimerMoves);
            _bombsToSpawn--;
        }

        /// <summary>
        /// What crawls out of a cracked shell. Weights (of 100): 70 plain, 15 striped
        /// (orientation rolled like the finale's conversions), 10 wrapped, 5 fish.
        /// Roll order is fixed — kind, colour, then stripe orientation — so scripted
        /// tests can pin every boundary.
        /// </summary>
        private Tile RollHatch()
        {
            int kindRoll = _random.Next(100);
            int color = _random.Next(_factory.ColorCount);
            if (kindRoll < 70)
                return _factory.Create(color);
            if (kindRoll < 85)
            {
                TileKind stripe = _random.Next(2) == 0 ? TileKind.StripedV : TileKind.StripedH;
                return _factory.CreateSpecial(color, stripe);
            }
            if (kindRoll < 95)
                return _factory.CreateSpecial(color, TileKind.Wrapped);
            return _factory.CreateSpecial(color, TileKind.Fish);
        }

        private static bool HasEgg(Board board)
        {
            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                    if (board[new GridPosition(x, y)] is { } tile && tile.Kind == TileKind.MysteryEgg)
                        return true;
            return false;
        }

        private static int CountBombs(Board board)
        {
            int count = 0;
            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                    if (board[new GridPosition(x, y)] is { } tile && tile.Kind == TileKind.Bomb)
                        count++;
            return count;
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
        /// caller's generic detonation expansion. <paramref name="fireFish"/> is the
        /// wave's dart launcher (it records the strike and hits the target cell).
        /// </summary>
        private void BuildComboWave(
            Board board, SwapKind combo,
            GridPosition fromPos, Tile fromTile, GridPosition toPos, Tile toTile,
            HashSet<GridPosition> clearSet, List<SpecialCreation> creations,
            HashSet<int> processedIds, Queue<GridPosition> pending,
            Action<Tile, GridPosition, DetonationKind, List<GridPosition>> emit,
            Func<Tile, GridPosition, GridPosition?> fireFish)
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

                case SwapKind.FishFish:
                    // A school: both fish are consumed and THREE darts fly.
                    ConsumeSwappedPair();
                    for (int i = 0; i < 3; i++)
                        fireFish(toTile, toPos);
                    break;

                case SwapKind.FishStriped:
                {
                    // Three darts, each detonating a striped beam (random orientation)
                    // where it lands — the partner's power rides along on every strike.
                    ConsumeSwappedPair();
                    var (fishPos, fishTile, partnerTile) = SplitFishPair();
                    for (int i = 0; i < 3; i++)
                    {
                        if (fireFish(fishTile, fishPos) is not { } target)
                            continue;
                        bool horizontal = _random == null || _random.Next(2) == 0;
                        emit(partnerTile, target,
                             horizontal ? DetonationKind.Row : DetonationKind.Column,
                             horizontal
                                 ? DetonationRules.BeamRowArea(board, target)
                                 : DetonationRules.BeamColumnArea(board, target));
                    }
                    break;
                }

                case SwapKind.FishWrapped:
                {
                    ConsumeSwappedPair();
                    var (fishPos, fishTile, partnerTile) = SplitFishPair();
                    for (int i = 0; i < 3; i++)
                    {
                        if (fireFish(fishTile, fishPos) is not { } target)
                            continue;
                        emit(partnerTile, target, DetonationKind.Blast3x3,
                             DetonationRules.BlastArea(board, target, 1));
                    }
                    break;
                }

                case SwapKind.BombFish:
                {
                    // The school strikes for two rounds: six darts, no extra payload.
                    var (bombPos, bombTile, fishPos, fishTile) = SplitBombPair();
                    ConsumeBomb(bombPos, bombTile);
                    processedIds.Add(fishTile.Id);
                    clearSet.Add(fishPos);
                    for (int i = 0; i < 6; i++)
                        fireFish(fishTile, fishPos);
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

            (GridPosition fishPos, Tile fishTile, Tile partnerTile) SplitFishPair() =>
                fromTile.IsFish
                    ? (fromPos, fromTile, toTile)
                    : (toPos, toTile, fromTile);
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
