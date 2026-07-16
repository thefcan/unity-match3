using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.UI
{
    /// <summary>
    /// The Figma design language ("Candy Match — Game UI" file) as code: the colour
    /// variables from the Candy UI collection, the Baloo 2 / Nunito type pairing, and
    /// the generated UI-chrome sprites (rounded cards, pills, star, lock, gradient).
    /// Everything loads lazily from Resources and degrades gracefully — a missing
    /// font or sprite falls back to Unity defaults instead of breaking a screen.
    /// </summary>
    public static class UiTheme
    {
        // ---- Colours (Figma: Candy UI variable collection) ----------------------------
        public static readonly Color BgDeep = new Color(0.075f, 0.078f, 0.16f);
        public static readonly Color Card = new Color(0.137f, 0.145f, 0.28f);
        public static readonly Color Slot = new Color(0.10f, 0.105f, 0.21f);
        public static readonly Color TextPrimary = Color.white;
        public static readonly Color TextDim = new Color(0.66f, 0.67f, 0.80f);
        public static readonly Color Cta = new Color(1f, 0.35f, 0.47f);
        public static readonly Color AccentBlue = new Color(0.18f, 0.52f, 0.90f);
        public static readonly Color Gold = new Color(1f, 0.78f, 0.24f);
        public static readonly Color StarDim = new Color(0.28f, 0.28f, 0.35f);
        public static readonly Color OutlineDim = new Color(0.4f, 0.42f, 0.58f);

        /// <summary>The five candy colours (matches LevelConfig's palette).</summary>
        public static readonly Color[] CandyColors =
        {
            new Color(0.91f, 0.30f, 0.24f),
            new Color(0.18f, 0.80f, 0.44f),
            new Color(0.20f, 0.60f, 0.86f),
            new Color(0.95f, 0.77f, 0.06f),
            new Color(0.61f, 0.35f, 0.71f),
        };

        // ---- Fonts (Baloo 2 for display, Nunito for body) ------------------------------

        private static TMP_FontAsset _titleFont;
        private static TMP_FontAsset _buttonFont;
        private static TMP_FontAsset _bodyFont;

        /// <summary>Baloo 2 ExtraBold — big headings ("Candy Match", panel titles). Null if missing.</summary>
        public static TMP_FontAsset TitleFont => _titleFont ??= LoadFont("Fonts/Baloo2-ExtraBold");

        /// <summary>Baloo 2 SemiBold — buttons, counters, chips.</summary>
        public static TMP_FontAsset ButtonFont => _buttonFont ??= LoadFont("Fonts/Baloo2-SemiBold");

        /// <summary>Nunito Bold — labels and body copy.</summary>
        public static TMP_FontAsset BodyFont => _bodyFont ??= LoadFont("Fonts/Nunito-Bold");

        private static TMP_FontAsset LoadFont(string resourcePath)
        {
            var font = Resources.Load<Font>(resourcePath);
            return font != null ? TMP_FontAsset.CreateFontAsset(font) : null;
        }

        // ---- Generated UI-chrome sprites (Resources/UI) ---------------------------------

        public static Sprite Round => LoadSprite("UI/ui_round");
        public static Sprite RoundOutline => LoadSprite("UI/ui_round_outline");
        public static Sprite Pill => LoadSprite("UI/ui_pill");
        public static Sprite PillOutline => LoadSprite("UI/ui_pill_outline");
        public static Sprite PillPink => LoadSprite("UI/ui_pill_pink");
        public static Sprite CircleSprite => LoadSprite("UI/ui_circle");
        public static Sprite StarSprite => LoadSprite("UI/ui_star");
        public static Sprite LockSprite => LoadSprite("UI/ui_lock");
        public static Sprite BgGradient => LoadSprite("UI/ui_bg_gradient");

        private static Sprite LoadSprite(string path) => Resources.Load<Sprite>(path);

        // ---- Helpers --------------------------------------------------------------------

        /// <summary>Applies a theme font when available; silently keeps the default otherwise.</summary>
        public static void ApplyFont(TMP_Text text, TMP_FontAsset font)
        {
            if (text != null && font != null)
                text.font = font;
        }

        /// <summary>Sets a sliced sprite on an Image, falling back to a flat tint when the sprite is missing.</summary>
        public static void ApplySprite(Image image, Sprite sprite, Color color)
        {
            image.color = color;
            if (sprite == null)
                return;
            image.sprite = sprite;
            image.type = sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        }
    }
}
