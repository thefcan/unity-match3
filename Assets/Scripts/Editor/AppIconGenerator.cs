using System.Collections.Generic;
using System.IO;
using Match3.Core;
using Match3.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// Draws the launcher icon, the Android adaptive layers and the Play feature
    /// graphic from <see cref="IconArtist"/>, then assigns them in PlayerSettings.
    ///
    /// Everything else in this project is generated; the icon was the one thing that
    /// wasn't — and an unset icon is not neutral, it ships the stock Unity logo as the
    /// app's face. Colours come from the campaign itself (<see cref="ThemeCurve"/>'s
    /// level-1 palette and the level config's candy colours), so a palette edit
    /// repaints the icon too.
    ///
    ///   Match3 > Generate > App Icons
    ///
    /// The store feature graphic is written but never assigned — Play wants it
    /// uploaded by hand, so it just sits in Assets/Icons ready to drag.
    /// </summary>
    public static class AppIconGenerator
    {
        private const string IconFolder = "Assets/Icons";
        private const string ConfigPath = "Assets/ScriptableObjects/Level1.asset";

        // Android wants 432x432 adaptive layers; 512 is the store icon size and a fine
        // master for every legacy slot (Unity downscales per density).
        private const int IconSize = 512;
        private const int AdaptiveSize = 432;
        private const int FeatureWidth = 1024;
        private const int FeatureHeight = 500;

        [MenuItem("Match3/Generate/App Icons")]
        public static void Generate()
        {
            Directory.CreateDirectory(IconFolder);

            ThemeParameters theme = ThemeCurve.For(1); // the campaign's opening palette
            var top = new CandyArtist.Rgb(theme.BgTop.R, theme.BgTop.G, theme.BgTop.B);
            var bottom = new CandyArtist.Rgb(theme.BgBottom.R, theme.BgBottom.G, theme.BgBottom.B);
            CandyArtist.Rgb[] palette = LoadPalette();

            Texture2D icon = Write("app_icon", IconSize, IconSize,
                                   IconArtist.RenderIcon(IconSize, top, bottom, palette));
            Texture2D background = Write("app_icon_adaptive_background", AdaptiveSize, AdaptiveSize,
                                         IconArtist.RenderAdaptiveBackground(AdaptiveSize, top, bottom));
            Texture2D foreground = Write("app_icon_adaptive_foreground", AdaptiveSize, AdaptiveSize,
                                         IconArtist.RenderAdaptiveForeground(AdaptiveSize, palette));
            Write("store_feature_graphic", FeatureWidth, FeatureHeight,
                  IconArtist.RenderFeatureGraphic(FeatureWidth, FeatureHeight, top, bottom, palette));

            var report = new System.Text.StringBuilder("App icons generated in " + IconFolder + ":\n");
            AssignPlatformIcons(BuildTargetGroup.Android, icon, background, foreground, report);
            AssignStandalone(icon, report);

            AssetDatabase.SaveAssets();
            EditorApplication.ExecuteMenuItem("File/Save Project"); // PlayerSettings lives outside the asset db
            Debug.Log(report.ToString());
        }

        /// <summary>
        /// Assigns every icon slot the platform reports. Adaptive slots take two layers
        /// (background first, foreground second); everything else takes the square.
        /// </summary>
        private static void AssignPlatformIcons(BuildTargetGroup group, Texture2D square,
                                                Texture2D background, Texture2D foreground,
                                                System.Text.StringBuilder report)
        {
            // GetSupportedIconKindsForPlatform still speaks BuildTargetGroup in 2022.3
            // while the get/set pair has moved on to NamedBuildTarget.
            NamedBuildTarget target = NamedBuildTarget.FromBuildTargetGroup(group);

            PlatformIconKind[] kinds;
            try
            {
                kinds = PlayerSettings.GetSupportedIconKindsForPlatform(group);
            }
            catch (System.Exception e)
            {
                // No module for this platform installed — the PNGs are still on disk and
                // a later run with the module present will wire them up.
                report.AppendLine($"- {target.TargetName}: icon slots unavailable ({e.GetType().Name}); PNGs written only");
                return;
            }

            if (kinds == null || kinds.Length == 0)
            {
                report.AppendLine($"- {target.TargetName}: no icon slots reported (build support module not installed?)");
                return;
            }

            foreach (PlatformIconKind kind in kinds)
            {
                PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(target, kind);
                foreach (PlatformIcon icon in icons)
                {
                    icon.SetTextures(icon.maxLayerCount >= 2
                        ? new[] { background, foreground }
                        : new[] { square });
                }
                PlayerSettings.SetPlatformIcons(target, kind, icons);
                report.AppendLine($"- {target.TargetName}/{kind}: {icons.Length} slot");
            }
        }

        /// <summary>
        /// Standalone still uses the old sized-array API: the array has to be exactly as
        /// long as the platform's slot list, or the assignment is silently dropped (one
        /// texture for six slots writes nothing at all). Same master in every slot —
        /// Unity downscales.
        /// </summary>
        private static void AssignStandalone(Texture2D icon, System.Text.StringBuilder report)
        {
            int[] sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Any);
            var textures = new Texture2D[sizes.Length];
            for (int i = 0; i < textures.Length; i++)
                textures[i] = icon;

            PlayerSettings.SetIcons(NamedBuildTarget.Standalone, textures, IconKind.Any);
            report.AppendLine($"- Standalone: app_icon in {textures.Length} slot");
        }

        // ---- Asset plumbing ---------------------------------------------------------------

        /// <summary>
        /// Writes one top-down RGBA buffer as a PNG and returns the imported texture.
        /// Icon textures must stay uncompressed and un-atlased: PlayerSettings reads
        /// their pixels at build time.
        /// </summary>
        private static Texture2D Write(string name, int width, int height, byte[] topDownRgba)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var flipped = new Color32[width * height]; // IconArtist is top-down, Texture2D is bottom-up
            for (int row = 0; row < height; row++)
            {
                int src = row * width * 4;
                int dst = (height - 1 - row) * width;
                for (int col = 0; col < width; col++, src += 4)
                    flipped[dst + col] = new Color32(topDownRgba[src], topDownRgba[src + 1],
                                                     topDownRgba[src + 2], topDownRgba[src + 3]);
            }
            texture.SetPixels32(flipped);
            texture.Apply();

            string path = $"{IconFolder}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = Mathf.Max(width, height) <= 1024 ? 1024 : 2048;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>The live candy palette, so the icon can never drift from the board.</summary>
        private static CandyArtist.Rgb[] LoadPalette()
        {
            var config = AssetDatabase.LoadAssetAtPath<LevelConfig>(ConfigPath);
            if (config != null && config.tileColors != null && config.tileColors.Length > 0)
            {
                var palette = new List<CandyArtist.Rgb>(config.tileColors.Length);
                foreach (Color color in config.tileColors)
                    palette.Add(new CandyArtist.Rgb(color.r, color.g, color.b));
                return palette.ToArray();
            }

            return new[]
            {
                new CandyArtist.Rgb(0.91f, 0.30f, 0.24f),
                new CandyArtist.Rgb(0.18f, 0.80f, 0.44f),
                new CandyArtist.Rgb(0.20f, 0.60f, 0.86f),
                new CandyArtist.Rgb(0.95f, 0.77f, 0.06f),
                new CandyArtist.Rgb(0.61f, 0.35f, 0.71f),
            };
        }
    }
}
