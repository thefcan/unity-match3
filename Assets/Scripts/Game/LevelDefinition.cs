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

        [Header("Blockers (chapter 4 mechanics)")]
        [Tooltip("Cells starting under a licorice lock (x = column, y = row from the bottom).")]
        public Vector2Int[] lockCells = Array.Empty<Vector2Int>();
        [Tooltip("Cells starting as chocolate blocks.")]
        public Vector2Int[] chocolateCells = Array.Empty<Vector2Int>();
        [Tooltip("Ingredients dispensed through refills. Pair with a CollectIngredients objective.")]
        [Min(0)] public int ingredientCount = 0;

        [Header("Blockers (chapter 5 mechanics)")]
        [Tooltip("Frosting slabs: x = column, y = row from the bottom, z = layers (1-3). Pair with a ClearFrosting objective.")]
        public Vector3Int[] frostingCells = Array.Empty<Vector3Int>();
        [Tooltip("Cells starting as licorice swirls (they fall and absorb striped beams).")]
        public Vector2Int[] swirlCells = Array.Empty<Vector2Int>();
        [Tooltip("Cells holding an indestructible chocolate fountain.")]
        public Vector2Int[] fountainCells = Array.Empty<Vector2Int>();
        [Tooltip("Cells starting as mystery eggs (colourless, mobile; hatch when hit). Pair with a HatchEggs objective.")]
        public Vector2Int[] eggCells = Array.Empty<Vector2Int>();
        [Tooltip("Bomb candies dispensed through refills (0 = none). Each must be matched before its countdown ends.")]
        [Min(0)] public int bombCount = 0;
        [Tooltip("Moves on a freshly dispensed bomb's countdown.")]
        [Min(1)] public int bombTimerMoves = 9;

        [Header("Tutorial (optional)")]
        [Tooltip("One short line shown over a dimmed board on the level's first look. Empty = no tutorial.")]
        public string tutorialText = "";
        [Tooltip("Cells highlighted through the tutorial dim (x = column, y = row from the bottom).")]
        public Vector2Int[] tutorialCells = Array.Empty<Vector2Int>();

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
