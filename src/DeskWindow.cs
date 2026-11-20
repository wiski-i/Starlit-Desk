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
        private readonly DeskSelect themes, density, rate, effects;
        private readonly List<DeskSelect> densitySelects = new List<DeskSelect>();
        private readonly Dictionary<DesktopTheme, Control> themeCards = new Dictionary<DesktopTheme, Control>();
        private readonly Label intro, status, themeDetail;
        private readonly Button pause;
        private readonly Control screenCard;
        private readonly FlowLayoutPanel screenList;
        private string screenLayout;
        private DesktopTheme actionTheme;
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
            ClientSize = new Size(884, Screen.AllScreens.Length > 1 ? 924 : 786);
            MinimumSize = new Size(770, 465);
            FormClosing += KeepInTray;

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            DeskStyle.DarkScrollbars(scroll);
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

            var hero = new DeskCard(true) { Dock = DockStyle.Top, Height = 142 };
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
            pause.Anchor = AnchorStyles.Right;
            pause.Click += (sender, e) => app.ToggleScene();
            heroGrid.Controls.Add(pause, 2, 0);

            var settingsRow = DeskStyle.Flow(2);
            settingsRow.ColumnStyles[0] = new ColumnStyle(SizeType.Percent, 42);
            settingsRow.ColumnStyles[1] = new ColumnStyle(SizeType.Percent, 58);
            body.Controls.Add(settingsRow);

            var general = DeskStyle.Card("Tema ve performans", 291);
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

            // Each theme brings its own settings card into the same column; only the selected one is visible.
            var themeColumn = DeskStyle.Flow(1);
            settingsRow.Controls.Add(themeColumn, 1, 0);
            var winter = DeskStyle.Card("Kar ve süslemeler", 291);
            themeCards.Add(DesktopTheme.Winter, winter);
            themeCards.Add(DesktopTheme.Spring, BuildThemeCard("Çiçekler ve bahar süsleri", "Çiçek yaprağı yoğunluğu",
                new[] { "Seyrek", "Orta", "Bolca" }, "Pencerelerde yaprak", "İmleçle yaprakları savur", extras =>
                {
                    AddSpring(extras, "Çiçek bahçesi", SpringEffect.Garden);
                    AddSpring(extras, "Kelebekler", SpringEffect.Butterflies);
                    AddSpring(extras, "Arılar", SpringEffect.Bees);
                    AddSpring(extras, "Çiçek sulayan bahçıvan", SpringEffect.Gardener);
                    AddSpring(extras, "Kırlangıçlar", SpringEffect.Birds);
                    AddSpring(extras, "Sakura ağacı ve kovan", SpringEffect.Tree);
                    AddSpring(extras, "Kovanı ezen araba", SpringEffect.Car);
                    AddSpring(extras, "Arıcı", SpringEffect.Beekeeper);
                }));
            themeCards.Add(DesktopTheme.Summer, BuildThemeCard("Yağmur ve yaz süsleri", "Yağmur yoğunluğu",
                new[] { "Çisenti", "Sağanak", "Bardaktan boşanırcasına" }, "Pencerelerde su", null, extras =>
                {
                    AddSummer(extras, "Simitli güneşlenen kız", SummerEffect.Bather);
                    AddSummer(extras, "Yağmurda koşan adam", SummerEffect.Runner);
                    AddSummer(extras, "Su pompası", SummerEffect.Pump);
                    AddSummer(extras, "Nilüfer ve kurbağa", SummerEffect.Pond);
                    AddSummer(extras, "Lastik ördek", SummerEffect.Duck);
                    AddSummer(extras, "Gökkuşağı ve güneş", SummerEffect.Rainbow);
                }));
            themeCards.Add(DesktopTheme.Autumn, BuildThemeCard("Yapraklar ve sonbahar süsleri", "Yaprak yoğunluğu",
                new[] { "Seyrek", "Orta", "Yoğun" }, "Pencerelerde yaprak", "İmleçle yaprakları savur", extras =>
                {
                    AddAutumn(extras, "Yaprak toplayan tırmıkçı", AutumnEffect.Raker);
                    AddAutumn(extras, "Yığına atlayan çocuk", AutumnEffect.Kid);
                    AddAutumn(extras, "Sonbahar ağacı", AutumnEffect.Tree);
                    AddAutumn(extras, "Palamut kaçıran sincap", AutumnEffect.Squirrel);
                    AddAutumn(extras, "Ara sıra fırtına", AutumnEffect.Gale);
                    AddAutumn(extras, "Çöpçü", AutumnEffect.Sweeper);
                }));
            // Cards grow with their list of pieces; the row keeps both columns the same height.
            int tallest = general.Height;
            foreach (Control card in themeCards.Values)
            {
                themeColumn.Controls.Add(card);
                tallest = Math.Max(tallest, card.Height);
            }
            general.Height = tallest;
            foreach (Control card in themeCards.Values) card.Height = tallest;
            var winterBody = (TableLayoutPanel)winter.Controls[0];
            var winterGrid = DeskStyle.Flow(2);
            winterGrid.ColumnStyles[0] = new ColumnStyle(SizeType.Percent, 46);
            winterGrid.ColumnStyles[1] = new ColumnStyle(SizeType.Percent, 54);
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

            screenCard = DeskStyle.Card("Ekranlar", 124);
            body.Controls.Add(screenCard);
            var screenBody = (TableLayoutPanel)screenCard.Controls[0];
            screenBody.Controls.Add(DeskStyle.Copy("Efektin görüneceği ekranları seç."));
            screenList = new FlowLayoutPanel
            {
                AutoSize = true, Dock = DockStyle.Top, BackColor = DeskStyle.Surface, Margin = new Padding(0, 4, 0, 0)
            };
            screenBody.Controls.Add(screenList);

            var actions = DeskStyle.Card("Efekt başlat", 108);
            body.Controls.Add(actions);
            var actionBody = (TableLayoutPanel)actions.Controls[0];
            var actionRow = DeskStyle.Flow(2);
            actionRow.ColumnStyles[0] = new ColumnStyle(SizeType.Percent, 100);
            actionRow.ColumnStyles[1] = new ColumnStyle(SizeType.AutoSize);
            actionBody.Controls.Add(actionRow);
            effects = DeskStyle.Select("Başlatılacak efekt");
            effects.Margin = new Padding(0, 1, 10, 0);
            actionRow.Controls.Add(effects, 0, 0);
            var play = DeskStyle.Button("Uygula", false);
            play.Click += (sender, e) =>
            {
                if (effects.SelectedItem is SnowAction) app.RunSnowAction((SnowAction)effects.SelectedItem);
                else if (effects.SelectedItem is SummerAction) app.RunSummerAction((SummerAction)effects.SelectedItem);
                else if (effects.SelectedItem is SeasonAction) app.RunSeasonAction((SeasonAction)effects.SelectedItem);
            };
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
                foreach (DeskSelect select in densitySelects) select.SelectedIndex = settings.Density;
                rate.SelectedIndex = Array.IndexOf(Rates, settings.Rate);
                foreach (var pair in themeCards) pair.Value.Visible = pair.Key == selected;
                if (actionTheme != selected)
                {
                    actionTheme = selected;
                    effects.Items.Clear();
                    effects.SelectedIndex = -1;
                    if (selected == DesktopTheme.Summer) effects.Items.AddRange(SummerAction.All);
                    else if (selected == DesktopTheme.Autumn) effects.Items.AddRange(SeasonAction.Autumn);
                    else if (selected == DesktopTheme.Spring) effects.Items.AddRange(SeasonAction.Spring);
                    else effects.Items.AddRange(SnowAction.All);
                    effects.SelectedIndex = 0;
                }
                foreach (Action update in updateChecks) update();
                SyncScreens();
            }
            finally { syncing = false; }
        }

        /// <summary>Rebuilds the display switches when monitors change and ticks the ones showing the effect.</summary>
        private void SyncScreens()
        {
            Screen[] screens = Screen.AllScreens;
            screenCard.Visible = screens.Length > 1;
            string layout = string.Join("|", Array.ConvertAll(screens, item => item.DeviceName + item.Bounds));
            if (layout != screenLayout)
            {
                screenLayout = layout;
                while (screenList.Controls.Count != 0) screenList.Controls[0].Dispose();
                foreach (Screen screen in screens)
                {
                    string device = screen.DeviceName;
                    CheckBox check = DeskStyle.Toggle(Program.ScreenLabel(screen));
                    check.Tag = device;
                    check.Margin = new Padding(0, 1, 18, 1);
                    check.CheckedChanged += delegate
                    {
                        if (!syncing) app.SetScreenShown(device, check.Checked);
                    };
                    screenList.Controls.Add(check);
                }
            }
            foreach (Control control in screenList.Controls)
                ((CheckBox)control).Checked = settings.ShowsOn((string)control.Tag);
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
            app.DashboardChanged();
        }

        protected override void OnDeactivate(EventArgs e)
        {
            foreground = false;
            UpdatePreview();
            app.DashboardChanged();
            base.OnDeactivate(e);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            UpdatePreview();
            app.DashboardChanged();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdatePreview();
            app.DashboardChanged();
        }

        protected override void OnMove(EventArgs e)
        {
            base.OnMove(e);
            app.DashboardChanged();
        }

        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);
            const int SettingChange = 0x001A;
            if (message.Msg == SettingChange) UpdatePreview();
        }

        private static DeskSelect AddSelect(TableLayoutPanel panel, string title, bool compact, params object[] choices)
        {
            panel.Controls.Add(DeskStyle.Copy(title));
            DeskSelect select = DeskStyle.Select(title);
            select.Margin = new Padding(0, 4, 0, compact ? 6 : 8);
            select.Items.AddRange(choices);
            panel.Controls.Add(select);
            return select;
        }

        private void AddCheck(TableLayoutPanel panel, string title, Func<bool> read, Action<bool> change, bool save = true)
        {
            CheckBox check = DeskStyle.Toggle(title);
            check.CheckedChanged += delegate
            {
                if (syncing) return;
                change(check.Checked);
                if (save) app.SaveSettings();
            };
            updateChecks.Add(() => check.Checked = read());
            panel.Controls.Add(check);
        }

        /// <summary>
        /// Settings card of a theme: density, window pile, optional cursor sweep and wind on the left, the
        /// theme's own pieces (filled by <paramref name="extras"/>) on the right.
        /// </summary>
        private Control BuildThemeCard(string title, string densityTitle, string[] densities, string ledgeTitle,
            string sweepTitle, Action<TableLayoutPanel> extras)
        {
            var card = DeskStyle.Card(title, 291);
            var grid = DeskStyle.Flow(2);
            grid.ColumnStyles[0] = new ColumnStyle(SizeType.Percent, 46);
            grid.ColumnStyles[1] = new ColumnStyle(SizeType.Percent, 54);
            ((TableLayoutPanel)card.Controls[0]).Controls.Add(grid);
            var weather = DeskStyle.Flow(1);
            weather.Margin = new Padding(0, 0, 10, 0);
            grid.Controls.Add(weather, 0, 0);
            DeskSelect select = AddSelect(weather, densityTitle, true, densities);
            select.SelectedIndexChanged += delegate
            {
                if (syncing || select.SelectedIndex < 0) return;
                settings.Density = select.SelectedIndex;
                app.SaveSettings();
            };
            densitySelects.Add(select);
            AddCheck(weather, ledgeTitle, () => settings.WindowLedges, app.SetWindowLedges, false);
            if (sweepTitle != null) AddCheck(weather, sweepTitle, () => settings.CursorSweep, value => settings.CursorSweep = value);
            AddCheck(weather, "Rüzgâr", () => settings.Wind, value => settings.Wind = value);

            var pieces = DeskStyle.Flow(1);
            grid.Controls.Add(pieces, 1, 0);
            pieces.Controls.Add(DeskStyle.Copy("Süslemeler"));
            extras(pieces);
            card.Height = Math.Max(card.Height, 112 + 32 * (pieces.Controls.Count - 1));
            return card;
        }

        private void AddAutumn(TableLayoutPanel panel, string title, AutumnEffect effect)
        {
            AddCheck(panel, title, () => settings.Has(effect), enabled =>
            {
                if (enabled) settings.Autumn |= effect;
                else settings.Autumn &= ~effect;
            });
        }

        private void AddSpring(TableLayoutPanel panel, string title, SpringEffect effect)
        {
            AddCheck(panel, title, () => settings.Has(effect), enabled =>
            {
                if (enabled) settings.Spring |= effect;
                else settings.Spring &= ~effect;
            });
        }

        private void AddSummer(TableLayoutPanel panel, string title, SummerEffect effect)
        {
            AddCheck(panel, title, () => settings.Has(effect), enabled =>
            {
                if (enabled) settings.Summer |= effect;
                else settings.Summer &= ~effect;
            });
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
            const int ImmersiveDarkMode = 20;
            const int BorderColor = 34;
            const int CaptionColor = 35;
            const int TextColor = 36;
            if (SystemInformation.HighContrast) return;
            if (DeskStyle.SetFrameAttribute(Handle, ImmersiveDarkMode, 1) != 0)
                DeskStyle.SetFrameAttribute(Handle, 19, 1);
            SetCaptionColor(BorderColor, DeskStyle.Border);
            SetCaptionColor(CaptionColor, DeskStyle.Caption);
            SetCaptionColor(TextColor, DeskStyle.Text);
        }

        private void SetCaptionColor(int attribute, Color color)
        {
            DeskStyle.SetFrameAttribute(Handle, attribute, ColorTranslator.ToWin32(color));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && logo != null) logo.Dispose();
            base.Dispose(disposing);
        }

    }
}
