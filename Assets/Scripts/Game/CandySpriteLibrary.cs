using System;
using Match3.Core;
using UnityEngine;

namespace Match3.Game
{
    /// <summary>
    /// Sprite lookup for every candy visual: colour index x kind -> Sprite.
    /// Generated (sprites AND this asset) by the Match3/Generate menu; BoardView
    /// falls back to Resources.Load("CandySpriteLibrary") when the field isn't
    /// wired in the scene, so no manual scene step is required.
    /// </summary>
    [CreateAssetMenu(menuName = "Match3/Candy Sprite Library")]
    public sealed class CandySpriteLibrary : ScriptableObject
    {
        [Serializable]
        public struct ColorSet
        {
            public Sprite normal;
            public Sprite stripedH;
            public Sprite stripedV;
            public Sprite wrapped;
        }

        [SerializeField] private ColorSet[] colors;
        [SerializeField] private ColorSet[] colorsColorblind;
        [SerializeField] private Sprite colorBomb;

        /// <summary>
        /// Accessibility switch (Settings → Colorblind mode): when true, For() serves
        /// the glyph-badged sprite set. Falls back to the normal set until the badged
        /// sprites have been generated, so the toggle can never blank the board.
        /// </summary>
        public static bool ColorblindMode;

        /// <summary>The sprite for a candy, or null when the library has no entry (caller falls back to tinting).</summary>
        public Sprite For(int colorIndex, TileKind kind)
        {
            if (kind == TileKind.ColorBomb)
                return colorBomb; // multi-coloured by construction — no badge needed
            if (colors == null || colorIndex < 0 || colorIndex >= colors.Length)
                return null;

            Sprite sprite = null;
            if (ColorblindMode && colorsColorblind != null && colorIndex < colorsColorblind.Length)
                sprite = FromSet(colorsColorblind[colorIndex], kind);
            if (sprite == null)
                sprite = FromSet(colors[colorIndex], kind);
            return sprite;
        }

        private static Sprite FromSet(ColorSet set, TileKind kind)
        {
            switch (kind)
            {
                case TileKind.StripedH: return set.stripedH;
                case TileKind.StripedV: return set.stripedV;
                case TileKind.Wrapped: return set.wrapped;
                default: return set.normal;
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor-only: the sprite generator rebuilds the tables in place.</summary>
        public void EditorSetSprites(ColorSet[] colorSets, ColorSet[] colorblindSets, Sprite bomb)
        {
            colors = colorSets;
            colorsColorblind = colorblindSets;
            colorBomb = bomb;
        }
#endif
    }
}
