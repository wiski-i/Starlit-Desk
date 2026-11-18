using System;
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
                "density={0}\nfps={1}\nwind={2}\neffects={3}\nsweep={4}\nledges={5}\nicicles={6}\ntheme={7}\nsummer={8}\n",
                Density, Rate, Wind ? 1 : 0, (int)Effects, CursorSweep ? 1 : 0, WindowLedges ? 1 : 0,
                Icicles ? 1 : 0, Theme.Id, (int)Summer));
            if (File.Exists(filePath)) File.Replace(temp, filePath, null);
            else File.Move(temp, filePath);
        }
    }
}
