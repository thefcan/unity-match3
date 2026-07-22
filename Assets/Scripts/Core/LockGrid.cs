using System;
using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// The licorice-lock layer: per-CELL cages that pin the candy standing in them.
    /// Same shape as <see cref="JellyGrid"/> but single-layer and with different
    /// physics: a locked cell is IMMOBILE (the board's gravity/shuffle/swap consult
    /// it via <see cref="Board.AttachLocks"/>), and when a match or blast hits it the
    /// LOCK absorbs the hit — the candy survives, unfreed. The resolver owns breaking.
    /// </summary>
    public sealed class LockGrid
    {
        private readonly bool[,] _locked;

        public int Width { get; }
        public int Height { get; }

        /// <summary>Locks still on the board — the ClearLocks-style live counter.</summary>
        public int TotalRemaining { get; private set; }

        public bool IsClear => TotalRemaining == 0;

        public LockGrid(int width, int height)
        {
            if (width < 1) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 1) throw new ArgumentOutOfRangeException(nameof(height));

            Width = width;
            Height = height;
            _locked = new bool[width, height];
        }

        public static LockGrid FromCells(int width, int height, IEnumerable<GridPosition> cells)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));

            var grid = new LockGrid(width, height);
            foreach (GridPosition cell in cells)
                grid.Set(cell, true);
            return grid;
        }

        public bool HasLock(GridPosition position) =>
            InBounds(position) && _locked[position.X, position.Y];

        public void Set(GridPosition position, bool locked)
        {
            if (!InBounds(position))
                throw new ArgumentOutOfRangeException(nameof(position));

            if (_locked[position.X, position.Y] == locked)
                return;
            _locked[position.X, position.Y] = locked;
            TotalRemaining += locked ? 1 : -1;
        }

        /// <summary>Removes the lock if present. Returns true when a lock actually broke.</summary>
        public bool Break(GridPosition position)
        {
            if (!HasLock(position))
                return false;

            _locked[position.X, position.Y] = false;
            TotalRemaining--;
            return true;
        }

        private bool InBounds(GridPosition position) =>
            position.X >= 0 && position.X < Width && position.Y >= 0 && position.Y < Height;
    }
}
