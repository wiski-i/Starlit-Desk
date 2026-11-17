using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using Point = System.Windows.Point;

namespace SnowDesk
{
    internal sealed class Canvas : IDisposable
    {
        private readonly Graphics graphics;
        private readonly Action<int, int> clearPixels;
        private static readonly GraphicsPath IceShape = CreateIceShape();
        private static readonly Brush IceGlass = CreateIceGlass();
        private static readonly GraphicsPath IceFacet = CreateIceFacet();
        private static readonly Brush IceGlint = new SolidBrush(Color.FromArgb(155, 233, 249, 255));
        private static readonly Brush IceRim = new SolidBrush(Color.FromArgb(176, 204, 229, 242));
        private static readonly GraphicsPath SnowHeap = CreateSnowHeap();
        private static readonly Brush HeapPowder = new LinearGradientBrush(new PointF(0, -1.01f),
            new PointF(0, 0.02f), Color.FromArgb(252, 250, 253, 255), Color.FromArgb(238, 174, 199, 218));
        private static readonly Brush HeapRidge = new SolidBrush(Color.FromArgb(153, 252, 255, 255));
        private static readonly GraphicsPath SnowRidge = CreateSnowRidge();

        internal Canvas(Bitmap surface, Action<int, int> clearPixels = null)
        {
            this.clearPixels = clearPixels;
            graphics = Graphics.FromImage(surface);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
        }

        internal void BeginFrame(int width, int height)
        {
            var viewport = new Rectangle(0, 0, width, height);
            graphics.SetClip(viewport, CombineMode.Replace);
            if (clearPixels != null) clearPixels(width, height);
            else
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.FillRectangle(Brushes.Transparent, viewport);
                graphics.CompositingMode = CompositingMode.SourceOver;
            }
        }
        internal void Flush() { graphics.Flush(FlushIntention.Sync); }
        public void Dispose() { graphics.Dispose(); }

        internal void DrawEllipse(Brush brush, Pen pen, Point center, double rx, double ry)
        {
            var rect = new RectangleF((float)(center.X - rx), (float)(center.Y - ry), (float)(2 * rx), (float)(2 * ry));
            graphics.FillEllipse(brush, rect);
            if (pen != null) graphics.DrawEllipse(pen, rect);
        }

        internal void DrawSnowLayer(Bitmap layer, int x, int y)
        {
            graphics.DrawImageUnscaled(layer, x, y);
        }

        private static GraphicsPath CreateSnowHeap()
        {
            var path = new GraphicsPath();
            path.AddBezier(-0.10f, 0, -0.16f, -0.27f, -0.05f, -0.78f, 0.19f, -0.91f);
            path.AddBezier(0.19f, -0.91f, 0.31f, -1.04f, 0.45f, -0.91f, 0.54f, -0.71f);
            path.AddBezier(0.54f, -0.71f, 0.69f, -0.66f, 0.80f, -0.18f, 1, 0);
            path.CloseFigure();
            return path;
        }

        private static GraphicsPath CreateSnowRidge()
        {
            var path = new GraphicsPath();
            path.AddBezier(-0.04f, -0.56f, 0.19f, -0.98f, 0.38f, -1.02f, 0.52f, -0.73f);
            path.AddBezier(0.52f, -0.73f, 0.30f, -0.91f, 0.17f, -0.62f, -0.04f, -0.56f);
            path.CloseFigure();
            return path;
        }

        internal void DrawSnowHeap(double x, double y, double height, double width)
        {
            if (width <= 0.45) return;
            GraphicsState state = graphics.Save();
            try
            {
                graphics.TranslateTransform((float)x, (float)y);
                graphics.ScaleTransform((float)width, (float)height);
                graphics.FillPath(HeapPowder, SnowHeap);
                graphics.FillPath(HeapRidge, SnowRidge);
            }
            finally { graphics.Restore(state); }
        }

        internal void DrawCrystal(Pen ice, double x, double y, double radius, double angle)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                double phase = angle + axis * Math.PI / 3;
                float dx = (float)(Math.Cos(phase) * radius);
                float dy = (float)(Math.Sin(phase) * radius);
                graphics.DrawLine(ice, (float)x - dx, (float)y - dy, (float)x + dx, (float)y + dy);
            }
        }

        internal void DrawLine(Pen pen, Point start, Point end)
        {
            graphics.DrawLine(pen, (float)start.X, (float)start.Y, (float)end.X, (float)end.Y);
        }

        internal void DrawIcicle(double x, double y, double length, double halfWidth, double angle)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.TranslateTransform((float)x, (float)y);
                if (Math.Abs(angle) > 0.01) graphics.RotateTransform((float)angle);
                graphics.ScaleTransform((float)halfWidth, (float)length);
                graphics.FillPath(IceGlass, IceShape);
                graphics.FillPath(IceGlint, IceFacet);
                graphics.FillEllipse(IceRim, -0.98f, -0.045f, 1.95f, 0.16f);
            }
            finally { graphics.Restore(state); }
        }

        private static GraphicsPath CreateIceShape()
        {
            var shape = new GraphicsPath();
            shape.AddBezier(-1.04f, 0.035f, -0.61f, -0.105f, 0.75f, -0.067f, 1.04f, 0.064f);
            shape.AddBezier(1.04f, 0.064f, 0.89f, 0.28f, 0.61f, 0.59f, 0.16f, 0.98f);
            shape.AddBezier(0.16f, 0.98f, 0.08f, 1.028f, 0.02f, 1, -0.05f, 0.94f);
            shape.AddBezier(-0.05f, 0.94f, -0.32f, 0.78f, -0.34f, 0.51f, -0.57f, 0.35f);
            shape.AddBezier(-0.57f, 0.35f, -0.87f, 0.25f, -0.81f, 0.14f, -1.04f, 0.035f);
            shape.CloseFigure();
            return shape;
        }

        private static GraphicsPath CreateIceFacet()
        {
            var facet = new GraphicsPath();
            facet.AddPolygon(new[]
            {
                new PointF(-0.47f, 0.07f), new PointF(-0.10f, 0.12f),
                new PointF(0.18f, 0.77f), new PointF(-0.14f, 0.48f)
            });
            return facet;
        }

        private static Brush CreateIceGlass()
        {
            return new LinearGradientBrush(new RectangleF(-1, 0, 2, 1), Color.White, Color.White, LinearGradientMode.Horizontal)
            {
                WrapMode = WrapMode.TileFlipX,
                InterpolationColors = new ColorBlend
                {
                    Colors = new[]
                    {
                        Color.FromArgb(142, 105, 151, 176),
                        Color.FromArgb(231, 213, 235, 248),
                        Color.FromArgb(153, 134, 179, 202)
                    },
                    Positions = new[] { 0f, 0.39f, 1f }
                }
            };
        }

        internal void DrawDecoration(Effect kind, double x, double y, double scale, double angle,
            double phase, double pivotOffset, double visibleBottom = double.PositiveInfinity)
        {
            GraphicsState state = graphics.Save();
            try
            {
                if (!double.IsPositiveInfinity(visibleBottom))
                {
                    RectangleF clip = graphics.VisibleClipBounds;
                    clip.Height = Math.Max(0, Math.Min(clip.Bottom, (float)visibleBottom) - clip.Top);
                    graphics.IntersectClip(clip);
                }
                graphics.TranslateTransform((float)x, (float)y);
                if (Math.Abs(angle) > 0.02) graphics.RotateTransform((float)angle);
                graphics.ScaleTransform((float)scale, (float)scale);
                if (pivotOffset != 0) graphics.TranslateTransform(0, (float)pivotOffset);
                DecorationArt.Draw(graphics, kind, phase);
            }
            finally { graphics.Restore(state); }
        }
    }
}
