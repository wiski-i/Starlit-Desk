using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace SnowDesk
{
    /// <summary>
    /// Leaves, blossom petals and set pieces of the autumn and spring themes, drawn like the winter
    /// decorations: shaded fills, thin creases, veins. Piece shapes are unit sized and scaled per piece.
    /// </summary>
    internal static class SeasonArt
    {
        private static readonly GraphicsPath[] AutumnShapes = { Maple(), Oak(), Birch() };
        private static readonly GraphicsPath[] SpringShapes = { Sakura(), RoundPetal() };
        private static readonly Brush[] AutumnFront =
        {
            Unit(Color.FromArgb(214, 70, 46), Color.FromArgb(150, 34, 32)), Unit(Color.FromArgb(240, 136, 44), Color.FromArgb(196, 84, 28)),
            Unit(Color.FromArgb(246, 190, 62), Color.FromArgb(208, 132, 30)), Unit(Color.FromArgb(232, 210, 92), Color.FromArgb(186, 158, 48)),
            Unit(Color.FromArgb(170, 108, 58), Color.FromArgb(112, 66, 36))
        };
        private static readonly Brush[] AutumnBack =
        {
            Solid(160, 62, 50), Solid(186, 112, 52), Solid(196, 154, 66), Solid(186, 170, 92), Solid(126, 84, 50)
        };
        private static readonly Brush[] SpringFront =
        {
            Unit(Color.FromArgb(252, 200, 220), Color.FromArgb(236, 138, 176)), Unit(Color.FromArgb(255, 226, 236), Color.FromArgb(242, 178, 204)),
            Unit(Color.FromArgb(255, 252, 252), Color.FromArgb(234, 212, 222))
        };
        private static readonly Brush[] SpringBack = { Solid(226, 160, 186), Solid(236, 196, 212), Solid(226, 214, 220) };
        private static readonly Pen LeafVein = UnitPen(Color.FromArgb(120, 70, 30, 20), 0.045f);
        private static readonly Pen LeafEdge = UnitPen(Color.FromArgb(90, 60, 26, 18), 0.04f);
        private static readonly Pen PetalVein = UnitPen(Color.FromArgb(70, 200, 100, 140), 0.04f);
        private static readonly Pen Stem = UnitPen(Color.FromArgb(150, 92, 58, 32), 0.06f);

        private static readonly Brush LeafGreen = DecorationArt.Ramp(-40, 0, Color.FromArgb(120, 182, 92), Color.FromArgb(56, 120, 62));
        private static readonly Brush DaisyPetal = DecorationArt.Ramp(-40, -20, Color.FromArgb(255, 255, 252), Color.FromArgb(226, 224, 236));
        private static readonly Brush DaisyHeart = DecorationArt.Ramp(-34, -26, Color.FromArgb(252, 214, 80), Color.FromArgb(222, 150, 30));
        private static readonly Brush[] TulipFill =
        {
            DecorationArt.Ramp(-44, -28, Color.FromArgb(240, 82, 92), Color.FromArgb(178, 36, 60)),
            DecorationArt.Ramp(-44, -28, Color.FromArgb(252, 214, 86), Color.FromArgb(226, 150, 40)),
            DecorationArt.Ramp(-44, -28, Color.FromArgb(178, 128, 222), Color.FromArgb(112, 70, 170))
        };
        private static readonly Pen Crease = Line(Color.FromArgb(110, 36, 35, 40), 0.8f);
        private static readonly Pen StemLine = Line(Color.FromArgb(84, 140, 70), 1.6f);
        private static readonly Pen Blade = Line(Color.FromArgb(96, 164, 82), 1.4f);
        private static readonly Pen BladeDark = Line(Color.FromArgb(64, 128, 66), 1.3f);
        private static readonly Pen Shine = Line(Color.FromArgb(150, 255, 255, 255), 0.9f);

        /// <summary>
        /// Draws one leaf or petal centred at (x, y). <paramref name="flip"/> is the cosine of its tumble: the
        /// width narrows as it turns and the back face shows when negative. Resting pieces are foreshortened.
        /// </summary>
        internal static void Piece(Graphics g, Season season, byte kind, byte tint, float size, float angle, float flip,
            float x, float y, bool resting)
        {
            bool autumn = season == Season.Autumn;
            GraphicsPath[] shapes = autumn ? AutumnShapes : SpringShapes;
            Brush[] front = autumn ? AutumnFront : SpringFront, back = autumn ? AutumnBack : SpringBack;
            GraphicsState state = g.Save();
            g.TranslateTransform(x, y);
            // A resting piece lies flat, so it is foreshortened in screen space, not along its own axis.
            if (resting) g.ScaleTransform(1, 0.5f);
            g.RotateTransform(angle);
            g.ScaleTransform(Math.Max(0.3f, Math.Abs(flip)) * size, size);
            GraphicsPath shape = shapes[kind % shapes.Length];
            g.FillPath(flip < 0 ? back[tint % back.Length] : front[tint % front.Length], shape);
            if (autumn)
            {
                g.DrawPath(LeafEdge, shape);
                g.DrawLine(Stem, 0, 0.22f, 0, 0.5f);
                g.DrawLine(LeafVein, 0, 0.24f, 0, -0.38f);
                if (kind == 0)
                {
                    g.DrawLine(LeafVein, 0, 0.12f, 0.3f, -0.18f);
                    g.DrawLine(LeafVein, 0, 0.12f, -0.3f, -0.18f);
                }
            }
            else g.DrawLine(PetalVein, 0, 0.36f, 0, -0.18f);
            g.Restore(state);
        }

        private static GraphicsPath Maple()
        {
            return DecorationArt.RoundedPolygon(new[] {
                new PointF(0, -0.5f), new PointF(0.08f, -0.24f), new PointF(0.26f, -0.38f), new PointF(0.22f, -0.12f),
                new PointF(0.46f, -0.14f), new PointF(0.3f, 0.04f), new PointF(0.38f, 0.16f), new PointF(0.1f, 0.12f),
                new PointF(0.03f, 0.26f), new PointF(-0.03f, 0.26f), new PointF(-0.1f, 0.12f), new PointF(-0.38f, 0.16f),
                new PointF(-0.3f, 0.04f), new PointF(-0.46f, -0.14f), new PointF(-0.22f, -0.12f), new PointF(-0.26f, -0.38f),
                new PointF(-0.08f, -0.24f)
            }, 0.16f);
        }

        private static GraphicsPath Oak()
        {
            var points = new PointF[14];
            for (int i = 0; i < points.Length; i++)
            {
                double a = i * Math.PI * 2 / points.Length - Math.PI / 2;
                double r = i % 2 == 0 ? 1 : 0.74;
                points[i] = new PointF((float)(Math.Cos(a) * 0.27 * r), (float)(Math.Sin(a) * 0.48 * (i % 2 == 0 ? 1 : 0.92)));
            }
            return DecorationArt.RoundedPolygon(points, 0.42f);
        }

        private static GraphicsPath Birch()
        {
            var path = new GraphicsPath();
            path.AddBezier(0, -0.5f, 0.3f, -0.3f, 0.34f, 0.08f, 0, 0.26f);
            path.AddBezier(0, 0.26f, -0.34f, 0.08f, -0.3f, -0.3f, 0, -0.5f);
            path.CloseFigure();
            return path;
        }

        private static GraphicsPath Sakura()
        {
            var path = new GraphicsPath();
            path.AddBezier(0, 0.46f, 0.34f, 0.2f, 0.4f, -0.22f, 0.16f, -0.46f);
            path.AddBezier(0.16f, -0.46f, 0.1f, -0.4f, 0.05f, -0.34f, 0, -0.32f);
            path.AddBezier(0, -0.32f, -0.05f, -0.34f, -0.1f, -0.4f, -0.16f, -0.46f);
            path.AddBezier(-0.16f, -0.46f, -0.4f, -0.22f, -0.34f, 0.2f, 0, 0.46f);
            path.CloseFigure();
            return path;
        }

        private static GraphicsPath RoundPetal()
        {
            var path = new GraphicsPath();
            path.AddBezier(0, 0.44f, 0.36f, 0.3f, 0.36f, -0.44f, 0, -0.44f);
            path.AddBezier(0, -0.44f, -0.36f, -0.44f, -0.36f, 0.3f, 0, 0.44f);
            path.CloseFigure();
            return path;
        }

        /// <summary>A raked heap of leaves; <paramref name="amount"/> 0..1 grows it from a few leaves to a dome.</summary>
        internal static void Heap(Graphics g, double amount, int seed, float scale)
        {
            if (amount <= 0.01) return;
            var random = new Random(seed);
            int count = (int)(10 + amount * 46);
            float width = (float)(18 + amount * 34) * scale, height = (float)(4 + amount * 18) * scale;
            for (int i = 0; i < count; i++)
            {
                double t = random.NextDouble() * 2 - 1;
                double top = height * Math.Sqrt(Math.Max(0, 1 - t * t));
                float x = (float)(t * width), y = -(float)(random.NextDouble() * top);
                Piece(g, Season.Autumn, (byte)random.Next(3), (byte)random.Next(5), (float)((15 + random.NextDouble() * 7) * scale),
                    (float)(random.NextDouble() * 360), 1, x, y, true);
            }
        }

        /// <summary>A flower on (0, 0); kind 0 tulip, 1 daisy, 2 crocus. <paramref name="grow"/> 0..1 from sprout to bloom.</summary>
        internal static void Flower(Graphics g, int kind, int tint, double grow, double sway)
        {
            float stemTop = -(float)(8 + 30 * grow) * (kind == 2 ? 0.55f : 1);
            float tipX = (float)sway * (float)grow * 4;
            g.DrawBezier(StemLine, 0, 0, 0, stemTop * 0.4f, tipX * 0.6f, stemTop * 0.7f, tipX, stemTop);
            using (var leaf = new GraphicsPath())
            {
                float reach = (float)(6 + 10 * grow);
                leaf.AddBezier(0, -1, 4, -reach * 0.5f, 7, -reach * 0.9f, 4 + (float)sway, -reach * 1.3f);
                leaf.AddBezier(4 + (float)sway, -reach * 1.3f, 3, -reach * 0.7f, 1.6f, -reach * 0.3f, 0, -1);
                leaf.CloseFigure();
                g.FillPath(LeafGreen, leaf);
                g.DrawPath(Crease, leaf);
            }
            if (grow < 0.45)
            {
                g.FillEllipse(LeafGreen, tipX - 2, stemTop - 5, 4, 6);
                return;
            }
            double open = Math.Min(1, (grow - 0.45) / 0.45);
            g.TranslateTransform(tipX, stemTop);
            if (kind == 1) Daisy(g, open);
            else Cup(g, TulipFill[tint % TulipFill.Length], open, kind == 2 ? 0.75f : 1);
        }

        private static void Cup(Graphics g, Brush fill, double open, float size)
        {
            float spread = (float)(2 + open * 3) * size, tall = 11 * size;
            for (int side = -1; side <= 1; side++)
            {
                using (var petal = new GraphicsPath())
                {
                    float lean = side * spread;
                    petal.AddBezier(side * 1.4f, 1, lean - 4 * size, -tall * 0.3f, lean - 2.6f * size, -tall, lean, -tall);
                    petal.AddBezier(lean, -tall, lean + 2.6f * size, -tall, lean + 4 * size, -tall * 0.3f, side * 1.4f, 1);
                    petal.CloseFigure();
                    g.FillPath(fill, petal);
                    g.DrawPath(Crease, petal);
                }
            }
            g.DrawLine(Shine, -1.4f * size, -tall * 0.75f, -1 * size, -tall * 0.35f);
        }

        private static void Daisy(Graphics g, double open)
        {
            int petals = 10;
            for (int i = 0; i < petals; i++)
            {
                GraphicsState state = g.Save();
                g.RotateTransform(i * 360f / petals);
                g.ScaleTransform(1, (float)(0.4 + open * 0.6));
                g.FillEllipse(DaisyPetal, -1.6f, -9.4f, 3.2f, 8);
                g.DrawEllipse(Crease, -1.6f, -9.4f, 3.2f, 8);
                g.Restore(state);
            }
            g.FillEllipse(DaisyHeart, -3, -3, 6, 6);
            g.DrawEllipse(Crease, -3, -3, 6, 6);
        }

        /// <summary>A tuft of grass blades on (0, 0) bending with <paramref name="sway"/>.</summary>
        internal static void Grass(Graphics g, int seed, double sway)
        {
            var random = new Random(seed);
            int blades = 4 + random.Next(4);
            for (int i = 0; i < blades; i++)
            {
                float root = (float)(random.NextDouble() * 10 - 5), tall = (float)(6 + random.NextDouble() * 9);
                float bend = (float)(sway * (0.6 + random.NextDouble() * 0.8) + (random.NextDouble() - 0.5) * 3);
                g.DrawBezier(i % 2 == 0 ? Blade : BladeDark, root, 0, root, -tall * 0.4f, root + bend * 0.4f, -tall * 0.8f, root + bend, -tall);
            }
        }

        /// <summary>A soft grey exhaust puff of radius r centred on (0, 0).</summary>
        internal static void Smoke(Graphics g, float r, int alpha)
        {
            using (var puff = new SolidBrush(Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), 196, 196, 200)))
                g.FillEllipse(puff, -r, -r, r * 2, r * 2);
        }

        private static Brush Unit(Color light, Color dark)
        {
            return new LinearGradientBrush(new PointF(0, -0.52f), new PointF(0, 0.52f), light, dark) { WrapMode = WrapMode.TileFlipXY };
        }

        private static Brush Solid(int red, int green, int blue) { return new SolidBrush(Color.FromArgb(red, green, blue)); }

        private static Pen UnitPen(Color color, float width)
        {
            return new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        }

        private static Pen Line(Color color, float width)
        {
            return new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        }
    }
}
