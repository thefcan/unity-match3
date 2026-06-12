using System;

namespace Match3.Core
{
    /// <summary>
    /// Scoring rules, kept as plain C# so the resolver stays engine-free.
    /// The Unity layer builds one of these from the LevelConfig ScriptableObject.
    ///
    /// Formula: points = clearedTiles * PointsPerTile * (1 + cascadeIndex * CascadeMultiplierStep)
    /// With the defaults, wave 0 scores 1x, wave 1 scores 2x, wave 2 scores 3x...
    /// — a simple, readable combo reward.
    /// </summary>
    public sealed class ScoreConfig
    {
        public int PointsPerTile { get; }
        public int CascadeMultiplierStep { get; }

        public ScoreConfig(int pointsPerTile = 10, int cascadeMultiplierStep = 1)
        {
            if (pointsPerTile <= 0)
                throw new ArgumentOutOfRangeException(nameof(pointsPerTile));
            if (cascadeMultiplierStep < 0)
                throw new ArgumentOutOfRangeException(nameof(cascadeMultiplierStep));

            PointsPerTile = pointsPerTile;
            CascadeMultiplierStep = cascadeMultiplierStep;
        }

        public int PointsFor(int clearedCount, int cascadeIndex) =>
            clearedCount * PointsPerTile * (1 + cascadeIndex * CascadeMultiplierStep);
    }
}
