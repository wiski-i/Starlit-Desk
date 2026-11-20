using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace SnowDesk
{
    /// <summary>
    /// Keeps finished vector drawings as bitmaps, keyed by their look (kind, growth step, sway step...). A cached
    /// look is copied with a plain blit instead of being filled, stroked and antialiased again every frame.
    /// The cache empties itself when it grows past its limit.
    /// </summary>
    internal sealed class ArtCache : IDisposable
    {
        private sealed class Entry
        {
            internal Bitmap Image;
            internal float OriginX, OriginY;
        }

        private readonly Dictionary<long, Entry> entries = new Dictionary<long, Entry>();
        private readonly int limit;

        internal ArtCache(int limit)
        {
            this.limit = limit;
        }

        /// <summary>
        /// Draws the look for <paramref name="key"/> with its art origin at (x, y). <paramref name="art"/> is the
        /// drawing's extent in art units around its origin; <paramref name="paint"/> renders it in art units.
        /// </summary>
        internal void Draw(Canvas canvas, long key, double x, double y, RectangleF art, float scale, bool mirror,
            Action<Graphics> paint)
        {
            Entry entry;
            if (!entries.TryGetValue(key, out entry))
            {
                if (entries.Count >= limit) Clear();
                entry = Render(art, scale, mirror, paint);
                entries.Add(key, entry);
            }
            canvas.Blit(entry.Image, x - entry.OriginX, y - entry.OriginY);
        }

        /// <summary>The same as the canvas overload, for code that already holds the frame's Graphics.</summary>
        internal void Draw(Graphics g, long key, double x, double y, RectangleF art, float scale, bool mirror,
            Action<Graphics> paint)
        {
            Entry entry;
            if (!entries.TryGetValue(key, out entry))
            {
                if (entries.Count >= limit) Clear();
                entry = Render(art, scale, mirror, paint);
                entries.Add(key, entry);
            }
            PixelOffsetMode offset = g.PixelOffsetMode;
            InterpolationMode filter = g.InterpolationMode;
            g.PixelOffsetMode = PixelOffsetMode.None;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImage(entry.Image, (int)Math.Round(x - entry.OriginX), (int)Math.Round(y - entry.OriginY),
                entry.Image.Width, entry.Image.Height);
            g.PixelOffsetMode = offset;
            g.InterpolationMode = filter;
        }

        private static Entry Render(RectangleF art, float scale, bool mirror, Action<Graphics> paint)
        {
            int width = (int)Math.Ceiling(art.Width * scale) + 4, height = (int)Math.Ceiling(art.Height * scale) + 4;
            var entry = new Entry
            {
                Image = new Bitmap(Math.Max(1, width), Math.Max(1, height), PixelFormat.Format32bppPArgb),
                OriginX = (mirror ? art.Right : -art.Left) * scale + 2,
                OriginY = -art.Top * scale + 2
            };
            using (Graphics g = Graphics.FromImage(entry.Image))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.TranslateTransform(entry.OriginX, entry.OriginY);
                g.ScaleTransform(mirror ? -scale : scale, scale);
                paint(g);
            }
            return entry;
        }

        internal void Clear()
        {
            foreach (Entry entry in entries.Values) entry.Image.Dispose();
            entries.Clear();
        }

        public void Dispose() { Clear(); }
    }
}
