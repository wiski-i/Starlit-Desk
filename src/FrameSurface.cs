using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SnowDesk
{
    // Windows copies this source when each layered window is presented.
    // The UI thread can therefore reuse it for the next display.
    internal sealed class FrameSurface : IDisposable
    {
        private Bitmap bitmap;
        private IntPtr dib, previous, pixels;

        internal IntPtr DeviceContext { get; private set; }
        internal Canvas Canvas { get; private set; }
        internal readonly Size Size;

        internal FrameSurface(Size size)
        {
            Size = size;
            try
            {
                DeviceContext = CreateCompatibleDC(IntPtr.Zero);
                if (DeviceContext == IntPtr.Zero) throw new Win32Exception();
                var info = new BitmapInfo { HeaderSize = 40, Width = size.Width, Height = -size.Height, Planes = 1, Bits = 32 };
                dib = CreateDIBSection(DeviceContext, ref info, 0, out pixels, IntPtr.Zero, 0);
                if (dib == IntPtr.Zero) throw new Win32Exception();
                previous = SelectObject(DeviceContext, dib);
                if (previous == IntPtr.Zero || previous == new IntPtr(-1)) throw new Win32Exception();
                bitmap = new Bitmap(size.Width, size.Height, size.Width * 4, PixelFormat.Format32bppPArgb, pixels);
                Canvas = new Canvas(bitmap, ClearPixels);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void ClearPixels(int width, int height)
        {
            int stride = checked(Size.Width * 4);
            if (width == Size.Width)
            {
                Memset(pixels, 0, new UIntPtr(checked((uint)(stride * height))));
                return;
            }
            uint bytes = checked((uint)(width * 4));
            for (int y = 0; y < height; y++)
                Memset(IntPtr.Add(pixels, y * stride), 0, new UIntPtr(bytes));
        }

        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "memset")]
        private static extern IntPtr Memset(IntPtr target, int value, UIntPtr length);

        public void Dispose()
        {
            if (Canvas != null) Canvas.Dispose();
            if (bitmap != null) bitmap.Dispose();
            if (previous != IntPtr.Zero && previous != new IntPtr(-1)) SelectObject(DeviceContext, previous);
            if (dib != IntPtr.Zero) DeleteObject(dib);
            if (DeviceContext != IntPtr.Zero) DeleteDC(DeviceContext);
            Canvas = null;
            bitmap = null;
            DeviceContext = dib = previous = IntPtr.Zero;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfo
        {
            internal int HeaderSize, Width, Height;
            internal short Planes, Bits;
            internal int Compression, ByteSize, XResolution, YResolution, ColorsUsed, ColorImportant;
        }

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hwnd);
        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, int colorMode,
            out IntPtr pixels, IntPtr section, int offset);
        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr dc, IntPtr gdiObject);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr gdiObject);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr dc);
    }
}
