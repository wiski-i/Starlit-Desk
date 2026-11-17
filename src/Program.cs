using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("SnowDesk for Windows")]
[assembly: System.Reflection.AssemblyDescription("Offline desktop snowfall overlay")]
[assembly: System.Reflection.AssemblyVersion("1.11.0.0")]

namespace SnowDesk
{
    internal sealed class Program : Forms.ApplicationContext
    {
        private const string StartupKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupValue = "SnowDeskWindows";
        private readonly Settings settings = Settings.Load();
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
        private bool running = true;
        private bool sessionLocked;
        private bool geometryDirty;
        private IntPtr shellFocus;
        private volatile bool closing;
        private double lastFrame;
        private Desktop.Point cursor;

        [STAThread]
        private static void Main()
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
                        app.Start();
                        Forms.Application.Run(app);
                    }
                }
                catch (Exception error)
                {
                    Forms.MessageBox.Show("SnowDesk could not run. " + error.Message, "SnowDesk",
                        Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Error);
                }
                finally { instance.ReleaseMutex(); }
            }
        }

        private Program()
        {
            frameClock = new FrameClock(RunOnUiThread, Render, settings.Rate);
            windowsTimer = new Forms.Timer { Interval = 100 };
            windowsTimer.Tick += RefreshDesktop;
            windowsChanged = OnWindowEvent;
            icon = MakeIcon();
            tray = new Forms.NotifyIcon
            {
                Icon = icon,
                Text = "SnowDesk - Snow on",
                ContextMenuStrip = CreateMenu(),
                Visible = true
            };
            tray.MouseClick += delegate(object sender, Forms.MouseEventArgs e)
            {
                if (e.Button == Forms.MouseButtons.Left) ToggleSnow();
            };
        }

        private void Start()
        {
            uiThread.CreateControl();
            SystemEvents.DisplaySettingsChanged += DisplayChanged;
            SystemEvents.SessionSwitch += SessionChanged;
            RebuildDisplays();
            SetHooks();
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
                overlay.Field.Dispose();
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
                overlays.Add(new Overlay(screen));
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
                    if (overlay.IsVisible) overlay.Field.ResetPointer();
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

            var geometry = settings.WindowLedges ? Desktop.ReadWindows(ShellChanged, shellFocus) : new List<Desktop.WindowBounds>();
            foreach (Overlay overlay in overlays)
            {
                if (overlay.DisplayName == fullscreen) continue;
                overlay.Field.UpdateLedges(geometry);
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
                if (overlay.Field.IsHoldingTree)
                    overlay.Field.DragTree(cursor, elapsed, Desktop.PrimaryButtonDown());
                overlay.Field.Advance(elapsed, settings, cursor);
                overlay.RenderFrame(frameSurface);
                overlay.UpdateTreeTarget();
            }
        }

        private void ToggleSnow()
        {
            running = !running;
            tray.Text = running ? "SnowDesk - Snow on" : "SnowDesk - Paused";
            if (running) SetHooks();
            else StopHooks();
            RefreshDesktop(null, EventArgs.Empty);
        }

        private Forms.ContextMenuStrip CreateMenu()
        {
            var menu = new Forms.ContextMenuStrip();
            var toggle = new Forms.ToolStripMenuItem("Pause Snow", null, (sender, e) => ToggleSnow());
            var wind = new Forms.ToolStripMenuItem("Wind", null, (sender, e) =>
            {
                settings.Wind = !settings.Wind;
                SaveSettings();
            });
            var sweep = new Forms.ToolStripMenuItem("Sweep with Cursor", null, (sender, e) =>
            {
                settings.CursorSweep = !settings.CursorSweep;
                SaveSettings();
            });
            var ledges = new Forms.ToolStripMenuItem("Snow on Windows and Menus", null, (sender, e) =>
            {
                settings.WindowLedges = !settings.WindowLedges;
                SaveSettings();
                RefreshDesktop(null, EventArgs.Empty);
            });
            var startup = new Forms.ToolStripMenuItem("Start with Windows", null, (sender, e) => ToggleStartup());
            var density = new Forms.ToolStripMenuItem("Snow Density");
            AddOption(density, "Light", 0, () => settings.Density, value => settings.Density = value);
            AddOption(density, "Medium", 1, () => settings.Density, value => settings.Density = value);
            AddOption(density, "Heavy", 2, () => settings.Density, value => settings.Density = value);
            var frameRate = new Forms.ToolStripMenuItem("Frame Rate");
            AddOption(frameRate, "Eco (20 FPS)", 20, () => settings.Rate, SetFrameRate);
            AddOption(frameRate, "Balanced (30 FPS)", 30, () => settings.Rate, SetFrameRate);
            AddOption(frameRate, "Smooth (60 FPS)", 60, () => settings.Rate, SetFrameRate);
            var decorations = new Forms.ToolStripMenuItem("Decorations");
            AddEffect(decorations, "Christmas Trees", Effect.Trees);
            AddEffect(decorations, "Santa's Sleigh", Effect.Sleigh);
            AddEffect(decorations, "Walking Santa", Effect.Santa);
            AddEffect(decorations, "Snowplow", Effect.Plow);
            AddEffect(decorations, "Gondola", Effect.Gondola);
            var icicles = new Forms.ToolStripMenuItem("Icicles", null, delegate
            {
                settings.Icicles = !settings.Icicles;
                SaveSettings();
            });
            decorations.DropDownItems.Add(icicles);
            decorations.DropDownOpening += delegate { icicles.Checked = settings.Icicles; };
            var manual = new Forms.ToolStripMenuItem("Trigger Effect");
            AddAction(manual, "Drop a Christmas Tree", field => field.Trigger(Effect.Trees));
            AddAction(manual, "Santa Takes a Walk", field => field.Trigger(Effect.Santa));
            AddAction(manual, "Send the Sleigh", field => field.Trigger(Effect.Sleigh));
            AddAction(manual, "Call the Snowplow", field => field.Trigger(Effect.Plow));
            AddAction(manual, "Send the Gondola", field => field.Trigger(Effect.Gondola));
            AddAction(manual, "Gust of Wind", field => field.Gust());
            manual.DropDownItems.Add(new Forms.ToolStripSeparator());
            manual.DropDownItems.Add(new Forms.ToolStripMenuItem("Clear Trees", null, delegate
            {
                foreach (Overlay overlay in overlays) overlay.Field.ClearTrees();
            }));
            menu.Items.AddRange(new Forms.ToolStripItem[]
            {
                toggle, new Forms.ToolStripSeparator(), density, frameRate, sweep, ledges, wind,
                decorations, manual, startup,
                new Forms.ToolStripSeparator(), new Forms.ToolStripMenuItem("Exit SnowDesk", null, (sender, e) => ExitThread())
            });
            menu.Opening += delegate
            {
                toggle.Text = running ? "Pause Snow" : "Resume Snow";
                wind.Checked = settings.Wind;
                sweep.Checked = settings.CursorSweep;
                ledges.Checked = settings.WindowLedges;
                startup.Checked = HasStartup();
            };
            return menu;
        }

        private void AddAction(Forms.ToolStripMenuItem group, string title, Action<SnowField> action)
        {
            group.DropDownItems.Add(new Forms.ToolStripMenuItem(title, null, delegate
            {
                if (!running) ToggleSnow();
                Desktop.GetCursorPos(out cursor);
                Overlay target = overlays.Find(item => item.IsVisible && item.Contains(cursor)) ??
                    overlays.Find(item => item.IsVisible);
                if (target != null) action(target.Field);
            }));
        }

        private void SetFrameRate(int value)
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

        private void SaveSettings()
        {
            try { settings.Save(); }
            catch (IOException error) { ShowTrayError("Could not save settings", error); }
            catch (UnauthorizedAccessException error) { ShowTrayError("Could not save settings", error); }
        }

        private static bool HasStartup()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(StartupKey))
                return key != null && string.Equals(key.GetValue(StartupValue) as string,
                    "\"" + Forms.Application.ExecutablePath + "\"", StringComparison.OrdinalIgnoreCase);
        }

        private void ToggleStartup()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(StartupKey))
                {
                    if (HasStartup()) key.DeleteValue(StartupValue, false);
                    else key.SetValue(StartupValue, "\"" + Forms.Application.ExecutablePath + "\"");
                }
            }
            catch (IOException error) { ShowTrayError("Could not change startup settings", error); }
            catch (UnauthorizedAccessException error) { ShowTrayError("Could not change startup settings", error); }
            catch (System.Security.SecurityException error) { ShowTrayError("Could not change startup settings", error); }
        }

        private void ShowTrayError(string title, Exception error)
        {
            tray.ShowBalloonTip(3500, title, error.Message, Forms.ToolTipIcon.Warning);
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr icon);

        private static Drawing.Icon MakeIcon()
        {
            using (var bitmap = new Drawing.Bitmap(32, 32))
            using (var canvas = Drawing.Graphics.FromImage(bitmap))
            using (var outline = new Drawing.Pen(Drawing.Color.FromArgb(255, 39, 72, 107), 4.6f))
            using (var snow = new Drawing.Pen(Drawing.Color.FromArgb(255, 210, 242, 255), 2.2f))
            {
                canvas.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
                for (int pass = 0; pass < 2; pass++)
                {
                    Drawing.Pen pen = pass == 0 ? outline : snow;
                    for (int spoke = 0; spoke < 6; spoke++)
                    {
                        double angle = spoke * Math.PI / 3;
                        float x = (float)Math.Cos(angle), y = (float)Math.Sin(angle);
                        canvas.DrawLine(pen, 16, 16, 16 + 12 * x, 16 + 12 * y);
                        canvas.DrawLine(pen, 16 + 8 * x, 16 + 8 * y, 16 + 11 * x - 3 * y, 16 + 11 * y + 3 * x);
                        canvas.DrawLine(pen, 16 + 8 * x, 16 + 8 * y, 16 + 11 * x + 3 * y, 16 + 11 * y - 3 * x);
                    }
                }
                IntPtr handle = bitmap.GetHicon();
                try { return (Drawing.Icon)Drawing.Icon.FromHandle(handle).Clone(); }
                finally { DestroyIcon(handle); }
            }
        }
    }
}
