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
    /// and the session numbers (score, moves) — and it is the only class that talks
    /// both "logic" and "Unity".
    ///
    /// OBSERVER PATTERN via C# events: the UI subscribes to ScoreChanged / MovesChanged /
    /// GameEnded instead of being called directly. GameManager has no reference to any
    /// UI class, so HUD elements can be added or removed without touching game logic.
    /// (C# note for Java readers: an <c>event</c> is a compiler-managed listener list —
    /// the += / -= syntax replaces addListener/removeListener boilerplate, and
    /// <c>?.Invoke</c> is a null-safe "fire if anyone is listening".)
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private LevelConfig levelConfig;
        [SerializeField] private BoardView boardView;
        [SerializeField] private InputController inputController;

        public event Action<int> ScoreChanged;
        public event Action<int> MovesChanged;
        public event Action<GamePhase> PhaseChanged;
        /// <summary>Fired once per game; true = target score reached, false = out of moves.</summary>
        public event Action<bool> GameEnded;

        public Board Board { get; private set; }
        public CascadeResolver Resolver { get; private set; }
        public int Score { get; private set; }
        public int MovesLeft { get; private set; }

        public LevelConfig Level => levelConfig;
        public BoardView BoardView => boardView;

        private GameState _currentState;

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

        /// <summary>Wired to the game-over panel's button.</summary>
        public void Restart()
        {
            SetState(new InitState(this));
        }

        // ---- State machine plumbing -------------------------------------------------
        // The members below are the context API that states call back into. They're
        // public-but-narrow on purpose: states live in the same assembly and need them;
        // nothing else should mutate score/moves directly.

        public void SetState(GameState next)
        {
            _currentState?.Exit();
            _currentState = next;
            PhaseChanged?.Invoke(next.Phase);
            next.Enter();
        }

        /// <summary>States are plain C# classes; they borrow MonoBehaviour's coroutine runner via this.</summary>
        public Coroutine RunCoroutine(IEnumerator routine) => StartCoroutine(routine);

        /// <summary>Creates fresh core objects and resets session numbers. Called by InitState.</summary>
        public void BuildNewGame()
        {
            var factory = new TileFactory(levelConfig.ColorCount, new SystemRandom());
            Board = new Board(levelConfig.width, levelConfig.height, factory);
            Resolver = new CascadeResolver(levelConfig.ToScoreConfig());

            Score = 0;
            MovesLeft = levelConfig.moveLimit;

            boardView.Initialize(Board, levelConfig);

            ScoreChanged?.Invoke(Score);
            MovesChanged?.Invoke(MovesLeft);
        }

        public void AddScore(int points)
        {
            Score += points;
            ScoreChanged?.Invoke(Score);
        }

        public void ConsumeMove()
        {
            MovesLeft--;
            MovesChanged?.Invoke(MovesLeft);
        }

        public void RaiseGameEnded(bool won)
        {
            GameEnded?.Invoke(won);
        }

        // ---- Input routing ----------------------------------------------------------

        private void HandleSwapRequested(GridPosition from, GridPosition to)
        {
            // The current state decides whether input means anything right now.
            // No "isInputLocked" flags anywhere — that's the State pattern's job.
            _currentState?.OnSwapRequested(from, to);
        }
    }
}
