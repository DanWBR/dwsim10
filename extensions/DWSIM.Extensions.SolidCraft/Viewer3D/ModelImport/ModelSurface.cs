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
using Newtonsoft.Json;

namespace DWSIM.Extensions.SolidCraft.Viewer3D.ModelImport
{
    /// <summary>A cut of islands by a plane, which the user made in the review: kept with the model and made again at each import.</summary>
    public class SurfaceCut
    {
        /// <summary>The islands cut, by their numbers before this cut.</summary>
        [JsonProperty("islands")] public List<int> Islands = new List<int>();
        /// <summary>A point of the plane and its normal, metres in the plant frame (Z up).</summary>
        [JsonProperty("p")] public double[] Point;
        [JsonProperty("n")] public double[] Normal;
        /// <summary>The box of the islands cut, before the cut (metres): where the cut is made again when the model file changes.</summary>
        [JsonProperty("min", NullValueHandling = NullValueHandling.Ignore)] public double[] Min;
        [JsonProperty("max", NullValueHandling = NullValueHandling.Ignore)] public double[] Max;
    }

    /// <summary>
    /// Splits a continuous surface into pieces, for a model that a photogrammetry or a laser scan
    /// made, or that a program fused and remeshed: the whole plant then is one island, and the
    /// islands of a CAD file are not there to tell the objects apart. The floor goes first (the
    /// plane most of the surface that faces up lies on). The rest grows into regions across its
    /// edges and stops where the surface bends into a concave crease, as where one object stands
    /// on another or a pipe goes into a vessel; the faces of the crease then go to the region
    /// nearest them. The crease is measured at a radius of a few edges and against the noise of
    /// the surface, so that a scan's noise does not break a face apart. Each region becomes an
    /// island, which the shapes and the segmentation then take as they take the islands of a CAD
    /// file; a region shaped as a run of pipe that turns is told as a pipe. A cut by a plane,
    /// which the user makes in the review, splits the islands that one region left together.
    /// </summary>
    public static class ModelSurface
    {
        /// <summary>The smallest island taken as a continuous surface: triangles, and metres across in plan.</summary>
        public static int MinTriangles = 5000;
        public static double MinSpan = 5.0;
        /// <summary>The mean sine of the angle the neighbours of a vertex rise over its plane, above which the vertex is on a concave crease.</summary>
        public static double CreaseRise = 0.05;
        /// <summary>How many times the noise of the surface (the median rise) a crease must rise.</summary>
        public static double NoiseFactor = 3.0;
        /// <summary>The radius a vertex looks round, in median edge lengths.</summary>
        public static double CreaseScale = 3.0;

        /// <summary>
        /// The islands with the continuous surfaces split: the islands of a file's tree (a part
        /// with a path deeper than two) and the small ones stay as they are. Their numbers follow
        /// the new order.
        /// </summary>
        public static List<Island> Split(ImportedModel model, List<Island> islands, double tolerance, out int surfaces)
        {
            surfaces = 0;
            var result = new List<Island>(islands.Count);
            var welds = new Dictionary<int, ModelGeometry.Welded>();
            foreach (var island in islands)
            {
                var part = model.Parts[island.Part];
                if (!IsSurface(part, island))
                {
                    result.Add(island);
                    continue;
                }
                ModelGeometry.Welded weld;
                if (!welds.TryGetValue(island.Part, out weld)) welds[island.Part] = weld = ModelGeometry.Weld(part.Positions, tolerance);
                var regions = Regions(part, weld, island.Triangles);
                if (regions.Count < 2)
                {
                    result.Add(island);
                    continue;
                }
                surfaces++;
                foreach (var r in regions)
                {
                    var piece = new Island { Part = island.Part, Triangles = r, Region = true };
                    ModelGeometry.Measure(part, piece);
                    result.Add(piece);
                }
            }
            for (var i = 0; i < result.Count; i++) result[i].Index = i;
            return result;
        }

        static bool IsSurface(MeshPart part, Island island)
        {
            if (part.Path != null && part.Path.Length > 2) return false;
            if (island.Triangles.Length < MinTriangles) return false;
            return Math.Max(island.Max[0] - island.Min[0], island.Max[1] - island.Min[1]) >= MinSpan;
        }

        /// <summary>The regions of a surface (lists of its triangles): the floor first when it has one, then the others by size.</summary>
        internal static List<int[]> Regions(MeshPart part, ModelGeometry.Welded weld, int[] triangles)
        {
            var n = triangles.Length;
            var normal = new double[n * 3];
            var centre = new double[n * 3];
            var area = new double[n];
            var v = part.Positions;
            for (var k = 0; k < n; k++)
            {
                var t = triangles[k];
                int a = part.Indices[3 * t] * 3, b = part.Indices[3 * t + 1] * 3, c = part.Indices[3 * t + 2] * 3;
                double ux = v[b] - v[a], uy = v[b + 1] - v[a + 1], uz = v[b + 2] - v[a + 2];
                double wx = v[c] - v[a], wy = v[c + 1] - v[a + 1], wz = v[c + 2] - v[a + 2];
                double nx = uy * wz - uz * wy, ny = uz * wx - ux * wz, nz = ux * wy - uy * wx;
                var len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                area[k] = len / 2;
                if (len > 0) { normal[3 * k] = nx / len; normal[3 * k + 1] = ny / len; normal[3 * k + 2] = nz / len; }
                for (var d = 0; d < 3; d++) centre[3 * k + d] = (v[a + d] + v[b + d] + v[c + d]) / 3.0;
            }

            var floor = Floor(normal, centre, area);

            // the triangles on each welded edge, and the welded vertices of each triangle
            var edges = new Dictionary<long, List<int>>(n * 2, ModelGeometry.PairKeys.Instance);
            var corner = new int[n * 3];
            for (var k = 0; k < n; k++)
            {
                var t = triangles[k];
                for (var e = 0; e < 3; e++)
                {
                    int p = weld.Map[part.Indices[3 * t + e]], q = weld.Map[part.Indices[3 * t + (e + 1) % 3]];
                    corner[3 * k + e] = p;
                    if (p == q) continue;
                    var key = p < q ? ((long)p << 32) | (uint)q : ((long)q << 32) | (uint)p;
                    List<int> list;
                    if (!edges.TryGetValue(key, out list)) edges[key] = list = new List<int>(2);
                    list.Add(k);
                }
            }
            var crease = Creases(part, triangles, corner, normal, area, edges.Keys, floor);

            var parent = new int[n];
            for (var k = 0; k < n; k++) parent[k] = k;
            var shared = new List<int[]>();
            var across = new List<int>[n];
            foreach (var list in edges.Values)
            {
                if (list.Count != 2) continue;
                int x = list[0], y = list[1];
                (across[x] ?? (across[x] = new List<int>(3))).Add(y);
                (across[y] ?? (across[y] = new List<int>(3))).Add(x);
                shared.Add(new[] { x, y });
                if (floor[x] != floor[y]) continue;
                if (floor[x] || !crease[x] && !crease[y]) Union(parent, x, y);
            }

            // the faces of a crease go to the region nearest them, by steps across their edges
            var queue = new Queue<int>();
            var taken = new bool[n];
            for (var k = 0; k < n; k++)
                if (!crease[k] || floor[k]) { taken[k] = true; queue.Enqueue(k); }
            while (queue.Count > 0)
            {
                var x = queue.Dequeue();
                if (across[x] == null) continue;
                foreach (var y in across[x])
                {
                    if (taken[y] || floor[x]) continue;
                    taken[y] = true;
                    Union(parent, x, y);
                    queue.Enqueue(y);
                }
            }

            // the floor is one region, even where the objects that stand on it break it in pieces
            var firstFloor = -1;
            for (var k = 0; k < n; k++)
                if (floor[k]) { if (firstFloor < 0) firstFloor = k; else Union(parent, firstFloor, k); }

            // a region too small to be an object (the faces of a rounded crease, the noise of a
            // scan) goes to the neighbour it shares the longest border with
            var total = area.Sum();
            var small = Math.Max(0.02, 2e-4 * total);
            for (var pass = 0; pass < 4; pass++)
            {
                var size = new Dictionary<int, double>();
                var count = new Dictionary<int, int>();
                for (var k = 0; k < n; k++)
                {
                    var r = Find(parent, k);
                    double s; size.TryGetValue(r, out s); size[r] = s + area[k];
                    int c; count.TryGetValue(r, out c); count[r] = c + 1;
                }
                var border = new Dictionary<long, int>(ModelGeometry.PairKeys.Instance);
                foreach (var e in shared)
                {
                    int ra = Find(parent, e[0]), rb = Find(parent, e[1]);
                    if (ra == rb) continue;
                    var key = ra < rb ? ((long)ra << 32) | (uint)rb : ((long)rb << 32) | (uint)ra;
                    int c; border.TryGetValue(key, out c); border[key] = c + 1;
                }
                var best = new Dictionary<int, KeyValuePair<int, int>>();
                foreach (var b in border)
                {
                    int ra = (int)(b.Key >> 32), rb = (int)(b.Key & 0xFFFFFFFF);
                    foreach (var pair in new[] { new[] { ra, rb }, new[] { rb, ra } })
                    {
                        KeyValuePair<int, int> was;
                        if (!best.TryGetValue(pair[0], out was) || b.Value > was.Value) best[pair[0]] = new KeyValuePair<int, int>(pair[1], b.Value);
                    }
                }
                var merged = 0;
                foreach (var r in size.Keys.OrderBy(r => size[r]).ToList())
                {
                    if (size[r] >= small && count[r] >= 12) continue;
                    KeyValuePair<int, int> to;
                    if (!best.TryGetValue(r, out to)) continue;
                    if (firstFloor >= 0 && Find(parent, r) == Find(parent, firstFloor)) continue;
                    if (Find(parent, r) == Find(parent, to.Key)) continue;
                    Union(parent, r, to.Key);
                    merged++;
                }
                if (merged == 0) break;
            }

            var byRoot = new Dictionary<int, List<int>>();
            for (var k = 0; k < n; k++)
            {
                var r = Find(parent, k);
                List<int> list;
                if (!byRoot.TryGetValue(r, out list)) byRoot[r] = list = new List<int>();
                list.Add(triangles[k]);
            }
            var floorRoot = firstFloor >= 0 ? Find(parent, firstFloor) : -1;
            var order = byRoot.Keys.OrderByDescending(r => r == floorRoot).ThenByDescending(r => byRoot[r].Count).ThenBy(r => r);
            return order.Select(r => byRoot[r].ToArray()).ToList();
        }

        /// <summary>
        /// The faces that touch a concave crease, where a pipe goes into a vessel or a vessel stands
        /// on a skid. Each vertex looks at the vertices within a radius of a few edges (that face
        /// the same side of the surface): when they rise, on average, above the plane of the mean
        /// normal there (the mean of the sines of their angles over it is above the limit), the
        /// vertex is on a crease. At that scale the noise of a scan averages out, and a crease
        /// that a remesh rounded over a few faces still shows. The floor's own vertices do not count.
        /// </summary>
        static bool[] Creases(MeshPart part, int[] triangles, int[] corner, double[] normal, double[] area,
            IEnumerable<long> edgeKeys, bool[] floor)
        {
            var n = triangles.Length;
            var local = new Dictionary<int, int>();
            var pos = new List<double>();
            var nrm = new List<double>();
            var onFloor = new List<bool>();
            for (var k = 0; k < n; k++)
            {
                var t = triangles[k];
                for (var e = 0; e < 3; e++)
                {
                    var w = corner[3 * k + e];
                    int li;
                    if (!local.TryGetValue(w, out li))
                    {
                        li = local.Count;
                        local[w] = li;
                        var vi = part.Indices[3 * t + e] * 3;
                        pos.Add(part.Positions[vi]); pos.Add(part.Positions[vi + 1]); pos.Add(part.Positions[vi + 2]);
                        nrm.Add(0); nrm.Add(0); nrm.Add(0);
                        onFloor.Add(true);
                    }
                    for (var d = 0; d < 3; d++) nrm[3 * li + d] += area[k] * normal[3 * k + d];
                    if (!floor[k]) onFloor[li] = false;
                }
            }
            var m = local.Count;
            var unit = new double[3 * m];
            for (var i = 0; i < m; i++)
            {
                var nl = Math.Sqrt(nrm[3 * i] * nrm[3 * i] + nrm[3 * i + 1] * nrm[3 * i + 1] + nrm[3 * i + 2] * nrm[3 * i + 2]);
                if (nl > 0) for (var d = 0; d < 3; d++) unit[3 * i + d] = nrm[3 * i + d] / nl;
            }

            // the radius: a few times the median length of an edge
            var lengths = new List<double>();
            var every = 0;
            foreach (var key in edgeKeys)
            {
                if (every++ % 7 != 0) continue;
                int a, b;
                if (!local.TryGetValue((int)(key >> 32), out a) || !local.TryGetValue((int)(key & 0xFFFFFFFF), out b)) continue;
                double dx = pos[3 * b] - pos[3 * a], dy = pos[3 * b + 1] - pos[3 * a + 1], dz = pos[3 * b + 2] - pos[3 * a + 2];
                lengths.Add(Math.Sqrt(dx * dx + dy * dy + dz * dz));
            }
            if (lengths.Count == 0) return new bool[n];
            lengths.Sort();
            var radius = Math.Max(1e-3, CreaseScale * lengths[lengths.Count / 2]);
            var r2 = radius * radius;

            // the vertices in cells of the radius
            var cells = new Dictionary<long, List<int>>(ModelGeometry.PairKeys.Instance);
            Func<double, long> c = x => (long)Math.Floor(x / radius) & 0x1FFFFF;
            for (var i = 0; i < m; i++)
            {
                var key = c(pos[3 * i]) << 42 | c(pos[3 * i + 1]) << 21 | c(pos[3 * i + 2]);
                List<int> list;
                if (!cells.TryGetValue(key, out list)) cells[key] = list = new List<int>(8);
                list.Add(i);
            }
            var near = new List<int>(64);
            var value = new double[m];
            var measured = new bool[m];
            for (var i = 0; i < m; i++)
            {
                double px = pos[3 * i], py = pos[3 * i + 1], pz = pos[3 * i + 2];
                long cx = c(px), cy = c(py), cz = c(pz);
                near.Clear();
                double mx = 0, my = 0, mz = 0;
                for (var dx = -1; dx <= 1; dx++)
                    for (var dy = -1; dy <= 1; dy++)
                        for (var dz = -1; dz <= 1; dz++)
                        {
                            List<int> list;
                            var key = ((cx + dx) & 0x1FFFFF) << 42 | ((cy + dy) & 0x1FFFFF) << 21 | ((cz + dz) & 0x1FFFFF);
                            if (!cells.TryGetValue(key, out list)) continue;
                            foreach (var j in list)
                            {
                                double ex = pos[3 * j] - px, ey = pos[3 * j + 1] - py, ez = pos[3 * j + 2] - pz;
                                if (ex * ex + ey * ey + ez * ez > r2) continue;
                                // the other side of a thin wall looks the other way
                                if (unit[3 * j] * unit[3 * i] + unit[3 * j + 1] * unit[3 * i + 1] + unit[3 * j + 2] * unit[3 * i + 2] < -0.3) continue;
                                near.Add(j);
                                mx += unit[3 * j]; my += unit[3 * j + 1]; mz += unit[3 * j + 2];
                            }
                        }
                var ml = Math.Sqrt(mx * mx + my * my + mz * mz);
                if (near.Count < 4 || ml <= 0) continue;
                mx /= ml; my /= ml; mz /= ml;
                double rise = 0;
                var count = 0;
                foreach (var j in near)
                {
                    if (j == i) continue;
                    double ex = pos[3 * j] - px, ey = pos[3 * j + 1] - py, ez = pos[3 * j + 2] - pz;
                    var len = Math.Sqrt(ex * ex + ey * ey + ez * ez);
                    if (len <= 0) continue;
                    rise += (ex * mx + ey * my + ez * mz) / len;
                    count++;
                }
                if (count == 0) continue;
                value[i] = rise / count;
                measured[i] = true;
            }
            // the noise of the surface: the median of the rise over every vertex, most of which
            // are on smooth faces; a crease must stand well above it
            var all = new List<double>(m);
            for (var i = 0; i < m; i++) if (measured[i]) all.Add(Math.Abs(value[i]));
            all.Sort();
            var noise = all.Count > 0 ? all[all.Count / 2] : 0;
            var limit = Math.Max(CreaseRise, NoiseFactor * noise);
            var concave = new bool[m];
            for (var i = 0; i < m; i++) concave[i] = measured[i] && !onFloor[i] && value[i] > limit;
            var result = new bool[n];
            for (var k = 0; k < n; k++)
                for (var e = 0; e < 3; e++)
                    if (concave[local[corner[3 * k + e]]]) result[k] = true;
            return result;
        }

        /// <summary>
        /// The faces of the floor: the plane that most of the area facing up lies on, in the lower
        /// part of the surface, fitted to them (a pad may slope a little); none when that area is
        /// small.
        /// </summary>
        static bool[] Floor(double[] normal, double[] centre, double[] area)
        {
            var n = area.Length;
            var floor = new bool[n];
            double zmin = double.MaxValue, zmax = double.MinValue, total = 0;
            for (var k = 0; k < n; k++)
            {
                zmin = Math.Min(zmin, centre[3 * k + 2]);
                zmax = Math.Max(zmax, centre[3 * k + 2]);
                total += area[k];
            }
            if (n == 0 || total <= 0) return floor;
            const double bin = 0.1;
            var bins = new Dictionary<int, double>();
            for (var k = 0; k < n; k++)
            {
                if (normal[3 * k + 2] < 0.9) continue;
                var b = (int)Math.Floor((centre[3 * k + 2] - zmin) / bin);
                double s; bins.TryGetValue(b, out s); bins[b] = s + area[k];
            }
            if (bins.Count == 0) return floor;
            // the fullest band of the lower third (with its two neighbours, for a pad that slopes)
            var lower = Math.Max(1.0, (zmax - zmin) / 3);
            var top = bins.Keys.Where(b => b * bin <= lower)
                .Select(b => new { b, s = Band(bins, b) })
                .OrderByDescending(x => x.s).FirstOrDefault();
            if (top == null || top.s < 0.1 * total) return floor;
            var z0 = zmin + (top.b + 0.5) * bin;

            // the plane z = a x + b y + c by least squares, twice, on the faces near it
            double pa = 0, pb = 0, pc = z0, band = 0.3;
            for (var it = 0; it < 3; it++)
            {
                double sw = 0, sx = 0, sy = 0, sz = 0, sxx = 0, sxy = 0, syy = 0, sxz = 0, syz = 0;
                for (var k = 0; k < n; k++)
                {
                    if (normal[3 * k + 2] < 0.9) continue;
                    double x = centre[3 * k], y = centre[3 * k + 1], z = centre[3 * k + 2];
                    if (Math.Abs(z - (pa * x + pb * y + pc)) > band) continue;
                    var w = area[k];
                    sw += w; sx += w * x; sy += w * y; sz += w * z;
                    sxx += w * x * x; sxy += w * x * y; syy += w * y * y; sxz += w * x * z; syz += w * y * z;
                }
                if (sw <= 0) break;
                double mx = sx / sw, my = sy / sw, mz = sz / sw;
                double cxx = sxx / sw - mx * mx, cxy = sxy / sw - mx * my, cyy = syy / sw - my * my;
                double cxz = sxz / sw - mx * mz, cyz = syz / sw - my * mz;
                var det = cxx * cyy - cxy * cxy;
                if (Math.Abs(det) > 1e-12)
                {
                    pa = (cxz * cyy - cyz * cxy) / det;
                    pb = (cyz * cxx - cxz * cxy) / det;
                }
                else { pa = 0; pb = 0; }
                // a pad slopes a few percent at most; more than that is not a floor
                if (Math.Abs(pa) > 0.1 || Math.Abs(pb) > 0.1) { pa = 0; pb = 0; }
                pc = mz - pa * mx - pb * my;
                band = 0.15;
            }
            for (var k = 0; k < n; k++)
            {
                if (normal[3 * k + 2] < 0.9) continue;
                double x = centre[3 * k], y = centre[3 * k + 1], z = centre[3 * k + 2];
                floor[k] = Math.Abs(z - (pa * x + pb * y + pc)) <= 0.15;
            }
            return floor;
        }

        static double Band(Dictionary<int, double> bins, int b)
        {
            double s = 0, x;
            for (var d = -1; d <= 1; d++) if (bins.TryGetValue(b + d, out x)) s += x;
            return s;
        }

        /// <summary>
        /// Cuts islands by a plane: the faces whose centre lies in front of the plane leave the
        /// island and make a new one, at the end of the list, so that the numbers of the islands
        /// before stay. An island with every face on one side stays whole. Returns the new islands.
        /// </summary>
        public static List<int> Cut(ImportedModel model, List<Island> islands, SurfaceCut cut)
        {
            var made = new List<int>();
            if (cut?.Islands == null || cut.Point == null || cut.Normal == null || cut.Point.Length != 3 || cut.Normal.Length != 3) return made;
            var nl = Math.Sqrt(cut.Normal.Sum(x => x * x));
            if (nl <= 0) return made;
            var nrm = cut.Normal.Select(x => x / nl).ToArray();
            foreach (var i in cut.Islands.Distinct().OrderBy(x => x).ToList())
            {
                if (i < 0 || i >= islands.Count) continue;
                var island = islands[i];
                var part = model.Parts[island.Part];
                var v = part.Positions;
                var back = new List<int>();
                var front = new List<int>();
                foreach (var t in island.Triangles)
                {
                    double s = 0;
                    for (var d = 0; d < 3; d++)
                    {
                        var c = (v[part.Indices[3 * t] * 3 + d] + v[part.Indices[3 * t + 1] * 3 + d] + v[part.Indices[3 * t + 2] * 3 + d]) / 3.0;
                        s += (c - cut.Point[d]) * nrm[d];
                    }
                    (s > 0 ? front : back).Add(t);
                }
                if (front.Count == 0 || back.Count == 0) continue;
                island.Triangles = back.ToArray();
                ModelGeometry.Measure(part, island);
                var piece = new Island { Part = island.Part, Triangles = front.ToArray(), Index = islands.Count, Region = island.Region };
                ModelGeometry.Measure(part, piece);
                islands.Add(piece);
                made.Add(piece.Index);
            }
            return made;
        }

        /// <summary>
        /// The regions of a surface that are runs of pipe, which the shapes of the vertices do not
        /// tell when a run turns (an L, a U): their class becomes Pipe, with the radius and the
        /// length of the run. A vessel, a sphere and the ground stay what they are.
        /// </summary>
        public static void FindTubes(ImportedModel model, List<Island> islands)
        {
            foreach (var island in islands)
            {
                if (!island.Region || island.Shape == null) continue;
                var c = island.Shape.Class;
                if (c == ShapeClass.Column || c == ShapeClass.VerticalVessel || c == ShapeClass.HorizontalVessel || c == ShapeClass.Sphere || c == ShapeClass.Ground || c == ShapeClass.Pipe) continue;
                double radius, length;
                if (!Tube(model.Parts[island.Part], island, out radius, out length)) continue;
                island.Shape.Class = ShapeClass.Pipe;
                island.Shape.Radius = radius;
                island.Shape.Length = length;
            }
        }

        /// <summary>
        /// The regions of a surface that are vessels with something on them (the saddles a noisy
        /// scan leaves on a drum, a platform): most of their vertices lie at one distance from an
        /// axis, all round it, and the rest stand out. The shape of all the vertices calls them
        /// boxes; the shell alone says what they are. A region of a CAD file is never looked at.
        /// </summary>
        public static void FindVessels(ImportedModel model, List<Island> islands)
        {
            foreach (var island in islands)
            {
                if (!island.Region || island.Shape == null) continue;
                if (island.Shape.Class != ShapeClass.Box && island.Shape.Class != ShapeClass.Unknown) continue;
                var size = island.Size;
                if (Math.Max(size[0], Math.Max(size[1], size[2])) < 1.0) continue;
                // the middles of the faces, each as much as its area: the way the mesh is cut up
                // (vertices split at sharp edges, a finer tessellation at the corners) does not count
                var part = model.Parts[island.Part];
                var pts = new List<double[]>(island.Triangles.Length);
                var weights = new List<double>(island.Triangles.Length);
                foreach (var t in island.Triangles)
                {
                    var a = ModelGeometry.TriangleArea(part, t);
                    if (a <= 0) continue;
                    var c = new double[3];
                    for (var k = 0; k < 3; k++)
                    {
                        var v = part.Indices[3 * t + k] * 3;
                        for (var d = 0; d < 3; d++) c[d] += part.Positions[v + d] / 3.0;
                    }
                    pts.Add(c);
                    weights.Add(a);
                }
                if (pts.Count < 32) continue;
                double[] mean, e1, e2, e3;
                IslandShapes.Principal(pts, out mean, out e1, out e2, out e3);
                double[] best = null, bestAxis = null;
                foreach (var axis in new[] { e1, e2, e3 })
                {
                    var fit = Shell(pts, weights, mean, axis);
                    if (best == null || fit[1] > best[1]) { best = fit; bestAxis = axis; }
                }
                // [radius, share of the vertices on the shell, sectors round the axis, length]
                if (best[1] < VesselShare || best[2] < 7 || best[0] <= 0.35) continue;
                var vertical = Math.Abs(bestAxis[2]) > 0.85;
                var horizontal = Math.Abs(bestAxis[2]) < 0.3;
                if (!vertical && !horizontal) continue;
                island.Shape.Axis = bestAxis;
                island.Shape.Radius = best[0];
                island.Shape.Length = best[3];
                island.Shape.Class = vertical ? (best[3] > 8 * best[0] ? ShapeClass.Column : ShapeClass.VerticalVessel) : ShapeClass.HorizontalVessel;
            }
        }

        /// <summary>The share of a region's area that must lie on the shell of a vessel.</summary>
        public static double VesselShare = 0.6;

        /// <summary>
        /// The shell of the faces about an axis: the median distance of their middles from it (by
        /// area), the share of the area within 12 % of that distance, how many of eight sectors round
        /// the axis it fills, and its length along it. The axis goes through the middle of the shell
        /// (a second pass moves it there from the middle of all the faces).
        /// </summary>
        static double[] Shell(List<double[]> pts, List<double> weights, double[] mean, double[] axis)
        {
            var centre = (double[])mean.Clone();
            var h = Math.Abs(axis[2]) < 0.9 ? new[] { 0.0, 0, 1 } : new[] { 1.0, 0, 0 };
            var u = new[] { axis[1] * h[2] - axis[2] * h[1], axis[2] * h[0] - axis[0] * h[2], axis[0] * h[1] - axis[1] * h[0] };
            var ul = Math.Sqrt(u[0] * u[0] + u[1] * u[1] + u[2] * u[2]);
            for (var k = 0; k < 3; k++) u[k] /= ul;
            var w = new[] { axis[1] * u[2] - axis[2] * u[1], axis[2] * u[0] - axis[0] * u[2], axis[0] * u[1] - axis[1] * u[0] };
            double radius = 0, share = 0, length = 0;
            var sectors = 0;
            var d = new double[pts.Count];
            for (var pass = 0; pass < 2; pass++)
            {
                for (var i = 0; i < pts.Count; i++)
                {
                    double vx = pts[i][0] - centre[0], vy = pts[i][1] - centre[1], vz = pts[i][2] - centre[2];
                    var along = vx * axis[0] + vy * axis[1] + vz * axis[2];
                    double rx = vx - along * axis[0], ry = vy - along * axis[1], rz = vz - along * axis[2];
                    d[i] = Math.Sqrt(rx * rx + ry * ry + rz * rz);
                }
                var order = Enumerable.Range(0, pts.Count).OrderBy(i => d[i]).ToList();
                var total = weights.Sum();
                double acc = 0;
                radius = 0;
                foreach (var i in order)
                {
                    acc += weights[i];
                    if (acc >= total / 2) { radius = d[i]; break; }
                }
                if (radius <= 0 || total <= 0) return new double[] { 0, 0, 0, 0 };
                double sx = 0, sy = 0, sz = 0, lo = double.MaxValue, hi = double.MinValue, on = 0;
                var n = 0;
                var filled = new bool[8];
                for (var i = 0; i < pts.Count; i++)
                {
                    if (Math.Abs(d[i] - radius) > 0.12 * radius) continue;
                    double vx = pts[i][0] - centre[0], vy = pts[i][1] - centre[1], vz = pts[i][2] - centre[2];
                    var along = vx * axis[0] + vy * axis[1] + vz * axis[2];
                    double rx = vx - along * axis[0], ry = vy - along * axis[1], rz = vz - along * axis[2];
                    sx += rx; sy += ry; sz += rz;
                    on += weights[i];
                    lo = Math.Min(lo, along); hi = Math.Max(hi, along);
                    var angle = Math.Atan2(rx * w[0] + ry * w[1] + rz * w[2], rx * u[0] + ry * u[1] + rz * u[2]) + Math.PI;
                    filled[Math.Min(7, (int)(angle / (Math.PI / 4)))] = true;
                    n++;
                }
                share = on / total;
                sectors = filled.Count(x => x);
                length = n > 0 ? hi - lo : 0;
                if (n == 0) break;
                // the middle of the shell: the mean of the shell's offsets from the axis
                centre[0] += sx / n; centre[1] += sy / n; centre[2] += sz / n;
            }
            return new[] { radius, share, sectors, length };
        }

        /// <summary>The thickest wall a tube may have (twice the radius of a large pipe), metres.</summary>
        public static double MaxTube = 0.8;

        /// <summary>
        /// Whether a surface is a tube: from each face, a ray into the surface meets the far wall,
        /// a face that looks back, at about one distance (the diameter) over most of the area; the
        /// normals turn round the axis (they do not point only two ways, as on a plate); and the
        /// run is several diameters long. The length is the area over the circumference.
        /// </summary>
        public static bool Tube(MeshPart part, Island island, out double radius, out double length)
        {
            radius = 0; length = 0;
            var tris = island.Triangles;
            var n = tris.Length;
            if (n < 16) return false;
            var v = part.Positions;
            var c = new double[n * 3];
            var nr = new double[n * 3];
            var w = new double[n];
            double total = 0;
            for (var k = 0; k < n; k++)
            {
                var t = tris[k];
                int a = part.Indices[3 * t] * 3, b = part.Indices[3 * t + 1] * 3, cc = part.Indices[3 * t + 2] * 3;
                double ux = v[b] - v[a], uy = v[b + 1] - v[a + 1], uz = v[b + 2] - v[a + 2];
                double wx = v[cc] - v[a], wy = v[cc + 1] - v[a + 1], wz = v[cc + 2] - v[a + 2];
                double nx = uy * wz - uz * wy, ny = uz * wx - ux * wz, nz = ux * wy - uy * wx;
                var len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (len <= 0) continue;
                w[k] = len / 2;
                total += w[k];
                nr[3 * k] = nx / len; nr[3 * k + 1] = ny / len; nr[3 * k + 2] = nz / len;
                for (var d = 0; d < 3; d++) c[3 * k + d] = (v[a + d] + v[b + d] + v[cc + d]) / 3.0;
            }
            if (total <= 0) return false;

            // the normals of a plate point two ways only, and a tube's turn round its axis
            var cov = new double[3, 3];
            for (var k = 0; k < n; k++)
                for (var i = 0; i < 3; i++)
                    for (var j = 0; j < 3; j++) cov[i, j] += w[k] * nr[3 * k + i] * nr[3 * k + j] / total;
            if (LargestEigen(cov) > 0.8) return false;

            // the faces in cells, for the rays
            var cell = Math.Max(0.02, 2 * Math.Sqrt(total / n));
            var grid = new Dictionary<long, List<int>>(ModelGeometry.PairKeys.Instance);
            Func<double, double, double, long> key = (x, y, z) =>
            {
                long ix = (long)Math.Floor(x / cell) & 0x1FFFFF, iy = (long)Math.Floor(y / cell) & 0x1FFFFF, iz = (long)Math.Floor(z / cell) & 0x1FFFFF;
                return ix << 42 | iy << 21 | iz;
            };
            for (var k = 0; k < n; k++)
            {
                if (w[k] <= 0) continue;
                var kk = key(c[3 * k], c[3 * k + 1], c[3 * k + 2]);
                List<int> list;
                if (!grid.TryGetValue(kk, out list)) grid[kk] = list = new List<int>(4);
                list.Add(k);
            }

            var stride = Math.Max(1, n / 3000);
            var found = new List<double[]>();
            double tried = 0;
            for (var k = 0; k < n; k += stride)
            {
                if (w[k] <= 0) continue;
                tried += w[k];
                var best = double.MaxValue;
                for (var s = cell / 2; s <= MaxTube + cell && best == double.MaxValue; s += cell / 2)
                {
                    double px = c[3 * k] - s * nr[3 * k], py = c[3 * k + 1] - s * nr[3 * k + 1], pz = c[3 * k + 2] - s * nr[3 * k + 2];
                    List<int> list;
                    if (!grid.TryGetValue(key(px, py, pz), out list)) continue;
                    foreach (var o in list)
                    {
                        if (nr[3 * o] * nr[3 * k] + nr[3 * o + 1] * nr[3 * k + 1] + nr[3 * o + 2] * nr[3 * k + 2] > -0.5) continue;
                        double dx = c[3 * o] - c[3 * k], dy = c[3 * o + 1] - c[3 * k + 1], dz = c[3 * o + 2] - c[3 * k + 2];
                        var along = -(dx * nr[3 * k] + dy * nr[3 * k + 1] + dz * nr[3 * k + 2]);
                        if (along <= 0) continue;
                        var off2 = dx * dx + dy * dy + dz * dz - along * along;
                        if (off2 < cell * cell && along < best) best = along;
                    }
                }
                if (best < double.MaxValue) found.Add(new[] { best, w[k] });
            }
            var hit = found.Sum(x => x[1]);
            if (tried <= 0 || hit < 0.7 * tried) return false;
            found.Sort((x, y) => x[0].CompareTo(y[0]));
            double acc = 0, median = 0;
            foreach (var x in found) { acc += x[1]; if (acc >= hit / 2) { median = x[0]; break; } }
            if (median <= 0 || median > MaxTube) return false;
            var near = found.Where(x => Math.Abs(x[0] - median) <= 0.2 * median).Sum(x => x[1]);
            if (near < 0.75 * tried) return false;
            var extent = Math.Max(island.Max[0] - island.Min[0], Math.Max(island.Max[1] - island.Min[1], island.Max[2] - island.Min[2]));
            if (extent < 4 * median) return false;
            radius = median / 2;
            length = total / (Math.PI * median);
            return true;
        }

        /// <summary>The largest eigenvalue of a symmetric 3 by 3 matrix, by the power method.</summary>
        static double LargestEigen(double[,] m)
        {
            double x = 1, y = 0.7, z = 0.4, l = 0;
            for (var it = 0; it < 60; it++)
            {
                double nx = m[0, 0] * x + m[0, 1] * y + m[0, 2] * z;
                double ny = m[1, 0] * x + m[1, 1] * y + m[1, 2] * z;
                double nz = m[2, 0] * x + m[2, 1] * y + m[2, 2] * z;
                l = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (l <= 0) return 0;
                x = nx / l; y = ny / l; z = nz / l;
            }
            return l;
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
