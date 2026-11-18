using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SnowDesk
{
    internal static class DeskStyle
    {
        internal static readonly Color Background = Color.FromArgb(20, 12, 19);
        internal static readonly Color Surface = Color.FromArgb(32, 18, 29);
        internal static readonly Color Input = Color.FromArgb(53, 29, 44);
        internal static readonly Color Border = Color.FromArgb(91, 53, 72);
        internal static readonly Color Text = Color.FromArgb(250, 235, 241);
        internal static readonly Color Muted = Color.FromArgb(216, 176, 195);
        internal static readonly Color Accent = Color.FromArgb(249, 179, 204);

        internal static TableLayoutPanel Flow(int columns)
        {
            var layout = new TableLayoutPanel
            {
                ColumnCount = columns, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top, Margin = Padding.Empty
            };
            for (int index = 0; index < columns; index++)
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            return layout;
        }

        internal static Label Title(string text, float size)
        {
            return new Label
            {
                Text = text, AutoSize = true, UseCompatibleTextRendering = true, ForeColor = Text,
                Margin = Padding.Empty, Font = new Font("Segoe UI", size, FontStyle.Bold)
            };
        }

        internal static Label Copy(string text)
        {
            return new Label
            {
                Text = text, AutoSize = true, UseCompatibleTextRendering = true, ForeColor = Muted,
                Margin = new Padding(0, 2, 0, 2)
            };
        }

        internal static ComboBox Select(string label) { return new DeskSelect(label); }

        internal static Button Button(string text, bool primary)
        {
            var button = new Button
            {
                Text = text, Width = 118, Height = 32, Margin = Padding.Empty,
                UseVisualStyleBackColor = false, UseCompatibleTextRendering = true,
                FlatStyle = FlatStyle.Flat, BackColor = primary ? Accent : Input,
                ForeColor = primary ? Background : Text
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(255, 205, 224) : Border;
            button.Resize += (sender, e) => RoundButton(button);
            RoundButton(button);
            return button;
        }

        private static void RoundButton(Button button)
        {
            int radius = Math.Min(8, Math.Min(button.Width, button.Height) / 2);
            if (radius == 0) return;
            using (GraphicsPath shape = Rounded(button.ClientRectangle, radius))
                button.Region = new Region(shape);
        }

        internal static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            int size = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, size, size, 180, 90);
            path.AddArc(bounds.Right - size, bounds.Top, size, size, 270, 90);
            path.AddArc(bounds.Right - size, bounds.Bottom - size, size, size, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - size, size, size, 90, 90);
            path.CloseFigure();
            return path;
        }

        internal static DeskCard Card(string title, int height)
        {
            var card = new DeskCard(false) { Height = height, Dock = DockStyle.Top };
            var content = Flow(1);
            content.BackColor = Surface;
            Label heading = Title(title, 13);
            heading.Margin = new Padding(0, 0, 0, 10);
            content.Controls.Add(heading);
            card.Controls.Add(content);
            return card;
        }
    }

    internal sealed class DeskSelect : ComboBox
    {
        internal DeskSelect(string label)
        {
            AccessibleName = label;
            DropDownStyle = ComboBoxStyle.DropDownList;
            BackColor = DeskStyle.Input;
            ForeColor = DeskStyle.Text;
            Dock = DockStyle.Top;
            FlatStyle = FlatStyle.Flat;
            IntegralHeight = false;
            DropDownHeight = 196;
            DrawMode = DrawMode.OwnerDrawFixed;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            bool active = (e.State & DrawItemState.Selected) != 0;
            using (var fill = new SolidBrush(active ? DeskStyle.Border : BackColor))
                e.Graphics.FillRectangle(fill, e.Bounds);
            if (e.Index >= 0)
            {
                Rectangle text = e.Bounds;
                text.Inflate(-5, 0);
                TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, text, ForeColor,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                    TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform);
            }
            e.DrawFocusRectangle();
        }
    }

    internal sealed class DeskCard : Panel
    {
        private readonly bool hero;

        internal DeskCard(bool hero)
        {
            this.hero = hero;
            BackColor = DeskStyle.Background;
            Padding = new Padding(17, 13, 17, 13);
            Margin = new Padding(0, 0, 0, 12);
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            if (Width < 44 || Height < 44) return;
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(1, 1, Width - 3, Height - 3);
            using (GraphicsPath outline = DeskStyle.Rounded(bounds, hero ? 20 : 17))
            using (var border = new Pen(DeskStyle.Border))
            {
                if (hero)
                {
                    using (var fill = new LinearGradientBrush(bounds, DeskStyle.Surface,
                        Color.FromArgb(77, 36, 58), 14f))
                        graphics.FillPath(fill, outline);
                }
                else
                {
                    using (var fill = new SolidBrush(DeskStyle.Surface))
                        graphics.FillPath(fill, outline);
                }
                graphics.DrawPath(border, outline);
            }
        }

    }

    internal sealed class DeskThemePreview : Control
    {
        private readonly Timer previewClock = new Timer { Interval = 40 };
        private ThemePreview preview;
        private DesktopTheme previewTheme;
        private Settings previewSettings;
        private bool previewPlaying;

        internal DeskThemePreview()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            Dock = DockStyle.Fill;
            Margin = new Padding(4, 0, 10, 0);
            TabStop = false;
            previewClock.Tick += AdvancePreview;
        }

        internal void SetPreview(Settings settings, bool playing)
        {
            previewSettings = settings;
            DesktopTheme theme = settings.Theme;
            if (theme != previewTheme)
            {
                previewClock.Stop();
                if (preview != null) preview.Dispose();
                preview = theme.CreatePreview == null ? null : theme.CreatePreview();
                previewTheme = theme;
                Invalidate();
            }
            previewPlaying = playing;
            UpdatePreviewPlayback();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            UpdatePreviewPlayback();
        }

        private void UpdatePreviewPlayback()
        {
            if (previewClock == null) return;
            previewClock.Enabled = Visible && previewPlaying && preview != null &&
                ThemePreview.WindowsAllowsAnimation();
        }

        private void AdvancePreview(object sender, EventArgs e)
        {
            if (!ThemePreview.WindowsAllowsAnimation())
            {
                previewClock.Stop();
                return;
            }
            preview.Advance(previewClock.Interval / 1000.0, previewSettings);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 24 || Height < 24) return;
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (preview == null)
            {
                DrawConstellation(graphics, ClientRectangle);
                return;
            }
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath shape = DeskStyle.Rounded(bounds, 12))
            {
                GraphicsState previous = graphics.Save();
                graphics.SetClip(shape, CombineMode.Intersect);
                preview.Draw(graphics, bounds);
                graphics.Restore(previous);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                previewPlaying = false;
                previewClock.Dispose();
                if (preview != null) preview.Dispose();
            }
            base.Dispose(disposing);
        }

        private static void DrawConstellation(Graphics graphics, Rectangle bounds)
        {
            float x = bounds.Width * 0.63f, y = bounds.Height * 0.48f;
            using (var orbit = new Pen(Color.FromArgb(71, DeskStyle.Accent), 1f))
            using (var starlight = new SolidBrush(Color.FromArgb(155, DeskStyle.Accent)))
            using (var dust = new SolidBrush(Color.FromArgb(90, DeskStyle.Accent)))
            {
                GraphicsState previous = graphics.Save();
                graphics.TranslateTransform(x, y);
                graphics.RotateTransform(-16);
                graphics.DrawEllipse(orbit, -112, -33, 224, 66);
                graphics.DrawEllipse(orbit, -150, -51, 300, 102);
                graphics.Restore(previous);

                var random = new Random(56);
                for (int i = 0; i < 34; i++)
                {
                    float px = bounds.Width * 0.46f + (float)random.NextDouble() * bounds.Width * 0.54f;
                    float py = 8 + (float)random.NextDouble() * (bounds.Height - 16);
                    float size = i % 7 == 0 ? 2.8f : 1.3f;
                    graphics.FillEllipse(dust, px, py, size, size);
                }
                DrawStar(graphics, starlight, x, y, 22);
                DrawStar(graphics, starlight, x - 79, y + 28, 8);
                DrawStar(graphics, starlight, x + 77, y - 29, 11);
            }
        }

        private static void DrawStar(Graphics graphics, Brush brush, float x, float y, float radius)
        {
            float inner = radius * 0.20f;
            graphics.FillPolygon(brush, new[]
            {
                new PointF(x, y - radius), new PointF(x + inner, y - inner),
                new PointF(x + radius * 0.72f, y), new PointF(x + inner, y + inner),
                new PointF(x, y + radius), new PointF(x - inner, y + inner),
                new PointF(x - radius * 0.72f, y), new PointF(x - inner, y - inner)
            });
        }

    }
}
