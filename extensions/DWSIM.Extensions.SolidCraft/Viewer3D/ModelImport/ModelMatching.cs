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
using DWSIM.Interfaces;
using ObjectType = DWSIM.Interfaces.Enums.GraphicObjects.ObjectType;

namespace DWSIM.Extensions.SolidCraft.Viewer3D.ModelImport
{
    /// <summary>A unit operation of the flowsheet as the matching sees it.</summary>
    public class FlowsheetUnit
    {
        /// <summary>GraphicObject.Name.</summary>
        public string Name;
        public string Tag;
        public string Category;
        /// <summary>The place of the icon on the canvas (y grows downward).</summary>
        public double X, Y;
        /// <summary>The size DWSIM estimates for the equipment (metres): height or length, diameter.</summary>
        public double Length, Diameter;
        public bool Vertical;
        /// <summary>The units a stream joins to this one, through valves, mixers, splitters and pipes.</summary>
        public HashSet<string> Neighbours = new HashSet<string>();
    }

    /// <summary>A flowsheet object proposed for a group of the model, with how sure the matching is (0 to 1) and why.</summary>
    public class Suggestion
    {
        public string Group;
        /// <summary>The object proposed, or null when the user already chose the group's object and only <see cref="Also"/> is proposed.</summary>
        public string Object;
        public string Tag;
        public double Score;
        public string Why;
        /// <summary>More objects of the same tag that go with the group (the other side of an exchanger).</summary>
        public List<string> Also = new List<string>();
    }

    /// <summary>
    /// Proposes, for each equipment group of a plant model, the flowsheet object it stands for. The
    /// score of a pair adds up five things:
    /// <list type="bullet">
    /// <item>the tag (V-100 in the model and in the flowsheet decides);</item>
    /// <item>the kind of equipment (a column for a column, a pump for a pump);</item>
    /// <item>the size, against the one DWSIM estimates;</item>
    /// <item>the place in the plan, against the place of the icon on the canvas, after the best of eight turns and mirrors;</item>
    /// <item>the lines: when two groups that a pipe joins go to two objects that a stream joins.</item>
    /// </list>
    /// The pairs are then chosen together, so that no object goes to two groups (the Hungarian
    /// algorithm), and the pairs too weak to propose are left out.
    /// </summary>
    public static class ModelMatching
    {
        /// <summary>
        /// The weights of the five things a pair's score adds up. The place on the canvas weighs
        /// little: a flowsheet is drawn in the order of the process, not as the plot plan.
        /// </summary>
        public static double WName = 4, WType = 1.5, WSize = 0.8, WPlace = 0.4, WLines = 1.6;
        static double Best => WName + WType + WSize + WPlace + WLines;

        /// <summary>The flowsheet's unit operations, with their estimated sizes (from the plant model DWSIM builds) and neighbours. Reads the flowsheet: run it on the interface thread.</summary>
        public static List<FlowsheetUnit> Units(IFlowsheet fs, PlantModel model)
        {
            var units = new List<FlowsheetUnit>();
            foreach (var item in model.Equipment)
            {
                ISimulationObject obj;
                if (item.Name == null || !fs.SimulationObjects.TryGetValue(item.Name, out obj) || obj?.GraphicObject == null) continue;
                var category = CategoryOf(obj.GraphicObject.ObjectType, item);
                if (category == null) continue;
                var u = new FlowsheetUnit { Name = item.Name, Tag = obj.GraphicObject.Tag, Category = category, X = item.CanvasX, Y = item.CanvasY };
                var e = item.Envelope;
                if (e != null)
                {
                    var h = (e.ZTop - e.ZBottom) / 1000.0;
                    switch (e.Kind)
                    {
                        case EnvelopeKind.VerticalCylinder: u.Vertical = true; u.Length = h; u.Diameter = 2 * e.HalfX / 1000.0; break;
                        case EnvelopeKind.HorizontalCylinder: u.Length = 2 * e.HalfX / 1000.0; u.Diameter = 2 * e.HalfY / 1000.0; break;
                        default:
                            var w = 2 * Math.Max(e.HalfX, e.HalfY) / 1000.0;
                            u.Vertical = h > w;
                            u.Length = u.Vertical ? h : w;
                            u.Diameter = u.Vertical ? w : 2 * Math.Min(e.HalfX, e.HalfY) / 1000.0;
                            break;
                    }
                }
                units.Add(u);
            }
            var names = new HashSet<string>(units.Select(x => x.Name));
            foreach (var u in units)
            {
                ISimulationObject obj;
                if (!fs.SimulationObjects.TryGetValue(u.Name, out obj)) continue;
                foreach (var n in Reach(obj.GraphicObject, names)) if (n != u.Name) u.Neighbours.Add(n);
            }
            return units;
        }

        /// <summary>The kind of equipment of a flowsheet object, or null for what is not equipment one sees in a plant model (streams, valves, mixers, logic).</summary>
        public static string CategoryOf(ObjectType t, EquipmentItem item)
        {
            switch (t)
            {
                case ObjectType.DistillationColumn:
                case ObjectType.AbsorptionColumn:
                case ObjectType.ShortcutColumn:
                case ObjectType.RefluxedAbsorber:
                case ObjectType.ReboiledAbsorber: return "column";
                case ObjectType.Vessel:
                case ObjectType.TPVessel:
                case ObjectType.SolidSeparator: return "vessel";
                case ObjectType.Tank: return "tank";
                case ObjectType.HeatExchanger:
                case ObjectType.Cooler:
                case ObjectType.HeaterCooler: return "exchanger";
                case ObjectType.Heater: return (item?.Style ?? "").Contains("fire") ? "furnace" : "exchanger";
                case ObjectType.AirCooler2: return "aircooler";
                case ObjectType.Pump: return "pump";
                case ObjectType.Compressor:
                case ObjectType.Expander:
                case ObjectType.CompressorExpander: return "compressor";
                case ObjectType.RCT_Conversion:
                case ObjectType.RCT_Equilibrium:
                case ObjectType.RCT_Gibbs:
                case ObjectType.RCT_CSTR:
                case ObjectType.RCT_PFR:
                case ObjectType.RCT_GibbsReaktoro: return "reactor";
                case ObjectType.Filter:
                case ObjectType.ComponentSeparator:
                case ObjectType.External: return "other";
                default: return null;
            }
        }

        /// <summary>The units a stream reaches from an object, through the objects one does not see as equipment.</summary>
        static IEnumerable<string> Reach(IGraphicObject start, HashSet<string> units)
        {
            var found = new HashSet<string>();
            var seen = new HashSet<IGraphicObject> { start };
            var queue = new Queue<KeyValuePair<IGraphicObject, int>>();
            queue.Enqueue(new KeyValuePair<IGraphicObject, int>(start, 0));
            while (queue.Count > 0)
            {
                var kv = queue.Dequeue();
                var go = kv.Key;
                if (kv.Value > 8) continue;
                foreach (var other in Adjacent(go))
                {
                    if (other == null || !seen.Add(other)) continue;
                    if (units.Contains(other.Name)) { found.Add(other.Name); continue; }
                    queue.Enqueue(new KeyValuePair<IGraphicObject, int>(other, kv.Value + 1));
                }
            }
            return found;
        }

        static IEnumerable<IGraphicObject> Adjacent(IGraphicObject go)
        {
            foreach (var cp in (go.InputConnectors ?? new List<IConnectionPoint>()).Concat(go.OutputConnectors ?? new List<IConnectionPoint>()))
            {
                if (cp == null || !cp.IsAttached || cp.AttachedConnector == null) continue;
                var c = cp.AttachedConnector;
                yield return c.AttachedFrom == go ? c.AttachedTo : c.AttachedFrom;
            }
        }

        static string Norm(string tag) => new string((tag ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

        /// <summary>
        /// How much the tags say the pair is one thing: the same tag (1); the same tag but a letter
        /// for the train or the spare, E-120 and E-120A (0.8); the same number and first letter
        /// (0.6). The tag of a flowsheet object is read from its name, which may carry words
        /// ("E-120 (gas de carga)").
        /// </summary>
        static double NameScore(SegmentGroup g, FlowsheetUnit u)
        {
            var a = Norm(g.Tag ?? ModelSegmentation.TagOf(g.Name ?? ""));
            var b = Norm(ModelSegmentation.TagOf(u.Tag ?? "") ?? u.Tag);
            if (a.Length == 0 || b.Length == 0) return 0;
            if (a == b) return 1;
            var da = new string(a.Where(char.IsDigit).ToArray());
            var db = new string(b.Where(char.IsDigit).ToArray());
            if (da.Length == 0 || da != db || a[0] != b[0]) return 0;
            var la = a.Substring(0, a.IndexOfAny("0123456789".ToCharArray()));
            var lb = b.Substring(0, b.IndexOfAny("0123456789".ToCharArray()));
            return la == lb ? 0.8 : 0.6;
        }

        static readonly Dictionary<string, double> Near = new Dictionary<string, double>
        {
            ["vessel|column"] = 0.5, ["vessel|tank"] = 0.6, ["vessel|reactor"] = 0.6, ["vessel|exchanger"] = 0.4,
            ["column|reactor"] = 0.3, ["pump|compressor"] = 0.5, ["furnace|exchanger"] = 0.5, ["aircooler|exchanger"] = 0.6,
            ["tank|reactor"] = 0.3
        };

        static double TypeScore(string a, string b)
        {
            if (a == null || b == null) return 0;
            if (a == b) return 1;
            if (a == "other" || b == "other") return 0.3;
            double v;
            if (Near.TryGetValue(a + "|" + b, out v) || Near.TryGetValue(b + "|" + a, out v)) return v;
            return 0;
        }

        static double SizeScore(SegmentGroup g, FlowsheetUnit u)
        {
            if (g.Length <= 0 || u.Length <= 0) return 0.3;
            var s = Math.Abs(Math.Log(g.Length / u.Length));
            if (g.Diameter > 0 && u.Diameter > 0) s += Math.Abs(Math.Log(g.Diameter / u.Diameter));
            var score = Math.Exp(-0.7 * s);
            if (g.Vertical != u.Vertical) score *= 0.6;
            return score;
        }

        /// <summary>
        /// The proposals for the equipment groups. A group whose object the user already chose
        /// (<paramref name="accepted"/>, group id to object name) keeps it, and that object is not
        /// proposed for another group.
        /// </summary>
        public static List<Suggestion> Suggest(List<SegmentGroup> groups, List<FlowsheetUnit> units, IDictionary<string, string> accepted = null, IEnumerable<string> chosenElsewhere = null)
        {
            accepted = accepted ?? new Dictionary<string, string>();
            var taken = new HashSet<string>(accepted.Values);
            if (chosenElsewhere != null) taken.UnionWith(chosenElsewhere);
            var gs = groups.Where(g => g.Role == "equipment" && !accepted.ContainsKey(g.Id)).ToList();
            var us = units.Where(u => !taken.Contains(u.Name)).ToList();
            var result = new List<Suggestion>();
            if (gs.Count == 0 || us.Count == 0) return result;

            var name = new double[gs.Count, us.Count];
            var type = new double[gs.Count, us.Count];
            var size = new double[gs.Count, us.Count];
            for (var i = 0; i < gs.Count; i++)
                for (var j = 0; j < us.Count; j++)
                {
                    name[i, j] = NameScore(gs[i], us[j]);
                    type[i, j] = TypeScore(gs[i].Category, us[j].Category);
                    size[i, j] = SizeScore(gs[i], us[j]);
                }

            // the plan against the canvas: both centred and scaled, and the canvas turned and
            // mirrored the eight ways; the way with the best assignment wins
            var gp = Standard(gs.Select(g => new[] { g.Center[0], g.Center[1] }).ToList());
            var upRaw = Standard(us.Select(u => new[] { u.X, -u.Y }).ToList());
            double[,] place = null;
            var bestTotal = double.MinValue;
            for (var k = 0; k < 8; k++)
            {
                var up = upRaw.Select(p => Turn(p, k)).ToList();
                var pl = new double[gs.Count, us.Count];
                for (var i = 0; i < gs.Count; i++)
                    for (var j = 0; j < us.Count; j++)
                    {
                        double dx = gp[i][0] - up[j][0], dy = gp[i][1] - up[j][1];
                        pl[i, j] = Math.Exp(-(dx * dx + dy * dy) / (2 * 0.35 * 0.35));
                    }
                var s = Combine(name, type, size, pl, null, gs.Count, us.Count);
                var a = Hungarian(s);
                var total = 0.0;
                for (var i = 0; i < gs.Count; i++) if (a[i] >= 0) total += s[i, a[i]];
                if (total > bestTotal) { bestTotal = total; place = pl; }
            }

            // the lines: rounds, each with the pairs of the round before and the pairs the user chose,
            // so that a few objects chosen by hand pull their neighbours in
            var groupIndex = gs.Select((g, i) => new KeyValuePair<string, int>(g.Id, i)).ToDictionary(kv => kv.Key, kv => kv.Value);
            var assign = Enumerable.Repeat(-1, gs.Count).ToArray();
            double[,] lines = null;
            double[,] score = null;
            for (var round = 0; round < 3; round++)
            {
                lines = new double[gs.Count, us.Count];
                for (var i = 0; i < gs.Count; i++)
                {
                    // the objects the group's neighbours stand for, now
                    var near = new List<string>();
                    foreach (var x in gs[i].Neighbours)
                    {
                        int k;
                        string chosen;
                        if (groupIndex.TryGetValue(x, out k)) { if (assign[k] >= 0) near.Add(us[assign[k]].Name); }
                        else if (accepted.TryGetValue(x, out chosen)) near.Add(chosen);
                    }
                    if (near.Count == 0) continue;
                    for (var j = 0; j < us.Count; j++)
                    {
                        var hits = near.Count(n => n != us[j].Name && us[j].Neighbours.Contains(n));
                        lines[i, j] = (double)hits / gs[i].Neighbours.Count;
                    }
                }
                score = Combine(name, type, size, place, lines, gs.Count, us.Count);
                assign = Hungarian(score);
            }

            for (var i = 0; i < gs.Count; i++)
            {
                var j = assign[i];
                if (j < 0) continue;
                // how sure: the score without the tag, over the most it can be; a tag that matches makes it sure
                var s = (score[i, j] - WName * name[i, j]) / (Best - WName);
                if (name[i, j] >= 1) s = Math.Max(s, 0.9);
                else if (name[i, j] > 0) s = Math.Max(s, 0.6);
                // too weak to propose; without a tag that helps, only a clear case is proposed (in a real
                // plant the flowsheet leaves out spares and splits exchangers, so kind and lines alone
                // mislead more often than they help)
                if (s < (name[i, j] > 0 ? 0.35 : 0.7)) continue;
                var why = new List<string>();
                if (name[i, j] >= 1) why.Add("same tag");
                else if (name[i, j] > 0) why.Add("similar tag");
                if (type[i, j] >= 1) why.Add("same kind (" + us[j].Category + ")");
                if (size[i, j] > 0.6) why.Add("similar size");
                if (place[i, j] > 0.5) why.Add("same place in the plan");
                if (lines != null && lines[i, j] > 0) why.Add("joined like the streams");
                result.Add(new Suggestion { Group = gs[i].Id, Object = us[j].Name, Tag = us[j].Tag, Score = Math.Round(s, 3), Why = string.Join(", ", why) });
            }

            // a flowsheet may draw one piece of equipment as several objects (the two sides of an
            // exchanger): an object left over whose tag is a group's own, or of its family, goes with it
            var used = new HashSet<string>(taken.Concat(result.Select(r => r.Object)));
            var equipment = groups.Where(g => g.Role == "equipment").ToList();
            foreach (var u in units.Where(x => !used.Contains(x.Name)))
            {
                SegmentGroup best = null;
                var bestScore = 0.0;
                foreach (var g in equipment)
                {
                    var n = NameScore(g, u);
                    if (n >= 0.8 && n > bestScore) { bestScore = n; best = g; }
                }
                if (best == null) continue;
                var sug = result.FirstOrDefault(r => r.Group == best.Id);
                if (sug == null)
                {
                    sug = new Suggestion { Group = best.Id, Object = accepted.ContainsKey(best.Id) ? null : u.Name, Tag = u.Tag, Score = 0.9, Why = "same tag" };
                    result.Add(sug);
                    if (sug.Object != null) { used.Add(u.Name); continue; }
                }
                sug.Also.Add(u.Name);
                used.Add(u.Name);
            }
            return result;
        }

        static double[,] Combine(double[,] name, double[,] type, double[,] size, double[,] place, double[,] lines, int n, int m)
        {
            var s = new double[n, m];
            for (var i = 0; i < n; i++)
                for (var j = 0; j < m; j++)
                    s[i, j] = WName * name[i, j] + WType * type[i, j] + WSize * size[i, j] + WPlace * place[i, j] + (lines == null ? 0 : WLines * lines[i, j]);
            return s;
        }

        static List<double[]> Standard(List<double[]> pts)
        {
            if (pts.Count == 0) return pts;
            double cx = pts.Average(p => p[0]), cy = pts.Average(p => p[1]);
            var rms = Math.Sqrt(pts.Average(p => (p[0] - cx) * (p[0] - cx) + (p[1] - cy) * (p[1] - cy)));
            if (rms <= 0) rms = 1;
            return pts.Select(p => new[] { (p[0] - cx) / rms, (p[1] - cy) / rms }).ToList();
        }

        /// <summary>One of the eight turns and mirrors of the plan: k mod 4 quarter turns, mirrored when k is 4 or more.</summary>
        static double[] Turn(double[] p, int k)
        {
            double x = k >= 4 ? -p[0] : p[0], y = p[1];
            switch (k % 4)
            {
                case 1: return new[] { -y, x };
                case 2: return new[] { -x, -y };
                case 3: return new[] { y, -x };
                default: return new[] { x, y };
            }
        }

        /// <summary>The assignment of rows to columns with the largest total score (Kuhn and Munkres, with potentials); -1 for a row left out.</summary>
        public static int[] Hungarian(double[,] score)
        {
            int n = score.GetLength(0), m = score.GetLength(1);
            var size = Math.Max(n, m);
            var max = 0.0;
            for (var i = 0; i < n; i++) for (var j = 0; j < m; j++) max = Math.Max(max, score[i, j]);
            // costs, 1-based, with the padding rows and columns at the largest cost
            var a = new double[size + 1, size + 1];
            for (var i = 1; i <= size; i++)
                for (var j = 1; j <= size; j++)
                    a[i, j] = i <= n && j <= m ? max - score[i - 1, j - 1] : max;
            var u = new double[size + 1];
            var v = new double[size + 1];
            var p = new int[size + 1];
            var way = new int[size + 1];
            for (var i = 1; i <= size; i++)
            {
                p[0] = i;
                var j0 = 0;
                var minv = Enumerable.Repeat(double.MaxValue, size + 1).ToArray();
                var used = new bool[size + 1];
                do
                {
                    used[j0] = true;
                    int i0 = p[j0], j1 = 0;
                    var delta = double.MaxValue;
                    for (var j = 1; j <= size; j++)
                    {
                        if (used[j]) continue;
                        var cur = a[i0, j] - u[i0] - v[j];
                        if (cur < minv[j]) { minv[j] = cur; way[j] = j0; }
                        if (minv[j] < delta) { delta = minv[j]; j1 = j; }
                    }
                    for (var j = 0; j <= size; j++)
                    {
                        if (used[j]) { u[p[j]] += delta; v[j] -= delta; }
                        else minv[j] -= delta;
                    }
                    j0 = j1;
                } while (p[j0] != 0);
                do
                {
                    var j1 = way[j0];
                    p[j0] = p[j1];
                    j0 = j1;
                } while (j0 != 0);
            }
            var result = Enumerable.Repeat(-1, n).ToArray();
            for (var j = 1; j <= size; j++)
                if (p[j] >= 1 && p[j] <= n && j <= m) result[p[j] - 1] = j - 1;
            return result;
        }
    }
}
