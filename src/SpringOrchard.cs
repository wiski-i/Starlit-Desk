using System;
using System.Collections.Generic;

namespace SnowDesk
{
    /// <summary>
    /// The spring tree and its story: a cherry tree drops onto the ground with a wild hive on a branch. After
    /// a while the hive shakes loose and falls; a car drives over it, an angry swarm bursts out and chases the
    /// car off the screen, and a beekeeper clears the wreck and hangs a new hive.
    /// </summary>
    internal sealed class SpringOrchard
    {
        private enum HiveState { None, Hanging, Wobbling, Falling, Ground, Crushed }
        private enum Stage { Away, Arriving, Busy, Leaving }

        private sealed class Angry
        {
            internal double X, Y, VX, VY, Phase, Offset;
        }

        private sealed class Puff
        {
            internal double X, Y, Age;
        }

        private readonly SeasonScene scene;
        private readonly SeasonTree tree;
        private readonly List<Angry> swarm = new List<Angry>();
        private readonly List<Puff> puffs = new List<Puff>();
        private HiveState hive = HiveState.None;
        private Stage car = Stage.Away, keeper = Stage.Away;
        private double clock, nextTree = 3, nextFall, shed, hiveX, hiveY, hiveFall, hiveTimer, carWait = -1;
        private double carX, carDirection = 1, carSpeed, carWheel, carBump, puffWait;
        private double keeperX, keeperDirection = 1, keeperStep, keeperTimer, keeperWait = -1, regrow = -1;
        private bool fleeing, treeOn = true, carOn = true, keeperOn = true;

        internal SpringOrchard(SeasonScene scene)
        {
            this.scene = scene;
            tree = new SeasonTree(scene, Landed);
        }

        private double S { get { return scene.Scale; } }

        /// <summary>Entrance of the hanging hive, where the garden bees fly home to.</summary>
        internal bool HiveEntrance(out double x, out double y)
        {
            x = tree.HiveX;
            y = tree.HiveY + 21 * S;
            return treeOn && tree.Standing && (hive == HiveState.Hanging || hive == HiveState.Wobbling);
        }

        /// <summary>4 drops a new tree, 5 sends the car (shaking the hive loose first), 6 calls the beekeeper.</summary>
        internal void Trigger(int command)
        {
            if (command == 4) DropTree();
            else if (command == 5)
            {
                if (hive == HiveState.Hanging) ShakeLoose();
                else if (hive == HiveState.Ground) carWait = 0.01;
                else if (car == Stage.Away) StartCar();
            }
            else if (command == 6 && keeper == Stage.Away) keeperWait = 0.01;
        }

        private void DropTree()
        {
            tree.Drop(scene.Width * (0.2 + scene.Random.NextDouble() * 0.6));
            hive = HiveState.None;
        }

        private void Landed()
        {
            var random = scene.Random;
            for (int i = 0; i < 26; i++)
            {
                double x, y;
                tree.CanopyPoint(out x, out y);
                scene.Launch(x, y, Petal(), (random.NextDouble() - 0.5) * 160, -60 - random.NextDouble() * 120);
            }
            double reach = 90 * S;
            scene.GroundPile.Lift(tree.X - reach, tree.X + reach, 0.7, random,
                (x, y, piece) => scene.Launch(x, y, piece, (x - tree.X) * 2.5, -140 - random.NextDouble() * 120));
            hive = HiveState.Hanging;
            nextFall = clock + 45 + random.NextDouble() * 30;
        }

        private PieceLedge.Piece Petal()
        {
            var random = scene.Random;
            return new PieceLedge.Piece
            {
                Kind = (byte)random.Next(2), Tint = (byte)random.Next(3), Size = (float)((5.5 + random.NextDouble() * 3.5) * S),
                Angle = (float)(random.NextDouble() * 360)
            };
        }

        internal void Advance(double dt, Settings settings)
        {
            clock += dt * scene.Pace;
            treeOn = settings.Has(SpringEffect.Tree);
            carOn = settings.Has(SpringEffect.Car);
            keeperOn = settings.Has(SpringEffect.Beekeeper);
            if (!treeOn) return;
            if (!tree.Present && clock >= nextTree) DropTree();
            tree.Advance(dt);
            if (tree.Standing)
            {
                shed += dt * scene.Pace;
                while (shed > 0.5)
                {
                    shed -= 0.5;
                    double x, y;
                    tree.CanopyPoint(out x, out y);
                    scene.Launch(x, y, Petal(), scene.Wind * 0.5, 0);
                }
            }
            AdvanceHive(dt);
            AdvanceCar(dt);
            AdvanceSwarm(dt);
            AdvanceKeeper(dt);
        }

        private void ShakeLoose()
        {
            hive = HiveState.Wobbling;
            hiveTimer = 1.4;
            carWait = carOn ? 2.4 : -1;
        }

        private void AdvanceHive(double dt)
        {
            if (hive == HiveState.Hanging && carOn && clock >= nextFall && tree.Standing) ShakeLoose();
            else if (hive == HiveState.Wobbling)
            {
                hiveTimer -= dt;
                if (hiveTimer > 0) return;
                hive = HiveState.Falling;
                hiveX = tree.HiveX;
                hiveY = tree.HiveY;
                hiveFall = 0;
            }
            else if (hive == HiveState.Falling)
            {
                hiveFall += 900 * S * dt;
                hiveY += hiveFall * dt;
                double rest = scene.Ground - 27 * S;
                if (hiveY < rest) return;
                hiveY = rest;
                hive = HiveState.Ground;
                scene.Splash(hiveX, scene.Ground);
            }
            else if (hive == HiveState.Ground && carWait > 0)
            {
                carWait -= dt;
                if (carWait <= 0 && car == Stage.Away) StartCar();
            }
            else if (hive == HiveState.Crushed && !keeperOn && regrow > 0)
            {
                regrow -= dt;
                if (regrow <= 0) RehangHive();
            }
        }

        private void StartCar()
        {
            carDirection = hive != HiveState.None && hiveX < scene.Width * 0.5 ? -1 : 1;
            carX = carDirection > 0 ? -60 * S : scene.Width + 60 * S;
            carSpeed = 150 * S;
            fleeing = false;
            car = Stage.Arriving;
        }

        private void AdvanceCar(double dt)
        {
            if (car == Stage.Away) return;
            if (fleeing) carSpeed = Math.Min(400 * S, carSpeed + 280 * S * dt);
            carX += carDirection * carSpeed * dt;
            carWheel += carSpeed * dt / (6.5 * S);
            carBump = Math.Max(0, carBump - dt * 3);
            double front = carX + carDirection * 28 * S;
            if (hive == HiveState.Ground && (front - hiveX) * carDirection >= 0) Crush();
            if (fleeing)
            {
                puffWait -= dt;
                if (puffWait <= 0)
                {
                    puffWait = 0.08;
                    puffs.Add(new Puff { X = carX - carDirection * 36 * S, Y = scene.Ground - 9 * S });
                }
            }
            if (carX < -100 * S || carX > scene.Width + 100 * S) car = Stage.Away;
        }

        /// <summary>The wheels go over the fallen hive: it is flattened and the swarm comes out.</summary>
        private void Crush()
        {
            hive = HiveState.Crushed;
            carBump = 1;
            fleeing = true;
            var random = scene.Random;
            for (int i = 0; i < 16; i++)
                swarm.Add(new Angry
                {
                    X = hiveX, Y = scene.Ground - 6 * S, VX = (random.NextDouble() - 0.5) * 200 * S, VY = -80 - random.NextDouble() * 160 * S,
                    Phase = random.NextDouble() * 9, Offset = random.NextDouble() * Math.PI * 2
                });
            keeperWait = keeperOn ? 8 : -1;
            regrow = 25;
        }

        private void AdvanceSwarm(double dt)
        {
            for (int i = puffs.Count - 1; i >= 0; i--)
            {
                puffs[i].Age += dt;
                if (puffs[i].Age > 0.9) puffs.RemoveAt(i);
            }
            for (int i = swarm.Count - 1; i >= 0; i--)
            {
                Angry bee = swarm[i];
                bee.Phase += dt;
                double tx, ty;
                if (car != Stage.Away)
                {
                    tx = carX - carDirection * 6 * S + Math.Cos(bee.Phase * 4 + bee.Offset) * 30 * S;
                    ty = scene.Ground - 34 * S + Math.Sin(bee.Phase * 5 + bee.Offset) * 18 * S;
                }
                else
                {
                    tx = carDirection > 0 ? scene.Width + 300 * S : -300 * S;
                    ty = scene.Ground - 60 * S;
                }
                double dx = tx - bee.X, dy = ty - bee.Y, distance = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
                double speed = 420 * S;
                bee.VX += (dx / distance * speed - bee.VX) * Math.Min(1, dt * 3.2);
                bee.VY += (dy / distance * speed - bee.VY) * Math.Min(1, dt * 3.2);
                bee.X += bee.VX * dt;
                bee.Y += bee.VY * dt;
                if (car == Stage.Away && (bee.X < -140 * S || bee.X > scene.Width + 140 * S)) swarm.RemoveAt(i);
            }
        }

        private void AdvanceKeeper(double dt)
        {
            if (keeper == Stage.Away)
            {
                if (keeperWait <= 0 || !keeperOn || !tree.Standing) return;
                keeperWait -= dt;
                if (keeperWait > 0) return;
                keeperWait = -1;
                keeperDirection = tree.X < scene.Width * 0.5 ? 1 : -1;
                keeperX = keeperDirection > 0 ? -40 * S : scene.Width + 40 * S;
                keeper = Stage.Arriving;
                return;
            }
            keeperStep += dt * (keeper == Stage.Busy ? 1 : 2.4);
            double stand = tree.HiveX - keeperDirection * 6 * S;
            if (keeper == Stage.Arriving)
            {
                keeperX += keeperDirection * 68 * S * dt;
                if ((keeperX - stand) * keeperDirection < 0) return;
                keeper = Stage.Busy;
                keeperTimer = 3;
            }
            else if (keeper == Stage.Busy)
            {
                keeperTimer -= dt;
                if (keeperTimer < 2.2 && hive == HiveState.Crushed) hive = HiveState.None;
                if (keeperTimer < 1 && hive == HiveState.None) RehangHive();
                if (keeperTimer <= 0) keeper = Stage.Leaving;
            }
            else
            {
                keeperX += keeperDirection * 72 * S * dt;
                if (keeperX < -60 * S || keeperX > scene.Width + 60 * S) keeper = Stage.Away;
            }
        }

        private void RehangHive()
        {
            hive = HiveState.Hanging;
            regrow = -1;
            nextFall = clock + 60 + scene.Random.NextDouble() * 30;
        }

        internal void DrawBack(Canvas canvas)
        {
            if (!treeOn) return;
            tree.Draw(canvas);
            if (tree.Standing && (hive == HiveState.Hanging || hive == HiveState.Wobbling))
            {
                double wobble = hive == HiveState.Wobbling ? Math.Sin(hiveTimer * 30) * 14 : Math.Sin(scene.Clock * 1.1) * 2;
                canvas.DrawArt(tree.HiveX, tree.HiveY, S, false, g => SpringArt.Hive(g, false, wobble));
            }
        }

        internal void DrawFront(Canvas canvas)
        {
            if (!treeOn) return;
            if (hive == HiveState.Falling || hive == HiveState.Ground)
                canvas.DrawArt(hiveX, hiveY, S, false, g => SpringArt.Hive(g, false, 0));
            else if (hive == HiveState.Crushed)
                canvas.DrawArt(hiveX, scene.Ground, S, false, g => SpringArt.Hive(g, true, 0));
            foreach (Puff puff in puffs)
            {
                double r = (3 + puff.Age * 9) * S;
                int alpha = (int)(120 * (1 - puff.Age / 0.9));
                canvas.DrawArt(puff.X, puff.Y - puff.Age * 10 * S, 1, false, g => SeasonArt.Smoke(g, (float)r, alpha));
            }
            if (car != Stage.Away)
            {
                double wheels = carWheel, bump = Math.Sin(carBump * Math.PI) * 6 * S;
                bool scared = fleeing;
                canvas.DrawArt(carX, scene.Ground - bump, S * 1.15, carDirection < 0, g => SpringArt.Car(g, wheels, scared));
            }
            foreach (Angry bee in swarm)
            {
                double phase = bee.Phase;
                canvas.DrawArt(bee.X, bee.Y, S * 0.8, bee.VX < 0, g => SpringArt.Bee(g, phase, true));
            }
            if (keeper != Stage.Away)
            {
                int pose = keeper == Stage.Busy ? 1 : hive == HiveState.Hanging ? 2 : 0;
                double step = keeperStep;
                canvas.DrawArt(keeperX, scene.Ground, S, keeperDirection < 0, g => SpringArt.Beekeeper(g, step, pose));
            }
        }
    }
}
