using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Win32;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Starlit Desk")]
[assembly: System.Reflection.AssemblyProduct("Starlit Desk")]
[assembly: System.Reflection.AssemblyDescription("Offline desktop effects for Windows")]
[assembly: System.Reflection.AssemblyVersion("1.13.0.0")]

namespace SnowDesk
{
    internal sealed class Program : Forms.ApplicationContext
    {
        private const string StartupKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupValue = "SnowDeskWindows";
        private readonly Settings settings;
        private readonly List<Overlay> overlays = new List<Overlay>();
        private FrameSurface frameSurface;
        private readonly List<IntPtr> winEventHooks = new List<IntPtr>();
        private readonly Stopwatch animation = new Stopwatch();
        private readonly FrameClock frameClock;
        private readonly Forms.Timer windowsTimer;
        private readonly Forms.Timer profileTimer;
        private readonly DesktopIcons desktopIcons = new DesktopIcons();
        private List<Desktop.WindowBounds> sceneGeometry = new List<Desktop.WindowBounds>();
        private static readonly List<Desktop.WindowBounds> NoGeometry = new List<Desktop.WindowBounds>();
        private readonly List<ScreenTransfer> transfers = new List<ScreenTransfer>(48);
        private IntPtr lastForeground;
        private string foregroundApp = "", pausedScreen, quietScreen;
        private DateTime calendarDate;
        private bool onBattery;
        private int effectiveRate;
        private readonly Forms.Control uiThread = new Forms.Control();
        private readonly Desktop.EventHandler windowsChanged;
        private readonly Forms.NotifyIcon tray;
        private readonly Drawing.Icon icon;
        private DeskWindow dashboard;
        private Drawing.Region dashboardShield;
        private bool running = true;
        private bool sessionLocked;
        private bool geometryDirty;
        private IntPtr shellFocus;
        private volatile bool closing;
        private double lastFrame;
        private Desktop.Point cursor;

        [STAThread]
        private static void Main(string[] args)
        {
            bool created;
            using (var instance = new Mutex(true, @"Local\SnowDeskWindows", out created))
            {
                if (!created) return;
                try
                {
                    Forms.Application.EnableVisualStyles();
                    Forms.Application.SetCompatibleTextRenderingDefault(false);
                    Forms.Application.SetUnhandledExceptionMode(Forms.UnhandledExceptionMode.ThrowException);
                    using (var app = new Program())
                    {
                        app.Start(Array.IndexOf(args, "--background") < 0);
                        Forms.Application.Run(app);
                    }
                }
                catch (Exception error)
                {
                    Forms.MessageBox.Show("Starlit Desk başlatılamadı. " + error.Message, "Starlit Desk",
                        Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Error);
                }
                finally { instance.ReleaseMutex(); }
            }
        }

        internal Program(Settings preferences = null)
        {
            settings = preferences ?? Settings.Load();
            effectiveRate = settings.Rate;
            frameClock = new FrameClock(RunOnUiThread, Render, settings.Rate);
            windowsTimer = new Forms.Timer { Interval = 100 };
            windowsTimer.Tick += RefreshDesktop;
            profileTimer = new Forms.Timer { Interval = 15000 };
            profileTimer.Tick += delegate { UpdateEnvironment(); };
            windowsChanged = OnWindowEvent;
            icon = Drawing.Icon.ExtractAssociatedIcon(Forms.Application.ExecutablePath) ??
                (Drawing.Icon)Drawing.SystemIcons.Application.Clone();
            tray = new Forms.NotifyIcon
            {
                Icon = icon,
                Text = "Starlit Desk - Efekt açık",
                ContextMenuStrip = CreateMenu(),
                Visible = true
            };
            tray.MouseClick += delegate(object sender, Forms.MouseEventArgs e)
            {
                if (e.Button == Forms.MouseButtons.Left) ShowDashboard();
            };
        }

        internal void Start(bool showDashboard)
        {
            uiThread.CreateControl();
            SystemEvents.DisplaySettingsChanged += DisplayChanged;
            SystemEvents.SessionSwitch += SessionChanged;
            SystemEvents.PowerModeChanged += PowerChanged;
            UpdateEnvironment();
            profileTimer.Start();
            RebuildDisplays();
            SetHooks();
            if (showDashboard) ShowDashboard();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !closing)
            {
                closing = true;
                SystemEvents.DisplaySettingsChanged -= DisplayChanged;
                SystemEvents.SessionSwitch -= SessionChanged;
                SystemEvents.PowerModeChanged -= PowerChanged;
                profileTimer.Dispose();
                StopHooks();
                frameClock.Dispose();
                windowsTimer.Dispose();
                CloseDisplays();
                tray.Visible = false;
                tray.ContextMenuStrip.Dispose();
                tray.Dispose();
                if (dashboard != null) dashboard.Dispose();
                if (dashboardShield != null) dashboardShield.Dispose();
                icon.Dispose();
                uiThread.Dispose();
            }
            base.Dispose(disposing);
        }

        private void SetHooks()
        {
            if (winEventHooks.Count != 0) return;
            WatchWindows(0x0003, 0x0003); // Foreground.
            WatchWindows(0x0006, 0x0007); // Menu popups.
            WatchWindows(0x000A, 0x000B); // Move/size start and stop.
            WatchWindows(0x0016, 0x0017); // Minimize start/end.
            WatchWindows(0x8000, 0x8003); // Create, destroy, show, hide.
            WatchWindows(0x8005, 0x8005); // Accessibility focus.
            WatchWindows(0x800B, 0x800B); // Location or size changed.
            WatchWindows(0x8017, 0x8018); // Cloak or uncloak.
        }

        private void WatchWindows(uint first, uint last)
        {
            const uint OutOfContextSkipOwnProcess = 0x0002;
            IntPtr hook = Desktop.SetWinEventHook(first, last, IntPtr.Zero, windowsChanged,
                0, 0, OutOfContextSkipOwnProcess);
            if (hook != IntPtr.Zero) winEventHooks.Add(hook);
        }

        private void StopHooks()
        {
            foreach (IntPtr hook in winEventHooks) Desktop.UnhookWinEvent(hook);
            winEventHooks.Clear();
            PopupSurfaces.Clear();
        }

        private void OnWindowEvent(IntPtr hook, uint kind, IntPtr hwnd, int obj, int child, uint thread, uint time)
        {
            if (closing || hwnd == IntPtr.Zero || !running || sessionLocked) return;
            if (kind == 0x8005)
            {
                if (!Desktop.MayBeStartFocus(hwnd)) return;
                shellFocus = hwnd;
            }
            else if (kind != 0x0006 && kind != 0x0007 && (obj != 0 || child != 0)) return;
            if (kind == 0x8001 || kind == 0x8003) PopupSurfaces.Forget(hwnd);
            if (kind == 0x0003) shellFocus = IntPtr.Zero;
            if (kind == 0x800B && !Desktop.IsTopLevel(hwnd)) return;
            geometryDirty = true;
            if (!frameClock.Running && !windowsTimer.Enabled) windowsTimer.Start();
        }

        private void DisplayChanged(object sender, EventArgs e)
        {
            RunOnUiThread(RebuildDisplays);
        }

        private void SessionChanged(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason != SessionSwitchReason.SessionLock && e.Reason != SessionSwitchReason.SessionUnlock) return;
            RunOnUiThread(delegate
            {
                sessionLocked = e.Reason == SessionSwitchReason.SessionLock;
                RefreshDesktop(null, EventArgs.Empty);
            });
        }

        private void RunOnUiThread(Action action)
        {
            if (closing) return;
            try { uiThread.BeginInvoke(action); }
            catch (InvalidOperationException) { if (!closing) throw; }
        }

        private void CloseDisplays()
        {
            foreach (Overlay overlay in overlays)
            {
                overlay.Close();
                overlay.Scene.Dispose();
            }
            overlays.Clear();
            ReleaseFrameSurface();
        }

        private void ReleaseFrameSurface()
        {
            if (frameSurface == null) return;
            frameSurface.Dispose();
            frameSurface = null;
        }

        private void RebuildDisplays()
        {
            CloseDisplays();
            foreach (Forms.Screen screen in Forms.Screen.AllScreens)
            {
                var overlay = new Overlay(screen, settings.ThemeFor(screen.DeviceName, DateTime.Today));
                overlays.Add(overlay);
            }
            RefreshDesktop(null, EventArgs.Empty);
            SyncDashboard();
        }

        /// <summary>Turns the effect on or off for one display; the other displays keep their state.</summary>
        internal void SetScreenShown(string device, bool shown)
        {
            if (shown) settings.HiddenScreens.Remove(device);
            else settings.HiddenScreens.Add(device);
            RefreshDesktop(null, EventArgs.Empty);
            SaveSettings();
        }

        private void RefreshDesktop(object sender, EventArgs e)
        {
            windowsTimer.Stop();
            geometryDirty = false;
            bool active = running && !sessionLocked;
            string fullscreen = active ? Desktop.FullscreenScreen() : null;
            IntPtr foreground = active ? Desktop.GetForegroundWindow() : IntPtr.Zero;
            bool onDesktop = active && Desktop.DesktopHasFocus(foreground);
            ApplyForegroundRules(foreground);
            ApplyProfiles();
            if (active && settings.IconDust) desktopIcons.Refresh(onDesktop, ShellChanged);

            var size = new Drawing.Size();
            foreach (Overlay overlay in overlays)
            {
                overlay.Active = active && overlay.DisplayName != fullscreen && overlay.DisplayName != pausedScreen &&
                    settings.ShowsOn(overlay.DisplayName);
                if (!overlay.Active)
                {
                    if (overlay.IsVisible) overlay.Scene.ResetPointer();
                    overlay.Close();
                    continue;
                }
                size.Width = Math.Max(size.Width, overlay.Bounds.Width);
                size.Height = Math.Max(size.Height, overlay.Bounds.Height);
            }

            if (size.IsEmpty)
            {
                frameClock.Stop();
                animation.Stop();
                ReleaseFrameSurface();
                return;
            }

            if (frameSurface == null || frameSurface.Size != size)
            {
                ReleaseFrameSurface();
                frameSurface = new FrameSurface(size);
            }

            Drawing.Rectangle dashboardBounds = DashboardBounds();
            bool needsGeometry = overlays.Exists(item => item.Active && item.Scene.UsesWindowGeometry(item.Options));
            var windows = needsGeometry || settings.IconDust || !dashboardBounds.IsEmpty ?
                Desktop.ReadWindows(ShellChanged, shellFocus) : new List<Desktop.WindowBounds>();
            sceneGeometry = windows;
            UpdateShield(windows, dashboardBounds);
            foreach (Overlay overlay in overlays)
            {
                if (!overlay.Active) continue;
                AttachTravel(overlay);
                UpdateSceneGeometry(overlay);
                if (settings.IconDust) overlay.Dust.Update(desktopIcons.Bounds, windows, overlay.Bounds, onDesktop);
                overlay.Reveal(frameSurface, dashboardShield);
                var popup = windows.Find(item => item.IsShellPopup && overlay.Contains(new Desktop.Point
                {
                    X = (int)(item.Bounds.Left + item.Bounds.Width * 0.5), Y = (int)item.Bounds.Top
                }));
                overlay.AlignWithShell(onDesktop, popup.Handle);
            }

            if (!frameClock.Running)
            {
                lastFrame = 0;
                animation.Restart();
                frameClock.Start();
            }
        }

        private void ShellChanged()
        {
            RunOnUiThread(delegate
            {
                if (!running || sessionLocked) return;
                geometryDirty = true;
                if (!frameClock.Running && !windowsTimer.Enabled) windowsTimer.Start();
            });
        }

        private void Render()
        {
            if (geometryDirty) RefreshDesktop(null, EventArgs.Empty);
            if (!frameClock.Running) return;
            double now = animation.Elapsed.TotalSeconds;
            double elapsed = Math.Max(0, Math.Min(0.12, now - lastFrame));
            lastFrame = now;
            Desktop.GetCursorPos(out cursor);
            Drawing.Rectangle dashboardBounds = DashboardBounds();
            foreach (Overlay overlay in overlays)
            {
                if (!overlay.IsVisible) continue;
                if (overlay.AdvanceTransition(elapsed))
                {
                    AttachTravel(overlay);
                    UpdateSceneGeometry(overlay);
                }
                SnowField snow = overlay.Snow;
                if (snow != null && snow.IsHoldingTree)
                    snow.DragTree(cursor, elapsed, Desktop.PrimaryButtonDown());
                overlay.Scene.Advance(elapsed, overlay.Options, cursor);
                if (settings.IconDust) overlay.Dust.Advance(elapsed);
            }
            foreach (ScreenTransfer transfer in transfers) transfer.Target.Receive(transfer.Particle);
            transfers.Clear();
            if (settings.IconDust && Desktop.PrimaryButtonDown() && Desktop.DesktopHasFocus(Desktop.GetForegroundWindow()))
                desktopIcons.Refresh(true, ShellChanged);
            foreach (Overlay overlay in overlays)
            {
                if (!overlay.IsVisible) continue;
                overlay.RenderFrame(frameSurface, dashboardShield);
                overlay.UpdateTreeTarget(dashboardBounds);
            }
        }

        private void AttachTravel(Overlay overlay)
        {
            if (overlay.Routed == overlay.Scene) return;
            overlay.Routed = overlay.Scene;
            var travel = overlay.Scene as TravelingScene;
            if (travel != null) travel.Send = particle => SendAcross(overlay, particle);
        }

        private void UpdateSceneGeometry(Overlay overlay)
        {
            overlay.Scene.UpdateLedges(overlay.Scene.UsesWindowGeometry(overlay.Options) ? sceneGeometry : NoGeometry);
        }

        private bool SendAcross(Overlay origin, TravelingParticle particle)
        {
            if (transfers.Count >= 160) return false;
            foreach (Overlay target in overlays)
            {
                if (target == origin || !target.Active || target.Theme != origin.Theme) continue;
                var receiving = target.Scene as TravelingScene;
                if (receiving == null || !ScreenTransfer.Between(origin.Bounds, target.Bounds, ref particle)) continue;
                transfers.Add(new ScreenTransfer { Target = receiving, Particle = particle });
                return true;
            }
            return false;
        }

        /// <summary>
        /// The effect is kept off the panel only where the panel is really visible: windows above it in the
        /// z-order take their part back, so a panel left behind other windows no longer blocks the rain.
        /// </summary>
        private void UpdateShield(List<Desktop.WindowBounds> windows, Drawing.Rectangle bounds)
        {
            if (dashboardShield != null) dashboardShield.Dispose();
            dashboardShield = null;
            if (bounds.IsEmpty) return;
            dashboardShield = new Drawing.Region(bounds);
            IntPtr handle = dashboard.Handle;
            int index = windows.FindIndex(item => item.Handle == handle);
            for (int i = 0; i < index; i++)
            {
                System.Windows.Rect above = windows[i].Bounds;
                dashboardShield.Exclude(new Drawing.RectangleF((float)above.X, (float)above.Y,
                    (float)above.Width, (float)above.Height));
            }
        }

        private Drawing.Rectangle DashboardBounds()
        {
            return dashboard != null && dashboard.Visible && dashboard.WindowState != Forms.FormWindowState.Minimized
                ? dashboard.Bounds : Drawing.Rectangle.Empty;
        }

        /// <summary>
        /// The window-event hooks skip this process, so the panel reports its own moves here; otherwise snow or
        /// water on its top edge stays behind at the old position.
        /// </summary>
        internal void DashboardChanged()
        {
            if (closing || !running || sessionLocked) return;
            geometryDirty = true;
            if (!frameClock.Running && !windowsTimer.Enabled) windowsTimer.Start();
        }

        internal Settings Preferences { get { return settings; } }
        internal bool IsRunning { get { return running; } }
        internal bool BatteryProfileActive { get { return settings.BatterySaver && onBattery; } }

        internal void ShowDashboard()
        {
            if (dashboard == null) dashboard = new DeskWindow(this, icon);
            dashboard.Show();
            if (dashboard.WindowState == Forms.FormWindowState.Minimized)
                dashboard.WindowState = Forms.FormWindowState.Normal;
            dashboard.Activate();
            dashboard.Sync();
        }

        private void SyncDashboard()
        {
            if (dashboard != null && dashboard.Visible) dashboard.Sync();
        }

        internal void SelectTheme(DesktopTheme theme)
        {
            if (theme == settings.Theme && settings.CalendarMode == 0) return;
            settings.CalendarMode = 0;
            settings.Theme = theme;
            SaveSettings();
        }

        private void PowerChanged(object sender, PowerModeChangedEventArgs e)
        {
            RunOnUiThread(UpdateEnvironment);
        }

        private void UpdateEnvironment()
        {
            bool battery = Forms.SystemInformation.PowerStatus.PowerLineStatus == Forms.PowerLineStatus.Offline;
            DateTime today = DateTime.Today;
            if (battery == onBattery && calendarDate == today) return;
            onBattery = battery;
            calendarDate = today;
            if (overlays.Count != 0) RefreshDesktop(null, EventArgs.Empty);
            SyncDashboard();
        }

        private static bool MatchesApp(string list, string app)
        {
            if (list.Length == 0 || app.Length == 0) return false;
            return ("|" + list + "|").IndexOf("|" + app + "|", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ApplyForegroundRules(IntPtr hwnd)
        {
            pausedScreen = quietScreen = null;
            if (hwnd == IntPtr.Zero || (settings.PausedApps.Length == 0 && settings.QuietApps.Length == 0)) return;
            if (hwnd != lastForeground)
            {
                lastForeground = hwnd;
                foregroundApp = Path.GetFileNameWithoutExtension(ShellSurfaces.ProcessImagePath(hwnd));
            }
            if (MatchesApp(settings.PausedApps, foregroundApp)) pausedScreen = Forms.Screen.FromHandle(hwnd).DeviceName;
            else if (MatchesApp(settings.QuietApps, foregroundApp)) quietScreen = Forms.Screen.FromHandle(hwnd).DeviceName;
        }

        private void ApplyProfiles()
        {
            bool battery = BatteryProfileActive;
            int rate = battery ? 20 : settings.Rate;
            if (effectiveRate != rate) { frameClock.SetRate(rate); effectiveRate = rate; }
            foreach (Overlay overlay in overlays)
            {
                overlay.Options = settings.ForScreen(overlay.DisplayName, battery || overlay.DisplayName == quietScreen);
                overlay.RequestTheme(settings.ThemeFor(overlay.DisplayName, DateTime.Today));
            }
        }

        internal void SetScreenTheme(string device, DesktopTheme theme)
        {
            if (theme == null) settings.ScreenThemes.Remove(device);
            else settings.ScreenThemes[device] = theme;
            SaveSettings();
        }

        internal void SetScreenDensity(string device, int density)
        {
            if (density < 0) settings.ScreenDensities.Remove(device);
            else settings.ScreenDensities[device] = density;
            SaveSettings();
        }

        internal void ToggleScene()
        {
            running = !running;
            tray.Text = running ? "Starlit Desk - Efekt açık" : "Starlit Desk - Duraklatıldı";
            if (running) SetHooks();
            else StopHooks();
            RefreshDesktop(null, EventArgs.Empty);
            SyncDashboard();
        }

        private Forms.ContextMenuStrip CreateMenu()
        {
            var menu = new Forms.ContextMenuStrip();
            var toggle = new Forms.ToolStripMenuItem("Duraklat", null, (sender, e) => ToggleScene());
            var wind = new Forms.ToolStripMenuItem("Rüzgâr", null, (sender, e) =>
            {
                settings.Wind = !settings.Wind;
                SaveSettings();
            });
            var sweep = new Forms.ToolStripMenuItem("İmleçle karı dağıt", null, (sender, e) =>
            {
                settings.CursorSweep = !settings.CursorSweep;
                SaveSettings();
            });
            var ledges = new Forms.ToolStripMenuItem("Pencere ve menülerde kar", null, delegate
            {
                if (settings.ThemeFor("", DateTime.Today) == DesktopTheme.Space)
                {
                    settings.SpaceOcclusion = !settings.SpaceOcclusion;
                    SaveSettings();
                }
                else SetWindowLedges(!settings.WindowLedges);
            });
            var startup = new Forms.ToolStripMenuItem("Windows ile başlat", null, (sender, e) => ToggleStartup());
            var themes = new Forms.ToolStripMenuItem("Masaüstü teması") { Available = DesktopTheme.All.Count > 1 };
            foreach (DesktopTheme theme in DesktopTheme.All)
            {
                var option = new Forms.ToolStripMenuItem(theme.Name, null, (sender, e) => SelectTheme(theme));
                themes.DropDownOpening += (sender, e) => option.Checked = settings.ThemeFor("", DateTime.Today) == theme;
                themes.DropDownItems.Add(option);
            }
            var screens = new Forms.ToolStripMenuItem("Ekranlar");
            FillScreenMenu(screens);
            screens.DropDownOpening += (sender, e) => FillScreenMenu(screens);
            var density = new Forms.ToolStripMenuItem("Kar yoğunluğu");
            AddOption(density, "Hafif", 0, () => settings.Density, value => settings.Density = value);
            AddOption(density, "Orta", 1, () => settings.Density, value => settings.Density = value);
            AddOption(density, "Yoğun", 2, () => settings.Density, value => settings.Density = value);
            var frameRate = new Forms.ToolStripMenuItem("Kare hızı");
            AddOption(frameRate, "Ekonomik (20 FPS)", 20, () => settings.Rate, SetFrameRate);
            AddOption(frameRate, "Dengeli (30 FPS)", 30, () => settings.Rate, SetFrameRate);
            AddOption(frameRate, "Akıcı (60 FPS)", 60, () => settings.Rate, SetFrameRate);
            var decorations = new Forms.ToolStripMenuItem("Süslemeler");
            AddEffect(decorations, "Yılbaşı ağaçları", Effect.Trees);
            AddEffect(decorations, "Noel Baba'nın kızağı", Effect.Sleigh);
            AddEffect(decorations, "Yürüyen Noel Baba", Effect.Santa);
            AddEffect(decorations, "Kar küreme aracı", Effect.Plow);
            AddEffect(decorations, "Teleferik", Effect.Gondola);
            var icicles = new Forms.ToolStripMenuItem("Buz sarkıtları", null, delegate
            {
                settings.Icicles = !settings.Icicles;
                SaveSettings();
            });
            decorations.DropDownItems.Add(icicles);
            decorations.DropDownOpening += delegate { icicles.Checked = settings.Icicles; };
            var manual = new Forms.ToolStripMenuItem("Efekt başlat");
            foreach (SnowAction action in SnowAction.All)
                manual.DropDownItems.Add(new Forms.ToolStripMenuItem(action.Name, null,
                    (sender, e) => RunSnowAction(action)));
            var orbitActions = new Forms.ToolStripMenuItem("Uzay efektleri");
            foreach (SpaceAction action in SpaceAction.All)
                orbitActions.DropDownItems.Add(new Forms.ToolStripMenuItem(action.Name, null,
                    (sender, e) => RunSpaceAction(action)));
            menu.Items.AddRange(new Forms.ToolStripItem[]
            {
                new Forms.ToolStripMenuItem("Kontrol panelini aç", null, (sender, e) => ShowDashboard()),
                toggle, new Forms.ToolStripSeparator(), themes, screens, density, frameRate, sweep, ledges, wind,
                decorations, manual, orbitActions, startup,
                new Forms.ToolStripSeparator(), new Forms.ToolStripMenuItem("Çıkış", null, (sender, e) => ExitThread())
            });
            menu.Opening += delegate
            {
                DesktopTheme theme = settings.ThemeFor("", DateTime.Today);
                bool winter = theme == DesktopTheme.Winter;
                bool space = theme == DesktopTheme.Space;
                screens.Available = Forms.Screen.AllScreens.Length > 1;
                decorations.Available = manual.Available = winter;
                orbitActions.Available = space;
                wind.Available = !space;
                density.Text = space ? "Yıldız yoğunluğu" : winter ? "Kar yoğunluğu" : theme == DesktopTheme.Summer ? "Yağmur yoğunluğu" :
                    theme == DesktopTheme.Autumn ? "Yaprak yoğunluğu" : "Çiçek yaprağı yoğunluğu";
                ledges.Text = space ? "Pencerelerin arkasında" : winter ? "Pencere ve menülerde kar" : theme == DesktopTheme.Summer ? "Pencerelerde su" :
                    theme == DesktopTheme.Autumn ? "Pencerelerde yaprak" : "Pencerelerde çiçek yaprağı";
                sweep.Text = space ? "Kozmik tozu imleçle savur" : winter ? "İmleçle karı dağıt" :
                    theme == DesktopTheme.Summer ? "İmleçle suyu dağıt" : "İmleçle yaprakları savur";
                toggle.Text = running ? "Duraklat" : "Devam et";
                wind.Checked = settings.Wind;
                sweep.Checked = settings.CursorSweep;
                ledges.Checked = space ? settings.SpaceOcclusion : settings.WindowLedges;
                startup.Checked = HasStartup();
            };
            return menu;
        }

        private void FillScreenMenu(Forms.ToolStripMenuItem menu)
        {
            while (menu.DropDownItems.Count != 0)
            {
                Forms.ToolStripItem old = menu.DropDownItems[0];
                menu.DropDownItems.RemoveAt(0);
                old.Dispose();
            }
            foreach (Forms.Screen screen in Forms.Screen.AllScreens)
            {
                string device = screen.DeviceName;
                menu.DropDownItems.Add(new Forms.ToolStripMenuItem(ScreenLabel(screen), null,
                    (sender, e) => SetScreenShown(device, !settings.ShowsOn(device))) { Checked = settings.ShowsOn(device) });
            }
        }

        /// <summary>"Ekran 2 · 2560×1440 · ana ekran", numbered like Windows display settings.</summary>
        internal static string ScreenLabel(Forms.Screen screen)
        {
            string name = screen.DeviceName;
            int digits = name.Length;
            while (digits > 0 && char.IsDigit(name[digits - 1])) digits--;
            string number = digits < name.Length ? name.Substring(digits) : "?";
            return "Ekran " + number + " · " + screen.Bounds.Width + "×" + screen.Bounds.Height +
                (screen.Primary ? " · ana ekran" : "");
        }

        internal void RunSnowAction(SnowAction action)
        {
            if (action == SnowAction.ClearTrees)
            {
                foreach (Overlay overlay in overlays)
                    if (overlay.Snow != null) action.Apply(overlay.Snow);
                return;
            }

            Overlay target = ActionTarget(item => item.Snow != null);
            if (target != null) action.Apply(target.Snow);
        }

        internal void RunSeasonAction(SeasonAction action)
        {
            Overlay target = ActionTarget(item => item.Scene is SeasonScene &&
                item.Theme == (Array.IndexOf(SeasonAction.Spring, action) >= 0 ? DesktopTheme.Spring : DesktopTheme.Autumn));
            var scene = target == null ? null : target.Scene as SeasonScene;
            if (scene != null) action.Apply(scene);
        }

        internal void RunSummerAction(SummerAction action)
        {
            Overlay target = ActionTarget(item => item.Scene is SummerScene);
            if (target != null) action.Apply((SummerScene)target.Scene);
        }

        internal void RunSpaceAction(SpaceAction action)
        {
            Overlay target = ActionTarget(item => item.Scene is SpaceScene);
            if (target != null) action.Apply((SpaceScene)target.Scene);
        }

        /// <summary>Resumes the scene if needed and returns the visible display under the cursor.</summary>
        private Overlay ActionTarget(Predicate<Overlay> matches)
        {
            if (!running) ToggleScene();
            Desktop.GetCursorPos(out cursor);
            return overlays.Find(item => item.IsVisible && matches(item) && item.Contains(cursor)) ??
                overlays.Find(item => item.IsVisible && matches(item));
        }

        internal void SetWindowLedges(bool enabled)
        {
            settings.WindowLedges = enabled;
            RefreshDesktop(null, EventArgs.Empty);
            SaveSettings();
        }

        internal void SetFrameRate(int value)
        {
            settings.Rate = value;
            ApplyProfiles();
        }

        private void AddOption(Forms.ToolStripMenuItem group, string title, int value,
            Func<int> current, Action<int> change)
        {
            var item = new Forms.ToolStripMenuItem(title, null, delegate
            {
                change(value);
                SaveSettings();
            });
            group.DropDownOpening += (sender, e) => item.Checked = current() == value;
            group.DropDownItems.Add(item);
        }

        private void AddEffect(Forms.ToolStripMenuItem group, string title, Effect effect)
        {
            var item = new Forms.ToolStripMenuItem(title, null, delegate
            {
                settings.Effects ^= effect;
                SaveSettings();
            });
            group.DropDownOpening += (sender, e) => item.Checked = (settings.Effects & effect) != 0;
            group.DropDownItems.Add(item);
        }

        internal void SaveSettings()
        {
            try { settings.Save(); }
            catch (IOException error) { ShowTrayError("Ayarlar kaydedilemedi", error); }
            catch (UnauthorizedAccessException error) { ShowTrayError("Ayarlar kaydedilemedi", error); }
            if (overlays.Count != 0) RefreshDesktop(null, EventArgs.Empty);
            SyncDashboard();
        }

        internal static bool HasStartup()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(StartupKey))
                {
                    string value = key == null ? null : key.GetValue(StartupValue) as string;
                    string executable = "\"" + Forms.Application.ExecutablePath + "\"";
                    return string.Equals(value, executable, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(value, executable + " --background", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (UnauthorizedAccessException) { return false; }
            catch (System.Security.SecurityException) { return false; }
        }

        internal void ToggleStartup()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(StartupKey))
                {
                    if (HasStartup()) key.DeleteValue(StartupValue, false);
                    else key.SetValue(StartupValue, "\"" + Forms.Application.ExecutablePath + "\" --background");
                }
            }
            catch (IOException error) { ShowTrayError("Başlangıç ayarı değiştirilemedi", error); }
            catch (UnauthorizedAccessException error) { ShowTrayError("Başlangıç ayarı değiştirilemedi", error); }
            catch (System.Security.SecurityException error) { ShowTrayError("Başlangıç ayarı değiştirilemedi", error); }
            SyncDashboard();
        }

        private void ShowTrayError(string title, Exception error)
        {
            tray.ShowBalloonTip(3500, title, error.Message, Forms.ToolTipIcon.Warning);
        }

    }
}
