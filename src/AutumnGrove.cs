using System;
using System.Collections.Generic;

namespace SnowDesk
{
    /// <summary>
    /// The autumn tree and its story: it drops onto the ground, sheds leaves and acorns; a squirrel runs in,
    /// snatches a fallen acorn and races off with it; now and then a gale strips the crown, flattens the leaf
    /// piles and blows the bare tree over and off the screen, and later a new tree drops in.
    /// </summary>
    internal sealed class AutumnGrove
    {
        private enum Stage { Away, Arriving, Busy, Leaving }

        private sealed class Nut
        {
            internal double X, Y, VX, VY;
            internal bool Resting;
        }

        private readonly SeasonScene scene;
        private readonly SeasonTree tree;
        private readonly Action<double> scatterHeap;
        private readonly List<Nut> acorns = new List<Nut>();
        private Stage squirrel = Stage.Away;
        private Nut prize;
        private double clock, nextTree = 3, shed, nextAcorn = 8, nextGale = 120, nextSquirrel = 6;
        private double squirrelX, squirrelDirection = 1, squirrelStep, squirrelTimer;
        private bool treeOn = true, squirrelOn = true, hasAcorn;

        /// <param name="scatterHeap">Blows a raked heap away in the given direction when a gale starts.</param>
        internal AutumnGrove(SeasonScene scene, Action<double> scatterHeap)
        {
            this.scene = scene;
            this.scatterHeap = scatterHeap;
            tree = new SeasonTree(scene, Landed);
        }

        private double S { get { return scene.Scale; } }

        /// <summary>3 drops an acorn and sends the squirrel, 4 drops a new tree, 5 starts a gale.</summary>
        internal void Trigger(int command)
        {
            if (command == 3)
            {
                if (tree.Standing && acorns.Count < 5) DropAcorn();
                nextSquirrel = 0;
            }
            else if (command == 4) DropTree();
            else if (command == 5) StartGale();
        }

        private void DropTree()
        {
            tree.Drop(scene.Width * (0.18 + scene.Random.NextDouble() * 0.64));
            acorns.Clear();
        }

        private void Landed()
        {
            for (int i = 0; i < 24; i++) ShedLeaf((scene.Random.NextDouble() - 0.5) * 180, -60 - scene.Random.NextDouble() * 140);
            scene.Splash(tree.X, scene.Ground);
            nextAcorn = clock + 5;
        }

        private void ShedLeaf(double vx, double vy)
        {
            var random = scene.Random;
            double x, y;
            tree.CanopyPoint(out x, out y);
            scene.Launch(x, y, new PieceLedge.Piece
            {
                Kind = (byte)random.Next(3), Tint = (byte)random.Next(5), Size = (float)((15 + random.NextDouble() * 8) * S),
                Angle = (float)(random.NextDouble() * 360)
            }, vx, vy);
        }

        private void DropAcorn()
        {
            double x, y;
            tree.CanopyPoint(out x, out y);
            acorns.Add(new Nut { X = x, Y = y, VX = (scene.Random.NextDouble() - 0.5) * 40 });
        }

        private void StartGale()
        {
            double direction = scene.Random.Next(2) == 0 ? -1 : 1;
            scene.Gale(direction);
            scatterHeap(direction);
            nextGale = clock + 110 + scene.Random.NextDouble() * 60;
        }

        internal void Advance(double dt, Settings settings)
        {
            clock += dt * scene.Pace;
            treeOn = settings.Has(AutumnEffect.Tree);
            squirrelOn = settings.Has(AutumnEffect.Squirrel);
            if (settings.Has(AutumnEffect.Gale) && clock >= nextGale) StartGale();
            if (treeOn)
            {
                if (!tree.Present && clock >= nextTree) DropTree();
                tree.Advance(dt);
                if (tree.Standing) AdvanceCrown(dt);
            }
            AdvanceAcorns(dt);
            AdvanceSquirrel(dt);
        }

        private void AdvanceCrown(double dt)
        {
            double gale = scene.GaleStrength;
            if (gale > 0.15)
            {
                // The gale tears the crown off leaf by leaf, then takes the bare tree with it.
                int torn = (int)Math.Ceiling(gale * 40 * dt);
                for (int i = 0; i < torn && tree.Foliage > 0; i++)
                {
                    ShedLeaf(scene.GaleDirection * 260, -80);
                    tree.Foliage = Math.Max(0, tree.Foliage - 0.012);
                }
                if (tree.Foliage <= 0.02 && gale > 0.6)
                {
                    tree.BlowDown(scene.GaleDirection);
                    nextTree = clock + 25 + scene.Random.NextDouble() * 15;
                }
                return;
            }
            shed += dt * scene.Pace;
            while (shed > 0.8 && tree.Foliage > 0.03)
            {
                shed -= 0.8;
                ShedLeaf(scene.Wind * 0.4, 0);
                tree.Foliage -= 0.003;
            }
            if (tree.Foliage > 0.25 && clock >= nextAcorn && acorns.Count < 4)
            {
                DropAcorn();
                nextAcorn = clock + 10 + scene.Random.NextDouble() * 8;
            }
        }

        private void AdvanceAcorns(double dt)
        {
            double gale = scene.GaleStrength;
            for (int i = acorns.Count - 1; i >= 0; i--)
            {
                Nut nut = acorns[i];
                if (gale > 0.4) nut.Resting = false;
                if (nut.Resting) continue;
                nut.VY += 900 * S * dt;
                nut.VX += (gale * 320 * scene.GaleDirection - nut.VX) * Math.Min(1, dt * (gale > 0 ? 2 : 0.4));
                nut.X += nut.VX * dt;
                nut.Y += nut.VY * dt;
                if (nut.Y >= scene.Ground)
                {
                    nut.Y = scene.Ground;
                    if (nut.VY > 120 * S) nut.VY = -nut.VY * 0.35;
                    else if (gale < 0.2)
                    {
                        nut.VY = 0;
                        nut.Resting = true;
                    }
                    else nut.VY = 0;
                }
                if (nut.X < -30 || nut.X > scene.Width + 30)
                {
                    if (nut == prize) prize = null;
                    acorns.RemoveAt(i);
                }
            }
        }

        private void AdvanceSquirrel(double dt)
        {
            if (squirrel == Stage.Away)
            {
                if (!squirrelOn || clock < nextSquirrel) return;
                prize = acorns.Find(nut => nut.Resting);
                if (prize == null) return;
                squirrelDirection = prize.X < scene.Width * 0.5 ? 1 : -1;
                squirrelX = squirrelDirection > 0 ? -20 * S : scene.Width + 20 * S;
                squirrel = Stage.Arriving;
                return;
            }
            squirrelStep += dt * (squirrel == Stage.Leaving ? 4.6 : 3.6);
            if (squirrel == Stage.Arriving)
            {
                if (prize == null || !acorns.Contains(prize))
                {
                    squirrel = Stage.Leaving;
                    return;
                }
                squirrelX += squirrelDirection * 170 * S * dt;
                if ((squirrelX - (prize.X - squirrelDirection * 10 * S)) * squirrelDirection < 0) return;
                acorns.Remove(prize);
                prize = null;
                hasAcorn = true;
                squirrel = Stage.Busy;
                squirrelTimer = 0.5;
            }
            else if (squirrel == Stage.Busy)
            {
                squirrelTimer -= dt;
                if (squirrelTimer > 0) return;
                // Got it: turn around and bolt back with the acorn.
                squirrel = Stage.Leaving;
                squirrelDirection = -squirrelDirection;
            }
            else
            {
                squirrelX += squirrelDirection * 260 * S * dt;
                if (squirrelX < -40 * S || squirrelX > scene.Width + 40 * S)
                {
                    squirrel = Stage.Away;
                    hasAcorn = false;
                    nextSquirrel = clock + 8;
                }
            }
        }

        internal void DrawBack(Canvas canvas)
        {
            if (treeOn) tree.Draw(canvas);
        }

        internal void DrawFront(Canvas canvas)
        {
            foreach (Nut nut in acorns) canvas.DrawArt(nut.X, nut.Y, S, false, AutumnArt.DrawAcorn);
            if (squirrel == Stage.Away) return;
            bool sitting = squirrel == Stage.Busy, carrying = squirrel == Stage.Leaving && hasAcorn;
            double step = squirrelStep;
            canvas.DrawArt(squirrelX, scene.Ground, S, squirrelDirection < 0, g => AutumnArt.Squirrel(g, step, sitting, carrying));
        }
    }
}
