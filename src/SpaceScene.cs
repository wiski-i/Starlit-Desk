using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using Forms = System.Windows.Forms;
using Rect = System.Windows.Rect;

namespace SnowDesk
{
    internal sealed class SpaceScene : DesktopScene
    {
        private static readonly Brush[] StarColors =
        {
            SnowField.Brush(112, 161, 188, 227), SnowField.Brush(158, 188, 211, 239),
            SnowField.Brush(201, 222, 236, 255), SnowField.Brush(189, 247, 223, 188)
        };
        private static readonly Brush DustColor = SnowField.Brush(135, 168, 207, 238);
        private static readonly Pen Diffract = new Pen(Color.FromArgb(76, 151, 200, 238), 0.7f);

        private readonly Rectangle screen;
        private readonly bool preview, motion;
        private readonly float scale;
        private readonly Random random;
        private readonly Star[] stars = new Star[236];
        private readonly Dust[] dust = new Dust[36];
        private readonly Region masking = new Region(Rectangle.Empty);
        private readonly Action<Graphics> painter;
        private bool hasOccluders, hasPointer;
        private Desktop.Point lastPointer;
        private double clock, elapsed, nextSatellite, nextMeteor, nextAurora;
        private double satelliteAge, satelliteDuration, meteorAge, auroraAge;
        private double meteorX, meteorY, meteorVX, meteorVY;
        private bool satelliteMirrored, manualSatellite, manualMeteor, manualAurora;
        private SpaceEffect effects = SpaceEffect.All;
        private int starCount, dustCount, look = -1;
        private double lookFade = 1;
        private SpaceArt.SkySprite planetSprite, cloudSprite;
        private int cachedLook = -1;

        private struct Star
        {
            internal float X, Y, Radius;
            internal byte Depth, Color;
            internal bool Bright;
        }

        private struct Dust
        {
            internal float X, Y, Radius, DX, DY, VX, VY;
        }

        internal SpaceScene(Forms.Screen display) : this(display.Bounds, false) { }

        internal SpaceScene(Rectangle viewport, bool preview)
        {
            screen = viewport;
            this.preview = preview;
            motion = Desktop.DecorativeMotionEnabled();
            scale = preview ? 0.55f : (float)Math.Max(0.82, Math.Min(1.55, screen.Height / 1080.0));
            random = new Random(unchecked(screen.X * 349 + screen.Y * 173 + screen.Width * 71 + screen.Height));
            starCount = preview ? 75 : 105;
            nextSatellite = preview ? 0.9 : 4.5 + random.NextDouble() * 3.2;
            nextMeteor = preview ? 16 : 36 + random.NextDouble() * 27;
            nextAurora = preview ? 24 : 97 + random.NextDouble() * 57;
            painter = DrawSky;
            Seed();
        }

        private void Seed()
        {
            for (int i = 0; i < stars.Length; i++)
            {
                byte level = (byte)(i % 5 == 0 ? 2 : i % 3 == 0 ? 1 : 0);
                stars[i] = new Star
                {
                    X = (float)(random.NextDouble() * screen.Width), Y = (float)(random.NextDouble() * screen.Height),
                    Radius = (float)(0.53 + level * 0.23 + random.NextDouble() * (0.27 + 0.16 * level)),
                    Depth = level, Bright = i % 29 == 0, Color = (byte)(i % 17 == 0 ? 3 : level)
                };
            }
            for (int i = 0; i < dust.Length; i++)
                dust[i] = new Dust
                {
                    X = (float)(random.NextDouble() * screen.Width), Y = (float)(random.NextDouble() * screen.Height),
                    Radius = (float)(0.73 + random.NextDouble() * 0.64),
                    DX = (float)(-7 + random.NextDouble() * 19), DY = (float)(-3 + random.NextDouble() * 6)
                };
        }

        public bool UsesWindowGeometry(Settings settings) { return settings.SpaceOcclusion; }

        public void UpdateLedges(List<Desktop.WindowBounds> windows)
        {
            masking.MakeEmpty();
            hasOccluders = false;
            var viewport = new Rect(screen.X, screen.Y, screen.Width, screen.Height);
            foreach (Desktop.WindowBounds window in windows)
            {
                Rect rect = window.Bounds;
                if (rect.IsEmpty || !rect.IntersectsWith(viewport)) continue;
                var local = Rectangle.FromLTRB((int)Math.Floor(rect.Left - screen.Left), (int)Math.Floor(rect.Top - screen.Top),
                    (int)Math.Ceiling(rect.Right - screen.Left), (int)Math.Ceiling(rect.Bottom - screen.Top));
                if (local.Width <= 0 || local.Height <= 0) continue;
                int radius = Math.Min((int)Math.Round(window.CornerRadius), Math.Min(local.Width, local.Height) / 2);
                if (radius < 2) masking.Union(local);
                else
                    using (GraphicsPath path = DeskStyle.Rounded(local, radius)) masking.Union(path);
                hasOccluders = true;
            }
            if (hasOccluders) masking.Intersect(new Rectangle(0, 0, screen.Width, screen.Height));
        }

        public void ResetPointer()
        {
            hasPointer = false;
            ReleaseSky();
        }

        internal void SendSatellite() { StartSatellite(true); }
        internal void SendMeteor() { StartMeteor(true); }
        internal void ShowAurora() { StartAurora(true); }

        public void Advance(double seconds, Settings settings, Desktop.Point pointer)
        {
            if (seconds <= 0) return;
            clock += seconds;
            if (look < 0) look = settings.SpaceLook;
            bool switchLook = look != settings.SpaceLook;
            if (!motion || preview)
            {
                look = settings.SpaceLook;
                lookFade = 1;
            }
            else
            {
                lookFade = Math.Max(0, Math.Min(1, lookFade + seconds * (switchLook ? -3.1 : 1.7)));
                if (switchLook && lookFade <= 0) look = settings.SpaceLook;
            }
            effects = settings.Space;
            if (settings.QuietScene) effects &= SpaceEffect.Planet | SpaceEffect.Dust;
            double area = preview ? 0.76 : Math.Sqrt((double)screen.Width * screen.Height / 2073600.0);
            starCount = Math.Max(24, Math.Min(stars.Length, (int)((settings.Density == 0 ? 57 : settings.Density == 1 ? 113 : 175) * area)));
            dustCount = (effects & SpaceEffect.Dust) == 0 ? 0 : Math.Min(dust.Length, (int)((settings.Density + 1) * 9 * area));
            double step = motion ? seconds * (settings.QuietScene ? 0.22 : 1) : 0;
            elapsed += step;
            if (step > 0)
            {
                if (settings.CursorSweep && hasPointer) SweepDust(pointer, seconds);
                MoveDust(step);
                if ((effects & SpaceEffect.Satellite) != 0 && clock >= nextSatellite) StartSatellite(false);
                if ((effects & SpaceEffect.Meteors) != 0 && clock >= nextMeteor) StartMeteor(false);
                if ((effects & (SpaceEffect.Aurora | SpaceEffect.Planet)) == (SpaceEffect.Aurora | SpaceEffect.Planet) &&
                    clock >= nextAurora) StartAurora(false);
            }
            lastPointer = pointer;
            hasPointer = true;
            if (satelliteAge > 0) satelliteAge = Math.Max(0, satelliteAge - seconds);
            if (meteorAge > 0) meteorAge = Math.Max(0, meteorAge - seconds);
            if (auroraAge > 0) auroraAge = Math.Max(0, auroraAge - seconds);
        }

        private void StartSatellite(bool manual)
        {
            nextSatellite = clock + (preview ? 22 + random.NextDouble() * 10 : 65 + random.NextDouble() * 35);
            if (satelliteAge > 0) { manualSatellite |= manual; return; }
            satelliteDuration = !motion ? 2.3 : preview ? 8.4 : (screen.Width + 102 * scale) / (76 * scale);
            satelliteMirrored = false;
            if (!preview) satelliteMirrored = random.Next(2) == 0;
            satelliteAge = satelliteDuration;
            manualSatellite = manual;
        }

        private void StartMeteor(bool manual)
        {
            nextMeteor = clock + (preview ? 24 + random.NextDouble() * 16 : 49 + random.NextDouble() * 54);
            meteorX = screen.Width * (0.22 + random.NextDouble() * 0.44);
            meteorY = screen.Height * (0.09 + random.NextDouble() * 0.28);
            meteorVX = (preview ? 154 : 410) * (random.Next(2) == 0 ? -1 : 1);
            meteorVY = preview ? 54 : 158;
            meteorAge = 0.92;
            manualMeteor = manual;
        }

        private void StartAurora(bool manual)
        {
            nextAurora = clock + (preview ? 33 : 128 + random.NextDouble() * 52);
            auroraAge = 7;
            manualAurora = manual;
        }

        private void SweepDust(Desktop.Point pointer, double seconds)
        {
            var swipe = CursorImpulse.Measure(lastPointer, pointer, seconds);
            if (swipe.Speed < 255 || swipe.Distance < 2) return;
            double x = lastPointer.X - screen.Left, y = lastPointer.Y - screen.Top;
            double reach = 26 + 36 * swipe.Strength, reachSquared = reach * reach;
            double dvx = swipe.DX / swipe.Distance, dvy = swipe.DY / swipe.Distance;
            for (int i = 0; i < dustCount; i++)
            {
                Dust particle = dust[i];
                double toX = particle.X - x, toY = particle.Y - y;
                double along = Math.Max(0, Math.Min(swipe.Distance, toX * dvx + toY * dvy));
                double offX = toX - along * dvx, offY = toY - along * dvy;
                double distanceSquared = offX * offX + offY * offY;
                if (distanceSquared >= reachSquared) continue;
                double force = (1 - distanceSquared / reachSquared) * (24 + 108 * swipe.Strength);
                particle.VX = (float)Math.Max(-180, Math.Min(180, particle.VX + force * (dvx + offX / reach)));
                particle.VY = (float)Math.Max(-180, Math.Min(180, particle.VY + force * (dvy + offY / reach)));
                dust[i] = particle;
            }
        }

        private void MoveDust(double seconds)
        {
            float friction = (float)Math.Exp(-2.8 * seconds);
            for (int i = 0; i < dustCount; i++)
            {
                Dust particle = dust[i];
                particle.X = (float)Wrap(particle.X + (particle.DX + particle.VX) * seconds, screen.Width);
                particle.Y = (float)Wrap(particle.Y + (particle.DY + particle.VY) * seconds, screen.Height);
                particle.VX *= friction;
                particle.VY *= friction;
                dust[i] = particle;
            }
        }

        private static double Wrap(double value, double span)
        {
            if (value < 0) return value % span + span;
            if (value >= span) return value % span;
            return value;
        }

        public void Draw(Canvas drawing)
        {
            drawing.DrawArt(0, 0, 1, false, painter);
        }

        private void DrawSky(Graphics g)
        {
            if (hasOccluders) g.ExcludeClip(masking);
            float width = screen.Width, height = screen.Height;
            bool deep = look == 1;
            float x = width * (deep ? 0.075f : 0.905f), y = height * (preview ? 1.025f : 1.10f);
            int drift = (int)Math.Round(Math.Sin(elapsed * 0.031) * width * 0.003);
            float radius = Math.Min(width * 0.36f, height * (preview ? 0.64f : 0.50f));
            bool hasPlanet = (effects & SpaceEffect.Planet) != 0 && lookFade > 0.015;
            PrepareSky(deep, hasPlanet, x, y, radius);
            if (deep && lookFade > 0.015) cloudSprite.Draw(g, (float)lookFade);
            DrawStars(g, width, height, x + drift, y, hasPlanet ? radius : 0);
            if (hasPlanet)
            {
                planetSprite.Draw(g, (float)lookFade, drift);
                if (auroraAge > 0 && (manualAurora || (effects & SpaceEffect.Aurora) != 0))
                {
                    double light = Math.Min(1, Math.Min((7 - auroraAge) / 1.5, auroraAge / 2.4)) * lookFade;
                    if (light > 0.035) SpaceArt.Aurora(g, x + drift, y, radius, deep, Math.Sin(elapsed * 0.88), light);
                }
            }
            DrawDust(g);
            if (satelliteAge > 0 && (manualSatellite || (effects & SpaceEffect.Satellite) != 0))
                DrawSatellite(g, width, height);
            if (meteorAge > 0 && (manualMeteor || (effects & SpaceEffect.Meteors) != 0))
            {
                double travel = motion ? 0.92 - meteorAge : 0;
                double brightness = Math.Min(1, Math.Min(meteorAge / 0.27, (0.92 - meteorAge) / 0.17));
                if (brightness > 0.08)
                    SpaceArt.Meteor(g, (float)(meteorX + travel * meteorVX), (float)(meteorY + travel * meteorVY),
                        (float)(meteorVX * 0.205), (float)(meteorVY * 0.205), brightness);
            }
        }

        private void PrepareSky(bool deep, bool showPlanet, float x, float y, float radius)
        {
            int palette = deep ? 1 : 0;
            if (cachedLook != palette)
            {
                ReleaseSky();
                cachedLook = palette;
            }
            var viewport = new Rectangle(0, 0, screen.Width, screen.Height);
            if (deep && cloudSprite == null) cloudSprite = SpaceArt.PrepareCloud(viewport);
            if (showPlanet && planetSprite == null)
                planetSprite = SpaceArt.PreparePlanet(x, y, radius, deep, viewport, (int)Math.Ceiling(screen.Width * 0.003));
        }

        private void ReleaseSky()
        {
            if (planetSprite != null) { planetSprite.Dispose(); planetSprite = null; }
            if (cloudSprite != null) { cloudSprite.Dispose(); cloudSprite = null; }
        }

        private void DrawStars(Graphics g, float width, float height, float planetX, float planetY, float radiusLimit)
        {
            for (int i = 0; i < starCount; i++)
            {
                Star star = stars[i];
                float x = (float)Wrap(star.X - elapsed * (0.38 + star.Depth * 0.87), width);
                float y = (float)Wrap(star.Y + elapsed * star.Depth * 0.19, height);
                float dx = x - planetX, dy = y - planetY;
                if (dx * dx + dy * dy < radiusLimit * radiusLimit) continue;
                float radius = star.Radius;
                if (star.Bright) radius += (float)(0.16 * Math.Sin(elapsed * 0.65 + i));
                g.FillEllipse(StarColors[star.Color], x - radius, y - radius, radius * 2, radius * 2);
                if (!star.Bright) continue;
                g.DrawLine(Diffract, x - radius * 2.1f, y, x + radius * 2.1f, y);
                g.DrawLine(Diffract, x, y - radius * 1.75f, x, y + radius * 1.75f);
            }
        }

        private void DrawDust(Graphics g)
        {
            for (int i = 0; i < dustCount; i++)
            {
                Dust particle = dust[i];
                g.FillEllipse(DustColor, particle.X - particle.Radius, particle.Y - particle.Radius * 0.5f,
                    particle.Radius * 2, particle.Radius);
            }
        }

        private void DrawSatellite(Graphics g, float width, float height)
        {
            double t = motion ? 1 - satelliteAge / satelliteDuration : 0.51;
            double path = satelliteMirrored ? 1 - t : t;
            float margin = 50 * scale;
            float x = (float)(-margin + path * (width + 2 * margin));
            float y = (float)(height * (0.435 - 0.225 * path + 0.08 * Math.Sin(path * Math.PI)));
            float pitch = (float)(-9 + 5.6 * Math.Cos(t * Math.PI) + Math.Sin(elapsed * 0.71) * 0.9);
            double glint = Math.Pow(Math.Max(0, Math.Cos((t - 0.61) * Math.PI)), 38);
            SpaceArt.Satellite(g, x, y, scale, pitch, satelliteMirrored, glint);
        }

        public void Dispose()
        {
            ReleaseSky();
            masking.Dispose();
        }
    }
}
