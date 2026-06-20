using System;
using Match3.Core;
using UnityEngine;

namespace Match3.Game
{
    /// <summary>
    /// SCRIPTABLEOBJECT PATTERN: level rules as a data ASSET instead of hard-coded
    /// constants or scene values. Designers can tune the time limit, target and bonus
    /// from the Inspector without touching code. (Closest Java analogy: an immutable
    /// config bean loaded from a file, but editable in the Unity Inspector.)
    ///
    /// This is a TIME-ATTACK config: you get <see cref="timeLimit"/> seconds to reach
    /// the level's target score; clearing a level raises the target and resets the
    /// clock, so play loops endlessly until the timer runs out.
    ///
    /// Create via: Assets > Create > Match3 > Level Config.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelConfig", menuName = "Match3/Level Config")]
    public sealed class LevelConfig : ScriptableObject
    {
        [Header("Board")]
        [Min(3)] public int width = 8;
        [Min(3)] public int height = 8;

        [Header("Time attack")]
        [Tooltip("Seconds on the clock at the start of every level.")]
        [Min(1f)] public float timeLimit = 45f;
        [Tooltip("Points needed to clear level 1.")]
        [Min(1)] public int targetScore = 120;
        [Tooltip("How much the target grows each level (level 2 = targetScore + this, etc.).")]
        [Min(0)] public int targetScoreIncrementPerLevel = 40;
        [Tooltip("Seconds the 'Level Complete!' celebration holds before the next level begins.")]
        [Min(0f)] public float levelCompletePauseSeconds = 2f;
        [Tooltip("Seconds of inactivity before a hint pulses a still-available move.")]
        [Min(1f)] public float hintDelaySeconds = 4f;

        [Header("Bonus time")]
        [Tooltip("A single straight match of this many tiles (or more) is a 'big match'.")]
        [Min(3)] public int bonusMatchSize = 4;
        [Tooltip("Seconds added to the clock for each big match.")]
        [Min(0f)] public float bonusSeconds = 5f;

        [Header("Scoring")]
        [Min(1)] public int pointsPerTile = 10;
        [Min(0)] public int cascadeMultiplierStep = 1;

        [Header("Tiles")]
        [Tooltip("One entry per tile colour. The core only knows colour INDICES; this palette is purely visual.")]
        public Color[] tileColors =
        {
            new Color(0.91f, 0.30f, 0.24f), // red
            new Color(0.18f, 0.80f, 0.44f), // green
            new Color(0.20f, 0.60f, 0.86f), // blue
            new Color(0.95f, 0.77f, 0.06f), // yellow
            new Color(0.61f, 0.35f, 0.71f), // purple
        };

        public int ColorCount => tileColors.Length;

        /// <summary>The score needed to clear a given 1-based level.</summary>
        public int TargetScoreForLevel(int level) =>
            targetScore + Math.Max(0, level - 1) * targetScoreIncrementPerLevel;

        /// <summary>Maps the inspector values into the engine-free core type.</summary>
        public ScoreConfig ToScoreConfig() => new ScoreConfig(pointsPerTile, cascadeMultiplierStep);
    }
}
