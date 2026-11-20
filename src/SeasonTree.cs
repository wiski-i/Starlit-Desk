using System;

namespace SnowDesk
{
    /// <summary>
    /// A tree that drops onto the ground from above, settles with a small squash, sways with the wind and can
    /// be blown down: it tips over in the wind's direction and tumbles off the screen.
    /// </summary>
    internal sealed class SeasonTree
    {
        private readonly SeasonScene scene;
        private readonly Action landed;
        private double drop, fallSpeed, squash, topple, toppleSpeed, toppleDirection, slide, slideSpeed;
        private bool onGround;

        /// <param name="landed">Called once when the tree hits the ground.</param>
        internal SeasonTree(SeasonScene scene, Action landed)
        {
            this.scene = scene;
            this.landed = landed;
        }

        internal bool Present { get; private set; }
        /// <summary>Standing upright on the ground: the crown can shed and hold a hive.</summary>
        internal bool Standing { get { return Present && onGround && toppleDirection == 0; } }
        internal double X { get; private set; }
        /// <summary>How much of the crown is left, 0 (bare) .. 1 (full).</summary>
        internal double Foliage { get; set; }
        private double S { get { return scene.Scale; } }
        /// <summary>The tree is drawn larger than the figures around it.</summary>
        private double T { get { return scene.Scale * 1.6; } }

        internal void Drop(double x)
        {
            Present = true;
            onGround = false;
            X = x;
            drop = -(scene.Ground + 260 * T);
            fallSpeed = 0;
            Foliage = 1;
            topple = toppleSpeed = toppleDirection = slide = slideSpeed = 0;
        }

        /// <summary>Starts tipping the tree over towards <paramref name="direction"/> (-1 left, 1 right).</summary>
        internal void BlowDown(double direction)
        {
            if (Standing) toppleDirection = direction < 0 ? -1 : 1;
        }

        internal void Advance(double seconds)
        {
            if (!Present) return;
            if (!onGround)
            {
                fallSpeed += 1500 * S * seconds;
                drop += fallSpeed * seconds;
                if (drop < 0) return;
                drop = 0;
                onGround = true;
                squash = 1;
                landed();
                return;
            }
            squash = Math.Max(0, squash - seconds * 2.6);
            if (toppleDirection == 0) return;
            if (Math.Abs(topple) < 86)
            {
                toppleSpeed += 140 * seconds;
                topple = Math.Max(-86, Math.Min(86, topple + toppleSpeed * seconds * toppleDirection));
                return;
            }
            // Down on its side, the gale rolls it away until it is gone.
            slideSpeed = Math.Min(520 * S, slideSpeed + 360 * S * seconds);
            slide += slideSpeed * seconds * toppleDirection;
            if (X + slide < -260 * T || X + slide > scene.Width + 260 * T) Present = false;
        }

        /// <summary>A random point in the standing crown, in scene coordinates.</summary>
        internal void CanopyPoint(out double x, out double y)
        {
            Random random = scene.Random;
            double angle = random.NextDouble() * Math.PI * 2, reach = Math.Sqrt(random.NextDouble()) * 0.85;
            x = X + (TreeArt.Canopy.X + Math.Cos(angle) * reach * TreeArt.CanopyRadiusX) * T;
            y = scene.Ground + (TreeArt.Canopy.Y + Math.Sin(angle) * reach * TreeArt.CanopyRadiusY) * T;
        }

        internal double HiveX { get { return X + TreeArt.HiveBranch.X * T; } }
        internal double HiveY { get { return scene.Ground + TreeArt.HiveBranch.Y * T; } }

        internal void Draw(Canvas canvas)
        {
            if (!Present) return;
            double sway = Math.Max(-6, Math.Min(6, scene.Wind / 25.0)) + Math.Sin(scene.Clock * 0.8) * 0.6;
            float stretch = (float)(1 - 0.1 * Math.Sin(squash * Math.PI * 2) * squash);
            float angle = (float)topple;
            double foliage = Foliage;
            Season season = scene.Season;
            canvas.DrawArt(X + slide, scene.Ground + drop, T, false, g =>
            {
                g.RotateTransform(angle);
                g.ScaleTransform(1, stretch);
                TreeArt.Tree(g, season, foliage, sway);
            });
        }
    }
}
