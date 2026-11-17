using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace SnowDesk
{
    internal sealed class TreeTarget : Forms.NativeWindow, IDisposable
    {
        private static readonly IntPtr Topmost = new IntPtr(-1);
        private static readonly PointF[] Shape = DecorationArt.TreeSilhouette;
        private readonly Action<Desktop.Point> pressed;
        private readonly Point[] vertices = new Point[Shape.Length];
        private Rectangle bounds;
        private int maskHeight;
        private bool shown;

        internal TreeTarget(IntPtr owner, Action<Desktop.Point> pressed)
        {
            this.pressed = pressed;
            var parameters = new Forms.CreateParams
            {
                Caption = "SnowDesk Tree Input",
                Parent = owner,
                Style = unchecked((int)0x80000000),
                ExStyle = 0x08080088
            };
            CreateHandle(parameters);
            SetLayeredWindowAttributes(Handle, 0, 1, 2);
        }

        internal void Place(Rectangle next, int visibleHeight = int.MaxValue)
        {
            visibleHeight = Math.Min(visibleHeight, next.Height);
            if (next.Width < 2 || visibleHeight < 2)
            {
                Hide();
                return;
            }
            if (bounds.Size != next.Size || maskHeight != visibleHeight)
            {
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i].X = (int)Math.Round((Shape[i].X / DecorationArt.TreeWidth + 0.5) * next.Width);
                    vertices[i].Y = (int)Math.Round((Shape[i].Y / DecorationArt.TreeHeight + 1) * next.Height);
                }
                IntPtr mask = CreatePolygonRgn(vertices, vertices.Length, 2);
                if (mask == IntPtr.Zero) return;
                if (visibleHeight < next.Height)
                {
                    IntPtr cut = CreateRectRgn(0, 0, next.Width, visibleHeight);
                    const int Intersect = 1;
                    int clipped = cut == IntPtr.Zero ? 0 : CombineRgn(mask, mask, cut, Intersect);
                    if (cut != IntPtr.Zero) DeleteObject(cut);
                    if (clipped == 0) { DeleteObject(mask); return; }
                }
                if (SetWindowRgn(Handle, mask, true) == 0) { DeleteObject(mask); return; }
                maskHeight = visibleHeight;
            }
            if (!shown || next != bounds)
            {
                bounds = next;
                const uint ShowWithoutActivation = 0x0050;
                shown = SetWindowPos(Handle, Topmost, bounds.X, bounds.Y, bounds.Width, bounds.Height, ShowWithoutActivation);
            }
        }

        private void Hide()
        {
            if (!shown) return;
            ShowWindow(Handle, 0);
            shown = false;
        }

        protected override void WndProc(ref Forms.Message message)
        {
            const int MouseActivate = 0x0021, SetCursorMessage = 0x0020, LeftDown = 0x0201;
            if (message.Msg == MouseActivate) { message.Result = new IntPtr(3); return; }
            if (message.Msg == SetCursorMessage)
            {
                SetCursor(Forms.Cursors.Hand.Handle);
                message.Result = new IntPtr(1);
                return;
            }
            if (message.Msg == LeftDown)
            {
                long location = message.LParam.ToInt64();
                pressed(new Desktop.Point {
                    X = bounds.X + unchecked((short)location),
                    Y = bounds.Y + unchecked((short)(location >> 16))
                });
                return;
            }
            base.WndProc(ref message);
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero) DestroyHandle();
        }

        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr window, uint key, byte alpha, uint flags);
        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr window, IntPtr region, bool redraw);
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")]
        internal static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int w, int h, uint flags);
        [DllImport("user32.dll")]
        private static extern IntPtr SetCursor(IntPtr cursor);
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreatePolygonRgn([In] Point[] points, int count, int mode);
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
        [DllImport("gdi32.dll")]
        private static extern int CombineRgn(IntPtr result, IntPtr first, IntPtr second, int mode);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr handle);
    }
}
