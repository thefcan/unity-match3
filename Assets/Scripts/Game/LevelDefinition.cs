using System;
using System.Linq;
using Match3.Core;
using UnityEngine;

namespace Match3.Game
{
    /// <summary>
    /// One authored moves-limited level: board size, colour count, the move budget,
    /// the objectives to complete, and the star thresholds. The time-attack mode keeps
    /// its own <see cref="LevelConfig"/>; this asset is the Candy-Crush-style contract.
    ///
    /// Create via: Assets > Create > Match3 > Level Definition (or the
    /// Match3/Generate/Level Definitions menu, which authors a whole campaign).
    /// </summary>
    [CreateAssetMenu(fileName = "LevelDefinition", menuName = "Match3/Level Definition")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [Serializable]
        public struct ObjectiveSpec
        {
            public ObjectiveType type;
            [Tooltip("Palette colour this objective counts (CollectColor only).")]
            public int colorIndex;
            [Min(1)] public int amount;
        }

        [Header("Board")]
        [Min(3)] public int width = 8;
        [Min(3)] public int height = 8;
        [Range(3, 5)] public int colorCount = 5;

        [Header("Moves")]
        [Min(1)] public int movesLimit = 20;
        [Tooltip("Points banked per unused move when the level is won (cashout).")]
        [Min(0)] public int movesBonusPoints = 30;

        [Header("Objectives")]
        public ObjectiveSpec[] objectives =
        {
            new ObjectiveSpec { type = ObjectiveType.Score, colorIndex = 0, amount = 600 },
        };

        [Header("Jelly")]
        [Tooltip("Bottom rows covered in jelly (0 = none). Pair with a ClearJelly objective.")]
        [Min(0)] public int jellyRows = 0;
        [Range(1, 2)] public int jellyLayers = 1;

        [Header("Stars (ascending score thresholds)")]
        public int[] starScores = { 600, 900, 1300 };

        [Header("Scoring")]
        [Min(1)] public int pointsPerTile = 10;
        [Min(0)] public int cascadeMultiplierStep = 1;

        /// <summary>Maps the inspector specs into engine-free core objectives (never empty).</summary>
        public Objective[] ToObjectives()
        {
            if (objectives == null || objectives.Length == 0)
                return new[] { new Objective(ObjectiveType.Score, 0, Math.Max(1, starScores.FirstOrDefault())) };

            return objectives
                .Select(spec => new Objective(spec.type, spec.colorIndex, Math.Max(1, spec.amount)))
                .ToArray();
        }

        public ScoreConfig ToScoreConfig() => new ScoreConfig(pointsPerTile, cascadeMultiplierStep);
    }
}
