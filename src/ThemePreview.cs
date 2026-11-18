using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SnowDesk
{
    internal abstract class ThemePreview : IDisposable
    {
        internal abstract void Advance(double seconds, Settings settings);
        internal abstract void Draw(Graphics graphics, Rectangle destination);
        public abstract void Dispose();

        internal static bool WindowsAllowsAnimation()
        {
            const uint GetClientAreaAnimation = 0x1042;
            int enabled;
            return SystemParametersInfo(GetClientAreaAnimation, 0, out enabled, 0) && enabled != 0;
        }

        [DllImport("user32.dll")]
        private static extern bool SystemParametersInfo(uint action, uint parameter, out int value, uint flags);
    }

    internal sealed class WinterPreview : ThemePreview
    {
        private static readonly Rectangle Viewport = new Rectangle(0, 0, 640, 220);
        private static readonly Rectangle FrontWindow = new Rectangle(230, 82, 257, 112);
        private static readonly Rectangle BackWindow = new Rectangle(353, 41, 221, 99);
        private static readonly List<Desktop.WindowBounds> WindowEdges = new List<Desktop.WindowBounds>
        {
            WindowEdge(1, FrontWindow), WindowEdge(2, BackWindow)
        };
        private static readonly List<Desktop.WindowBounds> NoWindows = new List<Desktop.WindowBounds>();

        private readonly SnowField snow = new SnowField(Viewport);
        private readonly Bitmap backdrop = CreateBackdrop();
        private readonly Bitmap frame = new Bitmap(Viewport.Width, Viewport.Height, PixelFormat.Format32bppPArgb);
        private readonly Canvas canvas;
        private bool ledgesEnabled;

        internal WinterPreview()
        {
            canvas = new Canvas(frame);
            canvas.BeginFrame(Viewport.Width, Viewport.Height);
            canvas.DrawSnowLayer(backdrop, 0, 0);
            canvas.Flush();
        }

        internal override void Advance(double seconds, Settings settings)
        {
            if (ledgesEnabled != settings.WindowLedges)
            {
                ledgesEnabled = settings.WindowLedges;
                snow.UpdateLedges(ledgesEnabled ? WindowEdges : NoWindows);
            }
            snow.Advance(seconds, settings, default(Desktop.Point));
            // The opaque backdrop replaces the previous frame in the same buffer.
            canvas.DrawSnowLayer(backdrop, 0, 0);
            snow.Draw(canvas);
            canvas.Flush();
        }

        internal override void Draw(Graphics graphics, Rectangle destination)
        {
            graphics.DrawImage(frame, destination);
        }

        private static Desktop.WindowBounds WindowEdge(int id, Rectangle bounds)
        {
            return new Desktop.WindowBounds
            {
                Handle = new IntPtr(id),
                Bounds = new System.Windows.Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height),
                CornerRadius = 12
            };
        }

        private static Bitmap CreateBackdrop()
        {
            var bitmap = new Bitmap(Viewport.Width, Viewport.Height, PixelFormat.Format32bppPArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var gradient = new LinearGradientBrush(Viewport,
                Color.FromArgb(41, 22, 36), Color.FromArgb(70, 42, 68), LinearGradientMode.Horizontal))
            using (var orbit = new Pen(Color.FromArgb(42, DeskStyle.Accent)))
            using (var star = new SolidBrush(Color.FromArgb(198, DeskStyle.Accent)))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.FillRectangle(gradient, Viewport);
                graphics.DrawEllipse(orbit, 156, 27, 464, 174);
                graphics.DrawEllipse(orbit, 189, 49, 413, 135);
                DrawWindow(graphics, BackWindow, Color.FromArgb(63, 39, 65));
                DrawWindow(graphics, FrontWindow, Color.FromArgb(49, 30, 52));
                graphics.FillPolygon(star, new[]
                {
                    new Point(593, 42), new Point(596, 52), new Point(605, 56), new Point(596, 60),
                    new Point(593, 70), new Point(590, 60), new Point(581, 56), new Point(590, 52)
                });
            }
            return bitmap;
        }

        private static void DrawWindow(Graphics graphics, Rectangle bounds, Color color)
        {
            using (GraphicsPath shape = DeskStyle.Rounded(bounds, 12))
            using (var body = new SolidBrush(color))
            using (var rim = new Pen(Color.FromArgb(91, DeskStyle.Muted), 1.5f))
            using (var content = new SolidBrush(Color.FromArgb(34, DeskStyle.Muted)))
            {
                graphics.FillPath(body, shape);
                graphics.DrawPath(rim, shape);
                graphics.DrawLine(rim, bounds.Left + 2, bounds.Top + 24, bounds.Right - 2, bounds.Top + 24);
                for (int i = 0; i < 3; i++) graphics.FillEllipse(content, bounds.Left + 13 + i * 10, bounds.Top + 10, 5, 5);
                graphics.FillRectangle(content, bounds.Left + 14, bounds.Top + 37, 55, bounds.Height - 51);
                for (int i = 0; i < 3; i++)
                    graphics.FillRectangle(content, bounds.Left + 84, bounds.Top + 39 + i * 15, bounds.Width - 108 - i * 18, 3);
            }
        }

        public override void Dispose()
        {
            canvas.Dispose();
            frame.Dispose();
            backdrop.Dispose();
            snow.Dispose();
        }
    }
}
