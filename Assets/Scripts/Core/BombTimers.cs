using System;
using System.Collections.Generic;
using System.Linq;

namespace Match3.Core
{
    /// <summary>
    /// The countdowns of every armed bomb candy, keyed by TILE ID — bombs are
    /// coloured candies that fall and match like normal ones, so a position key
    /// would go stale on the first gravity pass. Matching a bomb defuses it (the
    /// resolver calls <see cref="Defuse"/> for every cleared bomb); the game layer
    /// ticks the survivors once per counted player move and fails the level when
    /// any counter reaches zero.
    /// </summary>
    public sealed class BombTimers
    {
        private readonly Dictionary<int, int> _remaining = new Dictionary<int, int>();

        public int Count => _remaining.Count;

        public void Arm(int tileId, int moves)
        {
            if (moves < 1)
                throw new ArgumentOutOfRangeException(nameof(moves), "A bomb needs at least one move on the clock.");
            _remaining[tileId] = moves;
        }

        public bool TryGet(int tileId, out int remaining) => _remaining.TryGetValue(tileId, out remaining);

        public void Defuse(int tileId) => _remaining.Remove(tileId);

        /// <summary>Armed ids in ascending order — a stable tick order for deterministic recordings.</summary>
        public List<int> ArmedIds() => _remaining.Keys.OrderBy(id => id).ToList();

        /// <summary>Decrements one counter (floor 0) and returns the value after the tick.</summary>
        public int Tick(int tileId)
        {
            if (!_remaining.TryGetValue(tileId, out int value))
                return 0;
            value = Math.Max(0, value - 1);
            _remaining[tileId] = value;
            return value;
        }
    }
}
