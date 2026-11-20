using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using Rect = System.Windows.Rect;

namespace SnowDesk
{
    internal sealed class SnowLedge : IDisposable
    {
        internal const int Cell = 8;
        internal const double MaxDepth = 43.5;
        private const double SmallestVisibleDepth = 0.16;
        private const double ReposeRise = Cell * 0.73;
        private readonly GraphicsPath outline = new GraphicsPath();
        private readonly GraphicsPath flecks = new GraphicsPath();
        private static readonly Brush GrainShade = new SolidBrush(Color.FromArgb(69, 101, 136, 161));
        private LinearGradientBrush powder;
        private PointF[] crest;
        private PointF[] bezierControls;
        private Bitmap cachedSnow;
        private Graphics cacheGraphics;
        private int cacheX, cacheY;
        private bool dirty = true;
        private readonly List<Imprint> imprints = new List<Imprint>(24);

        private struct Imprint
        {
            internal double Offset, Radius, Depth, Age;
            internal bool Trunk;
        }
        private double total, radius, windowLeft, windowRight;
        private double[] crust;
        internal WindowMotion Motion;
        internal double LooseShare(int cell) { return 1 - 0.72 * crust[cell]; }

        internal readonly IntPtr Handle;
        internal Rect WindowBounds { get; private set; }
        internal double X { get; private set; }
        internal double Y { get; private set; }
        internal double Width { get; private set; }
        internal double[] Snow { get; private set; }
        internal bool[] Visible { get; private set; }
        internal double Total { get { return total; } }
        private double ContactDepth { get { return Handle == IntPtr.Zero ? 0 : 1.45; } }
        internal double LowestSupport { get { return Y + ContactDepth + radius; } }

        internal SnowLedge(IntPtr handle, Rect bounds, double x, double y, double width)
        {
            Handle = handle;
            SetShape(bounds, x, y, width, new double[CellCount(width)], 0);
        }

        internal static int CellCount(double width) { return (int)Math.Ceiling(width / Cell); }

        internal void SetShape(Rect bounds, double x, double y, double width, double[] snow, double mass)
        {
            double carried = bounds.Width == WindowBounds.Width ? bounds.Left - WindowBounds.Left : 0;
            for (int i = imprints.Count - 1; i >= 0; i--)
            {
                Imprint print = imprints[i];
                print.Offset += X + carried - x;
                if (print.Offset < 0 || print.Offset >= width) imprints.RemoveAt(i);
                else imprints[i] = print;
            }
            WindowBounds = bounds;
            X = x;
            Y = y;
            Width = width;
            Snow = snow;
            if (crust == null || crust.Length != snow.Length) Array.Resize(ref crust, snow.Length);
            if (Visible == null || Visible.Length != snow.Length)
            {
                Visible = new bool[snow.Length];
                crest = new PointF[snow.Length + 2];
                bezierControls = new PointF[(snow.Length + 1) * 3 + 1];
            }
            total = mass;
            dirty = true;
        }

        internal void SetCorners(double size, double left, double right)
        {
            size = Math.Min(size, (right - left) * 0.5);
            if (radius == size && windowLeft == left && windowRight == right) return;
            radius = size;
            windowLeft = left;
            windowRight = right;
            dirty = true;
        }

        internal double CellLeft(int cell) { return X + cell * Cell; }
        internal double CellRight(int cell) { return Math.Min(X + Width, CellLeft(cell) + Cell); }
        internal double CellCenter(int cell) { return (CellLeft(cell) + CellRight(cell)) * 0.5; }
        internal int IndexAt(double x) { return (int)Math.Floor((x - X) / Cell); }

        internal bool IsVisibleAt(double x)
        {
            int cell = IndexAt(x);
            return cell >= 0 && cell < Visible.Length && x < X + Width && Visible[cell];
        }

        internal double SupportAt(double x)
        {
            double baseY = Y + ContactDepth;
            if (radius <= 0) return baseY;
            double edge = Math.Max(0, Math.Min(x - windowLeft, windowRight - x));
            if (edge >= radius) return baseY;
            double offset = radius - edge;
            return baseY + radius - Math.Sqrt(radius * radius - offset * offset);
        }

        internal double SurfaceAt(double x)
        {
            int cell = Math.Max(0, Math.Min(Snow.Length - 1, IndexAt(x)));
            if (!DrawsAt(cell)) return SupportAt(x);
            double depth = ProfileDepth(cell);
            double center = CellCenter(cell);
            double otherCenter;
            double otherDepth;
            if (x < center)
            {
                otherCenter = DrawsAt(cell - 1) ? CellCenter(cell - 1) : CellLeft(cell);
                otherDepth = DrawsAt(cell - 1) ? ProfileDepth(cell - 1) : 0;
            }
            else
            {
                otherCenter = DrawsAt(cell + 1) ? CellCenter(cell + 1) : CellRight(cell);
                otherDepth = DrawsAt(cell + 1) ? ProfileDepth(cell + 1) : 0;
            }
            double distance = Math.Abs(center - otherCenter);
            double blend = distance > 0 ? Math.Min(1, Math.Abs(x - center) / distance) : 0;
            return SupportAt(x) - (depth + (otherDepth - depth) * blend);
        }

        internal void SetVisible(int cell, bool value)
        {
            if (Visible[cell] == value) return;
            Visible[cell] = value;
            dirty = true;
        }

        internal double Remove(int cell, double mass)
        {
            double removed = Math.Min(Snow[cell], mass);
            if (removed <= 0) return 0;
            Snow[cell] -= removed;
            if (Snow[cell] <= 0.001) crust[cell] = 0;
            total -= removed;
            dirty = true;
            return removed;
        }

        internal double Sweep(double from, ref double to)
        {
            int first = Math.Max(0, IndexAt(from));
            int last = Math.Min(Snow.Length - 1, IndexAt(to));
            double gathered = 0;
            for (int cell = first; cell <= last; cell++)
            {
                if (!Visible[cell])
                {
                    to = CellLeft(cell);
                    break;
                }
                gathered += Remove(cell, Snow[cell]);
            }
            return gathered;
        }

        internal void Drift(double wind, double seconds)
        {
            if (total <= 0.00001 || Math.Abs(wind) < 4) return;
            int downwind = wind > 0 ? 1 : -1;
            int end = wind > 0 ? 0 : Snow.Length - 1;
            double strength = seconds * Math.Min(0.72, (Math.Abs(wind) - 4) * 0.013);
            for (int cell = wind > 0 ? Snow.Length - 2 : 1; cell != end - downwind; cell -= downwind)
            {
                if (!Visible[cell] || Snow[cell] <= SmallestVisibleDepth) continue;
                int upstream = cell - downwind;
                double shelter = upstream >= 0 && upstream < Snow.Length && Visible[upstream]
                    ? Math.Max(0, Snow[upstream] - Snow[cell]) : 0;
                double center = CellCenter(cell);
                double sheltered = center >= ShelteredLeft && center <= ShelteredRight ? Shelter : 0;
                double carried = strength * Math.Min(Snow[cell], 6) * LooseShare(cell) /
                    (1 + 0.22 * shelter + 1.8 * sheltered);
                Remove(cell, Add(cell + downwind, Math.Min(Snow[cell], carried)));
            }
        }

        internal double Pack(int cell, double mass)
        {
            double placed = Add(cell - 1, mass * 0.21) + Add(cell + 1, mass * 0.21);
            placed += Add(cell, mass - placed);
            placed += Add(cell - 1, (mass - placed) * 0.5);
            placed += Add(cell + 1, mass - placed);

            RelaxPair(cell - 2);
            RelaxPair(cell - 1);
            RelaxPair(cell);
            RelaxPair(cell + 1);
            return placed;
        }

        internal bool Press(double x, double radius, double depth, bool trunk = false)
        {
            double powder;
            return Press(x, radius, depth, trunk, out powder);
        }

        internal bool Press(double x, double radius, double depth, bool trunk, out double powder)
        {
            powder = 0;
            if (!IsVisibleAt(x) || depth <= 0 || Snow[IndexAt(x)] < 1.8) return false;
            double snowDepth = Snow[IndexAt(x)];
            int left = Math.Max(0, IndexAt(x - radius));
            int right = Math.Min(Snow.Length - 1, IndexAt(x + radius));
            double compressed = 0;
            for (int cell = left; cell <= right; cell++)
            {
                if (!Visible[cell]) continue;
                double overlap = Math.Min(CellRight(cell), x + radius) - Math.Max(CellLeft(cell), x - radius);
                if (overlap <= 0) continue;
                compressed += Remove(cell, Math.Min(Snow[cell] * 0.25, depth * overlap / (CellRight(cell) - CellLeft(cell))));
            }
            if (compressed <= 0.001) return false;

            // A little powder moves to the sides. The rest loses volume as the snow compacts.
            Add(left - 1, compressed * 0.18);
            Add(right + 1, compressed * 0.18);
            if (!trunk) powder = compressed * 0.60 * Math.Min(1, Math.Max(0, (snowDepth - 8) / 8));
            if (imprints.Count == 24) imprints.RemoveAt(0);
            imprints.Add(new Imprint
            {
                Offset = x - X, Radius = radius, Depth = Math.Min(trunk ? 2.4 : 1.45, compressed), Trunk = trunk
            });
            dirty = true;
            return true;
        }

        private void RelaxPair(int left)
        {
            int right = left + 1;
            if (left < 0 || right >= Snow.Length || !Visible[left] || !Visible[right]) return;
            double rise = Snow[left] - Snow[right];
            if (Math.Abs(rise) <= ReposeRise) return;
            double slip = (rise - Math.Sign(rise) * ReposeRise) * 0.46;
            Snow[left] -= slip;
            Snow[right] += slip;
            dirty = true;
        }

        private double Add(int cell, double mass)
        {
            if (cell < 0 || cell >= Snow.Length || !Visible[cell] || mass <= 0) return 0;
            double placed = Math.Max(0, Math.Min(MaxDepth - Snow[cell], mass));
            if (placed <= 0) return 0;
            crust[cell] *= Snow[cell] / (Snow[cell] + 2.4 * placed);
            Snow[cell] += placed;
            total += placed;
            for (int i = imprints.Count - 1; i >= 0; i--)
            {
                Imprint print = imprints[i];
                if (Math.Abs(X + print.Offset - CellCenter(cell)) > print.Radius + Cell * 0.5) continue;
                print.Depth -= placed * 0.29;
                if (print.Depth <= 0.08) imprints.RemoveAt(i);
                else imprints[i] = print;
            }
            dirty = true;
            return placed;
        }

        internal double Shelter, ShelteredLeft, ShelteredRight;

        internal void MeltAndSettle(double seconds, double melt = 1)
        {
            if (seconds <= 0) return;
            if (total <= 0.00001)
            {
                imprints.Clear();
                return;
            }
            for (int cell = 0; cell < Snow.Length; cell++)
            {
                double depth = Snow[cell];
                if (depth <= 0) continue;
                crust[cell] = Math.Min(1, crust[cell] + seconds * (0.017 + depth * 0.00045));
                double surplus = Math.Max(0, depth - 17);
                double rate = 0.008 + depth * 0.0013 + surplus * surplus * 0.00022;
                Remove(cell, Math.Min(depth, rate * seconds * melt));
                RelaxPair(cell);
            }
            for (int i = imprints.Count - 1; i >= 0; i--)
            {
                Imprint print = imprints[i];
                print.Age += seconds;
                print.Depth = Math.Max(0, print.Depth - seconds * 0.009);
                if (print.Depth < 0.08 || print.Age >= 65 || !IsVisibleAt(X + print.Offset) ||
                    Snow[IndexAt(X + print.Offset)] < 1.2) imprints.RemoveAt(i);
                else imprints[i] = print;
            }
            total = Math.Max(0, total);
        }

        internal void Draw(Canvas canvas, Pen edge)
        {
            if (dirty)
            {
                UpdateOutline();
                if (powder != null) UpdateCache(edge);
            }
            if (powder != null) canvas.DrawSnowLayer(cachedSnow, cacheX, cacheY);
        }

        private void UpdateCache(Pen edge)
        {
            cacheX = (int)Math.Floor(X) - 2;
            cacheY = (int)Math.Floor(Y - MaxDepth) - 3;
            int width = (int)Math.Ceiling(Width) + 5;
            int height = (int)Math.Ceiling(MaxDepth + radius + ContactDepth) + 7;
            if (cachedSnow == null || cachedSnow.Width != width || cachedSnow.Height != height)
            {
                if (cacheGraphics != null) cacheGraphics.Dispose();
                if (cachedSnow != null) cachedSnow.Dispose();
                cachedSnow = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                cacheGraphics = Graphics.FromImage(cachedSnow);
                cacheGraphics.SmoothingMode = SmoothingMode.AntiAlias;
                cacheGraphics.PixelOffsetMode = PixelOffsetMode.Half;
            }
            cacheGraphics.ResetTransform();
            cacheGraphics.Clear(Color.Transparent);
            cacheGraphics.TranslateTransform(-cacheX, -cacheY);
            cacheGraphics.DrawPath(edge, outline);
            cacheGraphics.FillPath(powder, outline);
            if (flecks.PointCount != 0) cacheGraphics.FillPath(GrainShade, flecks);
            DrawImprints();
            cacheGraphics.Flush(FlushIntention.Sync);
        }

        private void DrawImprints()
        {
            if (imprints.Count == 0) return;
            GraphicsState state = cacheGraphics.Save();
            try
            {
                cacheGraphics.SetClip(outline, CombineMode.Intersect);
                using (var shade = new SolidBrush(Color.Transparent))
                using (var rim = new SolidBrush(Color.Transparent))
                {
                    foreach (Imprint print in imprints)
                    {
                        double x = X + print.Offset;
                        int cell = IndexAt(x);
                        if (!IsVisibleAt(x) || Snow[cell] < 1.2) continue;
                        float center = (float)x, radius = (float)print.Radius;
                        float height = (float)(Math.Min(1, print.Depth) * (print.Trunk ? 2.65 : 2.15));
                        float y = (float)SurfaceAt(x) + 1.15f;
                        int opacity = (int)(Math.Min(1, print.Depth) * 152);
                        shade.Color = Color.FromArgb(opacity, 80, 113, 140);
                        rim.Color = Color.FromArgb(opacity, 255, 255, 255);
                        if (print.Trunk)
                        {
                            cacheGraphics.FillEllipse(rim, center - radius * 0.78f, y + 0.72f, radius * 1.56f, height + 0.5f);
                            cacheGraphics.FillEllipse(shade, center - radius * 0.63f, y, radius * 1.26f, height);
                        }
                        else
                        {
                            cacheGraphics.FillEllipse(rim, center - radius * 0.79f, y + 0.65f, radius * 1.58f, height + 0.35f);
                            cacheGraphics.FillEllipse(shade, center - radius * 0.76f, y + 0.22f, radius * 0.57f, height * 0.82f);
                            cacheGraphics.FillEllipse(shade, center - radius * 0.07f, y, radius * 0.84f, height);
                        }
                    }
                }
            }
            finally { cacheGraphics.Restore(state); }
        }

        private void UpdateOutline()
        {
            dirty = false;
            outline.Reset();
            flecks.Reset();
            if (powder != null) powder.Dispose();
            powder = null;
            if (total <= 0.00001) return;

            int cell = 0;
            while (cell < Snow.Length)
            {
                if (!DrawsAt(cell))
                {
                    cell++;
                    continue;
                }
                int first = cell;
                int count = 1;
                double left = CellLeft(first);
                crest[0] = PointOnSupport(left);
                do
                {
                    double center = CellCenter(cell);
                    double depth = ProfileDepth(cell);
                    double crown = SupportAt(center) - depth;
                    crest[count++] = new PointF((float)center, (float)crown);
                    if (depth >= 4.6) AddGrainTexture(cell, center, crown, depth);
                    cell++;
                }
                while (DrawsAt(cell));
                double right = CellRight(cell - 1);
                crest[count] = PointOnSupport(right);
                outline.StartFigure();
                TraceCrest(count);
                TraceSupport(left, right);
                outline.CloseFigure();
            }
            if (outline.PointCount == 0) return;
            RectangleF bounds = outline.GetBounds();
            if (bounds.Height < 0.5f) bounds.Height = 0.5f;
            powder = new LinearGradientBrush(bounds, Color.White, Color.White, LinearGradientMode.Vertical)
            {
                InterpolationColors = new ColorBlend
                {
                    Colors = new[]
                    {
                        Color.FromArgb(248, 251, 254, 255),
                        Color.FromArgb(246, 234, 245, 252),
                        Color.FromArgb(239, 210, 226, 240)
                    },
                    Positions = new[] { 0f, 0.65f, 1f }
                }
            };
        }

        private void AddGrainTexture(int cell, double x, double crown, double depth)
        {
            uint hash = unchecked((uint)(cell + 1) * 2654435761u);
            if ((hash & 3) == 0) return;
            float speck = 0.55f + ((hash >> 8) & 7) * 0.09f;
            double offset = (((hash >> 16) & 7) - 3.5) * 0.5;
            double y = crown + 1.7 + ((hash >> 25) & 7) * Math.Min(0.7, depth * 0.055);
            flecks.AddEllipse((float)(x + offset), (float)y, speck, speck * 0.76f);
        }

        private void TraceCrest(int segments)
        {
            bezierControls[0] = crest[0];
            for (int segment = 0; segment < segments; segment++)
            {
                PointF start = crest[segment], end = crest[segment + 1];
                float previous = crest[Math.Max(0, segment - 1)].Y;
                float following = crest[Math.Min(segments, segment + 2)].Y;
                float width = (end.X - start.X) / 3;
                float low = Math.Min(start.Y, end.Y), high = Math.Max(start.Y, end.Y);
                int index = segment * 3 + 1;
                bezierControls[index] = new PointF(start.X + width,
                    Math.Max(low, Math.Min(high, start.Y + (end.Y - previous) * 0.17f)));
                bezierControls[index + 1] = new PointF(end.X - width,
                    Math.Max(low, Math.Min(high, end.Y - (following - start.Y) * 0.17f)));
                bezierControls[index + 2] = end;
            }
            // AddBezier avoids allocating a shortened array for each visible run.
            for (int segment = 0; segment < segments; segment++)
            {
                int index = segment * 3;
                outline.AddBezier(bezierControls[index], bezierControls[index + 1],
                    bezierControls[index + 2], bezierControls[index + 3]);
            }
        }

        private void TraceSupport(double left, double right)
        {
            if (radius <= 0) return;
            double x = right;
            while (x > left)
            {
                double next = x - 1.5;
                if (x > windowRight - radius)
                    next = Math.Max(next, windowRight - radius);
                else if (x > windowLeft + radius)
                    next = windowLeft + radius;
                next = Math.Max(left, next);
                outline.AddLine(PointOnSupport(x), PointOnSupport(next));
                x = next;
            }
        }

        private PointF PointOnSupport(double x)
        {
            return new PointF((float)x, (float)SupportAt(x));
        }

        private double ProfileDepth(int cell)
        {
            double depth = Snow[cell] * 0.72;
            depth += DrawsAt(cell - 1) ? Snow[cell - 1] * 0.14 : Snow[cell] * 0.10;
            depth += DrawsAt(cell + 1) ? Snow[cell + 1] * 0.14 : Snow[cell] * 0.10;
            if (radius <= 0) return depth;
            double center = CellCenter(cell);
            double edge = Math.Max(0, Math.Min(center - windowLeft, windowRight - center));
            return depth * (0.45 + 0.55 * Math.Min(1, edge / (radius + Cell)));
        }

        private bool DrawsAt(int cell)
        {
            return cell >= 0 && cell < Snow.Length && Visible[cell] && Snow[cell] >= SmallestVisibleDepth;
        }

        public void Dispose()
        {
            if (powder != null) powder.Dispose();
            if (cacheGraphics != null) cacheGraphics.Dispose();
            if (cachedSnow != null) cachedSnow.Dispose();
            outline.Dispose();
            flecks.Dispose();
        }
    }
}
