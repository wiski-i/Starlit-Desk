using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace SnowDesk
{
    /// <summary>Spring characters in the winter decorations' style. Walkers stand on (0, 0) and face right.</summary>
    internal static class SpringArt
    {
        private static readonly Brush Skin = DecorationArt.Ramp(-70, -40, Color.FromArgb(236, 186, 150), Color.FromArgb(196, 138, 104));
        private static readonly Brush Straw = DecorationArt.Ramp(-74, -64, Color.FromArgb(244, 214, 140), Color.FromArgb(206, 166, 88));
        private static readonly Brush Band = Ink(196, 70, 70);
        private static readonly Brush Shirt = DecorationArt.Ramp(-56, -30, Color.FromArgb(246, 246, 240), Color.FromArgb(206, 206, 200));
        private static readonly Brush Overall = DecorationArt.Ramp(-48, 0, Color.FromArgb(104, 150, 92), Color.FromArgb(60, 104, 64));
        private static readonly Brush OverallFar = Ink(54, 92, 58);
        private static readonly Brush Boot = DecorationArt.Ramp(-6, 0, Color.FromArgb(232, 196, 72), Color.FromArgb(190, 146, 40));
        private static readonly Brush Can = DecorationArt.Ramp(-16, 0, Color.FromArgb(108, 196, 200), Color.FromArgb(44, 128, 140));
        private static readonly Brush Eye = Ink(40, 32, 32);
        private static readonly Brush Glint = new SolidBrush(Color.FromArgb(230, 255, 255, 255));
        private static readonly Brush Blush = new SolidBrush(Color.FromArgb(80, 232, 120, 110));
        private static readonly Brush Drop = new SolidBrush(Color.FromArgb(210, 170, 216, 246));
        private static readonly Brush[] WingFill =
        {
            DecorationArt.Ramp(-8, 8, Color.FromArgb(252, 168, 60), Color.FromArgb(214, 100, 30)),
            DecorationArt.Ramp(-8, 8, Color.FromArgb(120, 182, 246), Color.FromArgb(56, 102, 206)),
            DecorationArt.Ramp(-8, 8, Color.FromArgb(252, 236, 120), Color.FromArgb(224, 196, 60))
        };
        private static readonly Brush WingDark = Ink(44, 34, 38);
        private static readonly Brush WingDot = new SolidBrush(Color.FromArgb(230, 255, 255, 250));
        private static readonly Brush BeeBody = DecorationArt.Ramp(-4, 4, Color.FromArgb(252, 212, 70), Color.FromArgb(222, 152, 30));
        private static readonly Brush BeeWing = new SolidBrush(Color.FromArgb(120, 230, 244, 255));
        private static readonly Brush Navy = DecorationArt.Ramp(-6, 4, Color.FromArgb(52, 64, 108), Color.FromArgb(24, 30, 58));
        private static readonly Brush WhiteBelly = Ink(246, 242, 236);
        private static readonly Brush Throat = Ink(200, 92, 64);
        private static readonly Brush HiveFill = DecorationArt.Ramp(-2, 28, Color.FromArgb(232, 196, 120), Color.FromArgb(176, 128, 66));
        private static readonly Brush Honey = new SolidBrush(Color.FromArgb(220, 236, 168, 40));
        private static readonly Brush CarPaint = DecorationArt.Ramp(-34, -8, Color.FromArgb(226, 72, 64), Color.FromArgb(150, 34, 40));
        private static readonly Brush CarGlass = DecorationArt.Ramp(-31, -22, Color.FromArgb(176, 210, 222), Color.FromArgb(96, 140, 162));
        private static readonly Brush TailLight = Ink(176, 36, 40);
        private static readonly Brush Suit = DecorationArt.Ramp(-80, 0, Color.FromArgb(250, 250, 246), Color.FromArgb(206, 208, 204));
        private static readonly Brush SuitFar = Ink(196, 198, 194);
        private static readonly Brush Glove = Ink(232, 196, 92);
        private static readonly Brush Veil = new SolidBrush(Color.FromArgb(120, 40, 40, 44));
        private static readonly Brush AngryEye = Ink(214, 40, 40);
        private static readonly Pen Mesh = Line(Color.FromArgb(70, 20, 20, 24), 0.5f);
        private static readonly Pen Brow = Line(Color.FromArgb(40, 30, 30), 0.9f);
        private static readonly Pen Crease = Line(Color.FromArgb(110, 36, 35, 40), 0.8f);
        private static readonly Pen Vein = Line(Color.FromArgb(150, 44, 34, 38), 0.6f);
        private static readonly Pen Stripe = Line(Color.FromArgb(48, 36, 30), 1.6f);
        private static readonly Pen Antenna = Line(Color.FromArgb(60, 46, 46), 0.5f);
        private static readonly Pen Spout = Line(Color.FromArgb(56, 140, 150), 1.8f);
        private static readonly Pen Handle = Line(Color.FromArgb(44, 120, 130), 1.4f);
        private static readonly Pen Shine = Line(Color.FromArgb(150, 255, 255, 255), 0.9f);
        private static readonly GraphicsPath Bib = DecorationArt.RoundedPolygon(new[] {
            new PointF(-5, -48), new PointF(5, -48), new PointF(7, -38), new PointF(8, -28), new PointF(-7, -28), new PointF(-6, -38)
        }, 0.2f);

        /// <summary>A gardener with a straw hat and watering can; when watering, the can tips and pours.</summary>
        internal static void Gardener(Graphics g, double phase, bool watering)
        {
            float swing = watering ? 0 : (float)Math.Cos(phase * Math.PI);
            Leg(g, OverallFar, -swing * 8);
            Leg(g, Overall, swing * 8);
            SummerArt.Taper(g, Shirt, 4.4f, 3.4f, new PointF(-2, -52), new PointF(-5 - swing * 4, -43), new PointF(-3 - swing * 5, -36));
            using (var torso = DecorationArt.RoundedPolygon(new[] {
                new PointF(-6, -56), new PointF(6, -57), new PointF(8, -44), new PointF(7, -30), new PointF(-7, -30), new PointF(-7.4f, -44)
            }, 0.24f))
            {
                g.FillPath(Shirt, torso);
                g.DrawPath(Crease, torso);
            }
            g.FillPath(Overall, Bib);
            g.DrawPath(Crease, Bib);
            g.DrawLine(Crease, -4, -48, -5, -56);
            g.DrawLine(Crease, 4, -48, 4.6f, -56.6f);
            g.FillEllipse(Boot, 1, -45, 2.4f, 2.4f);
            float tilt = watering ? -34 + (float)Math.Sin(phase * 2) * 4 : 0;
            var hand = watering ? new PointF(13, -40) : new PointF(8 + swing * 5, -35);
            WateringCan(g, hand.X + 2, hand.Y + 14, tilt, watering, phase);
            SummerArt.Taper(g, Shirt, 4.6f, 3.6f, new PointF(3, -52), new PointF(8, -45), hand);
            g.FillEllipse(Skin, hand.X - 1.8f, hand.Y - 1.8f, 3.6f, 3.6f);
            g.FillRectangle(Skin, -1, -59.4f, 4, 4);
            g.FillEllipse(Skin, -5, -71, 12.6f, 13);
            g.DrawEllipse(Crease, -5, -71, 12.6f, 13);
            g.FillEllipse(Eye, 4, -65.4f, 1.6f, 1.8f);
            g.FillEllipse(Glint, 4.5f, -65.2f, 0.6f, 0.6f);
            g.FillEllipse(Blush, 3, -62.6f, 3, 1.6f);
            g.DrawArc(Crease, 3.4f, -61.8f, 3, 1.6f, 10, 150);
            g.FillEllipse(Straw, -13, -70.6f, 28, 5.4f);
            g.DrawEllipse(Crease, -13, -70.6f, 28, 5.4f);
            using (var crown = new GraphicsPath())
            {
                crown.AddBezier(-6, -68, -6.4f, -77, 7.6f, -77.6f, 8, -68.4f);
                crown.CloseFigure();
                g.FillPath(Straw, crown);
                g.DrawPath(Crease, crown);
            }
            g.FillRectangle(Band, -6.2f, -70.8f, 14.4f, 2.4f);
        }

        private static void Leg(Graphics g, Brush cloth, float footX)
        {
            SummerArt.Taper(g, cloth, 6, 4.8f, new PointF(0, -29), new PointF(footX * 0.45f + 1.6f, -15), new PointF(footX, -4));
            using (GraphicsPath boot = DeskStyle.Rounded(new Rectangle((int)Math.Round(footX - 3), -6, 8, 6), 2))
            {
                g.FillPath(Boot, boot);
                g.DrawPath(Crease, boot);
            }
        }

        private static void WateringCan(Graphics g, float x, float y, float tilt, bool pouring, double phase)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform(tilt);
            g.DrawLine(Spout, 6, -8, 17, -16);
            g.FillEllipse(Can, 15, -18.6f, 5, 4);
            using (GraphicsPath body = DeskStyle.Rounded(new Rectangle(-8, -14, 15, 13), 3))
            {
                g.FillPath(Can, body);
                g.DrawPath(Crease, body);
            }
            g.DrawArc(Handle, -6, -20, 10, 10, 180, 180);
            g.DrawLine(Shine, -6, -11, -6, -4);
            g.Restore(state);
            if (!pouring) return;
            // Drops leave the rose in a short arc towards the ground.
            double radians = tilt * Math.PI / 180;
            float roseX = x + (float)(17.5 * Math.Cos(radians) + 16.6 * Math.Sin(radians));
            float roseY = y + (float)(17.5 * Math.Sin(radians) - 16.6 * Math.Cos(radians));
            for (int i = 0; i < 9; i++)
            {
                double t = ((phase * 1.8 + i / 9.0) % 1);
                float dx = (float)(t * 9 + (i % 3) * 1.6), dy = (float)(t * t * 46);
                g.FillEllipse(Drop, roseX + dx - 0.8f, roseY + dy, 1.6f, 2.4f);
            }
        }

        /// <summary>A butterfly seen from above; <paramref name="open"/> 0..1 spreads the wings.</summary>
        internal static void Butterfly(Graphics g, int tint, double open)
        {
            float spread = (float)(0.2 + 0.8 * open);
            Brush fill = WingFill[tint % WingFill.Length];
            for (int side = -1; side <= 1; side += 2)
            {
                GraphicsState state = g.Save();
                g.ScaleTransform(side * spread, 1);
                using (var fore = new GraphicsPath())
                {
                    fore.AddBezier(0.6f, -1, 4, -9, 11, -10, 11, -6);
                    fore.AddBezier(11, -6, 11, -2, 6, 0, 0.6f, 0);
                    fore.CloseFigure();
                    g.FillPath(fill, fore);
                    g.DrawPath(Crease, fore);
                    g.DrawBezier(Vein, 1, -0.6f, 4, -4, 7, -6, 10, -6.4f);
                }
                using (var hind = new GraphicsPath())
                {
                    hind.AddBezier(0.6f, 0, 6, 0, 9, 3, 7, 7);
                    hind.AddBezier(7, 7, 5, 9, 1.6f, 6, 0.6f, 1);
                    hind.CloseFigure();
                    g.FillPath(fill, hind);
                    g.DrawPath(Crease, hind);
                }
                g.FillEllipse(WingDark, 8.4f, -8, 2.6f, 2);
                g.FillEllipse(WingDot, 9, -7.6f, 1, 0.9f);
                g.FillEllipse(WingDot, 5.4f, 4.6f, 1, 0.9f);
                g.Restore(state);
            }
            g.FillEllipse(WingDark, -1, -4, 2, 10);
            g.DrawBezier(Antenna, -0.4f, -4, -1.6f, -7, -3, -9, -3.6f, -9.6f);
            g.DrawBezier(Antenna, 0.4f, -4, 1.6f, -7, 3, -9, 3.6f, -9.6f);
        }

        /// <summary>A wild hive hanging from (0, 0); <paramref name="crushed"/> draws the flattened remains on the ground instead.</summary>
        internal static void Hive(Graphics g, bool crushed, double wobble)
        {
            if (crushed)
            {
                g.FillEllipse(Honey, -18, -3.4f, 36, 5);
                g.FillEllipse(HiveFill, -14, -5, 13, 5);
                g.FillEllipse(HiveFill, 2, -6, 12, 6);
                g.DrawEllipse(Crease, -14, -5, 13, 5);
                g.DrawEllipse(Crease, 2, -6, 12, 6);
                g.FillPolygon(HiveFill, new[] { new PointF(-3, -4), new PointF(1, -9), new PointF(3, -3) });
                g.DrawLine(Shine, -12, -1.6f, -4, -1.6f);
                return;
            }
            g.RotateTransform((float)wobble);
            g.DrawLine(Handle, 0, 0, 0, 4);
            using (var body = new GraphicsPath())
            {
                body.AddBezier(0, 3, 7, 3, 10, 10, 10, 15);
                body.AddBezier(10, 15, 10, 22, 6, 27, 0, 27);
                body.AddBezier(0, 27, -6, 27, -10, 22, -10, 15);
                body.AddBezier(-10, 15, -10, 10, -7, 3, 0, 3);
                body.CloseFigure();
                g.FillPath(HiveFill, body);
                GraphicsState state = g.Save();
                g.SetClip(body, CombineMode.Intersect);
                for (float y = 8; y < 26; y += 4.6f) g.DrawArc(Crease, -12, y - 2, 24, 5, 0, 180);
                g.Restore(state);
                g.DrawPath(Crease, body);
            }
            g.FillEllipse(WingDark, -2.4f, 19, 4.8f, 3.6f);
            g.DrawArc(Shine, -7, 8, 8, 12, 160, 70);
        }

        /// <summary>A small red car; <paramref name="scared"/> shows the driver wide-eyed as the bees close in.</summary>
        internal static void Car(Graphics g, double wheels, bool scared)
        {
            g.FillRectangle(WingDark, -33, -12, 67, 5);
            using (var body = DecorationArt.RoundedPolygon(new[] {
                new PointF(-34, -9), new PointF(-33, -20), new PointF(-20, -22), new PointF(-11, -33), new PointF(11, -33),
                new PointF(21, -22), new PointF(33, -20), new PointF(36, -11), new PointF(35, -8)
            }, 0.14f))
            {
                g.FillPath(CarPaint, body);
                g.DrawPath(Crease, body);
            }
            g.FillPolygon(CarGlass, new[] { new PointF(-17, -22), new PointF(-9, -30.6f), new PointF(-1, -30.6f), new PointF(-1, -22) });
            g.FillPolygon(CarGlass, new[] { new PointF(2, -22), new PointF(2, -30.6f), new PointF(10, -30.6f), new PointF(18, -22) });
            g.DrawLine(Shine, -11, -28.4f, -7, -24);
            g.FillEllipse(Skin, 4.4f, -30, 7, 7.4f);
            g.FillPie(WingDark, 4, -31, 7.6f, 5, 180, 180);
            if (scared)
            {
                g.FillEllipse(Glint, 8.2f, -28.6f, 2.6f, 2.6f);
                g.FillEllipse(WingDark, 8.9f, -27.9f, 1.2f, 1.2f);
                g.FillEllipse(WingDark, 9.2f, -24.8f, 1.6f, 1.8f);
            }
            else g.FillEllipse(Eye, 8.6f, -27.6f, 1.4f, 1.5f);
            g.DrawLine(Crease, 0.4f, -22, 0.4f, -10);
            g.DrawLine(Crease, 5, -18, 8, -18);
            g.FillEllipse(Honey, 31.6f, -18, 4, 3.4f);
            g.FillRectangle(TailLight, -34.4f, -18, 2.4f, 3.6f);
            g.DrawLine(Shine, -28, -19.6f, 28, -19);
            DecorationArt.Wheel(g, -20, wheels);
            DecorationArt.Wheel(g, 22, wheels);
        }

        /// <summary>
        /// A beekeeper in a white suit and veiled hat. Pose 0 walks carrying a new hive, 1 reaches up to hang
        /// it, 2 walks away empty-handed.
        /// </summary>
        internal static void Beekeeper(Graphics g, double phase, int pose)
        {
            float swing = pose != 1 ? (float)Math.Cos(phase * Math.PI) : 0;
            Leg(g, SuitFar, -swing * 8);
            Leg(g, Suit, swing * 8);
            using (var torso = DecorationArt.RoundedPolygon(new[] {
                new PointF(-7, -56), new PointF(7, -57), new PointF(9, -44), new PointF(8, -29), new PointF(-8, -29), new PointF(-8.4f, -44)
            }, 0.24f))
            {
                g.FillPath(Suit, torso);
                g.DrawPath(Crease, torso);
            }
            g.DrawLine(Crease, 0.6f, -55, 1, -30);
            var hand = pose == 1 ? new PointF(8, -78) : new PointF(11, -40);
            if (pose == 0)
            {
                GraphicsState state = g.Save();
                g.TranslateTransform(13, -52);
                g.ScaleTransform(0.8f, 0.8f);
                Hive(g, false, 0);
                g.Restore(state);
            }
            SummerArt.Taper(g, Suit, 5, 4, new PointF(-3, -53), new PointF(pose == 1 ? -2 : 4, pose == 1 ? -66 : -44), pose == 1 ? new PointF(2, -78) : hand);
            SummerArt.Taper(g, Suit, 5, 4, new PointF(4, -53), new PointF(pose == 1 ? 8 : 9, pose == 1 ? -66 : -46), hand);
            g.FillEllipse(Glove, hand.X - 2.6f, hand.Y - 2.6f, 5.2f, 5.2f);
            g.FillRectangle(Skin, -1, -60, 4, 4);
            g.FillEllipse(Skin, -5, -71.6f, 12.6f, 13);
            g.FillEllipse(Eye, 4, -66, 1.6f, 1.8f);
            using (var veil = new GraphicsPath())
            {
                veil.AddRectangle(new RectangleF(-8, -71, 18, 14));
                g.FillPath(Veil, veil);
                for (float x = -6; x < 10; x += 2.6f) g.DrawLine(Mesh, x, -71, x, -57);
            }
            g.FillEllipse(Suit, -13, -74, 28, 5.4f);
            g.DrawEllipse(Crease, -13, -74, 28, 5.4f);
            using (var crown = new GraphicsPath())
            {
                crown.AddBezier(-6, -71.6f, -6.4f, -80, 7.6f, -80.6f, 8, -72);
                crown.CloseFigure();
                g.FillPath(Suit, crown);
                g.DrawPath(Crease, crown);
            }
        }

        /// <summary>A bumblebee in side view with fast wings.</summary>
        internal static void Bee(Graphics g, double phase)
        {
            Bee(g, phase, false);
        }

        /// <summary>A bumblebee; an angry one frowns and runs hot.</summary>
        internal static void Bee(Graphics g, double phase, bool angry)
        {
            bool up = Math.Sin(phase * 60) > 0;
            g.FillEllipse(BeeWing, -2, up ? -9 : -6.6f, 6, up ? 6 : 3.6f);
            using (var body = new GraphicsPath())
            {
                body.AddEllipse(-6, -3.6f, 11, 7.4f);
                g.FillPath(BeeBody, body);
                GraphicsState state = g.Save();
                g.SetClip(body, CombineMode.Intersect);
                g.DrawLine(Stripe, -3, -4, -3.6f, 4);
                g.DrawLine(Stripe, 0.4f, -4, 0, 4);
                g.Restore(state);
                g.DrawPath(Crease, body);
            }
            g.FillEllipse(WingDark, 3.6f, -3, 5, 5);
            g.FillEllipse(angry ? AngryEye : Glint, 6, -1.8f, 1, 1);
            if (angry) g.DrawLine(Brow, 5, -3.4f, 8, -1.8f);
            g.FillPolygon(WingDark, new[] { new PointF(-6, -0.6f), new PointF(-8.4f, 0), new PointF(-6, 0.8f) });
            g.FillEllipse(BeeWing, 0.6f, up ? -10 : -7, 5, up ? 6.4f : 3.6f);
        }

        /// <summary>A swallow in side view; <paramref name="flap"/> -1..1 raises or lowers the wings.</summary>
        internal static void Swallow(Graphics g, double flap)
        {
            float lift = (float)flap * 8;
            using (var far = new GraphicsPath())
            {
                far.AddBezier(-2, -1, -4, -6 - lift, -10, -10 - lift * 1.4f, -15, -11 - lift * 1.6f);
                far.AddBezier(-15, -11 - lift * 1.6f, -9, -6 - lift, -5, -2, -1, 0);
                far.CloseFigure();
                g.FillPath(Navy, far);
            }
            using (var body = new GraphicsPath())
            {
                body.AddBezier(9, -1, 7, -4, 0, -4, -6, -1.6f);
                body.AddLine(-6, -1.6f, -16, -4.6f);
                body.AddLine(-16, -4.6f, -9, 0);
                body.AddLine(-9, 0, -16, 3.4f);
                body.AddLine(-16, 3.4f, -6, 1.4f);
                body.AddBezier(-6, 1.4f, 0, 3.4f, 6, 3, 9, -1);
                body.CloseFigure();
                g.FillPath(Navy, body);
                g.DrawPath(Crease, body);
            }
            g.FillEllipse(WhiteBelly, -4, -0.4f, 10, 3);
            g.FillEllipse(Throat, 5.6f, -1.6f, 3, 2.6f);
            g.FillEllipse(Glint, 6.6f, -2.6f, 0.9f, 0.9f);
            using (var near = new GraphicsPath())
            {
                near.AddBezier(1, -1, 0, -7 - lift, -6, -12 - lift * 1.6f, -13, -14 - lift * 1.8f);
                near.AddBezier(-13, -14 - lift * 1.8f, -6, -7 - lift, -3, -2, 2, 0);
                near.CloseFigure();
                g.FillPath(Navy, near);
                g.DrawPath(Crease, near);
            }
        }

        private static Brush Ink(int red, int green, int blue) { return new SolidBrush(Color.FromArgb(red, green, blue)); }

        private static Pen Line(Color color, float width)
        {
            return new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        }
    }
}
