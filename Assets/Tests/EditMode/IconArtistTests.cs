using System;
using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests
{
    /// <summary>
    /// The app icon is generated like every other asset here, so it gets the same
    /// treatment: the rules a launcher actually enforces — an opaque background layer,
    /// nothing outside the adaptive safe zone, rounded corners on the legacy square —
    /// are pinned as tests rather than checked by eye once.
    /// </summary>
    public sealed class IconArtistTests
    {
        private static readonly CandyArtist.Rgb Top = new CandyArtist.Rgb(0.12f, 0.09f, 0.24f);
        private static readonly CandyArtist.Rgb Bottom = new CandyArtist.Rgb(0.06f, 0.06f, 0.14f);

        private static readonly CandyArtist.Rgb[] Palette =
        {
            new CandyArtist.Rgb(0.91f, 0.30f, 0.24f),
            new CandyArtist.Rgb(0.18f, 0.80f, 0.44f),
            new CandyArtist.Rgb(0.20f, 0.60f, 0.86f),
            new CandyArtist.Rgb(0.95f, 0.77f, 0.06f),
            new CandyArtist.Rgb(0.61f, 0.35f, 0.71f),
        };

        private static byte AlphaAt(byte[] pixels, int size, int col, int row) =>
            pixels[(row * size + col) * 4 + 3];

        [Test]
        public void EveryRenderer_FillsExactlyItsCanvas()
        {
            Assert.That(IconArtist.RenderIcon(64, Top, Bottom, Palette).Length, Is.EqualTo(64 * 64 * 4));
            Assert.That(IconArtist.RenderAdaptiveBackground(48, Top, Bottom).Length, Is.EqualTo(48 * 48 * 4));
            Assert.That(IconArtist.RenderAdaptiveForeground(48, Palette).Length, Is.EqualTo(48 * 48 * 4));
            Assert.That(IconArtist.RenderFeatureGraphic(128, 64, Top, Bottom, Palette).Length,
                        Is.EqualTo(128 * 64 * 4));
        }

        [Test]
        public void TheLegacyIcon_IsOpaqueInside_AndRoundedAtTheCorners()
        {
            const int size = 128;
            byte[] icon = IconArtist.RenderIcon(size, Top, Bottom, Palette);

            Assert.That(AlphaAt(icon, size, size / 2, size / 2), Is.EqualTo(255), "the middle must be solid");
            Assert.That(AlphaAt(icon, size, 0, 0), Is.EqualTo(0), "the very corner is rounded away");
            Assert.That(AlphaAt(icon, size, size - 1, size - 1), Is.EqualTo(0));
            Assert.That(AlphaAt(icon, size, size / 2, 0), Is.EqualTo(255), "mid-edge is not a corner");
        }

        [Test]
        public void TheAdaptiveBackground_HasNoCornersOfItsOwn()
        {
            // The launcher masks this layer to its own shape; pre-rounding it would show
            // that mask cutting through a second, different curve.
            const int size = 96;
            byte[] background = IconArtist.RenderAdaptiveBackground(size, Top, Bottom);

            for (int col = 0; col < size; col++)
            {
                Assert.That(AlphaAt(background, size, col, 0), Is.EqualTo(255));
                Assert.That(AlphaAt(background, size, col, size - 1), Is.EqualTo(255));
            }
        }

        [Test]
        public void TheAdaptiveForeground_StaysInsideTheSafeZone()
        {
            const int size = 200;
            byte[] foreground = IconArtist.RenderAdaptiveForeground(size, Palette);

            // Anything a launcher may crop away must be empty. The margin is half of
            // what the safe zone leaves over, per side.
            int margin = (int)(size * (1f - IconArtist.AdaptiveSafeZone) * 0.5f);
            for (int row = 0; row < size; row++)
            {
                for (int col = 0; col < size; col++)
                {
                    bool outside = col < margin || col >= size - margin ||
                                   row < margin || row >= size - margin;
                    if (outside)
                        Assert.That(AlphaAt(foreground, size, col, row), Is.EqualTo(0),
                                    $"({col},{row}) is outside the safe zone and must be empty");
                }
            }
        }

        [Test]
        public void TheAdaptiveForeground_ActuallyDrawsSomething()
        {
            const int size = 200;
            byte[] foreground = IconArtist.RenderAdaptiveForeground(size, Palette);

            int opaque = 0;
            for (int i = 3; i < foreground.Length; i += 4)
                if (foreground[i] > 200)
                    opaque++;

            // A blank foreground layer would pass every "stays inside" check above.
            Assert.That(opaque, Is.GreaterThan(size * size / 10), "the cluster must cover real area");
        }

        [Test]
        public void TheSameInputs_DrawTheSameBytes()
        {
            byte[] first = IconArtist.RenderIcon(96, Top, Bottom, Palette);
            byte[] second = IconArtist.RenderIcon(96, Top, Bottom, Palette);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void APaletteWithNoColours_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => IconArtist.RenderAdaptiveForeground(64, new CandyArtist.Rgb[0]));
            Assert.Throws<ArgumentException>(() => IconArtist.RenderIcon(64, Top, Bottom, null));
        }
    }
}
