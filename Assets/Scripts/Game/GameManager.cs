using System;
using System.Collections;
using Match3.Core;
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
    /// Game mode: TIME ATTACK with endless levels. You have a countdown to reach the
    /// level's target score; clearing it bumps the level/target and resets the clock.
    /// Big matches (4+ in a line) add bonus seconds. The run ends only when the clock
    /// hits zero — there is no fixed move limit.
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
        /// <summary>Fired once when the clock runs out.</summary>
        public event Action GameEnded;

        public Board Board { get; private set; }
        public CascadeResolver Resolver { get; private set; }
        public int Score { get; private set; }
        public int Level { get; private set; }
        /// <summary>Score needed to clear the current level.</summary>
        public int CurrentTarget { get; private set; }
        /// <summary>Seconds left on the clock. Polled by the HUD every frame.</summary>
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
        }

        private void OnDisable()
        {
            // Always unsubscribe what you subscribe — C# events hold strong references,
            // and a dangling handler on a destroyed object throws on the next invoke.
            inputController.SwapRequested -= HandleSwapRequested;
        }

        private void Start()
        {
            SetState(new InitState(this));
        }

        private void Update()
        {
            if (_currentState == null) return;
            GamePhase phase = _currentState.Phase;

            // The clock ticks while you're playing or watching a cascade resolve.
            if (phase == GamePhase.Playing || phase == GamePhase.Resolving)
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

        // ---- State machine plumbing -------------------------------------------------
        // The members below are the context API that states call back into. They're
        // public-but-narrow on purpose: states live in the same assembly and need them;
        // nothing else should mutate score/level/clock directly.

        public void SetState(GameState next)
        {
            _currentState?.Exit();
            _currentState = next;
            PhaseChanged?.Invoke(next.Phase);
            next.Enter();
        }

        /// <summary>States are plain C# classes; they borrow MonoBehaviour's coroutine runner via this.</summary>
        public Coroutine RunCoroutine(IEnumerator routine) => StartCoroutine(routine);

        /// <summary>Creates fresh core objects and starts a brand-new run at level 1. Called by InitState.</summary>
        public void BuildNewGame()
        {
            _random = new SystemRandom();
            var factory = new TileFactory(levelConfig.ColorCount, _random);
            Board = new Board(levelConfig.width, levelConfig.height, factory);
            Resolver = new CascadeResolver(levelConfig.ToScoreConfig());

            Level = 1;
            Score = 0;
            CurrentTarget = levelConfig.TargetScoreForLevel(Level);
            TimeLeft = levelConfig.timeLimit;

            boardView.Initialize(Board, levelConfig);

            ScoreChanged?.Invoke(Score);
            LevelChanged?.Invoke(Level);
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

        // ---- Input routing ----------------------------------------------------------

        private void HandleSwapRequested(GridPosition from, GridPosition to)
        {
            // Any interaction dismisses the idle hint and restarts the idle clock.
            ClearHint();

            // The current state decides whether input means anything right now.
            // No "isInputLocked" flags anywhere — that's the State pattern's job.
            _currentState?.OnSwapRequested(from, to);
        }
    }
}
