using System;

namespace Match3.Core
{
    /// <summary>
    /// The app's launcher icon and store banner, drawn the same way everything else in
    /// this project is drawn: pure pixel math with no UnityEngine, so it runs in the
    /// editor menu and in plain .NET tests alike. A stock Unity logo on a store listing
    /// is an instant "unfinished" signal, and shipping a hand-made PNG would be the one
    /// asset in the repo nobody could regenerate.
    ///
    /// Composition: the menu's own purple-night gradient, a three-candy cluster lifted
    /// straight from <see cref="CandyArtist"/> (so the icon can never drift from the
    /// game's actual art), and a soft glow behind them.
    ///
    /// Output: RGBA8, rows TOP-DOWN (PNG order) — same contract as CandyArtist.
    /// </summary>
    public static class IconArtist
    {
        /// <summary>
        /// Android's adaptive icon reserves the outer ~1/3: the launcher may mask the
        /// square to a circle, a squircle or a rounded square, and only the middle 66%
        /// is guaranteed visible. Everything meaningful stays inside this fraction.
        /// </summary>
        public const float AdaptiveSafeZone = 0.66f;

        /// <summary>One candy in the cluster: which silhouette, where, how big.</summary>
        private readonly struct Placement
        {
            public readonly int ShapeIndex;
            public readonly float CenterX; // normalized 0..1 across the canvas
            public readonly float CenterY;
            public readonly float Scale;   // candy width as a fraction of the canvas

            public Placement(int shapeIndex, float centerX, float centerY, float scale)
            {
                ShapeIndex = shapeIndex;
                CenterX = centerX;
                CenterY = centerY;
                Scale = scale;
            }
        }

        /// <summary>
        /// The cluster, in draw order (back to front): two candies tucked behind and a
        /// hero candy front-centre, so the icon reads as "match-3" at 48px.
        /// </summary>
        private static readonly Placement[] Cluster =
        {
            new Placement(4, 0.32f, 0.36f, 0.44f), // hexagon, back left
            new Placement(3, 0.68f, 0.36f, 0.44f), // diamond, back right
            new Placement(0, 0.50f, 0.59f, 0.52f), // circle, hero
        };

        /// <summary>
        /// The full-bleed launcher icon (legacy Android, iOS, standalone): opaque, with
        /// the corners rounded in like a real app icon.
        /// </summary>
        public static byte[] RenderIcon(int size, CandyArtist.Rgb top, CandyArtist.Rgb bottom,
                                        CandyArtist.Rgb[] palette)
        {
            byte[] pixels = Gradient(size, size, top, bottom, cornerRadius: size * 0.22f);
            Glow(pixels, size, size, 0.5f, 0.5f, 0.42f, 0.30f);
            DrawCluster(pixels, size, size, palette, AdaptiveSafeZone + 0.22f);
            return pixels;
        }

        /// <summary>
        /// Android adaptive icon, BACKGROUND layer: full bleed, no rounding — the
        /// launcher applies its own mask, and a pre-rounded background would show the
        /// system's own shape cutting through this one's corners.
        /// </summary>
        public static byte[] RenderAdaptiveBackground(int size, CandyArtist.Rgb top, CandyArtist.Rgb bottom)
        {
            byte[] pixels = Gradient(size, size, top, bottom, cornerRadius: 0f);
            Glow(pixels, size, size, 0.5f, 0.5f, 0.45f, 0.26f);
            return pixels;
        }

        /// <summary>
        /// Android adaptive icon, FOREGROUND layer: the cluster alone on transparency,
        /// scaled to sit inside <see cref="AdaptiveSafeZone"/> so no launcher mask can
        /// clip a candy.
        /// </summary>
        public static byte[] RenderAdaptiveForeground(int size, CandyArtist.Rgb[] palette)
        {
            var pixels = new byte[size * size * 4];
            DrawCluster(pixels, size, size, palette, AdaptiveSafeZone);
            return pixels;
        }

        /// <summary>
        /// The Play listing's feature graphic (1024x500): the same gradient, the cluster
        /// off to one side, room for the title on the other.
        /// </summary>
        public static byte[] RenderFeatureGraphic(int width, int height, CandyArtist.Rgb top,
                                                  CandyArtist.Rgb bottom, CandyArtist.Rgb[] palette)
        {
            byte[] pixels = Gradient(width, height, top, bottom, cornerRadius: 0f);
            Glow(pixels, width, height, 0.63f, 0.48f, 0.52f, 0.28f); // centred on the cluster

            // Same cluster, parked in the right third; a banner's left side is where
            // the store's own title overlay lands.
            foreach (Placement placement in Cluster)
            {
                float candySize = placement.Scale * height * 0.78f;
                float cx = width * (0.62f + (placement.CenterX - 0.5f) * 0.30f);
                float cy = height * placement.CenterY;
                Blit(pixels, width, height, CandyArtist.Render((int)candySize, palette[placement.ShapeIndex % palette.Length],
                                                               placement.ShapeIndex, TileKind.Normal),
                     (int)candySize, cx, cy);
            }
            return pixels;
        }

        // ---- Composition -----------------------------------------------------------------

        private static void DrawCluster(byte[] pixels, int width, int height,
                                        CandyArtist.Rgb[] palette, float fit)
        {
            if (palette == null || palette.Length == 0)
                throw new ArgumentException("Need at least one candy colour.", nameof(palette));

            int canvas = Math.Min(width, height);
            foreach (Placement placement in Cluster)
            {
                int candySize = Math.Max(4, (int)(placement.Scale * fit * canvas));
                byte[] candy = CandyArtist.Render(candySize, palette[placement.ShapeIndex % palette.Length],
                                                  placement.ShapeIndex, TileKind.Normal);

                // Pull the cluster towards the middle by the same fit factor, so the
                // whole arrangement shrinks into the safe zone rather than just the
                // candies shrinking inside an unchanged layout.
                float cx = width * (0.5f + (placement.CenterX - 0.5f) * fit);
                float cy = height * (0.5f + (placement.CenterY - 0.5f) * fit);
                Blit(pixels, width, height, candy, candySize, cx, cy);
            }
        }

        /// <summary>Alpha-composites a square RGBA source centred on (cx, cy).</summary>
        private static void Blit(byte[] target, int width, int height, byte[] source, int size,
                                 float centerX, float centerY)
        {
            int left = (int)(centerX - size * 0.5f);
            int topRow = (int)(centerY - size * 0.5f);

            for (int row = 0; row < size; row++)
            {
                int y = topRow + row;
                if (y < 0 || y >= height) continue;

                for (int col = 0; col < size; col++)
                {
                    int x = left + col;
                    if (x < 0 || x >= width) continue;

                    int src = (row * size + col) * 4;
                    float alpha = source[src + 3] / 255f;
                    if (alpha <= 0f) continue;

                    int dst = (y * width + x) * 4;
                    for (int channel = 0; channel < 3; channel++)
                        target[dst + channel] = (byte)(source[src + channel] * alpha +
                                                       target[dst + channel] * (1f - alpha));
                    target[dst + 3] = (byte)Math.Min(255f, target[dst + 3] + 255f * alpha);
                }
            }
        }

        /// <summary>Vertical gradient, optionally with rounded (transparent) corners.</summary>
        private static byte[] Gradient(int width, int height, CandyArtist.Rgb top,
                                       CandyArtist.Rgb bottom, float cornerRadius)
        {
            var pixels = new byte[width * height * 4];
            for (int row = 0; row < height; row++)
            {
                float t = height <= 1 ? 0f : row / (float)(height - 1);
                float r = Lerp(top.R, bottom.R, t);
                float g = Lerp(top.G, bottom.G, t);
                float b = Lerp(top.B, bottom.B, t);

                for (int col = 0; col < width; col++)
                {
                    float alpha = cornerRadius <= 0f ? 1f : RoundedAlpha(col, row, width, height, cornerRadius);
                    int index = (row * width + col) * 4;
                    pixels[index] = ToByte(r);
                    pixels[index + 1] = ToByte(g);
                    pixels[index + 2] = ToByte(b);
                    pixels[index + 3] = ToByte(alpha);
                }
            }
            return pixels;
        }

        /// <summary>Anti-aliased rounded-rectangle coverage for one pixel.</summary>
        private static float RoundedAlpha(int col, int row, int width, int height, float radius)
        {
            float x = col + 0.5f;
            float y = row + 0.5f;
            float dx = Math.Max(Math.Max(radius - x, x - (width - radius)), 0f);
            float dy = Math.Max(Math.Max(radius - y, y - (height - radius)), 0f);
            float distance = (float)Math.Sqrt(dx * dx + dy * dy) - radius;
            return Clamp01(0.5f - distance); // 1px feather
        }

        /// <summary>A soft radial lift behind the candies, so they read off the gradient.</summary>
        private static void Glow(byte[] pixels, int width, int height, float centerX, float centerY,
                                 float radius, float strength)
        {
            float cx = centerX * width;
            float cy = centerY * height;
            float r = radius * Math.Min(width, height);

            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    float dx = (col + 0.5f - cx) / r;
                    float dy = (row + 0.5f - cy) / r;
                    float falloff = 1f - Clamp01((float)Math.Sqrt(dx * dx + dy * dy));
                    if (falloff <= 0f) continue;

                    float lift = strength * falloff * falloff;
                    int index = (row * width + col) * 4;
                    for (int channel = 0; channel < 3; channel++)
                        pixels[index + channel] = (byte)Math.Min(255f, pixels[index + channel] + 255f * lift);
                }
            }
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        private static byte ToByte(float value) => (byte)(Clamp01(value) * 255f + 0.5f);
    }
}
