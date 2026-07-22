using System.IO;
using Match3.Core;
using UnityEditor;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// Renders one MusicComposer loop per campaign chapter into
    /// Resources/Audio/Music and applies streaming/Vorbis import settings.
    /// The chapter count follows LevelCurve, so extending the campaign and
    /// re-running the menu adds the new chapter's loop automatically.
    /// (Offline twin: scratchpad musicgen — same composer, same bytes.)
    /// </summary>
    public static class MusicGenerator
    {
        private const string Folder = "Assets/Resources/Audio/Music";

        [MenuItem("Match3/Generate/Music")]
        public static void Generate()
        {
            int chapters = Mathf.Max(1, LevelCurve.LevelCount / ThemeCurve.ChapterLength);
            Directory.CreateDirectory(Folder);

            for (int chapter = 0; chapter < chapters; chapter++)
            {
                string path = $"{Folder}/chapter{chapter}.wav";
                File.WriteAllBytes(path, MusicComposer.ComposeWav(chapter));
                AssetDatabase.ImportAsset(path);

                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.Streaming; // ~23s loops — never DecompressOnLoad
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.35f;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = false;
                importer.loadInBackground = true;
                importer.SaveAndReimport();
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"MusicGenerator: wrote {chapters} chapter loops to {Folder}.");
        }
    }
}
