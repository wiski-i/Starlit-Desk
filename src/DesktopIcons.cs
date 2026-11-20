using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using Rect = System.Windows.Rect;
using Point = System.Windows.Point;

namespace SnowDesk
{
    internal sealed class DesktopIcons
    {
        private long nextProbe;
        private int probing;
        private volatile Dictionary<int, Rect> bounds = new Dictionary<int, Rect>();
        internal Dictionary<int, Rect> Bounds { get { return bounds; } }

        internal void Refresh(bool desktop, Action changed)
        {
            long now = Stopwatch.GetTimestamp();
            if (!desktop || now < nextProbe || Interlocked.Exchange(ref probing, 1) != 0) return;
            nextProbe = now + Stopwatch.Frequency / 3;
            ThreadPool.QueueUserWorkItem(delegate
            {
                Dictionary<int, Rect> previous = bounds;
                try { bounds = Read(); }
                catch (ElementNotAvailableException) { }
                catch (COMException) { }
                catch (InvalidOperationException) { }
                catch (ArgumentException) { }
                catch (UnauthorizedAccessException) { }
                finally { Interlocked.Exchange(ref probing, 0); }
                if (Different(previous, bounds)) changed();
            });
        }

        private static bool Different(Dictionary<int, Rect> before, Dictionary<int, Rect> after)
        {
            if (before.Count != after.Count) return true;
            foreach (var item in after)
            {
                Rect old;
                if (!before.TryGetValue(item.Key, out old) || old != item.Value) return true;
            }
            return false;
        }

        private static Dictionary<int, Rect> Read()
        {
            var result = new Dictionary<int, Rect>();
            IntPtr view = FindWindowEx(GetShellWindow(), IntPtr.Zero, "SHELLDLL_DefView", null);
            for (IntPtr worker = IntPtr.Zero; view == IntPtr.Zero;)
            {
                worker = FindWindowEx(IntPtr.Zero, worker, "WorkerW", null);
                if (worker == IntPtr.Zero) break;
                view = FindWindowEx(worker, IntPtr.Zero, "SHELLDLL_DefView", null);
            }
            if (view == IntPtr.Zero) return result;
            IntPtr list = FindWindowEx(view, IntPtr.Zero, "SysListView32", null);
            if (list == IntPtr.Zero) return result;
            var root = AutomationElement.FromHandle(list);
            if (root == null) return result;
            var walker = TreeWalker.RawViewWalker;
            for (var item = walker.GetFirstChild(root); item != null && result.Count < 192; item = walker.GetNextSibling(item))
            {
                var info = item.Current;
                Rect rect = info.BoundingRectangle;
                if (info.IsOffscreen || rect.IsEmpty || rect.Width < 10 || rect.Height < 10 || rect.Width > 248 || rect.Height > 248)
                    continue;
                int id = 23;
                foreach (int part in item.GetRuntimeId()) id = unchecked(id * 31 + part);
                result[id] = rect;
            }
            return result;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string windowClass, string title);
    }

    internal sealed class IconDust
    {
        private static readonly Brush Snow = SnowField.Brush(172, 238, 249, 255);
        private static readonly Brush Leaf = SnowField.Brush(158, 228, 164, 89);
        private static readonly Brush Petal = SnowField.Brush(176, 255, 189, 213);
        private static readonly Brush Water = SnowField.Brush(164, 168, 219, 247);
        private sealed class Mark
        {
            internal Rect Position;
            internal double Cover;
            internal bool Visible;
        }

        private readonly Dictionary<int, Mark> marks = new Dictionary<int, Mark>();
        private readonly List<int> removed = new List<int>();
        private readonly List<TravelingParticle> falling = new List<TravelingParticle>(48);

        internal void Update(Dictionary<int, Rect> icons, List<Desktop.WindowBounds> windows, Rectangle screen, bool desktop)
        {
            removed.Clear();
            foreach (var entry in marks)
                if (!icons.ContainsKey(entry.Key)) removed.Add(entry.Key);
            foreach (int id in removed) marks.Remove(id);
            foreach (var icon in icons)
            {
                Rect box = icon.Value;
                double x = box.Left + box.Width * 0.5, y = box.Top + 5;
                Mark mark;
                if (!marks.TryGetValue(icon.Key, out mark))
                {
                    mark = new Mark { Position = box };
                    marks.Add(icon.Key, mark);
                }
                if (desktop && mark.Visible && mark.Position != box && mark.Cover > 0.15)
                {
                    Rect old = mark.Position;
                    int count = Math.Min(4, 48 - falling.Count);
                    for (int i = 0; i < count; i++)
                        falling.Add(new TravelingParticle { X = old.Left + old.Width * (0.31 + i * 0.1), Y = old.Top + 4,
                            VX = (i - 1.5) * 28, VY = -16 - i * 5, Size = 1.3 + i * 0.28, Mass = 0.58 });
                    mark.Cover = 0;
                }
                mark.Position = box;
                mark.Visible = screen.Contains((int)x, (int)y) && !windows.Exists(window => window.Bounds.Contains(x, y));
            }
            falling.RemoveAll(piece => !screen.Contains((int)piece.X, (int)piece.Y) ||
                windows.Exists(window => window.Bounds.Contains(piece.X, piece.Y)));
        }

        internal void Advance(double seconds)
        {
            foreach (Mark mark in marks.Values)
                if (mark.Visible) mark.Cover = Math.Min(1, mark.Cover + seconds * 0.18);
            for (int i = falling.Count - 1; i >= 0; i--)
            {
                TravelingParticle piece = falling[i];
                piece.Mass -= seconds;
                if (piece.Mass <= 0) { falling.RemoveAt(i); continue; }
                piece.VY += 430 * seconds;
                piece.X += piece.VX * seconds;
                piece.Y += piece.VY * seconds;
                falling[i] = piece;
            }
        }

        internal void Draw(Canvas canvas, Rectangle screen, DesktopTheme theme)
        {
            Brush color = theme == DesktopTheme.Winter ? Snow : theme == DesktopTheme.Autumn ? Leaf :
                theme == DesktopTheme.Summer ? Water : Petal;
            foreach (Mark mark in marks.Values)
            {
                if (!mark.Visible || mark.Cover < 0.08) continue;
                Rect icon = mark.Position;
                for (int i = 0; i < (mark.Cover > 0.49 ? 3 : 2); i++)
                    canvas.DrawEllipse(color, null, new Point(icon.Left - screen.Left + icon.Width * (0.35 + i * 0.14),
                        icon.Top - screen.Top + 4 - (i == 1 ? 1 : 0)), (2.7 - i * 0.5) * mark.Cover, 0.85);
            }
            foreach (TravelingParticle piece in falling)
                canvas.DrawEllipse(color, null, new Point(piece.X - screen.Left, piece.Y - screen.Top), piece.Size, 0.9);
        }
    }
}
