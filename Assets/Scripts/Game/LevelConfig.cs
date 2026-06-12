using Match3.Core;
using UnityEngine;

namespace Match3.Game
{
    /// <summary>
    /// SCRIPTABLEOBJECT PATTERN: level rules as a data ASSET instead of hard-coded
    /// constants or scene values. Designers can create "Level 2", "Hard Mode" etc.
    /// from the Assets menu and tweak numbers without touching code — and the same
    /// asset can be shared by many scenes. (Closest Java analogy: an immutable config
    /// bean loaded from a file, but editable in the Unity Inspector.)
    ///
    /// Create via: Assets > Create > Match3 > Level Config.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelConfig", menuName = "Match3/Level Config")]
    public sealed class LevelConfig : ScriptableObject
    {
        [Header("Board")]
        [Min(3)] public int width = 8;
        [Min(3)] public int height = 8;

        [Header("Rules")]
        [Min(1)] public int moveLimit = 20;
        [Min(1)] public int targetScore = 2000;

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

        /// <summary>Maps the inspector values into the engine-free core type.</summary>
        public ScoreConfig ToScoreConfig() => new ScoreConfig(pointsPerTile, cascadeMultiplierStep);
    }
}
