using System;

namespace Match3.Core
{
    /// <summary>An engine-free RGB colour (0-1 range) for theme math in the core.</summary>
    public readonly struct ThemeColor
    {
        public float R { get; }
        public float G { get; }
        public float B { get; }

        public ThemeColor(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }

        public static ThemeColor Lerp(ThemeColor a, ThemeColor b, float t) =>
            new ThemeColor(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
    }

    /// <summary>The ambience of one level: background gradient ends and card tints.</summary>
    public readonly struct ThemeParameters
    {
        public ThemeColor BgTop { get; }
        public ThemeColor BgBottom { get; }
        public ThemeColor Card { get; }
        public ThemeColor Slot { get; }

        /// <summary>0-based chapter index (levels 1-20 = 0, 21-40 = 1, ...).</summary>
        public int Chapter { get; }

        public ThemeParameters(ThemeColor bgTop, ThemeColor bgBottom, ThemeColor card, ThemeColor slot, int chapter)
        {
            BgTop = bgTop;
            BgBottom = bgBottom;
            Card = card;
            Slot = slot;
            Chapter = chapter;
        }
    }

    /// <summary>
    /// The campaign's slow ambience drift: every 20-level chapter has an anchor
    /// palette (purple night → ocean teal → dusk plum → ember), and each level
    /// interpolates towards the NEXT anchor — so no two consecutive levels differ
    /// by more than 1/20th of a chapter transition. Deliberately gentle: candy
    /// colours and UI accents never change, only the ambience (background + cards).
    /// Pure C# so the drift-rate guarantee is unit-testable.
    /// </summary>
    public static class ThemeCurve
    {
        public const int ChapterLength = 20;

        // Anchor palettes at level 1, 21, 41, 61, and a tail for the drift past 80.
        // Levels 1-60 interpolate over the SAME first four anchors as before the
        // chapter-4 expansion — their colours are bit-identical (landmark-tested).
        private static readonly ThemeParameters[] Anchors =
        {
            new ThemeParameters( // chapter 0 — purple night (the original look)
                new ThemeColor(0.12f, 0.09f, 0.24f), new ThemeColor(0.06f, 0.06f, 0.14f),
                new ThemeColor(0.137f, 0.145f, 0.28f), new ThemeColor(0.10f, 0.105f, 0.21f), 0),
            new ThemeParameters( // chapter 1 — ocean teal
                new ThemeColor(0.06f, 0.17f, 0.24f), new ThemeColor(0.03f, 0.09f, 0.14f),
                new ThemeColor(0.10f, 0.19f, 0.28f), new ThemeColor(0.07f, 0.14f, 0.21f), 1),
            new ThemeParameters( // chapter 2 — dusk plum
                new ThemeColor(0.23f, 0.10f, 0.20f), new ThemeColor(0.12f, 0.05f, 0.10f),
                new ThemeColor(0.26f, 0.13f, 0.24f), new ThemeColor(0.19f, 0.09f, 0.17f), 2),
            new ThemeParameters( // chapter 3 — warm ember (levels 61-80 open here)
                new ThemeColor(0.26f, 0.13f, 0.10f), new ThemeColor(0.13f, 0.06f, 0.05f),
                new ThemeColor(0.29f, 0.16f, 0.14f), new ThemeColor(0.21f, 0.11f, 0.09f), 3),
            new ThemeParameters( // tail — golden dawn (approached after level 80)
                new ThemeColor(0.27f, 0.19f, 0.08f), new ThemeColor(0.14f, 0.10f, 0.04f),
                new ThemeColor(0.30f, 0.22f, 0.12f), new ThemeColor(0.22f, 0.16f, 0.07f), 4),
        };

        public static ThemeParameters For(int level)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level), "Levels are 1-based.");

            // Continuous position in anchor space: level 1 -> 0.0, level 21 -> 1.0, ...
            float position = (level - 1) / (float)ChapterLength;
            int segment = Math.Min((int)position, Anchors.Length - 2);
            float t = Math.Min(position - segment, 1f); // clamp: past the tail, hold the last palette

            ThemeParameters from = Anchors[segment];
            ThemeParameters to = Anchors[segment + 1];

            return new ThemeParameters(
                ThemeColor.Lerp(from.BgTop, to.BgTop, t),
                ThemeColor.Lerp(from.BgBottom, to.BgBottom, t),
                ThemeColor.Lerp(from.Card, to.Card, t),
                ThemeColor.Lerp(from.Slot, to.Slot, t),
                Math.Min((level - 1) / ChapterLength, Anchors.Length - 1));
        }
    }
}
