using System;
using System.Drawing;
using Rect = System.Windows.Rect;

namespace SnowDesk
{
    internal struct TravelingParticle
    {
        internal double X, Y, VX, VY, Mass, Size, Angle, Spin;
        internal byte Kind, Tint;
    }

    internal interface TravelingScene
    {
        Func<TravelingParticle, bool> Send { set; }
        void Receive(TravelingParticle particle);
    }

    internal struct ScreenTransfer
    {
        internal TravelingScene Target;
        internal TravelingParticle Particle;

        internal static bool Between(Rectangle from, Rectangle to, ref TravelingParticle particle)
        {
            bool right = particle.X >= from.Width && particle.VX > 0 && from.Right == to.Left;
            bool left = particle.X < 0 && particle.VX < 0 && from.Left == to.Right;
            double desktopY = from.Top + particle.Y;
            if ((!left && !right) || desktopY < to.Top || desktopY >= to.Bottom) return false;
            particle.X = Math.Max(0.05, Math.Min(to.Width - 0.05, from.Left + particle.X - to.Left));
            particle.Y = desktopY - to.Top;
            return true;
        }
    }

    internal struct WindowMotion
    {
        private double previousX, previousY;
        internal double ThrowX, ThrowY;

        // Sample the change in velocity so a steady drag carries the load and stops/reversals loosen it.
        internal double Sample(Rect before, Rect after, double seconds)
        {
            double vx = (after.X - before.X) / Math.Max(0.016, seconds);
            double vy = (after.Y - before.Y) / Math.Max(0.016, seconds);
            double acceleration = Length(vx - previousX, vy - previousY);
            double speed = Length(vx, vy);
            double change = Math.Max(0, acceleration - 110);
            double fraction = Math.Min(0.68, change / 2100 + Math.Max(0, speed - 320) / 9800);
            ThrowX = vx + (previousX - vx) * 1.25;
            ThrowY = vy + (previousY - vy) * 1.25;
            previousX = vx;
            previousY = vy;
            return fraction;
        }

        private static double Length(double x, double y) { return Math.Sqrt(x * x + y * y); }
    }

    internal struct SurfaceRim
    {
        internal double Radius, Left, Right;

        internal double At(double x)
        {
            if (Radius <= 0) return 0;
            double edge = Math.Max(0, Math.Min(x - Left, Right - x));
            if (edge >= Radius) return 0;
            double circleX = Radius - edge;
            return Radius - Math.Sqrt(Radius * Radius - circleX * circleX) + 0.45;
        }
    }

    internal struct CursorImpulse
    {
        internal double DX, DY, Distance, Speed, Strength;

        internal static CursorImpulse Measure(Desktop.Point start, Desktop.Point end, double seconds)
        {
            double x = (double)end.X - start.X, y = (double)end.Y - start.Y;
            double distance = Math.Sqrt(x * x + y * y);
            double speed = distance / Math.Max(0.008, seconds);
            return new CursorImpulse
            {
                DX = x, DY = y, Distance = distance, Speed = speed,
                Strength = Math.Max(0, Math.Min(1, (speed - 35) / 750))
            };
        }
    }
}
