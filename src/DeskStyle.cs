using System;
using System.Collections.Generic;
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
        internal static readonly Color Caption = Color.FromArgb(40, 21, 34);
        internal static readonly Color Hover = Color.FromArgb(69, 37, 55);
        internal static readonly Bitmap Chevron = CreateChevron();
        private static readonly Bitmap SwitchOff = CreateSwitch(false);
        private static readonly Bitmap SwitchOn = CreateSwitch(true);

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

        internal static DeskSelect Select(string label) { return new DeskSelect(label); }

        internal static Button Button(string text, bool primary)
        {
            var button = new Button { Text = text, Width = 118, Height = 34 };
            StyleButton(button, primary);
            return button;
        }

        internal static void StyleButton(Button button, bool primary)
        {
            button.Margin = Padding.Empty;
            button.UseVisualStyleBackColor = false;
            button.UseCompatibleTextRendering = true;
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = primary ? Accent : Input;
            button.ForeColor = primary ? Background : Text;
            button.FlatAppearance.BorderSize = primary ? 0 : 1;
            button.FlatAppearance.BorderColor = Border;
            button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(255, 205, 224) : Hover;
            button.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(235, 149, 183) : Border;
            button.Resize += (sender, e) => RoundButton(button);
            RoundButton(button);
        }

        internal static CheckBox Toggle(string title)
        {
            var check = new CheckBox
            {
                Text = title, Appearance = Appearance.Button, AccessibleRole = AccessibleRole.CheckButton,
                AutoSize = true, MinimumSize = new Size(0, 25), ForeColor = Text, BackColor = Surface,
                FlatStyle = FlatStyle.Flat, UseCompatibleTextRendering = true, UseVisualStyleBackColor = false,
                Image = SwitchOff, ImageAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleLeft, TextImageRelation = TextImageRelation.ImageBeforeText,
                Anchor = AnchorStyles.Left, Margin = new Padding(0, 1, 0, 1)
            };
            check.FlatAppearance.BorderSize = 0;
            check.FlatAppearance.CheckedBackColor = Surface;
            check.FlatAppearance.MouseOverBackColor = Input;
            check.FlatAppearance.MouseDownBackColor = Hover;
            check.CheckedChanged += (sender, e) => check.Image = check.Checked ? SwitchOn : SwitchOff;
            return check;
        }

        private static Bitmap CreateSwitch(bool enabled)
        {
            var glyph = new Bitmap(28, 16);
            using (Graphics graphics = Graphics.FromImage(glyph))
            using (GraphicsPath track = Rounded(new Rectangle(0, 1, 24, 13), 6))
            using (var fill = new SolidBrush(enabled ? Accent : Input))
            using (var rim = new Pen(enabled ? Accent : Border))
            using (var thumb = new SolidBrush(enabled ? Background : Muted))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.FillPath(fill, track);
                graphics.DrawPath(rim, track);
                graphics.FillEllipse(thumb, enabled ? 13 : 3, 4, 8, 8);
            }
            return glyph;
        }

        private static Bitmap CreateChevron()
        {
            var glyph = new Bitmap(12, 12);
            using (Graphics graphics = Graphics.FromImage(glyph))
            using (var pen = new Pen(Muted, 1.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.DrawLines(pen, new[] { new Point(2, 4), new Point(6, 8), new Point(10, 4) });
            }
            return glyph;
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

    internal sealed class DeskSelect : Button
    {
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        internal readonly List<object> Items = new List<object>();
        internal event EventHandler SelectedIndexChanged;
        private int selectedIndex = -1;

        internal DeskSelect(string label)
        {
            DeskStyle.StyleButton(this, false);
            AccessibleName = label;
            AccessibleRole = AccessibleRole.ComboBox;
            Dock = DockStyle.Top;
            Height = 32;
            Padding = new Padding(9, 0, 9, 0);
            UseMnemonic = false;
            AutoEllipsis = true;
            Image = DeskStyle.Chevron;
            TextAlign = ContentAlignment.MiddleLeft;
            ImageAlign = ContentAlignment.MiddleRight;
            TextImageRelation = TextImageRelation.TextBeforeImage;
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = true;
            menu.Renderer = new ToolStripProfessionalRenderer(new DeskMenuColors());
        }

        internal int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                if (value < -1 || value >= Items.Count) throw new ArgumentOutOfRangeException("value");
                if (value == selectedIndex) return;
                selectedIndex = value;
                Text = value < 0 ? string.Empty : Items[value].ToString();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        internal object SelectedItem
        {
            get { return selectedIndex < 0 ? null : Items[selectedIndex]; }
            set { SelectedIndex = Items.IndexOf(value); }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (Items.Count == 0 || menu.Visible) return;
            while (menu.Items.Count != 0)
            {
                ToolStripItem previous = menu.Items[0];
                menu.Items.RemoveAt(0);
                previous.Dispose();
            }
            menu.Font = Font;
            menu.MinimumSize = new Size(Width, 0);
            for (int index = 0; index < Items.Count; index++)
            {
                int optionIndex = index;
                var item = new ToolStripMenuItem(Items[index].ToString())
                {
                    Checked = index == SelectedIndex, ForeColor = DeskStyle.Text,
                    AutoSize = false, Size = new Size(Math.Max(120, Width - 4), Math.Max(27, Font.Height + 9))
                };
                item.Click += (sender, args) => SelectedIndex = optionIndex;
                menu.Items.Add(item);
            }

            Form owner = FindForm();
            bool below = owner == null || PointToScreen(new Point(0, Height)).Y +
                menu.GetPreferredSize(Size.Empty).Height <= owner.RectangleToScreen(owner.ClientRectangle).Bottom;
            menu.Show(this, new Point(0, below ? Height : 0),
                below ? ToolStripDropDownDirection.BelowRight : ToolStripDropDownDirection.AboveRight);
            if (SelectedIndex >= 0) menu.Items[SelectedIndex].Select();
        }

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (keyData == Keys.F4 || keyData == (Keys.Alt | Keys.Down))
            {
                PerformClick();
                return true;
            }
            if (Items.Count > 0 && (keyData == Keys.Up || keyData == Keys.Down))
            {
                SelectedIndex = Math.Max(0, Math.Min(Items.Count - 1, SelectedIndex + (keyData == Keys.Up ? -1 : 1)));
                return true;
            }
            return base.ProcessCmdKey(ref message, keyData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) menu.Dispose();
            base.Dispose(disposing);
        }
    }

    internal sealed class DeskMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return DeskStyle.Surface; } }
        public override Color MenuBorder { get { return DeskStyle.Border; } }
        public override Color MenuItemBorder { get { return DeskStyle.Border; } }
        public override Color MenuItemSelected { get { return DeskStyle.Hover; } }
        public override Color MenuItemSelectedGradientBegin { get { return DeskStyle.Hover; } }
        public override Color MenuItemSelectedGradientEnd { get { return DeskStyle.Hover; } }
        public override Color ImageMarginGradientBegin { get { return DeskStyle.Surface; } }
        public override Color ImageMarginGradientMiddle { get { return DeskStyle.Surface; } }
        public override Color ImageMarginGradientEnd { get { return DeskStyle.Surface; } }
        public override Color ButtonSelectedBorder { get { return DeskStyle.Accent; } }
        public override Color ButtonPressedBorder { get { return DeskStyle.Border; } }
        public override Color CheckBackground { get { return DeskStyle.Accent; } }
        public override Color CheckSelectedBackground { get { return DeskStyle.Accent; } }
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
