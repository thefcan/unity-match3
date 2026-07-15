using System;
using System.Collections.Generic;
using System.Linq;

namespace Match3.Core
{
    /// <summary>
    /// A special candy the analyzer decided a match should create: what kind, which
    /// cell it appears in, and which cells "fund" it (the view converges those tiles
    /// into the creation point). The resolver mints the actual tile via the factory.
    /// </summary>
    public readonly struct SpecialPlan
    {
        public GridPosition Position { get; }
        public TileKind Kind { get; }
        public int ColorIndex { get; }
        public IReadOnlyList<GridPosition> SourcePositions { get; }

        public SpecialPlan(GridPosition position, TileKind kind, int colorIndex, IReadOnlyList<GridPosition> sourcePositions)
        {
            Position = position;
            Kind = kind;
            ColorIndex = colorIndex;
            SourcePositions = sourcePositions ?? throw new ArgumentNullException(nameof(sourcePositions));
        }
    }

    /// <summary>
    /// Maps match SHAPES to the special candies they create (the Candy Crush rules):
    ///   - 5+ in a straight line  -> colour bomb,
    ///   - two intersecting runs (an L or T) -> wrapped candy at the intersection,
    ///   - exactly 4 in a line -> striped candy, stripes PERPENDICULAR to the run
    ///     (a horizontal 4-match yields the column-clearing candy and vice versa).
    ///
    /// Rules are applied longest-run-first and each cell funds at most one special.
    /// The special appears at the player's swap cell when that cell is part of the
    /// run (feels intentional), else at the run's middle (cascade-made matches).
    /// A special never replaces another special: creation positions must currently
    /// hold a Normal tile, so e.g. a primed wrapped can't be silently overwritten.
    /// </summary>
    public static class SpecialMatchAnalyzer
    {
        public static List<SpecialPlan> Analyze(Board board, List<MatchRun> runs, GridPosition? swapFrom, GridPosition? swapTo)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (runs == null) throw new ArgumentNullException(nameof(runs));

            var plans = new List<SpecialPlan>();
            var consumed = new HashSet<GridPosition>();

            // OrderByDescending is a stable sort, so equal-length runs keep the
            // deterministic order FindMatchRuns produced them in.
            List<MatchRun> ordered = runs.OrderByDescending(run => run.Length).ToList();

            // 1. Colour bombs from straight runs of 5+.
            foreach (MatchRun run in ordered)
            {
                if (run.Length < 5 || IsSpent(run, consumed)) continue;

                GridPosition? position = PickPosition(board, run, swapTo, swapFrom);
                if (position is { } pos)
                {
                    plans.Add(new SpecialPlan(pos, TileKind.ColorBomb, Tile.NoColor, run.Positions));
                    Consume(run, consumed);
                }
            }

            // 2. Wrapped candies from intersecting run pairs (L / T shapes).
            for (int i = 0; i < ordered.Count; i++)
            {
                for (int j = i + 1; j < ordered.Count; j++)
                {
                    MatchRun a = ordered[i];
                    MatchRun b = ordered[j];
                    if (IsSpent(a, consumed) || IsSpent(b, consumed)) continue;

                    // Two straight runs can share at most one cell — the corner.
                    GridPosition? shared = a.Positions.Cast<GridPosition?>()
                        .FirstOrDefault(pos => b.Positions.Contains(pos.Value));
                    if (shared is not { } corner) continue;
                    if (board[corner] is not { } cornerTile || cornerTile.IsSpecial) continue;

                    List<GridPosition> sources = a.Positions.Concat(b.Positions).Distinct().ToList();
                    plans.Add(new SpecialPlan(corner, TileKind.Wrapped, cornerTile.ColorIndex, sources));
                    Consume(a, consumed);
                    Consume(b, consumed);
                }
            }

            // 3. Striped candies from runs of exactly 4.
            foreach (MatchRun run in ordered)
            {
                if (run.Length != 4 || IsSpent(run, consumed)) continue;

                GridPosition? position = PickPosition(board, run, swapTo, swapFrom);
                if (position is { } pos)
                {
                    bool horizontalRun = run.Positions[0].Y == run.Positions[1].Y;
                    TileKind kind = horizontalRun ? TileKind.StripedV : TileKind.StripedH; // perpendicular
                    plans.Add(new SpecialPlan(pos, kind, RunColor(board, run), run.Positions));
                    Consume(run, consumed);
                }
            }

            return plans;
        }

        /// <summary>A run is spent when any of its cells already funded another special.</summary>
        private static bool IsSpent(MatchRun run, HashSet<GridPosition> consumed) =>
            run.Positions.Any(consumed.Contains);

        private static void Consume(MatchRun run, HashSet<GridPosition> consumed)
        {
            foreach (GridPosition pos in run.Positions)
                consumed.Add(pos);
        }

        /// <summary>
        /// Swap cell first (landing cell preferred), then the run's middle, then any
        /// cell — the first candidate currently holding a NORMAL tile wins. Null when
        /// the whole run is specials (nothing may be replaced).
        /// </summary>
        private static GridPosition? PickPosition(Board board, MatchRun run, GridPosition? swapTo, GridPosition? swapFrom)
        {
            IEnumerable<GridPosition> candidates = Enumerable.Empty<GridPosition>();
            if (swapTo is { } to && run.Positions.Contains(to)) candidates = candidates.Append(to);
            if (swapFrom is { } from && run.Positions.Contains(from)) candidates = candidates.Append(from);
            candidates = candidates.Append(run.Positions[run.Length / 2]).Concat(run.Positions);

            foreach (GridPosition pos in candidates)
                if (board[pos] is { } tile && tile.Kind == TileKind.Normal)
                    return pos;

            return null;
        }

        private static int RunColor(Board board, MatchRun run)
        {
            foreach (GridPosition pos in run.Positions)
                if (board[pos] is { } tile && tile.ColorIndex >= 0)
                    return tile.ColorIndex;
            return Tile.NoColor;
        }
    }
}
