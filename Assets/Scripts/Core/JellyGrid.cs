using System;

namespace Match3.Core
{
    /// <summary>
    /// The jelly blocker layer: a per-CELL state grid that lives alongside the board.
    /// Jelly belongs to cells, not tiles — gravity moves tiles THROUGH jelly without
    /// disturbing it. Clearing a tile on a jelly cell removes one layer (cells carry
    /// 1 or 2 layers); the moves-mode ClearJelly objective counts removed layers.
    /// Kept separate from <see cref="Board"/> so classic callers never pay for it.
    /// </summary>
    public sealed class JellyGrid
    {
        public const int MaxLayers = 2;

        private readonly int[,] _layers;

        public int Width { get; }
        public int Height { get; }

        /// <summary>Sum of all remaining layers — the ClearJelly objective's live counter.</summary>
        public int TotalRemaining { get; private set; }

        public bool IsClear => TotalRemaining == 0;

        public JellyGrid(int width, int height)
        {
            if (width < 1) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 1) throw new ArgumentOutOfRangeException(nameof(height));

            Width = width;
            Height = height;
            _layers = new int[width, height];
        }

        /// <summary>
        /// The standard authored pattern: the bottom <paramref name="rows"/> rows
        /// (y = 0 is the gravity floor) covered with <paramref name="layers"/> layers.
        /// </summary>
        public static JellyGrid BottomRows(int width, int height, int rows, int layers)
        {
            var grid = new JellyGrid(width, height);
            int coveredRows = Math.Min(Math.Max(rows, 0), height);
            for (int y = 0; y < coveredRows; y++)
                for (int x = 0; x < width; x++)
                    grid.Set(new GridPosition(x, y), layers);
            return grid;
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
