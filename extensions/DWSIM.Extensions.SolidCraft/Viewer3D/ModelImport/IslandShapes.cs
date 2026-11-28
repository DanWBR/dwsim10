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
    /// <summary>What an island of a plant model looks like.</summary>
    public enum ShapeClass
    {
        Unknown,
        /// <summary>A long, thin cylinder: a length of pipe.</summary>
        Pipe,
        /// <summary>A small piece: a flange, an elbow, a valve, a bolt.</summary>
        Fitting,
        /// <summary>A vertical cylinder at most four diameters tall.</summary>
        VerticalVessel,
        /// <summary>A vertical cylinder more than four diameters tall.</summary>
        Column,
        /// <summary>A horizontal cylinder of vessel size: a drum, an exchanger shell, a bullet.</summary>
        HorizontalVessel,
        Sphere,
        /// <summary>A compact solid that is not round: a machine, a skid, a building.</summary>
        Box,
        /// <summary>A long, thin solid that is not round: a beam, a column of a structure, a rail.</summary>
        Beam,
        /// <summary>A thin, wide solid: a sheet, a floor plate, a wall.</summary>
        Plate,
        /// <summary>A large, flat piece at the bottom of the model: the ground or a slab.</summary>
        Ground
    }

    /// <summary>The shape of an island: its class and, for a round one, its axis, length and radius (metres).</summary>
    public class IslandShape
    {
        public ShapeClass Class;
        /// <summary>The main axis (unit vector); for a round island, the axis of the cylinder.</summary>
        public double[] Axis = { 0, 0, 1 };
        public double Length, Radius;
        /// <summary>The sizes along the three principal axes, largest first.</summary>
        public double[] Extents = new double[3];
    }

    /// <summary>
    /// Finds the shape of the islands of a model from their vertices: the principal axes, the
    /// sizes along them, and whether the vertices lie at one distance from an axis (a cylinder)
    /// or from a centre (a sphere). The thresholds are those of process plants: a pipe is at most
    /// 0.35 m in radius, a vessel more.
    /// </summary>
    public static class IslandShapes
    {
        public static List<IslandShape> Classify(ImportedModel model, IList<Island> islands)
        {
            var result = new List<IslandShape>(islands.Count);
            double[] min, max;
            model.Bounds(out min, out max);
            var floor = min[2];
            foreach (var island in islands)
                result.Add(Classify(model.Parts[island.Part], island, floor));
            return result;
        }

        public static IslandShape Classify(MeshPart part, Island island, double floor)
        {
            var shape = new IslandShape();
            var seen = new HashSet<int>();
            var pts = new List<double[]>();
            foreach (var t in island.Triangles)
                for (var k = 0; k < 3; k++)
                {
                    var v = part.Indices[3 * t + k];
                    if (seen.Add(v)) pts.Add(new double[] { part.Positions[3 * v], part.Positions[3 * v + 1], part.Positions[3 * v + 2] });
                }
            var size = new[] { island.Max[0] - island.Min[0], island.Max[1] - island.Min[1], island.Max[2] - island.Min[2] };
            var biggest = Math.Max(size[0], Math.Max(size[1], size[2]));
            if (pts.Count < 4)
            {
                shape.Class = ShapeClass.Fitting;
                shape.Extents = new[] { biggest, 0.0, 0.0 };
                return shape;
            }

            double[] mean, e1, e2, e3;
            Principal(pts, out mean, out e1, out e2, out e3);
            var ext = new[] { Span(pts, e1), Span(pts, e2), Span(pts, e3) };
            shape.Extents = ext;
            shape.Axis = e1;
            double a = ext[0], b = ext[1], c = ext[2];

            // a cylinder along the long axis, or a disk round the short one
            double r1, s1, r3, s3;
            int n1, n3;
            Radial(pts, mean, e1, e2, out r1, out s1, out n1);
            Radial(pts, mean, e3, e1, out r3, out s3, out n3);
            // round: the far vertices at one distance from the axis, and all round it (a square
            // prism has its corners at one distance too, but in four directions only)
            var longRound = b > 0 && c / b > 0.75 && r1 > 0 && s1 / r1 < 0.12 && n1 >= 7;
            // round about the short axis: a disk, or a squat tank (a storage tank is often wider than tall)
            var diskRound = a > 0 && b / a > 0.8 && r3 > 0 && s3 / r3 < 0.12 && n3 >= 7;
            double rs, ss;
            Spherical(pts, mean, out rs, out ss);
            // a sphere has vertices at every height along each axis; a cylinder as tall as it is wide
            // has its rims at one distance from the centre too, but at two heights only
            var sphere = a > 0 && c / a > 0.85 && rs > 0 && ss / rs < 0.06 && n1 >= 7 && n3 >= 7
                && Heights(pts, mean, e1, rs) >= 4 && Heights(pts, mean, e2, rs) >= 4 && Heights(pts, mean, e3, rs) >= 4;

            if (biggest < 0.05) shape.Class = ShapeClass.Fitting;
            else if (sphere)
            {
                shape.Class = rs > 0.5 ? ShapeClass.Sphere : ShapeClass.Fitting;
                shape.Radius = rs;
                shape.Length = 2 * rs;
            }
            else if (longRound)
            {
                shape.Radius = r1;
                shape.Length = a;
                var vertical = Math.Abs(e1[2]) > 0.85;
                var horizontal = Math.Abs(e1[2]) < 0.3;
                if (r1 <= 0.35) shape.Class = a >= 4 * r1 ? ShapeClass.Pipe : ShapeClass.Fitting;
                else if (vertical) shape.Class = a > 8 * r1 ? ShapeClass.Column : ShapeClass.VerticalVessel;
                else if (horizontal) shape.Class = ShapeClass.HorizontalVessel;
                else shape.Class = ShapeClass.Box;
            }
            else if (diskRound)
            {
                shape.Axis = e3;
                shape.Radius = r3;
                shape.Length = c;
                if (a < 1.5) shape.Class = ShapeClass.Fitting;
                else if (c < 0.15 * a || c < 0.3) shape.Class = ShapeClass.Plate;
                else if (Math.Abs(e3[2]) > 0.85) shape.Class = ShapeClass.VerticalVessel;
                else if (Math.Abs(e3[2]) < 0.3) shape.Class = ShapeClass.HorizontalVessel;
                else shape.Class = ShapeClass.Box;
            }
            else if (c < 0.5 && a > 10 && Math.Abs(e3[2]) > 0.9 && island.Min[2] - floor < 0.5) shape.Class = ShapeClass.Ground;
            else if (biggest < 0.6) shape.Class = ShapeClass.Fitting;
            else if (a > 3 * b && b < 0.6) shape.Class = ShapeClass.Beam;
            else if (c < 0.12 * b && c < 0.15) shape.Class = ShapeClass.Plate;
            else shape.Class = ShapeClass.Box;
            if (shape.Length == 0) shape.Length = a;
            return shape;
        }

        static double Span(List<double[]> pts, double[] axis)
        {
            double lo = double.MaxValue, hi = double.MinValue;
            foreach (var p in pts)
            {
                var d = p[0] * axis[0] + p[1] * axis[1] + p[2] * axis[2];
                if (d < lo) lo = d;
                if (d > hi) hi = d;
            }
            return hi - lo;
        }

        /// <summary>
        /// The mean distance of the vertices from a line through the centre, and its spread. The
        /// vertices nearer the axis than half the farthest (the centres of end caps) are left out.
        /// </summary>
        /// <summary>
        /// The mean distance of the vertices from a line through the centre, its spread, and in how
        /// many of 16 directions round the line they lie. The vertices nearer the axis than half the
        /// farthest (the centres of end caps) are left out.
        /// </summary>
        static void Radial(List<double[]> pts, double[] c, double[] axis, double[] across, out double mean, out double spread, out int directions)
        {
            var other = new[] { axis[1] * across[2] - axis[2] * across[1], axis[2] * across[0] - axis[0] * across[2], axis[0] * across[1] - axis[1] * across[0] };
            var d = new double[pts.Count];
            var ang = new double[pts.Count];
            var far = 0.0;
            for (var i = 0; i < pts.Count; i++)
            {
                double x = pts[i][0] - c[0], y = pts[i][1] - c[1], z = pts[i][2] - c[2];
                var along = x * axis[0] + y * axis[1] + z * axis[2];
                double px = x - along * axis[0], py = y - along * axis[1], pz = z - along * axis[2];
                d[i] = Math.Sqrt(px * px + py * py + pz * pz);
                ang[i] = Math.Atan2(px * other[0] + py * other[1] + pz * other[2], px * across[0] + py * across[1] + pz * across[2]);
                if (d[i] > far) far = d[i];
            }
            double s = 0, s2 = 0;
            var n = 0;
            var bins = new bool[16];
            for (var i = 0; i < d.Length; i++)
                if (d[i] >= 0.5 * far)
                {
                    s += d[i]; s2 += d[i] * d[i]; n++;
                    bins[Math.Min(15, (int)Math.Floor((ang[i] + Math.PI) / (2 * Math.PI) * 16))] = true;
                }
            mean = n > 0 ? s / n : 0;
            spread = n > 1 ? Math.Sqrt(Math.Max(0, s2 / n - mean * mean)) : 0;
            directions = bins.Count(x => x);
        }

        /// <summary>In how many of 8 bands of height along an axis (from -r to r round the centre) the vertices lie.</summary>
        static int Heights(List<double[]> pts, double[] c, double[] axis, double r)
        {
            var bands = new bool[8];
            foreach (var p in pts)
            {
                var h = ((p[0] - c[0]) * axis[0] + (p[1] - c[1]) * axis[1] + (p[2] - c[2]) * axis[2]) / r;
                bands[Math.Max(0, Math.Min(7, (int)Math.Floor((h + 1) * 4)))] = true;
            }
            return bands.Count(x => x);
        }

        static void Spherical(List<double[]> pts, double[] c, out double mean, out double spread)
        {
            double s = 0, s2 = 0;
            foreach (var p in pts)
            {
                double x = p[0] - c[0], y = p[1] - c[1], z = p[2] - c[2];
                var d = Math.Sqrt(x * x + y * y + z * z);
                s += d; s2 += d * d;
            }
            mean = s / pts.Count;
            spread = Math.Sqrt(Math.Max(0, s2 / pts.Count - mean * mean));
        }

        /// <summary>The mean of the points and the eigenvectors of their covariance, largest spread first (Jacobi rotations).</summary>
        static void Principal(List<double[]> pts, out double[] mean, out double[] e1, out double[] e2, out double[] e3)
        {
            mean = new double[3];
            foreach (var p in pts) for (var k = 0; k < 3; k++) mean[k] += p[k];
            for (var k = 0; k < 3; k++) mean[k] /= pts.Count;
            var m = new double[3, 3];
            foreach (var p in pts)
                for (var i = 0; i < 3; i++)
                    for (var j = 0; j < 3; j++)
                        m[i, j] += (p[i] - mean[i]) * (p[j] - mean[j]);
            var v = new double[3, 3] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
            for (var sweep = 0; sweep < 50; sweep++)
            {
                var off = Math.Abs(m[0, 1]) + Math.Abs(m[0, 2]) + Math.Abs(m[1, 2]);
                if (off < 1e-14 * (Math.Abs(m[0, 0]) + Math.Abs(m[1, 1]) + Math.Abs(m[2, 2]) + 1e-30)) break;
                for (var p = 0; p < 2; p++)
                    for (var q = p + 1; q < 3; q++)
                    {
                        if (Math.Abs(m[p, q]) < 1e-30) continue;
                        var theta = (m[q, q] - m[p, p]) / (2 * m[p, q]);
                        var t = Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1));
                        if (theta == 0) t = 1;
                        var cs = 1 / Math.Sqrt(t * t + 1);
                        var sn = t * cs;
                        for (var k = 0; k < 3; k++)
                        {
                            double mkp = m[k, p], mkq = m[k, q];
                            m[k, p] = cs * mkp - sn * mkq;
                            m[k, q] = sn * mkp + cs * mkq;
                        }
                        for (var k = 0; k < 3; k++)
                        {
                            double mpk = m[p, k], mqk = m[q, k];
                            m[p, k] = cs * mpk - sn * mqk;
                            m[q, k] = sn * mpk + cs * mqk;
                        }
                        for (var k = 0; k < 3; k++)
                        {
                            double vkp = v[k, p], vkq = v[k, q];
                            v[k, p] = cs * vkp - sn * vkq;
                            v[k, q] = sn * vkp + cs * vkq;
                        }
                    }
            }
            var order = new[] { 0, 1, 2 };
            Array.Sort(order, (x, y) => m[y, y].CompareTo(m[x, x]));
            e1 = new[] { v[0, order[0]], v[1, order[0]], v[2, order[0]] };
            e2 = new[] { v[0, order[1]], v[1, order[1]], v[2, order[1]] };
            e3 = new[] { v[0, order[2]], v[1, order[2]], v[2, order[2]] };
        }
    }
}
