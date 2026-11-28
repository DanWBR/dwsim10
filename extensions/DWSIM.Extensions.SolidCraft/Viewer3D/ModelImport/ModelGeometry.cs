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

namespace DWSIM.Extensions.SolidCraft.Viewer3D.ModelImport
{
    /// <summary>
    /// A piece of a model that does not share any vertex with the rest of its part, such as a
    /// flange, a length of pipe or a beam: CAD programs keep every solid as a closed shell of its
    /// own, even when they merge a whole plant into one object. Lengths are in metres, in the frame
    /// of the plant (Z up).
    /// </summary>
    public class Island
    {
        public int Index;
        /// <summary>The part of the model the island belongs to.</summary>
        public int Part;
        /// <summary>The triangles of the part that make the island (their numbers, not their indices).</summary>
        public int[] Triangles;
        public double[] Min, Max, Centroid;
        /// <summary>The surface area, m².</summary>
        public double Area;
        /// <summary>What the island looks like (see <see cref="IslandShapes"/>), once it is classified.</summary>
        public IslandShape Shape;

        public double[] Size => new[] { Max[0] - Min[0], Max[1] - Min[1], Max[2] - Min[2] };
    }

    /// <summary>The unit, the up axis and the place a model is put in the plant with.</summary>
    public class Placement3D
    {
        /// <summary>Metres per unit of the file.</summary>
        public double Scale = 1;
        public UpAxis Up = UpAxis.Y;
        /// <summary>The turn about the vertical axis, degrees, applied after the scale.</summary>
        public double Rotation;
        /// <summary>The move, metres in the plant frame, applied last.</summary>
        public double[] Offset = { 0, 0, 0 };
    }

    public static class ModelGeometry
    {
        /// <summary>
        /// The unit of a file that does not say it, from the size of the model: the candidate (m, cm,
        /// mm) that makes its largest dimension nearest a typical size in the log scale, 60 m for a
        /// plant and 6 m for a prop. A format that gives the unit (glTF) keeps it unless the model
        /// would then be larger than 5 km or smaller than 5 cm.
        /// </summary>
        public static double GuessScale(ImportedModel model, bool prop = false)
        {
            double[] min, max;
            if (!model.Bounds(out min, out max)) return 1;
            var extent = Math.Max(max[0] - min[0], Math.Max(max[1] - min[1], max[2] - min[2]));
            if (extent <= 0) return 1;
            var known = ScaleOf(model.Units);
            if (known > 0 && extent * known < 5000 && extent * known > 0.05) return known;
            var typical = prop ? 6.0 : 60.0;
            var best = 1.0;
            var bestDistance = double.MaxValue;
            foreach (var s in new[] { 1.0, 0.01, 0.001 })
            {
                var d = Math.Abs(Math.Log10(extent * s / typical));
                if (d < bestDistance - 1e-9) { bestDistance = d; best = s; }
            }
            return best;
        }

        public static double ScaleOf(ModelUnits u)
        {
            switch (u)
            {
                case ModelUnits.Meters: return 1;
                case ModelUnits.Centimeters: return 0.01;
                case ModelUnits.Millimeters: return 0.001;
                case ModelUnits.Feet: return 0.3048;
                case ModelUnits.Inches: return 0.0254;
                default: return 0;
            }
        }

        /// <summary>The up axis of a file, from its format, or Y (the most common) when it does not say.</summary>
        public static UpAxis GuessUp(ImportedModel model) => model.Up == UpAxis.Unknown ? UpAxis.Y : model.Up;

        /// <summary>
        /// Puts the model in the frame of the plant, in place: metres, Z up, turned and moved. A
        /// file with Y up has its Y become Z and its Z become -Y, as glTF and Godot have it.
        /// </summary>
        public static void Normalize(ImportedModel model, Placement3D place)
        {
            var s = place.Scale;
            var a = place.Rotation * Math.PI / 180.0;
            double c = Math.Cos(a), sn = Math.Sin(a);
            var off = place.Offset ?? new double[3];
            foreach (var p in model.Parts)
            {
                var v = p.Positions;
                for (var i = 0; i + 2 < v.Length; i += 3)
                {
                    double x = v[i] * s, y = v[i + 1] * s, z = v[i + 2] * s;
                    if (place.Up == UpAxis.Y) { var t = y; y = -z; z = t; }
                    var xr = c * x - sn * y;
                    var yr = sn * x + c * y;
                    v[i] = (float)(xr + off[0]); v[i + 1] = (float)(yr + off[1]); v[i + 2] = (float)(z + off[2]);
                }
            }
            model.Up = UpAxis.Z;
            model.Units = ModelUnits.Meters;
        }

        /// <summary>
        /// Moves a normalized model so that the centre of its base is at the origin: the way a prop
        /// stands on the ground where it is placed.
        /// </summary>
        public static void CenterOnBase(ImportedModel model)
        {
            double[] min, max;
            if (!model.Bounds(out min, out max)) return;
            float cx = (float)((min[0] + max[0]) / 2), cy = (float)((min[1] + max[1]) / 2), cz = (float)min[2];
            foreach (var p in model.Parts)
                for (var i = 0; i + 2 < p.Positions.Length; i += 3)
                {
                    p.Positions[i] -= cx; p.Positions[i + 1] -= cy; p.Positions[i + 2] -= cz;
                }
        }

        /// <summary>
        /// Splits every part into islands. The vertices closer than the tolerance (metres) are
        /// welded, and two triangles belong to the same island when they share a welded edge. When
        /// more than two triangles meet on an edge, as where two solids touch face to face, they are
        /// paired by their order round the edge, so that each solid keeps its own faces. Then the
        /// open pieces (a face whose edges another face of the same solid does not close, as at a
        /// T-junction of the tessellation) are joined to the open pieces they share a vertex with,
        /// while a closed solid is never joined to anything by a vertex. Triangles with a zero area
        /// are left out. Islands of different parts are never joined.
        /// </summary>
        public static List<Island> Islands(ImportedModel model, double tolerance = 1e-4)
        {
            var result = new List<Island>();
            for (var pi = 0; pi < model.Parts.Count; pi++)
            {
                var part = model.Parts[pi];
                var weld = Weld(part.Positions, tolerance);
                var byRoot = Components(part, weld);
                // in the order of their first triangle, so the numbering stays the same for the same file
                foreach (var list in byRoot.Values.OrderBy(l => l[0]))
                {
                    var island = new Island { Part = pi, Triangles = list.ToArray() };
                    Measure(part, island);
                    island.Index = result.Count;
                    result.Add(island);
                }
            }
            return result;
        }

        /// <summary>The triangles of each island of a part, by the root of its union-find.</summary>
        static Dictionary<int, List<int>> Components(MeshPart part, Welded weld)
        {
            var tris = part.TriangleCount;
            var w = new int[tris * 3];
            var keep = new bool[tris];
            for (var t = 0; t < tris; t++)
            {
                for (var k = 0; k < 3; k++) w[3 * t + k] = weld.Map[part.Indices[3 * t + k]];
                keep[t] = w[3 * t] != w[3 * t + 1] && w[3 * t + 1] != w[3 * t + 2] && w[3 * t] != w[3 * t + 2] && TriangleArea(part, t) > 0;
            }

            // the triangles on each welded edge
            var edges = new Dictionary<long, List<int>>();
            for (var t = 0; t < tris; t++)
            {
                if (!keep[t]) continue;
                for (var k = 0; k < 3; k++)
                {
                    var key = EdgeKey(w[3 * t + k], w[3 * t + (k + 1) % 3]);
                    List<int> list;
                    if (!edges.TryGetValue(key, out list)) edges[key] = list = new List<int>(2);
                    list.Add(t);
                }
            }

            var parent = new int[tris];
            for (var i = 0; i < tris; i++) parent[i] = i;
            foreach (var pair in edges)
            {
                var list = pair.Value;
                if (list.Count == 2) Union(parent, list[0], list[1]);
                else if (list.Count > 2) PairRoundEdge(part, w, pair.Key, list, parent);
            }

            // open pieces: a piece with an edge that only one of its own triangles has
            var edgeUse = new Dictionary<int, int>();
            var open = new HashSet<int>();
            foreach (var pair in edges)
            {
                edgeUse.Clear();
                foreach (var t in pair.Value)
                {
                    var r = Find(parent, t);
                    int n;
                    edgeUse.TryGetValue(r, out n);
                    edgeUse[r] = n + 1;
                }
                foreach (var u in edgeUse)
                    if (u.Value == 1) open.Add(u.Key);
            }
            if (open.Count > 0)
            {
                // the open pieces that share a welded vertex become one
                var firstOpen = new Dictionary<int, int>();
                var joins = new List<int[]>();
                for (var t = 0; t < tris; t++)
                {
                    if (!keep[t]) continue;
                    if (!open.Contains(Find(parent, t))) continue;
                    for (var k = 0; k < 3; k++)
                    {
                        int other;
                        if (firstOpen.TryGetValue(w[3 * t + k], out other)) joins.Add(new[] { other, t });
                        else firstOpen[w[3 * t + k]] = t;
                    }
                }
                foreach (var j in joins) Union(parent, j[0], j[1]);
            }

            var byRoot = new Dictionary<int, List<int>>();
            for (var t = 0; t < tris; t++)
            {
                if (!keep[t]) continue;
                var root = Find(parent, t);
                List<int> list;
                if (!byRoot.TryGetValue(root, out list)) byRoot[root] = list = new List<int>();
                list.Add(t);
            }
            return byRoot;
        }

        static long EdgeKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        /// <summary>
        /// Pairs the triangles that meet on an edge shared by more than two. They are sorted by
        /// their angle round the edge, and when a triangle's normal points back against that turn,
        /// the wedge up to the next triangle is the inside of its solid, so the two are joined. When
        /// two triangles are at the same angle (two solids face to face), the one whose normal points
        /// along the turn goes first, which leaves an empty wedge of no width between the solids.
        /// </summary>
        static void PairRoundEdge(MeshPart part, int[] w, long key, List<int> list, int[] parent)
        {
            int ea = (int)(key >> 32), eb = (int)(key & 0xFFFFFFFF);
            double[] pa = null, axis = null;
            var faces = new List<double[]>();
            foreach (var t in list)
            {
                int ia = -1, ib = -1, io = -1;
                for (var k = 0; k < 3; k++)
                {
                    var wv = w[3 * t + k];
                    if (wv == ea && ia < 0) ia = part.Indices[3 * t + k];
                    else if (wv == eb && ib < 0) ib = part.Indices[3 * t + k];
                    else io = part.Indices[3 * t + k];
                }
                if (ia < 0 || ib < 0 || io < 0) continue;
                if (pa == null)
                {
                    pa = P(part, ia);
                    var e = Sub(P(part, ib), pa);
                    var el = Math.Sqrt(Dot(e, e));
                    if (el <= 0) return;
                    axis = Mul(e, 1 / el);
                }
                var d = Sub(P(part, io), pa);
                d = Sub(d, Mul(axis, Dot(d, axis)));
                var n = Normal(part, t);
                faces.Add(new[] { t, 0, 0, d[0], d[1], d[2], n[0], n[1], n[2] });
            }
            if (faces.Count < 2) return;
            var u = Perpendicular(axis);
            var v = Cross(axis, u);
            foreach (var f in faces)
            {
                var d = new[] { f[3], f[4], f[5] };
                var theta = Math.Atan2(Dot(d, v), Dot(d, u));
                if (theta < 0) theta += 2 * Math.PI;
                f[1] = theta;
                f[2] = Dot(new[] { f[6], f[7], f[8] }, Cross(axis, d));
            }
            // by angle; at the same angle, the normal along the turn first
            faces.Sort((x, y) => Math.Abs(x[1] - y[1]) > 1e-6 ? x[1].CompareTo(y[1]) : y[2].CompareTo(x[2]));
            for (var i = 0; i < faces.Count; i++)
                if (faces[i][2] < 0)
                    Union(parent, (int)faces[i][0], (int)faces[(i + 1) % faces.Count][0]);
        }

        static double[] P(MeshPart part, int i) => new double[] { part.Positions[3 * i], part.Positions[3 * i + 1], part.Positions[3 * i + 2] };
        static double[] Sub(double[] a, double[] b) => new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] };
        static double[] Mul(double[] a, double f) => new[] { a[0] * f, a[1] * f, a[2] * f };
        static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        static double[] Cross(double[] a, double[] b) => new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };

        static double[] Perpendicular(double[] a)
        {
            var h = Math.Abs(a[2]) < 0.9 ? new[] { 0.0, 0, 1 } : new[] { 1.0, 0, 0 };
            var p = Cross(a, h);
            return Mul(p, 1 / Math.Sqrt(Dot(p, p)));
        }

        static double[] Normal(MeshPart part, int t)
        {
            var a = P(part, part.Indices[3 * t]);
            var b = P(part, part.Indices[3 * t + 1]);
            var c = P(part, part.Indices[3 * t + 2]);
            return Cross(Sub(b, a), Sub(c, a));
        }

        static void Measure(MeshPart part, Island island)
        {
            var min = new[] { double.MaxValue, double.MaxValue, double.MaxValue };
            var max = new[] { double.MinValue, double.MinValue, double.MinValue };
            var c = new double[3];
            double area = 0;
            var v = part.Positions;
            foreach (var t in island.Triangles)
            {
                var a = TriangleArea(part, t);
                area += a;
                for (var k = 0; k < 3; k++)
                {
                    var i = part.Indices[3 * t + k] * 3;
                    for (var d = 0; d < 3; d++)
                    {
                        var x = v[i + d];
                        if (x < min[d]) min[d] = x;
                        if (x > max[d]) max[d] = x;
                        c[d] += a * x / 3.0;
                    }
                }
            }
            island.Min = min;
            island.Max = max;
            island.Area = area;
            island.Centroid = area > 0 ? new[] { c[0] / area, c[1] / area, c[2] / area } : new[] { (min[0] + max[0]) / 2, (min[1] + max[1]) / 2, (min[2] + max[2]) / 2 };
        }

        public static double TriangleArea(MeshPart part, int t)
        {
            var v = part.Positions;
            int a = part.Indices[3 * t] * 3, b = part.Indices[3 * t + 1] * 3, c = part.Indices[3 * t + 2] * 3;
            double ux = v[b] - v[a], uy = v[b + 1] - v[a + 1], uz = v[b + 2] - v[a + 2];
            double wx = v[c] - v[a], wy = v[c + 1] - v[a + 1], wz = v[c + 2] - v[a + 2];
            double nx = uy * wz - uz * wy, ny = uz * wx - ux * wz, nz = ux * wy - uy * wx;
            return 0.5 * Math.Sqrt(nx * nx + ny * ny + nz * nz);
        }

        internal class Welded
        {
            /// <summary>The welded vertex of each vertex.</summary>
            public int[] Map;
            public int Count;
        }

        struct Cell : IEquatable<Cell>
        {
            public long X, Y, Z;
            public Cell(long x, long y, long z) { X = x; Y = y; Z = z; }
            public bool Equals(Cell o) => X == o.X && Y == o.Y && Z == o.Z;
            public override bool Equals(object obj) => obj is Cell && Equals((Cell)obj);
            public override int GetHashCode() => unchecked((int)(X * 73856093L ^ Y * 19349663L ^ Z * 83492791L));
        }

        /// <summary>
        /// Welds the vertices closer than the tolerance: each vertex goes to a grid cell of the
        /// tolerance's size, and it takes the first vertex already kept within the tolerance in its
        /// cell or the 26 cells round it.
        /// </summary>
        internal static Welded Weld(float[] positions, double tolerance)
        {
            var n = positions.Length / 3;
            var map = new int[n];
            var kept = new List<int>();
            var grid = new Dictionary<Cell, List<int>>();
            var tol = Math.Max(tolerance, 1e-9);
            var tol2 = tol * tol;
            for (var i = 0; i < n; i++)
            {
                double x = positions[3 * i], y = positions[3 * i + 1], z = positions[3 * i + 2];
                long cx = (long)Math.Floor(x / tol), cy = (long)Math.Floor(y / tol), cz = (long)Math.Floor(z / tol);
                var found = -1;
                for (var dx = -1; dx <= 1 && found < 0; dx++)
                    for (var dy = -1; dy <= 1 && found < 0; dy++)
                        for (var dz = -1; dz <= 1 && found < 0; dz++)
                        {
                            List<int> list;
                            if (!grid.TryGetValue(new Cell(cx + dx, cy + dy, cz + dz), out list)) continue;
                            foreach (var k in list)
                            {
                                var j = kept[k];
                                double ex = positions[3 * j] - x, ey = positions[3 * j + 1] - y, ez = positions[3 * j + 2] - z;
                                if (ex * ex + ey * ey + ez * ez <= tol2) { found = k; break; }
                            }
                        }
                if (found < 0)
                {
                    found = kept.Count;
                    kept.Add(i);
                    var cell = new Cell(cx, cy, cz);
                    List<int> list;
                    if (!grid.TryGetValue(cell, out list)) grid[cell] = list = new List<int>(1);
                    list.Add(found);
                }
                map[i] = found;
            }
            return new Welded { Map = map, Count = kept.Count };
        }

        static int Find(int[] parent, int i)
        {
            while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; }
            return i;
        }

        static void Union(int[] parent, int a, int b)
        {
            a = Find(parent, a); b = Find(parent, b);
            if (a != b) { if (a < b) parent[b] = a; else parent[a] = b; }
        }
    }
}
