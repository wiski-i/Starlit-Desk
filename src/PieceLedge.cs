using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Rect = System.Windows.Rect;

namespace SnowDesk
{
    /// <summary>
    /// Leaves or petals lying on a window top or on the ground. Pieces keep their offset from the window, so
    /// they travel with it; shaking, the cursor or a gust lifts them off again. Drawing goes through a cached
    /// bitmap: a new piece is painted on top incrementally and only removals trigger a full repaint.
    /// </summary>
    internal sealed class PieceLedge : IDisposable
    {
        internal const int Cell = 10;
        private const int Pad = 12;

        /// <summary>One resting piece; Offset is from the ledge's left edge, Lift is its height above the ledge.</summary>
        internal struct Piece
        {
            internal float Offset, Lift, Angle, Size;
            internal byte Kind, Tint;
        }

        internal delegate void Painter(Graphics g, Piece piece, float x, float y);

        private readonly List<Piece> pieces = new List<Piece>();
        private readonly Painter paint;
        private readonly float maxHeight;
        private readonly int perCell;
        private float[] height = new float[0];
        private Bitmap cache;
        private Graphics cacheGraphics;
        private bool dirty = true;

        internal readonly IntPtr Handle;
        internal Rect Window { get; private set; }
        internal double X { get; private set; }
        internal double Y { get; private set; }
        internal double Width { get; private set; }
        internal bool[] Visible { get; private set; }
        internal int Count { get { return pieces.Count; } }

        internal PieceLedge(IntPtr handle, Painter paint, float maxHeight, int perCell)
        {
            Handle = handle;
            this.paint = paint;
            this.maxHeight = maxHeight;
            this.perCell = perCell;
            Visible = new bool[0];
        }

        internal int IndexAt(double x) { return (int)Math.Floor((x - X) / Cell); }
        internal double CellCenter(int cell) { return X + Math.Min(Width - 0.5, cell * Cell + Cell * 0.5); }

        internal bool VisibleAt(double x)
        {
            int cell = IndexAt(x);
            return cell >= 0 && cell < Visible.Length && x < X + Width && Visible[cell];
        }

        /// <summary>Top of the pile at x, which is where a falling piece settles.</summary>
        internal double SurfaceAt(double x)
        {
            int cell = Math.Max(0, Math.Min(height.Length - 1, IndexAt(x)));
            return height.Length == 0 ? Y : Y - height[cell];
        }

        internal void Place(Rect window, double x, double y, double width)
        {
            int cells = Math.Max(1, (int)Math.Ceiling(width / Cell));
            bool resized = cells != height.Length || Math.Abs(width - Width) > 0.5;
            Window = window;
            X = x;
            Y = y;
            Width = width;
            if (!resized) return;
            Array.Resize(ref height, cells);
            Visible = new bool[cells];
            if (pieces.RemoveAll(piece => piece.Offset >= width) > 0) Recount();
            DropCache();
        }

        /// <summary>Lays a piece down at x; false when that stretch is hidden behind another window.</summary>
        internal bool Rest(double x, byte kind, byte tint, float size, float angle)
        {
            int cell = IndexAt(x);
            if (cell < 0 || cell >= height.Length || !Visible[cell]) return false;
            var piece = new Piece
            {
                Offset = (float)(x - X), Lift = height[cell] + size * 0.12f, Angle = angle, Size = size, Kind = kind, Tint = tint
            };
            Raise(cell, size);
            pieces.Add(piece);
            if (pieces.Count > height.Length * perCell) pieces.RemoveAt(0); // The oldest, lowest piece stays painted until the next repaint.
            if (!dirty && cacheGraphics != null) paint(cacheGraphics, piece, piece.Offset + Pad, maxHeight + Pad - piece.Lift);
            return true;
        }

        private void Raise(int cell, float size)
        {
            float rise = size * 0.2f;
            height[cell] = Math.Min(maxHeight, height[cell] + rise);
            if (cell > 0) height[cell - 1] = Math.Min(maxHeight, Math.Max(height[cell - 1], height[cell] - rise * 1.5f));
            if (cell + 1 < height.Length) height[cell + 1] = Math.Min(maxHeight, Math.Max(height[cell + 1], height[cell] - rise * 1.5f));
        }

        /// <summary>
        /// Lifts pieces whose offset lies in [left, right) with the given chance (topmost first) and reports each
        /// visible one as (x, y, piece). Hidden pieces leave unseen. Returns how many were lifted.
        /// </summary>
        internal int Lift(double left, double right, double chance, Random random, Action<double, double, Piece> emit)
        {
            int lifted = 0;
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                Piece piece = pieces[i];
                double x = X + piece.Offset;
                if (x < left || x >= right || random.NextDouble() >= chance) continue;
                pieces.RemoveAt(i);
                lifted++;
                int cell = Math.Max(0, Math.Min(Visible.Length - 1, (int)(piece.Offset / Cell)));
                if (emit != null && Visible[cell]) emit(x, Y - piece.Lift, piece);
            }
            if (lifted > 0)
            {
                Recount();
                dirty = true;
            }
            return lifted;
        }

        /// <summary>Number of pieces lying between left and right.</summary>
        internal int CountIn(double left, double right)
        {
            int count = 0;
            foreach (Piece piece in pieces)
            {
                double x = X + piece.Offset;
                if (x >= left && x < right) count++;
            }
            return count;
        }

        private void Recount()
        {
            Array.Clear(height, 0, height.Length);
            foreach (Piece piece in pieces)
            {
                int cell = Math.Max(0, Math.Min(height.Length - 1, (int)(piece.Offset / Cell)));
                Raise(cell, piece.Size);
            }
        }

        internal void Draw(Graphics g)
        {
            if (pieces.Count == 0 || Visible.Length == 0) return;
            if (dirty || cache == null) Repaint();
            GraphicsState state = g.Save();
            try
            {
                // Only the stretches not covered by front windows are shown.
                using (var shown = new Region(Rectangle.Empty))
                {
                    int start = -1;
                    for (int cell = 0; cell <= Visible.Length; cell++)
                    {
                        bool visible = cell < Visible.Length && Visible[cell];
                        if (visible && start < 0) start = cell;
                        if (visible || start < 0) continue;
                        float left = (float)(X + start * Cell), right = (float)Math.Min(X + Width, X + cell * Cell);
                        shown.Union(new RectangleF(left - (start == 0 ? Pad : 0), (float)(Y - maxHeight - Pad),
                            right - left + (start == 0 ? Pad : 0) + (cell == Visible.Length ? Pad : 0), maxHeight + Pad * 2));
                        start = -1;
                    }
                    g.IntersectClip(shown);
                }
                g.DrawImageUnscaled(cache, (int)Math.Round(X) - Pad, (int)Math.Round(Y - maxHeight) - Pad);
            }
            finally { g.Restore(state); }
        }

        private void Repaint()
        {
            int width = (int)Math.Ceiling(Width) + Pad * 2, tall = (int)Math.Ceiling(maxHeight) + Pad * 2;
            if (cache == null || cache.Width != width || cache.Height != tall)
            {
                DropCache();
                cache = new Bitmap(Math.Max(1, width), Math.Max(1, tall), PixelFormat.Format32bppPArgb);
                cacheGraphics = Graphics.FromImage(cache);
                cacheGraphics.SmoothingMode = SmoothingMode.AntiAlias;
                cacheGraphics.PixelOffsetMode = PixelOffsetMode.Half;
            }
            cacheGraphics.Clear(Color.Transparent);
            foreach (Piece piece in pieces) paint(cacheGraphics, piece, piece.Offset + Pad, maxHeight + Pad - piece.Lift);
            dirty = false;
        }

        private void DropCache()
        {
            if (cacheGraphics != null) cacheGraphics.Dispose();
            if (cache != null) cache.Dispose();
            cacheGraphics = null;
            cache = null;
            dirty = true;
        }

        public void Dispose() { DropCache(); }
    }
}
