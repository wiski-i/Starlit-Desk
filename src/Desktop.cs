using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Forms = System.Windows.Forms;

namespace SnowDesk
{
    internal static class Desktop
    {
        internal delegate void EventHandler(IntPtr hook, uint type, IntPtr hwnd, int obj, int child, uint thread, uint time);
        private delegate bool EnumCallback(IntPtr hwnd, IntPtr data);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Point
        {
            internal int X, Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            internal int Left, Top, Right, Bottom;
        }

        internal struct WindowBounds
        {
            internal IntPtr Handle;
            internal Rect Bounds;
            internal double CornerRadius;
            internal bool IsShellPopup;
        }

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumCallback callback, IntPtr data);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hwnd, uint relationship);

        internal static bool IsTopLevel(IntPtr hwnd)
        {
            const uint Root = 2;
            return GetAncestor(hwnd, Root) == hwnd;
        }
        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
        [DllImport("user32.dll")]
        private static extern int GetWindowRgnBox(IntPtr hwnd, out NativeRect rect);
        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hwnd, out NativeRect rect);
        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hwnd, ref Point point);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();
        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        internal static extern bool GetCursorPos(out Point point);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        internal static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")]
        internal static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, EventHandler handler,
            uint process, uint thread, uint flags);
        [DllImport("user32.dll")]
        internal static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
        private static extern int DwmBounds(IntPtr hwnd, int attribute, out NativeRect rect, int length);
        [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
        private static extern int DwmFlags(IntPtr hwnd, int attribute, out int flags, int length);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hwnd, StringBuilder windowClass, int capacity);
        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string windowClass, string windowTitle);
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")]
        private static extern bool SystemParametersInfo(uint action, uint parameter, out int value, uint update);

        internal static bool PrimaryButtonDown()
        {
            int key = GetSystemMetrics(23) != 0 ? 2 : 1; // SM_SWAPBUTTON.
            return (GetAsyncKeyState(key) & 0x8000) != 0;
        }

        internal static bool DecorativeMotionEnabled()
        {
            int value;
            return !SystemParametersInfo(0x1042, 0, out value, 0) || value != 0;
        }

        private static string ClassName(IntPtr hwnd, StringBuilder buffer)
        {
            buffer.Clear();
            GetClassName(hwnd, buffer, buffer.Capacity);
            return buffer.ToString();
        }

        internal static bool MayBeStartFocus(IntPtr hwnd)
        {
            IntPtr root = GetAncestor(hwnd, 2);
            return ShellSurfaces.CouldHostStart(ClassName(root == IntPtr.Zero ? hwnd : root, new StringBuilder(260)));
        }

        private static double WindowScale(IntPtr hwnd)
        {
            try
            {
                uint dpi = GetDpiForWindow(hwnd);
                return dpi == 0 ? 1 : dpi / 96.0;
            }
            catch (EntryPointNotFoundException) { return 1; }
        }

        internal static List<WindowBounds> ReadWindows(Action shellChanged = null, IntPtr shellFocus = default(IntPtr))
        {
            var result = new List<WindowBounds>();
            var buffer = new StringBuilder(260);
            IntPtr foreground = GetAncestor(GetForegroundWindow(), 2);
            IntPtr desktop = GetShellWindow();
            uint ownProcess = GetCurrentProcessId();
            bool sawStart = false;
            Func<IntPtr, bool> addWindow = delegate(IntPtr hwnd)
            {
                int cloaked;
                if (hwnd == IntPtr.Zero || hwnd == desktop || !IsWindowVisible(hwnd) || IsIconic(hwnd) ||
                    (DwmFlags(hwnd, 14, out cloaked, 4) == 0 && cloaked != 0))
                    return false;

                string name = ClassName(hwnd, buffer);
                if (ShellSurfaces.IsDesktop(name)) return false;
                bool taskbar = ShellSurfaces.IsTaskbar(name);
                bool overflow = ShellSurfaces.IsOverflow(name);
                bool start = !taskbar && !overflow && ShellSurfaces.IsStart(hwnd, name, hwnd == foreground);
                bool nativeMenu = name == "#32768" || name == "ComboLBox";
                bool flyout = ShellSurfaces.IsFlyout(hwnd, name);
                bool toolPopup = flyout, wpfLayered = false;
                const int ToolWindow = 0x80, Transparent = 0x20, Layered = 0x80000, Caption = 0x00C00000;
                const int Popup = unchecked((int)0x80000000);
                if (!taskbar && !overflow && !start && !nativeMenu && !flyout)
                {
                    int extended = GetWindowLong(hwnd, -20);
                    wpfLayered = (extended & (Layered | Transparent)) == Layered &&
                        name.StartsWith("HwndWrapper[", StringComparison.Ordinal) && (GetWindowLong(hwnd, -16) & Caption) == 0;
                    int style = GetWindowLong(hwnd, -16);
                    bool captionlessPopup = (style & (Popup | Caption)) == Popup;
                    bool layeredPopup = (extended & (Layered | Transparent)) == Layered && captionlessPopup;
                    bool ownedPopup = captionlessPopup && GetWindow(hwnd, 4) != IntPtr.Zero;
                    if ((extended & ToolWindow) != 0 || wpfLayered || layeredPopup || ownedPopup)
                    {
                        uint ownerProcess;
                        GetWindowThreadProcessId(hwnd, out ownerProcess);
                        toolPopup = (wpfLayered || (style & (Popup | Caption)) == Popup) && (extended & Transparent) == 0 &&
                            ownerProcess != ownProcess && name != "tooltips_class32" && name != "SysShadow";
                        if (!toolPopup) return false;
                    }
                }
                if (result.Exists(item => item.Handle == hwnd)) return false;

                Rect rect = Bounds(hwnd);
                Rect frame = rect;
                Rect region = taskbar ? Rect.Empty : DrawingRegion(hwnd, rect);
                if (start)
                {
                    sawStart = true;
                    rect = ShellSurfaces.StartBounds(hwnd, rect, shellChanged, WindowScale(hwnd));
                }
                else if (!region.IsEmpty && region != rect) rect = region;
                else if (overflow) rect = PopupSurfaces.Resolve(hwnd, rect, WindowScale(hwnd), false, shellChanged,
                    ShellSurfaces.OverflowPanel(rect, name, WindowScale(hwnd)));
                else if (nativeMenu) rect = MenuClientBounds(hwnd, rect, WindowScale(hwnd));
                else if (toolPopup) rect = PopupSurfaces.Resolve(hwnd, rect, WindowScale(hwnd), wpfLayered, shellChanged);
                bool popup = start || overflow || nativeMenu || toolPopup;
                if (rect.IsEmpty || rect.Width < (popup ? 32 : 70) || rect.Height < (popup || taskbar ? 18 : 35)) return false;
                double radius = taskbar ? 0 : EstimateCornerRadius(hwnd);
                if (wpfLayered && rect != frame) radius = Math.Max(radius, 5 * WindowScale(hwnd));
                var window = new WindowBounds
                {
                    Handle = hwnd, Bounds = rect, IsShellPopup = popup,
                    CornerRadius = start || overflow ? Math.Max(8, radius) : radius
                };
                if (start || overflow) result.Insert(0, window);
                else result.Add(window);
                return true;
            };
            EnumWindows(delegate(IntPtr hwnd, IntPtr data) { addWindow(hwnd); return true; }, IntPtr.Zero);

            addWindow(FindWindow("TopLevelWindowForOverflowXamlIsland", null));
            addWindow(FindWindow("NotifyIconOverflowWindow", null));
            if (!sawStart)
            {
                if (foreground != IntPtr.Zero && !result.Exists(item => item.Handle == foreground) &&
                    ShellSurfaces.IsStart(foreground, ClassName(foreground, buffer), true))
                    addWindow(foreground);
                if (!sawStart) addWindow(FindWindow("Windows.UI.Core.CoreWindow", "Start"));
                if (!sawStart) addWindow(FindWindow("Windows.UI.Core.CoreWindow", "Ba\u015flat"));
                if (!sawStart) addWindow(FindWindow("Windows.UI.Core.CoreWindow", "Ba\u015flang\u0131\u00e7"));
                IntPtr focusRoot = shellFocus == IntPtr.Zero ? IntPtr.Zero : GetAncestor(shellFocus, 2);
                IntPtr candidate = focusRoot != IntPtr.Zero && IsWindowVisible(focusRoot) ?
                    focusRoot : foreground;
                if (!sawStart && candidate != IntPtr.Zero && MayBeStartFocus(candidate))
                {
                    Rect panel = ShellSurfaces.StartBounds(candidate, Rect.Empty, shellChanged,
                        WindowScale(candidate), false);
                    if (!panel.IsEmpty)
                    {
                        result.RemoveAll(item => item.Handle == candidate);
                        result.Insert(0, new WindowBounds {
                            Handle = candidate, Bounds = panel, IsShellPopup = true, CornerRadius = 10
                        });
                    }
                }
                else if (!sawStart) ShellSurfaces.ClearStart();
            }
            return result;
        }

        internal static bool DesktopHasFocus(IntPtr foreground)
        {
            if (foreground == IntPtr.Zero) return false;
            if (foreground == GetShellWindow()) return true;
            IntPtr root = GetAncestor(foreground, 2);
            return ShellSurfaces.IsDesktop(ClassName(root == IntPtr.Zero ? foreground : root, new StringBuilder(260)));
        }

        private static readonly bool HasNativeRounding = Environment.OSVersion.Version.Build >= 22000;

        private static double EstimateCornerRadius(IntPtr hwnd)
        {
            if (!HasNativeRounding || IsZoomed(hwnd)) return 0;
            const int Layered = 0x80000;
            if ((GetWindowLong(hwnd, -20) & Layered) != 0) return 0;
            const int CornerPreference = 33, Square = 1, Small = 3;
            int preference;
            if (DwmFlags(hwnd, CornerPreference, out preference, 4) != 0) preference = 0;
            if (preference == Square) return 0;
            const int Caption = 0x00C00000, ThickFrame = 0x00040000;
            if (preference == 0 && (GetWindowLong(hwnd, -16) & (Caption | ThickFrame)) == 0) return 0;

            uint dpi = GetDpiForWindow(hwnd);
            return (preference == Small ? 4 : 8) * (dpi == 0 ? 1 : dpi / 96.0);
        }

        internal static bool IsFullscreen(IntPtr hwnd, System.Drawing.Rectangle screen)
        {
            if (hwnd == IntPtr.Zero || IsIconic(hwnd) || DesktopHasFocus(hwnd)) return false;
            int style = GetWindowLong(hwnd, -16);
            if ((style & 0x00C00000) != 0) return false;
            if (IsZoomed(hwnd) && (style & unchecked((int)0x80000000)) == 0) return false;
            Rect bounds = Bounds(hwnd);
            if (bounds.IsEmpty || bounds.Left > screen.Left + 2 || bounds.Top > screen.Top + 2 ||
                bounds.Right < screen.Right - 2 || bounds.Bottom < screen.Bottom - 2) return false;
            string name = ClassName(hwnd, new StringBuilder(260));
            return !ShellSurfaces.IsTaskbar(name) && !ShellSurfaces.IsOverflow(name) &&
                !ShellSurfaces.IsFlyout(hwnd, name) && !ShellSurfaces.IsStart(hwnd, name, true);
        }

        internal static string FullscreenScreen()
        {
            IntPtr foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero) return null;
            Forms.Screen screen = Forms.Screen.FromHandle(foreground);
            return IsFullscreen(foreground, screen.Bounds) ? screen.DeviceName : null;
        }

        private static Rect Bounds(IntPtr hwnd)
        {
            NativeRect rect;
            if (DwmBounds(hwnd, 9, out rect, 16) != 0 && !GetWindowRect(hwnd, out rect))
                return Rect.Empty;
            if (rect.Right <= rect.Left || rect.Bottom <= rect.Top) return Rect.Empty;
            return new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }

        private static Rect DrawingRegion(IntPtr hwnd, Rect frame)
        {
            NativeRect region, outer;
            const int SimpleRegion = 2;
            if (frame.IsEmpty || GetWindowRgnBox(hwnd, out region) < SimpleRegion || !GetWindowRect(hwnd, out outer) ||
                region.Right <= region.Left || region.Bottom <= region.Top) return Rect.Empty;
            return Rect.Intersect(frame, new Rect(outer.Left + region.Left, outer.Top + region.Top,
                region.Right - region.Left, region.Bottom - region.Top));
        }

        private static Rect MenuClientBounds(IntPtr hwnd, Rect frame, double scale)
        {
            NativeRect client;
            var origin = new Point();
            if (frame.IsEmpty || !GetClientRect(hwnd, out client) || client.Right < 32 || client.Bottom < 18 ||
                !ClientToScreen(hwnd, ref origin)) return frame;
            var content = new Rect(origin.X, origin.Y, client.Right, client.Bottom);
            double margin = Math.Ceiling(16 * scale);
            if (!frame.Contains(content) || content.Left - frame.Left > margin || content.Top - frame.Top > margin ||
                frame.Right - content.Right > margin || frame.Bottom - content.Bottom > margin) return frame;
            return content;
        }
    }
}
