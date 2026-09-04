using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.U2D;

namespace Match3.EditorTools
{
    /// <summary>
    /// One-click mobile hardening. EVERY ProjectSettings / URP / importer mutation
    /// lives here — never hand-edit those YAML files while the editor is open, it
    /// rewrites them from memory and silently discards external changes.
    ///
    ///   Match3 > Setup > Apply Mobile Settings   — player/quality/URP/importer flags
    ///   Match3 > Generate > Sprite Atlas          — packs the candy sprites
    ///   Match3 > Generate > Font Assets           — pre-baked TMP SDF fonts
    /// </summary>
    public static class MobileSetupMenu
    {
        /// <summary>
        /// The Android package name. PERMANENT once a build is published — Play keys
        /// every listing, review and install off it, and it can never be changed for
        /// that app. Change it HERE before the first upload, not after.
        /// </summary>
        private const string AndroidApplicationId = "com.thefcan.candymatch";

        /// <summary>
        /// Marketing version (what the store shows). versionCode is derived from it
        /// below, so bumping this one line is the whole release ritual.
        /// </summary>
        private const string BundleVersion = "1.0.0";

        /// <summary>
        /// Play REJECTS an upload whose versionCode it has seen before, so this must
        /// rise monotonically and can never be reused — even for a build that was later
        /// discarded. Derived as major*10000 + minor*100 + patch, which keeps it
        /// readable (1.2.3 -> 10203) and leaves room for 99 patches per minor.
        /// </summary>
        private static int VersionCodeFor(string version)
        {
            string[] parts = version.Split('.');
            int major = parts.Length > 0 && int.TryParse(parts[0], out int m) ? m : 0;
            int minor = parts.Length > 1 && int.TryParse(parts[1], out int n) ? n : 0;
            int patch = parts.Length > 2 && int.TryParse(parts[2], out int p) ? p : 0;
            return major * 10000 + minor * 100 + patch;
        }

        [MenuItem("Match3/Setup/Apply Mobile Settings")]
        public static void ApplyMobileSettings()
        {
            var report = new StringBuilder("Mobile settings applied:\n");
            ApplyPlayerSettings(report);
            ApplyQualitySettings(report);
            ApplyUrpSettings(report);
            ApplyTextureSettings(report);
            ApplyAudioSettings(report);

            AssetDatabase.SaveAssets();
            // ProjectSettings/QualitySettings live outside the asset database — a full
            // project save is what actually flushes them to disk.
            EditorApplication.ExecuteMenuItem("File/Save Project");
            Debug.Log(report.ToString());
        }

        private static void ApplyPlayerSettings(StringBuilder report)
        {
            // The whole UI (canvas scalers, CameraFitter, HUD layout) is portrait-only;
            // free rotation just breaks the framing.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;

            // The HUD now lives in a SafeAreaFitter container; stop drawing under notches.
            PlayerSettings.Android.renderOutsideSafeArea = false;

            ApplyReleaseIdentity(report);

            report.AppendLine("- Player: portrait kilidi, IL2CPP + ARM64/ARMv7, renderOutsideSafeArea=false");
        }

        /// <summary>
        /// Who the app IS, as far as the store is concerned. Without this the package
        /// name falls out of companyName + productName at build time
        /// (com.DefaultCompany.unity-match3), which is both unclaimable on Play and
        /// impossible to change once uploaded.
        ///
        /// productName is deliberately NOT touched here: on desktop it also decides
        /// persistentDataPath, so renaming it silently orphans every local save. That
        /// rename is a documented, deliberate step in docs/RELEASE.md, not a side
        /// effect of running this menu.
        /// </summary>
        private static void ApplyReleaseIdentity(StringBuilder report)
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidApplicationId);
            PlayerSettings.bundleVersion = BundleVersion;
            PlayerSettings.Android.bundleVersionCode = VersionCodeFor(BundleVersion);

            // "Automatic (highest installed)" makes the shipped targetSdk a property of
            // whichever SDK the build machine happens to have — the manifest changes
            // under you. Pin it: Play's current floor for new apps is 34.
            // Cast rather than the enum member: AndroidApiLevel34 only exists in newer
            // 2022.3 patches, and the value is what actually lands in the manifest.
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)34;
            // minSdk is left where the project set it (22): raising it drops real
            // devices for nothing this game needs.

            report.AppendLine($"- Identity: {AndroidApplicationId} v{BundleVersion} " +
                              $"(versionCode {VersionCodeFor(BundleVersion)}), targetSdk 34 pinned, " +
                              $"minSdk {(int)PlayerSettings.Android.minSdkVersion} unchanged");
        }

        private static void ApplyQualitySettings(StringBuilder report)
        {
            // GameBoot caps the frame rate via Application.targetFrameRate; vSync must
            // be off on every tier or Android ignores the cap and chases refresh rate.
            int original = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, applyExpensiveChanges: false);
                QualitySettings.vSyncCount = 0;
            }
            QualitySettings.SetQualityLevel(original, applyExpensiveChanges: false);
            report.AppendLine($"- Quality: {QualitySettings.names.Length} tier'da vSync=0");
        }

        private static void ApplyUrpSettings(StringBuilder report)
        {
            // Flat 2D game: HDR targets and shadow maps are pure bandwidth/memory waste.
            // SerializedObject keeps this compilable without a URP assembly reference.
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null)
                    continue;

                var so = new SerializedObject(asset);
                SetBool(so, "m_SupportsHDR", false);
                SetBool(so, "m_MainLightShadowsSupported", false);
                SetBool(so, "m_AdditionalLightShadowsSupported", false);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                report.AppendLine($"- URP: HDR + gölgeler kapalı → {path}");
            }
        }

        private static void SetBool(SerializedObject so, string propertyPath, bool value)
        {
            SerializedProperty property = so.FindProperty(propertyPath);
            if (property != null)
                property.boolValue = value;
        }

        private static void ApplyTextureSettings(StringBuilder report)
        {
            // Candies feed the sprite atlas, so their SOURCES must stay uncompressed:
            // the atlas applies its own ASTC 6x6 (GenerateSpriteAtlas), and compressing
            // the source too would both pack from lossy pixels and log a "Source
            // Texture ... is using compressed format" warning on every pack. The
            // originals are stripped from builds once packed, so this costs nothing.
            int candyCount = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Sprites/Candies" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                    continue;

                TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
                if (!android.overridden && importer.textureCompression == TextureImporterCompression.Uncompressed)
                    continue;

                android.overridden = false;
                importer.SetPlatformTextureSettings(android);
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                candyCount++;
            }

            // Resources/UI sprites are NOT atlased (atlasing Resources duplicates
            // texture memory) — they ship standalone, so they carry the ASTC override.
            int uiCount = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/UI" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                    continue;

                TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
                bool dirty = !android.overridden
                             || android.format != TextureImporterFormat.ASTC_6x6
                             || android.maxTextureSize > 1024;
                if (!dirty)
                    continue;

                android.overridden = true;
                android.format = TextureImporterFormat.ASTC_6x6;
                android.maxTextureSize = Mathf.Min(importer.maxTextureSize, 1024);
                importer.SetPlatformTextureSettings(android);
                importer.SaveAndReimport();
                uiCount++;
            }

            report.AppendLine($"- Texture: {uiCount} UI sprite ASTC 6x6; {candyCount} atlas kaynağı uncompressed");
        }

        private static void ApplyAudioSettings(StringBuilder report)
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Resources/Audio" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Music/"))
                    continue; // music loops stay stereo/streaming (generated in Faz C)
                if (AssetImporter.GetAtPath(path) is not AudioImporter importer || importer.forceToMono)
                    continue;

                importer.forceToMono = true; // synthesized SFX are mono content in stereo files
                importer.SaveAndReimport();
                count++;
            }
            report.AppendLine($"- Audio: {count} SFX forceToMono");
        }

        // ---- Sprite atlas ---------------------------------------------------------------

        [MenuItem("Match3/Generate/Sprite Atlas")]
        public static void GenerateSpriteAtlas()
        {
            // 21 separate candy textures break URP's batching on every board redraw;
            // one atlas collapses the board to a couple of draw calls.
            EditorSettings.spritePackerMode = SpritePackerMode.AlwaysOnAtlas;

            const string atlasPath = "Assets/Sprites/CandyAtlas.spriteatlas";
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            bool fresh = atlas == null;
            if (fresh)
                atlas = new SpriteAtlas();

            atlas.SetPackingSettings(new SpriteAtlasPackingSettings
            {
                padding = 4,
                enableRotation = false,
                enableTightPacking = false,
            });
            atlas.SetTextureSettings(new SpriteAtlasTextureSettings
            {
                generateMipMaps = false,
                filterMode = FilterMode.Bilinear,
                sRGB = true,
            });
            atlas.SetPlatformSettings(new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                format = TextureImporterFormat.ASTC_6x6,
                maxTextureSize = 2048,
            });

            if (fresh)
            {
                // The whole candies folder — future kinds (lock, chocolate, ingredient
                // sprites) join the atlas automatically. Resources/UI is deliberately
                // NOT atlased: atlasing Resources sprites duplicates the texture memory.
                var folder = AssetDatabase.LoadAssetAtPath<Object>("Assets/Sprites/Candies");
                atlas.Add(new[] { folder });
                AssetDatabase.CreateAsset(atlas, atlasPath);
            }

            SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget);
            AssetDatabase.SaveAssets();
            Debug.Log($"Sprite atlas packed: {atlasPath}");
        }

        // ---- Pre-baked TMP fonts ----------------------------------------------------------

        private static readonly string[] FontNames = { "Baloo2-SemiBold", "Baloo2-ExtraBold", "Nunito-Bold" };

        [MenuItem("Match3/Generate/Font Assets")]
        public static void GenerateFontAssets()
        {
            string charset = BuildCharset();
            foreach (string fontName in FontNames)
                BakeFont(fontName, charset);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("TMP font assets baked to Resources/Fonts/* SDF (UiTheme picks them up automatically).");
        }

        private static void BakeFont(string fontName, string charset)
        {
            string ttfPath = $"Assets/Resources/Fonts/{fontName}.ttf";
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null)
            {
                Debug.LogWarning($"Font bake skipped, TTF missing: {ttfPath}");
                return;
            }

            string assetPath = $"Assets/Resources/Fonts/{fontName} SDF.asset";
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null)
                AssetDatabase.DeleteAsset(assetPath); // regenerate from scratch; loaded by name, not GUID

            var fontAsset = TMP_FontAsset.CreateFontAsset(
                font, 80, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
            fontAsset.name = fontName + " SDF";
            fontAsset.TryAddCharacters(charset, out string missing);
            if (!string.IsNullOrEmpty(missing))
                Debug.LogWarning($"{fontName}: glyphs missing from the TTF: {missing}");

            // Freeze the atlas: static population = the baked texture ships as-is,
            // no runtime rasterization, no growing atlas memory.
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

            AssetDatabase.CreateAsset(fontAsset, assetPath);
            if (fontAsset.material != null)
            {
                fontAsset.material.name = fontName + " SDF Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }
            if (fontAsset.atlasTexture != null)
            {
                fontAsset.atlasTexture.name = fontName + " SDF Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
            }
            EditorUtility.SetDirty(fontAsset);
        }

        private static string BuildCharset()
        {
            var sb = new StringBuilder();
            for (char c = ' '; c <= '~'; c++)
                sb.Append(c); // full printable ASCII
            sb.Append("çğıöşüÇĞİÖŞÜ"); // Turkish
            sb.Append("★☆×—–’‘“”…∞");  // typographic extras used by the UI (∞ = Relaxed mode)
            return sb.ToString();
        }
    }
}
