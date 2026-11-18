using System;
using System.Collections.Generic;
using Forms = System.Windows.Forms;

namespace SnowDesk
{
    internal sealed class DesktopTheme
    {
        internal static readonly DesktopTheme Winter = new DesktopTheme(
            "winter", "Kış", "Pencerelerin üzerinde biriken kar ve kış süsleri.",
            display => new SnowField(display), () => new WinterPreview());

        internal static readonly IReadOnlyList<DesktopTheme> All = Array.AsReadOnly(new[] { Winter });

        internal readonly string Id;
        internal readonly string Name;
        internal readonly string Description;
        internal readonly Func<ThemePreview> CreatePreview;
        internal readonly Func<Forms.Screen, DesktopScene> CreateScene;

        private DesktopTheme(string id, string name, string description,
            Func<Forms.Screen, DesktopScene> createScene, Func<ThemePreview> createPreview = null)
        {
            Id = id;
            Name = name;
            Description = description;
            CreateScene = createScene;
            CreatePreview = createPreview;
        }

        internal static DesktopTheme FromId(string id)
        {
            foreach (DesktopTheme theme in All)
                if (string.Equals(theme.Id, id, StringComparison.Ordinal)) return theme;
            return Winter;
        }

        public override string ToString() { return Name; }
    }

    internal interface DesktopScene : IDisposable
    {
        bool UsesWindowGeometry(Settings settings);
        void UpdateLedges(List<Desktop.WindowBounds> windows);
        void ResetPointer();
        void Advance(double seconds, Settings settings, Desktop.Point pointer);
        void Draw(Canvas drawing);
    }
}
