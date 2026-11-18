using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace SnowDesk
{
    /// <summary>Vector art for the summer theme. Every figure stands on (0, 0) and faces right.</summary>
    internal static class SummerArt
    {
        internal enum Pose { Walk, Run, Sit }

        private static readonly Brush SkinBrush = Ink(226, 168, 128);
        private static readonly Brush SkinShade = Ink(196, 136, 100);
        private static readonly Brush Hair = Ink(92, 52, 36);
        private static readonly Brush Coral = Ink(236, 92, 104);
        private static readonly Brush CoralDark = Ink(186, 62, 78);
        private static readonly Brush Shades = Ink(32, 30, 40);
        private static readonly Brush RingPink = Ink(247, 128, 170);
        private static readonly Brush RingWhite = Ink(255, 244, 248);
        private static readonly Brush Teal = Ink(42, 176, 182);
        private static readonly Brush Sand = Ink(255, 241, 214);
        private static readonly Brush Shirt = Ink(236, 240, 246);
        private static readonly Brush Paper = Ink(214, 214, 204);
        private static readonly Brush Yellow = Ink(246, 196, 54);
        private static readonly Brush YellowDark = Ink(196, 142, 32);
        private static readonly Brush Steel = Ink(132, 146, 158);
        private static readonly Brush Tyre = Ink(36, 36, 42);
        private static readonly Brush Beacon = Ink(255, 136, 40);
        private static readonly Brush HoseReel = Ink(36, 120, 196);
        private static readonly Brush HoseHub = Ink(26, 92, 160);
        internal static readonly Pen Hose = Limb(Color.FromArgb(36, 120, 196), 2.6f);
        private static readonly Brush DuckBody = Ink(255, 214, 64);
        private static readonly Brush Beak = Ink(244, 128, 40);
        private static readonly Brush PadGreen = Ink(76, 156, 88);
        private static readonly Brush PadLight = Ink(118, 190, 112);
        private static readonly Brush Petal = Ink(250, 170, 204);
        private static readonly Brush PetalLight = Ink(255, 222, 236);
        private static readonly Brush FrogGreen = Ink(96, 176, 74);
        private static readonly Brush FrogBelly = Ink(198, 226, 132);
        private static readonly Brush Eye = Ink(250, 250, 240);
        private static readonly Pen Leg = Limb(Color.FromArgb(226, 168, 128), 4.4f);
        private static readonly Pen LegFar = Limb(Color.FromArgb(198, 140, 104), 4.2f);
        private static readonly Pen Arm = Limb(Color.FromArgb(226, 168, 128), 3.4f);
        private static readonly Pen ArmFar = Limb(Color.FromArgb(198, 140, 104), 3.2f);
        private static readonly Pen Trouser = Limb(Color.FromArgb(58, 64, 86), 5.2f);
        private static readonly Pen TrouserFar = Limb(Color.FromArgb(40, 44, 62), 5f);
        private static readonly Pen Sleeve = Limb(Color.FromArgb(220, 226, 236), 3.8f);
        private static readonly Pen Pole = Limb(Color.FromArgb(120, 92, 70), 1.6f);
        private static readonly Pen Folded = Limb(Color.FromArgb(42, 176, 182), 3.2f);
        private static readonly Pen Ink1 = Limb(Color.FromArgb(120, 40, 34, 44), 0.8f);
        private static readonly Pen Print = Limb(Color.FromArgb(150, 110, 110, 104), 0.9f);
        private static readonly Pen Spoke = Limb(Color.FromArgb(150, 150, 160), 1f);
        private static readonly Pen Ponytail = Limb(Color.FromArgb(92, 52, 36), 3.6f);

        internal static void Bather(Graphics g, Pose pose, double phase)
        {
            if (pose == Pose.Sit)
            {
                Floating(g, phase);
                return;
            }
            bool run = pose == Pose.Run;
            if (run) g.RotateTransform(8); // Lean into the sprint around the feet.
            float stride = run ? 13 : 8;
            float lift = run ? 6 : 3;
            Stride(g, LegFar, -30, phase + 1, stride, lift);
            Swing(g, ArmFar, -48, phase, run);
            RingAround(g, -31, 13, 4.5f, true);
            Torso(g);
            RingAround(g, -31, 13, 4.5f, false);
            Stride(g, Leg, -30, phase, stride, lift);
            if (run) g.DrawLine(Folded, -14, -22, 16, -40);
            else g.DrawLine(Folded, -12, -40, 13, -66);
            Swing(g, Arm, -48, phase + 1, run);
            Head(g, 1, -58, (float)Math.Sin(phase * Math.PI) * (run ? 5 : 2));
        }

        private static void Floating(Graphics g, double phase)
        {
            float bob = (float)Math.Sin(phase * 1.7) * 1.2f;
            g.TranslateTransform(0, bob);
            RingAround(g, 0, 18, 5.5f, true);
            g.DrawLine(Pole, -9, 6, -6, -46);
            using (var torso = new GraphicsPath())
            {
                torso.AddBezier(-5, 0, -7, -8, -6, -16, -3, -22);
                torso.AddLine(-3, -22, 4, -22);
                torso.AddBezier(4, -22, 5, -14, 5, -7, 5, 0);
                torso.CloseFigure();
                g.FillPath(SkinBrush, torso);
            }
            g.FillEllipse(Coral, -5.5f, -18, 11, 4.4f);
            g.FillEllipse(Coral, -5, -3, 10.5f, 4.5f);
            RingAround(g, 0, 18, 5.5f, false);
            float kick = (float)Math.Sin(phase * 4.2) * 2.5f;
            g.DrawLines(Leg, new[] { new PointF(2, -1), new PointF(12, -6), new PointF(19, -1 + kick) });
            g.DrawLines(LegFar, new[] { new PointF(0, -1), new PointF(10, -4), new PointF(17, 1 - kick) });
            g.DrawLines(Arm, new[] { new PointF(-2, -19), new PointF(-8, -24), new PointF(-7, -30) });
            g.DrawLines(Arm, new[] { new PointF(3, -19), new PointF(9, -13), new PointF(13, -9) });
            Head(g, 0, -28, 0);
            Umbrella(g, -6, -46, 25);
            g.TranslateTransform(0, -bob);
        }

        private static void Torso(Graphics g)
        {
            using (var torso = new GraphicsPath())
            {
                torso.AddBezier(-4, -29, -6, -38, -5, -46, -3, -52);
                torso.AddLine(-3, -52, 4, -52);
                torso.AddBezier(4, -52, 6, -44, 5, -36, 5, -29);
                torso.CloseFigure();
                g.FillPath(SkinBrush, torso);
            }
            g.FillEllipse(Coral, -4.5f, -48, 10, 4.4f);
            g.FillEllipse(Coral, -4.8f, -34, 10.6f, 5);
            g.FillEllipse(CoralDark, -1, -47, 2, 2);
        }

        private static void Head(Graphics g, float x, float y, float sway)
        {
            g.FillRectangle(SkinShade, x - 1.6f, y + 4, 3.2f, 4);
            g.DrawBezier(Ponytail, x - 5, y - 4, x - 10, y - 3, x - 11 - sway, y + 2, x - 13 - sway, y + 8);
            g.FillEllipse(SkinBrush, x - 6, y - 6.5f, 12, 13);
            using (var hair = new GraphicsPath())
            {
                hair.AddArc(x - 6.6f, y - 7.4f, 13.2f, 11.4f, 180, 180);
                hair.AddBezier(x + 6.6f, y - 1.7f, x + 3, y - 4.4f, x - 2, y - 3, x - 6.6f, y + 2);
                hair.CloseFigure();
                g.FillPath(Hair, hair);
            }
            g.FillRectangle(Shades, x + 0.5f, y - 1.6f, 6, 2.4f);
            g.FillEllipse(Shades, x - 1, y - 1.8f, 3, 2.8f);
            g.DrawArc(Ink1, x + 1.5f, y + 1.4f, 3.6f, 2.4f, 10, 150);
        }

        private static void Stride(Graphics g, Pen pen, float hip, double phase, float stride, float lift)
        {
            float swing = (float)Math.Cos(phase * Math.PI);
            float raise = Math.Max(0, (float)Math.Sin(phase * Math.PI)) * lift;
            float footX = swing * stride;
            float kneeX = footX * 0.45f + 2.5f;
            g.DrawLines(pen, new[] { new PointF(0, hip), new PointF(kneeX, hip * 0.48f - raise), new PointF(footX, -raise) });
        }

        private static void Swing(Graphics g, Pen pen, float shoulder, double phase, bool run)
        {
            float swing = (float)Math.Cos(phase * Math.PI) * (run ? 9 : 6);
            g.DrawLines(pen, new[]
            {
                new PointF(0.5f, shoulder), new PointF(swing * 0.6f, shoulder + 9), new PointF(swing + (run ? 4 : 0), shoulder + (run ? 13 : 18))
            });
        }

        private static void RingAround(Graphics g, float y, float rx, float ry, bool back)
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
                g.FillPath(RingPink, shape);
                GraphicsState state = g.Save();
                g.SetClip(shape, CombineMode.Intersect);
                for (int stripe = -3; stripe <= 3; stripe += 2)
                    g.FillRectangle(RingWhite, stripe * rx / 4f - rx / 9f, y - ry * 2, rx / 4.5f, ry * 4);
                g.Restore(state);
                g.DrawPath(Ink1, shape);
            }
        }

        private static void Umbrella(Graphics g, float x, float y, float radius)
        {
            using (var canopy = new GraphicsPath())
            {
                canopy.AddArc(x - radius, y - radius * 0.62f, radius * 2, radius * 1.24f, 180, 180);
                for (int bump = 0; bump < 5; bump++)
                {
                    float left = x + radius - bump * radius * 0.4f;
                    canopy.AddArc(left - radius * 0.4f, y - radius * 0.1f, radius * 0.4f, radius * 0.2f, 0, 180);
                }
                canopy.CloseFigure();
                g.FillPath(Sand, canopy);
                GraphicsState state = g.Save();
                g.SetClip(canopy, CombineMode.Intersect);
                for (int band = 0; band < 5; band += 2)
                {
                    float left = x - radius + band * radius * 0.4f;
                    g.FillPolygon(band == 2 ? Coral : Teal, new[]
                    {
                        new PointF(x, y - radius * 0.66f), new PointF(left, y + 2), new PointF(left + radius * 0.4f, y + 2)
                    });
                }
                g.Restore(state);
                g.DrawPath(Ink1, canopy);
            }
            g.FillEllipse(Coral, x - 1.4f, y - radius * 0.66f - 2.6f, 2.8f, 2.8f);
        }

        internal static void Runner(Graphics g, double phase)
        {
            Stride(g, TrouserFar, -30, phase + 1, 15, 8);
            g.DrawLines(Sleeve, new[] { new PointF(-1, -49), new PointF(-6, -60), new PointF(-4, -70) });
            using (var torso = new GraphicsPath())
            {
                torso.AddPolygon(new[] { new PointF(-4, -30), new PointF(-3, -52), new PointF(6, -53), new PointF(7, -30) });
                g.FillPath(Shirt, torso);
                g.DrawPath(Ink1, torso);
            }
            g.FillPolygon(Coral, new[] { new PointF(2.5f, -51), new PointF(4, -51), new PointF(3.8f, -40), new PointF(2.2f, -42) });
            Stride(g, Trouser, -30, phase, 15, 8);
            g.DrawLines(Sleeve, new[] { new PointF(4, -50), new PointF(10, -61), new PointF(7, -71) });
            g.FillEllipse(SkinBrush, -2.5f, -64, 11, 12);
            g.FillPie(Hair, -3, -65, 12, 9, 180, 180);
            g.FillEllipse(Shades, 5, -59, 1.6f, 1.8f);
            g.DrawArc(Ink1, 3.5f, -56, 3.6f, 2, 20, 140);
            g.FillRectangle(Paper, -15, -75, 30, 4.5f);
            g.DrawLine(Print, -12, -73.5f, -2, -73.5f);
            g.DrawLine(Print, 2, -73.5f, 12, -73.5f);
            g.FillEllipse(SkinBrush, -5.5f, -73, 3.5f, 3.5f);
            g.FillEllipse(SkinBrush, 5.5f, -73, 3.5f, 3.5f);
        }

        internal static void Pump(Graphics g, double phase, bool working)
        {
            float shake = working ? (float)Math.Sin(phase * 40) * 0.5f : 0;
            g.TranslateTransform(0, shake);
            g.FillRectangle(Steel, -20, -40, 22, 13);
            g.FillEllipse(Steel, -21, -42, 3, 15);
            g.DrawLine(Ink1, -20, -36, 2, -36);
            using (GraphicsPath body = DeskStyle.Rounded(new Rectangle(-28, -28, 52, 20), 4))
            {
                g.FillPath(Yellow, body);
                g.DrawPath(Ink1, body);
            }
            g.FillRectangle(YellowDark, -28, -14, 52, 3);
            g.FillEllipse(HoseReel, 10, -26, 12, 12);
            g.FillEllipse(HoseHub, 13, -23, 6, 6);
            bool flash = Math.Sin(phase * 7) > 0;
            g.FillEllipse(flash ? Beacon : YellowDark, -11, -47, 6, 6);
            g.TranslateTransform(0, -shake);
            Wheel(g, -16, -6, phase);
            Wheel(g, 13, -6, phase);
        }

        private static void Wheel(Graphics g, float x, float y, double phase)
        {
            g.FillEllipse(Tyre, x - 6, y - 6, 12, 12);
            g.FillEllipse(Steel, x - 2.5f, y - 2.5f, 5, 5);
            float angle = (float)(phase * 3);
            g.DrawLine(Spoke, x + (float)Math.Cos(angle) * 4.5f, y + (float)Math.Sin(angle) * 4.5f,
                x - (float)Math.Cos(angle) * 4.5f, y - (float)Math.Sin(angle) * 4.5f);
        }

        internal static void Duck(Graphics g, double phase)
        {
            g.RotateTransform((float)Math.Sin(phase * 2.3) * 6);
            using (var body = new GraphicsPath())
            {
                body.AddBezier(-9, -1, -11, -8, -4, -9, 2, -7);
                body.AddBezier(2, -7, 7, -6, 10, -4, 9, 0);
                body.CloseFigure();
                g.FillPath(DuckBody, body);
                g.DrawPath(Ink1, body);
            }
            g.FillEllipse(DuckBody, -1, -15, 9, 9);
            g.FillPolygon(Beak, new[] { new PointF(7.5f, -11.5f), new PointF(12, -10.5f), new PointF(7.5f, -9) });
            g.FillEllipse(Shades, 4, -13, 1.8f, 1.8f);
            g.FillEllipse(RingWhite, -6, -6, 5, 2);
        }

        internal static void LilyPad(Graphics g, float radius, bool flower)
        {
            g.FillPie(PadGreen, -radius, -radius * 0.32f, radius * 2, radius * 0.64f, 20, 320);
            g.FillPie(PadLight, -radius * 0.7f, -radius * 0.26f, radius * 1.4f, radius * 0.4f, 30, 300);
            if (!flower) return;
            for (int petal = -2; petal <= 2; petal++)
            {
                GraphicsState state = g.Save();
                g.TranslateTransform(0, -1);
                g.RotateTransform(petal * 24);
                g.FillEllipse(petal == 0 ? PetalLight : Petal, -2.1f, -9, 4.2f, 9.5f);
                g.Restore(state);
            }
            g.FillEllipse(Yellow, -1.8f, -3.8f, 3.6f, 2.6f);
        }

        internal static void Frog(Graphics g, double crouch)
        {
            float squash = (float)(1 - crouch * 0.25);
            g.ScaleTransform(1, squash);
            g.FillEllipse(FrogGreen, -7, -8, 14, 8);
            g.FillEllipse(FrogBelly, -3, -5, 8, 4);
            g.FillEllipse(FrogGreen, -5, -11, 4.5f, 4.5f);
            g.FillEllipse(FrogGreen, 1, -11, 4.5f, 4.5f);
            g.FillEllipse(Eye, -4.2f, -10.4f, 2.8f, 2.8f);
            g.FillEllipse(Eye, 1.8f, -10.4f, 2.8f, 2.8f);
            g.FillEllipse(Shades, -3.4f, -9.8f, 1.4f, 1.6f);
            g.FillEllipse(Shades, 2.6f, -9.8f, 1.4f, 1.6f);
            g.FillEllipse(FrogGreen, -9, -3, 6, 3);
            g.FillEllipse(FrogGreen, 3, -3, 6, 3);
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

        private static Brush Ink(int red, int green, int blue)
        {
            return new SolidBrush(Color.FromArgb(red, green, blue));
        }

        private static Pen Limb(Color color, float width)
        {
            return new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        }
    }
}
