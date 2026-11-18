using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace SnowDesk
{
    internal sealed class Overlay
    {
        private const int Layered = 0x00080000, ClickThrough = 0x20, ToolWindow = 0x80;
        private const int NoActivate = 0x08000000, Topmost = 0x08;
        private readonly Forms.Screen screen;
        private IntPtr window;
        private IntPtr raisedPopup;
        private bool wasOnDesktop;
        private TreeTarget treeTarget;

        internal DesktopScene Scene { get; private set; }
        internal SnowField Snow { get { return Scene as SnowField; } }
        internal string DisplayName { get { return screen.DeviceName; } }
        internal bool IsVisible { get; private set; }
        internal Rectangle Bounds { get { return screen.Bounds; } }

        internal Overlay(Forms.Screen display, DesktopTheme theme)
        {
            screen = display;
            Scene = theme.CreateScene(display);
        }

        internal void Reveal(FrameSurface frame, Rectangle dashboard)
        {
            if (IsVisible) return;
            if (window == IntPtr.Zero) Open(frame, dashboard);
            ShowWindow(window, 4); // SW_SHOWNOACTIVATE.
            IsVisible = true;
        }

        private void Open(FrameSurface frame, Rectangle dashboard)
        {
            Rectangle bounds = screen.Bounds;
            try
            {
                const uint Popup = 0x80000000;
                window = CreateWindowEx(Layered | ClickThrough | ToolWindow | NoActivate | Topmost,
                    "Static", "Starlit Desk Overlay", Popup, bounds.X, bounds.Y, bounds.Width, bounds.Height,
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (window == IntPtr.Zero) throw new Win32Exception();
                RenderFrame(frame, dashboard);
            }
            catch
            {
                Close();
                throw;
            }
        }

        private void Hide()
        {
            if (!IsVisible) return;
            ShowWindow(window, 0);
            IsVisible = false;
        }

        internal void Close()
        {
            Hide();
            if (window == IntPtr.Zero) return;
            if (treeTarget != null)
            {
                treeTarget.Dispose();
                treeTarget = null;
            }
            DestroyWindow(window);
            window = IntPtr.Zero;
            raisedPopup = IntPtr.Zero;
            wasOnDesktop = false;
        }

        internal bool Contains(Desktop.Point point) { return screen.Bounds.Contains(point.X, point.Y); }

        internal void AlignWithShell(bool onDesktop, IntPtr popup)
        {
            if (window != IntPtr.Zero && ((onDesktop && !wasOnDesktop) || (popup != IntPtr.Zero && raisedPopup != popup)))
                TreeTarget.SetWindowPos(window, new IntPtr(-1), 0, 0, 0, 0, 0x0013);
            raisedPopup = popup;
            wasOnDesktop = onDesktop;
        }

        internal void UpdateTreeTarget(Rectangle dashboard)
        {
            System.Windows.Rect grip;
            double visibleBottom;
            if (Snow == null || !Snow.TryTreeBounds(out grip, out visibleBottom))
            {
                if (treeTarget != null) treeTarget.Place(Rectangle.Empty);
                return;
            }
            var bounds = new Rectangle((int)Math.Round(grip.X), (int)Math.Round(grip.Y),
                (int)Math.Round(grip.Width), (int)Math.Round(grip.Height));
            if (bounds.IntersectsWith(dashboard))
            {
                if (treeTarget != null) treeTarget.Place(Rectangle.Empty);
                return;
            }
            if (treeTarget == null)
                treeTarget = new TreeTarget(window, Snow.GrabTree);
            treeTarget.Place(bounds, (int)Math.Ceiling(visibleBottom) - bounds.Top);
        }

        internal void RenderFrame(FrameSurface frame, Rectangle dashboard)
        {
            Rectangle bounds = screen.Bounds;
            frame.Canvas.BeginFrame(bounds.Width, bounds.Height);
            Rectangle blocked = Rectangle.Intersect(bounds, dashboard);
            if (blocked.Width > 0 && blocked.Height > 0)
            {
                blocked.Offset(-bounds.X, -bounds.Y);
                frame.Canvas.Exclude(blocked);
            }
            Scene.Draw(frame.Canvas);
            frame.Canvas.Flush();
            var destination = new Desktop.Point { X = bounds.X, Y = bounds.Y };
            var extent = new Desktop.Point { X = bounds.Width, Y = bounds.Height };
            var source = new Desktop.Point();
            var blend = new Blend { Opacity = 255, AlphaFormat = 1 };
            if (!UpdateLayeredWindow(window, IntPtr.Zero, ref destination, ref extent, frame.DeviceContext, ref source,
                0, ref blend, 2))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Blend
        {
            internal byte Operation, Flags, Opacity, AlphaFormat;
        }

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hwnd, int command);
        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(int exStyle, string className, string title, uint style,
            int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr screen, ref Desktop.Point destination,
            ref Desktop.Point size, IntPtr source, ref Desktop.Point sourcePoint, int colorKey, ref Blend blend, int flags);
    }
}
