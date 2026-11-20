using System;
using System.Drawing;

namespace SnowDesk
{
    internal sealed class Snowman
    {
        private static readonly Brush Shade = SnowField.Brush(227, 170, 194, 211);
        private static readonly Brush Snow = SnowField.Brush(246, 246, 252, 255);
        private static readonly Brush Coal = SnowField.Brush(248, 52, 61, 70);
        private static readonly Brush Carrot = SnowField.Brush(255, 213, 118, 58);
        private static readonly Brush Scarf = SnowField.Brush(249, 150, 63, 79);
        private double x, y, mass, packed;

        internal double Gather(double target, double ground, double offer)
        {
            if (offer <= 0 || (mass >= 0.5 && Math.Abs(x - target) > (mass < 24 ? 80 : 30))) return 0;
            if (mass < 0.5) { x = target; y = ground; mass = 0; }
            double accepted = Math.Min(offer, 142 - mass);
            if (mass < 24) x += (target - x) * Math.Min(0.35, accepted / 14);
            mass += accepted;
            return accepted;
        }

        internal void Advance(double seconds, double melt)
        {
            mass = Math.Max(0, mass - seconds * melt * (mass < 24 ? 1.3 : 0.42));
            packed += (mass - packed) * (1 - Math.Exp(-3 * seconds));
        }

        internal void Draw(Canvas canvas)
        {
            if (packed < 1) return;
            double size = Math.Sqrt(packed / 116);
            double head = Math.Max(0, Math.Min(1, (packed - 22) / 32));
            canvas.DrawArt(x, y, 1, false, g => Paint(g, (float)size, (float)head));
        }

        private static void Paint(Graphics g, float size, float head)
        {
            float body = 25 * size;
            g.FillEllipse(Shade, -body * 0.61f, -body * 0.9f, body * 1.22f, body * 0.9f);
            g.FillEllipse(Snow, -body * 0.46f, -body * 0.91f, body * 1.01f, body * 0.84f);
            if (head <= 0.025f) return;
            float width = body * 0.66f * head, crown = -body * 0.83f - width * 0.97f;
            size *= head;
            g.FillEllipse(Shade, -width * 0.5f, crown, width, width);
            g.FillEllipse(Snow, -width * 0.45f, crown, width * 0.91f, width * 0.91f);
            g.FillEllipse(Coal, -3.6f * size, crown + width * 0.33f, 1.7f * size, 1.7f * size);
            g.FillEllipse(Coal, 1.8f * size, crown + width * 0.33f, 1.7f * size, 1.7f * size);
            g.FillPolygon(Carrot, new[] { new PointF(0, crown + width * 0.52f),
                new PointF(6 * size, crown + width * 0.58f), new PointF(0, crown + width * 0.66f) });
            g.FillEllipse(Scarf, -width * 0.49f, crown + width * 0.81f, width * 0.98f, width * 0.22f);
        }
    }
}
