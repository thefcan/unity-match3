using System.Collections.Generic;
using System.IO;
using System.Linq;
using Match3.Core;
using Match3.Game;
using UnityEditor;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// Authors the whole campaign from <see cref="LevelCurve"/>: Level_01..Level_20
    /// under Resources/Levels plus the LevelCatalog asset. Re-running updates assets
    /// in place (GUIDs and references survive).
    /// </summary>
    public static class LevelDefinitionGenerator
    {
        private const string LevelFolder = "Assets/Resources/Levels";
        private const string CatalogPath = "Assets/Resources/LevelCatalog.asset";

        [MenuItem("Match3/Generate/Level Definitions")]
        public static void Generate()
        {
            Directory.CreateDirectory(LevelFolder);

            var levels = new List<LevelDefinition>();
            for (int number = 1; number <= LevelCurve.LevelCount; number++)
            {
                LevelParameters parameters = LevelCurve.For(number);
                string path = $"{LevelFolder}/Level_{number:00}.asset";

                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                if (level == null)
                {
                    level = ScriptableObject.CreateInstance<LevelDefinition>();
                    AssetDatabase.CreateAsset(level, path);
                }

                level.width = parameters.Width;
                level.height = parameters.Height;
                level.colorCount = parameters.ColorCount;
                level.movesLimit = parameters.MovesLimit;
                level.movesBonusPoints = parameters.MovesBonusPoints;
                level.objectives = parameters.Objectives
                    .Select(objective => new LevelDefinition.ObjectiveSpec
                    {
                        type = objective.Type,
                        colorIndex = objective.ColorIndex,
                        amount = objective.TargetAmount,
                    })
                    .ToArray();
                level.starScores = parameters.StarScores.ToArray();
                EditorUtility.SetDirty(level);

                levels.Add(level);
            }

            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<LevelCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.EditorSetLevels(levels.ToArray());
            EditorUtility.SetDirty(catalog);

            AssetDatabase.SaveAssets();
            Debug.Log($"LevelDefinitionGenerator: authored {levels.Count} levels and refreshed {CatalogPath}.");
        }
    }
}
