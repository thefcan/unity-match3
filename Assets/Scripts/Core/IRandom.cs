using System;

namespace Match3.Core
{
    /// <summary>
    /// Randomness abstraction injected into <see cref="TileFactory"/>.
    /// Production uses <see cref="SystemRandom"/>; tests inject a seeded or fully
    /// scripted implementation so board generation and refills are deterministic.
    /// (Same dependency-inversion idea as injecting a Clock or Random in Java tests.)
    /// </summary>
    public interface IRandom
    {
        /// <summary>Returns a value in [0, maxExclusive).</summary>
        int Next(int maxExclusive);
    }

    /// <summary>Default implementation backed by System.Random.</summary>
    public sealed class SystemRandom : IRandom
    {
        private readonly Random _random;

        public SystemRandom()
        {
            _random = new Random();
        }

        /// <summary>Seeded constructor — used by tests for reproducible boards.</summary>
        public SystemRandom(int seed)
        {
            _random = new Random(seed);
        }

        public int Next(int maxExclusive) => _random.Next(maxExclusive);
    }
}
