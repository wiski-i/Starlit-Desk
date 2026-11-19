using System;

namespace SnowDesk
{
    /// <summary>
    /// Autumn characters. A gardener rakes fallen leaves off the ground into a heap; a child later runs in and
    /// jumps into it, throwing the leaves back into the air; a squirrel hops by to nibble an acorn; pumpkins sit
    /// on the ground. Commands: 1 raker, 2 child, 3 squirrel.
    /// </summary>
    internal sealed class AutumnCast : ISeasonCast
    {
        private enum Stage { Away, Arriving, Busy, Leaving }

        private readonly SeasonScene scene;
        private readonly double[] pumpkins;
        private Stage raker = Stage.Away, kid = Stage.Away, squirrel = Stage.Away;
        private double clock, rakeX, rakeDirection = 1, rakeStep, rakeTarget, rakeTime, nextRaker = 30;
        private double heapX, heap, nextKid;
        private int heapSeed = 7;
        private double kidX, kidDirection = 1, kidStep, kidJump, kidFrom, kidTarget, kidSit;
        private double squirrelX, squirrelDirection = 1, squirrelStep, squirrelStop, squirrelSit, nextSquirrel = 12;
        private bool showPumpkins = true;

        internal AutumnCast(SeasonScene scene)
        {
            this.scene = scene;
            var random = scene.Random;
            int count = scene.Width > 1400 * scene.Scale ? 3 : 2;
            pumpkins = new double[count];
            for (int i = 0; i < count; i++)
                pumpkins[i] = scene.Width * (0.12 + 0.76 * (i + 0.3 + random.NextDouble() * 0.4) / count);
        }

        private double S { get { return scene.Scale; } }

        public void Trigger(int command)
        {
            if (command == 1) CallRaker(true);
            else if (command == 2) CallKid();
            else if (command == 3) CallSquirrel();
        }

        public void Advance(double seconds, Settings settings)
        {
            clock += seconds * scene.Pace;
            showPumpkins = settings.Has(AutumnEffect.Pumpkins);
            if (settings.Has(AutumnEffect.Raker) && raker == Stage.Away && clock >= nextRaker) CallRaker(false);
            if (settings.Has(AutumnEffect.Kid) && kid == Stage.Away && heap > 0.45 && raker == Stage.Away && clock >= nextKid) CallKid();
            if (settings.Has(AutumnEffect.Squirrel) && squirrel == Stage.Away && clock >= nextSquirrel) CallSquirrel();
            AdvanceRaker(seconds);
            AdvanceKid(seconds);
            AdvanceSquirrel(seconds);
        }

        private void CallRaker(bool forced)
        {
            if (raker != Stage.Away) return;
            nextRaker = clock + 50 + scene.Random.NextDouble() * 30;
            // Rake where the most leaves lie, unless the ground is still almost bare.
            double best = -1, bestX = scene.Width * 0.5, span = 120 * S;
            for (double x = span; x < scene.Width - span; x += span * 0.5)
            {
                int count = scene.GroundPile.CountIn(x - span * 0.5, x + span * 0.5);
                if (count > best) { best = count; bestX = x; }
            }
            if (best < 14 && !forced) return;
            rakeTarget = bestX;
            rakeDirection = rakeTarget < scene.Width * 0.5 ? 1 : -1;
            rakeX = rakeDirection > 0 ? -40 * S : scene.Width + 40 * S;
            raker = Stage.Arriving;
        }

        private void AdvanceRaker(double dt)
        {
            if (raker == Stage.Away) return;
            if (raker == Stage.Arriving)
            {
                rakeX += rakeDirection * 70 * S * dt;
                rakeStep += dt * 2.4;
                if ((rakeX - rakeTarget) * rakeDirection >= 0)
                {
                    raker = Stage.Busy;
                    rakeTime = 0;
                    if (heap < 0.05) heapX = rakeX + rakeDirection * 42 * S;
                    heapSeed = scene.Random.Next(1000);
                }
                return;
            }
            if (raker == Stage.Busy)
            {
                rakeTime += dt;
                rakeStep += dt;
                // Each stroke pulls leaves in reach off the ground and onto the heap in front of him.
                double reach = 110 * S;
                int gathered = scene.GroundPile.Lift(rakeX - reach, rakeX + reach, dt * 0.9, scene.Random, null);
                heap = Math.Min(1, Math.Max(heap, 0.2) + gathered * 0.03);
                bool empty = scene.GroundPile.CountIn(rakeX - reach, rakeX + reach) < 3;
                if (rakeTime > 14 || heap >= 1 || (empty && rakeTime > 4))
                {
                    raker = Stage.Leaving;
                    nextKid = clock + 6;
                }
                return;
            }
            rakeX += rakeDirection * 80 * S * dt;
            rakeStep += dt * 2.6;
            if (rakeX < -60 * S || rakeX > scene.Width + 60 * S) raker = Stage.Away;
        }

        private void CallKid()
        {
            if (kid != Stage.Away) return;
            nextKid = clock + 40;
            kidTarget = heap > 0.05 ? heapX : scene.Width * (0.25 + scene.Random.NextDouble() * 0.5);
            kidDirection = scene.Random.Next(2) == 0 ? -1 : 1;
            kidX = kidDirection > 0 ? -30 * S : scene.Width + 30 * S;
            kidJump = -1;
            kid = Stage.Arriving;
        }

        private void AdvanceKid(double dt)
        {
            if (kid == Stage.Away) return;
            kidStep += dt * 5.4;
            if (kid == Stage.Arriving)
            {
                kidX += kidDirection * 190 * S * dt;
                if (kidJump < 0 && (kidTarget - kidX) * kidDirection <= 46 * S)
                {
                    kidJump = 0;
                    kidFrom = kidX;
                }
                if (kidJump >= 0)
                {
                    kidJump = Math.Min(1, kidJump + dt * 1.5);
                    kidX = kidFrom + (kidTarget - kidFrom) * kidJump;
                    if (kidJump >= 1) Land();
                }
                return;
            }
            if (kid == Stage.Busy)
            {
                kidSit -= dt;
                if (kidSit <= 0) kid = Stage.Leaving;
                return;
            }
            kidX += kidDirection * 200 * S * dt;
            if (kidX < -50 * S || kidX > scene.Width + 50 * S) kid = Stage.Away;
        }

        /// <summary>The landing throws the heap and nearby fallen leaves back into the air.</summary>
        private void Land()
        {
            kid = Stage.Busy;
            kidSit = 1.6;
            var random = scene.Random;
            int burst = (int)(heap * 46);
            for (int i = 0; i < burst; i++)
            {
                var piece = new PieceLedge.Piece
                {
                    Kind = (byte)random.Next(3), Tint = (byte)random.Next(5), Size = (float)((10 + random.NextDouble() * 6) * S),
                    Angle = (float)(random.NextDouble() * 360)
                };
                scene.Launch(kidX + (random.NextDouble() - 0.5) * 40 * S, scene.Ground - 6 * S, piece,
                    (random.NextDouble() - 0.5) * 300, -180 - random.NextDouble() * 240);
            }
            heap = 0;
            double reach = 60 * S;
            scene.GroundPile.Lift(kidX - reach, kidX + reach, 0.8, random,
                (x, y, piece) => scene.Launch(x, y, piece, (x - kidX) * 3, -160 - random.NextDouble() * 200));
        }

        private void CallSquirrel()
        {
            if (squirrel != Stage.Away) return;
            nextSquirrel = clock + 30 + scene.Random.NextDouble() * 25;
            squirrelDirection = scene.Random.Next(2) == 0 ? -1 : 1;
            squirrelX = squirrelDirection > 0 ? -20 * S : scene.Width + 20 * S;
            squirrelStop = scene.Width * (0.2 + scene.Random.NextDouble() * 0.6);
            squirrel = Stage.Arriving;
        }

        private void AdvanceSquirrel(double dt)
        {
            if (squirrel == Stage.Away) return;
            if (squirrel == Stage.Busy)
            {
                squirrelSit -= dt;
                squirrelStep += dt;
                if (squirrelSit <= 0) squirrel = Stage.Leaving;
                return;
            }
            squirrelX += squirrelDirection * 150 * S * dt;
            squirrelStep += dt * 3.6;
            if (squirrel == Stage.Arriving && (squirrelX - squirrelStop) * squirrelDirection >= 0)
            {
                squirrel = Stage.Busy;
                squirrelSit = 3.5;
            }
            else if (squirrelX < -40 * S || squirrelX > scene.Width + 40 * S) squirrel = Stage.Away;
        }

        public void DrawBack(Canvas canvas)
        {
            if (!showPumpkins) return;
            for (int i = 0; i < pumpkins.Length; i++)
            {
                float size = i % 2 == 0 ? 1 : 0.78f;
                canvas.DrawArt(pumpkins[i], scene.Ground, S, i == 1, g => SeasonArt.Pumpkin(g, size));
            }
        }

        public void DrawFront(Canvas canvas)
        {
            if (heap > 0.01)
            {
                double amount = heap;
                int seed = heapSeed;
                float scale = (float)S;
                canvas.DrawArt(heapX, scene.Ground, 1, false, g => SeasonArt.Heap(g, amount, seed, scale));
            }
            if (raker != Stage.Away)
            {
                bool busy = raker == Stage.Busy;
                double step = rakeStep;
                canvas.DrawArt(rakeX, scene.Ground, S, rakeDirection < 0, g => AutumnArt.Raker(g, step, busy));
            }
            if (squirrel != Stage.Away)
            {
                bool sitting = squirrel == Stage.Busy;
                double step = squirrelStep;
                canvas.DrawArt(squirrelX, scene.Ground, S, squirrelDirection < 0, g => AutumnArt.Squirrel(g, step, sitting));
            }
            if (kid != Stage.Away)
            {
                int pose = kid == Stage.Busy ? 2 : kidJump >= 0 && kid == Stage.Arriving ? 1 : 0;
                double lift = kidJump >= 0 && kid == Stage.Arriving ? Math.Sin(kidJump * Math.PI) * 46 * S : 0;
                double step = kidStep;
                canvas.DrawArt(kidX, scene.Ground - lift, S * 0.8, kidDirection < 0, g => AutumnArt.Kid(g, step, pose));
            }
        }
    }
}
