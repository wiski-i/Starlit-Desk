using System;
using System.Collections.Generic;
using System.Windows;

namespace SnowDesk
{
    internal sealed class Icicles
    {
        private const int MaximumHanging = 20;
        private const int MaximumFalling = 12;
        private readonly Rect screen;
        private readonly double scale, bottomLimit;
        private readonly List<Shard> hanging = new List<Shard>(MaximumHanging);
        private readonly List<Shard> falling = new List<Shard>(MaximumFalling);
        private readonly List<Candidate> anchors = new List<Candidate>(MaximumHanging);
        private bool enabled = true;

        private sealed class Shard
        {
            internal IntPtr Owner;
            internal int Slot;
            internal Rect Bounds;
            internal double X, Y, Length, Limit, Age, VisibleLength, VX, VY, Angle, Spin, BodyWidth;
            internal bool Fragment, Drip;
            internal double MeltCredit;
        }

        private struct Candidate
        {
            internal IntPtr Owner;
            internal int Slot;
            internal Rect Bounds;
            internal double Offset, VisibleLength, Limit, BodyWidth;
        }

        internal Icicles(Rect screen, Rect workArea)
        {
            this.screen = screen;
            scale = Math.Max(1, Math.Min(1.65, screen.Height / 1080));
            bottomLimit = Math.Min(screen.Height - 30 * scale, workArea.Bottom - screen.Top - 22 * scale);
        }

        internal void UpdateWindows(List<Desktop.WindowBounds> windows)
        {
            anchors.Clear();
            for (int index = 0; index < windows.Count && anchors.Count < MaximumHanging; index++)
            {
                var window = windows[index];
                Rect bounds = window.Bounds;
                double baseY = bounds.Bottom - screen.Top - 0.6;
                if (window.IsShellPopup || bounds.Width < 150 * scale || bounds.Height < 70 ||
                    baseY < 20 || baseY > bottomLimit ||
                    bounds.Right <= screen.Left || bounds.Left >= screen.Right)
                    continue;

                int slots = Math.Min(3, Math.Max(1, (int)(bounds.Width / (290 * scale))));
                for (int slot = 0; slot < slots && anchors.Count < MaximumHanging; slot++)
                {
                    uint seed = unchecked((uint)window.Handle.ToInt64() * 2654435761u + (uint)(slot + 1) * 2246822519u);
                    double offset = bounds.Width * (slot + 0.28 + ((seed >> 8) & 255) / 255.0 * 0.44) / slots;
                    double x = bounds.Left + offset;
                    if (x <= screen.Left + 4 || x >= screen.Right - 4) continue;
                    double visible = 30 * scale;
                    for (int front = 0; front < index; front++)
                    {
                        Rect blocker = windows[front].Bounds;
                        if (x < blocker.Left || x > blocker.Right || blocker.Bottom <= bounds.Bottom) continue;
                        if (blocker.Top <= bounds.Bottom + 1) { visible = 0; break; }
                        visible = Math.Min(visible, blocker.Top - bounds.Bottom - 1);
                    }
                    if (visible < 3 * scale) continue;
                    anchors.Add(new Candidate
                    {
                        Owner = window.Handle, Slot = slot, Bounds = bounds, Offset = offset,
                        VisibleLength = visible, Limit = (15 + (seed & 15) * 0.64) * scale,
                        BodyWidth = 0.88 + ((seed >> 16) & 7) * 0.04
                    });
                }
            }

            for (int i = hanging.Count - 1; i >= 0; i--)
            {
                Shard shard = hanging[i];
                Candidate target;
                if (!FindAnchor(shard.Owner, shard.Slot, out target))
                {
                    hanging.RemoveAt(i);
                    continue;
                }
                double dx = target.Bounds.Left - shard.Bounds.Left;
                double dy = target.Bounds.Bottom - shard.Bounds.Bottom;
                bool resize = Math.Abs(target.Bounds.Width - shard.Bounds.Width) > 1 ||
                    Math.Abs(target.Bounds.Height - shard.Bounds.Height) > 1;
                if ((resize || Math.Abs(dx) + Math.Abs(dy) > 14 * scale) && shard.Length > 3 * scale)
                {
                    if (enabled) Break(shard, Math.Max(-145, Math.Min(145, dx * 2.0)));
                    hanging.RemoveAt(i);
                    continue;
                }
                ApplyAnchor(shard, target);
            }
            foreach (Candidate anchor in anchors)
            {
                bool existing = false;
                foreach (Shard shard in hanging)
                    if (shard.Owner == anchor.Owner && shard.Slot == anchor.Slot) { existing = true; break; }
                if (existing || hanging.Count == MaximumHanging) continue;
                var fresh = new Shard { Owner = anchor.Owner, Slot = anchor.Slot, Length = scale * 1.2 };
                ApplyAnchor(fresh, anchor);
                hanging.Add(fresh);
            }
        }

        private bool FindAnchor(IntPtr owner, int slot, out Candidate result)
        {
            foreach (Candidate candidate in anchors)
                if (candidate.Owner == owner && candidate.Slot == slot) { result = candidate; return true; }
            result = new Candidate();
            return false;
        }

        private void ApplyAnchor(Shard shard, Candidate anchor)
        {
            shard.Bounds = anchor.Bounds;
            shard.X = anchor.Bounds.Left - screen.Left + anchor.Offset;
            shard.Y = anchor.Bounds.Bottom - screen.Top - 0.6;
            shard.VisibleLength = anchor.VisibleLength;
            shard.Limit = anchor.Limit;
            shard.BodyWidth = anchor.BodyWidth;
        }

        internal void Advance(double seconds, bool active, List<SnowLedge> ledges, double sunshine = 0)
        {
            enabled = active;
            if (!active)
            {
                falling.Clear();
                return;
            }
            if (seconds <= 0) return;
            foreach (Shard shard in hanging)
            {
                shard.Age += seconds;
                double maturity = shard.Length / shard.Limit;
                double growth = (0.32 + (1 - maturity) * 0.42) * scale;
                double lit = sunshine * (0.34 + 0.66 * shard.X / screen.Width);
                shard.Length = Math.Max(scale * 0.8, Math.Min(shard.Limit, shard.Length + (growth - 1.05 * lit) * seconds));
                if (lit > 0.46 && shard.Length > 3 * scale && shard.Length < shard.VisibleLength)
                {
                    shard.MeltCredit += lit * seconds;
                    if (shard.MeltCredit >= 1.12 && falling.Count < MaximumFalling)
                    {
                        shard.MeltCredit = 0;
                        falling.Add(new Shard { X = shard.X, Y = shard.Y + shard.Length,
                            Drip = true, Length = 1.6 * scale, BodyWidth = 0.8, VY = 28 * scale });
                    }
                }
                if (shard.Length >= shard.Limit && shard.Age > 31 + shard.Limit / scale)
                {
                    Break(shard, (shard.Slot % 2 == 0 ? -9 : 9) * scale);
                    shard.Length = 0.8 * scale;
                    shard.Age = 0;
                }
            }
            for (int i = falling.Count - 1; i >= 0; i--)
            {
                Shard shard = falling[i];
                shard.Age += seconds;
                double oldTip = shard.Y + TipDrop(shard), oldSpeed = shard.VY;
                shard.VY = Math.Min(490, oldSpeed + 355 * seconds);
                shard.Y += (oldSpeed + shard.VY) * seconds * 0.5;
                shard.X += shard.VX * seconds;
                shard.Angle += shard.Spin * seconds;
                bool hit = false;
                if (!shard.Fragment)
                {
                    double drop = TipDrop(shard);
                    double tipX = shard.X - Math.Sin(shard.Angle * Math.PI / 180) * shard.Length;
                    foreach (SnowLedge ledge in ledges)
                    {
                        if (!ledge.IsVisibleAt(tipX)) continue;
                        double surface = ledge.SurfaceAt(tipX);
                        if (oldTip > surface || shard.Y + drop < surface) continue;
                        shard.Y = surface - drop;
                        hit = true;
                        break;
                    }
                }
                if (hit || shard.Y > screen.Height + shard.Length || shard.X < -25 ||
                    shard.X > screen.Width + 25 || (shard.Fragment && shard.Age > 0.32))
                {
                    falling.RemoveAt(i);
                    if (hit && !shard.Drip && shard.Length > 4 * scale) ScatterChips(shard);
                }
            }
        }

        private static double TipDrop(Shard shard)
        {
            return Math.Max(0, Math.Cos(shard.Angle * Math.PI / 180) * shard.Length);
        }

        private void Break(Shard source, double vx)
        {
            if (falling.Count == MaximumFalling) falling.RemoveAt(0);
            falling.Add(new Shard
            {
                X = source.X, Y = source.Y, Length = Math.Min(source.Length, source.VisibleLength),
                VX = vx, VY = 15 * scale, Angle = 0, BodyWidth = source.BodyWidth,
                Spin = (vx < 0 ? -1 : 1) * (18 + source.Length * 0.65)
            });
        }

        private void ScatterChips(Shard source)
        {
            if (falling.Count > MaximumFalling - 3) return;
            for (int piece = 0; piece < 3; piece++)
                falling.Add(new Shard
                {
                    X = source.X - Math.Sin(source.Angle * Math.PI / 180) * source.Length + (piece - 1) * scale,
                    Y = source.Y + TipDrop(source),
                    Length = (1.4 + piece * 0.45) * scale, Fragment = true,
                    BodyWidth = 1,
                    VX = source.VX * 0.18 + (piece - 1) * 38 * scale, VY = (-43 + piece * 8) * scale,
                    Angle = (piece - 1) * 37, Spin = (piece - 1) * 145
                });
        }

        internal void Draw(Canvas canvas)
        {
            if (!enabled) return;
            foreach (Shard shard in hanging)
            {
                double length = Math.Min(shard.Length, shard.VisibleLength);
                if (length < 2.5 * scale) continue;
                canvas.DrawIcicle(shard.X, shard.Y, length, HalfWidth(shard, length), 0);
            }
            foreach (Shard shard in falling)
            {
                double length = shard.Length;
                if (shard.Drip)
                {
                    canvas.DrawEllipse(DripColor, null, new Point(shard.X, shard.Y), length * 0.55, length);
                    continue;
                }
                if (shard.Fragment) length *= Math.Max(0.25, 1 - shard.Age * 2.25);
                canvas.DrawIcicle(shard.X, shard.Y, length, HalfWidth(shard, length), shard.Angle);
            }
        }

        private static readonly System.Drawing.Brush DripColor = SnowField.Brush(182, 195, 232, 255);

        private double HalfWidth(Shard shard, double length)
        {
            return Math.Min(4.2 * scale, 0.82 * scale + length * 0.16) * shard.BodyWidth;
        }
    }
}
