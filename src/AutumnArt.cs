using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace SnowDesk
{
    /// <summary>Autumn characters in the winter decorations' style. Each stands on (0, 0) and faces right.</summary>
    internal static class AutumnArt
    {
        private static readonly Brush Skin = DecorationArt.Ramp(-70, -40, Color.FromArgb(240, 192, 156), Color.FromArgb(204, 146, 110));
        private static readonly Brush Flannel = DecorationArt.Ramp(-56, -28, Color.FromArgb(206, 62, 54), Color.FromArgb(138, 34, 38));
        private static readonly Brush FlannelFar = Ink(132, 34, 38);
        private static readonly Brush Denim = DecorationArt.Ramp(-30, 0, Color.FromArgb(76, 104, 156), Color.FromArgb(44, 62, 104));
        private static readonly Brush DenimFar = Ink(40, 54, 90);
        private static readonly Brush Boot = DecorationArt.Ramp(-6, 0, Color.FromArgb(138, 92, 56), Color.FromArgb(88, 56, 34));
        private static readonly Brush Beanie = DecorationArt.Ramp(-74, -62, Color.FromArgb(238, 182, 64), Color.FromArgb(196, 132, 34));
        private static readonly Brush Parka = DecorationArt.Ramp(-44, -18, Color.FromArgb(92, 150, 104), Color.FromArgb(52, 98, 70));
        private static readonly Brush ParkaFar = Ink(48, 90, 64);
        private static readonly Brush Bobble = DecorationArt.Ramp(-56, -44, Color.FromArgb(226, 72, 72), Color.FromArgb(164, 38, 48));
        private static readonly Brush Scarf = Ink(244, 214, 92);
        private static readonly Brush Fur = DecorationArt.Ramp(-20, 0, Color.FromArgb(196, 112, 58), Color.FromArgb(136, 70, 36));
        private static readonly Brush TailFur = DecorationArt.Ramp(-28, 0, Color.FromArgb(214, 132, 70), Color.FromArgb(146, 78, 40));
        private static readonly Brush Belly = Ink(244, 222, 190);
        private static readonly Brush Acorn = DecorationArt.Ramp(-4, 3, Color.FromArgb(196, 140, 72), Color.FromArgb(140, 92, 44));
        private static readonly Brush AcornCap = Ink(110, 76, 44);
        private static readonly Brush Hair = Ink(86, 56, 38);
        private static readonly Brush Eye = Ink(40, 32, 32);
        private static readonly Brush Glint = new SolidBrush(Color.FromArgb(230, 255, 255, 255));
        private static readonly Brush Blush = new SolidBrush(Color.FromArgb(80, 232, 120, 110));
        private static readonly Pen Crease = Line(Color.FromArgb(110, 36, 35, 40), 0.8f);
        private static readonly Pen Plaid = Line(Color.FromArgb(110, 40, 18, 22), 0.9f);
        private static readonly Pen PlaidLight = Line(Color.FromArgb(70, 255, 220, 200), 0.6f);
        private static readonly Pen Handle = Line(Color.FromArgb(150, 108, 66), 1.9f);
        private static readonly Pen Tine = Line(Color.FromArgb(120, 124, 132), 0.9f);
        private static readonly Pen Knit = Line(Color.FromArgb(120, 150, 96, 24), 0.7f);
        private static readonly Pen ScarfStripe = Line(Color.FromArgb(214, 84, 66), 1.4f);
        private static readonly Pen Whisker = Line(Color.FromArgb(90, 60, 40, 30), 0.5f);
        private static readonly GraphicsPath Shirt = DecorationArt.RoundedPolygon(new[] {
            new PointF(-6, -56), new PointF(6, -57), new PointF(9, -45), new PointF(8, -29), new PointF(-7, -28), new PointF(-8, -45)
        }, 0.24f);
        private static readonly GraphicsPath KidCoat = DecorationArt.RoundedPolygon(new[] {
            new PointF(-5.6f, -40), new PointF(5.6f, -40.6f), new PointF(8, -30), new PointF(8.6f, -18), new PointF(-7, -18), new PointF(-7.4f, -30)
        }, 0.26f);

        /// <summary>Gardener with a rake: walks with it on the shoulder, or rakes with strokes driven by phase.</summary>
        internal static void Raker(Graphics g, double phase, bool raking)
        {
            float stride = raking ? 0 : (float)Math.Cos(phase * Math.PI);
            Leg(g, DenimFar, -stride * 8, raking ? -3 : 0);
            Leg(g, Denim, stride * 8, raking ? 4 : 0);
            float stroke = raking ? (float)Math.Sin(phase * 2.4) : 0;
            if (!raking) SummerArt.Taper(g, FlannelFar, 4.6f, 3.6f, new PointF(-2, -52), new PointF(-5 - stride * 4, -42), new PointF(-3, -36));
            g.FillPath(Flannel, Shirt);
            GraphicsState state = g.Save();
            g.SetClip(Shirt, CombineMode.Intersect);
            for (int i = -8; i <= 8; i += 4) g.DrawLine(Plaid, i, -58, i + 1, -27);
            for (int j = -54; j <= -28; j += 5) g.DrawLine((j / 5) % 2 == 0 ? Plaid : PlaidLight, -9, j, 10, j - 0.6f);
            g.Restore(state);
            g.DrawPath(Crease, Shirt);
            g.FillPolygon(FlannelFar, new[] { new PointF(-3, -57), new PointF(1, -52), new PointF(4.6f, -57.4f) });
            if (raking)
            {
                var hand = new PointF(16 + stroke * 6, -36 + stroke);
                g.DrawLine(Handle, 4 + stroke * 3, -50, 32 + stroke * 9, -2);
                RakeHead(g, 32 + stroke * 9, -1, 0);
                SummerArt.Taper(g, FlannelFar, 4.4f, 3.4f, new PointF(-1, -51), new PointF(7, -44), new PointF(9 + stroke * 4, -44 + stroke));
                SummerArt.Taper(g, Flannel, 4.6f, 3.6f, new PointF(3, -51), new PointF(10, -40), hand);
                g.FillEllipse(Skin, hand.X - 1.8f, hand.Y - 1.8f, 3.6f, 3.6f);
            }
            else
            {
                // Walking, the rake is carried upright like a staff, tines up.
                g.DrawLine(Handle, 12, -1, 9, -70);
                RakeHead(g, 9, -70, 180);
                SummerArt.Taper(g, Flannel, 4.6f, 3.6f, new PointF(3, -52), new PointF(8 + stride * 2, -44), new PointF(10.6f, -37));
                g.FillEllipse(Skin, 8.8f, -38.8f, 3.6f, 3.6f);
            }
            Head(g, 1, -63, raking);
        }

        private static void RakeHead(Graphics g, float x, float y, float angle)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform(angle);
            g.DrawLine(Handle, -6, -1.5f, 6, -1.5f);
            for (int tine = -5; tine <= 5; tine += 2) g.DrawLine(Tine, tine * 1.1f, -1, tine * 1.6f, 4.5f);
            g.Restore(state);
        }

        private static void Leg(Graphics g, Brush cloth, float footX, float lean)
        {
            SummerArt.Taper(g, cloth, 6.2f, 4.8f, new PointF(0, -29), new PointF(footX * 0.45f + 1.6f + lean * 0.4f, -15), new PointF(footX + lean, -4));
            using (GraphicsPath boot = DeskStyle.Rounded(new Rectangle((int)Math.Round(footX + lean - 3), -6, 9, 6), 2))
            {
                g.FillPath(Boot, boot);
                g.DrawPath(Crease, boot);
            }
        }

        private static void Head(Graphics g, float x, float y, bool down)
        {
            g.FillRectangle(Skin, x - 2, y + 4.6f, 4, 4);
            g.FillEllipse(Skin, x - 6, y - 6.6f, 13, 13.4f);
            g.DrawEllipse(Crease, x - 6, y - 6.6f, 13, 13.4f);
            g.FillPie(Hair, x - 6.4f, y - 2, 6, 9, 90, 180);
            g.FillEllipse(Eye, x + 3.2f, y + (down ? -0.2f : -1.6f), 1.6f, 1.8f);
            g.FillEllipse(Skin, x + 5.6f, y + 0.4f, 2.6f, 2.4f);
            g.DrawArc(Crease, x + 2.6f, y + 3, 3, 1.6f, 10, 150);
            g.FillEllipse(Blush, x + 2.4f, y + 1.4f, 3, 1.6f);
            using (var hat = new GraphicsPath())
            {
                hat.AddBezier(x - 7, y - 2, x - 7.6f, y - 11, x + 6.6f, y - 12, x + 7.6f, y - 2.6f);
                hat.CloseFigure();
                g.FillPath(Beanie, hat);
                g.DrawPath(Crease, hat);
            }
            g.FillRectangle(Beanie, x - 7.4f, y - 4, 15.2f, 3.4f);
            g.DrawRectangle(Crease, x - 7.4f, y - 4, 15.2f, 3.4f);
            for (int rib = -6; rib <= 6; rib += 2) g.DrawLine(Knit, x + rib, y - 3.6f, x + rib, y - 1);
        }

        /// <summary>A child in a parka and bobble hat. pose 0 run, 1 jump (tucked), 2 sitting in leaves.</summary>
        internal static void Kid(Graphics g, double phase, int pose)
        {
            float swing = pose == 0 ? (float)Math.Cos(phase * Math.PI) : 0;
            if (pose == 1)
            {
                SummerArt.Taper(g, DenimFar, 4.8f, 3.8f, new PointF(0, -18), new PointF(6, -12), new PointF(-1, -6));
                SummerArt.Taper(g, Denim, 5, 4, new PointF(1, -18), new PointF(8, -13), new PointF(2, -7));
            }
            else if (pose == 0)
            {
                SummerArt.Taper(g, DenimFar, 4.8f, 3.8f, new PointF(0, -18), new PointF(-swing * 4 + 1.6f, -9), new PointF(-swing * 8, -2.6f));
                SummerArt.Taper(g, Denim, 5, 4, new PointF(1, -18), new PointF(swing * 4 + 2, -9), new PointF(swing * 8, -2.6f));
                g.FillEllipse(Boot, -swing * 8 - 2.4f, -4, 6.4f, 4);
                g.FillEllipse(Boot, swing * 8 - 2.4f, -4, 6.4f, 4);
            }
            float armLift = pose == 1 ? -14 : 0;
            SummerArt.Taper(g, ParkaFar, 4.2f, 3.4f, new PointF(-1, -36), new PointF(-5 - swing * 4, -29 + armLift * 0.5f), new PointF(-3 - swing * 6, -22 + armLift));
            g.FillPath(Parka, KidCoat);
            g.DrawPath(Crease, KidCoat);
            g.DrawLine(Crease, 1, -39, 1.4f, -19);
            g.FillRectangle(Scarf, -5.6f, -41.4f, 11.6f, 3.6f);
            for (float s = -4; s < 6; s += 3) g.DrawLine(ScarfStripe, s, -41, s, -38.2f);
            float flutter = (float)Math.Sin(phase * 5) * 1.6f;
            g.FillPolygon(Scarf, new[] { new PointF(-5, -40), new PointF(-12, -38 + flutter), new PointF(-11, -35.6f + flutter), new PointF(-4.6f, -38) });
            SummerArt.Taper(g, Parka, 4.4f, 3.6f, new PointF(3, -36), new PointF(8 + swing * 4, -29 + armLift * 0.5f), new PointF(7 + swing * 6, -22 + armLift));
            g.FillEllipse(Skin, 7 + swing * 6 - 1.8f, -23.8f + armLift, 3.6f, 3.6f);
            g.FillRectangle(Skin, -1.6f, -44, 3.6f, 3);
            g.FillEllipse(Skin, -5, -54, 11, 11.4f);
            g.DrawEllipse(Crease, -5, -54, 11, 11.4f);
            g.FillEllipse(Eye, 2.6f, -49.6f, 1.6f, 1.8f);
            g.FillEllipse(Glint, 3.1f, -49.4f, 0.6f, 0.6f);
            g.FillEllipse(Blush, 1.6f, -47, 3, 1.6f);
            g.DrawArc(Crease, 1.8f, -46.2f, 3, pose == 1 ? 2.4f : 1.6f, 10, 160);
            using (var hat = new GraphicsPath())
            {
                hat.AddBezier(-5.8f, -49, -6.4f, -57.6f, 5.6f, -58.4f, 6.4f, -49.6f);
                hat.CloseFigure();
                g.FillPath(Bobble, hat);
                g.DrawPath(Crease, hat);
            }
            g.FillEllipse(Scarf, -2.4f, -61.4f, 5, 5);
            g.DrawEllipse(Crease, -2.4f, -61.4f, 5, 5);
        }

        /// <summary>A squirrel: hopping when <paramref name="sitting"/> is false, otherwise upright nibbling an acorn.</summary>
        internal static void Squirrel(Graphics g, double phase, bool sitting)
        {
            float bound = sitting ? 0 : (float)Math.Abs(Math.Sin(phase * Math.PI));
            g.TranslateTransform(0, -bound * 7);
            float twitch = (float)Math.Sin(phase * (sitting ? 9 : 3)) * 1.6f;
            using (var tail = new GraphicsPath())
            {
                tail.AddBezier(-6, -4, -16, -2, -20 + twitch, -18, -12 + twitch, -26);
                tail.AddBezier(-12 + twitch, -26, -6 + twitch, -30, -2, -24, -6, -20);
                tail.AddBezier(-6, -20, -12, -16, -9, -8, -4, -7);
                tail.CloseFigure();
                g.FillPath(TailFur, tail);
                g.DrawPath(Crease, tail);
                g.DrawBezier(Crease, -10, -8, -15, -12, -14 + twitch, -20, -10 + twitch, -24);
            }
            GraphicsState state = g.Save();
            if (sitting) g.RotateTransform(-28);
            else g.RotateTransform((float)Math.Cos(phase * Math.PI) * 8);
            g.FillEllipse(Fur, -7, -10, 15, 9.4f);
            g.DrawEllipse(Crease, -7, -10, 15, 9.4f);
            g.FillEllipse(Belly, 1, -5.4f, 6, 4);
            g.FillEllipse(Fur, 4, -15, 9, 8);
            g.DrawEllipse(Crease, 4, -15, 9, 8);
            g.FillEllipse(Fur, 5, -18, 3.4f, 4.6f);
            g.FillEllipse(Eye, 9.4f, -12.6f, 1.8f, 1.9f);
            g.FillEllipse(Glint, 9.9f, -12.4f, 0.6f, 0.6f);
            g.FillEllipse(Eye, 12.2f, -10.6f, 1.2f, 1);
            g.DrawLine(Whisker, 12.6f, -10, 15.6f, -11);
            g.Restore(state);
            if (sitting)
            {
                g.FillEllipse(Acorn, 8, -12, 5, 6);
                g.FillEllipse(AcornCap, 7.6f, -13.6f, 5.8f, 3);
                g.FillEllipse(Fur, 6.4f, -10.6f, 3.4f, 2.6f);
            }
            g.FillEllipse(Fur, -6 - bound * 3, -3.6f, 6, 3.6f);
            g.FillEllipse(Fur, 4 + bound * 3, -3, 4.4f, 3);
        }

        private static Brush Ink(int red, int green, int blue) { return new SolidBrush(Color.FromArgb(red, green, blue)); }

        private static Pen Line(Color color, float width)
        {
            return new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        }
    }
}
