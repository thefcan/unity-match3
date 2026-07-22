using System;

namespace Match3.Core
{
    /// <summary>
    /// A single tile on the board. Pure data — it knows its identity and colour,
    /// nothing about rendering or position (the Board owns positions).
    ///
    /// <see cref="Id"/> is unique per spawned tile and stays stable while the tile
    /// moves around the board. The view layer uses it to map logic tiles to pooled
    /// GameObjects, so animations can track "the same tile" across falls and swaps.
    ///
    /// <see cref="ColorIndex"/> is an index into the level's colour palette rather
    /// than an actual colour: the core stays free of UnityEngine.Color and the
    /// palette becomes data-driven (see LevelConfig ScriptableObject).
    /// </summary>
    public readonly struct Tile : IEquatable<Tile>
    {
        /// <summary>ColorIndex of a tile that has no colour (only the colour bomb).</summary>
        public const int NoColor = -1;

        public int Id { get; }
        public int ColorIndex { get; }
        public TileKind Kind { get; }

        public Tile(int id, int colorIndex)
            : this(id, colorIndex, TileKind.Normal)
        {
        }

        public Tile(int id, int colorIndex, TileKind kind)
        {
            Id = id;
            ColorIndex = colorIndex;
            Kind = kind;
        }

        /// <summary>
        /// A DETONATING candy (striped/wrapped/bomb). Deliberately not "Kind != Normal":
        /// chocolate and ingredients are non-detonating board pieces, and the resolver's
        /// chain expansion must never try to set them off.
        /// </summary>
        public bool IsSpecial => Kind == TileKind.StripedH || Kind == TileKind.StripedV ||
                                 Kind == TileKind.Wrapped || Kind == TileKind.ColorBomb;

        public bool IsColorBomb => Kind == TileKind.ColorBomb;
        public bool IsStriped => Kind == TileKind.StripedH || Kind == TileKind.StripedV;
        public bool IsChocolate => Kind == TileKind.Chocolate;
        public bool IsIngredient => Kind == TileKind.Ingredient;

        // Identity is the Id alone — a tile never changes colour or kind during its
        // lifetime (a "morphed" tile is a NEW tile minted by the factory).
        public bool Equals(Tile other) => Id == other.Id;

        public override bool Equals(object obj) => obj is Tile other && Equals(other);

        public override int GetHashCode() => Id;

        public override string ToString() => $"Tile#{Id}({Kind}, color {ColorIndex})";
    }
}
