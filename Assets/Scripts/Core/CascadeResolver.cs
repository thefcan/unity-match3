using System;
using System.Collections.Generic;
using System.Linq;

namespace Match3.Core
{
    /// <summary>
    /// Resolves a board until it is stable: find matches -> score -> clear ->
    /// apply gravity -> refill -> repeat while new matches keep forming (cascades).
    ///
    /// NOTE: Resolve MUTATES the board (that's its job — the board must end up in the
    /// post-cascade state) and returns a step-by-step recording of what it did.
    /// The view animates the recording; it never recomputes rules.
    /// </summary>
    public sealed class CascadeResolver
    {
        private readonly ScoreConfig _scoreConfig;

        public CascadeResolver(ScoreConfig scoreConfig)
        {
            _scoreConfig = scoreConfig ?? throw new ArgumentNullException(nameof(scoreConfig));
        }

        public ResolutionResult Resolve(Board board)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));

            var steps = new List<CascadeStep>();
            int cascadeIndex = 0;

            while (true)
            {
                List<MatchRun> runs = board.FindMatchRuns();
                if (runs.Count == 0)
                    break;

                // Union the runs into the set of cells to clear; an L / T shape's shared
                // corner collapses to one cell, so it's cleared and scored only once.
                var matched = new HashSet<GridPosition>();
                foreach (MatchRun run in runs)
                    foreach (GridPosition pos in run.Positions)
                        matched.Add(pos);

                // Snapshot the matched tiles before clearing — the view needs to know
                // WHICH tile popped where. board[pos].Value is safe: a matched cell
                // is by definition non-empty.
                List<ClearedTile> cleared = matched
                    .Select(pos => new ClearedTile(board[pos].Value, pos))
                    .ToList();

                // Per-run lengths travel with the step so the game can reward big
                // matches (e.g. +time for any 4+ run) without re-scanning the board.
                List<int> runLengths = runs.Select(run => run.Length).ToList();
                int points = _scoreConfig.PointsFor(cleared.Count, cascadeIndex);

                board.ClearTiles(matched);
                List<TileFall> falls = board.ApplyGravity();
                List<TileSpawn> spawns = board.Refill();

                steps.Add(new CascadeStep(cascadeIndex, cleared, falls, spawns, points, runLengths));
                cascadeIndex++;
            }

            return new ResolutionResult(steps);
        }
    }
}
