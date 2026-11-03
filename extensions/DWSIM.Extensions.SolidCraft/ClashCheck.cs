//    Copyright 2026 Daniel Wagner O. de Medeiros
//
//    This file is part of DWSIM.
//
//    DWSIM is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    DWSIM is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// Finds where the model runs into itself: a line through an item, two lines that meet, two items
    /// that overlap. Worked out on the model's own shapes (lines as tubes along their routes, items as
    /// their cylinders, boxes and spheres) by sampling along the lines, which takes a fraction of a
    /// second; SolidCraft's exact interference check takes minutes on a plant.
    /// </summary>
    public static class ClashCheck
    {
        public class Clash
        {
            public string A, B, Kind;

            /// <summary>Line through an item: length inside, m. Lines that meet: how far they cut into each other, mm. Items: overlap along the tighter side, mm.</summary>
            public double Amount;

            /// <summary>Where it happens, mm.</summary>
            public Vec3 At;
        }

        const double Step = 50;   // mm between samples along a line

        public static List<Clash> Find(PlantModel model)
        {
            var clashes = new List<Clash>();
            var items = model.Equipment.Where(e => e.Visible).ToList();
            var solids = items.ToDictionary(e => e, e => e.Parts.Where(p => !p.NoClash).Select(p => p.Moved(e.Origin)).ToList());

            // lines through items
            foreach (var run in model.Pipes)
            {
                var r = run.OuterDiameter / 2 + run.Insulation;
                var internalLine = run.From == run.To;
                var allowance = Math.Max(150, 1.5 * run.OuterDiameter) + 2 * run.OuterDiameter;
                var total = RouteLength(run.Path);

                foreach (var item in items)
                {
                    // a line is inside its own item where it leaves the nozzle; a column's own lines are its business
                    var own = item.Tag == run.From || item.Tag == run.To;
                    if (own && internalLine) continue;

                    double inside = 0;
                    Vec3 first = default;
                    var along = 0.0;
                    for (var i = 1; i < run.Path.Count; i++)
                    {
                        var a = run.Path[i - 1];
                        var b = run.Path[i];
                        var length = (b - a).Length;
                        var n = Math.Max(1, (int)Math.Ceiling(length / Step));
                        for (var k = 0; k < n; k++)
                        {
                            var s = along + (k + 0.5) / n * length;
                            if (own && ((item.Tag == run.From && s < allowance) || (item.Tag == run.To && total - s < allowance))) continue;
                            var p = a + (b - a) * ((k + 0.5) / n);
                            if (solids[item].Any(prim => Inside(prim, p, r)))
                            {
                                if (inside == 0) first = p;
                                inside += length / n;
                            }
                        }
                        along += length;
                    }
                    // a graze next to its own nozzle is not a line through the item
                    if (inside > (own ? 300 : 0))
                        clashes.Add(new Clash { A = run.Tag, B = item.Tag, Kind = "line through item", Amount = inside / 1000, At = first });
                }
            }

            // lines that meet
            for (var i = 0; i < model.Pipes.Count; i++)
            {
                for (var j = i + 1; j < model.Pipes.Count; j++)
                {
                    var p = model.Pipes[i];
                    var q = model.Pipes[j];
                    var gap = p.OuterDiameter / 2 + p.Insulation + q.OuterDiameter / 2 + q.Insulation;
                    double worst = 0;
                    Vec3 at = default;
                    for (var a = 1; a < p.Path.Count; a++)
                        for (var b = 1; b < q.Path.Count; b++)
                        {
                            var d = SegmentDistance(p.Path[a - 1], p.Path[a], q.Path[b - 1], q.Path[b], out var c);
                            if (gap - d > worst) { worst = gap - d; at = c; }
                        }
                    if (worst <= 1) continue;
                    // lines that join at the same item (a mixer, a splitter, a recycle) meet there by design
                    var ends = new[] { p.Path[0], p.Path[p.Path.Count - 1], q.Path[0], q.Path[q.Path.Count - 1] };
                    var shared = new[] { p.From, p.To }.Intersect(new[] { q.From, q.To }).Any(t => t != "battery limit");
                    if (shared && ends.Any(e => (e - at).Length < 1500)) continue;
                    clashes.Add(new Clash { A = p.Tag, B = q.Tag, Kind = "lines meet", Amount = worst, At = at });
                }
            }

            // items that overlap, on their bounding boxes
            var boxes = items.ToDictionary(e => e, e => Bounds(solids[e]));
            for (var i = 0; i < items.Count; i++)
            {
                for (var j = i + 1; j < items.Count; j++)
                {
                    var a = boxes[items[i]];
                    var b = boxes[items[j]];
                    var ox = Math.Min(a.Item2.X, b.Item2.X) - Math.Max(a.Item1.X, b.Item1.X);
                    var oy = Math.Min(a.Item2.Y, b.Item2.Y) - Math.Max(a.Item1.Y, b.Item1.Y);
                    var oz = Math.Min(a.Item2.Z, b.Item2.Z) - Math.Max(a.Item1.Z, b.Item1.Z);
                    if (ox > 1 && oy > 1 && oz > 1)
                        clashes.Add(new Clash
                        {
                            A = items[i].Tag, B = items[j].Tag, Kind = "items overlap", Amount = Math.Min(ox, oy),
                            At = new Vec3((Math.Max(a.Item1.X, b.Item1.X) + Math.Min(a.Item2.X, b.Item2.X)) / 2,
                                          (Math.Max(a.Item1.Y, b.Item1.Y) + Math.Min(a.Item2.Y, b.Item2.Y)) / 2,
                                          (Math.Max(a.Item1.Z, b.Item1.Z) + Math.Min(a.Item2.Z, b.Item2.Z)) / 2)
                        });
                }
            }

            return clashes;
        }

        static double RouteLength(List<Vec3> path)
        {
            double l = 0;
            for (var i = 1; i < path.Count; i++) l += (path[i] - path[i - 1]).Length;
            return l;
        }

        /// <summary>Whether a sphere of radius r at p cuts into the solid.</summary>
        static bool Inside(Primitive s, Vec3 p, double r)
        {
            switch (s.Kind)
            {
                case PrimitiveKind.Sphere:
                case PrimitiveKind.Head:
                    return (p - s.Point).Length < s.Diameter / 2 + r;
                case PrimitiveKind.Box:
                    return Math.Abs(p.X - s.Point.X) < s.SizeX / 2 + r
                        && Math.Abs(p.Y - s.Point.Y) < s.SizeY / 2 + r
                        && Math.Abs(p.Z - s.Point.Z) < s.SizeZ / 2 + r;
                default:
                    var axis = s.AxisVector ?? (s.Axis == "X" ? Vec3.PlusX : s.Axis == "Y" ? new Vec3(0, 1, 0) : Vec3.PlusZ);
                    var d = p - s.Point;
                    var t = Vec3.Dot(d, axis);
                    if (t < -r || t > s.Height + r) return false;
                    return (d - axis * t).Length < s.Diameter / 2 + r;
            }
        }

        static Tuple<Vec3, Vec3> Bounds(List<Primitive> parts)
        {
            double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;
            foreach (var s in parts)
            {
                Vec3 lo, hi;
                switch (s.Kind)
                {
                    case PrimitiveKind.Box:
                        lo = s.Point - new Vec3(s.SizeX / 2, s.SizeY / 2, s.SizeZ / 2);
                        hi = s.Point + new Vec3(s.SizeX / 2, s.SizeY / 2, s.SizeZ / 2);
                        break;
                    case PrimitiveKind.Sphere:
                    case PrimitiveKind.Head:
                        lo = s.Point - new Vec3(s.Diameter / 2, s.Diameter / 2, s.Diameter / 2);
                        hi = s.Point + new Vec3(s.Diameter / 2, s.Diameter / 2, s.Diameter / 2);
                        break;
                    default:
                        var axis = s.AxisVector ?? (s.Axis == "X" ? Vec3.PlusX : s.Axis == "Y" ? new Vec3(0, 1, 0) : Vec3.PlusZ);
                        var e = s.Point + axis * s.Height;
                        var rr = s.Diameter / 2;
                        lo = new Vec3(Math.Min(s.Point.X, e.X) - rr, Math.Min(s.Point.Y, e.Y) - rr, Math.Min(s.Point.Z, e.Z) - (Math.Abs(axis.Z) > 0.99 ? 0 : rr));
                        hi = new Vec3(Math.Max(s.Point.X, e.X) + rr, Math.Max(s.Point.Y, e.Y) + rr, Math.Max(s.Point.Z, e.Z) + (Math.Abs(axis.Z) > 0.99 ? 0 : rr));
                        break;
                }
                x0 = Math.Min(x0, lo.X); y0 = Math.Min(y0, lo.Y); z0 = Math.Min(z0, lo.Z);
                x1 = Math.Max(x1, hi.X); y1 = Math.Max(y1, hi.Y); z1 = Math.Max(z1, hi.Z);
            }
            return Tuple.Create(new Vec3(x0, y0, z0), new Vec3(x1, y1, z1));
        }

        /// <summary>Shortest distance between two segments, and the midpoint of the closest pair of points.</summary>
        static double SegmentDistance(Vec3 p1, Vec3 q1, Vec3 p2, Vec3 q2, out Vec3 at)
        {
            var d1 = q1 - p1;
            var d2 = q2 - p2;
            var r = p1 - p2;
            double a = Vec3.Dot(d1, d1), e = Vec3.Dot(d2, d2), f = Vec3.Dot(d2, r);
            double s, t;
            if (a <= 1e-9 && e <= 1e-9) { s = t = 0; }
            else if (a <= 1e-9) { s = 0; t = Clamp(f / e); }
            else
            {
                var c = Vec3.Dot(d1, r);
                if (e <= 1e-9) { t = 0; s = Clamp(-c / a); }
                else
                {
                    var b = Vec3.Dot(d1, d2);
                    var denom = a * e - b * b;
                    s = denom > 1e-9 ? Clamp((b * f - c * e) / denom) : 0;
                    t = (b * s + f) / e;
                    if (t < 0) { t = 0; s = Clamp(-c / a); }
                    else if (t > 1) { t = 1; s = Clamp((b - c) / a); }
                }
            }
            var c1 = p1 + d1 * s;
            var c2 = p2 + d2 * t;
            at = (c1 + c2) * 0.5;
            return (c1 - c2).Length;
        }

        static double Clamp(double x) => x < 0 ? 0 : (x > 1 ? 1 : x);
    }
}
