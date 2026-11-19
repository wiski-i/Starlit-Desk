using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using Rect = System.Windows.Rect;

namespace SnowDesk
{
    /// <summary>
    /// Rain water resting on a window's top edge, kept in cells like <see cref="SnowLedge"/>. Water levels out
    /// between neighbouring cells, holds a small bead at the window ends and spills over them as drips or a
    /// stream. Depths are in pixels; masses (depth × cell width) let falling water land on other ledges.
    /// </summary>
    internal sealed class WaterLedge
    {
        internal const int Cell = 6;
        internal const double MaxDepth = 6.5;
        internal const double DropMass = 2.6;
        private const double Bead = 1.4, SpillRate = 1.1, FlowRate = 10, DryDepth = 0.18;
        private static readonly Pen Crest = new Pen(Color.FromArgb(190, 232, 246, 255), 1.05f) { LineJoin = LineJoin.Round };
        private static readonly Pen Contact = new Pen(Color.FromArgb(70, 22, 46, 70), 1f);
        private static readonly Brush Glint = new SolidBrush(Color.FromArgb(205, 248, 252, 255));
        private readonly List<PointF> crest = new List<PointF>(64);
        private double leftSpill, rightSpill;

        internal readonly IntPtr Handle;
        internal Rect Window { get; private set; }
        internal double X { get; private set; }
        internal double Y { get; private set; }
        internal double Width { get; private set; }
        internal double[] Water { get; private set; }
        internal bool[] Visible { get; private set; }

        internal WaterLedge(IntPtr handle)
        {
            Handle = handle;
            Water = new double[0];
            Visible = new bool[0];
        }

        internal int IndexAt(double x) { return (int)Math.Floor((x - X) / Cell); }
        internal double CellCenter(int cell) { return X + Math.Min(Width - 0.5, cell * Cell + Cell * 0.5); }

        internal bool VisibleAt(double x)
        {
            int cell = IndexAt(x);
            return cell >= 0 && cell < Visible.Length && x < X + Width && Visible[cell];
        }

        internal double Total
        {
            get
            {
                double sum = 0;
                foreach (double depth in Water) sum += depth;
                return sum * Cell;
            }
        }

        /// <summary>Water surface height at x (the window top where the edge is dry).</summary>
        internal double SurfaceAt(double x)
        {
            double position = (x - X) / Cell - 0.5;
            int left = Math.Max(0, Math.Min(Water.Length - 1, (int)Math.Floor(position)));
            int right = Math.Min(Water.Length - 1, left + 1);
            double t = Math.Max(0, Math.Min(1, position - left));
            return Y - (Water[left] * (1 - t) + Water[right] * t);
        }

        /// <summary>Moves the ledge to a new window rectangle; water is resampled when the width changes.</summary>
        internal void Place(Rect window, double x, double y, double width)
        {
            int cells = Math.Max(1, (int)Math.Ceiling(width / Cell));
            if (cells != Water.Length)
            {
                double[] next = new double[cells];
                for (int cell = 0; cell < cells && Water.Length > 0; cell++)
                    next[cell] = Water[Math.Min(Water.Length - 1, cell * Water.Length / cells)];
                Water = next;
                Visible = new bool[cells];
            }
            Window = window;
            X = x;
            Y = y;
            Width = width;
        }

        internal void Add(double x, double mass)
        {
            int cell = Math.Max(0, Math.Min(Water.Length - 1, IndexAt(x)));
            Water[cell] = Math.Min(MaxDepth * 1.4, Water[cell] + mass / Cell);
        }

        /// <summary>Rain only reaches the visible stretch; water under a front window arrives by flowing there.</summary>
        internal void Rain(double depth)
        {
            for (int cell = 0; cell < Water.Length; cell++)
                if (Visible[cell]) Water[cell] += depth;
        }

        internal void Evaporate(double depth)
        {
            for (int cell = 0; cell < Water.Length; cell++) Water[cell] = Math.Max(0, Water[cell] - depth);
        }

        /// <summary>Takes a fraction of every cell away and reports it in chunks as (x, surface y, mass).</summary>
        internal void Release(double fraction, Action<double, double, double> emit)
        {
            double mass = 0, weightedX = 0;
            for (int cell = 0; cell < Water.Length; cell++)
            {
                double taken = Water[cell] * fraction;
                if (taken <= 0) continue;
                Water[cell] -= taken;
                if (!Visible[cell]) continue; // Water behind a front window leaves unseen.
                mass += taken * Cell;
                weightedX += CellCenter(cell) * taken * Cell;
                if (mass >= DropMass * 1.6 || cell == Water.Length - 1)
                {
                    emit(weightedX / mass, Y - Water[cell] - 1, mass);
                    mass = weightedX = 0;
                }
            }
            if (mass > 0.05) emit(weightedX / mass, Y - 1, mass);
        }

        /// <summary>Levels the water and lets it run off the window ends through <paramref name="spill"/>.</summary>
        internal void Flow(double seconds, Action<double, double, double> spill)
        {
            int length = Water.Length;
            if (length == 0) return;
            int steps = Math.Max(1, (int)Math.Ceiling(FlowRate * seconds / 0.4));
            double share = FlowRate * seconds / steps * 0.5;
            for (int step = 0; step < steps; step++)
            {
                for (int cell = 0; cell + 1 < length; cell++)
                {
                    double moved = (Water[cell] - Water[cell + 1]) * share;
                    Water[cell] -= moved;
                    Water[cell + 1] += moved;
                }
            }
            double rate = Math.Min(1, SpillRate * seconds);
            double outLeft = Math.Max(0, Water[0] - Bead) * rate + Math.Max(0, Water[0] - MaxDepth);
            double outRight = Math.Max(0, Water[length - 1] - Bead) * rate + Math.Max(0, Water[length - 1] - MaxDepth);
            Water[0] -= outLeft;
            Water[length - 1] -= outRight;
            // An end hidden behind another window drains out of sight instead of dripping from thin air.
            leftSpill = Visible[0] ? leftSpill + outLeft * Cell : 0;
            rightSpill = Visible[length - 1] ? rightSpill + outRight * Cell : 0;
            while (leftSpill >= DropMass)
            {
                leftSpill -= DropMass;
                spill(X + 0.8, Y - 0.5, DropMass);
            }
            while (rightSpill >= DropMass)
            {
                rightSpill -= DropMass;
                spill(X + Width - 0.8, Y - 0.5, DropMass);
            }
        }

        /// <summary>Draws each visible wet stretch as a smooth body with a bright crest and beaded ends.</summary>
        internal void Draw(Graphics g, Brush body, double clock)
        {
            int start = -1;
            for (int cell = 0; cell <= Water.Length; cell++)
            {
                bool wet = cell < Water.Length && Visible[cell] && Water[cell] > DryDepth;
                if (wet && start < 0) start = cell;
                if (wet || start < 0) continue;
                DrawRun(g, body, start, cell, clock);
                start = -1;
            }
        }

        private void DrawRun(Graphics g, Brush body, int first, int end, double clock)
        {
            float left = (float)(X + first * Cell), right = (float)Math.Min(X + Width, X + end * Cell);
            if (right - left < 2) return;
            float baseY = (float)Y;
            crest.Clear();
            crest.Add(new PointF(left, baseY));
            crest.Add(new PointF(left + Math.Min(2.2f, (right - left) * 0.25f), baseY - (float)Water[first] * 0.82f));
            for (int cell = first; cell < end; cell++)
            {
                double ripple = Math.Sin(clock * 3.1 + cell * 0.9) * Math.Min(0.35, Water[cell] * 0.08);
                crest.Add(new PointF((float)CellCenter(cell), baseY - (float)(Water[cell] + ripple)));
            }
            crest.Add(new PointF(right - Math.Min(2.2f, (right - left) * 0.25f), baseY - (float)Water[end - 1] * 0.82f));
            crest.Add(new PointF(right, baseY));
            PointF[] points = crest.ToArray();
            using (var shape = new GraphicsPath())
            {
                shape.AddCurve(points, 0.45f);
                shape.AddLine(right, baseY + 0.6f, left, baseY + 0.6f);
                shape.CloseFigure();
                g.FillPath(body, shape);
            }
            if (points.Length > 4)
            {
                var top = new PointF[points.Length - 2];
                Array.Copy(points, 1, top, 0, top.Length);
                g.DrawCurve(Crest, top, 0.45f);
            }
            g.DrawLine(Contact, left + 1, baseY + 1, right - 1, baseY + 1);
            for (int cell = first + 2; cell < end - 1; cell += 9)
            {
                if (Water[cell] < 1.2) continue;
                float x = (float)CellCenter(cell), y = baseY - (float)Water[cell] * 0.62f;
                g.FillEllipse(Glint, x - 1.6f, y - 0.5f, 3.2f, 1.1f);
            }
        }
    }
}
