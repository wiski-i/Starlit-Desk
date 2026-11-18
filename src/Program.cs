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
[assembly: System.Reflection.AssemblyVersion("1.11.0.0")]

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
        private readonly Forms.Control uiThread = new Forms.Control();
        private readonly Desktop.EventHandler windowsChanged;
        private readonly Forms.NotifyIcon tray;
        private readonly Drawing.Icon icon;
        private DeskWindow dashboard;
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
            frameClock = new FrameClock(RunOnUiThread, Render, settings.Rate);
            windowsTimer = new Forms.Timer { Interval = 100 };
            windowsTimer.Tick += RefreshDesktop;
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
                StopHooks();
                frameClock.Dispose();
                windowsTimer.Dispose();
                CloseDisplays();
                tray.Visible = false;
                tray.ContextMenuStrip.Dispose();
                tray.Dispose();
                if (dashboard != null) dashboard.Dispose();
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
                overlays.Add(new Overlay(screen, settings.Theme));
            RefreshDesktop(null, EventArgs.Empty);
        }

        private void RefreshDesktop(object sender, EventArgs e)
        {
            windowsTimer.Stop();
            geometryDirty = false;
            bool active = running && !sessionLocked;
            string fullscreen = active ? Desktop.FullscreenScreen() : null;
            bool onDesktop = active && Desktop.DesktopHasFocus(Desktop.GetForegroundWindow());

            var size = new Drawing.Size();
            foreach (Overlay overlay in overlays)
            {
                if (!active || overlay.DisplayName == fullscreen)
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

            bool needsGeometry = overlays.Exists(item => item.Scene.UsesWindowGeometry(settings));
            var geometry = needsGeometry ? Desktop.ReadWindows(ShellChanged, shellFocus) : new List<Desktop.WindowBounds>();
            foreach (Overlay overlay in overlays)
            {
                if (overlay.DisplayName == fullscreen) continue;
                overlay.Scene.UpdateLedges(geometry);
                overlay.Reveal(frameSurface);
                var popup = geometry.Find(item => item.IsShellPopup && overlay.Contains(new Desktop.Point
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
            foreach (Overlay overlay in overlays)
            {
                if (!overlay.IsVisible) continue;
                SnowField snow = overlay.Snow;
                if (snow != null && snow.IsHoldingTree)
                    snow.DragTree(cursor, elapsed, Desktop.PrimaryButtonDown());
                overlay.Scene.Advance(elapsed, settings, cursor);
                overlay.RenderFrame(frameSurface);
                overlay.UpdateTreeTarget();
            }
        }

        internal Settings Preferences { get { return settings; } }
        internal bool IsRunning { get { return running; } }

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
            if (theme == settings.Theme) return;
            settings.Theme = theme;
            PopupSurfaces.Clear();
            RebuildDisplays();
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
            var ledges = new Forms.ToolStripMenuItem("Pencere ve menülerde kar", null, (sender, e) =>
                SetWindowLedges(!settings.WindowLedges));
            var startup = new Forms.ToolStripMenuItem("Windows ile başlat", null, (sender, e) => ToggleStartup());
            var themes = new Forms.ToolStripMenuItem("Masaüstü teması") { Available = DesktopTheme.All.Count > 1 };
            foreach (DesktopTheme theme in DesktopTheme.All)
            {
                var option = new Forms.ToolStripMenuItem(theme.Name, null, (sender, e) => SelectTheme(theme));
                themes.DropDownOpening += (sender, e) => option.Checked = settings.Theme == theme;
                themes.DropDownItems.Add(option);
            }
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
            menu.Items.AddRange(new Forms.ToolStripItem[]
            {
                new Forms.ToolStripMenuItem("Kontrol panelini aç", null, (sender, e) => ShowDashboard()),
                toggle, new Forms.ToolStripSeparator(), themes, density, frameRate, sweep, ledges, wind,
                decorations, manual, startup,
                new Forms.ToolStripSeparator(), new Forms.ToolStripMenuItem("Çıkış", null, (sender, e) => ExitThread())
            });
            menu.Opening += delegate
            {
                bool winter = settings.Theme == DesktopTheme.Winter;
                density.Available = sweep.Available = ledges.Available = wind.Available =
                    decorations.Available = manual.Available = winter;
                toggle.Text = running ? "Duraklat" : "Devam et";
                wind.Checked = settings.Wind;
                sweep.Checked = settings.CursorSweep;
                ledges.Checked = settings.WindowLedges;
                startup.Checked = HasStartup();
            };
            return menu;
        }

        internal void RunSnowAction(SnowAction action)
        {
            if (settings.Theme != DesktopTheme.Winter) return;
            if (action == SnowAction.ClearTrees)
            {
                foreach (Overlay overlay in overlays) action.Apply(overlay.Snow);
                return;
            }

            if (!running) ToggleScene();
            Desktop.GetCursorPos(out cursor);
            Overlay target = overlays.Find(item => item.IsVisible && item.Contains(cursor)) ??
                overlays.Find(item => item.IsVisible);
            if (target != null) action.Apply(target.Snow);
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
            frameClock.SetRate(value);
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
            group.DropDownOpening += (sender, e) => item.Checked = settings.Has(effect);
            group.DropDownItems.Add(item);
        }

        internal void SaveSettings()
        {
            try { settings.Save(); }
            catch (IOException error) { ShowTrayError("Ayarlar kaydedilemedi", error); }
            catch (UnauthorizedAccessException error) { ShowTrayError("Ayarlar kaydedilemedi", error); }
            SyncDashboard();
        }

        internal static bool HasStartup()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(StartupKey))
            {
                string value = key == null ? null : key.GetValue(StartupValue) as string;
                string executable = "\"" + Forms.Application.ExecutablePath + "\"";
                return string.Equals(value, executable, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, executable + " --background", StringComparison.OrdinalIgnoreCase);
            }
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
