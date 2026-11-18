using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SnowDesk
{
    internal sealed class DeskWindow : Form
    {
        private static readonly int[] Rates = { 20, 30, 60 };
        private readonly Program app;
        private readonly DeskThemePreview themePreview;
        private readonly Settings settings;
        private readonly List<Action> updateChecks = new List<Action>();
        private readonly ComboBox themes, density, rate;
        private readonly Label intro, status, themeDetail;
        private readonly Button pause;
        private readonly Control winterCard, actionCard;
        private readonly Image logo;
        private bool syncing;
        private bool foreground;

        internal DeskWindow(Program app, Icon icon)
        {
            this.app = app;
            settings = app.Preferences;
            Text = "Starlit Desk";
            Icon = icon;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 10f);
            BackColor = DeskStyle.Background;
            ForeColor = DeskStyle.Text;
            ClientSize = new Size(884, 665);
            MinimumSize = new Size(680, 465);
            FormClosing += KeepInTray;

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var body = DeskStyle.Flow(1);
            body.Dock = DockStyle.Top;
            body.Padding = new Padding(22, 14, 22, 10);
            Controls.Add(scroll);
            scroll.Controls.Add(body);

            using (Stream stream = typeof(DeskWindow).Assembly.GetManifestResourceStream("StarlitDesk.Logo"))
            using (var bitmap = new Bitmap(stream))
                logo = new Bitmap(bitmap);

            var header = DeskStyle.Flow(2);
            header.Margin = new Padding(0, 0, 0, 12);
            header.ColumnStyles[0] = new ColumnStyle(SizeType.Absolute, 59);
            header.Controls.Add(new PictureBox
            {
                Image = logo, SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(44, 44),
                Margin = new Padding(0, 3, 0, 0)
            }, 0, 0);
            var brand = DeskStyle.Flow(1);
            brand.Controls.Add(DeskStyle.Title("Starlit Desk", 20));
            brand.Controls.Add(DeskStyle.Copy("Windows masaüstü efektleri"));
            header.Controls.Add(brand, 1, 0);
            body.Controls.Add(header);

            var hero = new DeskCard(true) { Dock = DockStyle.Top, Height = 137 };
            body.Controls.Add(hero);
            var heroGrid = DeskStyle.Flow(3);
            heroGrid.AutoSize = false;
            heroGrid.BackColor = Color.Transparent;
            heroGrid.Dock = DockStyle.Fill;
            heroGrid.ColumnStyles[0] = new ColumnStyle(SizeType.Percent, 35);
            heroGrid.ColumnStyles[1] = new ColumnStyle(SizeType.Percent, 65);
            heroGrid.ColumnStyles[2] = new ColumnStyle(SizeType.AutoSize);
            heroGrid.RowCount = 1;
            heroGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            hero.Controls.Add(heroGrid);
            var heroCopy = DeskStyle.Flow(1);
            heroCopy.Controls.Add(DeskStyle.Copy("Seçili tema"));
            intro = DeskStyle.Title("", 24);
            heroCopy.Controls.Add(intro);
            heroCopy.Controls.Add(status = DeskStyle.Copy(""));
            heroGrid.Controls.Add(heroCopy, 0, 0);
            themePreview = new DeskThemePreview();
            heroGrid.Controls.Add(themePreview, 1, 0);
            pause = DeskStyle.Button("", true);
            pause.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            pause.Click += (sender, e) => app.ToggleScene();
            heroGrid.Controls.Add(pause, 2, 0);

            var settingsRow = DeskStyle.Flow(2);
            settingsRow.ColumnStyles[0] = new ColumnStyle(SizeType.Percent, 42);
            settingsRow.ColumnStyles[1] = new ColumnStyle(SizeType.Percent, 58);
            body.Controls.Add(settingsRow);

            var general = DeskStyle.Card("Tema ve performans", 266);
            general.Margin = new Padding(0, 0, 12, 12);
            settingsRow.Controls.Add(general, 0, 0);
            var options = (TableLayoutPanel)general.Controls[0];
            themes = AddSelect(options, "Masaüstü teması", false);
            foreach (DesktopTheme theme in DesktopTheme.All) themes.Items.Add(theme);
            themes.SelectedIndexChanged += (sender, e) =>
            {
                if (!syncing && themes.SelectedItem is DesktopTheme)
                    app.SelectTheme((DesktopTheme)themes.SelectedItem);
            };
            themeDetail = DeskStyle.Copy("");
            themeDetail.Dock = DockStyle.Top;
            themeDetail.Height = 38;
            themeDetail.AutoSize = false;
            options.Controls.Add(themeDetail);

            rate = AddSelect(options, "Kare hızı", false,
                "Ekonomik · 20 FPS", "Dengeli · 30 FPS", "Akıcı · 60 FPS");
            rate.SelectedIndexChanged += delegate
            {
                if (syncing || rate.SelectedIndex < 0) return;
                app.SetFrameRate(Rates[rate.SelectedIndex]);
                app.SaveSettings();
            };
            AddCheck(options, "Windows ile başlat", Program.HasStartup, value => app.ToggleStartup(), false);

            var winter = DeskStyle.Card("Kar ve süslemeler", 266);
            winterCard = winter;
            settingsRow.Controls.Add(winter, 1, 0);
            var winterBody = (TableLayoutPanel)winter.Controls[0];
            var winterGrid = DeskStyle.Flow(2);
            winterBody.Controls.Add(winterGrid);
            var snow = DeskStyle.Flow(1);
            snow.Margin = new Padding(0, 0, 10, 0);
            winterGrid.Controls.Add(snow, 0, 0);
            density = AddSelect(snow, "Kar yoğunluğu", true, "Hafif", "Orta", "Yoğun");
            density.SelectedIndexChanged += delegate
            {
                if (syncing || density.SelectedIndex < 0) return;
                settings.Density = density.SelectedIndex;
                app.SaveSettings();
            };
            AddCheck(snow, "Kenarlarda kar", () => settings.WindowLedges, app.SetWindowLedges, false);
            AddCheck(snow, "İmleçle karı dağıt", () => settings.CursorSweep, value => settings.CursorSweep = value);
            AddCheck(snow, "Rüzgâr", () => settings.Wind, value => settings.Wind = value);

            var decorations = DeskStyle.Flow(1);
            winterGrid.Controls.Add(decorations, 1, 0);
            decorations.Controls.Add(DeskStyle.Copy("Süslemeler"));
            AddDecoration(decorations, "Yılbaşı ağaçları", Effect.Trees);
            AddDecoration(decorations, "Noel Baba'nın kızağı", Effect.Sleigh);
            AddDecoration(decorations, "Yürüyen Noel Baba", Effect.Santa);
            AddDecoration(decorations, "Kar küreme aracı", Effect.Plow);
            AddDecoration(decorations, "Teleferik", Effect.Gondola);
            AddCheck(decorations, "Buz sarkıtları", () => settings.Icicles, value => settings.Icicles = value);

            var actions = DeskStyle.Card("Efekt başlat", 108);
            actionCard = actions;
            body.Controls.Add(actions);
            var actionBody = (TableLayoutPanel)actions.Controls[0];
            var actionRow = DeskStyle.Flow(2);
            actionRow.ColumnStyles[0] = new ColumnStyle(SizeType.Percent, 100);
            actionRow.ColumnStyles[1] = new ColumnStyle(SizeType.AutoSize);
            actionBody.Controls.Add(actionRow);
            var effects = DeskStyle.Select("Başlatılacak efekt");
            effects.Items.AddRange(SnowAction.All);
            effects.SelectedIndex = 0;
            effects.Margin = new Padding(0, 3, 10, 0);
            actionRow.Controls.Add(effects, 0, 0);
            var play = DeskStyle.Button("Uygula", false);
            play.Click += (sender, e) => app.RunSnowAction((SnowAction)effects.SelectedItem);
            actionRow.Controls.Add(play, 1, 0);

            Sync();
        }

        internal void Sync()
        {
            if (syncing) return;
            syncing = true;
            try
            {
                DesktopTheme selected = settings.Theme;
                themes.SelectedItem = selected;
                themeDetail.Text = selected.Description;
                intro.Text = selected.Name;
                status.Text = app.IsRunning ? "●  Efekt açık" : "○  Efekt duraklatıldı";
                pause.Text = app.IsRunning ? "Duraklat" : "Devam et";
                UpdatePreview();
                density.SelectedIndex = settings.Density;
                rate.SelectedIndex = Array.IndexOf(Rates, settings.Rate);
                winterCard.Visible = actionCard.Visible = selected == DesktopTheme.Winter;
                foreach (Action update in updateChecks) update();
            }
            finally { syncing = false; }
        }

        private void UpdatePreview()
        {
            if (themePreview != null)
                themePreview.SetPreview(settings,
                    foreground && Visible && app.IsRunning && WindowState != FormWindowState.Minimized);
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            foreground = true;
            UpdatePreview();
        }

        protected override void OnDeactivate(EventArgs e)
        {
            foreground = false;
            UpdatePreview();
            base.OnDeactivate(e);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            UpdatePreview();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdatePreview();
        }

        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);
            const int SettingChange = 0x001A;
            if (message.Msg == SettingChange) UpdatePreview();
        }

        private static ComboBox AddSelect(TableLayoutPanel panel, string title, bool compact, params object[] choices)
        {
            panel.Controls.Add(DeskStyle.Copy(title));
            ComboBox select = DeskStyle.Select(title);
            select.Margin = new Padding(0, 4, 0, compact ? 6 : 8);
            select.Items.AddRange(choices);
            panel.Controls.Add(select);
            return select;
        }

        private void AddCheck(TableLayoutPanel panel, string title, Func<bool> read, Action<bool> change, bool save = true)
        {
            var check = new CheckBox
            {
                Text = title, AutoSize = true, ForeColor = DeskStyle.Text,
                BackColor = DeskStyle.Surface, FlatStyle = FlatStyle.Flat, UseCompatibleTextRendering = true,
                UseVisualStyleBackColor = false, Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 1, 0, 2)
            };
            check.CheckedChanged += delegate
            {
                if (syncing) return;
                change(check.Checked);
                if (save) app.SaveSettings();
            };
            updateChecks.Add(() => check.Checked = read());
            panel.Controls.Add(check);
        }

        private void AddDecoration(TableLayoutPanel panel, string title, Effect effect)
        {
            AddCheck(panel, title, () => settings.Has(effect), enabled =>
            {
                if (enabled) settings.Effects |= effect;
                else settings.Effects &= ~effect;
            });
        }

        private void KeepInTray(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason != CloseReason.UserClosing) return;
            e.Cancel = true;
            Hide();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int dark = 1;
            if (DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int)) != 0)
                DwmSetWindowAttribute(Handle, 19, ref dark, sizeof(int));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && logo != null) logo.Dispose();
            base.Dispose(disposing);
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
