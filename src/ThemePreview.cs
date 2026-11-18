using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace SnowDesk
{
    internal sealed class ThemePreview : IDisposable
    {
        private const int FrameDelayTag = 0x5100;
        private readonly Stream source;
        private readonly int frameCount;
        private readonly byte[] delays;
        private int frame;
        internal readonly Image Image;

        private ThemePreview(Stream source)
        {
            this.source = source;
            Image = Image.FromStream(source);
            frameCount = Image.GetFrameCount(FrameDimension.Time);
            if (Array.IndexOf(Image.PropertyIdList, FrameDelayTag) >= 0)
                delays = Image.GetPropertyItem(FrameDelayTag).Value;
        }

        internal static ThemePreview Load(string resource)
        {
            if (resource == null) return null;
            Stream source = typeof(ThemePreview).Assembly.GetManifestResourceStream(resource);
            if (source == null) return null;
            try { return new ThemePreview(source); }
            catch
            {
                source.Dispose();
                throw;
            }
        }

        internal bool IsAnimated { get { return frameCount > 1; } }

        internal int Delay
        {
            get
            {
                int offset = frame * sizeof(int);
                if (delays == null || delays.Length < offset + sizeof(int)) return 100;
                int duration = BitConverter.ToInt32(delays, offset);
                return Math.Max(20, Math.Min(6000, duration) * 10);
            }
        }

        internal void Advance()
        {
            frame = (frame + 1) % frameCount;
            Image.SelectActiveFrame(FrameDimension.Time, frame);
        }

        internal static bool WindowsAllowsAnimation()
        {
            const uint GetClientAreaAnimation = 0x1042;
            int enabled;
            return SystemParametersInfo(GetClientAreaAnimation, 0, out enabled, 0) && enabled != 0;
        }

        public void Dispose()
        {
            Image.Dispose();
            source.Dispose();
        }

        [DllImport("user32.dll")]
        private static extern bool SystemParametersInfo(uint action, uint parameter, out int value, uint flags);
    }
}
