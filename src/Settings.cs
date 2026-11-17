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

    internal sealed class Settings
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SnowDeskWindows", "settings.ini");

        internal int Density = 1;
        internal int Rate = 30;
        internal bool Wind = true;
        internal bool CursorSweep = true;
        internal bool WindowLedges = true;
        internal bool Icicles = true;
        internal Effect Effects = Effect.Trees | Effect.Sleigh | Effect.Santa | Effect.Plow;

        internal bool Has(Effect effect)
        {
            return (Effects & effect) != 0;
        }

        internal static Settings Load()
        {
            var settings = new Settings();
            try
            {
                foreach (string line in File.ReadLines(FilePath))
                {
                    string[] parts = line.Split('=');
                    int value;
                    if (parts.Length != 2 || !int.TryParse(parts[1], NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out value)) continue;
                    string key = parts[0];
                    if (key == "density" && value >= 0 && value <= 2) settings.Density = value;
                    if (key == "fps" && (value == 20 || value == 30 || value == 60)) settings.Rate = value;
                    if (key == "wind") settings.Wind = value != 0;
                    if (key == "sweep") settings.CursorSweep = value != 0;
                    if (key == "ledges") settings.WindowLedges = value != 0;
                    if (key == "icicles") settings.Icicles = value != 0;
                    if (key == "effects") settings.Effects = (Effect)(value & 31);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return settings;
        }

        internal void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, string.Format(CultureInfo.InvariantCulture,
                "density={0}\nfps={1}\nwind={2}\neffects={3}\nsweep={4}\nledges={5}\nicicles={6}\n",
                Density, Rate, Wind ? 1 : 0, (int)Effects, CursorSweep ? 1 : 0, WindowLedges ? 1 : 0, Icicles ? 1 : 0));
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, null);
            else File.Move(temp, FilePath);
        }
    }
}
