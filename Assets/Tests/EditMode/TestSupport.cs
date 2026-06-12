using System;
using Match3.Core;

namespace Match3.Tests
{
    /// <summary>
    /// Readable colour names for board layouts in tests.
    /// Used via "using static Match3.Tests.TestColors;" — C#'s version of Java's static import —
    /// so layout literals read like a picture of the board.
    /// </summary>
    public static class TestColors
    {
        public const int A = 0;
        public const int B = 1;
        public const int C = 2;
        public const int D = 3;
        public const int E = 4;
        public const int _ = -1; // empty cell
    }

    /// <summary>
    /// A fully scripted IRandom: returns a fixed sequence of values, then throws.
    /// Makes refill colours during cascades 100% deterministic — no flaky tests.
    /// </summary>
    public sealed class SequenceRandom : IRandom
    {
        private readonly int[] _values;
        private int _index;

        public SequenceRandom(params int[] values)
        {
            _values = values ?? throw new ArgumentNullException(nameof(values));
        }

        public int Next(int maxExclusive)
        {
            if (_index >= _values.Length)
                throw new InvalidOperationException(
                    $"SequenceRandom exhausted after {_values.Length} draws — the test consumed more randomness than scripted.");

            int value = _values[_index++];
            if (value < 0 || value >= maxExclusive)
                throw new InvalidOperationException(
                    $"Scripted value {value} is out of range [0, {maxExclusive}).");

            return value;
        }
    }

    public static class TestFactories
    {
        /// <summary>Factory with seeded pseudo-randomness — reproducible but realistic.</summary>
        public static TileFactory Seeded(int colorCount = 5, int seed = 0) =>
            new TileFactory(colorCount, new SystemRandom(seed));

        /// <summary>Factory whose random draws are fully scripted.</summary>
        public static TileFactory Scripted(int colorCount = 5, params int[] draws) =>
            new TileFactory(colorCount, new SequenceRandom(draws));
    }
}
