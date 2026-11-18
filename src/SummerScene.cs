using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using Rect = System.Windows.Rect;
using WPoint = System.Windows.Point;
using Forms = System.Windows.Forms;

namespace SnowDesk
{
    /// <summary>
    /// Summer shower scene: warm rain, water films on window tops, puddles on the taskbar edge and a
    /// rain/sun cycle that drives the characters in <see cref="SummerCast"/>.
    /// </summary>
    internal sealed class SummerScene : DesktopScene
    {
        private const int MaxDrops = 520, MaxSparks = 320, MaxRipples = 90, MaxFilms = 32, Cell = 8;
        private static readonly Pen[] DropPens =
        {
            Line(70, 186, 214, 240, 1f), Line(110, 204, 228, 250, 1.1f), Line(160, 226, 240, 255, 1.3f)
        };
        private static readonly Pen[] RipplePens =
        {
            Line(50, 226, 244, 255, 1f), Line(100, 226, 244, 255, 1f), Line(160, 226, 244, 255, 1.1f)
        };
        private static readonly Brush SparkBrush = SnowField.Brush(200, 214, 236, 255);
        private static readonly Brush FilmBrush = SnowField.Brush(118, 118, 186, 232);
        private static readonly Pen FilmGlint = Line(180, 230, 246, 255, 1f);
        private static readonly Pen WaterEdge = Line(170, 222, 242, 255, 1.1f);

        private readonly Random random = new Random(Guid.NewGuid().GetHashCode());
        private readonly Rect screen;
        private readonly Drop[] drops = new Drop[MaxDrops];
        private readonly List<Spark> sparks = new List<Spark>(MaxSparks);
        private readonly List<Ripple> ripples = new List<Ripple>(MaxRipples);
        private readonly Dictionary<IntPtr, Film> films = new Dictionary<IntPtr, Film>();
        private readonly List<Film> visibleFilms = new List<Film>(MaxFilms);
        private readonly HashSet<IntPtr> seen = new HashSet<IntPtr>();
        private readonly List<IntPtr> gone = new List<IntPtr>();
        private readonly List<Pool> pools = new List<Pool>(3);
        private readonly List<RectangleF> blockers = new List<RectangleF>();
        private bool filmsOn = true;
        private readonly SummerCast cast;
        private int dropCount;
        private bool seeded, raining = true;
        private double phaseLeft, intensity = 1, elapsed, sunTime, rainbow, sunGlow, gust, gustDirection = 1;

        private struct Drop
        {
            internal double X, Y, Speed, Length;
            internal int Layer;
        }

        private struct Spark
        {
            internal double X, Y, VX, VY, Age, Life;
            internal bool Drip;
        }

        private struct Ripple
        {
            internal double X, Y, Age, Size;
        }

        private sealed class Film
        {
            internal double Left, Top, Width, Level;
            internal bool[] Visible;
            internal double Height { get { return 1.2 + Level * 5; } }
        }

        /// <summary>A puddle that grows from its centre along the ground line.</summary>
        internal sealed class Pool
        {
            internal double X, Reach, Volume, Drain;
            internal bool Pond;
            internal double Half { get { return Reach * Math.Sqrt(Volume); } }
            internal double Depth(double scale) { return (1.5 + 11 * Volume) * scale; }

            /// <summary>Water surface height at x, or NaN outside the puddle.</summary>
            internal double SurfaceAt(double x, double ground, double scale)
            {
                double half = Half;
                if (half < 2 || Math.Abs(x - X) >= half) return double.NaN;
                double t = (x - X) / half;
                return ground - Depth(scale) * Math.Sqrt(1 - t * t);
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
            pools.Add(new Pool { X = main, Reach = 92 * Scale, Volume = pace > 1 ? 0.62 : 0.3 });
            double pond = main + (random.Next(2) == 0 ? -1 : 1) * Math.Max(Width * 0.24, 190 * Scale);
            if (pond < 70 * Scale || pond > Width - 70 * Scale) pond = main + (main > Width * 0.5 ? -1 : 1) * Width * 0.24;
            pools.Add(new Pool { X = pond, Reach = 62 * Scale, Volume = pace > 1 ? 0.6 : 0.34, Pond = true });
            if (Width > 1500 * Scale)
                pools.Add(new Pool { X = Width * (main > Width * 0.5 ? 0.12 : 0.88), Reach = 44 * Scale, Volume = 0.2 });
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
            visibleFilms.Clear();
            seen.Clear();
            blockers.Clear();
            foreach (Desktop.WindowBounds window in windows)
                blockers.Add(new RectangleF((float)(window.Bounds.X - screen.Left), (float)(window.Bounds.Y - screen.Top),
                    (float)window.Bounds.Width, (float)window.Bounds.Height));
            for (int i = 0; i < windows.Count && visibleFilms.Count < MaxFilms; i++)
            {
                Rect bounds = windows[i].Bounds;
                double left = Math.Max(screen.Left, bounds.Left), right = Math.Min(screen.Right, bounds.Right);
                if (bounds.Top <= screen.Top + 2 || bounds.Top - screen.Top >= Ground - 12 || right - left < 32) continue;
                IntPtr handle = windows[i].Handle;
                double x = left - screen.Left, y = bounds.Top - screen.Top, width = right - left;
                Film film;
                if (!films.TryGetValue(handle, out film))
                {
                    film = new Film();
                    films.Add(handle, film);
                }
                else if (film.Left != x || film.Top != y || film.Width != width)
                {
                    Spill(film);
                    film.Level *= 0.35;
                }
                film.Left = x;
                film.Top = y;
                film.Width = width;
                int cells = (int)Math.Ceiling(width / Cell);
                if (film.Visible == null || film.Visible.Length != cells) film.Visible = new bool[cells];
                bool any = false;
                for (int cell = 0; cell < cells; cell++)
                {
                    double probe = screen.Left + x + Math.Min(width - 1, cell * Cell + Cell * 0.5);
                    bool visible = true;
                    for (int front = 0; front < i && visible; front++)
                        visible = !windows[front].Bounds.Contains(probe, bounds.Top - 1);
                    film.Visible[cell] = visible;
                    any |= visible;
                }
                seen.Add(handle);
                if (any) visibleFilms.Add(film);
            }
            gone.Clear();
            foreach (var pair in films)
                if (!seen.Contains(pair.Key)) gone.Add(pair.Key);
            foreach (IntPtr handle in gone)
            {
                Spill(films[handle]);
                films.Remove(handle);
            }
        }

        public void Advance(double seconds, Settings settings, Desktop.Point pointer)
        {
            if (seconds <= 0) return;
            elapsed += seconds;
            filmsOn = settings.WindowLedges;
            AdvanceWeather(seconds, settings);
            double wind = settings.Wind ? 24 + 32 * Math.Sin(elapsed * 0.27) : 0;
            if (gust > 0)
            {
                gust = Math.Max(0, gust - seconds);
                wind += Math.Sin(gust * Math.PI / 1.6) * 170 * gustDirection;
            }
            AdvanceDrops(seconds, settings, wind);
            AdvanceWater(seconds, settings);
            AdvanceSparks(seconds);
            for (int i = ripples.Count - 1; i >= 0; i--)
            {
                Ripple ripple = ripples[i];
                ripple.Age += seconds;
                if (ripple.Age >= 0.7) ripples.RemoveAt(i);
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
            sunGlow += ((raining ? 0 : 1) - sunGlow) * (1 - Math.Exp(-seconds * 0.6));
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
                bool landed = drop.Layer > 0 && Land(oldX, oldY, drop.X, drop.Y);
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
            return new Drop
            {
                X = random.NextDouble() * Width, Y = y, Layer = layer,
                Speed = (520 + layer * 170 + random.NextDouble() * 90) * Math.Max(0.55, Scale * (Height < 400 ? 0.5 : 1)),
                Length = (7 + layer * 4 + random.NextDouble() * 3) * Math.Max(0.6, Scale * (Height < 400 ? 0.6 : 1))
            };
        }

        private bool Land(double oldX, double oldY, double x, double y)
        {
            foreach (Film film in visibleFilms)
            {
                if (!filmsOn) break;
                double surface = film.Top - film.Height;
                if (oldY > surface || y < surface || x < film.Left || x >= film.Left + film.Width) continue;
                int cell = Math.Min(film.Visible.Length - 1, (int)((x - film.Left) / Cell));
                if (!film.Visible[cell]) continue;
                if (random.NextDouble() < 0.55) Splash(x, surface, 2, 0.8);
                return true;
            }
            if (y < Ground - 12 * Scale) return false;
            foreach (Pool pool in pools)
            {
                double surface = pool.SurfaceAt(x, Ground, Scale);
                if (double.IsNaN(surface) || oldY > surface || y < surface) continue;
                if (ripples.Count < MaxRipples && random.NextDouble() < 0.5)
                    ripples.Add(new Ripple { X = x, Y = surface + 1, Size = (3 + random.NextDouble() * 4) * Scale });
                return true;
            }
            if (y < Ground) return false;
            if (random.NextDouble() < 0.45) Splash(x, Ground - 1, 2, 1);
            return true;
        }

        /// <summary>Throws a few droplets up from (x, y); used by rain, runners, kicks and the pump outlet.</summary>
        internal void Splash(double x, double y, int count, double strength)
        {
            for (int i = 0; i < count && sparks.Count < MaxSparks; i++)
                sparks.Add(new Spark
                {
                    X = x, Y = y, Life = 0.3 + random.NextDouble() * 0.25,
                    VX = (random.NextDouble() * 2 - 1) * 70 * strength * Scale,
                    VY = -(45 + random.NextDouble() * 70) * strength * Scale
                });
        }

        /// <summary>Releases a stream of drops along a direction, e.g. water leaving the pump hose.</summary>
        internal void Spray(double x, double y, double vx, double vy)
        {
            if (sparks.Count >= MaxSparks) return;
            sparks.Add(new Spark
            {
                X = x, Y = y, Life = 1.4, Drip = true,
                VX = vx * (0.85 + random.NextDouble() * 0.3) * Scale, VY = vy * (0.8 + random.NextDouble() * 0.4) * Scale
            });
        }

        private void Drip(double x, double y)
        {
            if (sparks.Count < MaxSparks)
                sparks.Add(new Spark { X = x, Y = y, VY = 18, Life = 6, Drip = true });
        }

        private void Spill(Film film)
        {
            int count = (int)(film.Level * 8);
            for (int i = 0; i < count; i++)
                Drip(i % 2 == 0 ? film.Left + 2 : film.Left + film.Width - 2, film.Top - random.NextDouble() * 3);
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
            foreach (Film film in films.Values)
            {
                film.Level = Math.Max(0, Math.Min(1, film.Level + (raining ? 0.05 * intensity : -0.07) * rate));
                if (!filmsOn) film.Level = 0;
                if (film.Level < 0.88 || !raining || random.NextDouble() > seconds * 3) continue;
                bool right = random.Next(2) == 0;
                int cell = right ? film.Visible.Length - 1 : 0;
                if (film.Visible[cell]) Drip(right ? film.Left + film.Width - 1.5 : film.Left + 1.5, film.Top);
            }
        }

        private void AdvanceSparks(double seconds)
        {
            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                Spark spark = sparks[i];
                spark.Age += seconds;
                spark.VY += 420 * seconds;
                spark.X += spark.VX * seconds;
                spark.Y += spark.VY * seconds;
                bool done = spark.Age >= spark.Life || spark.Y > Height + 4;
                if (!done && spark.Drip && spark.Y >= Ground - 1)
                {
                    done = true;
                    bool inPool = false;
                    foreach (Pool pool in pools)
                        inPool |= !double.IsNaN(pool.SurfaceAt(spark.X, Ground, Scale));
                    if (inPool && ripples.Count < MaxRipples)
                        ripples.Add(new Ripple { X = spark.X, Y = Ground - 2, Size = 5 * Scale });
                    else if (!inPool) Splash(spark.X, Ground - 1, 1, 0.6);
                }
                if (done)
                {
                    sparks[i] = sparks[sparks.Count - 1];
                    sparks.RemoveAt(sparks.Count - 1);
                }
                else sparks[i] = spark;
            }
        }

        public void Draw(Canvas drawing)
        {
            if (sunGlow > 0.02 || rainbow > 0.02) drawing.DrawArt(0, 0, 1, false, DrawSky);
            if (filmsOn && visibleFilms.Count > 0) drawing.DrawArt(0, 0, 1, false, DrawFilms);
            drawing.DrawArt(0, 0, 1, false, DrawPools);
            cast.DrawWater(drawing);
            cast.Draw(drawing);
            foreach (Ripple ripple in ripples)
            {
                double t = ripple.Age / 0.7;
                Pen pen = RipplePens[Math.Min(2, (int)((1 - t) * 3))];
                double radius = ripple.Size * (0.3 + t);
                drawing.StrokeEllipse(pen, new WPoint(ripple.X, ripple.Y), radius, radius * 0.28);
            }
            for (int i = 0; i < dropCount; i++)
            {
                Drop drop = drops[i];
                double slant = drop.Length * 0.08;
                drawing.DrawLine(DropPens[drop.Layer], new WPoint(drop.X - slant, drop.Y - drop.Length), new WPoint(drop.X, drop.Y));
            }
            foreach (Spark spark in sparks)
            {
                double radius = spark.Drip ? 1.3 : 0.95;
                drawing.DrawEllipse(SparkBrush, null, new WPoint(spark.X, spark.Y), radius, spark.Drip ? radius * 1.4 : radius);
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

        private void DrawFilms(Graphics g)
        {
            foreach (Film film in visibleFilms)
            {
                if (film.Level < 0.03) continue;
                float height = (float)film.Height;
                int start = -1;
                for (int cell = 0; cell <= film.Visible.Length; cell++)
                {
                    bool visible = cell < film.Visible.Length && film.Visible[cell];
                    if (visible && start < 0) start = cell;
                    if (visible || start < 0) continue;
                    float left = (float)(film.Left + start * Cell);
                    float right = (float)Math.Min(film.Left + film.Width, film.Left + cell * Cell);
                    using (GraphicsPath band = DeskStyle.Rounded(
                        Rectangle.Round(new RectangleF(left, (float)film.Top - height, right - left, height + 1)),
                        Math.Max(1, (int)(height * 0.5))))
                        g.FillPath(FilmBrush, band);
                    g.DrawLine(FilmGlint, left + 3, (float)film.Top - height + 0.6f, right - 3, (float)film.Top - height + 0.6f);
                    start = -1;
                }
            }
        }

        private void DrawPools(Graphics g)
        {
            foreach (Pool pool in pools)
            {
                double half = pool.Half;
                if (half < 3) continue;
                float depth = (float)pool.Depth(Scale);
                var bounds = new RectangleF((float)(pool.X - half), (float)Ground - depth, (float)half * 2, depth * 2);
                using (var water = new GraphicsPath())
                {
                    water.AddArc(bounds, 180, 180);
                    water.CloseFigure();
                    using (var fill = new LinearGradientBrush(new RectangleF(bounds.X, bounds.Y - 1, bounds.Width, depth + 2),
                        Color.FromArgb(150, 150, 212, 244), Color.FromArgb(190, 58, 128, 190), LinearGradientMode.Vertical))
                        g.FillPath(fill, water);
                    g.DrawArc(WaterEdge, bounds, 200, 140);
                }
                float glint = (float)(half * 0.35);
                g.DrawLine(FilmGlint, (float)pool.X - glint, (float)Ground - depth * 0.55f, (float)pool.X - glint * 0.2f,
                    (float)Ground - depth * 0.55f);
            }
        }

        private static Pen Line(int alpha, int red, int green, int blue, float width)
        {
            return new Pen(Color.FromArgb(alpha, red, green, blue), width) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        }

        public void Dispose() { }
    }
}
