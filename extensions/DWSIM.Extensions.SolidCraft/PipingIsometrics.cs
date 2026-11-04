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
using System.IO;
using System.Linq;
using DWSIM.Interfaces;
using Newtonsoft.Json.Linq;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// The piping isometric of each line of the 3D plant model: the route from nozzle to nozzle (or
    /// to the battery limit) in isometric projection, not to scale, with elbows, flanges, welds, the
    /// true length of every leg, elevations, end coordinates, the flow direction and a north arrow,
    /// beside the line data (operating and preliminary design conditions, material, flange rating)
    /// and a bill of materials, on the sheet and title block of the chosen standard. Drawn in
    /// CADCraft, one drawing per line, saved as DXF and DWG and plotted to PDF and PNG.
    /// </summary>
    public static class PipingIsometrics
    {
        public class Result
        {
            public List<string> Drawings = new List<string>(), Pdfs = new List<string>(), Failed = new List<string>();
            public string Error;
        }

        const double TextHeight = 2.5;

        /// <summary>The longest spool, mm of pipe: what a truck carries.</summary>
        const double SpoolLength = 12000;

        /// <summary>Shortest and longest leg on the sheet, mm.</summary>
        const double MinLeg = 12, MaxLeg = 110;

        static readonly double C30 = Math.Cos(Math.PI / 6), S30 = 0.5;

        struct P2
        {
            public double X, Y;
            public P2(double x, double y) { X = x; Y = y; }
            public static P2 operator +(P2 a, P2 b) => new P2(a.X + b.X, a.Y + b.Y);
            public static P2 operator -(P2 a, P2 b) => new P2(a.X - b.X, a.Y - b.Y);
            public static P2 operator *(P2 a, double f) => new P2(a.X * f, a.Y * f);
            public double Length => Math.Sqrt(X * X + Y * Y);
            public P2 Unit() { var l = Length; return l > 1e-9 ? this * (1 / l) : new P2(1, 0); }
            public P2 Left => new P2(-Y, X);
        }

        /// <summary>The isometric projection: east down to the right, north up to the right, up straight up.</summary>
        static P2 Iso(Vec3 v) => new P2((v.X + v.Y) * C30, (v.Y - v.X) * S30 + v.Z);

        /// <summary>Draws every line of the 3D model; <paramref name="titleFor"/> fills each title block from the line, its number and its short id.</summary>
        public static Result Export(IFlowsheet fs, string cadcraftFolder, string outputFolder, string name, DrawingStyle style, Func<PipeRun, string, string, TitleData> titleFor,
            ICollection<string> only = null)
        {
            var result = new Result();
            PlantModel model;
            try { model = PlantModelBuilder.Build(fs, name, PlantLayout.Load(fs), ExportOptions.Load()); }
            catch (Exception ex) { result.Error = "the plant model could not be built (" + ex.Message + ")"; return result; }
            if (model.Pipes.Count == 0) { result.Error = "the 3D model has no lines"; return result; }

            Dictionary<string, (string Number, string Id)> numbers;
            try { numbers = ProcessFlowDiagram.LineNumbers(fs, model.Pipes.GroupBy(r => r.Tag).ToDictionary(g => g.Key, g => g.First())); }
            catch (Exception) { numbers = new Dictionary<string, (string, string)>(); }

            var temp = Path.Combine(Path.GetTempPath(), "dwsim-iso-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(temp);
            var next = numbers.Values.Select(v => int.TryParse(v.Id, out var i) ? i : 0).DefaultIfEmpty(0).Max();
            using (var session = new CADCraftSession(cadcraftFolder))
            {
                var runs = only == null ? model.Pipes : model.Pipes.Where(r => only.Contains(r.Tag)).ToList();
                foreach (var run in runs)
                {
                    CadJob.Report(string.Format(CultureInfo.InvariantCulture, "CADCraft: drawing {0} of {1}, line {2}", runs.IndexOf(run) + 1, runs.Count, run.Tag));
                    try
                    {
                        if (!numbers.TryGetValue(run.Tag ?? "", out var n))
                        {
                            var id = (++next).ToString(CultureInfo.InvariantCulture);
                            n = (run.Size + "-P-" + id + (run.InsulationService == "hot" ? "-H" : run.InsulationService == "cold" ? "-C" : ""), id);
                        }
                        var file = Draw(session, model, run, n.Number, Path.Combine(outputFolder, name + " - ISO " + Safe(run.Tag)), style, titleFor(run, n.Number, n.Id), temp);
                        if (file != null)
                        {
                            result.Drawings.Add(file);
                            if (File.Exists(Path.ChangeExtension(file, ".pdf"))) result.Pdfs.Add(Path.ChangeExtension(file, ".pdf"));
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) { result.Failed.Add(run.Tag + ": " + ex.Message); }
                }
                result.Failed.AddRange(session.Failed);
            }
            try { Directory.Delete(temp, true); } catch (Exception) { }
            if (result.Drawings.Count == 0 && result.Error == null) result.Error = "CADCraft did not save any drawing" + (result.Failed.Count > 0 ? " (" + result.Failed.Last() + ")" : "");
            return result;
        }

        static string Safe(string s) => string.Concat((s ?? "line").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

        static string F(double v, string format) => v.ToString(format, CultureInfo.InvariantCulture);

        static string Elevation(double z) => "EL " + F(z / 1000, "+0.000;-0.000");

        /// <summary>One line's drawing; returns the DXF file, or null.</summary>
        static string Draw(CADCraftSession session, PlantModel model, PipeRun run, string number, string outputBase, DrawingStyle style, TitleData title, string temp)
        {
            const string bl = "battery limit";
            var fromItem = run.From == bl ? null : model.Equipment.FirstOrDefault(e => e.Tag == run.From);
            var toItem = run.To == bl ? null : model.Equipment.FirstOrDefault(e => e.Tag == run.To);

            // the route from flange face to flange face: the nozzle necks belong to the items
            var path = new List<Vec3>(run.Path);
            if (fromItem != null && run.Flanges.Count > 0) path = Cut(path, run.Flanges[0].Item1, true);
            var ki = fromItem != null ? 1 : 0;
            if (toItem != null && run.Flanges.Count > ki) path = Cut(path, run.Flanges[ki].Item1, false);
            path = Simplify(path);
            if (path.Count < 2) return null;
            var n = path.Count - 1;
            var dirs = new Vec3[n]; var lengths = new double[n];
            for (var i = 0; i < n; i++) { var v = path[i + 1] - path[i]; lengths[i] = v.Length; dirs[i] = v.Normalized(); }
            var cum = new double[n + 1];
            for (var i = 0; i < n; i++) cum[i + 1] = cum[i] + lengths[i];

            // the sheet: the isometric on the left, the tables over the title block, notes underneath
            DrawingSheets.Margins(style, out var ml, out var mo);
            var sheet = SheetSize.For(style).First();
            var notes = new[]
            {
                "NOTES",
                "1. DIMENSIONS IN MM. COORDINATES AND ELEVATIONS IN M, ELEVATIONS FROM GRADE (EL +0.000).",
                "2. NOT TO SCALE. NORTH IS +Y OF THE PLOT PLAN.",
                "3. DESIGN CONDITIONS, MATERIAL AND FLANGE RATING ARE PRELIMINARY ESTIMATES FROM THE",
                "   OPERATING CONDITIONS (ASME B31.3 AND B16.5).",
                "4. WELDS: W SHOP, FW FIELD. SPOOLS (SP) OF UP TO 12 M OF PIPE, SPLIT AT THE FIELD WELDS.",
            };
            double ax0 = ml + 12, ax1 = sheet.Width - mo - DrawingSheets.TitleWidth - 12;
            double ay0 = mo + 12 + notes.Length * 4.2, ay1 = sheet.Height - mo - 12;

            // each leg drawn along its isometric axis, its length compressed so short legs stay
            // readable next to long ones, the whole fitted to the drawing area with room for labels;
            // of the four ways to turn the plan, the one whose legs cross least and come out largest
            double k = 1;
            var q = new P2[n + 1];
            var view = new Vec3[n];
            double Leg(int i) => Math.Max(MinLeg, Math.Min(MaxLeg, k * Math.Sqrt(lengths[i])));
            void Lay()
            {
                q[0] = new P2(0, 0);
                for (var i = 0; i < n; i++) q[i + 1] = q[i] + Iso(view[i]) * Leg(i);
            }
            double aw = ax1 - ax0 - 70, ah = ay1 - ay0 - 50;
            void Fit()
            {
                k = 1;
                for (var it = 0; it < 12; it++)
                {
                    Lay();
                    var w = Math.Max(1, q.Max(p => p.X) - q.Min(p => p.X));
                    var h = Math.Max(1, q.Max(p => p.Y) - q.Min(p => p.Y));
                    var f = Math.Min(aw / w, ah / h);
                    if (Math.Abs(f - 1) < 0.02) break;
                    k *= Math.Max(0.3, Math.Min(3, f));
                }
                Lay();
            }
            var turn = 0; var bestScore = double.MaxValue;
            foreach (var rot in new[] { 0, 90, 180, 270 })
            {
                for (var i = 0; i < n; i++) view[i] = dirs[i].TurnedZ(rot);
                Fit();
                var score = Crossings(q) * 1000 - k;
                if (score < bestScore - 1e-6) { bestScore = score; turn = rot; }
            }
            for (var i = 0; i < n; i++) view[i] = dirs[i].TurnedZ(turn);
            Fit();
            {
                var cx = (q.Max(p => p.X) + q.Min(p => p.X)) / 2; var cy = (q.Max(p => p.Y) + q.Min(p => p.Y)) / 2;
                var shift = new P2((ax0 + ax1) / 2 - cx, (ay0 + ay1) / 2 - cy - 4);
                for (var i = 0; i <= n; i++) q[i] = q[i] + shift;
            }

            var cmds = new List<(string Name, JObject P)>();
            void C(string name, JObject p) => cmds.Add((name, p));
            JArray A(P2 p) => new JArray(Math.Round(p.X, 2), Math.Round(p.Y, 2));
            var clutter = new Clutter { X0 = ax0, Y0 = ay0, X1 = ax1, Y1 = ay1 };
            void Text(P2 at, string s, double h, string just, double angle = 0)
            {
                var o = new JObject { ["at"] = A(at), ["text"] = s, ["height"] = h, ["justify"] = just };
                if (Math.Abs(angle) > 0.01) o["rotation"] = Math.Round(angle, 2);
                C("text", o);
                clutter.Boxes.Add(TextBox(at, s, h, just, angle));
            }

            C("linetype", new JObject { ["load"] = "*" });
            C("style", new JObject { ["name"] = DrawingSheets.TextStyle, ["font"] = DrawingSheets.Font, ["current"] = true });
            foreach (var (ln, lt, lw) in new[] { ("PIPE", "Continuous", 0.7), ("CONNECTION", "DASHED", 0.25), ("SYMBOL", "Continuous", 0.35), ("DIMENSION", "Continuous", 0.18),
                ("TEXT", "Continuous", 0.25), ("TABLE", "Continuous", 0.25), ("BORDER", "Continuous", 0.7), ("TITLE", "Continuous", 0.35), ("VIEWPORT", "Continuous", 0.0) })
                C("layer.new", new JObject { ["name"] = ln, ["color"] = "white", ["linetype"] = lt, ["lineweight"] = lw });
            C("ltscale", new JObject { ["scale"] = 0.4 });

            // the pipe: a single thick line, the elbows rounded, a weld dot at each end of each
            // elbow, and the leg behind broken where two legs cross (depth towards the viewer,
            // along (1, -1, 1) of the turned plan)
            var depth = new double[n + 1];
            {
                var d3 = new Vec3(0, 0, 0);
                for (var i = 0; i < n; i++) { d3 = d3 + view[i] * Leg(i); depth[i + 1] = d3.X - d3.Y + d3.Z; }
            }
            var gaps = new List<(int Leg, P2 At)>();
            for (var i = 0; i < n; i++)
                for (var j = i + 2; j < n; j++)
                    if (Intersect(q[i], q[i + 1], q[j], q[j + 1], out var ti, out var tj))
                    {
                        var at = q[i] + (q[i + 1] - q[i]) * ti;
                        var di = depth[i] + (depth[i + 1] - depth[i]) * ti;
                        var dj = depth[j] + (depth[j + 1] - depth[j]) * tj;
                        gaps.Add((di < dj ? i : j, at));
                    }
            var rr = new double[n + 1];
            for (var i = 1; i < n; i++) rr[i] = Math.Min(2.5, 0.3 * Math.Min((q[i] - q[i - 1]).Length, (q[i + 1] - q[i]).Length));
            var welds = new List<(P2 At, double S, bool Joint)>();
            var pipes = new List<List<P2>>();
            var cur = new List<P2> { q[0] };
            for (var i = 0; i < n; i++)
            {
                var d = (q[i + 1] - q[i]).Unit();
                var st = q[i] + d * rr[i]; var e = q[i + 1] - d * rr[i + 1];
                var len = (e - st).Length;
                foreach (var g in gaps.Where(g => g.Leg == i).Select(g => g.At).OrderBy(g => (g - st).Length))
                {
                    var along = (g - st).Length;
                    if (along < 2.5 || along > len - 2.5) continue;
                    cur.Add(g - d * 1.8);
                    pipes.Add(cur);
                    cur = new List<P2> { g + d * 1.8 };
                }
                cur.Add(e);
                if (i < n - 1)
                {
                    var t2 = q[i + 1] + (q[i + 2] - q[i + 1]).Unit() * rr[i + 1];
                    for (var sgm = 1; sgm <= 6; sgm++)
                    {
                        var t = sgm / 6.0;
                        cur.Add(e * ((1 - t) * (1 - t)) + q[i + 1] * (2 * t * (1 - t)) + t2 * (t * t));
                    }
                    welds.Add((e, cum[i + 1] - 1e-3, false)); welds.Add((t2, cum[i + 1] + 1e-3, false));
                }
            }
            pipes.Add(cur);
            C("layer.current", new JObject { ["name"] = "PIPE" });
            foreach (var pl in pipes)
            {
                for (var i = 0; i < pl.Count - 1; i++) clutter.Segs.Add((pl[i], pl[i + 1]));
                C("pline", new JObject { ["vertices"] = new JArray(pl.Select(A)) });
            }

            // the ends: the flange pair at a nozzle (the item's neck dashed beyond it), or a flange
            // and a blind flange at the battery limit
            P2 Across(Vec3 d) => Math.Abs(d.Z) < 0.7 ? new P2(0, 1) : Iso(Vec3.PlusX);
            var ends = new[] { (At: q[0], Into: (q[1] - q[0]).Unit(), Dir3: view[0], Item: fromItem, Outlet: true),
                               (At: q[n], Into: (q[n - 1] - q[n]).Unit(), Dir3: view[n - 1], Item: toItem, Outlet: false) };
            C("layer.current", new JObject { ["name"] = "SYMBOL" });
            foreach (var e in ends)
            {
                var t = Across(e.Dir3) * 2.6;
                welds.Add((e.At + e.Into * 1.2, e.Outlet ? 0 : cum[n], false));
                var outside = e.At - e.Into * 1.0;
                C("line", new JObject { ["points"] = new JArray(A(e.At - t), A(e.At + t)) });
                C("line", new JObject { ["points"] = new JArray(A(outside - t), A(outside + t)) });
                clutter.Segs.Add((e.At - t, e.At + t)); clutter.Segs.Add((outside - t, outside + t));
                if (e.Item == null)
                {
                    var b = outside - e.Into * 0.9;
                    C("solid", new JObject { ["points"] = new JArray(A(outside - t), A(outside + t), A(b - t), A(b + t)) });
                }
                else
                {
                    var neck = outside - e.Into * 7;
                    C("layer.current", new JObject { ["name"] = "CONNECTION" });
                    C("line", new JObject { ["points"] = new JArray(A(outside), A(neck)) });
                    C("layer.current", new JObject { ["name"] = "SYMBOL" });
                    clutter.Segs.Add((outside, neck));
                }
            }
            // a leg longer than a spool gets field joints along it, every 12 m of pipe
            for (var i = 0; i < n; i++)
                for (var j = 1; j * SpoolLength < lengths[i] - 500; j++)
                    welds.Add((q[i] + (q[i + 1] - q[i]) * (j * SpoolLength / lengths[i]), cum[i] + j * SpoolLength, true));
            // the spools: as much pipe as fits in one, from weld to weld; where the next weld would
            // make it longer, this one is made in the field
            welds = welds.OrderBy(w => w.S).ToList();
            var fieldWeld = new bool[welds.Count];
            {
                double s0 = 0;
                for (var w = 1; w < welds.Count - 1; w++)
                {
                    if (welds[w].Joint || welds[w + 1].S - s0 > SpoolLength) { fieldWeld[w] = true; s0 = welds[w].S; }
                }
            }
            var spoolStarts = new List<double> { 0 };
            for (var w = 0; w < welds.Count; w++) if (fieldWeld[w]) spoolStarts.Add(welds[w].S);
            foreach (var w in welds) C("donut", new JObject { ["center"] = A(w.At), ["inside"] = 0, ["outside"] = 1.0 });

            // the flow arrow on the longest leg
            var longest = Enumerable.Range(0, n).OrderByDescending(i => (q[i + 1] - q[i]).Length).First();
            {
                var d = (q[longest + 1] - q[longest]).Unit();
                var m = (q[longest] + q[longest + 1]) * 0.5;
                C("solid", new JObject { ["points"] = new JArray(A(m + d * 2.2), A(m - d * 2.2 + d.Left * 1.1), A(m - d * 2.2 - d.Left * 1.1)) });
                clutter.Boxes.Add(new[] { m - d * 2.5 - d.Left * 1.5, m + d * 2.5 - d.Left * 1.5, m + d * 2.5 + d.Left * 1.5, m - d * 2.5 + d.Left * 1.5 });
            }

            // the north arrow, top left, turned with the plan
            {
                var c = new P2(ax0 + 14, ay1 - 14);
                var d = Iso(new Vec3(0, 1, 0).TurnedZ(turn));
                var tail = c - d * 8; var tip = c + d * 8;
                C("line", new JObject { ["points"] = new JArray(A(tail), A(tip)) });
                C("solid", new JObject { ["points"] = new JArray(A(tip), A(tip - d * 4 + d.Left * 1.4), A(tip - d * 4 - d.Left * 1.4)) });
                C("layer.current", new JObject { ["name"] = "TEXT" });
                Text(tip + d * 2.5, "N", 3.5, d.X >= 0 ? "ML" : "MR");
                clutter.Boxes.Add(Quad(c - new P2(12, 10), c + new P2(12, 10)));
                C("layer.current", new JObject { ["name"] = "SYMBOL" });
            }

            C("layer.current", new JObject { ["name"] = "TEXT" });

            // the end labels: where the line comes from and goes to, coordinates and elevation
            for (var j = 0; j < 2; j++)
            {
                var e = ends[j];
                var p3 = j == 0 ? path[0] : path[n];
                var mark = e.Item == null ? null : EquipmentDrawings.NozzleMark(model, e.Item, run, e.Outlet);
                var lines = new[]
                {
                    (j == 0 ? "FROM " : "TO ") + (e.Item == null ? "BATTERY LIMIT" : e.Item.Tag + (mark != null ? " NOZZLE " + mark : "")),
                    "E " + F(p3.X / 1000, "0.000") + "  N " + F(p3.Y / 1000, "0.000"),
                    Elevation(p3.Z)
                };
                PlaceBlock(clutter, e.At, e.Into * -1, lines, TextHeight, C, Text);
            }

            // the true length of each leg, dimensioned in its isometric plane
            for (var i = 0; i < n; i++)
            {
                var a = q[i]; var b = q[i + 1];
                var d = (b - a).Unit();
                var normals = Math.Abs(dirs[i].Z) < 0.7
                    ? new[] { new P2(0, 1), new P2(0, -1) }
                    : new[] { Iso(Vec3.PlusX), Iso(Vec3.PlusX) * -1, Iso(new Vec3(0, 1, 0)), Iso(new Vec3(0, 1, 0)) * -1 };
                if (Math.Abs(dirs[i].X) < 0.7 && Math.Abs(dirs[i].Y) < 0.7 && Math.Abs(dirs[i].Z) < 0.7) normals = new[] { d.Left, d.Left * -1 };
                var label = F(Math.Round(lengths[i]), "0");
                var angle = Upright(d);
                double best = double.MaxValue; List<(P2, P2)> bestSegs = null; P2 bestAt = default; string bestJust = null;
                foreach (var off in new[] { 6.0, 11.0, 16.0, 22.0 })
                    foreach (var nv in normals)
                    {
                        // the offset along the normal, measured square to the leg
                        var sq = Math.Abs(nv.X * d.Left.X + nv.Y * d.Left.Y);
                        if (sq < 0.3) continue;
                        var o = nv * (off / sq);
                        var segs = new List<(P2, P2)> { (a + o, b + o), (a + nv * (1.5 / sq), a + o + nv * (1.5 / sq)), (b + nv * (1.5 / sq), b + o + nv * (1.5 / sq)) };
                        var up = new P2(-Math.Sin(angle * Math.PI / 180), Math.Cos(angle * Math.PI / 180));
                        var above = up.X * nv.X + up.Y * nv.Y >= 0;
                        var at = (a + b) * 0.5 + o + nv * (0.9 / sq);
                        var just = above ? "BC" : "TC";
                        var cost = clutter.Cost(segs, new[] { TextBox(at, label, TextHeight, just, angle) }) + off * 0.05;
                        if (cost < best) { best = cost; bestSegs = segs; bestAt = at; bestJust = just; }
                    }
                if (bestSegs == null) continue;
                C("layer.current", new JObject { ["name"] = "DIMENSION" });
                foreach (var s in bestSegs)
                {
                    C("line", new JObject { ["points"] = new JArray(A(s.Item1), A(s.Item2)) });
                    clutter.Segs.Add(s);
                }
                var dl = bestSegs[0];
                foreach (var tp in new[] { dl.Item1, dl.Item2 })
                {
                    var tk = (d + d.Left).Unit() * 1.2;
                    C("line", new JObject { ["points"] = new JArray(A(tp - tk), A(tp + tk)) });
                }
                C("layer.current", new JObject { ["name"] = "TEXT" });
                Text(bestAt, label, TextHeight, bestJust, angle);
            }

            // the line number along the longest horizontal leg, the elevation of each horizontal
            // leg whose height changes along the line
            var horizontal = Enumerable.Range(0, n).Where(i => Math.Abs(dirs[i].Z) < 0.1).ToList();
            AlongLeg(clutter, (horizontal.Count > 0 ? horizontal : Enumerable.Range(0, n).ToList()).Select(i => (q[i], q[i + 1])).ToList(), number, 3.0, Text);
            var lastZ = path[0].Z;
            foreach (var i in horizontal)
            {
                if (Math.Abs(path[i].Z - lastZ) > 1 && i > 0) AlongLeg(clutter, new List<(P2, P2)> { (q[i], q[i + 1]) }, Elevation(path[i].Z), TextHeight, Text, true);
                lastZ = path[i].Z;
            }

            // the weld numbers, along the line, and a spool number in the middle of each spool
            for (var w = 0; w < welds.Count; w++)
                PlaceLabel(clutter, welds[w].At, (fieldWeld[w] ? "FW" : "W") + (w + 1).ToString(CultureInfo.InvariantCulture), 1.6, new[] { 2.2, 3.5, 5.0 }, Text);
            P2 Along(double sAt)
            {
                for (var i = 0; i < n; i++)
                    if (sAt <= cum[i + 1] || i == n - 1)
                        return q[i] + (q[i + 1] - q[i]) * (lengths[i] > 0 ? Math.Max(0, Math.Min(1, (sAt - cum[i]) / lengths[i])) : 0);
                return q[n];
            }
            for (var sp = 0; sp < spoolStarts.Count; sp++)
            {
                var sEnd = sp + 1 < spoolStarts.Count ? spoolStarts[sp + 1] : cum[n];
                PlaceLabel(clutter, Along((spoolStarts[sp] + sEnd) / 2), "SP" + (sp + 1).ToString(CultureInfo.InvariantCulture), 2.2, new[] { 4.0, 6.5, 9.0 }, Text);
            }

            // the sheet
            title.Scale = "NTS";
            C("layout.new", new JObject { ["name"] = "ISO", ["viewport"] = false });
            C("pagesetup", new JObject
            {
                ["layout"] = "ISO", ["device"] = "CADCraft PDF", ["width"] = sheet.Width, ["height"] = sheet.Height, ["landscape"] = true,
                ["margins"] = new JArray(0, 0, 0, 0), ["plotArea"] = "layout", ["scale"] = 1, ["lineweights"] = true, ["plotStyleTable"] = "monochrome.ctb"
            });
            C("layout.set", new JObject { ["name"] = "ISO" });
            C("pspace", new JObject());
            var border = Path.Combine(temp, Safe(run.Tag) + "-border.dxf");
            DrawingSheets.Border(style, sheet).Save(border);
            C("layer.current", new JObject { ["name"] = "BORDER" });
            C("insert", new JObject { ["name"] = "SHEET_BORDER", ["file"] = border.Replace('\\', '/'), ["at"] = new JArray(0, 0) });
            var tb = Path.Combine(temp, Safe(run.Tag) + "-title.dxf");
            DrawingSheets.TitleBlock(style, out _).Save(tb);
            C("layer.current", new JObject { ["name"] = "TITLE" });
            C("insert", new JObject
            {
                ["name"] = "TITLE_BLOCK", ["file"] = tb.Replace('\\', '/'), ["at"] = new JArray(sheet.Width - mo, mo),
                ["attribs"] = JObject.FromObject(DrawingSheets.Attributes(title, sheet, style))
            });

            // the tables over the title block: the line data and the bill of materials
            C("tablestyle", new JObject { ["name"] = "ISO", ["textHeight"] = TextHeight, ["margin"] = 0.8, ["current"] = true });
            foreach (var cs in new[] { "Header", "Data" })
                C("tablestyle.cellstyle", new JObject { ["style"] = "ISO", ["name"] = cs, ["textStyle"] = DrawingSheets.TextStyle, ["textHeight"] = TextHeight });
            C("layer.current", new JObject { ["name"] = "TABLE" });
            var tx = sheet.Width - mo - DrawingSheets.TitleWidth;
            var top = sheet.Height - mo - 5;
            var design = DesignBasis.For(run);
            var fromText = fromItem == null ? "Battery limit" : fromItem.Tag + Nz(EquipmentDrawings.NozzleMark(model, fromItem, run, true));
            var toText = toItem == null ? "Battery limit" : toItem.Tag + Nz(EquipmentDrawings.NozzleMark(model, toItem, run, false));
            var data = LineData(run, fromText, toText, design);
            data.Add(new KeyValuePair<string, string>("Spools (12 m max.)", spoolStarts.Count.ToString(CultureInfo.InvariantCulture)));
            C("table", new JObject
            {
                ["at"] = new JArray(tx, top), ["rows"] = data.Count, ["cols"] = 2, ["rowHeight"] = 5.5, ["colWidth"] = DrawingSheets.TitleWidth / 2,
                ["header"] = new JArray("LINE", number), ["cells"] = new JArray(data.Select(kv => new JArray(kv.Key, kv.Value)))
            });
            top -= (data.Count + 1) * 5.5 + 5;
            var bom = Materials(run, lengths, n - 1, fromItem == null || toItem == null ? 1 : 0, design);
            var fields = fieldWeld.Count(f => f);
            bom.Add(new KeyValuePair<string, string>("Butt welds, shop " + run.Size, (welds.Count - fields).ToString(CultureInfo.InvariantCulture)));
            if (fields > 0) bom.Add(new KeyValuePair<string, string>("Butt welds, field " + run.Size, fields.ToString(CultureInfo.InvariantCulture)));
            C("table", new JObject
            {
                ["at"] = new JArray(tx, top), ["rows"] = bom.Count, ["cols"] = 2, ["rowHeight"] = 5.5, ["colWidth"] = DrawingSheets.TitleWidth / 2,
                ["header"] = new JArray("BILL OF MATERIALS", "QUANTITY"), ["cells"] = new JArray(bom.Select(kv => new JArray(kv.Key, kv.Value)))
            });

            // the notes, under the drawing
            C("layer.current", new JObject { ["name"] = "TEXT" });
            for (var i = 0; i < notes.Length; i++)
                C("text", new JObject { ["at"] = new JArray(ml + 12, mo + 8 + (notes.Length - 1 - i) * 4.2), ["text"] = notes[i], ["height"] = i == 0 ? 2.5 : 2.2, ["justify"] = "BL" });

            File.Delete(outputBase + ".dxf");
            if (session.Tool("new_drawing", new JObject { ["metric"] = true }, "new drawing for line " + run.Tag) == null) return null;
            foreach (var c in cmds) session.Execute(c.Name, c.P);

            // the viewport over the isometric, at 1:1: the model is drawn in millimetres of paper
            session.Execute("layer.current", new JObject { ["name"] = "VIEWPORT" });
            session.Execute("layer.set", new JObject { ["name"] = "VIEWPORT", ["plot"] = false });
            var vp = session.Execute("mview", new JObject { ["p1"] = new JArray(ax0 - 4, ay0 - 4), ["p2"] = new JArray(ax1 + 4, ay1 + 4) });
            var handle = vp == null ? null : (JObject.Parse(vp)["viewports"] as JArray)?.FirstOrDefault()?.ToString();
            if (handle != null)
                session.Execute("viewport.set", new JObject { ["handle"] = handle, ["scale"] = 1.0, ["locked"] = true, ["center"] = new JArray((ax0 + ax1) / 2, (ay0 + ay1) / 2) });

            return DrawingSheets.Save(session, outputBase, "ISO", "line " + run.Tag);
        }

        static string Nz(string mark) => mark == null ? "" : " (" + mark + ")";

        /// <summary>A text angle along a direction, turned to read from the bottom or the right.</summary>
        static double Upright(P2 d)
        {
            var a = Math.Atan2(d.Y, d.X) * 180 / Math.PI;
            if (a > 90.01) a -= 180;
            if (a <= -89.99) a += 180;
            return a;
        }

        /// <summary>A short text by a point: tried round it at a few distances, kept where it is clearest.</summary>
        static void PlaceLabel(Clutter clutter, P2 p, string s, double h, double[] radii, Action<P2, string, double, string, double> text)
        {
            double best = double.MaxValue; P2 at = p; string just = "BL";
            foreach (var r in radii)
                for (var k = 0; k < 8; k++)
                {
                    var a = k * Math.PI / 4 + Math.PI / 8;
                    double c = Math.Cos(a), sn = Math.Sin(a);
                    var q = p + new P2(c, sn) * r;
                    var j = (sn > 0.3 ? "B" : sn < -0.3 ? "T" : "M") + (c > 0.3 ? "L" : c < -0.3 ? "R" : "C");
                    var cost = clutter.Cost(new List<(P2, P2)>(), new[] { TextBox(q, s, h, j, 0, 0.4) }) + r * 0.05;
                    if (cost < best) { best = cost; at = q; just = j; }
                }
            text(at, s, h, just, 0);
        }

        /// <summary>
        /// A text along one of the legs, on the clearer leg, side and place, the longer legs first;
        /// an optional one is left out when it cannot keep clear of the rest.
        /// </summary>
        static void AlongLeg(Clutter clutter, List<(P2 A, P2 B)> legs, string s, double h, Action<P2, string, double, string, double> text, bool optional = false)
        {
            double best = double.MaxValue; P2 at = default; string just = null; double angle = 0;
            foreach (var (a, b) in legs)
            {
                var d = (b - a).Unit();
                var ang = Upright(d);
                var up = new P2(-Math.Sin(ang * Math.PI / 180), Math.Cos(ang * Math.PI / 180));
                foreach (var off in new[] { 1.6, 4.5, 7.5 })
                foreach (var t in new[] { 0.5, 0.4, 0.6, 0.3, 0.7, 0.2, 0.8, 0.1, 0.9 })
                    foreach (var side in new[] { 1, -1 })
                    {
                        var p = a + (b - a) * t + up * (side * off);
                        var j = side > 0 ? "BC" : "TC";
                        var cost = clutter.Cost(new List<(P2, P2)>(), new[] { TextBox(p, s, h, j, ang, 1.6) }) + Math.Abs(t - 0.5) * 0.2 - 0.01 * (b - a).Length + (off - 1.6) * 0.08;
                        if (cost < best) { best = cost; at = p; just = j; angle = ang; }
                    }
            }
            if (optional && best > 1.5) return;
            text(at, s, h, just, angle);
        }

        /// <summary>Whether two segments cross inside both, and where along each (0 to 1).</summary>
        static bool Intersect(P2 a, P2 b, P2 c, P2 d, out double t, out double u)
        {
            t = u = 0;
            var r = b - a; var sv = d - c;
            var den = r.X * sv.Y - r.Y * sv.X;
            if (Math.Abs(den) < 1e-9) return false;
            var ac = c - a;
            t = (ac.X * sv.Y - ac.Y * sv.X) / den;
            u = (ac.X * r.Y - ac.Y * r.X) / den;
            return t > 1e-6 && t < 1 - 1e-6 && u > 1e-6 && u < 1 - 1e-6;
        }

        /// <summary>How badly the legs of a layout cross or lie on top of each other.</summary>
        static int Crossings(P2[] q)
        {
            var c = 0;
            for (var i = 0; i < q.Length - 1; i++)
                for (var j = i + 2; j < q.Length - 1; j++)
                {
                    if (Intersect(q[i], q[i + 1], q[j], q[j + 1], out _, out _)) { c++; continue; }
                    // parallel and close: one leg drawn over the other
                    var di = (q[i + 1] - q[i]).Unit(); var dj = (q[j + 1] - q[j]).Unit();
                    if (Math.Abs(di.X * dj.Y - di.Y * dj.X) > 0.05) continue;
                    var off = q[j] - q[i];
                    if (Math.Abs(off.X * di.Left.X + off.Y * di.Left.Y) > 2) continue;
                    var e = q[j + 1] - q[i];
                    double a1 = (q[i + 1] - q[i]).Length;
                    double b0 = off.X * di.X + off.Y * di.Y, b1 = e.X * di.X + e.Y * di.Y;
                    if (Math.Min(a1, Math.Max(b0, b1)) - Math.Max(0, Math.Min(b0, b1)) > 0.5) c += 3;
                }
            return c;
        }

        /// <summary>A block of text lines at the end of a line: tried around the end point, kept where it is clearest, with a leader.</summary>
        static void PlaceBlock(Clutter clutter, P2 end, P2 outward, string[] lines, double h, Action<string, JObject> cmd, Action<P2, string, double, string, double> text)
        {
            var gap = 1.5;
            var bw = lines.Max(l => Width(l, h)); var bh = lines.Length * h + (lines.Length - 1) * gap;
            double best = double.MaxValue; P2 bestCorner = default; P2 bestHook = default;
            var baseAngle = Math.Atan2(outward.Y, outward.X);
            for (var i = 0; i < 16; i++)
            {
                var ang = baseAngle + (i % 2 == 0 ? 1 : -1) * ((i + 1) / 2) * Math.PI / 8;
                foreach (var r in new[] { 9.0, 15.0, 22.0 })
                {
                    var hook = end + new P2(Math.Cos(ang), Math.Sin(ang)) * r;
                    // the block hangs off the hook on the side it points to
                    var c = Math.Cos(ang); var sn = Math.Sin(ang);
                    var x0 = c > 0.3 ? hook.X : c < -0.3 ? hook.X - bw : hook.X - bw / 2;
                    var y0 = sn > 0.3 ? hook.Y : sn < -0.3 ? hook.Y - bh : hook.Y - bh / 2;
                    x0 += c * 1.2; y0 += sn * 1.2;
                    var box = Quad(new P2(x0 - 0.8, y0 - 0.8), new P2(x0 + bw + 0.8, y0 + bh + 0.2 * h + 0.8));
                    var leader = new List<(P2, P2)> { (end + (hook - end).Unit() * 2, hook) };
                    var cost = clutter.Cost(leader, new[] { box }) + r * 0.04 + (i / 2) * 0.05;
                    if (cost < best) { best = cost; bestCorner = new P2(x0, y0); bestHook = hook; }
                }
            }
            var from = end + (bestHook - end).Unit() * 2;
            cmd("line", new JObject { ["points"] = new JArray(new JArray(Math.Round(from.X, 2), Math.Round(from.Y, 2)), new JArray(Math.Round(bestHook.X, 2), Math.Round(bestHook.Y, 2))) });
            clutter.Segs.Add((from, bestHook));
            for (var i = 0; i < lines.Length; i++)
                text(new P2(bestCorner.X, bestCorner.Y + (lines.Length - 1 - i) * (h + gap)), lines[i], h, "BL", 0);
        }

        static double Width(string s, double h) => (s.Length * 0.72 + 0.2) * h;

        static P2[] Quad(P2 a, P2 b) => new[] { a, new P2(b.X, a.Y), b, new P2(a.X, b.Y) };

        /// <summary>The box a text covers, from its insertion point, justification and angle.</summary>
        static P2[] TextBox(P2 at, string s, double h, string just, double angle, double pad = 0.7)
        {
            var w = Width(s, h);
            double x0 = just.EndsWith("L") ? 0 : just.EndsWith("R") ? -w : -w / 2;
            double y0 = just.StartsWith("B") ? 0 : just.StartsWith("T") ? -h : -h / 2;
            // osifont draws its capitals about 15 % taller than the nominal height
            y0 -= 0.1 * h; h *= 1.2;
            var c = Math.Cos(angle * Math.PI / 180); var sn = Math.Sin(angle * Math.PI / 180);
            P2 R(double x, double y) => new P2(at.X + x * c - y * sn, at.Y + x * sn + y * c);
            return new[] { R(x0 - pad, y0 - pad), R(x0 + w + pad, y0 - pad), R(x0 + w + pad, y0 + h + pad), R(x0 - pad, y0 + h + pad) };
        }

        /// <summary>What is already on the drawing, for placing the labels where they cross least.</summary>
        class Clutter
        {
            public readonly List<(P2 A, P2 B)> Segs = new List<(P2, P2)>();
            public readonly List<P2[]> Boxes = new List<P2[]>();
            public double X0, Y0, X1, Y1;

            public double Cost(IEnumerable<(P2 A, P2 B)> segs, IEnumerable<P2[]> boxes)
            {
                double c = 0;
                foreach (var s in segs)
                {
                    if (Out(s.A) || Out(s.B)) c += 4;
                    foreach (var t in Segs)
                    {
                        if (Intersect(s.A, s.B, t.A, t.B, out _, out _)) c += 1;
                        else if (SegDist(s.A, s.B, t.A, t.B) < 0.6) c += 0.5;
                    }
                    foreach (var b in Boxes) if (Hits(s.A, s.B, b)) c += 2;
                }
                foreach (var b in boxes)
                {
                    if (b.Any(Out)) c += 4;
                    foreach (var t in Segs) if (Hits(t.A, t.B, b)) c += 3;
                    foreach (var o in Boxes) if (Overlap(b, o)) c += 3;
                }
                return c;
            }

            bool Out(P2 p) => p.X < X0 || p.X > X1 || p.Y < Y0 || p.Y > Y1;

            static double SegDist(P2 a, P2 b, P2 c, P2 d) => Math.Min(Math.Min(Dist(a, c, d), Dist(b, c, d)), Math.Min(Dist(c, a, b), Dist(d, a, b)));

            /// <summary>Whether a segment touches a box: an end inside it, or crossing an edge.</summary>
            static bool Hits(P2 a, P2 b, P2[] q)
            {
                if (Inside(a, q) || Inside(b, q)) return true;
                for (var i = 0; i < 4; i++) if (Intersect(a, b, q[i], q[(i + 1) % 4], out _, out _)) return true;
                return false;
            }

            static bool Overlap(P2[] a, P2[] b)
            {
                if (a.Any(p => Inside(p, b)) || b.Any(p => Inside(p, a))) return true;
                for (var i = 0; i < 4; i++) if (Hits(a[i], a[(i + 1) % 4], b)) return true;
                return false;
            }

            static double Dist(P2 p, P2 a, P2 b)
            {
                var ab = b - a; var l2 = ab.X * ab.X + ab.Y * ab.Y;
                var t = l2 < 1e-12 ? 0 : Math.Max(0, Math.Min(1, ((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / l2));
                return (p - (a + ab * t)).Length;
            }

            static bool Inside(P2 p, P2[] q)
            {
                bool sign = false;
                for (var i = 0; i < 4; i++)
                {
                    var a = q[i]; var b = q[(i + 1) % 4];
                    var cross = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
                    if (i == 0) sign = cross > 0;
                    else if ((cross > 0) != sign) return false;
                }
                return true;
            }
        }

        /// <summary>The route cut at a point on it: from the point on (<paramref name="fromStart"/>), or up to it.</summary>
        static List<Vec3> Cut(List<Vec3> path, Vec3 p, bool fromStart)
        {
            if (path.Count < 2) return path;
            var best = 0; var bd = double.MaxValue;
            for (var i = 0; i < path.Count - 1; i++)
            {
                var a = path[i]; var ab = path[i + 1] - a; var l2 = Vec3.Dot(ab, ab);
                var t = l2 < 1e-9 ? 0 : Math.Max(0, Math.Min(1, Vec3.Dot(p - a, ab) / l2));
                var d = (p - (a + ab * t)).Length;
                if (d < bd) { bd = d; best = i; }
            }
            if (bd > 50) return path;
            return fromStart ? new[] { p }.Concat(path.Skip(best + 1)).ToList() : path.Take(best + 1).Concat(new[] { p }).ToList();
        }

        /// <summary>The route without repeated points and with the points along a straight leg dropped.</summary>
        static List<Vec3> Simplify(List<Vec3> path)
        {
            var r = new List<Vec3>();
            foreach (var p in path)
                if (r.Count == 0 || (p - r[r.Count - 1]).Length > 1) r.Add(p);
            for (var i = r.Count - 2; i >= 1; i--)
                if (Vec3.Dot((r[i] - r[i - 1]).Normalized(), (r[i + 1] - r[i]).Normalized()) > 0.9999) r.RemoveAt(i);
            return r;
        }

        static List<KeyValuePair<string, string>> LineData(PipeRun run, string from, string to, DesignBasis d)
        {
            var list = new List<KeyValuePair<string, string>>();
            void Add(string k, string v) { if (!string.IsNullOrEmpty(v)) list.Add(new KeyValuePair<string, string>(k, v)); }
            Add("Stream", run.Tag);
            Add("From", from);
            Add("To", to);
            Add("Size", run.Size + "  OD " + F(run.OuterDiameter, "0.0") + " x " + F(run.Wall, "0.00") + " mm");
            Add("Phase", run.Phase);
            if (!double.IsNaN(d.OperatingT)) Add("Operating temperature (C)", F(d.OperatingT, "0.0"));
            if (!double.IsNaN(d.OperatingP)) Add("Operating pressure (bar g)", F(d.OperatingP, "0.00"));
            Add("Design temperature (C)", d.Cold ? F(d.MinT, "0") + " / " + F(d.MaxT, "0") : F(d.MaxT, "0"));
            Add("Design pressure (bar g)", F(d.Pressure, "0.0"));
            Add("Hydrotest pressure (bar g)", F(d.Test, "0.0"));
            Add("Material", d.Pipe);
            Add("Flange rating", d.Class + " RF");
            Add("Insulation", run.Insulation > 0 ? F(run.Insulation, "0") + " mm " + run.InsulationService : "none");
            if (run.MassFlow > 0) Add("Mass flow (kg/h)", F(run.MassFlow * 3600, "0.##"));
            if (run.Velocity > 0) Add("Velocity (m/s)", F(run.Velocity, "0.00"));
            var h = run.Hydraulics;
            if (h != null && !double.IsNaN(h.Total)) Add("Pressure drop (bar)", F(h.Total, "0.000"));
            return list;
        }

        /// <summary>The bill of materials: pipe by length, elbows, flanges, gaskets, bolts and insulation.</summary>
        static List<KeyValuePair<string, string>> Materials(PipeRun run, double[] lengths, int elbows, int blinds, DesignBasis d)
        {
            var list = new List<KeyValuePair<string, string>>();
            void Add(string k, string v) => list.Add(new KeyValuePair<string, string>(k, v));
            var size = run.Size;
            var length = lengths.Sum() / 1000;
            var joints = 2;
            Add("Pipe " + size + " SMLS BE " + d.Pipe + ", " + F(run.Wall, "0.00") + " mm", F(Math.Ceiling(length * 10) / 10, "0.0") + " m");
            if (elbows > 0) Add("Elbow 90 LR " + size + " BW " + d.Fitting, elbows.ToString(CultureInfo.InvariantCulture));
            if (joints > 0) Add("Flange WN RF " + size + " " + d.Class + " " + d.Flange, joints.ToString(CultureInfo.InvariantCulture));
            if (blinds > 0) Add("Blind flange RF " + size + " " + d.Class + " " + d.Flange, blinds.ToString(CultureInfo.InvariantCulture));
            if (joints > 0)
            {
                Add("Gasket spiral wound " + size + " " + d.Class, joints.ToString(CultureInfo.InvariantCulture));
                Add("Stud bolt set " + size + " " + d.Class, joints.ToString(CultureInfo.InvariantCulture));
            }
            if (run.Insulation > 0) Add("Insulation " + run.InsulationService + " " + F(run.Insulation, "0") + " mm, " + size, F(Math.Ceiling(length * 10) / 10, "0.0") + " m");
            return list;
        }
    }
}
