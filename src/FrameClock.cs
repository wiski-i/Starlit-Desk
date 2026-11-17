using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace SnowDesk
{
    internal sealed class FrameClock : IDisposable
    {
        private readonly Action<Action> post;
        private readonly Action render;
        private readonly Action dispatch;
        private readonly AutoResetEvent changed = new AutoResetEvent(false);
        private readonly Thread worker;
        private volatile bool running, disposed;
        private volatile int rate;
        private int pending;

        internal FrameClock(Action<Action> post, Action render, int rate)
        {
            this.post = post;
            this.render = render;
            this.rate = rate;
            dispatch = Dispatch;
            worker = new Thread(Tick) { IsBackground = true, Name = "SnowDesk frame clock" };
            worker.Start();
        }

        internal bool Running { get { return running; } }

        internal void Start() { running = true; changed.Set(); }
        internal void Stop() { running = false; changed.Set(); }
        internal void SetRate(int framesPerSecond) { rate = framesPerSecond; changed.Set(); }

        private void Dispatch()
        {
            try { if (running && !disposed) render(); }
            finally { Interlocked.Exchange(ref pending, 0); }
        }

        private void Tick()
        {
            const uint TimerAccess = 0x00100002;
            const uint HighResolution = 2;
            IntPtr handle = CreateWaitableTimerEx(IntPtr.Zero, null, HighResolution, TimerAccess);
            if (handle == IntPtr.Zero)
                handle = CreateWaitableTimerEx(IntPtr.Zero, null, 0, TimerAccess);

            using (var timer = new NativeTimer(handle))
            {
                var signals = new WaitHandle[] { changed, timer };
                double next = Stopwatch.GetTimestamp();
                while (!disposed)
                {
                    if (!running)
                    {
                        changed.WaitOne();
                        next = Stopwatch.GetTimestamp();
                        continue;
                    }

                    double interval = (double)Stopwatch.Frequency / rate;
                    double remaining = next - Stopwatch.GetTimestamp();
                    if (remaining > 0)
                    {
                        long due = -Math.Max(1, (long)(remaining * 10000000 / Stopwatch.Frequency));
                        bool signal = handle != IntPtr.Zero &&
                            SetWaitableTimer(timer.SafeWaitHandle, ref due, 0, IntPtr.Zero, IntPtr.Zero, false)
                            ? WaitHandle.WaitAny(signals) == 0
                            : changed.WaitOne((int)Math.Ceiling(remaining * 1000 / Stopwatch.Frequency));
                        if (signal) next = Stopwatch.GetTimestamp();
                        continue;
                    }

                    if (Interlocked.CompareExchange(ref pending, 1, 0) == 0) post(dispatch);
                    double now = Stopwatch.GetTimestamp();
                    next += interval;
                    if (next <= now) next = now + interval;
                }
            }
        }

        public void Dispose()
        {
            disposed = true;
            running = false;
            changed.Set();
            worker.Join();
            changed.Dispose();
        }

        private sealed class NativeTimer : WaitHandle
        {
            internal NativeTimer(IntPtr handle)
            {
                SafeWaitHandle = new SafeWaitHandle(handle, true);
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWaitableTimerEx(IntPtr attributes, string name, uint flags, uint access);

        [DllImport("kernel32.dll")]
        private static extern bool SetWaitableTimer(SafeWaitHandle timer, ref long dueTime, int period,
            IntPtr completion, IntPtr argument, bool resume);
    }
}
