using System;
using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// Pure geometry: which cells a detonating special (or a special+special combo)
    /// hits. Every area contains only occupied cells — empty (null) cells are skipped
    /// so a snapshot of the area is always safe to take. Nothing here mutates the
    /// board or decides chaining; the resolver owns that.
    /// </summary>
    public static class DetonationRules
    {
        /// <summary>The blast area of a single special detonating on its own.</summary>
        public static List<GridPosition> AreaFor(Board board, GridPosition origin, TileKind kind)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));

            switch (kind)
            {
                case TileKind.StripedH: return RowArea(board, origin.Y);
                case TileKind.StripedV: return ColumnArea(board, origin.X);
                case TileKind.Wrapped: return BlastArea(board, origin, 1);
                // A colour bomb set off by a blast (not a swap) has no partner colour
                // to target, so it clears the board's MOST COMMON colour — deterministic,
                // hence testable, unlike picking one at random.
                case TileKind.ColorBomb: return ColorArea(board, MostCommonColor(board));
                default:
                    throw new ArgumentException($"{kind} does not detonate.", nameof(kind));
            }
        }

        /// <summary>Every occupied cell in row <paramref name="y"/>.</summary>
        public static List<GridPosition> RowArea(Board board, int y)
        {
            var area = new List<GridPosition>(board.Width);
            for (int x = 0; x < board.Width; x++)
                AddIfOccupied(board, new GridPosition(x, y), area);
            return area;
        }

        /// <summary>Every occupied cell in column <paramref name="x"/>.</summary>
        public static List<GridPosition> ColumnArea(Board board, int x)
        {
            var area = new List<GridPosition>(board.Height);
            for (int y = 0; y < board.Height; y++)
                AddIfOccupied(board, new GridPosition(x, y), area);
            return area;
        }

        /// <summary>
        /// A square blast centred on <paramref name="origin"/>, clamped to the board.
        /// Radius 1 = 3x3 (wrapped), radius 2 = 5x5 (wrapped+wrapped combo).
        /// </summary>
        public static List<GridPosition> BlastArea(Board board, GridPosition origin, int radius)
        {
            var area = new List<GridPosition>((2 * radius + 1) * (2 * radius + 1));
            for (int x = origin.X - radius; x <= origin.X + radius; x++)
                for (int y = origin.Y - radius; y <= origin.Y + radius; y++)
                    AddIfOccupied(board, new GridPosition(x, y), area);
            return area;
        }

        /// <summary>Every tile of one colour (colour bombs themselves never qualify: they have no colour).</summary>
        public static List<GridPosition> ColorArea(Board board, int colorIndex)
        {
            var area = new List<GridPosition>();
            if (colorIndex < 0)
                return area;

            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                {
                    var pos = new GridPosition(x, y);
                    if (board[pos] is { } tile && tile.ColorIndex == colorIndex)
                        area.Add(pos);
                }
            return area;
        }

        /// <summary>Every occupied cell — the bomb+bomb full wipe.</summary>
        public static List<GridPosition> BoardArea(Board board)
        {
            var area = new List<GridPosition>(board.Width * board.Height);
            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                    AddIfOccupied(board, new GridPosition(x, y), area);
            return area;
        }

        /// <summary>Full row + full column through <paramref name="origin"/> (striped+striped combo).</summary>
        public static List<GridPosition> CrossArea(Board board, GridPosition origin)
        {
            List<GridPosition> area = RowArea(board, origin.Y);
            foreach (GridPosition pos in ColumnArea(board, origin.X))
                if (pos.Y != origin.Y) // the shared cell is already in the row
                    area.Add(pos);
            return area;
        }

        /// <summary>
        /// Three rows + three columns centred on <paramref name="origin"/>, clamped
        /// (striped+wrapped combo). De-duplicated like <see cref="CrossArea"/>.
        /// </summary>
        public static List<GridPosition> TripleCrossArea(Board board, GridPosition origin)
        {
            var seen = new HashSet<GridPosition>();
            var area = new List<GridPosition>();

            for (int y = origin.Y - 1; y <= origin.Y + 1; y++)
            {
                if (y < 0 || y >= board.Height) continue;
                foreach (GridPosition pos in RowArea(board, y))
                    if (seen.Add(pos))
                        area.Add(pos);
            }
            for (int x = origin.X - 1; x <= origin.X + 1; x++)
            {
                if (x < 0 || x >= board.Width) continue;
                foreach (GridPosition pos in ColumnArea(board, x))
                    if (seen.Add(pos))
                        area.Add(pos);
            }
            return area;
        }

        /// <summary>
        /// The board's most frequent colour; ties break to the LOWEST colour index so
        /// the result is fully deterministic. -1 when no coloured tile exists.
        /// </summary>
        public static int MostCommonColor(Board board)
        {
            var counts = new Dictionary<int, int>();
            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                {
                    if (board[new GridPosition(x, y)] is { } tile && tile.ColorIndex >= 0)
                        counts[tile.ColorIndex] = counts.TryGetValue(tile.ColorIndex, out int c) ? c + 1 : 1;
                }

            int bestColor = Tile.NoColor;
            int bestCount = 0;
            foreach (KeyValuePair<int, int> entry in counts)
            {
                if (entry.Value > bestCount ||
                    (entry.Value == bestCount && entry.Key < bestColor))
                {
                    bestColor = entry.Key;
                    bestCount = entry.Value;
                }
            }
            return bestColor;
        }

        private static void AddIfOccupied(Board board, GridPosition pos, List<GridPosition> area)
        {
            if (board.IsInside(pos) && board[pos].HasValue)
                area.Add(pos);
        }
    }
}
