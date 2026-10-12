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

        /// <summary>White trophy cup (the event panel's podium shelf) — tinted gold/silver/bronze.</summary>
        public static byte[] Trophy(int size)
        {
            float unit = size / 24f; // designed on a 24 grid, like Lock
            return RenderMask(size, (x, y) =>
            {
                // bowl
                if (RoundedRectSdfAt(x, y, 6f * unit, 3f * unit, 12f * unit, 8f * unit, 3f * unit) < 0f)
                    return true;
                // handles: half-rings hanging off the bowl rim
                float dxl = x - 6.5f * unit, dyl = y - 6f * unit;
                float dl = (float)Math.Sqrt(dxl * dxl + dyl * dyl);
                if (dl < 4.2f * unit && dl > 2.6f * unit && x < 6.5f * unit)
                    return true;
                float dxr = x - 17.5f * unit, dyr = y - 6f * unit;
                float dr = (float)Math.Sqrt(dxr * dxr + dyr * dyr);
                if (dr < 4.2f * unit && dr > 2.6f * unit && x > 17.5f * unit)
                    return true;
                // stem and base
                if (x > 10.4f * unit && x < 13.6f * unit && y >= 11f * unit && y < 17f * unit)
                    return true;
                return RoundedRectSdfAt(x, y, 7f * unit, 17f * unit, 10f * unit, 4f * unit, 1.5f * unit) < 0f;
            }, White);
        }

        /// <summary>
        /// White "swap this for another" glyph: two arcs chasing each other round a
        /// ring, each ending in an arrowhead. Drawn rather than typed because none of
        /// the shipped fonts carry a refresh arrow — the daily-task reroll button fell
        /// back to "~", which at button size reads as a minus sign, i.e. "remove".
        /// </summary>
        public static byte[] Swap(int size)
        {
            float unit = size / 24f; // designed on a 24 grid, like Lock and Trophy
            const float centre = 12f, outer = 8.4f, inner = 5.8f, mid = (outer + inner) * 0.5f;

            // Angles in degrees, screen space (y down): -90 is the top, 0 the right.
            // Each arc stops short of the other so its arrowhead has room to land.
            const float topStart = -175f, topEnd = -40f, bottomStart = 5f, bottomEnd = 140f;

            // An arrowhead at the END of an arc, pointing the way the arc travels.
            void Head(float degrees, out float tipX, out float tipY, out float aX, out float aY, out float bX, out float bY)
            {
                double a = degrees * Math.PI / 180.0;
                float nx = (float)Math.Cos(a), ny = (float)Math.Sin(a); // radial, outward
                float tx = -ny, ty = nx;                                // tangent, clockwise
                float px = centre + mid * nx, py = centre + mid * ny;
                tipX = px + tx * 4.6f; tipY = py + ty * 4.6f;
                aX = px + nx * 3.5f; aY = py + ny * 3.5f;
                bX = px - nx * 3.5f; bY = py - ny * 3.5f;
            }

            Head(topEnd, out float t1x, out float t1y, out float a1x, out float a1y, out float b1x, out float b1y);
            Head(bottomEnd, out float t2x, out float t2y, out float a2x, out float a2y, out float b2x, out float b2y);

            return RenderMask(size, (px, py) =>
            {
                float x = px / unit, y = py / unit;
                if (InTriangle(x, y, t1x, t1y, a1x, a1y, b1x, b1y) ||
                    InTriangle(x, y, t2x, t2y, a2x, a2y, b2x, b2y))
                    return true;

                float dx = x - centre, dy = y - centre;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                if (dist > outer || dist < inner)
                    return false;

                float angle = (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI);
                return (angle >= topStart && angle <= topEnd) || (angle >= bottomStart && angle <= bottomEnd);
            }, White);
        }

        private static bool InTriangle(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = (px - bx) * (ay - by) - (ax - bx) * (py - by);
            float d2 = (px - cx) * (by - cy) - (bx - cx) * (py - cy);
            float d3 = (px - ax) * (cy - ay) - (cx - ax) * (py - ay);
            bool negative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool positive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(negative && positive);
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
