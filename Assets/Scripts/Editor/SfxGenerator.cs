using System.IO;
using Match3.Core;
using UnityEditor;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// Regenerates every synthesized sound effect into Resources/Audio (where
    /// AudioManager loads them by name). Safe to re-run; files are overwritten
    /// in place and GUIDs survive.
    /// </summary>
    public static class SfxGenerator
    {
        private const string AudioFolder = "Assets/Resources/Audio";

        [MenuItem("Match3/Generate/Sound Effects")]
        public static void Generate()
        {
            Directory.CreateDirectory(AudioFolder);

            Write("swap", SfxSynth.Swap());
            Write("pop", SfxSynth.Pop());
            Write("special_create", SfxSynth.SpecialCreate());
            Write("line_clear", SfxSynth.LineClear());
            Write("wrapped_blast", SfxSynth.WrappedBlast());
            Write("color_bomb", SfxSynth.ColorBomb());
            Write("shuffle", SfxSynth.Shuffle());
            Write("win", SfxSynth.Win());
            Write("lose", SfxSynth.Lose());
            Write("button", SfxSynth.Button());

            AssetDatabase.Refresh();
            Debug.Log($"SfxGenerator: wrote 10 clips to {AudioFolder}.");
        }

        private static void Write(string name, float[] samples)
        {
            File.WriteAllBytes($"{AudioFolder}/{name}.wav", SfxSynth.ToWav(samples));
        }
    }
}
