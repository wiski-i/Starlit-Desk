using System;
using System.Drawing;

namespace SnowDesk
{
    /// <summary>
    /// Autumn characters. A gardener rakes fallen leaves off the ground into a heap and a child later jumps
    /// into it; a street sweeper pushes a wheelie bin along and slowly sweeps the leaves into it; the tree,
    /// acorns, squirrel and gales live in <see cref="AutumnGrove"/>.
    /// Commands: 1 raker, 2 child, 3 squirrel, 4 tree, 5 gale, 6 sweeper.
    /// </summary>
    internal sealed class AutumnCast : ISeasonCast
    {
        private enum Stage { Away, Arriving, Busy, Leaving }

        private readonly SeasonScene scene;
        private readonly AutumnGrove grove;
        private Stage raker = Stage.Away, kid = Stage.Away, sweeper = Stage.Away;
        private double clock, rakeX, rakeDirection = 1, rakeStep, rakeTarget, rakeTime, nextRaker = 30;
        private double heapX, heap, nextKid;
        private double rakeMark, rakeMarkLife, slumpWait;
        private int heapSeed = 7;
        private double kidX, kidDirection = 1, kidStep, kidJump, kidFrom, kidTarget, kidSit;
        private double sweepX, sweepDirection = 1, sweepStep, sweepTimer, binFill, nextSweeper = 70;
        private static readonly Pen Trace = new Pen(SnowField.Brush(86, 180, 143, 101), 0.8f);

        internal AutumnCast(SeasonScene scene)
        {
            this.scene = scene;
            grove = new AutumnGrove(scene, ScatterHeap);
        }

        private double S { get { return scene.Scale; } }

        public void Trigger(int command)
        {
            if (command == 1) CallRaker(true);
            else if (command == 2) CallKid();
            else if (command == 6) CallSweeper();
            else grove.Trigger(command);
        }

        public void Advance(double seconds, Settings settings)
        {
            clock += seconds * scene.Pace;
            grove.Advance(seconds, settings);
            if (settings.QuietScene)
            {
                raker = kid = sweeper = Stage.Away;
                rakeMarkLife = 0;
                return;
            }
            rakeMarkLife = Math.Max(0, rakeMarkLife - seconds);
            slumpWait -= seconds;
            if (settings.Has(AutumnEffect.Raker) && raker == Stage.Away && clock >= nextRaker) CallRaker(false);
            if (settings.Has(AutumnEffect.Kid) && kid == Stage.Away && heap > 0.45 && raker == Stage.Away && clock >= nextKid) CallKid();
            if (settings.Has(AutumnEffect.Sweeper) && sweeper == Stage.Away && clock >= nextSweeper) CallSweeper();
            AdvanceRaker(seconds);
            AdvanceKid(seconds);
            AdvanceSweeper(seconds);
        }

        /// <summary>A gale takes the raked heap with it.</summary>
        private void ScatterHeap(double direction)
        {
            if (heap <= 0.05) return;
            var random = scene.Random;
            int count = (int)(heap * 40);
            for (int i = 0; i < count; i++)
                scene.Launch(heapX + (random.NextDouble() - 0.5) * 40 * S, scene.Ground - 8 * S, Leaf(),
                    direction * (200 + random.NextDouble() * 160), -120 - random.NextDouble() * 160);
            heap = 0;
        }

        private PieceLedge.Piece Leaf()
        {
            var random = scene.Random;
            return new PieceLedge.Piece
            {
                Kind = (byte)random.Next(3), Tint = (byte)random.Next(5), Size = (float)((15 + random.NextDouble() * 8) * S),
                Angle = (float)(random.NextDouble() * 360)
            };
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
                double bite = rakeX + rakeDirection * (32 + Math.Sin(rakeStep * 2.4) * 19) * S;
                double reach = 73 * S;
                int gathered = scene.GroundPile.Lift(bite - reach, bite + reach, dt * 1.2, scene.Random, null, 0.83);
                if (gathered > 0)
                {
                    heap = Math.Min(1, heap + gathered * 0.035);
                    rakeMark = bite;
                    rakeMarkLife = 4.6;
                }
                if (heap > 0.67 && slumpWait <= 0)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        double x = heapX + (i == 0 ? -1 : 1) * (20 + scene.Random.NextDouble() * 15) * S;
                        scene.GroundPile.Rest(x, (byte)scene.Random.Next(3), (byte)scene.Random.Next(5), (float)(13 * S), 95 + i * 64);
                    }
                    heap -= 0.045;
                    slumpWait = 0.62 + scene.Random.NextDouble() * 0.48;
                }
                bool empty = scene.GroundPile.CountIn(bite - reach, bite + reach) < 3;
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
                scene.Launch(kidX + (random.NextDouble() - 0.5) * 40 * S, scene.Ground - 6 * S, Leaf(),
                    (random.NextDouble() - 0.5) * 300, -180 - random.NextDouble() * 240);
            heap = 0;
            double reach = 60 * S;
            scene.GroundPile.Lift(kidX - reach, kidX + reach, 0.8, random,
                (x, y, piece) => scene.Launch(x, y, piece, (x - kidX) * 3, -160 - random.NextDouble() * 200));
        }

        private void CallSweeper()
        {
            if (sweeper != Stage.Away) return;
            nextSweeper = clock + 80 + scene.Random.NextDouble() * 40;
            sweepDirection = scene.Random.Next(2) == 0 ? -1 : 1;
            sweepX = sweepDirection > 0 ? -50 * S : scene.Width + 50 * S;
            binFill = 0;
            sweepTimer = 2.4;
            sweeper = Stage.Arriving;
        }

        /// <summary>The sweeper crosses the screen in short walks, stopping to sweep the leaves around him into the bin.</summary>
        private void AdvanceSweeper(double dt)
        {
            if (sweeper == Stage.Away) return;
            sweepTimer -= dt;
            if (sweeper == Stage.Arriving)
            {
                sweepX += sweepDirection * 52 * S * dt;
                sweepStep += dt * 2.2;
                bool beyond = sweepDirection > 0 ? sweepX > scene.Width + 70 * S : sweepX < -70 * S;
                if (beyond)
                {
                    sweeper = Stage.Away;
                    return;
                }
                bool onScreen = sweepX > 30 * S && sweepX < scene.Width - 30 * S;
                if (sweepTimer > 0 || !onScreen) return;
                sweeper = Stage.Busy;
                sweepTimer = 3.6;
                return;
            }
            sweepStep += dt;
            double broom = sweepX - sweepDirection * 18 * S, reach = 70 * S;
            int swept = scene.GroundPile.Lift(broom - reach, broom + reach, dt * 0.7, scene.Random, null);
            binFill = Math.Min(1, binFill + swept * 0.006);
            if (sweepTimer > 0) return;
            sweeper = Stage.Arriving;
            sweepTimer = 2.2;
        }

        public void DrawBack(Canvas canvas)
        {
            grove.DrawBack(canvas);
        }

        public void DrawFront(Canvas canvas)
        {
            grove.DrawFront(canvas);
            if (rakeMarkLife > 0)
            {
                float span = (float)(18 * S * Math.Min(1, rakeMarkLife / 2.0));
                canvas.DrawArt(rakeMark, scene.Ground - 0.8, 1, rakeDirection < 0, g =>
                {
                    for (int i = 0; i < 3; i++) g.DrawLine(Trace, -span, -i * 1.8f, span * (0.54f + i * 0.16f), -i * 1.8f - 1);
                });
            }
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
            if (sweeper != Stage.Away)
            {
                bool sweeping = sweeper == Stage.Busy;
                double step = sweepStep, fill = binFill;
                canvas.DrawArt(sweepX, scene.Ground, S, sweepDirection < 0, g => AutumnArt.Sweeper(g, step, sweeping, fill));
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
