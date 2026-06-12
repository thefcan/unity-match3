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
                HashSet<GridPosition> matches = board.FindMatches();
                if (matches.Count == 0)
                    break;

                // Snapshot the matched tiles before clearing — the view needs to know
                // WHICH tile popped where. board[pos].Value is safe: a matched cell
                // is by definition non-empty.
                List<ClearedTile> cleared = matches
                    .Select(pos => new ClearedTile(board[pos].Value, pos))
                    .ToList();

                int points = _scoreConfig.PointsFor(cleared.Count, cascadeIndex);

                board.ClearTiles(matches);
                List<TileFall> falls = board.ApplyGravity();
                List<TileSpawn> spawns = board.Refill();

                steps.Add(new CascadeStep(cascadeIndex, cleared, falls, spawns, points));
                cascadeIndex++;
            }

            return new ResolutionResult(steps);
        }
    }
}
