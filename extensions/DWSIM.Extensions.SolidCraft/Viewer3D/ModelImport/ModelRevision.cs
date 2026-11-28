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
    /// <summary>What an update to a new revision of the model file kept and what it lost.</summary>
    public class RevisionReport
    {
        /// <summary>The flowsheet objects whose group was found again, and those whose group was not.</summary>
        public int Kept, Lost;
        /// <summary>The groups the user made that kept islands, and those that lost them all.</summary>
        public int UserKept, UserLost;
        /// <summary>The cuts made again, and those dropped (no islands where they were).</summary>
        public int CutsMade, CutsDropped;

        public string Text(string file)
        {
            var parts = new List<string>();
            if (Kept + Lost > 0) parts.Add(Kept + " of " + (Kept + Lost) + (Kept + Lost == 1 ? " object kept its group" : " objects kept their groups"));
            if (UserKept + UserLost > 0) parts.Add(UserKept + " of " + (UserKept + UserLost) + " groups made by hand kept");
            if (CutsMade + CutsDropped > 0) parts.Add(CutsMade + " of " + (CutsMade + CutsDropped) + (CutsMade + CutsDropped == 1 ? " cut made again" : " cuts made again"));
            var text = "The plant model " + file + " changed and was read again";
            text += parts.Count > 0 ? ": " + string.Join(", ", parts) + "." : ".";
            if (Lost + UserLost + CutsDropped > 0) text += " Check the rest in the review.";
            return text;
        }
    }

    /// <summary>
    /// Carries a flowsheet's plant model over to a new revision of its file (the designers saved
    /// the model again): the model is read again where it stood, the cuts are made again where they
    /// were, the islands of the old revision are found in the new one by their place and size, and
    /// then the groups the user made keep their islands and the flowsheet objects keep the groups
    /// that hold most of their old islands. What is not found again is left for the review.
    /// </summary>
    public static class ModelRevision
    {
        /// <summary>
        /// Reads the new revision and returns its import; <paramref name="stored"/> gets the new
        /// reference and the groups carried over (before the matching, which the caller runs).
        /// </summary>
        public static ModelImportResult Update(string file, PlantModelImport stored, string flowsheetPath, out RevisionReport report)
        {
            report = new RevisionReport();
            var oldRef = stored.Model;
            var oldIndex = ModelImporter.Cached(oldRef);

            // the new file where the old one stood: its unit, axis, turn, place and tolerance
            var keep = JsonConvert.DeserializeObject<ModelReference>(JsonConvert.SerializeObject(oldRef));
            keep.Cuts = null;
            var r = ModelImporter.Import(file, keep, flowsheetPath);
            var newRef = r.Reference;
            newRef.Place = keep.Place;
            newRef.WeldTolerance = keep.WeldTolerance;

            if (oldRef.Cuts != null && oldRef.Cuts.Count > 0)
            {
                var model = r.Model ?? ModelImporter.ReadPlaced(file, newRef);
                int surfaces;
                var islands = ModelImporter.IslandsOf(model, newRef, false, out surfaces);
                var cuts = new List<SurfaceCut>();
                foreach (var c in oldRef.Cuts)
                {
                    var again = Again(c, islands);
                    if (again == null) { report.CutsDropped++; continue; }
                    if (ModelSurface.Cut(model, islands, again).Count == 0) { report.CutsDropped++; continue; }
                    cuts.Add(again);
                    report.CutsMade++;
                }
                if (cuts.Count > 0)
                {
                    newRef.Cuts = cuts;
                    r = ModelImporter.Import(file, newRef, flowsheetPath);
                }
            }
            var newIndex = ModelImporter.Cached(r.Reference) ?? r;

            var map = oldIndex == null ? null : Map(oldIndex.Islands, newIndex.Islands);
            var segments = ModelSegmentation.Groups(newIndex);
            var carried = new List<ModelGroup>();
            var taken = new HashSet<string>();
            foreach (var g in stored.Groups)
            {
                if (!g.User && g.Object == null) continue;
                var islands = Mapped(g, oldIndex, map, newIndex.Islands);
                if (g.User)
                {
                    if (islands.Count == 0) { report.UserLost++; continue; }
                    g.Islands = islands;
                    carried.Add(g);
                    report.UserKept++;
                    continue;
                }
                var objects = g.Objects().Count();
                var set = new HashSet<int>(islands);
                var best = segments.Where(s => !taken.Contains(s.Id))
                    .Select(s => new { s, n = s.Islands.Count(set.Contains) })
                    .OrderByDescending(x => x.n).FirstOrDefault();
                if (set.Count == 0 || best == null || best.n < 0.5 * set.Count)
                {
                    report.Lost += objects;
                    continue;
                }
                taken.Add(best.s.Id);
                g.Id = best.s.Id;
                g.Name = best.s.Name;
                g.Islands = best.s.Islands.ToList();
                carried.Add(g);
                report.Kept += objects;
            }
            stored.Model = r.Reference;
            stored.Groups = carried;
            return r;
        }

        /// <summary>
        /// The cut again on the new islands: those whose middle lies in the box of the islands it
        /// cut before (a little larger) and that the plane crosses; null when there are none, or
        /// when the cut does not have its box (a cut of an older version).
        /// </summary>
        static SurfaceCut Again(SurfaceCut c, List<Island> islands)
        {
            if (c.Min == null || c.Max == null || c.Point == null || c.Normal == null) return null;
            var grow = new double[3];
            for (var d = 0; d < 3; d++) grow[d] = 0.05 + 0.05 * (c.Max[d] - c.Min[d]);
            var pick = new List<int>();
            foreach (var i in islands)
            {
                var inside = true;
                for (var d = 0; d < 3 && inside; d++)
                    inside = i.Centroid[d] >= c.Min[d] - grow[d] && i.Centroid[d] <= c.Max[d] + grow[d];
                if (inside && Crosses(i, c.Point, c.Normal)) pick.Add(i.Index);
            }
            if (pick.Count == 0) return null;
            return new SurfaceCut { Islands = pick, Point = c.Point, Normal = c.Normal, Min = c.Min, Max = c.Max };
        }

        /// <summary>Whether the plane goes through the island's box.</summary>
        static bool Crosses(Island i, double[] p, double[] n)
        {
            double lo = double.MaxValue, hi = double.MinValue;
            for (var k = 0; k < 8; k++)
            {
                double s = 0;
                for (var d = 0; d < 3; d++) s += ((((k >> d) & 1) == 0 ? i.Min[d] : i.Max[d]) - p[d]) * n[d];
                lo = Math.Min(lo, s); hi = Math.Max(hi, s);
            }
            return lo < 0 && hi > 0;
        }

        /// <summary>
        /// The island of the new revision each island of the old one became (or -1): the nearest
        /// by its middle, within a quarter of its size (2 m at most), of about the same area and
        /// size.
        /// </summary>
        public static int[] Map(List<Island> before, List<Island> after)
        {
            var cell = 1.0;
            var grid = new Dictionary<long, List<int>>(ModelGeometry.PairKeys.Instance);
            Func<double, long> c = x => (long)Math.Floor(x / cell) & 0x1FFFFF;
            foreach (var a in after)
            {
                var key = c(a.Centroid[0]) << 42 | c(a.Centroid[1]) << 21 | c(a.Centroid[2]);
                List<int> list;
                if (!grid.TryGetValue(key, out list)) grid[key] = list = new List<int>(2);
                list.Add(a.Index);
            }
            var map = new int[before.Count];
            for (var k = 0; k < before.Count; k++)
            {
                var o = before[k];
                map[k] = -1;
                var size = Size(o);
                var reach = Math.Min(2.0, Math.Max(0.05, 0.25 * size));
                var steps = (int)Math.Ceiling(reach / cell);
                long cx = c(o.Centroid[0]), cy = c(o.Centroid[1]), cz = c(o.Centroid[2]);
                var best = reach * reach;
                for (var dx = -steps; dx <= steps; dx++)
                    for (var dy = -steps; dy <= steps; dy++)
                        for (var dz = -steps; dz <= steps; dz++)
                        {
                            List<int> list;
                            if (!grid.TryGetValue(((cx + dx) & 0x1FFFFF) << 42 | ((cy + dy) & 0x1FFFFF) << 21 | ((cz + dz) & 0x1FFFFF), out list)) continue;
                            foreach (var j in list)
                            {
                                var n = after[j];
                                if (!Like(o.Area, n.Area) || !Like(size, Size(n))) continue;
                                double ex = n.Centroid[0] - o.Centroid[0], ey = n.Centroid[1] - o.Centroid[1], ez = n.Centroid[2] - o.Centroid[2];
                                var d2 = ex * ex + ey * ey + ez * ez;
                                if (d2 <= best) { best = d2; map[k] = j; }
                            }
                        }
            }
            return map;
        }

        static double Size(Island i) => Math.Max(i.Max[0] - i.Min[0], Math.Max(i.Max[1] - i.Min[1], i.Max[2] - i.Min[2]));

        static bool Like(double a, double b) => a <= 0 && b <= 0 || a > 0 && b > 0 && a / b < 2 && b / a < 2;

        /// <summary>
        /// The islands of the new revision a group of the old one had: its islands through the map,
        /// or, when the old revision's cache is gone, the islands whose middle lies in its box.
        /// </summary>
        static List<int> Mapped(ModelGroup g, ModelImportResult oldIndex, int[] map, List<Island> islands)
        {
            var result = new List<int>();
            if (map != null && g.Islands != null)
            {
                foreach (var i in g.Islands)
                    if (i >= 0 && i < map.Length && map[i] >= 0) result.Add(map[i]);
                return result.Distinct().ToList();
            }
            if (g.Min == null || g.Max == null) return result;
            foreach (var i in islands)
            {
                var inside = true;
                for (var d = 0; d < 3 && inside; d++)
                {
                    var grow = 0.05 + 0.02 * (g.Max[d] - g.Min[d]);
                    inside = i.Centroid[d] >= g.Min[d] - grow && i.Centroid[d] <= g.Max[d] + grow;
                }
                if (inside) result.Add(i.Index);
            }
            return result;
        }
    }
}
