using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests
{
    /// <summary>
    /// The generated UI glyphs. They are tinted white masks, so what matters is the
    /// alpha channel: where the shape is, where it is not, and — for the swap icon —
    /// that its two arrows really are the same arrow turned half-way round.
    /// </summary>
    public sealed class UiArtistTests
    {
        private static byte Alpha(byte[] pixels, int size, int col, int row) =>
            pixels[(row * size + col) * 4 + 3];

        [Test]
        public void EveryGlyph_FillsExactlyItsCanvas_AndIsPureWhite()
        {
            const int size = 96;
            foreach (byte[] glyph in new[]
                     {
                         UiArtist.Circle(size), UiArtist.Star(size), UiArtist.Lock(size),
                         UiArtist.Trophy(size), UiArtist.Swap(size),
                     })
            {
                Assert.That(glyph.Length, Is.EqualTo(size * size * 4));
                for (int i = 0; i < glyph.Length; i += 4)
                {
                    if (glyph[i + 3] == 0) continue; // colour under zero alpha is irrelevant
                    Assert.That(glyph[i], Is.EqualTo(255), "glyphs are white so Image can tint them");
                    Assert.That(glyph[i + 1], Is.EqualTo(255));
                    Assert.That(glyph[i + 2], Is.EqualTo(255));
                }
            }
        }

        [Test]
        public void TheSwapIcon_IsARingWithAHoleAndEmptyCorners()
        {
            const int size = 96;
            byte[] swap = UiArtist.Swap(size);

            Assert.That(Alpha(swap, size, size / 2, size / 2), Is.EqualTo(0), "the middle of the ring is open");
            Assert.That(Alpha(swap, size, 0, 0), Is.EqualTo(0));
            Assert.That(Alpha(swap, size, size - 1, size - 1), Is.EqualTo(0));

            // Straight up from the centre the top arc is solid.
            bool hitsTopArc = false;
            for (int row = 0; row < size / 2; row++)
                hitsTopArc |= Alpha(swap, size, size / 2, row) == 255;
            Assert.That(hitsTopArc, Is.True, "the top arc must cross the vertical axis");
        }

        [Test]
        public void TheSwapIcon_IsTheSameArrowTwice_HalfATurnApart()
        {
            // Two arrows chasing each other only read as "swap" when they match; a
            // lopsided pair reads as a broken circle. Rotating the glyph 180 degrees
            // about its centre must give the glyph back.
            const int size = 96;
            byte[] swap = UiArtist.Swap(size);

            int mismatched = 0;
            for (int row = 0; row < size; row++)
            {
                for (int col = 0; col < size; col++)
                {
                    int there = Alpha(swap, size, size - 1 - col, size - 1 - row);
                    int here = Alpha(swap, size, col, row);
                    // Supersampled edges may differ by a hair; a whole-pixel flip may not.
                    if (System.Math.Abs(here - there) > 64)
                        mismatched++;
                }
            }
            Assert.That(mismatched, Is.EqualTo(0));
        }
    }
}
