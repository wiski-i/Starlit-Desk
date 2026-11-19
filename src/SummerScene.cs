using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using Rect = System.Windows.Rect;
using WPoint = System.Windows.Point;
using Forms = System.Windows.Forms;

namespace SnowDesk
{
    /// <summary>
    /// Summer shower scene: warm rain, water that collects on window tops (<see cref="WaterLedge"/>), runs off
    /// their ends and is thrown off when a window is dragged or shaken, puddles on the taskbar edge and a
    /// rain/sun cycle that drives the characters in <see cref="SummerCast"/>.
    /// </summary>
    internal sealed class SummerScene : DesktopScene
    {
        private const int MaxDrops = 520, MaxBeads = 420, MaxRipples = 90, MaxLedges = 32;
        private const double Gravity = 520;
        private static readonly Pen[] TailPens = { Line(22, 205, 225, 245, 1f), Line(38, 210, 230, 248, 1f), Line(56, 220, 236, 252, 1.1f) };
        private static readonly Pen[] HeadPens = { Line(60, 214, 232, 250, 1f), Line(95, 222, 238, 252, 1.1f), Line(140, 234, 245, 255, 1.25f) };
        private static readonly Pen[] RipplePens = { Line(45, 226, 244, 255, 1f), Line(90, 226, 244, 255, 1f), Line(150, 232, 246, 255, 1.1f) };
        private static readonly Brush MistBrush = SnowField.Brush(170, 220, 238, 255);
        private static readonly Brush BeadBrush = SnowField.Brush(205, 168, 214, 244);
        private static readonly Brush BeadShine = SnowField.Brush(230, 250, 253, 255);

        private readonly Random random = new Random(Guid.NewGuid().GetHashCode());
        private readonly Rect screen;
        private readonly Drop[] drops = new Drop[MaxDrops];
        private readonly List<Bead> beads = new List<Bead>(MaxBeads);
        private readonly List<Ripple> ripples = new List<Ripple>(MaxRipples);
        private readonly Dictionary<IntPtr, WaterLedge> ledges = new Dictionary<IntPtr, WaterLedge>();
        private readonly List<WaterLedge> visibleLedges = new List<WaterLedge>(MaxLedges);
        private readonly HashSet<IntPtr> seen = new HashSet<IntPtr>();
        private readonly List<IntPtr> gone = new List<IntPtr>();
        private readonly List<Pool> pools = new List<Pool>(3);
        private readonly List<RectangleF> blockers = new List<RectangleF>();
        private readonly SummerCast cast;
        private int dropCount;
        private long lastGeometry;
        private bool seeded, raining = true, ledgesOn = true;
        private double phaseLeft, intensity = 1, elapsed, sunTime, rainbow, sunGlow, gust, gustDirection = 1;

        private struct Drop
        {
            internal double X, Y, Speed, Length;
            internal int Layer;
        }

        /// <summary>A flying drop of water. Beads with mass land on ledges and puddles; mist is only spray.</summary>
        private struct Bead
        {
            internal double X, Y, VX, VY, Age, Life, Mass;
            internal IntPtr Source;
        }

        private struct Ripple
        {
            internal double X, Y, Age, Size;
        }

        /// <summary>A puddle that grows from its centre along the ground line.</summary>
        internal sealed class Pool
        {
            internal double X, Reach, Volume, Drain, Seed;
            internal bool Pond;
            internal double Half { get { return Reach * Math.Sqrt(Volume); } }
            internal double Depth(double scale) { return (1.5 + 11 * Volume) * scale; }

            /// <summary>Flattened lens profile: 1 at the middle, 0 at the rim.</summary>
            internal static double Profile(double t)
            {
                double edge = 1 - t * t * t * t;
                return edge <= 0 ? 0 : Math.Sqrt(edge);
            }

            /// <summary>Water surface height at x, or NaN outside the puddle.</summary>
            internal double SurfaceAt(double x, double ground, double scale)
            {
                double half = Half;
                if (half < 2 || Math.Abs(x - X) >= half) return double.NaN;
                return ground - Depth(scale) * Profile((x - X) / half);
            }

            internal void Absorb(double mass)
            {
                Volume = Math.Min(1, Volume + mass * 2.2 / (Reach * 100));
            }
        }

        internal SummerScene(Forms.Screen display) : this(display.Bounds, display.WorkingArea, 1, 0) { }

        /// <summary>Preview scene for the theme card: faster weather and smaller figures.</summary>
        internal SummerScene(Rectangle viewport) : this(viewport, viewport, 3.2, 0.62) { }

        private SummerScene(Rectangle bounds, Rectangle work, double pace, double artScale)
        {
            screen = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            Width = bounds.Width;
            Height = bounds.Height;
            Ground = Math.Min(Height - 1, work.Bottom - bounds.Y);
            if (Ground < Height * 0.5) Ground = Height - 1;
            Scale = artScale > 0 ? artScale : Math.Max(1.2, Math.Min(1.9, Height / 900.0));
            Pace = pace;
            phaseLeft = 50 + random.NextDouble() * 20;
            double main = Width * (0.34 + random.NextDouble() * 0.3);
            pools.Add(new Pool { X = main, Reach = 96 * Scale, Volume = pace > 1 ? 0.62 : 0.3, Seed = random.NextDouble() * 9 });
            double pond = main + (random.Next(2) == 0 ? -1 : 1) * Math.Max(Width * 0.24, 190 * Scale);
            if (pond < 70 * Scale || pond > Width - 70 * Scale) pond = main + (main > Width * 0.5 ? -1 : 1) * Width * 0.24;
            pools.Add(new Pool { X = pond, Reach = 64 * Scale, Volume = pace > 1 ? 0.6 : 0.34, Pond = true, Seed = random.NextDouble() * 9 });
            if (Width > 1500 * Scale)
                pools.Add(new Pool { X = Width * (main > Width * 0.5 ? 0.12 : 0.88), Reach = 46 * Scale, Volume = 0.2, Seed = 3 });
            cast = new SummerCast(this);
        }

        internal double Width { get; private set; }
        internal double Height { get; private set; }
        internal double Ground { get; private set; }
        internal double Scale { get; private set; }
        internal double Pace { get; private set; }
        internal bool Raining { get { return raining; } }
        internal double Clock { get { return elapsed; } }
        internal Random Random { get { return random; } }
        internal Pool Main { get { return pools[0]; } }
        internal Pool Pond { get { return pools[1]; } }
        internal IList<Pool> Pools { get { return pools; } }

        // Window outlines also keep the rainbow and sun off open windows, so they are read for either feature.
        public bool UsesWindowGeometry(Settings settings)
        {
            return settings.WindowLedges || settings.Has(SummerEffect.Rainbow);
        }

        public void ResetPointer() { }

        /// <summary>Starts a shower or sunshine right away (panel and tray actions).</summary>
        internal void SetWeather(bool rain)
        {
            raining = rain;
            sunTime = 0;
            phaseLeft = rain ? 55 + random.NextDouble() * 20 : 34 + random.NextDouble() * 12;
            if (!rain) cast.OnSunshine();
        }

        internal void Gust()
        {
            gust = 1.6;
            gustDirection = random.Next(2) == 0 ? -1 : 1;
        }

        internal void CallPump() { cast.CallPump(); }
        internal void SendRunner() { cast.SendRunner(); }

        internal void InviteBather()
        {
            Main.Volume = Math.Max(Main.Volume, 0.7);
            cast.InviteBather();
        }

        public void UpdateLedges(List<Desktop.WindowBounds> windows)
        {
            long now = Stopwatch.GetTimestamp();
            double scanTime = Math.Max(0.016, Math.Min(0.1, (double)(now - lastGeometry) / Stopwatch.Frequency));
            lastGeometry = now;
            visibleLedges.Clear();
            seen.Clear();
            blockers.Clear();
            foreach (Desktop.WindowBounds window in windows)
                blockers.Add(new RectangleF((float)(window.Bounds.X - screen.Left), (float)(window.Bounds.Y - screen.Top),
                    (float)window.Bounds.Width, (float)window.Bounds.Height));
            for (int i = 0; i < windows.Count && visibleLedges.Count < MaxLedges; i++)
            {
                Rect bounds = windows[i].Bounds;
                double left = Math.Max(screen.Left, bounds.Left), right = Math.Min(screen.Right, bounds.Right);
                if (bounds.Top <= screen.Top + 2 || bounds.Top - screen.Top >= Ground - 12 || right - left < 32) continue;
                WaterLedge ledge = Locate(windows[i].Handle, bounds, left - screen.Left, bounds.Top - screen.Top,
                    right - left, scanTime);
                bool any = false;
                for (int cell = 0; cell < ledge.Visible.Length; cell++)
                {
                    double probe = screen.Left + ledge.CellCenter(cell);
                    bool visible = true;
                    for (int front = 0; front < i && visible; front++)
                        visible = !windows[front].Bounds.Contains(probe, bounds.Top - 1);
                    ledge.Visible[cell] = visible;
                    any |= visible;
                }
                seen.Add(ledge.Handle);
                if (any) visibleLedges.Add(ledge);
            }
            gone.Clear();
            foreach (var pair in ledges)
                if (!seen.Contains(pair.Key)) gone.Add(pair.Key);
            foreach (IntPtr handle in gone)
            {
                // A closed or minimised window drops all of its water where it stood.
                WaterLedge ledge = ledges[handle];
                ledge.Release(1, (x, y, mass) => Throw(x, y, mass, 0, 10, IntPtr.Zero));
                ledges.Remove(handle);
            }
        }

        /// <summary>
        /// Finds the ledge of a window and follows its movement. Water travels with the window, but a share
        /// that grows with the jump stays behind and is thrown with the window's speed, as snow does.
        /// </summary>
        private WaterLedge Locate(IntPtr handle, Rect bounds, double x, double y, double width, double scanTime)
        {
            WaterLedge ledge;
            if (!ledges.TryGetValue(handle, out ledge))
            {
                ledge = new WaterLedge(handle);
                ledge.Place(bounds, x, y, width);
                ledges.Add(handle, ledge);
                return ledge;
            }
            if (ledge.X == x && ledge.Y == y && ledge.Width == width) return ledge;
            double shift = bounds.Width == ledge.Window.Width ? bounds.Left - ledge.Window.Left : 0;
            double vertical = y - ledge.Y;
            double travel = Math.Sqrt(shift * shift + vertical * vertical);
            double loosened = 1 - Math.Exp(-travel / 28);
            double vx = Clamp(shift / scanTime * 0.55, -480, 480);
            double vy = Clamp(vertical / scanTime * 0.35, -150, 170);
            if (loosened > 0.01)
                ledge.Release(loosened, (px, py, mass) => Throw(px + shift * 0.25, py, mass, vx, vy - 30, handle));
            ledge.Place(bounds, x, y, width);
            return ledge;
        }

        /// <summary>Releases water as one to three beads with a little scatter around (vx, vy).</summary>
        private void Throw(double x, double y, double mass, double vx, double vy, IntPtr source)
        {
            int parts = mass > WaterLedge.DropMass * 3 ? 3 : mass > WaterLedge.DropMass * 1.5 ? 2 : 1;
            for (int part = 0; part < parts; part++)
            {
                if (beads.Count >= MaxBeads)
                {
                    int last = beads.Count - 1;
                    Bead merged = beads[last];
                    merged.Mass += mass * (parts - part) / parts;
                    beads[last] = merged;
                    return;
                }
                beads.Add(new Bead
                {
                    X = x + (random.NextDouble() - 0.5) * 6, Y = y - random.NextDouble() * 2, Mass = mass / parts, Life = 8,
                    VX = vx + (random.NextDouble() * 2 - 1) * 38, VY = vy - random.NextDouble() * 30, Source = source
                });
            }
        }

        /// <summary>Throws a few droplets of spray up from (x, y); used by rain, runners, kicks and the pump.</summary>
        internal void Splash(double x, double y, int count, double strength)
        {
            for (int i = 0; i < count && beads.Count < MaxBeads; i++)
                beads.Add(new Bead
                {
                    X = x, Y = y, Life = 0.3 + random.NextDouble() * 0.25,
                    VX = (random.NextDouble() * 2 - 1) * 70 * strength * Scale,
                    VY = -(45 + random.NextDouble() * 70) * strength * Scale
                });
        }

        public void Advance(double seconds, Settings settings, Desktop.Point pointer)
        {
            if (seconds <= 0) return;
            elapsed += seconds;
            ledgesOn = settings.WindowLedges;
            AdvanceWeather(seconds, settings);
            double wind = settings.Wind ? 24 + 32 * Math.Sin(elapsed * 0.27) : 0;
            if (gust > 0)
            {
                gust = Math.Max(0, gust - seconds);
                wind += Math.Sin(gust * Math.PI / 1.6) * 170 * gustDirection;
            }
            AdvanceDrops(seconds, settings, wind);
            AdvanceWater(seconds, settings);
            AdvanceBeads(seconds, wind);
            for (int i = ripples.Count - 1; i >= 0; i--)
            {
                Ripple ripple = ripples[i];
                ripple.Age += seconds;
                if (ripple.Age >= 0.8) ripples.RemoveAt(i);
                else ripples[i] = ripple;
            }
            cast.Advance(seconds, settings);
        }

        private void AdvanceWeather(double seconds, Settings settings)
        {
            phaseLeft -= seconds * Pace;
            if (phaseLeft <= 0) SetWeather(!raining);
            if (!raining) sunTime += seconds * Pace;
            double ease = 1 - Math.Exp(-seconds * Pace * 0.45);
            intensity += ((raining ? 1 : 0) - intensity) * ease;
            double rainbowTarget = !raining && sunTime < 24 && settings.Has(SummerEffect.Rainbow) ? 1 - intensity : 0;
            rainbow += (rainbowTarget - rainbow) * (1 - Math.Exp(-seconds * 0.8));
            sunGlow += ((raining || !settings.Has(SummerEffect.Rainbow) ? 0 : 1) - sunGlow) * (1 - Math.Exp(-seconds * 0.6));
        }

        private void AdvanceDrops(double seconds, Settings settings, double wind)
        {
            int density = settings.Density == 0 ? 90 : settings.Density == 1 ? 170 : 290;
            int target = (int)Math.Min(MaxDrops, density * intensity * Math.Sqrt(Width * Height / 2073600.0));
            for (int i = dropCount - 1; i >= 0; i--)
            {
                Drop drop = drops[i];
                double oldX = drop.X, oldY = drop.Y;
                drop.X += wind * (0.7 + drop.Layer * 0.2) * seconds;
                drop.Y += drop.Speed * seconds;
                bool landed = drop.Layer > 0 && Land(oldY, drop.X, drop.Y, drop.Layer);
                if (landed || drop.Y > Ground + 4)
                {
                    if (dropCount > target)
                    {
                        drops[i] = drops[--dropCount];
                        continue;
                    }
                    drop = NewDrop(-random.NextDouble() * 50);
                }
                if (drop.X < -10) drop.X += Width + 20;
                else if (drop.X > Width + 10) drop.X -= Width + 20;
                drops[i] = drop;
            }
            while (dropCount < target)
                drops[dropCount++] = NewDrop(seeded ? -random.NextDouble() * 60 : random.NextDouble() * Ground);
            seeded = true;
        }

        private Drop NewDrop(double y)
        {
            int layer = random.Next(3);
            double size = Height < 400 ? 0.55 : Math.Min(1.3, Scale);
            return new Drop
            {
                X = random.NextDouble() * Width, Y = y, Layer = layer,
                Speed = (560 + layer * 160 + random.NextDouble() * 90) * size,
                Length = (5 + layer * 2.6 + random.NextDouble() * 2.5) * size
            };
        }

        private bool Land(double oldY, double x, double y, int layer)
        {
            if (ledgesOn)
                foreach (WaterLedge ledge in visibleLedges)
                {
                    if (x < ledge.X || x >= ledge.X + ledge.Width || !ledge.VisibleAt(x)) continue;
                    double surface = ledge.SurfaceAt(x);
                    if (oldY > surface || y < surface) continue;
                    ledge.Add(x, 0.5 * layer);
                    if (random.NextDouble() < 0.4) Splash(x, surface, 2, 0.55);
                    return true;
                }
            if (y < Ground - 14 * Scale) return false;
            foreach (Pool pool in pools)
            {
                double surface = pool.SurfaceAt(x, Ground, Scale);
                if (double.IsNaN(surface) || oldY > surface || y < surface) continue;
                if (ripples.Count < MaxRipples && random.NextDouble() < 0.55)
                    ripples.Add(new Ripple { X = x, Y = surface + 1, Size = (3 + random.NextDouble() * 4) * Scale });
                return true;
            }
            if (y < Ground) return false;
            if (random.NextDouble() < 0.4) Splash(x, Ground - 1, 2, 0.8);
            return true;
        }

        private void AdvanceWater(double seconds, Settings settings)
        {
            double rate = seconds * Pace;
            double fill = 0.022 * intensity * (0.75 + settings.Density * 0.25);
            foreach (Pool pool in pools)
            {
                pool.Volume += raining ? fill * rate * (pool.Pond ? 1.15 : 1) : -0.027 * rate;
                pool.Volume -= pool.Drain * seconds;
                pool.Volume = Math.Max(0, Math.Min(1, pool.Volume));
            }
            foreach (WaterLedge ledge in ledges.Values)
            {
                if (!ledgesOn)
                {
                    ledge.Evaporate(WaterLedge.MaxDepth);
                    continue;
                }
                if (raining) ledge.Rain(0.14 * intensity * (0.7 + settings.Density * 0.3) * rate);
                else ledge.Evaporate(0.11 * rate);
                IntPtr source = ledge.Handle;
                ledge.Flow(seconds, (x, y, mass) => Throw(x, y, mass, 0, 6, source));
            }
        }

        private void AdvanceBeads(double seconds, double wind)
        {
            for (int i = beads.Count - 1; i >= 0; i--)
            {
                Bead bead = beads[i];
                double oldX = bead.X, oldY = bead.Y;
                bead.Age += seconds;
                bead.VX += (wind * 0.3 - bead.VX * 0.6) * seconds;
                bead.VY = Math.Min(900, bead.VY + Gravity * seconds);
                bead.X += bead.VX * seconds;
                bead.Y += bead.VY * seconds;
                bool done = bead.Age >= bead.Life || bead.Y > Height + 6 || bead.X < -20 || bead.X > Width + 20;
                if (!done && bead.Mass > 0) done = Deposit(oldY, ref bead);
                else if (!done) done = bead.Y >= Ground;
                if (done)
                {
                    beads[i] = beads[beads.Count - 1];
                    beads.RemoveAt(beads.Count - 1);
                }
                else beads[i] = bead;
            }
        }

        /// <summary>Lands a falling bead on a window ledge or puddle below it; true once it is absorbed.</summary>
        private bool Deposit(double oldY, ref Bead bead)
        {
            if (bead.Y <= oldY) return false;
            if (ledgesOn)
                foreach (WaterLedge ledge in visibleLedges)
                {
                    if (bead.X < ledge.X || bead.X >= ledge.X + ledge.Width || !ledge.VisibleAt(bead.X)) continue;
                    if (ledge.Handle == bead.Source && bead.Age < 0.3) continue;
                    double surface = ledge.SurfaceAt(bead.X);
                    if (oldY > surface + 1 || bead.Y < surface) continue;
                    ledge.Add(bead.X, bead.Mass);
                    if (bead.VY > 260) Splash(bead.X, surface, 2, 0.5);
                    return true;
                }
            if (bead.Y < Ground - 14 * Scale) return false;
            Pool target = null;
            double nearest = 60 * Scale;
            foreach (Pool pool in pools)
            {
                double surface = pool.SurfaceAt(bead.X, Ground, Scale);
                if (!double.IsNaN(surface) && bead.Y >= surface)
                {
                    target = pool;
                    break;
                }
                double gap = Math.Abs(bead.X - pool.X) - pool.Half;
                if (bead.Y >= Ground && gap < nearest)
                {
                    nearest = gap;
                    target = pool;
                }
            }
            if (target == null) return bead.Y >= Ground && SplashOut(bead.X);
            target.Absorb(bead.Mass);
            if (ripples.Count < MaxRipples)
                ripples.Add(new Ripple { X = bead.X, Y = Ground - target.Depth(Scale) * 0.5, Size = 6 * Scale });
            return true;
        }

        private bool SplashOut(double x)
        {
            Splash(x, Ground - 1, 2, 0.7);
            return true;
        }

        public void Draw(Canvas drawing)
        {
            if (sunGlow > 0.02 || rainbow > 0.02) drawing.DrawArt(0, 0, 1, false, DrawSky);
            if (ledgesOn && visibleLedges.Count > 0) drawing.DrawArt(0, 0, 1, false, DrawLedges);
            drawing.DrawArt(0, 0, 1, false, g =>
            {
                foreach (Pool pool in pools) SummerArt.Puddle(g, pool, Ground, Scale, elapsed);
            });
            cast.DrawWater(drawing);
            cast.Draw(drawing);
            foreach (Ripple ripple in ripples)
            {
                double t = ripple.Age / 0.8;
                Pen pen = RipplePens[Math.Min(2, (int)((1 - t) * 3))];
                double radius = ripple.Size * (0.3 + t);
                drawing.StrokeEllipse(pen, new WPoint(ripple.X, ripple.Y), radius, radius * 0.26);
                if (ripple.Size > 5 && t < 0.6)
                    drawing.StrokeEllipse(pen, new WPoint(ripple.X, ripple.Y), radius * 0.5, radius * 0.13);
            }
            for (int i = 0; i < dropCount; i++)
            {
                Drop drop = drops[i];
                double slant = drop.Length * 0.06;
                var tail = new WPoint(drop.X - slant, drop.Y - drop.Length);
                var neck = new WPoint(drop.X - slant * 0.35, drop.Y - drop.Length * 0.35);
                drawing.DrawLine(TailPens[drop.Layer], tail, neck);
                drawing.DrawLine(HeadPens[drop.Layer], neck, new WPoint(drop.X, drop.Y));
            }
            foreach (Bead bead in beads)
            {
                if (bead.Mass <= 0)
                {
                    drawing.DrawEllipse(MistBrush, null, new WPoint(bead.X, bead.Y), 0.9, 0.9);
                    continue;
                }
                double radius = Clamp(0.9 + Math.Sqrt(bead.Mass) * 0.55, 1, 3.4);
                double stretch = 1 + Math.Min(0.9, Math.Abs(bead.VY) / 650);
                drawing.DrawEllipse(BeadBrush, null, new WPoint(bead.X, bead.Y), radius, radius * stretch);
                drawing.DrawEllipse(BeadShine, null, new WPoint(bead.X - radius * 0.35, bead.Y - radius * 0.4),
                    radius * 0.32, radius * 0.32);
            }
        }

        private void DrawLedges(Graphics g)
        {
            foreach (WaterLedge ledge in visibleLedges)
            {
                using (var body = new LinearGradientBrush(new PointF(0, (float)(ledge.Y - WaterLedge.MaxDepth)),
                    new PointF(0, (float)ledge.Y + 1), Color.FromArgb(150, 176, 222, 246), Color.FromArgb(205, 70, 138, 196)))
                    ledge.Draw(g, body, elapsed);
            }
        }

        private void DrawSky(Graphics g)
        {
            // Sky art stays on the bare desktop: never over open windows or below the ground line.
            g.IntersectClip(new RectangleF(0, 0, (float)Width, (float)Ground));
            foreach (RectangleF blocker in blockers) g.ExcludeClip(Rectangle.Round(blocker));
            if (sunGlow > 0.02)
                SummerArt.Sun(g, (float)(Width - 86 * Scale), (float)(78 * Scale), (float)(26 * Scale), sunGlow, elapsed);
            if (rainbow > 0.02)
                SummerArt.Rainbow(g, (float)(Width * 0.5), (float)Ground, (float)Math.Min(Width * 0.42, Ground * 0.86), rainbow);
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private static Pen Line(int alpha, int red, int green, int blue, float width)
        {
            return new Pen(Color.FromArgb(alpha, red, green, blue), width) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        }

        public void Dispose() { }
    }
}
