using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace SnowDesk
{
    [Flags]
    internal enum Effect
    {
        Trees = 1,
        Sleigh = 2,
        Santa = 4,
        Plow = 8,
        Gondola = 16
    }

    /// <summary>Optional pieces of the summer theme; stored as the "summer" bit mask.</summary>
    [Flags]
    internal enum SummerEffect
    {
        Bather = 1,
        Runner = 2,
        Pump = 4,
        Pond = 8,
        Duck = 16,
        Rainbow = 32,
        All = 63
    }

    /// <summary>Optional pieces of the autumn theme; stored as the "autumn2" bit mask.</summary>
    [Flags]
    internal enum AutumnEffect
    {
        Raker = 1,
        Kid = 2,
        Squirrel = 4,
        Tree = 8,
        Sweeper = 16,
        Gale = 32,
        All = 63
    }

    /// <summary>Optional pieces of the spring theme; stored as the "spring2" bit mask.</summary>
    [Flags]
    internal enum SpringEffect
    {
        Butterflies = 1,
        Bees = 2,
        Gardener = 4,
        Birds = 8,
        Garden = 16,
        Tree = 32,
        Car = 64,
        Beekeeper = 128,
        All = 255
    }

    [Flags]
    internal enum SpaceEffect
    {
        Planet = 1,
        Satellite = 2,
        Meteors = 4,
        Aurora = 8,
        Dust = 16,
        All = 31
    }

    internal sealed class Settings
    {
        private static readonly string DefaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SnowDeskWindows", "settings.ini");
        private readonly string filePath;

        internal DesktopTheme Theme = DesktopTheme.Winter;
        internal int Density = 1;
        internal int Rate = 30;
        internal bool Wind = true;
        internal bool CursorSweep = true;
        internal bool WindowLedges = true;
        internal bool Icicles = true;
        internal int Accumulation = 2;
        internal int Melt = 1;
        internal int CalendarMode;
        internal bool IconDust;
        internal bool QuietScene;
        internal bool BatterySaver = true;
        internal string PausedApps = "", QuietApps = "";
        internal readonly Dictionary<string, DesktopTheme> ScreenThemes = new Dictionary<string, DesktopTheme>(StringComparer.OrdinalIgnoreCase);
        internal readonly Dictionary<string, int> ScreenDensities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        internal double DepositScale { get { return Accumulation == 0 ? 0 : Accumulation == 1 ? 0.52 : Accumulation == 2 ? 1 : 1.55; } }
        internal double MeltScale { get { return Melt == 0 ? 0.48 : Melt == 1 ? 1 : 2.1; } }

        internal DesktopTheme ThemeFor(string device, DateTime date)
        {
            DesktopTheme theme;
            if (ScreenThemes.TryGetValue(device, out theme)) return theme;
            if (CalendarMode == 0) return Theme;
            int month = (date.Month - 1 + (CalendarMode == 2 ? 6 : 0)) % 12 + 1;
            if (month >= 3 && month <= 5) return DesktopTheme.Spring;
            if (month >= 6 && month <= 8) return DesktopTheme.Summer;
            return month >= 9 && month <= 11 ? DesktopTheme.Autumn : DesktopTheme.Winter;
        }

        internal Settings ForScreen(string device, bool conserve)
        {
            int density;
            if (!ScreenDensities.TryGetValue(device, out density)) density = Density;
            if (conserve) density = 0;
            if (density == Density && (!conserve || QuietScene)) return this;
            var effective = (Settings)MemberwiseClone();
            effective.Density = density;
            effective.QuietScene |= conserve;
            return effective;
        }

        internal static string AppList(string input)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string entry in input.Split(new[] { '|', ';', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string name = entry.Trim();
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4);
                if (name.Length > 0 && name.IndexOfAny(new[] { '\\', '/', ':', '=' }) < 0) names.Add(name);
            }
            return string.Join("|", names);
        }
        internal Effect Effects = Effect.Trees | Effect.Sleigh | Effect.Santa | Effect.Plow;
        internal SummerEffect Summer = SummerEffect.All;
        internal AutumnEffect Autumn = AutumnEffect.All;
        internal SpringEffect Spring = SpringEffect.All;
        internal SpaceEffect Space = SpaceEffect.All;
        internal int SpaceLook;
        internal bool SpaceOcclusion = true;

        /// <summary>Device names (Screen.DeviceName) of displays the user switched the effect off on.</summary>
        internal readonly HashSet<string> HiddenScreens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private Settings(string filePath)
        {
            this.filePath = filePath;
        }

        internal bool Has(Effect effect)
        {
            return !QuietScene && (Effects & effect) != 0;
        }

        internal bool Has(SummerEffect effect)
        {
            return (!QuietScene || effect == SummerEffect.Rainbow) && (Summer & effect) != 0;
        }

        internal bool Has(AutumnEffect effect) { return !QuietScene && (Autumn & effect) != 0; }
        internal bool Has(SpringEffect effect) { return !QuietScene && (Spring & effect) != 0; }

        internal bool ShowsOn(string device)
        {
            return !HiddenScreens.Contains(device);
        }

        internal static Settings Load(string path = null)
        {
            var settings = new Settings(path ?? DefaultPath);
            try
            {
                foreach (string line in File.ReadLines(settings.filePath))
                {
                    string[] parts = line.Split('=');
                    if (parts.Length != 2) continue;
                    string key = parts[0];
                    if (key.StartsWith("screentheme:", StringComparison.Ordinal))
                    {
                        settings.ScreenThemes[key.Substring(12)] = DesktopTheme.FromId(parts[1]);
                        continue;
                    }
                    if (key == "pauseapps") { settings.PausedApps = AppList(parts[1]); continue; }
                    if (key == "quietapps") { settings.QuietApps = AppList(parts[1]); continue; }
                    if (key == "theme")
                    {
                        settings.Theme = DesktopTheme.FromId(parts[1]);
                        continue;
                    }
                    if (key == "hiddenscreens")
                    {
                        foreach (string device in parts[1].Split('|'))
                            if (device.Length > 0) settings.HiddenScreens.Add(device);
                        continue;
                    }
                    int value;
                    if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) continue;
                    if (key.StartsWith("screendensity:", StringComparison.Ordinal) && value >= 0 && value <= 2)
                        settings.ScreenDensities[key.Substring(14)] = value;
                    if (key == "accumulation" && value >= 0 && value <= 3) settings.Accumulation = value;
                    if (key == "melt" && value >= 0 && value <= 2) settings.Melt = value;
                    if (key == "calendar" && value >= 0 && value <= 2) settings.CalendarMode = value;
                    if (key == "icondust") settings.IconDust = value != 0;
                    if (key == "quietscene") settings.QuietScene = value != 0;
                    if (key == "batterysaver") settings.BatterySaver = value != 0;
                    if (key == "density" && value >= 0 && value <= 2) settings.Density = value;
                    if (key == "fps" && (value == 20 || value == 30 || value == 60)) settings.Rate = value;
                    if (key == "wind") settings.Wind = value != 0;
                    if (key == "sweep") settings.CursorSweep = value != 0;
                    if (key == "ledges") settings.WindowLedges = value != 0;
                    if (key == "icicles") settings.Icicles = value != 0;
                    if (key == "effects") settings.Effects = (Effect)(value & 31);
                    if (key == "summer") settings.Summer = (SummerEffect)(value & (int)SummerEffect.All);
                    if (key == "autumn2") settings.Autumn = (AutumnEffect)(value & (int)AutumnEffect.All);
                    if (key == "spring2") settings.Spring = (SpringEffect)(value & (int)SpringEffect.All);
                    if (key == "space") settings.Space = (SpaceEffect)(value & (int)SpaceEffect.All);
                    if (key == "spacelook" && value >= 0 && value <= 1) settings.SpaceLook = value;
                    if (key == "spaceocclusion") settings.SpaceOcclusion = value != 0;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return settings;
        }

        internal void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            string temp = filePath + ".tmp";
            File.WriteAllText(temp, string.Format(CultureInfo.InvariantCulture,
                "density={0}\nfps={1}\nwind={2}\neffects={3}\nsweep={4}\nledges={5}\nicicles={6}\ntheme={7}\nsummer={8}\nhiddenscreens={9}\nautumn2={10}\nspring2={11}\n",
                Density, Rate, Wind ? 1 : 0, (int)Effects, CursorSweep ? 1 : 0, WindowLedges ? 1 : 0,
                Icicles ? 1 : 0, Theme.Id, (int)Summer, string.Join("|", HiddenScreens), (int)Autumn, (int)Spring));
            using (var writer = File.AppendText(temp))
            {
                writer.WriteLine("accumulation=" + Accumulation);
                writer.WriteLine("melt=" + Melt);
                writer.WriteLine("calendar=" + CalendarMode);
                writer.WriteLine("icondust=" + (IconDust ? 1 : 0));
                writer.WriteLine("quietscene=" + (QuietScene ? 1 : 0));
                writer.WriteLine("batterysaver=" + (BatterySaver ? 1 : 0));
                writer.WriteLine("pauseapps=" + PausedApps);
                writer.WriteLine("quietapps=" + QuietApps);
                writer.WriteLine("space=" + (int)Space);
                writer.WriteLine("spacelook=" + SpaceLook);
                writer.WriteLine("spaceocclusion=" + (SpaceOcclusion ? 1 : 0));
                foreach (var entry in ScreenThemes) writer.WriteLine("screentheme:" + entry.Key + "=" + entry.Value.Id);
                foreach (var entry in ScreenDensities) writer.WriteLine("screendensity:" + entry.Key + "=" + entry.Value);
            }
            if (File.Exists(filePath)) File.Replace(temp, filePath, null);
            else File.Move(temp, filePath);
        }
    }
}
