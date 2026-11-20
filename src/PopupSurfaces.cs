using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;

namespace SnowDesk
{
    internal static class PopupSurfaces
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<IntPtr, Probe> Probes = new Dictionary<IntPtr, Probe>(24);
        private static int active;

        private sealed class Probe
        {
            internal Rect Frame, Panel = Rect.Empty;
            internal double Scale;
        }

        internal static void Forget(IntPtr hwnd)
        {
            lock (Gate) Probes.Remove(hwnd);
        }

        internal static void Clear()
        {
            lock (Gate) Probes.Clear();
        }

        internal static Rect Resolve(IntPtr hwnd, Rect frame, double scale, bool wpfLayered, Action invalidate,
            Rect? fallbackBounds = null)
        {
            if (invalidate == null || frame.IsEmpty || frame.Width < 64 || frame.Height < 32 ||
                frame.Width > 1200 * scale || frame.Height > 860 * scale) return fallbackBounds ?? frame;
            Probe probe;
            lock (Gate)
            {
                if (Probes.TryGetValue(hwnd, out probe) && probe.Frame.Size == frame.Size && probe.Scale == scale)
                {
                    if (probe.Panel.IsEmpty) return fallbackBounds ?? frame;
                    Rect moved = probe.Panel;
                    moved.Offset(frame.X - probe.Frame.X, frame.Y - probe.Frame.Y);
                    return moved;
                }
                if (active >= 2 || (probe == null && Probes.Count >= 24)) return fallbackBounds ?? frame;
                probe = new Probe { Frame = frame, Scale = scale };
                Probes[hwnd] = probe;
                active++;
            }
            ThreadPool.QueueUserWorkItem(delegate
            {
                if (wpfLayered)
                {
                    Rect fallback = BalloonPanel(frame, scale, ShellSurfaces.ProcessImagePath(hwnd));
                    if (!fallback.IsEmpty && Save(hwnd, probe, fallback)) invalidate();
                }
                Rect panel = FindPanel(hwnd, frame, scale);
                lock (Gate) active--;
                if (Save(hwnd, probe, panel)) invalidate();
            });
            return fallbackBounds ?? frame;
        }

        private static bool Save(IntPtr hwnd, Probe probe, Rect panel)
        {
            lock (Gate)
            {
                Probe current;
                if (!Probes.TryGetValue(hwnd, out current) || current != probe) return false;
                if (!panel.IsEmpty) probe.Panel = panel;
                return true;
            }
        }

        internal static Rect BalloonPanel(Rect frame, double scale, string imagePath)
        {
            if (frame.IsEmpty || frame.Width < 280 * scale || frame.Width > 560 * scale ||
                frame.Height < 85 * scale || frame.Height > 500 * scale ||
                imagePath.IndexOf(@"\Kaspersky Lab\", StringComparison.OrdinalIgnoreCase) < 0 ||
                !imagePath.EndsWith(@"\avpui.exe", StringComparison.OrdinalIgnoreCase)) return Rect.Empty;

            // The notification window includes a shadow around its WPF card.
            double inset = Math.Round(15 * scale);
            frame.Inflate(-inset, -inset);
            return frame;
        }

        internal static bool LooksLikePanel(Rect panel, Rect frame, double scale)
        {
            if (panel.IsEmpty || !frame.Contains(panel) || panel.Width < 44 || panel.Height < 24) return false;
            double left = panel.Left - frame.Left, right = frame.Right - panel.Right;
            double top = panel.Top - frame.Top, bottom = frame.Bottom - panel.Bottom;
            double margin = 29 * scale;
            return top >= 3 * scale && left <= margin && right <= margin && top <= margin && bottom <= margin &&
                Math.Abs(left - right) <= 10 * scale && panel.Width >= 0.70 * frame.Width && panel.Height >= 0.62 * frame.Height;
        }

        private static Rect FindPanel(IntPtr hwnd, Rect frame, double scale)
        {
            try
            {
                var root = AutomationElement.FromHandle(hwnd);
                if (root == null) return Rect.Empty;
                var best = Rect.Empty;
                var walker = TreeWalker.RawViewWalker;
                var parents = new List<AutomationElement>(16) { root };
                int allowance = 20;
                for (int index = 0; index < parents.Count && allowance > 0; index++)
                {
                    for (var child = walker.GetFirstChild(parents[index]); child != null && allowance > 0;
                        child = walker.GetNextSibling(child))
                    {
                        allowance--;
                        var info = child.Current;
                        if (info.IsOffscreen) continue;
                        ControlType type = info.ControlType;
                        if (type != ControlType.Pane && type != ControlType.Window && type != ControlType.Menu &&
                            type != ControlType.Group && type != ControlType.Custom && type != ControlType.ToolTip) continue;
                        Rect panel = info.BoundingRectangle;
                        if (LooksLikePanel(panel, frame, scale) &&
                            (best.IsEmpty || panel.Width * panel.Height > best.Width * best.Height)) best = panel;
                        if (!panel.IsEmpty && panel.IntersectsWith(frame)) parents.Add(child);
                    }
                }
                return best;
            }
            catch (ElementNotAvailableException) { return Rect.Empty; }
            catch (COMException) { return Rect.Empty; }
            catch (InvalidOperationException) { return Rect.Empty; }
            catch (ArgumentException) { return Rect.Empty; }
            catch (UnauthorizedAccessException) { return Rect.Empty; }
        }
    }
}
