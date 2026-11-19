using System;
using System.Drawing;

namespace SnowDesk
{
    /// <summary>
    /// Characters of the summer scene. The bather walks to the main puddle once it fills, floats in her ring
    /// under an umbrella and runs off when the water goes; the pump drains that puddle after a shower or when it
    /// overflows; a runner crosses in the rain; a frog hops between lily pads on the pond and a duck bobs along.
    /// </summary>
    internal sealed class SummerCast
    {
        private enum Stage { Away, Arriving, Busy, Leaving }

        private static readonly double[] PadSpots = { -0.58, 0.04, 0.52 };
        private readonly SummerScene scene;
        private Stage bather = Stage.Away, pump = Stage.Away;
        private double batherX, batherDirection = 1, batherStep, batherReady;
        private bool batherInvited;
        private double pumpX, pumpDirection = 1, pumpStop, hose, overflow, tankFill, pumpedFrom;
        private bool runnerActive;
        private double runnerX, runnerDirection = 1, runnerStep, nextRunner = 14;
        private int frogPad = 1, frogTarget = 1;
        private double frogHop = 1, nextHop = 4;
        private double clock;
        private bool pondVisible = true, duckVisible = true;

        internal SummerCast(SummerScene scene)
        {
            this.scene = scene;
        }

        private double S { get { return scene.Scale; } }

        internal void OnSunshine()
        {
            if (pump == Stage.Away && scene.Main.Volume > 0.35 && scene.Random.NextDouble() < 0.6) CallPump();
        }

        internal void CallPump()
        {
            if (pump != Stage.Away || scene.Main.Volume < 0.05) return;
            pumpDirection = scene.Main.X < scene.Width * 0.5 ? 1 : -1;
            pumpX = pumpDirection > 0 ? -50 * S : scene.Width + 50 * S;
            pump = Stage.Arriving;
            tankFill = 0.08;
            pumpedFrom = -1;
        }

        internal void SendRunner()
        {
            if (runnerActive) return;
            runnerDirection = scene.Random.Next(2) == 0 ? -1 : 1;
            runnerX = runnerDirection > 0 ? -30 * S : scene.Width + 30 * S;
            runnerActive = true;
        }

        internal void InviteBather()
        {
            batherInvited = true;
            batherReady = 0;
        }

        internal void Advance(double dt, Settings settings)
        {
            clock += dt * scene.Pace;
            pondVisible = settings.Has(SummerEffect.Pond);
            duckVisible = settings.Has(SummerEffect.Duck);
            AdvanceBather(dt, settings.Has(SummerEffect.Bather) || batherInvited);
            AdvancePump(dt, settings.Has(SummerEffect.Pump));
            AdvanceRunner(dt, settings.Has(SummerEffect.Runner));
            AdvanceFrog(dt);
        }

        private void AdvanceBather(double dt, bool enabled)
        {
            SummerScene.Pool pool = scene.Main;
            double surface = scene.Ground - pool.Depth(S) * 0.5;
            switch (bather)
            {
                case Stage.Away:
                    if (!enabled || pool.Volume < 0.5 || clock < batherReady || pump != Stage.Away) return;
                    batherDirection = pool.X < scene.Width * 0.5 ? 1 : -1;
                    batherX = batherDirection > 0 ? -30 * S : scene.Width + 30 * S;
                    bather = Stage.Arriving;
                    return;
                case Stage.Arriving:
                    batherX += batherDirection * 78 * S * dt;
                    batherStep += dt * 2.6;
                    if (pool.Volume < 0.25 || !enabled) Leave();
                    else if ((batherX - pool.X) * batherDirection >= 0)
                    {
                        bather = Stage.Busy;
                        scene.Splash(pool.X, surface, 9, 1.2);
                    }
                    return;
                case Stage.Busy:
                    batherX = pool.X + Math.Sin(clock * 0.5) * Math.Min(7 * S, pool.Half * 0.18);
                    if (scene.Random.NextDouble() < dt * 0.7) scene.Splash(batherX + 18 * S, surface, 2, 0.7);
                    if (pool.Volume < 0.22 || !enabled)
                    {
                        scene.Splash(batherX, surface, 7, 1.1);
                        Leave();
                    }
                    return;
                case Stage.Leaving:
                    batherX += batherDirection * 205 * S * dt;
                    batherStep += dt * 5.2;
                    if (batherX < -60 * S || batherX > scene.Width + 60 * S)
                    {
                        bather = Stage.Away;
                        batherInvited = false;
                        batherReady = clock + 25;
                    }
                    return;
            }
        }

        private void Leave()
        {
            bather = Stage.Leaving;
            batherDirection = batherX < scene.Width * 0.5 ? -1 : 1;
        }

        private void AdvancePump(double dt, bool enabled)
        {
            SummerScene.Pool pool = scene.Main;
            pool.Drain = 0;
            if (pump == Stage.Away)
            {
                overflow = scene.Raining && pool.Volume >= 0.999 ? overflow + dt * scene.Pace : 0;
                if (enabled && overflow > 14) CallPump();
                return;
            }
            // The stop is fixed on arrival; the truck stays parked while the puddle shrinks.
            if (pump == Stage.Arriving) pumpStop = pool.X - pumpDirection * (pool.Half + 46 * S);
            if (pump == Stage.Arriving)
            {
                pumpX += pumpDirection * 95 * S * dt;
                if ((pumpX - pumpStop) * pumpDirection >= 0)
                {
                    pumpX = pumpStop;
                    pump = Stage.Busy;
                }
            }
            else if (pump == Stage.Busy)
            {
                pumpX = pumpStop;
                hose = Math.Min(1, hose + dt * 1.5);
                if (hose >= 1)
                {
                    // The truck stores what it drains: the tank gauge rises as the puddle shrinks.
                    if (pumpedFrom < 0) pumpedFrom = Math.Max(0.05, pool.Volume);
                    pool.Drain = 0.075 * scene.Pace;
                    tankFill = Math.Max(tankFill, 0.08 + 0.86 * Math.Min(1, 1 - pool.Volume / pumpedFrom));
                }
                if (pool.Volume < 0.02)
                {
                    hose = Math.Max(0, hose - dt * 3);
                    if (hose <= 0)
                    {
                        pump = Stage.Leaving;
                        pumpDirection = -pumpDirection;
                    }
                }
            }
            else
            {
                pumpX += pumpDirection * 120 * S * dt;
                if (pumpX < -70 * S || pumpX > scene.Width + 70 * S) pump = Stage.Away;
            }
        }

        private void AdvanceRunner(double dt, bool enabled)
        {
            if (!runnerActive)
            {
                if (enabled && scene.Raining && clock >= nextRunner)
                {
                    SendRunner();
                    nextRunner = clock + 24 + scene.Random.NextDouble() * 20;
                }
                return;
            }
            runnerX += runnerDirection * 235 * S * dt;
            runnerStep += dt * 6;
            foreach (SummerScene.Pool pool in scene.Pools)
                if (!double.IsNaN(pool.SurfaceAt(runnerX, scene.Ground, S)) && scene.Random.NextDouble() < dt * 22)
                    scene.Splash(runnerX, scene.Ground - 2, 3, 1.2);
            if (runnerX < -50 * S || runnerX > scene.Width + 50 * S) runnerActive = false;
        }

        private void AdvanceFrog(double dt)
        {
            if (frogHop < 1)
            {
                frogHop = Math.Min(1, frogHop + dt * 1.8);
                if (frogHop >= 1)
                {
                    frogPad = frogTarget;
                    double x = PadX(frogPad);
                    scene.Splash(x, scene.Ground - scene.Pond.Depth(S), 3, 0.6);
                }
                return;
            }
            if (clock < nextHop || Grow(scene.Pond) < 0.6) return;
            frogTarget = (frogPad + 1 + scene.Random.Next(2)) % PadSpots.Length;
            frogHop = 0;
            nextHop = clock + 4 + scene.Random.NextDouble() * 4;
        }

        private static double Grow(SummerScene.Pool pool)
        {
            return Math.Max(0, Math.Min(1, (pool.Volume - 0.25) / 0.25));
        }

        private double PadX(int pad)
        {
            return scene.Pond.X + PadSpots[pad] * scene.Pond.Half * 0.8;
        }

        /// <summary>Things that float: lily pads, the frog and the duck, drawn right after the water.</summary>
        internal void DrawWater(Canvas canvas)
        {
            SummerScene.Pool pond = scene.Pond;
            double grow = Grow(pond);
            double waterline = scene.Ground - pond.Depth(S) * 0.45;
            if (grow > 0.05 && pondVisible)
            {
                for (int pad = 0; pad < PadSpots.Length; pad++)
                {
                    bool flower = pad == 2;
                    canvas.DrawArt(PadX(pad), waterline, S * grow, pad == 0,
                        g => SummerArt.LilyPad(g, 10, flower));
                }
                double x = PadX(frogPad), lift = 0;
                if (frogHop < 1)
                {
                    x += (PadX(frogTarget) - x) * frogHop;
                    lift = Math.Sin(frogHop * Math.PI) * 16 * S;
                }
                double crouch = frogHop < 1 ? 0 : Math.Max(0, Math.Sin(clock * 1.3)) * 0.3;
                canvas.DrawArt(x, waterline - 1 - lift, S * grow, frogTarget < frogPad, g => SummerArt.Frog(g, crouch));
            }
            SummerScene.Pool main = scene.Main;
            if (duckVisible && main.Volume > 0.3)
            {
                double half = main.Half;
                double drift = Math.Sin(clock * 0.21 + 1.3);
                double x = main.X + half * 0.62 * drift;
                double bob = Math.Sin(clock * 2.1) * 0.8 * S;
                bool mirror = Math.Cos(clock * 0.21 + 1.3) < 0;
                canvas.DrawArt(x, scene.Ground - main.Depth(S) * 0.5 + bob, S * Math.Min(1, main.Volume * 1.6), mirror,
                    g => SummerArt.Duck(g, clock));
            }
        }

        internal void Draw(Canvas canvas)
        {
            if (pump != Stage.Away)
            {
                bool facingLeft = pumpDirection < 0;
                bool working = pump == Stage.Busy;
                double fill = tankFill, slosh = pump == Stage.Busy ? 0 : Math.Sin(clock * 3) * 0.6;
                if (hose > 0) DrawHose(canvas);
                canvas.DrawArt(pumpX, scene.Ground, S, facingLeft, g => SummerArt.Pump(g, clock, working, fill, slosh));
            }
            if (bather != Stage.Away)
            {
                SummerArt.Pose pose = bather == Stage.Busy ? SummerArt.Pose.Sit :
                    bather == Stage.Leaving ? SummerArt.Pose.Run : SummerArt.Pose.Walk;
                double y = pose == SummerArt.Pose.Sit ? scene.Ground - scene.Main.Depth(S) * 0.5 : scene.Ground;
                double step = pose == SummerArt.Pose.Sit ? clock : batherStep;
                canvas.DrawArt(batherX, y, S, batherDirection < 0 && pose != SummerArt.Pose.Sit,
                    g => SummerArt.Bather(g, pose, step));
            }
            if (runnerActive)
            {
                double step = runnerStep;
                canvas.DrawArt(runnerX, scene.Ground, S, runnerDirection < 0, g => SummerArt.Runner(g, step));
            }
        }

        private void DrawHose(Canvas canvas)
        {
            SummerScene.Pool pool = scene.Main;
            float startX = (float)(pumpX + pumpDirection * 37 * S), startY = (float)(scene.Ground - 19.5 * S);
            float endX = (float)(pool.X - pumpDirection * pool.Half * 0.35);
            float endY = (float)(scene.Ground - pool.Depth(S) * 0.2);
            endX = startX + (endX - startX) * (float)hose;
            endY = startY + (endY - startY) * (float)hose;
            float sag = (float)(14 * S);
            bool flowing = pump == Stage.Busy && pool.Drain > 0;
            float offset = (float)(clock * 9);
            canvas.DrawArt(0, 0, 1, false, g =>
            {
                g.DrawBezier(SummerArt.Hose, startX, startY, startX + (endX - startX) * 0.3f, startY - sag,
                    endX - (endX - startX) * 0.2f, endY - sag, endX, endY);
                if (!flowing) return;
                // Light dashes run from the puddle end up to the truck while it pumps.
                SummerArt.HoseFlow.DashOffset = offset;
                g.DrawBezier(SummerArt.HoseFlow, endX, endY, endX - (endX - startX) * 0.2f, endY - sag,
                    startX + (endX - startX) * 0.3f, startY - sag, startX, startY);
            });
        }
    }
}
