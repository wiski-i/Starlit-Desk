using System;
using System.Collections.Generic;

namespace SnowDesk
{
    /// <summary>
    /// Spring characters. A garden along the ground grows from sprouts into tulips, daisies and crocuses and
    /// sways in the wind; butterflies settle on open flowers and flee the cursor; bees hop between blooms; a
    /// gardener waters the least grown flower; swallows cross the sky; a cherry tree with a hive plays out
    /// <see cref="SpringOrchard"/>. Commands: 1 butterflies, 2 gardener, 3 swallows, 4 tree, 5 car, 6 beekeeper.
    /// </summary>
    internal sealed class SpringCast : ISeasonCast
    {
        private enum Stage { Away, Arriving, Busy, Leaving }

        private sealed class Bloom
        {
            internal double X, Grow, Rate;
            internal int Kind, Tint, Seed;
        }

        private sealed class Flyer
        {
            internal double X, Y, VX, VY, TargetX, TargetY, Rest, Phase;
            internal int Tint, Flower = -1;
        }

        private readonly SeasonScene scene;
        private readonly SpringOrchard orchard;
        private readonly List<Bloom> garden = new List<Bloom>();
        private readonly List<Flyer> butterflies = new List<Flyer>();
        private readonly List<Flyer> bees = new List<Flyer>();
        private Stage gardener = Stage.Away;
        private double clock, gardenerX, gardenerDirection = 1, gardenerStep, waterTime, nextGardener = 25;
        private int watered = -1;
        private double birdsX, birdsY, birdsDirection = 1, nextBirds = 18;
        private bool birds, showGarden = true;

        internal SpringCast(SeasonScene scene)
        {
            this.scene = scene;
            orchard = new SpringOrchard(scene);
            var random = scene.Random;
            // A few small beds with bare ground between them, rather than a wall of flowers.
            int beds = Math.Max(2, (int)(scene.Width / (560 * scene.Scale)));
            for (int bed = 0; bed < beds; bed++)
            {
                double center = scene.Width * (bed + 0.25 + random.NextDouble() * 0.5) / beds;
                int count = 2 + random.Next(3);
                for (int i = 0; i < count; i++)
                    garden.Add(new Bloom
                    {
                        X = center + (i - (count - 1) * 0.5) * (24 + random.NextDouble() * 10) * scene.Scale,
                        Grow = 0.15 + random.NextDouble() * 0.75, Rate = 0.006 + random.NextDouble() * 0.01,
                        Kind = random.Next(3), Tint = random.Next(3), Seed = random.Next(10000)
                    });
            }
        }

        private double S { get { return scene.Scale; } }

        public void Trigger(int command)
        {
            if (command == 1) for (int i = 0; i < 3; i++) AddButterfly(true);
            else if (command == 2) CallGardener();
            else if (command == 3) SendBirds();
            else orchard.Trigger(command);
        }

        public void Advance(double seconds, Settings settings)
        {
            clock += seconds * scene.Pace;
            showGarden = settings.Has(SpringEffect.Garden);
            orchard.Advance(seconds, settings);
            foreach (Bloom bloom in garden) bloom.Grow = Math.Min(1, bloom.Grow + bloom.Rate * seconds * scene.Pace);
            int wantButterflies = settings.Has(SpringEffect.Butterflies) ? 3 : 0;
            while (butterflies.Count < wantButterflies) AddButterfly(false);
            while (butterflies.Count > wantButterflies + 3) butterflies.RemoveAt(butterflies.Count - 1);
            int wantBees = settings.Has(SpringEffect.Bees) ? 2 : 0;
            while (bees.Count < wantBees) bees.Add(NewFlyer(0));
            while (bees.Count > wantBees) bees.RemoveAt(bees.Count - 1);
            foreach (Flyer butterfly in butterflies) MoveButterfly(butterfly, seconds);
            foreach (Flyer bee in bees) MoveBee(bee, seconds);
            if (settings.Has(SpringEffect.Gardener) && gardener == Stage.Away && clock >= nextGardener) CallGardener();
            AdvanceGardener(seconds);
            if (settings.Has(SpringEffect.Birds) && !birds && clock >= nextBirds) SendBirds();
            if (birds)
            {
                birdsX += birdsDirection * 260 * S * seconds;
                if (birdsX < -120 * S || birdsX > scene.Width + 120 * S) birds = false;
            }
        }

        private Flyer NewFlyer(int tint)
        {
            var random = scene.Random;
            double x = random.NextDouble() * scene.Width, y = scene.Ground - (60 + random.NextDouble() * 200) * S;
            return new Flyer { X = x, Y = y, TargetX = x, TargetY = y, Tint = tint, Phase = random.NextDouble() * 9 };
        }

        private void AddButterfly(bool fromEdge)
        {
            Flyer butterfly = NewFlyer(scene.Random.Next(3));
            if (fromEdge) butterfly.X = scene.Random.Next(2) == 0 ? -20 : scene.Width + 20;
            butterflies.Add(butterfly);
        }

        /// <summary>Picks an open flower head, or a point in the air above the garden when none is open.</summary>
        private void ChooseTarget(Flyer flyer, bool flowersOnly)
        {
            var random = scene.Random;
            flyer.Flower = -1;
            double hiveX, hiveY;
            if (flowersOnly && random.NextDouble() < 0.35 && orchard.HiveEntrance(out hiveX, out hiveY))
            {
                // Garden bees fly home to the hive between flowers.
                flyer.Flower = -2;
                flyer.TargetX = hiveX;
                flyer.TargetY = hiveY;
                return;
            }
            if (showGarden && garden.Count > 0 && (flowersOnly || random.NextDouble() < 0.6))
            {
                int pick = random.Next(garden.Count);
                if (garden[pick].Grow > 0.6)
                {
                    flyer.Flower = pick;
                    flyer.TargetX = garden[pick].X;
                    flyer.TargetY = FlowerTop(garden[pick]);
                    return;
                }
            }
            flyer.TargetX = random.NextDouble() * scene.Width;
            flyer.TargetY = scene.Ground - (50 + random.NextDouble() * 220) * S;
        }

        private double FlowerTop(Bloom bloom)
        {
            return scene.Ground - (8 + 30 * bloom.Grow) * (bloom.Kind == 2 ? 0.55 : 1) * S - 8 * S;
        }

        private void MoveButterfly(Flyer flyer, double dt)
        {
            flyer.Phase += dt;
            double fleeX = flyer.X - scene.PointerX, fleeY = flyer.Y - scene.PointerY;
            double near = Math.Sqrt(fleeX * fleeX + fleeY * fleeY);
            if (near < 70 * S)
            {
                // The cursor startles it: off it goes, away from the pointer.
                flyer.Rest = 0;
                flyer.Flower = -1;
                flyer.TargetX = Clamp(flyer.X + fleeX / Math.Max(1, near) * 240 * S, 0, scene.Width);
                flyer.TargetY = Clamp(flyer.Y + fleeY / Math.Max(1, near) * 160 * S - 80 * S, scene.Ground - 320 * S, scene.Ground - 30 * S);
            }
            if (flyer.Rest > 0)
            {
                flyer.Rest -= dt;
                if (flyer.Flower >= 0) flyer.Y = FlowerTop(garden[flyer.Flower]);
                if (flyer.Rest <= 0) ChooseTarget(flyer, false);
                return;
            }
            double dx = flyer.TargetX - flyer.X, dy = flyer.TargetY - flyer.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance < 4 * S)
            {
                if (flyer.Flower >= 0) flyer.Rest = 2 + scene.Random.NextDouble() * 3;
                else ChooseTarget(flyer, false);
                return;
            }
            double speed = 70 * S;
            flyer.VX += (dx / distance * speed - flyer.VX) * Math.Min(1, dt * 2);
            flyer.VY += (dy / distance * speed - flyer.VY) * Math.Min(1, dt * 2);
            flyer.X += (flyer.VX + Math.Sin(flyer.Phase * 3.1) * 30 * S + scene.Wind * 0.3) * dt;
            flyer.Y += (flyer.VY + Math.Sin(flyer.Phase * 5.3) * 26 * S) * dt;
        }

        private void MoveBee(Flyer bee, double dt)
        {
            bee.Phase += dt;
            if (bee.Rest > 0)
            {
                bee.Rest -= dt;
                if (bee.Rest <= 0) ChooseTarget(bee, true);
                return;
            }
            double dx = bee.TargetX - bee.X, dy = bee.TargetY - bee.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance < 3 * S)
            {
                bee.Rest = bee.Flower != -1 ? 1.2 + scene.Random.NextDouble() * 1.6 : 0.01;
                if (bee.Flower == -1) ChooseTarget(bee, true);
                return;
            }
            double speed = 120 * S;
            bee.X += dx / distance * Math.Min(distance, speed * dt) + Math.Sin(bee.Phase * 9) * 0.6 * S;
            bee.Y += dy / distance * Math.Min(distance, speed * dt) + Math.Cos(bee.Phase * 11) * 0.6 * S;
        }

        private void CallGardener()
        {
            if (gardener != Stage.Away || garden.Count == 0) return;
            nextGardener = clock + 55 + scene.Random.NextDouble() * 35;
            watered = 0;
            for (int i = 1; i < garden.Count; i++) if (garden[i].Grow < garden[watered].Grow) watered = i;
            gardenerDirection = garden[watered].X < scene.Width * 0.5 ? 1 : -1;
            gardenerX = gardenerDirection > 0 ? -40 * S : scene.Width + 40 * S;
            gardener = Stage.Arriving;
        }

        private void AdvanceGardener(double dt)
        {
            if (gardener == Stage.Away) return;
            gardenerStep += dt * (gardener == Stage.Busy ? 1 : 2.4);
            double stand = garden[watered].X - gardenerDirection * 26 * S;
            if (gardener == Stage.Arriving)
            {
                gardenerX += gardenerDirection * 66 * S * dt;
                if ((gardenerX - stand) * gardenerDirection >= 0)
                {
                    gardener = Stage.Busy;
                    waterTime = 4;
                }
            }
            else if (gardener == Stage.Busy)
            {
                waterTime -= dt;
                garden[watered].Grow = Math.Min(1, garden[watered].Grow + dt * 0.18);
                if (waterTime <= 0) gardener = Stage.Leaving;
            }
            else
            {
                gardenerX += gardenerDirection * 72 * S * dt;
                if (gardenerX < -60 * S || gardenerX > scene.Width + 60 * S) gardener = Stage.Away;
            }
        }

        private void SendBirds()
        {
            birds = true;
            nextBirds = clock + 40 + scene.Random.NextDouble() * 30;
            birdsDirection = scene.Random.Next(2) == 0 ? -1 : 1;
            birdsX = birdsDirection > 0 ? -100 * S : scene.Width + 100 * S;
            birdsY = scene.Height * (0.12 + scene.Random.NextDouble() * 0.2);
        }

        public void DrawBack(Canvas canvas)
        {
            orchard.DrawBack(canvas);
            if (!showGarden) return;
            double sway = Math.Max(-1.6, Math.Min(1.6, scene.Wind / 30.0));
            foreach (Bloom bloom in garden)
            {
                Bloom b = bloom;
                double bend = sway + Math.Sin(clock * 1.3 + b.Seed) * 0.4;
                canvas.DrawArt(b.X + 9 * S, scene.Ground, S, false, g => SeasonArt.Grass(g, b.Seed, bend * 3));
                canvas.DrawArt(b.X, scene.Ground, S, false, g => SeasonArt.Flower(g, b.Kind, b.Tint, b.Grow, bend));
            }
        }

        public void DrawFront(Canvas canvas)
        {
            orchard.DrawFront(canvas);
            if (gardener != Stage.Away)
            {
                bool busy = gardener == Stage.Busy;
                double step = gardenerStep;
                canvas.DrawArt(gardenerX, scene.Ground, S, gardenerDirection < 0, g => SpringArt.Gardener(g, step, busy));
            }
            foreach (Flyer bee in bees)
            {
                double phase = bee.Phase;
                canvas.DrawArt(bee.X, bee.Y, S * 0.8, bee.TargetX < bee.X, g => SpringArt.Bee(g, phase));
            }
            foreach (Flyer butterfly in butterflies)
            {
                double open = butterfly.Rest > 0 ? 0.6 + 0.4 * Math.Sin(butterfly.Phase * 1.5) : Math.Abs(Math.Cos(butterfly.Phase * 9));
                int tint = butterfly.Tint;
                canvas.DrawArt(butterfly.X, butterfly.Y, S * 0.85, false, g => SpringArt.Butterfly(g, tint, open));
            }
            if (birds)
            {
                for (int i = 0; i < 2; i++)
                {
                    double flap = Math.Sin(clock * 9 + i * 1.7);
                    double x = birdsX - birdsDirection * i * 46 * S, y = birdsY + i * 18 * S + Math.Sin(clock * 1.4 + i) * 10 * S;
                    canvas.DrawArt(x, y, S, birdsDirection < 0, g => SpringArt.Swallow(g, flap));
                }
            }
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
