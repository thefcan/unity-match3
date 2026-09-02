using System;
using System.Collections;
using System.Collections.Generic;
using Match3.Core;
using Match3.UI;
using Match3.View;
using UnityEngine;

namespace Match3.Game
{
    /// <summary>
    /// The composition root and state-machine CONTEXT. It owns the core objects
    /// (Board, CascadeResolver, TileFactory), the current <see cref="GameState"/>,
    /// and the session numbers (score, level, clock) — and it is the only class that
    /// talks both "logic" and "Unity".
    ///
    /// TWO MODES share this root (<see cref="GameMode"/>):
    /// • MOVES — the 120-level campaign. A <see cref="LevelDefinition"/> hands over a
    ///   move limit, objectives and blockers; <see cref="MovesLeft"/> counts down per
    ///   counted move, the objectives decide the win, and leftover moves feed the
    ///   Sugar Crush finale. Relaxed mode swaps the limit for
    ///   <see cref="RelaxedMovesLimit"/> and caps the reward at one star.
    /// • TIME ATTACK — endless levels against a countdown. Reach the level's target
    ///   score; clearing it bumps the level/target and resets the clock, and big
    ///   matches (4+ in a line) add bonus seconds. No move limit at all.
    /// Everything below that is shared: board, resolver, cascade recording, boosters.
    ///
    /// OBSERVER PATTERN via C# events: the UI subscribes to ScoreChanged / LevelChanged /
    /// TimeBonusAwarded / GameEnded instead of being called directly. GameManager has no
    /// reference to any UI class, so HUD elements can be added or removed without
    /// touching game logic. (C# note for Java readers: an <c>event</c> is a
    /// compiler-managed listener list — += / -= replace addListener/removeListener, and
    /// <c>?.Invoke</c> is a null-safe "fire if anyone is listening". The continuously
    /// changing clock is NOT an event — the HUD polls <see cref="TimeLeft"/> each frame
    /// rather than making us fire 60 events a second.)
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        /// <summary>Relaxed mode's move budget — big enough to never run out, small enough to render.</summary>
        public const int RelaxedMovesLimit = 999;

        [SerializeField] private LevelConfig levelConfig;
        [SerializeField] private BoardView boardView;
        [SerializeField] private InputController inputController;

        public event Action<int> ScoreChanged;
        public event Action<int> LevelChanged;
        /// <summary>Fired when a big match adds seconds to the clock; carries the amount.</summary>
        public event Action<float> TimeBonusAwarded;
        /// <summary>Fired when a level is cleared, before the next one starts (clock is paused).</summary>
        public event Action LevelCompleted;
        /// <summary>Fired when a dead board triggers a shuffle (clock is paused).</summary>
        public event Action ShuffleStarted;
        public event Action<GamePhase> PhaseChanged;
        /// <summary>Fired once when the clock runs out (time attack).</summary>
        public event Action GameEnded;
        /// <summary>Moves mode: the move counter changed.</summary>
        public event Action<int> MovesChanged;
        /// <summary>Moves mode: objective progress changed.</summary>
        public event Action ObjectivesChanged;
        /// <summary>Moves mode: level won; carries the 0-3 star rating.</summary>
        public event Action<int> LevelWon;
        /// <summary>Moves mode: the Sugar Crush finale is about to play (banner cue).</summary>
        public event Action FinaleStarted;
        /// <summary>Booster inventory or armed state changed (tray refresh).</summary>
        public event Action BoostersChanged;
        /// <summary>Moves mode: out of moves with objectives unfinished.</summary>
        public event Action LevelFailed;

        public Board Board { get; private set; }
        public CascadeResolver Resolver { get; private set; }
        public GameMode Mode { get; private set; }
        /// <summary>The authored level being played (Moves mode only, else null).</summary>
        public LevelDefinition LevelDefinition { get; private set; }
        /// <summary>Moves mode: swaps remaining. Meaningless in time attack.</summary>
        public int MovesLeft { get; private set; }

        /// <summary>Relaxed mode as it stood when this run STARTED (see BuildNewGame).</summary>
        public bool RelaxedThisRun { get; private set; }

        /// <summary>
        /// Moves the Sugar Crush finale may convert. Relaxed mode hands out 999
        /// moves, and the finale turns 4/5 of the leftovers into striped candies —
        /// unclamped that converts the WHOLE board and farms every mission in one
        /// win, so relaxed runs spend the level's authored budget instead.
        /// </summary>
        public int FinaleMoveBudget => RelaxedThisRun && LevelDefinition != null
            ? Mathf.Min(MovesLeft, LevelDefinition.movesLimit)
            : MovesLeft;
        /// <summary>Moves mode: goal progress. Null in time attack.</summary>
        public ObjectiveTracker Objectives { get; private set; }
        /// <summary>Moves mode: the level's jelly layer. Null when the level (or mode) has none.</summary>
        public JellyGrid Jelly { get; private set; }
        /// <summary>Moves mode: the level's licorice locks. Null when the level has none.</summary>
        public LockGrid Locks { get; private set; }
        /// <summary>Moves mode: the level's frosting layer ledger. Null when the level has none.</summary>
        public FrostingGrid Frosting { get; private set; }
        /// <summary>Moves mode: armed bomb countdowns. Null when the level dispenses no bombs.</summary>
        public BombTimers Bombs { get; private set; }

        /// <summary>True when the current loss came from a bomb, not an empty move counter (fail headline).</summary>
        public bool LastFailWasBomb { get; private set; }

        public void NoteBombFail() => LastFailWasBomb = true;

        /// <summary>Moves and re-armed fuses one Rescue buys on the fail panel.</summary>
        public const int RescueMoves = 5;
        /// <summary>Every armed fuse is raised to at least this on a rescue — a bomb one
        /// move from zero must not eat the purchased window on the first swap.</summary>
        public const int RescueBombFloor = 3;

        /// <summary>One continue per attempt keeps the tension honest. Reset in BuildNewGame.</summary>
        public bool RescueUsedThisAttempt { get; private set; }

        /// <summary>The fail panel's single source of truth for showing the rescue button.</summary>
        public bool CanRescue =>
            Mode == GameMode.Moves && CurrentPhase == GamePhase.LevelFailed
            && !RescueUsedThisAttempt && MetaService.Rescues > 0;

        /// <summary>
        /// Spends one Rescue to continue the failed level: +5 moves, every short
        /// bomb fuse re-armed (fresh, so the rescued move doesn't burn it), and
        /// back to play. The streak survives because the fail was never
        /// registered — see LevelFailedState. Returns false when nothing was spent.
        /// </summary>
        public bool TryRescue()
        {
            if (CurrentPhase != GamePhase.LevelFailed || RescueUsedThisAttempt)
                return false;
            if (!MetaService.TrySpendRescue()) // the only persistent mutation — saves inside
                return false;

            RescueUsedThisAttempt = true;
            AddMoves(RescueMoves);
            if (Bombs != null)
            {
                foreach (int id in Bombs.ArmedIds())
                    if (Bombs.TryGet(id, out int remaining) && remaining < RescueBombFloor)
                        Bombs.Arm(id, remaining == 0 ? RescueMoves : RescueBombFloor);
                boardView.RefreshBombBadges(); // badge text only updates on ticks otherwise
            }
            LastFailWasBomb = false; // the next genuine loss deserves its own headline

            // The fail branches skipped the dead-board check — run it now.
            if (Board.HasPossibleMove())
                SetState(new PlayingState(this));
            else
                SetState(new ShuffleState(this));
            return true;
        }
        public int Score { get; private set; }
        public int Level { get; private set; }
        /// <summary>Score needed to clear the current level (time attack).</summary>
        public int CurrentTarget { get; private set; }
        /// <summary>Seconds left on the clock. Polled by the HUD every frame (time attack).</summary>
        public float TimeLeft { get; private set; }

        public LevelConfig Config => levelConfig;
        public BoardView BoardView => boardView;
        /// <summary>The run's RNG, shared with the tile factory so shuffles stay reproducible under a seed.</summary>
        public IRandom Random => _random;

        private GameState _currentState;
        private IRandom _random;
        private float _idleTime;
        private bool _hintActive;

        private void Awake()
        {
            // Fail fast on missing wiring — a NullReference three frames later is
            // much harder to diagnose than an explicit message at startup.
            if (levelConfig == null) throw new InvalidOperationException($"{name}: LevelConfig is not assigned.");
            if (boardView == null) throw new InvalidOperationException($"{name}: BoardView is not assigned.");
            if (inputController == null) throw new InvalidOperationException($"{name}: InputController is not assigned.");
        }

        private void OnEnable()
        {
            inputController.SwapRequested += HandleSwapRequested;
            inputController.TapRequested += HandleTapRequested;
        }

        private void OnDisable()
        {
            // Always unsubscribe what you subscribe — C# events hold strong references,
            // and a dangling handler on a destroyed object throws on the next invoke.
            inputController.SwapRequested -= HandleSwapRequested;
            inputController.TapRequested -= HandleTapRequested;
        }

        private void Start()
        {
            SetState(new InitState(this));
        }

        private void Update()
        {
            if (_currentState == null) return;
            GamePhase phase = _currentState.Phase;

            // The clock ticks while you're playing or watching a cascade resolve —
            // time attack only; moves mode has no clock.
            if (Mode == GameMode.TimeAttack && (phase == GamePhase.Playing || phase == GamePhase.Resolving))
            {
                TimeLeft -= Time.deltaTime;
                if (TimeLeft <= 0f)
                {
                    TimeLeft = 0f;

                    // Idle and out of time → lose immediately. Mid-cascade, we let the
                    // active resolve finish (its bonus time or a clutch level-up might
                    // still save you); ResolvingState re-checks the clock when it ends.
                    if (phase == GamePhase.Playing)
                        SetState(new GameOverState(this));
                    return;
                }
            }

            // Idle hint: only while it's the player's turn.
            if (_currentState.Phase == GamePhase.Playing)
                TickHint();
            else
                ClearHint();
        }

        private void TickHint()
        {
            // The tutorial veil sits over a Playing board and swallows every touch,
            // so idle time there is the overlay's, not the player's — the hint used
            // to fire (and pulse tiles) behind the dim.
            if (TutorialOverlay.VeilOpen)
            {
                _idleTime = 0f;
                return;
            }

            _idleTime += Time.deltaTime;
            if (_hintActive || _idleTime < levelConfig.hintDelaySeconds)
                return;

            (GridPosition, GridPosition)? move = Board.FindPossibleMove();
            if (move.HasValue)
            {
                boardView.ShowHint(move.Value.Item1, move.Value.Item2);
                _hintActive = true;
            }
        }

        private void ClearHint()
        {
            if (_hintActive)
                boardView.HideHint();
            _hintActive = false;
            _idleTime = 0f;
        }

        /// <summary>Wired to the game-over panel's button.</summary>
        public void Restart()
        {
            SetState(new InitState(this));
        }

        public GamePhase CurrentPhase => _currentState?.Phase ?? GamePhase.Init;

        /// <summary>
        /// Pause toggle (settings button / Android back). Deliberately only from and
        /// back to Playing: pausing mid-cascade would freeze animations half-done, and
        /// the end states have their own overlays.
        /// </summary>
        public void TogglePause()
        {
            if (CurrentPhase == GamePhase.Playing)
                SetState(new PausedState(this));
            else if (CurrentPhase == GamePhase.Paused)
                SetState(new PlayingState(this));
        }

        // ---- State machine plumbing -------------------------------------------------
        // The members below are the context API that states call back into. They're
        // public-but-narrow on purpose: states live in the same assembly and need them;
        // nothing else should mutate score/level/clock directly.

        public void SetState(GameState next)
        {
            // Any state change disarms a waiting booster — the states that need the
            // armed info (hammer target, free-swap pair) receive it via constructor.
            if (ArmedBooster != null)
            {
                ArmedBooster = null;
                BoostersChanged?.Invoke();
            }

            _currentState?.Exit();
            _currentState = next;
            PhaseChanged?.Invoke(next.Phase);
            next.Enter();
        }

        // ---- Boosters -----------------------------------------------------------------

        /// <summary>Armed booster awaiting its target (Hammer: a tap; FreeSwap: a swap gesture).</summary>
        public BoosterKind? ArmedBooster { get; private set; }

        /// <summary>
        /// Tray click. Hammer/FreeSwap arm (or disarm on a second click) and wait for
        /// the target gesture; Shuffle acts immediately. Moves mode + Playing only —
        /// boosters never touch time attack (leaderboard purity).
        /// </summary>
        public bool ArmBooster(BoosterKind kind)
        {
            if (Mode != GameMode.Moves || CurrentPhase != GamePhase.Playing)
                return false;

            if (kind == BoosterKind.Shuffle)
            {
                if (!MetaService.TrySpendBooster(BoosterKind.Shuffle))
                    return false;
                BoostersChanged?.Invoke();
                SetState(new ShuffleState(this, announced: false));
                return true;
            }

            if (ArmedBooster == kind)
            {
                ArmedBooster = null; // toggle off
                BoostersChanged?.Invoke();
                return true;
            }

            if (MetaService.BoosterCount(kind) <= 0)
                return false;

            ArmedBooster = kind;
            BoostersChanged?.Invoke();
            return true;
        }

        public void RaiseBoostersChanged() => BoostersChanged?.Invoke();

        /// <summary>States are plain C# classes; they borrow MonoBehaviour's coroutine runner via this.</summary>
        public Coroutine RunCoroutine(IEnumerator routine) => StartCoroutine(routine);

        /// <summary>
        /// Creates fresh core objects and starts a new run. Called by InitState.
        /// The mode comes from <see cref="GameSession"/>: Moves plays the selected
        /// (or default) LevelDefinition; TimeAttack keeps the original endless loop.
        /// </summary>
        public void BuildNewGame()
        {
            Mode = GameSession.Mode;
            LastFailWasBomb = false;
            RescueUsedThisAttempt = false;
            LevelDefinition = GameSession.SelectedLevel;
            if (Mode == GameMode.Moves && LevelDefinition == null)
                LevelDefinition = Resources.Load<LevelDefinition>("Levels/Level_01");
            if (LevelDefinition == null)
                Mode = GameMode.TimeAttack; // no level asset anywhere — original game

            _random = new SystemRandom();

            if (Mode == GameMode.Moves)
            {
                // The palette caps the colour count: the core only knows indices and
                // every index must have a colour (and candy sprites) to render.
                int colorCount = Mathf.Clamp(LevelDefinition.colorCount, 3, levelConfig.ColorCount);
                var factory = new TileFactory(colorCount, _random);
                Board = new Board(LevelDefinition.width, LevelDefinition.height, factory);
                Board.SetSquaresLive(true); // full resolver honors 2x2s — hints/shuffle must too
                Resolver = new CascadeResolver(LevelDefinition.ToScoreConfig(), factory, _random);

                Level = GameSession.SelectedLevelIndex;
                // Relaxed mode: effectively unlimited moves (the HUD shows ∞). Wins
                // cap at one star (see LevelWonState), so the economy holds. The
                // flag is FROZEN for the run: the pause panel can flip the pref
                // mid-level, and the move budget was already decided here.
                RelaxedThisRun = Prefs.RelaxedOn;
                MovesLeft = RelaxedThisRun ? RelaxedMovesLimit : LevelDefinition.movesLimit;
                Objectives = new ObjectiveTracker(LevelDefinition.ToObjectives());
                Jelly = LevelDefinition.jellyRows > 0
                    ? JellyGrid.BottomRows(LevelDefinition.width, LevelDefinition.height,
                                           LevelDefinition.jellyRows, LevelDefinition.jellyLayers)
                    : null;
                Resolver.AttachJelly(Jelly);

                // Chapter-4 blockers: chocolate replaces candies in place (cannot
                // create a match — it has no colour), locks pin their cells, and the
                // ingredient dispenser arms the refill injector.
                foreach (Vector2Int cell in LevelDefinition.chocolateCells ?? Array.Empty<Vector2Int>())
                {
                    var pos = new GridPosition(cell.x, cell.y);
                    if (Board.IsInside(pos))
                        Board.SetTile(pos, factory.CreateChocolate());
                }
                if (LevelDefinition.lockCells is { Length: > 0 })
                {
                    // Out-of-range cells are dropped, exactly as every sibling blocker
                    // list below does. LockGrid.Set throws on a bad cell, and this runs
                    // inside InitState.Enter — one mistyped authored cell would take the
                    // board, the state machine and the result panel down with it.
                    var lockCells = new List<GridPosition>(LevelDefinition.lockCells.Length);
                    foreach (Vector2Int cell in LevelDefinition.lockCells)
                    {
                        var pos = new GridPosition(cell.x, cell.y);
                        if (Board.IsInside(pos))
                            lockCells.Add(pos);
                    }
                    Locks = lockCells.Count > 0
                        ? LockGrid.FromCells(Board.Width, Board.Height, lockCells)
                        : null;
                }
                else
                {
                    Locks = null;
                }
                Board.AttachLocks(Locks);
                Resolver.AttachLocks(Locks);
                if (LevelDefinition.ingredientCount > 0)
                    Resolver.AttachIngredients(LevelDefinition.ingredientCount);

                // Chapter-5 blockers. Frosting/fountains replace candies in place
                // (colourless — can't create a match); swirls likewise; the bomb
                // dispenser arms the refill injector like ingredients do.
                Frosting = null;
                if (LevelDefinition.frostingCells is { Length: > 0 })
                {
                    Frosting = new FrostingGrid(Board.Width, Board.Height);
                    foreach (Vector3Int cell in LevelDefinition.frostingCells)
                    {
                        var pos = new GridPosition(cell.x, cell.y);
                        if (!Board.IsInside(pos))
                            continue;
                        Board.SetTile(pos, factory.CreateFrosting());
                        Frosting.Set(pos, Mathf.Clamp(cell.z, 1, FrostingGrid.MaxLayers));
                    }
                }
                Resolver.AttachFrosting(Frosting);

                foreach (Vector2Int cell in LevelDefinition.swirlCells ?? Array.Empty<Vector2Int>())
                {
                    var pos = new GridPosition(cell.x, cell.y);
                    if (Board.IsInside(pos))
                        Board.SetTile(pos, factory.CreateSwirl());
                }
                foreach (Vector2Int cell in LevelDefinition.fountainCells ?? Array.Empty<Vector2Int>())
                {
                    var pos = new GridPosition(cell.x, cell.y);
                    if (Board.IsInside(pos))
                        Board.SetTile(pos, factory.CreateFountain());
                }
                foreach (Vector2Int cell in LevelDefinition.eggCells ?? Array.Empty<Vector2Int>())
                {
                    var pos = new GridPosition(cell.x, cell.y);
                    if (Board.IsInside(pos))
                        Board.SetTile(pos, factory.CreateMysteryEgg());
                }

                Bombs = null;
                if (LevelDefinition.bombCount > 0)
                {
                    Bombs = new BombTimers();
                    Resolver.AttachBombs(Bombs, LevelDefinition.bombCount, LevelDefinition.bombTimerMoves);
                }

                CurrentTarget = 0;
                TimeLeft = 0f;

                // Daily-streak reward: extra moves, or a special candy planted on the
                // fresh board. Same-colour replacement can never create a match the
                // no-initial-matches board didn't already have.
                if (MetaService.ConsumePendingReward() is { } boost)
                    ApplyStreakBoost(boost, factory);

                // Butler's Gift: registering the start FIRST breaks the streak if the
                // previous level was abandoned; then consecutive wins pre-load specials.
                MetaService.RegisterLevelStart();
                ApplyWinStreakPreload(factory);
            }
            else
            {
                var factory = new TileFactory(levelConfig.ColorCount, _random);
                Board = new Board(levelConfig.width, levelConfig.height, factory);
                Board.SetSquaresLive(true); // time attack runs the full resolver too
                // Factory + random unlock the resolver's special-candy mode: match shapes
                // mint striped/wrapped/colour-bomb tiles and combos detonate.
                Resolver = new CascadeResolver(levelConfig.ToScoreConfig(), factory, _random);

                Level = 1;
                MovesLeft = 0;
                Objectives = null;
                Jelly = null;
                Locks = null;
                Frosting = null;
                Bombs = null;
                Board.AttachLocks(null);
                CurrentTarget = levelConfig.TargetScoreForLevel(Level);
                TimeLeft = levelConfig.timeLimit;
            }

            Score = 0;

            // The ambience drifts one notch per level (ThemeCurve) — set it BEFORE the
            // UI-refreshing events below so themed widgets read the current chapter.
            UiTheme.SetThemeForLevel(Mode == GameMode.Moves ? Level : 1);
            ApplyAmbience();
            MusicManager.PlayForLevel(Mode == GameMode.Moves ? Level : 1);

            boardView.Initialize(Board, levelConfig, Jelly, Locks, Frosting, Bombs);

            ScoreChanged?.Invoke(Score);
            LevelChanged?.Invoke(Level);
            MovesChanged?.Invoke(MovesLeft);
            ObjectivesChanged?.Invoke();
        }

        private void ApplyStreakBoost(StreakReward boost, TileFactory factory)
        {
            if (boost.Kind == StreakRewardKind.ExtraMoves)
            {
                MovesLeft += boost.Amount;
                return;
            }

            // Booster grants are banked at claim time (MetaService.Claim) and should
            // never reach here — guard so a stray value can't fall through to the
            // colour-bomb default below.
            if (boost.Kind == StreakRewardKind.BoosterHammer ||
                boost.Kind == StreakRewardKind.BoosterFreeSwap ||
                boost.Kind == StreakRewardKind.BoosterShuffle)
                return;

            for (int planted = 0; planted < boost.Amount; planted++)
            {
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    var pos = new GridPosition(_random.Next(Board.Width), _random.Next(Board.Height));
                    if (!(Board[pos] is { } tile) || tile.Kind != TileKind.Normal)
                        continue;

                    Tile replacement = boost.Kind switch
                    {
                        StreakRewardKind.StartStriped => factory.CreateSpecial(tile.ColorIndex,
                            _random.Next(2) == 0 ? TileKind.StripedH : TileKind.StripedV),
                        StreakRewardKind.StartWrapped => factory.CreateSpecial(tile.ColorIndex, TileKind.Wrapped),
                        _ => factory.CreateColorBomb(),
                    };
                    Board.SetTile(pos, replacement);
                    break;
                }
            }
        }

        /// <summary>The win-streak preload ladder: 1 win = striped, 2 = +wrapped, 3+ = +colour bomb.</summary>
        private void ApplyWinStreakPreload(TileFactory factory)
        {
            int tier = WinStreakRules.PreloadCount(MetaService.WinStreak);
            if (tier <= 0)
                return;

            ApplyStreakBoost(new StreakReward(StreakRewardKind.StartStriped, 1), factory);
            if (tier >= 2)
                ApplyStreakBoost(new StreakReward(StreakRewardKind.StartWrapped, 1), factory);
            if (tier >= 3)
                ApplyStreakBoost(new StreakReward(StreakRewardKind.StartColorBomb, 1), factory);
        }

        /// <summary>Tints the scene's ambience (camera + HUD card) with the level's chapter theme.</summary>
        private void ApplyAmbience()
        {
            if (Camera.main != null)
                Camera.main.backgroundColor = UiTheme.ThemeBgBottom;

            GameObject card = GameObject.Find("HudTopCard");
            if (card != null && card.TryGetComponent(out UnityEngine.UI.Image image))
            {
                Color tint = UiTheme.ThemeCard;
                image.color = new Color(tint.r, tint.g, tint.b, 0.88f);
            }
        }

        /// <summary>Moves mode: burns one move (a committed, non-bounced swap).</summary>
        public void SpendMove()
        {
            MovesLeft = Mathf.Max(0, MovesLeft - 1);
            MovesChanged?.Invoke(MovesLeft);
        }

        /// <summary>Grants extra moves mid-level (rescues) — the HUD must hear about it.</summary>
        public void AddMoves(int amount)
        {
            MovesLeft += amount;
            MovesChanged?.Invoke(MovesLeft);
        }

        /// <summary>
        /// Target reached: bump the level and target, reset the score and clock, and
        /// keep playing on the SAME board (no churn — combos can flow into the next level).
        /// </summary>
        public void AdvanceLevel()
        {
            Level++;
            Score = 0;
            CurrentTarget = levelConfig.TargetScoreForLevel(Level);
            TimeLeft = levelConfig.timeLimit;

            ScoreChanged?.Invoke(Score);
            LevelChanged?.Invoke(Level);
        }

        public void AddScore(int points)
        {
            Score += points;
            ScoreChanged?.Invoke(Score);
        }

        public void AddTime(float seconds)
        {
            if (seconds <= 0f) return;
            TimeLeft += seconds;
            TimeBonusAwarded?.Invoke(seconds);
        }

        public void RaiseLevelCompleted()
        {
            LevelCompleted?.Invoke();
        }

        public void RaiseShuffleStarted()
        {
            ShuffleStarted?.Invoke();
        }

        public void RaiseGameEnded()
        {
            GameEnded?.Invoke();
        }

        public void RaiseObjectivesChanged()
        {
            ObjectivesChanged?.Invoke();
        }

        public void RaiseLevelWon(int stars)
        {
            LevelWon?.Invoke(stars);
        }

        public void RaiseFinaleStarted()
        {
            FinaleStarted?.Invoke();
        }

        public void RaiseLevelFailed()
        {
            LevelFailed?.Invoke();
        }

        // ---- Input routing ----------------------------------------------------------

        private void HandleSwapRequested(GridPosition from, GridPosition to)
        {
            // Any interaction dismisses the idle hint and restarts the idle clock.
            ClearHint();

            // The current state decides whether input means anything right now.
            // No "isInputLocked" flags anywhere — that's the State pattern's job.
            _currentState?.OnSwapRequested(from, to);
        }

        private void HandleTapRequested(GridPosition cell)
        {
            ClearHint();
            _currentState?.OnTapRequested(cell);
        }
    }
}
