using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace SnowDesk
{
    /// <summary>
    /// Hand-shaded vector art for the summer theme, in the same language as <see cref="DecorationArt"/>:
    /// contoured bodies with vertical shading ramps, tapered limbs and thin crease lines. Every figure
    /// stands on (0, 0), faces right and is about as tall as the winter Santa.
    /// </summary>
    internal static class SummerArt
    {
        internal enum Pose { Walk, Run, Sit }

        private static readonly Color SkinLight = Color.FromArgb(242, 196, 160), SkinDark = Color.FromArgb(208, 148, 112);
        private static readonly Brush Skin = DecorationArt.Ramp(-66, -6, SkinLight, SkinDark);
        private static readonly Brush SkinFar = Ink(196, 138, 104);
        private static readonly Brush Blush = new SolidBrush(Color.FromArgb(90, 236, 120, 120));
        private static readonly Brush HairFill = DecorationArt.Ramp(-68, -50, Color.FromArgb(128, 72, 44), Color.FromArgb(72, 38, 28));
        private static readonly Brush Swim = DecorationArt.Ramp(-50, -28, Color.FromArgb(244, 104, 112), Color.FromArgb(186, 54, 76));
        private static readonly Brush SwimDot = new SolidBrush(Color.FromArgb(220, 255, 236, 230));
        private static readonly Brush RingFill = DecorationArt.Ramp(-8, 6, Color.FromArgb(255, 150, 188), Color.FromArgb(206, 78, 124));
        private static readonly Brush RingStripe = new SolidBrush(Color.FromArgb(250, 252, 246, 248));
        private static readonly Brush CanopyTeal = DecorationArt.Ramp(-60, -40, Color.FromArgb(72, 196, 196), Color.FromArgb(28, 128, 140));
        private static readonly Brush CanopyCream = DecorationArt.Ramp(-60, -40, Color.FromArgb(255, 248, 228), Color.FromArgb(226, 208, 176));
        private static readonly Brush Coat = DecorationArt.Ramp(-62, -14, Color.FromArgb(255, 214, 82), Color.FromArgb(212, 146, 30));
        private static readonly Brush CoatFar = Ink(196, 134, 30);
        private static readonly Brush Boot = DecorationArt.Ramp(-8, 0, Color.FromArgb(74, 132, 92), Color.FromArgb(38, 82, 56));
        private static readonly Brush Navy = Ink(48, 56, 82);
        private static readonly Brush Dark = Ink(40, 38, 43);
        private static readonly Brush Eye = Ink(52, 40, 40);
        private static readonly Brush Glint = new SolidBrush(Color.FromArgb(235, 255, 255, 255));
        private static readonly Brush Lens = Ink(54, 46, 62);
        private static readonly Brush Cab = DecorationArt.Ramp(-34, -10, Color.FromArgb(86, 156, 214), Color.FromArgb(38, 90, 150));
        private static readonly Brush Tank = DecorationArt.Ramp(-34, -14, Color.FromArgb(226, 234, 240), Color.FromArgb(124, 140, 154));
        private static readonly Brush Chassis = Ink(57, 48, 45);
        private static readonly Brush Glass = Ink(90, 132, 151), GlassLight = Ink(166, 201, 210);
        private static readonly Brush Amber = Ink(239, 183, 83), AmberGlow = new SolidBrush(Color.FromArgb(120, 255, 170, 70));
        private static readonly Brush HoseCoil = Ink(36, 112, 180);
        private static readonly Brush DuckFill = DecorationArt.Ramp(-16, 0, Color.FromArgb(255, 228, 96), Color.FromArgb(230, 164, 28));
        private static readonly Brush BeakFill = DecorationArt.Ramp(-12, -8, Color.FromArgb(252, 152, 62), Color.FromArgb(214, 96, 32));
        private static readonly Brush PadFill = DecorationArt.Ramp(-4, 2, Color.FromArgb(112, 182, 104), Color.FromArgb(52, 122, 72));
        private static readonly Brush PetalFill = DecorationArt.Ramp(-10, -1, Color.FromArgb(255, 226, 238), Color.FromArgb(232, 128, 172));
        private static readonly Brush FrogFill = DecorationArt.Ramp(-12, 0, Color.FromArgb(132, 196, 92), Color.FromArgb(62, 132, 58));
        private static readonly Brush FrogBelly = Ink(214, 230, 150);
        private static readonly Brush FrogSpot = new SolidBrush(Color.FromArgb(120, 40, 92, 44));
        private static readonly Brush Pollen = Ink(246, 204, 70);
        private static readonly Pen Crease = Stroke(Color.FromArgb(110, 36, 35, 40), 0.8f);
        private static readonly Pen Seam = Stroke(Color.FromArgb(130, 150, 64, 46), 0.7f);
        private static readonly Pen Rib = Stroke(Color.FromArgb(120, 30, 70, 76), 0.7f);
        private static readonly Pen PoleLine = Stroke(Color.FromArgb(150, 110, 82), 1.5f);
        private static readonly Pen Strap = Stroke(Color.FromArgb(186, 54, 76), 0.9f);
        private static readonly Pen Shine = Stroke(Color.FromArgb(170, 255, 255, 255), 0.9f);
        private static readonly Pen Vein = Stroke(Color.FromArgb(90, 34, 84, 48), 0.6f);
        private static readonly Pen Reflective = Stroke(Color.FromArgb(220, 236, 240, 232), 1.4f);
        private static readonly Pen Lash = Stroke(Color.FromArgb(60, 40, 40), 0.8f);
        internal static readonly Pen Hose = Stroke(Color.FromArgb(36, 112, 180), 2.6f);
        internal static readonly Pen HoseFlow = new Pen(Color.FromArgb(200, 196, 232, 252), 1.1f) { DashPattern = new[] { 2f, 3f } };
        private static readonly Brush GaugeBack = Ink(44, 62, 78);
        private static readonly Brush GaugeWater = DecorationArt.Ramp(-31, -22, Color.FromArgb(120, 196, 240), Color.FromArgb(40, 112, 182));
        private static readonly Brush Steel = Ink(150, 162, 172);
        private static readonly Pen WaterRim = Stroke(Color.FromArgb(215, 236, 248, 255), 1.1f);
        private static readonly Pen WaterStreak = Stroke(Color.FromArgb(80, 255, 255, 255), 1f);
        private static readonly Pen WetEdge = Stroke(Color.FromArgb(70, 20, 40, 60), 1f);
        private static readonly GraphicsPath Torso = DecorationArt.RoundedPolygon(new[] {
            new PointF(-4.5f, -51), new PointF(4.5f, -51.5f), new PointF(6.2f, -45), new PointF(3.4f, -38),
            new PointF(5.6f, -31), new PointF(-5.4f, -30), new PointF(-3.8f, -38), new PointF(-6, -45)
        }, 0.3f);
        private static readonly GraphicsPath Top = DecorationArt.RoundedPolygon(new[] {
            new PointF(-5.6f, -47.5f), new PointF(5.8f, -47.5f), new PointF(5.4f, -43.2f), new PointF(-5.4f, -43.2f)
        }, 0.3f);
        private static readonly GraphicsPath Bottom = DecorationArt.RoundedPolygon(new[] {
            new PointF(-5.4f, -34), new PointF(5.6f, -34.5f), new PointF(5.2f, -30), new PointF(1, -28.4f), new PointF(-5, -30)
        }, 0.3f);
        private static readonly GraphicsPath Raincoat = DecorationArt.RoundedPolygon(new[] {
            new PointF(-6, -52), new PointF(6, -53), new PointF(9, -42), new PointF(11, -25),
            new PointF(-8, -24), new PointF(-7, -40)
        }, 0.22f);
        private static readonly GraphicsPath TruckCab = DecorationArt.RoundedPolygon(new[] {
            new PointF(14, -10), new PointF(14, -33), new PointF(19, -35), new PointF(30, -34),
            new PointF(36, -23), new PointF(36, -10)
        }, 0.2f);

        internal static void Bather(Graphics g, Pose pose, double phase)
        {
            if (pose == Pose.Sit)
            {
                Floating(g, phase);
                return;
            }
            bool run = pose == Pose.Run;
            if (run) g.RotateTransform(9);
            float stride = run ? 12 : 7.5f, lift = run ? 7 : 3;
            Leg(g, SkinFar, -30, phase + 1, stride, lift);
            Arm(g, SkinFar, phase, run, -1);
            Ring(g, -32, 14, 4.8f, true);
            g.FillPath(Skin, Torso);
            g.DrawPath(Crease, Torso);
            Swimsuit(g);
            Ring(g, -32, 14, 4.8f, false);
            Leg(g, Skin, -30, phase, stride, lift);
            ClosedUmbrella(g, run, phase);
            Arm(g, Skin, phase + 1, run, 1);
            Head(g, 0.5f, -58, (float)Math.Sin(phase * Math.PI) * (run ? 4 : 1.5f), run ? 6 : 0, false);
        }

        private static void Floating(Graphics g, double phase)
        {
            float bob = (float)Math.Sin(phase * 1.7) * 1.1f;
            g.TranslateTransform(0, bob);
            Ring(g, 0, 19, 6, true);
            GraphicsState lean = g.Save();
            g.RotateTransform(-6);
            g.DrawLine(PoleLine, -9, 6, -7, -45);
            g.Restore(lean);
            GraphicsState seated = g.Save();
            g.TranslateTransform(0, 29);
            g.FillPath(Skin, Torso);
            g.DrawPath(Crease, Torso);
            Swimsuit(g);
            g.Restore(seated);
            Ring(g, 0, 19, 6, false);
            float kick = (float)Math.Sin(phase * 4.2) * 2.4f;
            Taper(g, SkinFar, 5.2f, 2.6f, new PointF(0, -2), new PointF(9, -6.5f), new PointF(17, 0.5f - kick));
            Taper(g, Skin, 5.6f, 2.8f, new PointF(2, -2), new PointF(12, -7.5f), new PointF(20, -1 + kick));
            Taper(g, Skin, 3.4f, 2.4f, new PointF(-2, -19), new PointF(-8.5f, -23), new PointF(-8.4f, -30));
            Taper(g, Skin, 3.4f, 2.4f, new PointF(3, -19), new PointF(9, -13), new PointF(14, -8));
            Head(g, 0.5f, -29, 0, 0, true);
            BeachUmbrella(g, -10.5f, -44, 24);
            g.TranslateTransform(0, -bob);
        }

        private static void Swimsuit(Graphics g)
        {
            g.FillPath(Swim, Top);
            g.FillPath(Swim, Bottom);
            g.DrawPath(Crease, Top);
            g.DrawPath(Crease, Bottom);
            g.DrawLine(Strap, -2.5f, -47.5f, -3.4f, -51);
            g.DrawLine(Strap, 3, -47.5f, 3.6f, -51.2f);
            for (int dot = 0; dot < 3; dot++)
            {
                g.FillEllipse(SwimDot, -3.8f + dot * 3.4f, -46.4f + (dot % 2), 1.3f, 1.3f);
                g.FillEllipse(SwimDot, -3.2f + dot * 3.2f, -32.6f + (dot % 2) * 0.8f, 1.2f, 1.2f);
            }
            g.FillEllipse(SkinFar, -0.4f, -37.6f, 1.2f, 1.4f);
        }

        private static void Head(Graphics g, float x, float y, float sway, float wind, bool relaxed)
        {
            g.FillRectangle(SkinFar, x - 1.8f, y + 4.5f, 3.6f, 4);
            using (var tail = new GraphicsPath())
            {
                tail.AddBezier(x - 4, y - 5.6f, x - 11 - wind, y - 6 - sway * 0.3f, x - 13 - wind - sway, y + 1, x - 10.5f - wind - sway, y + 9);
                tail.AddBezier(x - 10.5f - wind - sway, y + 9, x - 10 - wind, y + 2, x - 8, y - 2, x - 3.5f, y - 2.8f);
                tail.CloseFigure();
                g.FillPath(HairFill, tail);
                g.DrawPath(Crease, tail);
            }
            using (var face = new GraphicsPath())
            {
                face.AddBezier(x - 5.6f, y - 1, x - 6, y - 6.8f, x + 2, y - 8.6f, x + 5.6f, y - 3.6f);
                face.AddBezier(x + 5.6f, y - 3.6f, x + 6.6f, y - 1.8f, x + 7.4f, y - 0.6f, x + 6, y + 0.4f);
                face.AddBezier(x + 6, y + 0.4f, x + 6.6f, y + 2.2f, x + 5, y + 6.4f, x + 0.6f, y + 6.6f);
                face.AddBezier(x + 0.6f, y + 6.6f, x - 4.4f, y + 6.6f, x - 5.6f, y + 3, x - 5.6f, y - 1);
                face.CloseFigure();
                g.FillPath(Skin, face);
                g.DrawPath(Crease, face);
            }
            using (var hair = new GraphicsPath())
            {
                hair.AddBezier(x - 6.4f, y + 1.6f, x - 7.6f, y - 7.8f, x + 1.6f, y - 10.4f, x + 6, y - 4.6f);
                hair.AddBezier(x + 6, y - 4.6f, x + 2.4f, y - 5.6f, x - 0.8f, y - 3.4f, x - 2.4f, y + 0.4f);
                hair.AddBezier(x - 2.4f, y + 0.4f, x - 3.4f, y - 0.6f, x - 5, y + 0.4f, x - 6.4f, y + 1.6f);
                hair.CloseFigure();
                g.FillPath(HairFill, hair);
            }
            g.FillEllipse(Lens, x - 2.2f, y - 8.4f, 3.6f, 2.2f);
            g.FillEllipse(Lens, x + 1.6f, y - 8, 3.6f, 2.2f);
            g.DrawLine(Shine, x - 1.4f, y - 7.9f, x - 0.2f, y - 8.1f);
            if (relaxed) g.DrawArc(Lash, x + 1.8f, y - 2.4f, 2.8f, 1.8f, 10, 160);
            else
            {
                g.FillEllipse(Eye, x + 2.6f, y - 2.6f, 1.7f, 2);
                g.FillEllipse(Glint, x + 3.2f, y - 2.4f, 0.6f, 0.6f);
            }
            g.FillEllipse(Blush, x + 0.8f, y + 0.8f, 3.2f, 1.8f);
            g.DrawArc(Seam, x + 2.6f, y + 2.4f, 2.8f, 1.6f, 15, 140);
        }

        private static void Leg(Graphics g, Brush skin, float hip, double phase, float stride, float lift)
        {
            float swing = (float)Math.Cos(phase * Math.PI);
            float raise = Math.Max(0, (float)Math.Sin(phase * Math.PI)) * lift;
            float footX = swing * stride;
            Taper(g, skin, 6.4f, 3, new PointF(0, hip), new PointF(footX * 0.42f + 2.6f, hip * 0.47f - raise), new PointF(footX, -1.6f - raise));
            g.FillEllipse(skin, footX - 1.4f, -3 - raise, 5, 2.8f);
        }

        private static void Arm(Graphics g, Brush skin, double phase, bool run, float side)
        {
            float swing = (float)Math.Cos(phase * Math.PI) * (run ? 9 : 5);
            var hand = new PointF(swing + (run ? 4 : 0), run ? -36 : -32);
            Taper(g, skin, 3.4f, 2.3f, new PointF(side * 0.4f, -49), new PointF(swing * 0.55f, -41), hand);
        }

        private static void ClosedUmbrella(Graphics g, bool run, double phase)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(run ? 6 : -9, run ? -34 : -44);
            g.RotateTransform(run ? 62 : -38);
            using (var furled = new GraphicsPath())
            {
                furled.AddBezier(0, -16, 2.6f, -10, 2.6f, 2, 0.6f, 6);
                furled.AddLine(0.6f, 6, -0.6f, 6);
                furled.AddBezier(-0.6f, 6, -2.6f, 2, -2.6f, -10, 0, -16);
                furled.CloseFigure();
                g.FillPath(CanopyTeal, furled);
                g.DrawPath(Crease, furled);
            }
            g.DrawLine(PoleLine, 0, 6, 0, 11);
            g.DrawArc(PoleLine, -2.6f, 9.6f, 2.8f, 3, 0, 180);
            g.Restore(state);
        }

        private static void Ring(Graphics g, float y, float rx, float ry, bool back)
        {
            // The back half is drawn behind the body and the front half over it, so the ring reads as a torus.
            float tube = rx * 0.38f;
            var outer = new RectangleF(-rx, y - ry, rx * 2, ry * 2);
            var inner = new RectangleF(-rx + tube, y - ry + tube * 0.45f, rx * 2 - tube * 2, ry * 2 - tube * 0.9f);
            using (var shape = new GraphicsPath())
            {
                shape.AddArc(outer, back ? 180 : 0, 180);
                shape.AddArc(inner, back ? 0 : 180, -180);
                shape.CloseFigure();
                using (var fill = new LinearGradientBrush(new RectangleF(-rx, y - ry - 1, rx * 2, ry * 2 + 2),
                    Color.FromArgb(255, 160, 196), Color.FromArgb(200, 72, 118), LinearGradientMode.Vertical))
                    g.FillPath(fill, shape);
                GraphicsState clip = g.Save();
                g.SetClip(shape, CombineMode.Intersect);
                for (int stripe = -3; stripe <= 3; stripe += 2)
                    g.FillRectangle(RingStripe, stripe * rx / 4f - rx / 10f, y - ry * 2, rx / 5f, ry * 4);
                g.Restore(clip);
                g.DrawPath(Crease, shape);
            }
            if (!back) g.DrawArc(Shine, -rx * 0.7f, y - ry * 0.2f, rx * 0.9f, ry * 1.1f, 110, 50);
            else g.FillEllipse(RingFill, rx * 0.55f, y - ry - 1.4f, 2.4f, 1.8f);
        }

        private static void BeachUmbrella(Graphics g, float x, float y, float radius)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform(-6);
            const int Panels = 6;
            float sag = radius * 0.16f;
            for (int panel = 0; panel < Panels; panel++)
            {
                float a = -radius + panel * radius * 2 / Panels, b = a + radius * 2 / Panels;
                using (var piece = new GraphicsPath())
                {
                    piece.AddLine(0, -radius * 0.55f, a, 0);
                    piece.AddBezier(a, 0, a + (b - a) * 0.3f, sag, a + (b - a) * 0.7f, sag, b, 0);
                    piece.CloseFigure();
                    g.FillPath(panel % 2 == 0 ? CanopyTeal : CanopyCream, piece);
                }
                g.DrawLine(Rib, 0, -radius * 0.55f, a, 0);
            }
            using (var hem = new GraphicsPath())
            {
                hem.AddBezier(-radius, 0, -radius * 0.5f, -radius * 0.62f, radius * 0.5f, -radius * 0.62f, radius, 0);
                g.DrawPath(Crease, hem);
            }
            g.DrawBezier(Shine, -radius * 0.7f, -radius * 0.12f, -radius * 0.5f, -radius * 0.36f, -radius * 0.2f, -radius * 0.48f, 0, -radius * 0.52f);
            g.FillEllipse(Amber, -1.5f, -radius * 0.55f - 2.6f, 3, 3);
            g.Restore(state);
        }

        /// <summary>A jogger in a yellow raincoat with the hood up and green boots.</summary>
        internal static void Runner(Graphics g, double phase)
        {
            g.RotateTransform(8);
            RunnerLeg(g, Navy, phase + 1, true);
            Taper(g, CoatFar, 4.4f, 3.4f, new PointF(-1, -48), new PointF(-7 - (float)Math.Cos(phase * Math.PI) * 4, -40), new PointF(-4, -33));
            g.FillPath(Coat, Raincoat);
            g.DrawPath(Crease, Raincoat);
            g.DrawLine(Reflective, -7.6f, -28, 10.4f, -28.4f);
            g.DrawBezier(Crease, 2, -50, 2.4f, -42, 1.6f, -33, 2.2f, -25);
            for (int button = 0; button < 3; button++) g.FillEllipse(Dark, 3, -47 + button * 6.5f, 1.4f, 1.4f);
            g.DrawBezier(Crease, -6, -40, -3, -38, 3, -38, 8, -40);
            RunnerLeg(g, Navy, phase, false);
            float swing = (float)Math.Cos(phase * Math.PI) * 6;
            Taper(g, Coat, 4.6f, 3.6f, new PointF(2, -48), new PointF(6 + swing, -40), new PointF(11 + swing * 0.6f, -44));
            g.FillEllipse(Skin, 9.6f + swing * 0.6f, -46.4f, 3.6f, 3.4f);
            using (var hood = new GraphicsPath())
            {
                hood.AddBezier(-7, -51, -9, -60, -4, -67.5f, 3, -67);
                hood.AddBezier(3, -67, 8.4f, -66.6f, 10.6f, -61, 9.6f, -55);
                hood.AddBezier(9.6f, -55, 7, -51, 0, -50, -7, -51);
                hood.CloseFigure();
                g.FillPath(Coat, hood);
                g.DrawPath(Crease, hood);
            }
            using (var face = new GraphicsPath())
            {
                face.AddEllipse(1.4f, -63.4f, 8, 9.6f);
                g.FillPath(Skin, face);
            }
            g.DrawBezier(Crease, 2, -64, 5, -66, 9, -64, 10, -59);
            g.FillEllipse(Eye, 6.4f, -60.6f, 1.4f, 1.7f);
            g.FillEllipse(Blush, 5.4f, -57.8f, 2.8f, 1.6f);
            g.DrawArc(Seam, 6, -56.6f, 2.6f, 1.4f, 10, 150);
        }

        private static void RunnerLeg(Graphics g, Brush trouser, double phase, bool far)
        {
            float swing = (float)Math.Cos(phase * Math.PI);
            float raise = Math.Max(0, (float)Math.Sin(phase * Math.PI)) * 8;
            float footX = swing * 14;
            Taper(g, far ? Dark : trouser, 5.6f, 4.2f, new PointF(0, -27), new PointF(footX * 0.4f + 3.4f, -14 - raise), new PointF(footX, -6 - raise));
            using (GraphicsPath boot = DeskStyle.Rounded(new Rectangle((int)Math.Round(footX - 2.6f), (int)Math.Round(-8 - raise), 7, 8), 2))
            {
                g.FillPath(Boot, boot);
                g.DrawPath(Crease, boot);
            }
        }

        /// <summary>
        /// A small municipal drainage truck: blue cab, steel tank with a sight glass showing the water it has
        /// taken in (<paramref name="fill"/> 0..1, tilted by <paramref name="slosh"/>), hose reel at the front.
        /// </summary>
        internal static void Pump(Graphics g, double phase, bool working, double fill, double slosh)
        {
            float shake = working ? (float)Math.Sin(phase * 38) * 0.45f : 0;
            g.TranslateTransform(0, shake);
            g.FillRectangle(Chassis, -36, -15, 72, 9);
            using (var tank = new GraphicsPath())
            {
                tank.AddArc(-37, -35, 8, 21, 90, 180);
                tank.AddArc(8, -35, 8, 21, 270, 180);
                tank.CloseFigure();
                g.FillPath(Tank, tank);
                g.DrawPath(Crease, tank);
            }
            g.DrawArc(Crease, 8, -35, 8, 21, 90, 180);
            g.DrawLine(Crease, -21, -34.6f, -21, -14.4f);
            g.DrawLine(Crease, -1, -34.6f, -1, -14.4f);
            g.FillRectangle(Steel, -16, -38, 9, 3.4f);
            g.DrawRectangle(Crease, -16, -38, 9, 3.4f);
            SightGlass(g, new RectangleF(-30, -30.5f, 34, 8.5f), fill, slosh, phase);
            g.DrawLine(Shine, -31, -33, 10, -33);
            g.FillRectangle(Cab, -34, -17.6f, 50, 2.4f);
            g.FillPath(Cab, TruckCab);
            g.DrawPath(Crease, TruckCab);
            g.FillRectangle(Glass, 21, -31, 11, 9);
            g.FillRectangle(GlassLight, 22, -30, 2, 7);
            g.DrawLine(Reflective, 15, -16, 35, -16);
            g.FillEllipse(HoseCoil, 33, -24, 9, 9);
            g.FillEllipse(Glass, 35.4f, -21.6f, 4.2f, 4.2f);
            g.FillRectangle(Dark, -38, -12, 3, 6);
            if (working && Math.Sin(phase * 7) > 0) g.FillEllipse(AmberGlow, 19, -40, 9, 7);
            g.FillRectangle(working && Math.Sin(phase * 7) > 0 ? Amber : Chassis, 21, -37.6f, 5, 3);
            g.TranslateTransform(0, -shake);
            DecorationArt.Wheel(g, -22, phase * 3);
            DecorationArt.Wheel(g, 24, phase * 3);
        }

        private static void SightGlass(Graphics g, RectangleF window, double fill, double slosh, double phase)
        {
            using (GraphicsPath glass = DeskStyle.Rounded(Rectangle.Round(window), 3))
            {
                g.FillPath(GaugeBack, glass);
                GraphicsState state = g.Save();
                g.SetClip(glass, CombineMode.Intersect);
                float level = window.Bottom - window.Height * (float)Math.Max(0, Math.Min(1, fill));
                float tilt = (float)slosh * 1.6f;
                float wave = (float)Math.Sin(phase * 4) * 0.35f;
                using (var water = new GraphicsPath())
                {
                    water.AddLine(window.Left - 1, level + tilt + wave, window.Right + 1, level - tilt - wave);
                    water.AddLine(window.Right + 1, window.Bottom + 1, window.Left - 1, window.Bottom + 1);
                    water.CloseFigure();
                    g.FillPath(GaugeWater, water);
                }
                g.DrawLine(WaterRim, window.Left, level + tilt + wave, window.Right, level - tilt - wave);
                g.DrawLine(Shine, window.Left + 2, window.Top + 1.4f, window.Left + 9, window.Top + 1.4f);
                g.Restore(state);
                g.DrawPath(Crease, glass);
            }
            for (int tick = 1; tick < 4; tick++)
            {
                float y = window.Bottom - window.Height * tick / 4f;
                g.DrawLine(Crease, window.Right - 2.6f, y, window.Right, y);
            }
        }

        internal static void Duck(Graphics g, double phase)
        {
            g.RotateTransform((float)Math.Sin(phase * 2.3) * 6);
            using (var body = new GraphicsPath())
            {
                body.AddBezier(-10, -2, -12, -9, -5, -10.5f, 1, -8);
                body.AddBezier(1, -8, 7, -7, 10.4f, -4.6f, 9, 0);
                body.AddBezier(9, 0, 3, 1.6f, -5, 1.6f, -10, -2);
                body.CloseFigure();
                g.FillPath(DuckFill, body);
                g.DrawPath(Crease, body);
            }
            g.DrawBezier(Crease, -6, -5.6f, -3, -3, 1, -3, 3, -5.2f);
            g.FillEllipse(DuckFill, -1.6f, -16, 10, 10);
            g.DrawArc(Crease, -1.6f, -16, 10, 10, 200, 230);
            using (var beak = new GraphicsPath())
            {
                beak.AddBezier(7.4f, -12.4f, 10, -13, 12.8f, -11.6f, 12.6f, -10.4f);
                beak.AddBezier(12.6f, -10.4f, 11, -9.4f, 9, -9.2f, 7.4f, -9.6f);
                beak.CloseFigure();
                g.FillPath(BeakFill, beak);
            }
            g.FillEllipse(Eye, 4, -13.6f, 1.8f, 2);
            g.FillEllipse(Glint, 4.5f, -13.4f, 0.6f, 0.6f);
            g.DrawArc(Shine, -0.4f, -15, 5, 5, 200, 70);
        }

        internal static void LilyPad(Graphics g, float radius, bool flower)
        {
            g.FillPie(PadFill, -radius, -radius * 0.32f, radius * 2, radius * 0.64f, 22, 316);
            g.DrawArc(Crease, -radius, -radius * 0.32f, radius * 2, radius * 0.64f, 22, 316);
            for (int vein = 0; vein < 5; vein++)
            {
                double angle = (60 + vein * 60) * Math.PI / 180;
                g.DrawLine(Vein, 0, 0, (float)Math.Cos(angle) * radius * 0.8f, (float)Math.Sin(angle) * radius * 0.26f);
            }
            if (!flower) return;
            for (int petal = -2; petal <= 2; petal++)
            {
                GraphicsState state = g.Save();
                g.TranslateTransform(0, -1);
                g.RotateTransform(petal * 26);
                using (var shape = new GraphicsPath())
                {
                    shape.AddBezier(0, 0, -3, -2.6f, -2.4f, -7.4f, 0, -9.6f);
                    shape.AddBezier(0, -9.6f, 2.4f, -7.4f, 3, -2.6f, 0, 0);
                    shape.CloseFigure();
                    g.FillPath(PetalFill, shape);
                    g.DrawPath(Crease, shape);
                }
                g.Restore(state);
            }
            g.FillEllipse(Pollen, -1.8f, -3.6f, 3.6f, 2.4f);
        }

        internal static void Frog(Graphics g, double crouch)
        {
            g.ScaleTransform(1, (float)(1 - crouch * 0.25));
            g.FillEllipse(FrogFill, -9.6f, -4, 6.4f, 4);
            using (var body = new GraphicsPath())
            {
                body.AddBezier(-7, 0, -8.6f, -6, -4, -10, 1, -9.6f);
                body.AddBezier(1, -9.6f, 6, -9.4f, 8.4f, -5, 7, 0);
                body.CloseFigure();
                g.FillPath(FrogFill, body);
                g.DrawPath(Crease, body);
            }
            g.FillEllipse(FrogSpot, -4.4f, -7.4f, 2.4f, 1.6f);
            g.FillEllipse(FrogSpot, -1.4f, -8.8f, 1.8f, 1.2f);
            g.FillEllipse(FrogBelly, 1.6f, -4.4f, 5, 3.6f);
            for (int side = 0; side < 2; side++)
            {
                float ex = side == 0 ? -2.4f : 2.6f;
                g.FillEllipse(FrogFill, ex - 2.4f, -12.4f, 4.8f, 4.6f);
                g.DrawArc(Crease, ex - 2.4f, -12.4f, 4.8f, 4.6f, 180, 180);
                g.FillEllipse(Eye, ex - 0.6f, -11.2f, 1.8f, 2);
                g.FillEllipse(Glint, ex - 0.1f, -11, 0.6f, 0.6f);
            }
            g.DrawArc(Seam, 2.6f, -6.8f, 4, 1.8f, 10, 150);
            g.FillEllipse(FrogFill, 3.4f, -1.6f, 5.4f, 2.4f);
        }

        /// <summary>Side view of a puddle: flattened lens with a moving surface, sky reflection and rim light.</summary>
        internal static void Puddle(Graphics g, SummerScene.Pool pool, double ground, double scale, double clock)
        {
            double half = pool.Half;
            if (half < 3) return;
            double depth = pool.Depth(scale);
            const int Steps = 26;
            var surface = new PointF[Steps + 1];
            for (int i = 0; i <= Steps; i++)
            {
                double t = -1 + 2.0 * i / Steps;
                double profile = SummerScene.Pool.Profile(t);
                double wave = Math.Sin(clock * 1.7 + i * 0.75 + pool.Seed) * 0.35 * scale * profile;
                surface[i] = new PointF((float)(pool.X + t * half), (float)(ground - depth * profile + wave));
            }
            float baseY = (float)ground;
            g.DrawLine(WetEdge, (float)(pool.X - half - 4 * scale), baseY - 0.5f, (float)(pool.X + half + 4 * scale), baseY - 0.5f);
            using (var water = new GraphicsPath())
            {
                water.AddCurve(surface, 0.4f);
                water.AddLine(surface[Steps].X, baseY, surface[0].X, baseY);
                water.CloseFigure();
                using (var fill = new LinearGradientBrush(new RectangleF((float)(pool.X - half), (float)(ground - depth - 1),
                    (float)half * 2, (float)depth + 2), Color.FromArgb(170, 178, 224, 248), Color.FromArgb(215, 46, 104, 164),
                    LinearGradientMode.Vertical))
                    g.FillPath(fill, water);
            }
            g.DrawCurve(WaterRim, surface, 2, Steps - 4, 0.4f);
            float streakY = (float)(ground - depth * 0.42);
            g.DrawLine(WaterStreak, (float)(pool.X - half * 0.45), streakY, (float)(pool.X + half * 0.1), streakY);
            g.DrawLine(WaterStreak, (float)(pool.X + half * 0.22), streakY + 1.6f, (float)(pool.X + half * 0.5), streakY + 1.6f);
        }

        /// <summary>Soft rainbow arcs that fade in after a shower; alpha is 0..1.</summary>
        internal static void Rainbow(Graphics g, float centerX, float baseY, float radius, double alpha)
        {
            Color[] bands =
            {
                Color.FromArgb(232, 74, 84), Color.FromArgb(246, 150, 64), Color.FromArgb(246, 214, 82),
                Color.FromArgb(104, 196, 110), Color.FromArgb(76, 150, 222), Color.FromArgb(150, 104, 206)
            };
            float width = Math.Max(3, radius * 0.028f);
            for (int band = 0; band < bands.Length; band++)
            {
                float r = radius - band * width;
                using (var pen = new Pen(Color.FromArgb((int)(alpha * 46), bands[band]), width + 0.6f))
                    g.DrawArc(pen, centerX - r, baseY - r, r * 2, r * 2, 180, 180);
            }
        }

        /// <summary>Warm sun glow in the corner while the shower is over; alpha is 0..1.</summary>
        internal static void Sun(Graphics g, float x, float y, float radius, double alpha, double phase)
        {
            using (var glow = new GraphicsPath())
            {
                glow.AddEllipse(x - radius * 2.4f, y - radius * 2.4f, radius * 4.8f, radius * 4.8f);
                using (var fill = new PathGradientBrush(glow))
                {
                    fill.CenterColor = Color.FromArgb((int)(alpha * 90), 255, 226, 140);
                    fill.SurroundColors = new[] { Color.FromArgb(0, 255, 226, 140) };
                    g.FillPath(fill, glow);
                }
            }
            using (var rays = new Pen(Color.FromArgb((int)(alpha * 110), 255, 214, 110), Math.Max(1.5f, radius * 0.08f)))
            {
                rays.StartCap = rays.EndCap = LineCap.Round;
                for (int ray = 0; ray < 10; ray++)
                {
                    double angle = ray * Math.PI / 5 + phase * 0.12;
                    float cos = (float)Math.Cos(angle), sin = (float)Math.Sin(angle);
                    g.DrawLine(rays, x + cos * radius * 1.25f, y + sin * radius * 1.25f, x + cos * radius * 1.7f, y + sin * radius * 1.7f);
                }
            }
            using (var disc = new SolidBrush(Color.FromArgb((int)(alpha * 210), 255, 218, 108)))
                g.FillEllipse(disc, x - radius, y - radius, radius * 2, radius * 2);
        }

        /// <summary>Fills a limb that runs through the spine points and narrows from w0 to w1, with round ends.</summary>
        private static void Taper(Graphics g, Brush fill, float w0, float w1, params PointF[] spine)
        {
            int count = spine.Length;
            var outline = new PointF[count * 2];
            for (int i = 0; i < count; i++)
            {
                PointF before = spine[Math.Max(0, i - 1)], after = spine[Math.Min(count - 1, i + 1)];
                float dx = after.X - before.X, dy = after.Y - before.Y;
                float length = (float)Math.Sqrt(dx * dx + dy * dy);
                if (length < 0.001f) length = 1;
                float half = (w0 + (w1 - w0) * i / (count - 1)) * 0.5f;
                float nx = -dy / length * half, ny = dx / length * half;
                outline[i] = new PointF(spine[i].X + nx, spine[i].Y + ny);
                outline[count * 2 - 1 - i] = new PointF(spine[i].X - nx, spine[i].Y - ny);
            }
            using (var limb = new GraphicsPath())
            {
                limb.AddClosedCurve(outline, 0.3f);
                g.FillPath(fill, limb);
                g.DrawPath(Crease, limb);
            }
            g.FillEllipse(fill, spine[0].X - w0 * 0.5f, spine[0].Y - w0 * 0.5f, w0, w0);
            g.FillEllipse(fill, spine[count - 1].X - w1 * 0.5f, spine[count - 1].Y - w1 * 0.5f, w1, w1);
        }

        private static Brush Ink(int red, int green, int blue)
        {
            return new SolidBrush(Color.FromArgb(red, green, blue));
        }

        private static Pen Stroke(Color color, float width)
        {
            return new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        }
    }
}
