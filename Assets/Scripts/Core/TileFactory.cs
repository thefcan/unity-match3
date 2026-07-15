using System;
using System.Collections.Generic;
using System.Linq;

namespace Match3.Core
{
    /// <summary>
    /// FACTORY PATTERN: the single place where tiles are created.
    /// Centralising creation gives us:
    ///   - unique, monotonically increasing tile Ids (the view relies on these),
    ///   - one injection point for randomness (deterministic tests),
    ///   - one place to extend later (e.g. weighted colours, special/bomb tiles).
    /// </summary>
    public sealed class TileFactory
    {
        private readonly IRandom _random;
        private int _nextId;

        /// <summary>Number of distinct tile colours this factory can produce.</summary>
        public int ColorCount { get; }

        public TileFactory(int colorCount, IRandom random)
        {
            if (colorCount < 1)
                throw new ArgumentOutOfRangeException(nameof(colorCount), "Need at least one colour.");

            ColorCount = colorCount;
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>Creates a tile with a random colour. Used for refills.</summary>
        public Tile Create() => new Tile(_nextId++, _random.Next(ColorCount));

        /// <summary>Creates a tile with an explicit colour. Used by tests and scripted layouts.</summary>
        public Tile Create(int colorIndex)
        {
            if (colorIndex < 0 || colorIndex >= ColorCount)
                throw new ArgumentOutOfRangeException(nameof(colorIndex));

            return new Tile(_nextId++, colorIndex);
        }

        /// <summary>
        /// Creates a special candy of the given kind and colour. The colour bomb is
        /// the one kind with no colour — <paramref name="colorIndex"/> is ignored for it.
        /// </summary>
        public Tile CreateSpecial(int colorIndex, TileKind kind)
        {
            if (kind == TileKind.ColorBomb)
                return CreateColorBomb();
            if (kind == TileKind.Normal)
                return Create(colorIndex);
            if (colorIndex < 0 || colorIndex >= ColorCount)
                throw new ArgumentOutOfRangeException(nameof(colorIndex));

            return new Tile(_nextId++, colorIndex, kind);
        }

        /// <summary>Creates a colour bomb — the only tile without a colour of its own.</summary>
        public Tile CreateColorBomb() => new Tile(_nextId++, Tile.NoColor, TileKind.ColorBomb);

        /// <summary>
        /// Creates a tile whose colour is NOT in <paramref name="excludedColors"/>.
        /// Used by the initial board fill to avoid starting with ready-made matches.
        /// We build the allowed list and pick once instead of re-rolling in a loop,
        /// so the call is deterministic per random draw (nicer for seeded tests).
        /// </summary>
        public Tile CreateExcluding(IReadOnlyCollection<int> excludedColors)
        {
            // LINQ here reads like a Java Stream: range -> filter -> collect.
            List<int> allowed = Enumerable.Range(0, ColorCount)
                .Where(color => !excludedColors.Contains(color))
                .ToList();

            if (allowed.Count == 0)
                throw new InvalidOperationException("Every colour was excluded — need more colours than constraints.");

            return new Tile(_nextId++, allowed[_random.Next(allowed.Count)]);
        }
    }
}
