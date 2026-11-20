using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SnowDesk
{
    /// <summary>
    /// Procedural, pre-rendered imagery for the space theme: the Milky Way band, supernova glow and remnant,
    /// a ringed gas giant, tumbling asteroids and a comet. Everything is computed once per pixel into bitmaps,
    /// so a frame only blits them.
    /// </summary>
    internal static class SpaceSprites
    {
        /// <summary>A sprite tile placed at a fixed screen position; empty tiles are skipped when drawing.</summary>
        internal sealed class Tile
        {
            internal Bitmap Image;
            internal int X, Y;
        }

        // ---------- noise ----------

        private static double Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 2147483647;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0x7fffffff) / (double)0x7fffffff;
            }
        }

        private static double Noise(double x, double y, int seed)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            double fx = x - ix, fy = y - iy;
            double sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
            double a = Hash(ix, iy, seed), b = Hash(ix + 1, iy, seed), c = Hash(ix, iy + 1, seed), d = Hash(ix + 1, iy + 1, seed);
            return (a + (b - a) * sx) * (1 - sy) + (c + (d - c) * sx) * sy;
        }

        /// <summary>Fractal value noise in 0..1.</summary>
        internal static double Fbm(double x, double y, int seed, int octaves)
        {
            double sum = 0, amplitude = 0.5, total = 0;
            for (int i = 0; i < octaves; i++)
            {
                sum += Noise(x, y, seed + i * 17) * amplitude;
                total += amplitude;
                x *= 2.03;
                y *= 2.03;
                amplitude *= 0.5;
            }
            return sum / total;
        }

        // ---------- Milky Way ----------

        /// <summary>
        /// A faint galactic band across the screen with clumpy star clouds, dark dust lanes and fine star dust.
        /// Built at half resolution, scaled up once and split into tiles; empty tiles are dropped.
        /// </summary>
        internal static List<Tile> MilkyWay(int width, int height, bool deep, int seed)
        {
            int w = Math.Max(2, width / 2), h = Math.Max(2, height / 2);
            var pixels = new int[w * h];
            double x0 = -0.08, y0 = deep ? 0.18 : 0.9, x1 = 1.08, y1 = deep ? 0.86 : 0.06;
            double dx = x1 - x0, dy = (y1 - y0) * height / width, length = Math.Sqrt(dx * dx + dy * dy);
            double nx = -dy / length, ny = dx / length;
            var random = new Random(seed);
            for (int row = 0; row < h; row++)
                for (int col = 0; col < w; col++)
                {
                    double u = (double)col / w, v = (double)row / w;
                    double along = ((u - x0) * dx + (v - y0 * height / width) * dy) / length;
                    double across = (u - x0) * nx + (v - y0 * height / width) * ny;
                    double clumps = Fbm(along * 7, across * 22, seed, 5);
                    double width0 = 0.07 + 0.03 * Fbm(along * 3, 1.7, seed + 5, 3);
                    double band = Math.Exp(-across * across / (width0 * width0)) * (0.35 + 0.95 * clumps * clumps);
                    double lane = Math.Exp(-Math.Pow((across - 0.006 * Math.Sin(along * 9)) / 0.013, 2)) *
                        Fbm(along * 14, across * 30, seed + 9, 4);
                    double light = Math.Max(0, band * (1 - 1.35 * lane));
                    double warm = Math.Exp(-across * across / (width0 * width0 * 0.25));
                    double alpha = Math.Min(54, light * 46);
                    // Below this the band is invisible on any wallpaper; leaving it empty lets whole tiles drop out.
                    if (alpha < 4) continue;
                    pixels[row * w + col] = Pixel(alpha, 196 + 44 * warm, 204 + 22 * warm, 238 - 36 * warm);
                }
            using (Bitmap half = FromPixels(w, h, pixels))
            {
                var full = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(full))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.DrawImage(half, new Rectangle(0, 0, width, height));
                    // Fine star dust follows the band's brightness.
                    using (var dust = new SolidBrush(Color.FromArgb(150, 236, 238, 255)))
                    using (var faint = new SolidBrush(Color.FromArgb(80, 214, 222, 255)))
                        for (int i = 0; i < width * height / 900; i++)
                        {
                            int x = random.Next(width), y = random.Next(height);
                            int a = full.GetPixel(x, y).A;
                            if (random.Next(56) > a) continue;
                            g.FillRectangle(a > 24 && random.Next(3) == 0 ? dust : faint, x, y, 1, 1);
                        }
                }
                return Tiles(full);
            }
        }

        private static List<Tile> Tiles(Bitmap full)
        {
            const int size = 128;
            var tiles = new List<Tile>();
            BitmapData data = full.LockBits(new Rectangle(0, 0, full.Width, full.Height), ImageLockMode.ReadOnly, full.PixelFormat);
            var row = new int[full.Width];
            var used = new bool[(full.Width + size - 1) / size, (full.Height + size - 1) / size];
            try
            {
                for (int y = 0; y < full.Height; y++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, full.Width);
                    for (int x = 0; x < full.Width; x++)
                        if (((row[x] >> 24) & 255) > 3) used[x / size, y / size] = true;
                }
            }
            finally { full.UnlockBits(data); }
            for (int ty = 0; ty < used.GetLength(1); ty++)
                for (int tx = 0; tx < used.GetLength(0); tx++)
                {
                    if (!used[tx, ty]) continue;
                    var area = Rectangle.Intersect(new Rectangle(tx * size, ty * size, size, size), new Rectangle(Point.Empty, full.Size));
                    tiles.Add(new Tile { Image = full.Clone(area, PixelFormat.Format32bppPArgb), X = area.X, Y = area.Y });
                }
            full.Dispose();
            return tiles;
        }

        // ---------- supernova ----------

        /// <summary>A white-hot point glow; brightness falls off smoothly to the edge.</summary>
        internal static Bitmap Glow(int size, Color tint)
        {
            var pixels = new int[size * size];
            double half = size * 0.5;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    double r = Math.Sqrt(Math.Pow((x + 0.5 - half) / half, 2) + Math.Pow((y + 0.5 - half) / half, 2));
                    if (r >= 1) continue;
                    double core = Math.Exp(-r * r * 60), halo = Math.Exp(-r * r * 6) * (1 - r);
                    double a = Math.Min(255, 255 * core + 150 * halo);
                    double white = core / (core + halo * 0.6 + 1e-6);
                    pixels[y * size + x] = Pixel(a, tint.R + (255 - tint.R) * white, tint.G + (255 - tint.G) * white,
                        tint.B + (255 - tint.B) * white);
                }
            return FromPixels(size, size, pixels);
        }

        /// <summary>
        /// The expanding remnant of a supernova: a filamentary shell, red hydrogen inside, teal oxygen at the rim,
        /// with a faint glowing interior.
        /// </summary>
        internal static Bitmap Remnant(int size, int seed)
        {
            var pixels = new int[size * size];
            double half = size * 0.5;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    double px = (x + 0.5 - half) / half, py = (y + 0.5 - half) / half;
                    double r = Math.Sqrt(px * px + py * py);
                    if (r >= 1) continue;
                    double angle = Math.Atan2(py, px);
                    double filaments = Fbm(Math.Cos(angle) * 3 + 5, Math.Sin(angle) * 3 + r * 6, seed, 5);
                    double wobble = 0.78 + 0.07 * Fbm(angle * 2 + 9, 3.3, seed + 3, 3);
                    double shell = Math.Exp(-Math.Pow((r - wobble) / 0.07, 2)) * (0.35 + 1.1 * filaments * filaments);
                    double inner = Math.Exp(-Math.Pow((r - wobble * 0.82) / 0.11, 2)) * Fbm(px * 6, py * 6, seed + 7, 4);
                    double core = Math.Exp(-r * r * 18) * 0.25;
                    double a = Math.Min(230, 210 * shell + 120 * inner + 160 * core);
                    if (a < 1) continue;
                    double rim = Math.Max(0, Math.Min(1, (r - wobble * 0.86) / 0.14));
                    pixels[y * size + x] = Pixel(a, 236 - 150 * rim + 19 * core, 96 + 130 * rim + 120 * core, 112 + 110 * rim + 120 * core);
                }
            return FromPixels(size, size, pixels);
        }

        // ---------- gas giant ----------

        /// <summary>A banded gas giant with a tilted ring system, lit from the upper left; ring and planet shadow each other.</summary>
        internal static Bitmap GasGiant(int radius, int seed)
        {
            int width = radius * 5, height = radius * 3;
            var pixels = new int[width * height];
            double cx = width * 0.5, cy = height * 0.5, tilt = -0.32, cos = Math.Cos(tilt), sin = Math.Sin(tilt);
            double lightX = -0.62, lightY = -0.48, lightZ = 0.62;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    double px = (x + 0.5 - cx) / radius, py = (y + 0.5 - cy) / radius;
                    double rx = px * cos - py * sin, ry = px * sin + py * cos;
                    double ring = (rx * rx) / (2.3 * 2.3) + (ry * ry) / (0.46 * 0.46);
                    bool onRing = ring > 0.42 && ring < 1 && Math.Abs(Math.Sqrt(ring) - 0.83) > 0.025;
                    double disk = px * px + py * py;
                    bool front = ry > 0;
                    int color = 0;
                    if (disk < 1 && !(onRing && front))
                    {
                        double z = Math.Sqrt(1 - disk);
                        double lit = Math.Max(0, px * lightX + py * lightY + z * lightZ);
                        double latitude = ry * 1.0 + 0.08 * Math.Sin(rx * 3);
                        double bands = 0.55 + 0.25 * Math.Sin(latitude * 13 + Fbm(rx * 2, latitude * 8, seed, 3) * 3) +
                            0.12 * Fbm(rx * 6, latitude * 30, seed + 4, 4);
                        // The ring's shadow falls across the globe just below the ring plane.
                        double shadow = Math.Abs(ry - 0.16 * rx - 0.1) < 0.07 && Math.Abs(rx) < 1 ? 0.45 : 1;
                        double light = Math.Pow(lit, 0.7) * shadow;
                        double edge = Math.Min(1, (1 - disk) * radius * 0.5);
                        color = Pixel(255 * edge, (70 + 170 * bands) * light + 8, (56 + 130 * bands) * light + 8, (40 + 90 * bands) * light + 12);
                    }
                    else if (onRing)
                    {
                        double distance = Math.Sqrt(ring);
                        // Behind the planet the ring is hidden; in the planet's shadow it goes dark.
                        double inShadow = !front && Math.Abs(px - lightX * 0.6) < 0.95 && py > -0.2 ? 0.35 : 1;
                        double bright = (0.55 + 0.35 * Math.Sin(distance * 70) + 0.1 * Hash((int)(distance * 400), 1, seed)) * inShadow;
                        double a = 180 * (0.5 + 0.5 * Math.Sin(distance * 40)) * Math.Min(1, (distance - 0.648) * 12);
                        color = Pixel(Math.Max(0, a), 190 * bright + 30, 168 * bright + 26, 132 * bright + 24);
                    }
                    pixels[y * width + x] = color;
                }
            return FromPixels(width, height, pixels);
        }

        // ---------- asteroids and comet ----------

        /// <summary>A lumpy, cratered rock lit from the upper left, turned by <paramref name="turn"/> radians.</summary>
        internal static Bitmap Asteroid(int size, int seed, double turn)
        {
            var pixels = new int[size * size];
            double half = size * 0.5;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    double px = (x + 0.5 - half) / (half * 0.86), py = (y + 0.5 - half) / (half * 0.86);
                    double r = Math.Sqrt(px * px + py * py), angle = Math.Atan2(py, px) + turn;
                    double outline = 0.78 + 0.22 * Fbm(Math.Cos(angle) * 1.6 + 3, Math.Sin(angle) * 1.6 + 3, seed, 4);
                    if (r >= outline) continue;
                    double q = r / outline, z = Math.Sqrt(Math.Max(0, 1 - q * q));
                    double bumpX = Fbm(px * 4 + turn, py * 4, seed + 2, 4) - 0.5, bumpY = Fbm(px * 4, py * 4 - turn, seed + 6, 4) - 0.5;
                    double lit = Math.Max(0, (px / outline + bumpX * 0.8) * -0.6 + (py / outline + bumpY * 0.8) * -0.5 + z * 0.62);
                    double crater = Fbm(px * 7 + turn * 2, py * 7, seed + 11, 3);
                    double shade = (0.25 + 0.95 * lit) * (crater > 0.66 ? 0.62 : 1);
                    double edge = Math.Min(1, (outline - r) * half * 0.9);
                    pixels[y * size + x] = Pixel(255 * edge, 118 * shade + 10, 108 * shade + 9, 98 * shade + 8);
                }
            return FromPixels(size, size, pixels);
        }

        /// <summary>
        /// A comet heading along <paramref name="heading"/> with its tails streaming away from the sun direction:
        /// a broad curved yellow-white dust tail and a straight, narrow blue ion tail. The head sits at the origin
        /// returned in <paramref name="head"/>.
        /// </summary>
        internal static Bitmap Comet(int length, double tailAngle, int seed, out Point head)
        {
            int size = length * 2 + 40;
            var pixels = new int[size * size];
            double cx = size * 0.5, cy = size * 0.5, ux = Math.Cos(tailAngle), uy = Math.Sin(tailAngle);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    double px = x + 0.5 - cx, py = y + 0.5 - cy;
                    double along = (px * ux + py * uy) / length, across = (-px * uy + py * ux) / length;
                    double coma = Math.Exp(-(px * px + py * py) / (length * length * 0.0009));
                    double ion = 0, dust = 0;
                    if (along > 0 && along < 1)
                    {
                        double streak = 0.6 + 0.4 * Fbm(along * 30, across * 120, seed, 3);
                        ion = Math.Exp(-across * across / (0.0004 + along * 0.0012)) * (1 - along) * streak;
                        double bend = across - along * along * 0.22;
                        dust = Math.Exp(-bend * bend / (0.002 + along * 0.02)) * Math.Pow(1 - along, 1.5);
                    }
                    double a = Math.Min(255, 255 * coma + 150 * ion + 120 * dust);
                    if (a < 1) continue;
                    double total = coma + ion + dust + 1e-6;
                    pixels[y * size + x] = Pixel(a, (255 * coma + 150 * ion + 250 * dust) / total,
                        (255 * coma + 200 * ion + 232 * dust) / total, (255 * coma + 255 * ion + 190 * dust) / total);
                }
            head = new Point((int)cx, (int)cy);
            return FromPixels(size, size, pixels);
        }

        // ---------- pixels ----------

        private static int Pixel(double alpha, double r, double g, double b)
        {
            int a = Math.Max(0, Math.Min(255, (int)(alpha + 0.5)));
            return (a << 24) | (Channel(r, a) << 16) | (Channel(g, a) << 8) | Channel(b, a);
        }

        private static int Channel(double value, int alpha)
        {
            return (int)(Math.Max(0, Math.Min(255, value)) * alpha / 255);
        }

        private static Bitmap FromPixels(int width, int height, int[] pixels)
        {
            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, bitmap.PixelFormat);
            try { Marshal.Copy(pixels, 0, data.Scan0, pixels.Length); }
            finally { bitmap.UnlockBits(data); }
            return bitmap;
        }
    }
}
