using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SnowDesk
{
    internal static class SpaceArt
    {
        private static readonly Lazy<Bitmap> BluePlanet = new Lazy<Bitmap>(() => BuildPlanet(false));
        private static readonly Lazy<Bitmap> RosePlanet = new Lazy<Bitmap>(() => BuildPlanet(true));
        private static readonly Lazy<Bitmap> Nebula = new Lazy<Bitmap>(BuildNebula);

        private static readonly Brush Bus = DecorationArt.Ramp(-9, 10, Color.FromArgb(229, 234, 238), Color.FromArgb(114, 128, 145));
        private static readonly Brush BusSide = new SolidBrush(Color.FromArgb(192, 157, 92));
        private static readonly Brush Dish = DecorationArt.Ramp(-21, 1, Color.FromArgb(223, 234, 243), Color.FromArgb(110, 131, 150));
        private static readonly Brush SolarCells = DecorationArt.Ramp(-5, 7, Color.FromArgb(49, 93, 155), Color.FromArgb(25, 46, 88));
        private static readonly Brush Pearl = new SolidBrush(Color.FromArgb(245, 220, 238, 253));
        private static readonly Pen Outline = new Pen(Color.FromArgb(232, 19, 29, 44), 0.85f);
        private static readonly Pen Rails = new Pen(Color.FromArgb(171, 147, 180, 219), 0.62f);
        private static readonly Pen Highlight = new Pen(Color.FromArgb(236, 240, 249, 255), 0.9f)
        {
            StartCap = LineCap.Round, EndCap = LineCap.Round
        };
        private static readonly GraphicsPath BusShape = DecorationArt.RoundedPolygon(new[]
        {
            new PointF(-7, -8), new PointF(5, -9), new PointF(10, -5), new PointF(9, 7),
            new PointF(4, 11), new PointF(-7, 9), new PointF(-10, 4), new PointF(-10, -5)
        }, 0.16f);
        private static readonly GraphicsPath DishShape = CreateDish();

        private static GraphicsPath CreateDish()
        {
            var path = new GraphicsPath();
            path.AddBezier(-5, -5, -4, -12, 5, -18, 12, -15);
            path.AddBezier(12, -15, 12, -8, 5, -2, -5, -5);
            path.CloseFigure();
            return path;
        }

        internal sealed class SkySprite : IDisposable
        {
            private readonly Bitmap image;
            private readonly Rectangle position;

            internal SkySprite(Bitmap source, Rectangle target, Rectangle visible, bool mirror)
            {
                position = Rectangle.Intersect(target, visible);
                if (position.Width == 0 || position.Height == 0) return;
                image = new Bitmap(position.Width, position.Height, PixelFormat.Format32bppPArgb);
                using (Graphics graphics = Graphics.FromImage(image))
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    target.Offset(-position.Left, -position.Top);
                    if (mirror)
                    {
                        graphics.TranslateTransform(target.Left + target.Right, 0);
                        graphics.ScaleTransform(-1, 1);
                    }
                    graphics.DrawImage(source, target);
                }
            }

            internal void Draw(Graphics g, float alpha, int lateralShift = 0)
            {
                if (image == null) return;
                if (alpha >= 0.999f)
                {
                    g.DrawImageUnscaled(image, position.X + lateralShift, position.Y);
                    return;
                }
                using (var fade = new ImageAttributes())
                {
                    fade.SetColorMatrix(new ColorMatrix { Matrix33 = alpha });
                    Rectangle target = position;
                    target.Offset(lateralShift, 0);
                    g.DrawImage(image, target, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, fade);
                }
            }

            public void Dispose()
            {
                if (image != null) image.Dispose();
            }
        }

        internal static SkySprite PreparePlanet(float x, float y, float radius, bool deep, Rectangle view, int drift)
        {
            view.Inflate(drift, 0);
            return new SkySprite(deep ? RosePlanet.Value : BluePlanet.Value,
                Rectangle.Round(new RectangleF(x - radius, y - radius, radius * 2, radius * 2)), view, deep);
        }

        internal static SkySprite PrepareCloud(Rectangle view)
        {
            RectangleF target = new RectangleF(view.Width * 0.22f, view.Height * 0.015f, view.Width * 0.71f, view.Height * 0.51f);
            return new SkySprite(Nebula.Value, Rectangle.Round(target), view, false);
        }

        internal static void Aurora(Graphics g, float x, float y, float radius, bool mirrored, double lift, double alpha)
        {
            GraphicsState state = g.Save();
            try
            {
                g.TranslateTransform(x, y);
                g.ScaleTransform(mirrored ? -radius : radius, radius);
                for (int i = 2; i >= 0; i--)
                {
                    float width = 0.004f + i * 0.008f;
                    int opacity = (int)((i == 0 ? 122 : i == 1 ? 63 : 21) * alpha);
                    using (var light = new Pen(Color.FromArgb(opacity, 122 + i * 13, 239, 215), width))
                        g.DrawBezier(light, -0.80f, -0.57f, -0.66f, -0.85f - (float)lift * 0.038f,
                            -0.37f, -0.98f, -0.16f, -0.977f);
                }
            }
            finally { g.Restore(state); }
        }

        internal static void Satellite(Graphics g, float x, float y, float scale, float pitch, bool mirrored, double glint)
        {
            GraphicsState state = g.Save();
            try
            {
                g.TranslateTransform(x, y);
                g.RotateTransform(pitch);
                g.ScaleTransform(mirrored ? -scale : scale, scale);
                g.DrawLine(Outline, -31, 1, 31, 1);
                Wing(g, -38);
                Wing(g, 15);
                g.DrawLine(Highlight, -15, 1, 15, 1);
                g.FillPath(Bus, BusShape);
                g.DrawPath(Outline, BusShape);
                g.FillRectangle(BusSide, -7, 1, 12, 5);
                g.DrawLine(Rails, -7, -1, 7, -1);
                g.DrawLine(Outline, 4, -5, 13, -13);
                g.FillPath(Dish, DishShape);
                g.DrawPath(Outline, DishShape);
                g.DrawLine(Highlight, 1, -6, 10, -13);
                g.FillEllipse(Pearl, 8.7f, -14.4f, 2.1f, 2.1f);
                g.DrawLine(Outline, -6, 7, -11, 15);
                if (glint > 0.04)
                {
                    float spread = (float)(2.2 + glint * 4.2);
                    using (var shine = new Pen(Color.FromArgb((int)(glint * 184), 230, 249, 255), 1.1f))
                    {
                        g.DrawLine(shine, 27 - spread, -4, 27 + spread, -4);
                        g.DrawLine(shine, 27, -4 - spread * 0.55f, 27, -4 + spread * 0.55f);
                    }
                }
            }
            finally { g.Restore(state); }
        }

        private static void Wing(Graphics g, float x)
        {
            g.FillRectangle(SolarCells, x, -5, 23, 12);
            g.DrawRectangle(Outline, x, -5, 23, 12);
            g.DrawLine(Rails, x + 1, 0.8f, x + 21, 0.8f);
            for (int cell = 1; cell < 5; cell++)
                g.DrawLine(Rails, x + cell * 4.6f, -4, x + cell * 4.6f, 6);
            g.DrawLine(Highlight, x + 1, -4.5f, x + 21, -4.5f);
        }

        internal static void Meteor(Graphics g, float x, float y, float dx, float dy, double brightness)
        {
            using (var trail = new LinearGradientBrush(new PointF(x - dx, y - dy), new PointF(x, y),
                Color.FromArgb(0, 131, 160, 232), Color.FromArgb((int)(207 * brightness), 208, 232, 255)))
            using (var pen = new Pen(trail, 1.4f) { EndCap = LineCap.Round })
                g.DrawLine(pen, x - dx, y - dy, x, y);
            if (brightness >= 0.43) g.FillEllipse(Pearl, x - 0.85f, y - 0.85f, 1.7f, 1.7f);
        }

        private static Bitmap BuildPlanet(bool deep)
        {
            const int size = 352;
            var pixels = new int[size * size];
            for (int row = 0; row < size; row++)
            {
                double y = (row + 0.5) * (2.0 / size) - 1;
                for (int column = 0; column < size; column++)
                {
                    double x = (column + 0.5) * (2.0 / size) - 1;
                    double distance = x * x + y * y;
                    if (distance >= 1) continue;
                    double z = Math.Sqrt(1 - distance);
                    double solar = -0.725 * x - 0.49 * y - 0.49 * z;
                    double light = Math.Sqrt(Math.Max(0, solar));
                    double longitude = x / (0.50 + 0.50 * z);
                    double belts = Math.Sin(y * 19 + longitude * 1.5 + Math.Sin(longitude * 6.2) * 0.6);
                    double clouds = Math.Sin(9 * y - 3.2 * longitude + Math.Sin(7 * longitude)) *
                        Math.Sin(16 * y + longitude * 4.8);
                    double grain = Math.Sin(49 * y + 24 * longitude) * Math.Cos(37 * longitude - 21 * y);
                    double texture = 0.72 + 0.15 * belts + 0.14 * clouds + 0.024 * grain;
                    double limb = Math.Exp(-z * 19) * (0.19 + Math.Max(0, solar) * 0.81);
                    double coverage = Math.Min(1, (1 - distance) * size * 0.21);
                    double warm = light * texture;
                    pixels[row * size + column] = Pixel((18 + 235 * light + limb * 38) * coverage,
                        14 + warm * (deep ? 206 : 145) + limb * 93,
                        23 + warm * (deep ? 142 : 182) + limb * 113,
                        41 + warm * (deep ? 179 : 209) + limb * 119);
                }
            }
            return BitmapFromPixels(size, size, pixels);
        }

        private static Bitmap BuildNebula()
        {
            const int width = 320, height = 144;
            var pixels = new int[width * height];
            for (int row = 0; row < height; row++)
                for (int col = 0; col < width; col++)
                {
                    double x = (col + 0.5) * (2.0 / width) - 1;
                    double y = (row + 0.5) * (2.0 / height) - 1;
                    double axis = y + x * 0.43 + 0.12 * Math.Sin(x * 5);
                    double envelope = Math.Max(0, 1 - x * x) * Math.Max(0, 1 - y * y);
                    double body = Math.Exp(-axis * axis * 5.6) * envelope;
                    double fold = Math.Max(0, 0.58 + 0.24 * Math.Sin(x * 9 + y * 4) +
                        0.13 * Math.Sin(x * 21 - y * 12) + 0.07 * Math.Cos(x * 39 + y * 17));
                    double cool = 0.5 + 0.5 * Math.Sin(x * 2.5 + y * 3.1);
                    pixels[row * width + col] = Pixel(body * fold * 83,
                        118 - 25 * cool, 62 + 55 * cool, 157 + 41 * cool);
                }
            return BitmapFromPixels(width, height, pixels);
        }

        private static int Pixel(double alpha, double r, double g, double b)
        {
            int a = Math.Min(255, (int)(alpha + 0.5));
            return (a << 24) | ((int)(Math.Min(255, r) * a / 255) << 16) |
                ((int)(Math.Min(255, g) * a / 255) << 8) | (int)(Math.Min(255, b) * a / 255);
        }

        private static Bitmap BitmapFromPixels(int width, int height, int[] pixels)
        {
            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, bitmap.PixelFormat);
            try { Marshal.Copy(pixels, 0, data.Scan0, pixels.Length); }
            finally { bitmap.UnlockBits(data); }
            return bitmap;
        }
    }
}
