using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace SnowDesk
{
    internal static class DecorationArt
    {
        internal const int TreeWidth = 48, TreeHeight = 66;
        internal const int SantaWidth = 34, SleighWidth = 128, PlowBlade = 37, GondolaWidth = 40;
        internal const int PlowRear = 36;
        internal const double SantaScale = 0.87;
        internal const double SantaStepLength = 14 * SantaScale;
        private static readonly Brush Dark = Ink(40, 38, 43);
        private static readonly Brush WarmBlack = Ink(57, 48, 45);
        private static readonly Brush Ice = Ink(224, 237, 244);
        private static readonly Brush Chalk = Ink(250, 246, 232);
        private static readonly Brush FurShadow = Ink(174, 181, 177);
        private static readonly Brush Red = Ink(151, 42, 49);
        private static readonly Brush RedDark = Ink(104, 33, 39);
        private static readonly Brush RedLight = Ink(189, 59, 62);
        private static readonly Brush PineLight = Ink(64, 111, 87);
        private static readonly Brush Wood = Ink(117, 77, 52);
        private static readonly Brush Hide = Ink(143, 107, 76);
        private static readonly Brush HideLight = Ink(192, 151, 108);
        private static readonly Brush Honey = Ink(239, 183, 83);
        private static readonly Brush Glass = Ink(90, 132, 151);
        private static readonly Brush GlassLight = Ink(166, 201, 210);
        private static readonly Brush FirShading = Ramp(-66, -7, Color.FromArgb(79, 123, 98), Color.FromArgb(34, 66, 55));
        private static readonly Brush Wool = Ramp(-44, -12, Color.FromArgb(178, 59, 63), Color.FromArgb(111, 38, 43));
        private static readonly Brush Lacquer = Ramp(-31, -7, Color.FromArgb(161, 57, 52), Color.FromArgb(94, 37, 43));
        private static readonly Brush Enamel = Ramp(-33, -10, Color.FromArgb(229, 167, 85), Color.FromArgb(170, 92, 34));
        private static readonly Brush WindowGlass = Ramp(-29, -12, Color.FromArgb(155, 191, 200), Color.FromArgb(62, 105, 131));
        private static readonly Brush Skin = Ink(217, 157, 120);
        private static readonly Pen Crease = Stroke(Color.FromArgb(100, 36, 35, 40), 0.8f);
        private static readonly Pen GoldLine = Stroke(Color.FromArgb(207, 151, 82), 1.4f);
        private static readonly Pen CableLine = Stroke(Color.FromArgb(137, 104, 134, 150), 1.15f);
        private static readonly Pen SnowLine = Stroke(Color.FromArgb(221, 234, 239), 1.35f);
        private static readonly Pen Branch = Stroke(Color.FromArgb(82, 122, 99), 0.85f);
        private static readonly Pen BootBack = Stroke(Color.FromArgb(49, 43, 44), 4);
        private static readonly Pen TrouserBack = Stroke(Color.FromArgb(117, 37, 42), 6.3f);
        private static readonly Pen TrouserFront = Stroke(Color.FromArgb(162, 48, 50), 7);
        private static readonly Pen Hoof = Stroke(Color.FromArgb(64, 48, 40), 2.1f);
        private static readonly Pen DeerFar = Stroke(Color.FromArgb(114, 81, 60), 3);
        private static readonly Pen DeerNear = Stroke(Color.FromArgb(182, 135, 94), 3.4f);
        private static readonly Pen Antler = Stroke(Color.FromArgb(118, 85, 63), 1.4f);
        private static readonly Pen Arm = Stroke(Color.FromArgb(171, 49, 51), 6);
        private static readonly Pen BootTrim = Stroke(Color.FromArgb(187, 190, 179), 2.45f);
        private static readonly Pen Needles = Stroke(Color.FromArgb(165, 126, 153, 125), 0.7f);
        private static readonly Pen TrimGlint = Stroke(Color.FromArgb(160, 245, 204, 144), 0.55f);
        private static readonly Pen ClothSeam = Stroke(Color.FromArgb(163, 230, 132, 123), 0.7f);
        private static readonly GraphicsPath TreeShape = RoundedPolygon(TreeSilhouette, 0.12f);
        private static readonly GraphicsPath TreeNeedles = BuildNeedles();
        private static readonly GraphicsPath Beard = BeardContour();
        private static readonly GraphicsPath Cap = CapContour();
        private static readonly GraphicsPath TruckCab = RoundedPolygon(new[] {
            new PointF(-3,-11), new PointF(-3,-30), new PointF(1,-32), new PointF(13,-31),
            new PointF(18,-21), new PointF(17,-11)
        }, 0.20f);
        private static readonly GraphicsPath Cabin = CabinContour();
        private static readonly GraphicsPath CabinWindow = RoundedPolygon(new[] {
            new PointF(-12,-28), new PointF(12,-28), new PointF(15,-13), new PointF(-15,-13)
        }, 0.22f);
        private static readonly GraphicsPath Coat = RoundedPolygon(new[] {
            new PointF(-10,-43), new PointF(2,-44), new PointF(11,-35), new PointF(10,-22),
            new PointF(13,-15), new PointF(5,-12), new PointF(-12,-14), new PointF(-9,-25), new PointF(-12,-35)
        });
        private static readonly GraphicsPath SleighBody = RoundedPolygon(new[] {
            new PointF(-58,-18), new PointF(-58,-31), new PointF(-49,-28), new PointF(-44,-20),
            new PointF(-27,-18), new PointF(-19,-25), new PointF(-13,-24), new PointF(-17,-13),
            new PointF(-23,-8), new PointF(-51,-8)
        });
        private static readonly GraphicsPath Blade = RoundedPolygon(new[] {
            new PointF(27,-13), new PointF(38,-18), new PointF(36,-7), new PointF(38,-1),
            new PointF(29,-1), new PointF(25,-5)
        });

        internal static PointF[] TreeSilhouette
        {
            get
            {
                return new[] {
                    new PointF(0,-66), new PointF(12,-49), new PointF(8,-48),
                    new PointF(17,-37), new PointF(12,-37), new PointF(22,-25),
                    new PointF(17,-24), new PointF(24,-12), new PointF(18,-9),
                    new PointF(4,-9), new PointF(4,0), new PointF(-4,0),
                    new PointF(-4,-9), new PointF(-21,-9), new PointF(-24,-13),
                    new PointF(-16,-24), new PointF(-21,-25), new PointF(-11,-37),
                    new PointF(-16,-37), new PointF(-7,-49), new PointF(-10,-49)
                };
            }
        }

        internal static void Draw(Graphics g, Effect kind, double phase)
        {
            switch (kind)
            {
                case Effect.Trees: Tree(g); break;
                case Effect.Santa: Santa(g, phase, false); break;
                case Effect.Sleigh: Sleigh(g, phase); break;
                case Effect.Plow: Plow(g, phase); break;
                case Effect.Gondola: Gondola(g); break;
            }
        }

        private static void Tree(Graphics g)
        {
            g.FillPath(FirShading, TreeShape);
            g.DrawPath(Crease, TreeShape);
            g.DrawPath(Needles, TreeNeedles);
            g.FillRectangle(Wood, -3.1f, -17, 6.2f, 17);
            for (int tier = 0; tier < 6; tier++)
            {
                float y = -56 + tier * 8.4f;
                float spread = 5 + tier * 3.25f;
                g.DrawBezier(Branch, -spread, y + 7, -spread * 0.48f, y + 7, -4, y + 3, 1, y - 1);
                g.DrawBezier(Branch, -1, y, 6, y + 5, spread * 0.68f, y + 7, spread, y + 8);
                g.DrawBezier(SnowLine, -spread * 0.83f, y + 7, -8, y + 7, -4, y + 2, 0, y - 1);
                g.DrawBezier(SnowLine, 1, y, 5, y + 3, spread * 0.59f, y + 6, spread * 0.87f, y + 7);
                Oval(g, tier % 2 == 0 ? Honey : RedLight, (tier % 2 == 0 ? -1 : 1) * spread * 0.6f - 1.35f,
                    y + 7.7f, 2.7f, 2.7f);
                Oval(g, PineLight, -1.5f, y + 2, 3, 3.2f);
            }
            Oval(g, Honey, -1.8f, -66, 3.6f, 3.6f);
        }

        private static void Santa(Graphics g, double phase, bool seated)
        {
            float stride = (float)Math.Cos(phase * Math.PI);
            if (!seated)
            {
                Leg(g, -4, WalkFoot(phase + 1), TrouserBack);
                Leg(g, 4, WalkFoot(phase), TrouserFront);
            }

            g.FillEllipse(Wood, -20, -43, 16, 22);
            g.FillEllipse(Hide, -20, -44, 13, 17);
            g.DrawEllipse(Crease, -20, -44, 16, 22);
            g.FillRectangle(RedDark, -14, -37, 9, 17);
            g.FillPath(Wool, Coat);
            g.DrawPath(Crease, Coat);
            g.DrawBezier(ClothSeam, -6, -40, -7, -31, -5, -24, -7, -20);
            g.DrawBezier(ClothSeam, 3, -42, 2, -38, 6, -32, 7, -30);
            g.DrawBezier(Crease, -17, -41, -14, -35, -17, -28, -19, -26);
            Oval(g, FurShadow, -12, -19, 24, 5);
            Oval(g, Chalk, -11, -20, 23, 4);
            g.FillRectangle(Dark, -11, -23, 22, 4);
            g.FillRectangle(Honey, 1, -24, 6, 5);
            g.FillRectangle(Dark, 2.3f, -23, 3.4f, 2.8f);
            float cuff = seated ? 16 : 7 - stride * 7.2f;
            float wristY = seated ? -25 : -25 - Math.Abs(stride) * 2;
            g.DrawLine(Arm, 4, -35, cuff, wristY);
            Oval(g, Chalk, cuff - 2.7f, wristY - 2, 5.2f, 4.8f);
            Oval(g, Dark, cuff + 0.5f, wristY - 1.7f, 4, 3.8f);

            Oval(g, Skin, -5, -54, 15, 14);
            g.FillPath(FurShadow, Beard);
            GraphicsState beard = g.Save();
            g.TranslateTransform(-0.35f, -0.8f);
            g.FillPath(Chalk, Beard);
            g.Restore(beard);
            g.DrawBezier(ClothSeam, 3.4f, -45.7f, 4.1f, -44.8f, 5.4f, -44.8f, 6.1f, -45.7f);
            Oval(g, Skin, 6.5f, -49.8f, 3.5f, 3);
            Oval(g, Dark, 5.4f, -51.7f, 1.2f, 1.3f);
            g.FillPath(RedDark, Cap);
            g.DrawBezier(ClothSeam, -6, -57, -4, -62, 4, -61.5f, 7.7f, -56.5f);
            Oval(g, Chalk, -6, -56, 17, 4.2f);
            Oval(g, Chalk, -10.2f, -59.4f, 4.7f, 4.7f);
        }

        private static PointF WalkFoot(double phase)
        {
            double cycle = phase % 2;
            if (cycle < 1) return new PointF((float)(7 - 14 * cycle), 0);

            double swing = cycle - 1;
            double travel = swing * swing * (3 - 2 * swing);
            return new PointF((float)(-7 + 14 * travel), (float)(-5 * Math.Sin(Math.PI * swing)));
        }

        internal static double SantaPlantOffset
        {
            get { return (WalkFoot(0).X + 2) * SantaScale; }
        }

        private static void Leg(Graphics g, float hip, PointF foot, Pen fabric)
        {
            float footX = foot.X;
            float footY = foot.Y;
            g.DrawLine(fabric, hip, -15.5f, (hip + footX) * 0.5f + 1, -9);
            g.DrawLine(fabric, (hip + footX) * 0.5f + 1, -9, footX, footY - 3);
            g.DrawLine(BootTrim, footX - 1.6f, footY - 4.2f, footX + 1.6f, footY - 4.2f);
            g.DrawLine(BootBack, footX, footY - 2.1f, footX + 4, footY - 2.1f);
        }

        private static void Sleigh(Graphics g, double phase)
        {
            g.DrawBezier(GoldLine, -61, -3, -46, 1, -17, 1, -11, -6);
            g.DrawBezier(GoldLine, -11, -6, -7, -12, -12, -13, -15, -10);
            g.DrawLine(GoldLine, -48, -5, -48, -11);
            g.DrawLine(GoldLine, -24, -5, -24, -10);
            g.FillRectangle(RedDark, -58, -36, 10, 11);
            g.FillRectangle(Honey, -55, -36, 2, 11);
            GraphicsState seated = g.Save();
            g.TranslateTransform(-38,-6);
            g.ScaleTransform(0.51f,0.51f);
            Santa(g,phase,true);
            g.Restore(seated);

            g.FillPath(Lacquer, SleighBody);
            g.DrawPath(GoldLine, SleighBody);
            g.DrawBezier(TrimGlint, -53, -14, -45, -11, -32, -13, -25, -13);
            g.DrawLine(TrimGlint, -50, -8, -33, -8);
            g.DrawBezier(GoldLine, -51, -18, -45, -18, -48, -11, -37, -12);
            g.DrawLine(GoldLine, -18, -22, 21, -21);

            float step = (float)Math.Sin(phase);
            DeerLeg(g, 24, -step, DeerFar);
            DeerLeg(g, 43, step, DeerFar);
            Oval(g, Hide, 17, -29, 36, 18);
            Oval(g, HideLight, 22, -27, 28, 11);
            Oval(g, Hide, 42, -39, 12, 22);
            Oval(g, HideLight, 46, -41, 18, 10);
            Oval(g, Dark, 61, -38, 3.8f, 3.4f);
            Oval(g, Dark, 54, -38.5f, 1.7f, 1.7f);
            g.DrawLine(Antler, 49, -40, 45, -51);
            g.DrawLine(Antler, 45, -51, 46, -55);
            g.DrawLine(Antler, 47, -46, 51, -52);
            g.DrawLine(Antler, 53, -40, 54, -51);
            g.DrawLine(Antler, 54, -48, 57, -53);
            DeerLeg(g, 22, step, DeerNear);
            DeerLeg(g, 42, -step, DeerNear);
            g.DrawLine(GoldLine, 46, -28, 53, -24);
        }

        private static void DeerLeg(Graphics g, float x, float stride, Pen coat)
        {
            float toe = x + stride * 9;
            float y = -Math.Max(0, stride) * 6;
            g.DrawLine(coat, x, -16, (x + toe) * 0.5f - 1, -7);
            g.DrawLine(coat, (x + toe) * 0.5f - 1, -7, toe, y - 1.2f);
            g.DrawLine(Hoof, toe, y - 1.2f, toe + 2, y - 1.2f);
        }

        private static void Plow(Graphics g, double phase)
        {
            g.FillRectangle(WarmBlack, -34, -15, 61, 10);
            g.FillRectangle(Enamel, -34, -26, 30, 14);
            g.DrawLine(TrimGlint, -32, -21, -7, -21);
            g.FillRectangle(Honey, -33, -27, 30, 3);
            g.DrawLine(SnowLine, -33, -28, -4, -28);
            g.DrawRectangle(Crease, -34, -26, 30, 14);
            g.FillPath(Enamel, TruckCab);
            g.FillRectangle(Glass, 3, -29, 12, 10);
            g.FillRectangle(GlassLight, 4, -28, 2, 8);
            g.DrawPath(Crease, TruckCab);
            g.FillRectangle(Honey, 16, -20, 11, 9);
            g.FillRectangle(Dark, 23, -18, 4, 5);
            g.DrawLine(TrimGlint, 23, -17, 26, -17);
            g.DrawLine(TrimGlint, 23, -15, 26, -15);
            g.FillRectangle(Honey, 1, -34, 8, 3);
            g.FillPath(FurShadow, Blade);
            g.DrawPath(Crease, Blade);
            g.DrawLine(SnowLine, 29, -2, PlowBlade, -2);
            Wheel(g, -23, phase);
            Wheel(g, 14, phase);
        }

        private static void Wheel(Graphics g, float x, double phase)
        {
            Oval(g, Dark, x - 6.5f, -13, 13, 13);
            Oval(g, FurShadow, x - 4, -10.5f, 8, 8);
            for (int i = 0; i < 3; i++)
            {
                double angle = phase + i * Math.PI / 3;
                float dx = (float)Math.Cos(angle) * 3.3f, dy = (float)Math.Sin(angle) * 3.3f;
                g.DrawLine(Crease, x - dx, -6.5f - dy, x + dx, -6.5f + dy);
            }
            Oval(g, WarmBlack, x - 1.6f, -8.1f, 3.2f, 3.2f);
        }

        private static void Gondola(Graphics g)
        {
            g.DrawLine(CableLine, 0, -43, -2, -32);
            Oval(g, FurShadow, -5, -44, 10, 3.1f);
            g.FillPath(Lacquer, Cabin);
            g.DrawPath(Crease, Cabin);
            g.FillPath(WindowGlass, CabinWindow);
            g.FillRectangle(RedDark, -2, -28, 3.2f, 23);
            g.DrawBezier(SnowLine, -12, -31.5f, -6, -33, 6, -33, 12, -31.5f);
            g.DrawLine(GoldLine, -11, -6, 11, -6);
            g.DrawLine(Crease, 8, -28, 10, -12);
            g.FillRectangle(GlassLight, -12, -23, 2, 9);
            g.DrawLine(TrimGlint, -14, -26, -10, -26);
            g.DrawLine(TrimGlint, 5, -26, 9, -26);
        }

        private static void Oval(Graphics g, Brush fill, float x, float y, float w, float h)
        {
            g.FillEllipse(fill, x, y, w, h);
        }

        private static SolidBrush Ink(int red, int green, int blue) { return new SolidBrush(Color.FromArgb(red, green, blue)); }

        private static Pen Stroke(Color color, float width)
        {
            return new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        }

        private static Brush Ramp(float top, float bottom, Color light, Color dark)
        {
            return new LinearGradientBrush(new RectangleF(-68, top, 136, bottom - top),
                light, dark, LinearGradientMode.Vertical) { WrapMode = WrapMode.TileFlipY };
        }

        private static GraphicsPath BuildNeedles()
        {
            var path = new GraphicsPath();
            for (int tier = 0; tier < 6; tier++)
            {
                float y = -56 + tier * 8.1f;
                float spread = 4 + tier * 3.05f;
                for (int tip = 1; tip <= 5; tip++)
                {
                    float x = spread * tip / 6f;
                    float branchY = y + 1 + tip * 1.05f;
                    path.StartFigure();
                    path.AddLine(-x, branchY, -x - 1.9f, branchY + 2.5f);
                    path.StartFigure();
                    path.AddLine(x, branchY + 0.55f, x + 1.9f, branchY + 2.8f);
                }
            }
            return path;
        }

        private static GraphicsPath BeardContour()
        {
            var path = new GraphicsPath();
            path.StartFigure();
            path.AddBezier(-5.4f, -48, -4.8f, -50, -1, -47.6f, 1.4f, -46);
            path.AddBezier(1.4f, -46, 4.5f, -47.6f, 9.5f, -48.7f, 10.4f, -46.2f);
            path.AddBezier(10.4f, -46.2f, 10, -41.7f, 7.6f, -36.7f, 3.1f, -36.3f);
            path.AddBezier(3.1f, -36.3f, -2.7f, -36.8f, -5.4f, -42.3f, -5.4f, -48);
            path.CloseFigure();
            return path;
        }

        private static GraphicsPath CapContour()
        {
            var path = new GraphicsPath();
            path.AddBezier(-6, -54, -6.8f, -60.8f, 2.5f, -66, 8.5f, -59);
            path.AddBezier(8.5f, -59, 9.3f, -57.8f, 9.8f, -56, 10, -54);
            path.AddBezier(10, -54, 3, -56, -3, -54.5f, -6, -54);
            path.CloseFigure();
            return path;
        }

        private static GraphicsPath CabinContour()
        {
            var path = new GraphicsPath();
            path.AddBezier(-12, -32, -8, -34, 8, -34, 12, -32);
            path.AddBezier(12, -32, 15.8f, -28, 18.2f, -20, 18, -10);
            path.AddBezier(18, -10, 17.9f, -2, 9.4f, -0.6f, 0, -0.6f);
            path.AddBezier(0, -0.6f, -9.4f, -0.6f, -17.9f, -2, -18, -10);
            path.AddBezier(-18, -10, -18.2f, -20, -15.8f, -28, -12, -32);
            path.CloseFigure();
            return path;
        }

        private static GraphicsPath RoundedPolygon(PointF[] points, float rounding = 0.19f)
        {
            var path = new GraphicsPath();
            PointF last = new PointF();
            for (int i = 0; i < points.Length; i++)
            {
                PointF point = points[i];
                PointF before = points[(i + points.Length - 1) % points.Length];
                PointF after = points[(i + 1) % points.Length];
                var entry = new PointF(point.X + (before.X - point.X) * rounding,
                    point.Y + (before.Y - point.Y) * rounding);
                var exit = new PointF(point.X + (after.X - point.X) * rounding,
                    point.Y + (after.Y - point.Y) * rounding);
                if (i > 0) path.AddLine(last, entry);
                path.AddBezier(entry, point, point, exit);
                last = exit;
            }
            path.CloseFigure();
            return path;
        }

    }
}
