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
            public Sprite fish;
            public Sprite bomb;
        }

        [SerializeField] private ColorSet[] colors;
        [SerializeField] private ColorSet[] colorsColorblind;
        [SerializeField] private Sprite colorBomb;
        [SerializeField] private Sprite chocolate;
        [SerializeField] private Sprite ingredient;
        [SerializeField] private Sprite lockCage;
        [SerializeField] private Sprite frosting1;
        [SerializeField] private Sprite frosting2;
        [SerializeField] private Sprite frosting3;
        [SerializeField] private Sprite swirl;
        [SerializeField] private Sprite fountain;
        [SerializeField] private Sprite mysteryEgg;

        /// <summary>The licorice-cage overlay sprite (drawn over a locked candy). Null until generated.</summary>
        public Sprite LockCage => lockCage;

        /// <summary>
        /// The frosting slab for a remaining-layer count (1-3). The view swaps sprites
        /// as layers peel; falls back down the stack (then to null → tint fallback)
        /// until the sprite menu has been re-run.
        /// </summary>
        public Sprite FrostingSprite(int layers)
        {
            if (layers >= 3 && frosting3 != null) return frosting3;
            if (layers >= 2 && frosting2 != null) return frosting2;
            return frosting1;
        }

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
            if (kind == TileKind.Chocolate)
                return chocolate;
            if (kind == TileKind.Ingredient)
                return ingredient;
            if (kind == TileKind.Frosting)
                return FrostingSprite(3); // generic entry — the view rebinds per layer
            if (kind == TileKind.Swirl)
                return swirl;
            if (kind == TileKind.ChocolateFountain)
                return fountain;
            if (kind == TileKind.MysteryEgg)
                return mysteryEgg;
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
                // Until the sprite menu is re-run the new kinds fall back to the plain
                // candy of their colour — the board never blanks.
                case TileKind.Fish: return set.fish != null ? set.fish : set.normal;
                case TileKind.Bomb: return set.bomb != null ? set.bomb : set.normal;
                default: return set.normal;
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor-only: the sprite generator rebuilds the tables in place.</summary>
        public void EditorSetSprites(ColorSet[] colorSets, ColorSet[] colorblindSets, Sprite bomb,
                                     Sprite chocolateSprite, Sprite ingredientSprite, Sprite lockCageSprite)
        {
            colors = colorSets;
            colorsColorblind = colorblindSets;
            colorBomb = bomb;
            chocolate = chocolateSprite;
            ingredient = ingredientSprite;
            lockCage = lockCageSprite;
        }

        /// <summary>Editor-only: the chapter-5 blocker sprites (frosting stack, swirl, fountain).</summary>
        public void EditorSetBlockerSprites(Sprite frostingOne, Sprite frostingTwo, Sprite frostingThree,
                                            Sprite swirlSprite, Sprite fountainSprite)
        {
            frosting1 = frostingOne;
            frosting2 = frostingTwo;
            frosting3 = frostingThree;
            swirl = swirlSprite;
            fountain = fountainSprite;
        }

        /// <summary>Editor-only: chapter 6's mystery-egg shell.</summary>
        public void EditorSetEggSprite(Sprite eggSprite)
        {
            mysteryEgg = eggSprite;
        }
#endif
    }
}
