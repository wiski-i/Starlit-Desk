using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace SnowDesk
{
    /// <summary>
    /// The season tree: a shaded trunk with three branches and a canopy of foliage clumps. Autumn clumps are
    /// orange, red and gold; spring clumps are cherry blossom. <c>foliage</c> 0..1 thins the canopy clump by
    /// clump down to bare branches. The tree stands on (0, 0); the canopy centre is <see cref="Canopy"/>.
    /// </summary>
    internal static class TreeArt
    {
        /// <summary>Canopy centre and radii in art units, for spawning pieces from the crown.</summary>
        internal static readonly PointF Canopy = new PointF(0, -112);
        internal const float CanopyRadiusX = 50, CanopyRadiusY = 34;
        /// <summary>End of the right-hand branch, where the spring hive hangs.</summary>
        internal static readonly PointF HiveBranch = new PointF(34, -76);

        private const int Clumps = 13;
        private static readonly Brush Bark = DecorationArt.Ramp(-100, 0, Color.FromArgb(132, 96, 70), Color.FromArgb(84, 58, 44));
        private static readonly Brush[] AutumnClump =
        {
            DecorationArt.Ramp(-150, -80, Color.FromArgb(250, 166, 64), Color.FromArgb(214, 104, 34)),
            DecorationArt.Ramp(-150, -80, Color.FromArgb(232, 92, 56), Color.FromArgb(176, 52, 40)),
            DecorationArt.Ramp(-150, -80, Color.FromArgb(250, 206, 84), Color.FromArgb(218, 150, 40))
        };
        private static readonly Brush[] SpringClump =
        {
            DecorationArt.Ramp(-150, -80, Color.FromArgb(255, 222, 236), Color.FromArgb(244, 168, 198)),
            DecorationArt.Ramp(-150, -80, Color.FromArgb(254, 206, 224), Color.FromArgb(236, 146, 182))
        };
        private static readonly Brush AutumnShade = DecorationArt.Ramp(-150, -76, Color.FromArgb(200, 92, 40), Color.FromArgb(132, 46, 30));
        private static readonly Brush SpringShade = DecorationArt.Ramp(-150, -76, Color.FromArgb(236, 154, 188), Color.FromArgb(198, 104, 146));
        private static readonly Pen Rim = Line(Color.FromArgb(90, 60, 30, 30), 0.9f);
        private static readonly Brush AutumnFleck = new SolidBrush(Color.FromArgb(110, 120, 46, 24));
        private static readonly Brush Blossom = new SolidBrush(Color.FromArgb(235, 255, 248, 250));
        private static readonly Brush BlossomHeart = new SolidBrush(Color.FromArgb(220, 214, 92, 134));
        private static readonly Pen Crease = Line(Color.FromArgb(105, 40, 30, 32), 0.8f);
        private static readonly Pen BarkLine = Line(Color.FromArgb(90, 40, 26, 20), 0.8f);
        private static readonly Pen Twig = Line(Color.FromArgb(104, 74, 56), 1.2f);
        private static readonly PointF[] ClumpCentres = BuildClumps();

        private static PointF[] BuildClumps()
        {
            var random = new Random(41);
            var centres = new PointF[Clumps];
            for (int i = 0; i < Clumps; i++)
            {
                double angle = i * 2.4, reach = i == 0 ? 0 : 0.35 + 0.6 * random.NextDouble();
                centres[i] = new PointF(Canopy.X + (float)(Math.Cos(angle) * reach * CanopyRadiusX),
                    Canopy.Y + (float)(Math.Sin(angle) * reach * CanopyRadiusY));
            }
            return centres;
        }

        internal static void Tree(Graphics g, Season season, double foliage, double sway)
        {
            Trunk(g, (float)sway);
            int shown = (int)Math.Round(Clumps * Math.Max(0, Math.Min(1, foliage)));
            if (shown == 0) return;
            Brush[] fills = season == Season.Autumn ? AutumnClump : SpringClump;
            // One shaded mass first, so the crown reads as a single canopy; lit clumps on top give it texture.
            using (var mass = new GraphicsPath(FillMode.Winding))
            {
                for (int i = Clumps - shown; i < Clumps; i++)
                {
                    PointF c = ClumpCentres[Clumps - 1 - i];
                    float r = Radius(Clumps - 1 - i) * 1.12f, x = c.X + Lean(c, sway);
                    mass.AddEllipse(x - r, c.Y - r * 0.86f, r * 2, r * 1.72f);
                }
                g.FillPath(season == Season.Autumn ? AutumnShade : SpringShade, mass);
                g.DrawPath(Rim, mass);
            }
            // The outer clumps go first, so a thinning crown keeps its middle the longest.
            for (int i = Clumps - shown; i < Clumps; i++)
            {
                int index = Clumps - 1 - i;
                PointF c = ClumpCentres[index];
                float r = Radius(index) * 0.82f, x = c.X + Lean(c, sway);
                g.FillEllipse(fills[index % fills.Length], x - r, c.Y - r * 1.02f, r * 2, r * 1.5f);
                if (season == Season.Autumn)
                {
                    g.FillEllipse(AutumnFleck, x - r * 0.4f, c.Y - r * 0.1f, r * 0.34f, r * 0.24f);
                    g.FillEllipse(AutumnFleck, x + r * 0.2f, c.Y + r * 0.3f, r * 0.3f, r * 0.2f);
                }
                else
                {
                    for (int f = 0; f < 3; f++)
                    {
                        float fx = x + (float)Math.Cos(index + f * 2.1) * r * 0.55f, fy = c.Y + (float)Math.Sin(index * 1.7 + f * 2.3) * r * 0.45f;
                        g.FillEllipse(Blossom, fx - 2.2f, fy - 2.2f, 4.4f, 4.4f);
                        g.FillEllipse(BlossomHeart, fx - 0.8f, fy - 0.8f, 1.6f, 1.6f);
                    }
                }
            }
        }

        private static float Radius(int index) { return 19 + (index % 3) * 3.5f; }

        private static float Lean(PointF clump, double sway) { return (float)sway * (1 - clump.Y / -150f) * 2; }

        private static void Trunk(Graphics g, float sway)
        {
            using (var root = new GraphicsPath())
            {
                root.AddBezier(-12, 0, -7, -3, -6, -8, -5.4f, -14);
                root.AddLine(-5.4f, -14, 5.4f, -14);
                root.AddBezier(5.4f, -14, 6, -8, 7, -3, 12, 0);
                root.CloseFigure();
                g.FillPath(Bark, root);
                g.DrawPath(Crease, root);
            }
            SummerArt.Taper(g, Bark, 11, 6, new PointF(0, -2), new PointF(1, -50), new PointF(-1 + sway * 0.4f, -96));
            SummerArt.Taper(g, Bark, 5.4f, 2.4f, new PointF(1, -50), new PointF(18, -64), new PointF(HiveBranch.X + sway * 0.6f, HiveBranch.Y));
            SummerArt.Taper(g, Bark, 5, 2.2f, new PointF(0, -66), new PointF(-16, -80), new PointF(-34 + sway * 0.7f, -90));
            SummerArt.Taper(g, Bark, 4, 2, new PointF(0, -90), new PointF(6, -106), new PointF(12 + sway, -124));
            g.DrawBezier(BarkLine, -2, -8, -1, -26, -3, -40, -1, -58);
            g.DrawBezier(BarkLine, 2.4f, -16, 3, -30, 2, -44, 3, -52);
            g.DrawLine(Twig, -34 + sway * 0.7f, -90, -42 + sway, -100);
            g.DrawLine(Twig, 12 + sway, -124, 8 + sway, -134);
            g.DrawLine(Twig, 12 + sway, -124, 20 + sway, -130);
            g.DrawLine(Twig, -20, -84, -22, -96);
        }

        private static Pen Line(Color color, float width)
        {
            return new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        }
    }
}
