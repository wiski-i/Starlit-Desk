using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;

namespace SnowDesk
{
    /// <summary>
    /// The space theme's set pieces: the Milky Way band, a supernova that flares and leaves a growing remnant,
    /// a ringed gas giant drifting across, two asteroids that collide into a cooling debris cloud, a comet with
    /// dust and ion tails and an astronaut on a spacewalk. Heavy imagery is built on a background thread at
    /// start-up; an event waits until its sprites are ready. Big events take turns; the astronaut comes and
    /// goes on its own.
    /// </summary>
    internal sealed class SpaceEvents : IDisposable
    {
        internal const int Supernova = 1, Giant = 2, Collision = 3, Comet = 4, Astronaut = 5;

        private struct Debris
        {
            internal float X, Y, VX, VY, Size, Age, Life;
            internal bool Rock;
        }

        private readonly int width, height;
        private readonly float scale;
        private readonly bool preview;
        private readonly Random random;
        private readonly ArtCache astronautCache = new ArtCache(48);
        private readonly List<Debris> debris = new List<Debris>();
        private readonly List<Debris> puffs = new List<Debris>();
        private volatile bool ready;
        private List<SpaceSprites.Tile> milkyWay, deepMilkyWay;
        private Bitmap glow, remnant, giant, cometRight, cometLeft;
        private Bitmap[] rockA, rockB;
        private Point cometHeadRight, cometHeadLeft;
        private double clock, nextBig, nextAstronaut;
        private int active;
        private double age, duration;
        private float startX, startY, endX, endY, meetX, meetY;
        private bool impacted, leftward;
        private Bitmap shellImage;
        private int shellSize, shellFade;
        private double astronautAge = -1, astronautDuration;
        private float astroX0, astroY0, astroX1, astroY1, astroSpin;

        internal SpaceEvents(int width, int height, float scale, bool preview, int seed)
        {
            this.width = width;
            this.height = height;
            this.scale = scale;
            this.preview = preview;
            random = new Random(seed);
            nextBig = preview ? 2 : 22 + random.NextDouble() * 12;
            nextAstronaut = preview ? 6 : 10 + random.NextDouble() * 8;
            var builder = new Thread(Build) { IsBackground = true, Name = "Space sprites", Priority = ThreadPriority.BelowNormal };
            builder.Start(seed);
        }

        private void Build(object state)
        {
            int seed = (int)state;
            try
            {
                glow = SpaceSprites.Glow(256, Color.FromArgb(150, 200, 255));
                remnant = SpaceSprites.Remnant(preview ? 192 : 448, seed);
                giant = SpaceSprites.GasGiant((int)(78 * scale), seed + 1);
                int rock = Math.Max(16, (int)(46 * scale));
                rockA = new Bitmap[16];
                rockB = new Bitmap[16];
                for (int i = 0; i < 16; i++)
                {
                    rockA[i] = SpaceSprites.Asteroid(rock, seed + 21, i * Math.PI / 8);
                    rockB[i] = SpaceSprites.Asteroid((int)(rock * 0.78), seed + 37, -i * Math.PI / 8);
                }
                int tail = (int)(250 * scale);
                cometRight = SpaceSprites.Comet(tail, Math.PI * 0.82, seed + 5, out cometHeadRight);
                cometLeft = SpaceSprites.Comet(tail, Math.PI * 0.18, seed + 6, out cometHeadLeft);
                milkyWay = SpaceSprites.MilkyWay(width, height, false, seed + 9);
                deepMilkyWay = SpaceSprites.MilkyWay(width, height, true, seed + 10);
                ready = true;
            }
            catch (OutOfMemoryException) { }
            catch (ArgumentException) { }
        }

        /// <summary>Starts a set piece by hand; ignored while its sprites are still being built.</summary>
        internal void Trigger(int which)
        {
            if (!ready) return;
            if (which == Astronaut) StartAstronaut();
            else StartBig(which);
        }

        internal void Advance(double seconds, SpaceEffect effects, bool motion)
        {
            clock += seconds;
            if (!ready || !motion) return;
            if (active == 0 && clock >= nextBig)
            {
                var choices = new List<int>();
                if ((effects & SpaceEffect.Supernova) != 0) choices.Add(Supernova);
                if ((effects & SpaceEffect.Giant) != 0) choices.Add(Giant);
                if ((effects & SpaceEffect.Collision) != 0) choices.Add(Collision);
                if ((effects & SpaceEffect.Comet) != 0) choices.Add(Comet);
                if (choices.Count > 0) StartBig(choices[random.Next(choices.Count)]);
                else nextBig = clock + 30;
            }
            if (astronautAge < 0 && (effects & SpaceEffect.Astronaut) != 0 && clock >= nextAstronaut) StartAstronaut();
            AdvanceBig(seconds);
            AdvanceAstronaut(seconds);
            AdvanceParticles(debris, seconds, 0.35f);
            AdvanceParticles(puffs, seconds, 1.6f);
        }

        private void StartBig(int which)
        {
            active = which;
            age = 0;
            impacted = false;
            leftward = random.Next(2) == 0;
            if (which == Supernova)
            {
                duration = preview ? 24 : 75;
                startX = width * (0.15f + (float)random.NextDouble() * 0.7f);
                startY = height * (0.1f + (float)random.NextDouble() * 0.35f);
            }
            else if (which == Giant)
            {
                duration = preview ? 30 : 170;
                float span = giant.Width * 0.6f;
                startX = leftward ? width + span : -span;
                endX = leftward ? -span : width + span;
                startY = height * (0.16f + (float)random.NextDouble() * 0.2f);
                endY = startY + height * 0.06f;
            }
            else if (which == Collision)
            {
                duration = preview ? 16 : 30;
                meetX = width * (0.25f + (float)random.NextDouble() * 0.5f);
                meetY = height * (0.15f + (float)random.NextDouble() * 0.3f);
                debris.Clear();
            }
            else
            {
                duration = preview ? 22 : 95;
                startX = leftward ? width + 60 * scale : -60 * scale;
                endX = leftward ? -width * 0.1f : width * 1.1f;
                startY = height * (0.08f + (float)random.NextDouble() * 0.18f);
                endY = startY + height * (0.12f + (float)random.NextDouble() * 0.1f);
            }
        }

        private void AdvanceBig(double seconds)
        {
            if (active == 0) return;
            age += seconds;
            if (active == Collision && !impacted && age >= CollisionTime) Impact();
            if (age < duration) return;
            active = 0;
            nextBig = clock + (preview ? 4 : 35 + random.NextDouble() * 40);
        }

        private double CollisionTime { get { return preview ? 4 : 8; } }

        /// <summary>The two rocks meet: a flash, tumbling fragments and hot dust that cools as it spreads.</summary>
        private void Impact()
        {
            impacted = true;
            int count = preview ? 40 : 90;
            for (int i = 0; i < count; i++)
            {
                double angle = random.NextDouble() * Math.PI * 2, speed = (12 + random.NextDouble() * random.NextDouble() * 120) * scale;
                bool rock = i % 6 == 0;
                debris.Add(new Debris
                {
                    X = meetX, Y = meetY, VX = (float)(Math.Cos(angle) * speed), VY = (float)(Math.Sin(angle) * speed),
                    Size = (float)((rock ? 2.4 + random.NextDouble() * 2.6 : 0.8 + random.NextDouble() * 1.6) * scale),
                    Life = (float)(rock ? 22 + random.NextDouble() * 6 : 6 + random.NextDouble() * 8), Rock = rock
                });
            }
        }

        private void StartAstronaut()
        {
            astronautAge = 0;
            astronautDuration = preview ? 20 : 85;
            bool fromLeft = random.Next(2) == 0;
            astroX0 = fromLeft ? -60 * scale : width + 60 * scale;
            astroX1 = fromLeft ? width + 60 * scale : -60 * scale;
            astroY0 = height * (0.2f + (float)random.NextDouble() * 0.4f);
            astroY1 = astroY0 + height * ((float)random.NextDouble() - 0.5f) * 0.2f;
            astroSpin = (float)((random.NextDouble() < 0.5 ? -1 : 1) * (5 + random.NextDouble() * 6));
        }

        private void AdvanceAstronaut(double seconds)
        {
            if (astronautAge < 0) return;
            astronautAge += seconds;
            if (astronautAge >= astronautDuration)
            {
                astronautAge = -1;
                nextAstronaut = clock + (preview ? 8 : 70 + random.NextDouble() * 60);
                return;
            }
            // A short burst from the backpack thrusters now and then.
            if (random.NextDouble() < seconds * 0.4)
            {
                PointF at = AstronautPosition();
                double angle = (astronautAge * astroSpin + 180) * Math.PI / 180;
                for (int i = 0; i < 7; i++)
                {
                    double spread = angle + (random.NextDouble() - 0.5) * 0.7, speed = (18 + random.NextDouble() * 26) * scale;
                    puffs.Add(new Debris
                    {
                        X = at.X, Y = at.Y, VX = (float)(Math.Cos(spread) * speed), VY = (float)(Math.Sin(spread) * speed),
                        Size = (float)((1 + random.NextDouble() * 1.4) * scale), Life = (float)(0.9 + random.NextDouble() * 0.7)
                    });
                }
            }
        }

        private PointF AstronautPosition()
        {
            double t = astronautAge / astronautDuration;
            return new PointF((float)(astroX0 + (astroX1 - astroX0) * t),
                (float)(astroY0 + (astroY1 - astroY0) * t + Math.Sin(astronautAge * 0.5) * 10 * scale));
        }

        private static void AdvanceParticles(List<Debris> list, double seconds, float drag)
        {
            float keep = (float)Math.Exp(-drag * seconds);
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Debris p = list[i];
                p.Age += (float)seconds;
                if (p.Age >= p.Life)
                {
                    list[i] = list[list.Count - 1];
                    list.RemoveAt(list.Count - 1);
                    continue;
                }
                p.X += p.VX * (float)seconds;
                p.Y += p.VY * (float)seconds;
                p.VX *= keep;
                p.VY *= keep;
                list[i] = p;
            }
        }

        /// <summary>The galactic band, drawn under the stars.</summary>
        internal void DrawBackground(Graphics g, SpaceEffect effects, bool deep)
        {
            if (!ready || (effects & SpaceEffect.MilkyWay) == 0) return;
            foreach (SpaceSprites.Tile tile in deep ? deepMilkyWay : milkyWay) Blit(g, tile.Image, tile.X, tile.Y);
        }

        internal void Draw(Graphics g)
        {
            if (!ready) return;
            if (active == Giant) DrawGiant(g);
            else if (active == Comet) DrawComet(g);
            else if (active == Supernova) DrawSupernova(g);
            else if (active == Collision) DrawCollision(g);
            DrawDebris(g);
            DrawAstronaut(g);
        }

        private void DrawGiant(Graphics g)
        {
            double t = age / duration;
            float x = (float)(startX + (endX - startX) * t), y = (float)(startY + (endY - startY) * t);
            Blit(g, giant, x - giant.Width * 0.5f, y - giant.Height * 0.5f);
        }

        private void DrawComet(Graphics g)
        {
            double t = age / duration;
            float fade = (float)Math.Min(1, Math.Min(age / 3, (duration - age) / 3));
            float x = (float)(startX + (endX - startX) * t), y = (float)(startY + (endY - startY) * t);
            Bitmap sprite = leftward ? cometLeft : cometRight;
            Point head = leftward ? cometHeadLeft : cometHeadRight;
            Fade(g, sprite, new RectangleF(x - head.X, y - head.Y, sprite.Width, sprite.Height), fade);
        }

        private void DrawSupernova(Graphics g)
        {
            float x = startX, y = startY;
            if (age < 1.6)
            {
                // The progenitor brightens.
                float r = (float)(4 + age / 1.6 * 34) * scale;
                Fade(g, glow, new RectangleF(x - r, y - r, r * 2, r * 2), (float)(0.4 + age / 1.6 * 0.6));
                return;
            }
            double after = age - 1.6;
            double grow = 1 - Math.Exp(-after / (preview ? 6 : 20));
            float shell = (float)((20 + 300 * grow) * scale * (preview ? 0.5 : 1));
            float shellAlpha = (float)(Math.Min(1, after / 4) * Math.Max(0, 1 - after / (duration - 1.6)));
            if (shellAlpha > 0.01) DrawRemnant(g, x, y, shell, shellAlpha);
            if (after < 6)
            {
                // Peak flash with diffraction spikes, then a fading point.
                double flash = after < 0.6 ? after / 0.6 : Math.Exp(-(after - 0.6) * 0.8);
                float r = (float)(40 + 190 * flash) * scale * (preview ? 0.5f : 1);
                Fade(g, glow, new RectangleF(x - r, y - r, r * 2, r * 2), (float)Math.Min(1, flash * 1.2));
                using (var spike = new Pen(Color.FromArgb((int)(170 * flash), 220, 236, 255), 1.4f * scale))
                {
                    float reach = r * 1.7f;
                    g.DrawLine(spike, x - reach, y, x + reach, y);
                    g.DrawLine(spike, x, y - reach, x, y + reach);
                }
            }
        }

        /// <summary>
        /// The remnant lives for about a minute and changes slowly, so it is rescaled and faded only when its size
        /// or opacity crosses a step; the frames in between blit the prepared bitmap.
        /// </summary>
        private void DrawRemnant(Graphics g, float x, float y, float radius, float alpha)
        {
            int size = Math.Max(8, (int)(Math.Round(radius / 6) * 12)), fade = (int)Math.Round(alpha * 20);
            if (shellImage == null || shellSize != size || shellFade != fade)
            {
                if (shellImage != null) shellImage.Dispose();
                shellImage = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
                using (Graphics paint = Graphics.FromImage(shellImage))
                using (var attributes = new ImageAttributes())
                {
                    paint.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                    paint.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    attributes.SetColorMatrix(new ColorMatrix { Matrix33 = fade / 20f });
                    paint.DrawImage(remnant, new Rectangle(0, 0, size, size), 0, 0, remnant.Width, remnant.Height, GraphicsUnit.Pixel, attributes);
                }
                shellSize = size;
                shellFade = fade;
            }
            Blit(g, shellImage, x - size * 0.5f, y - size * 0.5f);
        }

        private void DrawCollision(Graphics g)
        {
            if (!impacted)
            {
                // Two rocks close in on the meeting point, tumbling as they come.
                double t = age / CollisionTime, ease = t * t * (3 - 2 * t);
                float span = width * 0.32f;
                int frame = (int)(age * 3) & 15;
                DrawRock(g, rockA[frame], meetX - span * (float)(1 - ease), meetY - height * 0.08f * (float)(1 - ease));
                DrawRock(g, rockB[frame], meetX + span * (float)(1 - ease), meetY + height * 0.05f * (float)(1 - ease));
                return;
            }
            double since = age - CollisionTime;
            if (since < 2.4)
            {
                double flash = since < 0.15 ? since / 0.15 : Math.Exp(-(since - 0.15) * 1.6);
                float r = (float)(30 + 150 * flash) * scale;
                Fade(g, glow, new RectangleF(meetX - r, meetY - r, r * 2, r * 2), (float)flash);
            }
        }

        private void DrawRock(Graphics g, Bitmap sprite, float x, float y)
        {
            Blit(g, sprite, x - sprite.Width * 0.5f, y - sprite.Height * 0.5f);
        }

        private void DrawDebris(Graphics g)
        {
            foreach (Debris p in debris)
            {
                float life = p.Age / p.Life;
                int alpha = (int)(255 * Math.Max(0, 1 - life * life));
                Color color;
                if (p.Rock) color = Color.FromArgb(alpha, 118, 108, 98);
                else
                {
                    // Hot dust cools from yellow-white through orange and red to grey.
                    float heat = Math.Max(0, 1 - p.Age / 3.5f);
                    color = Color.FromArgb(alpha, (int)(150 + 105 * Math.Min(1, heat * 1.6)), (int)(110 + 120 * heat * heat),
                        (int)(100 + 60 * heat * heat * heat));
                }
                using (var brush = new SolidBrush(color)) g.FillEllipse(brush, p.X - p.Size, p.Y - p.Size, p.Size * 2, p.Size * 2);
            }
            foreach (Debris p in puffs)
            {
                int alpha = (int)(150 * (1 - p.Age / p.Life));
                using (var brush = new SolidBrush(Color.FromArgb(alpha, 230, 236, 244)))
                    g.FillEllipse(brush, p.X - p.Size, p.Y - p.Size, p.Size * 2, p.Size * 2);
            }
        }

        private void DrawAstronaut(Graphics g)
        {
            if (astronautAge < 0) return;
            PointF at = AstronautPosition();
            int step = ((int)Math.Round(astronautAge * astroSpin / 10) % 36 + 36) % 36;
            float size = scale * (preview ? 0.55f : 1.05f);
            astronautCache.Draw(g, step, at.X, at.Y, SpaceFigures.Bounds, size, false, art => SpaceFigures.Astronaut(art, step * 10));
        }

        private static void Blit(Graphics g, Bitmap image, float x, float y)
        {
            // Unscaled copies skip filtering; the frame's half-pixel offset would otherwise resample them.
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.DrawImage(image, (int)Math.Round(x), (int)Math.Round(y), image.Width, image.Height);
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
        }

        private static void Fade(Graphics g, Bitmap image, RectangleF target, float alpha)
        {
            if (alpha <= 0.01f) return;
            if (alpha >= 0.99f && Math.Abs(target.Width - image.Width) < 0.5f)
            {
                Blit(g, image, target.X, target.Y);
                return;
            }
            using (var attributes = new ImageAttributes())
            {
                attributes.SetColorMatrix(new ColorMatrix { Matrix33 = Math.Min(1, alpha) });
                g.DrawImage(image, Rectangle.Round(target), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            }
        }

        public void Dispose()
        {
            astronautCache.Dispose();
        }
    }
}
