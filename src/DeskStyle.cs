using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SnowDesk
{
    internal static class DeskStyle
    {
        internal static readonly Color Background = Color.FromArgb(25, 15, 24);
        internal static readonly Color Surface = Color.FromArgb(37, 23, 35);
        internal static readonly Color Input = Color.FromArgb(60, 37, 53);
        internal static readonly Color Border = Color.FromArgb(86, 51, 70);
        internal static readonly Color Text = Color.FromArgb(252, 238, 244);
        internal static readonly Color Muted = Color.FromArgb(221, 188, 204);
        internal static readonly Color Accent = Color.FromArgb(244, 181, 204);
        internal static readonly Color Caption = Color.FromArgb(43, 24, 38);
        internal static readonly Color Hover = Color.FromArgb(78, 48, 67);
        internal static readonly Bitmap Chevron = CreateChevron();
        internal static readonly Bitmap MenuTick = CreateMenuTick();
        private static readonly Font ActionFont = new Font("Segoe UI Semibold", 10f);
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
            var button = new Button { Text = text, Width = 122, Height = 36, Font = ActionFont };
            StyleButton(button, primary);
            return button;
        }

        internal static void StyleButton(Button button, bool primary)
        {
            button.Margin = Padding.Empty;
            button.UseVisualStyleBackColor = false;
            button.UseCompatibleTextRendering = true;
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = primary ? Color.Transparent : Surface;
            button.ForeColor = primary ? Background : Text;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = button.BackColor;
            button.FlatAppearance.MouseDownBackColor = button.BackColor;
            new DeskButtonSurface(button, primary);
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
            // A filled hover rectangle clashed with the rounded cards; the switch itself is the cue.
            check.FlatAppearance.MouseOverBackColor = Surface;
            check.FlatAppearance.MouseDownBackColor = Surface;
            check.CheckedChanged += (sender, e) => check.Image = check.Checked ? SwitchOn : SwitchOff;
            return check;
        }

        private static Bitmap CreateSwitch(bool enabled)
        {
            var glyph = new Bitmap(33, 16); // Track 24 px plus a gap before the label.
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

        private static Bitmap CreateMenuTick()
        {
            var glyph = new Bitmap(12, 12);
            using (Graphics graphics = Graphics.FromImage(glyph))
            using (var pen = new Pen(Accent, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.DrawLines(pen, new[] { new Point(2, 6), new Point(5, 9), new Point(10, 3) });
            }
            return glyph;
        }

        internal static int SetFrameAttribute(IntPtr window, int attribute, int value)
        {
            return DwmSetWindowAttribute(window, attribute, ref value, sizeof(int));
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string subApp, string subIdList);

        /// <summary>Gives a scrolling container the dark Windows scrollbars instead of the light default.</summary>
        internal static void DarkScrollbars(Control control)
        {
            control.HandleCreated += (sender, e) => SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
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

    internal sealed class DeskButtonSurface
    {
        private static readonly Color[] PrimaryTop =
        {
            Color.FromArgb(255, 211, 224), Color.FromArgb(255, 225, 234), Color.FromArgb(240, 171, 197)
        };
        private static readonly Color[] PrimaryBottom =
        {
            Color.FromArgb(231, 160, 185), Color.FromArgb(249, 190, 211), Color.FromArgb(217, 141, 170)
        };
        private static readonly Color[] SecondaryFill =
        {
            Color.FromArgb(51, 30, 44), Color.FromArgb(68, 38, 55), Color.FromArgb(76, 40, 61)
        };

        private readonly Button button;
        private readonly bool primary;
        private readonly Bitmap[] surfaces = new Bitmap[3];
        private bool hovered;
        private bool pressed;

        internal DeskButtonSurface(Button button, bool primary)
        {
            this.button = button;
            this.primary = primary;
            button.BackgroundImageLayout = ImageLayout.None;
            button.Resize += (sender, e) => Rebuild();
            button.DpiChangedAfterParent += (sender, e) => Rebuild();
            button.MouseEnter += (sender, e) => { hovered = true; Apply(); };
            button.MouseLeave += (sender, e) => { hovered = false; Apply(); };
            button.MouseDown += (sender, e) =>
            {
                if (e.Button == MouseButtons.Left) { pressed = true; Apply(); }
            };
            button.MouseUp += (sender, e) => Release();
            button.MouseCaptureChanged += (sender, e) => { if (!button.Capture) Release(); };
            button.KeyDown += (sender, e) => { if (e.KeyCode == Keys.Space) { pressed = true; Apply(); } };
            button.KeyUp += (sender, e) => { if (e.KeyCode == Keys.Space) Release(); };
            button.LostFocus += (sender, e) => Release();
            button.Disposed += (sender, e) => DisposeSurfaces();
            Apply();
        }

        private void Release()
        {
            if (!pressed) return;
            pressed = false;
            Apply();
        }

        private void Rebuild()
        {
            button.BackgroundImage = null;
            DisposeSurfaces();
            Apply();
        }

        private void Apply()
        {
            if (button.Width < 4 || button.Height < 4 || button.IsDisposed) return;
            int state = pressed ? 2 : hovered ? 1 : 0;
            if (surfaces[state] == null) surfaces[state] = DrawSurface(state);
            button.BackgroundImage = surfaces[state];
        }

        private Bitmap DrawSurface(int state)
        {
            var bitmap = new Bitmap(button.Width, button.Height, PixelFormat.Format32bppPArgb);
            var bounds = new Rectangle(1, 1, bitmap.Width - 2, bitmap.Height - 2);
            int radius = Math.Max(1, Math.Min(10 * button.DeviceDpi / 96,
                Math.Min(bounds.Width, bounds.Height) / 2));
            Color top = primary ? PrimaryTop[state] : SecondaryFill[state];
            Color bottom = primary ? PrimaryBottom[state] : top;
            Color stroke = primary ? Color.FromArgb(233, 167, 189) :
                (state == 0 ? Color.FromArgb(79, 47, 65) : Color.FromArgb(120, 64, 87));

            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (GraphicsPath outline = DeskStyle.Rounded(bounds, radius))
            using (var fill = new LinearGradientBrush(bounds, top, bottom, 135f))
            using (var edge = new Pen(stroke))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.FillPath(fill, outline);
                graphics.DrawPath(edge, outline);
            }
            return bitmap;
        }

        private void DisposeSurfaces()
        {
            for (int i = 0; i < surfaces.Length; i++)
            {
                if (surfaces[i] != null) surfaces[i].Dispose();
                surfaces[i] = null;
            }
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
            Height = 35;
            Padding = new Padding(11, 0, 10, 0);
            UseMnemonic = false;
            AutoEllipsis = true;
            Image = DeskStyle.Chevron;
            TextAlign = ContentAlignment.MiddleLeft;
            ImageAlign = ContentAlignment.MiddleRight;
            TextImageRelation = TextImageRelation.TextBeforeImage;
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = true;
            menu.Renderer = new DeskMenuRenderer();
            menu.HandleCreated += (sender, e) =>
            {
                const int CornerPreference = 33;
                const int RoundSmall = 3;
                DeskStyle.SetFrameAttribute(menu.Handle, CornerPreference, RoundSmall);
            };
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
            menu.Font = Font;
            menu.MinimumSize = new Size(Width, 0);
            if (menu.Items.Count != Items.Count)
            {
                menu.SuspendLayout();
                while (menu.Items.Count != 0)
                {
                    ToolStripItem previous = menu.Items[0];
                    menu.Items.RemoveAt(0);
                    previous.Dispose();
                }
                for (int index = 0; index < Items.Count; index++)
                {
                    int optionIndex = index;
                    var item = new ToolStripMenuItem(Items[index].ToString())
                    {
                        ForeColor = DeskStyle.Text, AutoSize = false
                    };
                    item.Click += (sender, args) => SelectedIndex = optionIndex;
                    menu.Items.Add(item);
                }
                menu.ResumeLayout();
            }
            for (int index = 0; index < menu.Items.Count; index++)
            {
                var item = (ToolStripMenuItem)menu.Items[index];
                item.Text = Items[index].ToString();
                item.Checked = index == SelectedIndex;
                item.Size = new Size(Math.Max(120, Width - 4), Math.Max(30, Font.Height + 10));
            }

            Form owner = FindForm();
            const int Gap = 4;
            bool below = owner == null || PointToScreen(new Point(0, Height + Gap)).Y +
                menu.GetPreferredSize(Size.Empty).Height <= owner.RectangleToScreen(owner.ClientRectangle).Bottom;
            menu.Show(this, new Point(0, below ? Height + Gap : -Gap),
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
        public override Color MenuBorder { get { return DeskStyle.Surface; } }
        public override Color ImageMarginGradientBegin { get { return DeskStyle.Surface; } }
        public override Color ImageMarginGradientMiddle { get { return DeskStyle.Surface; } }
        public override Color ImageMarginGradientEnd { get { return DeskStyle.Surface; } }
    }

    internal sealed class DeskMenuRenderer : ToolStripProfessionalRenderer
    {
        internal DeskMenuRenderer() : base(new DeskMenuColors())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || e.Item.Width < 20 || e.Item.Height < 14) return;
            var inset = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath shape = DeskStyle.Rounded(inset, 7))
            using (var fill = new SolidBrush(DeskStyle.Hover))
                e.Graphics.FillPath(fill, shape);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Rectangle slot = e.ImageRectangle;
            Bitmap mark = DeskStyle.MenuTick;
            e.Graphics.DrawImageUnscaled(mark, slot.Left + (slot.Width - mark.Width) / 2,
                slot.Top + (slot.Height - mark.Height) / 2);
        }
    }

    internal sealed class DeskCard : Panel
    {
        private readonly bool hero;

        internal DeskCard(bool hero)
        {
            this.hero = hero;
            BackColor = DeskStyle.Background;
            Padding = new Padding(19, 15, 19, 15);
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
            using (GraphicsPath outline = DeskStyle.Rounded(bounds, hero ? 22 : 19))
            using (var border = new Pen(DeskStyle.Border))
            {
                if (hero)
                {
                    using (Brush fill = HeroFill(Size)) graphics.FillPath(fill, outline);
                }
                else
                {
                    using (var fill = new SolidBrush(DeskStyle.Surface))
                        graphics.FillPath(fill, outline);
                }
                graphics.DrawPath(border, outline);
            }
        }

        /// <summary>Hero gradient in card coordinates, shared with children that paint over it.</summary>
        internal static Brush HeroFill(Size card)
        {
            return new LinearGradientBrush(new Rectangle(1, 1, card.Width - 3, card.Height - 3),
                DeskStyle.Surface, Color.FromArgb(74, 43, 64), 14f);
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
            SetStyle(ControlStyles.Opaque | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = DeskStyle.Surface;
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
            PaintHeroBackground(graphics);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            // Uniform scale keeps the miniature desktop and its flakes undistorted at every panel width.
            Size source = preview == null ? ClientSize : preview.Frame.Size;
            float scale = Math.Min((float)Width / source.Width, (float)Height / source.Height);
            int width = (int)(source.Width * scale), height = (int)(source.Height * scale);
            var art = new Rectangle((Width - width) / 2, (Height - height) / 2, width - 1, height - 1);
            int radius = Math.Min(13 * DeviceDpi / 96, art.Height / 2);
            // An antialiased path fill replaces the old Region clip, whose corners stepped by whole pixels.
            using (GraphicsPath shape = DeskStyle.Rounded(art, radius))
            {
                if (preview != null)
                {
                    using (var texture = new TextureBrush(preview.Frame, WrapMode.Clamp))
                    {
                        texture.TranslateTransform(art.X, art.Y);
                        texture.ScaleTransform(scale, scale);
                        graphics.FillPath(texture, shape);
                    }
                    return;
                }
                using (var fill = new SolidBrush(DeskStyle.Surface)) graphics.FillPath(fill, shape);
                graphics.SetClip(shape);
                DrawConstellation(graphics, art);
                graphics.ResetClip();
            }
        }

        private void PaintHeroBackground(Graphics graphics)
        {
            Control hero = Parent;
            while (hero != null && !(hero is DeskCard)) hero = hero.Parent;
            if (hero == null)
            {
                graphics.Clear(DeskStyle.Surface);
                return;
            }
            Point origin = hero.PointToClient(PointToScreen(Point.Empty));
            graphics.TranslateTransform(-origin.X, -origin.Y);
            using (Brush fill = DeskCard.HeroFill(hero.ClientSize))
                graphics.FillRectangle(fill, new Rectangle(origin, Size));
            graphics.ResetTransform();
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
