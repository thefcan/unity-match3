using System;
using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// The match-3 board: a Width x Height grid of optional tiles plus every rule
    /// that manipulates it (swapping, match detection, gravity, refilling).
    ///
    /// This class is PURE C# — no UnityEngine anywhere (enforced by the Match3.Core
    /// assembly definition with "noEngineReferences"). That makes every rule unit-testable
    /// without an editor or play mode, and is the backbone of the project's architecture:
    /// MonoBehaviours render and animate; this class decides.
    ///
    /// Coordinate system: x = column (0 = left), y = row (0 = BOTTOM). Gravity moves
    /// tiles towards y = 0; new tiles spawn above the top row (y = Height - 1).
    ///
    /// C# note for Java readers: <c>Tile?</c> is a nullable VALUE type — conceptually
    /// like Optional&lt;Tile&gt;, but built into the type system. An empty cell is
    /// represented by null; the compiler forces a .HasValue / pattern-match check
    /// before the tile can be used.
    /// </summary>
    public sealed class Board
    {
        private const int MinMatchLength = 3;

        private readonly Tile?[,] _tiles;
        private readonly TileFactory _factory;

        public int Width { get; }
        public int Height { get; }

        /// <summary>
        /// Creates a board filled with random tiles, guaranteed to contain no
        /// pre-existing matches (so the player always starts on a stable board).
        /// </summary>
        public Board(int width, int height, TileFactory factory)
            : this(width, height, factory, fill: true)
        {
        }

        private Board(int width, int height, TileFactory factory, bool fill)
        {
            if (width < MinMatchLength || height < MinMatchLength)
                throw new ArgumentOutOfRangeException(
                    $"Board must be at least {MinMatchLength}x{MinMatchLength}, got {width}x{height}.");

            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            Width = width;
            Height = height;
            _tiles = new Tile?[width, height];

            if (fill)
            {
                if (factory.ColorCount < 3)
                    throw new ArgumentException("Need at least 3 colours to fill a board without matches.");
                FillWithoutMatches();
            }
        }

        /// <summary>
        /// Builds a board from an explicit colour layout — the workhorse of the unit tests.
        /// <paramref name="layout"/> is indexed [row, column] with row 0 at the TOP,
        /// so a test's array literal looks exactly like the board on screen.
        /// Use -1 for an empty cell. Pre-existing matches are allowed (cascade tests need them).
        /// </summary>
        public static Board FromLayout(int[,] layout, TileFactory factory)
        {
            int height = layout.GetLength(0);
            int width = layout.GetLength(1);
            var board = new Board(width, height, factory, fill: false);

            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    int color = layout[row, col];
                    if (color >= 0)
                        board._tiles[col, height - 1 - row] = factory.Create(color); // flip: row 0 = top
                }
            }

            return board;
        }

        /// <summary>Indexer — C#'s syntax for Java-style get(pos): board[pos] instead of board.getTile(pos).</summary>
        public Tile? this[GridPosition pos]
        {
            get
            {
                if (!IsInside(pos))
                    throw new ArgumentOutOfRangeException(nameof(pos), $"{pos} is outside the {Width}x{Height} board.");
                return _tiles[pos.X, pos.Y];
            }
        }

        public bool IsInside(GridPosition pos) =>
            pos.X >= 0 && pos.X < Width && pos.Y >= 0 && pos.Y < Height;

        /// <summary>
        /// Exchanges the contents of two cells. Purely mechanical — adjacency rules and
        /// "does this swap produce a match?" live with the caller (game layer / resolver),
        /// because the board shouldn't dictate game flow.
        /// </summary>
        public void Swap(GridPosition a, GridPosition b)
        {
            if (!IsInside(a) || !IsInside(b))
                throw new ArgumentOutOfRangeException($"Swap positions {a}/{b} must be on the board.");

            (_tiles[a.X, a.Y], _tiles[b.X, b.Y]) = (_tiles[b.X, b.Y], _tiles[a.X, a.Y]); // tuple swap, no temp
        }

        /// <summary>
        /// Tries the swap, checks for matches, then reverts. Lets the game layer
        /// validate a move without committing to it.
        /// </summary>
        public bool WouldSwapMatch(GridPosition a, GridPosition b)
        {
            Swap(a, b);
            bool matches = FindMatches().Count > 0;
            Swap(a, b);
            return matches;
        }

        /// <summary>
        /// The first swap (as its two cells) that would create a match, or null if the
        /// board is dead. Powers both the idle hint and the no-moves shuffle. Testing
        /// each cell against only its right and upper neighbour covers every adjacent
        /// pair exactly once.
        /// </summary>
        public (GridPosition, GridPosition)? FindPossibleMove()
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    var here = new GridPosition(x, y);

                    if (x + 1 < Width)
                    {
                        var right = new GridPosition(x + 1, y);
                        if (WouldSwapMatch(here, right) || IsActivationSwap(here, right)) return (here, right);
                    }
                    if (y + 1 < Height)
                    {
                        var up = new GridPosition(x, y + 1);
                        if (WouldSwapMatch(here, up) || IsActivationSwap(here, up)) return (here, up);
                    }
                }
            }
            return null;
        }

        /// <summary>True when at least one legal swap would create a match or fire a special combo.</summary>
        public bool HasPossibleMove() => FindPossibleMove().HasValue;

        /// <summary>
        /// True when swapping these two cells fires a special+special / bomb combo —
        /// always a legal move even with no colour match, so a board holding a colour
        /// bomb is never "dead".
        /// </summary>
        private bool IsActivationSwap(GridPosition a, GridPosition b) =>
            this[a] is { } tileA && this[b] is { } tileB && SwapRules.IsActivationSwap(tileA, tileB);

        /// <summary>
        /// Rearranges the EXISTING tiles (same colours, new cells) into a layout with no
        /// immediate matches and at least one possible move — the classic "no moves left,
        /// shuffle the board" recovery. Retries random permutations until both hold, with
        /// a safety cap for a degenerate colour mix. Randomness is injected so tests are
        /// deterministic.
        /// </summary>
        public void Shuffle(IRandom random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));

            var tiles = new List<Tile>(Width * Height);
            foreach (Tile? cell in _tiles)
                if (cell.HasValue)
                    tiles.Add(cell.Value);

            const int maxAttempts = 100;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // Fisher–Yates, driven by the injected IRandom.
                for (int i = tiles.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    (tiles[i], tiles[j]) = (tiles[j], tiles[i]);
                }

                int index = 0;
                for (int x = 0; x < Width; x++)
                    for (int y = 0; y < Height; y++)
                        _tiles[x, y] = tiles[index++];

                if (FindMatches().Count == 0 && HasPossibleMove())
                    return;
            }
            // Degenerate fallback (e.g. far too few colours): accept the last permutation.
        }

        /// <summary>
        /// Finds every maximal straight run of 3+ same-coloured tiles, each reported
        /// separately with its length. An L / T shape is two overlapping runs (they
        /// share a cell). Knowing per-run lengths is what lets the game reward a
        /// "4-match" with bonus time, which a flattened position set can't express.
        /// </summary>
        public List<MatchRun> FindMatchRuns()
        {
            var runs = new List<MatchRun>();

            // Horizontal runs: walk each row, closing a run whenever the colour changes.
            for (int y = 0; y < Height; y++)
            {
                int runStart = 0;
                for (int x = 1; x <= Width; x++)
                {
                    bool continuesRun = x < Width && SameColor(x, y, x - 1, y);
                    if (continuesRun) continue;

                    if (x - runStart >= MinMatchLength && _tiles[runStart, y].HasValue)
                    {
                        var positions = new List<GridPosition>(x - runStart);
                        for (int i = runStart; i < x; i++)
                            positions.Add(new GridPosition(i, y));
                        runs.Add(new MatchRun(positions));
                    }
                    runStart = x;
                }
            }

            // Vertical runs: same scan, transposed.
            for (int x = 0; x < Width; x++)
            {
                int runStart = 0;
                for (int y = 1; y <= Height; y++)
                {
                    bool continuesRun = y < Height && SameColor(x, y, x, y - 1);
                    if (continuesRun) continue;

                    if (y - runStart >= MinMatchLength && _tiles[x, runStart].HasValue)
                    {
                        var positions = new List<GridPosition>(y - runStart);
                        for (int i = runStart; i < y; i++)
                            positions.Add(new GridPosition(x, i));
                        runs.Add(new MatchRun(positions));
                    }
                    runStart = y;
                }
            }

            return runs;
        }

        /// <summary>
        /// Every cell that is part of a match, de-duplicated (an L / T shape's shared
        /// corner appears once). Built on <see cref="FindMatchRuns"/> so the two can
        /// never disagree.
        /// </summary>
        public HashSet<GridPosition> FindMatches()
        {
            var matched = new HashSet<GridPosition>();
            foreach (MatchRun run in FindMatchRuns())
                foreach (GridPosition pos in run.Positions)
                    matched.Add(pos);
            return matched;
        }

        /// <summary>
        /// Places a tile directly into a cell, replacing whatever was there. Used by
        /// the resolver to morph a matched tile into a freshly minted special candy.
        /// </summary>
        public void SetTile(GridPosition pos, Tile tile)
        {
            if (!IsInside(pos))
                throw new ArgumentOutOfRangeException(nameof(pos), $"{pos} is outside the {Width}x{Height} board.");
            _tiles[pos.X, pos.Y] = tile;
        }

        /// <summary>Removes the tiles at the given positions, leaving empty (null) cells.</summary>
        public void ClearTiles(IEnumerable<GridPosition> positions)
        {
            foreach (GridPosition pos in positions)
            {
                if (!IsInside(pos))
                    throw new ArgumentOutOfRangeException(nameof(positions), $"{pos} is outside the board.");
                _tiles[pos.X, pos.Y] = null;
            }
        }

        /// <summary>
        /// Lets every tile fall straight down into the empty cells below it.
        /// Returns one <see cref="TileFall"/> per tile that moved, so the view layer
        /// can animate exactly what happened instead of re-deriving it.
        /// </summary>
        public List<TileFall> ApplyGravity()
        {
            var falls = new List<TileFall>();

            for (int x = 0; x < Width; x++)
            {
                int writeY = 0; // lowest cell not yet settled in this column
                for (int y = 0; y < Height; y++)
                {
                    // Pattern matching: "is { } tile" reads as "is non-null; bind to tile".
                    if (_tiles[x, y] is { } tile)
                    {
                        if (y != writeY)
                        {
                            _tiles[x, writeY] = tile;
                            _tiles[x, y] = null;
                            falls.Add(new TileFall(tile, new GridPosition(x, y), new GridPosition(x, writeY)));
                        }
                        writeY++;
                    }
                }
            }

            return falls;
        }

        /// <summary>
        /// Fills every remaining empty cell with a fresh tile from the factory.
        /// Call after <see cref="ApplyGravity"/>, so the gaps are all at the top of
        /// their columns. Each spawn records how many rows ABOVE the board it should
        /// visually start, letting the view drop new tiles in from off-screen.
        /// </summary>
        public List<TileSpawn> Refill()
        {
            var spawns = new List<TileSpawn>();

            for (int x = 0; x < Width; x++)
            {
                int spawnHeight = 1; // first new tile in a column starts 1 row above the top
                for (int y = 0; y < Height; y++)
                {
                    if (_tiles[x, y] == null)
                    {
                        Tile tile = _factory.Create();
                        _tiles[x, y] = tile;
                        spawns.Add(new TileSpawn(tile, new GridPosition(x, y), spawnHeight++));
                    }
                }
            }

            return spawns;
        }

        private bool SameColor(int x1, int y1, int x2, int y2)
        {
            Tile? a = _tiles[x1, y1];
            Tile? b = _tiles[x2, y2];
            // ColorIndex >= 0 excludes colour bombs (NoColor): two adjacent bombs
            // must never count as a colour match.
            return a.HasValue && b.HasValue &&
                   a.Value.ColorIndex >= 0 && a.Value.ColorIndex == b.Value.ColorIndex;
        }

        /// <summary>
        /// Initial fill that never produces a starting match: filling left-to-right,
        /// bottom-to-top, a new tile can only complete a run with the two cells to its
        /// left or the two below — so we exclude those colours when they already match.
        /// At most 2 colours get excluded, hence the 3-colour minimum.
        /// </summary>
        private void FillWithoutMatches()
        {
            var excluded = new HashSet<int>();

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    excluded.Clear();

                    if (x >= 2 && SameColor(x - 1, y, x - 2, y))
                        excluded.Add(_tiles[x - 1, y].Value.ColorIndex);

                    if (y >= 2 && SameColor(x, y - 1, x, y - 2))
                        excluded.Add(_tiles[x, y - 1].Value.ColorIndex);

                    _tiles[x, y] = excluded.Count == 0
                        ? _factory.Create()
                        : _factory.CreateExcluding(excluded);
                }
            }
        }
    }
}
