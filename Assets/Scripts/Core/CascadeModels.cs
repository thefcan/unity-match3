using System;
using System.Collections.Generic;
using System.Linq;

namespace Match3.Core
{
    // The data types that flow from the core to the view layer. The resolver computes
    // the ENTIRE outcome of a move up front (every clear, fall and spawn, per cascade
    // wave); the view then just plays the recording back with animations. This one-way
    // data flow is what keeps logic and presentation decoupled.

    /// <summary>
    /// One maximal straight line of 3+ same-coloured tiles. <see cref="Length"/> is
    /// what the game reads to decide bonuses (e.g. a run of 4+ is a "big match").
    /// </summary>
    public readonly struct MatchRun
    {
        public IReadOnlyList<GridPosition> Positions { get; }
        public int Length => Positions.Count;

        public MatchRun(IReadOnlyList<GridPosition> positions)
        {
            Positions = positions ?? throw new ArgumentNullException(nameof(positions));
        }
    }

    /// <summary>A tile that was cleared, and where it stood when it cleared.</summary>
    public readonly struct ClearedTile
    {
        public Tile Tile { get; }
        public GridPosition Position { get; }

        public ClearedTile(Tile tile, GridPosition position)
        {
            Tile = tile;
            Position = position;
        }
    }

    /// <summary>A tile that fell from one cell to another during gravity.</summary>
    public readonly struct TileFall
    {
        public Tile Tile { get; }
        public GridPosition From { get; }
        public GridPosition To { get; }

        public TileFall(Tile tile, GridPosition from, GridPosition to)
        {
            Tile = tile;
            From = from;
            To = to;
        }
    }

    /// <summary>
    /// A freshly created tile and its destination cell. <see cref="SpawnHeightOffset"/>
    /// is how many rows above the board the tile should visually start falling from
    /// (1 = just above the top row), so stacked spawns drop in as a column.
    /// </summary>
    public readonly struct TileSpawn
    {
        public Tile Tile { get; }
        public GridPosition Position { get; }
        public int SpawnHeightOffset { get; }

        public TileSpawn(Tile tile, GridPosition position, int spawnHeightOffset)
        {
            Tile = tile;
            Position = position;
            SpawnHeightOffset = spawnHeightOffset;
        }
    }

    /// <summary>
    /// One wave of a cascade: the tiles that matched, the falls and spawns that
    /// followed, and the points it scored (multiplier already applied).
    /// </summary>
    public sealed class CascadeStep
    {
        /// <summary>0 for the wave caused directly by the player's swap, 1+ for chain reactions.</summary>
        public int CascadeIndex { get; }

        public IReadOnlyList<ClearedTile> Cleared { get; }
        public IReadOnlyList<TileFall> Falls { get; }
        public IReadOnlyList<TileSpawn> Spawns { get; }
        public int Points { get; }

        /// <summary>Length of each individual match run cleared in this wave, e.g. [3, 4].</summary>
        public IReadOnlyList<int> RunLengths { get; }

        public CascadeStep(
            int cascadeIndex,
            IReadOnlyList<ClearedTile> cleared,
            IReadOnlyList<TileFall> falls,
            IReadOnlyList<TileSpawn> spawns,
            int points,
            IReadOnlyList<int> runLengths)
        {
            CascadeIndex = cascadeIndex;
            Cleared = cleared ?? throw new ArgumentNullException(nameof(cleared));
            Falls = falls ?? throw new ArgumentNullException(nameof(falls));
            Spawns = spawns ?? throw new ArgumentNullException(nameof(spawns));
            Points = points;
            RunLengths = runLengths ?? throw new ArgumentNullException(nameof(runLengths));
        }

        /// <summary>How many runs in this wave were at least <paramref name="minLength"/> tiles long.</summary>
        public int BigMatchCount(int minLength) => RunLengths.Count(length => length >= minLength);
    }

    /// <summary>Everything that happened after one committed swap, in playback order.</summary>
    public sealed class ResolutionResult
    {
        public IReadOnlyList<CascadeStep> Steps { get; }

        public bool HadMatches => Steps.Count > 0;

        // Expression-bodied property + LINQ Sum: the C# idiom for a derived value
        // (Java equivalent: steps.stream().mapToInt(CascadeStep::getPoints).sum()).
        public int TotalPoints => Steps.Sum(step => step.Points);

        public ResolutionResult(IReadOnlyList<CascadeStep> steps)
        {
            Steps = steps ?? throw new ArgumentNullException(nameof(steps));
        }
    }
}
