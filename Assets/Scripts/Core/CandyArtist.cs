using System;

namespace Match3.Core
{
    /// <summary>
    /// Procedural candy renderer — pure pixel math, no UnityEngine, so the same code
    /// can run inside the editor (sprite generator menu) and in plain .NET tooling.
    /// Each colour index gets a distinct SILHOUETTE (circle, rounded square, triangle,
    /// diamond, hexagon), which keeps candies tellable-apart without relying on colour
    /// alone. Kind variants: stripes for striped candies, a dark wrapper band for
    /// wrapped, and a dotted dark sphere for the colour bomb.
    ///
    /// Output: RGBA8 bytes, rows TOP-DOWN (PNG order). Callers building a Unity
    /// Texture2D must flip rows (Unity is bottom-up).
    /// </summary>
    public static class CandyArtist
    {
        public readonly struct Rgb
        {
            public readonly float R;
            public readonly float G;
            public readonly float B;

            public Rgb(float r, float g, float b)
            {
                R = r;
                G = g;
                B = b;
            }
        }

        /// <summary>Renders one candy. <paramref name="shapeIndex"/> is normally the colour index.</summary>
        public static byte[] Render(int size, Rgb color, int shapeIndex, TileKind kind)
        {
            if (kind == TileKind.ColorBomb)
                throw new ArgumentException("Use RenderColorBomb for the colour bomb.", nameof(kind));

            var pixels = new byte[size * size * 4];
            float aa = 3f / size; // anti-alias width in normalized units

            for (int row = 0; row < size; row++)
            {
                float y = 1f - 2f * (row + 0.5f) / size; // top-down rows, y up
                for (int col = 0; col < size; col++)
                {
                    float x = 2f * (col + 0.5f) / size - 1f;

                    float d = ShapeSdf(shapeIndex, x, y);
                    float alpha = Clamp01(0.5f - d / aa);

                    float r = color.R, g = color.G, b = color.B;

                    // Vertical light: brighter towards the top.
                    float shade = Lerp(0.78f, 1.1f, (y + 1f) * 0.5f);
                    r *= shade; g *= shade; b *= shade;

                    // Darkened rim just inside the edge gives the candy body volume.
                    float rim = Clamp01((d + 0.12f) / 0.12f);
                    float rimMul = Lerp(1f, 0.68f, rim);
                    r *= rimMul; g *= rimMul; b *= rimMul;

                    if (kind == TileKind.StripedH || kind == TileKind.StripedV)
                    {
                        float band = kind == TileKind.StripedH ? y : x;
                        if ((int)Math.Floor((band + 1f) * 3f) % 2 == 0)
                        {
                            r = Lerp(r, 1f, 0.7f);
                            g = Lerp(g, 1f, 0.7f);
                            b = Lerp(b, 1f, 0.7f);
                        }
                    }
                    else if (kind == TileKind.Wrapped)
                    {
                        // The dark "wrapper" band around a bright centre.
                        if (d > -0.24f && d < -0.08f)
                        {
                            r *= 0.5f; g *= 0.5f; b *= 0.5f;
                        }
                        float centre = (float)Math.Sqrt(x * x + y * y);
                        if (centre < 0.2f)
                        {
                            float glow = (1f - centre / 0.2f) * 0.85f;
                            r = Lerp(r, 1f, glow);
                            g = Lerp(g, 1f, glow);
                            b = Lerp(b, 1f, glow);
                        }
                    }

                    // Glossy highlight, upper-left.
                    float hd = Dist(x, y, -0.34f, 0.4f);
                    if (hd < 0.3f && kind != TileKind.Wrapped)
                    {
                        float shine = (1f - hd / 0.3f) * 0.45f;
                        r = Lerp(r, 1f, shine);
                        g = Lerp(g, 1f, shine);
                        b = Lerp(b, 1f, shine);
                    }

                    WritePixel(pixels, (row * size + col) * 4, r, g, b, alpha);
                }
            }

            return pixels;
        }

        /// <summary>The colour bomb: a dark sphere sprinkled with dots of every candy colour.</summary>
        public static byte[] RenderColorBomb(int size, Rgb[] palette)
        {
            if (palette == null || palette.Length == 0)
                throw new ArgumentException("Need at least one palette colour.", nameof(palette));

            var pixels = new byte[size * size * 4];
            float aa = 3f / size;

            for (int row = 0; row < size; row++)
            {
                float y = 1f - 2f * (row + 0.5f) / size;
                for (int col = 0; col < size; col++)
                {
                    float x = 2f * (col + 0.5f) / size - 1f;

                    float centre = (float)Math.Sqrt(x * x + y * y);
                    float d = centre - 0.86f;
                    float alpha = Clamp01(0.5f - d / aa);

                    // Dark chocolate sphere, brighter towards the middle.
                    float shade = Lerp(1.5f, 0.65f, Clamp01(centre / 0.86f));
                    float r = 0.16f * shade, g = 0.13f * shade, b = 0.2f * shade;

                    // Candy dots in a ring, one per palette colour (cycled to 8).
                    for (int i = 0; i < 8; i++)
                    {
                        double angle = (i * 45.0 + 22.5) * Math.PI / 180.0;
                        float dotX = 0.52f * (float)Math.Cos(angle);
                        float dotY = 0.52f * (float)Math.Sin(angle);
                        float dd = Dist(x, y, dotX, dotY);
                        if (dd < 0.15f)
                        {
                            Rgb dot = palette[i % palette.Length];
                            float mix = Clamp01((0.15f - dd) / 0.04f);
                            r = Lerp(r, dot.R, mix);
                            g = Lerp(g, dot.G, mix);
                            b = Lerp(b, dot.B, mix);
                        }
                    }

                    float hd = Dist(x, y, -0.3f, 0.42f);
                    if (hd < 0.24f)
                    {
                        float shine = (1f - hd / 0.24f) * 0.5f;
                        r = Lerp(r, 1f, shine);
                        g = Lerp(g, 1f, shine);
                        b = Lerp(b, 1f, shine);
                    }

                    WritePixel(pixels, (row * size + col) * 4, r, g, b, alpha);
                }
            }

            return pixels;
        }

        /// <summary>Signed distance to the silhouette for a colour index (negative = inside).</summary>
        private static float ShapeSdf(int shapeIndex, float x, float y)
        {
            switch (((shapeIndex % 5) + 5) % 5)
            {
                case 0: // circle
                    return (float)Math.Sqrt(x * x + y * y) - 0.85f;

                case 1: // rounded square
                {
                    float dx = Math.Abs(x) - 0.6f;
                    float dy = Math.Abs(y) - 0.6f;
                    float ox = Math.Max(dx, 0f);
                    float oy = Math.Max(dy, 0f);
                    return (float)Math.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(dx, dy), 0f) - 0.22f;
                }

                case 2: // rounded equilateral triangle, pointing up
                {
                    const float k = 1.7320508f; // sqrt(3)
                    float px = Math.Abs(x) - 0.78f;
                    float py = y + 0.78f / k + 0.12f;
                    if (px + k * py > 0f)
                    {
                        float nx = (px - k * py) / 2f;
                        float ny = (-k * px - py) / 2f;
                        px = nx;
                        py = ny;
                    }
                    px -= Math.Min(Math.Max(px, -1.56f), 0f);
                    float len = (float)Math.Sqrt(px * px + py * py);
                    return -len * Math.Sign(py) - 0.1f;
                }

                case 3: // diamond
                    return (Math.Abs(x) + Math.Abs(y)) * 0.7071f - 0.62f;

                default: // hexagon (flat top)
                {
                    float ax = Math.Abs(x);
                    float ay = Math.Abs(y);
                    const float kx = -0.8660254f, ky = 0.5f;
                    float dot = 2f * Math.Min(kx * ax + ky * ay, 0f);
                    ax -= dot * kx;
                    ay -= dot * ky;
                    float cx = ax - Math.Min(Math.Max(ax, -0.45f), 0.45f);
                    float cy = ay - 0.78f;
                    float len = (float)Math.Sqrt(cx * cx + cy * cy);
                    return len * Math.Sign(cy) - 0.06f;
                }
            }
        }

        private static void WritePixel(byte[] pixels, int offset, float r, float g, float b, float alpha)
        {
            pixels[offset] = ToByte(r);
            pixels[offset + 1] = ToByte(g);
            pixels[offset + 2] = ToByte(b);
            pixels[offset + 3] = ToByte(alpha);
        }

        private static byte ToByte(float value) => (byte)(Clamp01(value) * 255f + 0.5f);

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        private static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

        private static float Dist(float x, float y, float cx, float cy)
        {
            float dx = x - cx, dy = y - cy;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
