using System.IO;
using Match3.Core;
using Match3.Game;
using UnityEditor;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// Regenerates every candy sprite (5 colours x normal/stripedH/stripedV/wrapped
    /// + the colour bomb = 21 PNGs) from <see cref="CandyArtist"/> and rebuilds the
    /// CandySpriteLibrary asset in Resources. Safe to re-run any time — files are
    /// overwritten in place, GUIDs (and thus references) survive.
    /// </summary>
    public static class CandySpriteGenerator
    {
        private const int Size = 256;
        private const string SpriteFolder = "Assets/Sprites/Candies";
        private const string LibraryPath = "Assets/Resources/CandySpriteLibrary.asset";
        private const string ConfigPath = "Assets/ScriptableObjects/Level1.asset";

        [MenuItem("Match3/Generate/Candy Sprites")]
        public static void Generate()
        {
            CandyArtist.Rgb[] palette = LoadPalette();
            Directory.CreateDirectory(SpriteFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));

            var colorSets = new CandySpriteLibrary.ColorSet[palette.Length];
            var colorblindSets = new CandySpriteLibrary.ColorSet[palette.Length];
            for (int color = 0; color < palette.Length; color++)
            {
                colorSets[color] = new CandySpriteLibrary.ColorSet
                {
                    normal = WriteSprite($"candy{color}_normal", CandyArtist.Render(Size, palette[color], color, TileKind.Normal)),
                    stripedH = WriteSprite($"candy{color}_stripedH", CandyArtist.Render(Size, palette[color], color, TileKind.StripedH)),
                    stripedV = WriteSprite($"candy{color}_stripedV", CandyArtist.Render(Size, palette[color], color, TileKind.StripedV)),
                    wrapped = WriteSprite($"candy{color}_wrapped", CandyArtist.Render(Size, palette[color], color, TileKind.Wrapped)),
                };
                // Accessibility set: same candies with a per-colour glyph badge.
                colorblindSets[color] = new CandySpriteLibrary.ColorSet
                {
                    normal = WriteSprite($"candy{color}_normal_cb", CandyArtist.RenderColorblind(Size, palette[color], color, TileKind.Normal)),
                    stripedH = WriteSprite($"candy{color}_stripedH_cb", CandyArtist.RenderColorblind(Size, palette[color], color, TileKind.StripedH)),
                    stripedV = WriteSprite($"candy{color}_stripedV_cb", CandyArtist.RenderColorblind(Size, palette[color], color, TileKind.StripedV)),
                    wrapped = WriteSprite($"candy{color}_wrapped_cb", CandyArtist.RenderColorblind(Size, palette[color], color, TileKind.Wrapped)),
                };
            }
            Sprite bomb = WriteSprite("candy_bomb", CandyArtist.RenderColorBomb(Size, palette));
            Sprite chocolate = WriteSprite("candy_chocolate", CandyArtist.RenderChocolate(Size));
            Sprite ingredient = WriteSprite("candy_ingredient", CandyArtist.RenderIngredient(Size));
            Sprite lockCage = WriteSprite("candy_lock_cage", CandyArtist.RenderLockCage(Size));

            var library = AssetDatabase.LoadAssetAtPath<CandySpriteLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<CandySpriteLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            library.EditorSetSprites(colorSets, colorblindSets, bomb, chocolate, ingredient, lockCage);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            Debug.Log($"CandySpriteGenerator: wrote {palette.Length * 8 + 4} sprites (colorblind set + blockers) and refreshed {LibraryPath}.");
        }

        private static CandyArtist.Rgb[] LoadPalette()
        {
            var config = AssetDatabase.LoadAssetAtPath<LevelConfig>(ConfigPath);
            if (config != null && config.tileColors != null && config.tileColors.Length > 0)
            {
                var palette = new CandyArtist.Rgb[config.tileColors.Length];
                for (int i = 0; i < palette.Length; i++)
                {
                    Color c = config.tileColors[i];
                    palette[i] = new CandyArtist.Rgb(c.r, c.g, c.b);
                }
                return palette;
            }

            // Fallback: the palette Level1.asset ships with.
            return new[]
            {
                new CandyArtist.Rgb(0.91f, 0.30f, 0.24f),
                new CandyArtist.Rgb(0.18f, 0.80f, 0.44f),
                new CandyArtist.Rgb(0.20f, 0.60f, 0.86f),
                new CandyArtist.Rgb(0.95f, 0.77f, 0.06f),
                new CandyArtist.Rgb(0.61f, 0.35f, 0.71f),
            };
        }

        /// <summary>Writes one PNG, imports it as a single 256-PPU sprite, and returns the Sprite asset.</summary>
        private static Sprite WriteSprite(string name, byte[] topDownRgba)
        {
            // CandyArtist emits rows top-down; Texture2D wants bottom-up.
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var flipped = new Color32[Size * Size];
            for (int row = 0; row < Size; row++)
            {
                int src = row * Size * 4;
                int dst = (Size - 1 - row) * Size;
                for (int col = 0; col < Size; col++, src += 4)
                    flipped[dst + col] = new Color32(topDownRgba[src], topDownRgba[src + 1], topDownRgba[src + 2], topDownRgba[src + 3]);
            }
            texture.SetPixels32(flipped);

            string path = $"{SpriteFolder}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = Size;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
