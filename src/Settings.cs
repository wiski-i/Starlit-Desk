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
        internal Effect Effects = Effect.Trees | Effect.Sleigh | Effect.Santa | Effect.Plow;
        internal SummerEffect Summer = SummerEffect.All;
        internal AutumnEffect Autumn = AutumnEffect.All;
        internal SpringEffect Spring = SpringEffect.All;

        /// <summary>Device names (Screen.DeviceName) of displays the user switched the effect off on.</summary>
        internal readonly HashSet<string> HiddenScreens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private Settings(string filePath)
        {
            this.filePath = filePath;
        }

        internal bool Has(Effect effect)
        {
            return (Effects & effect) != 0;
        }

        internal bool Has(SummerEffect effect)
        {
            return (Summer & effect) != 0;
        }

        internal bool Has(AutumnEffect effect) { return (Autumn & effect) != 0; }
        internal bool Has(SpringEffect effect) { return (Spring & effect) != 0; }

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
            if (File.Exists(filePath)) File.Replace(temp, filePath, null);
            else File.Move(temp, filePath);
        }
    }
}
