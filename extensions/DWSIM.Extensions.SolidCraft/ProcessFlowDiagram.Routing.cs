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
    public static partial class ProcessFlowDiagram
    {
        /// <summary>
        /// Routes the lines of the diagram with horizontal and vertical segments only, as short as
        /// they can be while keeping a clearance from the items: a shortest path (Dijkstra) over a
        /// sparse grid made of the edges of the items, grown by the clearance, and the lanes half
        /// way between them. A bend costs a little length, so a route does not zigzag; running
        /// along a line already routed, or through the texts under an item, costs more.
        /// </summary>
        sealed class Router
        {
            public struct Box
            {
                public double X0, Y0, X1, Y1;
                public Box(double x0, double y0, double x1, double y1) { X0 = Math.Min(x0, x1); Y0 = Math.Min(y0, y1); X1 = Math.Max(x0, x1); Y1 = Math.Max(y0, y1); }
                public Box Grown(double d) => new Box(X0 - d, Y0 - d, X1 + d, Y1 + d);
                public bool Inside(double x, double y) => x > X0 + Eps && x < X1 - Eps && y > Y0 + Eps && y < Y1 - Eps;

                /// <summary>Whether an axis-aligned segment runs through the inside of the box.</summary>
                public bool Cut(double ax, double ay, double bx, double by)
                {
                    if (Math.Abs(ay - by) <= Eps)
                        return ay > Y0 + Eps && ay < Y1 - Eps && Math.Max(ax, bx) > X0 + Eps && Math.Min(ax, bx) < X1 - Eps;
                    return ax > X0 + Eps && ax < X1 - Eps && Math.Max(ay, by) > Y0 + Eps && Math.Min(ay, by) < Y1 - Eps;
                }
            }

            const double Eps = 1e-6;

            /// <summary>The cost of a bend, in millimetres of line.</summary>
            const double BendCost = 6;

            /// <summary>Extra cost per millimetre along a line already routed, and through a text.</summary>
            const double OverlapCost = 3, TextCost = 2;

            readonly List<Box> hard, soft;
            readonly double clearance;

            public double Clearance => clearance;
            readonly List<(Pt A, Pt B)> routed = new List<(Pt, Pt)>();

            public Router(IEnumerable<Box> items, IEnumerable<Box> texts, double clearance)
            {
                this.clearance = clearance;
                hard = items.Select(b => b.Grown(clearance)).ToList();
                soft = texts.ToList();
            }

            static readonly int[] Dx = { 1, 0, -1, 0 }, Dy = { 0, 1, 0, -1 };

            /// <summary>The direction (0 +x, 1 +y, 2 -x, 3 -y) of a unit vector.</summary>
            public static int Dir(double dx, double dy) => Math.Abs(dx) >= Math.Abs(dy) ? (dx >= 0 ? 0 : 2) : (dy >= 0 ? 1 : 3);

            /// <summary>
            /// The route from <paramref name="start"/> to <paramref name="end"/>: straight out of the
            /// start to <paramref name="s"/>, clear of its item, leaving in <paramref name="startDir"/>,
            /// then the shortest clear way to <paramref name="e"/>, and straight into the end in
            /// <paramref name="endDir"/>. Null when the items leave no way through.
            /// </summary>
            public List<Pt> Route(Pt start, Pt s, int startDir, Pt e, Pt end, int endDir, bool slideEnd = false)
            {
                // on the grid's own rounding; with slideEnd, an end point inside an item's clearance
                // (an actuator on its valve) is reached by a straight run in from outside it
                Pt R(Pt p) => new Pt(Math.Round(p.X, 3), Math.Round(p.Y, 3));
                s = R(s); e = R(e);
                if (slideEnd) e = Out(e, (endDir + 2) % 4);
                if (hard.Any(b => b.Inside(s.X, s.Y)) || hard.Any(b => b.Inside(e.X, e.Y))) return null;

                // the grid: the grown items' edges, the lanes half way between them, the two ends
                // and a ring outside everything
                var xs = new List<double> { s.X, e.X };
                var ys = new List<double> { s.Y, e.Y };
                foreach (var b in hard) { xs.Add(b.X0); xs.Add(b.X1); ys.Add(b.Y0); ys.Add(b.Y1); }
                double minX = xs.Min() - clearance, maxX = xs.Max() + clearance, minY = ys.Min() - clearance, maxY = ys.Max() + clearance;
                xs.Add(minX); xs.Add(maxX); ys.Add(minY); ys.Add(maxY);
                xs = Lanes(xs); ys = Lanes(ys);
                int nx = xs.Count, ny = ys.Count;
                int si = xs.IndexOf(s.X), sj = ys.IndexOf(s.Y), ei = xs.IndexOf(e.X), ej = ys.IndexOf(e.Y);
                if (si < 0 || sj < 0 || ei < 0 || ej < 0) return null;

                var free = new bool[nx, ny];
                for (var i = 0; i < nx; i++)
                    for (var j = 0; j < ny; j++)
                        free[i, j] = !hard.Any(b => b.Inside(xs[i], ys[j]));

                // Dijkstra over (node, direction of arrival)
                var cost = new double[nx, ny, 4];
                var prev = new int[nx, ny, 4];
                for (var i = 0; i < nx; i++) for (var j = 0; j < ny; j++) for (var d = 0; d < 4; d++) { cost[i, j, d] = double.MaxValue; prev[i, j, d] = -1; }
                var heap = new Heap();
                cost[si, sj, startDir] = 0;
                heap.Push(0, Key(si, sj, startDir, ny));
                int best = -1;
                var bestTotal = double.MaxValue;
                while (heap.Count > 0)
                {
                    var (c, key) = heap.Pop();
                    Unkey(key, ny, out var i, out var j, out var d);
                    if (c > cost[i, j, d]) continue;
                    if (i == ei && j == ej)
                    {
                        // the last leg goes into the nozzle in its direction
                        var total = c + (d == endDir ? 0 : (d == (endDir + 2) % 4 ? 2 * BendCost : BendCost));
                        if (total < bestTotal) { bestTotal = total; best = d; }
                        continue;
                    }
                    for (var nd = 0; nd < 4; nd++)
                    {
                        if (nd == (d + 2) % 4) continue;   // no turning back
                        int ni = i + Dx[nd], nj = j + Dy[nd];
                        if (ni < 0 || nj < 0 || ni >= nx || nj >= ny || !free[ni, nj]) continue;
                        double ax = xs[i], ay = ys[j], bx = xs[ni], by = ys[nj];
                        if (hard.Any(b => b.Cut(ax, ay, bx, by))) continue;
                        var len = Math.Abs(bx - ax) + Math.Abs(by - ay);
                        var nc = c + len + (nd != d ? BendCost : 0) + Penalty(ax, ay, bx, by, len);
                        if (nc < cost[ni, nj, nd])
                        {
                            cost[ni, nj, nd] = nc;
                            prev[ni, nj, nd] = key;
                            heap.Push(nc, Key(ni, nj, nd, ny));
                        }
                    }
                }
                if (best < 0) return null;

                var path = new List<Pt>();
                var k = Key(ei, ej, best, ny);
                while (k >= 0)
                {
                    Unkey(k, ny, out var i, out var j, out var d);
                    path.Add(new Pt(xs[i], ys[j]));
                    k = prev[i, j, d];
                }
                path.Reverse();
                path.Insert(0, start);
                path.Add(end);
                return Simplify(path);
            }

            /// <summary>A point moved along a direction until no item's clearance holds it (a few boxes at most).</summary>
            Pt Out(Pt p, int dir)
            {
                for (var n = 0; n < 4; n++)
                {
                    var box = hard.FirstOrDefault(b => b.Inside(p.X, p.Y));
                    if (box.X1 <= box.X0) break;
                    p = dir == 0 ? new Pt(box.X1, p.Y) : dir == 1 ? new Pt(p.X, box.Y1) : dir == 2 ? new Pt(box.X0, p.Y) : new Pt(p.X, box.Y0);
                    p = new Pt(Math.Round(p.X, 3), Math.Round(p.Y, 3));
                }
                return p;
            }

            /// <summary>Adds an obstacle (an instrument bubble), kept clear like the items.</summary>
            public void Block(Box b) => hard.Add(b.Grown(clearance));

            /// <summary>Keeps a route, so the next ones keep off it.</summary>
            public void Add(List<Pt> route)
            {
                for (var i = 0; i + 1 < route.Count; i++) routed.Add((route[i], route[i + 1]));
            }

            double Penalty(double ax, double ay, double bx, double by, double len)
            {
                double p = 0;
                foreach (var t in soft) if (t.Cut(ax, ay, bx, by)) p += TextCost * len;
                var horizontal = Math.Abs(ay - by) <= Eps;
                foreach (var (a, b) in routed)
                {
                    if (horizontal && Math.Abs(a.Y - b.Y) <= Eps && Math.Abs(a.Y - ay) <= 0.5)
                        p += OverlapCost * Math.Max(0, Math.Min(Math.Max(ax, bx), Math.Max(a.X, b.X)) - Math.Max(Math.Min(ax, bx), Math.Min(a.X, b.X)));
                    else if (!horizontal && Math.Abs(a.X - b.X) <= Eps && Math.Abs(a.X - ax) <= 0.5)
                        p += OverlapCost * Math.Max(0, Math.Min(Math.Max(ay, by), Math.Max(a.Y, b.Y)) - Math.Max(Math.Min(ay, by), Math.Min(a.Y, b.Y)));
                }
                return p;
            }

            /// <summary>The coordinates, sorted and distinct, with the lane half way between each pair.</summary>
            static List<double> Lanes(List<double> v)
            {
                var u = v.Select(x => Math.Round(x, 3)).Distinct().OrderBy(x => x).ToList();
                var o = new List<double>();
                for (var i = 0; i < u.Count; i++)
                {
                    o.Add(u[i]);
                    if (i + 1 < u.Count && u[i + 1] - u[i] > 1) o.Add(Math.Round((u[i] + u[i + 1]) / 2, 3));
                }
                return o;
            }

            static int Key(int i, int j, int d, int ny) => ((i * ny) + j) * 4 + d;

            static void Unkey(int key, int ny, out int i, out int j, out int d)
            {
                d = key % 4; var n = key / 4; j = n % ny; i = n / ny;
            }

            /// <summary>A binary min-heap of (cost, key).</summary>
            sealed class Heap
            {
                readonly List<(double C, int K)> a = new List<(double, int)>();
                public int Count => a.Count;

                public void Push(double c, int k)
                {
                    a.Add((c, k));
                    var i = a.Count - 1;
                    while (i > 0)
                    {
                        var p = (i - 1) / 2;
                        if (a[p].C <= a[i].C) break;
                        (a[p], a[i]) = (a[i], a[p]); i = p;
                    }
                }

                public (double, int) Pop()
                {
                    var top = a[0];
                    a[0] = a[a.Count - 1];
                    a.RemoveAt(a.Count - 1);
                    var i = 0;
                    while (true)
                    {
                        int l = 2 * i + 1, r = l + 1, m = i;
                        if (l < a.Count && a[l].C < a[m].C) m = l;
                        if (r < a.Count && a[r].C < a[m].C) m = r;
                        if (m == i) break;
                        (a[m], a[i]) = (a[i], a[m]); i = m;
                    }
                    return top;
                }
            }
        }
    }
}
