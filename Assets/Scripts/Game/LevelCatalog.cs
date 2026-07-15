using UnityEngine;

namespace Match3.Game
{
    /// <summary>
    /// The ordered campaign: level 1 is element 0. Lives in Resources so the level
    /// map (and the Game scene's fallback) can load it without scene wiring.
    /// Rebuilt by the Match3/Generate/Level Definitions menu.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelCatalog", menuName = "Match3/Level Catalog")]
    public sealed class LevelCatalog : ScriptableObject
    {
        [SerializeField] private LevelDefinition[] levels;

        public int Count => levels != null ? levels.Length : 0;

        /// <summary>The definition for a 1-based level number, or null when out of range.</summary>
        public LevelDefinition Get(int levelNumber) =>
            levels != null && levelNumber >= 1 && levelNumber <= levels.Length ? levels[levelNumber - 1] : null;

#if UNITY_EDITOR
        /// <summary>Editor-only: the level generator rebuilds the list in place.</summary>
        public void EditorSetLevels(LevelDefinition[] newLevels)
        {
            levels = newLevels;
        }
#endif
    }
}
