using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using Rect = System.Windows.Rect;
using Forms = System.Windows.Forms;

namespace SnowDesk
{
    internal enum Season { Autumn, Spring }

    /// <summary>Characters and set pieces of a season, driven by <see cref="SeasonScene"/>.</summary>
    internal interface ISeasonCast
    {
        void Advance(double seconds, Settings settings);
        /// <summary>Drawn before the ground pile (pumpkins, flower stems behind fallen petals).</summary>
        void DrawBack(Canvas canvas);
        /// <summary>Drawn over the piles (people, animals, insects).</summary>
        void DrawFront(Canvas canvas);
        /// <summary>Runs a panel action; the meaning of <paramref name="command"/> belongs to each cast.</summary>
        void Trigger(int command);
    }

    /// <summary>
    /// Autumn leaves or spring blossom: pieces tumble down, settle on window tops and the ground, travel with
    /// their window, and are lifted again by shaking a window, by the cursor or by gusts of wind.
    /// </summary>
    internal sealed class SeasonScene : DesktopScene
    {
        private const int MaxFlakes = 700, MaxLedges = 32;
        private readonly Random random = new Random(Guid.NewGuid().GetHashCode());
        private readonly Rect screen;
        private readonly List<Flake> flakes = new List<Flake>(MaxFlakes);
        private readonly Dictionary<IntPtr, PieceLedge> ledges = new Dictionary<IntPtr, PieceLedge>();
        private readonly List<PieceLedge> visibleLedges = new List<PieceLedge>(MaxLedges);
        private readonly HashSet<IntPtr> seen = new HashSet<IntPtr>();
        private readonly List<IntPtr> gone = new List<IntPtr>();
        private readonly PieceLedge ground;
        private readonly PieceLedge.Painter paint;
        private readonly ISeasonCast cast;
        private int ambient;
        private long lastGeometry;
        private bool seeded, hasPointer, ledgesOn = true;
        private Desktop.Point lastPointer;
        private double elapsed, gust, gustDirection = 1, nextGust = 9, gale;

        private struct Flake
        {
            internal double X, Y, VX, VY, Angle, Spin, Phase, Sway, Flip, FlipRate;
            internal float Size;
            internal byte Kind, Tint;
            internal bool Ambient;
        }

        internal SeasonScene(Forms.Screen display, Season season) : this(display.Bounds, display.WorkingArea, season, 1, 0) { }

        /// <summary>Preview scene for the theme card: smaller figures and a faster cast.</summary>
        internal SeasonScene(Rectangle viewport, Season season) : this(viewport, viewport, season, 2.5, 0.62) { }

        private SeasonScene(Rectangle bounds, Rectangle work, Season season, double pace, double artScale)
        {
            screen = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            Season = season;
            Width = bounds.Width;
            Height = bounds.Height;
            Ground = Math.Min(Height - 1, work.Bottom - bounds.Y);
            if (Ground < Height * 0.5) Ground = Height - 1;
            Scale = artScale > 0 ? artScale : Math.Max(1.2, Math.Min(1.9, Height / 900.0));
            Pace = pace;
            paint = (g, piece, x, y) => SeasonArt.Piece(g, season, piece.Kind, piece.Tint, piece.Size, piece.Angle, 1, x, y, true);
            ground = new PieceLedge(IntPtr.Zero, paint, (float)((season == Season.Autumn ? 20 : 8) * Scale), 5);
            ground.Place(new Rect(0, Ground, Width, 1), 0, Ground, Width);
            for (int cell = 0; cell < ground.Visible.Length; cell++) ground.Visible[cell] = true;
            if (season == Season.Autumn) cast = new AutumnCast(this);
            else cast = new SpringCast(this);
        }

        internal Season Season { get; private set; }
        internal double Width { get; private set; }
        internal double Height { get; private set; }
        internal double Ground { get; private set; }
        internal double Scale { get; private set; }
        internal double Pace { get; private set; }
        internal double Clock { get { return elapsed; } }
        internal double Wind { get; private set; }
        /// <summary>Cursor position in scene coordinates (updated every frame).</summary>
        internal double PointerX { get; private set; }
        internal double PointerY { get; private set; }
        internal Random Random { get { return random; } }
        internal PieceLedge GroundPile { get { return ground; } }
        internal ISeasonCast Cast { get { return cast; } }

        public bool UsesWindowGeometry(Settings settings) { return settings.WindowLedges; }

        public void ResetPointer() { hasPointer = false; }

        /// <summary>Strength of a running gale, 0..1, and the way it blows (-1 or 1).</summary>
        internal double GaleStrength { get; private set; }
        internal double GaleDirection { get; private set; }

        /// <summary>
        /// A storm-force wind for a few seconds: it strips most pieces off the ground and the window tops and
        /// carries them off the screen.
        /// </summary>
        internal void Gale(double direction)
        {
            gale = 5;
            GaleDirection = direction < 0 ? -1 : 1;
        }

        internal void Gust()
        {
            gust = 2.2;
            gustDirection = random.Next(2) == 0 ? -1 : 1;
        }

        public void UpdateLedges(List<Desktop.WindowBounds> windows)
        {
            long now = Stopwatch.GetTimestamp();
            double scanTime = Math.Max(0.016, Math.Min(0.1, (double)(now - lastGeometry) / Stopwatch.Frequency));
            lastGeometry = now;
            visibleLedges.Clear();
            seen.Clear();
            for (int i = 0; i < windows.Count && visibleLedges.Count < MaxLedges; i++)
            {
                Rect bounds = windows[i].Bounds;
                double left = Math.Max(screen.Left, bounds.Left), right = Math.Min(screen.Right, bounds.Right);
                if (bounds.Top <= screen.Top + 2 || bounds.Top - screen.Top >= Ground - 12 || right - left < 32) continue;
                PieceLedge ledge = Locate(windows[i].Handle, bounds, left - screen.Left, bounds.Top - screen.Top,
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
                PieceLedge ledge = ledges[handle];
                ledge.Lift(double.MinValue, double.MaxValue, 1, random, (x, y, piece) => Launch(x, y, piece, 0, -20));
                ledge.Dispose();
                ledges.Remove(handle);
            }
        }

        /// <summary>Follows a window; a jump shakes off a share of its pieces with the window's speed.</summary>
        private PieceLedge Locate(IntPtr handle, Rect bounds, double x, double y, double width, double scanTime)
        {
            PieceLedge ledge;
            if (!ledges.TryGetValue(handle, out ledge))
            {
                ledge = new PieceLedge(handle, paint, (float)((Season == Season.Autumn ? 14 : 6) * Scale), 3);
                ledge.Place(bounds, x, y, width);
                ledges.Add(handle, ledge);
                return ledge;
            }
            if (ledge.X == x && ledge.Y == y && ledge.Width == width) return ledge;
            double shift = bounds.Width == ledge.Window.Width ? bounds.Left - ledge.Window.Left : 0;
            double vertical = y - ledge.Y;
            double travel = Math.Sqrt(shift * shift + vertical * vertical);
            double loosened = 1 - Math.Exp(-travel / 36);
            double vx = Clamp(shift / scanTime * 0.5, -420, 420);
            double vy = Clamp(vertical / scanTime * 0.35, -160, 140) - 50;
            if (loosened > 0.02 && ledge.Count > 0)
                ledge.Lift(double.MinValue, double.MaxValue, loosened, random, (px, py, piece) => Launch(px, py, piece, vx, vy));
            ledge.Place(bounds, x, y, width);
            return ledge;
        }

        /// <summary>Sends a resting piece flying again with (vx, vy) plus some scatter.</summary>
        internal void Launch(double x, double y, PieceLedge.Piece piece, double vx, double vy)
        {
            if (flakes.Count >= MaxFlakes) return;
            flakes.Add(new Flake
            {
                X = x, Y = y - 1, Size = piece.Size, Kind = piece.Kind, Tint = piece.Tint, Angle = piece.Angle,
                VX = vx + (random.NextDouble() * 2 - 1) * 45, VY = vy - random.NextDouble() * 60,
                Spin = (random.NextDouble() * 2 - 1) * 260, Phase = random.NextDouble() * 6.28,
                Sway = 1.6 + random.NextDouble() * 1.4, FlipRate = 3 + random.NextDouble() * 5
            });
        }

        /// <summary>Something hit the ground at x: nearby fallen pieces jump up.</summary>
        internal void Splash(double x, double y)
        {
            double reach = 30 * Scale;
            ground.Lift(x - reach, x + reach, 0.6, random,
                (px, py, piece) => Launch(px, py, piece, (px - x) * 3, -90 - random.NextDouble() * 90));
        }

        public void Advance(double seconds, Settings settings, Desktop.Point pointer)
        {
            if (seconds <= 0) return;
            elapsed += seconds;
            ledgesOn = settings.WindowLedges;
            AdvanceWind(seconds, settings);
            if (settings.CursorSweep && hasPointer) Sweep(lastPointer, pointer, seconds);
            lastPointer = pointer;
            hasPointer = true;
            PointerX = pointer.X - screen.Left;
            PointerY = pointer.Y - screen.Top;
            AdvanceFlakes(seconds, settings);
            cast.Advance(seconds, settings);
        }

        private void AdvanceWind(double seconds, Settings settings)
        {
            double wind = settings.Wind ? 16 + 24 * Math.Sin(elapsed * 0.23) : 4 * Math.Sin(elapsed * 0.4);
            if (settings.Wind && elapsed >= nextGust)
            {
                Gust();
                nextGust = elapsed + 16 + random.NextDouble() * 18;
            }
            if (gust > 0)
            {
                gust = Math.Max(0, gust - seconds);
                double strength = Math.Sin(gust * Math.PI / 2.2);
                wind += strength * 150 * gustDirection;
                // A gust lifts a few pieces off the ground and the window tops.
                double chance = strength * seconds * 0.5;
                ground.Lift(double.MinValue, double.MaxValue, chance * 0.4, random,
                    (x, y, piece) => Launch(x, y, piece, gustDirection * 120, -90));
                foreach (PieceLedge ledge in visibleLedges)
                    ledge.Lift(double.MinValue, double.MaxValue, chance, random,
                        (x, y, piece) => Launch(x, y, piece, gustDirection * 140, -60));
            }
            GaleStrength = 0;
            if (gale > 0)
            {
                gale = Math.Max(0, gale - seconds);
                GaleStrength = Math.Sin(gale * Math.PI / 5);
                wind += GaleStrength * 430 * GaleDirection;
                double chance = GaleStrength * seconds * 1.4;
                double push = GaleDirection * 320;
                ground.Lift(double.MinValue, double.MaxValue, chance * 0.6, random, (x, y, piece) => Launch(x, y, piece, push, -150));
                foreach (PieceLedge ledge in visibleLedges)
                    ledge.Lift(double.MinValue, double.MaxValue, chance, random, (x, y, piece) => Launch(x, y, piece, push, -110));
            }
            Wind = wind;
        }

        /// <summary>A quick cursor flick through a pile throws the pieces it passes along its path.</summary>
        private void Sweep(Desktop.Point start, Desktop.Point end, double seconds)
        {
            double dx = (double)end.X - start.X, dy = (double)end.Y - start.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            double speed = distance / Math.Max(0.008, seconds);
            if (speed < 260) return;
            double x0 = Math.Min(start.X, end.X) - screen.Left, x1 = Math.Max(start.X, end.X) - screen.Left;
            double y0 = Math.Min(start.Y, end.Y) - screen.Top, y1 = Math.Max(start.Y, end.Y) - screen.Top;
            double reach = 18 * Scale;
            double vx = Clamp(dx / seconds * 0.45, -460, 460), vy = Clamp(dy / seconds * 0.25, -200, 120) - 120;
            Action<double, double, PieceLedge.Piece> kick = (x, y, piece) => Launch(x, y, piece, vx, vy);
            if (y1 >= Ground - 30 * Scale && y0 <= Ground + 4) ground.Lift(x0 - reach, x1 + reach, 0.85, random, kick);
            if (!ledgesOn) return;
            foreach (PieceLedge ledge in visibleLedges)
                if (y1 >= ledge.Y - 24 * Scale && y0 <= ledge.Y + 6) ledge.Lift(x0 - reach, x1 + reach, 0.85, random, kick);
        }

        private void AdvanceFlakes(double seconds, Settings settings)
        {
            bool autumn = Season == Season.Autumn;
            int density = autumn ? (settings.Density == 0 ? 16 : settings.Density == 1 ? 30 : 54)
                : (settings.Density == 0 ? 34 : settings.Density == 1 ? 66 : 120);
            int target = (int)Math.Min(MaxFlakes / 2, density * Math.Sqrt(Width * Height / 2073600.0));
            while (ambient < target) SpawnAmbient(autumn);
            double sizeScale = Height < 400 ? 0.6 : 1;
            for (int i = flakes.Count - 1; i >= 0; i--)
            {
                Flake flake = flakes[i];
                double oldY = flake.Y;
                double fall = (autumn ? 46 : 34) * (0.7 + flake.Size / (autumn ? 20 : 9)) * sizeScale;
                flake.Phase += seconds * flake.Sway;
                double targetVX = Wind * (autumn ? 0.85 : 1) + Math.Sin(flake.Phase) * (autumn ? 34 : 24);
                flake.VX += (targetVX - flake.VX) * Math.Min(1, seconds * 1.6);
                flake.VY += (fall * (1 + 0.45 * Math.Cos(flake.Phase * 2)) - flake.VY) * Math.Min(1, seconds * 2);
                flake.X += flake.VX * seconds;
                flake.Y += flake.VY * seconds;
                flake.Angle += (flake.Spin + flake.VX * 0.6) * seconds;
                flake.Flip += flake.FlipRate * seconds;
                bool outside = flake.X < -30 || flake.X > Width + 30;
                if (outside && !flake.Ambient)
                {
                    // Pieces blown off the screen are gone; only the steady fall wraps around.
                    flakes[i] = flakes[flakes.Count - 1];
                    flakes.RemoveAt(flakes.Count - 1);
                    continue;
                }
                if (flake.X < -30) flake.X += Width + 60;
                else if (flake.X > Width + 30) flake.X -= Width + 60;
                if (flake.VY > 0 && Settle(oldY, ref flake))
                {
                    if (flake.Ambient) ambient--;
                    flakes[i] = flakes[flakes.Count - 1];
                    flakes.RemoveAt(flakes.Count - 1);
                    continue;
                }
                flakes[i] = flake;
            }
            seeded = true;
        }

        private void SpawnAmbient(bool autumn)
        {
            ambient++;
            float size = (float)((autumn ? 15 + random.NextDouble() * 8 : 5.5 + random.NextDouble() * 3.5) * Scale *
                (Height < 400 ? 0.6 : 1));
            flakes.Add(new Flake
            {
                X = random.NextDouble() * Width, Y = seeded ? -20 - random.NextDouble() * 60 : random.NextDouble() * Ground,
                Size = size, Kind = (byte)random.Next(autumn ? 3 : 2), Tint = (byte)random.Next(autumn ? 5 : 3),
                Angle = random.NextDouble() * 360, Spin = (random.NextDouble() * 2 - 1) * (autumn ? 120 : 200),
                Phase = random.NextDouble() * 6.28, Sway = (autumn ? 1.1 : 1.6) + random.NextDouble() * 1.2,
                Flip = random.NextDouble() * 6.28, FlipRate = 1 + random.NextDouble() * 2.2, Ambient = true, VY = 30
            });
        }

        /// <summary>Lays a falling piece on the first pile it crosses; true once it rests.</summary>
        private bool Settle(double oldY, ref Flake flake)
        {
            // Pieces settle lying roughly along the surface.
            float rest = (float)(90 + random.NextDouble() * 60 - 30 + (random.Next(2) == 0 ? 180 : 0));
            if (ledgesOn)
                foreach (PieceLedge ledge in visibleLedges)
                {
                    if (!ledge.VisibleAt(flake.X)) continue;
                    double surface = ledge.SurfaceAt(flake.X);
                    if (oldY > surface || flake.Y < surface) continue;
                    if (ledge.Rest(flake.X, flake.Kind, flake.Tint, flake.Size, rest)) return true;
                }
            if (flake.Y < ground.SurfaceAt(flake.X)) return false;
            ground.Rest(Clamp(flake.X, 1, Width - 1), flake.Kind, flake.Tint, flake.Size, rest);
            return true;
        }

        public void Draw(Canvas drawing)
        {
            cast.DrawBack(drawing);
            drawing.DrawArt(0, 0, 1, false, g =>
            {
                if (ledgesOn) foreach (PieceLedge ledge in visibleLedges) ledge.Draw(g);
                ground.Draw(g);
            });
            cast.DrawFront(drawing);
            drawing.DrawArt(0, 0, 1, false, g =>
            {
                foreach (Flake flake in flakes)
                    SeasonArt.Piece(g, Season, flake.Kind, flake.Tint, flake.Size, (float)flake.Angle,
                        (float)Math.Cos(flake.Flip), (float)flake.X, (float)flake.Y, false);
            });
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        public void Dispose()
        {
            ground.Dispose();
            foreach (PieceLedge ledge in ledges.Values) ledge.Dispose();
        }
    }
}
