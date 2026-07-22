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

        /// <summary>
        /// Colorblind-mode variant: the normal candy plus a small badge in the lower-
        /// right corner carrying a distinct white glyph per colour index (dot, plus,
        /// bar, cross, ring). The silhouettes already differ per colour; the glyph adds
        /// a second redundant channel for players who can't rely on hue at a glance.
        /// </summary>
        public static byte[] RenderColorblind(int size, Rgb color, int shapeIndex, TileKind kind)
        {
            byte[] pixels = Render(size, color, shapeIndex, kind);
            StampGlyphBadge(pixels, size, shapeIndex);
            return pixels;
        }

        private static void StampGlyphBadge(byte[] pixels, int size, int shapeIndex)
        {
            const float cx = 0.46f, cy = -0.46f; // badge centre (x right, y up)
            const float badge = 0.27f;           // badge radius in normalized units
            float aa = 3f / size;

            for (int row = 0; row < size; row++)
            {
                float y = 1f - 2f * (row + 0.5f) / size;
                if (Math.Abs(y - cy) > badge + aa * 2f)
                    continue;

                for (int col = 0; col < size; col++)
                {
                    float x = 2f * (col + 0.5f) / size - 1f;
                    float badgeD = Dist(x, y, cx, cy) - badge;
                    if (badgeD > aa)
                        continue;

                    float badgeA = Clamp01(0.5f - badgeD / aa);
                    float glyphD = GlyphSdf(shapeIndex, (x - cx) / badge, (y - cy) / badge);
                    float glyphA = Clamp01(0.5f - glyphD * badge / aa);

                    int offset = (row * size + col) * 4;
                    float er = pixels[offset] / 255f;
                    float eg = pixels[offset + 1] / 255f;
                    float eb = pixels[offset + 2] / 255f;
                    float ea = pixels[offset + 3] / 255f;

                    // Dark plate over the candy (straight-alpha "over"), then the glyph.
                    float outA = badgeA + ea * (1f - badgeA);
                    if (outA <= 0f)
                        continue;
                    float r = (0.13f * badgeA + er * ea * (1f - badgeA)) / outA;
                    float g = (0.11f * badgeA + eg * ea * (1f - badgeA)) / outA;
                    float b = (0.18f * badgeA + eb * ea * (1f - badgeA)) / outA;

                    r = Lerp(r, 1f, glyphA);
                    g = Lerp(g, 1f, glyphA);
                    b = Lerp(b, 1f, glyphA);

                    WritePixel(pixels, offset, r, g, b, outA);
                }
            }
        }

        /// <summary>Glyph SDF in badge-local coordinates (unit circle). One symbol per colour index.</summary>
        private static float GlyphSdf(int shapeIndex, float x, float y)
        {
            switch (((shapeIndex % 5) + 5) % 5)
            {
                case 0: // filled dot
                    return (float)Math.Sqrt(x * x + y * y) - 0.4f;

                case 1: // plus
                    return Math.Min(BoxSdf(x, y, 0.52f, 0.15f), BoxSdf(x, y, 0.15f, 0.52f));

                case 2: // horizontal bar
                    return BoxSdf(x, y, 0.5f, 0.16f);

                case 3: // diagonal cross
                {
                    float rx = (x + y) * 0.7071f;
                    float ry = (y - x) * 0.7071f;
                    return Math.Min(BoxSdf(rx, ry, 0.55f, 0.14f), BoxSdf(rx, ry, 0.14f, 0.55f));
                }

                default: // ring
                    return Math.Abs((float)Math.Sqrt(x * x + y * y) - 0.38f) - 0.13f;
            }
        }

        private static float BoxSdf(float x, float y, float halfWidth, float halfHeight)
        {
            return Math.Max(Math.Abs(x) - halfWidth, Math.Abs(y) - halfHeight);
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

        /// <summary>The chocolate block: a rounded brown slab with ridge grooves.</summary>
        public static byte[] RenderChocolate(int size)
        {
            var pixels = new byte[size * size * 4];
            float aa = 3f / size;

            for (int row = 0; row < size; row++)
            {
                float y = 1f - 2f * (row + 0.5f) / size;
                for (int col = 0; col < size; col++)
                {
                    float x = 2f * (col + 0.5f) / size - 1f;

                    float d = ShapeSdf(1, x, y); // the rounded square silhouette
                    float alpha = Clamp01(0.5f - d / aa);

                    float shade = Lerp(0.7f, 1.05f, (y + 1f) * 0.5f);
                    float r = 0.42f * shade, g = 0.26f * shade, b = 0.14f * shade;

                    // Three horizontal grooves make it read as a chocolate bar.
                    float groove = Math.Min(Math.Abs(y - 0.34f), Math.Min(Math.Abs(y + 0.02f), Math.Abs(y + 0.38f)));
                    if (groove < 0.045f)
                    {
                        float dark = Lerp(0.55f, 1f, groove / 0.045f);
                        r *= dark; g *= dark; b *= dark;
                    }

                    float rim = Clamp01((d + 0.12f) / 0.12f);
                    float rimMul = Lerp(1f, 0.62f, rim);
                    r *= rimMul; g *= rimMul; b *= rimMul;

                    float hd = Dist(x, y, -0.34f, 0.42f);
                    if (hd < 0.26f)
                    {
                        float shine = (1f - hd / 0.26f) * 0.28f;
                        r = Lerp(r, 1f, shine); g = Lerp(g, 1f, shine); b = Lerp(b, 1f, shine);
                    }

                    WritePixel(pixels, (row * size + col) * 4, r, g, b, alpha);
                }
            }
            return pixels;
        }

        /// <summary>The ingredient: a cherry pair with stems and a leaf.</summary>
        public static byte[] RenderIngredient(int size)
        {
            var pixels = new byte[size * size * 4];
            float aa = 3f / size;

            for (int row = 0; row < size; row++)
            {
                float y = 1f - 2f * (row + 0.5f) / size;
                for (int col = 0; col < size; col++)
                {
                    float x = 2f * (col + 0.5f) / size - 1f;

                    // Two cherries, slightly different sizes.
                    float dA = Dist(x, y, -0.3f, -0.3f) - 0.36f;
                    float dB = Dist(x, y, 0.34f, -0.42f) - 0.3f;
                    // Stems: line segments from each cherry top to a shared point.
                    float stemA = SegmentSdf(x, y, -0.24f, 0.02f, 0.06f, 0.62f) - 0.045f;
                    float stemB = SegmentSdf(x, y, 0.3f, -0.16f, 0.06f, 0.62f) - 0.045f;
                    // Leaf: a squashed disc by the stem top.
                    float leaf = Dist((x - 0.28f) * 1.6f, y - 0.6f, 0f, 0f) - 0.22f;

                    float r = 0f, g = 0f, b = 0f, alpha = 0f;

                    float cherry = Math.Min(dA, dB);
                    if (cherry < aa)
                    {
                        float a = Clamp01(0.5f - cherry / aa);
                        float centre = dA < dB ? Dist(x, y, -0.3f, -0.3f) / 0.36f : Dist(x, y, 0.34f, -0.42f) / 0.3f;
                        float shade = Lerp(1.05f, 0.6f, Clamp01(centre));
                        r = 0.85f * shade; g = 0.16f * shade; b = 0.18f * shade;
                        float hd = dA < dB ? Dist(x, y, -0.42f, -0.18f) : Dist(x, y, 0.26f, -0.32f);
                        if (hd < 0.12f)
                        {
                            float shine = (1f - hd / 0.12f) * 0.55f;
                            r = Lerp(r, 1f, shine); g = Lerp(g, 1f, shine); b = Lerp(b, 1f, shine);
                        }
                        alpha = a;
                    }

                    float green = Math.Min(Math.Min(stemA, stemB), leaf);
                    if (green < aa)
                    {
                        float a = Clamp01(0.5f - green / aa);
                        if (a > alpha)
                        {
                            r = 0.32f; g = 0.62f; b = 0.24f;
                            alpha = a;
                        }
                    }

                    WritePixel(pixels, (row * size + col) * 4, r, g, b, alpha);
                }
            }
            return pixels;
        }

        /// <summary>
        /// The licorice cage OVERLAY (transparent centre): a rounded frame with three
        /// vertical bars, drawn over the locked candy by the view.
        /// </summary>
        public static byte[] RenderLockCage(int size)
        {
            var pixels = new byte[size * size * 4];
            float aa = 3f / size;

            for (int row = 0; row < size; row++)
            {
                float y = 1f - 2f * (row + 0.5f) / size;
                for (int col = 0; col < size; col++)
                {
                    float x = 2f * (col + 0.5f) / size - 1f;

                    float body = ShapeSdf(1, x, y); // rounded square silhouette
                    float frame = Math.Abs(body + 0.05f) - 0.07f; // hollow frame just inside the edge

                    // Vertical bars at x = -0.4, 0, 0.4 spanning the body.
                    float bars = Math.Min(Math.Abs(x + 0.4f), Math.Min(Math.Abs(x), Math.Abs(x - 0.4f))) - 0.055f;
                    if (body > 0f)
                        bars = 1f; // bars exist only inside the silhouette

                    float d = Math.Min(frame, bars);
                    float alpha = Clamp01(0.5f - d / aa) * 0.92f;
                    if (alpha <= 0f)
                    {
                        WritePixel(pixels, (row * size + col) * 4, 0f, 0f, 0f, 0f);
                        continue;
                    }

                    // Dark steel with a vertical sheen.
                    float shade = Lerp(0.75f, 1.15f, (y + 1f) * 0.5f);
                    float r = 0.30f * shade, g = 0.27f * shade, b = 0.36f * shade;

                    WritePixel(pixels, (row * size + col) * 4, r, g, b, alpha);
                }
            }
            return pixels;
        }

        /// <summary>Distance from (x, y) to the segment (ax, ay)-(bx, by).</summary>
        private static float SegmentSdf(float x, float y, float ax, float ay, float bx, float by)
        {
            float px = x - ax, py = y - ay;
            float dx = bx - ax, dy = by - ay;
            float lengthSq = dx * dx + dy * dy;
            float t = lengthSq > 0f ? Clamp01((px * dx + py * dy) / lengthSq) : 0f;
            float cx = px - dx * t, cy = py - dy * t;
            return (float)Math.Sqrt(cx * cx + cy * cy);
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
