using System;
using System.Collections.Generic;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using Pen = System.Drawing.Pen;
using Brush = System.Drawing.Brush;

namespace SnowDesk
{
    internal sealed class Decorations
    {
        private static readonly Pen Cable = new Pen(SnowField.Brush(154, 112, 135, 154), 1);
        private static readonly Brush ContactShadow = SnowField.Brush(55, 33, 58, 84);
        private readonly double width, height, scale;
        private readonly bool flourish;
        private readonly Action<SnowLedge, double, double> spillPlow;
        private readonly Action<double, double, double, double> sprayPlow;
        private readonly Action<SnowLedge, double, double> kickPowder;
        private readonly Random random = new Random(Guid.NewGuid().GetHashCode());
        private readonly List<Actor> actors = new List<Actor>(5);
        private double nextTree = 11, nextSleigh = 24, nextSanta = 19, nextPlow = 13, nextGondola = 15;
        private Actor held;
        private double gripX, gripY;

        private sealed class Actor
        {
            internal Effect Kind;
            internal double Age, X, Y, Offset, VX, VY, LastLeft, LastWidth, SettledTime;
            internal double GatheredSnow, PlowEnd, SidePowder, Overflow, SprayWait, UnloadLeft, UnloadRate, PourWait;
            internal double PlowResistance;
            internal double RootSupport, Snowline;
            internal SnowLedge Ledge;
            internal bool Manual, Unloading, LeavingScreen;
        }

        internal Decorations(int width, int height, Action<SnowLedge, double, double> spillPlow,
            Action<SnowLedge, double, double> kickPowder, Action<double, double, double, double> sprayPlow)
        {
            this.width = width;
            this.height = height;
            this.spillPlow = spillPlow;
            this.kickPowder = kickPowder;
            this.sprayPlow = sprayPlow;
            scale = Math.Max(1, Math.Min(1.65, height / 1080.0));
            flourish = Desktop.DecorativeMotionEnabled();
        }

        internal bool IsHoldingTree { get { return held != null; } }

        internal bool TryTreeBounds(out Rect bounds, out double visibleBottom)
        {
            bounds = Rect.Empty;
            visibleBottom = 0;
            if (held != null) return false;
            Actor tree = Find(Effect.Trees);
            if (tree == null || tree.X <= 0 || tree.X >= width || tree.Y <= 0 || tree.Y > height)
                return false;
            double w = DecorationArt.TreeWidth * scale, h = DecorationArt.TreeHeight * scale;
            bounds = new Rect(tree.X - w * 0.5, tree.Y - h, w, h);
            visibleBottom = tree.Ledge == null ? tree.Y : Math.Min(tree.Y, tree.Snowline);
            return true;
        }

        internal bool GrabTree(double x, double y)
        {
            Rect bounds;
            double visibleBottom;
            if (!TryTreeBounds(out bounds, out visibleBottom) || y > visibleBottom || !bounds.Contains(x, y)) return false;
            held = Find(Effect.Trees);
            gripX = held.X - x;
            gripY = held.Y - y;
            held.Ledge = null;
            held.VX = held.VY = 0;
            return true;
        }

        internal void DragTree(double x, double y, double dt, bool down)
        {
            if (held == null) return;
            if (!down)
            {
                held.Age = 0;
                held.VX *= 0.62;
                held.VY *= 0.45;
                held = null;
                return;
            }
            if (dt <= 0) return;
            double targetX = Clamp(x + gripX, DecorationArt.TreeWidth * scale * 0.5, width - DecorationArt.TreeWidth * scale * 0.5);
            double targetY = Clamp(y + gripY, DecorationArt.TreeHeight * scale * 0.35, height - 2);
            double eased = 1 - Math.Exp(-35 * dt);
            double shiftX = (targetX - held.X) * eased;
            double shiftY = (targetY - held.Y) * eased;
            held.X += shiftX;
            held.Y += shiftY;
            held.VX = Clamp(shiftX / dt, -440, 440);
            held.VY = Clamp(shiftY / dt, -310, 390);
        }

        internal void ClearTrees()
        {
            Actor tree = Find(Effect.Trees);
            if (tree != null) actors.Remove(tree);
            held = null;
        }

        internal void Trigger(Effect kind, List<SnowLedge> ledges)
        {
            if (kind == Effect.Trees) ClearTrees();
            Spawn(kind, ledges, true);
        }

        internal void Advance(double dt, double clock, Settings settings, List<SnowLedge> ledges)
        {
            TickSpawn(Effect.Trees, ref nextTree, 18, clock, settings, ledges);
            TickSpawn(Effect.Santa, ref nextSanta, 32, clock, settings, ledges);
            TickSpawn(Effect.Plow, ref nextPlow, 9, clock, settings, ledges);
            TickSpawn(Effect.Sleigh, ref nextSleigh, 48, clock, settings, ledges);
            TickSpawn(Effect.Gondola, ref nextGondola, Math.Max(32, width / 65 + 4), clock, settings, ledges);
            for (int i = actors.Count - 1; i >= 0; i--)
            {
                Actor actor = actors[i];
                if (actor == held) continue;
                if ((!actor.Manual && !settings.Has(actor.Kind)) || !Move(actor, dt, ledges))
                {
                    Spill(actor);
                    actors.RemoveAt(i);
                }
            }
        }

        private void TickSpawn(Effect kind, ref double next, double cycle, double clock, Settings settings, List<SnowLedge> ledges)
        {
            if (!settings.Has(kind) || clock < next) return;
            Spawn(kind, ledges, false);
            next = clock + cycle + (kind == Effect.Trees ? random.Next(10) : 0);
        }

        private Actor Find(Effect kind)
        {
            foreach (Actor actor in actors)
                if (actor.Kind == kind) return actor;
            return null;
        }

        private void Spawn(Effect kind, List<SnowLedge> ledges, bool manual)
        {
            Actor previous = Find(kind);
            if (previous != null)
            {
                if (!manual) return;
                Spill(previous);
                actors.Remove(previous);
            }
            var actor = new Actor { Kind = kind, Manual = manual };
            if (kind == Effect.Sleigh || kind == Effect.Gondola)
            {
                actor.X = -(kind == Effect.Sleigh ? DecorationArt.SleighWidth : DecorationArt.GondolaWidth) * scale;
                actor.Y = height * 0.21;
                actors.Add(actor);
                return;
            }

            int count = kind == Effect.Plow || manual ? ledges.Count : ledges.Count - 1;
            if (count == 0) return;
            int start = kind == Effect.Plow ? 0 : random.Next(count);
            for (int i = 0; i < count; i++)
            {
                SnowLedge ledge = ledges[(start + i) % count];
                if (kind == Effect.Plow)
                {
                    if (!StartPlow(actor, ledge)) continue;
                    actors.Add(actor);
                    return;
                }
                double margin = 25 * scale;
                if (ledge.Width < 113 * scale) continue;
                actor.Offset = kind == Effect.Trees
                    ? margin + random.NextDouble() * (ledge.Width - 2 * margin)
                    : margin;
                actor.X = ledge.X + actor.Offset;
                if (!ledge.IsVisibleAt(actor.X)) continue;
                if (kind == Effect.Trees)
                {
                    actor.Y = -5;
                    actor.VY = 40;
                }
                else
                {
                    actor.Ledge = ledge;
                    actor.Y = ledge.SurfaceAt(actor.X);
                }
                actors.Add(actor);
                return;
            }
        }

        private bool StartPlow(Actor actor, SnowLedge ledge)
        {
            int first = 0;
            double deepest = 0;
            for (int cell = 0; cell < ledge.Visible.Length; cell++)
            {
                if (!ledge.Visible[cell])
                {
                    first = cell + 1;
                    deepest = 0;
                    continue;
                }
                deepest = Math.Max(deepest, ledge.Snow[cell]);
                double blade = ledge.CellLeft(first);
                if (ledge.CellRight(cell) - blade < 113 * scale || (!actor.Manual && deepest < 30)) continue;
                actor.Ledge = ledge;
                actor.Offset = blade - ledge.X;
                int end = cell + 1;
                while (end < ledge.Visible.Length && ledge.Visible[end]) end++;
                actor.PlowEnd = ledge.CellRight(end - 1) - ledge.X;
                actor.X = blade - DecorationArt.PlowBlade * scale;
                actor.Y = ledge.SupportAt(blade);
                return true;
            }
            return false;
        }

        private bool Move(Actor actor, double dt, List<SnowLedge> ledges)
        {
            actor.Age += dt;
            switch (actor.Kind)
            {
                case Effect.Sleigh:
                    actor.X += 215 * dt;
                    return actor.X < width + DecorationArt.SleighWidth * scale;
                case Effect.Gondola:
                    actor.X += 65 * dt;
                    return actor.X < width + DecorationArt.GondolaWidth * scale;
                case Effect.Trees:
                    return MoveTree(actor, dt, ledges);
            }

            SnowLedge ledge = actor.Ledge;
            if (!ledges.Contains(ledge)) return false;
            if (actor.Kind == Effect.Plow) return MovePlow(actor, ledge, dt);
            double half = DecorationArt.SantaWidth * scale * 0.5;
            double oldOffset = actor.Offset;
            actor.Offset += 63 * scale * dt;
            actor.X = ledge.X + actor.Offset;
            if (actor.Offset + half > ledge.Width || !ledge.IsVisibleAt(actor.X)) return false;
            double stepLength = DecorationArt.SantaStepLength * scale;
            int plantedStep = (int)(actor.Offset / stepLength);
            if (plantedStep != (int)(oldOffset / stepLength))
            {
                double bootX = ledge.X + plantedStep * stepLength + DecorationArt.SantaPlantOffset * scale;
                double powder;
                if (ledge.Press(bootX, 4.2 * scale, 1.05 * scale, false, out powder) && powder >= 0.08)
                    kickPowder(ledge, bootX, powder);
            }
            actor.Y = ledge.SurfaceAt(actor.X) + 0.6;
            return true;
        }

        private bool MovePlow(Actor actor, SnowLedge ledge, double dt)
        {
            if (actor.Unloading) return PourPlow(actor, ledge, dt);
            double from = ledge.X + actor.Offset;
            if (!ledge.IsVisibleAt(from)) return false;
            double end = ledge.X + Math.Min(actor.PlowEnd, ledge.Width);
            if (end - from < 40 * scale && ledge.IsVisibleAt(end))
            {
                int cell = ledge.IndexAt(end);
                while (cell < ledge.Visible.Length && ledge.Visible[cell]) cell++;
                end = ledge.CellRight(cell - 1);
                actor.PlowEnd = end - ledge.X;
            }
            double capacity = 320 * scale;
            int ahead = Math.Min(ledge.Snow.Length - 1, ledge.IndexAt(Math.Min(end, from + SnowLedge.Cell)));
            double depth = ledge.Visible[ahead] ? ledge.Snow[ahead] : 0;
            double load = Math.Min(1, actor.GatheredSnow / capacity);
            double resistance = Math.Min(1, depth / 39) * 0.85 + load * 0.15;
            actor.PlowResistance += (resistance - actor.PlowResistance) * (1 - Math.Exp(-8 * dt));
            bool exitsScreen = end >= width - 0.01;
            double speed = (104 - 35 * actor.PlowResistance) * scale;
            if (!exitsScreen) speed = Math.Min(speed, 24 * scale + (end - from) * 3);
            double to = Math.Min(from + speed * dt, end);
            double swept = ledge.Sweep(from, ref to);
            // Leave room for the forward plume to settle before the blade clears the screen edge.
            bool nearScreenEdge = exitsScreen && to >= width - 62 * scale;
            double loose = nearScreenEdge ? 0 : swept * (0.026 + 0.07 * actor.PlowResistance);
            actor.GatheredSnow += swept - loose;
            actor.SidePowder += loose;
            if (!nearScreenEdge && actor.GatheredSnow > capacity)
            {
                actor.Overflow += actor.GatheredSnow - capacity;
                actor.GatheredSnow = capacity;
            }
            actor.Offset = to - ledge.X;
            actor.X = to - DecorationArt.PlowBlade * scale;
            actor.Y = ledge.SupportAt(Math.Max(ledge.X, actor.X + 9 * scale));
            actor.SprayWait -= dt;
            if (!nearScreenEdge && actor.SprayWait <= 0 && (actor.SidePowder >= 0.6 || actor.Overflow >= 1.0))
            {
                sprayPlow(to, actor.Y, actor.SidePowder, actor.Overflow);
                actor.SidePowder = actor.Overflow = 0;
                actor.SprayWait = 0.073;
            }
            if (to < end && ledge.IsVisibleAt(to)) return true;
            actor.GatheredSnow += actor.SidePowder + actor.Overflow;
            actor.SidePowder = actor.Overflow = 0;
            actor.LeavingScreen = to >= width - 0.01;
            if (actor.GatheredSnow <= 0.001 && !actor.LeavingScreen) return false;
            actor.PlowEnd = actor.Offset;
            actor.Unloading = true;
            actor.UnloadLeft = Math.Min(0.91, 0.33 + Math.Sqrt(actor.GatheredSnow) * 0.019);
            actor.UnloadRate = actor.GatheredSnow / actor.UnloadLeft;
            return true;
        }

        private bool PourPlow(Actor actor, SnowLedge ledge, double dt)
        {
            double blade = ledge.X + actor.Offset;
            if (actor.LeavingScreen) actor.X += 93 * scale * dt;
            else
            {
                actor.X = blade - DecorationArt.PlowBlade * scale;
                actor.Y = ledge.SupportAt(Math.Max(ledge.X, actor.X + 9 * scale));
            }
            double flowing = Math.Min(dt, actor.UnloadLeft);
            actor.UnloadLeft -= flowing;
            actor.PourWait += flowing;
            if (actor.GatheredSnow > 0 && (actor.PourWait >= 0.061 || actor.UnloadLeft <= 0))
            {
                double mass = actor.UnloadLeft <= 0 ? actor.GatheredSnow : actor.UnloadRate * actor.PourWait;
                mass = Math.Min(actor.GatheredSnow, mass);
                spillPlow(ledge, blade, mass);
                actor.GatheredSnow -= mass;
                actor.PourWait = 0;
            }
            return actor.UnloadLeft > 0 || (actor.LeavingScreen && actor.X - DecorationArt.PlowRear * scale < width);
        }

        private void Spill(Actor actor)
        {
            double mass = actor.GatheredSnow + actor.SidePowder + actor.Overflow;
            if (mass <= 0) return;
            spillPlow(actor.Ledge, actor.Ledge.X + actor.Offset, mass);
            actor.GatheredSnow = actor.SidePowder = actor.Overflow = 0;
        }

        private bool MoveTree(Actor actor, double dt, List<SnowLedge> ledges)
        {
            SnowLedge ledge = actor.Ledge;
            if (ledge != null)
            {
                double shift = ledge.Width == actor.LastWidth ? ledge.X - actor.LastLeft : 0;
                if (actor.Age < 31 && ledges.Contains(ledge) && Math.Abs(shift) < 55 * scale &&
                    ledge.IsVisibleAt(actor.X + shift))
                {
                    actor.X += shift;
                    double support = ledge.SupportAt(actor.X);
                    actor.Y += support - actor.RootSupport;
                    actor.RootSupport = support;
                    actor.Snowline = ledge.SurfaceAt(actor.X) + 0.7;
                    double footing = Math.Min(support + 0.7, actor.Snowline + 3.5 * scale);
                    actor.Y += Math.Max(0, footing - actor.Y) * (1 - Math.Exp(-2.1 * dt));
                    actor.LastLeft = ledge.X;
                    actor.LastWidth = ledge.Width;
                    actor.SettledTime += dt;
                    return true;
                }
                actor.Ledge = null;
                actor.VX = Math.Abs(shift) > 0.1 ? Math.Sign(shift) * 54 : 24;
                actor.VY = 44;
            }
            double oldY = actor.Y, speed = actor.VY;
            actor.VY = Math.Min(435, actor.VY + 490 * dt);
            actor.Y += (speed + actor.VY) * dt * 0.5;
            actor.X = Clamp(actor.X + actor.VX * dt, DecorationArt.TreeWidth * scale * 0.5,
                width - DecorationArt.TreeWidth * scale * 0.5);
            actor.VX *= Math.Exp(-1.8 * dt);
            if (actor.Age < 31 && actor.Y > oldY)
            {
                foreach (SnowLedge support in ledges)
                {
                    if (!support.IsVisibleAt(actor.X)) continue;
                    double surface = support.SurfaceAt(actor.X);
                    if (oldY > surface + 0.8 || actor.Y < surface) continue;
                    support.Press(actor.X, 5.3 * scale, 2.5 * scale, true);
                    actor.Y = actor.Snowline = support.SurfaceAt(actor.X) + 0.7;
                    actor.RootSupport = support.SupportAt(actor.X);
                    actor.Ledge = support;
                    actor.LastLeft = support.X;
                    actor.LastWidth = support.Width;
                    actor.SettledTime = 0;
                    break;
                }
            }
            return actor.Y < height + DecorationArt.TreeHeight * scale;
        }

        internal void Draw(Canvas drawing)
        {
            foreach (Actor actor in actors)
            {
                double y = actor.Y, angle = 0, phase = 0, size = scale;
                double visibleBottom = double.PositiveInfinity;
                switch (actor.Kind)
                {
                    case Effect.Trees:
                        angle = actor.Ledge == null ? Clamp(actor.VX * 0.018, -8, 8)
                            : Math.Sin(actor.SettledTime * 15) * 5 * Math.Exp(-actor.SettledTime * 5);
                        if (actor.Ledge != null) visibleBottom = actor.Snowline;
                        break;
                    case Effect.Santa:
                        size *= DecorationArt.SantaScale;
                        phase = actor.Offset / (DecorationArt.SantaStepLength * scale);
                        break;
                    case Effect.Plow:
                        phase = (actor.X - actor.Ledge.X) / (6.5 * scale);
                        if (actor.GatheredSnow > 0.001)
                        {
                            double heap = Math.Sqrt(actor.GatheredSnow);
                            double blade = actor.X + DecorationArt.PlowBlade * scale;
                            double fullness = 1 - Math.Exp(-heap / 21);
                            double spread = (5 + 38 * fullness) * scale;
                            double nearEdge = actor.LeavingScreen ? 0 : Clamp(1 - (actor.PlowEnd - actor.Offset) / (18 * scale), 0, 1);
                            drawing.DrawSnowHeap(blade - spread * 0.32 * nearEdge,
                                actor.Ledge.SupportAt(blade), (2 + 37 * fullness) * scale, spread);
                        }
                        break;
                    case Effect.Sleigh:
                        phase = actor.Age * 13;
                        if (flourish)
                        {
                            y += 11 * Math.Sin(actor.Age * 0.92);
                            angle = -2.2 * Math.Cos(actor.Age * 0.92);
                        }
                        break;
                    case Effect.Gondola:
                        drawing.DrawLine(Cable, new Point(0, CableHeight(0)), new Point(width, CableHeight(width)));
                        y = CableHeight(actor.X);
                        break;
                }
                if (actor.Ledge != null)
                    drawing.DrawEllipse(ContactShadow, null, new Point(actor.X, Math.Min(actor.Y, visibleBottom) + 0.55),
                        (actor.Kind == Effect.Plow ? 22 : 7.8) * scale, 1.1 * scale);
                if (actor.Kind == Effect.Gondola)
                {
                    double swing = flourish ? Math.Sin(actor.Age * 2.1) * 2.1 : 0;
                    drawing.DrawDecoration(actor.Kind, actor.X, y, size, swing, phase, 43);
                }
                else drawing.DrawDecoration(actor.Kind, actor.X, y, size, flourish ? angle : 0, phase, 0, visibleBottom);
            }
        }

        private double CableHeight(double x) { return height * (0.26 - 0.11 * x / width); }
        private static double Clamp(double value, double min, double max) { return Math.Max(min, Math.Min(max, value)); }
    }
}
