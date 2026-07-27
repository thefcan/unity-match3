using System;

namespace Match3.Core
{
    /// <summary>
    /// The frosting blocker's layer ledger. Unlike jelly (which belongs to CELLS and
    /// lets tiles pass through), frosting is a TILE — colourless and immobile — and
    /// this grid only tracks how many layers each frosting tile has left. Keying by
    /// position is safe precisely because frosting never moves. Each adjacent match
    /// or blast hit peels one layer per wave; the resolver clears the tile itself
    /// when the last layer goes.
    /// </summary>
    public sealed class FrostingGrid
    {
        public const int MaxLayers = 3;

        private readonly int[,] _layers;

        public int Width { get; }
        public int Height { get; }

        /// <summary>Sum of all remaining layers — the ClearFrosting objective's total.</summary>
        public int TotalRemaining { get; private set; }

        public bool IsClear => TotalRemaining == 0;

        public FrostingGrid(int width, int height)
        {
            if (width < 1) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 1) throw new ArgumentOutOfRangeException(nameof(height));

            Width = width;
            Height = height;
            _layers = new int[width, height];
        }

        public int LayersAt(GridPosition position) => InBounds(position) ? _layers[position.X, position.Y] : 0;

        public void Set(GridPosition position, int layers)
        {
            if (!InBounds(position))
                throw new ArgumentOutOfRangeException(nameof(position));

            int clamped = Math.Min(Math.Max(layers, 0), MaxLayers);
            TotalRemaining += clamped - _layers[position.X, position.Y];
            _layers[position.X, position.Y] = clamped;
        }

        /// <summary>Removes one layer if any remains. Returns true when a layer actually came off.</summary>
        public bool Damage(GridPosition position)
        {
            if (!InBounds(position) || _layers[position.X, position.Y] == 0)
                return false;

            _layers[position.X, position.Y]--;
            TotalRemaining--;
            return true;
        }

        private bool InBounds(GridPosition position) =>
            position.X >= 0 && position.X < Width && position.Y >= 0 && position.Y < Height;
    }
}
