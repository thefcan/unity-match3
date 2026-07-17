using System;

namespace Match3.Core
{
    /// <summary>
    /// Procedural UI-chrome renderer — the interface counterpart to
    /// <see cref="CandyArtist"/>. Draws the shapes the Figma design language uses
    /// (rounded cards, pill buttons, stars, lock, background gradient) as white or
    /// pre-coloured RGBA sprites. White shapes are tinted by UnityEngine.UI.Image;
    /// gradients are baked because Image tinting cannot express them.
    ///
    /// Output: RGBA8 bytes, rows TOP-DOWN (PNG order), same contract as CandyArtist.
    /// </summary>
    public static class UiArtist
    {
        public readonly struct Rgba
        {
            public readonly float R;
            public readonly float G;
            public readonly float B;
            public readonly float A;

            public Rgba(float r, float g, float b, float a = 1f)
            {
                R = r;
                G = g;
                B = b;
                A = a;
            }
        }

        /// <summary>White rounded-rect for 9-sliced cards/buttons. Border for slicing = <paramref name="cornerRadius"/>.</summary>
        public static byte[] RoundedRect(int size, float cornerRadius)
        {
            return RenderSdf(size, (x, y) => RoundedRectSdf(x, y, size, size, cornerRadius), White);
        }

        /// <summary>White rounded-rect OUTLINE (ring) for secondary buttons / highlights.</summary>
        public static byte[] RoundedRectOutline(int size, float cornerRadius, float thickness)
        {
            return RenderSdf(size, (x, y) =>
            {
                float d = RoundedRectSdf(x, y, size, size, cornerRadius);
                return Math.Abs(d + thickness * 0.5f) - thickness * 0.5f;
            }, White);
        }

        /// <summary>White filled circle (chips, dots).</summary>
        public static byte[] Circle(int size)
        {
            float radius = size * 0.5f - 1f;
            return RenderSdf(size, (x, y) =>
            {
                float dx = x - size * 0.5f, dy = y - size * 0.5f;
                return (float)Math.Sqrt(dx * dx + dy * dy) - radius;
            }, White);
        }

        /// <summary>White five-point star (rating pips) — supersampled polygon.</summary>
        public static byte[] Star(int size)
        {
            // 10 vertices alternating outer/inner radius, tip pointing up.
            var xs = new float[10];
            var ys = new float[10];
            float cx = size * 0.5f, cy = size * 0.52f;
            float outer = size * 0.48f, inner = outer * 0.44f;
            for (int i = 0; i < 10; i++)
            {
                double angle = -Math.PI / 2 + i * Math.PI / 5;
                float r = i % 2 == 0 ? outer : inner;
                xs[i] = cx + r * (float)Math.Cos(angle);
                ys[i] = cy + r * (float)Math.Sin(angle);
            }
            return RenderPolygon(size, xs, ys, White);
        }

        /// <summary>White padlock glyph (locked levels).</summary>
        public static byte[] Lock(int size)
        {
            float unit = size / 24f; // designed on a 24 grid
            return RenderMask(size, (x, y) =>
            {
                // body: rounded rect from y=10..21, x=4..20
                float body = RoundedRectSdfAt(x, y, 4f * unit, 10f * unit, 16f * unit, 11f * unit, 2.5f * unit);
                if (body < 0f) return true;
                // shackle: ring centred (12,8), outer r=6, inner r=3.4, upper half only
                float dx = x - 12f * unit, dy = y - 8f * unit;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                bool ring = dist < 6f * unit && dist > 3.4f * unit && y < 9f * unit;
                return ring;
            }, White);
        }

        /// <summary>
        /// The screen background: a NEUTRAL vertical luminance gradient (white top
        /// fading to mid-gray). Kept hueless on purpose — the UI tints it with the
        /// current chapter's theme colour, so one sprite serves every ambience.
        /// </summary>
        public static byte[] BackgroundGradient(int width, int height)
        {
            var top = new Rgba(1f, 1f, 1f);
            var bottom = new Rgba(0.5f, 0.5f, 0.5f);
            var pixels = new byte[width * height * 4];
            for (int row = 0; row < height; row++)
            {
                float t = row / (float)(height - 1); // rows are top-down
                var c = Mix(top, bottom, t);
                for (int col = 0; col < width; col++)
                    WritePixel(pixels, (row * width + col) * 4, c);
            }
            return pixels;
        }

        /// <summary>The CTA pill: baked pink gradient rounded-rect (Image tint cannot do gradients).</summary>
        public static byte[] PillGradient(int size, float cornerRadius)
        {
            var top = new Rgba(1.0f, 0.45f, 0.55f);
            var bottom = new Rgba(0.95f, 0.25f, 0.4f);
            return RenderSdf(size, (x, y) => RoundedRectSdf(x, y, size, size, cornerRadius),
                (x, y) => Mix(top, bottom, y / (size - 1f)));
        }

        // ---- Rendering helpers ---------------------------------------------------------

        private static readonly Func<float, float, Rgba> White = (x, y) => new Rgba(1f, 1f, 1f);

        private static byte[] RenderSdf(int size, Func<float, float, float> sdf, Func<float, float, Rgba> colorAt)
        {
            var pixels = new byte[size * size * 4];
            for (int row = 0; row < size; row++)
            {
                for (int col = 0; col < size; col++)
                {
                    float d = sdf(col + 0.5f, row + 0.5f);
                    float alpha = Clamp01(0.5f - d / 1.5f);
                    Rgba c = colorAt(col + 0.5f, row + 0.5f);
                    WritePixel(pixels, (row * size + col) * 4, new Rgba(c.R, c.G, c.B, c.A * alpha));
                }
            }
            return pixels;
        }

        /// <summary>Boolean mask shape with 3x3 supersampling (for glyphs without a clean SDF).</summary>
        private static byte[] RenderMask(int size, Func<float, float, bool> inside, Func<float, float, Rgba> colorAt)
        {
            var pixels = new byte[size * size * 4];
            for (int row = 0; row < size; row++)
            {
                for (int col = 0; col < size; col++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < 3; sy++)
                        for (int sx = 0; sx < 3; sx++)
                            if (inside(col + (sx + 0.5f) / 3f, row + (sy + 0.5f) / 3f))
                                hits++;
                    Rgba c = colorAt(col + 0.5f, row + 0.5f);
                    WritePixel(pixels, (row * size + col) * 4, new Rgba(c.R, c.G, c.B, c.A * hits / 9f));
                }
            }
            return pixels;
        }

        private static byte[] RenderPolygon(int size, float[] xs, float[] ys, Func<float, float, Rgba> colorAt)
        {
            return RenderMask(size, (px, py) =>
            {
                // even-odd ray cast
                bool inside = false;
                for (int i = 0, j = xs.Length - 1; i < xs.Length; j = i++)
                {
                    if (ys[i] > py != ys[j] > py &&
                        px < (xs[j] - xs[i]) * (py - ys[i]) / (ys[j] - ys[i]) + xs[i])
                        inside = !inside;
                }
                return inside;
            }, colorAt);
        }

        /// <summary>SDF of a rounded rect spanning the full texture minus a 1px guard.</summary>
        private static float RoundedRectSdf(float x, float y, float width, float height, float radius)
        {
            return RoundedRectSdfAt(x, y, 1f, 1f, width - 2f, height - 2f, radius);
        }

        private static float RoundedRectSdfAt(float x, float y, float left, float top, float width, float height, float radius)
        {
            float cx = left + width * 0.5f, cy = top + height * 0.5f;
            float bx = width * 0.5f - radius, by = height * 0.5f - radius;
            float dx = Math.Abs(x - cx) - bx;
            float dy = Math.Abs(y - cy) - by;
            float ox = Math.Max(dx, 0f), oy = Math.Max(dy, 0f);
            return (float)Math.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(dx, dy), 0f) - radius;
        }

        private static Rgba Mix(Rgba a, Rgba b, float t)
        {
            t = Clamp01(t);
            return new Rgba(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);
        }

        private static void WritePixel(byte[] pixels, int offset, Rgba c)
        {
            pixels[offset] = ToByte(c.R);
            pixels[offset + 1] = ToByte(c.G);
            pixels[offset + 2] = ToByte(c.B);
            pixels[offset + 3] = ToByte(c.A);
        }

        private static byte ToByte(float value) => (byte)(Clamp01(value) * 255f + 0.5f);

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
