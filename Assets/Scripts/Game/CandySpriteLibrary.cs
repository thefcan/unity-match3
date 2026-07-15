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
        [SerializeField] private Sprite colorBomb;

        /// <summary>The sprite for a candy, or null when the library has no entry (caller falls back to tinting).</summary>
        public Sprite For(int colorIndex, TileKind kind)
        {
            if (kind == TileKind.ColorBomb)
                return colorBomb;
            if (colors == null || colorIndex < 0 || colorIndex >= colors.Length)
                return null;

            ColorSet set = colors[colorIndex];
            switch (kind)
            {
                case TileKind.StripedH: return set.stripedH;
                case TileKind.StripedV: return set.stripedV;
                case TileKind.Wrapped: return set.wrapped;
                default: return set.normal;
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor-only: the sprite generator rebuilds the table in place.</summary>
        public void EditorSetSprites(ColorSet[] colorSets, Sprite bomb)
        {
            colors = colorSets;
            colorBomb = bomb;
        }
#endif
    }
}
