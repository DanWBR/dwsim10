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
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DWSIM.Extensions.SolidCraft.Viewer3D.ModelImport
{
    /// <summary>A group of islands of a plant model that stands for one thing, with what the segmentation found of it.</summary>
    public class SegmentGroup
    {
        public string Id;
        public List<int> Islands = new List<int>();
        /// <summary>equipment, piping, structure or scenery.</summary>
        public string Role;
        /// <summary>The name the file gives the group (a node of its tree), or one made up.</summary>
        public string Name;
        /// <summary>The equipment tag read from the name (V-100, P-250A), or null.</summary>
        public string Tag;
        /// <summary>column, vessel, exchanger, aircooler, pump, compressor, furnace, tank, reactor or other.</summary>
        public string Category;
        /// <summary>The class of the largest island.</summary>
        public ShapeClass Body;
        public double[] Min, Max;
        /// <summary>The main body: height (vertical) or length (horizontal), diameter, metres; and whether it stands up.</summary>
        public double Length, Diameter;
        public bool Vertical;
        /// <summary>The other equipment groups a piping group of the model joins this one to.</summary>
        public List<string> Neighbours = new List<string>();
        /// <summary>A group the user made (joined, split or given a role); the segmentation does not change it.</summary>
        public bool User;

        public double[] Center => new[] { (Min[0] + Max[0]) / 2, (Min[1] + Max[1]) / 2, (Min[2] + Max[2]) / 2 };
    }

    /// <summary>
    /// Splits a plant model into groups: equipment, piping, structure and scenery. When the file
    /// keeps a tree of objects (a DWF from a plant design program, a glTF or FBX with named nodes),
    /// the groups come from the tree: the words of the path say what a branch holds (EQUIPAMENTOS,
    /// TUBULACAO, ESTRUTURAS, CIVIL; or EQUIPMENT, PIPING, STRUCTURE), and the node that carries a
    /// tag (V-100) is the equipment. When the model is a single mesh, the groups come from the
    /// shapes of its islands: each round body of vessel size takes the small pieces that touch it
    /// (nozzles, heads, skirts, platforms), never through a pipe; pipes and fittings that touch
    /// make runs; the rest is structure. A piping group that touches two equipment groups joins
    /// them, as a line of the plant does.
    /// </summary>
    public static class ModelSegmentation
    {
        static readonly Regex TagPattern = new Regex(@"(?<![A-Z0-9])([A-Z]{1,4})-?(\d{2,6}[A-Z]?)(?![0-9])", RegexOptions.Compiled);
        static readonly Regex LinePattern = new Regex("(\"|\\d+-[A-Z]{1,4}-)", RegexOptions.Compiled);

        static readonly string[] Scenery = { "CIVIL", "PISO", "FLOOR", "DORMENT", "FOUNDATION", "FUNDAC", "PAVIMENT", "ARRUAMENTO", "TERRENO", "GROUND", "CERCA", "FENCE", "ROAD" };
        static readonly string[] Structure = { "ESTRUTURA", "STRUCTUR", "RACK", "PLATAFORM", "PLATFORM", "ESCADA", "LADDER", "STAIR", "FRMWORK", "FRAMEWORK", "SCTN", "COBERTURA", "ROOF", "CASA", "BUILDING", "PREDIO", "SHELTER", "GALPAO" };
        static readonly string[] Equipment = { "EQUIPAMENT", "EQUIPMENT", "VASO", "VESSEL", "BOMBA", "PUMP", "COMPRESSOR", "FORNO", "FURNACE", "AIR_COOLER", "AIR COOLER", "AIRCOOLER", "PERMUTADOR", "EXCHANGER", "TANQUE", "TANK", "TORRE", "COLUMN", "REATOR", "REACTOR", "ESFERA", "SPHERE", "FILTRO", "FILTER", "TURBINA", "TURBINE" };
        static readonly string[] Piping = { "TUBULAC", "PIPING", "PIPELINE", "LINHA", "LINE", "BRANCH", "TRIM" };

        public static List<SegmentGroup> Groups(ModelImportResult index)
        {
            var paths = index.PartPaths ?? new List<string[]>();
            var structured = paths.Count > 0 && paths.Count(p => p != null && p.Length >= 2) > paths.Count / 2;
            var groups = structured ? FromTree(index) : FromShapes(index);
            foreach (var g in groups) Measure(g, index.Islands);
            Link(groups, index.Islands);
            return groups;
        }

        static string Upper(string s)
        {
            var d = (s ?? "").Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(d.Length);
            foreach (var ch in d)
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToUpperInvariant(ch));
            return sb.ToString();
        }

        static bool Has(string upper, string[] words) => words.Any(w => upper.Contains(w));

        /// <summary>The role a node's name says, or null.</summary>
        static string RoleOf(string name)
        {
            var u = Upper(name);
            if (Has(u, Scenery)) return "scenery";
            if (Has(u, Structure)) return "structure";
            if (Has(u, Equipment)) return "equipment";
            if (Has(u, Piping)) return "piping";
            return null;
        }

        /// <summary>The tag in a name (V-100 in "/V-100", V-404 in "/SKID-V-404"), or null.</summary>
        public static string TagOf(string name)
        {
            var m = TagPattern.Match(Upper(name));
            return m.Success ? m.Groups[1].Value + "-" + m.Groups[2].Value : null;
        }

        static List<SegmentGroup> FromTree(ModelImportResult index)
        {
            var byKey = new Dictionary<string, SegmentGroup>();
            var order = new List<SegmentGroup>();
            var partKey = new string[index.Parts];
            var partGroup = new SegmentGroup[index.Parts];
            for (var p = 0; p < index.Parts; p++)
            {
                var path = p < index.PartPaths.Count && index.PartPaths[p] != null ? index.PartPaths[p] : new[] { index.PartNames[p] ?? "part" + p };
                var hint = index.PartRoles != null && p < index.PartRoles.Count ? index.PartRoles[p] : null;
                string role = null;
                var roleAt = -1;
                var at = -1;
                string tag = null;
                if (hint != null)
                {
                    // the file says what the part is (an IFC class): the group is the element at the top of its branch
                    role = hint;
                    at = Math.Min(1, path.Length - 1);
                }
                for (var k = 0; k < path.Length && role == null; k++)
                {
                    role = RoleOf(path[k]);
                    if (role != null) roleAt = k;
                }
                if (hint == null && role == "equipment")
                    for (var k = roleAt; k < path.Length && at < 0; k++)
                    {
                        tag = TagOf(path[k]);
                        if (tag != null && k > roleAt) at = k;
                        else tag = null;
                    }
                if (role == "piping")
                    for (var k = roleAt + 1; k < path.Length && at < 0; k++)
                        if (LinePattern.IsMatch(path[k])) at = k;
                if (at < 0) at = Math.Min(path.Length - 1, roleAt < 0 ? Math.Min(1, path.Length - 1) : roleAt + 2);
                if (tag == null && role == "equipment") tag = TagOf(path[at]);
                var key = string.Join("\u001f", path.Take(at + 1));
                SegmentGroup g;
                if (!byKey.TryGetValue(key, out g))
                {
                    g = new SegmentGroup { Id = "g" + order.Count, Role = role, Name = path[at], Tag = tag };
                    byKey[key] = g;
                    order.Add(g);
                }
                partGroup[p] = g;
            }
            foreach (var island in index.Islands)
                if (island.Part >= 0 && island.Part < partGroup.Length) partGroup[island.Part].Islands.Add(island.Index);
            var groups = order.Where(g => g.Islands.Count > 0).ToList();
            // a branch without a word that says what it is takes the role its shapes say
            foreach (var g in groups.Where(x => x.Role == null)) g.Role = RoleFromShapes(g, index.Islands);
            return groups;
        }

        static string RoleFromShapes(SegmentGroup g, List<Island> islands)
        {
            double vessel = 0, pipe = 0, structure = 0, ground = 0;
            foreach (var i in g.Islands)
            {
                var s = islands[i].Shape;
                var a = islands[i].Area;
                if (s == null) continue;
                switch (s.Class)
                {
                    case ShapeClass.Column:
                    case ShapeClass.VerticalVessel:
                    case ShapeClass.HorizontalVessel:
                    case ShapeClass.Sphere: vessel += a; break;
                    case ShapeClass.Pipe:
                    case ShapeClass.Fitting: pipe += a; break;
                    case ShapeClass.Ground: ground += a; break;
                    default: structure += a; break;
                }
            }
            var top = Math.Max(Math.Max(vessel, pipe), Math.Max(structure, ground));
            if (top == 0) return "scenery";
            if (top == vessel) return "equipment";
            if (top == pipe) return "piping";
            if (top == ground) return "scenery";
            return "structure";
        }

        static bool IsBody(IslandShape s) =>
            s != null && (s.Class == ShapeClass.Column || s.Class == ShapeClass.VerticalVessel || s.Class == ShapeClass.HorizontalVessel || s.Class == ShapeClass.Sphere);

        static bool IsPipe(IslandShape s) => s != null && s.Class == ShapeClass.Pipe;

        static List<SegmentGroup> FromShapes(ModelImportResult index)
        {
            var islands = index.Islands;
            var n = islands.Count;
            var owner = new int[n];
            for (var i = 0; i < n; i++) owner[i] = -1;
            var grid = new BoxGrid(islands, 1.0);

            // vessel bodies; the courses of one shell (same axis and radius, end to end) are one body
            var bodies = Enumerable.Range(0, n).Where(i => IsBody(islands[i].Shape)).OrderByDescending(i => islands[i].Area).ToList();
            var seeds = new List<List<int>>();
            foreach (var b in bodies)
            {
                if (owner[b] >= 0) continue;
                var list = new List<int> { b };
                owner[b] = seeds.Count;
                foreach (var o in bodies)
                    if (owner[o] < 0 && SameShell(islands[b], islands[o])) { owner[o] = seeds.Count; list.Add(o); }
                seeds.Add(list);
            }
            // each body takes what touches it, and what touches that, but never a pipe and never far
            for (var s = 0; s < seeds.Count; s++)
            {
                var queue = new Queue<int>(seeds[s]);
                var hops = new Dictionary<int, int>();
                foreach (var x in seeds[s]) hops[x] = 0;
                while (queue.Count > 0)
                {
                    var x = queue.Dequeue();
                    if (hops[x] >= 3) continue;
                    foreach (var y in grid.Touching(x, 0.05))
                    {
                        if (owner[y] >= 0 || IsPipe(islands[y].Shape) || islands[y].Shape?.Class == ShapeClass.Ground) continue;
                        if (Size(islands[y]) > 1.5 * Size(islands[seeds[s][0]])) continue;
                        owner[y] = s;
                        hops[y] = hops[x] + 1;
                        queue.Enqueue(y);
                    }
                }
            }
            var groups = seeds.Select((list, s) => new SegmentGroup
            {
                Id = "g" + s,
                Role = "equipment",
                Name = "Equipment " + (s + 1),
                Islands = Enumerable.Range(0, n).Where(i => owner[i] == s).ToList()
            }).ToList();

            // a model without a tree may still name its pieces by tag (a glTF or FBX with one node per
            // equipment): the body's part gives the group its tag and name
            for (var s = 0; s < groups.Count; s++) NameFromPart(groups[s], seeds[s][0], index);

            // machines: compact boxes of machine size that nothing took
            foreach (var i in Enumerable.Range(0, n).Where(i => owner[i] < 0 && islands[i].Shape?.Class == ShapeClass.Box)
                .Where(i => Size(islands[i]) >= 0.8 && Size(islands[i]) <= 8).OrderByDescending(i => islands[i].Area).ToList())
            {
                if (owner[i] >= 0) continue;
                var g = new SegmentGroup { Id = "g" + groups.Count, Role = "equipment", Name = "Machine " + (groups.Count + 1) };
                owner[i] = groups.Count;
                g.Islands.Add(i);
                foreach (var y in grid.Touching(i, 0.05))
                    if (owner[y] < 0 && !IsPipe(islands[y].Shape) && Size(islands[y]) <= Size(islands[i])) { owner[y] = groups.Count; g.Islands.Add(y); }
                NameFromPart(g, i, index);
                groups.Add(g);
            }

            // runs of pipe: pipes and fittings that touch; then the rest by contact
            Components(islands, grid, owner, groups, i => IsPipe(islands[i].Shape) || islands[i].Shape?.Class == ShapeClass.Fitting, "piping", "Line ");
            Components(islands, grid, owner, groups, i => islands[i].Shape?.Class != ShapeClass.Ground, "structure", "Structure ");
            Components(islands, grid, owner, groups, i => true, "scenery", "Ground ");
            return groups;
        }

        static void NameFromPart(SegmentGroup g, int island, ModelImportResult index)
        {
            var part = index.Islands[island].Part;
            var name = index.PartNames != null && part >= 0 && part < index.PartNames.Count ? index.PartNames[part] : null;
            var tag = name == null ? null : TagOf(name);
            if (tag == null) return;
            g.Tag = tag;
            g.Name = name;
        }

        static void Components(List<Island> islands, BoxGrid grid, int[] owner, List<SegmentGroup> groups, Func<int, bool> take, string role, string name)
        {
            for (var i = 0; i < islands.Count; i++)
            {
                if (owner[i] >= 0 || !take(i)) continue;
                var g = new SegmentGroup { Id = "g" + groups.Count, Role = role, Name = name + (groups.Count(x => x.Role == role) + 1) };
                var queue = new Queue<int>();
                owner[i] = groups.Count;
                queue.Enqueue(i);
                while (queue.Count > 0)
                {
                    var x = queue.Dequeue();
                    g.Islands.Add(x);
                    foreach (var y in grid.Touching(x, 0.02))
                        if (owner[y] < 0 && take(y)) { owner[y] = groups.Count; queue.Enqueue(y); }
                }
                groups.Add(g);
            }
        }

        static double Size(Island i) => Math.Max(i.Max[0] - i.Min[0], Math.Max(i.Max[1] - i.Min[1], i.Max[2] - i.Min[2]));

        static bool SameShell(Island a, Island b)
        {
            var sa = a.Shape; var sb = b.Shape;
            if (sa == null || sb == null || sa.Radius <= 0 || sb.Radius <= 0) return false;
            var dot = Math.Abs(sa.Axis[0] * sb.Axis[0] + sa.Axis[1] * sb.Axis[1] + sa.Axis[2] * sb.Axis[2]);
            if (dot < 0.97 || Math.Abs(sa.Radius - sb.Radius) > 0.15 * Math.Max(sa.Radius, sb.Radius)) return false;
            // the centre of one on the axis of the other, and the two near end to end
            double dx = b.Centroid[0] - a.Centroid[0], dy = b.Centroid[1] - a.Centroid[1], dz = b.Centroid[2] - a.Centroid[2];
            var along = dx * sa.Axis[0] + dy * sa.Axis[1] + dz * sa.Axis[2];
            double px = dx - along * sa.Axis[0], py = dy - along * sa.Axis[1], pz = dz - along * sa.Axis[2];
            var off = Math.Sqrt(px * px + py * py + pz * pz);
            return off < 0.2 * sa.Radius && Math.Abs(along) < (sa.Length + sb.Length) / 2 + 0.3;
        }

        /// <summary>Measures the groups again and joins them again by the pipes, after the user changed some.</summary>
        public static void Remeasure(List<SegmentGroup> groups, List<Island> islands)
        {
            foreach (var g in groups)
            {
                g.Neighbours.Clear();
                Measure(g, islands);
            }
            Link(groups, islands);
        }

        static void Measure(SegmentGroup g, List<Island> islands)
        {
            var min = new[] { double.MaxValue, double.MaxValue, double.MaxValue };
            var max = new[] { double.MinValue, double.MinValue, double.MinValue };
            Island body = null;
            foreach (var i in g.Islands)
            {
                var x = islands[i];
                for (var k = 0; k < 3; k++) { min[k] = Math.Min(min[k], x.Min[k]); max[k] = Math.Max(max[k], x.Max[k]); }
                if (body == null || BodyRank(x) > BodyRank(body) || BodyRank(x) == BodyRank(body) && x.Area > body.Area) body = x;
            }
            g.Min = min; g.Max = max;
            g.Body = body?.Shape?.Class ?? ShapeClass.Unknown;
            if (body?.Shape != null && body.Shape.Radius > 0 && IsBody(body.Shape))
            {
                // the courses of a shell count as one length
                var courses = g.Islands.Select(i => islands[i]).Where(x => x == body || SameShell(body, x)).ToList();
                g.Diameter = 2 * body.Shape.Radius;
                g.Vertical = Math.Abs(body.Shape.Axis[2]) > 0.85;
                g.Length = courses.Sum(x => x.Shape.Length);
            }
            else
            {
                var sx = max[0] - min[0]; var sy = max[1] - min[1]; var sz = max[2] - min[2];
                g.Vertical = sz > Math.Max(sx, sy);
                g.Length = g.Vertical ? sz : Math.Max(sx, sy);
                g.Diameter = g.Vertical ? Math.Max(sx, sy) : Math.Min(sx, sy);
            }
            if (g.Role == "equipment") g.Category = CategoryOf(g);
        }

        static int BodyRank(Island x)
        {
            var c = x.Shape?.Class ?? ShapeClass.Unknown;
            if (IsBody(x.Shape)) return 3;
            if (c == ShapeClass.Box) return 2;
            if (c == ShapeClass.Plate || c == ShapeClass.Beam) return 1;
            return 0;
        }

        /// <summary>What kind of equipment a group is, from its tag's letters and from its body.</summary>
        public static string CategoryOf(SegmentGroup g)
        {
            var letters = g.Tag == null ? "" : g.Tag.Substring(0, g.Tag.IndexOf('-'));
            var tall = g.Body == ShapeClass.Column || g.Vertical && g.Length > 4 * Math.Max(g.Diameter, 0.1) && g.Diameter > 0.5;
            switch (letters)
            {
                case "V": case "D": case "VS": return tall ? "column" : "vessel";
                case "C": case "T": case "TO": case "CT": return tall || g.Body != ShapeClass.HorizontalVessel ? "column" : "vessel";
                case "E": case "EX": case "TC-E": return "exchanger";
                case "AC": case "EA": case "AR": case "A": return "aircooler";
                case "P": case "B": case "BP": return "pump";
                case "K": case "CP": case "KC": case "PK": return "compressor";
                case "H": case "F": case "FO": return "furnace";
                case "TQ": case "TK": case "TA": case "ES": return "tank";
                case "R": case "RE": return "reactor";
            }
            switch (g.Body)
            {
                case ShapeClass.Column: return "column";
                case ShapeClass.VerticalVessel: return "vessel";
                case ShapeClass.HorizontalVessel: return "vessel";
                case ShapeClass.Sphere: return "tank";
                default: return "other";
            }
        }

        /// <summary>The equipment groups each piping group touches become neighbours.</summary>
        static void Link(List<SegmentGroup> groups, List<Island> islands)
        {
            var equipment = groups.Where(g => g.Role == "equipment").ToList();
            if (equipment.Count < 2) return;
            var islandGroup = new Dictionary<int, SegmentGroup>();
            foreach (var g in equipment) foreach (var i in g.Islands) islandGroup[i] = g;
            var grid = new BoxGrid(islands, 1.0, islandGroup.Keys);
            var pairs = new HashSet<string>();
            foreach (var p in groups.Where(g => g.Role == "piping"))
            {
                var touched = new HashSet<SegmentGroup>();
                foreach (var i in p.Islands)
                    foreach (var j in grid.Touching(islands[i], 0.05))
                        touched.Add(islandGroup[j]);
                // a header that touches very many is too vague to say which joins which
                if (touched.Count < 2 || touched.Count > 6) continue;
                var list = touched.ToList();
                for (var a = 0; a < list.Count; a++)
                    for (var b = a + 1; b < list.Count; b++)
                    {
                        var key = string.CompareOrdinal(list[a].Id, list[b].Id) < 0 ? list[a].Id + "|" + list[b].Id : list[b].Id + "|" + list[a].Id;
                        if (!pairs.Add(key)) continue;
                        list[a].Neighbours.Add(list[b].Id);
                        list[b].Neighbours.Add(list[a].Id);
                    }
            }
        }

        /// <summary>The boxes of islands in a grid of cells, for the islands that touch a box.</summary>
        sealed class BoxGrid
        {
            readonly List<Island> _islands;
            readonly double _cell;
            readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();

            public BoxGrid(List<Island> islands, double cell, IEnumerable<int> only = null)
            {
                _islands = islands;
                _cell = cell;
                foreach (var i in only ?? Enumerable.Range(0, islands.Count))
                {
                    var x = islands[i];
                    foreach (var key in Keys(x.Min, x.Max, 0))
                    {
                        List<int> list;
                        if (!_cells.TryGetValue(key, out list)) _cells[key] = list = new List<int>(4);
                        list.Add(i);
                    }
                }
            }

            IEnumerable<long> Keys(double[] min, double[] max, double grow)
            {
                int x0 = (int)Math.Floor((min[0] - grow) / _cell), x1 = (int)Math.Floor((max[0] + grow) / _cell);
                int y0 = (int)Math.Floor((min[1] - grow) / _cell), y1 = (int)Math.Floor((max[1] + grow) / _cell);
                // a box that spans very many cells (a long pipe, a slab) is kept in a coarse way
                if ((long)(x1 - x0 + 1) * (y1 - y0 + 1) > 4096)
                {
                    var step = (int)Math.Ceiling(Math.Sqrt((double)(x1 - x0 + 1) * (y1 - y0 + 1) / 4096.0));
                    for (var x = x0; x <= x1; x += step)
                        for (var y = y0; y <= y1; y += step)
                            yield return ((long)x << 32) ^ (uint)y;
                    yield break;
                }
                for (var x = x0; x <= x1; x++)
                    for (var y = y0; y <= y1; y++)
                        yield return ((long)x << 32) ^ (uint)y;
            }

            public IEnumerable<int> Touching(int i, double tolerance) => Touching(_islands[i], tolerance, i);

            public IEnumerable<int> Touching(Island a, double tolerance, int self = -1)
            {
                var seen = new HashSet<int>();
                foreach (var key in Keys(a.Min, a.Max, tolerance))
                {
                    List<int> list;
                    if (!_cells.TryGetValue(key, out list)) continue;
                    foreach (var j in list)
                    {
                        if (j == self || !seen.Add(j)) continue;
                        var b = _islands[j];
                        if (a.Min[0] - tolerance <= b.Max[0] && b.Min[0] - tolerance <= a.Max[0] &&
                            a.Min[1] - tolerance <= b.Max[1] && b.Min[1] - tolerance <= a.Max[1] &&
                            a.Min[2] - tolerance <= b.Max[2] && b.Min[2] - tolerance <= a.Max[2])
                            yield return j;
                    }
                }
            }
        }
    }
}
