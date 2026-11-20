using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using Condition = System.Windows.Automation.Condition;
using Forms = System.Windows.Forms;

namespace SnowDesk
{
    internal static class ShellSurfaces
    {
        private static readonly object Gate = new object();
        private static IntPtr requested;
        private static Rect result = Rect.Empty;
        private static long lastRequest;
        private static bool searching;
        private static class StartAccessibility
        {
            internal static readonly Condition Panel = new OrCondition(
                new PropertyCondition(AutomationElement.AutomationIdProperty, "AcrylicBorder"),
                new PropertyCondition(AutomationElement.AutomationIdProperty, "StartRoot"),
                new PropertyCondition(AutomationElement.AutomationIdProperty, "FrameRoot"),
                new PropertyCondition(AutomationElement.AutomationIdProperty, "MainMenu"),
                new PropertyCondition(AutomationElement.ClassNameProperty, "StartDocked.LauncherFrame"),
                new PropertyCondition(AutomationElement.ClassNameProperty, "StartMenu.StartBlendedFlexFrame"),
                new PropertyCondition(AutomationElement.ClassNameProperty, "LauncherFrame"),
                new PropertyCondition(AutomationElement.ClassNameProperty, "StartBlendedFlexFrame"));

            internal static readonly Condition SearchBox = new OrCondition(
                new PropertyCondition(AutomationElement.AutomationIdProperty, "SearchBoxToggleButton"),
                new PropertyCondition(AutomationElement.ClassNameProperty, "StartMenu.SearchBoxToggleButton"),
                new PropertyCondition(AutomationElement.ClassNameProperty, "StartDocked.SearchBoxToggleButton"));
        }

        internal static bool IsDesktop(string windowClass)
        {
            return windowClass == "Progman" || windowClass == "WorkerW" || windowClass == "SHELLDLL_DefView";
        }

        internal static bool IsOverflow(string windowClass)
        {
            return windowClass == "NotifyIconOverflowWindow" || windowClass == "TopLevelWindowForOverflowXamlIsland";
        }

        internal static bool IsFlyout(IntPtr hwnd, string windowClass)
        {
            if (windowClass == "ControlCenterWindow" || windowClass == "NotificationCenterWindow" ||
                windowClass == "Windows.UI.ActionCenter" || windowClass == "Xaml_WindowedPopupClass") return true;
            if (windowClass != "Windows.UI.Core.CoreWindow" && windowClass != "XamlExplorerHostIslandWindow") return false;
            var title = new StringBuilder(96);
            GetWindowText(hwnd, title, title.Capacity);
            string name = title.ToString();
            return name == "Notification Center" || name == "Action Center" || name == "Quick Settings" ||
                name == "Bildirim Merkezi" || name == "İşlem Merkezi" || name == "Hızlı Ayarlar";
        }

        internal static Rect OverflowPanel(Rect outer, string windowClass, double scale)
        {
            if (outer.IsEmpty || windowClass != "TopLevelWindowForOverflowXamlIsland") return outer;

            // This host includes the transparent XAML shadow margin around the painted panel.
            double margin = Math.Round(12 * scale);
            if (outer.Width < 70 + 2 * margin || outer.Height < 35 + 2 * margin) return outer;
            return new Rect(outer.X + margin, outer.Y + margin, outer.Width - 2 * margin, outer.Height - 2 * margin);
        }

        internal static bool CouldHostStart(string windowClass)
        {
            return windowClass == "Windows.UI.Core.CoreWindow" || windowClass == "WinUIDesktopWin32WindowClass" ||
                windowClass == "ApplicationFrameWindow" || windowClass == "Microsoft.UI.Content.DesktopChildSiteBridge" ||
                windowClass == "Windows.UI.Input.InputSite.WindowClass" || windowClass == "XamlExplorerHostIslandWindow";
        }

        internal static bool IsTaskbar(string windowClass)
        {
            return windowClass == "Shell_TrayWnd" || windowClass == "Shell_SecondaryTrayWnd";
        }

        internal static bool IsStart(IntPtr hwnd, string windowClass, bool inspectOwner = false)
        {
            if (windowClass == "DV2ControlHost") return true;
            bool shellHost = CouldHostStart(windowClass);
            if (!inspectOwner && !shellHost) return false;
            if (shellHost)
            {
                var text = new StringBuilder(100);
                GetWindowText(hwnd, text, text.Capacity);
                string title = text.ToString();
                if (title == "Start" || title == "Ba\u015flat" || title == "Ba\u015flang\u0131\u00e7") return true;
            }
            return ProcessImagePath(hwnd).EndsWith(@"\StartMenuExperienceHost.exe", StringComparison.OrdinalIgnoreCase);
        }

        internal static string ProcessImagePath(IntPtr hwnd)
        {
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            IntPtr process = OpenProcess(0x1000, false, pid); // PROCESS_QUERY_LIMITED_INFORMATION.
            if (process == IntPtr.Zero) return string.Empty;
            try
            {
                var path = new StringBuilder(512);
                uint size = (uint)path.Capacity;
                return QueryFullProcessImageName(process, 0, path, ref size) ? path.ToString() : string.Empty;
            }
            finally { CloseHandle(process); }
        }

        internal static Rect StartBounds(IntPtr hwnd, Rect native, Action invalidate,
            double scale = 1, bool confirmed = true)
        {
            var display = Forms.Screen.FromHandle(hwnd);
            var screen = display.Bounds;
            var monitor = new Rect(screen.X, screen.Y, screen.Width, screen.Height);
            Rect fallback = confirmed && LooksLikePanel(native, monitor) ? native : Rect.Empty;
            if (invalidate == null) return fallback;
            lock (Gate)
            {
                if (requested != hwnd)
                {
                    requested = hwnd;
                    result = Rect.Empty;
                    lastRequest = 0;
                }
                if (!result.IsEmpty) return result;
                long now = DateTime.UtcNow.Ticks;
                if (searching || now - lastRequest < TimeSpan.TicksPerSecond || invalidate == null) return fallback;
                lastRequest = now;
                searching = true;
            }
            ThreadPool.QueueUserWorkItem(delegate
            {
                int workBottom = display.WorkingArea.Bottom;
                Rect actual = FindAccessiblePanel(hwnd, monitor, workBottom, scale, confirmed);
                if (actual.IsEmpty)
                {
                    Thread.Sleep(180);
                    bool stillRequested;
                    lock (Gate) stillRequested = requested == hwnd;
                    if (stillRequested) actual = FindAccessiblePanel(hwnd, monitor, workBottom, scale, confirmed);
                }
                lock (Gate)
                {
                    searching = false;
                    if (requested == hwnd) result = actual;
                }
                if (!actual.IsEmpty) invalidate();
            });
            return fallback;
        }

        internal static void ClearStart()
        {
            lock (Gate)
            {
                requested = IntPtr.Zero;
                result = Rect.Empty;
            }
        }

        internal static bool LooksLikePanel(Rect panel, Rect monitor)
        {
            return !panel.IsEmpty && panel.Width >= 215 && panel.Height >= 160 &&
                panel.Width <= monitor.Width - 12 && panel.Height <= monitor.Height - 12 &&
                panel.IntersectsWith(monitor);
        }

        private static Rect FindAccessiblePanel(IntPtr hwnd, Rect monitor, int workBottom, double scale, bool confirmed)
        {
            try
            {
                AutomationElement root = AutomationElement.FromHandle(hwnd);
                if (root == null) return Rect.Empty;
                var search = root.FindFirst(TreeScope.Descendants, StartAccessibility.SearchBox);
                var panel = root.FindFirst(TreeScope.Descendants, StartAccessibility.Panel);
                Rect searchBounds = search == null ? Rect.Empty : search.Current.BoundingRectangle;
                if (panel != null)
                {
                    var visual = panel.Current.BoundingRectangle;
                    bool startClass = panel.Current.ClassName.IndexOf("Start", StringComparison.OrdinalIgnoreCase) >= 0;
                    if ((confirmed || startClass || !searchBounds.IsEmpty) && LooksLikePanel(visual, monitor) &&
                        (searchBounds.IsEmpty || (visual.Contains(searchBounds) && searchBounds.Top - visual.Top <= 48 * scale)))
                        return visual;
                }

                if (!searchBounds.IsEmpty)
                {
                    Rect anchored = PanelFromSearchBox(searchBounds, monitor, workBottom, scale);
                    if (!anchored.IsEmpty) return anchored;
                }
                Rect focusedPanel = PanelFromFocusedAncestors(hwnd, monitor);
                if (!focusedPanel.IsEmpty) return focusedPanel;
                if (!confirmed) return Rect.Empty;
                if (LooksLikePanel(root.Current.BoundingRectangle, monitor)) return root.Current.BoundingRectangle;

                Rect best = Rect.Empty;
                foreach (AutomationElement child in root.FindAll(TreeScope.Children, System.Windows.Automation.Condition.TrueCondition))
                {
                    if (child.Current.ControlType != ControlType.Pane && child.Current.ControlType != ControlType.Window &&
                        child.Current.ControlType != ControlType.Group) continue;
                    Rect bounds = child.Current.BoundingRectangle;
                    if (LooksLikePanel(bounds, monitor) && (best.IsEmpty || bounds.Width * bounds.Height > best.Width * best.Height))
                        best = bounds;
                }
                return best;
            }
            catch (ElementNotAvailableException) { return Rect.Empty; }
            catch (COMException) { return Rect.Empty; }
            catch (InvalidOperationException) { return Rect.Empty; }
            catch (ArgumentException) { return Rect.Empty; }
        }

        private static Rect PanelFromFocusedAncestors(IntPtr hwnd, Rect monitor)
        {
            uint process;
            GetWindowThreadProcessId(hwnd, out process);
            var element = AutomationElement.FocusedElement;
            var walker = TreeWalker.RawViewWalker;
            bool inStart = false;
            for (int depth = 0; depth < 14 && element != null; depth++, element = walker.GetParent(element))
            {
                var info = element.Current;
                if (info.ProcessId != (int)process) break;
                string name = info.ClassName;
                string id = info.AutomationId;
                bool startClass = name.StartsWith("StartMenu.", StringComparison.Ordinal) ||
                    name.StartsWith("StartDocked.", StringComparison.Ordinal);
                inStart |= startClass;
                if ((inStart && (id == "MainMenu" || id == "AcrylicBorder" || id == "FrameRoot")) ||
                    name == "StartMenu.StartBlendedFlexFrame" || name == "StartDocked.LauncherFrame")
                {
                    Rect bounds = info.BoundingRectangle;
                    if (LooksLikePanel(bounds, monitor)) return bounds;
                }
            }
            return Rect.Empty;
        }

        internal static Rect PanelFromSearchBox(Rect search, Rect monitor, int workBottom, double scale)
        {
            if (search.IsEmpty || search.Width < 192 * scale || search.Height < 18 * scale ||
                search.Height > 60 * scale || !monitor.Contains(search)) return Rect.Empty;

            double left = Math.Max(monitor.Left, search.Left - 32 * scale);
            double right = Math.Min(monitor.Right, search.Right + 32 * scale);
            double top = Math.Max(monitor.Top, search.Top - 18 * scale);
            double bottom = Math.Min(monitor.Bottom, workBottom - 8 * scale);
            if (bottom <= top) return Rect.Empty;
            var estimate = new Rect(left, top, right - left, bottom - top);
            return LooksLikePanel(estimate, monitor) ? estimate : Rect.Empty;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hwnd, StringBuilder title, int capacity);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder name, ref uint size);
        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
