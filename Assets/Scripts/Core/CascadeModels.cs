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
    /// A tile that MORPHED into a special candy this wave instead of clearing.
    /// <see cref="Replaced"/> is the tile that stood in the cell (the view rebinds its
    /// visual to <see cref="Created"/>); <see cref="SourcePositions"/> are the match
    /// cells that funded the special, for a converge-into-the-morph animation.
    /// When a creation's cell also appears in <see cref="CascadeStep.Cleared"/>, the
    /// special was consumed in the same wave (bomb+striped conversions) — the view
    /// shows a flash, not a persistent tile.
    /// </summary>
    public readonly struct SpecialCreation
    {
        public Tile Created { get; }
        public Tile Replaced { get; }
        public GridPosition Position { get; }
        public IReadOnlyList<GridPosition> SourcePositions { get; }

        public SpecialCreation(Tile created, Tile replaced, GridPosition position, IReadOnlyList<GridPosition> sourcePositions)
        {
            Created = created;
            Replaced = replaced;
            Position = position;
            SourcePositions = sourcePositions ?? throw new ArgumentNullException(nameof(sourcePositions));
        }
    }

    /// <summary>The shape of a detonation — the view picks its VFX purely from this.</summary>
    public enum DetonationKind
    {
        Row,
        Column,
        Blast3x3,
        /// <summary>Striped+striped combo: full row + column cross.</summary>
        Cross,
        /// <summary>Striped+wrapped combo: three rows + three columns.</summary>
        TripleCross,
        /// <summary>Wrapped+wrapped combo blast.</summary>
        Blast5x5,
        /// <summary>Colour bomb: every tile of one colour.</summary>
        ColorClear,
        /// <summary>Bomb+bomb: the full wipe.</summary>
        BoardClear,
    }

    /// <summary>
    /// One jelly layer coming off a cell this wave. <see cref="RemainingLayers"/> is
    /// the cell's state AFTER the hit (0 = jelly gone), so the view can restyle or
    /// remove its overlay without consulting the grid.
    /// </summary>
    public readonly struct JellyHit
    {
        public GridPosition Position { get; }
        public int RemainingLayers { get; }

        public JellyHit(GridPosition position, int remainingLayers)
        {
            Position = position;
            RemainingLayers = remainingLayers;
        }
    }

    /// <summary>A licorice lock breaking this wave — the candy in the cell survives.</summary>
    public readonly struct LockBreak
    {
        public GridPosition Position { get; }

        public LockBreak(GridPosition position)
        {
            Position = position;
        }
    }

    /// <summary>
    /// Chocolate creeping onto a neighbouring candy at the end of a move in which no
    /// chocolate was destroyed. <see cref="Consumed"/> is the candy it ate;
    /// <see cref="Spawned"/> is the new chocolate tile now standing in
    /// <see cref="To"/> — the view rebinds that cell's visual to it.
    /// </summary>
    public readonly struct ChocolateSpread
    {
        public GridPosition From { get; }
        public GridPosition To { get; }
        public Tile Consumed { get; }
        public Tile Spawned { get; }

        public ChocolateSpread(GridPosition from, GridPosition to, Tile consumed, Tile spawned)
        {
            From = from;
            To = to;
            Consumed = consumed;
            Spawned = spawned;
        }
    }

    /// <summary>An ingredient reaching the bottom row and leaving the board this wave.</summary>
    public readonly struct IngredientExit
    {
        public Tile Tile { get; }
        public GridPosition Position { get; }

        public IngredientExit(Tile tile, GridPosition position)
        {
            Tile = tile;
            Position = position;
        }
    }

    /// <summary>One special going off: which tile, from where, what shape, which cells it hit.</summary>
    public readonly struct Detonation
    {
        public Tile Source { get; }
        public GridPosition Origin { get; }
        public DetonationKind Kind { get; }
        public IReadOnlyList<GridPosition> Area { get; }

        public Detonation(Tile source, GridPosition origin, DetonationKind kind, IReadOnlyList<GridPosition> area)
        {
            Source = source;
            Origin = origin;
            Kind = kind;
            Area = area ?? throw new ArgumentNullException(nameof(area));
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

        /// <summary>Length of each individual match run cleared in this wave, e.g. [3, 4]. Empty for combo waves.</summary>
        public IReadOnlyList<int> RunLengths { get; }

        /// <summary>Special candies created (morphed into place) this wave.</summary>
        public IReadOnlyList<SpecialCreation> Creations { get; }

        /// <summary>Specials that went off this wave, in trigger order (chains preserved).</summary>
        public IReadOnlyList<Detonation> Detonations { get; }

        /// <summary>Jelly layers removed this wave (empty when the level has no jelly).</summary>
        public IReadOnlyList<JellyHit> JellyHits { get; }

        /// <summary>Licorice locks broken this wave (their candies survive in place).</summary>
        public IReadOnlyList<LockBreak> LockBreaks { get; }

        /// <summary>Chocolate spread (only ever on the cascade's final, clear-less step).</summary>
        public IReadOnlyList<ChocolateSpread> ChocolateSpreads { get; }

        /// <summary>Ingredients that reached the bottom row and left the board this wave.</summary>
        public IReadOnlyList<IngredientExit> IngredientExits { get; }

        public CascadeStep(
            int cascadeIndex,
            IReadOnlyList<ClearedTile> cleared,
            IReadOnlyList<TileFall> falls,
            IReadOnlyList<TileSpawn> spawns,
            int points,
            IReadOnlyList<int> runLengths)
            : this(cascadeIndex, cleared, falls, spawns, points, runLengths,
                   Array.Empty<SpecialCreation>(), Array.Empty<Detonation>())
        {
        }

        public CascadeStep(
            int cascadeIndex,
            IReadOnlyList<ClearedTile> cleared,
            IReadOnlyList<TileFall> falls,
            IReadOnlyList<TileSpawn> spawns,
            int points,
            IReadOnlyList<int> runLengths,
            IReadOnlyList<SpecialCreation> creations,
            IReadOnlyList<Detonation> detonations)
            : this(cascadeIndex, cleared, falls, spawns, points, runLengths,
                   creations, detonations, Array.Empty<JellyHit>())
        {
        }

        public CascadeStep(
            int cascadeIndex,
            IReadOnlyList<ClearedTile> cleared,
            IReadOnlyList<TileFall> falls,
            IReadOnlyList<TileSpawn> spawns,
            int points,
            IReadOnlyList<int> runLengths,
            IReadOnlyList<SpecialCreation> creations,
            IReadOnlyList<Detonation> detonations,
            IReadOnlyList<JellyHit> jellyHits)
            : this(cascadeIndex, cleared, falls, spawns, points, runLengths, creations, detonations, jellyHits,
                   Array.Empty<LockBreak>(), Array.Empty<ChocolateSpread>(), Array.Empty<IngredientExit>())
        {
        }

        public CascadeStep(
            int cascadeIndex,
            IReadOnlyList<ClearedTile> cleared,
            IReadOnlyList<TileFall> falls,
            IReadOnlyList<TileSpawn> spawns,
            int points,
            IReadOnlyList<int> runLengths,
            IReadOnlyList<SpecialCreation> creations,
            IReadOnlyList<Detonation> detonations,
            IReadOnlyList<JellyHit> jellyHits,
            IReadOnlyList<LockBreak> lockBreaks,
            IReadOnlyList<ChocolateSpread> chocolateSpreads,
            IReadOnlyList<IngredientExit> ingredientExits)
        {
            CascadeIndex = cascadeIndex;
            Cleared = cleared ?? throw new ArgumentNullException(nameof(cleared));
            Falls = falls ?? throw new ArgumentNullException(nameof(falls));
            Spawns = spawns ?? throw new ArgumentNullException(nameof(spawns));
            Points = points;
            RunLengths = runLengths ?? throw new ArgumentNullException(nameof(runLengths));
            Creations = creations ?? throw new ArgumentNullException(nameof(creations));
            Detonations = detonations ?? throw new ArgumentNullException(nameof(detonations));
            JellyHits = jellyHits ?? throw new ArgumentNullException(nameof(jellyHits));
            LockBreaks = lockBreaks ?? throw new ArgumentNullException(nameof(lockBreaks));
            ChocolateSpreads = chocolateSpreads ?? throw new ArgumentNullException(nameof(chocolateSpreads));
            IngredientExits = ingredientExits ?? throw new ArgumentNullException(nameof(ingredientExits));
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
