using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using SolidBrush = System.Drawing.SolidBrush;
using Color = System.Drawing.Color;
using Pen = System.Drawing.Pen;
using Forms = System.Windows.Forms;

namespace SnowDesk
{
    internal sealed class SnowField : DesktopScene
    {
        private const int MaxFlakes = 600;
        private const int MaxLedges = 32;
        private const int MaxGrains = 720;
        private const double Gravity = 345;
        private const double Accumulation = 1.5;
        private static readonly SolidBrush[] FlakeBrushes =
        {
            Brush(120, 220, 239, 255), Brush(175, 236, 247, 255), Brush(235, 255, 255, 255)
        };
        private static readonly Pen PileEdge = new Pen(Brush(87, 37, 66, 100), 0.9f);
        private static readonly Pen CrystalRay = new Pen(Brush(220, 238, 249, 255), 0.95f);
        private static readonly Pen FlakeOutline = new Pen(Brush(85, 34, 67, 102), 0.85f);

        private readonly Random random = new Random(Guid.NewGuid().GetHashCode());
        private readonly Flake[] flakes = new Flake[MaxFlakes];
        private readonly List<Grain> grains = new List<Grain>(MaxGrains);
        private readonly List<SnowLedge> ledges = new List<SnowLedge>(MaxLedges + 1);
        private readonly Dictionary<IntPtr, SnowLedge> piles = new Dictionary<IntPtr, SnowLedge>();
        private readonly HashSet<IntPtr> retained = new HashSet<IntPtr>();
        private readonly List<IntPtr> removed = new List<IntPtr>();
        private readonly Rect screen;
        private readonly Rect workArea;
        private readonly Decorations effects;
        private readonly Icicles icicles;
        private readonly SnowLedge floor;
        private int flakeCount;
        private double elapsed;
        private double snowMaintenance, windTravel;
        private double gustTime, gustDirection = 1;
        private long lastGeometry;
        private Desktop.Point lastPointer;
        private bool hasPointer;

        private struct Flake
        {
            internal double X, Y, Speed, Drift, Radius, PushX, PushY;
            internal int Layer;
        }

        private struct Grain
        {
            internal double X, Y, VX, VY, Age, Mass;
            internal IntPtr Source;
            internal bool LeavesRight;
        }

        private struct ReleasedSnow
        {
            internal double Mass, WeightedX, WeightedY;

            internal void Add(double x, double y, double mass)
            {
                Mass += mass;
                WeightedX += x * mass;
                WeightedY += y * mass;
            }
        }

        internal SnowField(Forms.Screen display)
        {
            var bounds = display.Bounds;
            screen = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            var work = display.WorkingArea;
            workArea = new Rect(work.X, work.Y, work.Width, work.Height);
            effects = new Decorations(bounds.Width, bounds.Height, SpillPlow, KickPowder, SprayPlow);
            icicles = new Icicles(screen, workArea);
            floor = new SnowLedge(IntPtr.Zero, screen, 0, screen.Height - 1, screen.Width);
            for (int cell = 0; cell < floor.Visible.Length; cell++) floor.SetVisible(cell, true);
            ledges.Add(floor);
        }

        public bool UsesWindowGeometry(Settings settings) { return settings.WindowLedges; }

        public void UpdateLedges(List<Desktop.WindowBounds> windows)
        {
            icicles.UpdateWindows(windows);
            long now = Stopwatch.GetTimestamp();
            double scanTime = Math.Max(0.016, Math.Min(0.1, (double)(now - lastGeometry) / Stopwatch.Frequency));
            lastGeometry = now;
            ledges.Clear();
            retained.Clear();
            for (int i = 0; i < windows.Count && retained.Count < MaxLedges; i++)
            {
                Rect bounds = windows[i].Bounds;
                double left = Math.Max(screen.Left, bounds.Left);
                double right = Math.Min(screen.Right, bounds.Right);
                double width = right - left;
                if (bounds.Top <= screen.Top + 2 || bounds.Top >= screen.Bottom - 18 || width < 32)
                    continue;

                IntPtr handle = windows[i].Handle;
                bool newPopup = windows[i].IsShellPopup && !piles.ContainsKey(handle);
                SnowLedge ledge = LocateLedge(handle, bounds, left - screen.Left, bounds.Top - screen.Top, width, scanTime);
                bool fitsWorkArea = Math.Abs(bounds.Top - workArea.Top) <= 2 &&
                    (Math.Abs(bounds.Left - workArea.Left) <= 2 || Math.Abs(bounds.Right - workArea.Right) <= 2);
                double corner = fitsWorkArea ? 0 : windows[i].CornerRadius;
                ledge.SetCorners(corner, bounds.Left - screen.Left, bounds.Right - screen.Left);
                bool hasVisibleCells = false;
                for (int cell = 0; cell < ledge.Visible.Length; cell++)
                {
                    double x = screen.Left + ledge.CellCenter(cell);
                    bool visible = true;
                    for (int front = 0; front < i; front++)
                    {
                        if (windows[front].Bounds.Contains(x, bounds.Top - 1))
                        {
                            visible = false;
                            break;
                        }
                    }
                    ledge.SetVisible(cell, visible);
                    hasVisibleCells |= visible;
                }
                retained.Add(handle);
                if (hasVisibleCells)
                {
                    ledges.Add(ledge);
                    if (newPopup) SnowAbovePopup(ledge);
                }
            }

            removed.Clear();
            foreach (var pair in piles)
                if (!retained.Contains(pair.Key)) removed.Add(pair.Key);
            foreach (IntPtr handle in removed)
            {
                SnowLedge ledge = piles[handle];
                ShedLedge(ledge, ledge.WindowBounds, ledge.X, ledge.Y, 0, 1, 0, 0, 15);
                ledge.Dispose();
                piles.Remove(handle);
            }
            ledges.Sort((left, right) => left.Y.CompareTo(right.Y));
            ledges.Add(floor);
        }

        private void SnowAbovePopup(SnowLedge ledge)
        {
            int count = Math.Max(5, Math.Min(14, (int)(ledge.Width / 54)));
            for (int index = 0; index < count; index++)
            {
                double x = ledge.X + ledge.Width * (index + 0.45 + random.NextDouble() * 0.22) / count;
                if (!ledge.IsVisibleAt(x)) continue;
                var flake = new ReleasedSnow();
                flake.Add(x, Math.Max(0, ledge.SurfaceAt(x) - 14 - random.NextDouble() * 18), 1.45);
                Emit(ref flake, IntPtr.Zero, 0, 28);
            }
        }

        private SnowLedge LocateLedge(IntPtr handle, Rect bounds, double x, double y, double width, double scanTime)
        {
            SnowLedge ledge;
            if (!piles.TryGetValue(handle, out ledge))
            {
                ledge = new SnowLedge(handle, bounds, x, y, width);
                piles.Add(handle, ledge);
                return ledge;
            }
            if (ledge.WindowBounds.Left == bounds.Left && ledge.WindowBounds.Width == bounds.Width &&
                ledge.X == x && ledge.Y == y && ledge.Width == width) return ledge;

            double shift = bounds.Width == ledge.WindowBounds.Width ? bounds.Left - ledge.WindowBounds.Left : 0;
            double vertical = y - ledge.Y;
            double travel = Math.Sqrt(shift * shift + vertical * vertical);
            double loosened = 1 - Math.Exp(-travel / 90);
            double vx = Clamp(shift / scanTime * 0.50, -390, 390);
            double vy = Clamp(vertical / scanTime * 0.32, -100, 140);
            ShedLedge(ledge, bounds, x, y, width, loosened, shift, vx, vy);
            return ledge;
        }

        private void ShedLedge(SnowLedge ledge, Rect bounds, double x, double y, double width,
            double loosened, double shift, double vx, double vy)
        {
            bool aligned = width == ledge.Width && x == ledge.X + shift;
            double[] next = aligned ? ledge.Snow : new double[SnowLedge.CellCount(width)];
            double retainedMass = 0;
            ReleasedSnow release = new ReleasedSnow();
            for (int cell = 0; cell < ledge.Snow.Length; cell++)
            {
                double mass = ledge.Snow[cell];
                if (mass > 0)
                {
                    double left = ledge.CellLeft(cell) + shift;
                    double right = ledge.CellRight(cell) + shift;
                    double surface = ledge.SurfaceAt(ledge.CellCenter(cell));
                    double kept = mass * (1 - loosened);
                    double placed = aligned ? kept : Transfer(next, x, width, left, right, kept);
                    if (aligned) next[cell] = placed;
                    retainedMass += placed;
                    double origin = ledge.CellCenter(cell) + shift * 0.45;
                    if (loosened == 0 && placed > 0 && placed < mass)
                        origin = left < x ? (left + x) * 0.5 : (right + x + width) * 0.5;
                    release.Add(origin, surface, mass - placed);
                }
                if (cell % 3 == 2) Emit(ref release, ledge.Handle, vx, vy);
            }
            Emit(ref release, ledge.Handle, vx, vy);
            if (width > 0) ledge.SetShape(bounds, x, y, width, next, retainedMass);
        }

        private static double Transfer(double[] next, double x, double width, double left, double right, double mass)
        {
            double overlapLeft = Math.Max(x, left);
            double overlapRight = Math.Min(x + width, right);
            if (overlapRight <= overlapLeft) return 0;
            int start = (int)((overlapLeft - x) / SnowLedge.Cell);
            double stored = 0;
            for (int cell = start; cell < next.Length; cell++)
            {
                double cellLeft = x + cell * SnowLedge.Cell;
                if (cellLeft >= overlapRight) break;
                double overlap = Math.Min(cellLeft + SnowLedge.Cell, overlapRight) - Math.Max(cellLeft, overlapLeft);
                double amount = mass * overlap / (right - left);
                next[cell] += amount;
                stored += amount;
            }
            return Math.Min(mass, stored);
        }

        internal bool IsHoldingTree { get { return effects.IsHoldingTree; } }

        internal bool TryTreeBounds(out Rect bounds, out double visibleBottom)
        {
            if (!effects.TryTreeBounds(out bounds, out visibleBottom)) return false;
            bounds.Offset(screen.Left, screen.Top);
            visibleBottom += screen.Top;
            return true;
        }

        internal void GrabTree(Desktop.Point pointer)
        {
            effects.GrabTree(pointer.X - screen.Left, pointer.Y - screen.Top);
        }

        internal void DragTree(Desktop.Point pointer, double seconds, bool down)
        {
            effects.DragTree(pointer.X - screen.Left, pointer.Y - screen.Top, seconds, down);
        }

        internal void Trigger(Effect kind) { effects.Trigger(kind, ledges); }
        internal void ClearTrees() { effects.ClearTrees(); }

        internal void Gust()
        {
            gustTime = 1.65;
            gustDirection = random.Next(2) == 0 ? -1 : 1;
        }

        public void ResetPointer()
        {
            hasPointer = false;
            effects.DragTree(0, 0, 0, false);
        }

        public void Advance(double seconds, Settings settings, Desktop.Point pointer)
        {
            if (seconds <= 0) return;
            elapsed += seconds;
            int density = settings.Density == 0 ? 88 : settings.Density == 1 ? 165 : 270;
            int target = (int)Math.Min(MaxFlakes, density * Math.Sqrt(screen.Width * screen.Height / 2073600.0));
            while (flakeCount < target)
                flakes[flakeCount++] = NewFlake(random.NextDouble() * screen.Height);
            flakeCount = target;

            double wind = settings.Wind ? 11 + 17 * Math.Sin(elapsed * 0.32) : 0;
            if (gustTime > 0)
            {
                gustTime = Math.Max(0, gustTime - seconds);
                wind += Math.Sin(gustTime * Math.PI / 1.65) * 135 * gustDirection;
            }
            if (settings.CursorSweep && hasPointer) SweepPointer(lastPointer, pointer, seconds);
            lastPointer = pointer;
            hasPointer = true;
            double decay = Math.Exp(-5.4 * seconds);
            for (int i = 0; i < flakeCount; i++)
            {
                Flake flake = flakes[i];
                double oldX = flake.X, oldY = flake.Y;
                flake.X += (wind + flake.Drift + flake.PushX + 8 * Math.Sin(elapsed * 1.1 + i)) * seconds;
                flake.Y += (flake.Speed + flake.PushY) * seconds;
                flake.PushX *= decay;
                flake.PushY *= decay;
                bool landed = Deposit(oldX, oldY, flake.X, flake.Y, (0.9 + flake.Radius * 0.65) * Accumulation, null) <= 0.001;
                if (flake.X < -6) flake.X += screen.Width + 12;
                if (flake.X > screen.Width + 6) flake.X -= screen.Width + 12;
                if (landed || flake.Y > screen.Height + 6)
                    flake = NewFlake(-5 - random.NextDouble() * 40);
                flakes[i] = flake;
            }

            AdvanceGrains(seconds, wind);
            snowMaintenance += seconds;
            windTravel += wind * seconds;
            if (snowMaintenance >= 0.5)
            {
                double averageWind = windTravel / snowMaintenance;
                floor.MeltAndSettle(snowMaintenance);
                floor.Drift(averageWind, snowMaintenance);
                foreach (SnowLedge ledge in piles.Values)
                {
                    ledge.MeltAndSettle(snowMaintenance);
                    ledge.Drift(averageWind, snowMaintenance);
                }
                snowMaintenance = windTravel = 0;
            }
            effects.Advance(seconds, elapsed, settings, ledges);
            icicles.Advance(seconds, settings.Icicles, ledges);
        }

        private Flake NewFlake(double y)
        {
            int layer = random.Next(3);
            return new Flake
            {
                X = random.NextDouble() * screen.Width,
                Y = y,
                Layer = layer,
                Speed = 38 + layer * 20 + random.NextDouble() * 17,
                Radius = (1.0 + layer * 0.72 + random.NextDouble() * 0.85)
                    * Math.Max(1, Math.Min(1.6, screen.Height / 1080)),
                Drift = -12 + random.NextDouble() * 24
            };
        }

        private double Deposit(double oldX, double oldY, double x, double y, double mass, IntPtr? ignored)
        {
            if (y <= oldY) return mass;
            foreach (SnowLedge ledge in ledges)
            {
                if (ledge.Y > y + SnowLedge.MaxDepth) break;
                if (oldY > ledge.LowestSupport || ledge.Handle == ignored) continue;
                double t = Clamp((ledge.Y - oldY) / (y - oldY), 0, 1);
                double crossingX = oldX + (x - oldX) * t;
                if (crossingX < ledge.X || crossingX >= ledge.X + ledge.Width) continue;
                int cell = ledge.IndexAt(crossingX);
                if (!ledge.Visible[cell]) continue;
                double crest = ledge.SurfaceAt(crossingX);
                if (oldY > crest || y < crest) continue;
                mass -= ledge.Pack(cell, mass);
                if (mass <= 0.001) break;
            }
            return mass;
        }

        private void SweepPointer(Desktop.Point start, Desktop.Point end, double seconds)
        {
            double dx = (double)end.X - start.X, dy = (double)end.Y - start.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance < 1) return;
            double x = start.X - screen.Left, y = start.Y - screen.Top;
            double speed = distance / Math.Max(0.008, seconds);
            double impulse = Math.Min(0.94, distance / 28);
            double vx = Clamp(dx / seconds * 0.43, -480, 480);
            double vy = Clamp(dy / seconds * 0.27, -170, 160) - 42;
            double radius = 22 + Math.Min(27, speed / 38);
            SweepAir(x, y, dx, dy, radius, distance, speed);
            foreach (SnowLedge ledge in ledges)
            {
                if (ledge.Total <= 0 || ledge.Y < Math.Min(y, y + dy) - 12 ||
                    ledge.Y > Math.Max(y, y + dy) + SnowLedge.MaxDepth + 7) continue;
                double entry = 0, exit = 1;
                if (Math.Abs(dy) > 0.01)
                {
                    double top = (ledge.Y - SnowLedge.MaxDepth - 7 - y) / dy;
                    double bottom = (ledge.Y + 12 - y) / dy;
                    entry = Math.Max(0, Math.Min(top, bottom));
                    exit = Math.Min(1, Math.Max(top, bottom));
                }
                double left = Math.Min(x + dx * entry, x + dx * exit);
                double right = Math.Max(x + dx * entry, x + dx * exit);
                int first = Math.Max(0, ledge.IndexAt(left - radius));
                int last = Math.Min(ledge.Snow.Length - 1, ledge.IndexAt(right + radius));
                ReleasedSnow release = new ReleasedSnow();
                for (int cell = first; cell <= last; cell++)
                {
                    if (!ledge.Visible[cell]) continue;
                    double center = ledge.CellCenter(cell);
                    double falloff = 1 - Math.Min(1, Math.Abs(center - Clamp(center, left, right)) / radius);
                    falloff *= falloff;
                    double depth = ledge.Snow[cell];
                    double surface = ledge.SurfaceAt(center);
                    release.Add(center, surface, ledge.Remove(cell, depth * impulse * falloff));
                    if (cell % 2 == 1) Emit(ref release, ledge.Handle, vx, vy);
                }
                Emit(ref release, ledge.Handle, vx, vy);
            }
        }

        private void SweepAir(double x, double y, double dx, double dy, double radius, double travel, double speed)
        {
            double reachX = x + dx, reachY = y + dy;
            double minX = Math.Min(x, reachX) - radius, maxX = Math.Max(x, reachX) + radius;
            double minY = Math.Min(y, reachY) - radius, maxY = Math.Max(y, reachY) + radius;
            double radiusSquared = radius * radius;
            double strength = Math.Min(210, speed * 0.48);
            for (int index = 0; index < flakeCount; index++)
            {
                Flake flake = flakes[index];
                if (flake.X < minX || flake.X > maxX || flake.Y < minY || flake.Y > maxY) continue;
                double position = Clamp(((flake.X - x) * dx + (flake.Y - y) * dy) / (travel * travel), 0, 1);
                double awayX = flake.X - x - dx * position, awayY = flake.Y - y - dy * position;
                double distance = awayX * awayX + awayY * awayY;
                if (distance >= radiusSquared) continue;
                double push = strength * (1 - Math.Sqrt(distance) / radius);
                flake.PushX = Clamp(flake.PushX + push * (dx / travel + awayX / radius), -275, 275);
                flake.PushY = Clamp(flake.PushY + push * (dy / travel + awayY / radius), -155, 155);
                flakes[index] = flake;
            }
        }

        private void SpillPlow(SnowLedge ledge, double blade, double mass)
        {
            bool leavesRight = blade >= screen.Width - 0.01;
            int batches = Math.Max(1, Math.Min(12, (int)Math.Ceiling(mass / 36)));
            double support = ledge.SupportAt(blade);
            double height = Math.Min(17, 2 + Math.Sqrt(mass));
            for (int i = 0; i < batches; i++)
            {
                double layer = (i + 0.5) / batches;
                var spill = new ReleasedSnow();
                spill.Add(blade + (leavesRight ? -4 + 3 * layer : 2 + 4 * layer), support - layer * height, mass / batches);
                Emit(ref spill, ledge.Handle, (leavesRight ? 42 : 13) + 26 * layer,
                    41 - 22 * layer, 0.48, leavesRight);
            }
        }

        private void SprayPlow(double blade, double support, double powder, double overflow)
        {
            var spray = new ReleasedSnow();
            spray.Add(blade - 3, support - 4, powder * 0.63);
            Emit(ref spray, IntPtr.Zero, -57, -44, 0.38);
            spray.Add(blade + 1, support - 8, powder * 0.37);
            Emit(ref spray, IntPtr.Zero, 37, -49, 0.38);
            if (overflow <= 0) return;

            spray.Add(blade - 6, support - 18, overflow * 0.77);
            Emit(ref spray, IntPtr.Zero, -86, -42, 0.59);
            spray.Add(blade + 4, support - 12, overflow * 0.23);
            Emit(ref spray, IntPtr.Zero, 35, -31, 0.64);
        }

        private void KickPowder(SnowLedge ledge, double x, double mass)
        {
            double surface = ledge.SurfaceAt(x) - 1;
            var powder = new ReleasedSnow();
            powder.Add(x - 1, surface, mass * 0.55);
            Emit(ref powder, IntPtr.Zero, -46, -58);
            powder.Add(x + 1, surface, mass * 0.45);
            Emit(ref powder, IntPtr.Zero, 46, -53);
        }

        private void Emit(ref ReleasedSnow release, IntPtr source, double vx, double vy, double scatter = 1,
            bool leavesRight = false)
        {
            if (release.Mass <= 0.00001) return;
            double x = Clamp(release.WeightedX / release.Mass, 1, screen.Width - 1);
            double y = release.WeightedY / release.Mass;
            int parts = release.Mass >= 17 ? 3 : release.Mass >= 7 ? 2 : 1;
            double share = release.Mass / parts;
            for (int part = 0; part < parts; part++)
            {
                if (grains.Count == MaxGrains)
                {
                    if (leavesRight) break;
                    int last = grains.Count - 1;
                    Grain combined = grains[last];
                    combined.Mass += share * (parts - part);
                    grains[last] = combined;
                    break;
                }
                grains.Add(new Grain
                {
                    X = Clamp(x + random.NextDouble() * 5 - 2.5, 1, screen.Width - 1),
                    Y = y - random.NextDouble() * 3, VX = vx + (random.NextDouble() * 84 - 42) * scatter,
                    VY = vy + (random.NextDouble() * 64 - 32) * scatter, Mass = share, Source = source,
                    LeavesRight = leavesRight
                });
            }
            release = new ReleasedSnow();
        }

        private void AdvanceGrains(double seconds, double wind)
        {
            for (int i = grains.Count - 1; i >= 0; i--)
            {
                Grain grain = grains[i];
                double oldX = grain.X, oldY = grain.Y;
                grain.Age += seconds;
                grain.VX += (wind * 0.65 - grain.VX * 0.52) * seconds;
                if (grain.LeavesRight) grain.VX = Math.Max(35, grain.VX);
                double oldVY = grain.VY;
                grain.VY = Math.Min(510, grain.VY + Gravity * seconds);
                grain.X += grain.VX * seconds;
                grain.Y += (oldVY + grain.VY) * 0.5 * seconds;
                if (!grain.LeavesRight && (grain.X <= 0 || grain.X >= screen.Width))
                {
                    grain.X = Clamp(grain.X, 0.2, screen.Width - 0.2);
                    grain.VX *= -0.28;
                }
                IntPtr? ignore = grain.LeavesRight || (grain.Age < 0.22 && grain.Source != IntPtr.Zero)
                    ? (IntPtr?)grain.Source : null;
                grain.Mass = Deposit(oldX, oldY, grain.X, grain.Y, grain.Mass, ignore);
                if (grain.Mass <= 0.001 || grain.Y > screen.Height + 14 || (grain.LeavesRight && grain.X >= screen.Width + 4))
                {
                    int last = grains.Count - 1;
                    grains[i] = grains[last];
                    grains.RemoveAt(last);
                }
                else grains[i] = grain;
            }
        }

        public void Draw(Canvas drawing)
        {
            foreach (SnowLedge ledge in ledges) ledge.Draw(drawing, PileEdge);
            icicles.Draw(drawing);
            effects.Draw(drawing);
            for (int i = 0; i < flakeCount; i++)
            {
                Flake flake = flakes[i];
                if (flake.Layer == 2 && flake.Radius > 2.8 && i % 5 == 0)
                    drawing.DrawCrystal(CrystalRay, flake.X, flake.Y, flake.Radius * 1.18,
                        elapsed * (0.41 + (i % 7) * 0.12) + i);
                else
                {
                    double flutter = 0.78 + 0.22 * Math.Abs(Math.Sin(elapsed * 1.46 + i));
                    drawing.DrawEllipse(FlakeBrushes[flake.Layer], flake.Layer == 2 ? FlakeOutline : null,
                        new Point(flake.X, flake.Y), flake.Radius, flake.Radius * flutter);
                }
            }
            foreach (Grain grain in grains)
            {
                double radius = Clamp(0.65 + Math.Sqrt(grain.Mass) * 0.46, 0.9, 3.3);
                drawing.DrawEllipse(FlakeBrushes[2], null, new Point(grain.X, grain.Y),
                    radius, radius * 0.78);
                if (grain.Mass > 6)
                    drawing.DrawEllipse(FlakeBrushes[1], null,
                        new Point(grain.X - radius * 1.6, grain.Y - radius * 1.24), radius * 0.47, radius * 0.47);
            }
        }

        internal static SolidBrush Brush(byte alpha, byte red, byte green, byte blue)
        {
            return new SolidBrush(Color.FromArgb(alpha, red, green, blue));
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        public void Dispose()
        {
            floor.Dispose();
            foreach (SnowLedge ledge in piles.Values) ledge.Dispose();
        }
    }
}
