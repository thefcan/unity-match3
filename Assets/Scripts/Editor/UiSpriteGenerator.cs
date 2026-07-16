using System.IO;
using Match3.Core;
using UnityEditor;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// Regenerates the UI-chrome sprites (rounded cards, pills, star, lock, background
    /// gradient) from <see cref="UiArtist"/> into Resources/UI, where UiTheme loads
    /// them by name. Overwrites in place; import settings (9-slice borders) survive.
    /// </summary>
    public static class UiSpriteGenerator
    {
        private const string UiFolder = "Assets/Resources/UI";

        [MenuItem("Match3/Generate/UI Sprites")]
        public static void Generate()
        {
            Directory.CreateDirectory(UiFolder);

            Write("ui_round", 128, 128, UiArtist.RoundedRect(128, 48f), 48);
            Write("ui_round_outline", 128, 128, UiArtist.RoundedRectOutline(128, 48f, 6f), 48);
            Write("ui_pill", 128, 128, UiArtist.RoundedRect(128, 63f), 63);
            Write("ui_pill_outline", 128, 128, UiArtist.RoundedRectOutline(128, 63f, 5f), 63);
            Write("ui_pill_pink", 256, 256, UiArtist.PillGradient(256, 127f), 127);
            Write("ui_circle", 128, 128, UiArtist.Circle(128), 0);
            Write("ui_star", 128, 128, UiArtist.Star(128), 0);
            Write("ui_lock", 96, 96, UiArtist.Lock(96), 0);
            Write("ui_bg_gradient", 16, 512, UiArtist.BackgroundGradient(16, 512), 0);

            AssetDatabase.Refresh();
            Debug.Log($"UiSpriteGenerator: wrote 9 sprites to {UiFolder}.");
        }

        private static void Write(string name, int width, int height, byte[] topDownRgba, int border)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var flipped = new Color32[width * height];
            for (int row = 0; row < height; row++)
            {
                int src = row * width * 4;
                int dst = (height - 1 - row) * width;
                for (int col = 0; col < width; col++, src += 4)
                    flipped[dst + col] = new Color32(topDownRgba[src], topDownRgba[src + 1], topDownRgba[src + 2], topDownRgba[src + 3]);
            }
            texture.SetPixels32(flipped);

            string path = $"{UiFolder}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.SaveAndReimport();
        }
    }
}
