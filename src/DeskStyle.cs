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
                Dock = DockStyle.Top, Margin = Padding.Empty, BackColor = Color.Transparent
            };
            for (int index = 0; index < columns; index++)
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            return layout;
        }

        internal static Label Title(string text, float size)
        {
            return new Label
            {
                Text = text, AutoSize = true, BackColor = Color.Transparent, ForeColor = Text,
                Margin = Padding.Empty, Font = new Font("Segoe UI", size, FontStyle.Bold)
            };
        }

        internal static Label Copy(string text)
        {
            return new Label
            {
                Text = text, AutoSize = true, BackColor = Color.Transparent, ForeColor = Muted,
                Margin = new Padding(0, 2, 0, 2)
            };
        }

        internal static ComboBox Select(string label) { return new DeskSelect(label); }

        internal static Button Button(string text, bool primary)
        {
            var button = new Button
            {
                Text = text, Width = 118, Height = 32, UseVisualStyleBackColor = false,
                FlatStyle = FlatStyle.Flat, BackColor = primary ? Accent : Input,
                ForeColor = primary ? Background : Text, Margin = Padding.Empty
            };
            button.FlatAppearance.BorderColor = primary ? Accent : Border;
            button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(255, 205, 224) : Border;
            return button;
        }

        internal static DeskCard Card(string title, int height)
        {
            var card = new DeskCard(false) { Height = height, Dock = DockStyle.Top };
            var content = Flow(1);
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
            if (e.Index >= 0) DrawText(e.Graphics, GetItemText(Items[e.Index]), e.Bounds);
            e.DrawFocusRectangle();
        }

        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);
            const int Paint = 0x000F, Print = 0x0317, PrintClient = 0x0318;
            if (message.Msg == Paint)
            {
                using (Graphics graphics = Graphics.FromHwnd(Handle)) DrawField(graphics);
            }
            else if ((message.Msg == Print || message.Msg == PrintClient) && message.WParam != IntPtr.Zero)
            {
                using (Graphics graphics = Graphics.FromHdc(message.WParam)) DrawField(graphics);
            }
        }

        private void DrawField(Graphics graphics)
        {
            Rectangle bounds = ClientRectangle;
            if (bounds.Width < 32 || bounds.Height < 4) return;
            int arrowWidth = Math.Max(20, bounds.Height);
            using (var fill = new SolidBrush(BackColor))
            using (var edge = new Pen(Focused ? DeskStyle.Accent : DeskStyle.Border))
            using (var arrow = new Pen(DeskStyle.Muted, 1.5f))
            {
                graphics.FillRectangle(fill, bounds);
                graphics.DrawRectangle(edge, 0, 0, bounds.Width - 1, bounds.Height - 1);
                graphics.DrawLine(edge, bounds.Right - arrowWidth, 2, bounds.Right - arrowWidth, bounds.Bottom - 3);
                int x = bounds.Right - arrowWidth / 2, y = bounds.Height / 2;
                graphics.DrawLines(arrow, new[] { new Point(x - 4, y - 2), new Point(x, y + 2), new Point(x + 4, y - 2) });
            }

            bounds.Width -= arrowWidth;
            if (SelectedItem != null) DrawText(graphics, GetItemText(SelectedItem), bounds);
        }

        private void DrawText(Graphics graphics, string value, Rectangle bounds)
        {
            bounds.Inflate(-7, 0);
            TextRenderer.DrawText(graphics, value, Font, bounds, ForeColor,
                TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal sealed class DeskCheckBox : CheckBox
    {
        private const TextFormatFlags TextFlags = TextFormatFlags.SingleLine | TextFormatFlags.NoPadding |
            TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter;
        private int BoxSize { get { return Math.Max(14, Font.Height - 3); } }

        public override Size GetPreferredSize(Size proposedSize)
        {
            Size text = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFlags);
            return new Size(BoxSize + 8 + text.Width, Math.Max(Font.Height + 6, BoxSize + 4));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            int size = BoxSize;
            var square = new Rectangle(1, (Height - size) / 2, size, size);
            using (var fill = new SolidBrush(Checked ? DeskStyle.Accent : DeskStyle.Input))
            using (var border = new Pen(Checked ? DeskStyle.Accent : DeskStyle.Muted))
            {
                graphics.FillRectangle(fill, square);
                graphics.DrawRectangle(border, square);
            }
            if (Checked)
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var tick = new Pen(DeskStyle.Background, 1.8f))
                {
                    int middle = square.Top + size / 2;
                    graphics.DrawLines(tick, new[] { new Point(4, middle), new Point(7, middle + 3),
                        new Point(size - 2, middle - 3) });
                }
            }

            var textBounds = new Rectangle(size + 6, 0, Math.Max(0, Width - size - 6), Height);
            TextRenderer.DrawText(graphics, Text, Font, textBounds, ForeColor, TextFlags | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(graphics, ClientRectangle, DeskStyle.Accent, DeskStyle.Surface);
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            base.OnCheckedChanged(e);
            Invalidate();
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
            if (Width < 28 || Height < 28) return;
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(1, 1, Width - 3, Height - 3);
            using (GraphicsPath outline = Rounded(bounds, 13))
            using (var border = new Pen(DeskStyle.Border))
            {
                if (hero)
                {
                    using (var fill = new LinearGradientBrush(bounds, DeskStyle.Surface,
                        Color.FromArgb(77, 36, 58), 14f))
                        graphics.FillPath(fill, outline);
                    GraphicsState previous = graphics.Save();
                    graphics.SetClip(outline);
                    DrawConstellation(graphics, bounds);
                    graphics.Restore(previous);
                }
                else
                {
                    using (var fill = new SolidBrush(DeskStyle.Surface))
                        graphics.FillPath(fill, outline);
                }
                graphics.DrawPath(border, outline);
            }
        }

        private static void DrawConstellation(Graphics graphics, Rectangle bounds)
        {
            float x = bounds.Width * 0.73f, y = bounds.Height * 0.48f;
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

        private static GraphicsPath Rounded(Rectangle bounds, int radius)
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
    }
}
