using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace SnowDesk
{
    /// <summary>
    /// An astronaut on a spacewalk, drawn around (0, 0) about 64 units tall: white suit with soft shading,
    /// life-support backpack, gold sun visor with a reflection, chest control unit and grey gloves and boots.
    /// </summary>
    internal static class SpaceFigures
    {
        private static readonly Brush Suit = DecorationArt.Ramp(-34, 30, Color.FromArgb(250, 251, 252), Color.FromArgb(176, 182, 192));
        private static readonly Brush SuitFar = new SolidBrush(Color.FromArgb(168, 174, 186));
        private static readonly Brush Pack = DecorationArt.Ramp(-30, 6, Color.FromArgb(222, 226, 232), Color.FromArgb(140, 146, 158));
        private static readonly Brush Glove = new SolidBrush(Color.FromArgb(150, 156, 168));
        private static readonly Brush Visor = new LinearGradientBrush(new PointF(-8, -38), new PointF(8, -24),
            Color.FromArgb(246, 206, 116), Color.FromArgb(126, 78, 30));
        private static readonly Brush Unit = new SolidBrush(Color.FromArgb(108, 116, 130));
        private static readonly Brush Lamp = new SolidBrush(Color.FromArgb(232, 246, 252, 255));
        private static readonly Brush Patch = new SolidBrush(Color.FromArgb(52, 86, 150));
        private static readonly Brush Red = new SolidBrush(Color.FromArgb(206, 72, 64));
        private static readonly Brush Green = new SolidBrush(Color.FromArgb(96, 196, 120));
        private static readonly Pen Crease = new Pen(Color.FromArgb(110, 40, 46, 58), 0.8f) { LineJoin = LineJoin.Round };
        private static readonly Pen Seam = new Pen(Color.FromArgb(90, 120, 128, 142), 0.7f);
        private static readonly Pen Shine = new Pen(Color.FromArgb(200, 255, 255, 255), 1.1f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        private static readonly GraphicsPath Torso = DecorationArt.RoundedPolygon(new[] {
            new PointF(-11, -22), new PointF(11, -22), new PointF(13, -8), new PointF(11, 8), new PointF(-11, 8), new PointF(-13, -8)
        }, 0.26f);
        private static readonly GraphicsPath Backpack = DecorationArt.RoundedPolygon(new[] {
            new PointF(-15, -26), new PointF(15, -26), new PointF(16, 4), new PointF(-16, 4)
        }, 0.18f);

        /// <summary>A square that holds the figure at any rotation, for caching each turn as a bitmap.</summary>
        internal static readonly RectangleF Bounds = new RectangleF(-60, -60, 120, 120);

        internal static void Astronaut(Graphics g, float angle)
        {
            g.RotateTransform(angle);
            g.FillPath(Pack, Backpack);
            g.DrawPath(Crease, Backpack);
            // Far limbs first, then the body, then near limbs: a relaxed float with knees and elbows bent.
            SummerArt.Taper(g, SuitFar, 8, 7, new PointF(-6, 6), new PointF(-10, 17), new PointF(-7, 28));
            g.FillEllipse(Glove, -11, 26, 9, 6);
            SummerArt.Taper(g, SuitFar, 7, 6, new PointF(-11, -18), new PointF(-22, -10), new PointF(-25, 2));
            g.FillEllipse(Glove, -29, 0, 8, 8);
            g.FillPath(Suit, Torso);
            g.DrawPath(Crease, Torso);
            g.DrawLine(Seam, -11, -4, 11, -4);
            g.FillRectangle(Unit, -6, -14, 12, 7);
            g.DrawRectangle(Crease, -6, -14, 12, 7);
            g.FillEllipse(Red, -4, -12, 2.4f, 2.4f);
            g.FillEllipse(Green, -0.6f, -12, 2.4f, 2.4f);
            g.FillRectangle(Patch, 6, -19, 4, 3);
            SummerArt.Taper(g, Suit, 8.6f, 7.4f, new PointF(5, 6), new PointF(10, 18), new PointF(7, 30));
            g.FillEllipse(Glove, 3, 28, 10, 6);
            SummerArt.Taper(g, Suit, 7.4f, 6.4f, new PointF(11, -18), new PointF(21, -8), new PointF(22, 4));
            g.FillEllipse(Glove, 18, 2, 8, 8);
            g.FillEllipse(Suit, -13, -46, 26, 26);
            g.DrawEllipse(Crease, -13, -46, 26, 26);
            g.FillEllipse(Visor, -9, -41, 18, 15);
            g.DrawEllipse(Crease, -9, -41, 18, 15);
            g.DrawArc(Shine, -6, -39, 10, 9, 200, 70);
            g.FillEllipse(Lamp, 9, -44, 3, 3);
        }
    }
}
